using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using UnityEngine;

namespace Godmist.Views.UI
{
    [CreateAssetMenu(
        fileName = "CombatStatusVisualCatalog",
        menuName = "Godmist/Combat/Status Visual Catalog")]
    public class CombatStatusVisualCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string EffectId;
            public Sprite Icon;
            public string DisplayName;
            [TextArea] public string Description;
        }

        [SerializeField] private Sprite genericBuffIcon;
        [SerializeField] private Sprite genericDebuffIcon;
        [SerializeField] private List<Entry> entries = new();

        public Sprite GetIcon(CombatStatusPresentation presentation)
        {
            if (presentation.Kind == CombatStatusKind.Debuff)
            {
                return genericDebuffIcon;
            }

            if (presentation.Kind == CombatStatusKind.Buff)
            {
                return genericBuffIcon;
            }

            var entry = FindEntry(presentation.Effect.EffectId);
            if (entry?.Icon != null)
            {
                return entry.Icon;
            }

            return null;
        }

        public string GetDisplayName(CombatStatusPresentation presentation)
        {
            if (presentation.Kind == CombatStatusKind.Debuff)
            {
                return "Debuff";
            }

            if (presentation.Kind == CombatStatusKind.Buff)
            {
                return "Buff";
            }

            var entry = FindEntry(presentation.Effect.EffectId);
            if (!string.IsNullOrWhiteSpace(entry?.DisplayName))
            {
                return entry.DisplayName;
            }

            return Humanize(presentation.Effect.EffectId);
        }

        public string GetDescription(CombatStatusPresentation presentation)
        {
            if (presentation.Kind == CombatStatusKind.Buff)
            {
                return "Positive temporary effect.";
            }

            if (presentation.Kind == CombatStatusKind.Debuff)
            {
                return "Negative temporary effect.";
            }

            var entry = FindEntry(presentation.Effect.EffectId);
            if (!string.IsNullOrWhiteSpace(entry?.Description))
            {
                return entry.Description;
            }

            return "Active status effect.";
        }

        public bool TryGetStatusTerm(
            string effectId,
            out Sprite icon,
            out string displayName,
            out string description)
        {
            var entry = FindEntry(effectId);
            if (entry == null)
            {
                icon = null;
                displayName = null;
                description = null;
                return false;
            }

            icon = entry.Icon;
            displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? Humanize(effectId)
                : entry.DisplayName;
            description = string.IsNullOrWhiteSpace(entry.Description)
                ? "Active status effect."
                : entry.Description;
            return true;
        }

        private Entry FindEntry(string effectId)
        {
            foreach (var entry in entries)
            {
                if (entry != null && string.Equals(entry.EffectId, effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Effect";
            return char.ToUpperInvariant(value[0]) + value.Substring(1).Replace('_', ' ');
        }
    }
}
