using System;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;

namespace Godmist.Systems.NPC
{
    public class DialogueService
    {
        public DialogueSession ActiveDialogue { get; private set; }

        public event Action<DialogueSession> DialogueStarted;
        public event Action<DialogueSession> DialogueNodeChanged;
        public event Action<DialogueSession, DialogueChoiceData> DialogueChoiceSelected;
        public event Action<NpcInstance> DialogueEnded;

        public bool TryStartDialogue(NpcInstance npc, string initiatorCharacterId)
        {
            var definition = npc?.Definition?.BaseDialogue;

            if (npc == null || definition == null)
            {
                return false;
            }

            return TryStartDialogue(npc, definition, initiatorCharacterId);
        }

        public bool TryStartDialogue(
            NpcInstance npc,
            DialogueDefinition definition,
            string initiatorCharacterId)
        {
            if (ActiveDialogue != null
                || npc == null
                || definition == null
                || string.IsNullOrWhiteSpace(definition.Id)
                || string.IsNullOrWhiteSpace(initiatorCharacterId))
            {
                return false;
            }

            var startNode = definition.FindNode(definition.StartNodeId);

            if (startNode == null)
            {
                return false;
            }

            ActiveDialogue = new DialogueSession(
                npc,
                definition,
                startNode,
                initiatorCharacterId);

            DialogueStarted?.Invoke(ActiveDialogue);
            return true;
        }

        public bool TrySelectChoice(int choiceIndex)
        {
            if (ActiveDialogue == null)
            {
                return false;
            }

            var choices = ActiveDialogue.CurrentNode.Choices;

            if (choiceIndex < 0 || choiceIndex >= choices.Count)
            {
                return false;
            }

            var choice = choices[choiceIndex];
            DialogueChoiceSelected?.Invoke(ActiveDialogue, choice);

            if (string.IsNullOrWhiteSpace(choice.NextNodeId))
            {
                EndDialogue();
                return true;
            }

            var nextNode = ActiveDialogue.Definition.FindNode(choice.NextNodeId);

            if (nextNode == null)
            {
                return false;
            }

            ActiveDialogue.SetCurrentNode(nextNode);
            DialogueNodeChanged?.Invoke(ActiveDialogue);
            return true;
        }

        public bool TryEndDialogue()
        {
            if (ActiveDialogue == null)
            {
                return false;
            }

            EndDialogue();
            return true;
        }

        private void EndDialogue()
        {
            var npc = ActiveDialogue.Npc;
            ActiveDialogue = null;
            DialogueEnded?.Invoke(npc);
        }
    }
}