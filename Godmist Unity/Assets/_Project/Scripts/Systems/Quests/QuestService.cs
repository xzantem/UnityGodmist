using System;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Data.World;
using Godmist.Systems.Dungeons;

namespace Godmist.Systems.Quests
{
    public class QuestService
    {
        private readonly GameSession gameSession;
        private readonly QuestRewardSystem questRewardSystem;
        private readonly Random random;

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
            QuestRewardSystem nextQuestRewardSystem = null,
            GameContentCatalog nextContentCatalog = null,
            Random nextRandom = null)
        {
            gameSession = nextGameSession
                ?? throw new ArgumentNullException(nameof(nextGameSession));

            random = nextRandom ?? new Random();
            questRewardSystem = nextQuestRewardSystem
                ?? new QuestRewardSystem(nextContentCatalog: nextContentCatalog,
                    nextRandom: random);
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

            if (definition.QuestType == QuestType.Radiant)
                EnsureRadiantRewardOffer(definition.Id);

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
                || (questId == CatacombsMissionBoard.BossQuestId
                    && !CatacombsMissionBoard.HasBossOffer(gameSession))
                || (quest.Definition.QuestType == QuestType.Radiant
                    && !EnsureRadiantRewardOffer(questId))
                || !quest.TrySetStatus(QuestStatus.Active))
            {
                return false;
            }

            if (questId == CatacombsMissionBoard.BossQuestId)
                quest.TrySetQuestFlag("radiant.boss_level",
                    CatacombsMissionBoard.BossOfferLevel(gameSession).ToString());

            QuestStarted?.Invoke(quest);
            return true;
        }

        public bool EnsureRadiantRewardOffer(string questId)
        {
            if (!gameSession.TryGetQuest(questId, out var quest)
                || quest.Definition.QuestType != QuestType.Radiant) return false;
            if (quest.RadiantRewardOffer != null) return true;

            var isBoss = questId == CatacombsMissionBoard.BossQuestId;
            var level = 0;
            if (quest.QuestFlags.TryGetValue("radiant.reward_level", out var legacyLevel)
                && int.TryParse(legacyLevel, out var parsedLegacyLevel)) level = parsedLegacyLevel;
            if (level == 0 && isBoss
                && quest.QuestFlags.TryGetValue("radiant.boss_level", out var bossLevel)
                && int.TryParse(bossLevel, out var parsedBossLevel)) level = parsedBossLevel;
            if (level == 0) level = isBoss
                ? CatacombsMissionBoard.BossOfferLevel(gameSession)
                : DungeonOfferSystem.PartyLevel(gameSession);
            if (level < 1 || level > 50) return false;

            if (!quest.QuestFlags.TryGetValue("radiant.reward_variant", out var variant))
            {
                var roll = random.Next(10);
                variant = roll < 3 ? "experience" : roll < 6 ? "gold" : "mix";
                quest.TrySetQuestFlag("radiant.reward_variant", variant);
            }
            return quest.TrySetRadiantRewardOffer(
                questRewardSystem.CreateRadiantRewardOffer(level, isBoss, variant));
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
                if (quest.Definition.QuestType != QuestType.Radiant)
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
            if (quest.Definition.QuestType != QuestType.Radiant)
                GrantStageCompletionRewards(quest, completedStage);
            gameSession.TryClearTrackedQuest(quest.QuestId);

            QuestCompleted?.Invoke(quest);
            return true;
        }

        public bool TryTurnInRadiantQuest(string questId)
        {
            if (!gameSession.TryGetQuest(questId, out var quest)
                || quest.Definition.QuestType != QuestType.Radiant
                || quest.Status != QuestStatus.Completed
                || !EnsureRadiantRewardOffer(questId))
                return false;

            var rewards = questRewardSystem.GrantRadiantRewards(
                gameSession, quest.RadiantRewardOffer);
            if (!quest.TryResetRadiantQuest()) return false;
            EnsureRadiantRewardOffer(questId);
            QuestRewardsGranted?.Invoke(quest, rewards);
            QuestBecameAvailable?.Invoke(quest);
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
