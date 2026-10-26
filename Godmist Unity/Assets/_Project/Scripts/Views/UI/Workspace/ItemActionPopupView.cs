using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    [DefaultExecutionOrder(100)]
    public sealed class ItemActionPopupView : MonoBehaviour
    {
        [SerializeField] private PartyRailMenuView menu;
        [SerializeField] private GameObject quantityPanel, comparisonPanel;
        [SerializeField] private TMP_Text quantityTitle, quantityHint, quantityError;
        [SerializeField] private TMP_InputField quantityInput;
        [SerializeField] private Button minusButton, plusButton, maxButton, confirmButton, cancelButton, closeComparisonButton;
        [SerializeField] private TMP_Text candidateTitle, candidateBody, equippedTitle, equippedBody;
        [SerializeField] private Image candidateIcon, equippedIcon;
        [SerializeField] private ScrollRect candidateScroll, equippedScroll;
        private ItemRarityBorderGraphic candidateRarityBorder, equippedRarityBorder;
        private readonly ItemOwnershipActions actions = new();
        private Func<int, string> describe, confirm;
        private int maximum;
        private bool notice;
        private GameObject previousSelection;
        public bool IsModalOpen => quantityPanel.activeSelf || comparisonPanel.activeSelf;

        private void Awake()
        {
            minusButton.onClick.AddListener(() => SetCount(Count - 1));
            plusButton.onClick.AddListener(() => SetCount(Count + 1));
            maxButton.onClick.AddListener(() => SetCount(maximum));
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(HideAll);
            closeComparisonButton.onClick.AddListener(HideAll);
            quantityInput.onValueChanged.AddListener(_ => UpdateQuantity());
            quantityInput.onEndEdit.AddListener(_ => SetCount(Count));
            quantityInput.onSubmit.AddListener(_ => { if (!quantityInput.wasCanceled) Confirm(); });
            quantityPanel.SetActive(false); comparisonPanel.SetActive(false); menu.Hide();
        }

        public void ShowMenu(Vector2 position, IReadOnlyList<PartyRailMenuView.Entry> entries, ItemInstance item, string hint = null)
        {
            HideAll(); gameObject.SetActive(true);
            // Focusing an owning window can move it above this reusable popup after a transaction.
            transform.SetAsLastSibling();
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
            menu.Show(position, entries, ItemRarityPresentation.Name(item),
                hint ?? (item.IsSaleProtected ? "Ochrona przed sprzedażą włączona." : null));
        }

        public void AddOwnedEntries(List<PartyRailMenuView.Entry> entries, GameSession session,
            CharacterInstance owner, ItemInstance item, Action refresh)
        {
            var ids = ItemOwnershipActions.CaptureStack(owner, item);
            bool InCity() => GameBootstrap.Instance?.IsDungeonScene != true;
            var recipients = session.CharactersById.Values.Where(character => character != owner
                && (session.PlayerParty.IsActiveMember(character.CharacterId)
                    || InCity() && session.PlayerParty.BenchMemberIds.Contains(character.CharacterId)))
                .Select(character => new PartyRailMenuView.Entry(character.Definition?.DisplayName ?? character.CharacterId,
                    () => Transfer(character, false), ItemOwnershipActions.CanReceive(session, character, InCity()))).ToList();
            var free = !session.IsCharacterInCombat(owner.CharacterId);
            entries.Add(new PartyRailMenuView.Entry("Przekaż towarzyszowi…", enabled: free && recipients.Any(entry => entry.Enabled),
                separatorBefore: entries.Count > 0, children: recipients, childrenHint: "Wybierz odbiorcę przedmiotu."));
            entries.Add(new PartyRailMenuView.Entry(InCity() ? "Przenieś do schowka…" : "Schowek — tylko w mieście",
                () => Transfer(null, true), free && InCity()));
            AddCompare(entries, owner, item);
            var protect = !item.IsSaleProtected;
            entries.Add(new PartyRailMenuView.Entry(protect ? "Zablokuj sprzedaż" : "Odblokuj sprzedaż", () =>
            {
                if (actions.TrySetSaleProtection(owner, item, ids, protect)) Refresh();
            }, separatorBefore: true));

            void Refresh()
            {
                UIWorkspaceTooltipView.ActiveInstance?.Hide();
                RefreshOwners(session);
                refresh?.Invoke();
            }
            void Transfer(CharacterInstance recipient, bool stash)
            {
                string Execute(int count)
                {
                    if (!actions.TryTransfer(session, owner, item, ids, count, recipient, stash, InCity(), out var reason)) return reason;
                    Refresh(); return null;
                }
                var destination = stash ? "schowka" : recipient.Definition?.DisplayName ?? recipient.CharacterId;
                if (ids.Count > 1) RequestQuantity("Przekaż przedmioty", ids.Count,
                    count => $"{item.DisplayName}\n{count} szt. → {destination}", Execute);
                else { var error = Execute(1); if (error != null) ShowNotice(error); }
            }
        }

        public void RefreshOwners(GameSession session)
        {
            CharacterInfoWindowView.RefreshOpenWindows(session);
            CampaignStorageWindowView.Active?.RefreshForSession(session);
            // Popups are authored under the owning workspace. Refresh only views in that workspace and session.
            foreach (var trade in transform.parent.GetComponentsInChildren<TradeServiceWindowView>(true))
                trade.RefreshForSession(session);
        }

        public void AddCompare(List<PartyRailMenuView.Entry> entries, CharacterInstance owner, ItemInstance item)
        {
            if (item is not EquipmentItemInstance equipment) return;
            entries.Add(new PartyRailMenuView.Entry("Porównaj z założonym", () => ShowComparison(owner, equipment),
                !ReferenceEquals(owner?.Equipment?.GetItem(equipment.EquipmentSlot), equipment)));
        }

        private void OpenModal(GameObject panel)
        {
            previousSelection = EventSystem.current?.currentSelectedGameObject;
            menu.Hide(); gameObject.SetActive(true); transform.SetAsLastSibling();
            quantityPanel.SetActive(false); comparisonPanel.SetActive(false); panel.SetActive(true);
            UIWorkspaceTooltipView.ActiveInstance?.Hide();
        }

        public void RequestQuantity(string title, int max, Func<int, string> description, Func<int, string> execute)
        {
            if (max < 1) return;
            OpenModal(quantityPanel); maximum = max; notice = false; describe = description; confirm = execute;
            quantityTitle.text = title; quantityError.text = string.Empty;
            foreach (var control in new GameObject[] { quantityInput.gameObject, minusButton.gameObject, plusButton.gameObject,
                maxButton.gameObject, confirmButton.gameObject }) control.SetActive(true);
            SetCount(1);
            quantityInput.Select(); quantityInput.ActivateInputField();
        }

        private int Count => int.TryParse(quantityInput.text, out var value) ? Mathf.Clamp(value, 1, maximum) : 1;
        public void RequestConfirmation(string title, string description, Func<string> execute)
        {
            RequestQuantity(title, 1, _ => description, _ => execute());
            foreach (var control in new GameObject[] { quantityInput.gameObject, minusButton.gameObject,
                plusButton.gameObject, maxButton.gameObject }) control.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(confirmButton.gameObject);
        }
        private void SetCount(int value) { quantityInput.SetTextWithoutNotify(Mathf.Clamp(value, 1, maximum).ToString()); UpdateQuantity(); }
        private void UpdateQuantity()
        {
            if (notice) return;
            quantityHint.text = describe?.Invoke(Count) ?? string.Empty;
            minusButton.interactable = Count > 1; plusButton.interactable = Count < maximum;
            confirmButton.interactable = int.TryParse(quantityInput.text, out var count) && count >= 1 && count <= maximum;
        }
        private void Confirm()
        {
            if (confirm == null || !confirmButton.interactable) return;
            var error = confirm(Count);
            if (string.IsNullOrEmpty(error)) HideAll(); else quantityError.text = error;
        }

        public void ShowNotice(string message)
        {
            OpenModal(quantityPanel); notice = true; confirm = null;
            quantityTitle.text = "Nie można wykonać akcji"; quantityHint.text = message; quantityError.text = string.Empty;
            foreach (var control in new GameObject[] { quantityInput.gameObject, minusButton.gameObject, plusButton.gameObject,
                maxButton.gameObject, confirmButton.gameObject }) control.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(cancelButton.gameObject);
        }

        private void ShowComparison(CharacterInstance owner, EquipmentItemInstance candidate)
        {
            OpenModal(comparisonPanel);
            var equipped = owner?.Equipment?.GetItem(candidate.EquipmentSlot);
            candidateTitle.text = "Wybrany przedmiot\n" + ItemRarityPresentation.Name(candidate);
            candidateBody.text = UIWorkspaceTooltipView.BuildEquipmentDetails(candidate, owner) + "\n" + UIWorkspaceTooltipView.BuildItemMeta(candidate);
            equippedTitle.text = "Założony przedmiot\n" + (equipped == null ? "Puste miejsce" : ItemRarityPresentation.Name(equipped));
            equippedBody.text = equipped == null ? "Postać nie ma wyposażenia w tym miejscu." :
                UIWorkspaceTooltipView.BuildEquipmentDetails(equipped, owner) + "\n" + UIWorkspaceTooltipView.BuildItemMeta(equipped);
            ItemRarityPresentation.ApplyBorder(ref candidateRarityBorder, candidateIcon.rectTransform, candidate, 0f);
            ItemRarityPresentation.ApplyBorder(ref equippedRarityBorder, equippedIcon.rectTransform, equipped, 0f);
            candidateIcon.sprite = ItemIconPresentation.GetIcon(candidate); candidateIcon.enabled = candidateIcon.sprite != null;
            equippedIcon.sprite = ItemIconPresentation.GetIcon(equipped); equippedIcon.enabled = equippedIcon.sprite != null;
            candidateScroll.verticalNormalizedPosition = equippedScroll.verticalNormalizedPosition = 1;
            EventSystem.current?.SetSelectedGameObject(closeComparisonButton.gameObject);
        }

        public void HideAll()
        {
            menu.Hide(); quantityPanel.SetActive(false); comparisonPanel.SetActive(false); confirm = null;
            GetComponentInParent<UIWorkspaceWindowManager>()?.ConsumeContextMenuInput();
            var events = EventSystem.current;
            if (events?.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(transform))
                events.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            gameObject.SetActive(false);
        }
        private void Update() { if (IsModalOpen && Input.GetKeyDown(KeyCode.Escape)) HideAll(); }
    }
}
