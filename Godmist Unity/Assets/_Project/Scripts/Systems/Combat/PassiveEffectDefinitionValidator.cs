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
                    ValidateCapability(operation, errors);
                    if (operation.Type is PassiveEffectOperationType.ModifyResourceCost or PassiveEffectOperationType.ModifySkillParameter)
                    {
                        foreach (var trigger in definition.Triggers ?? new List<PassiveEffectTriggerDefinition>())
                            if (trigger != null && trigger.Type is not (PassiveEffectTriggerType.AlwaysActive or PassiveEffectTriggerType.OnAttack))
                                errors.Add($"Modyfikator {operation.Id}: wymagany trigger AlwaysActive lub OnAttack.");
                        foreach (var condition in definition.Conditions ?? new List<PassiveEffectConditionDefinition>())
                            if (condition != null && condition.Type is not (PassiveEffectConditionType.Always
                                or PassiveEffectConditionType.CasterHasStatus or PassiveEffectConditionType.CasterHasBuff
                                or PassiveEffectConditionType.CasterHealthBelowPercent or PassiveEffectConditionType.CasterHealthAbovePercent
                                or PassiveEffectConditionType.Skill or PassiveEffectConditionType.AttackType
                                or PassiveEffectConditionType.Resource or PassiveEffectConditionType.StackCount))
                                errors.Add($"Modyfikator {operation.Id}: warunek {condition.Type} wymaga kontekstu niedostępnego we wspólnym podglądzie.");
                    }
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

                ValidateConditionShape(condition, index, errors);
            }
        }

        private static void ValidateConditionShape(
            PassiveEffectConditionDefinition condition,
            int index,
            List<string> errors)
        {
            var requiresReference = condition.Type is
                PassiveEffectConditionType.TargetHasStatus
                or PassiveEffectConditionType.CasterHasStatus
                or PassiveEffectConditionType.TargetHasTag
                or PassiveEffectConditionType.DungeonTheme
                or PassiveEffectConditionType.AttackType
                or PassiveEffectConditionType.AttackPosition
                or PassiveEffectConditionType.CasterHasBuff
                or PassiveEffectConditionType.TargetHasBuff
                or PassiveEffectConditionType.Skill
                or PassiveEffectConditionType.Resource
                or PassiveEffectConditionType.ReferencedEffectSucceeded
                or PassiveEffectConditionType.ReferencedEffectCrit
                or PassiveEffectConditionType.ReferencedEffectDodged;

            if (requiresReference && string.IsNullOrWhiteSpace(condition.ReferenceId))
            {
                errors.Add($"Warunek pasywki pod indeksem {index} typu {condition.Type} " +
                    "wymaga referencji.");
            }

            if (condition.Type == PassiveEffectConditionType.TargetHasTag
                && !string.IsNullOrWhiteSpace(condition.ReferenceId)
                && !CharacterTagUtility.TryParse(condition.ReferenceId, out _))
            {
                errors.Add($"Warunek pasywki pod indeksem {index} ma nieznany tag celu: " +
                    $"{condition.ReferenceId}.");
            }

            if (condition.Type == PassiveEffectConditionType.DungeonTheme
                && !string.IsNullOrWhiteSpace(condition.ReferenceId)
                && (!Enum.TryParse(condition.ReferenceId, ignoreCase: true,
                        out DungeonTheme theme)
                    || !Enum.IsDefined(typeof(DungeonTheme), theme)))
            {
                errors.Add($"Warunek pasywki pod indeksem {index} ma nieznany theme lochu: " +
                    $"{condition.ReferenceId}.");
            }

            var requiresValue = condition.Type is
                PassiveEffectConditionType.TargetHealthBelowPercent
                or PassiveEffectConditionType.CasterHealthBelowPercent
                or PassiveEffectConditionType.TargetHealthAbovePercent
                or PassiveEffectConditionType.CasterHealthAbovePercent
                or PassiveEffectConditionType.Distance
                or PassiveEffectConditionType.Resource
                or PassiveEffectConditionType.StackCount
                or PassiveEffectConditionType.Turn;

            if (requiresValue && condition.Value == null)
            {
                errors.Add($"Warunek pasywki pod indeksem {index} typu {condition.Type} " +
                    "wymaga wartosci.");
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

        private static void ValidateCapability(PassiveEffectOperationDefinition operation, List<string> errors)
        {
            if (operation.Type is PassiveEffectOperationType.ReduceIncomingDamage or PassiveEffectOperationType.MaxHealthFromMaxResource
                or PassiveEffectOperationType.LowHealthShield or PassiveEffectOperationType.OrderActionCostReduction)
            {
                if (operation.Target != PassiveEffectTargetType.Self || operation.Value?.Source != PassiveEffectValueSourceType.Constant)
                    errors.Add($"Operacja {operation.Id} wymaga celu Self i stałej wartości.");
                if (operation.Type == PassiveEffectOperationType.ReduceIncomingDamage
                    && (operation.ReferenceId is not (UniqueEquipmentCombatRules.FirstPhysicalHit or UniqueEquipmentCombatRules.PhysicalRanged)
                        || operation.Value?.NumberValue is < 0 or > 1))
                    errors.Add($"Operacja {operation.Id}: nieprawidłowy zakres redukcji obrażeń.");
            }
            if (operation.Type is PassiveEffectOperationType.ModifyStatus
                or PassiveEffectOperationType.RedirectDamage
                or PassiveEffectOperationType.ReflectDamage)
                errors.Add($"Operacja {operation.Id}: {operation.Type} nie ma wykonawcy pasywnego. Użyj obsługiwanego efektu skilla albo uzupełnij runtime.");
            if (operation.Type is PassiveEffectOperationType.ModifyResourceCost or PassiveEffectOperationType.ModifySkillParameter
                && operation.ModifierOperation == StatModifierOperation.Maximum)
                errors.Add($"Modyfikator {operation.Id} nie obsługuje operacji Maximum.");
            if (operation.Type == PassiveEffectOperationType.DealDamage
                && !string.IsNullOrWhiteSpace(operation.ReferenceId)
                && (!Enum.TryParse<DamageType>(operation.ReferenceId, true, out var damageType)
                    || !Enum.IsDefined(typeof(DamageType), damageType)))
                errors.Add($"Nieznany typ obrażeń reakcji: {operation.ReferenceId}.");
            if (operation.Type == PassiveEffectOperationType.ModifySkillParameter)
            {
                var key = operation.ReferenceId?.ToLowerInvariant();
                if (key is not ("resource_cost" or "accuracy" or "hit_count" or "additional_hit_trials" or "duration_turns"
                    or "status_chance" or "range" or "area_range" or "damage_multiplier"))
                    errors.Add($"Nieobsługiwany parametr skilla: {operation.ReferenceId}.");
            }
            if (operation.Type is PassiveEffectOperationType.ModifyResourceCost or PassiveEffectOperationType.ModifySkillParameter
                && operation.Value?.Source != PassiveEffectValueSourceType.Constant)
                errors.Add($"Modyfikator {operation.Id} wymaga stałej lub wartości rang, aby podgląd i wykonanie były zgodne.");
            if (operation.Type is PassiveEffectOperationType.Heal or PassiveEffectOperationType.RestoreResource or PassiveEffectOperationType.DealDamage
                or PassiveEffectOperationType.GrantShield or PassiveEffectOperationType.GrantActionPoints
                or PassiveEffectOperationType.ModifyActionValue
                && operation.Value?.Source is PassiveEffectValueSourceType.TargetCount or PassiveEffectValueSourceType.RuntimeState)
                errors.Add($"Źródło wartości {operation.Value.Source} nie jest obsługiwane dla {operation.Type}.");
        }

        private static void ValidateOperationReference(
            PassiveEffectOperationDefinition operation,
            List<string> errors)
        {
            var requiresReference = operation.Type is
                PassiveEffectOperationType.ModifySkillParameter
                or PassiveEffectOperationType.ApplyStatus
                or PassiveEffectOperationType.ModifyStatus
                or PassiveEffectOperationType.SetRuntimeFlag
                || operation.Type == PassiveEffectOperationType.ModifyGameplayVariable
                    && operation.VariableType == GameplayVariableType.DamageDealtPercentInDungeonTheme;

            if (requiresReference && string.IsNullOrWhiteSpace(operation.ReferenceId))
            {
                errors.Add($"Operacja {operation.Id} typu {operation.Type} wymaga referencji.");
            }
        }
    }
}
