using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonSpatialDensityTests
    {
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/spatial-density-20261023"));

        [Test, Explicit("One-off capture before editing geometry; never overwrites the baseline.")]
        public void CaptureBaseline()
        {
            Export("before");
        }

        [Test, Explicit("One-off paired density audit, including saved physical maps for plotting.")]
        public void CaptureCompactLayouts()
        {
            Export("after");
        }

        [Test, Explicit("Paired capture for the organic forest authoring pass.")]
        public void CaptureOrganicLayouts()
        {
            Export("after", Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/forest-organic-20261023")));
        }

        [Test, Explicit("Paired capture for off-centre Catacombs entrances.")]
        public void CaptureCatacombsEntrances()
        {
            Export("after", Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/catacombs-ports-20261023")));
        }

        [TestCase("forest", DungeonLength.Short)]
        [TestCase("forest", DungeonLength.Normal)]
        [TestCase("forest", DungeonLength.Long)]
        [TestCase("catacombs", DungeonLength.Short)]
        [TestCase("catacombs", DungeonLength.Normal)]
        [TestCase("catacombs", DungeonLength.Long)]
        public void ConnectorsVaryAndCanBeOmitted(string theme, DungeonLength length)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + theme + ".expedition");
            var areas = new HashSet<int>();
            var direct = 0;
            var cells = 0;
            var rooms = 0;
            var connections = 0;
            var emptySteps = 0;
            for (var seed = 231023; seed < 231027; seed++)
            {
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                foreach (var floor in dungeon.Floors)
                {
                    cells += floor.Map.FloorCells.Count();
                    rooms += floor.Rooms.Count;
                    foreach (var connection in floor.Connections)
                    {
                        connections++;
                        var span = connection.GetEntranceCell(connection.SecondRoomId) - connection.FirstDoorCell;
                        Assert.That(Mathf.Abs(span.x) + Mathf.Abs(span.y) - 1, Is.LessThanOrEqualTo(16),
                            "A compact average must not conceal a very long empty connector.");
                        emptySteps += Mathf.Abs(span.x) + Mathf.Abs(span.y) - 1;
                        areas.Add(connection.CorridorCells.Count);
                        if (connection.CorridorCells.Count == 0)
                        {
                            direct++;
                            Assert.That(Vector2Int.Distance(connection.FirstDoorCell, connection.SecondDoorCell), Is.EqualTo(1));
                            Assert.That(connection.EndDoorCell, Is.EqualTo(connection.SecondDoorCell));
                        }
                        var normal = connection.SecondDoorCell - connection.FirstDoorCell;
                        var lateral = normal.x != 0 ? Vector2Int.up : Vector2Int.right;
                        var end = connection.GetEntranceCell(connection.SecondRoomId);
                        for (var i = 0; i < connection.DoorwayWidth; i++)
                        {
                            Assert.That(floor.Map.CanMoveBetween(connection.FirstDoorCell + lateral * i,
                                connection.SecondDoorCell + lateral * i, true), Is.True);
                            Assert.That(floor.Map.CanMoveBetween(end - normal + lateral * i, end + lateral * i, true), Is.True);
                        }
                    }
                }
            }
            Assert.That(direct, Is.GreaterThan(0), "Zero-length links must actually occur in generated maps.");
            Assert.That(areas.Count, Is.GreaterThanOrEqualTo(3), "Connector geometry must vary, not use a fixed gap.");
            Assert.That((double)emptySteps / connections, Is.LessThanOrEqualTo(4.5),
                "Varying links must remain compact rather than reintroducing long empty walks.");
            Assert.That((double)cells / rooms, Is.LessThanOrEqualTo(theme == "forest" ? 165 : 180),
                "Room-body growth must not silently dilute the unchanged encounter budget.");
        }

        private static void Export(string stage, string output = null)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var directory = Path.Combine(output ?? Output, stage);
            Directory.CreateDirectory(directory);
            foreach (var theme in new[] { "forest", "catacombs" })
            foreach (var length in new[] { DungeonLength.Short, DungeonLength.Normal, DungeonLength.Long })
            for (var seed = 231023; seed < 231031; seed++)
            {
                var id = "dungeon." + theme + ".expedition";
                var file = Path.Combine(directory, id + "-" + length + "-" + seed + ".json");
                if (stage == "before" && File.Exists(file)) continue;
                var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                new DungeonObjectPlacementSystem().Place(dungeon, catalog.EquipmentPartDefinitions);
                var save = new DungeonSaveData { DungeonId = id, Seed = seed, DungeonLevel = 10, Length = length,
                    LayoutVersion = DungeonLayoutSnapshot.CurrentVersion, Layout = DungeonLayoutSnapshot.Capture(dungeon) };
                File.WriteAllText(file, JsonUtility.ToJson(save));
            }
        }
    }
}
