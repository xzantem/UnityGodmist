using System;
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
    /// <summary>Enchanter tab for inserting an identified galduryte into equipment.</summary>
    public sealed class GalduryteSocketingWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform equipmentGrid;
        [SerializeField] private Transform galduryteGrid;
        [SerializeField] private Transform socketList;
        [SerializeField] private GameObject socketsSection;
        [SerializeField] private BlacksmithCraftingSlotView equipmentSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView galduryteSlotTemplate;
        [SerializeField] private GalduryteSocketRowView socketRowTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button socketButton;

        [SerializeField] private ScrollRect equipmentScroll;
        [SerializeField] private ScrollRect galduryteScroll;
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private TextMeshProUGUI equipmentCountText;
        [SerializeField] private TextMeshProUGUI galduryteCountText;
        [SerializeField] private TextMeshProUGUI equipmentEmptyText;
        [SerializeField] private TextMeshProUGUI galduryteEmptyText;
        [SerializeField] private TextMeshProUGUI walletText;
        [SerializeField] private GameObject previewSection;
        [SerializeField] private GameObject costSection;
        [SerializeField] private GameObject candidateSection;
        [SerializeField] private GameObject installedSection;
        [SerializeField] private GalduryteSocketRowView candidateRow;

        private EquipmentItemInstance completedEquipment;
        private bool isVisible;
        private readonly GalduryteSocketingService service = new();
        private readonly List<BlacksmithCraftingSlotView> equipmentSlots = new();
        private readonly List<BlacksmithCraftingSlotView> galduryteSlots = new();
        private readonly List<GalduryteSocketRowView> socketRows = new();
        private GameSession session;
        private NpcInstance enchanter;
        private EquipmentItemInstance selectedEquipment;
        private GalduryteItemInstance selectedGalduryte;

        private void Awake()
        {
            socketButton?.onClick.AddListener(TrySocket);
            HideTemplates();
        }

        private void OnDestroy() => socketButton?.onClick.RemoveListener(TrySocket);
        private void OnDisable()
        {
            isVisible = false;
            Clear(equipmentSlots); Clear(galduryteSlots); Clear(socketRows);
        }

        public bool Show(NpcInstance nextEnchanter, string _)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextEnchanter?.Definition == null) return false;
            enchanter = nextEnchanter;
            isVisible = true;
            completedEquipment = null;
            serviceRoot?.SetActive(true);
            HideTemplates();
            PopulateEquipment(false);
            PopulateGaldurytes(false);
            RefreshPreview();
            return true;
        }

        public void Hide() { isVisible = false; serviceRoot?.SetActive(false); }

        private void PopulateEquipment(bool preserveScroll = true)
        {
            var position = preserveScroll && equipmentScroll != null ? equipmentScroll.verticalNormalizedPosition : 1f;
            Clear(equipmentSlots);
            // Do not filter the service's shared enumeration: extraction also uses it.
            var items = service.GetSocketableEquipment(session)
                .Where(item => GalduryteSocketingService.GetFreeSockets(item) > 0).ToList();
            if (!items.Contains(selectedEquipment)) selectedEquipment = completedEquipment == null ? items.FirstOrDefault() : null;
            foreach (var item in items)
            {
                var captured = item;
                var slot = Create(equipmentSlotTemplate, equipmentGrid, ItemIconPresentation.GetIcon(captured),
                    ItemRarityPresentation.Name(captured), string.Empty, ReferenceEquals(captured, selectedEquipment), () =>
                    {
                        completedEquipment = null;
                        selectedEquipment = captured;
                        selectedGalduryte = null;
                        PopulateEquipment(); PopulateGaldurytes(false); RefreshPreview();
                    });
                slot?.SetItemTooltip(captured, null);
                if (slot != null) equipmentSlots.Add(slot);
            }
            if (equipmentCountText != null) equipmentCountText.text = items.Count.ToString();
            SetEmpty(equipmentEmptyText, items.Count == 0, "Brak wyposażenia z wolnym gniazdem.");
            RestoreScroll(equipmentScroll, position);
        }

        private void PopulateGaldurytes(bool preserveScroll = true)
        {
            var position = preserveScroll && galduryteScroll != null ? galduryteScroll.verticalNormalizedPosition : 1f;
            Clear(galduryteSlots);
            var items = service.GetIdentifiedGaldurytes(session)
                .Where(item => GalduryteSocketingService.CanSocketPair(selectedEquipment, item, out _)).ToList();
            if (!items.Contains(selectedGalduryte)) selectedGalduryte = completedEquipment == null ? items.FirstOrDefault() : null;
            foreach (var item in items)
            {
                var captured = item;
                var slot = Create(galduryteSlotTemplate, galduryteGrid, ItemIconPresentation.GetIcon(captured),
                    ItemRarityPresentation.Name(captured), string.Empty, ReferenceEquals(captured, selectedGalduryte), () =>
                    {
                        completedEquipment = null;
                        selectedGalduryte = captured;
                        PopulateGaldurytes(); RefreshPreview();
                    });
                slot?.SetItemTooltip(captured, null);
                if (slot != null) galduryteSlots.Add(slot);
            }
            if (galduryteCountText != null) galduryteCountText.text = items.Count.ToString();
            SetEmpty(galduryteEmptyText, items.Count == 0, "Brak pasujących, zidentyfikowanych galdurytów.");
            RestoreScroll(galduryteScroll, position);
        }

        private void RefreshPreview()
        {
            Clear(socketRows);
            var equipment = completedEquipment ?? selectedEquipment;
            var candidate = completedEquipment == null ? selectedGalduryte : null;
            var preview = service.CreatePreview(session, enchanter, selectedEquipment, candidate);
            var hasPair = equipment != null && candidate != null;
            previewSection?.SetActive(equipment != null);
            costSection?.SetActive(hasPair);
            socketsSection?.SetActive(equipment != null);
            candidateSection?.SetActive(hasPair);
            installedSection?.SetActive(equipment?.SocketedGaldurytes?.Count > 0);
            candidateRow?.BindForSocketing(candidate);
            if (previewSlot != null)
            {
                previewSlot.SetIcon(ItemIconPresentation.GetIcon(equipment));
                if (equipment != null) previewSlot.SetItemTooltip(equipment, null);
                else previewSlot.SetTooltip(string.Empty, string.Empty);
            }
            if (previewNameText != null) previewNameText.text = ItemRarityPresentation.Name(equipment);
            if (previewSummaryText != null)
            {
                var count = equipment?.SocketedGaldurytes?.Count ?? 0;
                previewSummaryText.text = equipment == null ? string.Empty
                    : $"{(equipment.Definition.ItemType == Godmist.Core.Enums.ItemType.Weapon ? "Broń" : "Pancerz")} • Poziom {equipment.ItemLevel}\n"
                      + UIWorkspaceTooltipView.GetColoredRarityDisplayName(equipment.CurrentRarity)
                      + $"\nGniazda: {count}/{equipment.SocketCapacity}"
                      + (hasPair ? $" → <color=#69BD80>{count + 1}/{equipment.SocketCapacity}</color>" : string.Empty);
            }
            if (walletText != null) walletText.text = $"Posiadane złoto: {session?.Gold ?? 0:N0}";
            var deficit = Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
            if (costText != null)
            {
                costText.text = hasPair ? preview.GoldCost.ToString("N0") : string.Empty;
                costText.color = deficit > 0 ? new Color32(224,108,117,255) : new Color32(242,193,78,255);
            }
            if (socketButton != null)
            {
                socketButton.gameObject.SetActive(hasPair);
                socketButton.interactable = preview.CanSocket;
            }
            SetStatus(!hasPair || preview.CanSocket ? string.Empty
                : deficit > 0 ? $"Brakuje: {deficit:N0} złota." : preview.FailureReason);
            var index = 0;
            foreach (var stone in equipment?.SocketedGaldurytes ?? new List<GalduryteItemInstance>())
            {
                var row = CreateSocketRow(stone, index + 1);
                row?.SetInactive(!equipment.IsSocketActive(index++));
                if (row != null) socketRows.Add(row);
            }
            RestoreScroll(detailsScroll, 1f);
        }

        private void TrySocket()
        {
            if (!isVisible) return;
            if (!service.TrySocket(session, enchanter, selectedEquipment, selectedGalduryte,
                    out _, out var failure))
            {
                SetStatus(failure);
                return;
            }
            completedEquipment = selectedEquipment;
            selectedGalduryte = null;
            PopulateEquipment(); PopulateGaldurytes(); RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }
        private static void SetEmpty(TMP_Text label, bool empty, string message)
        {
            if (label == null) return;
            label.text = message; label.gameObject.SetActive(empty);
        }
        private static void RestoreScroll(ScrollRect scroll, float position)
        {
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases(); scroll.StopMovement(); scroll.verticalNormalizedPosition = position;
        }

        private BlacksmithCraftingSlotView Create(BlacksmithCraftingSlotView template, Transform parent,
            Sprite icon, string title, string body, bool selected, Action click)
        {
            if (template == null || parent == null) return null;
            var slot = Instantiate(template, parent);
            slot.gameObject.SetActive(true);
            slot.Bind(icon, title, body, string.Empty, selected, false, click);
            return slot;
        }

        private GalduryteSocketRowView CreateSocketRow(GalduryteItemInstance galduryte, int socketNumber)
        {
            if (socketRowTemplate == null || socketList == null) return null;
            var row = Instantiate(socketRowTemplate, socketList);
            row.gameObject.SetActive(true);
            row.BindForSocketing(galduryte, socketNumber);
            return row;
        }

        private static void Clear<T>(List<T> slots) where T : Component
        {
            foreach (var slot in slots.Where(slot => slot != null)) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
            slots.Clear();
        }
        private void HideTemplates()
        {
            equipmentSlotTemplate?.gameObject.SetActive(false);
            galduryteSlotTemplate?.gameObject.SetActive(false);
            socketRowTemplate?.gameObject.SetActive(false);
        }
    }
}
