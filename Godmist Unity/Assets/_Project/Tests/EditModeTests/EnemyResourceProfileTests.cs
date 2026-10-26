using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EnemyResourceProfileTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
        static CharacterDefinition Definition(string id) => Catalog.CharacterDefinitions.Single(d => d.Id == id);
        static BattleUnit Unit(string id, bool player = false, int x = 1, int y = 3)
        {
            var character = new CharacterInstanceFactory().Create(Definition(id));
            character.CharacterId += Guid.NewGuid(); character.WeaponAccuracy = 10000;
            character.Stats.MaxHealth = new Stat(100000); character.CurrentHealth = 100000;
            character.Stats.Dodge = new Stat(0); character.Stats.CritChance = new Stat(0);
            return new BattleUnit { Character = character, IsPlayerSide = player, Position = new GridPosition(x, y),
                CurrentActionPoints = 1000, MaxActionPoints = 1000 };
        }
        static IEnumerable<TestCaseData> Profiles()
        {
            foreach (var d in Catalog.CharacterDefinitions)
                if (d != null && EnemyResourceProfilesSetup.TryGetType(d.Id, out var type))
                    yield return new TestCaseData(d.Id, type).SetName("Profile_" + d.Id);
        }
        [TestCaseSource(nameof(Profiles))]
        public void FreshEnemyHasApprovedProfileAndEverySkillMatches(string id, ResourceType type)
        {
            var definition = Definition(id); var character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 10);
            var pool = character.ResourcePool;
            Assert.That(pool.ResourceType, Is.EqualTo(type));
            Assert.That(definition.ClassData.StartingResourcePercent, Is.EqualTo(type == ResourceType.Mana ? 100 : 0));
            Assert.That(pool.MaxValue, Is.EqualTo(type == ResourceType.Rage ? 50 : type == ResourceType.Momentum ? 200 : 100));
            Assert.That(pool.CurrentValue, Is.EqualTo(type == ResourceType.Mana ? pool.MaxValue : 0));
            Assert.That(pool.Regeneration, Is.EqualTo(type == ResourceType.Mana ? 15 : 0));
            foreach (var skill in character.GetActiveSkills())
            {
                Assert.That(skill.ResourceType, Is.EqualTo(type), skill.SkillId);
                Assert.That(skill.ResourceCost, Is.LessThanOrEqualTo(pool.MaxValue), skill.SkillId);
                if (type == ResourceType.Rage) Assert.That(skill.ResourceCost % 5, Is.Zero, skill.SkillId);
            }
        }
        [TestCase(0, 0)] [TestCase(5, 5)] [TestCase(15, 10)] [TestCase(20, 10)]
        [TestCase(35, 20)] [TestCase(40, 20)] [TestCase(50, 25)] [TestCase(55, 30)] [TestCase(60, 30)]
        public void RageDiscountRoundsUpToFive(int cost, int expected) => Assert.That(EnemyResourceProfilesSetup.RageCost(cost), Is.EqualTo(expected));

        [Test]
        public void ExplicitLegacyEncounterClassCannotOverrideTheEnemyResourceProfile()
        {
            var definition = Definition("enemy.catacombs.swordsman");
            var legacy = Catalog.CharacterClasses.Single(c => c.Id == "class.enemy.catacombs");
            var unit = new CharacterInstanceFactory().Create(definition, legacy);
            Assert.That(unit.ClassData, Is.SameAs(definition.ClassData)); Assert.That(unit.ResourcePool.ResourceType, Is.EqualTo(ResourceType.Rage));
            Assert.That(unit.ResourcePool.CurrentValue, Is.Zero);
        }
        [Test]
        public void EnemyEncounterOverridesAndBossPhasesAgreeWithAuthoredDefinitions()
        {
            foreach (var encounter in Catalog.EncounterDefinitions)
                foreach (var member in encounter.Enemies)
                    if (EnemyResourceProfilesSetup.TryGetType(member.CharacterDefinition?.Id, out _))
                        Assert.That(member.ClassData, Is.SameAs(member.CharacterDefinition.ClassData), encounter.Id);
            foreach (var d in Catalog.CharacterDefinitions)
                if (d.SecondPhaseDefinition != null && EnemyResourceProfilesSetup.TryGetType(d.Id, out _))
                    Assert.That(d.SecondPhaseDefinition.ClassData.ResourceType, Is.EqualTo(d.ClassData.ResourceType), d.Id);
        }
        [Test]
        public void PlayerResourceStartRulesRemainUnchanged()
        {
            foreach (var path in new[] { "CD_Jerzy", "CD_Wladimir_Test", "CD_Mage", "CD_Paladin" })
            {
                var d = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Characters/" + path + ".asset");
                Assert.That(d, Is.Not.Null, path);
                var character = new CharacterInstanceFactory().Create(d);
                Assert.That(character.ClassData.StartingResourcePercent, Is.EqualTo(-1));
                Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(character.ResourcePool.ResourceType == ResourceType.Mana
                    ? character.ResourcePool.MaxValue : 0), path);
            }
        }
        [Test]
        public void ApplyingProfilesAgainDoesNotDiscountCostsTwiceOrDuplicateClasses()
        {
            var costs = Catalog.SkillDefinitions.ToDictionary(s => s.SkillId, s => (s.ResourceType, s.ResourceCost));
            var classes = Catalog.CharacterClasses.Count;
            EnemyResourceProfilesSetup.ApplyAll();
            Assert.That(Catalog.CharacterClasses.Count, Is.EqualTo(classes));
            foreach (var skill in Catalog.SkillDefinitions) Assert.That((skill.ResourceType, skill.ResourceCost), Is.EqualTo(costs[skill.SkillId]), skill.SkillId);
        }
        [Test]
        public void RangedEnemyWithoutMomentumMovesOnceAndStillCanFireBasicAttack()
        {
            var shooter = Unit("enemy.forest.bandit_shooter"); var target = Unit("enemy.catacombs.swordsman", true, 5);
            shooter.Character.ResourcePool.CurrentValue = 0;
            var session = new CombatSession { Grid = new BattleGrid(12, 10), Units = new() { shooter, target }, ActiveUnit = shooter };
            var rules = new ResourceRuleSystem();
            var movement = new CombatMovementSystem(resourceRuleSystem: rules);
            var execution = new SkillExecutionSystem(resourceRuleSystem: rules);
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: execution, movementSystem: movement);
            var state = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, shooter, state, out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(action.MovementPath.Count, Is.InRange(1, 2)); Assert.That(shooter.Character.ResourcePool.CurrentValue, Is.GreaterThan(0));
            Assert.That(shooter.CurrentActionPoints, Is.GreaterThanOrEqualTo(CombatActionCostCalculator.CalculateSkillCost(shooter, shooter.Character.GetBasicAttackSkill())));
            Assert.That(ai.TryExecuteNextAction(session, shooter, state, out var attack), Is.True);
            Assert.That(attack.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            var afterReposition = shooter.Position;
            shooter.CurrentActionPoints = shooter.MaxActionPoints; shooter.Character.ResourcePool.CurrentValue = 0;
            Assert.That(ai.TryExecuteNextAction(session, shooter, state, out var repeated), Is.True);
            Assert.That(repeated.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(shooter.Position, Is.EqualTo(afterReposition), "One resource reposition per turn state, even after another drain.");
        }
        [Test]
        public void ResourceBlockedShooterDoesNotWasteApOnResourceReposition()
        {
            var shooter = Unit("enemy.forest.bandit_shooter"); var target = Unit("enemy.catacombs.swordsman", true, 5);
            shooter.Character.ResourcePool.CurrentValue = 0;
            shooter.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { BlocksResourceRegeneration = true });
            var session = new CombatSession { Grid = new BattleGrid(12, 10), Units = new() { shooter, target }, ActiveUnit = shooter };
            var start = shooter.Position;
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem());
            Assert.That(ai.TryExecuteNextAction(session, shooter, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill)); Assert.That(shooter.Position, Is.EqualTo(start));
            Assert.That(shooter.Character.ResourcePool.CurrentValue, Is.Zero);
        }
        [Test]
        public void DepletedRangedEnemyUsesRegainedMomentumOnSpecial()
        {
            // Let the AI choose the useful special; rebuilding a resource should
            // unlock actions without forcing one particular tactical preference.
            var shooter = Unit("enemy.forest.elf_archer"); var target = Unit("enemy.catacombs.swordsman", true, 5);
            shooter.Character.ResourcePool.CurrentValue = 0;
            var session = new CombatSession { Grid = new BattleGrid(12, 10), Units = new() { shooter, target }, ActiveUnit = shooter };
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem());
            var moved = false; var usedSpecial = false; var trace = new List<string>();
            for (var turn = 0; turn < 12 && !usedSpecial; turn++)
            {
                shooter.MovementStepsThisTurn = 0; // Same reset as BeginActiveTurn.
                shooter.CurrentActionPoints = shooter.MaxActionPoints;
                var state = ai.CreateTurnState();
                for (var actionIndex = 0; actionIndex < 16; actionIndex++)
                {
                    var before = shooter.Character.ResourcePool.CurrentValue;
                    if (!ai.TryExecuteNextAction(session, shooter, state, out var action)) break;
                    trace.Add($"Turn {turn}: {action.Type} {action.Skill?.SkillId}, resource {before}->{shooter.Character.ResourcePool.CurrentValue}");
                    moved |= action.Type == SimpleCombatAiActionType.Movement;
                    if (action.Skill?.ResourceCost > 0)
                    {
                        Assert.That(shooter.Character.ResourcePool.CurrentValue, Is.EqualTo(before - action.Skill.ResourceCost));
                        usedSpecial = true; break;
                    }
                }
            }
            Assert.That(moved, Is.True); Assert.That(usedSpecial, Is.True,
                "Normal paid movement must restore access to a special skill.\n" + string.Join("\n", trace));
        }
        [Test]
        public void RageEnemyCanOpenWithBasicAttackAndBuildItsOwnResource()
        {
            var enemy = Unit("enemy.catacombs.swordsman"); var target = Unit("enemy.forest.bandit_thug", true, 2);
            var session = new CombatSession { Grid = new BattleGrid(10, 10), Units = new() { enemy, target }, ActiveUnit = enemy };
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem(
                resourceRuleSystem: new ResourceRuleSystem()));
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(action.Skill.ResourceCost, Is.Zero); Assert.That(enemy.Character.ResourcePool.CurrentValue, Is.GreaterThan(0));
        }
        [Test]
        public void NewEnemySaveKeepsSpentResourceAndLegacyManaMigratesToProfileStart()
        {
            var d = Definition("enemy.catacombs.swordsman"); var character = new CharacterInstanceFactory().Create(d);
            character.ResourcePool.CurrentValue = 15;
            var game = new GameSession(GameModeState.Exploration, "resource.test");
            Assert.That(game.TryRegisterCharacter(character), Is.True);
            var save = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Enemy resource test");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, Catalog, out var restored), Is.True);
            Assert.That(restored.CharactersById[d.Id].ResourcePool.CurrentValue, Is.EqualTo(15));
            var old = save.Characters.Single(); old.ClassId = "class.enemy.catacombs";
            old.ResourcePool.ResourceType = ResourceType.Mana; old.ResourcePool.CurrentValue = old.ResourcePool.MaxValue = 100;
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, Catalog, out restored), Is.True);
            Assert.That(restored.CharactersById[d.Id].ResourcePool.ResourceType, Is.EqualTo(ResourceType.Rage));
            Assert.That(restored.CharactersById[d.Id].ResourcePool.CurrentValue, Is.Zero);
        }
        [TestCase("enemy.catacombs.swordsman", 0)]
        [TestCase("enemy.forest.bandit_shooter", 0)]
        [TestCase("enemy.forest.elf_druid", 100)]
        public void FirstCombatAdmissionAppliesOpeningProfileAfterExploration(string id, int expected)
        {
            var unit = Unit(id); var pool = unit.Character.ResourcePool;
            pool.CurrentValue = pool.ResourceType == ResourceType.Mana ? 0 : pool.MaxValue;
            pool.HasStartedTurn = pool.HasGainedResourceSinceLastTurn = true; pool.FractionalGainRemainder = .5f;
            ScoutKnifeSystem.ResetForCombat(new CombatSession { Units = new() { unit } }, true);
            Assert.That(pool.CurrentValue, Is.EqualTo(expected)); Assert.That(pool.HasStartedTurn, Is.False);
            Assert.That(pool.HasGainedResourceSinceLastTurn, Is.False); Assert.That(pool.FractionalGainRemainder, Is.Zero);
        }
        [Test]
        public void RebindingAndBossPhaseChangeDoNotRefillEnemyResource()
        {
            var unit = Unit("enemy.forest.bandit_leader"); var session = new CombatSession { Units = new() { unit } };
            ScoutKnifeSystem.ResetForCombat(session, true); unit.Character.ResourcePool.CurrentValue = 17;
            unit.Character.Definition = Definition("enemy.forest.bandit_leader.revealed");
            ScoutKnifeSystem.ResetForCombat(session, true); ScoutKnifeSystem.ResetForCombat(session);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(17));
        }
        [Test]
        public void CombatAdmissionDoesNotRefillPlayerSideResource()
        {
            var unit = Unit("enemy.forest.bandit_shooter", true); unit.Character.ResourcePool.CurrentValue = 7;
            ScoutKnifeSystem.ResetForCombat(new CombatSession { Units = new() { unit } });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(7));
        }
        [TestCase(100, 0)] [TestCase(124, 0)] [TestCase(125, 4)]
        [TestCase(149, 4)] [TestCase(150, 8)] [TestCase(174, 8)]
        [TestCase(175, 12)] [TestCase(199, 12)] [TestCase(200, 16)]
        public void MomentumSpeedUsesCompleteStepsAndNeverStacksOnRefresh(int resource, int bonus)
        {
            foreach (var player in new[] { false, true })
            {
                var unit = Unit("enemy.forest.bandit_shooter", player);
                var pipeline = new CharacterStatPipelineSystem(); pipeline.Refresh(unit.Character);
                var baseline = unit.Character.Stats.Speed.GetValue();
                unit.Character.ResourcePool.CurrentValue = resource;
                pipeline.Refresh(unit.Character); pipeline.Refresh(unit.Character);
                Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(baseline + bonus));
                unit.Character.ResourcePool.CurrentValue = 100; pipeline.Refresh(unit.Character);
                Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(baseline));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void PaidMovementCrossesSpeedThresholdAndAffectsFutureTurnApAndAv(bool player)
        {
            var unit = Unit("enemy.forest.bandit_shooter", player);
            unit.Character.ResourcePool.CurrentValue = 124;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            var speed = unit.Character.Stats.Speed.GetValue(); var av = CombatTurnSystem.CalculateBaseActionValue(unit);
            var ap = new CombatTurnSystem().CalculateMaxActionPoints(unit.Character, .25f);
            var session = new CombatSession { Grid = new BattleGrid(10, 10), Units = new() { unit } };
            Assert.That(new CombatMovementSystem(resourceRuleSystem: new ResourceRuleSystem())
                .TryMoveOneStep(session, unit, 1, 0), Is.True);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(125));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(speed + 4));
            Assert.That(CombatTurnSystem.CalculateBaseActionValue(unit), Is.LessThan(av));
            Assert.That(new CombatTurnSystem().CalculateMaxActionPoints(unit.Character, .25f), Is.EqualTo(ap + 100));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(900), "Speed must not refill spent AP mid-turn.");
        }
        [Test]
        public void SpendingMomentumOnSkillRemovesTheSpeedBonusImmediately()
        {
            var unit = Unit("enemy.forest.bandit_thug"); unit.CurrentActionPoints = 10000;
            unit.Character.ResourcePool.CurrentValue = 125;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            var speed = unit.Character.Stats.Speed.GetValue();
            var session = new CombatSession { Grid = new BattleGrid(10, 10), Units = new() { unit }, ActiveUnit = unit };
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem())
                .TryExecute(session, unit, unit.Character.Definition.StartingSkills[1], unit), Is.True);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(80));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(speed - 4));
        }
        [Test]
        public void RecoveryCrossesSpeedThresholdAndResourceBlockPreservesIt()
        {
            var unit = Unit("enemy.forest.bandit_shooter"); unit.Character.ResourcePool.CurrentValue = 124;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            var speed = unit.Character.Stats.Speed.GetValue(); var recovery = new CombatRecoverySystem();
            Assert.That(recovery.RestoreResource(unit, 1), Is.EqualTo(1));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(speed + 4));
            unit.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { BlocksResourceRegeneration = true });
            Assert.That(recovery.RestoreResource(unit, 25), Is.Zero);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(125));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(speed + 4));
        }
        [Test]
        public void RestoredMomentumAboveOneHundredKeepsValueAndDerivedSpeed()
        {
            var d = Definition("enemy.forest.bandit_shooter"); var character = new CharacterInstanceFactory().Create(d);
            character.ResourcePool.CurrentValue = 175;
            var game = new GameSession(GameModeState.Exploration, "momentum.save");
            game.TryRegisterCharacter(character);
            var save = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Momentum");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, Catalog, out var restored), Is.True);
            var loaded = restored.CharactersById[d.Id];
            Assert.That(loaded.ResourcePool.MaxValue, Is.EqualTo(200));
            Assert.That(loaded.ResourcePool.CurrentValue, Is.EqualTo(175));
            Assert.That(loaded.Stats.Speed.GetValue(), Is.EqualTo(loaded.Stats.Speed.BaseValue + 12));
        }
        [Test]
        public void CombatAdmissionResetsPreContactSpeedAlongWithStartingMomentum()
        {
            var unit = Unit("enemy.forest.bandit_shooter"); unit.Character.ResourcePool.CurrentValue = 200;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            var boosted = unit.Character.Stats.Speed.GetValue();
            ScoutKnifeSystem.ResetForCombat(new CombatSession { Units = new() { unit } });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(boosted - 16));
        }
        [TestCase("enemy.catacombs.swordsman")]
        [TestCase("enemy.forest.elf_druid")]
        public void HighResourceSpeedDoesNotApplyToOtherResourceTypes(string id)
        {
            var unit = Unit(id); unit.Character.ResourcePool.CurrentValue = 200;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(unit.Character.Stats.Speed.BaseValue));
        }
    }
}
