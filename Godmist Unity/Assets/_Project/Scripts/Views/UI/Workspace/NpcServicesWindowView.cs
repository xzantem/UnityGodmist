using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    [Serializable]
    public sealed class NpcServiceTabDefinition
    {
        public NpcServiceKind Kind;
        public Sprite Icon;
    }

    /// <summary>
    /// One UI Workspace shell for services offered by an interacted NPC. Roles
    /// determine available tabs; individual content views retain service logic.
    /// </summary>
    public sealed class NpcServicesWindowView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform tabsContent;
        [SerializeField] private NpcServiceTabView tabTemplate;
        [SerializeField] private NpcServiceTabDefinition[] tabDefinitions;
        [SerializeField] private TradeServiceWindowView tradeService;
        [SerializeField] private BlacksmithCraftingWindowView equipmentCraftingService;
        [SerializeField] private MaterialCraftingWindowView materialCraftingService;
        [SerializeField] private EquipmentUpgradeWindowView equipmentUpgradeService;
        [SerializeField] private EquipmentUpgradeWindowView equipmentReforgeService;

        private readonly List<NpcServiceTabView> spawnedTabs = new();
        private readonly NpcLoyaltyService loyaltyService = new();
        private GameSession session;
        private NpcInstance npc;
        private string recipientCharacterId = string.Empty;
        private NpcServiceKind? selectedService;
        private bool isServiceSessionOpen;
        private NpcLoyaltyHeaderView loyaltyHeader;
        private int lastDisplayedLoyaltyLevel = -1;
        private long lastDisplayedGoldSpent = -1;

        private void Awake()
        {
            if (tabTemplate != null) tabTemplate.gameObject.SetActive(false);
            EnsureLoyaltyHeader();

            // TradeServiceWindowView is a serialized child/content reference of
            // PF_UIW_NpcServicesWindow. Never create a second one at runtime:
            // doing so loses all prefab references and leaves a visible template
            // or an empty, fallback-generated layout behind.
            if (tradeService == null)
            {
                Debug.LogError(
                    "NpcServicesWindowView has no serialized TradeServiceWindowView. "
                    + "Assign the Trade tab component in PF_UIW_NpcServicesWindow.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (!isServiceSessionOpen) return;
            EndServiceSession();
        }

        private void Update()
        {
            if (!isServiceSessionOpen || npc == null) return;

            var currentLevel = npc.LoyaltyLevel;
            var goldSpent = loyaltyService.GetGoldSpentTowardNextLevel(npc);
            if (currentLevel == lastDisplayedLoyaltyLevel
                && goldSpent == lastDisplayedGoldSpent)
            {
                return;
            }

            UpdateLoyaltyHeader();
        }

        public bool Open(NpcInstance nextNpc, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextNpc?.Definition == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId)
                || !session.PlayerParty.IsActiveMember(nextRecipientCharacterId))
            {
                return false;
            }

            npc = nextNpc;
            recipientCharacterId = nextRecipientCharacterId;
            BuildTabs();
            if (!GetAvailableServices().Any()) return false;

            if (!session.TrySetMode(GameModeState.Menu)) return false;

            isServiceSessionOpen = true;
            if (titleText != null)
            {
                titleText.text = $"Usługi: {npc.Definition.DisplayName}";
            }
            UpdateLoyaltyHeader();
            window?.Open();
            SelectService(GetDefaultService());
            return true;
        }

        private void BuildTabs()
        {
            ClearTabs();
            foreach (var kind in GetAvailableServices())
            {
                if (tabTemplate == null || tabsContent == null) continue;
                var tab = Instantiate(tabTemplate, tabsContent);
                tab.gameObject.SetActive(true);
                tab.Bind(GetTabDefinition(kind)?.Icon, selectedService == kind,
                    () => SelectService(kind));
                spawnedTabs.Add(tab);
            }
        }

        private IEnumerable<NpcServiceKind> GetAvailableServices()
        {
            if (npc?.Definition?.GetRole<TraderRoleData>() != null)
            {
                yield return NpcServiceKind.Trade;
            }

            if (npc?.Definition?.GetRole<BlacksmithRoleData>() != null)
            {
                yield return NpcServiceKind.EquipmentCrafting;
                yield return NpcServiceKind.MaterialCrafting;
                yield return NpcServiceKind.EquipmentUpgrade;
                yield return NpcServiceKind.EquipmentReforge;
            }
        }

        private NpcServiceKind GetDefaultService()
        {
            foreach (var service in GetAvailableServices()) return service;
            return NpcServiceKind.EquipmentCrafting;
        }

        private void SelectService(NpcServiceKind kind)
        {
            HideSelectedService();
            selectedService = kind;

            var didShow = kind switch
            {
                NpcServiceKind.Trade => tradeService != null
                    && tradeService.Show(npc, recipientCharacterId),
                NpcServiceKind.EquipmentCrafting => equipmentCraftingService != null
                    && equipmentCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.MaterialCrafting => materialCraftingService != null
                    && materialCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.EquipmentUpgrade => equipmentUpgradeService != null
                    && equipmentUpgradeService.Show(npc, recipientCharacterId),
                NpcServiceKind.EquipmentReforge => equipmentReforgeService != null
                    && equipmentReforgeService.Show(npc, recipientCharacterId),
                _ => false
            };

            if (!didShow)
            {
                selectedService = null;
                return;
            }

            RefreshTabSelection();
        }

        private void HideSelectedService()
        {
            switch (selectedService)
            {
                case NpcServiceKind.Trade:
                    tradeService?.Hide();
                    break;
                case NpcServiceKind.EquipmentCrafting:
                    equipmentCraftingService?.Hide();
                    break;
                case NpcServiceKind.MaterialCrafting:
                    materialCraftingService?.Hide();
                    break;
                case NpcServiceKind.EquipmentUpgrade:
                    equipmentUpgradeService?.Hide();
                    break;
                case NpcServiceKind.EquipmentReforge:
                    equipmentReforgeService?.Hide();
                    break;
            }
        }

        private void RefreshTabSelection()
        {
            for (var index = 0; index < spawnedTabs.Count; index++)
            {
                var kind = GetAvailableServiceAt(index);
                spawnedTabs[index].Bind(GetTabDefinition(kind)?.Icon,
                    selectedService == kind, () => SelectService(kind));
            }
        }

        private NpcServiceKind GetAvailableServiceAt(int index)
        {
            return GetAvailableServices().ElementAt(index);
        }

        private NpcServiceTabDefinition GetTabDefinition(NpcServiceKind kind)
        {
            return (tabDefinitions ?? Array.Empty<NpcServiceTabDefinition>())
                .FirstOrDefault(definition => definition != null && definition.Kind == kind);
        }

        private void EndServiceSession()
        {
            HideSelectedService();
            selectedService = null;
            npc = null;
            recipientCharacterId = string.Empty;
            isServiceSessionOpen = false;
            lastDisplayedLoyaltyLevel = -1;
            lastDisplayedGoldSpent = -1;

            if (session != null && session.CurrentMode == GameModeState.Menu)
            {
                session.TrySetMode(GameModeState.Exploration);
            }
        }

        private void ClearTabs()
        {
            foreach (var tab in spawnedTabs)
            {
                if (tab != null) Destroy(tab.gameObject);
            }
            spawnedTabs.Clear();
        }

        private void EnsureLoyaltyHeader()
        {
            if (titleText == null) return;

            var header = titleText.transform.parent as RectTransform;
            if (header == null) return;

            var existing = header.GetComponentInChildren<NpcLoyaltyHeaderView>(true);
            if (existing != null)
            {
                loyaltyHeader = existing;
                return;
            }

            var indicatorObject = new GameObject(
                "NpcLoyaltyHeader",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(NpcLoyaltyHeaderView));
            indicatorObject.transform.SetParent(header, false);

            var indicatorRect = (RectTransform)indicatorObject.transform;
            indicatorRect.anchorMin = new Vector2(1f, 0f);
            indicatorRect.anchorMax = new Vector2(1f, 1f);
            indicatorRect.pivot = new Vector2(1f, .5f);
            indicatorRect.sizeDelta = new Vector2(220f, 0f);
            indicatorRect.anchoredPosition = new Vector2(-56f, 0f);

            var indicatorText = indicatorObject.GetComponent<TextMeshProUGUI>();
            indicatorText.font = titleText.font;
            indicatorText.fontSize = Mathf.Min(titleText.fontSize, 20f);
            indicatorText.fontSizeMin = 12f;
            indicatorText.fontSizeMax = indicatorText.fontSize;
            indicatorText.enableAutoSizing = true;
            indicatorText.color = titleText.color;
            indicatorText.alignment = TextAlignmentOptions.Right;
            indicatorText.verticalAlignment = VerticalAlignmentOptions.Middle;
            indicatorText.overflowMode = TextOverflowModes.Ellipsis;
            indicatorText.raycastTarget = true;
            indicatorText.margin = new Vector4(0f, 0f, 8f, 0f);

            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(16f, 0f);
            titleRect.offsetMax = new Vector2(-288f, 0f);
            titleText.alignment = TextAlignmentOptions.Left;

            loyaltyHeader = indicatorObject.GetComponent<NpcLoyaltyHeaderView>();
        }

        private void UpdateLoyaltyHeader()
        {
            if (loyaltyHeader == null) EnsureLoyaltyHeader();
            if (loyaltyHeader == null || npc == null) return;

            var currentLevel = npc.LoyaltyLevel;
            var spent = loyaltyService.GetGoldSpentTowardNextLevel(npc);
            var required = loyaltyService.GetGoldRequiredForNextLevel(npc);
            var progress = currentLevel >= loyaltyService.MaximumLevel
                ? $"{currentLevel} (MAX)"
                : $"{currentLevel} ({spent}/{required})";

            loyaltyHeader.GetComponent<TextMeshProUGUI>().text = $"Lojalność: {progress}";
            loyaltyHeader.Bind(npc, BuildLoyaltyTooltipData(npc));
            loyaltyHeader.RefreshTooltip();
            lastDisplayedLoyaltyLevel = currentLevel;
            lastDisplayedGoldSpent = spent;
        }

        private NpcLoyaltyTooltipData BuildLoyaltyTooltipData(NpcInstance targetNpc)
        {
            var currentLevel = targetNpc.LoyaltyLevel;
            var maximumLevel = loyaltyService.MaximumLevel;
            var breakpoints = new List<NpcLoyaltyBreakpointPresentation>();
            for (var level = 1; level <= maximumLevel; level++)
            {
                var labels = new List<string>();
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Common,
                    "Handel materiałami pospolitymi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Uncommon,
                    "Handel materiałami niepospolitymi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Rare,
                    "Handel materiałami rzadkimi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Ancient,
                    "Handel materiałami starożytnymi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Legendary,
                    "Handel materiałami legendarnymi");

                if (level > 1
                    && loyaltyService.GetDiscountPercent(level)
                        != loyaltyService.GetDiscountPercent(level - 1))
                {
                    labels.Add($"Ceny usług niższe o {loyaltyService.GetDiscountPercent(level)}%");
                }

                AppendBlacksmithSpecialEffectLabel(labels, targetNpc, level);
                if (labels.Count == 0) continue;

                breakpoints.Add(new NpcLoyaltyBreakpointPresentation(
                    level,
                    string.Join(" · ", labels),
                    currentLevel >= level));
            }

            return new NpcLoyaltyTooltipData(
                currentLevel,
                maximumLevel,
                loyaltyService.GetDiscountPercent(targetNpc),
                loyaltyService.GetGoldSpentTowardNextLevel(targetNpc),
                loyaltyService.GetGoldRequiredForNextLevel(targetNpc),
                breakpoints);
        }

        private void AppendMaterialBreakpointLabel(
            List<string> labels,
            int level,
            RarityTier rarity,
            string label)
        {
            if (loyaltyService.GetMaterialTradeUnlockLevel(rarity) == level)
                labels.Add(label);
        }

        private static void AppendBlacksmithSpecialEffectLabel(
            List<string> labels,
            NpcInstance targetNpc,
            int level)
        {
            if (targetNpc?.Definition?.GetRole<BlacksmithRoleData>() == null) return;

            var label = level switch
            {
                3 => "Tworzony ekwipunek ma o 5% lepsze statystyki główne",
                6 => "Nowy ekwipunek zaczyna z 25% ulepszenia",
                11 => "Niższy koszt ponownego ulepszania",
                14 => "Tworzony ekwipunek ma co najmniej 4 unikalności",
                15 => "Przekuwanie ekwipunku nie może obniżyć unikalności",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(label)) labels.Add(label);
        }

    }

    public sealed class NpcLoyaltyHeaderView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private NpcInstance npc;
        private NpcLoyaltyTooltipData tooltipData;

        public void Bind(NpcInstance nextNpc, NpcLoyaltyTooltipData nextTooltipData)
        {
            npc = nextNpc;
            tooltipData = nextTooltipData;
        }

        public void RefreshTooltip()
        {
            if (tooltipData == null) return;
            UIWorkspaceTooltipView.ActiveInstance?.RefreshNpcLoyaltyTooltip(tooltipData, this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (npc == null) return;
            UIWorkspaceTooltipView.ActiveInstance?.ShowNpcLoyalty(tooltipData, this);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}
