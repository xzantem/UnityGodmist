using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class WorldObjectPrimaryInteractionTests
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        static string Output=>Path.GetFullPath("../LocalHandoffs/world-object-primary-pass-20261020");
        GameObject root,canvas;
        ExplorationPartyController party;
        ExplorationCharacterView actor;
        DungeonFloorMapView terrain;
        Tilemap tiles;
        DungeonFloorMap map;
        DungeonFloorInstance floor;
        GameSession session;
        ExplorationWorldObjectInteraction pointer;
        DungeonObjectInstance model;
        DoorDungeonObjectDefinition doorDefinition;
        int exitUses;
        bool leverOnlyDoor;
        static object Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
        static void Set(object o,string name,object v)=>o.GetType().GetField(name,Flags).SetValue(o,v);
        Vector2 Center(Vector2Int c)=>tiles.GetCellCenterWorld(new Vector3Int(c.x,c.y));
        [SetUp] public void Setup()
        {
            exitUses=0;leverOnlyDoor=false;model=null;pointer=null;canvas=null;doorDefinition=null;
            Directory.CreateDirectory(Output);root=new GameObject("World object tests",typeof(Grid));
            tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer)).GetComponent<Tilemap>();tiles.transform.SetParent(root.transform);
            terrain=root.AddComponent<DungeonFloorMapView>();Set(terrain,"floorTilemap",tiles);
            map=new DungeonFloorMap();for(var y=0;y<7;y++)for(var x=0;x<12;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
            floor=new DungeonFloorInstance(0);floor.TrySetMap(map);Set(terrain,"boundMap",map);Get<DungeonElevationViewState>(terrain,"elevation").Bind(map);
            floor.Fog.SetVisible(map.FloorCells.Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
            session=new GameSession(GameModeState.Exploration);
            var character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            session.TryRegisterCharacter(character);session.TryAddCharacterToActiveParty(character.CharacterId);session.TrySetActiveLeader(character.CharacterId);
            var dungeon=new DungeonInstance("world.objects",1);dungeon.TryAddFloor(floor);session.TrySetActiveDungeon(dungeon);
            actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"),root.transform);
            actor.Bind(character);actor.ConfigureTerrain(map,terrain);actor.SetPosition(Center(new Vector2Int(1,3)));
            party=root.AddComponent<ExplorationPartyController>();party.Initialize(session,new[]{actor});party.ConfigureTraversal(floor,terrain,null);
        }
        [TearDown] public void Cleanup()
        {
            if(canvas!=null)Object.DestroyImmediate(canvas);
            foreach(var visual in root.GetComponentsInChildren<CombatUnitVisualStateView>(true))Invoke(visual,"OnDestroy");
            foreach(var feedback in root.GetComponentsInChildren<DungeonLootFloatingText>(true))Invoke(feedback,"OnDestroy");
            Object.DestroyImmediate(root);if(doorDefinition!=null)Object.DestroyImmediate(doorDefinition);Physics2D.SyncTransforms();
        }
        void Create(string kind)
        {
            var position=new Vector2Int(8,3);var world=Center(position);MonoBehaviour view;
            var definitionRoot="Assets/_Project/ScriptableObjects/Dungeons/Forest/";
            if(kind=="lever"||kind=="condition")
            {
                var api=typeof(DungeonObjectView).Assembly.GetType("Godmist.Views.Dungeons.DungeonForestObjectVisuals");
                if(kind=="lever")
                {
                    var definition=AssetDatabase.LoadAssetAtPath<LeverDungeonObjectDefinition>(definitionRoot+"DO_Forest_Lever.asset");
                    var native=(DungeonLeverView)api.GetMethod("CreateLeverView").Invoke(null,new object[]{root.transform,"Winch",definition});
                    var lever=new LeverDungeonObjectInstance("lever",definition,position);model=lever;floor.TryAddObject(lever);
                    native.Initialize(party,floor,lever,world,tiles);native.BindElevationView(terrain);view=native;
                }
                else
                {
                    var definition=AssetDatabase.LoadAssetAtPath<ConditionDungeonObjectDefinition>(definitionRoot+"DO_Forest_Condition.asset");
                    var native=(DungeonConditionView)api.GetMethod("CreateConditionView").Invoke(null,new object[]{root.transform,"Mark",definition});
                    var condition=new ConditionDungeonObjectInstance("condition",definition,position);model=condition;floor.TryAddObject(condition);
                    native.Initialize(party,floor,condition,world,tiles);native.BindElevationView(terrain);view=native;
                }
            }
            else if(kind=="door")
            {
                doorDefinition=ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
                if(leverOnlyDoor)Set(doorDefinition,"<InteractionMode>k__BackingField",DoorInteractionMode.LeverOnly);
                var door=new DoorDungeonObjectInstance("door",doorDefinition,new DungeonCellEdge(position,position+Vector2Int.right));model=door;floor.TryAddObject(door);
                var native=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonDoorView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonDoor_Debug.prefab"),root.transform);
                native.Initialize(party,floor,door,tiles);native.BindElevationView(terrain);view=native;
            }
            else if(kind=="exit")
            {
                var native=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab"),root.transform);
                native.InitializeTransition(party,world,()=>exitUses++);
                terrain.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>(definitionRoot+"DVP_Forest_Prototype.asset"));
                terrain.StyleForestTransition(native);view=native;
            }
            else
            {
                session.TryClearActiveDungeon();party.ConfigureTraversal(null,null,null);actor.ConfigureTerrain(null,null);
                var node=new GameObject("Campaign stash",typeof(SpriteRenderer));node.transform.SetParent(root.transform);node.transform.position=world;
                node.GetComponent<SpriteRenderer>().sprite=Resources.Load<Sprite>("Arungard/CampaignStorageChestV1");
                var box=node.AddComponent<BoxCollider2D>();box.size=new Vector2(1.5f,.45f);box.offset=new Vector2(0,.225f);
                var native=node.AddComponent<CampaignStorageChestView>();Set(native,"partyController",party);
                Set(native,"storageWindowPrefab",AssetDatabase.LoadAssetAtPath<CampaignStorageWindowView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CampaignStorageWindow.prefab"));
                Invoke(native,"OnEnable");view=native;
                canvas=new GameObject("Storage UI",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
                Invoke(canvas.AddComponent<UIWorkspaceWindowManager>(),"Awake");
            }
            pointer=view.GetComponent<ExplorationWorldObjectInteraction>();Assert.That(pointer,Is.Not.Null);Physics2D.SyncTransforms();
        }
        Vector2 HitPoint()=>pointer.Target is DungeonExitView?pointer.Position:
            (Vector2)pointer.GetComponentsInChildren<SpriteRenderer>().First(r=>r.enabled&&r.sprite!=null).bounds.center;
        CombatSession SetupCombatDoor(bool open)
        {
            var source=(DoorDungeonObjectInstance)model;
            floor.TrySetDoorOpen(source.InstanceId,open);
            ((DungeonDoorView)pointer.Target).RefreshState();
            var player=new BattleUnit {Character=actor.RuntimeCharacter,IsPlayerSide=true,
                Position=new GridPosition(8,3),MaxActionPoints=1000,CurrentActionPoints=1000};
            var enemyCharacter=new CharacterInstanceFactory().Create(actor.RuntimeCharacter.Definition);
            enemyCharacter.CharacterId="door-test-enemy";
            var enemy=new BattleUnit {Character=enemyCharacter,Position=new GridPosition(0,0),MaxActionPoints=1000,CurrentActionPoints=1000};
            var combat=new CombatSession {CombatId="door-hover",IsSeamless=true,Grid=new BattleGrid(12,7),
                ActiveUnit=player,Units=new(){player,enemy},DungeonContext=new DungeonCombatContext("world.objects",0,"encounter","room",
                    Vector2Int.zero,Vector2Int.right,map.FloorCells,null,null)};
            combat.MapObjects.Add(new DoorCombatMapObject(source.InstanceId,new GridPosition(8,3),new GridPosition(9,3),source.InteractionMode,open));
            var host=root.AddComponent<CombatSandboxBootstrap>();
            Set(host,"seamlessDungeon",true);Set(host,"unitViewSpawner",root.AddComponent<CombatUnitViewSpawner>());
            Assert.That(host.TryBegin(combat),Is.True);host.Focus(combat);
            host.CombatMapObjectChanged+=value=>floor.TrySetDoorOpen(value.ObjectId,((DoorCombatMapObject)value).IsOpen);
            Assert.That(session.TryRegisterDungeonCombat(combat),Is.True);session.TryFocusCombat(combat);session.TrySetMode(GameModeState.Combat);
            var coordinator=root.AddComponent<DungeonCombatCoordinator>();
            Set(coordinator,"initialized",true);Set(coordinator,"game",session);Set(coordinator,"floor",floor);
            Set(coordinator,"combatHost",host);Set(coordinator,"targeting",root.AddComponent<CombatTargetSelectionController>());
            party.SetCombatCoordinator(coordinator);
            Assert.That(party.CanRequestWorldInteraction,Is.False);
            return combat;
        }

        [TestCase(false)] [TestCase(true)]
        public void CombatDoorSpriteHoverAndClickUsePaidToggleWithoutMovementOrDoubleUse(bool open)
        {
            Create("door");var combat=SetupCombatDoor(open);var player=combat.ActiveUnit;
            Assert.That(pointer.CanRequestInteraction,Is.True);
            Assert.That(pointer.IsPointerHit(HitPoint()),Is.True,"Opened door artwork must remain pickable without a blocking collider.");
            pointer.SetInspectionHighlight(true);Invoke(pointer,"LateUpdate");
            Assert.That(pointer.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.name=="Visual State Outline"&&r.enabled),Is.True);
            Assert.That(pointer.TryPrimaryInteraction(HitPoint()),Is.True);
            Assert.That(((DoorDungeonObjectInstance)model).IsOpen,Is.EqualTo(!open));
            Assert.That(player.CurrentActionPoints,Is.EqualTo(800));
            Assert.That(player.Position,Is.EqualTo(new GridPosition(8,3)));
            Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);
            Assert.That(pointer.TryPrimaryInteraction(HitPoint()),Is.False);
            var movement=root.AddComponent<CombatMovementInputController>();
            movement.IsWorldInteractionClaimedThisFrame=()=>party.IsWorldInteractionClaimedThisFrame;
            Invoke(movement,"Update");Assert.That(movement.ConsumedPrimaryClickThisFrame,Is.True);
            Assert.That(player.CurrentActionPoints,Is.EqualTo(800));
        }

        [Test]
        public void CombatDoorEShortcutKeepsPaidInteraction()
        {
            Create("door");var combat=SetupCombatDoor(true);var player=combat.ActiveUnit;
            Assert.That(pointer.TryKeyboardInteraction(),Is.True);
            Assert.That(((DoorDungeonObjectInstance)model).IsOpen,Is.False);
            Assert.That(player.CurrentActionPoints,Is.EqualTo(800));
            Assert.That(pointer.TryKeyboardInteraction(),Is.False);
        }

        [TestCase("AP")] [TestCase("enemy-turn")] [TestCase("targeting")] [TestCase("remote")] [TestCase("hidden")]
        public void InvalidCombatDoorClearsHoverAndNeverUsesFreeExplorationToggle(string reason)
        {
            Create("door");var combat=SetupCombatDoor(true);
            pointer.SetInspectionHighlight(true);Invoke(pointer,"LateUpdate");
            if(reason=="AP")combat.ActiveUnit.CurrentActionPoints=0;
            if(reason=="enemy-turn")combat.ActiveUnit=combat.Units[1];
            if(reason=="targeting")Set(root.GetComponent<CombatTargetSelectionController>(),"selectedSkill",actor.RuntimeCharacter.GetBasicAttackSkill());
            if(reason=="remote")combat.ActiveUnit.Position=new GridPosition(5,3);
            if(reason=="hidden")
            {
                Set(terrain,"<FogFloor>k__BackingField",floor);
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            }
            Invoke(pointer,"LateUpdate");
            Assert.That(pointer.CanRequestInteraction,Is.False);
            Assert.That(pointer.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Visual State Outline").All(r=>!r.enabled),Is.True);
            Assert.That(pointer.TryPrimaryInteraction(pointer.Position),Is.False);
            Assert.That(pointer.TryKeyboardInteraction(),Is.False);
            Assert.That(((DoorDungeonObjectInstance)model).IsOpen,Is.True);
        }
        void Used(string kind)
        {
            if(kind=="lever")Assert.That(((LeverDungeonObjectInstance)model).IsActivated,Is.True);
            if(kind=="condition")Assert.That(((ConditionDungeonObjectInstance)model).IsSatisfied,Is.True);
            if(kind=="door")Assert.That(((DoorDungeonObjectInstance)model).IsOpen,Is.True);
            if(kind=="exit")Assert.That(exitUses,Is.EqualTo(1));
            if(kind=="stash")Assert.That(canvas.GetComponentsInChildren<CampaignStorageWindowView>(true).Length,Is.EqualTo(1));
        }
        [TestCase("exit")] [TestCase("stash")] [TestCase("lever")] [TestCase("condition")] [TestCase("door")]
        public void FarClickClaimsInputAndPlansValidApproach(string kind)
        {
            Create(kind);Assert.That(pointer.CanRequestInteraction,Is.True);Assert.That(pointer.TryPrimaryInteraction(HitPoint()),Is.True);
            Assert.That(party.IsWorldPrimaryClickClaimed,Is.True);Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.SameAs(pointer));
            var goal=Get<Vector2?>(party,"movementDestination");Assert.That(goal.HasValue,Is.True);Assert.That(pointer.CanInteractFrom(goal.Value),Is.True);
            Assert.That(exitUses,Is.Zero);if(model is LeverDungeonObjectInstance l)Assert.That(l.IsActivated,Is.False);
        }
        [TestCase("exit")] [TestCase("stash")] [TestCase("lever")] [TestCase("condition")] [TestCase("door")]
        public void ArrivalUsesActualObjectOnceAndClearsPendingRequest(string kind)
        {
            Create(kind);Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);
            actor.SetPosition(Get<Vector2?>(party,"movementDestination").Value);
            if(kind!="stash")
            {
                Get<Queue<DungeonTraversalPosition>>(party,"traversalPath").Clear();Invoke(party,"TickTraversalMovement",actor);
                Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.SameAs(pointer));
            }
            Assert.That((bool)Invoke(party,"TickWorldObjectApproach"),Is.True);Used(kind);
            Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);
            Assert.That((bool)Invoke(party,"TickWorldObjectApproach"),Is.False);Used(kind);
        }
        [TestCase("exit")] [TestCase("stash")] [TestCase("lever")] [TestCase("condition")] [TestCase("door")]
        public void NearInteractionUsesTheSameActionAsE(string kind)
        {
            Create(kind);Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);var goal=Get<Vector2?>(party,"movementDestination").Value;
            party.CancelMovement();actor.SetPosition(goal);Assert.That(pointer.Target.CanInteract(),Is.True);Assert.That(pointer.TryKeyboardInteraction(),Is.True);Used(kind);
        }
        [TestCase("disabled")] [TestCase("destroyed")] [TestCase("combat")] [TestCase("dead")] [TestCase("used")]
        public void InvalidPendingTargetCancelsWithoutUsingIt(string reason)
        {
            Create("lever");Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);
            if(reason=="disabled")((MonoBehaviour)pointer.Target).enabled=false;
            if(reason=="destroyed")Object.DestroyImmediate(pointer.gameObject);
            if(reason=="combat")session.TrySetActiveCombat(new Godmist.Runtime.Combat.CombatSession{CombatId="blocked"});
            if(reason=="dead")actor.RuntimeCharacter.LifeState=CharacterLifeState.Dead;
            if(reason=="used")((LeverDungeonObjectInstance)model).TryToggle(out _);
            Assert.That((bool)Invoke(party,"TickWorldObjectApproach"),Is.False);
            Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);Assert.That(Get<Vector2?>(party,"movementDestination"),Is.Null);
            if(reason!="used")Assert.That(((LeverDungeonObjectInstance)model).IsActivated,Is.False);
        }
        [Test] public void NewMovementCancelAndClaimedClickCannotReuseOldTarget()
        {
            Create("condition");party.ClaimWorldPrimaryClick();Assert.That(pointer.TryPrimaryInteraction(HitPoint()),Is.False);
            Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);party.CancelMovement();Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);
            Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);Invoke(party,"TrySetTraversalDestination",(Vector3)Center(new Vector2Int(2,2)));
            Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);Assert.That(((ConditionDungeonObjectInstance)model).IsSatisfied,Is.False);
        }
        [TestCase("lever")] [TestCase("condition")] [TestCase("exit")]
        public void WallPreventsRemoteUseAndEnclosureHasNoApproach(string kind)
        {
            Create(kind);var cell=new Vector2Int(8,3);actor.SetPosition(Center(cell+Vector2Int.left));map.TryAddWall(cell+Vector2Int.left,cell);
            Assert.That(pointer.Target.CanInteract(),Is.False);
            foreach(var d in new[]{Vector2Int.right,Vector2Int.down,Vector2Int.up})map.TryAddWall(cell,cell+d);
            Assert.That(party.RequestWorldObjectInteraction(pointer),Is.False);Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);
        }
        [Test] public void HiddenOrSpentObjectClearsItsOutlineAndRejectsClicks()
        {
            Create("condition");pointer.SetInspectionHighlight(true);Invoke(pointer,"LateUpdate");
            var outlines=pointer.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Visual State Outline").ToArray();Assert.That(outlines.Any(r=>r.enabled),Is.True);
            Set(terrain,"<FogFloor>k__BackingField",floor);floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Invoke(pointer,"LateUpdate");Assert.That(outlines.All(r=>!r.enabled),Is.True);Assert.That(pointer.TryPrimaryInteraction(HitPoint()),Is.False);
        }
        [Test] public void LeverOnlyDoorCannotBeOpenedByTheNewClickPath()
        {
            leverOnlyDoor=true;Create("door");Assert.That(pointer.CanRequestInteraction,Is.False);
            Assert.That(pointer.TryPrimaryInteraction(pointer.Position),Is.False);Assert.That(party.RequestWorldObjectInteraction(pointer),Is.False);
            Assert.That(((DoorDungeonObjectInstance)model).IsOpen,Is.False);
        }
        [Test] public void ForeignPartyAndPreviousFloorCannotRetainAnInteraction()
        {
            Create("lever");var foreign=root.AddComponent<ExplorationPartyController>();Assert.That(foreign.RequestWorldObjectInteraction(pointer),Is.False);
            Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);
            var next=new DungeonFloorInstance(1);next.TrySetMap(map);party.ConfigureTraversal(next,terrain,null);
            Assert.That(pointer.CanRequestInteraction,Is.False);Assert.That(pointer.Target.CanInteract(),Is.False);
            Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);
        }
        [Test] public void NpcOrUnitOwnerClaimPreventsASecondWorldObjectAction()
        {
            Create("lever");var inspector=root.AddComponent<CombatUnitInspectController>();Set(party,"unitInteractions",inspector);Set(inspector,"primaryFrame",Time.frameCount);
            Assert.That(party.IsWorldPrimaryClickClaimed,Is.True);Assert.That(pointer.TryPrimaryInteraction(HitPoint()),Is.False);
            Assert.That(Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject"),Is.Null);Assert.That(((LeverDungeonObjectInstance)model).IsActivated,Is.False);
        }
        [Test] public void MerelyHoveringAUnitDoesNotDisableTheEShortcut()
        {
            Create("lever");Assert.That(party.RequestWorldObjectInteraction(pointer),Is.True);actor.SetPosition(Get<Vector2?>(party,"movementDestination").Value);party.CancelMovement();
            var inspector=root.AddComponent<CombatUnitInspectController>();Set(party,"unitInteractions",inspector);Set(inspector,"hoveredView",actor);
            Assert.That(party.IsWorldPrimaryClickClaimed,Is.True);Assert.That(party.IsWorldInteractionClaimedThisFrame,Is.False);
            Assert.That(pointer.TryKeyboardInteraction(),Is.True);Used("lever");
        }
        [Test] public void ExitRequestsConfirmationAndCancelKeepsTheExpedition()
        {
            Create("exit");
            var old=Godmist.Views.Game.GameBootstrap.Instance;
            var node=new GameObject("Confirmation bootstrap");node.transform.SetParent(root.transform);node.SetActive(false);
            var game=node.AddComponent<Godmist.Views.Game.GameBootstrap>();
            Set(game,"<Session>k__BackingField",session);
            var instance=typeof(Godmist.Views.Game.GameBootstrap).GetProperty("Instance",BindingFlags.Static|BindingFlags.Public);
            instance.SetValue(null,game);
            var popup=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemActionPopupView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ItemActionPopup.prefab"));
            try
            {
                Invoke(popup,"Awake");
                var exit=(DungeonExitView)pointer.Target;
                Set(exit,"confirmation",popup);
                // Isolate the exit UI from the independently tested guard rule.
                exit.InitializeTransition(party,actor.Position,null);
                var dungeon=session.ActiveDungeon;
                exit.Interact();
                Assert.That(popup.IsModalOpen,Is.True);
                Assert.That(Get<TMPro.TMP_Text>(popup,"quantityTitle").text,Is.EqualTo("Opuścić loch?"));
                Assert.That(session.ActiveDungeon,Is.SameAs(dungeon));
                Get<UnityEngine.UI.Button>(popup,"cancelButton").onClick.Invoke();
                Assert.That(popup.IsModalOpen,Is.False);Assert.That(session.ActiveDungeon,Is.SameAs(dungeon));
                exit.Interact();Assert.That(popup.IsModalOpen,Is.True);
                Get<UnityEngine.UI.Button>(popup,"confirmButton").onClick.Invoke();
                Assert.That(session.ActiveDungeon,Is.SameAs(dungeon),"Final guard validation must run again on confirmation.");
                Assert.That(Get<TMPro.TMP_Text>(popup,"quantityError").text,Is.Not.Empty);
                popup.HideAll();
            }
            finally { Set((DungeonExitView)pointer.Target,"confirmation",null);Object.DestroyImmediate(popup.gameObject);instance.SetValue(null,old); }
        }
        [Test] public void ForestExitUsesOneNativeGateWithStandingLightingAndStableDepth()
        {
            Create("exit"); var exit = (DungeonExitView)pointer.Target;
            var sprite = Resources.Load<Sprite>("Godmist/ForestArt/Exploration/exit-gate-v2");
            Assert.That(sprite, Is.Not.Null); Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(128,85)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32)); Assert.That(sprite.pivot.x, Is.EqualTo(64).Within(.01f));
            Assert.That(sprite.pivot.y, Is.EqualTo(85*.285f).Within(.01f));
            var bodies = exit.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled).ToArray();
            Assert.That(bodies.Length, Is.EqualTo(1)); Assert.That(bodies[0].sprite, Is.SameAs(sprite));
            Assert.That(bodies[0].transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(bodies[0].gameObject.layer, Is.EqualTo(exit.gameObject.layer));
            Assert.That(exit.GetComponentsInChildren<Collider2D>().Length, Is.EqualTo(1), "Presentation must not block the open passage.");
            var rig = root.AddComponent<Godmist.Views.Lighting.DungeonSilhouetteLighting>();
            var bootstrap = root.AddComponent<DungeonSandboxBootstrap>();
            Set(bootstrap,"floorMapView",terrain); Set(bootstrap,"silhouetteLighting",rig);
            Invoke(bootstrap,"RegisterTransitionLighting",exit); Invoke(bootstrap,"RegisterTransitionLighting",exit);
            Assert.That(exit.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.enabled), Is.EqualTo(1), "Repeated styling cannot duplicate the gate.");
            Assert.That(Get<List<SpriteRenderer>>(rig,"groundContactPresentations"), Is.EqualTo(bodies));
            Assert.That(Get<List<SpriteRenderer>>(rig,"groundPresentations"), Is.Empty, "A standing gate must not be flattened into the stair lane.");
            Assert.That(terrain.IsGroundLightingPresentation(bodies[0]), Is.True);
            Assert.That(terrain.PreservesGroundPresentationSorting(bodies[0]), Is.True);
            var group = bodies[0].GetComponent<SortingGroup>();
            terrain.GetStandingObjectDepthSortingOrder(exit.transform.position.y+.3f);
            Invoke(exit,"LateUpdate");
            Assert.That(group.sortingOrder, Is.EqualTo(terrain.GetStandingObjectDepthSortingOrder(exit.transform.position.y)));
            Assert.That(group.sortingLayerID, Is.EqualTo(terrain.FloorFeedbackSortingLayerID));
        }
        [Test] public void CatacombsExitKeepsNativeStairMarker()
        {
            var exit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab"),root.transform);
            var bodies = exit.GetComponentsInChildren<SpriteRenderer>(true); var sprites = bodies.Select(r=>r.sprite).ToArray();
            terrain.StyleForestTransition(exit);
            Assert.That(exit.UsesStandingPresentation, Is.False);
            Assert.That(exit.GetComponentsInChildren<SpriteRenderer>(true), Is.EqualTo(bodies));
            Assert.That(bodies.Select(r=>r.sprite), Is.EqualTo(sprites));
        }
        [Test] public void ExitHoverIncludesActualGateArtAndRejectsEmptySpace()
        {
            Create("exit");Assert.That(pointer.IsPointerHit(pointer.Position),Is.True);
            var body=pointer.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.enabled&&r.sprite!=null);
            Assert.That(pointer.IsPointerHit(body.bounds.center+Vector3.up*(body.bounds.extents.y*.6f)),Is.True);
            Assert.That(pointer.IsPointerHit(new Vector2(body.bounds.max.x+1,body.bounds.center.y)),Is.False);
            Assert.That(pointer.TryPrimaryInteraction(body.bounds.center),Is.True);
            Assert.That(PartyPendingWorld(),Is.True);
        }
        private bool PartyPendingWorld() => Get<ExplorationWorldObjectInteraction>(party,"pendingWorldObject") == pointer;
        [Test] public void ExitClickAtGateArtUsesInteractionRatherThanGroundMovement()
        {
            Create("exit");
            Assert.That(pointer.TryPrimaryInteraction(pointer.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.enabled&&r.sprite!=null).bounds.center),Is.True);
            Assert.That(PartyPendingWorld(),Is.True);
        }
        [TestCase("exit")] [TestCase("stash")] [TestCase("lever")] [TestCase("condition")] [TestCase("door")]
        [TestCase("combat-door-open")] [TestCase("combat-door-closed")]
        public void GpuHoverOutlinesActualArtWithoutDuplicatingOrChangingTheBody(string kind)
        {
            Create(kind.StartsWith("combat-door")?"door":kind);
            if(kind.StartsWith("combat-door"))SetupCombatDoor(kind=="combat-door-open");
            var originals=pointer.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled).ToArray();var colors=originals.Select(r=>r.color).ToArray();
            foreach(var renderer in pointer.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=30;
            var camera=new GameObject("Hover camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(root.transform);
            camera.orthographic=true;camera.orthographicSize=kind=="exit"?4f:1.5f;camera.aspect=4f/3;camera.cullingMask=1<<30;
            var bounds=originals[0].bounds;foreach(var renderer in originals)bounds.Encapsulate(renderer.bounds);
            camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.065f,.04f);
            var target=new RenderTexture(480,360,24);var pixels=new Texture2D(480,360,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,360),0,0);pixels.Apply();var before=pixels.GetPixels32();
                pointer.SetInspectionHighlight(true);Invoke(pointer,"LateUpdate");
                Assert.That(pointer.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.name=="Visual State Outline"&&r.enabled),Is.True,"Hover must enable native outline renderers.");
                SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,360),0,0);pixels.Apply();var after=pixels.GetPixels32();
                File.WriteAllBytes(Path.Combine(Output,kind+"-hover-debug.png"),pixels.EncodeToPNG());
                Assert.That(before.Where((c,i)=>!c.Equals(after[i])).Count(),Is.GreaterThan(10));
                File.WriteAllBytes(Path.Combine(Output,kind+"-hover.png"),pixels.EncodeToPNG());
                var count=pointer.GetComponentsInChildren<CombatUnitVisualStateView>(true).Length;Invoke(pointer,"LateUpdate");Assert.That(pointer.GetComponentsInChildren<CombatUnitVisualStateView>(true).Length,Is.EqualTo(count));
                Assert.That(originals.Select(r=>r.color),Is.EqualTo(colors));
                pointer.SetInspectionHighlight(false);Assert.That(pointer.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Visual State Outline").All(r=>!r.enabled),Is.True);
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
