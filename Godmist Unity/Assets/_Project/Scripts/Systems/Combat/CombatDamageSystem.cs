using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
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

        public CombatDamageSystem(Action<string> battleLog = null,
            Action<CombatSession> evaluateOutcome = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            ResourceRuleSystem resourceRuleSystem = null,
            CombatVariableSystem combatVariableSystem = null)
        {
            this.battleLog = battleLog;
            this.evaluateOutcome = evaluateOutcome;
            this.removeUnitFromCombat = removeUnitFromCombat;
            this.resourceRuleSystem = resourceRuleSystem;
            this.combatVariableSystem = combatVariableSystem ?? new CombatVariableSystem();
        }
        public CombatDamageResult ResolveAndApplyDamage(
            CombatSession session,
            BattleUnit source,
            BattleUnit target,
            int attackValue,
            DamageType damageType,
            float skillAccuracy)
        {
            if (source?.Character == null || target?.Character == null || attackValue <= 0)
            {
                return new CombatDamageResult(false, false, 0);
            }

            if (StatusEffectSystem.IsParalyzed(target))
            {
                return new CombatDamageResult(false, false, 0);
            }

            if (!IsGuaranteedHitDamageType(damageType) && !RollHit(source, target, skillAccuracy))
            {
                battleLog?.Invoke($"{GetDisplayName(source)} chybia {GetDisplayName(target)}.");

                return new CombatDamageResult(false, false, 0);
            }

            var damageAfterFreeze = ApplyFreezeDamageBonus(target, attackValue);
            var isCritical = RollCritical(source, damageType);
            var damageAfterCrit = ApplyCriticalDamage(
                source,
                damageAfterFreeze,
                isCritical);

            var finalDamage = ApplyDefense(target, damageAfterCrit, damageType);

            if (isCritical)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} trafia krytycznie.");
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
            bool wasHit)
        {
            if (source?.Character == null
                || target?.Character == null
                || attackValue <= 0
                || !wasHit
                || StatusEffectSystem.IsParalyzed(target))
            {
                return new CombatDamageResult(false, false, 0);
            }

            var damageAfterFreeze = ApplyFreezeDamageBonus(target, attackValue);
            var isCritical = RollCritical(source, damageType);
            var damageAfterCrit = ApplyCriticalDamage(
                source,
                damageAfterFreeze,
                isCritical);

            var finalDamage = ApplyDefense(target, damageAfterCrit, damageType);

            if (isCritical)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} trafia krytycznie.");
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
            int requestedDamage, DamageType damageType)
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

            var damageBeforeShield = remainingDamage;

            remainingDamage = AbsorbShield(target, remainingDamage);

            var shieldDamage = damageBeforeShield - remainingDamage;
            if (shieldDamage > 0) 
                { battleLog?.Invoke($"{target.Character.Definition.DisplayName} traci {shieldDamage} tarczy.");}
            var healthDamage = Mathf.Min(target.Character.CurrentHealth, remainingDamage);
            target.Character.CurrentHealth -= healthDamage;

            if (healthDamage > 0)
            {
                var damageText = GetDamageText(damageType);

                battleLog?.Invoke($"{target.Character.Definition.DisplayName} otrzymuje {healthDamage} {damageText}");
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
            return healthDamage;
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

            var chance = source.Character.Stats == null ? 0f : source.Character.Stats.CritChance.GetValue();

            return UnityEngine.Random.value <= Mathf.Clamp01(chance);
        }

        private static int ApplyCriticalDamage(BattleUnit source, int damage, bool isCritical)
        {
            if (!isCritical) return damage;

            var multiplier = source.Character.Stats == null ? 0f : source.Character.Stats.CritDamage.GetValue();

            return Mathf.CeilToInt(damage * Mathf.Max(1f, multiplier));
        }

        private static int ApplyDefense(BattleUnit target, int damage, DamageType damageType)
        {
            if (damage <= 0) return 0;
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

            defense = Mathf.Max(0f, defense);

            if (defense <= 0f) return damage;

            return Mathf.CeilToInt(damage * damage / (damage + defense));
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
                session?.TryMarkEnemyDefeated(target.Character.CharacterId);
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
