using Godmist.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Views.UI;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Scene-owned, shared tooltip for every UI Workspace surface.
    /// Glossary links stay inside this view so combat and workspace surfaces do not
    /// maintain separate tooltip systems.
    /// </summary>
    public sealed partial class UIWorkspaceTooltipView : MonoBehaviour
    {
        private static UIWorkspaceTooltipView activeInstance;
        public static UIWorkspaceTooltipView ActiveInstance
        {
            // A never-activated Unity object may be destroyed without OnDestroy.
            // Never expose its surviving managed wrapper to hover sources using ?.
            get => activeInstance != null ? activeInstance : null;
            private set => activeInstance = value;
        }
        private static readonly List<UIWorkspaceTooltipView> enabledInstances = new();

        [SerializeField] private RectTransform tooltipRect;
        private ItemRarityBorderGraphic itemRarityBorder;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI metaText;
        [SerializeField] private CombatTooltipGlossary glossary;
        [SerializeField, Min(0f)] private float hoverDelay = .25f;
        [SerializeField, Min(0f)] private float lockDelay = 1f;
        [SerializeField] private Image lockProgressFill;
        [SerializeField] private Slider npcLoyaltyProgressSliderPrefab;
        [SerializeField] private Sprite npcLoyaltyGoldIcon;
        [SerializeField] private Sprite npcLoyaltyBarFrame;
        [SerializeField] private Sprite npcLoyaltyBarFill;
        [SerializeField, Min(1f)] private float lockProgressWidth = 336f;
        [SerializeField] private Graphic lockAccentGraphic;
        [SerializeField] private Color lockedAccentColor = new(.22f, .18f, .10f, .97f);
        // Manual pinning uses the glossary blue on the authored border and any
        // legacy lock accent graphic without adding prefab wiring.
        private static readonly Color pinnedAccentColor = new(126f / 255f, 219f / 255f, 1f, 1f);
        [SerializeField, Min(0f)] private float nestedLinkDelay = .25f;
        // Shared timing also applies to older prefabs with a serialized grace value.
        private const float ExitGracePeriod = .12f;
        [SerializeField, Min(64f)] private float minimumHeight = 68f;
        [SerializeField, Min(0f)] private float contentHeightPadding = 16f;
        [SerializeField] private Vector2 pointerOffset = new(4f, -4f);

        private const float TitleOnlyTooltipHorizontalPadding = 32f;
        private const float TitleOnlyTooltipMinimumWidth = 120f;
        private const float TitleOnlyTooltipVerticalPadding = 20f;
        private const float ItemTooltipIconAndSpacingWidth = 76f;
        private const float ItemTooltipContentHorizontalPadding = 24f;
        private const float ItemTooltipTitleBuffer = 8f;
        private const float ItemTooltipMinimumWidth = 200f;
        private const float ItemTooltipMaximumWidth = 440f;

        private CanvasGroup canvasGroup;
        private readonly List<UIWorkspaceTooltipView> nested = new();
        private bool visible;
        private bool sourceHovered;
        private bool suppressForDrag;
        private bool isLocked;
        private bool isPinned;
        private bool isNested;
        private bool isRichUnitContent;
        private object sourceIdentity;
        private object pendingSource;
        private Action pendingShow;
        private float pendingShownAt;
        private bool resizeAfterLayout;
        private float shownAt;
        private float lockedAt = -1f;
        private string hoveredLinkId;
        private string pendingLinkId;
        private UIWorkspaceTooltipView pendingLinkOwner;
        private float pendingLinkShownAt;
        private UIWorkspaceTooltipView pendingNestedExitOwner;
        private float pendingNestedExitStartedAt;
        private object pendingNestedSource;
        private Action pendingNestedShow;
        private float pendingNestedShownAt;
        private UIWorkspaceTooltipView pendingNestedOwner;
        private string glossaryEntryId;
        private UIWorkspaceTooltipView glossaryParent;
        private static int lastPinnedCloseFrame = -1;
        private UIWorkspaceRichUnitTooltipView richUnitContent;
        private UIWorkspaceTooltipView contentTargetOverride;
        private Graphic authoredBorderGraphic;
        private Color defaultAccentColor;
        private bool hasDefaultAccentColor;
        private Color defaultBorderColor;
        private bool hasDefaultBorderColor;
        private const string TooltipFooterName = "TooltipFooter";
        private RectTransform tooltipFooter;
        private TextMeshProUGUI tooltipFooterShortcut;
        private UIWorkspaceTooltipLockGlyph tooltipFooterLockGlyph;
        private TextAlignmentOptions defaultTitleAlignment = TextAlignmentOptions.TopLeft;
        private NpcLoyaltyProgressionView npcLoyaltyProgression;
        private float defaultTooltipWidth;
        private Vector2 placementScreenPosition;
        private bool hasPlacementScreenPosition;
        private CombatStatusPresentation? liveExplorationStatus;
        private int displayedStatusSeconds;

        public bool IsSkillTooltipSuppressed => suppressForDrag;

        public Slider InstantiateNpcLoyaltyProgressSlider(Transform parent)
        {
            if (parent == null || npcLoyaltyProgressSliderPrefab == null) return null;

            return Instantiate(
                npcLoyaltyProgressSliderPrefab,
                parent,
                false);
        }

        private void Awake()
        {
            // Windows are created later under another canvas child. Keep the
            // tooltip layer above them in every scene, including DungeonSandbox.
            transform.parent?.SetAsLastSibling();
            defaultTooltipWidth = tooltipRect != null ? tooltipRect.rect.width : 0f;
            defaultTitleAlignment = TextAlignmentOptions.TopLeft;
            EnsureCanvasGroup();
            ConfigureTextLayout(titleText);
            ConfigureTextLayout(bodyText);
            ConfigureTextLayout(metaText);
            if (bodyText != null) bodyText.lineSpacing = 1.5f;
            ConfigureItemHeaderLayout();
            EnsureNpcLoyaltyProgression();
            EnsureTooltipFooter();
            CacheDefaultAccentColor();
            Hide();
        }

        private void OnEnable()
        {
            if (!enabledInstances.Contains(this)) enabledInstances.Add(this);
            if (!isNested) ActiveInstance = this;
        }

        private void OnDisable()
        {
            Hide();
            enabledInstances.Remove(this);
            if (ActiveInstance == this) RestoreActiveInstance();
        }

        private static void RestoreActiveInstance()
        {
            ActiveInstance = null;
            for (var index = enabledInstances.Count - 1; index >= 0; index--)
            {
                var candidate = enabledInstances[index];
                if (candidate == null)
                {
                    enabledInstances.RemoveAt(index);
                    continue;
                }

                if (candidate.isActiveAndEnabled && !candidate.isNested)
                {
                    ActiveInstance = candidate;
                    return;
                }
            }
        }

        private static void ConfigureTextLayout(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        private void ConfigureItemHeaderLayout()
        {
            var headerLayout = titleText?.transform.parent?.GetComponent<HorizontalLayoutGroup>();
            if (headerLayout == null) return;

            // Keep the icon centered against the complete title block instead of
            // pinning it to the first line of a wrapped title.
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            if(titleText != null)
            {
                titleText.alignment = TextAlignmentOptions.TopLeft;
                var titleLayout=titleText.GetComponent<LayoutElement>();
                if(titleLayout != null) { titleLayout.minWidth=0; titleLayout.flexibleWidth=1; }
            }
        }

        private void OnDestroy()
        {
            enabledInstances.Remove(this);
            if (ActiveInstance == this) RestoreActiveInstance();
        }

        private void Update()
        {
            if (!isNested && ActiveInstance == this
                && Input.GetKeyDown(KeyCode.T)
                && !UIWorkspaceInputRouter.HasFocusedTextInput())
            {
                TryPinAtCurrentPointer(Input.mousePosition);
            }

            if (!isNested && ActiveInstance == this
                && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                HandlePointerClick(Input.mousePosition, Input.GetMouseButtonDown(1));
            }

            if ((visible && !IsSourceAlive(sourceIdentity))
                || (pendingShow != null && !IsSourceAlive(pendingSource)))
            {
                Hide();
                return;
            }

            if (!isNested)
            {
                RemoveInvalidNestedViews();
                if (pendingNestedShow != null && !IsSourceAlive(pendingNestedSource))
                {
                    ClearPendingNestedShow();
                }
            }

            if (pendingShow != null)
            {
                if (!sourceHovered)
                {
                    // Passing over a different interactive source while travelling to a
                    // locked tooltip must not tear down the current tooltip immediately.
                    // Keep it unless the new source was hovered for the whole delay.
                    if (visible && isLocked)
                    {
                        pendingShow = null;
                        pendingSource = null;
                        return;
                    }

                    Hide();
                    return;
                }

                if (Time.unscaledTime - pendingShownAt < hoverDelay) return;
                var show = pendingShow;
                pendingShow = null;
                show();
            }

            if (pendingNestedShow != null)
            {
                if (!IsPointerOverAnyTooltip())
                {
                    ClearPendingNestedShow();
                }
                else if (Time.unscaledTime - pendingNestedShownAt >= nestedLinkDelay)
                {
                    var show = pendingNestedShow;
                    var source = pendingNestedSource;
                    var owner = pendingNestedOwner;
                    ClearPendingNestedShow();
                    if (owner == null || !owner.IsDescendantOf(this)) owner = this;
                    owner.CreateNestedTooltip(source, show, pinnedContent: true);
                }
            }

            if (visible)
            {
                RefreshExplorationStatusDuration();
                if (!visible) return;
                if (!isLocked && Time.unscaledTime - shownAt >= lockDelay)
                {
                    isLocked = true;
                    lockedAt = Time.unscaledTime;
                    ApplyPointerInteraction();
                }

                UpdateLockProgressVisual();

                if (isNested) return;

                var hasPinnedNestedContent = !isPinned && HasPinnedNestedContent();
                if (!isPinned && !isLocked && !hasPinnedNestedContent)
                {
                    if (!sourceHovered && !IsPointerOverAnyTooltip()) Hide();
                    return;
                }

                if (!isPinned && !hasPinnedNestedContent && !sourceHovered && !IsPointerOverAnyTooltip()
                    && Time.unscaledTime - lockedAt >= ExitGracePeriod)
                {
                    Hide();
                    return;
                }

                RefreshNestedTooltip();
                ExpireNestedExitGracePeriod();
            }
        }

        private static readonly Unity.Profiling.ProfilerMarker TooltipLateSample = new("Godmist.UI.TooltipLate");
        private void LateUpdate()
        {
            using var sample = TooltipLateSample.Auto();
            if (!visible || !resizeAfterLayout) return;
            resizeAfterLayout = false;
            ResizeForContent();
            // A manual pin owns its screen position. Re-running PlaceAt here would
            // make a pinned card drift whenever layout or the pointer changes.
            if (!isPinned)
                PlaceAt(hasPlacementScreenPosition ? placementScreenPosition : Input.mousePosition);
        }

        private void SetRichUnitContent(BattleUnit unit)
        {
            if (contentTargetOverride != null && contentTargetOverride != this)
            {
                var target = contentTargetOverride;
                contentTargetOverride = null;
                target.SetRichUnitContent(unit);
                return;
            }

            if (unit?.Character == null) return;
            var character = unit.Character;
            SetContentCore(
                null,
                character.Definition?.DisplayName ?? character.CharacterId,
                string.Empty,
                string.Empty,
                reserveIconSlot: false,
                linkifyBody: false);

            // Give the rich card room for readable labels and the authored
            // workspace slider treatment. Normal content restores the prefab
            // width in SetContentCore.
            SetTooltipWidth(ClampTooltipWidth(UIWorkspaceRichUnitTooltipView.RichTooltipWidth));

            isRichUnitContent = true;
            PrepareRichUnitContent();
            if (richUnitContent == null) return;

            richUnitContent.gameObject.SetActive(true);
            richUnitContent.Bind(unit, this);
            ResizeForContent();
            resizeAfterLayout = true;
            PlaceAt(Input.mousePosition);
        }

        // The dungeon owner prepares its shared tooltip while loading the floor.
        // Generic/nested text tooltips stay lazy and do not pay this cost.
        public void PrepareRichUnitContent()
        {
            richUnitContent ??= CreateRichUnitContent();
        }

        private UIWorkspaceRichUnitTooltipView CreateRichUnitContent()
        {
            if (bodyText == null || bodyText.transform.parent == null) return null;

            var content = new GameObject(
                "RichUnitContent",
                typeof(RectTransform),
                typeof(UIWorkspaceRichUnitTooltipView));
            content.transform.SetParent(bodyText.transform.parent, false);
            content.transform.SetSiblingIndex(bodyText.transform.GetSiblingIndex());
            var view = content.GetComponent<UIWorkspaceRichUnitTooltipView>();
            view.Configure(bodyText, GetStatusIcon, this);
            content.SetActive(false);
            return view;
        }

        public void ShowSkill(
            SkillData skill,
            int actionCost,
            object source,
            BattleUnit caster = null,
            string availabilityReason = null)
        {
            if (suppressForDrag || skill == null) return;
            QueueShow(source ?? skill, () => SetContent(
                skill.Icon,
                string.IsNullOrWhiteSpace(skill.DisplayName)
                    ? "Przedmiot użytkowy"
                    : skill.DisplayName,
                BuildSkillDescription(skill, caster),
                BuildSkillMeta(skill, actionCost, caster) + (string.IsNullOrEmpty(availabilityReason) ? string.Empty : "\n" + availabilityReason)));
        }

        public void ShowStatus(CombatStatusPresentation presentation, object source)
        {
            var effect = presentation.Effect;
            if (effect == null) return;
            QueueShow(source ?? effect, () => SetStatusContent(presentation));
        }

        private void SetStatusContent(CombatStatusPresentation presentation)
        {
            if (contentTargetOverride != null && contentTargetOverride != this)
            {
                var target = contentTargetOverride;
                contentTargetOverride = null;
                target.SetStatusContent(presentation);
                return;
            }
            var effect = presentation.Effect;
            TryGetGlossaryEntry(effect.EffectId, out var glossaryEntry);
            SetContent(
                GetStatusIcon(presentation),
                FormatStatusTitle(
                    effect.EffectId,
                    glossaryEntry?.Title,
                    presentation.DisplayName),
                BuildPresentedStatusPayload(presentation),
                presentation.HasMixedDurations || presentation.StackCount == 0 ? "" : presentation.ExplorationCharacter != null ? ExplorationStatusDuration(presentation) : BuildStatusMeta(
                    effect,
                    presentation.Aura,
                    presentation.OutgoingRedirect,
                    presentation.StackCount));
            if (presentation.ExplorationCharacter != null && !presentation.HasMixedDurations && presentation.StackCount > 0)
            {
                liveExplorationStatus = presentation;
                displayedStatusSeconds = ExplorationStatusSeconds(presentation);
            }
        }

        private void RefreshExplorationStatusDuration()
        {
            if (!liveExplorationStatus.HasValue || metaText == null) return;
            var presentation = liveExplorationStatus.Value;
            if (!PassiveEffectLifecycleSystem.IsActive(presentation.Effect)) { Hide(); return; }
            var seconds = ExplorationStatusSeconds(presentation);
            if (seconds == displayedStatusSeconds) return;
            displayedStatusSeconds = seconds;
            metaText.text = ExplorationStatusDuration(presentation);
            resizeAfterLayout = true;
        }

        private static int ExplorationStatusSeconds(CombatStatusPresentation presentation)
        {
            var ticks = presentation.Effect?.RemainingDuration ?? 0;
            if (ticks <= 0) return -1;
            return Mathf.CeilToInt(Mathf.Max(0, ticks * ExplorationStatusEffectSystem.ExplorationTickSeconds
                - (presentation.ExplorationCharacter?.ExplorationStatusElapsedSeconds ?? 0)));
        }

        public static string ExplorationStatusDuration(CombatStatusPresentation presentation)
        {
            if (presentation.HasMixedDurations) return "Różne czasy działania";
            var ticks = presentation.Effect?.RemainingDuration ?? 0;
            if (ticks <= 0) return "Bez limitu czasu";
            return $"Pozostało: {ExplorationStatusSeconds(presentation)} s";
        }

        public void ShowCompactText(string title, string body, object source)
        {
            title = UiTerms.Present(title);
            body = UiTerms.Present(body);
            QueueShow(source ?? this, () => {
                SetContent(null, title, body, string.Empty);
                var titleWidth = titleText != null ? titleText.GetPreferredValues(title, 10000f, 0f).x : 0f;
                var bodyWidth = bodyText != null ? bodyText.GetPreferredValues(body, 10000f, 0f).x : 0f;
                SetTooltipWidth(ClampTooltipWidth(Mathf.Clamp(Mathf.Ceil(Mathf.Max(titleWidth, bodyWidth)) + TitleOnlyTooltipHorizontalPadding, 120f, defaultTooltipWidth > 1f ? defaultTooltipWidth : 360f)));
                ResizeForContent();
                PlaceAt(Input.mousePosition);
            });
        }

        public void ShowText(string title, string body, object source)
        {
            ShowText(title, body, source, string.Empty);
        }

        public void ShowText(string title, string body, object source, string meta)
        {
            title = UiTerms.Present(title);
            QueueShow(source ?? this, () => SetContent(null, title, body, meta));
        }

        /// <summary>
        /// Shows the same compact unit presentation used by the party rail for a
        /// world or timeline hover. The view is composed at runtime so existing
        /// tooltip prefabs remain backward compatible and require no serialized
        /// references to combat HUD prefabs.
        /// </summary>
        public void ShowUnit(BattleUnit unit, object source, bool known = true)
        {
            if (!known || unit?.Character == null)
            {
                if (!known) ShowText("Nieznany przeciwnik", string.Empty, source ?? unit);
                return;
            }

            QueueShow(source ?? unit, () => SetRichUnitContent(unit));
        }

        public void ShowUnit(CharacterInstance character, object source, bool isPlayerSide = true)
        {
            if (character == null) return;
            ShowUnit(new BattleUnit { Character = character, IsPlayerSide = isPlayerSide }, source, true);
        }

        public bool IsPinned => isPinned;

        public bool IsVisible => visible;

        /// <summary>
        /// Returns true while this view is already showing, or while it is
        /// waiting to show content for the same source. World hover callers
        /// use this to keep one pending hover alive across Update frames.
        /// </summary>
        public bool IsVisibleOrPendingFor(object source)
        {
            return visible || pendingShow != null && ReferenceEquals(pendingSource, source);
        }

        /// <summary>
        /// Indicates that this root tooltip owns a manually pinned branch, or
        /// that one of its descendants is protected by that branch. Controllers
        /// use this for soft source transitions so a deep pin is not mistaken
        /// for an unpinned root tooltip.
        /// </summary>
        public bool HasPinnedBranch => HasProtectedTooltipChain();

        private bool IsPinnedInChain => isPinned
            || (glossaryParent != null && glossaryParent.IsPinnedInChain);

        /// <summary>
        /// Pins the tooltip at its current screen position. This direct API remains
        /// idempotent for callers that explicitly request a pin; T uses
        /// <see cref="TogglePinned"/> for its toggle behavior.
        /// </summary>
        public void PinAtCurrentPosition()
        {
            if (!visible) return;
            isPinned = true;
            isLocked = true;
            lockedAt = Time.unscaledTime;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();
            RefreshDescendantPinnedVisuals();
        }

        /// <summary>
        /// Toggles the manual pin owned by this tooltip branch. When this tooltip is
        /// blue only because an ancestor is pinned, the ancestor owns the toggle so
        /// pressing T never creates a child pin underneath an existing one.
        /// </summary>
        public void TogglePinned()
        {
            if (!visible) return;

            var owner = GetManualPinnedOwner();
            if (owner != null)
            {
                owner.UnpinManualPosition();
                return;
            }

            PinAtCurrentPosition();
        }

        private UIWorkspaceTooltipView GetManualPinnedOwner()
        {
            for (var current = this; current != null; current = current.glossaryParent)
                if (current.isPinned) return current;

            return null;
        }

        private void UnpinManualPosition()
        {
            if (!isPinned) return;

            isPinned = false;
            isLocked = true;
            lockedAt = Time.unscaledTime;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();
            RestoreUnpinnedDescendants();
        }

        private void RestoreUnpinnedDescendants()
        {
            foreach (var child in nested)
            {
                if (child == null || child.isPinned) continue;

                child.isLocked = true;
                child.lockedAt = Time.unscaledTime;
                child.ApplyPointerInteraction();
                child.UpdateLockProgressVisual();
                child.RestoreUnpinnedDescendants();
            }
        }

        private void RefreshDescendantPinnedVisuals()
        {
            foreach (var child in nested)
            {
                if (child == null) continue;
                child.UpdateLockProgressVisual();
                child.RefreshDescendantPinnedVisuals();
            }
        }

        /// <summary>
        /// Toggles the tooltip under the pointer, or the active source tooltip
        /// when the pointer is still over its source. This lets T be pressed
        /// before crossing the source-to-card gap without changing placement.
        /// </summary>
        public static bool TryPinAtCurrentPointer(Vector2 screenPosition)
        {
            var hovered = GetTooltipAtScreenPosition(screenPosition);
            if (hovered != null)
            {
                hovered.TogglePinned();
                return true;
            }

            var active = ActiveInstance;
            if (active == null || !active.visible) return false;
            active.TogglePinned();
            return true;
        }

        /// <summary>
        /// Processes a global pointer press against the shared tooltip geometry.
        /// Returning false for an outside press lets the normal world/UI input
        /// handlers continue with their existing action or context menu.
        /// </summary>
        public static bool HandlePointerClick(Vector2 screenPosition, bool _rightButton)
        {
            var hovered = GetTooltipAtScreenPosition(screenPosition);
            var insidePinnedChain = GetPinnedTooltipAtScreenPosition(screenPosition);
            if (insidePinnedChain != null)
            {
                // Both mouse buttons are content clicks while the pointer is in
                // the complete chain. Manual pinning belongs to T.
                return true;
            }

            // Any press outside a pinned chain closes it. Returning false keeps
            // the press available to the normal UI/world input owner.
            if (ClosePinnedTooltipChains())
                lastPinnedCloseFrame = Time.frameCount;
            return hovered != null;
        }

        public static bool IsPointerOverVisibleTooltip(Vector2 screenPosition)
        {
            return GetTooltipAtScreenPosition(screenPosition) != null;
        }

        public static bool TryClosePinnedTooltip()
        {
            if (lastPinnedCloseFrame == Time.frameCount) return true;

            var closed = ClosePinnedTooltipChains();
            if (closed) lastPinnedCloseFrame = Time.frameCount;
            return closed;
        }

        private static bool ClosePinnedTooltipChains()
        {
            var closed = false;
            for (var index = enabledInstances.Count - 1; index >= 0; index--)
            {
                var tooltip = enabledInstances[index];
                if (tooltip == null || tooltip.isNested || !tooltip.HasPinnedTooltipChain()) continue;
                tooltip.Hide();
                closed = true;
            }

            return closed;
        }

        private bool HasPinnedTooltipChain()
        {
            return visible && (isPinned || nested.Any(view => view != null && view.HasPinnedTooltipChain()));
        }

        private bool HasProtectedTooltipChain()
        {
            return visible && (IsPinnedInChain
                || nested.Any(view => view != null && view.HasProtectedTooltipChain()));
        }

        private static UIWorkspaceTooltipView GetTooltipAtScreenPosition(Vector2 screenPosition)
        {
            for (var index = enabledInstances.Count - 1; index >= 0; index--)
            {
                var tooltip = enabledInstances[index];
                if (tooltip == null || !tooltip.visible || !tooltip.IsPointerOverTooltip(screenPosition)) continue;
                return tooltip;
            }

            return null;
        }

        private static UIWorkspaceTooltipView GetPinnedTooltipAtScreenPosition(Vector2 screenPosition)
        {
            for (var index = enabledInstances.Count - 1; index >= 0; index--)
            {
                var tooltip = enabledInstances[index];
                if (tooltip == null || tooltip.isNested) continue;
                var hovered = tooltip.FindPinnedTooltipAtScreenPosition(screenPosition);
                if (hovered != null) return hovered;
            }

            return null;
        }

        private UIWorkspaceTooltipView FindPinnedTooltipAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasPinnedTooltipChain()) return null;

            return FindTooltipAtScreenPositionInChain(screenPosition);
        }

        private UIWorkspaceTooltipView FindTooltipAtScreenPositionInChain(Vector2 screenPosition)
        {
            // Every visible child belongs to this chain, even when it is still an
            // ordinary hover child. This keeps a pinned parent alive while the
            // pointer crosses the gap between its own card and the child card.
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child == null || !child.visible) continue;
                var hoveredChild = child.FindTooltipAtScreenPositionInChain(screenPosition);
                if (hoveredChild != null) return hoveredChild;
            }

            return IsPointerOverTooltip(screenPosition) ? this : null;
        }

        public void ShowNpcLoyalty(NpcLoyaltyTooltipData data, object source)
        {
            if (data == null) return;
            QueueShow(source ?? data, () => SetNpcLoyaltyContent(data));
        }

        public void RefreshNpcLoyaltyTooltip(NpcLoyaltyTooltipData data, object source)
        {
            if (data == null || source == null) return;

            if (visible && ReferenceEquals(sourceIdentity, source))
            {
                RefreshNpcLoyaltyContent(data);
                return;
            }

            if (pendingShow != null && ReferenceEquals(pendingSource, source))
            {
                pendingShow = () => SetNpcLoyaltyContent(data);
            }
        }

        public void ShowItem(
            ItemInstance item,
            CharacterInstance owner,
            object source,
            int? contextPrice = null)
        {
            if (item?.Definition == null) return;
            QueueShow(source ?? item, () => SetContentCore(
                ItemIconPresentation.GetIcon(item),
                ItemRarityPresentation.Name(item),
                LinkItemMechanics(item, BuildItemBody(item, owner)),
                BuildItemMeta(item, contextPrice),
                reserveIconSlot: true, linkifyBody: false, itemRarity: item.CurrentRarity));
        }

        /// <summary>
        /// Shows the normal item tooltip with a short context-specific suffix.
        /// Service views use this for transient information such as a buy or sell
        /// price without duplicating the item-stat formatting here.
        /// </summary>
        public void ShowItemWithExtra(
            ItemInstance item,
            CharacterInstance owner,
            object source,
            string extraBody,
            int? contextPrice = null)
        {
            if (item?.Definition == null) return;

            QueueShow(source ?? item, () => SetContentCore(
                ItemIconPresentation.GetIcon(item),
                ItemRarityPresentation.Name(item),
                LinkItemMechanics(item, AppendTooltipExtra(BuildItemBody(item, owner), extraBody)),
                BuildItemMeta(item, contextPrice),
                reserveIconSlot: true, linkifyBody: false, itemRarity: item.CurrentRarity));
        }

        public void NotifySourceExit(object source = null)
        {
            if (source != null)
            {
                // Rich status icons and glossary links belong to the deepest
                // nested view once their detail view has been created. Route the
                // exit through the whole tree instead of only checking root
                // siblings; otherwise a level-3+ card can lose its parent chain.
                if (TryNotifyNestedSourceExit(source)) return;

                if (ReferenceEquals(pendingNestedSource, source))
                {
                    ClearPendingNestedShow();
                    return;
                }

                // Pointer enter for the next UI element can occur before pointer exit for
                // the previous one. While a new tooltip is pending, only that new source
                // may cancel the pending hover.
                var expectedSource = pendingSource ?? sourceIdentity;
                if (!ReferenceEquals(source, expectedSource)) return;
            }

            sourceHovered = false;
            PromoteVisibleTooltipToExitGracePeriod();
        }

        private void PromoteVisibleTooltipToExitGracePeriod()
        {
            // A card that has already been shown needs a short, enterable grace
            // window when the pointer leaves its source. Pending hovers still
            // cancel normally, and explicit pins keep their own lifetime.
            if (!visible || isPinned || isLocked || pendingShow != null) return;

            isLocked = true;
            lockedAt = Time.unscaledTime;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();
        }

        public void NotifySourceEnter(object source)
        {
            if (source == null) return;
            if (ReferenceEquals(sourceIdentity, source)
                || ReferenceEquals(pendingSource, source))
            {
                sourceHovered = true;
                return;
            }

            TryNotifyNestedSourceEnter(source);
        }

        public void InvalidateSource(object source)
        {
            if (source == null) return;
            if (ReferenceEquals(source, sourceIdentity)
                || ReferenceEquals(source, pendingSource))
            {
                Hide();
                return;
            }

            if (ReferenceEquals(source, pendingNestedSource))
            {
                ClearPendingNestedShow();
                return;
            }

            RemoveNestedSource(source);
        }

        public void Hide()
        {
            isPinned = false;
            ClearNested(preservePinnedChildren: false);
            HideLocal();
        }

        public Sprite GetGlossaryIcon(string id)
        {
            return TryGetGlossaryEntry(id, out var entry)
                ? entry.Icon
                : null;
        }

        public string GetGlossaryTitle(string id)
        {
            return HeroCombatStatusText.Name(id) ?? (TryGetGlossaryEntry(id, out var entry)
                ? entry.Title
                : string.Empty);
        }

        public void HideForSourceWithin(Transform root)
        {
            if (root == null || !ContainsSourceWithin(root))
            {
                return;
            }

            Hide();
        }

        public void BeginDragSuppression()
        {
            suppressForDrag = true;
            Hide();
        }

        public void EndDragSuppression()
        {
            suppressForDrag = false;
        }

        private void QueueShow(object source, Action show)
        {
            if (pendingNestedShow != null && !ReferenceEquals(pendingNestedSource, source))
                ClearPendingNestedShow();

            if (visible && ReferenceEquals(sourceIdentity, source))
            {
                sourceHovered = true;
                return;
            }

            // Rich unit cards own their status row. A status entered from that
            // row is a child tooltip source, never a replacement for the unit
            // card. Replacing the root here used to make the status icon's
            // OnDisable callback invalidate the newly displayed card while the
            // pointer was crossing the lower half of the rich tooltip.
            if (visible && IsRichUnitContentSource(source))
            {
                QueueNestedShow(source, show);
                return;
            }

            if (visible
                && (isPinned || HasPinnedNestedContent())
                && !ReferenceEquals(sourceIdentity, source))
            {
                // A pinned root or descendant is stable. A source entered from
                // inside its rectangle (status icon or glossary link) may open
                // a nested tooltip; an unrelated world/UI source leaves the
                // chain intact.
                if (IsPointerOverAnyTooltip())
                    QueueNestedShow(source, show);
                return;
            }

            if (pendingShow != null && ReferenceEquals(pendingSource, source))
            {
                sourceHovered = true;
                return;
            }

            pendingSource = source;
            pendingShow = show;
            pendingShownAt = Time.unscaledTime;
            sourceHovered = true;

            // A locked tooltip stays visible while a different source is only crossed
            // briefly. If the pointer remains there for hoverDelay, SetContent replaces
            // it normally below.
            if (visible && isLocked) return;

            isLocked = false;
            ClearNested();
            visible = false;
            EnsureCanvasGroup().alpha = 0f;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();
            // A root tooltip may have just shown a glossary entry from an
            // external rich-text source. The identity is valid for that card
            // only; normal status/skill/item hovers start a fresh branch.
            if (!isNested) glossaryEntryId = null;
        }

        private void QueueNestedShow(object source, Action show)
        {
            if (source == null || show == null) return;

            if (TryMarkNestedSourceHovered(source)) return;

            var owner = GetHoveredTooltip() ?? this;
            var root = GetRootTooltip();
            root.pendingNestedOwner = owner;
            root.pendingNestedSource = source;
            root.pendingNestedShow = show;
            root.pendingNestedShownAt = Time.unscaledTime;
        }

        private bool IsRichUnitContentSource(object source)
        {
            if (source is not Component component
                || richUnitContent == null
                || !richUnitContent.gameObject.activeInHierarchy)
            {
                return false;
            }

            return component.transform == richUnitContent.transform
                || component.transform.IsChildOf(richUnitContent.transform);
        }

        private UIWorkspaceTooltipView GetRootTooltip()
        {
            var root = this;
            while (root.glossaryParent != null) root = root.glossaryParent;
            return root;
        }

        private bool IsDescendantOf(UIWorkspaceTooltipView ancestor)
        {
            for (var current = this; current != null; current = current.glossaryParent)
                if (current == ancestor) return true;
            return false;
        }

        private int CountNestedViews()
        {
            var count = 1;
            foreach (var child in nested)
                if (child != null) count += child.CountNestedViews();
            return count;
        }

        private bool ContainsNestedView(UIWorkspaceTooltipView candidate)
        {
            if (candidate == null) return false;
            foreach (var child in nested)
            {
                if (child == null) continue;
                if (child == candidate || child.ContainsNestedView(candidate)) return true;
            }
            return false;
        }

        private bool TryNotifyNestedSourceExit(object source)
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child != null && child.TryNotifyNestedSourceExit(source)) return true;
            }

            if (!ReferenceEquals(sourceIdentity, source)
                && !ReferenceEquals(pendingSource, source)) return false;

            sourceHovered = false;
            PromoteVisibleTooltipToExitGracePeriod();
            return true;
        }

        private bool TryNotifyNestedSourceEnter(object source)
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child != null && child.TryNotifyNestedSourceEnter(source)) return true;
            }

            if (!ReferenceEquals(sourceIdentity, source)
                && !ReferenceEquals(pendingSource, source)) return false;

            sourceHovered = true;
            return true;
        }

        private bool TryMarkNestedSourceHovered(object source)
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child != null && child.TryMarkNestedSourceHovered(source)) return true;
            }

            if (!ReferenceEquals(sourceIdentity, source)
                && !ReferenceEquals(pendingSource, source)) return false;

            sourceHovered = true;
            return true;
        }

        private bool RemoveNestedSource(object source)
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child == null)
                {
                    nested.RemoveAt(index);
                    continue;
                }

                if (ReferenceEquals(child.sourceIdentity, source)
                    || ReferenceEquals(child.pendingSource, source))
                {
                    nested.RemoveAt(index);
                    GetRootTooltip().ClearReferencesToRemovedView(child);
                    DestroyNestedView(child);
                    GetRootTooltip().RefreshNestedOpacity();
                    return true;
                }

                if (child.RemoveNestedSource(source)) return true;
            }

            return false;
        }

        private void RemoveInvalidNestedViews()
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child == null)
                {
                    nested.RemoveAt(index);
                    continue;
                }

                child.RemoveInvalidNestedViews();
                if (!child.visible
                    || (child.visible && !IsSourceAlive(child.sourceIdentity))
                    || (child.pendingShow != null && !IsSourceAlive(child.pendingSource)))
                {
                    nested.RemoveAt(index);
                    GetRootTooltip().ClearReferencesToRemovedView(child);
                    DestroyNestedView(child);
                }
            }
        }

        private bool ContainsSourceWithin(Transform root)
        {
            if (IsWithin(root, sourceIdentity) || IsWithin(root, pendingSource)
                || IsWithin(root, pendingNestedSource)) return true;

            return nested.Any(child => child != null && child.ContainsSourceWithin(root));
        }

        private void ClearPendingNestedShow()
        {
            pendingNestedSource = null;
            pendingNestedShow = null;
            pendingNestedOwner = null;
        }

        private void ClearReferencesToRemovedView(UIWorkspaceTooltipView view)
        {
            if (view == null) return;
            if (pendingNestedOwner == view || view.ContainsNestedView(pendingNestedOwner))
                ClearPendingNestedShow();
            if (pendingLinkOwner == view || view.ContainsNestedView(pendingLinkOwner))
            {
                pendingLinkId = null;
                pendingLinkOwner = null;
            }
            if (pendingNestedExitOwner == view || view.ContainsNestedView(pendingNestedExitOwner))
                CancelNestedExitGracePeriod();
        }

        private void SetContent(
            Sprite sprite, string title, string body, string meta, bool reserveIconSlot = false) =>
            SetContentCore(sprite, title, body, meta, reserveIconSlot, true);

        private void SetContentCore(
            Sprite sprite,
            string title,
            string body,
            string meta,
            bool reserveIconSlot = false,
            bool linkifyBody = true,
            RarityTier? itemRarity = null)
        {
            body = UiTerms.Present(body);
            meta = UiTerms.Present(meta);

            if (contentTargetOverride != null && contentTargetOverride != this)
            {
                var target = contentTargetOverride;
                contentTargetOverride = null;
                target.SetContentCore(sprite, title, body, meta, reserveIconSlot, linkifyBody, itemRarity);
                return;
            }

            ItemRarityPresentation.ApplyBorder(ref itemRarityBorder, icon?.rectTransform, itemRarity, 0f);
            liveExplorationStatus = null;
            RestoreDefaultTooltipWidth();
            var isTitleOnly = sprite == null
                && string.IsNullOrWhiteSpace(body)
                && string.IsNullOrWhiteSpace(meta);
            if (npcLoyaltyProgression != null) npcLoyaltyProgression.gameObject.SetActive(false);
            isRichUnitContent = false;
            if (richUnitContent != null) richUnitContent.gameObject.SetActive(false);
            sourceIdentity = pendingSource;
            pendingSource = null;
            visible = true;
            isLocked = false;
            shownAt = Time.unscaledTime;
            lockedAt = -1f;
            hoveredLinkId = null;
            transform.parent?.SetAsLastSibling();
            transform.SetAsLastSibling();
            EnsureCanvasGroup().alpha = 1f;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();

            if (icon != null)
            {
                icon.sprite = sprite;
                // Many authored item definitions do not have an icon yet. Keep
                // the header's optional spacing without drawing Unity's white
                // default Image in the empty icon slot.
                icon.enabled = sprite != null;
                // The icon lives in a horizontal layout group with the title.
                // Disabling only its Image still left its LayoutElement active and
                // reserved a 40 px blank column for iconless tooltips.
                icon.gameObject.SetActive(sprite != null || reserveIconSlot);
            }

            if (titleText != null)
            {
                titleText.textWrappingMode = TextWrappingModes.Normal;
                titleText.overflowMode = TextOverflowModes.Overflow;
                titleText.alignment = isTitleOnly
                    ? TextAlignmentOptions.Center
                    : defaultTitleAlignment;
                titleText.text = title ?? string.Empty;
            }
            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
                bodyText.text = linkifyBody
                    ? LinkifyGlossary(body)
                    : body ?? string.Empty;
            }
            if (metaText != null)
            {
                metaText.gameObject.SetActive(!string.IsNullOrWhiteSpace(meta));
                metaText.text = LinkifyGlossary(meta);
            }

            if (reserveIconSlot || body?.Contains(ItemStatColumnsView.MarkerId) == true)
                ResizeItemTooltipWidthToContent();
            if (isTitleOnly) ResizeTitleOnlyTooltipWidth();

            ResizeForContent();
            resizeAfterLayout = true;
            PlaceAt(Input.mousePosition);
        }

        private void SetNpcLoyaltyContent(NpcLoyaltyTooltipData data)
        {
            SetContent(null, string.IsNullOrEmpty(data?.NpcName) ? "Lojalność" : $"Lojalność — {data.NpcName}", string.Empty, string.Empty);
            RefreshNpcLoyaltyContent(data);
        }

        private void RefreshNpcLoyaltyContent(NpcLoyaltyTooltipData data)
        {
            EnsureNpcLoyaltyProgression();
            if (npcLoyaltyProgression == null) return;

            var desiredWidth = NpcLoyaltyProgressionView.GetPreferredWidth(data, bodyText);
            SetTooltipWidth(ClampTooltipWidth(desiredWidth));
            Canvas.ForceUpdateCanvases();
            npcLoyaltyProgression.Bind(data, bodyText);
            npcLoyaltyProgression.gameObject.SetActive(true);
            ResizeForContent();
            resizeAfterLayout = true;
            PlaceAt(Input.mousePosition);
        }

        private float ClampTooltipWidth(float desiredWidth)
        {
            // Measure loyalty content against the canvas, not the transient tooltip layout.
            var canvas = tooltipRect?.GetComponentInParent<Canvas>()?.rootCanvas;
            var canvasRect = canvas?.GetComponent<RectTransform>();
            var availableWidth = canvasRect != null && canvasRect.rect.width > 1f
                ? Mathf.Max(1f, canvasRect.rect.width - 24f)
                : Mathf.Max(1f, desiredWidth);
            return Mathf.Min(Mathf.Max(1f, desiredWidth), availableWidth);
        }

        private void SetTooltipWidth(float width)
        {
            if (tooltipRect == null) return;
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void RestoreDefaultTooltipWidth()
        {
            if (defaultTooltipWidth > 1f) SetTooltipWidth(defaultTooltipWidth);
        }

        private void ResizeTitleOnlyTooltipWidth()
        {
            if (titleText == null) return;

            // Measure against an unconstrained line. Measuring with a width of
            // zero used TMP's previous narrow layout width, so a title that had
            // already wrapped reported the width of one word and made the next
            // tooltip even narrower.
            var preferredWidth = titleText.GetPreferredValues(
                titleText.text,
                10000f,
                0f).x;
            var desiredWidth = Mathf.Max(
                TitleOnlyTooltipMinimumWidth,
                Mathf.Ceil(preferredWidth) + TitleOnlyTooltipHorizontalPadding);
            SetTooltipWidth(ClampTooltipWidth(
                defaultTooltipWidth > 1f
                    ? Mathf.Min(desiredWidth, defaultTooltipWidth)
                    : desiredWidth));
        }

        private void EnsureNpcLoyaltyProgression()
        {
            if (npcLoyaltyProgression != null || bodyText == null) return;

            var parent = bodyText.transform.parent as RectTransform;
            if (parent == null) return;

            var progressionObject = new GameObject(
                "NpcLoyaltyProgression",
                typeof(RectTransform),
                typeof(NpcLoyaltyProgressionView));
            progressionObject.transform.SetParent(parent, false);

            npcLoyaltyProgression = progressionObject.GetComponent<NpcLoyaltyProgressionView>();
            npcLoyaltyProgression.Configure(bodyText, npcLoyaltyProgressSliderPrefab,
                npcLoyaltyGoldIcon, npcLoyaltyBarFrame, npcLoyaltyBarFill);
            progressionObject.SetActive(false);
        }

        private void EnsureTooltipFooter()
        {
            var contentRect = bodyText?.transform.parent as RectTransform;
            if (contentRect == null) return;

            tooltipFooter = contentRect.Find(TooltipFooterName) as RectTransform;
            if (tooltipFooter == null)
            {
                var footerObject = new GameObject(
                    TooltipFooterName,
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(HorizontalLayoutGroup));
                footerObject.transform.SetParent(contentRect, false);
                tooltipFooter = (RectTransform)footerObject.transform;
            }
            tooltipFooter.SetAsLastSibling();

            var footerLayout = tooltipFooter.GetComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 4f;
            footerLayout.childAlignment = TextAnchor.MiddleRight;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            var footerElement = tooltipFooter.GetComponent<LayoutElement>();
            footerElement.minHeight = 16f;
            footerElement.preferredHeight = 16f;
            footerElement.flexibleWidth = 1f;

            var reference = metaText != null && metaText.font != null
                ? metaText
                : bodyText != null && bodyText.font != null
                    ? bodyText
                    : titleText;

            tooltipFooterShortcut = tooltipFooter.Find("Shortcut")?.GetComponent<TextMeshProUGUI>();
            if (tooltipFooterShortcut == null)
            {
                var shortcutObject = new GameObject(
                    "Shortcut",
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(TextMeshProUGUI));
                shortcutObject.transform.SetParent(tooltipFooter, false);
                tooltipFooterShortcut = shortcutObject.GetComponent<TextMeshProUGUI>();
            }

            if (reference != null)
            {
                tooltipFooterShortcut.font = reference.font;
                tooltipFooterShortcut.fontSharedMaterial = reference.fontSharedMaterial;
                tooltipFooterShortcut.fontSize = Mathf.Max(10f, reference.fontSize * .72f);
            }
            tooltipFooterShortcut.text = "T —";
            tooltipFooterShortcut.alignment = TextAlignmentOptions.MidlineRight;
            tooltipFooterShortcut.textWrappingMode = TextWrappingModes.NoWrap;
            tooltipFooterShortcut.overflowMode = TextOverflowModes.Overflow;
            tooltipFooterShortcut.margin = Vector4.zero;
            tooltipFooterShortcut.raycastTarget = false;

            var shortcutElement = tooltipFooterShortcut.GetComponent<LayoutElement>();
            shortcutElement.minWidth = 24f;
            shortcutElement.preferredWidth = 24f;
            shortcutElement.minHeight = 16f;
            shortcutElement.preferredHeight = 16f;

            tooltipFooterLockGlyph = tooltipFooter.Find("LockGlyph")
                ?.GetComponent<UIWorkspaceTooltipLockGlyph>();
            if (tooltipFooterLockGlyph == null)
            {
                var glyphObject = new GameObject(
                    "LockGlyph",
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(UIWorkspaceTooltipLockGlyph));
                glyphObject.transform.SetParent(tooltipFooter, false);
                tooltipFooterLockGlyph = glyphObject.GetComponent<UIWorkspaceTooltipLockGlyph>();
            }

            tooltipFooterLockGlyph.raycastTarget = false;
            var glyphElement = tooltipFooterLockGlyph.GetComponent<LayoutElement>();
            glyphElement.minWidth = 14f;
            glyphElement.preferredWidth = 14f;
            glyphElement.minHeight = 14f;
            glyphElement.preferredHeight = 14f;
            UpdateTooltipFooterColor();
        }

        private void UpdateTooltipFooterColor()
        {
            var color = IsPinnedInChain
                ? pinnedAccentColor
                : new Color(.68f, .69f, .68f, .78f);
            if (tooltipFooterShortcut != null) tooltipFooterShortcut.color = color;
            if (tooltipFooterLockGlyph != null) tooltipFooterLockGlyph.color = color;
        }

        private void ResizeForContent()
        {
            if (tooltipRect == null) return;
            EnsureTooltipFooter();
            Canvas.ForceUpdateCanvases();
            var contentRect = bodyText?.rectTransform.parent as RectTransform;
            // Percentage column positions need the available panel width, rather
            // than TMP's preferred width for the labels alone.
            var contentLayout = contentRect?.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null) contentLayout.childForceExpandWidth = true;
            if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            bodyText?.ForceMeshUpdate();
            metaText?.ForceMeshUpdate();
            // Body can report a zero rect during the first frame after the
            // tooltip is enabled. Derive the available width from the fixed
            // tooltip width instead of letting GetPreferredValues wrap every
            // character into a very tall column.
            var tooltipWidth = Mathf.Max(1f, tooltipRect.rect.width);
            var contentWidth = contentRect != null && contentRect.rect.width > 1f
                ? contentRect.rect.width
                : Mathf.Max(1f, tooltipWidth - 24f);
            var iconHeight = icon != null && icon.gameObject.activeInHierarchy
                ? Mathf.Max(40f, LayoutUtility.GetPreferredHeight(icon.rectTransform)) : 0f;
            var titleWidth = titleText != null && titleText.rectTransform.rect.width > 1f
                ? titleText.rectTransform.rect.width
                : Mathf.Max(1f, contentWidth - (iconHeight > 0f ? iconHeight + 12f : 0f));
            var titleHeight = titleText != null
                ? Mathf.Ceil(titleText.GetPreferredValues(titleText.text, titleWidth, 0f).y)
                : 0f;
            var headerHeight = Mathf.Max(iconHeight, titleHeight);
            var headerLayout = titleText?.transform.parent?.GetComponent<LayoutElement>();
            if (headerLayout != null)
            {
                var compactHeaderHeight = Mathf.Max(24f, headerHeight + 8f);
                headerLayout.minHeight = compactHeaderHeight;
                headerLayout.preferredHeight = compactHeaderHeight;
                headerHeight = compactHeaderHeight;
            }

            var customBodyActive = npcLoyaltyProgression != null
                && npcLoyaltyProgression.gameObject.activeSelf;
            var richBodyActive = isRichUnitContent
                && richUnitContent != null
                && richUnitContent.gameObject.activeSelf;
            var hasBody = customBodyActive || richBodyActive
                || bodyText != null && bodyText.gameObject.activeSelf;
            var hasMeta = metaText != null && metaText.gameObject.activeSelf;
            var isTitleOnly = !customBodyActive && !richBodyActive
                && !hasBody
                && !hasMeta
                && (icon == null || !icon.isActiveAndEnabled);
            var bodyHeight = customBodyActive
                ? npcLoyaltyProgression.GetPreferredHeight(contentWidth)
                : richBodyActive
                    ? richUnitContent.GetPreferredHeight(contentWidth)
                : hasBody
                    ? Mathf.Ceil(bodyText.GetPreferredValues(bodyText.text, contentWidth, 0f).y)
                    : 0f;
            var metaHeight = hasMeta
                ? Mathf.Ceil(Mathf.Max(metaText.GetPreferredValues(metaText.text, contentWidth, 0f).y,
                    LayoutUtility.GetPreferredHeight(metaText.rectTransform)))
                : 0f;
            var hasFooter = tooltipFooter != null && tooltipFooter.gameObject.activeSelf;
            var footerHeight = hasFooter
                ? Mathf.Max(16f, LayoutUtility.GetPreferredHeight(tooltipFooter))
                : 0f;
            var sectionCount = 1 + (hasBody ? 1 : 0) + (hasMeta ? 1 : 0) + (hasFooter ? 1 : 0);
            // The authored content inset can exceed the legacy padding field.
            // Respect it so the vertical layout never compresses long item text.
            var verticalPadding = contentRect != null && contentRect.parent == tooltipRect
                && contentRect.anchorMin.y == 0 && contentRect.anchorMax.y == 1
                ? Mathf.Max(contentHeightPadding, contentRect.offsetMin.y - contentRect.offsetMax.y)
                : contentHeightPadding;
            tooltipRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                isTitleOnly
                    ? headerHeight + TitleOnlyTooltipVerticalPadding + footerHeight
                        + (hasFooter ? contentRect?.GetComponent<VerticalLayoutGroup>()?.spacing ?? 6f : 0f)
                    : Mathf.Max(minimumHeight, verticalPadding + headerHeight + bodyHeight + metaHeight
                        + footerHeight
                        + (contentRect?.GetComponent<VerticalLayoutGroup>()?.spacing ?? 6f) * (sectionCount - 1)));
            if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void ResizeItemTooltipWidthToContent()
        {
            if (tooltipRect == null) return;

            var titleWidth = titleText == null || string.IsNullOrWhiteSpace(titleText.text)
                ? 0f
                : titleText.GetPreferredValues(titleText.text, 10000f, 0f).x
                    + (icon != null && icon.gameObject.activeSelf
                        ? ItemTooltipIconAndSpacingWidth
                        : 0f)
                    + ItemTooltipTitleBuffer;
            var bodyWidth = bodyText == null || !bodyText.gameObject.activeSelf
                || string.IsNullOrWhiteSpace(bodyText.text)
                ? 0f
                : bodyText.GetPreferredValues(bodyText.text, 10000f, 0f).x;
            var metaWidth = metaText == null || !metaText.gameObject.activeSelf
                || string.IsNullOrWhiteSpace(metaText.text)
                ? 0f
                : metaText.GetPreferredValues(metaText.text, 10000f, 0f).x;
            var desiredWidth = Mathf.Ceil(
                Mathf.Max(titleWidth, bodyWidth, metaWidth)
                + ItemTooltipContentHorizontalPadding);
            var targetWidth = Mathf.Min(
                ItemTooltipMaximumWidth,
                Mathf.Max(ItemTooltipMinimumWidth, desiredWidth));
            SetTooltipWidth(ClampTooltipWidth(targetWidth));
        }

        private void HideLocal()
        {
            liveExplorationStatus = null;
            if (npcLoyaltyProgression != null) npcLoyaltyProgression.gameObject.SetActive(false);
            if (richUnitContent != null) richUnitContent.gameObject.SetActive(false);
            visible = false;
            sourceHovered = false;
            isLocked = false;
            isPinned = false;
            isRichUnitContent = false;
            sourceIdentity = null;
            pendingSource = null;
            pendingShow = null;
            pendingNestedSource = null;
            pendingNestedShow = null;
            pendingNestedOwner = null;
            resizeAfterLayout = false;
            hasPlacementScreenPosition = false;
            hoveredLinkId = null;
            pendingLinkId = null;
            pendingLinkOwner = null;
            pendingNestedExitOwner = null;
            glossaryEntryId = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();
        }

        private static bool IsWithin(Transform root, object source)
        {
            // Tooltip sources are often dynamically rebuilt inventory slots. Unity
            // keeps their managed wrapper briefly after Destroy, but accessing its
            // Transform throws MissingReferenceException.
            if (source is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            return source switch
            {
                Component component => component.transform == root
                    || component.transform.IsChildOf(root),
                GameObject gameObject => gameObject.transform == root
                    || gameObject.transform.IsChildOf(root),
                _ => false
            };
        }

        private static bool IsSourceAlive(object source)
        {
            if (source == null) return true;
            if (source is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            if (source is Component component)
            {
                return component != null
                    && component.gameObject != null
                    && component.gameObject.activeInHierarchy;
            }

            if (source is GameObject gameObject)
            {
                return gameObject != null && gameObject.activeInHierarchy;
            }

            return source is not UnityEngine.Object unityObject || unityObject != null;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            // Do not rely only on the cached serialized reference here. Minimal
            // test objects and older prefab instances may not have a CanvasGroup,
            // or may retain a stale component reference after a prefab change.
            // Resolve the component from the current GameObject before every use.
            var current = GetComponent<CanvasGroup>();
            if (current == null) current = gameObject.AddComponent<CanvasGroup>();
            canvasGroup = current;
            ApplyPointerInteraction();
            return canvasGroup;
        }

        private void ApplyPointerInteraction()
        {
            if (canvasGroup == null) return;

            var canReceivePointer = visible && isLocked;
            canvasGroup.blocksRaycasts = canReceivePointer;
            canvasGroup.interactable = canReceivePointer;

            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == lockProgressFill)
                {
                    graphic.raycastTarget = false;
                    continue;
                }

                graphic.raycastTarget = canReceivePointer;
            }
        }

        private void UpdateLockProgressVisual()
        {
            if (lockProgressFill != null)
            {
                var shouldShow = visible;
                if (lockProgressFill.gameObject.activeSelf != shouldShow)
                {
                    lockProgressFill.gameObject.SetActive(shouldShow);
                }

                if (shouldShow)
                {
                    var progressRect = lockProgressFill.rectTransform;
                    // This is the same lower-left origin used by the legacy combat tooltip.
                    // Do it at runtime as well, so a copied prefab cannot make the bar grow
                    // symmetrically around its centre.
                    progressRect.anchorMin = Vector2.zero;
                    progressRect.anchorMax = Vector2.zero;
                    progressRect.pivot = Vector2.zero;
                    progressRect.anchoredPosition = new Vector2(6f, 4f);
                    progressRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2f);

                    var duration = Mathf.Max(.01f, lockDelay);
                    var progress = isLocked
                        ? 1f
                        : Mathf.Min(.96f, Mathf.Clamp01((Time.unscaledTime - shownAt) / duration));
                    progressRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        lockProgressWidth * progress);
                }
            }

            CacheDefaultAccentColor();

            if (lockAccentGraphic != null)
            {
                lockAccentGraphic.color = IsPinnedInChain
                    ? pinnedAccentColor
                    : isLocked ? lockedAccentColor : defaultAccentColor;
            }

            var border = GetAuthoredBorderGraphic();
            if (border != null)
            {
                border.color = IsPinnedInChain
                    ? pinnedAccentColor
                    : ReferenceEquals(border, lockAccentGraphic)
                        ? isLocked ? lockedAccentColor : defaultAccentColor
                        : defaultBorderColor;
            }

            UpdateTooltipFooterColor();
        }

        private void RefreshNestedTooltip()
        {
            var owner = GetHoveredTooltip();
            if (owner == null) return;

            var id = owner.GetHoveredLinkId();
            var wasReturningToPendingNested = pendingNestedExitOwner == owner;
            if (owner != this) CancelNestedExitGracePeriod();

            if (id == owner.hoveredLinkId)
            {
                TryShowPendingNested(owner, id);
                return;
            }

            owner.hoveredLinkId = id;
            pendingLinkId = null;
            pendingLinkOwner = null;

            if (string.IsNullOrWhiteSpace(id) && HasNestedAfter(owner))
            {
                if (HasProtectedTooltipChain() && IsPointerOverAnyTooltip())
                {
                    CancelNestedExitGracePeriod();
                    return;
                }
                BeginNestedExitGracePeriod(owner);
                return;
            }

            CancelNestedExitGracePeriod();
            if (wasReturningToPendingNested)
            {
                return;
            }

            ClearNestedAfter(owner);

            if (!string.IsNullOrWhiteSpace(id)
                && TryGetGlossaryEntry(id, out _))
            {
                pendingLinkId = id;
                pendingLinkOwner = owner;
                pendingLinkShownAt = Time.unscaledTime;
            }
        }

        private void TryShowPendingNested(
            UIWorkspaceTooltipView owner,
            string id)
        {
            if (string.IsNullOrWhiteSpace(pendingLinkId)
                || pendingLinkOwner != owner
                || !string.Equals(pendingLinkId, id, StringComparison.Ordinal)
                || Time.unscaledTime - pendingLinkShownAt < nestedLinkDelay)
            {
                return;
            }

            if (TryGetGlossaryEntry(pendingLinkId, out var entry))
            {
                owner.CreateNestedTooltip(entry);
            }

            pendingLinkId = null;
            pendingLinkOwner = null;
        }

        private UIWorkspaceTooltipView GetHoveredTooltip()
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var child = nested[index];
                if (child == null || !child.visible) continue;
                var deepest = child.GetHoveredTooltip();
                if (deepest != null) return deepest;
                if (child.IsPointerOverTooltip())
                {
                    return child;
                }
            }

            return IsPointerOverTooltip() ? this : null;
        }

        private string GetHoveredLinkId()
        {
            return GetHoveredLinkId(titleText)
                ?? GetHoveredLinkId(bodyText)
                ?? GetHoveredLinkId(metaText);
        }

        private static string GetHoveredLinkId(TextMeshProUGUI text)
        {
            if (text == null || text.textInfo.linkCount == 0) return null;

            var canvas = text.canvas;
            var camera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            var index = TMP_TextUtilities.FindIntersectingLink(
                text,
                Input.mousePosition,
                camera);
            return index < 0 ? null : text.textInfo.linkInfo[index].GetLinkID();
        }

        private void CreateNestedTooltip(CombatTooltipGlossary.Entry entry)
        {
            if (entry == null) return;
            CreateNestedTooltip(
                entry,
                () => SetContent(entry.Icon, entry.Title, entry.Description, string.Empty),
                pinnedContent: false,
                glossaryId: entry.Id);
        }

        private void CreateNestedTooltip(
            object source,
            Action show,
            bool pinnedContent,
            string glossaryId = null)
        {
            CacheDefaultAccentColor();
            var clone = Instantiate(gameObject, transform.parent);
            var view = clone.GetComponent<UIWorkspaceTooltipView>();
            // The authored tooltip overrides canvas sorting. Clones must render
            // above earlier levels rather than sharing their sorting order.
            var root = GetRootTooltip();
            var rootCanvas = root.GetComponent<Canvas>();
            var nestedCanvas = clone.GetComponent<Canvas>();
            if (rootCanvas != null && nestedCanvas != null && rootCanvas.overrideSorting)
                nestedCanvas.sortingOrder = rootCanvas.sortingOrder + root.CountNestedViews();
            view.DisableRuntimeComposedContentInClone();
            view.EnsureTooltipFooter();
            view.isNested = true;
            view.defaultAccentColor = defaultAccentColor;
            view.hasDefaultAccentColor = hasDefaultAccentColor;
            view.defaultBorderColor = defaultBorderColor;
            view.hasDefaultBorderColor = hasDefaultBorderColor;
            view.pendingSource = source;
            view.glossaryParent = this;
            view.glossaryEntryId = glossaryId;
            view.visible = true;
            view.isLocked = false;
            view.sourceHovered = source != null;
            view.shownAt = Time.unscaledTime;
            view.lockedAt = -1f;
            view.gameObject.SetActive(true);
            view.EnsureCanvasGroup().alpha = 1f;
            var previousTarget = contentTargetOverride;
            contentTargetOverride = view;
            show?.Invoke();
            contentTargetOverride = previousTarget;
            view.isLocked = pinnedContent;
            view.sourceHovered = false;
            view.ApplyPointerInteraction();
            view.UpdateLockProgressVisual();
            view.PlaceAt(Input.mousePosition);
            nested.Add(view);
            ActiveInstance = root;
            root.RefreshNestedOpacity();
        }

        private void DisableRuntimeComposedContentInClone()
        {
            // Instantiate(gameObject) also clones runtime-created rich/status and
            // NPC loyalty children. Their private references are not serialized,
            // so SetContentCore cannot discover them on the clone. Disable every
            // composed child before the nested view lays out its own content.
            foreach (var rich in GetComponentsInChildren<UIWorkspaceRichUnitTooltipView>(true))
                rich.gameObject.SetActive(false);
            foreach (var loyalty in GetComponentsInChildren<NpcLoyaltyProgressionView>(true))
                loyalty.gameObject.SetActive(false);
            richUnitContent = null;
            npcLoyaltyProgression = null;
            isRichUnitContent = false;
        }

        private bool TryGetGlossaryEntry(
            string id,
            out CombatTooltipGlossary.Entry result)
        {
            if (!string.IsNullOrEmpty(id) && GetRootTooltip().calculationEntries.TryGetValue(id,out result)) return true;
            if (TryGetGalduryteEffectEntry(id, out result)) return true;
            if (glossary != null && glossary.TryGet(id, out result)) return true;
            if (glossary == null && CombatTooltipGlossaryFallback.TryGet(id, out result)) return true;

            result = null;
            return false;
        }

        private void ClearNestedAfter(UIWorkspaceTooltipView owner, bool preservePinnedChildren = true)
        {
            if (pendingLinkOwner != null && pendingLinkOwner != owner)
            {
                pendingLinkId = null;
                pendingLinkOwner = null;
            }

            if (owner == null) return;
            if (pendingNestedExitOwner != null
                && (pendingNestedExitOwner == owner || owner.ContainsNestedView(pendingNestedExitOwner)))
            {
                CancelNestedExitGracePeriod();
            }
            if (pendingNestedOwner != null
                && (pendingNestedOwner == owner || owner.ContainsNestedView(pendingNestedOwner)))
            {
                ClearPendingNestedShow();
            }

            owner.ClearNestedChildren(preservePinnedChildren);
            GetRootTooltip().RefreshNestedOpacity();
        }

        private void ClearNested(bool preservePinnedChildren = true) =>
            ClearNestedAfter(this, preservePinnedChildren);

        private void ClearNestedChildren(bool preservePinnedChildren)
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                var view = nested[index];
                if (view == null)
                {
                    nested.RemoveAt(index);
                    continue;
                }

                if (preservePinnedChildren && view.HasProtectedTooltipChain()) continue;

                nested.RemoveAt(index);
                DestroyNestedView(view);
            }
        }

        private static void DestroyNestedView(UIWorkspaceTooltipView view)
        {
            if (view == null) return;
            for (var index = view.nested.Count - 1; index >= 0; index--)
            {
                var child = view.nested[index];
                view.nested.RemoveAt(index);
                DestroyNestedView(child);
            }

            view.HideLocal();
            Destroy(view.gameObject);
        }

        private bool HasPinnedNestedContent()
        {
            return nested.Any(view => view != null && view.HasPinnedTooltipChain());
        }

        private bool HasNestedAfter(UIWorkspaceTooltipView owner)
        {
            return owner != null && owner.nested.Any(view => view != null && view.visible);
        }

        private void BeginNestedExitGracePeriod(UIWorkspaceTooltipView owner)
        {
            pendingNestedExitOwner = owner;
            pendingNestedExitStartedAt = Time.unscaledTime;
        }

        private void CancelNestedExitGracePeriod()
        {
            pendingNestedExitOwner = null;
            pendingNestedExitStartedAt = 0f;
        }

        private void ExpireNestedExitGracePeriod()
        {
            if (pendingNestedExitOwner == null
                || Time.unscaledTime - pendingNestedExitStartedAt < ExitGracePeriod)
            {
                return;
            }

            var owner = pendingNestedExitOwner;
            CancelNestedExitGracePeriod();
            ClearNestedAfter(owner);
        }

        private void CacheDefaultAccentColor()
        {
            if (!hasDefaultAccentColor && lockAccentGraphic != null)
            {
                defaultAccentColor = lockAccentGraphic.color;
                hasDefaultAccentColor = true;
            }

            var border = GetAuthoredBorderGraphic();
            if (!hasDefaultBorderColor && border != null)
            {
                defaultBorderColor = border.color;
                hasDefaultBorderColor = true;
            }
        }

        private Graphic GetAuthoredBorderGraphic()
        {
            if (authoredBorderGraphic != null) return authoredBorderGraphic;

            var border = transform.Find("Border");
            authoredBorderGraphic = border?.GetComponent<Graphic>();
            return authoredBorderGraphic;
        }

        private void RefreshNestedOpacity()
        {
            var views = new List<UIWorkspaceTooltipView>();
            CollectNestedViews(views);

            for (var index = 0; index < views.Count; index++)
            {
                var distance = views.Count - 1 - index;
                views[index].EnsureCanvasGroup().alpha = distance switch
                {
                    <= 1 => 1f,
                    2 => .75f,
                    3 => .5f,
                    _ => .25f
                };
            }
        }

        private void CollectNestedViews(List<UIWorkspaceTooltipView> views)
        {
            if (views == null) return;
            views.Add(this);
            foreach (var child in nested)
                if (child != null) child.CollectNestedViews(views);
        }

        private bool IsPointerOverAnyTooltip()
        {
            if (IsPointerOverTooltip()) return true;

            foreach (var tooltip in nested)
            {
                if (tooltip != null && tooltip.IsPointerOverAnyTooltip()) return true;
            }

            return false;
        }

        private bool IsPointerOverTooltip()
        {
            return IsPointerOverTooltip(Input.mousePosition);
        }

        private bool IsPointerOverTooltip(Vector2 screenPosition)
        {
            var canvas = tooltipRect != null
                ? tooltipRect.GetComponentInParent<Canvas>()?.rootCanvas
                : richUnitContent?.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            if (tooltipRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(tooltipRect, screenPosition, camera))
            {
                return true;
            }

            // Runtime rich content is inserted below the authored body layout.
            // During the first layout pass its child rect can be one frame ahead
            // of the parent panel rect. Keep the complete rich card as part of
            // the tooltip chain so crossing the status heading/row cannot be
            // mistaken for leaving the tooltip.
            return richUnitContent != null
                && richUnitContent.gameObject.activeInHierarchy
                && richUnitContent.transform is RectTransform richRect
                && RectTransformUtility.RectangleContainsScreenPoint(richRect, screenPosition, camera);
        }

        private static string FormatStatusTitle(
            string effectId,
            string title,
            string displayName = null)
        {
            if (effectId == "mage.tiring") displayName = "Wycieńczające Zaklęcia";
            if (!string.IsNullOrWhiteSpace(displayName)
                && !string.Equals(displayName, title, StringComparison.OrdinalIgnoreCase))
            {
                // A skill, aura or grouped effect can provide a useful typed
                // label. Keep that label as the title; the status glossary is
                // linked from the body instead of making an unrelated title
                // look like the status name itself.
                return displayName;
            }

            var displayedTitle = string.IsNullOrWhiteSpace(displayName)
                ? string.IsNullOrWhiteSpace(title) ? effectId : title
                : displayName;
            return $"<link=\"{effectId}\"><color=#7EDBFF>{displayedTitle}</color></link>";
        }

        private void PlaceAt(Vector2 screenPosition)
        {
            if (tooltipRect == null || tooltipRect.parent is not RectTransform parent) return;
            placementScreenPosition = screenPosition;
            hasPlacementScreenPosition = true;
            var canvas = tooltipRect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out var pointer)) return;

            Canvas.ForceUpdateCanvases();
            var size = tooltipRect.rect.size;
            var pivot = new Vector2(0f, 1f);
            var offset = pointerOffset;
            if (pointer.x + offset.x + size.x > parent.rect.xMax) { pivot.x = 1f; offset.x = -offset.x; }
            if (pointer.y + offset.y - size.y < parent.rect.yMin) { pivot.y = 0f; offset.y = -offset.y; }
            tooltipRect.pivot = pivot;
            var anchor = new Vector2(
                Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, tooltipRect.anchorMin.x),
                Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, tooltipRect.anchorMin.y));
            var requested = pointer + offset;
            var min = parent.rect.min + Vector2.Scale(size, pivot);
            var max = parent.rect.max - Vector2.Scale(size, Vector2.one - pivot);
            tooltipRect.anchoredPosition = new Vector2(
                Mathf.Clamp(requested.x, min.x, max.x),
                Mathf.Clamp(requested.y, min.y, max.y)) - anchor;
        }

        public void ShowGlossaryEntry(string id, object source)
        {
            if (!TryGetGlossaryEntry(id, out var entry)) return;
            QueueShow(source ?? entry, () =>
            {
                // Set the identity before SetContent links the body so the
                // entry card cannot immediately link to itself.
                glossaryEntryId = entry.Id;
                SetContent(entry.Icon, HeroCombatStatusText.Name(entry.Id, entry.Title), entry.Description, string.Empty);
            });
        }

        private string LinkifyGlossary(string text)
        {
            return glossary != null
                ? glossary.Linkify(text ?? string.Empty, glossaryEntryId)
                : CombatTooltipGlossary.LinkifyFallback(text ?? string.Empty, glossaryEntryId);
        }

        public void ShowLoadoutSkillDetails(SkillData skill, CharacterInstance character, object source, string rankMetadata)
        {
            if (suppressForDrag || skill == null) return;
            QueueShow(source ?? skill, () => SetContentCore(skill.Icon, skill.DisplayName,
                BuildLoadoutSkillDetails(skill, character),
                BuildLoadoutSkillCost(skill, character) + (string.IsNullOrWhiteSpace(rankMetadata) ? string.Empty : "\n" + rankMetadata),
                false, false));
        }

        public string BuildLoadoutSkillDetails(SkillData skill, CharacterInstance character)
        {
            if (skill == null) return string.Empty;
            var caster = character == null ? null : new BattleUnit { Character = character };
            var result = new StringBuilder();
            var targeting = new StringBuilder();
            AppendSkillTargetingSummary(targeting, skill, caster);
            if (targeting.Length > 0) result.Append(FormatSkillTargetingSummary(new StringBuilder(GeometryValues(targeting.ToString(),skill,caster)))).Append("\n\n");
            result.Append(AccuracyHeader(skill,caster));
            if(skill.IsToggle)result.Append("<size=85%>Włączana/wyłączana</size>\n\n");
            result.Append("<b>Efekty</b>\n");
            foreach (var description in BuildSkillMechanics(skill, caster))
                result.Append("• ").Append(description).Append('\n');
            var body = result.ToString().TrimEnd();
            return LinkifyGlossary(UiTerms.Present(body));
        }

        private static bool CanUseGeneratedSkillSummary(SkillData skill)
        {
            // Mechanics have one source: runtime skill data. Authored copy is a
            // fallback for incomplete/custom data, never a second mechanics block.
            return !string.IsNullOrWhiteSpace(skill?.SkillId) && skill.Effects?.Count > 0
                && skill.Effects.All(effect => effect != null && Enum.IsDefined(typeof(EffectType), effect.EffectType));
        }

        private IEnumerable<string> BuildSkillMechanics(SkillData skill, BattleUnit caster)
        {
            var effects = skill.Effects ?? Array.Empty<SkillEffectData>();
            if (skill.SkillId == WarriorCombatSystem.DeathWish)
            {
                var value=effects.FirstOrDefault(e=>e.EffectType==EffectType.ModifyVar);
                value=PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,value);
                yield return $"Zadawane obrażenia: +{LinkEffectParameter(skill,caster,value,"modifier",FormatPercent(value?.ModifierValue ?? .5f))}. Otrzymywane obrażenia: +{LinkEffectParameter(skill,caster,PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,effects.Last(e=>e.EffectType==EffectType.ModifyVar)),"modifier",FormatPercent(value?.ModifierValue ?? .5f))}. Śmiertelne trafienie pozostawia {BaseValue("1","Pragnienie Śmierci — pozostawione zdrowie")} HP. Wygasa przy braku furii na podtrzymanie.";
                yield break;
            }
            if (!CanUseGeneratedSkillSummary(skill) && !string.IsNullOrWhiteSpace(skill.TooltipDescription))
            {
                yield return UiTerms.Present(skill.TooltipDescription.Trim(), SkillResource(skill, caster?.Character?.ResourcePool?.ResourceType ?? ResourceType.None));
            }
            // These object behaviours are implemented by dedicated systems, not
            // CreateObject's generic payload. Keep their complete rule exactly once.
            else if (skill.SkillId is "skill.enemy.catacombs.dead_king_vengeance"
                or Godmist.Systems.Combat.SkeletonSlayerBossSystem.DefenseId
                or Godmist.Systems.Combat.SkeletonSlayerBossSystem.HammerId
                or Godmist.Systems.Combat.SkeletonSlayerBossSystem.TaskId
                or Godmist.Systems.Combat.DeadKingBossSystem.ShadowId
                && !string.IsNullOrWhiteSpace(skill.TooltipDescription))
                yield return UiTerms.Present(skill.TooltipDescription.Trim(), SkillResource(skill, caster?.Character?.ResourcePool?.ResourceType ?? ResourceType.None));
            else
            {
                var commonTarget = effects.Where(effect => effect != null)
                    .Select(effect => effect.TargetModeIndex).Distinct().Count() == 1;
                var descriptions = effects.Where(effect => effect != null).Select(effect => DescribeEffect(skill, caster,
                    caster?.Character == null ? effect : PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, effect), !commonTarget));
                foreach (var group in descriptions.Where(text => !string.IsNullOrWhiteSpace(text)).GroupBy(text => text))
                    yield return group.Count() > 1 ? $"{BaseValue(group.Count().ToString(),"Liczba powtórzeń efektu")} × {group.Key}" : group.Key;
            }
            if (skill.GuaranteedCritical) yield return "Trafienie jest zawsze krytyczne.";
            if (!Mathf.Approximately(skill.PointBlankDamageMultiplier, 1f))
                yield return $"W zwarciu zadaje {BaseValue(FormatPercent(skill.PointBlankDamageMultiplier),"Modyfikator obrażeń w zwarciu")} obrażeń.";
            if (!string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId))
                yield return $"Warunek użycia: rzucający ma {GetStatusDisplayName(skill.RequiredCasterStatusId)}.";
            if (skill.CanRecoverKnives) yield return $"Odzyskuje wskazany własny sztylet i przywraca {BaseValue(ScoutCombatSystem.KnifeRecoveryMomentum.ToString(),"Impet za odzysk sztyletu")} impetu.";
            var special = skill.SkillId switch
            {
                "skill.scout.ricochet" => DescribeRicochetCritical(skill,caster),
                MageTiringSpellSystem.SkillId => TiringValues(),
                WarriorDischargeRules.SkillId => $"Każdy wydany punkt furii zwiększa obrażenia o {DischargeValue(caster)}." + ((caster?.Character?.ResourcePool?.CurrentValue ?? 20)<20?$" Podgląd przy {BaseValue("20","Minimalna furia dla Wyładowania")} furii; za mało furii.":""),
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(special)) yield return special;
            if (skill.SkillId == WarriorDischargeRules.SkillId && caster != null)
            {
                var refund = Mathf.Clamp(WarriorCombatSystem.B(caster, "wounding"), 0, 2) * .25f;
                var ignore = Mathf.Clamp(WarriorCombatSystem.B(caster, "larger_wound"), 0, 2) * .2f;
                if (refund > 0) yield return $"Zabicie celu: zwrot {TalentPercent(caster,"Zwrot furii po zabiciu",0,refund,"node.warrior.brawler.wounding")} zużytej furii.";
                if (ignore > 0) yield return $"Ignoruje {TalentPercent(caster,"Ignorowanie obrony fizycznej",0,ignore,"node.warrior.brawler.larger_wound")} obrony fizycznej; {BaseValue(FormatPercent(ignore*2),"Ignorowanie obrony od progu furii")} przy zużyciu co najmniej {BaseValue("50","Próg furii — ignorowanie obrony")} furii.";
            }
        }

        public static string BuildLoadoutSkillCost(SkillData skill, CharacterInstance character)
        {
            if (skill == null) return string.Empty;
            return UiTerms.Present(BuildSkillMeta(skill,0,character==null?null:new BattleUnit {Character=character}));
        }

        private string BuildSkillDescription(SkillData skill, BattleUnit caster)
        {
            var generated = new StringBuilder();
            var targeting = new StringBuilder();
            // Consumables use the same transient SkillData pipeline as combat
            // skills, but their range is an implementation detail of the target
            // picker. Showing it in the item tooltip is noisy and misleading.
            if (!IsRuntimeConsumable(skill))
            {
                AppendSkillTargetingSummary(targeting, skill, caster);
            }

            generated.Append(AccuracyHeader(skill,caster));
            if(skill.IsToggle)generated.Append("<size=85%>Włączana/wyłączana</size>\n\n");
            foreach (var description in BuildSkillMechanics(skill, caster)) Append(generated, description);

            return UiTerms.Present(targeting.Length > 0
                ? FormatSkillTargetingSummary(new StringBuilder(GeometryValues(targeting.ToString(),skill,caster))) + "\n\n" + generated
                : generated.ToString());
        }

        private static string FormatSkillTargetingSummary(StringBuilder targeting) =>
            $"<size=85%>{targeting}</size>";

        public static ResourceType SkillResource(SkillData skill, ResourceType fallback = ResourceType.None)
        {
            if (IsRuntimeConsumable(skill)) return ResourceType.None;
            if (fallback != ResourceType.None) return fallback;
            if (skill?.ResourceType != ResourceType.None && skill != null) return skill.ResourceType;
            var id = skill?.SkillId ?? string.Empty;
            if (id.StartsWith("skill.warrior.", StringComparison.Ordinal)) return ResourceType.Rage;
            if (id.StartsWith("skill.scout.", StringComparison.Ordinal)) return ResourceType.Momentum;
            if (id.StartsWith("skill.mage.", StringComparison.Ordinal) || id.StartsWith("skill.paladin.", StringComparison.Ordinal)) return ResourceType.Mana;
            return ResourceType.None;
        }

        private static ResourceType EffectResource(SkillData skill, SkillEffectData effect, BattleUnit caster)
        {
            if (IsRuntimeConsumable(skill)) return ResourceType.None;
            // An ally/enemy can use a different resource from the caster.
            var mode = skill?.TargetModes != null && effect.TargetModeIndex >= 0 && effect.TargetModeIndex < skill.TargetModes.Count
                ? skill.TargetModes[effect.TargetModeIndex] : null;
            return mode?.TargetType == TargetType.Caster
                ? SkillResource(skill, caster?.Character?.ResourcePool?.ResourceType ?? ResourceType.None) : ResourceType.None;
        }

        private static bool IsRuntimeConsumable(SkillData skill)
        {
            return skill != null
                && skill.SkillId != null
                && skill.SkillId.StartsWith(
                    "runtime.consumable.",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSkillMeta(SkillData skill, int actionCost, BattleUnit caster)
        {
            string Link(string value,string title,string body)=>ActiveInstance!=null?ActiveInstance.LinkCalculation(value,title,body):value;
            var percentage=skill.ActionCost*UniqueEquipmentCombatRules.OrderCostMultiplier(caster,skill);
            var cost=caster?.MaxActionPoints>0?Godmist.Runtime.Combat.ActionPointUnits.Format(actionCost)+" PA":percentage.ToString("0.##",UiTerms.PolishCulture)+"% PA";
            var result="Koszt: "+Link(cost,"Koszt PA",ActionCostDetails(skill,caster,cost,false));
            var resourceTrace=new CombatValueExplanation();
            var resourceCost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill,explanation:resourceTrace);
            if(skill.SkillId==WarriorDischargeRules.SkillId)
                result+="  •  Furia: "+Link("20–100","Koszt furii",Row("Aktualna furia",(caster?.Character?.ResourcePool?.CurrentValue??0).ToString())+Row("Minimalna furia",WarriorDischargeRules.MinimumRage.ToString())+Row("Maksymalna furia",WarriorDischargeRules.MaximumRage.ToString())+Row("Koszt końcowy",resourceCost.ToString()));
            else if(skill.SkillId=="skill.warrior.shout")result+="  •  Furia: "+Link("100%","Koszt furii",Row("Udział aktualnej furii","100%")+Row("Koszt końcowy",resourceCost.ToString()));
            else if(resourceCost>0)result+=$"  •  {CombatResourceColors.ResourceName(SkillResource(skill,caster?.Character?.ResourcePool?.ResourceType ?? ResourceType.None))}: "+Link(resourceCost.ToString(),"Koszt "+UiTerms.Resource(SkillResource(skill,caster?.Character?.ResourcePool?.ResourceType ?? ResourceType.None),genitive:true),ResourceCostDetails(skill,caster,resourceTrace));
            if(skill.CanRecoverKnives)result+="\n"+BuildKnifeRecoveryCost(skill,caster);
            if(skill.KnifeCost>0)
            {
                result+="\nSztylety: "+Link(skill.KnifeCost.ToString(),"Zużycie sztyletów",Row("Bazowe zużycie",skill.KnifeCost.ToString())+(skill.FreeKnivesInMelee?Row("Zużycie w zwarciu","0"):"")+(.01f*ScoutCombatSystem.Points(caster,ScoutCombatSystem.BladeTree)>0?Row("Szansa zachowania sztyletów",FormatPercent(.01f*ScoutCombatSystem.Points(caster,ScoutCombatSystem.BladeTree))):""));
                if(skill.FreeKnivesInMelee)result+=" na dystans • W zwarciu: bez zużycia";
            }
            var upkeep=ToggleSkillUpkeepCalculator.Calculate(skill,caster?.Character?.ResourcePool);
            if(skill.IsToggle && upkeep>0)
                result+="\nPodtrzymanie: "+Link(upkeep.ToString(),"Podtrzymanie",Row("Koszt stały",skill.ToggleUpkeepFlatResourceCost.ToString())
                    +Row("Udział maksymalnego zasobu",FormatPercent(skill.ToggleUpkeepMaxResourcePercent))+Row(UiTerms.Stat(CharacterStatType.MaxResource,caster?.Character?.ResourcePool?.ResourceType??ResourceType.None),(caster?.Character?.ResourcePool?.MaxValue??0).ToString())+Row("Koszt końcowy",upkeep.ToString()))
                    +" "+UiTerms.Resource(SkillResource(skill,caster?.Character?.ResourcePool?.ResourceType ?? ResourceType.None),genitive:true)+" na turę";
            return result;
        }

        private static string BuildKnifeRecoveryCost(SkillData skill, BattleUnit caster)
        {
            string Link(string value,string title,string body)=>ActiveInstance!=null?ActiveInstance.LinkCalculation(value,title,body):value;
            var ap=caster?.MaxActionPoints>0?Godmist.Runtime.Combat.ActionPointUnits.Format(CombatActionCostCalculator.CalculateSkillCost(caster,skill,true))+" PA"
                :ScoutCombatSystem.KnifeRecoveryCost(caster,skill,skill.ActionCost,true)+"% PA";
            var trace=new CombatValueExplanation();var resource=PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill,true,trace);
            return "Odzysk sztyletu: "+Link(ap,"Koszt PA — odzysk sztyletu",ActionCostDetails(skill,caster,ap,true))+"  •  "+CombatResourceColors.ResourceName(skill.ResourceType)+": "
                +Link(resource.ToString(),"Koszt zasobu — odzysk sztyletu",ResourceCostDetails(skill,caster,trace,true));
        }

        public Sprite GetStatusIcon(CombatStatusPresentation presentation)
        {
            var character = presentation.Owner?.Character ?? presentation.ExplorationCharacter;
            var knownSkills = character?.GetKnownSkills() ?? Array.Empty<SkillData>();
            var catalogSkills = GameBootstrap.Instance?.ContentCatalog?.SkillDefinitions;
            var catalogClasses = GameBootstrap.Instance?.ContentCatalog?.CharacterClasses;
            var trees = (character?.ClassData?.SkillTrees ?? new List<SkillTreeDefinition>())
                .Concat(catalogClasses?.Where(definition => definition != null).SelectMany(definition => definition.SkillTrees)
                    ?? Enumerable.Empty<SkillTreeDefinition>());
            return CombatStatusPresentationFactory.ResolveIcon(presentation, GetGlossaryIcon,
                catalogSkills != null ? knownSkills.Concat(catalogSkills) : knownSkills, trees);
        }

        private void AppendSkillTargetingSummary(
            StringBuilder result,
            SkillData skill, BattleUnit caster)
        {
            if (skill?.TargetModes == null || skill.TargetModes.Count == 0)
            {
                return;
            }

            var minimumRange = int.MaxValue;
            var maximumRange = 0;
            var hasTarget = false;
            var hasAreaTarget = false;

            foreach (var authoredMode in skill.TargetModes)
            {
                var mode = PassiveEffectCombatExecutionSystem.ResolveMode(caster, skill, authoredMode);
                if (mode == null
                    || mode.TargetType == TargetType.Caster
                    || mode.TargetType == TargetType.All)
                {
                    continue;
                }

                hasTarget = true;
                hasAreaTarget |= mode.TargetType == TargetType.AreaAroundCaster
                    || mode.TargetType == TargetType.AreaAroundTile
                    || mode.TargetType == TargetType.Line
                    || mode.TargetType == TargetType.Chain
                    || mode.TargetType == TargetType.MultipleChosen
                    || mode.TargetType == TargetType.MultipleRandom;

                if (mode.AbsoluteRange == null) continue;
                minimumRange = Mathf.Min(minimumRange, mode.AbsoluteRange.MinRange);
                maximumRange = Mathf.Max(maximumRange, mode.AbsoluteRange.MaxRange);
            }

            var commonModes = (skill.Effects ?? Array.Empty<SkillEffectData>()).Where(effect => effect != null)
                .Select(effect => effect.TargetModeIndex).Distinct().ToArray();
            if (!hasTarget)
            {
                var target = commonModes.Length == 1 ? GetEffectTargetDescription(skill, commonModes[0], caster) : string.Empty;
                if (!string.IsNullOrWhiteSpace(target)) Append(result, "Cel: " + target + ".");
                return;
            }

            var deliveryType = hasAreaTarget
                ? "obszarowy"
                : maximumRange <= 1
                    ? "wręcz"
                    : "zasięgowy";
            var rangeText = minimumRange == int.MaxValue
                ? string.Empty
                : $"Zasięg: {FormatRange(minimumRange, maximumRange)} ({deliveryType}).";
            Append(result, rangeText);

            if (skill.RequiresLineOfSight)
            {
                Append(result, "Wymaga linii wzroku.");
            }
            if (commonModes.Length == 1)
            {
                var target = GetEffectTargetDescription(skill, commonModes[0], caster);
                if (!string.IsNullOrWhiteSpace(target)) Append(result, $"Cel: {target}.");
            }
        }

        private string DescribeEffect(
            SkillData skill,
            BattleUnit caster,
            SkillEffectData effect,
            bool includeTarget = true)
        {
            var reviewed=DescribeReviewedEffect(skill,caster,effect);
            if(reviewed!=null)return includeTarget?reviewed+" Cel: "+GeometryValues(GetEffectTargetDescription(skill,effect.TargetModeIndex,caster),skill,caster)+".":reviewed;
            var description = effect.EffectType switch
            {
                EffectType.PaladinHeal => $"Przywraca {FormatPercent(effect.DamageMultiplier*(1-PaladinCombatSystem.HealingVariation))}–{FormatPercent(effect.DamageMultiplier*(1+PaladinCombatSystem.HealingVariation))} siły leczenia (średniego ataku), z premiami Kapłana.",
                EffectType.PaladinRegeneration => "Przywraca łącznie 40% maksymalnego HP w ciągu 4 tur; Pośpiech oddaje część tej samej sumy wcześniej.",
                EffectType.PaladinBlessing => "Przez 4 tury zwiększa zadawane i zmniejsza otrzymywane obrażenia o 10/15/20%, zależnie od rangi Silnego Błogosławieństwa.",
                EffectType.MageSacrifice => "Wybrani sojusznicy zadają o 20% mniej obrażeń przez 3 tury; rzucający otrzymuje +20% obrażeń za każdego z nich. Ponowne użycie zastępuje poprzednie poświęcenie. Wskaż cele osobno; Enter zatwierdza mniejszą liczbę, Esc anuluje.",
                EffectType.MageRitual => "Poświęca 10% maksymalnego HP każdego sojusznika (pozostawia co najmniej 1 HP), następnie regeneruje łącznie 30% jego maksymalnego HP przez 5 tur.",
                EffectType.MageFireAura => "Przez 3 tury utrzymuje wokół celu aurę ognia o zasięgu 1. " + DescribeStatus(caster, effect),
                EffectType.DealDamage => DescribeDamage(skill, caster, effect),
                EffectType.Heal when effect.HealAverageAttackMultiplier > 0f => caster != null
                    ? $"Przywraca {CombatRecoverySystem.CalculateSkillHealing(caster, effect)} zdrowia."
                    : $"Przywraca {FormatPercent(effect.HealAverageAttackMultiplier)} średniego ataku jako zdrowie."
                        + (effect.BaseValue > 0 ? $" Dodatkowo {effect.BaseValue} zdrowia." : ""),
                EffectType.Heal => effect.BaseValue > 0
                    ? $"Przywraca {effect.BaseValue} zdrowia."
                    : "Przywraca zdrowie.",
                EffectType.HealMissingHealthPercent =>
                    $"Przywraca {FormatPercent(effect.PercentageValue)} brakującego zdrowia.",
                EffectType.MarkWeakPoint =>
                    $"{FormatPercent(effect.BaseChance)} szansy na oznaczenie słabego punktu{FormatEffectDuration(skill, effect)}. Ataki przeciw celowi mają +10 punktów procentowych szansy na kryt i +{FormatPercent(.2f + .05f * ScoutCombatSystem.AssassinRank(caster, "critical_point"))} obrażeń krytycznych.",
                EffectType.MarkForDestruction =>
                    $"{FormatPercent(effect.BaseChance)} szansy na oznaczenie celu{FormatEffectDuration(skill, effect)}. Otrzymuje od drużyny rzucającego o {FormatPercent(.2f + .05f * WarriorCombatSystem.C(caster, "breakdown"))} więcej obrażeń.",
                EffectType.ProtectAlly =>
                    $"Zmniejsza obrażenia otrzymywane przez cel o 30% oraz o 10 punktów procentowych za każdego sąsiadującego sojusznika, maksymalnie o 60%{FormatEffectDuration(skill, effect)}.",
                EffectType.GrantFreeMovement => caster != null && WarriorCombatSystem.C(caster, "strategy") == 0
                    ? string.Empty : "Ze Strategią: drużyna otrzymuje darmowy ruch na 3 tury.",
                EffectType.RestoreResource => effect.BaseValue > 0
                    ? $"Przywraca {effect.BaseValue} " +
                      $"{UiTerms.Resource(EffectResource(skill, effect, caster), genitive: true)}."
                    : $"Przywraca " +
                      $"{UiTerms.Resource(EffectResource(skill, effect, caster), accusative: true)}.",
                EffectType.HealPercent =>
                    $"Przywraca {FormatPercent(effect.PercentageValue)} maksymalnego zdrowia.",
                EffectType.RestoreResourcePercent =>
                    $"Przywraca {FormatPercent(effect.PercentageValue)} {(EffectResource(skill, effect, caster) is ResourceType.Mana or ResourceType.Rage ? "maksymalnej" : "maksymalnego")} " +
                    $"{UiTerms.Resource(EffectResource(skill, effect, caster), genitive: true)}.",
                EffectType.HealOverTimePercent =>
                    $"Regeneruje {FormatPercent(effect.PercentageValue)} maksymalnego zdrowia " +
                    $"co turę{FormatEffectDuration(skill, effect)}.",
                EffectType.RestoreResourceOverTimePercent =>
                    $"Regeneruje {FormatPercent(effect.PercentageValue)} {(EffectResource(skill, effect, caster) is ResourceType.Mana or ResourceType.Rage ? "maksymalnej" : "maksymalnego")} " +
                    $"{UiTerms.Resource(EffectResource(skill, effect, caster), genitive: true)} " +
                    $"co turę{FormatEffectDuration(skill, effect)}.",
                EffectType.Shield => DescribeShield(caster, effect)
                    + (skill?.SkillId == CombatShieldSystem.MageShieldSourceId
                        ? $" Czas trwania: {FormatTurns(effect.DurationTurns)} odbiorcy, odliczane na końcu jego tury. Ponowne rzucenie zastępuje poprzednią Magiczną Tarczę i odnawia czas."
                        : string.Empty),
                EffectType.ApplyStatus => DescribeStatus(caster, effect),
                EffectType.RemoveStatus =>
                    $"Usuwa status {GetStatusDisplayName(effect.StatusId)}.",
                EffectType.Push => DescribeMovementEffect("Odpycha", effect),
                EffectType.Pull => DescribeMovementEffect("Przyciąga", effect)
                    + (skill.SkillId == "skill.paladin.grounding_wave" ? " Kierunek: ku środkowi wybranego obszaru." : string.Empty),
                EffectType.SwapPositions => effect.BaseChance < 0.9999f
                    ? $"{FormatPercent(effect.BaseChance)} szansy na zamianę miejsc rzucającego z celem."
                    : "Zamienia miejscami rzucającego i cel.",
                EffectType.Fallback =>
                    $"Wycofuje rzucającego o {FormatDistance(effect.DisplacementDistance)}.",
                EffectType.Charge => DescribeCharge(effect),
                EffectType.Teleport =>
                    $"Teleportuje cel na wybrane pole " +
                    $"(zasięg: {FormatRange(effect.TeleportDestinationRange)}).",
                EffectType.CreateObject => DescribeCreateObject(effect),
                EffectType.Summon => DescribeSummon(effect),
                EffectType.ModifyStat => DescribeModifyStat(skill, effect),
                EffectType.ModifyVar => DescribeModifyVar(skill, effect),
                EffectType.ExtendStatusDuration =>
                    $"Przedłuża czas {(!string.IsNullOrWhiteSpace(effect.StatusId) ? GetStatusGenitiveName(effect.StatusId) : "wskazanego statusu")} o {FormatTurns(effect.DurationTurns)}.",
                EffectType.ModifyActionPointsPercent =>
                    $"Zwiększa punkty akcji o {FormatPercent(effect.PercentageValue)} " +
                    $"w następnej turze{FormatEffectDuration(skill, effect)}.",
                EffectType.ModifyActionValue => DescribeActionValue(effect),
                EffectType.DrainResource => effect.BaseValue > 0
                    ? $"Odbiera celowi {effect.BaseValue} zasobu."
                    : "Odbiera celowi zasób.",
                EffectType.BlockResourceRegeneration =>
                    DescribeResourceRegenerationBlock(skill, effect),
                EffectType.SacrificeHealthForResource =>
                    $"Poświęca {FormatPercent(effect.HealthSacrificePercent)} maks. zdrowia, " +
                    $"aby odzyskać {FormatValue(effect.ResourceRestorePerSacrificedHealth)} zasobu za każdy punkt zdrowia.",
                EffectType.ModifyResistance => DescribeResistance(skill, effect),
                EffectType.ApplyUniqueBuff => DescribeUniqueBuff(skill, caster, effect),
                EffectType.LegacyModifyActionPoints => $"Zapewnia {effect.ActionPointModifier} dodatkowych AP w następnej turze{FormatEffectDuration(skill, effect)}.",
                EffectType.LegacyRedirectDamage => $"Przekierowuje {FormatPercent(effect.RedirectedDamagePercent)} otrzymanych obrażeń do rzucającego{FormatEffectDuration(skill, effect)}.",
                EffectType.LegacyApplyAura => DescribeAura(skill, caster, effect, FormatEffectDuration(skill, effect)),
                EffectType.LegacyApplyOnHitStatus => DescribeOnHitBuff(skill, caster, effect, FormatEffectDuration(skill, effect)),
                _ => "Wywołuje dodatkowy efekt."
            };

            description = UiTerms.Present(description, EffectResource(skill, effect, caster));
            return IsRuntimeConsumable(skill)
                ? description
                : AddEffectContext(description, skill, effect, includeTarget);
        }

        private string DescribeDamage(SkillData skill, BattleUnit caster, SkillEffectData effect)
        {
            var range = Godmist.Systems.Combat.CombatSkillPreviewCalculator
                .CalculateDamageRange(caster, effect, skill);
            var damageText = range.HasDamage
                ? range.ToDisplayString()
                : "";
            var hitCountText = effect.DamageHitCount > 1
                ? $" w {FormatHitCount(effect.DamageHitCount)}"
                : string.Empty;
            var description = string.IsNullOrEmpty(damageText)
                ? effect.DamageMultiplier > 0f
                    ? $"Zadaje {FormatPercent(effect.DamageMultiplier)} średniego ataku "
                      + $"obrażeń {GetDamageTypeDisplayName(effect.DamageType)}{hitCountText}."
                    : $"Zadaje obrażenia {GetDamageTypeDisplayName(effect.DamageType)}{hitCountText}."
                : $"Zadaje {damageText} obrażeń {GetDamageTypeDisplayName(effect.DamageType)}{hitCountText}.";

            if (effect.RearAttackDamageBonus > 0f)
                description += $" Atak od tyłu zadaje o {FormatPercent(effect.RearAttackDamageBonus)} więcej obrażeń.";

            if (effect.BonusDamageMultiplierChance > 0f)
            {
                description += $" Każde trafienie ma {FormatPercent(effect.BonusDamageMultiplierChance)} " +
                    $"szansy na obrażenia ×{FormatValue(effect.BonusDamageMultiplier)}.";
            }

            if (effect.BonusPenetration > 0f
                && (effect.DamageType == DamageType.Physical
                    || effect.DamageType == DamageType.Magical))
            {
                var resistance = UiTerms.Stat(effect.DamageType == DamageType.Magical
                    ? CharacterStatType.MagicalDefensePenetration : CharacterStatType.PhysicalDefensePenetration);
                description += $" {resistance}: +{FormatPercent(effect.BonusPenetration)}.";
            }

            if (effect.LifeStealPercent > 0f)
            {
                description += $" Leczy rzucającego za {FormatPercent(effect.LifeStealPercent)} " +
                    "zadanych obrażeń.";
            }

            if (effect.ResourceDrainPercent > 0f)
            {
                description += $" Odbiera celowi {FormatPercent(effect.ResourceDrainPercent)} " +
                    "zadanych obrażeń jako zasób i przekazuje go rzucającemu.";
            }

            if (effect.AdditionalHitTrials > 0)
                description += $" Dodatkowo {effect.AdditionalHitTrials} prób kolejnego trafienia, każda z szansą {FormatPercent(effect.AdditionalHitChance)}.";
            if (effect.AllyLifeStealPercent > 0f)
                description += $" Każdy sojusznik odzyskuje {FormatPercent(effect.AllyLifeStealPercent)} faktycznie zadanych obrażeń jako zdrowie.";
            if (!string.IsNullOrWhiteSpace(effect.OnHitStatusId) && effect.OnHitStatusChance > 0f)
                description += " " + DescribeOnHitBuff(skill, caster, effect, string.Empty);

            return description;
        }

        private static string DescribeShield(BattleUnit caster, SkillEffectData effect)
        {
            var baseValue = Mathf.Max(0, effect.BaseValue);
            if (effect.ShieldAverageAttackMultiplier <= 0f)
            {
                return baseValue > 0
                    ? $"Nadaje {baseValue} tarczy."
                    : "Nadaje tarczę.";
            }

            if (caster?.Character?.Stats == null)
            {
                return baseValue > 0
                    ? $"Nadaje {baseValue} tarczy oraz {FormatPercent(effect.ShieldAverageAttackMultiplier)} średniego ataku jako tarczę."
                    : $"Nadaje tarczę równą {FormatPercent(effect.ShieldAverageAttackMultiplier)} średniego ataku.";
            }

            var averageAttack = Mathf.RoundToInt((
                Mathf.Max(0f, caster.Character.Stats.MinAttack.GetValue())
                + Mathf.Max(0f, caster.Character.Stats.MaxAttack.GetValue())) * 0.5f);
            var amount = baseValue + Mathf.FloorToInt(
                averageAttack * effect.ShieldAverageAttackMultiplier);
            return $"Nadaje {amount} tarczy.";
        }

        private string DescribeStatus(BattleUnit caster, SkillEffectData effect)
        {
            var guaranteed = effect.BaseChance >= 0.9999f;
            var statusName = guaranteed
                ? GetStatusDisplayName(effect.StatusId)
                : GetStatusGenitiveName(effect.StatusId);
            var description = guaranteed
                ? $"Nakłada {statusName}"
                : $"{FormatPercent(effect.BaseChance)} szansy na nałożenie {statusName}";

            if (effect.StatusDamageMultiplier > 0f
                && IsDamageOverTime(effect.StatusId))
            {
                var damage = Godmist.Systems.Combat.StatusEffectSystem.CalculateStatusDamagePerTurn(caster, effect);
                var scaling = effect.DamageValueSource == DamageValueSource.AlchemicalPower
                    ? "mocy alchemicznej" : "średniego ataku";
                description += damage > 0
                    ? guaranteed
                        ? $", zadając {damage} obrażeń co turę"
                        : $" zadającego {damage} obrażeń co turę"
                    : guaranteed
                        ? $", zadając {FormatPercent(effect.StatusDamageMultiplier)} {scaling} co turę"
                        : $" zadającego {FormatPercent(effect.StatusDamageMultiplier)} {scaling} co turę";
            }

            if (effect.StatusHealPercent > 0f)
            {
                description += guaranteed
                    ? $", przywracając {FormatPercent(effect.StatusHealPercent)} maks. zdrowia na końcu każdej tury"
                    : $" i przywracającego {FormatPercent(effect.StatusHealPercent)} maks. zdrowia na końcu każdej tury";
            }

            if (effect.DurationTurns > 0)
            {
                description += $" przez {FormatTurns(effect.DurationTurns)}";
            }
            if (effect.CounterattackEffects?.Count > 0)
                description += ". Reakcja w zasięgu " + FormatRange(effect.CounterattackRange) + ": "
                    + string.Join(" ", effect.CounterattackEffects.Where(counter => counter != null)
                        .Select(counter => DescribeEffect(null, caster, counter, false)));
            return description.TrimEnd('.') + ".";
        }

        private static string DescribeMovementEffect(string verb, SkillEffectData effect)
        {
            var chance = effect.EffectType == EffectType.Push
                ? 1f
                : effect.BaseChance;
            if (chance < 0.9999f)
            {
                var action = verb == "Odpycha" ? "odpychanie" : "przyciągnięcie";
                return $"{FormatPercent(chance)} szansy na {action} " +
                    $"o {FormatDistance(effect.DisplacementDistance)}.";
            }

            return $"{verb} o {FormatDistance(effect.DisplacementDistance)}.";
        }

        private static string DescribeCharge(SkillEffectData effect)
        {
            var prefix = effect.BaseChance < 0.9999f
                ? $"{FormatPercent(effect.BaseChance)} szansy na "
                : string.Empty;
            return string.IsNullOrEmpty(prefix)
                ? $"Szarżuje o {FormatDistance(effect.DisplacementDistance)}."
                : $"{prefix}szarżę o {FormatDistance(effect.DisplacementDistance)}.";
        }

        private static string DescribeCreateObject(SkillEffectData effect)
        {
            var name = effect.SummonedObjectDefinition?.DisplayName;
            if (string.IsNullOrWhiteSpace(name)) name = "obiekt";
            return $"Tworzy {name} na wybranym polu " +
                $"(zasięg: {FormatRange(effect.ObjectPlacementRange)}, " +
                $"maks. aktywnych: {effect.MaxActiveObjects}).";
        }

        private static string DescribeSummon(SkillEffectData effect)
        {
            var name = effect.SummonedCharacterDefinition?.DisplayName
                ?? effect.SummonedClassData?.ClassName;
            if (effect.SummonPool?.Count > 0)
                name = "losową jednostkę z puli: " + string.Join(", ", effect.SummonPool.Where(unit => unit != null).Select(unit => unit.DisplayName));
            if (string.IsNullOrWhiteSpace(name)) name = "jednostkę";
            var duration = effect.SummonDurationTurns > 0
                ? $" przez {FormatTurns(effect.SummonDurationTurns)}"
                : string.Empty;
            return $"Przyzywa {name}{duration} " +
                $"z {FormatPercent(effect.SummonPercentMaxHealth)} maks. zdrowia " +
                $"(zasięg umieszczenia: {FormatRange(effect.SummonPlacementRange)}, " +
                $"maks. aktywnych: {effect.MaxActiveSummons}).";
        }

        private static string DescribeResourceRegenerationBlock(
            SkillData skill,
            SkillEffectData effect)
        {
            var duration = skill?.IsToggle == true
                ? " dopóki skill jest aktywny"
                : effect.ResourceRegenerationBlockTurns > 0
                    ? $" przez {FormatTurns(effect.ResourceRegenerationBlockTurns)}"
                    : string.Empty;
            return $"Blokuje dodatnią regenerację zasobu{duration}.";
        }

        private static string DescribeModifyStat(
            SkillData skill,
            SkillEffectData effect)
        {
            var change = FormatStatusStatModifier(effect.ModifierStatType, effect.ModifierOperation, effect.ModifierValue)
                + FormatEffectDuration(skill, effect) + ".";
            return effect.BaseChance < .9999f ? $"{FormatPercent(effect.BaseChance)} szansy: {change}" : change;
        }

        public static string FormatStatusStatModifier(CharacterStatType stat, StatModifierOperation operation, float amount, ResourceType resource = ResourceType.None)
        {
            var statName = UiTerms.Stat(stat, resource);
            var value = FormatStatusStatValue(stat, operation, amount);
            return operation == StatModifierOperation.Maximum
                ? $"Limit — {statName}: {value}" : $"{statName}: {value}";
        }

        private static string FormatStatusStatValue(CharacterStatType stat, StatModifierOperation operation, float amount)
        {
            // Relative modifiers use %, additive chance bonuses use pp.
            // Flat Dodge/Celność remain point values, never percentages.
            var isPercent = operation == StatModifierOperation.Percent
                || (operation != StatModifierOperation.FlatAbsolute && IsFractionalStat(stat));
            var value = isPercent
                ? FormatSignedStatValue(stat, amount, true)
                : FormatSignedStatValue(stat, amount, false);
            if (IsFractionalStat(stat) && operation != StatModifierOperation.Percent && operation != StatModifierOperation.Maximum)
                value = (amount * 100f).ToString("+0.##;-0.##;0", UiTerms.PolishCulture) + " " + UiTerms.PercentagePoints;
            else if (IsFractionalStat(stat) && operation == StatModifierOperation.FlatAbsolute)
                value = FormatSignedPercent(amount);
            return operation == StatModifierOperation.Maximum
                ? FormatStatValue(stat, amount, isPercent) : value;
        }

        public static string FormatStatusVariableModifier(GameplayVariableType type, float value, string referenceId = null, ResourceType resource = ResourceType.None)
            => $"{UiTerms.Variable(type, referenceId, resource)}: " + FormatStatusVariableValue(type, value);

        private static string FormatStatusVariableValue(GameplayVariableType type, float value)
            => type == GameplayVariableType.HitChanceBonus
                ? (value * 100f).ToString("+0.##;-0.##;0", UiTerms.PolishCulture) + " " + UiTerms.PercentagePoints
                : FormatSignedPercent(value);

        private static string DescribeModifyVar(
            SkillData skill,
            SkillEffectData effect)
        {
            var change = FormatStatusVariableModifier(effect.ModifierVariableType, effect.ModifierValue)
                + FormatEffectDuration(skill, effect) + ".";
            return effect.BaseChance < .9999f ? $"{FormatPercent(effect.BaseChance)} szansy: {change}" : change;
        }

        private static string DescribeActionValue(SkillEffectData effect)
        {
            var percent = FormatPercent(Mathf.Abs(effect.ActionValuePercent));
            return effect.ActionValuePercent >= 0f
                ? $"Przyspiesza kolejkę działania celu o {percent}."
                : $"Opóźnia kolejkę działania celu o {percent}.";
        }

        private static string DescribeResistance(
            SkillData skill,
            SkillEffectData effect)
        {
            var verb = effect.ModifierValue >= 0f ? "Zwiększa" : "Zmniejsza";
            var name = effect.ResistanceType == ResistanceType.All ? "wszystkie odporności"
                : UiTerms.Resistance(effect.ResistanceType).ToLowerInvariant();
            var change = $"{verb} {name} " +
                $"o {FormatPercent(Mathf.Abs(effect.ModifierValue))}" +
                $"{FormatEffectDuration(skill, effect)}.";
            return effect.BaseChance < .9999f ? $"{FormatPercent(effect.BaseChance)} szansy: {change}" : change;
        }

        private string DescribeUniqueBuff(
            SkillData skill,
            BattleUnit caster,
            SkillEffectData effect)
        {
            var duration = FormatEffectDuration(skill, effect);
            return effect.UniqueBuffType switch
            {
                UniqueBuffType.GuaranteedCriticalHit =>
                    $"Gwarantuje trafienie krytyczne przy następnym ataku{duration}.",
                UniqueBuffType.GuaranteedDodge =>
                    $"Gwarantuje unik przed następnym atakiem{duration}.",
                UniqueBuffType.MageMaximumDamage => $"Ataki zadają maksymalne obrażenia{duration}.",
                UniqueBuffType.Hawkeye => $"Ataki nie chybiają i zadają o 5% więcej obrażeń za każde pole odległości{duration}.",
                UniqueBuffType.MageOffering => $"Zwiększa obrażenia zadawane o {FormatPercent(MageCombatSystem.OfferingBonus(caster))} i otrzymywane o {FormatPercent(MageCombatSystem.OfferingBonus(caster) * (1 - .1f * MageCombatSystem.W(caster, "calm")))}{duration}.",
                UniqueBuffType.MaximumDamage =>
                    $"Następne obrażenia będą maksymalne{duration}.",
                UniqueBuffType.ActionPointsNextTurn =>
                    $"Zapewnia {effect.ActionPointModifier} dodatkowych AP w następnej turze{duration}.",
                UniqueBuffType.OnHitStatus => DescribeOnHitBuff(
                    skill,
                    caster,
                    effect,
                    duration),
                UniqueBuffType.DamageRedirect =>
                    $"Przekierowuje {FormatPercent(effect.RedirectedDamagePercent)} " +
                    $"obrażeń otrzymanych przez cel do rzucającego{duration}.",
                UniqueBuffType.AuraStatus => DescribeAura(skill, caster, effect, duration),
                _ => "Nakłada unikalne wzmocnienie."
            };
        }

        private string DescribeOnHitBuff(
            SkillData skill,
            BattleUnit caster,
            SkillEffectData effect,
            string duration)
        {
            var chance = effect.OnHitStatusChance < 0.9999f;
            var statusDuration = effect.OnHitStatusDurationTurns > 0
                ? $" na {FormatTurns(effect.OnHitStatusDurationTurns)}"
                : string.Empty;
            var statusName = GetStatusDisplayName(effect.OnHitStatusId);
            var statusObjectName = chance
                ? GetStatusGenitiveName(effect.OnHitStatusId)
                : statusName;
            var description = chance
                ? $"Każde trafienie ma {FormatPercent(effect.OnHitStatusChance)} " +
                  $"szansy na nałożenie {statusObjectName}{statusDuration}."
                : $"Każde trafienie nakłada {statusName}{statusDuration}.";
            var buffDuration = string.IsNullOrWhiteSpace(duration)
                ? string.Empty
                : $" Wzmocnienie działa{duration}.";
            return description
                + DescribePeriodicStatusDamage(
                    caster,
                    effect.OnHitStatusId,
                    statusName,
                    effect.StatusDamageMultiplier)
                + buffDuration;
        }

        private string DescribeAura(
            SkillData skill,
            BattleUnit caster,
            SkillEffectData effect,
            string duration)
        {
            var action = effect.BaseChance < 0.9999f
                ? $"ma {FormatPercent(effect.BaseChance)} szansy na nałożenie " +
                  GetStatusGenitiveName(effect.StatusId)
                : $"nakłada {GetStatusDisplayName(effect.StatusId)}";
            var includesCaster = effect.AuraIncludesCaster
                ? ", w tym rzucającego"
                : string.Empty;
            var description = $"Tworzy aurę, która {action} " +
                $"na {GetTargetingDisplayName(effect.AuraTargetingType)} " +
                $"w promieniu {FormatDistance(effect.AuraRadius)}{includesCaster}{duration}.";
            return description + DescribePeriodicStatusDamage(
                caster,
                effect.StatusId,
                GetStatusDisplayName(effect.StatusId),
                effect.StatusDamageMultiplier);
        }

        private string DescribePeriodicStatusDamage(
            BattleUnit caster,
            string statusId,
            string statusName,
            float damageMultiplier)
        {
            if (damageMultiplier <= 0f || !IsDamageOverTime(statusId))
            {
                return string.Empty;
            }
            var damage = caster?.Character?.Stats == null
                ? 0
                : Godmist.Systems.Combat.CombatDamageValueCalculator.CalculateDamageValue(
                    caster,
                    DamageValueSource.AverageAttack,
                    damageMultiplier);
            return damage > 0
                ? $" Nałożony status ({statusName}) zadaje {damage} obrażeń " +
                  "na początku każdej tury."
                : $" Nałożony status ({statusName}) zadaje " +
                  $"{FormatPercent(damageMultiplier)} średniego ataku " +
                  "na początku każdej tury.";
        }

        private string AddEffectContext(
            string description,
            SkillData skill,
            SkillEffectData effect,
            bool includeTarget = true)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;

            var result = description.Trim();
            if (!result.EndsWith(".", StringComparison.Ordinal)) result += ".";

            var target = GetEffectTargetDescription(skill, effect.TargetModeIndex);
            if (includeTarget && !string.IsNullOrWhiteSpace(target)) result += $" Cel: {target}.";

            var conditions = DescribeConditions(skill, effect);
            if (!string.IsNullOrWhiteSpace(conditions)) result += $" Warunek: {conditions}.";

            if (effect.DelayCasterTurns > 0)
            {
                result += $" Opóźnienie efektu: {FormatTurnCount(effect.DelayCasterTurns)}.";
            }

            return result;
        }

        private string GetEffectTargetDescription(
            SkillData skill,
            int targetModeIndex,
            BattleUnit caster = null)
        {
            if (skill?.TargetModes == null
                || targetModeIndex < 0
                || targetModeIndex >= skill.TargetModes.Count)
            {
                return string.Empty;
            }

            var mode = PassiveEffectCombatExecutionSystem.ResolveMode(caster, skill, skill.TargetModes[targetModeIndex]);
            if (mode == null) return string.Empty;

            var group = GetTargetingDisplayName(mode.TargetingType);
            var recipients=mode.TargetingType switch{TargetingType.Enemies=>"wrogowie",TargetingType.Allies=>"sojusznicy",_=>"jednostki"};
            if(skill?.SkillId=="skill.paladin.judgement_field")
            {
                var extent=2+PaladinCombatSystem.C(caster,"field_range");
                return $"kwadrat {2*extent+1}×{2*extent+1} wokół wskazanego pola, bez środka; wrogowie w linii wzroku";
            }
            if (mode.AbsoluteRange?.Shape == RangeShape.ThreeRays)
                return $"trzy promienie: jeden prosty i dwa ukośne; {recipients}";
            if(mode.AbsoluteRange?.Shape==RangeShape.Cone)return $"stożek przed rzucającym do {FormatDistance(mode.AbsoluteRange.MaxRange)}; {recipients}";
            if(mode.TargetType is TargetType.AreaAroundCaster or TargetType.Line)
                return DescribeShape(mode.AbsoluteRange)+$" od rzucającego; {recipients}";
            return mode.TargetType switch
            {
                TargetType.Caster => "użytkownik umiejętności",
                TargetType.Single => $"pojedynczy {GetTargetingSingularName(mode.TargetingType)}",
                TargetType.All => GetTargetingPluralName(mode.TargetingType),
                TargetType.AreaAroundCaster =>
                    $"obszar wokół rzucającego, obejmujący {group}",
                TargetType.AreaAroundTile =>
                    DescribeAreaTarget(mode, recipients),
                TargetType.Line => mode.AbsoluteRange?.Shape == RangeShape.ThreeRays
                    ? $"trzy promienie: prosto i dwa ukośne, obejmujące {group}"
                    : $"linia obejmująca {group}",
                TargetType.Chain => $"łańcuch do {mode.ChainTargets} celów: {group}; kolejne cele w zasięgu {FormatRange(mode.RelativeRange)} od poprzedniego",
                TargetType.MultipleChosen => $"wybrane cele: {group}",
                TargetType.MultipleRandom => $"losowe cele: {group}",
                _ => "niestandardowy cel"
            };
        }

        private static string DescribeAreaTarget(
            SkillTargetModeData mode,
            string group)
        {
            return DescribeShape(mode?.RelativeRange)+$" wokół wskazanego pola; {group}";
        }

        private static string DescribeShape(SkillRangeData range)
        {
            if(range==null)return "obszar";
            var shape=range.Shape switch
            {
                RangeShape.Square=>$"kwadrat {2*range.MaxRange+1}×{2*range.MaxRange+1}",
                RangeShape.Radial=>$"okrąg o zasięgu {FormatRange(range)}",
                RangeShape.Ring=>$"pierścień o zasięgu {FormatRange(range)}",
                RangeShape.Line=>$"linia długości {FormatRange(range)}",
                RangeShape.TwoSidedLine=>$"linia w dwóch kierunkach, zasięg {FormatRange(range)}",
                RangeShape.Diagonal=>$"dwa ukośne promienie, zasięg {FormatRange(range)}",
                RangeShape.XShape=>$"obszar X, zasięg {FormatRange(range)}",
                RangeShape.Rectangle=>$"prostokąt długości {range.MaxRange-range.MinRange+1} i szerokości {range.RectangleWidth}",
                RangeShape.ThreeRays=>"trzy promienie: jeden prosty i dwa ukośne",
                RangeShape.Cone=>$"stożek o zasięgu {FormatRange(range)}",
                _=>$"obszar o zasięgu {FormatRange(range)}"
            };
            return shape+(range.MinRange>0 && range.Shape==RangeShape.Square?range.MinRange==1?", bez środka":$", bez wewnętrznego zasięgu {range.MinRange-1}":"");
        }

        private string DescribeConditions(SkillData skill, SkillEffectData effect)
        {
            if (effect == null) return string.Empty;

            var result = new StringBuilder();
            // Standalone physical controls share one per-target accuracy roll in execution.
            if (effect.EffectType != EffectType.DealDamage && !effect.AlwaysHits
                && effect.Polarity == EffectPolarity.Negative) result.Append("trafienie celu");
            foreach (var condition in effect.Conditions ?? Array.Empty<SkillEffectConditionData>())
            {
                if (condition == null || condition.ConditionType == EffectConditionType.Always)
                {
                    continue;
                }

                var text = condition.ConditionType switch
                {
                    EffectConditionType.ReferencedEffectSucceeded =>
                        skill?.Effects != null && condition.ReferencedEffectIndex >= 0
                        && condition.ReferencedEffectIndex < skill.Effects.Count
                        && skill.Effects[condition.ReferencedEffectIndex]?.EffectType == EffectType.DealDamage
                            ? "trafienie celu" : $"efekt {condition.ReferencedEffectIndex + 1} zadziała",
                    EffectConditionType.ReferencedEffectDealtHealthDamage =>
                        $"efekt {condition.ReferencedEffectIndex + 1} zada obrażenia zdrowiu",
                    EffectConditionType.TargetHasStatus =>
                        $"cel ma {GetStatusGenitiveName(condition.StatusId)}",
                    EffectConditionType.CasterHasStatus =>
                        $"rzucający ma {GetStatusGenitiveName(condition.StatusId)}",
                    EffectConditionType.TargetHealthBelowPercent =>
                        $"cel ma mniej niż {FormatPercent(condition.HealthPercentThreshold)} zdrowia",
                    EffectConditionType.CasterHealthBelowPercent =>
                        $"rzucający ma mniej niż {FormatPercent(condition.HealthPercentThreshold)} zdrowia",
                    EffectConditionType.MinimumAdjacentAllies =>
                        $"w pobliżu jest co najmniej {condition.IntegerThreshold} sojuszników",
                    EffectConditionType.MinimumResourceSpent =>
                        $"wydano co najmniej {condition.IntegerThreshold} zasobu",
                    EffectConditionType.TargetHasActiveShield => "cel ma aktywną tarczę",
                    EffectConditionType.AnyOpponentHasStatus => $"co najmniej jeden przeciwnik ma status: {GetStatusDisplayName(condition.StatusId)}",
                    EffectConditionType.NoOpponentHasStatus => $"żaden przeciwnik nie ma statusu: {GetStatusDisplayName(condition.StatusId)}",
                    _ => condition.ConditionType.ToString()
                };

                if (result.Length > 0) result.Append(" i ");
                result.Append(text);
            }

            return result.ToString();
        }

        private static string FormatEffectDuration(
            SkillData skill,
            SkillEffectData effect)
        {
            if (skill?.IsToggle == true) return " dopóki skill jest aktywny";
            return effect?.DurationTurns > 0
                ? $" przez {FormatTurns(effect.DurationTurns)}"
                : string.Empty;
        }

        private static string FormatTurns(int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns == 1) return "następną turę";
            return $"następne {turns} {GetTurnWord(turns)}";
        }

        private static string FormatTurnCount(int turns)
        {
            turns = Mathf.Max(0, turns);
            if (turns == 1) return "1 turę";
            return $"{turns} {GetTurnCountWord(turns)}";
        }

        private static string GetTurnWord(int turns) =>
            turns is >= 2 and <= 4 ? "tury" : "tur";

        private static string GetTurnCountWord(int turns) =>
            turns is >= 2 and <= 4 ? "tury" : "tur";

        private static string FormatHitCount(int hits)
        {
            hits = Mathf.Max(1, hits);
            if (hits == 1) return "1 trafieniu";
            return $"{hits} trafieniach";
        }

        private static string FormatRange(SkillRangeData range)
        {
            return range == null
                ? "nieznany"
                : FormatRange(range.MinRange, range.MaxRange);
        }

        private static string FormatRange(int minimum, int maximum)
        {
            minimum = Mathf.Max(0, minimum);
            maximum = Mathf.Max(minimum, maximum);
            if (minimum == maximum) return $"{minimum} {GetFieldWord(minimum)}";
            return $"{minimum}–{maximum} pól";
        }

        private static string GetFieldWord(int value)
        {
            return value == 1
                ? "pole"
                : value is >= 2 and <= 4
                    ? "pola"
                    : "pól";
        }

        private static string FormatDistance(int distance)
        {
            distance = Mathf.Max(0, distance);
            return $"{distance} {GetFieldWord(distance)}";
        }

        private static string FormatSignedPercent(float value)
        {
            if (value > 0f) return $"+{FormatPercent(value)}";
            if (value < 0f) return $"-{FormatPercent(value)}";
            return "0%";
        }

        private static string FormatPercent(float value)
        {
            return (Mathf.Abs(value) * 100f).ToString("0.#", UiTerms.PolishCulture) + "%";
        }

        private static string FormatSignedStatValue(
            CharacterStatType stat,
            float value,
            bool isPercent)
        {
            if (value > 0f) return $"+{FormatStatValue(stat, value, isPercent)}";
            if (value < 0f) return $"-{FormatStatValue(stat, Mathf.Abs(value), isPercent)}";
            return FormatStatValue(stat, 0f, isPercent);
        }

        private static string FormatStatValue(
            CharacterStatType stat,
            float value,
            bool isPercent)
        {
            return stat switch
            {
                CharacterStatType.CritDamage => isPercent
                    ? (value * 100f).ToString("0.00", UiTerms.PolishCulture) + "%"
                    : value.ToString("0.00;-0.00;0.00", UiTerms.PolishCulture),
                CharacterStatType.Dodge => isPercent
                    ? $"{Mathf.RoundToInt(value * 100f)}%"
                    : Mathf.RoundToInt(value).ToString("0;-0;0", UiTerms.PolishCulture),
                _ => isPercent
                    ? (value * 100f).ToString("0.##", UiTerms.PolishCulture) + "%"
                    : FormatValue(value)
            };
        }

        private static string GetDamageTypeDisplayName(DamageType type)
        {
            return UiTerms.Damage(type, genitive: true);
        }

        private static string GetResourceAccusativeName(ResourceType type)
        {
            return UiTerms.Resource(type, accusative: true);
        }

        private static string GetTargetingDisplayName(TargetingType type)
        {
            return type switch
            {
                TargetingType.Enemies => "wrogów",
                TargetingType.Allies => "sojuszników",
                _ => "jednostek"
            };
        }

        private static string GetTargetingSingularName(TargetingType type)
        {
            return type switch
            {
                TargetingType.Enemies => "wróg",
                TargetingType.Allies => "sojusznik",
                _ => "jednostka"
            };
        }

        private static string GetTargetingPluralName(TargetingType type)
        {
            return type switch
            {
                TargetingType.Enemies => "wszyscy wrogowie",
                TargetingType.Allies => "wszyscy sojusznicy",
                _ => "wszystkie jednostki"
            };
        }

        private static string GetResistanceDisplayName(ResistanceType type)
        {
            return UiTerms.Resistance(type, subjectOnly: true);
        }

        private static string GetGameplayVariableDisplayName(
            GameplayVariableType type,
            string referenceId = null)
        {
            return UiTerms.Variable(type, referenceId);
        }

        private string GetStatusDisplayName(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId)) return "status";
            var glossaryTitle = GetGlossaryTitle(statusId);
            return UiTerms.Status(statusId, string.IsNullOrWhiteSpace(glossaryTitle) ? ToTitleCase(statusId) : glossaryTitle);
        }

        private string GetStatusGenitiveName(string statusId)
        {
            return statusId?.ToLowerInvariant() switch
            {
                StatusEffectIds.Bleed => "krwawienia",
                StatusEffectIds.Poison => "zatrucia",
                StatusEffectIds.Burn => "podpalenia",
                StatusEffectIds.Stun => "ogłuszenia",
                StatusEffectIds.Freeze => "zamrożenia",
                StatusEffectIds.Sleep => "snu",
                StatusEffectIds.Paralysis => "paraliżu",
                StatusEffectIds.Immobilize => "unieruchomienia",
                StatusEffectIds.Chill => "oziębienia",
                StatusEffectIds.Taunt => "prowokacji",
                StatusEffectIds.Invisibility => "niewidzialności",
                StatusEffectIds.Regeneration => "regeneracji",
                StatusEffectIds.Counterattack => "kontrataku",
                _ => GetStatusDisplayName(statusId).ToLowerInvariant()
            };
        }

        private static string ToTitleCase(string value)
        {
            var normalized = value.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ');
            var result = new StringBuilder(normalized.Length);
            var capitalize = true;
            foreach (var character in normalized)
            {
                if (char.IsWhiteSpace(character))
                {
                    capitalize = true;
                    result.Append(character);
                    continue;
                }

                result.Append(capitalize
                    ? char.ToUpperInvariant(character)
                    : character);
                capitalize = false;
            }

            return result.ToString();
        }

        private static bool IsFractionalStat(CharacterStatType statType)
        {
            return statType == CharacterStatType.CritChance
                || statType == CharacterStatType.PhysicalDefensePenetration
                || statType == CharacterStatType.MagicalDefensePenetration;
        }

        private static string GetStatDisplayName(CharacterStatType statType)
        {
            return UiTerms.Stat(statType);
        }

        private static string GetEquipmentStatDisplayName(CharacterStatType statType, EquipmentItemInstance equipment = null)
        {
            var resource = equipment?.EquipmentDefinition?.RequiredClass?.ResourceType ?? ResourceType.None;
            if (resource == ResourceType.None) resource = equipment?.RequiredClassId switch
            {
                "class.mage" or "class.paladin" => ResourceType.Mana,
                "class.warrior" => ResourceType.Rage,
                "class.scout" => ResourceType.Momentum,
                _ => ResourceType.None
            };
            return UiTerms.Stat(statType, resource);
        }

        private static string GetEquipmentSlotDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "Broń",
                EquipmentSlot.Armor => "Pancerz",
                EquipmentSlot.Necklace => "Naszyjnik",
                EquipmentSlot.Ring => "Pierścień",
                EquipmentSlot.Bracelet => "Bransoleta",
                EquipmentSlot.Earrings => "Kolczyki",
                _ => slot.ToString()
            };
        }

        private static string GetQualityDisplayName(EquipmentQuality quality)
        {
            return quality switch
            {
                EquipmentQuality.Normal => "Normalna",
                EquipmentQuality.Poor => "Słaba",
                EquipmentQuality.Excellent => "Doskonała",
                EquipmentQuality.Masterpiece => "Arcydzieło",
                _ => quality.ToString()
            };
        }

        public static string GetColoredRarityDisplayName(RarityTier rarity) =>
            ItemRarityPresentation.Colorize(rarity, GetRarityDisplayName(rarity));

        public static string GetRarityDisplayName(RarityTier rarity)
        {
            return rarity switch
            {
                RarityTier.Trash => "Śmieciowy",
                RarityTier.Broken => "Złamany",
                RarityTier.Damaged => "Uszkodzony",
                RarityTier.Common => "Pospolity",
                RarityTier.Uncommon => "Niepospolity",
                RarityTier.Rare => "Rzadki",
                RarityTier.Ancient => "Starożytny",
                RarityTier.Legendary => "Legendarny",
                RarityTier.Mythic => "Mityczny",
                RarityTier.Godly => "Boski",
                _ => rarity.ToString()
            };
        }

        private static string FormatAttackValue(float minimum, float maximum)
        {
            return Mathf.Approximately(minimum, maximum)
                ? FormatEquipmentValue(minimum)
                : $"{FormatEquipmentValue(minimum)}–{FormatEquipmentValue(maximum)}";
        }

        private static string CapitalizeFirst(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;
            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        public string BuildPresentedStatusPayload(CombatStatusPresentation presentation, CombatSession session = null)
        {
            session ??= GameBootstrap.Instance?.Session?.Combats?.FirstOrDefault(combat => combat.Units.Contains(presentation.Owner));
            session ??= presentation.Owner?.WarriorState.Session;
            var result = new StringBuilder();
            var combineDamage = presentation.IsDamageOverTimeGroup && presentation.Effects.Count > 1
                && presentation.Aura == null
                && presentation.Effects.All(effect => effect?.GetPayload<DamageOverTimePayload>()?.RemainingTotalDamage > 0);
            if (combineDamage)
            {
                // Round and mitigate each real tick separately, exactly as the combat processor does.
                var damage = presentation.NextTickDamage;
                Append(result, $"{GetStatusDisplayName(presentation.Effect.EffectId)}: łącznie {damage} obrażeń na początku następnej tury.");
                if (presentation.HasMixedDurations)
                {
                    var durations = presentation.Effects.GroupBy(effect => new { effect.RemainingDuration, effect.Lifetime })
                        .OrderBy(group => group.Key.RemainingDuration)
                        .Select(group =>
                        {
                            var member = new CombatStatusPresentation(group.First(), presentation.Kind, presentation.Priority,
                                explorationCharacter: presentation.ExplorationCharacter);
                            var duration = presentation.ExplorationCharacter != null ? ExplorationStatusDuration(member)
                                : group.Key.RemainingDuration > 0 ? $"{group.Key.RemainingDuration} t." : "bez limitu czasu";
                            return $"{duration} ({group.Sum(effect => Mathf.Max(1, effect.Stacks))} stos.)";
                        });
                    Append(result, UiTerms.Duration + " nałożeń: " + string.Join(" • ", durations));
                    Append(result, $"{UiTerms.Stacks}: {presentation.StackCount}");
                }
            }
            var describedRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var effect in presentation.Effects ?? Array.Empty<PassiveEffectRuntime>())
            {
                if (effect == null) continue;
                var body = BuildStatusPayloadCore(effect, presentation.Aura, presentation.OutgoingRedirect, presentation.Owner,
                    !combineDamage, describedRules.Add(effect.EffectId + ":" + effect.RemainingDuration + ":" + effect.Lifetime), session,
                    presentation.ExplorationCharacter?.ResourcePool?.ResourceType ?? ResourceType.None);
                if (combineDamage)
                {
                    if (!string.IsNullOrWhiteSpace(body)) Append(result, body);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(body)) continue;
                if (presentation.Effects.Count > 1 && presentation.StackCount == 0 && effect.Stacks > 1)
                    body += $"\n{UiTerms.Stacks}: {effect.Stacks}";
                if (presentation.HasMixedDurations || presentation.StackCount == 0)
                {
                    var member = new CombatStatusPresentation(effect, presentation.Kind, presentation.Priority,
                        explorationCharacter: presentation.ExplorationCharacter);
                    var duration = presentation.ExplorationCharacter != null ? ExplorationStatusDuration(member)
                        : effect.RemainingDuration > 0 ? $"Pozostało: {effect.RemainingDuration} t." : "Bez limitu czasu";
                    body += "\n" + duration;
                }
                Append(result, body);
            }
            return result.ToString();
        }

        private string BuildStatusPayload(
            PassiveEffectRuntime effect,
            CombatAuraRuntime aura = null,
            CombatDamageRedirectRuntime outgoingRedirect = null,
            BattleUnit owner = null)
            => BuildStatusPayloadCore(effect, aura, outgoingRedirect, owner, true, true);

        public static ResourceType StatusResource(PassiveEffectRuntime effect, BattleUnit owner, ResourceType fallback = ResourceType.None)
        {
            if (effect?.EffectId?.StartsWith("consumable.", StringComparison.OrdinalIgnoreCase) == true
                || effect?.EffectId?.StartsWith("potion.", StringComparison.OrdinalIgnoreCase) == true
                || effect?.EffectId?.StartsWith("equipment.", StringComparison.OrdinalIgnoreCase) == true
                || effect?.EffectId?.StartsWith("galduryte.", StringComparison.OrdinalIgnoreCase) == true)
                return ResourceType.None;
            return owner?.Character?.ResourcePool?.ResourceType ?? fallback;
        }

        private string BuildStatusPayloadCore(
            PassiveEffectRuntime effect, CombatAuraRuntime aura, CombatDamageRedirectRuntime outgoingRedirect,
            BattleUnit owner, bool includeDamage, bool includeRule, CombatSession session = null, ResourceType resourceContext = ResourceType.None)
        {
            var result = new StringBuilder();
            var heroDescription = HeroCombatStatusText.Describe(effect, owner, session);
            if (!string.IsNullOrWhiteSpace(heroDescription)) Append(result, heroDescription);
            if (effect?.EffectId == "catacombs.spectral_knight.shell")
            {
                Append(result, "Bezpośrednie trafienie: 1 HP. Odporność na obrażenia okresowe.");
                Append(result, $"Pozostałe trafienia: {Math.Max(0, 3 - (owner?.DeadKingKnightHits ?? 0))}.");
            }
            if (effect?.EffectId == "catacombs.dead_king.despair")
                Append(result, "Król i jego sojusznicy otrzymują o 25% mniej obrażeń od ataków dystansowych. Działa w fazie I.");
            if(effect?.EffectId=="catacombs.skeleton_slayer.armor")Append(result,"Otrzymuje o 35% mniej obrażeń fizycznych.");
            if(effect?.EffectId=="forest.bandit_leader.hidden_hand")Append(result,"Stale niewidzialny. Pokonanie obstawy przygotowuje fazę II na własną turę; wszystkie jego pułapki wtedy detonują.");
            if(effect?.EffectId=="forest.bandit_leader.commander")Append(result,"Na początku własnej tury: jeśli od poprzedniej własnej tury stracił mniej niż 20% maks. HP, regeneruje 10% maks. HP i uzyskuje niewidzialność.");
            if(effect?.EffectId=="equipment.poacher-armor.cooldown")Append(result,"Gruboskórność Kłusownika odnowi się po wskazanej liczbie własnych tur. Ponownie przyzna tarczę, jeśli zdrowie pozostaje poniżej progu.");
            if(effect?.EffectId=="catacombs.skeleton_slayer.revenge")Append(result,"Każde otrzymane obrażenia zwiększają regenerację AP o 3%, do 160% podstawowego limitu.");
            if(effect?.EffectId=="catacombs.skeleton_slayer.defense")Append(result,"Otrzymuje o 50% mniej obrażeń i kumuluje obrażenia przed mitygacją na następny atak. Brak obrażeń powoduje ogłuszenie na 2 tury.");
            if(effect?.EffectId=="catacombs.skeleton_slayer.hammer")Append(result,"Po przygotowaniu: 5 trafień w każdego wroga na Polu Impetu, ignoruje obronę. Otrzymanie 10% maks. HP obrażeń przerywa przygotowanie.");
            if(effect?.EffectId=="catacombs.skeleton_slayer.retaliation")Append(result,"Następny atak zadaje dodatkowe obrażenia zgromadzone przez Nieugiętą Obronę.");
            if (effect?.EffectId == "catacombs.dead_king.rage")
                Append(result, "Król zadaje o 30% więcej obrażeń i otrzymuje o 50% więcej obrażeń. Działa w fazie II.");
            if (aura != null)
            {
                var chance = aura.Chance < .999f
                    ? $" z {FormatPercent(aura.Chance)} szansy"
                    : string.Empty;
                var includesCaster = aura.IncludesCaster
                    ? " Obejmuje również rzucającego."
                    : " Nie obejmuje rzucającego.";
                Append(
                    result,
                    $"Nakłada {GetStatusDisplayName(aura.StatusId)}{chance} na " +
                    $"{GetAuraTargetingName(aura.TargetingType)} w promieniu " +
                    $"{FormatDistance(aura.Radius)}.{includesCaster}");
            }
            if (includeRule) AppendStatusRule(result, effect);

            var damageOverTime = effect.GetPayload<DamageOverTimePayload>();
            var periodicRecovery = effect.GetPayload<PeriodicRecoveryPayload>();
            if (periodicRecovery != null)
            {
                if (periodicRecovery.HealthPercent > 0f)
                {
                    Append(
                        result,
                        $"Regeneruje {FormatPercent(periodicRecovery.HealthPercent)} " +
                        "maksymalnego zdrowia na końcu każdej tury.");
                }

                if (periodicRecovery.ResourcePercent > 0f)
                {
                    Append(
                        result,
                        $"Regeneruje {FormatPercent(periodicRecovery.ResourcePercent)} " +
                        "maksymalnego zasobu na końcu każdej tury.");
                }
            }
            var auraDamagePerTurn = aura != null && IsDamageOverTime(effect.EffectId)
                ? aura.StatusDamagePerTurn
                : 0;
            if (includeDamage && auraDamagePerTurn > 0)
            {
                Append(
                    result,
                    $"{GetStatusDisplayName(effect.EffectId)} zadaje " +
                    $"{FormatEffectiveStatusDamage(owner, effect.EffectId, auraDamagePerTurn)} " +
                    "obrażeń na początku każdej tury.");
            }
            else if (includeDamage && IsDamageOverTime(effect.EffectId)
                && damageOverTime?.RemainingTotalDamage > 0)
            {
                var damagePerTurn = Mathf.CeilToInt(
                    damageOverTime.RemainingTotalDamage /
                    (float)Mathf.Max(1, effect.RemainingDuration));
                Append(
                    result,
                    $"{GetStatusDisplayName(effect.EffectId)} zadaje " +
                    $"{FormatEffectiveStatusDamage(owner, effect.EffectId, damagePerTurn)} " +
                    "obrażeń na początku każdej tury.");
            }
            else if (includeDamage && IsDamageOverTime(effect.EffectId)
                && (damageOverTime == null
                    || damageOverTime.RemainingTotalDamage <= 0))
            {
                // A status can have been restored from older combat data, or be a
                // presentation-only aura, without a numeric DoT payload. Keep the
                // status tooltip useful and avoid replacing its gameplay description
                // with the glossary sentence when the value is not recoverable.
                Append(
                    result,
                    $"{GetStatusDisplayName(effect.EffectId)} zadaje obrażenia " +
                    "na początku każdej tury.");
            }

            var sleepHealing = effect.GetPayload<SleepHealingPayload>();
            if (sleepHealing?.MaxHealthPercent > 0f)
            {
                Append(
                    result,
                    $"Przywraca {FormatPercent(sleepHealing.MaxHealthPercent)} maksymalnego zdrowia na końcu tury.");
            }

            foreach (var modifier in effect.StatModifiers)
            {
                if (modifier == null || (modifier.Operation != StatModifierOperation.Maximum && Mathf.Approximately(modifier.Value, 0f))) continue;
                Append(
                    result,
                    FormatStatusStatModifier(modifier.StatType, modifier.Operation, modifier.Value, StatusResource(effect, owner, resourceContext)) + ".");
            }

            foreach (var modifier in effect.VariableModifiers)
            {
                if (modifier == null || Mathf.Approximately(modifier.Value, 0f)) continue;
                Append(
                    result,
                    FormatStatusVariableModifier(modifier.VariableType, modifier.Value, modifier.ReferenceId, StatusResource(effect, owner, resourceContext)) + ".");
            }

            var uniqueBuff = effect.GetPayload<UniqueCombatBuffPayload>();
            if (uniqueBuff != null)
            {
                var description = uniqueBuff.BuffType switch
                {
                    UniqueBuffType.GuaranteedCriticalHit or UniqueBuffType.GuaranteedDodge or UniqueBuffType.MaximumDamage
                        or UniqueBuffType.MageMaximumDamage or UniqueBuffType.Hawkeye when !string.IsNullOrWhiteSpace(heroDescription) => string.Empty,
                    UniqueBuffType.GuaranteedCriticalHit =>
                        "Ładunki: 1. Następny atak zawsze trafia krytycznie.",
                    UniqueBuffType.GuaranteedDodge =>
                        "Ładunki: 1. Następny atak przeciwko tej postaci zawsze chybia.",
                    UniqueBuffType.MageMaximumDamage => "Ataki zadają maksymalne obrażenia przez czas działania efektu.",
                    UniqueBuffType.Hawkeye => "Ataki nie chybiają. Obrażenia zwiększone o 5% za każde pole odległości.",
                    UniqueBuffType.MageOffering => string.IsNullOrWhiteSpace(heroDescription)
                        ? "Ofiara zwiększa obrażenia zadawane i otrzymywane." : string.Empty,
                    UniqueBuffType.MaximumDamage =>
                        "Ładunki: 1. Następny atak zada maksymalne obrażenia.",
                    UniqueBuffType.ActionPointsNextTurn =>
                        $"Zapewnia {uniqueBuff.ActionPointModifier:+0.##;-0.##;0} AP w następnej turze.",
                    UniqueBuffType.OnHitStatus when effect.GetPayload<OnHitStatusPayload>() != null => string.Empty,
                    UniqueBuffType.DamageRedirect when effect.GetPayload<DamageRedirectPayload>() != null => string.Empty,
                    UniqueBuffType.AuraStatus when aura != null => string.Empty,
                    UniqueBuffType.AuraStatus =>
                        "Utrzymuje aurę nakładającą status na jednostki w jej zasięgu.",
                    _ => "Nadaje unikalny efekt bojowy."
                };
                Append(result, description);
            }

            var redirect = effect.GetPayload<DamageRedirectPayload>();
            if (redirect?.RedirectPercent > 0f)
            {
                if (outgoingRedirect != null
                    && !string.IsNullOrWhiteSpace(redirect.ProtectedCharacterDisplayName))
                {
                    Append(
                        result,
                        $"Przyjmuje {FormatPercent(redirect.RedirectPercent)} obrażeń " +
                        $"przekierowanych z {redirect.ProtectedCharacterDisplayName}.");
                }
                else
                {
                    var protector = string.IsNullOrWhiteSpace(redirect.ProtectorDisplayName)
                        ? "rzucającego"
                        : redirect.ProtectorDisplayName;
                    Append(
                        result,
                        $"Przekierowuje {FormatPercent(redirect.RedirectPercent)} " +
                        $"obrażeń otrzymywanych przez tę postać do {protector}.");
                }
            }

            var onHit = effect.GetPayload<OnHitStatusPayload>();
            if (onHit != null && !string.IsNullOrWhiteSpace(onHit.StatusId))
            {
                var statusName = GetStatusDisplayName(onHit.StatusId);
                var chance = onHit.Chance >= .999f
                    ? "nakładają"
                    : $"mają {FormatPercent(onHit.Chance)} szansy na nałożenie";
                var statusObjectName = onHit.Chance >= .999f
                    ? statusName
                    : GetStatusGenitiveName(onHit.StatusId);
                var duration = onHit.DurationTurns > 0
                    ? $" na {FormatTurns(onHit.DurationTurns)}"
                    : string.Empty;
                Append(result, $"Trafienia {chance} {statusObjectName}{duration}.");
                if (onHit.StatusDamageMultiplier > 0f
                    || onHit.StatusDamagePerTurn > 0)
                {
                    var damage = onHit.StatusDamagePerTurn > 0
                        ? $"zadaje {onHit.StatusDamagePerTurn} obrażeń"
                        : $"zadaje {FormatPercent(onHit.StatusDamageMultiplier)} średniego ataku";
                    Append(
                        result,
                        $"{statusName} {damage} " +
                        "na początku każdej tury.");
                }
            }

            var resistance = effect.GetPayload<ResistanceModifierPayload>();
            if (resistance != null && !Mathf.Approximately(resistance.Value, 0f))
            {
                var verb = resistance.Value > 0f ? "Zwiększa" : "Zmniejsza";
                Append(
                    result,
                    $"{verb} {(resistance.ResistanceType == ResistanceType.All ? "wszystkie odporności" : UiTerms.Resistance(resistance.ResistanceType).ToLowerInvariant())} o " +
                    $"{FormatPercent(Mathf.Abs(resistance.Value))}.");
            }

            if (effect.GetPayload<ResourceRegenerationBlockPayload>() != null)
                Append(result, "Blokuje regenerację zasobu.");

            if (effect.GetPayload<CounterattackPayload>() != null)
                Append(result, "Wykonuje kontratak po ataku przeciwnika.");

            // The glossary is the source of truth for statuses whose gameplay rule
            // is not represented by a runtime payload.
            if (includeDamage && includeRule && result.Length == 0
                && TryGetGlossaryEntry(effect.EffectId, out var glossaryEntry)
                && !string.IsNullOrWhiteSpace(glossaryEntry.Description))
            {
                Append(result, glossaryEntry.Description);
            }

            if (includeDamage && includeRule && result.Length == 0
                && PassiveEffectCombatExecutionSystem.TryResolveDefinition(
                    owner,
                    effect.DefinitionId,
                    out var definition))
            {
                AppendPassiveDescriptions(result, new[] { definition });
            }

            return result.Length > 0
                ? UiTerms.Present(result.ToString(), StatusResource(effect, owner, resourceContext))
                : includeDamage && includeRule ? "Brak dodatkowego działania." : string.Empty;
        }

        private static void AppendStatusRule(
            StringBuilder result,
            PassiveEffectRuntime effect)
        {
            var rule = effect.EffectId?.ToLowerInvariant() switch
            {
                _ when effect.EffectId?.StartsWith("status.forest.demonic_plague.", StringComparison.Ordinal) == true =>
                    $"Na początku tury traci {FormatPercent(.005f * effect.Stacks)} maksymalnego zdrowia. Ent odzyskuje {(.5f * effect.Stacks).ToString("0.#", UiTerms.PolishCulture)} MP.",
                StatusEffectIds.Stun =>
                    "Nie może wykonywać akcji ani się poruszać.",
                StatusEffectIds.Freeze =>
                    "Nie może wykonywać akcji ani się poruszać. Pierwsze otrzymane obrażenia zrywają Zamrożenie i są wzmocnione.",
                StatusEffectIds.Sleep =>
                    "Nie może wykonywać akcji. Ruch budzi postać i kończy ten efekt.",
                StatusEffectIds.Paralysis =>
                    "Nie może wykonywać akcji ani się poruszać oraz nie może być celem umiejętności.",
                StatusEffectIds.Immobilize =>
                    "Nie może się poruszać ani używać umiejętności przemieszczających.",
                StatusEffectIds.Chill when !effect.StatModifiers.Any(modifier => modifier != null
                    && modifier.StatType == CharacterStatType.Speed && !Mathf.Approximately(modifier.Value, 0f)) =>
                    "Ogranicza szybkość postaci.",
                StatusEffectIds.Slow when !effect.StatModifiers.Any(modifier => modifier != null
                    && modifier.StatType == CharacterStatType.Speed && !Mathf.Approximately(modifier.Value, 0f)) => "Zmniejsza szybkość o 20%.",
                StatusEffectIds.Taunt =>
                    "Umiejętności ofensywne mogą celować wyłącznie w postać, która nałożyła Prowokację.",
                StatusEffectIds.Invisibility =>
                    "Ukryta przed wrogami. Nie może być bezpośrednio wybrana ani objęta automatycznym obszarowym celowaniem. Efekty kierowane w teren nadal mogą ją trafić.",
                StatusEffectIds.Regeneration when effect.GetPayload<PeriodicRecoveryPayload>()?.HealthPercent is not > 0f =>
                    "Przywraca zdrowie na końcu tury.",
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(rule)) Append(result, rule);
        }

        private static string BuildStatusMeta(
            PassiveEffectRuntime effect,
            CombatAuraRuntime aura = null,
            CombatDamageRedirectRuntime outgoingRedirect = null,
            int? stackCount = null)
        {
            var parts = new List<string>();
            if (aura != null) parts.Add("Aktywna aura");
            if (outgoingRedirect != null) parts.Add("Aktywne przekierowanie");
            if (HeroCombatStatusText.IsFieldSlow(effect.EffectId)) parts.Add("W obszarze Pola Osądu");
            else if (aura == null && effect.RemainingDuration > 0)
                parts.Add($"Pozostało: {effect.RemainingDuration} t.");
            else if (aura == null && effect.Lifetime == PassiveEffectLifetime.CombatScoped)
                parts.Add("Bez limitu tur w tej walce");
            var stacks = stackCount ?? effect.Stacks;
            if (stacks > 1)
                parts.Add($"{UiTerms.Stacks}: {stacks}");
            return string.Join("  •  ", parts);
        }

        private static string GetAuraTargetingName(TargetingType type)
        {
            return type switch
            {
                TargetingType.Enemies => "wrogów",
                TargetingType.Allies => "sojuszników",
                TargetingType.All => "wszystkie jednostki",
                _ => "jednostki"
            };
        }

        private string BuildItemBody(ItemInstance item, CharacterInstance owner)
        {
            // Consumable definitions may still contain legacy text such as
            // "restores 0 health". Their authoritative description is the
            // runtime effect payload below, so do not prepend stale definition
            // text for those instances.
            var result = new StringBuilder(ItemIntro(item));
            if (item is EquipmentItemInstance equipment) return BuildEquipmentDetails(equipment, owner, true);
            if (item is ConsumableItemInstance consumable)
            {
                if (consumable.IsFood)
                {
                    Append(result, $"Leczenie: +{Mathf.RoundToInt(DungeonFoodRules.HealthFraction * 100)}% maks. HP.");
                    Append(result, "Tylko poza walką.");
                    return result.ToString();
                }
                Append(result, "<b>Efekty użycia</b>");
                if (consumable.RuntimeEffects != null && consumable.RuntimeEffects.Count > 0)
                {
                    if (consumable.RuntimeEffects.Any(effect => effect?.EffectType == ConsumableRuntimeEffectType.ReturnToArungard))
                        Append(result, "Wraca do Arungardu.");
                }
                else if (ConsumableRuntimeRules.TryGetImpliedUtilityEffect(
                    consumable.ConsumableDefinition,
                    out var impliedUtilityEffect)
                    && impliedUtilityEffect.EffectType == ConsumableRuntimeEffectType.ReturnToArungard)
                {
                    Append(result, "Wraca do Arungardu.");
                }

                // Consumable effects are runtime payload, not definition data.
                // Build the same transient action used by combat so inventory,
                // stash and shop tooltips describe the actual item instance.
                var runtimeSkill = ConsumableRuntimeActionBuilder.Build(consumable);
                if (runtimeSkill != null)
                {
                    // Item tooltips outside combat still know the owning
                    // character in inventory views. Reuse those attack stats
                    // to calculate a concrete damage range for bombs.
                    var previewCharacter = owner ?? ResolvePreviewCharacter();
                    var previewCaster = previewCharacter == null
                        ? null
                        : new BattleUnit { Character = previewCharacter };
                    var compact = consumable.ConsumableDefinition?.UseType switch
                    {
                        ConsumableUseType.Potion => FormatPotionEffects(runtimeSkill),
                        ConsumableUseType.Bomb => FormatBombEffects(runtimeSkill, previewCaster),
                        _ => FormatPotionEffects(runtimeSkill)
                    };
                    if (compact != null) Append(result, compact);
                    else foreach (var effect in runtimeSkill.Effects ?? Array.Empty<SkillEffectData>())
                    {
                        if (effect != null)
                        {
                            Append(result, DescribeEffect(runtimeSkill, previewCaster, effect, false));
                        }
                    }

                    if (Application.isPlaying) Destroy(runtimeSkill);
                    else DestroyImmediate(runtimeSkill);
                }
                Append(result, ItemDetailsDividerView.MarkerLine);
                result.Append("<size=90%><color=#B7B09B>");
                if (consumable.ConsumableDefinition?.UseType is ConsumableUseType.Potion or ConsumableUseType.Bomb)
                    Append(result, $"Poziom: {consumable.ItemLevel}");
                Append(result, $"Ładunki: {consumable.CurrentCharges}/{consumable.MaxCharges}");
                if (consumable.IsBomb)
                    Append(result, $"Zasięg: {consumable.BlastRange} • Obszar: {consumable.BlastAreaSize}×{consumable.BlastAreaSize}");
                AppendConsumableCatalyst(result, consumable);
                result.Append("</color></size>");
            }
            else if (item is GalduryteItemInstance galduryte)
            {
                Append(result, "<b>Efekty po osadzeniu</b>");
                Append(result, BuildGalduryteEffectList(galduryte));
                Append(result, ItemDetailsDividerView.MarkerLine);
                result.Append("<size=90%><color=#B7B09B>");
                Append(result, $"Typ: Galduryt {(galduryte.TargetType == GalduryteTargetType.Weapon ? "broni" : "pancerza")}");
                Append(result, $"Poziom galdurytu: {galduryte.ItemLevel}");
                result.Append("</color></size>");
            }
            return result.ToString();
        }

        // The hover lists individual bonuses. Longer rules remain reachable through
        // the existing nested tooltip flow, with values encoded in each link so no
        // source-item state survives reuse of the shared tooltip.
        private const string GalduryteEffectLinkPrefix = "galduryte-effect:";

        private static string BuildGalduryteEffectList(GalduryteItemInstance item, bool interactive = true)
        {
            if (item.IsDestroyed || !item.IsIdentified || item.Effects == null || item.Effects.Count == 0)
                return item.GetEffectSummary();

            var lines = new List<string>();
            foreach (var effect in item.Effects)
            {
                if (effect == null) continue;
                var line = GalduryteRuntimeText.FormatEffectSummary(effect);
                if (interactive && GalduryteRuntimeText.HasEffectDetails(effect.EffectType))
                {
                    var id = GalduryteEffectLinkPrefix + effect.EffectType + ":" + effect.Value.ToString("R", CultureInfo.InvariantCulture);
                    var label = GalduryteRuntimeText.GetEffectName(effect.EffectType);
                    line = $"<link=\"{id}\"><color=#7EDBFF>{label}</color></link>" + line.Substring(label.Length);
                }
                // Wrapped continuations align with the text rather than the bullet.
                lines.Add("<indent=14><pos=0>• " + line + "</indent>");
            }
            return lines.Count == 0 ? item.GetEffectSummary()
                : "<line-height=125%>" + string.Join("\n", lines) + "</line-height>";
        }

        private static bool TryGetGalduryteEffectEntry(string id, out CombatTooltipGlossary.Entry entry)
        {
            entry = null;
            if (id == null || !id.StartsWith(GalduryteEffectLinkPrefix, StringComparison.Ordinal)) return false;
            var parts = id.Substring(GalduryteEffectLinkPrefix.Length).Split(':');
            if (parts.Length != 2 || !Enum.TryParse(parts[0], out GalduryteEffectType type)
                || !Enum.IsDefined(typeof(GalduryteEffectType), type)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || float.IsNaN(value) || float.IsInfinity(value)) return false;
            var description = GalduryteRuntimeText.FormatEffect(new GalduryteEffectInstance { EffectType = type, Value = value });
            if (GalduryteRuntimeText.IsAttackTrigger(type)) description += "\n\n" + GalduryteRuntimeText.AttackTriggerRules;
            entry = new CombatTooltipGlossary.Entry
            {
                Id = id,
                Title = GalduryteRuntimeText.GetEffectName(type),
                Description = description
            };
            return true;
        }

        // Shared, presentation-only content for hover and the card's pinned equipment details.
        public static string BuildEquipmentDetails(EquipmentItemInstance equipment, CharacterInstance owner)
            => BuildEquipmentDetails(equipment, owner, false);

        private static string BuildEquipmentDetails(
            EquipmentItemInstance equipment, CharacterInstance owner, bool interactiveEffects)
        {
            if (equipment?.Definition == null) return string.Empty;
            var result = new StringBuilder(ItemIntro(equipment));
                var equippedItem = owner?.Equipment?.GetItem(equipment.EquipmentSlot);
                if (equippedItem != null && !ReferenceEquals(equipment, equippedItem))
                    Append(result, $"<size=85%>Porównanie z: {equippedItem.DisplayName} (różnice bonusów)</size>");
                Append(result, "<b>Statystyki</b>");
                AppendEquipmentStats(
                    result,
                    equipment,
                    ReferenceEquals(equipment, equippedItem) ? null : equippedItem);
                AppendPassiveDescriptions(
                    result,
                    (equipment.Definition as UniqueEquipmentDefinition)?.GetEffects(equipment.ItemLevel));
                if (equipment is JewelryItemInstance jewelry)
                {
                    var effects = new StringBuilder();
                    AppendJewelryEffects(effects, jewelry);
                    if (effects.Length > 0)
                    {
                        Append(result, "<b>Efekty</b>");
                        Append(result, effects.ToString().TrimEnd());
                    }
                }
                Append(result, ItemDetailsDividerView.MarkerLine);
                result.Append("<color=#DFD7B8>");
                if (equipment is JewelryItemInstance)
                    Append(result, $"Typ: {GetEquipmentSlotDisplayName(equipment.EquipmentSlot)}");
                if (equipment.ItemLevel == equipment.RequiredLevel)
                    Append(result, $"Poziom: {equipment.ItemLevel}");
                else
                {
                    Append(result, $"Poziom przedmiotu: {equipment.ItemLevel}");
                    Append(result, $"Wymagany poziom: {equipment.RequiredLevel}");
                }
                if (equipment is not JewelryItemInstance && equipment.UpgradeProgress > 0f)
                    Append(result, $"Ulepszenie: {equipment.UpgradeProgress:P0}");
                if (!string.IsNullOrWhiteSpace(equipment.RequiredClassId))
                    Append(result, $"Wymagana klasa: {GetRequiredClassDisplayName(equipment)}");
                result.Append("</color>");
                if (owner != null && !Godmist.Systems.Items.EquipmentService.MeetsRequirements(owner, equipment))
                    Append(result, "<color=#E06C75>Postać nie spełnia wymagań.</color>");
                AppendSocketedGalduryteEffects(result, equipment, interactiveEffects);

            return UiTerms.Present(result.ToString());
        }

        private static string ItemIntro(ItemInstance item)
        {
            var quality = item is EquipmentItemInstance equipment && item is not JewelryItemInstance
                ? $" • Jakość: {GetQualityDisplayName(equipment.Quality)}" : string.Empty;
            var rarity = ItemRarityPresentation.Colorize(item.CurrentRarity, $"Rzadkość: {GetRarityDisplayName(item.CurrentRarity)}");
            var intro = $"<size=85%>{rarity}<color=#B7B09B>{quality}</color></size>\n";
            if (item is not ConsumableItemInstance && !string.IsNullOrWhiteSpace(item.Definition.Description))
                intro += $"<color=#B7B09B>{item.Definition.Description}</color>\n";
            return intro;
        }

        private string LinkItemMechanics(ItemInstance item, string body)
        {
            var intro = ItemIntro(item);
            if (!body.StartsWith(intro, StringComparison.Ordinal)) return body;
            var mechanics = LinkifyGlossary(body.Substring(intro.Length));
            // Only mechanical glossary links are interactive; flavor prose remains plain.
            mechanics = mechanics.Replace("<color=#7EDBFF>", "<color=#7EDBFF><u>")
                .Replace("</color></link>", "</u></color></link>");
            return intro + mechanics;
        }

        private static void AppendSocketedGalduryteEffects(
            StringBuilder result,
            EquipmentItemInstance equipment,
            bool interactiveEffects)
        {
            if (equipment == null || equipment is JewelryItemInstance) return;
            if (equipment.SocketCapacity <= 0 && (equipment.SocketedGaldurytes?.Count ?? 0) == 0) return;
            result.AppendLine();
            Append(result, $"Galduryty: {equipment.SocketedGaldurytes?.Count ?? 0}/{equipment.SocketCapacity}");
            var hasDisplayedGalduryte = false;
            var socketIndex = 0;
            foreach (var galduryte in equipment?.SocketedGaldurytes
                         ?? new List<GalduryteItemInstance>())
            {
                var inactive = !equipment.IsSocketActive(socketIndex++);
                if (galduryte?.IsIdentified != true) continue;
                // Link decoration depends on the surface; the displayed text does not.
                var effects = BuildGalduryteEffectList(galduryte, interactiveEffects);
                if (!string.IsNullOrWhiteSpace(effects))
                {
                    if (hasDisplayedGalduryte) result.AppendLine();
                    Append(result, $"<b>{galduryte.DisplayName}</b>" + (inactive ? " — nieaktywne gniazdo" : string.Empty));
                    Append(result, effects);
                    hasDisplayedGalduryte = true;
                }
            }
        }

        public static string GetRequiredClassDisplayName(EquipmentItemInstance equipment) =>
            equipment.RequiredClassId switch
            {
                "class.warrior" => "Wojownik",
                "class.scout" => "Zwiadowca",
                "class.mage" => "Mag",
                "class.paladin" => "Paladyn",
                _ => equipment.EquipmentDefinition?.RequiredClass?.ClassName ?? "Nieznana klasa"
            };

        /// <summary>Uses the combat payload, including duplicate efficiency and catalyst adjustments.</summary>
        public static string BuildPotionEffectSummary(ConsumableItemInstance consumable)
        {
            if (consumable == null) return string.Empty;
            var skill = ConsumableRuntimeActionBuilder.Build(consumable);
            if (skill == null) return string.Empty;
            try { return FormatPotionEffects(skill) ?? FormatBombEffects(skill, null); }
            finally { if (Application.isPlaying) Destroy(skill); else DestroyImmediate(skill); }
        }

        public static string BuildBombEffectSummary(ConsumableItemInstance consumable, CharacterInstance owner = null)
        {
            if (consumable == null) return string.Empty;
            var skill = ConsumableRuntimeActionBuilder.Build(consumable);
            if (skill == null) return string.Empty;
            var character = owner ?? ResolvePreviewCharacter();
            var caster = character == null ? null : new BattleUnit { Character = character };
            try { return FormatBombEffects(skill, caster); }
            finally { if (Application.isPlaying) Destroy(skill); else DestroyImmediate(skill); }
        }

        public static string BuildAlchemyIngredientEffects(ConsumableEffectDefinition effect, ConsumableItemDefinition definition)
        {
            if (effect == null) return "Brak efektu.";
            var item = new ConsumableItemInstance(definition) { RuntimeEffects = new()
            {
                new ConsumableEffectRuntime { EffectType = effect.EffectType, Value = effect.Value,
                    DurationTurns = effect.DurationTurns, Chance = effect.Chance, TargetModeIndex = effect.TargetModeIndex,
                    StatusId = effect.StatusId, ResistanceType = effect.ResistanceType,
                    TargetType = effect.TargetType, TargetingType = effect.TargetingType }
            }};
            return definition?.UseType == ConsumableUseType.Bomb ? BuildBombEffectSummary(item) : BuildPotionEffectSummary(item);
        }

        private static string FormatBombEffects(SkillData skill, BattleUnit caster)
        {
            var lines = new List<string>();
            foreach (var effect in skill.Effects ?? Array.Empty<SkillEffectData>())
            {
                if (effect == null) continue;
                if (effect.EffectType == EffectType.DealDamage)
                {
                    var range = Godmist.Systems.Combat.CombatSkillPreviewCalculator.CalculateDamageRange(caster, effect, skill);
                    var label = effect.DamageType switch
                    {
                        DamageType.Physical => UiTerms.Damage(effect.DamageType),
                        DamageType.Magical => UiTerms.Damage(effect.DamageType),
                        DamageType.Inevitable => UiTerms.Damage(effect.DamageType),
                        _ => null
                    };
                    if (label == null) return null;
                    var value = range.HasDamage
                        ? range.Minimum == range.Maximum ? range.Minimum.ToString() : $"{range.Minimum}–{range.Maximum}"
                        : PotionPercent(effect.DamageMultiplier) + (effect.DamageValueSource == DamageValueSource.AlchemicalPower
                            ? " mocy alchemicznej" : " średniego ataku");
                    lines.Add($"{label}: {value}");
                }
                else if (effect.EffectType == EffectType.ApplyStatus)
                {
                    var status = FormatConsumableStatus(effect, caster);
                    if (status == null) return null;
                    lines.Add(status);
                }
                else
                {
                    var modifier = FormatPotionEffects(new[] { effect });
                    if (modifier == null) return null;
                    lines.Add(modifier);
                }
            }
            return string.Join("\n", lines);
        }

        private static string FormatPotionEffects(SkillData skill) =>
            FormatPotionEffects(skill.Effects ?? Array.Empty<SkillEffectData>());

        private static string ConsumableStatusName(string id) => UiTerms.Status(id) is var name && name != id ? name : null;

        private static string FormatConsumableStatus(SkillEffectData effect, BattleUnit caster = null)
        {
            var name = ConsumableStatusName(effect.StatusId);
            if (name == null) return null;
            var parts = new List<string> { name };
            if (ConsumableRuntimeRules.IsDamageOverTime(effect.StatusId) && effect.StatusDamageMultiplier > 0)
            {
                var damage = Godmist.Systems.Combat.StatusEffectSystem.CalculateStatusDamagePerTurn(caster, effect);
                parts[0] += damage > 0 ? $": {damage} obrażeń / turę"
                    : $": {PotionPercent(effect.StatusDamageMultiplier)}"
                        + (effect.DamageValueSource == DamageValueSource.AlchemicalPower
                            ? " mocy alchemicznej / turę" : " średniego ataku / turę");
            }
            if (effect.StatusHealPercent > 0)
                parts.Add($"Leczenie: {PotionPercent(effect.StatusHealPercent)} maks. zdrowia / turę");
            if (effect.DurationTurns > 0) parts.Add(FormatConsumableDuration(effect.DurationTurns));
            if (effect.BaseChance < .9999f) parts.Add($"Szansa: {PotionPercent(effect.BaseChance)}");
            return string.Join(" • ", parts);
        }

        private static string FormatPotionEffects(IEnumerable<SkillEffectData> effects)
        {
            var remaining = new List<SkillEffectData>(effects);
            var lines = new List<string>();
            while (remaining.Count > 0)
            {
                var effect = remaining[0]; remaining.RemoveAt(0);
                if (effect == null) continue;
                var duration = effect.DurationTurns > 0 ? $" • {FormatConsumableDuration(effect.DurationTurns)}" : string.Empty;
                var chance = effect.BaseChance < .9999f ? $" • Szansa: {PotionPercent(effect.BaseChance)}" : string.Empty;
                var matching = remaining.Where(other => other != null && other.EffectType == effect.EffectType
                    && other.ModifierValue == effect.ModifierValue && other.DurationTurns == effect.DurationTurns
                    && other.ModifierOperation == effect.ModifierOperation && other.BaseChance == effect.BaseChance).ToList();
                string label, value;
                switch (effect.EffectType)
                {
                    case EffectType.HealPercent:
                        label = "Przywraca zdrowie"; value = PotionPercent(effect.PercentageValue) + " maks. HP"; break;
                    case EffectType.RestoreResourcePercent:
                        label = "Przywraca zasób"; value = PotionPercent(effect.PercentageValue) + " maks. zasobu"; break;
                    case EffectType.HealOverTimePercent:
                        label = "Regeneracja zdrowia"; value = "+" + PotionPercent(effect.PercentageValue) + " maks. / turę"; break;
                    case EffectType.RestoreResourceOverTimePercent:
                        label = UiTerms.ResourceRecovery; value = "+" + PotionPercent(effect.PercentageValue) + " maks. / turę"; break;
                    case EffectType.ModifyActionPointsPercent:
                        label = UiTerms.ActionPoints; value = "+" + PotionPercent(effect.PercentageValue) + " / turę"; break;
                    case EffectType.ApplyStatus:
                        var status = FormatConsumableStatus(effect);
                        if (status == null) return null;
                        lines.Add(status); continue;
                    case EffectType.RemoveStatus:
                        var statusName = ConsumableStatusName(effect.StatusId);
                        if (statusName == null) return null;
                        lines.Add($"Usuwa: {statusName}"); continue;
                    case EffectType.ModifyVar:
                        label = UiTerms.Variable(effect.ModifierVariableType);
                        value = FormatStatusVariableValue(effect.ModifierVariableType, effect.ModifierValue); break;
                    case EffectType.ModifyResistance:
                        var resistances = matching.Append(effect).Select(other => other.ResistanceType).Distinct().ToArray();
                        if (resistances.Length == Enum.GetValues(typeof(ResistanceType)).Length)
                        { label = UiTerms.Resistance(ResistanceType.All); foreach (var other in matching) remaining.Remove(other); }
                        else label = UiTerms.Resistance(effect.ResistanceType);
                        value = SignedPotionValue(effect.ModifierValue, true); break;
                    case EffectType.ModifyStat:
                        label = GetEquipmentStatDisplayName(effect.ModifierStatType);
                        if (effect.ModifierStatType == CharacterStatType.PhysicalDefense)
                        {
                            var magical = matching.FirstOrDefault(other => other.ModifierStatType == CharacterStatType.MagicalDefense);
                            if (magical != null) { label = UiTerms.Defenses; remaining.Remove(magical); }
                        }
                        if (effect.ModifierOperation == StatModifierOperation.Maximum)
                        {
                            lines.Add(FormatStatusStatModifier(effect.ModifierStatType, effect.ModifierOperation, effect.ModifierValue) + duration + chance);
                            continue;
                        }
                        value = FormatStatusStatValue(effect.ModifierStatType, effect.ModifierOperation, effect.ModifierValue); break;
                    default: return null; // Existing detailed descriptions handle utility and legacy payloads.
                }
                lines.Add($"{label}: {value}{duration}{chance}");
            }
            return string.Join("\n", lines);
        }

        private static string PotionPercent(float value) => (Mathf.Abs(value) * 100).ToString("0.##", UiTerms.PolishCulture) + "%";

        private static string FormatConsumableDuration(int turns) =>
            $"{turns} {(turns == 1 ? "tura" : GetTurnCountWord(turns))}";

        private static string SignedPotionValue(float value, bool percent) =>
            (percent ? value * 100 : value).ToString("+0.##;-0.##;0", UiTerms.PolishCulture) + (percent ? "%" : "");

        private static void AppendConsumableCatalyst(StringBuilder result, ConsumableItemInstance consumable)
        {
            // Final effect values, durations and charges already include the catalyst.
            if (string.IsNullOrWhiteSpace(consumable?.CatalystDisplayName)) return;
            Append(result, $"Katalizator: {GetCatalystShortName(consumable.CatalystDisplayName)}");
        }

        private static string GetCatalystShortName(string name)
        {
            var separator = name?.LastIndexOf(':') ?? -1;
            return separator >= 0 && separator + 1 < name.Length
                ? name[(separator + 1)..].Trim()
                : name;
        }

        private static string AppendTooltipExtra(string body, string extraBody)
        {
            if (string.IsNullOrWhiteSpace(extraBody)) return body;
            if (string.IsNullOrWhiteSpace(body)) return extraBody;
            return $"{body}\n{extraBody}";
        }

        private static CharacterInstance ResolvePreviewCharacter()
        {
            // Storage, loot and shop views intentionally do not own an item
            // character. Use the active campaign leader for a concrete bomb
            // damage range instead of falling back to the vague "physical
            // damage" text. This is preview-only; it never changes gameplay.
            var session = GameBootstrap.Instance?.Session;
            var leaderId = session?.PlayerParty?.ActiveLeaderId;
            if (!string.IsNullOrWhiteSpace(leaderId)
                && session?.CharactersById != null
                && session.CharactersById.TryGetValue(leaderId, out var leader))
            {
                return leader;
            }

            // A freshly created debug/campaign party can have active members
            // before its leader field is assigned. The first active member is a
            // deterministic preview source for that short initialization window.
            foreach (var memberId in session?.PlayerParty?.ActiveMemberIds
                ?? Array.Empty<string>())
            {
                if (session.CharactersById.TryGetValue(memberId, out var member))
                {
                    return member;
                }
            }

            return null;
        }

        public static string BuildItemMeta(ItemInstance item, int? contextPrice = null)
        {
            if (item?.Definition == null) return string.Empty;
            var intrinsicValue = Mathf.Max(0, item.BaseValue);
            if (intrinsicValue <= 0 && item.Definition is CraftableItemDefinition craftable)
                intrinsicValue = Mathf.Max(0, craftable.CalculatedBaseValue);
            var price = contextPrice.HasValue ? Mathf.Max(0, contextPrice.Value) : intrinsicValue;
            return $"{item.Definition.Weight:0.##} kg • <sprite name=\"gold\"> {price}"
                + (item.IsSaleProtected ? "\n<color=#E8C65A>Ochrona przed sprzedażą</color>" : string.Empty);
        }

        private static void AppendEquipmentStats(
            StringBuilder result,
            EquipmentItemInstance item,
            EquipmentItemInstance equippedItem)
        {
            var itemStats = GetEquipmentStats(item);
            var equippedStats = GetEquipmentStats(equippedItem);
            var hasAttack = itemStats.ContainsKey(CharacterStatType.MinAttack)
                || itemStats.ContainsKey(CharacterStatType.MaxAttack)
                || equippedStats.ContainsKey(CharacterStatType.MinAttack)
                || equippedStats.ContainsKey(CharacterStatType.MaxAttack);

            if (hasAttack)
            {
                var minAttack = GetStatValue(itemStats, CharacterStatType.MinAttack);
                var maxAttack = GetStatValue(itemStats, CharacterStatType.MaxAttack);
                var equippedAverage = (GetStatValue(equippedStats, CharacterStatType.MinAttack)
                    + GetStatValue(equippedStats, CharacterStatType.MaxAttack)) * 0.5f;
                var average = (minAttack + maxAttack) * 0.5f;
                Append(
                    result,
                    ItemStatColumnsView.Marker + $"Atak: {FormatAttackValue(minAttack, maxAttack)}"
                    + FormatComparisonDelta(average - equippedAverage, equippedItem != null)
                    + (equippedItem != null ? " (średni atak)" : string.Empty));
            }

            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType)))
            {
                if (stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack) continue;

                var value = GetStatValue(itemStats, stat);
                var equippedValue = GetStatValue(equippedStats, stat);
                if (Mathf.Abs(value) < 0.0001f && Mathf.Abs(equippedValue) < 0.0001f) continue;

                Append(
                    result,
                    ItemStatColumnsView.Marker + $"{GetEquipmentStatDisplayName(stat, item)}: {(value > 0 ? "+" : string.Empty)}{FormatEquipmentStatValue(stat, value)}"
                    + FormatComparisonDelta(value - equippedValue, equippedItem != null, stat));
            }
            // Percentage multipliers and caps are not flat stat bonuses. Keep
            // their units separate instead of adding them to health/attack points.
            foreach (var operation in new[] { StatModifierOperation.Percent, StatModifierOperation.Maximum })
            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType)))
            {
                var value = GetOperationValue(item, stat, operation);
                var equippedValue = GetOperationValue(equippedItem, stat, operation);
                if (Mathf.Abs(value) < .0001f && Mathf.Abs(equippedValue) < .0001f) continue;
                if (operation == StatModifierOperation.Maximum)
                    Append(result, ItemStatColumnsView.Marker + $"Limit — {GetEquipmentStatDisplayName(stat, item)}: {value:0.##}");
                else
                    Append(result, ItemStatColumnsView.Marker + $"{GetEquipmentStatDisplayName(stat, item)}: {value * 100f:+0.##;-0.##;0}%"
                        + (equippedItem == null ? string.Empty : $" (różnica {(value - equippedValue) * 100f:+0.##;-0.##;0} pp)"));
            }
        }

        private static float GetOperationValue(EquipmentItemInstance item, CharacterStatType stat, StatModifierOperation operation)
        {
            var modifiers = item?.StatModifiers?.Where(m => m != null && m.StatType == stat && m.Operation == operation).ToArray();
            if (modifiers == null || modifiers.Length == 0) return 0;
            if (operation == StatModifierOperation.Maximum) return modifiers.Min(m => m.Value);
            var additive = modifiers.Where(m => m.Stacking == StatModifierStacking.Additive).Sum(m => m.Value);
            var multiplier = 1f;
            foreach (var modifier in modifiers.Where(m => m.Stacking != StatModifierStacking.Additive)) multiplier *= Mathf.Max(0, 1 + modifier.Value);
            return (1 + additive) * multiplier - 1;
        }

        /// <summary>Upgrade deltas use the same equipment values and units as item tooltips.</summary>
        public static string BuildEquipmentUpgradeChanges(EquipmentItemInstance before, EquipmentItemInstance after, bool showCurrentValues = false)
        {
            if (before?.Definition == null || after?.Definition == null) return string.Empty;
            var result = new StringBuilder();
            var previous = GetEquipmentStats(before);
            var next = GetEquipmentStats(after);
            var weapon = before.Definition.ItemType == ItemType.Weapon;
            var stats = weapon
                ? new[] { CharacterStatType.MinAttack, CharacterStatType.MaxAttack }
                : new[] { CharacterStatType.PhysicalDefense, CharacterStatType.MagicalDefense };
            if (weapon)
            {
                var oldMin = GetStatValue(previous, CharacterStatType.MinAttack);
                var oldMax = GetStatValue(previous, CharacterStatType.MaxAttack);
                var newMin = GetStatValue(next, CharacterStatType.MinAttack);
                var newMax = GetStatValue(next, CharacterStatType.MaxAttack);
                var oldValue = FormatAttackValue(oldMin, oldMax);
                var newValue = FormatAttackValue(newMin, newMax);
                if (oldValue != newValue)
                    Append(result, $"Atak: {oldValue} → {UpgradeValue(newValue, newMin + newMax - oldMin - oldMax)}");
                else if (showCurrentValues && (Mathf.Abs(newMin) > .0001f || Mathf.Abs(newMax) > .0001f))
                    Append(result, $"Atak: {newValue}");
            }
            else foreach (var stat in stats)
            {
                var oldValue = FormatEquipmentStatValue(stat, GetStatValue(previous, stat));
                var newValue = FormatEquipmentStatValue(stat, GetStatValue(next, stat));
                if (oldValue != newValue)
                    Append(result, $"{GetEquipmentStatDisplayName(stat, before)}: {oldValue} → {UpgradeValue(newValue, GetStatValue(next, stat) - GetStatValue(previous, stat))}");
                else if (showCurrentValues && Mathf.Abs(GetStatValue(next, stat)) > .0001f)
                    Append(result, $"{GetEquipmentStatDisplayName(stat, before)}: {newValue}");
            }
            foreach (var operation in new[] { StatModifierOperation.Percent, StatModifierOperation.Maximum })
            foreach (var stat in stats)
            {
                var oldValue = GetOperationValue(before, stat, operation);
                var newValue = GetOperationValue(after, stat, operation);
                var changed = !Mathf.Approximately(oldValue, newValue);
                if (!changed && (!showCurrentValues || Mathf.Abs(newValue) < .0001f)) continue;
                var label = GetEquipmentStatDisplayName(stat, before);
                var oldText = operation == StatModifierOperation.Percent ? $"{oldValue * 100f:0.##}%" : $"{oldValue:0.##}";
                var newText = operation == StatModifierOperation.Percent ? $"{newValue * 100f:0.##}%" : $"{newValue:0.##}";
                Append(result, $"{label}{(operation == StatModifierOperation.Percent ? " (%)" : " (limit)")}: "
                    + (changed ? $"{oldText} → {UpgradeValue(newText, newValue - oldValue)}" : newText));
            }
            Append(result, showCurrentValues && before.SocketCapacity == after.SocketCapacity
                ? $"Gniazda: {after.SocketCapacity}"
                : $"Gniazda: {before.SocketCapacity} → {UpgradeValue(after.SocketCapacity.ToString(), after.SocketCapacity - before.SocketCapacity)}");
            return result.ToString();
        }

        /// <summary>Actual reforge deltas, including secondary stats, in the item tooltip's units.</summary>
        public static string BuildEquipmentReforgeChanges(EquipmentItemInstance before, EquipmentItemInstance after)
        {
            if (before?.Definition == null || after?.Definition == null) return string.Empty;
            var result = new StringBuilder();
            var previous = GetEquipmentStats(before);
            var next = GetEquipmentStats(after);
            var stats = (before.StatModifiers ?? new()).Concat(after.StatModifiers ?? new())
                .Where(m => m != null).Select(m => m.StatType).Distinct().OrderBy(s => s).ToArray();
            var oldAttack = FormatAttackValue(GetStatValue(previous, CharacterStatType.MinAttack), GetStatValue(previous, CharacterStatType.MaxAttack));
            var newAttack = FormatAttackValue(GetStatValue(next, CharacterStatType.MinAttack), GetStatValue(next, CharacterStatType.MaxAttack));
            if (oldAttack != newAttack)
            {
                var delta = GetStatValue(next, CharacterStatType.MinAttack) + GetStatValue(next, CharacterStatType.MaxAttack)
                    - GetStatValue(previous, CharacterStatType.MinAttack) - GetStatValue(previous, CharacterStatType.MaxAttack);
                Append(result, $"Atak: {oldAttack} → {UpgradeValue(newAttack, delta)}");
            }
            foreach (var stat in stats.Where(s => s is not (CharacterStatType.MinAttack or CharacterStatType.MaxAttack)))
            {
                var oldValue = FormatEquipmentStatValue(stat, GetStatValue(previous, stat));
                var newValue = FormatEquipmentStatValue(stat, GetStatValue(next, stat));
                if (oldValue != newValue)
                    Append(result, $"{GetEquipmentStatDisplayName(stat, after)}: {oldValue} → {UpgradeValue(newValue, GetStatValue(next, stat) - GetStatValue(previous, stat))}");
            }
            foreach (var operation in new[] { StatModifierOperation.Percent, StatModifierOperation.Maximum })
            foreach (var stat in stats)
            {
                var oldValue = GetOperationValue(before, stat, operation);
                var newValue = GetOperationValue(after, stat, operation);
                var oldText = operation == StatModifierOperation.Percent ? $"{oldValue * 100f:0.##}%" : $"{oldValue:0.##}";
                var newText = operation == StatModifierOperation.Percent ? $"{newValue * 100f:0.##}%" : $"{newValue:0.##}";
                if (oldText == newText) continue;
                var label = GetEquipmentStatDisplayName(stat, after);
                Append(result, $"{label}{(operation == StatModifierOperation.Percent ? " (%)" : " (limit)")}: {oldText} → {UpgradeValue(newText, newValue - oldValue)}");
            }
            return result.ToString();
        }

        private static string UpgradeValue(string text, float delta) => Mathf.Approximately(delta, 0f)
            ? text : $"<color={(delta > 0f ? "#3D9E63" : "#E06C75")}>{text}</color>";

        private static Dictionary<CharacterStatType, float> GetEquipmentStats(
            EquipmentItemInstance item)
        {
            var values = new Dictionary<CharacterStatType, float>();
            if (item?.StatModifiers == null) return values;

            foreach (var modifier in item.StatModifiers)
            {
                if (modifier == null || modifier.Operation is StatModifierOperation.Percent or StatModifierOperation.Maximum) continue;
                values.TryGetValue(modifier.StatType, out var currentValue);
                values[modifier.StatType] = currentValue + modifier.Value;
            }

            return values;
        }

        private static void AppendPassiveDescriptions(
            StringBuilder result,
            IEnumerable<PassiveEffectDefinition> definitions)
        {
            foreach (var definition in definitions ?? Array.Empty<PassiveEffectDefinition>())
            {
                if (definition == null) continue;

                var description = definition?.Tooltip?.DescriptionOverride;
                if (string.IsNullOrWhiteSpace(description))
                    description = definition?.Description;

                Append(result, description);
                Append(result, BuildPassiveMetadata(new[] { definition }, result.ToString()));
            }
        }

        internal static string BuildPassiveMetadata(IEnumerable<PassiveEffectDefinition> definitions, string describedText)
        {
            var result = new StringBuilder();
            foreach (var definition in definitions ?? Array.Empty<PassiveEffectDefinition>())
            {
                if (definition == null) continue;
                var conditionText = string.IsNullOrWhiteSpace(definition.Tooltip?.ConditionText)
                    ? DescribePassiveConditions(definition)
                    : definition.Tooltip.ConditionText;
                if (!string.IsNullOrWhiteSpace(conditionText) && !ContainsTooltipPhrase(describedText + "\n" + result, conditionText))
                    Append(result, $"Warunek: {conditionText}");

                var triggerText = string.IsNullOrWhiteSpace(definition.Tooltip?.TriggerText)
                    ? DescribePassiveTriggers(definition)
                    : definition.Tooltip.TriggerText;
                if (!string.IsNullOrWhiteSpace(triggerText) && !ContainsTooltipPhrase(describedText + "\n" + result, triggerText))
                    Append(result, $"Aktywacja: {triggerText}");
            }
            return result.ToString();
        }

        private static bool ContainsTooltipPhrase(string description, string phrase)
        {
            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(phrase)) return false;
            var plain = System.Text.RegularExpressions.Regex.Replace(description, "<[^>]*>", "");
            plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ");
            var term = phrase.Trim().TrimEnd('.');
            return term.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(plain,
                @"(?<![\p{L}\p{N}])" + System.Text.RegularExpressions.Regex.Escape(term) + @"(?![\p{L}\p{N}])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        private static void AppendJewelryModifiers(
            StringBuilder result,
            string sourceName,
            IEnumerable<JewelryModifierData> modifiers)
        {
            foreach (var modifier in modifiers ?? Array.Empty<JewelryModifierData>())
            {
                if (modifier == null || Mathf.Approximately(modifier.Value, 0f)) continue;
                // Actual stats are already included by AppendEquipmentStats.
                if (modifier.Target == JewelryModifierTarget.Stat) continue;
                var prefix = string.IsNullOrWhiteSpace(sourceName) ? string.Empty : sourceName + ": ";
                Append(result, prefix + FormatJewelryEffect(modifier));
            }
        }

        /// <summary>Uses the crafted item's actual stat payload and selected ingredient effects.</summary>
        public static string BuildJewelryCraftingSummary(JewelryItemInstance item)
        {
            if (item?.EquipmentDefinition is not JewelryItemDefinition) return string.Empty;
            var result = new StringBuilder();
            AppendEquipmentStats(result, item, null);
            AppendJewelryEffects(result, item);
            return result.ToString().Replace(ItemStatColumnsView.Marker, string.Empty).TrimEnd();
        }

        private static void AppendJewelryEffects(StringBuilder result, JewelryItemInstance item)
        {
            if (item?.EquipmentDefinition is not JewelryItemDefinition definition) return;
            var metal = definition.Metals?.FirstOrDefault(value => value != null
                && Godmist.Systems.Items.JewelryGenerationService.GetMetalRecipeKey(value) == item.MetalId);
            var gem = definition.Gems?.FirstOrDefault(value => value?.CraftingMaterial?.Id == item.GemId);
            AppendJewelryModifiers(result, metal?.Material?.DisplayName, metal?.Modifiers);
            AppendJewelryModifiers(result, item.GemDisplayName, gem?.Modifiers);
        }

        public static string BuildJewelryIngredientEffects(
            IEnumerable<JewelryModifierData> modifiers)
        {
            var source = modifiers?.Where(modifier => modifier != null).ToList()
                ?? new List<JewelryModifierData>();
            var result = new StringBuilder();
            var hasMinimumAttack = false;
            var hasMaximumAttack = false;
            var minimumAttack = 0f;
            var maximumAttack = 0f;

            foreach (var modifier in source)
            {
                if (modifier.Target != JewelryModifierTarget.Stat) continue;
                if (modifier.StatType == CharacterStatType.MinAttack)
                {
                    hasMinimumAttack = true;
                    minimumAttack += modifier.Value;
                }
                else if (modifier.StatType == CharacterStatType.MaxAttack)
                {
                    hasMaximumAttack = true;
                    maximumAttack += modifier.Value;
                }
            }

            if (hasMinimumAttack || hasMaximumAttack)
            {
                var minimum = hasMinimumAttack ? minimumAttack : maximumAttack;
                var maximum = hasMaximumAttack ? maximumAttack : minimumAttack;
                Append(result, $"Atak: {FormatAttackValue(minimum, maximum)}");
            }

            foreach (var modifier in source)
            {
                if (modifier.Target == JewelryModifierTarget.Stat)
                {
                    if (modifier.StatType is CharacterStatType.MinAttack or CharacterStatType.MaxAttack)
                        continue;

                    var isPercent = modifier.Operation == StatModifierOperation.Percent
                        || IsFractionalStat(modifier.StatType);
                    Append(result,
                        $"{GetEquipmentStatDisplayName(modifier.StatType)}: "
                        + FormatStatValue(modifier.StatType, modifier.Value, isPercent));
                    continue;
                }

                Append(result, FormatJewelryEffect(modifier));
            }

            return result.ToString();
        }

        private static string FormatJewelryEffect(JewelryModifierData modifier) => modifier.Target switch
        {
            JewelryModifierTarget.Resistance => $"{UiTerms.Resistance(modifier.ResistanceType)}: {FormatSignedPercent(modifier.Value)}",
            JewelryModifierTarget.GameplayVariable => GetJewelryGameplayVariableDisplayName(modifier.GameplayVariable, modifier.ReferenceId)
                + ": " + FormatStatusVariableValue(modifier.GameplayVariable, modifier.Value),
            _ => string.Empty
        };

        private static string GetJewelryGameplayVariableDisplayName(
            GameplayVariableType type,
            string referenceId)
        {
            return UiTerms.Variable(type, referenceId);
        }

        private static string GetDungeonThemeLocativeName(string referenceId)
        {
            return UiTerms.DungeonLocative(referenceId);
        }

        private static string DescribePassiveTriggers(
            PassiveEffectDefinition definition)
        {
            var descriptions = new List<string>();
            foreach (var trigger in definition?.Triggers
                         ?? new List<PassiveEffectTriggerDefinition>())
            {
                if (trigger == null
                    || trigger.Type == PassiveEffectTriggerType.AlwaysActive)
                {
                    continue;
                }

                descriptions.Add(trigger.Type switch
                {
                    PassiveEffectTriggerType.OnApply => "po otrzymaniu efektu",
                    PassiveEffectTriggerType.OnAttack => "przy ataku",
                    PassiveEffectTriggerType.OnHit => "po trafieniu",
                    PassiveEffectTriggerType.OnCriticalHit => "po trafieniu krytycznym",
                    PassiveEffectTriggerType.OnMiss => "po chybieniu",
                    PassiveEffectTriggerType.OnDodge => "po uniku",
                    PassiveEffectTriggerType.OnMove => "przy ruchu",
                    PassiveEffectTriggerType.OnKill => "po zabiciu celu",
                    PassiveEffectTriggerType.OnDamageTaken => "po otrzymaniu obrażeń",
                    PassiveEffectTriggerType.OnTurnStart => "na początku tury",
                    PassiveEffectTriggerType.OnTurnEnd => "na końcu tury",
                    PassiveEffectTriggerType.OnResourceSpent => "po wydaniu zasobu",
                    PassiveEffectTriggerType.OnStatusApplied => "po nałożeniu statusu",
                    PassiveEffectTriggerType.OnStatusExpired => "po wygaśnięciu statusu",
                    PassiveEffectTriggerType.OnDamageThresholdReached => "po otrzymaniu wymaganej ilości obrażeń",
                    PassiveEffectTriggerType.OnItemPickup => "po podniesieniu przedmiotu",
                    PassiveEffectTriggerType.OnHeal => "po leczeniu",
                    _ => string.Empty
                });
            }

            return string.Join(" lub ", descriptions.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct());
        }

        private static string DescribePassiveConditions(PassiveEffectDefinition definition)
        {
            if (definition?.Conditions == null || definition.Conditions.Count == 0)
                return string.Empty;

            var descriptions = new List<string>();
            foreach (var condition in definition.Conditions)
            {
                var description = DescribePassiveCondition(condition);
                if (!string.IsNullOrWhiteSpace(description))
                    descriptions.Add(description);
            }

            if (descriptions.Count == 0) return string.Empty;
            var separator = definition.ConditionCombination == PassiveEffectConditionCombination.Any
                ? " lub "
                : " i ";
            return string.Join(separator, descriptions);
        }

        private static string DescribePassiveCondition(
            PassiveEffectConditionDefinition condition)
        {
            if (condition == null || condition.Type == PassiveEffectConditionType.Always)
                return string.Empty;

            string description;
            switch (condition.Type)
            {
                case PassiveEffectConditionType.TargetHasTag:
                    description = CharacterTagUtility.TryParse(
                            condition.ReferenceId,
                            out var targetTag)
                        ? $"cel jest typu {GetCharacterTagDisplayName(targetTag)}"
                        : $"typ celu: {condition.ReferenceId}";
                    break;

                case PassiveEffectConditionType.DungeonTheme:
                    description = $"walka toczy się w {GetDungeonThemeLocativeName(condition.ReferenceId)}";
                    break;

                case PassiveEffectConditionType.AttackType:
                    description = condition.ReferenceId == nameof(AttackType.Melee) ? "tylko dla ataków wręcz"
                        : condition.ReferenceId == nameof(AttackType.Ranged) ? "tylko dla ataków dystansowych" : "dla wskazanego typu ataku";
                    break;
                case PassiveEffectConditionType.TargetHasStatus:
                case PassiveEffectConditionType.TargetHasBuff:
                    description = $"cel ma efekt: {ConsumableStatusName(condition.ReferenceId) ?? condition.ReferenceId}";
                    break;
                case PassiveEffectConditionType.CasterHasStatus:
                case PassiveEffectConditionType.CasterHasBuff:
                    description = $"postać ma efekt: {ConsumableStatusName(condition.ReferenceId) ?? condition.ReferenceId}";
                    break;

                default:
                    return string.Empty;
            }

            return condition.Negated ? $"nie ({description})" : description;
        }

        private static string GetCharacterTagDisplayName(CharacterTag tag)
        {
            return tag switch
            {
                CharacterTag.Human => "człowiek",
                CharacterTag.Beast => "bestia",
                CharacterTag.Undead => "nieumarły",
                CharacterTag.Demon => "demon",
                _ => tag.ToString()
            };
        }

        private static float GetStatValue(
            IReadOnlyDictionary<CharacterStatType, float> values,
            CharacterStatType stat)
        {
            return values != null && values.TryGetValue(stat, out var value)
                ? value
                : 0f;
        }

        private static string FormatEquipmentStatValue(CharacterStatType stat, float value)
        {
            return stat switch
            {
                CharacterStatType.CritChance => (value * 100f).ToString("0.##", UiTerms.PolishCulture) + " " + UiTerms.PercentagePoints,
                CharacterStatType.CritDamage => value.ToString("0.00;-0.00;0.00", UiTerms.PolishCulture),
                CharacterStatType.Dodge => Mathf.RoundToInt(value).ToString("0;-0;0", UiTerms.PolishCulture),
                _ => FormatEquipmentValue(value)
            };
        }

        // Equipment resolves as whole combat values. Keep the float internally for
        // calculations, but never expose binary-float fragments in item tooltips.
        private static string FormatEquipmentValue(float value) => Mathf.RoundToInt(value).ToString("0;-0;0", UiTerms.PolishCulture);

        private static string FormatValue(float value) => value.ToString("0.##;-0.##;0", UiTerms.PolishCulture);

        private static string FormatComparisonDelta(
            float delta,
            bool hasComparison,
            CharacterStatType? stat = null)
        {
            if (!hasComparison || Mathf.Abs(delta) < 0.0001f) return string.Empty;

            var color = delta > 0f ? "#3D9E63" : "#C74A45";
            var arrow = delta > 0f ? "↑" : "↓";
            var sign = delta > 0f ? "+" : "-";
            var value = stat switch
            {
                CharacterStatType.CritChance => $"{Mathf.Abs(delta) * 100f:0.##} pp",
                CharacterStatType.CritDamage => Mathf.Abs(delta).ToString("0.00", UiTerms.PolishCulture),
                CharacterStatType.Dodge => Mathf.RoundToInt(Mathf.Abs(delta)).ToString("0", UiTerms.PolishCulture),
                _ => FormatEquipmentValue(Mathf.Abs(delta))
            };
            return $" <color={color}>{arrow} {sign}{value}</color>";
        }

        private static bool IsDamageOverTime(string effectId) =>
            string.Equals(effectId, StatusEffectIds.Bleed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectId, StatusEffectIds.Poison, StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectId, StatusEffectIds.Burn, StringComparison.OrdinalIgnoreCase);

        private static string FormatEffectiveStatusDamage(
            BattleUnit owner,
            string statusId,
            int rawDamage)
        {
            return DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(
                owner,
                statusId,
                rawDamage).ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(value);
        }
    }

    /// <summary>
    /// Runtime-only lock glyph used by the tooltip shortcut footer. It keeps the
    /// hint independent from platform emoji fonts and does not require prefab
    /// or serialized sprite references.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIWorkspaceTooltipLockGlyph : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            var width = Mathf.Max(4f, rect.width);
            var height = Mathf.Max(4f, rect.height);
            var center = rect.center;
            var bodyLeft = center.x - width * .34f;
            var bodyRight = center.x + width * .34f;
            var bodyBottom = center.y - height * .36f;
            var bodyTop = center.y + height * .08f;
            AddQuad(vertices, bodyLeft, bodyRight, bodyBottom, bodyTop, color);

            var outerRadius = Mathf.Min(width * .28f, height * .3f);
            var thickness = Mathf.Max(1f, Mathf.Min(width, height) * .11f);
            var innerRadius = Mathf.Max(.5f, outerRadius - thickness);
            var shackleCenter = new Vector2(center.x, bodyTop);
            const int segments = 8;
            for (var index = 0; index < segments; index++)
            {
                var from = Mathf.PI - Mathf.PI * index / segments;
                var to = Mathf.PI - Mathf.PI * (index + 1) / segments;
                var outerFrom = shackleCenter + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * outerRadius;
                var outerTo = shackleCenter + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * outerRadius;
                var innerTo = shackleCenter + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * innerRadius;
                var innerFrom = shackleCenter + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * innerRadius;
                AddQuad(vertices, outerFrom, outerTo, innerTo, innerFrom, color);
            }
        }

        private static void AddQuad(
            VertexHelper vertices,
            float left,
            float right,
            float bottom,
            float top,
            Color tint)
        {
            AddQuad(
                vertices,
                new Vector2(left, bottom),
                new Vector2(left, top),
                new Vector2(right, top),
                new Vector2(right, bottom),
                tint);
        }

        private static void AddQuad(
            VertexHelper vertices,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Color tint)
        {
            var start = vertices.currentVertCount;
            vertices.AddVert(bottomLeft, tint, Vector2.zero);
            vertices.AddVert(topLeft, tint, Vector2.zero);
            vertices.AddVert(topRight, tint, Vector2.zero);
            vertices.AddVert(bottomRight, tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }

    public sealed class NpcLoyaltyTooltipData
    {
        public NpcLoyaltyTooltipData(
            int currentLevel,
            int maximumLevel,
            int discountPercent,
            long spentTowardNextLevel,
            int requiredForNextLevel,
            IReadOnlyList<NpcLoyaltyBreakpointPresentation> breakpoints,
            string npcName = null)
        {
            CurrentLevel = currentLevel;
            MaximumLevel = maximumLevel;
            DiscountPercent = discountPercent;
            SpentTowardNextLevel = spentTowardNextLevel;
            RequiredForNextLevel = requiredForNextLevel;
            Breakpoints = breakpoints ?? Array.Empty<NpcLoyaltyBreakpointPresentation>();
            NpcName = npcName;
        }

        public int CurrentLevel { get; }
        public int MaximumLevel { get; }
        public int DiscountPercent { get; }
        public long SpentTowardNextLevel { get; }
        public int RequiredForNextLevel { get; }
        public IReadOnlyList<NpcLoyaltyBreakpointPresentation> Breakpoints { get; }
        public string NpcName { get; }
    }

    public readonly struct NpcLoyaltyBreakpointPresentation
    {
        public NpcLoyaltyBreakpointPresentation(int level, string label, bool isUnlocked)
        {
            Level = level;
            Label = label ?? string.Empty;
            IsUnlocked = isUnlocked;
        }

        public int Level { get; }
        public string Label { get; }
        public bool IsUnlocked { get; }
    }

    public sealed class NpcLoyaltyProgressionView : MonoBehaviour
    {
        private const float MarkerCenterX = 12f;
        private const float MarkerSize = 18f;
        private const float LevelLeft = 28f;
        private const float LabelLeft = 68f;
        private const float MinimumRowHeight = 36f;
        private const float TrackPadding = 8f;
        private const float SummaryHeight = 26f;
        private const float ProgressLabelHeight = 26f;
        private const float ProgressBarHeight = 12f;
        private const float LayoutSpacing = 8f;
        private const float ContentPadding = 8f;
        private const float BottomPadding = 8f;
        private static readonly Color SuccessColor = new(.34f, .82f, .58f, 1f);
        private static readonly Color NextColor = new(.90f, .72f, .26f, 1f);
        private static readonly Color LockedColor = new(.52f, .56f, .55f, 1f);

        private readonly List<GameObject> spawnedObjects = new();
        private TextMeshProUGUI referenceText;
        private Slider npcLoyaltyProgressSliderPrefab;
        private Sprite goldIcon, barFrame, barFill;
        private NpcLoyaltyTooltipData data;
        private float contentWidth;

        private float FontSize => Mathf.Max(16f, referenceText.fontSize);
        private bool HasProgress => data != null && data.CurrentLevel < data.MaximumLevel;

        public void Configure(TextMeshProUGUI reference, Slider progressSliderPrefab,
            Sprite gold = null, Sprite frame = null, Sprite fill = null)
        {
            referenceText = reference;
            npcLoyaltyProgressSliderPrefab = progressSliderPrefab;
            goldIcon = gold; barFrame = frame; barFill = fill;
        }

        public void Bind(NpcLoyaltyTooltipData nextData, TextMeshProUGUI reference)
        {
            data = nextData;
            referenceText = reference ?? referenceText;
            Rebuild();
        }

        public static float GetPreferredWidth(NpcLoyaltyTooltipData nextData, TextMeshProUGUI reference)
        {
            if (nextData == null || reference == null) return 0f;
            var size = Mathf.Max(16f, reference.fontSize);
            var width = MeasureText(reference, Summary(nextData), size, 10000f).x;
            foreach (var breakpoint in nextData.Breakpoints)
                width = Mathf.Max(width, LabelLeft + MeasureText(reference, breakpoint.Label, size, 10000f).x);
            return Mathf.Clamp(width + 2f * ContentPadding + 32f, 420f, 560f);
        }

        private static string Summary(NpcLoyaltyTooltipData value) =>
            $"Poziom {value.CurrentLevel}/{value.MaximumLevel}    Rabat {value.DiscountPercent}%";

        private static Vector2 MeasureText(TextMeshProUGUI reference, string text, float size, float width)
        {
            var previousSize = reference.fontSize;
            var previousWrap = reference.textWrappingMode;
            try
            {
                reference.fontSize = size;
                reference.textWrappingMode = TextWrappingModes.Normal;
                return reference.GetPreferredValues(text, Mathf.Max(1f, width), 0f);
            }
            finally { reference.fontSize = previousSize; reference.textWrappingMode = previousWrap; }
        }

        private float RowHeight(NpcLoyaltyBreakpointPresentation breakpoint, float width) =>
            Mathf.Max(MinimumRowHeight, Mathf.Ceil(MeasureText(referenceText, breakpoint.Label,
                FontSize, Mathf.Max(1f, width - 2f * ContentPadding - LabelLeft - 1f)).y) + 12f);

        private float TrackHeight(float width)
        {
            var height = TrackPadding * 2f;
            if (data != null) foreach (var breakpoint in data.Breakpoints) height += RowHeight(breakpoint, width);
            return height;
        }

        public float GetPreferredHeight(float width)
        {
            if (data == null || referenceText == null) return 0f;
            if (width <= 1f) width = contentWidth > 1f ? contentWidth : GetPreferredWidth(data, referenceText) - 32f;
            return SummaryHeight + TrackHeight(width) + BottomPadding + 2f * LayoutSpacing
                + (HasProgress ? ProgressLabelHeight + ProgressBarHeight + 2f * LayoutSpacing : 0f);
        }

        private void Rebuild()
        {
            ClearSpawnedObjects();
            if (data == null || referenceText == null) return;
            contentWidth = ((RectTransform)transform.parent).rect.width;
            if (contentWidth <= 1f) contentWidth = GetPreferredWidth(data, referenceText) - 32f;
            var layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)ContentPadding, (int)ContentPadding, 0, 0);
            layout.spacing = LayoutSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            SetLayout(gameObject, GetPreferredHeight(contentWidth));
            var summary = CreateText("Summary", transform, SummaryHeight);
            summary.text = Summary(data);
            if (HasProgress) BuildProgress();
            var track = CreateRect("ProgressionTrack", transform);
            SetLayout(track.gameObject, TrackHeight(contentWidth));
            BuildTrack(track);
            SetLayout(CreateRect("BottomPadding", transform).gameObject, BottomPadding);
        }

        private void BuildProgress()
        {
            var row = CreateRect("ProgressRow", transform);
            SetLayout(row.gameObject, ProgressLabelHeight);
            var value = CreateText("ProgressValue", row, ProgressLabelHeight);
            value.text = $"{data.SpentTowardNextLevel} / {data.RequiredForNextLevel}";
            value.alignment = TextAlignmentOptions.MidlineRight;
            var valueWidth = Mathf.Ceil(MeasureText(referenceText, value.text, FontSize, 10000f).x) + 4f;
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.pivot = Vector2.one;
            value.rectTransform.sizeDelta = new Vector2(valueWidth, ProgressLabelHeight);
            value.rectTransform.anchoredPosition = Vector2.zero;
            if (goldIcon != null)
            {
                var gold = CreateImage("GoldIcon", row, goldIcon, Color.white);
                gold.preserveAspect = true;
                gold.rectTransform.anchorMin = gold.rectTransform.anchorMax = Vector2.one;
                gold.rectTransform.pivot = Vector2.one;
                gold.rectTransform.sizeDelta = new Vector2(24, 24);
                gold.rectTransform.anchoredPosition = new Vector2(-valueWidth - 8f, -1f);
            }
            var bar = CreateRect("ProgressBar", transform);
            SetLayout(bar.gameObject, ProgressBarHeight);
            var ratio = data.RequiredForNextLevel <= 0 ? 1f
                : Mathf.Clamp01(data.SpentTowardNextLevel / (float)data.RequiredForNextLevel);
            if (barFrame != null && barFill != null)
            {
                var frame = CreateImage("Frame", bar, barFrame, Color.white);
                frame.type = Image.Type.Sliced;
                Stretch(frame.rectTransform);
                var fill = CreateImage("Fill", bar, barFill, NextColor);
                Stretch(fill.rectTransform); fill.rectTransform.offsetMin = new Vector2(6, 4);
                fill.rectTransform.offsetMax = new Vector2(-6, -4);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0; fill.fillAmount = ratio;
            }
            else if (npcLoyaltyProgressSliderPrefab != null)
            {
                var slider = Instantiate(npcLoyaltyProgressSliderPrefab, bar, false);
                spawnedObjects.Add(slider.gameObject);
                slider.interactable = false; slider.minValue = 0; slider.maxValue = 1;
                slider.wholeNumbers = false; slider.value = ratio;
                Stretch((RectTransform)slider.transform);
            }
        }

        private void BuildTrack(RectTransform track)
        {
            var nextLevel = data.Breakpoints.Where(b => b.Level > data.CurrentLevel)
                .Select(b => b.Level).DefaultIfEmpty(int.MaxValue).Min();
            var heights = data.Breakpoints.Select(b => RowHeight(b, contentWidth)).ToArray();
            var top = TrackPadding;
            for (var index = 0; index < data.Breakpoints.Count; index++)
            {
                var breakpoint = data.Breakpoints[index];
                var height = heights[index];
                var center = top + height * .5f;
                var next = breakpoint.Level == nextLevel;
                if (index < heights.Length - 1)
                {
                    var line = CreateImage($"ProgressionTrackSegment_{breakpoint.Level}", track, null,
                        data.Breakpoints[index + 1].IsUnlocked ? SuccessColor * new Color(1, 1, 1, .65f)
                        : new Color(.35f, .37f, .34f, 1));
                    SetTopRect(line.rectTransform, MarkerCenterX - 1f, center, 2f,
                        height * .5f + heights[index + 1] * .5f);
                }
                var markerRect = CreateRect($"Breakpoint_{breakpoint.Level}", track);
                SetTopRect(markerRect, MarkerCenterX - MarkerSize * .5f, center - MarkerSize * .5f, MarkerSize, MarkerSize);
                var marker = markerRect.gameObject.AddComponent<SkillTreeRankMarkers>();
                marker.SetRanks(breakpoint.IsUnlocked ? 1 : 0, 1, SuccessColor, next ? NextColor : LockedColor);
                var level = CreateText($"BreakpointLevel_{breakpoint.Level}", track, height);
                level.text = breakpoint.Level.ToString();
                level.fontSize = FontSize + 2f;
                level.color = breakpoint.IsUnlocked ? SuccessColor : next ? NextColor : LockedColor;
                SetTopRect(level.rectTransform, LevelLeft, top, 32, height);
                var label = CreateText($"BreakpointLabel_{breakpoint.Level}", track, height);
                label.text = breakpoint.Label;
                label.color = referenceText.color;
                if (!breakpoint.IsUnlocked && !next) label.color = new Color(label.color.r, label.color.g, label.color.b, .78f);
                label.textWrappingMode = TextWrappingModes.Normal;
                SetTopRect(label.rectTransform, LabelLeft, top, -1, height);
                top += height;
            }
        }

        private static void SetLayout(GameObject target, float height)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleWidth = 1;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float height)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = referenceText.font; text.fontSharedMaterial = referenceText.fontSharedMaterial;
            text.fontSize = FontSize; text.color = referenceText.color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false; text.margin = Vector4.zero;
            SetLayout(rect.gameObject, height);
            return text;
        }

        private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            spawnedObjects.Add(node);
            return (RectTransform)node.transform;
        }

        private static void SetTopRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = width < 0 ? Vector2.one : Vector2.up;
            rect.pivot = Vector2.up;
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = width < 0 ? new Vector2(width, -top) : new Vector2(left + width, -top);
        }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private void ClearSpawnedObjects()
        {
            foreach (var node in spawnedObjects)
            {
                if (node == null) continue;
                node.SetActive(false);
                if (Application.isPlaying) Destroy(node); else DestroyImmediate(node);
            }
            spawnedObjects.Clear();
        }
    }

    /// <summary>
    /// Runtime-only unit summary used by world and turn-order hovers. It mirrors
    /// the party rail's information density while keeping the authored tooltip
    /// prefab free of combat-specific serialized references.
    /// </summary>
    public sealed class UIWorkspaceRichUnitTooltipView : MonoBehaviour
    {
        private const int MaximumVisibleStatuses = 6;
        internal const float RichTooltipWidth = 360f;
        private const float PortraitSize = 88f;
        private const float BarHeight = 16f;
        private const float RowHeight = 22f;
        private const float StatusIconSize = 34f;
        private const float RichTextScale = .95f;

        private readonly List<UIWorkspaceRichUnitStatusIconView> statusIcons = new();
        private TextMeshProUGUI referenceText;
        private Func<CombatStatusPresentation, Sprite> iconResolver;
        private UIWorkspaceTooltipView tooltip;
        private BattleUnit boundUnit;
        private RectTransform statusRoot;
        private TextMeshProUGUI statusHeading;
        private TextMeshProUGUI healthText;
        private TextMeshProUGUI resourceText;
        private Slider healthSlider;
        private Image shieldOverlay;
        private Slider resourceSlider;
        private Image portrait;
        private float nextRefresh;
        private bool configured;

        public void Configure(
            TextMeshProUGUI sourceText,
            Func<CombatStatusPresentation, Sprite> sourceIconResolver,
            UIWorkspaceTooltipView sourceTooltip = null)
        {
            referenceText = sourceText;
            iconResolver = sourceIconResolver;
            tooltip = sourceTooltip;
            Build();
            EnsureStatusIcons(MaximumVisibleStatuses);
            foreach (var icon in statusIcons) icon.gameObject.SetActive(false);
            configured = true;
        }

        public void Bind(BattleUnit unit, UIWorkspaceTooltipView sourceTooltip)
        {
            boundUnit = unit;
            tooltip = sourceTooltip;
            Refresh();
        }

        public float GetPreferredHeight(float width)
        {
            if (!configured) return 0f;
            var rect = transform as RectTransform;
            if (rect == null) return 0f;
            if (width > 1f) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return Mathf.Max(1f, LayoutUtility.GetPreferredHeight(rect));
        }

        private void Update()
        {
            if (!configured || boundUnit?.Character == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }

        private void Build()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            // The authored tooltip background may finish its layout one frame
            // after this runtime-composed child. A transparent local Graphic
            // gives the whole rich card a stable raycast surface once the
            // shared tooltip locks, including the status heading and row gaps.
            var hitSurface = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            hitSurface.color = Color.clear;
            hitSurface.raycastTarget = true;
            hitSurface.maskable = false;
            var root = gameObject.GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            root.spacing = 5f;
            root.padding = new RectOffset(0, 0, 0, 0);
            root.childAlignment = TextAnchor.UpperLeft;
            root.childControlWidth = true;
            root.childControlHeight = true;
            root.childForceExpandWidth = true;
            root.childForceExpandHeight = false;
            var fitter = gameObject.GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var element = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;

            var identity = CreateNode("Identity", transform);
            var identityLayout = identity.gameObject.AddComponent<HorizontalLayoutGroup>();
            identityLayout.spacing = 8f;
            identityLayout.childAlignment = TextAnchor.UpperLeft;
            identityLayout.childControlWidth = true;
            identityLayout.childControlHeight = true;
            identityLayout.childForceExpandWidth = false;
            identityLayout.childForceExpandHeight = false;
            SetHeight(identity.gameObject, PortraitSize);

            var portraitNode = CreateNode("Portrait", identity);
            SetSize(portraitNode, PortraitSize, PortraitSize);
            portrait = portraitNode.gameObject.AddComponent<Image>();
            portrait.enabled = false;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            var vitals = CreateNode("Vitals", identity);
            var vitalsLayout = vitals.gameObject.AddComponent<VerticalLayoutGroup>();
            vitalsLayout.spacing = 2f;
            vitalsLayout.childControlWidth = true;
            vitalsLayout.childControlHeight = true;
            vitalsLayout.childForceExpandWidth = true;
            vitalsLayout.childForceExpandHeight = false;
            var vitalsElement = vitals.gameObject.AddComponent<LayoutElement>();
            vitalsElement.flexibleWidth = 1f;
            vitalsElement.flexibleHeight = 1f;

            healthText = CreateText("Health", vitals, RowHeight);
            healthSlider = CreateBar("HealthBar", vitals, UIWorkspaceSliderStyle.Health);
            resourceText = CreateText("Resource", vitals, RowHeight);
            resourceSlider = CreateBar("ResourceBar", vitals, UIWorkspaceSliderStyle.Resource);

            statusHeading = CreateText("StatusHeading", transform, RowHeight);
            statusHeading.text = "Statusy";
            statusHeading.gameObject.SetActive(false);
            statusRoot = CreateNode("StatusList", transform);
            var statusLayout = statusRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            statusLayout.spacing = 4f;
            statusLayout.childAlignment = TextAnchor.MiddleLeft;
            statusLayout.childControlWidth = true;
            statusLayout.childControlHeight = true;
            statusLayout.childForceExpandWidth = false;
            statusLayout.childForceExpandHeight = false;
            SetHeight(statusRoot.gameObject, StatusIconSize);
            statusRoot.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            var character = boundUnit?.Character;
            if (character == null) return;
            if (portrait != null)
            {
                portrait.sprite = character.Definition?.UiPortraitSprite;
                portrait.enabled = portrait.sprite != null;
            }

            var maximumHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? character.CurrentHealth);
            SetBar(healthSlider, character.CurrentHealth, maximumHealth);
            if (healthText != null) healthText.text = $"HP  {CombatHealthPresentation.Format(character)}";
            CombatHealthPresentation.UpdateShieldOverlay(ref shieldOverlay, healthSlider,
                character.ShieldPool?.TotalShield ?? 0, maximumHealth);

            var resource = character.ResourcePool;
            var hasResource = resource != null && resource.MaxValue > 0;
            if (resourceText != null)
            {
                resourceText.gameObject.SetActive(hasResource);
                resourceText.text = hasResource
                    ? $"{CombatResourceColors.ResourceName(resource.ResourceType)}  {resource.CurrentValue}/{resource.MaxValue}"
                    : string.Empty;
            }
            if (resourceSlider != null)
            {
                resourceSlider.gameObject.SetActive(hasResource);
                if (hasResource)
                {
                    SetBar(resourceSlider, resource.CurrentValue, resource.MaxValue);
                    CombatResourceColors.ApplyToFill(resourceSlider, resource.ResourceType);
                }
            }

            var statuses = CombatStatusPresentationFactory.Create(
                character.PassiveEffects,
                boundUnit.ActiveAuras,
                boundUnit.OutgoingDamageRedirects,
                boundUnit);
            var visibleCount = Mathf.Min(MaximumVisibleStatuses, statuses.Count);
            EnsureStatusIcons(visibleCount);
            for (var index = 0; index < statusIcons.Count; index++)
            {
                var active = index < visibleCount;
                statusIcons[index].gameObject.SetActive(active);
                if (active) statusIcons[index].Bind(statuses[index], tooltip, iconResolver?.Invoke(statuses[index]));
            }

            statusHeading?.gameObject.SetActive(statuses.Count > 0);
            statusRoot?.gameObject.SetActive(statuses.Count > 0);
            var overflow = statuses.Count - visibleCount;
            var overflowText = statusRoot?.Find("Overflow")?.GetComponent<TextMeshProUGUI>();
            if (overflowText != null)
            {
                overflowText.gameObject.SetActive(overflow > 0);
                overflowText.text = overflow > 0 ? $"+{overflow}" : string.Empty;
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        private void EnsureStatusIcons(int count)
        {
            while (statusIcons.Count < count)
            {
                var node = CreateNode("Status", statusRoot);
                SetSize(node, StatusIconSize, StatusIconSize);
                var icon = node.gameObject.AddComponent<Image>();
                // Runtime-composed children do not inherit the prefab's authored
                // stretch anchors. Without an explicit rect the default Image
                // size (100x100) extends below the 34x34 layout slot, making the
                // visible status row larger than tooltipRect and putting the
                // counter outside the card's raycast geometry.
                Stretch(icon.rectTransform, 0f, 0f, 0f, 0f);
                icon.raycastTarget = true;
                icon.preserveAspect = true;
                var stack = CreateText("Stack", node, 14f);
                Stretch(stack.rectTransform, 0f, 0f, 0f, 0f);
                stack.alignment = TextAlignmentOptions.BottomRight;
                stack.fontSize = Mathf.Max(10f, stack.fontSize - 3f);
                stack.raycastTarget = false;
                var duration = CreateText("Duration", node, 14f);
                Stretch(duration.rectTransform, 0f, 0f, 0f, 0f);
                duration.alignment = TextAlignmentOptions.BottomLeft;
                duration.fontSize = Mathf.Max(10f, duration.fontSize - 4f);
                duration.raycastTarget = false;
                var view = node.gameObject.AddComponent<UIWorkspaceRichUnitStatusIconView>();
                view.Configure(icon, stack, duration);
                statusIcons.Add(view);
            }

            if (statusRoot != null && statusRoot.Find("Overflow") == null)
            {
                var overflow = CreateText("Overflow", statusRoot, StatusIconSize);
                overflow.fontSize = Mathf.Max(12f, overflow.fontSize - 2f);
                overflow.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float height)
        {
            var node = CreateNode(name, parent);
            var text = node.gameObject.AddComponent<TextMeshProUGUI>();
            if (referenceText != null)
            {
                text.font = referenceText.font;
                text.fontSharedMaterial = referenceText.fontSharedMaterial;
                text.fontSize = Mathf.Max(16f, referenceText.fontSize * RichTextScale);
                text.color = referenceText.color;
            }
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            SetHeight(node.gameObject, height);
            return text;
        }

        private Slider CreateBar(string name, Transform parent, UIWorkspaceSliderStyle style)
        {
            var slider = tooltip?.InstantiateNpcLoyaltyProgressSlider(parent);
            if (slider == null) return null;

            slider.gameObject.name = name;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.GetComponent<UIWorkspaceSliderStyleApplier>()?.SetStyle(style);
            SetHeight(slider.gameObject, BarHeight);
            return slider;
        }

        private static void SetBar(Slider slider, float value, float maximum)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = Mathf.Max(1f, maximum);
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, slider.maxValue));
        }

        private static RectTransform CreateNode(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            return node.transform as RectTransform;
        }

        private static void SetHeight(GameObject node, float height)
        {
            var element = node.GetComponent<LayoutElement>() ?? node.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            element.flexibleWidth = 1f;
        }

        private static void SetSize(RectTransform rect, float width, float height)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            element.minHeight = element.preferredHeight = height;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }

    internal sealed class UIWorkspaceRichUnitStatusIconView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private Image icon;
        private TextMeshProUGUI stackText;
        private TextMeshProUGUI durationText;
        private CombatStatusPresentation presentation;
        private UIWorkspaceTooltipView tooltip;

        public void Configure(Image sourceIcon, TextMeshProUGUI sourceStack, TextMeshProUGUI sourceDuration)
        {
            icon = sourceIcon;
            stackText = sourceStack;
            durationText = sourceDuration;
        }

        public void Bind(CombatStatusPresentation source, UIWorkspaceTooltipView sourceTooltip, Sprite sourceSprite)
        {
            presentation = source;
            tooltip = sourceTooltip;
            if (icon != null)
            {
                icon.sprite = sourceSprite;
                icon.enabled = sourceSprite != null;
            }
            SetCounter(stackText, source.StackCount);
            SetCounter(durationText, source.Duration);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowStatus(presentation, this);
        }

        public void OnPointerExit(PointerEventData eventData) => tooltip?.NotifySourceExit(this);

        private void OnDisable() => tooltip?.NotifySourceExit(this);

        private static void SetCounter(TextMeshProUGUI text, int value)
        {
            if (text == null) return;
            text.gameObject.SetActive(value > 1);
            text.text = value > 1 ? value.ToString() : string.Empty;
        }
    }
}
