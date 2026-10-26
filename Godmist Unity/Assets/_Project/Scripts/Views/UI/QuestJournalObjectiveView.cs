using Godmist.Data.Quests;
using Godmist.Runtime.Quests;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed class QuestJournalObjectiveView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI description;
        [SerializeField] private TextMeshProUGUI counter;
        [SerializeField] private Image progressFill;
        [SerializeField] private SkillTreeRankMarkers completionMarker;
        [SerializeField] private LayoutElement layout;

        public void Bind(QuestObjectiveDefinition objective, QuestObjectiveProgress progress)
        {
            var required = Mathf.Max(1, objective.RequiredAmount);
            var amount = Mathf.Clamp(progress?.CurrentAmount ?? 0, 0, required);
            var complete = progress?.IsCompleted ?? false;
            description.text = QuestJournalPresentation.ObjectiveLabel(objective)
                + (objective.IsOptional ? " <size=80%>(opcjonalne)</size>" : string.Empty);
            counter.text = $"{amount}/{required}";
            counter.color = complete ? QuestJournalPresentation.Green : QuestJournalPresentation.Cream;
            progressFill.fillAmount = (float)amount / required;
            progressFill.color = complete ? QuestJournalPresentation.Green : QuestJournalPresentation.Cyan;
            completionMarker.SetRanks(complete ? 1 : 0, 1, QuestJournalPresentation.Green,
                new Color32(94, 102, 102, 255));
        }

        private void LateUpdate()
        {
            // Text wraps at the actual window width; the layout must reserve all its lines.
            var height = Mathf.Max(52, description.GetPreferredValues(description.text,
                Mathf.Max(1, description.rectTransform.rect.width), 0).y + 32);
            if (!Mathf.Approximately(layout.preferredHeight, height)) layout.preferredHeight = height;
        }
    }
}
