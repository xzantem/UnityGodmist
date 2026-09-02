using System.Collections.Generic;
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
            runtime = null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return false;
            }

            var lifetime = definition.Lifetime;
            runtime = new PassiveEffectRuntime
            {
                EffectId = definition.Id,
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

            return true;
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
