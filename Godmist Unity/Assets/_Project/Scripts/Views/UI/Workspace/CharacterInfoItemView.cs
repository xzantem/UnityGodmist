using Godmist.Runtime.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unavailableOverlay;
        private ItemInstance item;
        private CharacterInfoWindowView owner;

        public ItemInstance Item => item;
        public CharacterInfoWindowView Owner => owner;

        private void Awake() { if (button != null) button.onClick.AddListener(() => owner?.TryEquip(item)); }
        public void Bind(ItemInstance nextItem, CharacterInfoWindowView nextOwner)
        {
            item = nextItem; owner = nextOwner;
            if (icon != null)
            {
                icon.sprite = item?.Definition?.Icon;
                icon.enabled = icon.sprite != null;
            }
            var unavailable = item is EquipmentItemInstance equipment
                && !owner.MeetsEquipmentRequirements(equipment);
            if (unavailableOverlay != null) unavailableOverlay.enabled = unavailable;
        }
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(item, eventData);
        public void OnDrag(PointerEventData eventData) => owner?.UpdateDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(item, owner?.Character, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit();
    }

}
