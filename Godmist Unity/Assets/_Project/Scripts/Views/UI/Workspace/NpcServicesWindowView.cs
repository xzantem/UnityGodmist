using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;

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
        private GameSession session;
        private NpcInstance npc;
        private string recipientCharacterId = string.Empty;
        private NpcServiceKind? selectedService;
        private bool isServiceSessionOpen;

        private void Awake()
        {
            if (tabTemplate != null) tabTemplate.gameObject.SetActive(false);
            HideInHierarchyTemplates();

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

        private void HideInHierarchyTemplates()
        {
            // The shared service prefab contains legacy slot/card templates for
            // the other tabs. They are valid Instantiate sources, but must not
            // render as ordinary children of the shell. External prefab assets
            // assigned to serialized fields are not returned by this query and
            // remain available as templates.
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform || child == tabTemplate?.transform)
                    continue;

                var name = child.name;
                if (name.IndexOf("Template", StringComparison.OrdinalIgnoreCase) >= 0
                    && !string.Equals(name, "Templates", StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            if (!isServiceSessionOpen) return;
            EndServiceSession();
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
            if (titleText != null) titleText.text = $"Usługi: {npc.Definition.DisplayName}";
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

    }
}
