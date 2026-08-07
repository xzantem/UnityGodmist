using System;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [Serializable]
    public class DialogueChoiceData
    {
        [field: SerializeField] public string ChoiceId { get; private set; } = string.Empty;

        [field: SerializeField, TextArea(2, 4)]
        public string DisplayText { get; private set; } = string.Empty;

        [field: SerializeField]
        public string NextNodeId { get; private set; } = string.Empty;

        [field: SerializeField]
        public DialogueChoiceQuestActionData QuestAction { get; private set; }
    }
}