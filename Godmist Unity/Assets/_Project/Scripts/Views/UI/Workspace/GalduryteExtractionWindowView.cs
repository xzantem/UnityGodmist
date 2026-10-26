using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class GalduryteExtractionWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform equipmentGrid;
        [SerializeField] private Transform socketList;
        [SerializeField] private GameObject socketsSection;
        [SerializeField] private BlacksmithCraftingSlotView equipmentSlotTemplate;
        [SerializeField] private GalduryteSocketRowView socketRowTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI chanceText;
        [SerializeField] private TextMeshProUGUI warningText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button extractButton;
        [SerializeField] private BlacksmithCraftingSlotView socketSlotTemplate;
        [SerializeField] private ScrollRect equipmentScroll;
        [SerializeField] private ScrollRect socketsScroll;
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private TextMeshProUGUI equipmentCountText;
        [SerializeField] private TextMeshProUGUI equipmentEmptyText;
        [SerializeField] private TextMeshProUGUI socketsCountText;
        [SerializeField] private TextMeshProUGUI socketsEmptyText;
        [SerializeField] private TextMeshProUGUI walletText;
        [SerializeField] private TextMeshProUGUI selectedHeadingText;
        [SerializeField] private GameObject previewSection;
        [SerializeField] private GameObject selectedSection;
        [SerializeField] private GameObject costSection;
        [SerializeField] private GalduryteSocketRowView selectedRow;

        private readonly GalduryteExtractionService service = new();
        private readonly List<BlacksmithCraftingSlotView> equipmentSlots = new();
        private readonly List<BlacksmithCraftingSlotView> socketSlots = new();
        private GameSession session;
        private NpcInstance enchanter;
        private EquipmentItemInstance selectedEquipment;
        private GalduryteItemInstance selectedGalduryte;
        private GalduryteItemInstance resultStone;
        private string resultHeading;
        private bool resultSucceeded;
        private int previousCount;
        private bool isVisible;

        private void Awake() { extractButton?.onClick.AddListener(TryExtract); HideTemplates(); HideMechanicsFooter(); }
        private void OnDestroy() => extractButton?.onClick.RemoveListener(TryExtract);
        private void OnDisable() { isVisible = false; Clear(equipmentSlots); Clear(socketSlots); }
        public void Hide() { isVisible = false; serviceRoot?.SetActive(false); }

        public bool Show(NpcInstance npc, string _)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || npc?.Definition == null) return false;
            enchanter = npc; isVisible = true; resultStone = null;
            serviceRoot?.SetActive(true); HideTemplates();
            PopulateEquipment(false); PopulateSockets(false); RefreshPreview();
            return true;
        }

        private void PopulateEquipment(bool preserveScroll = true)
        {
            var position = preserveScroll && equipmentScroll != null ? equipmentScroll.verticalNormalizedPosition : 1f;
            Clear(equipmentSlots);
            // Filter only this catalog; socketing shares the ownership enumeration.
            var items = new GalduryteSocketingService().GetSocketableEquipment(session)
                .Where(item => item.SocketedGaldurytes?.Count > 0).ToList();
            if (!items.Contains(selectedEquipment) && resultStone == null) selectedEquipment = items.FirstOrDefault();
            foreach (var item in items)
            {
                var captured = item;
                var slot = Instantiate(equipmentSlotTemplate, equipmentGrid);
                slot.gameObject.SetActive(true);
                slot.Bind(ItemIconPresentation.GetIcon(item), ItemRarityPresentation.Name(item), string.Empty,
                    string.Empty, ReferenceEquals(item, selectedEquipment), false, () =>
                    {
                        if (!isVisible) return;
                        resultStone = null; selectedEquipment = captured; selectedGalduryte = null;
                        PopulateEquipment(); PopulateSockets(false); RefreshPreview();
                    });
                slot.SetItemTooltip(item, null); equipmentSlots.Add(slot);
            }
            equipmentCountText.text = items.Count.ToString();
            SetEmpty(equipmentEmptyText, items.Count == 0, "Brak wyposażenia z osadzonym galdurytem.");
            RestoreScroll(equipmentScroll, position);
        }

        private void PopulateSockets(bool preserveScroll = true)
        {
            var position = preserveScroll && socketsScroll != null ? socketsScroll.verticalNormalizedPosition : 1f;
            Clear(socketSlots);
            var socketed = selectedEquipment?.SocketedGaldurytes;
            if (socketed?.Contains(selectedGalduryte) != true)
                selectedGalduryte = resultStone == null ? socketed?.FirstOrDefault() : null;
            for (var index = 0; index < (socketed?.Count ?? 0); index++)
            {
                var captured = socketed[index];
                var slot = Instantiate(socketSlotTemplate, socketList);
                slot.gameObject.SetActive(true);
                slot.Bind(ItemIconPresentation.GetIcon(captured), ItemRarityPresentation.Name(captured), string.Empty,
                    (index + 1).ToString(), ReferenceEquals(captured, selectedGalduryte), false, () =>
                    {
                        if (!isVisible) return;
                        resultStone = null; selectedGalduryte = captured;
                        PopulateSockets(); RefreshPreview();
                    });
                slot.SetItemTooltip(captured, null); socketSlots.Add(slot);
            }
            socketsSection.SetActive(selectedEquipment != null);
            socketsCountText.text = (socketed?.Count ?? 0).ToString();
            SetEmpty(socketsEmptyText, socketed?.Count == 0, "Brak osadzonych galdurytów.");
            RestoreScroll(socketsScroll, position);
        }

        private void RefreshPreview()
        {
            HideMechanicsFooter();
            var candidate = resultStone == null ? selectedGalduryte : null;
            var stone = resultStone ?? candidate;
            var hasSelection = selectedEquipment != null && candidate != null;
            previewSection.SetActive(selectedEquipment != null); selectedSection.SetActive(stone != null);
            costSection.SetActive(hasSelection);
            selectedHeadingText.text = resultStone != null ? resultHeading : "Do wyjęcia";
            selectedHeadingText.color = resultStone == null ? new Color32(223,215,184,255)
                : resultSucceeded ? new Color32(105,189,128,255) : new Color32(224,108,117,255);
            selectedRow.BindForSocketing(stone, resultStone == null
                ? (selectedEquipment?.SocketedGaldurytes?.IndexOf(stone) ?? -1) + 1 : 0);
            if (resultStone == null && stone != null)
                selectedRow.SetInactive(!selectedEquipment.IsSocketActive(selectedEquipment.SocketedGaldurytes.IndexOf(stone)));
            previewSlot.SetIcon(ItemIconPresentation.GetIcon(selectedEquipment));
            if (selectedEquipment != null) previewSlot.SetItemTooltip(selectedEquipment, null);
            else previewSlot.SetTooltip(string.Empty, string.Empty);
            previewNameText.text = ItemRarityPresentation.Name(selectedEquipment);
            var count = selectedEquipment?.SocketedGaldurytes?.Count ?? 0;
            previewSummaryText.text = selectedEquipment == null ? string.Empty
                : $"{(selectedEquipment.Definition.ItemType == Godmist.Core.Enums.ItemType.Weapon ? "Broń" : "Pancerz")} • Poziom {selectedEquipment.ItemLevel}\n"
                  + UIWorkspaceTooltipView.GetColoredRarityDisplayName(selectedEquipment.CurrentRarity)
                  + $"\nGniazda: {(resultStone != null ? previousCount : count)}/{selectedEquipment.SocketCapacity}"
                  + (resultStone != null ? $" → <color=#69BD80>{count}/{selectedEquipment.SocketCapacity}</color>"
                      : hasSelection ? $" → <color=#69BD80>{count - 1}/{selectedEquipment.SocketCapacity}</color>" : string.Empty);
            var preview = service.CreatePreview(session, enchanter, selectedEquipment, candidate);
            var missing = Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
            costText.text = hasSelection ? preview.GoldCost.ToString("N0") : string.Empty;
            costText.color = missing > 0 ? new Color32(224,108,117,255) : new Color32(242,193,78,255);
            walletText.text = $"Posiadane złoto: {session?.Gold ?? 0:N0}";
            // Legacy destroyed stones are removed on either roll, never returned.
            chanceText.gameObject.SetActive(hasSelection && !candidate.IsDestroyed);
            chanceText.text = $"Szansa odzyskania: {preview.SuccessChance * 100f:0}%";
            extractButton.gameObject.SetActive(hasSelection); extractButton.interactable = preview.CanExtract;
            extractButton.GetComponentInChildren<TMP_Text>(true).text = candidate?.IsDestroyed == true ? "Usuń" : "Wyciągnij";
            SetStatus(!hasSelection || preview.CanExtract ? string.Empty
                : missing > 0 ? $"Brakuje: {missing:N0} złota." : preview.FailureReason);
            RestoreScroll(detailsScroll, 1f);
        }

        // Keep the serialized reference compatible with existing prefabs.
        private void HideMechanicsFooter()
        {
            if (warningText == null) return;
            warningText.text = string.Empty;
            warningText.gameObject.SetActive(false);
        }

        private void TryExtract()
        {
            if (!isVisible || resultStone != null) return;
            if (!service.TryExtract(session, enchanter, selectedEquipment, selectedGalduryte, out var result, out var reason))
            { RefreshPreview(); SetStatus(reason); return; }
            resultStone = selectedGalduryte; previousCount = selectedEquipment.SocketedGaldurytes.Count + 1;
            resultSucceeded = result.Succeeded || result.RemovedDestroyedGalduryte;
            resultHeading = result.RemovedDestroyedGalduryte ? "Usunięty galduryt"
                : result.Succeeded ? "Odzyskany galduryt" : "Niepowodzenie";
            selectedGalduryte = null;
            PopulateEquipment(); PopulateSockets(); RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private void SetStatus(string message)
        { statusText.text = message ?? string.Empty; statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message)); }
        private static void SetEmpty(TMP_Text text, bool empty, string message)
        { text.text = message; text.gameObject.SetActive(empty); }
        private static void RestoreScroll(ScrollRect scroll, float position)
        { if (scroll == null) return; Canvas.ForceUpdateCanvases(); scroll.StopMovement(); scroll.verticalNormalizedPosition = position; }
        private void HideTemplates()
        { equipmentSlotTemplate?.gameObject.SetActive(false); socketSlotTemplate?.gameObject.SetActive(false); socketRowTemplate?.gameObject.SetActive(false); }
        private static void Clear<T>(List<T> instances) where T : Component
        {
            foreach (var instance in instances.Where(instance => instance != null))
            { instance.gameObject.SetActive(false); Destroy(instance.gameObject); }
            instances.Clear();
        }
    }
}
