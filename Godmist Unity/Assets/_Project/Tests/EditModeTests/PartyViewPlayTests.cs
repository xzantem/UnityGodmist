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
using Godmist.Views.UI;
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
    public sealed class PartyViewPlayTests
    {
        [Serializable] private sealed class Scenes { public string[] paths; public bool[] loaded, active; public bool maximized, background; }
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/party-view-20261102"));
        private Mouse mouse, previousMouse;
        private Keyboard keyboard, previousKeyboard;
        private KeyboardState keyboardState;
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
            if(InputState.currentUpdateType==InputUpdateType.Dynamic && keyboard is {added:true})InputSystem.QueueStateEvent(keyboard,keyboardState);
        }
        private IEnumerator KeyPress(Key key)
        {
            keyboardState=new KeyboardState(key);yield return Frames(4);keyboardState=new KeyboardState();yield return Frames(4);
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
            yield return BackgroundGameCapture.Capture(path,name.StartsWith("08-"));
            Assert.That(File.Exists(path),Is.True);
            Assert.That(new FileInfo(path).Length,Is.GreaterThan(10000));
        }

        [UnityTest]
        public IEnumerator PartyColumnsUseIndependentModesTransferItemsAndKeepLeaderAndActor()
        {
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty),Is.False,"Do not replace unsaved user scenes.");
            Directory.CreateDirectory(Output);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            File.WriteAllText(Path.Combine(Output,"previous-scenes.json"),JsonUtility.ToJson(new Scenes {
                paths=setup.Select(s=>s.path).ToArray(),loaded=setup.Select(s=>s.isLoaded).ToArray(),active=setup.Select(s=>s.isActive).ToArray(),background=PlayerSettings.runInBackground }));
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
            previousMouse=Mouse.current;previousKeyboard=Keyboard.current; configured=true;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach(var device in InputSystem.devices.ToArray())
                if(device.enabled && device is Mouse or Keyboard or Gamepad) {disabled.Add(device);InputSystem.DisableDevice(device);}
            mouse=InputSystem.AddDevice<Mouse>("Party View verification mouse");
            keyboard=InputSystem.AddDevice<Keyboard>("Party View verification keyboard");
            InputSystem.onBeforeUpdate+=FeedMouse;
            Assert.That(bootstrap.TryStartNewGame(out var message),Is.True,message);
            yield return Loaded(bootstrap);
            yield return KeyPress(Key.F10);
            Assert.That(Godmist.Views.UI.DevConsoleView.IsOpen, Is.True);
            var console = View<Godmist.Views.UI.DevConsoleView>();
            Assert.That(Get<GameObject>(console, "panelRoot").activeInHierarchy, Is.True);
            Assert.That(console.GetComponentInParent<Canvas>().enabled, Is.True);
            yield return KeyPress(Key.Backquote);
            Assert.That(Godmist.Views.UI.DevConsoleView.IsOpen, Is.False);
            yield return Capture("00-hud-shortcuts");
            var session=bootstrap.Session;
            var rail=View<CharacterInfoPartyRailView>();
            var leader=session.PlayerParty.ActiveLeaderId;
            var wounded=session.CharactersById[leader];
            wounded.CurrentHealth=wounded.MaximumHealth/2;wounded.CurrentExperience=600;
            if(wounded.ResourcePool!=null)wounded.ResourcePool.CurrentValue=40;
            var shortcuts = rail.GetComponentInParent<Canvas>().rootCanvas.transform.Find("HUD Window Shortcuts");
            Assert.That(shortcuts, Is.Not.Null);
            yield return Click(shortcuts.Find("J").GetComponent<Button>());
            var journal = View<Godmist.Views.UI.QuestJournalPanelView>();
            Assert.That(Get<GameObject>(journal, "panelRoot").activeInHierarchy, Is.True);
            journal.Close();
            yield return Frames();
            Assert.That(session.TrySetMode(Godmist.Core.Enums.GameModeState.Combat), Is.True);
            yield return Frames();
            yield return Click(shortcuts.Find("J").GetComponent<Button>());
            Assert.That(Get<GameObject>(journal, "panelRoot").activeInHierarchy, Is.True);
            Assert.That(session.CurrentMode, Is.EqualTo(Godmist.Core.Enums.GameModeState.Combat));
            yield return KeyPress(Key.J);
            Assert.That(Get<GameObject>(journal, "panelRoot").activeInHierarchy, Is.False);
            Assert.That(session.CurrentMode, Is.EqualTo(Godmist.Core.Enums.GameModeState.Combat));
            session.TrySetMode(Godmist.Core.Enums.GameModeState.Exploration);
            yield return Frames();
            yield return Click(shortcuts.Find("I").GetComponent<Button>());
            yield return Frames(12);
            var party=rail.PartyView;
            Assert.That(party,Is.Not.Null);
            Assert.That(party.Columns.Count,Is.EqualTo(session.PlayerParty.ActiveMemberIds.Count()));
            Assert.That(party.Columns.Count,Is.GreaterThanOrEqualTo(4));
            Assert.That(party.Columns.All(c=>c.Mode==PartyViewMode.Equipment),Is.True);
            Assert.That(party.GetComponentsInChildren<UIWorkspaceWindow>(true).Length,Is.EqualTo(1),"Embedded content must not register competing windows.");
            foreach(var column in party.Columns)
            {
                var character=session.CharactersById[column.CharacterId];
                Assert.That(Get<TMPro.TMP_Text>(column,"experience").text,Is.EqualTo(Godmist.Views.UI.CharacterOverviewPresentation.Experience(character,new Godmist.Systems.Characters.CharacterProgressionSystem())));
                Assert.That(Get<Image>(column,"healthFill").fillAmount,Is.EqualTo((float)character.CurrentHealth/character.MaximumHealth).Within(.001));
                Assert.That(Get<Image>(column,"experienceFill").fillAmount,Is.EqualTo((float)character.CurrentExperience/new Godmist.Systems.Characters.CharacterProgressionSystem().GetExperienceRequiredForNextLevel(character.Level)).Within(.001));
                Assert.That(Get<Image>(column,"resourceFill").fillAmount,Is.EqualTo((float)character.ResourcePool.CurrentValue/character.ResourcePool.MaxValue).Within(.001));
                var healthBar = Get<Image>(column, "healthFill").GetComponentInParent<Godmist.Views.UI.UIBarFillMask>().transform;
                var experienceBar = Get<Image>(column, "experienceFill").GetComponentInParent<Godmist.Views.UI.UIBarFillMask>().transform;
                var resourceBar = Get<GameObject>(column, "resourceTrack").transform;
                var honorMetric = Get<TMPro.TMP_Text>(column, "honor").transform.parent;
                Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(column.transform, healthBar).max.x,
                    Is.LessThan(RectTransformUtility.CalculateRelativeRectTransformBounds(column.transform, resourceBar).min.x));
                Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(column.transform, experienceBar).max.x,
                    Is.LessThan(RectTransformUtility.CalculateRelativeRectTransformBounds(column.transform, honorMetric).min.x));
                Assert.That(Get<TMPro.TMP_Text>(column,"honor").text,Is.EqualTo(character.Honor.ToString()));
                Assert.That(Get<TMPro.TMP_Text>(column,"availability").text,Does.Contain(Godmist.Core.Utilities.UiTerms.LifeState(character.LifeState)));
                Assert.That(Get<Button[]>(column,"modeButtons")[3].GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Rozwój"));
            }
            Assert.That(Get<TMPro.TMP_Text>(party,"goldText").text,Does.Not.Contain("Złoto"));
            yield return Capture("01-party-equipment");
            var a=party.Columns[0];var b=party.Columns[1];var c=party.Columns[2];var d=party.Columns[3];
            yield return Click(Get<Button[]>(b,"modeButtons")[1]);
            Assert.That(b.Mode,Is.EqualTo(PartyViewMode.Stats));Assert.That(a.Mode,Is.EqualTo(PartyViewMode.Equipment));
            yield return Click(Get<Button[]>(c,"modeButtons")[2]);
            yield return Click(Get<Button[]>(d,"modeButtons")[3]);
            Assert.That(c.Mode,Is.EqualTo(PartyViewMode.Skills));Assert.That(d.Mode,Is.EqualTo(PartyViewMode.Development));
            var library=Get<Transform>(Get<SkillLoadoutModalView>(c,"skills"),"availableSkillsRoot");
            var cards=library.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
            Assert.That(cards.Length,Is.GreaterThanOrEqualTo(5));
            Assert.That(cards.Take(5).Select(t=>Mathf.RoundToInt(((RectTransform)t).anchoredPosition.y)).Distinct().Count(),Is.EqualTo(1),"Five actual library icons must fit on the first row.");
            var development=Get<SkillTreeProgressionPanelView>(d,"development");
            Assert.That(Get<TMPro.TextMeshProUGUI>(development,"experienceValueText").gameObject.activeInHierarchy,Is.False,"Embedded development must not duplicate header XP.");
            Assert.That(Get<TMPro.TextMeshProUGUI>(development,"progressionText").gameObject.activeInHierarchy,Is.False);
            yield return Capture("02-mixed-columns");
            yield return KeyPress(Key.I);Assert.That(party.gameObject.activeInHierarchy,Is.False);yield return KeyPress(Key.I);yield return Frames(8);
            Assert.That(b.Mode,Is.EqualTo(PartyViewMode.Stats));Assert.That(c.Mode,Is.EqualTo(PartyViewMode.Skills));Assert.That(d.Mode,Is.EqualTo(PartyViewMode.Development));
            Assert.That(session.PlayerParty.ActiveLeaderId,Is.EqualTo(leader));
            party.Select(a.CharacterId,true);party.CycleColumn(1);Assert.That(party.SelectedCharacterId,Is.EqualTo(b.CharacterId));
            Assert.That(session.PlayerParty.ActiveLeaderId,Is.EqualTo(leader));
            yield return Capture("03-selected-companion");
            yield return Click(Get<Button[]>(b,"modeButtons")[0]);
            var source=a.Equipment;var recipient=b.Equipment.Character;
            var material=AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).First(i=>i!=null&&i.ItemType==ItemType.Material&&i.Weight>0&&i.IsStackable&&source.Character.Inventory.Items.All(item=>item.Definition!=i));
            var items=Enumerable.Range(1,3).Select(i=>new ItemInstance(material,"party-transfer-"+i)).ToArray();source.Character.Inventory.Items.AddRange(items);source.Refresh();yield return Frames(6);
            var row=source.GetComponentsInChildren<CharacterInfoItemView>().Single(v=>v.Item==items[0]);
            var target=Center(Get<Button>(b,"individualButton").GetComponent<RectTransform>());
            yield return Drag(Center(row.GetComponent<RectTransform>()),target);yield return Send(target);
            var popup=Get<ItemActionPopupView>(source,"itemActionPopup");Assert.That(popup,Is.Not.Null);Assert.That(popup.IsModalOpen,Is.True);
            yield return Click(Get<Button>(popup,"plusButton"));yield return Capture("04-transfer-quantity");
            yield return Click(Get<Button>(popup,"confirmButton"));
            Assert.That(items.Take(2).All(recipient.Inventory.Items.Contains),Is.True);Assert.That(source.Character.Inventory.Items.Contains(items[2]),Is.True);
            Assert.That(session.PlayerParty.ActiveLeaderId,Is.EqualTo(leader));
            yield return Capture("05-after-transfer");
            yield return Click(Get<Button>(b,"individualButton"));yield return Frames(6);
            var individual=UnityEngine.Object.FindObjectsByType<CharacterInfoWindowView>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                .Single(v=>v.Character==recipient&&v.GetComponentInParent<PartyViewColumnView>()==null);
            Assert.That(individual,Is.Not.Null);yield return Capture("06-individual-card");
            individual.GetComponent<UIWorkspaceWindow>().Close();
            yield return Click(Get<Button[]>(a,"modeButtons")[1]);yield return Capture("07-full-stats");
            Assert.That(session.PlayerParty.ActiveLeaderId,Is.EqualTo(leader));
            var dimensions=party.Window.RectTransform.sizeDelta;party.Window.RectTransform.sizeDelta=new Vector2(1180,824);yield return Frames(5);
            party.Select(d.CharacterId,true);yield return Frames(5);
            var viewport=Get<ScrollRect>(party,"memberScroll").viewport;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,d.transform);
            Assert.That(bounds.max.x,Is.LessThanOrEqualTo(viewport.rect.xMax+1));Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(viewport.rect.xMin-1));
            Assert.That(((RectTransform)d.transform).rect.width,Is.GreaterThanOrEqualTo(PartyViewWindowView.MinimumColumnWidth-1));
            yield return Capture("08-narrow-party-view");
            party.Window.RectTransform.sizeDelta=dimensions;yield return Frames(5);
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
            var column=button.GetComponentInParent<PartyViewColumnView>();
            var party=button.GetComponentInParent<PartyViewWindowView>();
            if(column!=null&&party!=null){party.Select(column.CharacterId,true);yield return Frames(3);}
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
                if(keyboard is {added:true})InputSystem.RemoveDevice(keyboard);
                foreach(var device in disabled)if(device.added)InputSystem.EnableDevice(device);
                disabled.Clear();
                if(previousMouse is { added:true })previousMouse.MakeCurrent();
                if(previousKeyboard is {added:true})previousKeyboard.MakeCurrent();
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
        }
    }
}
