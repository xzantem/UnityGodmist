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
            internal bool UsePreviousTeamPolicy;
            bool TeamPolicy=>BudgetSupport&&!UsePreviousTeamPolicy;
            bool RescueEmergency(BattleUnit actor,BattleUnit fallen)=>fallen.DownedTurnsRemaining<=1
                ||RescueDeadline(fallen)<=CombatTurnSystem.CalculateBaseActionValue(actor)
                ||Session.Units.Count(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive)==1;
            bool RangedPlayer(BattleUnit actor)=>actor.Character.GetBasicAttackSkill()?.TargetModes
                .Any(m=>PassiveEffectCombatExecutionSystem.ResolveMode(actor,actor.Character.GetBasicAttackSkill(),m).AbsoluteRange.MaxRange>1)==true;
            bool TeamHealer(BattleUnit actor)=>actor.Character.GetActiveSkills().Any(s=>s.Effects.Any(e=>e.EffectType is
                EffectType.PaladinHeal or EffectType.Heal or EffectType.HealPercent or EffectType.HealMissingHealthPercent));

            internal bool KeepsTeamSupportReach(BattleUnit actor,GridPosition cell)
            {
                if(!TeamPolicy||!TeamHealer(actor))return true;
                var patients=Session.Units.Where(u=>u!=actor&&u.IsPlayerSide==actor.IsPlayerSide&&CriticalPatient(u)).ToArray();
                if(patients.Length==0)return true;
                var distance=patients.Min(u=>PlayerDistance(actor.Position,u.Position));
                return patients.Any(u=>PlayerDistance(cell,u.Position)<=Math.Max(2,distance));
            }

            // An actual affordable preferred recovery before the victim's death,
            // not an assumption that the healer will acquire mana or ignore a
            // more urgent patient. Projections restore all temporary state.
            internal bool DeferPlayerRescue(BattleUnit actor,BattleUnit fallen)
            {
                if(!TeamPolicy||RescueEmergency(actor,fallen))return false;
                if(CoordinatedPolicy&&(RangedPlayer(actor)||TeamHealer(actor))&&!DesignatedRescuer(actor,fallen)
                    &&!SafeCoordinatedHelpUp(actor,fallen))return true;
                var exposed=PlayerIncoming(actor,actor.Position)>0||NextPlayerDot(actor)>0;
                if(!exposed&&!RangedPlayer(actor))return false;
                foreach(var healer in Session.Units.Where(u=>u!=actor&&u!=fallen&&u.IsPlayerSide==actor.IsPlayerSide
                    &&u.Character.LifeState==CharacterLifeState.Alive&&!StatusEffectSystem.IsHardCrowdControlled(u)
                    &&NextPlayerTurn(u)<RescueDeadline(fallen)&&NextPlayerTurn(u)<CombatTurnSystem.CalculateBaseActionValue(actor)))
                {
                    var ap=healer.CurrentActionPoints;var active=Session.ActiveUnit;
                    try
                    {
                        healer.CurrentActionPoints=ForecastPoints(healer);Session.ActiveUnit=healer;
                        float best=0;Dictionary<int,IReadOnlyList<BattleUnit>> chosen=null;SkillData selected=null;
                        foreach(var skill in healer.Character.GetActiveSkills().Where(IsSupport))
                            foreach(var patient in Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Downed))
                                if(SupportPlan(healer,patient,skill,out var targets))
                                {
                                    var score=GroupSupportPriority(healer,skill,targets)/SupportOpportunityCost(healer,skill,targets);
                                    if(score>best){best=score;chosen=targets;selected=skill;}
                                }
                        if(chosen!=null&&chosen.Where(p=>p.Value.Contains(fallen)).Sum(p=>ImmediateRecovery(healer,fallen,selected,p.Key))>NextPlayerDot(fallen))return true;
                    }
                    finally{healer.CurrentActionPoints=ap;Session.ActiveUnit=active;}
                }
                return false;
            }

            internal bool ProtectRangedPlayer(BattleUnit actor)
            {
                if(!TeamPolicy||!(RangedPlayer(actor)||CoordinatedPolicy&&TeamHealer(actor))||playerRepositioned||!PlayerNeedsProtection(actor))return false;
                // Retreat must not bypass the rescue fallback and spend the
                // only helper's approach budget. A lower-priority rescue is
                // still owned until another real recovery can take it over.
                if(Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Downed&&!DeferPlayerRescue(actor,u)))return false;
                var current=PlayerIncoming(actor,actor.Position);if(current<=0)return false;
                var opponents=VisiblePlayerOpponents(actor).ToArray();if(opponents.Length==0)return false;
                var allies=Session.Units.Where(u=>u!=actor&&u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive).ToArray();
                var party=allies;var partyCohesion=party.Length==0?float.MaxValue:Math.Max(3,party.Min(u=>PlayerDistance(actor.Position,u.Position)));
                // A second ranged ally may leave to recover a dagger; anchor a
                // withdrawal to actual recovery support when it is available.
                var healers=allies.Where(TeamHealer).ToArray();if(healers.Length>0)allies=healers;
                var cohesion=allies.Length==0?float.MaxValue:Math.Max(3,allies.Min(u=>PlayerDistance(actor.Position,u.Position)));
                var danger=DangerousCells();
                var candidates=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains))
                    .Select(p=>(cell:p.Key,path:p.Value,cost:PlayerPathCost(actor,p.Value)))
                    .Where(p=>p.cost<=actor.CurrentActionPoints&&(allies.Length==0||allies.Any(u=>PlayerDistance(p.cell,u.Position)<=cohesion)
                        ||MageMovementPolicy(actor)&&(party.Any(u=>PlayerDistance(p.cell,u.Position)<=partyCohesion)
                            ||healers.Any(h=>MageRecoveryReach(h,actor,p.cell)))))
                    .Select(p=>(p.cell,p.path,p.cost,risk:PlayerIncoming(actor,p.cell)))
                    .Where(p=>p.risk<current*.65f).OrderBy(p=>p.risk).ThenBy(p=>p.cost).Take(8)
                    .Select(p=>(p.path,p.cost,p.risk,attack:ProjectPlayerPlan(actor,p.cell,actor.CurrentActionPoints-p.cost)?.score??0))
                    .OrderByDescending(p=>p.attack>0).ThenBy(p=>p.risk).ThenBy(p=>p.cost);
                foreach(var p in candidates)
                    if(movement.TryMoveAlongPath(Session,actor,p.path))
                    {playerRepositioned=true;if(Mage(actor))Result.mageRetreats++;return true;}
                return false;
            }

            // Preserve focus unless a visibly more urgent attacker wins by a
            // substantial margin. All classes use the same exposed-party model.
            internal float TeamFocusPriority(BattleUnit enemy,float ordinary)
            {
                if(!TeamPolicy)return ordinary;
                var pressure=Session.Units.Where(u=>u.IsPlayerSide!=enemy.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive
                    &&u.Character.CurrentHealth<u.Character.Stats.MaxHealth.GetValue()*.5f&&NextPlayerTurn(enemy)<=NextPlayerTurn(u))
                    .Select(u=>EnemyContact(enemy,u,u.Position)/Math.Max(1,u.Character.CurrentHealth+u.Character.ShieldPool.TotalShield))
                    .DefaultIfEmpty(0).Max();
                return ordinary/(1+Math.Min(2,pressure));
            }

            float TeamBurnScore(BattleUnit actor,SkillData skill,float value)
            {
                if(!TeamPolicy||MageCombatSystem.F(actor,"fire_offering")==0||NextPlayerDot(actor)<actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield)return value;
                if(!skill.Effects.Any(e=>e.EffectType==EffectType.ApplyStatus&&e.StatusId=="burn"))return value;
                // Soft opportunity penalty: retain an exceptionally valuable
                // lethal AoE instead of banning an offensive skill outright.
                return value*.15f;
            }
        }
    }
}
