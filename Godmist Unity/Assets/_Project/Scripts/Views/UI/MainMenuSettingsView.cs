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

        private IMainMenuSettingsPlatform platform;
        private TMP_Text applyLabel;
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
            volumeSlider.onValueChanged.AddListener(PreviewVolume);
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
            && (!ReadDisplay().Matches(acceptedDisplay) || !Mathf.Approximately(volumeSlider.value, acceptedVolume));
        private void PreviewVolume(float value)
        {
            if (!IsOpen || awaitingConfirmation) return;
            Platform.Volume = Mathf.Clamp01(value);
            messageText.text = "";
            RefreshControls();
        }
        private void DisplayChanged(int _) { if (IsOpen) { messageText.text = ""; RefreshControls(); } }
        private void FullscreenChanged(bool _) { if (IsOpen) { messageText.text = ""; RefreshControls(); } }
        private void RefreshControls()
        {
            // Shared slider styling runs on enable; settings own this muted fill.
            if (volumeSlider.fillRect != null && volumeSlider.fillRect.TryGetComponent<Image>(out var fill))
                fill.color = volumeFillColor;
            volumeValueText.text = $"{Mathf.RoundToInt(volumeSlider.value * 100)}%";
            volumeSlider.interactable = resolutionDropdown.interactable = fullscreenToggle.interactable = !awaitingConfirmation;
            closeButton.interactable = !awaitingConfirmation;
            applyButton.interactable = !awaitingConfirmation && HasChanges;
            if (applyLabel != null)
                applyLabel.color = applyButton.interactable ? applyEnabledTextColor : applyDisabledTextColor;
        }
        private void Apply()
        {
            if (!IsOpen || awaitingConfirmation || !HasChanges) return;
            pendingDisplay = ReadDisplay();
            pendingVolume = Mathf.Clamp01(volumeSlider.value);
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
            messageText.text = Platform.IsEditor ? "Zapisano. Zmianę obrazu sprawdź w buildzie." : "Zastosowano ustawienia.";
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
                + (Platform.IsEditor ? "\nPodgląd zmiany obrazu działa w buildzie." : string.Empty);
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
            else Close();
        }
        public void Close()
        {
            if (!IsOpen) return;
            if (resolutionDropdown.IsExpanded) resolutionDropdown.Hide();
            RestoreAccepted();
            panelRoot.SetActive(false);
            Closed?.Invoke();
        }
        private static void Focus(Selectable control)
        {
            if (EventSystem.current != null && control != null && control.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
        private void OnDisable() { if (IsOpen && resolutions != null) Close(); }
        private void OnDestroy()
        {
            if (applyButton != null) applyButton.onClick.RemoveListener(Apply);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(PreviewVolume);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(DisplayChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(FullscreenChanged);
            if (keepDisplayButton != null) keepDisplayButton.onClick.RemoveListener(KeepDisplay);
            if (revertDisplayButton != null) revertDisplayButton.onClick.RemoveListener(RevertDisplay);
        }
    }
}
