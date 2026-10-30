using System;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatDamageSystem
    {
        private readonly Action<string> battleLog;
        private readonly Action<CombatSession> evaluateOutcome;
        private readonly Action<BattleUnit> removeUnitFromCombat;
        private readonly CombatVariableSystem combatVariableSystem;
        private readonly CombatFeedbackBus feedbackBus;
        private readonly PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;

        public CombatDamageSystem(Action<string> battleLog = null,
            Action<CombatSession> evaluateOutcome = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            CombatVariableSystem combatVariableSystem = null,
            CombatFeedbackBus feedbackBus = null,
            PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher = null)
        {
            this.battleLog = battleLog;
            this.evaluateOutcome = evaluateOutcome;
            this.removeUnitFromCombat = removeUnitFromCombat;
            this.combatVariableSystem = combatVariableSystem ?? new CombatVariableSystem();
            this.feedbackBus = feedbackBus;
            this.passiveEffectEventDispatcher = passiveEffectEventDispatcher;
        }
        public CombatDamageResult ResolveAndApplyDamage(
            CombatSession session,
            BattleUnit source,
            BattleUnit target,
            int attackValue,
            DamageType damageType,
            float skillAccuracy,
            float bonusDefensePenetration = 0f,
            PassiveEffectCombatContext passiveContext = null,
            bool ignoreDefense = false)
        {
            if (source?.Character == null || target?.Character == null || attackValue <= 0)
            {
                return new CombatDamageResult(false, false, 0);
            }

            var eventContext = passiveContext ?? new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                damageType: damageType);
            Publish(session, PassiveEffectTriggerType.OnAttack, eventContext);

            if (StatusEffectSystem.IsParalyzed(target))
            {
                return new CombatDamageResult(false, false, 0);
            }

            var wasHit = IsGuaranteedHitDamageType(damageType)
                || RollHit(source, target, skillAccuracy, eventContext.Skill);
            var wasDodged = false;
            if (wasHit && TryConsumeUniqueBuff(target, UniqueBuffType.GuaranteedDodge))
            {
                wasHit = false;
                wasDodged = true;
            }

            if (!wasHit)
            {
                if (!wasDodged
                    && PassiveEffectCombatExecutionSystem.TryConvertMiss(
                        session,
                        eventContext))
                {
                    wasHit = true;
                }
            }

            if (!wasHit)
            {
                if (session.Diagnostics?.Enabled == true)
                    session.Diagnostics.Record("miss", session, source, $"target={target.Character.CharacterId};dodged={wasDodged};type={damageType}");
                battleLog?.Invoke($"{GetDisplayName(source)} chybia {GetDisplayName(target)}.");
                feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Miss, damageType: damageType, source: source));

                Publish(
                    session,
                    wasDodged
                        ? PassiveEffectTriggerType.OnDodge
                        : PassiveEffectTriggerType.OnMiss,
                    eventContext.WithOutcome(
                        succeeded: false,
                        wasDodged: wasDodged,
                        wasMissed: !wasDodged));
                return new CombatDamageResult(false, false, 0);
            }

            return ResolveSuccessfulHit(session, source, target, attackValue, damageType,
                bonusDefensePenetration, eventContext, ignoreDefense);
        }

        public CombatDamageResult ResolveAndApplyDamage(
            CombatSession session,
            BattleUnit source,
            BattleUnit target,
            int attackValue,
            DamageType damageType,
            bool wasHit,
            float bonusDefensePenetration = 0f,
            PassiveEffectCombatContext passiveContext = null,
            bool ignoreDefense = false)
        {
            if (source?.Character == null
                || target?.Character == null
                || attackValue <= 0
                || StatusEffectSystem.IsParalyzed(target))
            {
                return new CombatDamageResult(false, false, 0);
            }

            var eventContext = passiveContext ?? new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                damageType: damageType);
            Publish(session, PassiveEffectTriggerType.OnAttack, eventContext);
            if (!wasHit)
            {
                if (PassiveEffectCombatExecutionSystem.TryConvertMiss(
                        session,
                        eventContext))
                {
                    wasHit = true;
                }
            }

            if (!wasHit)
            {
                Publish(session, PassiveEffectTriggerType.OnMiss,
                    eventContext.WithOutcome(succeeded: false, wasMissed: true));
                return new CombatDamageResult(false, false, 0);
            }
            if (TryConsumeUniqueBuff(target, UniqueBuffType.GuaranteedDodge))
            {
                Publish(session, PassiveEffectTriggerType.OnDodge,
                    eventContext.WithOutcome(succeeded: false, wasDodged: true));
                return new CombatDamageResult(false, false, 0);
            }

            return ResolveSuccessfulHit(session, source, target, attackValue, damageType,
                bonusDefensePenetration, eventContext, ignoreDefense);
        }

        // Accuracy and dodge have already been resolved by the entry point.
        // This stage must not roll hit chance again for an externally supplied hit.
        private CombatDamageResult ResolveSuccessfulHit(CombatSession session, BattleUnit source, BattleUnit target,
            int attackValue, DamageType damageType, float bonusDefensePenetration,
            PassiveEffectCombatContext eventContext, bool ignoreDefense)
        {
            ScoutCombatSystem.Face(source, target.Position);
            attackValue += SkeletonSlayerBossSystem.AttackBonus(source);
            attackValue = Mathf.RoundToInt(attackValue * ScoutCombatSystem.DamageMultiplier(eventContext) * WarriorCombatSystem.DamageMultiplier(eventContext) * MageCombatSystem.DamageMultiplier(eventContext) * PaladinCombatSystem.DamageMultiplier(eventContext) * HawkeyeDamageMultiplier(source, target, damageType));
            attackValue = Mathf.RoundToInt(attackValue * PaladinCombatSystem.ConsumeExposure(target,true));
            var freezeMultiplier = ConsumeFreezeDamageMultiplier(target);
            var isCritical = RollCritical(session, source, damageType, eventContext)
                && !EquipmentEffectSystem.AvoidCritical(target.Character);
            Publish(session, PassiveEffectTriggerType.OnHit,
                eventContext.WithOutcome(succeeded: true));
            if (isCritical)
            {
                Publish(session, PassiveEffectTriggerType.OnCriticalHit,
                    eventContext.WithOutcome(succeeded: true, isCritical: true));
            }
            // Critical/freeze bonuses multiply damage after the non-linear armor reduction.
            // Keep the unmitigated equivalent for consumers explicitly requesting pre-mitigation damage.
            var baseDamage = StatusEffectSystem.HasStatus(source, Godmist.Core.Constants.StatusEffectIds.Weakening)
                ? Mathf.CeilToInt(attackValue * .8f) : attackValue;
            var damageAfterCrit = ApplyPostDefenseBonuses(source, target, baseDamage, isCritical, freezeMultiplier);
            var defended = ignoreDefense ? baseDamage : ApplyDefense(source, target, baseDamage, damageType,
                bonusDefensePenetration, WarriorDischargeRules.DefenseIgnore(source, eventContext.Skill,
                    eventContext.DischargeCast?.RageSpent, damageType));
            var finalDamage = ApplyPostDefenseBonuses(source, target, defended, isCritical, freezeMultiplier);

            if (isCritical)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} trafia krytycznie.");
                feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Critical));
            }

            finalDamage = ScoutCombatSystem.ResolveThroatCut(session, eventContext, finalDamage);
            var healthDamage = ApplyDamage(
                session,
                source,
                target,
                finalDamage,
                damageType,
                passiveContext: eventContext.WithOutcome(
                    succeeded: true,
                    isCritical: isCritical), preMitigationDamage: damageAfterCrit);
            PaladinCombatSystem.AfterAttack(session,source,target,eventContext.WithOutcome(isCritical:isCritical),damageAfterCrit,bonusDefensePenetration,healthDamage,this);

            if (target.Character.LifeState == CharacterLifeState.Dead)
            {
                Publish(session, PassiveEffectTriggerType.OnKill,
                    eventContext.WithOutcome(
                        succeeded: true,
                        isCritical: isCritical,
                        healthDamage: healthDamage,
                        eventValue: healthDamage));
            }

            ScoutCombatSystem.RecordHit(source, target, damageType);
            ScoutCombatSystem.AfterCritical(eventContext, isCritical);
            WarriorCombatSystem.AfterHit(session,eventContext,isCritical,damageAfterCrit,finalDamage,this);
            return new CombatDamageResult(true, isCritical, healthDamage);
        }

        public int ApplyDamage(CombatSession session, BattleUnit source, BattleUnit target, 
            int requestedDamage, DamageType damageType, bool allowDamageRedirect = true,
            PassiveEffectCombatContext passiveContext = null, bool allowWarriorProcs = true,
            int? preMitigationDamage = null)
        {
            if (session == null || target?.Character == null)
            {
                return 0;
            }
            if (target.Character.Definition?.Id == DeadKingBossSystem.KnightId && IsDamageOverTime(damageType)) return 0;
            CombatStallWarningSystem.Synchronize(session);
            if (target.Character.LifeState == CharacterLifeState.Dead)
            {
                return 0;
            }
            if (StatusEffectSystem.IsParalyzed(target))
            {
                return 0;
            }
            if (target.Character.DebugIsInvulnerable)
            {
                battleLog?.Invoke(
                    $"{target.Character.Definition.DisplayName} ignoruje obrażenia (godmode).");

                return 0;
            }
            var incomingDamage=CalculateIncomingBeforeRedirect(session,source,target,requestedDamage,damageType,passiveContext,variables:combatVariableSystem);
            if (incomingDamage <= 0)
            {
                SkeletonSlayerBossSystem.RecordPreMitigation(target,preMitigationDamage ?? requestedDamage);
                return 0;
            }
            var damageContext = passiveContext ?? new PassiveEffectCombatContext(
                session: session,
                source: source,
                target: target,
                damageType: damageType);
            if (PassiveEffectCombatExecutionSystem.TryPreventDamage(
                    session,
                    damageContext,
                    damageType))
            {
                battleLog?.Invoke(
                    $"{GetDisplayName(target)} absorbuje obrażenia magiczne.");
                return 0;
            }
            if (target.Character.LifeState == CharacterLifeState.Downed)
            {
                AccelerateDownedDeath(session, target);
                evaluateOutcome?.Invoke(session);
                return 0;
            }
            SkeletonSlayerBossSystem.RecordPreMitigation(target,preMitigationDamage ?? requestedDamage);
            var remainingDamage = incomingDamage;

            var redirectedHealthDamage = 0;
            if (allowDamageRedirect
                && TryResolveDamageRedirect(session, source, target, incomingDamage,
                    damageType, out var redirectedDamage, out redirectedHealthDamage))
            {
                remainingDamage = Mathf.Max(0, incomingDamage - redirectedDamage);
            }

            remainingDamage = MageCombatSystem.AdaptIncoming(source, target, remainingDamage);
            remainingDamage = Mathf.Max(0, Mathf.RoundToInt(remainingDamage
                * UniqueEquipmentCombatRules.IncomingMultiplier(target, damageType,
                    passiveContext?.AttackType ?? AttackType.Unknown)));
            var damageBeforeShield = remainingDamage;

            remainingDamage = AbsorbShield(target, remainingDamage);

            var shieldDamage = damageBeforeShield - remainingDamage;
            if (shieldDamage > 0) 
            {
                battleLog?.Invoke($"{target.Character.Definition.DisplayName} traci {shieldDamage} tarczy.");
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    target, CombatFeedbackType.ShieldLoss, shieldDamage, damageType, source: source));
            }
            if(WarriorCombatSystem.DeathProtected(target))remainingDamage=Mathf.Min(remainingDamage,Mathf.Max(0,target.Character.CurrentHealth-1));
            remainingDamage = DeadKingBossSystem.KnightDamage(target, remainingDamage, damageType);
            var healthBeforeDamage = target.Character.CurrentHealth;
            var healthDamage = Mathf.Min(healthBeforeDamage, remainingDamage);
            UniqueEquipmentCombatRules.RecordHealthDamage(target, damageType, healthDamage);
            BanditLeaderBossSystem.RecordHealthDamage(target,healthDamage);
            target.Character.CurrentHealth -= healthDamage;
            UniqueEquipmentCombatRules.TryPoacherShield(target);
            if (healthDamage > 0 && !target.IsPlayerSide)
                session.EnemyHealthChanged?.Invoke(target, healthBeforeDamage);
            if (session.Diagnostics?.Enabled == true)
                session.Diagnostics.Record("damage", session, source,
                    $"target={target.Character.CharacterId};targetDefinition={target.Character.Definition?.Id};playerTarget={target.IsPlayerSide};type={damageType};hpDamage={healthDamage};shieldDamage={shieldDamage};hpBefore={healthBeforeDamage};hpAfter={target.Character.CurrentHealth};requested={requestedDamage};preMitigation={preMitigationDamage};critical={passiveContext?.IsCritical};skill={passiveContext?.SkillId};action={passiveContext?.ActionId}");
            session.DamageApplied?.Invoke(source, target, healthDamage + shieldDamage, damageType, passiveContext?.ActionId);
            WarriorCombatSystem.DamageTaken(target,healthDamage);
            MageCombatSystem.DamageTaken(source,target,healthDamage);
            PaladinCombatSystem.RecordDamage(target,healthDamage);
            PaladinCombatSystem.RecordDamageDealt(source,healthDamage);

            if (healthDamage > 0)
            {
                var damageText = GetDamageText(damageType);

                battleLog?.Invoke($"{target.Character.Definition.DisplayName} otrzymuje {healthDamage} {damageText}");
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    target, CombatFeedbackType.Damage, healthDamage, damageType, source: source));
            }
            if (target.Character.CurrentHealth <= 0)
            {
                target.Character.CurrentHealth = 0;

                if (target.IsPlayerSide)
                {
                    target.Character.LifeState = CharacterLifeState.Downed;
                    target.DownedTurnsRemaining = BattleUnit.DownedTurnLimit;

                    battleLog?.Invoke(
                        $"{target.Character.Definition.DisplayName} zostaje powalony.");
                    feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Downed,
                        damageType: damageType, source: source));
                }
                else
                {
                    MarkDead(session, target);
                }
            }
            if (target.Character.LifeState != CharacterLifeState.Alive && !IsDamageOverTime(damageType))
                MageCombatSystem.ExtinguishBurns(session, target);
            CombatStallWarningSystem.RecordDamage(session, target, healthDamage + shieldDamage);
            if (allowWarriorProcs && source != null && !IsDamageOverTime(damageType) && (shieldDamage > 0 || healthDamage > 0))
            {
                WarriorCombatSystem.DamageDealt(source,target,passiveContext);
                MageCombatSystem.AfterHit(session,source,target,healthDamage,this,feedbackBus,passiveContext);
            }
            if (shieldDamage > 0 || healthDamage > 0)
            {
                ScoutCombatSystem.RecordDamageTaken(source, target, damageType);
                Publish(session, PassiveEffectTriggerType.OnDamageTaken,
                    damageContext.WithOutcome(
                        succeeded: true,
                        healthDamage: healthDamage,
                        eventValue: healthDamage + redirectedHealthDamage));
            }
            evaluateOutcome?.Invoke(session);
            return healthDamage + redirectedHealthDamage;
        }

        public int ApplySummonedObjectDamage(
            CombatSession session,
            BattleUnit target,
            int requestedDamage,
            DamageType damageType)
        {
            if (target?.Character == null || requestedDamage <= 0)
            {
                return 0;
            }

            var finalDamage = ApplyDefense(
                target,
                requestedDamage,
                damageType);

            return ApplyDamage(
                session,
                null,
                target,
                finalDamage,
                damageType);
        }

        private void Publish(
            CombatSession session,
            PassiveEffectTriggerType triggerType,
            PassiveEffectCombatContext context,
            string eventId = null)
        {
            (passiveEffectEventDispatcher ?? session?.PassiveEffectEventDispatcher)
                ?.Publish(triggerType, context, eventId);
        }
        public static float CalculateAccuracy(float weaponAccuracy,float skillAccuracy)
            => (Mathf.Max(0f,weaponAccuracy)+Mathf.Max(0f,skillAccuracy))*.5f;

        public static float CalculateHitChance(float weaponAccuracy, float skillAccuracy, float dodge)
        {
            var accuracy = CalculateAccuracy(weaponAccuracy,skillAccuracy);

            var targetDodge = Mathf.Max(0f, dodge);

            if (accuracy <= 0f)
                return 0f;

            var chancePercent = accuracy * accuracy / (accuracy + targetDodge);

            return Mathf.Clamp01(chancePercent / 100f);
        }

        public static bool RollHit(
            BattleUnit source,
            BattleUnit target,
            float skillAccuracy,
            SkillData skill = null)
        {
            if (source?.Character == null || target?.Character == null)
            {
                return false;
            }

            if (HasUniqueBuff(source, UniqueBuffType.Hawkeye)) return true;
            return UnityEngine.Random.value <= CalculateSkillHitChance(source, target, skill, skillAccuracy);
        }

        public static float CalculateSkillHitChance(
            BattleUnit source, BattleUnit target, SkillData skill, float? skillAccuracy = null)
        {
            if (source?.Character == null || target?.Character == null) return 0f;
            if (HasUniqueBuff(source, UniqueBuffType.Hawkeye)) return 1f;
            var alchemical = CombatDamageValueCalculator.UsesAlchemicalPower(skill);
            var accuracy = alchemical ? 100f : skillAccuracy
                ?? PassiveEffectCombatExecutionSystem.ResolveParameter(source, skill, "accuracy", skill?.SkillAccuracy ?? 0f);
            var chance = CalculateHitChance(alchemical ? 100f : source.Character.WeaponAccuracy,
                accuracy, target.Character.Stats?.Dodge.GetValue() ?? 0f);
            return ApplyHitChanceBonus(source.Character, chance);
        }

        public static bool IsGuaranteedHitDamageType(DamageType damageType)
        {
            return damageType != DamageType.Physical;
        }

        public static float ApplyHitChanceBonus(Godmist.Runtime.Characters.CharacterInstance character, float chance)
        {
            var bonus = new CombatVariableSystem().GetMultiplier(character, GameplayVariableType.HitChanceBonus) - 1f;
            return Mathf.Clamp01(chance + bonus);
        }

        private static bool RollCritical(
            CombatSession session,
            BattleUnit source,
            DamageType damageType,
            PassiveEffectCombatContext context)
        {
            if (damageType is not (DamageType.Physical or DamageType.Magical or DamageType.Inevitable)) return false;

            if (context?.Skill?.GuaranteedCritical == true) return true;
            if (TryConsumeUniqueBuff(source, UniqueBuffType.GuaranteedCriticalHit))
            {
                return true;
            }

            var chance = damageType == DamageType.Magical
                ? MageCombatSystem.CalculateMagicCriticalChance(context)
                : CalculateRegularCriticalChance(session, source, context);
            return damageType==DamageType.Magical ? UnityEngine.Random.value < chance : UnityEngine.Random.value <= chance;
        }

        // No rolls or consumption: used by AI opportunity estimates. Execution
        // keeps its existing guaranteed-buff consumption and random comparison.
        public static float PreviewCriticalChance(CombatSession session, BattleUnit source, BattleUnit target,
            Godmist.Data.Skills.SkillData skill, DamageType damageType, bool ignoreConsumedGuaranteedCritical = false)
        {
            if (source?.Character == null || target?.Character == null) return 0f;
            var avoidance=Mathf.Clamp01(EquipmentEffectSystem.Sum(target.Character,GalduryteEffectType.CriticalAvoidance));
            var context = new PassiveEffectCombatContext(session: session, source: source, target: target,
                skill: skill, skillId: skill?.SkillId, damageType: damageType,
                attackType: skill?.ResolveAttackType(target.Position.X - source.Position.X, target.Position.Y - source.Position.Y) ?? AttackType.Unknown,
                distance: Mathf.Abs(source.Position.X - target.Position.X) + Mathf.Abs(source.Position.Y - target.Position.Y));
            if (damageType is not (DamageType.Physical or DamageType.Magical or DamageType.Inevitable)) return 0f;
            if (skill?.GuaranteedCritical == true || !ignoreConsumedGuaranteedCritical && HasUniqueBuff(source, UniqueBuffType.GuaranteedCriticalHit)) return 1f-avoidance;
            return (damageType == DamageType.Magical ? MageCombatSystem.CalculateMagicCriticalChance(context)
                : CalculateRegularCriticalChance(session, source, context))*(1-avoidance);
        }

        public static float PreviewUntargetedCriticalChance(BattleUnit source,Godmist.Data.Skills.SkillData skill,int chainIndex=0)
        {
            if(source?.Character==null)return 0;
            if(skill?.GuaranteedCritical==true||HasUniqueBuff(source,UniqueBuffType.GuaranteedCriticalHit))return 1;
            var context=new PassiveEffectCombatContext(source:source,skill:skill,skillId:skill?.SkillId,attackType:skill?.AttackType??AttackType.Unknown);
            return CalculateRegularCriticalChance(source.WarriorState.Session,source,context,chainIndex);
        }

        private static float CalculateRegularCriticalChance(CombatSession session, BattleUnit source, PassiveEffectCombatContext context,int untargetedChainIndex=0)
        {
            var chance = context?.DischargeCast?.CritChance ?? (source.Character.Stats == null ? 0f : source.Character.Stats.CritChance.GetValue());
            chance += MageCombatSystem.BurningCriticalChance(source,context?.Target);
            chance = PassiveEffectCombatExecutionSystem.ApplyCriticalChance(
                session,
                context,
                chance);

            chance = (chance+ScoutCombatSystem.CriticalChance(context)+(context?.Target==null&&context?.SkillId=="skill.scout.ricochet"?ScoutCombatSystem.ChainCriticalChance(untargetedChainIndex):0))*WarriorCombatSystem.CriticalMultiplier(source,context?.Target)*PaladinCombatSystem.CriticalMultiplier(source,context?.Target);
            return Mathf.Clamp01(chance);
        }



        public static int ApplyDefense(
            BattleUnit source,
            BattleUnit target,
            int damage,
            DamageType damageType,
            float bonusDefensePenetration = 0f, float defenseIgnorePercent = 0f)
        {
            if (damage <= 0 || target?.Character == null) return 0;
            var stats = target.Character.Stats;
            var defense = 0f;
            if (stats != null)
            {
                defense = damageType switch
                {
                    DamageType.Physical => stats.PhysicalDefense.GetValue(),
                    DamageType.Magical => stats.MagicalDefense.GetValue(),
                    _ => 0f
                };
            }

            var penetration = PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(source, target, damageType)
                + GetDefensePenetration(source, damageType)
                + (damageType == DamageType.Physical ? ScoutCombatSystem.AnalysisPenetration(source) : 0f)
                + Mathf.Max(0f, bonusDefensePenetration);
            defense = Mathf.Max(0f, defense) * (1f - Mathf.Clamp01(defenseIgnorePercent)) * WarriorCombatSystem.DefenseMultiplier(target,null) / (1f + penetration);

            if (defense <= 0f) return damage;

            return Mathf.CeilToInt((float)damage * damage / (damage + defense));
        }

        public static int CalculateIncomingBeforeRedirect(CombatSession session,BattleUnit source,BattleUnit target,
            int requestedDamage,DamageType damageType,PassiveEffectCombatContext passiveContext,
            CombatValueExplanation explanation=null,Godmist.Data.Skills.SkillData previewSkill=null,CombatVariableSystem variables=null)
        {
            var incomingDamage = Mathf.RoundToInt(SkeletonSlayerBossSystem.IncomingMultiplier(target,damageType) * DeadKingBossSystem.DamageMultiplier(session, source, target, passiveContext) * (variables??new CombatVariableSystem()).ApplyDamageModifiers(source, target, requestedDamage,explanation,previewSkill) * (variables??new CombatVariableSystem()).GetDungeonThemeDamageMultiplier(source?.Character, session?.DungeonTheme ?? DungeonTheme.Story) * EquipmentEffectSystem.Outgoing(source, target, damageType, session,explanation) * EquipmentEffectSystem.Incoming(target?.Character, damageType)*WarriorCombatSystem.Received(session,source,target,damageType)*MageCombatSystem.FireOutgoing(source,session));
            if (HasUniqueBuff(target, UniqueBuffType.MageOffering))
                incomingDamage = Mathf.RoundToInt(incomingDamage * (1 + MageCombatSystem.OfferingBonus(target) * (1 - .1f * MageCombatSystem.W(target,"calm"))));
            incomingDamage = Mathf.Max(
                0,
                Mathf.RoundToInt(
                    PassiveEffectCombatExecutionSystem.ApplyDamageMultiplier(
                        session,
                        passiveContext,
                        incomingDamage)));
            explanation?.Add("equipment_outgoing",EquipmentEffectSystem.Outgoing(source,target,damageType,session));
            explanation?.Add("incoming_before_redirect",incomingDamage);
            return incomingDamage;
        }

        public static int ApplyJewelryDamagePassive(
            CombatSession session,
            BattleUnit source,
            int damage)
        {
            if (damage <= 0 || source?.Character == null) return damage;

            return Mathf.Max(
                0,
                Mathf.RoundToInt(
                    damage * new CombatVariableSystem().GetDungeonThemeDamageMultiplier(
                        source.Character,
                        session?.DungeonTheme ?? DungeonTheme.Story)));
        }

        public static int ApplyDefense(
            BattleUnit target,
            int damage,
            DamageType damageType)
        {
            return ApplyDefense(null, target, damage, damageType);
        }

        private static float GetDefensePenetration(
            BattleUnit source,
            DamageType damageType)
        {
            var stats = source?.Character?.Stats;
            if (stats == null)
            {
                return 0f;
            }

            return damageType switch
            {
                DamageType.Physical => Mathf.Max(0f,
                    stats.PhysicalDefensePenetration.GetValue()),
                DamageType.Magical => Mathf.Max(0f,
                    stats.MagicalDefensePenetration.GetValue()),
                _ => 0f
            };
        }

        private static bool IsDamageOverTime(DamageType damageType)
        {
            return damageType == DamageType.Bleed
                || damageType == DamageType.Poison
                || damageType == DamageType.Burn;
        }

        private static string GetDamageText(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Bleed => "obrażeń od krwawienia",
                DamageType.Poison => "obrażeń od zatrucia",
                DamageType.Burn => "obrażeń od podpalenia",
                _ => "obrażeń"
            };
        }

        private static string GetDisplayName(BattleUnit unit)
        {
            if (unit?.Character?.Definition == null)
            {
                return "Jednostka";
            }
            return unit.Character.Definition.DisplayName;
        }
        private static int AbsorbShield(BattleUnit target, int damage)
        {
            if (target?.Character?.ShieldPool == null || damage <= 0) return damage;

            return target.Character.ShieldPool.AbsorbDamage(damage);
        }

        public static float HawkeyeDamageMultiplier(BattleUnit source, BattleUnit target, DamageType type)
        {
            if (target == null || source == null || (type != DamageType.Physical && type != DamageType.Magical)
                || !HasUniqueBuff(source, UniqueBuffType.Hawkeye)) return 1f;
            var distance = Mathf.Max(Mathf.Abs(source.Position.X - target.Position.X),
                Mathf.Abs(source.Position.Y - target.Position.Y));
            return 1f + .05f * distance;
        }

        public static bool HasUniqueBuff(BattleUnit unit, UniqueBuffType buffType)
        {
            return FindUniqueBuff(unit, buffType) != null;
        }

        public static bool TryConsumeUniqueBuff(BattleUnit unit, UniqueBuffType buffType)
        {
            var effect = FindUniqueBuff(unit, buffType);
            if (effect == null) return false;

            effect.Stacks--;
            if (effect.Stacks <= 0)
            {
                unit.Character.PassiveEffects.Remove(effect);
            }
            return true;
        }

        private static PassiveEffectRuntime FindUniqueBuff(
            BattleUnit unit, UniqueBuffType buffType)
        {
            if (unit?.Character?.PassiveEffects == null) return null;

            foreach (var effect in unit.Character.PassiveEffects)
            {
                var payload = effect?.GetPayload<UniqueCombatBuffPayload>();
                if (PassiveEffectLifecycleSystem.IsActive(effect)
                    && payload?.BuffType == buffType
                    && effect.Stacks > 0)
                {
                    return effect;
                }
            }
            return null;
        }

        private bool TryResolveDamageRedirect(
            CombatSession session,
            BattleUnit source,
            BattleUnit protectedUnit,
            int incomingDamage,
            DamageType damageType,
            out int redirectedDamage,
            out int redirectedHealthDamage)
        {
            redirectedDamage = 0;
            redirectedHealthDamage = 0;
            if (session?.Units == null || protectedUnit == null || incomingDamage <= 0)
            {
                return false;
            }

            var redirectPercent = protectedUnit.DamageRedirectPercent;
            var protectorCharacterId = protectedUnit.DamageRedirectProtectorCharacterId;
            if (redirectPercent <= 0f || string.IsNullOrWhiteSpace(protectorCharacterId))
            {
                var runtimeBuff = protectedUnit.Character?.PassiveEffects?.Find(effect =>
                    PassiveEffectLifecycleSystem.IsActive(effect)
                    && effect.GetPayload<DamageRedirectPayload>()?.RedirectPercent > 0f
                    && effect.Sources != null && effect.Sources.Count > 0);
                if (runtimeBuff == null) return false;
                redirectPercent = runtimeBuff
                    .GetPayload<DamageRedirectPayload>()
                    .RedirectPercent;
                protectorCharacterId = runtimeBuff.Sources[0]?.SourceCharacterId;
            }

            var protector = session.Units.Find(unit => unit?.Character != null
                && unit.Character.LifeState == CharacterLifeState.Alive
                && string.Equals(unit.Character.CharacterId,
                    protectorCharacterId,
                    StringComparison.OrdinalIgnoreCase));
            if (protector == null || protector == protectedUnit)
            {
                protectedUnit.DamageRedirectProtectorCharacterId = string.Empty;
                protectedUnit.DamageRedirectPercent = 0f;
                return false;
            }

            var requestedRedirect = Mathf.FloorToInt(incomingDamage
                * Mathf.Clamp01(redirectPercent));
            if (requestedRedirect <= 0)
            {
                return false;
            }

            var mitigatedRedirect = ApplyDefense(
                source,
                protector,
                requestedRedirect,
                damageType);
            redirectedHealthDamage = ApplyDamage(session, null, protector,
                mitigatedRedirect, damageType, allowDamageRedirect: false);
            redirectedDamage = requestedRedirect;
            feedbackBus?.Publish(new CombatFeedbackEvent(
                protectedUnit,
                CombatFeedbackType.DamageRedirected,
                redirectedDamage,
                damageType,
                effectName: "Redirect"));
            feedbackBus?.Publish(new CombatFeedbackEvent(
                protector,
                CombatFeedbackType.DamageRedirected,
                redirectedHealthDamage,
                damageType,
                effectName: "Redirect"));
            return true;
        }
        private void AccelerateDownedDeath(CombatSession session, BattleUnit target)
        {
            target.DownedTurnsRemaining = Math.Max(
                0,
                target.DownedTurnsRemaining - 1);

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} " +
                $"otrzymuje trafienie jako powalony. " +
                $"Pozostale tury: {target.DownedTurnsRemaining}");

            if (target.DownedTurnsRemaining <= 0)
            {
                MarkDead(session, target);
            }
        }

        private void MarkDead(CombatSession session, BattleUnit target)
        {
            ScoutCombatSystem.ReleaseKnivesOnDeath(session, target);
            target.Character.CurrentHealth = 0;
            target.Character.LifeState = CharacterLifeState.Dead;
            StatusEffectSystem.RemoveTauntsAppliedBy(
                session,
                target.Character.CharacterId);
            target.DownedTurnsRemaining = 0;

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} ginie.");

            if (!target.IsPlayerSide)
            {
                session?.TryMarkEnemyDefeated(
                    target.Character.CharacterId,
                    target.Position);
                removeUnitFromCombat?.Invoke(target);
            }
        }

        private float ConsumeFreezeDamageMultiplier(BattleUnit target)
        {
            if (!StatusEffectSystem.TryBreakFreeze(target, out var remainingTurns)) return 1f;
            battleLog?.Invoke($"{target.Character.Definition.DisplayName} traci Freeze.");
            return 1f + remainingTurns * GameDefinesRuntime.GetFloat(GameDefineIds.FreezeDamagePerRemainingTurn, .5f);
        }

        public static float PreviewFreezeDamageMultiplier(BattleUnit target)
        {
            var freeze = target?.Character?.PassiveEffects?.Find(effect => effect?.EffectId == Godmist.Core.Constants.StatusEffectIds.Freeze
                && effect.Lifetime == Godmist.Core.Enums.PassiveEffectLifetime.Timed && effect.RemainingDuration > 0);
            return freeze == null ? 1f : 1f + freeze.RemainingDuration * GameDefinesRuntime.GetFloat(GameDefineIds.FreezeDamagePerRemainingTurn, .5f);
        }

        public static int ApplyPostDefenseBonuses(BattleUnit source, BattleUnit target, int damage, bool critical, float freezeMultiplier = 1f)
        {
            var criticalMultiplier = critical ? Mathf.Max(1f, (source?.Character?.Stats?.CritDamage.GetValue() ?? 1f)
                + ScoutCombatSystem.WeakPointCriticalDamage(target)) : 1f;
            return Mathf.Max(0, Mathf.CeilToInt(damage * criticalMultiplier * freezeMultiplier));
        }
    }
}
