using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using UnityEngine;

namespace Godmist.Systems.SaveLoad
{
    public class GameSessionRestoreFactory
    {
        private readonly CharacterInstanceFactory characterFactory;
        private readonly ItemInstanceFactory itemFactory;
        private readonly NpcInstanceFactory npcFactory;
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
            if (!TryCreate(saveData, contentCatalog, out var session))
            {
                throw new InvalidOperationException(
                    "Nie udalo sie odtworzyc GameSession z zapisu.");
            }

            return session;
        }

        public bool TryCreate(
            GameSaveData saveData,
            GameContentCatalog contentCatalog,
            out GameSession session)
        {
            session = null;

            if (saveData == null
                || contentCatalog == null
                || saveData.SaveVersion != GameSaveData.CurrentSaveVersion
                || saveData.Party == null
                || saveData.Party.MaxActiveMembers
                    != Party.DefaultMaxActiveMembers
                || saveData.Gold < 0)
            {
                return false;
            }

            if (!TryCreateItems(
                    saveData.ItemInstances,
                    contentCatalog,
                    out var itemsById))
            {
                return false;
            }

            var restoredSession = new GameSession(
                GameModeState.Exploration);

            var assignedItemIds = new HashSet<string>(
                StringComparer.Ordinal);

            if (!TryRestoreCharacters(
                    restoredSession,
                    saveData.Characters,
                    contentCatalog,
                    itemsById,
                    assignedItemIds))
            {
                return false;
            }

            if (!TryRestoreParty(
                    restoredSession,
                    saveData.Party))
            {
                return false;
            }

            if (saveData.Gold > 0
                && !restoredSession.TryAddGold(saveData.Gold))
            {
                return false;
            }

            if (!TryRestoreWorldFlags(
                    restoredSession,
                    saveData.WorldFlags)
                || !TryRestoreQuests(
                    restoredSession,
                    saveData.Quests,
                    contentCatalog)
                || !TryRestoreNpcs(
                    restoredSession,
                    saveData.Npcs,
                    contentCatalog,
                    itemsById,
                    assignedItemIds)
                || !TryRestorePendingLoot(
                    restoredSession,
                    saveData.PendingLootItemInstanceIds,
                    itemsById,
                    assignedItemIds)
                || !TryRestoreActiveDungeon(
                    restoredSession,
                    saveData.ActiveDungeon,
                    contentCatalog))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(saveData.TrackedQuestId)
                && !restoredSession.TrySetTrackedQuest(
                    saveData.TrackedQuestId))
            {
                return false;
            }

            if (assignedItemIds.Count != itemsById.Count)
            {
                return false;
            }

            session = restoredSession;
            return true;
        }

        private bool TryCreateItems(
            List<ItemSaveData> itemSaveDataList,
            GameContentCatalog contentCatalog,
            out Dictionary<string, ItemInstance> itemsById)
        {
            itemsById = new Dictionary<string, ItemInstance>(
                StringComparer.Ordinal);

            if (itemSaveDataList == null)
            {
                return true;
            }

            foreach (var itemSaveData in itemSaveDataList)
            {
                if (itemSaveData == null
                    || string.IsNullOrWhiteSpace(itemSaveData.InstanceId)
                    || string.IsNullOrWhiteSpace(
                        itemSaveData.ItemDefinitionId)
                    || itemsById.ContainsKey(itemSaveData.InstanceId)
                    || !Enum.IsDefined(
                        typeof(RarityTier),
                        itemSaveData.CurrentRarity)
                    || !contentCatalog.TryGetItem(
                        itemSaveData.ItemDefinitionId,
                        out var definition))
                {
                    return false;
                }

                var isEquipmentDefinition =
                    definition is EquipmentItemDefinition;

                if (itemSaveData.IsEquipment != isEquipmentDefinition)
                {
                    return false;
                }

                var item = itemFactory.Create(
                    definition,
                    itemSaveData.InstanceId);

                item.CurrentRarity = itemSaveData.CurrentRarity;
                item.ItemLevel = itemSaveData.ItemLevel;

                if (item is EquipmentItemInstance equipmentItem)
                {
                    if (itemSaveData.RequiredLevel < 0
                        || !TryCreateStatModifiers(
                            itemSaveData.StatModifiers,
                            out var modifiers))
                    {
                        return false;
                    }

                    equipmentItem.RequiredClassId =
                        itemSaveData.RequiredClassId ?? string.Empty;

                    equipmentItem.RequiredLevel =
                        itemSaveData.RequiredLevel;

                    equipmentItem.StatModifiers = modifiers;
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
            HashSet<string> assignedItemIds)
        {
            if (characterSaveDataList == null)
            {
                return true;
            }

            foreach (var characterSaveData in characterSaveDataList)
            {
                if (!TryCreateCharacter(
                        characterSaveData,
                        contentCatalog,
                        itemsById,
                        assignedItemIds,
                        out var character)
                    || !session.TryRegisterCharacter(character))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCreateCharacter(
            CharacterSaveData saveData,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds,
            out CharacterInstance character)
        {
            character = null;

            if (saveData == null
                || string.IsNullOrWhiteSpace(saveData.CharacterId)
                || string.IsNullOrWhiteSpace(saveData.ClassId)
                || saveData.ResourcePool == null
                || saveData.Resistances == null
                || !Enum.IsDefined(
                    typeof(CharacterLifeState),
                    saveData.LifeState)
                || !Enum.IsDefined(
                    typeof(CharacterAvailabilityState),
                    saveData.AvailabilityState)
                || !contentCatalog.TryGetCharacter(
                    saveData.CharacterId,
                    out var definition)
                || !contentCatalog.TryGetCharacterClass(
                    saveData.ClassId,
                    out var classData))
            {
                return false;
            }

            if (definition.Id != saveData.CharacterId
                || classData.Id != saveData.ClassId
                || classData.ResourceType
                    != saveData.ResourcePool.ResourceType)
            {
                return false;
            }

            character = characterFactory.Create(definition, classData);

            character.Level = saveData.Level;
            character.CurrentExperience = saveData.CurrentExperience;
            character.AvailableSkillPoints = saveData.AvailableSkillPoints;
            character.CurrentHealth = saveData.CurrentHealth;
            character.Honor = saveData.Honor;
            character.WeaponAccuracy = saveData.WeaponAccuracy;
            character.LifeState = saveData.LifeState;
            character.AvailabilityState = saveData.AvailabilityState;

            RestoreResourcePool(
                character.ResourcePool,
                saveData.ResourcePool);

            RestoreResistances(
                character.Resistances,
                saveData.Resistances);

            if (!TryRestoreShieldLayers(
                    character,
                    saveData.ShieldLayers)
                || !TryRestoreSkills(
                    character,
                    saveData.SkillIds,
                    contentCatalog)
                || !TryRestorePassiveEffects(
                    character,
                    saveData.PassiveEffects)
                || !TryRestoreInventory(
                    character.Inventory,
                    saveData.InventoryItemInstanceIds,
                    itemsById,
                    assignedItemIds)
                || !TryRestoreEquipment(
                    character.Equipment,
                    saveData.Equipment,
                    itemsById,
                    assignedItemIds))
            {
                character = null;
                return false;
            }

            statPipeline.Refresh(character);
            return true;
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
            List<ShieldLayerSaveData> shieldLayers)
        {
            if (character.ShieldPool == null)
            {
                return false;
            }

            if (shieldLayers == null)
            {
                return true;
            }

            foreach (var shieldLayer in shieldLayers)
            {
                if (shieldLayer == null || shieldLayer.CurrentValue < 0)
                {
                    return false;
                }

                character.ShieldPool.AddLayer(
                    shieldLayer.CurrentValue,
                    shieldLayer.SourceId);
            }

            return true;
        }

        private static bool TryRestoreSkills(
            CharacterInstance character,
            List<string> skillIds,
            GameContentCatalog contentCatalog)
        {
            character.Skills.Clear();

            if (skillIds == null)
            {
                return true;
            }

            var restoredSkillIds = new HashSet<string>(
                StringComparer.Ordinal);

            foreach (var skillId in skillIds)
            {
                if (string.IsNullOrWhiteSpace(skillId)
                    || !restoredSkillIds.Add(skillId)
                    || !contentCatalog.TryGetSkill(
                        skillId,
                        out var skill))
                {
                    return false;
                }

                character.Skills.Add(skill);
            }

            return true;
        }

        private static bool TryRestorePassiveEffects(
            CharacterInstance character,
            List<PassiveEffectSaveData> passiveEffectSaveDataList)
        {
            character.PassiveEffects.Clear();

            if (passiveEffectSaveDataList == null)
            {
                return true;
            }

            foreach (var effectSaveData in passiveEffectSaveDataList)
            {
                if (effectSaveData == null
                    || string.IsNullOrWhiteSpace(effectSaveData.EffectId)
                    || effectSaveData.Stacks < 0
                    || effectSaveData.RemainingDuration < 0
                    || effectSaveData.TotalDamage < 0
                    || !TryCreateStatModifiers(
                        effectSaveData.StatModifiers,
                        out var statModifiers))
                {
                    return false;
                }

                character.PassiveEffects.Add(
                    new PassiveEffectRuntime(
                        effectSaveData.EffectId,
                        effectSaveData.SourceEntityIds,
                        effectSaveData.Stacks,
                        effectSaveData.RemainingDuration,
                        effectSaveData.TotalDamage)
                    {
                        StatModifiers = statModifiers
                    });
            }

            return true;
        }

        private bool TryRestoreInventory(
            Inventory inventory,
            List<string> itemInstanceIds,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (inventory?.Items == null)
            {
                return false;
            }

            if (itemInstanceIds == null)
            {
                return true;
            }

            foreach (var itemInstanceId in itemInstanceIds)
            {
                if (!TryTakeItem(
                        itemInstanceId,
                        itemsById,
                        assignedItemIds,
                        out var item)
                    || !inventoryService.TryAddItem(inventory, item))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryRestoreEquipment(
            EquipmentSet equipment,
            EquipmentSetSaveData saveData,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (equipment == null)
            {
                return false;
            }

            saveData ??= new EquipmentSetSaveData();

            return TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Weapon,
                       saveData.WeaponItemInstanceId,
                       itemsById,
                       assignedItemIds)
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Armor,
                       saveData.ArmorItemInstanceId,
                       itemsById,
                       assignedItemIds)
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Necklace,
                       saveData.NecklaceItemInstanceId,
                       itemsById,
                       assignedItemIds)
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Ring,
                       saveData.RingItemInstanceId,
                       itemsById,
                       assignedItemIds)
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Bracelet,
                       saveData.BraceletItemInstanceId,
                       itemsById,
                       assignedItemIds)
                   && TryRestoreEquipmentSlot(
                       equipment,
                       EquipmentSlot.Earrings,
                       saveData.EarringsItemInstanceId,
                       itemsById,
                       assignedItemIds);
        }

        private static bool TryRestoreEquipmentSlot(
            EquipmentSet equipment,
            EquipmentSlot slot,
            string itemInstanceId,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return true;
            }

            if (!TryTakeItem(
                    itemInstanceId,
                    itemsById,
                    assignedItemIds,
                    out var item)
                || item is not EquipmentItemInstance equipmentItem
                || equipmentItem.EquipmentSlot != slot)
            {
                return false;
            }

            equipment.SetItem(slot, equipmentItem);
            return true;
        }

        private static bool TryTakeItem(
            string itemInstanceId,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds,
            out ItemInstance item)
        {
            item = null;

            return !string.IsNullOrWhiteSpace(itemInstanceId)
                && itemsById.TryGetValue(
                    itemInstanceId,
                    out item)
                && assignedItemIds.Add(itemInstanceId);
        }

        private static bool TryRestoreParty(
            GameSession session,
            PartySaveData saveData)
        {
            if (saveData == null
                || saveData.MaxActiveMembers
                    != Party.DefaultMaxActiveMembers)
            {
                return false;
            }

            foreach (var characterId in saveData.ActiveMemberIds)
            {
                if (!session.TryAddCharacterToActiveParty(characterId))
                {
                    return false;
                }
            }

            foreach (var characterId in saveData.BenchMemberIds)
            {
                if (!session.TryAddCharacterToBench(characterId))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(saveData.ActiveLeaderId))
            {
                return string.IsNullOrWhiteSpace(
                    session.PlayerParty.ActiveLeaderId);
            }

            return session.TrySetActiveLeader(
                saveData.ActiveLeaderId);
        }

        private static bool TryRestoreWorldFlags(
            GameSession session,
            List<KeyValueSaveData> worldFlags)
        {
            if (worldFlags == null)
            {
                return true;
            }

            foreach (var worldFlag in worldFlags)
            {
                if (worldFlag == null
                    || !session.WorldState.TrySetFlag(
                        worldFlag.Key,
                        worldFlag.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryRestoreQuests(
            GameSession session,
            List<QuestSaveData> questSaveDataList,
            GameContentCatalog contentCatalog)
        {
            if (questSaveDataList == null)
            {
                return true;
            }

            foreach (var questSaveData in questSaveDataList)
            {
                if (questSaveData == null
                    || !contentCatalog.TryGetQuest(
                        questSaveData.QuestId,
                        out var definition)
                    || !TryCreateDictionary(
                        questSaveData.ObjectiveProgress,
                        progress => progress.ObjectiveId,
                        progress => progress.CurrentAmount,
                        out Dictionary<string, int> objectiveAmounts)
                    || !TryCreateDictionary(
                        questSaveData.QuestFlags,
                        flag => flag.Key,
                        flag => flag.Value,
                        out Dictionary<string, string> questFlags))
                {
                    return false;
                }

                var quest = new QuestInstance(definition);

                if (!quest.TryRestoreState(
                        questSaveData.Status,
                        questSaveData.CurrentStageId,
                        questSaveData.IsHidden,
                        objectiveAmounts,
                        questFlags,
                        questSaveData.ChosenBranchIds,
                        questSaveData.ClaimedRewardKeys)
                    || !session.TryRegisterQuest(quest))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryRestoreNpcs(
            GameSession session,
            List<NpcSaveData> npcSaveDataList,
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (npcSaveDataList == null)
            {
                return true;
            }

            foreach (var npcSaveData in npcSaveDataList)
            {
                if (npcSaveData == null
                    || npcSaveData.LoyaltyLevel < 1
                    || npcSaveData.SpentGoldTotal < 0
                    || !contentCatalog.TryGetNpc(
                        npcSaveData.NpcId,
                        out var definition))
                {
                    return false;
                }

                var npc = npcFactory.Create(definition);

                npc.LoyaltyLevel = npcSaveData.LoyaltyLevel;
                npc.SpentGoldTotal = npcSaveData.SpentGoldTotal;

                if (npc.ShopInventory == null)
                {
                    if ((npcSaveData.ShopItemInstanceIds?.Count ?? 0) > 0
                        || (npcSaveData.BuybackEntries?.Count ?? 0) > 0)
                    {
                        return false;
                    }
                }
                else
                {
                    npc.ShopInventory.Items.Clear();
                    npc.ShopInventory.BuybackStock.Clear();

                    if (!TryRestoreInventory(
                            npc.ShopInventory,
                            npcSaveData.ShopItemInstanceIds,
                            itemsById,
                            assignedItemIds)
                        || !TryRestoreBuybackEntries(
                            npc.ShopInventory,
                            npcSaveData.BuybackEntries,
                            itemsById,
                            assignedItemIds))
                    {
                        return false;
                    }
                }

                if (!session.TryRegisterNpc(npc))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryRestoreBuybackEntries(
            ShopInventory shopInventory,
            List<BuybackEntrySaveData> buybackEntries,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (buybackEntries == null)
            {
                return true;
            }

            foreach (var buybackEntry in buybackEntries)
            {
                if (buybackEntry == null
                    || buybackEntry.SoldPrice < 0
                    || !TryTakeItem(
                        buybackEntry.ItemInstanceId,
                        itemsById,
                        assignedItemIds,
                        out var item))
                {
                    return false;
                }

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
            HashSet<string> assignedItemIds)
        {
            if (pendingLootItemInstanceIds == null)
            {
                return true;
            }

            var restoredItems = new List<ItemInstance>();

            foreach (var itemInstanceId in pendingLootItemInstanceIds)
            {
                if (!TryTakeItem(
                        itemInstanceId,
                        itemsById,
                        assignedItemIds,
                        out var item))
                {
                    return false;
                }

                restoredItems.Add(item);
            }

            session.AddPendingLoot(restoredItems);
            return true;
        }

        private bool TryRestoreActiveDungeon(
            GameSession session,
            DungeonSaveData saveData,
            GameContentCatalog contentCatalog)
        {
            if (saveData == null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(saveData.DungeonId)
                || !contentCatalog.TryGetDungeon(
                    saveData.DungeonId,
                    out var definition)
                || !definition.IsProcedural)
            {
                return false;
            }

            DungeonInstance dungeon;

            try
            {
                dungeon = dungeonGenerator.Generate(
                    definition,
                    saveData.Seed);

                encounterPlacementSystem.Place(dungeon);
                objectPlacementSystem.Place(dungeon);
                rewardPreparationSystem.Prepare(dungeon);
            }
            catch (Exception)
            {
                return false;
            }

            if (!dungeon.TryRestoreState(
                    saveData.CurrentFloorIndex,
                    saveData.IsCompleted,
                    saveData.HasPlayerPosition,
                    new Vector2(
                        saveData.PlayerPositionX,
                        saveData.PlayerPositionY),
                    saveData.ClearedEncounterIds)
                || !TryRestoreDungeonFloorDeltas(
                    dungeon,
                    saveData.FloorDeltas)
                || !session.TrySetActiveDungeon(dungeon))
            {
                return false;
            }

            return true;
        }

        private static bool TryRestoreDungeonFloorDeltas(
            DungeonInstance dungeon,
            List<DungeonFloorDeltaSaveData> floorDeltas)
        {
            if (floorDeltas == null)
            {
                return true;
            }

            var restoredFloorIndices = new HashSet<int>();

            foreach (var floorDelta in floorDeltas)
            {
                if (floorDelta == null
                    || !restoredFloorIndices.Add(
                        floorDelta.FloorIndex)
                    || !dungeon.TryGetFloor(
                        floorDelta.FloorIndex,
                        out var floor))
                {
                    return false;
                }

                if (floorDelta.VisitedRoomIds != null)
                {
                    foreach (var roomId in floorDelta.VisitedRoomIds)
                    {
                        if (!floor.TryRestoreRoomVisited(
                                roomId,
                                true))
                        {
                            return false;
                        }
                    }
                }

                if (floorDelta.ObjectDeltas == null)
                {
                    continue;
                }

                foreach (var objectDelta
                    in floorDelta.ObjectDeltas)
                {
                    if (objectDelta == null
                        || !floor.TryRestoreObjectState(
                            objectDelta.ObjectInstanceId,
                            objectDelta.IsActive,
                            objectDelta.IsDoor,
                            objectDelta.IsOpen))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryCreateStatModifiers(
            List<StatModifierSaveData> saveDataList,
            out List<StatModifier> modifiers)
        {
            modifiers = new List<StatModifier>();

            if (saveDataList == null)
            {
                return true;
            }

            foreach (var saveData in saveDataList)
            {
                if (saveData == null
                    || !Enum.IsDefined(
                        typeof(CharacterStatType),
                        saveData.StatType)
                    || !Enum.IsDefined(
                        typeof(StatModifierOperation),
                        saveData.Operation)
                    || !Enum.IsDefined(
                        typeof(StatModifierStacking),
                        saveData.Stacking))
                {
                    return false;
                }

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
            out Dictionary<string, TValue> result)
        {
            result = new Dictionary<string, TValue>(
                StringComparer.Ordinal);

            if (source == null)
            {
                return true;
            }

            foreach (var entry in source)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(getKey(entry))
                    || !result.TryAdd(getKey(entry), getValue(entry)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCreateDictionary(
            List<KeyValueSaveData> source,
            Func<KeyValueSaveData, string> getKey,
            Func<KeyValueSaveData, string> getValue,
            out Dictionary<string, string> result)
        {
            result = new Dictionary<string, string>(
                StringComparer.Ordinal);

            if (source == null)
            {
                return true;
            }

            foreach (var entry in source)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(getKey(entry))
                    || !result.TryAdd(
                        getKey(entry),
                        getValue(entry) ?? string.Empty))
                {
                    return false;
                }
            }

            return true;
        }
    }
}