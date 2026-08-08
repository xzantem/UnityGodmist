using System;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;

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
                dungeonId);
        }

        private void ReportMatchingObjectives(
            QuestObjectiveType objectiveType,
            string targetId)
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

                var progressedQuest = false;

                foreach (var objective in quest.CurrentStage.Objectives)
                {
                    if (!MatchesObjective(
                        objective,
                        objectiveType,
                        targetId))
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
            string expectedTargetId)
        {
            return objective != null
                && objective.ObjectiveType == expectedType
                && objective.TargetId == expectedTargetId;
        }
    }
}