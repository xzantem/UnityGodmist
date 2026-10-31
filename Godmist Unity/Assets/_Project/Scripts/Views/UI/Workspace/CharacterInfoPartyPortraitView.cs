using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoPartyPortraitView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI nameText;

        [SerializeField] private CombatStatusCollectionView statusCollection;
        [SerializeField] private Image groupLineTop, groupLineBottom, selectedMarker, insertBefore, insertAfter;
        [SerializeField] private CombatPartyMemberView memberView;
        [Header("Rail presentation")]
        [SerializeField] private Image healthFill;
        [SerializeField] private TextMeshProUGUI lifeStateText;
        [SerializeField] private RectTransform resourceRect;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
        private bool portraitHovered;
        private bool downedHovered;
        private bool resourceHovered;
        private string tooltipBody;
        private Godmist.Runtime.Combat.BattleUnit boundUnit;
        private CharacterInfoPartyRailView rail;
        private bool dragging;
        private CharacterProgressionBadgeView progressionBadge;
        private CharacterInstance experienceSource;
        private int pendingExperience, pendingLevels;
        public string CharacterId => character?.CharacterId;
        public Sprite PortraitSprite => portrait != null ? portrait.sprite : null;
        public RectTransform Rect => (RectTransform)transform;
        public void ConfigureRail(CharacterInfoPartyRailView owner, bool first, bool last)
        {
            rail = owner;
            if (groupLineTop != null) groupLineTop.enabled = false;
            if (groupLineBottom != null) groupLineBottom.enabled = false;
            MarkInsertion(false, false);
            RefreshProgressionBadge();
        }
        public void MarkSelected(bool selected) => RefreshPresentation(null, selected, false);
        public void RefreshPresentation(Godmist.Runtime.Combat.BattleUnit unit, bool selected, bool hasTurn)
        {
            // Keep the combat wrapper when this rail row is backed by a battle
            // unit. Rich status presentation needs its auras and redirects;
            // the CharacterInstance-only fallback is for exploration/city rows.
            boundUnit = unit;
            if (selectedMarker != null)
            {
                selectedMarker.enabled = selected;
                selectedMarker.color = unit != null ? new Color(.35f,.85f,1f) : new Color(1f,.81f,.4f);
            }
            if (memberView != null)
            {
                if (unit != null) memberView.Bind(unit, selected, hasTurn);
                else memberView.BindCharacter(character, selected);
            }
            var shownCharacter = unit?.Character ?? character;
            if (shownCharacter == null) return;
            RefreshProgressionBadge();
            if (nameText != null) nameText.color = hasTurn ? new Color(1f,.81f,.4f)
                : selected ? unit != null ? new Color(.35f,.85f,1f) : new Color(1f,.81f,.4f)
                : new Color(.91f, .875f, .78f);
            var state = shownCharacter.LifeState;
            if (portrait != null) portrait.color = state == CharacterLifeState.Dead
                ? new Color(.4f, .4f, .4f) : state == CharacterLifeState.Downed
                ? new Color(.65f, .5f, .5f) : Color.white;
            if (lifeStateText != null)
            {
                lifeStateText.text = state == CharacterLifeState.Downed ? "Powalony"
                    : state == CharacterLifeState.Dead ? "Martwy" : string.Empty;
                lifeStateText.gameObject.SetActive(state != CharacterLifeState.Alive
                    && !CombatDownedIndicatorView.ShouldShow(unit));
            }
            var maximum = shownCharacter.MaximumHealth;
            if (healthFill != null) healthFill.color = state == CharacterLifeState.Dead
                ? new Color(.4f, .4f, .4f) : (float)shownCharacter.CurrentHealth / maximum <= .25f
                ? new Color(1f, .4f, .23f) : new Color(.8f, .22f, .2f);
            if (portraitHovered || resourceHovered) ShowPortraitTooltip();
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
            HidePortraitTooltip();
            dragging = true; rail.BeginDrag(this, e);
        }
        public void OnDrag(PointerEventData e) { if (dragging) rail.Drag(e); }
        public void OnEndDrag(PointerEventData e) { if (dragging) rail.EndDrag(e); dragging = false; }
        private void OnEnable() => SubscribeToExperience();
        private void OnDisable()
        {
            HidePortraitTooltip(); if (dragging) rail?.CancelDrag(); dragging = false;
            if (experienceSource != null) experienceSource.ExperienceGained -= OnExperienceGained;
            experienceSource = null; pendingExperience = pendingLevels = 0;
        }
        private void SubscribeToExperience()
        {
            if (experienceSource != null) experienceSource.ExperienceGained -= OnExperienceGained;
            experienceSource = isActiveAndEnabled ? character : null;
            if (experienceSource != null) experienceSource.ExperienceGained += OnExperienceGained;
        }
        private void OnExperienceGained(int amount, int levels)
        { pendingExperience += amount; pendingLevels += levels; RefreshProgressionBadge(); }
        private void LateUpdate() => FlushProgressionFeedback();
        public void FlushProgressionFeedback()
        {
            if (pendingExperience <= 0) return;
            var amount = pendingExperience; var levels = pendingLevels;
            pendingExperience = pendingLevels = 0;
            rail?.ShowProgressionFeedback(character, amount, levels, Rect, nameText);
        }
        private void RefreshProgressionBadge()
        {
            if (progressionBadge == null && character?.AvailableSkillPoints > 0 && portrait != null)
                progressionBadge = CharacterProgressionBadgeView.Create(portrait.rectTransform, nameText,
                    selectedMarker?.sprite, () => rail?.OpenCharacterDevelopment(character));
            progressionBadge?.Bind(character?.AvailableSkillPoints ?? 0, !dragging && rail?.CanOpenDevelopment(character) == true);
        }
        public void OnPointerEnter(PointerEventData e) => UpdatePortraitHover(e);
        public void OnPointerMove(PointerEventData e) => UpdatePortraitHover(e);
        public void OnPointerExit(PointerEventData e)
        {
            portraitHovered = false;
            downedHovered = false;
            resourceHovered = false;
            tooltipView?.NotifySourceExit(this);
        }
        private void UpdatePortraitHover(PointerEventData e)
        {
            if (progressionBadge?.Contains(e) == true)
            {
                portraitHovered = downedHovered = resourceHovered = false;
                tooltipView?.NotifySourceExit(this); return;
            }
            portraitHovered = !dragging && !e.dragging && portrait != null
                && RectTransformUtility.RectangleContainsScreenPoint(portrait.rectTransform, e.position, e.enterEventCamera);
            resourceHovered = !dragging && !e.dragging && resourceRect != null && resourceRect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(resourceRect, e.position, e.enterEventCamera);
            downedHovered = portraitHovered && memberView != null
                && memberView.IsPointerOverDownedCountdown(e.position,e.enterEventCamera);
            if (portraitHovered || resourceHovered) ShowPortraitTooltip();
            else tooltipView?.NotifySourceExit(this);
        }
        private void ShowPortraitTooltip()
        {
            if (character == null) return;
            tooltipView ??= UIWorkspaceTooltipView.ActiveInstance;
            if (tooltipView == null || tooltipView.IsSkillTooltipSuppressed) return;
            if (downedHovered && CombatDownedIndicatorView.ShouldShow(boundUnit))
            {
                var body = CombatDownedIndicatorView.TooltipBody(boundUnit.DownedTurnsRemaining);
                if (tooltipBody != body)
                {
                    if (tooltipView.HasPinnedBranch) return;
                    tooltipView.InvalidateSource(this);
                }
                tooltipBody = body;
                tooltipView.ShowCompactText("Powalony",body,this);
                return;
            }
            // The rail and world/timeline hovers share the same rich unit card.
            // Keep the source stable while the pointer moves between the portrait
            // and resource strip so the card can refresh live values in place.
            const string richContentKey = "rich-unit";
            if (tooltipBody != richContentKey)
            {
                if (tooltipView.HasPinnedBranch) return;
                tooltipView.InvalidateSource(this);
            }
            tooltipBody = richContentKey;
            if (boundUnit?.Character == character)
                tooltipView.ShowUnit(boundUnit, this, true);
            else
                tooltipView.ShowUnit(character, this, true);
        }
        private void HidePortraitTooltip()
        {
            portraitHovered = false;
            downedHovered = false;
            resourceHovered = false;
            tooltipBody = null;
            tooltipView?.InvalidateSource(this);
        }
        internal static string BuildPortraitTooltipBody(CharacterInstance value)
        {
            var maximum = value.MaximumHealth;
            var body = $"Zdrowie: {CombatHealthPresentation.Format(value)}";
            var resource = value.ResourcePool;
            if (resource != null && resource.MaxValue > 0)
                body += $"\n{CombatResourceColors.ResourceName(resource.ResourceType)}: {resource.CurrentValue}/{resource.MaxValue}";
            if (value.LifeState == CharacterLifeState.Downed) body += "\nPowalony";
            else if (value.LifeState == CharacterLifeState.Dead) body += "\nMartwy";
            else if ((float)value.CurrentHealth / maximum <= .25f) body += "\nNiskie zdrowie";
            return body;
        }
        private CharacterInstance character;
        private Action<CharacterInstance> onClick;

        public void Bind(CharacterInstance nextCharacter, Action<CharacterInstance> nextOnClick)
        {
            if (character != nextCharacter) { HidePortraitTooltip(); pendingExperience = pendingLevels = 0; }
            character = nextCharacter;
            SubscribeToExperience();
            RefreshProgressionBadge();
            boundUnit = null;
            statusCollection?.BindCharacter(character);
            onClick = nextOnClick;
            if (portrait != null)
            {
                portrait.sprite = character?.Definition?.UiPortraitSprite;
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
