using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private SaveLoadPanelView savePanel;
        [SerializeField] private MainMenuSettingsView settingsPanel;
        [SerializeField] private Image loadSelectionFrame;
        [SerializeField] private Image settingsSelectionFrame;
        private bool wasBusy;
        private bool hasSaves;

        private void Awake()
        {
            if (continueButton == null || newGameButton == null || loadButton == null
                || settingsButton == null || exitButton == null || messageText == null
                || savePanel == null || settingsPanel == null)
            { Debug.LogError("MainMenuView: uzupełnij referencje menu.", this); enabled = false; return; }
            continueButton.onClick.AddListener(Continue);
            newGameButton.onClick.AddListener(NewGame);
            loadButton.onClick.AddListener(OpenSaves);
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(Exit);
            savePanel.Closed += RefreshSaveAvailability;
            settingsPanel.Closed += SettingsClosed;
        }

        private void Start() { if (enabled) RefreshSaveAvailability(); }
        private void Update()
        {
            var busy = GameBootstrap.Instance?.IsSceneLoading == true;
            if (busy != wasBusy) Refresh();
        }
        private void Refresh()
        {
            var bootstrap = GameBootstrap.Instance;
            wasBusy = bootstrap?.IsSceneLoading == true;
            var available = bootstrap != null && !wasBusy && !savePanel.IsOpen && !settingsPanel.IsOpen;
            newGameButton.interactable = settingsButton.interactable = exitButton.interactable = available;
            // Continue revalidates full content on click, including fallback past broken saves.
            continueButton.interactable = loadButton.interactable = available && hasSaves;
            if (loadSelectionFrame != null) loadSelectionFrame.enabled = savePanel.IsOpen;
            if (settingsSelectionFrame != null) settingsSelectionFrame.enabled = settingsPanel.IsOpen;
        }
        private void RefreshSaveAvailability()
        {
            hasSaves = false;
            var manager = GameBootstrap.Instance?.SaveManager;
            if (manager != null)
            {
                if (manager.TryGetSaveSummaries(out var saves, out var error)) hasSaves = saves.Count > 0;
                else messageText.text = error;
            }
            Refresh();
        }
        private void SettingsClosed()
        {
            Refresh();
            if (settingsButton != null && settingsButton.interactable && settingsButton.gameObject.activeInHierarchy
                && EventSystem.current != null) settingsButton.Select();
        }
        private void Continue()
        {
            if (GameBootstrap.Instance == null) return;
            GameBootstrap.Instance.TryContinueGame(out var message);
            messageText.text = message;
            Refresh();
        }
        private void NewGame()
        {
            if (GameBootstrap.Instance == null) return;
            GameBootstrap.Instance.TryStartNewGame(out var message);
            messageText.text = message;
            Refresh();
        }
        private void OpenSaves() { messageText.text = ""; savePanel.Open(SaveLoadPanelMode.Load); Refresh(); }
        private void OpenSettings() { messageText.text = ""; settingsPanel.Open(); Refresh(); }
        private static void Exit() { Application.Quit(); }
        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
            if (newGameButton != null) newGameButton.onClick.RemoveListener(NewGame);
            if (loadButton != null) loadButton.onClick.RemoveListener(OpenSaves);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
            if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
            if (savePanel != null) savePanel.Closed -= RefreshSaveAvailability;
            if (settingsPanel != null) settingsPanel.Closed -= SettingsClosed;
        }
    }
}
