using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Configuration;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Systems.Quests;
using Godmist.Systems.SaveLoad;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Views.Game
{
    [RequireComponent(typeof(SceneLoader))]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string combatSceneName = "CombatSandbox";
        [SerializeField] private string explorationSceneName = "Arungard";
        [SerializeField] private string dungeonSceneName = "DungeonSandbox";

        [Header("Content")]
        [SerializeField] private GameContentCatalog contentCatalog;

        [Header("Configuration")]
        [SerializeField] private GameDefines gameDefines;

        public static GameBootstrap Instance { get; private set; }

        public GameSession Session { get; private set; }
        public DialogueService DialogueService { get; private set; }
        public QuestService QuestService { get; private set; }
        public TradeService TradeService { get; private set; }
        public NpcEconomyService NpcEconomyService { get; private set; }
        public SaveManager SaveManager { get; private set; }
        public GameContentCatalog ContentCatalog => contentCatalog;
        public GameDefines GameDefines => gameDefines;
        public bool IsDungeonScene => SceneManager.GetActiveScene().name == dungeonSceneName;

        public event Action<DungeonInstance> DungeonExited;

        private SceneLoader sceneLoader;
        private QuestObjectiveProgressSystem questObjectiveProgressSystem;
        private DialogueQuestActionSystem dialogueQuestActionSystem;
        private bool autosaveRequested;
        private string pendingAutosaveReason = string.Empty;

        private readonly DungeonGeneratorService dungeonGenerator = new();
        private readonly DungeonEncounterRewardPreparationSystem
            rewardPreparationSystem = new();

        private readonly DungeonEncounterPlacementSystem
            encounterPlacementSystem = new();

        private readonly DungeonObjectPlacementSystem
            objectPlacementSystem = new();
        private CombatSessionFactory debugCombatSessionFactory;

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
            SaveManager = new SaveManager();

            GameDefinesRuntime.Configure(
                gameDefines != null
                    ? gameDefines
                    : Resources.Load<GameDefines>("GameDefines"));

            sceneLoader = GetComponent<SceneLoader>();
            sceneLoader.Initialize(Session);
            debugCombatSessionFactory = new CombatSessionFactory(
                rewardSystem: new CombatRewardSystem(
                    lootTableSystem: new LootTableSystem(
                        new System.Random(),
                        contentCatalog?.EquipmentPartDefinitions)));

            InitializeRuntimeServices();
        }

        private void Update()
        {
            if (!autosaveRequested)
            {
                return;
            }

            autosaveRequested = false;

            if (TrySaveAutosave(
                    pendingAutosaveReason,
                    out var message))
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }

            pendingAutosaveReason = string.Empty;
        }

        private void OnDestroy()
        {
            UnsubscribeRuntimeServices();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool StartCombat()
        {
            if (sceneLoader == null)
            {
                Debug.LogError("GameBootstrap: brak SceneLoader.");
                return false;
            }

            return sceneLoader.TryLoadScene(
                combatSceneName,
                GameModeState.Combat);
        }

        public bool TryStartDebugCombat(
            string encounterId,
            out string message)
        {
            message = string.Empty;

            if (Session == null)
            {
                message = "Brak aktywnej GameSession.";
                return false;
            }

            if (Session.ActiveCombat != null)
            {
                message = "Walczysz juz w aktywnej sesji.";
                return false;
            }

            if (contentCatalog == null)
            {
                message = "Brak GameContentCatalog.";
                return false;
            }

            if (!contentCatalog.TryGetEncounter(
                    encounterId,
                    out var encounter))
            {
                message = $"Nie znaleziono encountera: {encounterId}";
                return false;
            }

            if (!debugCombatSessionFactory.TryCreate(
                    Session,
                    encounter,
                    out var combatSession))
            {
                message =
                    "Nie udalo sie utworzyc walki. Sprawdz aktywna druzyne i EncounterDefinition.";

                return false;
            }

            combatSession.IsDebugSession = true;

            if (!Session.TrySetActiveCombat(combatSession))
            {
                message = "Nie udalo sie ustawic aktywnej walki.";
                return false;
            }

            if (!StartCombat())
            {
                Session.TryClearActiveCombat();

                message = "Nie udalo sie zaladowac sceny walki.";
                return false;
            }

            message = $"Uruchomiono debugowa walke: {encounter.DisplayName}.";
            return true;
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

        public bool TrySaveQuick(out string message)
        {
            if (SaveManager == null)
            {
                message = "GameBootstrap: brak SaveManager.";
                return false;
            }

            return SaveManager.TrySaveQuick(Session, out message);
        }

        public bool TryLoadQuick(out string message)
        {
            message = string.Empty;

            if (Session?.ActiveCombat != null)
            {
                message = "Nie można wczytać gry podczas walki.";
                return false;
            }

            if (SaveManager == null)
            {
                message = "GameBootstrap: brak SaveManager.";
                return false;
            }

            if (contentCatalog == null)
            {
                message = "GameBootstrap: brak GameContentCatalog.";
                return false;
            }

            if (!SaveManager.TryLoadQuick(
                    Session.CampaignId,
                    contentCatalog,
                    out var restoredSession,
                    out message))
            {
                return false;
            }

            return TryReplaceSession(restoredSession, out message);
        }

        public bool TryLoadSave(
            GameSaveSummary summary,
            out string message)
        {
            message = string.Empty;

            if (Session?.ActiveCombat != null)
            {
                message = "Nie można wczytać gry podczas walki.";
                return false;
            }

            if (SaveManager == null)
            {
                message = "GameBootstrap: brak SaveManager.";
                return false;
            }

            if (contentCatalog == null)
            {
                message = "GameBootstrap: brak GameContentCatalog.";
                return false;
            }

            if (!SaveManager.TryLoadSave(
                    summary,
                    contentCatalog,
                    out var restoredSession,
                    out message))
            {
                return false;
            }

            return TryReplaceSession(restoredSession, out message);
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
            int seed,
            int targetFloorIndex = 0,
            string targetRoomId = null)
        {
            if (definition == null || !definition.IsProcedural)
            {
                Debug.LogError(
                    "GameBootstrap: wejscie wymaga proceduralnego DungeonDefinition.");

                return false;
            }

            if (Session == null)
            {
                return false;
            }

            // A return scroll leaves the generated dungeon in the session so
            // its cleared encounters, loot and party position can be resumed.
            // The entrance must therefore reopen that same instance instead of
            // rejecting it as if the player were trying to create a second
            // dungeon. A different entrance remains blocked until the current
            // dungeon is completed or otherwise cleared.
            if (Session.ActiveDungeon != null)
            {
                if (!string.Equals(
                        Session.ActiveDungeon.DungeonId,
                        definition.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log(
                        "Najpierw zakończ lub wznow istniejący loch przed wejściem do innego.");
                    return false;
                }

                if (!sceneLoader.TryLoadScene(
                        dungeonSceneName,
                        GameModeState.Exploration))
                {
                    return false;
                }

                RequestAutosave("Wznowienie lochu");
                return true;
            }

            try
            {
                var dungeon = dungeonGenerator.Generate(definition, seed);

                encounterPlacementSystem.Place(dungeon);
                objectPlacementSystem.Place(
                    dungeon,
                    contentCatalog?.EquipmentPartDefinitions);
                rewardPreparationSystem.Prepare(
                    dungeon,
                    contentCatalog?.EquipmentPartDefinitions);

                if (targetFloorIndex != 0
                    && !dungeon.TrySetCurrentFloorIndex(targetFloorIndex))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(targetRoomId)
                    && !TrySetDebugDungeonRoomPosition(dungeon, targetRoomId))
                {
                    return false;
                }

                if (!Session.TrySetActiveDungeon(dungeon))
                {
                    return false;
                }

                if (sceneLoader.TryLoadScene(
                        dungeonSceneName,
                        GameModeState.Exploration))
                {
                    RequestAutosave("Wejście do lochu");
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

            if (!AreFinalBossEncountersCleared(dungeon))
            {
                Debug.Log("Nie można opuścić lochu przed pokonaniem bossa.");
                return false;
            }

            if (!sceneLoader.TryLoadScene(
                    explorationSceneName,
                    GameModeState.Exploration))
            {
                return false;
            }

            dungeon.TryMarkCompleted();
            NpcEconomyService?.TryApplyDungeonProgress(
                Session,
                dungeon,
                out _,
                out _);

            questObjectiveProgressSystem?.ReportDungeonExited(
                dungeon.DungeonId);

            DungeonExited?.Invoke(dungeon);

            Session.TryClearActiveDungeon();
            RequestAutosave("Wyjście z lochu");
            return true;
        }

        private static bool TrySetDebugDungeonRoomPosition(
            DungeonInstance dungeon,
            string roomId)
        {
            if (dungeon == null
                || string.IsNullOrWhiteSpace(roomId)
                || !dungeon.TryGetCurrentFloor(out var floor)
                || floor.Map == null
                || !floor.TryGetRoom(roomId, out _))
            {
                return false;
            }

            foreach (var cell in floor.Map.FloorCells)
            {
                if (!floor.Map.TryGetRoomId(cell, out var mappedRoomId)
                    || !string.Equals(mappedRoomId, roomId, StringComparison.Ordinal))
                {
                    continue;
                }

                dungeon.SetPlayerPosition(cell);
                return true;
            }

            return false;
        }

        public bool TryReturnToArungardFromDungeon()
        {
            if (Session?.ActiveDungeon == null || Session.ActiveCombat != null) return false;

            // Scene loading is asynchronous. Capture the live leader position
            // before the dungeon scene is destroyed so resuming the same
            // instance cannot fall back to a stale room position.
            FindAnyObjectByType<Godmist.Views.Exploration.ExplorationPartyController>()
                ?.PersistCurrentPosition();

            if (!sceneLoader.TryLoadScene(explorationSceneName, GameModeState.Exploration)) return false;

            NpcEconomyService?.TryApplyDungeonProgress(
                Session,
                Session.ActiveDungeon,
                out _,
                out _);
            RequestAutosave("Return scroll");
            return true;
        }

        private static bool AreFinalBossEncountersCleared(
            DungeonInstance dungeon)
        {
            var bossDefinition = dungeon?.Definition?.BossEncounterDefinition;

            if (bossDefinition == null) return true;
            if (!dungeon.TryGetCurrentFloor(out var floor)) return false;

            foreach (var encounter in floor.Encounters)
            {
                if (encounter?.Definition == bossDefinition
                    && !dungeon.IsEncounterCleared(encounter.InstanceId))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryChangeDungeonFloor(
            int targetFloorIndex,
            Vector2 targetLeaderPosition,
            IReadOnlyDictionary<string, Vector2> targetPartyPositions)
        {
            if (Session?.ActiveDungeon == null
                || targetPartyPositions == null
                || sceneLoader == null)
            {
                return false;
            }

            var dungeon = Session.ActiveDungeon;
            var previousFloorIndex = dungeon.CurrentFloorIndex;
            var previousPosition = dungeon.PlayerPosition;

            if (!dungeon.TrySetCurrentFloorIndex(targetFloorIndex)
                || !dungeon.TrySetPendingPlayerPartyPositions(targetPartyPositions))
            {
                return false;
            }

            dungeon.SetPlayerPosition(targetLeaderPosition);

            if (sceneLoader.TryLoadScene(dungeonSceneName, GameModeState.Exploration))
            {
                RequestAutosave("Zmiana piętra lochu");
                return true;
            }

            dungeon.TrySetCurrentFloorIndex(previousFloorIndex);
            dungeon.SetPlayerPosition(previousPosition);
            dungeon.ClearPendingPlayerPartyPositions();
            return false;
        }

        private bool TryReplaceSession(
            GameSession restoredSession,
            out string message)
        {
            message = string.Empty;
            autosaveRequested = false;
            pendingAutosaveReason = string.Empty;

            if (restoredSession == null || sceneLoader == null)
            {
                message = "Nie udało się przygotować odtworzonej sesji.";
                return false;
            }

            var previousSession = Session;
            var destinationSceneName = restoredSession.ActiveDungeon != null
                ? dungeonSceneName
                : explorationSceneName;

            UnsubscribeRuntimeServices();

            Session = restoredSession;
            sceneLoader.Initialize(Session);
            InitializeRuntimeServices();

            if (sceneLoader.TryLoadScene(
                    destinationSceneName,
                    GameModeState.Exploration))
            {
                message = "Wczytano zapis gry.";
                return true;
            }

            UnsubscribeRuntimeServices();

            Session = previousSession;
            sceneLoader.Initialize(Session);
            InitializeRuntimeServices();

            message = "Nie udało się załadować sceny po odtworzeniu zapisu.";
            return false;
        }

        private void InitializeRuntimeServices()
        {
            DialogueService = new DialogueService();
            QuestService = new QuestService(Session);
            NpcEconomyService = new NpcEconomyService();
            TradeService = new TradeService(NpcEconomyService);

            dialogueQuestActionSystem = new DialogueQuestActionSystem(
                DialogueService,
                QuestService);

            questObjectiveProgressSystem =
                new QuestObjectiveProgressSystem(
                    Session,
                    QuestService);

            DialogueService.DialogueEnded += HandleDialogueEnded;
            QuestService.QuestCompleted += HandleQuestCompleted;
        }

        private void UnsubscribeRuntimeServices()
        {
            dialogueQuestActionSystem?.Dispose();
            dialogueQuestActionSystem = null;

            if (QuestService != null)
            {
                QuestService.QuestCompleted -= HandleQuestCompleted;
            }
            if (DialogueService != null)
            {
                DialogueService.DialogueEnded -= HandleDialogueEnded;
            }
        }

        private bool TrySaveAutosave(
            string reason,
            out string message)
        {
            message = string.Empty;

            if (SaveManager == null)
            {
                message = "GameBootstrap: brak SaveManager.";
                return false;
            }

            if (!SaveManager.TrySaveAutosave(Session, out message))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                message = $"Autosave ({reason}): {message}";
            }

            return true;
        }

        private void RequestAutosave(string reason)
        {
            autosaveRequested = true;
            pendingAutosaveReason = reason ?? string.Empty;
        }

        private void HandleQuestCompleted(QuestInstance quest)
        {
            if (quest == null)
            {
                return;
            }

            RequestAutosave(
                $"ukończenie questa '{quest.QuestId}'");
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
