using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Runtime.Combat;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Data.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonPatrolTrapLightingPassTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/patrol-traps-lighting-pass-20261017"));

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void MeasureProductionLightingAndPreserveShadowGeometry(string id)
        {
            var stage = File.ReadAllText(Path.Combine(Output, "stage.txt")).Trim();
            var metrics = new List<Metric>();
            var captures = new List<Capture>();
            var probe = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "lighting-" + stage };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var camera = Read<Camera>(rig, "worldCamera");
                camera.aspect = 16f / 9f;
                var floor = session.ActiveDungeon.Floors[0];
                new DungeonFogSystem().Update(floor, party.ActiveMemberViews.Select(v => v.DungeonPosition).ToArray());
                map.ApplyFog(floor);
                var leader = party.ActiveLeaderTransform.position;
                camera.transform.position = new Vector3(leader.x, leader.y, -10);
                var refresh = typeof(DungeonSilhouetteLighting).GetMethod("LateUpdate", Private);
                void Refresh() { typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame", Private).SetValue(rig, int.MinValue); refresh.Invoke(rig, null); }
                void Measure(string name)
                {
                    Refresh();
                    var samples = new double[15];
                    for (var i = 0; i < samples.Length; i++) { var watch = Stopwatch.StartNew(); Refresh(); samples[i] = watch.Elapsed.TotalMilliseconds; }
                    Array.Sort(samples); metrics.Add(new Metric { name = name, medianMs = samples[7], maxMs = samples[14] });
                    File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-timings.json"), JsonUtility.ToJson(new Metrics { values = metrics.ToArray() }, true));
                }
                void Snapshot(string name)
                {
                    Refresh();
                    if (stage == "after") CheckCoverage(map, Read<Rect>(rig, "cameraGroundBounds"), id.Contains("forest"));
                    var contact = Read<object>(rig, "contactShadows");
                    var mesh = Read<Mesh>(contact, "mesh");
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    foreach (var v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    foreach (var c in mesh.colors32) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
                    foreach (var i in mesh.triangles) writer.Write(i);
                    captures.Add(new Capture { name = name, contact = Hash(stream.ToArray()), mask = MaskHash(rig.ShadowMask, Math.Max(1, Read<System.Collections.ICollection>(rig, "activeTorches").Count)), casters = rig.LastCasterCount });
                }
                Measure("normal fog"); Snapshot("normal fog");
                floor.Fog.ToggleDebugFog(); map.ApplyFog(floor);
                Measure("revealed map"); Snapshot("revealed map");
                var rooms = floor.Rooms.Where(r => r != null).ToArray();
                foreach (var index in new[] { rooms.Length / 3, rooms.Length * 2 / 3 })
                {
                    var position = map.CellToWorldCenter(rooms[index].LayoutPosition + Vector2Int.one * 2);
                    camera.transform.position = new Vector3(position.x, position.y, -10);
                    Snapshot("pan " + index);
                }
                camera.orthographicSize *= 1.5f; Snapshot("zoom");
                var coordinator = Read<DungeonCombatCoordinator>(bootstrap, "combatCoordinator");
                void Invoke(string method) => coordinator.GetType().GetMethod(method, Private).Invoke(coordinator, null);
                void MeasureStage(string name, Action action)
                {
                    action(); var samples = new double[15];
                    for (var i = 0; i < samples.Length; i++) { var timer = Stopwatch.StartNew(); action(); samples[i] = timer.Elapsed.TotalMilliseconds; }
                    Array.Sort(samples); metrics.Add(new Metric { name = name, medianMs = samples[7], maxMs = samples[14] });
                }
                File.WriteAllText(Path.Combine(Output,stage+"-"+id+"-clock.txt"),"Editor deltaTime="+Time.deltaTime+"; probes use explicit 1/60 s traps and .02 s patrols");
                MeasureStage("exploration traps fixed 1/60s", () => coordinator.GetType().GetMethod("AdvanceExplorationTrapsForElapsed",Private).Invoke(coordinator,new object[]{1f/60f}));
                MeasureStage("patrol full sense+move tick", () => {
                    typeof(DungeonCombatCoordinator).GetField("patrolElapsed", Private).SetValue(coordinator, 0f);
                    typeof(DungeonCombatCoordinator).GetField("nextPatrolSense", Private).SetValue(coordinator, 0f);
                    coordinator.GetType().GetMethod("UpdatePatrolsForElapsed",Private).Invoke(coordinator,new object[]{.02f});
                });
                File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-timings.json"), JsonUtility.ToJson(new Metrics { values = metrics.ToArray() }, true));
            };
            probe.MeasureProductionFloorEntry(id, DungeonLength.Long);
            var json = JsonUtility.ToJson(new Captures { values = captures.ToArray() }, true);
            File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-shadows.json"), json);
            if (stage == "after") Assert.That(json, Is.EqualTo(File.ReadAllText(Path.Combine(Output, "before-" + id + "-shadows.json"))), "Contact geometry and projected shadow pixels must remain identical.");
        }
        static void CheckCoverage(DungeonFloorMapView map, Rect viewport, bool forest)
        {
            static bool Intersects(SpriteRenderer r, Rect rect) => r != null && r.bounds.max.x >= rect.xMin
                && r.bounds.min.x <= rect.xMax && r.bounds.max.y >= rect.yMin && r.bounds.min.y <= rect.yMax;
            var walls = new List<SpriteRenderer>(); var ground = new List<SpriteRenderer>();
            map.CollectLightingWallRenderers(viewport, walls); map.CollectLightingGroundRenderers(viewport, ground);
            foreach (var renderer in map.LightingWallRenderers.Where(r => Intersects(r, viewport)))
                Assert.That(walls, Does.Contain(renderer), "Broad phase must retain every potentially lit wall, even while hidden.");
            foreach (var renderer in map.LightingPresentationRenderers.Where(r => Intersects(r, viewport)))
                Assert.That(ground, Does.Contain(renderer), "Broad phase must retain every potentially lit underlay/prop.");
            if (forest)
            {
                var contacts = new List<KeyValuePair<SpriteRenderer, Vector2>>();
                map.CollectForestPropContacts(viewport, contacts);
                var expected = map.ForestPropContacts.Where(p => Intersects(p.Key, viewport)).ToArray();
                Assert.That(contacts.Where(p => Intersects(p.Key, viewport)), Is.EqualTo(expected), "Local contact candidates preserve visible sources and their precedence.");
                Assert.That(contacts.Count, Is.LessThan(map.ForestPropContacts.Count() / 2));
                Assert.That(walls.Count, Is.LessThan(map.LightingWallRenderers.Count() / 2));
                Assert.That(ground.Count, Is.LessThan(map.LightingPresentationRenderers.Count() / 2));
            }
        }

        [Test]
        public void ContactCoverageRebindsAfterOwnerMoveAppendAndDestruction()
        {
            var root = new GameObject("Contact coverage");
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 1);
            try
            {
                var map = root.AddComponent<DungeonFloorMapView>();
                var sources = Read<Dictionary<SpriteRenderer, Vector2>>(map, "forestPropContacts");
                SpriteRenderer Add(Vector2 p)
                {
                    var go = new GameObject("prop"); go.transform.SetParent(root.transform); go.transform.position = p;
                    var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; sources.Add(r, p); return r;
                }
                var first = Add(Vector2.zero); Add(new Vector2(100, 100));
                var result = new List<KeyValuePair<SpriteRenderer, Vector2>>();
                var area = new Rect(-1, -1, 3, 3);
                map.CollectForestPropContacts(area, result); Assert.That(result.Select(p => p.Key), Is.EqualTo(new[] {first}));
                var second = Add(Vector2.one);
                map.CollectForestPropContacts(area, result); Assert.That(result.Select(p => p.Key), Is.EqualTo(new[] {first,second}));
                root.transform.position += Vector3.right * 200;
                map.CollectForestPropContacts(area, result); Assert.That(result, Is.Empty);
                area.position += Vector2.right * 200;
                map.CollectForestPropContacts(area, result); Assert.That(result.Select(p => p.Key), Is.EqualTo(new[] {first,second}));
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                map.CollectForestPropContacts(area, result); Assert.That(result.Select(p => p.Key), Is.EqualTo(new[] {second}));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PatrolStepMatchesNeighboursAfterWallDoorGateAndStairChanges()
        {
            var root = new GameObject("Patrol adjacency");
            try
            {
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap(); floor.TrySetMap(map);
                for (var x = 0; x < 4; x++) for (var y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(x,y), "r");
                var upper = new DungeonSurfacePosition(new Vector2Int(2,0), 1); map.TryAddFloorCell(upper,"upper");
                Assert.That(map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                    new[] {Vector2Int.zero, Vector2Int.right, new Vector2Int(2,0)}), Is.True);
                var view = root.AddComponent<DungeonFloorMapView>();
                var party = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); party.ConfigureTraversal(floor, view, null);
                var coordinator = root.AddComponent<DungeonCombatCoordinator>();
                typeof(DungeonCombatCoordinator).GetField("floor",Private).SetValue(coordinator,floor);
                typeof(DungeonCombatCoordinator).GetField("party",Private).SetValue(coordinator,party);
                var step = typeof(DungeonCombatCoordinator).GetMethod("IsPatrolStepOpen",Private);
                void Compare()
                {
                    var positions = map.Surfaces.Select(p => new DungeonTraversalPosition(p)).ToList();
                    positions.Add(new DungeonTraversalPosition(map.Stairs.First(),1));
                    foreach (var from in positions) foreach (var to in positions)
                        Assert.That((bool)step.Invoke(coordinator,new object[]{from,to}),
                            Is.EqualTo(map.GetTraversalNeighbours(from,party.CanOpenTraversalDoor).Contains(to)), from+" -> "+to);
                }
                Compare();
                var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.up);
                map.TrySetDoorState(edge,false); Compare(); map.TrySetDoorState(edge,true); Compare();
                map.TryAddWall(new Vector2Int(1,1),new Vector2Int(1,2)); Compare();
                var gate = new DungeonSurfaceEdge(new DungeonSurfacePosition(new Vector2Int(2,2)), new DungeonSurfacePosition(new Vector2Int(2,3)));
                map.TrySetProgressionGateState(gate,false); Compare(); map.TrySetProgressionGateState(gate,true); Compare();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TrapIndexKeepsLiveRestoreStateAndFindsAppendedTrapsWithoutAllocatingEmptyCellQueries()
        {
            var definition = ScriptableObject.CreateInstance<SpikeTrapDungeonObjectDefinition>();
            try
            {
                typeof(SpikeTrapDungeonObjectDefinition).GetField("<StatusId>k__BackingField",Private).SetValue(definition, "");
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap(); floor.TrySetMap(map);
                for(var x=0;x<501;x++) map.TryAddFloorCell(new Vector2Int(x,0),"r");
                var system = new DungeonTrapSystem(floor,1);
                var occupants = Array.Empty<DungeonTrapOccupant>();
                system.EnterCell(new Vector2Int(-1,0),occupants);
                for (var x=0;x<500;x++) Assert.That(floor.TryAddObject(new SpikeTrapDungeonObjectInstance("trap"+x,definition,new Vector2Int(x,0))),Is.True);
                system.EnterCell(new Vector2Int(-1,0),occupants);
                var before = GC.GetAllocatedBytesForCurrentThread();
                for(var i=0;i<10000;i++) system.EnterCell(new Vector2Int(-1,0),occupants);
                Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
                var trap = (SpikeTrapDungeonObjectInstance)floor.Objects[499];
                var unit = new BattleUnit {Character = new CharacterInstance { CharacterId="victim", CurrentHealth=10000,LifeState=CharacterLifeState.Alive,
                    Stats=new CharacterStats {PhysicalDefense=new Stat(0),MagicalDefense=new Stat(0)},ShieldPool=new ShieldPool()}};
                occupants=new[]{new DungeonTrapOccupant(unit,null,trap.Position)};
                Assert.That(system.EnterCell(trap.Position,occupants),Is.True);
                Assert.That(system.EnterCell(trap.Position,occupants),Is.False);
                trap.TryRestoreTriggered(false); trap.TryDeactivate(); Assert.That(system.EnterCell(trap.Position,occupants),Is.False);
                trap.TryRestoreIsActive(true); Assert.That(system.EnterCell(trap.Position,occupants),Is.True);
                var appended=new SpikeTrapDungeonObjectInstance("new",definition,new Vector2Int(500,0));floor.TryAddObject(appended);
                Assert.That(system.EnterCell(appended.Position,new[]{new DungeonTrapOccupant(unit,null,appended.Position)}),Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void PatrolIdentityDoesNotAllocatePerTickAndRebindsForEnemyAndEncounterChanges()
        {
            var root = new GameObject("Patrol identity");
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Combat.EncounterDefinition>();
            try
            {
                var owner=root.AddComponent<DungeonCombatCoordinator>();
                var view=root.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();view.SetEnemyInstanceId("one");
                var trigger=root.AddComponent<EncounterTriggerView>();
                var field=typeof(EncounterTriggerView).GetField("encounterInstance",Private);
                field.SetValue(trigger,new DungeonEncounterInstance("encounter",definition,"room",new RectInt(0,0,2,2)));
                var method=typeof(DungeonCombatCoordinator).GetMethod("PatrolKey",Private);
                var get=(Func<EncounterTriggerView,Godmist.Views.Exploration.ExplorationCharacterView,string>)Delegate.CreateDelegate(
                    typeof(Func<EncounterTriggerView,Godmist.Views.Exploration.ExplorationCharacterView,string>),owner,method);
                var first=get(trigger,view);
                Assert.That(first,Is.EqualTo(DungeonCombatMembershipSystem.EnemyKey("encounter","one")));
                var before=GC.GetAllocatedBytesForCurrentThread();
                for(var i=0;i<10000;i++) get(trigger,view);
                Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
                view.SetEnemyInstanceId("two");Assert.That(get(trigger,view),Is.EqualTo(DungeonCombatMembershipSystem.EnemyKey("encounter","two")));
                field.SetValue(trigger,new DungeonEncounterInstance("new",definition,"room",new RectInt(0,0,2,2)));
                Assert.That(get(trigger,view),Is.EqualTo(DungeonCombatMembershipSystem.EnemyKey("new","two")));
            }
            finally { UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(definition); }
        }

        static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        static string MaskHash(RenderTexture mask, int slices)
        {
            var active = RenderTexture.active;
            var image = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false, true);
            try
            {
                using var stream = new MemoryStream();
                for (var slice = 0; slice < slices; slice++)
                {
                    Graphics.SetRenderTarget(mask, 0, CubemapFace.Unknown, slice);
                    image.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0); image.Apply();
                    var pixels = image.GetRawTextureData<byte>().ToArray(); stream.Write(pixels, 0, pixels.Length);
                }
                return Hash(stream.ToArray());
            }
            finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(image); }
        }
        [Serializable] sealed class Metric { public string name; public double medianMs, maxMs; }
        [Serializable] sealed class Metrics { public Metric[] values; }
        [Serializable] sealed class Capture { public string name, contact, mask; public int casters; }
        [Serializable] sealed class Captures { public Capture[] values; }
    }
}
