using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed partial class PassiveEffectCombatExecutionSystem
    {
        private readonly HashSet<string> reactionActions = new();
        // The driver supplies the same damage owner used by active skills.
        public CombatDamageSystem ReactionDamageSystem { get; set; }

        private void ExecuteReaction(BattleUnit owner, PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation, PassiveEffectCombatContext context)
        {
            if (owner?.Character?.LifeState != CharacterLifeState.Alive) return;
            if (HasParameter(operation, "frequency", "once_per_action")
                && !string.IsNullOrEmpty(context.ActionId)
                && !reactionActions.Add(owner.Character.CharacterId + "|" + runtime.EffectId
                    + "|" + operation.Id + "|" + context.ActionId)) return;
            if (TryGetParameter(operation, "chance", out var chance) && UnityEngine.Random.value >= Mathf.Clamp01(chance)) return;
            var recovery = new CombatRecoverySystem(battleLog);
            foreach (var target in ResolveTargets(owner, operation.Target, context))
            {
                if (target?.Character?.LifeState != CharacterLifeState.Alive) continue;
                var amount = Mathf.Max(0, Mathf.RoundToInt(ResolveReactionValue(operation.Value,
                    runtime, owner, target, context)));
                switch (operation.Type)
                {
                    case PassiveEffectOperationType.Heal:
                        recovery.Heal(target, amount, new PassiveEffectCombatContext(session: session,
                            source: owner, target: target, actionId: context.ActionId, isPassiveReaction: true));
                        break;
                    case PassiveEffectOperationType.RestoreResource:
                        recovery.RestoreResource(target, amount);
                        break;
                    case PassiveEffectOperationType.GrantShield:
                        target.Character.ShieldPool?.AddLayer(amount, runtime.EffectId);
                        break;
                    case PassiveEffectOperationType.GrantActionPoints:
                        target.CurrentActionPoints = Mathf.Clamp(
                            target.CurrentActionPoints + Mathf.RoundToInt(ResolveReactionValue(operation.Value,
                                runtime, owner, target, context) * ActionPointUnits.PerPoint), 0, target.MaxActionPoints);
                        break;
                    case PassiveEffectOperationType.ModifyActionValue:
                        target.CurrentActionValue = Mathf.Max(0, target.CurrentActionValue
                            - CombatTurnSystem.CalculateBaseActionValue(target) * ResolveReactionValue(operation.Value,
                                runtime, owner, target, context));
                        CombatTurnSystem.ReorderParticipants(session);
                        break;
                    case PassiveEffectOperationType.DealDamage:
                        var damageType = Enum.TryParse<DamageType>(operation.ReferenceId, true, out var type)
                            ? type : DamageType.Physical;
                        var reaction = new PassiveEffectCombatContext(session: session,
                            source: owner, target: target, skillId: context.SkillId, skill: context.Skill,
                            actionId: context.ActionId, damageType: damageType, isPassiveReaction: true);
                        (ReactionDamageSystem ?? new CombatDamageSystem(battleLog)).ResolveAndApplyDamage(
                            session, owner, target, amount, damageType, true, passiveContext: reaction);
                        break;
                }
            }
        }

        private static float ResolveReactionValue(PassiveEffectValueDefinition value,
            PassiveEffectRuntime runtime, BattleUnit owner, BattleUnit target, PassiveEffectCombatContext context)
        {
            if (value == null) return 0;
            if (value.Source == PassiveEffectValueSourceType.Constant) return ResolveValue(value, runtime);
            float raw = 0;
            switch (value.Source)
            {
                case PassiveEffectValueSourceType.EventValue:
                    raw = value.ReferenceId == "health_damage" ? context.HealthDamage : context.EventValue; break;
                case PassiveEffectValueSourceType.StackCount: raw = runtime?.Stacks ?? 1; break;
                case PassiveEffectValueSourceType.Resource: raw = owner.Character.ResourcePool?.CurrentValue ?? 0; break;
                case PassiveEffectValueSourceType.Distance: raw = Math.Max(0, context.Distance); break;
                case PassiveEffectValueSourceType.Stat:
                    if (Enum.TryParse<CharacterStatType>(value.ReferenceId, true, out var stat)
                        && Enum.IsDefined(typeof(CharacterStatType), stat))
                        raw = owner.Character.Stats.GetStat(stat).GetValue();
                    break;
                case PassiveEffectValueSourceType.MissingHealthPercent:
                    raw = 1 - owner.Character.CurrentHealth / Mathf.Max(1, owner.Character.Stats.MaxHealth.GetValue());
                    break;
                case PassiveEffectValueSourceType.MissingResourcePercent:
                    var pool = owner.Character.ResourcePool;
                    raw = pool == null ? 0 : 1 - (float)pool.CurrentValue / Mathf.Max(1, pool.MaxValue);
                    break;
            }
            return raw * value.Scale + value.Offset;
        }
    }
}
