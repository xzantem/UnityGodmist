using Godmist.Views.Game;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI
{
    public sealed class BootstrapMenuView : MonoBehaviour
    {
        [SerializeField] private Button explorationButton;
        [SerializeField] private Button combatButton;
        private void Awake()
        {
            explorationButton.onClick.AddListener(StartExploration);
            combatButton.onClick.AddListener(StartCombat);
        }
        private void StartCombat() => GameBootstrap.Instance?.StartCombat();
        private void StartExploration() => GameBootstrap.Instance?.StartExploration();
        private void OnDestroy()
        {
            explorationButton?.onClick.RemoveListener(StartExploration);
            combatButton?.onClick.RemoveListener(StartCombat);
        }
    }
}
