using System;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [Serializable]
    public class DialogueChoiceQuestActionData
    {
        [field: SerializeField]
        public string QuestId { get; private set; } = string.Empty;

        [field: SerializeField]
        public bool MakeQuestAvailable { get; private set; }

        [field: SerializeField]
        public bool StartQuest { get; private set; }

        [field: SerializeField]
        public string QuestBranchId { get; private set; } = string.Empty;
    }
}