using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Debugging;
using Godmist.Views.Combat;
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
        private int commandHistoryIndex = -1;

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
                HandleUpArrow();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                HandleDownArrow();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                TryAcceptSelectedSuggestion();
            }
        }

        private void OnDisable()
        {
            IsOpen = false;
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
        }

        private void HandleCommandInputChanged(string commandLine)
        {
            commandHistoryIndex = -1;
            selectedSuggestionIndex = -1;
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
            if (selectedSuggestionIndex >= 0)
            {
                ApplySelectedSuggestion();
                return;
            }

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
                StartDebugCombat);

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
                selectedSuggestionIndex = suggestions.Count - 1;
            }
            else
            {
                selectedSuggestionIndex--;

                if (selectedSuggestionIndex < 0)
                {
                    selectedSuggestionIndex = suggestions.Count - 1;
                }
            }

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
            RefreshSuggestions();
            FocusInput();
        }

        private void BrowseCommandHistory(int direction)
        {
            if (commandInput == null
                || !string.IsNullOrWhiteSpace(commandInput.text)
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
                .Take(maximumSuggestions)
                .ToList();

            selectedSuggestionIndex = -1;
            RefreshSuggestionText();
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

            for (var index = 0; index < suggestions.Count; index++)
            {
                var suggestion = suggestions[index];

                rows.Add(index == selectedSuggestionIndex
                    ? $"<color=#F2D17B>> {suggestion}</color>"
                    : $"  {suggestion}");
            }

            suggestionText.text = string.Join("\n", rows);

            var panelHeight = suggestionVerticalPadding
                + suggestions.Count * suggestionRowHeight;

            suggestionPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                panelHeight);

            suggestionPanel.gameObject.SetActive(true);
        }

        private void ClearSuggestions()
        {
            suggestions.Clear();
            selectedSuggestionIndex = -1;

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
