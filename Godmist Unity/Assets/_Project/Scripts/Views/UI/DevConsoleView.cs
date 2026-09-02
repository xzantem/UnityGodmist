using System.Collections.Generic;
using System.Linq;
using System;
using Godmist.Data.Dungeons;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Debugging;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class DevConsoleView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_InputField commandInput;
        [SerializeField] private TextMeshProUGUI outputText;
        [SerializeField] private ScrollRect outputScrollRect;
        [SerializeField] private RectTransform suggestionPanel;
        [SerializeField] private TextMeshProUGUI suggestionText;

        [Header("Configuration")]
        [SerializeField] private int maximumHistoryEntries = 30;
        [SerializeField] private int maximumSuggestions = 5;
        [SerializeField] private float suggestionRowHeight = 27f;
        [SerializeField] private float suggestionVerticalPadding = 12f;

        public static bool IsOpen { get; private set; }

        private readonly List<string> outputHistory = new();
        private readonly List<string> commandHistory = new();
        private readonly DevConsoleCommandService commandService = new();

        private List<string> suggestions = new();
        private int selectedSuggestionIndex = -1;
        private int suggestionWindowStartIndex;
        private int commandHistoryIndex = -1;
        private KeyCode navigationRepeatKey = KeyCode.None;
        private float nextNavigationRepeatTime;

        private const float NavigationRepeatDelaySeconds = 0.3f;
        private const float NavigationRepeatIntervalSeconds = 0.075f;

        private void Awake()
        {
            if (commandInput != null)
            {
                commandInput.onSubmit.AddListener(ExecuteCommand);
                commandInput.onValueChanged.AddListener(
                    HandleCommandInputChanged);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            ClearSuggestions();
            IsOpen = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                Toggle();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                BeginNavigationRepeat(KeyCode.UpArrow);
                HandleUpArrow();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                BeginNavigationRepeat(KeyCode.DownArrow);
                HandleDownArrow();
                return;
            }

            if (Input.GetKeyUp(KeyCode.UpArrow)
                && navigationRepeatKey == KeyCode.UpArrow)
            {
                navigationRepeatKey = KeyCode.None;
            }

            if (Input.GetKeyUp(KeyCode.DownArrow)
                && navigationRepeatKey == KeyCode.DownArrow)
            {
                navigationRepeatKey = KeyCode.None;
            }

            HandleHeldNavigationKey();

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                TryAcceptSelectedSuggestion();
                return;
            }

            var scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.01f && suggestions.Count > 0)
            {
                if (scrollDelta > 0f)
                {
                    SelectPreviousSuggestion();
                }
                else
                {
                    SelectNextSuggestion();
                }
            }
        }

        private void OnDisable()
        {
            IsOpen = false;
            navigationRepeatKey = KeyCode.None;
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        public void Open()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(true);
            IsOpen = true;
            commandHistoryIndex = -1;

            if (outputHistory.Count == 0)
            {
                AppendOutput(
                    "Godmist Dev Console v1\n" +
                    "Wpisz help, aby wyswietlic dostepne komendy.");
            }

            RefreshSuggestions();
            FocusInput();
        }

        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            ClearSuggestions();
            IsOpen = false;
            navigationRepeatKey = KeyCode.None;
        }

        private void HandleCommandInputChanged(string commandLine)
        {
            commandHistoryIndex = -1;
            selectedSuggestionIndex = -1;
            suggestionWindowStartIndex = 0;
            RefreshSuggestions();
        }

        private void HandleUpArrow()
        {
            if (suggestions.Count > 0)
            {
                SelectPreviousSuggestion();
                return;
            }

            BrowseCommandHistory(-1);
        }

        private void HandleDownArrow()
        {
            if (suggestions.Count > 0)
            {
                SelectNextSuggestion();
                return;
            }

            BrowseCommandHistory(1);
        }

        private void ExecuteCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                FocusInput();
                return;
            }

            AddToCommandHistory(commandLine);

            var gameSession = GameBootstrap.Instance?.Session;
            var combatSession = GetActiveCombatSession(gameSession);

            AppendOutput($"> {commandLine}");

            var commandName = commandLine
                .Split(
                    ' ',
                    System.StringSplitOptions.RemoveEmptyEntries)[0]
                .ToLowerInvariant();

            var result = commandService.Execute(
                commandLine,
                gameSession,
                combatSession,
                GameBootstrap.Instance?.ContentCatalog,
                StartDebugCombat,
                HandleNoclip,
                HandleTeleport,
                HandleGoto,
                HandleAdvance,
                HandleTick,
                HandleForceCombatResult,
                HandleTriggerDialogue);

            var prefix = result.Success ? "OK" : "BLAD";
            AppendOutput($"{prefix}: {result.Message}");

            commandInput.SetTextWithoutNotify(string.Empty);
            commandHistoryIndex = -1;
            ClearSuggestions();

            if (result.Success && commandName == "combat")
            {
                Close();
                return;
            }

            if (result.Success && commandName == "spawnobject")
            {
                RebuildDungeonObjects();
            }

            FocusInput();
        }

        private void TryAcceptSelectedSuggestion()
        {
            if (suggestions.Count == 1)
            {
                selectedSuggestionIndex = 0;
                ApplySelectedSuggestion();
                return;
            }

            if (selectedSuggestionIndex >= 0)
            {
                ApplySelectedSuggestion();
            }
        }

        private void SelectPreviousSuggestion()
        {
            if (suggestions.Count == 0)
            {
                return;
            }

            if (selectedSuggestionIndex < 0)
            {
                selectedSuggestionIndex = 0;
            }
            else
            {
                selectedSuggestionIndex--;
                if (selectedSuggestionIndex < 0)
                {
                    selectedSuggestionIndex = suggestions.Count - 1;
                }
            }

            EnsureSelectedSuggestionVisible();
            RefreshSuggestionText();
        }

        private void SelectNextSuggestion()
        {
            if (suggestions.Count == 0)
            {
                return;
            }

            selectedSuggestionIndex++;
            if (selectedSuggestionIndex >= suggestions.Count)
            {
                selectedSuggestionIndex = 0;
            }

            EnsureSelectedSuggestionVisible();
            RefreshSuggestionText();
        }

        private void ApplySelectedSuggestion()
        {
            if (selectedSuggestionIndex < 0
                || selectedSuggestionIndex >= suggestions.Count
                || commandInput == null)
            {
                return;
            }

            var commandLine = commandInput.text;
            var tokenStartIndex = FindCurrentTokenStartIndex(commandLine);
            var prefix = commandLine.Substring(0, tokenStartIndex);
            var suggestion = suggestions[selectedSuggestionIndex];

            var completedLine = $"{prefix}{suggestion}";

            if (tokenStartIndex == 0
                && DevConsoleSuggestionService.CommandRequiresArgument(
                    suggestion))
            {
                completedLine += " ";
            }

            commandInput.SetTextWithoutNotify(completedLine);
            commandInput.caretPosition = completedLine.Length;

            selectedSuggestionIndex = -1;
            suggestionWindowStartIndex = 0;
            RefreshSuggestions();
            FocusInput();
        }

        private void BrowseCommandHistory(int direction)
        {
            if (commandInput == null
                || (commandHistoryIndex < 0
                    && !string.IsNullOrWhiteSpace(commandInput.text))
                || commandHistory.Count == 0)
            {
                return;
            }

            if (direction < 0)
            {
                if (commandHistoryIndex < 0)
                {
                    commandHistoryIndex = commandHistory.Count - 1;
                }
                else
                {
                    commandHistoryIndex = Mathf.Max(
                        0,
                        commandHistoryIndex - 1);
                }
            }
            else
            {
                if (commandHistoryIndex < 0)
                {
                    return;
                }

                commandHistoryIndex++;

                if (commandHistoryIndex >= commandHistory.Count)
                {
                    commandHistoryIndex = -1;
                    commandInput.SetTextWithoutNotify(string.Empty);
                    return;
                }
            }

            commandInput.SetTextWithoutNotify(
                commandHistory[commandHistoryIndex]);

            commandInput.caretPosition = commandInput.text.Length;
        }

        private void AddToCommandHistory(string commandLine)
        {
            if (commandHistory.Count == 0
                || commandHistory[^1] != commandLine)
            {
                commandHistory.Add(commandLine);
            }

            while (commandHistory.Count > maximumHistoryEntries)
            {
                commandHistory.RemoveAt(0);
            }
        }

        private void RefreshSuggestions()
        {
            if (!IsOpen
                || commandInput == null
                || string.IsNullOrWhiteSpace(commandInput.text))
            {
                ClearSuggestions();
                return;
            }

            var gameSession = GameBootstrap.Instance?.Session;
            var combatSession = GetActiveCombatSession(gameSession);

            suggestions = DevConsoleSuggestionService.GetSuggestions(
                    commandInput.text,
                    gameSession,
                    combatSession,
                    GameBootstrap.Instance?.ContentCatalog)
                .ToList();

            selectedSuggestionIndex = suggestions.Count > 0 ? 0 : -1;
            suggestionWindowStartIndex = 0;
            RefreshSuggestionText();
        }

        private void BeginNavigationRepeat(KeyCode key)
        {
            navigationRepeatKey = key;
            nextNavigationRepeatTime = Time.unscaledTime
                + NavigationRepeatDelaySeconds;
        }

        private void HandleHeldNavigationKey()
        {
            if (navigationRepeatKey == KeyCode.None
                || !Input.GetKey(navigationRepeatKey)
                || Time.unscaledTime < nextNavigationRepeatTime)
            {
                return;
            }

            if (navigationRepeatKey == KeyCode.UpArrow)
            {
                HandleUpArrow();
            }
            else
            {
                HandleDownArrow();
            }

            nextNavigationRepeatTime = Time.unscaledTime
                + NavigationRepeatIntervalSeconds;
        }

        private void RefreshSuggestionText()
        {
            if (suggestionPanel == null || suggestionText == null)
            {
                return;
            }

            if (suggestions.Count == 0)
            {
                ClearSuggestions();
                return;
            }

            var rows = new List<string>();
            var visibleSuggestionCount = Mathf.Max(1, maximumSuggestions);
            var maximumWindowStart = Mathf.Max(
                0,
                suggestions.Count - visibleSuggestionCount);

            suggestionWindowStartIndex = Mathf.Clamp(
                suggestionWindowStartIndex,
                0,
                maximumWindowStart);

            var windowEndIndex = Mathf.Min(
                suggestions.Count,
                suggestionWindowStartIndex + visibleSuggestionCount);

            for (var index = suggestionWindowStartIndex;
                 index < windowEndIndex;
                 index++)
            {
                var suggestion = suggestions[index];

                rows.Add(index == selectedSuggestionIndex
                    ? $"<color=#F2D17B>> {suggestion}</color>"
                    : $"  {suggestion}");
            }

            suggestionText.text = string.Join("\n", rows);

            var panelHeight = suggestionVerticalPadding
                + (windowEndIndex - suggestionWindowStartIndex)
                    * suggestionRowHeight;

            suggestionPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                panelHeight);

            suggestionPanel.gameObject.SetActive(true);
        }

        private void EnsureSelectedSuggestionVisible()
        {
            var visibleSuggestionCount = Mathf.Max(1, maximumSuggestions);

            if (selectedSuggestionIndex < suggestionWindowStartIndex)
            {
                suggestionWindowStartIndex = selectedSuggestionIndex;
            }
            else if (selectedSuggestionIndex
                >= suggestionWindowStartIndex + visibleSuggestionCount)
            {
                suggestionWindowStartIndex = selectedSuggestionIndex
                    - visibleSuggestionCount
                    + 1;
            }
        }

        private void ClearSuggestions()
        {
            suggestions.Clear();
            selectedSuggestionIndex = -1;
            suggestionWindowStartIndex = 0;

            if (suggestionText != null)
            {
                suggestionText.text = string.Empty;
            }

            if (suggestionPanel != null)
            {
                suggestionPanel.gameObject.SetActive(false);
            }
        }

        private static int FindCurrentTokenStartIndex(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return 0;
            }

            for (var index = commandLine.Length - 1; index >= 0; index--)
            {
                if (char.IsWhiteSpace(commandLine[index]))
                {
                    return index + 1;
                }
            }

            return 0;
        }

        private static DevConsoleCommandResult StartDebugCombat(
            string encounterId)
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null)
            {
                return new DevConsoleCommandResult(
                    false,
                    "Brak GameBootstrap.");
            }

            if (!gameBootstrap.TryStartDebugCombat(
                    encounterId,
                    out var message))
            {
                return new DevConsoleCommandResult(
                    false,
                    message);
            }

            return new DevConsoleCommandResult(true, message);
        }

        private static CombatSession GetActiveCombatSession(
            GameSession gameSession)
        {
            var sandboxBootstrap =
                FindAnyObjectByType<CombatSandboxBootstrap>();

            if (sandboxBootstrap != null
                && sandboxBootstrap.Session != null)
            {
                return sandboxBootstrap.Session;
            }

            return gameSession?.ActiveCombat;
        }

        private static DevConsoleCommandResult HandleNoclip(bool? requestedState)
        {
            var controller = FindAnyObjectByType<ExplorationPartyController>();
            if (controller == null) return new DevConsoleCommandResult(false, "Brak ExplorationPartyController.");
            var enabled = requestedState ?? !controller.IsNoclipEnabled;
            controller.SetNoclip(enabled);
            return new DevConsoleCommandResult(true, $"Noclip: {(enabled ? "on" : "off")}");
        }

        private static DevConsoleCommandResult HandleTeleport(float x, float y)
        {
            var controller = FindAnyObjectByType<ExplorationPartyController>();
            if (controller == null) return new DevConsoleCommandResult(false, "Brak ExplorationPartyController.");
            return controller.TryTeleport(new Vector2(x, y))
                ? new DevConsoleCommandResult(true, $"Przeniesiono drużynę na ({x:0.###}, {y:0.###}).")
                : new DevConsoleCommandResult(false, "Nie można teleportować drużyny w bieżącym trybie.");
        }

        private static DevConsoleCommandResult HandleGoto(
            IReadOnlyList<string> tokens,
            int? debugSeed)
        {
            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null || bootstrap.ContentCatalog == null) return new DevConsoleCommandResult(false, "Brak GameBootstrap albo GameContentCatalog.");
            if (!bootstrap.ContentCatalog.TryGetDungeon(tokens[1], out var definition)) return new DevConsoleCommandResult(false, $"Nie znaleziono lochu: {tokens[1]}.");

            var session = bootstrap.Session;
            if (session?.ActiveDungeon == null
                || !string.Equals(session.ActiveDungeon.DungeonId, definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                var enteringFloor = 0;
                var targetRoomId = string.Empty;
                if (tokens.Count >= 3)
                {
                    if (int.TryParse(tokens[2], out var parsedFloor)) enteringFloor = parsedFloor;
                    else targetRoomId = tokens[2];
                }
                if (tokens.Count >= 4) targetRoomId = tokens[3];
                if (enteringFloor < 0) return new DevConsoleCommandResult(false, "floor musi być nieujemną liczbą całkowitą.");

                return bootstrap.TryEnterDungeon(
                        definition,
                        debugSeed ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                        enteringFloor,
                        targetRoomId)
                    ? new DevConsoleCommandResult(true, $"Przejście do lochu {definition.Id} rozpoczęte.")
                    : new DevConsoleCommandResult(false, "Nie udało się wejść do lochu.");
            }

            if (!bootstrap.IsDungeonScene)
            {
                return bootstrap.TryEnterDungeon(
                        definition,
                        debugSeed ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue))
                    ? new DevConsoleCommandResult(true, $"Wznowienie lochu {definition.Id} rozpoczęte.")
                    : new DevConsoleCommandResult(false, "Nie udało się wznowić lochu.");
            }

            var dungeon = session.ActiveDungeon;
            var targetFloor = dungeon.CurrentFloorIndex;
            if (tokens.Count >= 3
                && !int.TryParse(tokens[2], out targetFloor))
            {
                if (tokens.Count == 3) targetFloor = dungeon.CurrentFloorIndex;
                else return new DevConsoleCommandResult(false, "floor musi być nieujemną liczbą całkowitą.");
            }
            if (targetFloor < 0) return new DevConsoleCommandResult(false, "floor musi być nieujemną liczbą całkowitą.");
            if (!dungeon.TryGetFloor(targetFloor, out var floor)) return new DevConsoleCommandResult(false, $"Nie znaleziono piętra: {targetFloor}.");

            var targetPosition = dungeon.PlayerPosition;
            if (tokens.Count >= 4)
            {
                if (!floor.TryGetRoom(tokens[3], out var room)) return new DevConsoleCommandResult(false, $"Nie znaleziono pokoju: {tokens[3]}.");
                var roomCell = default(Vector2Int);
                var foundRoomCell = false;
                if (floor.Map != null)
                {
                    foreach (var cell in floor.Map.FloorCells)
                    {
                        if (!floor.Map.TryGetRoomId(cell, out var mapped)
                            || mapped != room.RoomId) continue;
                        roomCell = cell;
                        foundRoomCell = true;
                        break;
                    }
                }
                if (!foundRoomCell) return new DevConsoleCommandResult(false, $"Pokój {room.RoomId} nie ma pozycji na mapie.");
                targetPosition = new Vector2(roomCell.x, roomCell.y);
            }
            else if (tokens.Count == 3 && !int.TryParse(tokens[2], out _))
            {
                if (!floor.TryGetRoom(tokens[2], out var room)) return new DevConsoleCommandResult(false, $"Nie znaleziono pokoju: {tokens[2]}.");
                var roomCell = default(Vector2Int);
                var foundRoomCell = false;
                if (floor.Map != null)
                {
                    foreach (var cell in floor.Map.FloorCells)
                    {
                        if (floor.Map.TryGetRoomId(cell, out var mapped) && mapped == room.RoomId)
                        {
                            roomCell = cell;
                            foundRoomCell = true;
                            break;
                        }
                    }
                }
                if (!foundRoomCell) return new DevConsoleCommandResult(false, $"Pokój {room.RoomId} nie ma pozycji na mapie.");
                targetPosition = new Vector2(roomCell.x, roomCell.y);
            }

            var controller = FindAnyObjectByType<ExplorationPartyController>();
            if (targetFloor == dungeon.CurrentFloorIndex)
            {
                if (controller == null || !controller.TryTeleport(targetPosition)) return new DevConsoleCommandResult(false, "Nie udało się przenieść drużyny.");
                return new DevConsoleCommandResult(true, $"Przeniesiono drużynę na piętro {targetFloor}.");
            }

            if (controller == null) return new DevConsoleCommandResult(false, "Brak ExplorationPartyController.");
            var positions = controller.ActiveMemberViews.ToDictionary(view => view.CharacterId, view => view.Position);
            return bootstrap.TryChangeDungeonFloor(targetFloor, targetPosition, positions)
                ? new DevConsoleCommandResult(true, $"Zmiana na piętro {targetFloor} rozpoczęta.")
                : new DevConsoleCommandResult(false, "Nie udało się zmienić piętra.");
        }

        private static DevConsoleCommandResult HandleAdvance(int turns)
        {
            var sandbox = FindAnyObjectByType<CombatSandboxBootstrap>();
            if (sandbox?.Session == null || sandbox.Session.Outcome != Godmist.Core.Enums.CombatOutcomeState.Ongoing)
                return new DevConsoleCommandResult(false, "Brak aktywnej walki.");
            for (var index = 0; index < turns; index++) sandbox.RequestEndTurn();
            return new DevConsoleCommandResult(true, $"Przesunięto walkę o {turns} tur.");
        }

        private static DevConsoleCommandResult HandleTick(float seconds)
        {
            var turns = Math.Max(1, Mathf.RoundToInt(seconds));
            return HandleAdvance(turns);
        }

        private static DevConsoleCommandResult HandleForceCombatResult(Godmist.Core.Enums.CombatOutcomeState outcome)
        {
            var sandbox = FindAnyObjectByType<CombatSandboxBootstrap>();
            return sandbox != null && sandbox.TryForceOutcome(outcome)
                ? new DevConsoleCommandResult(true, $"Wymuszono wynik walki: {outcome}.")
                : new DevConsoleCommandResult(false, "Nie udało się wymusić wyniku walki.");
        }

        private static DevConsoleCommandResult HandleTriggerDialogue(string dialogueId)
        {
            var bootstrap = GameBootstrap.Instance;
            var session = bootstrap?.Session;
            var npc = session?.NpcsById?.Values.FirstOrDefault(candidate => candidate?.Definition?.BaseDialogue?.Id == dialogueId);
            var leaderId = session?.PlayerParty?.ActiveLeaderId;
            if (bootstrap?.DialogueService == null || npc == null || string.IsNullOrWhiteSpace(leaderId))
                return new DevConsoleCommandResult(false, $"Nie znaleziono dialogu albo aktywnego lidera: {dialogueId}.");
            return bootstrap.DialogueService.TryStartDialogue(npc, leaderId)
                ? new DevConsoleCommandResult(true, $"Uruchomiono dialog {dialogueId}.")
                : new DevConsoleCommandResult(false, $"Nie udało się uruchomić dialogu {dialogueId}.");
        }

        private static void RebuildDungeonObjects()
        {
            var bootstrap = GameBootstrap.Instance;
            var controller = FindAnyObjectByType<ExplorationPartyController>();
            var spawner = FindAnyObjectByType<DungeonObjectSpawner>();
            if (bootstrap?.Session?.ActiveDungeon == null || controller == null || spawner == null) return;
            if (bootstrap.Session.ActiveDungeon.TryGetCurrentFloor(out var floor)) spawner.Rebuild(floor, controller);
        }

        private void AppendOutput(string message)
        {
            outputHistory.Add(message);

            while (outputHistory.Count > maximumHistoryEntries)
            {
                outputHistory.RemoveAt(0);
            }

            if (outputText != null)
            {
                outputText.text = string.Join("\n\n", outputHistory);
            }

            Canvas.ForceUpdateCanvases();

            if (outputScrollRect != null)
            {
                outputScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void FocusInput()
        {
            if (commandInput == null)
            {
                return;
            }

            commandInput.ActivateInputField();
            commandInput.Select();
        }
    }
}
