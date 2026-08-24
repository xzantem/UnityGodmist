using UnityEngine;
using UnityEngine.EventSystems;

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
            var sourceItemView = eventData.pointerDrag?.GetComponent<CharacterInfoItemView>();
            if (sourceItemView?.Owner != null && sourceItemView.Item != null)
            {
                owner?.TryReceiveInventoryTransfer(sourceItemView.Owner, sourceItemView.Item);
                return;
            }

            owner?.TryHandleInventoryDrop();
        }
    }
}
