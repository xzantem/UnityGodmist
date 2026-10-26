using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Quests
{
    public static class CatacombsMissionBoard
    {
        public const string KillQuestId = "quest.catacombs.radiant.kill";
        public const string ExpeditionQuestId = "quest.catacombs.radiant.expedition";
        public const string BossQuestId = "quest.catacombs.radiant.boss";
        public const string BossDungeonId = "dungeon.catacombs.boss_expedition";

        public static readonly string[] QuestIds =
            { KillQuestId, ExpeditionQuestId, BossQuestId };

        public static bool HasBossOffer(GameSession session) =>
            session?.DungeonOffers?.Offers?.Any(
                offer => offer.DefinitionId == BossDungeonId) == true;

        public static int BossOfferLevel(GameSession session) =>
            session?.DungeonOffers?.Offers?.FirstOrDefault(
                offer => offer.DefinitionId == BossDungeonId)?.FixedLevel ?? 0;

        public static bool CanOffer(GameSession session, string questId)
        {
            if (session == null || !QuestIds.Contains(questId)
                || !session.TryGetQuest(questId, out var quest)
                || quest.Definition?.QuestType != QuestType.Radiant
                || quest.Status is not (QuestStatus.Unavailable or QuestStatus.Available))
                return false;

            return quest.BoardContract!=null || questId != BossQuestId || HasBossOffer(session);
        }

        public static bool HasOffer(GameSession session) =>
            session != null && QuestIds.Any(id => CanOffer(session, id));

        public static bool HasReward(GameSession session) =>
            session != null && QuestIds.Any(id =>
                session.TryGetQuest(id, out var quest)
                && quest.Status == QuestStatus.Completed);

        public static bool HasAction(GameSession session) =>
            HasOffer(session) || HasReward(session);
    }
}
