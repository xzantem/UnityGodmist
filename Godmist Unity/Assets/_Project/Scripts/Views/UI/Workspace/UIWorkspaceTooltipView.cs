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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Scene-owned, shared hover tooltip for every UI Workspace surface.
    /// It deliberately has no glossary/link behaviour; that is introduced by a later card.
    /// </summary>
    public sealed class UIWorkspaceTooltipView : MonoBehaviour
    {
        public static UIWorkspaceTooltipView ActiveInstance { get; private set; }

        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI metaText;
        [SerializeField, Min(0f)] private float hoverDelay = .25f;
        [SerializeField, Min(64f)] private float minimumHeight = 68f;
        [SerializeField, Min(0f)] private float contentHeightPadding = 16f;
        [SerializeField] private Vector2 pointerOffset = new(16f, -16f);

        private CanvasGroup canvasGroup;
        private bool visible;
        private bool sourceHovered;
        private bool suppressForDrag;
        private object sourceIdentity;
        private object pendingSource;
        private Action pendingShow;
        private float pendingShownAt;
        private bool resizeAfterLayout;

        public bool IsSkillTooltipSuppressed => suppressForDrag;

        private void Awake()
        {
            ActiveInstance = this;
            EnsureCanvasGroup();
            HideLocal();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
        }

        private void Update()
        {
            if (pendingShow != null && sourceHovered
                && Time.unscaledTime - pendingShownAt >= hoverDelay)
            {
                var show = pendingShow;
                pendingShow = null;
                show();
            }

            if (visible)
            {
                if (!sourceHovered) HideLocal();
                else PlaceAt(Input.mousePosition);
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
            QueueShow(source ?? effect, () => SetContent(
                null,
                effect.EffectId,
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

        public void NotifySourceExit()
        {
            sourceHovered = false;
        }

        public void Hide()
        {
            HideLocal();
        }

        public void BeginDragSuppression()
        {
            suppressForDrag = true;
            HideLocal();
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
        }

        private void SetContent(Sprite sprite, string title, string body, string meta)
        {
            sourceIdentity = pendingSource;
            pendingSource = null;
            visible = true;
            transform.SetAsLastSibling();
            EnsureCanvasGroup().alpha = 1f;

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (titleText != null) titleText.text = title ?? string.Empty;
            if (bodyText != null) bodyText.text = body ?? string.Empty;
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
            sourceIdentity = null;
            pendingSource = null;
            pendingShow = null;
            resizeAfterLayout = false;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            return canvasGroup;
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
