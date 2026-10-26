using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Combat;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Godmist.Views.Exploration;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestImportedAssetsTests
    {
        [Test]
        public void ShippedAssetsPlaceForestPropsWithoutRunningTheAuthoringTool()
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.DoesNotThrow(() => DungeonObjectPlacementSystem.ValidateContentDefinitions(definition));
            var dungeon = new DungeonGeneratorService().Generate(definition, 42);
            new DungeonObjectPlacementSystem().Place(dungeon);
            Assert.That(dungeon.Floors.Single().Objects.OfType<ObstacleDungeonObjectInstance>().Count(), Is.GreaterThan(2));
            Assert.That(dungeon.Floors.SelectMany(f => f.Objects).Any(o => o.Definition.Id == "object.forest.tree.prototype"), Is.False);
        }

        [Test]
        public void PrototypeEntryReloadsImportedPoolWithoutReauthoringAssets()
        {
            var path = ForestPrototypeSetup.DefinitionPath;
            var before = File.ReadAllText(path);
            // Reproduce an Inspector-held definition that predates the external pool update.
            AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path).ObjectDefinitions.Clear();
            var definition = ForestPrototypeSetup.LoadImportedPrototype();
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            var tree = definition.ObjectDefinitions.OfType<ObstacleDungeonObjectDefinition>().Single(d => d.Id == "object.forest.tree.prototype");
            Assert.That(tree.Id, Is.EqualTo("object.forest.tree.prototype"));
            var dungeon = new DungeonGeneratorService().Generate(definition, 42);
            Assert.DoesNotThrow(() => new DungeonObjectPlacementSystem().Place(dungeon));
        }
    }

    public sealed class ForestPrototypeTests
    {
        private DungeonDefinition definition;
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        [OneTimeSetUp]
        public void AuthorAssets()
        {
            ForestPrototypeSetup.Configure();
            definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
        }

        [Test]
        public void InteriorTreesKeepNewCrownsAndReserveTwoByTwoAtNativePixelScale()
        {
            var tree=definition.ObjectDefinitions.OfType<ObstacleDungeonObjectDefinition>().Single(d=>d.Id=="object.forest.tree.prototype");
            var sprites=new[]{tree.PresentationSprite}.Concat(tree.PresentationVariants).ToArray();
            Assert.That(sprites.All(s=>s!=null && s.name.StartsWith("forest-tree-mature-") && s.pixelsPerUnit==32),Is.True,
                "Keep the new art and give it space rather than substituting old small trees.");
            Assert.That(tree.PlacementReservationSizeInCells,Is.EqualTo(new Vector2Int(2,2)));
            Assert.That(tree.NativePixelScale,Is.True);
            Assert.That(tree.TrunkRadius,Is.EqualTo(.18f));
            Assert.That(tree.BlocksMovement && tree.BlocksLineOfSight,Is.True);
        }

        [TestCase("object.forest.tree.prototype", .18f, 1.5f, true)]
        [TestCase("object.forest.boulder.prototype", .32f, 1.4f, true)]
        [TestCase("object.forest.stump.prototype", .22f, 1.2f, false)]
        [TestCase("object.forest.ruined_pillar.prototype", .3f, 1.7f, true)]
        [TestCase("object.forest.cold_campfire.prototype", .26f, 1.7f, false)]
        [TestCase("object.forest.broken_crate.prototype", .28f, 1.25f, false)]
        [TestCase("object.forest.gravestone.prototype", .2f, 1f, false)]
        public void InteriorObstaclePrefabUsesGroundedBaseInsteadOfItsOldBox(string id, float radius, float width, bool blocksSight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonObstacle.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var tree = definition.ObjectDefinitions.OfType<ObstacleDungeonObjectDefinition>().Single(d => d.Id == id);
                Assert.That(tree.BlocksLineOfSight, Is.EqualTo(blocksSight));
                var instance = new ObstacleDungeonObjectInstance("tree.test", tree, Vector2Int.zero);
                var view = root.GetComponent<DungeonObstacleView>();
                view.Initialize(instance, Vector3.zero);
                view.Initialize(instance, Vector3.zero);
                Physics2D.SyncTransforms();
                var collider = root.GetComponentsInChildren<Collider2D>().Single(c => c.enabled);
                var large = instance.OccupiedSizeInCells == new Vector2Int(2,2);
                Assert.That(collider, large ? Is.TypeOf<BoxCollider2D>() : Is.TypeOf<CircleCollider2D>());
                Assert.That(collider.bounds.size.x, Is.EqualTo(large ? 2 : radius * 2).Within(.001f));
                Assert.That(collider.bounds.min.y, Is.EqualTo(large ? -.5f : 0).Within(.001f));
                if (large)
                {
                    Assert.That(collider.bounds.size.y,Is.EqualTo(2).Within(.001f));
                    foreach(var cell in instance.EnumerateOccupiedCells())
                        Assert.That(collider.bounds.Contains((Vector3)(Vector2)cell),Is.True,"Every occupied cell centre lies inside the collider.");
                }
                var art = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled);
                Assert.That(new[] { tree.PresentationSprite }.Concat(tree.PresentationVariants), Does.Contain(art.sprite));
                Assert.That(tree.NativePixelScale, Is.True);
                Assert.That(art.sprite.pixelsPerUnit, Is.EqualTo(32));
                Assert.That(art.transform.lossyScale.x, Is.EqualTo(1).Within(.0001f));
                Assert.That(art.transform.lossyScale.y, Is.EqualTo(1).Within(.0001f));
                Assert.That(art.bounds.size.x, Is.EqualTo(art.sprite.rect.width / 32f).Within(.001f));
                var initialScale = art.transform.localScale;
                var initialColor = art.color;
                view.Initialize(instance, Vector3.zero);
                Assert.That(art.transform.localScale, Is.EqualTo(initialScale));
                Assert.That(art.color, Is.EqualTo(initialColor));
                Assert.That(art.transform.position.y, Is.EqualTo(large ? -.5f : 0).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("object.forest.stump.prototype",40,40)]
        [TestCase("object.forest.boulder.prototype",48,36)]
        public void PropVariantsStayDeterministicAndKeepTheSameCollision(string id, int width, int height)
        {
            var prop = definition.ObjectDefinitions.OfType<ObstacleDungeonObjectDefinition>().Single(d => d.Id == id);
            Assert.That(prop.PresentationVariants.Length, Is.EqualTo(2));
            foreach (var alternate in prop.PresentationVariants)
            {
                Assert.That(alternate.rect.size, Is.EqualTo(new Vector2(width,height)));
                Assert.That(alternate.pixelsPerUnit, Is.EqualTo(32));
            }
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonObstacle.prefab"));
            try
            {
                var choices = new List<Sprite>();
                for (var x = 0; x < 36; x++)
                {
                    var instance = new ObstacleDungeonObjectInstance("stump.variant", prop, new Vector2Int(x,0));
                    var view = root.GetComponent<DungeonObstacleView>();
                    view.Initialize(instance, Vector3.zero);
                    var art = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled);
                    choices.Add(art.sprite);
                    Assert.That(art.transform.lossyScale.x, Is.EqualTo(1).Within(.0001f));
                    Assert.That(art.transform.lossyScale.y, Is.EqualTo(1).Within(.0001f));
                    Physics2D.SyncTransforms();
                    Assert.That(root.GetComponentsInChildren<CircleCollider2D>().Single(c => c.enabled).bounds.size.x, Is.EqualTo(prop.TrunkRadius * 2).Within(.0001f));
                    view.Initialize(instance, Vector3.zero);
                    Assert.That(art.sprite, Is.SameAs(choices.Last()));
                }
                foreach (var expected in new[] { prop.PresentationSprite }.Concat(prop.PresentationVariants))
                    Assert.That(choices, Does.Contain(expected));
                Assert.That(choices.Distinct().Count(), Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ForestDetailVariantsHaveNativeDensityAndSafeFallbacks()
        {
            var profile = definition.VegetationPresentation;
            var specs = new[] { ("WoodlandFungi",40,28), ("WoodlandFern",40,27), ("ExposedRoots",40,20), ("Deadwood",40,20), ("FallenLog",64,24), ("SmallStone",24,18), ("LowShrub",40,18), ("GroundCover",28,12) };
            foreach (var spec in specs)
            {
                var original = (Sprite)typeof(DungeonVegetationPresentation).GetProperty(spec.Item1).GetValue(profile);
                var variants = (Sprite[])typeof(DungeonVegetationPresentation).GetProperty(spec.Item1+"Variants").GetValue(profile);
                Assert.That(variants.Length, Is.EqualTo(1), spec.Item1);
                Assert.That(variants[0], Is.Not.Null, spec.Item1);
                Assert.That(variants[0].rect.size, Is.EqualTo(new Vector2(spec.Item2,spec.Item3)), spec.Item1);
                Assert.That(variants[0].pixelsPerUnit, Is.EqualTo(32), spec.Item1);
                Assert.That(variants[0].texture.filterMode, Is.EqualTo(FilterMode.Point), spec.Item1);
                var choices = Enumerable.Range(0,128).Select(n => DungeonVegetationPresentation.SelectDetailSprite(original,variants,(uint)n)).ToArray();
                Assert.That(choices, Does.Contain(original));
                Assert.That(choices, Does.Contain(variants[0]));
                Assert.That(choices.All(s => s == original || s == variants[0]), Is.True);
                Assert.That(DungeonVegetationPresentation.SelectDetailSprite(original,null,12), Is.SameAs(original));
                Assert.That(DungeonVegetationPresentation.SelectDetailSprite(original,System.Array.Empty<Sprite>(),12), Is.SameAs(original));
                Assert.That(Enumerable.Range(0,16).All(n => DungeonVegetationPresentation.SelectDetailSprite(original,new Sprite[1],(uint)n)==original), Is.True);
            }
        }

        [Test]
        public void BedrollKeepsNativeScaleWithoutBlockingCharacters()
        {
            var prop = definition.ObjectDefinitions.OfType<ObstacleDungeonObjectDefinition>().Single(d => d.Id == "object.forest.bedroll.prototype");
            Assert.That(prop.BlocksMovement || prop.BlocksLineOfSight, Is.False);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonObstacle.prefab"));
            try
            {
                root.GetComponent<DungeonObstacleView>().Initialize(new ObstacleDungeonObjectInstance("bedroll.test", prop, Vector2Int.zero), Vector3.zero);
                Assert.That(root.GetComponent<DungeonObstacleView>().IsGroundPresentation, Is.True);
                Assert.That(root.GetComponentsInChildren<Collider2D>().Any(c => c.enabled), Is.False);
                var art = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled);
                Assert.That(art.sprite.rect.size, Is.EqualTo(new Vector2(40,48)));
                Assert.That(art.sprite.pixelsPerUnit, Is.EqualTo(32));
                Assert.That(art.transform.lossyScale.x, Is.EqualTo(1).Within(.0001f));
                Assert.That(art.transform.lossyScale.y, Is.EqualTo(1).Within(.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HollowTreeVariantIsRareAndKeepsNativePixelDensity()
        {
            var presentation = definition.VegetationPresentation;
            Assert.That(presentation.TreeVariants.Length, Is.EqualTo(6));
            var hollow = presentation.TreeVariants[2];
            Assert.That(hollow, Is.Not.Null);
            Assert.That(hollow.pixelsPerUnit, Is.EqualTo(32));
            Assert.That(hollow.rect.size, Is.EqualTo(new Vector2(160,192)));
            Assert.That(Enumerable.Range(0,72).Count(n => DungeonVegetationPresentation.SelectTreeSprite(presentation.BoundaryTree,presentation.TreeVariants,(uint)n) == hollow), Is.EqualTo(17));
            Assert.That(DungeonVegetationPresentation.SelectTreeSprite(presentation.BoundaryTree,presentation.TreeVariants.Take(2).ToArray(),12), Is.SameAs(presentation.BoundaryTree));
            Assert.That(Enumerable.Range(0,72).Count(n => DungeonVegetationPresentation.SelectTreeSprite(presentation.BoundaryTree,presentation.TreeVariants,(uint)n) == presentation.TreeVariants[5]), Is.EqualTo(1));
        }

        [Test]
        public void TreeApproachUsesOneColliderClearanceFromAllFourSides()
        {
            var root = new GameObject("Tree approach regression", typeof(Grid));
            var treeObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Dungeon/PF_DungeonObstacle.prefab"), root.transform);
            try
            {
                var tiles = new GameObject("Floor", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var mapView = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(mapView, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var y = 0; y < 5; y++) for (var x = 0; x < 5; x++)
                    map.TryAddFloorCell(new Vector2Int(x, y), "glade");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var tree = definition.ObjectDefinitions.OfType<ObstacleDungeonObjectDefinition>().Single(d => d.Id == "object.forest.tree.prototype");
                var instance = new ObstacleDungeonObjectInstance("approach.tree", tree, new Vector2Int(1, 1));
                floor.TryAddObject(instance);
                treeObject.GetComponent<DungeonObstacleView>().Initialize(instance, mapView.CellToWorldCenter(instance.Position));
                var trunk = treeObject.GetComponentsInChildren<BoxCollider2D>().Single(c => c.enabled);
                var actorObject = new GameObject("Actor", typeof(CircleCollider2D)); actorObject.transform.SetParent(root.transform);
                var collider = actorObject.GetComponent<CircleCollider2D>(); collider.radius = .1f; collider.offset = Vector2.up * .07f;
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(actor, collider);
                actor.ConfigureTerrain(map, mapView, floor);
                var controller = root.AddComponent<ExplorationPartyController>(); controller.ConfigureTraversal(floor, mapView, null);
                var direct = typeof(ExplorationPartyController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(m => m.Name == "CanNavigateDirectly" && m.GetParameters().Length == 5
                        && m.GetParameters()[2].ParameterType == typeof(Vector2));
                foreach (var direction in Directions)
                {
                    var center = (Vector2)trunk.bounds.center;
                    var from = center + (Vector2)direction * 1.8f - collider.offset;
                    var target = center + (Vector2)direction * 1.2f - collider.offset;
                    actor.SetPosition(from); Physics2D.SyncTransforms();
                    var start = new DungeonTraversalPosition(new DungeonSurfacePosition(mapView.WorldToCell(from)));
                    var end = new DungeonTraversalPosition(new DungeonSurfacePosition(mapView.WorldToCell(target)));
                    Assert.That(actor.HasClearMovementSegment(from, target), Is.True, direction.ToString());
                    Assert.That(direct.Invoke(controller, new object[] { actor, start, from, end, target }), Is.True,
                        "Clear physical approach must not acquire a second actor radius: " + direction);
                    var inside = center + (Vector2)direction * .95f - collider.offset;
                    Assert.That(actor.HasClearMovementSegment(from, inside), Is.False, "Trunk must still block: " + direction);
                }
                var beside = (Vector2)trunk.bounds.center + Vector2.right * 1.2f - collider.offset;
                var goal = beside + Vector2.right;
                actor.SetPosition(beside); Physics2D.SyncTransforms();
                var destination = new DungeonTraversalPosition(new DungeonSurfacePosition(mapView.WorldToCell(goal)));
                var arguments = new object[] { actor, destination, null, goal };
                Assert.That(typeof(ExplorationPartyController).GetMethod("FindTraversalPathWithTargetWorld",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, arguments), Is.True);
                var routes = (System.Collections.IDictionary)typeof(ExplorationPartyController).GetField("meshRoutes",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                Assert.That(routes.Contains(actor), Is.True,
                    "Standing beside the full 2x2 footprint must retain native routing.");
                controller.ConfigureTraversal(null, null, null);
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void ForestFogFeathersFurtherInsideKnownGroundWithoutRevealingUnknown()
        {
            var neighbours = Enumerable.Repeat(DungeonKnowledge.Visible, 8).ToArray();
            neighbours[4] = DungeonKnowledge.Unexplored;
            var normal = new float[81]; var forest = new float[81];
            DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Visible, neighbours, normal);
            DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Visible, neighbours, forest, true);
            Assert.That(forest[4 * 9 + 4], Is.LessThan(normal[4 * 9 + 4]));
            DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Unexplored, neighbours, forest, true);
            Assert.That(forest.All(v => v == 0), Is.True);
        }

        [Test]
        public void ForestPathsRenderAcrossRoomConnectionsWithoutChangingTraversal()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Requires GPU");
            var root = new GameObject("Forest paths render", typeof(Grid));
            var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform); tiles.layer = 31;
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var tile = ScriptableObject.CreateInstance<Tile>();
            var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var target = new RenderTexture(768, 576, 24);
            var pixels = new Texture2D(768, 576, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                void Set(string name, object value) => typeof(DungeonFloorMapView)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
                Set("floorTilemap", tiles.GetComponent<Tilemap>()); Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap();
                for (var x = 0; x < 16; x++) for (var y = 0; y < 12; y++)
                    map.TryAddFloorCell(new Vector2Int(x, y), x < 5 ? "west" : x > 10 ? "east" : "glade");
                var revision = map.TraversalRevision;
                view.ConfigureVegetationPresentation(definition.VegetationPresentation); view.Rebuild(map);
                view.ConfigureForestGroundMaterial(material);
                Assert.That(material.GetFloat("_ForestSurfaceEnabled"), Is.EqualTo(1));
                Assert.That(material.GetTexture("_ForestSurfaceMask"), Is.Not.Null);
                Assert.That(map.TraversalRevision, Is.EqualTo(revision));
                var mask = material.GetTexture("_ForestSurfaceMask");
                view.SetPresentationOccupancy(new[] { new Vector2Int(2, 2) });
                view.ConfigureForestGroundMaterial(material);
                Assert.That(material.GetTexture("_ForestSurfaceMask"), Is.SameAs(mask), "Occupancy must not rebake visual paths.");
                view.SetForestPathObstacles(new[] { new Vector2Int(7, 5), new Vector2Int(8, 5) });
                view.ConfigureForestGroundMaterial(material);
                Assert.That(material.GetTexture("_ForestSurfaceMask"), Is.Not.SameAs(mask));
                mask = material.GetTexture("_ForestSurfaceMask");
                view.SetForestPathObstacles(new[] { new Vector2Int(8, 5), new Vector2Int(7, 5) });
                view.ConfigureForestGroundMaterial(material);
                Assert.That(material.GetTexture("_ForestSurfaceMask"), Is.SameAs(mask), "Unchanged obstacles must not rebake.");
                // Inspect the actual GPU mask: a tree's occupied cell cannot contain exposed path soil.
                var maskBounds = material.GetVector("_ForestSurfaceBounds");
                var maskTarget = RenderTexture.GetTemporary(mask.width, mask.height, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                var maskPixels = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false, true);
                try
                {
                    Graphics.Blit(mask, maskTarget); RenderTexture.active = maskTarget;
                    maskPixels.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0); maskPixels.Apply();
                    foreach (var blocked in new[] { new Vector2Int(7, 5), new Vector2Int(8, 5) })
                    {
                        var world = view.CellToWorldCenter(blocked);
                        var x = Mathf.FloorToInt((world.x - maskBounds.x) / maskBounds.z * mask.width);
                        var y = Mathf.FloorToInt((world.y - maskBounds.y) / maskBounds.w * mask.height);
                        Assert.That(maskPixels.GetPixel(x, y).r, Is.LessThan(.01f), "Path crosses a tree: " + blocked);
                        Assert.That(maskPixels.GetPixel(x, y).a, Is.InRange(.69f, .85f), "Base must have a subtle contact shade: " + blocked);
                    }
                    Assert.That(maskPixels.GetPixels32().Count(c => c.a < 250), Is.InRange(1, mask.width * mask.height / 20),
                        "Contact shade must remain local to obstacle bases.");
                    Assert.That(maskPixels.GetPixels32().Count(c => c.r > 128), Is.InRange(10, mask.width * mask.height / 8),
                        "Paths must remain narrow rather than cover the glade.");
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(maskTarget); Object.DestroyImmediate(maskPixels); }
                material.SetFloat("_Ambient", 1); material.SetFloat("_ReceiveShadows", 0);
                tiles.GetComponent<TilemapRenderer>().sharedMaterial = material;
                var camera = cameraObject.GetComponent<Camera>(); camera.transform.position = new Vector3(8, 6, -10);
                camera.orthographic = true; camera.orthographicSize = 6;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 768, 576), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory("out"); System.IO.File.WriteAllBytes("out/forest-paths-preview.png", pixels.EncodeToPNG());
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                Assert.That(pixels.GetPixel(384, 288).a, Is.GreaterThan(.99f));
                view.ConfigureVegetationPresentation(null); view.ConfigureForestGroundMaterial(material);
                Assert.That(material.GetFloat("_ForestSurfaceEnabled"), Is.Zero, "Indoor material must keep its original ground.");
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(tile); Object.DestroyImmediate(material); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void ForestTrailCannotAdvertiseVoidOrACrossingThroughASolidWall()
        {
            var root=new GameObject("Blocked forest trail",typeof(Grid));
            var tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform,false);
            var material=new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            RenderTexture target=null; Texture2D pixels=null; var previous=RenderTexture.active;
            try
            {
                var view=root.AddComponent<DungeonFloorMapView>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                void Set(string name,object value)=>typeof(DungeonFloorMapView).GetField(name,flags).SetValue(view,value);
                Set("floorTilemap",tiles.GetComponent<Tilemap>());
                view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                DungeonFloorMap Map(bool wall)
                {
                    var map=new DungeonFloorMap();
                    for(var y=0;y<3;y++)for(var x=0;x<6;x++)map.TryAddFloorCell(new Vector2Int(x,y),x<3?"west":"east");
                    if(wall)for(var y=0;y<3;y++)map.TryAddWall(new Vector2Int(2,y),new Vector2Int(3,y));
                    return map;
                }
                var blocked=Map(true); Set("boundMap",blocked);
                var bake=typeof(DungeonFloorMapView).GetMethod("BuildForestSurfaceMask",flags); bake.Invoke(view,null);
                var paths=(HashSet<Vector2Int>)typeof(DungeonFloorMapView).GetField("forestVisualPathCells",flags).GetValue(view);
                Assert.That(paths,Is.Empty,"Separated rooms must not sprout paths through their solid divider.");
                var open=Map(false); Set("boundMap",open); var revision=open.TraversalRevision; bake.Invoke(view,null);
                Assert.That(paths,Is.Not.Empty);
                view.ConfigureForestGroundMaterial(material);
                var mask=(Texture2D)material.GetTexture("_ForestSurfaceMask");
                var bounds=material.GetVector("_ForestSurfaceBounds");
                target=new RenderTexture(mask.width,mask.height,0); Graphics.Blit(mask,target); RenderTexture.active=target;
                pixels=new Texture2D(mask.width,mask.height,TextureFormat.RGBA32,false);
                pixels.ReadPixels(new Rect(0,0,mask.width,mask.height),0,0); pixels.Apply();
                var values=pixels.GetPixels32(); var trailPixels=0;
                for(var y=0;y<mask.height;y++)for(var x=0;x<mask.width;x++)
                {
                    if(values[y*mask.width+x].r==0)continue;
                    trailPixels++;
                    var point=new Vector2(bounds.x+(x+.5f)/mask.width*bounds.z,bounds.y+(y+.5f)/mask.height*bounds.w);
                    Assert.That(open.IsWalkable(view.WorldToCell(point)),Is.True,"Underlay must never carry a trail into void.");
                }
                Assert.That(trailPixels,Is.GreaterThan(5)); Assert.That(open.TraversalRevision,Is.EqualTo(revision));
            }
            finally
            {
                RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(material);
                if(target!=null)UnityEngine.Object.DestroyImmediate(target); if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void ForestFloorAndFogShareTheSameRenderedCellBounds()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Requires GPU");
            var root = new GameObject("Forest floor and fog", typeof(Grid));
            var floorObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            floorObject.layer = 31; floorObject.transform.SetParent(root.transform);
            var cameraObject = new GameObject("Floor fog camera", typeof(Camera));
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var tile = ScriptableObject.CreateInstance<Tile>();
            var previous = RenderTexture.active;
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                void Set(string name, object value) => typeof(DungeonFloorMapView)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
                Set("floorTilemap", floorObject.GetComponent<Tilemap>()); Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "glade");
                map.TryAddFloorCell(Vector2Int.up, "glade");
                map.TryAddFloorCell(Vector2Int.right, "glade");
                floor.TrySetMap(map);
                floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)) });
                view.ConfigureVegetationPresentation(definition.VegetationPresentation); view.Rebuild(map);
                view.SetPresentationOccupancy(map.FloorCells); view.ApplyFog(floor);
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
                camera.transform.position = new Vector3(.5f, .5f, -10);
                camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixel(64, 100).a, Is.LessThan(.01f), "No shifted floor strip may extend into the unknown northern cell.");
                Assert.That(pixels.GetPixel(64, 35).a, Is.GreaterThan(.9f), "The southern half of the known cell must be covered by floor.");
                Assert.That(pixels.GetPixel(64, 94).grayscale, Is.LessThan(.08f), "Fog must darken the actual northern floor edge.");
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../out"));
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, "forest-floor-fog-alignment.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void UnderlayShaderFeathersCornersAndPreservesOrdinaryFloor()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Requires GPU");
            var root = new GameObject("Forest feather render", typeof(Camera));
            var patch = new GameObject("Patch", typeof(SpriteRenderer));
            patch.transform.SetParent(root.transform, false);
            patch.transform.position = Vector3.zero;
            patch.layer = 31;
            var texture = new Texture2D(2, 2);
            texture.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray()); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 1);
            var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var renderer = patch.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
                var camera = root.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                patch.transform.position = Vector3.zero;
                camera.orthographic = true; camera.orthographicSize = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                void Read() { camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply(); }
                material.SetVector("_ForestPatchBounds", new Vector4(0, 0, 1, 1)); Read();
                Assert.That(pixels.GetPixel(64, 64).a, Is.GreaterThan(.9f));
                Assert.That(pixels.GetPixel(4, 4).a, Is.LessThan(.05f));
                Assert.That(pixels.GetPixel(110, 64).a, Is.InRange(.1f, .9f));
                material.SetVector("_ForestPatchBounds", Vector4.zero); Read();
                Assert.That(pixels.GetPixel(4, 4).a, Is.GreaterThan(.9f));
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void GroundCoverIsDeterministicSparseAndAvoidsOccupiedCells()
        {
            var root = new GameObject("Forest ground cover test", typeof(Grid));
            var floorObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            floorObject.transform.SetParent(root.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                void Set(string name, object value) => typeof(DungeonFloorMapView)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
                Set("floorTilemap", floorObject.GetComponent<Tilemap>());
                Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap();
                for (var y = 0; y < 12; y++) for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, y), "glade");
                var revision = map.TraversalRevision;
                view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                view.Rebuild(map);
                Assert.That(definition.VegetationPresentation.LowShrub, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.DryShrub, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.DryShrub.rect.size, Is.EqualTo(new Vector2(40,24)));
                Assert.That(definition.VegetationPresentation.DryShrub.pixelsPerUnit, Is.EqualTo(32));
                Assert.That(definition.VegetationPresentation.Deadwood, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.ExposedRoots, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.WoodlandFungi, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.WoodlandFern, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.FallenLog, Is.Not.Null);
                Assert.That(definition.VegetationPresentation.SmallStone, Is.Not.Null);
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Forest ") && (r.name == "Forest ground cover" || r.name == "Forest deadwood" || r.name == "Forest low shrub" || r.name == "Forest exposed roots" || r.name == "Forest woodland fungi" || r.name == "Forest woodland fern" || r.name == "Forest fallen log" || r.name == "Forest small stone")))
                {
                    Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(32), renderer.name);
                    Assert.That(renderer.transform.lossyScale.x, Is.EqualTo(1).Within(.0001f), renderer.name);
                    Assert.That(renderer.transform.lossyScale.y, Is.EqualTo(1).Within(.0001f), renderer.name);
                }
                var edgeDetails = root.GetComponentsInChildren<SpriteRenderer>()
                    .Where(r => r.name == "Forest deadwood" || r.name == "Forest low shrub" || r.name == "Forest exposed roots" || r.name == "Forest woodland fungi" || r.name == "Forest woodland fern" || r.name == "Forest fallen log" || r.name == "Forest small stone").ToArray();
                Assert.That(edgeDetails.Length, Is.GreaterThan(0));
                var paths = (HashSet<Vector2Int>)typeof(DungeonFloorMapView)
                    .GetField("forestVisualPathCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var grass = root.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == "Forest ground cover").ToArray();
                Assert.That(grass.Length, Is.GreaterThan(0));
                foreach (var detail in grass)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(detail.bounds.center.x), Mathf.FloorToInt(detail.bounds.center.y));
                    Assert.That(paths.Contains(c), Is.False, "The trodden path must remain free of grass sprites.");
                    Assert.That(detail.color.a, Is.InRange(.64f,.86f));
                }
                foreach (var detail in edgeDetails)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(detail.bounds.center.x), Mathf.FloorToInt(detail.bounds.center.y));
                    Assert.That(c.x == 0 || c.y == 0 || c.x == 11 || c.y == 11, Is.True);
                    Assert.That(paths.Any(path => Mathf.Abs(path.x - c.x) <= 1 && Mathf.Abs(path.y - c.y) <= 1), Is.False);
                }
                var variantSprites = new[] { definition.VegetationPresentation.GroundCoverVariants, definition.VegetationPresentation.LowShrubVariants, definition.VegetationPresentation.DeadwoodVariants, definition.VegetationPresentation.ExposedRootsVariants, definition.VegetationPresentation.WoodlandFungiVariants, definition.VegetationPresentation.WoodlandFernVariants, definition.VegetationPresentation.FallenLogVariants, definition.VegetationPresentation.SmallStoneVariants }.SelectMany(s => s).ToHashSet();
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => variantSprites.Contains(r.sprite)), Is.True, "Rebuilt forest must use the authored detail variants.");
                var count = view.PresentationFloorDecorationCount;
                Assert.That(count, Is.InRange(4, 30));
                var tilemap = floorObject.GetComponent<Tilemap>();
                var dirt = (Tile)definition.VegetationPresentation.GroundTile;
                var matrices = map.FloorCells.Select(c => tilemap.GetTransformMatrix(new Vector3Int(c.x, c.y, 0))).ToArray();
                Assert.That(matrices.Distinct().Count(), Is.GreaterThan(1));
                foreach (var matrix in matrices)
                {
                    Assert.That(matrix.MultiplyPoint3x4(dirt.sprite.bounds.center).sqrMagnitude, Is.LessThan(.00001f));
                    var size = dirt.sprite.bounds.size;
                    var corner = matrix.MultiplyVector(size * .5f);
                    Assert.That(Mathf.Abs(corner.x), Is.EqualTo(.5f).Within(.0001f));
                    Assert.That(Mathf.Abs(corner.y), Is.EqualTo(.5f).Within(.0001f));
                }
                var signature = view.PresentationLayoutSignature;
                view.SetPresentationOccupancy(System.Array.Empty<Vector2Int>());
                Assert.That(view.PresentationLayoutSignature, Is.EqualTo(signature));
                Assert.That(root.GetComponentsInChildren<Collider2D>().Any(c => c.enabled), Is.False);
                view.SetForestPathObstacles(new[] { new Vector2Int(3,3) });
                view.SetPresentationOccupancy(new[] { new Vector2Int(3,3) });
                var relocated = root.GetComponentsInChildren<SpriteRenderer>()
                    .Where(r => r.name == "Forest deadwood" || r.name == "Forest low shrub" || r.name == "Forest exposed roots" || r.name == "Forest woodland fungi" || r.name == "Forest woodland fern" || r.name == "Forest fallen log" || r.name == "Forest small stone").ToArray();
                Assert.That(relocated.Length, Is.EqualTo(edgeDetails.Length), "Interior details must reuse the existing budget.");
                Assert.That(relocated.Any(r => r.bounds.center.x > 1 && r.bounds.center.x < 10 && r.bounds.center.y > 1 && r.bounds.center.y < 10), Is.True);
                var interiorDetails = relocated.Where(r => r.bounds.center.x > 1 && r.bounds.center.x < 10 && r.bounds.center.y > 1 && r.bounds.center.y < 10).ToArray();
                foreach (var detail in interiorDetails)
                {
                    Assert.That(new[] { "Forest low shrub", "Forest exposed roots", "Forest woodland fungi", "Forest woodland fern" }, Does.Contain(detail.name));
                    Assert.That(Vector2.Distance(detail.bounds.center, view.CellToWorldCenter(new Vector2Int(3,3))), Is.LessThan(1.6f));
                    Assert.That(detail.transform.lossyScale.x, Is.EqualTo(1).Within(.0001f));
                }
                var relocatedSignature = view.PresentationLayoutSignature;
                view.SetPresentationOccupancy(new[] { new Vector2Int(3,3) });
                Assert.That(view.PresentationLayoutSignature, Is.EqualTo(relocatedSignature));
                view.SetPresentationOccupancy(map.FloorCells);
                Assert.That(view.PresentationFloorDecorationCount, Is.Zero);
                Assert.That(map.TraversalRevision, Is.EqualTo(revision));
                Assert.That(Shader.Find("Godmist/Dungeon/Silhouette Floor"), Is.Not.Null);
                Assert.That(ShaderUtil.ShaderHasError(Shader.Find("Godmist/Dungeon/Silhouette Floor")), Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void InteriorPropsKeepEveryGladeReachableWithoutMatureTrees()
        {
            var entrance = definition.RoomTemplateCatalog.Templates.Single(t => t.RoomType == DungeonRoomType.Start);
            Assert.That(entrance.SemanticAnchors.Count(a => a.ObjectDefinition is ObstacleDungeonObjectDefinition), Is.EqualTo(2),
                "Both new props must be visible in every freshly generated entrance.");
            var ruins = definition.RoomTemplateCatalog.Templates.Single(t => t.Id == "room.forest.overgrown_ruins");
            Assert.That(ruins.SemanticAnchors.Any(a => a.ObjectDefinition?.Id == "object.forest.ruined_pillar.prototype"), Is.True);
            Assert.That(definition.RoomTemplateCatalog.Templates.Where(t => t != ruins)
                .All(t => t.SemanticAnchors.All(a => a.ObjectDefinition?.Id != "object.forest.ruined_pillar.prototype")), Is.True);
            Assert.That(ruins.SemanticAnchors.Count(a => a.ObjectDefinition?.Id == "object.forest.gravestone.prototype"), Is.EqualTo(1));
            Assert.That(definition.RoomTemplateCatalog.Templates.Where(t => t != ruins)
                .All(t => t.SemanticAnchors.All(a => a.ObjectDefinition?.Id != "object.forest.gravestone.prototype")), Is.True);
            var camp = definition.RoomTemplateCatalog.Templates.Single(t => t.Id == "room.forest.blind_glade");
            foreach (var id in new[] { "object.forest.cold_campfire.prototype", "object.forest.broken_crate.prototype", "object.forest.bedroll.prototype" })
            {
                Assert.That(camp.SemanticAnchors.Count(a => a.ObjectDefinition?.Id == id), Is.EqualTo(1));
                Assert.That(definition.RoomTemplateCatalog.Templates.Where(t => t != camp)
                    .All(t => t.SemanticAnchors.All(a => a.ObjectDefinition?.Id != id)), Is.True);
            }
            foreach (var theme in new[] { ("roots", "object.forest.stump.prototype"), ("hollow", "object.forest.boulder.prototype") })
            {
                var room = definition.RoomTemplateCatalog.Templates.Single(t => t.Id == "room.forest." + theme.Item1);
                var props = room.SemanticAnchors.Where(a => a.Id.StartsWith("edgeprop.")).ToArray();
                Assert.That(props.Length, Is.EqualTo(2), "Themed glades must keep their previous object budget.");
                Assert.That(props.All(a => a.ObjectDefinition.Id == theme.Item2), Is.True);
                Assert.That((props[0].LocalCell - props[1].LocalCell).sqrMagnitude, Is.InRange(4, 10),
                    "Props should form one accessible composition.");
            }
            var totalTrees = 0;
            var totalBoulders = 0;
            var totalStumps = 0;
            foreach (var seed in new[] { 1, 7, 19, 42, 117, 4205, 9891, 12345, 65535, 20261011 })
            {
                var dungeon = new DungeonGeneratorService().Generate(definition, seed);
                new DungeonObjectPlacementSystem().Place(dungeon);
                var floor = dungeon.Floors.Single();
                var trees = floor.Objects.OfType<ObstacleDungeonObjectInstance>().ToArray();
                Assert.That(trees.Any(t => t.Definition.Id == "object.forest.tree.prototype"), Is.False,
                    "Mature trees must not be reintroduced by the authoring tool.");
                var expectedTrees = floor.Rooms.Sum(r => definition.RoomTemplateCatalog.Templates
                    .Single(t => t.Id == r.TemplateId).SemanticAnchors.Count(a => a.ObjectDefinition is ObstacleDungeonObjectDefinition));
                Assert.That(trees.Length, Is.EqualTo(expectedTrees), "Missing authored trees; seed " + seed);
                totalTrees += trees.Length;
                totalBoulders += trees.Count(t => t.Definition.Id == "object.forest.boulder.prototype");
                totalStumps += trees.Count(t => t.Definition.Id == "object.forest.stump.prototype");
                Assert.That(trees.All(t => t.Definition.Id == "object.forest.bedroll.prototype" ? !t.BlocksMovement && !t.BlocksLineOfSight
                    : t.BlocksMovement && (t.OccupiedSizeInCells == Vector2Int.one ? t.UsesPhysicalExplorationFootprint : !t.UsesPhysicalExplorationFootprint)), Is.True);
                var occupied = trees.SelectMany(t => t.EnumerateOccupiedCells()).ToHashSet();
                Assert.That(occupied.Count, Is.EqualTo(trees.Sum(t=>t.OccupiedSizeInCells.x*t.OccupiedSizeInCells.y)),"All four tree cells must be distinct from other props.");
                Assert.That(occupied.All(floor.Map.IsWalkable), Is.True);
                var reachable = new HashSet<Vector2Int>();
                var pending = new Queue<Vector2Int>();
                pending.Enqueue(floor.Map.FloorCells.First(c => !occupied.Contains(c)));
                while (pending.Count > 0)
                {
                    var cell = pending.Dequeue();
                    if (!reachable.Add(cell)) continue;
                    foreach (var direction in Directions)
                        if (!occupied.Contains(cell + direction) && floor.Map.CanMoveBetween(cell, cell + direction)
                            && !reachable.Contains(cell + direction)) pending.Enqueue(cell + direction);
                }
                Assert.That(reachable.Count, Is.EqualTo(floor.Map.FloorCells.Count - occupied.Count), "Seed " + seed);
                if (trees.Length == 0) continue; // Narrow clearings deliberately have no interior cover.
                var sight = new DungeonVisibilitySystem();
                var tree = trees.First(t => t.BlocksLineOfSight && floor.Map.IsWalkable(t.Position + Vector2Int.left)
                    && floor.Map.IsWalkable(t.Position + Vector2Int.right));
                Assert.That(sight.HasLineOfSight(floor,
                    new DungeonTraversalPosition(new DungeonSurfacePosition(tree.Position + Vector2Int.left)),
                    new DungeonTraversalPosition(new DungeonSurfacePosition(tree.Position + Vector2Int.right))), Is.False);
            }
            Assert.That(totalTrees, Is.GreaterThan(20));
            Assert.That(totalBoulders, Is.GreaterThan(0));
            Assert.That(totalStumps, Is.GreaterThan(0));
        }

        [Test]
        public void PrototypeHasOwnOutdoorContentWithoutBorrowedEncounters()
        {
            Assert.That(definition.Theme, Is.EqualTo(DungeonTheme.Forest));
            Assert.That(definition.EnvironmentConfigured, Is.True);
            Assert.That(definition.IsIndoor, Is.False);
            Assert.That(definition.IsAvailableAsOffer, Is.False);
            Assert.That(definition.EncounterDefinitions.All(e => e.Id.StartsWith("encounter.forest.")), Is.True);
            var ent = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(ForestEnemiesSetup.EntEncounterPath);
            Assert.That(definition.BossEncounterDefinition, Is.SameAs(ent));
            Assert.That(definition.VegetationPresentation.GroundTile, Is.Not.Null);
            Assert.That(definition.VegetationPresentation.BoundaryTree, Is.Not.Null);
            Assert.That(definition.VegetationPresentation.GroundCover, Is.Not.Null);
            Assert.That(definition.RoomTemplateCatalog.Templates.Count, Is.GreaterThanOrEqualTo(11));
            foreach (var room in definition.RoomTemplateCatalog.Templates)
            {
                var report = DungeonRoomTemplateEditorValidator.Validate(room);
                Assert.That(report.IsValid, Is.True, room.Id + ": " + string.Join("; ", report.Errors));
                Assert.That(room.Footprint.FloorCellOffsets.Count,
                    Is.LessThan(room.SizeInCells.x * room.SizeInCells.y), room.Id);
            }
        }

        [Test]
        public void DenseIslandBlocksSightButOpenGladeDoesNot()
        {
            var room = definition.RoomTemplateCatalog.Templates.Single(t => t.Id == "room.forest.hollow");
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("glade", DungeonRoomType.Normal, room.Id));
            floor.TrySetMap(new DungeonFloorMapBuilder().Build(floor, definition.RoomTemplateCatalog));
            var sight = new DungeonVisibilitySystem();
            DungeonTraversalPosition Position(int x, int y) => new(new DungeonSurfacePosition(new Vector2Int(x, y)));
            Assert.That(sight.HasLineOfSight(floor, Position(3, 5), Position(8, 5)), Is.False);
            Assert.That(sight.HasLineOfSight(floor, Position(3, 3), Position(8, 3)), Is.True);
        }

        [Test]
        public void FringePocketsAreRealWalkableCellsReachableByNativeNavigation()
        {
            var floor = new DungeonGeneratorService().Generate(definition, 42).Floors.Single();
            var originalCatalog = Object.Instantiate(definition.RoomTemplateCatalog);
            var root = new GameObject("Forest fringe navigation", typeof(Grid));
            using var navigation = new DungeonNavigationMesh();
            try
            {
                typeof(DungeonRoomTemplateCatalog).GetField("<HasVegetationFringe>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(originalCatalog, false);
                var original = new DungeonFloorMapBuilder().Build(floor, originalCatalog);
                var pockets = floor.Map.FloorCells.Where(c => !original.IsWalkable(c)).ToArray();
                Assert.That(pockets.Length, Is.GreaterThan(10));
                var tilemap = new GameObject("Floor", typeof(Tilemap));
                tilemap.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(view, tilemap.GetComponent<Tilemap>());
                view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                Assert.That(navigation.Build(floor, view, 0, .15f, null), Is.True);
                foreach (var pocket in pockets.Take(10))
                {
                    var neighbour = Directions.Select(d => pocket + d)
                        .First(c => original.IsWalkable(c) && floor.Map.CanMoveBetween(c, pocket));
                    Assert.That(navigation.TryFindPath(view.CellToWorldCenter(neighbour), view.CellToWorldCenter(pocket),
                        Vector2.zero, (a, b) => true, out _), Is.True, "Pocket not reachable: " + pocket);
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(originalCatalog); }
        }

        [Test]
        public void LegacyInteriorTrunkMarkersCreateNeitherTreeArtNorPhysicalBlockers()
        {
            var root = new GameObject("Individual forest roots", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<Tile>();
            using var navigation = new DungeonNavigationMesh();
            try
            {
                var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                void Set(string name, object value) => typeof(DungeonFloorMapView)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
                Set("floorTilemap", tiles.GetComponent<Tilemap>());
                Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap();
                for (var y = 0; y < 3; y++)
                    for (var x = 0; x < 3; x++) map.TryAddFloorCell(new Vector2Int(x, y), "glade");
                foreach (var cell in map.FloorCells)
                    foreach (var direction in Directions)
                        if (!map.IsWalkable(cell + direction)) map.TryAddWall(cell, cell + direction);
                var from = new Vector2Int(1, 1);
                var to = new Vector2Int(2, 1);
                map.SetWallType(new DungeonCellEdge(from, to), DungeonWallType.TreeTrunk);
                view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                view.Rebuild(map);
                var actorObject = new GameObject("Actor", typeof(CircleCollider2D));
                actorObject.transform.SetParent(root.transform);
                var collider = actorObject.GetComponent<CircleCollider2D>();
                collider.radius = .196f;
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(actor, collider);
                actor.ConfigureTerrain(map, view);
                var start = view.CellToWorldCenter(from);
                var goal = view.CellToWorldCenter(to);
                actor.SetPosition(start);
                Physics2D.SyncTransforms();
                Assert.That(root.GetComponentsInChildren<CircleCollider2D>().Any(c=>c!=collider&&c.enabled),Is.False,
                    "A saved interior trunk marker must not leave an invisible physical obstacle.");
                Assert.That(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Individual tree roots"||t.name=="Tree art"),Is.False);
                Assert.That(map.CanMoveBetween(from, to), Is.True, "The old edge must no longer be a solid wall.");
                Assert.That(actor.HasClearMovementSegment(start, goal), Is.True, "Visible gap must be physically traversable.");
                var edgeCentre=(start+goal)*.5f-actor.MovementColliderCenterOffset;
                Assert.That(actor.HasClearMovementSegment(edgeCentre-Vector2.up*.4f,
                    edgeCentre+Vector2.up*.4f),Is.True,"The former off-centre root area must also be clear.");
                var floor = new DungeonFloorInstance(0);
                floor.TrySetMap(map);
                Assert.That(navigation.Build(floor, view, 0, actor.MovementRadius, view.NavigationWallBounds(0)), Is.True);
                Assert.That(navigation.TryFindPath(start, goal, actor.MovementColliderCenterOffset,
                    actor.HasClearMovementSegment, out _), Is.True, "Native route must use the same gap.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void FreshForestFringeNeverCreatesInteriorTreeMarkers()
        {
            foreach(var seed in new[]{1,42,171026})
            {
                var floor=new DungeonGeneratorService().Generate(definition,seed).Floors.Single();
                Assert.That(floor.Map.WallEdges.Any(e=>floor.Map.GetWallType(e)==DungeonWallType.TreeTrunk),Is.False,
                    "The traversable fringe is clearing floor, not an independent random tree source; seed="+seed);
            }
        }

        [Test]
        public void TreesReplaceWallDrawingWithoutRemovingCollidersOrChangingMap()
        {
            var root = new GameObject("Forest presentation fixture");
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(root.transform);
            var floorObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
            floorObject.transform.SetParent(grid.transform);
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                void Set(string name, object value) => typeof(DungeonFloorMapView)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
                Set("floorTilemap", floorObject.GetComponent<Tilemap>());
                Set("floorTile", tile);
                Set("wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "glade");
                foreach (var direction in Directions) map.TryAddWall(Vector2Int.zero, direction);
                var revision = map.TraversalRevision;
                view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                view.Rebuild(map);
                var lighting = view.LightingWallRenderers.ToArray();
                Assert.That(lighting, Is.Not.Empty, "Mature trees retain deep woodland while reserving four cells each.");
                var vegetation=definition.VegetationPresentation;
                var accepted=new[]{vegetation.BoundaryTree,vegetation.LowShrub,vegetation.DryShrub,vegetation.WoodlandFern}
                    .Concat(vegetation.TreeVariants).Concat(vegetation.SmallTreeVariants).Concat(vegetation.LowShrubVariants).Concat(vegetation.WoodlandFernVariants).ToHashSet();
                Assert.That(lighting.All(r=>accepted.Contains(r.sprite)),Is.True);
                var scrub=lighting.Where(r=>r.name=="Forest boundary scrub").ToArray();
                // Low shrubs were moved off the blocking perimeter during the
                // playtest pass: the map border must read as trees/trunks.
                Assert.That(scrub,Is.Empty);
                var trunks=lighting.Where(r=>r.name=="Boundary vegetation").ToArray();
                Assert.That(trunks.Select(r=>r.sprite).Distinct().Count(),Is.GreaterThan(1));
                Assert.That(trunks.All(r=>r.sprite.pixelsPerUnit==32 && r.transform.localScale==Vector3.one),Is.True);
                var groundCenter = view.CellToWorldCenter(Vector2Int.zero);
                var footprints=(Dictionary<SpriteRenderer,RectInt>)typeof(DungeonFloorMapView)
                    .GetField("forestTreeFootprints",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                Assert.That(footprints,Is.Not.Empty);
                foreach(var pair in footprints)
                {
                    Assert.That(pair.Value.size,Is.EqualTo(Vector2Int.one*(pair.Key.sprite.bounds.size.x>=3?2:1)));
                    Assert.That(pair.Value.Contains(view.WorldToCell(pair.Key.transform.position)),Is.True);
                    foreach(var cell in pair.Value.allPositionsWithin)Assert.That(map.IsWalkable(cell),Is.False);
                }
                Assert.That(lighting.Select(r => r.color.r).Distinct().Count(), Is.GreaterThanOrEqualTo(5));
                Assert.That(lighting.Min(r => r.color.r), Is.InRange(lighting.Max(r => r.color.r)*.5f,lighting.Max(r => r.color.r)*.9f),
                    "Deeper woodland keeps a modest tint difference; the shared fog composite supplies extinction.");
                Assert.That(lighting.Max(r => Vector2.Distance(r.transform.position, view.CellToWorldCenter(Vector2Int.zero))),
                    Is.GreaterThan(4f));
                view.SetPresentationOccupancy(new[] { Vector2Int.zero });
                Assert.That(view.ForestPropContacts.All(p=>p.Key!=null),Is.True);
                Assert.That(scrub.All(view.IsForestReadableProp),Is.True,"Boundary contacts survive decoration rebuilds.");
                Assert.That(view.LightingWallRenderers, Is.EquivalentTo(lighting),
                    "Occupancy refresh must not restore Catacombs wall renderers.");
                Assert.That(root.GetComponentsInChildren<Collider2D>().Count(c => c.enabled), Is.GreaterThanOrEqualTo(4));
                Assert.That(map.TraversalRevision, Is.EqualTo(revision));
                Assert.That(floorObject.GetComponent<Tilemap>().GetTile(Vector3Int.zero),
                    Is.SameAs(definition.VegetationPresentation.GroundTile));
                var groundTile = (Tile)definition.VegetationPresentation.GroundTile;
                var groundTransform = floorObject.GetComponent<Tilemap>().GetTransformMatrix(Vector3Int.zero);
                Assert.That(groundTransform.MultiplyPoint3x4(groundTile.sprite.bounds.center).sqrMagnitude,
                    Is.LessThan(.00001f), "Ground art must align with the grid-centred soft fog mesh.");
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r => !lighting.Contains(r))
                    .Where(r => !view.IsGroundLightingPresentation(r))
                    .All(r => r.forceRenderingOff), Is.True);
                Assert.That(view.LightingPresentationRenderers, Is.Not.Empty);
                foreach (var underlay in root.GetComponentsInChildren<SpriteRenderer>()
                    .Where(r => view.PreservesGroundPresentationSorting(r)))
                {
                    Assert.That(view.LightingPresentationRenderers, Does.Contain(underlay), "Every ground patch must remain available to lighting after tree spacing changes.");
                    var properties = new MaterialPropertyBlock();
                    underlay.GetPropertyBlock(properties);
                    if(properties.GetFloat("_ForestBackdrop")>.5f)
                    {
                        Assert.That(properties.GetVector("_ForestBackdropBounds").z,Is.GreaterThan(1));
                        continue; // Continuous backdrop is not a local soil patch.
                    }
                    var bounds = properties.GetVector("_ForestPatchBounds");
                    Assert.That(bounds.z, Is.GreaterThan(1));
                    Assert.That(underlay.bounds.size.x, Is.EqualTo(bounds.z * 2).Within(.001f));
                    Assert.That(new Vector2(bounds.x, bounds.y), Is.EqualTo((Vector2)underlay.bounds.center));
                }
                Assert.That(root.GetComponentsInChildren<BoxCollider2D>().Where(c => c.enabled)
                    .All(c => c.edgeRadius > 0 && Mathf.Min(c.bounds.size.x, c.bounds.size.y) < .1f), Is.True);
                var actorObject = new GameObject("Forest approach", typeof(CircleCollider2D));
                actorObject.transform.SetParent(root.transform);
                var actorCollider = actorObject.GetComponent<CircleCollider2D>();
                actorCollider.radius = .1f;
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(actor, actorCollider);
                actor.ConfigureTerrain(map, view);
                actor.SetPosition(groundCenter);
                Physics2D.SyncTransforms();
                Assert.That(actor.HasClearMovementSegment(groundCenter, groundCenter + Vector2.up * .27f), Is.True,
                    "Thin vegetation boundary must allow approaching the trunk beyond the old masonry clearance.");
                Assert.That(actor.HasClearMovementSegment(groundCenter, groundCenter + Vector2.up * .4f), Is.False,
                    "Approach clearance must not allow crossing into deep vegetation.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [TestCase(1)] [TestCase(7)] [TestCase(19)] [TestCase(42)] [TestCase(117)]
        [TestCase(4205)] [TestCase(9891)] [TestCase(12345)] [TestCase(65535)] [TestCase(20261011)]
        public void EntireGeneratedFloorIsReachableAndBoundariesBlockTraversal(int seed)
        {
            var dungeon = new DungeonGeneratorService().Generate(definition, seed);
            var floor = dungeon.Floors.Single();
            var map = floor.Map;
            var visited = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(map.FloorCells.First());
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                if (!visited.Add(cell)) continue;
                foreach (var direction in Directions)
                    if (map.CanMoveBetween(cell, cell + direction) && !visited.Contains(cell + direction))
                        pending.Enqueue(cell + direction);
            }
            Assert.That(visited.Count, Is.EqualTo(map.FloorCells.Count), "Disconnected glade/path; seed=" + seed);
            Assert.That(floor.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional), Is.GreaterThanOrEqualTo(3));
            Assert.That(floor.Rooms.Count(r => r.RoomType == DungeonRoomType.Final), Is.EqualTo(1));
            foreach (var cell in map.FloorCells)
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (map.IsWalkable(next)) continue;
                    Assert.That(map.HasWallBetween(cell, next), Is.True, "Open forest boundary: " + cell);
                    Assert.That(map.CanMoveBetween(cell, next), Is.False);
                }
            var repeated = new DungeonGeneratorService().Generate(definition, seed).Floors.Single();
            Assert.That(repeated.Map.FloorCells, Is.EquivalentTo(map.FloorCells));
            Assert.That(repeated.Map.WallEdges, Is.EquivalentTo(map.WallEdges));
            if (seed == 42) ExportLayout(floor);
        }

        private static void ExportLayout(DungeonFloorInstance floor)
        {
            var cells = floor.Map.FloorCells;
            var minX = cells.Min(c => c.x) - 1;
            var maxY = cells.Max(c => c.y) + 1;
            var width = (cells.Max(c => c.x) - minX + 2) * 8;
            var height = (maxY - cells.Min(c => c.y) + 2) * 8;
            var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
            svg.Append("<rect width='100%' height='100%' fill='#10140f'/>");
            foreach (var cell in cells)
            {
                floor.Map.TryGetRoomId(cell, out var roomId);
                var type = floor.Rooms.FirstOrDefault(r => r.RoomId == roomId)?.RoomType;
                var color = type switch { DungeonRoomType.Start => "#939575", DungeonRoomType.Final => "#97624c",
                    DungeonRoomType.Optional => "#63764e", DungeonRoomType.Corridor => "#55594a", _ => "#777b62" };
                if (floor.Map.IsDifficultTerrain(cell)) color = "#3b4935";
                svg.Append($"<rect x='{(cell.x-minX)*8}' y='{(maxY-cell.y)*8}' width='8' height='8' fill='{color}'/>");
            }
            svg.Append("</svg>");
            Directory.CreateDirectory("out");
            File.WriteAllText("out/forest-layout-seed42.svg", svg.ToString());
        }
    }
}
