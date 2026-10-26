using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        sealed partial class Engine
        {
            internal bool UsePreviousCoordinationPolicy;
            bool CoordinatedPolicy=>MageRecoveryPolicy&&!UsePreviousCoordinationPolicy;
            PlayerPlan committedTeamAttack;
            BattleUnit committedTeamActor;
            int committedTeamTurn;

            internal bool SafeCoordinatedSetup(BattleUnit actor,GridPosition cell)
            {
                if(!CoordinatedPolicy||!MageMovementPolicy(actor)&&!TeamHealer(actor))return SafeMageApproach(actor,cell);
                var risk=PlayerIncoming(actor,cell);
                return risk<PlayerIncoming(actor,actor.Position)*.65f
                    ||risk*1.2f+NextPlayerDot(actor)<actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield;
            }

            float CoordinatedFocusPriority(BattleUnit enemy,float ordinary)
            {
                if(!CoordinatedPolicy)return ordinary;
                // Estimate shared access, not just the nearest hero. Real paths,
                // costs, LOS and legal targets still decide every executed action.
                var approach=Session.Units.Where(u=>u.IsPlayerSide!=enemy.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive)
                    .Select(u=>Math.Max(0,PlayerDistance(u.Position,enemy.Position)-
                        (u.Character.GetBasicAttackSkill()?.TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies)
                         .Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(u,u.Character.GetBasicAttackSkill(),m).AbsoluteRange.MaxRange)
                         .DefaultIfEmpty(1).Max()??1))).OrderBy(d=>d).Take(2).ToArray();
                return ordinary*(1+.15f*(approach.Length==0?0:(float)approach.Average()));
            }

            bool TryCommittedTeamAttack(BattleUnit actor)
            {
                var plan=committedTeamAttack;committedTeamAttack=null;
                if(!CoordinatedPolicy||plan==null||committedTeamActor!=actor||committedTeamTurn!=Result.turns)return false;
                if(Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Downed&&RescueEmergency(actor,u)))return false;
                if(!skills.TryExecute(Session,actor,plan.skill,plan.targets,null,plan.objectPosition,plan.positions))return false;
                AuditChosenPlan?.Invoke(actor,plan.skill,plan.targets.Values.SelectMany(t=>t).Distinct().ToArray(),plan.score);
                if(CombatActionCostCalculator.CalculateSkillCost(actor,plan.skill)==0)zeroCostUsed.Add(plan.skill);
                return true;
            }

            bool TryCoordinatedFocusAttack(BattleUnit actor,List<PlayerPlan> current)
            {
                if(!CoordinatedPolicy||playerRepositioned||playerFocus==null)return false;
                bool Focus(PlayerPlan p)=>p.targets.Values.SelectMany(t=>t).Contains(playerFocus)
                    &&p.skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage);
                var bestCurrent=current.OrderByDescending(p=>p.score).FirstOrDefault();
                if(bestCurrent!=null&&Focus(bestCurrent))return false;
                var danger=DangerousCells();var start=actor.Position;var points=actor.CurrentActionPoints;
                List<GridPosition> selected=null;PlayerPlan attack=null;var best=bestCurrent?.score??0;
                var range=actor.Character.GetBasicAttackSkill()?.TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies)
                    .Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(actor,actor.Character.GetBasicAttackSkill(),m).AbsoluteRange.MaxRange)
                    .DefaultIfEmpty(1).Max()??1;
                // Focus remains a shared score bonus, not a prohibition on a
                // productive alternative when the selected shooter cannot be hit.
                var opponents=VisiblePlayerOpponents(actor).ToArray();
                var paths=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains)
                    &&(!JointPolicy||SafeCoordinatedSetup(actor,p.Key))
                    &&opponents.Any(e=>Math.Max(Math.Abs(p.Key.X-e.Position.X),Math.Abs(p.Key.Y-e.Position.Y))<=range))
                    .Select(p=>(cell:p.Key,path:p.Value,cost:PlayerPathCost(actor,p.Value)))
                    .Where(p=>p.cost<points&&KeepsTeamSupportReach(actor,p.cell)).OrderBy(p=>p.cost).ThenBy(p=>p.cell.X).ThenBy(p=>p.cell.Y).Take(16);
                foreach(var path in paths)
                {
                    PlayerPlan plan;
                    try
                    {
                        actor.Position=path.cell;actor.CurrentActionPoints=points-path.cost;
                        plan=BuildPlayerPlans(actor,true).OrderByDescending(p=>p.score).FirstOrDefault();
                    }
                    finally{actor.Position=start;actor.CurrentActionPoints=points;}
                    if(plan==null)continue;
                    var cost=CombatActionCostCalculator.CalculateSkillCost(actor,plan.skill);
                    var value=plan.score*Math.Max(20,cost)/Math.Max(20,cost+path.cost);
                    if(value<=best)continue;
                    best=value;selected=path.path;attack=plan;
                }
                if(selected==null||!movement.TryMoveAlongPath(Session,actor,selected))return false;
                playerRepositioned=true;committedTeamActor=actor;committedTeamTurn=Result.turns;committedTeamAttack=attack;
                return true;
            }

            internal bool SafeCoordinatedHelpUp(BattleUnit helper,BattleUnit fallen)
            {
                var hp=fallen.Character.CurrentHealth;var lifeState=fallen.Character.LifeState;
                try
                {
                    fallen.Character.CurrentHealth=1;fallen.Character.LifeState=CharacterLifeState.Alive;
                    var dot=NextPlayerDot(fallen);var incoming=PlayerIncoming(fallen,fallen.Position);
                    var risk=dot+incoming;
                    if(risk<1+fallen.Character.ShieldPool.TotalShield)return true;
                    var firstThreat=VisiblePlayerOpponents(fallen).Where(e=>EnemyContact(e,fallen,fallen.Position)>0)
                        .Select(NextPlayerTurn).DefaultIfEmpty(float.MaxValue).Min();
                    if(dot>0)firstThreat=Math.Min(firstThreat,NextPlayerTurn(fallen));
                    foreach(var healer in Session.Units.Where(u=>u!=fallen&&u.IsPlayerSide==helper.IsPlayerSide
                        &&u.Character.LifeState==CharacterLifeState.Alive&&!StatusEffectSystem.IsHardCrowdControlled(u)
                        &&NextPlayerTurn(u)<firstThreat))
                    {
                        var ap=healer.CurrentActionPoints;var active=Session.ActiveUnit;
                        try
                        {
                            healer.CurrentActionPoints=ForecastPoints(healer);Session.ActiveUnit=healer;
                            float best=0;SkillData chosen=null;Dictionary<int,IReadOnlyList<BattleUnit>> selected=null;
                            foreach(var skill in healer.Character.GetActiveSkills().Where(IsSupport))
                                foreach(var patient in Session.Units.Where(u=>u.IsPlayerSide==helper.IsPlayerSide&&u.Character.LifeState!=CharacterLifeState.Dead))
                                    if(SupportPlan(healer,patient,skill,out var targets))
                                    {
                                        var value=GroupSupportPriority(healer,skill,targets)/SupportOpportunityCost(healer,skill,targets);
                                        if(value>best){best=value;chosen=skill;selected=targets;}
                                    }
                            if(selected!=null&&selected.Where(p=>p.Value.Contains(fallen)).Sum(p=>ImmediateRecovery(healer,fallen,chosen,p.Key))
                                +1+fallen.Character.ShieldPool.TotalShield>risk)return true;
                        }
                        finally{healer.CurrentActionPoints=ap;Session.ActiveUnit=active;}
                    }
                    return false;
                }
                finally{fallen.Character.CurrentHealth=hp;fallen.Character.LifeState=lifeState;}
            }
        }
    }
}
