using System;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;

namespace Godmist.Systems.Quests
{
    public class QuestService
    {
        private readonly GameSession gameSession;
        private readonly QuestRewardSystem questRewardSystem;

        public event Action<QuestInstance> QuestRegistered;
        public event Action<QuestInstance> QuestBecameAvailable;
        public event Action<QuestInstance> QuestStarted;
        public event Action<QuestInstance, QuestObjectiveProgress>
            QuestObjectiveProgressed;
        public event Action<QuestInstance, string, string>
            QuestStageAdvanced;
        public event Action<QuestInstance> QuestCompleted;
        public event Action<QuestInstance, QuestRewardResult> QuestRewardsGranted;

        public QuestService(
            GameSession nextGameSession,
            QuestRewardSystem nextQuestRewardSystem = null)
        {
            gameSession = nextGameSession
                ?? throw new ArgumentNullException(nameof(nextGameSession));

            questRewardSystem = nextQuestRewardSystem
                ?? new QuestRewardSystem();
        }

        public bool TryRegisterQuest(
            QuestDefinition definition,
            QuestStatus initialStatus = QuestStatus.Unavailable)
        {
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.Id)
                || gameSession.QuestsById.ContainsKey(definition.Id))
            {
                return false;
            }

            var quest = new QuestInstance(definition, initialStatus);

            if (!gameSession.TryRegisterQuest(quest))
            {
                return false;
            }

            QuestRegistered?.Invoke(quest);
            return true;
        }

        public bool TryMakeQuestAvailable(string questId)
        {
            if (!gameSession.TryGetQuest(questId, out var quest)
                || quest.Status != QuestStatus.Unavailable
                || !quest.TrySetStatus(QuestStatus.Available))
            {
                return false;
            }

            QuestBecameAvailable?.Invoke(quest);
            return true;
        }

        public bool TryStartQuest(string questId)
        {
            if (!gameSession.TryGetQuest(questId, out var quest)
                || quest.Status != QuestStatus.Available
                || !quest.TrySetStatus(QuestStatus.Active))
            {
                return false;
            }

            QuestStarted?.Invoke(quest);
            return true;
        }

        public bool TryAddObjectiveProgress(
            string questId,
            string objectiveId,
            int amount = 1)
        {
            if (!gameSession.TryGetQuest(questId, out var quest)
                || !quest.TryAddObjectiveProgress(objectiveId, amount)
                || !quest.TryGetObjectiveProgress(
                    objectiveId,
                    out var progress))
            {
                return false;
            }

            QuestObjectiveProgressed?.Invoke(quest, progress);
            return true;
        }

        public bool TryRecordQuestBranch(
            string questId,
            string branchId)
        {
            return gameSession.TryGetQuest(questId, out var quest)
                && quest.TryRecordBranch(branchId);
        }

        public bool TrySetQuestFlag(
            string questId,
            string flagId,
            string value)
        {
            return gameSession.TryGetQuest(questId, out var quest)
                && quest.TrySetQuestFlag(flagId, value);
        }

        public bool TryAdvanceQuestStage(
            string questId,
            string selectedBranchId = null)
        {
            if (!gameSession.TryGetQuest(questId, out var quest)
                || quest.Status != QuestStatus.Active
                || !quest.IsCurrentStageCompleted())
            {
                return false;
            }

            var previousStageId = quest.CurrentStageId;
            var completedStage = quest.CurrentStage;

            if (quest.TryAdvanceStage(selectedBranchId))
            {
                GrantStageCompletionRewards(quest, completedStage);
                QuestStageAdvanced?.Invoke(
                    quest,
                    previousStageId,
                    quest.CurrentStageId);

                return true;
            }

            if (quest.CurrentStage?.Transitions == null
                || quest.CurrentStage.Transitions.Count > 0
                || !quest.TrySetStatus(QuestStatus.Completed))
            {
                return false;
            }
            GrantStageCompletionRewards(quest, completedStage);
            gameSession.TryClearTrackedQuest(quest.QuestId);

            QuestCompleted?.Invoke(quest);
            return true;
        }

        private void GrantStageCompletionRewards(
            QuestInstance quest,
            QuestStageDefinition completedStage)
        {
            var rewards = questRewardSystem.GrantStageCompletionRewards(
                gameSession,
                quest,
                completedStage);

            if (rewards.HasRewards)
            {
                QuestRewardsGranted?.Invoke(quest, rewards);
            }
        }
    }
}