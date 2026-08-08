using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Systems.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class QuestObjectiveProgressSystemTests
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
        public void ReportNpcDialogueCompleted_AdvancesTalkObjective()
        {
            var session = new GameSession();
            var questService = new QuestService(session);
            var progressSystem = new QuestObjectiveProgressSystem(
                session,
                questService);

            var definition = CreateQuestDefinition();

            StartQuest(questService, definition);

            progressSystem.ReportNpcDialogueCompleted(
                "npc.arungard.questgiver_01");

            Assert.That(
                session.TryGetQuest(definition.Id, out var quest),
                Is.True);

            Assert.That(
                quest.CurrentStageId,
                Is.EqualTo("stage.exit"));
        }

        [Test]
        public void ReportDungeonExited_CompletesMatchingQuest()
        {
            var session = new GameSession();
            var questService = new QuestService(session);
            var progressSystem = new QuestObjectiveProgressSystem(
                session,
                questService);

            var definition = CreateQuestDefinition();

            StartQuest(questService, definition);

            progressSystem.ReportNpcDialogueCompleted(
                "npc.arungard.questgiver_01");

            progressSystem.ReportDungeonExited(
                "dungeon.catacombs.procedural");

            Assert.That(
                session.TryGetQuest(definition.Id, out var quest),
                Is.True);

            Assert.That(
                quest.Status,
                Is.EqualTo(QuestStatus.Completed));
        }

        [Test]
        public void ReportNpcDialogueCompleted_IgnoresDifferentNpc()
        {
            var session = new GameSession();
            var questService = new QuestService(session);
            var progressSystem = new QuestObjectiveProgressSystem(
                session,
                questService);

            var definition = CreateQuestDefinition();

            StartQuest(questService, definition);

            progressSystem.ReportNpcDialogueCompleted("npc.other");

            Assert.That(
                session.TryGetQuest(definition.Id, out var quest),
                Is.True);

            Assert.That(
                quest.CurrentStageId,
                Is.EqualTo("stage.talk"));
        }

        private static void StartQuest(
            QuestService questService,
            QuestDefinition definition)
        {
            Assert.That(
                questService.TryRegisterQuest(definition),
                Is.True);

            Assert.That(
                questService.TryMakeQuestAvailable(definition.Id),
                Is.True);

            Assert.That(
                questService.TryStartQuest(definition.Id),
                Is.True);
        }

        private QuestDefinition CreateQuestDefinition()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(
                definition,
                "Id",
                "quest.test.catacombs");

            SetAutoProperty(
                definition,
                "DisplayName",
                "Zbadaj Katakumby");

            SetAutoProperty(
                definition,
                "QuestType",
                QuestType.Mini);

            SetAutoProperty(
                definition,
                "StartStageId",
                "stage.talk");

            var talkStage = CreateStage(
                "stage.talk",
                CreateObjective(
                    "objective.talk",
                    QuestObjectiveType.TalkToNpc,
                    "npc.arungard.questgiver_01"),
                "stage.exit");

            var exitStage = CreateStage(
                "stage.exit",
                CreateObjective(
                    "objective.exit",
                    QuestObjectiveType.ExitDungeon,
                    "dungeon.catacombs.procedural"),
                null);

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
            QuestObjectiveDefinition objective,
            string nextStageId)
        {
            var stage = new QuestStageDefinition();

            SetAutoProperty(stage, "StageId", stageId);
            SetAutoProperty(
                stage,
                "Objectives",
                new List<QuestObjectiveDefinition>
                {
                    objective
                });

            var transitions = new List<QuestStageTransitionDefinition>();

            if (!string.IsNullOrWhiteSpace(nextStageId))
            {
                var transition =
                    new QuestStageTransitionDefinition();

                SetAutoProperty(
                    transition,
                    "TransitionId",
                    $"transition.{stageId}");

                SetAutoProperty(
                    transition,
                    "NextStageId",
                    nextStageId);

                transitions.Add(transition);
            }

            SetAutoProperty(stage, "Transitions", transitions);

            return stage;
        }

        private static QuestObjectiveDefinition CreateObjective(
            string objectiveId,
            QuestObjectiveType objectiveType,
            string targetId)
        {
            var objective = new QuestObjectiveDefinition();

            SetAutoProperty(objective, "ObjectiveId", objectiveId);
            SetAutoProperty(
                objective,
                "ObjectiveType",
                objectiveType);

            SetAutoProperty(objective, "TargetId", targetId);
            SetAutoProperty(objective, "RequiredAmount", 1);
            SetAutoProperty(objective, "IsOptional", false);

            return objective;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}