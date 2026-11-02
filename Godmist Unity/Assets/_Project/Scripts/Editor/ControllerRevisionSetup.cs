using System;
using System.IO;
using System.Linq;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Godmist.Editor
{
    [InitializeOnLoad]
    public static class ControllerRevisionSetup
    {
        private static string Output => Path.GetFullPath("../LocalHandoffs/controller-rework-20261102");
        static ControllerRevisionSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            var request = Path.Combine(Output, "wire.request");
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Apply(); File.WriteAllText(Path.Combine(Output,"wire-state.txt"), "PASS"); }
            catch (Exception error) { File.WriteAllText(Path.Combine(Output,"wire-state.txt"),error.ToString()); Debug.LogException(error); }
        }
        public static void ConfigureSettings(MainMenuSettingsView settings)
        {
            var so = new SerializedObject(settings);
            var panel = ((GameObject)so.FindProperty("panelRoot").objectReferenceValue).GetComponent<RectTransform>();
            var slider = (Slider)so.FindProperty("volumeSlider").objectReferenceValue;
            var valueTemplate = (TMP_Text)so.FindProperty("volumeValueText").objectReferenceValue;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x,740);
            void Top(RectTransform rect, float y, float height, float left=28, float right=28)
            {
                rect.anchorMin = new Vector2(0,1); rect.anchorMax = new Vector2(1,1); rect.pivot = new Vector2(.5f,1);
                rect.offsetMin = new Vector2(left,-y-height); rect.offsetMax = new Vector2(-right,-y);
            }
            TMP_Text Label(string name, string text, float y)
            {
                var label = panel.Find(name)?.GetComponent<TMP_Text>() ?? UnityEngine.Object.Instantiate(valueTemplate,panel);
                label.name = name; label.text = text; label.alignment = TextAlignmentOptions.Left; label.raycastTarget = false;
                Top(label.rectTransform,y,28); label.gameObject.SetActive(true); return label;
            }
            Label("ControllerHeading","Kontroler",426).fontSize = 24;
            void Control(string name,string labelText,float y,string sliderField,string valueField)
            {
                Label(name+"Label",labelText,y);
                var control = panel.Find(name)?.GetComponent<Slider>() ?? UnityEngine.Object.Instantiate(slider,panel);
                control.name = name; control.minValue = .25f; control.maxValue = 2; control.value = 1;
                Top((RectTransform)control.transform,y+32,32); control.gameObject.SetActive(true);
                var label = Label(name+"Value","1,00×",y); label.alignment = TextAlignmentOptions.Right;
                Top(label.rectTransform,y,28,520);
                so.FindProperty(sliderField).objectReferenceValue = control;
                so.FindProperty(valueField).objectReferenceValue = label;
            }
            Control("ControllerCursorSpeed","Szybkość kursora",466,"controllerCursorSlider","controllerCursorValue");
            Control("ControllerCameraSpeed","Szybkość kamery",546,"controllerCameraSlider","controllerCameraValue");
            var message = (TMP_Text)so.FindProperty("messageText").objectReferenceValue;
            Top(message.rectTransform,622,38);
            so.ApplyModifiedPropertiesWithoutUndo();
            ControllerSupportSetup.ConfigureNavigation(panel);
            var keep = (Button)so.FindProperty("keepDisplayButton").objectReferenceValue;
            var revert = (Button)so.FindProperty("revertDisplayButton").objectReferenceValue;
            var keepNav = keep.navigation; keepNav.mode = Navigation.Mode.Explicit;
            keepNav.selectOnLeft = keepNav.selectOnRight = revert; keep.navigation = keepNav;
            var revertNav = revert.navigation; revertNav.mode = Navigation.Mode.Explicit;
            revertNav.selectOnLeft = revertNav.selectOnRight = keep; revert.navigation = revertNav;
            ((GameObject)so.FindProperty("confirmationRoot").objectReferenceValue).transform.SetAsLastSibling();
        }

        public static void Apply()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Unsaved scenes will not be overwritten.");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControllerSupportSetup.ActionsPath);
            var copy = InputActionAsset.FromJson(actions.ToJson());
            try
            {
                ControllerSupportSetup.ConfigureActions(copy);
                File.WriteAllText(ControllerSupportSetup.ActionsPath,copy.ToJson());
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            AssetDatabase.ImportAsset(ControllerSupportSetup.ActionsPath,ImportAssetOptions.ForceUpdate);
            var original = EditorSceneManager.GetSceneManagerSetup();
            var paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            try
            {
                foreach (var path in paths)
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    var roots = scene.GetRootGameObjects();
                    var inspect = roots.SelectMany(r => r.GetComponentsInChildren<CombatUnitInspectController>(true)).FirstOrDefault();
                    var views = roots.SelectMany(r => r.GetComponentsInChildren<ControllerInputView>(true));
                    var changed = false;
                    foreach (var view in views)
                    {
                        var serialized = new SerializedObject(view); var field = serialized.FindProperty("inspect");
                        if (field.objectReferenceValue == inspect) continue;
                        field.objectReferenceValue = inspect; serialized.ApplyModifiedPropertiesWithoutUndo(); changed = true;
                    }
                    var inputOwner = views.FirstOrDefault();
                    if (inputOwner != null)
                    {
                        var targeting = new SerializedObject(inputOwner).FindProperty("targeting").objectReferenceValue;
                        foreach (var hud in roots.SelectMany(r=>r.GetComponentsInChildren<CombatHudView>(true)))
                        {
                            var data = new SerializedObject(hud); var property = data.FindProperty("targeting");
                            if (property.objectReferenceValue == targeting) continue;
                            property.objectReferenceValue = targeting; data.ApplyModifiedPropertiesWithoutUndo(); changed = true;
                        }
                    }
                    foreach (var settings in roots.SelectMany(r => r.GetComponentsInChildren<MainMenuSettingsView>(true)))
                    { ConfigureSettings(settings); changed = true; }
                    if (changed) EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (original.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(original);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
    }
}
