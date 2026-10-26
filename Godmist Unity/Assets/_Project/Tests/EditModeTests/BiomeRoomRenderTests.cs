using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Godmist.Tests.EditMode
{
    public class BiomeRoomRenderTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void NativeRoomAndConnectorPresentation(string id)
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/biome-room-authoring-20261023"));
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = output, StageOverride = "production-rooms", SeedOverride = 231023 };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = Field<DungeonFloorMapView>(bootstrap, "floorMapView");
                var rig = Field<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var camera = Field<Camera>(rig, "worldCamera");
                var floor = session.ActiveDungeon.Floors[0];
                var start = floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Start);
                var edge = floor.Connections.First(c => c.FirstRoomId == start.RoomId || c.SecondRoomId == start.RoomId);
                var oldTarget = camera.targetTexture; var oldMask = camera.cullingMask; var active = RenderTexture.active;
                var target = new RenderTexture(1440, 900, 24);
                var pixels = new Texture2D(1440, 900, TextureFormat.RGBA32, false);
                try
                {
                    foreach (var renderer in bootstrap.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true))) renderer.gameObject.layer = 30;
                    camera.targetTexture = target; camera.cullingMask = 1 << 30; camera.aspect = 1.6f;
                    var cellWidth = Vector2.Distance(map.CellToWorldCenter(Vector2Int.zero), map.CellToWorldCenter(Vector2Int.right));
                    camera.orthographicSize = 14 * cellWidth;
                    floor.Fog.SetVisible(floor.Map.Surfaces.Select(s => new DungeonTraversalPosition(s)));
                    void Capture(string label, Vector2Int cell)
                    {
                        var center = map.CellToWorldCenter(cell);
                        camera.transform.position = new Vector3(center.x, center.y, -10);
                        map.ConfigurePresentationCamera(camera);
                        map.ApplyFog(floor);
                        typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting", Private).Invoke(rig, new object[] { 1f });
                        SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output, id + "-native-" + label + ".png"), pixels.EncodeToPNG());
                        Assert.That(pixels.GetPixels32().Count(c => c.r > 8 || c.g > 8 || c.b > 8), Is.GreaterThan(10000), "Native render cannot be blank.");
                    }
                    Capture("connector", (edge.FirstDoorCell + edge.EndDoorCell) / 2);
                    var template = session.ActiveDungeon.Definition.RoomTemplateCatalog.AllTemplates.Single(t => t.Id == start.TemplateId);
                    Capture("entry", start.LayoutPosition + Godmist.Data.Dungeons.DungeonRoomTransformUtility.GetRotatedSize(template.SizeInCells, start.RotationDegrees) / 2);
                }
                finally
                {
                    camera.targetTexture = oldTarget; camera.cullingMask = oldMask; RenderTexture.active = active;
                    UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
                }
            };
            fixture.MeasureProductionFloorEntry(id, DungeonLength.Short);
        }
    }
}
