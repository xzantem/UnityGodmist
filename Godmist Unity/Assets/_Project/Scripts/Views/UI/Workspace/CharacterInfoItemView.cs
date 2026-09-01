using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color UnavailableOverlayColor = new(0.45f, 0.06f, 0.06f, 0.48f);

        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private TextMeshProUGUI countText;
        private ItemInstance item;
        private CharacterInfoWindowView owner;

        public ItemInstance Item => item;
        public CharacterInfoWindowView Owner => owner;

        private void Awake() { if (button != null) button.onClick.AddListener(() => owner?.TryEquip(item)); }
        public void Bind(
            ItemInstance nextItem,
            int stackCount,
            CharacterInfoWindowView nextOwner)
        {
            item = nextItem; owner = nextOwner;
            if (icon != null)
            {
                icon.sprite = item?.Definition?.Icon;
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
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(item, eventData);
        public void OnDrag(PointerEventData eventData) => owner?.UpdateDrag(eventData);
        public void OnEndDrag(PointerEventData eventData)
        {
            owner?.TryDropToCampaignStorage(item, eventData);
            owner?.EndDrag();
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (item is LootBagItemInstance) owner?.TryOpenLootBag(item);
                else if (item is ConsumableItemInstance) owner?.TryUseConsumable(item);
                else CampaignStorageWindowView.Active?.TryDeposit(
                    owner,
                    item,
                    Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            }
        }
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowItem(item, owner?.Character, this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }

}
