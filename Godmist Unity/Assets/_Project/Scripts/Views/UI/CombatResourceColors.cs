using Godmist.Core.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>
    /// Keeps every combat HUD presentation of a resource visually consistent.
    /// A resource's colour represents its gameplay type, never its owner.
    /// </summary>
    public static class CombatResourceColors
    {
        private static readonly Color Neutral = new(0.64f, 0.67f, 0.73f, 1f);
        private static readonly Color Rage = new(0.84f, 0.24f, 0.18f, 1f);
        private static readonly Color Momentum = new(0.25f, 0.78f, 0.55f, 1f);
        private static readonly Color Mana = new(0.27f, 0.59f, 0.94f, 1f);

        public static string ResourceName(ResourceType type) => type switch
        {
            ResourceType.Rage => "Furia",
            ResourceType.Momentum => "Impet",
            ResourceType.Mana => "Mana",
            _ => "Zasób"
        };

        public static Color Get(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Rage => Rage,
                ResourceType.Momentum => Momentum,
                ResourceType.Mana => Mana,
                _ => Neutral
            };
        }

        public static void ApplyToFill(Slider slider, ResourceType resourceType)
        {
            CombatBarPresentation.FitFrame(slider);
            if (slider?.fillRect == null)
            {
                return;
            }

            var fillImage = slider.fillRect.GetComponent<Image>()
                ?? slider.fillRect.GetComponentInChildren<Image>();
            if (fillImage != null)
            {
                fillImage.color = Get(resourceType);
            }
        }
    }
}
