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
            internal bool UsePreviousSupportPolicy;
            bool BudgetSupport=>SmartRescue&&!UsePreviousSupportPolicy;

            // This is a player decision, never a production cost/resource rule.
            // Reserve one real immediate heal while someone is exposed or already
            // hurt. Do not presume a possible enemy attack is a committed shot.
            internal int SupportManaReserve(BattleUnit actor)
            {
                if(!BudgetSupport||actor.Character.ResourcePool.ResourceType!=ResourceType.Mana)return 0;
                var heal=actor.Character.GetActiveSkills().Where(s=>s.Effects.Any(e=>e.EffectType is
                    EffectType.PaladinHeal or EffectType.Heal or EffectType.HealPercent or EffectType.HealMissingHealthPercent))
                    .Select(s=>PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,s)).DefaultIfEmpty(0).Min();
                if(heal<=0)return 0;
                var next=NextPlayerTurn(actor);
                var exposed=Session.Units.Where(a=>a.IsPlayerSide==actor.IsPlayerSide&&a.Character.LifeState!=CharacterLifeState.Dead)
                    .Any(a=>a.Character.LifeState==CharacterLifeState.Downed||NextPlayerDot(a)>0
                        ||MageRecoveryPolicy&&Mage(a)&&PlayerIncoming(a,a.Position)>0
                        ||a.Character.CurrentHealth<a.Character.Stats.MaxHealth.GetValue()*.75f
                        &&VisiblePlayerOpponents(actor).Any(e=>NextPlayerTurn(e)<=next&&EnemyContact(e,a,a.Position)>0));
                if(!exposed)return 0;
                // Critical patients may need a second heal in this same turn;
                // otherwise the real pool's next-turn regeneration is available.
                var immediate=Session.Units.Any(a=>a.IsPlayerSide==actor.IsPlayerSide&&CriticalPatient(a));
                return Math.Max(0,heal-(immediate?0:Math.Max(0,actor.Character.ResourcePool.Regeneration)));
            }

            internal bool PreservesSupportMana(BattleUnit actor,SkillData skill)
            {
                if(!BudgetSupport||IsSupport(skill))return true;
                // Damaging actions can remove the threat, and resource-free/basic
                // attacks remain legal. Only optional utility spends are deferred.
                if(BuildAware)return BuildPreservesHealing(actor,skill,SupportManaReserve(actor));
                if(skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage))return true;
                var cost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,skill);
                return cost<=0||actor.Character.ResourcePool.CurrentValue-cost>=SupportManaReserve(actor);
            }

            internal float SupportOpportunityCost(BattleUnit actor,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets)
            {
                var ap=PaladinCombatSystem.HealCost(actor,skill,targets.Values.SelectMany(t=>t).FirstOrDefault(),
                    CombatActionCostCalculator.CalculateSkillCost(actor,skill));
                // Express scarce RP as part of an action budget, so a discounted
                // 0-AP heal still has its real 30-mana opportunity cost.
                var pool=actor.Character.ResourcePool;
                var resource=PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,skill);
                return Math.Max(20,ap+ForecastPoints(actor)*resource/(float)Math.Max(1,pool.MaxValue));
            }

            internal float GroupSupportPriority(BattleUnit actor,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets)
            {
                var allies=targets.Values.SelectMany(t=>t).Distinct().Where(t=>t.IsPlayerSide==actor.IsPlayerSide).ToArray();
                if(BuildAware&&skill.Effects.Any(e=>e.EffectType==EffectType.Shield))
                    return allies.Sum(a=>BuildShieldPriority(actor,a,skill,targets,true));
                // Still require a concrete critical patient in the tactical pass.
                // Count each affected recipient once, regardless of the anchor.
                if(!allies.Any(CriticalPatient))return 0;
                return allies.Sum(a=>CriticalPatient(a)?SupportPriority(actor,a,skill,targets):MaintenanceBenefit(actor,a,skill,targets));
            }

            float MaintenanceBenefit(BattleUnit actor,BattleUnit ally,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets)
            {
                if(ally.Character.LifeState!=CharacterLifeState.Alive)return 0;
                var max=ally.Character.Stats.MaxHealth.GetValue();
                var missing=Math.Max(0,max-ally.Character.CurrentHealth);
                var regen=skill.Effects.Any(e=>e.EffectType==EffectType.PaladinRegeneration);
                if(regen&&ally.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration"&&PassiveEffectLifecycleSystem.IsActive(e)))return 0;
                var immediate=targets.Where(t=>t.Value.Contains(ally)).Sum(t=>ImmediateRecovery(actor,ally,skill,t.Key));
                if(skill.Effects.Any(e=>e.EffectType==EffectType.Shield))
                    return BuildAware?BuildShieldPriority(actor,ally,skill,targets,false)
                        :Math.Min(immediate,Math.Max(NextPlayerDot(ally)+PlayerIncoming(ally,ally.Position),max*.15f));
                return Math.Min(missing,immediate+(regen?max*.4f*.6f:0));
            }

            bool BudgetMaintenance(BattleUnit actor,bool downedOnly)
            {
                if(downedOnly)return false; // TacticalSupport owns actual recoveries.
                var plans=new List<(SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets,float score,bool regen)>();
                foreach(var skill in actor.Character.GetActiveSkills().Where(IsSupport))
                {
                    var regen=skill.Effects.Any(e=>e.EffectType==EffectType.PaladinRegeneration);
                    var shield=skill.Effects.Any(e=>e.EffectType==EffectType.Shield);
                    foreach(var ally in Session.Units.Where(a=>a.IsPlayerSide==actor.IsPlayerSide&&a.Character.LifeState==CharacterLifeState.Alive
                        &&a.Character.CurrentHealth/a.Character.Stats.MaxHealth.GetValue()<=(shield?.6f:.75f)))
                        if(SupportPlan(actor,ally,skill,out var targets))
                        {
                            if(!PreservesMageRecoveryMana(actor,skill,targets))continue;
                            var value=targets.Values.SelectMany(t=>t).Distinct().Where(t=>t.IsPlayerSide==actor.IsPlayerSide)
                                .Sum(t=>MaintenanceBenefit(actor,t,skill,targets));
                            if(value>0)plans.Add((skill,targets,value/SupportOpportunityCost(actor,skill,targets),regen));
                        }
                }
                // Preserve regeneration as the proven maintenance strategy;
                // compare all actual recipients within each tier rather than the
                // first wounded anchor, including efficient group heals.
                foreach(var p in plans.OrderByDescending(p=>p.regen).ThenByDescending(p=>p.score))
                {
                    if(BuildAware&&p.skill.Effects.Any(e=>e.EffectType==EffectType.Shield)
                        &&BuildPlayerPlans(actor).Any(a=>a.score>p.score*1.1f))continue;
                    if(BuildAware&&!p.regen&&!Session.Units.Any(a=>a.IsPlayerSide==actor.IsPlayerSide&&CriticalPatient(a))
                        &&p.targets.Values.SelectMany(t=>t).All(t=>t.Character.CurrentHealth>t.Character.Stats.MaxHealth.GetValue()*.5f)
                        &&BuildPlayerPlans(actor).Any(a=>a.score>p.score*1.1f))continue;
                    if(skills.TryExecute(Session,actor,p.skill,p.targets))return true;
                }
                return false;
            }
        }
    }
}
