using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
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

        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);

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
            if(node?.GrantedSkill != null)
            {
                var skill=node.GrantedSkill;
                var resource=skill.ResourceType switch {ResourceType.Momentum=>"PP",ResourceType.Mana=>"MP",ResourceType.Rage=>"PW",_=>""};
                text += $"\nOdblokowuje umiejętność: {skill.DisplayName}.\nKoszt: {skill.ResourceCost} {resource}, {skill.ActionCost}% PA";
                if(skill.KnifeCost>0)text += $", {skill.KnifeCost} sztyletów";
                text += ".\nPo odblokowaniu przypisz ją w zestawie umiejętności.";
            }
            AppendPassiveMetadata(ref text, node?.PassiveEffects);
            if (!string.IsNullOrWhiteSpace(text)) text += "\n\n";
            text += $"Ranga: {ranks}/{node?.EffectiveMaxRanks ?? 0}";
            if (!string.IsNullOrWhiteSpace(blockReason)) text += $"\n{blockReason}";
            return text;
        }

        private static void AppendPassiveMetadata(
            ref string text,
            IEnumerable<PassiveEffectDefinition> definitions)
        {
            foreach (var definition in definitions ?? Array.Empty<PassiveEffectDefinition>())
            {
                if (definition == null) continue;

                var condition = string.IsNullOrWhiteSpace(definition.Tooltip?.ConditionText)
                    ? DescribeConditions(definition)
                    : definition.Tooltip.ConditionText;
                if (!string.IsNullOrWhiteSpace(condition))
                    AppendLine(ref text, $"Warunek: {condition}");

                var trigger = definition.Tooltip?.TriggerText;
                if (string.IsNullOrWhiteSpace(trigger))
                    trigger = DescribeTriggers(definition);
                if (!string.IsNullOrWhiteSpace(trigger))
                    AppendLine(ref text, $"Aktywacja: {trigger}");
            }
        }

        private static string DescribeConditions(PassiveEffectDefinition definition)
        {
            var descriptions = new List<string>();
            foreach (var condition in definition?.Conditions
                         ?? new List<PassiveEffectConditionDefinition>())
            {
                if (condition == null || condition.Type == PassiveEffectConditionType.Always)
                    continue;

                var description = condition.Type switch
                {
                    PassiveEffectConditionType.AttackType =>
                        condition.ReferenceId == nameof(AttackType.Melee)
                            ? "tylko dla ataków wręcz"
                            : condition.ReferenceId == nameof(AttackType.Ranged)
                                ? "tylko dla ataków dystansowych"
                                : $"typ ataku: {condition.ReferenceId}",
                    PassiveEffectConditionType.CasterHasBuff =>
                        $"podczas działania buffa {condition.ReferenceId}",
                    PassiveEffectConditionType.TargetHasBuff =>
                        $"gdy cel ma buff {condition.ReferenceId}",
                    PassiveEffectConditionType.TargetHasStatus =>
                        $"gdy cel ma status {condition.ReferenceId}",
                    _ => condition.Type.ToString()
                };
                descriptions.Add(condition.Negated
                    ? $"nie {description}"
                    : description);
            }

            var separator = definition?.ConditionCombination ==
                            PassiveEffectConditionCombination.Any
                ? " lub "
                : " i ";
            return string.Join(separator, descriptions);
        }

        private static string DescribeTriggers(PassiveEffectDefinition definition)
        {
            var triggers = new List<string>();
            foreach (var trigger in definition?.Triggers
                         ?? new List<PassiveEffectTriggerDefinition>())
            {
                if (trigger == null || trigger.Type == PassiveEffectTriggerType.AlwaysActive)
                    continue;

                triggers.Add(trigger.Type switch
                {
                    PassiveEffectTriggerType.OnAttack => "przy ataku",
                    PassiveEffectTriggerType.OnCriticalHit => "po trafieniu krytycznym",
                    PassiveEffectTriggerType.OnMiss => "po chybieniu",
                    PassiveEffectTriggerType.OnDodge => "po uniku",
                    PassiveEffectTriggerType.OnMove => "przy ruchu",
                    _ => trigger.Type.ToString()
                });
            }

            return string.Join(" lub ", triggers);
        }

        private static void AppendLine(ref string text, string line)
        {
            if (!string.IsNullOrWhiteSpace(text)) text += "\n";
            text += line;
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
