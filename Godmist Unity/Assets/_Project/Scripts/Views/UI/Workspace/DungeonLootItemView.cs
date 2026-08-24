using Godmist.Runtime.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class DungeonLootItemView : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMPro.TextMeshProUGUI label;

        private ItemInstance item;
        private DungeonLootWindowView owner;

        public ItemInstance Item => item;
        public DungeonLootWindowView Owner => owner;

        public void Bind(ItemInstance nextItem, DungeonLootWindowView nextOwner)
        {
            item = nextItem;
            owner = nextOwner;
            icon ??= transform.Find("Icon")?.GetComponent<Image>();
            label ??= transform.Find("Label")?.GetComponent<TMPro.TextMeshProUGUI>();

            if (icon != null)
            {
                icon.sprite = item?.Definition?.Icon;
                icon.preserveAspect = true;
                icon.enabled = icon.sprite != null;
            }

            if (label != null)
            {
                label.text = item?.Definition?.DisplayName ?? "Przedmiot";
            }
        }

        public void OnBeginDrag(PointerEventData eventData) =>
            owner?.BeginDrag(this, eventData);

        public void OnDrag(PointerEventData eventData) =>
            owner?.UpdateDrag(eventData);

        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.clickCount >= 2)
            {
                owner?.TryClaimItem(item?.InstanceId);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIWorkspaceTooltipView.ActiveInstance?.ShowItem(
                item,
                owner?.ResolveActiveCharacter(),
                this);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit();
    }
}
