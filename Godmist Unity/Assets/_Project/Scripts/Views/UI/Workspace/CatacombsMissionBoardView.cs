using System;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CatacombsMissionBoardView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private Transform questList;
        [SerializeField] private Button questButtonTemplate;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailsText;
        [SerializeField] private TextMeshProUGUI windowTitleText;
        [SerializeField] private TextMeshProUGUI metadataText;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private GameObject rewardsRoot;
        [SerializeField] private TextMeshProUGUI experienceText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI honorText;
        [SerializeField] private MissionRewardItemView rewardItem;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;

        private GameSession session;
        private QuestService service;
        private string selectedQuestId;

        private void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(Act);
        }

        private void OnDestroy()
        {
            if (actionButton != null) actionButton.onClick.RemoveListener(Act);
        }

        private void OnDisable()
        {
            if (session?.CurrentMode == GameModeState.Menu)
                session.TrySetMode(GameModeState.Exploration);
        }

        public bool Open(NpcInstance npc)
        {
            if (npc?.NpcId != "npc.arungard.questgiver_01") return false;
            var bootstrap = GameBootstrap.Instance;
            session = bootstrap?.Session;
            service = bootstrap?.QuestService;
            if (session == null || service == null || window == null
                || questList == null || questButtonTemplate == null
                || !session.TrySetMode(GameModeState.Menu)) return false;

            selectedQuestId = CatacombsMissionBoard.KillQuestId;
            service.EnsureMissionBoard();
            messageText.text = string.Empty;
            if (windowTitleText != null) windowTitleText.text = "Zlecenia";
            window.Open();
            Refresh();
            return true;
        }

        private void Refresh()
        {
            if (windowTitleText != null) windowTitleText.text = "Zlecenia";
            for (var i = questList.childCount - 1; i >= 0; i--)
            {
                var child = questList.GetChild(i);
                if (child == questButtonTemplate.transform) continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            QuestInstance firstVisible = null;
            if (!session.TryGetQuest(selectedQuestId, out var selected) || !IsVisible(selected))
                selectedQuestId = null;
            foreach (var questId in CatacombsMissionBoard.QuestIds)
            {
                if (!session.TryGetQuest(questId, out var quest)
                    || !IsVisible(quest)) continue;
                service.EnsureRadiantRewardOffer(questId);
                firstVisible ??= quest;
                selectedQuestId ??= questId;
                var row = Instantiate(questButtonTemplate, questList);
                row.gameObject.SetActive(true);
                var rowTitle = row.transform.Find("TextRoot/Title")?.GetComponent<TextMeshProUGUI>();
                var rowDetail = row.transform.Find("TextRoot/Detail")?.GetComponent<TextMeshProUGUI>();
                var rowBorder = row.transform.Find("Border")?.GetComponent<Image>();
                if (rowTitle != null) rowTitle.text = quest.DisplayName;
                if (rowDetail != null) rowDetail.text = Metadata(quest);
                if (rowBorder != null && questId == selectedQuestId)
                {
                    var palette = row.GetComponent<UIWorkspaceStyleApplier>()?.Palette;
                    if (palette != null)
                        palette.ApplyFrameAccent(rowBorder, UIWorkspaceAccentStyle.Selected);
                }
                var capturedId = questId;
                row.onClick.AddListener(() => Select(capturedId));
            }

            if (!session.TryGetQuest(selectedQuestId, out selected)
                || !IsVisible(selected))
            {
                selected = firstVisible;
                selectedQuestId = selected?.QuestId;
            }
            RefreshDetails(selected);
        }

        private bool IsVisible(QuestInstance quest) =>
            quest.Status is QuestStatus.Active or QuestStatus.Completed
            || CatacombsMissionBoard.CanOffer(session, quest.QuestId);

        private void Select(string questId)
        {
            selectedQuestId = questId;
            messageText.text = string.Empty;
            Refresh();
        }

        private void RefreshDetails(QuestInstance quest)
        {
            if (quest == null)
            {
                titleText.text = "Brak dostępnych zleceń";
                detailsText.text = string.Empty;
                metadataText.text = objectiveText.text = string.Empty;
                progressRoot.SetActive(false);
                rewardsRoot.SetActive(false);
                rewardItem.Bind(null);
                actionButton.interactable = false;
                actionButtonText.text = "Brak zlecenia";
                return;
            }

            titleText.text = quest.DisplayName;
            var offer = quest.EffectiveRadiantRewardOffer;
            metadataText.text = Metadata(quest);
            var description = quest.Description ?? string.Empty;
            var sentenceEnd = description.IndexOf('.');
            objectiveText.text = sentenceEnd >= 0 ? description.Substring(0, sentenceEnd + 1) : description;
            detailsText.text = sentenceEnd >= 0 ? description.Substring(sentenceEnd + 1).Trim() : string.Empty;
            if (offer != null)
                detailsText.text += (detailsText.text.Length > 0 ? "\n" : string.Empty)
                    + $"Poziom wyprawy: {offer.Level} lub wyższy.";
            progressRoot.SetActive(quest.Status is QuestStatus.Active or QuestStatus.Completed);
            var progressLines = new StringBuilder();
            var current = 0;
            var required = 0;
            foreach (var objective in quest.CurrentStage.Objectives)
            {
                if (objective == null) continue;
                quest.TryGetObjectiveProgress(objective.ObjectiveId, out var progress);
                var amount = Math.Clamp(progress?.CurrentAmount ?? 0, 0, objective.RequiredAmount);
                if (progressLines.Length > 0) progressLines.AppendLine();
                progressLines.Append($"{objective.Description.TrimEnd('.')}  {amount}/{objective.RequiredAmount}");
                current += amount;
                required += objective.RequiredAmount;
            }
            progressText.text = progressLines.ToString();
            progressFill.fillAmount = required > 0 ? (float)current / required : 0;
            progressFill.color = quest.Status == QuestStatus.Completed
                ? new Color32(112, 202, 91, 255) : new Color32(93, 183, 210, 255);
            rewardsRoot.SetActive(offer != null);
            experienceText.text = offer?.Experience.ToString("N0") ?? string.Empty;
            goldText.text = offer?.Gold.ToString("N0") ?? string.Empty;
            honorText.text = offer?.Honor.ToString() ?? string.Empty;
            rewardItem.Bind(offer?.BonusItem);

            actionButton.interactable = quest.Status is QuestStatus.Unavailable
                or QuestStatus.Available or QuestStatus.Completed;
            actionButtonText.text = quest.Status switch
            {
                QuestStatus.Completed => "Odbierz nagrodę",
                QuestStatus.Active => "W trakcie",
                _ => "Przyjmij zlecenie"
            };
        }

        private static string Metadata(QuestInstance quest)
        {
            var status = quest.Status switch
            {
                QuestStatus.Active => "<color=#70C9DF>W trakcie</color>",
                QuestStatus.Completed => "<color=#70CA5B>Do odebrania</color>",
                _ => "Dostępne"
            };
            return $"Poziom {quest.RadiantRewardOffer?.Level ?? 0} • {status}";
        }

        private void Act()
        {
            if (!session.TryGetQuest(selectedQuestId, out var quest)) return;
            var success = quest.Status switch
            {
                QuestStatus.Completed => service.TryTurnInRadiantQuest(quest.QuestId),
                QuestStatus.Unavailable => CatacombsMissionBoard.CanOffer(session, quest.QuestId)
                    && service.TryMakeQuestAvailable(quest.QuestId)
                    && service.TryStartQuest(quest.QuestId),
                QuestStatus.Available => CatacombsMissionBoard.CanOffer(session, quest.QuestId)
                    && service.TryStartQuest(quest.QuestId),
                _ => false
            };
            messageText.text = success ? string.Empty : "Nie można teraz wykonać tej czynności.";
            if (success) GameBootstrap.Instance?.RequestAutosave("Zlecenie");
            Refresh();
        }
    }
}
