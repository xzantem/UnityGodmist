using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
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
    public class ScoutContentTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(Root+path);
        static SkillData Skill(string file)=>Load<SkillData>("Skills/SD_"+file+".asset");
        static BattleUnit Unit(bool player,int x)
        {
            var character=new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_Jerzy.asset"),Load<CharacterClassData>("Classes/CCD_Scout.asset"),1);
            character.CharacterId=System.Guid.NewGuid().ToString();
            character.Stats.MaxHealth=new Stat(5000);character.CurrentHealth=5000;
            character.Stats.MinAttack=new Stat(100);character.Stats.MaxAttack=new Stat(100);
            character.Stats.PhysicalDefense=new Stat(0);character.Stats.Dodge=new Stat(0);character.Stats.CritChance=new Stat(0);character.Stats.CritDamage=new Stat(2);
            character.Stats.WeaponAccuracy=new Stat(10000);character.WeaponAccuracy=10000;character.Resistances=new ResistanceSet();character.BaseResistances=new ResistanceSet();
            character.ResourcePool.CurrentValue=200;
            return new BattleUnit{Character=character,IsPlayerSide=player,Position=new GridPosition(x,2),CurrentActionPoints=10000,MaxActionPoints=10000,MaxKnives=5,AvailableKnives=5};
        }
        static void Rank(BattleUnit u,string tree,string id,int ranks)
        {
            var progress=u.Character.SkillTreeProgress.Find(p=>p.TreeId=="tree.scout."+tree);
            if(progress==null){progress=new SkillTreeProgress{TreeId="tree.scout."+tree};u.Character.SkillTreeProgress.Add(progress);}
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId="node.scout."+tree+"."+id,Ranks=ranks});
        }
        static CombatSession Session(params BattleUnit[] units)=>new(){Grid=new BattleGrid(12,8),Units=units.ToList(),ActiveUnit=units[0]};
        [Test] public void StarterRosterHasFiveValidDistinctSkillsAndTwoProductionTrees()
        {
            var u=Unit(true,1);Assert.That(u.Character.Skills.Count,Is.EqualTo(5));
            Assert.That(u.Character.Skills.Select(s=>s.SkillId).Distinct().Count(),Is.EqualTo(5));
            foreach(var skill in u.Character.Skills)Assert.That(CombatDataValidator.ValidateSkill(skill).Errors,Is.Empty,skill.name);
            Assert.That(u.Character.ClassData.SkillTrees.Count,Is.EqualTo(2));
            foreach(var tree in u.Character.ClassData.SkillTrees)
            {
                Assert.That(tree.Nodes.Count,Is.EqualTo(20));Assert.That(tree.Nodes.All(n=>n.Tier<=4),Is.True);
                foreach(var node in tree.Nodes)
                    foreach(var requirement in node.Requirements)Assert.That(tree.Nodes.Any(n=>n.Id==requirement.NodeId),Is.True,node.Id);
            }
        }
        [TestCase("ScoutSwap")][TestCase("ScoutSmoke")][TestCase("ScoutHook")][TestCase("ScoutAimedThrow")]
        [TestCase("ScoutRicochet")][TestCase("ScoutWeakPoint")][TestCase("ScoutShadow")]
        [TestCase("ScoutPoisonedDagger")][TestCase("ScoutQuickStrikes")][TestCase("ScoutTonic")]
        public void AuthoredSkillPassesRuntimeValidation(string name)
        {Assert.That(Skill(name),Is.Not.Null);Assert.That(CombatDataValidator.ValidateSkill(Skill(name)).Errors,Is.Empty);}
        [Test] public void TreeGrantCanBeEquippedAndDisappearsOnResetWithoutDeletingIndependentKnowledge()
        {
            var u=Unit(true,1);var skill=Skill("ScoutRicochet");
            Assert.That(u.Character.GetKnownSkills(),Has.No.Member(skill));
            Rank(u,"assassin","ricochet",1);Assert.That(u.Character.GetKnownSkills(),Has.Member(skill));
            Assert.That(u.Character.Skills,Has.No.Member(skill));
            var ids=u.Character.GetKnownSkills().Select(s=>s.SkillId).ToList();
            Assert.That(u.Character.ActiveSkillLoadout.TryInitialize(ids,"skill.knife_toss",new[]{"skill.knife_toss",skill.SkillId}),Is.True);
            Assert.That(u.Character.GetActiveSkills(),Has.Member(skill));
            u.Character.SkillTreeProgress.Clear();Assert.That(u.Character.GetActiveSkills(),Has.No.Member(skill));
            u.Character.Skills.Add(skill);Assert.That(u.Character.GetKnownSkills(),Has.Member(skill));
        }
        [TestCase(-1, -1)][TestCase(0, -1)][TestCase(1, -1)]
        [TestCase(-1, 0)][TestCase(1, 0)]
        [TestCase(-1, 1)][TestCase(0, 1)][TestCase(1, 1)]
        public void BasicMeleeUsesNoKnifeAndDealsSixtyPercent(int dx, int dy)
        {
            var u=Unit(true,4);var enemy=Unit(false,4+dx);u.AvailableKnives=0;
            u.Position=new GridPosition(4,4);enemy.Position=new GridPosition(4+dx,4+dy);
            var skill=Skill("KnifeToss");
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(u,enemy,skill);
            Assert.That(preview.Damage.Minimum,Is.EqualTo(60));
            Assert.That(preview.Damage.Maximum,Is.EqualTo(60));
            var bus=new CombatFeedbackBus();
            bus.Published+=feedback=> { if(feedback.SkillPresentation.HasValue)
                Assert.That(feedback.SkillPresentation.Value.AttackType,Is.EqualTo(AttackType.Melee)); };
            var session=Session(u,enemy);
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem(),feedbackBus:bus).TryExecute(session,u,skill,enemy),Is.True);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(4940));Assert.That(u.AvailableKnives,Is.Zero);
            Assert.That(session.MapObjects.OfType<KnifeCombatMapObject>(),Is.Empty);
            Assert.That(enemy.ScoutState.Knives,Is.Empty);
        }
        [TestCase(2,0)][TestCase(0,2)][TestCase(2,2)]
        public void BasicRangedStillRequiresKnifeBeyondAdjacentCells(int dx,int dy)
        {
            var u=Unit(true,4);var enemy=Unit(false,4+dx);u.AvailableKnives=0;
            u.Position=new GridPosition(4,4);enemy.Position=new GridPosition(4+dx,4+dy);
            var skill=Skill("KnifeToss");
            Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(u,enemy,skill).Damage.Minimum,Is.EqualTo(100));
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(Session(u,enemy),u,skill,enemy),Is.False);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(5000));
        }
        [TestCase(2, 120, 0)]
        [TestCase(4, 200, 4)]
        public void AimedThrowUsesSixtyPercentOnlyInMeleeAndKeepsGuaranteedCritical(int targetX, int damage, int knivesAfter)
        {
            var u=Unit(true,1);var enemy=Unit(false,targetX);
            if(targetX==2)u.AvailableKnives=0;
            var skill=Skill("ScoutAimedThrow");
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(u,enemy,skill);
            Assert.That(preview.Damage.Minimum,Is.EqualTo(damage));
            Assert.That(preview.Damage.Maximum,Is.EqualTo(damage));
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(Session(u,enemy),u,skill,enemy),Is.True);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(5000-damage));
            Assert.That(u.AvailableKnives,Is.EqualTo(knivesAfter));
        }
        [Test] public void AimedThrowCriticalIsScopedToThatAttack()
        {
            var u=Unit(true,1);var enemy=Unit(false,4);var session=Session(u,enemy);
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(session,u,Skill("ScoutAimedThrow"),enemy),Is.True);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(4800));
            var result=new CombatDamageSystem().ResolveAndApplyDamage(session,u,enemy,100,DamageType.Physical,wasHit:true);
            Assert.That(result.IsCritical,Is.False);
        }
        [Test] public void ChainBouncesWithinRelativeRangeAndNeverRepeatsTargets()
        {
            var u=Unit(true,1);var a=Unit(false,4);var b=Unit(false,6);var c=Unit(false,8);var far=Unit(false,11);
            var session=Session(u,a,b,c,far);var skill=Skill("ScoutRicochet");
            var targets=new CombatTargetingService().GetTargets(session,u,skill.TargetModes[0],a.Position);
            Assert.That(targets,Is.EqualTo(new[]{a,b,c}));
            Rank(u,"assassin","farsight",2);Rank(u,"assassin","knife_bounce",1);
            var mode=PassiveEffectCombatExecutionSystem.ResolveMode(u,skill,skill.TargetModes[0]);
            Assert.That(mode.AbsoluteRange.MaxRange,Is.EqualTo(7));Assert.That(mode.RelativeRange.MaxRange,Is.EqualTo(5));
            Assert.That(new CombatTargetingService().GetTargets(session,u,mode,a.Position),Is.EqualTo(new[]{a,b,c,far}));
        }
        [Test] public void QuickStrikesUpgradesDoNotMutateSharedAsset()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","series",2);Rank(u,"blade_dancer","dance_of_doom",2);Rank(u,"blade_dancer","fine_thrusts",2);
            var skill=Skill("ScoutQuickStrikes");var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(u,skill,skill.Effects[0]);
            Assert.That(effect.DamageHitCount,Is.EqualTo(3));Assert.That(effect.AdditionalHitTrials,Is.EqualTo(4));
            Assert.That(effect.BonusDamageMultiplierChance,Is.EqualTo(.55f).Within(.001));Assert.That(skill.Effects[0].AdditionalHitTrials,Is.Zero);
        }
        [Test] public void FacingTracksActionsAndBehindBonusUsesTargetsDirection()
        {
            var u=Unit(true,1);var enemy=Unit(false,2);Rank(u,"assassin","backstab",2);
            ScoutCombatSystem.Face(enemy,new GridPosition(3,2));Assert.That(ScoutCombatSystem.Behind(u,enemy),Is.True);
            var context=new PassiveEffectCombatContext(source:u,target:enemy,damageType:DamageType.Physical);
            Assert.That(ScoutCombatSystem.DamageMultiplier(context),Is.EqualTo(1.2f).Within(.001));
            ScoutCombatSystem.Face(enemy,u.Position);Assert.That(ScoutCombatSystem.Behind(u,enemy),Is.False);
        }
        [Test] public void WeakPointBenefitsTeammatesAndExpiresOnTargetsTurns()
        {
            var u=Unit(true,1);var enemy=Unit(false,2);enemy.ScoutState.WeakPointTurns=5;enemy.ScoutState.WeakPointCritDamage=.35f;
            Assert.That(ScoutCombatSystem.CriticalChance(new PassiveEffectCombatContext(source:u,target:enemy)),Is.EqualTo(.1f));
            Assert.That(ScoutCombatSystem.WeakPointCriticalDamage(enemy),Is.EqualTo(.35f));
            for(var i=0;i<5;i++)ScoutCombatSystem.TurnEnd(enemy);
            Assert.That(ScoutCombatSystem.WeakPointCriticalDamage(enemy),Is.Zero);
        }
        [Test] public void ShadowMovesOneCellAndRefusesLongerMovesBeforeChargingCost()
        {
            var u=Unit(true,1);var session=Session(u);var skill=Skill("ScoutShadow");
            var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{u}}};var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());
            Assert.That(execution.TryExecute(session,u,skill,targets,new GridPosition(4,2)),Is.False);
            Assert.That(u.CurrentActionPoints,Is.EqualTo(10000));
            Assert.That(execution.TryExecute(session,u,skill,targets,new GridPosition(2,2)),Is.True);
            Assert.That(u.Position,Is.EqualTo(new GridPosition(2,2)));Assert.That(StatusEffectSystem.IsInvisible(u),Is.True);
        }

        [Test] public void GrantedSkillSurvivesSaveLoadWithoutBecomingPermanent()
        {
            var u=Unit(true,1);u.Character.CharacterId=u.Character.Definition.Id;Rank(u,"assassin","ricochet",1);
            var skill=Skill("ScoutRicochet");
            u.Character.ActiveSkillLoadout.TryInitialize(u.Character.GetKnownSkills().Select(s=>s.SkillId),"skill.knife_toss",new[]{"skill.knife_toss",skill.SkillId});
            var game=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration,"campaign.scout.test");
            Assert.That(game.TryRegisterCharacter(u.Character),Is.True);Assert.That(game.TryAddCharacterToActiveParty(u.Character.CharacterId),Is.True);
            var snapshot=new Godmist.Systems.SaveLoad.GameSessionSaveSnapshotFactory().CreateSnapshot(game,"Scout");
            Assert.That(snapshot.Characters[0].SkillIds,Has.No.Member(skill.SkillId));
            var catalog=Load<Godmist.Data.World.GameContentCatalog>("World/GC_MainContentCatalog.asset");
            Assert.That(new Godmist.Systems.SaveLoad.GameSessionRestoreFactory().TryCreate(snapshot,catalog,out var restored),Is.True);
            var character=restored.CharactersById[u.Character.CharacterId];
            Assert.That(character.GetActiveSkills(),Has.Member(skill));Assert.That(character.Skills,Has.No.Member(skill));
            character.SkillTreeProgress.Clear();new CharacterStatPipelineSystem().Refresh(character);
            Assert.That(character.ActiveSkillLoadout.ActiveSkillIds,Has.No.Member(skill.SkillId));
            var second=new Godmist.Systems.SaveLoad.GameSessionSaveSnapshotFactory().CreateSnapshot(restored,"Reset");
            Assert.That(new Godmist.Systems.SaveLoad.GameSessionRestoreFactory().TryCreate(second,catalog,out _),Is.True);
        }
        [TestCase(false,20)] [TestCase(true,10)]
        public void HookExecutesGroundRecoveryAndPaysOnce(bool upgraded,int cost)
        {
            var u=Unit(true,1);u.AvailableKnives=4;var session=Session(u);var pos=new GridPosition(5,2);
            if(upgraded)Rank(u,"blade_dancer",ScoutCombatSystem.EfficientRecoveryNode,1);
            CombatTurnSystem.RefreshMaxActionPoints(session);u.CurrentActionPoints=u.MaxActionPoints;
            var expectedAp=u.CurrentActionPoints-CombatActionCostCalculator.CalculatePercentageCost(u.MaxActionPoints,cost);
            session.MapObjects.Add(new KnifeCombatMapObject("ground",pos,u.Character.CharacterId,1));
            var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new List<BattleUnit>()}};
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(session,u,Skill("ScoutHook"),targets,null,null,
                new Dictionary<int,GridPosition?>{{0,pos}}),Is.True);
            Assert.That(u.AvailableKnives,Is.EqualTo(5));Assert.That(u.CurrentActionPoints,Is.EqualTo(expectedAp));
            // Recovery pays the usual cost first, then grants its one flat reward.
            Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(200-cost+8));Assert.That(session.MapObjects,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public void HookEnemyUsesFullCostAndAuthoredPullChance(bool upgraded)
        {
            var u=Unit(true,1);var enemy=Unit(false,4);var skill=Skill("ScoutHook");
            if(upgraded)Rank(u,"blade_dancer",ScoutCombatSystem.EfficientRecoveryNode,1);
            Assert.That(skill.ActionCost,Is.EqualTo(25));Assert.That(skill.ResourceCost,Is.EqualTo(25));
            var pull=skill.Effects.Single(e=>e.EffectType==EffectType.Pull);
            Assert.That(pull.BaseChance,Is.EqualTo(.8f));Assert.That(pull.DisplacementDistance,Is.EqualTo(3));
            var session=Session(u,enemy);CombatTurnSystem.RefreshMaxActionPoints(session);u.CurrentActionPoints=u.MaxActionPoints;
            var before=u.CurrentActionPoints;
            var cost=CombatActionCostCalculator.CalculatePercentageCost(u.MaxActionPoints,25);
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(session,u,skill,enemy),Is.True);
            Assert.That(u.CurrentActionPoints,Is.EqualTo(before-cost));
            Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(175));
        }
        [TestCase(20,2000,true,false)]
        [TestCase(19,2000,false,false)]
        [TestCase(20,1999,false,false)]
        [TestCase(10,1000,true,true)]
        [TestCase(9,1000,false,true)]
        [TestCase(10,999,false,true)]
        public void HookRecoveryValidatesDiscountedCostsWithoutMutatingOnFailure(int pp,int ap,bool expected,bool upgraded)
        {
            var u=Unit(true,1);u.AvailableKnives=4;u.Character.ResourcePool.CurrentValue=pp;u.CurrentActionPoints=ap;
            if(upgraded)Rank(u,"blade_dancer",ScoutCombatSystem.EfficientRecoveryNode,1);
            var session=Session(u);var position=new GridPosition(5,2);
            session.MapObjects.Add(new KnifeCombatMapObject("ground",position,u.Character.CharacterId,1));
            var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new List<BattleUnit>()}};
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(session,u,Skill("ScoutHook"),targets,null,null,
                new Dictionary<int,GridPosition?>{{0,position}}),Is.EqualTo(expected));
            Assert.That(u.AvailableKnives,Is.EqualTo(expected?5:4));
            Assert.That(u.CurrentActionPoints,Is.EqualTo(expected?ap-(upgraded?1000:2000):ap));
            Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(expected?pp-(upgraded?10:20)+8:pp));
            Assert.That(session.MapObjects.Count,Is.EqualTo(expected?0:1));
        }
        [Test] public void LightStepPathPriceMatchesRealMovement()
        {
            var u=Unit(true,1);var session=Session(u);var statuses=new StatusEffectSystem();
            Rank(u,"assassin","light_step",2);statuses.ApplyStatus(u,u,StatusEffectIds.Invisibility,1,3,1,0,0,"test.invisible");
            Assert.That(CombatActionCostCalculator.CalculateMovementCost(u),Is.Zero);
            Assert.That(CombatMovementSystem.CalculatePathCost(u,4),Is.EqualTo(ActionPointUnits.PerPoint));
            var movement=new CombatMovementSystem(statusEffectSystem:statuses);
            var before=u.CurrentActionPoints;
            Assert.That(movement.TryMoveOneStep(session,u,1,0),Is.True);Assert.That(u.CurrentActionPoints,Is.EqualTo(before));
        }
        [Test] public void PoisonUpgradesAndTonicCrashValuesFollowRanks()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","venom",3);Rank(u,"blade_dancer","viruses",2);
            var poison=Skill("ScoutPoisonedDagger");
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveEffect(u,poison,poison.Effects[1]).ModifierValue,Is.EqualTo(-.3f).Within(.001));
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveEffect(u,poison,poison.Effects[2]).StatusDamageMultiplier,Is.EqualTo(.52f).Within(.001));
            Rank(u,"blade_dancer","brew_strength",1);Rank(u,"blade_dancer",ScoutCombatSystem.EfficientRecoveryNode,1);
            var tonic=Skill("ScoutTonic");var boost=PassiveEffectCombatExecutionSystem.ResolveEffect(u,tonic,tonic.Effects[0]);var crash=PassiveEffectCombatExecutionSystem.ResolveEffect(u,tonic,tonic.Effects[1]);
            Assert.That(boost.ModifierValue,Is.EqualTo(30));Assert.That(crash.ModifierValue,Is.EqualTo(-15));Assert.That(crash.DurationTurns,Is.EqualTo(3));
        }

        [Test] public void EfficientRecoveryReplacesStrongLiverAndResetRemovesOnlyItsDiscount()
        {
            var u=Unit(true,1);var hook=Skill("ScoutHook");
            var node=u.Character.ClassData.SkillTrees.Single(t=>t.Id==ScoutCombatSystem.BladeTree).Nodes
                .Single(n=>n.Id=="node.scout.blade_dancer.efficient_recovery");
            Assert.That(node.DisplayName,Is.EqualTo("Sprawny Odzysk"));
            Assert.That(node.Tier,Is.EqualTo(4));Assert.That(node.Column,Is.EqualTo(2));
            Assert.That(node.MaxRanks,Is.EqualTo(1));Assert.That(node.Requirements,Is.Empty);
            Rank(u,"blade_dancer",ScoutCombatSystem.EfficientRecoveryNode,1);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,hook,true),Is.EqualTo(10));
            Assert.That(Godmist.Views.UI.Workspace.UIWorkspaceTooltipView.BuildLoadoutSkillCost(hook,u.Character),Does.Contain("Odzysk sztyletu: 10% PA"));
            var tonic=Skill("ScoutTonic");
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveEffect(u,tonic,tonic.Effects[1]).DurationTurns,Is.EqualTo(3));
            u.Character.SkillTreeProgress.Clear();new CharacterStatPipelineSystem().Refresh(u.Character);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,hook,true),Is.EqualTo(20));
            Assert.That(hook.KnifeRecoveryCostMultiplier,Is.EqualTo(.8f));
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacyStrongLiverInvestmentSurvivesSaveLoadAsEfficientRecovery(bool includesNewId)
        {
            var u=Unit(true,1);u.Character.CharacterId=u.Character.Definition.Id;
            Rank(u,"blade_dancer","strong_liver",1);
            if(includesNewId)Rank(u,"blade_dancer",ScoutCombatSystem.EfficientRecoveryNode,1);
            var game=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration,"campaign.scout.recovery");
            Assert.That(game.TryRegisterCharacter(u.Character),Is.True);
            Assert.That(game.TryAddCharacterToActiveParty(u.Character.CharacterId),Is.True);
            var snapshot=new Godmist.Systems.SaveLoad.GameSessionSaveSnapshotFactory().CreateSnapshot(game,"Recovery");
            var catalog=Load<Godmist.Data.World.GameContentCatalog>("World/GC_MainContentCatalog.asset");
            Assert.That(new Godmist.Systems.SaveLoad.GameSessionRestoreFactory().TryCreate(snapshot,catalog,out var restored),Is.True);
            var loaded=new BattleUnit{Character=restored.CharactersById[u.Character.CharacterId]};
            Assert.That(ScoutCombatSystem.BladeRank(loaded,ScoutCombatSystem.EfficientRecoveryNode),Is.EqualTo(1));
            var investments=loaded.Character.SkillTreeProgress.Single(t=>t.TreeId==ScoutCombatSystem.BladeTree).NodeInvestments;
            Assert.That(investments.Count(n=>n.NodeId=="node.scout.blade_dancer.efficient_recovery"),Is.EqualTo(1));
            Assert.That(investments.Any(n=>n.NodeId=="node.scout.blade_dancer.strong_liver"),Is.False);
            var next=new Godmist.Systems.SaveLoad.GameSessionSaveSnapshotFactory().CreateSnapshot(restored,"Migrated");
            var saved=next.Characters.Single(c=>c.CharacterId==u.Character.CharacterId).SkillTreeProgress
                .Single(t=>t.TreeId==ScoutCombatSystem.BladeTree).NodeInvestments;
            Assert.That(saved.Any(n=>n.NodeId=="node.scout.blade_dancer.strong_liver"),Is.False);
            Assert.That(saved.Single(n=>n.NodeId=="node.scout.blade_dancer.efficient_recovery").Ranks,Is.EqualTo(1));
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(loaded,Skill("ScoutHook"),true),Is.EqualTo(10));
        }

        [Test] public void TonicBoostIsImmediatelyFollowedByCrashAfterFiveTurnEnds()
        {
            var u=Unit(true,1);var session=Session(u);var statuses=new StatusEffectSystem();
            // Isolate tonic modifiers from the independent high-Momentum bonus.
            u.Character.ResourcePool.CurrentValue=100;new CharacterStatPipelineSystem().Refresh(u.Character);
            var execution=new SkillExecutionSystem(statusEffectSystem:statuses);var speed=u.Character.Stats.Speed.GetValue();
            Assert.That(execution.TryExecute(session,u,Skill("ScoutTonic"),u),Is.True);
            Assert.That(u.Character.Stats.Speed.GetValue(),Is.EqualTo(speed+20));
            for(var i=0;i<5;i++){statuses.ProcessTurnEnd(u);execution.ProcessScheduledEffects(session,u);}
            Assert.That(u.Character.Stats.Speed.GetValue(),Is.EqualTo(speed-10));
            for(var i=0;i<3;i++){statuses.ProcessTurnEnd(u);execution.ProcessScheduledEffects(session,u);}
            Assert.That(u.Character.Stats.Speed.GetValue(),Is.EqualTo(speed));
        }
        [Test] public void SpareKnivesDoNotStackOnRepeatedCombatInitialization()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","spare_daggers",2);var session=Session(u);
            ScoutKnifeSystem.ResetForCombat(session);Assert.That(u.MaxKnives,Is.EqualTo(7));
            ScoutKnifeSystem.ResetForCombat(session);Assert.That(u.MaxKnives,Is.EqualTo(7));
            u.Character.SkillTreeProgress.Clear();ScoutKnifeSystem.ResetForCombat(session);Assert.That(u.MaxKnives,Is.EqualTo(5));
        }
        [Test] public void ThroatCutRespectsExecuteThresholdAndBossFallback()
        {
            var u=Unit(true,1);Rank(u,"assassin","throat_cut",3);var enemy=Unit(false,2);enemy.Character.CurrentHealth=400;
            var context=new PassiveEffectCombatContext(source:u,target:enemy,attackType:AttackType.Melee,damageType:DamageType.Physical);
            Assert.That(ScoutCombatSystem.ResolveThroatCut(Session(u,enemy),context,100,()=>0),Is.EqualTo(500));
            enemy.Character.CurrentHealth=1000;
            Assert.That(ScoutCombatSystem.ResolveThroatCut(Session(u,enemy),context,100,()=>0),Is.EqualTo(100));
            enemy.IsBoss=true;
            Assert.That(ScoutCombatSystem.ResolveThroatCut(Session(u,enemy),context,100,()=>0),Is.EqualTo(200));
            Assert.That(ScoutCombatSystem.ResolveThroatCut(Session(u,enemy),context,100,()=>.99f),Is.EqualTo(100));
        }

        [Test] public void AssassinGlobalCriticalBonusIsVisibleInCharacterStats()
        {
            var u=Unit(true,1);Rank(u,"assassin","ricochet",1);new CharacterStatPipelineSystem().Refresh(u.Character);
            Assert.That(u.Character.Stats.CritChance.GetValue(),Is.EqualTo(.002f).Within(.00001));
        }
    }
}
