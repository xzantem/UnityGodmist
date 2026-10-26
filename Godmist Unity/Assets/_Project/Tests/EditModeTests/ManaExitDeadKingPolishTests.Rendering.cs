using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class ManaExitDeadKingPolishTests
    {
        [Test]
        public void ForestGateClosesAtTheSameBoundaryAndOpensAfterClearing()
        {
            Directory.CreateDirectory(Output);
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "forest-exit", SeedOverride = 261026 };
            fixture.FrameProbe = (bootstrap, game) =>
            {
                var floor = game.ActiveDungeon.Floors[0];
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var lighting = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var camera = Read<Camera>(lighting, "worldCamera");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var final = floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Final);
                var exit = Object.Instantiate(Prefab, bootstrap.transform);
                exit.Initialize(party, map.ResolveForestTransitionPosition(floor, map.CellToWorldCenter(final.LayoutPosition), DungeonRoomType.Final));
                typeof(DungeonSandboxBootstrap).GetMethod("RegisterTransitionLighting", Private).Invoke(bootstrap, new object[] { exit });
                exit.ConfigureExitState(game.ActiveDungeon);
                var marker = Read<SpriteRenderer>(exit, "standingRenderer");
                Assert.That(marker, Is.Not.Null);
                floor.Fog.SetVisible(floor.Map.FloorCells.Select(c => new Godmist.Runtime.Dungeons.DungeonTraversalPosition(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(c))));
                map.ApplyFog(floor);
                typeof(DungeonExitView).GetMethod("LateUpdate", Private).Invoke(exit, null);
                Assert.That(marker.forceRenderingOff, Is.False, "Locked gate must remain visible.");
                foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                    foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
                var target = new RenderTexture(800, 600, 24);
                var image = new Texture2D(800, 600, TextureFormat.RGBA32, false);
                var previousTarget = camera.targetTexture; var previousPosition = camera.transform.position;
                var previousSize = camera.orthographicSize; var previousMask = camera.cullingMask; var active = RenderTexture.active;
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30; camera.orthographicSize = 3;
                    camera.transform.position = new Vector3(marker.bounds.center.x, marker.bounds.center.y, -10);
                    var closedPosition = marker.transform.position; var closedBounds = marker.bounds;
                    foreach (var state in new[] { "closed", "open" })
                    {
                        if (state == "open")
                        {
                            foreach (var guard in floor.Encounters.Where(e => e.RoomId == final.RoomId)) game.ActiveDungeon.TryMarkEncounterCleared(guard.InstanceId);
                            typeof(DungeonExitView).GetMethod("LateUpdate", Private).Invoke(exit, null);
                            Assert.That(exit.IsExitBlocked, Is.False);
                            Assert.That(marker.transform.position, Is.EqualTo(closedPosition));
                            Assert.That(marker.bounds, Is.EqualTo(closedBounds), "Closing the leaf cannot move posts or fences, including the sprite pivot.");
                        }
                        typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting", Private).Invoke(lighting, new object[] { Time.time + (state == "open" ? 4 : 3) });
                        SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(Output, "forest-gate-" + state + "-context.png"), image.EncodeToPNG());
                    }
                }
                finally
                {
                    camera.targetTexture = previousTarget; camera.transform.position = previousPosition;
                    camera.orthographicSize = previousSize; camera.cullingMask = previousMask; RenderTexture.active = active;
                    Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(exit.gameObject);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Short);
        }

        [TestCase(false)] [TestCase(true)]
        public void ProductionStairsUseFloorLightingAndRespondToTorch(bool upwards)
        {
            Directory.CreateDirectory(Output);
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "stairs", SeedOverride = 261026 };
            fixture.FrameProbe = (bootstrap, game) =>
            {
                var lighting = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var camera = Read<Camera>(lighting, "worldCamera");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var stair = Object.Instantiate(Prefab, bootstrap.transform);
                stair.InitializeTransition(party, party.ActiveMemberViews.First().Position + Vector2.right, () => { }, goesUp: upwards);
                typeof(DungeonSandboxBootstrap).GetMethod("RegisterTransitionLighting", Private).Invoke(bootstrap, new object[] { stair });
                var marker = Read<SpriteRenderer>(stair, "transitionRenderer");
                Assert.That(map.IsGroundLightingPresentation(marker), Is.True);
                foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                    foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
                var target = new RenderTexture(640, 480, 24);
                var image = new Texture2D(640, 480, TextureFormat.RGBA32, false);
                var active = RenderTexture.active; var previousTarget = camera.targetTexture;
                var previousPosition = camera.transform.position; var previousSize = camera.orthographicSize;
                var previousMask = camera.cullingMask; var previousClear = camera.clearFlags; var previousBackground = camera.backgroundColor;
                Material saved = null;
                try
                {
                    var direction = upwards ? "up" : "down";
                    camera.targetTexture = target; camera.cullingMask = 1 << 30; camera.orthographicSize = 1.8f;
                    camera.transform.position = new Vector3(stair.transform.position.x, stair.transform.position.y, -10);
                    marker.forceRenderingOff = false;
                    typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting", Private).Invoke(lighting, new object[] { Time.time + 2f });
                    Assert.That(marker.sharedMaterial.shader.name, Is.EqualTo("Godmist/Dungeon/Silhouette Floor"));
                    Color[] Render(string name)
                    {
                        SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
                        return image.GetPixels();
                    }
                    Render("stairs-" + direction + "-context");
                    saved = new Material(marker.sharedMaterial);
                    marker.gameObject.layer = 31; camera.cullingMask = 1 << 31;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    marker.sharedMaterial.SetInt("_TorchCount", 0);
                    var dark = Render("stairs-" + direction + "-ambient");
                    marker.sharedMaterial.SetInt("_TorchCount", 1);
                    marker.sharedMaterial.SetVectorArray("_Torches", Enumerable.Repeat(new Vector4(stair.transform.position.x, stair.transform.position.y + 1, 5, 1), 8).ToArray());
                    marker.sharedMaterial.SetVectorArray("_TorchColors", Enumerable.Repeat(new Vector4(1.45f, .55f, .14f, 1), 8).ToArray());
                    var lit = Render("stairs-" + direction + "-torch");
                    float L(Color c) => c.r * .2126f + c.g * .7152f + c.b * .0722f;
                    var pixels = Enumerable.Range(0, dark.Length).Where(i => L(dark[i]) > .005f).ToArray();
                    Assert.That(pixels.Length, Is.GreaterThan(100));
                    var ambient = pixels.Average(i => L(dark[i])); var torch = pixels.Average(i => L(lit[i]));
                    File.WriteAllText(Path.Combine(Output, "stairs-" + direction + "-lighting.txt"), $"ambient={ambient:F5}; torch={torch:F5}; ratio={torch/ambient:F3}; shader={marker.sharedMaterial.shader.name}");
                    Assert.That(torch, Is.GreaterThan(ambient * 1.15f), "Stair pixels must react to light, not merely use a named material.");
                    marker.sharedMaterial.CopyPropertiesFromMaterial(saved);
                    marker.gameObject.layer = 30; camera.cullingMask = 1 << 30;
                    camera.clearFlags = previousClear; camera.backgroundColor = previousBackground;
                    if (!upwards)
                    {
                        Write(stair, "requiresClearedExitRoom", true); stair.ConfigureExitState(game.ActiveDungeon);
                        Assert.That(stair.IsExitBlocked, Is.True);
                        typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting", Private).Invoke(lighting, new object[] { Time.time + 3f });
                        Render("stairs-closed-context");
                    }
                }
                finally
                {
                    if (saved != null) { marker.sharedMaterial.CopyPropertiesFromMaterial(saved); Object.DestroyImmediate(saved); }
                    camera.targetTexture = previousTarget; camera.transform.position = previousPosition;
                    camera.orthographicSize = previousSize; camera.cullingMask = previousMask;
                    camera.clearFlags = previousClear; camera.backgroundColor = previousBackground;
                    RenderTexture.active = active; Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(stair.gameObject);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.catacombs.expedition", DungeonLength.Short);
        }
    }
}
