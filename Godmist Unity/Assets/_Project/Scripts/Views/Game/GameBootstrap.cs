using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Data.Dungeons;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Game
{
    [RequireComponent(typeof(SceneLoader))]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string combatSceneName = "CombatSandbox";
        [SerializeField] private string explorationSceneName = "ExplorationSandbox";
        [SerializeField] private string dungeonSceneName = "DungeonSandbox";

        public static GameBootstrap Instance { get; private set; }
        public GameSession Session { get; private set; }

        private SceneLoader sceneLoader;
        private readonly DungeonGeneratorService dungeonGenerator = new();
        private readonly DungeonEncounterRewardPreparationSystem rewardPreparationSystem = new();
        private readonly DungeonEncounterPlacementSystem encounterPlacementSystem = new();
        private readonly DungeonObjectPlacementSystem objectPlacementSystem = new();

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

        public bool TryReturnFromCombat()
        {
            if (sceneLoader == null || Session == null) return false;

            var destinationSceneName = Session.ActiveDungeon != null ? dungeonSceneName : explorationSceneName;

            return sceneLoader.TryLoadScene(destinationSceneName, GameModeState.Exploration);
        }

        public bool TryEnterDungeon(
            DungeonDefinition definition,
            int seed)
        {
            if (definition == null || !definition.IsProcedural)
            {
                Debug.LogError("GameBootstrap: wejscie wymaga proceduralnego DungeonDefinition.");

                return false;
            }

            if (Session == null || Session.ActiveDungeon != null) return false;

            try
            {
                var dungeon = dungeonGenerator.Generate(definition, seed);

                encounterPlacementSystem.Place(dungeon);
                objectPlacementSystem.Place(dungeon);
                rewardPreparationSystem.Prepare(dungeon);

                if (!Session.TrySetActiveDungeon(dungeon)) return false;

                if (sceneLoader.TryLoadScene(dungeonSceneName, GameModeState.Exploration)) return true;

                Session.TryClearActiveDungeon();
                return false;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public bool TryRestartAfterDefeat()
        {
            if (sceneLoader == null)
            {
                Debug.LogError("GameBootstrap: brak SceneLoader.");
                return false;
            }

            Session = new GameSession(GameModeState.Menu);
            sceneLoader.Initialize(Session);

            return sceneLoader.TryLoadScene(
                explorationSceneName,
                GameModeState.Exploration);
        }

        public bool TryExitDungeon()
        {
            if (Session == null || Session.ActiveDungeon == null) return false;

            var dungeon = Session.ActiveDungeon;

            if (!sceneLoader.TryLoadScene(explorationSceneName, GameModeState.Exploration)) return false;

            dungeon.TryMarkCompleted();
            Session.TryClearActiveDungeon();

            return true;
        }
    }
}