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
            internal bool UsePreviousMagePolicy;
            bool MageRecoveryPolicy=>TeamPolicy&&!UsePreviousMagePolicy;
            bool MageMovementPolicy(BattleUnit actor)
            {
                if(!MageRecoveryPolicy||!Mage(actor))return false;
                int Range(BattleUnit unit)
                {
                    var basic=unit.Character.GetBasicAttackSkill();
                    return basic?.TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies)
                        .Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(unit,basic,m).AbsoluteRange.MaxRange).DefaultIfEmpty(1).Max()??1;
                }
                var own=Range(actor);
                return VisiblePlayerOpponents(actor).Any(e=>Range(e)>own);
            }
            PlayerPlan committedMageAttack;
            BattleUnit committedMageActor;
            int committedMageTurn;

            void CommitMageAttack(BattleUnit actor,PlayerPlan plan)
            {
                if(!MageMovementPolicy(actor))return;
                committedMageAttack=plan;committedMageActor=actor;committedMageTurn=Result.turns;
            }
            bool TryCommittedMageAttack(BattleUnit actor)
            {
                var plan=committedMageAttack;
                committedMageAttack=null;
                if(!MageMovementPolicy(actor)||plan==null||committedMageActor!=actor||committedMageTurn!=Result.turns)return false;
                // Revalidate the actual post-move state and pay normal costs.
                // Do not undo a planned move+shot with a new retreat decision.
                if(!skills.TryExecute(Session,actor,plan.skill,plan.targets,null,plan.objectPosition,plan.positions))return false;
                AuditChosenPlan?.Invoke(actor,plan.skill,plan.targets.Values.SelectMany(t=>t).Distinct().ToArray(),plan.score);
                if(CombatActionCostCalculator.CalculateSkillCost(actor,plan.skill)==0)zeroCostUsed.Add(plan.skill);
                return true;
            }

            float MageApproachRisk(BattleUnit actor,GridPosition cell)=>MageMovementPolicy(actor)?PlayerIncoming(actor,cell):0;

            // Prefer a non-lethal approach; only leftover-AP movement treats this
            // as a restriction. A full turn can still prepare a necessary attack.
            internal bool SafeMageApproach(BattleUnit actor,GridPosition cell)
            {
                if(!MageMovementPolicy(actor))return true;
                var risk=PlayerIncoming(actor,cell);
                return risk<=PlayerIncoming(actor,actor.Position)
                    ||risk+NextPlayerDot(actor)<actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield;
            }


            // This only previews a real available recovery: no mana, AP, healing,
            // AV or random rolls are granted to the live session.
            internal bool MageRecoveryReach(BattleUnit healer,BattleUnit mage,GridPosition cell)
            {
                if(healer==mage||healer.Character.LifeState!=CharacterLifeState.Alive||StatusEffectSystem.IsHardCrowdControlled(healer))return false;
                var position=mage.Position;var points=healer.CurrentActionPoints;var active=Session.ActiveUnit;
                try
                {
                    mage.Position=cell;healer.CurrentActionPoints=ForecastPoints(healer);Session.ActiveUnit=healer;
                    return healer.Character.GetActiveSkills().Where(IsSupport).Any(s=>SupportPlan(healer,mage,s,out var targets)
                        &&targets.Where(t=>t.Value.Contains(mage)).Sum(t=>ImmediateRecovery(healer,mage,s,t.Key))>NextPlayerDot(mage));
                }
                finally{mage.Position=position;healer.CurrentActionPoints=points;Session.ActiveUnit=active;}
            }

            // TacticalSupport still owns urgent healing and can spend the last
            // mana. Only non-urgent maintenance must leave a real heal available.
            internal bool PreservesMageRecoveryMana(BattleUnit actor,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets)
            {
                if(!MageRecoveryPolicy||actor.Character.ResourcePool.ResourceType!=ResourceType.Mana)return true;
                var patients=Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&Mage(u)
                    &&u.Character.LifeState==CharacterLifeState.Alive&&u.Character.CurrentHealth<u.Character.Stats.MaxHealth.GetValue()*.75f
                    &&(NextPlayerDot(u)>0||PlayerIncoming(u,u.Position)>0)).ToArray();
                if(patients.Length==0)return true;
                if(patients.Any(m=>m.Character.CurrentHealth<m.Character.Stats.MaxHealth.GetValue()
                    &&targets.Where(t=>t.Value.Contains(m)).Sum(t=>ImmediateRecovery(actor,m,skill,t.Key))>0))return true;
                var reserve=actor.Character.GetActiveSkills().Where(s=>s.Effects.Any(e=>e.EffectType is
                    EffectType.PaladinHeal or EffectType.Heal or EffectType.HealPercent or EffectType.HealMissingHealthPercent))
                    .Select(s=>PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,s)).DefaultIfEmpty(0).Min();
                if(!patients.Any(PlayerNeedsProtection))reserve=Math.Max(0,reserve-Math.Max(0,actor.Character.ResourcePool.Regeneration));
                return actor.Character.ResourcePool.CurrentValue-PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,skill)>=reserve;
            }
        }
    }
}
