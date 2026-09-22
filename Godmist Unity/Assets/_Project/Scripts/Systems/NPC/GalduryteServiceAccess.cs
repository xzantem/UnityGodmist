using System.Linq;
using Godmist.Runtime.Game;

namespace Godmist.Systems.NPC
{
    public static class GalduryteServiceAccess
    {
        public const int RequiredLevel = 11;
        public const string LockedMessage = "Usługi galdurytów wymagają poziomu 11 dowolnej postaci w drużynie lub rezerwie.";
        public static bool IsUnlocked(GameSession session) => session != null
            && session.CharactersById.Values.Any(character => character != null && character.Level >= RequiredLevel);
    }
}
