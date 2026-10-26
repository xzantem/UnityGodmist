using System.IO;
using Godmist.Runtime.Dungeons;
using System.Collections.Generic;
using System.Linq;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace Godmist.Tests.EditMode
{
    public class DungeonSimpleVisualTests
    {
        [TestCase(0, .75f)] [TestCase(1, .75f)] [TestCase(2, .75f)] [TestCase(3, .75f)]
        [TestCase(3, 1f)]
        public void ObstacleHealthStaysInsideFrameAndAboveActualArt(int hits, float uiScale)
        {
            var preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(
                "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            var root = new GameObject("Obstacle health render", typeof(Grid));
            var target = new RenderTexture(960, 640, 24);
            var pixels = new Texture2D(960, 640, TextureFormat.RGB24, false);
            var oldTarget = RenderTexture.active;
            try
            {
                var authored = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonFloorMapView>(true)).First();
                var mapView = root.AddComponent<DungeonFloorMapView>();
                var tileObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
                tileObject.transform.SetParent(root.transform, false);
                SetField(mapView, "floorTilemap", tileObject.GetComponent<Tilemap>());
                SetField(mapView, "floorTile", typeof(DungeonFloorMapView).GetField("floorTile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(authored));
                SetField(mapView, "wallSegmentPrefab", typeof(DungeonFloorMapView).GetField("wallSegmentPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(authored));
                var map = new DungeonFloorMap();
                for (var y = 0; y < 7; y++) for (var x = 0; x < 8; x++) map.TryAddFloorCell(new Vector2Int(x, y), "review");
                mapView.Rebuild(map);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonObstacle.prefab");
                var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Obstacle_Pillar.asset");
                var pillar = Object.Instantiate(prefab, root.transform).GetComponent<DungeonObstacleView>();
                var obstacle = new ObstacleDungeonObjectInstance("review", definition, new Vector2Int(3, 2));
                pillar.Initialize(obstacle, mapView.CellToWorldCenter(obstacle.Position));
                Physics2D.SyncTransforms();
                var resolver = typeof(DungeonObstacleView).Assembly.GetType("Godmist.Views.Dungeons.DungeonCatacombsObjectVisuals");
                Assert.That(resolver.GetMethod("TryApplyObstacle").Invoke(null, new object[] { pillar, definition }), Is.True);
                pillar.BindElevationView(mapView);
                var actorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png");
                var actor = CreateRenderActor(root.transform, actorSprite, map, mapView, new Vector2(4.3f, 2.4f));
                InvokePrivate(actor, "RefreshWorldDepthSorting");
                var cameraObject = new GameObject("Camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 3.2f;
                camera.transform.position = new Vector3(4, 2f, -10); camera.targetTexture = target; camera.aspect = 1.5f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 30; camera.enabled = false;
                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.scaleFactor = uiScale; canvas.pixelPerfect = true;
                canvas.sortingOrder = 32000;
                var widget = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ObstacleHealth.prefab"), canvas.transform);
                var bar = widget.GetComponent<RectTransform>();
                var slider = widget.GetComponent<UnityEngine.UI.Slider>();
                var label = widget.GetComponentInChildren<TMPro.TMP_Text>(true);
                var tooltip = root.AddComponent<DungeonTacticalTooltip>();
                SetField(tooltip, "worldCamera", camera); SetField(tooltip, "obstacleHealthRoot", bar);
                SetField(tooltip, "obstacleHealthSlider", slider); SetField(tooltip, "obstacleHealthText", label);
                tooltip.Bind(null, mapView, new DungeonObjectView[] { pillar });
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                var output = Path.GetFullPath("../LocalHandoffs/obstacle-health-20261020"); Directory.CreateDirectory(output);
                void Capture(string name)
                {
                    Canvas.ForceUpdateCanvases(); SortingGroup.UpdateAllSortingGroups(); camera.Render();
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                }
                if (hits == 3 && uiScale < 1)
                {
                    widget.SetActive(true); slider.maxValue = 3; slider.SetValueWithoutNotify(3); label.text = "3/3";
                    var screen = camera.WorldToScreenPoint((Vector3)mapView.CellToWorldCenter(obstacle.Position) + Vector3.up * .65f);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, camera, out var local);
                    bar.localPosition = local; Capture("before"); widget.SetActive(false);
                }
                obstacle.ApplyHits(3 - hits);
                var refresh = typeof(DungeonTacticalTooltip).GetMethod("RefreshObstacleHealth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                refresh.Invoke(tooltip, new object[] { obstacle });
                Canvas.ForceUpdateCanvases();
                if (hits == 0)
                {
                    Assert.That(widget.activeSelf, Is.False, "A destroyed obstacle must not leave a bar behind.");
                    return;
                }
                Assert.That(widget.activeSelf, Is.True);
                Assert.That(slider.value, Is.EqualTo(hits)); Assert.That(label.text, Is.EqualTo($"{hits}/3"));
                var area = (RectTransform)slider.fillRect.parent;
                Assert.That(area.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
                var fillCorners = new Vector3[4]; slider.fillRect.GetWorldCorners(fillCorners);
                foreach (var corner in fillCorners)
                {
                    var local = area.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(area.rect.xMin - .01f, area.rect.xMax + .01f));
                    Assert.That(local.y, Is.InRange(area.rect.yMin - .01f, area.rect.yMax + .01f));
                }
                Assert.That(pillar.TryGetPresentationBounds(out var bounds), Is.True);
                var corners = new Vector3[4]; bar.GetWorldCorners(corners);
                var bottom = camera.WorldToScreenPoint(corners[0]).y;
                var top = camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y)).y;
                Assert.That(bottom, Is.GreaterThanOrEqualTo(top + 7), "The bar belongs above the actual pillar, not at a fixed cell offset.");
                Capture($"after-{hits}-scale-{uiScale:0.00}");
                var areaBottomLeft = camera.WorldToScreenPoint(area.TransformPoint(area.rect.min));
                var areaTopRight = camera.WorldToScreenPoint(area.TransformPoint(area.rect.max));
                var redCount = 0;
                for (var y = Mathf.CeilToInt(bottom); y < bottom + 24 * uiScale; y++)
                    for (var x = Mathf.FloorToInt(camera.WorldToScreenPoint(corners[0]).x); x < camera.WorldToScreenPoint(corners[2]).x; x++)
                    {
                        var color = pixels.GetPixel(x, y);
                        if (color.r < .4f || color.r < color.g * 1.7f || color.r < color.b * 1.7f) continue;
                        redCount++;
                        Assert.That(x, Is.InRange(Mathf.FloorToInt(areaBottomLeft.x), Mathf.CeilToInt(areaTopRight.x)));
                        Assert.That(y, Is.InRange(Mathf.FloorToInt(areaBottomLeft.y), Mathf.CeilToInt(areaTopRight.y)));
                    }
                Assert.That(redCount, Is.GreaterThan(25), "The production fill must actually render visibly, inside the frame.");
                // Reopening after style-applier OnEnable must retain the presentation.
                widget.SetActive(false); refresh.Invoke(tooltip, new object[] { obstacle });
                Assert.That(label.fontSize, Is.EqualTo(16)); Assert.That(area.GetComponents<UnityEngine.UI.RectMask2D>().Length, Is.EqualTo(1));
            }
            finally
            {
                RenderTexture.active = oldTarget; Object.DestroyImmediate(root); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(true,true)]
        public void LowWallJunctionsOnNativeFloorRender(bool torchLit,bool forest)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            var root = new GameObject("Low masonry junction review", typeof(Grid));
            var target = new RenderTexture(1280, 960, 24);
            var pixels = new Texture2D(1280, 960, TextureFormat.RGBA32, false);
            var materials = new List<Material>();
            var previous = RenderTexture.active;
            try
            {
                var authored = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DungeonFloorMapView>(true)).Single();
                var view = root.AddComponent<DungeonFloorMapView>();
                var tilesObject = new GameObject("Native floor", typeof(Tilemap), typeof(TilemapRenderer));
                tilesObject.transform.SetParent(root.transform, false);
                var tiles = tilesObject.GetComponent<Tilemap>(); SetField(view, "floorTilemap", tiles);
                foreach (var field in new[] { "floorTile", "wallSegmentPrefab", "lowWallPrefab", "railingPrefab" })
                    SetField(view, field, typeof(DungeonFloorMapView).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(authored));
                view.ConfigureCatacombsArtPresentation(true);
                if(forest)view.ConfigureVegetationPresentation(UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonDefinition>(Godmist.Editor.ForestExpeditionsSetup.RegularPath).VegetationPresentation);
                var map = new DungeonFloorMap();
                for (var y = 0; y < 14; y++) for (var x = 0; x < 18; x++) map.TryAddFloorCell(new Vector2Int(x, y), "review");
                for (var x = 0; x < 18; x++)
                {
                    map.SetWallType(new DungeonCellEdge(new Vector2Int(x, -1), new Vector2Int(x, 0)), Godmist.Core.Enums.DungeonWallType.Full);
                    map.SetWallType(new DungeonCellEdge(new Vector2Int(x, 13), new Vector2Int(x, 14)), Godmist.Core.Enums.DungeonWallType.Full);
                }
                for (var y = 0; y < 14; y++)
                {
                    map.SetWallType(new DungeonCellEdge(new Vector2Int(-1, y), new Vector2Int(0, y)), Godmist.Core.Enums.DungeonWallType.Full);
                    map.SetWallType(new DungeonCellEdge(new Vector2Int(17, y), new Vector2Int(18, y)), Godmist.Core.Enums.DungeonWallType.Full);
                }
                void H(int x, int y) => map.SetWallType(new DungeonCellEdge(new Vector2Int(x, y - 1), new Vector2Int(x, y)), Godmist.Core.Enums.DungeonWallType.Low);
                void V(int x, int y) => map.SetWallType(new DungeonCellEdge(new Vector2Int(x - 1, y), new Vector2Int(x, y)), Godmist.Core.Enums.DungeonWallType.Low);
                // Isolated horizontal/vertical runs, all four turns, T and cross.
                H(1, 2); H(2, 2); H(3, 2); V(6, 1); V(6, 2); V(6, 3);
                H(1, 7); H(2, 7); V(3, 7); V(3, 8);
                H(5, 9); H(6, 9); V(7, 7); V(7, 8);
                H(9, 9); H(10, 9); V(9, 7); V(9, 8);
                H(13, 7); H(14, 7); V(13, 7); V(13, 8);
                H(9, 3); H(10, 3); H(11, 3); H(12, 3); V(11, 3); V(11, 4);
                H(14, 3); H(15, 3); V(15, 2); V(15, 3);
                H(0, 12); H(1, 12); H(2, 12); V(3, 12); V(3, 13);
                var revision = map.TraversalRevision;
                view.Rebuild(map);
                Assert.That(map.TraversalRevision, Is.EqualTo(revision));
                if(!forest)
                {
                var westMeetingWall = view.GetComponentsInChildren<DungeonWallSegmentView>()
                    .Single(w => Vector2.Distance(w.transform.position, new Vector2(.5f, 12)) < .001f);
                Assert.That(westMeetingWall.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled)
                    .Min(r => r.bounds.min.x), Is.EqualTo(DungeonWallSegmentView.NativeWallWidthWorld * .5f).Within(.001f),
                    "Low masonry must meet the inside of the side wall, without protruding into the void.");
                var northMeetingWall = view.GetComponentsInChildren<DungeonWallSegmentView>()
                    .Single(w => Vector2.Distance(w.transform.position, new Vector2(3, 13.5f)) < .001f);
                Assert.That(northMeetingWall.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled)
                    .Max(r => r.bounds.max.y), Is.EqualTo(14).Within(.001f),
                    "The raised cap must end at the tall north wall face, without painting a post over it.");
                var verticalRun = view.GetComponentsInChildren<DungeonWallSegmentView>()
                    .Where(w => Mathf.Abs(w.transform.position.x - 6) < .001f
                        && w.transform.position.y > 1 && w.transform.position.y < 4).ToArray();
                Assert.That(verticalRun.Length, Is.EqualTo(3));
                Assert.That(verticalRun.SelectMany(w => w.GetComponentsInChildren<SpriteRenderer>())
                    .Count(r => r.name == "Cropped native low wall" && r.enabled), Is.EqualTo(3),
                      "Each low segment retains its native side profile.");
                foreach (var wall in verticalRun)
                {
                    var profile = wall.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled&&r.name=="Cropped native low wall");
                    Assert.That(wall.GetComponent<BoxCollider2D>(), Is.Not.Null,
                        "The existing gameplay collider remains owned by the wall prefab.");
                    Assert.That(profile.bounds.size.x, Is.EqualTo(8f / profile.sprite.pixelsPerUnit).Within(.001f));
                    Assert.That(profile.sprite.rect.height, Is.EqualTo(32),"The top strip keeps native vertical tiling phase.");
                }
                  Assert.That(verticalRun.SelectMany(w=>w.GetComponentsInChildren<SpriteRenderer>())
                      .Count(r=>r.enabled&&r.name=="Native low wall south face"),Is.EqualTo(1),
                      "Only the exposed south end owns a standing face; continuation cells must not repeat it.");
                  var ordered=verticalRun.Select(w=>w.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.enabled&&r.name=="Cropped native low wall"))
                      .OrderBy(r=>r.bounds.min.y).ToArray();
                  for(var i=1;i<ordered.Length;i++)Assert.That(ordered[i-1].bounds.max.y,Is.EqualTo(ordered[i].bounds.min.y).Within(.001f),"Vertical cap tiling cannot leave gaps between cells.");
                }
                else
                {
                    var hedges=view.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Crossable forest low hedge"||r.name=="Forest low hedge native join").ToArray();
                    Assert.That(hedges.Count(r=>r.name=="Crossable forest low hedge"),Is.EqualTo(map.SurfaceWallEdges.Count(e=>map.GetWallType(e)==Godmist.Core.Enums.DungeonWallType.Low)));
                    foreach(var hedge in hedges){hedge.enabled=true;hedge.forceRenderingOff=false;}
                    // Isolate the real hedge renderers so tree crowns cannot
                    // conceal the L/T/cross being reviewed.
                    foreach(var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))if(!hedges.Contains(renderer))renderer.enabled=false;
                    tiles.GetComponent<TilemapRenderer>().enabled=false;
                }
                foreach (var renderer in view.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                if (torchLit)
                {
                    Material Lit(string shader)
                    {
                        var material = new Material(Shader.Find(shader)); materials.Add(material);
                        material.SetFloat("_Ambient", .22f); material.SetFloat("_ReceiveShadows", 0);
                        material.SetInt("_TorchCount", 3);
                        material.SetVectorArray("_Torches", new[] { new Vector4(4, 6, 7, 1), new Vector4(11, 6, 7, 1), new Vector4(16, 3, 6, 1), Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero });
                        material.SetVectorArray("_TorchColors", Enumerable.Repeat(new Vector4(1.45f, .55f, .14f, 1), 8).ToArray());
                        return material;
                    }
                    var wallMaterial = Lit("Godmist/Dungeon/Torch Wall");
                    var floorMaterial = Lit("Godmist/Dungeon/Silhouette Floor");
                    foreach (var wall in view.LightingWallRenderers) wall.sharedMaterial = wallMaterial;
                    tiles.GetComponent<TilemapRenderer>().sharedMaterial = floorMaterial;
                }
                var cameraObject = new GameObject("Review camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.aspect = 4f / 3;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                var output = Path.GetFullPath("../LocalHandoffs/low-wall-joins-20261023");Directory.CreateDirectory(output);
                void Capture(string name, Vector3 center, float size)
                {
                    camera.transform.position = new Vector3(center.x, center.y, -10); camera.orthographicSize = size;
                    SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, (forest?"forest-":"")+name + ".png"), pixels.EncodeToPNG());
                    Assert.That(pixels.GetPixels32().Select(c => c.r).Distinct().Count(), Is.GreaterThan(20));
                }
                Capture(torchLit ? "junctions-lit" : "junctions-native", new Vector3(9, 6, 0), 7.25f);
                Capture(torchLit ? "junction-close-lit" : "junction-close-native", new Vector3(3.5f, 7.5f, 0), 2.5f);
                Capture(torchLit ? "vertical-close-lit" : "vertical-close-native", new Vector3(6, 2.875f, 0), 2.125f);
                Capture(torchLit ? "horizontal-close-lit" : "horizontal-close-native", new Vector3(2.5f, 2.25f, 0), 1.75f);
                var actorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png");
                Assert.That(actorSprite, Is.Not.Null);
                foreach (var position in new[] { new Vector2(2.2f, 6.1f), new Vector2(1.2f, 7.2f) })
                {
                    var actor = CreateRenderActor(root.transform, actorSprite, map, view, position);
                    actor.gameObject.layer = 30;
                    InvokePrivate(actor, "RefreshWorldDepthSorting");
                    if (torchLit) actor.GetComponent<SpriteRenderer>().sharedMaterial = materials[0];
                }
                Capture(torchLit ? "low-wall-actor-scale-lit" : "low-wall-actor-scale-native", new Vector3(3.5f, 7, 0), 3.5f);
                Capture(torchLit ? "low-wall-full-wall-lit" : "low-wall-full-wall-native", new Vector3(3, 12.5f, 0), 3.5f);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                foreach (var material in materials) Object.DestroyImmediate(material);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void CatacombsLowWallsAndBalconyProductionRender(bool withStairs)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            var root = new GameObject("Structures render",typeof(Grid));
            var target = new RenderTexture(1024,768,24);
            var pixels = new Texture2D(1024,768,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            try
            {
                var authored = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DungeonFloorMapView>(true)).Single();
                var view = root.AddComponent<DungeonFloorMapView>();
                var tilesObject = new GameObject("Render floor",typeof(Tilemap),typeof(TilemapRenderer));tilesObject.transform.SetParent(root.transform,false);
                var tiles=tilesObject.GetComponent<Tilemap>();SetField(view,"floorTilemap",tiles);
                foreach(var field in new[]{"floorTile","wallSegmentPrefab","lowWallPrefab","railingPrefab"})
                    SetField(view,field,typeof(DungeonFloorMapView).GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(authored));
                view.ConfigureCatacombsArtPresentation(true);
                var map = new DungeonFloorMap();
                for(var y=0;y<10;y++)for(var x=0;x<13;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                for(var x=1;x<4;x++)map.SetWallType(new DungeonCellEdge(new Vector2Int(x,2),new Vector2Int(x,3)),Godmist.Core.Enums.DungeonWallType.Low);
                for(var y=2;y<5;y++)map.SetWallType(new DungeonCellEdge(new Vector2Int(5,y),new Vector2Int(6,y)),Godmist.Core.Enums.DungeonWallType.Low);
                for(var y=5;y<8;y++)for(var x=8;x<11;x++)map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(x,y),1),"balcony");
                if(withStairs)Assert.That(map.TryAddStairs(new DungeonSurfacePosition(new Vector2Int(9,2)),
                    new DungeonSurfacePosition(new Vector2Int(9,5),1),new[]{new Vector2Int(9,2),new Vector2Int(9,3),new Vector2Int(9,4),new Vector2Int(9,5)}),Is.True);
                var revision=map.TraversalRevision;
                view.Rebuild(map);
                Assert.That(map.TraversalRevision,Is.EqualTo(revision));
                var faces=view.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Catacombs balcony stone face").ToArray();
                var caps=view.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Catacombs balcony stone cap").ToArray();
                Assert.That(faces.Length,Is.EqualTo(withStairs?2:3),"Only the south-facing platform edges have a vertical face; stair arrival stays open.");
                Assert.That(caps.Length,Is.EqualTo(withStairs?11:12));
                foreach(var face in faces)
                {
                    Assert.That(view.LightingWallRenderers,Does.Contain(face));
                    Assert.That(view.LightingPresentationRenderers.Contains(face),Is.False);
                    Assert.That(face.transform.eulerAngles.z,Is.EqualTo(0).Within(.001f));
                    Assert.That(face.bounds.size.y,Is.EqualTo(.6f).Within(.001f));
                    Assert.That(face.drawMode,Is.EqualTo(SpriteDrawMode.Tiled));
                    Assert.That(face.bounds.max.y,Is.EqualTo(view.CellToWorldCenter(new Vector2Int(8,5)).y-.5f).Within(.001f));
                }
                foreach(var cap in caps)
                {
                    Assert.That(view.LightingPresentationRenderers,Does.Contain(cap));
                    Assert.That(view.PreservesGroundPresentationSorting(cap),Is.True);
                }
                foreach(var renderer in view.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
                tiles.gameObject.layer=30;
                var cameraObject=new GameObject("Structures render camera",typeof(Camera));
                cameraObject.transform.SetParent(root.transform,false);
                var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.5f;camera.aspect=4f/3;
                var center=view.CellToWorldCenter(new Vector2Int(6,4));camera.transform.position=new Vector3(center.x,center.y,-10);
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.065f,.05f);
                camera.targetTexture=target;SortingGroup.UpdateAllSortingGroups();camera.Render();
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1024,768),0,0);pixels.Apply();
                var output=Path.GetFullPath("../LocalHandoffs/low-walls-balconies-20261020");Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output,withStairs?"structures-stairs.png":"structures.png"),pixels.EncodeToPNG());
                Assert.That(pixels.GetPixels32().Select(c=>c.r).Distinct().Count(),Is.GreaterThan(20));
            }
            finally
            {
                RenderTexture.active=previous;
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void LowWallFaceAndCapPreserveColliderAndReuseTheirRenderers(bool vertical)
        {
            var root=new GameObject("Low wall profiles",typeof(Grid));
            var tileObject=new GameObject("Tiles",typeof(Tilemap));tileObject.transform.SetParent(root.transform,false);
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab");
            var wall=Object.Instantiate(prefab,root.transform);
            try
            {
                var edge=new DungeonCellEdge(new Vector2Int(3,4),new Vector2Int(3,4)+(vertical?Vector2Int.right:Vector2Int.up));
                wall.gameObject.layer=14;
                wall.Bind(edge,tileObject.GetComponent<Tilemap>());
                var collider=wall.GetComponent<BoxCollider2D>();Physics2D.SyncTransforms();
                var bounds=collider.bounds;var size=collider.size;var offset=collider.offset;
                var position=wall.transform.position;var scale=wall.transform.localScale;var rotation=wall.transform.rotation;
                var art=Resources.Load<Sprite>("CatacombsAudit/catacombs-low-wall-sheet-v1");
                Assert.That(wall.ConfigureCatacombsLowWall(edge,art),Is.True);
                var first=wall.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).ToArray();
                Assert.That(first.Length,Is.EqualTo(3));
                Assert.That(first.All(r=>r.gameObject.layer==14),Is.True);
                var outline = first.Single(r => r.name == "Catacombs low wall foot outline");
                Assert.That(outline.color, Is.EqualTo((Color)new Color32(0, 0, 0, 70)));
                Assert.That(outline.bounds.size.y, Is.EqualTo(1f / 32).Within(.0001f));
                var combined=first[0].bounds;foreach(var renderer in first)combined.Encapsulate(renderer.bounds);
                Assert.That(wall.TryGetVisualBounds(out var reported),Is.True);
                Assert.That(reported,Is.EqualTo(combined));
                Assert.That(combined.min.y,Is.EqualTo(position.y-(vertical?wall.VisualLength*.5f:0)-1f/32).Within(.001f));
                Assert.That(combined.size.y,Is.EqualTo((vertical?wall.VisualLength+DungeonWallSegmentView.CatacombsLowWallFrontHeightWorld:DungeonWallSegmentView.CatacombsLowWallFrontHeightWorld)+1f/32).Within(.001f));
                if(vertical)Assert.That(combined.size.x,Is.EqualTo(DungeonWallSegmentView.CatacombsLowWallSideWidthWorld).Within(.001f));
                Assert.That(wall.ConfigureCatacombsLowWall(edge,art),Is.True);
                Assert.That(wall.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled),Is.EquivalentTo(first));
                Physics2D.SyncTransforms();Assert.That(collider.bounds,Is.EqualTo(bounds));Assert.That(collider.size,Is.EqualTo(size));Assert.That(collider.offset,Is.EqualTo(offset));
                Assert.That(collider.enabled,Is.True);Assert.That(wall.transform.position,Is.EqualTo(position));Assert.That(wall.transform.localScale,Is.EqualTo(scale));Assert.That(wall.transform.rotation,Is.EqualTo(rotation));
            }
            finally {Object.DestroyImmediate(root);}
        }

        [Test]
        public void StandingObjectBaselinesSeparateActorsAndKeepGroundObjectsBelowThem()
        {
            var root = new GameObject("Standing object depth");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                for (var index = -20; index < 20; index++)
                    view.GetStandingObjectDepthSortingOrder(index + .1f);
                view.GetDoorDepthSortingOrder(.5f);
                foreach (var baseline in new[] { -19.9f, .1f, 19.1f })
                {
                    var order = view.GetStandingObjectDepthSortingOrder(baseline);
                    Assert.That(view.GetActorDepthSortingOrder(baseline + 1f / 32f), Is.LessThan(order));
                    Assert.That(view.GetActorDepthSortingOrder(baseline), Is.LessThan(order));
                    Assert.That(view.GetActorDepthSortingOrder(baseline - 1f / 32f), Is.GreaterThan(order));
                    Assert.That(view.GetDungeonObjectDepthSortingOrder(), Is.LessThan(order));
                    Assert.That(view.GetFloorFeedbackDepthSortingOrder(), Is.LessThan(order));
                    Assert.That(order, Is.LessThan(100));
                    Assert.That(view.GetStandingObjectDepthSortingOrder(baseline), Is.EqualTo(order));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ExistingDepthBaselinesDoNotAllocateDuringRepeatedFrameQueries()
        {
            var root = new GameObject("Allocation-free depth queries");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                // Deliberately register out of order, as objects spawn.
                foreach (var baseline in new[] { 8f, -3f, 2f, 0f })
                {
                    view.GetStandingObjectDepthSortingOrder(baseline);
                    view.GetDoorDepthSortingOrder(baseline);
                }
                var expected = view.GetStandingObjectDepthSortingOrder(2f);
                var before = System.GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1000; i++)
                {
                    view.GetStandingObjectDepthSortingOrder(2f);
                    view.GetDoorDepthSortingOrder(-3f);
                    // Both sides of the epsilon must reuse the registered lane.
                    view.GetStandingObjectDepthSortingOrder(2f + .00001f);
                    view.GetDoorDepthSortingOrder(-3f - .00001f);
                }
                var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
                Assert.That(view.GetStandingObjectDepthSortingOrder(2f), Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StandingObstacleRenderOccludesBehindActorButNotFrontActor(bool combat)
        {
            var root = new GameObject("Pillar actor occlusion");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonObstacle.prefab");
            var pillar = Object.Instantiate(prefab, root.transform).GetComponent<DungeonObstacleView>();
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Obstacle_Pillar.asset");
            var pillarTexture = CreateSolidTexture(32, 64, Color.blue);
            var pillarSprite = Sprite.Create(pillarTexture, new Rect(0, 0, 32, 64), new Vector2(.5f, 0), 32);
            var actorTexture = CreateSolidTexture(32, 64, Color.red);
            var actorSprite = Sprite.Create(actorTexture, new Rect(0, 0, 32, 64), new Vector2(.5f, 0), 32);
            var cameraObject = new GameObject("Depth camera", typeof(Camera));
            var renderTexture = new RenderTexture(128, 128, 24);
            try
            {
                Assert.That(definition, Is.Not.Null);
                var view = root.AddComponent<DungeonFloorMapView>();
                var map = new DungeonFloorMap();
                for (var y = -3; y < 4; y++) map.TryAddFloorCell(new Vector2Int(0, y), "room");
                pillar.Initialize(new ObstacleDungeonObjectInstance("pillar", definition, Vector2Int.zero), Vector3.zero);
                Physics2D.SyncTransforms();
                Assert.That(pillar.ConfigureRuntimeSprite(pillarSprite, true), Is.True);
                pillar.BindElevationView(view);
                var group = pillar.GetComponent<SortingGroup>();

                // The runtime child has its visible base at the collider bottom.
                var art = System.Array.Find(pillar.GetComponentsInChildren<SpriteRenderer>(), r => r.enabled);
                var baseline = art.transform.position.y;
                Assert.That(baseline, Is.LessThan(pillar.transform.position.y));
                var actor = CreateRenderActor(root.transform, actorSprite, map, view, new Vector2(0, baseline + .1f));
                if (combat)
                {
                    actor.BindCombat(new Godmist.Runtime.Combat.BattleUnit { Character = new Godmist.Runtime.Characters.CharacterInstance() });
                }
                InvokePrivate(actor, "RefreshWorldDepthSorting");
                Assert.That(actor.GetComponent<SortingGroup>().sortingOrder, Is.LessThan(group.sortingOrder));
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, baseline + 1f, -10);
                camera.orthographic = true;
                camera.orthographicSize = 1.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var behind = ReadRenderPixel(camera, renderTexture, new Vector3(0, baseline + .6f, 0));
                Assert.That(behind.b, Is.GreaterThan(.65f));
                Assert.That(behind.r, Is.LessThan(.35f));
                actor.SetPosition(new Vector2(0, baseline - .1f));
                InvokePrivate(actor, "RefreshWorldDepthSorting");
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var front = ReadRenderPixel(camera, renderTexture, new Vector3(0, baseline + .6f, 0));
                Assert.That(front.r, Is.GreaterThan(.65f));
                Assert.That(front.b, Is.LessThan(.35f));
                pillar.BindElevationView(null);
                Assert.That(group.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pillarSprite);
                Object.DestroyImmediate(pillarTexture);
                Object.DestroyImmediate(actorSprite);
                Object.DestroyImmediate(actorTexture);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LowWallMapPresentationSeparatesActorsAtItsBaseline(bool vertical)
        {
            var root = new GameObject("Low wall depth");
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(root.transform);
            grid.transform.position = new Vector3(0, -12f, 0);
            var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            floor.transform.SetParent(grid.transform, false);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", floor.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", prefab.GetComponent<DungeonWallSegmentView>());
                SetField(view, "lowWallPrefab", prefab.GetComponent<DungeonWallSegmentView>());
                var edge = new DungeonCellEdge(Vector2Int.zero, vertical ? Vector2Int.right : Vector2Int.up);
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(edge.FirstCell, "room");
                map.TryAddFloorCell(edge.SecondCell, "room");
                map.SetWallType(edge, Godmist.Core.Enums.DungeonWallType.Low);
                view.Rebuild(map);
                var walls = root.GetComponentsInChildren<DungeonWallSegmentView>();
                Assert.That(walls, Is.Not.Empty);
                foreach (var wall in walls)
                {
                    Assert.That(wall.UsesNativeWallCrossSection, Is.False);
                    var group = wall.GetComponent<SortingGroup>();
                    Assert.That(group, Is.Not.Null);
                    Assert.That(view.GetActorDepthSortingOrder(wall.DepthSortBaselineY + .03125f),
                        vertical ? Is.GreaterThan(group.sortingOrder) : Is.LessThan(group.sortingOrder),
                        "Vertical low walls already keep actors above them outside the crossing animation.");
                    Assert.That(view.GetActorDepthSortingOrder(wall.DepthSortBaselineY - .03125f), Is.GreaterThan(group.sortingOrder));
                    Assert.That(floor.GetComponent<TilemapRenderer>().sortingOrder, Is.LessThan(group.sortingOrder));
                    Assert.That(wall.ConfigureCatacombsLowWall(edge, Resources.Load<Sprite>("CatacombsAudit/catacombs-low-wall-sheet-v1")), Is.True);
                    Assert.That(wall.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(group.sortingOrder));
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [TestCase(true, false, 0)] [TestCase(true, true, 0)]
        [TestCase(false, false, 0)] [TestCase(false, true, 0)]
        [TestCase(true, false, 1)] [TestCase(true, true, 1)]
        [TestCase(false, false, 1)] [TestCase(false, true, 1)]
        [TestCase(true, false, 2)] [TestCase(true, true, 2)]
        [TestCase(false, false, 2)] [TestCase(false, true, 2)]
        [TestCase(true, false, 3)] [TestCase(true, true, 3)]
        [TestCase(false, false, 3)] [TestCase(false, true, 3)]
        public void NativeLowWallCrossingDrawsActorOverCapAndRestoresDepth(bool vertical, bool reverse, int mode)
        {
            var root = new GameObject("Low wall crossing render", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<Tile>();
            var target = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
                floor.transform.SetParent(root.transform, false);
                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", floor.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab"));
                SetField(view, "lowWallPrefab", UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab"));
                view.ConfigureCatacombsArtPresentation(true);
                var map = new DungeonFloorMap();
                for (var y = 0; y < 6; y++) for (var x = 0; x < 6; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                for (var n = 1; n <= 3; n++) map.SetWallType(new DungeonCellEdge(
                    vertical ? new Vector2Int(2, n) : new Vector2Int(n, 2),
                    vertical ? new Vector2Int(3, n) : new Vector2Int(n, 3)), Godmist.Core.Enums.DungeonWallType.Low);
                if (mode == 2) map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(20, 20), 1), "upper");
                view.Rebuild(map);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                { renderer.forceRenderingOff = false; renderer.gameObject.layer = 30; }
                var center = vertical ? new Vector2(3, 2.5f) : new Vector2(2.5f, 3);
                var direction = (vertical ? Vector2.right : Vector2.up) * (reverse ? -1 : 1);
                var start = center - direction * .05f;
                var end = center + direction * .5f;
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png");
                var actor = CreateRenderActor(root.transform, sprite, map, view, start);
                actor.gameObject.layer = 30;
                Godmist.Views.Characters.CharacterView combatActor = null;
                if (mode == 3)
                {
                    actor.enabled = false;
                    combatActor = actor.gameObject.AddComponent<Godmist.Views.Characters.CharacterView>();
                    SetField(combatActor, "bodyRenderer", actor.BodyRenderer);
                    combatActor.Bind(new Godmist.Runtime.Combat.BattleUnit { IsPlayerSide = true,
                        Character = new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive,
                            Definition = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset") } });
                    combatActor.BindTerrainView(view);
                    combatActor.SetWorldPosition(end);
                    actor.transform.position = center + direction * .1f;
                }
                void RefreshDepth()
                {
                    if (combatActor != null) InvokePrivate(combatActor, "LateUpdate");
                    else InvokePrivate(actor, "RefreshWorldDepthSorting");
                }
                if (mode == 1) actor.BindCombat(new Godmist.Runtime.Combat.BattleUnit
                { IsPlayerSide = true, Character = new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive } });
                if (mode == 1) actor.MoveTowards(end, .15f, 0);
                else if (mode != 3)
                {
                    actor.Move(direction * .1f);
                    Assert.That(actor.IsCrossingLowWall, Is.True);
                    SetField(actor, "crossingElapsed", .25f);
                    InvokePrivate(actor, "Update");
                }
                RefreshDepth();
                var actorGroup = actor.GetComponent<SortingGroup>();
                var actorBounds = actor.BodyRenderer.bounds;
                var overlapping = root.GetComponentsInChildren<DungeonWallSegmentView>().Where(w =>
                    w.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.bounds.Intersects(actorBounds))).ToArray();
                Assert.That(overlapping, Is.Not.Empty);
                foreach (var wall in overlapping)
                    Assert.That(actorGroup.sortingOrder, Is.GreaterThan(wall.GetComponent<SortingGroup>().sortingOrder), "The crossed cap or its southern continuation must not cover the actor.");

                var cameraObject = new GameObject("Crossing camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.6f;
                camera.transform.position = new Vector3(center.x, center.y + .4f, -10); camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .07f, .08f); camera.targetTexture = target;
                var output = Path.GetFullPath("../LocalHandoffs/low-wall-crossing-20261023"); Directory.CreateDirectory(output);
                void Capture(string suffix)
                {
                    SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, $"{(vertical ? "vertical" : "horizontal")}-{reverse}-{mode}-{suffix}.png"), pixels.EncodeToPNG());
                }
                Capture("after");
                var afterPixels = pixels.GetPixels32();
                actorGroup.sortingOrder = view.GetActorDepthSortingOrder(actor.Position.y);
                Capture("before");
                if (vertical) Assert.That(pixels.GetPixels32().Where((p, index) => !p.Equals(afterPixels[index])).Count(), Is.Zero,
                    "Vertical low walls keep the actor above the cap both during and outside the crossing override.");
                RefreshDepth();
                if (mode == 1) actor.MoveTowards(end, 2, 0);
                else if (mode == 3)
                {
                    // EditMode deltaTime is zero: supply the arrival pose and
                    // run the real movement-completion/cleanup branch.
                    combatActor.transform.position = end;
                    InvokePrivate(combatActor, "UpdateMovement");
                }
                else { SetField(actor, "crossingElapsed", .6f); InvokePrivate(actor, "Update"); }
                Assert.That(Vector2.Distance(actor.Position, end), Is.LessThan(.001f));
                RefreshDepth();
                Assert.That(actorGroup.sortingOrder, Is.EqualTo(view.GetActorDepthSortingOrder(end.y)));
                void BeginAgain()
                {
                    if (combatActor != null) { combatActor.SetWorldPosition(start, true); combatActor.SetWorldPosition(end); }
                    else { actor.SetPosition(start); if (mode == 1) actor.MoveTowards(end, .1f, 0); else actor.Move(direction * .1f); }
                }
                BeginAgain();
                if (combatActor != null) combatActor.SetWorldPosition(start, true); else actor.SetPosition(start);
                RefreshDepth();
                Assert.That(actor.IsCrossingLowWall, Is.False);
                Assert.That(actorGroup.sortingOrder, Is.EqualTo(view.GetActorDepthSortingOrder(start.y)), "Teleport cancels the temporary lane.");
                BeginAgain();
                if (combatActor != null) combatActor.enabled = false; else actor.enabled = false;
                // These non-ExecuteAlways views do not receive runtime lifecycle
                // callbacks in EditMode; exercise the actual cleanup hook explicitly.
                if (combatActor != null) InvokePrivate(combatActor, "OnDisable"); else InvokePrivate(actor, "OnDisable");
                RefreshDepth();
                Assert.That(actor.IsCrossingLowWall, Is.False);
                Assert.That(actorGroup.sortingOrder, Is.EqualTo(view.GetActorDepthSortingOrder(actor.Position.y)), "Disabling the view cancels the temporary lane.");
                var unrelated = new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
                Assert.That(view.GetLowWallCrossingDepthSortingOrder(start.y, unrelated), Is.EqualTo(view.GetActorDepthSortingOrder(start.y)));
                Assert.That(view.TryGetLowWallCrossing(Vector2.zero, new Vector2(4, 4), 0, out _), Is.False, "A diagonal or teleport is not a low-wall step.");
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(root); Object.DestroyImmediate(tile); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
        }

        [Test]
        public void DoorBaselineSeparatesActorsOnBothSidesEvenWithoutWalls()
        {
            var root = new GameObject("Door depth test");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                // Half-cell thresholds need a lane even when no wall uses it.
                view.GetDoorDepthSortingOrder(1.5f);
                view.GetDoorDepthSortingOrder(-2f);
                foreach (var baseline in new[] { -2f, 1.5f })
                {
                    var doorOrder = view.GetDoorDepthSortingOrder(baseline);
                    Assert.That(view.GetActorDepthSortingOrder(baseline - 1f / 32f), Is.GreaterThan(doorOrder));
                    Assert.That(view.GetActorDepthSortingOrder(baseline + 1f / 32f), Is.LessThan(doorOrder));
                    Assert.That(view.GetActorDepthSortingOrder(baseline), Is.LessThan(doorOrder));
                }
                var stableOrder = view.GetDoorDepthSortingOrder(1.5f);
                Assert.That(view.GetDoorDepthSortingOrder(1.5f), Is.EqualTo(stableOrder));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PresentationFloorVariantsStayStableAcrossNegativeModuleCoordinates()
        {
            Assert.That(
                DungeonFloorMapView.ResolvePresentationFloorVariant(new Vector2Int(-1, -1)),
                Is.EqualTo(DungeonFloorMapView.ResolvePresentationFloorVariant(new Vector2Int(-4, -4))));
            Assert.That(
                DungeonFloorMapView.ResolvePresentationFloorVariant(new Vector2Int(-5, -1)),
                Is.Not.EqualTo(DungeonFloorMapView.ResolvePresentationFloorVariant(new Vector2Int(-1, -1))));

            var variants = new HashSet<int>();
            foreach (var groupY in Enumerable.Range(-2, 4))
                foreach (var groupX in Enumerable.Range(-2, 4))
                    variants.Add(DungeonFloorMapView.ResolvePresentationFloorVariant(
                        new Vector2Int(groupX * 4, groupY * 4)));
            Assert.That(variants, Has.Count.EqualTo(2));
            Assert.That(variants, Does.Contain(0));
            Assert.That(variants, Does.Contain(1));
        }

        [Test]
        public void WallUsesQuarterCellThicknessAndClosesCornerVisually()
        {
            var gridObject = new GameObject("Grid", typeof(Grid));
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tilemap = tilemapObject.GetComponent<Tilemap>();

            var wallObject = new GameObject("Wall", typeof(SpriteRenderer), typeof(BoxCollider2D));
            var wall = wallObject.AddComponent<DungeonWallSegmentView>();
            var collider = wallObject.GetComponent<BoxCollider2D>();
            SetField(wall, "movementCollider", collider);

            try
            {
                wall.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), tilemap);

                Assert.That(wall.Thickness, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(wall.VisualLength, Is.EqualTo(1.25f).Within(0.0001f));
                // This fixture has no assigned sprite, so it exercises the legacy
                // fallback transform path. Production native sprites use tiled draw
                // mode and keep the root scale at one (covered below).
                Assert.That(wall.transform.localScale.x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(wall.transform.localScale.y, Is.EqualTo(1.25f).Within(0.0001f));
                Assert.That(collider.bounds.size.x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(collider.bounds.size.y, Is.EqualTo(1f).Within(0.0001f));

                wall.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.up), tilemap);

                Assert.That(Quaternion.Angle(wall.transform.rotation, Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.0001f));
                Physics2D.SyncTransforms();
                Assert.That(collider.bounds.size.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(collider.bounds.size.y, Is.EqualTo(0.25f).Within(0.0001f));

                wall.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), tilemap, true, false, false);
                Assert.That(wall.VisualLength, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wall.transform.position.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wall.transform.position.y, Is.EqualTo(0.5f).Within(0.0001f));

                wall.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), tilemap, true, false, true);
                Assert.That(wall.VisualLength, Is.EqualTo(1.125f).Within(0.0001f));
                Assert.That(wall.transform.position.y, Is.EqualTo(0.5625f).Within(0.0001f));
                Physics2D.SyncTransforms();
                Assert.That(wall.transform.position.y + collider.offset.y, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(wallObject);
                Object.DestroyImmediate(tilemapObject);
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void MapViewExtendsOnlyWallEndsThatMeetAnotherSegment()
        {
            var root = new GameObject("Dungeon view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var wallObject = new GameObject("Wall", typeof(SpriteRenderer), typeof(BoxCollider2D));
            var wall = wallObject.AddComponent<DungeonWallSegmentView>();
            SetField(wall, "movementCollider", wallObject.GetComponent<BoxCollider2D>());
            var tile = ScriptableObject.CreateInstance<Tile>();

            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                var tilemap = tilemapObject.GetComponent<Tilemap>();
                SetField(view, "floorTilemap", tilemap);
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", wall);

                var map = new DungeonFloorMap();
                foreach (var cell in new[]
                         {
                             new Vector2Int(0, 0), new Vector2Int(1, 0),
                             new Vector2Int(0, 1), new Vector2Int(1, 1)
                         })
                    map.TryAddFloorCell(cell, "room");
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    Godmist.Core.Enums.DungeonWallType.Full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 1), new Vector2Int(1, 1)),
                    Godmist.Core.Enums.DungeonWallType.Full);

                view.Rebuild(map);

                var walls = root.GetComponentsInChildren<DungeonWallSegmentView>();
                Assert.That(walls, Has.Length.EqualTo(2));
                Assert.That(walls, Has.All.Property(nameof(DungeonWallSegmentView.VisualLength))
                    .EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(wallObject);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void NativeWallProfilesUseFloorSideAndMeetWithoutOverlap()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");

            Assert.That(wallPrefab, Is.Not.Null);

            var instance = Object.Instantiate(wallPrefab);
            var gridObject = new GameObject("Grid", typeof(Grid));
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            try
            {
                var wall = instance.GetComponent<DungeonWallSegmentView>();
                var renderer = instance.GetComponent<SpriteRenderer>();
                var collider = instance.GetComponent<BoxCollider2D>();
                var tilemap = tilemapObject.GetComponent<Tilemap>();
                SetField(wall, "sideWallSprite", sideSprite);
                SetField(wall, "northWallSprite", northSprite);
                SetField(wall, "southWallSprite", southSprite);

                wall.Bind(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)),
                    tilemap, true, false, false, false, DungeonWallSurfaceProfile.North);
                Physics2D.SyncTransforms();
                Assert.That(wall.SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.North));
                Assert.That(renderer.sprite, Is.SameAs(northSprite));
                Assert.That(renderer.size, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(instance.transform.position.y, Is.EqualTo(1.5f).Within(.0001f));
                Assert.That(collider.bounds.center.y, Is.EqualTo(1f).Within(.0001f));
                Assert.That(collider.bounds.size.x, Is.EqualTo(1f).Within(.0001f));
                Assert.That(collider.bounds.size.y, Is.EqualTo(.25f).Within(.0001f));

                wall.Bind(new DungeonCellEdge(new Vector2Int(0, -1), new Vector2Int(0, 0)),
                    tilemap, true, false, false, false, DungeonWallSurfaceProfile.South);
                Physics2D.SyncTransforms();
                Assert.That(wall.SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.South));
                Assert.That(renderer.sprite, Is.SameAs(southSprite));
                Assert.That(renderer.size, Is.EqualTo(new Vector2(1f, .25f)));
                Assert.That(instance.transform.position.y, Is.EqualTo(.625f).Within(.0001f));
                Assert.That(collider.bounds.center.y, Is.EqualTo(0f).Within(.0001f));
                Assert.That((Vector2)collider.bounds.size, Is.EqualTo(new Vector2(1f, .25f)));

                wall.Bind(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    tilemap, true, false, false, false, DungeonWallSurfaceProfile.Side);
                Physics2D.SyncTransforms();
                Assert.That(wall.SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.Side));
                Assert.That(renderer.sprite, Is.SameAs(sideSprite));
                Assert.That(renderer.size, Is.EqualTo(new Vector2(.25f, 1f)));
                Assert.That(collider.bounds.center.x, Is.EqualTo(1f).Within(.0001f));
                Assert.That((Vector2)collider.bounds.size, Is.EqualTo(new Vector2(.25f, 1f)));

                SetField(wall, "northWallSprite", null);
                wall.Bind(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)),
                    tilemap, true, false, false, false, DungeonWallSurfaceProfile.North);
                Assert.That(wall.SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.Side));
                Assert.That(renderer.size, Is.EqualTo(new Vector2(.25f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(tilemapObject);
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void MapViewClassifiesWallProfilesFromWalkableSide()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon profile view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, -1), new Vector2Int(0, 0)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)), full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                var walls = root.GetComponentsInChildren<DungeonWallSegmentView>();
                Assert.That(walls, Has.Length.EqualTo(3));
                foreach (var wall in walls)
                {
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.North)
                    {
                        Assert.That(wall.VisualLength, Is.EqualTo(1.125f).Within(.0001f));
                        Assert.That(wall.transform.position.x, Is.EqualTo(.5625f).Within(.0001f));
                        Assert.That(wall.transform.position.y, Is.EqualTo(1.5f).Within(.0001f));
                        Assert.That(wall.GetComponent<SpriteRenderer>().size,
                            Is.EqualTo(new Vector2(1.125f, 1f)));
                        Assert.That(wall.GetComponent<BoxCollider2D>().bounds.center.x,
                            Is.EqualTo(.5f).Within(.0001f));
                    }
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South)
                    {
                        Assert.That(wall.VisualLength, Is.EqualTo(1.125f).Within(.0001f));
                        Assert.That(wall.transform.position.x, Is.EqualTo(.5625f).Within(.0001f));
                        Assert.That(wall.transform.position.y, Is.EqualTo(.625f).Within(.0001f));
                        Assert.That(wall.GetComponent<SpriteRenderer>().size,
                            Is.EqualTo(new Vector2(1.125f, .25f)));
                        Assert.That(wall.GetComponent<BoxCollider2D>().bounds.center.x,
                            Is.EqualTo(.5f).Within(.0001f));
                    }
                    else
                    {
                        Assert.That(wall.SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.Side));
                        Assert.That(wall.VisualLength, Is.EqualTo(.25f).Within(.0001f));
                        Assert.That(wall.GetComponent<SpriteRenderer>().size,
                            Is.EqualTo(new Vector2(.25f, .25f)));
                        var sideRenderers = GetWallProfileRenderers(wall);
                        Assert.That(sideRenderers, Has.Count.EqualTo(2));
                        Assert.That(wall.GetComponent<SpriteRenderer>().bounds.min.y,
                            Is.EqualTo(.75f).Within(.0001f));
                        Assert.That(wall.GetComponent<SpriteRenderer>().bounds.max.y,
                            Is.EqualTo(1f).Within(.0001f));
                        Assert.That(sideRenderers[1].name, Is.EqualTo("NorthSideCapOverlay"));
                        Assert.That(sideRenderers[1].size,
                            Is.EqualTo(new Vector2(.25f, .75f)));
                        Assert.That(sideRenderers[1].sortingOrder,
                            Is.EqualTo(DungeonWallSegmentView.UpperVerticalCornerSortingOrder));
                        Assert.That(sideRenderers[1].transform.position.y,
                            Is.EqualTo(1.375f).Within(.0001f));
                        Assert.That(sideRenderers[1].bounds.min.y,
                            Is.EqualTo(1f).Within(.0001f));
                        Assert.That(sideRenderers[1].bounds.max.y,
                            Is.EqualTo(1.75f).Within(.0001f));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewCapsUpperCornersAndKeepsLowerFacesAtBothSides()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon corner cap view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.up), full);
                map.SetWallType(new DungeonCellEdge(Vector2Int.down, Vector2Int.zero), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(-1, 0), Vector2Int.zero), full);
                map.SetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                var walls = root.GetComponentsInChildren<DungeonWallSegmentView>();
                Assert.That(walls, Has.Length.EqualTo(4));
                foreach (var wall in walls)
                {
                    var renderers = GetWallProfileRenderers(wall);
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.North)
                    {
                        Assert.That(renderers, Has.Count.EqualTo(1));
                        Assert.That(renderers[0].size, Is.EqualTo(new Vector2(1.25f, 1f)));
                    }
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South)
                    {
                        Assert.That(renderers, Has.Count.EqualTo(2));
                        Assert.That(renderers[0].size,
                            Is.EqualTo(new Vector2(1.25f, .25f)));
                        Assert.That(renderers[1].name, Is.EqualTo("SouthWallOcclusionMask"));
                        Assert.That(renderers[1].sortingOrder,
                            Is.LessThan(renderers[0].sortingOrder));
                    }
                    else
                    {
                        Assert.That(wall.SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.Side));
                        Assert.That(renderers, Has.Count.EqualTo(2));
                        Assert.That(renderers[1].name, Is.EqualTo("NorthSideCapOverlay"));
                        Assert.That(renderers[1].size,
                            Is.EqualTo(new Vector2(.25f, .75f)));
                        Assert.That(renderers[1].transform.position.y,
                            Is.EqualTo(1.375f).Within(.0001f));
                        Assert.That(renderers[1].bounds.min.y,
                            Is.EqualTo(1f).Within(.0001f));
                        Assert.That(renderers[1].bounds.max.y,
                            Is.EqualTo(1.75f).Within(.0001f));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewDoesNotUseNorthSideCapForLowWallNeighbor()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var lowWallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var root = new GameObject("Dungeon low neighbor cap view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);
                SetField(view, "lowWallPrefab", lowWallPrefab.GetComponent<DungeonWallSegmentView>());

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                var northEdge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.up);
                var sideEdge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
                map.SetWallType(northEdge, Godmist.Core.Enums.DungeonWallType.Low);
                map.SetWallType(sideEdge, Godmist.Core.Enums.DungeonWallType.Full);

                view.Rebuild(map);

                var side = null as DungeonWallSegmentView;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.UsesNativeWallCrossSection)
                    {
                        side = wall;
                        break;
                    }

                Assert.That(side, Is.Not.Null);
                Assert.That(side.GetComponentsInChildren<SpriteRenderer>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewKeepsLowerEndpointFaceWhenStepMeetsNorthWall()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var root = new GameObject("Dungeon step cap view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                map.TryAddFloorCell(new Vector2Int(1, -1), "step");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                var sideEdge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
                var northAtStep = new DungeonCellEdge(new Vector2Int(1, -1), new Vector2Int(1, 0));
                map.SetWallType(sideEdge, full);
                map.SetWallType(northAtStep, full);

                view.Rebuild(map);

                DungeonWallSegmentView side = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.UsesNativeWallCrossSection)
                    {
                        side = wall;
                        break;
                    }

                Assert.That(side, Is.Not.Null);
                var renderers = GetWallProfileRenderers(side);
                Assert.That(renderers, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewUsesUpperEndpointForSouthCapsOnBothSideWalls()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon South stepped corners");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                // Auxiliary floor cells make the two Side edges valid while
                // keeping each South edge on the upper side of its boundary.
                map.TryAddFloorCell(new Vector2Int(-1, 1), "left-side-floor");
                map.TryAddFloorCell(new Vector2Int(0, 2), "left-south-floor");
                map.TryAddFloorCell(new Vector2Int(1, 2), "right-south-floor");
                map.TryAddFloorCell(new Vector2Int(2, 1), "right-side-floor");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                // Both real Side walls meet a South edge at their upper grid
                // vertex. The same map therefore exercises left and right
                // screen orientations through the actual Rebuild/Bind path.
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(-1, 1), new Vector2Int(0, 1)),
                    full);
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(2, 1)),
                    full);
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, 1), new Vector2Int(0, 2)),
                    full);
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)),
                    full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                var sideWalls = root.GetComponentsInChildren<DungeonWallSegmentView>();
                var sideCount = 0;
                foreach (var wall in sideWalls)
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.UsesNativeWallCrossSection)
                    {
                        sideCount++;
                        var renderers = GetWallProfileRenderers(wall);
                        Assert.That(renderers, Has.Count.EqualTo(2));
                        Assert.That(renderers[1].name, Is.EqualTo("SouthSideCapOverlay"));
                        Assert.That(renderers[0].bounds.min.y,
                            Is.EqualTo(1f).Within(.0001f));
                        Assert.That(renderers[0].bounds.max.y,
                            Is.EqualTo(2.75f).Within(.0001f));
                        Assert.That(renderers[0].sortingOrder,
                            Is.EqualTo(DungeonWallSegmentView.VerticalWallSortingOrder));
                        Assert.That(renderers[1].sortingOrder,
                            Is.EqualTo(DungeonWallSegmentView.UpperVerticalCornerSortingOrder));
                        Assert.That(renderers[1].bounds.min.y,
                            Is.EqualTo(2.5f).Within(.0001f));
                        Assert.That(renderers[1].bounds.max.y,
                            Is.EqualTo(2.75f).Within(.0001f));
                    }

                Assert.That(sideCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewTrimsSideVisualAtLowerSouthEndpointWithoutMovingCollider()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon lower South endpoint");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "room");
                map.TryAddFloorCell(new Vector2Int(1, 0), "room");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, -1), new Vector2Int(0, 0)), full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                DungeonWallSegmentView side = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.GetComponent<SpriteRenderer>().sprite == sideSprite)
                    {
                        side = wall;
                        break;
                    }

                Assert.That(side, Is.Not.Null);
                Assert.That(side.VisualLength, Is.EqualTo(.25f).Within(.0001f));
                var renderer = side.GetComponent<SpriteRenderer>();
                Assert.That(renderer.bounds.min.y, Is.EqualTo(.75f).Within(.0001f));
                Assert.That(renderer.bounds.max.y, Is.EqualTo(1f).Within(.0001f));
                var collider = side.GetComponent<BoxCollider2D>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.bounds.center.y, Is.EqualTo(.5f).Within(.0001f));
                Assert.That(collider.bounds.min.y, Is.EqualTo(0f).Within(.0001f));
                Assert.That(collider.bounds.max.y, Is.EqualTo(1f).Within(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void ActorWorldDepthSortKeepsBehindActorUnderSouthWallAndFrontActorAbove()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon actor wall occlusion");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var actorObject = new GameObject("Actor", typeof(SpriteRenderer));
            var actorRenderer = actorObject.GetComponent<SpriteRenderer>();
            var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            DungeonWallSegmentView southWall = null;
            var southGroups = new List<SortingGroup>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(1, 0), "front");
                map.TryAddFloorCell(new Vector2Int(1, 2), "back");
                map.TryAddFloorCell(new Vector2Int(3, 0), "front");
                map.TryAddFloorCell(new Vector2Int(3, 2), "back");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(1, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(3, 0), new Vector2Int(3, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(3, 1), new Vector2Int(3, 2)), full);
                view.Rebuild(map);

                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South
                        && Mathf.Abs(wall.DepthSortBaselineY - 2f) < .0001f)
                    {
                        southWall ??= wall;
                        southGroups.Add(wall.GetComponent<SortingGroup>());
                    }
                Assert.That(southWall, Is.Not.Null);
                Assert.That(southGroups, Has.Count.EqualTo(2));
                Assert.That(southGroups, Has.All.Not.Null);

                actorRenderer.sprite = sideSprite;
                SetField(actor, "bodyRenderer", actorRenderer);
                actor.ConfigureTerrain(map, view);
                actor.transform.position = view.CellToWorldCenter(new Vector2Int(1, 2));
                InvokePrivate(actor, "RefreshWorldDepthSorting");
                var actorSortingGroup = actorObject.GetComponent<SortingGroup>();
                Assert.That(actorSortingGroup, Is.Not.Null);
                Assert.That(actorSortingGroup.sortingOrder, Is.LessThan(100));
                var southGroup = southWall.GetComponent<SortingGroup>();
                Assert.That(southGroup, Is.Not.Null);
                Assert.That(actorSortingGroup.sortingOrder, Is.LessThan(southGroup.sortingOrder));
                Assert.That(actorObject.GetComponentsInChildren<SpriteMask>(true), Is.Empty);
                Assert.That(actorObject.activeSelf, Is.True);

                actor.transform.position = view.CellToWorldCenter(new Vector2Int(1, 0));
                InvokePrivate(actor, "RefreshWorldDepthSorting");
                Assert.That(actorSortingGroup.sortingOrder, Is.GreaterThan(southGroup.sortingOrder));
                foreach (var sameBaselineWall in southGroups)
                    Assert.That(actorSortingGroup.sortingOrder,
                        Is.GreaterThan(sameBaselineWall.sortingOrder));
                Assert.That(actorObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void ActorWorldDepthAddsMirroredSideVoidOccludersWithoutMaskingFrontActor()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var actorTexture = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            var actorSprite = Sprite.Create(
                actorTexture,
                new Rect(0f, 0f, 64f, 32f),
                new Vector2(.5f, .1f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var root = new GameObject("Dungeon actor corner occlusion");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var leftActorObject = new GameObject("Left corner actor", typeof(SpriteRenderer));
            var leftActorRenderer = leftActorObject.GetComponent<SpriteRenderer>();
            var leftActor = leftActorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            var rightActorObject = new GameObject("Right corner actor", typeof(SpriteRenderer));
            var rightActorRenderer = rightActorObject.GetComponent<SpriteRenderer>();
            var rightActor = rightActorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            var frontActorObject = new GameObject("Front corner actor", typeof(SpriteRenderer));
            var frontActorRenderer = frontActorObject.GetComponent<SpriteRenderer>();
            var frontActor = frontActorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "front");
                map.TryAddFloorCell(new Vector2Int(0, 1), "left-side");
                map.TryAddFloorCell(new Vector2Int(1, 2), "left-south");
                map.TryAddFloorCell(new Vector2Int(2, 2), "right-south");
                map.TryAddFloorCell(new Vector2Int(3, 1), "right-side");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 1), new Vector2Int(1, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 1), new Vector2Int(3, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 1), new Vector2Int(2, 2)), full);
                view.Rebuild(map);

                leftActorRenderer.sprite = actorSprite;
                leftActorRenderer.sortingOrder = 10;
                SetField(leftActor, "bodyRenderer", leftActorRenderer);
                leftActor.ConfigureTerrain(map, view);
                leftActor.transform.position = new Vector2(.65f, 1.5f);
                InvokePrivate(leftActor, "RefreshWorldDepthSorting");

                rightActorRenderer.sprite = actorSprite;
                rightActorRenderer.sortingOrder = 10;
                SetField(rightActor, "bodyRenderer", rightActorRenderer);
                rightActor.ConfigureTerrain(map, view);
                rightActor.transform.position = new Vector2(3.35f, 1.5f);
                InvokePrivate(rightActor, "RefreshWorldDepthSorting");

                frontActorRenderer.sprite = actorSprite;
                frontActorRenderer.sortingOrder = 10;
                SetField(frontActor, "bodyRenderer", frontActorRenderer);
                frontActor.ConfigureTerrain(map, view);
                frontActor.transform.position = view.CellToWorldCenter(new Vector2Int(0, 0));
                InvokePrivate(frontActor, "RefreshWorldDepthSorting");

                DungeonWallSegmentView leftSide = null;
                DungeonWallSegmentView rightSide = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
                {
                    if (wall.SurfaceProfile != DungeonWallSurfaceProfile.Side)
                        continue;
                    if (wall.transform.position.x < 2f) leftSide = wall;
                    else rightSide = wall;
                }

                Assert.That(leftSide, Is.Not.Null);
                Assert.That(rightSide, Is.Not.Null);
                var leftVoidOcclusion = FindRenderer(leftSide, "SideVoidOcclusion");
                var rightVoidOcclusion = FindRenderer(rightSide, "SideVoidOcclusion");
                Assert.That(leftVoidOcclusion, Is.Not.Null);
                Assert.That(rightVoidOcclusion, Is.Not.Null);
                Assert.That(leftVoidOcclusion.bounds.Contains(new Vector3(1.4f, 1.5f, 0f)), Is.True);
                Assert.That(rightVoidOcclusion.bounds.Contains(new Vector3(2.6f, 1.5f, 0f)), Is.True);
                Assert.That(leftVoidOcclusion.sortingOrder,
                    Is.LessThan(leftSide.GetComponent<SpriteRenderer>().sortingOrder));
                Assert.That(rightVoidOcclusion.sortingOrder,
                    Is.LessThan(rightSide.GetComponent<SpriteRenderer>().sortingOrder));
                Assert.That(leftActorObject.GetComponentsInChildren<SpriteMask>(true), Is.Empty);
                Assert.That(rightActorObject.GetComponentsInChildren<SpriteMask>(true), Is.Empty);
                Assert.That(frontActorObject.GetComponentsInChildren<SpriteMask>(true), Is.Empty);
                var leftGroup = leftActorObject.GetComponent<SortingGroup>();
                var rightGroup = rightActorObject.GetComponent<SortingGroup>();
                var frontGroup = frontActorObject.GetComponent<SortingGroup>();
                Assert.That(leftGroup, Is.Not.Null);
                Assert.That(rightGroup, Is.Not.Null);
                Assert.That(frontGroup, Is.Not.Null);
                Assert.That(frontGroup.sortingOrder, Is.GreaterThan(leftGroup.sortingOrder));
                Assert.That(frontGroup.sortingOrder, Is.GreaterThan(rightGroup.sortingOrder));
                Assert.That(frontActorObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(frontActorObject);
                Object.DestroyImmediate(rightActorObject);
                Object.DestroyImmediate(leftActorObject);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(actorSprite);
                Object.DestroyImmediate(actorTexture);
            }
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void MapViewShortVerticalRecessKeepsUpperCapAndAlignedSouthFace(int horizontalDirection)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon short vertical recess");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "lower");
                map.TryAddFloorCell(new Vector2Int(horizontalDirection, 1), "upper");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(
                    new DungeonCellEdge(
                        new Vector2Int(0, 0),
                        new Vector2Int(horizontalDirection, 0)),
                    full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)), full);
                map.SetWallType(
                    new DungeonCellEdge(
                        new Vector2Int(horizontalDirection, 0),
                        new Vector2Int(horizontalDirection, 1)),
                    full);

                view.Rebuild(map);

                DungeonWallSegmentView side = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.UsesNativeWallCrossSection)
                    {
                        side = wall;
                        break;
                    }

                Assert.That(side, Is.Not.Null);
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                var baseRenderer = side.GetComponent<SpriteRenderer>();
                Assert.That(baseRenderer.sprite, Is.SameAs(sideSprite),
                    "Endpoint face patches must not replace ordinary Side body material.");
                Assert.That(baseRenderer.sprite.rect.width, Is.EqualTo(8f).Within(.001f));
                Assert.That(baseRenderer.sprite.rect.height, Is.EqualTo(32f).Within(.001f));
                SpriteRenderer northCap = null;
                SpriteRenderer southFace = null;
                var cornerOverlayCount = 0;
                foreach (var renderer in renderers)
                {
                    if (renderer == baseRenderer
                        || !renderer.gameObject.activeSelf
                        || !renderer.gameObject.activeInHierarchy)
                        continue;

                    if (renderer.name == "NorthSideCapOverlay")
                    {
                        cornerOverlayCount++;
                        northCap = renderer;
                    }
                    else if (renderer.name == "SouthSideFaceOverlay")
                    {
                        cornerOverlayCount++;
                        southFace = renderer;
                    }
                }

                Assert.That(cornerOverlayCount, Is.EqualTo(2));
                Assert.That(northCap, Is.Not.Null);
                Assert.That(southFace, Is.Not.Null);
                Assert.That(northCap.sprite, Is.SameAs(sideSprite));
                Assert.That(southFace.sprite, Is.SameAs(southSprite));
                Assert.That(baseRenderer.bounds.min.y, Is.EqualTo(0f).Within(.0001f));
                Assert.That(baseRenderer.bounds.max.y, Is.EqualTo(1f).Within(.0001f));
                Assert.That(northCap.bounds.min.y, Is.EqualTo(1f).Within(.0001f));
                Assert.That(northCap.bounds.max.y, Is.EqualTo(1.75f).Within(.0001f));
                Assert.That(southFace.bounds.min.y, Is.EqualTo(.75f + side.SouthVisualOffsetCells).Within(.0001f));
                Assert.That(southFace.bounds.max.y, Is.EqualTo(1f + side.SouthVisualOffsetCells).Within(.0001f));
                Assert.That(southFace.bounds.max.y,
                    Is.LessThanOrEqualTo(northCap.bounds.max.y + .0001f));
                Assert.That(northCap.sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.UpperVerticalCornerSortingOrder));
                Assert.That(southFace.sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.UpperVerticalCornerSortingOrder));
                Assert.That(southFace.transform.IsChildOf(side.transform), Is.False,
                    "The South face patch must be rehomed under the South wall lane.");
                Assert.That(northCap.sortingOrder, Is.GreaterThan(baseRenderer.sortingOrder));
                Assert.That(southFace.sortingOrder, Is.GreaterThan(baseRenderer.sortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [TestCase(1, false)]
        [TestCase(-1, false)]
        [TestCase(1, true)]
        [TestCase(-1, true)]
        public void MapViewShortSideChainFacesOnlyInternalPartitions(
            int horizontalDirection,
            bool internalPartition)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon opposite-end short Side");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "lower");
                map.TryAddFloorCell(new Vector2Int(0, 1), "upper");
                if (internalPartition)
                {
                    map.TryAddFloorCell(new Vector2Int(horizontalDirection, 0), "lower-other-side");
                    map.TryAddFloorCell(new Vector2Int(horizontalDirection, 1), "upper-other-side");
                }
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                // Two adjacent horizontal Side edges form the short vertical
                // strip. With only the first cells present this is an outside
                // boundary and must keep Side cap art. Adding both adjacent
                // cells turns it into an internal partition, which is the only
                // topology allowed to use the cropped North face.
                map.SetWallType(new DungeonCellEdge(
                    new Vector2Int(0, 0), new Vector2Int(horizontalDirection, 0)), full);
                map.SetWallType(new DungeonCellEdge(
                    new Vector2Int(0, 1), new Vector2Int(horizontalDirection, 1)), full);
                map.SetWallType(new DungeonCellEdge(
                    new Vector2Int(0, -1), new Vector2Int(0, 0)), full);
                map.SetWallType(new DungeonCellEdge(
                    new Vector2Int(0, 1), new Vector2Int(0, 2)), full);

                view.Rebuild(map);

                DungeonWallSegmentView side = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.UsesNativeWallCrossSection
                        && (side == null || wall.transform.position.y > side.transform.position.y))
                        side = wall;

                Assert.That(side, Is.Not.Null);
                var baseRenderer = side.GetComponent<SpriteRenderer>();
                Assert.That(baseRenderer, Is.Not.Null);
                Assert.That(baseRenderer.sprite, Is.SameAs(sideSprite),
                    "Internal and exterior vertical runs retain continuous cap art.");
                Assert.That(baseRenderer.sprite.rect.width, Is.EqualTo(8f).Within(.001f));
                Assert.That(baseRenderer.sprite.rect.height, Is.EqualTo(32f).Within(.001f));
                Assert.That(baseRenderer.bounds.size.y, Is.EqualTo(1f).Within(.001f));

                var northCap = FindRenderer(side, "NorthSideCapOverlay");
                Assert.That(northCap, Is.Not.Null);
                Assert.That(northCap.sprite, Is.SameAs(sideSprite));
                Assert.That(northCap.bounds.size.y, Is.EqualTo(.75f).Within(.001f));

            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MapViewSingleNorthJunctionFacesOnlyInternalSide(bool internalPartition)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var root = new GameObject("Dungeon single North Side junction");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "room");
                if (internalPartition)
                    map.TryAddFloorCell(new Vector2Int(1, 0), "other-room");

                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    full);
                // This North edge touches only the upper endpoint of the Side
                // edge; there is deliberately no second Side or South edge.
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)),
                    full);

                view.Rebuild(map);

                DungeonWallSegmentView side = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.UsesNativeWallCrossSection)
                    {
                        side = wall;
                        break;
                    }

                Assert.That(side, Is.Not.Null);
                var body = side.GetComponent<SpriteRenderer>();
                Assert.That(body, Is.Not.Null);
                if (internalPartition)
                    Assert.That(body.sprite.name, Is.EqualTo("RuntimeDungeonMixedSideFace"),
                        "A single internal segment with a free southern end is the terminal face.");
                else
                    Assert.That(body.sprite, Is.SameAs(sideSprite), "Exterior boundaries retain native cap art.");
                Assert.That(body.sprite.rect.width, Is.EqualTo(8f).Within(.001f));
                Assert.That(body.sprite.rect.height, Is.EqualTo(32f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void NorthSideJoinOverlayWrapsNativePhaseAcrossTileBoundary()
        {
            var sourceTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var sourcePixels = new Color32[32 * 32];
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    sourcePixels[y * 32 + x] = new Color32(
                        (byte)((x & 1) == 0 ? 0 : 255),
                        (byte)((x & 2) == 0 ? 0 : 255),
                        (byte)((x & 4) == 0 ? 0 : 255),
                        255);
            sourceTexture.SetPixels32(sourcePixels);
            sourceTexture.Apply(false, false);

            var sourceSprite = Sprite.Create(
                sourceTexture,
                new Rect(0f, 0f, 32f, 32f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var sideTexture = CreateSolidTexture(8, 32, new Color(.35f, .4f, .45f, 1f));
            var sideSprite = Sprite.Create(
                sideTexture,
                new Rect(0f, 0f, 8f, 32f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var root = new GameObject("North join wrapped phase regression");
            var view = root.AddComponent<DungeonWallSegmentView>();
            var sourceRenderer = root.AddComponent<SpriteRenderer>();
            sourceRenderer.sprite = sourceSprite;
            SetField(view, "<SurfaceProfile>k__BackingField", DungeonWallSurfaceProfile.North);
            SetField(view, "sideWallSprite", sideSprite);
            SetField(view, "northWallSprite", sourceSprite);

            try
            {
                var sourceBounds = sourceRenderer.bounds;
                var overlap = new Bounds(
                    new Vector3(sourceBounds.min.x + .875f + .125f, sourceBounds.min.y + .125f, 0f),
                    new Vector3(.25f, .25f, 1f));
                var sideBounds = new Bounds(
                    new Vector3(sourceBounds.min.x + .5f, sourceBounds.min.y + .5f, 0f),
                    new Vector3(.25f, 1f, 1f));
                view.ConfigureNorthSideJoinOverlay(overlap, 1, sideBounds);

                var overlay = FindRenderer(view, "NorthSideJoinOverlay");
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.sprite, Is.Not.Null);
                Assert.That(overlay.sprite.texture, Is.Not.SameAs(sourceTexture));
                Assert.That(overlay.sprite.texture.width, Is.EqualTo(8));
                Assert.That(overlay.sprite.texture.height, Is.EqualTo(8));

                var wrappedPixels = overlay.sprite.texture.GetPixels32();
                Assert.That(wrappedPixels[0], Is.EqualTo(sourcePixels[28]));
                Assert.That(wrappedPixels[7], Is.EqualTo(sourcePixels[3]));
                Assert.That(
                    wrappedPixels[7],
                    Is.Not.EqualTo(sourcePixels[31]),
                    "The North join must wrap the native phase instead of clamping at texture column 31.");

                var miter = FindRenderer(view, "CornerMiterNorthCap");
                Assert.That(miter, Is.Not.Null,
                    "A mixed North/Side join must create its dedicated cap miter.");
                Assert.That(
                    miter.bounds.center.y,
                    Is.EqualTo(sourceBounds.max.y - DungeonWallSegmentView.NativeWallWidthWorld * .5f)
                        .Within(.0001f),
                    "The mixed North/Side join miter belongs to the Side upper endpoint band.");
                Assert.That(miter.bounds.min.y, Is.GreaterThan(sourceBounds.center.y));
                Assert.That(miter.GetComponent<SortingGroup>().sortAtRoot, Is.True);
                Assert.That(miter.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(2));
                Assert.That(miter.sortingOrder, Is.Zero);

                // This fixture joins the right end of the North wall. Its
                // horizontal arm must therefore run left from the endpoint;
                // the first pixel of the authored border is present at the
                // left end of the runtime miter instead of being mirrored to
                // the right edge.
                var miterPixels = miter.sprite.texture.GetPixels32();
                Assert.That(
                    miterPixels[8],
                    Is.EqualTo(sourcePixels[25 * 32 + 28]),
                    "The right North/Side join must keep the phase-aligned world-left border pixel.");
                Assert.That(
                    miterPixels[15],
                    Is.Not.EqualTo(sourcePixels[25 * 32 + 3]),
                    "The right North/Side join must not mirror the border to the outer right pixel.");
                Assert.That(
                    miterPixels[15],
                    Is.EqualTo(sideTexture.GetPixels32()[26 * sideTexture.width + 5]),
                    "The stopped NorthCap body must sample neutral Side grain instead of the North seam.");
            }
            finally
            {
                view.ClearNorthSideJoinOverlays();
                Object.DestroyImmediate(sourceSprite);
                Object.DestroyImmediate(sourceTexture);
                Object.DestroyImmediate(sideSprite);
                Object.DestroyImmediate(sideTexture);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(-1, 0)]
        [TestCase(-1, 1)]
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        public void MapViewNorthFaceWinsOnlyAtOverlappingSideJoin(
            int horizontalDirection,
            int sideRow)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideTexture = CreateSolidTexture(8, 32, new Color(.92f, .08f, .75f, 1f));
            var sideSprite = Sprite.Create(
                sideTexture,
                new Rect(0f, 0f, 8f, 32f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var northTexture = CreateNorthJoinTexture(
                new Color(.08f, .85f, .95f, 1f),
                new Color(.95f, .15f, .12f, 1f));
            var northSprite = Sprite.Create(
                northTexture,
                new Rect(0f, 0f, 32f, 32f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var southTexture = CreateSolidTexture(32, 8, new Color(.1f, .2f, .3f, 1f));
            var southSprite = Sprite.Create(
                southTexture,
                new Rect(0f, 0f, 32f, 8f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var actorBodyTexture = CreateSolidTexture(64, 96, new Color(.95f, .08f, .04f, 1f));
            var actorFootTexture = CreateSolidTexture(32, 16, new Color(.98f, .78f, .08f, 1f));
            var actorWeaponTexture = CreateSolidTexture(16, 16, new Color(.1f, .85f, .98f, 1f));
            var root = new GameObject("Dungeon North Side join render");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("North Side join camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            ExplorationCharacterView frontActor = null;
            Sprite[] frontActorSprites = null;

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "lower");
                map.TryAddFloorCell(new Vector2Int(horizontalDirection, 1), "upper");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(
                    new DungeonCellEdge(
                        new Vector2Int(0, sideRow),
                        new Vector2Int(horizontalDirection, sideRow)),
                    full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)), full);
                if (sideRow == 0)
                    map.SetWallType(
                        new DungeonCellEdge(
                            new Vector2Int(horizontalDirection, 0),
                            new Vector2Int(horizontalDirection, 1)),
                        full);

                view.Rebuild(map);

                DungeonWallSegmentView side = null;
                DungeonWallSegmentView north = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
                {
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && wall.GetComponent<SpriteRenderer>().sprite == sideSprite)
                        side = wall;
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.North)
                        north = wall;
                }

                Assert.That(side, Is.Not.Null);
                Assert.That(north, Is.Not.Null);
                var sideGroup = side.GetComponent<SortingGroup>();
                var northGroup = north.GetComponent<SortingGroup>();
                Assert.That(sideGroup, Is.Not.Null);
                Assert.That(northGroup, Is.Not.Null);

                var activeCornerMiterCount = 0;
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null
                        || !renderer.gameObject.activeInHierarchy
                        || !renderer.name.StartsWith("CornerMiter")
                        || renderer.sprite == null)
                        continue;

                    activeCornerMiterCount++;
                    AssertDedicatedCornerSprite(renderer.sprite);
                }

                Assert.That(activeCornerMiterCount, Is.GreaterThanOrEqualTo(1),
                    "Every mirrored endpoint fixture must render one dedicated full-rect corner sprite.");

                var sideBounds = side.GetComponentsInChildren<SpriteRenderer>(true);
                var northBounds = north.GetComponentsInChildren<SpriteRenderer>(true);
                var hasOverlap = false;
                var overlap = default(Bounds);
                foreach (var sideRenderer in sideBounds)
                {
                    if (sideRenderer == null
                        || !sideRenderer.enabled
                        || !sideRenderer.gameObject.activeInHierarchy
                        || sideRenderer.name == "SideVoidOcclusion")
                        continue;

                    foreach (var northRenderer in northBounds)
                    {
                        if (northRenderer == null
                            || !northRenderer.enabled
                            || !northRenderer.gameObject.activeInHierarchy
                            || northRenderer.name == "SouthWallOcclusionMask")
                            continue;

                        var minX = Mathf.Max(sideRenderer.bounds.min.x, northRenderer.bounds.min.x);
                        var maxX = Mathf.Min(sideRenderer.bounds.max.x, northRenderer.bounds.max.x);
                        var minY = Mathf.Max(sideRenderer.bounds.min.y, northRenderer.bounds.min.y);
                        var maxY = Mathf.Min(sideRenderer.bounds.max.y, northRenderer.bounds.max.y);
                        if (maxX - minX <= .0001f || maxY - minY <= .0001f)
                            continue;

                        overlap = new Bounds(
                            new Vector3((minX + maxX) * .5f, (minY + maxY) * .5f, 0f),
                            new Vector3(maxX - minX, maxY - minY, 1f));
                        hasOverlap = true;
                        break;
                    }

                    if (hasOverlap)
                        break;
                }

                Assert.That(hasOverlap, Is.True,
                    "The mirrored fixture must contain a real Side/North visual overlap.");
                Assert.That(northGroup.sortingOrder, Is.LessThan(sideGroup.sortingOrder));
                SpriteRenderer joinOverlay = null;
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null && renderer.name == "NorthSideJoinOverlay")
                    {
                        joinOverlay = renderer;
                        break;
                    }

                SortingGroup joinGroup = null;
                if (sideRow == 0)
                {
                    Assert.That(joinOverlay, Is.Null,
                        "The upper Side endpoint must not create a North face join over its cap.");
                }
                else
                {
                    Assert.That(joinOverlay, Is.Not.Null);
                    Assert.That(joinOverlay.gameObject.activeSelf, Is.True);
                    joinGroup = joinOverlay.GetComponent<SortingGroup>();
                    Assert.That(joinGroup, Is.Not.Null);
                    Assert.That(joinGroup.sortingOrder, Is.GreaterThan(sideGroup.sortingOrder));
                }
                frontActor = CreateDetailedRenderActor(
                    root.transform,
                    map,
                    view,
                    new Vector2(.5f, .5f),
                    actorBodyTexture,
                    actorFootTexture,
                    actorWeaponTexture,
                    new Color(.95f, .08f, .04f, 1f),
                    out frontActorSprites);
                InvokePrivate(frontActor, "RefreshWorldDepthSorting");
                var frontActorGroup = frontActor.GetComponent<SortingGroup>();
                Assert.That(frontActorGroup, Is.Not.Null);
                Assert.That(northGroup.sortingOrder, Is.LessThan(frontActorGroup.sortingOrder),
                    "The base North wall must stay below an actor at feet Y=.5 outside the join.");
                if (joinGroup != null)
                    Assert.That(frontActorGroup.sortingOrder, Is.GreaterThan(joinGroup.sortingOrder),
                        "An actor in front of both walls must remain above the local face join.");

                var northCap = FindRenderer(side, "NorthSideCapOverlay");
                if (sideRow == 0)
                {
                    Assert.That(northCap, Is.Not.Null);
                    Assert.That(northCap.gameObject.activeSelf, Is.True);
                    Assert.That(northCap.sprite, Is.SameAs(sideSprite));
                    Assert.That(northCap.bounds.size.y, Is.EqualTo(.75f).Within(.0001f));
                }
                else
                {
                    Assert.That(northCap == null || !northCap.gameObject.activeSelf, Is.True,
                        "The lower Side endpoint must use the North face role.");
                }

                frontActor.gameObject.SetActive(false);
                camera.transform.position = new Vector3(.5f, 1.5f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 3f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();

                var overlapPixel = ReadRenderPixel(camera, renderTexture, overlap.center);
                if (sideRow == 0)
                {
                    Assert.That(overlapPixel.r, Is.GreaterThan(.65f),
                        "The upper Side endpoint must remain the authored cap.");
                    Assert.That(overlapPixel.b, Is.GreaterThan(.45f),
                        "The upper Side cap must remain visible at the join.");
                }
                else
                {
                    Assert.That(overlapPixel.g, Is.GreaterThan(.65f),
                        "The lower Side endpoint must show the North face.");
                    Assert.That(overlapPixel.b, Is.GreaterThan(.65f),
                        "The lower Side endpoint must show the North face, not the cap.");
                }
                frontActor.gameObject.SetActive(true);
                InvokePrivate(frontActor, "RefreshWorldDepthSorting");
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var frontNorthFacePixel = ReadRenderPixel(
                    camera,
                    renderTexture,
                    new Vector3(.5f, 1.25f, 0f));
                Assert.That(frontNorthFacePixel.r, Is.GreaterThan(.45f),
                    "An actor on the front North side must remain visible outside the corner join.");
            }
            finally
            {
                if (frontActor != null)
                    Object.DestroyImmediate(frontActor.gameObject);
                DestroySprites(frontActorSprites);
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(sideSprite);
                Object.DestroyImmediate(sideTexture);
                Object.DestroyImmediate(northSprite);
                Object.DestroyImmediate(northTexture);
                Object.DestroyImmediate(southSprite);
                Object.DestroyImmediate(southTexture);
                Object.DestroyImmediate(actorBodyTexture);
                Object.DestroyImmediate(actorFootTexture);
                Object.DestroyImmediate(actorWeaponTexture);
            }
        }

        [Test]
        public void MapViewRendersNativeCornerMiterPreviews()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            Assert.That(wallPrefab, Is.Not.Null);
            AssertNativeWallSprite(sideSprite, 8f, 32f);
            AssertNativeWallSprite(northSprite, 32f, 32f);
            AssertNativeWallSprite(southSprite, 32f, 8f);

            var outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "out",
                "wall-corners");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("Native wall corner room preview");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Native wall corner room preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++)
                    for (var y = 0; y < 3; y++)
                        map.TryAddFloorCell(new Vector2Int(x, y), "room");

                var full = Godmist.Core.Enums.DungeonWallType.Full;
                for (var x = 0; x < 3; x++)
                {
                    map.SetWallType(
                        new DungeonCellEdge(new Vector2Int(x, 2), new Vector2Int(x, 3)),
                        full);
                    map.SetWallType(
                        new DungeonCellEdge(new Vector2Int(x, -1), new Vector2Int(x, 0)),
                        full);
                }

                for (var y = 0; y < 3; y++)
                {
                    map.SetWallType(
                        new DungeonCellEdge(new Vector2Int(-1, y), new Vector2Int(0, y)),
                        full);
                    map.SetWallType(
                        new DungeonCellEdge(new Vector2Int(2, y), new Vector2Int(3, y)),
                        full);
                }

                view.Rebuild(map);
                var activeCornerMiterCount = 0;
                template.SetActive(false);
                var cornerMiterSummary = new List<string>();
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name.StartsWith("CornerMiter"))
                    {
                        activeCornerMiterCount++;
                        AssertDedicatedCornerSprite(renderer.sprite);
                        cornerMiterSummary.Add(
                            $"{renderer.name}@{renderer.bounds.center.x:F2},{renderer.bounds.center.y:F2}");
                    }
                camera.orthographic = true;
                camera.orthographicSize = .9f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                var corners = new[]
                {
                    (Name: "NW", CameraPoint: new Vector3(0f, 3.875f, 0f), SamplePoint: new Vector3(0f, 3.875f, 0f)),
                    (Name: "NE", CameraPoint: new Vector3(3f, 3.875f, 0f), SamplePoint: new Vector3(3f, 3.875f, 0f)),
                    (Name: "SW", CameraPoint: new Vector3(0f, .625f, 0f), SamplePoint: new Vector3(0f, .625f, 0f)),
                    (Name: "SE", CameraPoint: new Vector3(3f, .625f, 0f), SamplePoint: new Vector3(3f, .625f, 0f))
                };
                var cornerPixelsContinuous = true;
                var cornerPixelSummary = new List<string>();
                foreach (var corner in corners)
                {
                    camera.transform.position = corner.CameraPoint + new Vector3(0f, 0f, -10f);
                    SortingGroup.UpdateAllSortingGroups();
                    camera.Render();
                    SaveRenderTextureCapture(
                        renderTexture,
                        Path.Combine(outputDirectory, $"{corner.Name}.png"));

                    var renderedDedicatedCorners = 0;
                    foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (renderer == null
                            || !renderer.gameObject.activeInHierarchy
                            || !renderer.name.StartsWith("CornerMiter")
                            || renderer.sprite == null
                            || Vector2.Distance(
                                   new Vector2(renderer.bounds.center.x, renderer.bounds.center.y),
                                   new Vector2(corner.SamplePoint.x, corner.SamplePoint.y)) > .3f)
                            continue;

                        AssertDedicatedCornerSprite(renderer.sprite);
                        var pixel = ReadRenderPixel(
                            camera,
                            renderTexture,
                            renderer.bounds.center);
                        if (pixel.maxColorComponent > .02f)
                            renderedDedicatedCorners++;
                    }

                    cornerPixelSummary.Add($"{corner.Name}:{renderedDedicatedCorners}");
                    // Each corner is one opaque full-rect runtime sprite. The
                    // center sample guards against an inactive/occluded sprite;
                    // the sprite topology assertion above guards against the
                    // old two-triangle diagonal seam.
                    cornerPixelsContinuous &= renderedDedicatedCorners >= 1;
                }

                Assert.That(activeCornerMiterCount, Is.GreaterThanOrEqualTo(4),
                    string.Join("; ", cornerMiterSummary));
                Assert.That(cornerPixelsContinuous, Is.True,
                    $"Each native room corner must render one visible dedicated sprite: {string.Join("; ", cornerPixelSummary)}");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void MapViewCapturesNativeMirroredStepCornerMiter(int horizontalDirection)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            Assert.That(wallPrefab, Is.Not.Null);
            AssertNativeWallSprite(sideSprite, 8f, 32f);
            AssertNativeWallSprite(northSprite, 32f, 32f);
            AssertNativeWallSprite(southSprite, 32f, 8f);

            var outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "out",
                "wall-corners");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject($"Native mirrored step preview {horizontalDirection}");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Native mirrored step preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "lower");
                map.TryAddFloorCell(new Vector2Int(horizontalDirection, 1), "upper");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(
                    new DungeonCellEdge(
                        new Vector2Int(0, 0),
                        new Vector2Int(horizontalDirection, 0)),
                    full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)), full);
                map.SetWallType(
                    new DungeonCellEdge(
                        new Vector2Int(horizontalDirection, 0),
                        new Vector2Int(horizontalDirection, 1)),
                    full);

                view.Rebuild(map);

                var miterRenderers = new List<SpriteRenderer>();
                template.SetActive(false);
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name.StartsWith("CornerMiter")
                        && renderer.sprite != null)
                    {
                        AssertDedicatedCornerSprite(renderer.sprite);
                        miterRenderers.Add(renderer);
                    }

                Assert.That(miterRenderers, Has.Count.GreaterThanOrEqualTo(1),
                    "The mirrored step must produce one dedicated full-rect corner sprite.");
                var focus = miterRenderers[0].bounds.center;
                camera.orthographic = true;
                camera.orthographicSize = .7f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                camera.transform.position = new Vector3(focus.x, focus.y, -10f);
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                SaveRenderTextureCapture(
                    renderTexture,
                    Path.Combine(outputDirectory, $"step-{horizontalDirection}.png"));

                var visibleDedicatedCorners = 0;
                foreach (var renderer in miterRenderers)
                {
                    AssertDedicatedCornerSprite(renderer.sprite);
                    var pixel = ReadRenderPixel(
                        camera,
                        renderTexture,
                        renderer.bounds.center);
                    if (pixel.maxColorComponent > .02f)
                        visibleDedicatedCorners++;
                }

                Assert.That(visibleDedicatedCorners, Is.GreaterThanOrEqualTo(1),
                    $"Mirrored step {horizontalDirection} must show its dedicated corner sprite in the native capture.");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void MapViewCapturesNativeBayStepCornerDirections(int mirrorDirection)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            Assert.That(wallPrefab, Is.Not.Null);
            AssertNativeWallSprite(sideSprite, 8f, 32f);
            AssertNativeWallSprite(northSprite, 32f, 32f);
            AssertNativeWallSprite(southSprite, 32f, 8f);

            var outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "out",
                "wall-corners");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject($"Native bay step preview {mirrorDirection}");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Native bay step preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var baseCells = new List<Vector2Int>();
                for (var x = 2; x <= 3; x++)
                    for (var y = 0; y <= 4; y++)
                        baseCells.Add(new Vector2Int(x, y));
                for (var x = 0; x <= 1; x++)
                    for (var y = 1; y <= 2; y++)
                        baseCells.Add(new Vector2Int(x, y));

                var cells = new HashSet<Vector2Int>();
                foreach (var baseCell in baseCells)
                {
                    var cell = mirrorDirection > 0
                        ? baseCell
                        : new Vector2Int(-baseCell.x - 1, baseCell.y);
                    cells.Add(cell);
                }

                var map = new DungeonFloorMap();
                foreach (var cell in cells)
                    map.TryAddFloorCell(cell, "bay-step");

                var full = Godmist.Core.Enums.DungeonWallType.Full;
                var directions = new[]
                {
                    Vector2Int.right,
                    Vector2Int.left,
                    Vector2Int.up,
                    Vector2Int.down
                };
                foreach (var cell in cells)
                    foreach (var direction in directions)
                    {
                        var neighbor = cell + direction;
                        if (!cells.Contains(neighbor))
                            map.SetWallType(new DungeonCellEdge(cell, neighbor), full);
                    }

                view.Rebuild(map);
                // The prefab is only an Instantiate source. It must not remain
                // at world origin and contribute a stray wall to the capture.
                template.SetActive(false);

                var miterRenderers = new List<SpriteRenderer>();
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name.StartsWith("CornerMiter")
                        && renderer.sprite != null)
                    {
                        AssertDedicatedCornerSprite(renderer.sprite);
                        miterRenderers.Add(renderer);
                    }

                var baseCorners = new[]
                {
                    (Name: "NW", Point: new Vector2(0f, 3.875f), GeometryX: -1f, GeometryY: 1f),
                    (Name: "NE", Point: new Vector2(2f, 3.875f), GeometryX: 1f, GeometryY: -1f),
                    (Name: "SW", Point: new Vector2(0f, 1.625f), GeometryX: -1f, GeometryY: -1f),
                    (Name: "SE", Point: new Vector2(2f, 1.625f), GeometryX: 1f, GeometryY: 1f)
                };

                camera.orthographic = true;
                camera.orthographicSize = 3.1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                camera.transform.position = new Vector3(
                    mirrorDirection > 0 ? 2f : -2f,
                    2.5f,
                    -10f);
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                SaveRenderTextureCapture(
                    renderTexture,
                    Path.Combine(outputDirectory, $"bay-step-{mirrorDirection}.png"));

                var lowerMiterRenderers = new List<SpriteRenderer>();
                var lowerMiterCaptures = new Dictionary<string, Color[]>();

                foreach (var corner in baseCorners)
                {
                    var point = new Vector2(
                        mirrorDirection > 0 ? corner.Point.x : -corner.Point.x,
                        corner.Point.y);
                    camera.orthographicSize = .45f;
                    camera.transform.position = new Vector3(point.x, point.y, -10f);
                    camera.Render();
                    SaveRenderTextureCapture(renderTexture,
                        Path.Combine(outputDirectory, $"bay-step-{mirrorDirection}-{corner.Name}.png"));
                    if (corner.Name == "SW" || corner.Name == "SE")
                        lowerMiterCaptures[corner.Name] = CaptureRenderPixels(renderTexture);
                    var geometryXDirection = mirrorDirection > 0
                        ? corner.GeometryX
                        : -corner.GeometryX;
                    var candidates = new List<SpriteRenderer>();
                    foreach (var renderer in miterRenderers)
                        if (Vector2.Distance(
                                new Vector2(renderer.bounds.center.x, renderer.bounds.center.y),
                                point) < .3f)
                            candidates.Add(renderer);

                    Assert.That(candidates, Has.Count.EqualTo(1),
                        $"Bay step {mirrorDirection} corner {corner.Name} must have one dedicated full-rect corner sprite. " + string.Join("; ", candidates.ConvertAll(renderer => $"{renderer.name} {renderer.bounds.center} parent={renderer.transform.parent.name}")));
                    var visibleCorners = 0;
                    foreach (var renderer in candidates)
                    {
                        AssertDedicatedCornerSprite(renderer.sprite);
                        if ((corner.Name == "NW" || corner.Name == "NE")
                            && renderer.name == "CornerMiterNorthCap")
                        {
                            AssertNorthCapKeepsNativeArt(renderer.sprite);
                            AssertNorthCapCornerBorderPaths(
                                renderer.sprite,
                                geometryXDirection > 0f,
                                corner.GeometryY > 0f);
                        }
                        var pixel = ReadRenderPixel(
                            camera,
                            renderTexture,
                            renderer.bounds.center);
                        if (pixel.maxColorComponent > .02f)
                            visibleCorners++;
                    }

                    Assert.That(visibleCorners, Is.EqualTo(1),
                        $"Bay step {mirrorDirection} corner {corner.Name} must have no gap in its dedicated sprite.");

                    if (corner.Name == "SW" || corner.Name == "SE")
                    {
                        SpriteRenderer faceRenderer = null;
                        foreach (var renderer in candidates)
                            if (renderer.name == "CornerMiterSouthFace")
                            {
                                faceRenderer = renderer;
                                break;
                            }

                        var southRenderer = FindSouthHorizontalRenderer(root, point);
                        var sideRenderer = FindSideVerticalRenderer(root, point);
                        Assert.That(faceRenderer, Is.Not.Null,
                            $"Bay step {mirrorDirection} corner {corner.Name} must have a South face miter.");
                        Assert.That(southRenderer, Is.Not.Null,
                            $"Bay step {mirrorDirection} corner {corner.Name} must resolve its horizontal South source.");
                        Assert.That(sideRenderer, Is.Not.Null,
                            $"Bay step {mirrorDirection} corner {corner.Name} must resolve its vertical Side source.");

                        // The dedicated runtime texture is a self-contained
                        // 8x8 crop. Its source phase is validated by the
                        // captured native pattern; the runtime sprite must not
                        // expose the old atlas crop metadata.
                        Assert.That(faceRenderer.sprite.texture.width, Is.EqualTo(8));
                        Assert.That(faceRenderer.sprite.texture.height, Is.EqualTo(8));
                        AssertSouthFaceMatchesNeighborPhases(
                            faceRenderer,
                            southRenderer,
                            sideRenderer,
                            geometryXDirection > 0f,
                            corner.GeometryY > 0f);
                        AssertSouthFaceCornerBorderPaths(
                            faceRenderer.sprite,
                            geometryXDirection > 0f,
                            corner.GeometryY > 0f);
                    }

                    if (corner.Name == "SW" || corner.Name == "SE")
                    {
                        foreach (var renderer in candidates)
                            lowerMiterRenderers.Add(renderer);
                    }
                }

                foreach (var renderer in lowerMiterRenderers)
                    renderer.gameObject.SetActive(false);

                var changedLowerPixels = 0;
                foreach (var cornerName in new[] { "SW", "SE" })
                {
                    var corner = System.Array.Find(
                        baseCorners,
                        value => value.Name == cornerName);
                    var point = new Vector2(
                        mirrorDirection > 0 ? corner.Point.x : -corner.Point.x,
                        corner.Point.y);
                    camera.transform.position = new Vector3(point.x, point.y, -10f);
                    camera.orthographicSize = .45f;
                    SortingGroup.UpdateAllSortingGroups();
                    camera.Render();
                    var withoutCorner = CaptureRenderPixels(renderTexture);
                    var withCorner = lowerMiterCaptures[cornerName];
                    for (var pixelIndex = 0; pixelIndex < withoutCorner.Length; pixelIndex++)
                        if (ColorDistance(withCorner[pixelIndex], withoutCorner[pixelIndex]) > .02f)
                            changedLowerPixels++;
                }

                Assert.That(changedLowerPixels, Is.GreaterThan(8),
                    "South corner render must change its visible raster when the dedicated sprite is disabled; "
                    + "renderer counts and hidden centroids must not pass the regression.");

                foreach (var renderer in lowerMiterRenderers)
                    renderer.gameObject.SetActive(true);
                SortingGroup.UpdateAllSortingGroups();
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewCapturesEightJunctionNarrowPassageWithDedicatedCorners()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            Assert.That(wallPrefab, Is.Not.Null);
            AssertNativeWallSprite(sideSprite, 8f, 32f);
            AssertNativeWallSprite(northSprite, 32f, 32f);
            AssertNativeWallSprite(southSprite, 32f, 8f);

            var outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "out",
                "wall-corners");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("Native eight-junction narrow passage");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Eight-junction passage camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                // Wide lower room, one-cell inset on both sides, then the
                // wide upper room. The four inset transitions plus four outer
                // perimeter corners form the eight junctions from the user
                // corridor screenshot.
                var cells = new HashSet<Vector2Int>();
                for (var y = 0; y <= 1; y++)
                    for (var x = 0; x <= 4; x++)
                        cells.Add(new Vector2Int(x, y));
                for (var y = 2; y <= 3; y++)
                    for (var x = 1; x <= 3; x++)
                        cells.Add(new Vector2Int(x, y));
                for (var y = 4; y <= 5; y++)
                    for (var x = 0; x <= 4; x++)
                        cells.Add(new Vector2Int(x, y));

                var map = new DungeonFloorMap();
                foreach (var cell in cells)
                    map.TryAddFloorCell(cell, "narrow-passage");

                var full = Godmist.Core.Enums.DungeonWallType.Full;
                var directions = new[]
                {
                    Vector2Int.right,
                    Vector2Int.left,
                    Vector2Int.up,
                    Vector2Int.down
                };
                foreach (var cell in cells)
                    foreach (var direction in directions)
                    {
                        var neighbor = cell + direction;
                        if (!cells.Contains(neighbor))
                            map.SetWallType(new DungeonCellEdge(cell, neighbor), full);
                    }

                view.Rebuild(map);
                template.SetActive(false);

                var miterRenderers = new List<SpriteRenderer>();
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null
                        || !renderer.gameObject.activeInHierarchy
                        || !renderer.name.StartsWith("CornerMiter")
                        || renderer.sprite == null)
                        continue;

                    AssertDedicatedCornerSprite(renderer.sprite);
                    miterRenderers.Add(renderer);
                }

                var junctions = new List<SpriteRenderer>();
                foreach (var renderer in miterRenderers)
                {
                    var duplicate = false;
                    foreach (var existing in junctions)
                        if (Vector2.Distance(existing.bounds.center, renderer.bounds.center) < .05f)
                        {
                            duplicate = true;
                            break;
                        }

                    if (!duplicate)
                        junctions.Add(renderer);
                }

                Assert.That(junctions, Has.Count.GreaterThanOrEqualTo(8),
                    $"The narrowed passage must expose eight distinct corner junctions, got {junctions.Count}.");

                camera.orthographic = true;
                camera.orthographicSize = 3.8f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                camera.transform.position = new Vector3(2.5f, 3f, -10f);
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                SaveRenderTextureCapture(
                    renderTexture,
                    Path.Combine(outputDirectory, "narrow-passage-8-junctions-full.png"));

                for (var index = 0; index < junctions.Count; index++)
                {
                    var junction = junctions[index];
                    camera.orthographicSize = .38f;
                    camera.transform.position = junction.bounds.center + new Vector3(0f, 0f, -10f);
                    SortingGroup.UpdateAllSortingGroups();
                    camera.Render();
                    SaveRenderTextureCapture(
                        renderTexture,
                        Path.Combine(outputDirectory, $"narrow-passage-junction-{index:00}.png"));

                    var visibleSamples = 0;
                    var center = junction.bounds.center;
                    var halfWidth = Mathf.Max(.015f, junction.bounds.extents.x * .55f);
                    var halfHeight = Mathf.Max(.015f, junction.bounds.extents.y * .55f);
                    foreach (var sample in new[]
                    {
                        center,
                        center + new Vector3(-halfWidth, 0f, 0f),
                        center + new Vector3(halfWidth, 0f, 0f),
                        center + new Vector3(0f, -halfHeight, 0f),
                        center + new Vector3(0f, halfHeight, 0f)
                    })
                    {
                        if (ReadRenderPixel(camera, renderTexture, sample).maxColorComponent > .02f)
                            visibleSamples++;
                    }

                    Assert.That(visibleSamples, Is.GreaterThan(0),
                        $"Narrow-passage junction {index} must remain visible in its native close capture.");
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewRehomesMixedSouthJunctionWithoutStaleCapMiter()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon mixed South passage junction");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Mixed South passage camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                // This is the short passage transition: the vertical Side
                // segment at x=1 meets a North strip on its left and a South
                // strip on its right at the upper endpoint. The bind path
                // creates a SouthCap pair here; the owner pass must replace
                // that exact pair with the visible SouthFace miter.
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "lower");
                map.TryAddFloorCell(new Vector2Int(1, 1), "upper");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    full);
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(0, 1)),
                    full);
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(1, 1)),
                    full);

                view.Rebuild(map);
                template.SetActive(false);

                DungeonWallSegmentView sideWall = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
                    if (wall != null && wall.SurfaceProfile == DungeonWallSurfaceProfile.Side)
                    {
                        sideWall = wall;
                        break;
                    }
                Assert.That(sideWall, Is.Not.Null);

                var endpoint = new Vector2(1f, 1f);
                var southRenderer = FindSouthHorizontalRenderer(root, endpoint);
                Assert.That(southRenderer, Is.Not.Null,
                    "The mixed passage must expose its horizontal South source renderer.");
                var southVisualOffsetWorld = southRenderer.bounds.min.y
                                              - endpoint.y
                                              + DungeonWallSegmentView.NativeWallDepthWorld;
                var cornerCenter = endpoint
                                   + Vector2.up
                                   * (southVisualOffsetWorld
                                      - DungeonWallSegmentView.NativeWallDepthWorld * .5f);
                var cornerRenderers = new List<SpriteRenderer>();
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name.StartsWith("CornerMiter")
                        && Vector2.Distance(renderer.transform.position, cornerCenter) < .001f)
                        cornerRenderers.Add(renderer);

                Assert.That(cornerRenderers, Has.Count.EqualTo(1),
                    "The mixed South junction must contain one dedicated full-rect corner sprite.");
                var southFaceMiter = cornerRenderers.Find(
                    renderer => renderer.name == "CornerMiterSouthFace");
                SpriteRenderer southFaceOverlay = null;
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name == "SouthSideFaceOverlay")
                    {
                        southFaceOverlay = renderer;
                        break;
                    }
                Assert.That(southFaceMiter, Is.Not.Null);
                AssertDedicatedCornerSprite(southFaceMiter.sprite);
                Assert.That(southFaceOverlay, Is.Not.Null,
                    "The mixed junction keeps its active South face patch as the visual replacement contract.");
                Assert.That(southFaceOverlay.sprite, Is.SameAs(southSprite),
                    "The rehomed mixed patch must use the native South face material, never the Side cap texture.");
                Assert.That(southFaceOverlay.transform.IsChildOf(sideWall.transform), Is.False,
                    "The active South face patch must be outside the Side SortingGroup.");
                Assert.That(southFaceMiter.bounds.min.x,
                    Is.EqualTo(southFaceOverlay.bounds.min.x).Within(.0001f));
                Assert.That(southFaceMiter.bounds.max.x,
                    Is.EqualTo(southFaceOverlay.bounds.max.x).Within(.0001f));
                Assert.That(southFaceMiter.bounds.min.y,
                    Is.EqualTo(southFaceOverlay.bounds.min.y).Within(.0001f));
                Assert.That(southFaceMiter.bounds.max.y,
                    Is.EqualTo(southFaceOverlay.bounds.max.y).Within(.0001f));

                var diagnostics = new System.Text.StringBuilder();
                diagnostics.AppendLine($"cornerCenter={cornerCenter}");
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null)
                        continue;

                    var group = renderer.GetComponentInParent<SortingGroup>();
                    var bounds = renderer.bounds;
                    var spriteRect = renderer.sprite != null
                        ? renderer.sprite.textureRect.ToString()
                        : "<null>";
                    diagnostics.AppendLine(
                        $"name={renderer.name};active={renderer.gameObject.activeInHierarchy};"
                        + $"rendererOrder={renderer.sortingOrder};layer={renderer.sortingLayerID};"
                        + $"group={(group != null ? group.name : "<none>")};"
                        + $"groupActive={(group != null && group.enabled)};"
                        + $"groupOrder={(group != null ? group.sortingOrder : 0)};"
                        + $"sortAtRoot={(group != null && group.sortAtRoot)};"
                        + $"position={renderer.transform.position};"
                        + $"boundsMin={bounds.min};boundsMax={bounds.max};"
                        + $"spriteRect={spriteRect}");
                }
                File.WriteAllText(
                    Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName,
                        "out",
                        "wall-corners",
                        "mixed-south-diagnostics.txt"),
                    diagnostics.ToString());

                camera.orthographic = true;
                camera.orthographicSize = .55f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;
                camera.transform.position = new Vector3(cornerCenter.x, cornerCenter.y, -10f);
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                SaveRenderTextureCapture(
                    renderTexture,
                    Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName,
                        "out",
                        "wall-corners",
                        "mixed-south-passage.png"));

                // The dedicated corner is one full-rect sprite. Compare its
                // complete raster with the miter disabled so a hidden or
                // metadata-only renderer cannot satisfy this regression.
                var withMiterPixels = CaptureRenderPixels(renderTexture);

                foreach (var renderer in cornerRenderers)
                    renderer.gameObject.SetActive(false);
                SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                SaveRenderTextureCapture(
                    renderTexture,
                    Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName,
                        "out",
                        "wall-corners",
                        "mixed-south-passage-without-miter.png"));
                var withoutMiterPixels = CaptureRenderPixels(renderTexture);
                var minScreen = camera.WorldToScreenPoint(southFaceMiter.bounds.min);
                var maxScreen = camera.WorldToScreenPoint(southFaceMiter.bounds.max);
                var changedPixels = 0;
                var minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(minScreen.x, maxScreen.x)), 0, renderTexture.width - 1);
                var maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(minScreen.x, maxScreen.x)), 0, renderTexture.width - 1);
                var minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(minScreen.y, maxScreen.y)), 0, renderTexture.height - 1);
                var maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(minScreen.y, maxScreen.y)), 0, renderTexture.height - 1);
                for (var y = minY; y <= maxY; y++)
                    for (var x = minX; x <= maxX; x++)
                    {
                        var index = y * renderTexture.width + x;
                        if (ColorDistance(withMiterPixels[index], withoutMiterPixels[index]) > .02f)
                            changedPixels++;
                    }

                Assert.That(changedPixels, Is.GreaterThan(0),
                    "The mixed South passage must visibly change when its dedicated corner sprite is disabled.");
                foreach (var renderer in cornerRenderers)
                    renderer.gameObject.SetActive(true);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewExternalSouthJoinKeepsSingleMiterWithoutFaceRehome()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon external South join");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "room");
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    Godmist.Core.Enums.DungeonWallType.Full);
                map.SetWallType(
                    new DungeonCellEdge(new Vector2Int(0, -1), new Vector2Int(0, 0)),
                    Godmist.Core.Enums.DungeonWallType.Full);

                view.Rebuild(map);
                template.SetActive(false);

                DungeonWallSegmentView side = null;
                DungeonWallSegmentView south = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
                {
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.Side)
                        side = wall;
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South)
                        south = wall;
                }

                Assert.That(side, Is.Not.Null);
                Assert.That(south, Is.Not.Null);
                var southRenderer = south.GetComponent<SpriteRenderer>();
                Assert.That(southRenderer, Is.Not.Null);
                var endpoint = new Vector2(1f, 0f);
                var southVisualOffset = southRenderer.bounds.min.y
                    - endpoint.y
                    + DungeonWallSegmentView.NativeWallDepthWorld;
                var center = endpoint + Vector2.up * (
                    southVisualOffset - DungeonWallSegmentView.NativeWallDepthWorld * .5f);
                var activeMiters = new List<SpriteRenderer>();
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name == "CornerMiterSouthFace"
                        && Vector2.Distance(renderer.bounds.center, center) < .001f)
                        activeMiters.Add(renderer);

                Assert.That(activeMiters, Has.Count.EqualTo(1),
                    "An exterior South join must not retain the bind-time Side miter when South owns the replacement.");
                Assert.That(FindRenderer(south, "SouthSideFaceOverlay"), Is.Null,
                    "An exterior join has no mixed face patch to rehome.");
                AssertDedicatedCornerSprite(activeMiters[0].sprite);
                Assert.That(activeMiters[0].bounds.size.x, Is.EqualTo(.25f).Within(.0001f));

            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void SouthOwnedMiterUsesProvidedSideBodyFaceSprite()
        {
            var root = new GameObject("South-owned mixed face miter");
            var owner = root.AddComponent<DungeonWallSegmentView>();
            var ownerRenderer = root.AddComponent<SpriteRenderer>();
            var sideObject = new GameObject("Mixed Side body");
            sideObject.transform.SetParent(root.transform, false);
            var sideRenderer = sideObject.AddComponent<SpriteRenderer>();
            var fallbackTexture = CreateSolidTexture(8, 32, Color.green);
            var faceTexture = CreateSolidTexture(8, 32, Color.magenta);
            var southTexture = CreateSolidTexture(32, 8, Color.cyan);
            var fallbackSprite = Sprite.Create(
                fallbackTexture, new Rect(0f, 0f, 8f, 32f), new Vector2(.5f, .5f), 32f);
            var faceSprite = Sprite.Create(
                faceTexture, new Rect(0f, 0f, 8f, 32f), new Vector2(.5f, .5f), 32f);
            var southSprite = Sprite.Create(
                southTexture, new Rect(0f, 0f, 32f, 8f), new Vector2(.5f, .5f), 32f);

            try
            {
                SetField(owner, "<SurfaceProfile>k__BackingField", DungeonWallSurfaceProfile.South);
                SetField(owner, "sideWallSprite", fallbackSprite);
                SetField(owner, "southWallSprite", southSprite);
                ownerRenderer.sprite = southSprite;
                ownerRenderer.transform.position = new Vector3(1f, 1f, 0f);
                sideRenderer.sprite = faceSprite;
                sideRenderer.transform.position = new Vector3(1f, 1.5f, 0f);

                var endpoint = new Vector2(1f, 1f);
                var southVisualOffset = ownerRenderer.bounds.min.y
                    - endpoint.y
                    + DungeonWallSegmentView.NativeWallDepthWorld;
                var configureMethod = typeof(DungeonWallSegmentView).GetMethod(
                    "ConfigureSouthCornerMiterAt",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
                Assert.That(configureMethod, Is.Not.Null);
                var configured = (bool)configureMethod.Invoke(
                    owner,
                    new object[]
                    {
                        endpoint,
                        new Vector2(1f, 0f),
                        Vector2.right,
                        southVisualOffset,
                        ownerRenderer.bounds,
                        sideRenderer.bounds,
                        faceSprite
                    });

                Assert.That(configured, Is.True,
                    "A South-owned mixed miter must be created when its body face is supplied.");
                var miter = FindRenderer(owner, "CornerMiterSouthFace");
                Assert.That(miter, Is.Not.Null);
                var hasFacePixel = false;
                foreach (var pixel in miter.sprite.texture.GetPixels32())
                    if (pixel.r == 255 && pixel.g == 0 && pixel.b == 255 && pixel.a == 255)
                    {
                        hasFacePixel = true;
                        break;
                    }

                Assert.That(hasFacePixel, Is.True,
                    "The South-owned miter must sample the mixed Side body face, not the fallback Side cap sprite.");
            }
            finally
            {
                Object.DestroyImmediate(fallbackSprite);
                Object.DestroyImmediate(faceSprite);
                Object.DestroyImmediate(southSprite);
                Object.DestroyImmediate(fallbackTexture);
                Object.DestroyImmediate(faceTexture);
                Object.DestroyImmediate(southTexture);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MapViewSharedWallKeepsLowerWallDepthPriority()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon short horizontal notch");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(1, 0), "lower-room");
                map.TryAddFloorCell(new Vector2Int(1, 2), "upper-room");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(1, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)), full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                DungeonWallSegmentView north = null;
                DungeonWallSegmentView south = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                {
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.North)
                        north = wall;
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South)
                        south = wall;
                }

                Assert.That(north, Is.Not.Null);
                Assert.That(south, Is.Not.Null);
                var northRenderer = north.GetComponent<SpriteRenderer>();
                var southRenderer = south.GetComponent<SpriteRenderer>();
                Assert.That(northRenderer.sprite, Is.SameAs(northSprite));
                Assert.That(southRenderer.sprite, Is.SameAs(southSprite));
                Assert.That(northRenderer.bounds.min.y, Is.EqualTo(1f).Within(.0001f));
                Assert.That(northRenderer.bounds.max.y, Is.EqualTo(2f).Within(.0001f));
                Assert.That(southRenderer.bounds.min.y, Is.EqualTo(2.5f).Within(.0001f));
                Assert.That(southRenderer.bounds.max.y, Is.EqualTo(2.75f).Within(.0001f));
                Assert.That(southRenderer.bounds.min.y - northRenderer.bounds.max.y,
                    Is.EqualTo(.5f).Within(.0001f));
                SpriteRenderer southMask = null;
                foreach (var renderer in south.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.name == "SouthWallOcclusionMask")
                        southMask = renderer;
                Assert.That(southMask, Is.Not.Null);
                Assert.That(southMask.gameObject.activeSelf, Is.True);
                Assert.That(southMask.bounds.min.y, Is.EqualTo(2f).Within(.0001f));
                Assert.That(southMask.bounds.max.y, Is.EqualTo(2.5f).Within(.0001f));
                Assert.That(southMask.sortingOrder, Is.LessThan(southRenderer.sortingOrder));

                var northGroup = north.GetComponent<SortingGroup>();
                var southGroup = south.GetComponent<SortingGroup>();
                Assert.That(northGroup, Is.Not.Null);
                Assert.That(southGroup, Is.Not.Null);
                Assert.That(north.DepthSortBaselineY, Is.EqualTo(1f).Within(.0001f));
                Assert.That(south.DepthSortBaselineY, Is.EqualTo(2f).Within(.0001f));
                Assert.That(northGroup.sortingOrder, Is.GreaterThan(southGroup.sortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [TestCase(.75f, 2.5f, 2.75f, .5f)]
        [TestCase(.5f, 2.25f, 2.5f, .25f)]
        public void MapViewSameRoomHorizontalNotchUsesConfiguredSouthOffset(
            float offsetCells,
            float expectedSouthMinY,
            float expectedSouthMaxY,
            float expectedMaskHeight)
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon same-room horizontal notch");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);
                SetField(templateView, "southVisualOffsetCells", offsetCells);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                for (var x = 0; x < 3; x++)
                    for (var y = 0; y < 3; y++)
                        if (x != 1 || y != 1)
                            map.TryAddFloorCell(new Vector2Int(x, y), "recess");

                var full = Godmist.Core.Enums.DungeonWallType.Full;
                var northEdge = new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(1, 1));
                var southEdge = new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2));
                map.SetWallType(northEdge, full);
                map.SetWallType(southEdge, full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                DungeonWallSegmentView north = null;
                DungeonWallSegmentView south = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                {
                    if (wall.SurfaceProfile == DungeonWallSurfaceProfile.North
                        && Mathf.Abs(wall.DepthSortBaselineY - 1f) < .0001f)
                        north = wall;
                    else if (wall.SurfaceProfile == DungeonWallSurfaceProfile.South
                             && Mathf.Abs(wall.DepthSortBaselineY - 2f) < .0001f)
                        south = wall;
                }

                Assert.That(north, Is.Not.Null);
                Assert.That(south, Is.Not.Null);
                var northRenderer = north.GetComponent<SpriteRenderer>();
                var southRenderer = south.GetComponent<SpriteRenderer>();
                Assert.That(northRenderer.sprite, Is.SameAs(northSprite));
                Assert.That(southRenderer.sprite, Is.SameAs(southSprite));
                Assert.That(northRenderer.bounds.min.y, Is.EqualTo(1f).Within(.0001f));
                Assert.That(northRenderer.bounds.max.y, Is.EqualTo(2f).Within(.0001f));
                Assert.That(southRenderer.bounds.min.y, Is.EqualTo(expectedSouthMinY).Within(.0001f));
                Assert.That(southRenderer.bounds.max.y, Is.EqualTo(expectedSouthMaxY).Within(.0001f));
                Assert.That(southRenderer.bounds.min.y,
                    Is.GreaterThan(northRenderer.bounds.min.y));
                Assert.That(southRenderer.bounds.max.y,
                    Is.GreaterThan(northRenderer.bounds.min.y));
                var southCollider = south.GetComponent<BoxCollider2D>();
                Assert.That(southCollider, Is.Not.Null);
                Assert.That(southCollider.bounds.center.y, Is.EqualTo(2f).Within(.0001f));
                SpriteRenderer southMask = null;
                foreach (var renderer in south.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.name == "SouthWallOcclusionMask")
                        southMask = renderer;
                Assert.That(southMask, Is.Not.Null);
                Assert.That(southMask.gameObject.activeSelf, Is.True);
                Assert.That(southMask.bounds.min.y, Is.EqualTo(2f).Within(.0001f));
                Assert.That(southMask.bounds.max.y,
                    Is.EqualTo(expectedSouthMinY).Within(.0001f));
                Assert.That(southMask.bounds.size.y,
                    Is.EqualTo(expectedMaskHeight).Within(.0001f));
                Assert.That(southMask.sortingOrder, Is.LessThan(southRenderer.sortingOrder));

                var northGroup = north.GetComponent<SortingGroup>();
                var southGroup = south.GetComponent<SortingGroup>();
                Assert.That(northGroup, Is.Not.Null);
                Assert.That(southGroup, Is.Not.Null);
                Assert.That(northGroup.sortingOrder, Is.GreaterThan(southGroup.sortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewDepthSortsLowerWallBaselineAboveHigherWallBaseline()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var root = new GameObject("Dungeon depth sorting view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            GameObject objectObject = null;
            GameObject actorObject = null;
            GameObject hudObject = null;

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                for (var y = 0; y < 32; y++)
                {
                    map.TryAddFloorCell(new Vector2Int(0, y), "room");
                    map.TryAddFloorCell(new Vector2Int(1, y), "room");
                }

                var full = Godmist.Core.Enums.DungeonWallType.Full;
                for (var y = 0; y < 32; y++)
                    map.SetWallType(
                        new DungeonCellEdge(new Vector2Int(0, y), new Vector2Int(1, y)),
                        full);

                view.Rebuild(map);
                Physics2D.SyncTransforms();

                DungeonWallSegmentView lowerWall = null;
                DungeonWallSegmentView adjacentHigherWall = null;
                DungeonWallSegmentView farHigherWall = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>())
                {
                    if (wall.SurfaceProfile != DungeonWallSurfaceProfile.Side
                        || !wall.UsesNativeWallCrossSection)
                        continue;

                    if (wall.DepthSortBaselineY < 1f) lowerWall = wall;
                    else if (wall.DepthSortBaselineY < 2f) adjacentHigherWall = wall;
                    else if (wall.DepthSortBaselineY > 10f
                             && (farHigherWall == null
                                 || wall.DepthSortBaselineY > farHigherWall.DepthSortBaselineY))
                        farHigherWall = wall;
                }

                Assert.That(lowerWall, Is.Not.Null);
                Assert.That(adjacentHigherWall, Is.Not.Null);
                Assert.That(farHigherWall, Is.Not.Null);
                Assert.That(lowerWall.DepthSortBaselineY, Is.EqualTo(0f).Within(.0001f));
                Assert.That(adjacentHigherWall.DepthSortBaselineY, Is.EqualTo(1f).Within(.0001f));
                Assert.That(farHigherWall.DepthSortBaselineY, Is.EqualTo(31f).Within(.0001f));

                var lowerGroup = lowerWall.GetComponent<SortingGroup>();
                var adjacentHigherGroup = adjacentHigherWall.GetComponent<SortingGroup>();
                var farHigherGroup = farHigherWall.GetComponent<SortingGroup>();
                Assert.That(lowerGroup, Is.Not.Null);
                Assert.That(adjacentHigherGroup, Is.Not.Null);
                Assert.That(farHigherGroup, Is.Not.Null);
                Assert.That(lowerGroup.sortingOrder, Is.LessThan(100));
                Assert.That(adjacentHigherGroup.sortingOrder, Is.LessThan(100));
                Assert.That(farHigherGroup.sortingOrder, Is.LessThan(100));
                Assert.That(lowerGroup.sortingOrder, Is.GreaterThan(adjacentHigherGroup.sortingOrder));
                Assert.That(adjacentHigherGroup.sortingOrder, Is.GreaterThan(farHigherGroup.sortingOrder));
                var floorRenderer = tilemapObject.GetComponent<TilemapRenderer>();
                Assert.That(floorRenderer.sortingOrder,
                    Is.LessThan(farHigherGroup.sortingOrder));

                // Large maps move the map-local bands below the HUD. Ordinary
                // DungeonObjectView renderers must remain between the floor
                // and every actor lane even when the band's base is negative.
                objectObject = new GameObject("Dungeon object", typeof(SpriteRenderer));
                objectObject.transform.SetParent(root.transform, true);
                var objectRenderer = objectObject.GetComponent<SpriteRenderer>();
                objectRenderer.sortingOrder = 5;
                var objectView = objectObject.AddComponent<DungeonObstacleView>();
                objectView.BindElevationView(view);
                InvokePrivate(objectView, "LateUpdate");
                var objectGroup = objectObject.GetComponent<SortingGroup>();
                Assert.That(objectGroup, Is.Not.Null);
                Assert.That(objectGroup.sortingOrder,
                    Is.EqualTo(view.GetDungeonObjectDepthSortingOrder()));
                Assert.That(floorRenderer.sortingOrder, Is.LessThan(objectGroup.sortingOrder));

                actorObject = new GameObject("Actor depth slot", typeof(SpriteRenderer), typeof(SortingGroup));
                actorObject.transform.SetParent(root.transform, true);
                var actorGroup = actorObject.GetComponent<SortingGroup>();
                actorGroup.sortingOrder = view.GetActorDepthSortingOrder(0f);
                hudObject = new GameObject("HUD depth baseline", typeof(SpriteRenderer));
                var hudRenderer = hudObject.GetComponent<SpriteRenderer>();
                hudRenderer.sortingOrder = 100;
                Assert.That(objectGroup.sortingOrder, Is.LessThan(actorGroup.sortingOrder));
                Assert.That(actorGroup.sortingOrder, Is.LessThan(hudRenderer.sortingOrder));
                Assert.That(objectRenderer.sortingOrder,
                    Is.EqualTo(5),
                    "The object SortingGroup must preserve child body/outline renderer orders.");
                Assert.That(lowerWall.transform.parent.GetComponent<SortingGroup>(), Is.Null);
                Assert.That(adjacentHigherWall.transform.parent.GetComponent<SortingGroup>(), Is.Null);
                Assert.That(farHigherWall.transform.parent.GetComponent<SortingGroup>(), Is.Null);
                Assert.That(lowerWall.GetComponent<SpriteRenderer>().sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.VerticalWallSortingOrder));
                Assert.That(adjacentHigherWall.GetComponent<SpriteRenderer>().sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.VerticalWallSortingOrder));
                Assert.That(farHigherWall.GetComponent<SpriteRenderer>().sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.VerticalWallSortingOrder));
            }
            finally
            {
                if (hudObject != null) Object.DestroyImmediate(hudObject);
                if (actorObject != null) Object.DestroyImmediate(actorObject);
                if (objectObject != null) Object.DestroyImmediate(objectObject);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void MapViewUsesSideForTightWalkableGap()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var root = new GameObject("Dungeon tight gap view");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "lower-room");
                map.TryAddFloorCell(Vector2Int.up, "upper-room");
                map.SetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.up),
                    Godmist.Core.Enums.DungeonWallType.Full);

                view.Rebuild(map);

                var walls = root.GetComponentsInChildren<DungeonWallSegmentView>();
                Assert.That(walls, Has.Length.EqualTo(1));
                var renderer = walls[0].GetComponent<SpriteRenderer>();
                Assert.That(walls[0].SurfaceProfile, Is.EqualTo(DungeonWallSurfaceProfile.Side));
                Assert.That(renderer.sprite, Is.SameAs(sideSprite));
                Assert.That(renderer.size, Is.EqualTo(new Vector2(.25f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void ProductionAssetsUse32PixelsPerWorldUnitAndThreeWallSprites()
        {
            var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<Tile>(
                "Assets/_Project/Art/Tiles/T_DungeonFloor_0x72.asset");
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var simpleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSimple32.png");
            var topSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallTop32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");

            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.sprite, Is.Not.Null);
            Assert.That(tile.sprite.pixelsPerUnit, Is.EqualTo(32f).Within(0.0001f));
            Assert.That(tile.sprite.rect.width, Is.EqualTo(32f).Within(0.0001f));
            Assert.That(wallPrefab, Is.Not.Null);
            Assert.That(northSprite, Is.Not.Null);
            Assert.That(southSprite, Is.Not.Null);
            Assert.That(simpleSprite, Is.Not.Null);
            AssertNativeWallSprite(sideSprite, 8f, 32f);
            AssertNativeWallSprite(northSprite, 32f, 32f);
            AssertNativeWallSprite(southSprite, 32f, 8f);

            var wall = wallPrefab.GetComponent<DungeonWallSegmentView>();
            var renderer = wallPrefab.GetComponent<SpriteRenderer>();
            var movementCollider = wallPrefab.GetComponent<BoxCollider2D>();
            Assert.That(wall.Thickness, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(wall.UsesNativeWallCrossSection, Is.True);
            Assert.That(wall.VerticalWallSprite, Is.Not.Null);
            Assert.That(wall.HorizontalWallSprite, Is.Not.Null);
            Assert.That(wall.VerticalWallSprite, Is.Not.SameAs(wall.HorizontalWallSprite));
            // The serialized prefab mapping follows the runtime-facing orientation
            // confirmed in the dungeon view: the zero-right edge selects the
            // vertical field and zero-up selects the horizontal field. The fields
            // intentionally carry the swapped assets after the visual QA correction.
            Assert.That(wall.VerticalWallSprite, Is.SameAs(topSprite));
            Assert.That(wall.HorizontalWallSprite, Is.SameAs(simpleSprite));
            Assert.That(wall.SideWallSprite, Is.SameAs(sideSprite));
            Assert.That(wall.NorthWallSprite, Is.SameAs(northSprite));
            Assert.That(wall.SouthWallSprite, Is.SameAs(southSprite));
            Assert.That(wall.VerticalWallSprite.rect.width, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(wall.HorizontalWallSprite.rect.width, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(32f).Within(0.0001f));
            Assert.That(wallPrefab.GetComponentsInChildren<SpriteRenderer>(true), Has.Length.EqualTo(1));
            Assert.That(DungeonWallSegmentView.VerticalWallSortingOrder, Is.EqualTo(2));
            Assert.That(DungeonWallSegmentView.HorizontalWallSortingOrder, Is.EqualTo(3));

            var gridObject = new GameObject("Grid", typeof(Grid));
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var wallInstance = Object.Instantiate(wallPrefab);
            try
            {
                var instanceView = wallInstance.GetComponent<DungeonWallSegmentView>();
                var instanceRenderer = wallInstance.GetComponent<SpriteRenderer>();
                var instanceCollider = wallInstance.GetComponent<BoxCollider2D>();
                var tilemap = tilemapObject.GetComponent<Tilemap>();
                Assert.That(movementCollider, Is.Not.Null);
                Assert.That(instanceCollider, Is.Not.Null);
                instanceView.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), tilemap, true, false, false);
                Physics2D.SyncTransforms();
                Assert.That(instanceRenderer.sprite, Is.SameAs(instanceView.VerticalWallSprite));
                Assert.That(instanceRenderer.sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.VerticalWallSortingOrder));
                Assert.That(instanceRenderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
                Assert.That(instanceRenderer.size.x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(instanceRenderer.size.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wallInstance.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instanceCollider.bounds.size.x, Is.EqualTo(.25f).Within(.0001f));
                Assert.That(instanceCollider.bounds.size.y, Is.EqualTo(1f).Within(.0001f));

                instanceView.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), tilemap, true, false, true);
                Physics2D.SyncTransforms();
                Assert.That(instanceView.VisualLength, Is.EqualTo(1.125f).Within(0.0001f));
                Assert.That(instanceRenderer.size.y, Is.EqualTo(1.125f).Within(0.0001f));
                Assert.That(instanceRenderer.sprite.rect.height, Is.EqualTo(32f).Within(0.0001f));
                Assert.That(wallInstance.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instanceCollider.bounds.size.x, Is.EqualTo(.25f).Within(.0001f));
                Assert.That(instanceCollider.bounds.size.y, Is.EqualTo(1f).Within(.0001f));
                Assert.That(instanceCollider.bounds.center.y, Is.EqualTo(.5f).Within(.0001f));

                instanceView.Bind(new DungeonCellEdge(Vector2Int.zero, Vector2Int.up), tilemap, true, false, false);
                Physics2D.SyncTransforms();
                Assert.That(instanceRenderer.sprite, Is.SameAs(instanceView.HorizontalWallSprite));
                Assert.That(instanceRenderer.sortingOrder,
                    Is.EqualTo(DungeonWallSegmentView.HorizontalWallSortingOrder));
                Assert.That(instanceCollider.bounds.size.x, Is.EqualTo(1f).Within(.0001f));
                Assert.That(instanceCollider.bounds.size.y, Is.EqualTo(.25f).Within(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(wallInstance);
                Object.DestroyImmediate(tilemapObject);
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void GroundedMovementColliderKeepsFeetClearanceAtSouthWall()
        {
            var root = new GameObject("Dungeon feet clearance");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", wallPrefab.GetComponent<DungeonWallSegmentView>());

                var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room");
                map.SetWallType(
                    new DungeonCellEdge(Vector2Int.zero, Vector2Int.down),
                    Godmist.Core.Enums.DungeonWallType.Full);
                view.Rebuild(map);

                var actorObject = new GameObject("Actor");
                actorObject.transform.SetParent(root.transform);
                actorObject.transform.localScale = Vector3.one * .7f;
                var circle = actorObject.AddComponent<CircleCollider2D>();
                circle.radius = .28f;
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                SetField(actor, "movementCollider", circle);
                actor.ConfigureTerrain(map, view);

                var start = view.CellToWorldCenter(Vector2Int.zero);
                actor.SetPosition(start);
                Physics2D.SyncTransforms();

                Assert.That(circle.offset.y, Is.EqualTo(circle.radius).Within(.0001f));
                Assert.That(actor.MovementColliderCenterOffset.y,
                    Is.EqualTo(actor.MovementRadius).Within(.0001f));
                Assert.That(actor.HasClearMovementSegment(
                    start,
                    start + Vector2.down * .36f), Is.True,
                    "The grounded feet may approach the wall until its top edge.");
                Assert.That(actor.HasClearMovementSegment(
                    start,
                    start + Vector2.down * .41f), Is.False,
                    "The circle must still stop before crossing the full wall collider.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void WallPrefabVariantsKeepTheirVisualAndColliderProfiles()
        {
            var lowWallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonLowWall.prefab");
            var railingPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonRailing.prefab");

            Assert.That(lowWallPrefab, Is.Not.Null);
            Assert.That(railingPrefab, Is.Not.Null);

            var lowWall = lowWallPrefab.GetComponent<DungeonWallSegmentView>();
            var railing = railingPrefab.GetComponent<DungeonWallSegmentView>();
            Assert.That(lowWall.Thickness, Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(lowWall.UsesNativeWallCrossSection, Is.False);
            Assert.That(railing.Thickness, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(railing.UsesNativeWallCrossSection, Is.False);

            var gridObject = new GameObject("Grid", typeof(Grid));
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var lowWallInstance = Object.Instantiate(lowWallPrefab);
            var railingInstance = Object.Instantiate(railingPrefab);
            try
            {
                var tilemap = tilemapObject.GetComponent<Tilemap>();
                var lowInstanceView = lowWallInstance.GetComponent<DungeonWallSegmentView>();
                var lowRenderer = lowWallInstance.GetComponent<SpriteRenderer>();
                var lowCollider = lowWallInstance.GetComponent<BoxCollider2D>();
                lowInstanceView.Bind(
                    new DungeonCellEdge(Vector2Int.zero, Vector2Int.right),
                    tilemap,
                    true);
                Physics2D.SyncTransforms();
                Assert.That(lowRenderer.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
                Assert.That(lowWallInstance.transform.localScale.x, Is.EqualTo(0.16f).Within(0.0001f));
                Assert.That(lowCollider.bounds.size.x, Is.EqualTo(0.16f).Within(0.0001f));

                var railingInstanceView = railingInstance.GetComponent<DungeonWallSegmentView>();
                var railingRenderer = railingInstance.GetComponent<SpriteRenderer>();
                var railingCollider = railingInstance.GetComponent<BoxCollider2D>();
                railingInstanceView.Bind(
                    new DungeonCellEdge(Vector2Int.zero, Vector2Int.right),
                    tilemap,
                    true);
                Physics2D.SyncTransforms();
                Assert.That(railingInstanceView.VisualLength, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(railingRenderer.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
                Assert.That(railingInstance.transform.localScale.x, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(railingCollider.bounds.size.x, Is.EqualTo(0.12f).Within(0.0001f));

                var postRenderers = railingInstance.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(postRenderers, Has.Length.EqualTo(4));
                Assert.That(railingRenderer.sprite, Is.SameAs(postRenderers[1].sprite));
                foreach (var post in railingInstance.GetComponentsInChildren<Transform>(true))
                {
                    if (post.name.StartsWith("Post"))
                        Assert.That(post.localScale.x, Is.EqualTo(2.1f).Within(0.0001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(railingInstance);
                Object.DestroyImmediate(lowWallInstance);
                Object.DestroyImmediate(tilemapObject);
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void ActorWorldDepthRenderPlacesBodyBehindAndInFrontOfSouthWall()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southTexture = CreateSolidTexture(32, 8, new Color(.05f, .2f, .95f, 1f));
            var southSprite = Sprite.Create(
                southTexture,
                new Rect(0f, 0f, 32f, 8f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var actorTexture = CreateSolidTexture(32, 128, new Color(.95f, .08f, .04f, 1f));
            var actorSprite = Sprite.Create(
                actorTexture,
                new Rect(0f, 0f, 32f, 128f),
                new Vector2(.5f, 0f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var root = new GameObject("Dungeon world depth render");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Depth camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            ExplorationCharacterView backActor = null;
            ExplorationCharacterView frontActor = null;

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(1, 0), "front");
                map.TryAddFloorCell(new Vector2Int(1, 2), "back");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 0), new Vector2Int(1, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)), full);
                view.Rebuild(map);

                camera.transform.position = new Vector3(1.5f, 2.25f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 2.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;

                backActor = CreateRenderActor(root.transform, actorSprite, map, view, new Vector2(1.5f, 2.5f));
                InvokePrivate(backActor, "RefreshWorldDepthSorting");
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var behindPixel = ReadRenderPixel(camera, renderTexture, new Vector3(1.5f, 2.625f, 0f));
                Assert.That(behindPixel.b, Is.GreaterThan(.65f),
                    "The South face must draw over the body of an actor whose grounded feet are behind it.");
                Assert.That(behindPixel.r, Is.LessThan(.35f));

                Object.DestroyImmediate(backActor.gameObject);
                backActor = null;
                frontActor = CreateRenderActor(root.transform, actorSprite, map, view, new Vector2(1.5f, .5f));
                InvokePrivate(frontActor, "RefreshWorldDepthSorting");
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var frontPixel = ReadRenderPixel(camera, renderTexture, new Vector3(1.5f, 2.625f, 0f));
                Assert.That(frontPixel.r, Is.GreaterThan(.65f),
                    "The same wall must remain behind a front-side actor when its feet are in front of the baseline.");
                Assert.That(frontPixel.b, Is.LessThan(.35f));
            }
            finally
            {
                if (frontActor != null) Object.DestroyImmediate(frontActor.gameObject);
                if (backActor != null) Object.DestroyImmediate(backActor.gameObject);
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(actorSprite);
                Object.DestroyImmediate(actorTexture);
                Object.DestroyImmediate(southSprite);
                Object.DestroyImmediate(southTexture);
            }
        }

        [Test]
        public void ActorWorldDepthRenderOccludesMirroredCornerFootWeaponKeepsHeadAndFrontVisible()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var sideSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSide32.png");
            var northSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallNorth32.png");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var bodyTexture = CreateSolidTexture(64, 96, new Color(.95f, .08f, .04f, 1f));
            var footTexture = CreateSolidTexture(32, 16, new Color(.98f, .78f, .08f, 1f));
            var weaponTexture = CreateSolidTexture(16, 16, new Color(.1f, .85f, .98f, 1f));
            var frontBodyTexture = CreateSolidTexture(64, 96, new Color(.18f, .95f, .22f, 1f));
            var root = new GameObject("Dungeon mirrored corner render");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Corner camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            ExplorationCharacterView leftActor = null;
            ExplorationCharacterView rightActor = null;
            ExplorationCharacterView frontActor = null;
            Sprite[] leftSprites = null;
            Sprite[] rightSprites = null;
            Sprite[] frontSprites = null;

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "northWallSprite", northSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "front");
                map.TryAddFloorCell(new Vector2Int(0, 1), "left-side");
                map.TryAddFloorCell(new Vector2Int(1, 2), "left-south");
                map.TryAddFloorCell(new Vector2Int(2, 2), "right-south");
                map.TryAddFloorCell(new Vector2Int(3, 1), "right-side");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 1), new Vector2Int(1, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 1), new Vector2Int(3, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 1), new Vector2Int(2, 2)), full);
                view.Rebuild(map);

                camera.transform.position = new Vector3(2f, 2.5f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 4.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;

                leftActor = CreateDetailedRenderActor(
                    root.transform,
                    map,
                    view,
                    new Vector2(.65f, 1.5f),
                    bodyTexture,
                    footTexture,
                    weaponTexture,
                    new Color(.95f, .08f, .04f, 1f),
                    out leftSprites);
                InvokePrivate(leftActor, "RefreshWorldDepthSorting");
                Assert.That(leftActor.GetComponent<CombatUnitVisualStateView>(), Is.Not.Null);
                var leftOutlineEnabled = false;
                foreach (var renderer in leftActor.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.name == "Visual State Outline" && renderer.enabled)
                        leftOutlineEnabled = true;
                Assert.That(leftOutlineEnabled, Is.True);
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var leftFootPixel = ReadRenderPixel(camera, renderTexture, new Vector3(1.4f, 1.55f, 0f));
                var leftWeaponPixel = ReadRenderPixel(camera, renderTexture, new Vector3(1.4f, 1.85f, 0f));
                var leftHeadPixel = ReadRenderPixel(camera, renderTexture, new Vector3(.65f, 4.1f, 0f));
                Assert.That(leftFootPixel.maxColorComponent, Is.LessThan(.15f),
                    "The mirrored left corner must occlude the actor foot in the adjacent void cell.");
                Assert.That(leftWeaponPixel.maxColorComponent, Is.LessThan(.15f),
                    "The mirrored left corner must occlude the weapon, not only the body center.");
                Assert.That(leftHeadPixel.r, Is.GreaterThan(.45f),
                    "The corner occluder must leave the actor head visible.");

                Object.DestroyImmediate(leftActor.gameObject);
                leftActor = null;
                rightActor = CreateDetailedRenderActor(
                    root.transform,
                    map,
                    view,
                    new Vector2(3.35f, 1.5f),
                    bodyTexture,
                    footTexture,
                    weaponTexture,
                    new Color(.95f, .08f, .04f, 1f),
                    out rightSprites);
                InvokePrivate(rightActor, "RefreshWorldDepthSorting");
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var rightFootPixel = ReadRenderPixel(camera, renderTexture, new Vector3(2.6f, 1.55f, 0f));
                var rightWeaponPixel = ReadRenderPixel(camera, renderTexture, new Vector3(2.6f, 1.85f, 0f));
                var rightHeadPixel = ReadRenderPixel(camera, renderTexture, new Vector3(3.35f, 4.1f, 0f));
                Assert.That(rightFootPixel.maxColorComponent, Is.LessThan(.15f),
                    "The mirrored right corner must occlude the actor foot in the adjacent void cell.");
                Assert.That(rightWeaponPixel.maxColorComponent, Is.LessThan(.15f),
                    "The mirrored right corner must occlude the weapon, not only the body center.");
                Assert.That(rightHeadPixel.r, Is.GreaterThan(.45f),
                    "The mirrored right corner must leave the actor head visible.");

                Object.DestroyImmediate(rightActor.gameObject);
                rightActor = null;
                frontActor = CreateDetailedRenderActor(
                    root.transform,
                    map,
                    view,
                    new Vector2(.5f, .5f),
                    frontBodyTexture,
                    footTexture,
                    weaponTexture,
                    new Color(.18f, .95f, .22f, 1f),
                    out frontSprites);
                InvokePrivate(frontActor, "RefreshWorldDepthSorting");
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                camera.Render();
                var frontPixel = ReadRenderPixel(camera, renderTexture, new Vector3(1.4f, 1.55f, 0f));
                Assert.That(frontPixel.g, Is.GreaterThan(.45f),
                    "An actor on the walkable front side must remain above the corner occluder.");
            }
            finally
            {
                if (frontActor != null) Object.DestroyImmediate(frontActor.gameObject);
                if (rightActor != null) Object.DestroyImmediate(rightActor.gameObject);
                if (leftActor != null) Object.DestroyImmediate(leftActor.gameObject);
                DestroySprites(leftSprites);
                DestroySprites(rightSprites);
                DestroySprites(frontSprites);
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(bodyTexture);
                Object.DestroyImmediate(footTexture);
                Object.DestroyImmediate(weaponTexture);
                Object.DestroyImmediate(frontBodyTexture);
            }
        }

        [Test]
        public void SideVoidOcclusionSubtractsProtectedWallInMirroredRenderFrames()
        {
            var wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab");
            var southSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Tiles/DungeonWallSouth32.png");
            var sideTexture = CreateSolidTexture(8, 32, Color.cyan);
            var sideSprite = Sprite.Create(
                sideTexture,
                new Rect(0f, 0f, 8f, 32f),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            var bodyTexture = CreateSolidTexture(64, 96, new Color(.95f, .08f, .04f, 1f));
            var footTexture = CreateSolidTexture(32, 16, new Color(.98f, .78f, .08f, 1f));
            var weaponTexture = CreateSolidTexture(16, 16, new Color(.1f, .85f, .98f, 1f));
            var frontBodyTexture = CreateSolidTexture(64, 96, new Color(.18f, .95f, .22f, 1f));
            var root = new GameObject("Dungeon protected side wall render");
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root.transform);
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(gridObject.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var template = Object.Instantiate(wallPrefab);
            var renderTexture = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Protected side camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();

            try
            {
                var templateView = template.GetComponent<DungeonWallSegmentView>();
                SetField(templateView, "sideWallSprite", sideSprite);
                SetField(templateView, "southWallSprite", southSprite);

                var view = root.AddComponent<DungeonFloorMapView>();
                SetField(view, "floorTilemap", tilemapObject.GetComponent<Tilemap>());
                SetField(view, "floorTile", tile);
                SetField(view, "wallSegmentPrefab", templateView);

                var map = new DungeonFloorMap();
                map.TryAddFloorCell(new Vector2Int(0, 0), "front");
                map.TryAddFloorCell(new Vector2Int(0, 1), "left-side");
                map.TryAddFloorCell(new Vector2Int(1, 2), "left-south");
                map.TryAddFloorCell(new Vector2Int(2, 2), "right-south");
                map.TryAddFloorCell(new Vector2Int(3, 1), "right-side");
                var full = Godmist.Core.Enums.DungeonWallType.Full;
                map.SetWallType(new DungeonCellEdge(new Vector2Int(0, 1), new Vector2Int(1, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 1), new Vector2Int(3, 1)), full);
                map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 1), new Vector2Int(2, 2)), full);
                view.Rebuild(map);

                DungeonWallSegmentView leftSide = null;
                DungeonWallSegmentView rightSide = null;
                foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
                {
                    if (wall.SurfaceProfile != DungeonWallSurfaceProfile.Side)
                        continue;

                    if (wall.transform.position.x < 2f)
                        leftSide = wall;
                    else
                        rightSide = wall;
                }

                Assert.That(leftSide, Is.Not.Null);
                Assert.That(rightSide, Is.Not.Null);

                camera.transform.position = new Vector3(2f, 2.5f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 4.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = renderTexture;
                camera.enabled = false;

                for (var mirror = 0; mirror < 2; mirror++)
                {
                    var protectedX = mirror == 0 ? 1.875f : 2.125f;
                    var actorX = mirror == 0 ? .65f : 3.35f;
                    var protectedSampleX = mirror == 0 ? 1.9f : 2.1f;
                    var voidSampleX = mirror == 0 ? 1.4f : 2.6f;

                    var ownerSide = mirror == 0 ? leftSide : rightSide;
                    var protectedSide = mirror == 0 ? rightSide : leftSide;

                    Assert.That(ownerSide, Is.Not.Null);
                    Assert.That(protectedSide, Is.Not.Null);
                    protectedSide.transform.position = new Vector3(
                        protectedX,
                        protectedSide.transform.position.y,
                        protectedSide.transform.position.z);
                    protectedSide.GetComponent<SpriteRenderer>().color = Color.cyan;

                    var ownerGroup = ownerSide.GetComponent<SortingGroup>();
                    var protectedGroup = protectedSide.GetComponent<SortingGroup>();
                    Assert.That(ownerGroup, Is.Not.Null);
                    Assert.That(protectedGroup, Is.Not.Null);
                    protectedGroup.sortingOrder = ownerGroup.sortingOrder - 1;
                    Assert.That(protectedGroup.sortingOrder,
                        Is.LessThan(ownerGroup.sortingOrder));

                    Assert.That(ownerSide.TryGetVisualBounds(out var ownerBounds), Is.True);
                    var voidCell = new Vector2Int(mirror == 0 ? 1 : 2, 1);
                    var voidMin = tilemapObject.GetComponent<Tilemap>().CellToWorld(
                        new Vector3Int(voidCell.x, voidCell.y, 0));
                    var voidMax = tilemapObject.GetComponent<Tilemap>().CellToWorld(
                        new Vector3Int(voidCell.x + 1, voidCell.y + 1, 0));
                    var rawCandidate = new Bounds(
                        new Vector3(
                            (Mathf.Min(ownerBounds.min.x, voidMin.x)
                                + Mathf.Max(ownerBounds.max.x, voidMax.x)) * .5f,
                            (Mathf.Max(ownerBounds.min.y, voidMin.y)
                                + Mathf.Min(ownerBounds.max.y, voidMax.y)) * .5f,
                            ownerBounds.center.z),
                        new Vector3(
                            Mathf.Max(ownerBounds.max.x, voidMax.x)
                                - Mathf.Min(ownerBounds.min.x, voidMin.x),
                            Mathf.Min(ownerBounds.max.y, voidMax.y)
                                - Mathf.Max(ownerBounds.min.y, voidMin.y),
                            ownerBounds.size.z));
                    var protectedBounds = protectedSide.GetComponent<SpriteRenderer>().bounds;
                    var candidateOverlapX = Mathf.Min(rawCandidate.max.x, protectedBounds.max.x)
                        - Mathf.Max(rawCandidate.min.x, protectedBounds.min.x);
                    var candidateOverlapY = Mathf.Min(rawCandidate.max.y, protectedBounds.max.y)
                        - Mathf.Max(rawCandidate.min.y, protectedBounds.min.y);
                    Assert.That(candidateOverlapX, Is.GreaterThan(.001f));
                    Assert.That(candidateOverlapY, Is.GreaterThan(.001f));

                    InvokePrivate(view, "BuildSideVoidOccluders");
                    InvokePrivate(view, "RefreshWallRendererCache");
                    foreach (var occluder in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (occluder.name != "SideVoidOcclusion" || !occluder.gameObject.activeSelf)
                            continue;

                        var overlapX = Mathf.Min(occluder.bounds.max.x, protectedBounds.max.x)
                            - Mathf.Max(occluder.bounds.min.x, protectedBounds.min.x);
                        var overlapY = Mathf.Min(occluder.bounds.max.y, protectedBounds.max.y)
                            - Mathf.Max(occluder.bounds.min.y, protectedBounds.min.y);
                        Assert.That(overlapX <= .001f || overlapY <= .001f, Is.True,
                            "SideVoidOcclusion must leave the protected wall geometry visible.");
                    }

                    var actor = CreateDetailedRenderActor(
                        root.transform,
                        map,
                        view,
                        new Vector2(actorX, 1.5f),
                        bodyTexture,
                        footTexture,
                        weaponTexture,
                        new Color(.95f, .08f, .04f, 1f),
                        out var actorSprites);
                    InvokePrivate(actor, "RefreshWorldDepthSorting");
                    UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                    camera.transform.position = new Vector3(mirror == 0 ? 2f : 1f, 2.5f, -10f);
                    camera.Render();

                    var protectedPixel = ReadRenderPixel(
                        camera,
                        renderTexture,
                        new Vector3(protectedSampleX, 1.5f, 0f));
                    var footPixel = ReadRenderPixel(
                        camera,
                        renderTexture,
                        new Vector3(voidSampleX, 1.55f, 0f));
                    var weaponPixel = ReadRenderPixel(
                        camera,
                        renderTexture,
                        new Vector3(voidSampleX, 1.85f, 0f));
                    var headPixel = ReadRenderPixel(
                        camera,
                        renderTexture,
                        new Vector3(actorX, 4.1f, 0f));
                    Assert.That(protectedPixel.b, Is.GreaterThan(.45f));
                    Assert.That(footPixel.maxColorComponent, Is.LessThan(.15f));
                    Assert.That(weaponPixel.maxColorComponent, Is.LessThan(.15f));
                    Assert.That(headPixel.r, Is.GreaterThan(.45f));

                    Object.DestroyImmediate(actor.gameObject);
                    DestroySprites(actorSprites);

                    var frontActor = CreateDetailedRenderActor(
                        root.transform,
                        map,
                        view,
                        new Vector2(mirror == 0 ? .5f : 3.5f, .5f),
                        frontBodyTexture,
                        footTexture,
                        weaponTexture,
                        new Color(.18f, .95f, .22f, 1f),
                        out var frontSprites);
                    InvokePrivate(frontActor, "RefreshWorldDepthSorting");
                    UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                    camera.Render();
                    var frontPixel = ReadRenderPixel(
                        camera,
                        renderTexture,
                        new Vector3(voidSampleX, 1.55f, 0f));
                    Assert.That(frontPixel.g, Is.GreaterThan(.45f));
                    Object.DestroyImmediate(frontActor.gameObject);
                    DestroySprites(frontSprites);
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(sideSprite);
                Object.DestroyImmediate(sideTexture);
                Object.DestroyImmediate(bodyTexture);
                Object.DestroyImmediate(footTexture);
                Object.DestroyImmediate(weaponTexture);
                Object.DestroyImmediate(frontBodyTexture);
            }
        }

        private static ExplorationCharacterView CreateRenderActor(
            Transform parent,
            Sprite sprite,
            DungeonFloorMap map,
            DungeonFloorMapView view,
            Vector2 position)
        {
            var actorObject = new GameObject("Render actor", typeof(SpriteRenderer));
            actorObject.transform.SetParent(parent, true);
            actorObject.transform.position = position;
            var renderer = actorObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            var actor = actorObject.AddComponent<ExplorationCharacterView>();
            SetField(actor, "bodyRenderer", renderer);
            actor.ConfigureTerrain(map, view);
            return actor;
        }

        private static ExplorationCharacterView CreateDetailedRenderActor(
            Transform parent,
            DungeonFloorMap map,
            DungeonFloorMapView view,
            Vector2 position,
            Texture2D bodyTexture,
            Texture2D footTexture,
            Texture2D weaponTexture,
            Color bodyColor,
            out Sprite[] sprites)
        {
            var actorObject = new GameObject("Detailed render actor");
            actorObject.transform.SetParent(parent, true);
            actorObject.transform.position = position;
            var bodyRenderer = actorObject.AddComponent<SpriteRenderer>();
            var bodySprite = Sprite.Create(
                bodyTexture,
                new Rect(0f, 0f, bodyTexture.width, bodyTexture.height),
                new Vector2(.5f, 0f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            bodyRenderer.sprite = bodySprite;
            bodyRenderer.color = bodyColor;
            bodyRenderer.sortingOrder = 10;

            var footObject = new GameObject("Foot renderer", typeof(SpriteRenderer));
            footObject.transform.SetParent(actorObject.transform, false);
            footObject.transform.localPosition = new Vector3(.8f, .08f, 0f);
            var footRenderer = footObject.GetComponent<SpriteRenderer>();
            var footSprite = Sprite.Create(
                footTexture,
                new Rect(0f, 0f, footTexture.width, footTexture.height),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            footRenderer.sprite = footSprite;
            footRenderer.sortingOrder = 11;

            var weaponObject = new GameObject("Weapon renderer", typeof(SpriteRenderer));
            weaponObject.transform.SetParent(actorObject.transform, false);
            weaponObject.transform.localPosition = new Vector3(.8f, .36f, 0f);
            var weaponRenderer = weaponObject.GetComponent<SpriteRenderer>();
            var weaponSprite = Sprite.Create(
                weaponTexture,
                new Rect(0f, 0f, weaponTexture.width, weaponTexture.height),
                new Vector2(.5f, .5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            weaponRenderer.sprite = weaponSprite;
            weaponRenderer.sortingOrder = 12;

            var actor = actorObject.AddComponent<ExplorationCharacterView>();
            SetField(actor, "bodyRenderer", bodyRenderer);
            var visualState = actorObject.AddComponent<CombatUnitVisualStateView>();
            // This synthetic actor has no living BattleUnit, hence no active
            // turn. Hover explicitly enables the outline tested below.
            visualState.Refresh(null, false, true, false);
            actor.ConfigureTerrain(map, view);
            sprites = new[] { bodySprite, footSprite, weaponSprite };
            return actor;
        }

        private static void DestroySprites(Sprite[] sprites)
        {
            if (sprites == null) return;
            foreach (var sprite in sprites)
                if (sprite != null) Object.DestroyImmediate(sprite);
        }

        private static Texture2D CreateSolidTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (var index = 0; index < pixels.Length; index++) pixels[index] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        private static Texture2D CreateNorthJoinTexture(Color faceColor, Color capColor)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[32 * 32];
            for (var y = 0; y < 32; y++)
            {
                var color = y >= 24 ? capColor : faceColor;
                for (var x = 0; x < 32; x++)
                    pixels[y * 32 + x] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        private static void AssertDedicatedCornerSprite(Sprite sprite)
        {
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture, Is.Not.Null);
            Assert.That(sprite.texture.width, Is.EqualTo(8));
            Assert.That(sprite.texture.height, Is.EqualTo(8));
            Assert.That(sprite.rect.width, Is.EqualTo(8f).Within(.001f));
            Assert.That(sprite.rect.height, Is.EqualTo(8f).Within(.001f));
            var vertices = sprite.vertices;
            var triangles = sprite.triangles;
            var uvs = sprite.uv;
            Assert.That(vertices.Length, Is.EqualTo(4));
            Assert.That(triangles.Length, Is.EqualTo(6));
            Assert.That(uvs.Length, Is.EqualTo(vertices.Length));
            var pixels = sprite.texture.GetPixels32();
            var interior = pixels[3 * 8 + 3];
            if (sprite.name.IndexOf("NorthCap") < 0)
                for (var y = 2; y <= 5; y++)
                    for (var x = 2; x <= 5; x++)
                        Assert.That(pixels[y * 8 + x], Is.EqualTo(interior),
                            "Dedicated South/face corner interiors must stay a single native face colour; border art belongs on the L path.");
            for (var first = 0; first < vertices.Length; first++)
                for (var second = first + 1; second < vertices.Length; second++)
                {
                    if (vertices[first].x < vertices[second].x - .0001f)
                        Assert.That(uvs[first].x, Is.LessThanOrEqualTo(uvs[second].x + .0001f),
                            "Miter U coordinates must follow final mesh X positions.");
                    else if (vertices[first].x > vertices[second].x + .0001f)
                        Assert.That(uvs[first].x, Is.GreaterThanOrEqualTo(uvs[second].x - .0001f),
                            "Miter U coordinates must follow final mesh X positions.");

                    if (vertices[first].y < vertices[second].y - .0001f)
                        Assert.That(uvs[first].y, Is.LessThanOrEqualTo(uvs[second].y + .0001f),
                            "Miter V coordinates must follow final mesh Y positions.");
                    else if (vertices[first].y > vertices[second].y + .0001f)
                        Assert.That(uvs[first].y, Is.GreaterThanOrEqualTo(uvs[second].y - .0001f),
                            "Miter V coordinates must follow final mesh Y positions.");
                }
        }

        private static void AssertNorthCapKeepsNativeArt(Sprite sprite)
        {
            var pixels = sprite.texture.GetPixels32();
            var distinctInteriorColors = new HashSet<Color32>();
            for (var y = 2; y <= 5; y++)
                for (var x = 2; x <= 5; x++)
                    distinctInteriorColors.Add(pixels[y * 8 + x]);

            Assert.That(distinctInteriorColors.Count, Is.GreaterThan(1),
                "NorthCap corner must retain native stone detail inside its L path.");
        }

        private static void AssertNorthCapCornerBorderPaths(
            Sprite sprite,
            bool horizontalArmLeft,
            bool verticalArmDown)
        {
            var pixels = sprite.texture.GetPixels32();
            var interior = pixels[3 * 8 + 3];

            Color32 BodyPixel(int x, int y)
                => pixels[Mathf.Clamp(y, 2, 5) * 8 + Mathf.Clamp(x, 2, 5)];

            // The North cap has two native border tracks. Verify the far end
            // and the stopped end of each horizontal and vertical L arm so a
            // full two-pixel band or a flat rectangle cannot satisfy the
            // corner test.
            foreach (var border in new[] { 1, 6 })
            {
                var horizontalArmX = horizontalArmLeft ? 0 : 7;
                var horizontalOutsideX = horizontalArmLeft ? 7 : 0;
                Assert.That(
                    pixels[border * 8 + horizontalArmX],
                    Is.Not.EqualTo(interior),
                    $"North cap horizontal L path {border} must reach its arm.");
                // NorthCap source V follows world Y; its renderer does not
                // apply flipY, so arm direction affects only the L mask.
                var horizontalBodyY = border;
                Assert.That(
                    pixels[border * 8 + horizontalOutsideX],
                    Is.EqualTo(BodyPixel(horizontalOutsideX, horizontalBodyY)),
                    $"North cap horizontal L path {border} must stop at the turn.");

                var sideBorderX = verticalArmDown ? border : 7 - border;
                var verticalArmY = verticalArmDown ? 0 : 7;
                var verticalOutsideY = verticalArmDown ? 7 : 0;
                Assert.That(
                    pixels[verticalArmY * 8 + sideBorderX],
                    Is.Not.EqualTo(interior),
                    $"North cap vertical L path {border} must reach its arm.");
                Assert.That(
                    pixels[verticalOutsideY * 8 + sideBorderX],
                    Is.EqualTo(BodyPixel(sideBorderX, verticalOutsideY)),
                    $"North cap vertical L path {border} must stop at the turn.");
            }
        }

        private static void AssertSouthFaceCornerBorderPaths(
            Sprite sprite,
            bool horizontalArmLeft,
            bool verticalArmDown)
        {
            var pixels = sprite.texture.GetPixels32();
            var interior = pixels[3 * 8 + 3];

            // Check both L paths at their far arm pixels and check the opposite
            // ends remain face colour. The latter rejects the old full-cross
            // construction, which put an extra vertical seam through the tile.
            foreach (var border in new[] { 1, 6 })
            {
                var horizontalArmX = horizontalArmLeft ? 0 : 7;
                var horizontalOutsideX = horizontalArmLeft ? 7 : 0;
                Assert.That(pixels[border * 8 + horizontalArmX], Is.Not.EqualTo(interior),
                    $"South corner horizontal L path {border} must reach its arm.");
                Assert.That(pixels[border * 8 + horizontalOutsideX], Is.EqualTo(interior),
                    $"South corner horizontal L path {border} must stop at the turn.");

                var sideBorderX = verticalArmDown ? border : 7 - border;
                var verticalArmY = verticalArmDown ? 0 : 7;
                var verticalOutsideY = verticalArmDown ? 7 : 0;
                Assert.That(pixels[verticalArmY * 8 + sideBorderX], Is.Not.EqualTo(interior),
                    $"South corner vertical L path {border} must reach its arm.");
                Assert.That(pixels[verticalOutsideY * 8 + sideBorderX], Is.EqualTo(interior),
                    $"South corner vertical L path {border} must stop at the turn.");
            }
        }

        private static void AssertSouthFaceMatchesNeighborPhases(
            SpriteRenderer cornerRenderer,
            SpriteRenderer southRenderer,
            SpriteRenderer sideRenderer,
            bool horizontalArmLeft,
            bool verticalArmDown)
        {
            var cornerPixels = cornerRenderer.sprite.texture.GetPixels32();
            // Native wall PNGs intentionally keep Read/Write disabled. Read
            // their source pixels through the same GPU path as the runtime
            // corner composer instead of changing asset import settings.
            var southPixels = ReadTexturePixelsForTest(southRenderer.sprite.texture);
            var sidePixels = ReadTexturePixelsForTest(sideRenderer.sprite.texture);
            var cornerBounds = cornerRenderer.bounds;
            // The South art is offset upward. At a short side segment its
            // visual centre can cross the corner, so arm direction must come
            // from the authored topology, not the shifted renderer centres.
            var southTextureRect = southRenderer.sprite.textureRect;
            var sideTextureRect = sideRenderer.sprite.textureRect;
            var ppu = cornerRenderer.sprite.pixelsPerUnit;
            var horizontalPhase = Mathf.FloorToInt(
                (cornerBounds.min.x - southRenderer.bounds.min.x) * ppu);
            var verticalPhase = Mathf.FloorToInt(
                (cornerBounds.min.y - sideRenderer.bounds.min.y) * ppu);
            var southWidth = Mathf.Max(1, Mathf.FloorToInt(southTextureRect.width));
            var sideWidth = Mathf.Max(1, Mathf.FloorToInt(sideTextureRect.width));
            var sideHeight = Mathf.Max(1, Mathf.FloorToInt(sideTextureRect.height));
            var southStartX = Mathf.FloorToInt(southTextureRect.x);
            var southStartY = Mathf.FloorToInt(southTextureRect.y);
            var sideStartX = Mathf.FloorToInt(sideTextureRect.x);
            var sideStartY = Mathf.FloorToInt(sideTextureRect.y);

            int Wrap(int value, int length)
            {
                var result = value % length;
                return result < 0 ? result + length : result;
            }

            foreach (var border in new[] { 1, 6 })
            {
                var turnX = horizontalArmLeft == verticalArmDown
                    ? border
                    : 7 - border;
                var horizontalArmX = horizontalArmLeft ? 0 : 7;
                var southX = southStartX + Wrap(horizontalPhase + horizontalArmX, southWidth);
                var southY = southStartY + border;
                var horizontalExpected = southPixels[
                    southY * southRenderer.sprite.texture.width + southX];
                Assert.That(
                    cornerPixels[border * 8 + horizontalArmX],
                    Is.EqualTo(horizontalExpected),
                    $"South corner far horizontal edge phase mismatch at border {border}.");

                var verticalArmY = verticalArmDown ? 0 : 7;
                var sidePhaseX = Mathf.FloorToInt(
                    (cornerBounds.min.x - sideRenderer.bounds.min.x) * ppu);
                // Side has no authored flipX. Its source column is the
                // world-X phase of the actual neighbouring renderer plus the
                // output column, rather than a mirror inferred from the
                // corner label.
                var sideX = Wrap(sidePhaseX + turnX, sideWidth);
                var sideY = sideStartY + Wrap(verticalPhase + verticalArmY, sideHeight);
                var verticalExpected = sidePixels[
                    sideY * sideRenderer.sprite.texture.width + sideStartX + sideX];
                Assert.That(
                    cornerPixels[verticalArmY * 8 + turnX],
                    Is.EqualTo(verticalExpected),
                    $"South corner far vertical edge phase mismatch at border {border}.");
            }
        }

        private static SpriteRenderer FindSideVerticalRenderer(
            GameObject root,
            Vector2 cornerPoint)
        {
            SpriteRenderer best = null;
            var bestDistance = float.MaxValue;
            foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
            {
                if (wall == null
                    || !wall.gameObject.activeInHierarchy
                    || wall.SurfaceProfile != DungeonWallSurfaceProfile.Side)
                    continue;

                var renderer = wall.GetComponent<SpriteRenderer>();
                if (renderer == null
                    || !renderer.enabled
                    || renderer.sprite == null
                    || renderer.bounds.size.y <= renderer.bounds.size.x)
                    continue;

                if (cornerPoint.y < renderer.bounds.min.y - .01f
                    || cornerPoint.y > renderer.bounds.max.y + .01f)
                    continue;

                var distance = Mathf.Abs(cornerPoint.x - renderer.bounds.center.x);
                if (distance < bestDistance)
                {
                    best = renderer;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static SpriteRenderer FindSouthHorizontalRenderer(
            GameObject root,
            Vector2 cornerPoint)
        {
            SpriteRenderer best = null;
            var bestDistance = float.MaxValue;
            foreach (var wall in root.GetComponentsInChildren<DungeonWallSegmentView>(true))
            {
                if (wall == null
                    || !wall.gameObject.activeInHierarchy
                    || wall.SurfaceProfile != DungeonWallSurfaceProfile.South)
                    continue;

                var renderer = wall.GetComponent<SpriteRenderer>();
                if (renderer == null
                    || !renderer.enabled
                    || renderer.sprite == null
                    || renderer.bounds.size.x <= renderer.bounds.size.y)
                    continue;

                if (cornerPoint.x < renderer.bounds.min.x - .01f
                    || cornerPoint.x > renderer.bounds.max.x + .01f)
                    continue;

                var distance = Mathf.Abs(cornerPoint.y - renderer.bounds.center.y);
                if (distance < bestDistance)
                {
                    best = renderer;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static Color ReadRenderPixel(Camera camera, RenderTexture renderTexture, Vector3 worldPoint)
        {
            var screen = camera.WorldToScreenPoint(worldPoint);
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            pixel.ReadPixels(new Rect(screen.x, screen.y, 1f, 1f), 0, 0);
            pixel.Apply();
            var color = pixel.GetPixel(0, 0);
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixel);
            return color;
        }

        private static Color32[] ReadTexturePixelsForTest(Texture source)
        {
            var renderTexture = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var capture = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                false);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                capture.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height),
                    0,
                    0);
                capture.Apply(false, false);
                return capture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                Object.DestroyImmediate(capture);
            }
        }

        private static Color[] CaptureRenderPixels(RenderTexture renderTexture)
        {
            var capture = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RGBA32,
                false);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            capture.ReadPixels(
                new Rect(0f, 0f, renderTexture.width, renderTexture.height),
                0,
                0);
            capture.Apply();
            var pixels = capture.GetPixels();
            RenderTexture.active = previous;
            Object.DestroyImmediate(capture);
            return pixels;
        }

        private static bool IsInsideScreenTriangle(
            Vector3 point,
            Vector3 first,
            Vector3 second,
            Vector3 third)
        {
            var firstSign = (point.x - second.x) * (first.y - second.y)
                            - (first.x - second.x) * (point.y - second.y);
            var secondSign = (point.x - third.x) * (second.y - third.y)
                             - (second.x - third.x) * (point.y - third.y);
            var thirdSign = (point.x - first.x) * (third.y - first.y)
                            - (third.x - first.x) * (point.y - first.y);
            const float epsilon = .01f;
            return (firstSign >= -epsilon && secondSign >= -epsilon && thirdSign >= -epsilon)
                   || (firstSign <= epsilon && secondSign <= epsilon && thirdSign <= epsilon);
        }

        private static float ColorDistance(Color first, Color second)
        {
            var red = first.r - second.r;
            var green = first.g - second.g;
            var blue = first.b - second.b;
            return Mathf.Sqrt(red * red + green * green + blue * blue);
        }

        private static void SaveRenderTextureCapture(RenderTexture renderTexture, string path)
        {
            var capture = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RGBA32,
                false);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            capture.ReadPixels(
                new Rect(0f, 0f, renderTexture.width, renderTexture.height),
                0,
                0);
            capture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Object.DestroyImmediate(capture);
        }

        private static void SetField(Object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(Object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static SpriteRenderer FindRenderer(
            DungeonWallSegmentView wall,
            string objectName)
        {
            foreach (var renderer in wall.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null && renderer.name == objectName)
                    return renderer;
            return null;
        }

        private static List<SpriteRenderer> GetWallProfileRenderers(
            DungeonWallSegmentView wall)
        {
            var renderers = new List<SpriteRenderer>();
            foreach (var renderer in wall.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name != "SideVoidOcclusion"
                    && renderer.name != "NorthSideJoinOverlay"
                    && !renderer.name.StartsWith("CornerMiter"))
                    renderers.Add(renderer);
            return renderers;
        }

        private static void AssertNativeWallSprite(Sprite sprite, float width, float height)
        {
            Assert.That(sprite.rect.width, Is.EqualTo(width).Within(.0001f));
            Assert.That(sprite.rect.height, Is.EqualTo(height).Within(.0001f));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32f).Within(.0001f));
        }
    }
}
