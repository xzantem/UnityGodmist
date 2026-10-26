using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    // Explicit authoring request only; never rewrites content on normal import.
    [InitializeOnLoad]
    public static partial class BiomeRoomProductionSetup
    {
        [Serializable] private sealed class Specs { public Spec[] rooms; public AuthoredRoom[] authoredRooms; }
        [Serializable] private sealed class AuthoredRoom { public string biome,path,productionPath,function; }
        [Serializable] private sealed class Point { public int x, y; public Vector2Int Cell => new(x, y); }
        [Serializable] private sealed class Port { public string direction; public int x, y; public Vector2Int Cell => new(x, y); }
        [Serializable] private sealed class Spec
        {
            public string id, name, source, biome, type, doors, function;
            public string[] rows;
            public int zone = -1;
            public Point[] props, enemies, traps, difficult;
            public Port[] lowWalls;
            public bool repositionFurniture;
            public string lowWallPurpose;
            public BarrierRouteProbe lowWallRouteProbe;
            public Point goal, reward;
            public Port[] ports;
        }
        [Serializable] private sealed class BarrierRouteProbe { public Point from,to; public int minDetour; }
        private const string Root = "Assets/_Project/ScriptableObjects/Dungeons/";
        private const string Output = Root + "ProductionRooms/";
        private static readonly string Request = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/biome-room-authoring-20261023/apply-content.request"));
        static BiomeRoomProductionSetup() => EditorApplication.update += Tick;
        private static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed) return;
            if (File.Exists(TerrainRequest))
            {
                File.Delete(TerrainRequest);
                try { ApplyTerrainOnly(); File.WriteAllText(TerrainRequest+".state.txt", "APPLIED "+DateTime.UtcNow.ToString("O")); }
                catch(Exception error) { File.WriteAllText(TerrainRequest+".state.txt",error.ToString());Debug.LogException(error); }
                return;
            }
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { Apply(); File.WriteAllText(Path.ChangeExtension(Request, ".state.txt"), "APPLIED " + DateTime.UtcNow.ToString("O")); }
            catch (Exception error) { File.WriteAllText(Path.ChangeExtension(Request, ".state.txt"), error.ToString()); Debug.LogException(error); }
        }
        private static void Set(UnityEngine.Object asset, string property, Action<SerializedProperty> assign)
        {
            var state = new SerializedObject(asset);
            var field = state.FindProperty("<" + property + ">k__BackingField")
                ?? throw new InvalidOperationException(asset.name + "." + property);
            assign(field); state.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Godmist/Content/Apply production biome room authoring")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(Output.TrimEnd('/'))) AssetDatabase.CreateFolder(Root.TrimEnd('/'), "ProductionRooms");
            var specAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Scripts/Editor/BiomeRoomProductionBlueprints.json");
            var specs = JsonUtility.FromJson<Specs>(specAsset.text).rooms;
            var allRooms = AssetDatabase.FindAssets("t:DungeonRoomTemplateDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null).GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
            var authored = new Dictionary<string, List<DungeonRoomTemplateDefinition>> { ["forest"] = new(), ["catacombs"] = new() };
            foreach (var spec in specs)
            {
                var source = allRooms[spec.source];
                var path = Output + "DRT_" + spec.biome + "_" + spec.id + ".asset";
                var room = AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(path);
                if (room == null)
                { room = UnityEngine.Object.Instantiate(source); room.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(room, path); }
                Configure(room, source, spec);
                ConfigureTerrain(room, spec);
                var report = DungeonRoomTemplateEditorValidator.Validate(room);
                if (!report.IsValid) throw new InvalidOperationException(room.Id + ": " + string.Join("; ", report.Errors));
                authored[spec.biome].Add(room); EditorUtility.SetDirty(room);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonRoomTemplateCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog.Id.StartsWith("rooms.catacombs.tactical.", StringComparison.Ordinal))
                {
                    // These are intentionally isolated editor/combat fixtures.
                    // They must never receive the expedition production pool.
                    if (catalog.ProductionTemplates.Count > 0 || catalog.UseRoutedConnections)
                    {
                        catalog.ProductionTemplates.Clear();
                        Set(catalog, "UseRoutedConnections", p => p.boolValue = false);
                        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
                    }
                    continue;
                }
                var biome = catalog.Id == "rooms.forest.bandit_leader"
                    || catalog.Id == "dungeon.forest.templates.prototype" ? "forest"
                    : catalog.Id == "rooms.catacombs.skeleton_slayer"
                        || catalog.Id == "dungeon.catacombs.templates.production" ? "catacombs" : null;
                if (biome == null) continue;
                catalog.ProductionTemplates.Clear(); catalog.ProductionTemplates.AddRange(authored[biome]);
                // Dedicated bosses keep their arena semantics, not the generic final room.
                var specialFinal = catalog.Templates.FirstOrDefault(r => r != null && r.RoomType == DungeonRoomType.Final
                    && (r.Id.Contains("bandit_leader.final") || r.Id.Contains("skeleton_slayer.final")));
                if (specialFinal != null)
                {
                    var baseFinal = authored[biome].First(r => r.RoomType == DungeonRoomType.Final);
                    var path = Output + "DRT_" + biome + "_boss_" + specialFinal.name + ".asset";
                    var final = AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(path);
                    if (final == null) { final = UnityEngine.Object.Instantiate(specialFinal); AssetDatabase.CreateAsset(final, path); }
                    var spec = specs.First(s => s.biome == biome && s.type == "Final");
                    Configure(final, specialFinal, spec, "room." + biome + ".production." + specialFinal.name.ToLowerInvariant());
                    ConfigureTerrain(final, spec);
                    catalog.ProductionTemplates.Remove(baseFinal); catalog.ProductionTemplates.Add(final); EditorUtility.SetDirty(final);
                }
                AppendAuthoredRooms(catalog,biome);
                BranchEquipmentRewardsSetup.ConfigureRooms(catalog.GenerationTemplates);
                Set(catalog, "UseRoutedConnections", p => p.boolValue = true);
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            }
            foreach (var room in authored.Values.SelectMany(v => v)) AssetDatabase.SaveAssetIfDirty(room);
        }

        private static void Configure(DungeonRoomTemplateDefinition room, DungeonRoomTemplateDefinition source, Spec spec, string id = null)
        {
            if (spec.rows.Length == 0 || spec.rows.Any(r => r.Length != spec.rows[0].Length)) throw new InvalidOperationException(spec.id);
            var oldSize = source.SizeInCells;
            var size = new Vector2Int(spec.rows[0].Length, spec.rows.Length);
            Set(room, "Id", p => p.stringValue = id ?? "room." + spec.biome + ".production." + spec.id);
            Set(room, "DisplayName", p => p.stringValue = spec.name);
            Set(room, "RoomFunction", p => p.stringValue = spec.function);
            Set(room, "PreferredFloorIndex", p => p.intValue = spec.zone);
            Set(room, "SizeInCells", p => p.vector2IntValue = size);
            Set(room, "RoomType", p => p.enumValueIndex = (int)Enum.Parse<DungeonRoomType>(spec.type));
            Set(room, "BoundaryEdgesConfigured", p => p.boolValue = true);
            Set(room, "CanBeRequiredProgressionBranch", p => p.boolValue = source.CanBeRequiredProgressionBranch);
            room.Footprint.FloorCellOffsets.Clear(); room.Surfaces.Clear(); room.Stairs.Clear(); room.StructuralAnchors.Clear();
            room.DifficultTerrainCells.Clear(); room.SemanticAnchors.Clear(); room.ContentGroups.Clear();
            room.AllowedBoundaryEdges.Clear(); room.AllowedRotationDegrees.Clear(); room.AllowedRotationDegrees.AddRange(new[] { 0, 90, 180, 270 });
            for (var y = 0; y < size.y; y++) for (var x = 0; x < size.x; x++)
                if (spec.rows[size.y - y - 1][x] != '#') room.Footprint.FloorCellOffsets.Add(new Vector2Int(x, y));
            var floor = room.Footprint.FloorCellOffsets.ToHashSet();
            var width = spec.biome == "forest" ? 3 : 2;
            foreach (var letter in spec.doors)
            {
                var direction = letter switch { 'U' => DungeonEdgeDirection.Up, 'D' => DungeonEdgeDirection.Down, 'L' => DungeonEdgeDirection.Left, _ => DungeonEdgeDirection.Right };
                var start = letter switch
                { 'U' => new Vector2Int((size.x - width) / 2, size.y - 1), 'D' => new Vector2Int((size.x - width) / 2, 0),
                    'L' => new Vector2Int(0, (size.y - width) / 2), _ => new Vector2Int(size.x - 1, (size.y - width) / 2) };
                var authoredPort = spec.ports?.SingleOrDefault(p => p.direction == letter.ToString());
                if (authoredPort != null) start = authoredPort.Cell;
                var lateral = letter is 'L' or 'R' ? Vector2Int.up : Vector2Int.right;
                for (var i = 0; i < width; i++)
                {
                    if (!floor.Contains(start + lateral * i)) throw new InvalidOperationException(spec.id + ": doorway outside body.");
                    room.AllowedBoundaryEdges.Add(new DungeonRoomBoundaryEdgeData(start + lateral * i, direction));
                }
            }
            // Recompose functional content inside the new body. Reserve actual
            // entrances and a short approach, not an arbitrary central cross.
            var reserved = new HashSet<Vector2Int>();
            foreach (var edge in room.AllowedBoundaryEdges)
                for (var step = 0; step < 3; step++) reserved.Add(edge.LocalCell - edge.GetDirectionOffset() * step);
            if (spec.biome == "catacombs")
            {
                ConfigureCryptContent(room, source, spec);
                return;
            }
            foreach (var anchor in source.SemanticAnchors)
            {
                if (anchor.ObjectDefinition is ObstacleDungeonObjectDefinition obstacle && obstacle.Id.Contains("tree.prototype")) continue;
                var desired = new Vector2Int(Mathf.RoundToInt(anchor.LocalCell.x * (size.x - 3f) / Math.Max(1, oldSize.x - 1)) + 1,
                    Mathf.RoundToInt(anchor.LocalCell.y * (size.y - 3f) / Math.Max(1, oldSize.y - 1)) + 1);
                IEnumerable<Vector2Int> Occupied(Vector2Int cell)
                {
                    if (anchor.ObjectDefinition is ObstacleDungeonObjectDefinition prop)
                    { foreach (var occupied in prop.EnumerateOccupiedCells(cell)) yield return occupied; }
                    else foreach (var occupied in new RectInt(cell, anchor.AreaSize).allPositionsWithin) yield return occupied;
                }
                IEnumerable<Vector2Int> Clearance(Vector2Int cell)
                {
                    if (anchor.ObjectDefinition is ObstacleDungeonObjectDefinition prop && prop.SizeInCells != Vector2Int.one)
                    {
                        var radius = Math.Max(prop.SizeInCells.x, prop.SizeInCells.y) - 1;
                        for (var y = -radius; y <= radius; y++) for (var x = -radius; x <= radius; x++) yield return cell + new Vector2Int(x, y);
                    }
                    else foreach (var occupied in Occupied(cell)) yield return occupied;
                }
                var choices = floor.Where(c => Clearance(c).All(v => floor.Contains(v) && !reserved.Contains(v))
                        && new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left }.All(d => floor.Contains(c + d)))
                    .OrderBy(c => (c - desired).sqrMagnitude).ThenBy(c => c.y).ThenBy(c => c.x).ToArray();
                if (choices.Length == 0) throw new InvalidOperationException(spec.id + ": no space for " + anchor.Id);
                var cell = choices[0]; reserved.UnionWith(Clearance(cell));
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData(anchor.Id, anchor.Type, cell, anchor.AreaSize,
                    anchor.SpawnChance, anchor.IsRequired, anchor.RequiresAnchorId, anchor.ActivationGroupId,
                    anchor.EncounterEnemyIndex, anchor.ObjectDefinition, anchor.DistributionKey, anchor.PatrolMode));
            }
            // Content-group dependencies retain their original anchor identities.
            room.ContentGroups.AddRange(source.ContentGroups);
        }

        private static void ConfigureCryptContent(DungeonRoomTemplateDefinition room, DungeonRoomTemplateDefinition source, Spec spec)
        {
            var prop = source.SemanticAnchors.FirstOrDefault(a => a.Type == DungeonSemanticAnchorType.Obstacle)?.ObjectDefinition
                ?? AssetDatabase.LoadAssetAtPath<DungeonObjectDefinition>(Root + "Objects/DO_Obstacle_Pillar.asset");
            var trap = source.SemanticAnchors.FirstOrDefault(a => a.Type == DungeonSemanticAnchorType.Trap)?.ObjectDefinition
                ?? AssetDatabase.LoadAssetAtPath<DungeonObjectDefinition>(Root + "Objects/DO_Trap_Spikes.asset");
            for (var i = 0; i < spec.props.Length; i++)
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData("furniture." + i, DungeonSemanticAnchorType.Obstacle,
                    spec.props[i].Cell, Vector2Int.one, isRequired: true, objectDefinition: prop));
            for (var i = 0; i < spec.enemies.Length; i++)
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData("enemy." + i, DungeonSemanticAnchorType.Enemy,
                    spec.enemies[i].Cell, Vector2Int.one, encounterEnemyIndex: i));
            for (var i = 0; i < spec.traps.Length; i++)
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData("hazard." + i, DungeonSemanticAnchorType.Trap,
                    spec.traps[i].Cell, Vector2Int.one, spawnChance: .25f, objectDefinition: trap,
                    distributionKey: "catacombs.production.hazards"));
            foreach (var anchor in source.SemanticAnchors.Where(a => a.Type is DungeonSemanticAnchorType.Chest
                or DungeonSemanticAnchorType.Lever or DungeonSemanticAnchorType.Condition or DungeonSemanticAnchorType.Reward))
            {
                var cell = anchor.Type is DungeonSemanticAnchorType.Lever or DungeonSemanticAnchorType.Condition
                    ? spec.goal.Cell : spec.reward.Cell;
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData(anchor.Id, anchor.Type, cell, Vector2Int.one,
                    anchor.SpawnChance, anchor.IsRequired, objectDefinition: anchor.ObjectDefinition, distributionKey: anchor.DistributionKey));
            }
        }
    }
}
