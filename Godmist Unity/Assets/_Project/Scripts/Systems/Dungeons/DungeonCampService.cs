using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public sealed class DungeonCampService
    {
        public const string KitId = "CampingKit";
        public const float RecoveryFraction = .6f;
        public static bool IsCampingKit(ItemInstance item) => item?.Definition?.Id == KitId;

        public bool TryIssueSupplies(GameSession session, GameContentCatalog catalog)
        {
            var dungeon = session?.ActiveDungeon;
            if (dungeon == null || dungeon.CampSuppliesIssued || dungeon.Floors.Count == 0
                || catalog == null || !catalog.TryGetItem(KitId, out var definition)
                || !session.CharactersById.TryGetValue(session.PlayerParty.ActiveLeaderId, out var leader)) return false;
            // Expedition supplies are weightless. Validate the content before committing the entire grant.
            if (definition.Weight != 0f) return false;
            for (var i = 0; i < dungeon.CampingKitAllowance; i++)
                leader.Inventory.Items.Add(new ItemInstance(definition));
            dungeon.MarkCampSuppliesIssued();
            return true;
        }

        public void RemoveSupplies(GameSession session)
        {
            if (session == null) return;
            foreach (var character in session.CharactersById.Values) RemoveFrom(character.Inventory.Items);
            RemoveFrom(session.Stash.Items);
            foreach (var npc in session.NpcsById.Values)
            {
                if (npc.ShopInventory == null) continue;
                RemoveFrom(npc.ShopInventory.Items);
                npc.ShopInventory.BuybackStock.RemoveAll(entry => IsCampingKit(entry.Item));
            }
            // Returning to the same instance must not replenish supplies, including legacy instances.
            session.ActiveDungeon?.MarkCampSuppliesIssued();
        }

        private static void RemoveFrom(System.Collections.Generic.List<ItemInstance> items)
        {
            if (items == null) return;
            foreach (var bag in items.OfType<LootBagItemInstance>()) RemoveFrom(bag.Contents);
            items.RemoveAll(IsCampingKit);
        }

        public bool CanCamp(GameSession session, DungeonFloorInstance floor, out string reason)
        {
            reason = string.Empty;
            if (session == null || session.IsDefeated || session.CurrentMode != GameModeState.Exploration
                || session.ActiveDungeon == null || !session.ActiveDungeon.TryGetCurrentFloor(out var current) || current != floor)
                reason = "Obóz można rozbić tylko podczas eksploracji lochu.";
            else if (session.HasCombat) reason = "Nie można rozbić obozu podczas walki.";
            else
            {
                var living = session.PlayerParty.ActiveMemberIds.Select(id => session.CharactersById[id])
                    .Where(c => c.LifeState == CharacterLifeState.Alive).ToArray();
                if (living.Length == 0) reason = "Brak żywych członków drużyny.";
                else if (!living.Any(c => c.Inventory.Items.Any(IsCampingKit)))
                    reason = "Brak zestawów obozowych.";
                else if (living.Any(c => !session.PlayerParty.AreGrouped(c.CharacterId, session.PlayerParty.ActiveLeaderId)))
                    reason = "Przed odpoczynkiem zbierz drużynę w jednej grupie.";
            }
            return reason.Length == 0;
        }

        public bool TryCamp(GameSession session, DungeonFloorInstance floor, out string reason,
            string carrierId = null, string kitInstanceId = null)
        {
            if (!CanCamp(session, floor, out reason)) return false;
            var carrier = session.PlayerParty.ActiveMemberIds.Select(id => session.CharactersById[id])
                .FirstOrDefault(c => c.LifeState == CharacterLifeState.Alive
                    && (carrierId == null || c.CharacterId == carrierId)
                    && c.Inventory.Items.Any(i => IsCampingKit(i) && (kitInstanceId == null || i.InstanceId == kitInstanceId)));
            if (carrier == null) { reason = "Zestaw musi znajdować się u żywego członka aktywnej drużyny."; return false; }
            var kit = carrier.Inventory.Items.First(i => IsCampingKit(i) && (kitInstanceId == null || i.InstanceId == kitInstanceId));
            if (!new InventoryService().TryRemoveItem(carrier.Inventory, kit.InstanceId, out _)) return false;
            foreach (var id in session.PlayerParty.ActiveMemberIds)
            {
                var character = session.CharactersById[id];
                if (character.LifeState != CharacterLifeState.Alive) continue;
                var maxHealth = Mathf.Max(1, Mathf.RoundToInt(character.Stats?.MaxHealth.GetValue() ?? character.CurrentHealth));
                character.CurrentHealth = Mathf.Min(maxHealth,
                    character.CurrentHealth + Mathf.RoundToInt(maxHealth * RecoveryFraction));
                if (character.ResourcePool?.ResourceType == ResourceType.Mana)
                    character.ResourcePool.Restore(Mathf.RoundToInt(character.ResourcePool.MaxValue * RecoveryFraction));
            }
            return true;
        }
    }
}
