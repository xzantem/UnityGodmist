using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public sealed class MainMenuSettingsView : MonoBehaviour
    {
        private const string Prefix = "Godmist.Settings.";
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI messageText;
        private List<Vector2Int> resolutions;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public event Action Closed;

        public static void ApplySavedSettings()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 1f));
            if (!Application.isEditor && PlayerPrefs.HasKey(Prefix + "Width"))
            {
                var width = PlayerPrefs.GetInt(Prefix + "Width");
                var height = PlayerPrefs.GetInt(Prefix + "Height");
                if (Screen.resolutions.Any(r => r.width == width && r.height == height))
                    Screen.SetResolution(width, height, PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) != 0
                        ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
        }

        private void Awake()
        {
            if (panelRoot == null || volumeSlider == null || resolutionDropdown == null
                || fullscreenToggle == null || applyButton == null || closeButton == null || messageText == null)
            { Debug.LogError("MainMenuSettingsView: uzupełnij referencje ustawień.", this); enabled = false; return; }
            applyButton.onClick.AddListener(Apply);
            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);
        }
        public void Open()
        {
            if (!enabled) return;
            resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height))
                .Append(new Vector2Int(Screen.width, Screen.height)).Distinct()
                .OrderBy(r => r.x).ThenBy(r => r.y).ToList();
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.Select(r => $"{r.x} × {r.y}").ToList());
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height))));
            volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            messageText.text = "";
            panelRoot.SetActive(true);
        }
        private void Apply()
        {
            if (resolutions == null || resolutionDropdown.value < 0 || resolutionDropdown.value >= resolutions.Count) return;
            var resolution = resolutions[resolutionDropdown.value];
            var volume = Mathf.Clamp01(volumeSlider.value);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(Prefix + "Volume", volume);
            PlayerPrefs.SetInt(Prefix + "Width", resolution.x);
            PlayerPrefs.SetInt(Prefix + "Height", resolution.y);
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreenToggle.isOn ? 1 : 0);
            PlayerPrefs.Save();
            if (!Application.isEditor)
                Screen.SetResolution(resolution.x, resolution.y, fullscreenToggle.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            messageText.text = Application.isEditor
                ? "Zapisano. Zmiana trybu ekranu działa w buildzie gry." : "Zastosowano ustawienia.";
        }
        public void Close()
        {
            if (!IsOpen) return;
            panelRoot.SetActive(false);
            Closed?.Invoke();
        }
        private void OnDestroy()
        {
            if (applyButton != null) applyButton.onClick.RemoveListener(Apply);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }
    }
}
