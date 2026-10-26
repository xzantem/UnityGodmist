using UnityEngine;
using UnityEngine.EventSystems;
using Godmist.Views.Dungeons;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Accepts an equipped-item drag as an unequip request for its owning window.
    /// Inventory-to-inventory transfer is intentionally handled by a later card.
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

            var storageItemView = eventData.pointerDrag?.GetComponentInParent<CampaignStorageItemView>();
            if (storageItemView?.Owner != null && storageItemView.Item != null)
            {
                if (storageItemView.IsStashItem) storageItemView.Owner.TryWithdraw(
                    owner.Character,
                    storageItemView.Item,
                    Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
                storageItemView.Owner.EndDrag();
                return;
            }

            owner?.TryHandleInventoryDrop();
        }
    }
}
