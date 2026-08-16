using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;

namespace Godmist.Views.UI
{
    public enum CombatStatusKind { Status, Buff, Debuff }

    public readonly struct CombatStatusPresentation
    {
        public CombatStatusPresentation(PassiveEffectRuntime effect, CombatStatusKind kind, int priority)
        { Effect = effect; Kind = kind; Priority = priority; }
        public PassiveEffectRuntime Effect { get; }
        public CombatStatusKind Kind { get; }
        public int Priority { get; }
    }

    public static class CombatStatusPresentationFactory
    {
        public static List<CombatStatusPresentation> Create(IEnumerable<PassiveEffectRuntime> effects)
        {
            var result = new List<CombatStatusPresentation>();
            if (effects == null) return result;
            foreach (var effect in effects)
            {
                if (!PassiveEffectLifecycleSystem.IsActive(effect)
                    || string.IsNullOrWhiteSpace(effect.EffectId))
                {
                    continue;
                }

                var known = IsKnownStatus(effect.EffectId);
                var kind = known ? CombatStatusKind.Status : IsDebuff(effect) ? CombatStatusKind.Debuff : CombatStatusKind.Buff;
                result.Add(new CombatStatusPresentation(effect, kind, GetPriority(effect.EffectId, kind)));
            }
            result.Sort((a,b) => a.Priority.CompareTo(b.Priority));
            return result;
        }

        private static bool IsDebuff(PassiveEffectRuntime effect)
        {
            foreach (var modifier in effect.StatModifiers)
                if (modifier.Value < 0f) return true;
            foreach (var modifier in effect.VariableModifiers)
                if (modifier.Value < 0f) return true;
            return false;
        }

        private static bool IsKnownStatus(string effectId)
        {
            foreach (var knownId in StatusEffectIds.KnownIds)
            {
                if (string.Equals(knownId, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetPriority(string id, CombatStatusKind kind) => id switch
        {
            StatusEffectIds.Stun or StatusEffectIds.Freeze or StatusEffectIds.Sleep or StatusEffectIds.Paralysis => 0,
            StatusEffectIds.Bleed or StatusEffectIds.Poison or StatusEffectIds.Burn => 1,
            _ => kind == CombatStatusKind.Debuff ? 2 : 3
        };
    }
}
