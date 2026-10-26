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
            BattleUnit playerFocus;
            bool playerRepositioned;
            sealed class PlayerPlan
            {
                public SkillData skill;
                public Dictionary<int,IReadOnlyList<BattleUnit>> targets;
                public Dictionary<int,GridPosition?> positions;
                public float score;
                public GridPosition? objectPosition;
            }
            static int PlayerDistance(GridPosition a,GridPosition b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);
            IEnumerable<BattleUnit> VisiblePlayerOpponents(BattleUnit actor)=>Session.Units.Where(u=>u.IsPlayerSide!=actor.IsPlayerSide
                && u.Character.LifeState==CharacterLifeState.Alive && !StatusEffectSystem.IsInvisible(u));

            internal BattleUnit UpdatePlayerFocus(BattleUnit actor)
            {
                var allies=Session.Units.Where(h=>h.IsPlayerSide==actor.IsPlayerSide&&h.Character.LifeState==CharacterLifeState.Alive).ToArray();
                if(allies.Length==0){playerFocus=null;return null;}
                var opponents=VisiblePlayerOpponents(actor).Where(u=>u.SkeletonSlayerState?.DefenseTurns is not >0).ToArray();
                var keep=playerFocus!=null&&opponents.Contains(playerFocus);
                if(keep&&!TeamPolicy)return playerFocus;
                // Shared visible information only: remaining work to remove an
                // attacker, with a small cost for a distant target. Keep this target
                // until killed/hidden; individual legal killing blows still win.
                float Priority(BattleUnit u)
                {
                    var attack=Math.Max(1,(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f);
                    var range=u.Character.GetBasicAttackSkill()?.TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies)
                        .Select(m=>m.AbsoluteRange?.MaxRange??1).DefaultIfEmpty(1).Max()??1;
                    var distance=allies.Min(h=>PlayerDistance(h.Position,u.Position));
                    return CoordinatedFocusPriority(u,TeamFocusPriority(u,u.Character.CurrentHealth/(attack*(1+.05f*Math.Max(0,range-1)))*(1+.04f*distance)));
                }
                var best=opponents.OrderBy(Priority).ThenBy(u=>u.Character.CharacterId,StringComparer.Ordinal).FirstOrDefault();
                if(keep&&best!=null&&Priority(best)>=Priority(playerFocus)*.65f)return playerFocus;
                playerFocus=best;
                return playerFocus;
            }

            List<PlayerPlan> BuildPlayerPlans(BattleUnit actor,bool projected=false)
            {
                var plans=new List<PlayerPlan>();
                foreach(var skill in actor.Character.GetActiveSkills())
                {
                    if(zeroCostUsed.Contains(skill)||IsSupport(skill))continue;
                    if(!PreservesSupportMana(actor,skill))continue;
                    if(BuildAware&&skill.Effects.Any(e=>e.EffectType==EffectType.CreateObject))
                    {if(!projected)BuildObjectPlans(actor,skill,plans);continue;}
                    if(projected&&!skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage))continue;
                    if(!ScoutKnifeSystem.CanPay(actor,skill)||CombatActionCostCalculator.CalculateSkillCost(actor,skill)>actor.CurrentActionPoints)continue;
                    var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>();var positions=new Dictionary<int,GridPosition?>();float score=0;
                    for(var m=0;m<skill.TargetModes.Count;m++)
                    {
                        var mode=PassiveEffectCombatExecutionSystem.ResolveMode(actor,skill,skill.TargetModes[m]);
                        IReadOnlyList<BattleUnit> best=null;GridPosition? position=null;var bestScore=float.MinValue;
                        IEnumerable<GridPosition?> choices;
                        if(BuildAware&&mode.TargetType==TargetType.AreaAroundCaster&&CombatTargetingService.IsDirectionalShape(mode.AbsoluteRange.Shape))
                            choices=BuildDirections(actor,mode).Select(p=>(GridPosition?)p);
                        else if(mode.TargetType is TargetType.Caster or TargetType.All or TargetType.AreaAroundCaster)choices=new GridPosition?[]{null};
                        else if(projected)choices=Session.Units.Where(u=>u.Character.LifeState==CharacterLifeState.Alive
                            && (u.IsPlayerSide==actor.IsPlayerSide||!StatusEffectSystem.IsInvisible(u)))
                            .Select(u=>u.Position).Distinct().Where(p=>CombatTargetingService.IsPositionWithinRange(actor.Position,p,mode.AbsoluteRange)).Select(p=>(GridPosition?)p);
                        else choices=targeting.GetPositionsInRange(Session,actor.Position,mode.AbsoluteRange).Select(p=>(GridPosition?)p);
                        foreach(var choice in choices)
                        {
                            var group=targeting.GetTargets(Session,actor,mode,choice,true)
                                .Where(u=>!skill.RequiresLineOfSight||sight.HasLineOfSight(Session,actor.Position,u.Position)).ToArray();
                            if(group.Length==0)continue;
                            var value=Score(actor,skill,m,group);
                            if(value>bestScore){best=group;position=choice;bestScore=value;}
                        }
                        if(best==null)break;
                        targets[m]=best;positions[m]=position;score+=bestScore;
                    }
                    if(targets.Count!=skill.TargetModes.Count||score<=0||!skills.CanPreview(Session,actor,skill,targets,null,null,positions))continue;
                    var cost=BuildAware?BuildPlanCost(actor,skill):Math.Max(20,!UseLegacyPlayerPolicy||AuditUseEffectiveActionCost?CombatActionCostCalculator.CalculateSkillCost(actor,skill):skill.ActionCost);
                    plans.Add(new PlayerPlan{skill=skill,targets=targets,positions=positions,score=score/cost});
                }
                return plans;
            }

            internal bool CanProjectPlayerAttack(BattleUnit actor,GridPosition cell,int availablePoints)
                =>ProjectPlayerPlan(actor,cell,availablePoints)!=null;
            PlayerPlan ProjectPlayerPlan(BattleUnit actor,GridPosition cell,int availablePoints)
            {
                var position=actor.Position;var points=actor.CurrentActionPoints;
                try
                {
                    actor.Position=cell;actor.CurrentActionPoints=Math.Max(0,availablePoints);
                    return BuildPlayerPlans(actor,true).OrderByDescending(p=>p.score).FirstOrDefault();
                }
                finally{actor.Position=position;actor.CurrentActionPoints=points;}
            }
            int PlayerPathCost(BattleUnit actor,IReadOnlyList<GridPosition> path)
            {
                var cost=0;var from=actor.Position;var free=WarriorCombatSystem.FreeSteps(actor);var discount=ScoutCombatSystem.MovementDiscount(actor);
                for(var i=0;i<path.Count;i++)
                {
                    if(i>=free)cost+=PassiveEffectCombatExecutionSystem.ApplyMovementCost(Session,actor,
                        Math.Max(0,actor.MovementStepsThisTurn+i+1-discount)*ActionPointUnits.PerPoint)
                        +Session.Grid.GetMovementSurcharge(from,path[i])*ActionPointUnits.PerPoint;
                    from=path[i];
                }
                return cost;
            }
            bool PlayerFiringLane(BattleUnit actor,GridPosition cell,BattleUnit target)
            {
                var basic=actor.Character.GetBasicAttackSkill();
                return basic!=null && !SkillData.IsPointBlank(target.Position.X-cell.X,target.Position.Y-cell.Y)
                    && basic.TargetModes.Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(actor,basic,m))
                        .Any(m=>m.TargetingType==TargetingType.Enemies&&m.AbsoluteRange.MaxRange>1
                            && CombatTargetingService.IsPositionWithinRange(cell,target.Position,m.AbsoluteRange))
                    && (!basic.RequiresLineOfSight||sight.HasLineOfSight(Session,cell,target.Position));
            }
            bool MovePlayerTactically(BattleUnit actor,BattleUnit[] opponents)
            {
                var danger=DangerousCells();
                var paths=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains))
                    .Select(p=>(cell:p.Key,path:p.Value,cost:PlayerPathCost(actor,p.Value)))
                    .Where(p=>p.cost<=actor.CurrentActionPoints&&KeepsTeamSupportReach(actor,p.cell)).OrderBy(p=>p.cost).ThenBy(p=>p.cell.X).ThenBy(p=>p.cell.Y).ToArray();
                if(paths.Length==0)return false;
                // Recovery is a real movement action, before the ranged hold rule.
                if(actor.MaxKnives>0&&actor.AvailableKnives==0)
                {
                    var knives=Session.MapObjects.OfType<KnifeCombatMapObject>().Where(k=>k.OwnerCharacterId==actor.Character.CharacterId).Select(k=>k.Position).ToHashSet();
                    var pickup=paths.FirstOrDefault(p=>knives.Contains(p.cell));
                    if(pickup.path!=null)return movement.TryMoveAlongPath(Session,actor,pickup.path);
                    var embedded=opponents.Where(t=>t.ScoutState.Knives.Any(k=>k.Owner==actor)).ToArray();
                    var extraction=paths.Where(p=>embedded.Any(t=>PlayerDistance(p.cell,t.Position)==1)).FirstOrDefault();
                    if(extraction.path!=null)return movement.TryMoveAlongPath(Session,actor,extraction.path);
                    var recoverTargets=knives.Concat(embedded.Select(t=>t.Position)).ToArray();
                    if(recoverTargets.Length>0)
                    {
                        int RecoverDistance(GridPosition cell)=>recoverTargets.Min(t=>PlayerDistance(cell,t));
                        var progress=paths.Where(p=>RecoverDistance(p.cell)<RecoverDistance(actor.Position))
                            .OrderBy(p=>RecoverDistance(p.cell)).ThenBy(p=>p.cost).FirstOrDefault();
                        if(progress.path!=null)return movement.TryMoveAlongPath(Session,actor,progress.path);
                    }

                }
                var basic=actor.Character.GetBasicAttackSkill();
                var ranged=basic?.TargetModes.Any(m=>PassiveEffectCombatExecutionSystem.ResolveMode(actor,basic,m).AbsoluteRange.MaxRange>1)==true;
                if(!ranged)
                {
                    var focus=playerFocus??opponents.OrderBy(t=>PlayerDistance(actor.Position,t.Position)).First();
                    var approach=paths.Where(p=>PlayerDistance(p.cell,focus.Position)<PlayerDistance(actor.Position,focus.Position))
                        .OrderBy(p=>PlayerDistance(p.cell,focus.Position)).ThenBy(p=>p.cost).FirstOrDefault();
                    if(approach.path!=null)return JointSetupMove(actor,approach.path);
                    // A blocked focus must not trap another reachable enemy forever.
                    var fallback=paths.Where(p=>opponents.Min(t=>PlayerDistance(p.cell,t.Position))<opponents.Min(t=>PlayerDistance(actor.Position,t.Position)))
                        .OrderBy(p=>opponents.Min(t=>PlayerDistance(p.cell,t.Position))).ThenBy(p=>p.cost).FirstOrDefault();
                    return fallback.path!=null&&JointSetupMove(actor,fallback.path);
                }
                bool Safe(GridPosition cell)=>!opponents.Any(t=>SkillData.IsPointBlank(t.Position.X-cell.X,t.Position.Y-cell.Y));
                // First find a real affordable move+attack. Preview never grants
                // mana/knives, executes effects or changes the actor permanently.
                List<GridPosition> attackPath=null;PlayerPlan attackPlan=null;float attackScore=0;
                foreach(var candidate in paths)
                {
                    var plan=ProjectPlayerPlan(actor,candidate.cell,actor.CurrentActionPoints-candidate.cost);
                    if(plan==null)continue;
                    var actionCost=CombatActionCostCalculator.CalculateSkillCost(actor,plan.skill);
                    var value=plan.score*Math.Max(20,actionCost)/Math.Max(20,actionCost+candidate.cost)*(Safe(candidate.cell)?1:.5f);
                    if(value>attackScore){attackPath=candidate.path;attackPlan=plan;attackScore=value;}
                }
                if(attackPath!=null&&movement.TryMoveAlongPath(Session,actor,attackPath))
                {CommitMageAttack(actor,attackPlan);return true;}
                // Keep real move+attack opportunities. Prefer safer setup cells,
                // but permit a full turn to close against greater enemy range;
                // leftover points alone do not justify leaving a refuge.
                var canPrepare=MageMovementPolicy(actor)&&basic!=null
                    &&actor.CurrentActionPoints>=CombatActionCostCalculator.CalculateSkillCost(actor,basic);
                paths=paths.Where(p=>CoordinatedPolicy?SafeCoordinatedSetup(actor,p.cell):canPrepare||SafeMageApproach(actor,p.cell)).ToArray();
                var lane=opponents.Any(t=>PlayerFiringLane(actor,actor.Position,t));
                if(lane&&Safe(actor.Position))
                {
                    // One purposeful firing-position step can fund Momentum
                    // specials; this is a choice, not a change to resource rules.
                    var pool=actor.Character.ResourcePool;
                    var needsMomentum=pool.ResourceType==ResourceType.Momentum&&actor.Character.GetActiveSkills()
                        .Any(s=>PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,s)>pool.CurrentValue);
                    if(!playerRepositioned&&needsMomentum)
                    {
                        var step=paths.FirstOrDefault(p=>p.path.Count==1&&Safe(p.cell)&&opponents.Any(t=>PlayerFiringLane(actor,p.cell,t)));
                        if(step.path!=null){playerRepositioned=true;return movement.TryMoveAlongPath(Session,actor,step.path);}
                    }
                    return false;
                }
                // Approach only far enough to establish a safe firing lane for
                // the next turn. Do not charge to adjacency with leftover AP.
                var firing=paths.Where(p=>Safe(p.cell)&&opponents.Any(t=>PlayerFiringLane(actor,p.cell,t)))
                    .OrderBy(p=>SafeMageApproach(actor,p.cell)?0:1).ThenBy(p=>MageApproachRisk(actor,p.cell))
                    .ThenByDescending(p=>playerFocus!=null&&PlayerFiringLane(actor,p.cell,playerFocus)).ThenBy(p=>p.cost).FirstOrDefault();
                if(firing.path!=null&&movement.TryMoveAlongPath(Session,actor,firing.path))
                {if(MageMovementPolicy(actor)||JointPolicy)playerRepositioned=true;return true;}
                if(lane)return false;
                var advance=paths.Where(p=>opponents.Min(t=>PlayerDistance(p.cell,t.Position))<opponents.Min(t=>PlayerDistance(actor.Position,t.Position)))
                    .OrderBy(p=>SafeMageApproach(actor,p.cell)?0:1).ThenBy(p=>MageApproachRisk(actor,p.cell))
                    .ThenBy(p=>opponents.Min(t=>PlayerDistance(p.cell,t.Position))).ThenBy(p=>p.cost).FirstOrDefault();
                if(advance.path!=null&&movement.TryMoveAlongPath(Session,actor,advance.path))
                {if(MageMovementPolicy(actor)||JointPolicy)playerRepositioned=true;return true;}
                return false;
            }
        }
    }
}
