using UnityEngine;
namespace Godmist.Views.UI
{
    /// <summary>User input preferences only; no campaign state or gameplay rule.</summary>
    public static class ControllerPreferences
    {
        public const string CursorKey = "Godmist.Controller.CursorSpeed";
        public const string CameraKey = "Godmist.Controller.CameraSpeed";
        public static float CursorSpeed => Mathf.Clamp(PlayerPrefs.GetFloat(CursorKey,1),.25f,2);
        public static float CameraSpeed => Mathf.Clamp(PlayerPrefs.GetFloat(CameraKey,1),.25f,2);
        public static void Save(float cursor, float camera)
        {
            PlayerPrefs.SetFloat(CursorKey,Mathf.Clamp(cursor,.25f,2));
            PlayerPrefs.SetFloat(CameraKey,Mathf.Clamp(camera,.25f,2)); PlayerPrefs.Save();
        }
    }
}
