using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;

namespace Godmist.Views.UI
{
    public enum CombatStatusKind { Status, Buff, Debuff }

    public readonly struct CombatStatusPresentation
    {
        public CombatStatusPresentation(
            PassiveEffectRuntime effect,
            CombatStatusKind kind,
            int priority,
            CombatAuraRuntime aura = null,
            CombatDamageRedirectRuntime outgoingRedirect = null,
            BattleUnit owner = null)
        {
            Effect = effect;
            Kind = kind;
            Priority = priority;
            Aura = aura;
            OutgoingRedirect = outgoingRedirect;
            Owner = owner;
        }
        public PassiveEffectRuntime Effect { get; }
        public CombatStatusKind Kind { get; }
        public int Priority { get; }
        // Auras are combat-scoped on BattleUnit rather than passive effects on the
        // character. Keep their runtime details with the presentation so the owner
        // can inspect the active aura even when it does not affect them personally.
        public CombatAuraRuntime Aura { get; }
        public CombatDamageRedirectRuntime OutgoingRedirect { get; }
        public BattleUnit Owner { get; }
    }

    public static class CombatStatusPresentationFactory
    {
        public static List<CombatStatusPresentation> Create(
            IEnumerable<PassiveEffectRuntime> effects,
            IEnumerable<CombatAuraRuntime> activeAuras = null,
            IEnumerable<CombatDamageRedirectRuntime> outgoingRedirects = null,
            BattleUnit owner = null)
        {
            var result = new List<CombatStatusPresentation>();
            var effectList = effects == null
                ? new List<PassiveEffectRuntime>()
                : new List<PassiveEffectRuntime>(effects);
            var auraList = activeAuras == null
                ? new List<CombatAuraRuntime>()
                : new List<CombatAuraRuntime>(activeAuras);
            var redirectList = outgoingRedirects == null
                ? new List<CombatDamageRedirectRuntime>()
                : new List<CombatDamageRedirectRuntime>(outgoingRedirects);
            foreach (var effect in effectList)
            {
                if (!PassiveEffectLifecycleSystem.IsActive(effect)
                    || string.IsNullOrWhiteSpace(effect.EffectId))
                {
                    continue;
                }

                var known = IsKnownStatus(effect.EffectId);
                var kind = known ? CombatStatusKind.Status : IsDebuff(effect) ? CombatStatusKind.Debuff : CombatStatusKind.Buff;
                var appliedAura = FindAppliedAura(effect, auraList);
                result.Add(new CombatStatusPresentation(
                    effect,
                    kind,
                    GetPriority(effect.EffectId, kind),
                    appliedAura,
                    owner: owner));
            }

            foreach (var aura in auraList)
            {
                if (aura == null || string.IsNullOrWhiteSpace(aura.AuraId)
                    || string.IsNullOrWhiteSpace(aura.StatusId))
                {
                    continue;
                }

                // When the aura includes its owner, Refresh has already applied the
                // status to that unit. Do not render a second, identical icon for the
                // presentation-only aura entry; the real status carries its DoT and
                // duration data.
                var ownerAlreadyHasAuraStatus = false;
                foreach (var candidate in effectList)
                {
                    if (!PassiveEffectLifecycleSystem.IsActive(candidate)
                        || !string.Equals(candidate.EffectId, aura.StatusId,
                            StringComparison.OrdinalIgnoreCase)
                        || candidate.Sources == null)
                    {
                        continue;
                    }

                    ownerAlreadyHasAuraStatus = candidate.Sources.Exists(source =>
                        string.Equals(source?.SourceId, aura.AuraId,
                            StringComparison.OrdinalIgnoreCase));
                    if (ownerAlreadyHasAuraStatus) break;
                }

                if (ownerAlreadyHasAuraStatus) continue;

                // This is presentation-only. Aura application itself remains owned
                // by CombatAuraSystem and no artificial effect is added to saves.
                var effect = new PassiveEffectRuntime
                {
                    EffectId = aura.StatusId,
                    DisplayName = aura.DisplayName,
                    Lifetime = PassiveEffectLifetime.CombatScoped,
                    Stacks = 1
                };
                if (aura.StatusDamagePerTurn > 0)
                {
                    effect.SetPayload(new DamageOverTimePayload
                    {
                        RemainingTotalDamage = aura.StatusDamagePerTurn,
                        DamagePerTurn = aura.StatusDamagePerTurn
                    });
                }
                result.Add(new CombatStatusPresentation(
                    effect,
                    CombatStatusKind.Buff,
                    GetPriority(effect.EffectId, CombatStatusKind.Buff),
                    aura,
                    owner: owner));
            }

            foreach (var redirect in redirectList)
            {
                if (redirect == null || string.IsNullOrWhiteSpace(redirect.SourceEffectId)
                    || redirect.RedirectPercent <= 0f)
                {
                    continue;
                }

                var targetEffect = redirect.Target?.Character?.PassiveEffects?.Find(effect =>
                    PassiveEffectLifecycleSystem.IsActive(effect)
                    && string.Equals(effect.EffectId, redirect.SourceEffectId,
                        StringComparison.OrdinalIgnoreCase)
                    && effect.GetPayload<DamageRedirectPayload>()?.RedirectPercent > 0f);
                if (redirect.Target != null && targetEffect == null) continue;

                var effect = new PassiveEffectRuntime
                {
                    EffectId = "buff",
                    DisplayName = "Przekierowanie obrażeń",
                    Lifetime = PassiveEffectLifetime.CombatScoped,
                    Stacks = 1
                };
                effect.SetPayload(new DamageRedirectPayload
                {
                    RedirectPercent = redirect.RedirectPercent,
                    ProtectedCharacterId = redirect.TargetCharacterId,
                    ProtectedCharacterDisplayName = redirect.TargetDisplayName
                });
                result.Add(new CombatStatusPresentation(
                    effect,
                    CombatStatusKind.Buff,
                    3,
                    outgoingRedirect: redirect,
                    owner: owner));
            }
            result.Sort((a,b) => a.Priority.CompareTo(b.Priority));
            return result;
        }

        private static CombatAuraRuntime FindAppliedAura(
            PassiveEffectRuntime effect,
            IEnumerable<CombatAuraRuntime> activeAuras)
        {
            if (effect?.Sources == null || activeAuras == null) return null;

            foreach (var aura in activeAuras)
            {
                if (aura == null
                    || !string.Equals(effect.EffectId, aura.StatusId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (effect.Sources.Exists(source =>
                        string.Equals(source?.SourceId, aura.AuraId,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return aura;
                }
            }

            return null;
        }

        private static bool IsDebuff(PassiveEffectRuntime effect)
        {
            var resistance = effect?.GetPayload<ResistanceModifierPayload>();
            if (resistance != null && resistance.Value < 0f)
            {
                return true;
            }

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
