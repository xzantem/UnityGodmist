using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CampaignStorageItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI countText;
        private CampaignStorageWindowView owner;
        public CampaignStorageWindowView Owner => owner;
        public ItemInstance Item { get; private set; }
        public bool IsStashItem { get; private set; }
        public Sprite DisplayedIcon => icon != null ? icon.sprite : Item?.Definition?.Icon;

        public void Bind(CampaignStorageWindowView nextOwner, ItemInstance item, int count, bool isStashItem)
        {
            owner = nextOwner; Item = item; IsStashItem = isStashItem;
            if (icon != null) { icon.sprite = item?.Definition?.Icon; icon.enabled = icon.sprite != null; }
            if (countText != null) { countText.gameObject.SetActive(item?.Definition?.IsStackable ?? false); countText.text = count.ToString(); }
        }
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(this, eventData);
        public void OnDrag(PointerEventData eventData) => owner?.UpdateDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) owner?.TryContextTransfer(this);
            else if (eventData.button == PointerEventData.InputButton.Left && IsStashItem && Item is EquipmentItemInstance) owner?.TryEquipFromStash(this);
        }
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(Item, null, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
