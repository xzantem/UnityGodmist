using System;
using Godmist.Runtime.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoPartyPortraitView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI nameText;

        [SerializeField] private CombatStatusCollectionView statusCollection;
        [SerializeField] private Image groupLineTop, groupLineBottom, selectedMarker, insertBefore, insertAfter;
        [SerializeField] private CombatPartyMemberView memberView;
        private CharacterInfoPartyRailView rail;
        private bool dragging;
        public string CharacterId => character?.CharacterId;
        public Sprite PortraitSprite => portrait != null ? portrait.sprite : null;
        public RectTransform Rect => (RectTransform)transform;
        public void ConfigureRail(CharacterInfoPartyRailView owner, bool first, bool last)
        {
            rail = owner;
            if (groupLineTop != null) groupLineTop.enabled = !first;
            if (groupLineBottom != null) groupLineBottom.enabled = !last;
            MarkInsertion(false, false);
        }
        public void MarkSelected(bool selected) => RefreshPresentation(null, selected, false);
        public void RefreshPresentation(Godmist.Runtime.Combat.BattleUnit unit, bool selected, bool hasTurn)
        {
            if (selectedMarker != null) selectedMarker.enabled = selected;
            if (memberView == null) return;
            if (unit != null) memberView.Bind(unit, selected, hasTurn);
            else memberView.BindCharacter(character, selected);
        }
        public void MarkInsertion(bool before, bool after)
        {
            if (insertBefore != null) insertBefore.enabled = before;
            if (insertAfter != null) insertAfter.enabled = after;
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (rail == null || dragging || e.dragging) return;
            if (e.button == PointerEventData.InputButton.Right) rail.ShowMenu(this, e.position);
            else if (e.button == PointerEventData.InputButton.Left && e.clickCount == 2) rail.Center(CharacterId);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || rail == null) return;
            dragging = true; rail.BeginDrag(this, e);
        }
        public void OnDrag(PointerEventData e) { if (dragging) rail.Drag(e); }
        public void OnEndDrag(PointerEventData e) { if (dragging) rail.EndDrag(e); dragging = false; }
        private void OnDisable() { if (dragging) rail?.CancelDrag(); dragging = false; }
        private CharacterInstance character;
        private Action<CharacterInstance> onClick;

        public void Bind(CharacterInstance nextCharacter, Action<CharacterInstance> nextOnClick)
        {
            character = nextCharacter;
            statusCollection?.BindCharacter(character);
            onClick = nextOnClick;
            if (portrait != null)
            {
                portrait.sprite = character?.Definition?.VisualSprite;
                portrait.enabled = portrait.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = character?.Definition?.DisplayName ?? character?.CharacterId ?? "-";
            }

            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.interactable = character != null;
            button.onClick.AddListener(() => { if (!dragging) onClick?.Invoke(character); });
        }
    }
}
