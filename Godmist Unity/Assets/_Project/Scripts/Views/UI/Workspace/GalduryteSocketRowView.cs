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
                : targeted ? $"Do osadzenia: {ItemRarityPresentation.Name(galduryte)}" : ItemRarityPresentation.Name(galduryte);
            var effects = galduryte == null ? string.Empty : galduryte.GetEffectSummary();
            if (galduryte?.IsIdentified == true && !galduryte.IsDestroyed)
                effects = "• " + effects.Replace("\n", "\n• ");

            iconSlot?.SetIcon(ItemIconPresentation.GetIcon(galduryte));
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

        public void SetInactive(bool inactive)
        {
            if (inactive && titleText != null) titleText.text += " (nieaktywne gniazdo)";
        }

        public void BindForSocketing(GalduryteItemInstance galduryte, int socketNumber = 0)
        {
            Bind(galduryte, false);
            if (socketNumber > 0 && titleText != null)
                titleText.text = $"<size=80%><color=#B7B09B>Gniazdo {socketNumber}</color></size>\n" + titleText.text;
            iconSlot?.Bind(ItemIconPresentation.GetIcon(galduryte), string.Empty, string.Empty,
                string.Empty, false, false, null);
            if (galduryte != null) iconSlot?.SetItemTooltip(galduryte, null);
        }

        public void BindForExtraction(GalduryteItemInstance galduryte, bool selected, System.Action onClick)
        {
            Bind(galduryte, false);
            iconSlot?.Bind(ItemIconPresentation.GetIcon(galduryte), ItemRarityPresentation.Name(galduryte),
                galduryte?.GetEffectSummary(), string.Empty, selected, false, onClick);
            if (galduryte != null) iconSlot?.SetItemTooltip(galduryte, null);
        }
    }
}
