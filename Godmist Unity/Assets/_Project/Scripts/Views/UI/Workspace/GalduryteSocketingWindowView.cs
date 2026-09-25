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
            Clear(equipmentSlots); Clear(galduryteSlots); Clear(socketRows);
        }

        public bool Show(NpcInstance nextEnchanter, string _)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextEnchanter?.Definition == null) return false;
            enchanter = nextEnchanter;
            serviceRoot?.SetActive(true);
            HideTemplates();
            PopulateEquipment();
            PopulateGaldurytes();
            RefreshPreview();
            return true;
        }

        public void Hide() => serviceRoot?.SetActive(false);

        private void PopulateEquipment()
        {
            Clear(equipmentSlots);
            var items = service.GetSocketableEquipment(session).ToList();
            if (!items.Contains(selectedEquipment)) selectedEquipment = items.FirstOrDefault();
            foreach (var item in items)
            {
                var captured = item;
                var slot = Create(equipmentSlotTemplate, equipmentGrid, captured.Definition.Icon,
                    captured.DisplayName, string.Empty, ReferenceEquals(captured, selectedEquipment), () =>
                    {
                        selectedEquipment = captured;
                        PopulateEquipment(); PopulateGaldurytes(); RefreshPreview();
                    });
                slot?.SetItemTooltip(captured, null);
                if (slot != null) equipmentSlots.Add(slot);
            }
        }

        private void PopulateGaldurytes()
        {
            Clear(galduryteSlots);
            var expected = selectedEquipment?.Definition?.ItemType;
            var items = service.GetIdentifiedGaldurytes(session)
                .Where(item => expected == null || (expected == Godmist.Core.Enums.ItemType.Weapon
                    ? item.TargetType == Godmist.Core.Enums.GalduryteTargetType.Weapon
                    : item.TargetType == Godmist.Core.Enums.GalduryteTargetType.Armor))
                .ToList();
            if (!items.Contains(selectedGalduryte)) selectedGalduryte = items.FirstOrDefault();
            foreach (var item in items)
            {
                var captured = item;
                var slot = Create(galduryteSlotTemplate, galduryteGrid, captured.Definition.Icon,
                    captured.DisplayName, captured.GetEffectSummary(), ReferenceEquals(captured, selectedGalduryte), () =>
                    {
                        selectedGalduryte = captured;
                        PopulateGaldurytes(); RefreshPreview();
                    });
                slot?.SetItemTooltip(captured, null);
                if (slot != null) galduryteSlots.Add(slot);
            }
        }

        private void RefreshPreview()
        {
            Clear(socketRows);
            if (socketsSection != null)
                socketsSection.SetActive(selectedEquipment != null);
            var preview = service.CreatePreview(session, enchanter, selectedEquipment, selectedGalduryte);
            if (previewSlot != null)
            {
                previewSlot.SetIcon(selectedEquipment?.Definition?.Icon);
                if (selectedEquipment != null) previewSlot.SetItemTooltip(selectedEquipment, null);
                else previewSlot.SetTooltip("Wybierz wyposażenie", string.Empty);
            }
            if (previewNameText != null) previewNameText.text = selectedEquipment?.DisplayName ?? "Wybierz wyposażenie";
            if (previewSummaryText != null) previewSummaryText.text = selectedEquipment == null
                ? "Wybierz broń lub pancerz."
                : $"Galduryty: {selectedEquipment.SocketedGaldurytes?.Count ?? 0}/{selectedEquipment.SocketCapacity}";
            if (costText != null)
            {
                var missingGold = Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
                costText.text = selectedGalduryte == null ? "Koszt: —"
                    : missingGold > 0
                        ? $"<color=#E06C75>Koszt: {preview.GoldCost} g • Brakuje {missingGold} g</color>"
                        : $"<color=#F2C14E>Koszt: {preview.GoldCost} g</color>";
            }
            if (socketButton != null) socketButton.interactable = preview.CanSocket;
            if (statusText != null) statusText.text = preview.CanSocket ? string.Empty : preview.FailureReason;
            if (statusText != null) statusText.gameObject.SetActive(!string.IsNullOrEmpty(statusText.text));

            if (selectedEquipment == null) return;
            var socketIndex = 0;
            foreach (var galduryte in selectedEquipment.SocketedGaldurytes ?? new List<GalduryteItemInstance>())
            {
                var row = CreateSocketRow(galduryte, false);
                row?.SetInactive(!selectedEquipment.IsSocketActive(socketIndex++));
                if (row != null) socketRows.Add(row);
            }
            for (var index = selectedEquipment.SocketedGaldurytes?.Count ?? 0;
                 index < selectedEquipment.SocketCapacity; index++)
            {
                var targeted = index == (selectedEquipment.SocketedGaldurytes?.Count ?? 0) && selectedGalduryte != null;
                var row = CreateSocketRow(targeted ? selectedGalduryte : null, targeted);
                if (row != null) socketRows.Add(row);
            }
        }

        private void TrySocket()
        {
            if (!service.TrySocket(session, enchanter, selectedEquipment, selectedGalduryte,
                    out var paid, out var failure))
            {
                if (statusText != null) statusText.text = failure;
                if (statusText != null) statusText.gameObject.SetActive(true);
                return;
            }
            if (statusText != null) statusText.text = $"Osadzono galduryt za {paid} g.";
            PopulateEquipment(); PopulateGaldurytes(); RefreshPreview();
            CharacterInfoWindowView.RefreshOpenWindows(session);
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

        private GalduryteSocketRowView CreateSocketRow(GalduryteItemInstance galduryte, bool targeted)
        {
            if (socketRowTemplate == null || socketList == null) return null;
            var row = Instantiate(socketRowTemplate, socketList);
            row.gameObject.SetActive(true);
            row.Bind(galduryte, targeted);
            return row;
        }

        private static void Clear<T>(List<T> slots) where T : Component
        {
            foreach (var slot in slots.Where(slot => slot != null)) Destroy(slot.gameObject);
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
