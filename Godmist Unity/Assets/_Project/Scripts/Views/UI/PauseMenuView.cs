using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private MainMenuSettingsView settingsPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private TextMeshProUGUI messageText;

        private GameSession gameSession;
        private GameModeState modeBeforeOpen;
        private int childClosedFrame = -1;
        private Button saveLoadReturnButton;
        private float timeScaleBeforeOpen = 1f;
        private bool ownsTimePause;

        public bool IsOpen => overlayRoot != null && overlayRoot.activeSelf;

        private void Awake()
        {
            resumeButton.onClick.AddListener(Close);
            saveGameButton.onClick.AddListener(OpenSavePanel);
            loadGameButton.onClick.AddListener(OpenLoadPanel);
            settingsButton.onClick.AddListener(OpenSettings);
            settingsButton.interactable = settingsPanel != null;
            quitGameButton.onClick.AddListener(QuitGame);

            saveLoadPanel.Closed += HandleSaveLoadPanelClosed;
            if (settingsPanel != null) settingsPanel.Closed += HandleSettingsClosed;

            overlayRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            RestoreTime();
            resumeButton?.onClick.RemoveListener(Close);
            saveGameButton?.onClick.RemoveListener(OpenSavePanel);
            loadGameButton?.onClick.RemoveListener(OpenLoadPanel);
            settingsButton?.onClick.RemoveListener(OpenSettings);
            quitGameButton?.onClick.RemoveListener(QuitGame);
            if (settingsPanel != null) settingsPanel.Closed -= HandleSettingsClosed;
            if (saveLoadPanel != null)
            {
                saveLoadPanel.Closed -= HandleSaveLoadPanelClosed;
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            var session = GameBootstrap.Instance?.Session;

            if (session == null || session.IsDefeated) return;
            // Settings owns Escape, including its dropdown and display confirmation.
            if (settingsPanel?.IsOpen == true || childClosedFrame == Time.frameCount
                || (saveLoadPanel?.IsOpen == true && saveLoadPanel.HandlesKeyboardNavigation)) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            HandleEscape();
        }

        public void HandleEscape()
        {
            if (settingsPanel?.IsOpen == true)
            {
                settingsPanel.HandleEscape();
                return;
            }

            if (saveLoadPanel != null && saveLoadPanel.IsOpen)
            {
                saveLoadPanel.HandleEscape();
                return;
            }

            if (IsOpen)
            {
                Close();
                return;
            }

            Open();
        }

        public void OpenLoadAfterDefeat()
        {
            gameSession = GameBootstrap.Instance?.Session;
            if (gameSession?.IsDefeated != true) return;
            overlayRoot.SetActive(true);
            menuPanelRoot.SetActive(false);
            SetGameTitleVisible(false);
            saveLoadPanel.Open(SaveLoadPanelMode.Load);
        }

        public void Open()
        {
            gameSession = GameBootstrap.Instance?.Session;

            if (IsOpen || gameSession == null
                || gameSession.CurrentMode is not (GameModeState.Exploration or GameModeState.Combat))
            {
                return;
            }

            modeBeforeOpen = gameSession.CurrentMode;
            timeScaleBeforeOpen = Time.timeScale;
            ownsTimePause = true;
            Time.timeScale = 0f;
            gameSession.TrySetMode(GameModeState.Menu);
            saveGameButton.interactable = !gameSession.HasCombat;

            menuPanelRoot.SetActive(true);
            overlayRoot.SetActive(true);
            SetGameTitleVisible(true);
            SetMessage(gameSession.HasCombat ? "Nie można zapisać gry podczas walki." : string.Empty);
            Focus(resumeButton);
        }

        public void Close()
        {
            if (!IsOpen) return;

            // Hide first so child Closed events cannot reopen the menu during teardown.
            overlayRoot.SetActive(false);
            RestoreTime();
            if (settingsPanel?.IsOpen == true) settingsPanel.Close();

            if (saveLoadPanel != null && saveLoadPanel.IsOpen)
            {
                saveLoadPanel.Close();
            }

            if (gameSession != null
                && gameSession.CurrentMode == GameModeState.Menu
                && ReferenceEquals(gameSession, GameBootstrap.Instance?.Session)
                && modeBeforeOpen is GameModeState.Exploration or GameModeState.Combat)
            {
                gameSession.TrySetMode(modeBeforeOpen);
            }
        }

        private void OpenSavePanel()
        {
            if (gameSession?.HasCombat == true) return;
            saveLoadReturnButton = saveGameButton;
            menuPanelRoot.SetActive(false);
            SetGameTitleVisible(false);
            saveLoadPanel.Open(SaveLoadPanelMode.Save);
        }

        private void RestoreTime()
        {
            if (!ownsTimePause) return;
            Time.timeScale = timeScaleBeforeOpen;
            ownsTimePause = false;
        }

        private void OnDisable() => RestoreTime();

        private void OpenLoadPanel()
        {
            saveLoadReturnButton = loadGameButton;
            menuPanelRoot.SetActive(false);
            SetGameTitleVisible(false);
            saveLoadPanel.Open(SaveLoadPanelMode.Load);
        }

        private void HandleSaveLoadPanelClosed()
        {
            childClosedFrame = Time.frameCount;
            if (gameSession?.IsDefeated == true) { overlayRoot.SetActive(false); return; }
            if (!IsOpen) return;

            menuPanelRoot.SetActive(true);
            SetGameTitleVisible(true);
            Focus(saveLoadReturnButton ?? resumeButton);
        }

        private void OpenSettings()
        {
            if (settingsPanel == null || !IsOpen) return;
            menuPanelRoot.SetActive(false);
            SetGameTitleVisible(false);
            settingsPanel.Open();
        }

        private void HandleSettingsClosed()
        {
            childClosedFrame = Time.frameCount;
            if (!IsOpen) return;
            menuPanelRoot.SetActive(true);
            SetGameTitleVisible(true);
            Focus(settingsButton);
        }

        private static void Focus(Button button)
        {
            if (EventSystem.current != null && button != null && button.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
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
                messageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            }
        }
    }
}
