using System;

namespace Godmist.Runtime.Quests
{
    [Serializable]
    public class QuestObjectiveProgress
    {
        public string ObjectiveId { get; }
        public int CurrentAmount { get; private set; }
        public bool IsCompleted { get; private set; }

        public QuestObjectiveProgress(string objectiveId)
        {
            ObjectiveId = objectiveId ?? string.Empty;
        }

        public bool TryAddAmount(int amount, int requiredAmount)
        {
            if (amount <= 0 || requiredAmount <= 0 || IsCompleted)
            {
                return false;
            }

            var previousAmount = CurrentAmount;

            CurrentAmount = Math.Min(
                requiredAmount,
                CurrentAmount + amount);

            IsCompleted = CurrentAmount >= requiredAmount;

            return CurrentAmount != previousAmount;
        }
    }
}