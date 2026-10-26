using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class MainMenuSettingsTests
    {
        private Scene scene;
        private MainMenuSettingsView view;
        private FakePlatform platform;
        private T Ref<T>(string field)where T:UnityEngine.Object=>new SerializedObject(view).FindProperty(field).objectReferenceValue as T;
        private void Invoke(string method)=>typeof(MainMenuSettingsView).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
        private sealed class FakePlatform : IMainMenuSettingsPlatform
        {
            public bool IsEditor { get; set; }
            public double Now { get; set; }
            public float Volume { get; set; }=.8f;
            public MenuDisplaySettings Display { get; set; }=new(new Vector2Int(1920,1080),FullScreenMode.Windowed);
            public IEnumerable<Vector2Int> Resolutions { get; set; }=new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1920,1080)};
            public readonly List<MenuDisplaySettings> DisplayChanges=new();
            public int Saves;
            public float SavedVolume;
            public MenuDisplaySettings SavedDisplay;
            public void SetDisplay(MenuDisplaySettings value){DisplayChanges.Add(value);if(!IsEditor)Display=value;}
            public void Save(float volume,MenuDisplaySettings display){Saves++;SavedVolume=volume;SavedDisplay=display;}
        }
        [SetUp] public void OpenScene()
        {
            scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            view=scene.GetRootGameObjects().Single(root=>root.name=="Canvas").transform.Find("MainMenuRoot").GetComponent<MainMenuSettingsView>();
            platform=new FakePlatform();typeof(MainMenuSettingsView).GetField("platform",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,platform);
            Invoke("Awake");view.Open();
        }
        [TearDown] public void CloseScene(){view.Close();if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
        private void ChangeDisplay(){Ref<TMP_Dropdown>("resolutionDropdown").value=0;}
        [Test] public void OpeningIsCleanAndChangingThenRestoringControlsDisablesApply()
        {
            var apply=Ref<Button>("applyButton");Assert.That(apply.interactable,Is.False);
            var label=apply.GetComponentInChildren<TMP_Text>(true);var disabledColor=label.color;
            var dropdown=Ref<TMP_Dropdown>("resolutionDropdown");Assert.That(dropdown.options.Count,Is.EqualTo(2));
            Assert.That(dropdown.captionText.text,Does.Contain("bieżąca"));
            ChangeDisplay();Assert.That(apply.interactable,Is.True);Assert.That(label.color,Is.Not.EqualTo(disabledColor));
            dropdown.value=1;Assert.That(apply.interactable,Is.False);Assert.That(label.color,Is.EqualTo(disabledColor));
            Ref<Toggle>("fullscreenToggle").isOn=true;Assert.That(apply.interactable,Is.True);Ref<Toggle>("fullscreenToggle").isOn=false;Assert.That(apply.interactable,Is.False);
        }
        [Test] public void VolumePreviewsImmediatelyButCloseRestoresWithoutSaving()
        {
            Ref<Slider>("volumeSlider").value=.4f;Assert.That(platform.Volume,Is.EqualTo(.4f).Within(.001));
            Assert.That(Ref<TMP_Text>("volumeValueText").text,Is.EqualTo("40%"));Assert.That(platform.Saves,Is.Zero);
            view.Close();Assert.That(platform.Volume,Is.EqualTo(.8f).Within(.001));Assert.That(platform.Saves,Is.Zero);
        }
        [Test] public void ApplyingAudioCommitsOnceAndLaterUnsavedChangesRestoreLastCommit()
        {
            Ref<Slider>("volumeSlider").value=.4f;Ref<Button>("applyButton").onClick.Invoke();
            Assert.That(platform.Saves,Is.EqualTo(1));Assert.That(Ref<GameObject>("confirmationRoot").activeSelf,Is.False);
            Assert.That(Ref<Button>("applyButton").interactable,Is.False);Assert.That(platform.DisplayChanges,Is.Empty);
            Ref<Slider>("volumeSlider").value=.2f;view.Close();Assert.That(platform.Volume,Is.EqualTo(.4f).Within(.001));
        }
        [Test] public void DisplayPreviewBlocksControlsAndDoesNotPersistBeforeConfirmation()
        {
            ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();
            Assert.That(platform.Display.Resolution,Is.EqualTo(new Vector2Int(1280,720)));Assert.That(platform.Saves,Is.Zero);
            Assert.That(Ref<GameObject>("confirmationRoot").activeSelf,Is.True);
            Assert.That(Ref<TMP_Text>("confirmationMessageText").text,Does.Contain("15 s"));
            Assert.That(Ref<Slider>("volumeSlider").interactable,Is.False);Assert.That(Ref<Button>("closeButton").interactable,Is.False);
        }
        [Test] public void KeepPersistsDisplayAndAudioAndRepeatedConfirmationDoesNothing()
        {
            Ref<Slider>("volumeSlider").value=.3f;ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();
            Ref<Button>("keepDisplayButton").onClick.Invoke();Ref<Button>("keepDisplayButton").onClick.Invoke();
            Assert.That(platform.Saves,Is.EqualTo(1));Assert.That(platform.SavedDisplay.Resolution,Is.EqualTo(new Vector2Int(1280,720)));
            Assert.That(Ref<TMP_Dropdown>("resolutionDropdown").captionText.text,Does.Contain("1280 × 720 (bieżąca)"));
            Assert.That(platform.SavedVolume,Is.EqualTo(.3f).Within(.001));view.Close();Assert.That(platform.Volume,Is.EqualTo(.3f).Within(.001));
            Assert.That(platform.DisplayChanges.Count,Is.EqualTo(1));
        }
        [Test] public void TimeoutUsesWallClockAndRestoresDisplayAudioAndControls()
        {
            Ref<Slider>("volumeSlider").value=.3f;ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();
            platform.Now=14.1;Invoke("Update");Assert.That(Ref<TMP_Text>("confirmationMessageText").text,Does.Contain("1 s"));
            platform.Now=15;Invoke("Update");Assert.That(platform.Saves,Is.Zero);
            Assert.That(platform.Display.Resolution,Is.EqualTo(new Vector2Int(1920,1080)));Assert.That(platform.Volume,Is.EqualTo(.8f).Within(.001));
            Assert.That(Ref<GameObject>("confirmationRoot").activeSelf,Is.False);Assert.That(view.IsOpen,Is.True);
            Assert.That(Ref<Button>("applyButton").interactable,Is.False);
        }
        [Test] public void LateKeepAfterReturningFromSuspensionCannotCommitExpiredChange()
        {
            ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();platform.Now=100;
            Ref<Button>("keepDisplayButton").onClick.Invoke();Assert.That(platform.Saves,Is.Zero);
            Assert.That(platform.Display.Resolution,Is.EqualTo(new Vector2Int(1920,1080)));
        }
        [Test] public void EscapeRevertsConfirmationFirstThenClosesSettings()
        {
            ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();view.HandleEscape();
            Assert.That(view.IsOpen,Is.True);Assert.That(Ref<GameObject>("confirmationRoot").activeSelf,Is.False);
            view.HandleEscape();Assert.That(view.IsOpen,Is.False);Assert.That(platform.Saves,Is.Zero);
        }
        [Test] public void CloseDuringPreviewRestoresExactPreviousFullscreenModeAndRaisesOnce()
        {
            view.Close();platform.Display=new MenuDisplaySettings(new Vector2Int(1920,1080),FullScreenMode.ExclusiveFullScreen);view.Open();
            int closed=0;view.Closed+=()=>closed++;
            Ref<Toggle>("fullscreenToggle").isOn=false;Ref<Button>("applyButton").onClick.Invoke();view.Close();view.Close();
            Assert.That(platform.Display.Mode,Is.EqualTo(FullScreenMode.ExclusiveFullScreen));Assert.That(closed,Is.EqualTo(1));Assert.That(platform.Saves,Is.Zero);
        }
        [Test] public void DisablingViewRollsBackUnconfirmedChanges()
        {
            ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();Invoke("OnDisable");
            Assert.That(platform.Display.Resolution,Is.EqualTo(new Vector2Int(1920,1080)));Assert.That(view.IsOpen,Is.False);Assert.That(platform.Saves,Is.Zero);
        }
        [Test] public void EditorPreviewStillRequiresConfirmationAndKeepsRealDisplayUntouched()
        {
            view.Close();platform.IsEditor=true;view.Open();ChangeDisplay();Ref<Button>("applyButton").onClick.Invoke();
            Assert.That(platform.Display.Resolution,Is.EqualTo(new Vector2Int(1920,1080)));Assert.That(platform.Saves,Is.Zero);
            Assert.That(Ref<TMP_Text>("confirmationMessageText").text,Does.Contain("buildzie"));
            Ref<Button>("keepDisplayButton").onClick.Invoke();Assert.That(platform.Saves,Is.EqualTo(1));
        }
        [Test] public void EmptyNativeResolutionListStillIncludesCurrentWindowAndIsClean()
        {
            view.Close();platform.Resolutions=System.Array.Empty<Vector2Int>();platform.Display=new MenuDisplaySettings(new Vector2Int(1234,777),FullScreenMode.Windowed);view.Open();
            Assert.That(Ref<TMP_Dropdown>("resolutionDropdown").captionText.text,Does.Contain("1234 × 777"));
            Assert.That(Ref<Button>("applyButton").interactable,Is.False);
        }
    }
}
