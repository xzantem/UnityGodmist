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
                Debug.LogError("Odtwarzanie GameSession odrzucone na walidacji nagłówka zapisu.");
                return false;
            }

            if (!TryCreateItems(
                    saveData.ItemInstances,
                    contentCatalog,
                    out var itemsById))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy tworzeniu itemów.");
                return false;
            }

            var restoredSession = new GameSession(
                GameModeState.Exploration, saveData.CampaignId);

            if (saveData.HasExplorationPosition
                && !restoredSession.TrySetExplorationPosition(
                    new Vector2(
                        saveData.ExplorationPositionX,
                        saveData.ExplorationPositionY)))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy pozycji eksploracji.");
                return false;
            }

            var assignedItemIds = new HashSet<string>(
                StringComparer.Ordinal);

            if (!TryRestoreLootBagContents(
                    saveData.ItemInstances,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu zawartości lootbagów.");
                return false;
            }

            if (!TryRestoreSocketedGaldurytes(
                    saveData.ItemInstances,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy osadzonych galdurytach.");
                return false;
            }

            if (!TryRestoreCharacters(
                    restoredSession,
                    saveData.Characters,
                    contentCatalog,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu postaci.");
                return false;
            }

            if (!TryRestoreInventory(
                    restoredSession.Stash,
                    saveData.StashItemInstanceIds,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu magazynu.");
                return false;
            }

            if (!TryRestoreParty(
                    restoredSession,
                    saveData.Party))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu drużyny.");
                return false;
            }

            if (saveData.Gold > 0
                && !restoredSession.TryAddGold(saveData.Gold))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu złota.");
                return false;
            }

            if (!TryRestoreWorldFlags(
                    restoredSession,
                    saveData.WorldFlags))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu flag świata.");
                return false;
            }

            if (!TryRestoreQuests(
                    restoredSession,
                    saveData.Quests,
                    contentCatalog))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu questów.");
                return false;
            }

            if (!TryRestoreNpcs(
                    restoredSession,
                    saveData.Npcs,
                    contentCatalog,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu NPC.");
                return false;
            }

            if (!TryRestorePendingLoot(
                    restoredSession,
                    saveData.PendingLootItemInstanceIds,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError("Odtwarzanie GameSession odrzucone przy odtwarzaniu oczekującego lootu.");
                return false;
            }

            if (!TryRestoreActiveDungeon(
                    restoredSession,
                    saveData.ActiveDungeon,
                    contentCatalog,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError(
                    $"Nie udało się odtworzyć aktywnego lochu "
                    + $"'{saveData.ActiveDungeon?.DungeonId}' "
                    + $"(seed: {saveData.ActiveDungeon?.Seed}).");
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
                Debug.LogError(
                    "Nie udało się przypisać wszystkich itemów z zapisu. "
                    + $"Przypisano: {assignedItemIds.Count}, "
                    + $"oczekiwano: {itemsById.Count}.");
                return false;
            }

            session = restoredSession;
            return true;
        }

        private static bool TryRestoreLootBagContents(
            List<ItemSaveData> itemSaveDataList,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (itemSaveDataList == null) return true;

            foreach (var itemSaveData in itemSaveDataList)
            {
                if (itemSaveData == null || !itemSaveData.IsLootBag)
                {
                    continue;
                }

                if (!itemsById.TryGetValue(
                        itemSaveData.InstanceId,
                        out var item)
                    || item is not LootBagItemInstance lootBag)
                {
                    return false;
                }

                var restoredContents = new List<ItemInstance>();

                foreach (var contentInstanceId in
                         itemSaveData.LootBagContentItemInstanceIds
                         ?? new List<string>())
                {
                    if (!TryTakeItem(
                            contentInstanceId,
                            itemsById,
                            assignedItemIds,
                            out var content))
                    {
                        return false;
                    }

                    restoredContents.Add(content);
                }

                if (!lootBag.TryRestoreContents(restoredContents))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryRestoreSocketedGaldurytes(
            List<ItemSaveData> itemSaveDataList,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            foreach (var data in itemSaveDataList ?? new List<ItemSaveData>())
            {
                if (data?.SocketedGalduryteInstanceIds == null
                    || data.SocketedGalduryteInstanceIds.Count == 0) continue;
                if (!itemsById.TryGetValue(data.InstanceId, out var item)
                    || item is not EquipmentItemInstance equipment) return false;

                equipment.SocketedGaldurytes = new List<GalduryteItemInstance>();
                foreach (var id in data.SocketedGalduryteInstanceIds)
                {
                    if (!TryTakeItem(id, itemsById, assignedItemIds, out var socketed)
                        || socketed is not GalduryteItemInstance galduryte
                        || !galduryte.IsIdentified
                        || equipment.SocketedGaldurytes.Count >= equipment.SocketCapacity)
                        return false;
                    equipment.SocketedGaldurytes.Add(galduryte);
                }
            }
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

                var isLootBagDefinition = definition is LootBagItemDefinition;

                var isConsumableDefinition = definition is ConsumableItemDefinition;
                var isGalduryteDefinition = definition is GalduryteItemDefinition;

                if (itemSaveData.IsEquipment != isEquipmentDefinition
                    || itemSaveData.IsLootBag != isLootBagDefinition
                    || itemSaveData.IsConsumable != isConsumableDefinition
                    || itemSaveData.IsGalduryte != isGalduryteDefinition
                    || (itemSaveData.IsEquipment && itemSaveData.IsLootBag)
                    || (itemSaveData.IsConsumable && (itemSaveData.IsEquipment || itemSaveData.IsLootBag || itemSaveData.IsGalduryte))
                    || (itemSaveData.IsGalduryte && (itemSaveData.IsEquipment || itemSaveData.IsLootBag)))
                {
                    return false;
                }

                if (definition is UniqueEquipmentDefinition
                    && itemSaveData.Quality != EquipmentQuality.Masterpiece)
                {
                    return false;
                }

                var item = itemFactory.Create(
                    definition,
                    itemSaveData.InstanceId);

                item.CurrentRarity = itemSaveData.CurrentRarity;
                item.ItemLevel = itemSaveData.ItemLevel;
                item.CustomName = itemSaveData.CustomName ?? string.Empty;

                if (item is ConsumableItemInstance consumableItem)
                {
                    if (itemSaveData.CatalystDurationModifier < -20
                        || itemSaveData.CatalystDurationModifier > 20
                        || float.IsNaN(itemSaveData.CatalystEffectMultiplier)
                        || float.IsInfinity(itemSaveData.CatalystEffectMultiplier)
                        || itemSaveData.CatalystEffectMultiplier < 0f
                        || itemSaveData.CatalystChargeBonus < 0
                        || itemSaveData.CatalystChargeBonus > 9)
                    {
                        return false;
                    }

                    consumableItem.CatalystDurationModifier = itemSaveData.CatalystDurationModifier;
                    consumableItem.CatalystEffectMultiplier = itemSaveData.CatalystEffectMultiplier;
                    consumableItem.CatalystChargeBonus = itemSaveData.CatalystChargeBonus;
                    consumableItem.CatalystDisplayName = itemSaveData.CatalystDisplayName ?? string.Empty;
                    consumableItem.CraftedBaseValue = itemSaveData.ConsumableCraftedBaseValue;
                    if (itemSaveData.CurrentCharges < 0 || itemSaveData.CurrentCharges > consumableItem.MaxCharges) return false;
                    consumableItem.CurrentCharges = itemSaveData.CurrentCharges;
                    if (consumableItem.IsBomb)
                    {
                        if (itemSaveData.BlastRange < ConsumableItemInstance.DefaultBombRange
                            || itemSaveData.BlastRange > ConsumableItemInstance.MaxBombRange
                            || itemSaveData.BlastAreaSize < ConsumableItemInstance.DefaultBombAreaSize
                            || itemSaveData.BlastAreaSize > ConsumableItemInstance.MaxBombAreaSize)
                        {
                            return false;
                        }

                        consumableItem.ConfigureBombTargeting(
                            itemSaveData.BlastRange,
                            itemSaveData.BlastAreaSize);
                    }
                    consumableItem.RuntimeEffects = new List<ConsumableEffectRuntime>();
                    foreach (var effect in itemSaveData.ConsumableEffects ?? new List<ConsumableEffectSaveData>())
                    {
                        if (effect == null || !Enum.IsDefined(typeof(ConsumableRuntimeEffectType), effect.EffectType)
                            || float.IsNaN(effect.Value) || float.IsInfinity(effect.Value)
                            || float.IsNaN(effect.Chance) || float.IsInfinity(effect.Chance)
                            || effect.Chance < 0f || effect.Chance > 1f || effect.DurationTurns < 0)
                        {
                            return false;
                        }
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
                    if (!Enum.IsDefined(typeof(GalduryteTargetType), itemSaveData.GalduryteTargetType)
                        || !Enum.IsDefined(typeof(GalduryteTier), itemSaveData.GalduryteTier)
                        || !Enum.IsDefined(typeof(GaldurytePowderColor), itemSaveData.GaldurytePowderColor)
                        || itemSaveData.GalduryteCraftedBaseValue < 0
                        || string.IsNullOrWhiteSpace(itemSaveData.GalduryteStoneDefinitionId)
                        || string.IsNullOrWhiteSpace(itemSaveData.GaldurytePowderDefinitionId)
                        || itemSaveData.GalduryteEffects == null
                        || itemSaveData.GalduryteEffects.Count == 0
                        || itemSaveData.ItemLevel != GalduryteRuntimeRules.GetLevel(itemSaveData.GalduryteTier))
                    {
                        return false;
                    }

                    var effectTypes = new HashSet<GalduryteEffectType>();
                    var effectColors = new HashSet<GaldurytePowderColor>();
                    var restoredEffects = new List<GalduryteEffectInstance>();
                    foreach (var effect in itemSaveData.GalduryteEffects)
                    {
                        if (effect == null
                            || !Enum.IsDefined(typeof(GalduryteEffectType), effect.EffectType)
                            || !Enum.IsDefined(typeof(GaldurytePowderColor), effect.PowderColor)
                            || !Enum.IsDefined(typeof(GalduryteEffectGrade), effect.Grade)
                            || float.IsNaN(effect.Value)
                            || float.IsInfinity(effect.Value)
                            || effect.Value < 0f
                            || !effectTypes.Add(effect.EffectType)
                            || !effectColors.Add(effect.PowderColor))
                        {
                            return false;
                        }

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

                    if (itemSaveData.RequiredLevel < 0
                        || float.IsNaN(itemSaveData.UpgradeProgress)
                        || float.IsInfinity(itemSaveData.UpgradeProgress)
                        || itemSaveData.UpgradeProgress < 0f
                        || itemSaveData.UpgradeProgress > 1f
                        || !Enum.IsDefined(
                            typeof(EquipmentQuality),
                            itemSaveData.Quality)
                        || partIds.Any(string.IsNullOrWhiteSpace)
                        || !TryCreateStatModifiers(
                            itemSaveData.BaseStatModifiers,
                            out var baseModifiers)
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

                    equipmentItem.Quality = itemSaveData.Quality;
                    equipmentItem.PartIds = new List<string>(
                        partIds);
                    equipmentItem.PrimaryPartDisplayName = itemSaveData.PrimaryPartDisplayName ?? string.Empty;
                    equipmentItem.PrimaryMaterialId = itemSaveData.PrimaryMaterialId ?? string.Empty;
                    equipmentItem.CraftedBaseValue = itemSaveData.EquipmentCraftedBaseValue;
                    equipmentItem.BaseStatModifiers = baseModifiers.Count > 0
                        ? baseModifiers
                        : new List<StatModifier>(modifiers);
                    equipmentItem.StatModifiers = modifiers;
                    equipmentItem.UpgradeProgress = itemSaveData.UpgradeProgress;

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
            character.SkillTreeProgress = RestoreSkillTreeProgress(
                saveData.SkillTreeProgress,
                classData);
            CharacterProgressionSystem.TryMigrateLegacySkillPointTotal(character);
            character.CurrentHealth = saveData.CurrentHealth;
            character.Honor = saveData.Honor;
            character.WeaponAccuracy = saveData.WeaponAccuracy;
            character.LifeState = saveData.LifeState;
            character.AvailabilityState = saveData.AvailabilityState;

            RestoreResourcePool(
                character.ResourcePool,
                saveData.ResourcePool);

            if (saveData.HasPersistentResistances)
            {
                RestoreResistances(
                    character.Resistances,
                    saveData.Resistances);
            }

            if (!TryRestoreShieldLayers(
                    character,
                    saveData.ShieldLayers)
                || !TryRestoreSkills(
                    character,
                    saveData.SkillIds,
                    contentCatalog)
                || !TryRestoreSkillLoadout(
                    character,
                    saveData)
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

            new CharacterLevelGrowthSystem(statPipeline).Apply(
                character,
                grantCurrentHealthGrowth: false);
            return true;
        }

        private static bool TryRestoreSkillLoadout(
            CharacterInstance character,
            CharacterSaveData saveData)
        {
            var knownSkillIds = GetSkillIds(character.Skills);
            var basicAttackSkillId = ResolveRestoredBasicAttackSkillId(
                character,
                saveData,
                knownSkillIds);

            if (string.IsNullOrWhiteSpace(basicAttackSkillId)
                || !knownSkillIds.Contains(basicAttackSkillId))
            {
                return !saveData.HasActiveSkillLoadout;
            }

            if (!saveData.HasActiveSkillLoadout)
            {
                return character.ActiveSkillLoadout.TryInitialize(
                    knownSkillIds,
                    basicAttackSkillId);
            }

            var modifiers = new List<SkillLoadoutCapacityModifier>();
            foreach (var saveDataModifier in saveData.ActiveSkillCapacityModifiers
                         ?? new List<SkillLoadoutCapacityModifierSaveData>())
            {
                if (saveDataModifier == null
                    || string.IsNullOrWhiteSpace(saveDataModifier.SourceId))
                {
                    return false;
                }

                modifiers.Add(new SkillLoadoutCapacityModifier
                {
                    SourceId = saveDataModifier.SourceId,
                    Amount = saveDataModifier.Amount
                });
            }

            return saveData.ActiveSkillSlotIds != null
                   && saveData.ActiveSkillSlotIds.Count > 0
                ? character.ActiveSkillLoadout.TryInitializeSlots(
                    knownSkillIds,
                    basicAttackSkillId,
                    saveData.ActiveSkillSlotIds,
                    modifiers)
                : character.ActiveSkillLoadout.TryInitialize(
                    knownSkillIds,
                    basicAttackSkillId,
                    saveData.ActiveSkillIds,
                    modifiers);
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
            List<SkillData> skills)
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

            var restoredSkillIds = new HashSet<string>(
                StringComparer.Ordinal);

            foreach (var skillId in skillIds ?? new List<string>())
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
                    || !IsValidPassiveEffectDefinitionId(effectSaveData.DefinitionId)
                    || effectSaveData.Stacks < 0
                    || effectSaveData.RemainingDuration < 0
                    || !Enum.IsDefined(
                        typeof(PassiveEffectLifetime),
                        effectSaveData.Lifetime)
                    || !Enum.IsDefined(
                        typeof(PassiveEffectStackingRule),
                        effectSaveData.StackingRule)
                    || !TryCreatePassiveEffectSources(
                        effectSaveData.Sources,
                        out var sources)
                    || !TryCreatePassiveEffectPayloads(
                        effectSaveData.Payloads,
                        out var payloads))
                {
                    return false;
                }

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
                        out var stateValues))
                {
                    return false;
                }

                character.PassiveEffects.Add(
                    new PassiveEffectRuntime(
                        effectSaveData.EffectId,
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
                    });
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
            out List<PassiveEffectRuntimeStateValue> stateValues)
        {
            stateValues = new List<PassiveEffectRuntimeStateValue>();
            if (saveDataList == null) return true;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var saveData in saveDataList)
            {
                if (saveData == null
                    || string.IsNullOrWhiteSpace(saveData.Key)
                    || saveData.Key.Length > 256
                    || !Enum.IsDefined(typeof(PassiveEffectRuntimeValueType), saveData.Type)
                    || !keys.Add(saveData.Key)
                    || float.IsNaN(saveData.NumberValue)
                    || float.IsInfinity(saveData.NumberValue))
                {
                    stateValues = null;
                    return false;
                }

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
            out List<PassiveEffectPayloadRuntime> payloads)
        {
            payloads = new List<PassiveEffectPayloadRuntime>();
            if (saveDataList == null) return true;

            var types = new HashSet<PassiveEffectPayloadType>();
            foreach (var saveData in saveDataList)
            {
                if (saveData == null
                    || !Enum.IsDefined(typeof(PassiveEffectPayloadType), saveData.Type)
                    || !types.Add(saveData.Type))
                {
                    return false;
                }

                switch (saveData.Type)
                {
                    case PassiveEffectPayloadType.DamageOverTime:
                        if (saveData.RemainingTotalDamage < 0) return false;
                        payloads.Add(new DamageOverTimePayload
                        {
                            RemainingTotalDamage = saveData.RemainingTotalDamage,
                            DamagePerTurn = saveData.DamagePerTurn
                        });
                        break;
                    case PassiveEffectPayloadType.SleepHealing:
                        if (saveData.MaxHealthPercent < 0f) return false;
                        payloads.Add(new SleepHealingPayload
                        {
                            MaxHealthPercent = saveData.MaxHealthPercent
                        });
                        break;
                    case PassiveEffectPayloadType.UniqueCombatBuff:
                        if (!Enum.IsDefined(typeof(UniqueBuffType), saveData.UniqueBuffType)) return false;
                        payloads.Add(new UniqueCombatBuffPayload
                        {
                            BuffType = saveData.UniqueBuffType,
                            ActionPointModifier = saveData.ActionPointModifier
                        });
                        break;
                    case PassiveEffectPayloadType.ResourceRegenerationBlock:
                        payloads.Add(new ResourceRegenerationBlockPayload());
                        break;
                    case PassiveEffectPayloadType.DamageRedirect:
                        if (saveData.DamageRedirectPercent < 0f
                            || saveData.DamageRedirectPercent > 1f) return false;
                        payloads.Add(new DamageRedirectPayload
                        {
                            RedirectPercent = saveData.DamageRedirectPercent,
                            ProtectorCharacterId = saveData.ProtectorCharacterId ?? string.Empty,
                            ProtectorDisplayName = saveData.ProtectorDisplayName ?? string.Empty,
                            ProtectedCharacterId = saveData.ProtectedCharacterId ?? string.Empty,
                            ProtectedCharacterDisplayName = saveData.ProtectedCharacterDisplayName ?? string.Empty
                        });
                        break;
                    case PassiveEffectPayloadType.OnHitStatus:
                        if (saveData.OnHitStatusChance < 0f
                            || saveData.OnHitStatusChance > 1f
                            || saveData.OnHitStatusDurationTurns < 0
                            || saveData.OnHitStatusDamageMultiplier < 0f
                            || saveData.OnHitStatusDamagePerTurn < 0) return false;
                        payloads.Add(new OnHitStatusPayload
                        {
                            StatusId = saveData.OnHitStatusId ?? string.Empty,
                            Chance = saveData.OnHitStatusChance,
                            DurationTurns = saveData.OnHitStatusDurationTurns,
                            StatusDamageMultiplier = saveData.OnHitStatusDamageMultiplier,
                            StatusDamagePerTurn = saveData.OnHitStatusDamagePerTurn
                        });
                        break;
                    case PassiveEffectPayloadType.Counterattack:
                        return false;
                    case PassiveEffectPayloadType.StatModifiers:
                        if (!TryCreateStatModifiers(saveData.StatModifiers, out var statModifiers)) return false;
                        payloads.Add(new StatModifiersPayload { Modifiers = statModifiers });
                        break;
                    case PassiveEffectPayloadType.VariableModifiers:
                        if (!TryCreateVariableModifiers(saveData.VariableModifiers, out var variableModifiers)) return false;
                        payloads.Add(new VariableModifiersPayload { Modifiers = variableModifiers });
                        break;
                    case PassiveEffectPayloadType.ResistanceModifier:
                        if (!Enum.IsDefined(typeof(ResistanceType), saveData.ResistanceType)
                            || float.IsNaN(saveData.ResistanceModifierValue)
                            || float.IsInfinity(saveData.ResistanceModifierValue))
                        {
                            return false;
                        }
                        payloads.Add(new ResistanceModifierPayload
                        {
                            ResistanceType = saveData.ResistanceType,
                            Value = saveData.ResistanceModifierValue
                        });
                        break;
                    case PassiveEffectPayloadType.PeriodicRecovery:
                        if (saveData.HealthPercent < 0f
                            || saveData.HealthPercent > 1f
                            || saveData.ResourcePercent < 0f
                            || saveData.ResourcePercent > 1f)
                        {
                            return false;
                        }
                        payloads.Add(new PeriodicRecoveryPayload
                        {
                            HealthPercent = saveData.HealthPercent,
                            ResourcePercent = saveData.ResourcePercent
                        });
                        break;
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
                        || node.Ranks <= 0
                        || !nodeDefinitionsByTreeId[tree.TreeId].TryGetValue(
                            node.NodeId,
                            out var nodeDefinition))
                    {
                        continue;
                    }

                    var ranks = Math.Min(node.Ranks, nodeDefinition.EffectiveMaxRanks);
                    if (!aggregatedRanks.TryGetValue(
                            node.NodeId,
                            out var existingRanks)
                        || ranks > existingRanks)
                    {
                        aggregatedRanks[node.NodeId] = ranks;
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
                // Restore must reproduce the saved party composition, including
                // incapacitated or dead members.  The gameplay API deliberately
                // prevents adding such characters to an active party, but using it
                // here made otherwise valid saves unloadable after a party member
                // died.
                if (!session.CharactersById.ContainsKey(characterId)
                    || !session.PlayerParty.TryAddActiveMember(characterId))
                {
                    return false;
                }
            }

            foreach (var characterId in saveData.BenchMemberIds)
            {
                if (!session.CharactersById.ContainsKey(characterId)
                    || !session.PlayerParty.TryAddBenchMember(characterId))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(saveData.ActiveLeaderId))
            {
                return string.IsNullOrWhiteSpace(
                    session.PlayerParty.ActiveLeaderId);
            }

            return session.PlayerParty.TrySetActiveLeader(
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
                    || npcSaveData.LoyaltyLevel < npcLoyaltyService.InitialLevel
                    || npcSaveData.LoyaltyLevel > npcLoyaltyService.MaximumLevel
                    || npcSaveData.SpentGoldTotal < 0
                    || npcSaveData.GoldBalance < 0
                    || npcSaveData.TotalGoldReceived < 0
                    || npcSaveData.TotalGoldSpent < 0
                    || float.IsNaN(npcSaveData.GuildPayoutProgress)
                    || float.IsInfinity(npcSaveData.GuildPayoutProgress)
                    || npcSaveData.GuildPayoutProgress < 0f
                    || npcSaveData.GuildPayoutProgress >= 1f
                    || !contentCatalog.TryGetNpc(
                        npcSaveData.NpcId,
                        out var definition))
                {
                    return false;
                }

                var npc = npcFactory.Create(definition);

                npc.LoyaltyLevel = npcSaveData.LoyaltyLevel;
                npc.SpentGoldTotal = npcSaveData.SpentGoldTotal;
                npc.GoldBalance = npcSaveData.GoldBalance;
                npc.TotalGoldReceived = npcSaveData.TotalGoldReceived;
                npc.TotalGoldSpent = npcSaveData.TotalGoldSpent;
                npc.GuildPayoutProgress = npcSaveData.GuildPayoutProgress;

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
            GameContentCatalog contentCatalog,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (saveData == null
                || string.IsNullOrWhiteSpace(saveData.DungeonId))
            {
                return true;
            }

            if (!contentCatalog.TryGetDungeon(
                    saveData.DungeonId,
                    out var definition)
                || !definition.IsProcedural)
            {
                return false;
            }

            DungeonInstance dungeon;

            try
            {
                if (saveData.LayoutVersion != 0)
                {
                    if (!DungeonLayoutSnapshot.TryRestore(saveData, definition, contentCatalog, out dungeon))
                        return false;
                }
                else
                {
                    // Legacy saves have no generated layout. The next snapshot upgrades them.
                    if (saveData.Layout != null && saveData.Layout.Count > 0) return false;
                    dungeon = dungeonGenerator.Generate(
                        definition,
                        saveData.Seed,
                        saveData.DungeonLevel > 0
                            ? saveData.DungeonLevel
                            : definition.DungeonLevel);

                    encounterPlacementSystem.Place(dungeon);
                    objectPlacementSystem.PlaceLegacySave(dungeon, contentCatalog.EquipmentPartDefinitions);
                    rewardPreparationSystem.Prepare(dungeon, contentCatalog.EquipmentPartDefinitions);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }

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
            {
                Debug.LogError(
                    $"Nie udało się odtworzyć stanu lochu "
                    + $"'{saveData.DungeonId}': nieprawidłowe piętro "
                    + $"{saveData.CurrentFloorIndex}.");
                return false;
            }

            if (!TryRestoreDungeonFloorDeltas(
                    dungeon,
                    saveData.FloorDeltas,
                    saveData.EncounterDeltas,
                    itemsById,
                    assignedItemIds))
            {
                Debug.LogError(
                    $"Nie udało się odtworzyć zmian obiektów/encounterów "
                    + $"w lochu '{saveData.DungeonId}'.");
                return false;
            }

            if (!session.TrySetActiveDungeon(dungeon))
            {
                Debug.LogError(
                    $"Nie udało się ustawić aktywnego lochu "
                    + $"'{saveData.DungeonId}' w sesji.");
                return false;
            }

            return true;
        }

        private static bool TryRestoreDungeonFloorDeltas(
            DungeonInstance dungeon,
            List<DungeonFloorDeltaSaveData> floorDeltas,
            List<DungeonEncounterDeltaSaveData> encounterDeltas,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (dungeon == null)
            {
                return false;
            }

            if (floorDeltas != null)
            {
                var restoredFloorIndices = new HashSet<int>();

                foreach (var floorDelta in floorDeltas)
                {
                    if (floorDelta == null
                        || !restoredFloorIndices.Add(floorDelta.FloorIndex)
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
                            if (!floor.TryRestoreRoomVisited(roomId, true))
                            {
                                return false;
                            }
                        }
                    }

                    if (floorDelta.ObjectDeltas != null)
                    {
                        foreach (var objectDelta in floorDelta.ObjectDeltas)
                        {
                            if (objectDelta == null
                                || !floor.TryRestoreObjectState(
                                    objectDelta.ObjectInstanceId,
                                    objectDelta.IsActive,
                                    objectDelta.IsDoor,
                                    objectDelta.IsOpen,
                                    objectDelta.IsTriggered,
                                    objectDelta.IsActivated,
                                    objectDelta.IsSatisfied,
                                    objectDelta.IsChest,
                                    objectDelta.IsUnlocked))
                            {
                                return false;
                            }

                            if (objectDelta.IsChest
                                && objectDelta.HasChestLootState)
                            {
                                var restoredChestItems = new List<ItemInstance>();

                                foreach (var itemInstanceId in
                                         objectDelta.ChestItemInstanceIds
                                         ?? new List<string>())
                                {
                                    if (!TryTakeItem(
                                            itemInstanceId,
                                            itemsById,
                                            assignedItemIds,
                                            out var item))
                                    {
                                        return false;
                                    }

                                    restoredChestItems.Add(item);
                                }

                                if (!floor.TryRestoreChestItems(
                                        objectDelta.ObjectInstanceId,
                                        restoredChestItems))
                                {
                                    return false;
                                }
                            }
                        }
                    }

                    if (floorDelta.ProgressionGateDeltas != null)
                    {
                        var restoredGateIds = new HashSet<string>(
                            StringComparer.Ordinal);

                        foreach (var gateDelta in floorDelta.ProgressionGateDeltas)
                        {
                            if (gateDelta == null
                                || string.IsNullOrWhiteSpace(gateDelta.GateId)
                                || !restoredGateIds.Add(gateDelta.GateId)
                                || !floor.TryRestoreProgressionGateState(
                                    gateDelta.GateId,
                                    gateDelta.IsUnlocked))
                            {
                                return false;
                            }
                        }
                    }

                    if (!TryRestoreEnemyBodyDeltas(
                            floor,
                            floorDelta.EnemyBodyDeltas,
                            itemsById,
                            assignedItemIds))
                    {
                        return false;
                    }
                }
            }

            if (encounterDeltas == null)
            {
                return true;
            }

            foreach (var encounterDelta in encounterDeltas)
            {
                if (encounterDelta == null
                    || string.IsNullOrWhiteSpace(
                        encounterDelta.EncounterInstanceId))
                {
                    return false;
                }

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
                            encounterDelta.DefeatedEnemyInstanceIds)
                        || !floor.TryGetEncounter(
                            encounterDelta.EncounterInstanceId,
                            out var restoredEncounter)
                        || !restoredEncounter
                            .TryRestoreGeneratedEnemyBodyEnemyInstanceIds(
                                encounterDelta
                                    .GeneratedEnemyBodyEnemyInstanceIds))
                    {
                        return false;
                    }

                    if (encounterDelta.HasPreparedRewards)
                    {
                        if (encounterDelta.Rewards == null) return false;
                        var rewards = new CombatRewardResult();
                        var rewardIds = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var savedReward in encounterDelta.Rewards)
                        {
                            if (savedReward == null || savedReward.Gold < 0 || savedReward.Experience < 0
                                || savedReward.ItemIds == null || !rewardIds.Add(savedReward.EnemyId ?? string.Empty)) return false;
                            var rewardItems = new List<ItemInstance>();
                            foreach (var itemId in savedReward.ItemIds)
                            {
                                if (!TryTakeItem(itemId, itemsById, assignedItemIds, out var item)) return false;
                                rewardItems.Add(item);
                            }
                            if (string.IsNullOrEmpty(savedReward.EnemyId))
                            {
                                if (encounterDelta.Rewards.Count != 1) return false;
                                rewards.Gold = savedReward.Gold;
                                rewards.ExperiencePerActiveCharacter = savedReward.Experience;
                                rewards.Items = rewardItems;
                            }
                            else
                            {
                                if (restoredEncounter.IsEnemyDefeated(savedReward.EnemyId)
                                    || restoredEncounter.IsEnemyBodyGenerated(savedReward.EnemyId)) return false;
                                rewards.EnemyRewards.Add(new CombatEnemyReward
                                {
                                    EnemyInstanceId = savedReward.EnemyId, Gold = savedReward.Gold,
                                    ExperiencePerActiveCharacter = savedReward.Experience, Items = rewardItems
                                });
                            }
                        }
                        if (!dungeon.TryStorePreRolledRewards(encounterDelta.EncounterInstanceId, rewards)) return false;
                    }

                    restored = true;
                    break;
                }

                if (!restored)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryRestoreEnemyBodyDeltas(
            DungeonFloorInstance floor,
            List<DungeonEnemyBodySaveData> bodyDeltas,
            Dictionary<string, ItemInstance> itemsById,
            HashSet<string> assignedItemIds)
        {
            if (bodyDeltas == null) return true;

            var restoredBodyIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var bodyDelta in bodyDeltas)
            {
                if (bodyDelta == null
                    || string.IsNullOrWhiteSpace(bodyDelta.BodyInstanceId)
                    || string.IsNullOrWhiteSpace(bodyDelta.EnemyInstanceId)
                    || bodyDelta.Gold < 0
                    || !restoredBodyIds.Add(bodyDelta.BodyInstanceId)
                    || bodyDelta.ItemInstanceIds == null)
                {
                    return false;
                }

                var restoredItems = new List<ItemInstance>();

                foreach (var itemInstanceId in bodyDelta.ItemInstanceIds)
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

                var body = new DungeonEnemyBodyInstance(
                    bodyDelta.BodyInstanceId,
                    bodyDelta.EnemyInstanceId,
                    new Vector2Int(
                        bodyDelta.PositionX,
                        bodyDelta.PositionY),
                    bodyDelta.Gold,
                    restoredItems);

                if (!floor.TryAddEnemyBody(body))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCreatePassiveEffectSources(
            List<PassiveEffectSourceSaveData> saveDataList,
            out List<PassiveEffectSourceRuntime> sources)
        {
            sources = new List<PassiveEffectSourceRuntime>();

            if (saveDataList == null)
            {
                return true;
            }

            foreach (var saveData in saveDataList)
            {
                if (saveData == null
                    || !Enum.IsDefined(
                        typeof(PassiveEffectSourceType),
                        saveData.SourceType))
                {
                    return false;
                }

                sources.Add(new PassiveEffectSourceRuntime
                {
                    SourceType = saveData.SourceType,
                    SourceId = saveData.SourceId ?? string.Empty,
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
            out List<GameplayVariableModifier> modifiers)
        {
            modifiers = new List<GameplayVariableModifier>();

            if (saveDataList == null)
            {
                return true;
            }

            foreach (var saveData in saveDataList)
            {
                if (saveData == null
                    || !Enum.IsDefined(
                        typeof(GameplayVariableType),
                        saveData.VariableType)
                    || !Enum.IsDefined(
                        typeof(StatModifierStacking),
                        saveData.Stacking))
                {
                    return false;
                }

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
