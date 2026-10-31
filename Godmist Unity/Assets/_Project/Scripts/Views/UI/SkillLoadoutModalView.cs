using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class SkillLoadoutModalView : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private UIWorkspaceWindow workspaceWindow;
        [SerializeField] private TextMeshProUGUI workspaceTitleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropCloseButton;

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

        [Header("Selected skill")]
        [SerializeField] private Image detailsIcon;
        [SerializeField] private TextMeshProUGUI detailsTitle, detailsBody, detailsCost, detailsState;
        [SerializeField] private Button assignButton, removeButton;
        [SerializeField] private TextMeshProUGUI assignButtonText;
        [SerializeField] private ScrollRect detailsScroll;
        private string selectedSkillId = string.Empty;
        private int assignmentSlot = -1;
        private string hoveredDetailsLink;

        [Header("Tooltip and drag")]
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
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
            assignButton?.onClick.AddListener(AssignSelectedSkill);
            removeButton?.onClick.AddListener(RemoveSelectedSkill);
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

            UpdateDragGhost(Godmist.Views.UI.ControllerInput.PointerPosition);

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

            if (session == null || session.IsCharacterInCombat(character?.CharacterId))
            {
                Close();
                return;
            }
            UpdateDetailsLinkHover();
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
            assignButton?.onClick.RemoveListener(AssignSelectedSkill);
            removeButton?.onClick.RemoveListener(RemoveSelectedSkill);
            backdropCloseButton?.onClick.RemoveListener(Close);
        }

        private void OnDisable()
        {
            ResetDrag();
            tooltipView?.Hide();
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            session = null;
            character = null;
        }

        public void Open(GameSession gameSession, string characterId)
        {
            if (gameSession == null
                || gameSession.IsCharacterInCombat(characterId)
                || !IsActivePartyMember(gameSession, characterId)
                || !gameSession.CharactersById.TryGetValue(
                    characterId,
                    out var selectedCharacter)
                || selectedCharacter == null)
            {
                Close();
                return;
            }

            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            session = gameSession;
            session.CharacterProgressionChanged += OnCharacterProgressionChanged;
            tooltipView = ResolveTooltipView();
            character = selectedCharacter;
            selectedSkillId = string.Empty;
            assignmentSlot = -1;
            panelRoot?.SetActive(true);
            workspaceWindow?.Focus();
            ResetScrollPositions();
            Refresh();
        }

        public void Close()
        {
            ResetDrag();
            tooltipView?.Hide();
            panelRoot?.SetActive(false);
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            session = null;
            character = null;
            selectedSkillId = string.Empty;
            hoveredDetailsLink = null;
        }

        private void OnCharacterProgressionChanged(string characterId)
        {
            if (!IsOpen || character?.CharacterId != characterId) return;
            ResetDrag();
            tooltipView?.Hide();
            Refresh();
        }

        private void Refresh()
        {
            if (session == null || character == null)
            {
                return;
            }

            if (session.IsCharacterInCombat(character?.CharacterId))
            {
                Close();
                return;
            }

            if (workspaceTitleText != null)
            {
                var name = character.Definition?.DisplayName ?? character.CharacterId;
                workspaceTitleText.text = $"{name} — Umiejętności";
            }

            if (capacityText != null)
            {
                var loadout = character.ActiveSkillLoadout;
                capacityText.text = loadout == null
                    ? "—"
                    : $"Aktywne {Mathf.Max(0, loadout.ActiveSkillCount - 1)} / {Mathf.Max(0, loadout.Capacity - 1)}";
            }

            RebuildActiveSlots();
            RebuildAvailableSkills();
            ConfigureAvailableDropSurface();
            if (FindKnownSkill(selectedSkillId) == null)
                selectedSkillId = character.GetKnownSkills().FirstOrDefault(skill => skill != null)?.SkillId ?? string.Empty;
            RefreshSelectedSkill();
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
            var activeSkillSlotIds = loadout.ActiveSkillSlotIds;
            ConfigureBasicSlot(loadout, activeSkillSlotIds);

            if (activeSlotsRoot == null || activeSlotTemplate == null)
            {
                return;
            }

            for (var slotIndex = 1; slotIndex < loadout.Capacity; slotIndex++)
            {
                var capturedIndex = slotIndex;
                var button = Instantiate(activeSlotTemplate, activeSlotsRoot);
                button.gameObject.SetActive(true);

                var hasSkill = slotIndex < activeSkillSlotIds.Count
                    && !string.IsNullOrWhiteSpace(activeSkillSlotIds[slotIndex]);
                var skillId = hasSkill ? activeSkillSlotIds[slotIndex] : string.Empty;
                var skill = hasSkill ? FindKnownSkill(skillId) : null;
                var card = EnsureCard(button);
                card.Configure(
                    skill,
                    isActive: hasSkill,
                    isBasic: false,
                    isEmpty: !hasSkill,
                    tooltipView,
                    () => SelectActiveSlot(capturedIndex, skillId));
                card.SetPresentation(character, FormatSlotLabel(slotIndex), false);
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
            IReadOnlyList<string> activeSkillSlotIds)
        {
            if (basicSlotCard == null)
            {
                return;
            }

            var skillId = activeSkillSlotIds != null && activeSkillSlotIds.Count > 0
                ? activeSkillSlotIds[0]
                : loadout.BasicAttackSkillId;
            var skill = FindKnownSkill(skillId);
            var card = EnsureCard(basicSlotCard);
            card.Configure(
                skill,
                isActive: skill != null,
                isBasic: true,
                isEmpty: skill == null,
                tooltipView,
                onClick: () => SelectSkill(skillId));
            basicSlotCard.interactable = true;
            card.SetPresentation(character, FormatSlotLabel(0), false);
            // The basic attack can be inspected but never moved or removed.
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
            foreach (var skill in character.GetKnownSkills())
            {
                if (skill == null
                    || string.IsNullOrWhiteSpace(skill.SkillId)
                    || !knownSkillIds.Add(skill.SkillId))
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
                    isActive: activeSkillIds.Contains(skill.SkillId),
                    isBasic: isBasic,
                    isEmpty: false,
                    tooltipView,
                    () => SelectSkill(skill.SkillId));
                var slotIndex = FindActiveIndex(skill.SkillId);
                card.SetPresentation(character, slotIndex >= 0 ? FormatSlotLabel(slotIndex) : "", false);
                knownCards.Add(card);

                ConfigureSurface(
                    button.gameObject,
                    skill.SkillId,
                    isSource: !isBasic,
                    sourceIsActive: activeSkillIds.Contains(skill.SkillId),
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
                availableCountText.text = $"Poznane umiejętności ({knownCards.Count})";
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

            if (availableViewportDropSurface == null && availableScrollRect?.viewport != null)
            {
                availableViewportDropSurface = availableScrollRect.viewport.GetComponent<SkillLoadoutDragSurface>();
                if (availableViewportDropSurface == null)
                {
                    availableViewportDropSurface = availableScrollRect.viewport.gameObject
                        .AddComponent<SkillLoadoutDragSurface>();
                }
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

        private UIWorkspaceTooltipView ResolveTooltipView()
        {
            return tooltipView != null && tooltipView.gameObject.scene.IsValid() && tooltipView.isActiveAndEnabled
                ? tooltipView
                : UIWorkspaceTooltipView.ActiveInstance;
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

        private static string FormatSlotLabel(int index)
        {
            var shortcut = CombatSkillSlotView.GetShortcutLabel(index);
            return string.IsNullOrEmpty(shortcut) ? (index + 1).ToString() : shortcut;
        }

        private int FindActiveIndex(string skillId)
        {
            var slots = character?.ActiveSkillLoadout?.ActiveSkillSlotIds;
            if (slots == null || string.IsNullOrWhiteSpace(skillId)) return -1;
            for (var i = 0; i < slots.Count; i++) if (slots[i] == skillId) return i;
            return -1;
        }

        private void SelectActiveSlot(int index, string skillId)
        {
            assignmentSlot = index;
            selectedSkillId = skillId ?? string.Empty;
            RefreshSelectedSkill();
        }

        private void SelectSkill(string skillId)
        {
            if (FindKnownSkill(skillId) == null) return;
            selectedSkillId = skillId;
            if (FindActiveIndex(skillId) >= 0) assignmentSlot = -1;
            RefreshSelectedSkill();
        }

        private void RefreshSelectedSkill()
        {
            var skill = FindKnownSkill(selectedSkillId);
            var index = FindActiveIndex(selectedSkillId);
            foreach (var card in knownCards) card.SetSelected(card.SkillId == selectedSkillId && skill != null);
            for (var i = 0; i < activeCards.Count; i++)
                activeCards[i].SetSelected(assignmentSlot > 0 ? i + 1 == assignmentSlot : skill != null && activeCards[i].SkillId == selectedSkillId);
            basicSlotCard?.GetComponent<SkillLoadoutCardView>()?.SetSelected(index == 0);
            tooltipView = ResolveTooltipView();
            tooltipView?.InvalidateSource(detailsBody);
            hoveredDetailsLink = null;
            if (detailsTitle != null) detailsTitle.text = skill?.DisplayName ?? "Wybierz umiejętność";
            if (detailsIcon != null) { detailsIcon.sprite = skill?.Icon; detailsIcon.enabled = skill?.Icon != null; }
            if (detailsBody != null) detailsBody.text = skill == null ? "Wybierz umiejętność z biblioteki, a następnie przypisz ją do wybranego slotu." : tooltipView?.BuildLoadoutSkillDetails(skill, character) ?? skill.TooltipDescription;
            if (detailsCost != null) detailsCost.text = skill == null ? "" : UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, character);
            var slots = character?.ActiveSkillLoadout?.ActiveSkillSlotIds;
            if (assignmentSlot >= (slots?.Count ?? 0)) assignmentSlot = -1;
            var targetSelected = assignmentSlot > 0;
            var freeSlot = slots != null && slots.Skip(1).Any(string.IsNullOrWhiteSpace);
            var canChange = session != null && character != null && !session.IsCharacterInCombat(character.CharacterId);
            if (assignButton != null) { assignButton.gameObject.SetActive(skill != null && index < 0); assignButton.interactable = canChange && (targetSelected || freeSlot); }
            if (removeButton != null) { removeButton.gameObject.SetActive(skill != null && index > 0); removeButton.interactable = canChange; }
            if (assignButtonText != null) assignButtonText.text = targetSelected ? (string.IsNullOrWhiteSpace(slots[assignmentSlot]) ? $"Przypisz do slotu {FormatSlotLabel(assignmentSlot)}" : $"Zastąp slot {FormatSlotLabel(assignmentSlot)}") : "Przypisz";
            if (detailsState != null) detailsState.text = index == 0 ? "Atak podstawowy • stały • 1" : index > 0 ? $"Aktywna • slot {FormatSlotLabel(index)}" : targetSelected ? $"Wybrany slot: {FormatSlotLabel(assignmentSlot)}" : freeSlot ? "Poza aktywnym zestawem" : "Zestaw pełny — wybierz slot do zastąpienia";
            if (detailsScroll != null) detailsScroll.verticalNormalizedPosition = 1;
        }

        private void AssignSelectedSkill()
        {
            if (session == null || character == null || FindKnownSkill(selectedSkillId) == null || FindActiveIndex(selectedSkillId) >= 0) return;
            var success = assignmentSlot > 0
                ? loadoutSystem.TryAddSkillAtSlot(session, character.CharacterId, selectedSkillId, assignmentSlot)
                : loadoutSystem.TryAddSkill(session, character.CharacterId, selectedSkillId);
            // Occupied targets require the existing replacement operation.
            if (!success && assignmentSlot > 0 && assignmentSlot < character.ActiveSkillLoadout.Capacity)
                success = loadoutSystem.TryReplaceSkill(session, character.CharacterId, character.ActiveSkillLoadout.ActiveSkillSlotIds[assignmentSlot], selectedSkillId);
            if (success) { assignmentSlot = -1; Refresh(); }
        }

        private void RemoveSelectedSkill()
        {
            if (session == null || character == null || FindActiveIndex(selectedSkillId) <= 0) return;
            if (loadoutSystem.TryRemoveSkill(session, character.CharacterId, selectedSkillId)) { assignmentSlot = -1; Refresh(); }
        }

        private void UpdateDetailsLinkHover()
        {
            if (detailsBody == null || tooltipView == null || tooltipView.IsSkillTooltipSuppressed) return;
            var canvas = detailsBody.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var inside = detailsScroll?.viewport == null || RectTransformUtility.RectangleContainsScreenPoint(detailsScroll.viewport, Godmist.Views.UI.ControllerInput.PointerPosition, camera);
            var hit = inside ? TMP_TextUtilities.FindIntersectingLink(detailsBody, Godmist.Views.UI.ControllerInput.PointerPosition, camera) : -1;
            var id = hit < 0 ? null : detailsBody.textInfo.linkInfo[hit].GetLinkID();
            if (id == hoveredDetailsLink) return;
            tooltipView.NotifySourceExit(detailsBody);
            hoveredDetailsLink = id;
            if (id != null)
            {
                tooltipView.InvalidateSource(detailsBody);
                tooltipView.ShowGlossaryEntry(id, detailsBody);
            }
        }

        internal void BeginDrag(
            string skillId,
            bool isActive,
            SkillLoadoutCardView sourceCard)
        {
            if (session == null
                || character == null
                || session.IsCharacterInCombat(character?.CharacterId)
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
            UpdateDragGhost(Godmist.Views.UI.ControllerInput.PointerPosition);
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
            var dragParent = dragGhostRect.parent as RectTransform;
            if (dragParent == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragParent,
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
            dragGhostRect.sizeDelta = new Vector2(72f, 72f);
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
            background.color = Color.white;
            background.preserveAspect = false;
            background.raycastTarget = false;

            var border = dragGhost.transform.Find("Border")?.GetComponent<Image>();
            if (border == null)
            {
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
                border = borderObject.GetComponent<Image>();
            }
            border.sprite = draggedCard?.BorderSprite;
            border.type = border.sprite != null
                ? Image.Type.Sliced
                : Image.Type.Simple;
            border.fillCenter = false;
            border.color = new Color32(0x40, 0x53, 0x68, 0xFF);
            border.raycastTarget = false;

            var overlay = dragGhost.transform.Find("Overlay")?.GetComponent<Image>();
            if (overlay != null)
            {
                overlay.enabled = false;
            }

            dragGhostImage = dragGhost.transform.Find("Icon")?.GetComponent<Image>();
            if (dragGhostImage == null)
            {
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
            }
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
                || session.IsCharacterInCombat(character?.CharacterId)
                || string.IsNullOrWhiteSpace(draggedSkillId))
            {
                return;
            }

            dragHasBeenHandled = true;
            var success = false;
            if (draggedSkillIsActive)
            {
                success = loadoutSystem.TryMoveSkill(
                    session,
                    character.CharacterId,
                    draggedSkillId,
                    targetIndex);
            }
            else if (character.ActiveSkillLoadout != null
                && targetIndex > 0
                && targetIndex < character.ActiveSkillLoadout.Capacity)
            {
                var targetSkillId = character.ActiveSkillLoadout.ActiveSkillSlotIds[targetIndex];
                success = string.IsNullOrWhiteSpace(targetSkillId)
                    ? loadoutSystem.TryAddSkillAtSlot(
                        session,
                        character.CharacterId,
                        draggedSkillId,
                        targetIndex)
                    : loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        targetSkillId,
                        draggedSkillId);
            }

            if (success)
            {
                assignmentSlot = -1;
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
                || session.IsCharacterInCombat(character?.CharacterId)
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
                assignmentSlot = -1;
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
                    && targetIndex > 0
                    && targetIndex < loadout.Capacity;
            }

            return targetIndex > 0
                && targetIndex < loadout.Capacity
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
                Godmist.Views.UI.ControllerInput.PointerPosition,
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
            foreach (var skill in character?.GetKnownSkills() ?? new List<SkillData>())
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
                    var events = EventSystem.current;
                    var selected = events != null ? events.currentSelectedGameObject : null;
                    if (selected != null && selected.transform.IsChildOf(child.transform))
                        events.SetSelectedGameObject(null);
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

    public class SkillLoadoutCardView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private static readonly Color ValidDropColor = new(0.35f, 1f, 0.55f, 1f);
        private static readonly Color InvalidDropColor = new(1f, 0.3f, 0.25f, 1f);
        private static readonly Color ActiveBorderColor = new(0.32f, 0.40f, 0.48f, 1f);
        private static readonly Color HoverBorderColor = new(0.7f, 0.9f, 1f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image icon;
        [SerializeField] private SkillIconShapeMask iconMask;
        [SerializeField] private TextMeshProUGUI fallbackGlyph;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Image feedbackBorder;
        [SerializeField] private CanvasGroup sourceCanvasGroup;
        [SerializeField] private TextMeshProUGUI slotStateText;
        [SerializeField] private Image selectionBorder;
        [SerializeField] private bool compactHover;
        private CharacterInstance previewCharacter;
        private bool selected;
        public string SkillId => skill?.SkillId ?? string.Empty;
        public void SetPresentation(CharacterInstance owner, string state, bool isSelected)
        {
            previewCharacter = owner;
            if (slotStateText != null)
            {
                slotStateText.text = state ?? "";
                slotStateText.transform.SetAsLastSibling();
            }
            SetSelected(isSelected);
        }
        public void SetSelected(bool value) { selected = value; if (selectionBorder != null) selectionBorder.enabled = value; }
        private Image basicSeparator;
        private Color defaultBackgroundColor;
        private SkillData skill;
        private UIWorkspaceTooltipView tooltipView;
        private Action clickAction;
        private Coroutine feedbackRoutine;
        private bool isActive;
        private bool isBasic;
        private bool isEmpty;
        private bool hovered;
        private bool dropPreview;
        private bool validDrop;

        public void Configure(
            SkillData sourceSkill,
            bool isActive,
            bool isBasic,
            bool isEmpty,
            UIWorkspaceTooltipView sourceTooltipView,
            Action onClick)
        {
            CacheVisuals();
            skill = sourceSkill;
            this.isActive = isActive;
            this.isBasic = isBasic;
            this.isEmpty = isEmpty;
            hovered = dropPreview = false;
            tooltipView = sourceTooltipView != null && sourceTooltipView.isActiveAndEnabled
                ? sourceTooltipView
                : UIWorkspaceTooltipView.ActiveInstance;
            clickAction = onClick;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = clickAction != null || !isBasic;
                if (clickAction != null)
                {
                    button.onClick.AddListener(() => clickAction.Invoke());
                }
            }

            if (icon != null)
            {
                icon.sprite = skill?.Icon;
                icon.enabled = skill?.Icon != null && !isEmpty;
                icon.preserveAspect = iconMask == null;
            }

            if (fallbackGlyph != null)
            {
                fallbackGlyph.gameObject.SetActive(
                    isEmpty || skill?.Icon == null);
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
            hovered = true;
            RefreshFeedback();

            if (skill == null)
            {
                return;
            }

            if (tooltipView == null || !tooltipView.isActiveAndEnabled)
            {
                tooltipView = UIWorkspaceTooltipView.ActiveInstance;
            }
            if (tooltipView == null || tooltipView.IsSkillTooltipSuppressed)
            {
                return;
            }

            if (compactHover)
                tooltipView.ShowText(skill.DisplayName, UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, previewCharacter), this);
            else tooltipView.ShowSkill(skill, skill.ActionCost, this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltipView?.NotifySourceExit(this);
            hovered = false;
            RefreshFeedback();
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
            dropPreview = isPreview;
            validDrop = isValid;
            RefreshFeedback();
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

            ConfigureFeedbackBorder();
        }

        private void ApplyBaseVisual()
        {
            if (background != null)
            {
                background.color = compactHover && isEmpty ? new Color(.6f, .65f, .65f, 1f) : defaultBackgroundColor;
            }

            if (icon != null)
            {
                icon.enabled = skill?.Icon != null && !isEmpty;
            }

            if (borderImage != null)
            {
                borderImage.color = compactHover ? (isEmpty ? new Color(.6f, .65f, .65f, 1f) : Color.white) : ActiveBorderColor;
                borderImage.enabled = true;
            }

            if (selectionBorder != null) selectionBorder.enabled = selected;
            RefreshFeedback();
            if (compactHover && fallbackGlyph != null) fallbackGlyph.color = isEmpty ? new Color(.6f, .63f, .58f, 1f) : Color.white;
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

        private void RefreshFeedback()
        {
            SetFeedback(dropPreview || hovered, dropPreview
                ? validDrop ? ValidDropColor : InvalidDropColor
                : HoverBorderColor);
        }

        private void ConfigureFeedbackBorder()
        {
            if (feedbackBorder == null)
            {
                return;
            }

            if (feedbackBorder.sprite == null && borderImage != null)
            {
                feedbackBorder.sprite = borderImage.sprite;
            }

            feedbackBorder.type = Image.Type.Sliced;
            feedbackBorder.fillCenter = false;
            feedbackBorder.raycastTarget = false;
        }
    }
}
