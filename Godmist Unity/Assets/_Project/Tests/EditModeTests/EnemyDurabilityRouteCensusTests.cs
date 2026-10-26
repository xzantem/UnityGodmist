using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EnemyDurabilityRouteCensusTests
    {
        [Serializable] sealed class EncounterRow {public string id;public string[] enemies;public int count;}
        [Serializable] sealed class RouteRow
        {
            public string biome;public int level,length,seed,floors,rooms;
            public List<EncounterRow> encounters=new();
        }
        [Serializable] sealed class Report
        {
            public string method="Current generator and placement only. Full generated floors; no combat, AI, turns, or asset writes.";
            public List<RouteRow> routes=new();
        }
        [TestCase(1),TestCase(10),TestCase(20),TestCase(50)]
        [Explicit("Read-only census of current generated encounters for before/after durability weighting.")]
        [Timeout(600000)]
        public void ExportCurrentEncounterCensus(int level)
        {
            var folder=Path.GetFullPath("../LocalHandoffs/enemy-durability-before-after-20261025");Directory.CreateDirectory(folder);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definitions=catalog.DungeonDefinitions.Where(d=>d!=null && (d.Id=="dungeon.forest.expedition" || d.Id=="dungeon.catacombs.expedition")).ToArray();
            var immutable=definitions.ToDictionary(d=>d,EditorJsonUtility.ToJson);
            var enemies=catalog.CharacterDefinitions.Where(d=>d!=null).ToDictionary(d=>d,EditorJsonUtility.ToJson);
            var report=new Report();
            foreach(var definition in definitions)foreach(var length in new[]{1,2,3})foreach(var seed in Enumerable.Range(71016,6))
            {
                var dungeon=new DungeonGeneratorService().Generate(definition,seed,level,(DungeonLength)length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                var row=new RouteRow{biome=definition.Id.Split('.')[1],level=level,length=length,seed=seed,
                    floors=dungeon.Floors.Count,rooms=dungeon.Floors.Sum(f=>f.Rooms.Count)};
                foreach(var group in dungeon.Floors.SelectMany(f=>f.Encounters).GroupBy(e=>e.Definition.Id))
                    row.encounters.Add(new EncounterRow{id=group.Key,count=group.Count(),enemies=group.First().Definition.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray()});
                Assert.That(row.encounters,Is.Not.Empty);
                Assert.That(row.encounters.All(e=>e.enemies.Length>0),Is.True);
                report.routes.Add(row);
            }
            foreach(var pair in immutable)Assert.That(EditorJsonUtility.ToJson(pair.Key),Is.EqualTo(pair.Value));
            foreach(var pair in enemies)Assert.That(EditorJsonUtility.ToJson(pair.Key),Is.EqualTo(pair.Value));
            Assert.That(report.routes,Has.Count.EqualTo(36));
            File.WriteAllText(Path.Combine(folder,"routes-level-"+level+".json"),JsonUtility.ToJson(report,true));
        }
    }
}
