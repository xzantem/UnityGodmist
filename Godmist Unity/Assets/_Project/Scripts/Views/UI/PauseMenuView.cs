using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class PauseMenuView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private GameObject menuPanelRoot;
        [SerializeField] private SaveLoadPanelView saveLoadPanel;
        [SerializeField] private GameObject gameTitleRoot;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private TextMeshProUGUI messageText;

        private GameSession gameSession;
        private GameModeState modeBeforeOpen;

        public bool IsOpen => overlayRoot != null && overlayRoot.activeSelf;

        private void Awake()
        {
            resumeButton.onClick.AddListener(Close);
            saveGameButton.onClick.AddListener(OpenSavePanel);
            loadGameButton.onClick.AddListener(OpenLoadPanel);
            settingsButton.onClick.AddListener(ShowSettingsPlaceholder);
            quitGameButton.onClick.AddListener(QuitGame);

            saveLoadPanel.Closed += HandleSaveLoadPanelClosed;

            overlayRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (saveLoadPanel != null)
            {
                saveLoadPanel.Closed -= HandleSaveLoadPanelClosed;
            }
        }

        private void Update()
        {
            var session = GameBootstrap.Instance?.Session;

            if (session == null) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (saveLoadPanel != null && saveLoadPanel.IsOpen)
            {
                saveLoadPanel.Close();
                return;
            }

            if (IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        public void Open()
        {
            gameSession = GameBootstrap.Instance?.Session;

            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration)
            {
                return;
            }

            modeBeforeOpen = gameSession.CurrentMode;
            gameSession.TrySetMode(GameModeState.Menu);

            menuPanelRoot.SetActive(true);
            overlayRoot.SetActive(true);
            SetGameTitleVisible(true);
            SetMessage(string.Empty);
        }

        public void Close()
        {
            if (!IsOpen) return;

            if (saveLoadPanel != null && saveLoadPanel.IsOpen)
            {
                saveLoadPanel.Close();
            }

            overlayRoot.SetActive(false);

            if (gameSession != null
                && gameSession.CurrentMode == GameModeState.Menu
                && modeBeforeOpen == GameModeState.Exploration)
            {
                gameSession.TrySetMode(GameModeState.Exploration);
            }
        }

        private void OpenSavePanel()
        {
            menuPanelRoot.SetActive(false);
            SetGameTitleVisible(false);
            saveLoadPanel.Open(SaveLoadPanelMode.Save);
        }

        private void OpenLoadPanel()
        {
            menuPanelRoot.SetActive(false);
            SetGameTitleVisible(false);
            saveLoadPanel.Open(SaveLoadPanelMode.Load);
        }

        private void HandleSaveLoadPanelClosed()
        {
            if (!IsOpen) return;

            menuPanelRoot.SetActive(true);
            SetGameTitleVisible(true);
        }

        private void ShowSettingsPlaceholder()
        {
            SetMessage("Ustawienia zostaną dodane w kolejnym milestone.");
        }

        private static void QuitGame()
        {
            Debug.Log("Wyjście z gry.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetGameTitleVisible(bool isVisible)
        {
            if (gameTitleRoot != null)
            {
                gameTitleRoot.SetActive(isVisible);
            }
        }

        private void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }
    }
}