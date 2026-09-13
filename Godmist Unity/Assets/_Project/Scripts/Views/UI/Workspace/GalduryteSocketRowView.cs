using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Static prefab row used to show an occupied or targeted galduryte socket.</summary>
    public sealed class GalduryteSocketRowView : MonoBehaviour
    {
        [SerializeField] private BlacksmithCraftingSlotView iconSlot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI effectText;

        public void Bind(GalduryteItemInstance galduryte, bool targeted)
        {
            var title = galduryte == null
                ? "Pusty socket"
                : targeted ? $"Do osadzenia: {galduryte.DisplayName}" : galduryte.DisplayName;
            var effects = galduryte == null ? string.Empty : galduryte.GetEffectSummary();

            iconSlot?.SetIcon(galduryte?.Definition?.Icon);
            iconSlot?.SetSelected(targeted);
            if (galduryte != null) iconSlot?.SetItemTooltip(galduryte, null);
            else iconSlot?.SetTooltip("Pusty socket", string.Empty);
            if (titleText != null) titleText.text = title;
            if (effectText != null)
            {
                effectText.text = effects;
                effectText.gameObject.SetActive(!string.IsNullOrWhiteSpace(effects));
            }
        }

        public void BindForExtraction(GalduryteItemInstance galduryte, bool selected, System.Action onClick)
        {
            Bind(galduryte, false);
            iconSlot?.Bind(galduryte?.Definition?.Icon, galduryte?.DisplayName,
                galduryte?.GetEffectSummary(), string.Empty, selected, false, onClick);
            if (galduryte != null) iconSlot?.SetItemTooltip(galduryte, null);
        }
    }
}
