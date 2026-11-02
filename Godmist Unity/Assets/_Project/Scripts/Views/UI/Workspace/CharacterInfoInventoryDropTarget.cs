using UnityEngine;
using UnityEngine.EventSystems;
using Godmist.Views.Dungeons;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Accepts an equipped-item drag as an unequip request for its owning window.
    /// Cross-character drops share the portrait/menu transfer and quantity contract.
    /// </summary>
    public sealed class CharacterInfoInventoryDropTarget : MonoBehaviour, IDropHandler
    {
        private CharacterInfoWindowView owner;

        private void Awake()
        {
            owner = GetComponentInParent<CharacterInfoWindowView>();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData?.button != PointerEventData.InputButton.Left || eventData.pointerDrag == null) return;
            var lootItemView = eventData.pointerDrag?.GetComponent<DungeonLootItemView>();
            if (lootItemView?.Owner != null && lootItemView.Item != null)
            {
                owner?.TryReceiveLootItem(lootItemView.Owner, lootItemView.Item);
                return;
            }

            var sourceItemView = eventData.pointerDrag?.GetComponent<CharacterInfoItemView>();
            if (sourceItemView?.Owner != null && sourceItemView.Item != null)
            {
                owner?.TryReceiveInventoryTransfer(sourceItemView.Owner, sourceItemView.Item);
                return;
            }

            var sourceEquipment = eventData.pointerDrag?.GetComponent<CharacterInfoEquipmentSlotView>();
            if (sourceEquipment?.Owner != null && sourceEquipment.Owner != owner)
            {
                owner?.TryReceiveInventoryTransfer(sourceEquipment.Owner, sourceEquipment.Item);
                return;
            }

            var storageItemView = eventData.pointerDrag?.GetComponentInParent<CampaignStorageItemView>();
            if (storageItemView?.Owner != null && storageItemView.Item != null)
            {
                if (storageItemView.IsStashItem) storageItemView.Owner.TryWithdraw(
                    owner.Character,
                    storageItemView.Item,
                    (Input.GetKey(KeyCode.LeftControl) || Godmist.Views.UI.ControllerInput.Alternate) || Input.GetKey(KeyCode.RightControl));
                storageItemView.Owner.EndDrag();
                return;
            }

            owner?.TryHandleInventoryDrop();
        }
    }
}
