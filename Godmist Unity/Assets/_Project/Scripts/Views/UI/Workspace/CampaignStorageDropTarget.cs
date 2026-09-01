using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CampaignStorageDropTarget : MonoBehaviour, IDropHandler
    {
        [SerializeField] private bool acceptsStashItems;
        private void Awake()
        {
            // This component belongs on Scroll View/Viewport. Do not create or recolor
            // a Graphic here; only make its existing viewport Image receive drops.
            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }
        public void OnDrop(PointerEventData eventData)
        {
            var item = eventData.pointerDrag?.GetComponentInParent<CampaignStorageItemView>();
            if (item != null) item.Owner?.TryDrop(item, acceptsStashItems);
            var characterItem = eventData.pointerDrag?.GetComponentInParent<CharacterInfoItemView>();
            if (acceptsStashItems && characterItem?.Owner != null && characterItem.Item != null)
            {
                GetComponentInParent<CampaignStorageWindowView>()?.TryDeposit(characterItem.Owner, characterItem.Item, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
                characterItem.Owner.EndDrag();
            }
        }
    }
}
