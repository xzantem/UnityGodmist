using System;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;

namespace Godmist.Systems.NPC
{
    public class NpcInstanceFactory
    {
        private readonly ItemInstanceFactory itemInstanceFactory;
        private readonly InventoryService inventoryService;
        private readonly NpcLoyaltyService npcLoyaltyService;
        private readonly NpcEconomyService npcEconomyService;

        public NpcInstanceFactory(
            ItemInstanceFactory itemInstanceFactory = null,
            InventoryService inventoryService = null)
        {
            this.itemInstanceFactory = itemInstanceFactory ?? new ItemInstanceFactory();
            this.inventoryService = inventoryService ?? new InventoryService();
            npcLoyaltyService = new NpcLoyaltyService();
            npcEconomyService = new NpcEconomyService();
        }

        public NpcInstance Create(
            NpcDefinition definition,
            int campaignLevel = 1,
            GameContentCatalog catalog = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException("NpcDefinition musi miec niepuste Id.", nameof(definition));
            }

            var npc = new NpcInstance
            {
                NpcId = definition.Id,
                Definition = definition,
                LoyaltyLevel = npcLoyaltyService.InitialLevel,
                SpentGoldTotal = 0,
                GoldBalance = 0,
                TotalGoldReceived = 0,
                TotalGoldSpent = 0,
                GuildPayoutProgress = 0f
            };

            npc.GoldBalance = npcEconomyService.GetInitialGold(
                npc,
                campaignLevel);

            CreateShopInventory(npc);
            new TraderStockService().Refresh(npc, campaignLevel,
                rotate: false, catalog: catalog);

            return npc;
        }

        private void CreateShopInventory(NpcInstance npc)
        {
            var traderRole = npc.Definition.GetRole<TraderRoleData>();

            if (traderRole == null) return;

            npc.ShopInventory = new ShopInventory();

            foreach (var itemDefinition in traderRole.InitialOffer)
            {
                if (itemDefinition == null) continue;
                // Equipment offers are authored by the crafted-stock generator,
                // not by instantiating a free starter or an empty base definition.
                if (npc.Definition.GetRole<Godmist.Data.NPC.BlacksmithRoleData>() != null
                    && itemDefinition is Godmist.Data.Items.EquipmentItemDefinition) continue;

                var item = itemInstanceFactory.Create(itemDefinition);
                inventoryService.TryAddItem(npc.ShopInventory, item);
            }
        }
    }
}
