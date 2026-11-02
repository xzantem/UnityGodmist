using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public sealed class TransferCapacityPreviewPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, background; }
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/transfer-capacity-preview-20261102"));
        private Mouse mouse, previousMouse;
        private MouseState desired;
        private readonly List<InputDevice> disabled = new();
        private InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        private InputSettings.BackgroundBehavior previousBackground;
        private bool configured;
        private static T View<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c.gameObject.scene.IsValid());
        private static IEnumerator Frames(int count=6) { for(var i=0;i<count;i++)yield return null; }
        private static IEnumerator Loaded(GameBootstrap bootstrap)
        {
            var until=Time.realtimeSinceStartup+60;
            while(bootstrap.IsSceneLoading && Time.realtimeSinceStartup<until)yield return null;
            Assert.That(bootstrap.IsSceneLoading,Is.False); yield return Frames();
        }
        private void FeedMouse()
        {
            if(InputState.currentUpdateType==InputUpdateType.Dynamic && mouse is { added:true }) InputSystem.QueueStateEvent(mouse,desired);
        }
        private IEnumerator Send(Vector2 point,bool held=false,int frames=6,MouseButton button=MouseButton.Left)
        {
            desired=new MouseState { position=point };
            if(held)desired=desired.WithButton(button);
            yield return Frames(frames);
        }
        private IEnumerator Drag(Vector2 start,Vector2 end)
        {
            yield return Send(start); yield return Send(start,true);
            for(var i=1;i<=12;i++)yield return Send(Vector2.Lerp(start,end,i/12f),true,1);
            yield return Send(end,true);
        }
        private static IEnumerator Capture(string name)
        {
            yield return Frames();
            var path=Path.Combine(Output,name+".png");
            if(File.Exists(path))File.Move(path,Path.Combine(Output,name+"-previous-"+DateTime.UtcNow.Ticks+".png"));
            ScreenCapture.CaptureScreenshot(path);
            for(var i=0;i<120 && !File.Exists(path);i++)yield return null;
            Assert.That(File.Exists(path),Is.True); Assert.That(new FileInfo(path).Length,Is.GreaterThan(10000));
        }

        [UnityTest]
        public IEnumerator LiveQuantityPreviewBlocksOverflowAndRevalidatesRecipient()
        {
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty),Is.False,"Do not replace unsaved user scenes.");
            Directory.CreateDirectory(Output);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            var game=EditorWindow.focusedWindow;
            Assert.That(game.GetType().Name,Is.EqualTo("GameView"));
            File.WriteAllText(Path.Combine(Output,"previous-scenes.json"),JsonUtility.ToJson(new Scenes {
                paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),active=setup.Select(s=>s.isActive).ToArray(),
                maximized=game.maximized,background=PlayerSettings.runInBackground }));
            game.maximized=true;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            yield return new EnterPlayMode();
            yield return RunAuthoredGame();
        }

        private IEnumerator RunAuthoredGame()
        {
            Application.runInBackground=true;
            SceneManager.LoadScene("Bootstrap",LoadSceneMode.Single);
            yield return Frames(10);
            var bootstrap=GameBootstrap.Instance;
            Assert.That(bootstrap,Is.Not.Null);
            typeof(GameBootstrap).GetProperty("SaveManager").GetSetMethod(true).Invoke(bootstrap,new object[] {new SaveManager(Path.Combine(Output,"isolated-saves"))});
            previousBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousMouse=Mouse.current; configured=true;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach(var device in InputSystem.devices.ToArray())
                if(device.enabled && device is Mouse or Keyboard or Gamepad) {disabled.Add(device);InputSystem.DisableDevice(device);}
            mouse=InputSystem.AddDevice<Mouse>("Native camera pan verification mouse");
            InputSystem.onBeforeUpdate+=FeedMouse;
            Assert.That(bootstrap.TryStartNewGame(out var message),Is.True,message);
            yield return Loaded(bootstrap);
            var session=bootstrap.Session;
            var characters=session.CharactersById.Values.Where(c=>session.PlayerParty.IsActiveMember(c.CharacterId)).ToArray();
            Assert.That(characters.Length,Is.GreaterThanOrEqualTo(2));
            var owner=characters[0]; var recipient=characters[1];
            var material=AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).First(d=>d!=null && d.ItemType==ItemType.Material && d.Weight>0 && d.IsStackable && owner.Inventory.Items.All(i=>i.Definition!=d));
            var items=Enumerable.Range(1,3).Select(i=>new ItemInstance(material,"portrait-test-"+i)).ToArray();
            owner.Inventory.Items.AddRange(items);
            var rail=View<CharacterInfoPartyRailView>();
            rail.OpenControllerCharacterPanel(ControllerCharacterPanel.Inventory,owner.CharacterId);
            yield return Frames(10);
            var window=UnityEngine.Object.FindObjectsByType<CharacterInfoWindowView>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                .Single(v=>v.Character==owner);
            var row=window.GetComponentsInChildren<CharacterInfoItemView>().Single(v=>v.Item==items[0]);
            var portrait=rail.GetComponentsInChildren<CharacterInfoPartyPortraitView>().Single(v=>v.CharacterId==recipient.CharacterId);
            var target=Center(portrait.GetComponent<RectTransform>());
            var weight=new Godmist.Systems.Items.InventoryService().GetCurrentWeight(recipient.Inventory);
            recipient.Inventory.MaxWeight=weight+material.Weight*2;
            yield return Drag(Center(row.GetComponent<RectTransform>()),target);yield return Send(target);
            var popup=Get<ItemActionPopupView>(window,"itemActionPopup");
            Assert.That(popup.IsModalOpen,Is.True);
            yield return Click(Get<Button>(popup,"maxButton"));
            Assert.That(Get<TMPro.TMP_InputField>(popup,"quantityInput").text,Is.EqualTo("3"));
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.False);
            Assert.That(Get<TMPro.TMP_Text>(popup,"quantityHint").text,Does.Contain("Waga odbiorcy"));
            Assert.That(Get<TMPro.TMP_Text>(popup,"quantityError").text,Does.Contain("limit wagi"));
            yield return Capture("01-over-capacity");
            yield return Click(Get<Button>(popup,"minusButton"));
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.True);
            yield return Capture("02-exact-capacity");
            var external=new ItemInstance(material,"capacity-external");recipient.Inventory.Items.Add(external);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.False);
            Assert.That(Get<TMPro.TMP_InputField>(popup,"quantityInput").text,Is.EqualTo("2"));
            yield return Capture("03-live-weight-change");
            recipient.Inventory.Items.Remove(external);recipient.Inventory.MaxWeight=null;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.True);
            Assert.That(Get<TMPro.TMP_Text>(popup,"quantityHint").text,Does.Contain("bez limitu"));
            yield return Capture("04-no-weight-limit");
            yield return Click(Get<Button>(popup,"confirmButton"));
            Assert.That(items.Take(2).All(recipient.Inventory.Items.Contains),Is.True);
            Assert.That(owner.Inventory.Items.Contains(items[2]),Is.True);Assert.That(popup.IsModalOpen,Is.False);
            owner.Inventory.Items.Add(new ItemInstance(material,"second-preview"));
            popup.RequestTransfer(session,owner,items[2],recipient,false,()=>true);
            recipient.LifeState=CharacterLifeState.Dead;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.False);
            Assert.That(Get<TMPro.TMP_Text>(popup,"quantityError").text,Does.Contain("nie może teraz"));
            yield return Capture("05-recipient-unavailable");
            recipient.LifeState=CharacterLifeState.Alive;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Get<Button>(popup,"confirmButton").interactable,Is.True);
            yield return Click(Get<Button>(popup,"cancelButton"));
            Assert.That(owner.Inventory.Items.Contains(items[2]),Is.True);

        }

        private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        private static Vector2 Center(RectTransform rect)
        {
            var canvas=rect.GetComponentInParent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
        }
        private IEnumerator Click(Button button)
        {
            var point=Center(button.GetComponent<RectTransform>());
            yield return Send(point); yield return Send(point,true); yield return Send(point);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if(configured)
            {
                InputSystem.onBeforeUpdate-=FeedMouse;
                if(mouse is { added:true })InputSystem.RemoveDevice(mouse);
                foreach(var device in disabled)if(device.added)InputSystem.EnableDevice(device);
                disabled.Clear();
                if(previousMouse is { added:true })previousMouse.MakeCurrent();
                InputSystem.settings.editorInputBehaviorInPlayMode=previousBehavior;
                InputSystem.settings.backgroundBehavior=previousBackground;
                configured=false;
            }
            if(Application.isPlaying)yield return new ExitPlayMode();
            var path=Path.Combine(Output,"previous-scenes.json");
            if(!File.Exists(path))yield break;
            var previous=JsonUtility.FromJson<Scenes>(File.ReadAllText(path));
            PlayerSettings.runInBackground=previous.background;
            if(previous.paths.Length==0 || previous.paths.Any(string.IsNullOrEmpty))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(previous.paths.Select((p,i)=>new SceneSetup {path=p,isLoaded=previous.loaded[i],isActive=previous.active[i]}).ToArray());
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorWindow.focusedWindow.maximized=previous.maximized;
        }
    }
}
