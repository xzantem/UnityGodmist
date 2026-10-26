using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Serialization;
using Godmist.Runtime.Game;
using Godmist.Runtime.Characters;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;
namespace Godmist.Tests.EditMode
{
    public class PartyGroupsTests
    {
        static Party PartyOf(params string[] ids) { var party=new Party(); foreach(var id in ids) party.TryAddActiveMember(id); party.TrySetActiveLeader(ids[0]);return party; }
        [Test] public void DetachJoinAndReorderDoNotChangeRosterOrSelectedCharacter()
        {
            var p=PartyOf("a","b","c","d");
            Assert.That(p.TryDetach("c"),Is.True);Assert.That(p.TryJoinGroup("d","c"),Is.True);
            Assert.That(p.GetGroup(0),Is.EqualTo(new[]{"a","b"}));Assert.That(p.GetGroup(1),Is.EqualTo(new[]{"c","d"}));
            p.TrySetActiveLeader("d");p.TryJoinGroup("d","c",0);
            Assert.That(p.GetGroup(1),Is.EqualTo(new[]{"d","c"}));Assert.That(p.ActiveLeaderId,Is.EqualTo("d"));
            Assert.That(p.ActiveMemberIds,Is.EqualTo(new[]{"a","b","c","d"}));
            p.TryDetach("a",0);Assert.That(p.GetGroup(0),Is.EqualTo(new[]{"a"}));
            Assert.That(p.AreGrouped("a","b"),Is.False);
            Assert.That(p.TryJoinGroup("missing","a"),Is.False);
        }
        [Test] public void SingletonCanBeMovedBetweenGroupsAndBenchLeavesNoEmptyGroups()
        {
            var p=PartyOf("a","b","c");p.TryDetach("b");p.TryDetach("c");p.TryDetach("c",0);
            Assert.That(p.GetGroup(0),Is.EqualTo(new[]{"c"}));
            p.TryMoveActiveMemberToBench("c");Assert.That(p.GroupCount,Is.EqualTo(2));
            p.TryMoveBenchMemberToActive("c");Assert.That(p.GetGroup(0),Is.EqualTo(new[]{"a","c"}));
            p.TryJoinGroup("b","a");Assert.That(p.GroupCount,Is.EqualTo(1));
        }
        [Test] public void InvalidSavedGroupsAreRejectedAtomically()
        {
            var p=PartyOf("a","b");p.TryDetach("b");
            foreach(var groups in new[]{new List<List<string>>{new(){"a","a"}},new List<List<string>>{new(){"a"}},new List<List<string>>{new(){"a","x"}},new List<List<string>>{new(),new(){"a","b"}}})
                Assert.That(p.TryRestoreGroups(groups),Is.False);
            Assert.That(p.GroupCount,Is.EqualTo(2));Assert.That(p.GetGroup(1),Is.EqualTo(new[]{"b"}));
        }
        [TestCase(false)] [TestCase(true)] public void SnapshotJsonAndRestorePreserveGroupsOrLoadLegacySingleGroup(bool legacy)
        {
            var party=PartyOf("a","b","c");party.TryDetach("b");party.TrySetActiveLeader("b");
            var data=new GameSaveData { SaveVersion=1, SaveId="groups", Party=new PartySaveData() };
            typeof(GameSessionSaveSnapshotFactory).GetMethod("CopyParty",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{party,data.Party});
            if(legacy) { data.Party.Groups=null; data.Party.GroupsVersion=0; }
            var serializer=new GameSaveJsonSerializer();Assert.That(serializer.TryDeserialize(serializer.Serialize(data),out var decoded),Is.True);
            var session=new GameSession();foreach(var id in party.ActiveMemberIds)session.TryRegisterCharacter(new CharacterInstance{CharacterId=id});
            Assert.That(typeof(GameSessionRestoreFactory).GetMethod("TryRestoreParty",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{session,decoded.Party}),Is.True);
            Assert.That(session.PlayerParty.ActiveLeaderId,Is.EqualTo("b"));
            Assert.That(session.PlayerParty.GroupCount,Is.EqualTo(legacy?1:2));
            if(!legacy)Assert.That(session.PlayerParty.GetGroup(1),Is.EqualTo(new[]{"b"}));
        }
        [Test] public void RejoiningAfterCombatPreservesManualGroups()
        {
            var session=new GameSession();session.PlayerParty.TryAddActiveMember("a");session.PlayerParty.TryAddActiveMember("b");session.PlayerParty.TryDetach("b");
            var root=new GameObject("party");root.SetActive(false);
            try { var view=root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();view.Initialize(session,new Godmist.Views.Exploration.ExplorationCharacterView[0]);view.SeparateForCombat();view.RejoinAfterCombat();Assert.That(session.PlayerParty.GroupCount,Is.EqualTo(2)); }
            finally{Object.DestroyImmediate(root);}
        }
        [Test] public void OnlySelectedGroupFollowsAndUnrelatedCombatDoesNotBlockIt()
        {
            var session=new GameSession(Godmist.Core.Enums.GameModeState.Exploration);
            var root=new GameObject("party");root.SetActive(false);
            var views=new List<Godmist.Views.Exploration.ExplorationCharacterView>();
            try
            {
                for(var i=0;i<3;i++)
                {
                    var id="p"+i;var c=new CharacterInstance{CharacterId=id};session.TryRegisterCharacter(c);session.PlayerParty.TryAddActiveMember(id);
                    var go=new GameObject(id);go.transform.SetParent(root.transform);var view=go.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();view.Bind(c);view.SetPosition(new Vector2(i*4,0));views.Add(view);
                }
                session.PlayerParty.TrySetActiveLeader("p0");session.PlayerParty.TryDetach("p2");
                var combat=new Godmist.Runtime.Combat.CombatSession{CombatId="other"};combat.Units.Add(new Godmist.Runtime.Combat.BattleUnit{Character=session.CharactersById["p2"],IsPlayerSide=true});session.TrySetActiveCombat(combat);
                var controller=root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();controller.Initialize(session,views);
                var destination=typeof(Godmist.Views.Exploration.ExplorationPartyController).GetField("movementDestination",BindingFlags.NonPublic|BindingFlags.Instance);
                destination.SetValue(controller,new Vector2(10,0));controller.SeparateForCombat();
                Assert.That(destination.GetValue(controller),Is.EqualTo(new Vector2(10,0)),"Starting another member's fight must not cancel the selected group's order.");
                var follow=typeof(Godmist.Views.Exploration.ExplorationPartyController).GetMethod("MoveFollowers",BindingFlags.NonPublic|BindingFlags.Instance);
                follow.Invoke(controller,new object[]{views[0]});
                Assert.That(views[1].Position.x,Is.LessThan(4));Assert.That(views[2].Position.x,Is.EqualTo(8));
                session.PlayerParty.TryDetach("p1");var old=views[1].Position;follow.Invoke(controller,new object[]{views[0]});Assert.That(views[1].Position,Is.EqualTo(old));
            } finally{Object.DestroyImmediate(root);}
        }
        [TestCase("City/Arungard.unity")] [TestCase("Sandbox/DungeonSandbox.unity")]
        public void AuthoredScenesWireExistingRailToGroupsMenuAndParty(string name)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name);
            try
            {
                CharacterInfoPartyRailView rail=null;
                foreach(var root in scene.GetRootGameObjects()) { var found=root.GetComponentInChildren<CharacterInfoPartyRailView>(true);if(found!=null)rail=found; }
                Assert.That(rail,Is.Not.Null);var s=new UnityEditor.SerializedObject(rail);
                foreach(var field in new[]{"portraitRoot","portraitTemplate","characterInfoWindowPrefab","workspaceManager","partyController","groupTemplate","menuTemplate","inspectTemplate","dropZone","dragGhost","newGroupMarker"})
                    Assert.That(s.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
                foreach (var root in scene.GetRootGameObjects())
                {
                    Assert.That(root.GetComponentsInChildren<Godmist.Views.UI.CombatPartyRailView>(true), Is.Empty);
                    foreach (var coordinator in root.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonCombatCoordinator>(true))
                    {
                        var coordinatorState = new UnityEditor.SerializedObject(coordinator);
                        var combatHud = (GameObject)coordinatorState.FindProperty("combatHudRoot").objectReferenceValue;
                        var sharedHud = (GameObject)coordinatorState.FindProperty("explorationHudRoot").objectReferenceValue;
                        Assert.That(rail.transform.IsChildOf(sharedHud.transform), Is.True);
                        Assert.That(rail.transform.IsChildOf(combatHud.transform), Is.False);
                        sharedHud.SetActive(true); combatHud.SetActive(false);
                        Assert.That(rail.gameObject.activeInHierarchy, Is.True);
                        combatHud.SetActive(true);
                        Assert.That(rail.gameObject.activeInHierarchy, Is.True);
                    }
                }
            } finally{UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);}
        }
        [Test] public void DragBelowGroupDetachesButDropOutsideRailCancels()
        {
            var root=new GameObject("canvas",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var session=new GameSession();for(var i=0;i<3;i++){var id="p"+i;session.TryRegisterCharacter(new CharacterInstance{CharacterId=id});session.PlayerParty.TryAddActiveMember(id);}
            var zone=new GameObject("zone",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup));zone.transform.SetParent(root.transform,false);
            var rect=(RectTransform)zone.transform;rect.sizeDelta=new Vector2(108,600);rect.pivot=new Vector2(0,1);rect.anchorMin=rect.anchorMax=new Vector2(0,1);
            var layout=zone.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            var rail=zone.AddComponent<CharacterInfoPartyRailView>();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            try
            {
                void Set(string field,object value)=>typeof(CharacterInfoPartyRailView).GetField(field,flags).SetValue(rail,value);
                Set("session",session);Set("portraitRoot",rect);Set("dropZone",rect);
                Set("portraitTemplate",UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab").GetComponent<CharacterInfoPartyPortraitView>());
                Set("groupTemplate",UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyGroup.prefab").GetComponent<PartyRailGroupView>());
                // This fixture must contain three authored cards plus a detach drop area.
                rect.sizeDelta = new Vector2(208, 3 * UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab").GetComponent<LayoutElement>().preferredHeight + 32);
                typeof(CharacterInfoPartyRailView).GetMethod("Rebuild",flags).Invoke(rail,null);Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(rect);Canvas.ForceUpdateCanvases();
                var rows=zone.GetComponentsInChildren<CharacterInfoPartyPortraitView>();var group=zone.GetComponentInChildren<PartyRailGroupView>();var corners=new Vector3[4];group.Rect.GetWorldCorners(corners);
                var e=new UnityEngine.EventSystems.PointerEventData(null){position=new Vector2(-10000,-10000)};
                rail.BeginDrag(rows[1],e);rail.EndDrag(e);Assert.That(session.PlayerParty.GroupCount,Is.EqualTo(1));
                e.position=RectTransformUtility.WorldToScreenPoint(null,corners[0])+new Vector2(40,-15);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect,e.position),Is.True);
                rail.BeginDrag(rows[1],e);rail.EndDrag(e);Assert.That(session.PlayerParty.GroupCount,Is.EqualTo(2));Assert.That(session.PlayerParty.GetGroup(1),Is.EqualTo(new[]{rows[1].CharacterId}));
            }finally{Object.DestroyImmediate(root);}
        }
        [Test] public void PopupUsesAuthoredRowHeightsAndClampsToCanvas()
        {
            var root=new GameObject("canvas",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            try
            {
                var menu=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyContextMenu.prefab"),root.transform).GetComponent<PartyRailMenuView>();
                var actions=new List<(string,System.Action,bool)>();for(var i=0;i<5;i++)actions.Add(("Action "+i,()=>{},true));menu.Show(new Vector2(Screen.width,0),actions);Canvas.ForceUpdateCanvases();
                var so=new UnityEditor.SerializedObject(menu);var panel=(RectTransform)so.FindProperty("panel").objectReferenceValue;
                Assert.That(panel.rect.height,Is.GreaterThanOrEqualTo(180));
                foreach(var button in panel.GetComponentsInChildren<Button>())Assert.That(((RectTransform)button.transform).rect.height,Is.GreaterThanOrEqualTo(36));
                var corners=new Vector3[4];panel.GetWorldCorners(corners);Assert.That(corners[0].y,Is.GreaterThanOrEqualTo(-1));Assert.That(corners[2].x,Is.LessThanOrEqualTo(Screen.width+1));
            }finally{Object.DestroyImmediate(root);}
        }
        [Test] public void SharedRowUsesCombatVisualsAndClearsTurnMarkerOutsideCombat()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab");
            var row = Object.Instantiate(prefab);
            try
            {
                var view = row.GetComponent<CharacterInfoPartyPortraitView>();
                var presenter = row.GetComponent<Godmist.Views.UI.CombatPartyMemberView>();
                Assert.That(presenter, Is.Not.Null);
                var state = new UnityEditor.SerializedObject(presenter);
                var character = new CharacterInstance { CharacterId = "test", CurrentHealth = 1 };
                view.Bind(character, null);
                view.RefreshPresentation(new Godmist.Runtime.Combat.BattleUnit { Character = character }, true, true);
                Assert.That(((Image)state.FindProperty("turnMarker").objectReferenceValue).enabled, Is.True);
                Assert.That(((Image)state.FindProperty("selectedMarker").objectReferenceValue).enabled, Is.True);
                view.RefreshPresentation(null, false, false);
                Assert.That(((Image)state.FindProperty("turnMarker").objectReferenceValue).enabled, Is.False);
                Assert.That(((Image)state.FindProperty("selectedMarker").objectReferenceValue).enabled, Is.False);
                Assert.That(((Slider)state.FindProperty("healthSlider").objectReferenceValue).value, Is.EqualTo(1));
                Assert.That(row.GetComponent<Button>().targetGraphic.raycastTarget, Is.True);
                foreach (var slider in row.GetComponentsInChildren<Slider>(true)) Assert.That(slider.interactable, Is.False);
            }
            finally { Object.DestroyImmediate(row); }
        }

        [Test] public void GroupPrefabComputesHeightFromMembersAndGapIsNotConnected()
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyGroup.prefab");
            var rowPrefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab");
            Assert.That(prefab,Is.Not.Null);Assert.That(rowPrefab,Is.Not.Null);
            var root=new GameObject("canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var group=Object.Instantiate(prefab,root.transform).GetComponent<PartyRailGroupView>();
                for(var i=0;i<2;i++)
                {
                    var row=Object.Instantiate(rowPrefab,group.MemberRoot).GetComponent<CharacterInfoPartyPortraitView>();
                    row.gameObject.SetActive(true);row.ConfigureRail(null,i==0,i==1);
                    var s=new UnityEditor.SerializedObject(row);
                    Assert.That(((Image)s.FindProperty("groupLineTop").objectReferenceValue).enabled,Is.False);
                    Assert.That(((Image)s.FindProperty("groupLineBottom").objectReferenceValue).enabled,Is.False);
                }
                group.ConfigurePresentation(2, false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(group.Rect);
                Assert.That(LayoutUtility.GetPreferredHeight(group.Rect),Is.EqualTo(410).Within(.01f));
            } finally{Object.DestroyImmediate(root);}
        }
    }
}
