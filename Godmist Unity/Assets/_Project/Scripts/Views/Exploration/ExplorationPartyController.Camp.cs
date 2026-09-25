using Godmist.Core.Enums;
using Godmist.Systems.Dungeons;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private readonly DungeonCampService campService = new();

        public bool CanCamp(out string reason)
        {
            if (traversalFloor == null || traversalMapView == null || GameBootstrap.Instance?.IsSceneLoading == true)
            {
                reason = "Obóz można rozbić tylko w lochu.";
                return false;
            }
            if (!campService.CanCamp(gameSession, traversalFloor, out reason)) return false;
            var leader = GetActiveLeaderView();
            if (leader == null) { reason = "Brak aktywnego przywódcy."; return false; }
            // A normal following formation must fit, but detached/distant members cannot be healed remotely.
            var reach = Mathf.Max(1f, followerDistance) * Mathf.Max(1, gameSession.PlayerParty.ActiveMemberIds.Count);
            foreach (var id in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (gameSession.CharactersById[id].LifeState != CharacterLifeState.Alive) continue;
                if (!viewsById.TryGetValue(id, out var view) || !view.isActiveAndEnabled
                    || Vector2.Distance(leader.Position, view.Position) > reach
                    || view.DungeonPosition.IsOnStairs || leader.DungeonPosition.IsOnStairs
                    || view.DungeonPosition.Surface.Level != leader.DungeonPosition.Surface.Level
                    || !FindTraversalPath(view, leader.DungeonPosition, out var path) || path.Count > reach + 2)
                {
                    reason = "Zbierz żywych członków drużyny przy przywódcy na tym samym poziomie podłogi.";
                    return false;
                }
            }
            return true;
        }

        // Empty result means success; both inventory and ground menu use this entry point.
        public string TryCamp(string carrierId = null, string kitInstanceId = null)
        {
            if (!CanCamp(out var reason)) return reason;
            if (!campService.TryCamp(gameSession, traversalFloor, out reason, carrierId, kitInstanceId)) return reason;
            CancelMovement();
            Godmist.Views.UI.Workspace.CharacterInfoWindowView.RefreshOpenWindows(gameSession);
            GameBootstrap.Instance?.RequestAutosave("Rozbicie obozu");
            return string.Empty;
        }
    }
}
