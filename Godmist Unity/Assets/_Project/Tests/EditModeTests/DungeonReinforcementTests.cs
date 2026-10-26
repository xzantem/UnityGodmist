using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonReinforcementTests
    {
        private readonly List<Object> assets = new();
        private DungeonInstance dungeon;
        private DungeonFloorInstance floor;
        private CombatSession session;
        private EncounterDefinition encounter, boss;
        private GameContentCatalog catalog;
        private CharacterDefinition enemyDefinition;
        private CharacterClassData enemyClass;

        private T Asset<T>(string id) where T : ScriptableObject
        { var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); Set(asset, "Id", id); return asset; }
        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value); return;
            }
        }
        [SetUp] public void Setup()
        {
            var definition = Asset<DungeonDefinition>("reinforcement.dungeon");
            Set(definition, "IsProcedural", true);
            catalog = ScriptableObject.CreateInstance<GameContentCatalog>(); assets.Add(catalog);
            catalog.DungeonDefinitions.Add(definition);
            enemyDefinition = Asset<CharacterDefinition>("enemy"); enemyClass = Asset<CharacterClassData>("enemy.class");
            encounter = Asset<EncounterDefinition>("regular"); boss = Asset<EncounterDefinition>("boss");
            Set(definition, "BossEncounterDefinition", boss);
            definition.EncounterDefinitions.AddRange(new[] { boss, encounter });
            catalog.EncounterDefinitions.AddRange(new[] { boss, encounter });
            for (var i = 0; i < 5; i++)
            {
                var enemy = new EncounterEnemyData(); Set(enemy, "InstanceId", "enemy." + i);
                Set(enemy, "CharacterDefinition", enemyDefinition); Set(enemy, "ClassData", enemyClass);
                encounter.Enemies.Add(enemy);
            }
            boss.Enemies.Add(encounter.Enemies[0]);
            dungeon = new DungeonInstance(definition, 42, 20);
            floor = new DungeonFloorInstance(0); dungeon.TryAddFloor(floor);
            floor.TryAddRoom(new DungeonRoomInstance("room", DungeonRoomType.Normal));
            floor.TryAddRoom(new DungeonRoomInstance("hall", DungeonRoomType.Start));
            var map = new DungeonFloorMap();
            for (var x = 0; x < 6; x++) for (var y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(x,y), "room");
            map.TryAddFloorCell(new Vector2Int(-1, 1), "hall"); floor.TrySetMap(map);
            floor.TryAddConnection("hall", "room");
            floor.Connections[0].TrySetDirectPassage(new Vector2Int(-1,1), new Vector2Int(0,1), 1);
            var context = new DungeonCombatContext(dungeon.DungeonId, 0, "original", "room", Vector2Int.one,
                new Vector2Int(4,2), map.FloorCells, null, null, 20, surfaceMap: map, isFloorWide: true);
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            session = new CombatSession { IsSeamless = true, IsDungeonWide = true, DungeonContext = context, Grid = grid };
            session.Units.Add(new BattleUnit { IsPlayerSide = false, Position = DungeonBattleGridFactory.ToBattlePosition(context, new Vector2Int(4,2)),
                Character = new CharacterInstance { LifeState = CharacterLifeState.Alive, CurrentHealth = 100 } });
        }
        [TearDown] public void Cleanup() { foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }
        private DungeonEncounterInstance Plan(int seed = 1)
        {
            Assert.That(DungeonReinforcementSystem.TryPlan(dungeon, floor, session, new HashSet<Vector2Int>(), new System.Random(seed), out var plan), Is.True);
            return plan;
        }
        [TestCase(1,1)] [TestCase(4,2)] [TestCase(5,3)]
        public void SamplesHalfRoundedUpWithoutBossOrDuplicateEnemies(int total, int expected)
        {
            encounter.Enemies.RemoveRange(total, encounter.Enemies.Count-total);
            var plan = Plan(); Assert.That(plan.Definition, Is.SameAs(encounter));
            Assert.That(plan.Enemies.Count, Is.EqualTo(expected));
            Assert.That(plan.Enemies.Select(e => e.InstanceId).Distinct().Count(), Is.EqualTo(expected));
            Assert.That(plan.EnemyExplorationPositions.Values.Distinct().Count(), Is.EqualTo(expected));
            Assert.That(plan.EnemyExplorationPositions.Values.All(cell => Mathf.Abs(cell.x) + Mathf.Abs(cell.y-1) <= 2), Is.True);
        }
        [Test] public void FullyOccupiedEntranceDefersWithoutChangingTheDungeon()
        {
            var occupied = new HashSet<Vector2Int>(floor.Map.FloorCells);
            Assert.That(DungeonReinforcementSystem.TryPlan(dungeon, floor, session, occupied, new System.Random(1), out _), Is.False);
            Assert.That(floor.Encounters, Is.Empty);
        }
        [Test] public void RespectsOccupantsFromOtherCombats()
        {
            session.ExternalPositionOccupied = position => true;
            Assert.That(DungeonReinforcementSystem.TryPlan(dungeon, floor, session, new HashSet<Vector2Int>(), new System.Random(1), out _), Is.False);
        }
        [Test] public void SelectionAndZeroRewardsSurviveSaveLoadAndPreparation()
        {
            var plan = Plan(); Assert.That(floor.TryAddEncounter(plan), Is.True);
            dungeon.MarkContentPlaced(); new DungeonEncounterRewardPreparationSystem().Prepare(dungeon);
            Assert.That(dungeon.TryGetPreRolledRewards(plan.InstanceId, out var reward), Is.True);
            Assert.That(reward.EnemyRewards, Is.Empty); Assert.That(reward.Gold, Is.Zero);
            var game = new GameSession(GameModeState.Exploration); game.TrySetActiveDungeon(dungeon);
            var saved = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "reinforcements");
            var json = new GameSaveJsonSerializer();
            Assert.That(json.TryDeserialize(json.Serialize(saved), out var loaded), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(loaded, catalog, out var restored), Is.True);
            var copy = restored.ActiveDungeon.Floors[0].Encounters.Single();
            Assert.That(copy.IsReinforcement, Is.True);
            Assert.That(copy.Enemies.Select(e => e.InstanceId), Is.EqualTo(plan.Enemies.Select(e => e.InstanceId)));
            Assert.That(copy.EnemyExplorationPositions, Is.EquivalentTo(plan.EnemyExplorationPositions));
            new DungeonEncounterRewardPreparationSystem().Prepare(restored.ActiveDungeon);
            Assert.That(restored.ActiveDungeon.TryGetPreRolledRewards(copy.InstanceId, out reward), Is.True);
            Assert.That(reward.EnemyRewards, Is.Empty); Assert.That(reward.Items, Is.Empty);
            Assert.That(reward.Gold, Is.Zero); Assert.That(reward.ExperiencePerActiveCharacter, Is.Zero);
        }
        [Test] public void ReinforcementsJoinWithNormalAVAndUniqueRewardIdentity()
        {
            var plan = Plan();
            foreach (var enemy in plan.Enemies)
            {
                var character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(enemy.CharacterDefinition, enemy.ClassData, dungeon.DungeonLevel);
                var position = DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, plan.EnemyExplorationPositions[enemy.InstanceId]);
                var unit = new CombatSessionFactory().CreateJoiningUnit(character, position, false);
                Assert.That(character.Level, Is.EqualTo(20));
                Assert.That(DungeonCombatParticipationSystem.TryJoin(session, unit, position), Is.True);
                Assert.That(unit.CurrentActionValue, Is.EqualTo(CombatTurnSystem.CalculateBaseActionValue(unit)));
                DungeonCombatMembershipSystem.RegisterEnemy(session, plan.InstanceId, enemy.InstanceId, new CombatRewardResult(), unit);
            }
            Assert.That(session.EnemySources.Count, Is.EqualTo(3));
            Assert.That(session.Rewards.EnemyRewards, Is.Empty);
        }
    }
}
