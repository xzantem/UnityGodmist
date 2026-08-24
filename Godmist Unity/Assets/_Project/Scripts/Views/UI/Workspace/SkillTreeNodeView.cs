using System;
using Godmist.Data.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class SkillTreeNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image border;
        [SerializeField] private Image disabledOverlay;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI lockText;

        private SkillTreeNodeDefinition node;
        private string tooltipBody;

        public void Configure(
            SkillTreeNodeDefinition sourceNode,
            int ranks,
            bool isStructurallyUnlocked,
            bool canInvest,
            string blockReason,
            Action onClick)
        {
            node = sourceNode;
            tooltipBody = BuildTooltip(sourceNode, ranks, blockReason);

            if (icon != null)
            {
                icon.sprite = sourceNode?.Icon;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
            }

            if (nameText != null) nameText.text = sourceNode?.DisplayName ?? string.Empty;
            if (rankText != null) rankText.text = $"{ranks}/{sourceNode?.EffectiveMaxRanks ?? 0}";
            if (lockText != null)
            {
                lockText.text = canInvest || string.IsNullOrWhiteSpace(blockReason) ? string.Empty : "Zablokowane";
                lockText.gameObject.SetActive(!string.IsNullOrWhiteSpace(lockText.text));
            }

            ApplyVisualState(sourceNode, ranks, isStructurallyUnlocked);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = canInvest;
                if (canInvest && onClick != null) button.onClick.AddListener(() => onClick.Invoke());
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (node != null) UIWorkspaceTooltipView.ActiveInstance?.ShowText(node.DisplayName, tooltipBody, this);
        }

        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit();

        private void ApplyVisualState(SkillTreeNodeDefinition sourceNode, int ranks, bool isStructurallyUnlocked)
        {
            var state = !isStructurallyUnlocked
                ? NodeVisualState.Locked
                : ranks >= (sourceNode?.EffectiveMaxRanks ?? 1)
                    ? NodeVisualState.MaxRank
                    : ranks > 0
                        ? NodeVisualState.Invested
                        : NodeVisualState.Available;
            var palette = GetComponent<UIWorkspaceStyleApplier>()?.Palette;
            border ??= transform.Find("Border")?.GetComponent<Image>();

            if (disabledOverlay != null)
            {
                disabledOverlay.color = new Color(0.02f, 0.03f, 0.05f, 0.58f);
                disabledOverlay.enabled = state == NodeVisualState.Locked;
                disabledOverlay.raycastTarget = false;
            }

            if (icon != null)
            {
                var color = icon.color;
                color.a = state == NodeVisualState.Locked ? 0.4f : 1f;
                icon.color = color;
            }

            if (palette == null) return;
            if (border != null)
            {
                border.color = state switch
                {
                    NodeVisualState.Locked => palette.GetTextColor(UIWorkspaceTextStyle.Disabled),
                    NodeVisualState.Invested => palette.GetAccentColor(UIWorkspaceAccentStyle.Selected),
                    NodeVisualState.MaxRank => palette.GetAccentColor(UIWorkspaceAccentStyle.Positive),
                    _ => palette.GetAccentColor(UIWorkspaceAccentStyle.Neutral)
                };
            }

            if (rankText != null)
            {
                rankText.color = state == NodeVisualState.Locked
                    ? palette.GetTextColor(UIWorkspaceTextStyle.Disabled)
                    : palette.GetTextColor(UIWorkspaceTextStyle.Body);
            }
        }

        private static string BuildTooltip(SkillTreeNodeDefinition node, int ranks, string blockReason)
        {
            var text = node?.Description ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text)) text += "\n\n";
            text += $"Ranga: {ranks}/{node?.EffectiveMaxRanks ?? 0}";
            if (!string.IsNullOrWhiteSpace(blockReason)) text += $"\n{blockReason}";
            return text;
        }

        private enum NodeVisualState
        {
            Locked,
            Available,
            Invested,
            MaxRank
        }
    }
}
