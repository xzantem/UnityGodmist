# Godmist - Architecture Sketch

## Goal

This document proposes a first-pass Unity architecture for Godmist based on the clarified gameplay context.

Core rule:

- `ScriptableObject` = static game definitions
- runtime C# classes = game state and game logic
- `MonoBehaviour` = Unity scene/view/input integration
- save DTO = serialized campaign state
- services/managers = orchestration and cross-system coordination

## High-Level Layers

### 1. Definitions Layer (`ScriptableObject`)

Static authoring data edited in Unity.

Main definitions:

- `CharacterDefinition`
- `CharacterClassData`
- `SkillData`
- `PassiveEffectData`
- `ItemDefinition`
- `NpcDefinition`
- `QuestDefinition`
- `DungeonDefinition`
- `DungeonThemeData`
- `EnemyDefinition`
- `PartDefinition`

Optional/supporting definitions:

- `TargetTypeData`
- `RoomTemplateData`
- `LootTableData`
- `CraftingRecipeData`
- `DialogueDefinition`
- `AudioCueData`

### 2. Runtime Domain Layer (plain C#)

Pure game state and logic, independent of Unity scene components.

Main runtime aggregates:

- `GameSession`
- `WorldState`
- `Party`
- `CharacterInstance`
- `BattleUnit`
- `CombatSession`
- `DungeonInstance`
- `QuestInstance`
- `NpcInstance`
- `ItemInstance`

### 3. Scene/View Layer (`MonoBehaviour`)

Unity-facing scene objects and UI presenters.

Examples:

- `CharacterView`
- `NpcView`
- `DungeonObjectView`
- `CombatGridView`
- `ProjectileView`
- `InteractionTrigger`
- `DialogueWindow`
- `InventoryWindow`
- `CombatHudView`

### 4. Save Layer (DTOs)

Serialized state for campaigns and settings.

Examples:

- `GameSaveData`
- `CharacterSaveData`
- `ItemSaveData`
- `QuestSaveData`
- `WorldSaveData`
- `DungeonSaveData`
- `NpcSaveData`
- `SettingsSaveData`

### 5. Services / Managers

Small orchestration services with one clear responsibility each.

Examples:

- `SaveService`
- `CombatService`
- `DungeonGeneratorService`
- `QuestService`
- `InventoryService`
- `EquipmentService`
- `CraftingService`
- `TradeService`
- `DialogueService`
- `NpcLoyaltyService`
- `WorldStateService`
- `GameModeService`

## Core Runtime Model

### `GameSession`

Top-level runtime root for one loaded campaign.

Suggested fields:

- `WorldState WorldState`
- `Party PlayerParty`
- `Dictionary<string, CharacterInstance> CharactersById`
- `Dictionary<string, NpcInstance> NpcsById`
- `Dictionary<string, QuestInstance> QuestsById`
- `DungeonInstance? ActiveDungeon`
- `CombatSession? ActiveCombat`
- `string CurrentLocationId`
- `GameMode CurrentMode`

### `WorldState`

Global non-character progression and world flags.

Suggested fields:

- `HashSet<string> UnlockedDungeonIds`
- `HashSet<string> CompletedQuestIds`
- `Dictionary<string, bool> Flags`
- `Dictionary<string, int> Counters`
- `string? EndingId`

### `Party`

Tracks player-controlled roster and active members.

Suggested fields:

- `List<string> ActiveMemberIds`
- `List<string> BenchMemberIds`
- `string ActiveLeaderId`
- `int MaxActiveMembers`

## Character Model

### `CharacterDefinition : ScriptableObject`

Static identity/config for a specific authored character.

Suggested fields:

- `string CharacterId`
- `string DisplayName`
- `CharacterClassData ClassData`
- `Sprite Portrait`
- `GameObject CharacterPrefab`
- `bool IsMainCharacter`
- `string? RecruitmentQuestId`
- `List<SkillData> StartingSkills`
- `CharacterStatsBlock BaseStats`

### `CharacterClassData : ScriptableObject`

Static class setup.

Suggested fields:

- `string ClassId`
- `string ClassName`
- `ResourceType ResourceType`
- `CharacterStatsBlock BaseStats`
- `CharacterGrowthData GrowthData`
- `List<SkillTreeDefinition> SkillTrees`
- `List<SkillData> StartingSkills`
- `EquipmentArchetypeData EquipmentArchetype`

### `CharacterInstance`

Main runtime character state.

Suggested fields:

- `string CharacterId`
- `CharacterDefinition Definition`
- `CharacterClassData ClassData`
- `int Level`
- `int CurrentExperience`
- `int AvailableSkillPoints`
- `int CurrentHealth`
- `ResourcePool ResourcePool`
- `EquipmentSet Equipment`
- `Inventory Inventory`
- `List<SkillRuntimeState> SkillStates`
- `List<PassiveEffectRuntime> PassiveEffects`
- `CharacterLifeState LifeState`
- `CharacterAvailabilityState AvailabilityState`
- `int Honor`
- `ResistanceSet Resistances`
- `ShieldPool ShieldPool`

Suggested responsibilities:

- own persistent character runtime state
- expose derived stats through stat/effect services
- hold passive and timed effects
- hold inventory/equipment references

### `BattleUnit`

Combat-only wrapper over `CharacterInstance`.

Suggested fields:

- `CharacterInstance Character`
- `int CurrentActionPoints`
- `float CurrentActionValue`
- `GridPosition Position`
- `Dictionary<string, TeamRelation> TeamRelations`
- `bool HasReacted`
- `bool IsReadyToEscape`

Suggested responsibilities:

- combat-only wrapper state
- no permanent ownership of long-term character data

### `ResourcePool`

Shared resource runtime object.

Suggested fields:

- `ResourceType ResourceType`
- `int CurrentValue`
- `int MaxValue`
- `int Regeneration`

Behavior should be delegated to:

- `IResourceRule`
- `ResourceRuleRegistry`

### `ShieldPool`

Suggested fields:

- `List<ShieldLayer> Layers`

Suggested responsibilities:

- expose total shield amount
- consume incoming damage from the end of the layer list

### `ResistanceSet`

Suggested fields:

- `float BleedResistance`
- `float PoisonResistance`
- `float FireResistance`
- `float WeakeningResistance`
- `float StunResistance`
- `float ImmobilizeResistance`
- `float SleepResistance`
- `float ParalysisResistance`
- `float KnockbackResistance`
- `float ChargeResistance`
- `float PullResistance`
- `float RetreatResistance`

## Skills / Effects / Statuses

### `SkillData : ScriptableObject`

Suggested fields:

- `string SkillId`
- `string Name`
- `string Description`
- `Sprite Icon`
- `int ActionCost`
- `int ResourceCost`
- `List<SkillTargetModeData> TargetModes`
- `SkillExecutionGraphData ExecutionGraph`
- `bool IsToggle`
- `SkillUsageRequirementsData Requirements`

### `PassiveEffectData`

Base authoring definition for passive/modifier behavior.

Suggested subtypes:

- `PassiveEffectData`
- `TimedPassiveEffectData`
- `ConditionalPassiveEffectData`
- `TriggeredPassiveEffectData`

### `PassiveEffectRuntime`

Runtime applied effect.

Suggested fields:

- `string EffectId`
- `PassiveEffectData SourceDefinition`
- `string? SourceEntityId`
- `int? RemainingDuration`
- `int? Stacks`
- `Dictionary<string, float> Payload`

### Skill execution model

Recommended structure:

- `SkillExecutionGraphData`
- `SkillNodeData`
- `SkillConditionData`
- `SkillEffectData`

This fits the chosen "condition + effect nodes" approach better than a single flat effect list.

### `SkillRuntimeState`

Suggested fields:

- `SkillData Skill`
- `bool IsUnlocked`
- `bool IsEquipped`
- `bool IsToggledOn`
- `Dictionary<string, float> RuntimeCounters`

## Items / Equipment

### `ItemDefinition : ScriptableObject`

Suggested fields:

- `string ItemId`
- `string Name`
- `string Description`
- `Sprite Icon`
- `ItemType ItemType`
- `RarityTier Rarity`
- `int BaseValue`
- `float Weight`
- `bool IsStackable`

### `ItemInstance`

Suggested fields:

- `string InstanceId`
- `ItemDefinition Definition`
- `RarityTier CurrentRarity`
- `ItemQuality? Quality`
- `int ItemLevel`
- `List<string> PartIds`
- `List<GalduryteInstance> SocketedGaldurytes`
- `int UpgradePercent`
- `int SocketCapacity`
- `Dictionary<string, float> RolledStats`
- `Dictionary<string, float> ExtraVars`

### `Inventory`

Suggested fields:

- `List<ItemInstance> Items`
- `float CurrentWeight`
- `float? MaxWeight`

### `ShopInventory : Inventory`

Suggested additional fields:

- `List<ItemInstance> RotatingStock`
- `List<ItemInstance> BuybackStock`
- `DateTime? LastRefreshTime`

### `EquipmentSet`

Suggested fields:

- `ItemInstance? Weapon`
- `ItemInstance? Armor`
- `ItemInstance? Necklace`
- `ItemInstance? Ring`
- `ItemInstance? Bracelet`
- `ItemInstance? Earrings`

### `GalduryteInstance`

Suggested fields:

- `string InstanceId`
- `RarityTier Rarity`
- `int ItemLevel`
- `bool IsIdentified`
- `List<GalduryteEffectRoll> Effects`

Note:

- no strong fixed definition is required if galdurytes are inherently random

### `PartDefinition : ScriptableObject`

Suggested fields:

- `string PartId`
- `PartKind PartKind`
- `CharacterClassType ClassRestriction`
- `int TierLevel`
- `MaterialType MaterialType`
- `Dictionary<string, float> BaseStatContributions`
- `Dictionary<string, float> ModifierPercents`

## NPCs / Trade / Crafting

### `NpcDefinition : ScriptableObject`

Suggested fields:

- `string NpcId`
- `string DisplayName`
- `Sprite Portrait`
- `GameObject Prefab`
- `DialogueDefinition BaseDialogue`
- `List<NpcRoleData> Roles`

### `NpcInstance`

Suggested fields:

- `string NpcId`
- `int LoyaltyLevel`
- `float LoyaltyProgressGold`
- `ShopInventory? Shop`
- `List<string> AvailableQuestIds`
- `Dictionary<string, bool> LocalFlags`

### Role data

Recommended modular role definitions:

- `TraderRoleData`
- `BlacksmithRoleData`
- `RuneMakerRoleData`
- `AlchemistRoleData`
- `QuestGiverRoleData`

### `CraftingService`

Use one common crafting facade with specialized strategies.

Recommended internals:

- `WeaponCraftStrategy`
- `ArmorCraftStrategy`
- `PotionCraftStrategy`
- `GalduryteCraftStrategy`
- `JewelryCraftStrategy`

## Quests / Story

### `QuestDefinition : ScriptableObject`

Suggested fields:

- `string QuestId`
- `string Name`
- `string Description`
- `QuestType QuestType`
- `List<QuestStageDefinition> Stages`
- `List<QuestRequirementData> Requirements`
- `List<QuestRewardData> Rewards`

### `QuestInstance`

Suggested fields:

- `string QuestId`
- `QuestState State`
- `int CurrentStageIndex`
- `Dictionary<string, float> ObjectiveProgress`
- `Dictionary<string, bool> QuestFlags`
- `List<string> ChosenBranches`

### `QuestStageDefinition`

Suggested fields:

- `string StageId`
- `List<QuestObjectiveDefinition> Objectives`
- `List<QuestRewardData> StageRewards`

## Dungeons / Exploration / Combat

### `DungeonDefinition : ScriptableObject`

Suggested fields:

- `string DungeonId`
- `DungeonThemeData Theme`
- `bool IsProcedural`
- `bool IsStoryDungeon`
- `DungeonGenerationRulesData Rules`
- `List<GuaranteedRoomRuleData> GuaranteedRooms`

### `DungeonInstance`

Suggested fields:

- `string DungeonId`
- `int Seed`
- `List<DungeonFloorInstance> Floors`
- `int CurrentFloorIndex`
- `GridPosition PlayerPosition`
- `HashSet<string> VisitedRoomIds`
- `HashSet<string> ClearedEncounterIds`
- `List<LootRecord> LootHistory`
- `Dictionary<string, bool> ObjectStateFlags`

### `DungeonFloorInstance`

Suggested fields:

- `int FloorIndex`
- `List<DungeonRoomInstance> Rooms`

### `DungeonRoomInstance`

Suggested fields:

- `string RoomId`
- `RoomType RoomType`
- `RectInt Bounds`
- `List<DungeonObjectState> Objects`
- `List<EncounterState> Encounters`

### `CombatSession`

Suggested fields:

- `string CombatId`
- `List<BattleUnit> Units`
- `BattleGrid Grid`
- `Queue<ScheduledCombatEvent> ScheduledEvents`
- `CombatOutcomeState Outcome`

### `BattleGrid`

Suggested fields:

- `int Width`
- `int Height`
- `Dictionary<GridPosition, GridCellState> Cells`

## Save DTOs

Suggested main DTO tree:

- `GameSaveData`
  - `SaveMetadataData`
  - `WorldSaveData`
  - `PartySaveData`
  - `List<CharacterSaveData>`
  - `List<NpcSaveData>`
  - `List<QuestSaveData>`
  - `DungeonSaveData?`
  - `StashSaveData`

Important rule:

- save stores IDs and runtime values
- definitions are always restored from `ScriptableObject` registries by ID

## Recommended Services

### Core

- `GameModeService`
- `SaveService`
- `DefinitionRegistry`
- `EventBus`

### Characters / Party

- `CharacterService`
- `PartyService`
- `CharacterStatService`
- `ResourceService`
- `PassiveEffectService`

### Combat

- `CombatService`
- `TurnOrderService`
- `TargetingService`
- `CombatResolutionService`
- `EscapeService`

### Items

- `InventoryService`
- `EquipmentService`
- `LootService`
- `UpgradeService`
- `InfusionService`
- `ReforgeService`
- `IdentificationService`

### NPC / Trade / Crafting

- `NpcService`
- `TradeService`
- `NpcLoyaltyService`
- `CraftingService`

### Quests / World

- `QuestService`
- `WorldStateService`
- `DialogueService`

### Dungeons

- `DungeonGeneratorService`
- `DungeonService`
- `EncounterService`

## Recommended Scene `MonoBehaviour` Components

### World / Dungeon

- `GameBootstrap`
- `DungeonSceneController`
- `ArungardSceneController`
- `CharacterView`
- `NpcView`
- `DungeonObjectView`
- `InteractionTrigger`
- `CameraFollowController`

### Combat

- `CombatGridView`
- `BattleUnitView`
- `CombatHudPresenter`
- `TargetingOverlayView`

### UI

- `UiRoot`
- `WindowManagerView`
- `InventoryWindowView`
- `EquipmentWindowView`
- `ShopWindowView`
- `CraftingWindowView`
- `QuestJournalView`
- `DialogueWindowView`
- `PartyManagementView`
- `SaveLoadWindowView`

## Event-Driven Backbone

Suggested event examples:

- `CharacterDamaged`
- `CharacterKilled`
- `CharacterDowned`
- `SkillUsed`
- `ItemObtained`
- `ItemEquipped`
- `QuestStarted`
- `QuestStageAdvanced`
- `QuestCompleted`
- `NpcTalkedTo`
- `DungeonEntered`
- `DungeonCompleted`
- `CombatStarted`
- `CombatEnded`

This fits your preferred event-driven architecture and keeps systems decoupled.

## Minimal First-Cut Build Order

If we later turn this into implementation work, the safest order is:

1. definitions + registries
2. save DTOs
3. character/item runtime model
4. inventory/equipment/passive effects
5. combat session + battle unit + targeting
6. quest/world state
7. dungeon generation/runtime
8. NPC/trade/crafting
9. UI integration

