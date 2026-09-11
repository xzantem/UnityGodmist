using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
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
    /// <summary>Separate Enchanter service for revealing an existing galduryte.</summary>
    public sealed class GalduryteIdentificationWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject serviceRoot;
        [SerializeField] private Transform identificationGrid;
        [SerializeField] private BlacksmithCraftingSlotView identificationSlotTemplate;
        [SerializeField] private BlacksmithCraftingSlotView previewTooltipSlot;
        [SerializeField] private TextMeshProUGUI previewNameText;
        [SerializeField] private TextMeshProUGUI previewSummaryText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button identifyButton;

        private readonly GalduryteCraftingService service = new();
        private readonly List<BlacksmithCraftingSlotView> slots = new();
        private GameSession session;
        private NpcInstance enchanter;
        private GalduryteItemInstance selectedGalduryte;
        private bool isVisible;

        private void Awake()
        {
            identifyButton?.onClick.AddListener(TryIdentify);
            identificationSlotTemplate?.gameObject.SetActive(false);
        }

        private void OnDestroy() => identifyButton?.onClick.RemoveListener(TryIdentify);

        private void OnDisable()
        {
            isVisible = false;
            ClearSlots();
        }

        public bool Show(NpcInstance nextEnchanter, string _)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextEnchanter?.Definition?.GetRole<EnchanterRoleData>() == null)
                return false;

            enchanter = nextEnchanter;
            isVisible = true;
            serviceRoot?.SetActive(true);
            PopulateOptions();
            return true;
        }

        public void Hide() => serviceRoot?.SetActive(false);

        private void PopulateOptions()
        {
            ClearSlots();
            var options = GetPartyGaldurytes().ToList();
            if (selectedGalduryte == null
                || options.All(option => option.InstanceId != selectedGalduryte.InstanceId))
            {
                selectedGalduryte = options.FirstOrDefault();
            }

            foreach (var option in options)
            {
                var captured = option;
                var slot = CreateSlot(
                    captured.Definition?.Icon,
                    captured.DisplayName,
                    string.Empty,
                    string.Empty,
                    selectedGalduryte?.InstanceId == captured.InstanceId,
                    () =>
                    {
                        selectedGalduryte = captured;
                        PopulateOptions();
                    });
                if (slot != null)
                {
                    // Use the same item tooltip as the inventory. The selection
                    // card itself should not duplicate level/effect information.
                    slot.SetItemTooltip(captured, null);
                    slots.Add(slot);
                }
            }

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var preview = service.CreateIdentificationPreview(
                session,
                enchanter,
                new GalduryteIdentificationRequest
                {
                    GalduryteInstanceId = selectedGalduryte?.InstanceId ?? string.Empty
                });

            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(preview.Galduryte?.Definition?.Icon);
                if (preview.Galduryte != null)
                    previewTooltipSlot.SetItemTooltip(preview.Galduryte, null);
                else
                    previewTooltipSlot.SetTooltip(
                        "Identyfikacja galdurytu",
                        preview.FailureReason ?? "Wybierz niezidentyfikowany galduryt.");
            }

            if (previewNameText != null)
                previewNameText.text = preview.Galduryte?.DisplayName ?? "Wybierz galduryt";
            if (previewSummaryText != null)
            {
                previewSummaryText.text = preview.Galduryte == null
                    ? "Wybierz niezidentyfikowany galduryt z ekwipunku."
                    : $"{GetTargetName(preview.Galduryte.TargetType)} • "
                        + $"Poziom {preview.Galduryte.ItemLevel}";
            }
            if (costText != null)
            {
                var missingGold = Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
                costText.text = preview.CanIdentify
                    ? $"<color=#F2C14E>Koszt: {preview.GoldCost} g</color>"
                    : $"<color=#E06C75>Koszt: {preview.GoldCost} g • Brakuje {missingGold} g</color>";
            }
            if (identifyButton != null) identifyButton.interactable = preview.CanIdentify;
            if (statusText != null && preview.Galduryte == null)
                statusText.text = preview.FailureReason;
        }

        private static string GetTargetName(GalduryteTargetType targetType) => targetType switch
        {
            GalduryteTargetType.Weapon => "Broń",
            GalduryteTargetType.Armor => "Pancerz",
            _ => "Galduryt"
        };

        private void TryIdentify()
        {
            if (!isVisible) return;
            if (!service.TryIdentify(
                    session,
                    enchanter,
                    new GalduryteIdentificationRequest
                    {
                        GalduryteInstanceId = selectedGalduryte?.InstanceId ?? string.Empty
                    },
                    out var identified,
                    out var paidGold,
                    out var failureReason))
            {
                if (statusText != null) statusText.text = failureReason;
                return;
            }

            if (statusText != null)
                statusText.text = $"Zidentyfikowano: {identified.DisplayName} za {paidGold}g.";
            selectedGalduryte = null;
            PopulateOptions();
            CharacterInfoWindowView.RefreshOpenWindows(session);
        }

        private IEnumerable<GalduryteItemInstance> GetPartyGaldurytes()
        {
            foreach (var characterId in session?.PlayerParty.ActiveMemberIds ?? Enumerable.Empty<string>())
            {
                if (!session.CharactersById.TryGetValue(characterId, out var character)) continue;
                foreach (var item in character.Inventory?.Items?.OfType<GalduryteItemInstance>()
                    ?? Enumerable.Empty<GalduryteItemInstance>())
                {
                    if (!item.IsIdentified) yield return item;
                }
            }

            foreach (var item in session?.Stash.Items?.OfType<GalduryteItemInstance>()
                ?? Enumerable.Empty<GalduryteItemInstance>())
            {
                if (!item.IsIdentified) yield return item;
            }
        }

        private BlacksmithCraftingSlotView CreateSlot(
            Sprite icon,
            string title,
            string description,
            string countLabel,
            bool selected,
            Action selectAction)
        {
            if (identificationSlotTemplate == null || identificationGrid == null) return null;
            var slot = Instantiate(identificationSlotTemplate, identificationGrid);
            slot.gameObject.SetActive(true);
            slot.Bind(icon, title, description, countLabel, selected, false, selectAction);
            return slot;
        }

        private void ClearSlots()
        {
            foreach (var slot in slots.Where(slot => slot != null)) Destroy(slot.gameObject);
            slots.Clear();
        }

    }
}
