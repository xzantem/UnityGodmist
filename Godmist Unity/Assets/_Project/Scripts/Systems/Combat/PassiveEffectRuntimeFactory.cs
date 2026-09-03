using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public static class PassiveEffectRuntimeFactory
    {
        public static bool TryCreate(
            PassiveEffectDefinition definition,
            PassiveEffectSourceRuntime source,
            out PassiveEffectRuntime runtime)
        {
            return TryCreate(
                definition,
                source,
                definition?.Id,
                out runtime,
                requireStaticDefinition: false);
        }

        public static bool TryCreate(
            PassiveEffectDefinition definition,
            PassiveEffectSourceRuntime source,
            string runtimeEffectId,
            out PassiveEffectRuntime runtime)
        {
            return TryCreate(
                definition,
                source,
                runtimeEffectId,
                out runtime,
                requireStaticDefinition: true);
        }

        private static bool TryCreate(
            PassiveEffectDefinition definition,
            PassiveEffectSourceRuntime source,
            string runtimeEffectId,
            out PassiveEffectRuntime runtime,
            bool requireStaticDefinition)
        {
            runtime = null;
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.Id)
                || string.IsNullOrWhiteSpace(runtimeEffectId)
                || requireStaticDefinition && !IsStaticDefinition(definition))
            {
                return false;
            }

            var lifetime = definition.Lifetime;
            runtime = new PassiveEffectRuntime
            {
                EffectId = runtimeEffectId,
                DefinitionId = definition.Id,
                DisplayName = definition.DisplayName ?? string.Empty,
                Sources = source == null
                    ? new List<PassiveEffectSourceRuntime>()
                    : new List<PassiveEffectSourceRuntime> { CopySource(source) },
                Stacks = 1,
                Lifetime = lifetime,
                RemainingDuration = lifetime == Core.Enums.PassiveEffectLifetime.Timed
                    ? definition.DefaultDurationTurns
                    : 0,
                TickPhase = lifetime == Core.Enums.PassiveEffectLifetime.Timed
                    ? definition.TickPhase
                    : Core.Enums.PassiveEffectTickPhase.None
            };

            var statModifiers = new List<StatModifier>();
            var variableModifiers = new List<GameplayVariableModifier>();
            ResistanceModifierPayload resistanceModifier = null;
            var materializePersistentPayloads = IsStaticDefinition(definition);

            foreach (var operation in definition.Operations ?? new List<PassiveEffectOperationDefinition>())
            {
                if (operation == null
                    || operation.Value == null
                    || operation.Value.Source != PassiveEffectValueSourceType.Constant
                    || requireStaticDefinition
                        && operation.Target != PassiveEffectTargetType.Self)
                {
                    return false;
                }

                if (operation.Type == PassiveEffectOperationType.ModifyGameplayVariable
                    && operation.VariableType == GameplayVariableType.DamageDealtPercentInDungeonTheme
                    && string.IsNullOrWhiteSpace(operation.ReferenceId))
                {
                    return false;
                }

                switch (operation.Type)
                {
                    case PassiveEffectOperationType.ModifyStat:
                        if (materializePersistentPayloads)
                        {
                            statModifiers.Add(new StatModifier
                            {
                                StatType = operation.StatType,
                                Operation = operation.ModifierOperation,
                                Stacking = operation.ModifierStacking,
                                Value = operation.Value.NumberValue
                            });
                        }
                        break;

                    case PassiveEffectOperationType.ModifyResistance:
                        if (!materializePersistentPayloads) break;
                        if (resistanceModifier != null) return false;
                        resistanceModifier = new ResistanceModifierPayload
                        {
                            ResistanceType = operation.ResistanceType,
                            Value = operation.Value.NumberValue
                        };
                        break;

                    case PassiveEffectOperationType.ModifyGameplayVariable:
                        if (!materializePersistentPayloads) break;
                        variableModifiers.Add(new GameplayVariableModifier
                        {
                            VariableType = operation.VariableType,
                            ReferenceId = operation.ReferenceId ?? string.Empty,
                            Stacking = operation.ModifierStacking,
                            Value = operation.Value.NumberValue
                        });
                        break;

                    default:
                        // Triggered definitions are kept as runtime carriers. Their
                        // executable operations are resolved against the combat
                        // context by PassiveEffectCombatExecutionSystem instead of
                        // being flattened into persistent stat payloads.
                        if (requireStaticDefinition)
                            return false;
                        break;
                }
            }

            if (statModifiers.Count > 0)
                runtime.StatModifiers = statModifiers;
            if (variableModifiers.Count > 0)
                runtime.VariableModifiers = variableModifiers;
            if (resistanceModifier != null)
                runtime.SetPayload(resistanceModifier);

            return true;
        }

        private static bool IsStaticDefinition(PassiveEffectDefinition definition)
        {
            return definition.IsAlwaysActive
                && !definition.IsTriggered
                && definition.Lifetime == PassiveEffectLifetime.Persistent;
        }

        private static PassiveEffectSourceRuntime CopySource(PassiveEffectSourceRuntime source)
        {
            return new PassiveEffectSourceRuntime
            {
                SourceType = source.SourceType,
                SourceId = source.SourceId ?? string.Empty,
                SourceCharacterId = source.SourceCharacterId ?? string.Empty
            };
        }
    }
}
