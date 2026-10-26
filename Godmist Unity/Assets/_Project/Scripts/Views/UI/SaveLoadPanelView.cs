using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Game;
using Godmist.Data.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
        [Header("Optional menu presentation")]
        [SerializeField] private ScrollRect saveScroll;
        [SerializeField] private Image[] partyPortraits;
        [SerializeField] private bool keyboardNavigation;
        [SerializeField] private GameContentCatalog previewCatalog;
        [SerializeField] private TMP_Dropdown campaignDropdown;
        [SerializeField] private Button historyButton;
        [SerializeField] private TextMeshProUGUI campaignStatusText;
        [SerializeField] private TextMeshProUGUI previewLocationText;
        [SerializeField] private TextMeshProUGUI previewDateText;
        [SerializeField] private TextMeshProUGUI partyHeadingText;
        [SerializeField] private TextMeshProUGUI[] partyCaptionTexts;
        private GameContentCatalog Catalog => GameBootstrap.Instance?.ContentCatalog ?? previewCatalog;

        private readonly List<GameSaveSummary> saveSummaries = new();
        private readonly List<SaveListEntryView> saveEntries = new();

        private SaveLoadPanelMode currentMode;
        private GameSaveSummary selectedSave;
        private bool confirmDelete;
        private IReadOnlyList<SaveBrowserCampaign> campaigns = System.Array.Empty<SaveBrowserCampaign>();
        private string selectedCampaignKey;
        private bool showHistory;
        private TextMeshProUGUI quickShortcutText;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public GameSaveSummary SelectedSave => selectedSave;
        public bool HandlesKeyboardNavigation => keyboardNavigation;

        public void HandleEscape()
        {
            if (campaignDropdown != null && campaignDropdown.IsExpanded) return;
            Close();
        }

        public event System.Action Closed;

        private void Update()
        {
            if (!keyboardNavigation || !IsOpen || DevConsoleView.IsOpen) return;
            if (campaignDropdown != null && campaignDropdown.IsExpanded) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { HandleEscape(); return; }
            if (saveNameInput != null && saveNameInput.isFocused) return;
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Let focused controls own submit (dropdown/history/delete/load).
                // Only a focused save row, or no UI focus, uses the load shortcut.
                var focused = EventSystem.current?.currentSelectedGameObject;
                if ((focused == null || focused.GetComponent<SaveListEntryView>() != null)
                    && currentMode == SaveLoadPanelMode.Load && loadButton.interactable) TryLoadSelectedSave();
            }
        }

        public void MoveSelection(int delta)
        {
            if (saveSummaries.Count == 0) return;
            var index = saveSummaries.FindIndex(summary => summary.SaveId == selectedSave?.SaveId);
            index = Mathf.Clamp(index < 0 ? 0 : index + delta, 0, saveSummaries.Count - 1);
            SelectSave(saveSummaries[index]);
            if (saveScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                var row = (RectTransform)saveEntries[index].transform;
                var viewport = saveScroll.viewport;
                var extent = saveScroll.content.rect.height - viewport.rect.height;
                if (extent > 0)
                {
                    var top = -row.anchoredPosition.y - row.rect.height * (1 - row.pivot.y);
                    var current = (1 - saveScroll.verticalNormalizedPosition) * extent;
                    var bottom = top + row.rect.height;
                    if (index == 0) current = 0; // Include the list's top padding when returning to the newest save.
                    else if (top < current) current = top;
                    else if (bottom > current + viewport.rect.height) current = bottom - viewport.rect.height;
                    saveScroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(current / extent);
                }
            }
        }

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            saveButton.onClick.AddListener(TryCreateManualSave);
            loadButton.onClick.AddListener(TryLoadSelectedSave);
            deleteButton.onClick.AddListener(TryDeleteSelectedSave);
            if (campaignDropdown != null) campaignDropdown.onValueChanged.AddListener(SelectCampaign);
            if (historyButton != null) historyButton.onClick.AddListener(ToggleHistory);

            quickShortcutText = CreateQuickShortcutHint();

            panelRoot.SetActive(false);
        }

        public void Open(SaveLoadPanelMode mode)
        {
            currentMode = mode;
            selectedSave = null;
            confirmDelete = false;
            selectedCampaignKey = mode == SaveLoadPanelMode.Save ? GameBootstrap.Instance?.Session?.CampaignId : null;
            showHistory = false;
            SetMessage(string.Empty);

            if (currentMode == SaveLoadPanelMode.Save)
            {
                saveNameInput.text = string.Empty;
            }

            panelRoot.SetActive(true);
            RefreshAll();
            if (mode == SaveLoadPanelMode.Save && keyboardNavigation) saveNameInput.Select();
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
            selectedCampaignKey = summary.CampaignId;
            saveNameInput.text = string.Empty;
            SetMessage("Zapisano grę.");
            RefreshAll();
        }

        private void TryLoadSelectedSave()
        {
            if (GameBootstrap.Instance?.IsSceneLoading == true) return;
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

            if (selectedSave.SlotType != SaveSlotType.Manual) return;
            if (!confirmDelete)
            {
                confirmDelete = true;
                SetMessage($"Usunąć zapis „{SaveSummaryPresentation.Safe(selectedSave.DisplayName)}”? Potwierdź przyciskiem poniżej.");
                RefreshActions();
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
            confirmDelete = false;
            SetMessage(message);
            RefreshAll();
        }

        private void SelectSave(GameSaveSummary summary)
        {
            selectedSave = summary;
            confirmDelete = false;
            SetMessage(string.Empty);
            for (var index = 0; index < saveEntries.Count; index++)
            {
                saveEntries[index].SetSelected(saveSummaries[index].SaveId == selectedSave?.SaveId);
                if (keyboardNavigation && saveSummaries[index].SaveId == selectedSave?.SaveId && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(saveEntries[index].GetComponent<Button>().gameObject);
            }
            RefreshDetails();
            RefreshActions();
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
            RefreshQuickShortcutHint();
        }

        private void RefreshSaveList()
        {
            ClearContent(saveListContent);
            saveSummaries.Clear();
            saveEntries.Clear();

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
            ShowSummaries(summaries);
        }

        public void ShowSummaries(IReadOnlyList<GameSaveSummary> summaries)
        {
            if (campaignDropdown == null)
            {
                RebuildEntries(summaries);
                return;
            }
            campaigns = SaveSummaryPresentation.Campaigns(summaries, Catalog);
            var index = campaigns.ToList().FindIndex(campaign => campaign.Key == selectedCampaignKey);
            if (index < 0) index = 0;
            selectedCampaignKey = campaigns.Count > 0 ? campaigns[index].Key : null;
            campaignDropdown.ClearOptions();
            campaignDropdown.AddOptions(campaigns.Select(campaign => campaign.Label).ToList());
            campaignDropdown.SetValueWithoutNotify(index);
            campaignDropdown.RefreshShownValue();
            campaignDropdown.interactable = campaigns.Count > 1;
            RefreshCampaignEntries();
        }

        public void SelectCampaign(int index)
        {
            if (index < 0 || index >= campaigns.Count) return;
            selectedCampaignKey = campaigns[index].Key;
            campaignDropdown.SetValueWithoutNotify(index);
            selectedSave = null;
            showHistory = false;
            SetMessage(string.Empty);
            RefreshCampaignEntries();
        }

        public void ToggleHistory()
        {
            showHistory = !showHistory;
            SetMessage(string.Empty);
            RefreshCampaignEntries();
        }

        private void RefreshCampaignEntries()
        {
            var campaign = campaigns.FirstOrDefault(item => item.Key == selectedCampaignKey);
            campaignStatusText.text = campaign == null ? "Brak kampanii" : $"Zapisy: {campaign.Saves.Count}"
                + (campaign.OlderAutosaveCount > 0 ? $" • starsze autozapisy: {campaign.OlderAutosaveCount}" : string.Empty);
            historyButton.gameObject.SetActive(campaign != null && campaign.OlderAutosaveCount > 0);
            historyButton.GetComponentInChildren<TMP_Text>(true).text = showHistory ? "Ukryj starsze autozapisy" : $"Starsze autozapisy ({campaign?.OlderAutosaveCount ?? 0})";
            RebuildEntries(campaign?.VisibleSaves(showHistory) ?? System.Array.Empty<GameSaveSummary>());
        }

        private void RebuildEntries(IReadOnlyList<GameSaveSummary> summaries)
        {
            ClearContent(saveListContent);
            saveSummaries.Clear();
            saveEntries.Clear();
            confirmDelete = false;
            var ordered = summaries.OrderByDescending(summary => summary.UpdatedAtUtc).ToArray();
            selectedSave = ordered.FirstOrDefault(summary => summary.SaveId == selectedSave?.SaveId)
                ?? (currentMode == SaveLoadPanelMode.Load ? ordered.FirstOrDefault() : null);
            foreach (var summary in ordered)
            {
                saveSummaries.Add(summary);

                var entry = Instantiate(saveEntryPrefab, saveListContent);
                saveEntries.Add(entry);

                entry.Bind(
                    summary,
                    summary.SaveId == selectedSave?.SaveId,
                    SelectSave,
                    Catalog,
                    campaignDropdown != null);
                if (keyboardNavigation)
                {
                    var button = entry.GetComponent<Button>();
                    var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
                }
            }

            emptyListText.gameObject.SetActive(saveSummaries.Count == 0);
            RefreshDetails();
            RefreshActions();
            if (saveScroll != null) saveScroll.verticalNormalizedPosition = 1;
            if (keyboardNavigation && selectedSave != null && EventSystem.current != null)
            {
                var index = saveSummaries.FindIndex(save => save.SaveId == selectedSave.SaveId);
                if (index >= 0) EventSystem.current.SetSelectedGameObject(saveEntries[index].GetComponent<Button>().gameObject);
            }
        }

        private void RefreshDetails()
        {
            if (selectedSave == null)
            {
                detailsText.text = saveSummaries.Count == 0 ? "Brak zapisów do wczytania.\nRozpocznij nową grę, aby utworzyć kampanię." : "Wybierz zapis, aby zobaczyć szczegóły.";
                RefreshPortraits();
                if (previewLocationText != null) previewLocationText.text = "Twoja drużyna";
                if (previewDateText != null) previewDateText.text = string.Empty;
                if (partyHeadingText != null) partyHeadingText.text = string.Empty;
                return;
            }
            detailsText.text = previewLocationText == null ? SaveSummaryPresentation.Details(selectedSave, Catalog) : SaveSummaryPresentation.Facts(selectedSave, Catalog);
            if (previewLocationText != null) previewLocationText.text = SaveSummaryPresentation.Location(selectedSave, Catalog);
            if (previewDateText != null) previewDateText.text = $"{SaveSummaryPresentation.Slot(selectedSave)} • {SaveSummaryPresentation.Date(selectedSave)}"
                + (selectedSave.SlotType == SaveSlotType.Manual ? $"\n{SaveSummaryPresentation.Safe(selectedSave.DisplayName)}" : string.Empty);
            if (partyHeadingText != null) partyHeadingText.text = selectedSave.Preview?.Party.Count > 0 ? "Drużyna w chwili zapisu" : "Brak danych drużyny";
            RefreshPortraits();
        }

        private void RefreshPortraits()
        {
            if (partyPortraits == null) return;
            var party = selectedSave?.Preview?.Party;
            var catalog = Catalog;
            for (var i = 0; i < partyPortraits.Length; i++)
            {
                var image = partyPortraits[i];
                if (image == null) continue;
                var sprite = party != null && i < party.Count && catalog != null && catalog.TryGetCharacter(party[i].CharacterId, out var character) ? character.UiPortraitSprite : null;
                image.sprite = sprite; image.enabled = sprite != null; image.preserveAspect = true;
                image.transform.parent.gameObject.SetActive(party != null && i < party.Count);
                if (partyCaptionTexts != null && i < partyCaptionTexts.Length && partyCaptionTexts[i] != null)
                    partyCaptionTexts[i].text = party != null && i < party.Count ? $"{SaveSummaryPresentation.Name(party[i].CharacterId, catalog)}\nPoz. {party[i].Level}" : string.Empty;
            }
        }

        private void OnDestroy()
        {
            if (campaignDropdown != null) campaignDropdown.onValueChanged.RemoveListener(SelectCampaign);
            if (historyButton != null) historyButton.onClick.RemoveListener(ToggleHistory);
        }

        private void RefreshActions()
        {
            saveButton.gameObject.SetActive(currentMode == SaveLoadPanelMode.Save);
            saveButton.interactable = currentMode == SaveLoadPanelMode.Save;

            loadButton.gameObject.SetActive(
                currentMode == SaveLoadPanelMode.Load);

            loadButton.interactable =
                currentMode == SaveLoadPanelMode.Load
                && selectedSave != null;

            deleteButton.gameObject.SetActive(selectedSave?.SlotType == SaveSlotType.Manual);
            deleteButton.interactable =
                selectedSave?.SlotType == SaveSlotType.Manual;
            var deleteLabel = deleteButton.GetComponentInChildren<TMP_Text>(true);
            if (deleteLabel != null) deleteLabel.text = confirmDelete ? "Potwierdź usunięcie" : "Usuń zapis";
        }

        private TextMeshProUGUI CreateQuickShortcutHint()
        {
            if (panelRoot == null) return null;

            var existing = panelRoot.GetComponentInChildren<TMP_Text>(true);
            var hintObject = new GameObject(
                "Quick Save Shortcut",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            hintObject.transform.SetParent(panelRoot.transform, false);
            var hint = hintObject.GetComponent<TextMeshProUGUI>();
            hintObject.GetComponent<LayoutElement>().ignoreLayout = true;
            hint.font = existing?.font ?? TMP_Settings.defaultFontAsset;
            hint.fontSharedMaterial = existing?.fontSharedMaterial ?? hint.font?.material;
            hint.fontSize = existing != null ? Mathf.Max(14f, existing.fontSize) : 16f;
            hint.fontStyle = FontStyles.Bold;
            hint.color = new Color(0.93f, 0.88f, 0.72f, 1f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.raycastTarget = false;

            var rect = (RectTransform)hint.transform;
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(520f, 24f);
            hint.gameObject.SetActive(false);
            return hint;
        }

        private void RefreshQuickShortcutHint()
        {
            if (quickShortcutText == null) return;

            var bootstrap = GameBootstrap.Instance;
            var session = bootstrap?.Session;
            var canQuickSave = currentMode == SaveLoadPanelMode.Save
                && session != null
                && !session.HasCombat
                && bootstrap.SaveManager != null
                && session.ActiveDungeon?.IsAssetAuditRoom != true;
            var canQuickLoad = currentMode == SaveLoadPanelMode.Load
                && session != null
                && bootstrap.SaveManager != null
                && bootstrap.ContentCatalog != null;
            var hint = canQuickSave ? "F5 – szybki zapis"
                : canQuickLoad ? "F9 – szybkie wczytanie"
                : string.Empty;
            quickShortcutText.text = hint;
            quickShortcutText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        }

        private static void ClearContent(Transform content)
        {
            if (content == null) return;

            for (var index = content.childCount - 1; index >= 0; index--)
            {
                var child = content.GetChild(index).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
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
