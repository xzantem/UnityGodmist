using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class MageTiringSpellSystem
    {
        public const string SkillId = "skill.mage.tiring", EffectId = "mage.tiring";
        public const int MaximumStacks = 3, DurationTurns = 3;
        public const float SpeedPerStack = -5f, BaseChance = .7f;
        public const string HitDescription = "Trafienia skilli mają 70% szansy na −5 szybkości na 3 tury, do 3 kumulacji (−15). Jeden rzut na cel z użycia skilla; udane nałożenie odświeża czas całego efektu. DoT i dodatkowe obrażenia pasywek nie dokładają kumulacji.";
        public const string Description = "Blokuje odzysk many. " + HitDescription;

        public static bool TryApply(BattleUnit source, BattleUnit target, PassiveEffectCombatContext context)
        {
            if (source?.Character == null || target?.Character?.LifeState != CharacterLifeState.Alive
                || source.IsPlayerSide == target.IsPlayerSide || !source.ActiveToggleSkillIds.Contains(SkillId)
                || context?.Skill == null || context.IsPassiveDuplicate || context.IsPassiveReaction
                || string.IsNullOrWhiteSpace(context.ActionId)
                || context.DamageType is DamageType.Bleed or DamageType.Poison or DamageType.Burn
                || !PassiveEffectCombatExecutionSystem.IsOffensiveSkill(context.Skill)) return false;
            var attempts = source.MageState.TiringAttempts ??= new Dictionary<string, HashSet<string>>();
            if (!attempts.TryGetValue(context.ActionId, out var targets))
                attempts.Add(context.ActionId, targets = new HashSet<string>());
            // Register the attempt, including a failed roll, before any proc can recurse.
            if (!targets.Add(target.Character.CharacterId)) return false;
            var chance = StatusEffectSystem.CalculateEffectChance(BaseChance,
                StatusEffectSystem.GetResistance(target, Godmist.Core.Constants.StatusEffectIds.Weakening));
            if (chance <= 0 || UnityEngine.Random.value >= chance) return false;
            var current = target.Character.PassiveEffects.Find(e => e.EffectId == EffectId && PassiveEffectLifecycleSystem.IsActive(e));
            var stacks = Math.Min(MaximumStacks, Math.Max(0, current?.Stacks ?? 0) + 1);
            var effect = new PassiveEffectRuntime(EffectId,
                new List<PassiveEffectSourceRuntime> { PassiveEffectSourceFactory.FromRuntimeSystem(source, SkillId) },
                stacks, DurationTurns, 0, PassiveEffectLifetime.Timed, PassiveEffectTickPhase.TurnEnd)
            {
                DisplayName = "Wycieńczające Zaklęcia",
                // Stat payloads are effective totals; the shared pipeline does not
                // multiply them by the separate presentation stack count.
                StatModifiers = new List<StatModifier> { new StatModifier { StatType = CharacterStatType.Speed,
                    Operation = StatModifierOperation.FlatAbsolute, Value = SpeedPerStack * stacks } }
            };
            return new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character, effect,
                PassiveEffectStackingMode.ReplaceDurationAndValues, out _);
        }

        // No allocation for unrelated skills. Delayed contacts keep their action's
        // set until its last scheduled effect resolves; nested casts have separate keys.
        public readonly struct CastScope : IDisposable
        {
            readonly CombatSession session;
            readonly BattleUnit source;
            readonly string actionId;
            public CastScope(CombatSession session, BattleUnit source, string actionId)
            { this.session = session; this.source = source; this.actionId = actionId; }
            public void Dispose()
            {
                var attempts = source?.MageState.TiringAttempts;
                if (attempts == null || !attempts.ContainsKey(actionId)) return;
                if (session?.ScheduledEffects != null)
                    foreach (var pending in session.ScheduledEffects)
                        if (pending?.ActionId == actionId && pending.CasterCharacterId == source.Character.CharacterId) return;
                attempts.Remove(actionId);
            }
        }
    }
}
