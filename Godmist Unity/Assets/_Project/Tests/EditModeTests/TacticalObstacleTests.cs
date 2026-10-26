using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class TacticalObstacleTests
    {
        private const string Root = "Assets/_Project/";
        [Test]
        public void SarcophagusCombatSessionBlocksBothCellsAndReleasesBothOnDeactivation()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>(
                Root + "ScriptableObjects/Dungeons/Objects/DO_Obstacle_Sarcophagus.asset");
            var source = new Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance("sarcophagus", definition, Vector2Int.right);
            var cells = new List<Vector2Int>();
            for (var x = 0; x < 4; x++) for (var y = 0; y < 3; y++) cells.Add(new Vector2Int(x, y));
            var context = new DungeonCombatContext("dungeon", 0, "encounter", "room", Vector2Int.zero,
                new Vector2Int(3, 0), cells, null, new[] {
                    new DungeonCombatMapObjectSnapshot("sarcophagus", DungeonObjectType.Obstacle,
                        source.Position, true, false, source) });
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            var session = new CombatSession { Grid = grid, DungeonContext = context };
            typeof(CombatSessionFactory).GetMethod("AddDungeonMapObjects", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { session, context });
            var obstacle = session.MapObjects.OfType<ObstacleCombatMapObject>().Single();
            var south = DungeonBattleGridFactory.ToBattlePosition(context, Vector2Int.right);
            var north = DungeonBattleGridFactory.ToBattlePosition(context, Vector2Int.one);
            Assert.That(obstacle.EnumerateOccupiedPositions(), Is.EquivalentTo(new[] { south, north }));
            Assert.That(obstacle.Occupies(south), Is.True);
            Assert.That(obstacle.Occupies(north), Is.True);
            Assert.That(obstacle.BlocksLineOfSight, Is.False);
            source.TryDeactivate();
            Assert.That(obstacle.Occupies(south), Is.False);
            Assert.That(obstacle.Occupies(north), Is.False);
            Assert.That(obstacle.BlocksMovement, Is.False);
        }

        [TestCase("Sarcophagus", false)]
        [TestCase("Rubble", false)]
        [TestCase("Pillar", true)]
        public void ContentVariants_RespectMovementSightAndTeleport(string name, bool blocksSight)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>(
                Root + "ScriptableObjects/Dungeons/Objects/DO_Obstacle_" + name + ".asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.BlocksMovement, Is.True);
            Assert.That(definition.BlocksLineOfSight, Is.EqualTo(blocksSight));
            var cells = new List<Vector2Int>();
            for (int x = 0; x < 3; x++) for (int y = 0; y < 2; y++) cells.Add(new Vector2Int(x,y));
            var context = new DungeonCombatContext("dungeon",0,"encounter","room",Vector2Int.zero,
                new Vector2Int(2,0),cells,null,new[] {
                    new DungeonCombatMapObjectSnapshot("obstacle",DungeonObjectType.Obstacle,
                        Vector2Int.right,definition.BlocksMovement,definition.BlocksLineOfSight) });
            Assert.That(new DungeonBattleGridFactory().TryCreate(context,out var grid),Is.True);
            var session = new CombatSession { Grid = grid, DungeonContext = context };
            session.MapObjects.Add(new ObstacleCombatMapObject("obstacle",new GridPosition(1,0),true,blocksSight));
            var unit = new BattleUnit { Position = new GridPosition(0,0), IsPlayerSide = true,
                CurrentActionPoints = 2000, Character = new CharacterInstance { CharacterId = "unit",
                    LifeState = CharacterLifeState.Alive, CurrentHealth = 10,
                    Stats = new CharacterStats { Speed = new Stat(20) } } };
            session.Units.Add(unit); session.ActiveUnit = unit;
            var sight = new CombatLineOfSightService();
            Assert.That(sight.HasLineOfSight(session,new GridPosition(0,0),new GridPosition(2,0)),Is.EqualTo(!blocksSight));
            Assert.That(sight.HasLineOfSight(session,new GridPosition(2,0),new GridPosition(0,0)),Is.EqualTo(!blocksSight));
            var paths = new CombatMovementSystem().CalculateReachablePaths(session,unit);
            Assert.That(paths.ContainsKey(new GridPosition(1,0)),Is.False);
            Assert.That(paths[new GridPosition(2,0)].Count,Is.EqualTo(4));
            var teleport = new CombatTeleportSystem();
            Assert.That(teleport.CanTeleport(session,unit,unit,new GridPosition(1,0),false),Is.False);
            Assert.That(teleport.CanTeleport(session,unit,unit,new GridPosition(2,0),false),Is.True);
            Assert.That(teleport.CanTeleport(session,unit,unit,new GridPosition(2,0),true),Is.EqualTo(!blocksSight));
            var charge = typeof(CombatChargeSystem).GetMethod("CanTraverse",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That((bool)charge.Invoke(null,new object[] { session,new GridPosition(0,0),new GridPosition(1,0) }),Is.False);
            var forced = typeof(SkillExecutionSystem).GetMethod("CanForcedDisplacementMove",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That((bool)forced.Invoke(null,new object[] { session,unit,new GridPosition(1,0),1,0 }),Is.False);
            Assert.That(unit.Position,Is.EqualTo(new GridPosition(0,0)),"Preview must not mutate the unit.");
            Assert.That(unit.CurrentActionPoints,Is.EqualTo(2000));
        }

        [Test]
        public void PrefabBindings_RejectMissingDuplicateAndColliderlessEntries()
        {
            var go = new GameObject("spawner"); var prefabGo = new GameObject("prefab");
            var def = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                var spawner = go.AddComponent<DungeonObjectSpawner>();
                var bindings = new List<DungeonObjectSpawner.ObstaclePrefabBinding>();
                typeof(DungeonObjectSpawner).GetField("obstaclePrefabs",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(spawner,bindings);
                Assert.That(spawner.ValidateObstaclePrefabs(out _),Is.True,"Legacy empty mapping stays valid.");
                bindings.Add(new DungeonObjectSpawner.ObstaclePrefabBinding { definition = def });
                Assert.That(spawner.ValidateObstaclePrefabs(out _),Is.False);
                bindings[0].prefab = prefabGo.AddComponent<DungeonObstacleView>();
                Assert.That(spawner.ValidateObstaclePrefabs(out _),Is.False);
                typeof(DungeonObstacleView).GetField("blockingCollider",BindingFlags.NonPublic|BindingFlags.Instance)
                    .SetValue(bindings[0].prefab,prefabGo.AddComponent<BoxCollider2D>());
                Assert.That(spawner.ValidateObstaclePrefabs(out _),Is.True);
                Assert.That(spawner.ResolveObstaclePrefab(def),Is.SameAs(bindings[0].prefab));
                bindings.Add(bindings[0]);
                Assert.That(spawner.ValidateObstaclePrefabs(out _),Is.False);
            }
            finally { Object.DestroyImmediate(go);Object.DestroyImmediate(prefabGo);Object.DestroyImmediate(def); }
        }

        [Test]
        public void RuntimeColliderGeometry_UsesColliderTransformAndPreservesWorldBottom()
        {
            var root = new GameObject("obstacle-root");
            var colliderObject = new GameObject("child-collider");
            colliderObject.transform.SetParent(root.transform, false);
            var view = root.AddComponent<DungeonObstacleView>();
            var collider = colliderObject.AddComponent<BoxCollider2D>();
            colliderObject.transform.localPosition = new Vector3(.13f, .17f, 0f);
            colliderObject.transform.localScale = new Vector3(.5f, .75f, 1f);
            collider.size = new Vector2(1.4f, 1.2f);
            typeof(DungeonObstacleView).GetField("blockingCollider", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, collider);
            root.transform.localScale = new Vector3(1.8f, .7f, 1f);
            var bottom = collider.bounds.min.y;

            try
            {
                Assert.That(view.ConfigureRuntimeColliderGeometry(new Vector2(1f, .5f)), Is.True);
                Physics2D.SyncTransforms();
                Assert.That(collider.bounds.size.x, Is.EqualTo(1f).Within(.0001f));
                Assert.That(collider.bounds.size.y, Is.EqualTo(.5f).Within(.0001f));
                Assert.That(collider.bounds.min.y, Is.EqualTo(bottom).Within(.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NonBlockingObstacle_ReportsAuthoringWarning()
        {
            var def = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                typeof(ObstacleDungeonObjectDefinition).GetField("<BlocksMovement>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(def,false);
                var report = ContentAuthoringValidator.ValidateProject(null,new Object[] { def });
                Assert.That(report.Warnings.Any(issue => issue.Asset == def && issue.FieldPath == "BlocksMovement"),Is.True);
            }
            finally { Object.DestroyImmediate(def); }
        }
    }
}
