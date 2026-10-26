using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Quests;
using UnityEngine;

namespace Godmist.Views.UI
{
    /// <summary>Read-only journal labels; quest state and reward grants remain in QuestService.</summary>
    public static class QuestJournalPresentation
    {
        public static readonly Color Cream = new Color32(232, 222, 191, 255);
        public static readonly Color Cyan = new Color32(112, 201, 223, 255);
        public static readonly Color Green = new Color32(112, 202, 91, 255);
        public static string ObjectiveLabel(QuestObjectiveDefinition objective) =>
            string.IsNullOrWhiteSpace(objective.Description)
                ? $"{objective.ObjectiveType}: {objective.TargetId}"
                : objective.Description.Trim().TrimEnd('.');
        private static string Normalize(string text) => (text ?? string.Empty).Trim().TrimEnd('.');

        public static bool ShowStage(QuestInstance quest) =>
            quest.Definition.QuestType != QuestType.Radiant
            && (quest.Definition.Stages?.Count > 1 || quest.CurrentStage?.Objectives?.Count > 1)
            && !string.IsNullOrWhiteSpace(quest.CurrentStage?.Description)
            && !string.Equals(Normalize(quest.CurrentStage.Description), Normalize(quest.Definition.Description), StringComparison.OrdinalIgnoreCase)
            && !(quest.CurrentStage.Objectives?.Any(o => o != null && string.Equals(
                Normalize(o.Description), Normalize(quest.CurrentStage.Description), StringComparison.OrdinalIgnoreCase)) ?? false);

        public static string Description(QuestInstance quest)
        {
            var text = quest.Description ?? string.Empty;
            // Radiant mission descriptions start with the objective already shown in the progress row.
            if (quest.Definition.QuestType == QuestType.Radiant)
            {
                var end = text.IndexOf('.');
                if (end >= 0) text = text.Substring(end + 1).Trim();
                if (quest.RadiantRewardOffer != null)
                    text += (text.Length > 0 ? "\n" : string.Empty)
                        + $"Poziom wyprawy: {quest.RadiantRewardOffer.Level} lub wyższy.";
            }
            return text;
        }

        public static string Status(QuestStatus status) => status switch
        {
            QuestStatus.Active => "<color=#70C9DF>W trakcie</color>",
            QuestStatus.Completed => "<color=#70CA5B>Do odebrania</color>",
            QuestStatus.TurnedIn => "<color=#70CA5B>Ukończone</color>",
            QuestStatus.Failed => "<color=#CF7770>Nieudane</color>",
            _ => string.Empty
        };

        public static string ProgressSummary(QuestInstance quest)
        {
            if (quest.Status != QuestStatus.Active) return Status(quest.Status);
            var current = 0; var required = 0;
            foreach (var objective in quest.CurrentStage?.Objectives ?? Enumerable.Empty<QuestObjectiveDefinition>())
            {
                if (objective == null || objective.IsOptional) continue;
                quest.TryGetObjectiveProgress(objective.ObjectiveId, out var progress);
                current += Mathf.Clamp(progress?.CurrentAmount ?? 0, 0, objective.RequiredAmount);
                required += objective.RequiredAmount;
            }
            return required > 0 ? $"{current}/{required}" : string.Empty;
        }
    }
}
