using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        sealed partial class Engine
        {
            float NextPlayerTurn(BattleUnit unit)=>unit==Session.ActiveUnit?CombatTurnSystem.CalculateBaseActionValue(unit):unit.CurrentActionValue;
            bool Mage(BattleUnit unit)=>unit.Character.ClassData.Id=="class.mage";
            int ForecastPoints(BattleUnit unit)=>Mathf.RoundToInt(turns.CalculateMaxActionPoints(unit.Character,Session.ActionPointsPerSpeed)
                *(1+EquipmentEffectSystem.Sum(unit.Character,GalduryteEffectType.ActionPointsPercent)));
            // Read-only estimate: no BeforeBurnTick call (it can roll RNG/spread fire).
            internal float NextPlayerDot(BattleUnit unit)
            {
                float total=0;
                foreach(var effect in unit.Character.PassiveEffects.Where(PassiveEffectLifecycleSystem.IsActive))
                {
                    var payload=effect.GetPayload<DamageOverTimePayload>();
                    if(payload==null||effect.TickPhase!=PassiveEffectTickPhase.TurnStart)continue;
                    var raw=Mathf.CeilToInt(payload.RemainingTotalDamage/(float)Math.Max(1,effect.RemainingDuration));
                    if(effect.EffectId=="burn")
                    {
                        var source=MageCombatSystem.BurnSource(Session,effect);
                        if(raw<unit.Character.Stats.MaxHealth.GetValue()*.1f)raw=Mathf.RoundToInt(raw*(1+.15f*MageCombatSystem.F(source,"sanguine")));
                        if(MageCombatSystem.F(unit,"fire_resistance")>0)
                            raw=Mathf.RoundToInt(raw*(effect.GetStateValue("mage.burn.self")?.BooleanValue==true?.5f:.75f));
                    }
                    total+=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(unit,effect.EffectId,raw);
                }
                return total;
            }
            float EnemyContact(BattleUnit enemy,BattleUnit ally,GridPosition cell)
            {
                var basic=enemy.Character.GetBasicAttackSkill();
                if(basic==null||StatusEffectSystem.IsHardCrowdControlled(enemy))return 0;
                if(!basic.TargetModes.Any(m=>CombatTargetingService.IsPositionWithinRange(enemy.Position,cell,m.AbsoluteRange)))return 0;
                if(basic.RequiresLineOfSight&&!sight.HasLineOfSight(Session,enemy.Position,cell))return 0;
                var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(enemy,ally,basic);
                if(!preview.Damage.HasDamage)return 0;
                var expected=(preview.Damage.Minimum+preview.Damage.Maximum)*.5f*(preview.HasHitChance?preview.HitChance:1);
                expected*=1+Mathf.Clamp01(enemy.Character.Stats.CritChance.GetValue())*Math.Max(0,enemy.Character.Stats.CritDamage.GetValue()-1);
                var cost=Math.Max(1,CombatActionCostCalculator.CalculateSkillCost(enemy,basic));
                return expected*Mathf.Clamp(ForecastPoints(enemy)/cost,1,2);
            }
            float PlayerIncoming(BattleUnit ally,GridPosition cell)
                =>VisiblePlayerOpponents(ally).Where(e=>NextPlayerTurn(e)<=NextPlayerTurn(ally)
                    +(MageRecoveryPolicy&&Mage(ally)&&StatusEffectSystem.IsHardCrowdControlled(ally)?CombatTurnSystem.CalculateBaseActionValue(ally):0))
                    .Sum(e=>EnemyContact(e,ally,cell));
            internal bool PlayerNeedsProtection(BattleUnit ally)
                =>ally.Character.LifeState==CharacterLifeState.Alive
                    &&NextPlayerDot(ally)+PlayerIncoming(ally,ally.Position)>=ally.Character.CurrentHealth+ally.Character.ShieldPool.TotalShield;
            bool CriticalPatient(BattleUnit ally)=>ally.Character.LifeState==CharacterLifeState.Downed
                ||ally.Character.LifeState==CharacterLifeState.Alive&&(ally.Character.CurrentHealth<ally.Character.Stats.MaxHealth.GetValue()*.3f
                    ||NextPlayerDot(ally)>=ally.Character.CurrentHealth+ally.Character.ShieldPool.TotalShield
                    ||Mage(ally)&&(MageRecoveryPolicy||ally.Character.CurrentHealth<ally.Character.Stats.MaxHealth.GetValue()*.5f)&&PlayerNeedsProtection(ally));

            // Non-critical, immediate restoration only. Delayed regeneration is
            // valuable for maintenance but cannot save a victim from an earlier tick.
            float ImmediateRecovery(BattleUnit actor,BattleUnit ally,SkillData skill,int mode)
            {
                float result=0;var max=ally.Character.Stats.MaxHealth.GetValue();
                var attack=(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.5f;
                foreach(var original in skill.Effects.Where(e=>e.TargetModeIndex==mode))
                {
                    var e=PassiveEffectCombatExecutionSystem.ResolveEffect(actor,skill,original);
                    switch(e.EffectType)
                    {
                        case EffectType.Heal:result+=CombatRecoverySystem.CalculateSkillHealing(actor,e);break;
                        case EffectType.HealPercent:result+=max*e.PercentageValue;break;
                        case EffectType.HealMissingHealthPercent:result+=(max-ally.Character.CurrentHealth)*e.PercentageValue;break;
                        case EffectType.Shield:
                            if(ally.Character.LifeState==CharacterLifeState.Alive&&ally.Character.ShieldPool.TotalShield==0)
                                result+=Math.Max(0,e.BaseValue)+attack*e.ShieldAverageAttackMultiplier;
                            break;
                        case EffectType.PaladinHeal:
                            var amount=attack*(1+.01f*PaladinCombatSystem.Points(actor.Character,PaladinCombatSystem.Priest))*e.DamageMultiplier;
                            if(skill.SkillId is "skill.paladin.heal" or "skill.paladin.group_heal")amount*=1+.15f*PaladinCombatSystem.P(actor,"healing_power");
                            if(skill.SkillId=="skill.paladin.heal")amount*=1+.15f*PaladinCombatSystem.P(actor,"healer_focus");
                            result+=amount;break;
                        case EffectType.PaladinRegeneration:
                            var front=PaladinCombatSystem.P(actor,"haste") switch{1=>.15f,2=>.25f,3=>.4f,_=>0};
                            result+=max*.4f*front;break;
                    }
                }
                return result;
            }
            bool SupportPlan(BattleUnit actor,BattleUnit ally,SkillData skill,out Dictionary<int,IReadOnlyList<BattleUnit>> targets)
            {
                targets=new();
                for(var m=0;m<skill.TargetModes.Count;m++)
                {
                    var mode=PassiveEffectCombatExecutionSystem.ResolveMode(actor,skill,skill.TargetModes[m]);
                    targets[m]=targeting.GetTargets(Session,actor,mode,ally.Position,true)
                        .Where(u=>!skill.RequiresLineOfSight||sight.HasLineOfSight(Session,actor.Position,u.Position)).ToArray();
                }
                return targets.Count>0&&targets.Values.All(t=>t.Count>0)&&targets.Values.Any(t=>t.Contains(ally))
                    &&skills.CanPreview(Session,actor,skill,targets,null,null,null);
            }
            float SupportPriority(BattleUnit actor,BattleUnit ally,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets)
            {
                if(ally.Character.LifeState==CharacterLifeState.Dead)return 0;
                var regen=skill.Effects.Any(e=>e.EffectType==EffectType.PaladinRegeneration);
                var shield=skill.Effects.Any(e=>e.EffectType==EffectType.Shield);
                if(BuildAware&&shield)return BuildShieldPriority(actor,ally,skill,targets,true);
                var down=ally.Character.LifeState==CharacterLifeState.Downed;
                if(shield&&(down||ally.Character.ShieldPool.TotalShield>0))return 0;
                if(regen&&(down||ally.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration"&&PassiveEffectLifecycleSystem.IsActive(e))))return 0;
                var max=ally.Character.Stats.MaxHealth.GetValue();var missing=Math.Max(0,max-ally.Character.CurrentHealth);
                var risk=NextPlayerDot(ally)+PlayerIncoming(ally,ally.Position);
                var urgent=CriticalPatient(ally);
                // Ordinary maintenance keeps the proven regeneration-first
                // policy; reserve this comparison for concrete critical patients.
                if(!urgent)return 0;
                if(!down&&!urgent&&missing<max*.25f)return 0;
                float immediate=0;
                foreach(var group in targets)if(group.Value.Contains(ally))immediate+=ImmediateRecovery(actor,ally,skill,group.Key);
                if(down&&immediate<=0)return 0;
                if(urgent&&regen&&immediate<risk-ally.Character.CurrentHealth)return 0;
                var benefit=Math.Min(missing,immediate)+(regen&&!urgent?Math.Min(missing,max*.4f)*.6f:0);
                if(shield)benefit=Math.Min(immediate,Math.Max(risk,max*.15f));
                if(benefit<=0)return 0;
                var survival=immediate+ally.Character.CurrentHealth+ally.Character.ShieldPool.TotalShield>risk;
                return benefit*(urgent?4:1)*(Mage(ally)?1.35f:1)
                    +(down?max*(RescueDeadline(ally)<=CombatTurnSystem.CalculateBaseActionValue(actor)?30:5):0)+(urgent&&survival?max:0);
            }
            bool TacticalSupport(BattleUnit actor,bool downedOnly=false,bool allowMove=true)
            {
                var plans=new List<(SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets,float score)>();
                foreach(var skill in actor.Character.GetActiveSkills().Where(IsSupport))
                    foreach(var ally in Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState!=CharacterLifeState.Dead
                        &&(!downedOnly||u.Character.LifeState==CharacterLifeState.Downed)))
                        if(SupportPlan(actor,ally,skill,out var targets))
                        {
                            var value=BudgetSupport?GroupSupportPriority(actor,skill,targets):SupportPriority(actor,ally,skill,targets);
                            var cost=BudgetSupport?SupportOpportunityCost(actor,skill,targets):Math.Max(20,CombatActionCostCalculator.CalculateSkillCost(actor,skill));
                            if(value>0)plans.Add((skill,targets,value/cost));
                        }
                foreach(var p in plans.OrderByDescending(p=>p.score))if(skills.TryExecute(Session,actor,p.skill,p.targets))return true;
                if(!allowMove)return false;
                // Move only when there is a legal affordable cast after the move.
                var endangered=Session.Units.Where(u=>u!=actor&&u.IsPlayerSide==actor.IsPlayerSide
                    &&CriticalPatient(u)).ToArray();
                if(endangered.Length==0||!actor.Character.GetActiveSkills().Any(IsSupport))return false;
                var danger=DangerousCells();var start=actor.Position;var points=actor.CurrentActionPoints;
                foreach(var path in movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains))
                    .OrderBy(p=>endangered.Min(u=>PlayerDistance(p.Key,u.Position))).ThenBy(p=>PlayerPathCost(actor,p.Value)))
                {
                    var cost=PlayerPathCost(actor,path.Value);bool useful=false;
                    try
                    {
                        actor.Position=path.Key;actor.CurrentActionPoints=points-cost;
                        useful=actor.CurrentActionPoints>=0&&actor.Character.GetActiveSkills().Where(IsSupport).Any(s=>endangered.Any(a=>
                            SupportPlan(actor,a,s,out var targets)&&SupportPriority(actor,a,s,targets)>0));
                    }
                    finally{actor.Position=start;actor.CurrentActionPoints=points;}
                    if(useful)return movement.TryMoveAlongPath(Session,actor,path.Value);
                }
                return false;
            }
            internal bool SafePlayerHelpUp(BattleUnit helper,BattleUnit fallen)
            {
                if(CoordinatedPolicy)return SafeCoordinatedHelpUp(helper,fallen);
                var dot=NextPlayerDot(fallen);
                // A legal enemy shot is a possibility, not a promise that its AI
                // will select this victim. Hard deferral uses the visible DoT;
                // potential focus fire stays a soft protection/movement estimate.
                if(dot<1)return true;
                var firstThreat=NextPlayerTurn(fallen);
                foreach(var healer in Session.Units.Where(u=>u!=helper&&u!=fallen&&u.IsPlayerSide==helper.IsPlayerSide
                    &&u.Character.LifeState==CharacterLifeState.Alive&&!StatusEffectSystem.IsHardCrowdControlled(u)&&NextPlayerTurn(u)<firstThreat))
                {
                    var hp=fallen.Character.CurrentHealth;var lifeState=fallen.Character.LifeState;var ap=healer.CurrentActionPoints;var active=Session.ActiveUnit;
                    try
                    {
                        // Preview the proposed state; do not heal, spend or advance AV.
                        fallen.Character.LifeState=CharacterLifeState.Alive;fallen.Character.CurrentHealth=1;
                        healer.CurrentActionPoints=healer==Session.ActiveUnit?ap:ForecastPoints(healer);
                        Session.ActiveUnit=healer;
                        foreach(var skill in healer.Character.GetActiveSkills().Where(IsSupport))
                            if(SupportPlan(healer,fallen,skill,out var targets))
                            {
                                var gain=targets.Where(p=>p.Value.Contains(fallen)).Sum(p=>ImmediateRecovery(healer,fallen,skill,p.Key));
                                if(1+gain>dot)return true;
                            }
                    }
                    finally{fallen.Character.CurrentHealth=hp;fallen.Character.LifeState=lifeState;healer.CurrentActionPoints=ap;Session.ActiveUnit=active;}
                }
                return false;
            }
            internal bool CanWaitForRecovery(BattleUnit fallen)
            {
                // Conservative forecast: require an affordable heal before death;
                // this policy does not predict future downed-status expiration.
                foreach(var healer in Session.Units.Where(u=>u!=fallen&&u.IsPlayerSide==fallen.IsPlayerSide
                    &&u.Character.LifeState==CharacterLifeState.Alive&&!StatusEffectSystem.IsHardCrowdControlled(u)
                    &&NextPlayerTurn(u)<RescueDeadline(fallen)))
                {
                    var ap=healer.CurrentActionPoints;var active=Session.ActiveUnit;
                    try
                    {
                        healer.CurrentActionPoints=ForecastPoints(healer);Session.ActiveUnit=healer;
                        foreach(var skill in healer.Character.GetActiveSkills().Where(IsSupport))
                            if(SupportPlan(healer,fallen,skill,out var targets)
                                &&targets.Where(p=>p.Value.Contains(fallen)).Sum(p=>ImmediateRecovery(healer,fallen,skill,p.Key))>NextPlayerDot(fallen))return true;
                    }
                    finally{healer.CurrentActionPoints=ap;Session.ActiveUnit=active;}
                }
                return false;
            }
            bool TrySafeRescue(BattleUnit actor)
            {
                foreach(var fallen in Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Downed)
                    .OrderBy(RescueDeadline).ThenByDescending(Mage))
                {
                    var emergency=fallen.DownedTurnsRemaining<=1||RescueDeadline(fallen)<=CombatTurnSystem.CalculateBaseActionValue(actor)
                        ||Session.Units.Count(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive)==1;
                    if(TeamPolicy&&DeferPlayerRescue(actor,fallen)){Result.rescuesDeferred++;continue;}
                    var safe=SafePlayerHelpUp(actor,fallen);
                    var defer=!safe&&!emergency&&(CoordinatedPolicy||CanWaitForRecovery(fallen));
                    if(defer)Result.rescuesDeferred++;
                    if(!defer&&new CombatReviveSystem().TryRevive(Session,actor,fallen))
                    {
                        knockdownTrace?.ManualRevive(actor,fallen);Result.heroRevives++;if(!safe)Result.riskRescues++;
                        helpedUp.Add(fallen.Character.CharacterId);endPlayerTurn=true;return true;
                    }
                    // Staging is separate from standing up. Arrive in time even
                    // if the current 1HP revive is unsafe; remain nearby afterward.
                    if(Math.Max(Math.Abs(actor.Position.X-fallen.Position.X),Math.Abs(actor.Position.Y-fallen.Position.Y))<=1)continue;
                    if(defer&&!DesignatedRescuer(actor,fallen))continue;
                    var danger=DangerousCells();var before=PlayerDistance(actor.Position,fallen.Position);
                    var path=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&PlayerDistance(p.Key,fallen.Position)<before&&!p.Value.Any(danger.Contains)
                        &&(!CoordinatedPolicy||emergency||RescueDeadline(fallen)<=2*CombatTurnSystem.CalculateBaseActionValue(actor)||SafeCoordinatedSetup(actor,p.Key)))
                        .OrderBy(p=>PlayerDistance(p.Key,fallen.Position)).ThenBy(p=>PlayerPathCost(actor,p.Value)).Select(p=>p.Value).FirstOrDefault();
                    if(path!=null&&movement.TryMoveAlongPath(Session,actor,path))return true;
                }
                return false;
            }
            internal float RescueDeadline(BattleUnit fallen)=>Math.Max(0,fallen.CurrentActionValue)
                +Math.Max(0,fallen.DownedTurnsRemaining-1)*CombatTurnSystem.CalculateBaseActionValue(fallen);
            bool DesignatedRescuer(BattleUnit actor,BattleUnit fallen)=>Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide
                &&u.Character.LifeState==CharacterLifeState.Alive&&!StatusEffectSystem.IsHardCrowdControlled(u))
                .OrderBy(u=>PlayerDistance(u.Position,fallen.Position)+(u.MaxKnives>0&&u.AvailableKnives==0?.5f:0)
                    +(CoordinatedPolicy&&PlayerNeedsProtection(u)?4:0))
                .ThenBy(u=>u.Character.CharacterId,StringComparer.Ordinal).FirstOrDefault()==actor;
            bool NeedsUrgentRescueApproach(BattleUnit actor)=>Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide
                &&u.Character.LifeState==CharacterLifeState.Downed&&DesignatedRescuer(actor,u)
                &&(!TeamPolicy||!DeferPlayerRescue(actor,u))
                &&PlayerDistance(actor.Position,u.Position)>2&&RescueDeadline(u)<=2*CombatTurnSystem.CalculateBaseActionValue(actor));
            bool HoldRescuePosition(BattleUnit actor)=>Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide
                &&u.Character.LifeState==CharacterLifeState.Downed&&DesignatedRescuer(actor,u)
                &&(!TeamPolicy||!DeferPlayerRescue(actor,u))
                &&Math.Max(Math.Abs(u.Position.X-actor.Position.X),Math.Abs(u.Position.Y-actor.Position.Y))<=1);
            bool TryProtectMagePosition(BattleUnit actor)
            {
                if(TeamPolicy||!Mage(actor)||playerRepositioned)return false;
                if(actor.Character.CurrentHealth>=actor.Character.Stats.MaxHealth.GetValue()*.5f)return false;
                var current=PlayerIncoming(actor,actor.Position);
                if(current<=0||!PlayerNeedsProtection(actor))return false;
                var danger=DangerousCells();var opponents=VisiblePlayerOpponents(actor).ToArray();
                var allies=Session.Units.Where(u=>u!=actor&&u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive).ToArray();
                var cohesion=allies.Length==0?float.MaxValue:Math.Max(3,allies.Min(u=>PlayerDistance(actor.Position,u.Position)));
                var paths=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains))
                    .Select(p=>(cell:p.Key,path:p.Value,cost:PlayerPathCost(actor,p.Value)))
                    .Where(p=>p.cost<=actor.CurrentActionPoints&&(allies.Length==0||allies.Any(u=>PlayerDistance(p.cell,u.Position)<=cohesion)))
                    .OrderByDescending(p=>opponents.Min(u=>PlayerDistance(p.cell,u.Position))).ThenBy(p=>p.cost).Take(20)
                    .Select(p=>(p.cell,p.path,p.cost,risk:PlayerIncoming(actor,p.cell)))
                    .Where(p=>p.risk<current*.65f).OrderBy(p=>p.risk).ThenBy(p=>p.cost);
                foreach(var p in paths)
                    if(movement.TryMoveAlongPath(Session,actor,p.path))
                    {playerRepositioned=true;Result.mageRetreats++;return true;}
                return false;
            }
            float MageBurnRiskPenalty(BattleUnit actor,SkillData skill,int mode,IReadOnlyList<BattleUnit> targets)
            {
                if(!SmartRescue||!Mage(actor)||MageCombatSystem.F(actor,"fire_offering")==0)return 0;
                var rank=MageCombatSystem.F(actor,"fire_offering");var chance=rank==1?.15f:rank==2?.3f:.5f;
                float expected=0;
                foreach(var original in skill.Effects.Where(e=>e.TargetModeIndex==mode&&e.EffectType==EffectType.ApplyStatus&&e.StatusId=="burn"))
                {
                    var e=PassiveEffectCombatExecutionSystem.ResolveEffect(actor,skill,original);
                    var raw=CombatDamageValueCalculator.CalculateDamageValue(actor,DamageValueSource.AverageAttack,e.StatusDamageMultiplier)
                        *(1+.02f*MageCombatSystem.Points(actor.Character,MageCombatSystem.Fire))*(1+.1f*MageCombatSystem.F(actor,"arcana"));
                    if(MageCombatSystem.F(actor,"fire_resistance")>0)raw*=.5f;
                    var tick=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(actor,"burn",Mathf.RoundToInt(raw));
                    expected+=targets.Where(t=>t.IsPlayerSide!=actor.IsPlayerSide).Sum(t=>tick*chance*StatusEffectSystem.CalculateStatusApplicationChance(actor,t,"burn",e.BaseChance));
                }
                var budget=actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield-NextPlayerDot(actor);
                return expected>0&&expected>budget?expected*2:0;
            }
        }
    }
}
