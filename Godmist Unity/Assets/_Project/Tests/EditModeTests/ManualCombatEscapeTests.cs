using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ManualCombatEscapeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        CombatSandboxBootstrap host;
        CombatExecutionDriver driver;
        CombatSession session;
        BattleUnit player, ally, enemy;
        bool seen;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Manual escape runtime"); root.SetActive(false);
            host = root.AddComponent<CombatSandboxBootstrap>();
            player = Unit("player", true); ally = Unit("ally", true); enemy = Unit("enemy", false);
            session = new CombatSession { IsSeamless = true, IsDungeonWide = true, Grid = new BattleGrid(8,8) };
            session.DungeonContext = new DungeonCombatContext("dungeon",0,"combat","room",Vector2Int.zero,Vector2Int.right,
                new[]{Vector2Int.zero,Vector2Int.right,Vector2Int.up},null,null);
            session.Units.AddRange(new[]{player,ally,enemy});
            foreach(var unit in session.Units) session.TurnParticipants.Add(new CombatTurnParticipant(unit));
            session.ActiveUnit = player; session.ActiveParticipant = session.TurnParticipants[0];
            session.Contact = new DungeonContactState((e,p)=>seen);
            typeof(CombatSandboxBootstrap).GetField("seamlessDungeon",Private).SetValue(host,true);
            typeof(CombatSandboxBootstrap).GetField("unitViewSpawner",Private).SetValue(host,root.AddComponent<CombatUnitViewSpawner>());
            Assert.That(host.TryBegin(session),Is.True);
            driver=host.GetDriver(session);
            host.Focus(session);
        }
        [TearDown] public void TearDown()
        {
            host.End(session); Object.DestroyImmediate(root);
        }
        static BattleUnit Unit(string id,bool isPlayer)
        {
            var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var character=new CharacterInstanceFactory().Create(definition);
            character.CharacterId=id;character.CurrentHealth=12;
            return new BattleUnit{IsPlayerSide=isPlayer,CurrentActionPoints=0,CurrentActionValue=500,
                Position=new GridPosition(1,1),Character=character};
        }
        void LoseContact()
        {
            seen=false;
            for(var i=0;i<session.Contact.EscapeReactionTurns;i++)
            { session.Contact.BeginTurn(session,enemy);session.Contact.EndTurn(session,enemy); }
        }

        [Test] public void LosingContactOnlyEnablesAction_ExplicitEscapeWithdrawsOnlyActiveUnitWithoutApOrRoll()
        {
            LoseContact();
            Assert.That(driver.CanEscape(),Is.True);
            CollectionAssert.Contains(session.Units,player);
            Assert.That(session.DepartedUnits,Is.Empty);
            var chance=session.CurrentEscapeChancePercent;
            typeof(CombatExecutionDriver).GetField("escapeSystem",Private).SetValue(driver,
                new CombatEscapeSystem(rollPercent:()=>throw new InvalidOperationException("Dungeon escape must not roll.")));
            var notifications=0;
            driver.UnitLeftCombat+=(s,u)=>{Assert.That(s,Is.SameAs(session));Assert.That(u,Is.SameAs(player));notifications++;};
            driver.RequestEscape();
            CollectionAssert.DoesNotContain(session.Units,player);
            CollectionAssert.Contains(session.Units,ally);CollectionAssert.Contains(session.Units,enemy);
            Assert.That(session.DepartedUnits[player.Character.CharacterId],Is.SameAs(player));
            Assert.That(session.EscapePenaltyEligibleCharacterIds,Does.Contain(player.Character.CharacterId));
            Assert.That(session.Outcome,Is.EqualTo(CombatOutcomeState.Ongoing));
            Assert.That(session.ActiveUnit,Is.Not.SameAs(player));
            Assert.That(player.CurrentActionPoints,Is.Zero);
            Assert.That(player.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(session.CurrentEscapeChancePercent,Is.EqualTo(chance));
            Assert.That(notifications,Is.EqualTo(1));
        }

        [Test] public void ReacquiredContactBetweenHoverAndClickBlocksEscapeAndResetsReadiness()
        {
            LoseContact();Assert.That(driver.CanEscape(),Is.True);
            seen=true;driver.RequestEscape();
            CollectionAssert.Contains(session.Units,player);
            Assert.That(driver.GetEscapeBlockReason(),Is.EqualTo("Wrogowie wciąż cię widzą."));
            seen=false;
            Assert.That(driver.CanEscape(),Is.False);
            Assert.That(driver.GetEscapeBlockReason(),Is.EqualTo("Wrogowie wciąż cię szukają."));
            Assert.That(session.ActiveUnit,Is.SameAs(player));
            Assert.That(session.DepartedUnits,Is.Empty);
        }

        [TestCase("pending")][TestCase("presentation")][TestCase("enemy")][TestCase("downed")][TestCase("finished")][TestCase("control")]
        public void BlockedEscapeDoesNotChangeMembershipOrTurn(string blocker)
        {
            LoseContact();
            switch(blocker)
            {
                case "pending":session.MembershipChangePending=true;break;
                case "presentation":typeof(CombatExecutionDriver).GetField("skillPresentationEnds",Private).SetValue(driver,Time.time+30);break;
                case "enemy":player.IsPlayerSide=false;break;
                case "downed":player.Character.LifeState=CharacterLifeState.Downed;break;
                case "finished":session.Outcome=CombatOutcomeState.Victory;break;
                case "control":player.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId=StatusEffectSystem.StunId});break;
            }
            Assert.That(driver.CanEscape(),Is.False);
            Assert.That(driver.GetEscapeBlockReason(),Is.Not.Empty);
            driver.RequestEscape();
            CollectionAssert.Contains(session.Units,player);
            Assert.That(session.ActiveUnit,Is.SameAs(player));Assert.That(session.DepartedUnits,Is.Empty);
        }

        [Test] public void LastPlayerEscapeResolvesEscapeAndReentryPreservesResources()
        {
            session.Units.Remove(ally);session.TurnParticipants.RemoveAll(p=>p.Unit==ally);
            LoseContact();driver.RequestEscape();
            Assert.That(session.Outcome,Is.EqualTo(CombatOutcomeState.Escaped));
            Assert.That(player.Character.CurrentHealth,Is.EqualTo(12));
            session.Outcome=CombatOutcomeState.Ongoing;
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session,player,new GridPosition(3,3)),Is.True);
            Assert.That(player.CurrentActionPoints,Is.Zero);
            Assert.That(session.DepartedUnits,Is.Empty);
            session.ActiveUnit=player;
            session.ActiveParticipant=session.TurnParticipants.Single(p=>p.Unit==player);
            Assert.That(driver.CanEscape(),Is.False,"Joining must not retain old lost-contact progress.");
        }

        [Test] public void EveryLivingEnemyMustFinishItsContactLossTurns()
        {
            LoseContact();
            var arrival=Unit("arrival",false);session.Units.Add(arrival);
            Assert.That(driver.CanEscape(),Is.False);
            for(var i=0;i<session.Contact.EscapeReactionTurns;i++)
            {session.Contact.BeginTurn(session,arrival);session.Contact.EndTurn(session,arrival);}
            Assert.That(driver.CanEscape(),Is.True);
        }

        [Test] public void DisabledButtonTooltipShowsCurrentBlockerAndUpdatesWhileHovered()
        {
            var canvas=new GameObject("escape tooltip canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var tooltip=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"),canvas.transform).GetComponent<UIWorkspaceTooltipView>();
                var button=new GameObject("disabled escape",typeof(RectTransform),typeof(Image),typeof(Button),typeof(CombatSimpleTooltipTrigger));
                button.transform.SetParent(canvas.transform,false);button.GetComponent<Button>().interactable=false;
                var trigger=button.GetComponent<CombatSimpleTooltipTrigger>();
                trigger.Initialize(tooltip,"Ucieknij","Wrogowie wciąż cię widzą.");
                trigger.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(null));
                void ShowPending()=>((Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow",Private).GetValue(tooltip))();
                ShowPending();
                var body=(TMPro.TMP_Text)typeof(UIWorkspaceTooltipView).GetField("bodyText",Private).GetValue(tooltip);
                Assert.That(body.text,Is.EqualTo("Wrogowie wciąż cię widzą."));
                trigger.UpdateDescription("Wrogowie wciąż cię szukają.");ShowPending();
                Assert.That(body.text,Is.EqualTo("Wrogowie wciąż cię szukają."));
                trigger.OnPointerExit(new UnityEngine.EventSystems.PointerEventData(null));
                Assert.That((bool)typeof(UIWorkspaceTooltipView).GetField("sourceHovered",Private).GetValue(tooltip),Is.False);
            }
            finally{Object.DestroyImmediate(canvas);}
        }

        [Test] public void CoordinatorReleaseRetainsUnitAndClearsBorrowedCombatView()
        {
            var coordinator=root.AddComponent<DungeonCombatCoordinator>();
            var spawner=root.GetComponent<CombatUnitViewSpawner>();
            spawner.ConfigureDungeon(new GameSession(),root.AddComponent<DungeonFloorMapView>());
            var party=root.AddComponent<ExplorationPartyController>();
            typeof(DungeonCombatCoordinator).GetField("unitViews",Private).SetValue(coordinator,spawner);
            typeof(DungeonCombatCoordinator).GetField("party",Private).SetValue(coordinator,party);
            var actor=new GameObject("borrowed escaped actor");
            try
            {
                var view=actor.AddComponent<ExplorationCharacterView>();
                spawner.Borrow(session,player,view);
                host.UnitLeftCombat+=(s,u)=>typeof(DungeonCombatCoordinator).GetMethod("HandleUnitLeftCombat",Private).Invoke(coordinator,new object[]{s,u});
                LoseContact();driver.RequestEscape();
                Assert.That(view.CombatUnit,Is.Null);
                var retained=(Dictionary<string,BattleUnit>)typeof(DungeonCombatCoordinator).GetField("retainedPlayers",Private).GetValue(coordinator);
                Assert.That(retained[player.Character.CharacterId],Is.SameAs(player));
            }
            finally{Object.DestroyImmediate(actor);}
        }

        [Test] public void PrefabHasThreeMatchingButtonsAndLiveDisabledTooltip()
        {
            var prefab=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatActionPanel.prefab");
            try
            {
                var actions=prefab.GetComponentInChildren<CombatActionButtonsView>(true);
                var state=new SerializedObject(actions);
                state.FindProperty("sandboxBootstrap").objectReferenceValue=host;state.ApplyModifiedPropertiesWithoutUndo();
                var end=(Button)state.FindProperty("endTurnButton").objectReferenceValue;
                var items=(Button)state.FindProperty("consumablesButton").objectReferenceValue;
                var escape=(Button)state.FindProperty("escapeButton").objectReferenceValue;
                Assert.That(escape,Is.Not.Null);
                var escapeImage=(Image)state.FindProperty("escapeIcon").objectReferenceValue;
                var disabledMaterial=(Material)state.FindProperty("escapeDisabledMaterial").objectReferenceValue;
                Assert.That(escapeImage,Is.Not.Null);
                Assert.That(Quaternion.Angle(escapeImage.transform.rotation,Quaternion.identity),Is.LessThan(.01f),"Escape must not inherit the end-turn icon's -90 degree correction.");
                Assert.That(disabledMaterial.shader.name,Is.EqualTo("Godmist/UI/Empty Slot Silhouette"));
                Assert.That(actions.GetComponent<LayoutElement>().preferredWidth,Is.GreaterThanOrEqualTo(184),"The layout must reserve space for all three buttons.");
                foreach(var b in new[]{end,items,escape})
                {
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b.gameObject),Is.EqualTo("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatIconButton.prefab"));
                    Assert.That(((RectTransform)b.transform).rect.size,Is.EqualTo(new Vector2(56,56)));
                    Assert.That(b.targetGraphic.GetComponent<Image>().sprite,Is.SameAs(end.targetGraphic.GetComponent<Image>().sprite));
                }
                typeof(CombatActionButtonsView).GetMethod("Start",Private).Invoke(actions,null);
                typeof(CombatActionButtonsView).GetMethod("Update",Private).Invoke(actions,null);
                var trigger=escape.GetComponent<CombatSimpleTooltipTrigger>();
                var normalMaterial=escapeImage.material;
                Assert.That(normalMaterial,Is.SameAs(disabledMaterial));
                Assert.That(escape.targetGraphic.color.r,Is.LessThan(.6f));
                Assert.That(escape.gameObject.activeSelf,Is.True);Assert.That(escape.interactable,Is.False);
                Assert.That(typeof(CombatSimpleTooltipTrigger).GetField("description",Private).GetValue(trigger),Is.EqualTo("Wrogowie wciąż cię szukają."));
                LoseContact();typeof(CombatActionButtonsView).GetMethod("Update",Private).Invoke(actions,null);
                Assert.That(escape.interactable,Is.True);
                Assert.That(escapeImage.material,Is.Not.SameAs(disabledMaterial));
                Assert.That(escapeImage.color,Is.EqualTo(Color.white));
                Assert.That(escape.targetGraphic.color,Is.EqualTo(Color.white));
                seen=true;typeof(CombatActionButtonsView).GetMethod("Update",Private).Invoke(actions,null);
                Assert.That(escape.interactable,Is.False);
                Assert.That(escapeImage.material,Is.SameAs(disabledMaterial));
                Assert.That(escape.targetGraphic.color.r,Is.LessThan(.6f));
                Assert.That(typeof(CombatSimpleTooltipTrigger).GetField("description",Private).GetValue(trigger),Is.EqualTo("Wrogowie wciąż cię widzą."));
                Assert.That(escape.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="X"),Is.True);
                var icon=escape.GetComponentsInChildren<Image>(true).Single(i=>i.name=="ActionIcon").sprite;
                Assert.That(icon.rect.size,Is.EqualTo(new Vector2(64,64)));
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(icon));
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(32));
            }
            finally
            {
                // Shortcut labels are runtime-owned and use runtime Destroy.
                foreach(var label in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t=>t.name.StartsWith("Shortcut (")).ToArray()) Object.DestroyImmediate(label.gameObject);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
