using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Quests
{
    [Serializable]
    public class QuestObjectiveDefinition
    {
        [field: SerializeField]
        public string ObjectiveId { get; private set; } = string.Empty;

        [field: SerializeField, TextArea(1, 3)]
        public string Description { get; private set; } = string.Empty;

        [field: SerializeField]
        public QuestObjectiveType ObjectiveType { get; private set; }

        [field: SerializeField]
        public string TargetId { get; private set; } = string.Empty;

        [field: SerializeField, Min(1)]
        public int RequiredAmount { get; private set; } = 1;

        [field: SerializeField]
        public bool IsOptional { get; private set; }
    }
}