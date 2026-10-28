using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class CombatDamageValueCalculator
    {
        // Class-independent alchemical power, anchored to the former normal-equipment
        // reference through L30. Never reads the equipped weapon.
        private static readonly int[] ReferenceLevels = { 1, 5, 15, 25, 30 };
        private static readonly float[] ReferenceAttacks = { 28f, 51.125f, 120.34375f, 248.625f, 344.5625f };

        public static float GetAlchemicalReferenceAttack(int level)
        {
            level = Mathf.Clamp(level, 1, 50);
            if (level > 30)
            {
                // Continue the L25–30 slope without a jump, then accelerate toward
                // 1200 at L50. The old L45–50 segment almost stopped growing.
                const float start = 344.5625f, slope = 19.1875f;
                var delta = level - 30;
                return start + slope * delta + (1200f - start - slope * 20f) * delta * delta / 400f;
            }
            for (var i = 1; i < ReferenceLevels.Length; i++)
            {
                if (level <= ReferenceLevels[i])
                {
                    var t = (float)(level - ReferenceLevels[i - 1]) / (ReferenceLevels[i] - ReferenceLevels[i - 1]);
                    return Mathf.Lerp(ReferenceAttacks[i - 1], ReferenceAttacks[i], t);
                }
            }
            return ReferenceAttacks[ReferenceAttacks.Length - 1];
        }

        public static int CalculateAlchemicalDamage(int level, float strength)
        {
            // Calibrate against defense equal to reference attack: 0.15 => 40%,
            // two ingredients (0.225 after duplicate penalty) => 60% of a normal hit.
            // Invert x*x/(x+1) = desired damage / reference attack.
            var q = Mathf.Max(0f, strength) * (0.2f / 0.15f);
            var rawRatio = (q + Mathf.Sqrt(q * q + 4f * q)) * 0.5f;
            return Mathf.CeilToInt(GetAlchemicalReferenceAttack(level) * rawRatio);
        }

        // DoT already bypasses armor. Use the shared level power linearly;
        // the inverse-armor calibration above is only for instant damage.
        public static int CalculateAlchemicalPeriodicDamage(int level, float multiplier) =>
            Mathf.CeilToInt(GetAlchemicalReferenceAttack(level) * Mathf.Max(0f, multiplier) - .00001f);

        public static bool UsesAlchemicalPower(SkillData skill)
        {
            if (skill?.Effects == null) return false;
            foreach (var effect in skill.Effects)
                if (effect != null && effect.DamageValueSource == DamageValueSource.AlchemicalPower)
                    return true;
            return false;
        }

        public static int ScaleAttackDamage(int attack,float multiplier)
            => Mathf.CeilToInt(attack*Mathf.Max(0f,multiplier)-.00001f);

        public static int CalculateDamageValue(
            BattleUnit source,
            DamageValueSource valueSource,
            float multiplier, WarriorDischargeCast dischargeCast = null)
        {
            if (source?.Character?.Stats == null)
            {
                return 0;
            }
            if (valueSource == DamageValueSource.AlchemicalPower)
            {
                return CalculateAlchemicalDamage(source.Character.Level, multiplier);
            }
            var minAttack = Mathf.Max(0, Mathf.RoundToInt(dischargeCast?.MinAttack ?? source.Character.Stats.MinAttack.GetValue()));
            var maxAttack = Mathf.Max(minAttack, Mathf.RoundToInt(dischargeCast?.MaxAttack ?? source.Character.Stats.MaxAttack.GetValue()));
            var baseDamage = valueSource switch
            {
                DamageValueSource.MinAttack => minAttack,
                DamageValueSource.MaxAttack => maxAttack,
                DamageValueSource.AverageAttack => Mathf.RoundToInt((minAttack + maxAttack) * 0.5f),
                _ => Random.Range(minAttack,maxAttack + 1)
            };

            // Avoid promoting an exact integer (e.g. 100 * 0.6f) by floating-point noise.
            return ScaleAttackDamage(baseDamage,multiplier);
        }
    }
}
