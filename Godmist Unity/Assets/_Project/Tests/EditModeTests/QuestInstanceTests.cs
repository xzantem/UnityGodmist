using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class QuestInstanceTests
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
        public void Constructor_UsesConfiguredStartStage()
        {
            var definition = CreateQuestDefinition();

            var instance = new QuestInstance(definition);

            Assert.That(instance.QuestId, Is.EqualTo("quest.test"));
            Assert.That(instance.Status, Is.EqualTo(QuestStatus.Available));
            Assert.That(instance.CurrentStageId, Is.EqualTo("stage.intro"));
            Assert.That(instance.ProgressByObjectiveId.Count, Is.EqualTo(4));
        }

        [Test]
        public void TryAdvanceStage_SelectsMatchingDialogueBranch()
        {
            var definition = CreateQuestDefinition();
            var instance = new QuestInstance(
                definition,
                QuestStatus.Active);

            Assert.That(
                instance.TryAddObjectiveProgress("objective.talk"),
                Is.True);

            Assert.That(instance.TryAdvanceStage(), Is.False);

            Assert.That(
                instance.TryAdvanceStage("dialogue.choice.accept"),
                Is.True);

            Assert.That(
                instance.CurrentStageId,
                Is.EqualTo("stage.accept"));

            Assert.That(
                instance.HasChosenBranch("dialogue.choice.accept"),
                Is.True);
        }

        [Test]
        public void TryAdvanceStage_AllowsBranchesToMerge()
        {
            var definition = CreateQuestDefinition();
            var instance = new QuestInstance(
                definition,
                QuestStatus.Active);

            Assert.That(
                instance.TryAddObjectiveProgress("objective.talk"),
                Is.True);

            Assert.That(
                instance.TryAdvanceStage("dialogue.choice.refuse"),
                Is.True);

            Assert.That(
                instance.CurrentStageId,
                Is.EqualTo("stage.refuse"));

            Assert.That(instance.TryAdvanceStage(), Is.True);

            Assert.That(
                instance.CurrentStageId,
                Is.EqualTo("stage.common"));
        }

        private QuestDefinition CreateQuestDefinition()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(definition, "Id", "quest.test");
            SetAutoProperty(definition, "DisplayName", "Testowy quest");
            SetAutoProperty(definition, "QuestType", QuestType.Mini);
            SetAutoProperty(definition, "StartStageId", "stage.intro");

            var talkObjective = CreateObjective(
                "objective.talk",
                QuestObjectiveType.TalkToNpc,
                "npc.test",
                1,
                false);

            var optionalObjective = CreateObjective(
                "objective.optional",
                QuestObjectiveType.InteractWithObject,
                "object.test",
                1,
                true);

            var introStage = CreateStage(
                "stage.intro",
                new List<QuestObjectiveDefinition>
                {
                    talkObjective,
                    optionalObjective
                },
                new List<QuestStageTransitionDefinition>
                {
                    CreateTransition(
                        "transition.accept",
                        "stage.accept",
                        "dialogue.choice.accept"),

                    CreateTransition(
                        "transition.refuse",
                        "stage.refuse",
                        "dialogue.choice.refuse")
                });

            var acceptStage = CreateStage(
                "stage.accept",
                new List<QuestObjectiveDefinition>
                {
                    CreateObjective(
                        "objective.accept",
                        QuestObjectiveType.TalkToNpc,
                        "npc.test",
                        1,
                        false)
                },
                new List<QuestStageTransitionDefinition>
                {
                    CreateTransition(
                        "transition.accept.common",
                        "stage.common")
                });

            var refuseStage = CreateStage(
                "stage.refuse",
                new List<QuestObjectiveDefinition>(),
                new List<QuestStageTransitionDefinition>
                {
                    CreateTransition(
                        "transition.refuse.common",
                        "stage.common")
                });

            var commonStage = CreateStage(
                "stage.common",
                new List<QuestObjectiveDefinition>
                {
                    CreateObjective(
                        "objective.exit",
                        QuestObjectiveType.ExitDungeon,
                        "dungeon.test",
                        1,
                        false)
                },
                new List<QuestStageTransitionDefinition>());

            SetAutoProperty(
                definition,
                "Stages",
                new List<QuestStageDefinition>
                {
                    introStage,
                    acceptStage,
                    refuseStage,
                    commonStage
                });

            return definition;
        }

        private static QuestStageDefinition CreateStage(
            string stageId,
            List<QuestObjectiveDefinition> objectives,
            List<QuestStageTransitionDefinition> transitions)
        {
            var stage = new QuestStageDefinition();

            SetAutoProperty(stage, "StageId", stageId);
            SetAutoProperty(stage, "Objectives", objectives);
            SetAutoProperty(stage, "Transitions", transitions);

            return stage;
        }

        private static QuestObjectiveDefinition CreateObjective(
            string objectiveId,
            QuestObjectiveType objectiveType,
            string targetId,
            int requiredAmount,
            bool isOptional)
        {
            var objective = new QuestObjectiveDefinition();

            SetAutoProperty(objective, "ObjectiveId", objectiveId);
            SetAutoProperty(objective, "ObjectiveType", objectiveType);
            SetAutoProperty(objective, "TargetId", targetId);
            SetAutoProperty(objective, "RequiredAmount", requiredAmount);
            SetAutoProperty(objective, "IsOptional", isOptional);

            return objective;
        }

        private static QuestStageTransitionDefinition CreateTransition(
            string transitionId,
            string nextStageId,
            string requiredBranchId = "")
        {
            var transition = new QuestStageTransitionDefinition();

            SetAutoProperty(transition, "TransitionId", transitionId);
            SetAutoProperty(transition, "NextStageId", nextStageId);
            SetAutoProperty(
                transition,
                "RequiredBranchId",
                requiredBranchId);

            return transition;
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