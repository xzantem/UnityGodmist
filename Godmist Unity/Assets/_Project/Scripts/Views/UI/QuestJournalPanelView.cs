using System.Collections.Generic;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using Godmist.Data.Quests;
using Godmist.Systems.Characters;
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

        [Header("Workspace journal")]
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private QuestJournalWindowVisibility visibility;
        [SerializeField] private Button activeTab;
        [SerializeField] private Button completedTab;
        [SerializeField] private TextMeshProUGUI activeTabText;
        [SerializeField] private TextMeshProUGUI completedTabText;
        [SerializeField] private GameObject activeListRoot;
        [SerializeField] private GameObject completedListRoot;
        [SerializeField] private TextMeshProUGUI emptyListText;
        [SerializeField] private TextMeshProUGUI metadataText;
        [SerializeField] private GameObject stageRoot;
        [SerializeField] private Transform objectivesContent;
        [SerializeField] private QuestJournalObjectiveView objectivePrefab;
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private GameObject detailsContent;
        [SerializeField] private GameObject rewardsRoot;
        [SerializeField] private TextMeshProUGUI experienceText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI honorText;
        [SerializeField] private MissionRewardItemView rewardItem;

        [SerializeField] private TrackedQuestHudView trackedQuestHudPrefab;
        private TrackedQuestHudView trackedQuestHud;
        private GameSession gameSession;
        private QuestService questService;
        private string selectedQuestId = string.Empty;
        private GameModeState modeBeforeOpen;
        private bool completedSelected;
        private bool categoryInitialized;

        private bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (trackedQuestHudPrefab != null && panelRoot != null)
            {
                trackedQuestHud = Instantiate(trackedQuestHudPrefab, panelRoot.transform.parent);
                trackedQuestHud.transform.SetAsFirstSibling();
            }
            closeButton.onClick.AddListener(Close);
            trackButton.onClick.AddListener(ToggleTracking);
            if (activeTab != null) activeTab.onClick.AddListener(ShowActive);
            if (completedTab != null) completedTab.onClick.AddListener(ShowCompleted);
            if (visibility != null) visibility.Hidden += HandleWindowHidden;

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
            if (trackedQuestHud != null) Destroy(trackedQuestHud.gameObject);
            if (visibility != null) visibility.Hidden -= HandleWindowHidden;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (trackButton != null) trackButton.onClick.RemoveListener(ToggleTracking);
            if (activeTab != null) activeTab.onClick.RemoveListener(ShowActive);
            if (completedTab != null) completedTab.onClick.RemoveListener(ShowCompleted);
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

            if (!categoryInitialized)
            {
                completedSelected = !HasQuests(false) && HasQuests(true);
                categoryInitialized = true;
            }
            if (window != null) window.Open();
            else panelRoot.SetActive(true);
            EnsureSelectedQuest();
            RefreshAll();
        }

        public void Close()
        {
            if (window != null) window.Close();
            else panelRoot.SetActive(false);
            HandleWindowHidden();
        }

        private void HandleWindowHidden()
        {
            UnbindServices();

            if (gameSession != null
                && gameSession.CurrentMode == GameModeState.Menu
                && modeBeforeOpen == GameModeState.Exploration)
            {
                gameSession.TrySetMode(GameModeState.Exploration);
            }
        }

        private void ShowActive() => ShowCategory(false);
        private void ShowCompleted() => ShowCategory(true);
        private void ShowCategory(bool completed)
        {
            completedSelected = completed;
            selectedQuestId = string.Empty;
            EnsureSelectedQuest();
            RefreshAll();
            if (detailsScroll != null) detailsScroll.verticalNormalizedPosition = 1;
        }

        private bool HasQuests(bool completed)
        {
            foreach (var quest in gameSession.QuestsById.Values)
                if (quest != null && (completed ? IsCompletedQuest(quest) : quest.Status == QuestStatus.Active)) return true;
            return false;
        }

        private bool IsInSelectedCategory(QuestInstance quest) => quest != null
            && (completedSelected ? IsCompletedQuest(quest) : quest.Status == QuestStatus.Active);

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
            if (detailsScroll != null) detailsScroll.verticalNormalizedPosition = 1;
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
                && IsInSelectedCategory(selectedQuest))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(gameSession.TrackedQuestId)
                && gameSession.TryGetQuest(
                    gameSession.TrackedQuestId,
                    out var trackedQuest)
                && IsInSelectedCategory(trackedQuest))
            {
                selectedQuestId = trackedQuest.QuestId;
                return;
            }

            foreach (var quest in gameSession.QuestsById.Values)
            {
                if (IsInSelectedCategory(quest))
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
                    SelectQuest,
                    gameSession.TrackedQuestId == quest.QuestId);
            }

            if (activeListRoot != null) activeListRoot.SetActive(!completedSelected);
            if (completedListRoot != null) completedListRoot.SetActive(completedSelected);
            if (activeTabText != null) activeTabText.color = !completedSelected ? new Color32(225, 182, 61, 255) : QuestJournalPresentation.Cream;
            if (completedTabText != null) completedTabText.color = completedSelected ? new Color32(225, 182, 61, 255) : QuestJournalPresentation.Cream;
            if (emptyListText != null)
            {
                emptyListText.text = completedSelected ? "Brak zakończonych zadań" : "Brak aktywnych zadań";
                emptyListText.gameObject.SetActive(!HasQuests(completedSelected));
            }
        }

        private void RefreshDetails()
        {
            ClearContent(objectivesContent);
            if (!gameSession.TryGetQuest(selectedQuestId, out var quest))
            {
                if (detailsContent != null) detailsContent.SetActive(false);
                titleText.text = string.Empty;
                descriptionText.text = string.Empty;
                stageText.text = string.Empty;
                objectivesText.text = string.Empty;
                if (metadataText != null) metadataText.text = string.Empty;
                if (stageRoot != null) stageRoot.SetActive(false);
                if (rewardsRoot != null) rewardsRoot.SetActive(false);
                if (rewardItem != null) rewardItem.Bind(null);
                return;
            }

            if (detailsContent != null) detailsContent.SetActive(true);
            titleText.text = quest.DisplayName ?? quest.QuestId;
            descriptionText.text = QuestJournalPresentation.Description(quest);
            descriptionText.gameObject.SetActive(descriptionText.text.Length > 0);
            if (metadataText != null) metadataText.text = (quest.RadiantRewardOffer != null
                ? $"Poziom {quest.RadiantRewardOffer.Level} • " : string.Empty) + QuestJournalPresentation.Status(quest.Status);

            var stage = quest.CurrentStage;

            stageText.text = stage?.Description ?? string.Empty;
            if (stageRoot != null) stageRoot.SetActive(QuestJournalPresentation.ShowStage(quest));
            if (objectivesContent != null && objectivePrefab != null)
            {
                objectivesText.text = stage?.Objectives?.Count > 0 ? "Cele" : string.Empty;
                foreach (var objective in stage?.Objectives ?? new List<QuestObjectiveDefinition>())
                {
                    if (objective == null) continue;
                    quest.TryGetObjectiveProgress(objective.ObjectiveId, out var progress);
                    Instantiate(objectivePrefab, objectivesContent).Bind(objective, progress);
                }
            }
            else objectivesText.text = BuildObjectivesText(quest);
            RefreshRewards(quest);
        }

        private void RefreshRewards(QuestInstance quest)
        {
            if (rewardsRoot == null) return;
            var offer = quest.RadiantRewardOffer;
            var xp = offer?.Experience ?? 0; var gold = offer?.Gold ?? 0; var honor = offer?.Honor ?? 0;
            if (offer == null)
                foreach (var reward in quest.CurrentStage?.CompletionRewards ?? new List<QuestRewardDefinition>())
                {
                    if (reward == null) continue;
                    switch (reward.RewardType)
                    {
                        case QuestRewardType.Experience: xp += reward.StandardExperienceLevel > 0
                            ? CharacterProgressionSystem.GetStandardMissionExperience(reward.StandardExperienceLevel) : reward.Amount; break;
                        case QuestRewardType.Gold: gold += reward.Amount; break;
                        case QuestRewardType.Honor: honor += reward.Amount; break;
                    }
                }
            rewardsRoot.SetActive(xp > 0 || gold > 0 || honor > 0 || offer?.BonusItem != null);
            experienceText.text = xp.ToString("N0"); goldText.text = gold.ToString("N0"); honorText.text = honor.ToString("N0");
            experienceText.transform.parent.gameObject.SetActive(xp > 0);
            goldText.transform.parent.gameObject.SetActive(gold > 0);
            honorText.transform.parent.gameObject.SetActive(honor > 0);
            rewardItem.Bind(offer?.BonusItem);
        }

        private void RefreshTrackingButton()
        {
            if (!gameSession.TryGetQuest(selectedQuestId, out var quest)
                || quest.Status != QuestStatus.Active)
            {
                trackButton.gameObject.SetActive(false);
                return;
            }

            var isTracked = gameSession.TrackedQuestId == quest.QuestId;

            trackButton.interactable = true;
            trackButton.gameObject.SetActive(true);
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
                var child = content.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
