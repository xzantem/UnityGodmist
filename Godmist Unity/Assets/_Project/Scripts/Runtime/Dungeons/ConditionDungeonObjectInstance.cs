using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class ConditionDungeonObjectInstance : DungeonObjectInstance
    {
        public bool IsSatisfied { get; private set; }
        public string ProgressionConditionId { get; private set; } = string.Empty;

        public ConditionDungeonObjectInstance(
            string instanceId,
            ConditionDungeonObjectDefinition definition,
            Vector2Int position,
            bool isProgressionRequirement = false)
            : base(
                instanceId,
                definition,
                position,
                isProgressionRequirement)
        {
        }

        public bool TryBindCondition(string conditionId)
        {
            if (string.IsNullOrWhiteSpace(conditionId)) return false;

            ProgressionConditionId = conditionId;
            return true;
        }

        public bool TrySatisfy()
        {
            if (IsSatisfied) return false;

            IsSatisfied = true;
            return true;
        }

        public bool TryRestoreSatisfied(bool isSatisfied)
        {
            IsSatisfied = isSatisfied;
            return true;
        }
    }
}
