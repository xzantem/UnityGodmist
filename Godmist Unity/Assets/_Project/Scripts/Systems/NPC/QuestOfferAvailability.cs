using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.Quests;

namespace Godmist.Systems.NPC
{
    public enum NpcQuestMarkerState { None, Offer, Reward }

    public static class QuestOfferAvailability
    {
        public static NpcQuestMarkerState GetMarkerState(GameSession session, NpcInstance npc)
        {
            if (session == null || npc == null) return NpcQuestMarkerState.None;
            // The guard's current job offers and turn-ins belong to the mission board.
            // Its legacy introduction dialogue must not leave a permanent offer marker.
            if (npc.NpcId == "npc.arungard.questgiver_01")
            {
                if (CatacombsMissionBoard.HasReward(session)) return NpcQuestMarkerState.Reward;
                return CatacombsMissionBoard.HasOffer(session) ? NpcQuestMarkerState.Offer : NpcQuestMarkerState.None;
            }
            return HasOffer(session, npc.Definition?.BaseDialogue) ? NpcQuestMarkerState.Offer : NpcQuestMarkerState.None;
        }

        public static bool HasOffer(GameSession session, DialogueDefinition dialogue)
        {
            if (session == null || dialogue == null) return false;
            var pending = new Stack<string>();
            var visited = new HashSet<string>();
            pending.Push(dialogue.StartNodeId);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (string.IsNullOrWhiteSpace(id) || !visited.Add(id)) continue;
                var node = dialogue.FindNode(id);
                if (node?.Choices == null) continue;
                foreach (var choice in node.Choices)
                {
                    if (choice == null) continue;
                    var action = choice.QuestAction;
                    if (action != null && action.StartQuest
                        && session.TryGetQuest(action.QuestId, out var quest)
                        && (quest.Status == QuestStatus.Available
                            || quest.Status == QuestStatus.Unavailable && action.MakeQuestAvailable)) return true;
                    pending.Push(choice.NextNodeId);
                }
            }
            return false;
        }
    }
}
