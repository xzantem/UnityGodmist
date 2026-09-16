using System;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class DungeonMinimapTests
    {
        static DungeonTraversalPosition At(int x, int level = 0) => new(new DungeonSurfacePosition(new Vector2Int(x, 0), level));
        static DungeonFloorInstance Floor()
        {
            var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
            for (int x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "room");
            floor.TrySetMap(map); return floor;
        }
        [Test]
        public void EnemyRequiresCurrentSightAndViewedHeightEvenWhenMapIsRevealed()
        {
            var floor = Floor();
            floor.Fog.RestoreDiscovered(floor.Map, At(0));
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.False);
            floor.Fog.ToggleDebugFog();
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.False);
            floor.Fog.SetVisible(new[] { At(0) });
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.True);
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 1, true), Is.False);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, true), Is.False);
            Assert.That(DungeonMinimapView.CanShowActor(floor, At(0), 0, false), Is.True);
        }
        [Test]
        public void TerrainUsesExistingMemoryAndDoesNotRevealOverlappingSurface()
        {
            var floor = Floor(); floor.Fog.SetVisible(new[] { At(0) });
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0).Surface), Is.EqualTo(DungeonKnowledge.Visible));
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0, 1).Surface), Is.EqualTo(DungeonKnowledge.Unexplored));
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0).Surface), Is.EqualTo(DungeonKnowledge.Remembered));
            floor.Fog.SetMapDiscovered(false);
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(0).Surface), Is.EqualTo(DungeonKnowledge.Unexplored));
            floor.Fog.ToggleDebugFog();
            Assert.That(DungeonMinimapView.TerrainKnowledge(floor, At(2, 1).Surface), Is.EqualTo(DungeonKnowledge.Unexplored));
        }
        [Test]
        public void UnseenDoorChangesDoNotLeakIntoRenderedMap()
        {
            var floor = Floor(); var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            var go = new GameObject("minimap test");
            try
            {
                var door = new DoorDungeonObjectInstance("door", definition, new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
                floor.TryAddObject(door);
                floor.Fog.RestoreDiscovered(floor.Map, At(0));
                floor.Fog.Objects[door.InstanceId] = new DungeonObjectKnowledge { Active = true, State = false };
                var view = go.AddComponent<DungeonMinimapView>();
                Set(view, "floor", floor);
                Call(view, "DrawTerrain");
                var before = ((Color32[])Get(view, "terrain")).ToArray();
                door.TrySetOpen(true); Call(view, "DrawTerrain");
                CollectionAssert.AreEqual(before, (Color32[])Get(view, "terrain"));
                floor.Fog.SetVisible(new[] { At(0) }); Call(view, "DrawTerrain");
                Assert.That(((Color32[])Get(view, "terrain"))[126 * 252 + 130], Is.Not.EqualTo(before[126 * 252 + 130]));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(definition); }
        }
        [Test]
        public void StairActorIsShownOnlyAtItsConnectedLevels()
        {
            var floor = Floor();
            var lower = At(2).Surface; var upper = At(0, 1).Surface;
            floor.Map.TryAddStairs(lower, upper, new[] { new Vector2Int(2,0), new Vector2Int(1,0), Vector2Int.zero });
            var stair = floor.Map.Stairs.Single(); var node = new DungeonTraversalPosition(stair, 1);
            floor.Fog.SetVisible(new[] { node });
            Assert.That(DungeonMinimapView.CanShowActor(floor, node, 0, true), Is.True);
            Assert.That(DungeonMinimapView.CanShowActor(floor, node, 1, true), Is.True);
            Assert.That(DungeonMinimapView.CanShowActor(floor, node, 2, true), Is.False);
        }
        [Test]
        public void AuthoredPrefabHasReferencesAndCanCollapseWithoutDisablingItsButton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonMinimap.prefab");
            Assert.That(prefab, Is.Not.Null);
            var go = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = go.GetComponent<DungeonMinimapView>();
                var data = new SerializedObject(view);
                foreach (var field in new[] { "image", "content", "toggle", "title", "toggleLabel", "markers" })
                    Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                var image = (RawImage)Get(view, "image");
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(220,220)));
                Call(view, "Toggle");
                Assert.That(new SerializedObject((UnityEngine.Object)Get(view,"title")).FindProperty("m_text").stringValue, Is.EqualTo("Mapa - poziom 0"));
                Assert.That(new SerializedObject((UnityEngine.Object)Get(view,"toggleLabel")).FindProperty("m_text").stringValue, Is.EqualTo("+"));
                Assert.That(((GameObject)Get(view,"content")).activeSelf, Is.False);
                Assert.That(((Button)Get(view,"toggle")).gameObject.activeInHierarchy, Is.True);
                Call(view, "Toggle");
                Assert.That(((GameObject)Get(view,"content")).activeSelf, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void BodiesRequireSightOrRememberedDiscoveryAndUseSavedHeight()
        {
            var floor=Floor(); var body=new DungeonEnemyBodyInstance("body", "enemy", Vector2Int.zero, 0, null);
            floor.TryAddEnemyBody(body);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.False);
            floor.Fog.SetVisible(new[]{At(0)});
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out var cell),Is.True);
            Assert.That(cell,Is.EqualTo(Vector2Int.zero));
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.False);
            floor.Fog.Bodies.Add(body.InstanceId);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.True);
            floor.TrySetActorPosition("body:"+body.InstanceId,At(0,1));
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,0,out _),Is.False);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,1,out _),Is.False);
            floor.Fog.SetVisible(new[]{At(0,1)});
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,1,out _),Is.True);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            floor.Fog.SetMapDiscovered(false);
            Assert.That(DungeonMinimapView.TryGetBodyMarker(floor,body,1,out _),Is.False);
        }
        [Test]
        public void BodyIsRenderedAsCrossAndRemovedWithRuntimeBody()
        {
            var floor=Floor();var body=new DungeonEnemyBodyInstance("body", "enemy", Vector2Int.zero,0,null);
            floor.TryAddEnemyBody(body);floor.Fog.SetVisible(new[]{At(0)});
            var go=new GameObject("body minimap");
            try
            {
                var view=go.AddComponent<DungeonMinimapView>();Set(view,"floor",floor);Call(view,"DrawTerrain");
                var pixels=(Color32[])Get(view,"terrain");var color=(Color32)(Color)Get(view,"bodyColor");
                Assert.That(pixels[126*252+126],Is.EqualTo(color));
                Assert.That(pixels[124*252+124],Is.EqualTo(color));
                Assert.That(pixels[126*252+125],Is.Not.EqualTo(color));
                floor.TryRemoveEnemyBody(body.InstanceId);Call(view,"DrawTerrain");
                Assert.That(pixels[126*252+126],Is.Not.EqualTo(color));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void ViewportTracksFractionalMovementWithoutCellBoundaryJump()
        {
            var before = DungeonMinimapView.Viewport(new Vector2(.49f,0), Vector2Int.zero);
            var after = DungeonMinimapView.Viewport(new Vector2(.51f,0), Vector2Int.right);
            float absoluteBefore = before.x * DungeonMinimapView.TextureSize;
            float absoluteAfter = after.x * DungeonMinimapView.TextureSize + 8;
            Assert.That(absoluteAfter - absoluteBefore, Is.EqualTo(.02f * 8).Within(.0001f));
            var middle = DungeonMinimapView.Viewport(new Vector2(.25f,0), Vector2Int.zero);
            var origin = DungeonMinimapView.Viewport(Vector2.zero, Vector2Int.zero);
            Assert.That((middle.x - origin.x) * DungeonMinimapView.TextureSize, Is.EqualTo(2).Within(.0001f));
            Assert.That(before.xMin, Is.GreaterThan(0)); Assert.That(before.xMax, Is.LessThan(1));
        }
        [Test]
        public void SemanticObjectsUseRememberedPresenceUntilSeenAgain()
        {
            var floor = Floor(); var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            var go = new GameObject("minimap content test");
            try
            {
                var obj = new ObstacleDungeonObjectInstance("pillar", definition, Vector2Int.zero); floor.TryAddObject(obj);
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.False);
                floor.Fog.SetVisible(new[] { At(0) });
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.True);
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,1), Is.False);
                floor.Fog.Objects[obj.InstanceId] = new DungeonObjectKnowledge { Active=true };
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                var view=go.AddComponent<DungeonMinimapView>(); Set(view,"floor",floor);
                Call(view,"DrawTerrain"); var remembered=((Color32[])Get(view,"terrain")).ToArray();
                obj.TryDeactivate();
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.True);
                Call(view,"DrawTerrain"); CollectionAssert.AreEqual(remembered,(Color32[])Get(view,"terrain"));
                floor.Fog.SetVisible(new[] { At(0) });
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.False);
                Call(view,"DrawTerrain");
                Assert.That(((Color32[])Get(view,"terrain"))[126*252+126], Is.Not.EqualTo(remembered[126*252+126]));
                floor.Fog.Objects[obj.InstanceId].Active=false;
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                Assert.That(DungeonMinimapView.CanShowObject(floor,obj,0), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(definition); }
        }
        [Test]
        public void MarkerGeometryRetainsSizeAndClipsAtViewportEdge()
        {
            var go=new GameObject("markers",typeof(RectTransform));
            try
            {
                ((RectTransform)go.transform).sizeDelta=new Vector2(220,220);
                var markers=go.AddComponent<DungeonMinimapMarkers>();
                markers.AddMarker(Vector2.zero,Color.red); markers.AddMarker(new Vector2(110,0),Color.blue); markers.AddMarker(new Vector2(120,0),Color.white);
                using var vh=new VertexHelper();
                typeof(DungeonMinimapMarkers).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(markers,new object[]{vh});
                Assert.That(vh.currentVertCount,Is.EqualTo(8));
                var vertex=new UIVertex(); vh.PopulateUIVertex(ref vertex,0); Assert.That(vertex.position.x,Is.EqualTo(-2.5f));
                for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);Assert.That(Mathf.Abs(vertex.position.x),Is.LessThanOrEqualTo(110));}
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void LargeFloorRenderingHasFixedViewportAndDoesNotMutateKnowledge()
        {
            var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
            for (var x = -100; x < 100; x++) for (var y = -100; y < 100; y++) map.TryAddFloorCell(new Vector2Int(x,y), "room");
            floor.TrySetMap(map); floor.Fog.SetVisible(new[] { At(0) });
            var go = new GameObject("minimap performance");
            try
            {
                var view = go.AddComponent<DungeonMinimapView>(); Set(view,"floor",floor);
                Call(view,"DrawTerrain");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < 50; i++) Call(view,"DrawTerrain");
                timer.Stop();
                TestContext.WriteLine($"50 terrain refreshes on 40,000 cells: {timer.Elapsed.TotalMilliseconds:F2} ms; includes reflection, excludes GPU upload.");
                Assert.That(((Color32[])Get(view,"terrain")).Length, Is.EqualTo(DungeonMinimapView.TextureSize * DungeonMinimapView.TextureSize));
                Assert.That(floor.Fog.Discovered.Count, Is.EqualTo(1));
                Assert.That(floor.Fog.Visible.Count, Is.EqualTo(1));
                Assert.That(((Color32[])Get(view,"terrain"))[0], Is.EqualTo((Color32)Color.black));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void SceneWiresOneMinimapBelowWorkspaceWindows()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
                var maps = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonMinimapView>(true)).ToArray();
                Assert.That(maps.Length, Is.EqualTo(1));
                var coordinator = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonCombatCoordinator>(true)).Single();
                Assert.That(new SerializedObject(coordinator).FindProperty("minimap").objectReferenceValue, Is.EqualTo(maps[0]));
                Assert.That(maps[0].transform.GetSiblingIndex(), Is.Zero);
                Assert.That(maps[0].GetComponent<Canvas>(), Is.Null);
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
        static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
