using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Core.Constants;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        private static int TacticalDistance(GridPosition a,GridPosition b) => Mathf.Abs(a.X-b.X)+Mathf.Abs(a.Y-b.Y);

        // Bounded, deterministic estimates. Never execute an effect, consume a proc,
        // draw random numbers or assume an unobserved opponent's position.
        private static float IncomingThreat(CombatSession session, BattleUnit observer, BattleUnit recipient)
        {
            if (session == null || recipient?.Character == null) return 0f;
            return session.Units.Where(u => u?.Character?.LifeState == CharacterLifeState.Alive
                && u.IsPlayerSide != observer.IsPlayerSide && !StatusEffectSystem.IsInvisible(u)
                && (session.Contact == null || session.Contact.Sees(observer,u)))
                .OrderBy(u=>TacticalDistance(u.Position,recipient.Position)).Take(4)
                .Sum(enemy=>OpponentThreat(session,enemy,recipient));
        }

        private static float OpponentThreat(CombatSession session,BattleUnit enemy,BattleUnit recipient)
        {
            if(StatusEffectSystem.IsHardCrowdControlled(enemy))return 0f;
            var sight=new CombatLineOfSightService();var best=0f;
            foreach(var skill in enemy.Character.GetActiveSkills().Where(s=>s!=null && !s.IsToggle).Take(12))
            {
                if(PassiveEffectCombatExecutionSystem.ResolveResourceCost(enemy,skill)>(enemy.Character.ResourcePool?.CurrentValue ?? 0))continue;
                if(!string.IsNullOrEmpty(skill.RequiredCasterStatusId) && !StatusEffectSystem.HasStatus(enemy,skill.RequiredCasterStatusId))continue;
                foreach(var effect in skill.Effects)
                {
                    if(effect?.EffectType!=EffectType.DealDamage || effect.TargetModeIndex<0 || effect.TargetModeIndex>=skill.TargetModes.Count)continue;
                    var mode=PassiveEffectCombatExecutionSystem.ResolveMode(enemy,skill,skill.TargetModes[effect.TargetModeIndex]);
                    if(mode.TargetingType==TargetingType.Allies || !SkillExecutionSystem.CanPlanEffect(session,effect,enemy,recipient))continue;
                    var distance=TacticalDistance(enemy.Position,recipient.Position);
                    var inRange=CombatTargetingService.IsPositionWithinRange(enemy.Position,recipient.Position,mode.AbsoluteRange);
                    if(!inRange && (StatusEffectSystem.IsImmobilized(enemy) || distance>(mode.AbsoluteRange?.MaxRange ?? 0)+2))continue;
                    if(skill.RequiresLineOfSight && !sight.HasLineOfSight(session,enemy.Position,recipient.Position))continue;
                    best=Mathf.Max(best,CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,enemy,recipient,skill,effect.TargetModeIndex,effect)*(inRange?1f:.35f));
                }
            }
            return best;
        }

        private static float ControlDenial(CombatSession session,BattleUnit observer,BattleUnit controlled)
        {
            var damage=session.Units.Where(u=>u?.Character?.LifeState==CharacterLifeState.Alive && u.IsPlayerSide==observer.IsPlayerSide)
                .OrderBy(u=>TacticalDistance(controlled.Position,u.Position)).Take(4)
                .Select(u=>OpponentThreat(session,controlled,u)).DefaultIfEmpty(0).Max();
            var recovery=0f;
            foreach(var skill in controlled.Character.GetActiveSkills().Where(s=>s!=null).Take(12))
            {
                if(PassiveEffectCombatExecutionSystem.ResolveResourceCost(controlled,skill)>(controlled.Character.ResourcePool?.CurrentValue ?? 0))continue;
                foreach(var effect in skill.Effects.Where(e=>e?.EffectType is EffectType.Heal or EffectType.HealPercent or EffectType.HealMissingHealthPercent))
                foreach(var ally in session.Units.Where(u=>IsObservedLivingOpponent(session,observer,u)).Take(6))
                {
                    var mode=skill.TargetModes[effect.TargetModeIndex];
                    if(ally!=controlled && mode.TargetType==TargetType.Caster)continue;
                    if(mode.TargetingType==TargetingType.Enemies || ally==controlled && mode.TargetType!=TargetType.Caster && !mode.IncludesCaster)continue;
                    if(mode.TargetType!=TargetType.All && mode.TargetType!=TargetType.Caster
                        && !CombatTargetingService.IsPositionWithinRange(controlled.Position,ally.Position,mode.AbsoluteRange))continue;
                    if(skill.RequiresLineOfSight && !new CombatLineOfSightService().HasLineOfSight(session,controlled.Position,ally.Position))continue;
                    recovery=Mathf.Max(recovery,HealingAmount(controlled,ally,effect));
                }
            }
            return damage+recovery*.5f;
        }

        private static float DefensiveModifierUtility(CombatSession session,BattleUnit caster,BattleUnit target,SkillEffectData effect)
        {
            if(Mathf.Abs(effect.ModifierValue)<.00001f)return 0f;
            var isDefense=effect.EffectType==EffectType.ModifyStat && effect.ModifierStatType is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense;
            var incoming=effect.EffectType==EffectType.ModifyVar && effect.ModifierVariableType==GameplayVariableType.DamageReceivedPercent;
            if(!isDefense && !incoming)return Ai(Godmist.Data.Configuration.GameDefineIds.AiModifyBonus,40f);
            if(target.IsPlayerSide!=caster.IsPlayerSide)return 0f;
            var copy=CopyPlanningUnit(target);copy.WarriorState.Session=session;
            if(isDefense)
            {
                ref var stat=ref copy.Character.Stats.GetStat(effect.ModifierStatType);
                var before=Godmist.Systems.Characters.CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue,effect.ModifierStatType,copy.Character.PassiveEffects);
                copy.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId="ai.preview.defense",Lifetime=PassiveEffectLifetime.CombatScoped,
                    StatModifiers=new System.Collections.Generic.List<StatModifier>{new(){StatType=effect.ModifierStatType,Operation=effect.ModifierOperation,Stacking=effect.ModifierStacking,Value=effect.ModifierValue}}});
                var after=Godmist.Systems.Characters.CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue,effect.ModifierStatType,copy.Character.PassiveEffects);
                stat.SetEffectiveValue(Mathf.Max(0,stat.GetValue()+after-before));
            }
            else copy.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId="ai.preview.protection",Lifetime=PassiveEffectLifetime.CombatScoped,
                VariableModifiers=new System.Collections.Generic.List<GameplayVariableModifier>{new(){VariableType=effect.ModifierVariableType,Stacking=effect.ModifierStacking,Value=effect.ModifierValue}}});
            return Mathf.Max(0,IncomingThreat(session,caster,target)-IncomingThreat(session,caster,copy))*Mathf.Clamp(effect.DurationTurns,1,2);
        }

        private static float SetupOpportunity(CombatSession session,BattleUnit caster,SkillData setup)
        {
            var best=0f;
            foreach(var status in setup.Effects.Where(e=>e?.EffectType==EffectType.ApplyStatus && e.DurationTurns>1
                && setup.TargetModes[e.TargetModeIndex].TargetType==TargetType.Caster))
            foreach(var preparation in caster.Character.GetActiveSkills().Where(s=>s!=null && s.RequiredCasterStatusId==status.StatusId && IsShortOffensivePreparation(s) && session.CanUseBossSkill?.Invoke(caster,s)!=false))
            foreach(var attack in caster.Character.GetActiveSkills().Where(s=>s!=null && s!=preparation && s!=setup && session.CanUseBossSkill?.Invoke(caster,s)!=false))
            {
                if(!CanPayPreparationFollowUp(caster,preparation,attack) ||
                    (caster.Character.ResourcePool?.CurrentValue ?? 0)<PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,setup)
                        +PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,preparation)+PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,attack))continue;
                var benefit=preparation.Effects.Where(e=>IsOffensivePreparationEffect(e)).Sum(e=>Mathf.Abs(e.ModifierValue));
                foreach(var effect in attack.Effects.Where(e=>e?.EffectType==EffectType.DealDamage))
                foreach(var target in session.Units.Where(u=>IsObservedLivingOpponent(session,caster,u)))
                {
                    var mode=attack.TargetModes[effect.TargetModeIndex];
                    if(TacticalDistance(caster.Position,target.Position)>(mode.AbsoluteRange?.MaxRange ?? 1)+TurnPlanDepth)continue;
                    if(attack.RequiresLineOfSight && !new CombatLineOfSightService().HasLineOfSight(session,caster.Position,target.Position))continue;
                    var damage=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,attack,effect.TargetModeIndex,effect);
                    best=Mathf.Max(best,damage*(1f+benefit)*.75f);
                }
            }
            return best;
        }

        private static float HealingAmount(BattleUnit caster,BattleUnit target,SkillEffectData effect)
        {
            if (target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead) return 0f;
            var maximum=Mathf.Max(0,Mathf.RoundToInt(target.Character.Stats.MaxHealth.GetValue()));
            var raw=effect.EffectType switch
            {
                EffectType.Heal => CombatRecoverySystem.CalculateSkillHealing(caster,effect),
                EffectType.HealMissingHealthPercent => Mathf.CeilToInt(GetMissingHealth(target)*effect.PercentageValue),
                _ => Mathf.CeilToInt(maximum*effect.PercentageValue)
            };
            return Mathf.Max(0,Mathf.Min(raw,GetMissingHealth(target)));
        }

        private static float HealingUtility(CombatSession session,BattleUnit caster,BattleUnit target,float amount)
        {
            if (amount<=0f) return 0f;
            if (target.IsPlayerSide!=caster.IsPlayerSide) return -amount;
            var threat=IncomingThreat(session,caster,target);
            var value=amount + Mathf.Min(amount,threat)*.5f;
            if(target.Character.LifeState==CharacterLifeState.Downed)
                value+=Mathf.Min(amount,100f)+(target.DownedTurnsRemaining==1?100f:0f);
            return value;
        }

        private static float ShieldAmount(BattleUnit caster,SkillEffectData effect) => Mathf.Max(0,effect.BaseValue
            +Mathf.FloorToInt(CombatDamageValueCalculator.CalculateDamageValue(caster,DamageValueSource.AverageAttack,1f)*effect.ShieldAverageAttackMultiplier));

        private static float ShieldUtility(CombatSession session,BattleUnit caster,BattleUnit target,SkillEffectData effect, BattleUnit observer = null, BattleUnit observedRecipient = null)
        {
            var amount=ShieldAmount(caster,effect);
            if(target.IsPlayerSide!=caster.IsPlayerSide)return -amount;
            var existing=target.Character.ShieldPool?.TotalShield ?? 0;
            var threat=IncomingThreat(session,observer ?? caster,observedRecipient ?? target)*Mathf.Clamp(effect.DurationTurns,1,2);
            return Mathf.Min(amount,Mathf.Max(0,threat-existing));
        }

        private static float SharedFocusMultiplier(CombatSession session,BattleUnit caster,BattleUnit target)
        {
            if(target.IsPlayerSide==caster.IsPlayerSide)return 1f;
            var sight=new CombatLineOfSightService();
            var support=0;
            foreach(var ally in session.Units)
            {
                if(ally==caster || ally?.Character?.LifeState!=CharacterLifeState.Alive || ally.IsPlayerSide!=caster.IsPlayerSide
                    || StatusEffectSystem.IsHardCrowdControlled(ally) || session.Contact!=null && !session.Contact.Sees(ally,target))continue;
                var basic=ally.Character.GetBasicAttackSkill();
                if(basic==null || PassiveEffectCombatExecutionSystem.ResolveResourceCost(ally,basic)>(ally.Character.ResourcePool?.CurrentValue ?? 0))continue;
                if(basic.Effects.Any(e=>e?.EffectType==EffectType.DealDamage && e.TargetModeIndex>=0 && e.TargetModeIndex<basic.TargetModes.Count
                    && CombatTargetingService.IsPositionWithinRange(ally.Position,target.Position,basic.TargetModes[e.TargetModeIndex].AbsoluteRange))
                    && (!basic.RequiresLineOfSight || sight.HasLineOfSight(session,ally.Position,target.Position)))support++;
                if(support==3)break;
            }
            return 1f+.08f*support;
        }

        private static float DamageUtility(CombatSession session,BattleUnit caster,BattleUnit target,SkillData skill,SkillEffectData authored, CombatSession observations = null, BattleUnit observer = null)
        {
            var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,authored);
            var expected=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,skill,effect.TargetModeIndex,authored);
            var shield=target.Character.ShieldPool?.TotalShield ?? 0;
            var useful=Mathf.Min(expected,Mathf.Max(0,target.Character.CurrentHealth)+shield);
            var kill=CombatSkillPreviewCalculator.EstimateKillChance(session,caster,target,skill,authored);
            var value=useful+Ai(Godmist.Data.Configuration.GameDefineIds.AiLethalDamageBonus,10000f)*kill;
            if(target.IsPlayerSide==caster.IsPlayerSide)return -value;
            value*=SharedFocusMultiplier(session,caster,target);
            var healthDamage=Mathf.Min(target.Character.CurrentHealth,Mathf.Max(0,expected-shield));
            float Recovery(BattleUnit recipient,float amount)
            {
                if(observations==null)return HealingUtility(session,caster,recipient,amount);
                var index=session.Units.IndexOf(recipient);
                return index>=0 ? HealingUtility(observations,observer,observations.Units[index],amount) : amount;
            }
            value+=Recovery(caster,Mathf.Min(GetMissingHealth(caster),Mathf.Floor(healthDamage*effect.LifeStealPercent)));
            if(effect.AllyLifeStealPercent>0)
                foreach(var ally in session.Units.Where(u=>u?.Character!=null && u.IsPlayerSide==caster.IsPlayerSide))
                    value+=Recovery(ally,Mathf.Min(GetMissingHealth(ally),Mathf.Floor(healthDamage*effect.AllyLifeStealPercent)));
            return value;
        }

        private static float StatusUtility(CombatSession session,BattleUnit caster,BattleUnit target,SkillData skill,SkillEffectData effect)
        {
            if(string.IsNullOrEmpty(effect.StatusId))return 0f;
            var chance=StatusEffectSystem.CalculateStatusApplicationChance(caster,target,effect.StatusId,effect.BaseChance);
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,target,skill,effect.TargetModeIndex);
            if(preview.StatusId==effect.StatusId)chance=preview.StatusChance;
            else if(!effect.AlwaysHits && effect.Polarity==EffectPolarity.Negative)
                chance*=CombatDamageSystem.CalculateSkillHitChance(caster,target,skill);
            if(StatusEffectSystem.IsDamageOverTimeStatus(effect.StatusId))
            {
                var tick=effect.DamageValueSource==DamageValueSource.AlchemicalPower
                    ? StatusEffectSystem.CalculateStatusDamagePerTurn(caster,effect)
                    : CombatDamageValueCalculator.CalculateDamageValue(caster,DamageValueSource.AverageAttack,effect.StatusDamageMultiplier);
                var value=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(target,effect.StatusId,tick)*Mathf.Min(2,Mathf.Max(1,effect.DurationTurns))*.7f;
                return chance*value*(target.IsPlayerSide==caster.IsPlayerSide?-1f:1f);
            }
            if(StatusEffectSystem.HasStatus(target,effect.StatusId))return 0f;
            if(IsHardControl(effect.StatusId))
            {
                if(target.IsPlayerSide==caster.IsPlayerSide)return -100f*chance;
                var lost=ControlDenial(session,caster,target);
                // Rooting a ranged attacker already able to shoot denies no attack.
                if(effect.StatusId==StatusEffectIds.Immobilize && GetPreferredCombatRange(target)>1 && TacticalDistance(target.Position,caster.Position)>1)lost*=.1f;
                return chance*Mathf.Min(Ai(Godmist.Data.Configuration.GameDefineIds.AiHardControlBonus,300f),lost)*Mathf.Clamp(effect.DurationTurns,1,2);
            }
            if(effect.StatusId==StatusEffectIds.Invisibility && target.IsPlayerSide==caster.IsPlayerSide)
                return Mathf.Min(100f,IncomingThreat(session,caster,target));
            if(effect.StatusId==StatusEffectIds.Regeneration && target.IsPlayerSide==caster.IsPlayerSide)
                return HealingUtility(session,caster,target,Mathf.Min(GetMissingHealth(target),target.Character.Stats.MaxHealth.GetValue()*effect.StatusHealPercent*Mathf.Clamp(effect.DurationTurns,1,2)));
            if(target.IsPlayerSide!=caster.IsPlayerSide)
            {
                var denial=ControlDenial(session,caster,target);
                if(effect.StatusId==StatusEffectIds.Weakening)return chance*denial*.2f*Mathf.Clamp(effect.DurationTurns,1,2);
                if(effect.StatusId==StatusEffectIds.Slow)return chance*denial*.35f*Mathf.Clamp(effect.DurationTurns,1,2);
                if(effect.StatusId==StatusEffectIds.Chill)return chance*denial*Mathf.Clamp01(1f-20f/Mathf.Max(1f,target.Character.Stats.Speed.GetValue()));
                if(effect.StatusId==StatusEffectIds.Taunt)return chance*denial*.25f;
            }
            return chance*Ai(Godmist.Data.Configuration.GameDefineIds.AiSoftControlBonus,100f);
        }

        private static float CleanseUtility(CombatSession session,BattleUnit caster,BattleUnit target,string id)
        {
            if(target.IsPlayerSide!=caster.IsPlayerSide || string.IsNullOrEmpty(id))return 0;
            var active=target.Character.PassiveEffects.FirstOrDefault(e=>e!=null && e.EffectId==id && PassiveEffectLifecycleSystem.IsActive(e));
            if(active==null || id==StatusEffectIds.Invisibility || id==StatusEffectIds.Regeneration)return 0;
            if(IsHardControl(id))return Mathf.Max(20,IncomingThreat(session,caster,target));
            var dot=active.GetPayload<DamageOverTimePayload>();
            if(dot!=null)return Mathf.Min(target.Character.CurrentHealth,DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(target,id,
                Mathf.CeilToInt(dot.RemainingTotalDamage/(float)Mathf.Max(1,active.RemainingDuration)))*Mathf.Min(2,active.RemainingDuration));
            return id==StatusEffectIds.Weakening || id==StatusEffectIds.Slow || id==StatusEffectIds.Chill || id==StatusEffectIds.Taunt ? 30f : 0f;
        }
        private float TrapPlacementValue(CombatSession session,BattleUnit caster,SkillData skill,GridPosition cell)
        {
            var definition=skill.Effects[0].SummonedObjectDefinition;
            if(definition==null || definition.EffectTargetingType==TargetingType.Allies)return 0f;
            var value=0f;
            foreach(var enemy in session.Units.Where(u=>IsObservedLivingOpponent(session,caster,u)).OrderBy(u=>TacticalDistance(cell,u.Position)).Take(6))
            {
                var phaseDetonation=BanditLeaderBossSystem.HasPendingTrapDetonation(caster)
                    && CombatTargetingService.IsPositionWithinRange(cell,enemy.Position,definition.EffectRange);
                if(!phaseDetonation && (StatusEffectSystem.IsImmobilized(enemy) || StatusEffectSystem.IsHardCrowdControlled(enemy)))continue;
                var distance=TacticalDistance(cell,enemy.Position);
                if(!phaseDetonation && (distance>3 || distance<1))continue;
                // An enemy already firing from range need not walk into a trap.
                if(!phaseDetonation && !StatusEffectSystem.IsInvisible(caster) && GetAvailableRangedAttacks(enemy).Any() && CanUseRangedSkillAgainst(session,caster,enemy.Position,GetAvailableRangedAttacks(enemy)))continue;
                if(!phaseDetonation && TacticalDistance(cell,caster.Position)>=TacticalDistance(enemy.Position,caster.Position))continue;
                if(!phaseDetonation && distance==1 && !session.CanMoveBetween(enemy,enemy.Position,cell))continue;
                var benefit=0f;
                foreach(var effect in definition.Effects)
                {
                    if(effect.EffectType==SummonedObjectEffectType.DealDamage)
                    {
                        var raw=effect.BaseValue+CombatDamageValueCalculator.CalculateDamageValue(caster,effect.DamageValueSource==DamageValueSource.AttackRange?DamageValueSource.AverageAttack:effect.DamageValueSource,effect.DamageMultiplier);
                        var amount=CombatDamageSystem.ApplyDefense(caster,enemy,raw,effect.DamageType);
                        var hit=CombatDamageSystem.IsGuaranteedHitDamageType(effect.DamageType)?1f:
                            CombatDamageSystem.CalculateHitChance(caster.Character.WeaponAccuracy,definition.EffectAccuracy,enemy.Character.Stats.Dodge.GetValue());
                        benefit+=Mathf.Min(amount,enemy.Character.CurrentHealth)*hit;
                    }
                    else if(effect.EffectType==SummonedObjectEffectType.ApplyStatus)
                        benefit+=StatusEffectSystem.CalculateStatusApplicationChance(caster,enemy,effect.StatusId,effect.BaseChance)*(IsHardControl(effect.StatusId)?100f:25f);
                }
                var exits=session.GetNavigationNeighbours(cell).Count(c=>session.Grid.IsWalkable(c) && !session.Grid.IsBlocked(c) && session.CanMoveBetween(enemy,cell,c));
                value+=phaseDetonation?benefit*.5f:benefit*(exits<=2?.8f:.35f)/distance;
            }
            if(definition.EffectTargetingType==TargetingType.All && definition.EntryAffectsArea)
                value-=session.Units.Count(u=>u?.Character?.LifeState==CharacterLifeState.Alive && u.IsPlayerSide==caster.IsPlayerSide
                    && CombatTargetingService.IsPositionWithinRange(cell,u.Position,definition.EffectRange))*100f;
            return value;
        }

        private int RemainingApAfterRoute(CombatSession session,BattleUnit actor,System.Collections.Generic.IReadOnlyList<GridPosition> route)
        {
            var copy=CopyPlanningUnit(actor); var previous=actor.Position;
            foreach(var cell in route)
            {
                if(FindClosedDirectDoor(session,previous,cell)!=null)copy.CurrentActionPoints-=CombatInteractionSystem.CalculateActionCost(copy);
                var cost=WarriorCombatSystem.FreeStep(copy)?0:PassiveEffectCombatExecutionSystem.ApplyMovementCost(session,copy,
                    CombatActionCostCalculator.CalculateMovementCost(copy))+session.Grid.GetMovementSurcharge(previous,cell)*ActionPointUnits.PerPoint;
                copy.CurrentActionPoints-=cost;
                WarriorCombatSystem.Moved(copy);copy.MovementStepsThisTurn++;copy.Position=cell;
                if(copy.CurrentActionPoints<0)return copy.CurrentActionPoints;
                previous=cell;
            }
            return copy.CurrentActionPoints;
        }

    }
}
