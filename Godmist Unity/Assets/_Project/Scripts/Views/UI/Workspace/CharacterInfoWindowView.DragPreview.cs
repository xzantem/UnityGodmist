using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class CharacterInfoWindowView
    {
        private CharacterInfoPartyPortraitView recipientDragTarget;
        private TMP_Text recipientDragLabel;
        private RectTransform recipientDragFeedback;

        // The preview describes whether at least one unit can be handed over.
        // The existing quantity dialog and final transaction still validate the chosen amount.
        public bool CanPreviewRecipientDrop(GameSession destinationSession, string recipientId,
            ItemInstance item, out string reason)
        {
            reason = "Przedmiot nie jest już dostępny.";
            if (!isActiveAndEnabled || session == null || destinationSession != session
                || item == null || !ReferenceEquals(draggedItem, item)
                || !ItemOwnershipActions.Owns(character, item)) return false;
            if (!session.CharactersById.TryGetValue(recipientId ?? string.Empty, out var recipient)) return false;
            if (ReferenceEquals(recipient, character))
            { reason = "Własny plecak — anuluj przekazanie."; return false; }
            if (session.IsCharacterInCombat(character.CharacterId))
            { reason = "Nie można przekazywać przedmiotów podczas walki."; return false; }
            if (!ItemOwnershipActions.CanReceive(session, recipient,
                Godmist.Views.Game.GameBootstrap.Instance?.IsDungeonScene != true))
            { reason = "Towarzysz jest niedostępny."; return false; }
            if (!inventoryService.CanAddItem(recipient.Inventory, item))
            { reason = "Brak miejsca w plecaku odbiorcy."; return false; }
            reason = draggedItemIds?.Count > 1 ? "Przekaż — wybierz liczbę sztuk" : "Przekaż do plecaka";
            return true;
        }

        private void ConfigureRecipientDragLabel(Transform countLabel)
        {
            recipientDragLabel = countLabel != null ? countLabel.GetComponent<TMP_Text>() : null;
            if (recipientDragLabel == null) return;
            recipientDragLabel.raycastTarget = false;
            recipientDragLabel.fontSize = 17;
            recipientDragLabel.enableAutoSizing = false;
            recipientDragLabel.alignment = TextAlignmentOptions.MidlineLeft;
            recipientDragLabel.textWrappingMode = TextWrappingModes.Normal;
            var node = new GameObject("RecipientFeedback", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            node.transform.SetParent(dragGhost.transform, false);
            node.GetComponent<LayoutElement>().ignoreLayout = true;
            var background = node.GetComponent<Image>();
            background.color = new Color(.09f, .085f, .10f, .96f);
            background.raycastTarget = false;
            recipientDragFeedback = node.GetComponent<RectTransform>();
            recipientDragFeedback.anchorMin = recipientDragFeedback.anchorMax = new Vector2(1, .5f);
            recipientDragFeedback.pivot = new Vector2(0, .5f);
            recipientDragFeedback.anchoredPosition = new Vector2(12, 0);
            recipientDragFeedback.sizeDelta = new Vector2(286, 52);
            recipientDragLabel.transform.SetParent(node.transform, false);
            var rect = recipientDragLabel.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 6); rect.offsetMax = new Vector2(-10, -6);
            recipientDragLabel.gameObject.SetActive(true);
            node.SetActive(false);
        }

        private void UpdateRecipientDragTarget(PointerEventData data)
        {
            var hit = data?.pointerCurrentRaycast.gameObject;
            var next = hit != null ? hit.GetComponentInParent<CharacterInfoPartyPortraitView>() : null;
            if (next != recipientDragTarget)
            {
                recipientDragTarget?.ClearItemDropPreview();
                recipientDragTarget = next;
            }
            RefreshRecipientDragPreview();
        }

        private void RefreshRecipientDragPreview()
        {
            if (recipientDragTarget == null || !recipientDragTarget.isActiveAndEnabled || draggedItem == null)
            {
                recipientDragTarget?.ClearItemDropPreview();
                recipientDragTarget = null;
                if (recipientDragFeedback != null) recipientDragFeedback.gameObject.SetActive(false);
                return;
            }
            var allowed = recipientDragTarget.ShowItemDropPreview(this, draggedItem, out var reason);
            if (recipientDragLabel == null) return;
            recipientDragFeedback.gameObject.SetActive(true);
            if (recipientDragLabel.text != reason)
            {
                recipientDragLabel.text = reason;
                recipientDragFeedback.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(42, recipientDragLabel.GetPreferredValues(reason, 266, 0).y + 12));
            }
            recipientDragLabel.color = allowed ? HighlightStyle.Friendly : HighlightStyle.Hostile;
            // Keep feedback inside the owning workspace when the pointer nears its right edge.
            var workspace = window?.Manager?.WorkspaceRoot;
            var ghostRect = dragGhost != null ? dragGhost.transform as RectTransform : null;
            if (workspace != null && ghostRect != null)
            {
                var right = ghostRect.anchoredPosition.x + ghostRect.rect.width * (1 - ghostRect.pivot.x);
                var fits = right + 12 + recipientDragFeedback.rect.width <= workspace.rect.xMax;
                var labelRect = recipientDragFeedback;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(fits ? 1 : 0, .5f);
                labelRect.pivot = new Vector2(fits ? 0 : 1, .5f);
                labelRect.anchoredPosition = new Vector2(fits ? 12 : -12, 0);
            }
        }

        private void ClearRecipientDragPreview()
        {
            recipientDragTarget?.ClearItemDropPreview();
            recipientDragTarget = null;
            recipientDragLabel = null;
            recipientDragFeedback = null;
        }
    }
}
