using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;

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

        private DialogueService dialogueService;
        private GameSession gameSession;

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

            gameSession.NpcInteractionRequested +=
                HandleNpcInteractionRequested;

            gameSession.PlayerParty.ActiveLeaderChanged +=
                HandleActiveLeaderChanged;

            dialogueService.DialogueStarted += HandleDialogueStarted;
            dialogueService.DialogueNodeChanged += HandleDialogueNodeChanged;
            dialogueService.DialogueEnded += HandleDialogueEnded;

            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (gameSession != null)
            {
                gameSession.NpcInteractionRequested -=
                    HandleNpcInteractionRequested;

                gameSession.PlayerParty.ActiveLeaderChanged -=
                    HandleActiveLeaderChanged;
            }

            if (dialogueService != null)
            {
                dialogueService.DialogueStarted -= HandleDialogueStarted;
                dialogueService.DialogueNodeChanged -=
                    HandleDialogueNodeChanged;

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

            var initiatorCharacterId =
                gameSession.PlayerParty.ActiveLeaderId;

            if (string.IsNullOrWhiteSpace(initiatorCharacterId))
            {
                return;
            }

            dialogueService.TryStartDialogue(
                npc,
                initiatorCharacterId);
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
                return;
            }

            var shouldShowDialogue =
                gameSession.PlayerParty.ActiveLeaderId ==
                dialogue.InitiatorCharacterId;

            panelRoot.SetActive(shouldShowDialogue);

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
                var choiceView = Instantiate(
                    choiceButtonPrefab,
                    choicesContent);

                choiceView.Bind(choice, TrySelectChoice);
            }
        }

        private void TrySelectChoice(string choiceId)
        {
            var dialogue = dialogueService.ActiveDialogue;

            if (dialogue == null || string.IsNullOrWhiteSpace(choiceId))
            {
                return;
            }

            for (var index = 0;
                 index < dialogue.CurrentNode.Choices.Count;
                 index++)
            {
                var choice = dialogue.CurrentNode.Choices[index];

                if (choice.ChoiceId != choiceId)
                {
                    continue;
                }

                dialogueService.TrySelectChoice(index);
                return;
            }

            Debug.LogWarning(
                $"Nie znaleziono wyboru dialogowego o ID '{choiceId}'.");
        }

        private void ClearChoices()
        {
            for (var index = choicesContent.childCount - 1;
                 index >= 0;
                 index--)
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