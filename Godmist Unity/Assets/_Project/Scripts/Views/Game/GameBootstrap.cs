using System;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.Dungeons;
using Godmist.Systems.NPC;
using Godmist.Systems.Quests;
using UnityEngine;

namespace Godmist.Views.Game
{
    [RequireComponent(typeof(SceneLoader))]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string combatSceneName = "CombatSandbox";
        [SerializeField] private string explorationSceneName = "Arungard";
        [SerializeField] private string dungeonSceneName = "DungeonSandbox";

        public static GameBootstrap Instance { get; private set; }

        public GameSession Session { get; private set; }
        public DialogueService DialogueService { get; private set; }
        public QuestService QuestService { get; private set; }
        public TradeService TradeService { get; private set; }

        public event Action<DungeonInstance> DungeonExited;

        private SceneLoader sceneLoader;
        private QuestObjectiveProgressSystem questObjectiveProgressSystem;
        private DialogueQuestActionSystem dialogueQuestActionSystem;

        private readonly DungeonGeneratorService dungeonGenerator = new();
        private readonly DungeonEncounterRewardPreparationSystem
            rewardPreparationSystem = new();

        private readonly DungeonEncounterPlacementSystem
            encounterPlacementSystem = new();

        private readonly DungeonObjectPlacementSystem
            objectPlacementSystem = new();

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

            InitializeRuntimeServices();
        }

        private void OnDestroy()
        {
            UnsubscribeRuntimeServices();

            if (Instance == this)
            {
                Instance = null;
            }
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

            sceneLoader.TryLoadScene(
                explorationSceneName,
                GameModeState.Exploration);
        }

        public bool TryReturnFromCombat()
        {
            if (sceneLoader == null || Session == null)
            {
                return false;
            }

            var destinationSceneName = Session.ActiveDungeon != null
                ? dungeonSceneName
                : explorationSceneName;

            return sceneLoader.TryLoadScene(
                destinationSceneName,
                GameModeState.Exploration);
        }

        public bool TryEnterDungeon(
            DungeonDefinition definition,
            int seed)
        {
            if (definition == null || !definition.IsProcedural)
            {
                Debug.LogError(
                    "GameBootstrap: wejscie wymaga proceduralnego DungeonDefinition.");

                return false;
            }

            if (Session == null || Session.ActiveDungeon != null)
            {
                return false;
            }

            try
            {
                var dungeon = dungeonGenerator.Generate(definition, seed);

                encounterPlacementSystem.Place(dungeon);
                objectPlacementSystem.Place(dungeon);
                rewardPreparationSystem.Prepare(dungeon);

                if (!Session.TrySetActiveDungeon(dungeon))
                {
                    return false;
                }

                if (sceneLoader.TryLoadScene(
                    dungeonSceneName,
                    GameModeState.Exploration))
                {
                    return true;
                }

                Session.TryClearActiveDungeon();
                return false;
            }
            catch (Exception exception)
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

            UnsubscribeRuntimeServices();

            Session = new GameSession(GameModeState.Menu);
            sceneLoader.Initialize(Session);

            InitializeRuntimeServices();

            return sceneLoader.TryLoadScene(
                explorationSceneName,
                GameModeState.Exploration);
        }

        public bool TryExitDungeon()
        {
            if (Session == null || Session.ActiveDungeon == null)
            {
                return false;
            }

            var dungeon = Session.ActiveDungeon;

            if (!sceneLoader.TryLoadScene(
                explorationSceneName,
                GameModeState.Exploration))
            {
                return false;
            }

            dungeon.TryMarkCompleted();

            questObjectiveProgressSystem?.ReportDungeonExited(
                dungeon.DungeonId);

            DungeonExited?.Invoke(dungeon);

            Session.TryClearActiveDungeon();
            return true;
        }

        private void InitializeRuntimeServices()
        {
            DialogueService = new DialogueService();
            QuestService = new QuestService(Session);
            TradeService = new TradeService();

            dialogueQuestActionSystem = new DialogueQuestActionSystem(
                DialogueService,
                QuestService);

            questObjectiveProgressSystem =
                new QuestObjectiveProgressSystem(
                    Session,
                    QuestService);

            DialogueService.DialogueEnded += HandleDialogueEnded;
        }

        private void UnsubscribeRuntimeServices()
        {
            dialogueQuestActionSystem?.Dispose();
            dialogueQuestActionSystem = null;

            if (DialogueService != null)
            {
                DialogueService.DialogueEnded -= HandleDialogueEnded;
            }
        }

        private void HandleDialogueEnded(NpcInstance npc)
        {
            if (npc == null)
            {
                return;
            }

            questObjectiveProgressSystem?.ReportNpcDialogueCompleted(
                npc.NpcId);
        }
    }
}
