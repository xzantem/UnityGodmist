using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class LeverDungeonObjectInstance : DungeonObjectInstance
    {
        public bool IsReversible { get; }
        public string ProgressionConditionId { get; private set; } = string.Empty;
        public bool IsActivated { get; private set; }

        public LeverDungeonObjectInstance(
            string instanceId,
            LeverDungeonObjectDefinition definition,
            Vector2Int position,
            bool isProgressionRequirement = false)
            : base(
                instanceId,
                definition,
                position,
                isProgressionRequirement)
        {
            IsReversible = definition.IsReversible;
        }

        public bool TryBindCondition(string conditionId)
        {
            if (string.IsNullOrWhiteSpace(conditionId)) return false;

            ProgressionConditionId = conditionId;
            return true;
        }

        public bool TryToggle(out bool isActivated)
        {
            isActivated = IsActivated;

            if (IsActivated && !IsReversible) return false;

            IsActivated = !IsActivated;
            isActivated = IsActivated;
            return true;
        }

        public bool TryRestoreActivated(bool isActivated)
        {
            IsActivated = isActivated;
            return true;
        }
    }
}
