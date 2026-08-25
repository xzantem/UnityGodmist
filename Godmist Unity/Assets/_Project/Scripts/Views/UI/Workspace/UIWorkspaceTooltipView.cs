using System;
using System.Collections.Generic;
using System.Text;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Views.UI;
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
            CacheDefaultAccentColor();
            Hide();
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

        public void ShowSkill(SkillData skill, int actionCost, object source)
        {
            if (suppressForDrag || skill == null) return;
            QueueShow(source ?? skill, () => SetContent(
                skill.Icon,
                skill.DisplayName,
                BuildSkillDescription(skill),
                $"AP {actionCost}  •  RES {skill.ResourceCost}"));
        }

        public void ShowStatus(CombatStatusPresentation presentation, object source)
        {
            var effect = presentation.Effect;
            if (effect == null) return;
            TryGetGlossaryEntry(effect.EffectId, out var glossaryEntry);
            QueueShow(source ?? effect, () => SetContent(
                glossaryEntry?.Icon,
                FormatStatusTitle(effect.EffectId, glossaryEntry?.Title),
                BuildStatusPayload(effect),
                string.Empty));
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
                item.Definition.DisplayName,
                BuildItemBody(item, owner),
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
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
            var contentRect = bodyText?.rectTransform.parent as RectTransform;
            if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            bodyText?.ForceMeshUpdate();
            metaText?.ForceMeshUpdate();
            var contentWidth = bodyText != null && bodyText.rectTransform.rect.width > 0f
                ? bodyText.rectTransform.rect.width
                : contentRect != null
                    ? Mathf.Max(1f, contentRect.rect.width)
                    : 336f;
            var iconHeight = icon != null && icon.enabled ? 40f : 0f;
            var titleHeight = titleText != null ? Mathf.Ceil(titleText.preferredHeight) : 0f;
            var headerHeight = Mathf.Max(iconHeight, titleHeight);
            var bodyHeight = bodyText != null
                ? Mathf.Ceil(bodyText.GetPreferredValues(bodyText.text, contentWidth, 0f).y)
                : 0f;
            var metaHeight = metaText != null && metaText.gameObject.activeSelf
                ? Mathf.Ceil(metaText.GetPreferredValues(metaText.text, contentWidth, 0f).y)
                : 0f;
            var sectionCount = 1 + (bodyText != null ? 1 : 0)
                + (metaText != null && metaText.gameObject.activeSelf ? 1 : 0);
            tooltipRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(minimumHeight, 20f + contentHeightPadding + headerHeight + bodyHeight + metaHeight + 4f * (sectionCount - 1)));
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

        private static string FormatStatusTitle(string effectId, string title)
        {
            var displayedTitle = string.IsNullOrWhiteSpace(title)
                ? effectId
                : title;
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

        private static string BuildSkillDescription(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill.TooltipDescription)) return skill.TooltipDescription;
            if (skill.Effects == null || skill.Effects.Count == 0) return string.Empty;
            var result = new StringBuilder();
            foreach (var effect in skill.Effects)
            {
                if (effect == null) continue;
                if (result.Length > 0) result.Append('\n');
                result.Append(DescribeEffect(effect));
            }
            return result.ToString();
        }

        private static string DescribeEffect(SkillEffectData effect) => effect.EffectType switch
        {
            EffectType.DealDamage => effect.BaseValue > 0 ? $"Zadaje {effect.BaseValue} obrażeń." : "Zadaje obrażenia.",
            EffectType.Heal => effect.BaseValue > 0 ? $"Przywraca {effect.BaseValue} zdrowia." : "Przywraca zdrowie.",
            EffectType.RestoreResource => effect.BaseValue > 0 ? $"Przywraca {effect.BaseValue} Energii." : "Przywraca Energię.",
            EffectType.Shield => effect.BaseValue > 0 ? $"Nadaje tarczę: {effect.BaseValue}." : "Nadaje tarczę.",
            EffectType.ApplyStatus => $"Nakłada status: {effect.StatusId} na {effect.DurationTurns} t.",
            EffectType.Push => $"Odpycha o {effect.DisplacementDistance} pól.",
            EffectType.Pull => $"Przyciąga o {effect.DisplacementDistance} pól.",
            EffectType.Charge => "Wykonuje szarżę.",
            EffectType.Teleport => "Teleportuje na wybrane pole.",
            EffectType.CreateObject => "Tworzy obiekt na polu.",
            EffectType.Summon => "Przyzywa jednostkę.",
            EffectType.ModifyStat => $"Modyfikuje: {effect.ModifierStatType} {effect.ModifierValue:+#;-#;0}.",
            EffectType.ModifyVar => $"Modyfikuje: {effect.ModifierVariableType} {effect.ModifierValue:+#;-#;0}.",
            _ => "Wywołuje dodatkowy efekt."
        };

        private static string BuildStatusPayload(PassiveEffectRuntime effect)
        {
            var result = new StringBuilder();
            if (IsDamageOverTime(effect.EffectId) && effect.TotalDamage > 0) Append(result, $"Obrażenia: {effect.TotalDamage}");
            if (effect.StatusHealPercent > 0f) Append(result, $"Leczenie: {effect.StatusHealPercent * 100f:0.#}% maks. zdrowia");
            if (effect.RemainingDuration > 0) Append(result, $"Czas: {effect.RemainingDuration} t.");
            if (effect.Stacks > 1) Append(result, $"Stosy: {effect.Stacks}");
            foreach (var modifier in effect.StatModifiers) Append(result, $"{modifier.StatType} {modifier.Value:+#;-#;0}");
            foreach (var modifier in effect.VariableModifiers) Append(result, $"{modifier.VariableType} {modifier.Value:+#;-#;0}");
            if (effect.CounterattackSkill != null) Append(result, "Reakcja: kontratak");
            return result.ToString();
        }

        private static string BuildItemBody(ItemInstance item, CharacterInstance owner)
        {
            var result = new StringBuilder(item.Definition.Description ?? string.Empty);
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
            }
            return result.ToString();
        }

        private static string BuildItemMeta(ItemInstance item)
        {
            var quality = item is EquipmentItemInstance equipment ? $" • {equipment.Quality}" : string.Empty;
            return $"{item.CurrentRarity}{quality}  •  Poziom przedmiotu {item.ItemLevel}  •  Waga {item.Definition.Weight:0.##} kg";
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
                    $"Atak: {FormatValue(minAttack)}–{FormatValue(maxAttack)}"
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
                    $"{stat}: {FormatStatValue(stat, value)}"
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

        private static float GetStatValue(
            IReadOnlyDictionary<CharacterStatType, float> values,
            CharacterStatType stat)
        {
            return values != null && values.TryGetValue(stat, out var value)
                ? value
                : 0f;
        }

        private static string FormatStatValue(CharacterStatType stat, float value)
        {
            return stat == CharacterStatType.CritChance
                ? $"{value * 100f:0.##}%"
                : FormatValue(value);
        }

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
                : FormatValue(Mathf.Abs(delta));
            return $" <color={color}>{arrow} {sign}{value}</color>";
        }

        private static bool IsDamageOverTime(string effectId) =>
            string.Equals(effectId, StatusEffectIds.Bleed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectId, StatusEffectIds.Poison, StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectId, StatusEffectIds.Burn, StringComparison.OrdinalIgnoreCase);

        private static void Append(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(value);
        }
    }
}
