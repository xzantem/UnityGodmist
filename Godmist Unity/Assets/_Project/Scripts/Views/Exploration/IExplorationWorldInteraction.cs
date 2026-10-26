using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public interface IExplorationWorldInteraction : IInteractable
    {
        ExplorationPartyController InteractionParty { get; }
        DungeonFloorInstance InteractionFloor { get; }
        float InteractionRadius { get; }
        KeyCode InteractionKey { get; }
        bool CanRequestInteraction { get; }
        bool CanInteractFrom(Vector2 position);
    }
}
