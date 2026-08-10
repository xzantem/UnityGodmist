using System.Collections.Generic;
using Godmist.Runtime.Combat;
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

        [Header("Configuration")]
        [SerializeField] private int maximumHistoryEntries = 30;

        public static bool IsOpen { get; private set; }

        private readonly List<string> history = new();
        private readonly DevConsoleCommandService commandService = new();

        private void Awake()
        {
            if (commandInput != null)
            {
                commandInput.onSubmit.AddListener(ExecuteCommand);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                Toggle();
                return;
            }

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
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

            if (history.Count == 0)
            {
                AppendOutput(
                    "Godmist Dev Console v1\n" +
                    "Wpisz help, aby wyświetlić dostępne komendy.");
            }

            FocusInput();
        }

        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private void ExecuteCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                FocusInput();
                return;
            }

            var gameSession = GameBootstrap.Instance?.Session;
            var combatSession = GetActiveCombatSession(gameSession);

            AppendOutput($"> {commandLine}");

            var result = commandService.Execute(
                commandLine,
                gameSession,
                combatSession,
                GameBootstrap.Instance?.ContentCatalog);

            var prefix = result.Success ? "OK" : "BŁĄD";
            AppendOutput($"{prefix}: {result.Message}");

            commandInput.text = string.Empty;
            FocusInput();
        }

        private static CombatSession GetActiveCombatSession(
            Godmist.Runtime.Game.GameSession gameSession)
        {
            var sandboxBootstrap =
                FindFirstObjectByType<CombatSandboxBootstrap>();

            if (sandboxBootstrap != null
                && sandboxBootstrap.Session != null)
            {
                return sandboxBootstrap.Session;
            }

            return gameSession?.ActiveCombat;
        }

        private void AppendOutput(string message)
        {
            history.Add(message);

            while (history.Count > maximumHistoryEntries)
            {
                history.RemoveAt(0);
            }

            if (outputText != null)
            {
                outputText.text = string.Join("\n\n", history);
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