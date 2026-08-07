using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
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