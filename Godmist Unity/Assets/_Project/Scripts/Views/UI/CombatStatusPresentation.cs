using Godmist.Core.Utilities;
using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Data.Skills;
using UnityEngine;

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
            Godmist.Runtime.Characters.CharacterInstance explorationCharacter = null,
            IReadOnlyList<PassiveEffectRuntime> groupedEffects = null)
        {
            Effect = effect;
            Kind = kind;
            Priority = priority;
            Aura = aura;
            OutgoingRedirect = outgoingRedirect;
            Owner = owner;
            ExplorationCharacter = explorationCharacter;
            Effects = groupedEffects ?? new[] { effect };
        }
        public PassiveEffectRuntime Effect { get; }
        public string DisplayName => HeroCombatStatusText.Name(Effect?.EffectId, Effect?.DisplayName);
        public IReadOnlyList<PassiveEffectRuntime> Effects { get; }
        public bool IsDamageOverTimeGroup
        {
            get
            {
                if (!IsDamageOverTime(Effect?.EffectId)) return false;
                foreach (var effect in Effects ?? Array.Empty<PassiveEffectRuntime>())
                    if (effect == null || !string.Equals(effect.EffectId, Effect.EffectId, StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }
        }
        public int NextTickDamage
        {
            get
            {
                var total = 0;
                foreach (var effect in Effects ?? Array.Empty<PassiveEffectRuntime>())
                {
                    var payload = effect?.GetPayload<DamageOverTimePayload>();
                    if (payload == null || !IsDamageOverTime(effect.EffectId)) continue;
                    var raw = Mathf.CeilToInt(payload.RemainingTotalDamage / (float)Mathf.Max(1, effect.RemainingDuration));
                    total += DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(Owner, effect.EffectId, raw);
                }
                return total;
            }
        }
        internal static bool IsDamageOverTime(string id) => id != null
            && (string.Equals(id, StatusEffectIds.Bleed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, StatusEffectIds.Poison, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, StatusEffectIds.Burn, StringComparison.OrdinalIgnoreCase));
        public int StackCount
        {
            get
            {
                if (HeroCombatStatusText.Key(Effect?.EffectId) == "mage.erosion" && Owner != null
                    && Owner.MageState.FireErosion.TryGetValue(Effect.EffectId.Substring("mage.erosion.".Length), out var hits))
                    return hits;
                if (IsDamageOverTimeGroup)
                {
                    var total = 0;
                    foreach (var effect in Effects ?? Array.Empty<PassiveEffectRuntime>())
                        if (effect != null) total += Math.Max(1, effect.Stacks);
                    return total;
                }
                var stacks = Effect?.Stacks ?? 0;
                foreach (var effect in Effects ?? Array.Empty<PassiveEffectRuntime>())
                    if (effect.Stacks != stacks) return 0;
                return stacks;
            }
        }
        public int Duration
        {
            get
            {
                var duration = HeroCombatStatusText.IsFieldSlow(Effect?.EffectId) ? 0 : Effect?.RemainingDuration ?? 0;
                foreach (var effect in Effects ?? Array.Empty<PassiveEffectRuntime>())
                    if (effect.RemainingDuration != duration || effect.Lifetime != Effect.Lifetime) return 0;
                return duration;
            }
        }
        public bool HasMixedDurations
        {
            get
            {
                foreach (var effect in Effects ?? Array.Empty<PassiveEffectRuntime>())
                    if (effect.RemainingDuration != Effect.RemainingDuration || effect.Lifetime != Effect.Lifetime) return true;
                return false;
            }
        }
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
        public static Sprite ResolveIcon(
            CombatStatusPresentation presentation,
            Func<string, Sprite> effectIcon,
            IEnumerable<SkillData> skills,
            IEnumerable<SkillTreeDefinition> trees = null)
        {
            var fallback = effectIcon?.Invoke("buff");
            var effectId = presentation.Effect?.EffectId;
            var dedicated = effectIcon?.Invoke(effectId);
            if (effectId?.StartsWith("status.forest.demonic_plague.", StringComparison.Ordinal) == true)
                dedicated = effectIcon?.Invoke("status.forest.demonic_plague") ?? dedicated;
            // Some glossary entries still use the same question-mark placeholder.
            if (dedicated != null && dedicated != fallback) return dedicated;

            var iconSource = HeroCombatStatusText.IconSource(effectId);
            if (iconSource?.StartsWith("node.", StringComparison.Ordinal) == true)
                foreach (var tree in trees ?? Array.Empty<SkillTreeDefinition>())
                    foreach (var node in tree?.Nodes ?? new List<SkillTreeNodeDefinition>())
                        if (node?.Id == iconSource && (node.Icon != null || node.GrantedSkill?.Icon != null))
                            return node.Icon != null ? node.Icon : node.GrantedSkill.Icon;

            Sprite skillIcon = null;
            var matchedIdLength = 0;
            foreach (var skill in skills ?? Array.Empty<SkillData>())
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId)
                    || skill.SkillId.Length < matchedIdLength) continue;
                var matches = skill.SkillId == iconSource
                    || MatchesSkillSource(presentation.Aura?.AuraId, skill.SkillId)
                    || MatchesSkillSource(presentation.OutgoingRedirect?.SourceEffectId, skill.SkillId);
                foreach (var effect in presentation.Effects ?? Array.Empty<PassiveEffectRuntime>())
                {
                    if (effect?.Sources == null) continue;
                    foreach (var source in effect.Sources)
                        matches |= MatchesSkillSource(source?.SourceId, skill.SkillId);
                }
                if (!matches) continue;
                // Prefer the most specific ID, so e.g. skill.test.extra is not skill.test.
                if (skill.SkillId.Length > matchedIdLength || skillIcon == null)
                {
                    matchedIdLength = skill.SkillId.Length;
                    skillIcon = skill.Icon;
                }
            }
            return skillIcon != null ? skillIcon : dedicated != null ? dedicated : fallback;
        }

        private static bool MatchesSkillSource(string sourceId, string skillId)
        {
            return !string.IsNullOrWhiteSpace(sourceId)
                && (string.Equals(sourceId, skillId, StringComparison.Ordinal)
                    || sourceId.StartsWith(skillId + ".", StringComparison.Ordinal));
        }

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
                if(owner.PoacherArmorCooldown>0)
                    result.Add(new CombatStatusPresentation(new PassiveEffectRuntime { EffectId="equipment.poacher-armor.cooldown",
                        DisplayName="Gruboskórność — odnowienie",Stacks=1,RemainingDuration=owner.PoacherArmorCooldown,
                        Lifetime=PassiveEffectLifetime.Timed},CombatStatusKind.Buff,30,owner:owner));
                if(owner.BanditLeaderState is { } bandit && owner.Character?.LifeState==CharacterLifeState.Alive)
                    result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {
                        EffectId=bandit.PhaseTwo?"forest.bandit_leader.commander":"forest.bandit_leader.hidden_hand",
                        DisplayName=bandit.PhaseTwo?"Nieugięty Dowódca":"Niewidzialna Ręka",
                        Stacks=1,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Buff,30,owner:owner));
                if(owner.SkeletonSlayerState is { } slayer && owner.Character?.LifeState==CharacterLifeState.Alive)
                {
                    result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {
                        EffectId=slayer.PhaseTwo ? Godmist.Systems.Combat.SkeletonSlayerBossSystem.RevengeStatusId : Godmist.Systems.Combat.SkeletonSlayerBossSystem.ArmorStatusId,
                        DisplayName=slayer.PhaseTwo ? "Zemsta Zabójcy ("+(100+3*slayer.RevengeHits)+"% " + UiTerms.ActionPointsShort + ")" : "Kościana Zbroja",
                        Stacks=1,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Buff,30,owner:owner));
                    foreach(var entry in new[]{("defense","Nieugięta Obrona",slayer.DefenseTurns),("hammer","Młot Zniszczenia — przygotowanie",slayer.HammerTurns)})
                        if(entry.Item3>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {
                            EffectId="catacombs.skeleton_slayer."+entry.Item1,DisplayName=entry.Item2,Stacks=1,RemainingDuration=entry.Item3,Lifetime=PassiveEffectLifetime.Timed},CombatStatusKind.Buff,30,owner:owner));
                    if(slayer.NextAttackBonus>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {
                        EffectId="catacombs.skeleton_slayer.retaliation",DisplayName="Odwet (+"+slayer.NextAttackBonus+")",Stacks=1,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Buff,30,owner:owner));
                }
                if (owner.DeadKingState != null && owner.Character?.LifeState == CharacterLifeState.Alive)
                {
                    var phaseTwo = owner.DeadKingState.PhaseTwo;
                    result.Add(new CombatStatusPresentation(new PassiveEffectRuntime
                    {
                        EffectId = phaseTwo ? "catacombs.dead_king.rage" : "catacombs.dead_king.despair",
                        DisplayName = phaseTwo ? "Widmowy Szał" : "Aura Rozpaczy",
                        Lifetime = PassiveEffectLifetime.CombatScoped, Stacks = 1
                    }, CombatStatusKind.Buff, 30, owner: owner));
                }
                if (owner.Character?.Definition?.Id == Godmist.Systems.Combat.DeadKingBossSystem.KnightId
                    && owner.Character.LifeState == CharacterLifeState.Alive)
                    result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {
                        EffectId = "catacombs.spectral_knight.shell", DisplayName = "Widmowa powłoka",
                        Stacks = Math.Max(1, 3 - owner.DeadKingKnightHits),
                        Lifetime = PassiveEffectLifetime.CombatScoped
                    }, CombatStatusKind.Buff, 3, owner: owner));
                var warrior=owner.WarriorState;
                foreach(var entry in new[]{("warrior.order","Rozkaz Unicestwienia",warrior.OrderTurns,CombatStatusKind.Debuff),("warrior.protection","Rozkaz Ochrony",warrior.ProtectionTurns,CombatStatusKind.Buff),("warrior.trauma","Trauma",warrior.TraumaTurns,CombatStatusKind.Debuff),("warrior.strategy","Strategia — darmowe kroki",warrior.FreeMovementTurns,CombatStatusKind.Buff)})
                    if(entry.Item3>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime{EffectId=entry.Item1,DisplayName=entry.Item2,Stacks=1,RemainingDuration=entry.Item3,Lifetime=PassiveEffectLifetime.Timed},entry.Item4,30,owner:owner));
                var anger=0;foreach(var entry in owner.ScoutState.AngerBySource)anger=Math.Max(anger,entry.Value);
                if(anger>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.anger",DisplayName="Złość ("+anger+")",Stacks=anger,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Debuff,30,owner:owner));
                if(owner.ScoutState.WeakPointTurns>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.weak_point",DisplayName="Słaby Punkt",Stacks=1,RemainingDuration=owner.ScoutState.WeakPointTurns,Lifetime=PassiveEffectLifetime.Timed},CombatStatusKind.Debuff,30,owner:owner));
                if(owner.ScoutState.Knives.Count>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.embedded_knives",DisplayName="Wbite sztylety ("+owner.ScoutState.Knives.Count+")",Stacks=owner.ScoutState.Knives.Count,Lifetime=PassiveEffectLifetime.CombatScoped},CombatStatusKind.Debuff,30,owner:owner));
                if(owner.ScoutState.AnalysisTurns>0)result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {EffectId="scout.analysis",DisplayName="Dogłębna Analiza",Stacks=1,RemainingDuration=owner.ScoutState.AnalysisTurns,Lifetime=PassiveEffectLifetime.Timed},CombatStatusKind.Buff,30,owner:owner));
                foreach (var adaptation in owner.MageState.Adaptation)
                    if (adaptation.Value.RemainingHits > 0)
                        result.Add(new CombatStatusPresentation(new PassiveEffectRuntime {
                            EffectId = "mage.adaptation." + adaptation.Key,
                            DisplayName = "Adaptacja Energetyczna", Stacks = adaptation.Value.RemainingHits,
                            Lifetime = PassiveEffectLifetime.CombatScoped
                        }, CombatStatusKind.Buff, 30, owner: owner));
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
            var grouped = GroupBySource(result);
            grouped.Sort((a,b) => a.Priority.CompareTo(b.Priority));
            return grouped;
        }

        private static List<CombatStatusPresentation> GroupBySource(List<CombatStatusPresentation> entries)
        {
            var result = new List<CombatStatusPresentation>();
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var key = entry.Aura == null && entry.OutgoingRedirect == null
                    ? entry.IsDamageOverTimeGroup ? "dot:" + entry.Effect.EffectId.ToLowerInvariant() : SourceKey(entry.Effect)
                    : null;
                if (key == null || !indices.TryGetValue(key, out var index))
                {
                    if (key != null) indices.Add(key, result.Count);
                    result.Add(entry);
                    continue;
                }
                var previous = result[index];
                var effects = new List<PassiveEffectRuntime>(previous.Effects) { entry.Effect };
                // Preserve each real runtime effect; grouping never changes ticking, stacks or payloads.
                var primary = entry.Priority < previous.Priority ? entry : previous;
                result[index] = new CombatStatusPresentation(primary.Effect, primary.Kind,
                    Math.Min(previous.Priority, entry.Priority), owner: primary.Owner, groupedEffects: effects);
            }
            return result;
        }

        private static string SourceKey(PassiveEffectRuntime effect)
        {
            if (effect.Sources?.Count == 1)
            {
                var source = effect.Sources[0];
                if (source == null || string.IsNullOrWhiteSpace(source.SourceId)) return null;
                if (effect.EffectId is "paladin.zeal.base" or "paladin.zeal.extra")
                    return "hero:paladin.zeal:" + source.SourceCharacterId;
                if (source.SourceType == PassiveEffectSourceType.Skill)
                {
                    var marker = source.SourceId.LastIndexOf(".effect.", StringComparison.Ordinal);
                    if (marker > 0 && source.SourceId.Length > marker + 8)
                        return "skill:" + source.SourceId.Substring(0, marker) + ":" + source.SourceCharacterId;
                }
                if (source.SourceType == PassiveEffectSourceType.Consumable)
                    return "consumable:" + source.SourceId + ":" + source.SourceCharacterId;
                return null;
            }
            // Older exploration potions had no source metadata. Their IDs end in effect type/index.
            if ((effect.Sources == null || effect.Sources.Count == 0)
                && effect.EffectId.StartsWith("consumable.", StringComparison.Ordinal))
            {
                var last = effect.EffectId.LastIndexOf('.');
                var type = last > 0 ? effect.EffectId.LastIndexOf('.', last - 1) : -1;
                if (type > "consumable.".Length && int.TryParse(effect.EffectId.Substring(last + 1), out _))
                    return "consumable:" + effect.EffectId.Substring(0, type);
            }
            return null;
        }

        private static bool IsCombatRelevant(PassiveEffectRuntime effect)
        {
            if (effect == null || effect.EffectId == "summon.health") return false;

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
            if (effect?.EffectId == "paladin.exposure") return true;
            // A voluntary mixed-benefit toggle remains a buff, like Ofiara.
            if (effect?.Sources != null)
                foreach (var source in effect.Sources)
                    if (source?.SourceId?.StartsWith(WarriorCombatSystem.DeathWish + ".", StringComparison.Ordinal) == true)
                        return false;
            if (effect?.EffectId?.StartsWith("status.forest.demonic_plague.", StringComparison.Ordinal) == true) return true;
            var resistance = effect?.GetPayload<ResistanceModifierPayload>();
            if (resistance != null && resistance.Value < 0f)
            {
                return true;
            }

            foreach (var modifier in effect.StatModifiers)
                if (modifier.Value < 0f) return true;
            foreach (var modifier in effect.VariableModifiers)
                if (modifier.VariableType == GameplayVariableType.DamageReceivedPercent ? modifier.Value > 0f : modifier.Value < 0f) return true;
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
