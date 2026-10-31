using System;
using Godmist.Systems.Combat;
using System.Linq;
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

            if ((session.HasCombat || session.IsDefeated))
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
                || (session.HasCombat || session.IsDefeated)
                || string.IsNullOrWhiteSpace(saveData.SaveId))
            {
                return false;
            }

            ClearRuntimePayload(saveData);

            saveData.SaveVersion = GameSaveData.CurrentSaveVersion;
            saveData.CampaignId = session.CampaignId;
            saveData.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
            saveData.Gold = session.Gold;
            saveData.DungeonOffers = DungeonOfferPersistence.Capture(session.DungeonOffers);
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

            if (!TryCaptureInventory(
                    session.Stash,
                    saveData,
                    capturedItemIds,
                    saveData.StashItemInstanceIds))
            {
                ClearRuntimePayload(saveData);
                return false;
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
                || !TryCaptureQuests(session, saveData, capturedItemIds)
                || !TryCaptureNpcs(session, saveData, capturedItemIds))
            {
                ClearRuntimePayload(saveData);
                return false;
            }

            if (!TryCaptureActiveDungeon(
                    session,
                    saveData,
                    capturedItemIds))
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
            saveData.StashItemInstanceIds.Clear();
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
            GameSaveData saveData,
            HashSet<string> capturedItemIds)
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

                if (quest.BoardContract is { } contract)
                {
                    questSaveData.HasBoardContract=true;
                    questSaveData.BoardContract=new RadiantBoardContractSaveData
                    {
                        ObjectiveType=contract.ObjectiveType,Theme=contract.Theme,Length=contract.Length,
                        Level=contract.Level,RequiredAmount=contract.RequiredAmount,RewardMultiplier=contract.RewardMultiplier,Cycle=contract.Cycle
                    };
                }
                if (quest.RadiantRewardOffer != null)
                {
                    var offer = quest.RadiantRewardOffer;
                    questSaveData.RadiantRewardLevel = offer.Level;
                    questSaveData.RadiantRewardExperience = offer.Experience;
                    questSaveData.RadiantRewardGold = offer.Gold;
                    questSaveData.RadiantRewardHonor = offer.Honor;
                    if (offer.BonusItem != null)
                    {
                        if (!TryCaptureItem(offer.BonusItem, saveData, capturedItemIds))
                            return false;
                        questSaveData.RadiantBonusItemInstanceId = offer.BonusItem.InstanceId;
                    }
                }

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
                    SpentGoldTotal = npc.SpentGoldTotal,
                    GoldBalance = npc.GoldBalance,
                    TotalGoldReceived = npc.TotalGoldReceived,
                    TotalGoldSpent = npc.TotalGoldSpent,
                    RotatingStockItemIds = new System.Collections.Generic.List<string>(npc.RotatingStockItemIds),
                    CraftedStockInstanceIds = new System.Collections.Generic.List<string>(
                        npc.CraftedStockInstanceIds ?? new System.Collections.Generic.List<string>()),
                    GuildPayoutProgress = npc.GuildPayoutProgress
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
            GameSaveData saveData,
            HashSet<string> capturedItemIds)
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
                DungeonLevel = dungeon.DungeonLevel,
                Length = dungeon.Length,
                ItemDropChanceBonus = dungeon.ItemDropChanceBonus,
                LayoutVersion = DungeonLayoutSnapshot.CurrentVersion,
                Layout = DungeonLayoutSnapshot.Capture(dungeon),
                CurrentFloorIndex = dungeon.CurrentFloorIndex,
                HighestReachedFloorIndex = dungeon.HighestReachedFloorIndex,
                GuildProgressCreditedFloorCount = dungeon.GuildProgressCreditedFloorCount,
                TraderStockRefreshed = dungeon.TraderStockRefreshed,
                CampSuppliesIssued = dungeon.CampSuppliesIssued,
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
                    FloorIndex = floor.FloorIndex,
                    Patrols = DungeonPatrolPersistence.Capture(floor),
                    Fog = DungeonFogPersistence.Capture(floor)
                };

                foreach (var pair in floor.EnemyAwareness.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
                    if (pair.Value.Progress > 0f)
                        floorDelta.EnemyAwareness.Add(new DungeonEnemyAwarenessSaveData { EnemyKey = pair.Key, Progress = pair.Value.Progress });
                foreach (var pair in floor.ActorFacings.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
                    floorDelta.ActorFacings.Add(new DungeonActorFacingSaveData { ActorId = pair.Key, X = pair.Value.x, Y = pair.Value.y });

                foreach (var pair in floor.ActorPositions.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
                {
                    var position = pair.Value;
                    if (floor.Map == null || !floor.Map.TryGetTraversalCell(position, out _)) return false;
                    floorDelta.ActorPositions.Add(new DungeonActorPositionSaveData
                    {
                        ActorId = pair.Key, IsOnStairs = position.IsOnStairs,
                        OffsetX = floor.GetActorOffset(pair.Key).x, OffsetY = floor.GetActorOffset(pair.Key).y,
                        X = position.Surface.Cell.x, Y = position.Surface.Cell.y, Level = position.Surface.Level,
                        StairStep = position.StairStep,
                        FirstX = position.Stair.First.Cell.x, FirstY = position.Stair.First.Cell.y, FirstLevel = position.Stair.First.Level,
                        SecondX = position.Stair.Second.Cell.x, SecondY = position.Stair.Second.Cell.y, SecondLevel = position.Stair.Second.Level
                    });
                }

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

                    if (dungeonObject is ChestDungeonObjectInstance chest)
                    {
                        objectDelta.IsChest = true;
                        objectDelta.IsUnlocked = chest.IsUnlocked;
                        objectDelta.HasChestLootState = true;
                        objectDelta.HasChestGoldState = true;
                        objectDelta.ChestGold = chest.Gold;

                        foreach (var item in chest.Items)
                        {
                            if (!TryCaptureItem(
                                    item,
                                    saveData,
                                    capturedItemIds))
                            {
                                ClearRuntimePayload(saveData);
                                return false;
                            }

                            objectDelta.ChestItemInstanceIds.Add(item.InstanceId);
                        }
                    }

                    if (dungeonObject is DoorDungeonObjectInstance door)
                    {
                        objectDelta.IsDoor = true;
                        objectDelta.IsOpen = door.IsOpen;
                    }

                    if (dungeonObject is SpikeTrapDungeonObjectInstance spikeTrap)
                    {
                        objectDelta.IsTriggered = spikeTrap.IsTriggered;
                    }

                    if (dungeonObject is LeverDungeonObjectInstance lever)
                    {
                        objectDelta.IsActivated = lever.IsActivated;
                    }

                    if (dungeonObject is ConditionDungeonObjectInstance condition)
                    {
                        objectDelta.IsSatisfied = condition.IsSatisfied;
                    }

                    if (dungeonObject is ObstacleDungeonObjectInstance obstacle)
                    {
                        objectDelta.HasObstacleDurability = true;
                        objectDelta.ObstacleDestructible = obstacle.IsDestructible;
                        objectDelta.ObstacleMaxHits = obstacle.MaxHits;
                        objectDelta.ObstacleRemainingHits = obstacle.RemainingHits;
                    }
                    floorDelta.ObjectDeltas.Add(objectDelta);
                }

                foreach (var connection in floor.Connections)
                {
                    if (!connection.HasProgressionGate)
                    {
                        continue;
                    }

                    floorDelta.ProgressionGateDeltas.Add(
                        new DungeonProgressionGateDeltaSaveData
                        {
                            GateId = connection.ProgressionGateId,
                            IsUnlocked = connection.IsProgressionGateUnlocked
                        });
                }

                foreach (var body in floor.EnemyBodies)
                {
                    if (body == null
                        || string.IsNullOrWhiteSpace(body.InstanceId)
                        || string.IsNullOrWhiteSpace(body.EnemyInstanceId)
                        || body.Gold < 0)
                    {
                        ClearRuntimePayload(saveData);
                        return false;
                    }

                    var bodyDelta = new DungeonEnemyBodySaveData
                    {
                        BodyInstanceId = body.InstanceId,
                        EnemyInstanceId = body.EnemyInstanceId,
                        PositionX = body.Position.x,
                        PositionY = body.Position.y,
                        Gold = body.Gold
                    };

                    foreach (var item in body.Items)
                    {
                        if (!TryCaptureItem(
                                item,
                                saveData,
                                capturedItemIds))
                        {
                            ClearRuntimePayload(saveData);
                            return false;
                        }

                        bodyDelta.ItemInstanceIds.Add(item.InstanceId);
                    }

                    floorDelta.EnemyBodyDeltas.Add(bodyDelta);
                }

                foreach (var encounter in floor.Encounters)
                {
                    if (encounter == null)
                    {
                        return false;
                    }

                    var encounterDelta = new DungeonEncounterDeltaSaveData
                        {
                            EncounterInstanceId = encounter.InstanceId,
                            DefeatedEnemyInstanceIds = new List<string>(
                                encounter.DefeatedEnemyInstanceIds),
                            GeneratedEnemyBodyEnemyInstanceIds =
                                new List<string>(
                                    encounter.GeneratedEnemyBodyEnemyInstanceIds)
                        };
                    if (dungeon.TryGetPreRolledRewards(encounter.InstanceId, out var rewards))
                    {
                        encounterDelta.HasPreparedRewards = true;
                        if (!dungeon.IsEncounterCleared(encounter.InstanceId))
                        {
                            if (rewards.EnemyRewards.Count == 0)
                            {
                                if (!TryCaptureDungeonReward(encounterDelta, null, rewards.ExperiencePerActiveCharacter,
                                    rewards.Gold, rewards.Items, saveData, capturedItemIds)) return false;
                            }
                            foreach (var reward in rewards.EnemyRewards)
                            {
                                if (encounter.IsEnemyDefeated(reward.EnemyInstanceId)
                                    || encounter.IsEnemyBodyGenerated(reward.EnemyInstanceId)) continue;
                                if (!TryCaptureDungeonReward(encounterDelta, reward.EnemyInstanceId, reward.ExperiencePerActiveCharacter,
                                    reward.Gold, reward.Items, saveData, capturedItemIds, reward.ExperienceLevel)) return false;
                            }
                        }
                    }
                    dungeonSaveData.EncounterDeltas.Add(encounterDelta);
                }

                dungeonSaveData.FloorDeltas.Add(floorDelta);
            }

            saveData.ActiveDungeon = dungeonSaveData;
            return true;
        }

        private static bool TryCaptureDungeonReward(DungeonEncounterDeltaSaveData encounter, string enemyId,
            int experience, int gold, List<ItemInstance> items, GameSaveData saveData, HashSet<string> capturedItemIds, int experienceLevel = 0)
        {
            if (experienceLevel < 0 || experienceLevel > 50) return false;
            if (experience < 0 || gold < 0 || items == null) return false;
            var reward = new DungeonEnemyRewardSaveData { EnemyId = enemyId, Experience = experience, Gold = gold, ExperienceLevel = experienceLevel };
            foreach (var item in items)
            {
                if (!TryCaptureItem(item, saveData, capturedItemIds)) return false;
                reward.ItemIds.Add(item.InstanceId);
            }
            encounter.Rewards.Add(reward);
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
            partySaveData.GroupsVersion = 1;
            partySaveData.Groups = new List<PartyGroupSaveData>();
            for (var i = 0; i < party.GroupCount; i++)
                partySaveData.Groups.Add(new PartyGroupSaveData { MemberIds = new List<string>(party.GetGroup(i)) });
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
                EquipmentHealthCarry = character.EquipmentHealthCarry,
                EquipmentResourceCarry = character.EquipmentResourceCarry,
                EquipmentGenerationCarry = character.EquipmentGenerationCarry,
                Honor = character.Honor,
                WeaponAccuracy = character.WeaponAccuracy,
                LifeState = character.LifeState,
                AvailabilityState = character.AvailabilityState,
                ResourcePool = CopyResourcePool(character.ResourcePool),
                HasPersistentResistances = true,
                Resistances = CopyResistances(character.Resistances),
                ShieldLayers = CopyShieldLayers(character.ShieldPool),
                SkillIds = CopySkillIds(character),
                HasActiveSkillLoadout = character.ActiveSkillLoadout?.IsInitialized == true,
                ActiveSkillIds = CopyActiveSkillIds(character),
                ActiveSkillSlotIds = CopyActiveSkillSlotIds(character),
                ActiveSkillCapacityModifiers = CopyActiveSkillCapacityModifiers(character),
                SkillTreeProgress = CopySkillTreeProgress(character),
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
                    CurrentValue = layer.CurrentValue,
                    RemainingTurns = layer.RemainingTurns
                });
            }

            return shieldLayers;
        }

        private static List<string> CopySkillIds(CharacterInstance character)
        {
            var skillIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (character.Skills == null)
            {
                return skillIds;
            }

            foreach (var skill in character.Skills)
            {
                if (skill != null
                    && !string.IsNullOrWhiteSpace(skill.SkillId)
                    && seen.Add(skill.SkillId))
                {
                    skillIds.Add(skill.SkillId);
                }
            }

            return skillIds;
        }

        private static List<string> CopyActiveSkillIds(
            CharacterInstance character)
        {
            return character.ActiveSkillLoadout?.CopyActiveSkillIds()
                ?? new List<string>();
        }

        private static List<string> CopyActiveSkillSlotIds(
            CharacterInstance character)
        {
            return character.ActiveSkillLoadout?.CopyActiveSkillSlotIds()
                ?? new List<string>();
        }

        private static List<SkillLoadoutCapacityModifierSaveData>
            CopyActiveSkillCapacityModifiers(CharacterInstance character)
        {
            var result = new List<SkillLoadoutCapacityModifierSaveData>();

            foreach (var modifier in character.ActiveSkillLoadout?
                         .CopyCapacityModifiers()
                     ?? new List<SkillLoadoutCapacityModifier>())
            {
                if (modifier == null)
                {
                    continue;
                }

                result.Add(new SkillLoadoutCapacityModifierSaveData
                {
                    SourceId = modifier.SourceId,
                    Amount = modifier.Amount
                });
            }

            return result;
        }

        private static List<SkillTreeProgressSaveData> CopySkillTreeProgress(CharacterInstance character)
        {
            var result = new List<SkillTreeProgressSaveData>();
            foreach (var tree in character.SkillTreeProgress ?? new())
            {
                if (tree == null || string.IsNullOrWhiteSpace(tree.TreeId)) continue;
                var copy = new SkillTreeProgressSaveData { TreeId = tree.TreeId };
                foreach (var node in tree.NodeInvestments ?? new())
                {
                    if (!string.IsNullOrWhiteSpace(node.NodeId) && node.Ranks > 0)
                        copy.NodeInvestments.Add(new SkillTreeNodeInvestmentSaveData { NodeId = node.NodeId, Ranks = node.Ranks });
                }
                result.Add(copy);
            }
            return result;
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
                    DefinitionId = effect.DefinitionId,
                    DisplayName = effect.DisplayName,
                    Sources = CopyPassiveEffectSources(effect.Sources),
                    Stacks = effect.Stacks,
                    StackingRule = effect.StackingRule,
                    Lifetime = effect.Lifetime,
                    TickPhase = effect.TickPhase,
                    RemainingDuration = effect.RemainingDuration,
                    Payloads = CopyPassiveEffectPayloads(effect.Payloads),
                    StateValues = CopyPassiveEffectStateValues(effect.StateValues)
                });
            }

            return saveData;
        }

        private static List<PassiveEffectRuntimeStateValueSaveData>
            CopyPassiveEffectStateValues(
                List<PassiveEffectRuntimeStateValue> stateValues)
        {
            var saveData = new List<PassiveEffectRuntimeStateValueSaveData>();
            if (stateValues == null) return saveData;

            foreach (var stateValue in stateValues)
            {
                if (stateValue == null || string.IsNullOrWhiteSpace(stateValue.Key))
                    continue;

                saveData.Add(new PassiveEffectRuntimeStateValueSaveData
                {
                    Key = stateValue.Key,
                    Type = stateValue.Type,
                    NumberValue = stateValue.NumberValue,
                    IntegerValue = stateValue.IntegerValue,
                    TextValue = stateValue.TextValue,
                    BooleanValue = stateValue.BooleanValue
                });
            }

            return saveData;
        }

        private static List<PassiveEffectPayloadSaveData> CopyPassiveEffectPayloads(List<PassiveEffectPayloadRuntime> payloads)
        {
            var result = new List<PassiveEffectPayloadSaveData>();
            foreach (var payload in payloads ?? new List<PassiveEffectPayloadRuntime>())
                if (payload != null && PassiveEffectPayloadCodec.IsPersistent(payload)) result.Add(PassiveEffectPayloadCodec.Write(payload));
            return result;
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
                ItemLevel = item.ItemLevel,
                CustomName = item.CustomName ?? string.Empty,
                IsSaleProtected = item.IsSaleProtected
            };

            if (item is EquipmentItemInstance equipmentItem)
            {
                itemSaveData.IsEquipment = true;
                itemSaveData.RequiredClassId = equipmentItem.RequiredClassId;
                itemSaveData.RequiredLevel = equipmentItem.RequiredLevel;
                itemSaveData.Quality = equipmentItem.Quality;
                itemSaveData.PartIds = equipmentItem.PartIds == null
                    ? new List<string>()
                    : new List<string>(equipmentItem.PartIds);
                itemSaveData.PrimaryPartDisplayName = equipmentItem.PrimaryPartDisplayName ?? string.Empty;
                itemSaveData.PrimaryMaterialId = equipmentItem.PrimaryMaterialId ?? string.Empty;
                itemSaveData.EquipmentCraftedBaseValue = equipmentItem.CraftedBaseValue;
                itemSaveData.BaseStatModifiers = CopyStatModifiers(
                    equipmentItem.BaseStatModifiers);
                itemSaveData.StatModifiers = CopyStatModifiers(
                    equipmentItem.StatModifiers);
                itemSaveData.UpgradeProgress = equipmentItem.UpgradeProgress;
                foreach (var socketed in equipmentItem.SocketedGaldurytes
                             ?? new List<GalduryteItemInstance>())
                {
                    if (!TryCaptureItem(socketed, saveData, capturedItemIds)) return false;
                    itemSaveData.SocketedGalduryteInstanceIds.Add(socketed.InstanceId);
                }

                if (equipmentItem is JewelryItemInstance jewelryItem)
                {
                    itemSaveData.JewelryMetalId = jewelryItem.MetalId;
                    itemSaveData.JewelryMetalDisplayName = jewelryItem.MetalDisplayName;
                    itemSaveData.JewelryGemId = jewelryItem.GemId;
                    itemSaveData.JewelryGemDisplayName = jewelryItem.GemDisplayName;
                    itemSaveData.JewelryCraftedBaseValue = jewelryItem.CraftedBaseValue;
                }
            }

            if (item is ConsumableItemInstance consumableItem)
            {
                itemSaveData.IsConsumable = true;
                itemSaveData.CurrentCharges = consumableItem.CurrentCharges;
                itemSaveData.BlastRange = consumableItem.BlastRange;
                itemSaveData.BlastAreaSize = consumableItem.BlastAreaSize;
                itemSaveData.CatalystDurationModifier = consumableItem.CatalystDurationModifier;
                itemSaveData.CatalystEffectMultiplier = consumableItem.CatalystEffectMultiplier;
                itemSaveData.CatalystChargeBonus = consumableItem.CatalystChargeBonus;
                itemSaveData.CatalystDisplayName = consumableItem.CatalystDisplayName;
                itemSaveData.ConsumableCraftedBaseValue = consumableItem.CraftedBaseValue;
                foreach (var effect in consumableItem.RuntimeEffects ?? new List<ConsumableEffectRuntime>())
                {
                    if (effect == null) continue;
                    itemSaveData.ConsumableEffects.Add(new ConsumableEffectSaveData
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
                itemSaveData.IsGalduryte = true;
                itemSaveData.GalduryteTargetType = galduryteItem.TargetType;
                itemSaveData.GalduryteTier = galduryteItem.Tier;
                itemSaveData.GaldurytePowderColor = galduryteItem.GuaranteedPowderColor;
                itemSaveData.GalduryteIsIdentified = galduryteItem.IsIdentified;
                itemSaveData.GalduryteIsDestroyed = galduryteItem.IsDestroyed;
                itemSaveData.GalduryteStoneDefinitionId = galduryteItem.StoneDefinitionId ?? string.Empty;
                itemSaveData.GaldurytePowderDefinitionId = galduryteItem.PowderDefinitionId ?? string.Empty;
                itemSaveData.GalduryteStoneDisplayName = galduryteItem.StoneDisplayName ?? string.Empty;
                itemSaveData.GalduryteCraftedBaseValue = galduryteItem.CraftedBaseValue;

                foreach (var effect in galduryteItem.Effects ?? new List<GalduryteEffectInstance>())
                {
                    if (effect == null) continue;
                    itemSaveData.GalduryteEffects.Add(new GalduryteEffectSaveData
                    {
                        EffectType = effect.EffectType,
                        PowderColor = effect.PowderColor,
                        Grade = effect.Grade,
                        Value = effect.Value
                    });
                }
            }

            if (item is LootBagItemInstance lootBag)
            {
                itemSaveData.IsLootBag = true;

                foreach (var content in lootBag.Contents
                    ?? new List<ItemInstance>())
                {
                    if (!TryCaptureItem(
                            content,
                            saveData,
                            capturedItemIds))
                    {
                        return false;
                    }

                    itemSaveData.LootBagContentItemInstanceIds.Add(
                        content.InstanceId);
                }
            }

            saveData.ItemInstances.Add(itemSaveData);
            return true;
        }

        private static List<PassiveEffectSourceSaveData> CopyPassiveEffectSources(
            List<PassiveEffectSourceRuntime> sources)
        {
            var saveData = new List<PassiveEffectSourceSaveData>();

            if (sources == null)
            {
                return saveData;
            }

            foreach (var source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                saveData.Add(new PassiveEffectSourceSaveData
                {
                    SourceType = source.SourceType,
                    SourceId = source.SourceId,
                    SourceCharacterId = source.SourceCharacterId
                });
            }

            return saveData;
        }

        private static List<GameplayVariableModifierSaveData> CopyVariableModifiers(
                List<GameplayVariableModifier> modifiers)
        {
            var saveData = new List<GameplayVariableModifierSaveData>();

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

                saveData.Add(new GameplayVariableModifierSaveData
                {
                    VariableType = modifier.VariableType,
                    ReferenceId = modifier.ReferenceId,
                    Stacking = modifier.Stacking,
                    Value = modifier.Value
                });
            }

            return saveData;
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
