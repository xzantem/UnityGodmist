using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatTooltipView : MonoBehaviour
    {
        public static CombatTooltipView ActiveInstance { get; private set; }
        public CombatTooltipGlossary Glossary => glossary;
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image lockProgressFill;
        [SerializeField, Min(1f)] private float lockProgressWidth = 248f;
        [SerializeField, Min(1f)] private float expandedTooltipHeight = 130f;
        [SerializeField] private Graphic lockAccentGraphic;
        [SerializeField] private CombatTooltipGlossary glossary;
        [SerializeField, Min(0f)] private float hoverDelay = .25f;
        [SerializeField, Min(0f)] private float statusLockDelay = 1f;
        [SerializeField, Min(0f)] private float entryGracePeriod = .4f;
        [SerializeField] private Color lockedAccentColor = new(0.22f, 0.18f, 0.10f, 0.97f);
        [SerializeField] private bool pointerPassthrough;
        [SerializeField] private Vector2 pointerOffset;

        private readonly List<CombatTooltipView> nested = new();
        private CanvasGroup canvasGroup;
        private bool isVisible, sourceHovered, locked, isNested;
        private float shownAt;
        private float lockedAt = -1f;
        private string hoveredLinkId;
        private string pendingLinkId;
        private CombatTooltipView pendingLinkOwner;
        private float pendingLinkShownAt;
        private System.Action pendingShow;
        private object pendingSource;
        private float pendingShownAt;
        private Color defaultAccentColor;
        private bool hasDefaultAccentColor;
        private object sourceIdentity;
        private bool suppressSkillTooltipsForDrag;

        public bool IsSkillTooltipSuppressed => suppressSkillTooltipsForDrag;

        private void Awake()
        {
            EnsureCanvasGroup();
            ApplyPointerInteraction();
            CacheDefaultAccentColor();
            if (!isNested) ActiveInstance = this;
            if (!isVisible) HideLocal();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
        }

        public void ShowSkill(SkillData skill, int actionCost, Vector2 position)
        {
            if (suppressSkillTooltipsForDrag) return;
            if (skill == null) { Hide(); return; }
            QueueShow(skill, () =>
            {
                Begin(Input.mousePosition, skill);
                SetContent(skill.Icon, skill.DisplayName, BuildDescription(skill), $"AP {actionCost}  •  RES {skill.ResourceCost}");
            });
        }

        public void ShowText(string title, string description, object source)
        {
            QueueShow(source, () =>
            {
                Begin(Input.mousePosition, source);
                SetContent(null, title, description, string.Empty, compact: true);
            });
        }

        public void ShowStatus(CombatStatusPresentation presentation, Vector2 position)
        {
            var effect = presentation.Effect;
            if (effect == null) { Hide(); return; }
            QueueShow(effect, () =>
            {
                Begin(Input.mousePosition, effect);
                CombatTooltipGlossary.Entry glossaryEntry = null;
                if (glossary != null) glossary.TryGet(effect.EffectId, out glossaryEntry);
                var title = glossaryEntry?.Title ?? effect.EffectId;
                SetContent(
                    glossaryEntry?.Icon,
                    $"<link=\"{effect.EffectId}\"><color=#7EDBFF>{title}</color></link>",
                    BuildPresentedStatusPayload(presentation),
                    string.Empty,
                    compact: true);
            });
        }

        public void NotifySourceExit()
        {
            sourceHovered = false;
        }
        public void NotifyStatusSourceExit() => NotifySourceExit();

        public void Hide()
        {
            ClearNested();
            HideLocal();
        }

        public void BeginDragSuppression()
        {
            suppressSkillTooltipsForDrag = true;
            Hide();
        }

        public void EndDragSuppression()
        {
            suppressSkillTooltipsForDrag = false;
        }

        private void Begin(Vector2 position, object source)
        {
            if (isVisible && ReferenceEquals(sourceIdentity, source))
            {
                sourceHovered = true;
                return;
            }

            ClearNested();
            isVisible = true;
            sourceHovered = true;
            locked = false;
            sourceIdentity = source;
            shownAt = Time.unscaledTime;
            lockedAt = -1f;
            hoveredLinkId = null;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var group = EnsureCanvasGroup();
            group.alpha = 1f;
            ApplyPointerInteraction();
            UpdateLockVisual();
            PlaceAt(position);
        }

        private void QueueShow(object source, System.Action show)
        {
            if (isVisible && ReferenceEquals(sourceIdentity, source))
            {
                sourceHovered = true;
                return;
            }
            if (pendingShow != null && ReferenceEquals(pendingSource, source))
            {
                sourceHovered = true;
                return;
            }

            ClearNested();
            isVisible = false;
            sourceHovered = true;
            locked = false;
            pendingSource = source;
            pendingShow = show;
            pendingShownAt = Time.unscaledTime;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var group = EnsureCanvasGroup();
            group.alpha = 0f;
            ApplyPointerInteraction();
        }

        private void Update()
        {
            if (pendingShow != null)
            {
                if (!sourceHovered)
                {
                    HideLocal();
                    return;
                }
                if (Time.unscaledTime - pendingShownAt < hoverDelay) return;
                var show = pendingShow;
                pendingShow = null;
                pendingSource = null;
                show?.Invoke();
            }

            if (!isVisible) return;
            if (!locked && Time.unscaledTime - shownAt >= statusLockDelay)
            {
                locked = true;
                lockedAt = Time.unscaledTime;
            }
            UpdateLockVisual();
            if (isNested) return;
            if (!locked)
            {
                PlaceAt(Input.mousePosition);
                if (!sourceHovered && !IsPointerOverAnyTooltip()) Hide();
                return;
            }

            if (!sourceHovered && !IsPointerOverAnyTooltip()
                && Time.unscaledTime - lockedAt >= entryGracePeriod)
            {
                Hide();
                return;
            }
            RefreshLinkTooltip();
        }

        private void RefreshLinkTooltip()
        {
            var owner = GetHoveredTooltip();
            if (owner == null) return;
            var id = owner.GetHoveredLinkId();
            if (id == owner.hoveredLinkId)
            {
                TryShowPendingLink(owner, id);
                return;
            }
            owner.hoveredLinkId = id;
            ClearNestedAfter(owner);
            pendingLinkId = null;
            pendingLinkOwner = null;
            if (!string.IsNullOrWhiteSpace(id) && TryGetGlossaryTerm(id, out _))
            {
                pendingLinkId = id;
                pendingLinkOwner = owner;
                pendingLinkShownAt = Time.unscaledTime;
            }
        }

        private void TryShowPendingLink(CombatTooltipView owner, string id)
        {
            if (string.IsNullOrWhiteSpace(pendingLinkId)
                || pendingLinkOwner != owner
                || !string.Equals(pendingLinkId, id, System.StringComparison.Ordinal)
                || Time.unscaledTime - pendingLinkShownAt < hoverDelay) return;

            if (TryGetGlossaryTerm(pendingLinkId, out var entry)) CreateNested(entry);
            pendingLinkId = null;
            pendingLinkOwner = null;
        }

        private CombatTooltipView GetHoveredTooltip()
        {
            for (var i = nested.Count - 1; i >= 0; i--)
                if (nested[i] != null && nested[i].IsPointerOverTooltip()) return nested[i];
            return IsPointerOverTooltip() ? this : null;
        }

        private string GetHoveredLinkId()
        {
            return GetHoveredLinkId(titleText) ?? GetHoveredLinkId(descriptionText);
        }

        private static string GetHoveredLinkId(TextMeshProUGUI text)
        {
            if (text == null || text.textInfo.linkCount == 0) return null;
            var camera = text.canvas != null && text.canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? text.canvas.worldCamera : null;
            var index = TMP_TextUtilities.FindIntersectingLink(text, Input.mousePosition, camera);
            return index < 0 ? null : text.textInfo.linkInfo[index].GetLinkID();
        }

        private void CreateNested(CombatTooltipGlossary.Entry entry)
        {
            var clone = Instantiate(gameObject, transform.parent);
            var view = clone.GetComponent<CombatTooltipView>();
            view.isNested = true;
            view.isVisible = true;
            view.locked = false;
            view.sourceHovered = false;
            view.shownAt = Time.unscaledTime;
            view.lockedAt = -1f;
            view.gameObject.SetActive(true);
            view.ApplyPointerInteraction();
            view.UpdateLockVisual();
            view.SetContent(entry.Icon, entry.Title, entry.Description, string.Empty);
            view.PlaceAt(Input.mousePosition);
            nested.Add(view);
            ActiveInstance = this;
            RefreshOpacity();
        }

        private bool TryGetGlossaryTerm(string id, out CombatTooltipGlossary.Entry result)
        {
            if (glossary != null && glossary.TryGet(id, out result)) return true;

            result = null;
            return false;
        }

        private void ClearNestedAfter(CombatTooltipView owner)
        {
            if (pendingLinkOwner != null && pendingLinkOwner != owner)
            {
                pendingLinkId = null;
                pendingLinkOwner = null;
            }
            var keep = owner == this ? 0 : nested.IndexOf(owner) + 1;
            while (nested.Count > keep)
            {
                var view = nested[^1];
                nested.RemoveAt(nested.Count - 1);
                if (view != null) Destroy(view.gameObject);
            }
            RefreshOpacity();
        }

        private void ClearNested() => ClearNestedAfter(this);

        private void RefreshOpacity()
        {
            var all = new List<CombatTooltipView> { this };
            all.AddRange(nested);
            for (var i = 0; i < all.Count; i++)
            {
                var distance = all.Count - 1 - i;
                all[i].EnsureCanvasGroup().alpha = distance switch { <= 1 => 1f, 2 => .75f, 3 => .5f, _ => .25f };
            }
        }

        private bool IsPointerOverAnyTooltip()
        {
            if (IsPointerOverTooltip()) return true;
            foreach (var view in nested) if (view != null && view.IsPointerOverTooltip()) return true;
            return false;
        }

        private bool IsPointerOverTooltip()
        {
            if (tooltipRect == null) return false;
            var canvas = tooltipRect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(tooltipRect, Input.mousePosition, camera);
        }

        private void SetContent(
            Sprite sprite,
            string title,
            string description,
            string meta,
            bool compact = false)
        {
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (titleText != null) titleText.text = title ?? string.Empty;
            if (descriptionText != null)
            {
                descriptionText.text = glossary != null
                    ? glossary.Linkify(description ?? string.Empty)
                    : description ?? string.Empty;
            }
            if (costText != null) { costText.gameObject.SetActive(!string.IsNullOrWhiteSpace(meta)); costText.text = meta ?? string.Empty; }
            ResizeForContent(compact);
        }

        private void ResizeForContent(bool compact)
        {
            if (tooltipRect == null) return;
            titleText?.ForceMeshUpdate();
            var titleWidth = titleText != null && titleText.rectTransform.rect.width > 1f
                ? titleText.rectTransform.rect.width
                : Mathf.Max(1f, tooltipRect.rect.width - 24f - (icon != null && icon.enabled ? 48f : 0f));
            var titleHeight = titleText != null
                ? Mathf.Ceil(titleText.GetPreferredValues(titleText.text, titleWidth, 0f).y)
                : 0f;
            if (!compact)
            {
                if (descriptionText != null) descriptionText.ForceMeshUpdate();
                var descriptionHeight = descriptionText != null ? Mathf.Ceil(descriptionText.preferredHeight) : 0f;
                var metaHeight = costText != null && costText.gameObject.activeSelf ? 22f : 0f;
                tooltipRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(expandedTooltipHeight, 24f + titleHeight + descriptionHeight + metaHeight));
                return;
            }

            if (descriptionText != null) descriptionText.ForceMeshUpdate();
            var payloadHeight = descriptionText != null
                ? Mathf.Ceil(descriptionText.preferredHeight)
                : 0f;
            var height = Mathf.Max(68f, 24f + titleHeight + payloadHeight);
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void HideLocal()
        {
            isVisible = false; sourceHovered = false; locked = false; hoveredLinkId = null;
            sourceIdentity = null;
            pendingShow = null;
            pendingSource = null;
            pendingLinkId = null;
            pendingLinkOwner = null;
            if (canvasGroup != null)
            {
                ApplyPointerInteraction();
            }
            gameObject.SetActive(false);
        }

        private void UpdateLockVisual()
        {
            if (lockProgressFill != null)
            {
                lockProgressFill.gameObject.SetActive(true);
                var progress = locked
                    ? 1f
                    : Mathf.Min(
                        .96f,
                        Mathf.Clamp01(
                            (Time.unscaledTime - shownAt) / Mathf.Max(.01f, statusLockDelay)));
                lockProgressFill.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    lockProgressWidth * progress);
            }

            if (lockAccentGraphic != null)
            {
                CacheDefaultAccentColor();
                lockAccentGraphic.color = locked ? lockedAccentColor : defaultAccentColor;
            }
        }

        private void CacheDefaultAccentColor()
        {
            if (!hasDefaultAccentColor && lockAccentGraphic != null)
            {
                defaultAccentColor = lockAccentGraphic.color;
                hasDefaultAccentColor = true;
            }
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            return canvasGroup;
        }

        private void ApplyPointerInteraction()
        {
            var group = EnsureCanvasGroup();
            var canReceivePointer = !pointerPassthrough && isVisible;
            group.blocksRaycasts = canReceivePointer;
            group.interactable = canReceivePointer;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = !pointerPassthrough;
            }
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
            var anchor = new Vector2(Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, tooltipRect.anchorMin.x), Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, tooltipRect.anchorMin.y));
            var requested = pointer + offset;
            var min = parent.rect.min + Vector2.Scale(size, pivot);
            var max = parent.rect.max - Vector2.Scale(size, Vector2.one - pivot);
            tooltipRect.anchoredPosition = new Vector2(Mathf.Clamp(requested.x, min.x, max.x), Mathf.Clamp(requested.y, min.y, max.y)) - anchor;
        }

        private static string BuildDescription(SkillData skill)
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

        private static string DescribeEffect(SkillEffectData effect)
        {
            return effect.EffectType switch
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
                EffectType.ModifyStat => Workspace.UIWorkspaceTooltipView.FormatStatusStatModifier(effect.ModifierStatType, effect.ModifierOperation, effect.ModifierValue) + ".",
                EffectType.ModifyVar => Workspace.UIWorkspaceTooltipView.FormatStatusVariableModifier(effect.ModifierVariableType, effect.ModifierValue) + ".",
                _ => "Wywołuje dodatkowy efekt."
            };
        }

        private static string BuildPresentedStatusPayload(CombatStatusPresentation presentation)
        {
            var result = new StringBuilder();
            var combined = presentation.IsDamageOverTimeGroup && presentation.Effects.Count > 1
                && presentation.Aura == null
                && presentation.Effects.All(effect => effect?.GetPayload<DamageOverTimePayload>()?.RemainingTotalDamage > 0);
            if (combined)
            {
                Append(result, $"Łącznie: {presentation.NextTickDamage} obrażeń na początku następnej tury.");
                Append(result, $"Stosy: {presentation.StackCount}");
                Append(result, "Czas stosów: " + string.Join(" • ", presentation.Effects
                    .GroupBy(effect => new { effect.RemainingDuration, effect.Lifetime })
                    .OrderBy(group => group.Key.RemainingDuration)
                    .Select(group => $"{group.Key.RemainingDuration} t. ({group.Sum(effect => Mathf.Max(1, effect.Stacks))} stos.)")));
            }
            foreach (var effect in presentation.Effects)
            {
                if (effect == null) continue;
                var body = BuildStatusPayload(effect, combined);
                if (!string.IsNullOrWhiteSpace(body)) Append(result, body);
            }
            return result.ToString();
        }

        private static string BuildStatusPayload(PassiveEffectRuntime effect, bool omitDamageAndDuration = false)
        {
            var result = new StringBuilder();
            if (!omitDamageAndDuration && IsDamageOverTime(effect.EffectId) && effect.TotalDamage > 0)
                Append(result, $"Obrażenia: {effect.TotalDamage}");
            if (effect.StatusHealPercent > 0f)
                Append(result, $"Leczenie: {effect.StatusHealPercent * 100f:0.#}% maks. zdrowia");
            if (!omitDamageAndDuration && effect.RemainingDuration > 0) Append(result, $"Czas: {effect.RemainingDuration} t.");
            if (!omitDamageAndDuration && effect.Stacks > 1) Append(result, $"Stosy: {effect.Stacks}");
            foreach (var modifier in effect.StatModifiers)
            {
                if (modifier != null)
                {
                      Append(result, Workspace.UIWorkspaceTooltipView.FormatStatusStatModifier(modifier.StatType, modifier.Operation, modifier.Value));
                }
            }
            foreach (var modifier in effect.VariableModifiers)
            {
                if (modifier == null) continue;
                Append(result, Workspace.UIWorkspaceTooltipView.FormatStatusVariableModifier(modifier.VariableType, modifier.Value, modifier.ReferenceId));
            }
            if (effect.CounterattackSkill != null) Append(result, "Reakcja: kontratak");
            return result.ToString();
        }

        private static bool IsDamageOverTime(string effectId)
        {
            return string.Equals(effectId, StatusEffectIds.Bleed, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(effectId, StatusEffectIds.Poison, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(effectId, StatusEffectIds.Burn, System.StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildStatusMeta(PassiveEffectRuntime effect)
        {
            var result = new StringBuilder();
            if (effect.RemainingDuration > 0) result.Append($"Czas: {effect.RemainingDuration} t.");
            return result.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(value);
        }

        private static string FormatStatModifier(CharacterStatType statType, float value)
        {
            return statType switch
            {
                CharacterStatType.CritDamage => value.ToString("+0.00;-0.00;0.00"),
                CharacterStatType.Dodge => Mathf.RoundToInt(value).ToString("+0;-0;0"),
                _ => value.ToString("+0.##;-0.##;0")
            };
        }
    }
}
