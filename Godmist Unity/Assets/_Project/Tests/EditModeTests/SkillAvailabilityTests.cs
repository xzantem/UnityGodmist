using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Constants;
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
    public sealed class SkillAvailabilityTests
    {
        private Random.State random;
        [SetUp] public void Setup() { random=Random.state; Random.InitState(7); }
        [TearDown] public void Cleanup() => Random.state=random;
        private static SkillData Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/_Project/ScriptableObjects/Skills/SD_{name}.asset");
        private static BattleUnit Unit(string name="Wladimir_Test")
        {
            var character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"Assets/_Project/ScriptableObjects/Characters/CD_{name}.asset"));
            character.SkillTreeProgress.Clear();
            return new BattleUnit { Character=character, IsPlayerSide=true, Position=new GridPosition(1,1), MaxActionPoints=2000, CurrentActionPoints=2000 };
        }
        private static CombatSession Session(BattleUnit actor) => new() {ActiveUnit=actor,Units=new(){actor},Grid=new BattleGrid(12,12)};
        private static void Status(BattleUnit unit,string id)
        {
            unit.Character.Resistances.SuppressionResistance=0;
            Assert.That(new StatusEffectSystem().ApplyStatus(unit,unit,id,1,3,1,0,0,"availability test"),Is.True);
        }

        [TestCase(StatusEffectIds.Stun)] [TestCase(StatusEffectIds.Freeze)]
        [TestCase(StatusEffectIds.Sleep)] [TestCase(StatusEffectIds.Paralysis)]
        public void CrowdControlBlocksSelectionAndActualPreviewWithoutMutatingState(string status)
        {
            var unit=Unit();var session=Session(unit);var skill=Skill("WarriorCut");
            Status(unit,status);var ap=unit.CurrentActionPoints;var effects=unit.Character.PassiveEffects.Count;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).Reason,Is.EqualTo(SkillUseBlockReason.CrowdControlled));
            Assert.That(new SkillExecutionSystem().CanPreview(session,unit,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>(),null,null,null),Is.False);
            Assert.That(unit.CurrentActionPoints,Is.EqualTo(ap)); Assert.That(unit.Character.PassiveEffects.Count,Is.EqualTo(effects));
        }

        [Test]
        public void ImmobilizationBlocksMobilityButNotOrdinaryAttackSelectionOrApproachableTargets()
        {
            var unit=Unit("Jerzy");var session=Session(unit);unit.Character.ResourcePool.CurrentValue=unit.Character.ResourcePool.MaxValue;
            Status(unit,StatusEffectIds.Immobilize);
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,Skill("ScoutShadow")).Reason,Is.EqualTo(SkillUseBlockReason.Immobilized));
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,unit.Character.GetBasicAttackSkill()).CanSelect,Is.True);
        }

        [Test]
        public void ActiveToggleCanBeDisabledWithoutResourcesButStillObeysCrowdControl()
        {
            var unit=Unit();var session=Session(unit);var skill=Skill("WarriorDeathWish");
            unit.ActiveToggleSkillIds.Add(skill.SkillId);unit.ToggleSkillIdsUsedThisTurn.Add(skill.SkillId);
            unit.CurrentActionPoints=0;unit.Character.ResourcePool.CurrentValue=0;
            var result=SkillExecutionSystem.GetSelectionAvailability(session,unit,skill);
            Assert.That(result.CanSelect,Is.True);Assert.That(result.ActionCost,Is.Zero);
            Assert.That(new SkillExecutionSystem().CanPreview(session,unit,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>(),null,null,null),Is.True);
            Status(unit,StatusEffectIds.Stun);
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).CanSelect,Is.False);
        }

        [Test]
        public void RequiredCasterStatusUsesTheSameRuleAsExecution()
        {
            var unit=Unit();var session=Session(unit);var skill=Object.Instantiate(Skill("WarriorCut"));
            try
            {
                typeof(SkillData).GetField("<RequiredCasterStatusId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(skill,"availability.required");
                Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).Reason,Is.EqualTo(SkillUseBlockReason.RequiredStatus));
                Assert.That(SkillExecutionSystem.GetCasterBlockReason(session,unit,skill,false),Is.EqualTo(SkillUseBlockReason.RequiredStatus));
            }
            finally {Object.DestroyImmediate(skill);}
        }

        [Test]
        public void ResourcesActionPointsEnemyTurnAndBusyStateRefreshWithoutChangingLoadout()
        {
            var unit=Unit();var session=Session(unit);var skill=Skill("WarriorDischarge");
            unit.Character.ResourcePool.CurrentValue=0;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).Reason,Is.EqualTo(SkillUseBlockReason.WarriorRequirement));
            unit.Character.ResourcePool.CurrentValue=40;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).CanSelect,Is.True);
            unit.CurrentActionPoints=0;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).Reason,Is.EqualTo(SkillUseBlockReason.NotEnoughActionPoints));
            unit.CurrentActionPoints=2000;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill,true).Reason,Is.EqualTo(SkillUseBlockReason.ActionInProgress));
            unit.IsPlayerSide=false;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,skill).Reason,Is.EqualTo(SkillUseBlockReason.EnemyTurn));
        }

        [Test]
        public void HealingDiscountCanKeepHealSelectableAtZeroActionPoints()
        {
            var unit=Unit("Paladin");var session=Session(unit);var ally=Unit();ally.Position=new GridPosition(2,1);session.Units.Add(ally);
            unit.Character.ResourcePool.CurrentValue=unit.Character.ResourcePool.MaxValue;
            unit.Character.SkillTreeProgress.Add(new SkillTreeProgress {TreeId=PaladinCombatSystem.Priest,
                NodeInvestments=new(){new SkillTreeNodeInvestment {NodeId="node.paladin.priest.persistent_healer",Ranks=3}}});
            ally.PaladinState.HealingDiscount=true;unit.CurrentActionPoints=0;
            var result=SkillExecutionSystem.GetSelectionAvailability(session,unit,Skill("PaladinHeal"));
            Assert.That(result.CanSelect,Is.True);Assert.That(result.ActionCost,Is.Zero);
            Assert.That(ally.PaladinState.HealingDiscount,Is.True);
        }

        [Test]
        public void KnifeShortageKeepsMeleeUseButBlocksARequiredThrow()
        {
            var unit=Unit("Jerzy");var session=Session(unit);
            unit.Character.ResourcePool.CurrentValue=unit.Character.ResourcePool.MaxValue;
            unit.AvailableKnives=0;
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,Skill("ScoutRicochet")).Reason,Is.EqualTo(SkillUseBlockReason.NotEnoughKnives));
            var basic=Skill("ScoutAimedThrow");
            Assert.That(basic.FreeKnivesInMelee,Is.True);
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,basic).CanSelect,Is.True);
        }

        [Test]
        public void CheaperKnifeRecoveryIsASelectableAlternativeAndQueryDoesNotRecoverIt()
        {
            var unit=Unit("Jerzy");var session=Session(unit);var skill=Skill("ScoutHook");
            unit.Character.ResourcePool.CurrentValue=unit.Character.ResourcePool.MaxValue;
            unit.MaxKnives=5;unit.AvailableKnives=3;
            unit.Character.SkillTreeProgress.Add(new SkillTreeProgress {TreeId=ScoutCombatSystem.BladeTree,
                NodeInvestments=new(){new SkillTreeNodeInvestment {NodeId="node.scout.blade_dancer."+ScoutCombatSystem.EfficientRecoveryNode,Ranks=1}}});
            var knife=new KnifeCombatMapObject("availability.knife",new GridPosition(2,1),unit.Character.CharacterId,1);
            session.MapObjects.Add(knife);
            var recoveryCost=CombatActionCostCalculator.CalculateSkillCost(unit,skill,true);
            Assert.That(recoveryCost,Is.LessThan(CombatActionCostCalculator.CalculateSkillCost(unit,skill)));
            unit.CurrentActionPoints=recoveryCost;
            var result=SkillExecutionSystem.GetSelectionAvailability(session,unit,skill);
            Assert.That(result.CanSelect,Is.True);Assert.That(result.ActionCost,Is.EqualTo(recoveryCost));
            Assert.That(session.MapObjects,Does.Contain(knife));Assert.That(unit.AvailableKnives,Is.EqualTo(3));
        }

        [Test]
        public void OutOfRangeOrNotYetChosenTargetDoesNotDisableSkillSelection()
        {
            var unit=Unit();var session=Session(unit);
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(session,unit,Skill("WarriorCut")).CanSelect,Is.True);
            Assert.That(SkillExecutionSystem.GetSelectionAvailability(null,unit,Skill("WarriorCut")).CanSelect,Is.True);
        }
    }
}
