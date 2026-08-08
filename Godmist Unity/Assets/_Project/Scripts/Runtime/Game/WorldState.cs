using System.Collections.Generic;

namespace Godmist.Runtime.Game
{
    public class WorldState
    {
        private readonly Dictionary<string, string> flagsById = new();

        public IReadOnlyDictionary<string, string> FlagsById => flagsById;

        public bool TrySetFlag(string flagId, string value)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            var normalizedValue = value ?? string.Empty;

            if (flagsById.TryGetValue(flagId, out var currentValue)
                && currentValue == normalizedValue)
            {
                return false;
            }

            flagsById[flagId] = normalizedValue;
            return true;
        }

        public bool TryGetFlag(string flagId, out string value)
        {
            value = string.Empty;

            return !string.IsNullOrWhiteSpace(flagId)
                && flagsById.TryGetValue(flagId, out value);
        }

        public bool HasFlag(string flagId, string expectedValue = null)
        {
            if (!TryGetFlag(flagId, out var currentValue))
            {
                return false;
            }

            return expectedValue == null || currentValue == expectedValue;
        }
    }
}