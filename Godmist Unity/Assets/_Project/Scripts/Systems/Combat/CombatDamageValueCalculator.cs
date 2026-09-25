using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class CombatDamageValueCalculator
    {
        // Normal equipment reference, shared by all classes; never reads the equipped weapon.
        private static readonly int[] ReferenceLevels = { 1, 5, 15, 25, 35, 45, 50 };
        private static readonly float[] ReferenceAttacks = { 28f, 51.125f, 120.34375f, 248.625f, 440.5f, 709.5625f, 750.34375f };

        public static float GetAlchemicalReferenceAttack(int level)
        {
            level = Mathf.Clamp(level, 1, 50);
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

        public static int CalculateDamageValue(
            BattleUnit source,
            DamageValueSource valueSource,
            float multiplier)
        {
            if (source?.Character?.Stats == null)
            {
                return 0;
            }
            if (valueSource == DamageValueSource.AlchemicalPower)
            {
                return CalculateAlchemicalDamage(source.Character.Level, multiplier);
            }
            var minAttack = Mathf.Max(0, Mathf.RoundToInt(source.Character.Stats.MinAttack.GetValue()));
            var maxAttack = Mathf.Max(minAttack, Mathf.RoundToInt(source.Character.Stats.MaxAttack.GetValue()));
            var baseDamage = valueSource switch
            {
                DamageValueSource.MinAttack => minAttack,
                DamageValueSource.MaxAttack => maxAttack,
                DamageValueSource.AverageAttack => Mathf.RoundToInt((minAttack + maxAttack) * 0.5f),
                _ => Random.Range(minAttack,maxAttack + 1)
            };

            // Avoid promoting an exact integer (e.g. 100 * 0.6f) by floating-point noise.
            return Mathf.CeilToInt(baseDamage * Mathf.Max(0f, multiplier) - .00001f);
        }
    }
}
