using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public class ManaEconomyTests
    {
        private static BattleUnit Hero(string file = "CD_Mage", int mana = 0)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/" + file + ".asset");
            var character = new CharacterInstanceFactory().Create(definition);
            character.ResourcePool.CurrentValue = mana;
            return new BattleUnit
            {
                Character = character, IsPlayerSide = true, Position = new GridPosition(1, 1),
                MaxActionPoints = 1000, CurrentActionPoints = 1000
            };
        }

        private static void Rank(BattleUnit unit, string node, int ranks)
        {
            var progress = unit.Character.SkillTreeProgress.FirstOrDefault(p => p.TreeId == MageCombatSystem.Warlock);
            if (progress == null)
                unit.Character.SkillTreeProgress.Add(progress = new SkillTreeProgress { TreeId = MageCombatSystem.Warlock });
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = "node.mage.warlock." + node, Ranks = ranks });
        }

        [TestCase("CD_Mage")]
        [TestCase("CD_Paladin")]
        public void ProductionHeroesHave15BaseRegenAndDoNotGainNaturalManaAtTurnStart(string file)
        {
            var unit = Hero(file);
            Assert.That(unit.Character.Stats.ResourceRegeneration.BaseValue, Is.EqualTo(15));
            Assert.That(unit.Character.ResourcePool.Regeneration, Is.EqualTo(15));
            new ResourceRuleSystem().ProcessTurnStart(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
        }

        [TestCase(1000, 40)]
        [TestCase(750, 30)]
        [TestCase(500, 20)]
        [TestCase(250, 12)]
        [TestCase(0, 5)]
        [TestCase(-20, 5)]
        [TestCase(2000, 40)]
        public void EndTurnUsesRemainingApAndClampsTheRatio(int remaining, int expected)
        {
            var unit = Hero(); unit.CurrentActionPoints = remaining;
            unit.Character.ResourcePool.Regeneration = 20;
            new ResourceRuleSystem().ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(expected));
        }

        [TestCase("CD_Mage")]
        [TestCase("CD_Paladin")]
        public void ExplorationDoesNotRestoreManaRegardlessOfRemainingAp(string file)
        {
            var unit = Hero(file); unit.CurrentActionPoints = 0;
            var rules = new ResourceRuleSystem();
            rules.ProcessExplorationTick(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            unit.CurrentActionPoints = 1000;
            rules.ProcessExplorationTick(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
        }

        [Test]
        public void NpcManaAndFriendlyAiSummonKeepTheirExistingStartTick()
        {
            foreach (var summon in new[] { false, true })
            {
                var unit = Hero(); unit.IsPlayerSide = summon; unit.IsAiControlledSummon = summon;
                var rules = new ResourceRuleSystem();
                rules.ProcessTurnStart(unit);
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(15));
                rules.ProcessTurnEnd(unit);
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(15));
            }
        }

        [TestCase(CharacterLifeState.Downed)]
        [TestCase(CharacterLifeState.Dead)]
        public void IncapacitatedCharactersDoNotRecoverMana(CharacterLifeState state)
        {
            var unit = Hero(); unit.Character.LifeState = state;
            var rules = new ResourceRuleSystem();
            rules.ProcessTurnEnd(unit); rules.ProcessExplorationTick(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
        }

        [Test]
        public void ZeroApBudgetCannotGrantManaAndNonManaIsUnaffected()
        {
            var unit = Hero(); unit.MaxActionPoints = 0;
            new ResourceRuleSystem().ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            foreach (var type in new[] { ResourceType.Rage, ResourceType.Momentum })
            {
                unit = Hero(); unit.Character.ResourcePool.ResourceType = type;
                new ResourceRuleSystem().ProcessTurnEnd(unit);
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            }
        }

        [TestCase(0, 100)]
        [TestCase(15, 115)]
        [TestCase(30, 130)]
        public void EndTurnHonoursOverflowAndPublishesOnlyActualGain(int overflow, int expected)
        {
            var unit = Hero(mana: 95); unit.Character.ResourcePool.ManaOverflowPercent = overflow;
            unit.Character.ResourcePool.Regeneration = 20;
            var events = new List<CombatFeedbackEvent>();
            var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            new ResourceRuleSystem(feedbackBus: bus).ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(expected));
            Assert.That(events.Single(e => e.Type == CombatFeedbackType.ResourceGain).Amount, Is.EqualTo(expected - 95));
            Assert.That(unit.Character.ResourcePool.MaxValue, Is.EqualTo(100));
        }

        [Test]
        public void BlockSuppressesEvenFullRestAndPreservesPoolState()
        {
            var unit = Hero();
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime { BlocksResourceRegeneration = true });
            unit.Character.ResourcePool.FractionalGainRemainder = .5f;
            new ResourceRuleSystem().ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.Character.ResourcePool.FractionalGainRemainder, Is.EqualTo(.5f));
        }

        [TestCase(0, 0, 20)]
        [TestCase(1, 0, 25)]
        [TestCase(2, 0, 30)]
        [TestCase(1, 50, 22)]
        [TestCase(2, 50, 25)]
        [TestCase(2, 100, 20)]
        public void MissingManaTalentRetainsItsMechanicWithSmallerRanks(int rank, int mana, int expected)
        {
            var unit = Hero(mana: mana);
            unit.Character.Stats.ResourceRegeneration = new Stat(20);
            Rank(unit, "energy_saving", rank);
            var pipeline = new CharacterStatPipelineSystem();
            pipeline.Refresh(unit.Character); pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.ResourcePool.Regeneration, Is.EqualTo(expected));
        }

        [Test]
        public void EndTurnRecalculatesMissingManaAfterSpending()
        {
            var unit = Hero(mana: 100);
            unit.Character.Stats.ResourceRegeneration = new Stat(20);
            Rank(unit, "energy_saving", 2);
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            unit.Character.ResourcePool.Drain(80);
            unit.CurrentActionPoints = 250;
            new ResourceRuleSystem().ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(38),
                "After spending: regen28 × 62.5% =17.5, rounded to18.");
        }

        [TestCase(30, 30)]
        [TestCase(36, 29)]
        [TestCase(50, 27)]
        public void PassiveCostArithmeticUsesHalfPercentPerPoint(int points, int expected)
        {
            var unit = Hero();
            // Cost arithmetic boundary, not a claim that one node can legally hold these ranks.
            Rank(unit, "mastery", 1); Rank(unit, "cost_arithmetic_fixture", points - 1);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_MageFireball.asset");
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, skill), Is.EqualTo(expected));
            Assert.That(skill.ResourceCost, Is.EqualTo(40));
            Assert.That(skill.ActionCost, Is.EqualTo(75));
        }

        [Test]
        public void CentralTurnEndRecoversOnceBeforeTheNextStart()
        {
            var unit = Hero();
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            var rules = new ResourceRuleSystem(feedbackBus: bus);
            var session = new CombatSession
            {
                CombatId = "test.mana.end", Grid = new BattleGrid(4, 4), Units = new List<BattleUnit> { unit },
                Outcome = CombatOutcomeState.Ongoing
            };
            var turns = new CombatTurnSystem(onUnitTurnBegan: rules.ProcessTurnStart, resourceRuleSystem: rules);
            turns.InitializeTurnOrder(session);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            turns.EndActiveTurn(session);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(30));
            Assert.That(events.Count(e => e.Type == CombatFeedbackType.ResourceGain), Is.EqualTo(1));
        }

        [Test]
        public void EndTurnBlockIsCheckedBeforeItsExpiryCallback()
        {
            var unit = Hero();
            var block = new PassiveEffectRuntime { BlocksResourceRegeneration = true };
            unit.Character.PassiveEffects.Add(block);
            var session = new CombatSession
            {
                CombatId = "test.mana.block", Grid = new BattleGrid(4, 4), Units = new List<BattleUnit> { unit },
                Outcome = CombatOutcomeState.Ongoing
            };
            var rules = new ResourceRuleSystem();
            var turns = new CombatTurnSystem(onUnitTurnBegan: rules.ProcessTurnStart,
                onUnitTurnEnded: u => u.Character.PassiveEffects.Remove(block), resourceRuleSystem: rules);
            turns.InitializeTurnOrder(session); turns.EndActiveTurn(session);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            turns.EndActiveTurn(session);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(30));
        }

        [Test]
        public void ActualFireballCostsAndEndTickUseTheNewEconomy()
        {
            var unit = Hero(mana: 100);
            Rank(unit, "mastery", 1); Rank(unit, "energy_saving", 2);
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            unit.MaxActionPoints = unit.CurrentActionPoints = 1125;
            var target = Hero(mana: 100); target.IsPlayerSide = false; target.Position = new GridPosition(3, 1);
            var session = new CombatSession
            {
                CombatId = "test.mana.fireball", Grid = new BattleGrid(8, 8),
                Units = new List<BattleUnit> { unit, target }, ActiveUnit = unit, Outcome = CombatOutcomeState.Ongoing
            };
            var rules = new ResourceRuleSystem();
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_MageFireball.asset");
            var execution = new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), resourceRuleSystem: rules);
            Assert.That(execution.TryExecute(session, unit, skill, target), Is.True);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(65));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(281));
            rules.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(76));
        }

        [Test]
        public void ActualPaladinHealLeaves60PercentApAndRestores18Mana()
        {
            var unit = Hero("CD_Paladin", 100);
            unit.Character.CurrentHealth -= 20;
            unit.MaxActionPoints = unit.CurrentActionPoints = 875;
            var session = new CombatSession
            {
                CombatId = "test.mana.heal", Grid = new BattleGrid(4, 4),
                Units = new List<BattleUnit> { unit }, ActiveUnit = unit, Outcome = CombatOutcomeState.Ongoing
            };
            var rules = new ResourceRuleSystem();
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinHeal.asset");
            var execution = new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), resourceRuleSystem: rules);
            Assert.That(execution.TryExecute(session, unit, skill, unit), Is.True);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(70));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(525));
            rules.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(88));
        }

        [Test]
        public void ProductionDescriptionsMatchTheNewTalentAndPassive()
        {
            var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>("Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Mage_Warlock.asset");
            Assert.That(tree.GlobalPassive.Description, Does.Contain("-0,5%"));
            var node = tree.Nodes.Single(n => n.Id == "node.mage.warlock.energy_saving");
            Assert.That(SkillTreeNodeView.BuildRankDescription(node, 1), Does.Contain("0,25%"));
            Assert.That(SkillTreeNodeView.BuildRankDescription(node, 2), Does.Contain("0,5%"));
            Assert.That(SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree, 30), Does.Contain("-15%"));
        }
    }
}
