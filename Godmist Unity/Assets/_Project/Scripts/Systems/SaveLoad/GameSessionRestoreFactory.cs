using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using UnityEngine;

namespace Godmist.Systems.SaveLoad
{
    public class GameSessionRestoreFactory
    {
        private static float SafeEquipmentCarry(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, -1, 0);

        private readonly CharacterInstanceFactory characterFactory;
        private readonly ItemInstanceFactory itemFactory;
        private readonly NpcInstanceFactory npcFactory;
        private readonly NpcLoyaltyService npcLoyaltyService;
        private readonly InventoryService inventoryService;
        private readonly CharacterStatPipelineSystem statPipeline;
        private readonly DungeonGeneratorService dungeonGenerator;
        private readonly DungeonEncounterPlacementSystem encounterPlacementSystem;
        private readonly DungeonObjectPlacementSystem objectPlacementSystem;
        private readonly DungeonEncounterRewardPreparationSystem rewardPreparationSystem;

        public GameSessionRestoreFactory(
            CharacterInstanceFactory characterFactory = null,
            ItemInstanceFactory itemFactory = null,
            NpcInstanceFactory npcFactory = null,
            InventoryService inventoryService = null,
            CharacterStatPipelineSystem statPipeline = null,
            DungeonGeneratorService dungeonGenerator = null,
            DungeonEncounterPlacementSystem encounterPlacementSystem = null,
            DungeonObjectPlacementSystem objectPlacementSystem = null,
            DungeonEncounterRewardPreparationSystem rewardPreparationSystem = null)
        {
            this.characterFactory = characterFactory
                ?? new CharacterInstanceFactory();

            this.itemFactory = itemFactory
                ?? new ItemInstanceFactory();

            this.inventoryService = inventoryService
                ?? new InventoryService();

            this.npcFactory = npcFactory
                ?? new NpcInstanceFactory(
                    this.itemFactory,
                    this.inventoryService);

            npcLoyaltyService = new NpcLoyaltyService();

            this.statPipeline = statPipeline
                ?? new CharacterStatPipelineSystem();

            this.dungeonGenerator = dungeonGenerator
                ?? new DungeonGeneratorService();

            this.encounterPlacementSystem = encounterPlacementSystem
                ?? new DungeonEncounterPlacementSystem();

            this.objectPlacementSystem = objectPlacementSystem
                ?? new DungeonObjectPlacementSystem();

            this.rewardPreparationSystem = rewardPreparationSystem
                ?? new DungeonEncounterRewardPreparationSystem();
        }

        public GameSession Create(
            GameSaveData saveData,
            GameContentCatalog contentCatalog)
        {
            if (!TryCreate(saveData, contentCatalog, out var session, out var failure))
            {
                throw new InvalidOperationException(
                    failure.ToString());
            }

            return session;
        }

        public bool TryCreate(GameSaveData saveData, GameContentCatalog contentCatalog, out GameSession session)
        {
            var success = TryCreate(saveData, contentCatalog, out session, out var failure);
            if (!success) Debug.LogError(failure.ToString());
            return success;
        }

        public bool TryCreate(GameSaveData saveData, GameContentCatalog contentCatalog,
            out GameSession session, out SaveRestoreFailure failure)
        {
            var context = new RestoreContext();
            bool success;
            try { success = TryCreateCore(saveData, contentCatalog, out session, context); }
            catch (Exception exception)
            {
                session = null;
                success = context.Exception(exception);
            }
            if (!success)
            {
                session = null;
                context.Reject(SaveRestoreErrorCode.ContractRejected, "", "Fabryka odrzuciła zapis.");
            }
            failure = success ? null : context.Failure;
            return success;
        }

        private bool TryCreateCore(
            GameSaveData saveData,
            GameContentCatalog contentCatalog,
            out GameSession session, RestoreContext context)
        {
            session = null;

            if (saveData == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "", "Brak danych zapisu.");
            if (contentCatalog == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "ContentCatalog", "Brak katalogu contentu.");
            if (saveData.SaveVersion != GameSaveData.CurrentSaveVersion)
                return context.Reject(SaveRestoreErrorCode.UnsupportedVersion, "SaveVersion", "Nieobsługiwana wersja zapisu.");
            if (saveData.Party == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "Party", "Brak danych drużyny.");
            if (saveData.Party.MaxActiveMembers != Party.DefaultMaxActiveMembers)
                return context.Field("Party").Reject(SaveRestoreErrorCode.InvalidValue, "MaxActiveMembers", "Niezgodny limit aktywnej drużyny.");
            if (saveData.Gold < 0)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "Gold", "Złoto nie może być ujemne.");

            if (!TryCreateItems(
                    saveData.ItemInstances,
                    contentCatalog,
                    out var itemsById, context.Field("ItemInstances", "Items")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "ItemInstances", "Nie spełniono warunku danych: !TryCreateItems( saveData.ItemInstances, contentCatalog, out var itemsById, context.Field(\"ItemInstances\", \"Items\"))");

            var restoredSession = new GameSession(
                GameModeState.Exploration, saveData.CampaignId);

            if (saveData.HasExplorationPosition && !float.IsFinite(saveData.ExplorationPositionX))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "ExplorationPositionX", "Współrzędna musi być skończona.");
            if (saveData.HasExplorationPosition && !float.IsFinite(saveData.ExplorationPositionY))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "ExplorationPositionY", "Współrzędna musi być skończona.");
            if (saveData.HasExplorationPosition
                && !restoredSession.TrySetExplorationPosition(
                    new Vector2(
                        saveData.ExplorationPositionX,
                        saveData.ExplorationPositionY)))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "HasExplorationPosition", "Nie spełniono warunku danych: saveData.HasExplorationPosition && !restoredSession.TrySetExplorationPosition( new Vector2( saveData.ExplorationPositionX, saveData.ExplorationPositionY))");

            var assignedItemIds = new HashSet<string>(
                StringComparer.Ordinal);

            if (!TryRestoreLootBagContents(
                    saveData.ItemInstances,
                    itemsById,
                    assignedItemIds, context.Field("ItemInstances", "LootBags")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "ItemInstances", "Nie spełniono warunku danych: !TryRestoreLootBagContents( saveData.ItemInstances, itemsById, assignedItemIds, context.Field(\"ItemInstances\", \"LootBags\"))");

            if (!TryRestoreSocketedGaldurytes(
                    saveData.ItemInstances,
                    itemsById,
                    assignedItemIds, context.Field("ItemInstances", "Sockets")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "ItemInstances", "Nie spełniono warunku danych: !TryRestoreSocketedGaldurytes( saveData.ItemInstances, itemsById, assignedItemIds, context.Field(\"ItemInstances\", \"Sockets\"))");

            if (!TryRestoreCharacters(
                    restoredSession,
                    saveData.Characters,
                    contentCatalog,
                    itemsById,
                    assignedItemIds, context.Field("Characters", "Characters")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "Characters", "Nie spełniono warunku danych: !TryRestoreCharacters( restoredSession, saveData.Characters, contentCatalog, itemsById, assignedItemIds, context.Field(\"Characters\", \"Characters\"))");

            if (!TryRestoreInventory(
                    restoredSession.Stash,
                    saveData.StashItemInstanceIds,
                    itemsById,
                    assignedItemIds, context.Field("StashItemInstanceIds", "Stash")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "Stash", "Nie spełniono warunku danych: !TryRestoreInventory( restoredSession.Stash, saveData.StashItemInstanceIds, itemsById, assignedItemIds, context.Field(\"StashItemInstanceIds\", \"Stash\"))");

            if (!TryRestoreParty(
                    restoredSession,
                    saveData.Party, context.Field("Party", "Party")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "Party", "Nie spełniono warunku danych: !TryRestoreParty( restoredSession, saveData.Party, context.Field(\"Party\", \"Party\"))");

            if (saveData.Gold > 0
                && !restoredSession.TryAddGold(saveData.Gold))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "Gold", "Nie spełniono warunku danych: saveData.Gold > 0 && !restoredSession.TryAddGold(saveData.Gold)");

            if (!TryRestoreWorldFlags(
                    restoredSession,
                    saveData.WorldFlags, context.Field("WorldFlags", "WorldFlags")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "WorldFlags", "Nie spełniono warunku danych: !TryRestoreWorldFlags( restoredSession, saveData.WorldFlags, context.Field(\"WorldFlags\", \"WorldFlags\"))");

            if (!TryRestoreQuests(
                    restoredSession,
                    saveData.Quests,
                    contentCatalog,
                    itemsById,
                    assignedItemIds, saveData.DungeonOffers?.CompletedExpeditions ?? 0, context.Field("Quests", "Quests")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "Quests", "Nie spełniono warunku danych: !TryRestoreQuests( restoredSession, saveData.Quests, contentCatalog, itemsById, assignedItemIds, saveData.DungeonOffers?.CompletedExpeditions ?? 0, context.Field(\"Quests\", \"Quests\"))");

            if (!TryRestoreNpcs(
                    restoredSession,
                    saveData.Npcs,
                    contentCatalog,
                    itemsById,
                    assignedItemIds, context.Field("Npcs", "Npcs")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "Npcs", "Nie spełniono warunku danych: !TryRestoreNpcs( restoredSession, saveData.Npcs, contentCatalog, itemsById, assignedItemIds, context.Field(\"Npcs\", \"Npcs\"))");

            if (!TryRestorePendingLoot(
                    restoredSession,
                    saveData.PendingLootItemInstanceIds,
                    itemsById,
                    assignedItemIds, context.Field("PendingLootItemInstanceIds", "PendingLoot")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "PendingLootItemInstanceIds", "Nie spełniono warunku danych: !TryRestorePendingLoot( restoredSession, saveData.PendingLootItemInstanceIds, itemsById, assignedItemIds, context.Field(\"PendingLootItemInstanceIds\", \"PendingLoot\"))");

            if (!DungeonOfferPersistence.Restore(saveData.DungeonOffers, restoredSession.DungeonOffers,
                    context.Field("DungeonOffers", "DungeonOffers"))) return false;


            if (!TryRestoreActiveDungeon(
                    restoredSession,
                    saveData.ActiveDungeon,
                    contentCatalog,
                    itemsById,
                    assignedItemIds, context.Field("ActiveDungeon", "Dungeon")))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "ActiveDungeon", "Nie spełniono warunku danych: !TryRestoreActiveDungeon( restoredSession, saveData.ActiveDungeon, contentCatalog, itemsById, assignedItemIds, context.Field(\"ActiveDungeon\", \"Dungeon\"))");

            if (!string.IsNullOrWhiteSpace(saveData.TrackedQuestId)
                && !restoredSession.TrySetTrackedQuest(
                    saveData.TrackedQuestId))
            {
                return context.Field("TrackedQuestId", "Quests").Entity(saveData.TrackedQuestId).Reject(SaveRestoreErrorCode.UnknownReference, "", "Nie można ustawić śledzonego zadania.");
            }

            if (assignedItemIds.Count != itemsById.Count)
            {
                var id = itemsById.Keys.FirstOrDefault(candidate => !assignedItemIds.Contains(candidate));
                var index = saveData.ItemInstances?.FindIndex(item => item?.InstanceId == id) ?? -1;
                return context.Field("ItemInstances", "Ownership").Entry(index, id)
                    .Reject(SaveRestoreErrorCode.UnassignedItem, "InstanceId", "Nie przypisano instancji do żadnego właściciela.");
            }

            session = restoredSession;
            return true;
        }

        private static bool TryRestoreLootBagContents(
            List<ItemSaveData> itemSaveDataList,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (itemSaveDataList == null) return true;

            var restoreIndex1 = 0;
            foreach (var itemSaveData in itemSaveDataList)
            {
                var restoreEntry1 = context.Entry(restoreIndex1++, itemSaveData?.InstanceId);
                if (itemSaveData == null || !itemSaveData.IsLootBag)
                {
                    continue;
                }

                if (!itemsById.TryGetValue(
                        itemSaveData.InstanceId,
                        out var item))
                    return restoreEntry1.Reject(SaveRestoreErrorCode.InvalidValue, "InstanceId", "Nie spełniono warunku danych: !itemsById.TryGetValue( itemSaveData.InstanceId, out var item)");
                if (item is not LootBagItemInstance lootBag)
                    return restoreEntry1.Reject(SaveRestoreErrorCode.InvalidValue, "InstanceId", "Nie spełniono warunku danych: item is not LootBagItemInstance lootBag");

                var restoredContents = new List<ItemInstance>();

                var restoreIndex2 = 0;
                foreach (var contentInstanceId in
                         itemSaveData.LootBagContentItemInstanceIds
                         ?? new List<string>())
                {
                    var restoreEntry2 = restoreEntry1.Field("LootBagContentItemInstanceIds").Entry(restoreIndex2++, contentInstanceId);
                    if (!TryTakeItem(
                            contentInstanceId,
                            itemsById,
                            assignedItemIds,
                            out var content, restoreEntry2))
                        return restoreEntry2.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem( contentInstanceId, itemsById, assignedItemIds, out var content, restoreEntry2)");

                    restoredContents.Add(content);
                }

                if (!lootBag.TryRestoreContents(restoredContents))
                    return restoreEntry1.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !lootBag.TryRestoreContents(restoredContents)");
            }

            return true;
        }

        private static bool TryRestoreSocketedGaldurytes(
            List<ItemSaveData> itemSaveDataList,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            var restoreIndex3 = 0;
            foreach (var data in itemSaveDataList ?? new List<ItemSaveData>())
            {
                var restoreEntry3 = context.Entry(restoreIndex3++, data?.InstanceId);
                if (data?.SocketedGalduryteInstanceIds == null
                    || data.SocketedGalduryteInstanceIds.Count == 0) continue;
                if (!itemsById.TryGetValue(data.InstanceId, out var item))
                    return restoreEntry3.Reject(SaveRestoreErrorCode.InvalidValue, "InstanceId", "Nie spełniono warunku danych: !itemsById.TryGetValue(data.InstanceId, out var item)");
                if (item is not EquipmentItemInstance equipment)
                    return restoreEntry3.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: item is not EquipmentItemInstance equipment");

                equipment.SocketedGaldurytes = new List<GalduryteItemInstance>();
                var restoreIndex4 = 0;
                foreach (var id in data.SocketedGalduryteInstanceIds)
                {
                    var restoreEntry4 = restoreEntry3.Field("SocketedGalduryteInstanceIds").Entry(restoreIndex4++, id);
                    if (!TryTakeItem(id, itemsById, assignedItemIds, out var socketed, restoreEntry4))
                        return restoreEntry4.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem(id, itemsById, assignedItemIds, out var socketed, restoreEntry4)");
                    if (socketed is not GalduryteItemInstance galduryte)
                        return restoreEntry4.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: socketed is not GalduryteItemInstance galduryte");
                    if (!galduryte.IsIdentified)
                        return restoreEntry4.Reject(SaveRestoreErrorCode.InvalidValue, "IsIdentified", "Nie spełniono warunku danych: !galduryte.IsIdentified");
                    if (equipment.SocketedGaldurytes.Count >= EquipmentItemInstance.GetSocketCapacity(1f))
                        return restoreEntry4.Reject(SaveRestoreErrorCode.InvalidValue, "SocketedGaldurytes", "Nie spełniono warunku danych: equipment.SocketedGaldurytes.Count >= EquipmentItemInstance.GetSocketCapacity(1f)");
                    equipment.SocketedGaldurytes.Add(galduryte);
                }
            }
            return true;
        }

        private bool TryCreateItems(
            List<ItemSaveData> itemSaveDataList,
            GameContentCatalog contentCatalog,
            out Dictionary<string, ItemInstance> itemsById, RestoreContext context)
        {
            itemsById = new Dictionary<string, ItemInstance>(
                StringComparer.Ordinal);

            if (itemSaveDataList == null)
            {
                return true;
            }

            var restoreIndex5 = 0;
            foreach (var itemSaveData in itemSaveDataList)
            {
                var restoreEntry5 = context.Entry(restoreIndex5++, itemSaveData?.InstanceId);
                if (itemSaveData == null)
                    return restoreEntry5.Reject(SaveRestoreErrorCode.MissingValue, "", "Pusty rekord przedmiotu.");
                if (string.IsNullOrWhiteSpace(itemSaveData.InstanceId))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.MissingValue, "InstanceId", "Brak ID instancji przedmiotu.");
                if (string.IsNullOrWhiteSpace(itemSaveData.ItemDefinitionId))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.MissingValue, "ItemDefinitionId", "Brak ID definicji przedmiotu.");
                if (itemsById.ContainsKey(itemSaveData.InstanceId))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.DuplicateId, "InstanceId", "Powtórzone ID instancji przedmiotu.");
                if (!Enum.IsDefined(typeof(RarityTier), itemSaveData.CurrentRarity))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CurrentRarity", "Nieznana rzadkość.");
                if (!contentCatalog.TryGetItem(itemSaveData.ItemDefinitionId, out var definition))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.UnknownReference, "ItemDefinitionId", "Nie znaleziono definicji przedmiotu: " + itemSaveData.ItemDefinitionId);

                var isEquipmentDefinition =
                    definition is EquipmentItemDefinition;

                var isLootBagDefinition = definition is LootBagItemDefinition;

                var isConsumableDefinition = definition is ConsumableItemDefinition;
                var isGalduryteDefinition = definition is GalduryteItemDefinition;

                if (itemSaveData.IsEquipment != isEquipmentDefinition)
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsEquipment", "Nie spełniono warunku danych: itemSaveData.IsEquipment != isEquipmentDefinition");
                if (itemSaveData.IsLootBag != isLootBagDefinition)
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsLootBag", "Nie spełniono warunku danych: itemSaveData.IsLootBag != isLootBagDefinition");
                if (itemSaveData.IsConsumable != isConsumableDefinition)
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsConsumable", "Nie spełniono warunku danych: itemSaveData.IsConsumable != isConsumableDefinition");
                if (itemSaveData.IsGalduryte != isGalduryteDefinition)
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsGalduryte", "Nie spełniono warunku danych: itemSaveData.IsGalduryte != isGalduryteDefinition");
                if ((itemSaveData.IsEquipment && itemSaveData.IsLootBag))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsEquipment", "Nie spełniono warunku danych: (itemSaveData.IsEquipment && itemSaveData.IsLootBag)");
                if ((itemSaveData.IsConsumable && (itemSaveData.IsEquipment || itemSaveData.IsLootBag || itemSaveData.IsGalduryte)))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsConsumable", "Nie spełniono warunku danych: (itemSaveData.IsConsumable && (itemSaveData.IsEquipment || itemSaveData.IsLootBag || itemSaveData.IsGalduryte))");
                if ((itemSaveData.IsGalduryte && (itemSaveData.IsEquipment || itemSaveData.IsLootBag)))
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsGalduryte", "Nie spełniono warunku danych: (itemSaveData.IsGalduryte && (itemSaveData.IsEquipment || itemSaveData.IsLootBag))");

                if (definition is UniqueEquipmentDefinition
                    && itemSaveData.Quality != EquipmentQuality.Masterpiece)
                    return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "Quality", "Nie spełniono warunku danych: definition is UniqueEquipmentDefinition && itemSaveData.Quality != EquipmentQuality.Masterpiece");

                var item = itemFactory.Create(
                    definition,
                    itemSaveData.InstanceId);

                item.CurrentRarity = itemSaveData.CurrentRarity;
                item.ItemLevel = itemSaveData.ItemLevel;
                item.CustomName = itemSaveData.CustomName ?? string.Empty;
                item.IsSaleProtected = itemSaveData.IsSaleProtected;

                if (item is ConsumableItemInstance consumableItem)
                {
                    if (itemSaveData.CatalystDurationModifier < -20)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystDurationModifier", "Nie spełniono warunku danych: itemSaveData.CatalystDurationModifier < -20");
                    if (itemSaveData.CatalystDurationModifier > 20)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystDurationModifier", "Nie spełniono warunku danych: itemSaveData.CatalystDurationModifier > 20");
                    if (float.IsNaN(itemSaveData.CatalystEffectMultiplier))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystEffectMultiplier", "Nie spełniono warunku danych: float.IsNaN(itemSaveData.CatalystEffectMultiplier)");
                    if (float.IsInfinity(itemSaveData.CatalystEffectMultiplier))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystEffectMultiplier", "Nie spełniono warunku danych: float.IsInfinity(itemSaveData.CatalystEffectMultiplier)");
                    if (itemSaveData.CatalystEffectMultiplier < 0f)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystEffectMultiplier", "Nie spełniono warunku danych: itemSaveData.CatalystEffectMultiplier < 0f");
                    if (itemSaveData.CatalystChargeBonus < 0)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystChargeBonus", "Nie spełniono warunku danych: itemSaveData.CatalystChargeBonus < 0");
                    if (itemSaveData.CatalystChargeBonus > 9)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CatalystChargeBonus", "Nie spełniono warunku danych: itemSaveData.CatalystChargeBonus > 9");

                    consumableItem.CatalystDurationModifier = itemSaveData.CatalystDurationModifier;
                    consumableItem.CatalystEffectMultiplier = itemSaveData.CatalystEffectMultiplier;
                    consumableItem.CatalystChargeBonus = itemSaveData.CatalystChargeBonus;
                    consumableItem.CatalystDisplayName = itemSaveData.CatalystDisplayName ?? string.Empty;
                    consumableItem.CraftedBaseValue = itemSaveData.ConsumableCraftedBaseValue;
                    if (itemSaveData.CurrentCharges < 0)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CurrentCharges", "Nie spełniono warunku danych: itemSaveData.CurrentCharges < 0");
                    if (itemSaveData.CurrentCharges > consumableItem.MaxCharges)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "CurrentCharges", "Nie spełniono warunku danych: itemSaveData.CurrentCharges > consumableItem.MaxCharges");
                    consumableItem.CurrentCharges = itemSaveData.CurrentCharges;
                    if (consumableItem.IsBomb)
                    {
                        if (itemSaveData.BlastRange < ConsumableItemInstance.DefaultBombRange)
                            return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "BlastRange", "Nie spełniono warunku danych: itemSaveData.BlastRange < ConsumableItemInstance.DefaultBombRange");
                        if (itemSaveData.BlastRange > ConsumableItemInstance.MaxBombRange)
                            return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "BlastRange", "Nie spełniono warunku danych: itemSaveData.BlastRange > ConsumableItemInstance.MaxBombRange");
                        if (itemSaveData.BlastAreaSize < ConsumableItemInstance.DefaultBombAreaSize)
                            return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "BlastAreaSize", "Nie spełniono warunku danych: itemSaveData.BlastAreaSize < ConsumableItemInstance.DefaultBombAreaSize");
                        if (itemSaveData.BlastAreaSize > ConsumableItemInstance.MaxBombAreaSize)
                            return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "BlastAreaSize", "Nie spełniono warunku danych: itemSaveData.BlastAreaSize > ConsumableItemInstance.MaxBombAreaSize");

                        consumableItem.ConfigureBombTargeting(
                            itemSaveData.BlastRange,
                            itemSaveData.BlastAreaSize);
                    }
                    consumableItem.RuntimeEffects = new List<ConsumableEffectRuntime>();
                    var restoreIndex6 = 0;
                    foreach (var effect in itemSaveData.ConsumableEffects ?? new List<ConsumableEffectSaveData>())
                    {
                        var restoreEntry6 = restoreEntry5.Field("ConsumableEffects").Entry(restoreIndex6++, null);
                        if (effect == null)
                            return restoreEntry6.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: effect == null");
                        if (!Enum.IsDefined(typeof(ConsumableRuntimeEffectType), effect.EffectType))
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "EffectType", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(ConsumableRuntimeEffectType), effect.EffectType)");
                        if (float.IsNaN(effect.Value))
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "Value", "Nie spełniono warunku danych: float.IsNaN(effect.Value)");
                        if (float.IsInfinity(effect.Value))
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "Value", "Nie spełniono warunku danych: float.IsInfinity(effect.Value)");
                        if (float.IsNaN(effect.Chance))
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "Chance", "Nie spełniono warunku danych: float.IsNaN(effect.Chance)");
                        if (float.IsInfinity(effect.Chance))
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "Chance", "Nie spełniono warunku danych: float.IsInfinity(effect.Chance)");
                        if (effect.Chance < 0f)
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "Chance", "Nie spełniono warunku danych: effect.Chance < 0f");
                        if (effect.Chance > 1f)
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "Chance", "Nie spełniono warunku danych: effect.Chance > 1f");
                        if (effect.DurationTurns < 0)
                            return restoreEntry6.Reject(SaveRestoreErrorCode.InvalidValue, "DurationTurns", "Nie spełniono warunku danych: effect.DurationTurns < 0");
                        consumableItem.RuntimeEffects.Add(new ConsumableEffectRuntime
                        {
                            EffectType = effect.EffectType,
                            Value = effect.Value,
                            DurationTurns = effect.DurationTurns,
                            Chance = effect.Chance,
                            TargetModeIndex = effect.TargetModeIndex,
                            StatusId = effect.StatusId ?? string.Empty,
                            ResistanceType = effect.ResistanceType,
                            TargetType = effect.TargetType,
                            TargetingType = effect.TargetingType
                        });
                    }
                }

                if (item is GalduryteItemInstance galduryteItem)
                {
                    if (!Enum.IsDefined(typeof(GalduryteTargetType), itemSaveData.GalduryteTargetType))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GalduryteTargetType", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(GalduryteTargetType), itemSaveData.GalduryteTargetType)");
                    if (!Enum.IsDefined(typeof(GalduryteTier), itemSaveData.GalduryteTier))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GalduryteTier", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(GalduryteTier), itemSaveData.GalduryteTier)");
                    if (!Enum.IsDefined(typeof(GaldurytePowderColor), itemSaveData.GaldurytePowderColor))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GaldurytePowderColor", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(GaldurytePowderColor), itemSaveData.GaldurytePowderColor)");
                    if (itemSaveData.GalduryteCraftedBaseValue < 0)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GalduryteCraftedBaseValue", "Nie spełniono warunku danych: itemSaveData.GalduryteCraftedBaseValue < 0");
                    if (string.IsNullOrWhiteSpace(itemSaveData.GalduryteStoneDefinitionId))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GalduryteStoneDefinitionId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(itemSaveData.GalduryteStoneDefinitionId)");
                    if (string.IsNullOrWhiteSpace(itemSaveData.GaldurytePowderDefinitionId))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GaldurytePowderDefinitionId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(itemSaveData.GaldurytePowderDefinitionId)");
                    if (itemSaveData.GalduryteEffects == null)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.MissingValue, "GalduryteEffects", "Nie spełniono warunku danych: itemSaveData.GalduryteEffects == null");
                    if (itemSaveData.GalduryteEffects.Count == 0)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "GalduryteEffects", "Nie spełniono warunku danych: itemSaveData.GalduryteEffects.Count == 0");
                    if (itemSaveData.ItemLevel != GalduryteRuntimeRules.GetLevel(itemSaveData.GalduryteTier))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "ItemLevel", "Nie spełniono warunku danych: itemSaveData.ItemLevel != GalduryteRuntimeRules.GetLevel(itemSaveData.GalduryteTier)");

                    var effectTypes = new HashSet<GalduryteEffectType>();
                    var effectColors = new HashSet<GaldurytePowderColor>();
                    var restoredEffects = new List<GalduryteEffectInstance>();
                    var restoreIndex7 = 0;
                    foreach (var effect in itemSaveData.GalduryteEffects)
                    {
                        var restoreEntry7 = restoreEntry5.Field("GalduryteEffects").Entry(restoreIndex7++, null);
                        if (effect == null)
                            return restoreEntry7.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: effect == null");
                        if (!Enum.IsDefined(typeof(GalduryteEffectType), effect.EffectType))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "EffectType", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(GalduryteEffectType), effect.EffectType)");
                        if (!Enum.IsDefined(typeof(GaldurytePowderColor), effect.PowderColor))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "PowderColor", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(GaldurytePowderColor), effect.PowderColor)");
                        if (!Enum.IsDefined(typeof(GalduryteEffectGrade), effect.Grade))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "Grade", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(GalduryteEffectGrade), effect.Grade)");
                        if (float.IsNaN(effect.Value))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "Value", "Nie spełniono warunku danych: float.IsNaN(effect.Value)");
                        if (float.IsInfinity(effect.Value))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "Value", "Nie spełniono warunku danych: float.IsInfinity(effect.Value)");
                        if (effect.Value < 0f)
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "Value", "Nie spełniono warunku danych: effect.Value < 0f");
                        if (!effectTypes.Add(effect.EffectType))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "EffectType", "Nie spełniono warunku danych: !effectTypes.Add(effect.EffectType)");
                        if (!effectColors.Add(effect.PowderColor))
                            return restoreEntry7.Reject(SaveRestoreErrorCode.InvalidValue, "PowderColor", "Nie spełniono warunku danych: !effectColors.Add(effect.PowderColor)");

                        restoredEffects.Add(new GalduryteEffectInstance(
                            effect.EffectType,
                            effect.PowderColor,
                            effect.Grade,
                            effect.Value));
                    }

                    galduryteItem.TargetType = itemSaveData.GalduryteTargetType;
                    galduryteItem.Tier = itemSaveData.GalduryteTier;
                    galduryteItem.GuaranteedPowderColor = itemSaveData.GaldurytePowderColor;
                    galduryteItem.IsIdentified = itemSaveData.GalduryteIsIdentified;
                    galduryteItem.IsDestroyed = itemSaveData.GalduryteIsDestroyed;
                    galduryteItem.StoneDefinitionId = itemSaveData.GalduryteStoneDefinitionId ?? string.Empty;
                    galduryteItem.PowderDefinitionId = itemSaveData.GaldurytePowderDefinitionId ?? string.Empty;
                    galduryteItem.StoneDisplayName = itemSaveData.GalduryteStoneDisplayName ?? string.Empty;
                    galduryteItem.CraftedBaseValue = itemSaveData.GalduryteCraftedBaseValue;
                    galduryteItem.Effects = restoredEffects;
                }

                if (item is EquipmentItemInstance equipmentItem)
                {
                    var partIds = itemSaveData.PartIds ?? new List<string>();

                    if (itemSaveData.RequiredLevel < 0)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "RequiredLevel", "Nie spełniono warunku danych: itemSaveData.RequiredLevel < 0");
                    if (float.IsNaN(itemSaveData.UpgradeProgress))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "UpgradeProgress", "Nie spełniono warunku danych: float.IsNaN(itemSaveData.UpgradeProgress)");
                    if (float.IsInfinity(itemSaveData.UpgradeProgress))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "UpgradeProgress", "Nie spełniono warunku danych: float.IsInfinity(itemSaveData.UpgradeProgress)");
                    if (itemSaveData.UpgradeProgress < 0f)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "UpgradeProgress", "Nie spełniono warunku danych: itemSaveData.UpgradeProgress < 0f");
                    if (itemSaveData.UpgradeProgress > 1f)
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "UpgradeProgress", "Nie spełniono warunku danych: itemSaveData.UpgradeProgress > 1f");
                    if (!Enum.IsDefined(
                            typeof(EquipmentQuality),
                            itemSaveData.Quality))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "Quality", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(EquipmentQuality), itemSaveData.Quality)");
                    if (partIds.Any(string.IsNullOrWhiteSpace))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.InvalidValue, "IsNullOrWhiteSpace", "Nie spełniono warunku danych: partIds.Any(string.IsNullOrWhiteSpace)");
                    if (!TryCreateStatModifiers(
                            itemSaveData.BaseStatModifiers,
                            out var baseModifiers, restoreEntry5.Field("BaseStatModifiers")))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.ContractRejected, "BaseStatModifiers", "Nie spełniono warunku danych: !TryCreateStatModifiers( itemSaveData.BaseStatModifiers, out var baseModifiers, restoreEntry5.Field(\"BaseStatModifiers\"))");
                    if (!TryCreateStatModifiers(
                            itemSaveData.StatModifiers,
                            out var modifiers, restoreEntry5.Field("StatModifiers")))
                        return restoreEntry5.Reject(SaveRestoreErrorCode.ContractRejected, "StatModifiers", "Nie spełniono warunku danych: !TryCreateStatModifiers( itemSaveData.StatModifiers, out var modifiers, restoreEntry5.Field(\"StatModifiers\"))");

                    equipmentItem.RequiredClassId =
                        itemSaveData.RequiredClassId ?? string.Empty;

                    equipmentItem.RequiredLevel =
                        itemSaveData.RequiredLevel;

                    equipmentItem.Quality = itemSaveData.Quality;
                    equipmentItem.PartIds = new List<string>(
                        partIds);
                    equipmentItem.PrimaryPartDisplayName = itemSaveData.PrimaryPartDisplayName ?? string.Empty;
                    equipmentItem.PrimaryMaterialId = itemSaveData.PrimaryMaterialId ?? string.Empty;
                    equipmentItem.CraftedBaseValue = itemSaveData.EquipmentCraftedBaseValue;
                    equipmentItem.BaseStatModifiers = baseModifiers;
                    equipmentItem.StatModifiers = modifiers;
                    equipmentItem.UpgradeProgress = itemSaveData.UpgradeProgress;
                    LegacySaveMigrations.RecoverEquipmentBaseline(equipmentItem);
                    equipmentItem.RecalculateStatModifiers();

                    if (equipmentItem is JewelryItemInstance jewelryItem)
                    {
                        jewelryItem.MetalId = itemSaveData.JewelryMetalId ?? string.Empty;
                        jewelryItem.MetalDisplayName = itemSaveData.JewelryMetalDisplayName ?? string.Empty;
                        jewelryItem.GemId = itemSaveData.JewelryGemId ?? string.Empty;
                        jewelryItem.GemDisplayName = itemSaveData.JewelryGemDisplayName ?? string.Empty;
                        jewelryItem.CraftedBaseValue = itemSaveData.JewelryCraftedBaseValue;
                    }
                }

                itemsById.Add(item.InstanceId, item);
            }

            return true;
        }

        private bool TryRestoreCharacters(
            GameSession session,
            List<CharacterSaveData> characterSaveDataList,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (characterSaveDataList == null)
            {
                return true;
            }

            var restoreIndex8 = 0;
            foreach (var characterSaveData in characterSaveDataList)
            {
                var restoreEntry8 = context.Entry(restoreIndex8++, characterSaveData?.CharacterId);
                if (!TryCreateCharacter(
                        characterSaveData,
                        contentCatalog,
                        itemsById,
                        assignedItemIds,
                        out var character, restoreEntry8))
                    return restoreEntry8.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryCreateCharacter( characterSaveData, contentCatalog, itemsById, assignedItemIds, out var character, restoreEntry8)");
                if (!session.TryRegisterCharacter(character))
                    return restoreEntry8.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !session.TryRegisterCharacter(character)");
            }

            return true;
        }

        private bool TryCreateCharacter(
            CharacterSaveData saveData,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds,
            out CharacterInstance character, RestoreContext context)
        {
            character = null;

            if (saveData == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "", "Pusty rekord postaci.");
            if (string.IsNullOrWhiteSpace(saveData.CharacterId))
                return context.Reject(SaveRestoreErrorCode.MissingValue, "CharacterId", "Brak ID postaci.");
            if (string.IsNullOrWhiteSpace(saveData.ClassId))
                return context.Reject(SaveRestoreErrorCode.MissingValue, "ClassId", "Brak ID klasy.");
            if (saveData.ResourcePool == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "ResourcePool", "Brak puli zasobu.");
            if (saveData.Resistances == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "Resistances", "Brak danych odporności.");
            if (!Enum.IsDefined(typeof(CharacterLifeState), saveData.LifeState))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "LifeState", "Nieznany stan życia.");
            if (!Enum.IsDefined(typeof(CharacterAvailabilityState), saveData.AvailabilityState))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "AvailabilityState", "Nieznany stan dostępności.");
            if (!contentCatalog.TryGetCharacter(saveData.CharacterId, out var definition))
                return context.Reject(SaveRestoreErrorCode.UnknownReference, "CharacterId", "Nie znaleziono definicji postaci.");
            if (!contentCatalog.TryGetCharacterClass(saveData.ClassId, out var classData))
                return context.Reject(SaveRestoreErrorCode.UnknownReference, "ClassId", "Nie znaleziono definicji klasy.");

            if (definition.Id != saveData.CharacterId)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "Id", "Nie spełniono warunku danych: definition.Id != saveData.CharacterId");
            if (classData.Id != saveData.ClassId)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "Id", "Nie spełniono warunku danych: classData.Id != saveData.ClassId");
            if (classData.ResourceType
                    != saveData.ResourcePool.ResourceType)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "ResourceType", "Nie spełniono warunku danych: classData.ResourceType != saveData.ResourcePool.ResourceType");

            var resolvedClass = CharacterInstanceFactory.ResolveClassData(definition, classData);
            var migratedEnemyResource = resolvedClass != classData;
            classData = resolvedClass;
            character = characterFactory.Create(definition, classData);

            character.Level = saveData.Level;
            character.CurrentExperience = saveData.CurrentExperience;
            character.AvailableSkillPoints = saveData.AvailableSkillPoints;
            character.SkillTreeProgress = RestoreSkillTreeProgress(
                saveData.SkillTreeProgress,
                classData);
            LegacySaveMigrations.RecoverSkillPointTotal(character);
            character.CurrentHealth = saveData.CurrentHealth;
            character.EquipmentHealthCarry = SafeEquipmentCarry(saveData.EquipmentHealthCarry);
            character.EquipmentResourceCarry = SafeEquipmentCarry(saveData.EquipmentResourceCarry);
            character.EquipmentGenerationCarry = SafeEquipmentCarry(saveData.EquipmentGenerationCarry);
            character.Honor = saveData.Honor;
            character.WeaponAccuracy = saveData.WeaponAccuracy;
            character.LifeState = saveData.LifeState;
            character.AvailabilityState = saveData.AvailabilityState;

            // Legacy enemy Mana cannot become a free full Rage/Momentum pool.
            // New saves keep their exact resource state; only the old class alias
            // adopts the authored initial profile during migration.
            if (!migratedEnemyResource)
                RestoreResourcePool(character.ResourcePool, saveData.ResourcePool);

            if (saveData.HasPersistentResistances)
            {
                RestoreResistances(
                    character.Resistances,
                    saveData.Resistances);
            }

            if (!TryRestoreShieldLayers(
                    character,
                    saveData.ShieldLayers, context.Field("ShieldLayers"))
                || !TryRestoreSkills(
                    character,
                    saveData.SkillIds,
                    contentCatalog, context.Field("SkillIds"))
                || !TryRestoreSkillLoadout(
                    character,
                    saveData, context)
                || !TryRestorePassiveEffects(
                    character,
                    saveData.PassiveEffects, context.Field("PassiveEffects"))
                || !TryRestoreInventory(
                    character.Inventory,
                    saveData.InventoryItemInstanceIds,
                    itemsById,
                    assignedItemIds, context.Field("InventoryItemInstanceIds"))
                || !TryRestoreEquipment(
                    character.Equipment,
                    saveData.Equipment,
                    itemsById,
                    assignedItemIds, context.Field("Equipment")))
            {
                character = null;
                return false; // The failing child owns the precise path and cause.
            }

            new CharacterLevelGrowthSystem(statPipeline).Apply(
                character,
                grantCurrentHealthGrowth: false);
            return true;
        }

        private static bool TryRestoreSkillLoadout(
            CharacterInstance character,
            CharacterSaveData saveData, RestoreContext context)
        {
            string MigrateSkill(string id) => LegacySaveMigrations.LoadoutSkillId(character.ClassData?.Id, saveData.SkillIds, id);
            var knownSkillIds = GetSkillIds(character.GetKnownSkills());
            var basicAttackSkillId = ResolveRestoredBasicAttackSkillId(
                character,
                saveData,
                knownSkillIds);

            if (string.IsNullOrWhiteSpace(basicAttackSkillId)
                || !knownSkillIds.Contains(basicAttackSkillId))
            {
                return context.Require(!saveData.HasActiveSkillLoadout, "HasActiveSkillLoadout", "Brak prawidłowego ataku podstawowego dla zapisanego loadoutu.");
            }

            if (!saveData.HasActiveSkillLoadout)
            {
                return context.Require(character.ActiveSkillLoadout.TryInitialize(
                    knownSkillIds,
                    basicAttackSkillId), "BasicAttackSkillId", "Nie można zainicjalizować loadoutu.");
            }

            var modifiers = new List<SkillLoadoutCapacityModifier>();
            var restoreIndex9 = 0;
            foreach (var saveDataModifier in saveData.ActiveSkillCapacityModifiers
                         ?? new List<SkillLoadoutCapacityModifierSaveData>())
            {
                var restoreEntry9 = context.Field("ActiveSkillCapacityModifiers").Entry(restoreIndex9++, saveDataModifier?.SourceId);
                if (saveDataModifier == null)
                    return restoreEntry9.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saveDataModifier == null");
                if (string.IsNullOrWhiteSpace(saveDataModifier.SourceId))
                    return restoreEntry9.Reject(SaveRestoreErrorCode.InvalidValue, "SourceId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(saveDataModifier.SourceId)");

                modifiers.Add(new SkillLoadoutCapacityModifier
                {
                    SourceId = saveDataModifier.SourceId,
                    Amount = saveDataModifier.Amount
                });
            }

            return context.Require(saveData.ActiveSkillSlotIds != null
                   && saveData.ActiveSkillSlotIds.Count > 0
                ? character.ActiveSkillLoadout.TryInitializeSlots(
                    knownSkillIds,
                    basicAttackSkillId,
                    saveData.ActiveSkillSlotIds.Select(MigrateSkill),
                    modifiers)
                : character.ActiveSkillLoadout.TryInitialize(
                    knownSkillIds,
                    basicAttackSkillId,
                    saveData.ActiveSkillIds?.Select(MigrateSkill),
                    modifiers), "ActiveSkillSlotIds", "Nieprawidłowy zapis aktywnego loadoutu.");
        }

        private static string ResolveRestoredBasicAttackSkillId(
            CharacterInstance character,
            CharacterSaveData saveData,
            IReadOnlyCollection<string> knownSkillIds)
        {
            var configuredBasicSkillId = character?.Definition?.BasicAttackSkill?.SkillId;
            if (!string.IsNullOrWhiteSpace(configuredBasicSkillId)
                && ContainsKnownSkillId(knownSkillIds, configuredBasicSkillId))
            {
                return configuredBasicSkillId;
            }

            // Active loadouts always store their pinned basic attack at index 0.
            // This preserves both saves made before the ownership correction and
            // saves made by legacy assets using the first starting skill.
            if (saveData?.HasActiveSkillLoadout == true)
            {
                foreach (var skillId in saveData.ActiveSkillIds
                             ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(skillId)
                        && ContainsKnownSkillId(knownSkillIds, skillId))
                    {
                        return skillId;
                    }
                }
            }

            var legacyClassBasicSkillId = character?.ClassData?.BasicAttackSkill?.SkillId;
            if (!string.IsNullOrWhiteSpace(legacyClassBasicSkillId)
                && ContainsKnownSkillId(knownSkillIds, legacyClassBasicSkillId))
            {
                return legacyClassBasicSkillId;
            }

            return character?.GetBasicAttackSkill()?.SkillId ?? string.Empty;
        }

        private static bool ContainsKnownSkillId(
            IReadOnlyCollection<string> knownSkillIds,
            string skillId)
        {
            if (knownSkillIds == null || string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            foreach (var knownSkillId in knownSkillIds)
            {
                if (string.Equals(knownSkillId, skillId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> GetSkillIds(
            IReadOnlyList<SkillData> skills)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var skill in skills ?? new List<SkillData>())
            {
                if (skill != null
                    && !string.IsNullOrWhiteSpace(skill.SkillId)
                    && seen.Add(skill.SkillId))
                {
                    result.Add(skill.SkillId);
                }
            }

            return result;
        }

        private static void RestoreResourcePool(
            ResourcePool resourcePool,
            ResourcePoolSaveData saveData)
        {
            resourcePool.ResourceType = saveData.ResourceType;
            resourcePool.CurrentValue = saveData.CurrentValue;
            resourcePool.MaxValue = saveData.MaxValue;
            resourcePool.Regeneration = saveData.Regeneration;
            resourcePool.HasStartedTurn = saveData.HasStartedTurn;
            resourcePool.HasGainedResourceSinceLastTurn =
                saveData.HasGainedResourceSinceLastTurn;

            resourcePool.FractionalGainRemainder =
                saveData.FractionalGainRemainder;
        }

        private static void RestoreResistances(
            ResistanceSet resistances,
            ResistanceSetSaveData saveData)
        {
            resistances.BleedResistance =
                saveData.BleedResistance;

            resistances.PoisonResistance =
                saveData.PoisonResistance;

            resistances.FireResistance =
                saveData.FireResistance;

            resistances.WeakeningResistance =
                saveData.WeakeningResistance;

            resistances.SuppressionResistance =
                saveData.SuppressionResistance;

            resistances.MoveResistance =
                saveData.MoveResistance;
        }

        private static bool TryRestoreShieldLayers(
            CharacterInstance character,
            List<ShieldLayerSaveData> shieldLayers, RestoreContext context)
        {
            if (character.ShieldPool == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "ShieldPool", "Nie spełniono warunku danych: character.ShieldPool == null");

            if (shieldLayers == null)
            {
                return true;
            }

            var restoreIndex10 = 0;
            foreach (var shieldLayer in shieldLayers)
            {
                var restoreEntry10 = context.Entry(restoreIndex10++, shieldLayer?.SourceId);
                if (shieldLayer == null)
                    return restoreEntry10.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: shieldLayer == null");
                if (shieldLayer.CurrentValue < 0)
                    return restoreEntry10.Reject(SaveRestoreErrorCode.InvalidValue, "CurrentValue", "Nie spełniono warunku danych: shieldLayer.CurrentValue < 0");
                if (shieldLayer.RemainingTurns < 0)
                    return restoreEntry10.Reject(SaveRestoreErrorCode.InvalidValue, "RemainingTurns", "Nie spełniono warunku danych: shieldLayer.RemainingTurns < 0");

                var turns = shieldLayer.RemainingTurns;
                if (string.Equals(shieldLayer.SourceId, CombatShieldSystem.MageShieldSourceId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    // Legacy saves have no duration and may contain stacked casts.
                    // Keep the last surviving cast, with a bounded lifetime.
                    turns = LegacySaveMigrations.MageShieldDuration(turns);
                    character.ShieldPool.RemoveLayersBySourceId(shieldLayer.SourceId);
                }
                character.ShieldPool.AddLayer(
                    shieldLayer.CurrentValue,
                    shieldLayer.SourceId,
                    turns);
            }

            return true;
        }

        private static bool TryRestoreSkills(
            CharacterInstance character,
            List<string> skillIds,
            GameContentCatalog contentCatalog, RestoreContext context)
        {
            character.Skills.Clear();

            var restoredSkillIds = new HashSet<string>(
                StringComparer.Ordinal);

            var restoreIndex11 = 0;
            foreach (var savedSkillId in skillIds ?? new List<string>())
            {
                var restoreEntry11 = context.Entry(restoreIndex11++, savedSkillId);
                var skillId = LegacySaveMigrations.KnownSkillId(character.ClassData?.Id, savedSkillId);
                if (string.IsNullOrWhiteSpace(skillId))
                    return restoreEntry11.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(skillId)");
                if (!restoredSkillIds.Add(skillId))
                    return restoreEntry11.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !restoredSkillIds.Add(skillId)");
                if (!contentCatalog.TryGetSkill(skillId, out var skill))
                    return restoreEntry11.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !contentCatalog.TryGetSkill(skillId, out var skill)");
                character.Skills.Add(skill);
            }

            var basicAttackSkill = character.Definition?.BasicAttackSkill;
            if (basicAttackSkill != null
                && !string.IsNullOrWhiteSpace(basicAttackSkill.SkillId)
                && !character.Skills.Exists(skill => string.Equals(
                    skill?.SkillId,
                    basicAttackSkill.SkillId,
                    StringComparison.Ordinal)))
            {
                character.Skills.Add(basicAttackSkill);
            }

            return true;
        }

        private static bool TryRestorePassiveEffects(
            CharacterInstance character,
            List<PassiveEffectSaveData> passiveEffectSaveDataList, RestoreContext context)
        {
            character.PassiveEffects.Clear();

            if (passiveEffectSaveDataList == null)
            {
                return true;
            }

            var restoreIndex12 = 0;
            foreach (var effectSaveData in passiveEffectSaveDataList)
            {
                var restoreEntry12 = context.Entry(restoreIndex12++, effectSaveData?.EffectId);
                if (effectSaveData == null)
                    return restoreEntry12.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: effectSaveData == null");
                if (string.IsNullOrWhiteSpace(effectSaveData.EffectId))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "EffectId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(effectSaveData.EffectId)");
                if (character.PassiveEffects.Any(existing => existing.EffectId == LegacyContentIdentities.Effect(effectSaveData.EffectId)))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "EffectId", "Duplicate effect identity after legacy migration.");
                if (!IsValidPassiveEffectDefinitionId(effectSaveData.DefinitionId))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "DefinitionId", "Nie spełniono warunku danych: !IsValidPassiveEffectDefinitionId(effectSaveData.DefinitionId)");
                if (effectSaveData.Stacks < 0)
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "Stacks", "Nie spełniono warunku danych: effectSaveData.Stacks < 0");
                if (effectSaveData.RemainingDuration < 0)
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "RemainingDuration", "Nie spełniono warunku danych: effectSaveData.RemainingDuration < 0");
                if (!Enum.IsDefined(
                        typeof(PassiveEffectLifetime),
                        effectSaveData.Lifetime))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "Lifetime", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(PassiveEffectLifetime), effectSaveData.Lifetime)");
                if (!Enum.IsDefined(
                        typeof(PassiveEffectStackingRule),
                        effectSaveData.StackingRule))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.InvalidValue, "StackingRule", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(PassiveEffectStackingRule), effectSaveData.StackingRule)");
                if (!TryCreatePassiveEffectSources(
                        effectSaveData.Sources,
                        out var sources, restoreEntry12.Field("Sources")))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.ContractRejected, "Sources", "Nie spełniono warunku danych: !TryCreatePassiveEffectSources( effectSaveData.Sources, out var sources, restoreEntry12.Field(\"Sources\"))");
                if (!TryCreatePassiveEffectPayloads(
                        effectSaveData.Payloads,
                        out var payloads, restoreEntry12.Field("Payloads")))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.ContractRejected, "Payloads", "Nie spełniono warunku danych: !TryCreatePassiveEffectPayloads( effectSaveData.Payloads, out var payloads, restoreEntry12.Field(\"Payloads\"))");

                var tickPhase = effectSaveData.Lifetime == PassiveEffectLifetime.Timed
                    ? effectSaveData.TickPhase
                    : PassiveEffectTickPhase.None;

                if (effectSaveData.Lifetime == PassiveEffectLifetime.Timed
                    && tickPhase == PassiveEffectTickPhase.None)
                {
                    tickPhase = IsDamageOverTimeStatus(
                        effectSaveData.EffectId)
                        ? PassiveEffectTickPhase.TurnStart
                        : PassiveEffectTickPhase.TurnEnd;
                }

                if (!TryCreatePassiveEffectStateValues(
                        effectSaveData.StateValues,
                        out var stateValues, restoreEntry12.Field("StateValues")))
                    return restoreEntry12.Reject(SaveRestoreErrorCode.ContractRejected, "StateValues", "Nie spełniono warunku danych: !TryCreatePassiveEffectStateValues( effectSaveData.StateValues, out var stateValues, restoreEntry12.Field(\"StateValues\"))");

                character.PassiveEffects.Add(
                    StatusEffectSystem.NormalizeStatus(new PassiveEffectRuntime(
                        LegacyContentIdentities.Effect(effectSaveData.EffectId),
                        sources,
                        effectSaveData.Stacks,
                        effectSaveData.RemainingDuration,
                        totalDamage: 0,
                        lifetime: effectSaveData.Lifetime,
                        tickPhase)
                    {
                        DisplayName = effectSaveData.DisplayName ?? string.Empty,
                        DefinitionId = effectSaveData.DefinitionId ?? string.Empty,
                        StackingRule = effectSaveData.StackingRule,
                        Payloads = payloads,
                        StateValues = stateValues
                    }));
            }

            return true;
        }

        private static bool IsValidPassiveEffectDefinitionId(string definitionId)
        {
            return string.IsNullOrWhiteSpace(definitionId)
                || definitionId.Length <= 256;
        }

        private static bool TryCreatePassiveEffectStateValues(
            List<PassiveEffectRuntimeStateValueSaveData> saveDataList,
            out List<PassiveEffectRuntimeStateValue> stateValues, RestoreContext context)
        {
            stateValues = new List<PassiveEffectRuntimeStateValue>();
            if (saveDataList == null) return true;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var restoreIndex13 = 0;
            foreach (var saveData in saveDataList)
            {
                var restoreEntry13 = context.Entry(restoreIndex13++, saveData?.Key);
                if (saveData == null)
                    return restoreEntry13.Reject(SaveRestoreErrorCode.MissingValue, "", "Brak wartości stanu efektu.");
                if (string.IsNullOrWhiteSpace(saveData.Key) || saveData.Key.Length > 256)
                    return restoreEntry13.Reject(SaveRestoreErrorCode.InvalidValue, "Key", "Klucz musi zawierać od 1 do 256 znaków.");
                if (!Enum.IsDefined(typeof(PassiveEffectRuntimeValueType), saveData.Type))
                    return restoreEntry13.Reject(SaveRestoreErrorCode.InvalidValue, "Type", "Nieobsługiwany typ wartości stanu.");
                if (!keys.Add(saveData.Key))
                    return restoreEntry13.Reject(SaveRestoreErrorCode.DuplicateId, "Key", "Powtórzony klucz wartości stanu.");
                if (!float.IsFinite(saveData.NumberValue))
                    return restoreEntry13.Reject(SaveRestoreErrorCode.InvalidValue, "NumberValue", "Wartość musi być skończona.");

                stateValues.Add(new PassiveEffectRuntimeStateValue
                {
                    Key = saveData.Key,
                    Type = saveData.Type,
                    NumberValue = saveData.NumberValue,
                    IntegerValue = saveData.IntegerValue,
                    TextValue = saveData.TextValue ?? string.Empty,
                    BooleanValue = saveData.BooleanValue
                });
            }

            return true;
        }

        private static bool TryCreatePassiveEffectPayloads(
            List<PassiveEffectPayloadSaveData> saveDataList,
            out List<PassiveEffectPayloadRuntime> payloads, RestoreContext context)
        {
            payloads = new List<PassiveEffectPayloadRuntime>();
            if (saveDataList == null) return true;

            var types = new HashSet<PassiveEffectPayloadType>();
            var restoreIndex14 = 0;
            foreach (var saveData in saveDataList)
            {
                var restoreEntry14 = context.Entry(restoreIndex14++, null);
                if (saveData == null)
                    return restoreEntry14.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saveData == null");
                if (!Enum.IsDefined(typeof(PassiveEffectPayloadType), saveData.Type))
                    return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "Type", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(PassiveEffectPayloadType), saveData.Type)");
                if (!types.Add(saveData.Type))
                    return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "Type", "Nie spełniono warunku danych: !types.Add(saveData.Type)");

                switch (saveData.Type)
                {
                    case PassiveEffectPayloadType.DamageOverTime:
                        if (saveData.RemainingTotalDamage < 0)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "RemainingTotalDamage", "Nie spełniono warunku danych: saveData.RemainingTotalDamage < 0");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.SleepHealing:
                        if (saveData.MaxHealthPercent < 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "MaxHealthPercent", "Nie spełniono warunku danych: saveData.MaxHealthPercent < 0f");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.UniqueCombatBuff:
                        if (!Enum.IsDefined(typeof(UniqueBuffType), saveData.UniqueBuffType))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "UniqueBuffType", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(UniqueBuffType), saveData.UniqueBuffType)");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.ResourceRegenerationBlock:
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.DamageRedirect:
                        if (saveData.DamageRedirectPercent < 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "DamageRedirectPercent", "Nie spełniono warunku danych: saveData.DamageRedirectPercent < 0f");
                        if (saveData.DamageRedirectPercent > 1f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "DamageRedirectPercent", "Nie spełniono warunku danych: saveData.DamageRedirectPercent > 1f");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.OnHitStatus:
                        if (saveData.OnHitStatusChance < 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "OnHitStatusChance", "Nie spełniono warunku danych: saveData.OnHitStatusChance < 0f");
                        if (saveData.OnHitStatusChance > 1f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "OnHitStatusChance", "Nie spełniono warunku danych: saveData.OnHitStatusChance > 1f");
                        if (saveData.OnHitStatusDurationTurns < 0)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "OnHitStatusDurationTurns", "Nie spełniono warunku danych: saveData.OnHitStatusDurationTurns < 0");
                        if (saveData.OnHitStatusDamageMultiplier < 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "OnHitStatusDamageMultiplier", "Nie spełniono warunku danych: saveData.OnHitStatusDamageMultiplier < 0f");
                        if (saveData.OnHitStatusDamagePerTurn < 0)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "OnHitStatusDamagePerTurn", "Nie spełniono warunku danych: saveData.OnHitStatusDamagePerTurn < 0");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.Counterattack:
                        return restoreEntry14.Reject(SaveRestoreErrorCode.ContractRejected, "Type", "Payload kontrataku nie ma obsługi odtwarzania w tym formacie zapisu.");
                    case PassiveEffectPayloadType.StatModifiers:
                        if (!TryCreateStatModifiers(saveData.StatModifiers, out var statModifiers, restoreEntry14.Field("StatModifiers")))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.ContractRejected, "StatModifiers", "Nie spełniono warunku danych: !TryCreateStatModifiers(saveData.StatModifiers, out var statModifiers, restoreEntry14.Field(\"StatModifiers\"))");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.VariableModifiers:
                        if (!TryCreateVariableModifiers(saveData.VariableModifiers, out var variableModifiers, restoreEntry14.Field("VariableModifiers")))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.ContractRejected, "VariableModifiers", "Nie spełniono warunku danych: !TryCreateVariableModifiers(saveData.VariableModifiers, out var variableModifiers, restoreEntry14.Field(\"VariableModifiers\"))");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.ResistanceModifier:
                        if (!Enum.IsDefined(typeof(ResistanceType), saveData.ResistanceType))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResistanceType", "Nie spełniono warunku danych: !Enum.IsDefined(typeof(ResistanceType), saveData.ResistanceType)");
                        if (float.IsNaN(saveData.ResistanceModifierValue))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResistanceModifierValue", "Nie spełniono warunku danych: float.IsNaN(saveData.ResistanceModifierValue)");
                        if (float.IsInfinity(saveData.ResistanceModifierValue))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResistanceModifierValue", "Nie spełniono warunku danych: float.IsInfinity(saveData.ResistanceModifierValue)");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    case PassiveEffectPayloadType.PeriodicRecovery:
                        if (float.IsNaN(saveData.HealthRoundingCarry))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "HealthRoundingCarry", "Nie spełniono warunku danych: float.IsNaN(saveData.HealthRoundingCarry)");
                        if (saveData.HealthRoundingCarry < -1f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "HealthRoundingCarry", "Nie spełniono warunku danych: saveData.HealthRoundingCarry < -1f");
                        if (saveData.HealthRoundingCarry > 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "HealthRoundingCarry", "Nie spełniono warunku danych: saveData.HealthRoundingCarry > 0f");
                        if (float.IsNaN(saveData.ResourceRoundingCarry))
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResourceRoundingCarry", "Nie spełniono warunku danych: float.IsNaN(saveData.ResourceRoundingCarry)");
                        if (saveData.ResourceRoundingCarry < -1f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResourceRoundingCarry", "Nie spełniono warunku danych: saveData.ResourceRoundingCarry < -1f");
                        if (saveData.ResourceRoundingCarry > 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResourceRoundingCarry", "Nie spełniono warunku danych: saveData.ResourceRoundingCarry > 0f");
                        if (saveData.HealthPercent < 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "HealthPercent", "Nie spełniono warunku danych: saveData.HealthPercent < 0f");
                        if (saveData.HealthPercent > 1f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "HealthPercent", "Nie spełniono warunku danych: saveData.HealthPercent > 1f");
                        if (saveData.ResourcePercent < 0f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResourcePercent", "Nie spełniono warunku danych: saveData.ResourcePercent < 0f");
                        if (saveData.ResourcePercent > 1f)
                            return restoreEntry14.Reject(SaveRestoreErrorCode.InvalidValue, "ResourcePercent", "Nie spełniono warunku danych: saveData.ResourcePercent > 1f");
                        payloads.Add(PassiveEffectPayloadCodec.Read(saveData));
                        break;
                    default:
                        return restoreEntry14.Reject(SaveRestoreErrorCode.ContractRejected, "Type", $"Brak kontraktu payloadu: {saveData.Type}.");
                }
            }

            return true;
        }

        private static List<SkillTreeProgress> RestoreSkillTreeProgress(
            List<SkillTreeProgressSaveData> saveData,
            CharacterClassData classData)
        {
            var definitions = GetCanonicalSkillTreeDefinitions(classData);
            var definitionsById = new Dictionary<string, SkillTreeDefinition>(
                StringComparer.Ordinal);
            var nodeDefinitionsByTreeId = new Dictionary<
                string,
                Dictionary<string, SkillTreeNodeDefinition>>(
                StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                definitionsById.Add(definition.Id, definition);

                var nodeDefinitions = new Dictionary<string, SkillTreeNodeDefinition>(
                    StringComparer.Ordinal);
                foreach (var node in GetCanonicalSkillTreeNodes(definition))
                {
                    nodeDefinitions.Add(node.Id, node);
                }

                nodeDefinitionsByTreeId.Add(definition.Id, nodeDefinitions);
            }

            var aggregatedRanksByTreeId = new Dictionary<
                string,
                Dictionary<string, int>>(
                StringComparer.Ordinal);
            foreach (var tree in saveData ?? new())
            {
                if (tree == null
                    || string.IsNullOrWhiteSpace(tree.TreeId)
                    || !definitionsById.ContainsKey(tree.TreeId))
                {
                    continue;
                }

                if (!aggregatedRanksByTreeId.TryGetValue(
                        tree.TreeId,
                        out var aggregatedRanks))
                {
                    aggregatedRanks = new Dictionary<string, int>(
                        StringComparer.Ordinal);
                    aggregatedRanksByTreeId.Add(tree.TreeId, aggregatedRanks);
                }

                foreach (var node in tree.NodeInvestments ?? new())
                {
                    if (node == null
                        || string.IsNullOrWhiteSpace(node.NodeId)
                        || node.Ranks <= 0)
                    {
                        continue;
                    }

                    // Strong Liver was replaced by Efficient Recovery. Canonicalize before
                    // merging so a save containing both IDs cannot spend/count the point twice.
                    var nodeId = tree.TreeId == "tree.scout.blade_dancer"
                        && node.NodeId == "node.scout.blade_dancer.strong_liver"
                        ? "node.scout.blade_dancer.efficient_recovery" : node.NodeId;
                    if (!nodeDefinitionsByTreeId[tree.TreeId].TryGetValue(nodeId, out var nodeDefinition))
                        continue;

                    var ranks = Math.Min(node.Ranks, nodeDefinition.EffectiveMaxRanks);
                    if (!aggregatedRanks.TryGetValue(
                            nodeId,
                            out var existingRanks)
                        || ranks > existingRanks)
                    {
                        aggregatedRanks[nodeId] = ranks;
                    }
                }
            }

            var result = new List<SkillTreeProgress>();
            foreach (var definition in definitions)
            {
                if (!aggregatedRanksByTreeId.TryGetValue(
                        definition.Id,
                        out var aggregatedRanks))
                {
                    continue;
                }

                var progress = new SkillTreeProgress { TreeId = definition.Id };
                var remaining = SkillTreeProgressionSystem.MaximumPointsPerTree;
                foreach (var node in GetCanonicalSkillTreeNodes(definition))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    if (!aggregatedRanks.TryGetValue(node.Id, out var ranks))
                    {
                        continue;
                    }

                    ranks = Math.Min(ranks, remaining);
                    if (ranks <= 0)
                    {
                        continue;
                    }

                    progress.NodeInvestments.Add(
                        new SkillTreeNodeInvestment
                        {
                            NodeId = node.Id,
                            Ranks = ranks
                        });
                    remaining -= ranks;
                }

                if (progress.NodeInvestments.Count > 0)
                {
                    result.Add(progress);
                }
            }

            return result;
        }

        private static List<SkillTreeDefinition> GetCanonicalSkillTreeDefinitions(
            CharacterClassData classData)
        {
            var result = new List<SkillTreeDefinition>();
            var seenTreeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tree in classData?.SkillTrees ?? new())
            {
                if (tree != null
                    && !string.IsNullOrWhiteSpace(tree.Id)
                    && seenTreeIds.Add(tree.Id))
                {
                    result.Add(tree);
                }
            }

            return result;
        }

        private static List<SkillTreeNodeDefinition> GetCanonicalSkillTreeNodes(
            SkillTreeDefinition tree)
        {
            var result = new List<SkillTreeNodeDefinition>();
            var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in tree?.Nodes ?? new())
            {
                if (node != null
                    && !string.IsNullOrWhiteSpace(node.Id)
                    && node.EffectiveMaxRanks > 0
                    && seenNodeIds.Add(node.Id))
                {
                    result.Add(node);
                }
            }

            return result;
        }

        private static bool IsDamageOverTimeStatus(string effectId)
        {
            return string.Equals(effectId, StatusEffectIds.Bleed,
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(effectId, StatusEffectIds.Poison,
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(effectId, StatusEffectIds.Burn,
                       StringComparison.OrdinalIgnoreCase);
        }

        private bool TryRestoreInventory(
            Inventory inventory,
            List<string> itemInstanceIds,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (inventory?.Items == null)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "Items", "Nie spełniono warunku danych: inventory?.Items == null");

            if (itemInstanceIds == null)
            {
                return true;
            }

            var restoreIndex15 = 0;
            foreach (var itemInstanceId in itemInstanceIds)
            {
                var restoreEntry15 = context.Entry(restoreIndex15++, itemInstanceId);
                if (!TryTakeItem(
                        itemInstanceId,
                        itemsById,
                        assignedItemIds,
                        out var item, restoreEntry15))
                    return restoreEntry15.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem( itemInstanceId, itemsById, assignedItemIds, out var item, restoreEntry15)");
                if (!inventoryService.TryAddItem(inventory, item))
                    return restoreEntry15.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !inventoryService.TryAddItem(inventory, item)");
            }

            return true;
        }

        private static bool TryRestoreEquipment(
            EquipmentSet equipment,
            EquipmentSetSaveData saveData,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (equipment == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: equipment == null");

            saveData ??= new EquipmentSetSaveData();

            return TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Weapon,
                       saveData.WeaponItemInstanceId,
                       itemsById,
                       assignedItemIds, context.Field("WeaponItemInstanceId"))
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Armor,
                       saveData.ArmorItemInstanceId,
                       itemsById,
                       assignedItemIds, context.Field("ArmorItemInstanceId"))
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Necklace,
                       saveData.NecklaceItemInstanceId,
                       itemsById,
                       assignedItemIds, context.Field("NecklaceItemInstanceId"))
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Ring,
                       saveData.RingItemInstanceId,
                       itemsById,
                       assignedItemIds, context.Field("RingItemInstanceId"))
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Bracelet,
                       saveData.BraceletItemInstanceId,
                       itemsById,
                       assignedItemIds, context.Field("BraceletItemInstanceId"))
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Earrings,
                       saveData.EarringsItemInstanceId,
                       itemsById,
                       assignedItemIds, context.Field("EarringsItemInstanceId"));
        }

        private static bool TryRestoreEquipmentSlot(
            EquipmentSet equipment,
            EquipmentSlot slot,
            string itemInstanceId,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return true;
            }

            if (!TryTakeItem(
                    itemInstanceId,
                    itemsById,
                    assignedItemIds,
                    out var item, context))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem( itemInstanceId, itemsById, assignedItemIds, out var item, context)");
            if (item is not EquipmentItemInstance equipmentItem)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: item is not EquipmentItemInstance equipmentItem");
            if (equipmentItem.EquipmentSlot != slot)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "EquipmentSlot", "Nie spełniono warunku danych: equipmentItem.EquipmentSlot != slot");

            equipment.SetItem(slot, equipmentItem);
            return true;
        }

        private static bool TryTakeItem(
            string itemInstanceId,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds,
            out ItemInstance item, RestoreContext context)
        {
            item = null;
            context = context.Entity(itemInstanceId);
            if (string.IsNullOrWhiteSpace(itemInstanceId))
                return context.Reject(SaveRestoreErrorCode.MissingValue, "", "Brak ID przedmiotu.");
            if (!itemsById.TryGetValue(itemInstanceId, out item))
                return context.Reject(SaveRestoreErrorCode.UnknownReference, "", "Przedmiot o tym ID nie istnieje w ItemInstances.");
            if (!assignedItemIds.Add(itemInstanceId))
                return context.Reject(SaveRestoreErrorCode.AlreadyAssigned, "", "Ta instancja przedmiotu została już przypisana.");
            return true;
        }

        private static bool TryRestoreParty(
            GameSession session,
            PartySaveData saveData, RestoreContext context)
        {
            if (saveData == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saveData == null");
            if (saveData.MaxActiveMembers
                    != Party.DefaultMaxActiveMembers)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "MaxActiveMembers", "Nie spełniono warunku danych: saveData.MaxActiveMembers != Party.DefaultMaxActiveMembers");

            var restoreIndex16 = 0;
            foreach (var characterId in saveData.ActiveMemberIds)
            {
                var restoreEntry16 = context.Field("ActiveMemberIds").Entry(restoreIndex16++, characterId);
                // Restore must reproduce the saved party composition, including
                // incapacitated or dead members.  The gameplay API deliberately
                // prevents adding such characters to an active party, but using it
                // here made otherwise valid saves unloadable after a party member
                // died.
                if (!session.CharactersById.ContainsKey(characterId))
                    return restoreEntry16.Reject(SaveRestoreErrorCode.UnknownReference, "", "Nie znaleziono postaci o zapisanym ID.");
                if (!session.PlayerParty.TryAddActiveMember(characterId))
                    return restoreEntry16.Reject(SaveRestoreErrorCode.InvalidValue, "PlayerParty", "Nie spełniono warunku danych: !session.PlayerParty.TryAddActiveMember(characterId)");
            }

            var restoreIndex17 = 0;
            foreach (var characterId in saveData.BenchMemberIds)
            {
                var restoreEntry17 = context.Field("BenchMemberIds").Entry(restoreIndex17++, characterId);
                if (!session.CharactersById.ContainsKey(characterId))
                    return restoreEntry17.Reject(SaveRestoreErrorCode.UnknownReference, "", "Nie znaleziono postaci o zapisanym ID.");
                if (!session.PlayerParty.TryAddBenchMember(characterId))
                    return restoreEntry17.Reject(SaveRestoreErrorCode.InvalidValue, "PlayerParty", "Nie spełniono warunku danych: !session.PlayerParty.TryAddBenchMember(characterId)");
            }

            if (saveData.GroupsVersion != 0)
            {
                if (saveData.GroupsVersion != 1)
                    return context.Reject(SaveRestoreErrorCode.InvalidValue, "GroupsVersion", "Nie spełniono warunku danych: saveData.GroupsVersion != 1");
                if (saveData.Groups == null)
                    return context.Reject(SaveRestoreErrorCode.MissingValue, "Groups", "Nie spełniono warunku danych: saveData.Groups == null");
                var groups = new List<List<string>>();
                var restoreIndex18 = 0;
                foreach (var group in saveData.Groups)
                {
                    var restoreEntry18 = context.Field("Groups").Entry(restoreIndex18++, null);
                    if (group == null)
                        return restoreEntry18.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: group == null");
                    groups.Add(group.MemberIds);
                }
                if (!session.PlayerParty.TryRestoreGroups(groups))
                    return context.Reject(SaveRestoreErrorCode.InvalidValue, "Groups", "Nie spełniono warunku danych: !session.PlayerParty.TryRestoreGroups(groups)");
            }
            if (string.IsNullOrWhiteSpace(saveData.ActiveLeaderId))
            {
                return context.Require(string.IsNullOrWhiteSpace(
                    session.PlayerParty.ActiveLeaderId), "ActiveLeaderId", "Niezgodny lider aktywnej drużyny.");
            }

            return context.Entity(saveData.ActiveLeaderId).Require(session.PlayerParty.TrySetActiveLeader(
                saveData.ActiveLeaderId), "ActiveLeaderId", "Nie można przywrócić lidera drużyny.");
        }

        private static bool TryRestoreWorldFlags(
            GameSession session,
            List<KeyValueSaveData> worldFlags, RestoreContext context)
        {
            if (worldFlags == null)
            {
                return true;
            }

            var restoreIndex19 = 0;
            foreach (var worldFlag in worldFlags)
            {
                var restoreEntry19 = context.Entry(restoreIndex19++, worldFlag?.Key);
                if (worldFlag == null)
                    return restoreEntry19.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: worldFlag == null");
                if (!session.WorldState.TrySetFlag(
                        worldFlag.Key,
                        worldFlag.Value))
                    return restoreEntry19.Reject(SaveRestoreErrorCode.InvalidValue, "WorldState", "Nie spełniono warunku danych: !session.WorldState.TrySetFlag( worldFlag.Key, worldFlag.Value)");
            }

            return true;
        }

        private static bool TryRestoreQuests(
            GameSession session,
            List<QuestSaveData> questSaveDataList,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, int completedExpeditions, RestoreContext context)
        {
            if (questSaveDataList == null)
            {
                return true;
            }

            var restoreIndex20 = 0;
            foreach (var questSaveData in questSaveDataList)
            {
                var restoreEntry20 = context.Entry(restoreIndex20++, questSaveData?.QuestId);
                if (questSaveData == null)
                    return restoreEntry20.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: questSaveData == null");
                if (!contentCatalog.TryGetQuest(
                        questSaveData.QuestId,
                        out var definition))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "QuestId", "Nie spełniono warunku danych: !contentCatalog.TryGetQuest( questSaveData.QuestId, out var definition)");
                if (!TryCreateDictionary(
                        questSaveData.ObjectiveProgress,
                        progress => progress.ObjectiveId,
                        progress => progress.CurrentAmount,
                        out Dictionary<string, int> objectiveAmounts, restoreEntry20.Field("ObjectiveProgress")))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.ContractRejected, "ObjectiveProgress", "Nie spełniono warunku danych: !TryCreateDictionary( questSaveData.ObjectiveProgress, progress => progress.ObjectiveId, progress => progress.CurrentAmount, out Dictionary<string, int> objectiveAmounts, restoreEntry20.Field(\"ObjectiveProgress\"))");
                if (!TryCreateDictionary(
                        questSaveData.QuestFlags,
                        flag => flag.Key,
                        flag => flag.Value,
                        out Dictionary<string, string> questFlags, restoreEntry20.Field("QuestFlags")))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.ContractRejected, "QuestFlags", "Nie spełniono warunku danych: !TryCreateDictionary( questSaveData.QuestFlags, flag => flag.Key, flag => flag.Value, out Dictionary<string, string> questFlags, restoreEntry20.Field(\"QuestFlags\"))");

                var quest = new QuestInstance(definition);

                if (questSaveData.HasBoardContract)
                {
                    var contract=questSaveData.BoardContract;
                    if (contract==null)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "HasBoardContract", "Nie spełniono warunku danych: contract==null");
                    if (!Godmist.Systems.Quests.CatacombsMissionBoard.QuestIds.Contains(definition.Id))
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "Id", "Nie spełniono warunku danych: !Godmist.Systems.Quests.CatacombsMissionBoard.QuestIds.Contains(definition.Id)");
                    if (questSaveData.RadiantRewardLevel!=contract.Level)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardLevel", "Nie spełniono warunku danych: questSaveData.RadiantRewardLevel!=contract.Level");
                    if (contract.Cycle>completedExpeditions)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "Cycle", "Nie spełniono warunku danych: contract.Cycle>completedExpeditions");
                    try
                    {
                        if (!quest.TrySetBoardContract(new RadiantBoardContract(definition.StartStageId,contract.ObjectiveType,
                            contract.Theme,contract.Length,contract.Level,contract.RequiredAmount,contract.RewardMultiplier,contract.Cycle)))
                            return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "StartStageId", "Nie spełniono warunku danych: !quest.TrySetBoardContract(new RadiantBoardContract(definition.StartStageId,contract.ObjectiveType, contract.Theme,contract.Length,contract.Level,contract.RequiredAmount,contract.RewardMultiplier,contract.Cycle))");
                    }
                    catch (ArgumentException exception) { return restoreEntry20.Field("BoardContract").Exception(exception); }
                }

                if (questSaveData.RadiantRewardLevel != 0)
                {
                    if (questSaveData.RadiantRewardLevel < 1)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardLevel", "Nie spełniono warunku danych: questSaveData.RadiantRewardLevel < 1");
                    if (questSaveData.RadiantRewardLevel > 50)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardLevel", "Nie spełniono warunku danych: questSaveData.RadiantRewardLevel > 50");
                    if (questSaveData.RadiantRewardExperience < 0)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardExperience", "Nie spełniono warunku danych: questSaveData.RadiantRewardExperience < 0");
                    if (questSaveData.RadiantRewardGold < 0)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardGold", "Nie spełniono warunku danych: questSaveData.RadiantRewardGold < 0");
                    if (questSaveData.RadiantRewardHonor < 0)
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardHonor", "Nie spełniono warunku danych: questSaveData.RadiantRewardHonor < 0");
                    ItemInstance bonusItem = null;
                    if (!string.IsNullOrWhiteSpace(questSaveData.RadiantBonusItemInstanceId)
                        && !TryTakeItem(questSaveData.RadiantBonusItemInstanceId,
                            itemsById, assignedItemIds, out bonusItem, restoreEntry20.Field("RadiantBonusItemInstanceId")))
                        return restoreEntry20.Reject(SaveRestoreErrorCode.ContractRejected, "RadiantBonusItemInstanceId", "Nie spełniono warunku danych: !string.IsNullOrWhiteSpace(questSaveData.RadiantBonusItemInstanceId) && !TryTakeItem(questSaveData.RadiantBonusItemInstanceId, itemsById, assignedItemIds, out bonusItem, restoreEntry20.Field(\"RadiantBonusItemInstanceId\"))");
                    if (!quest.TrySetRadiantRewardOffer(new RadiantRewardOffer(
                        questSaveData.RadiantRewardLevel,
                        questSaveData.RadiantRewardExperience,
                        questSaveData.RadiantRewardGold,
                        questSaveData.RadiantRewardHonor,
                        bonusItem)))
                        return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantRewardLevel", "Nie spełniono warunku danych: !quest.TrySetRadiantRewardOffer(new RadiantRewardOffer( questSaveData.RadiantRewardLevel, questSaveData.RadiantRewardExperience, questSaveData.RadiantRewardGold, questSaveData.RadiantRewardHonor, bonusItem))");
                }
                else if (!string.IsNullOrWhiteSpace(questSaveData.RadiantBonusItemInstanceId))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "RadiantBonusItemInstanceId", "Nie spełniono warunku danych: !string.IsNullOrWhiteSpace(questSaveData.RadiantBonusItemInstanceId)");

                if (!LegacyContentIdentities.TryRewards(quest.QuestId, questSaveData.ClaimedRewardKeys, out var rewardKeys))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "ClaimedRewardKeys", "Unknown historical reward index; cannot infer identity from current order.");
                if (!quest.TryRestoreState(
                        questSaveData.Status,
                        questSaveData.CurrentStageId,
                        questSaveData.IsHidden,
                        objectiveAmounts,
                        questFlags,
                        questSaveData.ChosenBranchIds,
                        rewardKeys))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "Status", "Nie spełniono warunku danych: !quest.TryRestoreState( questSaveData.Status, questSaveData.CurrentStageId, questSaveData.IsHidden, objectiveAmounts, questFlags, questSaveData.ChosenBranchIds, questSaveData.ClaimedRewardKeys)");
                if (!session.TryRegisterQuest(quest))
                    return restoreEntry20.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !session.TryRegisterQuest(quest)");
            }

            return true;
        }

        private bool TryRestoreNpcs(
            GameSession session,
            List<NpcSaveData> npcSaveDataList,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (npcSaveDataList == null)
            {
                return true;
            }

            var restoreIndex21 = 0;
            foreach (var npcSaveData in npcSaveDataList)
            {
                var restoreEntry21 = context.Entry(restoreIndex21++, npcSaveData?.NpcId);
                if (npcSaveData == null)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: npcSaveData == null");
                if (npcSaveData.LoyaltyLevel < npcLoyaltyService.InitialLevel)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "LoyaltyLevel", "Nie spełniono warunku danych: npcSaveData.LoyaltyLevel < npcLoyaltyService.InitialLevel");
                if (npcSaveData.LoyaltyLevel > npcLoyaltyService.MaximumLevel)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "LoyaltyLevel", "Nie spełniono warunku danych: npcSaveData.LoyaltyLevel > npcLoyaltyService.MaximumLevel");
                if (npcSaveData.SpentGoldTotal < 0)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "SpentGoldTotal", "Nie spełniono warunku danych: npcSaveData.SpentGoldTotal < 0");
                if (npcSaveData.GoldBalance < 0)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "GoldBalance", "Nie spełniono warunku danych: npcSaveData.GoldBalance < 0");
                if (npcSaveData.TotalGoldReceived < 0)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "TotalGoldReceived", "Nie spełniono warunku danych: npcSaveData.TotalGoldReceived < 0");
                if (npcSaveData.TotalGoldSpent < 0)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "TotalGoldSpent", "Nie spełniono warunku danych: npcSaveData.TotalGoldSpent < 0");
                if (float.IsNaN(npcSaveData.GuildPayoutProgress))
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "GuildPayoutProgress", "Nie spełniono warunku danych: float.IsNaN(npcSaveData.GuildPayoutProgress)");
                if (float.IsInfinity(npcSaveData.GuildPayoutProgress))
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "GuildPayoutProgress", "Nie spełniono warunku danych: float.IsInfinity(npcSaveData.GuildPayoutProgress)");
                if (npcSaveData.GuildPayoutProgress < 0f)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "GuildPayoutProgress", "Nie spełniono warunku danych: npcSaveData.GuildPayoutProgress < 0f");
                if (npcSaveData.GuildPayoutProgress >= 1f)
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "GuildPayoutProgress", "Nie spełniono warunku danych: npcSaveData.GuildPayoutProgress >= 1f");
                if (!contentCatalog.TryGetNpc(
                        npcSaveData.NpcId,
                        out var definition))
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "NpcId", "Nie spełniono warunku danych: !contentCatalog.TryGetNpc( npcSaveData.NpcId, out var definition)");

                var npc = npcFactory.Create(definition);

                npc.LoyaltyLevel = npcSaveData.LoyaltyLevel;
                npc.SpentGoldTotal = npcSaveData.SpentGoldTotal;
                npc.GoldBalance = npcSaveData.GoldBalance;
                npc.TotalGoldReceived = npcSaveData.TotalGoldReceived;
                npc.TotalGoldSpent = npcSaveData.TotalGoldSpent;
                npc.GuildPayoutProgress = npcSaveData.GuildPayoutProgress;
                npc.RotatingStockItemIds = npcSaveData.RotatingStockItemIds == null
                    ? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(npcSaveData.RotatingStockItemIds);
                npc.CraftedStockInstanceIds = npcSaveData.CraftedStockInstanceIds == null
                    ? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(npcSaveData.CraftedStockInstanceIds);

                if (npc.ShopInventory == null)
                {
                    if ((npcSaveData.ShopItemInstanceIds?.Count ?? 0) > 0)
                        return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "ShopItemInstanceIds", "Nie spełniono warunku danych: (npcSaveData.ShopItemInstanceIds?.Count ?? 0) > 0");
                    if ((npcSaveData.BuybackEntries?.Count ?? 0) > 0)
                        return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "BuybackEntries", "Nie spełniono warunku danych: (npcSaveData.BuybackEntries?.Count ?? 0) > 0");
                }
                else
                {
                    npc.ShopInventory.Items.Clear();
                    npc.ShopInventory.BuybackStock.Clear();

                    if (!TryRestoreInventory(
                            npc.ShopInventory,
                            npcSaveData.ShopItemInstanceIds,
                            itemsById,
                            assignedItemIds, restoreEntry21.Field("ShopItemInstanceIds")))
                        return restoreEntry21.Reject(SaveRestoreErrorCode.ContractRejected, "ShopInventory", "Nie spełniono warunku danych: !TryRestoreInventory( npc.ShopInventory, npcSaveData.ShopItemInstanceIds, itemsById, assignedItemIds, restoreEntry21.Field(\"ShopItemInstanceIds\"))");
                    if (!TryRestoreBuybackEntries(
                            npc.ShopInventory,
                            npcSaveData.BuybackEntries,
                            itemsById,
                            assignedItemIds, restoreEntry21.Field("BuybackEntries")))
                        return restoreEntry21.Reject(SaveRestoreErrorCode.ContractRejected, "ShopInventory", "Nie spełniono warunku danych: !TryRestoreBuybackEntries( npc.ShopInventory, npcSaveData.BuybackEntries, itemsById, assignedItemIds, restoreEntry21.Field(\"BuybackEntries\"))");
                }

                TraderStockService.RemoveInvalidEquipmentOffers(npc);
                TraderStockService.EnforceAlchemyIngredientLimits(npc);

                if (!session.TryRegisterNpc(npc))
                    return restoreEntry21.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !session.TryRegisterNpc(npc)");
            }

            return true;
        }

        private static bool TryRestoreBuybackEntries(
            ShopInventory shopInventory,
            List<BuybackEntrySaveData> buybackEntries,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (buybackEntries == null)
            {
                return true;
            }

            var restoreIndex22 = 0;
            foreach (var buybackEntry in buybackEntries)
            {
                var restoreEntry22 = context.Entry(restoreIndex22++, buybackEntry?.ItemInstanceId);
                if (buybackEntry == null)
                    return restoreEntry22.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: buybackEntry == null");
                if (buybackEntry.SoldPrice < 0)
                    return restoreEntry22.Reject(SaveRestoreErrorCode.InvalidValue, "SoldPrice", "Nie spełniono warunku danych: buybackEntry.SoldPrice < 0");
                if (!TryTakeItem(
                        buybackEntry.ItemInstanceId,
                        itemsById,
                        assignedItemIds,
                        out var item, restoreEntry22))
                    return restoreEntry22.Reject(SaveRestoreErrorCode.ContractRejected, "ItemInstanceId", "Nie spełniono warunku danych: !TryTakeItem( buybackEntry.ItemInstanceId, itemsById, assignedItemIds, out var item, restoreEntry22)");

                shopInventory.BuybackStock.Add(
                    new BuybackEntry(
                        item,
                        buybackEntry.SoldPrice));
            }

            return true;
        }

        private static bool TryRestorePendingLoot(
            GameSession session,
            List<string> pendingLootItemInstanceIds,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (pendingLootItemInstanceIds == null)
            {
                return true;
            }

            var restoredItems = new List<ItemInstance>();

            var restoreIndex23 = 0;
            foreach (var itemInstanceId in pendingLootItemInstanceIds)
            {
                var restoreEntry23 = context.Entry(restoreIndex23++, itemInstanceId);
                if (!TryTakeItem(
                        itemInstanceId,
                        itemsById,
                        assignedItemIds,
                        out var item, restoreEntry23))
                    return restoreEntry23.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem( itemInstanceId, itemsById, assignedItemIds, out var item, restoreEntry23)");

                restoredItems.Add(item);
            }

            session.AddPendingLoot(restoredItems);
            return true;
        }

        private bool TryRestoreActiveDungeon(
            GameSession session,
            DungeonSaveData saveData,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            context = context.Entity(saveData?.DungeonId);
            if (saveData == null
                || string.IsNullOrWhiteSpace(saveData.DungeonId))
            {
                return true;
            }

            if (!contentCatalog.TryGetDungeon(
                    saveData.DungeonId,
                    out var definition))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "DungeonId", "Nie spełniono warunku danych: !contentCatalog.TryGetDungeon( saveData.DungeonId, out var definition)");
            if (!definition.IsProcedural)
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "IsProcedural", "Nie spełniono warunku danych: !definition.IsProcedural");

            DungeonInstance dungeon;

            try
            {
                if (saveData.LayoutVersion != 0)
                {
                    if (!DungeonLayoutSnapshot.TryRestore(saveData, definition, contentCatalog, out dungeon))
                        return context.Reject(SaveRestoreErrorCode.ContractRejected, "Layout", "Odtwarzanie geometrii lochu odrzucone; LayoutVersion=" + saveData.LayoutVersion);
                }
                else
                {
                    // Legacy saves have no generated layout. The next snapshot upgrades them.
                    if (saveData.Layout != null && saveData.Layout.Count > 0)
                        return context.Reject(SaveRestoreErrorCode.InvalidValue, "Layout", "Nie spełniono warunku danych: saveData.Layout != null && saveData.Layout.Count > 0");
                    dungeon = dungeonGenerator.Generate(
                        definition,
                        saveData.Seed,
                        saveData.DungeonLevel > 0
                            ? saveData.DungeonLevel
                            : definition.DungeonLevel, saveData.Length);

                    encounterPlacementSystem.Place(dungeon);
                    objectPlacementSystem.PlaceLegacySave(dungeon, contentCatalog.EquipmentPartDefinitions);
                    rewardPreparationSystem.Prepare(dungeon, contentCatalog.EquipmentPartDefinitions);
                }
            }
            catch (Exception exception)
            {
                return context.Exception(exception);
            }

            dungeon.ItemDropChanceBonus = float.IsNaN(saveData.ItemDropChanceBonus) || float.IsInfinity(saveData.ItemDropChanceBonus) ? 0 : Mathf.Clamp01(saveData.ItemDropChanceBonus);
            if (!dungeon.TryRestoreState(
                    saveData.CurrentFloorIndex,
                    saveData.IsCompleted,
                    saveData.HasPlayerPosition,
                    new Vector2(
                        saveData.PlayerPositionX,
                        saveData.PlayerPositionY),
                    saveData.ClearedEncounterIds,
                    saveData.HighestReachedFloorIndex,
                    saveData.GuildProgressCreditedFloorCount))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "CurrentFloorIndex", "Nie spełniono warunku danych: !dungeon.TryRestoreState( saveData.CurrentFloorIndex, saveData.IsCompleted, saveData.HasPlayerPosition, new Vector2( saveData.PlayerPositionX, saveData.PlayerPositionY), saveData.ClearedEncounterIds, saveData.HighestReachedFloorIndex, saveData.GuildProgressCreditedFloorCount)");

            if (saveData.TraderStockRefreshed) dungeon.MarkTraderStockRefreshed();
            if (saveData.CampSuppliesIssued) dungeon.MarkCampSuppliesIssued();

            if (!TryRestoreDungeonFloorDeltas(
                    dungeon,
                    saveData.FloorDeltas,
                    saveData.EncounterDeltas,
                    itemsById,
                    assignedItemIds, context))
                return context.Reject(SaveRestoreErrorCode.ContractRejected, "FloorDeltas", "Nie spełniono warunku danych: !TryRestoreDungeonFloorDeltas( dungeon, saveData.FloorDeltas, saveData.EncounterDeltas, itemsById, assignedItemIds, context)");

            if (!session.TrySetActiveDungeon(dungeon))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !session.TrySetActiveDungeon(dungeon)");

            return true;
        }

        private static bool TryRestoreDungeonFloorDeltas(
            DungeonInstance dungeon,
            List<DungeonFloorDeltaSaveData> floorDeltas,
            List<DungeonEncounterDeltaSaveData> encounterDeltas,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (dungeon == null)
                return context.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: dungeon == null");

            if (floorDeltas != null)
            {
                var restoredFloorIndices = new HashSet<int>();

                var restoreIndex24 = 0;
                foreach (var floorDelta in floorDeltas)
                {
                    var restoreEntry24 = context.Field("FloorDeltas").Entry(restoreIndex24++, floorDelta == null ? null : floorDelta.FloorIndex.ToString());
                    if (floorDelta == null)
                        return restoreEntry24.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: floorDelta == null");
                    if (!restoredFloorIndices.Add(floorDelta.FloorIndex))
                        return restoreEntry24.Reject(SaveRestoreErrorCode.InvalidValue, "FloorIndex", "Nie spełniono warunku danych: !restoredFloorIndices.Add(floorDelta.FloorIndex)");
                    if (!dungeon.TryGetFloor(
                            floorDelta.FloorIndex,
                            out var floor))
                        return restoreEntry24.Reject(SaveRestoreErrorCode.InvalidValue, "FloorIndex", "Nie spełniono warunku danych: !dungeon.TryGetFloor( floorDelta.FloorIndex, out var floor)");

                    if (!DungeonFogPersistence.Restore(floor, floorDelta.Fog)) return restoreEntry24.Reject(SaveRestoreErrorCode.ContractRejected, "Fog", "Nieprawidłowy stan mgły eksploracji.");
                    if (!DungeonPatrolPersistence.Restore(floor, floorDelta.Patrols)) return restoreEntry24.Reject(SaveRestoreErrorCode.ContractRejected, "Patrols", "Nieprawidłowy stan patroli.");
                    if (floorDelta.EnemyAwareness != null)
                    {
                        var awarenessIds = new HashSet<string>();
                        var awarenessIndex = 0;
                        foreach (var awareness in floorDelta.EnemyAwareness)
                        {
                            var entry = restoreEntry24.Field("EnemyAwareness").Entry(awarenessIndex++, awareness?.EnemyKey);
                            if (awareness == null || string.IsNullOrWhiteSpace(awareness.EnemyKey)
                                || !awarenessIds.Add(awareness.EnemyKey)
                                || !floor.GetEnemyAwareness(awareness.EnemyKey).TryRestore(awareness.Progress))
                                return entry.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nieprawidłowy EnemyKey lub Progress czujności.");
                        }
                    }
                    if (floorDelta.ActorFacings != null)
                    {
                        var facingIds = new HashSet<string>();
                        var facingIndex = 0;
                        foreach (var facing in floorDelta.ActorFacings)
                        {
                            var entry = restoreEntry24.Field("ActorFacings").Entry(facingIndex++, facing?.ActorId);
                            if (facing == null || !facingIds.Add(facing.ActorId)
                                || !floor.TrySetActorFacing(facing.ActorId, new UnityEngine.Vector2(facing.X, facing.Y)))
                                return entry.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nieprawidłowy ActorId lub kierunek X/Y.");
                        }
                    }
                    if (floorDelta.ActorPositions != null)
                    {
                        var actorIds = new HashSet<string>();
                        var restoreIndex25 = 0;
                        foreach (var saved in floorDelta.ActorPositions)
                        {
                            var restoreEntry25 = restoreEntry24.Field("ActorPositions").Entry(restoreIndex25++, saved?.ActorId);
                            if (saved == null)
                                return restoreEntry25.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saved == null");
                            if (string.IsNullOrWhiteSpace(saved.ActorId))
                                return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "ActorId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(saved.ActorId)");
                            if (!actorIds.Add(saved.ActorId))
                                return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "ActorId", "Nie spełniono warunku danych: !actorIds.Add(saved.ActorId)");
                            DungeonTraversalPosition position;
                            if (saved.IsOnStairs)
                            {
                                if (saved.StairStep <= 0)
                                    return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "StairStep", "Nie spełniono warunku danych: saved.StairStep <= 0");
                                if (saved.FirstLevel >= saved.SecondLevel)
                                    return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "FirstLevel", "Nie spełniono warunku danych: saved.FirstLevel >= saved.SecondLevel");
                                var stairs = new DungeonStairConnection(
                                    new DungeonSurfacePosition(new Vector2Int(saved.FirstX, saved.FirstY), saved.FirstLevel),
                                    new DungeonSurfacePosition(new Vector2Int(saved.SecondX, saved.SecondY), saved.SecondLevel));
                                position = new DungeonTraversalPosition(stairs, saved.StairStep);
                            }
                            else
                            {
                                if (saved.StairStep != 0)
                                    return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "StairStep", "Nie spełniono warunku danych: saved.StairStep != 0");
                                position = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(saved.X, saved.Y), saved.Level));
                            }
                            if (!float.IsFinite(saved.OffsetX))
                                return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "OffsetX", "Nie spełniono warunku danych: !float.IsFinite(saved.OffsetX)");
                            if (!float.IsFinite(saved.OffsetY))
                                return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "OffsetY", "Nie spełniono warunku danych: !float.IsFinite(saved.OffsetY)");
                            if (!floor.TrySetActorPosition(saved.ActorId, position))
                                return restoreEntry25.Reject(SaveRestoreErrorCode.InvalidValue, "ActorId", "Nie spełniono warunku danych: !floor.TrySetActorPosition(saved.ActorId, position)");
                            floor.SetActorOffset(saved.ActorId, new Vector2(saved.OffsetX, saved.OffsetY));
                        }
                    }

                    if (floorDelta.VisitedRoomIds != null)
                    {
                        var restoreIndex26 = 0;
                        foreach (var roomId in floorDelta.VisitedRoomIds)
                        {
                            var restoreEntry26 = restoreEntry24.Field("VisitedRoomIds").Entry(restoreIndex26++, roomId);
                            if (!floor.TryRestoreRoomVisited(roomId, true))
                                return restoreEntry26.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !floor.TryRestoreRoomVisited(roomId, true)");
                        }
                    }

                    if (floorDelta.ObjectDeltas != null)
                    {
                        var restoreIndex27 = 0;
                        foreach (var objectDelta in floorDelta.ObjectDeltas)
                        {
                            var restoreEntry27 = restoreEntry24.Field("ObjectDeltas").Entry(restoreIndex27++, objectDelta?.ObjectInstanceId);
                            if (objectDelta == null)
                                return restoreEntry27.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: objectDelta == null");
                            if (!floor.TryRestoreObjectState(
                                    objectDelta.ObjectInstanceId,
                                    objectDelta.IsActive,
                                    objectDelta.IsDoor,
                                    objectDelta.IsOpen,
                                    objectDelta.IsTriggered,
                                    objectDelta.IsActivated,
                                    objectDelta.IsSatisfied,
                                    objectDelta.IsChest,
                                    objectDelta.IsUnlocked))
                                return restoreEntry27.Reject(SaveRestoreErrorCode.InvalidValue, "ObjectInstanceId", "Nie spełniono warunku danych: !floor.TryRestoreObjectState( objectDelta.ObjectInstanceId, objectDelta.IsActive, objectDelta.IsDoor, objectDelta.IsOpen, objectDelta.IsTriggered, objectDelta.IsActivated, objectDelta.IsSatisfied, objectDelta.IsChest, objectDelta.IsUnlocked)");

                            if (objectDelta.HasObstacleDurability
                                && (!floor.TryGetObject(objectDelta.ObjectInstanceId, out var restoredObstacle)
                                    || restoredObstacle is not ObstacleDungeonObjectInstance obstacle
                                    || !obstacle.RestoreDurability(objectDelta.ObstacleDestructible, objectDelta.ObstacleMaxHits, objectDelta.ObstacleRemainingHits)))
                                return restoreEntry27.Reject(SaveRestoreErrorCode.InvalidValue, "HasObstacleDurability", "Nie spełniono warunku danych: objectDelta.HasObstacleDurability && (!floor.TryGetObject(objectDelta.ObjectInstanceId, out var restoredObstacle) || restoredObstacle is not ObstacleDungeonObjectInstance obstacle || !obstacle.RestoreDurability(objectDelta.ObstacleDestructible, objectDelta.ObstacleMaxHits, objectDelta.ObstacleRemainingHits))");
                            if (objectDelta.IsChest
                                && objectDelta.HasChestLootState)
                            {
                                var restoredChestItems = new List<ItemInstance>();

                                var restoreIndex28 = 0;
                                foreach (var itemInstanceId in
                                         objectDelta.ChestItemInstanceIds
                                         ?? new List<string>())
                                {
                                    var restoreEntry28 = restoreEntry27.Field("ChestItemInstanceIds").Entry(restoreIndex28++, itemInstanceId);
                                    if (!TryTakeItem(
                                            itemInstanceId,
                                            itemsById,
                                            assignedItemIds,
                                            out var item, restoreEntry28))
                                        return restoreEntry28.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem( itemInstanceId, itemsById, assignedItemIds, out var item, restoreEntry28)");

                                    restoredChestItems.Add(item);
                                }

                                if (!floor.TryRestoreChestItems(
                                        objectDelta.ObjectInstanceId,
                                        restoredChestItems))
                                    return restoreEntry27.Reject(SaveRestoreErrorCode.InvalidValue, "ObjectInstanceId", "Nie spełniono warunku danych: !floor.TryRestoreChestItems( objectDelta.ObjectInstanceId, restoredChestItems)");
                            }
                            if (objectDelta.IsChest && objectDelta.HasChestGoldState
                                && (!floor.TryGetObject(objectDelta.ObjectInstanceId, out var restoredGoldObject)
                                    || restoredGoldObject is not ChestDungeonObjectInstance restoredGoldChest
                                    || !restoredGoldChest.TryRestoreGold(objectDelta.ChestGold)))
                                return restoreEntry27.Reject(SaveRestoreErrorCode.InvalidValue, "IsChest", "Nie spełniono warunku danych: objectDelta.IsChest && objectDelta.HasChestGoldState && (!floor.TryGetObject(objectDelta.ObjectInstanceId, out var restoredGoldObject) || restoredGoldObject is not ChestDungeonObjectInstance restoredGoldChest || !restoredGoldChest.TryRestoreGold(objectDelta.ChestGold))");
                        }
                    }

                    if (floorDelta.ProgressionGateDeltas != null)
                    {
                        var restoredGateIds = new HashSet<string>(
                            StringComparer.Ordinal);

                        var restoreIndex29 = 0;
                        foreach (var gateDelta in floorDelta.ProgressionGateDeltas)
                        {
                            var restoreEntry29 = restoreEntry24.Field("ProgressionGateDeltas").Entry(restoreIndex29++, gateDelta?.GateId);
                            if (gateDelta == null)
                                return restoreEntry29.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: gateDelta == null");
                            if (string.IsNullOrWhiteSpace(gateDelta.GateId))
                                return restoreEntry29.Reject(SaveRestoreErrorCode.InvalidValue, "GateId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(gateDelta.GateId)");
                            if (!restoredGateIds.Add(gateDelta.GateId))
                                return restoreEntry29.Reject(SaveRestoreErrorCode.InvalidValue, "GateId", "Nie spełniono warunku danych: !restoredGateIds.Add(gateDelta.GateId)");
                            if (!floor.TryRestoreProgressionGateState(
                                    gateDelta.GateId,
                                    gateDelta.IsUnlocked))
                                return restoreEntry29.Reject(SaveRestoreErrorCode.InvalidValue, "GateId", "Nie spełniono warunku danych: !floor.TryRestoreProgressionGateState( gateDelta.GateId, gateDelta.IsUnlocked)");
                        }
                    }

                    if (!TryRestoreEnemyBodyDeltas(
                            floor,
                            floorDelta.EnemyBodyDeltas,
                            itemsById,
                            assignedItemIds, restoreEntry24.Field("EnemyBodyDeltas")))
                        return restoreEntry24.Reject(SaveRestoreErrorCode.ContractRejected, "EnemyBodyDeltas", "Nie spełniono warunku danych: !TryRestoreEnemyBodyDeltas( floor, floorDelta.EnemyBodyDeltas, itemsById, assignedItemIds, restoreEntry24.Field(\"EnemyBodyDeltas\"))");
                }
            }

            if (encounterDeltas == null)
            {
                return true;
            }

            var restoreIndex30 = 0;
            foreach (var encounterDelta in encounterDeltas)
            {
                var restoreEntry30 = context.Field("EncounterDeltas").Entry(restoreIndex30++, encounterDelta?.EncounterInstanceId);
                if (encounterDelta == null)
                    return restoreEntry30.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: encounterDelta == null");
                if (string.IsNullOrWhiteSpace(
                        encounterDelta.EncounterInstanceId))
                    return restoreEntry30.Reject(SaveRestoreErrorCode.InvalidValue, "EncounterInstanceId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace( encounterDelta.EncounterInstanceId)");

                var restored = false;

                foreach (var floor in dungeon.Floors)
                {
                    if (!floor.TryGetEncounter(
                            encounterDelta.EncounterInstanceId,
                            out _))
                    {
                        continue;
                    }

                    if (!floor.TryRestoreEncounterDefeatedEnemyInstanceIds(
                            encounterDelta.EncounterInstanceId,
                            encounterDelta.DefeatedEnemyInstanceIds))
                        return restoreEntry30.Reject(SaveRestoreErrorCode.InvalidValue, "EncounterInstanceId", "Nie spełniono warunku danych: !floor.TryRestoreEncounterDefeatedEnemyInstanceIds( encounterDelta.EncounterInstanceId, encounterDelta.DefeatedEnemyInstanceIds)");
                    if (!floor.TryGetEncounter(
                            encounterDelta.EncounterInstanceId,
                            out var restoredEncounter))
                        return restoreEntry30.Reject(SaveRestoreErrorCode.InvalidValue, "EncounterInstanceId", "Nie spełniono warunku danych: !floor.TryGetEncounter( encounterDelta.EncounterInstanceId, out var restoredEncounter)");
                    if (!restoredEncounter
                            .TryRestoreGeneratedEnemyBodyEnemyInstanceIds(
                                encounterDelta
                                    .GeneratedEnemyBodyEnemyInstanceIds))
                        return restoreEntry30.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !restoredEncounter .TryRestoreGeneratedEnemyBodyEnemyInstanceIds( encounterDelta .GeneratedEnemyBodyEnemyInstanceIds)");

                    if (encounterDelta.HasPreparedRewards)
                    {
                        if (encounterDelta.Rewards == null)
                            return restoreEntry30.Reject(SaveRestoreErrorCode.MissingValue, "Rewards", "Nie spełniono warunku danych: encounterDelta.Rewards == null");
                        var rewards = new CombatRewardResult();
                        var rewardIds = new HashSet<string>(StringComparer.Ordinal);
                        var restoreIndex31 = 0;
                        foreach (var savedReward in encounterDelta.Rewards)
                        {
                            var restoreEntry31 = restoreEntry30.Field("Rewards").Entry(restoreIndex31++, null);
                            if (savedReward == null)
                                return restoreEntry31.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: savedReward == null");
                            if (savedReward.Gold < 0)
                                return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "Gold", "Nie spełniono warunku danych: savedReward.Gold < 0");
                            if (savedReward.Experience < 0)
                                return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "Experience", "Nie spełniono warunku danych: savedReward.Experience < 0");
                            if (savedReward.ExperienceLevel < 0)
                                return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "ExperienceLevel", "Nie spełniono warunku danych: savedReward.ExperienceLevel < 0");
                            if (savedReward.ExperienceLevel > 50)
                                return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "ExperienceLevel", "Nie spełniono warunku danych: savedReward.ExperienceLevel > 50");
                            if (savedReward.ItemIds == null)
                                return restoreEntry31.Reject(SaveRestoreErrorCode.MissingValue, "ItemIds", "Nie spełniono warunku danych: savedReward.ItemIds == null");
                            if (!rewardIds.Add(savedReward.EnemyId ?? string.Empty))
                                return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "EnemyId", "Nie spełniono warunku danych: !rewardIds.Add(savedReward.EnemyId ?? string.Empty)");
                            var rewardItems = new List<ItemInstance>();
                            var restoreIndex32 = 0;
                            foreach (var itemId in savedReward.ItemIds)
                            {
                                var restoreEntry32 = restoreEntry31.Field("ItemIds").Entry(restoreIndex32++, itemId);
                                if (!TryTakeItem(itemId, itemsById, assignedItemIds, out var item, restoreEntry32))
                                    return restoreEntry32.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem(itemId, itemsById, assignedItemIds, out var item, restoreEntry32)");
                                rewardItems.Add(item);
                            }
                            if (string.IsNullOrEmpty(savedReward.EnemyId))
                            {
                                if (encounterDelta.Rewards.Count != 1)
                                    return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "Rewards", "Nie spełniono warunku danych: encounterDelta.Rewards.Count != 1");
                                rewards.Gold = savedReward.Gold;
                                rewards.ExperiencePerActiveCharacter = savedReward.Experience;
                                rewards.Items = rewardItems;
                            }
                            else
                            {
                                if (restoredEncounter.IsEnemyDefeated(savedReward.EnemyId))
                                    return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "EnemyId", "Nie spełniono warunku danych: restoredEncounter.IsEnemyDefeated(savedReward.EnemyId)");
                                if (restoredEncounter.IsEnemyBodyGenerated(savedReward.EnemyId))
                                    return restoreEntry31.Reject(SaveRestoreErrorCode.InvalidValue, "EnemyId", "Nie spełniono warunku danych: restoredEncounter.IsEnemyBodyGenerated(savedReward.EnemyId)");
                                bool isBoss = Godmist.Systems.Combat.ScoutCombatSystem.IsBoss(
                                    dungeon, restoredEncounter.Definition, savedReward.EnemyId);
                                rewards.EnemyRewards.Add(new CombatEnemyReward
                                {
                                    EnemyInstanceId = savedReward.EnemyId, Gold = savedReward.Gold,
                                    IsBoss = isBoss,
                                    ExperiencePerActiveCharacter = savedReward.ExperienceLevel > 0
                                        ? Godmist.Systems.Combat.CombatRewardSystem.CalculateEnemyExperience(
                                            savedReward.ExperienceLevel, savedReward.ExperienceLevel, isBoss)
                                        : savedReward.Experience,
                                    ExperienceLevel = savedReward.ExperienceLevel, Items = rewardItems
                                });
                            }
                        }
                        if (!dungeon.TryStorePreRolledRewards(encounterDelta.EncounterInstanceId, rewards))
                            return restoreEntry30.Reject(SaveRestoreErrorCode.InvalidValue, "EncounterInstanceId", "Nie spełniono warunku danych: !dungeon.TryStorePreRolledRewards(encounterDelta.EncounterInstanceId, rewards)");
                    }

                    restored = true;
                    break;
                }

                if (!restored)
                    return restoreEntry30.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !restored");
            }

            return true;
        }

        private static bool TryRestoreEnemyBodyDeltas(
            DungeonFloorInstance floor,
            List<DungeonEnemyBodySaveData> bodyDeltas,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds, RestoreContext context)
        {
            if (bodyDeltas == null) return true;

            var restoredBodyIds = new HashSet<string>(StringComparer.Ordinal);

            var restoreIndex33 = 0;
            foreach (var bodyDelta in bodyDeltas)
            {
                var restoreEntry33 = context.Entry(restoreIndex33++, bodyDelta?.BodyInstanceId);
                if (bodyDelta == null)
                    return restoreEntry33.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: bodyDelta == null");
                if (string.IsNullOrWhiteSpace(bodyDelta.BodyInstanceId))
                    return restoreEntry33.Reject(SaveRestoreErrorCode.InvalidValue, "BodyInstanceId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(bodyDelta.BodyInstanceId)");
                if (string.IsNullOrWhiteSpace(bodyDelta.EnemyInstanceId))
                    return restoreEntry33.Reject(SaveRestoreErrorCode.InvalidValue, "EnemyInstanceId", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(bodyDelta.EnemyInstanceId)");
                if (bodyDelta.Gold < 0)
                    return restoreEntry33.Reject(SaveRestoreErrorCode.InvalidValue, "Gold", "Nie spełniono warunku danych: bodyDelta.Gold < 0");
                if (!restoredBodyIds.Add(bodyDelta.BodyInstanceId))
                    return restoreEntry33.Reject(SaveRestoreErrorCode.InvalidValue, "BodyInstanceId", "Nie spełniono warunku danych: !restoredBodyIds.Add(bodyDelta.BodyInstanceId)");
                if (bodyDelta.ItemInstanceIds == null)
                    return restoreEntry33.Reject(SaveRestoreErrorCode.MissingValue, "ItemInstanceIds", "Nie spełniono warunku danych: bodyDelta.ItemInstanceIds == null");

                var restoredItems = new List<ItemInstance>();

                var restoreIndex34 = 0;
                foreach (var itemInstanceId in bodyDelta.ItemInstanceIds)
                {
                    var restoreEntry34 = restoreEntry33.Field("ItemInstanceIds").Entry(restoreIndex34++, itemInstanceId);
                    if (!TryTakeItem(
                            itemInstanceId,
                            itemsById,
                            assignedItemIds,
                            out var item, restoreEntry34))
                        return restoreEntry34.Reject(SaveRestoreErrorCode.ContractRejected, "", "Nie spełniono warunku danych: !TryTakeItem( itemInstanceId, itemsById, assignedItemIds, out var item, restoreEntry34)");

                    restoredItems.Add(item);
                }

                var body = new DungeonEnemyBodyInstance(
                    bodyDelta.BodyInstanceId,
                    bodyDelta.EnemyInstanceId,
                    new Vector2Int(
                        bodyDelta.PositionX,
                        bodyDelta.PositionY),
                    bodyDelta.Gold,
                    restoredItems);

                if (!floor.TryAddEnemyBody(body))
                    return restoreEntry33.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !floor.TryAddEnemyBody(body)");
            }

            return true;
        }

        private static bool TryCreatePassiveEffectSources(
            List<PassiveEffectSourceSaveData> saveDataList,
            out List<PassiveEffectSourceRuntime> sources, RestoreContext context)
        {
            sources = new List<PassiveEffectSourceRuntime>();

            if (saveDataList == null)
            {
                return true;
            }

            var restoreIndex35 = 0;
            foreach (var saveData in saveDataList)
            {
                var restoreEntry35 = context.Entry(restoreIndex35++, saveData?.SourceId);
                if (saveData == null)
                    return restoreEntry35.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saveData == null");
                if (!Enum.IsDefined(
                        typeof(PassiveEffectSourceType),
                        saveData.SourceType))
                    return restoreEntry35.Reject(SaveRestoreErrorCode.InvalidValue, "SourceType", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(PassiveEffectSourceType), saveData.SourceType)");

                sources.Add(new PassiveEffectSourceRuntime
                {
                    SourceType = saveData.SourceType,
                    SourceId = LegacyContentIdentities.Effect(saveData.SourceId) ?? string.Empty,
                    SourceCharacterId =
                        saveData.SourceCharacterId ?? string.Empty
                });
            }

            return true;
        }

        private static List<PassiveEffectSourceRuntime>
            CreateLegacyPassiveEffectSources(
                List<string> legacySourceEntityIds)
        {
            var sources = new List<PassiveEffectSourceRuntime>();

            if (legacySourceEntityIds == null)
            {
                return sources;
            }

            foreach (var sourceCharacterId in legacySourceEntityIds)
            {
                if (string.IsNullOrWhiteSpace(sourceCharacterId))
                {
                    continue;
                }

                sources.Add(new PassiveEffectSourceRuntime
                {
                    SourceType = PassiveEffectSourceType.RuntimeSystem,
                    SourceId = "legacy",
                    SourceCharacterId = sourceCharacterId
                });
            }

            return sources;
        }

        private static bool TryCreateVariableModifiers(
            List<GameplayVariableModifierSaveData> saveDataList,
            out List<GameplayVariableModifier> modifiers, RestoreContext context)
        {
            modifiers = new List<GameplayVariableModifier>();

            if (saveDataList == null)
            {
                return true;
            }

            var restoreIndex36 = 0;
            foreach (var saveData in saveDataList)
            {
                var restoreEntry36 = context.Entry(restoreIndex36++, null);
                if (saveData == null)
                    return restoreEntry36.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saveData == null");
                if (!Enum.IsDefined(
                        typeof(GameplayVariableType),
                        saveData.VariableType))
                    return restoreEntry36.Reject(SaveRestoreErrorCode.InvalidValue, "VariableType", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(GameplayVariableType), saveData.VariableType)");
                if (!Enum.IsDefined(
                        typeof(StatModifierStacking),
                        saveData.Stacking))
                    return restoreEntry36.Reject(SaveRestoreErrorCode.InvalidValue, "Stacking", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(StatModifierStacking), saveData.Stacking)");

                modifiers.Add(new GameplayVariableModifier
                {
                    VariableType = saveData.VariableType,
                    ReferenceId = saveData.ReferenceId ?? string.Empty,
                    Stacking = saveData.Stacking,
                    Value = saveData.Value
                });
            }

            return true;
        }

        private static bool TryCreateStatModifiers(
            List<StatModifierSaveData> saveDataList,
            out List<StatModifier> modifiers, RestoreContext context)
        {
            modifiers = new List<StatModifier>();

            if (saveDataList == null)
            {
                return true;
            }

            var restoreIndex37 = 0;
            foreach (var saveData in saveDataList)
            {
                var restoreEntry37 = context.Entry(restoreIndex37++, null);
                if (saveData == null)
                    return restoreEntry37.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: saveData == null");
                if (!Enum.IsDefined(
                        typeof(CharacterStatType),
                        saveData.StatType))
                    return restoreEntry37.Reject(SaveRestoreErrorCode.InvalidValue, "StatType", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(CharacterStatType), saveData.StatType)");
                if (!Enum.IsDefined(
                        typeof(StatModifierOperation),
                        saveData.Operation))
                    return restoreEntry37.Reject(SaveRestoreErrorCode.InvalidValue, "Operation", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(StatModifierOperation), saveData.Operation)");
                if (!Enum.IsDefined(
                        typeof(StatModifierStacking),
                        saveData.Stacking))
                    return restoreEntry37.Reject(SaveRestoreErrorCode.InvalidValue, "Stacking", "Nie spełniono warunku danych: !Enum.IsDefined( typeof(StatModifierStacking), saveData.Stacking)");

                modifiers.Add(new StatModifier
                {
                    StatType = saveData.StatType,
                    Operation = saveData.Operation,
                    Stacking = saveData.Stacking,
                    Value = saveData.Value
                });
            }

            return true;
        }

        private static bool TryCreateDictionary<TValue>(
            List<QuestObjectiveProgressSaveData> source,
            Func<QuestObjectiveProgressSaveData, string> getKey,
            Func<QuestObjectiveProgressSaveData, TValue> getValue,
            out Dictionary<string, TValue> result, RestoreContext context)
        {
            result = new Dictionary<string, TValue>(
                StringComparer.Ordinal);

            if (source == null)
            {
                return true;
            }

            var restoreIndex38 = 0;
            foreach (var entry in source)
            {
                var restoreEntry38 = context.Entry(restoreIndex38++, entry == null ? null : getKey(entry));
                if (entry == null)
                    return restoreEntry38.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: entry == null");
                if (string.IsNullOrWhiteSpace(getKey(entry)))
                    return restoreEntry38.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(getKey(entry))");
                if (!result.TryAdd(getKey(entry), getValue(entry)))
                    return restoreEntry38.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: !result.TryAdd(getKey(entry), getValue(entry))");
            }

            return true;
        }

        private static bool TryCreateDictionary(
            List<KeyValueSaveData> source,
            Func<KeyValueSaveData, string> getKey,
            Func<KeyValueSaveData, string> getValue,
            out Dictionary<string, string> result, RestoreContext context)
        {
            result = new Dictionary<string, string>(
                StringComparer.Ordinal);

            if (source == null)
            {
                return true;
            }

            var restoreIndex39 = 0;
            foreach (var entry in source)
            {
                var restoreEntry39 = context.Entry(restoreIndex39++, entry == null ? null : getKey(entry));
                if (entry == null)
                    return restoreEntry39.Reject(SaveRestoreErrorCode.MissingValue, "", "Nie spełniono warunku danych: entry == null");
                if (string.IsNullOrWhiteSpace(getKey(entry)))
                    return restoreEntry39.Reject(SaveRestoreErrorCode.InvalidValue, "", "Nie spełniono warunku danych: string.IsNullOrWhiteSpace(getKey(entry))");
                if (!result.TryAdd(
                        getKey(entry),
                        getValue(entry) ?? string.Empty))
                    return restoreEntry39.Reject(SaveRestoreErrorCode.InvalidValue, "Empty", "Nie spełniono warunku danych: !result.TryAdd( getKey(entry), getValue(entry) ?? string.Empty)");
            }

            return true;
        }
    }
}
