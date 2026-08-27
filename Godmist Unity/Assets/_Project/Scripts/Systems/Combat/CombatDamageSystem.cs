using System;
using Godmist.Core.Enums;
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
        private readonly ResourceRuleSystem resourceRuleSystem;
        private readonly CombatVariableSystem combatVariableSystem;
        private readonly CombatFeedbackBus feedbackBus;

        public CombatDamageSystem(Action<string> battleLog = null,
            Action<CombatSession> evaluateOutcome = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            ResourceRuleSystem resourceRuleSystem = null,
            CombatVariableSystem combatVariableSystem = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.evaluateOutcome = evaluateOutcome;
            this.removeUnitFromCombat = removeUnitFromCombat;
            this.resourceRuleSystem = resourceRuleSystem;
            this.combatVariableSystem = combatVariableSystem ?? new CombatVariableSystem();
            this.feedbackBus = feedbackBus;
        }
        public CombatDamageResult ResolveAndApplyDamage(
            CombatSession session,
            BattleUnit source,
            BattleUnit target,
            int attackValue,
            DamageType damageType,
            float skillAccuracy,
            float bonusDefensePenetration = 0f)
        {
            if (source?.Character == null || target?.Character == null || attackValue <= 0)
            {
                return new CombatDamageResult(false, false, 0);
            }

            if (StatusEffectSystem.IsParalyzed(target))
            {
                return new CombatDamageResult(false, false, 0);
            }

            var wasHit = IsGuaranteedHitDamageType(damageType)
                || RollHit(source, target, skillAccuracy);
            if (wasHit && TryConsumeUniqueBuff(target, UniqueBuffType.GuaranteedDodge))
            {
                wasHit = false;
            }

            if (!wasHit)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} chybia {GetDisplayName(target)}.");
                feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Miss));

                return new CombatDamageResult(false, false, 0);
            }

            var damageAfterFreeze = ApplyFreezeDamageBonus(target, attackValue);
            var isCritical = RollCritical(source, damageType);
            var damageAfterCrit = ApplyCriticalDamage(
                source,
                damageAfterFreeze,
                isCritical);

            var finalDamage = ApplyDefense(
                source,
                target,
                damageAfterCrit,
                damageType,
                bonusDefensePenetration);

            if (isCritical)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} trafia krytycznie.");
                feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Critical));
            }

            var healthDamage = ApplyDamage(
                session,
                source,
                target,
                finalDamage,
                damageType);

            return new CombatDamageResult(true, isCritical, healthDamage);
        }

        public CombatDamageResult ResolveAndApplyDamage(
            CombatSession session,
            BattleUnit source,
            BattleUnit target,
            int attackValue,
            DamageType damageType,
            bool wasHit,
            float bonusDefensePenetration = 0f)
        {
            if (source?.Character == null
                || target?.Character == null
                || attackValue <= 0
                || !wasHit
                || StatusEffectSystem.IsParalyzed(target)
                || TryConsumeUniqueBuff(target, UniqueBuffType.GuaranteedDodge))
            {
                return new CombatDamageResult(false, false, 0);
            }

            var damageAfterFreeze = ApplyFreezeDamageBonus(target, attackValue);
            var isCritical = RollCritical(source, damageType);
            var damageAfterCrit = ApplyCriticalDamage(
                source,
                damageAfterFreeze,
                isCritical);

            var finalDamage = ApplyDefense(
                source,
                target,
                damageAfterCrit,
                damageType,
                bonusDefensePenetration);

            if (isCritical)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} trafia krytycznie.");
                feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Critical));
            }

            var healthDamage = ApplyDamage(
                session,
                source,
                target,
                finalDamage,
                damageType);

            return new CombatDamageResult(true, isCritical, healthDamage);
        }

        public int ApplyDamage(CombatSession session, BattleUnit source, BattleUnit target, 
            int requestedDamage, DamageType damageType, bool allowDamageRedirect = true)
        {
            if (session == null || target?.Character == null)
            {
                return 0;
            }
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
            var incomingDamage = combatVariableSystem.ApplyDamageModifiers(source, target, requestedDamage);
            if (incomingDamage <= 0)
            {
                return 0;
            }
            if (target.Character.LifeState == CharacterLifeState.Downed)
            {
                AccelerateDownedDeath(session, target);
                evaluateOutcome?.Invoke(session);
                return 0;
            }
            var remainingDamage = incomingDamage;

            var redirectedHealthDamage = 0;
            if (allowDamageRedirect
                && TryResolveDamageRedirect(session, source, target, incomingDamage,
                    damageType, out var redirectedDamage, out redirectedHealthDamage))
            {
                remainingDamage = Mathf.Max(0, incomingDamage - redirectedDamage);
            }

            var damageBeforeShield = remainingDamage;

            remainingDamage = AbsorbShield(target, remainingDamage);

            var shieldDamage = damageBeforeShield - remainingDamage;
            if (shieldDamage > 0) 
            {
                battleLog?.Invoke($"{target.Character.Definition.DisplayName} traci {shieldDamage} tarczy.");
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    target, CombatFeedbackType.ShieldLoss, shieldDamage));
            }
            var healthDamage = Mathf.Min(target.Character.CurrentHealth, remainingDamage);
            target.Character.CurrentHealth -= healthDamage;

            if (healthDamage > 0)
            {
                var damageText = GetDamageText(damageType);

                battleLog?.Invoke($"{target.Character.Definition.DisplayName} otrzymuje {healthDamage} {damageText}");
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    target, CombatFeedbackType.Damage, healthDamage, damageType));
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
                }
                else
                {
                    MarkDead(session, target);
                }
            }
            if (source != null && !IsDamageOverTime(damageType) && (shieldDamage > 0 || healthDamage > 0))
            {
                resourceRuleSystem?.ProcessDamageDealt(source);
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
        public static float CalculateHitChance(float weaponAccuracy, float skillAccuracy, float dodge)
        {
            var accuracy = (Mathf.Max(0f, weaponAccuracy) + Mathf.Max(0f, skillAccuracy)) * 0.5f;

            var targetDodge = Mathf.Max(0f, dodge);

            if (accuracy <= 0f)
                return 0f;

            var chancePercent = accuracy * accuracy / (accuracy + targetDodge);

            return Mathf.Clamp01(chancePercent / 100f);
        }

        public static bool RollHit(
            BattleUnit source,
            BattleUnit target,
            float skillAccuracy)
        {
            if (source?.Character == null || target?.Character == null)
            {
                return false;
            }

            var dodge = target.Character.Stats == null
                ? 0f
                : target.Character.Stats.Dodge.GetValue();

            var chance = CalculateHitChance(
                source.Character.WeaponAccuracy,
                skillAccuracy,
                dodge);

            return UnityEngine.Random.value <= chance;
        }

        public static bool IsGuaranteedHitDamageType(DamageType damageType)
        {
            return damageType != DamageType.Physical;
        }

        private static bool RollCritical(BattleUnit source, DamageType damageType)
        {
            if (damageType != DamageType.Physical && damageType != DamageType.Inevitable)
                return false;

            if (TryConsumeUniqueBuff(source, UniqueBuffType.GuaranteedCriticalHit))
            {
                return true;
            }

            var chance = source.Character.Stats == null ? 0f : source.Character.Stats.CritChance.GetValue();

            return UnityEngine.Random.value <= Mathf.Clamp01(chance);
        }

        private static int ApplyCriticalDamage(BattleUnit source, int damage, bool isCritical)
        {
            if (!isCritical) return damage;

            var multiplier = source.Character.Stats == null ? 0f : source.Character.Stats.CritDamage.GetValue();

            return Mathf.CeilToInt(damage * Mathf.Max(1f, multiplier));
        }

        public static int ApplyDefense(
            BattleUnit source,
            BattleUnit target,
            int damage,
            DamageType damageType,
            float bonusDefensePenetration = 0f)
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

            var penetration = Mathf.Clamp01(
                GetDefensePenetration(source, damageType)
                + Mathf.Clamp01(bonusDefensePenetration));
            defense = Mathf.Max(0f, defense * (1f - penetration));

            if (defense <= 0f) return damage;

            return Mathf.CeilToInt(damage * damage / (damage + defense));
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
                DamageType.Physical => Mathf.Clamp01(
                    stats.PhysicalDefensePenetration.GetValue()),
                DamageType.Magical => Mathf.Clamp01(
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

        private int ApplyFreezeDamageBonus(BattleUnit target, int damage)
        {
            if (!StatusEffectSystem.TryBreakFreeze(
                    target,
                    out var remainingTurns))
            {
                return damage;
            }

            var multiplier = 1f + remainingTurns * 0.5f;
            var boostedDamage = Mathf.CeilToInt(damage * multiplier);

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} traci Freeze. " +
                $"Uderzenie zadaje {boostedDamage} obrażeń.");

            return boostedDamage;
        }
    }
}
