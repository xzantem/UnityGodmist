using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Systems.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DialogueQuestActionSystemTests
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
        public void TrySelectChoice_MakesQuestAvailableAndStartsIt()
        {
            var gameSession = new GameSession();
            var questService = new QuestService(gameSession);
            var dialogueService = new DialogueService();

            using var actionSystem = new DialogueQuestActionSystem(
                dialogueService,
                questService);

            var questDefinition = CreateQuestDefinition();

            Assert.That(
                questService.TryRegisterQuest(questDefinition),
                Is.True);

            var npc = CreateNpcWithDialogue(
                questDefinition.Id);

            Assert.That(
                dialogueService.TryStartDialogue(npc, "jerzy"),
                Is.True);

            Assert.That(dialogueService.TrySelectChoice(0), Is.True);

            Assert.That(
                gameSession.TryGetQuest(
                    questDefinition.Id,
                    out var quest),
                Is.True);

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Active));
        }

        private QuestDefinition CreateQuestDefinition()
        {
            var definition = ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);

            SetAutoProperty(
                definition,
                "Id",
                "quest.dialogue_action.test");

            SetAutoProperty(
                definition,
                "DisplayName",
                "Test akcji dialogowej");

            SetAutoProperty(
                definition,
                "QuestType",
                QuestType.Mini);

            SetAutoProperty(
                definition,
                "StartStageId",
                "stage.start");

            var stage = new QuestStageDefinition();

            SetAutoProperty(stage, "StageId", "stage.start");
            SetAutoProperty(
                stage,
                "Objectives",
                new List<QuestObjectiveDefinition>());

            SetAutoProperty(
                stage,
                "Transitions",
                new List<QuestStageTransitionDefinition>());

            SetAutoProperty(
                stage,
                "CompletionRewards",
                new List<QuestRewardDefinition>());

            SetAutoProperty(
                definition,
                "Stages",
                new List<QuestStageDefinition> { stage });

            return definition;
        }

        private NpcInstance CreateNpcWithDialogue(string questId)
        {
            var action = new DialogueChoiceQuestActionData();

            SetAutoProperty(action, "QuestId", questId);
            SetAutoProperty(action, "MakeQuestAvailable", true);
            SetAutoProperty(action, "StartQuest", true);

            var choice = new DialogueChoiceData();

            SetAutoProperty(choice, "ChoiceId", "choice.accept");
            SetAutoProperty(
                choice,
                "DisplayText",
                "Przyjmuje zadanie.");
            SetAutoProperty(choice, "NextNodeId", string.Empty);
            SetAutoProperty(choice, "QuestAction", action);

            var node = new DialogueNodeData();

            SetAutoProperty(node, "NodeId", "start");
            SetAutoProperty(
                node,
                "Choices",
                new List<DialogueChoiceData> { choice });

            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            createdObjects.Add(dialogue);

            SetAutoProperty(
                dialogue,
                "Id",
                "dialogue.quest_action.test");

            SetAutoProperty(dialogue, "StartNodeId", "start");
            SetAutoProperty(
                dialogue,
                "Nodes",
                new List<DialogueNodeData> { node });

            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            createdObjects.Add(npcDefinition);

            SetAutoProperty(
                npcDefinition,
                "Id",
                "npc.quest_action.test");

            SetAutoProperty(
                npcDefinition,
                "BaseDialogue",
                dialogue);

            return new NpcInstance
            {
                NpcId = npcDefinition.Id,
                Definition = npcDefinition
            };
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