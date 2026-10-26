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

        [SerializeField] private ScrollRect identificationScroll;
        [SerializeField] private TextMeshProUGUI itemCountText;
        [SerializeField] private TextMeshProUGUI emptyText;
        [SerializeField] private TextMeshProUGUI walletText;
        [SerializeField] private TextMeshProUGUI revealedEffectsText;
        [SerializeField] private GameObject previewSection;
        [SerializeField] private GameObject costSection;
        [SerializeField] private GameObject effectsSection;
        [SerializeField] private GameObject summarySpacer;

        private GalduryteItemInstance identifiedResult;
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
            identifiedResult = null;
            serviceRoot?.SetActive(true);
            PopulateOptions(false);
            return true;
        }

        public void Hide()
        {
            isVisible = false;
            serviceRoot?.SetActive(false);
        }

        private void PopulateOptions(bool preserveScroll = true)
        {
            var position = preserveScroll && identificationScroll != null
                ? identificationScroll.verticalNormalizedPosition : 1f;
            ClearSlots();
            var options = GetPartyGaldurytes().ToList();
            if (identifiedResult == null && (selectedGalduryte == null
                || options.All(option => option.InstanceId != selectedGalduryte.InstanceId)))
            {
                selectedGalduryte = options.FirstOrDefault();
            }

            foreach (var option in options)
            {
                var captured = option;
                var slot = CreateSlot(
                    ItemIconPresentation.GetIcon(captured),
                    ItemRarityPresentation.Name(captured),
                    string.Empty,
                    string.Empty,
                    selectedGalduryte?.InstanceId == captured.InstanceId,
                    () =>
                    {
                        identifiedResult = null;
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

            if (itemCountText != null) itemCountText.text = options.Count.ToString();
            if (emptyText != null)
            {
                emptyText.text = "Brak niezidentyfikowanych galdurytów.";
                emptyText.gameObject.SetActive(options.Count == 0);
            }
            Canvas.ForceUpdateCanvases();
            if (identificationScroll != null)
            {
                identificationScroll.StopMovement();
                identificationScroll.verticalNormalizedPosition = position;
            }
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var preview = identifiedResult != null ? null : service.CreateIdentificationPreview(
                session, enchanter, new GalduryteIdentificationRequest
                { GalduryteInstanceId = selectedGalduryte?.InstanceId ?? string.Empty });
            var item = identifiedResult ?? preview?.Galduryte;
            var revealed = identifiedResult != null && identifiedResult.IsIdentified;
            var hasItem = item != null;
            previewSection?.SetActive(hasItem);
            costSection?.SetActive(hasItem && !revealed);
            effectsSection?.SetActive(revealed);
            summarySpacer?.SetActive(!revealed);
            if (identifyButton != null)
            {
                identifyButton.gameObject.SetActive(hasItem && !revealed);
                identifyButton.interactable = preview?.CanIdentify == true;
            }
            if (previewTooltipSlot != null)
            {
                previewTooltipSlot.SetIcon(ItemIconPresentation.GetIcon(item));
                if (hasItem) previewTooltipSlot.SetItemTooltip(item, null);
                else previewTooltipSlot.SetTooltip(string.Empty, string.Empty);
            }
            if (previewNameText != null) previewNameText.text = ItemRarityPresentation.Name(item);
            if (previewSummaryText != null)
                previewSummaryText.text = hasItem
                    ? $"{GetTargetName(item.TargetType)} • Poziom {item.ItemLevel}\n"
                        + UIWorkspaceTooltipView.GetColoredRarityDisplayName(item.CurrentRarity)
                        + (revealed ? "\nZidentyfikowany" : "\nEfekty: nieznane")
                    : string.Empty;
            // The existing item owns the rolled effects; never reveal them before payment.
            if (revealedEffectsText != null)
                revealedEffectsText.text = revealed ? GetRevealedEffects(item) : string.Empty;
            if (walletText != null) walletText.text = $"Posiadane złoto: {session?.Gold ?? 0:N0}";
            var missingGold = preview == null ? 0 : Mathf.Max(0, preview.GoldCost - (session?.Gold ?? 0));
            if (costText != null)
            {
                costText.text = preview?.GoldCost.ToString("N0") ?? string.Empty;
                costText.color = missingGold > 0 ? new Color32(224,108,117,255) : new Color32(242,193,78,255);
            }
            SetStatus(!hasItem || revealed || preview?.CanIdentify == true ? string.Empty
                : missingGold > 0 ? $"Brakuje: {missingGold:N0} złota." : preview?.FailureReason);
        }

        private static string GetRevealedEffects(GalduryteItemInstance item)
        {
            var summary = item.GetEffectSummary();
            return item.IsDestroyed || !item.IsIdentified ? summary
                : "• " + summary.Replace("\n", "\n• ");
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
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
                SetStatus(failureReason);
                return;
            }

            identifiedResult = identified;
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
            foreach (var slot in slots.Where(slot => slot != null))
            {
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            slots.Clear();
        }

    }
}
