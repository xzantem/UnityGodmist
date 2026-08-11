using System;
using System.Collections.Generic;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;

namespace Godmist.Systems.SaveLoad
{
    public class GameSessionSaveSnapshotFactory
    {
        public GameSaveData CreateSnapshot(
            GameSession session,
            string displayName)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (session.ActiveCombat != null)
            {
                throw new InvalidOperationException(
                    "Nie mozna utworzyc zapisu podczas aktywnej walki.");
            }

            var saveData = GameSaveData.CreateNew(displayName, session.CampaignId);

            if (!TryCaptureInto(saveData, session))
            {
                throw new InvalidOperationException(
                    "Nie udalo sie utworzyc snapshotu GameSession.");
            }

            return saveData;
        }

        public bool TryCaptureInto(
            GameSaveData saveData,
            GameSession session)
        {
            if (saveData == null
                || session == null
                || session.ActiveCombat != null
                || string.IsNullOrWhiteSpace(saveData.SaveId))
            {
                return false;
            }

            ClearRuntimePayload(saveData);

            saveData.SaveVersion = GameSaveData.CurrentSaveVersion;
            saveData.CampaignId = session.CampaignId;
            saveData.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
            saveData.Gold = session.Gold;
            saveData.TrackedQuestId = session.TrackedQuestId;
            saveData.HasExplorationPosition = session.HasExplorationPosition;
            saveData.ExplorationPositionX = session.ExplorationPosition.x;
            saveData.ExplorationPositionY = session.ExplorationPosition.y;

            CopyParty(session.PlayerParty, saveData.Party);

            var capturedItemIds = new HashSet<string>(StringComparer.Ordinal);
            var characterIds = new List<string>(session.CharactersById.Keys);
            characterIds.Sort(StringComparer.Ordinal);

            foreach (var characterId in characterIds)
            {
                var character = session.CharactersById[characterId];

                if (!TryCaptureCharacter(
                        character,
                        saveData,
                        capturedItemIds,
                        out var characterSaveData))
                {
                    ClearRuntimePayload(saveData);
                    return false;
                }

                saveData.Characters.Add(characterSaveData);
            }

            foreach (var item in session.PendingLoot)
            {
                if (!TryCaptureItem(item, saveData, capturedItemIds))
                {
                    ClearRuntimePayload(saveData);
                    return false;
                }

                saveData.PendingLootItemInstanceIds.Add(item.InstanceId);
            }

            if (!TryCaptureWorldFlags(session, saveData)
                || !TryCaptureQuests(session, saveData)
                || !TryCaptureNpcs(session, saveData, capturedItemIds))
            {
                ClearRuntimePayload(saveData);
                return false;
            }

            if (!TryCaptureActiveDungeon(session, saveData))
            {
                ClearRuntimePayload(saveData);
                return false;
            }

            return true;
        }

        private static void ClearRuntimePayload(GameSaveData saveData)
        {
            saveData.Gold = 0;
            saveData.TrackedQuestId = string.Empty;
            saveData.HasExplorationPosition = false;
            saveData.ExplorationPositionX = 0f;
            saveData.ExplorationPositionY = 0f;
            saveData.Party = new PartySaveData();
            saveData.Characters.Clear();
            saveData.ItemInstances.Clear();
            saveData.PendingLootItemInstanceIds.Clear();
            saveData.WorldFlags.Clear();
            saveData.Quests.Clear();
            saveData.Npcs.Clear();
            saveData.ActiveDungeon = null;
        }

        private static bool TryCaptureWorldFlags(
            GameSession session,
            GameSaveData saveData)
        {
            var flagIds = new List<string>(session.WorldState.FlagsById.Keys);
            flagIds.Sort(StringComparer.Ordinal);

            foreach (var flagId in flagIds)
            {
                saveData.WorldFlags.Add(new KeyValueSaveData
                {
                    Key = flagId,
                    Value = session.WorldState.FlagsById[flagId]
                });
            }

            return true;
        }

        private static bool TryCaptureQuests(
            GameSession session,
            GameSaveData saveData)
        {
            var questIds = new List<string>(session.QuestsById.Keys);
            questIds.Sort(StringComparer.Ordinal);

            foreach (var questId in questIds)
            {
                var quest = session.QuestsById[questId];

                if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    return false;
                }

                var questSaveData = new QuestSaveData
                {
                    QuestId = quest.QuestId,
                    Status = quest.Status,
                    CurrentStageId = quest.CurrentStageId,
                    IsHidden = quest.IsHidden,
                    ChosenBranchIds = new List<string>(quest.ChosenBranchIds),
                    ClaimedRewardKeys = new List<string>(quest.ClaimedRewardKeys)
                };

                foreach (var pair in quest.QuestFlags)
                {
                    questSaveData.QuestFlags.Add(new KeyValueSaveData
                    {
                        Key = pair.Key,
                        Value = pair.Value
                    });
                }

                foreach (var pair in quest.ProgressByObjectiveId)
                {
                    var progress = pair.Value;

                    if (progress == null)
                    {
                        continue;
                    }

                    questSaveData.ObjectiveProgress.Add(
                        new QuestObjectiveProgressSaveData
                        {
                            ObjectiveId = progress.ObjectiveId,
                            CurrentAmount = progress.CurrentAmount,
                            IsCompleted = progress.IsCompleted
                        });
                }

                saveData.Quests.Add(questSaveData);
            }

            return true;
        }

        private static bool TryCaptureNpcs(
            GameSession session,
            GameSaveData saveData,
            HashSet<string> capturedItemIds)
        {
            var npcIds = new List<string>(session.NpcsById.Keys);
            npcIds.Sort(StringComparer.Ordinal);

            foreach (var npcId in npcIds)
            {
                var npc = session.NpcsById[npcId];

                if (npc == null || string.IsNullOrWhiteSpace(npc.NpcId))
                {
                    return false;
                }

                var npcSaveData = new NpcSaveData
                {
                    NpcId = npc.NpcId,
                    LoyaltyLevel = npc.LoyaltyLevel,
                    SpentGoldTotal = npc.SpentGoldTotal
                };

                if (npc.ShopInventory != null)
                {
                    foreach (var item in npc.ShopInventory.Items)
                    {
                        if (!TryCaptureItem(item, saveData, capturedItemIds))
                        {
                            return false;
                        }

                        npcSaveData.ShopItemInstanceIds.Add(item.InstanceId);
                    }

                    foreach (var buybackEntry in npc.ShopInventory.BuybackStock)
                    {
                        if (buybackEntry?.Item == null
                            || !TryCaptureItem(
                                buybackEntry.Item,
                                saveData,
                                capturedItemIds))
                        {
                            return false;
                        }

                        npcSaveData.BuybackEntries.Add(new BuybackEntrySaveData
                        {
                            ItemInstanceId = buybackEntry.Item.InstanceId,
                            SoldPrice = buybackEntry.SoldPrice
                        });
                    }
                }

                saveData.Npcs.Add(npcSaveData);
            }

            return true;
        }

        private static bool TryCaptureActiveDungeon(
            GameSession session,
            GameSaveData saveData)
        {
            if (session.ActiveDungeon == null)
            {
                saveData.ActiveDungeon = null;
                return true;
            }

            var dungeon = session.ActiveDungeon;

            if (string.IsNullOrWhiteSpace(dungeon.DungeonId))
            {
                return false;
            }

            var dungeonSaveData = new DungeonSaveData
            {
                DungeonId = dungeon.DungeonId,
                Seed = dungeon.Seed,
                CurrentFloorIndex = dungeon.CurrentFloorIndex,
                IsCompleted = dungeon.IsCompleted,
                HasPlayerPosition = dungeon.HasPlayerPosition,
                PlayerPositionX = dungeon.PlayerPosition.x,
                PlayerPositionY = dungeon.PlayerPosition.y,
                ClearedEncounterIds = new List<string>(dungeon.ClearedEncounterIds)
            };

            foreach (var floor in dungeon.Floors)
            {
                if (floor == null)
                {
                    return false;
                }

                var floorDelta = new DungeonFloorDeltaSaveData
                {
                    FloorIndex = floor.FloorIndex
                };

                foreach (var room in floor.Rooms)
                {
                    if (room != null && room.IsVisited)
                    {
                        floorDelta.VisitedRoomIds.Add(room.RoomId);
                    }
                }

                foreach (var dungeonObject in floor.Objects)
                {
                    if (dungeonObject == null)
                    {
                        continue;
                    }

                    var objectDelta = new DungeonObjectDeltaSaveData
                    {
                        ObjectInstanceId = dungeonObject.InstanceId,
                        IsActive = dungeonObject.IsActive
                    };

                    if (dungeonObject is DoorDungeonObjectInstance door)
                    {
                        objectDelta.IsDoor = true;
                        objectDelta.IsOpen = door.IsOpen;
                    }

                    floorDelta.ObjectDeltas.Add(objectDelta);
                }

                dungeonSaveData.FloorDeltas.Add(floorDelta);
            }

            saveData.ActiveDungeon = dungeonSaveData;
            return true;
        }

        private static void CopyParty(
            Party party,
            PartySaveData partySaveData)
        {
            partySaveData.MaxActiveMembers = party.MaxActiveMembers;
            partySaveData.ActiveLeaderId = party.ActiveLeaderId;
            partySaveData.ActiveMemberIds = new List<string>(party.ActiveMemberIds);
            partySaveData.BenchMemberIds = new List<string>(party.BenchMemberIds);
        }

        private static bool TryCaptureCharacter(
            CharacterInstance character,
            GameSaveData saveData,
            HashSet<string> capturedItemIds,
            out CharacterSaveData characterSaveData)
        {
            characterSaveData = null;

            if (character == null
                || string.IsNullOrWhiteSpace(character.CharacterId)
                || character.ClassData == null
                || string.IsNullOrWhiteSpace(character.ClassData.Id)
                || character.ResourcePool == null
                || character.Resistances == null)
            {
                return false;
            }

            characterSaveData = new CharacterSaveData
            {
                CharacterId = character.CharacterId,
                ClassId = character.ClassData.Id,
                Level = character.Level,
                CurrentExperience = character.CurrentExperience,
                AvailableSkillPoints = character.AvailableSkillPoints,
                CurrentHealth = character.CurrentHealth,
                Honor = character.Honor,
                WeaponAccuracy = character.WeaponAccuracy,
                LifeState = character.LifeState,
                AvailabilityState = character.AvailabilityState,
                ResourcePool = CopyResourcePool(character.ResourcePool),
                Resistances = CopyResistances(character.Resistances),
                ShieldLayers = CopyShieldLayers(character.ShieldPool),
                SkillIds = CopySkillIds(character),
                PassiveEffects = CopyPassiveEffects(character.PassiveEffects)
            };

            if (!TryCaptureInventory(
                    character.Inventory,
                    saveData,
                    capturedItemIds,
                    characterSaveData.InventoryItemInstanceIds))
            {
                return false;
            }

            if (!TryCaptureEquipment(
                    character.Equipment,
                    saveData,
                    capturedItemIds,
                    characterSaveData.Equipment))
            {
                return false;
            }

            return true;
        }

        private static ResourcePoolSaveData CopyResourcePool(ResourcePool resourcePool)
        {
            return new ResourcePoolSaveData
            {
                ResourceType = resourcePool.ResourceType,
                CurrentValue = resourcePool.CurrentValue,
                MaxValue = resourcePool.MaxValue,
                Regeneration = resourcePool.Regeneration,
                HasStartedTurn = resourcePool.HasStartedTurn,
                HasGainedResourceSinceLastTurn = resourcePool.HasGainedResourceSinceLastTurn,
                FractionalGainRemainder = resourcePool.FractionalGainRemainder
            };
        }

        private static ResistanceSetSaveData CopyResistances(
            ResistanceSet resistances)
        {
            return new ResistanceSetSaveData
            {
                BleedResistance = resistances.BleedResistance,
                PoisonResistance = resistances.PoisonResistance,
                FireResistance = resistances.FireResistance,
                WeakeningResistance = resistances.WeakeningResistance,
                SuppressionResistance = resistances.SuppressionResistance,
                MoveResistance = resistances.MoveResistance
            };
        }

        private static List<ShieldLayerSaveData> CopyShieldLayers(
            ShieldPool shieldPool)
        {
            var shieldLayers = new List<ShieldLayerSaveData>();

            if (shieldPool == null)
            {
                return shieldLayers;
            }

            foreach (var layer in shieldPool.Layers)
            {
                if (layer == null)
                {
                    continue;
                }

                shieldLayers.Add(new ShieldLayerSaveData
                {
                    SourceId = layer.SourceId ?? string.Empty,
                    CurrentValue = layer.CurrentValue
                });
            }

            return shieldLayers;
        }

        private static List<string> CopySkillIds(CharacterInstance character)
        {
            var skillIds = new List<string>();

            if (character.Skills == null)
            {
                return skillIds;
            }

            foreach (var skill in character.Skills)
            {
                if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                {
                    skillIds.Add(skill.SkillId);
                }
            }

            return skillIds;
        }

        private static List<PassiveEffectSaveData> CopyPassiveEffects(
            List<PassiveEffectRuntime> passiveEffects)
        {
            var saveData = new List<PassiveEffectSaveData>();

            if (passiveEffects == null)
            {
                return saveData;
            }

            foreach (var effect in passiveEffects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.EffectId))
                {
                    continue;
                }

                saveData.Add(new PassiveEffectSaveData
                {
                    EffectId = effect.EffectId,
                    SourceEntityIds = effect.SourceEntityIds != null
                        ? new List<string>(effect.SourceEntityIds)
                        : new List<string>(),
                    Stacks = effect.Stacks,
                    IsTimed = effect.IsTimed,
                    TickPhase = effect.TickPhase,
                    RemainingDuration = effect.RemainingDuration,
                    TotalDamage = effect.TotalDamage,
                    StatModifiers = CopyStatModifiers(effect.StatModifiers)
                });
            }

            return saveData;
        }

        private static bool TryCaptureInventory(
            Inventory inventory,
            GameSaveData saveData,
            HashSet<string> capturedItemIds,
            List<string> inventoryItemIds)
        {
            if (inventory?.Items == null)
            {
                return true;
            }

            foreach (var item in inventory.Items)
            {
                if (!TryCaptureItem(item, saveData, capturedItemIds))
                {
                    return false;
                }

                inventoryItemIds.Add(item.InstanceId);
            }

            return true;
        }

        private static bool TryCaptureEquipment(
            EquipmentSet equipment,
            GameSaveData saveData,
            HashSet<string> capturedItemIds,
            EquipmentSetSaveData equipmentSaveData)
        {
            if (equipment == null)
            {
                return true;
            }

            return TryCaptureEquipmentSlot(
                       equipment.Weapon,
                       saveData,
                       capturedItemIds,
                       out equipmentSaveData.WeaponItemInstanceId)
                   && TryCaptureEquipmentSlot(
                       equipment.Armor,
                       saveData,
                       capturedItemIds,
                       out equipmentSaveData.ArmorItemInstanceId)
                   && TryCaptureEquipmentSlot(
                       equipment.Necklace,
                       saveData,
                       capturedItemIds,
                       out equipmentSaveData.NecklaceItemInstanceId)
                   && TryCaptureEquipmentSlot(
                       equipment.Ring,
                       saveData,
                       capturedItemIds,
                       out equipmentSaveData.RingItemInstanceId)
                   && TryCaptureEquipmentSlot(
                       equipment.Bracelet,
                       saveData,
                       capturedItemIds,
                       out equipmentSaveData.BraceletItemInstanceId)
                   && TryCaptureEquipmentSlot(
                       equipment.Earrings,
                       saveData,
                       capturedItemIds,
                       out equipmentSaveData.EarringsItemInstanceId);
        }

        private static bool TryCaptureEquipmentSlot(
            EquipmentItemInstance item,
            GameSaveData saveData,
            HashSet<string> capturedItemIds,
            out string itemInstanceId)
        {
            itemInstanceId = string.Empty;

            if (item == null)
            {
                return true;
            }

            if (!TryCaptureItem(item, saveData, capturedItemIds))
            {
                return false;
            }

            itemInstanceId = item.InstanceId;
            return true;
        }

        private static bool TryCaptureItem(
            ItemInstance item,
            GameSaveData saveData,
            HashSet<string> capturedItemIds)
        {
            if (item == null
                || item.Definition == null
                || string.IsNullOrWhiteSpace(item.InstanceId)
                || string.IsNullOrWhiteSpace(item.Definition.Id))
            {
                return false;
            }

            if (!capturedItemIds.Add(item.InstanceId))
            {
                return true;
            }

            var itemSaveData = new ItemSaveData
            {
                InstanceId = item.InstanceId,
                ItemDefinitionId = item.Definition.Id,
                CurrentRarity = item.CurrentRarity,
                ItemLevel = item.ItemLevel
            };

            if (item is EquipmentItemInstance equipmentItem)
            {
                itemSaveData.IsEquipment = true;
                itemSaveData.RequiredClassId = equipmentItem.RequiredClassId;
                itemSaveData.RequiredLevel = equipmentItem.RequiredLevel;
                itemSaveData.StatModifiers = CopyStatModifiers(
                    equipmentItem.StatModifiers);
            }

            saveData.ItemInstances.Add(itemSaveData);
            return true;
        }

        private static List<StatModifierSaveData> CopyStatModifiers(
            List<StatModifier> modifiers)
        {
            var saveData = new List<StatModifierSaveData>();

            if (modifiers == null)
            {
                return saveData;
            }

            foreach (var modifier in modifiers)
            {
                if (modifier == null)
                {
                    continue;
                }

                saveData.Add(new StatModifierSaveData
                {
                    StatType = modifier.StatType,
                    Operation = modifier.Operation,
                    Stacking = modifier.Stacking,
                    Value = modifier.Value
                });
            }

            return saveData;
        }
    }
}