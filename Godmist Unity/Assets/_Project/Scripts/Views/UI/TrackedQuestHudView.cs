using System;
using System.Linq;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public sealed class TrackedQuestHudView : MonoBehaviour
    {
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text objectives;
        GameSession session;
        QuestService quests;

        private void LateUpdate()
        {
            var game = GameBootstrap.Instance;
            if (!ReferenceEquals(session, game?.Session) || !ReferenceEquals(quests, game?.QuestService)) Bind(game?.Session, game?.QuestService);
        }
        public void Bind(GameSession nextSession, QuestService nextQuests)
        {
            Unbind(); session = nextSession; quests = nextQuests;
            if (session != null) session.TrackedQuestChanged += TrackedChanged;
            if (quests != null)
            {
                quests.QuestObjectiveProgressed += Progressed;
                quests.QuestStageAdvanced += StageChanged;
                quests.QuestCompleted += Changed;
                quests.QuestStarted += Changed;
                quests.QuestRewardsGranted += RewardsGranted;
            }
            Refresh();
        }
        private void Unbind()
        {
            if (session != null) session.TrackedQuestChanged -= TrackedChanged;
            if (quests == null) return;
            quests.QuestObjectiveProgressed -= Progressed;
            quests.QuestStageAdvanced -= StageChanged;
            quests.QuestCompleted -= Changed;
            quests.QuestStarted -= Changed;
            quests.QuestRewardsGranted -= RewardsGranted;
        }
        private void TrackedChanged(string _) => Refresh();
        private void Progressed(QuestInstance quest, QuestObjectiveProgress _) => Changed(quest);
        private void StageChanged(QuestInstance quest, string _, string __) => Changed(quest);
        private void RewardsGranted(QuestInstance quest, QuestRewardResult _) => Changed(quest);
        private void Changed(QuestInstance quest) { if (quest?.QuestId == session?.TrackedQuestId) Refresh(); }
        public void Refresh()
        {
            QuestInstance quest = null;
            if (!string.IsNullOrWhiteSpace(session?.TrackedQuestId)) session.TryGetQuest(session.TrackedQuestId, out quest);
            var shown = quest != null && !quest.IsHidden && quest.Status is QuestStatus.Active or QuestStatus.Completed;
            if (content != null && content.activeSelf != shown) content.SetActive(shown);
            if (!shown) return;
            if (title != null) title.text = quest.DisplayName;
            if (objectives != null) objectives.text = BuildObjectives(quest);
        }
        public static string BuildObjectives(QuestInstance quest)
        {
            if (quest == null) return string.Empty;
            if (quest.Status == QuestStatus.Completed) return "<color=#70CA5B>Gotowe do odebrania</color>";
            var goals = quest.CurrentStage?.Objectives?.Where(o => o != null).OrderBy(o => {
                quest.TryGetObjectiveProgress(o.ObjectiveId, out var p); return p?.IsCompleted == true ? 1 : 0;
            }).ToArray();
            var text = new StringBuilder();
            if (goals == null) return string.Empty;
            foreach (var goal in goals.Take(3))
            {
                quest.TryGetObjectiveProgress(goal.ObjectiveId, out var progress);
                if (text.Length > 0) text.Append('\n');
                var amount = Mathf.Clamp(progress?.CurrentAmount ?? 0, 0, Mathf.Max(1, goal.RequiredAmount));
                text.Append(progress?.IsCompleted == true ? "<color=#70CA5B>✓ " : "• ");
                text.Append(QuestJournalPresentation.ObjectiveLabel(goal));
                text.Append($"  {amount}/{Mathf.Max(1,goal.RequiredAmount)}");
                if (progress?.IsCompleted == true) text.Append("</color>");
            }
            if (goals.Length > 3) text.Append($"\nPozostałe cele: {goals.Length-3} — J");
            return text.ToString();
        }
        private void OnDestroy() => Unbind();
    }
}
