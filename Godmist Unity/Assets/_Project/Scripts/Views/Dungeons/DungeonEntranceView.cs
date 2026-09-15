using Godmist.Data.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonEntranceView : MonoBehaviour
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private DungeonDefinition dungeonDefinition;
        [SerializeField] private int seed = 12345;
        [SerializeField] private Godmist.Views.UI.Workspace.DungeonSelectorWindowView selector;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private void Update()
        {
            if (DevConsoleView.IsOpen || Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedTextInput()) return;
            
            if (!Input.GetKeyDown(interactionKey)) return;

            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null
                || gameBootstrap.Session.CurrentMode != Core.Enums.GameModeState.Exploration
                || partyController == null
                || partyController.ActiveLeaderTransform == null)
            {
                return;
            }

            var offset = partyController.ActiveLeaderTransform.position
                         - transform.position;

            if (offset.sqrMagnitude > interactionRadius * interactionRadius)
            {
                return;
            }

            if(selector!=null) selector.Open(gameBootstrap,dungeonDefinition);
            else gameBootstrap.TryEnterDungeon(dungeonDefinition, seed);
        }
    }
}