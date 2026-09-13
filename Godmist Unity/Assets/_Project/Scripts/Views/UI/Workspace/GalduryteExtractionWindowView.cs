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

        private readonly GalduryteExtractionService service = new();
        private readonly List<BlacksmithCraftingSlotView> equipmentSlots = new();
        private readonly List<GalduryteSocketRowView> rows = new();
        private GameSession session;
        private NpcInstance enchanter;
        private EquipmentItemInstance selectedEquipment;
        private GalduryteItemInstance selectedGalduryte;

        private void Awake() => extractButton.onClick.AddListener(TryExtract);
        private void OnDestroy() => extractButton?.onClick.RemoveListener(TryExtract);
        private void OnDisable() { Clear(equipmentSlots); Clear(rows); }
        public void Hide() => serviceRoot.SetActive(false);

        public bool Show(NpcInstance npc, string _)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || npc?.Definition == null) return false;
            enchanter = npc;
            serviceRoot.SetActive(true);
            PopulateEquipment();
            RefreshPreview();
            return true;
        }

        private void PopulateEquipment()
        {
            Clear(equipmentSlots);
            var items = new GalduryteSocketingService().GetSocketableEquipment(session).ToList();
            if (!items.Contains(selectedEquipment)) selectedEquipment = items.FirstOrDefault();
            foreach (var item in items)
            {
                var captured = item;
                var slot = Instantiate(equipmentSlotTemplate, equipmentGrid);
                slot.gameObject.SetActive(true);
                slot.Bind(item.Definition.Icon, item.DisplayName, string.Empty, string.Empty,
                    ReferenceEquals(item, selectedEquipment), false, () =>
                    {
                        selectedEquipment = captured;
                        selectedGalduryte = null;
                        PopulateEquipment();
                        RefreshPreview();
                    });
                slot.SetItemTooltip(item, null);
                equipmentSlots.Add(slot);
            }
        }

        private void RefreshPreview()
        {
            Clear(rows);
            socketsSection.SetActive(selectedEquipment != null);
            var socketed = selectedEquipment?.SocketedGaldurytes;
            if (socketed?.Contains(selectedGalduryte) != true) selectedGalduryte = socketed?.FirstOrDefault();
            foreach (var item in socketed ?? new List<GalduryteItemInstance>())
            {
                var captured = item;
                var row = Instantiate(socketRowTemplate, socketList);
                row.gameObject.SetActive(true);
                row.BindForExtraction(item, ReferenceEquals(item, selectedGalduryte), () =>
                {
                    selectedGalduryte = captured;
                    RefreshPreview();
                });
                rows.Add(row);
            }
            previewSlot.SetIcon(selectedEquipment?.Definition?.Icon);
            if (selectedEquipment != null) previewSlot.SetItemTooltip(selectedEquipment, null);
            else previewSlot.SetTooltip("Wybierz wyposażenie", string.Empty);
            previewNameText.text = selectedEquipment?.DisplayName ?? "Wybierz wyposażenie";
            previewSummaryText.text = selectedEquipment == null ? "Wybierz broń lub pancerz."
                : $"Galduryty: {socketed?.Count ?? 0}/{selectedEquipment.SocketCapacity}";
            var preview = service.CreatePreview(session, enchanter, selectedEquipment, selectedGalduryte);
            var missing = Mathf.Max(0, preview.GoldCost - session.Gold);
            costText.text = selectedGalduryte == null ? "Koszt: —" : missing > 0
                ? $"<color=#E06C75>Koszt: {preview.GoldCost} g • Brakuje {missing} g</color>"
                : $"<color=#F2C14E>Koszt: {preview.GoldCost} g</color>";
            chanceText.text = selectedGalduryte == null ? "Szansa powodzenia: —"
                : $"Szansa powodzenia: {preview.SuccessChance * 100f:0}%";
            warningText.text = selectedGalduryte == null ? string.Empty
                : (selectedGalduryte.IsDestroyed
                        ? "Powodzenie usunie zniszczony galduryt. Opłata jest pobierana za każdą próbę."
                        : "Niepowodzenie niszczy galduryt, ale pozostawia go w wyposażeniu. Opłata jest pobierana za każdą próbę.");
            extractButton.interactable = preview.CanExtract;
            statusText.text = preview.CanExtract ? string.Empty : preview.FailureReason;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(statusText.text));
            warningText.gameObject.SetActive(selectedGalduryte != null);
        }

        private void TryExtract()
        {
            if (!service.TryExtract(session, enchanter, selectedEquipment, selectedGalduryte, out var result, out var reason))
            {
                RefreshPreview();
                statusText.text = reason;
                statusText.gameObject.SetActive(true);
                return;
            }
            PopulateEquipment();
            RefreshPreview();
            statusText.text = result.Succeeded
                ? result.RemovedDestroyedGalduryte ? "Usunięto zniszczony galduryt." : "Wyciągnięto galduryt."
                : "Niepowodzenie — galduryt jest zniszczony.";
            statusText.gameObject.SetActive(true);
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private static void Clear<T>(List<T> instances) where T : Component
        {
            foreach (var instance in instances)
            {
                if (instance == null) continue;
                instance.gameObject.SetActive(false);
                Destroy(instance.gameObject);
            }
            instances.Clear();
        }
    }
}
