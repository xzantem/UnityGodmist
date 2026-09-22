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
            BattleUnit owner = null,
            Godmist.Runtime.Characters.CharacterInstance explorationCharacter = null)
        {
            Effect = effect;
            Kind = kind;
            Priority = priority;
            Aura = aura;
            OutgoingRedirect = outgoingRedirect;
            Owner = owner;
            ExplorationCharacter = explorationCharacter;
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
        public Godmist.Runtime.Characters.CharacterInstance ExplorationCharacter { get; }
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

                if (!IsCombatRelevant(effect)) continue;

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

            if(owner!=null)
            {
                var warrior=owner.WarriorState;
                foreach(var entry in new[]{("warrior.order","Rozkaz Unicestwienia",warrior.OrderTurns,CombatStatusKind.Debuff),("warrior.protection","Rozkaz Ochrony",warrior.ProtectionTurns,CombatStatusKind.Buff),("warrior.trauma","Trauma",warrior.TraumaTurns,CombatStatusKind.Debuff),("warrior.strategy","Strategia — darmowe kroki",warrior.FreeMovementTurns,CombatStatusKind.Buff)})
                    if(entry.Item3>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime{EffectId=entry.Item1,DisplayName=entry.Item2,Stacks=1,RemainingDuration=entry.Item3,Lifetime=PassiveEffectLifetime.Timed},entry.Item4,30,owner:owner));
                var anger=0;foreach(var entry in owner.ScoutState.AngerBySource)anger=Math.Max(anger,entry.Value);
                if(anger>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.anger",DisplayName="Złość ("+anger+")",Stacks=anger,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Debuff,30,owner:owner));
                if(owner.ScoutState.WeakPointTurns>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.weak_point",DisplayName="Słaby Punkt",Stacks=1,RemainingDuration=owner.ScoutState.WeakPointTurns,Lifetime=PassiveEffectLifetime.Timed},CombatStatusKind.Debuff,30,owner:owner));
                if(owner.ScoutState.Knives.Count>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.embedded_knives",DisplayName="Wbite sztylety ("+owner.ScoutState.Knives.Count+")",Stacks=owner.ScoutState.Knives.Count,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Debuff,30,owner:owner));
                if(owner.ScoutState.AnalysisTurns>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.analysis",DisplayName="Dogłębna Analiza",Stacks=1,RemainingDuration=owner.ScoutState.AnalysisTurns,Lifetime=PassiveEffectLifetime.Timed},CombatStatusKind.Buff,30,owner:owner));
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

        private static bool IsCombatRelevant(PassiveEffectRuntime effect)
        {
            if (effect == null) return false;

            // This panel represents the state of the current fight, not the
            // character's complete passive loadout. Persistent equipment and
            // skill-tree effects are intentionally omitted; effects carried into
            // combat or applied during it use Timed/CombatScoped lifetimes.
            return effect.Lifetime != PassiveEffectLifetime.Persistent;
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
