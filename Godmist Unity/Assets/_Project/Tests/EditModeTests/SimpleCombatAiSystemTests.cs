using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class SimpleCombatAiSystemTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new();

        [TestCase("open")]
        [TestCase("diagonal")]
        [TestCase("chain")]
        [TestCase("wall")]
        [TestCase("edge")]
        [TestCase("ally")]
        public void ChargeGeometryPreviewMatchesExecutionWithoutMutationOrRandomRolls(string layout)
        {
            var attack = PlannerAttack("charge-geometry", 20, 1);
            var charge = CreateEffect(EffectType.Charge, 1);
            SetField(charge, "displacementDistance", 3); SetField(charge, "baseChance", 1f);
            var session = PlannerSession(3, attack);
            var caster = session.ActiveUnit; var target = session.Units[1];
            session.Grid = new BattleGrid(10, 6); caster.Position = new GridPosition(1, 2);
            target.Position = new GridPosition(3, layout == "diagonal" ? 4 : 2);
            if (layout == "chain")
            {
                var other = CreateNavigationSession(2, 1, new GridPosition(0, 0), new GridPosition(1, 0)).Units[1];
                other.Position = new GridPosition(4, 2); session.Units.Add(other);
            }
            if (layout == "wall") session.Grid.SetBlocked(new GridPosition(5, 2));
            if (layout == "edge") session.Grid.SetBlockedEdge(new GridPosition(2, 2), new GridPosition(3, 2), true);
            if (layout == "ally")
            {
                var other = CreateNavigationSession(2, 1, new GridPosition(0, 0), new GridPosition(1, 0)).ActiveUnit;
                other.Position = new GridPosition(4, 2); session.Units.Add(other);
            }
            var owner = new CombatChargeSystem(); var before = session.Units.Select(unit => unit.Position).ToArray();
            var rng = JsonUtility.ToJson(UnityEngine.Random.state); var callbacks = 0;
            session.UnitEnteredCell = _ => callbacks++;
            Assert.That(owner.TryPreviewCharge(session, caster, target, charge, out var positions, out _, out _), Is.EqualTo(layout != "ally"));
            Assert.That(session.Units.Select(unit => unit.Position), Is.EqualTo(before));
            Assert.That(callbacks, Is.Zero);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(rng));
            var state = UnityEngine.Random.state;
            try
            {
                Assert.That(owner.TryCharge(session, caster, target, attack, charge, 0), Is.EqualTo(layout != "ally"));
                foreach (var unit in session.Units)
                    Assert.That(unit.Position, Is.EqualTo(positions.TryGetValue(unit, out var expected) ? expected : before[session.Units.IndexOf(unit)]));
            }
            finally { UnityEngine.Random.state = state; }
        }

        [TestCase(0f)]
        [TestCase(.5f)]
        public void ChargePreviewDoesNotInventCertainPushesThroughResistantTargets(float chance)
        {
            var session = PlannerSession(2, PlannerAttack("charge", 20, 1));
            var effect = CreateEffect(EffectType.Charge, 1);
            SetField(effect, "displacementDistance", 2); SetField(effect, "baseChance", chance);
            Assert.That(new CombatChargeSystem().TryPreviewCharge(session, session.ActiveUnit, session.Units[1], effect,
                out var positions, out _, out var complete), Is.True);
            Assert.That(complete, Is.False);
            Assert.That(positions[session.ActiveUnit], Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(positions.ContainsKey(session.Units[1]), Is.False);
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void ProductionLeapsAreChosenForAffordableFollowUpAndCannotStealItsBudget(bool wolf, bool funded)
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(wolf
                ? "Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_RabidWolf.asset"
                : "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_ghoul.asset");
            Assert.That(definition, Is.Not.Null);
            var leap = definition.StartingSkills.First(skill => skill.Effects.Any(effect => effect.EffectType == EffectType.Charge));
            var session = PlannerSession(wolf ? 3 : 2, definition.StartingSkills.ToArray());
            session.Grid = new BattleGrid(9, 3);
            var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Position = new GridPosition(1, 1); target.Position = new GridPosition(wolf ? 4 : 3, 1);
            caster.MovementStepsThisTurn = 30;
            caster.Character.ResourcePool = new ResourcePool(leap.ResourceType, funded ? 100 : 0, 100, 0);
            caster.Character.Stats.MinAttack = new Stat(1000); caster.Character.Stats.MaxAttack = new Stat(1000);
            target.Character.CurrentHealth = 10000; target.Character.Stats.MaxHealth = new Stat(10000);
            caster.Character.Stats.WeaponAccuracy = new Stat(10000); caster.Character.WeaponAccuracy = 10000;
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            var acted = ai.TryExecuteNextAction(session, caster, turn, out var first);
            TestContext.WriteLine($"{definition.Id} funded={funded}: {first.Type} {first.Skill?.SkillId} AP={caster.CurrentActionPoints}");
            if (!funded) { Assert.That(first.Skill, Is.Not.SameAs(leap)); return; }
            Assert.That(acted, Is.True);
            Assert.That(first.Skill, Is.SameAs(leap));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var follow), Is.True);
            Assert.That(follow.Skill, Is.SameAs(definition.BasicAttackSkill));
            Assert.That(target.Character.CurrentHealth, Is.LessThan(10000));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LeapWithoutAnyAffordableAttackDoesNotSpendResourcesJustForItsModifier(bool wolf)
        {
            var path = wolf ? "Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Wolf_leap.asset"
                : "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_disorienting_leap.asset";
            var leap = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(path);
            var session = PlannerSession(wolf ? 3 : 2, leap);
            session.ActiveUnit.Character.ResourcePool = new ResourcePool(leap.ResourceType, 100, 100, 0);
            var ai = CreateProductionWraithAi();
            ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action);
            Assert.That(action.Skill, Is.Not.SameAs(leap));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProductionBanditFlanksForBackstabWhenMovementAndAttackFitTheBudget(bool invisible)
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_BanditThug.asset");
            var backstab = definition.StartingSkills.First(skill => skill.Effects.Any(effect => effect.RearAttackDamageBonus > 0));
            var session = PlannerSession(2, backstab);
            session.Grid = new BattleGrid(8, 6); var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Position = new GridPosition(3, 1); target.Position = new GridPosition(3, 3);
            target.ScoutState.Facing = new Vector2Int(1, 0);
            caster.Character.Stats.MinAttack = new Stat(100); caster.Character.Stats.MaxAttack = new Stat(100);
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Momentum, 100, 200, 0);
            if (invisible) caster.Character.PassiveEffects.Add(new PassiveEffectRuntime
                { EffectId = "invisibility", Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var first), Is.True);
            Assert.That(first.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(ScoutCombatSystem.Behind(caster, target), Is.True);
            Assert.That(caster.CurrentActionPoints, Is.GreaterThanOrEqualTo(CombatActionCostCalculator.CalculateSkillCost(caster, backstab)));
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.EqualTo(invisible));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var hit), Is.True);
            Assert.That(hit.Skill, Is.SameAs(backstab));
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RearDamagePreviewIncludesAuthoredBonusBeforeDefenseAndMatchesRealHit(bool rear)
        {
            var skill = PlannerAttack("rear-preview", 70, .9f);
            SetField(skill.Effects[0], "rearAttackDamageBonus", .5f);
            var session = PlannerSession(1, skill); var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Character.Stats.MinAttack = new Stat(101); caster.Character.Stats.MaxAttack = new Stat(101);
            target.ScoutState.Facing = new Vector2Int(rear ? 1 : -1, 0);
            target.Character.Stats.PhysicalDefense = new Stat(90);
            var expected = CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session, caster, target, skill, 0);
            var before = target.Character.CurrentHealth; var state = UnityEngine.Random.state;
            try
            {
                Assert.That(new SkillExecutionSystem().TryExecute(session, caster, skill, target), Is.True);
                Assert.That(before - target.Character.CurrentHealth, Is.EqualTo(expected));
            }
            finally { UnityEngine.Random.state = state; }
        }

        [TestCase("useful")]
        [TestCase("bleed-vulnerable")]
        [TestCase("unfunded")]
        [TestCase("unknown")]
        [TestCase("blocked")]
        public void BanditAmbushIsPartOfAFundedReachableFlankRatherThanAFlatBuffReward(string scenario)
        {
            var backstab = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Bandit_backstab.asset");
            var ambush = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Bandit_ambush.asset");
            var knife = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Bandit_knife.asset");
            var session = PlannerSession(2, backstab, ambush, knife); session.Grid = new BattleGrid(7, 6);
            var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Position = new GridPosition(3, 2); target.Position = new GridPosition(3, 3);
            target.ScoutState.Facing = new Vector2Int(1, 0);
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Momentum, scenario == "unfunded" ? 30 : 100, 200, 0);
            caster.Character.Stats.Speed = new Stat(20); caster.MaxActionPoints = caster.CurrentActionPoints = 2000;
            caster.Character.Stats.MinAttack = new Stat(200); caster.Character.Stats.MaxAttack = new Stat(200);
            caster.Character.Stats.CritDamage = new Stat(3);
            if (scenario == "unknown") session.Contact = new DungeonContactState((_, _) => false);
            if (scenario == "blocked") session.EnvironmentBlocksLineOfSight = (_, _) => true;
            if(scenario=="useful")target.Character.Resistances=new ResistanceSet{BleedResistance=1f};
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            ai.TryExecuteNextAction(session, caster, turn, out var action);
            if (scenario != "useful" && scenario != "bleed-vulnerable") { Assert.That(action.Skill, Is.Not.SameAs(ambush)); return; }
            Assert.That(action.Skill, Is.SameAs(ambush));
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var move), Is.True);
            if(scenario=="bleed-vulnerable")
            { Assert.That(move.Skill,Is.SameAs(knife),"Do not force a flank when knife damage plus bleeding is more valuable.");return; }
            Assert.That(move.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(ScoutCombatSystem.Behind(caster, target), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var hit), Is.True);
            Assert.That(hit.Skill, Is.SameAs(backstab));
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False);
        }

        [TestCase("kill")]
        [TestCase("ap")]
        [TestCase("blocked")]
        public void FlankingDoesNotDelayAnImmediateKillOrInventAnUnaffordableOrBlockedRearAttack(string scenario)
        {
            var backstab = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Bandit_backstab.asset");
            var session = PlannerSession(1, backstab); session.Grid = new BattleGrid(7, 6);
            var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Position = new GridPosition(3, 2); target.Position = new GridPosition(3, 3);
            target.ScoutState.Facing = new Vector2Int(0, -1);
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Momentum, 100, 200, 0);
            caster.Character.Stats.MinAttack = new Stat(100); caster.Character.Stats.MaxAttack = new Stat(100);
            caster.Character.Stats.WeaponAccuracy = new Stat(10000); caster.Character.WeaponAccuracy = 10000;
            if (scenario == "kill") target.Character.CurrentHealth = 1;
            if (scenario == "ap") caster.CurrentActionPoints = 750;
            if (scenario == "blocked") for (var x = 0; x < 7; x++) session.Grid.SetBlocked(new GridPosition(x, 4));
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(backstab));
            Assert.That(caster.Position, Is.EqualTo(new GridPosition(3, 2)));
        }

        [TestCase(2, true)]
        [TestCase(1, false)]
        public void ActiveStealthMayBridgeOneTurnForAValuableRearHitButExpiringStealthDoesNotDelayTheAttack(int duration, bool shouldFlank)
        {
            var attack = PlannerAttack("large-rear-opportunity", 70, 10);
            SetField(attack.Effects[0], "rearAttackDamageBonus", 3f);
            var session = PlannerSession(1, attack); session.Grid = new BattleGrid(7, 6);
            var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Position = new GridPosition(3, 2); target.Position = new GridPosition(3, 3);
            target.ScoutState.Facing = new Vector2Int(0, -1);
            var invisibility = new PassiveEffectRuntime { EffectId = "invisibility", Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = duration };
            caster.Character.PassiveEffects.Add(invisibility);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var first), Is.True);
            if (!shouldFlank) { Assert.That(first.Skill, Is.SameAs(attack)); return; }
            Assert.That(first.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(ScoutCombatSystem.Behind(caster, target), Is.True);
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.True);
            Assert.That(caster.CurrentActionPoints, Is.LessThan(CombatActionCostCalculator.CalculateSkillCost(caster, attack)));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out _), Is.False);
            caster.CurrentActionPoints = caster.MaxActionPoints; caster.MovementStepsThisTurn = 0;
            invisibility.RemainingDuration--;
            Assert.That(ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var hit), Is.True);
            Assert.That(hit.Skill, Is.SameAs(attack));
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False);
        }

        [Test]
        public void ChargePreviewStopsAtUncertainRealMovementResistanceWithoutTouchingTheTarget()
        {
            var session = PlannerSession(2, PlannerAttack("resisted-charge", 20, 1));
            var target = session.Units[1];
            target.Character.Resistances = new ResistanceSet { MoveResistance = .75f };
            var effect = CreateEffect(EffectType.Charge, 1);
            SetField(effect, "displacementDistance", 2); SetField(effect, "baseChance", 1f);
            var before = JsonUtility.ToJson(target.Character.Resistances);
            Assert.That(StatusEffectSystem.CalculateMovementEffectChance(target, 1f), Is.LessThan(1f));
            Assert.That(new CombatChargeSystem().TryPreviewCharge(session, session.ActiveUnit, target, effect,
                out var positions, out _, out var complete), Is.True);
            Assert.That(complete, Is.False);
            Assert.That(positions.ContainsKey(target), Is.False);
            Assert.That(JsonUtility.ToJson(target.Character.Resistances), Is.EqualTo(before));
        }

        [TestCase(.001f, false)]
        [TestCase(1f, true)]
        public void OffensivePreparationIsWorthActualDamageGainRatherThanFlatModifierPoints(float bonus, bool shouldPrepare)
        {
            var buff = PlannerBuff("measured-preparation", CharacterStatType.MinAttack, 0, 30);
            SetField(buff.Effects[0], "effectType", EffectType.ModifyVar);
            SetField(buff.Effects[0], "modifierVariableType", GameplayVariableType.DamageDealtPercent);
            SetField(buff.Effects[0], "modifierValue", bonus);
            var attack = PlannerAttack("attack", 60, 10f);
            var session = PlannerSession(1, buff, attack);
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(shouldPrepare ? buff : attack));
        }

        [TestCase("far")]
        [TestCase("wall")]
        [TestCase("unknown")]
        public void OffensivePreparationDoesNotSpendManaWithoutAnAvailableAttackOpportunity(string blocker)
        {
            var buff = PlannerBuff("unusable-preparation", CharacterStatType.MinAttack, 100, 30);
            SetAutoProperty(buff, "ResourceType", ResourceType.Mana); SetAutoProperty(buff, "ResourceCost", 50);
            var attack = PlannerAttack("attack", 60, 10f);
            var session = PlannerSession(blocker == "far" ? 7 : 1, buff, attack);
            if (blocker == "wall") session.EnvironmentBlocksLineOfSight = (_, _) => true;
            if (blocker == "unknown") session.Contact = new DungeonContactState((_, _) => false);
            var ai = CreateProductionWraithAi();
            ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action);
            Assert.That(action.Skill, Is.Not.SameAs(buff));
            Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
        }

        [Test]
        public void SpeedPreparationDoesNotSpendManaForAnUnreachableAttack()
        {
            var buff = PlannerBuff("blocked-speed-preparation", CharacterStatType.Speed, 100, 30);
            SetAutoProperty(buff, "ResourceType", ResourceType.Mana); SetAutoProperty(buff, "ResourceCost", 50);
            var session = PlannerSession(7, buff, PlannerAttack("attack", 60, 10f));
            session.Grid.SetBlocked(new GridPosition(3, 0));
            var ai = CreateProductionWraithAi();
            ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action);
            Assert.That(action.Skill, Is.Not.SameAs(buff));
            Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
        }

        [Test]
        public void PreparationDoesNotConsumeManaNeededForTheStrongerAffordableAttack()
        {
            var buff = PlannerBuff("mana-preparation", CharacterStatType.MinAttack, 10, 30);
            SetAutoProperty(buff, "ResourceType", ResourceType.Mana); SetAutoProperty(buff, "ResourceCost", 50);
            var strong = PlannerAttack("mana-finisher", 60, 10f);
            SetAutoProperty(strong, "ResourceType", ResourceType.Mana); SetAutoProperty(strong, "ResourceCost", 60);
            var plain = PlannerAttack("plain", 60, 2f);
            var session = PlannerSession(1, buff, strong, plain);
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(strong));
            Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(40));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ProductionArchersPrepareUsefulShotsButSkipPreparationForAnImmediateKill(bool forest, bool easyKill)
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(forest
                ? "Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_ElfArcher.asset"
                : "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_archer.asset");
            var preparation = definition.StartingSkills.First(skill => skill.Effects.Any(effect =>
                effect.EffectType is EffectType.ModifyVar or EffectType.ApplyUniqueBuff));
            var session = PlannerSession(5, definition.BasicAttackSkill, preparation);
            var caster = session.ActiveUnit;
            caster.Character.Definition = definition;
            caster.Character.ResourcePool = new ResourcePool(preparation.ResourceType, 100, 100, 0);
            caster.Character.Stats.MinAttack = new Stat(100); caster.Character.Stats.MaxAttack = new Stat(100);
            caster.Character.Stats.CritChance = new Stat(.2f); caster.Character.Stats.CritDamage = new Stat(3f);
            caster.Character.Stats.WeaponAccuracy = new Stat(10000); caster.Character.WeaponAccuracy = 10000;
            session.Units[1].Character.CurrentHealth = easyKill ? 1 : 3000;
            session.Units[1].Character.Stats.MaxHealth = new Stat(3000);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            TracePreparationEstimate(ai, session, preparation);
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var first), Is.True);
            Assert.That(first.Skill, Is.SameAs(easyKill ? definition.BasicAttackSkill : preparation));
            if (!easyKill)
            {
                Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var next), Is.True);
                Assert.That(next.Skill, Is.SameAs(definition.BasicAttackSkill));
                Assert.That(caster.CurrentActionPoints, Is.EqualTo(forest ? 100 : 150));
            }
        }

        private static void TracePreparationEstimate(SimpleCombatAiSystem ai, CombatSession session, SkillData skill)
        {
            var type = typeof(SimpleCombatAiSystem);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var nodeType = type.GetNestedType("AiTurnPlanNode", BindingFlags.NonPublic);
            var node = Activator.CreateInstance(nodeType, true);
            nodeType.GetField("World").SetValue(node, session);
            nodeType.GetField("OwnedUnits").SetValue(node, new HashSet<int> { 0 });
            var choiceType = type.GetNestedType("AiSkillSelection", BindingFlags.NonPublic);
            var choice = Activator.CreateInstance(choiceType, true);
            choiceType.GetField("Skill").SetValue(choice, skill);
            choiceType.GetField("TargetsByModeIndex").SetValue(choice, new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { session.ActiveUnit } });
            var projected = type.GetMethod("ProjectSkill", flags).Invoke(ai, new[] { node, choice, session, session.ActiveUnit });
            var evaluation = (IEnumerable<bool>)type.GetMethod("EvaluatePreparationWork", flags)
                .Invoke(ai, new[] { projected, session, session.ActiveUnit });
            foreach (var _ in evaluation) { }
            var world = (CombatSession)nodeType.GetField("World").GetValue(projected);
            TestContext.WriteLine($"{skill.SkillId}: score={nodeType.GetField("Score").GetValue(projected)}, future={nodeType.GetField("CarryoverValue").GetValue(projected)}, priority={nodeType.GetField("PreparationPriority").GetValue(projected)}, speed={world.ActiveUnit.Character.Stats.Speed.GetValue()}, AP={world.ActiveUnit.CurrentActionPoints}");
        }

        [TestCase(0f, 100f)]
        [TestCase(.5f, 150f)]
        [TestCase(1f, 200f)]
        public void ExpectedDamageUsesCriticalProbabilityWithoutRollingOrChangingTheUiRange(float critical, float expected)
        {
            var attack = PlannerAttack("critical-preview", 60, 10f);
            var session = PlannerSession(1, attack);
            var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Character.Stats.CritChance = new Stat(critical); caster.Character.Stats.CritDamage = new Stat(2f);
            var random = JsonUtility.ToJson(UnityEngine.Random.state);
            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, attack, 0);
            Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session, caster, target, attack, 0), Is.EqualTo(expected));
            Assert.That(preview.Damage.Minimum, Is.EqualTo(100));
            Assert.That(preview.Damage.Maximum, Is.EqualTo(100));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(random));
        }

        [Test]
        public void ExpectedCriticalDamageIsCalculatedBeforeNonlinearDefense()
        {
            var attack = PlannerAttack("critical-defense", 60, 10f);
            var session = PlannerSession(1, attack);
            session.ActiveUnit.Character.Stats.CritChance = new Stat(.5f);
            session.ActiveUnit.Character.Stats.CritDamage = new Stat(2f);
            session.Units[1].Character.Stats.PhysicalDefense = new Stat(200f);
            Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,
                session.ActiveUnit, session.Units[1], attack, 0), Is.EqualTo(67f));
        }

        [Test]
        public void CriticalPreviewDoesNotConsumeTheGuaranteedCriticalBuff()
        {
            var attack = PlannerAttack("guaranteed-preview", 60, 10f);
            var session = PlannerSession(1, attack);
            session.ActiveUnit.Character.Stats.CritDamage = new Stat(2f);
            var buff = new PassiveEffectRuntime { EffectId = "critical", IsUniqueCombatBuff = true,
                UniqueBuffType = UniqueBuffType.GuaranteedCriticalHit, Stacks = 1 };
            session.ActiveUnit.Character.PassiveEffects.Add(buff);
            var random = JsonUtility.ToJson(UnityEngine.Random.state);
            for (var query = 0; query < 10; query++)
                Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,
                    session.ActiveUnit, session.Units[1], attack, 0), Is.EqualTo(200f));
            Assert.That(buff.Stacks, Is.EqualTo(1));
            Assert.That(CombatDamageSystem.HasUniqueBuff(session.ActiveUnit, UniqueBuffType.GuaranteedCriticalHit), Is.True);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(random));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GroupPenetrationPreparationNeedsAnArmoredOpponentForItsAlly(bool armored)
        {
            var buff = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_battle_fervor.asset");
            var attack = PlannerAttack("armored-attack", 55, 1f);
            var session = PlannerSession(1, buff, attack);
            session.Grid = new BattleGrid(4, 3);
            var caster = session.ActiveUnit;
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Rage, 100, 100, 0);
            caster.Character.Stats.MinAttack = new Stat(1); caster.Character.Stats.MaxAttack = new Stat(1);
            var ally = CreateAiCombatUnit("armored-ally", false, new GridPosition(0, 1));
            ally.Character.Skills.Add(PlannerAttack("ally-attack", 55, 1f));
            ally.Character.Stats.MinAttack = new Stat(200); ally.Character.Stats.MaxAttack = new Stat(200);
            session.Units.Add(ally);
            session.Units[1].Character.CurrentHealth = 3000;
            session.Units[1].Character.Stats.PhysicalDefense = new Stat(armored ? 500 : 0);
            var ai = CreateProductionWraithAi();
            var acted = ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var action);
            if (armored)
            {
                Assert.That(acted, Is.True);
                Assert.That(action.Skill, Is.SameAs(buff));
            }
            else
            {
                Assert.That(action.Skill, Is.Not.SameAs(buff));
                Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
            }
        }

        [Test]
        public void GroupPreparationValuesTheAllyWhoCanActuallyUseTheBonus()
        {
            var buff = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_battle_fervor.asset");
            var attack = PlannerAttack("weak-caster", 55, 1f);
            var session = PlannerSession(1, buff, attack);
            session.Grid = new BattleGrid(4, 3);
            var caster = session.ActiveUnit;
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Rage, 100, 100, 0);
            caster.Character.Stats.MinAttack = new Stat(1); caster.Character.Stats.MaxAttack = new Stat(1);
            var ally = CreateAiCombatUnit("ally", false, new GridPosition(0, 1));
            ally.Character.Skills.Add(PlannerAttack("strong-ally", 55, 1f));
            ally.Character.Stats.MinAttack = new Stat(200); ally.Character.Stats.MaxAttack = new Stat(200);
            session.Units.Add(ally);
            session.Units[1].Character.CurrentHealth = 3000;
            session.Units[1].Character.Stats.PhysicalDefense = new Stat(500);
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, caster, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(buff));
            Assert.That(ally.Character.Stats.PhysicalDefensePenetration.GetValue(), Is.EqualTo(.5f));
        }

        [Test]
        public void SelfSpeedPreparationWithInitiativeDelayDoesNotMakeAiLoseItsAffordableAttack()
        {
            var buff = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_bone_fervor.asset"));
            createdAssets.Add(buff);
            SetField(buff.TargetModes[0], "targetType", TargetType.Caster);
            var attack = PlannerAttack("speed-attack", 55, 10f);
            var session = PlannerSession(1, buff, attack);
            var caster = session.ActiveUnit;
            caster.Character.ResourcePool = new ResourcePool(ResourceType.Rage, 100, 100, 0);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var first), Is.True);
            Assert.That(first.Skill, Is.SameAs(attack));
            Assert.That(ai.TryExecuteNextAction(session, caster, turn, out var next), Is.True);
            Assert.That(next.Skill, Is.SameAs(buff));
            Assert.That(caster.Character.Stats.Speed.GetValue(), Is.EqualTo(14));
            Assert.That(caster.CurrentActionPoints, Is.EqualTo(150));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TurnPlannerDoesNotCancelForUnrelatedRemotePatrolMovement(bool opponent)
        {
            var session = PlannerSession(2, PlannerAttack("attack", 60, 5f));
            var remote = CreateAiCombatUnit("remote", opponent, new GridPosition(100, 100));
            session.Units.Add(remote);
            session.Contact = new DungeonContactState((_, player) => player != remote);
            var ai = CreateProductionWraithAi();
            using var job = ai.CreateNextActionJob(session, session.ActiveUnit, ai.CreateTurnState());
            job.AdvanceSlice(maxSteps: 1);
            remote.Position = new GridPosition(101, 100);
            job.AdvanceSlice(maxSteps: 1);
            Assert.That(job.IsCanceled, Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(1000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TimedPreparationIsGenericForNewSkillIdsAndPlainOrChargingFinisher(bool charge)
        {
            SkillData preparation;
            SkillData attack;
            var setupStatus = charge ? StatusEffectIds.Invisibility : "test.readied";
            if (charge)
            {
                preparation = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(
                    "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_materialize.asset"));
                attack = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(
                    "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_fright.asset"));
                createdAssets.Add(preparation); createdAssets.Add(attack);
                SetAutoProperty(preparation, "SkillId", "future.apparition.reveal");
                SetAutoProperty(attack, "SkillId", "future.apparition.lunge");
            }
            else
            {
                preparation = PlannerBuff("release-preparation", CharacterStatType.MinAttack, .5f, 45);
                SetField(preparation.Effects[0], "effectType", EffectType.ModifyVar);
                SetField(preparation.Effects[0], "modifierVariableType", GameplayVariableType.DamageDealtPercent);
                SetField(preparation.Effects[0], "durationTurns", 1);
                var remove = CreateEffect(EffectType.RemoveStatus, 1);
                SetField(remove, "statusId", setupStatus);
                SetField(preparation, "effects", new List<SkillEffectData> { preparation.Effects[0], remove });
                SetAutoProperty(preparation, "RequiredCasterStatusId", setupStatus);
                attack = PlannerAttack("plain-finisher", 55, 1f);
                ConfigureTargetMode(attack, TargetingType.Enemies, 2);
            }
            var session = PlannerSession(4, preparation, attack);
            var unit = session.ActiveUnit;
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            { EffectId = setupStatus, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 3 });
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var approach), Is.True);
            Assert.That(approach.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(unit.Position.X, Is.EqualTo(charge ? 1 : 2));
            Assert.That(StatusEffectSystem.HasStatus(unit, setupStatus), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out _), Is.False);
            CombatTurnSystem.RefreshMaxActionPoints(session, unit, refillCurrentActionPoints: true);
            turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var reveal), Is.True);
            Assert.That(reveal.Skill, Is.SameAs(preparation));
            Assert.That(StatusEffectSystem.HasStatus(unit, setupStatus), Is.False);
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var strike), Is.True);
            Assert.That(strike.Skill, Is.SameAs(attack));
            Assert.That(unit.CurrentActionPoints, Is.Zero);
        }

        [Test]
        public void TurnPlannerReconsidersOtherSkillsAfterRealExecutionRejectsTheChoice()
        {
            var unavailable = PlannerAttack("missing-knife", 60, 20f);
            SetAutoProperty(unavailable, "KnifeCost", 1);
            var fallback = PlannerAttack("plain-attack", 60, 1f);
            var session = PlannerSession(1, unavailable, fallback);
            session.ActiveUnit.AvailableKnives = 0;
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(fallback));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(400));
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(990));
        }

        [Test]
        public void TurnPlannerPreservesExistingChoiceForUnsupportedMultifunctionPreparation()
        {
            var buff = PlannerBuff("complex-preparation", CharacterStatType.MinAttack, 0, 30);
            SetField(buff.Effects[0], "effectType", EffectType.ModifyVar);
            SetField(buff.Effects[0], "modifierVariableType", GameplayVariableType.DamageDealtPercent);
            SetField(buff.Effects[0], "modifierValue", .5f);
            SetField(buff.Effects[0], "durationTurns", 1);
            var extension = CreateEffect(EffectType.ExtendStatusDuration, 1);
            SetField(extension, "statusId", StatusEffectIds.Bleed);
            SetField(extension, "durationTurns", 1);
            SetField(buff, "effects", new List<SkillEffectData> { buff.Effects[0], extension });
            var session = PlannerSession(1, buff, PlannerAttack("attack", 60, 1f));
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(buff));
        }

        [Test]
        public void GenericPreparationDoesNotOverrideBossOwnerWithADifferentSequence()
        {
            var session = CreateProductionWraithSession(5);
            session.ActiveUnit.IsBoss = true;
            var preparation = session.ActiveUnit.Character.Skills.First(skill => skill.SkillId == "skill.enemy.catacombs.materialize");
            session.CanUseBossSkill = (_, skill) => skill == preparation;
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(preparation));
        }

        [Test]
        public void TurnPlannerLeavesBossPhaseChoicesWithTheirExistingOwner()
        {
            var small = PlannerAttack("small", 40, 1.2f);
            var heavy = PlannerAttack("heavy", 70, 1.8f);
            var session = PlannerSession(1, small, heavy);
            session.ActiveUnit.IsBoss = true;
            session.CanUseBossSkill = (_, skill) => skill == heavy;
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill, Is.SameAs(heavy));
        }

        [Test]
        public void TurnPlannerChoosesTwoAffordableAttacksOverGreedyHeavyAttack()
        {
            var small = PlannerAttack("small", 40, 1.2f);
            var heavy = PlannerAttack("heavy", 70, 1.8f);
            var session = PlannerSession(1, small, heavy);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var first), Is.True);
            Assert.That(first.Skill, Is.SameAs(small));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var second), Is.True);
            Assert.That(second.Skill, Is.SameAs(small));
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(976));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(200));
        }

        [TestCase(1000, true)]
        [TestCase(600, false)]
        public void TurnPlannerUsesOneTurnDamageBuffOnlyWithAnAffordableFinisher(int ap, bool shouldBuff)
        {
            var buff = PlannerBuff("damage-prep", CharacterStatType.MinAttack, 0, 30);
            var effect = buff.Effects[0];
            SetField(effect, "effectType", EffectType.ModifyVar);
            SetField(effect, "modifierVariableType", GameplayVariableType.DamageDealtPercent);
            SetField(effect, "modifierValue", 1f); SetField(effect, "durationTurns", 1);
            var attack = PlannerAttack("finisher", 60, 2f);
            var session = PlannerSession(1, buff, attack); session.ActiveUnit.CurrentActionPoints = ap;
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var first), Is.True);
            Assert.That(first.Skill, Is.SameAs(shouldBuff ? buff : attack));
            if (shouldBuff)
            {
                Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var second), Is.True);
                Assert.That(second.Skill, Is.SameAs(attack));
            }
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(shouldBuff ? 960 : 980));
        }

        [Test]
        public void TurnPlannerAttacksBeforeSpeedBuffThatWouldIncreaseFinisherCost()
        {
            var buff = PlannerBuff("speed-prep", CharacterStatType.Speed, 10, 30);
            var attack = PlannerAttack("finisher", 60, 2f);
            var session = PlannerSession(1, buff, attack);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var first), Is.True);
            Assert.That(first.Skill, Is.SameAs(attack));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var second), Is.True);
            Assert.That(second.Skill, Is.SameAs(buff));
            Assert.That(session.ActiveUnit.MaxActionPoints, Is.EqualTo(2000));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(100));
        }

        [TestCase(0, 700)]
        [TestCase(1, 800)]
        [TestCase(2, 900)]
        public void TurnPlannerReservesActualIncreasingMovementCostForAttack(int movementSteps, int ap)
        {
            var buff = PlannerBuff("speed-prep", CharacterStatType.Speed, 10, 35);
            var attack = PlannerAttack("finisher", 60, 10f);
            var session = PlannerSession(2, buff, attack);
            session.ActiveUnit.MovementStepsThisTurn = movementSteps;
            session.ActiveUnit.CurrentActionPoints = ap;
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var first), Is.True);
            Assert.That(first.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(600));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var second), Is.True);
            Assert.That(second.Skill, Is.SameAs(attack));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.Zero);
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(900));
        }

        [Test]
        public void TurnPlannerPaysForDoorThenMovementThenAttack()
        {
            var buff = PlannerBuff("speed-prep", CharacterStatType.Speed, 10, 35);
            var attack = PlannerAttack("finisher", 60, 10f);
            var session = PlannerSession(2, buff, attack);
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var first), Is.True);
            Assert.That(first.Type, Is.EqualTo(SimpleCombatAiActionType.Interaction));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(800));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var second), Is.True);
            Assert.That(second.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(700));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out var third), Is.True);
            Assert.That(third.Skill, Is.SameAs(attack));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(100));
        }

        [Test]
        public void TurnPlannerDoesNotSpendTheSameManaTwice()
        {
            var costly = PlannerAttack("mana-strike", 40, 7f);
            SetAutoProperty(costly, "ResourceType", ResourceType.Mana); SetAutoProperty(costly, "ResourceCost", 60);
            var free = PlannerAttack("plain-strike", 60, 5f);
            var session = PlannerSession(1, costly, free);
            var ai = CreateProductionWraithAi(); var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out _), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, turn, out _), Is.True);
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(880));
            Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(40));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.Zero);
        }

        [TestCase("health")]
        [TestCase("position")]
        [TestCase("death")]
        [TestCase("mana")]
        [TestCase("status")]
        [TestCase("door")]
        [TestCase("variable")]
        [TestCase("unique-buff")]
        [TestCase("resistance")]
        [TestCase("geometry")]
        [TestCase("facing")]
        public void TurnPlannerInvalidatesYieldedDecisionWhenCombatStateChanges(string change)
        {
            var session = PlannerSession(2, PlannerAttack("attack", 60, 5f));
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            var variable = new GameplayVariableModifier { VariableType = GameplayVariableType.DamageDealtPercent, Value = 0f };
            if (change == "variable") session.ActiveUnit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            { EffectId = "variable-buff", VariableModifiers = new List<GameplayVariableModifier> { variable } });
            var ai = CreateProductionWraithAi();
            using var job = ai.CreateNextActionJob(session, session.ActiveUnit, ai.CreateTurnState());
            job.AdvanceSlice(maxSteps: 1);
            Assert.That(job.IsComplete, Is.False);
            switch (change)
            {
                case "health": session.Units[1].Character.CurrentHealth--; break;
                case "position": session.Units[1].Position = new GridPosition(3, 0); break;
                case "death": session.Units[1].Character.LifeState = CharacterLifeState.Dead; break;
                case "mana": session.ActiveUnit.Character.ResourcePool.CurrentValue--; break;
                case "status": session.ActiveUnit.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = "test-status" }); break;
                case "door": door.IsOpen = true; break;
                case "variable": variable.Value = .5f; break;
                case "unique-buff": session.ActiveUnit.UniqueBuffs.Add(UniqueBuffType.Hawkeye); break;
                case "resistance": session.Units[1].Character.Resistances = new ResistanceSet { BleedResistance = 1f }; break;
                case "geometry": session.Grid.SetBlocked(new GridPosition(1, 0)); break;
                case "facing": session.Units[1].ScoutState.Facing = new Vector2Int(-1, 0); break;
            }
            var rng = JsonUtility.ToJson(UnityEngine.Random.state);
            job.AdvanceSlice();
            Assert.That(job.IsCanceled, Is.True);
            Assert.That(job.Executed, Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(1000));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(rng));
        }

        [Test]
        public void TurnPlannerQueryDoesNotMutateLiveStatsEffectsDoorsHealthResourcesOrRandom()
        {
            var buff = PlannerBuff("speed-prep", CharacterStatType.Speed, 10, 35);
            var session = PlannerSession(2, buff, PlannerAttack("attack", 60, 10f));
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            var ai = CreateProductionWraithAi();
            var type = typeof(SimpleCombatAiSystem);
            var selectionType = type.GetNestedType("AiSkillSelection", BindingFlags.NonPublic);
            var stepType = type.GetNestedType("AiTurnPlanStep", BindingFlags.NonPublic);
            var resultType = type.GetNestedType("AiResult`1", BindingFlags.NonPublic).MakeGenericType(stepType);
            var candidates = Activator.CreateInstance(typeof(List<>).MakeGenericType(selectionType));
            var greedy = type.GetMethod("FindBestSkillSelection", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ai, new object[] { session, session.ActiveUnit, new HashSet<SkillData>() });
            var work = (IEnumerable<bool>)type.GetMethod("FindTurnPlanWork", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(ai, new[] { session, session.ActiveUnit, ai.CreateTurnState(), greedy, candidates, Activator.CreateInstance(resultType) });
            var rng = JsonUtility.ToJson(UnityEngine.Random.state);
            var steps = 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            foreach (var _ in work) Assert.That(++steps, Is.LessThan(20000));
            timer.Stop();
            TestContext.WriteLine($"Bounded pure planner: {steps} checkpoints, {timer.Elapsed.TotalMilliseconds:F2} ms total (runtime uses frame slices).");
            Assert.That(steps, Is.GreaterThan(10));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(1000));
            Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
            Assert.That(session.ActiveUnit.Character.Stats.Speed.GetValue(), Is.EqualTo(10));
            Assert.That(session.ActiveUnit.Character.PassiveEffects, Is.Empty);
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(1000));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(session.Grid.HasBlockedEdge(door.Edge.FirstCell, door.Edge.SecondCell), Is.True);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(rng));
        }

        private SkillData PlannerAttack(string id, int cost, float multiplier)
        {
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, multiplier));
            SetAutoProperty(skill, "SkillId", "test.planner." + id);
            SetAutoProperty(skill, "SkillAccuracy", 100f); SetAutoProperty(skill, "ActionCost", cost);
            SetAutoProperty(skill, "RequiresLineOfSight", true);
            SetField(skill.Effects[0], "alwaysHits", true);
            ConfigureTargetMode(skill, TargetingType.Enemies, 1);
            return skill;
        }

        private SkillData PlannerBuff(string id, CharacterStatType type, float value, int cost)
        {
            var skill = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(skill, "SkillId", "test.planner." + id); SetAutoProperty(skill, "ActionCost", cost);
            SetAutoProperty(skill, "SkillAccuracy", 100f);
            SetField(skill.TargetModes[0], "targetType", TargetType.Caster);
            SetField(skill.TargetModes[0], "targetingType", TargetingType.Allies);
            SetField(skill.Effects[0], "alwaysHits", true);
            SetField(skill.Effects[0], "modifierStatType", type); SetField(skill.Effects[0], "modifierValue", value);
            SetField(skill.Effects[0], "durationTurns", 2);
            return skill;
        }

        private CombatSession PlannerSession(int distance, params SkillData[] skills)
        {
            var session = CreateNavigationSession(distance + 2, 1, new GridPosition(0, 0), new GridPosition(distance, 0));
            session.ActionPointsPerSpeed = 1f;
            session.ActiveUnit.Character.Skills.AddRange(skills);
            session.ActiveUnit.Character.ResourcePool = new ResourcePool(ResourceType.Mana, 100, 100, 0);
            session.ActiveUnit.Character.Stats.MaxResource = new Stat(100);
            session.Units[1].Character.CurrentHealth = 1000;
            session.Units[1].Character.Stats.MaxHealth = new Stat(1000);
            return session;
        }

        [Test]
        public void ProductionUndeadBitesBeforeScentAndRechecksActualBleedingAfterHitOrMiss()
        {
            var previousRandom = UnityEngine.Random.state;
            var succeeded = 0;
            var missed = 0;
            var resisted = 0;
            try
            {
                for (var seed = 0; seed < 32; seed++)
                {
                    var session = CreateProductionUndeadSession();
                    var unit = session.ActiveUnit;
                    var target = session.Units[1];
                    var ai = CreateProductionWraithAi(); // Full status/execution wiring shared by both production kits.
                    var turn = ai.CreateTurnState();
                    UnityEngine.Random.InitState(seed);
                    Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var bite), Is.True, $"seed {seed}");
                    TestContext.WriteLine($"Undead first: {bite.Skill?.SkillId}, HP={target.Character.CurrentHealth}, AP={unit.CurrentActionPoints}");
                    Assert.That(bite.Skill.SkillId, Does.EndWith(".manic_bite"), $"seed {seed}");
                    var bleeding = StatusEffectSystem.HasStatus(target, StatusEffectIds.Bleed);
                    var acted = ai.TryExecuteNextAction(session, unit, turn, out var followUp);
                    if (bleeding)
                    {
                        succeeded++;
                        Assert.That(acted, Is.True, $"seed {seed}");
                        Assert.That(followUp.Skill.SkillId, Does.EndWith(".blood_scent"));
                        Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(26));
                        Assert.That(unit.Character.PassiveEffects.Any(e => e.EffectId.EndsWith(".modify_stat.1")), Is.False);
                        Assert.That(unit.CurrentActionPoints, Is.Zero);
                    }
                    else
                    {
                        if (target.Character.CurrentHealth == 1000) missed++; else resisted++;
                        Assert.That(acted, Is.False, $"Do not spend the remaining AP on weak scent: seed {seed}");
                        Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(10));
                    }
                }
                Assert.That(succeeded, Is.GreaterThan(0));
                Assert.That(missed, Is.GreaterThan(0));
                Assert.That(resisted, Is.GreaterThan(0));
                TestContext.WriteLine($"32 fixed seeds: {succeeded} bleeds, {missed} misses, {resisted} hits without bleed.");
            }
            finally { UnityEngine.Random.state = previousRandom; }
        }

        [Test]
        public void ProductionUndeadGeneratesRageBeforePayingForConditionalBuff()
        {
            var session = CreateProductionUndeadSession(rage: 0);
            var unit = session.ActiveUnit;
            var target = session.Units[1];
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime
            { EffectId = StatusEffectIds.Bleed, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            var ai = CreateProductionWraithAi();
            var state = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var attack), Is.True);
            Assert.That(attack.Skill.SkillId, Does.EndWith(".manic_bite"));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var buff), Is.True);
            Assert.That(buff.Skill.SkillId, Does.EndWith(".blood_scent"));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.CurrentActionPoints, Is.Zero);
        }

        [TestCase("range")]
        [TestCase("wall")]
        [TestCase("unobserved")]
        [TestCase("hidden")]
        public void ProductionUndeadDoesNotSpendWeakScentBeforeAValidBleedingOpportunity(string reason)
        {
            var session = CreateProductionUndeadSession(reason == "range" ? 4 : 1);
            var unit = session.ActiveUnit;
            if (reason == "wall") session.EnvironmentBlocksLineOfSight = (_, _) => true;
            if (reason == "unobserved") session.Contact = new DungeonContactState((_, _) => false);
            if (reason == "hidden") session.Units[1].Character.PassiveEffects.Add(new PassiveEffectRuntime
            { EffectId = StatusEffectIds.Invisibility, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            var selected = FindChosenSkill(CreateProductionWraithAi(), session);
            Assert.That(selected, Is.Null);
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(1000));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [Test]
        public void ProductionUndeadDoesNotSpendWeakScentWhenApproachAndAttackAreBetter()
        {
            var session = CreateProductionUndeadSession(4);
            session.Units[1].Character.Resistances = new ResistanceSet { BleedResistance = 1f };
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            Assert.That(session.ActiveUnit.Character.PassiveEffects.Any(effect => effect.EffectId.Contains("blood_scent")), Is.False);
        }

        [Test]
        public void ProductionUndeadKeepsWeakFallbackSelectableWhenBleedingCannotBeApplied()
        {
            var session = CreateProductionUndeadSession(4);
            session.Units[1].Character.Resistances = new ResistanceSet { BleedResistance = 1f };
            var ai = CreateProductionWraithAi();
            // Immunity must not ban the weak fallback. Whether it beats
            // approaching/attacking is a separate opportunity comparison.
            Assert.That(FindChosenSkill(ai, session).SkillId, Does.EndWith(".blood_scent"));
            Assert.That(session.ActiveUnit.Character.Stats.Speed.GetValue(), Is.EqualTo(10));
        }

        [Test]
        public void ProductionUndeadDoesNotRefreshActiveStrongScent()
        {
            var session = CreateProductionUndeadSession(4);
            var unit = session.ActiveUnit;
            session.Units[1].Character.PassiveEffects.Add(new PassiveEffectRuntime
            { EffectId = StatusEffectIds.Bleed, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            var ai = CreateProductionWraithAi();
            var scent = unit.Character.Skills.First(skill => skill.SkillId.EndsWith(".blood_scent"));
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, unit, scent, unit), Is.True);
            CombatTurnSystem.RefreshMaxActionPoints(session, unit, refillCurrentActionPoints: true);
            Assert.That(FindChosenSkill(ai, session), Is.Null);
        }

        [TestCase(650)]
        [TestCase(999)]
        public void ProductionUndeadDoesNotInventApForBiteScentSequence(int ap)
        {
            var session = CreateProductionUndeadSession(rage: 0);
            session.ActiveUnit.CurrentActionPoints = ap;
            var ai = CreateProductionWraithAi();
            var state = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, state, out var attack), Is.True);
            Assert.That(attack.Skill.SkillId, Does.EndWith(".manic_bite"));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, state, out _), Is.False);
        }

        [TestCase(false,6f,true)]
        [TestCase(true,6f,false)]
        [TestCase(false,14f,false)]
        public void ConditionalPreparationUsesAuthoredPoisonConditionAndUnrelatedSkillIds(bool blockFollowUp,float decoyMultiplier,bool expectsCombo)
        {
            var session = CreateProductionUndeadSession();
            var unit = session.ActiveUnit;
            var original = unit.Character.Skills.ToArray();
            var attack = UnityEngine.Object.Instantiate(original.Single(skill => skill.SkillId.EndsWith(".manic_bite")));
            var buff = UnityEngine.Object.Instantiate(original.Single(skill => skill.SkillId.EndsWith(".blood_scent")));
            createdAssets.Add(attack); createdAssets.Add(buff);
            SetAutoProperty(attack, "SkillId", "future.sting");
            SetAutoProperty(attack, "SkillAccuracy", 100f);
            foreach (var effect in attack.Effects) SetField(effect, "alwaysHits", true);
            var poison = attack.Effects.Single(effect => effect.EffectType == EffectType.ApplyStatus);
            SetField(poison, "statusId", StatusEffectIds.Poison);
            SetField(poison, "baseChance", 1f);
            SetAutoProperty(buff, "SkillId", "future.focus");
            foreach (var effect in buff.Effects) SetField(effect.Conditions[0], "statusId", StatusEffectIds.Poison);
            SetField(buff.Effects[0], "modifierValue", 9f);
            SetField(buff.Effects[1], "modifierValue", 3f);
            var decoy = CreateSkill(CreateEffect(EffectType.DealDamage, decoyMultiplier));
            SetAutoProperty(decoy, "SkillId", "future.heavy_strike");
            SetAutoProperty(decoy, "ActionCost", 60);
            ConfigureTargetMode(decoy, TargetingType.Enemies, 1);
            unit.Character.Skills.Clear();
            unit.Character.Skills.AddRange(new[] { attack, buff, decoy });
            if (blockFollowUp) session.CanUseBossSkill = (_, skill) => skill != buff;
            var ai = CreateProductionWraithAi();
            var state = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var opening), Is.True);
            if (!expectsCombo)
            {
                Assert.That(opening.Skill, Is.SameAs(decoy), "A forbidden or less valuable combo must not beat a useful immediate attack.");
                return;
            }
            Assert.That(opening.Skill, Is.SameAs(attack));
            Assert.That(StatusEffectSystem.HasStatus(session.Units[1], StatusEffectIds.Poison), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var followUp), Is.True);
            Assert.That(followUp.Skill, Is.SameAs(buff));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(19));
            Assert.That(original.Single(skill => skill.SkillId.EndsWith(".manic_bite")).Effects[1].StatusId,
                Is.EqualTo(StatusEffectIds.Bleed), "Runtime test clones must not modify production assets.");
        }

        [Test]
        public void ConditionalPreparationForecastDoesNotSpendRageOrAdvanceRandom()
        {
            var session = CreateProductionUndeadSession(rage: 0);
            var unit = session.ActiveUnit;
            var rng = JsonUtility.ToJson(UnityEngine.Random.state);
            Assert.That(FindChosenSkill(CreateProductionWraithAi(), session).SkillId, Does.EndWith(".manic_bite"));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(1000));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.Character.ResourcePool.HasGainedResourceSinceLastTurn, Is.False);
            Assert.That(session.Units[1].Character.CurrentHealth, Is.EqualTo(1000));
            Assert.That(unit.Character.PassiveEffects, Is.Empty);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(rng));
        }

        [Test]
        public void ProductionUndeadDoesNotAssumeBlockedRageGainCanPayForBuff()
        {
            var session = CreateProductionUndeadSession(rage: 0);
            var unit = session.ActiveUnit;
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "test.block-resource", Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2,
                Payloads = new List<PassiveEffectPayloadRuntime> { new ResourceRegenerationBlockPayload() }
            });
            session.Units[1].Character.PassiveEffects.Add(new PassiveEffectRuntime
            { EffectId = StatusEffectIds.Bleed, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            var ai = CreateProductionWraithAi();
            var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var attack), Is.True);
            Assert.That(attack.Skill.SkillId, Does.EndWith(".manic_bite"));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out _), Is.False);
        }

        private SkillData FindChosenSkill(SimpleCombatAiSystem ai, CombatSession session)
        {
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection", BindingFlags.Instance | BindingFlags.NonPublic);
            var selection = find.Invoke(ai, new object[] { session, session.ActiveUnit, new HashSet<SkillData>() });
            return selection == null ? null : (SkillData)selection.GetType().GetField("Skill").GetValue(selection);
        }

        private CombatSession CreateProductionUndeadSession(int distance = 1, int rage = 50)
        {
            var unit = CreateAiCombatUnit("undead", false, new GridPosition(0, 0));
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_undead.asset");
            Assert.That(definition, Is.Not.Null);
            unit.Character.Definition = definition;
            unit.Character.ClassData = definition.ClassData;
            unit.Character.Skills.AddRange(definition.StartingSkills);
            unit.Character.ResourcePool = new ResourcePool(ResourceType.Rage, rage, 50, 0);
            var target = CreateAiCombatUnit("hero", true, new GridPosition(distance, 0));
            target.Character.CurrentHealth = 1000;
            target.Character.Stats.MaxHealth = new Stat(1000);
            var session = CreateAiSession(unit, target);
            session.ActionPointsPerSpeed = 1f;
            session.Grid = new BattleGrid(distance + 1, 1);
            return session;
        }

        [Test]
        public void ProductionWraithApproachesInvisibleAndWaitsForFullComboBudget()
        {
            var session = CreateProductionWraithSession(6, invisibleTurns: 0);
            var unit = session.ActiveUnit;
            var target = session.Units[1];
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var setup), Is.True);
            Assert.That(setup.Skill.SkillId, Does.EndWith(".dematerialize"));
            Assert.That(StatusEffectSystem.IsInvisible(unit), Is.True);
            Assert.That(unit.CurrentActionPoints, Is.Zero);

            new StatusEffectSystem().ProcessTurnEnd(unit);
            unit.CurrentActionPoints = unit.MaxActionPoints;
            var turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var approach), Is.True);
            Assert.That(approach.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(Math.Abs(target.Position.X - unit.Position.X), Is.EqualTo(3));
            var remainingAp = unit.CurrentActionPoints;
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out _), Is.False);
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(remainingAp));
            Assert.That(StatusEffectSystem.IsInvisible(unit), Is.True);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(1000));

            new StatusEffectSystem().ProcessTurnEnd(unit);
            unit.CurrentActionPoints = unit.MaxActionPoints;
            unit.MovementStepsThisTurn = 0;
            turn = ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var reveal), Is.True);
            Assert.That(reveal.Skill.SkillId, Does.EndWith(".materialize"));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(550));
            Assert.That(StatusEffectSystem.IsInvisible(unit), Is.False);
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var strike), Is.True);
            Assert.That(strike.Skill.SkillId, Does.EndWith(".fright"));
            Assert.That(target.Character.CurrentHealth, Is.LessThan(1000));
            Assert.That(unit.CurrentActionPoints, Is.Zero);
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out _), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ProductionWraithRevealsThenStrikesWithoutSpendingApOnMovement(int distance)
        {
            var session = CreateProductionWraithSession(distance);
            var unit = session.ActiveUnit;
            var ai = CreateProductionWraithAi();
            var turn = ai.CreateTurnState();
            var start = unit.Position;
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var reveal), Is.True);
            Assert.That(reveal.Skill.SkillId, Does.EndWith(".materialize"));
            Assert.That(unit.Position, Is.EqualTo(start));
            Assert.That(ai.TryExecuteNextAction(session, unit, turn, out var strike), Is.True);
            Assert.That(strike.Skill.SkillId, Does.EndWith(".fright"));
            Assert.That(unit.CurrentActionPoints, Is.Zero);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(75));
        }

        [TestCase(3, false)]
        [TestCase(1, true)]
        public void ProductionWraithPreservesApproachButCanStrikeBeforeInvisibilityExpires(int remainingTurns, bool attacks)
        {
            var session = CreateProductionWraithSession(3, invisibleTurns: remainingTurns);
            var unit = session.ActiveUnit;
            unit.CurrentActionPoints = 550;
            var ai = CreateProductionWraithAi();
            var executed = ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action);
            Assert.That(executed, Is.EqualTo(attacks));
            if (attacks) Assert.That(action.Skill.SkillId, Does.EndWith(".fright"));
            else
            {
                Assert.That(StatusEffectSystem.IsInvisible(unit), Is.True);
                Assert.That(unit.CurrentActionPoints, Is.EqualTo(550));
            }
        }

        [TestCase(5)]
        [TestCase(20)]
        [TestCase(24)]
        public void ProductionWraithDoesNotRevealOffensivelyWithoutManaForBothSkills(int mana)
        {
            var session = CreateProductionWraithSession(3, mana);
            var unit = session.ActiveUnit;
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill.SkillId, Does.EndWith(".fright"));
        }

        [Test]
        public void ProductionWraithCanMaterializeForEmergencyHealingOutsideAttackRange()
        {
            var session = CreateProductionWraithSession(8);
            var unit = session.ActiveUnit;
            unit.Character.CurrentHealth = 10;
            var ai = CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill.SkillId, Does.EndWith(".materialize"));
            Assert.That(unit.Character.CurrentHealth, Is.GreaterThan(10));
            Assert.That(StatusEffectSystem.IsInvisible(unit), Is.False);
        }

        [Test]
        public void ProductionWraithDoesNotReserveStrikeForDisallowedMaterialization()
        {
            var session = CreateProductionWraithSession(3);
            session.CanUseBossSkill = (_, skill) => !skill.SkillId.EndsWith(".materialize", StringComparison.Ordinal);
            Assert.That(CreateProductionWraithAi().TryExecuteNextAction(session, session.ActiveUnit,
                new SimpleCombatAiTurnState(), out var action), Is.True);
            Assert.That(action.Skill.SkillId, Does.EndWith(".fright"));
        }

        private static SimpleCombatAiSystem CreateProductionWraithAi() =>
            new(skillExecutionSystem: new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()));

        [TestCase("ally")]
        [TestCase("unobserved")]
        [TestCase("wall")]
        [TestCase("immobilized")]
        public void ProductionWraithDoesNotRevealForBlockedOrUnobservedStrike(string blocker)
        {
            var session = CreateProductionWraithSession(3);
            var unit = session.ActiveUnit;
            if (blocker == "unobserved") session.Contact = new DungeonContactState((_, _) => false);
            else if (blocker == "wall") session.EnvironmentBlocksLineOfSight = (_, _) => true;
            else if (blocker == "immobilized") unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Immobilize, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2
            });
            else
            {
                var ally = CreateAiCombatUnit("blocker", false, new GridPosition(1, 1));
                session.Units.Add(ally);
            }
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection", BindingFlags.Instance | BindingFlags.NonPublic);
            var selected = find.Invoke(CreateExecutableAiSystem(), new object[] { session, unit, new HashSet<SkillData>() });
            Assert.That(selected, Is.Null);
            Assert.That(StatusEffectSystem.IsInvisible(unit), Is.True);
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(1000));
        }

        private CombatSession CreateProductionWraithSession(int targetX, int mana = 100, int invisibleTurns = 3)
        {
            var unit = CreateAiCombatUnit("wraith", false, new GridPosition(0, 1));
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_wraith.asset");
            Assert.That(definition, Is.Not.Null);
            unit.Character.Definition = definition;
            unit.Character.Skills.AddRange(definition.StartingSkills);
            unit.Character.ResourcePool = new ResourcePool(ResourceType.Mana, mana, 100, 15);
            if (invisibleTurns > 0) unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Invisibility, Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = invisibleTurns
            });
            var target = CreateAiCombatUnit("target", true, new GridPosition(targetX, 1));
            target.Character.CurrentHealth = 1000;
            target.Character.Stats.MaxHealth = new Stat(1000);
            var session = CreateAiSession(unit, target);
            session.ActionPointsPerSpeed = 1f; // Speed 10 must retain the fixture's 1000-unit AP budget after casts.
            session.Grid = new BattleGrid(targetX + 2, 3);
            return session;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LiveNavigationReportDoesNotAdvanceSearchOrExecuteActions(bool hasContact)
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            var door = AddNavigationDoor(session, 1, DoorInteractionMode.DirectOnly);
            if (hasContact)
            {
                session.Contact = new DungeonContactState((_, _) => false);
                session.Contact.ReceiveAlert(session.ActiveUnit, session.Units[1], session.ActiveUnit.Position);
            }
            var beforePosition = session.ActiveUnit.Position;
            var beforeAp = session.ActiveUnit.CurrentActionPoints;
            var report = DungeonLiveFrameCapture.BuildCombatNavigationReport(session);
            Assert.That(report, Does.Contain("door=door"));
            if (hasContact) Assert.That(report, Does.Contain("checked=False"));
            Assert.That(DungeonLiveFrameCapture.BuildCombatNavigationReport(session), Is.EqualTo(report));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(beforePosition));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(beforeAp));
            Assert.That(door.IsOpen, Is.False);
        }

        [TestCase("Passages",0,false)] [TestCase("Passages",90,false)] [TestCase("Passages",180,false)] [TestCase("Passages",270,false)]
        [TestCase("Corridor",0,false)] [TestCase("Corridor",90,false)] [TestCase("Corridor",180,false)] [TestCase("Corridor",270,false)]
        [TestCase("Passages",0,true)] [TestCase("Passages",90,true)] [TestCase("Passages",180,true)] [TestCase("Passages",270,true)]
        [TestCase("Corridor",0,true)] [TestCase("Corridor",90,true)] [TestCase("Corridor",180,true)] [TestCase("Corridor",270,true)]
        public void ProductionInternalRoomDoorsAllowCombatPursuitFromBothSides(string roomName,int rotation,bool distant)
        {
            var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Expedition.asset").RoomTemplateCatalog;
            var template=UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonRoomTemplateDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRT_Catacombs_Tactical_"+roomName+"_Production.asset");
            var doorDefinition=UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Door_Debug.asset");
            var encounterDefinition=ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();createdAssets.Add(encounterDefinition);
            foreach(var anchor in template.StructuralAnchors.Where(a=>a.Type==DungeonStructuralAnchorType.Door))
            foreach(var reverse in new[]{false,true})
            {
                var floor=new DungeonFloorInstance(0);
                floor.TryAddRoom(new DungeonRoomInstance("room",template.RoomType,template.Id,rotationDegrees:rotation));
                var map=new Godmist.Systems.Dungeons.DungeonFloorMapBuilder().Build(floor,catalog);floor.TrySetMap(map);
                foreach(var authored in template.StructuralAnchors.Where(a=>a.Type==DungeonStructuralAnchorType.Door))
                {
                    var cell=Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformCell(authored.LocalCell,template.SizeInCells,rotation);
                    var offset=Godmist.Data.Dungeons.DungeonRoomTransformUtility.GetDirectionOffset(
                        Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformDirection(authored.Direction,rotation));
                    Assert.That(floor.TryAddObject(new DoorDungeonObjectInstance("door."+authored.LocalCell,doorDefinition,new DungeonCellEdge(cell,cell+offset))),Is.True);
                }
                var first=Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformCell(anchor.LocalCell,template.SizeInCells,rotation);
                var delta=Godmist.Data.Dungeons.DungeonRoomTransformUtility.GetDirectionOffset(
                    Godmist.Data.Dungeons.DungeonRoomTransformUtility.TransformDirection(anchor.Direction,rotation));
                var second=first+delta;
                var start=reverse?second:first;var beyond=reverse?first:second;
                var targetCell=map.FloorCells.First(c=>c!=start && c!=beyond && map.CanMoveBetween(beyond,c,ignoreClosedDoorEdges:true));
                if(distant)
                {
                    start=FarthestReachable(start);
                    targetCell=FarthestReachable(beyond);
                }
                Vector2Int FarthestReachable(Vector2Int origin)
                {
                    var pending=new Queue<Vector2Int>();var seen=new HashSet<Vector2Int>{origin};
                    pending.Enqueue(origin);var last=origin;
                    while(pending.Count>0)
                    {
                        last=pending.Dequeue();
                        foreach(var delta in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                        {
                            var next=last+delta;
                            if(map.CanMoveBetween(last,next)&&seen.Add(next))pending.Enqueue(next);
                        }
                    }
                    return last;
                }
                var dungeon=new DungeonInstance("room-doors",1);dungeon.TryAddFloor(floor);
                var encounter=new DungeonEncounterInstance("encounter",encounterDefinition,"room",new RectInt(Vector2Int.zero,
                    Godmist.Data.Dungeons.DungeonRoomTransformUtility.GetRotatedSize(template.SizeInCells,rotation)));
                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon,encounter,out var context),Is.True);
                Assert.That(new DungeonBattleGridFactory().TryCreate(context,out var grid),Is.True);
                var session=CreateNavigationSession(grid.Width,grid.Height,DungeonBattleGridFactory.ToBattlePosition(context,start),
                    DungeonBattleGridFactory.ToBattlePosition(context,targetCell));
                session.Grid=grid;session.DungeonContext=context;
                foreach(var source in floor.Objects.OfType<DoorDungeonObjectInstance>())
                    session.MapObjects.Add(new DoorCombatMapObject(source.InstanceId,DungeonBattleGridFactory.ToBattlePosition(context,source.Edge.FirstCell),
                        DungeonBattleGridFactory.ToBattlePosition(context,source.Edge.SecondCell),source.InteractionMode,source.IsOpen));
                var enemy=session.Units[0];var player=session.Units[1];
                var interactions=new CombatInteractionSystem(onObjectChanged:value=>floor.TrySetDoorOpen(value.ObjectId,((DoorCombatMapObject)value).IsOpen));
                interactions.InitializeMapObjects(session);
                var visibility=new Godmist.Systems.Dungeons.DungeonVisibilitySystem();
                session.Contact=new DungeonContactState((e,p)=>visibility.CanObserveCombat(floor,
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(context,e.Position),10f,
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(context,p.Position)));
                session.Contact.ReceiveAlert(enemy,player,player.Position);session.Contact.Refresh(session);
                var ai=new SimpleCombatAiSystem(interactionSystem:interactions);var actions=0;
                for(var turn=0;turn<16;turn++)
                {
                    enemy.CurrentActionPoints=enemy.MaxActionPoints;enemy.MovementStepsThisTurn=0;
                    session.Contact.BeginTurn(session,enemy);var state=ai.CreateTurnState();
                    while(ai.TryExecuteNextAction(session,enemy,state,out var action))
                    {
                        TestContext.WriteLine($"{roomName}/{rotation}/{anchor.LocalCell}/{reverse}: {action.Type}, ({enemy.Position.X},{enemy.Position.Y}), AP {enemy.CurrentActionPoints}");
                        Assert.That(++actions,Is.LessThan(100),"Door interaction must not repeatedly toggle or replan forever.");
                    }
                    session.Contact.EndTurn(session,enemy);
                    if(ai.IsAdjacent(enemy.Position,player.Position)&&session.CanMoveBetween(enemy,enemy.Position,player.Position))break;
                }
                Assert.That(Math.Abs(enemy.Position.X-player.Position.X)+Math.Abs(enemy.Position.Y-player.Position.Y),Is.EqualTo(1),
                    $"{roomName}/{rotation}/{anchor.LocalCell}/{reverse}: enemy cannot reach the player across a real room door.");
                Assert.That(session.CanMoveBetween(enemy,enemy.Position,player.Position),Is.True);
            }
        }

        [Test]
        public void PausedDecisionDoesNotAdvanceOrConsumeItsDeadline()
        {
            var session=CreateNavigationSession(30,2,new GridPosition(0,0),new GridPosition(29,0));
            var unit=session.ActiveUnit;var ap=unit.CurrentActionPoints;
            var ai=new SimpleCombatAiSystem();
            using var job=ai.CreateNextActionJob(session,unit,ai.CreateTurnState(),timeoutSeconds:.05);
            job.SetPaused(true);
            System.Threading.Thread.Sleep(70);
            job.AdvanceSlice();
            Assert.That(job.IsComplete||job.HasTimedOut||job.Executed,Is.False);
            Assert.That(unit.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(unit.Position,Is.EqualTo(new GridPosition(0,0)));
            job.SetPaused(false);
            job.AdvanceSlice(maxSteps:1);
            Assert.That(job.HasTimedOut,Is.False);
        }

        [Test]
        public void DecisionDeadlineCancelsBeforeApplyingAndRemainsCanceled()
        {
            var session=CreateNavigationSession(30,2,new GridPosition(0,0),new GridPosition(29,0));
            var unit=session.ActiveUnit;var ap=unit.CurrentActionPoints;
            var ai=new SimpleCombatAiSystem();
            using var job=ai.CreateNextActionJob(session,unit,ai.CreateTurnState(),timeoutSeconds:.02);
            job.AdvanceSlice(maxSteps:1);
            System.Threading.Thread.Sleep(30);
            job.AdvanceSlice();job.AdvanceSlice();
            Assert.That(job.IsComplete && job.IsCanceled && job.HasTimedOut,Is.True);
            Assert.That(job.Executed,Is.False);Assert.That(unit.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(unit.Position,Is.EqualTo(new GridPosition(0,0)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UndeadWaitsForBleedingBeforeBuffingAndStillPursuesTargets(bool bleeding)
        {
            var definition=UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_undead.asset");
            var unit=new BattleUnit { Character=new Godmist.Systems.Characters.CharacterInstanceFactory()
                .Create(definition,definition.ClassData,10), Position=new GridPosition(0,0),
                CurrentActionPoints=10000,MaxActionPoints=10000,IsPlayerSide=false };
            unit.Character.ResourcePool.CurrentValue=unit.Character.ResourcePool.MaxValue;
            var hero=CreateAiCombatUnit("hero",true,new GridPosition(3,0));
            hero.Character.CurrentHealth=100000;hero.Character.Stats.MaxHealth=new Stat(100000);
            var session=new CombatSession { Grid=new BattleGrid(9,3),ActiveUnit=unit,Units=new(){unit,hero} };
            if(bleeding)hero.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId="bleed",
                Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=2 });
            var ai=CreateProductionWraithAi();var turn=ai.CreateTurnState();
            var attacked = false;
            for (var step = 0; step < 8 && !attacked; step++)
            {
                var hadBleeding = StatusEffectSystem.HasStatus(hero, StatusEffectIds.Bleed);
                if (!ai.TryExecuteNextAction(session, unit, turn, out var action)) break;
                if (action.Skill?.SkillId == "skill.enemy.catacombs.blood_scent")
                {
                    Assert.That(hadBleeding, Is.True, "Do not spend on the weak variant while bleeding can be established.");
                    Assert.That(unit.Character.PassiveEffects.Any(effect => effect.EffectId.EndsWith(".modify_stat.1")), Is.False);
                }
                attacked = action.Skill != null && PassiveEffectCombatExecutionSystem.IsOffensiveSkill(action.Skill);
            }
            Assert.That(attacked, Is.True);
            Assert.That(unit.Position, Is.Not.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void DefaultSliceUsesTimeBudgetInsteadOfStoppingAfter128CheapSteps()
        {
            var session = CreateNavigationSession(30, 2, new GridPosition(0, 0), new GridPosition(29, 0));
            var ai = new SimpleCombatAiSystem();
            using var job = ai.CreateNextActionJob(session, session.ActiveUnit, ai.CreateTurnState());
            job.AdvanceSlice(milliseconds: 1000);
            Assert.That(job.IsComplete, Is.True, "Cheap searches should not wait arbitrary extra frames.");
            Assert.That(job.Executed, Is.True);
        }

        [Test]
        public void PlanningLatencyComparisonOnLargeRoom()
        {
            foreach (var stepLimit in new[] { 128, int.MaxValue })
            {
                var session = CreateNavigationSession(30, 30, new GridPosition(0, 0), new GridPosition(29, 29));
                var ai = new SimpleCombatAiSystem();
                using var job = ai.CreateNextActionJob(session, session.ActiveUnit, ai.CreateTurnState());
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var slices = 0;
                while (!job.IsComplete && ++slices < 10000) job.AdvanceSlice(maxSteps: stepLimit);
                Assert.That(job.Executed, Is.True);
                TestContext.WriteLine($"AI 30x30: limit={stepLimit}, slices={slices}, CPU={timer.Elapsed.TotalMilliseconds:F1} ms, delay at 30FPS={(slices - 1) * 1000d / 30:F0} ms");
            }
        }

        [Test]
        public void LargeSingleTargetRangeEvaluatesOccupiedPositionsInsteadOfEveryEmptyCell()
        {
            var session = CreateNavigationSession(100, 100, new GridPosition(0, 0), new GridPosition(99, 0));
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(skill, TargetingType.Enemies, 100);
            var mode = skill.TargetModes[0];
            var targeting = new CombatTargetingService();
            var method = typeof(CombatTargetingService).GetMethod("EnumerateSelectablePositionWork",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var steps = ((IEnumerable<GridPosition?>)method.Invoke(targeting,
                new object[] { session, session.ActiveUnit, mode, false, null })).ToArray();
            Assert.That(steps.Length, Is.LessThanOrEqualTo(4));
            Assert.That(steps.Where(p => p.HasValue).Select(p => p.Value),
                Is.EqualTo(new[] { session.Units[1].Position }));
        }

        [Test]
        public void SlicedPlanningMatchesSynchronousMovementAndDoesNotMoveBeforeCompletion()
        {
            var sync = CreateNavigationSession(12, 2, new GridPosition(0, 0), new GridPosition(11, 0));
            var sliced = CreateNavigationSession(12, 2, new GridPosition(0, 0), new GridPosition(11, 0));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(sync, sync.ActiveUnit, ai.CreateTurnState(), out var expected), Is.True);
            using var job = ai.CreateNextActionJob(sliced, sliced.ActiveUnit, ai.CreateTurnState());
            var initialAp = sliced.ActiveUnit.CurrentActionPoints;
            var slices = 0;
            while (!job.IsComplete)
            {
                job.AdvanceSlice(maxSteps: 1);
                Assert.That(++slices, Is.LessThan(10000));
                if (!job.IsComplete)
                {
                    Assert.That(sliced.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
                    Assert.That(sliced.ActiveUnit.CurrentActionPoints, Is.EqualTo(initialAp));
                }
            }
            Assert.That(slices, Is.GreaterThan(10));
            Assert.That(job.Executed, Is.True);
            Assert.That(job.Action.Type, Is.EqualTo(expected.Type));
            Assert.That(sliced.ActiveUnit.Position, Is.EqualTo(sync.ActiveUnit.Position));
            Assert.That(sliced.ActiveUnit.CurrentActionPoints, Is.EqualTo(sync.ActiveUnit.CurrentActionPoints));
            job.AdvanceSlice();
            Assert.That(job.Executed, Is.True);
        }

        [Test]
        public void SlicedSkillsPreserveTieOrderAndZeroCostTracking()
        {
            var caster = CreateAiCombatUnit("ai", false, new GridPosition(0, 0));
            var target = CreateAiCombatUnit("target", true, new GridPosition(1, 0));
            var first = CreateZeroCostDamageSkill("first");
            var second = CreateZeroCostDamageSkill("second");
            caster.Character.Skills.Add(first);
            caster.Character.Skills.Add(second);
            var session = CreateAiSession(caster, target);
            var ai = CreateExecutableAiSystem();
            var turn = ai.CreateTurnState();
            foreach (var skill in new[] { first, second })
            {
                using var job = ai.CreateNextActionJob(session, caster, turn);
                var health = target.Character.CurrentHealth;
                var ap = caster.CurrentActionPoints;
                var slices = 0;
                while (!job.IsComplete)
                {
                    job.AdvanceSlice(maxSteps: 1);
                    Assert.That(++slices, Is.LessThan(1000));
                    if (!job.IsComplete)
                    {
                        Assert.That(target.Character.CurrentHealth, Is.EqualTo(health));
                        Assert.That(caster.CurrentActionPoints, Is.EqualTo(ap));
                    }
                }
                Assert.That(job.Executed, Is.True);
                Assert.That(job.Action.Skill, Is.SameAs(skill));
                Assert.That(target.Character.CurrentHealth, Is.EqualTo(health - 1));
            }
            using var exhausted = ai.CreateNextActionJob(session, caster, turn);
            for (var i = 0; !exhausted.IsComplete && i < 1000; i++) exhausted.AdvanceSlice(maxSteps: 1);
            Assert.That(exhausted.IsComplete, Is.True);
            Assert.That(exhausted.Executed, Is.False);
        }
        [TestCase("dispose")]
        [TestCase("membership")]
        [TestCase("active-unit")]
        [TestCase("action-points")]
        [TestCase("owner")]
        [TestCase("position")]
        [TestCase("outcome")]
        public void SlicedPlanningCancelsWithoutApplyingAnAction(string change)
        {
            var session = CreateNavigationSession(30, 2, new GridPosition(0, 0), new GridPosition(29, 0));
            var unit = session.ActiveUnit;
            var ap = unit.CurrentActionPoints;
            var ownerValid = true;
            var ai = new SimpleCombatAiSystem();
            using var job = ai.CreateNextActionJob(session, unit, ai.CreateTurnState(), () => ownerValid);
            for (var i = 0; i < 8; i++) job.AdvanceSlice(maxSteps: 1);
            Assert.That(job.IsComplete, Is.False);
            switch (change)
            {
                case "dispose": job.Dispose(); break;
                case "membership": session.MembershipChangePending = true; break;
                case "active-unit": session.ActiveUnit = session.Units[1]; break;
                case "action-points": unit.CurrentActionPoints--; ap--; break;
                case "owner": ownerValid = false; break;
                case "position": unit.Position = new GridPosition(1, 0); break;
                case "outcome": session.Outcome = CombatOutcomeState.Victory; break;
            }
            job.AdvanceSlice();
            Assert.That(job.IsCanceled, Is.True);
            Assert.That(job.Executed, Is.False);
            Assert.That(unit.Position, Is.EqualTo(new GridPosition(change == "position" ? 1 : 0, 0)));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(ap));
        }

        [Test]
        public void Navigation_UnreachableUpperTargetVisitsBoundedGraphInsteadOfEnumeratingPaths()
        {
            var session = CreateNavigationSession(12, 12, new GridPosition(0, 0), new GridPosition(11, 11, 1));
            var grid = new BattleGrid(12, 12, nextUsesExplicitWalkablePositions: true);
            for (var x = 0; x < 12; x++) for (var y = 0; y < 12; y++) grid.SetWalkable(new GridPosition(x, y));
            grid.SetWalkable(session.Units[1].Position);
            var visits = 0;
            grid.TraversalNeighbours = p =>
            {
                Assert.That(++visits, Is.LessThan(5000), "An unreachable layer must not enumerate combinatorial route lengths.");
                return p.Level == 0 ? new[] { new GridPosition(p.X + 1, p.Y), new GridPosition(p.X - 1, p.Y),
                    new GridPosition(p.X, p.Y + 1), new GridPosition(p.X, p.Y - 1) } : Array.Empty<GridPosition>();
            };
            session.Grid = grid;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_EnemyOnUpperFloorDescendsThroughActualStairNodes()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(3, 0, 1), new GridPosition(0, 0));
            var map = new DungeonFloorMap();
            for (var x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            var upper = new DungeonSurfacePosition(new Vector2Int(3, 0), 1);
            map.TryAddFloorCell(upper, "room");
            Assert.That(map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                Enumerable.Range(0, 4).Select(x => new Vector2Int(x, 0)).ToArray()), Is.True);
            var context = new DungeonCombatContext("d", 0, "enc", "room", Vector2Int.zero, Vector2Int.right,
                map.Surfaces.Select(s => s.Cell).Distinct(), null, null, surfaceMap: map);
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            session.Grid = grid;
            session.DungeonContext = context;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.True);
            Assert.That(session.ActiveUnit.Position.IsOnStairs, Is.True);
            Assert.That(session.ActiveUnit.Position.StairStep, Is.EqualTo(1));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(ActionPointUnits.FromPoints(7)));
        }

        [Test]
        public void Navigation_DisconnectedLargeUpperRoomCompletesSearchPromptly()
        {
            const int size = 48;
            var session = CreateNavigationSession(size, size, new GridPosition(0, 0, 1), new GridPosition(size - 1, size - 1));
            var grid = new BattleGrid(size, size, nextUsesExplicitWalkablePositions: true);
            for (var x = 0; x < size; x++) for (var y = 0; y < size; y++) grid.SetWalkable(new GridPosition(x, y, 1));
            grid.SetWalkable(session.Units[1].Position);
            grid.TraversalNeighbours = p => new[] { new GridPosition(p.X + 1, p.Y, p.Level),
                new GridPosition(p.X - 1, p.Y, p.Level), new GridPosition(p.X, p.Y + 1, p.Level), new GridPosition(p.X, p.Y - 1, p.Level) };
            session.Grid = grid;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            TestContext.WriteLine($"Disconnected upper room search: {watch.ElapsedMilliseconds} ms");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(2000), "Unreachable upper rooms must not block the main thread for seconds.");
        }

        [Test]
        public void ContactEscapeRequiresFullTurnsOfEveryEnemyAndResetsOnReacquisition()
        {
            var session = CreateNavigationSession(8, 2, new GridPosition(0, 0), new GridPosition(6, 0));
            var first = session.Units[0]; var player = session.Units[1];
            var second = CreateAiCombatUnit("second", false, new GridPosition(0, 1));
            session.Units.Add(second);
            var seen = false;
            session.Contact = new DungeonContactState((e, p) => seen);
            session.Contact.BeginTurn(session, first); session.Contact.EndTurn(session, first);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, second);
            seen = true; session.Contact.Refresh(session);
            seen = false; session.Contact.EndTurn(session, second);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, first); session.Contact.EndTurn(session, first);
            session.Contact.BeginTurn(session, second); session.Contact.EndTurn(session, second);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, first); session.Contact.EndTurn(session, first);
            Assert.That(session.Contact.CanLeave(session, player), Is.False);
            session.Contact.BeginTurn(session, second); session.Contact.EndTurn(session, second);
            Assert.That(session.Contact.CanLeave(session, player), Is.True);
            session.IsSeamless = session.IsDungeonWide = true;
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session, player), Is.True);
            Assert.That(session.DepartedUnits[player.Character.CharacterId], Is.SameAs(player));
            Assert.That(session.Units.Contains(player), Is.False);
            new CombatOutcomeSystem().EvaluateCombatOutcome(session);
            Assert.That(session.Outcome, Is.EqualTo(CombatOutcomeState.Escaped));
        }

        [Test]
        public void ContactDoesNotCountPartialTurnAndWaitsForNewEnemy()
        {
            var session = CreateNavigationSession(8, 2, new GridPosition(0, 0), new GridPosition(6, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var seen = true;
            var contact = session.Contact = new DungeonContactState((e, p) => seen);
            contact.BeginTurn(session, enemy);
            seen = false; contact.EndTurn(session, enemy);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, enemy); contact.EndTurn(session, enemy);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, enemy); contact.EndTurn(session, enemy);
            Assert.That(contact.CanLeave(session, player), Is.True);
            var reinforcement = CreateAiCombatUnit("reinforcement", false, new GridPosition(0, 1));
            session.Units.Add(reinforcement); contact.Refresh(session);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, reinforcement); contact.EndTurn(session, reinforcement);
            Assert.That(contact.CanLeave(session, player), Is.False);
            contact.BeginTurn(session, reinforcement); contact.EndTurn(session, reinforcement);
            Assert.That(contact.CanLeave(session, player), Is.True);
        }

        [Test]
        public void ContactIsIndividualAndSearchUsesLastSeenPositionInsteadOfHiddenTarget()
        {
            var session = CreateNavigationSession(10, 2, new GridPosition(0, 0), new GridPosition(3, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var visible = true;
            var contact = session.Contact = new DungeonContactState((e, p) => visible);
            contact.Refresh(session);
            visible = false; player.Position = new GridPosition(8, 0); contact.Refresh(session);
            Assert.That(contact.TryGetSearchPosition(session, enemy, out var last), Is.True);
            Assert.That(last, Is.EqualTo(new GridPosition(3, 0)));
            var otherEnemy = CreateAiCombatUnit("uninformed", false, new GridPosition(0, 1));
            session.Units.Add(otherEnemy);
            Assert.That(contact.TryGetSearchPosition(session, otherEnemy, out _), Is.False);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.FindClosestPlayerUnit(session, enemy), Is.Null);
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(enemy.Position.X, Is.LessThanOrEqualTo(3));
            Assert.That(player.Position.X, Is.EqualTo(8));
        }

        [Test]
        public void ContactOtherVisiblePartyMemberDoesNotPreventIndividualEscape()
        {
            var session = CreateNavigationSession(8, 2, new GridPosition(0, 0), new GridPosition(6, 0));
            var enemy = session.Units[0]; var hidden = session.Units[1];
            var visible = CreateAiCombatUnit("visible", true, new GridPosition(3, 0));
            session.Units.Add(visible);
            session.Contact = new DungeonContactState((e, p) => p == visible);
            session.Contact.BeginTurn(session, enemy); session.Contact.EndTurn(session, enemy);
            Assert.That(session.Contact.CanLeave(session, hidden), Is.False);
            session.Contact.BeginTurn(session, enemy); session.Contact.EndTurn(session, enemy);
            Assert.That(session.Contact.CanLeave(session, hidden), Is.True);
            Assert.That(session.Contact.CanLeave(session, visible), Is.False);
            Assert.That(new SimpleCombatAiSystem().FindClosestPlayerUnit(session, enemy), Is.SameAs(visible));
        }

        [Test]
        public void ContactMergePreservesIndividualMemoryButRequiresFreshReactionTurns()
        {
            var destination = CreateNavigationSession(10, 2, new GridPosition(0, 0), new GridPosition(3, 0));
            var source = CreateNavigationSession(10, 2, new GridPosition(0, 1), new GridPosition(3, 1));
            var seen = true;
            destination.Contact = new DungeonContactState((e, p) => seen);
            source.Contact = new DungeonContactState((e, p) => seen);
            destination.Contact.Refresh(destination); source.Contact.Refresh(source);
            seen = false;
            destination.Contact.BeginTurn(destination, destination.Units[0]);
            destination.Contact.EndTurn(destination, destination.Units[0]);
            Assert.That(destination.Contact.CanLeave(destination, destination.Units[1]), Is.False);
            destination.Contact.BeginTurn(destination, destination.Units[0]);
            destination.Contact.EndTurn(destination, destination.Units[0]);
            Assert.That(destination.Contact.CanLeave(destination, destination.Units[1]), Is.True);
            destination.Units.AddRange(source.Units);
            destination.Contact.Absorb(source.Contact); destination.Contact.Refresh(destination);
            Assert.That(destination.Contact.CanLeave(destination, destination.Units[1]), Is.False);
            Assert.That(destination.Contact.TryGetSearchPosition(destination, source.Units[0], out var last), Is.True);
            Assert.That(last, Is.EqualTo(source.Units[1].Position));
        }

        [Test]
        public void UnseenTargetCannotBeChosenEvenBySkillWithoutLineOfSightRequirement()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(8, 0));
            session.Contact = new DungeonContactState((e, p) => false);
            session.Contact.Refresh(session);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            var method = typeof(SimpleCombatAiSystem).GetMethod("FilterTargetsByLineOfSight", BindingFlags.NonPublic | BindingFlags.Instance);
            var targets = (IReadOnlyList<BattleUnit>)method.Invoke(new SimpleCombatAiSystem(),
                new object[] { session, session.Units[0], skill, new[] { session.Units[1] } });
            Assert.That(targets.Count, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PursuitCrossesRoomBoundaryToVisibleOrLastSeenTarget(bool remainsVisible)
        {
            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            for (var x = 0; x < 10; x++) map.TryAddFloorCell(new Vector2Int(x, 0), x < 4 ? "one" : "two");
            var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0); floor.TrySetMap(map);
            var dungeon = new Godmist.Runtime.Dungeons.DungeonInstance("test", 1); dungeon.TryAddFloor(floor);
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();
            createdAssets.Add(definition);
            var encounter = new Godmist.Runtime.Dungeons.DungeonEncounterInstance("encounter", definition, "one", new RectInt(0, 0, 4, 1));
            Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context, true), Is.True);
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            session.Grid = grid; session.DungeonContext = context; session.IsSeamless = session.IsDungeonWide = true;
            var enemy = session.Units[0]; var target = session.Units[1];
            var seen = true;
            session.Contact = new DungeonContactState((e, p) => seen);
            session.Contact.Refresh(session);
            seen = remainsVisible;
            if (!seen) target.Position = new GridPosition(9, 0);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _), Is.True);
            Assert.That(enemy.Position.X, Is.GreaterThanOrEqualTo(4), "Enemy must enter the second room.");
            Assert.That(enemy.Position.X, Is.LessThanOrEqualTo(6));
        }

        [Test]
        public void RoomAlertSeedsPursuitWithoutGivingSightOrFollowingHiddenMovement()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var contact = session.Contact = new DungeonContactState((e, p) => false);
            contact.ReceiveAlert(enemy, player, new GridPosition(4, 0));
            player.Position = new GridPosition(9, 0); contact.Refresh(session);
            Assert.That(contact.Sees(enemy, player), Is.False);
            Assert.That(contact.TryGetSearchPosition(session, enemy, out var last), Is.True);
            Assert.That(last, Is.EqualTo(new GridPosition(4, 0)));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _), Is.True);
            Assert.That(enemy.Position.X, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void DelayedRoomAlertDoesNotOverwritePersonalObservation()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            var visible = true;
            var contact = session.Contact = new DungeonContactState((e, p) => visible);
            contact.Refresh(session); visible = false; contact.Refresh(session);
            contact.ReceiveAlert(session.Units[0], session.Units[1], new GridPosition(2, 0));
            Assert.That(contact.TryGetSearchPosition(session, session.Units[0], out var last), Is.True);
            Assert.That(last, Is.EqualTo(new GridPosition(6, 0)));
        }

        [Test]
        public void PursuitContinuesToOtherKnownPositionAfterCheckingNearestOne()
        {
            var session = CreateNavigationSession(10, 1, new GridPosition(2, 0), new GridPosition(8, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var companion = CreateAiCombatUnit("companion", true, new GridPosition(9, 0)); session.Units.Add(companion);
            session.Contact = new DungeonContactState((e,p) => false);
            session.Contact.ReceiveAlert(enemy, companion, enemy.Position);
            session.Contact.ReceiveAlert(enemy, player, new GridPosition(5, 0));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _), Is.True);
            Assert.That(enemy.Position.X, Is.GreaterThan(2), "A checked empty location must not mask another observed destination.");
        }

        [Test]
        public void MovementThroughDoorRecordsVisibleStepsBeforePlayerHidesBesideWall()
        {
            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            for (var x = 0; x < 7; x++) for (var y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(x,y), x < 3 ? "one" : "two");
            for (var y = 0; y < 4; y++) if (y != 1) map.TryAddWall(new Vector2Int(2,y), new Vector2Int(3,y));
            var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0); floor.TrySetMap(map);
            var dungeon = new Godmist.Runtime.Dungeons.DungeonInstance("d",1); dungeon.TryAddFloor(floor);
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>(); createdAssets.Add(definition);
            var encounter = new Godmist.Runtime.Dungeons.DungeonEncounterInstance("e",definition,"one",new RectInt(0,0,3,4));
            new DungeonCombatContextFactory().TryCreate(dungeon,encounter,out var context,true);
            new DungeonBattleGridFactory().TryCreate(context,out var grid);
            var session = CreateNavigationSession(7,4,new GridPosition(1,1),new GridPosition(2,1));
            session.Grid=grid; session.DungeonContext=context;
            var enemy=session.Units[0]; var player=session.Units[1];
            var vision = new Godmist.Systems.Dungeons.DungeonVisibilitySystem();
            session.Contact=new DungeonContactState((e,p)=>vision.CanObserveCombat(floor,
                DungeonBattleGridFactory.ToDungeonTraversalPosition(context,e.Position),10,
                DungeonBattleGridFactory.ToDungeonTraversalPosition(context,p.Position)));
            session.UnitEnteredCell=unit=>session.Contact.Refresh(session);
            session.Contact.Refresh(session);
            new CombatMovementSystem().TryMoveAlongPath(session,player,new[]{new GridPosition(3,1),new GridPosition(4,1),new GridPosition(4,2)});
            Assert.That(session.Contact.Sees(enemy,player),Is.False);
            Assert.That(session.Contact.TryGetSearchPosition(session,enemy,out var last),Is.True);
            Assert.That(last,Is.EqualTo(new GridPosition(4,1)));
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position.X,Is.EqualTo(2), "From the doorway approach the open edge already reveals the player; stop before crossing it.");
            Assert.That(enemy.Position.Y,Is.EqualTo(1));
            Assert.That(session.Contact.Sees(enemy,player),Is.True);
            Assert.That(ai.FindClosestPlayerUnit(session,enemy),Is.SameAs(player));
        }

        [TestCase(false, 4)]
        [TestCase(true, 2)]
        public void PursuitContinuesTwoCellsInObservedDirectionButNeverThroughWall(bool wall, int expectedX)
        {
            var session = CreateNavigationSession(10, 2, new GridPosition(0,0), new GridPosition(1,0));
            var enemy = session.Units[0]; var player = session.Units[1];
            var seen = true;
            session.Contact = new DungeonContactState((e,p)=>seen);
            session.Contact.Refresh(session);
            player.Position = new GridPosition(2,0); session.Contact.Refresh(session);
            seen = false;
            player.Position = new GridPosition(8,1); // Hidden movement must not redirect the search.
            session.Contact.Refresh(session);
            if (wall) session.Grid.SetWallEdge(new GridPosition(2,0),new GridPosition(3,0),DungeonWallType.Full);
            var ai = new SimpleCombatAiSystem();
            var steps = 0;
            while (ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _))
                Assert.That(++steps,Is.LessThan(5),"Search must terminate after its two forward steps.");
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(expectedX,0)));
        }

        [TestCase(DoorInteractionMode.DirectOnly, true, false, false)]
        [TestCase(DoorInteractionMode.DirectAndLever, true, false, false)]
        [TestCase(DoorInteractionMode.LeverOnly, false, false, false)]
        [TestCase(DoorInteractionMode.DirectOnly, true, true, false)]
        [TestCase(DoorInteractionMode.DirectAndLever, true, true, false)]
        [TestCase(DoorInteractionMode.LeverOnly, false, true, false)]
        [TestCase(DoorInteractionMode.DirectOnly, false, false, true)]
        [TestCase(DoorInteractionMode.DirectOnly, false, true, true)]
        public void PursuitOpensDoorInLastObservedDirection(DoorInteractionMode mode, bool opens, bool dungeonGrid, bool wall)
        {
            var session = CreateNavigationSession(10, 2, new GridPosition(0, 0), new GridPosition(1, 0));
            var enemy = session.Units[0]; var player = session.Units[1];
            enemy.CurrentActionPoints = 5000;
            var seen = true;
            session.Contact = new DungeonContactState((e, p) => seen);
            session.Contact.Refresh(session);
            player.Position = new GridPosition(2, 0); session.Contact.Refresh(session);
            seen = false; player.Position = new GridPosition(8, 1); session.Contact.Refresh(session);
            DungeonFloorInstance floor = null;
            if (dungeonGrid)
            {
                var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.DoorDungeonObjectDefinition>();
                var encounterDefinition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();
                createdAssets.Add(definition); createdAssets.Add(encounterDefinition);
                SetAutoProperty(definition, "InteractionMode", mode);
                floor = new DungeonFloorInstance(0);
                var map = new DungeonFloorMap();
                for (var x = -5; x < 5; x++) for (var y = 7; y < 9; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(-5, 7), 1), "room");
                floor.TrySetMap(map);
                floor.TryAddObject(new DoorDungeonObjectInstance("door", definition,
                    new DungeonCellEdge(new Vector2Int(-3, 7), new Vector2Int(-2, 7))));
                var dungeon = new DungeonInstance("door-test", 1); dungeon.TryAddFloor(floor);
                var encounter = new DungeonEncounterInstance("room", encounterDefinition, "room", new RectInt(-5, 7, 10, 2));
                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter, out var context), Is.True);
                session.DungeonContext = context;
                Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
                session.Grid = grid;
            }
            var door = AddNavigationDoor(session, 2, mode);
            if (wall) session.Grid.SetWallEdge(new GridPosition(2, 0), new GridPosition(3, 0), DungeonWallType.Full);
            var ai = new SimpleCombatAiSystem(interactionSystem: new CombatInteractionSystem(onObjectChanged:
                value => floor?.TrySetDoorOpen(value.ObjectId, ((DoorCombatMapObject)value).IsOpen)));
            var actions = 0;
            while (ai.TryExecuteNextAction(session, enemy, ai.CreateTurnState(), out _))
                Assert.That(++actions, Is.LessThan(7));
            Assert.That(door.IsOpen, Is.EqualTo(opens));
            Assert.That(enemy.Position, Is.EqualTo(new GridPosition(opens ? 4 : 2, 0)));
            if (floor != null) Assert.That(floor.Objects.OfType<DoorDungeonObjectInstance>().Single().IsOpen, Is.EqualTo(opens));
        }

        [Test]
        public void SearchStopsMidRouteOnReacquisitionAndNextActionMovesTowardsVisibleTarget()
        {
            var session = CreateNavigationSession(10,3,new GridPosition(0,0),new GridPosition(2,2));
            var enemy=session.Units[0]; var player=session.Units[1];
            session.Contact=new DungeonContactState((e,p)=>e.Position.X >= 2);
            session.Contact.ReceiveAlert(enemy,player,new GridPosition(8,0));
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(2,0)));
            Assert.That(session.Contact.Sees(enemy,player),Is.True);
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(2,1)));
        }

        [Test]
        public void ReacquisitionDoesNotInferDirectionFromUnobservedMovement()
        {
            var session = CreateNavigationSession(10,2,new GridPosition(0,0),new GridPosition(1,0));
            var seen=true; var player=session.Units[1]; var enemy=session.Units[0];
            session.Contact=new DungeonContactState((e,p)=>seen);
            session.Contact.Refresh(session); seen=false; session.Contact.Refresh(session);
            player.Position=new GridPosition(4,0); seen=true; session.Contact.Refresh(session);
            seen=false; player.Position=new GridPosition(9,1); session.Contact.Refresh(session);
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(4,0)));
            Assert.That(ai.TryExecuteNextAction(session,enemy,ai.CreateTurnState(),out _),Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CalculateSkillScore_PrefersGuaranteedKillOverNonLethalAreaDamage()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var lowHealthEnemy = CreateUnit("low", 10, 0, 0);
            var firstAreaEnemy = CreateUnit("area-first", 20, 0, 0);
            var secondAreaEnemy = CreateUnit("area-second", 20, 0, 0);
            lowHealthEnemy.IsPlayerSide=firstAreaEnemy.IsPlayerSide=secondAreaEnemy.IsPlayerSide=true;
            caster.Character.WeaponAccuracy=100;
            var killSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            var areaSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 0.9f));

            SetField(killSkill.Effects[0],"alwaysHits",true);SetField(areaSkill.Effects[0],"alwaysHits",true);
            var killScore = CalculateScore(caster, killSkill, lowHealthEnemy);
            var areaScore = CalculateScore(caster, areaSkill,
                firstAreaEnemy, secondAreaEnemy);

            Assert.That(killScore, Is.GreaterThan(areaScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersHardControlOverOrdinaryStatus()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var enemy = CreateUnit("enemy", 100, 20, 20);
            enemy.IsPlayerSide=true; enemy.Position=new GridPosition(1,0);
            var threat=PlannerAttack("control-threat",50,1f);enemy.Character.Skills.Add(threat);
            var stun = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(stun, "statusId", "stun");
            var poison = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(poison, "statusId", "poison");

            var stunScore = CalculateScore(caster, CreateSkill(stun), enemy);
            var poisonScore = CalculateScore(caster, CreateSkill(poison), enemy);

            Assert.That(stunScore, Is.GreaterThan(poisonScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersReviveOverShield()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var downedAlly = CreateUnit("downed", 0, 0, 0);
            downedAlly.Character.LifeState = CharacterLifeState.Downed;
            var heal = CreateEffect(EffectType.Heal, 1f);
            SetField(heal, "baseValue", 20);
            var shield = CreateEffect(EffectType.Shield, 1f);
            SetField(shield, "baseValue", 100);

            var reviveScore = CalculateScore(caster, CreateSkill(heal), downedAlly);
            var shieldScore = CalculateScore(caster, CreateSkill(shield), downedAlly);

            Assert.That(reviveScore, Is.GreaterThan(shieldScore));
        }

        [Test]
        public void AiSkipsActiveNonstackingBuffButAllowsItAfterExpiration()
        {
            var caster = CreateUnit("archer", 100, 10, 10);
            var buff = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(buff, "SkillId", "archer.quiver");
            var active = new PassiveEffectRuntime
            {
                EffectId = "archer.quiver.modify_stat.0",
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 3
            };
            caster.Character.PassiveEffects.Add(active);
            var targets = new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { caster } };
            var method = typeof(SimpleCombatAiSystem).GetMethod("IsRedundantBuff",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method.Invoke(null, new object[] { new CombatSession { Units = new() { caster } }, caster, buff, targets }), Is.True);
            active.RemainingDuration = 0;
            Assert.That(method.Invoke(null, new object[] { new CombatSession { Units = new() { caster } }, caster, buff, targets }), Is.False);
        }

        [Test]
        public void ArcherSelectsAttackAfterQuiverIsActive()
        {
            var archer = CreateAiCombatUnit("archer", false, new GridPosition(0, 0));
            var enemy = CreateAiCombatUnit("enemy", true, new GridPosition(1, 0));
            var session = CreateAiSession(archer, enemy);
            var quiver = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(quiver, "SkillId", "archer.quiver");
            SetField(quiver.Effects[0],"modifierStatType",CharacterStatType.MaxAttack);
            SetField(quiver.Effects[0],"modifierValue",20f);
            SetField(quiver.Effects[0],"durationTurns",2);
            SetAutoProperty(quiver, "ActionCost", 40);
            SetField(quiver.TargetModes[0], "targetType", TargetType.Caster);
            SetField(quiver.TargetModes[0], "targetingType", TargetingType.Allies);
            var attack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(attack, "SkillId", "archer.attack");
            ConfigureTargetMode(attack, TargetingType.Enemies, 1);
            archer.Character.Skills.Add(quiver);
            archer.Character.Skills.Add(attack);
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = new SimpleCombatAiSystem();
            SkillData Chosen()
            {
                var selected = find.Invoke(ai, new object[] { session, archer, new HashSet<SkillData>() });
                return (SkillData)selected.GetType().GetField("Skill").GetValue(selected);
            }

            Assert.That(Chosen(), Is.SameAs(quiver));
            archer.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "archer.quiver.modify_stat.0",
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 3
            });
            Assert.That(Chosen(), Is.SameAs(attack));
        }

        [Test]
        public void AiDoesNotRepeatActiveControlOrAttemptFullyResistedControl()
        {
            var caster = CreateAiCombatUnit("caster", false, new GridPosition(0, 0));
            var target = CreateAiCombatUnit("target", true, new GridPosition(1, 0));
            var session = CreateAiSession(caster, target);
            target.Character.Skills.Add(PlannerAttack("threatens-control-caster",50,5f));
            var controlEffect = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(controlEffect, "statusId", StatusEffectIds.Stun);
            SetField(controlEffect, "baseChance", 1f);
            var control = CreateSkill(controlEffect);
            SetAutoProperty(control, "SkillId", "ai.control");
            ConfigureTargetMode(control, TargetingType.Enemies, 1);
            var attack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(attack, "SkillId", "ai.attack");
            ConfigureTargetMode(attack, TargetingType.Enemies, 1);
            caster.Character.Skills.Add(control);
            caster.Character.Skills.Add(attack);
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = new SimpleCombatAiSystem();
            SkillData Chosen()
            {
                var selected = find.Invoke(ai, new object[] { session, caster, new HashSet<SkillData>() });
                return (SkillData)selected.GetType().GetField("Skill").GetValue(selected);
            }

            Assert.That(Chosen(), Is.SameAs(control));
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Stun,
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 2
            });
            Assert.That(Chosen(), Is.SameAs(attack));
            target.Character.PassiveEffects.Clear();
            target.Character.Resistances = new ResistanceSet { SuppressionResistance = 1f };
            Assert.That(Chosen(), Is.SameAs(attack));
        }

        [Test]
        public void AiSelectsDematerializationThenMaterializationCombo()
        {
            var caster = CreateAiCombatUnit("wraith", false, new GridPosition(0, 0));
            var enemy = CreateAiCombatUnit("enemy", true, new GridPosition(1, 0));
            var session = CreateAiSession(caster, enemy);
            var setupEffect = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(setupEffect, "statusId", "invisibility");
            SetField(setupEffect,"durationTurns",3);
            var setup = CreateSkill(setupEffect);
            SetAutoProperty(setup, "SkillId", "wraith.dematerialize");
            SetField(setup.TargetModes[0], "targetType", TargetType.Caster);
            SetField(setup.TargetModes[0], "targetingType", TargetingType.Allies);
            var followUp = CreateSkill(CreateEffect(EffectType.ModifyStat, 1f));
            SetAutoProperty(followUp, "SkillId", "wraith.materialize");
            SetField(followUp.Effects[0],"modifierStatType",CharacterStatType.MaxAttack);
            SetField(followUp.Effects[0],"modifierOperation",StatModifierOperation.Percent);
            SetField(followUp.Effects[0],"modifierValue",.5f);
            SetField(followUp.Effects[0],"durationTurns",1);
            SetAutoProperty(followUp, "RequiredCasterStatusId", "invisibility");
            SetField(followUp.TargetModes[0], "targetType", TargetType.Caster);
            SetField(followUp.TargetModes[0], "targetingType", TargetingType.Allies);
            var attack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(attack, "SkillId", "wraith.attack");
            ConfigureTargetMode(attack, TargetingType.Enemies, 1);
            caster.Character.Skills.Add(attack);
            caster.Character.Skills.Add(setup);
            caster.Character.Skills.Add(followUp);
            var find = typeof(SimpleCombatAiSystem).GetMethod("FindBestSkillSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ai = new SimpleCombatAiSystem();
            SkillData Chosen()
            {
                var selected = find.Invoke(ai, new object[] { session, caster, new HashSet<SkillData>() });
                return (SkillData)selected.GetType().GetField("Skill").GetValue(selected);
            }

            Assert.That(Chosen(), Is.SameAs(setup));
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = "invisibility", Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 3
            });
            Assert.That(Chosen(), Is.SameAs(followUp));
        }

        [Test]
        public void GetPreferredCombatRange_IgnoresLongRangeHealForMeleeDps()
        {
            var meleeAttack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(meleeAttack, TargetingType.Enemies, 1);
            var longRangeHeal = CreateSkill(CreateEffect(EffectType.Heal, 1f));
            ConfigureTargetMode(longRangeHeal, TargetingType.Allies, 4);
            var unit = CreateUnit("dps", 100, 10, 10);
            unit.Character.Skills.Add(meleeAttack);
            unit.Character.Skills.Add(longRangeHeal);
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "GetPreferredCombatRange",
                BindingFlags.Static | BindingFlags.NonPublic);

            var range = (int)method.Invoke(null, new object[] { unit });

            Assert.That(range, Is.EqualTo(1));
        }

        [Test]
        public void CombatFeedbackEvent_CapturesTargetPositionAtPublishTime()
        {
            var target = CreateUnit("target", 100, 0, 0);
            target.Position = new GridPosition(2, 3);

            var feedback = new CombatFeedbackEvent(
                target,
                CombatFeedbackType.Damage,
                amount: 12);

            target.Position = new GridPosition(7, 6);

            Assert.That(feedback.Position, Is.EqualTo(new GridPosition(2, 3)));
        }

        [TestCase(30)]
        [TestCase(35)]
        public void TryExecuteNextAction_PaidAttackUsesPointUnitsForUtility(int costPercent)
        {
            var attacker = CreateAiCombatUnit("ai", false, new GridPosition(0, 0));
            var target = CreateAiCombatUnit("target", true, new GridPosition(1, 0));
            var skill = CreateZeroCostDamageSkill("skill.paid");
            SetAutoProperty(skill, "ActionCost", costPercent);
            SetField(skill.Effects[0], "damageMultiplier", 1f);
            attacker.Character.Skills.Add(skill);
            var remaining = attacker.CurrentActionPoints - CombatActionCostCalculator.CalculateSkillCost(attacker, skill);
            var session = CreateAiSession(attacker, target);
            session.ActionPointsPerSpeed = 1f; // Keep the fixture budget consistent with Speed = 10.
            var ai = CreateExecutableAiSystem();

            Assert.That(ai.TryExecuteNextAction(session, attacker, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(90));
            Assert.That(attacker.CurrentActionPoints, Is.EqualTo(remaining));
        }

        [Test]
        public void TryExecuteNextAction_ExecutesOneZeroCostSkillPerCallAndThenStops()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            var firstSkill = CreateZeroCostDamageSkill("skill.first");
            var secondSkill = CreateZeroCostDamageSkill("skill.second");
            aiUnit.Character.Skills.Add(firstSkill);
            aiUnit.Character.Skills.Add(secondSkill);

            var session = CreateAiSession(aiUnit, target);
            var aiSystem = CreateExecutableAiSystem();
            var turnState = aiSystem.CreateTurnState();

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out var firstAction),
                Is.True);
            Assert.That(firstAction.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(firstAction.Skill, Is.SameAs(firstSkill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(99));

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out var secondAction),
                Is.True);
            Assert.That(secondAction.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(secondAction.Skill, Is.SameAs(secondSkill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out _),
                Is.False);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));
        }

        [Test]
        public void ExecuteSimpleAiTurn_EndsAfterZeroCostSkillsAreExhausted()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            aiUnit.Character.Skills.Add(CreateZeroCostDamageSkill("skill.first"));
            aiUnit.Character.Skills.Add(CreateZeroCostDamageSkill("skill.second"));

            var session = CreateAiSession(aiUnit, target);
            var endTurnCalls = 0;
            var aiSystem = new SimpleCombatAiSystem(
                endTurn: _ => endTurnCalls++,
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());

            aiSystem.ExecuteSimpleAiTurn(session, aiUnit);

            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));
            Assert.That(endTurnCalls, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteSimpleAiTurn_EndsWhenEveryPlayerTargetIsDowned()
        {
            var aiUnit = CreateAiCombatUnit(
                "skeleton-defender",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var downedParty = new[]
            {
                CreateAiCombatUnit("jerzy", true, new GridPosition(1, 0)),
                CreateAiCombatUnit("alison", true, new GridPosition(2, 0)),
                CreateAiCombatUnit("wladimir", true, new GridPosition(3, 0))
            };
            foreach (var unit in downedParty)
            {
                unit.Character.CurrentHealth = 0;
                unit.Character.LifeState = CharacterLifeState.Downed;
                unit.DownedTurnsRemaining = BattleUnit.DownedTurnLimit;
            }

            var session = new CombatSession
            {
                CombatId = "test.ai-all-targets-downed",
                Outcome = CombatOutcomeState.Ongoing,
                IsSeamless = true,
                CanReceiveReinforcements = true,
                Grid = new BattleGrid(4, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit }
            };
            session.Units.AddRange(downedParty);

            var endTurnCalls = 0;
            var aiSystem = new SimpleCombatAiSystem(
                endTurn: _ => endTurnCalls++,
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());

            var initialActionPoints = aiUnit.CurrentActionPoints;
            aiSystem.ExecuteSimpleAiTurn(session, aiUnit);

            Assert.That(endTurnCalls, Is.EqualTo(1));
            Assert.That(aiUnit.CurrentActionPoints, Is.EqualTo(initialActionPoints));
            Assert.That(session.ActiveUnit, Is.SameAs(aiUnit));
        }

        [Test]
        public void SlicedAiExecutesLowChanceMissAgainstOnlyLivingTargetAndThenEndsTurn()
        {
            var previousRandom=UnityEngine.Random.state;
            try
            {
            UnityEngine.Random.InitState(7);
            var aiUnit = CreateAiCombatUnit(
                "skeleton-defender",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var livingTarget = CreateAiCombatUnit(
                "reimond",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            var downedParty = new[]
            {
                CreateAiCombatUnit("jerzy", true, new GridPosition(2, 0)),
                CreateAiCombatUnit("alison", true, new GridPosition(3, 0)),
                CreateAiCombatUnit("wladimir", true, new GridPosition(4, 0))
            };
            foreach (var unit in downedParty)
            {
                unit.Character.CurrentHealth = 0;
                unit.Character.LifeState = CharacterLifeState.Downed;
                unit.DownedTurnsRemaining = BattleUnit.DownedTurnLimit;
            }

            var skill = CreateZeroCostDamageSkill("test.ai.forced-miss");
            SetAutoProperty(skill, "ActionCost", 100);
            SetAutoProperty(skill, "SkillAccuracy", 1f);
            aiUnit.Character.WeaponAccuracy = 1;
            // A 1% finisher remains useful, while the fixed seed resolves a miss. A guaranteed miss should be skipped.
            SetField(skill.Effects[0], "damageMultiplier", 100f);
            SetField(skill.Effects[0], "alwaysHits", false);
            aiUnit.Character.Skills.Add(skill);
            var actionPointCost = CombatActionCostCalculator.CalculateSkillCost(aiUnit, skill);
            Assert.That(actionPointCost, Is.GreaterThan(0));
            aiUnit.CurrentActionPoints = actionPointCost;

            var session = new CombatSession
            {
                CombatId = "test.ai-forced-miss-with-downed-party",
                Outcome = CombatOutcomeState.Ongoing,
                IsSeamless = true,
                CanReceiveReinforcements = true,
                Grid = new BattleGrid(5, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, livingTarget }
            };
            session.Units.AddRange(downedParty);

            var feedback = new List<CombatFeedbackEvent>();
            var feedbackBus = new CombatFeedbackBus();
            feedbackBus.Published += feedback.Add;
            var endTurnCalls = 0;
            var aiSystem = new SimpleCombatAiSystem(
                endTurn: _ => endTurnCalls++,
                skillExecutionSystem: new SkillExecutionSystem(feedbackBus: feedbackBus),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());

            var initialHealth = livingTarget.Character.CurrentHealth;
            var turnState = aiSystem.CreateTurnState();
            using (var job = aiSystem.CreateNextActionJob(session, aiUnit, turnState))
            {
                var slices = 0;
                while (!job.IsComplete)
                {
                    job.AdvanceSlice(milliseconds: 1000);
                    Assert.That(++slices, Is.LessThan(1000));
                }

                Assert.That(job.IsCanceled, Is.False);
                Assert.That(job.Executed, Is.True,
                    "The AI must execute the selected skill against the one living target.");
                Assert.That(job.Action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
                Assert.That(job.Action.Skill, Is.SameAs(skill));
            }

            Assert.That(aiUnit.CurrentActionPoints, Is.EqualTo(0),
                "A miss still consumes the skill's action-point cost.");
            Assert.That(livingTarget.Character.CurrentHealth, Is.EqualTo(initialHealth),
                "The forced miss must not damage the living target.");
            var miss = feedback.Single(eventData => eventData.Type == CombatFeedbackType.Miss);
            Assert.That(miss.Target, Is.SameAs(livingTarget));
            Assert.That(miss.Source, Is.SameAs(aiUnit));
            Assert.That(miss.SourcePosition, Is.EqualTo(aiUnit.Position));

            // The same turn now has no AP left. The normal turn loop must still
            // invoke its end-turn callback instead of waiting on unavailable targets.
            aiSystem.ExecuteSimpleAiTurn(session, aiUnit);
            Assert.That(endTurnCalls, Is.EqualTo(1));
            Assert.That(session.ActiveUnit, Is.SameAs(aiUnit));
            }
            finally { UnityEngine.Random.state=previousRandom; }
        }

        [Test]
        public void SkillMissFeedbackRetainsTheCasterForPresentationDirection()
        {
            var caster = CreateAiCombatUnit("skeleton-defender", false, new GridPosition(0, 0));
            var target = CreateAiCombatUnit("jerzy", true, new GridPosition(1, 0));
            var session = CreateAiSession(caster, target);
            var skill = CreateZeroCostDamageSkill("test.miss");
            caster.Character.WeaponAccuracy = 0;
            SetAutoProperty(skill, "SkillAccuracy", 0f);
            SetField(skill.Effects[0], "alwaysHits", false);
            var feedback = new List<CombatFeedbackEvent>();
            var bus = new CombatFeedbackBus();
            bus.Published += feedback.Add;
            var skillExecution = new SkillExecutionSystem(feedbackBus: bus);

            Assert.That(skillExecution.TryExecute(
                session,
                caster,
                skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { target } },
                teleportDestination: null,
                objectPlacementDestination: null), Is.True);

            var miss = feedback.Single(eventData => eventData.Type == CombatFeedbackType.Miss);
            Assert.That(miss.Source, Is.SameAs(caster));
            Assert.That(miss.SourcePosition, Is.EqualTo(caster.Position));
        }

        [Test]
        public void TryMoveAlongPath_PublishesSingleApAndMomentumFeedback()
        {
            var unit = CreateAiCombatUnit(
                "moving-unit",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            unit.Character.ResourcePool = new ResourcePool(
                ResourceType.Momentum,
                currentValue: 0,
                maxValue: 200,
                regeneration: 0);

            var session = new CombatSession
            {
                CombatId = "test.path-feedback",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(3, 1),
                ActiveUnit = unit,
                Units = new List<BattleUnit> { unit }
            };
            var feedbackBus = new CombatFeedbackBus();
            var feedbackEvents = new List<CombatFeedbackEvent>();
            feedbackBus.Published += feedbackEvents.Add;
            var movementSystem = new CombatMovementSystem(
                resourceRuleSystem: new ResourceRuleSystem(feedbackBus: feedbackBus),
                feedbackBus: feedbackBus);

            Assert.That(
                movementSystem.TryMoveAlongPath(
                    session,
                    unit,
                    new[] { new GridPosition(1, 0), new GridPosition(2, 0) }),
                Is.True);

            Assert.That(
                feedbackEvents.FindAll(e => e.Type == CombatFeedbackType.ActionPointSpend),
                Has.Count.EqualTo(1));
            Assert.That(
                feedbackEvents.FindAll(e => e.Type == CombatFeedbackType.ResourceGain),
                Has.Count.EqualTo(1));
            Assert.That(
                feedbackEvents.Find(e => e.Type == CombatFeedbackType.ActionPointSpend).Amount,
                Is.EqualTo(300));
            Assert.That(
                feedbackEvents.Find(e => e.Type == CombatFeedbackType.ResourceGain).Amount,
                Is.EqualTo(6));
        }

        [Test]
        public void CalculateReachablePathsForNextTurn_UsesFreshActionPointsAndMovementCost()
        {
            var unit = CreateAiCombatUnit(
                "preview-unit",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            unit.CurrentActionPoints = 0;
            unit.MaxActionPoints = 300;
            unit.MovementStepsThisTurn = 4;
            var session = new CombatSession
            {
                CombatId = "test.next-turn-movement-preview",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 1),
                Units = new List<BattleUnit> { unit }
            };

            var paths = new CombatMovementSystem()
                .CalculateReachablePathsForNextTurn(session, unit);

            Assert.That(paths.ContainsKey(new GridPosition(1, 0)), Is.True);
            Assert.That(paths.ContainsKey(new GridPosition(2, 0)), Is.True);
            Assert.That(paths.ContainsKey(new GridPosition(3, 0)), Is.False);
        }

        [Test]
        public void DirectionalArea_TargetsConfiguredDiagonalRays()
        {
            var caster = CreateAiCombatUnit("caster", false, new GridPosition(3, 3));
            var first = CreateAiCombatUnit("first", true, new GridPosition(4, 2));
            var second = CreateAiCombatUnit("second", true, new GridPosition(5, 5));
            var excluded = CreateAiCombatUnit("excluded", true, new GridPosition(4, 3));
            var mode = new SkillTargetModeData();
            SetField(mode, "targetType", TargetType.AreaAroundCaster);
            SetField(mode, "targetingType", TargetingType.Enemies);
            SetField(mode, "absoluteRange", new SkillRangeData(RangeShape.Diagonal, 1, 2));
            var session = new CombatSession
            {
                CombatId = "test.directional-diagonal",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(8, 8),
                Units = new List<BattleUnit> { caster, first, second, excluded }
            };

            var targets = new CombatTargetingService().GetTargets(
                session, caster, mode, new GridPosition(4, 3));

            Assert.That(targets, Is.EquivalentTo(new[] { first, second }));
        }

        [Test]
        public void RelativeSquare_RotationChangesEvenSideAnchor()
        {
            var caster = CreateAiCombatUnit(
                "caster",
                false,
                new GridPosition(2, 2));
            var session = new CombatSession
            {
                CombatId = "test.relative-square-rotation",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(9, 9),
                Units = new List<BattleUnit> { caster }
            };
            var range = new SkillRangeData(RangeShape.Square, 0, 1);
            var service = new CombatTargetingService();

            var unrotated = service.GetRelativeRangePositions(
                session,
                caster,
                range,
                new GridPosition(5, 5));
            var rotated = service.GetRelativeRangePositions(
                session,
                caster,
                range,
                new GridPosition(5, 5),
                rotationQuarterTurns: 1);

            Assert.That(unrotated, Is.EquivalentTo(new[]
            {
                new GridPosition(5, 5),
                new GridPosition(6, 5),
                new GridPosition(5, 6),
                new GridPosition(6, 6)
            }));
            Assert.That(rotated, Is.EquivalentTo(new[]
            {
                new GridPosition(4, 5),
                new GridPosition(5, 5),
                new GridPosition(4, 6),
                new GridPosition(5, 6)
            }));
        }

        [Test]
        public void Rectangle_RotationDoesNotDependOnDirectionReference()
        {
            var caster = CreateAiCombatUnit(
                "caster",
                false,
                new GridPosition(4, 4));
            var session = new CombatSession
            {
                CombatId = "test.rectangle-rotation",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(9, 9),
                Units = new List<BattleUnit> { caster }
            };
            var range = new SkillRangeData(RangeShape.Rectangle, 1, 2, 2);
            var service = new CombatTargetingService();

            var fromEast = service.GetDirectionalRangePositions(
                session, caster, range, new GridPosition(5, 4));
            var fromNorth = service.GetDirectionalRangePositions(
                session, caster, range, new GridPosition(4, 5));
            var rotated = service.GetDirectionalRangePositions(
                session, caster, range, new GridPosition(5, 4),
                rotationQuarterTurns: 1);

            Assert.That(fromNorth, Is.EquivalentTo(fromEast));
            Assert.That(rotated, Is.Not.EquivalentTo(fromEast));
        }

        [Test]
        public void AreaAroundTile_RelativeSquareTargetsFollowRotation()
        {
            var caster = CreateAiCombatUnit(
                "caster",
                false,
                new GridPosition(2, 2));
            var unrotatedTarget = CreateAiCombatUnit(
                "unrotated-target",
                true,
                new GridPosition(6, 6));
            var rotatedTarget = CreateAiCombatUnit(
                "rotated-target",
                true,
                new GridPosition(4, 6));
            var mode = new SkillTargetModeData();
            SetField(mode, "targetType", TargetType.AreaAroundTile);
            SetField(mode, "targetingType", TargetingType.Enemies);
            SetField(mode, "absoluteRange", new SkillRangeData(RangeShape.Square, 0, 8));
            SetField(mode, "relativeRange", new SkillRangeData(RangeShape.Square, 0, 1));
            var session = new CombatSession
            {
                CombatId = "test.area-around-tile-square-rotation",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(9, 9),
                Units = new List<BattleUnit>
                {
                    caster,
                    unrotatedTarget,
                    rotatedTarget
                }
            };
            var service = new CombatTargetingService();
            var selectedTile = new GridPosition(5, 5);

            var unrotatedTargets = service.GetTargets(
                session,
                caster,
                mode,
                selectedTile,
                rotationQuarterTurns: 0);
            var rotatedTargets = service.GetTargets(
                session,
                caster,
                mode,
                selectedTile,
                rotationQuarterTurns: 1);

            Assert.That(unrotatedTargets, Is.EquivalentTo(new[] { unrotatedTarget }));
            Assert.That(rotatedTargets, Is.EquivalentTo(new[] { rotatedTarget }));
        }

        [Test]
        public void TryExecuteNextAction_MovesReachablePathPrefixWhenTargetIsFartherThanAp()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            aiUnit.CurrentActionPoints = 300;
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(3, 0));
            var session = new CombatSession
            {
                CombatId = "test.ai-partial-path",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, target }
            };

            var aiSystem = CreateExecutableAiSystem();

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    aiSystem.CreateTurnState(),
                    out var action),
                Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(action.MovementPath, Is.EqualTo(new[]
            {
                new GridPosition(1, 0),
                new GridPosition(2, 0)
            }));
            Assert.That(aiUnit.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(aiUnit.CurrentActionPoints, Is.EqualTo(0));
        }

        [Test]
        public void Navigation_DetoursAroundEdgeWallAcrossTurnsInitiallyMovingAway()
        {
            var session = CreateNavigationSession(5, 4, new GridPosition(1, 1), new GridPosition(3, 1));
            var unit = session.ActiveUnit;
            for (var y = 0; y < 3; y++)
                session.Grid.SetBlockedEdge(new GridPosition(1, y), new GridPosition(2, y));
            var ai = new SimpleCombatAiSystem();
            var visited = new List<GridPosition>();
            for (var turn = 0; turn < 8; turn++)
            {
                unit.CurrentActionPoints = 100;
                unit.MovementStepsThisTurn = 0;
                var before = unit.Position;
                if (!ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action)) break;
                Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
                foreach (var step in action.MovementPath)
                {
                    Assert.That(session.Grid.CanMoveBetween(before, step), Is.True);
                    before = step;
                    visited.Add(step);
                }
                Assert.That(unit.CurrentActionPoints, Is.Zero);
            }
            Assert.That(ai.GetManhattanDistance(visited[0], session.Units[1].Position), Is.GreaterThan(2));
            Assert.That(visited, Does.Contain(new GridPosition(1, 3)));
            Assert.That(ai.IsAdjacent(unit.Position, session.Units[1].Position), Is.True);
        }

        [Test]
        public void Navigation_ClosedDoorIsSeparatePaidInteractionAndThenTraversable()
        {
            var session = CreateNavigationSession(6, 1, new GridPosition(0, 0), new GridPosition(5, 0));
            var unit = session.ActiveUnit;
            var door = AddNavigationDoor(session, 2, DoorInteractionMode.DirectOnly);
            var changed = 0;
            var interaction = new CombatInteractionSystem(onObjectChanged: _ => changed++);
            var ai = new SimpleCombatAiSystem(interactionSystem: interaction);
            var state = ai.CreateTurnState();

            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var approach), Is.True);
            Assert.That(approach.MovementPath, Is.EqualTo(new[] { new GridPosition(1, 0), new GridPosition(2, 0) }));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(700));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var opening), Is.True);
            Assert.That(opening.Type, Is.EqualTo(SimpleCombatAiActionType.Interaction));
            Assert.That(opening.MovementPath, Is.Null);
            Assert.That(unit.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(500));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(session.Grid.CanMoveBetween(new GridPosition(2, 0), new GridPosition(3, 0)), Is.True);
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out var crossing), Is.True);
            Assert.That(crossing.MovementPath, Is.EqualTo(new[] { new GridPosition(3, 0) }));
            Assert.That(unit.CurrentActionPoints, Is.EqualTo(200));
            Assert.That(ai.TryExecuteNextAction(session, unit, state, out _), Is.False);
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void Navigation_TargetImmediatelyBehindDoorOpensWithoutEnteringOccupiedCell()
        {
            var session = CreateNavigationSession(2, 1, new GridPosition(0, 0), new GridPosition(1, 0));
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Interaction));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out _), Is.False);
        }

        [Test]
        public void Navigation_CannotAffordDoorEndsTurnWithoutTogglingOrSpending()
        {
            var session = CreateNavigationSession(3, 1, new GridPosition(0, 0), new GridPosition(2, 0));
            var door = AddNavigationDoor(session, 0, DoorInteractionMode.DirectOnly);
            session.ActiveUnit.CurrentActionPoints = 100;
            var ended = 0;
            var ai = new SimpleCombatAiSystem(endTurn: _ => ended++);
            ai.ExecuteSimpleAiTurn(session, session.ActiveUnit);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(100));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Navigation_LeverOnlyDoorRequiresExternalOpening(bool open)
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            var door = AddNavigationDoor(session, 1, DoorInteractionMode.LeverOnly, open);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.EqualTo(open));
            Assert.That(door.IsOpen, Is.EqualTo(open));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(open ? 2 : 0, 0)));
        }

        [Test]
        public void Navigation_ReplansAfterOccupancyAndDoorChanges()
        {
            var session = CreateNavigationSession(5, 1, new GridPosition(0, 0), new GridPosition(4, 0));
            var blocker = CreateAiCombatUnit("blocker", false, new GridPosition(2, 0));
            session.Units.Add(blocker);
            var door = AddNavigationDoor(session, 2, DoorInteractionMode.LeverOnly);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            session.Units.Remove(blocker);
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            door.IsOpen = true;
            new CombatInteractionSystem().InitializeMapObjects(session);
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.True);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(3, 0)));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(400));
        }

        [Test]
        public void Navigation_ReplansAfterObstacleRemoval()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            session.Grid.SetBlocked(new GridPosition(1, 0), true);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.False);
            session.Grid.SetBlocked(new GridPosition(1, 0), false);
            Assert.That(ai.TryMoveAiTowardsTarget(session, session.ActiveUnit, session.Units[1]), Is.True);
        }

        [Test]
        public void Navigation_AdjacentTargetAcrossWallRequiresRealDetour()
        {
            var session = CreateNavigationSession(3, 2, new GridPosition(0, 0), new GridPosition(1, 0));
            session.Grid.SetBlockedEdge(new GridPosition(0, 0), new GridPosition(1, 0));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath, Is.EqualTo(new[] { new GridPosition(0, 1), new GridPosition(1, 1) }));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(700));
        }

        [Test]
        public void Navigation_UnreachableTargetEndsTurnWithoutFutileMovement()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            session.Grid.SetBlockedEdge(new GridPosition(1, 0), new GridPosition(2, 0));
            var ended = 0;
            new SimpleCombatAiSystem(endTurn: _ => ended++).ExecuteSimpleAiTurn(session, session.ActiveUnit);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(1000));
        }

        [Test]
        public void Navigation_EnemyCannotUseRoomExitAsDetour()
        {
            var session = CreateNavigationSession(3, 2, new GridPosition(0, 0), new GridPosition(2, 0));
            session.IsSeamless = true;
            session.Grid = new BattleGrid(3, 2, nextUsesExplicitWalkablePositions: true);
            for (var x = 0; x < 3; x++) session.Grid.SetWalkable(new GridPosition(x, 0));
            session.Grid.SetBlocked(new GridPosition(1, 0), true);
            session.OpenExitEdges.Add(new BattleCellEdge(new GridPosition(0, 0), new GridPosition(0, 1)));
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(
                session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_RespectsMovementStepsAlreadySpent()
        {
            var session = CreateNavigationSession(4, 1, new GridPosition(0, 0), new GridPosition(3, 0));
            session.ActiveUnit.MovementStepsThisTurn = 2;
            session.ActiveUnit.CurrentActionPoints = 200;
            Assert.That(new SimpleCombatAiSystem().TryMoveAiTowardsTarget(
                session, session.ActiveUnit, session.Units[1]), Is.False);
            Assert.That(session.ActiveUnit.CurrentActionPoints, Is.EqualTo(200));
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void Navigation_RangedRoleDetoursWhenTargetIsInRangeButBehindWall()
        {
            var session = CreateNavigationSession(4, 2, new GridPosition(0, 0), new GridPosition(2, 0));
            var unit = session.ActiveUnit;
            SetAutoProperty(unit.Character.Definition, "AiRole", CombatAiRole.Dps);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            SetAutoProperty(skill, "RequiresLineOfSight", true);
            ConfigureTargetMode(skill, TargetingType.Enemies, 3);
            unit.Character.Skills.Add(skill);
            var wall = new BattleCellEdge(new GridPosition(1, 0), new GridPosition(2, 0));
            session.Grid.SetBlockedEdge(wall.FirstCell, wall.SecondCell);
            // Grid edges describe movement; the dungeon supplies physical sight blockers separately.
            session.EnvironmentBlocksLineOfSight = (from, to) => CombatLineOfSightService.CrossesEdge(from, to, wall);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(session, unit.Position, session.Units[1].Position), Is.False);
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath, Does.Contain(new GridPosition(1, 1)));
            Assert.That(new CombatLineOfSightService().HasLineOfSight(session, unit.Position, session.Units[1].Position), Is.True);
        }

        [Test]
        public void Navigation_RangedRetreatSkipsWallAndUsesLegalAlternative()
        {
            var session = CreateNavigationSession(4, 3, new GridPosition(1, 1), new GridPosition(0, 1));
            var unit = session.ActiveUnit;
            SetAutoProperty(unit.Character.Definition, "AiRole", CombatAiRole.Dps);
            var skill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(skill, TargetingType.Enemies, 3);
            unit.Character.Skills.Add(skill);
            session.Grid.SetBlockedEdge(new GridPosition(1, 1), new GridPosition(2, 1));
            var ai = new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session, unit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath, Is.EqualTo(new[] { new GridPosition(1, 2) }));
        }

        [Test]
        public void Navigation_PartialMovementPublishesOneAggregatedApEvent()
        {
            var session = CreateNavigationSession(7, 1, new GridPosition(0, 0), new GridPosition(6, 0));
            session.ActiveUnit.CurrentActionPoints = 300;
            var bus = new CombatFeedbackBus();
            var events = new List<CombatFeedbackEvent>();
            bus.Published += events.Add;
            var ai = new SimpleCombatAiSystem(movementSystem: new CombatMovementSystem(feedbackBus: bus));
            Assert.That(ai.TryExecuteNextAction(session, session.ActiveUnit, ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.MovementPath.Count, Is.EqualTo(2));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Type, Is.EqualTo(CombatFeedbackType.ActionPointSpend));
            Assert.That(events[0].Amount, Is.EqualTo(300));
        }

        private CombatSession CreateNavigationSession(int width, int height, GridPosition start, GridPosition target)
        {
            var unit = CreateAiCombatUnit("ai", false, start);
            return new CombatSession
            {
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(width, height),
                ActiveUnit = unit,
                Units = new List<BattleUnit> { unit, CreateAiCombatUnit("target", true, target) }
            };
        }

        private static DoorCombatMapObject AddNavigationDoor(
            CombatSession session, int x, DoorInteractionMode mode, bool open = false)
        {
            var door = new DoorCombatMapObject("door", new GridPosition(x, 0), new GridPosition(x + 1, 0), mode, open);
            session.MapObjects.Add(door);
            new CombatInteractionSystem().InitializeMapObjects(session);
            return door;
        }

        private float CalculateScore(
            BattleUnit caster,
            SkillData skill,
            params BattleUnit[] targets)
        {
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "CalculateSkillScore",
                BindingFlags.Static | BindingFlags.NonPublic);
            var targetsByMode = new Dictionary<int, IReadOnlyList<BattleUnit>>
            {
                [0] = targets
            };

            return (float)method.Invoke(null, new object[]
            {
                new CombatSession { Grid = new BattleGrid(8,8), Units = targets.Append(caster).Distinct().ToList() },
                caster,
                skill,
                targetsByMode
            });
        }

        private SkillData CreateSkill(SkillEffectData effect)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetField(skill, "targetModes", new List<SkillTargetModeData>
            {
                new()
            });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static void ConfigureTargetMode(
            SkillData skill,
            TargetingType targetingType,
            int maxRange)
        {
            var mode = skill.TargetModes[0];
            SetField(mode, "targetType", TargetType.Single);
            SetField(mode, "targetingType", targetingType);
            SetField(mode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, maxRange));
        }

        private static SkillEffectData CreateEffect(EffectType type, float multiplier)
        {
            var effect = new SkillEffectData();
            SetField(effect, "effectType", type);
            SetField(effect, "targetModeIndex", 0);
            SetField(effect, "damageMultiplier", multiplier);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            return effect;
        }

        private static BattleUnit CreateUnit(
            string id,
            int health,
            float minAttack,
            float maxAttack)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    CurrentHealth = health,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(minAttack),
                        MaxAttack = new Stat(maxAttack)
                    }
                }
            };
        }

        private BattleUnit CreateAiCombatUnit(
            string id,
            bool isPlayerSide,
            GridPosition position)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdAssets.Add(definition);
            SetAutoProperty(definition, "DisplayName", id);

            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    Definition = definition,
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive,
                    ResourcePool = new ResourcePool(ResourceType.None, 0, 0, 0),
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(10f),
                        MaxAttack = new Stat(10f),
                        PhysicalDefense = new Stat(0f),
                        MagicalDefense = new Stat(0f),
                        Dodge = new Stat(0f),
                        CritChance = new Stat(0f),
                        CritDamage = new Stat(1.5f),
                        Speed = new Stat(10f),
                        WeaponAccuracy = new Stat(100f)
                    }
                },
                Position = position,
                CurrentActionPoints = 1000,
                MaxActionPoints = 1000,
                IsPlayerSide = isPlayerSide
            };
        }

        private static CombatSession CreateAiSession(
            BattleUnit aiUnit,
            BattleUnit target)
        {
            return new CombatSession
            {
                CombatId = "test.ai-action-step",
                Outcome = CombatOutcomeState.Ongoing,
                // Keep the adjacent units in a one-row arena so that the
                // post-skill assertion cannot accidentally observe a move
                // around the target as another valid AI action.
                Grid = new BattleGrid(2, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, target }
            };
        }

        private SkillData CreateZeroCostDamageSkill(string skillId)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetAutoProperty(skill, "SkillId", skillId);
            SetAutoProperty(skill, "DisplayName", skillId);
            SetAutoProperty(skill, "ActionCost", 0);
            SetAutoProperty(skill, "SkillAccuracy", 100f);
            SetAutoProperty(skill, "RequiresLineOfSight", false);

            var targetMode = new SkillTargetModeData();
            SetField(targetMode, "targetType", TargetType.Single);
            SetField(targetMode, "targetingType", TargetingType.Enemies);
            SetField(targetMode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, 1));

            var effect = CreateEffect(EffectType.DealDamage, 0.1f);
            SetField(effect, "alwaysHits", true);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            SetField(effect, "damageType", DamageType.Physical);

            SetField(skill, "targetModes", new List<SkillTargetModeData> { targetMode });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static SimpleCombatAiSystem CreateExecutableAiSystem()
        {
            return new SimpleCombatAiSystem(
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }
    }
}
