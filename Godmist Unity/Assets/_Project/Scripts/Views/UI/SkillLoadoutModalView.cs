using System;
using System.Collections;
using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class SkillLoadoutModalView : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropCloseButton;

        [Header("Party rail")]
        [SerializeField] private Transform characterSelectorRoot;
        [SerializeField] private PartyMemberButtonView characterSelectorTemplate;
        [SerializeField] private TextMeshProUGUI inspectedNameText;
        [SerializeField] private TextMeshProUGUI inspectedClassLevelText;

        [Header("Loadout workspace")]
        [SerializeField] private TextMeshProUGUI capacityText;
        [SerializeField] private Button basicSlotCard;
        [SerializeField] private Transform activeSlotsRoot;
        [SerializeField] private Button activeSlotTemplate;
        [SerializeField] private ScrollRect activeSlotsScrollRect;
        [SerializeField] private Transform availableSkillsRoot;
        [SerializeField] private Button availableSkillTemplate;
        [SerializeField] private ScrollRect availableScrollRect;
        [SerializeField] private SkillLoadoutDragSurface availableViewportDropSurface;
        [SerializeField] private TextMeshProUGUI emptyAvailableState;
        [SerializeField] private TextMeshProUGUI availableCountText;

        [Header("Tooltip and drag")]
        [SerializeField] private CombatTooltipView tooltipView;
        [SerializeField] private RectTransform dragLayerRect;
        [SerializeField] private GameObject dragGhostTemplate;

        private readonly SkillLoadoutSystem loadoutSystem = new();
        private readonly List<SkillLoadoutCardView> activeCards = new();
        private readonly List<SkillLoadoutCardView> knownCards = new();

        private GameSession session;
        private CharacterInstance character;
        private string draggedSkillId = string.Empty;
        private bool draggedSkillIsActive;
        private bool dragHasBeenHandled;
        private SkillLoadoutCardView draggedCard;
        private SkillLoadoutDragSurface previewSurface;
        private SkillLoadoutCardView previewCard;
        private GameObject dragGhost;
        private RectTransform dragGhostRect;
        private Image dragGhostImage;
        private TextMeshProUGUI dragGhostFallback;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            backdropCloseButton?.onClick.AddListener(Close);

            activeSlotTemplate?.gameObject.SetActive(false);
            availableSkillTemplate?.gameObject.SetActive(false);
            dragGhostTemplate?.SetActive(false);
            panelRoot?.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            UpdateDragGhost(Input.mousePosition);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!string.IsNullOrWhiteSpace(draggedSkillId))
                {
                    ResetDrag();
                }
                else
                {
                    Close();
                }
                return;
            }

            if (session == null || session.ActiveCombat != null)
            {
                Close();
            }
        }

        private void LateUpdate()
        {
            if (IsOpen)
            {
                RefreshAvailablePanelPreview();
            }
        }

        private void OnDestroy()
        {
            ResetDrag();
            closeButton?.onClick.RemoveListener(Close);
            backdropCloseButton?.onClick.RemoveListener(Close);
        }

        public void Open(GameSession gameSession, string characterId)
        {
            if (gameSession == null
                || gameSession.ActiveCombat != null
                || !IsActivePartyMember(gameSession, characterId)
                || !gameSession.CharactersById.TryGetValue(
                    characterId,
                    out var selectedCharacter)
                || selectedCharacter == null)
            {
                Close();
                return;
            }

            session = gameSession;
            character = selectedCharacter;
            panelRoot?.SetActive(true);
            ResetScrollPositions();
            Refresh();
        }

        public void Close()
        {
            ResetDrag();
            tooltipView?.Hide();
            panelRoot?.SetActive(false);
            session = null;
            character = null;
        }

        private void Refresh()
        {
            if (session == null || character == null)
            {
                return;
            }

            if (session.ActiveCombat != null)
            {
                Close();
                return;
            }

            if (inspectedNameText != null)
            {
                inspectedNameText.text = character.Definition?.DisplayName
                    ?? character.CharacterId;
            }

            if (inspectedClassLevelText != null)
            {
                inspectedClassLevelText.text = character.ClassData != null
                    ? $"{character.ClassData.ClassName} | poziom {character.Level}"
                    : "-";
            }

            if (capacityText != null)
            {
                var loadout = character.ActiveSkillLoadout;
                capacityText.text = loadout == null
                    ? "—"
                    : $"{loadout.ActiveSkillIds.Count} / {loadout.Capacity}";
            }

            RebuildCharacterSelector();
            RebuildActiveSlots();
            RebuildAvailableSkills();
            ConfigureAvailableDropSurface();
        }

        private void RebuildCharacterSelector()
        {
            Clear(characterSelectorRoot, characterSelectorTemplate?.transform);
            if (characterSelectorRoot == null
                || characterSelectorTemplate == null
                || session?.PlayerParty == null)
            {
                return;
            }

            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(
                        characterId,
                        out var member)
                    || member == null)
                {
                    continue;
                }

                var button = Instantiate(
                    characterSelectorTemplate,
                    characterSelectorRoot);
                button.gameObject.SetActive(true);
                button.Bind(
                    member,
                    string.Equals(
                        member.CharacterId,
                        character.CharacterId,
                        StringComparison.Ordinal),
                    SelectCharacter);
            }
        }

        private void SelectCharacter(CharacterInstance selectedCharacter)
        {
            if (session == null
                || selectedCharacter == null
                || !IsActivePartyMember(session, selectedCharacter.CharacterId))
            {
                return;
            }

            character = selectedCharacter;
            ResetScrollPositions();
            Refresh();
        }

        private void RebuildActiveSlots()
        {
            activeCards.Clear();
            Clear(activeSlotsRoot, activeSlotTemplate?.transform);
            if (character?.ActiveSkillLoadout == null)
            {
                return;
            }

            var loadout = character.ActiveSkillLoadout;
            var activeSkillIds = loadout.ActiveSkillIds;
            ConfigureBasicSlot(loadout, activeSkillIds);

            if (activeSlotsRoot == null || activeSlotTemplate == null)
            {
                return;
            }

            for (var slotIndex = 1; slotIndex < loadout.Capacity; slotIndex++)
            {
                var button = Instantiate(activeSlotTemplate, activeSlotsRoot);
                button.gameObject.SetActive(true);

                var hasSkill = slotIndex < activeSkillIds.Count;
                var skillId = hasSkill ? activeSkillIds[slotIndex] : string.Empty;
                var skill = hasSkill ? FindKnownSkill(skillId) : null;
                var card = EnsureCard(button);
                card.Configure(
                    skill,
                    isActive: hasSkill,
                    isBasic: false,
                    isEmpty: !hasSkill,
                    tooltipView,
                    hasSkill ? () => HandleActiveCardClick(skillId, false, card) : null);
                activeCards.Add(card);

                ConfigureSurface(
                    button.gameObject,
                    skillId,
                    isSource: hasSkill,
                    sourceIsActive: hasSkill,
                    isActiveSlot: true,
                    activeIndex: slotIndex,
                    isAvailableArea: false);
            }
        }

        private void ConfigureBasicSlot(
            ActiveSkillLoadout loadout,
            IReadOnlyList<string> activeSkillIds)
        {
            if (basicSlotCard == null)
            {
                return;
            }

            var skillId = activeSkillIds != null && activeSkillIds.Count > 0
                ? activeSkillIds[0]
                : loadout.BasicAttackSkillId;
            var skill = FindKnownSkill(skillId);
            var card = EnsureCard(basicSlotCard);
            card.Configure(
                skill,
                isActive: skill != null,
                isBasic: true,
                isEmpty: skill == null,
                tooltipView,
                onClick: null);
            basicSlotCard.interactable = false;
            // Basic attack is intentionally not a drag surface. It is locked and non-interactable.
        }

        private void RebuildAvailableSkills()
        {
            knownCards.Clear();
            Clear(availableSkillsRoot, availableSkillTemplate?.transform);
            if (availableSkillsRoot == null
                || availableSkillTemplate == null
                || character.ActiveSkillLoadout == null)
            {
                return;
            }

            var loadout = character.ActiveSkillLoadout;
            var activeSkillIds = new HashSet<string>(
                loadout.ActiveSkillIds,
                StringComparer.Ordinal);
            var knownSkillIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in character.Skills ?? new List<SkillData>())
            {
                if (skill == null
                    || string.IsNullOrWhiteSpace(skill.SkillId)
                    || !knownSkillIds.Add(skill.SkillId))
                {
                    continue;
                }

                if (activeSkillIds.Contains(skill.SkillId))
                {
                    continue;
                }

                var isBasic = string.Equals(
                    skill.SkillId,
                    loadout.BasicAttackSkillId,
                    StringComparison.Ordinal);
                var button = Instantiate(availableSkillTemplate, availableSkillsRoot);
                button.gameObject.SetActive(true);
                var card = EnsureCard(button);
                card.Configure(
                    skill,
                    isActive: false,
                    isBasic: false,
                    isEmpty: false,
                    tooltipView,
                    () => HandleKnownCardClick(skill.SkillId, false, false, card));
                knownCards.Add(card);

                ConfigureSurface(
                    button.gameObject,
                    skill.SkillId,
                    isSource: true,
                    sourceIsActive: false,
                    isActiveSlot: false,
                    activeIndex: -1,
                    isAvailableArea: true);
            }

            if (emptyAvailableState != null)
            {
                emptyAvailableState.gameObject.SetActive(knownCards.Count == 0);
            }

            if (availableCountText != null)
            {
                availableCountText.text = knownCards.Count.ToString();
            }
        }

        private void ConfigureAvailableDropSurface()
        {
            if (availableSkillsRoot != null)
            {
                ConfigureSurface(
                    availableSkillsRoot.gameObject,
                    string.Empty,
                    isSource: false,
                    sourceIsActive: false,
                    isActiveSlot: false,
                    activeIndex: -1,
                    isAvailableArea: true);
            }

            if (availableViewportDropSurface != null)
            {
                availableViewportDropSurface.Configure(
                    this,
                    string.Empty,
                    nextIsSource: false,
                    nextSourceIsActive: false,
                    nextIsActiveSlot: false,
                    nextActiveIndex: -1,
                    nextIsAvailableArea: true);
            }
        }

        private void ConfigureSurface(
            GameObject target,
            string skillId,
            bool isSource,
            bool sourceIsActive,
            bool isActiveSlot,
            int activeIndex,
            bool isAvailableArea)
        {
            if (target == null)
            {
                return;
            }

            var surface = target.GetComponent<SkillLoadoutDragSurface>();
            if (surface == null)
            {
                surface = target.AddComponent<SkillLoadoutDragSurface>();
            }

            surface.Configure(
                this,
                skillId,
                isSource,
                sourceIsActive,
                isActiveSlot,
                activeIndex,
                isAvailableArea);
        }

        private void HandleActiveCardClick(
            string skillId,
            bool isBasic,
            SkillLoadoutCardView card)
        {
            if (isBasic
                || session == null
                || character == null
                || session.ActiveCombat != null)
            {
                card?.FlashInvalid();
                return;
            }

            if (loadoutSystem.TryRemoveSkill(
                    session,
                    character.CharacterId,
                    skillId))
            {
                Refresh();
            }
            else
            {
                card?.FlashInvalid();
            }
        }

        private void HandleKnownCardClick(
            string skillId,
            bool isActive,
            bool isBasic,
            SkillLoadoutCardView card)
        {
            if (session == null
                || character == null
                || session.ActiveCombat != null
                || isBasic)
            {
                card?.FlashInvalid();
                return;
            }

            var success = isActive
                ? loadoutSystem.TryRemoveSkill(
                    session,
                    character.CharacterId,
                    skillId)
                : loadoutSystem.TryAddSkill(
                    session,
                    character.CharacterId,
                    skillId);
            if (success)
            {
                Refresh();
            }
            else
            {
                card?.FlashInvalid();
            }
        }

        internal void BeginDrag(
            string skillId,
            bool isActive,
            SkillLoadoutCardView sourceCard)
        {
            if (session == null
                || character == null
                || session.ActiveCombat != null
                || string.IsNullOrWhiteSpace(skillId))
            {
                return;
            }

            draggedSkillId = skillId;
            draggedSkillIsActive = isActive;
            draggedCard = sourceCard;
            dragHasBeenHandled = false;
            tooltipView?.BeginDragSuppression();
            draggedCard?.SetDragSource(true);
            CreateDragGhost();
            UpdateDragGhost(Input.mousePosition);
        }

        internal void UpdateDragGhost(Vector2 screenPosition)
        {
            if (dragGhostRect == null)
            {
                return;
            }

            var canvas = dragGhostRect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (canvas == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    screenPosition,
                    camera,
                    out var localPosition))
            {
                return;
            }

            dragGhostRect.anchoredPosition = localPosition;
        }

        private void CreateDragGhost()
        {
            DestroyDragGhost();
            var canvas = dragLayerRect?.GetComponentInParent<Canvas>()
                ?? GetComponentInParent<Canvas>()?.rootCanvas;
            var sprite = draggedCard?.IconSprite;
            if (canvas == null)
            {
                return;
            }

            dragGhost = dragGhostTemplate != null
                ? Instantiate(dragGhostTemplate, dragLayerRect ?? canvas.transform)
                : new GameObject("SkillDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            dragGhost.name = "SkillDragGhost";
            dragGhost.SetActive(true);
            dragGhost.transform.SetParent(dragLayerRect ?? canvas.transform, false);
            dragGhost.transform.SetAsLastSibling();
            dragGhostRect = dragGhost.GetComponent<RectTransform>();
            if (dragGhostRect == null)
            {
                dragGhostRect = dragGhost.AddComponent<RectTransform>();
            }
            dragGhostRect.sizeDelta = new Vector2(96f, 96f);
            dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
            dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhostRect.pivot = new Vector2(0.5f, 0.5f);
            var group = dragGhost.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = dragGhost.AddComponent<CanvasGroup>();
            }
            group.alpha = 0.92f;
            group.blocksRaycasts = false;
            var background = dragGhost.GetComponent<Image>();
            if (background == null)
            {
                background = dragGhost.AddComponent<Image>();
            }
            background.sprite = draggedCard?.BackgroundSprite;
            background.type = background.sprite != null
                ? Image.Type.Sliced
                : Image.Type.Simple;
            background.color = new Color32(0x1B, 0x27, 0x33, 0xF0);
            background.preserveAspect = false;
            background.raycastTarget = false;

            var borderObject = new GameObject(
                "Border",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            borderObject.transform.SetParent(dragGhost.transform, false);
            var borderRect = borderObject.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var border = borderObject.GetComponent<Image>();
            border.sprite = draggedCard?.BorderSprite;
            border.type = border.sprite != null
                ? Image.Type.Sliced
                : Image.Type.Simple;
            border.fillCenter = false;
            border.color = new Color32(0x40, 0x53, 0x68, 0xFF);
            border.raycastTarget = false;

            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(dragGhost.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(76f, 76f);
            dragGhostImage = iconObject.GetComponent<Image>();
            dragGhostFallback = dragGhost.GetComponentInChildren<TextMeshProUGUI>(true);
            if (dragGhostImage != null)
            {
                dragGhostImage.sprite = sprite;
                dragGhostImage.enabled = sprite != null;
                dragGhostImage.preserveAspect = true;
                dragGhostImage.raycastTarget = false;
            }
            if (dragGhostFallback != null)
            {
                dragGhostFallback.text = "?";
                dragGhostFallback.gameObject.SetActive(sprite == null);
                dragGhostFallback.raycastTarget = false;
            }
        }

        private void DestroyDragGhost()
        {
            if (dragGhost != null)
            {
                Destroy(dragGhost);
            }

            dragGhost = null;
            dragGhostRect = null;
            dragGhostImage = null;
            dragGhostFallback = null;
        }

        internal void DropOnActiveSlot(
            int targetIndex,
            SkillLoadoutCardView targetCard)
        {
            if (dragHasBeenHandled
                || session == null
                || character == null
                || session.ActiveCombat != null
                || string.IsNullOrWhiteSpace(draggedSkillId))
            {
                return;
            }

            dragHasBeenHandled = true;
            var success = false;
            if (draggedSkillIsActive)
            {
                var activeSkillCount = character.ActiveSkillLoadout?.ActiveSkillIds.Count ?? 0;
                var reorderIndex = Mathf.Min(targetIndex, activeSkillCount - 1);
                success = reorderIndex >= 0
                    && loadoutSystem.TryMoveSkill(
                        session,
                        character.CharacterId,
                        draggedSkillId,
                        reorderIndex);
            }
            else if (character.ActiveSkillLoadout != null
                && targetIndex >= 0
                && targetIndex < character.ActiveSkillLoadout.ActiveSkillIds.Count)
            {
                var activeSkillIds = character.ActiveSkillLoadout.ActiveSkillIds;
                success = loadoutSystem.TryReplaceSkill(
                    session,
                    character.CharacterId,
                    activeSkillIds[targetIndex],
                    draggedSkillId);
            }
            else if (character.ActiveSkillLoadout != null
                && targetIndex >= character.ActiveSkillLoadout.ActiveSkillIds.Count
                && loadoutSystem.TryAddSkill(
                    session,
                    character.CharacterId,
                    draggedSkillId))
            {
                var activeCount = character.ActiveSkillLoadout.ActiveSkillIds.Count;
                success = loadoutSystem.TryMoveSkill(
                    session,
                    character.CharacterId,
                    draggedSkillId,
                    Mathf.Min(targetIndex, activeCount - 1));
            }

            if (success)
            {
                ResetDrag();
                Refresh();
            }
            else
            {
                targetCard?.FlashInvalid();
                draggedCard?.FlashInvalid();
            }
        }

        internal void DropOnAvailableArea(SkillLoadoutCardView targetCard)
        {
            if (dragHasBeenHandled
                || session == null
                || character == null
                || session.ActiveCombat != null
                || string.IsNullOrWhiteSpace(draggedSkillId))
            {
                return;
            }

            dragHasBeenHandled = true;
            var success = draggedSkillIsActive
                && !string.Equals(
                    draggedSkillId,
                    character.ActiveSkillLoadout?.BasicAttackSkillId,
                    StringComparison.Ordinal)
                && loadoutSystem.TryRemoveSkill(
                    session,
                    character.CharacterId,
                    draggedSkillId);
            if (success)
            {
                ResetDrag();
                Refresh();
            }
            else
            {
                targetCard?.FlashInvalid();
                draggedCard?.FlashInvalid();
            }
        }

        internal void SetDropPreview(
            SkillLoadoutDragSurface surface,
            bool isEntering)
        {
            if (string.IsNullOrWhiteSpace(draggedSkillId))
            {
                return;
            }

            if (!isEntering)
            {
                if (previewSurface == surface)
                {
                    previewCard?.SetDropPreview(false, false);
                    previewSurface?.SetSurfacePreview(false, false);
                    var remainsOverAvailablePanel = surface != null
                        && surface.IsAvailableArea
                        && IsPointerOverAvailablePanel();
                    SetAvailablePanelPreview(
                        remainsOverAvailablePanel,
                        remainsOverAvailablePanel && CanDropOnAvailableArea());
                    previewSurface = null;
                    previewCard = null;
                }

                return;
            }

            previewCard?.SetDropPreview(false, false);
            previewSurface?.SetSurfacePreview(false, false);
            SetAvailablePanelPreview(false, false);
            previewSurface = surface;
            previewCard = surface?.GetComponent<SkillLoadoutCardView>();
            if (previewCard == null || string.IsNullOrWhiteSpace(draggedSkillId))
            {
                if (surface != null)
                {
                    surface.SetSurfacePreview(
                        true,
                        surface.IsAvailableArea
                            ? CanDropOnAvailableArea()
                            : CanDropOnActiveSlot(surface.ActiveIndex));
                }
                return;
            }

            var valid = surface.IsActiveSlot
                ? CanDropOnActiveSlot(surface.ActiveIndex)
                : surface.IsAvailableArea && CanDropOnAvailableArea();
            previewCard.SetDropPreview(true, valid);
            if (surface.IsAvailableArea)
            {
                SetAvailablePanelPreview(true, valid);
            }
        }

        internal void EndDrag()
        {
            ResetDrag();
        }

        private bool CanDropOnActiveSlot(int targetIndex)
        {
            var loadout = character?.ActiveSkillLoadout;
            if (loadout == null || targetIndex < 0)
            {
                return false;
            }

            if (draggedSkillIsActive)
            {
                return !string.Equals(
                        draggedSkillId,
                        loadout.BasicAttackSkillId,
                        StringComparison.Ordinal)
                    && targetIndex < loadout.ActiveSkillIds.Count
                    && !string.Equals(
                        loadout.ActiveSkillIds[targetIndex],
                        loadout.BasicAttackSkillId,
                        StringComparison.Ordinal);
            }

            if (targetIndex >= loadout.ActiveSkillIds.Count)
            {
                return loadout.ActiveSkillIds.Count < loadout.Capacity;
            }

            return !string.Equals(
                    loadout.ActiveSkillIds[targetIndex],
                    loadout.BasicAttackSkillId,
                    StringComparison.Ordinal)
                && !ContainsSkillId(loadout.ActiveSkillIds, draggedSkillId);
        }

        private static bool ContainsSkillId(IReadOnlyList<string> skillIds, string skillId)
        {
            if (skillIds == null || string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            foreach (var value in skillIds)
            {
                if (string.Equals(value, skillId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanDropOnAvailableArea()
        {
            return draggedSkillIsActive
                && !string.Equals(
                    draggedSkillId,
                    character?.ActiveSkillLoadout?.BasicAttackSkillId,
                    StringComparison.Ordinal);
        }

        private void ResetDrag()
        {
            draggedCard?.SetDragSource(false);
            DestroyDragGhost();
            previewCard?.SetDropPreview(false, false);
            previewSurface?.SetSurfacePreview(false, false);
            SetAvailablePanelPreview(false, false);
            draggedSkillId = string.Empty;
            draggedSkillIsActive = false;
            dragHasBeenHandled = false;
            tooltipView?.EndDragSuppression();
            draggedCard = null;
            previewSurface = null;
            previewCard = null;
        }

        private void SetAvailablePanelPreview(bool visible, bool valid)
        {
            if (availableViewportDropSurface != null)
            {
                availableViewportDropSurface.SetSurfacePreview(visible, valid);
            }
        }

        private bool IsPointerOverAvailablePanel()
        {
            if (availableViewportDropSurface == null
                || availableViewportDropSurface.transform is not RectTransform rect)
            {
                return false;
            }

            var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                Input.mousePosition,
                camera);
        }

        private void RefreshAvailablePanelPreview()
        {
            if (string.IsNullOrWhiteSpace(draggedSkillId))
            {
                return;
            }

            var isOverAvailablePanel = IsPointerOverAvailablePanel();
            SetAvailablePanelPreview(
                isOverAvailablePanel,
                isOverAvailablePanel && CanDropOnAvailableArea());
        }

        private void ResetScrollPositions()
        {
            if (activeSlotsScrollRect != null)
            {
                activeSlotsScrollRect.horizontalNormalizedPosition = 0f;
            }

            if (availableScrollRect != null)
            {
                availableScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private SkillLoadoutCardView EnsureCard(Button button)
        {
            var card = button.GetComponent<SkillLoadoutCardView>();
            if (card == null)
            {
                card = button.gameObject.AddComponent<SkillLoadoutCardView>();
            }

            return card;
        }

        private SkillData FindKnownSkill(string skillId)
        {
            foreach (var skill in character?.Skills ?? new List<SkillData>())
            {
                if (skill != null
                    && string.Equals(
                        skill.SkillId,
                        skillId,
                        StringComparison.Ordinal))
                {
                    return skill;
                }
            }

            return null;
        }

        private static bool IsActivePartyMember(GameSession gameSession, string characterId)
        {
            if (gameSession?.PlayerParty == null
                || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            foreach (var activeMemberId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (string.Equals(
                        activeMemberId,
                        characterId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Clear(Transform root, Transform template)
        {
            if (root == null)
            {
                return;
            }

            for (var index = root.childCount - 1; index >= 0; index--)
            {
                if (root.GetChild(index) != template)
                {
                    var child = root.GetChild(index).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }
        }
    }

    public sealed class SkillLoadoutDragSurface : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IDropHandler,
        IEndDragHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private SkillLoadoutModalView owner;
        private string skillId = string.Empty;
        private bool isSource;
        private bool sourceIsActive;
        private bool isActiveSlot;
        private int activeIndex;
        private bool isAvailableArea;
        [SerializeField] private Image[] previewBorderStrips;

        internal bool IsActiveSlot => isActiveSlot;
        internal int ActiveIndex => activeIndex;
        internal bool IsAvailableArea => isAvailableArea;

        internal void Configure(
            SkillLoadoutModalView nextOwner,
            string nextSkillId,
            bool nextIsSource,
            bool nextSourceIsActive,
            bool nextIsActiveSlot,
            int nextActiveIndex,
            bool nextIsAvailableArea)
        {
            owner = nextOwner;
            skillId = nextSkillId ?? string.Empty;
            isSource = nextIsSource;
            sourceIsActive = nextSourceIsActive;
            isActiveSlot = nextIsActiveSlot;
            activeIndex = nextActiveIndex;
            isAvailableArea = nextIsAvailableArea;
        }

        internal void SetSurfacePreview(bool visible, bool valid)
        {
            if (previewBorderStrips == null || previewBorderStrips.Length == 0)
            {
                return;
            }

            if (!visible)
            {
                foreach (var strip in previewBorderStrips)
                {
                    if (strip != null)
                        strip.enabled = false;
                }
                return;
            }

            var color = valid
                ? new Color32(0x3D, 0x9E, 0x63, 0xFF)
                : new Color32(0xC6, 0x4B, 0x48, 0xFF);
            foreach (var strip in previewBorderStrips)
            {
                if (strip == null)
                    continue;
                strip.color = color;
                strip.enabled = true;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isSource)
            {
                owner?.BeginDrag(
                    skillId,
                    sourceIsActive,
                    GetComponent<SkillLoadoutCardView>());
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            owner?.UpdateDragGhost(eventData.position);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var card = GetComponent<SkillLoadoutCardView>();
            if (isActiveSlot)
            {
                owner?.DropOnActiveSlot(activeIndex, card);
            }
            else if (isAvailableArea)
            {
                owner?.DropOnAvailableArea(card);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            owner?.EndDrag();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.SetDropPreview(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.SetDropPreview(this, false);
        }
    }

    public sealed class SkillLoadoutCardView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private static readonly Color ValidDropColor = new(0.24f, 0.62f, 0.39f, 1f);
        private static readonly Color InvalidDropColor = new(0.78f, 0.29f, 0.27f, 1f);
        private static readonly Color ActiveBorderColor = new(0.32f, 0.40f, 0.48f, 1f);
        private static readonly Color HoverBorderColor = new(0.49f, 0.65f, 0.78f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI fallbackGlyph;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Image feedbackBorder;
        [SerializeField] private CanvasGroup sourceCanvasGroup;
        private Image basicSeparator;
        private Color defaultBackgroundColor;
        private SkillData skill;
        private CombatTooltipView tooltipView;
        private Action clickAction;
        private Coroutine feedbackRoutine;
        private bool isActive;
        private bool isBasic;
        private bool isEmpty;

        public void Configure(
            SkillData sourceSkill,
            bool isActive,
            bool isBasic,
            bool isEmpty,
            CombatTooltipView sourceTooltipView,
            Action onClick)
        {
            CacheVisuals();
            skill = sourceSkill;
            this.isActive = isActive;
            this.isBasic = isBasic;
            this.isEmpty = isEmpty;
            tooltipView = sourceTooltipView;
            clickAction = onClick;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = !isBasic;
                if (clickAction != null)
                {
                    button.onClick.AddListener(() => clickAction.Invoke());
                }
            }

            if (icon != null)
            {
                icon.sprite = skill?.Icon;
                icon.enabled = skill?.Icon != null && !isEmpty;
                icon.preserveAspect = true;
            }

            if (fallbackGlyph != null)
            {
                fallbackGlyph.gameObject.SetActive(
                    isEmpty || (!isBasic && skill?.Icon == null));
                fallbackGlyph.text = isEmpty ? "+" : "?";
            }

            if (lockIcon != null)
            {
                lockIcon.SetActive(isBasic && !isEmpty);
            }

            if (basicSeparator != null)
            {
                basicSeparator.gameObject.SetActive(false);
            }

            ApplyBaseVisual();
        }

        internal Sprite IconSprite => skill?.Icon;
        internal Sprite BackgroundSprite => background != null ? background.sprite : null;
        internal Sprite BorderSprite => borderImage != null ? borderImage.sprite : null;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isEmpty)
            {
                SetFeedback(true, HoverBorderColor);
            }

            if (skill == null)
            {
                return;
            }

            tooltipView ??= CombatTooltipView.ActiveInstance
                ?? FindAnyObjectByType<CombatTooltipView>(FindObjectsInactive.Include);
            if (tooltipView == null || tooltipView.IsSkillTooltipSuppressed)
            {
                return;
            }

            tooltipView.ShowSkill(skill, skill.ActionCost, Input.mousePosition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltipView?.NotifySourceExit();
            if (!isEmpty)
            {
                ApplyBaseVisual();
            }
        }

        public void SetDragSource(bool isSource)
        {
            if (sourceCanvasGroup != null)
            {
                sourceCanvasGroup.alpha = isSource ? 0.4f : 1f;
                sourceCanvasGroup.blocksRaycasts = !isSource;
            }
        }

        public void SetDropPreview(bool isPreview, bool isValid)
        {
            if (!isPreview)
            {
                ApplyBaseVisual();
                return;
            }

            SetFeedback(true, isValid ? ValidDropColor : InvalidDropColor);
        }

        public void FlashInvalid()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            SetFeedback(true, InvalidDropColor);
            feedbackRoutine = StartCoroutine(ClearFeedback());
        }

        private IEnumerator ClearFeedback()
        {
            yield return new WaitForSecondsRealtime(0.18f);
            feedbackRoutine = null;
            ApplyBaseVisual();
        }

        private void CacheVisuals()
        {
            basicSeparator = null;
            if (defaultBackgroundColor == default)
            {
                defaultBackgroundColor = background != null
                    ? background.color
                    : Color.white;
            }
        }

        private void ApplyBaseVisual()
        {
            if (background != null)
            {
                background.color = defaultBackgroundColor;
            }

            if (icon != null)
            {
                icon.enabled = skill?.Icon != null && !isEmpty;
            }

            if (borderImage != null)
            {
                borderImage.color = ActiveBorderColor;
                borderImage.enabled = true;
            }

            if (feedbackBorder != null)
            {
                feedbackBorder.enabled = false;
            }
            if (lockIcon != null)
            {
                lockIcon.SetActive(isBasic && !isEmpty);
            }

            if (basicSeparator != null)
            {
                basicSeparator.gameObject.SetActive(false);
            }
        }

        private void SetFeedback(bool enabled, Color color)
        {
            if (feedbackBorder != null)
            {
                feedbackBorder.color = color;
                feedbackBorder.enabled = enabled;
            }

        }
    }
}
