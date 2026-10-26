using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Combat;
using Godmist.Data.Effects;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestWolfTests
    {
        private CharacterDefinition Wolf => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.WolfPath);
        private BattleUnit Unit(int level = 1, bool player = false)
        {
            var unit = new BattleUnit
            {
            Character = new CharacterInstanceFactory().Create(Wolf, Wolf.ClassData, level),
            Position = new GridPosition(1, 1), IsPlayerSide = player,
            CurrentActionPoints = 10000, MaxActionPoints = 10000
            };
            unit.Character.ResourcePool.CurrentValue = 100; // Isolate skill behavior from spawn balance.
            return unit;
        }
        private CombatSession Session(BattleUnit wolf, BattleUnit target) => new()
        {
            CombatId = "test.forest.wolf", Grid = new BattleGrid(12, 12),
            ActiveUnit = wolf, Units = new List<BattleUnit> { wolf, target }
        };

        [Test]
        public void ImportedWolfHasWorkbookStatsAndCosts()
        {
            Assert.That(Wolf, Is.Not.Null);
            Assert.That(Wolf.Id, Is.EqualTo(ForestEnemiesSetup.WolfId));
            Assert.That(Wolf.Tags, Does.Contain(CharacterTag.Beast));
            Assert.That(Wolf.BaseStats.MaxHealth, Is.EqualTo(118));
            Assert.That(Wolf.BaseStats.MinAttack, Is.EqualTo(32));
            Assert.That(Wolf.BaseStats.MaxAttack, Is.EqualTo(63));
            Assert.That(Wolf.BaseStats.Speed, Is.EqualTo(64));
            Assert.That(Wolf.BaseStats.CritChance, Is.EqualTo(.14f));
            Assert.That(Wolf.BaseStats.CritDamage, Is.EqualTo(2.1f));
            Assert.That(Wolf.GrowthData.MaxHealth, Is.EqualTo(13f)); // PT-06 level48 calibration.
            Assert.That(Wolf.GrowthData.MaxAttack, Is.EqualTo(4.68f));
            Assert.That(Wolf.BaseResistances.SuppressionResistance, Is.EqualTo(.15f));
            Assert.That(Wolf.StartingSkills.Select(s => s.ResourceCost), Is.EqualTo(new[] { 0, 15, 30 }));
            Assert.That(Wolf.StartingSkills.Select(s => s.ActionCost), Is.EqualTo(new[] { 55, 20, 60 }));
            Assert.That(Wolf.BasicAttackSkill, Is.SameAs(Wolf.StartingSkills[0]));
            var bleed = Wolf.BasicAttackSkill.Effects[1];
            Assert.That(bleed.BaseChance, Is.EqualTo(.25f));
            Assert.That(bleed.StatusDamageMultiplier, Is.EqualTo(.95f));
            Assert.That(bleed.DurationTurns, Is.EqualTo(2));
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(20)]
        public void LickWoundsScalesWithAttackAndPaysMana(int level)
        {
            var wolf = Unit(level);
            var target = Unit(player: true); target.Position = new GridPosition(10, 10);
            var session = Session(wolf, target);
            wolf.Character.CurrentHealth = 1;
            var expected = Mathf.RoundToInt((wolf.Character.Stats.MinAttack.GetValue()
                + wolf.Character.Stats.MaxAttack.GetValue()) * .5f * .8f);
            var ap = wolf.CurrentActionPoints;
            Assert.That(new SkillExecutionSystem().TryExecute(session, wolf, Wolf.StartingSkills[2], wolf), Is.True);
            Assert.That(wolf.Character.CurrentHealth, Is.EqualTo(1 + expected));
            Assert.That(wolf.Character.ResourcePool.CurrentValue, Is.EqualTo(70));
            Assert.That(wolf.CurrentActionPoints, Is.LessThan(ap));
        }

        [Test]
        public void MissingAttackCoefficientPreservesFlatHealing()
        {
            var effect = new SkillEffectData();
            typeof(SkillEffectData).GetField("baseValue", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(effect, 25);
            Assert.That(effect.HealAverageAttackMultiplier, Is.Zero);
            Assert.That(CombatRecoverySystem.CalculateSkillHealing(Unit(), effect), Is.EqualTo(25));
            Assert.That(CombatRecoverySystem.CalculateSkillHealing(null, effect), Is.EqualTo(25));
        }

        [Test]
        public void HealingClampsAndCannotBeUsedWithoutMana()
        {
            var wolf = Unit(); var target = Unit(player: true);
            target.Position = new GridPosition(10, 10);
            var session = Session(wolf, target);
            var maximum = Mathf.RoundToInt(wolf.Character.Stats.MaxHealth.GetValue());
            wolf.Character.CurrentHealth = maximum - 1;
            Assert.That(new SkillExecutionSystem().TryExecute(session, wolf, Wolf.StartingSkills[2], wolf), Is.True);
            Assert.That(wolf.Character.CurrentHealth, Is.EqualTo(maximum));
            wolf.Character.ResourcePool.CurrentValue = 29;
            wolf.Character.CurrentHealth = 1;
            var ap = wolf.CurrentActionPoints;
            Assert.That(new SkillExecutionSystem().TryExecute(session, wolf, Wolf.StartingSkills[2], wolf), Is.False);
            Assert.That(wolf.Character.CurrentHealth, Is.EqualTo(1));
            Assert.That(wolf.CurrentActionPoints, Is.EqualTo(ap));
        }

        [Test]
        public void LeapMovesTwoCellsAndBuffsOnlyCaster()
        {
            var wolf = Unit(); var target = Unit(player: true);
            target.Position = new GridPosition(4, 1);
            var speed = wolf.Character.Stats.Speed.GetValue();
            var session = Session(wolf, target);
            Assert.That(new SkillExecutionSystem().TryExecute(session, wolf, Wolf.StartingSkills[1], new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { target }, [1] = new[] { wolf } }), Is.True);
            Assert.That(wolf.Position, Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(target.Position, Is.EqualTo(new GridPosition(4, 1)));
            Assert.That(wolf.Character.Stats.Speed.GetValue(), Is.EqualTo(speed + 2));
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(speed));
            Assert.That(wolf.Character.ResourcePool.CurrentValue, Is.EqualTo(85));
        }

        [Test]
        public void BiteSupportsDiagonalsAndMissDoesNotBleed()
        {
            var wolf = Unit(); var target = Unit(player: true);
            target.Position = new GridPosition(2, 2);
            target.Character.Stats.Dodge = new Stat(1000);
            // Dodge alone retains a minimum hit chance; force hit failure so
            // this follow-up-status regression does not depend on global RNG.
            target.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { UniqueBuffType = UniqueBuffType.GuaranteedDodge });
            var health = target.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem().TryExecute(Session(wolf, target), wolf, Wolf.BasicAttackSkill, target), Is.True);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(health));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Bleed), Is.False);
        }

        [Test]
        public void WoundedAiHealsInsteadOfWalkingAndFullHealthAiDoesNotHeal()
        {
            foreach (var wounded in new[] { true, false })
            {
                var wolf = Unit(); var target = Unit(player: true);
                target.Position = new GridPosition(10, 10);
                if (wounded) wolf.Character.CurrentHealth = 1;
                var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem());
                Assert.That(ai.TryExecuteNextAction(Session(wolf, target), wolf, ai.CreateTurnState(), out var action), Is.True);
                if (wounded) Assert.That(action.Skill, Is.SameAs(Wolf.StartingSkills[2]));
                else Assert.That(action.Skill, Is.Not.SameAs(Wolf.StartingSkills[2]));
            }
        }

        [Test]
        public void FangUsesTenPercentRollAndOneItem()
        {
            var entry = Wolf.LootTable.Entries.Single();
            Assert.That(entry.DropChance, Is.EqualTo(.1f));
            Assert.That(entry.ItemDefinition.Id, Is.EqualTo("item.material.wolf_fang"));
            Assert.That(entry.ItemDefinition.MaterialDiscipline, Is.EqualTo(MaterialDiscipline.Alchemy));
            Assert.That(entry.ItemDefinition.BaseValue, Is.EqualTo(273));
            Assert.That(entry.Amount.Min, Is.EqualTo(1)); Assert.That(entry.Amount.Max, Is.EqualTo(1));
            Assert.That(new LootTableSystem(randomValue: () => .05f).RollLoot(Wolf.LootTable).Count, Is.EqualTo(1));
            Assert.That(new LootTableSystem(randomValue: () => .5f).RollLoot(Wolf.LootTable), Is.Empty);
        }

        [Test]
        public void TenSeedsPlacePairsOnFreePatrollingAnchors()
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            Assert.That(definition.IsAvailableAsOffer, Is.False);
            // This fixture tests wolf pairs, not the prototype's expanded mixed roster.
            var source = definition;
            definition = Object.Instantiate(source);
            var data = new SerializedObject(definition);
            var encounters = data.FindProperty("<EncounterDefinitions>k__BackingField");
            encounters.arraySize = 1;
            encounters.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(ForestEnemiesSetup.EncounterPath);
            data.FindProperty("<BossEncounterDefinition>k__BackingField").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                foreach (var seed in new[] { 1, 7, 19, 42, 117, 4205, 9891, 12345, 65535, 20261011 })
                {
                    var dungeon = new DungeonGeneratorService().Generate(definition, seed);
                    new DungeonObjectPlacementSystem().Place(dungeon);
                    new DungeonEncounterPlacementSystem().Place(dungeon);
                    var floor = dungeon.Floors.Single();
                    Assert.That(floor.Encounters.Count, Is.InRange(1, 2), "seed " + seed);
                    foreach (var encounter in floor.Encounters)
                    {
                        Assert.That(encounter.Enemies.Count, Is.EqualTo(2));
                        Assert.That(encounter.Enemies.All(e => e.CharacterDefinition.Id == ForestEnemiesSetup.WolfId), Is.True);
                        Assert.That(encounter.EnemyExplorationPositions.Count, Is.EqualTo(2));
                        Assert.That(encounter.EnemyExplorationPositions.Values.Distinct().Count(), Is.EqualTo(2));
                        Assert.That(encounter.StationaryEnemyHomes, Is.Empty);
                        foreach (var cell in encounter.EnemyExplorationPositions.Values)
                        {
                            Assert.That(floor.Map.IsWalkable(cell), Is.True);
                            Assert.That(floor.Objects.Any(o => o.Position == cell), Is.False, "Occupied wolf spawn, seed " + seed);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(definition); }
        }
    }
}
