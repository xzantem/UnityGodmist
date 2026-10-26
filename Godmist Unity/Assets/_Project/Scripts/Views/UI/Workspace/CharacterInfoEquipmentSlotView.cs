using Godmist.Core.Enums;
using Godmist.Runtime.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoEquipmentSlotView : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        ISubmitHandler,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private static readonly Color UnavailableOverlayColor = new(0.45f, 0.06f, 0.06f, 0.48f);

        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private Button button;
        private ItemRarityBorderGraphic rarityBorder;
        [SerializeField] private Image icon;
        [SerializeField] private Image feedbackBorder;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private Image selectionBorder;
        [SerializeField] private Image emptySlotIcon;
        [SerializeField] private Image saleProtectionBadge;

        private CharacterInfoWindowView owner;

        public EquipmentSlot Slot => slot;
        public EquipmentItemInstance Item { get; private set; }

        public void Initialize(CharacterInfoWindowView nextOwner)
        {
            owner = nextOwner;
            if (button != null) { button.onClick.RemoveListener(Select); button.onClick.AddListener(Select); }
        }
        private void Select() => owner?.SelectEquipmentSlot(this);
        public void OnSubmit(BaseEventData eventData) => Activate();
        private void Activate()
        {
            if (!isActiveAndEnabled || button != null && (!button.IsActive() || !button.IsInteractable())) return;
            owner?.TryActivateItem(Item);
        }
        private void OnDisable() => UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Select); }
        public void SetSelected(bool selected) { if (selectionBorder != null) selectionBorder.enabled = selected; }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.dragging) return;
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
                Activate();
            if (eventData.button == PointerEventData.InputButton.Right) owner?.ShowEquipmentMenu(this, eventData.position);
        }

        public void Bind(EquipmentItemInstance item)
        {
            if (!ReferenceEquals(Item, item))
                UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
            Item = item;
            ItemRarityPresentation.ApplyBorder(ref rarityBorder, transform as RectTransform, item);
            if (saleProtectionBadge != null) saleProtectionBadge.gameObject.SetActive(item?.IsSaleProtected == true);
            if (icon != null)
            {
                icon.sprite = ItemIconPresentation.GetIcon(item);
                icon.enabled = icon.sprite != null;
            }

            if (emptySlotIcon != null) emptySlotIcon.enabled = item == null;
            if (button != null) button.interactable = item != null;
            if (unavailableOverlay != null)
            {
                unavailableOverlay.color = UnavailableOverlayColor;
                unavailableOverlay.enabled = item != null && !owner.MeetsEquipmentRequirements(item);
            }
        }

        public void SetDropHighlight(bool enabled)
        {
            if (feedbackBorder != null) feedbackBorder.enabled = enabled;
        }

        public void OnDrop(PointerEventData eventData) => owner?.TryHandleDrop(this);

        public void OnBeginDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.BeginUnequipDrag(this, eventData); }

        public void OnDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.UpdateDrag(eventData); }

        public void OnEndDrag(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) owner?.EndDrag(); }
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(Item, owner?.Character, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
