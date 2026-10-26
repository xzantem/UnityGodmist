using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class EncounterBalanceTests
    {
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
        string[] legacyBefore;
        readonly List<UnityEngine.Object> temporary=new();
        [OneTimeSetUp] public void Author()
        {
            legacyBefore=Catalog.EncounterDefinitions.Where(e=>e!=null && EncounterBalanceSetup.TryGetRoster(e.Id,out _)
                && !e.Id.EndsWith(EncounterBalanceSetup.VersionSuffix)).OrderBy(e=>e.Id).Select(e=>EditorJsonUtility.ToJson(e)).ToArray();
            EncounterBalanceSetup.ApplyAll();
        }
        [TearDown] public void Cleanup(){foreach(var value in temporary)UnityEngine.Object.DestroyImmediate(value);temporary.Clear();}

        [Test] public void AllForestPoolsHaveEasyMediumAndHardGroupsWithoutMutatingLegacyDefinitions()
        {
            var pools=Catalog.DungeonDefinitions.Where(d=>d!=null && d.Theme==DungeonTheme.Forest && d.IsAvailableAsOffer).ToArray();
            Assert.That(pools.Length,Is.EqualTo(3));
            foreach(var dungeon in pools)
            {
                Assert.That(dungeon.EncounterDefinitions.Count,Is.EqualTo(7));
                Assert.That(dungeon.EncounterDefinitions.All(e=>e.Id.EndsWith(EncounterBalanceSetup.VersionSuffix)),Is.True);
                Assert.That(dungeon.EncounterDefinitions.Select(e=>e.Enemies.Count).OrderBy(n=>n),Is.EqualTo(new[]{2,2,3,3,3,3,4}));
                Assert.That(dungeon.EncounterDefinitions.Select(e=>e.Id).Distinct().Count(),Is.EqualTo(7));
            }
            Assert.That(Catalog.EncounterDefinitions.Where(e=>e!=null && EncounterBalanceSetup.TryGetRoster(e.Id,out _)
                && !e.Id.EndsWith(EncounterBalanceSetup.VersionSuffix)).OrderBy(e=>e.Id).Select(e=>EditorJsonUtility.ToJson(e)).ToArray(),Is.EqualTo(legacyBefore));
        }

        [Test] public void ReauthoringIsIdempotentAndKeepsBothCatalogGenerations()
        {
            var definitions=Catalog.DungeonDefinitions.Where(d=>d!=null && d.Theme==DungeonTheme.Forest).ToArray();
            var before=definitions.Select(e=>EditorJsonUtility.ToJson(e)).ToArray();
            var encounters=Catalog.EncounterDefinitions.Where(e=>e!=null && e.Id.StartsWith("encounter.forest.")).ToArray();
            var content=encounters.Select(e=>EditorJsonUtility.ToJson(e)).ToArray();
            EncounterBalanceSetup.ApplyAll();
            Assert.That(definitions.Select(e=>EditorJsonUtility.ToJson(e)).ToArray(),Is.EqualTo(before));
            Assert.That(encounters.Select(e=>EditorJsonUtility.ToJson(e)).ToArray(),Is.EqualTo(content));
            Assert.That(Catalog.EncounterDefinitions.Where(e=>e!=null).GroupBy(e=>e.Id).Any(g=>g.Count()>1),Is.False);
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.forest.boss_expedition")]
        [TestCase("dungeon.forest.bandit_leader_expedition")]
        public void ActualGeneratedGroupsHaveAllActorsOnDistinctLegalCellsAndRoundTrip(string id)
        {
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id==id);
            var dungeon=new DungeonGeneratorService().Generate(definition,171026,10,DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            new DungeonObjectPlacementSystem().Place(dungeon,Catalog.EquipmentPartDefinitions);
            foreach(var floor in dungeon.Floors)
            foreach(var encounter in floor.Encounters)
            {
                // Boss rooms retain their existing dedicated spawn resolver; this
                // pass adds semantic slots only for random regular encounters.
                if(encounter.Definition==definition.BossEncounterDefinition)continue;
                var positions=new DungeonEnemyAnchorResolver().Resolve(floor,definition.RoomTemplateCatalog,encounter,false);
                Assert.That(positions.Count,Is.EqualTo(encounter.Enemies.Count),encounter.Definition.Id);
                Assert.That(positions.Values.Distinct().Count(),Is.EqualTo(positions.Count));
                foreach(var cell in positions.Values)Assert.That(floor.Map.IsWalkable(cell),Is.True);
                foreach(var obstacle in floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.IsActive && o.BlocksMovement))
                    Assert.That(obstacle.EnumerateOccupiedCells().Any(c=>positions.Values.Contains(c)),Is.False,"Enemy spawn overlaps a blocking object.");
            }
            var game=Party();game.TrySetActiveDungeon(dungeon);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"balanced-encounters");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),Catalog,out var restored),Is.True);
            Assert.That(restored.ActiveDungeon.Floors.SelectMany(f=>f.Encounters).Select(e=>e.Enemies.Count),
                Is.EqualTo(dungeon.Floors.SelectMany(f=>f.Encounters).Select(e=>e.Enemies.Count)));
        }

        [Test] public void OldActiveForestRestoresOriginalRosterThroughGlobalCatalog()
        {
            var current=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.forest.expedition");
            var old=UnityEngine.Object.Instantiate(current);temporary.Add(old);
            old.EncounterDefinitions.Clear();old.EncounterDefinitions.AddRange(Catalog.EncounterDefinitions.Where(e=>e!=null
                && EncounterBalanceSetup.TryGetRoster(e.Id,out _) && !e.Id.EndsWith(EncounterBalanceSetup.VersionSuffix)));
            var dungeon=new DungeonGeneratorService().Generate(old,171027,1,DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var game=Party();game.TrySetActiveDungeon(dungeon);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"old-forest");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),Catalog,out var restored),Is.True);
            Assert.That(restored.ActiveDungeon.Floors.SelectMany(f=>f.Encounters).All(e=>e.Enemies.Count==2
                && !e.Definition.Id.EndsWith(EncounterBalanceSetup.VersionSuffix)),Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void ScoutingRespectsMandatoryRoomsAndLockedGateBranches(bool gate)
        {
            var floor=new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("start",DungeonRoomType.Start));
            floor.TryAddRoom(new DungeonRoomInstance("middle",DungeonRoomType.Normal));
            floor.TryAddRoom(new DungeonRoomInstance("final",DungeonRoomType.Final));
            floor.TryAddConnection("start","middle");floor.TryAddConnection("middle","final");
            if(gate)
            {
                floor.TryAddRoom(new DungeonRoomInstance("key",DungeonRoomType.Optional));floor.TryAddConnection("middle","key");
                floor.Connections.Single(c=>c.Connects("middle","final")).TrySetProgressionGate("gate","condition","key");
            }
            var rooms=EncounterScoutingRoute.SelectRooms(floor);
            Assert.That(rooms,Does.Contain("start"));Assert.That(rooms,Does.Contain("middle"));Assert.That(rooms,Does.Contain("final"));
            if(gate)
            {
                Assert.That(rooms,Does.Contain("key"));
                Assert.That(floor.Connections.Single(c=>c.HasProgressionGate).IsProgressionGateUnlocked,Is.False,"Simulation must not unlock live content.");
            }
        }
        [Serializable] sealed class RouteRow {public string dungeon;public int length,seed,full,scouted;}
        [Serializable] sealed class RouteReport {public List<RouteRow> rows=new();}
        [TestCase("dungeon.catacombs.expedition")]
        [TestCase("dungeon.forest.expedition")]
        public void GeneratedScoutingUsesConnectedRoomsAndReportsActualAvoidance(string id)
        {
            var report=new RouteReport();
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id==id);
            foreach(var length in new[]{DungeonLength.Short,DungeonLength.Normal,DungeonLength.Long})
            for(var seed=0;seed<2;seed++)
            {
                var dungeon=new DungeonGeneratorService().Generate(definition,71016+seed,1,length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                var scouted=0;
                foreach(var floor in dungeon.Floors)
                {
                    var rooms=EncounterScoutingRoute.SelectRooms(floor);
                    Assert.That(rooms,Does.Contain(floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Start).RoomId));
                    Assert.That(rooms,Does.Contain(floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Final).RoomId));
                    var reachable=new HashSet<string>{rooms[0]};var pending=new Queue<string>();pending.Enqueue(rooms[0]);
                    while(pending.Count>0)
                    {
                        var current=pending.Dequeue();
                        foreach(var edge in floor.Connections.Where(c=>c.FirstRoomId==current || c.SecondRoomId==current))
                        {
                            var next=edge.FirstRoomId==current?edge.SecondRoomId:edge.FirstRoomId;
                            if(rooms.Contains(next) && reachable.Add(next))pending.Enqueue(next);
                        }
                    }
                    Assert.That(reachable.Count,Is.EqualTo(rooms.Count));
                    scouted+=floor.Encounters.Count(e=>rooms.Contains(e.RoomId));
                }
                var full=dungeon.Floors.Sum(f=>f.Encounters.Count);
                Assert.That(scouted,Is.InRange(1,full));
                report.rows.Add(new RouteRow{dungeon=id,length=(int)length,seed=seed,full=full,scouted=scouted});
            }
            var folder="../LocalHandoffs/encounter-balance-pass-20261016";Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"scouting-"+id+".json"),JsonUtility.ToJson(report,true));
        }
        static GameSession Party()
        {
            var game=new GameSession(GameModeState.Exploration);var hero=EnemyScalingBalanceTests.Hero(0,1,false,true);
            game.TryRegisterCharacter(hero);game.TryAddCharacterToActiveParty(hero.CharacterId);game.TrySetActiveLeader(hero.CharacterId);return game;
        }
    }
}
