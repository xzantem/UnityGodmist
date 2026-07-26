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

        public CombatDamageSystem(Action<string> battleLog = null,
            Action<CombatSession> evaluateOutcome = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            ResourceRuleSystem resourceRuleSystem = null)
        {
            this.battleLog = battleLog;
            this.evaluateOutcome = evaluateOutcome;
            this.removeUnitFromCombat = removeUnitFromCombat;
            this.resourceRuleSystem = resourceRuleSystem;
        }
        public CombatDamageResult ResolveAndApplyDamage(CombatSession session, BattleUnit source, 
            BattleUnit target, int attackValue, DamageType damageType, float skillAccuracy)
        {
            if (source?.Character == null || target?.Character == null || attackValue <= 0)
            {
                return new CombatDamageResult(false, false, 0);
            }

            if (!RollHit(source, target, damageType, skillAccuracy))
            {
                battleLog?.Invoke($"{GetDisplayName(source)} chybia {GetDisplayName(target)}.");

                return new CombatDamageResult(false, false, 0);
            }

            var isCritical = RollCritical(source, damageType);

            var damageAfterCrit = ApplyCriticalDamage(source, attackValue, isCritical);

            var finalDamage = ApplyDefense( target, damageAfterCrit, damageType);

            if (isCritical)
            {
                battleLog?.Invoke($"{GetDisplayName(source)} trafia krytycznie.");
            }

            var healthDamage = ApplyDamage(session, source, target, finalDamage, damageType);

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
            var incomingDamage = Mathf.Max(0, requestedDamage);
            if (incomingDamage <= 0)
            {
                return 0;
            }
            if (target.Character.LifeState == CharacterLifeState.Downed)
            {
                AccelerateDownedDeath(target);
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
                var damageText = damageType == DamageType.Bleed ? "obrażeń od krwawienia" : "obrażeń";

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
                    MarkDead(target);
                }
            }
            if (source != null && damageType != DamageType.Bleed && (shieldDamage > 0 || healthDamage > 0))
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

        private static bool RollHit(BattleUnit source, BattleUnit target, DamageType damageType, float skillAccuracy)
        {
            if (damageType != DamageType.Physical)
                return true;

            var dodge = target.Character.Stats == null ? 0f : target.Character.Stats.Dodge.GetValue();

            var chance = CalculateHitChance(source.Character.WeaponAccuracy, skillAccuracy, dodge);

            return UnityEngine.Random.value <= chance;
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
        private void AccelerateDownedDeath(BattleUnit target)
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
                MarkDead(target);
            }
        }

        private void MarkDead(BattleUnit target)
        {
            target.Character.CurrentHealth = 0;
            target.Character.LifeState = CharacterLifeState.Dead;
            target.DownedTurnsRemaining = 0;

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} ginie.");

            if (!target.IsPlayerSide)
            {
                removeUnitFromCombat?.Invoke(target);
            }
        }
    }
}
