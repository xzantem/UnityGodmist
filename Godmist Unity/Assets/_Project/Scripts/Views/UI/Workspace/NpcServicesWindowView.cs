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
using UnityEngine.UI;

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
        [SerializeField] private JewelryCraftingWindowView jewelryCraftingService;
        [SerializeField] private GalduryteCraftingWindowView galduryteCraftingService;
        [SerializeField] private GalduryteIdentificationWindowView galduryteIdentificationService;
        [SerializeField] private GalduryteSocketingWindowView galduryteSocketingService;
        [SerializeField] private GalduryteExtractionWindowView galduryteExtractionService;
        [SerializeField] private AlchemyCraftingWindowView potionCraftingService;
        [SerializeField] private AlchemyCraftingWindowView bombCraftingService;
        [SerializeField] private PotionRefillWindowView potionRefillService;
        [SerializeField] private PotionRefillWindowView bombRefillService;

        [SerializeField] private CompanionRecoveryWindowView companionRecoveryService;
        private readonly List<NpcServiceTabView> spawnedTabs = new();
        private readonly NpcLoyaltyService loyaltyService = new();
        private GameSession session;
        private NpcInstance npc;
        private string recipientCharacterId = string.Empty;
        private NpcServiceKind? selectedService;
        private bool isServiceSessionOpen;
        [SerializeField] private NpcLoyaltyHeaderView loyaltyHeader;
        private int lastDisplayedLoyaltyLevel = -1;
        private long lastDisplayedGoldSpent = -1;

        private void Awake()
        {
            if (tabTemplate != null) tabTemplate.gameObject.SetActive(false);

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

            // Normalize runtime-created loyalty UI every time the service
            // window opens. This also repairs instances kept alive while
            // Unity reloads scripts with domain reload disabled.

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
                tab.Bind(
                    GetTabDefinition(kind)?.Icon,
                    GetServiceDisplayName(kind) + (IsServiceUnlocked(kind) ? string.Empty : " — " + GalduryteServiceAccess.LockedMessage),
                    selectedService == kind,
                    () => SelectService(kind));
                tab.SetAvailable(IsServiceUnlocked(kind));
                spawnedTabs.Add(tab);
            }
        }

        private IEnumerable<NpcServiceKind> GetAvailableServices()
        {
            if (npc?.Definition?.GetRole<PriestRoleData>() != null) yield return NpcServiceKind.CompanionRecovery;
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

            if (npc?.Definition?.GetRole<AlchemistRoleData>() != null)
            {
                yield return NpcServiceKind.MaterialCrafting;
                yield return NpcServiceKind.PotionCrafting;
                yield return NpcServiceKind.BombCrafting;
                yield return NpcServiceKind.PotionRefill;
                yield return NpcServiceKind.BombRefill;
            }

            if (npc?.Definition?.GetRole<EnchanterRoleData>() != null)
            {
                yield return NpcServiceKind.JewelryCrafting;
                yield return NpcServiceKind.GalduryteCrafting;
                yield return NpcServiceKind.GalduryteIdentification;
                yield return NpcServiceKind.GalduryteSocketing;
                yield return NpcServiceKind.GalduryteExtraction;
            }

        }

        private NpcServiceKind GetDefaultService()
        {
            foreach (var service in GetAvailableServices()) return service;
            return NpcServiceKind.EquipmentCrafting;
        }

        private bool IsServiceUnlocked(NpcServiceKind kind) => kind != NpcServiceKind.GalduryteCrafting
            && kind != NpcServiceKind.GalduryteIdentification && kind != NpcServiceKind.GalduryteSocketing
            && kind != NpcServiceKind.GalduryteExtraction || GalduryteServiceAccess.IsUnlocked(session);

        private void SelectService(NpcServiceKind kind)
        {
            if (!IsServiceUnlocked(kind)) return;
            HideSelectedService();
            selectedService = kind;

            var didShow = kind switch
            {
                NpcServiceKind.CompanionRecovery => companionRecoveryService != null && companionRecoveryService.Show(npc, recipientCharacterId),
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
                NpcServiceKind.JewelryCrafting => jewelryCraftingService != null
                    && jewelryCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.GalduryteCrafting => galduryteCraftingService != null
                    && galduryteCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.GalduryteIdentification => galduryteIdentificationService != null
                    && galduryteIdentificationService.Show(npc, recipientCharacterId),
                NpcServiceKind.GalduryteSocketing => galduryteSocketingService != null
                    && galduryteSocketingService.Show(npc, recipientCharacterId),
                NpcServiceKind.GalduryteExtraction => galduryteExtractionService != null
                    && galduryteExtractionService.Show(npc, recipientCharacterId),
                NpcServiceKind.PotionCrafting => potionCraftingService != null
                    && potionCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.BombCrafting => bombCraftingService != null
                    && bombCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.PotionRefill => potionRefillService != null
                    && potionRefillService.Show(npc, recipientCharacterId),
                NpcServiceKind.BombRefill => bombRefillService != null
                    && bombRefillService.Show(npc, recipientCharacterId),
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
                case NpcServiceKind.CompanionRecovery: companionRecoveryService?.Hide(); break;
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
                case NpcServiceKind.JewelryCrafting:
                    jewelryCraftingService?.Hide();
                    break;
                case NpcServiceKind.GalduryteCrafting:
                    galduryteCraftingService?.Hide();
                    break;
                case NpcServiceKind.GalduryteIdentification:
                    galduryteIdentificationService?.Hide();
                    break;
                case NpcServiceKind.GalduryteSocketing:
                    galduryteSocketingService?.Hide();
                    break;
                case NpcServiceKind.GalduryteExtraction:
                    galduryteExtractionService?.Hide();
                    break;
                case NpcServiceKind.PotionCrafting:
                    potionCraftingService?.Hide();
                    break;
                case NpcServiceKind.BombCrafting:
                    bombCraftingService?.Hide();
                    break;
                case NpcServiceKind.PotionRefill:
                    potionRefillService?.Hide();
                    break;
                case NpcServiceKind.BombRefill:
                    bombRefillService?.Hide();
                    break;
            }
        }

        private void RefreshTabSelection()
        {
            for (var index = 0; index < spawnedTabs.Count; index++)
            {
                var kind = GetAvailableServiceAt(index);
                spawnedTabs[index].Bind(
                    GetTabDefinition(kind)?.Icon,
                    GetServiceDisplayName(kind) + (IsServiceUnlocked(kind) ? string.Empty : " — " + GalduryteServiceAccess.LockedMessage),
                    selectedService == kind,
                    () => SelectService(kind));
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

        private static string GetServiceDisplayName(NpcServiceKind kind)
        {
            return kind switch
            {
                NpcServiceKind.CompanionRecovery => "Odzyskiwanie towarzyszy",
                NpcServiceKind.Trade => "Handel",
                NpcServiceKind.EquipmentCrafting => "Wytwarzanie wyposażenia",
                NpcServiceKind.MaterialCrafting => "Wytwarzanie materiałów",
                NpcServiceKind.EquipmentUpgrade => "Ulepszanie wyposażenia",
                NpcServiceKind.EquipmentReforge => "Przekuwanie wyposażenia",
                NpcServiceKind.EquipmentInfusion => "Infuzja wyposażenia",
                NpcServiceKind.JewelryCrafting => "Tworzenie biżuterii",
                NpcServiceKind.GalduryteCrafting => "Wytwarzanie galdurytów",
                NpcServiceKind.GalduryteIdentification => "Identyfikacja galdurytów",
                NpcServiceKind.GalduryteSocketing => "Osadzanie galdurytów",
                NpcServiceKind.GalduryteExtraction => "Wyciąganie galdurytów",
                NpcServiceKind.PotionCrafting => "Warzenie mikstur",
                NpcServiceKind.PotionRefill => "Napełnianie mikstur",
                NpcServiceKind.BombCrafting => "Wytwarzanie bomb",
                NpcServiceKind.BombRefill => "Napełnianie bomb",
                _ => kind.ToString()
            };
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



        private void UpdateLoyaltyHeader()
        {
            if (loyaltyHeader == null || npc == null) return;
            var isPriest = npc.Definition?.GetRole<PriestRoleData>() != null;
            loyaltyHeader.gameObject.SetActive(!isPriest);
            if (isPriest) return;

            var currentLevel = npc.LoyaltyLevel;
            var spent = loyaltyService.GetGoldSpentTowardNextLevel(npc);
            var required = loyaltyService.GetGoldRequiredForNextLevel(npc);
            loyaltyHeader.Bind(npc, BuildLoyaltyTooltipData(npc));
            loyaltyHeader.SetProgress(
                currentLevel,
                spent,
                required,
                currentLevel >= loyaltyService.MaximumLevel);
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

}
