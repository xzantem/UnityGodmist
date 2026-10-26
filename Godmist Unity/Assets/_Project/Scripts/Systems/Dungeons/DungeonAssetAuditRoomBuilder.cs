using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    /// <summary>
    /// Builds the explicit, dev-only Catacombs asset gallery used by the
    /// <c>assetroom catacombs &lt;seed&gt;</c> console command.
    ///
    /// The fixture reuses the production definition, room template, object
    /// definitions and encounter assets. It never participates in ordinary
    /// procedural generation or offer selection.
    /// </summary>
    public static class DungeonAssetAuditRoomBuilder
    {
        public const string CatacombsDungeonId = "dungeon.catacombs.expedition";
        private const string RoomIdPrefix = "catacombs.asset_audit";

        private static readonly string[] PreviewEncounterIds =
        {
            "encounter.catacombs.dead_king",
            "encounter.catacombs.undead_pack",
            "encounter.catacombs.bone_line",
            "encounter.catacombs.haunting"
        };

        private static readonly Dictionary<string, string[]> PreviewEnemyInstanceIds =
            new(StringComparer.Ordinal)
            {
                ["encounter.catacombs.dead_king"] = new[]
                {
                    "enemy.catacombs.dead_king.boss"
                },
                ["encounter.catacombs.undead_pack"] = new[]
                {
                    "enemy.catacombs.undead_pack.1",
                    "enemy.catacombs.undead_pack.2",
                    "enemy.catacombs.undead_pack.3",
                    "enemy.catacombs.undead_pack.4"
                },
                ["encounter.catacombs.bone_line"] = new[]
                {
                    "enemy.catacombs.bone_line.1",
                    "enemy.catacombs.bone_line.4"
                },
                ["encounter.catacombs.haunting"] = new[]
                {
                    "enemy.catacombs.haunting.1"
                }
            };

        public static DungeonInstance Build(
            DungeonDefinition definition,
            GameContentCatalog contentCatalog,
            int seed)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (contentCatalog == null)
                throw new ArgumentNullException(nameof(contentCatalog));
            if (!string.Equals(
                    definition.Id,
                    CatacombsDungeonId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Asset audit room is only available for the production Catacombs definition.");
            }

            var template = SelectTemplate(definition.RoomTemplateCatalog);
            if (template == null)
                throw new InvalidOperationException(
                    "Catacombs asset audit requires a production Final room template.");

            template.ValidateSurfaceGeometry();

            var dungeon = new DungeonInstance(
                definition,
                seed,
                definition.DungeonLevel);
            dungeon.MarkAssetAuditRoom();

            for (var floorIndex = 0; floorIndex < 2; floorIndex++)
            {
                var floor = new DungeonFloorInstance(floorIndex);
                var room = new DungeonRoomInstance(
                    RoomIdPrefix + ".floor_" + floorIndex,
                    DungeonRoomType.Final,
                    template.Id);

                if (!floor.TryAddRoom(room))
                    throw new InvalidOperationException("Nie można dodać pokoju audytowego.");

                var map = new DungeonFloorMapBuilder().Build(
                    floor,
                    definition.RoomTemplateCatalog);
                if (!floor.TrySetMap(map))
                    throw new InvalidOperationException("Nie można zbudować mapy pokoju audytowego.");

                DungeonAssetAuditRoomPlanner.Plan plan = null;
                if (floorIndex == 0)
                {
                    var spawnCell = SelectSpawnCell(map);
                    var encounterDefinitions = ResolveEncounterDefinitions(
                        dungeon,
                        contentCatalog);
                    var enemyDistances = encounterDefinitions
                        .SelectMany(SelectAuditEnemies)
                        .Where(enemy => enemy?.CharacterDefinition != null)
                        .Select(enemy => string.Equals(
                            enemy.CharacterDefinition.Id,
                            "enemy.catacombs.dead_king",
                            StringComparison.Ordinal)
                            ? 3
                            : 2)
                        .ToArray();
                    var objectFootprintHeights = GetObjectFootprintHeights(
                        definition.ObjectDefinitions);
                    plan = CreatePlan(
                        map,
                        spawnCell,
                        seed,
                        CountObjectPlacements(definition.ObjectDefinitions),
                        objectFootprintHeights,
                        enemyDistances);
                    ApplyStructuralAuditPreview(map, room.RoomId, plan);
                    map.SetAssetAuditDaggerCell(ToUnity(plan.DaggerCell));
                    dungeon.SetAssetAuditDaggerCell(ToUnity(plan.DaggerCell));
                }

                if (!dungeon.TryAddFloor(floor))
                    throw new InvalidOperationException("Nie można dodać piętra pokoju audytowego.");

                if (floorIndex == 0)
                {
                    var spawnCell = SelectSpawnCell(floor.Map);
                    var encounterDefinitions = ResolveEncounterDefinitions(
                        dungeon,
                        contentCatalog);
                    PopulateObjectGallery(
                        dungeon,
                        floor,
                        definition.ObjectDefinitions,
                        contentCatalog,
                        plan);
                    PopulateEnemyGallery(
                        dungeon,
                        floor,
                        room,
                        template,
                        encounterDefinitions,
                        plan);

                    dungeon.SetAssetAuditSpawnCell(spawnCell);
                }
            }

            dungeon.MarkContentPlaced();
            return dungeon;
        }

        private static void ApplyStructuralAuditPreview(
            DungeonFloorMap map,
            string roomId,
            DungeonAssetAuditRoomPlanner.Plan plan)
        {
            foreach (var cell in plan.ElevatedCells)
            {
                if (!map.TryAddFloorCell(
                        new DungeonSurfacePosition(ToUnity(cell), 1),
                        roomId))
                {
                    throw new InvalidOperationException(
                        "Nie można dodać podwyższonej powierzchni pokoju audytowego: " + cell);
                }
            }

            var firstLanding = new DungeonSurfacePosition(ToUnity(plan.StairPath[0]), 0);
            var secondLanding = new DungeonSurfacePosition(ToUnity(plan.StairPath[plan.StairPath.Count - 1]), 1);
            if (!map.TryAddStairs(
                    firstLanding,
                    secondLanding,
                    plan.StairPath.Select(ToUnity).ToArray()))
            {
                throw new InvalidOperationException("Nie można dodać schodów pokoju audytowego.");
            }

            foreach (var structuralEdge in plan.GroundStructuralEdges.Concat(plan.ElevatedStructuralEdges))
            {
                var edge = new DungeonSurfaceEdge(
                    new DungeonSurfacePosition(ToUnity(structuralEdge.Edge.First), structuralEdge.Level),
                    new DungeonSurfacePosition(ToUnity(structuralEdge.Edge.Second), structuralEdge.Level));
                if (!map.SetWallType(edge, structuralEdge.WallType))
                {
                    throw new InvalidOperationException(
                        "Nie można dodać krawędzi konstrukcyjnej pokoju audytowego: " + edge.CellEdge);
                }
            }
        }

        private static DungeonRoomTemplateDefinition SelectTemplate(
            DungeonRoomTemplateCatalog catalog)
        {
            if (catalog?.Templates == null)
                return null;

            return catalog.GenerationTemplates
                .Where(template => template != null
                    && template.RoomType == DungeonRoomType.Final
                    && template.Id.IndexOf("production", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(template => template.EnumerateSurfaces().Count())
                .ThenBy(template => template.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static void PopulateObjectGallery(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            IReadOnlyList<DungeonObjectDefinition> definitions,
            GameContentCatalog contentCatalog,
            DungeonAssetAuditRoomPlanner.Plan plan)
        {
            var positionIndex = 0;

            var doorDefinition = definitions?.OfType<DoorDungeonObjectDefinition>().FirstOrDefault();
            if (doorDefinition == null)
                throw new InvalidOperationException("Catacombs asset audit requires a door definition.");

            AddDoor(
                floor,
                doorDefinition,
                "door.horizontal.closed",
                isOpen: false,
                plan.DoorEdges[0]);
            AddDoor(
                floor,
                doorDefinition,
                "door.vertical.open",
                isOpen: true,
                plan.DoorEdges[1]);

            foreach (var definition in definitions ?? Array.Empty<DungeonObjectDefinition>())
            {
                if (definition == null || definition is DoorDungeonObjectDefinition)
                    continue;

                if (definition is ChestDungeonObjectDefinition chestDefinition)
                {
                    AddObject(
                        floor,
                        new ChestDungeonObjectInstance(
                            dungeon.DungeonId + ".asset_audit.chest.locked." + chestDefinition.Id,
                            chestDefinition,
                            ToUnity(plan.ObjectCells[positionIndex++]),
                            Array.Empty<ItemInstance>(),
                            initialGold: 25));

                    var unlocked = new ChestDungeonObjectInstance(
                        dungeon.DungeonId + ".asset_audit.chest.unlocked." + chestDefinition.Id,
                        chestDefinition,
                        ToUnity(plan.ObjectCells[positionIndex++]),
                        CreateAuditChestItems(contentCatalog, dungeon.DungeonId),
                        initialGold: 50);
                    unlocked.TryRestoreUnlocked(true);
                    AddObject(floor, unlocked);
                    continue;
                }

                var position = ToUnity(plan.ObjectCells[positionIndex++]);
                var instance = CreateObjectInstance(
                    dungeon.DungeonId + ".asset_audit." + definition.Id,
                    definition,
                    position);
                if (instance == null)
                    throw new InvalidOperationException(
                        "Nieobsługiwana definicja obiektu audytowego: " + definition.Id);

                AddObject(floor, instance);
            }
        }

        private static DungeonObjectInstance CreateObjectInstance(
            string instanceId,
            DungeonObjectDefinition definition,
            Vector2Int position)
        {
            return definition switch
            {
                ObstacleDungeonObjectDefinition obstacle =>
                    new ObstacleDungeonObjectInstance(instanceId, obstacle, position),
                SpikeTrapDungeonObjectDefinition trap =>
                    new SpikeTrapDungeonObjectInstance(instanceId, trap, position),
                LeverDungeonObjectDefinition lever =>
                    new LeverDungeonObjectInstance(instanceId, lever, position),
                ConditionDungeonObjectDefinition condition =>
                    new ConditionDungeonObjectInstance(instanceId, condition, position),
                _ => null
            };
        }

        private static void AddObject(
            DungeonFloorInstance floor,
            DungeonObjectInstance instance)
        {
            if (instance == null || !floor.TryAddObject(instance))
                throw new InvalidOperationException(
                    "Nie można umieścić obiektu pokoju audytowego: " + instance?.InstanceId);
        }

        private static void AddDoor(
            DungeonFloorInstance floor,
            DoorDungeonObjectDefinition definition,
            string suffix,
            bool isOpen,
            DungeonAssetAuditRoomPlanner.Edge plannedEdge)
        {
            var edge = new DungeonCellEdge(
                ToUnity(plannedEdge.First),
                ToUnity(plannedEdge.Second));
            if (!floor.Map.SetWallType(edge, DungeonWallType.Low))
                throw new InvalidOperationException("Nie można przygotować ściany dla drzwi audytowych.");

            var door = new DoorDungeonObjectInstance(
                "dungeon.catacombs.asset_audit." + suffix,
                definition,
                edge);
            if (isOpen)
                door.TrySetOpen(true);

            AddObject(floor, door);
        }

        private static void PopulateEnemyGallery(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            DungeonRoomInstance room,
            DungeonRoomTemplateDefinition template,
            IReadOnlyList<EncounterDefinition> encounterDefinitions,
            DungeonAssetAuditRoomPlanner.Plan plan)
        {
            var positionIndex = 0;
            var triggerArea = new RectInt(
                room.LayoutPosition,
                DungeonRoomTransformUtility.GetRotatedSize(
                    template.SizeInCells,
                    room.RotationDegrees));

            foreach (var definition in encounterDefinitions)
            {
                var instance = new DungeonEncounterInstance(
                    dungeon.DungeonId + ".asset_audit." + definition.Id,
                    definition,
                    room.RoomId,
                    triggerArea);

                var selectedEnemies = SelectAuditEnemies(definition);
                if (!instance.TryConfigureReinforcements(
                        selectedEnemies.Select(enemy => enemy.InstanceId)))
                {
                    throw new InvalidOperationException(
                        "Nie można ograniczyć encounteru audytowego do unikalnych archetypów: "
                        + definition.Id);
                }

                foreach (var enemy in instance.Enemies)
                {
                    var cell = ToUnity(plan.EnemyCells[positionIndex++]);
                    instance.TrySetEnemyExplorationPosition(enemy.InstanceId, cell);
                }

                if (!floor.TryAddEncounter(instance))
                    throw new InvalidOperationException(
                        "Nie można dodać encounteru pokoju audytowego: " + definition.Id);
            }
        }

        private static Vector2Int SelectSpawnCell(DungeonFloorMap map)
        {
            var cell = map.FloorCells
                .Where(candidate => candidate.x >= 1 && candidate.y >= 3)
                .OrderBy(candidate => candidate.y)
                .ThenBy(candidate => candidate.x)
                .FirstOrDefault();
            if (!map.IsWalkable(cell))
                throw new InvalidOperationException("Nie znaleziono pola spawnu pokoju audytowego.");
            return cell;
        }

        private static DungeonAssetAuditRoomPlanner.Plan CreatePlan(
            DungeonFloorMap map,
            Vector2Int spawnCell,
            int seed,
            int objectCount,
            IReadOnlyList<int> objectFootprintHeights,
            IReadOnlyList<int> enemyDistances)
        {
            var cells = map.FloorCells.Select(cell => new DungeonAssetAuditRoomPlanner.Cell(cell.x, cell.y));
            var blocked = map.SurfaceWallEdges
                .Where(edge => edge.Level == 0)
                .Select(edge => new DungeonAssetAuditRoomPlanner.Edge(
                    new DungeonAssetAuditRoomPlanner.Cell(edge.FirstCell.x, edge.FirstCell.y),
                    new DungeonAssetAuditRoomPlanner.Cell(edge.SecondCell.x, edge.SecondCell.y)));
            return DungeonAssetAuditRoomPlanner.Build(
                cells,
                blocked,
                new DungeonAssetAuditRoomPlanner.Cell(spawnCell.x, spawnCell.y),
                seed,
                objectCount,
                enemyDistances,
                objectFootprintHeights);
        }

        private static Vector2Int ToUnity(DungeonAssetAuditRoomPlanner.Cell cell)
            => new(cell.X, cell.Y);

        private static IReadOnlyList<ItemInstance> CreateAuditChestItems(
            GameContentCatalog contentCatalog,
            string dungeonId)
        {
            var factory = new ItemInstanceFactory();
            var items = new List<ItemInstance>();
            foreach (var id in new[] { "item.lootbag.catacombs.dead_king", DungeonCampService.KitId })
            {
                if (!contentCatalog.TryGetItem(id, out var definition))
                    throw new InvalidOperationException("Brak wymaganego assetu skrzyni audytowej: " + id);
                items.Add(factory.Create(
                    definition,
                    dungeonId + ".asset_audit.item." + id));
            }
            return items;
        }

        private static int CountObjectPlacements(
            IReadOnlyList<DungeonObjectDefinition> definitions)
        {
            return (definitions ?? Array.Empty<DungeonObjectDefinition>())
                .Where(definition => definition != null && definition is not DoorDungeonObjectDefinition)
                .Sum(definition => definition is ChestDungeonObjectDefinition ? 2 : 1);
        }

        private static IReadOnlyList<int> GetObjectFootprintHeights(
            IReadOnlyList<DungeonObjectDefinition> definitions)
        {
            var result = new List<int>();
            foreach (var definition in definitions ?? Array.Empty<DungeonObjectDefinition>())
            {
                if (definition == null || definition is DoorDungeonObjectDefinition) continue;
                if (definition is ChestDungeonObjectDefinition)
                {
                    result.Add(1);
                    result.Add(1);
                    continue;
                }

                result.Add(string.Equals(
                    definition.Id,
                    "object.obstacle.sarcophagus",
                    StringComparison.Ordinal)
                    ? 2
                    : 1);
            }

            return result;
        }

        private static List<EncounterDefinition> ResolveEncounterDefinitions(
            DungeonInstance dungeon,
            GameContentCatalog contentCatalog)
        {
            var encounterDefinitions = new List<EncounterDefinition>();
            foreach (var id in PreviewEncounterIds)
            {
                EncounterDefinition definition = null;
                if (contentCatalog.TryGetEncounter(id, out var catalogDefinition))
                    definition = catalogDefinition;
                else if (string.Equals(id, dungeon.Definition.BossEncounterDefinition?.Id,
                    StringComparison.Ordinal))
                    definition = dungeon.Definition.BossEncounterDefinition;
                if (definition != null && !encounterDefinitions.Contains(definition))
                    encounterDefinitions.Add(definition);
            }
            if (encounterDefinitions.Count != PreviewEncounterIds.Length)
                throw new InvalidOperationException(
                    "Catacombs asset audit requires all four preview encounter assets.");

            foreach (var definition in encounterDefinitions)
                SelectAuditEnemies(definition);

            return encounterDefinitions;
        }

        private static IReadOnlyList<EncounterEnemyData> SelectAuditEnemies(
            EncounterDefinition definition)
        {
            if (definition == null
                || !PreviewEnemyInstanceIds.TryGetValue(definition.Id, out var selectedIds))
            {
                throw new InvalidOperationException(
                    "Brak konfiguracji archetypów encounteru audytowego: "
                    + definition?.Id);
            }

            var source = definition.Enemies ?? new List<EncounterEnemyData>();
            var selected = selectedIds
                .Select(id => source.FirstOrDefault(enemy =>
                    string.Equals(enemy?.InstanceId, id, StringComparison.Ordinal)))
                .ToList();
            if (selected.Any(enemy => enemy == null || enemy.CharacterDefinition == null))
                throw new InvalidOperationException(
                    "Encounter audytowy nie zawiera wymaganego archetypu: " + definition.Id);
            return selected;
        }
    }
}
