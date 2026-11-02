using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Quests
{
    [CreateAssetMenu(
        fileName = "QuestDefinition",
        menuName = "Godmist/Quests/Quest Definition")]
    public class QuestDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField]
        public string Id { get; private set; } = string.Empty;

        [field: SerializeField]
        public string DisplayName { get; private set; } = string.Empty;

        [field: SerializeField, TextArea(3, 8)]
        public string Description { get; private set; } = string.Empty;

        [field: SerializeField]
        public QuestType QuestType { get; private set; }

        [field: SerializeField]
        public string StartStageId { get; private set; } = string.Empty;

        [field: SerializeField]
        public List<QuestStageDefinition> Stages { get; private set; } = new();

        [SerializeField] private bool stableRewardIdsInitialized;
        public bool HasStableRewardIds => stableRewardIdsInitialized;
        [SerializeField, Range(1,50)] private int level = 1;
        public int Level => level;

        public QuestStageDefinition FindStage(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || Stages == null)
            {
                return null;
            }

            foreach (var stage in Stages)
            {
                if (stage != null && stage.StageId == stageId)
                {
                    return stage;
                }
            }

            return null;
        }
    }
}