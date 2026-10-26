using System;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Quests
{
    public class QuestObjectiveProgressSystem
    {
        private readonly GameSession gameSession;
        private readonly QuestService questService;

        public QuestObjectiveProgressSystem(
            GameSession nextGameSession,
            QuestService nextQuestService)
        {
            gameSession = nextGameSession
                ?? throw new ArgumentNullException(
                    nameof(nextGameSession));

            questService = nextQuestService
                ?? throw new ArgumentNullException(
                    nameof(nextQuestService));
        }

        public void ReportNpcDialogueCompleted(string npcId)
        {
            ReportMatchingObjectives(
                QuestObjectiveType.TalkToNpc,
                npcId);
        }

        public void ReportDungeonExited(string dungeonId)
        {
            ReportMatchingObjectives(
                QuestObjectiveType.ExitDungeon,
                dungeonId,
                0);
        }

        public void ReportDungeonExited(DungeonInstance dungeon)
        {
            if (dungeon == null) return;
            if (dungeon.DungeonId == CatacombsMissionBoard.BossDungeonId)
            {
                var boss = dungeon.Definition?.BossEncounterDefinition;
                var bossDefeated = false;
                if (boss != null)
                    foreach (var floor in dungeon.Floors)
                        foreach (var encounter in floor.Encounters)
                            if (encounter?.Definition == boss
                                && dungeon.IsEncounterCleared(encounter.InstanceId))
                                bossDefeated = true;
                if (!bossDefeated) return;
            }
            ReportMatchingObjectives(QuestObjectiveType.ExitDungeon, dungeon.DungeonId, dungeon.DungeonLevel,
                completionMultiplier: dungeon.CompletionRewardMultiplier, completedDungeon: dungeon);
        }

        public void ReportEnemyDefeated(
            string dungeonId,
            int dungeonLevel,
            string enemyDefinitionId,
            bool isReinforcement)
        {
            if (isReinforcement || string.IsNullOrWhiteSpace(enemyDefinitionId)) return;
            ReportMatchingObjectives(QuestObjectiveType.KillEnemy, enemyDefinitionId, dungeonLevel, dungeonId);
        }

        private void ReportMatchingObjectives(
            QuestObjectiveType objectiveType,
            string targetId,
            int dungeonLevel = 0,
            string dungeonId = null, int completionMultiplier = 1, DungeonInstance completedDungeon = null)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            foreach (var quest in gameSession.QuestsById.Values)
            {
                if (quest.Status != QuestStatus.Active
                    || quest.CurrentStage?.Objectives == null)
                {
                    continue;
                }

                if (quest.Definition.QuestType == QuestType.Radiant
                    && (dungeonLevel < 1
                        || !questService.EnsureRadiantRewardOffer(quest.QuestId)
                        || dungeonLevel < quest.RadiantRewardOffer.Level))
                    continue;

                var progressedQuest = false;
                var contract=quest.BoardContract;
                if (contract!=null && !MatchesContract(contract,objectiveType,dungeonId,completedDungeon)) continue;

                foreach (var objective in quest.CurrentStage.Objectives)
                {
                    if (contract==null && !MatchesObjective(
                        objective,
                        objectiveType,
                        targetId,
                        dungeonId))
                    {
                        continue;
                    }

                    if (questService.TryAddObjectiveProgress(
                        quest.QuestId,
                        objective.ObjectiveId))
                    {
                        progressedQuest = true;
                    }
                }

                if (progressedQuest)
                {
                    if (objectiveType == QuestObjectiveType.ExitDungeon && quest.UsesLengthReward)
                        quest.TrySetQuestFlag(Godmist.Runtime.Quests.QuestInstance.CompletionLengthFlag,
                            Math.Clamp(completionMultiplier,1,3).ToString());
                    questService.TryAdvanceQuestStage(quest.QuestId);
                }
            }
        }

        private bool MatchesContract(Godmist.Runtime.Quests.RadiantBoardContract contract, QuestObjectiveType type,
            string dungeonId, DungeonInstance completedDungeon)
        {
            if(contract.ObjectiveType!=type) return false;
            if(type==QuestObjectiveType.ExitDungeon)
                return completedDungeon?.Definition?.Theme==contract.Theme && completedDungeon.Length==contract.Length;
            return questService.TryGetMissionDungeon(dungeonId,out var definition) && definition.Theme==contract.Theme;
        }

        private static bool MatchesObjective(
            QuestObjectiveDefinition objective,
            QuestObjectiveType expectedType,
            string expectedTargetId,
            string dungeonId)
        {
            return objective != null
                && objective.ObjectiveType == expectedType
                && (objective.TargetId == expectedTargetId
                    || expectedType == QuestObjectiveType.ExitDungeon
                       && objective.TargetId == "dungeon.catacombs.expedition"
                       && expectedTargetId == CatacombsMissionBoard.BossDungeonId
                    || expectedType == QuestObjectiveType.KillEnemy
                       && objective.TargetId == "dungeon.catacombs"
                       && (dungeonId == "dungeon.catacombs.expedition"
                           || dungeonId == "dungeon.catacombs.boss_expedition")
                       && expectedTargetId.StartsWith("enemy.catacombs.", StringComparison.Ordinal)
                       && expectedTargetId != "enemy.catacombs.dead_king");
        }
    }
}
