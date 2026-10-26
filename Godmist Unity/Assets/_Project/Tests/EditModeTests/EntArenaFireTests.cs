using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Tests.EditMode
{
    public sealed class EntArenaFireTests
    {
        [TestCase(false)] [TestCase(true)]
        public void NativeTreeFlamesLeaveMostBarkAndLeavesVisibleAndRespectFogTint(bool flip)
        {
            var root = new GameObject("Native burning tree preview");
            var cameraObject = new GameObject("Preview camera", typeof(Camera));
            var material = new Material(Resources.Load<Shader>("Godmist/EntArenaFire"));
            var target = new RenderTexture(256, 256, 24);
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
                var tree = root.AddComponent<SpriteRenderer>(); tree.sprite = definition.VegetationPresentation.BoundaryTree;
                tree.color = definition.VegetationPresentation.TreeTint; tree.flipX = flip;
                var node = new GameObject("Fire", typeof(SpriteRenderer)); node.transform.SetParent(root.transform, false);
                var fire = node.GetComponent<SpriteRenderer>(); fire.sprite = tree.sprite; fire.flipX = flip;
                fire.sharedMaterial = material; fire.sortingOrder = tree.sortingOrder + 1;
                var b = tree.sprite.bounds; var properties = new MaterialPropertyBlock();
                properties.SetVector("_SpriteBounds", new Vector4(b.min.x, b.min.y, b.size.x, b.size.y));
                properties.SetFloat("_Phase", 1.9f); fire.SetPropertyBlock(properties);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(b.extents.x,b.extents.y)+.1f; camera.aspect = 1;
                camera.transform.position = b.center + Vector3.back * 10;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                var output = System.Environment.GetEnvironmentVariable("GODMIST_TEST_ARTIFACTS");
                Color[] Read(string name)
                {
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                    if (!string.IsNullOrEmpty(output))
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+(flip?"-flipped":"")+".png"), pixels.EncodeToPNG());
                    return pixels.GetPixels().Select(c => c.linear).ToArray();
                }
                fire.enabled = false; var baseline = Read("ent-tree-baseline");
                fire.enabled = true; var burning = Read("ent-tree-burning");
                fire.color = new Color(.55f,.55f,.55f,1); var dim = Read("ent-tree-remembered");
                var occupied = 0; var changed = 0; float lift = 0, dimLift = 0;
                for (var i = 0; i < baseline.Length; i++)
                {
                    if (baseline[i].maxColorComponent < .002f) continue;
                    occupied++;
                    var delta = Mathf.Max(0,burning[i].g-baseline[i].g);
                    if (delta > .005f) changed++;
                    lift += delta; dimLift += Mathf.Max(0,dim[i].g-baseline[i].g);
                    Assert.That(burning[i].g, Is.LessThan(.65f), "No broad neon-green replacement of the crown.");
                }
                Assert.That(changed, Is.GreaterThan(5), "Fire must remain visible on native tree art.");
                Assert.That(changed, Is.LessThan(occupied*.3f), "Most original bark/leaf pixels must remain readable.");
                Assert.That(dimLift, Is.LessThan(lift*.8f), "Sprite fog tint must attenuate the flame too.");
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void GreenFireFollowsPhaseAndArenaWithoutChangingCollisionOrLightingCasters()
        {
            var root = new GameObject("Arena fire test", typeof(Grid));
            var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            tiles.transform.SetParent(root.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
                var map = new DungeonFloorMap();
                for (var x = 0; x < 12; x++) for (var y = 0; y < 5; y++)
                    map.TryAddFloorCell(new Vector2Int(x,y), x < 6 ? "arena" : "other");
                foreach(var cell in map.FloorCells.ToArray())
                    foreach(var direction in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                        if(!map.IsWalkable(cell+direction)) map.TryAddWall(cell,cell+direction);
                var view = root.AddComponent<DungeonFloorMapView>();
                void Set(string field, object value) => typeof(DungeonFloorMapView)
                    .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, value);
                Set("floorTilemap", tiles.GetComponent<Tilemap>()); Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var arenaFloor=new DungeonFloorInstance(0); arenaFloor.TrySetMap(map);
                arenaFloor.TryAddRoom(new DungeonRoomInstance("arena",DungeonRoomType.Final,"room.forest.ent_arena"));
                view.ConfigureForestPresentationFloor(arenaFloor);
                view.ConfigureVegetationPresentation(definition.VegetationPresentation); view.Rebuild(map);
                var trees=root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Boundary vegetation").ToArray();
                var grounds=root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Forest floor under vegetation").ToArray();
                var footprints = (Dictionary<SpriteRenderer, RectInt>)typeof(DungeonFloorMapView)
                    .GetField("forestTreeFootprints", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                foreach(var tree in trees)
                    foreach (var cell in footprints[tree].allPositionsWithin)
                    {
                        // Shared soil covers grounded root cells; leaf pixels
                        // can extend vertically across multiple world cells.
                        var point = (Vector3)view.CellToWorldCenter(cell);
                        Assert.That(grounds.Any(g => g.bounds.Contains(point)), Is.True,
                            "Shared soil must remain continuous beneath occupied roots.");
                    }
                var revision = map.TraversalRevision;
                var casterCount = view.LightingWallRenderers.Count();
                var colliderCount = root.GetComponentsInChildren<Collider2D>(true).Length;
                var ent = AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>(ForestEnemiesSetup.EntPath);
                var unit = new BattleUnit { ForestEntState = new ForestEntCombatState { OriginalDefinition = ent, ArenaRoomId = "arena" } };
                var context = new DungeonCombatContext("forest",0,"ent","arena",Vector2Int.zero,Vector2Int.one,
                    map.FloorCells, new DungeonCellEdge[0], new DungeonCombatMapObjectSnapshot[0],surfaceMap:map);
                var combat = new CombatSession { DungeonContext=context, Units=new List<BattleUnit>{unit} };
                void Refresh() { Set("nextEntFireRefresh",0f); view.RefreshEntArenaFire(new[]{combat}); }
                Refresh(); Assert.That(view.BurningArenaTreeCount,Is.Zero);
                unit.ForestEntState.PhaseTwo=true; Refresh();
                var burning=view.BurningArenaTreeCount;
                Assert.That(burning,Is.GreaterThan(0));
                Assert.That(view.ArenaFireLightSources().Count(),Is.EqualTo(burning));
                var lighting=root.AddComponent<DungeonSilhouetteLighting>();
                typeof(DungeonSilhouetteLighting).GetField("terrain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lighting,view);
                typeof(DungeonSilhouetteLighting).GetField("cameraGroundBounds",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(lighting,new Rect(-20,-20,50,50));
                var select=typeof(DungeonSilhouetteLighting).GetMethod("SelectArenaFireLights",BindingFlags.NonPublic|BindingFlags.Instance);
                select.Invoke(lighting,new object[]{new Vector2(3,2)});
                Assert.That(lighting.ActiveArenaFireLightCount,Is.InRange(1,8),"Arena fire shares the existing bounded light budget.");
                typeof(DungeonSilhouetteLighting).GetMethod("WriteLightInputs",BindingFlags.NonPublic|BindingFlags.Instance)
                    .Invoke(lighting,new object[]{0f});
                var lightColors=(Vector4[])typeof(DungeonSilhouetteLighting).GetField("lightColors",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(lighting);
                var lightData=(Vector4[])typeof(DungeonSilhouetteLighting).GetField("lightData",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(lighting);
                for(var i=0;i<lighting.ActiveArenaFireLightCount;i++)
                {
                    Assert.That(lightColors[i],Is.EqualTo((Vector4)DungeonFloorMapView.ArenaFireLightColor));
                    Assert.That(lightData[i].z,Is.GreaterThan(5)); Assert.That(lightData[i].w,Is.GreaterThan(.8f));
                    for(var j=0;j<i;j++)
                        Assert.That(Vector2.Distance(lightData[i],lightData[j]),Is.GreaterThanOrEqualTo(3f),
                            "Overlapping trees must not spend every light slot at the same edge.");
                }
                var fireMaterial=root.GetComponentsInChildren<SpriteRenderer>().First(r=>view.IsArenaFireRenderer(r)).sharedMaterial;
                Assert.That(Vector4.Distance(fireMaterial.GetColor("_FireCore"),DungeonFloorMapView.ArenaFireCore),Is.LessThan(.001f));
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r=>view.IsArenaFireRenderer(r)),Is.EqualTo(burning));
                var walls=(List<(DungeonSurfaceEdge Edge,DungeonWallSegmentView View,Renderer[] Renderers)>)typeof(DungeonFloorMapView)
                    .GetField("wallRenderers",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
                foreach(var wall in walls.Where(w=>w.Renderers.Any(r=>view.IsArenaFireRenderer(r))))
                    Assert.That(map.TryGetRoomId(wall.Edge.FirstCell,out var first)&&first=="arena"
                        ||map.TryGetRoomId(wall.Edge.SecondCell,out var second)&&second=="arena",Is.True);
                var owners = (Dictionary<Renderer, DungeonSurfaceEdge[]>)typeof(DungeonFloorMapView)
                    .GetField("forestBoundaryOwners", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                foreach(var wall in walls)
                    Assert.That(owners.Any(pair => pair.Key is SpriteRenderer canopy && view.IsForestCanopyRenderer(canopy)
                        && pair.Value.Contains(wall.Edge)), Is.True,
                        "Shared woodland canopies must retain the visibility owner of every hard boundary edge: " + wall.Edge);
                var output=System.Environment.GetEnvironmentVariable("GODMIST_TEST_ARTIFACTS");
                if(!string.IsNullOrEmpty(output))
                {
                    var cameraObject=new GameObject("Fire preview",typeof(Camera));
                    var target=new RenderTexture(640,480,24);
                    var pixels=new Texture2D(640,480,TextureFormat.RGBA32,false);
                    var groundMaterial=new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
                    var previous=RenderTexture.active;
                    try
                    {
                        var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=8;
                        camera.transform.position=new Vector3(6,3,-10);camera.clearFlags=CameraClearFlags.SolidColor;
                        camera.backgroundColor=new Color(.035f,.04f,.03f);camera.targetTexture=target;camera.Render();
                        RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"ent-arena-fire-preview.png"),pixels.EncodeToPNG());
                        view.ConfigureForestGroundMaterial(groundMaterial); groundMaterial.SetFloat("_ReceiveShadows",0);
                        tiles.GetComponent<TilemapRenderer>().sharedMaterial=groundMaterial;
                        foreach(var ground in grounds) ground.sharedMaterial=groundMaterial;
                        var updateMaterial=typeof(DungeonSilhouetteLighting).GetMethod("UpdateGroundMaterial",BindingFlags.NonPublic|BindingFlags.Instance);
                        updateMaterial.Invoke(lighting,new object[]{groundMaterial,50f});
                        groundMaterial.SetInt("_TorchCount",0);
                        camera.Render();pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();
                        var dark=pixels.GetPixels();
                        updateMaterial.Invoke(lighting,new object[]{groundMaterial,50f});
                        Assert.That(groundMaterial.GetInt("_TorchCount"),Is.EqualTo(lighting.ActiveArenaFireLightCount));
                        camera.Render();pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();
                        var lit=pixels.GetPixels(); var illuminated=0;
                        for(var i=0;i<lit.Length;i++)if(lit[i].g>dark[i].g+.015f)illuminated++;
                        Assert.That(illuminated,Is.GreaterThan(100),"Flames must illuminate real forest terrain, not just animate their own pixels.");
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"ent-arena-lit-preview.png"),pixels.EncodeToPNG());
                    }
                    finally {RenderTexture.active=previous;Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(groundMaterial);}
                }
                Assert.That(view.LightingWallRenderers.Count(),Is.EqualTo(casterCount));
                Assert.That(root.GetComponentsInChildren<Collider2D>(true).Length,Is.EqualTo(colliderCount));
                Assert.That(map.TraversalRevision,Is.EqualTo(revision));
                var shader=Resources.Load<Shader>("Godmist/EntArenaFire");
                Assert.That(shader,Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(shader),Is.False);
                var floor=new DungeonFloorInstance(0);Assert.That(floor.TrySetMap(map),Is.True);
                view.ApplyFog(floor);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r=>view.IsArenaFireRenderer(r))
                    .All(r=>r.forceRenderingOff),Is.True,"Fire cannot reveal unexplored trees.");
                Assert.That(view.ArenaFireLightSources(),Is.Empty,"Unexplored arena fire cannot leak light.");
                floor.Fog.ToggleDebugFog();view.ApplyFog(floor);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r=>view.IsArenaFireRenderer(r))
                    .Any(r=>!r.forceRenderingOff),Is.True);
                Refresh(); Assert.That(view.BurningArenaTreeCount,Is.EqualTo(burning));
                combat.Outcome=CombatOutcomeState.Victory; Refresh();
                Assert.That(view.BurningArenaTreeCount,Is.Zero);
                Assert.That(view.ArenaFireLightSources(),Is.Empty);
                combat.Outcome=CombatOutcomeState.Ongoing; Refresh();
                Assert.That(view.BurningArenaTreeCount,Is.EqualTo(burning));
                // A map rebuild releases the previous arena overlay and its shared material.
                view.Rebuild(map); Refresh();
                Assert.That(view.BurningArenaTreeCount,Is.EqualTo(burning));
                view.ConfigureVegetationPresentation(null); Refresh();
                Assert.That(view.BurningArenaTreeCount,Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }
    }
}
