using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class WarriorDischargeTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + path);
        static SkillData Skill => Load<SkillData>("Skills/SD_WarriorDischarge.asset");
        static BattleUnit Unit(int rage = 50, bool player = true)
        {
            var character = new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_Wladimir_Test.asset"));
            character.CharacterId = Guid.NewGuid().ToString(); character.SkillTreeProgress.Clear();
            character.Stats.MaxHealth = new Stat(10000); character.CurrentHealth = 10000;
            character.Stats.MinAttack = new Stat(100); character.Stats.MaxAttack = new Stat(100);
            character.Stats.PhysicalDefense = new Stat(0); character.Stats.MagicalDefense = new Stat(0);
            character.Stats.CritChance = new Stat(0); character.Stats.CritDamage = new Stat(2); character.Stats.Dodge = new Stat(0);
            character.Stats.MaxResource = new Stat(200); character.ResourcePool = new ResourcePool(ResourceType.Rage, rage, 200, 0);
            character.Stats.WeaponAccuracy = new Stat(10000); character.WeaponAccuracy = 10000;
            return new BattleUnit { Character = character, IsPlayerSide = player, Position = new GridPosition(player ? 1 : 2, 2), CurrentActionPoints = 1000, MaxActionPoints = 1000 };
        }
        static void Rank(BattleUnit caster, string id, int rank)
        {
            var progress = caster.Character.SkillTreeProgress.FirstOrDefault(p => p.TreeId == WarriorCombatSystem.Brawler);
            if (progress == null) { progress = new SkillTreeProgress { TreeId = WarriorCombatSystem.Brawler }; caster.Character.SkillTreeProgress.Add(progress); }
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = "node.warrior.brawler." + id, Ranks = rank });
        }
        static CombatSession Session(BattleUnit caster, BattleUnit target) => new CombatSession { Grid = new BattleGrid(8, 8), Units = new List<BattleUnit> { caster, target }, ActiveUnit = caster };

        [Test, Explicit("Apply the user-approved Warrior kit and tree through Editor API.")]
        public void ApplyContent()
        {
            WarriorDischargeSetup.Apply();
            Assert.That(Skill, Is.Not.Null); Assert.That(Skill.Icon, Is.Not.Null);
            var definition = Load<CharacterDefinition>("Characters/CD_Wladimir_Test.asset");
            Assert.That(definition.StartingSkills.Select(s => s.SkillId), Does.Contain("skill.warrior.bleeding_destruction").And.Not.Contain("skill.warrior.bandage"));
            Assert.That(Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Warrior_Brawler.asset").Nodes.Single(n => n.Id == "node.warrior.brawler.bleeding_destruction").GrantedSkill, Is.SameAs(Skill));
        }
        [TestCase(20, 120, 0)] [TestCase(49, 149, 0)] [TestCase(50, 150, 0)] [TestCase(100, 200, 0)] [TestCase(150, 200, 50)]
        public void ExecutionMatchesPreviewCapsSpendAndDoesNotGenerateFury(int rage, int damage, int remaining)
        {
            var caster = Unit(rage); var target = Unit(0, false); var session = Session(caster, target);
            var random = UnityEngine.Random.state;
            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, Skill, 0);
            Assert.That(preview.Damage.Minimum, Is.EqualTo(damage)); Assert.That(preview.Damage.Maximum, Is.EqualTo(damage));
            Assert.That(CombatSkillPreviewCalculator.CalculateDamageRange(caster, Skill).Minimum, Is.EqualTo(damage));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(rage));
            Assert.That(new SkillExecutionSystem().TryExecute(session, caster, Skill, target), Is.True);
            Assert.That(10000 - target.Character.CurrentHealth, Is.EqualTo(damage));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(remaining));
            Assert.That(caster.LastSkillResourceSpent, Is.EqualTo(Math.Min(100, rage)));
            Assert.That(caster.CurrentActionPoints, Is.EqualTo(400));
            Assert.That(caster.WarriorState.RageOffensiveActionUsed, Is.True);
        }
        [TestCase(0)] [TestCase(19)]
        public void RequiresTwentyFuryWithoutSpendingActionPoints(int rage)
        {
            var caster = Unit(rage); var target = Unit(0, false);
            Assert.That(new SkillExecutionSystem().TryExecute(Session(caster, target), caster, Skill, target), Is.False);
            Assert.That(caster.CurrentActionPoints, Is.EqualTo(1000)); Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(rage));
        }
        [TestCase(49, 1, .2f)] [TestCase(50, 1, .4f)] [TestCase(49, 2, .4f)] [TestCase(50, 2, .8f)] [TestCase(150, 2, .8f)]
        public void BreakthroughDoublesAtFiftySpentAndMatchesActualArmorDamage(int rage, int rank, float ignore)
        {
            var caster = Unit(rage); Rank(caster, "larger_wound", rank); var target = Unit(0, false);
            target.Character.Stats.PhysicalDefense = new Stat(100);
            var raw = Mathf.RoundToInt(100 * WarriorDischargeRules.Multiplier(Math.Min(rage, 100)));
            var expected = Mathf.CeilToInt((float)raw * raw / (raw + 100 * (1 - ignore)));
            Assert.That(WarriorDischargeRules.DefenseIgnore(caster, Skill), Is.EqualTo(ignore).Within(.0001));
            Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, Skill).Damage.Minimum, Is.EqualTo(expected));
            Assert.That(new SkillExecutionSystem().TryExecute(Session(caster, target), caster, Skill, target), Is.True);
            Assert.That(10000 - target.Character.CurrentHealth, Is.EqualTo(expected));
        }
        [TestCase(50, 1, 12)] [TestCase(50, 2, 25)] [TestCase(100, 2, 50)] [TestCase(150, 2, 100)]
        public void RefundsActualSpentFuryOnlyWhenDischargeKills(int rage, int rank, int remaining)
        {
            var caster = Unit(rage); Rank(caster, "wounding", rank); var target = Unit(0, false); target.Character.CurrentHealth = 1;
            Assert.That(new SkillExecutionSystem().TryExecute(Session(caster, target), caster, Skill, target), Is.True);
            Assert.That(target.Character.LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(remaining));
            var noKill = Unit(rage); Rank(noKill, "wounding", rank); var survivor = Unit(0, false);
            Assert.That(new SkillExecutionSystem().TryExecute(Session(noKill, survivor), noKill, Skill, survivor), Is.True);
            Assert.That(noKill.Character.ResourcePool.CurrentValue, Is.EqualTo(Math.Max(0, rage - 100)));
        }
        [Test]
        public void CostDiscountsCannotChangeActualSpendAndRefundIsOncePerCast()
        {
            var caster = Unit(100); Rank(caster, "furious_strikes", 3); Rank(caster, "wounding", 2);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, Skill), Is.EqualTo(100));
            var cast = WarriorDischargeRules.Capture(caster, Skill); caster.Character.ResourcePool.CurrentValue = 0;
            Assert.That(WarriorDischargeRules.RefundKill(caster, cast, new CombatRecoverySystem()), Is.EqualTo(50));
            Assert.That(WarriorDischargeRules.RefundKill(caster, cast, new CombatRecoverySystem()), Is.Zero);
        }
        [Test]
        public void CaptureKeepsAttackAndCriticalChanceBeforeResourcePayment()
        {
            var caster = Unit(100); var target = Unit(0, false); caster.Character.Stats.CritChance = new Stat(.35f);
            var cast = WarriorDischargeRules.Capture(caster, Skill);
            caster.Character.Stats.MinAttack = new Stat(1); caster.Character.Stats.MaxAttack = new Stat(1); caster.Character.Stats.CritChance = new Stat(0);
            Assert.That(CombatDamageValueCalculator.CalculateDamageValue(caster, DamageValueSource.MinAttack, 1, cast), Is.EqualTo(100));
            var context = new PassiveEffectCombatContext(source: caster, target: target, skill: Skill, skillId: Skill.SkillId, dischargeCast: cast);
            var chance = (float)typeof(CombatDamageSystem).GetMethod("CalculateRegularCriticalChance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Session(caster, target), caster, context });
            Assert.That(chance, Is.EqualTo(.35f).Within(.0001)); Assert.That(context.WithOutcome().DischargeCast, Is.SameAs(cast));
        }
        [Test]
        public void DodgedStrikeSpendsFuryWithoutKillRefund()
        {
            var caster = Unit(100); Rank(caster, "wounding", 2); var target = Unit(0, false); target.Character.CurrentHealth = 1;
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime { UniqueBuffType = UniqueBuffType.GuaranteedDodge });
            Assert.That(new SkillExecutionSystem().TryExecute(Session(caster, target), caster, Skill, target), Is.True);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(1)); Assert.That(caster.Character.ResourcePool.CurrentValue, Is.Zero);
        }
        [Test]
        public void RefundRespectsResourceGainBlockAndPenetrationIsPhysicalOnly()
        {
            var caster = Unit(100); Rank(caster, "wounding", 2); Rank(caster, "larger_wound", 2);
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = "test.block", Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 2, Payloads = new List<PassiveEffectPayloadRuntime> { new ResourceRegenerationBlockPayload() } });
            var target = Unit(0, false); target.Character.CurrentHealth = 1;
            Assert.That(new SkillExecutionSystem().TryExecute(Session(caster, target), caster, Skill, target), Is.True);
            Assert.That(target.Character.LifeState, Is.EqualTo(CharacterLifeState.Dead)); Assert.That(caster.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(WarriorDischargeRules.DefenseIgnore(caster, Skill, 100, DamageType.Magical), Is.Zero);
        }
        [Test]
        public void StarterBleedIsNotModifiedByRepurposedTalents()
        {
            var caster = Unit(100); Rank(caster, "wounding", 2); Rank(caster, "larger_wound", 2);
            var bleed = Load<SkillData>("Skills/SD_WarriorBleedingDestruction.asset"); var authored = bleed.Effects[1];
            var resolved = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, bleed, authored);
            Assert.That(resolved.BaseChance, Is.EqualTo(authored.BaseChance)); Assert.That(resolved.StatusDamageMultiplier, Is.EqualTo(authored.StatusDamageMultiplier));
        }
        [TestCase(false)] [TestCase(true)]
        public void AiProjectionUsesPreSpendDamageRefundAndUpdatedBonusesWithoutMutatingLiveState(bool kill)
        {
            var caster = Unit(100); Rank(caster, "wounding", 2); Rank(caster, "revenge", 2); Rank(caster, "adrenaline", 1);
            new CharacterStatPipelineSystem().Refresh(caster.Character);
            var target = Unit(0, false); target.Character.CurrentHealth = kill ? 1 : 10000;
            var session = Session(caster, target); var speed = caster.Character.Stats.Speed.GetValue();
            var expected = CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session, caster, target, Skill, 0);
            var type = typeof(SimpleCombatAiSystem); const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var nodeType = type.GetNestedType("AiTurnPlanNode", BindingFlags.NonPublic); var node = Activator.CreateInstance(nodeType, true);
            nodeType.GetField("World").SetValue(node, session); nodeType.GetField("OwnedUnits").SetValue(node, new HashSet<int> { 0 });
            var choiceType = type.GetNestedType("AiSkillSelection", BindingFlags.NonPublic); var choice = Activator.CreateInstance(choiceType, true);
            choiceType.GetField("Skill").SetValue(choice, Skill);
            choiceType.GetField("TargetsByModeIndex").SetValue(choice, new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { target } });
            var random = UnityEngine.Random.state;
            var projection = type.GetMethod("ProjectSkill", flags).Invoke(new SimpleCombatAiSystem(), new[] { node, choice, session, caster });
            Assert.That(projection, Is.Not.Null);
            var world = (CombatSession)nodeType.GetField("World").GetValue(projection); var projected = world.ActiveUnit;
            Assert.That(projected.Character.ResourcePool.CurrentValue, Is.EqualTo(kill ? 50 : 0));
            Assert.That(projected.Character.Stats.CritChance.GetValue(), Is.EqualTo(kill ? .05f : 0f).Within(.0001));
            Assert.That(projected.Character.Stats.Speed.GetValue(), Is.EqualTo(speed - (kill ? 8 : 16)));
            Assert.That(world.Units[1].Character.CurrentHealth, Is.EqualTo(kill ? 0 : 10000 - Mathf.RoundToInt(expected)));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(100)); Assert.That(caster.Character.Stats.Speed.GetValue(), Is.EqualTo(speed));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(kill ? 1 : 10000)); Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        }
        [Test]
        public void LegacySaveMigratesStarterAndPurchasedSkillWithoutChangingInvestmentOrGaps()
        {
            var caster = Unit(); Rank(caster, "bleeding_destruction", 1); Rank(caster, "wounding", 2); Rank(caster, "larger_wound", 2);
            caster.Character.CharacterId = caster.Character.Definition.Id;
            var game = new GameSession(GameModeState.Exploration); game.TryRegisterCharacter(caster.Character); game.TryAddCharacterToActiveParty(caster.Character.CharacterId);
            var save = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Discharge legacy");
            var saved = save.Characters.Single();
            saved.SkillIds[saved.SkillIds.IndexOf("skill.warrior.bleeding_destruction")] = "skill.warrior.bandage";
            saved.HasActiveSkillLoadout = true;
            saved.ActiveSkillIds = new List<string> { "skill.warrior.cut", "skill.warrior.bandage", "skill.warrior.bleeding_destruction" };
            saved.ActiveSkillSlotIds = new List<string> { "skill.warrior.cut", "", "skill.warrior.bandage", "", "skill.warrior.bleeding_destruction" };
            var catalog = Load<GameContentCatalog>("World/GC_MainContentCatalog.asset");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, catalog, out var restored), Is.True);
            var character = restored.CharactersById[caster.Character.CharacterId];
            Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds, Is.EqualTo(new[] { "skill.warrior.cut", "", "skill.warrior.bleeding_destruction", "", "skill.warrior.discharge" }));
            Assert.That(character.GetKnownSkills().Count(s => s.SkillId == "skill.warrior.bleeding_destruction"), Is.EqualTo(1));
            Assert.That(character.Skills.Any(s => s.SkillId == WarriorDischargeRules.SkillId), Is.False);
            Assert.That(WarriorCombatSystem.B(new BattleUnit { Character = character }, "wounding"), Is.EqualTo(2));
            Assert.That(saved.SkillIds, Does.Contain("skill.warrior.bandage"), "Restore must not mutate the source snapshot.");
        }
    }
}
