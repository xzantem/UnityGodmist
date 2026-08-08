using System;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;
using Godmist.Systems.Quests;

namespace Godmist.Systems.NPC
{
    public class DialogueQuestActionSystem : IDisposable
    {
        private readonly DialogueService dialogueService;
        private readonly QuestService questService;

        public DialogueQuestActionSystem(
            DialogueService nextDialogueService,
            QuestService nextQuestService)
        {
            dialogueService = nextDialogueService
                ?? throw new ArgumentNullException(
                    nameof(nextDialogueService));

            questService = nextQuestService
                ?? throw new ArgumentNullException(
                    nameof(nextQuestService));

            dialogueService.DialogueChoiceSelected +=
                HandleDialogueChoiceSelected;
        }

        public void Dispose()
        {
            dialogueService.DialogueChoiceSelected -=
                HandleDialogueChoiceSelected;
        }

        private void HandleDialogueChoiceSelected(
            DialogueSession dialogue,
            DialogueChoiceData choice)
        {
            var action = choice?.QuestAction;

            if (action == null
                || string.IsNullOrWhiteSpace(action.QuestId))
            {
                return;
            }

            if (action.MakeQuestAvailable)
            {
                questService.TryMakeQuestAvailable(action.QuestId);
            }

            if (action.StartQuest)
            {
                questService.TryStartQuest(action.QuestId);
            }

            if (!string.IsNullOrWhiteSpace(action.QuestBranchId))
            {
                questService.TryRecordQuestBranch(
                    action.QuestId,
                    action.QuestBranchId);
            }
        }
    }
}