using Godmist.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class SkillTreeNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private SkillIconShapeMask iconMask;
        [SerializeField] private Image border;
        [SerializeField] private Image disabledOverlay;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI lockText;
        [SerializeField] private Image selectionBorder;
        [SerializeField] private Image hoverBorder;
        [SerializeField] private Image rankAccent;
        [SerializeField] private RectTransform holdFillViewport;
        [SerializeField] private Image holdFillBorder;
        [SerializeField] private Image nodeSurface;
        [SerializeField] private RectTransform rankBackground;
        [SerializeField] private SkillTreeRankMarkers rankMarkers;
        [SerializeField] private SkillTreeActiveNodeFrame activeSurface;
        [SerializeField] private SkillTreeActiveNodeFrame activeRankAccent;
        [SerializeField] private SkillTreeActiveNodeFrame activeHoverBorder;
        [SerializeField] private SkillTreeActiveNodeFrame activeSelectionBorder;
        [SerializeField] private SkillTreeActiveNodeFrame activeHoldBorder;
        [SerializeField, Min(.1f)] private float holdDuration = .7f;

        private SkillTreeNodeDefinition node;
        private int investedRanks;
        private string investmentBlockReason;
        private CharacterInstance previewCharacter;
        private Action selectAction, investAction;
        private PointerEventData holdPointer;
        private Vector2 pressPosition;
        private float holdElapsed;
        private bool canHoldInvest, holding, holdCompleted, activeShape;
        private Color stateAccent;
        public string NodeId => node?.Id;
        public void SetSelected(bool selected)
        {
            if (selectionBorder != null) selectionBorder.gameObject.SetActive(selected && !activeShape);
            if (activeSelectionBorder != null) activeSelectionBorder.enabled = selected && activeShape;
        }

        public void Configure(
            SkillTreeNodeDefinition sourceNode,
            int ranks,
            bool isStructurallyUnlocked,
            bool canInvest,
            string blockReason,
            Action onClick,
            Action onInvest = null,
            CharacterInstance character = null)
        {
            CancelHold();
            holdCompleted = false;
            selectAction = onClick;
            investAction = onInvest;
            canHoldInvest = canInvest && onInvest != null;
            node = sourceNode;
            investedRanks = ranks;
            investmentBlockReason = blockReason;
            previewCharacter = character;
            activeShape = sourceNode?.GrantedSkill != null;
            // Keep the button's original raycast surface, including transparent corners.
            if (nodeSurface != null) { nodeSurface.enabled = true; nodeSurface.color = activeShape ? Color.clear : Color.white; }
            if (activeSurface != null) activeSurface.enabled = activeShape;
            if (iconMask != null)
            {
                var inset = activeShape ? 2f : 4f;
                iconMask.rectTransform.offsetMin = new Vector2(inset, inset);
                iconMask.rectTransform.offsetMax = new Vector2(-inset, -inset);
                iconMask.SetCornerCut(activeShape ? 14f : 2f);
            }
            if (hoverBorder != null) hoverBorder.enabled = false;
            if (activeHoverBorder != null) activeHoverBorder.enabled = false;
            if (disabledOverlay != null)
            {
                var overlayRect = disabledOverlay.rectTransform;
                if (iconMask != null)
                {
                    overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
                }
                else if (activeShape && icon != null)
                {
                    // Fade just the icon, leaving the octagon's cut corners transparent.
                    overlayRect.anchorMin = overlayRect.anchorMax = overlayRect.pivot = new Vector2(.5f,.5f);
                    overlayRect.sizeDelta = icon.rectTransform.sizeDelta;
                    overlayRect.anchoredPosition = icon.rectTransform.anchoredPosition;
                }
                else
                {
                    overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = new Vector2(6,6); overlayRect.offsetMax = new Vector2(-6,-6);
                }
            }
            SetSelected(false);
            if (rankText != null) rankText.enabled = rankMarkers == null;
            if (rankBackground != null) rankBackground.gameObject.SetActive(rankMarkers == null);
            if (rankMarkers == null)
            {
                PlaceRank(rankText?.rectTransform);
                PlaceRank(rankBackground);
            }

            if (icon != null)
            {
                icon.sprite = sourceNode?.Icon;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = iconMask == null;
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
                button.interactable = sourceNode != null;
                if (sourceNode != null && onClick != null)
                    button.onClick.AddListener(() => { if (!holdCompleted) selectAction?.Invoke(); });
            }
        }

        // Process after EventSystem.Update so release, dragging and wheel input cancel first.
        private void LateUpdate() => AdvanceHold(Time.unscaledDeltaTime);

        private void AdvanceHold(float seconds)
        {
            if (!holding) return;
            var threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            if (button == null || !button.IsInteractable() || holdPointer == null
                || holdPointer.dragging
                || (holdPointer.position - pressPosition).sqrMagnitude > threshold * threshold)
            {
                CancelHold();
                return;
            }
            holdElapsed += Mathf.Max(0f, seconds);
            var progress = Mathf.Clamp01(holdElapsed / Mathf.Max(.1f, holdDuration));
            SetHoldProgress(progress);
            if (progress < 1f) return;
            holding = false;
            holdCompleted = true;
            holdPointer = null;
            // One purchase per press. The owner rechecks points, requirements and combat.
            investAction?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || button == null || !button.IsInteractable()) return;
            CancelHold();
            holdCompleted = false;
            selectAction?.Invoke();
            if (!isActiveAndEnabled || !canHoldInvest) return;
            holdPointer = eventData;
            pressPosition = eventData.position;
            holding = true;
            SetHoldProgress(0f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && holdPointer?.pointerId == eventData.pointerId) CancelHold();
        }

        public void OnScroll(PointerEventData eventData)
        {
            // The Input System retains scrollDelta on its shared pointer data after the wheel stops.
            // Cancel only on a new scroll event, not by polling that retained value during a press.
            if (eventData.scrollDelta.sqrMagnitude > .01f) CancelHold();
            // This node now receives the event first; preserve scrolling in its parent ScrollRect.
            if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
        }

        private void OnDisable()
        {
            CancelHold();
            if (hoverBorder != null) hoverBorder.enabled = false;
            if (activeHoverBorder != null) activeHoverBorder.enabled = false;
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelHold(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelHold(); }

        private void CancelHold()
        {
            holding = false;
            holdElapsed = 0f;
            holdPointer = null;
            SetHoldProgress(0f);
        }

        private void SetHoldProgress(float progress)
        {
            if (holdFillViewport == null || holdFillBorder == null) return;
            var height = ((RectTransform)transform).rect.height;
            holdFillBorder.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            holdFillViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height * progress);
            holdFillBorder.enabled = progress > 0f && !activeShape;
            if (activeHoldBorder != null)
            {
                activeHoldBorder.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                activeHoldBorder.enabled = progress > 0f && activeShape;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverBorder != null) hoverBorder.enabled = !activeShape;
            if (activeHoverBorder != null) activeHoverBorder.enabled = activeShape;
            var tooltip = UIWorkspaceTooltipView.ActiveInstance;
            if (node?.GrantedSkill != null && tooltip != null)
                tooltip.ShowLoadoutSkillDetails(node.GrantedSkill, previewCharacter, this,
                    BuildRankMetadata(node, investedRanks, investmentBlockReason));
            else if (node != null && tooltip != null)
                tooltip.ShowText(node.DisplayName, BuildTooltip(node, investedRanks, investmentBlockReason, tooltip, previewCharacter), this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHold();
            if (hoverBorder != null) hoverBorder.enabled = false;
            if (activeHoverBorder != null) activeHoverBorder.enabled = false;
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        }

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
                disabledOverlay.color = new Color(0.02f, 0.03f, 0.05f, 0.28f);
                disabledOverlay.enabled = state == NodeVisualState.Locked;
                disabledOverlay.raycastTarget = false;
            }

            if (icon != null)
            {
                var color = icon.color;
                color.a = 1f;
                icon.color = color;
            }

            stateAccent = state switch
            {
                NodeVisualState.MaxRank => new Color(.25f, 1f, .4f),
                NodeVisualState.Invested => new Color(1f, .75f, .2f),
                NodeVisualState.Locked => new Color(.42f, .48f, .5f),
                _ => new Color(.7f, .78f, .8f)
            };
            if (rankAccent != null)
            {
                rankAccent.enabled = !activeShape;
                rankAccent.color = stateAccent;
            }
            if (activeRankAccent != null) { activeRankAccent.enabled = activeShape; activeRankAccent.color = stateAccent; }
            if (activeHoverBorder != null) activeHoverBorder.color = stateAccent;
            if (hoverBorder != null) hoverBorder.color = stateAccent;
            if (rankText != null) rankText.color = state switch {
                NodeVisualState.Locked => new Color(.67f, .65f, .6f),
                NodeVisualState.MaxRank => new Color(.5f, 1f, .65f),
                NodeVisualState.Invested => new Color(1f, .8f, .35f),
                _ => new Color(.91f, .875f, .78f)
            };
            if (rankMarkers != null)
                rankMarkers.SetRanks(ranks, sourceNode?.EffectiveMaxRanks ?? 1, new Color(.25f, 1f, .4f), Color.white);
            if (palette == null) return;
            if (border != null && !activeShape)
            {
                palette.ApplyFrameAccent(border, state switch
                {
                    NodeVisualState.Invested => UIWorkspaceAccentStyle.Selected,
                    NodeVisualState.MaxRank => UIWorkspaceAccentStyle.Positive,
                    _ => UIWorkspaceAccentStyle.Neutral
                }, state == NodeVisualState.Locked);
            }

        }

        private void PlaceRank(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(activeShape ? .5f : 1f, 0f);
            rect.anchoredPosition = new Vector2(activeShape ? 0f : -5f, activeShape ? -2f : 5f);
        }

        public static string BuildDetails(SkillTreeNodeDefinition node, int ranks,
            UIWorkspaceTooltipView tooltip = null, CharacterInstance character = null)
        {
            if (node == null) return string.Empty;
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            var text = string.Empty;
            if(node?.GrantedSkill != null)
            {
                var skill=node.GrantedSkill;
                var body = tooltip != null ? tooltip.BuildLoadoutSkillDetails(skill, character) : skill.TooltipDescription;
                text = UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, character) + "\n\n" + body;
            }
            else
            {
                ranks = Mathf.Clamp(ranks, 0, node.EffectiveMaxRanks);
                if (ranks == 0) text = "<b>Następne:</b>\n" + BuildRankDescription(node, 1);
                else if (ranks >= node.EffectiveMaxRanks) text = "<b>Aktualne:</b>\n" + BuildRankDescription(node, ranks);
                else
                {
                    var description = GetPassiveDescription(node);
                    var matches = RankValues.Matches(description).Cast<Match>().ToArray();
                    text = matches.Length > 0 && matches.All(match => match.Value.Split('/').Length == node.EffectiveMaxRanks)
                        ? "<b>Aktualne → następne:</b>\n" + RankValues.Replace(description,
                            match => RankValue(match, ranks - 1) + " → " + RankValue(match, ranks))
                        : "<b>Efekt:</b>\n" + BuildRankDescription(node, ranks);
                }
            }
            var metadata = UIWorkspaceTooltipView.BuildPassiveMetadata(node.PassiveEffects, text);
            if (!string.IsNullOrWhiteSpace(metadata)) text += "\n" + metadata;
            return UiTerms.Present(text, character?.ResourcePool?.ResourceType ?? ResourceType.None);
        }

        private static readonly Regex RankValues = new(@"[+-]?\d+(?:[.,]\d+)?%?(?:\s*/\s*[+-]?\d+(?:[.,]\d+)?%?)+");

        public static string BuildRankDescription(SkillTreeNodeDefinition node, int rank)
        {
            var text = UiTerms.Present(GetPassiveDescription(node));
            if (node == null || node.EffectiveMaxRanks < 2) return text;
            var matches = RankValues.Matches(text).Cast<Match>().ToArray();
            // Only expand complete authored rank lists. Other slash notation stays intact.
            if (matches.Length == 0 || matches.Any(match => match.Value.Split('/').Length != node.EffectiveMaxRanks)) return text;
            var index = Mathf.Clamp(rank, 1, node.EffectiveMaxRanks) - 1;
            return RankValues.Replace(text, match => RankValue(match, index));
        }

        private static string RankValue(Match match, int index)
        {
            var values = match.Value.Split('/').Select(value => value.Trim()).ToArray();
            var value = values[index];
            return values[^1].EndsWith("%") && !value.EndsWith("%") ? value + "%" : value;
        }

        private static string GetPassiveDescription(SkillTreeNodeDefinition node)
        {
            var text = node?.Description ?? string.Empty;
            // Translate authored notation without editing gameplay/serialized data.
            text = text.Replace("[85%] Ciosy krytyczne zwiększają obrażenia DoT", "Ciosy krytyczne mają 85% szansy na zwiększenie obrażeń okresowych");
            text = Regex.Replace(text, @"^\[(\d+(?:[.,]\d+)?%)\]\s*", "$1 szansy: ");
            return text.Replace("zasięg bezwzględny i względny Rykoszetu", "zasięg Rykoszetu oraz odległość między jego celami")
                .Replace("Po 3 bazowych ciosach Szybkich Ciosów osobno losuje 50% na każdy z 1/2 dodatkowych ciosów. Porażka nie przerywa kolejnych losowań.",
                    "Po 3 ciosach Szybkich Ciosów każdy z 1/2 dodatkowych ciosów ma niezależnie 50% szansy na wykonanie.")
                .Replace("Podpalenia mają 50% na zduplikowanie się na przeciwnikach", "Podpalenia mają 50% szansy na rozprzestrzenienie się na przeciwników")
                .Replace(", maksymalnie do trzech pokoleń kopii", ". Nowe podpalenia mogą rozprzestrzeniać się dalej, do trzech kolejnych przeniesień.")
                .Replace("Kliknięcie wyjmuje go rzucając [90%] krwawienie", "Wyjęcie noża ma 90% szansy na nałożenie krwawienia")
                .Replace("Kombo narzuca [80%] krwawienie", "Kombo ma 80% szansy na nałożenie krwawienia")
                .Replace("Podwójny Rzut narzuca [80%] krwawienie", "Podwójny Rzut ma 80% szansy na nałożenie krwawienia")
                .Replace("obrażenia DoT", "obrażenia okresowe")
                .Replace("obrażeń przedmitygacyjnych", "obrażeń przed ich redukcją")
                .Replace("odnowionego surowca", "odnowionego zasobu").Replace("4/8 surowca", "4/8 zasobu")
                .Replace("[p]", string.Empty).Replace("(max ", "(maks. ").Replace("etc.", "itp.");
        }

        public static string BuildTooltip(SkillTreeNodeDefinition node, int ranks, string blockReason,
            UIWorkspaceTooltipView tooltip = null, CharacterInstance character = null)
        {
            var text = BuildDetails(node, ranks, tooltip, character);
            if (!string.IsNullOrWhiteSpace(text)) text += "\n\n";
            return text + BuildRankMetadata(node, ranks, blockReason);
        }

        private static string BuildRankMetadata(SkillTreeNodeDefinition node, int ranks, string blockReason)
        {
            var text = $"Ranga: {ranks}/{node?.EffectiveMaxRanks ?? 0}";
            var reason = node != null && ranks >= node.EffectiveMaxRanks ? "Maksymalna ranga" : blockReason;
            if (!string.IsNullOrWhiteSpace(reason)) text += $"\n{reason}";
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
