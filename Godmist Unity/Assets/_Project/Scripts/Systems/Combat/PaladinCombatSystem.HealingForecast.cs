using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class PaladinCombatSystem
    {
        public const string HolyRegenerationId = "paladin.holy_regeneration";
        private const int HolyRegenerationTurns = 4;

        private static int RegenerationTotal(BattleUnit source, BattleUnit target, bool critical) =>
            Mathf.RoundToInt(target.Character.Stats.MaxHealth.GetValue() * .4f
                * (critical ? Mathf.Max(1f, source.Character.Stats.CritDamage.GetValue()) : 1f));

        public static PassiveEffectRuntime CreateHolyRegeneration(BattleUnit source, BattleUnit target, bool critical)
        {
            var effect = Timed(source, HolyRegenerationId, "Święta Regeneracja", HolyRegenerationTurns);
            var total = RegenerationTotal(source, target, critical);
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer("remaining", total - CalculateRegenerationUpfront(source, target, critical)));
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer("total", total));
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer("lasting", P(source, "lasting_effect")));
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("critical", critical));
            return effect;
        }

        private static int RegenerationTickAmount(PassiveEffectRuntime effect) => effect.RemainingDuration <= 0 ? 0
            : Mathf.CeilToInt((effect.GetStateValue("remaining")?.IntegerValue ?? 0) / (float)effect.RemainingDuration);

        // Called after the heal, before the lifecycle decrements RemainingDuration.
        private static void CompleteRegenerationTick(PassiveEffectRuntime effect, int amount)
        {
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer("remaining", (effect.GetStateValue("remaining")?.IntegerValue ?? 0) - amount));
            var lasting = effect.GetStateValue("lasting")?.IntegerValue ?? 0;
            if (effect.RemainingDuration != 1 || lasting <= 0 || effect.GetStateValue("tail")?.BooleanValue == true) return;
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("tail", true));
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer("remaining", Mathf.FloorToInt((effect.GetStateValue("total")?.IntegerValue ?? 0) * .25f)));
            effect.RemainingDuration = (lasting == 1 ? 4 : 2) + 1;
        }

        private static int HealingShareAmount(BattleUnit source, int actual) => Mathf.FloorToInt(actual * .01f * Points(source.Character, Priest));

        public static int ForecastRegenerationTicks(PassiveEffectRuntime effect, int turns, int maximumRecovery = int.MaxValue, BattleUnit selfHealer = null)
        {
            if (effect?.EffectId != HolyRegenerationId || effect.RemainingDuration <= 0) return 0;
            // SetStateValue replaces records; the forecast never mutates a live record.
            var copy = new PassiveEffectRuntime { EffectId = effect.EffectId, RemainingDuration = effect.RemainingDuration,
                StateValues = new List<PassiveEffectRuntimeStateValue>(effect.StateValues) };
            var result = 0;
            for (var turn = 0; turn < turns && copy.RemainingDuration > 0; turn++)
            {
                var amount = RegenerationTickAmount(copy);
                var actual = Math.Min(amount, Math.Max(0, maximumRecovery - result));
                result += actual;
                if (selfHealer != null) result += Math.Min(Math.Max(0, maximumRecovery - result), HealingShareAmount(selfHealer, actual));
                CompleteRegenerationTick(copy, amount);
                copy.RemainingDuration--;
            }
            return result;
        }

        // Expected clamped, rounded uniform roll. E[min(round(X), cap)] = sum P(X >= k - 0.5).
        public static float ExpectedHealingRoll(float minimum, float maximum, int cap)
        {
            if (cap <= 0) return 0f;
            if (maximum <= minimum) return Mathf.Clamp(Mathf.RoundToInt(minimum), 0, cap);
            var full = Math.Clamp((int)Math.Floor(minimum + .5), 0, cap);
            var last = Math.Clamp((int)Math.Ceiling(maximum + .5) - 1, full, cap);
            var count = last - full;
            var thresholds = count * ((double)full + 1 + last - 1) * .5;
            return (float)(full + (count * (double)maximum - thresholds) / (maximum - minimum));
        }

        private static float ExpectedSelfHealingRoll(BattleUnit source, float minimum, float maximum, int cap)
        {
            if (cap <= 0) return 0f;
            if (maximum <= minimum)
            {
                var actual = Mathf.Clamp(Mathf.RoundToInt(minimum), 0, cap);
                return Math.Min(cap, actual + HealingShareAmount(source, actual));
            }
            double Cdf(double value) => Math.Clamp((value - minimum) / (maximum - minimum), 0d, 1d);
            var first = Math.Clamp((int)Math.Floor(minimum + .5), 0, cap);
            var last = Math.Clamp((int)Math.Ceiling(maximum + .5) - 1, first, cap);
            var expected = 0d;
            for (var actual = first; actual <= last; actual++)
            {
                var probability = (actual == cap ? 1d : Cdf(actual + .5)) - Cdf(actual - .5);
                expected += probability * Math.Min(cap, actual + HealingShareAmount(source, actual));
            }
            return (float)expected;
        }

        public static (float Immediate, float Future) ForecastHealing(CombatSession session, BattleUnit source,
            BattleUnit target, SkillData skill, SkillEffectData effect, bool singleTarget, int futureTurns)
        {
            if (target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead) return default;
            var missing = Math.Max(0, target.Character.MaximumHealth - target.Character.CurrentHealth);
            var singleCrit = effect.EffectType == EffectType.PaladinRegeneration || singleTarget
                || session.Units.Count(unit => Alive(unit) && unit.IsPlayerSide == source.IsPlayerSide) == 1;
            var chance = HealingCritChance(source, singleCrit);
            var immediate = 0f;
            var future = 0f;
            for (var branch = 0; branch < 2; branch++)
            {
                var critical = branch == 1;
                var weight = critical ? chance : 1f - chance;
                if (weight <= 0f) continue;
                if (effect.EffectType == EffectType.PaladinHeal)
                {
                    var amount = CalculateHealing(source, skill, effect, singleTarget)
                        * (critical ? Mathf.Max(1f, source.Character.Stats.CritDamage.GetValue()) : 1f);
                    immediate += weight * (source == target
                        ? ExpectedSelfHealingRoll(source, amount * (1 - HealingVariation), amount * (1 + HealingVariation), missing)
                        : ExpectedHealingRoll(amount * (1 - HealingVariation), amount * (1 + HealingVariation), missing));
                }
                else if (effect.EffectType == EffectType.PaladinRegeneration)
                {
                    var upfront = Math.Min(missing, CalculateRegenerationUpfront(source, target, critical));
                    if (source == target) upfront = Math.Min(missing, upfront + HealingShareAmount(source, upfront));
                    immediate += weight * upfront;
                    future += weight * ForecastRegenerationTicks(CreateHolyRegeneration(source, target, critical), futureTurns,
                        missing - upfront, source == target ? source : null);
                }
            }
            return (immediate, future);
        }
    }
}
