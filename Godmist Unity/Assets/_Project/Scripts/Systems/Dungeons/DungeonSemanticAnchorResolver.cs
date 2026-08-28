using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public sealed class DungeonEnemyAnchorResolver
    {
        private readonly DungeonSemanticAnchorResolver semanticResolver = new();

        public Dictionary<string, Vector2Int> Resolve(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog,
            DungeonEncounterInstance encounter)
        {
            var result = new Dictionary<string, Vector2Int>(
                StringComparer.Ordinal);

            if (floor == null
                || floor.Map == null
                || catalog == null
                || encounter == null
                || encounter.Definition?.Enemies == null)
            {
                return result;
            }

            var liveEnemyIdsByIndex = new Dictionary<int, string>();

            for (var enemyIndex = 0;
                enemyIndex < encounter.Definition.Enemies.Count;
                enemyIndex++)
            {
                var enemy = encounter.Definition.Enemies[enemyIndex];

                if (enemy == null
                    || string.IsNullOrWhiteSpace(enemy.InstanceId)
                    || encounter.IsEnemyDefeated(enemy.InstanceId))
                {
                    continue;
                }

                liveEnemyIdsByIndex[enemyIndex] = enemy.InstanceId;
            }

            var candidates = semanticResolver.GetCandidates(
                floor,
                catalog,
                new[] { DungeonSemanticAnchorType.Enemy },
                _ => true);
            var usedCells = new HashSet<Vector2Int>();

            foreach (var candidate in candidates)
            {
                if (candidate.Room == null
                    || !string.Equals(
                        candidate.Room.RoomId,
                        encounter.RoomId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!liveEnemyIdsByIndex.TryGetValue(
                        candidate.Anchor.EncounterEnemyIndex,
                        out var enemyId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(enemyId))
                {
                    continue;
                }

                if (candidate.Cells.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Enemy anchor '{candidate.Anchor.Id}' musi wskazywac "
                        + "dokladnie jedna komorke.");
                }

                var cell = candidate.Cells[0];

                if (!result.TryAdd(enemyId, cell))
                {
                    throw new InvalidOperationException(
                        $"Enemy '{enemyId}' ma wiecej niz jeden anchor "
                        + $"w pokoju '{candidate.Room.TemplateId}'.");
                }

                if (!usedCells.Add(cell))
                {
                    throw new InvalidOperationException(
                        $"Enemy anchory w pokoju '{candidate.Room.TemplateId}' "
                        + $"wskazuja te sama komorke ({cell.x}, {cell.y}).");
                }
            }

            return result;
        }

        public HashSet<Vector2Int> GetReservedCells(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog)
        {
            var result = new HashSet<Vector2Int>();

            if (floor == null || catalog == null)
            {
                return result;
            }

            foreach (var encounter in floor.Encounters)
            {
                foreach (var cell in Resolve(floor, catalog, encounter).Values)
                {
                    result.Add(cell);
                }
            }

            return result;
        }
    }

    public sealed class DungeonSemanticAnchorPlacement
    {
        public DungeonRoomInstance Room { get; }
        public DungeonSemanticAnchorData Anchor { get; }
        public RectInt Area { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }

        public DungeonSemanticAnchorPlacement(
            DungeonRoomInstance room,
            DungeonSemanticAnchorData anchor,
            RectInt area,
            IReadOnlyList<Vector2Int> cells)
        {
            Room = room;
            Anchor = anchor;
            Area = area;
            Cells = cells;
        }
    }

    public class DungeonSemanticAnchorResolver
    {
        public List<DungeonSemanticAnchorPlacement> GetCandidates(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog,
            IReadOnlyCollection<DungeonSemanticAnchorType> types,
            Func<DungeonRoomType, bool> roomTypeFilter)
        {
            var result = new List<DungeonSemanticAnchorPlacement>();

            if (floor == null || floor.Map == null || catalog == null || types == null)
            {
                return result;
            }

            var templatesById = BuildTemplateLookup(catalog);
            var requestedTypes = new HashSet<DungeonSemanticAnchorType>(types);

            foreach (var room in floor.Rooms)
            {
                if (room == null || roomTypeFilter != null && !roomTypeFilter(room.RoomType))
                {
                    continue;
                }

                if (!templatesById.TryGetValue(room.TemplateId, out var template))
                {
                    continue;
                }

                var idsInTemplate = new HashSet<string>(StringComparer.Ordinal);

                foreach (var anchor in template.SemanticAnchors
                             ?? new List<DungeonSemanticAnchorData>())
                {
                    if (anchor == null)
                    {
                        continue;
                    }

                    anchor.Normalize();

                    if (string.IsNullOrWhiteSpace(anchor.Id))
                    {
                        throw new InvalidOperationException(
                            $"Kotwa semantyczna w szablonie '{template.Id}' musi miec Id.");
                    }

                    if (!idsInTemplate.Add(anchor.Id))
                    {
                        throw new InvalidOperationException(
                            $"Powtorzona kotwa semantyczna '{anchor.Id}' "
                            + $"w szablonie '{template.Id}'.");
                    }

                    if (!requestedTypes.Contains(anchor.Type))
                    {
                        continue;
                    }

                    var localArea = DungeonRoomTransformUtility.TransformArea(
                        anchor.LocalCell,
                        anchor.AreaSize,
                        template.SizeInCells,
                        room.RotationDegrees);
                    var area = new RectInt(
                        room.LayoutPosition + localArea.position,
                        localArea.size);
                    var cells = new List<Vector2Int>();

                    for (var x = area.xMin; x < area.xMax; x++)
                    {
                        for (var y = area.yMin; y < area.yMax; y++)
                        {
                            var cell = new Vector2Int(x, y);

                            if (!floor.Map.IsWalkable(cell)
                                || !floor.Map.TryGetRoomId(cell, out var roomId)
                                || !string.Equals(
                                    roomId,
                                    room.RoomId,
                                    StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException(
                                    $"Kotwa semantyczna '{anchor.Id}' w szablonie "
                                    + $"'{template.Id}' wskazuje pole poza pokojem.");
                            }

                            cells.Add(cell);
                        }
                    }

                    result.Add(
                        new DungeonSemanticAnchorPlacement(
                            room,
                            anchor,
                            area,
                            cells));
                }
            }

            result.Sort(ComparePlacements);
            return result;
        }

        public List<DungeonSemanticAnchorPlacement> Select(
            IReadOnlyList<DungeonSemanticAnchorPlacement> candidates,
            int minimumCount,
            int maximumCount,
            System.Random random)
        {
            var selected = new List<DungeonSemanticAnchorPlacement>();

            if (candidates == null || candidates.Count == 0 || maximumCount <= 0)
            {
                return selected;
            }

            random ??= new System.Random(0);

            var shuffled = new List<DungeonSemanticAnchorPlacement>(candidates);
            Shuffle(shuffled, random);

            var byRoomAndId = new Dictionary<string, DungeonSemanticAnchorPlacement>(
                StringComparer.Ordinal);

            foreach (var candidate in shuffled)
            {
                byRoomAndId.Add(
                    BuildKey(candidate.Room.RoomId, candidate.Anchor.Id),
                    candidate);
            }

            var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
            var visitingKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var candidate in shuffled)
            {
                if (!IsRequiredProgressionControl(candidate)
                    && !candidate.Anchor.IsRequired)
                {
                    continue;
                }

                TrySelect(
                    candidate,
                    true,
                    maximumCount,
                    random,
                    byRoomAndId,
                    selected,
                    selectedKeys,
                    visitingKeys);
            }

            var progress = true;

            while (selected.Count < maximumCount && progress)
            {
                progress = false;

                foreach (var candidate in shuffled)
                {
                    var beforeCount = selected.Count;

                    TrySelect(
                        candidate,
                        false,
                        maximumCount,
                        random,
                        byRoomAndId,
                        selected,
                        selectedKeys,
                        visitingKeys);

                    if (selected.Count > beforeCount)
                    {
                        progress = true;
                    }

                    if (selected.Count >= maximumCount)
                    {
                        break;
                    }
                }
            }

            if (selected.Count < minimumCount)
            {
                foreach (var candidate in shuffled)
                {
                    if (selected.Count >= minimumCount)
                    {
                        break;
                    }

                    TrySelect(
                        candidate,
                        true,
                        maximumCount,
                        random,
                        byRoomAndId,
                        selected,
                        selectedKeys,
                        visitingKeys);
                }
            }

            return selected;
        }

        private static bool TrySelect(
            DungeonSemanticAnchorPlacement candidate,
            bool force,
            int maximumCount,
            System.Random random,
            Dictionary<string, DungeonSemanticAnchorPlacement> byRoomAndId,
            List<DungeonSemanticAnchorPlacement> selected,
            HashSet<string> selectedKeys,
            HashSet<string> visitingKeys)
        {
            var key = BuildKey(candidate.Room.RoomId, candidate.Anchor.Id);

            if (selectedKeys.Contains(key)) return true;
            if (!visitingKeys.Add(key)) return false;

            try
            {
                var isRequired = candidate.Anchor.IsRequired
                                 || IsRequiredProgressionControl(candidate);

                if (!isRequired
                    && !force
                    && random.NextDouble() > candidate.Anchor.SpawnChance)
                {
                    return false;
                }

                if (selected.Count >= maximumCount && !force && !isRequired)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(candidate.Anchor.RequiresAnchorId))
                {
                    var dependencyKey = BuildKey(
                        candidate.Room.RoomId,
                        candidate.Anchor.RequiresAnchorId);

                    if (!byRoomAndId.TryGetValue(dependencyKey, out var dependency)
                        || !TrySelect(
                        dependency,
                            force || isRequired,
                            maximumCount,
                            random,
                            byRoomAndId,
                            selected,
                            selectedKeys,
                            visitingKeys))
                    {
                        return false;
                    }
                }

                selected.Add(candidate);
                selectedKeys.Add(key);
                return true;
            }
            finally
            {
                visitingKeys.Remove(key);
            }
        }

        private static Dictionary<string, DungeonRoomTemplateDefinition> BuildTemplateLookup(
            DungeonRoomTemplateCatalog catalog)
        {
            var result = new Dictionary<string, DungeonRoomTemplateDefinition>(
                StringComparer.Ordinal);

            foreach (var template in catalog.Templates)
            {
                if (template == null || string.IsNullOrWhiteSpace(template.Id))
                {
                    continue;
                }

                if (!result.TryAdd(template.Id, template))
                {
                    throw new InvalidOperationException(
                        $"Powtorzony RoomTemplateId '{template.Id}'.");
                }
            }

            return result;
        }

        private static string BuildKey(string roomId, string anchorId)
        {
            return $"{roomId}\u001F{anchorId}";
        }

        private static bool IsRequiredProgressionControl(
            DungeonSemanticAnchorPlacement candidate)
        {
            if (candidate?.Room == null || !candidate.Room.IsRequiredProgressionRoom)
            {
                return false;
            }

            return candidate.Anchor.Type == DungeonSemanticAnchorType.Lever
                || candidate.Anchor.Type == DungeonSemanticAnchorType.Condition;
        }

        private static int ComparePlacements(
            DungeonSemanticAnchorPlacement left,
            DungeonSemanticAnchorPlacement right)
        {
            var roomComparison = string.CompareOrdinal(
                left.Room.RoomId,
                right.Room.RoomId);

            return roomComparison != 0
                ? roomComparison
                : string.CompareOrdinal(left.Anchor.Id, right.Anchor.Id);
        }

        private static void Shuffle<T>(List<T> items, System.Random random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (items[index], items[swapIndex]) =
                    (items[swapIndex], items[index]);
            }
        }
    }
}
