using System;
using UnityEngine;

namespace Godmist.Data.Quests
{
    [Serializable]
    public class QuestStageTransitionDefinition
    {
        [field: SerializeField]
        public string TransitionId { get; private set; } = string.Empty;

        [field: SerializeField]
        public string NextStageId { get; private set; } = string.Empty;

        [field: SerializeField]
        public string RequiredBranchId { get; private set; } = string.Empty;

        [field: SerializeField]
        public string RequiredQuestFlagId { get; private set; } = string.Empty;

        [field: SerializeField]
        public string RequiredQuestFlagValue { get; private set; } = string.Empty;
    }
}