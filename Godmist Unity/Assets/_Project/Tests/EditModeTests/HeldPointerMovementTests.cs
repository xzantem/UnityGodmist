using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class HeldPointerMovementTests : InputTestFixture
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root;
        Camera camera;
        RenderTexture viewport;
        ExplorationPartyController party;
        ExplorationCharacterView actor;
        GameSession session;
        Mouse mouse;
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        [SetUp] public override void Setup()
        {
            base.Setup();
            root=new GameObject("Held pointer movement",typeof(Grid));
            camera=new GameObject("Movement projection",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(root.transform);
            camera.orthographic=true;camera.orthographicSize=5;camera.transform.position=new Vector3(5,4,-10);
            viewport=new RenderTexture(1000,600,0);viewport.Create();camera.targetTexture=viewport;camera.aspect=1000f/600;
            session=new GameSession(GameModeState.Exploration);
            var character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            session.TryRegisterCharacter(character);session.TryAddCharacterToActiveParty(character.CharacterId);session.TrySetActiveLeader(character.CharacterId);
            actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab"),root.transform);
            actor.Bind(character);actor.SetPosition(new Vector2(1.5f,3.5f));
            party=root.AddComponent<ExplorationPartyController>();party.Initialize(session,new[]{actor});Set(party,"worldCamera",camera);
            mouse=InputSystem.AddDevice<Mouse>("Hold movement test mouse");
        }
        void Dungeon()
        {
            var tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer)).GetComponent<Tilemap>();tiles.transform.SetParent(root.transform);
            var terrain=root.AddComponent<DungeonFloorMapView>();Set(terrain,"floorTilemap",tiles);
            var map=new DungeonFloorMap();for(int y=0;y<8;y++)for(int x=0;x<12;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
            var floor=new DungeonFloorInstance(0);floor.TrySetMap(map);Set(terrain,"boundMap",map);Get<DungeonElevationViewState>(terrain,"elevation").Bind(map);
            floor.Fog.SetVisible(map.FloorCells.Select(c=>new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
            var dungeon=new DungeonInstance("held.pointer",1);dungeon.TryAddFloor(floor);session.TrySetActiveDungeon(dungeon);
            actor.ConfigureTerrain(map,terrain);actor.SetPosition(tiles.GetCellCenterWorld(new Vector3Int(1,3,0)));
            party.ConfigureTraversal(floor,terrain,null);
        }
        void Send(Vector2 world,bool held=true)
        {
            var state=new MouseState{position=camera.WorldToScreenPoint(world)};if(held)state=state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse,state);InputSystem.Update();
        }
        void Begin(Vector2 goal)
        {
            Send(goal);Assert.That((bool)Call(party,"TryMoveLeaderToWorldPosition",goal),Is.True);
            Call(party,"BeginPointerMovementGesture",goal,0f);
            Assert.That(Get<Mouse>(party,"pointerMovementMouse"),Is.SameAs(mouse));
        }
        [TestCase(false)] [TestCase(true)]
        public void HoldRetargetsAndReleaseKeepsTheLastAcceptedRoute(bool dungeon)
        {
            if(dungeon)Dungeon();var first=new Vector2(4.5f,3.5f);var last=new Vector2(7.5f,3.5f);Begin(first);
            Send(last);Call(party,"TickPointerMovementGesture",.19f);Assert.That(Get<Vector2?>(party,"movementDestination"),Is.EqualTo(first));
            Call(party,"TickPointerMovementGesture",.21f);Assert.That(Vector2.Distance(Get<Vector2?>(party,"movementDestination").Value,last),Is.LessThan(.001f));
            Send(new Vector2(9.5f,3.5f),false);Call(party,"TickPointerMovementGesture",.4f);
            Assert.That(Get<Mouse>(party,"pointerMovementMouse"),Is.Null);Assert.That(Vector2.Distance(Get<Vector2?>(party,"movementDestination").Value,last),Is.LessThan(.001f));
        }
        [Test] public void FollowingCameraRetargetsWithAStationaryScreenCursor()
        {
            Begin(new Vector2(4.5f,3.5f));camera.transform.position+=Vector3.right;
            Call(party,"TickPointerMovementGesture",.21f);
            Assert.That(Get<Vector2?>(party,"movementDestination").Value.x,Is.EqualTo(5.5f).Within(.001));
        }
        [Test] public void RetargetingIsThrottledAndIgnoresPointerJitter()
        {
            var first=new Vector2(4.5f,3.5f);Begin(first);Send(first+Vector2.right*.05f);Call(party,"TickPointerMovementGesture",.3f);
            Assert.That(Get<Vector2?>(party,"movementDestination"),Is.EqualTo(first));
            Send(new Vector2(6.5f,3.5f));Call(party,"TickPointerMovementGesture",.31f);
            Send(new Vector2(8.5f,3.5f));Call(party,"TickPointerMovementGesture",.35f);
            Assert.That(Get<Vector2?>(party,"movementDestination").Value.x,Is.EqualTo(6.5f).Within(.001));
            Call(party,"TickPointerMovementGesture",.42f);Assert.That(Get<Vector2?>(party,"movementDestination").Value.x,Is.EqualTo(8.5f).Within(.001));
        }
        [Test] public void UnreachableDungeonTargetPreservesThePreviousValidDestination()
        {
            Dungeon();var first=new Vector2(4.5f,3.5f);Begin(first);Send(new Vector2(12.5f,3.5f));Call(party,"TickPointerMovementGesture",.3f);
            Assert.That(Get<Vector2?>(party,"movementDestination"),Is.EqualTo(first));
            Send(new Vector2(7.5f,3.5f));Call(party,"TickPointerMovementGesture",.5f);
            Assert.That(Get<Vector2?>(party,"movementDestination").Value.x,Is.EqualTo(7.5f).Within(.001));
        }
        [Test] public void ClaimedInteractionDisarmsUntilAnotherPressWithoutReplacingItsRoute()
        {
            Begin(new Vector2(4.5f,3.5f));party.ClaimWorldPrimaryClick();Send(new Vector2(8.5f,3.5f));Call(party,"TickPointerMovementGesture",.3f);
            Assert.That(Get<Mouse>(party,"pointerMovementMouse"),Is.Null);Assert.That(Get<Vector2?>(party,"movementDestination"),Is.EqualTo(new Vector2(4.5f,3.5f)));
        }
        [TestCase(GameModeState.Dialogue)] [TestCase(GameModeState.Combat)]
        public void LeavingExplorationDisarmsWithoutResumingOnReturn(GameModeState mode)
        {
            Begin(new Vector2(4.5f,3.5f));session.TrySetMode(mode);Send(new Vector2(8.5f,3.5f));Call(party,"TickPointerMovementGesture",.3f);
            Assert.That(Get<Mouse>(party,"pointerMovementMouse"),Is.Null);session.TrySetMode(GameModeState.Exploration);Call(party,"TickPointerMovementGesture",.5f);
            Assert.That(Get<Vector2?>(party,"movementDestination"),Is.EqualTo(new Vector2(4.5f,3.5f)));
        }
        [Test] public void ExplicitCancellationAndFocusLossEndTheGesture()
        {
            Begin(new Vector2(4.5f,3.5f));party.CancelMovement();Assert.That(Get<Mouse>(party,"pointerMovementMouse"),Is.Null);Assert.That(Get<Vector2?>(party,"movementDestination"),Is.Null);
            Begin(new Vector2(4.5f,3.5f));Call(party,"OnApplicationFocus",false);Assert.That(Get<Mouse>(party,"pointerMovementMouse"),Is.Null);
        }
        [Test] public void RetargetingPreservesFollowerRouteCaches()
        {
            Begin(new Vector2(4.5f,3.5f));
            var field=typeof(ExplorationPartyController).GetField("cityFollowerRoutes",Flags);
            var routes=(System.Collections.IDictionary)field.GetValue(party);
            var cached=Activator.CreateInstance(field.FieldType.GetGenericArguments()[1],true);
            routes.Add("following.companion",cached);
            Send(new Vector2(7.5f,3.5f));Call(party,"TickPointerMovementGesture",.3f);
            Assert.That(routes["following.companion"],Is.SameAs(cached),"Steering must not discard independent follower routes every 0.1 seconds.");
            party.CancelMovement();Assert.That(routes.Count,Is.Zero,"Explicit cancellation still clears the whole group's routes.");
        }
        [TearDown] public override void TearDown()
        {
            try {camera.targetTexture=null;Object.DestroyImmediate(root);viewport.Release();Object.DestroyImmediate(viewport);Physics2D.SyncTransforms();}
            finally {base.TearDown();}
        }
    }
}
