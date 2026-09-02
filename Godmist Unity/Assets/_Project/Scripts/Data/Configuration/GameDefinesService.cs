using System;
using System.Collections.Generic;

namespace Godmist.Data.Configuration
{
    public sealed class GameDefinesService
    {
        private readonly Dictionary<string, GameDefineEntry> configured;
        private readonly Dictionary<string, GameDefineEntry> defaults;

        public GameDefinesService(GameDefines definitions = null)
        {
            configured = BuildLookup(definitions?.Entries);
            defaults = BuildLookup(GameDefineDefaults.CreateEntries());
        }

        public int GetInt(string id, int fallback = 0)
        {
            if (TryGetEntry(id, out var entry)
                && entry.ValueType == GameDefineValueType.Integer)
            {
                return entry.CurrentIntegerValue;
            }

            if (TryGetDefault(id, out var defaultEntry)
                && defaultEntry.ValueType == GameDefineValueType.Integer)
            {
                return defaultEntry.DefaultIntegerValue;
            }

            return fallback;
        }

        public float GetFloat(string id, float fallback = 0f)
        {
            if (TryGetEntry(id, out var entry))
            {
                if (entry.ValueType == GameDefineValueType.Float) return entry.CurrentFloatValue;
                if (entry.ValueType == GameDefineValueType.Integer) return entry.CurrentIntegerValue;
            }

            if (TryGetDefault(id, out var defaultEntry))
            {
                if (defaultEntry.ValueType == GameDefineValueType.Float) return defaultEntry.DefaultFloatValue;
                if (defaultEntry.ValueType == GameDefineValueType.Integer) return defaultEntry.DefaultIntegerValue;
            }

            return fallback;
        }

        public bool GetBool(string id, bool fallback = false)
        {
            if (TryGetEntry(id, out var entry) && entry.ValueType == GameDefineValueType.Boolean)
            {
                return entry.CurrentBooleanValue;
            }

            return TryGetDefault(id, out var defaultEntry)
                && defaultEntry.ValueType == GameDefineValueType.Boolean
                ? defaultEntry.DefaultBooleanValue
                : fallback;
        }

        public string GetString(string id, string fallback = "")
        {
            if (TryGetEntry(id, out var entry) && entry.ValueType == GameDefineValueType.String)
            {
                return entry.CurrentStringValue ?? fallback;
            }

            return TryGetDefault(id, out var defaultEntry)
                && defaultEntry.ValueType == GameDefineValueType.String
                ? defaultEntry.DefaultStringValue ?? fallback
                : fallback;
        }

        public bool TryGetEntry(string id, out GameDefineEntry entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(id)
                && configured.TryGetValue(id.Trim(), out entry)
                && entry.TryValidate(out _);
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in configured.Values)
            {
                if (!ids.Add(entry.Id)) errors.Add($"Zduplikowany ID: {entry.Id}");
                if (!entry.TryValidate(out var error)) errors.Add($"{entry.Id}: {error}");
            }
            return errors;
        }

        private bool TryGetDefault(string id, out GameDefineEntry entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(id) && defaults.TryGetValue(id.Trim(), out entry);
        }

        private static Dictionary<string, GameDefineEntry> BuildLookup(
            IEnumerable<GameDefineEntry> entries)
        {
            var lookup = new Dictionary<string, GameDefineEntry>(StringComparer.OrdinalIgnoreCase);
            if (entries == null) return lookup;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                lookup.TryAdd(entry.Id.Trim(), entry);
            }
            return lookup;
        }

    }

    public static class GameDefinesRuntime
    {
        private static GameDefinesService current = new();

        public static GameDefinesService Current => current;

        public static void Configure(GameDefines definitions)
        {
            current = new GameDefinesService(definitions);
        }

        public static int GetInt(string id, int fallback = 0) => current.GetInt(id, fallback);
        public static float GetFloat(string id, float fallback = 0f) => current.GetFloat(id, fallback);
        public static bool GetBool(string id, bool fallback = false) => current.GetBool(id, fallback);
        public static string GetString(string id, string fallback = "") => current.GetString(id, fallback);
    }
}
