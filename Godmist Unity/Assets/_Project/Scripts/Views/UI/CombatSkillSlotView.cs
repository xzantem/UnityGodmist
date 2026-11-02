using Godmist.Core.Utilities;
using System;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatSkillSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unavailableOverlay;
        [SerializeField] private TextMeshProUGUI hotkeyText;

        [SerializeField] private TextMeshProUGUI actionCostText;
        [SerializeField] private Image selectedFrame;
        private string unavailableReason;
        private bool hovered;
        [SerializeField] private TextMeshProUGUI shortcutText;
        private Action<bool> hoverChanged;
        public static string GetShortcutLabel(int index) => index >= 0 && index < 10 ? ((index + 1) % 10).ToString() : string.Empty;
        public void SetShortcut(int index)
        {
            if (shortcutText == null) return;
            shortcutText.text = GetShortcutLabel(index);
            ApplyShortcutLayout();
        }
        private ItemRarityBorderGraphic rarityBorder;
        private SkillData skill;
        private ConsumableItemInstance consumable;
        private UIWorkspaceTooltipView tooltipView;
        private Func<BattleUnit> casterProvider;
        private int actionCost;
        private Color defaultIconColor = Color.white;
        private bool capturedIconColor;
        private Material originalIconMaterial;
        private Material unavailableIconMaterial;
        private int displayedActionCost = int.MinValue;
        private static readonly Color ActiveToggleColor = new(0.35f, 1f, 0.65f, 1f);

        public void Initialize(
            SkillData sourceSkill,
            UIWorkspaceTooltipView sourceTooltipView,
            Action onClick,
            Func<BattleUnit> sourceCasterProvider = null,
            Action<bool> onHoverChanged = null)
        {
            hoverChanged = onHoverChanged;
            consumable = null;
            ItemRarityPresentation.ApplyBorder(ref rarityBorder, transform as RectTransform, (ItemInstance)null);
            skill = sourceSkill;
            tooltipView = sourceTooltipView;
            casterProvider = sourceCasterProvider;
            if (shortcutText != null) shortcutText.gameObject.SetActive(true);
            if (actionCostText != null) actionCostText.gameObject.SetActive(true);
            ApplyShortcutLayout();

            if (icon != null)
            {
                if (!capturedIconColor)
                {
                    defaultIconColor = icon.color;
                    originalIconMaterial = icon.material;
                    capturedIconColor = true;
                }
                icon.material = originalIconMaterial;
                icon.color = defaultIconColor;
                icon.sprite = skill?.Icon;
                icon.enabled = skill?.Icon != null;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        public void InitializeConsumable(ConsumableItemInstance item, UIWorkspaceTooltipView tooltip,
            Action onClick, Func<BattleUnit> caster)
        {
            Initialize(null, tooltip, onClick, caster);
            consumable = item;
            ItemRarityPresentation.ApplyBorder(ref rarityBorder, transform as RectTransform, item);
            if (icon != null)
            {
                icon.sprite = ItemIconPresentation.GetIcon(item);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = Vector2.zero;
                icon.rectTransform.anchorMax = Vector2.one;
                icon.rectTransform.offsetMin = new Vector2(5f, 5f);
                icon.rectTransform.offsetMax = new Vector2(-5f, -5f);
            }
            if (shortcutText != null) shortcutText.gameObject.SetActive(false);
            if (actionCostText != null) actionCostText.gameObject.SetActive(false);
            if (hotkeyText != null) hotkeyText.gameObject.SetActive(true);
            RestoreIconLayout();
        }

        public void RefreshAvailability(
            bool isAvailable,
            int calculatedActionCost,
            bool isActiveToggle,
            string knifeCount,
            bool selected = false,
            string reason = null)
        {
            var tooltipChanged = actionCost != calculatedActionCost || unavailableReason != reason;
            actionCost = calculatedActionCost;
            unavailableReason = reason;
            if (actionCostText != null && displayedActionCost != actionCost) { displayedActionCost = actionCost; actionCostText.text = $"{Godmist.Runtime.Combat.ActionPointUnits.Format(actionCost)} {UiTerms.ActionPointsShort}"; }
            if (selectedFrame != null) selectedFrame.enabled = selected;
            if (button != null)
            {
                button.interactable = isAvailable;
            }

            if (unavailableOverlay != null)
            {
                unavailableOverlay.enabled = !isAvailable;
            }

            if (icon != null)
            {
                var material = isAvailable || skill == null ? originalIconMaterial : GetUnavailableIconMaterial();
                if (icon.material != material) icon.material = material;
                icon.color = isActiveToggle
                    ? ActiveToggleColor
                    : defaultIconColor;
            }

            if (hovered && tooltipChanged && skill != null)
                tooltipView?.RefreshSkillAvailability(skill, actionCost, this, casterProvider?.Invoke(), unavailableReason);
            if (hotkeyText != null)
            {
                var label = knifeCount ?? string.Empty;
                if (hotkeyText.text != label) hotkeyText.text = label;
                hotkeyText.enabled = !string.IsNullOrWhiteSpace(knifeCount);
                return;
            }

        }

        public SkillData ControllerSkill => skill;
        public int ControllerActionCost => actionCost;
        public string ControllerUnavailableReason => unavailableReason;
        public bool ControllerAvailable => button != null && button.interactable;
        private Material GetUnavailableIconMaterial()
        {
            if (unavailableIconMaterial == null)
            {
                // Resources retains the shader in player builds, including masked UI variants.
                var shader = Resources.Load<Shader>("Godmist/UnavailableSkillIcon");
                if (shader == null) throw new InvalidOperationException("Unavailable skill icon shader is missing.");
                unavailableIconMaterial = new Material(shader)
                { name = "Godmist unavailable skill icon", hideFlags = HideFlags.HideAndDontSave };
            }
            return unavailableIconMaterial;
        }

        private void OnDestroy()
        {
            if (unavailableIconMaterial == null) return;
            if (icon != null && icon.material == unavailableIconMaterial) icon.material = originalIconMaterial;
            if (Application.isPlaying) Destroy(unavailableIconMaterial);
            else DestroyImmediate(unavailableIconMaterial);
        }

        public bool TryActivate()
        {
            if (button == null || !button.interactable)
            {
                return false;
            }

            button.onClick.Invoke();
            return true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            hoverChanged?.Invoke(true);
            ShowTooltip();
        }

        private void ShowTooltip()
        {
            if (consumable != null)
                tooltipView?.ShowItemWithExtra(consumable, casterProvider?.Invoke()?.Character, this,
                    $"Koszt: {ActionPointUnits.Format(actionCost)} {UiTerms.ActionPointsShort}" + (string.IsNullOrEmpty(unavailableReason) ? "" : "\n" + unavailableReason));
            else tooltipView?.ShowSkill(skill, actionCost, this, casterProvider?.Invoke(), unavailableReason);
        }

        private void OnDisable(){hovered=false;hoverChanged?.Invoke(false);tooltipView?.NotifySourceExit(this);}

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            hoverChanged?.Invoke(false);
            tooltipView?.NotifySourceExit(this);
        }

        private void ApplyShortcutLayout()
        {
            if (shortcutText == null) return;

            var shortcutRect = shortcutText.rectTransform;
            shortcutRect.SetParent(transform, false);
            shortcutRect.SetAsLastSibling();
            shortcutRect.anchorMin = new Vector2(.5f, 1f);
            shortcutRect.anchorMax = new Vector2(.5f, 1f);
            shortcutRect.pivot = new Vector2(.5f, 1f);
            shortcutRect.anchoredPosition = new Vector2(0f, 18f);
            shortcutRect.sizeDelta = new Vector2(42f, 18f);
            shortcutText.alignment = TextAlignmentOptions.Center;
            shortcutText.fontStyle = FontStyles.Bold;
            shortcutText.fontSize = Mathf.Max(14f, shortcutText.fontSize);
            shortcutText.raycastTarget = false;

            if (icon != null)
            {
                icon.rectTransform.anchorMin = Vector2.zero;
                icon.rectTransform.anchorMax = Vector2.one;
                icon.rectTransform.offsetMin = Vector2.zero;
                icon.rectTransform.offsetMax = Vector2.zero;
            }
        }

        private void RestoreIconLayout()
        {
            if (icon == null) return;
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(5f, 5f);
            icon.rectTransform.offsetMax = new Vector2(-5f, -5f);
        }
    }
}
