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
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private static readonly Color UnavailableOverlayColor = new(0.45f, 0.06f, 0.06f, 0.48f);

        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image feedbackBorder;
        [SerializeField] private Image unavailableOverlay;

        private CharacterInfoWindowView owner;

        public EquipmentSlot Slot => slot;
        public EquipmentItemInstance Item { get; private set; }

        public void Initialize(CharacterInfoWindowView nextOwner)
        {
            owner = nextOwner;
            if (button != null) button.onClick.AddListener(() => owner?.TryUnequip(slot));
        }

        public void Bind(EquipmentItemInstance item)
        {
            if (!ReferenceEquals(Item, item))
                UIWorkspaceTooltipView.ActiveInstance?.InvalidateSource(this);
            Item = item;
            if (icon != null)
            {
                icon.sprite = item?.Definition?.Icon;
                icon.enabled = icon.sprite != null;
            }

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

        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginUnequipDrag(this, eventData);

        public void OnDrag(PointerEventData eventData) => owner?.UpdateDrag(eventData);

        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(Item, owner?.Character, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
