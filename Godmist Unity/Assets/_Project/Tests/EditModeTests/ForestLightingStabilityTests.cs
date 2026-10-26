using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestLightingStabilityTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/forest-fog-shadow-stability-20261022"));
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static void Call(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        static void Refresh(DungeonSilhouetteLighting lighting)
        {
            typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame", Private).SetValue(lighting, int.MinValue);
            Call(lighting, "RefreshLighting", 1f);
        }

        [Test]
        public void ForestKnowledgeMovementCostWithGrowingDiscovery()
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-fog-spikes-20261022"));
            Directory.CreateDirectory(output);
            var stage=File.Exists(Path.Combine(output,"stage.txt"))?File.ReadAllText(Path.Combine(output,"stage.txt")).Trim():"baseline";
            var root=new GameObject("Forest knowledge movement benchmark",typeof(Grid));
            var source=new Texture2D(2,2);var sprite=Sprite.Create(source,new Rect(0,0,2,2),Vector2.zero,1);
            var rows=new List<string>();
            try
            {
                var tiles=new GameObject("Floor",typeof(Tilemap));tiles.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();
                void Set(string name,object value)=>typeof(DungeonFloorMapView).GetField(name,Private).SetValue(view,value);
                Set("floorTilemap",tiles.GetComponent<Tilemap>());
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonVegetationPresentation>(
                    "Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var backdrop=new GameObject("Backdrop",typeof(SpriteRenderer));backdrop.transform.SetParent(root.transform,false);
                backdrop.GetComponent<SpriteRenderer>().sprite=sprite;backdrop.transform.position=new Vector3(-12,-12);
                backdrop.transform.localScale=new Vector3(60,60,1);Set("forestBackdrop",backdrop.GetComponent<SpriteRenderer>());
                var floor=new DungeonFloorInstance(0);var map=new DungeonFloorMap();
                for(int y=0;y<96;y++)for(int x=0;x<96;x++)
                    if(x%8<4||y%12<4)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                floor.TrySetMap(map);Set("boundMap",map);Set("<FogFloor>k__BackingField",floor);
                var nodes=map.FloorCells.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))).ToArray();
                var refresh=(Action)typeof(DungeonFloorMapView).GetMethod("RefreshForestKnowledge",Private).CreateDelegate(typeof(Action),view);
                var advance=(Action<float>)typeof(DungeonFloorMapView).GetMethod("AdvanceForestKnowledge",Private).CreateDelegate(typeof(Action<float>),view);
                foreach(int known in new[]{128,1024,nodes.Length})
                {
                    floor.Fog.SetMapDiscovered(false);floor.Fog.SetVisible(nodes.Take(80),nodes.Take(known));refresh();
                    Set("snapFogAnimation",false);
                    int scans=view.ForestKnowledgeDiscoveryScanCount, samples=view.ForestKnowledgeStencilSampleCount;
                    var retarget=new List<double>();var animate=new List<double>();
                    for(int i=0;i<18;i++)
                    {
                        floor.Fog.SetVisible(nodes.Skip(i*7).Take(80),Array.Empty<DungeonTraversalPosition>());
                        var timer=System.Diagnostics.Stopwatch.StartNew();refresh();retarget.Add(timer.Elapsed.TotalMilliseconds);
                        timer.Restart();advance(.0833333f);animate.Add(timer.Elapsed.TotalMilliseconds);
                    }
                    retarget.Sort();animate.Sort();
                    Assert.That(view.ForestKnowledgeDiscoveryScanCount,Is.EqualTo(scans),"Walking over remembered terrain cannot rescan discovery.");
                    Assert.That(view.ForestKnowledgeStencilSampleCount-samples,Is.LessThanOrEqualTo(18*80*441),"Only current visibility paints stencils after discovery is cached.");
                    rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"discovered\":{0},\"retargetMedianMs\":{1:F4},\"retargetMaxMs\":{2:F4},\"animateMedianMs\":{3:F4}",
                        known,retarget[retarget.Count/2],retarget.Last(),animate[animate.Count/2])+"}");
                    TestContext.WriteLine(rows.Last());
                }
                File.WriteAllText(Path.Combine(output,stage+"-timing.json"),"["+string.Join(",\n",rows)+"]");
                Call(view,"ClearForestKnowledge");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);Object.DestroyImmediate(source);}
        }

        [Test]
        public void ForestKnowledgeCacheMatchesOriginalFullPaintAcrossMovementResetAndGeometry()
        {
            var root=new GameObject("Forest knowledge exact cache regression",typeof(Grid));
            var source=new Texture2D(2,2);var sprite=Sprite.Create(source,new Rect(0,0,2,2),Vector2.zero,1);
            try
            {
                var tiles=new GameObject("Floor",typeof(Tilemap));tiles.transform.SetParent(root.transform,false);
                var view=root.AddComponent<DungeonFloorMapView>();
                void Set(string name,object value)=>typeof(DungeonFloorMapView).GetField(name,Private).SetValue(view,value);
                Set("floorTilemap",tiles.GetComponent<Tilemap>());
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonVegetationPresentation>(
                    "Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var backdrop=new GameObject("Backdrop",typeof(SpriteRenderer));backdrop.transform.SetParent(root.transform,false);
                backdrop.GetComponent<SpriteRenderer>().sprite=sprite;backdrop.transform.position=new Vector3(-18,-18);
                backdrop.transform.localScale=new Vector3(22,22,1);Set("forestBackdrop",backdrop.GetComponent<SpriteRenderer>());
                var floor=new DungeonFloorInstance(0);var map=new DungeonFloorMap();
                for(int y=-8;y<12;y++)for(int x=-8;x<12;x++)
                    if(x%5!=0&&y%6!=0)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                floor.TrySetMap(map);Set("boundMap",map);Set("<FogFloor>k__BackingField",floor);
                var nodes=map.FloorCells.OrderBy(c=>c.y).ThenBy(c=>c.x).Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))).ToArray();
                var previousPixels=Array.Empty<Color32>();int checkedPixels=0;
                void Check()
                {
                    Call(view,"RefreshForestKnowledge");
                    var texture=Read<Texture2D>(view,"forestKnowledgeTexture");var origin=Read<Vector2Int>(view,"forestKnowledgeOrigin");
                    int width=texture.width,height=texture.height;
                    var expected=new float[width*height];
                    int depth=(int)typeof(DungeonFloorMapView).GetProperty("ForestBoundaryDepthInCells",Private).GetValue(view);
                    // Independent reference: the original full traversal, with no cache/delta helpers.
                    void Paint(DungeonTraversalPosition position,float brightness)
                    {
                        if(!map.TryGetTraversalCell(position,out var cell))return;
                        for(int y=-depth;y<=depth;y++)for(int x=-depth;x<=depth;x++)
                        {
                            if((x!=0||y!=0)&&map.IsWalkable(cell+new Vector2Int(x,y)))continue;
                            int px=cell.x+x-origin.x,py=cell.y+y-origin.y;
                            if(px<0||py<0||px>=width||py>=height)continue;
                            float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1f,depth,Mathf.Sqrt(x*x+y*y)));
                            expected[py*width+px]=Mathf.Max(expected[py*width+px],brightness*weight);
                        }
                    }
                    foreach(var position in floor.Fog.Discovered)Paint(position,.35f);
                    foreach(var position in floor.Fog.Visible)Paint(position,1);
                    var actual=Read<float[]>(view,"forestKnowledgeTargets");
                    // Use the original per-pixel animation equation at intermediate times too.
                    // Check() is called again before and after visibility/reset changes.
                    foreach(float delta in new[]{.03f,.07f})
                    {
                        var prior=texture.GetPixels32();Call(view,"AdvanceForestKnowledge",delta);var intermediate=texture.GetPixels32();
                        for(int i=0;i<expected.Length;i++)
                        {
                            int alpha=Mathf.RoundToInt(Mathf.MoveTowards(prior[i].a/255f,1-expected[i],delta/DungeonFogTransition.Duration)*255);
                            Assert.That((int)intermediate[i].a,Is.EqualTo(alpha).Within(1),"Intermediate alpha differs at pixel "+i);
                        }
                    }
                    Call(view,"AdvanceForestKnowledge",1f);var pixels=texture.GetPixels32();
                    for(int i=0;i<expected.Length;i++)
                    {
                        Assert.That(actual[i],Is.EqualTo(expected[i]).Within(1e-7f),"Reference target differs at pixel "+i);
                        Assert.That((int)pixels[i].a,Is.EqualTo(Mathf.RoundToInt((1-expected[i])*255)).Within(1),"Rendered alpha differs at pixel "+i);
                    }
                    checkedPixels+=pixels.Length;previousPixels=pixels;
                }
                for(int step=0;step<18;step++)
                {
                    // A subset of visible cells is deliberately not permanently discovered.
                    floor.Fog.SetVisible(nodes.Skip(step*9).Take(25),step%3==0?nodes.Skip(step*9).Take(13):Array.Empty<DungeonTraversalPosition>());
                    Set("snapFogAnimation",false);Check();
                    int visits=view.ForestKnowledgeAnimationPixelCount;
                    Call(view,"AdvanceForestKnowledge",.08f);
                    Assert.That(view.ForestKnowledgeAnimationPixelCount,Is.EqualTo(visits),"Settled pixels must leave the animation set.");
                }
                floor.Fog.RestoreDiscovered(map,nodes.Last());Check(); // Restore does not increment Revision.
                floor.Fog.SetMapDiscovered(false);floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>(),Array.Empty<DungeonTraversalPosition>());Check();
                floor.Fog.SetVisible(nodes.Take(25));Check();
                var beforeToggle=previousPixels;
                floor.Fog.ToggleDebugFog();Call(view,"RefreshForestKnowledge");
                Assert.That(Read<SpriteRenderer>(view,"forestKnowledgeRenderer").enabled,Is.False);
                floor.Fog.ToggleDebugFog();Check();Assert.That(previousPixels,Is.EqualTo(beforeToggle));
                map.TryAddFloorCell(new Vector2Int(-10,-7),"new floor");Check();
                floor=new DungeonFloorInstance(1);floor.TrySetMap(map);Set("<FogFloor>k__BackingField",floor);
                floor.Fog.SetVisible(nodes.Skip(80).Take(25));Check();
                Call(view,"ClearForestKnowledge");Check();
                TestContext.WriteLine("Full-paint oracle matched "+checkedPixels+" filter pixels across 25 state changes.");
                Call(view,"ClearForestKnowledge");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);Object.DestroyImmediate(source);}
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ForestFogBehindTreeMatchesEnemySightEvenBesideVisibleGround(bool large, bool remembered)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/forest-fog-contrast-20261022"));
            Directory.CreateDirectory(output);
            var root = new GameObject("Forest LOS filter regression", typeof(Grid));
            var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            tiles.transform.SetParent(root.transform, false);
            var view = root.AddComponent<DungeonFloorMapView>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            var definition = large
                ? Object.Instantiate(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>(
                    "Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Tree_Prototype.asset"))
                : ScriptableObject.CreateInstance<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>();
            typeof(Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition).GetField("<BlocksLineOfSight>k__BackingField", Private)
                .SetValue(definition, true);
            var target = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var priorActive = RenderTexture.active;
            var camera = new GameObject("Fog GPU camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            Texture2D filter = null;
            try
            {
                void Set(string name, object value) => typeof(DungeonFloorMapView).GetField(name, Private).SetValue(view, value);
                Set("floorTilemap", tiles.GetComponent<Tilemap>()); Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonVegetationPresentation>(
                    "Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                for (int y = -2; y <= 6; y++) for (int x = -3; x <= 4; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(map);
                floor.TryAddObject(new ObstacleDungeonObjectInstance("blocking tree", definition, new Vector2Int(0, 1)));
                DungeonTraversalPosition At(Vector2Int cell) => new(new DungeonSurfacePosition(cell));
                var hidden = new Vector2Int(0, large ? 3 : 2);
                var beside = hidden + Vector2Int.left;
                if (remembered) floor.Fog.RestoreDiscovered(map, At(hidden));
                var fogSystem = new DungeonFogSystem();
                fogSystem.Update(floor, new[] { At(new Vector2Int(-1, 0)) });
                Assert.That(floor.Fog.Get(At(hidden)), Is.EqualTo(remembered ? DungeonKnowledge.Remembered : DungeonKnowledge.Unexplored));
                Assert.That(floor.Fog.Get(At(beside)), Is.EqualTo(DungeonKnowledge.Visible));
                var discovered = floor.Fog.Discovered.ToArray(); var visible = floor.Fog.Visible.ToArray();
                view.Rebuild(map); view.ApplyFog(floor);
                filter = Read<Texture2D>(view, "forestKnowledgeTexture");
                var origin = Read<Vector2Int>(view, "forestKnowledgeOrigin");
                float Alpha(Vector2Int cell) => filter.GetPixel(cell.x-origin.x, cell.y-origin.y).a;
                Assert.That(view.IsCurrentlySeen(At(hidden)), Is.False, "Enemy visibility uses this same knowledge.");
                Assert.That(Alpha(hidden), Is.EqualTo(remembered ? .65f : 1f).Within(1f/255),
                    "A visible neighbour may not erase fog behind the tree.");
                foreach (var cell in map.FloorCells)
                {
                    var knowledge = floor.Fog.Get(At(cell));
                    float alpha = knowledge == DungeonKnowledge.Visible ? 0 : knowledge == DungeonKnowledge.Remembered ? .65f : 1;
                    Assert.That(Alpha(cell), Is.EqualTo(alpha).Within(1f/255), "Fog state mismatch at " + cell);
                }
                Assert.That(floor.Fog.Discovered, Is.EquivalentTo(discovered));
                Assert.That(floor.Fog.Visible, Is.EquivalentTo(visible));
                var renderer = Read<SpriteRenderer>(view, "forestKnowledgeRenderer");
                renderer.gameObject.layer = 30;
                camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 1;
                camera.transform.position = new Vector3(0, 2, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.cullingMask = 1 << 30; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, $"tree-{(large ? 2 : 1)}-remembered-{remembered}.png"), pixels.EncodeToPNG());
                float Pixel(Vector2Int cell)
                {
                    var screen = camera.WorldToScreenPoint(view.CellToWorldCenter(cell));
                    return pixels.GetPixel(Mathf.FloorToInt(screen.x), Mathf.FloorToInt(screen.y)).r;
                }
                Assert.That(Pixel(beside), Is.GreaterThan(.97f), "The neighbouring visible tile remains clear on GPU.");
                if (remembered)
                    Assert.That(Pixel(hidden), Is.InRange(.55f, .7f), "Remembered ground must be clearly darker, while retaining detail.");
                else Assert.That(Pixel(hidden), Is.LessThan(.04f), "Actual fog rendering must cover the blocked tile.");
                fogSystem.Update(floor, new[] { At(hidden) }); view.ApplyFog(floor);
                Assert.That(Alpha(hidden), Is.LessThan(.01f), "Walking behind the blocker reveals the tile normally.");
                fogSystem.Update(floor, new[] { At(new Vector2Int(-1, 0)) }); view.ApplyFog(floor);
                Assert.That(floor.Fog.Get(At(hidden)), Is.EqualTo(DungeonKnowledge.Remembered));
                Assert.That(Alpha(hidden), Is.EqualTo(.65f).Within(1f/255), "Leaving LOS restores remembered fog without losing discovery.");
                File.WriteAllText(Path.Combine(output, $"tree-{(large ? 2 : 1)}-remembered-{remembered}.txt"),
                    $"Hidden GPU={Pixel(hidden)}; visible neighbour GPU={Pixel(beside)}; discovery and visibility unchanged by renderer.");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = priorActive;
                // This fixture never enters Play Mode; invoke the same release
                // path explicitly rather than relying on MonoBehaviour teardown.
                Call(view, "ReleaseSoftFog");
                Object.DestroyImmediate(root);
                if (!ReferenceEquals(filter, null)) Assert.That(filter == null, Is.True, "Releasing the map's fog must release its texture.");
                Object.DestroyImmediate(definition); Object.DestroyImmediate(tile);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void ProductionForestFogDimsSceneryOnceRegardlessOfSurfaceOrObject()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../LocalHandoffs/forest-fog-uniformity-20261022"));
            Directory.CreateDirectory(output);
            var fixture = new DungeonEntryPerformanceTests
            { OutputOverride = output, StageOverride = "uniformity", SeedOverride = 171026 };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var lighting = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var floor = session.ActiveDungeon.Floors[0];
                var camera = Read<Camera>(lighting, "worldCamera");
                var objects = bootstrap.gameObject.scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<DungeonObjectView>(true)).ToArray();
                var actors = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController.ActiveMemberViews.ToArray();
                var target = new RenderTexture(1280, 720, 24);
                var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
                var oldTarget = camera.targetTexture; var oldMask = camera.cullingMask; var oldActive = RenderTexture.active;
                Color32[] Render(string name, bool includeFlames = false)
                {
                    foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                    // Compare static scenery only: enemies correctly disappear outside LOS.
                    foreach (var actor in actors)
                        foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 29;
                    foreach (var enemy in Read<DungeonEncounterSpawner>(bootstrap, "encounterSpawner").EnemyViews)
                        if (enemy != null)
                            foreach (var renderer in enemy.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 29;
                    // Animated emitters change between Camera.Render calls and
                    // have their own LOS visibility. Keep their illumination,
                    // excluding only their drawing from the static pixel proof.
                    if (!includeFlames)
                        foreach (var renderer in lighting.GetComponentsInChildren<SpriteRenderer>(true))
                            if (renderer.sharedMaterial?.shader.name == "Godmist/Dungeon/Pixel Torch")
                                renderer.gameObject.layer = 29;
                    SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                void Present()
                {
                    map.ApplyFog(floor);
                    foreach (var obj in objects) Call(obj, "LateUpdate");
                    Refresh(lighting);
                }
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30;
                    camera.aspect = 16f / 9; camera.orthographicSize = 5.5f;
                    camera.transform.position = new Vector3(actors[0].Position.x, actors[0].Position.y + 1.5f, -10);
                    DungeonFogSystem.RevealMap(floor);
                    var all = floor.Map.Surfaces.Select(s => new DungeonTraversalPosition(s)).ToArray();
                    floor.Fog.SetVisible(all); Present();
                    // Exclude the animated emitters' small contact footprints as
                    // well. They follow emitter visibility rather than static
                    // obstacle memory; all real prop contacts remain exercised.
                    var contactSources = Read<List<SpriteRenderer>>(lighting, "groundContactPresentations");
                    var animatedContacts = contactSources.Where(sr =>
                        sr != null && sr.sharedMaterial?.shader.name == "Godmist/Dungeon/Pixel Torch").ToArray();
                    foreach (var sr in animatedContacts) contactSources.Remove(sr);
                    Present();
                    var filter = Read<SpriteRenderer>(map, "forestKnowledgeRenderer");
                    filter.enabled = false;
                    var visible = Render("visible-scenery");
                    var repeated = Render("visible-repeat");
                    int movingPixels = Enumerable.Range(0, visible.Length).Count(i =>
                        Math.Abs(visible[i].r - repeated[i].r) > 2 || Math.Abs(visible[i].g - repeated[i].g) > 2
                        || Math.Abs(visible[i].b - repeated[i].b) > 2);
                    var receiverInputs = new MaterialPropertyBlock();
                    string Describe(SpriteRenderer sr)
                    {
                        sr.GetPropertyBlock(receiverInputs);
                        return $"{sr.name} position={sr.transform.position} tint={sr.color} material={sr.sharedMaterial?.name} off={sr.forceRenderingOff} enabled={sr.enabled} order={sr.sortingOrder} readability={receiverInputs.GetFloat("_ForestPropReadability")} profile={receiverInputs.GetFloat("_ForestReadabilityProfile")}";
                    }
                    var rendererStates = bootstrap.gameObject.scene.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)).ToDictionary(sr => sr, Describe);
                    var visibleTiles = Read<Tilemap>(map, "floorTilemap");
                    var tileColors = floor.Map.FloorCells.ToDictionary(c => c,
                        c => visibleTiles.GetColor(new Vector3Int(c.x, c.y, 0)));
                    floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); Present();
                    var remembered = Render("remembered-composite");
                    filter.enabled = false;
                    var rawRemembered = Render("remembered-without-composite");
                    int changed = Enumerable.Range(0, visible.Length).Count(i =>
                        Math.Abs(visible[i].r - rawRemembered[i].r) > 2
                        || Math.Abs(visible[i].g - rawRemembered[i].g) > 2
                        || Math.Abs(visible[i].b - rawRemembered[i].b) > 2);
                    int dimmedTiles = tileColors.Count(pair => visibleTiles.GetColor(
                        new Vector3Int(pair.Key.x, pair.Key.y, 0)) != pair.Value);
                    File.WriteAllText(Path.Combine(output, "uniformity.txt"),
                        $"Scenery pixels changed before composite={changed}; independently dimmed tiles={dimmedTiles}; object views={objects.Length}; repeat differences={movingPixels}");
                    File.WriteAllLines(Path.Combine(output, "renderer-changes.txt"), rendererStates
                        .Where(pair => pair.Key != null && pair.Value != Describe(pair.Key))
                        .Select(pair => pair.Value + "\n -> " + Describe(pair.Key)));
                    Assert.That(objects.Length, Is.GreaterThan(0), "Exercise real obstacle and interactable views, including their LateUpdate fog path.");
                    Assert.That(dimmedTiles, Is.Zero, "Forest ground must not independently dim just its path channel.");
                    Assert.That(changed, Is.Zero, "The composite must be the only remembered darkening for scenery, including object views.");
                    int probes = 0, mismatches = 0;
                    for (int y = 1; y < 719; y++) for (int x = 1; x < 1279; x++)
                    {
                        var world = camera.ScreenToWorldPoint(new Vector3(x + .5f, y + .5f, 10));
                        var cell = map.WorldToCell(world);
                        var centre = map.CellToWorldCenter(cell);
                        if (!floor.Map.IsWalkable(cell) || Math.Abs(world.x-centre.x) > .2f
                            || Math.Abs(world.y-centre.y) > .2f) continue;
                        int i = y * 1280 + x;
                        var sample = visible[i];
                        if (sample.r + sample.g + sample.b < 30) continue;
                        probes++;
                        byte Expected(byte value) => (byte)Mathf.RoundToInt(255 * Mathf.LinearToGammaSpace(
                            Mathf.GammaToLinearSpace(value / 255f) * .35f));
                        if (Math.Abs(remembered[i].r - Expected(sample.r)) > 2
                            || Math.Abs(remembered[i].g - Expected(sample.g)) > 2
                            || Math.Abs(remembered[i].b - Expected(sample.b)) > 2) mismatches++;
                    }
                    File.AppendAllText(Path.Combine(output, "uniformity.txt"), $"\nUniform transmission GPU probes={probes}; mismatches={mismatches}");
                    Assert.That(probes, Is.GreaterThan(1000));
                    Assert.That(mismatches, Is.Zero, "Scenery pixels must share the same linear fog transmission regardless of path, litter or prop.");
                    contactSources.AddRange(animatedContacts);
                    new DungeonFogSystem().Update(floor, actors.Select(a => new DungeonTraversalPosition(
                        new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                    Present();
                    Render("mixed-los-production", true);
                }
                finally
                {
                    camera.targetTexture = oldTarget; camera.cullingMask = oldMask; RenderTexture.active = oldActive;
                    Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
        }

        [TestCase(false)] [TestCase(true)]
        public void ProductionForestDepthCanBeInspectedOnEverySideOfClearing(bool isolated)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../LocalHandoffs/forest-background-depth-20261022"));
            Directory.CreateDirectory(output);
            string stage = File.ReadAllText(Path.Combine(output, "stage.txt")).Trim();
            var fixture = new DungeonEntryPerformanceTests
            { OutputOverride = output, StageOverride = stage, SeedOverride = 171026 };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var floor = session.ActiveDungeon.Floors[0];
                var actors = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController.ActiveMemberViews.ToArray();
                var camera = Read<Camera>(rig, "worldCamera");
                if(isolated)
                {
                    // No neighbouring rooms exist here: any bare soil outside
                    // this clearing is genuinely off-map woodland, not a real
                    // unknown room that must retain its own fog state.
                    var layout=new DungeonFloorMap();
                    for(int y=0;y<8;y++)for(int x=0;x<8;x++)layout.TryAddFloorCell(new Vector2Int(x,y),"isolated clearing");
                    foreach(var cell in layout.FloorCells)
                        foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                            if(!layout.IsWalkable(cell+d))layout.TryAddWall(cell,cell+d);
                    floor=new DungeonFloorInstance(0);floor.TrySetMap(layout);map.Rebuild(layout);
                    foreach(var obj in Read<DungeonObjectSpawner>(bootstrap,"objectSpawner").SpawnedObjects)
                        if(obj!=null)obj.gameObject.SetActive(false);
                    foreach(var enemy in Read<DungeonEncounterSpawner>(bootstrap,"encounterSpawner").EnemyViews)
                        if(enemy!=null)enemy.gameObject.SetActive(false);
                    rig.Initialize(Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController,null,null,map);
                    stage+="-isolated";
                }
                var roomCells = floor.Map.FloorCells.GroupBy(c =>
                    { floor.Map.TryGetRoomId(c, out var roomId); return roomId; })
                    .OrderByDescending(g => g.Count()).First().ToArray();
                var centre = new Vector2(roomCells.Average(c => (float)c.x), roomCells.Average(c => (float)c.y));
                var target = new RenderTexture(1280, 720, 24);
                var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
                var oldTarget = camera.targetTexture; var oldMask = camera.cullingMask; var oldActive = RenderTexture.active;
                var report = new List<string>();
                void Capture(string label)
                {
                    foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                    SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, stage + "-" + label + ".png"), image.EncodeToPNG());
                }
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30;
                    camera.aspect = 16f / 9; camera.orthographicSize = 6.5f;
                    foreach (var side in new[] { ("west", Vector2Int.left), ("east", Vector2Int.right),
                        ("south", Vector2Int.down), ("north", Vector2Int.up) })
                    {
                        var edge = roomCells.Where(c => !floor.Map.IsWalkable(c + side.Item2))
                            .OrderByDescending(c => Vector2.Dot(c, side.Item2))
                            .ThenBy(c => ((Vector2)c-centre).sqrMagnitude).First();
                        var free = roomCells.Where(c => !floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                            .Any(o => o.IsActive && o.BlocksMovement && o.OccupiesCell(c)))
                            .OrderBy(c => (c-edge).sqrMagnitude).Take(actors.Length).ToArray();
                        for (int i = 0; i < actors.Length; i++) actors[i].SetPosition(map.CellToWorldCenter(free[i]));
                        camera.transform.position = map.CellToWorldCenter(edge) + (Vector2)side.Item2 * 2.5f;
                        camera.transform.position += new Vector3(0, .5f, -10);
                        new DungeonFogSystem().Update(floor, actors.Select(a => new DungeonTraversalPosition(
                            new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                        map.ApplyFog(floor);
                        Call(map,"AdvanceForestKnowledge",1f);
                        foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                            foreach (var obj in root.GetComponentsInChildren<DungeonObjectView>(true)) Call(obj, "LateUpdate");
                        foreach (var actor in actors) Call(actor, "LateUpdate");
                        foreach (var enemy in Read<DungeonEncounterSpawner>(bootstrap, "encounterSpawner").EnemyViews)
                            if (enemy != null) Call(enemy, "LateUpdate");
                        Refresh(rig);
                        Call(rig, "RefreshForestPartyReveal"); Call(rig, "AdvanceForestCanopyFade", .5f);
                        Capture(side.Item1);
                        var filter = Read<SpriteRenderer>(map, "forestKnowledgeRenderer");
                        filter.enabled = false; Capture(side.Item1 + "-without-filter"); filter.enabled = true;
                        var trees = Read<Dictionary<SpriteRenderer, RectInt>>(map, "forestTreeFootprints");
                        var deep = trees.Count(p => floor.Map.FloorCells.Min(c => Vector2.Distance(c, p.Value.center)) >= 3);
                        var mask = Read<Texture2D>(map, "forestKnowledgeTexture");
                        var origin = Read<Vector2Int>(map, "forestKnowledgeOrigin");
                        var sample = edge + side.Item2 * 4;
                        report.Add($"{side.Item1}: edge={edge}; deep tree footprints={deep}; alpha four cells outside={mask.GetPixel(sample.x-origin.x, sample.y-origin.y).a}");
                        Assert.That(deep, Is.GreaterThan(0), "The scene must actually contain multiple woodland layers.");
                        Assert.That(floor.Objects.Any(o => o.Definition.Id == "object.forest.tree.prototype"), Is.False);
                    }
                    File.WriteAllLines(Path.Combine(output, stage + "-depth.txt"), report);
                }
                finally
                {
                    camera.targetTexture = oldTarget; camera.cullingMask = oldMask; RenderTexture.active = oldActive;
                    Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
        }

        [Test]
        public void ProductionPassageCanopyFadeHasBeforeAndAfterRender()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-passage-canopy-20261022"));
            Directory.CreateDirectory(output);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=output,StageOverride="passage",SeedOverride=171026};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var camera=Read<Camera>(rig,"worldCamera");var floor=session.ActiveDungeon.Floors[0];
                var actors=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController.ActiveMemberViews.ToArray();
                DungeonFogSystem.RevealMap(floor);
                floor.Fog.SetVisible(floor.Map.Surfaces.Select(s=>new DungeonTraversalPosition(s)));map.ApplyFog(floor);
                map.CollectVisibleForestPassageCanopies(new List<SpriteRenderer>());
                var passageCells=Read<HashSet<Vector2Int>>(map,"forestPassageCells");
                var trees=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
                var cell=passageCells.Select(c=>(Cell:c,Point:map.CellToWorldCenter(c)))
                    .Where(c=>Vector2.Distance(c.Point,actors[0].Position)>8)
                    .Select(c=>(c.Cell,c.Point,Count:trees.Keys.Count(t=>t.bounds.Contains(c.Point)&&map.ForestCanopyContainsPoint(t,c.Point))))
                    .Where(c=>c.Count>0).OrderByDescending(c=>c.Count).ThenBy(c=>c.Cell.y).ThenBy(c=>c.Cell.x).First();
                var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                var oldTarget=camera.targetTexture;var oldMask=camera.cullingMask;var oldActive=RenderTexture.active;
                Color32[] Capture(string name)
                {
                    foreach(var root in bootstrap.gameObject.scene.GetRootGameObjects())
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
                    SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();
                }
                try
                {
                    camera.targetTexture=target;camera.cullingMask=1<<30;camera.aspect=16f/9;camera.orthographicSize=4.5f;
                    camera.transform.position=new Vector3(cell.Point.x,cell.Point.y+.5f,-10);
                    map.ApplyFog(floor);Refresh(rig);
                    foreach(var actor in actors)Call(actor,"LateUpdate");
                    foreach(var enemy in Read<DungeonEncounterSpawner>(bootstrap,"encounterSpawner").EnemyViews)
                        if(enemy!=null)Call(enemy,"LateUpdate");
                    Call(rig,"ResetForestCanopyFade");Call(rig,"RefreshForestPartyReveal");
                    var targets=Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets");
                    foreach(var tree in targets.Where(p=>Mathf.Approximately(p.Value,160f/255)).Select(p=>p.Key).ToArray())targets.Remove(tree);
                    Call(rig,"AdvanceForestCanopyFade",.6f);var before=Capture("passage-before");
                    Call(rig,"ResetForestCanopyFade");Call(rig,"RefreshForestPartyReveal");
                    Assert.That(rig.LastForestPassageRevealCount,Is.GreaterThan(0));
                    Call(rig,"AdvanceForestCanopyFade",.1f);Capture("passage-100ms");
                    Call(rig,"AdvanceForestCanopyFade",.1f);var after=Capture("passage-after");
                    int changed=Enumerable.Range(0,before.Length).Count(i=>Math.Abs(before[i].r-after[i].r)>2
                        ||Math.Abs(before[i].g-after[i].g)>2||Math.Abs(before[i].b-after[i].b)>2);
                    File.WriteAllText(Path.Combine(output,"production.txt"),$"Passage cell={cell.Cell}; overlapping crowns={cell.Count}; selected route crowns={rig.LastForestPassageRevealCount}; changed GPU pixels={changed}. Preview uses fully revealed terrain to isolate route readability; fog-state restrictions have separate tests.");
                    Assert.That(changed,Is.GreaterThan(100),"Real passage foliage must visibly respond, not only a private target value.");
                }
                finally{camera.targetTexture=oldTarget;camera.cullingMask=oldMask;RenderTexture.active=oldActive;Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Long);
        }

        [Test]
        public void ProductionLowerRoomCanopiesHaveBeforeAndAfterRenders()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-interior-tree-removal-20261022"));
            Directory.CreateDirectory(output);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=output,StageOverride="foreground",SeedOverride=171026};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var camera=Read<Camera>(rig,"worldCamera");var floor=session.ActiveDungeon.Floors[0];
                var actors=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController.ActiveMemberViews.ToArray();
                map.ApplyFog(floor);
                map.CollectVisibleForestPassageCanopies(new List<SpriteRenderer>());
                var passageCells=Read<HashSet<Vector2Int>>(map,"forestPassageCells");
                var trees=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
                var registeredCrowns=Read<Dictionary<SpriteRenderer,SortingGroup>>(map,"forestCanopyGroups");
                var dividers=Read<HashSet<SpriteRenderer>>(map,"forestDividerCanopies");
                Assert.That(registeredCrowns.Keys.Except(trees.Keys).Except(dividers),Is.Empty,
                    "Every mature tree must belong to the off-map boundary or an authored blocking divider, not a walkable fringe marker.");
                foreach(var reservation in trees.Values)
                    foreach(var reserved in reservation.allPositionsWithin)
                        Assert.That(floor.Map.IsWalkable(reserved),Is.False,"A tree root cannot occupy clearing floor.");
                Assert.That(map.GetComponentsInChildren<Transform>().Any(t=>t.name=="Individual tree roots"||t.name=="Tree art"),Is.False);
                var samples=floor.Map.FloorCells.Where(c=>!passageCells.Contains(c))
                    .Select(c=>(Cell:c,Point:map.CellToWorldCenter(c)))
                    .Where(c=>Vector2.Distance(c.Point,actors[0].Position)>10)
                    .Select(c=>(c.Cell,c.Point,Count:trees.Keys.Count(t=>c.Point.y>=map.ForestCanopyGroundBounds(t).yMax
                        &&t.bounds.Contains(c.Point)&&map.ForestCanopyContainsPoint(t,c.Point))))
                    .Where(c=>c.Count>0).OrderByDescending(c=>c.Count).ThenBy(c=>c.Cell.y).ThenBy(c=>c.Cell.x).ToArray();
                File.WriteAllLines(Path.Combine(output,"rooms.txt"),floor.Rooms.Select(r=>r.RoomId+" "+r.TemplateId));
                Assert.That(samples,Is.Not.Empty,"Find wide-room floor obscured by native southern boundary crowns.");
                var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                var oldTarget=camera.targetTexture;var oldMask=camera.cullingMask;var oldActive=RenderTexture.active;
                var passage=new List<SpriteRenderer>();var foreground=new List<SpriteRenderer>();
                Color32[] Capture(string name)
                {
                    foreach(var root in bootstrap.gameObject.scene.GetRootGameObjects())
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
                    SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();
                }
                try
                {
                    camera.targetTexture=target;camera.cullingMask=1<<30;camera.aspect=16f/9;camera.orthographicSize=4.5f;
                    string Template(Vector2Int cell)
                    {
                        floor.Map.TryGetRoomId(cell,out var room);
                        return floor.Rooms.FirstOrDefault(r=>r.RoomId==room)?.TemplateId??"";
                    }
                    var first=samples.First(s=>Template(s.Cell)=="room.forest.production.clearing_wide");
                    // Choose a real dead-end objective room from the current
                    // graph instead of a retired blind-glade template.
                    var second=samples.First(s=>
                    {
                        floor.Map.TryGetRoomId(s.Cell,out var roomId);
                        var room=floor.Rooms.First(r=>r.RoomId==roomId);
                        return room.RoomType!=DungeonRoomType.Start && room.RoomType!=DungeonRoomType.Final
                            && floor.Connections.Count(c=>c.FirstRoomId==roomId || c.SecondRoomId==roomId)==1;
                    });
                    File.WriteAllText(Path.Combine(output,"production.txt"),"Current party LOS and normal fog, without RevealMap. Before disables only newly added foreground targets; existing passage and actor policies remain active.\n");
                    int index=0;
                    foreach(var sample in new[]{first,second})
                    {
                        string label=++index==1?"clearing":"alcove";
                        camera.transform.position=new Vector3(sample.Point.x,sample.Point.y+1.5f,-10);
                        floor.Map.TryGetRoomId(sample.Cell,out var sampleRoom);
                        var positions=floor.Map.FloorCells.Where(c=>floor.Map.TryGetRoomId(c,out var owner)&&owner==sampleRoom
                            &&!floor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(o=>o.IsActive&&o.BlocksMovement&&o.OccupiesCell(c)))
                            .OrderBy(c=>Vector2.Distance(map.CellToWorldCenter(c),sample.Point+Vector2.up*3))
                            .Take(actors.Length).ToArray();
                        for(int a=0;a<actors.Length;a++)actors[a].SetPosition(map.CellToWorldCenter(positions[a]));
                        new DungeonFogSystem().Update(floor,positions.Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))).ToArray());
                        map.ApplyFog(floor);Refresh(rig);
                        Call(map,"AdvanceForestKnowledge",1f);
                        foreach(var actor in actors)Call(actor,"LateUpdate");
                        foreach(var enemy in Read<DungeonEncounterSpawner>(bootstrap,"encounterSpawner").EnemyViews)
                            if(enemy!=null)Call(enemy,"LateUpdate");
                        map.CollectVisibleForestPassageCanopies(passage);
                        map.CollectVisibleForestForegroundCanopies(foreground);
                        var added=new HashSet<SpriteRenderer>(foreground.Except(passage));
                        Call(rig,"ResetForestCanopyFade");Call(rig,"RefreshForestPartyReveal");
                        var targets=Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets");
                        foreach(var tree in added)
                            if(targets.TryGetValue(tree,out var alpha)&&Mathf.Approximately(alpha,160f/255))targets.Remove(tree);
                        Call(rig,"AdvanceForestCanopyFade",.6f);var before=Capture(label+"-before");
                        Call(rig,"ResetForestCanopyFade");Call(rig,"RefreshForestPartyReveal");
                        Assert.That(rig.LastForestForegroundRevealCount,Is.GreaterThan(0));
                        Call(rig,"AdvanceForestCanopyFade",.1f);Capture(label+"-100ms");
                        Call(rig,"AdvanceForestCanopyFade",.1f);var after=Capture(label+"-after");
                        int changed=Enumerable.Range(0,before.Length).Count(i=>Math.Abs(before[i].r-after[i].r)>2
                            ||Math.Abs(before[i].g-after[i].g)>2||Math.Abs(before[i].b-after[i].b)>2);
                        floor.Map.TryGetRoomId(sample.Cell,out var room);
                        File.AppendAllText(Path.Combine(output,"production.txt"),$"{label}: cell={sample.Cell}; room={room}; template={Template(sample.Cell)}; overlapping southern crowns={sample.Count}; foreground-only crowns={added.Count}; changed pixels={changed}.\n");
                        Assert.That(added.Count,Is.GreaterThan(0));Assert.That(changed,Is.GreaterThan(100));
                        var mildTargets=Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets");
                        var mildTrees=mildTargets.Where(p=>Mathf.Approximately(p.Value,160f/255)).Select(p=>p.Key).ToArray();
                        foreach(var tree in mildTrees)mildTargets[tree]=130f/255;
                        Call(rig,"AdvanceForestCanopyFade",.5f);Capture(label+"-alpha-130");
                        foreach(var tree in mildTrees)mildTargets[tree]=160f/255;
                        Call(rig,"AdvanceForestCanopyFade",.5f);Capture(label+"-alpha-160");
                        // Also inspect the combined policy with an actual hero
                        // and current LOS, rather than only the isolated A/B.
                        actors[0].SetPosition(sample.Point);
                        var sources=new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(sample.Cell))};
                        new DungeonFogSystem().Update(floor,sources);map.ApplyFog(floor);
                        foreach(var actor in actors)Call(actor,"LateUpdate");Refresh(rig);
                        Call(rig,"RefreshForestPartyReveal");Call(rig,"AdvanceForestCanopyFade",.5f);
                        Capture(label+"-party-los");
                    }
                }
                finally{camera.targetTexture=oldTarget;camera.cullingMask=oldMask;RenderTexture.active=oldActive;Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Long);
        }

        [Test]
        public void ProductionActorOutsideNativeCrownDoesNotTriggerRectangleFade()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-canopy-contact-20261022"));
            Directory.CreateDirectory(output);
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=output,StageOverride="native-contact",SeedOverride=171026};
            fixture.FrameProbe=(bootstrap,session)=>
            {
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var floor=session.ActiveDungeon.Floors[0];var camera=Read<Camera>(rig,"worldCamera");
                var actors=Read<DungeonPartySpawner>(bootstrap,"partySpawner").PartyController.ActiveMemberViews.ToArray();
                var trees=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
                var shapes=Read<Dictionary<Sprite,(Vector2[] Vertices,ushort[] Triangles,Vector2[][] Outlines)>>(map,"forestCanopyShapes");
                var samples=trees.Keys.SelectMany(tree=>floor.Map.FloorCells
                    .Select(c=>(Tree:tree,Cell:c,Point:map.CellToWorldCenter(c)))
                    .Where(s=>tree.bounds.Contains(s.Point)&&s.Point.y>tree.transform.position.y
                        &&!map.ForestCanopyGroundBounds(tree).Contains(s.Point)
                        &&!map.ForestCanopyContainsPoint(tree,s.Point)))
                    .Where(s=>!floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                        .Any(o=>o.IsActive&&o.BlocksMovement&&o.OccupiesCell(s.Cell)))
                    .OrderBy(s=>s.Cell.y).ThenBy(s=>s.Cell.x).ToArray();
                Assert.That(samples,Is.Not.Empty,"Find a real walkable point inside a native sprite rectangle but outside its crown.");
                var sample=samples[0];var shape=shapes[sample.Tree.sprite];
                var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                var oldTarget=camera.targetTexture;var oldMask=camera.cullingMask;var oldActive=RenderTexture.active;
                Color32[] Capture(string name)
                {
                    foreach(var root in bootstrap.gameObject.scene.GetRootGameObjects())
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
                    SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();
                }
                void SelectActorOnly()
                {
                    Call(rig,"ResetForestCanopyFade");Call(rig,"RefreshForestPartyReveal");
                    var targets=Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets");
                    foreach(var tree in targets.Where(p=>Mathf.Approximately(p.Value,160f/255)).Select(p=>p.Key).ToArray())targets.Remove(tree);
                    Call(rig,"AdvanceForestCanopyFade",.4f);
                }
                try
                {
                    camera.targetTexture=target;camera.cullingMask=1<<30;camera.aspect=16f/9;camera.orthographicSize=4.5f;
                    actors[0].SetPosition(sample.Point);
                    for(int i=1;i<actors.Length;i++)actors[i].SetPosition(sample.Point+Vector2.right*30);
                    camera.transform.position=new Vector3(sample.Point.x,sample.Point.y+1.5f,-10);
                    new DungeonFogSystem().Update(floor,new[]{new DungeonTraversalPosition(new DungeonSurfacePosition(sample.Cell))});
                    map.ApplyFog(floor);foreach(var actor in actors)Call(actor,"LateUpdate");Refresh(rig);
                    shapes[sample.Tree.sprite]=(shape.Vertices,shape.Triangles,Array.Empty<Vector2[]>());
                    SelectActorOnly();var before=Capture("native-contact-old-rectangle");
                    var oldTargets=Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets");
                    Assert.That(oldTargets.ContainsKey(sample.Tree),Is.True,"Reproduce the previous rectangle false positive.");
                    shapes[sample.Tree.sprite]=shape;SelectActorOnly();var after=Capture("native-contact-outline");
                    Assert.That(Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets").ContainsKey(sample.Tree),Is.False,
                        "The real hero point lies outside the native crown, so it must stay solid.");
                    int changed=Enumerable.Range(0,before.Length).Count(i=>Math.Abs(before[i].r-after[i].r)>2
                        ||Math.Abs(before[i].g-after[i].g)>2||Math.Abs(before[i].b-after[i].b)>2);
                    File.WriteAllText(Path.Combine(output,"production-contact.txt"),$"Native sprite={sample.Tree.sprite.name}; hero cell={sample.Cell}; hero point={sample.Point}; rectangle falsely faded tree; imported outline leaves it solid; changed pixels={changed}. Route targets removed only from this preview to isolate actor contact.");
                    Assert.That(changed,Is.GreaterThan(100));
                    Call(rig,"RefreshForestPartyReveal");Call(rig,"AdvanceForestCanopyFade",.5f);
                    Capture("native-contact-with-passages");
                }
                finally
                {
                    shapes[sample.Tree.sprite]=shape;camera.targetTexture=oldTarget;camera.cullingMask=oldMask;RenderTexture.active=oldActive;
                    Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition",DungeonLength.Long);
        }

        [TestCase(127)]
        [TestCase(171026)]
        public void ProductionDiscoveryFilterAndCachedShadowsMatchFreshRendering(int seed)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../LocalHandoffs/forest-discovery-regression-20261022"));
            Directory.CreateDirectory(output);
            var fixture = new DungeonEntryPerformanceTests
            { OutputOverride = output, StageOverride = "discovery-" + seed, SeedOverride = seed };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var lighting = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var actors = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController.ActiveMemberViews.ToArray();
                var floor = session.ActiveDungeon.Floors[0];
                var camera = Read<Camera>(lighting, "worldCamera");
                var target = new RenderTexture(960, 540, 24);
                var pixels = new Texture2D(960, 540, TextureFormat.RGBA32, false);
                var oldTarget = camera.targetTexture; var oldMask = camera.cullingMask; var oldActive = RenderTexture.active;
                Color32[] Render(string label = null)
                {
                    foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                    SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                    if (label != null) File.WriteAllBytes(Path.Combine(output, seed + "-" + label + ".png"), pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30;
                    camera.aspect = 16f / 9; camera.orthographicSize = 5.5f;
                    new DungeonFogSystem().Update(floor, actors.Select(a => new DungeonTraversalPosition(
                        new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                    var visible = floor.Fog.Visible.ToArray();
                    map.ApplyFog(floor);
                    var initialMask = Read<Texture2D>(map, "forestKnowledgeTexture");
                    var initialOrigin = Read<Vector2Int>(map, "forestKnowledgeOrigin");
                    foreach (var position in visible)
                    {
                        floor.Map.TryGetTraversalCell(position, out var cell);
                        Assert.That(initialMask.GetPixel(cell.x - initialOrigin.x, cell.y - initialOrigin.y).a, Is.LessThan(.01),
                            "Line of sight outside the permanent discovery radius must remain clear.");
                    }
                    var origin = new Vector3(actors[0].Position.x, actors[0].Position.y + 1.5f, -10);
                    var report = new List<string>();
                    for (int frame = 0; frame < 16; frame++)
                    {
                        camera.transform.position = origin + new Vector3(Mathf.Sin(frame * .4f) * 3, frame * .07f);
                        floor.Fog.SetVisible(frame % 4 < 2 ? visible : visible.Take(1));
                        map.ApplyFog(floor);
                        Call(map, "TickFogAnimation", .2f);
                        actors[0].BodyRenderer.flipX = frame % 2 == 0;
                        typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame", Private).SetValue(lighting, int.MinValue);
                        Call(lighting, "RefreshLighting", frame * .2f);
                        var maskReader = typeof(DungeonLightingCadenceTests).GetMethod("ReadMask",
                            BindingFlags.NonPublic | BindingFlags.Static);
                        Color32[][] ReadMasks() => Enumerable.Range(0, lighting.ActiveTorchCount)
                            .Select(i => (Color32[])maskReader.Invoke(null, new object[] { Read<RenderTexture>(lighting, "mask"), i })).ToArray();
                        var cachedMasks = ReadMasks();
                        var cached = Render(frame % 4 == 0 ? "frame-" + frame : null);
                        typeof(DungeonSilhouetteLighting).GetField("dirtyShadowLights", Private).SetValue(lighting, 255);
                        typeof(DungeonSilhouetteLighting).GetField("dirtyStaticShadowLights", Private).SetValue(lighting, 255);
                        typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame", Private).SetValue(lighting, int.MinValue);
                        Call(lighting, "RefreshLighting", frame * .2f);
                        var fresh = Render("fresh-" + frame);
                        var freshMasks = ReadMasks();
                        var maskChanged = Enumerable.Range(0, cachedMasks.Length).Select(layer =>
                            Enumerable.Range(0, cachedMasks[layer].Length).Count(i =>
                                cachedMasks[layer][i].r != freshMasks[layer][i].r)).ToArray();
                        int changed = Enumerable.Range(0, fresh.Length).Count(i =>
                            Math.Abs(cached[i].r - fresh[i].r) > 2 || Math.Abs(cached[i].g - fresh[i].g) > 2
                            || Math.Abs(cached[i].b - fresh[i].b) > 2);
                        report.Add($"frame={frame} cacheMismatchPixels={changed} maskChanged={string.Join(",",maskChanged)} sources={lighting.ActiveTorchCount} casters={lighting.LastCasterCount}");
                        File.WriteAllLines(Path.Combine(output, seed + "-cache.txt"), report);
                        Assert.That(maskChanged.All(count => count == 0), Is.True,
                            "Cached shadow textures differ from fresh shadows at frame " + frame);
                        if (frame == 0)
                        {
                            int rebuilds = lighting.ShadowMaskRebuildCount;
                            float previousEnergy = Read<Vector4[]>(lighting, "lightData")[0].w;
                            Call(lighting, "RefreshLighting", .01f);
                            var inputs = Read<Vector4[]>(lighting, "lightData");
                            Assert.That(inputs[0].w, Is.Not.EqualTo(previousEnergy));
                            Assert.That(lighting.ShadowMaskRebuildCount, Is.EqualTo(rebuilds),
                                "Per-frame forest energy must not trigger shadow geometry work.");
                            var materialInputs = Read<Material>(lighting, "floorMaterial").GetVectorArray("_Torches");
                            Assert.That(materialInputs[0].w, Is.EqualTo(inputs[0].w),
                                "The receiver must get energy changes between shadow ticks.");
                        }
                    }
                    floor.Fog.SetVisible(visible); map.ApplyFog(floor); Refresh(lighting);
                    var filter = Read<SpriteRenderer>(map, "forestKnowledgeRenderer");
                    var mask = Read<Texture2D>(map, "forestKnowledgeTexture");
                    var maskOrigin = Read<Vector2Int>(map, "forestKnowledgeOrigin");
                    float Alpha(Vector2Int cell) => mask.GetPixel(cell.x-maskOrigin.x, cell.y-maskOrigin.y).a;
                    var knownCell = visible.Select(p => { floor.Map.TryGetTraversalCell(p, out var c); return c; }).First();
                    Assert.That(Alpha(knownCell), Is.LessThan(.01), "Visible ground must remain clear.");
                    var unknown = floor.Map.FloorCells.OrderByDescending(c => Alpha(c)).First();
                    Assert.That(Alpha(unknown), Is.GreaterThan(.99), "Distant unknown terrain must be masked.");
                    var at = map.CellToWorldCenter(unknown);
                    camera.transform.position = new Vector3(at.x, at.y, -10); Refresh(lighting);
                    var hidden = Render("unknown-masked");
                    var hiddenFilter = mask.GetPixels32();
                    floor.Fog.ToggleDebugFog(); map.ApplyFog(floor); Refresh(lighting);
                    Assert.That(filter.enabled, Is.False, "togglefog is the only debug bypass.");
                    var revealed = Render("unknown-togglefog");
                    Assert.That(revealed.Sum(c => (long)c.r+c.g+c.b), Is.GreaterThan(hidden.Sum(c => (long)c.r+c.g+c.b)));
                    floor.Fog.ToggleDebugFog(); map.ApplyFog(floor); Refresh(lighting);
                    Assert.That(filter.enabled, Is.True);
                    Render("unknown-restored");
                    Assert.That(mask.GetPixels32(), Is.EqualTo(hiddenFilter),
                        "Turning fog back on must restore the same discovery filter (independent of animated flames).");
                    var oldFilterTexture = mask;
                    map.Rebuild(floor.Map);
                    Assert.That(oldFilterTexture == null, Is.True, "Map teardown releases discovery textures.");
                }
                finally
                {
                    camera.targetTexture = oldTarget; camera.cullingMask = oldMask; RenderTexture.active = oldActive;
                    Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
        }

        [Test]
        public void ZeroBrightnessRevealNeverUploadsNanOrOpaqueBlack()
        {
            var root = new GameObject("Fog zero baseline");
            var child = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            child.transform.SetParent(root.transform, false);
            var map = root.AddComponent<DungeonFloorMapView>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            child.GetComponent<Tilemap>().SetTile(Vector3Int.zero, tile);
            child.GetComponent<Tilemap>().SetTileFlags(Vector3Int.zero, TileFlags.None);
            typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(map, child.GetComponent<Tilemap>());
            map.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonVegetationPresentation>(
                "Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
            try
            {
                var type = typeof(DungeonFloorMapView).GetNestedType("FogCellVisual", BindingFlags.NonPublic);
                var visual = Activator.CreateInstance(type, true);
                var fields = BindingFlags.Public | BindingFlags.Instance;
                type.GetField("TargetColor", fields).SetValue(visual, Color.white);
                type.GetField("TargetBaseline", fields).SetValue(visual, 0f);
                type.GetField("Transition", fields).SetValue(visual, new DungeonFogTransition(0, new float[81]));
                var colors = Read<List<Color>>(map, "fogColors");
                colors.AddRange(Enumerable.Repeat(Color.clear, 81));
                Call(map, "WriteFogCellColors", visual);
                Assert.That(colors.All(c => !float.IsNaN(c.a) && !float.IsInfinity(c.a)), Is.True);
                Assert.That(colors.All(c => c.a == 0), Is.True);
                Call(map, "ApplyFogCellColor", Vector2Int.zero, visual);
                var tileColor = child.GetComponent<Tilemap>().GetColor(Vector3Int.zero);
                Assert.That(float.IsNaN(tileColor.r) || float.IsInfinity(tileColor.r), Is.False);
                Assert.That(tileColor.a, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [TestCase(171026)]
        [TestCase(127)]
        public void ProductionForestKeepsContactsAcrossFogAndCameraChangesWithoutLeakingRoutes(int seed)
        {
            Directory.CreateDirectory(Output);
            var fixture = new DungeonEntryPerformanceTests
            {
                OutputOverride = Output, StageOverride = "stability-" + seed, SeedOverride = seed
            };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                File.WriteAllText(Path.Combine(Output, seed + "-frame-diagnostics.txt"), "");
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var lighting = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var actors = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController.ActiveMemberViews.ToArray();
                var floor = session.ActiveDungeon.Floors[0];
                var camera = Read<Camera>(lighting, "worldCamera");
                var tiles = Read<Tilemap>(map, "floorTilemap");
                foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                var target = new RenderTexture(1280, 720, 24);
                var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
                var oldTarget = camera.targetTexture; var oldMask = camera.cullingMask; var oldActive = RenderTexture.active;
                var report = new List<string>();
                Color32[] Render(string name = null)
                {
                    SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                    if (name != null) File.WriteAllBytes(Path.Combine(Output, seed + "-" + name + ".png"), pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30;
                    camera.aspect = 16f / 9; camera.orthographicSize = 5.5f;
                    new DungeonFogSystem().Update(floor, actors.Select(a => new DungeonTraversalPosition(
                        new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                    var visible = floor.Fog.Visible.ToArray();
                    var origin = new Vector3(actors[0].Position.x, actors[0].Position.y + 1.5f, -10);
                    for (int frame = 0; frame < 6; frame++)
                    {
                        // Keep an observer active while this camera area becomes
                        // remembered, matching an exploration FOV change.
                        floor.Fog.SetVisible(frame % 2 == 0 ? visible : visible.Take(1).ToArray());
                        map.ApplyFog(floor);
                        foreach (var actor in actors) Call(actor, "LateUpdate");
                        camera.transform.position = origin + new Vector3(frame * .13f, 0);
                        Refresh(lighting);
                        Call(lighting, "RefreshForestPartyReveal");
                        Call(lighting, "AdvanceForestCanopyFade", .5f);
                        var contacts = Read<object>(lighting, "contactShadows");
                        var contactRenderer = Read<MeshRenderer>(contacts, "renderer");
                        // The lighting rig creates its mesh/fixtures lazily, after
                        // the preview's initial layer isolation.
                        foreach (var renderer in lighting.GetComponentsInChildren<Renderer>(true))
                            renderer.gameObject.layer = 30;
                        var fog = Read<MeshRenderer>(map, "fogBlendRenderer");
                        Assert.That(contactRenderer.sortingOrder, Is.GreaterThan(fog.sortingOrder),
                            "Opaque forest fog must never overwrite contact shadows.");
                        var withShadow = Render("motion-" + frame);
                        contactRenderer.enabled = false;
                        var withoutShadow = Render();
                        contactRenderer.enabled = true;
                        int shadowPixels = Enumerable.Range(0, withShadow.Length).Count(i =>
                            withoutShadow[i].r + withoutShadow[i].g + withoutShadow[i].b
                            > withShadow[i].r + withShadow[i].g + withShadow[i].b + 2);
                        var elevation = Read<object>(map, "elevation");
                        File.AppendAllText(Path.Combine(Output, seed + "-frame-diagnostics.txt"),
                            $"frame={frame} pixels={shadowPixels} quads={lighting.ContactShadowQuadCount} projection={elevation.GetType().GetProperty("ProjectionLevel").GetValue(elevation)} bounds={contactRenderer.bounds} floor={tiles.GetComponent<TilemapRenderer>().sortingOrder} contacts={contactRenderer.sortingOrder} fog={fog.sortingOrder} active={contactRenderer.gameObject.activeInHierarchy}\n");
                        Assert.That(shadowPixels, Is.GreaterThan(10), "Contacts disappeared at frame " + frame);
                        report.Add($"frame={frame} shadowPixels={shadowPixels} quads={lighting.ContactShadowQuadCount} fogOrder={fog.sortingOrder} contactOrder={contactRenderer.sortingOrder}");
                        if (frame == 0)
                        {
                            int fixedOrder = contactRenderer.sortingOrder;
                            contactRenderer.sortingOrder = tiles.GetComponent<TilemapRenderer>().sortingOrder + 1;
                            var tied = Render("old-tied-shadow-order");
                            contactRenderer.sortingOrder = fixedOrder;
                            int erased = Enumerable.Range(0, tied.Length).Count(i =>
                                tied[i].r + tied[i].g + tied[i].b > withShadow[i].r + withShadow[i].g + withShadow[i].b + 2);
                            report.Add("Pixels losing shadow with previous tied order: " + erased);
                        }
                    }

                    floor.Fog.SetVisible(visible); map.ApplyFog(floor);
                    var unknown = floor.Map.FloorCells.Where(c => floor.Fog.Get(new DungeonTraversalPosition(
                        new DungeonSurfacePosition(c))) == DungeonKnowledge.Unexplored)
                        .OrderBy(c => ((Vector2)c - (Vector2)map.WorldToCell(actors[0].Position)).sqrMagnitude).First();
                    var at = map.CellToWorldCenter(unknown);
                    camera.transform.position = new Vector3(at.x, at.y, -10);
                    Refresh(lighting);
                    var backdrop = Read<SpriteRenderer>(map, "forestBackdrop");
                    Assert.That(backdrop, Is.Not.Null);
                    Assert.That(tiles.HasTile(new Vector3Int(unknown.x, unknown.y, 0)), Is.False,
                        "No route tile may be rendered on an undiscovered cell.");
                    var knowledge = floor.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(unknown)));
                    backdrop.enabled = false; Render("frontier-before");
                    backdrop.enabled = true; Render("frontier-after");
                    backdrop.gameObject.layer = 29; camera.cullingMask = 1 << 29;
                    var material = backdrop.sharedMaterial;
                    var oldTexture = material.GetTexture("_ForestSurfaceMask");
                    var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
                    try
                    {
                        mask.SetPixel(0, 0, new Color(0, .5f, .5f, 0)); mask.Apply();
                        material.SetTexture("_ForestSurfaceMask", mask);
                        var noRoutes = Render();
                        Assert.That(noRoutes[360 * 1280 + 640].r + noRoutes[360 * 1280 + 640].g
                            + noRoutes[360 * 1280 + 640].b, Is.GreaterThan(3), "Unknown litter cannot be an empty black hole.");
                        mask.SetPixel(0, 0, new Color(1, .5f, .5f, 1)); mask.Apply();
                        Assert.That(Render(), Is.EqualTo(noRoutes),
                            "Decorative ground must ignore path and contact channels, so it cannot leak hidden layout/objects.");
                        mask.SetPixel(0, 0, new Color(0, .5f, .5f, 1)); mask.Apply();
                        var plainLitter = Render("ground-reference");
                        var floorRenderer = tiles.GetComponent<TilemapRenderer>();
                        var fogRenderer = Read<MeshRenderer>(map, "fogBlendRenderer");
                        floorRenderer.gameObject.layer = 29; fogRenderer.gameObject.layer = 29;
                        var composite = Render("ground-continuity");
                        floorRenderer.gameObject.layer = 30; fogRenderer.gameObject.layer = 30;
                        int seams = Enumerable.Range(0, composite.Length).Count(i =>
                            Math.Abs(composite[i].r - plainLitter[i].r) > 2
                            || Math.Abs(composite[i].g - plainLitter[i].g) > 2
                            || Math.Abs(composite[i].b - plainLitter[i].b) > 2);
                        Assert.That(seams, Is.Zero,
                            "Without paths or contacts, known floor/fog and unknown litter must join without rectangular colour boundaries.");
                    }
                    finally
                    {
                        material.SetTexture("_ForestSurfaceMask", oldTexture); Object.DestroyImmediate(mask);
                        backdrop.gameObject.layer = 30; camera.cullingMask = 1 << 30;
                    }
                    Assert.That(floor.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(unknown))), Is.EqualTo(knowledge));
                    var retainedBackdrop = Read<SpriteRenderer>(map, "forestBackdrop");
                    Call(map, "BuildForestSurfaceMask");
                    Assert.That(Read<SpriteRenderer>(map, "forestBackdrop"), Is.SameAs(retainedBackdrop),
                        "Updating obstacles must reuse the backdrop, not accumulate large floor quads.");
                    map.Rebuild(floor.Map);
                    Assert.That(retainedBackdrop == null, Is.True, "Rebuilding the map must destroy the previous backdrop.");
                    Assert.That(Read<SpriteRenderer>(map, "forestBackdrop"), Is.Not.Null);
                    Assert.That(Read<SpriteRenderer>(map, "forestBackdrop").gameObject.layer, Is.EqualTo(tiles.gameObject.layer));
                    File.WriteAllLines(Path.Combine(Output, seed + "-measurements.txt"), report);
                }
                finally
                {
                    camera.targetTexture = oldTarget; camera.cullingMask = oldMask; RenderTexture.active = oldActive;
                    Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
                }
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", DungeonLength.Long);
        }
    }
}
