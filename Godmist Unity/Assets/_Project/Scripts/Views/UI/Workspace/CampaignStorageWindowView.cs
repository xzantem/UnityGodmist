using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CampaignStorageWindowView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private Transform stashItemsRoot;
        [SerializeField] private CampaignStorageItemView itemTemplate;
        [SerializeField] private GameObject dragGhostTemplate;
        [SerializeField] private TextMeshProUGUI statusText;

        private readonly CampaignStorageService storage = new();
        private readonly List<CampaignStorageItemView> spawned = new();
        private GameSession session;
        private CampaignStorageItemView dragged;
        private GameObject dragGhost;
        public static CampaignStorageWindowView Active { get; private set; }

        private void Awake() { if (itemTemplate != null) itemTemplate.gameObject.SetActive(false); }
        public void Open(GameSession nextSession)
        {
            session = nextSession;
            if (session == null) return;
            Active = this;
            window?.Open(); Refresh();
        }
        public void Refresh()
        {
            foreach (var view in spawned) if (view != null) Destroy(view.gameObject);
            spawned.Clear();
            AddStacks(session?.Stash?.Items, stashItemsRoot, true);
        }
        public void BeginDrag(CampaignStorageItemView item, PointerEventData eventData)
        {
            if (item?.Item == null) return;
            dragged = item; UIWorkspaceTooltipView.ActiveInstance?.BeginDragSuppression();
            if (window?.Manager?.WorkspaceRoot == null) return;
            dragGhost = dragGhostTemplate != null
                ? Instantiate(dragGhostTemplate, window.Manager.WorkspaceRoot)
                : Instantiate(item.gameObject, window.Manager.WorkspaceRoot);
            var iconSprite = item.DisplayedIcon;
            if (dragGhostTemplate == null)
            {
                var rect = dragGhost.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(56f, 56f);
            }
            else
            {
                // PF_UIW_ItemSlot has a decorative root Image and a direct child named Icon.
                // The ghost must update that child, otherwise the decorative background replaces
                // the item icon while dragging from the stash.
                var image = dragGhost.transform.Find("Icon")?.GetComponent<Image>()
                    ?? dragGhost.GetComponent<Image>()
                    ?? dragGhost.GetComponentInChildren<Image>(true);
                if (image != null)
                {
                    image.sprite = iconSprite;
                    image.preserveAspect = true;
                    image.enabled = true;
                    image.color = image.sprite != null ? Color.white : new Color(.18f, .25f, .35f, 1f);
                }
            }
            var canvasGroup = dragGhost.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = dragGhost.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = .85f;
            var countLabel = dragGhost.transform.Find("CountLabel");
            if (countLabel != null) countLabel.gameObject.SetActive(false);
            UpdateDrag(eventData);
        }
        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragGhost == null || eventData == null) return;
            var canvas = window.GetComponentInParent<Canvas>(); var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(window.Manager.WorkspaceRoot, eventData.position, camera, out var point)) dragGhost.GetComponent<RectTransform>().anchoredPosition = point;
        }
        public void EndDrag()
        {
            UIWorkspaceTooltipView.ActiveInstance?.EndDragSuppression(); dragged = null;
            if (dragGhost != null) Destroy(dragGhost); dragGhost = null;
        }
        public void TryDrop(CampaignStorageItemView item, bool targetStash)
        {
            EndDrag();
        }
        public void TryContextTransfer(CampaignStorageItemView item)
        {
            if (item?.IsStashItem != true) return;
            var leaderId = session?.PlayerParty.ActiveLeaderId;
            if (!string.IsNullOrWhiteSpace(leaderId) && session.CharactersById.TryGetValue(leaderId, out var leader))
                TryWithdraw(leader, item.Item, false);
        }
        public void TryEquipFromStash(CampaignStorageItemView item)
        {
            var leaderId = session?.PlayerParty.ActiveLeaderId;
            if (item?.Item == null || string.IsNullOrWhiteSpace(leaderId) || !session.CharactersById.TryGetValue(leaderId, out var leader)) return;
            if (storage.TryEquipFromStash(session, leader, item.Item.InstanceId))
            { CharacterInfoWindowView.RefreshOpenWindows(session); Refresh(); }
            else if (statusText != null) statusText.text = "Nie można założyć tego wyposażenia.";
        }
        public void TryDeposit(CharacterInfoWindowView source, ItemInstance item, bool single)
        {
            var success = source?.Character != null && item != null && storage.TryDeposit(session, source.Character.Inventory, item.InstanceId, single);
            if (statusText != null) statusText.text = success ? string.Empty : "Nie można przenieść przedmiotu.";
            if (success) { UIWorkspaceTooltipView.ActiveInstance?.Hide(); CharacterInfoWindowView.RefreshOpenWindows(session); Refresh(); }
        }
        public bool TryDepositAtScreenPoint(CharacterInfoWindowView source, ItemInstance item, PointerEventData eventData, bool single)
        {
            if (stashItemsRoot is not RectTransform stashRect || eventData == null) return false;
            var canvas = window?.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(stashRect, eventData.position, camera)) return false;
            if (source?.Character?.Inventory?.Items == null
                || item == null
                || !source.Character.Inventory.Items.Any(candidate => candidate?.InstanceId == item.InstanceId)) return false;
            TryDeposit(source, item, single);
            return true;
        }
        public void TryWithdraw(CharacterInstance target, ItemInstance item, bool single)
        {
            var success = target?.Inventory != null && item != null && storage.TryWithdraw(session, target.Inventory, item.InstanceId, single);
            if (statusText != null) statusText.text = success ? string.Empty : "Nie można przenieść przedmiotu.";
            if (success) { UIWorkspaceTooltipView.ActiveInstance?.Hide(); CharacterInfoWindowView.RefreshOpenWindows(session); Refresh(); }
        }
        private void AddStacks(IEnumerable<ItemInstance> items, Transform root, bool isStash)
        {
            if (items == null || root == null || itemTemplate == null) return;
            foreach (var group in items.Where(item => item?.Definition != null).GroupBy(item => item.Definition.IsStackable ? item.Definition.Id : item.InstanceId))
            { var view = Instantiate(itemTemplate, root); view.gameObject.SetActive(true); view.Bind(this, group.First(), group.Count(), isStash); spawned.Add(view); }
        }
    }
}
