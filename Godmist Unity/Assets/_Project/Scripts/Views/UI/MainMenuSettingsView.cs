using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed class MainMenuSettingsView : MonoBehaviour
    {
        private const double ConfirmationSeconds = 15;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI volumeValueText;
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private TextMeshProUGUI confirmationMessageText;
        [SerializeField] private Button keepDisplayButton;
        [SerializeField] private Button revertDisplayButton;
        [SerializeField] private Color applyDisabledTextColor = new Color(.46f, .47f, .44f, 1);
        [SerializeField] private Color volumeFillColor = new Color(.64f, .72f, .60f, 1);

        [SerializeField] private Slider controllerCursorSlider, controllerCameraSlider;
        [SerializeField] private TMP_Text controllerCursorValue, controllerCameraValue;
        private float acceptedCursorSpeed, acceptedCameraSpeed, pendingCursorSpeed, pendingCameraSpeed;
        private IMainMenuSettingsPlatform platform;
        private TMP_Text applyLabel;
        private Button controlsButton, closeControlsButton;
        private GameObject controlsPreview;
        private Color applyEnabledTextColor;
        private IMainMenuSettingsPlatform Platform => platform ??= new UnityMainMenuSettingsPlatform();
        private List<Vector2Int> resolutions;
        private MenuDisplaySettings acceptedDisplay, pendingDisplay;
        private float acceptedVolume, pendingVolume;
        private double confirmationDeadline;
        private bool awaitingConfirmation, dropdownWasExpanded;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public event Action Closed;

        public static void ApplySavedSettings() => UnityMainMenuSettingsPlatform.ApplySaved();

        public string[] GetMissingReferences()
        {
            var references = new (string name, UnityEngine.Object value)[]
            {
                (nameof(panelRoot), panelRoot), (nameof(volumeSlider), volumeSlider),
                (nameof(resolutionDropdown), resolutionDropdown), (nameof(fullscreenToggle), fullscreenToggle),
                (nameof(applyButton), applyButton), (nameof(closeButton), closeButton),
                (nameof(messageText), messageText), (nameof(volumeValueText), volumeValueText),
                (nameof(confirmationRoot), confirmationRoot), (nameof(confirmationMessageText), confirmationMessageText),
                (nameof(keepDisplayButton), keepDisplayButton), (nameof(revertDisplayButton), revertDisplayButton)
            };
            return references.Where(reference => reference.value == null).Select(reference => reference.name).ToArray();
        }

        private void Awake()
        {
            var missing = GetMissingReferences();
            if (missing.Length > 0)
            {
                Debug.LogError($"MainMenuSettingsView ({gameObject.scene.path}/{name}): brak referencji: {string.Join(", ", missing)}.", this);
                enabled = false;
                return;
            }
            applyLabel = applyButton.GetComponentInChildren<TMP_Text>(true);
            applyEnabledTextColor = applyLabel != null ? applyLabel.color : Color.white;
            applyButton.onClick.AddListener(Apply);
            closeButton.onClick.AddListener(Close);
            CreateControlsPreview();
            volumeSlider.onValueChanged.AddListener(PreviewVolume);
            controllerCursorSlider?.onValueChanged.AddListener(ControllerChanged);
            controllerCameraSlider?.onValueChanged.AddListener(ControllerChanged);
            resolutionDropdown.onValueChanged.AddListener(DisplayChanged);
            fullscreenToggle.onValueChanged.AddListener(FullscreenChanged);
            keepDisplayButton.onClick.AddListener(KeepDisplay);
            revertDisplayButton.onClick.AddListener(RevertDisplay);
            confirmationRoot.SetActive(false);
            panelRoot.SetActive(false);
        }

        public void Open()
        {
            if (!enabled || IsOpen) return;
            acceptedDisplay = Platform.Display;
            acceptedVolume = Mathf.Clamp01(Platform.Volume);
            acceptedCursorSpeed = ControllerPreferences.CursorSpeed; acceptedCameraSpeed = ControllerPreferences.CameraSpeed;
            resolutions = Platform.Resolutions.Append(acceptedDisplay.Resolution)
                .Where(r => r.x > 0 && r.y > 0).Distinct().OrderBy(r => r.x).ThenBy(r => r.y).ToList();
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.Select(r => $"{r.x} × {r.y}"
                + (r == acceptedDisplay.Resolution ? " (bieżąca)" : string.Empty)).ToList());
            awaitingConfirmation = dropdownWasExpanded = false;
            confirmationRoot.SetActive(false);
            ResetControls();
            messageText.text = "";
            panelRoot.SetActive(true);
            RefreshControls();
            Focus(volumeSlider);
        }

        private MenuDisplaySettings ReadDisplay()
        {
            var resolution = resolutions[resolutionDropdown.value];
            // Preserve the exact original mode when the toggle was not changed.
            var mode = fullscreenToggle.isOn == acceptedDisplay.Fullscreen ? acceptedDisplay.Mode
                : fullscreenToggle.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            return new MenuDisplaySettings(resolution, mode);
        }
        private bool HasChanges => resolutions != null && resolutions.Count > 0
            && (!ReadDisplay().Matches(acceptedDisplay) || !Mathf.Approximately(volumeSlider.value, acceptedVolume)
                || controllerCursorSlider != null && !Mathf.Approximately(controllerCursorSlider.value,acceptedCursorSpeed)
                || controllerCameraSlider != null && !Mathf.Approximately(controllerCameraSlider.value,acceptedCameraSpeed));
        private void PreviewVolume(float value)
        {
            if (!IsOpen || awaitingConfirmation) return;
            Platform.Volume = Mathf.Clamp01(value);
            messageText.text = "";
            RefreshControls();
        }
        private void DisplayChanged(int _) { if (IsOpen) { messageText.text = ""; RefreshControls(); } }
        private void FullscreenChanged(bool _) { if (IsOpen) { messageText.text = ""; RefreshControls(); } }
        private void ControllerChanged(float value) { if (IsOpen) RefreshControls(); }
        private void RefreshControls()
        {
            if (controllerCursorSlider != null)
            {
                controllerCursorSlider.interactable = !awaitingConfirmation;
                if (controllerCursorValue != null) controllerCursorValue.text = $"{controllerCursorSlider.value:0.00}×";
            }
            if (controllerCameraSlider != null)
            {
                controllerCameraSlider.interactable = !awaitingConfirmation;
                if (controllerCameraValue != null) controllerCameraValue.text = $"{controllerCameraSlider.value:0.00}×";
            }
            // Shared slider styling runs on enable; settings own this muted fill.
            if (volumeSlider.fillRect != null && volumeSlider.fillRect.TryGetComponent<Image>(out var fill))
                fill.color = volumeFillColor;
            volumeValueText.text = $"{Mathf.RoundToInt(volumeSlider.value * 100)}%";
            volumeSlider.interactable = resolutionDropdown.interactable = fullscreenToggle.interactable = !awaitingConfirmation;
            closeButton.interactable = !awaitingConfirmation;
            if (controlsButton != null) controlsButton.interactable = !awaitingConfirmation;
            applyButton.interactable = !awaitingConfirmation && HasChanges;
            if (applyLabel != null)
                applyLabel.color = applyButton.interactable ? applyEnabledTextColor : applyDisabledTextColor;
        }
        private void Apply()
        {
            if (!IsOpen || awaitingConfirmation || !HasChanges) return;
            pendingDisplay = ReadDisplay();
            pendingVolume = Mathf.Clamp01(volumeSlider.value);
            pendingCursorSpeed = controllerCursorSlider != null ? controllerCursorSlider.value : acceptedCursorSpeed;
            pendingCameraSpeed = controllerCameraSlider != null ? controllerCameraSlider.value : acceptedCameraSpeed;
            if (pendingDisplay.Matches(acceptedDisplay)) { Commit(); return; }
            // Persist only after confirmation: a crash or timeout retains safe settings.
            Platform.SetDisplay(pendingDisplay);
            confirmationDeadline = Platform.Now + ConfirmationSeconds;
            awaitingConfirmation = true;
            confirmationRoot.SetActive(true);
            RefreshControls();
            RefreshCountdown();
            Focus(revertDisplayButton);
        }
        private void Commit()
        {
            if (controllerCursorSlider != null && controllerCameraSlider != null
                && (!Mathf.Approximately(pendingCursorSpeed,acceptedCursorSpeed) || !Mathf.Approximately(pendingCameraSpeed,acceptedCameraSpeed)))
            {
                ControllerPreferences.Save(pendingCursorSpeed,pendingCameraSpeed);
                acceptedCursorSpeed = pendingCursorSpeed; acceptedCameraSpeed = pendingCameraSpeed;
            }
            Platform.Volume = pendingVolume;
            Platform.Save(pendingVolume, pendingDisplay);
            acceptedVolume = pendingVolume;
            acceptedDisplay = pendingDisplay;
            for (int i = 0; i < resolutions.Count; i++)
            {
                var resolution = resolutions[i];
                resolutionDropdown.options[i].text = $"{resolution.x} × {resolution.y}"
                    + (resolution == acceptedDisplay.Resolution ? " (bieżąca)" : string.Empty);
            }
            resolutionDropdown.RefreshShownValue();
            awaitingConfirmation = false;
            confirmationRoot.SetActive(false);
            messageText.text = "Zapisano ustawienia.";
            RefreshControls();
            Focus(closeButton);
        }
        private void KeepDisplay()
        {
            if (!awaitingConfirmation) return;
            if (Platform.Now >= confirmationDeadline) { RevertDisplay(); return; }
            Commit();
        }
        private void RevertDisplay()
        {
            if (!awaitingConfirmation) return;
            RestoreAccepted();
            messageText.text = "Przywrócono poprzednie ustawienia.";
            Focus(resolutionDropdown);
        }
        private void ResetControls()
        {
            controllerCursorSlider?.SetValueWithoutNotify(acceptedCursorSpeed);
            controllerCameraSlider?.SetValueWithoutNotify(acceptedCameraSpeed);
            volumeSlider.SetValueWithoutNotify(acceptedVolume);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, resolutions.IndexOf(acceptedDisplay.Resolution)));
            fullscreenToggle.SetIsOnWithoutNotify(acceptedDisplay.Fullscreen);
        }
        private void RestoreAccepted()
        {
            if (awaitingConfirmation) Platform.SetDisplay(acceptedDisplay);
            Platform.Volume = acceptedVolume;
            awaitingConfirmation = false;
            confirmationRoot.SetActive(false);
            ResetControls();
            RefreshControls();
        }
        private void RefreshCountdown()
        {
            var seconds = Math.Max(0, (int)Math.Ceiling(confirmationDeadline - Platform.Now));
            confirmationMessageText.text = $"Zachować ustawienia obrazu?\nPowrót do poprzednich za {seconds} s."
                + (Platform.IsEditor ? "\nEdytor nie pokazuje zmian rozdzielczości ani trybu ekranu." : string.Empty);
        }
        private void Update()
        {
            if (!IsOpen) return;
            if (awaitingConfirmation)
            {
                if (Platform.Now >= confirmationDeadline) RevertDisplay();
                else RefreshCountdown();
            }
            var expanded = resolutionDropdown.IsExpanded;
            // The dropdown's EventSystem handler may already have hidden it this frame.
            if (!DevConsoleView.IsOpen && Input.GetKeyDown(KeyCode.Escape) && !expanded && !dropdownWasExpanded)
                HandleEscape();
            dropdownWasExpanded = expanded;
        }
        public void HandleEscape()
        {
            if (!IsOpen || resolutionDropdown.IsExpanded) return;
            if (awaitingConfirmation) RevertDisplay();
            else if (controlsPreview != null && controlsPreview.activeSelf) ShowControlsPreview(false);
            else Close();
        }
        public void Close()
        {
            if (!IsOpen) return;
            if (resolutionDropdown.IsExpanded) resolutionDropdown.Hide();
            if (controlsPreview != null) controlsPreview.SetActive(false);
            RestoreAccepted();
            panelRoot.SetActive(false);
            Closed?.Invoke();
        }
        private static void Focus(Selectable control)
        {
            if (EventSystem.current != null && control != null && control.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(control.gameObject);
        }

        private void CreateControlsPreview()
        {
            var panel = panelRoot.transform;
            controlsButton = CreateControlsButton(panel, "Sterowanie", Vector2.one, new Vector2(-28, -24));
            controlsButton.onClick.AddListener(() => ShowControlsPreview(true));
            var overlay = new GameObject("Controls Preview", typeof(RectTransform), typeof(Image));
            controlsPreview = overlay;
            var rect = overlay.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var background = overlay.GetComponent<Image>();
            var source = panel.GetComponent<Image>();
            background.sprite = source != null ? source.sprite : null;
            background.type = source != null ? source.type : Image.Type.Simple;
            background.color = background.sprite != null ? Color.white : new Color(.10f, .09f, .11f);

            var frame = panel.Find("GothicSettingsFrame")?.GetComponent<Image>();
            if (frame != null)
            {
                var copy = Instantiate(frame, rect);
                copy.name = "Window Frame";
                copy.raycastTarget = false;
            }
            AddControlsText(rect, "Sterowanie", 32, 0f, 1f, 24, 42, 28, 28, TextAlignmentOptions.Center);
            AddControlsColumn(rect, "Klawiatura i mysz", 0f, .5f, new (string Key, string Action)[]
            {
                ("WASD / strzałki", "Ruch"),
                ("LPM", "Ruch, także przytrzymany"),
                ("PPM + ruch", "Odblokuj kamerę"),
                ("ŚPM", "Przesuń kamerę"),
                ("Rolka", "Zoom"),
                ("F", "Tryb kamery / skupienie"),
                ("Tab", "Zmień lidera"),
                ("E", "Interakcja"),
                ("I", "Drużyna i ekwipunek"),
                ("J", "Dziennik zadań"),
                ("M", "Mapa lochu"),
                ("1–0", "Akcje bojowe"),
                ("R", "Obrót obszaru"),
                ("Enter", "Zatwierdź cele"),
                ("Esc / PPM", "Anuluj celowanie")
            });
            AddControlsColumn(rect, "Kontroler", .5f, 1f, new (string Key, string Action)[]
            {
                ("Lewy drążek", "Ruch / wybór pola"),
                ("Prawy drążek", "Kamera"),
                ("R3", "Wyśrodkuj kamerę"),
                ("L3", "Tryb kursora"),
                ("A", "Wybierz / zatwierdź"),
                ("Przytrzymaj A", "Interakcje"),
                ("B", "Wróć / anuluj"),
                ("X", "Akcje"),
                ("Y", "Inspekcja / potwierdź"),
                ("Przytrzymaj Y", "Koniec tury"),
                ("LT / RT", "Drużyna / akcje"),
                ("LB / RB", "Zmień postać / obróć"),
                ("D-pad", "Cel / zoom"),
                ("Back", "Mapa"),
                ("Start", "Pauza")
            });
            closeControlsButton = CreateControlsButton(rect, "WrÃ³Ä‡", Vector2.zero, new Vector2(28, 24));
            closeControlsButton.navigation = new Navigation { mode = Navigation.Mode.None };
            closeControlsButton.onClick.AddListener(() => ShowControlsPreview(false));
            overlay.SetActive(false);
        }

        private Button CreateControlsButton(Transform parent, string label, Vector2 anchor, Vector2 position)
        {
            var button = Instantiate(closeButton, parent);
            button.name = label;
            button.onClick = new Button.ButtonClickedEvent();
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(180, 44);
            rect.anchoredPosition = position;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            return button;
        }

        private void AddControlsColumn(RectTransform parent, string title, float left, float right,
            (string Key, string Action)[] bindings)
        {
            var inset = left == 0f ? 28f : 16f;
            var rightInset = right == 1f ? 28f : 16f;
            AddControlsText(parent, title, 23, left, right, 85, 32, inset, rightInset);
            var separator = panelRoot.transform.Find("GothicSettingsSeparator")?.GetComponent<Image>();
            if (separator != null)
            {
                var line = Instantiate(separator, parent);
                line.raycastTarget = false;
                PlaceControlsRow(line.rectTransform, left, right, 120, 6, inset, rightInset);
            }

            for (var index = 0; index < bindings.Length; index++)
            {
                var top = 135 + 31 * index;
                var (key, action) = bindings[index];
                var keyLabel = AddControlsText(parent, key, 17, left, left, top, 29, inset, -inset - 106);
                keyLabel.color = new Color(.93f, .81f, .51f);
                AddControlsText(parent, action, 17, left, right, top, 29, inset + 112, rightInset);
            }
        }

        private TMP_Text AddControlsText(Transform parent, string value, float size, float left, float right,
            float top, float height, float inset, float rightInset,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Instantiate(messageText, parent);
            label.text = value;
            label.fontSize = size;
            label.alignment = alignment;
            label.raycastTarget = false;
            PlaceControlsRow(label.rectTransform, left, right, top, height, inset, rightInset);
            label.gameObject.SetActive(true);
            return label;
        }

        private static void PlaceControlsRow(RectTransform rect, float left, float right,
            float top, float height, float inset, float rightInset)
        {
            rect.anchorMin = new Vector2(left, 1f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(inset, -top - height);
            rect.offsetMax = new Vector2(-rightInset, -top);
        }

        private void ShowControlsPreview(bool show)
        {
            if (!IsOpen || awaitingConfirmation) return;
            controlsPreview.SetActive(show);
            Focus(show ? closeControlsButton : controlsButton);
        }
        private void OnDisable() { if (IsOpen && resolutions != null) Close(); }
        private void OnDestroy()
        {
            if (applyButton != null) applyButton.onClick.RemoveListener(Apply);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(PreviewVolume);
            controllerCursorSlider?.onValueChanged.RemoveListener(ControllerChanged);
            controllerCameraSlider?.onValueChanged.RemoveListener(ControllerChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(DisplayChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(FullscreenChanged);
            if (keepDisplayButton != null) keepDisplayButton.onClick.RemoveListener(KeepDisplay);
            if (revertDisplayButton != null) revertDisplayButton.onClick.RemoveListener(RevertDisplay);
        }
    }
}
