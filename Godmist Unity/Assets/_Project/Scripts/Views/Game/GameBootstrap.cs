using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Views.Game
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string combatSceneName = "CombatSandbox";
        [SerializeField] private bool loadCombatOnStart = true;

        private bool isLoading;

        private void Start()
        {
            if (loadCombatOnStart)
            {
                StartCombat();
            }
        }

        public void StartCombat()
        {
            if (isLoading)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(combatSceneName))
            {
                Debug.LogError("GameBootstrap: brak nazwy sceny walki.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(combatSceneName))
            {
                Debug.LogError(
                    $"GameBootstrap: scena '{combatSceneName}' " +
                    "nie jest dodana do Build Settings.");
                return;
            }

            isLoading = true;
            SceneManager.LoadScene(combatSceneName);
        }
    }
}