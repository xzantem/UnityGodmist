using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using Godmist.Core.Enums;
using Godmist.Views.Lighting;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class SpriteRenderingAuditTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/forest-canopy-entry-20261020"));
        readonly List<Object> owned = new();
        T Keep<T>(T value) where T : Object { owned.Add(value); return value; }
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
        static void Call(object owner, string name) => owner.GetType().GetMethod(name, Flags).Invoke(owner, null);
        [TearDown] public void Cleanup()
        {
            foreach (var root in owned.OfType<GameObject>()) if (root != null)
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.targetTexture = null;
            foreach (var item in owned.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }
        DungeonFloorMapView Terrain(bool forest, out DungeonFloorInstance floor, IEnumerable<Vector2Int> cells = null)
        {
            var root = Keep(new GameObject("Sprite depth audit", typeof(Grid)));
            var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform, false);
            var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<Tilemap>());
            Set(view, "floorTile", Keep(ScriptableObject.CreateInstance<Tile>()));
            Set(view, "wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
            if (forest) view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
            var map = new DungeonFloorMap();
            if(cells == null)
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            else foreach(var cell in cells)map.TryAddFloorCell(cell,"room");
            foreach (var cell in map.FloorCells) foreach (var d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                if (!map.IsWalkable(cell + d)) map.TryAddWall(cell, cell + d);
            view.Rebuild(map); floor = new DungeonFloorInstance(0); floor.TrySetMap(map); return view;
        }
        Sprite Solid(int width, int height, Color color)
        {
            var texture = Keep(new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels(Enumerable.Repeat(color, width * height).ToArray()); texture.Apply();
            return Keep(Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect));
        }
        SpriteRenderer Rendered(string name, Sprite sprite, Vector2 feet)
        {
            var node = Keep(new GameObject(name, typeof(SpriteRenderer))); node.layer = 30; node.transform.position = feet;
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; return renderer;
        }
        Texture2D Capture(Vector2 centre, float halfHeight, string label, Color? backgroundColor = null, int resolution = 256)
        {
            var node = Keep(new GameObject("Audit camera", typeof(Camera))); var camera = node.GetComponent<Camera>();
            camera.transform.position = new Vector3(centre.x, centre.y, -10); camera.orthographic = true; camera.orthographicSize = halfHeight;
            camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = backgroundColor ?? new Color(.13f, .16f, .08f);
            var target = Keep(new RenderTexture(resolution, resolution, 24)); camera.targetTexture = target;
            var image = Keep(new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)); var previous = RenderTexture.active;
            try
            {
                SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); image.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Path.Combine(Output, label + ".png"), image.EncodeToPNG()); return image;
            }
            finally { RenderTexture.active = previous; }
        }

        [TestCase(false)] [TestCase(true)]
        public void OwnPartySurvivesStaleFogButEnemyRemainsHidden(bool forest)
        {
            var map = Terrain(forest, out var floor); floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); map.ApplyFog(floor);
            foreach (bool enemy in new[] { false, true })
            {
                var sprite = Rendered(enemy ? "enemy" : "party", Solid(16, 48, Color.red), new Vector2(2.5f, 2.5f));
                var actor = sprite.gameObject.AddComponent<ExplorationCharacterView>(); Set(actor, "bodyRenderer", sprite);
                actor.ConfigureTerrain(floor.Map, map, floor, enemy ? "enemy:test" : "party:test");
                if (enemy) actor.SetEnemyInstanceId("enemy:test");
                Call(actor, "LateUpdate");
                Assert.That(sprite.forceRenderingOff, Is.EqualTo(enemy)); Assert.That(sprite.color.a, Is.EqualTo(1));
                var group = actor.gameObject.GetComponent<SortingGroup>();
                Assert.That(group != null && group.enabled, Is.EqualTo(!enemy));
            }
        }

        [TestCase("party", true)] [TestCase("enemy", true)] [TestCase("growth", true)] [TestCase("corpse", true)]
        [TestCase("party", false)] [TestCase("enemy", false)] [TestCase("growth", false)] [TestCase("corpse", false)]
        public void ActorsInterleaveAtFeetAndCorpsesStayBelowStandingObjects(string role, bool tree)
        {
            var map = Terrain(tree, out _); var obstruction = Rendered(tree ? "tree" : "rock", Solid(64, 96, Color.green), new Vector2(2, 2));
            if (tree) map.RegisterForestCanopy(obstruction);
            else obstruction.sortingOrder = map.GetStandingObjectDepthSortingOrder(2);
            var actorSprite = Rendered(role, Solid(32, 64, Color.red), new Vector2(2, 2));
            ExplorationCharacterView actor = null; CharacterView growth = null; DungeonEnemyBodyView corpse = null;
            if (role is "party" or "enemy")
            {
                actor = actorSprite.gameObject.AddComponent<ExplorationCharacterView>(); Set(actor, "bodyRenderer", actorSprite); Set(actor, "terrainView", map);
                if (role == "enemy") actor.SetEnemyInstanceId("enemy:test");
            }
            else if (role == "growth")
            { growth = actorSprite.gameObject.AddComponent<CharacterView>(); Set(growth, "bodyRenderer", actorSprite); growth.BindTerrainView(map); }
            else
            { corpse = actorSprite.gameObject.AddComponent<DungeonEnemyBodyView>(); Set(corpse, "bodyRenderer", actorSprite); Set(corpse, "elevationView", map); }
            foreach (float offset in new[] { 1f / 32, 0, -1f / 32 })
            {
                actorSprite.transform.position = new Vector2(2, 2 + offset);
                if (actor != null) Call(actor, "RefreshWorldDepthSorting");
                if (growth != null) Call(growth, "LateUpdate");
                if (corpse != null) Call(corpse, "RefreshGroundSorting");
                var pixel = Capture(new Vector2(2, 3), 1.25f, $"depth-{role}-{tree}-{offset:F3}").GetPixel(128, 128);
                bool foreground = offset < 0 && role != "corpse";
                Assert.That(foreground ? pixel.r : pixel.g, Is.GreaterThan(.9f));
                Assert.That(actorSprite.color.a, Is.EqualTo(1));
            }
        }

        [TestCase(-1f / 32)] [TestCase(0)] [TestCase(1f / 32)]
        public void ForestTorchIsOccludedByForegroundTreeAndRendersBeforeBackgroundTree(float offset)
        {
            var map = Terrain(true, out _);
            var tree = Rendered("Tree at ground baseline", Solid(64, 96, Color.green), new Vector2(2, 2));
            map.RegisterForestCanopy(tree);
            var fixture = Rendered("Forest torch", Solid(32, 64, Color.red), new Vector2(2, 2 + offset));
            fixture.sharedMaterial = Keep(new Material(Shader.Find("Godmist/Dungeon/Pixel Torch")));
            var block = new MaterialPropertyBlock(); block.SetFloat("_ForestFixture", 1); block.SetFloat("_FlameBase", .68f);
            fixture.SetPropertyBlock(block);
            var rig = Keep(new GameObject("Torch depth owner")).AddComponent<DungeonSilhouetteLighting>();
            Set(rig, "terrain", map);
            var type = typeof(DungeonSilhouetteLighting).GetNestedType("Torch", BindingFlags.NonPublic);
            var torch = Activator.CreateInstance(type, true);
            type.GetField("forestFixture").SetValue(torch, true);
            type.GetField("position").SetValue(torch, new Vector2(2, 2 + offset));
            type.GetField("view").SetValue(torch, fixture);
            // Register both baselines before fetching either order: the map
            // may rebuild its depth bands when a new grounded object arrives.
            map.GetStandingObjectDepthSortingOrder(2 + offset);
            fixture.sortingLayerID = tree.sortingLayerID = map.FloorFeedbackSortingLayerID;
            fixture.sortingOrder = (int)typeof(DungeonSilhouetteLighting)
                .GetMethod("ResolveTorchSortingOrder", Flags).Invoke(rig, new[] { torch });
            var pixel = Capture(new Vector2(2, 3), 1.25f, $"torch-tree-depth-{offset:F3}").GetPixel(128, 128);
            if (offset > 0)
            {
                Assert.That(fixture.sortingOrder, Is.LessThan(tree.sortingOrder));
                Assert.That(pixel.g, Is.GreaterThan(.9f), "Foreground crown must cover the torch shader too.");
            }
            else
            {
                Assert.That(fixture.sortingOrder, Is.GreaterThan(tree.sortingOrder));
                Assert.That(pixel.g, Is.LessThan(.5f), "A tree behind the torch must not cover it.");
            }
        }

        [Test]
        public void ForestFrontRowKeepsDepthAfterAnotherBaselineAndDeepRowsCloseTheBorder()
        {
            var map = Terrain(true, out _);
            var front = Read<HashSet<SpriteRenderer>>(map, "forestFrontRowCanopies"); Assert.That(front, Is.Not.Empty);
            foreach (var tree in front.Take(12))
            {
                Assert.That(map.GetActorDepthSortingOrder(tree.transform.position.y + 1f / 32), Is.LessThan(tree.sortingOrder));
                Assert.That(map.GetActorDepthSortingOrder(tree.transform.position.y - 1f / 32), Is.GreaterThan(tree.sortingOrder));
            }
            map.GetStandingObjectDepthSortingOrder(13.123f);
            Assert.That(Read<HashSet<SpriteRenderer>>(map, "vegetationGroundRenderers").All(r => r.sortingOrder == map.GetFloorFeedbackDepthSortingOrder() - 2), Is.True, "Soil under trees must follow the floor band, never cover an actor.");
            foreach (var tree in front.Take(12))
                Assert.That(map.GetActorDepthSortingOrder(tree.transform.position.y - 1f / 32), Is.GreaterThan(tree.sortingOrder));
            var trees = map.LightingWallRenderers.Where(r => r != null && r.name == "Boundary vegetation").ToArray();
            Assert.That(trees.Any(r => r.transform.position.y < -5 || r.transform.position.y > 10), Is.True);
            Assert.That(trees.All(r => r.transform.localScale == Vector3.one), Is.True);
            Assert.That(Read<int>(map, "depthSortingBuildCount"), Is.LessThan(8), "Tree baselines must be reserved together, not rebuild the map once per tree.");
        }

        [Test]
        public void ForestVoidDoesNotPaintBlackFogOutlineButUnknownFloorStillDoes()
        {
            var map = Terrain(true, out var floor);
            var projection = floor.Map.FloorCells.ToDictionary(c => c, _ => DungeonKnowledge.Visible);
            var method = typeof(DungeonFloorMapView).GetMethod("ReadFogNeighbours", Flags);
            Assert.That((bool)method.Invoke(map, new object[] { projection, Vector2Int.zero, DungeonKnowledge.Visible }), Is.False);
            projection.Remove(Vector2Int.right);
            Assert.That((bool)method.Invoke(map, new object[] { projection, Vector2Int.zero, DungeonKnowledge.Visible }), Is.True,
                "Removing the artificial void outline cannot reveal an unknown walkable neighbour.");
        }

        [Test]
        public void StaticForestBoundaryRemainsVisibleAcrossDiscoveryAndFogReset()
        {
            var map = Terrain(true, out var floor);
            map.ApplyFog(floor);
            var trees = map.LightingWallRenderers.Where(r => r != null && r.name == "Boundary vegetation").ToArray();
            Assert.That(trees, Is.Not.Empty);
            // Static trees are independent of discovery; walkable routes and units
            // keep their own fog gates. This replaces the old discovery-only rule.
            Assert.That(trees.All(r => !r.forceRenderingOff), Is.True, "Static trees do not disappear in fog.");
            floor.Fog.SetVisible(floor.Map.FloorCells.Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
            map.ApplyFog(floor);
            Assert.That(trees.All(r => !r.forceRenderingOff), Is.True);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            map.ApplyFog(floor);
            Assert.That(trees.All(r => !r.forceRenderingOff), Is.True, "All discovered boundary rows survive leaving FOV.");
            var soil = Read<HashSet<SpriteRenderer>>(map, "vegetationGroundRenderers");
            Assert.That(soil.All(r => !r.forceRenderingOff), Is.True, "Remembered woodland cannot lose its underlay into black squares.");
            Assert.That(floor.Fog.DebugFogDisabled, Is.False);
            floor.Fog.SetMapDiscovered(false); map.ApplyFog(floor);
            Assert.That(trees.All(r => !r.forceRenderingOff), Is.True, "Resetting discovery cannot hide static trees.");
        }

        [TestCase(false)] [TestCase(true)]
        public void InvisibleActorPixelsDisappearOnlyForEnemiesAndReturnOnReveal(bool player)
        {
            var body = Rendered("invisibility actor", Solid(32,64,Color.white), Vector2.zero);
            var actor = body.gameObject.AddComponent<ExplorationCharacterView>(); Set(actor,"bodyRenderer",body);
            var character = new Godmist.Runtime.Characters.CharacterInstance();
            var unit = new Godmist.Runtime.Combat.BattleUnit {Character=character,IsPlayerSide=player};
            character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
                EffectId=Godmist.Core.Constants.StatusEffectIds.Invisibility,
                Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=2});
            actor.Bind(character);if(!player) actor.SetEnemyInstanceId("hidden");actor.BindCombat(unit);
            Call(actor,"LateUpdate");
            var hidden = Capture(Vector2.up,1.5f,"invisibility-"+(player?"ally":"enemy"),Color.black);
            var hiddenPixel=hidden.GetPixel(128,128).r;
            if(player) Assert.That(hiddenPixel,Is.GreaterThan(.1f).And.LessThan(.9f));
            else Assert.That(hiddenPixel,Is.LessThan(.01f));
            character.PassiveEffects.Clear();Call(actor,"LateUpdate");
            var revealed=Capture(Vector2.up,1.5f,"invisibility-revealed-"+(player?"ally":"enemy"),Color.black);
            Assert.That(revealed.GetPixel(128,128).r,Is.GreaterThan(.95f));
        }

        [Test]
        public void ForestCornerSharesKnowledgeWithNeighbouringEdges()
        {
            var map = Terrain(true, out var floor); map.ApplyFog(floor);
            var owners = Read<Dictionary<Renderer, DungeonSurfaceEdge[]>>(map, "forestBoundaryOwners");
            var crown = owners.First(p => p.Value.SelectMany(e => new[] { e.FirstCell, e.SecondCell })
                .Where(c => floor.Map.IsWalkable(c)).Distinct().Count() > 1);
            var cell = crown.Value.SelectMany(e => new[] { e.FirstCell, e.SecondCell })
                .First(c => floor.Map.IsWalkable(c));
            floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(cell)) });
            map.ApplyFog(floor);
            Assert.That(crown.Key.forceRenderingOff, Is.False);
            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); map.ApplyFog(floor);
            Assert.That(crown.Key.forceRenderingOff, Is.False, "Neighbour knowledge must invalidate a crown's original BFS owner.");
        }

        [Test]
        public void MatureTreeImportPreservesOneCellTrunkAndNativeScale()
        {
            var data = AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var trees = new[] { data.BoundaryTree }.Concat(data.TreeVariants).ToArray();
            Assert.That(trees.Select(s => s.name).Distinct().Count(), Is.EqualTo(5), "Only the five new mature variants belong in the tree pool.");
            Assert.That(trees.Select(s => s.rect.height).Distinct().Count(), Is.GreaterThanOrEqualTo(2));
            foreach (var sprite in trees.Take(4))
            {
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(160,192)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(80,8)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                var texture = Keep(new Texture2D(2,2));
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                // Cross-section above flared roots, below the lowest branches.
                Assert.That(texture.GetPixel(80,36).a, Is.GreaterThan(.5f));
                var left=80; var right=80;
                while (left>0 && texture.GetPixel(left-1,36).a>.5f) left--;
                while (right<159 && texture.GetPixel(right+1,36).a>.5f) right++;
                Assert.That(right-left+1, Is.InRange(11,32), sprite.name + ": core trunk must fit one grid cell.");
                Assert.That(left, Is.GreaterThanOrEqualTo(64)); Assert.That(right, Is.LessThan(96));
            }
            var obstacle = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Tree_Prototype.asset");
            Assert.That(obstacle.TrunkRadius*2, Is.LessThanOrEqualTo(1));
            Assert.That(data.TreeVisualOffsetInCells, Is.EqualTo(Vector2.zero), "Native ground pivot replaces the old half-cell drawing offset.");
            Assert.That(trees.Last().rect.size, Is.EqualTo(new Vector2(192,256)));
            Assert.That(obstacle.PresentationVariants.Contains(trees.Last()), Is.False, "Rare 2x2 trunks cannot use a one-cell interior collider.");
        }

        [Test]
        public void RareWideTreesKeepTwoCellRootsOutsidePathsAndApartFromEachOther()
        {
            var map = Terrain(true, out var floor);
            var wide = Read<HashSet<SpriteRenderer>>(map, "forestWideTrunks").ToArray();
            Assert.That(wide, Is.Not.Empty);
            foreach (var tree in wide)
            {
                Assert.That(map.ForestCanopyRootHeightInCells(tree), Is.EqualTo(2));
                foreach (var x in new[] { -.5f,.5f }) foreach (var y in new[] { .5f,1.5f })
                    Assert.That(floor.Map.IsWalkable(map.WorldToCell(tree.transform.position + new Vector3(x,y))), Is.False);
                foreach (var other in wide)
                    if (other != tree) Assert.That(Vector2.Distance(tree.transform.position,other.transform.position), Is.GreaterThanOrEqualTo(6));
            }
        }

        [Test]
        public void SeenDistantStaticObjectsRemainKnownWithoutRevealingUnseenState()
        {
            var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
            for (int y = 0; y < 10; y++) for (int x = 0; x < 30; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room"); floor.TrySetMap(map);
            var definition = Keep(ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>());
            typeof(DungeonObjectDefinition).GetProperty("Id").SetValue(definition, "object.obstacle.sarcophagus");
            var obstacle = new ObstacleDungeonObjectInstance("remembered.rock", definition, new Vector2Int(20, 4)); floor.TryAddObject(obstacle);
            var fog = new DungeonFogSystem(); var observer = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(1, 4)));
            fog.Update(floor, new[] { observer }); Assert.That(floor.Fog.Objects[obstacle.InstanceId].Active, Is.True);
            Assert.That(floor.Fog.Discovered, Does.Contain(new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(20, 5)))));
            Assert.That(floor.Fog.Discovered, Has.No.Member(new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(18, 4)))), "Unrelated distant floor retains its existing discovery rules.");
            for (int y = 0; y < 10; y++) map.TryAddWall(new Vector2Int(10, y), new Vector2Int(11, y));
            obstacle.TryDeactivate(); fog.Update(floor, new[] { observer });
            Assert.That(floor.Fog.Objects[obstacle.InstanceId].Active, Is.True, "Offscreen destruction cannot leak through remembered presentation.");
            Assert.That(floor.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(obstacle.Position))), Is.EqualTo(DungeonKnowledge.Remembered));
            var restored = new DungeonFloorInstance(0); restored.TrySetMap(map); restored.TryAddObject(obstacle);
            Assert.That(DungeonFogPersistence.Restore(restored, DungeonFogPersistence.Capture(floor)), Is.True);
            new DungeonFogSystem().Update(restored, Array.Empty<DungeonTraversalPosition>());
            Assert.That(restored.Fog.Objects[obstacle.InstanceId].Active, Is.True);
            Assert.That(restored.Fog.Get(new DungeonTraversalPosition(new DungeonSurfacePosition(obstacle.Position))), Is.EqualTo(DungeonKnowledge.Remembered));
            fog.Update(floor, new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(22, 4))) });
            Assert.That(floor.Fog.Objects[obstacle.InstanceId].Active, Is.False);
            floor.Fog.SetMapDiscovered(false); Assert.That(floor.Fog.Objects, Is.Empty);
        }

        [Test]
        public void AnyVisibleFootprintCellRevealsTheStaticObject()
        {
            var floor = new DungeonFloorInstance(0); var definition = Keep(ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>());
            typeof(DungeonObjectDefinition).GetProperty("Id").SetValue(definition, "object.obstacle.sarcophagus");
            var obstacle = new ObstacleDungeonObjectInstance("footprint.rock", definition, Vector2Int.zero);
            floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.up)) });
            Assert.That(DungeonFogSystem.IsObjectVisible(floor, obstacle), Is.True);
        }

        [Test]
        public void RememberedStaticSpriteRemainsPresentedButUnknownObstacleDoesNot()
        {
            var map = Terrain(false, out var floor);
            var definition = Keep(ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>());
            typeof(DungeonObjectDefinition).GetProperty("Id").SetValue(definition, "object.obstacle.column");
            var obstacle = new ObstacleDungeonObjectInstance("known.column", definition, new Vector2Int(2, 2)); floor.TryAddObject(obstacle);
            var renderer = Rendered("remembered column", Solid(32, 64, Color.green), Vector2.zero);
            var view = renderer.gameObject.AddComponent<DungeonObstacleView>(); view.Initialize(obstacle, map.CellToWorldCenter(obstacle.Position)); view.BindElevationView(map);
            var fog = new DungeonFogSystem(); fog.Update(floor, new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(obstacle.Position)) });
            map.ApplyFog(floor); Call(view, "LateUpdate"); Assert.That(renderer.forceRenderingOff, Is.False);
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>()); map.ApplyFog(floor); Call(view, "LateUpdate");
            Assert.That(renderer.forceRenderingOff, Is.False); Assert.That(view.IsLightingPresentationVisible, Is.True);
            Assert.That(view.IsElevationVisible, Is.False, "Remembering a static sprite does not grant live interaction/LOS.");
            floor.Fog.SetMapDiscovered(false); map.ApplyFog(floor); Call(view, "LateUpdate");
            Assert.That(renderer.forceRenderingOff, Is.True);
        }

        [Test]
        public void SummonDepthGroupRestoresAuthoredStateAndCanBeRecreated()
        {
            var map = Terrain(false, out _);
            var sprite = Rendered("summon", Solid(16, 32, Color.red), new Vector2(2, 2));
            var original = sprite.gameObject.AddComponent<SortingGroup>(); original.sortingOrder = 17;
            var view = sprite.gameObject.AddComponent<CharacterView>(); Set(view, "bodyRenderer", sprite); view.BindTerrainView(map); Call(view, "LateUpdate");
            Assert.That(original.sortingOrder, Is.EqualTo(map.GetActorDepthSortingOrder(2)));
            view.BindTerrainView(null); Assert.That(original.enabled, Is.True); Assert.That(original.sortingOrder, Is.EqualTo(17));
            Object.DestroyImmediate(original); view.BindTerrainView(map); Call(view, "LateUpdate");
            var replacement = view.GetComponent<SortingGroup>(); Assert.That(replacement != null && replacement.enabled, Is.True);
            view.BindTerrainView(null); Assert.That(replacement.enabled, Is.False);
        }

        [Test]
        public void ReadabilityDoesNotSampleNeighbouringAtlasSprite()
        {
            var texture = Keep(new Texture2D(32, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point });
            var pixels = new Color[32 * 16];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++) pixels[y * 32 + x] = x >= 16 ? Color.red : x == 2 && y == 8 ? Color.green : Color.clear;
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Keep(Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect));
            var renderer = Rendered("atlas left sprite", sprite, Vector2.zero);
            var material = Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body"))); renderer.sharedMaterial = material;
            material.SetFloat("_Ambient", 1); material.SetFloat("_ForestPropReadability", .55f); material.SetFloat("_ForestReadabilityProfile", 1);
            material.SetVector("_ForestPropUV", new Vector4(0, 0, .5f, 1)); material.SetVector("_ForestPropTexel", new Vector4(1f / 32, 1f / 16, 0, 0));
            var image = Capture(new Vector2(0, .25f), .5f, "atlas-no-neighbour-leak");
            Assert.That(image.GetPixel(187, 128), Is.EqualTo(image.GetPixel(0, 0)), "The transparent right edge cannot borrow neighbouring atlas alpha.");
            Assert.That(image.GetPixels().Any(p => p.g > .5f), Is.True, "The test sprite itself must render.");
        }

        [TestCase("Godmist/Dungeon/Torch Wall")] [TestCase("Godmist/Dungeon/Torch Body")]
        public void CanopyRevealsOnlyAboveRootCellAndKeepsActorAlpha(string shaderName)
        {
            var actor = Rendered("opaque party", Solid(32, 64, Color.red), new Vector2(0, -1)); actor.sortingOrder = 1;
            var tree = Rendered("foreground canopy", Solid(96, 96, Color.green), new Vector2(0, -1.5f)); tree.sortingOrder = 2;
            var material = Keep(new Material(Shader.Find(shaderName))); tree.sharedMaterial = material;
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            material.SetFloat("_Ambient", 1); material.SetInt("_TorchCount", 0); material.SetFloat("_ForestCanopy", 1);
            material.SetFloat("_ForestCanopyFade", .8f);
            material.SetFloat("_ForestCanopyRootTopY", -.5f);
            material.SetFloat("_ForestCanopyCellSize", 1);
            var revealed = Capture(Vector2.zero, 1.6f, "local-reveal-" + material.shader.name.Split('/').Last());
            Assert.That(revealed.GetPixel(128, 128).linear.r, Is.InRange(.75f, .85f), "The actor must remain readable through foreground leaves.");
            Assert.That(revealed.GetPixel(128, 128).linear.g, Is.InRange(.15f, .25f), "Leaves remain present over the actor.");
            Assert.That(revealed.GetPixel(128, 48).g, Is.GreaterThan(.7f), "The rooted first cell must not fade.");
            Assert.That(revealed.GetPixel(48, 128).g, Is.LessThan(.7f), "The whole crown remains partially visible.");
            Assert.That(actor.color.a, Is.EqualTo(1));
            material.SetFloat("_ForestCanopyFade", 0);
            Assert.That(Capture(Vector2.zero, 1.6f, "no-eligible-party").GetPixel(128, 128).g, Is.GreaterThan(.7f));
        }

        [TestCase("Godmist/Dungeon/Torch Wall")] [TestCase("Godmist/Dungeon/Torch Body")]
        public void WholeCrownFadesUniformlyRatherThanCuttingAnActorRectangle(string shaderName)
        {
            var tree = Rendered("Wide foreground crown", Solid(384, 160, Color.green), new Vector2(0, -1.5f));
            var material = Keep(new Material(Shader.Find(shaderName))); tree.sharedMaterial = material;
            material.SetFloat("_Ambient", 1); material.SetFloat("_ForestCanopy", 1);
            material.SetFloat("_ForestCanopyRootTopY", -.5f);
            material.SetFloat("_ForestCanopyCellSize", 1); material.SetFloat("_ForestCanopyFade", .8f);
            // Padded body bounds must not expand the reveal across the path.
            var image = Capture(Vector2.zero, 6, "wide-crown-local-" + material.shader.name.Split('/').Last());
            Assert.That(image.GetPixel(128,128).linear.g, Is.InRange(.15f,.3f));
            material.SetFloat("_ForestCanopyFade", 0);
            var opaque = Capture(Vector2.zero,6,"wide-crown-solid-" + material.shader.name.Split('/').Last());
            Assert.That(image.GetPixel(213,128), Is.Not.EqualTo(opaque.GetPixel(213,128)), "Far leaves on this same overlapping crown share its fade.");
            Assert.That(image.GetPixel(158,128), Is.Not.EqualTo(opaque.GetPixel(158,128)), "The whole crown fades without an actor-shaped cutout.");
        }

        [Test]
        public void BoundaryTreesHaveSizeAwareSpacingAndRebuildDeterministically()
        {
            var map = Terrain(true, out var floor);
            SpriteRenderer[] Trees() => map.LightingWallRenderers.OfType<SpriteRenderer>().Where(r => r.name == "Boundary vegetation").ToArray();
            var trees = Trees();
            Assert.That(trees, Is.Not.Empty);
            AssertTreeFootprintsAreExclusive(map,floor);
            foreach (var a in trees.Where(t=>t.sprite.bounds.size.x>=3)) foreach (var b in trees.Where(t=>t.sprite.bounds.size.x>=3))
            {
                if (a == b) continue;
                float Radius(SpriteRenderer r) => r.sprite.name.Contains("ancient") ? 1.5f : .95f;
                Assert.That(Vector2.Distance(a.transform.position,b.transform.position), Is.GreaterThanOrEqualTo(Radius(a)+Radius(b)-.001f));
            }
            string[] Snapshot() => Trees().Select(r => r.transform.position.ToString("F4") + ":" + r.sprite.name).OrderBy(s => s).ToArray();
            var before = Snapshot(); map.Rebuild(floor.Map);
            Assert.That(Snapshot(), Is.EqualTo(before), "Loading/rebuilding must preserve the same silhouettes and spacing.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"glade-tree-count.txt"), trees.Length.ToString());
        }

        void AssertMatureCrownsLeaveTrailCentresClear(DungeonFloorMapView map, DungeonFloorInstance floor)
        {
            AssertTreeFootprintsAreExclusive(map,floor);
            var width=Vector2.Distance(map.CellToWorldCenter(Vector2Int.zero),map.CellToWorldCenter(Vector2Int.right));
            var trees=map.LightingWallRenderers.OfType<SpriteRenderer>().Where(r=>r.name=="Boundary vegetation").ToArray();
            var mature=trees.Where(r=>r.sprite.bounds.size.x>=width*3).ToArray();
            Assert.That(mature,Is.Not.Empty,"Large trees should still form the deeper forest.");
            var front=Read<HashSet<SpriteRenderer>>(map,"forestFrontRowCanopies");
            Assert.That(front,Is.Not.Empty);
            Assert.That(trees.All(r=>r.sprite.name.StartsWith("forest-tree-mature-",StringComparison.Ordinal) || r.sprite.name.StartsWith("forest-tree-compact-",StringComparison.Ordinal)),Is.True,"Use new mature and compact art, never legacy saplings.");
            foreach(var tree in mature)
            {
                var bounds=tree.bounds;var first=map.WorldToCell(bounds.min);var last=map.WorldToCell(bounds.max);
                for(var y=first.y;y<=last.y;y++) for(var x=first.x;x<=last.x;x++)
                {
                    var cell=new Vector2Int(x,y);
                    if(!floor.Map.IsWalkable(cell))continue;
                    var core=new Bounds(map.CellToWorldCenter(cell),new Vector3(width*.6f,width*.6f,1));
                    Assert.That(bounds.Intersects(core),Is.False,tree.sprite.name+" covers the route centre at "+cell);
                }
            }
        }

        void AssertTreeFootprintsAreExclusive(DungeonFloorMapView map,DungeonFloorInstance floor)
        {
            var reservations=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
            var occupied=new HashSet<Vector2Int>();
            Assert.That(reservations,Is.Not.Empty);
            foreach(var pair in reservations)
            {
                var large=pair.Key.sprite.bounds.size.x>=3;
                Assert.That(pair.Value.size,Is.EqualTo(Vector2Int.one*(large?2:1)),"Every large tree must reserve 2x2; small trees keep 1x1.");
                Assert.That(pair.Value.Contains(map.WorldToCell(pair.Key.transform.position)),Is.True,"The visible trunk must stand inside its reserved ground.");
                foreach(var cell in pair.Value.allPositionsWithin)
                {
                    Assert.That(floor.Map.IsWalkable(cell),Is.False,"Tree reservations must stay outside the route.");
                    Assert.That(occupied.Add(cell),Is.True,"Two trees share reserved ground at "+cell);
                }
            }
        }

        [Test]
        public void AllLargeTreesReserveFourExclusiveGroundCells()
        {
            var map=Terrain(true,out var floor);
            AssertTreeFootprintsAreExclusive(map,floor);
            var footprints=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
            Assert.That(footprints.Any(p=>p.Value.size==new Vector2Int(2,2) && !p.Key.sprite.name.Contains("ancient")),Is.True,
                "The 2x2 rule must cover ordinary mature trees, not just rare ancient ones.");
            Assert.That(footprints.Where(p=>p.Value.size==new Vector2Int(2,2)).All(p=>map.ForestCanopyRootHeightInCells(p.Key)==2),Is.True);
        }

        [Test]
        public void TreePoolsNeverSelectLegacyLowThinSprites()
        {
            var data=AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var obstacle=AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Tree_Prototype.asset");
            foreach(var noise in Enumerable.Range(0,4096).Select(n=>(uint)n))
                foreach(var sprite in new[]{DungeonVegetationPresentation.SelectTreeSprite(data.BoundaryTree,data.TreeVariants,noise),
                    DungeonVegetationPresentation.SelectTreeSprite(obstacle.PresentationSprite,obstacle.PresentationVariants,noise)})
                    Assert.That(sprite.name,Does.StartWith("forest-tree-mature-"),"Available space changes placement, never substitutes a legacy sapling.");
        }

        [Test]
        public void CompactTreesFillBoundaryGapsAfterLargeReservationsWithoutDisplacingThem()
        {
            var map=Terrain(true,out var floor);
            var source=AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var presentation=Keep(Object.Instantiate(source));
            map.ConfigureVegetationPresentation(presentation);map.Rebuild(floor.Map);
            var footprints=Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints");
            AssertTreeFootprintsAreExclusive(map,floor);
            Assert.That(footprints.Any(p=>p.Value.size==Vector2Int.one),Is.True,"Residual cells must get compact trees.");
            var occupied=new HashSet<Vector2Int>();
            foreach(var rectangle in footprints.Values)foreach(var cell in rectangle.allPositionsWithin)occupied.Add(cell);
            foreach(var cell in floor.Map.FloorCells)
                foreach(var direction in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                    if(!floor.Map.IsWalkable(cell+direction))Assert.That(occupied.Contains(cell+direction),Is.True,"Unfilled blocking edge at "+(cell+direction));
            string[] Large()=>Read<Dictionary<SpriteRenderer,RectInt>>(map,"forestTreeFootprints")
                .Where(p=>p.Value.size==new Vector2Int(2,2)).Select(p=>p.Value.ToString()+":"+p.Key.sprite.name+":"+p.Key.transform.position.ToString("F4")).OrderBy(s=>s).ToArray();
            var before=Large();
            Set(presentation,"<SmallTreeVariants>k__BackingField",Array.Empty<Sprite>());map.Rebuild(floor.Map);
            Assert.That(Large(),Is.EqualTo(before),"Compact trees run after large placement, not as competing candidates.");
        }

        [Test]
        public void CompactTreeImportHasNativePixelsAndRootsInsideOneCell()
        {
            var presentation=AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            Assert.That(presentation.SmallTreeVariants,Is.Not.Empty);
            foreach(var sprite in presentation.SmallTreeVariants)
            {
                Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(80,128)));
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(40,8)));
                var path=AssetDatabase.GetAssetPath(sprite);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
                var image=Keep(new Texture2D(2,2));image.LoadImage(File.ReadAllBytes(path));
                var occupied=0;
                for(var y=8;y<32;y++)for(var x=0;x<80;x++)if(image.GetPixel(x,y).a>.5f)
                { occupied++;Assert.That(x,Is.InRange(24,55),"Roots must fit the actual 32-pixel ground cell."); }
                Assert.That(occupied,Is.GreaterThan(40));
            }
        }

        [Test]
        public void VisibleCombatEnemiesRevealCanopiesWhileHiddenAndExploringEnemiesDoNot()
        {
            var map=Terrain(true,out _);
            var root=Keep(new GameObject("Combat canopy recipients"));
            var rig=root.AddComponent<DungeonSilhouetteLighting>();
            var party=root.AddComponent<ExplorationPartyController>();
            var encounters=root.AddComponent<DungeonEncounterSpawner>();
            Set(rig,"terrain",map);Set(rig,"party",party);Set(rig,"encounters",encounters);
            var material=Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body")));Set(rig,"bodyMaterial",material);
            var enemies=Read<List<ExplorationCharacterView>>(encounters,"spawnedEnemyViews");
            for(var i=0;i<6;i++)
            {
                var sprite=Rendered("enemy "+i,Solid(16,32,Color.red),new Vector2(2,2));
                var actor=sprite.gameObject.AddComponent<ExplorationCharacterView>();Set(actor,"bodyRenderer",sprite);
                actor.BindCombat(new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance{CurrentHealth=100}});
                enemies.Add(actor);
            }
            Call(rig,"RefreshForestPartyReveal");
            Assert.That(rig.LastForestCombatRevealCount,Is.EqualTo(6));
            Assert.That(rig.LastForestPartyRevealCount,Is.EqualTo(6),"Visible recipients are selected on CPU, without per-pixel actor arrays.");
            enemies[0].BodyRenderer.forceRenderingOff=true;
            enemies[1].ReleaseCombat();
            enemies[2].CombatUnit.Character.CurrentHealth=0;
            Call(rig,"RefreshForestPartyReveal");
            Assert.That(rig.LastForestCombatRevealCount,Is.EqualTo(3));
            Assert.That(rig.LastForestPartyRevealCount,Is.EqualTo(3));
        }

        [TestCase("Godmist/Dungeon/Torch Wall")] [TestCase("Godmist/Dungeon/Torch Body")]
        public void CombatRecipientBeyondTheFourPartySlotsRevealsActualCanopyPixels(string shaderName)
        {
            var map=Terrain(false,out _);map.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
            var root=Keep(new GameObject("Sixth combat recipient"));var rig=root.AddComponent<DungeonSilhouetteLighting>();
            var party=root.AddComponent<ExplorationPartyController>();var encounters=root.AddComponent<DungeonEncounterSpawner>();
            Set(rig,"terrain",map);Set(rig,"party",party);Set(rig,"encounters",encounters);
            var enemies=Read<List<ExplorationCharacterView>>(encounters,"spawnedEnemyViews");
            for(var i=0;i<6;i++)
            {
                var sr=Rendered("enemy "+i,Solid(32,64,Color.red),i==5?new Vector2(2,2):new Vector2(100+i,100));
                var actor=sr.gameObject.AddComponent<ExplorationCharacterView>();Set(actor,"bodyRenderer",sr);
                actor.BindCombat(new Godmist.Runtime.Combat.BattleUnit{Character=new Godmist.Runtime.Characters.CharacterInstance{CurrentHealth=100}});enemies.Add(actor);
            }
            var tree=Rendered("canopy",Solid(96,96,Color.green),new Vector2(2,.5f));map.RegisterForestCanopy(tree);
            enemies[5].BodyRenderer.sortingOrder=map.GetActorDepthSortingOrder(2);
            var material=Keep(new Material(Shader.Find(shaderName)));tree.sharedMaterial=material;
            material.SetFloat("_Ambient",1);material.SetFloat("_ForestCanopy",1);material.SetFloat("_ForestCanopyRootTopY",1.5f);material.SetFloat("_ForestCanopyCellSize",1);
            Call(rig,"RefreshForestPartyReveal");typeof(DungeonSilhouetteLighting).GetMethod("AdvanceForestCanopyFade",Flags).Invoke(rig,new object[]{.5f});
            Assert.That(rig.LastForestCombatRevealCount,Is.EqualTo(6));
            var visible=Capture(new Vector2(2,2.5f),1.6f,"combat-sixth-whole-crown-"+material.shader.name.Split('/').Last());
            Assert.That(visible.GetPixel(128,128).linear.r,Is.InRange(.60f,.70f));
            enemies[5].ReleaseCombat();Call(rig,"RefreshForestPartyReveal");typeof(DungeonSilhouetteLighting).GetMethod("AdvanceForestCanopyFade",Flags).Invoke(rig,new object[]{.5f});
            Assert.That(Capture(new Vector2(2,2.5f),1.6f,"combat-without-recipient-"+material.shader.name.Split('/').Last()).GetPixel(128,128).g,Is.GreaterThan(.7f));
        }

        [TestCase(false)] [TestCase(true)]
        public void MatureCrownsLeaveTwoCellCorridorsReadableBeforePartyApproaches(bool vertical)
        {
            var cells=Enumerable.Range(0,12).SelectMany(a=>Enumerable.Range(0,2).Select(b=>vertical?new Vector2Int(b,a):new Vector2Int(a,b)));
            var map=Terrain(true,out var floor,cells);
            AssertMatureCrownsLeaveTrailCentresClear(map,floor);
            Assert.That(floor.Map.FloorCells.Count(),Is.EqualTo(24),"Presentation must not widen the dungeon or its navigation.");
        }

        [Test]
        public void NativeWholeCrownRemainsLeafyWhileFaded()
        {
            var tree = Rendered("Native oak crown", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-oak-v2-32ppu.png"), new Vector2(0,-3));
            tree.sortingOrder = 2;
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var actor = Rendered("Native actor", definition.VisualSprite, new Vector2(0,-1)); actor.sortingOrder = 1; actor.transform.localScale = Vector3.one * .7f;
            var material = Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body"))); tree.sharedMaterial = material;
            material.SetFloat("_Ambient", .8f); material.SetFloat("_ForestCanopy", 1);
            material.SetFloat("_ForestCanopyRootTopY", -2);
            material.SetFloat("_ForestCanopyCellSize", 1); material.SetFloat("_ForestCanopyFade", .8f);
            var bounds = actor.bounds;
            var near = Capture(Vector2.zero,4,"native-oak-local-transparency");
            material.SetFloat("_ForestCanopyFade", 0);
            var solid = Capture(Vector2.zero,4,"native-oak-solid");
            var solidPixels = solid.GetPixels32();
            Assert.That(near.GetPixels32().Where((pixel,index) => !pixel.Equals(solidPixels[index])).Count(), Is.GreaterThan(30));
            Assert.That(near.GetPixels32().Where((pixel,index)=>Mathf.Abs(index%near.width-near.width/2)>25 && !pixel.Equals(solidPixels[index])).Count(),Is.GreaterThan(30),"Leaf pixels outside the narrow actor silhouette share the whole-crown fade; transparent padding does not count.");
        }

        [TestCase("Godmist/Dungeon/Torch Body",1)] [TestCase("Godmist/Dungeon/Torch Body",4)]
        [TestCase("Godmist/Dungeon/Torch Body",6)] [TestCase("Godmist/Dungeon/Torch Wall",1)]
        [TestCase("Godmist/Dungeon/Torch Wall",4)] [TestCase("Godmist/Dungeon/Torch Wall",6)]
        public void OverlappingCrownsRetainFixedLeafAlphaAcrossWholeBody(string shaderName, int layers)
        {
            var actor = Rendered("Opaque party below overlapping crowns",Solid(32,64,Color.red),new Vector2(0,-1)); actor.sortingOrder=1;
            var material = Keep(new Material(Shader.Find(shaderName)));
            material.SetFloat("_Ambient",1); material.SetFloat("_ForestCanopy",1);
            material.SetFloat("_ForestCanopyRootTopY",-.5f);
            material.SetFloat("_ForestCanopyCellSize",1); material.SetFloat("_ForestCanopyFade",.8f);
            for(var index=0;index<layers;index++)
            {
                var tree=Rendered("Overlapping canopy "+index,Solid(96,96,Color.green),new Vector2(0,-1.5f));
                tree.sortingOrder=2+index;tree.sharedMaterial=material;
            }
            var label=material.shader.name.Split('/').Last()+"-"+layers;
            material.SetFloat("_ForestCanopyFade",.5f);
            var before=Capture(Vector2.zero,1.6f,"overlap-before-"+label);
            Assert.That(before.GetPixel(128,128).linear.r,Is.EqualTo(Mathf.Pow(.5f,layers)).Within(.025f),"Reproduce the previous stacked-cover failure.");
            var opacity=90f/255;
            material.SetFloat("_ForestCanopyFade",1-opacity);
            var after=Capture(Vector2.zero,1.6f,"overlap-after-"+label);
            var opaqueLeafGreen=after.GetPixel(128,48).linear.g;
            foreach(var point in new[]{new Vector2Int(128,128),new Vector2Int(128,185),new Vector2Int(156,170)})
            {
                var pixel=after.GetPixel(point.x,point.y).linear;
                var transmission=Mathf.Pow(1-opacity,layers);
                Assert.That(pixel.r,Is.EqualTo(transmission).Within(.035f),"Each overlapping crown retains 90/255 alpha.");
                Assert.That(pixel.g,Is.EqualTo(opaqueLeafGreen*(1-transmission)).Within(.025f),"Overlapping leaves retain their lit color at the authored alpha.");
            }
            Assert.That(after.GetPixel(128,48).g,Is.GreaterThan(.7f),"The protected roots do not inherit the body reveal.");
            Assert.That(after.GetPixel(40,128),Is.Not.EqualTo(before.GetPixel(40,128)),"The same budget applies across each whole crown.");
            Assert.That(actor.color.a,Is.EqualTo(1));
        }

        [TestCase(1)] [TestCase(4)] [TestCase(6)]
        public void RevealSelectionCountsOnlyLocalVisibleForegroundCanopies(int layers)
        {
            var map=Terrain(false,out _);
            map.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
            SpriteRenderer Tree(string name,Vector2 feet)
            {
                var tree=Rendered(name,Solid(96,128,Color.green),feet);
                map.RegisterForestCanopy(tree);tree.sortingOrder=map.GetWallMountedDepthSortingOrder(feet.y);return tree;
            }
            for(var index=0;index<layers;index++)Tree("Foreground "+index,new Vector2(2,1));
            Tree("Behind party",new Vector2(2,3));
            Tree("Hidden tree",new Vector2(2,1)).forceRenderingOff=true;
            Tree("Distant tree",new Vector2(20,1));
            var rig=Keep(new GameObject("Canopy budget owner")).AddComponent<DungeonSilhouetteLighting>();Set(rig,"terrain",map);
            var candidates=new List<SpriteRenderer>();
            map.CollectForestRevealCanopies(new Vector2Int(2,1),new Bounds(new Vector3(2,2.5f,0),new Vector3(1,2,0)),1.5f,candidates);
            Assert.That(candidates.Count,Is.EqualTo(layers),"Hidden, background and distant trees cannot enter the reveal selection.");
        }

        [TestCase(1f)] [TestCase(2f)]
        public void ReadabilityPreservesAuthoredDarkEdgeInsteadOfPaintingBrightBorder(float profile)
        {
            var renderer = Rendered("authored dark edge", Solid(16, 16, new Color(.04f, .03f, .02f, 1)), Vector2.zero);
            var material = Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body"))); renderer.sharedMaterial = material;
            material.SetFloat("_Ambient", 1); material.SetFloat("_ForestReadabilityProfile", profile);
            material.SetVector("_ForestPropUV", new Vector4(0, 0, 1, 1)); material.SetVector("_ForestPropTexel", new Vector4(1f / 16, 1f / 16, 0, 0));
            var baseline = Capture(new Vector2(0, .25f), .5f, "authored-edge-baseline");
            material.SetFloat("_ForestPropReadability", .55f);
            var separated = Capture(new Vector2(0, .25f), .5f, "authored-edge-separated");
            var gain = profile < 1.5f ? 1.16f : 1.18f;
            Assert.That(separated.GetPixel(65, 128).r, Is.EqualTo(baseline.GetPixel(65, 128).r * gain).Within(1.1f / 255), "A dark opaque edge cannot be replaced with a constant pale contour.");
            Assert.That(separated.GetPixel(60, 128).r, Is.LessThanOrEqualTo(baseline.GetPixel(60, 128).r + (profile < 1.5f ? .06f : 0)), "The actor rim may separate a dark silhouette but must not become a bright selection contour; props retain their dark rim.");
            Assert.That(renderer.color.a, Is.EqualTo(1));
        }

        [Test]
        public void ReadabilityCloseupAgainstActualForestGround()
        {
            var terrain = Terrain(true, out _);
            var presentation = AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var ground = Rendered("Actual forest soil", ((Tile)presentation.GroundTile).sprite, Vector2.zero);
            ground.drawMode = SpriteDrawMode.Tiled; ground.size = new Vector2(8, 8); ground.sortingOrder = -100;
            ground.transform.position += new Vector3(2.5f, 2.5f, 0) - ground.bounds.center;
            var soilMaterial = Keep(new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"))); soilMaterial.SetFloat("_Ambient", .7f); ground.sharedMaterial = soilMaterial;
            terrain.ConfigureForestGroundMaterial(soilMaterial);
            Assert.That(soilMaterial.GetFloat("_ForestSurfaceEnabled"), Is.EqualTo(1));
            var material = Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body"))); material.SetFloat("_Ambient", .7f);
            material.SetFloat("_ForestPropReadability", .55f);
            void Configure(SpriteRenderer renderer, bool actor)
            {
                renderer.sharedMaterial = material;
                var uv = renderer.sprite.uv; var block = new MaterialPropertyBlock();
                block.SetFloat("_IsActor", actor ? 1 : 0); block.SetFloat("_ForestReadabilityProfile", actor ? 1 : 2);
                block.SetVector("_ForestPropUV", new Vector4(uv.Min(p => p.x), uv.Min(p => p.y), uv.Max(p => p.x), uv.Max(p => p.y)));
                block.SetVector("_ForestPropTexel", new Vector4(1f / renderer.sprite.texture.width, 1f / renderer.sprite.texture.height, 0, 0));
                renderer.SetPropertyBlock(block);
            }
            var names = new[] { "Jerzy", "Wladimir_Test", "Paladin", "Mage" };
            var props = new[] { "forest-stump-v1", "forest-boulder-v1", "forest-deadwood-v1", "forest-small-stone" };
            for (int i = 0; i < names.Length; i++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"Assets/_Project/ScriptableObjects/Characters/CD_{names[i]}.asset");
                Assert.That(CharacterAnimationResolver.TryResolveCharacter(definition.Id, Vector2.down, CharacterAnimationState.Idle, out var idle), Is.True);
                var actor = Rendered(names[i], idle.Frames[0], new Vector2(i * 1.6f + .1f, .7f)); actor.transform.localScale = Vector3.one * .7f; Configure(actor, true);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/ForestPrototype/{props[i]}-32ppu.png"); Assert.That(sprite, Is.Not.Null);
                var prop = Rendered(props[i], sprite, new Vector2(i * 1.6f + .1f, 3.3f)); Configure(prop, false);
            }
            var image = Capture(new Vector2(2.5f, 2.5f), 3.6f, "forest-contour-closeup", resolution: 768);
            var destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/forest-contour-correction-20261018"));
            Directory.CreateDirectory(destination); File.WriteAllBytes(Path.Combine(destination, "closeup-current.png"), image.EncodeToPNG());
            Assert.That(image.GetPixels32().Select(p => (p.r, p.g, p.b)).Distinct().Count(), Is.GreaterThan(100));
        }

        [Test]
        public void ReadabilityPreviewUsesActualPartyArtWithoutEmittingLight()
        {
            var names = new[] { "Jerzy", "Wladimir_Test", "Paladin", "Mage" };
            var material = Keep(new Material(Shader.Find("Godmist/Dungeon/Torch Body")));
            material.SetFloat("_Ambient", .7f); material.SetInt("_TorchCount", 0); material.SetFloat("_IsActor", 1);
            var actors = new List<SpriteRenderer>();
            for (int i = 0; i < names.Length; i++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"Assets/_Project/ScriptableObjects/Characters/CD_{names[i]}.asset");
                Assert.That(definition, Is.Not.Null);
                Sprite sprite = definition.VisualSprite;
                if (CharacterAnimationResolver.TryResolveCharacter(definition.Id, Vector2.down, CharacterAnimationState.Idle, out var idle)) sprite = idle.Frames[0];
                Assert.That(sprite, Is.Not.Null);
                var actor = Rendered(names[i], sprite, new Vector2(i * 1.6f - 2.4f, -1)); actor.transform.localScale = Vector3.one * .7f;
                actor.sharedMaterial = material; actors.Add(actor);
                var block = new MaterialPropertyBlock(); var uv = sprite.uv;
                block.SetVector("_ForestPropUV", new Vector4(uv.Min(p => p.x), uv.Min(p => p.y), uv.Max(p => p.x), uv.Max(p => p.y)));
                block.SetVector("_ForestPropTexel", new Vector4(1f / sprite.texture.width, 1f / sprite.texture.height, 0, 0));
                actor.SetPropertyBlock(block);
            }
            material.SetFloat("_ForestPropReadability", 0); Capture(Vector2.zero, 3.6f, "actual-party-before-readability");
            material.SetFloat("_ForestPropReadability", .55f); material.SetFloat("_ForestReadabilityProfile", 1);
            Capture(Vector2.zero, 3.6f, "actual-party-after-readability");
            material.SetFloat("_Ambient", 0);
            var unlit = Capture(Vector2.zero, 3.6f, "actual-party-unlit", Color.black);
            Assert.That(unlit.GetPixels().All(p => Mathf.Max(p.r, Mathf.Max(p.g, p.b)) < .001f), Is.True, "The outline is lit and must not emit in zero light.");
            Assert.That(actors.All(a => a.color.a == 1), Is.True);
        }

        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        public void ProductionWorldPreservesDepthRangeAndRendersRememberedGeometry(string id, DungeonLength length)
        {
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "audit" };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var floor = session.ActiveDungeon.Floors[0];
                if(map.UsesVegetationPresentation)AssertMatureCrownsLeaveTrailCentresClear(map,floor);
                if(map.UsesVegetationPresentation)
                {
                    var reserved=new HashSet<Vector2Int>();
                    var trees=floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.Definition.Id=="object.forest.tree.prototype").ToArray();
                    foreach(var tree in trees)
                    {
                        var definition=(ObstacleDungeonObjectDefinition)tree.Definition;
                        var cells=definition.EnumeratePlacementReservationCells(tree.Position).ToArray();
                        Assert.That(cells.Length,Is.EqualTo(4));
                        foreach(var cell in cells)Assert.That(reserved.Add(cell),Is.True,"Interior tree reservations overlap.");
                        foreach(var other in floor.Objects.Where(o=>o!=tree))Assert.That(cells.Contains(other.Position),Is.False,"Another object stands in a mature tree reservation.");
                    }
                }
                var actors = party.ActiveMemberViews.ToArray();
                var center = (Vector2)actors[0].transform.position;
                var camera = Read<Camera>(rig, "worldCamera");
                if (map.UsesVegetationPresentation)
                    foreach (var tree in Read<DungeonObjectSpawner>(bootstrap, "objectSpawner").SpawnedObjects.Where(v => floor.Objects.Any(o => o.InstanceId == v.ObjectInstanceId && o.Definition.Id == "object.forest.tree.prototype")))
                        foreach (var sprite in tree.GetComponentsInChildren<SpriteRenderer>()) map.RegisterForestCanopy(sprite);
                camera.transform.position = new Vector3(center.x, center.y + 1.5f, -10);
                camera.orthographicSize = 6; camera.aspect = 16f / 9;
                new DungeonFogSystem().Update(floor, actors.Select(a => new DungeonTraversalPosition(new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                map.ApplyFog(floor);
                foreach (var actor in actors) Call(actor, "LateUpdate");
                Call(rig, "RefreshForestPartyReveal");
                typeof(DungeonSilhouetteLighting).GetMethod("AdvanceForestCanopyFade",Flags).Invoke(rig,new object[]{.5f});
                rig.GetType().GetMethod("RefreshLighting", Flags).Invoke(rig, new object[] { 1f });
                foreach (var actor in actors) Call(actor, "LateUpdate");
                Call(rig, "RefreshForestPartyReveal");
                typeof(DungeonSilhouetteLighting).GetMethod("AdvanceForestCanopyFade",Flags).Invoke(rig,new object[]{.5f});
                Assert.That(actors.All(a => !a.BodyRenderer.forceRenderingOff && a.BodyRenderer.color.a == 1), Is.True);
                if(map.UsesVegetationPresentation)
                {
                    var reveal=Read<Dictionary<SpriteRenderer,float>>(rig,"canopyFadeTargets").Values.ToArray();
                    Assert.That(reveal.All(v=>v>0 && v<=90f/255+.0001f),Is.True);
                    Directory.CreateDirectory(Output);
                    File.WriteAllLines(Path.Combine(Output,"production-party-canopy-opacity.txt"),reveal.Select((v,index)=>index+"\t"+v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
                }
                var roots = bootstrap.gameObject.scene.GetRootGameObjects();
                var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
                var groups = roots.SelectMany(r => r.GetComponentsInChildren<SortingGroup>(true)).ToArray();
                Assert.That(map.GetFloorFeedbackDepthSortingOrder(), Is.GreaterThan(short.MinValue + 8), "Check the computed band before Unity can clamp a renderer's native sortingOrder.");
                Assert.That(renderers.All(r => r.sortingOrder >= short.MinValue && r.sortingOrder <= short.MaxValue), Is.True);
                Assert.That(groups.All(g => g.sortingOrder >= short.MinValue && g.sortingOrder <= short.MaxValue), Is.True);
                foreach (var renderer in renderers) renderer.gameObject.layer = 30;
                var target = Keep(new RenderTexture(960, 540, 24)); var image = Keep(new Texture2D(960, 540, TextureFormat.RGBA32, false));
                var oldTarget = camera.targetTexture; var oldMask = camera.cullingMask; var active = RenderTexture.active;
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30;
                    void Save(string prefix)
                    {
                        SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(Output, prefix + id + ".png"), image.EncodeToPNG());
                    }
                    var captureTime = 1f;
                    void Refresh()
                    {
                        map.ApplyFog(floor);
                        // Advance the normal receiver cadence between fog states,
                        // as the game does; don't capture newly revealed art with
                        // the previous state's preparation time frozen at 1s.
                        rig.GetType().GetMethod("RefreshLighting", Flags).Invoke(rig, new object[] { ++captureTime });
                        Call(rig, "RefreshForestPartyReveal");
                typeof(DungeonSilhouetteLighting).GetMethod("AdvanceForestCanopyFade",Flags).Invoke(rig,new object[]{.5f});
                    }
                    Save("production-fog-");
                    if(map.UsesVegetationPresentation)
                    {
                        // Isolate the party's original lit image without
                        // changing its opacity, light inputs or shadow masks.
                        var crowns=renderers.OfType<SpriteRenderer>().Where(map.IsForestCanopyRenderer).ToArray();
                        var original=crowns.Select(r=>r.forceRenderingOff).ToArray();
                        try
                        {
                            foreach(var crown in crowns)crown.forceRenderingOff=true;
                            Save("production-without-crowns-");
                        }
                        finally
                        {
                            for(var index=0;index<crowns.Length;index++)crowns[index].forceRenderingOff=original[index];
                        }
                    }
                    if (map.UsesVegetationPresentation)
                    {
                        floor.Fog.SetVisible(floor.Map.FloorCells.Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
                        Refresh(); Save("production-known-");
                        floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                        Refresh(); Save("production-remembered-");
                    }
                    floor.Fog.ToggleDebugFog(); map.ApplyFog(floor);
                    foreach (var actor in actors) Call(actor, "LateUpdate");
                    rig.GetType().GetMethod("RefreshLighting", Flags).Invoke(rig, new object[] { ++captureTime });
                    Call(rig, "RefreshForestPartyReveal");
                typeof(DungeonSilhouetteLighting).GetMethod("AdvanceForestCanopyFade",Flags).Invoke(rig,new object[]{.5f});
                    Save("production-");
                    Assert.That(image.GetPixels32().Select(p => (p.r, p.g, p.b)).Distinct().Count(), Is.GreaterThan(100));
                }
                finally { RenderTexture.active = active; camera.targetTexture = oldTarget; camera.cullingMask = oldMask; }
            };
            fixture.MeasureProductionFloorEntry(id, length);
        }
    }
}
