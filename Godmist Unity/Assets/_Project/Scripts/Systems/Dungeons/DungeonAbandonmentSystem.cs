using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Dungeons
{
    public static class DungeonAbandonmentSystem
    {
        // Abandoning invalidates the expedition offers, but is not a successful
        // exit: no completion, quest progress, Danger change or boss consumption.
        public static bool TryAbandon(GameSession session, DungeonInstance expectedDungeon)
        {
            if (expectedDungeon == null || session?.ActiveDungeon != expectedDungeon || session.HasCombat)
                return false;

            new DungeonCampService().RemoveSupplies(session);
            if (!session.TryClearActiveDungeon()) return false;
            session.DungeonOffers.CompleteExpedition();
            return true;
        }
    }
}
