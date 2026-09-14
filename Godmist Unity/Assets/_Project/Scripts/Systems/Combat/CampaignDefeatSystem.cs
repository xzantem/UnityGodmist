using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.Characters;
namespace Godmist.Systems.Combat
{
    public enum CampaignDefeatReason { None, MainCharacterDead, EntirePartyDead, NoLivingRescuer }

    public static class CampaignDefeatSystem
    {
        public static bool ShouldDefeat(GameSession game) =>
            GetDefeatReason(game, out _) != CampaignDefeatReason.None;

        public static CampaignDefeatReason GetDefeatReason(GameSession game, out CharacterInstance deadMain)
        {
            deadMain = null;
            if (game == null) return CampaignDefeatReason.None;
            var members = game.PlayerParty.ActiveMemberIds.Concat(game.PlayerParty.BenchMemberIds)
                .Where(game.CharactersById.ContainsKey).Select(id => game.CharactersById[id]);
            deadMain = members.FirstOrDefault(c => c.Definition?.IsMainCharacter == true
                && c.LifeState == CharacterLifeState.Dead);
            if (deadMain != null) return CampaignDefeatReason.MainCharacterDead;
            var active = game.PlayerParty.ActiveMemberIds.Where(game.CharactersById.ContainsKey)
                .Select(id => game.CharactersById[id]).ToArray();
            if (active.Length == 0 || active.Any(c => c.LifeState == CharacterLifeState.Alive))
                return CampaignDefeatReason.None;
            return active.All(c => c.LifeState == CharacterLifeState.Dead)
                ? CampaignDefeatReason.EntirePartyDead : CampaignDefeatReason.NoLivingRescuer;
        }
        public static void StoreRecoverableCompanions(GameSession game)
        {
            foreach (var id in game.PlayerParty.ActiveMemberIds.ToArray())
            {
                if (!game.CharactersById.TryGetValue(id, out var character)
                    || character.Definition?.IsMainCharacter == true || character.LifeState != CharacterLifeState.Dead) continue;
                if (character.AvailabilityState != CharacterAvailabilityState.StoryLocked)
                    character.AvailabilityState = CharacterAvailabilityState.Recoverable;
                game.PlayerParty.TryMoveActiveMemberToBench(id);
            }
            if (string.IsNullOrEmpty(game.PlayerParty.ActiveLeaderId))
                foreach (var id in game.PlayerParty.ActiveMemberIds)
                    if (game.CharactersById[id].LifeState == CharacterLifeState.Alive)
                    { game.PlayerParty.TrySetActiveLeader(id); break; }
        }
    }
}
