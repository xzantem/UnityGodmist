using Godmist.Runtime.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        private ItemInstance item;
        private CharacterInfoWindowView owner;

        private void Awake() { if (button != null) button.onClick.AddListener(() => owner?.TryEquip(item)); }
        public void Bind(ItemInstance nextItem, CharacterInfoWindowView nextOwner)
        {
            item = nextItem; owner = nextOwner;
            if (icon != null)
            {
                icon.sprite = item?.Definition?.Icon;
                icon.enabled = icon.sprite != null;
            }
        }
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(item, eventData);
        public void OnDrag(PointerEventData eventData) => owner?.UpdateDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
    }

}
