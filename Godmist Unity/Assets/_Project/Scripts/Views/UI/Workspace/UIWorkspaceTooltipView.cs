using System;
using System.Collections.Generic;
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
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Scene-owned, shared tooltip for every UI Workspace surface.
    /// Glossary links stay inside this view so combat and workspace surfaces do not
    /// maintain separate tooltip systems.
    /// </summary>
    public sealed class UIWorkspaceTooltipView : MonoBehaviour
    {
        public static UIWorkspaceTooltipView ActiveInstance { get; private set; }

        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI metaText;
        [SerializeField] private CombatTooltipGlossary glossary;
        [SerializeField, Min(0f)] private float hoverDelay = .25f;
        [SerializeField, Min(0f)] private float lockDelay = 1f;
        [SerializeField] private Image lockProgressFill;
        [SerializeField, Min(1f)] private float lockProgressWidth = 336f;
        [SerializeField] private Graphic lockAccentGraphic;
        [SerializeField] private Color lockedAccentColor = new(.22f, .18f, .10f, .97f);
        [SerializeField, Min(0f)] private float nestedLinkDelay = .25f;
        [SerializeField, Min(0f)] private float exitGracePeriod = 1.3f;
        [SerializeField, Min(64f)] private float minimumHeight = 68f;
        [SerializeField, Min(0f)] private float contentHeightPadding = 16f;
        [SerializeField] private Vector2 pointerOffset = new(4f, -4f);

        private CanvasGroup canvasGroup;
        private readonly List<UIWorkspaceTooltipView> nested = new();
        private bool visible;
        private bool sourceHovered;
        private bool suppressForDrag;
        private bool isLocked;
        private bool isNested;
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
        private Color defaultAccentColor;
        private bool hasDefaultAccentColor;

        public bool IsSkillTooltipSuppressed => suppressForDrag;

        private void Awake()
        {
            ActiveInstance = this;
            EnsureCanvasGroup();
            ConfigureTextLayout(titleText);
            ConfigureTextLayout(bodyText);
            ConfigureTextLayout(metaText);
            CacheDefaultAccentColor();
            Hide();
        }

        private static void ConfigureTextLayout(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
        }

        private void Update()
        {
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

            if (visible)
            {
                if (!isLocked && Time.unscaledTime - shownAt >= lockDelay)
                {
                    isLocked = true;
                    lockedAt = Time.unscaledTime;
                    ApplyPointerInteraction();
                }

                UpdateLockProgressVisual();

                if (isNested) return;

                if (!isLocked)
                {
                    PlaceAt(Input.mousePosition);
                    if (!sourceHovered && !IsPointerOverAnyTooltip()) Hide();
                    return;
                }

                if (!sourceHovered && !IsPointerOverAnyTooltip()
                    && Time.unscaledTime - lockedAt >= exitGracePeriod)
                {
                    Hide();
                    return;
                }

                RefreshNestedTooltip();
                ExpireNestedExitGracePeriod();
            }
        }

        private void LateUpdate()
        {
            if (!visible || !resizeAfterLayout) return;
            resizeAfterLayout = false;
            ResizeForContent();
            PlaceAt(Input.mousePosition);
        }

        public void ShowSkill(
            SkillData skill,
            int actionCost,
            object source,
            BattleUnit caster = null)
        {
            if (suppressForDrag || skill == null) return;
            QueueShow(source ?? skill, () => SetContent(
                skill.Icon,
                string.IsNullOrWhiteSpace(skill.DisplayName)
                    ? "Przedmiot użytkowy"
                    : skill.DisplayName,
                BuildSkillDescription(skill, caster),
                BuildSkillMeta(skill, actionCost)));
        }

        public void ShowStatus(CombatStatusPresentation presentation, object source)
        {
            var effect = presentation.Effect;
            if (effect == null) return;
            TryGetGlossaryEntry(effect.EffectId, out var glossaryEntry);
            QueueShow(source ?? effect, () => SetContent(
                glossaryEntry?.Icon,
                FormatStatusTitle(
                    effect.EffectId,
                    glossaryEntry?.Title,
                    effect.DisplayName),
                BuildStatusPayload(
                    effect,
                    presentation.Aura,
                    presentation.OutgoingRedirect,
                    presentation.Owner),
                BuildStatusMeta(
                    effect,
                    presentation.Aura,
                    presentation.OutgoingRedirect)));
        }

        public void ShowText(string title, string body, object source)
        {
            QueueShow(source ?? this, () => SetContent(null, title, body, string.Empty));
        }

        public void ShowItem(ItemInstance item, CharacterInstance owner, object source)
        {
            if (item?.Definition == null) return;
            QueueShow(source ?? item, () => SetContent(
                item.Definition.Icon,
                item.DisplayName,
                BuildItemBody(item, owner),
                BuildItemMeta(item)));
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
            string extraBody)
        {
            if (item?.Definition == null) return;

            QueueShow(source ?? item, () => SetContent(
                item.Definition.Icon,
                item.DisplayName,
                AppendTooltipExtra(BuildItemBody(item, owner), extraBody),
                BuildItemMeta(item)));
        }

        public void NotifySourceExit(object source = null)
        {
            if (source != null)
            {
                // Pointer enter for the next UI element can occur before pointer exit for
                // the previous one. While a new tooltip is pending, only that new source
                // may cancel the pending hover.
                var expectedSource = pendingSource ?? sourceIdentity;
                if (!ReferenceEquals(source, expectedSource)) return;
            }

            sourceHovered = false;
        }

        public void Hide()
        {
            ClearNested();
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
            return TryGetGlossaryEntry(id, out var entry)
                ? entry.Title
                : string.Empty;
        }

        public void HideForSourceWithin(Transform root)
        {
            if (root == null
                || (!IsWithin(root, sourceIdentity) && !IsWithin(root, pendingSource)))
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
            if (visible && ReferenceEquals(sourceIdentity, source))
            {
                sourceHovered = true;
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
        }

        private void SetContent(Sprite sprite, string title, string body, string meta)
        {
            sourceIdentity = pendingSource;
            pendingSource = null;
            visible = true;
            isLocked = false;
            shownAt = Time.unscaledTime;
            lockedAt = -1f;
            hoveredLinkId = null;
            transform.SetAsLastSibling();
            EnsureCanvasGroup().alpha = 1f;
            ApplyPointerInteraction();
            UpdateLockProgressVisual();

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (titleText != null) titleText.text = title ?? string.Empty;
            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
                bodyText.text = glossary != null
                    ? glossary.Linkify(body ?? string.Empty)
                    : body ?? string.Empty;
            }
            if (metaText != null)
            {
                metaText.gameObject.SetActive(!string.IsNullOrWhiteSpace(meta));
                metaText.text = meta ?? string.Empty;
            }

            ResizeForContent();
            resizeAfterLayout = true;
            PlaceAt(Input.mousePosition);
        }

        private void ResizeForContent()
        {
            if (tooltipRect == null) return;
            Canvas.ForceUpdateCanvases();
            var contentRect = bodyText?.rectTransform.parent as RectTransform;
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
            var iconHeight = icon != null && icon.enabled ? 40f : 0f;
            var titleHeight = titleText != null
                ? Mathf.Ceil(titleText.GetPreferredValues(titleText.text, contentWidth, 0f).y)
                : 0f;
            var headerHeight = Mathf.Max(iconHeight, titleHeight);
            var headerLayout = titleText?.transform.parent?.GetComponent<LayoutElement>();
            if (headerLayout != null)
            {
                var compactHeaderHeight = Mathf.Max(24f, headerHeight);
                headerLayout.minHeight = compactHeaderHeight;
                headerLayout.preferredHeight = compactHeaderHeight;
                headerHeight = compactHeaderHeight;
            }

            var bodyHeight = bodyText != null && bodyText.gameObject.activeSelf
                ? Mathf.Ceil(bodyText.GetPreferredValues(bodyText.text, contentWidth, 0f).y)
                : 0f;
            var metaHeight = metaText != null && metaText.gameObject.activeSelf
                ? Mathf.Ceil(metaText.GetPreferredValues(metaText.text, contentWidth, 0f).y)
                : 0f;
            var sectionCount = 1 + (bodyText != null && bodyText.gameObject.activeSelf ? 1 : 0)
                + (metaText != null && metaText.gameObject.activeSelf ? 1 : 0);
            tooltipRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(minimumHeight, contentHeightPadding + headerHeight + bodyHeight + metaHeight + 6f * (sectionCount - 1)));
            if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void HideLocal()
        {
            visible = false;
            sourceHovered = false;
            isLocked = false;
            sourceIdentity = null;
            pendingSource = null;
            pendingShow = null;
            resizeAfterLayout = false;
            hoveredLinkId = null;
            pendingLinkId = null;
            pendingLinkOwner = null;
            pendingNestedExitOwner = null;
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

        private CanvasGroup EnsureCanvasGroup()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
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
            if (lockProgressFill == null) return;

            var shouldShow = visible;
            if (lockProgressFill.gameObject.activeSelf != shouldShow)
            {
                lockProgressFill.gameObject.SetActive(shouldShow);
            }

            if (!shouldShow) return;

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

            if (lockAccentGraphic != null)
            {
                CacheDefaultAccentColor();
                lockAccentGraphic.color = isLocked
                    ? lockedAccentColor
                    : defaultAccentColor;
            }
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
                CreateNestedTooltip(entry);
            }

            pendingLinkId = null;
            pendingLinkOwner = null;
        }

        private UIWorkspaceTooltipView GetHoveredTooltip()
        {
            for (var index = nested.Count - 1; index >= 0; index--)
            {
                if (nested[index] != null && nested[index].IsPointerOverTooltip())
                {
                    return nested[index];
                }
            }

            return IsPointerOverTooltip() ? this : null;
        }

        private string GetHoveredLinkId()
        {
            return GetHoveredLinkId(titleText)
                ?? GetHoveredLinkId(bodyText);
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
            var clone = Instantiate(gameObject, transform.parent);
            var view = clone.GetComponent<UIWorkspaceTooltipView>();
            view.isNested = true;
            view.visible = true;
            view.isLocked = false;
            view.sourceHovered = false;
            view.shownAt = Time.unscaledTime;
            view.lockedAt = -1f;
            view.gameObject.SetActive(true);
            view.EnsureCanvasGroup().alpha = 1f;
            view.SetContent(entry.Icon, entry.Title, entry.Description, string.Empty);
            view.ApplyPointerInteraction();
            view.UpdateLockProgressVisual();
            view.PlaceAt(Input.mousePosition);
            nested.Add(view);
            ActiveInstance = this;
            RefreshNestedOpacity();
        }

        private bool TryGetGlossaryEntry(
            string id,
            out CombatTooltipGlossary.Entry result)
        {
            if (glossary != null && glossary.TryGet(id, out result)) return true;

            result = null;
            return false;
        }

        private void ClearNestedAfter(UIWorkspaceTooltipView owner)
        {
            if (pendingLinkOwner != null && pendingLinkOwner != owner)
            {
                pendingLinkId = null;
                pendingLinkOwner = null;
            }

            var keepCount = owner == this ? 0 : nested.IndexOf(owner) + 1;
            while (nested.Count > keepCount)
            {
                var view = nested[^1];
                nested.RemoveAt(nested.Count - 1);
                if (view != null) Destroy(view.gameObject);
            }

            RefreshNestedOpacity();
        }

        private void ClearNested() => ClearNestedAfter(this);

        private bool HasNestedAfter(UIWorkspaceTooltipView owner)
        {
            var ownerIndex = owner == this ? -1 : nested.IndexOf(owner);
            return ownerIndex >= -1 && ownerIndex < nested.Count - 1;
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
                || Time.unscaledTime - pendingNestedExitStartedAt < exitGracePeriod)
            {
                return;
            }

            var owner = pendingNestedExitOwner;
            CancelNestedExitGracePeriod();
            ClearNestedAfter(owner);
        }

        private void CacheDefaultAccentColor()
        {
            if (hasDefaultAccentColor || lockAccentGraphic == null) return;

            defaultAccentColor = lockAccentGraphic.color;
            hasDefaultAccentColor = true;
        }

        private void RefreshNestedOpacity()
        {
            var views = new List<UIWorkspaceTooltipView> { this };
            views.AddRange(nested);

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

        private bool IsPointerOverAnyTooltip()
        {
            if (IsPointerOverTooltip()) return true;

            foreach (var tooltip in nested)
            {
                if (tooltip != null && tooltip.IsPointerOverTooltip()) return true;
            }

            return false;
        }

        private bool IsPointerOverTooltip()
        {
            if (tooltipRect == null) return false;

            var canvas = tooltipRect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                tooltipRect,
                Input.mousePosition,
                camera);
        }

        private static string FormatStatusTitle(
            string effectId,
            string title,
            string displayName = null)
        {
            var displayedTitle = string.IsNullOrWhiteSpace(displayName)
                ? string.IsNullOrWhiteSpace(title) ? effectId : title
                : displayName;
            return $"<link=\"{effectId}\"><color=#7EDBFF>{displayedTitle}</color></link>";
        }

        private void PlaceAt(Vector2 screenPosition)
        {
            if (tooltipRect == null || tooltipRect.parent is not RectTransform parent) return;
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

        private string BuildSkillDescription(SkillData skill, BattleUnit caster)
        {
            var result = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(skill.TooltipDescription))
            {
                result.Append(skill.TooltipDescription.Trim());
            }

            var generated = new StringBuilder();
            // Consumables use the same transient SkillData pipeline as combat
            // skills, but their range is an implementation detail of the target
            // picker. Showing it in the item tooltip is noisy and misleading.
            if (!IsRuntimeConsumable(skill))
            {
                AppendSkillTargetingSummary(generated, skill);
            }

            foreach (var effect in skill.Effects ?? Array.Empty<SkillEffectData>())
            {
                if (effect == null) continue;
                Append(generated, DescribeEffect(skill, caster, effect));
            }

            if (generated.Length > 0)
            {
                // Keep the authored skill description visually separate from the
                // calculated gameplay summary below it.
                if (result.Length > 0) result.Append("\n\n");
                result.Append(generated);
            }

            return result.ToString();
        }

        private static bool IsRuntimeConsumable(SkillData skill)
        {
            return skill != null
                && skill.SkillId != null
                && skill.SkillId.StartsWith(
                    "runtime.consumable.",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSkillMeta(SkillData skill, int actionCost)
        {
            var result = $"AP {actionCost}  •  RES {skill.ResourceCost}";
            if (skill.KnifeCost > 0) result += $"  •  SZTYLETY {skill.KnifeCost}";
            if (!skill.IsToggle) return result;

            var flatCost = Mathf.Max(0, skill.ToggleUpkeepFlatResourceCost);
            var percent = Mathf.Clamp01(skill.ToggleUpkeepMaxResourcePercent);
            if (flatCost <= 0 && percent <= 0f) return result + "  •  UTRZYMANIE 0";
            if (flatCost <= 0) return result + $"  •  UTRZYMANIE {percent * 100f:0.#}% maks.";
            if (percent <= 0f) return result + $"  •  UTRZYMANIE {flatCost}";

            return result
                + $"  •  UTRZYMANIE max({flatCost}, {percent * 100f:0.#}% maks.)";
        }

        private void AppendSkillTargetingSummary(
            StringBuilder result,
            SkillData skill)
        {
            if (skill?.TargetModes == null || skill.TargetModes.Count == 0)
            {
                return;
            }

            var minimumRange = int.MaxValue;
            var maximumRange = 0;
            var hasTarget = false;
            var hasAreaTarget = false;

            foreach (var mode in skill.TargetModes)
            {
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

            if (!hasTarget) return;

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
        }

        private string DescribeEffect(
            SkillData skill,
            BattleUnit caster,
            SkillEffectData effect)
        {
            var description = effect.EffectType switch
            {
                EffectType.DealDamage => DescribeDamage(caster, effect),
                EffectType.Heal => effect.BaseValue > 0
                    ? $"Przywraca {effect.BaseValue} zdrowia."
                    : "Przywraca zdrowie.",
                EffectType.RestoreResource => effect.BaseValue > 0
                    ? $"Przywraca {effect.BaseValue} " +
                      $"{GetResourceAccusativeName(skill?.ResourceType ?? ResourceType.None)}."
                    : $"Przywraca " +
                      $"{GetResourceAccusativeName(skill?.ResourceType ?? ResourceType.None)}.",
                EffectType.HealPercent =>
                    $"Przywraca {FormatPercent(effect.PercentageValue)} maksymalnego zdrowia.",
                EffectType.RestoreResourcePercent =>
                    $"Przywraca {FormatPercent(effect.PercentageValue)} maksymalnego " +
                    $"{GetResourceAccusativeName(skill?.ResourceType ?? ResourceType.None)}.",
                EffectType.HealOverTimePercent =>
                    $"Regeneruje {FormatPercent(effect.PercentageValue)} maksymalnego zdrowia " +
                    $"co turę{FormatEffectDuration(skill, effect)}.",
                EffectType.RestoreResourceOverTimePercent =>
                    $"Regeneruje {FormatPercent(effect.PercentageValue)} maksymalnego " +
                    $"{GetResourceAccusativeName(skill?.ResourceType ?? ResourceType.None)} " +
                    $"co turę{FormatEffectDuration(skill, effect)}.",
                EffectType.Shield => DescribeShield(caster, effect),
                EffectType.ApplyStatus => DescribeStatus(caster, effect),
                EffectType.RemoveStatus =>
                    $"Usuwa status {GetStatusDisplayName(effect.StatusId)}.",
                EffectType.Push => DescribeMovementEffect("Odpycha", effect),
                EffectType.Pull => DescribeMovementEffect("Przyciąga", effect),
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
                _ => "Wywołuje dodatkowy efekt."
            };

            return IsRuntimeConsumable(skill)
                ? description
                : AddEffectContext(description, skill, effect);
        }

        private string DescribeDamage(BattleUnit caster, SkillEffectData effect)
        {
            var range = Godmist.Systems.Combat.CombatSkillPreviewCalculator
                .CalculateDamageRange(caster, effect);
            var damageText = range.HasDamage
                ? $"{range.Minimum}–{range.Maximum}"
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

            if (effect.BonusDamageMultiplierChance > 0f)
            {
                description += $" Każde trafienie ma {FormatPercent(effect.BonusDamageMultiplierChance)} " +
                    $"szansy na obrażenia ×{FormatValue(effect.BonusDamageMultiplier)}.";
            }

            if (effect.BonusPenetration > 0f
                && (effect.DamageType == DamageType.Physical
                    || effect.DamageType == DamageType.Magical))
            {
                var resistance = effect.DamageType == DamageType.Magical
                    ? "obrony magicznej"
                    : "obrony fizycznej";
                description += $" Ignoruje dodatkowe {FormatPercent(effect.BonusPenetration)} {resistance}.";
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
                var damage = caster?.Character?.Stats == null
                    ? 0
                    : Godmist.Systems.Combat.CombatDamageValueCalculator.CalculateDamageValue(
                        caster,
                        DamageValueSource.AverageAttack,
                        effect.StatusDamageMultiplier);
                description += damage > 0
                    ? guaranteed
                        ? $", zadając {damage} obrażeń co turę"
                        : $" zadającego {damage} obrażeń co turę"
                    : guaranteed
                        ? $", zadając {FormatPercent(effect.StatusDamageMultiplier)} średniego ataku co turę"
                        : $" zadającego {FormatPercent(effect.StatusDamageMultiplier)} średniego ataku co turę";
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
            return description + ".";
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
            return $"{prefix}szarżę o {FormatDistance(effect.DisplacementDistance)}.";
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
            var statName = GetStatDisplayName(effect.ModifierStatType);
            // Runtime consumables explicitly mark percentage modifiers. Dodge
            // is the exception: its FlatAbsolute payload is a flat point value,
            // even though the base stat is normally displayed as a percentage.
            var isPercent = effect.ModifierOperation == StatModifierOperation.Percent
                || (effect.ModifierOperation != StatModifierOperation.FlatAbsolute
                    && IsPercentageStat(effect.ModifierStatType));
            var value = isPercent
                ? FormatSignedPercent(effect.ModifierValue)
                : FormatSignedValue(effect.ModifierValue);
            var maximumValue = isPercent
                ? FormatPercent(effect.ModifierValue)
                : FormatValue(effect.ModifierValue);

            return effect.ModifierOperation switch
            {
                StatModifierOperation.FlatBase =>
                    $"Zmienia bazową wartość statystyki {statName} o {value}{FormatEffectDuration(skill, effect)}.",
                StatModifierOperation.FlatAbsolute =>
                    $"Zmienia wartość statystyki {statName} o {value}{FormatEffectDuration(skill, effect)}.",
                StatModifierOperation.Percent =>
                    $"Zmienia statystykę {statName} o {value} " +
                    $"({GetStackingDisplayName(effect.ModifierStacking)}){FormatEffectDuration(skill, effect)}.",
                StatModifierOperation.Maximum =>
                    $"Ustawia maksymalną wartość statystyki {statName} na " +
                    $"{maximumValue}{FormatEffectDuration(skill, effect)}.",
                _ => $"Modyfikuje statystykę {statName} o {value}{FormatEffectDuration(skill, effect)}."
            };
        }

        private static string DescribeModifyVar(
            SkillData skill,
            SkillEffectData effect)
        {
            var variableName = GetGameplayVariableDisplayName(effect.ModifierVariableType);
            return $"Zmienia {variableName} o {FormatSignedPercent(effect.ModifierValue)} " +
                $"({GetStackingDisplayName(effect.ModifierStacking)}){FormatEffectDuration(skill, effect)}.";
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
            return $"{verb} odporność na {GetResistanceDisplayName(effect.ResistanceType)} " +
                $"o {FormatPercent(Mathf.Abs(effect.ModifierValue))}" +
                $"{FormatEffectDuration(skill, effect)}.";
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
            SkillEffectData effect)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;

            var result = description.Trim();
            if (!result.EndsWith(".", StringComparison.Ordinal)) result += ".";

            var target = GetEffectTargetDescription(skill, effect.TargetModeIndex);
            if (!string.IsNullOrWhiteSpace(target)) result += $" Cel: {target}.";

            var conditions = DescribeConditions(effect);
            if (!string.IsNullOrWhiteSpace(conditions)) result += $" Warunek: {conditions}.";

            if (effect.DelayCasterTurns > 0)
            {
                result += $" Opóźnienie efektu: {FormatTurnCount(effect.DelayCasterTurns)}.";
            }

            return result;
        }

        private string GetEffectTargetDescription(
            SkillData skill,
            int targetModeIndex)
        {
            if (skill?.TargetModes == null
                || targetModeIndex < 0
                || targetModeIndex >= skill.TargetModes.Count)
            {
                return string.Empty;
            }

            var mode = skill.TargetModes[targetModeIndex];
            if (mode == null) return string.Empty;

            var group = GetTargetingDisplayName(mode.TargetingType);
            return mode.TargetType switch
            {
                TargetType.Caster => "rzucający",
                TargetType.Single => $"pojedynczy {GetTargetingSingularName(mode.TargetingType)}",
                TargetType.All => GetTargetingPluralName(mode.TargetingType),
                TargetType.AreaAroundCaster =>
                    $"obszar wokół rzucającego, obejmujący {group}",
                TargetType.AreaAroundTile =>
                    DescribeAreaTarget(mode, group),
                TargetType.Line => $"linia obejmująca {group}",
                TargetType.Chain => $"łańcuch obejmujący {group}",
                TargetType.MultipleChosen => $"wybrane cele: {group}",
                TargetType.MultipleRandom => $"losowe cele: {group}",
                _ => "niestandardowy cel"
            };
        }

        private static string DescribeAreaTarget(
            SkillTargetModeData mode,
            string group)
        {
            var range = mode?.RelativeRange;
            if (range == null || range.Shape != RangeShape.Square)
            {
                return $"obszar wokół wskazanego pola, obejmujący {group}";
            }

            var sideLength = Mathf.Max(1, range.MaxRange - range.MinRange + 1);
            return $"obszar {sideLength}x{sideLength} wokół wskazanego pola, obejmujący {group}";
        }

        private string DescribeConditions(SkillEffectData effect)
        {
            if (effect?.Conditions == null || effect.Conditions.Count == 0)
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            foreach (var condition in effect.Conditions)
            {
                if (condition == null || condition.ConditionType == EffectConditionType.Always)
                {
                    continue;
                }

                var text = condition.ConditionType switch
                {
                    EffectConditionType.ReferencedEffectSucceeded => "poprzedni efekt zadziała",
                    EffectConditionType.ReferencedEffectDealtHealthDamage => "poprzedni efekt zada obrażenia",
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
            return $"{Mathf.Abs(value) * 100f:0.#}%";
        }

        private static string FormatSignedValue(float value)
        {
            return value > 0f
                ? $"+{FormatValue(value)}"
                : FormatValue(value);
        }

        private static string GetDamageTypeDisplayName(DamageType type)
        {
            return type switch
            {
                DamageType.Physical => "fizycznych",
                DamageType.Magical => "magicznych",
                DamageType.Inevitable => "nieuchronnych",
                DamageType.Direct => "bezpośrednich",
                DamageType.Bleed => "od krwawienia",
                DamageType.Poison => "od trucizny",
                DamageType.Burn => "od płonięcia",
                _ => type.ToString().ToLowerInvariant()
            };
        }

        private static string GetResourceAccusativeName(ResourceType type)
        {
            return type switch
            {
                ResourceType.Rage => "Furię",
                ResourceType.Momentum => "Impet",
                ResourceType.Mana => "Manę",
                _ => "zasób"
            };
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
            return type switch
            {
                ResistanceType.Bleed => "krwawienie",
                ResistanceType.Poison => "zatrucie",
                ResistanceType.Fire => "ogień",
                ResistanceType.Weakening => "osłabienie",
                ResistanceType.Suppression => "supresję",
                ResistanceType.Move => "ruch",
                _ => type.ToString().ToLowerInvariant()
            };
        }

        private static string GetGameplayVariableDisplayName(
            GameplayVariableType type,
            string referenceId = null)
        {
            return type switch
            {
                GameplayVariableType.DamageDealtPercent => "obrażenia zadawane",
                GameplayVariableType.DamageReceivedPercent => "obrażenia otrzymywane",
                GameplayVariableType.ExperienceGainPercent => "zdobywane doświadczenie",
                GameplayVariableType.GoldGainPercent => "zdobywane złoto",
                GameplayVariableType.DamageDealtPercentInDungeonTheme =>
                    string.IsNullOrWhiteSpace(referenceId)
                        ? "obrażenia zadawane w theme lochu"
                        : $"obrażenia zadawane w theme lochu {referenceId}",
                _ => type.ToString()
            };
        }

        private string GetStatusDisplayName(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId)) return "status";
            var glossaryTitle = GetGlossaryTitle(statusId);
            return string.IsNullOrWhiteSpace(glossaryTitle)
                ? ToTitleCase(statusId)
                : glossaryTitle;
        }

        private string GetStatusGenitiveName(string statusId)
        {
            return statusId?.ToLowerInvariant() switch
            {
                StatusEffectIds.Bleed => "krwawienia",
                StatusEffectIds.Poison => "zatrucia",
                StatusEffectIds.Burn => "płonięcia",
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

        private static bool IsPercentageStat(CharacterStatType statType)
        {
            return statType == CharacterStatType.CritChance
                || statType == CharacterStatType.Dodge
                || statType == CharacterStatType.WeaponAccuracy
                || statType == CharacterStatType.PhysicalDefensePenetration
                || statType == CharacterStatType.MagicalDefensePenetration;
        }

        private static string GetStatDisplayName(CharacterStatType statType)
        {
            return statType switch
            {
                CharacterStatType.MaxHealth => "zdrowia",
                CharacterStatType.MinAttack => "minimalnego ataku",
                CharacterStatType.MaxAttack => "maksymalnego ataku",
                CharacterStatType.PhysicalDefense => "obrony fizycznej",
                CharacterStatType.MagicalDefense => "obrony magicznej",
                CharacterStatType.Dodge => "uniku",
                CharacterStatType.CritChance => "szansy na trafienie krytyczne",
                CharacterStatType.CritDamage => "obrażenia krytyczne",
                CharacterStatType.Speed => "szybkości",
                CharacterStatType.ResourceRegeneration => "regeneracji zasobu",
                CharacterStatType.WeaponAccuracy => "celności broni",
                CharacterStatType.MaxResource => "maksymalnego zasobu",
                CharacterStatType.PhysicalDefensePenetration => "przebicia obrony fizycznej",
                CharacterStatType.MagicalDefensePenetration => "przebicia obrony magicznej",
                _ => statType.ToString()
            };
        }

        private static string GetStackingDisplayName(StatModifierStacking stacking)
        {
            return stacking == StatModifierStacking.Multiplicative
                ? "mnożnikowo"
                : "addytywnie";
        }

        private string BuildStatusPayload(
            PassiveEffectRuntime effect,
            CombatAuraRuntime aura = null,
            CombatDamageRedirectRuntime outgoingRedirect = null,
            BattleUnit owner = null)
        {
            var result = new StringBuilder();
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
            AppendStatusRule(result, effect);

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
            if (auraDamagePerTurn > 0)
            {
                Append(
                    result,
                    $"{GetStatusDisplayName(effect.EffectId)} zadaje " +
                    $"{FormatEffectiveStatusDamage(owner, effect.EffectId, auraDamagePerTurn)} " +
                    "obrażeń na początku każdej tury.");
            }
            else if (IsDamageOverTime(effect.EffectId)
                && damageOverTime?.RemainingTotalDamage > 0)
            {
                var damagePerTurn = damageOverTime.DamagePerTurn > 0
                    ? damageOverTime.DamagePerTurn
                    : Mathf.CeilToInt(
                        damageOverTime.RemainingTotalDamage /
                        (float)Mathf.Max(1, effect.RemainingDuration));
                Append(
                    result,
                    $"{GetStatusDisplayName(effect.EffectId)} zadaje " +
                    $"{FormatEffectiveStatusDamage(owner, effect.EffectId, damagePerTurn)} " +
                    "obrażeń na początku każdej tury.");
            }
            else if (IsDamageOverTime(effect.EffectId)
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
                if (modifier == null || Mathf.Approximately(modifier.Value, 0f)) continue;
                var isPercent = modifier.Operation == StatModifierOperation.Percent
                    || (modifier.Operation != StatModifierOperation.FlatAbsolute
                        && IsPercentageStat(modifier.StatType));
                var value = isPercent
                    ? FormatSignedPercent(modifier.Value)
                    : modifier.Value.ToString("+0.##;-0.##;0");
                Append(
                    result,
                    $"Modyfikator {GetStatDisplayName(modifier.StatType)}: {value}.");
            }

            foreach (var modifier in effect.VariableModifiers)
            {
                if (modifier == null || Mathf.Approximately(modifier.Value, 0f)) continue;
                var variableName = GetGameplayVariableDisplayName(
                    modifier.VariableType,
                    modifier.ReferenceId);
                Append(
                    result,
                    $"Modyfikator {variableName}: {FormatSignedPercent(modifier.Value)}.");
            }

            var uniqueBuff = effect.GetPayload<UniqueCombatBuffPayload>();
            if (uniqueBuff != null)
            {
                var description = uniqueBuff.BuffType switch
                {
                    UniqueBuffType.GuaranteedCriticalHit =>
                        "Następny atak zawsze trafia krytycznie.",
                    UniqueBuffType.GuaranteedDodge =>
                        "Następny atak przeciwko tej postaci zawsze chybia.",
                    UniqueBuffType.MaximumDamage =>
                        "Następny atak zada maksymalne obrażenia.",
                    UniqueBuffType.ActionPointsNextTurn =>
                        $"Zapewnia {uniqueBuff.ActionPointModifier:+#;-#;0} AP w następnej turze.",
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
                    $"{verb} odporność na " +
                    $"{GetResistanceDisplayName(resistance.ResistanceType)} o " +
                    $"{FormatPercent(Mathf.Abs(resistance.Value))}.");
            }

            if (effect.GetPayload<ResourceRegenerationBlockPayload>() != null)
                Append(result, "Blokuje regenerację zasobu.");

            if (effect.GetPayload<CounterattackPayload>() != null)
                Append(result, "Wykonuje kontratak po ataku przeciwnika.");

            // The glossary is the source of truth for statuses whose gameplay rule
            // is not represented by a runtime payload.
            if (result.Length == 0
                && TryGetGlossaryEntry(effect.EffectId, out var glossaryEntry)
                && !string.IsNullOrWhiteSpace(glossaryEntry.Description))
            {
                Append(result, glossaryEntry.Description);
            }

            return result.Length > 0
                ? result.ToString()
                : "Brak dodatkowego działania.";
        }

        private static void AppendStatusRule(
            StringBuilder result,
            PassiveEffectRuntime effect)
        {
            var rule = effect.EffectId?.ToLowerInvariant() switch
            {
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
                StatusEffectIds.Chill =>
                    "Ogranicza szybkość postaci.",
                StatusEffectIds.Taunt =>
                    "Umiejętności ofensywne mogą celować wyłącznie w postać, która nałożyła Prowokację.",
                StatusEffectIds.Invisibility =>
                    "Nie może być wybrana bezpośrednio przez wrogów ani objęta ich celowaniem All.",
                StatusEffectIds.Regeneration =>
                    "Przywraca zdrowie na końcu tury.",
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(rule)) Append(result, rule);
        }

        private static string BuildStatusMeta(
            PassiveEffectRuntime effect,
            CombatAuraRuntime aura = null,
            CombatDamageRedirectRuntime outgoingRedirect = null)
        {
            var parts = new List<string>();
            if (aura != null) parts.Add("Aktywna aura");
            if (outgoingRedirect != null) parts.Add("Aktywne przekierowanie");
            if (aura == null && effect.RemainingDuration > 0)
                parts.Add($"Pozostało: {effect.RemainingDuration} t.");
            if (effect.Stacks > 1)
                parts.Add($"Stosy: {effect.Stacks}");
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
            var result = new StringBuilder(item is ConsumableItemInstance
                ? string.Empty
                : item.Definition.Description ?? string.Empty);
            if (item is EquipmentItemInstance equipment)
            {
                Append(result, $"Slot: {equipment.EquipmentSlot}");
                Append(result, $"Wymagany poziom: {equipment.RequiredLevel}");
                if (!string.IsNullOrWhiteSpace(equipment.RequiredClassId)) Append(result, $"Wymagana klasa: {equipment.RequiredClassId}");
                if (owner != null && !Godmist.Systems.Items.EquipmentService.MeetsRequirements(owner, equipment))
                    Append(result, "<color=#E06C75>Postać nie spełnia wymagań.</color>");
                var equippedItem = owner?.Equipment?.GetItem(equipment.EquipmentSlot);
                AppendEquipmentStats(
                    result,
                    equipment,
                    ReferenceEquals(equipment, equippedItem) ? null : equippedItem);
                AppendPassiveDescriptions(
                    result,
                    equipment.EquipmentDefinition?.PassiveEffects);

                if (equipment is JewelryItemInstance jewelry
                    && equipment.EquipmentDefinition is JewelryItemDefinition jewelryDefinition)
                {
                    if (!string.IsNullOrWhiteSpace(jewelry.GemDisplayName))
                        Append(result, $"Kamień: {jewelry.GemDisplayName}");
                    if (!string.IsNullOrWhiteSpace(jewelry.GemDescription))
                        Append(result, jewelry.GemDescription);

                    var metals = jewelryDefinition.Metals != null && jewelryDefinition.Metals.Count > 0
                        ? jewelryDefinition.Metals
                        : jewelryDefinition.MetalSource?.Metals;
                    foreach (var metal in metals ?? new List<JewelryMetalData>())
                    {
                        if (metal?.Id == jewelry.MetalId)
                            AppendPassiveDescriptions(result, metal.PassiveEffects);
                    }

                }
            }
            else if (item is ConsumableItemInstance consumable)
            {
                if (consumable.RuntimeEffects != null && consumable.RuntimeEffects.Count > 0)
                {
                    foreach (var runtimeEffect in consumable.RuntimeEffects)
                    {
                        if (runtimeEffect?.EffectType == ConsumableRuntimeEffectType.ReturnToArungard)
                        {
                            Append(result, "Wraca do Arungardu.");
                        }
                    }
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
                    foreach (var effect in runtimeSkill.Effects ?? Array.Empty<SkillEffectData>())
                    {
                        if (effect != null)
                        {
                            Append(result, DescribeEffect(runtimeSkill, previewCaster, effect));
                        }
                    }

                    UnityEngine.Object.Destroy(runtimeSkill);
                }
            }
            return result.ToString();
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

        private static string BuildItemMeta(ItemInstance item)
        {
            var quality = item is EquipmentItemInstance equipment ? $" • {equipment.Quality}" : string.Empty;
            var upgrade = item is EquipmentItemInstance upgraded
                ? $"  •  Ulepszenie {upgraded.UpgradeProgress:P0}  •  Sockety {upgraded.SocketCapacity}/5"
                : string.Empty;
            return $"{item.CurrentRarity}{quality}  •  Poziom przedmiotu {item.ItemLevel}{upgrade}  •  Waga {item.Definition.Weight:0.##} kg";
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
                    $"Atak: {FormatEquipmentValue(minAttack)}–{FormatEquipmentValue(maxAttack)}"
                    + FormatComparisonDelta(average - equippedAverage, equippedItem != null));
            }

            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType)))
            {
                if (stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack) continue;

                var value = GetStatValue(itemStats, stat);
                var equippedValue = GetStatValue(equippedStats, stat);
                if (Mathf.Abs(value) < 0.0001f && Mathf.Abs(equippedValue) < 0.0001f) continue;

                Append(
                    result,
                    $"{stat}: {FormatEquipmentStatValue(stat, value)}"
                    + FormatComparisonDelta(value - equippedValue, equippedItem != null, stat));
            }
        }

        private static Dictionary<CharacterStatType, float> GetEquipmentStats(
            EquipmentItemInstance item)
        {
            var values = new Dictionary<CharacterStatType, float>();
            if (item?.StatModifiers == null) return values;

            foreach (var modifier in item.StatModifiers)
            {
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

                var conditionText = string.IsNullOrWhiteSpace(definition.Tooltip?.ConditionText)
                    ? DescribePassiveConditions(definition)
                    : definition.Tooltip.ConditionText;
                if (!string.IsNullOrWhiteSpace(conditionText))
                    Append(result, $"Warunek: {conditionText}");
            }
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
                        : $"cel ma tag {condition.ReferenceId}";
                    break;

                case PassiveEffectConditionType.DungeonTheme:
                    description = $"walka toczy się w theme lochu {condition.ReferenceId}";
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
            return stat == CharacterStatType.CritChance
                ? $"{value * 100f:0.##}%"
                : FormatEquipmentValue(value);
        }

        // Equipment resolves as whole combat values. Keep the float internally for
        // calculations, but never expose binary-float fragments in item tooltips.
        private static string FormatEquipmentValue(float value) => Mathf.RoundToInt(value).ToString("0;-0;0");

        private static string FormatValue(float value) => value.ToString("0.##;-0.##;0");

        private static string FormatComparisonDelta(
            float delta,
            bool hasComparison,
            CharacterStatType? stat = null)
        {
            if (!hasComparison || Mathf.Abs(delta) < 0.0001f) return string.Empty;

            var color = delta > 0f ? "#3D9E63" : "#C74A45";
            var arrow = delta > 0f ? "↑" : "↓";
            var sign = delta > 0f ? "+" : "-";
            var value = stat == CharacterStatType.CritChance
                ? $"{Mathf.Abs(delta) * 100f:0.##}%"
                : FormatEquipmentValue(Mathf.Abs(delta));
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
}
