using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CampaignStorageItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private ItemRarityBorderGraphic rarityBorder;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI countText;
        private CampaignStorageWindowView owner;
        public CampaignStorageWindowView Owner => owner;
        public ItemInstance Item { get; private set; }
        public bool IsStashItem { get; private set; }
        public Sprite DisplayedIcon => icon != null ? icon.sprite : ItemIconPresentation.GetIcon(Item);

        public void Bind(CampaignStorageWindowView nextOwner, ItemInstance item, int count, bool isStashItem)
        {
            owner = nextOwner; Item = item; IsStashItem = isStashItem;
            ItemRarityPresentation.ApplyBorder(ref rarityBorder, transform as RectTransform, item);
            if (icon != null)
            {
                icon.sprite = ItemIconPresentation.GetIcon(item);
                icon.preserveAspect = true;
                icon.enabled = icon.sprite != null;
            }
            if (countText != null)
            {
                countText.gameObject.SetActive(item?.Definition?.IsStackable ?? false);
                countText.text = Mathf.Max(1, count).ToString();
            }
        }
        public void OnBeginDrag(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) owner?.BeginDrag(this, eventData); }
        public void OnDrag(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) owner?.UpdateDrag(eventData); }
        public void OnEndDrag(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) owner?.EndDrag(); }
        public void OnPointerClick(PointerEventData eventData) => owner?.HandleClick(this, eventData);
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(Item, owner?.SelectedCharacter, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
