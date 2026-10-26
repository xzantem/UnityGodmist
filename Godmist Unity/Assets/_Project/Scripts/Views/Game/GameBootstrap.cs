using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly Unity.Profiling.ProfilerMarker AutosaveSample = new("Godmist.Game.Autosave");
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
        [SerializeField] private Godmist.Data.World.NewCampaignDefinition newCampaign;

        public bool IsSceneLoading => sceneLoader?.IsLoading == true;

        public event Action<DungeonInstance> DungeonExited;

        private SceneLoader sceneLoader;
        private QuestObjectiveProgressSystem questObjectiveProgressSystem;
        private DialogueQuestActionSystem dialogueQuestActionSystem;
        private DialoguePartyActionSystem dialoguePartyActionSystem;
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
            Godmist.Views.UI.MainMenuSettingsView.ApplySavedSettings();
        }

        private readonly ExplorationStatusEffectSystem cityStatusEffects = new();

        private void Update()
        {
            // Dungeon occupants are ticked by their scene owner; the persistent owner covers city exploration.
            if (Session != null && !IsDungeonScene && sceneLoader?.IsLoading == false && !Session.IsDefeated
                && Session.CurrentMode == GameModeState.Exploration
                && !Godmist.Views.UI.DevConsoleView.IsOpen)
            {
                var occupants = Session.PlayerParty.ActiveMemberIds
                    .Where(id => Session.CharactersById.ContainsKey(id) && Session.GetCombatForCharacter(id) == null)
                    .Select(id => new DungeonTrapOccupant(
                        new Godmist.Runtime.Combat.BattleUnit { Character = Session.CharactersById[id], IsPlayerSide = true },
                        null, Vector2Int.zero)).ToArray();
                cityStatusEffects.Advance(Time.deltaTime, occupants);
                if (CampaignDefeatSystem.ShouldDefeat(Session)) Session.MarkDefeated();
            }

            if (!autosaveRequested)
            {
                return;
            }

            autosaveRequested = false;

            if (Session?.ActiveDungeon?.IsAssetAuditRoom == true)
            {
                Debug.Log("Autosave pominięty dla dev-only pokoju audytowego assetów.");
                pendingAutosaveReason = string.Empty;
                return;
            }

            using var autosaveSample = AutosaveSample.Auto();
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

        public bool TryStartNewGame(out string message)
        {
            if (Session?.CurrentMode != GameModeState.Menu || IsSceneLoading || Session.HasCombat)
            { message = "Nową grę można rozpocząć z menu głównego."; return false; }
            // Prepare the complete starting party before switching away from the menu.
            // No save files are deleted or overwritten by starting another campaign.
            if (!new Godmist.Systems.Characters.NewCampaignService().TryCreate(newCampaign, out var fresh, out message)) return false;
            if (!TryReplaceSession(fresh, out message)) return false;
            message = "Rozpoczynanie nowej gry…";
            return true;
        }

        public bool TryContinueGame(out string message)
        {
            if (Session?.CurrentMode != GameModeState.Menu || IsSceneLoading || Session.HasCombat)
            { message = "Kontynuacja jest dostępna z menu głównego."; return false; }
            if (SaveManager == null)
            { message = "Nie udało się otworzyć zapisów gry."; return false; }
            return SaveManager.TryLoadLatestValid(contentCatalog, out var restored, out message)
                && TryReplaceSession(restored, out message);
        }

        public bool StartCombat()
        {
            // Dungeon encounters are started by the scene owner without loading a scene.
            if (Session?.ActiveCombat?.IsSeamless == true) return true;
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

            if (Session.HasCombat)
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

        public IReadOnlyList<GameSaveSummary> GetDefeatSaves()
        {
            if (Session == null || SaveManager == null || contentCatalog == null
                || !SaveManager.TryGetSaveSummaries(out var summaries, out _)) return Array.Empty<GameSaveSummary>();
            return summaries.Where(summary => summary.CampaignId == Session.CampaignId
                && SaveManager.TryLoadSave(summary, contentCatalog, out _, out _)).ToArray();
        }

        public bool TryLoadLatestAfterDefeat(out string message)
        {
            message = "Brak poprawnego zapisu tej kampanii.";
            if (Session?.IsDefeated != true) return false;
            var latest = GetDefeatSaves().FirstOrDefault();
            return latest != null && TryLoadSave(latest, out message);
        }

        public bool TryReturnToMainMenu()
        {
            if (sceneLoader == null || sceneLoader.IsLoading || !Application.CanStreamedLevelBeLoaded("Bootstrap")) return false;
            UnsubscribeRuntimeServices();
            Session = new GameSession(GameModeState.Menu);
            autosaveRequested = false;
            sceneLoader.Initialize(Session);
            InitializeRuntimeServices();
            return sceneLoader.TryLoadScene("Bootstrap", GameModeState.Menu);
        }

        public bool TrySaveQuick(out string message)
        {
            if (Session?.ActiveDungeon?.IsAssetAuditRoom == true)
            {
                message = "Quicksave jest wyłączony w dev-only pokoju audytowym assetów.";
                Debug.Log(message);
                return false;
            }

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
            if (Session?.ActiveCombat?.IsSeamless == true) return false;
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
            string targetRoomId = null,
            int? offeredLevel = null, DungeonLength length = DungeonLength.Unspecified)
        {
            if (Session == null || Session.HasCombat || IsSceneLoading || sceneLoader == null) return false;
            if (definition == null || !definition.IsProcedural)
            {
                Debug.LogError("GameBootstrap: wejscie wymaga proceduralnego DungeonDefinition.");
                return false;
            }
            if (Session.ActiveDungeon != null)
            {
                if (!string.Equals(Session.ActiveDungeon.DungeonId, definition.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("Najpierw zakończ lub wznow istniejący loch przed wejściem do innego.");
                    return false;
                }
                return sceneLoader.TryLoadScene(dungeonSceneName, GameModeState.Exploration,
                    message: "Wczytywanie lochu…", onLoaded: () => RequestAutosave("Wznowienie lochu"));
            }
            DungeonInstance preparedDungeon = null;
            return sceneLoader.TryLoadScene(dungeonSceneName, GameModeState.Exploration,
                prepare: () =>
                {
                    if (!TryPrepareDungeon(definition, seed, targetFloorIndex, targetRoomId, offeredLevel, length)) return false;
                    preparedDungeon = Session.ActiveDungeon;
                    if (!new DungeonCampService().TryIssueSupplies(Session, contentCatalog))
                        Debug.LogWarning("Nie przydzielono zestawów obozowych: sprawdź CampingKit w katalogu zawartości.");
                    return true;
                }, message: "Generowanie lochu…",
                onLoaded: () => RequestAutosave("Wejście do lochu"),
                onFailed: () =>
                {
                    if (preparedDungeon != null && Session.ActiveDungeon == preparedDungeon) Session.TryClearActiveDungeon();
                });
        }

        private bool TryPrepareDungeon(DungeonDefinition definition, int seed, int targetFloorIndex,
            string targetRoomId, int? offeredLevel, DungeonLength length)
        {
            try
            {
                var dungeon = dungeonGenerator.Generate(definition, seed, offeredLevel ?? definition.DungeonLevel, length);
                var dropBonus = 0f; var dropMembers = 0;
                foreach (var memberId in Session.PlayerParty.ActiveMemberIds)
                    if (Session.CharactersById.TryGetValue(memberId, out var member))
                    { dropBonus += Godmist.Systems.Combat.EquipmentEffectSystem.Sum(member, Godmist.Core.Enums.GalduryteEffectType.ItemDropChance); dropMembers++; }
                dungeon.ItemDropChanceBonus = dropMembers == 0 ? 0 : UnityEngine.Mathf.Clamp01(dropBonus / dropMembers);

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

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public bool TryEnterCatacombsAssetAuditRoom(int seed)
        {
            if (Session == null || Session.HasCombat || Session.ActiveDungeon != null
                || contentCatalog == null || sceneLoader == null)
            {
                return false;
            }

            if (!contentCatalog.TryGetDungeon(
                    DungeonAssetAuditRoomBuilder.CatacombsDungeonId,
                    out var definition))
            {
                return false;
            }

            try
            {
                var dungeon = DungeonAssetAuditRoomBuilder.Build(
                    definition,
                    contentCatalog,
                    seed);
                if (!Session.TrySetActiveDungeon(dungeon))
                {
                    return false;
                }

                autosaveRequested = false;
                pendingAutosaveReason = string.Empty;

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

        public bool TryAbandonDungeon()
        {
            if(Session?.ActiveDungeon==null || Session.HasCombat || SceneManager.GetActiveScene().name!=explorationSceneName) return false;
            new DungeonCampService().RemoveSupplies(Session);
            if(!Session.TryClearActiveDungeon()) return false;
            Session.DungeonOffers.CompleteExpedition();
            RequestAutosave("Porzucenie wyprawy");return true;
        }

        public bool TryEnterDungeonOffer(int index, DungeonDefinition configuredDefinition = null)
        {
            if(Session==null || Session.HasCombat || Session.ActiveDungeon!=null
                || index<0 || index>=Session.DungeonOffers.Offers.Count) return false;
            var offer=Session.DungeonOffers.Offers[index];
            var definition=configuredDefinition!=null && configuredDefinition.Id==offer.DefinitionId ? configuredDefinition : null;
            if(definition==null && (contentCatalog==null || !contentCatalog.TryGetDungeon(offer.DefinitionId,out definition))) return false;
            return TryEnterDungeon(definition,offer.Seed,offeredLevel: Godmist.Systems.Dungeons.DungeonOfferSystem.OfferLevel(
                Godmist.Systems.Dungeons.DungeonOfferSystem.PartyLevel(Session),offer),
                length: offer.Length == DungeonLength.Unspecified ? DungeonOfferSystem.LengthForSeed(offer.Seed) : offer.Length);
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
            if (Session == null || Session.ActiveDungeon == null || Session.HasCombat)
            {
                return false;
            }

            var dungeon = Session.ActiveDungeon;

            if (dungeon.IsAssetAuditRoom)
            {
                if (!sceneLoader.TryLoadScene(
                        explorationSceneName,
                        GameModeState.Exploration))
                {
                    return false;
                }

                Session.TryClearActiveDungeon();
                return true;
            }

            if (!AreFinalBossEncountersCleared(dungeon)
                || !dungeon.TryGetCurrentFloor(out var exitFloor)
                || !Godmist.Systems.Dungeons.DungeonExitRules.IsExitRoomCleared(dungeon, exitFloor))
            {
                Debug.Log("Nie można opuścić lochu przed oczyszczeniem pokoju wyjściowego.");
                return false;
            }

            if (!sceneLoader.TryLoadScene(
                    explorationSceneName,
                    GameModeState.Exploration))
            {
                return false;
            }

            dungeon.TryMarkCompleted();
            new DungeonCampService().RemoveSupplies(Session);
            new TraderStockService().TryRefreshAfterReturn(Session, dungeon, contentCatalog);
            NpcEconomyService?.TryApplyDungeonProgress(
                Session,
                dungeon,
                out _,
                out _);

            questObjectiveProgressSystem?.ReportDungeonExited(dungeon);

            DungeonExited?.Invoke(dungeon);

            Session.TryClearActiveDungeon();
            Godmist.Systems.Dungeons.DungeonOfferSystem.CompleteExpedition(
                Session, dungeon, contentCatalog?.DungeonDefinitions);
            QuestService?.EnsureMissionBoard();
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
            if (Session?.ActiveDungeon == null || Session.HasCombat) return false;

            var isAssetAuditRoom = Session.ActiveDungeon.IsAssetAuditRoom;

            // Scene loading is asynchronous. Capture the live leader position
            // before the dungeon scene is destroyed so resuming the same
            // instance cannot fall back to a stale room position.
            FindAnyObjectByType<Godmist.Views.Exploration.ExplorationPartyController>()
                ?.PersistCurrentPosition();

            if (!sceneLoader.TryLoadScene(explorationSceneName, GameModeState.Exploration)) return false;

            if (isAssetAuditRoom)
            {
                Session.TryClearActiveDungeon();
                return true;
            }

            new DungeonCampService().RemoveSupplies(Session);
            new TraderStockService().TryRefreshAfterReturn(Session, Session.ActiveDungeon,
                contentCatalog);
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
                || Session.HasCombat
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
            if (sceneLoader?.IsLoading == true)
            { message = "Trwa już wczytywanie gry."; return false; }
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
            QuestService = new QuestService(Session, nextContentCatalog: contentCatalog);
            NpcEconomyService = new NpcEconomyService();
            TradeService = new TradeService(NpcEconomyService);

            dialogueQuestActionSystem = new DialogueQuestActionSystem(
                DialogueService,
                QuestService);
            dialoguePartyActionSystem = new DialoguePartyActionSystem(
                DialogueService,
                Session);

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
            dialoguePartyActionSystem?.Dispose();
            dialoguePartyActionSystem = null;

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

            if (Session?.ActiveDungeon?.IsAssetAuditRoom == true)
            {
                message = "Autosave pominięty dla dev-only pokoju audytowego assetów.";
                return false;
            }

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

        public void RequestAutosave(string reason)
        {
            if (Session?.ActiveDungeon?.IsAssetAuditRoom == true)
            {
                autosaveRequested = false;
                pendingAutosaveReason = string.Empty;
                Debug.Log("Żądanie autosave pominięte dla dev-only pokoju audytowego assetów.");
                return;
            }

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
