using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace Godmist.Data.Configuration
{
    [CreateAssetMenu(fileName = "GameDefines", menuName = "Godmist/Configuration/Game Defines")]
    public sealed class GameDefines : ScriptableObject
    {
        [SerializeField] private List<GameDefineEntry> entries = new();

        public IReadOnlyList<GameDefineEntry> Entries => entries;

        private void OnEnable()
        {
            var defaults = GameDefineDefaults.CreateEntries();
            if (entries == null || entries.Count == 0)
            {
                entries = defaults;
                return;
            }

            foreach (var defaultEntry in defaults)
            {
                if (defaultEntry != null && !TryGet(defaultEntry.Id, out _))
                {
                    entries.Add(defaultEntry);
                }
            }
        }

        private void OnValidate()
        {
            if (entries == null) entries = new List<GameDefineEntry>();

            var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                entry.Normalize();
                if (!string.IsNullOrWhiteSpace(entry.Id) && !ids.Add(entry.Id))
                {
                    Debug.LogWarning($"GameDefines zawiera zduplikowany ID '{entry.Id}'.", this);
                }
            }
        }

        public bool TryGet(string id, out GameDefineEntry entry)
        {
            entry = entries?.FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.Id, id, System.StringComparison.OrdinalIgnoreCase));
            return entry != null;
        }

        public void AddEntry(GameDefineEntry entry)
        {
            entries ??= new List<GameDefineEntry>();
            entries.Add(entry);
        }

        public bool RemoveEntryAt(int index)
        {
            if (entries == null || index < 0 || index >= entries.Count) return false;
            entries.RemoveAt(index);
            return true;
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (entries == null || entries.Count == 0)
            {
                errors.Add("GameDefines nie zawiera żadnych parametrów.");
                return errors;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    errors.Add("GameDefines zawiera pusty rekord.");
                    continue;
                }

                if (!ids.Add(entry.Id ?? string.Empty))
                {
                    errors.Add($"Zduplikowany ID parametru: {entry.Id}");
                }

                if (!entry.TryValidate(out var error))
                {
                    errors.Add($"{entry.Id}: {error}");
                }
            }

            return errors;
        }
    }
}
