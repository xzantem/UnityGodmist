using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;

namespace Godmist.Systems.Combat
{
    public sealed class PassiveEffectValidationResult
    {
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;

        public PassiveEffectValidationResult(List<string> errors)
        {
            Errors = errors ?? new List<string>();
        }
    }

    public static class PassiveEffectDefinitionValidator
    {
        public static PassiveEffectValidationResult Validate(
            PassiveEffectDefinition definition)
        {
            var errors = new List<string>();
            if (definition == null)
            {
                errors.Add("Definicja pasywki jest pusta.");
                return new PassiveEffectValidationResult(errors);
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
                errors.Add("Definicja pasywki wymaga stabilnego ID.");

            if (definition.Lifetime == PassiveEffectLifetime.Timed
                && definition.DefaultDurationTurns <= 0)
            {
                errors.Add("Pasywka czasowa wymaga dodatniego domyslnego czasu trwania.");
            }

            if (definition.Operations == null || definition.Operations.Count == 0)
            {
                errors.Add("Definicja pasywki wymaga co najmniej jednej operacji.");
            }
            else
            {
                var operationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < definition.Operations.Count; index++)
                {
                    var operation = definition.Operations[index];
                    if (operation == null)
                    {
                        errors.Add($"Operacja pasywki pod indeksem {index} jest pusta.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(operation.Id))
                    {
                        errors.Add($"Operacja pasywki pod indeksem {index} wymaga stabilnego ID.");
                    }
                    else if (!operationIds.Add(operation.Id))
                    {
                        errors.Add($"Operacje pasywki maja zduplikowane ID: {operation.Id}.");
                    }

                    ValidateOperationReference(operation, errors);
                }
            }

            ValidateConditions(definition.Conditions, errors);
            ValidateTriggers(definition.Triggers, errors);
            return new PassiveEffectValidationResult(errors);
        }

        private static void ValidateConditions(
            List<PassiveEffectConditionDefinition> conditions,
            List<string> errors)
        {
            if (conditions == null) return;

            for (var index = 0; index < conditions.Count; index++)
            {
                var condition = conditions[index];
                if (condition == null)
                {
                    errors.Add($"Warunek pasywki pod indeksem {index} jest pusty.");
                    continue;
                }

                if (condition.Type == PassiveEffectConditionType.Always
                    && !string.IsNullOrWhiteSpace(condition.ReferenceId))
                {
                    errors.Add("Warunek Always nie moze miec referencji.");
                }
            }
        }

        private static void ValidateTriggers(
            List<PassiveEffectTriggerDefinition> triggers,
            List<string> errors)
        {
            if (triggers == null) return;

            for (var index = 0; index < triggers.Count; index++)
            {
                var trigger = triggers[index];
                if (trigger == null)
                {
                    errors.Add($"Trigger pasywki pod indeksem {index} jest pusty.");
                }
            }
        }

        private static void ValidateOperationReference(
            PassiveEffectOperationDefinition operation,
            List<string> errors)
        {
            var requiresReference = operation.Type is
                PassiveEffectOperationType.ModifySkillParameter
                or PassiveEffectOperationType.ApplyStatus
                or PassiveEffectOperationType.ModifyStatus
                or PassiveEffectOperationType.SetRuntimeFlag;

            if (requiresReference && string.IsNullOrWhiteSpace(operation.ReferenceId))
            {
                errors.Add($"Operacja {operation.Id} typu {operation.Type} wymaga referencji.");
            }
        }
    }
}
