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
        public void CloseForController() => dialogueService?.TryEndDialogue();
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Transform choicesContent;
        [SerializeField] private DialogueChoiceButtonView choiceButtonPrefab;
        [SerializeField] private NpcDialoguePanelView dialoguePanel;
        [SerializeField] private CombatEdgeDrawer partyDrawer;
        [SerializeField] private GameObject partyDrawerToggle;

        [Header("NPC services")]
        // Kept only to hide the legacy scene objects while old scene data is
        // being migrated. No trade interaction is routed through these fields.
        [SerializeField] private Button tradeButton;
        [SerializeField] private TradePanelView tradePanel;
        // Retained to hide the old authored footer button; services now live in the answer list.
        [SerializeField] private Button servicesButton;
        [SerializeField] private NpcServicesWindowView servicesWindowPrefab;
        // Retained to hide the old footer; mission access now lives in the answer list.
        [SerializeField] private Button missionBoardButton;
        [SerializeField] private CatacombsMissionBoardView missionBoardPrefab;

        private DialogueService dialogueService;
        private GameSession gameSession;
        private NpcServicesWindowView servicesWindow;
        private CatacombsMissionBoardView missionBoard;
        private bool capturedHud, previousExpanded, previousToggleVisible;

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

            if (tradeButton != null) tradeButton.gameObject.SetActive(false);
            if (tradePanel != null) tradePanel.gameObject.SetActive(false);

            SetPanelVisible(false);
        }

        private void OnDisable() => SetPanelVisible(false);

        private void OnDestroy()
        {
            RestoreHud();
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
            SetPanelVisible(false);

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
                SetPanelVisible(false);
                return;
            }

            var shouldShowDialogue = gameSession.PlayerParty.ActiveLeaderId
                == dialogue.InitiatorCharacterId;

            SetPanelVisible(shouldShowDialogue);

            if (shouldShowDialogue)
            {
                RefreshDialogue(dialogue);
            }
        }

        private void RefreshDialogue(DialogueSession dialogue)
        {
            speakerText.text = GetSpeakerName(dialogue);
            dialogueText.text = dialogue.CurrentNode.Text;
            if (dialoguePanel != null) dialoguePanel.BindNpc(dialogue.Npc.Definition);

            ClearChoices();

            if (dialogue.Npc?.NpcId == "npc.arungard.questgiver_01")
            {
                var missionsChoice = Instantiate(choiceButtonPrefab, choicesContent);
                missionsChoice.BindAction("Masz dla mnie jakieś zlecenia?", TryOpenMissionBoard);
            }

            if (HasServices(dialogue.Npc?.Definition))
            {
                var servicesChoice = Instantiate(choiceButtonPrefab, choicesContent);
                servicesChoice.BindAction("Chcę skorzystać z twoich usług", TryOpenServices);
            }

            foreach (var choice in dialogue.CurrentNode.Choices)
            {
                var choiceView = Instantiate(choiceButtonPrefab, choicesContent);
                choiceView.Bind(choice, TrySelectChoice);
            }
            if (dialoguePanel != null) dialoguePanel.RefreshLayout();
        }

        private void SetPanelVisible(bool visible)
        {
            if (servicesButton != null) servicesButton.gameObject.SetActive(false);
            if (missionBoardButton != null) missionBoardButton.gameObject.SetActive(false);
            if (visible && !capturedHud)
            {
                capturedHud = true;
                if (partyDrawer != null) { previousExpanded = partyDrawer.IsExpanded; partyDrawer.SetExpanded(false, true); }
                if (partyDrawerToggle != null) { previousToggleVisible = partyDrawerToggle.activeSelf; partyDrawerToggle.SetActive(false); }
                if (UIWorkspaceTooltipView.ActiveInstance != null) UIWorkspaceTooltipView.ActiveInstance.Hide();
            }
            if (!visible) RestoreHud();
            if (panelRoot != null) panelRoot.SetActive(visible);
        }

        private void RestoreHud()
        {
            if (!capturedHud) return;
            if (partyDrawer != null) partyDrawer.SetExpanded(previousExpanded, true);
            if (partyDrawerToggle != null) partyDrawerToggle.SetActive(previousToggleVisible);
            capturedHud = false;
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

        private void TryOpenMissionBoard()
        {
            var dialogue = dialogueService.ActiveDialogue;
            if (dialogue == null || !EnsureMissionBoard()) return;
            if (!missionBoard.Open(dialogue.Npc)) return;
            dialogueService.TryEndDialogue();
        }

        private bool EnsureMissionBoard()
        {
            if (missionBoard != null) return true;
            var workspaceRoot = UIWorkspaceWindowManager.Instance?.WorkspaceRoot;
            if (missionBoardPrefab == null || workspaceRoot == null) return false;
            missionBoard = Instantiate(missionBoardPrefab, workspaceRoot);
            return missionBoard != null;
        }

        private static bool HasServices(NpcDefinition definition) => definition != null
            && (definition.GetRole<TraderRoleData>() != null
                || definition.GetRole<BlacksmithRoleData>() != null
                || definition.GetRole<AlchemistRoleData>() != null
                || definition.GetRole<EnchanterRoleData>() != null
                || definition.GetRole<PriestRoleData>() != null);

        private void ClearChoices()
        {
            for (var index = choicesContent.childCount - 1; index >= 0; index--)
            {
                var choice = choicesContent.GetChild(index).gameObject;
                choice.SetActive(false);
                if (Application.isPlaying) Destroy(choice);
                else DestroyImmediate(choice);
            }
        }

        private string GetSpeakerName(DialogueSession dialogue)
        {
            var id = dialogue.CurrentNode?.SpeakerId;
            if (!string.IsNullOrWhiteSpace(id) && gameSession != null
                && gameSession.CharactersById.TryGetValue(id, out var character))
                return character.Definition?.DisplayName ?? character.CharacterId;
            return dialogue.Npc.Definition.DisplayName;
        }
    }
}
