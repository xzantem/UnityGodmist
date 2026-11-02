using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class SimpleCombatAiSystemTests
    {
        private static SkillData PaladinSkill(string name) => AssetDatabase.LoadAssetAtPath<SkillData>(
            "Assets/_Project/ScriptableObjects/Skills/SD_Paladin" + name + ".asset");

        private CombatSession PaladinRecoverySession(params string[] names)
        {
            var session = PlannerSession(5);
            var caster = session.ActiveUnit;
            caster.Character = new CharacterInstanceFactory().Create(
                AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset"),
                AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/_Project/ScriptableObjects/Classes/CCD_Paladin.asset"));
            caster.Character.SkillTreeProgress.Clear();
            foreach (var name in names)
                if (!caster.Character.Skills.Contains(PaladinSkill(name))) caster.Character.Skills.Add(PaladinSkill(name));
            var basic = caster.Character.GetBasicAttackSkill().SkillId;
            Assert.That(caster.Character.ActiveSkillLoadout.TryInitialize(caster.Character.GetKnownSkills().Select(skill => skill.SkillId),
                basic, new[] { basic }.Concat(names.Select(name => PaladinSkill(name).SkillId))), Is.True);
            caster.Character.Stats.MaxHealth = new Stat(1000); caster.Character.CurrentHealth = 1000;
            caster.Character.Stats.MinAttack = new Stat(100); caster.Character.Stats.MaxAttack = new Stat(100);
            caster.Character.Stats.CritChance = new Stat(0); caster.Character.Stats.CritDamage = new Stat(2);
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Mana, 100, 100, 0);
            caster.CurrentActionPoints = caster.MaxActionPoints = 10000;
            var ally = CreateAiCombatUnit("recovery-ally", caster.IsPlayerSide, new GridPosition(1, 0));
            ally.Character.Stats.MaxHealth = new Stat(1000); ally.Character.CurrentHealth = 100;
            session.Units.Add(ally);
            foreach (var unit in session.Units) unit.WarriorState.Session = session;
            return session;
        }

        private static void RecoveryTalent(BattleUnit caster, string name, int rank)
        {
            var progress = caster.Character.SkillTreeProgress.FirstOrDefault(p => p.TreeId == PaladinCombatSystem.Priest);
            if (progress == null) { progress = new SkillTreeProgress { TreeId = PaladinCombatSystem.Priest }; caster.Character.SkillTreeProgress.Add(progress); }
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = "node.paladin.priest." + name, Ranks = rank });
        }

        [TestCase("Heal")][TestCase("GroupHeal")][TestCase("HolyRegeneration")]
        public void PaladinAiSelectsAndActuallyExecutesAuthoredRecovery(string name)
        {
            var session = PaladinRecoverySession(name); var caster = session.ActiveUnit; var target = session.Units[2];
            var skill = PaladinSkill(name); var rng = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(31);
                var ai = CreateProductionWraithAi();
                Assert.That(TacticalScore(session, caster, skill, target), Is.GreaterThan(0));
                Assert.That(ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var action), Is.True);
                Assert.That(action.Skill, Is.SameAs(skill));
                if (name == "HolyRegeneration")
                {
                    Assert.That(target.Character.CurrentHealth, Is.EqualTo(100), "No haste: future healing is not immediate HP.");
                    new StatusEffectSystem().ProcessTurnEnd(target);
                }
                Assert.That(target.Character.CurrentHealth, Is.GreaterThan(100));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase("Heal")][TestCase("GroupHeal")][TestCase("HolyRegeneration")]
        public void PaladinAiDoesNotSpendRecoveryOnFullHealth(string name)
        {
            var session = PaladinRecoverySession(name); session.Units[2].Character.CurrentHealth = 1000;
            Assert.That(TacticalScore(session, session.ActiveUnit, PaladinSkill(name), session.Units[2]), Is.LessThanOrEqualTo(0));
            var ai = CreateProductionWraithAi(); ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action);
            Assert.That(action.Skill, Is.Not.SameAs(PaladinSkill(name)));
        }

        [TestCase(1)][TestCase(2)][TestCase(3)]
        public void PaladinAiUsesExactTargetDiscountAndExecutionConsumesIt(int rank)
        {
            var session = PaladinRecoverySession("Heal"); var caster = session.ActiveUnit; var target = session.Units[2];
            RecoveryTalent(caster, "persistent_healer", rank); target.PaladinState.HealingDiscount = true;
            var cost = CombatActionCostCalculator.CalculateSkillCostForTarget(caster, PaladinSkill("Heal"), target);
            caster.CurrentActionPoints = cost;
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState(); var rng = UnityEngine.Random.state;
            try
            {
                Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var action), Is.True);
                Assert.That(action.Skill, Is.SameAs(PaladinSkill("Heal")));
                Assert.That(caster.CurrentActionPoints, Is.Zero); Assert.That(target.PaladinState.HealingDiscount, Is.False);
                Assert.That(caster.Character.ResourcePool.CurrentValue, Is.LessThan(100), "AP discount does not waive mana.");
                if (rank == 3) Assert.That(ai.TryExecuteNextAction(session, caster, turn, out _), Is.False);
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [Test]
        public void DiscountDoesNotMakeAnotherUnaffordableTargetEligible()
        {
            var session = PaladinRecoverySession("Heal"); var caster = session.ActiveUnit; var target = session.Units[2];
            RecoveryTalent(caster, "persistent_healer", 2); target.PaladinState.HealingDiscount = true;
            target.Character.CurrentHealth = 600;
            var other = CreateAiCombatUnit("more-wounded-no-discount", caster.IsPlayerSide, new GridPosition(2, 0));
            other.Character.Stats.MaxHealth = new Stat(1000); other.Character.CurrentHealth = 1; session.Units.Add(other);
            caster.CurrentActionPoints = CombatActionCostCalculator.CalculateSkillCostForTarget(caster, PaladinSkill("Heal"), target);
            var ai = CreateProductionWraithAi(); var rng = UnityEngine.Random.state;
            try
            {
                Assert.That(ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var action), Is.True);
                Assert.That(action.Skill, Is.SameAs(PaladinSkill("Heal")));
                Assert.That(target.Character.CurrentHealth, Is.GreaterThan(600)); Assert.That(other.Character.CurrentHealth, Is.EqualTo(1));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(1)][TestCase(17)][TestCase(101)][TestCase(815)]
        public void AuthoredPaladinAiRecoveryScenarioAcrossSeededTurns(int seed)
        {
            var session = PaladinRecoverySession("Heal", "GroupHeal", "HolyRegeneration");
            var caster = session.ActiveUnit; var target = session.Units[2]; var ai = CreateProductionWraithAi();
            RecoveryTalent(caster, "healing_power", 2); RecoveryTalent(caster, "haste", 2);
            RecoveryTalent(caster, "unexpected_grace", 2); caster.Character.Stats.CritChance = new Stat(.3f);
            var rng = UnityEngine.Random.state; var starting = target.Character.CurrentHealth; var casts = 0;
            try
            {
                UnityEngine.Random.InitState(seed);
                for (var turn = 0; turn < 3; turn++)
                {
                    caster.CurrentActionPoints = caster.MaxActionPoints; caster.Character.ResourcePool.CurrentValue = 100;
                    var state = ai.CreateTurnState();
                    for (var actionIndex = 0; actionIndex < 6; actionIndex++)
                    {
                        if (!ai.TryExecuteNextAction(session, caster, state, out var action)) break;
                        if (action.Skill?.Effects.Any(effect => effect.EffectType is EffectType.PaladinHeal or EffectType.PaladinRegeneration) == true)
                        { casts++; TestContext.WriteLine($"Seed={seed}, turn={turn}, action={action.Skill.SkillId}, HP={target.Character.CurrentHealth}"); }
                    }
                    new StatusEffectSystem().ProcessTurnEnd(target);
                }
                Assert.That(casts, Is.GreaterThan(0)); Assert.That(target.Character.CurrentHealth, Is.GreaterThan(starting));
                Assert.That(target.Character.CurrentHealth, Is.LessThanOrEqualTo(target.Character.MaximumHealth));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(false)][TestCase(true)]
        public void PurelyFutureRegenerationDoesNotPretendToReviveDownedAlly(bool haste)
        {
            var session = PaladinRecoverySession("HolyRegeneration"); var caster = session.ActiveUnit; var target = session.Units[2];
            target.Character.LifeState = CharacterLifeState.Downed; target.Character.CurrentHealth = 0; target.DownedTurnsRemaining = 1;
            if (haste) RecoveryTalent(caster, "haste", 1);
            var score = TacticalScore(session, caster, PaladinSkill("HolyRegeneration"), target);
            Assert.That(haste ? score > 0 : score <= 0, Is.True);
        }

        [Test]
        public void RoundedHealingExpectationMatchesIndependentDiscreteIntegral()
        {
            foreach (var minimum in new[] { 0f, .35f, 7.5f, 89.2f, 200.55f })
            foreach (var width in new[] { .1f, 1f, 37.7f })
            foreach (var cap in new[] { 1, 15, 100, 500 })
            {
                var maximum = minimum + width; var oracle = 0d;
                for (var integer = 0; integer <= Mathf.CeilToInt(maximum) + 1; integer++)
                {
                    var length = Math.Max(0d, Math.Min(maximum, integer + .5d) - Math.Max(minimum, integer - .5d));
                    oracle += Math.Min(cap, integer) * length / (maximum - minimum);
                }
                Assert.That(PaladinCombatSystem.ExpectedHealingRoll(minimum, maximum, cap), Is.EqualTo(oracle).Within(.0001));
            }
        }

        [TestCase("Heal")][TestCase("GroupHeal")][TestCase("HolyRegeneration")]
        public void PaladinAiRespectsActualManaCost(string name)
        {
            var session = PaladinRecoverySession(name); var caster = session.ActiveUnit;
            caster.Character.ResourcePool.CurrentValue = PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, PaladinSkill(name)) - 1;
            var ai = CreateProductionWraithAi(); ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var action);
            Assert.That(action.Skill, Is.Not.SameAs(PaladinSkill(name)));
        }

        [Test]
        public void PaladinGroupScoreCountsWoundedRecipientsAndActualSingleTargetTalent()
        {
            var session = PaladinRecoverySession("GroupHeal"); var caster = session.ActiveUnit; var target = session.Units[2];
            RecoveryTalent(caster, "healer_focus", 3);
            var skill = PaladinSkill("GroupHeal"); var effect = skill.Effects.Single();
            var one = PaladinCombatSystem.ForecastHealing(session, caster, target, skill, effect, true, 2).Immediate;
            var many = PaladinCombatSystem.ForecastHealing(session, caster, target, skill, effect, false, 2).Immediate;
            Assert.That(one / many, Is.EqualTo(1.45f).Within(.002));
            caster.Character.CurrentHealth = 100;
            Assert.That(TacticalScore(session, caster, skill, target, caster), Is.GreaterThan(TacticalScore(session, caster, skill, target)));
        }

        [Test]
        public void PaladinRegenerationRecastValuesOnlyIncrementOverRemainingTicks()
        {
            var session = PaladinRecoverySession("HolyRegeneration"); var caster = session.ActiveUnit; var target = session.Units[2];
            var skill = PaladinSkill("HolyRegeneration"); var fresh = TacticalScore(session, caster, skill, target);
            target.Character.PassiveEffects.Add(PaladinCombatSystem.CreateHolyRegeneration(caster, target, false));
            var existing = target.Character.PassiveEffects.Last(); var rng = UnityEngine.Random.state;
            Assert.That(TacticalScore(session, caster, skill, target), Is.LessThanOrEqualTo(0));
            Assert.That(existing.RemainingDuration, Is.EqualTo(4)); Assert.That(existing.GetStateValue("remaining").IntegerValue, Is.EqualTo(400));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(rng));
            existing.RemainingDuration = 1; existing.SetStateValue(Godmist.Runtime.Effects.PassiveEffectRuntimeStateValue.Integer("remaining", 100));
            Assert.That(TacticalScore(session, caster, skill, target), Is.GreaterThan(0).And.LessThan(fresh));
        }

        [TestCase(0,false)][TestCase(1,false)][TestCase(2,false)][TestCase(3,false)]
        [TestCase(0,true)][TestCase(1,true)][TestCase(2,true)][TestCase(3,true)]
        public void RegenerationForecastUsesActualHasteAndLastingTicksWithoutMutation(int rank, bool critical)
        {
            var session = PaladinRecoverySession("HolyRegeneration"); var caster = session.ActiveUnit; var target = session.Units[2];
            RecoveryTalent(caster, "haste", rank); RecoveryTalent(caster, "lasting_effect", rank);
            if (critical) { RecoveryTalent(caster, "unexpected_grace", 3); caster.Character.Stats.CritChance = new Stat(1); }
            var rng = UnityEngine.Random.state;
            try
            {
                target.Character.CurrentHealth = 1;
                var forecast = PaladinCombatSystem.CreateHolyRegeneration(caster, target, critical);
                var expected = Math.Min(999, PaladinCombatSystem.CalculateRegenerationUpfront(caster, target, critical) + PaladinCombatSystem.ForecastRegenerationTicks(forecast, 8));
                var execution = new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem());
                Assert.That(execution.TryExecute(session, caster, PaladinSkill("HolyRegeneration"), target), Is.True);
                for (var tick = 0; tick < 8; tick++) new StatusEffectSystem().ProcessTurnEnd(target);
                Assert.That(target.Character.CurrentHealth - 1, Is.EqualTo(expected));
                Assert.That(expected, Is.EqualTo(critical ? (rank == 0 ? 800 : 999) : (rank == 0 ? 400 : 500)));
                Assert.That(forecast.RemainingDuration, Is.EqualTo(4));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [TestCase(0, 900, false)][TestCase(2, 900, false)][TestCase(3, 30, false)]
        [TestCase(2, 900, true)][TestCase(3, 30, true)]
        public void DirectHealingExpectationMatchesSeededRealExecutionWithTalentsAndOverheal(int criticalRank, int missing, bool self)
        {
            var session = PaladinRecoverySession("Heal"); var caster = session.ActiveUnit; var target = session.Units[2];
            if (self) target = caster;
            RecoveryTalent(caster, "healing_power", 2); RecoveryTalent(caster, "healer_focus", 2);
            RecoveryTalent(caster, "unexpected_grace", criticalRank); RecoveryTalent(caster, "critical_focus", 2);
            caster.Character.Stats.CritChance = new Stat(.4f);
            target.Character.CurrentHealth = 1000 - missing;
            var skill = PaladinSkill("Heal"); var effect = skill.Effects.Single();
            var expected = PaladinCombatSystem.ForecastHealing(session, caster, target, skill, effect, true, 2).Immediate;
            var rng = UnityEngine.Random.state; var total = 0; const int samples = 2048;
            try
            {
                UnityEngine.Random.InitState(815);
                for (var sample = 0; sample < samples; sample++)
                {
                    target.Character.CurrentHealth = 1000 - missing;
                    Assert.That(PaladinCombatSystem.Execute(session, caster, target, skill, effect, new CombatRecoverySystem(), true), Is.True);
                    total += target.Character.CurrentHealth - (1000 - missing);
                }
                TestContext.WriteLine($"Expected={expected}; execution mean={total/(float)samples}; N={samples}");
                Assert.That(total/(float)samples, Is.EqualTo(expected).Within(4));
            }
            finally { UnityEngine.Random.state = rng; }
        }

        [Test]
        public void RegenerationProjectionKeepsFutureHealthSeparateAndCannotScoreSameRecastTwice()
        {
            var session = PaladinRecoverySession("HolyRegeneration"); var caster = session.ActiveUnit; var target = session.Units[2];
            var type = typeof(SimpleCombatAiSystem);
            var nodeType = type.GetNestedType("AiTurnPlanNode", BindingFlags.NonPublic);
            var node = Activator.CreateInstance(nodeType, true); nodeType.GetField("World").SetValue(node, session);
            nodeType.GetField("OwnedUnits").SetValue(node, new HashSet<int> { 0, 2 });
            var choiceType = type.GetNestedType("AiSkillSelection", BindingFlags.NonPublic);
            var selection = Activator.CreateInstance(choiceType, true);
            choiceType.GetField("Skill").SetValue(selection, PaladinSkill("HolyRegeneration"));
            choiceType.GetField("TargetsByModeIndex").SetValue(selection, new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { target } });
            var project = type.GetMethod("ProjectSkill", BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = CreateProductionWraithAi();
            var first = project.Invoke(ai, new[] { node, selection, session, caster });
            Assert.That(first, Is.Not.Null);
            var world = (CombatSession)nodeType.GetField("World").GetValue(first);
            Assert.That(world.Units[2].Character.CurrentHealth, Is.EqualTo(100));
            choiceType.GetField("TargetsByModeIndex").SetValue(selection, new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { world.Units[2] } });
            var second = project.Invoke(ai, new[] { first, selection, session, caster });
            Assert.That(second, Is.Not.Null);
            Assert.That((float)nodeType.GetField("Score").GetValue(second), Is.LessThan((float)nodeType.GetField("Score").GetValue(first)));
            Assert.That(target.Character.PassiveEffects, Is.Empty); Assert.That(target.Character.CurrentHealth, Is.EqualTo(100));
        }
    }
}
