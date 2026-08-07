using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DialogueServiceTests
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
        public void TryStartDialogue_UsesDefinitionStartNode()
        {
            var service = new DialogueService();
            var npc = CreateNpcWithDialogue(out _);

            var result = service.TryStartDialogue(npc, "jerzy");

            Assert.That(result, Is.True);
            Assert.That(service.ActiveDialogue, Is.Not.Null);
            Assert.That(
                service.ActiveDialogue.InitiatorCharacterId,
                Is.EqualTo("jerzy"));
            Assert.That(
                service.ActiveDialogue.CurrentNode.NodeId,
                Is.EqualTo("greeting"));
        }

        [Test]
        public void TrySelectChoice_ChangesCurrentNode()
        {
            var service = new DialogueService();
            var npc = CreateNpcWithDialogue(out _);

            Assert.That(service.TryStartDialogue(npc, "jerzy"), Is.True);

            var result = service.TrySelectChoice(0);

            Assert.That(result, Is.True);
            Assert.That(
                service.ActiveDialogue.CurrentNode.NodeId,
                Is.EqualTo("answer"));
        }

        [Test]
        public void TrySelectChoice_WithEmptyNextNodeId_EndsDialogue()
        {
            var service = new DialogueService();
            var npc = CreateNpcWithDialogue(out _);

            Assert.That(service.TryStartDialogue(npc, "jerzy"), Is.True);

            var result = service.TrySelectChoice(1);

            Assert.That(result, Is.True);
            Assert.That(service.ActiveDialogue, Is.Null);
        }

        private NpcInstance CreateNpcWithDialogue(
            out DialogueDefinition dialogue)
        {
            dialogue = CreateDialogueDefinition();

            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            createdObjects.Add(npcDefinition);

            SetAutoProperty(
                npcDefinition,
                "Id",
                "npc.arungard.questgiver_01");

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

        private DialogueDefinition CreateDialogueDefinition()
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            createdObjects.Add(dialogue);

            var greeting = new DialogueNodeData();
            SetAutoProperty(greeting, "NodeId", "greeting");
            SetAutoProperty(
                greeting,
                "Choices",
                new List<DialogueChoiceData>
                {
                    CreateChoice("choice.ask", "Zapytaj o Katakumby.", "answer"),
                    CreateChoice("choice.goodbye", "Do widzenia.", string.Empty)
                });

            var answer = new DialogueNodeData();
            SetAutoProperty(answer, "NodeId", "answer");
            SetAutoProperty(
                answer,
                "Choices",
                new List<DialogueChoiceData>
                {
                    CreateChoice("choice.goodbye", "Do widzenia.", string.Empty)
                });

            SetAutoProperty(
                dialogue,
                "Id",
                "dialogue.arungard.questgiver.greeting");

            SetAutoProperty(dialogue, "StartNodeId", "greeting");
            SetAutoProperty(
                dialogue,
                "Nodes",
                new List<DialogueNodeData> { greeting, answer });

            return dialogue;
        }

        private static DialogueChoiceData CreateChoice(
            string choiceId,
            string text,
            string nextNodeId)
        {
            var choice = new DialogueChoiceData();

            SetAutoProperty(choice, "ChoiceId", choiceId);
            SetAutoProperty(choice, "DisplayText", text);
            SetAutoProperty(choice, "NextNodeId", nextNodeId);

            return choice;
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