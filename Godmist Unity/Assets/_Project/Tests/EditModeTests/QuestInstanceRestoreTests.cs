using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class QuestInstanceRestoreTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void TryRestoreState_RestoresQuestRuntimeState()
        {
            var definition = CreateQuestDefinition();
            var quest = new QuestInstance(definition);

            var objectiveAmounts = new Dictionary<string, int>
            {
                ["objective.talk"] = 2,
                ["objective.exit"] = 3
            };

            var flags = new Dictionary<string, string>
            {
                ["quest.route"] = "merchant"
            };

            var result = quest.TryRestoreState(
                QuestStatus.Active,
                "stage.exit",
                true,
                objectiveAmounts,
                flags,
                new[] { "branch.merchant" },
                new[] { "stage.talk.reward" });

            Assert.That(result, Is.True);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Active));
            Assert.That(quest.CurrentStageId, Is.EqualTo("stage.exit"));
            Assert.That(quest.IsHidden, Is.True);

            Assert.That(
                quest.ProgressByObjectiveId["objective.talk"].CurrentAmount,
                Is.EqualTo(2));

            Assert.That(
                quest.ProgressByObjectiveId["objective.talk"].IsCompleted,
                Is.True);

            Assert.That(
                quest.ProgressByObjectiveId["objective.exit"].CurrentAmount,
                Is.EqualTo(3));

            Assert.That(
                quest.ProgressByObjectiveId["objective.exit"].IsCompleted,
                Is.True);

            Assert.That(
                quest.QuestFlags["quest.route"],
                Is.EqualTo("merchant"));

            Assert.That(
                quest.HasChosenBranch("branch.merchant"),
                Is.True);

            Assert.That(
                quest.ClaimedRewardKeys,
                Does.Contain("stage.talk.reward"));
        }

        [Test]
        public void TryRestoreState_RejectsUnknownObjective()
        {
            var definition = CreateQuestDefinition();
            var quest = new QuestInstance(definition);

            var result = quest.TryRestoreState(
                QuestStatus.Active,
                "stage.talk",
                false,
                new Dictionary<string, int>
                {
                    ["objective.unknown"] = 1
                },
                null,
                null,
                null);

            Assert.That(result, Is.False);
            Assert.That(quest.CurrentStageId, Is.EqualTo("stage.talk"));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available));
        }

        private QuestDefinition CreateQuestDefinition()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(definition, "Id", "quest.restore.test");
            SetAutoProperty(definition, "DisplayName", "Quest restore test");
            SetAutoProperty(definition, "QuestType", QuestType.Mini);
            SetAutoProperty(definition, "StartStageId", "stage.talk");

            var talkStage = CreateStage(
                "stage.talk",
                CreateObjective("objective.talk", 2));

            var exitStage = CreateStage(
                "stage.exit",
                CreateObjective("objective.exit", 3));

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
            QuestObjectiveDefinition objective)
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

            SetAutoProperty(
                stage,
                "CompletionRewards",
                new List<QuestRewardDefinition>());

            SetAutoProperty(
                stage,
                "Transitions",
                new List<QuestStageTransitionDefinition>());

            return stage;
        }

        private static QuestObjectiveDefinition CreateObjective(
            string objectiveId,
            int requiredAmount)
        {
            var objective = new QuestObjectiveDefinition();

            SetAutoProperty(objective, "ObjectiveId", objectiveId);
            SetAutoProperty(
                objective,
                "ObjectiveType",
                QuestObjectiveType.Custom);

            SetAutoProperty(objective, "TargetId", "target.test");
            SetAutoProperty(
                objective,
                "RequiredAmount",
                requiredAmount);

            return objective;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = FindBackingField(
                target.GetType(),
                propertyName);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }

        private static FieldInfo FindBackingField(
            Type type,
            string propertyName)
        {
            for (var currentType = type;
                currentType != null;
                currentType = currentType.BaseType)
            {
                var field = currentType.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}