using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class CombatDamageValueCalculator
    {
        public static int CalculateDamageValue(
            BattleUnit source,
            DamageValueSource valueSource,
            float multiplier)
        {
            if (source?.Character?.Stats == null)
            {
                return 0;
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
