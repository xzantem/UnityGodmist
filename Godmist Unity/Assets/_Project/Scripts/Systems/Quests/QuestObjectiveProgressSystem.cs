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
            ReportMatchingObjectives(QuestObjectiveType.ExitDungeon, dungeon.DungeonId, dungeon.DungeonLevel);
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
            string dungeonId = null)
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

                foreach (var objective in quest.CurrentStage.Objectives)
                {
                    if (!MatchesObjective(
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
                    questService.TryAdvanceQuestStage(quest.QuestId);
                }
            }
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
