using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class DotBalanceTests
    {
        readonly List<SkillData> clones = new(); UnityEngine.Random.State random;
        [SetUp] public void Before() { random = UnityEngine.Random.state; UnityEngine.Random.InitState(241026); }
        [TearDown] public void After() { UnityEngine.Random.state = random; foreach (var clone in clones) Object.DestroyImmediate(clone); clones.Clear(); }
        static readonly (string Id, float PerTurn, float Chance, int Turns, int Applications)[] Expected = {
            ("skill.mage.fire_arrow", .30f, .7f, 2, 1), ("skill.mage.fireball", .25f, .75f, 3, 1),
            ("skill.scout.poisoned_dagger", .40f, .65f, 3, 1), ("skill.warrior.bleeding_destruction", .40f, .75f, 4, 1),
            ("skill.enemy.forest.shooter_piercing_bolt", .40f, .8f, 2, 1), ("skill.enemy.catacombs.burning_arrow", .25f, .9f, 3, 1),
            ("skill.enemy.catacombs.cross_cut", .30f, .6f, 3, 2), ("skill.enemy.catacombs.vomit", .50f, .9f, 3, 1)
        };
        static SkillData Skill(string id) => AssetDatabase.LoadAssetAtPath<SkillData>(DotBudgetAuditTests.Cases.Single(c => c.Id == id).Path);
        SkillData Certain(SkillData original)
        {
            var skill = Object.Instantiate(original); clones.Add(skill); var data = new SerializedObject(skill); var effects = data.FindProperty("effects");
            for (var n = 0; n < effects.arraySize; n++)
            {
                var effect = effects.GetArrayElementAtIndex(n); effect.FindPropertyRelative("alwaysHits").boolValue = true;
                effect.FindPropertyRelative("baseChance").floatValue = 1;
                if (!string.IsNullOrEmpty(effect.FindPropertyRelative("onHitStatusId").stringValue)) effect.FindPropertyRelative("onHitStatusChance").floatValue = 1;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); return skill;
        }
        static (BattleUnit Source, BattleUnit Target, CombatSession Session) Actors(string id, int distance = 1)
        {
            var entry = DotBudgetAuditTests.Cases.Single(c => c.Id == id); var enemy = id.Contains(".enemy.");
            var source = DotBudgetAuditTests.Unit(entry.Character, !enemy, .5f, false);
            var target = DotBudgetAuditTests.Unit("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset", enemy, .5f, true);
            target.Position = new GridPosition(2, 2 + distance);
            return (source, target, new CombatSession { Grid = new BattleGrid(12, 12), Units = new() { source, target }, ActiveUnit = source });
        }
        static bool Cast(SkillExecutionSystem execution, CombatSession session, BattleUnit source, BattleUnit target, SkillData skill) =>
            execution.TryExecute(session, source, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { target } },
                null, null, new Dictionary<int, GridPosition?> { [0] = target.Position }, rotationsByModeIndex: new Dictionary<int, int> { [0] = 0 });
        [Test, Explicit("Apply approved coefficients and 40PP cost through targeted Editor API only.")]
        public void ApplyContent()
        {
            DotBalanceSetup.Apply(); DotBalanceSetup.Apply();
            foreach (var e in Expected)
            {
                var skill = Skill(e.Id); var effect = skill.Effects.Single(x => x.EffectType == EffectType.ApplyStatus && StatusEffectSystem.IsDamageOverTimeStatus(x.StatusId)
                    || x.EffectType == EffectType.DealDamage && StatusEffectSystem.IsDamageOverTimeStatus(x.OnHitStatusId));
                Assert.That(effect.StatusDamageMultiplier, Is.EqualTo(e.PerTurn));
                Assert.That(effect.EffectType == EffectType.DealDamage ? effect.OnHitStatusChance : effect.BaseChance, Is.EqualTo(e.Chance));
                Assert.That(effect.EffectType == EffectType.DealDamage ? effect.OnHitStatusDurationTurns : effect.DurationTurns, Is.EqualTo(e.Turns));
            }
            Assert.That(Skill("skill.scout.poisoned_dagger").ResourceCost, Is.EqualTo(40));
        }
        [TestCase("skill.mage.fire_arrow")] [TestCase("skill.mage.fireball")]
        [TestCase("skill.scout.poisoned_dagger")] [TestCase("skill.warrior.bleeding_destruction")]
        [TestCase("skill.enemy.forest.shooter_piercing_bolt")] [TestCase("skill.enemy.catacombs.burning_arrow")]
        [TestCase("skill.enemy.catacombs.cross_cut")] [TestCase("skill.enemy.catacombs.vomit")]
        public void RealSkillExecutionRetainsDurationAndMultiHitApplicationsWithAgreedTickBudget(string id)
        {
            var e = Expected.Single(x => x.Id == id); var (source, target, session) = Actors(id);
            var status = new StatusEffectSystem(); var execution = new SkillExecutionSystem(statusEffectSystem: status);
            var skill = Certain(Skill(id)); Assert.That(Cast(execution, session, source, target, skill), Is.True);
            var dot = target.Character.PassiveEffects.Single(x => StatusEffectSystem.IsDamageOverTimeStatus(x.EffectId));
            var total = Mathf.RoundToInt(100 * e.PerTurn) * e.Turns * e.Applications;
            Assert.That(dot.RemainingDuration, Is.EqualTo(e.Turns)); Assert.That(dot.GetPayload<DamageOverTimePayload>().RemainingTotalDamage, Is.EqualTo(total));
            var health = target.Character.CurrentHealth;
            for (var turn = 0; turn < e.Turns; turn++) { status.ProcessTurnStart(session, target); status.ProcessTurnEnd(target); }
            Assert.That(health - target.Character.CurrentHealth, Is.EqualTo(total));
            Assert.That(target.Character.PassiveEffects.Any(x => StatusEffectSystem.IsDamageOverTimeStatus(x.EffectId)), Is.False);
        }
        [TestCase(1, 0)] [TestCase(2, 1)]
        public void PoisonedDaggerCostsFortyMomentumAndWorksWithoutOptionalViruses(int distance, int knives)
        {
            const string id = "skill.scout.poisoned_dagger"; var (source, target, session) = Actors(id, distance); source.Character.ResourcePool.CurrentValue = 40;
            var status = new StatusEffectSystem(); Assert.That(Cast(new SkillExecutionSystem(statusEffectSystem: status), session, source, target, Certain(Skill(id))), Is.True);
            Assert.That(source.LastSkillResourceSpent, Is.EqualTo(40)); Assert.That(source.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(source.CurrentActionPoints, Is.EqualTo(350)); Assert.That(source.AvailableKnives, Is.EqualTo(5 - knives));
            Assert.That(target.Character.PassiveEffects.Any(x => x.EffectId == "poison"), Is.True);
            Assert.That(target.Character.PassiveEffects.Any(x => x.EffectId.Contains("modify_resistance")), Is.False);
            Assert.That(UIWorkspaceTooltipView.BuildLoadoutSkillCost(Skill(id), source.Character), Does.Contain("40"));
        }
        [Test]
        public void ThirtyNineMomentumFailsBeforeAnyDamageApOrKnifeSpend()
        {
            const string id = "skill.scout.poisoned_dagger"; var (source, target, session) = Actors(id, 2); source.Character.ResourcePool.CurrentValue = 39;
            Assert.That(Cast(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()), session, source, target, Skill(id)), Is.False);
            Assert.That(source.CurrentActionPoints, Is.EqualTo(1000)); Assert.That(source.AvailableKnives, Is.EqualTo(5));
            Assert.That(source.Character.ResourcePool.CurrentValue, Is.EqualTo(39)); Assert.That(target.Character.CurrentHealth, Is.EqualTo(1000000));
        }
        [Test]
        public void ZeroResistanceModifierOnAnUnrelatedSkillStillFailsBeforeSpending()
        {
            const string id = "skill.scout.poisoned_dagger"; var (source, target, session) = Actors(id);
            var skill = Certain(Skill(id)); var data = new SerializedObject(skill);
            data.FindProperty("<SkillId>k__BackingField").stringValue = "skill.test.invalid_zero_resistance";
            data.FindProperty("effects").GetArrayElementAtIndex(1).FindPropertyRelative("modifierValue").floatValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.Expect(LogType.Warning, "Skill 'skill.test.invalid_zero_resistance' has ModifyResistance with value 0.");
            Assert.That(Cast(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()), session, source, target, skill), Is.False);
            Assert.That(source.CurrentActionPoints, Is.EqualTo(1000)); Assert.That(source.Character.ResourcePool.CurrentValue, Is.EqualTo(100));
            Assert.That(source.AvailableKnives, Is.EqualTo(5)); Assert.That(target.Character.CurrentHealth, Is.EqualTo(1000000));
        }
        [Test]
        public void VenomAndVirusesStillScaleDamageAndReducePoisonResistance()
        {
            const string id = "skill.scout.poisoned_dagger"; var (source, target, session) = Actors(id);
            var progress = new SkillTreeProgress { TreeId = ScoutCombatSystem.BladeTree };
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = "node.scout.blade_dancer.venom", Ranks = 3 });
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = "node.scout.blade_dancer.viruses", Ranks = 2 }); source.Character.SkillTreeProgress.Add(progress);
            var resolved = PassiveEffectCombatExecutionSystem.ResolveEffect(source, Skill(id), Skill(id).Effects[2]);
            Assert.That(resolved.StatusDamageMultiplier, Is.EqualTo(.52f).Within(.00001));
            Assert.That(Cast(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()), session, source, target, Certain(Skill(id))), Is.True);
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(target, ResistanceType.Poison), Is.EqualTo(.2f).Within(.00001));
        }
    }
}
