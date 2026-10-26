using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler
    {
        private static readonly Color UnavailableOverlayColor = new(0.45f, 0.06f, 0.06f, 0.48f);

        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private Image rarityAccent;
        [SerializeField] private Image saleProtectionBadge;
        [SerializeField] private Material rarityAccentMaterial;
        [SerializeField] private Material godlyAccentMaterial;
        [SerializeField] private TextMeshProUGUI countText;
        private ItemInstance item;
        private CharacterInfoWindowView owner;

        public ItemInstance Item => item;
        public CharacterInfoWindowView Owner => owner;

        public static Color RarityAccentColor(Godmist.Core.Enums.RarityTier rarity) => ItemRarityPresentation.Color(rarity);

        private void OnDisable() => UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);

        public void OnSubmit(BaseEventData eventData) => Activate();

        private void Activate()
        {
            if (!isActiveAndEnabled || button != null && (!button.IsActive() || !button.IsInteractable())) return;
            owner?.TryActivateItem(item);
        }
        public void Bind(
            ItemInstance nextItem,
            int stackCount,
            CharacterInfoWindowView nextOwner)
        {
            if (!ReferenceEquals(item, nextItem))
                UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
            item = nextItem; owner = nextOwner;
            if (saleProtectionBadge != null) saleProtectionBadge.gameObject.SetActive(item?.IsSaleProtected == true);
            if (rarityAccent != null)
            {
                rarityAccent.enabled = item != null;
                rarityAccent.color = RarityAccentColor(item?.CurrentRarity ?? Godmist.Core.Enums.RarityTier.Common);
                rarityAccent.material = item?.CurrentRarity == Godmist.Core.Enums.RarityTier.Godly
                    ? godlyAccentMaterial : rarityAccentMaterial;
            }
            if (icon != null)
            {
                icon.sprite = ItemIconPresentation.GetIcon(item);
                icon.enabled = icon.sprite != null;
            }
            var unavailable = item is EquipmentItemInstance equipment
                && !owner.MeetsEquipmentRequirements(equipment);
            if (unavailableOverlay != null)
            {
                unavailableOverlay.color = UnavailableOverlayColor;
                unavailableOverlay.enabled = unavailable;
            }

            countText ??= transform.Find("CountLabel")?.GetComponent<TextMeshProUGUI>();
            if (countText != null)
            {
                var isStackable = item?.Definition?.IsStackable ?? false;
                countText.gameObject.SetActive(isStackable);
                if (isStackable) countText.text = Mathf.Max(1, stackCount).ToString();
            }
        }
        public void OnBeginDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.BeginDrag(item, eventData); }
        public void OnDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.UpdateDrag(eventData); }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            owner?.TryDropToCampaignStorage(item, eventData);
            owner?.EndDrag();
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.dragging) return;
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
                Activate();
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                owner?.ShowItemMenu(item, eventData.position);
            }
        }
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(item, owner?.Character, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }

}
