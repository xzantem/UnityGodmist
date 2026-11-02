using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed partial class ControllerInputView
    {
        private TMP_Text panelHint, interactionHint;
        private ControllerFocusGraphic interactionFrame;

        private void ClearControllerPresentation()
        {
            if (panelHint != null) panelHint.gameObject.SetActive(false);
            if (interactionHint != null) interactionHint.gameObject.SetActive(false);
            if (interactionFrame != null) interactionFrame.gameObject.SetActive(false);
        }
        private void UpdateControllerPresentation()
        {
            ClearControllerPresentation();
            if (!controllerActive || selectorMenu == null || wheel != null && wheel.IsOpen) return;
            if (ControllerInput.UiActive && TopScope != null)
            {
                if (panelHint == null) panelHint = selectorMenu.CreateControllerLabel(TopScope.transform, "Controller Window Controls");
                panelHint.transform.SetParent(TopScope.transform, false);
                var text = "A — wybierz   B — wróć";
                var selected = events.currentSelectedGameObject;
                var slot = selected != null ? selected.GetComponentInParent<CombatSkillSlotView>() : null;
                var itemContext = selected != null && (selected.GetComponentInParent<Godmist.Views.UI.Workspace.CharacterInfoItemView>() != null
                    || selected.GetComponentInParent<Godmist.Views.UI.Workspace.CharacterInfoEquipmentSlotView>() != null
                    || selected.GetComponentInParent<Godmist.Views.UI.Workspace.TradeItemSlotView>() != null);
                if (itemContext) text += "   X — akcje";
                if (itemContext || slot != null || selected != null && selected.GetComponentInParent<Godmist.Views.UI.Workspace.SkillTreeNodeView>() != null) text += "   Y — szczegóły";
                var reason = slot != null && !slot.ControllerAvailable ? slot.ControllerUnavailableReason
                    : selected != null ? selected.GetComponent<ControllerAvailabilityHint>()?.Reason : null;
                if (!string.IsNullOrWhiteSpace(reason)) text = reason + "   ·   B — wróć";
                if (TopScope.GetComponentInChildren<Godmist.Views.UI.Workspace.NpcServicesWindowView>() != null)
                    text += "   LB / RB — zakładki";
                var partyWindow=TopScope.GetComponent<Godmist.Views.UI.Workspace.PartyViewWindowView>();
                if(partyWindow!=null)text+="   LB / RB — postać"+(itemContext?"":"   X — pozostałe okna");
                panelHint.text = text; panelHint.fontSize = 15; panelHint.alignment = TextAlignmentOptions.Center;
                var rect = panelHint.rectTransform;
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 1);
                rect.offsetMin = new Vector2(8, -28); rect.offsetMax = new Vector2(-8, -3);
                if(partyWindow!=null){rect.pivot=new Vector2(.5f,0);rect.offsetMin=new Vector2(8,5);rect.offsetMax=new Vector2(-8,29);}
                panelHint.gameObject.SetActive(true);
                return;
            }
            if (!ControllerInput.WorldActive || precisePointer || combat?.Session != null || targeting?.SelectedSkill != null
                || selectedInteraction is not Component target || target == null) return;
            var canvas = cursor != null ? cursor.GetComponentInParent<Canvas>() : null;
            if (canvas == null || worldCamera == null) return;
            if (interactionHint == null)
            {
                interactionHint = selectorMenu.CreateControllerLabel(canvas.transform, "Controller Interaction Target");
                interactionHint.alignment = TextAlignmentOptions.Center; interactionHint.fontSize = 17;
                var node = new GameObject("Controller Interaction Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(ControllerFocusGraphic));
                node.transform.SetParent(canvas.transform, false); interactionFrame = node.GetComponent<ControllerFocusGraphic>();
                interactionFrame.raycastTarget = false; interactionFrame.color = new Color(.93f,.79f,.43f,.9f);
            }
            var bounds = new Bounds(target.transform.position, Vector3.one * .8f);
            var renderers = target.GetComponentsInChildren<SpriteRenderer>();
            var first = true;
            foreach (var sprite in renderers)
            {
                if (!sprite.enabled || sprite.sprite == null) continue;
                if (first) { bounds = sprite.bounds; first = false; } else bounds.Encapsulate(sprite.bounds);
            }
            var min = (Vector2)worldCamera.WorldToScreenPoint(bounds.min);
            var max = (Vector2)worldCamera.WorldToScreenPoint(bounds.max);
            if (max.x < 0 || min.x > Screen.width || max.y < 0 || min.y > Screen.height) return;
            var factor = Mathf.Max(.01f, canvas.scaleFactor);
            var frame = interactionFrame.rectTransform;
            frame.position = (min + max) * .5f; frame.sizeDelta = (max - min) / factor + Vector2.one * 8;
            interactionFrame.gameObject.SetActive(selectedInteraction is not Godmist.Views.NPC.NpcView && selectedInteraction is not Godmist.Views.Dungeons.DungeonChestView);
            var label = interactionHint.rectTransform;
            label.position = new Vector2((min.x + max.x) * .5f, max.y + 22 * factor);
            label.sizeDelta = new Vector2(320, 28);
            interactionHint.text = InteractionPrompt + (InteractionHoldProgress > 0 ? $"   {Mathf.CeilToInt(InteractionHoldProgress * 100)}%" : "");
            interactionHint.gameObject.SetActive(true);
        }
    }
}
