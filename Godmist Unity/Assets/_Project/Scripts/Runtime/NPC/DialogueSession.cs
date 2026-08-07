using Godmist.Data.NPC;

namespace Godmist.Runtime.NPC
{
    public class DialogueSession
    {
        public NpcInstance Npc { get; }
        public DialogueDefinition Definition { get; }
        public DialogueNodeData CurrentNode { get; private set; }
        public string InitiatorCharacterId { get; }

        public DialogueSession(
            NpcInstance npc,
            DialogueDefinition definition,
            DialogueNodeData startNode,
            string initiatorCharacterId)
        {
            Npc = npc;
            Definition = definition;
            CurrentNode = startNode;
            InitiatorCharacterId = initiatorCharacterId;
        }

        public void SetCurrentNode(DialogueNodeData nextNode)
        {
            CurrentNode = nextNode;
        }
    }
}