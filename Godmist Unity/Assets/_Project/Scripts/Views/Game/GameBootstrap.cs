using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using UnityEngine;

namespace Godmist.Views.Game
{
    [RequireComponent(typeof(SceneLoader))]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string combatSceneName = "CombatSandbox";
        [SerializeField] private string explorationSceneName = "ExplorationSandbox";

        public static GameBootstrap Instance { get; private set; }
        public GameSession Session { get; private set; }

        private SceneLoader sceneLoader;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Session = new GameSession(GameModeState.Menu);

            sceneLoader = GetComponent<SceneLoader>();
            sceneLoader.Initialize(Session);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartCombat()
        {
            if (sceneLoader == null)
            {
                Debug.LogError("GameBootstrap: brak SceneLoader.");
                return;
            }

            sceneLoader.TryLoadScene(combatSceneName, GameModeState.Combat);
        }

        public void StartExploration()
        {
            if (sceneLoader == null)
            {
                Debug.LogError("GameBootstrap: brak SceneLoader.");
                return;
            }

            sceneLoader.TryLoadScene(explorationSceneName, GameModeState.Exploration);
        }
    }
}