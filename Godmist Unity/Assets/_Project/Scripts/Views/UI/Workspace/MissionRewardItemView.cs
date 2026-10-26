using Godmist.Core.Enums;
using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Read-only preview of the exact item stored in a mission's reward offer.</summary>
    public sealed class MissionRewardItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image rarityAccent;
        [SerializeField] private Material rarityMaterial;
        [SerializeField] private Material godlyMaterial;
        [SerializeField] private TextMeshProUGUI nameText;
        public ItemInstance Item { get; private set; }

        public void Bind(ItemInstance item)
        {
            if (!ReferenceEquals(Item, item)) UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
            Item = item;
            gameObject.SetActive(item != null);
            if (item == null) return;
            icon.sprite = ItemIconPresentation.GetIcon(item);
            icon.enabled = icon.sprite != null;
            rarityAccent.color = ItemRarityPresentation.Color(item.CurrentRarity);
            rarityAccent.material = item.CurrentRarity == RarityTier.Godly ? godlyMaterial : rarityMaterial;
            nameText.text = ItemRarityPresentation.Name(item);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Item != null) UIWorkspaceTooltipView.ActiveInstance?.ShowItem(Item, null, this);
        }
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        private void OnDisable() => UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
    }
}
