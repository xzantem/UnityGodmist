using System;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;

namespace Godmist.Systems.NPC
{
    public class NpcInstanceFactory
    {
        private readonly ItemInstanceFactory itemInstanceFactory;
        private readonly InventoryService inventoryService;

        public NpcInstanceFactory(
            ItemInstanceFactory itemInstanceFactory = null,
            InventoryService inventoryService = null)
        {
            this.itemInstanceFactory = itemInstanceFactory ?? new ItemInstanceFactory();
            this.inventoryService = inventoryService ?? new InventoryService();
        }

        public NpcInstance Create(NpcDefinition definition)
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
                LoyaltyLevel = 1,
                SpentGoldTotal = 0
            };

            CreateShopInventory(npc);

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

                var item = itemInstanceFactory.Create(itemDefinition);
                inventoryService.TryAddItem(npc.ShopInventory, item);
            }
        }
    }
}