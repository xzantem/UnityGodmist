using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Quests
{
    [Serializable]
    public class QuestStageDefinition
    {
        public QuestStageDefinition() { }
        public QuestStageDefinition(string id, string description, QuestObjectiveDefinition objective)
        { StageId=id; Description=description; Objectives=new List<QuestObjectiveDefinition>{objective}; }

        [field: SerializeField]
        public string StageId { get; private set; } = string.Empty;

        [field: SerializeField, TextArea(2, 5)]
        public string Description { get; private set; } = string.Empty;

        [field: SerializeField]
        public List<QuestObjectiveDefinition> Objectives { get; private set; } = new();

        [field: SerializeField]
        public List<QuestRewardDefinition> CompletionRewards { get; private set; } = new();

        [field: SerializeField]
        public List<QuestStageTransitionDefinition> Transitions { get; private set; } = new();

        public QuestObjectiveDefinition FindObjective(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId) || Objectives == null)
            {
                return null;
            }

            foreach (var objective in Objectives)
            {
                if (objective != null && objective.ObjectiveId == objectiveId)
                {
                    return objective;
                }
            }

            return null;
        }
    }
}
