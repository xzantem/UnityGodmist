using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public sealed class PassiveEffectEvaluationResult
    {
        public bool IsValid { get; }
        public bool ConditionsMet { get; }
        public bool TriggerMatched { get; }
        public bool CanApply => IsValid && ConditionsMet && TriggerMatched;
        public IReadOnlyList<PassiveEffectOperationDefinition> Operations { get; }
        public string FailureReason { get; }

        internal PassiveEffectEvaluationResult(
            bool isValid,
            bool conditionsMet,
            bool triggerMatched,
            IReadOnlyList<PassiveEffectOperationDefinition> operations,
            string failureReason)
        {
            IsValid = isValid;
            ConditionsMet = conditionsMet;
            TriggerMatched = triggerMatched;
            Operations = operations ?? Array.Empty<PassiveEffectOperationDefinition>();
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public static class PassiveEffectCombatEvaluator
    {
        public static PassiveEffectEvaluationResult Evaluate(
            PassiveEffectDefinition definition,
            PassiveEffectRuntime runtime,
            PassiveEffectCombatEvent combatEvent)
        {
            if (definition == null || combatEvent?.Context == null)
            {
                return Invalid("Brak definicji albo kontekstu combatowego.");
            }

            var validation = PassiveEffectDefinitionValidator.Validate(definition);
            if (!validation.IsValid)
            {
                return Invalid(string.Join(" ", validation.Errors));
            }

            if (runtime != null && !PassiveEffectLifecycleSystem.IsActive(runtime))
            {
                return Invalid("Runtime pasywki nie jest aktywny.");
            }

            if (!TryEvaluateConditions(definition, runtime, combatEvent.Context,
                    out var conditionsMet, out var conditionError))
            {
                return Invalid(conditionError);
            }

            if (!TryMatchTrigger(definition, combatEvent, out var triggerMatched,
                    out var triggerError))
            {
                return Invalid(triggerError);
            }

            return new PassiveEffectEvaluationResult(
                isValid: true,
                conditionsMet,
                triggerMatched,
                definition.Operations,
                string.Empty);
        }

        private static bool TryEvaluateConditions(
            PassiveEffectDefinition definition,
            PassiveEffectRuntime runtime,
            PassiveEffectCombatContext context,
            out bool result,
            out string error)
        {
            result = definition.Conditions == null || definition.Conditions.Count == 0;
            error = string.Empty;
            if (result)
            {
                return true;
            }

            var any = false;
            var all = true;
            foreach (var condition in definition.Conditions)
            {
                if (condition == null)
                {
                    error = "Pasywka zawiera pusty warunek.";
                    return false;
                }

                if (!TryEvaluateCondition(condition, runtime, context,
                        out var conditionResult))
                {
                    error = $"Warunek {condition.Type} nie ma kompletnego kontekstu.";
                    return false;
                }

                if (condition.Negated)
                {
                    conditionResult = !conditionResult;
                }

                any |= conditionResult;
                all &= conditionResult;
            }

            result = definition.ConditionCombination == PassiveEffectConditionCombination.Any
                ? any
                : all;
            return true;
        }

        private static bool TryEvaluateCondition(
            PassiveEffectConditionDefinition condition,
            PassiveEffectRuntime runtime,
            PassiveEffectCombatContext context,
            out bool result)
        {
            result = false;
            switch (condition.Type)
            {
                case PassiveEffectConditionType.Always:
                    result = true;
                    return true;

                case PassiveEffectConditionType.TargetHasStatus:
                case PassiveEffectConditionType.TargetHasBuff:
                    result = HasEffect(context.Target, condition.ReferenceId);
                    return true;

                case PassiveEffectConditionType.CasterHasStatus:
                case PassiveEffectConditionType.CasterHasBuff:
                    result = HasEffect(context.Source, condition.ReferenceId);
                    return true;

                case PassiveEffectConditionType.TargetHealthBelowPercent:
                    return CompareHealth(context.Target, condition, below: true, out result);
                case PassiveEffectConditionType.TargetHealthAbovePercent:
                    return CompareHealth(context.Target, condition, below: false, out result);
                case PassiveEffectConditionType.CasterHealthBelowPercent:
                    return CompareHealth(context.Source, condition, below: true, out result);
                case PassiveEffectConditionType.CasterHealthAbovePercent:
                    return CompareHealth(context.Source, condition, below: false, out result);

                case PassiveEffectConditionType.TargetHasTag:
                    if (!CharacterTagUtility.TryParse(condition.ReferenceId, out var targetTag))
                        return false;
                    foreach (var tag in context.TargetTags)
                    {
                        if (CharacterTagUtility.TryParse(tag, out var resolvedTag)
                            && resolvedTag == targetTag)
                        {
                            result = true;
                            break;
                        }
                    }
                    return true;

                case PassiveEffectConditionType.DungeonTheme:
                    if (!Enum.TryParse(condition.ReferenceId, ignoreCase: true,
                            out DungeonTheme dungeonTheme)
                        || !Enum.IsDefined(typeof(DungeonTheme), dungeonTheme))
                    {
                        return false;
                    }

                    result = context.Session != null
                        && context.Session.DungeonTheme == dungeonTheme;
                    return true;

                case PassiveEffectConditionType.AttackType:
                    return CompareEnum(context.AttackType, condition.ReferenceId, out result);
                case PassiveEffectConditionType.AttackPosition:
                    return CompareEnum(context.AttackPosition, condition.ReferenceId, out result);
                case PassiveEffectConditionType.Distance:
                    return CompareNumber(context.Distance, condition, valueIsAvailable: context.Distance >= 0,
                        out result);
                case PassiveEffectConditionType.Skill:
                    result = !string.IsNullOrWhiteSpace(condition.ReferenceId)
                        && string.Equals(context.SkillId, condition.ReferenceId,
                            StringComparison.OrdinalIgnoreCase);
                    return !string.IsNullOrWhiteSpace(condition.ReferenceId);
                case PassiveEffectConditionType.Resource:
                    if (!TryMatchResourceType(context.ResourceType, condition.ReferenceId,
                            out var resourceMatches)) return false;
                    if (!CompareNumber(context.ResourceAmount, condition,
                            valueIsAvailable: true, out var amountMatches))
                    {
                        return false;
                    }
                    result = resourceMatches && amountMatches;
                    return true;
                case PassiveEffectConditionType.StackCount:
                    return CompareNumber(runtime?.Stacks ?? context.StackCount, condition,
                        valueIsAvailable: true, out result);
                case PassiveEffectConditionType.Turn:
                    return CompareNumber(context.TurnNumber, condition,
                        valueIsAvailable: context.TurnNumber >= 0, out result);
                case PassiveEffectConditionType.ReferencedEffectSucceeded:
                    result = context.ReferencedEffectSucceeded == true;
                    return context.ReferencedEffectSucceeded.HasValue;
                case PassiveEffectConditionType.ReferencedEffectCrit:
                    result = context.ReferencedEffectCrit == true;
                    return context.ReferencedEffectCrit.HasValue;
                case PassiveEffectConditionType.ReferencedEffectDodged:
                    result = context.ReferencedEffectDodged == true;
                    return context.ReferencedEffectDodged.HasValue;
                default:
                    return false;
            }
        }

        private static bool TryMatchTrigger(
            PassiveEffectDefinition definition,
            PassiveEffectCombatEvent combatEvent,
            out bool result,
            out string error)
        {
            result = false;
            error = string.Empty;
            if (definition.Triggers == null || definition.Triggers.Count == 0)
            {
                result = true;
                return true;
            }

            foreach (var trigger in definition.Triggers)
            {
                if (trigger == null)
                {
                    error = "Pasywka zawiera pusty trigger.";
                    return false;
                }

                var typeMatches = trigger.Type == PassiveEffectTriggerType.AlwaysActive
                    || trigger.Type == combatEvent.TriggerType;
                var idMatches = string.IsNullOrWhiteSpace(trigger.EventId)
                    || string.Equals(trigger.EventId, combatEvent.EventId,
                        StringComparison.OrdinalIgnoreCase);
                result |= typeMatches && idMatches;
            }

            return true;
        }

        private static bool HasEffect(BattleUnit unit, string effectId)
        {
            return unit?.Character != null
                && PassiveEffectLifecycleSystem.HasActiveEffect(
                    unit.Character, effectId);
        }

        private static bool CompareHealth(
            BattleUnit unit,
            PassiveEffectConditionDefinition condition,
            bool below,
            out bool result)
        {
            result = false;
            if (unit?.Character == null || unit.Character.Stats?.MaxHealth == null)
            {
                return false;
            }

            var maxHealth = unit.Character.Stats.MaxHealth.GetValue();
            if (maxHealth <= 0f) return false;
            var percent = unit.Character.CurrentHealth / maxHealth;
            var threshold = NormalizePercent(condition.Value?.NumberValue ?? 0f);
            result = below ? percent < threshold : percent > threshold;
            return true;
        }

        private static bool CompareNumber(
            float actual,
            PassiveEffectConditionDefinition condition,
            bool valueIsAvailable,
            out bool result)
        {
            result = false;
            if (!valueIsAvailable || condition.Value == null) return false;
            result = Compare(actual, condition.Value.NumberValue, condition.Comparison);
            return true;
        }

        private static bool CompareEnum<T>(T actual, string expected, out bool result)
            where T : struct, Enum
        {
            result = false;
            if (string.IsNullOrWhiteSpace(expected)
                || !Enum.TryParse(expected, ignoreCase: true, out T parsed))
            {
                return false;
            }

            result = EqualityComparer<T>.Default.Equals(actual, parsed);
            return true;
        }

        private static bool TryMatchResourceType(
            ResourceType actual,
            string expected,
            out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(expected)
                || !Enum.TryParse(expected, true, out ResourceType parsed))
            {
                return false;
            }

            result = actual == parsed;
            return true;
        }

        private static bool Compare(
            float actual,
            float expected,
            PassiveEffectComparisonType comparison)
        {
            return comparison switch
            {
                PassiveEffectComparisonType.Equal => Math.Abs(actual - expected) < 0.0001f,
                PassiveEffectComparisonType.NotEqual => Math.Abs(actual - expected) >= 0.0001f,
                PassiveEffectComparisonType.LessThan => actual < expected,
                PassiveEffectComparisonType.LessOrEqual => actual <= expected,
                PassiveEffectComparisonType.GreaterThan => actual > expected,
                PassiveEffectComparisonType.GreaterOrEqual => actual >= expected,
                _ => false
            };
        }

        private static float NormalizePercent(float value)
        {
            return Math.Abs(value) > 1f ? value / 100f : value;
        }

        private static PassiveEffectEvaluationResult Invalid(string reason)
        {
            return new PassiveEffectEvaluationResult(
                isValid: false,
                conditionsMet: false,
                triggerMatched: false,
                operations: Array.Empty<PassiveEffectOperationDefinition>(),
                failureReason: reason);
        }
    }
}
