using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class QuestServiceTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [TestCase(0, 123)]
        [TestCase(1, 1200)]
        public void ExperienceReward_SupportsStandardMissionAndLegacyFixedReward(int level, int expected)
        {
            var session = new GameSession();
            var character = new Godmist.Runtime.Characters.CharacterInstance
            {
                CharacterId = "quest.xp", Level = 1,
                LifeState = CharacterLifeState.Alive, AvailabilityState = CharacterAvailabilityState.Available
            };
            session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
            var definition = CreateQuestDefinition();
            var quest = new QuestInstance(definition);
            var reward = new QuestRewardDefinition();
            typeof(QuestRewardDefinition).GetField("rewardType", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reward, QuestRewardType.Experience);
            typeof(QuestRewardDefinition).GetField("amount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reward, 123);
            typeof(QuestRewardDefinition).GetField("standardExperienceLevel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reward, level);
            var stage = quest.CurrentStage ?? definition.FindStage(definition.StartStageId);
            stage.CompletionRewards.Add(reward);
            var system = new QuestRewardSystem();
            var result = system.GrantStageCompletionRewards(session, quest, stage);
            Assert.That(result.ExperiencePerActiveCharacter, Is.EqualTo(expected));
            Assert.That(character.CurrentExperience, Is.EqualTo(expected));
            Assert.That(system.GrantStageCompletionRewards(session, quest, stage).ExperiencePerActiveCharacter, Is.Zero);
        }

        [Test]
        public void TryRegisterQuest_AddsQuestToGameSession()
        {
            var session = new GameSession();
            var service = new QuestService(session);
            var definition = CreateQuestDefinition();

            QuestInstance registeredQuest = null;
            service.QuestRegistered += quest => registeredQuest = quest;

            var result = service.TryRegisterQuest(definition);

            Assert.That(result, Is.True);
            Assert.That(
                session.TryGetQuest(definition.Id, out var quest),
                Is.True);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Unavailable));
            Assert.That(registeredQuest, Is.SameAs(quest));
        }

        [Test]
        public void RadiantQuest_OnlyCountsAcceptedOrdinaryKills_ThenPaysOnceAndReopens()
        {
            var session = new GameSession();
            var character = new Godmist.Runtime.Characters.CharacterInstance
            {
                CharacterId = "radiant.hero", Level = 5,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available
            };
            session.TryRegisterCharacter(character);
            session.TryAddCharacterToActiveParty(character.CharacterId);
            var service = new QuestService(session, nextRandom: new System.Random(7));
            var progress = new QuestObjectiveProgressSystem(session, service);
            var definition = CreateRadiantKillQuest();
            Assert.That(service.TryRegisterQuest(definition), Is.True);

            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 5,
                "enemy.catacombs.swordsman", false);
            Assert.That(service.TryMakeQuestAvailable(definition.Id), Is.True);
            Assert.That(service.TryStartQuest(definition.Id), Is.True);
            Assert.That(session.TryGetQuest(definition.Id, out var quest), Is.True);
            var offer = quest.RadiantRewardOffer;
            Assert.That(offer.Level, Is.EqualTo(5));

            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 5,
                "enemy.catacombs.swordsman", true);
            progress.ReportEnemyDefeated("dungeon.other", 5,
                "enemy.catacombs.swordsman", false);
            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 5,
                "enemy.catacombs.dead_king", false);
            Assert.That(quest.ProgressByObjectiveId["objective.contract"].CurrentAmount, Is.Zero);

            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 5,
                "enemy.catacombs.swordsman", false);
            progress.ReportEnemyDefeated("dungeon.catacombs.boss_expedition", 5,
                "enemy.catacombs.archer", false);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(quest.RadiantRewardOffer, Is.SameAs(offer));
            Assert.That(session.Gold, Is.Zero);

            Assert.That(service.TryTurnInRadiantQuest(definition.Id), Is.True);
            Assert.That(service.TryTurnInRadiantQuest(definition.Id), Is.False);
            Assert.That(session.Gold, Is.EqualTo(offer.Gold));
            Assert.That(character.CurrentExperience,
                Is.EqualTo(offer.Experience));
            Assert.That(character.Honor, Is.EqualTo(2));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available));
            Assert.That(quest.ProgressByObjectiveId["objective.contract"].CurrentAmount, Is.Zero);
            Assert.That(service.TryStartQuest(definition.Id), Is.True);
        }

        [Test]
        public void RadiantQuest_UsesAveragePartyLevelAndIgnoresLowerLevelExpeditions()
        {
            var session = new GameSession();
            foreach (var level in new[] { 5, 9 })
            {
                var character = new Godmist.Runtime.Characters.CharacterInstance
                {
                    CharacterId = $"hero.{level}", Level = level,
                    LifeState = CharacterLifeState.Alive,
                    AvailabilityState = CharacterAvailabilityState.Available
                };
                session.TryRegisterCharacter(character);
                session.TryAddCharacterToActiveParty(character.CharacterId);
            }
            var service = new QuestService(session, nextRandom: new System.Random(2));
            var progress = new QuestObjectiveProgressSystem(session, service);
            var definition = CreateRadiantKillQuest();
            Assert.That(service.TryRegisterQuest(definition), Is.True);
            Assert.That(service.TryMakeQuestAvailable(definition.Id), Is.True);
            Assert.That(service.TryStartQuest(definition.Id), Is.True);
            session.TryGetQuest(definition.Id, out var quest);
            Assert.That(quest.RadiantRewardOffer.Level, Is.EqualTo(7));

            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 6,
                "enemy.catacombs.swordsman", false);
            Assert.That(quest.ProgressByObjectiveId["objective.contract"].CurrentAmount, Is.Zero);
            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 8,
                "enemy.catacombs.swordsman", false);
            progress.ReportEnemyDefeated("dungeon.catacombs.expedition", 8,
                "enemy.catacombs.swordsman", false);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(quest.RadiantRewardOffer.Level, Is.EqualTo(7));
        }

        [Test]
        public void RadiantOffer_RoundTripsExactBonusItemAndPayout()
        {
            var definition = CreateRadiantKillQuest();
            var itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            createdObjects.Add(itemDefinition);
            SetAutoProperty(itemDefinition, "Id", "item.quest.bonus");
            SetAutoProperty(itemDefinition, "ItemType", ItemType.Material);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            createdObjects.Add(catalog);
            catalog.QuestDefinitions.Add(definition);
            catalog.ItemDefinitions.Add(itemDefinition);

            var quest = new QuestInstance(definition, QuestStatus.Completed);
            var bonus = new ItemInstance(itemDefinition, "item.quest.bonus.001");
            var offer = new RadiantRewardOffer(7, 4321, 123, 2, bonus);
            Assert.That(quest.TrySetRadiantRewardOffer(offer), Is.True);
            var session = new GameSession();
            Assert.That(session.TryRegisterQuest(quest), Is.True);

            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "Misja");
            Assert.That(snapshot.Quests[0].RadiantRewardLevel, Is.EqualTo(7));
            Assert.That(snapshot.Quests[0].RadiantRewardExperience, Is.EqualTo(4321));
            Assert.That(snapshot.Quests[0].RadiantBonusItemInstanceId,
                Is.EqualTo(bonus.InstanceId));
            var serializer = new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot), out var data), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(data, catalog, out var restored), Is.True);
            Assert.That(restored.TryGetQuest(definition.Id, out var restoredQuest), Is.True);
            Assert.That(restoredQuest.RadiantRewardOffer.Experience, Is.EqualTo(4321));
            Assert.That(restoredQuest.RadiantRewardOffer.Gold, Is.EqualTo(123));
            Assert.That(restoredQuest.RadiantRewardOffer.BonusItem.InstanceId,
                Is.EqualTo(bonus.InstanceId));
            Assert.That(new QuestService(restored).TryTurnInRadiantQuest(definition.Id), Is.True);
            Assert.That(restored.Gold, Is.EqualTo(123));
            Assert.That(restored.PendingLoot[0].InstanceId, Is.EqualTo(bonus.InstanceId));
        }

        [Test]
        public void RadiantExpedition_RequiresDungeonAtLeastAtMissionLevel()
        {
            var session = new GameSession();
            var character = new Godmist.Runtime.Characters.CharacterInstance
            {
                CharacterId = "expedition.hero", Level = 8,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available
            };
            session.TryRegisterCharacter(character);
            session.TryAddCharacterToActiveParty(character.CharacterId);
            var definition = CreateRadiantKillQuest();
            var objective = definition.Stages[0].Objectives[0];
            SetAutoProperty(objective, "ObjectiveType", QuestObjectiveType.ExitDungeon);
            SetAutoProperty(objective, "TargetId", "dungeon.catacombs.expedition");
            SetAutoProperty(objective, "RequiredAmount", 1);
            var dungeonDefinition = ScriptableObject.CreateInstance<DungeonDefinition>();
            createdObjects.Add(dungeonDefinition);
            SetAutoProperty(dungeonDefinition, "Id", "dungeon.catacombs.expedition");
            var service = new QuestService(session);
            var progress = new QuestObjectiveProgressSystem(session, service);
            Assert.That(service.TryRegisterQuest(definition), Is.True);
            Assert.That(service.TryMakeQuestAvailable(definition.Id), Is.True);
            Assert.That(service.TryStartQuest(definition.Id), Is.True);
            session.TryGetQuest(definition.Id, out var quest);
            Assert.That(quest.RadiantRewardOffer.Level, Is.EqualTo(8));

            progress.ReportDungeonExited(new DungeonInstance(dungeonDefinition, 1, 7));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Active));
            progress.ReportDungeonExited(new DungeonInstance(dungeonDefinition, 2, 9));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
        }

        [Test]
        public void RadiantQuest_CompletedStateAndRewardVariantSurviveRestore()
        {
            var definition = CreateRadiantKillQuest();
            var original = new QuestInstance(definition, QuestStatus.Completed);
            Assert.That(original.TryRestoreState(QuestStatus.Completed, "stage.contract", false,
                new Dictionary<string, int> { ["objective.contract"] = 2 },
                new Dictionary<string, string>
                {
                    ["radiant.reward_level"] = "10",
                    ["radiant.reward_variant"] = "experience"
                }, null, null), Is.True);
            var restored = new QuestInstance(definition);
            Assert.That(restored.TryRestoreState(original.Status, original.CurrentStageId,
                original.IsHidden, new Dictionary<string, int> { ["objective.contract"] = 2 },
                original.QuestFlags, original.ChosenBranchIds,
                original.ClaimedRewardKeys), Is.True);
            Assert.That(restored.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(restored.QuestFlags["radiant.reward_level"], Is.EqualTo("10"));
            Assert.That(restored.QuestFlags["radiant.reward_variant"], Is.EqualTo("experience"));
        }

        [TestCase("experience", 1.25d, .75d)]
        [TestCase("gold", .75d, 1.25d)]
        [TestCase("mix", 1d, 1d)]
        public void RadiantRewards_ApplyRolledVariantToCompletedLevel(
            string variant, double experienceFactor, double goldFactor)
        {
            const int level = 10;
            var session = new GameSession();
            var reward = new QuestRewardSystem(nextRandom: new System.Random(1));
            var result = reward.GrantRadiantRewards(session, level, false, variant);
            var baseExperience = CharacterProgressionSystem.GetStandardMissionExperience(level);
            var baseGold = 4 * CombatEconomyScaling.Scale(10, level);
            Assert.That(result.ExperiencePerActiveCharacter,
                Is.EqualTo((int)System.Math.Round(baseExperience * experienceFactor)));
            Assert.That(result.Gold,
                Is.EqualTo((int)System.Math.Round(baseGold * goldFactor)));
            Assert.That(result.HonorPerActiveCharacter, Is.EqualTo(2));
        }

        private QuestDefinition CreateRadiantKillQuest()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);
            SetAutoProperty(definition, "Id", "quest.radiant.test");
            SetAutoProperty(definition, "DisplayName", "Test zlecenia");
            SetAutoProperty(definition, "QuestType", QuestType.Radiant);
            SetAutoProperty(definition, "StartStageId", "stage.contract");
            var objective = CreateObjective("objective.contract", QuestObjectiveType.KillEnemy,
                "dungeon.catacombs");
            SetAutoProperty(objective, "RequiredAmount", 2);
            SetAutoProperty(definition, "Stages", new List<QuestStageDefinition>
            {
                CreateStage("stage.contract", new List<QuestObjectiveDefinition> { objective },
                    new List<QuestStageTransitionDefinition>())
            });
            return definition;
        }

        [Test]
        public void TryStartQuest_ChangesAvailableQuestToActive()
        {
            var session = new GameSession();
            var service = new QuestService(session);
            var definition = CreateQuestDefinition();

            Assert.That(service.TryRegisterQuest(definition), Is.True);

            QuestInstance availableQuest = null;
            QuestInstance startedQuest = null;

            service.QuestBecameAvailable += quest =>
                availableQuest = quest;

            service.QuestStarted += quest =>
                startedQuest = quest;

            Assert.That(
                service.TryMakeQuestAvailable(definition.Id),
                Is.True);

            Assert.That(
                service.TryStartQuest(definition.Id),
                Is.True);

            Assert.That(
                session.TryGetQuest(definition.Id, out var quest),
                Is.True);

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Active));
            Assert.That(availableQuest, Is.SameAs(quest));
            Assert.That(startedQuest, Is.SameAs(quest));
        }

        [Test]
        public void TryAdvanceQuestStage_CompletesFinalStage()
        {
            var session = new GameSession();
            var service = new QuestService(session);
            var definition = CreateQuestDefinition();

            Assert.That(service.TryRegisterQuest(definition), Is.True);
            Assert.That(
                service.TryMakeQuestAvailable(definition.Id),
                Is.True);
            Assert.That(service.TryStartQuest(definition.Id), Is.True);

            Assert.That(
                service.TryAddObjectiveProgress(
                    definition.Id,
                    "objective.talk"),
                Is.True);

            Assert.That(
                service.TryAdvanceQuestStage(definition.Id),
                Is.True);

            Assert.That(
                session.TryGetQuest(definition.Id, out var quest),
                Is.True);

            Assert.That(
                quest.CurrentStageId,
                Is.EqualTo("stage.exit"));

            Assert.That(
                service.TryAddObjectiveProgress(
                    definition.Id,
                    "objective.exit"),
                Is.True);

            QuestInstance completedQuest = null;
            service.QuestCompleted += quest =>
                completedQuest = quest;

            Assert.That(
                service.TryAdvanceQuestStage(definition.Id),
                Is.True);

            Assert.That(
                quest.Status,
                Is.EqualTo(QuestStatus.Completed));

            Assert.That(completedQuest, Is.SameAs(quest));
        }

        [Test]
        public void TryAdvanceQuestStage_GrantsFinalStageRewardsOnlyOnce()
        {
            var session = new GameSession();
            var character = new Godmist.Runtime.Characters.CharacterInstance
            {
                CharacterId = "jerzy",
                Level = 1,
                CurrentHealth = 100,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available
            };

            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var service = new QuestService(session);
            var definition = CreateQuestDefinitionWithFinalRewards();

            Assert.That(service.TryRegisterQuest(definition), Is.True);
            Assert.That(service.TryMakeQuestAvailable(definition.Id), Is.True);
            Assert.That(service.TryStartQuest(definition.Id), Is.True);

            Assert.That(
                service.TryAddObjectiveProgress(
                    definition.Id,
                    "objective.reward"),
                Is.True);

            Assert.That(service.TryAdvanceQuestStage(definition.Id), Is.True);

            Assert.That(session.Gold, Is.EqualTo(25));
            Assert.That(character.Honor, Is.EqualTo(3));
            Assert.That(character.CurrentExperience, Is.EqualTo(20));
            Assert.That(
                session.WorldState.HasFlag(
                    "world.quest.reward_completed",
                    "true"),
                Is.True);

            Assert.That(
                service.TryAdvanceQuestStage(definition.Id),
                Is.False);

            Assert.That(session.Gold, Is.EqualTo(25));
            Assert.That(character.Honor, Is.EqualTo(3));
            Assert.That(character.CurrentExperience, Is.EqualTo(20));
        }

        private QuestDefinition CreateQuestDefinitionWithFinalRewards()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(definition, "Id", "quest.reward.test");
            SetAutoProperty(definition, "DisplayName", "Test nagród questa");
            SetAutoProperty(definition, "QuestType", QuestType.Mini);
            SetAutoProperty(definition, "StartStageId", "stage.reward");

            var objective = CreateObjective(
                "objective.reward",
                QuestObjectiveType.TalkToNpc,
                "npc.test");

            var rewards = new List<QuestRewardDefinition>
            {
                CreateAmountReward(QuestRewardType.Experience, 20),
                CreateAmountReward(QuestRewardType.Gold, 25),
                CreateAmountReward(QuestRewardType.Honor, 3),
                CreateWorldFlagReward(
                    "world.quest.reward_completed",
                    "true")
            };

            var stage = CreateStage(
                "stage.reward",
                new List<QuestObjectiveDefinition>
                {
                    objective
                },
                new List<QuestStageTransitionDefinition>(),
                rewards);

            SetAutoProperty(
                definition,
                "Stages",
                new List<QuestStageDefinition>
                {
                    stage
                });

            return definition;
        }

        private static QuestRewardDefinition CreateAmountReward(
            QuestRewardType rewardType,
            int amount)
        {
            var reward = new QuestRewardDefinition();

            SetAutoProperty(reward, "RewardType", rewardType);
            SetAutoProperty(reward, "Amount", amount);

            return reward;
        }

        private static QuestRewardDefinition CreateWorldFlagReward(
            string flagId,
            string flagValue)
        {
            var reward = new QuestRewardDefinition();

            SetAutoProperty(
                reward,
                "RewardType",
                QuestRewardType.WorldFlag);

            SetAutoProperty(reward, "WorldFlagId", flagId);
            SetAutoProperty(reward, "WorldFlagValue", flagValue);

            return reward;
        }

        private QuestDefinition CreateQuestDefinition()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(definition, "Id", "quest.service.test");
            SetAutoProperty(
                definition,
                "DisplayName",
                "Test QuestService");

            SetAutoProperty(
                definition,
                "QuestType",
                QuestType.Mini);

            SetAutoProperty(
                definition,
                "StartStageId",
                "stage.talk");

            var talkObjective = CreateObjective(
                "objective.talk",
                QuestObjectiveType.TalkToNpc,
                "npc.test");

            var exitObjective = CreateObjective(
                "objective.exit",
                QuestObjectiveType.ExitDungeon,
                "dungeon.test");

            var talkStage = CreateStage(
                "stage.talk",
                new List<QuestObjectiveDefinition>
                {
                    talkObjective
                },
                new List<QuestStageTransitionDefinition>
                {
                    CreateTransition(
                        "transition.to.exit",
                        "stage.exit")
                });

            var exitStage = CreateStage(
                "stage.exit",
                new List<QuestObjectiveDefinition>
                {
                    exitObjective
                },
                new List<QuestStageTransitionDefinition>());

            SetAutoProperty(
                definition,
                "Stages",
                new List<QuestStageDefinition>
                {
                    talkStage,
                    exitStage
                });

            return definition;
        }

        private static QuestStageDefinition CreateStage(
            string stageId,
            List<QuestObjectiveDefinition> objectives,
            List<QuestStageTransitionDefinition> transitions,
            List<QuestRewardDefinition> completionRewards = null)
        {
            var stage = new QuestStageDefinition();

            SetAutoProperty(stage, "StageId", stageId);
            SetAutoProperty(stage, "Objectives", objectives);
            SetAutoProperty(stage, "Transitions", transitions);
            SetAutoProperty(stage, "CompletionRewards", completionRewards ?? new List<QuestRewardDefinition>());

            return stage;
        }

        private static QuestObjectiveDefinition CreateObjective(
            string objectiveId,
            QuestObjectiveType objectiveType,
            string targetId)
        {
            var objective = new QuestObjectiveDefinition();

            SetAutoProperty(objective, "ObjectiveId", objectiveId);
            SetAutoProperty(objective, "ObjectiveType", objectiveType);
            SetAutoProperty(objective, "TargetId", targetId);
            SetAutoProperty(objective, "RequiredAmount", 1);
            SetAutoProperty(objective, "IsOptional", false);

            return objective;
        }

        private static QuestStageTransitionDefinition CreateTransition(
            string transitionId,
            string nextStageId)
        {
            var transition = new QuestStageTransitionDefinition();

            SetAutoProperty(transition, "TransitionId", transitionId);
            SetAutoProperty(transition, "NextStageId", nextStageId);

            return transition;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                bindingFlags);

            if (field == null)
            {
                var serializedFieldName =
                    char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

                field = target.GetType().GetField(
                    serializedFieldName,
                    bindingFlags);
            }

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}
