using System.Collections.Generic;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class QuestJournalPanelView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;

        [Header("Quest lists")]
        [SerializeField] private Transform activeQuestsContent;
        [SerializeField] private Transform completedQuestsContent;
        [SerializeField] private QuestJournalQuestButtonView questButtonPrefab;

        [Header("Quest details")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private TextMeshProUGUI objectivesText;

        [Header("Tracking")]
        [SerializeField] private Button trackButton;
        [SerializeField] private TextMeshProUGUI trackButtonText;

        private GameSession gameSession;
        private QuestService questService;
        private string selectedQuestId = string.Empty;
        private GameModeState modeBeforeOpen;

        private bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            trackButton.onClick.AddListener(ToggleTracking);

            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            var session = GameBootstrap.Instance?.Session;

            if (session == null) return;
            if (!IsOpen && session.CurrentMode != GameModeState.Exploration) return;
            if (!Input.GetKeyDown(KeyCode.J)) return;

            Toggle();
        }

        private void OnDestroy()
        {
            UnbindServices();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        public void Open()
        {
            gameSession = GameBootstrap.Instance?.Session;
            questService = GameBootstrap.Instance?.QuestService;

            if (gameSession == null || questService == null) return;

            if (gameSession.CurrentMode != GameModeState.Exploration)
            {
                return;
            }

            modeBeforeOpen = gameSession.CurrentMode;
            gameSession.TrySetMode(GameModeState.Menu);

            BindServices();

            panelRoot.SetActive(true);
            EnsureSelectedQuest();
            RefreshAll();
        }

        public void Close()
        {
            panelRoot.SetActive(false);
            UnbindServices();

            if (gameSession != null
                && gameSession.CurrentMode == GameModeState.Menu
                && modeBeforeOpen == GameModeState.Exploration)
            {
                gameSession.TrySetMode(GameModeState.Exploration);
            }
        }

        private void BindServices()
        {
            UnbindServices();

            gameSession.TrackedQuestChanged += HandleTrackedQuestChanged;

            questService.QuestRegistered += HandleQuestChanged;
            questService.QuestBecameAvailable += HandleQuestChanged;
            questService.QuestStarted += HandleQuestChanged;
            questService.QuestCompleted += HandleQuestChanged;
            questService.QuestObjectiveProgressed += HandleQuestObjectiveProgressed;
            questService.QuestStageAdvanced += HandleQuestStageAdvanced;
        }

        private void UnbindServices()
        {
            if (gameSession != null)
            {
                gameSession.TrackedQuestChanged -= HandleTrackedQuestChanged;
            }

            if (questService == null) return;

            questService.QuestRegistered -= HandleQuestChanged;
            questService.QuestBecameAvailable -= HandleQuestChanged;
            questService.QuestStarted -= HandleQuestChanged;
            questService.QuestCompleted -= HandleQuestChanged;
            questService.QuestObjectiveProgressed -= HandleQuestObjectiveProgressed;
            questService.QuestStageAdvanced -= HandleQuestStageAdvanced;
        }

        private void HandleTrackedQuestChanged(string trackedQuestId)
        {
            RefreshAll();
        }

        private void HandleQuestChanged(QuestInstance quest)
        {
            RefreshAll();
        }

        private void HandleQuestObjectiveProgressed(
            QuestInstance quest,
            QuestObjectiveProgress progress)
        {
            RefreshAll();
        }

        private void HandleQuestStageAdvanced(
            QuestInstance quest,
            string previousStageId,
            string nextStageId)
        {
            RefreshAll();
        }

        private void SelectQuest(string questId)
        {
            if (gameSession == null
                || !gameSession.TryGetQuest(questId, out _))
            {
                return;
            }

            selectedQuestId = questId;
            RefreshAll();
        }

        private void ToggleTracking()
        {
            if (gameSession == null
                || !gameSession.TryGetQuest(selectedQuestId, out var quest)
                || quest.Status != QuestStatus.Active)
            {
                return;
            }

            if (gameSession.TrackedQuestId == quest.QuestId)
            {
                gameSession.TryClearTrackedQuest(quest.QuestId);
                return;
            }

            gameSession.TrySetTrackedQuest(quest.QuestId);
        }

        private void RefreshAll()
        {
            if (!IsOpen || gameSession == null) return;

            EnsureSelectedQuest();
            RefreshQuestLists();
            RefreshDetails();
            RefreshTrackingButton();
        }

        private void EnsureSelectedQuest()
        {
            if (!string.IsNullOrWhiteSpace(selectedQuestId)
                && gameSession.TryGetQuest(selectedQuestId, out var selectedQuest)
                && IsVisibleInJournal(selectedQuest))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(gameSession.TrackedQuestId)
                && gameSession.TryGetQuest(
                    gameSession.TrackedQuestId,
                    out var trackedQuest))
            {
                selectedQuestId = trackedQuest.QuestId;
                return;
            }

            foreach (var quest in gameSession.QuestsById.Values)
            {
                if (quest?.Status == QuestStatus.Active)
                {
                    selectedQuestId = quest.QuestId;
                    return;
                }
            }

            foreach (var quest in gameSession.QuestsById.Values)
            {
                if (quest != null && IsCompletedQuest(quest))
                {
                    selectedQuestId = quest.QuestId;
                    return;
                }
            }

            selectedQuestId = string.Empty;
        }

        private void RefreshQuestLists()
        {
            ClearContent(activeQuestsContent);
            ClearContent(completedQuestsContent);

            foreach (var quest in gameSession.QuestsById.Values)
            {
                if (quest == null || !IsVisibleInJournal(quest)) continue;

                var content = quest.Status == QuestStatus.Active
                    ? activeQuestsContent
                    : completedQuestsContent;

                var buttonView = Instantiate(questButtonPrefab, content);

                buttonView.Bind(
                    quest,
                    quest.QuestId == selectedQuestId,
                    SelectQuest);
            }
        }

        private void RefreshDetails()
        {
            if (!gameSession.TryGetQuest(selectedQuestId, out var quest))
            {
                titleText.text = "Brak wybranego zadania";
                descriptionText.text = "Przyjęte zadania pojawią się w dzienniku.";
                stageText.text = string.Empty;
                objectivesText.text = string.Empty;
                return;
            }

            titleText.text = quest.Definition?.DisplayName ?? quest.QuestId;
            descriptionText.text = quest.Definition?.Description ?? string.Empty;

            var stage = quest.CurrentStage;

            stageText.text = stage == null
                ? "Brak aktywnego etapu."
                : stage.Description;

            objectivesText.text = BuildObjectivesText(quest);
        }

        private void RefreshTrackingButton()
        {
            if (!gameSession.TryGetQuest(selectedQuestId, out var quest)
                || quest.Status != QuestStatus.Active)
            {
                trackButton.interactable = false;
                trackButtonText.text = "Nie można śledzić";
                return;
            }

            var isTracked = gameSession.TrackedQuestId == quest.QuestId;

            trackButton.interactable = true;
            trackButtonText.text = isTracked
                ? "Przestań śledzić"
                : "Śledź zadanie";
        }

        private static string BuildObjectivesText(QuestInstance quest)
        {
            var stage = quest.CurrentStage;

            if (stage?.Objectives == null || stage.Objectives.Count == 0)
            {
                return "Brak celów w tym etapie.";
            }

            var builder = new StringBuilder();

            foreach (var objective in stage.Objectives)
            {
                if (objective == null) continue;

                quest.TryGetObjectiveProgress(
                    objective.ObjectiveId,
                    out var progress);

                var currentAmount = progress?.CurrentAmount ?? 0;
                var isCompleted = progress?.IsCompleted ?? false;
                var description = string.IsNullOrWhiteSpace(objective.Description)
                    ? $"{objective.ObjectiveType}: {objective.TargetId}"
                    : objective.Description;

                var optionalLabel = objective.IsOptional
                    ? " (opcjonalne)"
                    : string.Empty;

                var marker = isCompleted ? "[x]" : "[ ]";

                builder.AppendLine(
                    $"{marker} {description} " +
                    $"({currentAmount}/{objective.RequiredAmount})" +
                    optionalLabel);
            }

            return builder.ToString();
        }

        private static bool IsVisibleInJournal(QuestInstance quest)
        {
            return quest.Status == QuestStatus.Active
                || IsCompletedQuest(quest);
        }

        private static bool IsCompletedQuest(QuestInstance quest)
        {
            return quest.Status == QuestStatus.Completed
                || quest.Status == QuestStatus.TurnedIn
                || quest.Status == QuestStatus.Failed;
        }

        private static void ClearContent(Transform content)
        {
            if (content == null) return;

            for (var index = content.childCount - 1; index >= 0; index--)
            {
                Destroy(content.GetChild(index).gameObject);
            }
        }
    }
}