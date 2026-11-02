using Godmist.Core.Enums;
using Godmist.Data.Effects;

namespace Godmist.Systems.Combat
{
    // Declares existing executors' contract. Unsupported combinations do not acquire fallback semantics.
    public static class PassiveEffectOperationCapabilities
    {
        public static bool IsStatic(PassiveEffectDefinition definition) => definition != null && definition.IsAlwaysActive
            && !definition.IsTriggered && definition.Lifetime == PassiveEffectLifetime.Persistent;

        public static bool IsResolvedTarget(PassiveEffectTargetType target) => target is PassiveEffectTargetType.Self
            or PassiveEffectTargetType.Source or PassiveEffectTargetType.PrimaryTarget or PassiveEffectTargetType.Attacker
            or PassiveEffectTargetType.AllTargets or PassiveEffectTargetType.LowestHealthAlly;

        public static bool IsOwnerTarget(PassiveEffectTargetType target) => target is PassiveEffectTargetType.Self or PassiveEffectTargetType.Source;

        public static bool IsSpecialUnique(PassiveEffectOperationType operation) => operation is PassiveEffectOperationType.ReduceIncomingDamage
            or PassiveEffectOperationType.MaxHealthFromMaxResource or PassiveEffectOperationType.LowHealthShield or PassiveEffectOperationType.OrderActionCostReduction;

        public static bool SupportsValue(PassiveEffectDefinition definition, PassiveEffectOperationDefinition operation)
        {
            if (operation?.Value == null) return false;
            if (operation.Value.Source == PassiveEffectValueSourceType.Constant) return true;
            if (IsStatic(definition)) return false;
            return operation.Type is PassiveEffectOperationType.Heal or PassiveEffectOperationType.RestoreResource
                or PassiveEffectOperationType.GrantShield or PassiveEffectOperationType.GrantActionPoints
                or PassiveEffectOperationType.ModifyActionValue or PassiveEffectOperationType.DealDamage
                && operation.Value.Source is PassiveEffectValueSourceType.Stat or PassiveEffectValueSourceType.Resource
                    or PassiveEffectValueSourceType.StackCount or PassiveEffectValueSourceType.MissingHealthPercent
                    or PassiveEffectValueSourceType.MissingResourcePercent or PassiveEffectValueSourceType.Distance or PassiveEffectValueSourceType.EventValue;
        }

        public static bool SupportsTarget(PassiveEffectDefinition definition, PassiveEffectOperationDefinition operation)
        {
            if (definition == null || operation == null) return false;
            if (IsStatic(definition) && operation.Type is PassiveEffectOperationType.ModifyStat
                or PassiveEffectOperationType.ModifyResistance or PassiveEffectOperationType.ModifyGameplayVariable)
                return operation.Target == PassiveEffectTargetType.Self;
            return operation.Type switch
            {
                PassiveEffectOperationType.ModifyStat when operation.DurationTurns == 0
                    && operation.StatType is CharacterStatType.PhysicalDefensePenetration or CharacterStatType.MagicalDefensePenetration
                    => IsOwnerTarget(operation.Target),
                PassiveEffectOperationType.ModifyStat or PassiveEffectOperationType.ApplyStatus or PassiveEffectOperationType.DealDamage
                    or PassiveEffectOperationType.Heal or PassiveEffectOperationType.RestoreResource or PassiveEffectOperationType.GrantShield
                    or PassiveEffectOperationType.GrantActionPoints or PassiveEffectOperationType.ModifyActionValue
                    => IsResolvedTarget(operation.Target),
                PassiveEffectOperationType.ModifyGameplayVariable or PassiveEffectOperationType.ModifySkillParameter
                    or PassiveEffectOperationType.ModifyResourceCost or PassiveEffectOperationType.ModifyMovementCost
                    or PassiveEffectOperationType.PreventDamage or PassiveEffectOperationType.DuplicateAction
                    or PassiveEffectOperationType.ChangeHitResolution or PassiveEffectOperationType.SetRuntimeFlag
                    or PassiveEffectOperationType.PreventStatus => IsOwnerTarget(operation.Target),
                PassiveEffectOperationType.ReduceIncomingDamage or PassiveEffectOperationType.MaxHealthFromMaxResource
                    or PassiveEffectOperationType.LowHealthShield or PassiveEffectOperationType.OrderActionCostReduction
                    => operation.Target == PassiveEffectTargetType.Self,
                _ => false
            };
        }
    }
}
