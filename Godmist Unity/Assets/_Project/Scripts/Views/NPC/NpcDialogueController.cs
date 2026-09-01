using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.NPC
{
    public class NpcDialogueController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Transform choicesContent;
        [SerializeField] private DialogueChoiceButtonView choiceButtonPrefab;

        [Header("NPC services")]
        // Kept only to hide the legacy scene objects while old scene data is
        // being migrated. No trade interaction is routed through these fields.
        [SerializeField] private Button tradeButton;
        [SerializeField] private TradePanelView tradePanel;
        [SerializeField] private Button servicesButton;
        [SerializeField] private NpcServicesWindowView servicesWindowPrefab;

        private DialogueService dialogueService;
        private GameSession gameSession;
        private NpcServicesWindowView servicesWindow;

        private void Start()
        {
            var gameBootstrap = GameBootstrap.Instance;

            gameSession = gameBootstrap?.Session;
            dialogueService = gameBootstrap?.DialogueService;

            if (gameSession == null || dialogueService == null)
            {
                Debug.LogError(
                    "NpcDialogueController nie znalazl GameSession lub DialogueService.");

                enabled = false;
                return;
            }

            gameSession.NpcInteractionRequested += HandleNpcInteractionRequested;
            gameSession.PlayerParty.ActiveLeaderChanged += HandleActiveLeaderChanged;

            dialogueService.DialogueStarted += HandleDialogueStarted;
            dialogueService.DialogueNodeChanged += HandleDialogueNodeChanged;
            dialogueService.DialogueEnded += HandleDialogueEnded;

            if (servicesButton != null)
            {
                servicesButton.onClick.AddListener(TryOpenServices);
                servicesButton.gameObject.SetActive(false);
            }

            if (tradeButton != null) tradeButton.gameObject.SetActive(false);
            if (tradePanel != null) tradePanel.gameObject.SetActive(false);

            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (gameSession != null)
            {
                gameSession.NpcInteractionRequested -= HandleNpcInteractionRequested;
                gameSession.PlayerParty.ActiveLeaderChanged -= HandleActiveLeaderChanged;
            }

            if (dialogueService != null)
            {
                dialogueService.DialogueStarted -= HandleDialogueStarted;
                dialogueService.DialogueNodeChanged -= HandleDialogueNodeChanged;
                dialogueService.DialogueEnded -= HandleDialogueEnded;
            }

            if (servicesButton != null)
            {
                servicesButton.onClick.RemoveListener(TryOpenServices);
            }
        }

        private void HandleNpcInteractionRequested(NpcInstance npc)
        {
            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration)
            {
                return;
            }

            var initiatorCharacterId = gameSession.PlayerParty.ActiveLeaderId;

            if (string.IsNullOrWhiteSpace(initiatorCharacterId)) return;

            dialogueService.TryStartDialogue(npc, initiatorCharacterId);
        }

        private void HandleDialogueStarted(DialogueSession dialogue)
        {
            gameSession.TrySetMode(GameModeState.Dialogue);
            RefreshPanelVisibility();
        }

        private void HandleDialogueNodeChanged(DialogueSession dialogue)
        {
            RefreshPanelVisibility();
        }

        private void HandleDialogueEnded(NpcInstance npc)
        {
            ClearChoices();
            panelRoot.SetActive(false);
            SetServicesButtonVisible(false);

            if (gameSession != null
                && gameSession.CurrentMode == GameModeState.Dialogue)
            {
                gameSession.TrySetMode(GameModeState.Exploration);
            }
        }

        private void HandleActiveLeaderChanged(
            string previousLeaderId,
            string nextLeaderId)
        {
            RefreshPanelVisibility();
        }

        private void RefreshPanelVisibility()
        {
            var dialogue = dialogueService.ActiveDialogue;

            if (gameSession == null || dialogue == null)
            {
                panelRoot.SetActive(false);
                SetServicesButtonVisible(false);
                return;
            }

            var shouldShowDialogue = gameSession.PlayerParty.ActiveLeaderId
                == dialogue.InitiatorCharacterId;

            panelRoot.SetActive(shouldShowDialogue);

            var isTrader = dialogue.Npc?.Definition?.GetRole<TraderRoleData>() != null;
            var isBlacksmith = dialogue.Npc?.Definition?.GetRole<BlacksmithRoleData>() != null;
            SetServicesButtonVisible(shouldShowDialogue && (isTrader || isBlacksmith));

            if (shouldShowDialogue)
            {
                RefreshDialogue(dialogue);
            }
        }

        private void RefreshDialogue(DialogueSession dialogue)
        {
            speakerText.text = GetSpeakerName(dialogue);
            dialogueText.text = dialogue.CurrentNode.Text;

            ClearChoices();

            foreach (var choice in dialogue.CurrentNode.Choices)
            {
                var choiceView = Instantiate(choiceButtonPrefab, choicesContent);
                choiceView.Bind(choice, TrySelectChoice);
            }
        }

        private void TrySelectChoice(string choiceId)
        {
            var dialogue = dialogueService.ActiveDialogue;

            if (dialogue == null || string.IsNullOrWhiteSpace(choiceId)) return;

            for (var index = 0; index < dialogue.CurrentNode.Choices.Count; index++)
            {
                var choice = dialogue.CurrentNode.Choices[index];

                if (choice.ChoiceId != choiceId) continue;

                dialogueService.TrySelectChoice(index);
                return;
            }

            Debug.LogWarning(
                $"Nie znaleziono wyboru dialogowego o ID '{choiceId}'.");
        }

        private void TryOpenServices()
        {
            var dialogue = dialogueService.ActiveDialogue;
            if (dialogue == null || !EnsureServicesWindow()) return;
            if (!servicesWindow.Open(dialogue.Npc, dialogue.InitiatorCharacterId)) return;
            dialogueService.TryEndDialogue();
        }

        private bool EnsureServicesWindow()
        {
            if (servicesWindow != null) return true;
            var workspaceRoot = UIWorkspaceWindowManager.Instance?.WorkspaceRoot;
            if (servicesWindowPrefab == null || workspaceRoot == null) return false;

            servicesWindow = Instantiate(servicesWindowPrefab, workspaceRoot);
            return servicesWindow != null;
        }

        private void SetServicesButtonVisible(bool isVisible)
        {
            if (servicesButton != null) servicesButton.gameObject.SetActive(isVisible);
        }

        private void ClearChoices()
        {
            for (var index = choicesContent.childCount - 1; index >= 0; index--)
            {
                Destroy(choicesContent.GetChild(index).gameObject);
            }
        }

        private static string GetSpeakerName(DialogueSession dialogue)
        {
            return dialogue.Npc.Definition.DisplayName;
        }
    }
}
