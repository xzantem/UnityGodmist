using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Authoring data for the first Catacombs room pool; the JSON can grow past 16 entries.</summary>
    public static class VS14RoomBlueprintAuthoring
    {
        private const string Root = "Assets/_Project/ScriptableObjects/Dungeons/";
        private const string Rooms = Root + "RoomTemplates/";
        private const string BlueprintPath = "Assets/_Project/Scripts/Editor/VS14RoomBlueprints.json";

        [Serializable]
        private sealed class BlueprintCatalog
        {
            public Blueprint[] rooms;
        }

        [Serializable]
        private sealed class Blueprint
        {
            public string suffix;
            public string displayName;
            public string[] rows;
            public string doors;
            public string[] structuralEdges;
            public string obstacle;
            public string trap;
            public string target;
            public bool progression;
            public bool nonCombat;
        }

        private sealed class ObjectSet
        {
            public DungeonObjectDefinition Pillar;
            public DungeonObjectDefinition Rubble;
            public DungeonObjectDefinition Sarcophagus;
            public DungeonObjectDefinition Spikes;
            public DungeonObjectDefinition GraveSeal;
            public DungeonObjectDefinition Gas;
            public DungeonObjectDefinition Chest;
            public DungeonObjectDefinition Lever;
            public DungeonObjectDefinition Condition;

            public IEnumerable<DungeonObjectDefinition> All => new[]
            {
                Pillar, Rubble, Sarcophagus, Spikes, GraveSeal, Gas, Chest, Lever, Condition
            };
        }

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("VS-14: missing asset " + path);

        private static string Field(string name) => "<" + name + ">k__BackingField";

        private static ObjectSet LoadObjects() => new()
        {
            Pillar = Require<DungeonObjectDefinition>(Root + "Objects/DO_Obstacle_Pillar.asset"),
            Rubble = Require<DungeonObjectDefinition>(Root + "Objects/DO_Obstacle_Rubble.asset"),
            Sarcophagus = Require<DungeonObjectDefinition>(Root + "Objects/DO_Obstacle_Sarcophagus.asset"),
            Spikes = Require<DungeonObjectDefinition>(Root + "Objects/DO_Trap_Spikes.asset"),
            GraveSeal = Require<DungeonObjectDefinition>(Root + "Objects/DO_Trap_GraveSeal.asset"),
            Gas = Require<DungeonObjectDefinition>(Root + "Objects/DO_Trap_Gas.asset"),
            Chest = Require<DungeonObjectDefinition>(Root + "Objects/DO_Chest_Debug.asset"),
            Lever = Require<DungeonObjectDefinition>(Root + "Objects/DO_Lever_Debug.asset"),
            Condition = Require<DungeonObjectDefinition>(Root + "Objects/DO_Condition_Debug.asset")
        };

        private static Blueprint[] LoadBlueprints()
        {
            var text = Require<TextAsset>(BlueprintPath);
            var catalog = JsonUtility.FromJson<BlueprintCatalog>(text.text);
            if (catalog?.rooms == null || catalog.rooms.Length < 16)
                throw new InvalidOperationException("VS-14: the first Catacombs pool needs at least 16 blueprints.");
            if (catalog.rooms.Any(spec => spec == null || string.IsNullOrWhiteSpace(spec.suffix)
                                               || spec.rows == null || spec.rows.Length == 0)
                || catalog.rooms.Select(spec => spec.suffix).Distinct(StringComparer.Ordinal).Count()
                   != catalog.rooms.Length)
                throw new InvalidOperationException("VS-14: invalid or duplicate room blueprint.");
            return catalog.rooms;
        }

        public static IReadOnlyList<DungeonRoomTemplateDefinition> Configure()
        {
            var objects = LoadObjects();
            var rooms = new List<DungeonRoomTemplateDefinition>();
            foreach (var spec in LoadBlueprints())
            {
                var existingNewRoom = spec.suffix == "Ossuary"
                                      || spec.suffix == "SarcophagusHall"
                                      || spec.suffix == "Tactical_Corridor_Production";
                var fileName = "DRT_Catacombs_" + spec.suffix
                    + (existingNewRoom ? "" : "_Production") + ".asset";
                var path = Rooms + fileName;
                var room = AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(path);
                if (room == null)
                {
                    var sourceSuffix = spec.suffix == "Tactical_Corridor_Production"
                        ? "Tactical_Corridor"
                        : spec.suffix;
                    var source = Require<DungeonRoomTemplateDefinition>(
                        Rooms + "DRT_Catacombs_" + sourceSuffix + ".asset");
                    room = UnityEngine.Object.Instantiate(source);
                    room.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(room, path);
                }

                ConfigureRoom(room, spec, objects);
                var report = DungeonRoomTemplateEditorValidator.Validate(room);
                if (!report.IsValid)
                    throw new InvalidOperationException(room.name + ": " + string.Join("; ", report.Errors));
                rooms.Add(room);
                EditorUtility.SetDirty(room);
            }
            return rooms;
        }

        public static void AddRequiredObjectDefinitions(DungeonDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            foreach (var candidate in LoadObjects().All)
                if (!definition.ObjectDefinitions.Contains(candidate))
                    definition.ObjectDefinitions.Add(candidate);
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureRoom(DungeonRoomTemplateDefinition room, Blueprint spec, ObjectSet objects)
        {
            var rows = MakePlayableRows(spec.rows);
            OpenFramedDoorways(rows, spec.doors);
            var width = rows[0].Length;
            var height = rows.Length;
            var size = new Vector2Int(width, height);
            var sourceId = spec.suffix switch
            {
                "Ossuary" => "room.catacombs.ossuary",
                "SarcophagusHall" => "room.catacombs.sarcophagus_hall",
                "Tactical_Corridor_Production" => "room.catacombs.corridor.tactical.production",
                _ => "room.catacombs." + spec.suffix.ToLowerInvariant().Replace('_', '.') + ".production"
            };
            var type = spec.suffix switch
            {
                "Start" => DungeonRoomType.Start,
                "Final" => DungeonRoomType.Final,
                "Optional" or "Optional_Lever" or "Optional_Condition" => DungeonRoomType.Optional,
                "Corridor" or "Tactical_Corridor_Production" => DungeonRoomType.Corridor,
                _ => DungeonRoomType.Normal
            };
            var serialized = new SerializedObject(room);
            serialized.FindProperty(Field("Id")).stringValue = sourceId;
            serialized.FindProperty(Field("DisplayName")).stringValue = spec.displayName;
            serialized.FindProperty(Field("RoomType")).enumValueIndex = (int)type;
            serialized.FindProperty(Field("ExcludeFromRandomEncounters")).boolValue = spec.nonCombat;
            serialized.FindProperty(Field("CanBeRequiredProgressionBranch")).boolValue = spec.progression;
            serialized.FindProperty(Field("SizeInCells")).vector2IntValue = size;
            serialized.FindProperty(Field("BoundaryEdgesConfigured")).boolValue = false;
            serialized.FindProperty(Field("AllowedBoundaryEdges")).ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            room.AllowedRotationDegrees.Clear();
            room.AllowedRotationDegrees.AddRange(new[] { 0, 90, 180, 270 });
            room.Footprint.FloorCellOffsets.Clear();
            room.Surfaces.Clear();
            room.Stairs.Clear();
            room.StructuralAnchors.Clear();
            room.SemanticAnchors.Clear();
            room.ContentGroups.Clear();
            room.DifficultTerrainCells.Clear();

            var floor = new HashSet<Vector2Int>();
            Vector2Int? entryMarker = null;
            var enemies = new List<Vector2Int>();
            var boss = new List<Vector2Int>();
            for (var row = 0; row < height; row++)
            for (var x = 0; x < width; x++)
            {
                var symbol = rows[row][x];
                if (symbol is '#' or '·') continue;
                var cell = new Vector2Int(x, height - 1 - row);
                floor.Add(cell);
                if (symbol == 'E') entryMarker = cell;
                if (symbol == 'W') enemies.Add(cell);
                if (symbol == 'B') boss.Add(cell);
                if (symbol == '■')
                    room.SemanticAnchors.Add(Anchor("cover", room.SemanticAnchors.Count, cell,
                        DungeonSemanticAnchorType.Obstacle, ResolveObstacle(spec.obstacle, objects)));
                if (symbol == '!')
                    room.SemanticAnchors.Add(Anchor("trap", room.SemanticAnchors.Count, cell,
                        DungeonSemanticAnchorType.Trap, ResolveTrap(spec.trap, objects)));
                if (symbol == 'S')
                    room.SemanticAnchors.Add(Anchor("goal", room.SemanticAnchors.Count, cell,
                        ResolveTargetType(spec.target), ResolveTarget(spec.target, objects)));
            }

            if (floor.Count != width * height)
                room.Footprint.FloorCellOffsets.AddRange(floor.OrderBy(cell => cell.x).ThenBy(cell => cell.y));
            AddStructuralEdges(room, spec, floor);
            foreach (var cell in boss.Concat(enemies))
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData(
                    "enemy." + room.SemanticAnchors.Count,
                    DungeonSemanticAnchorType.Enemy, cell, Vector2Int.one,
                    encounterEnemyIndex: boss.Concat(enemies).ToList().IndexOf(cell)));

            var externalEdges = DungeonRoomBoundaryUtility
                .EnumerateExternalBoundaryEdges(room).ToList();
            room.AllowedBoundaryEdges.Clear();
            room.AllowedBoundaryEdges.AddRange(externalEdges);
            var boundaryState = new SerializedObject(room);
            boundaryState.FindProperty(Field("BoundaryEdgesConfigured")).boolValue = true;
            boundaryState.ApplyModifiedPropertiesWithoutUndo();
            RestrictBoundaryEdges(room, spec.doors, entryMarker);
            if (room.AllowedBoundaryEdges.Count == 0)
                throw new InvalidOperationException(room.name + ": no legal doorway edges");
            if (room.Stairs.Count > 0 || room.EnumerateSurfaces().Any(surface => surface.Level != 0))
                throw new InvalidOperationException(room.name + ": elevated rooms are outside VS-14");
        }

        private static DungeonSemanticAnchorData Anchor(string prefix, int index, Vector2Int cell,
            DungeonSemanticAnchorType type, DungeonObjectDefinition definition)
        {
            if (definition == null)
                throw new InvalidOperationException("VS-14: missing object for " + prefix);
            return new DungeonSemanticAnchorData(prefix + "." + index, type, cell,
                Vector2Int.one, isRequired: true, objectDefinition: definition);
        }

        private static void AddStructuralEdges(DungeonRoomTemplateDefinition room,
            Blueprint spec, HashSet<Vector2Int> floor)
        {
            foreach (var description in spec.structuralEdges ?? Array.Empty<string>())
            {
                var parts = description.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4
                    || !Enum.TryParse(parts[0], out DungeonWallType wallType)
                    || !int.TryParse(parts[1], out var x)
                    || !int.TryParse(parts[2], out var y)
                    || !Enum.TryParse(parts[3], out DungeonEdgeDirection direction))
                    throw new InvalidOperationException(spec.suffix + ": invalid structural edge " + description);
                var cell = new Vector2Int(x, y);
                var neighbour = cell + DungeonRoomTransformUtility.GetDirectionOffset(direction);
                if (!floor.Contains(cell)
                    || (wallType == DungeonWallType.Railing) == floor.Contains(neighbour))
                    throw new InvalidOperationException(spec.suffix + ": misplaced structural edge " + description);
                room.StructuralAnchors.Add(new DungeonStructuralAnchorData(
                    DungeonStructuralAnchorType.Wall, cell, direction, wallType));
            }
        }

        private static DungeonObjectDefinition ResolveObstacle(string name, ObjectSet objects) => name switch
        {
            "Pillar" => objects.Pillar,
            "Rubble" => objects.Rubble,
            "Sarcophagus" => objects.Sarcophagus,
            _ => throw new InvalidOperationException("VS-14: unknown obstacle " + name)
        };

        private static DungeonObjectDefinition ResolveTrap(string name, ObjectSet objects) => name switch
        {
            "Spikes" => objects.Spikes,
            "GraveSeal" => objects.GraveSeal,
            "Gas" => objects.Gas,
            _ => throw new InvalidOperationException("VS-14: unknown trap " + name)
        };

        private static DungeonObjectDefinition ResolveTarget(string name, ObjectSet objects) => name switch
        {
            "Chest" => objects.Chest,
            "Lever" => objects.Lever,
            "Condition" => objects.Condition,
            _ => throw new InvalidOperationException("VS-14: unknown goal " + name)
        };

        private static DungeonSemanticAnchorType ResolveTargetType(string name) => name switch
        {
            "Chest" => DungeonSemanticAnchorType.Chest,
            "Lever" => DungeonSemanticAnchorType.Lever,
            "Condition" => DungeonSemanticAnchorType.Condition,
            _ => throw new InvalidOperationException("VS-14: unknown goal " + name)
        };

        private static char[][] MakePlayableRows(string[] authored)
        {
            if (authored == null || authored.Length == 0)
                throw new InvalidOperationException("VS-14: empty room footprint");
            var rows = authored.Select(value => value.ToCharArray()).ToArray();
            if (rows.Any(row => row.Length != rows[0].Length))
                throw new InvalidOperationException("VS-14: room rows must have matching widths");
            // Keep the same two column and row insertions as the concept viewer.
            // Removing its wall frame and spreading four replacement cells changed
            // the shape of nearly every saved room despite matching dimensions.
            var xCopies = new HashSet<int> { 2, rows[0].Length - 3 };
            var yCopies = new HashSet<int> { 2, rows.Length - 3 };
            var expanded = rows.Select(row =>
            {
                var result = new List<char>();
                for (var x = 0; x < row.Length; x++)
                {
                    result.Add(row[x]);
                    if (xCopies.Contains(x))
                        result.Add(CopyForExpansion(row[x]));
                }
                return result.ToArray();
            }).ToArray();
            var output = new List<char[]>();
            for (var y = 0; y < expanded.Length; y++)
            {
                output.Add(expanded[y]);
                if (yCopies.Contains(y))
                    output.Add(expanded[y].Select(CopyForExpansion).ToArray());
            }
            return output.ToArray();
        }

        private static char CopyForExpansion(char symbol) =>
            symbol is 'E' or 'X' or 'W' or 'S' or 'B' ? '.' : symbol;

        private static void OpenFramedDoorways(char[][] rows, string policy)
        {
            var width = rows[0].Length;
            var height = rows.Length;
            if (!rows[0].All(c => c == '#') || !rows[height - 1].All(c => c == '#')
                || !rows.All(row => row[0] == '#' && row[width - 1] == '#'))
                return;

            var directions = (policy ?? "all") == "all"
                ? new[] { "up", "right", "down", "left" }
                : policy.Split(',').Select(part => part.Trim()).ToArray();
            foreach (var direction in directions)
            {
                var horizontal = direction.StartsWith("up", StringComparison.Ordinal)
                                 || direction.StartsWith("down", StringComparison.Ordinal)
                                 || direction.StartsWith("bottom", StringComparison.Ordinal);
                var farEdge = direction.StartsWith("down", StringComparison.Ordinal)
                              || direction.StartsWith("bottom", StringComparison.Ordinal)
                              || direction.StartsWith("right", StringComparison.Ordinal);
                var length = horizontal ? width : height;
                var marker = direction.EndsWith("-marker", StringComparison.Ordinal)
                    ? FindMarkerCoordinate(rows, horizontal) : length / 2;
                var bestStart = -1;
                var bestScore = int.MaxValue;
                for (var start = 1; start < length - 2; start++)
                {
                    var firstDepth = DoorDepth(rows, horizontal, farEdge, start);
                    var secondDepth = DoorDepth(rows, horizontal, farEdge, start + 1);
                    if (firstDepth < 0 || secondDepth < 0) continue;
                    var score = Math.Max(firstDepth, secondDepth) * 100
                                + Math.Abs(start - marker) + Math.Abs(start + 1 - marker);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestStart = start;
                }
                if (bestStart < 0)
                    throw new InvalidOperationException("VS-14: no doorway into " + direction);
                for (var lane = bestStart; lane <= bestStart + 1; lane++)
                {
                    var depth = DoorDepth(rows, horizontal, farEdge, lane);
                    for (var step = 0; step < depth; step++)
                    {
                        var x = horizontal ? lane : farEdge ? width - 1 - step : step;
                        var y = horizontal ? farEdge ? height - 1 - step : step : lane;
                        rows[y][x] = '.';
                    }
                }
            }
        }

        private static int FindMarkerCoordinate(char[][] rows, bool horizontal)
        {
            for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == 'E') return horizontal ? x : y;
            throw new InvalidOperationException("VS-14: marker doorway needs an E cell");
        }

        private static int DoorDepth(char[][] rows, bool horizontal, bool farEdge, int lane)
        {
            var length = horizontal ? rows.Length : rows[0].Length;
            for (var step = 0; step < length; step++)
            {
                var x = horizontal ? lane : farEdge ? rows[0].Length - 1 - step : step;
                var y = horizontal ? farEdge ? rows.Length - 1 - step : step : lane;
                if (rows[y][x] != '#') return rows[y][x] == '·' ? -1 : step;
            }
            return -1;
        }

        private static void RestrictBoundaryEdges(DungeonRoomTemplateDefinition room,
            string policy, Vector2Int? entryMarker)
        {
            var parts = (policy ?? "all").Split(',');
            var marker = entryMarker ?? Vector2Int.zero;
            var occupied = room.SemanticAnchors.Select(anchor => anchor.LocalCell).ToHashSet();
            var size = room.SizeInCells;
            var candidates = room.AllowedBoundaryEdges.Where(edge =>
                edge != null && !occupied.Contains(edge.LocalCell)
                && (edge.Direction switch
                {
                    DungeonEdgeDirection.Up => edge.LocalCell.y == size.y - 1,
                    DungeonEdgeDirection.Right => edge.LocalCell.x == size.x - 1,
                    DungeonEdgeDirection.Down => edge.LocalCell.y == 0,
                    DungeonEdgeDirection.Left => edge.LocalCell.x == 0,
                    _ => false
                })).ToList();
            if (!string.Equals(policy, "all", StringComparison.Ordinal))
                candidates = candidates.Where(edge => parts.Any(part =>
                {
                    var name = part.Trim();
                    var direction = name.StartsWith("bottom", StringComparison.Ordinal)
                                    || name.StartsWith("down", StringComparison.Ordinal) ? DungeonEdgeDirection.Down
                        : name.StartsWith("left", StringComparison.Ordinal) ? DungeonEdgeDirection.Left
                        : name.StartsWith("right", StringComparison.Ordinal) ? DungeonEdgeDirection.Right
                        : name.StartsWith("up", StringComparison.Ordinal) ? DungeonEdgeDirection.Up
                        : throw new InvalidOperationException("VS-14: unknown doorway policy " + policy);
                    if (edge.Direction != direction) return false;
                    if (!name.EndsWith("-marker", StringComparison.Ordinal)) return true;
                    return direction is DungeonEdgeDirection.Up or DungeonEdgeDirection.Down
                        ? Mathf.Abs(edge.LocalCell.x - marker.x) <= 2
                        : Mathf.Abs(edge.LocalCell.y - marker.y) <= 2;
                })).ToList();
            room.AllowedBoundaryEdges.Clear();
            room.AllowedBoundaryEdges.AddRange(candidates);
        }
    }
}
