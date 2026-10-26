using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Godmist.Views.UI
{
    public readonly struct MenuDisplaySettings
    {
        public Vector2Int Resolution { get; }
        public FullScreenMode Mode { get; }
        public bool Fullscreen => Mode != FullScreenMode.Windowed;
        public MenuDisplaySettings(Vector2Int resolution, FullScreenMode mode)
        { Resolution = resolution; Mode = mode; }
        public bool Matches(MenuDisplaySettings other) => Resolution == other.Resolution && Mode == other.Mode;
    }

    /// <summary>Boundary for native display/audio changes and persistent global settings.</summary>
    public interface IMainMenuSettingsPlatform
    {
        bool IsEditor { get; }
        double Now { get; }
        float Volume { get; set; }
        MenuDisplaySettings Display { get; }
        IEnumerable<Vector2Int> Resolutions { get; }
        void SetDisplay(MenuDisplaySettings settings);
        void Save(float volume, MenuDisplaySettings display);
    }

    internal sealed class UnityMainMenuSettingsPlatform : IMainMenuSettingsPlatform
    {
        private const string Prefix = "Godmist.Settings.";
        public bool IsEditor => Application.isEditor;
        public double Now => Time.realtimeSinceStartupAsDouble;
        public float Volume { get => AudioListener.volume; set => AudioListener.volume = Mathf.Clamp01(value); }
        public MenuDisplaySettings Display => new(new Vector2Int(Screen.width, Screen.height), Screen.fullScreenMode);
        public IEnumerable<Vector2Int> Resolutions => Screen.resolutions.Select(r => new Vector2Int(r.width, r.height));
        public void SetDisplay(MenuDisplaySettings settings)
        {
            if (!IsEditor) Screen.SetResolution(settings.Resolution.x, settings.Resolution.y, settings.Mode);
        }
        public void Save(float volume, MenuDisplaySettings display)
        {
            PlayerPrefs.SetFloat(Prefix + "Volume", Mathf.Clamp01(volume));
            PlayerPrefs.SetInt(Prefix + "Width", display.Resolution.x);
            PlayerPrefs.SetInt(Prefix + "Height", display.Resolution.y);
            PlayerPrefs.SetInt(Prefix + "Fullscreen", display.Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }
        public static void ApplySaved()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 1f));
            if (Application.isEditor || !PlayerPrefs.HasKey(Prefix + "Width")) return;
            var resolution = new Vector2Int(PlayerPrefs.GetInt(Prefix + "Width"), PlayerPrefs.GetInt(Prefix + "Height"));
            if (Screen.resolutions.Any(r => r.width == resolution.x && r.height == resolution.y))
                Screen.SetResolution(resolution.x, resolution.y, PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) != 0
                    ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
