using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public enum SaveLoadPanelMode
    {
        Save = 0,
        Load = 1
    }

    public class SaveLoadPanelView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Save list")]
        [SerializeField] private Transform saveListContent;
        [SerializeField] private SaveListEntryView saveEntryPrefab;
        [SerializeField] private TextMeshProUGUI emptyListText;

        [Header("Details")]
        [SerializeField] private TextMeshProUGUI detailsText;

        [Header("Save action")]
        [SerializeField] private GameObject saveNameGroup;
        [SerializeField] private TMP_InputField saveNameInput;
        [SerializeField] private Button saveButton;

        [Header("Load actions")]
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;

        private readonly List<GameSaveSummary> saveSummaries = new();

        private SaveLoadPanelMode currentMode;
        private GameSaveSummary selectedSave;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        public event System.Action Closed;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            saveButton.onClick.AddListener(TryCreateManualSave);
            loadButton.onClick.AddListener(TryLoadSelectedSave);
            deleteButton.onClick.AddListener(TryDeleteSelectedSave);

            panelRoot.SetActive(false);
        }

        public void Open(SaveLoadPanelMode mode)
        {
            currentMode = mode;
            selectedSave = null;

            if (currentMode == SaveLoadPanelMode.Save)
            {
                saveNameInput.text = string.Empty;
            }

            panelRoot.SetActive(true);
            RefreshAll();
        }

        public void Close()
        {
            if (!IsOpen) return;

            panelRoot.SetActive(false);
            selectedSave = null;
            Closed?.Invoke();
        }

        private void TryCreateManualSave()
        {
            var bootstrap = GameBootstrap.Instance;

            if (bootstrap?.Session == null || bootstrap.SaveManager == null)
            {
                SetMessage("Brak aktywnej sesji gry.");
                return;
            }

            if (!bootstrap.SaveManager.TrySaveManual(
                    bootstrap.Session,
                    saveNameInput.text,
                    out var summary,
                    out var message))
            {
                SetMessage(message);
                return;
            }

            selectedSave = summary;
            saveNameInput.text = string.Empty;
            SetMessage(message);
            RefreshAll();
        }

        private void TryLoadSelectedSave()
        {
            if (selectedSave == null)
            {
                SetMessage("Wybierz zapis do wczytania.");
                return;
            }

            var bootstrap = GameBootstrap.Instance;

            if (bootstrap == null)
            {
                SetMessage("Brak GameBootstrap.");
                return;
            }

            if (!bootstrap.TryLoadSave(selectedSave, out var message))
            {
                SetMessage(message);
                return;
            }

            Debug.Log(message);
        }

        private void TryDeleteSelectedSave()
        {
            if (selectedSave == null)
            {
                SetMessage("Wybierz ręczny zapis do usunięcia.");
                return;
            }

            var bootstrap = GameBootstrap.Instance;

            if (bootstrap?.SaveManager == null)
            {
                SetMessage("Brak SaveManager.");
                return;
            }

            if (!bootstrap.SaveManager.TryDeleteManualSave(
                    selectedSave,
                    out var message))
            {
                SetMessage(message);
                return;
            }

            selectedSave = null;
            SetMessage(message);
            RefreshAll();
        }

        private void SelectSave(GameSaveSummary summary)
        {
            selectedSave = summary;
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshSaveList();
            RefreshDetails();
            RefreshActions();
        }

        private void RefreshHeader()
        {
            titleText.text = currentMode == SaveLoadPanelMode.Save
                ? "Zapisz grę"
                : "Wczytaj grę";

            saveNameGroup.SetActive(currentMode == SaveLoadPanelMode.Save);
        }

        private void RefreshSaveList()
        {
            ClearContent(saveListContent);
            saveSummaries.Clear();

            var saveManager = GameBootstrap.Instance?.SaveManager;

            if (saveManager == null)
            {
                SetMessage("Brak SaveManager.");
                emptyListText.gameObject.SetActive(true);
                return;
            }

            if (!saveManager.TryGetSaveSummaries(
                    out var summaries,
                    out var message))
            {
                SetMessage(string.IsNullOrWhiteSpace(message)
                    ? "Nie udało się odczytać listy zapisów."
                    : message);

                emptyListText.gameObject.SetActive(true);
                return;
            }

            if (GameBootstrap.Instance?.Session?.IsDefeated == true) summaries = GameBootstrap.Instance.GetDefeatSaves();
            foreach (var summary in summaries)
            {
                saveSummaries.Add(summary);

                var entry = Instantiate(saveEntryPrefab, saveListContent);

                entry.Bind(
                    summary,
                    summary.SaveId == selectedSave?.SaveId,
                    SelectSave);
            }

            emptyListText.gameObject.SetActive(saveSummaries.Count == 0);
        }

        private void RefreshDetails()
        {
            if (selectedSave == null)
            {
                detailsText.text = "Wybierz zapis, aby zobaczyć szczegóły.";
                return;
            }

            var slotLabel = selectedSave.SlotType switch
            {
                SaveSlotType.Quick => "Szybki zapis",
                SaveSlotType.Autosave => "Automatyczny zapis",
                _ => "Zapis ręczny"
            };

            var dateLabel = selectedSave.UpdatedAtUtc == System.DateTime.MinValue
                ? "Nieznana data"
                : selectedSave.UpdatedAtUtc
                    .ToLocalTime()
                    .ToString("dd.MM.yyyy HH:mm");

            detailsText.text =
                $"{selectedSave.DisplayName}\n\n" +
                $"Typ: {slotLabel}\n" +
                $"Ostatnia aktualizacja: {dateLabel}\n";
        }

        private void RefreshActions()
        {
            saveButton.interactable = currentMode == SaveLoadPanelMode.Save;

            loadButton.gameObject.SetActive(
                currentMode == SaveLoadPanelMode.Load);

            loadButton.interactable =
                currentMode == SaveLoadPanelMode.Load
                && selectedSave != null;

            deleteButton.gameObject.SetActive(selectedSave != null);
            deleteButton.interactable =
                selectedSave?.SlotType == SaveSlotType.Manual;
        }

        private static void ClearContent(Transform content)
        {
            if (content == null) return;

            for (var index = content.childCount - 1; index >= 0; index--)
            {
                Destroy(content.GetChild(index).gameObject);
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