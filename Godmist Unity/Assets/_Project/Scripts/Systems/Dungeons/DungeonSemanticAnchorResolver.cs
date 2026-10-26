using System;
using System.Collections.Generic;
using System.Linq;
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
            DungeonEncounterInstance encounter,
            bool normalizeLegacyData = true,
            Dictionary<string, Vector2Int> stationaryHomes = null)
        {
            var result = new Dictionary<string, Vector2Int>(
                StringComparer.Ordinal);

            if (floor == null
                || floor.Map == null
                || catalog == null
                || encounter == null
                || encounter.Enemies == null)
            {
                return result;
            }

            var liveEnemyIdsByIndex = new Dictionary<int, string>();

            for (var enemyIndex = 0;
                enemyIndex < encounter.Enemies.Count;
                enemyIndex++)
            {
                var enemy = encounter.Enemies[enemyIndex];

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
                _ => true, normalizeLegacyData);
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

                if (candidate.Anchor.PatrolMode == DungeonEnemyPatrolMode.Stationary)
                    stationaryHomes?.Add(enemyId, cell);

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
            DungeonRoomTemplateCatalog catalog,
            bool normalizeLegacyData = true)
        {
            var result = new HashSet<Vector2Int>();

            if (floor == null || catalog == null)
            {
                return result;
            }

            foreach (var encounter in floor.Encounters)
            {
                foreach (var cell in Resolve(floor, catalog, encounter, normalizeLegacyData).Values)
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

    // A selection opportunity is a single anchor or an atomic authored group.
    // Placement feasibility is evaluated before its one probability decision.
    public sealed class DungeonContentOpportunity
    {
        public string Id { get; }
        public string Category { get; }
        public string DistributionKey { get; }
        public float SpawnChance { get; }
        public bool IsRequired { get; internal set; }
        public List<DungeonSemanticAnchorPlacement> Anchors { get; } = new();
        public List<DungeonContentOpportunity> Dependencies { get; } = new();

        public DungeonContentOpportunity(string id, string category, float chance, bool required, string distributionKey = null)
        {
            DistributionKey = distributionKey;
            Id = id;
            Category = category;
            SpawnChance = chance;
            IsRequired = required;
        }
    }

    public static class DungeonRoomContentPlanner
    {
        public static void ValidateTemplate(DungeonRoomTemplateDefinition template)
        {
            var anchors = new Dictionary<string, DungeonSemanticAnchorData>(StringComparer.Ordinal);
            foreach (var anchor in template.SemanticAnchors ?? new())
            {
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.Id)
                    || !anchors.TryAdd(anchor.Id, anchor))
                    throw Error(template, "Pusta lub powtorzona kotwa.");
                if (!Enum.IsDefined(typeof(DungeonSemanticAnchorType), anchor.Type)
                    || float.IsNaN(anchor.SpawnChance) || anchor.SpawnChance < 0 || anchor.SpawnChance > 1
                    || anchor.AreaSize.x <= 0 || anchor.AreaSize.y <= 0)
                    throw Error(template, $"Kotwa '{anchor.Id}' ma nieprawidlowy typ, obszar lub szanse.");
            }
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            var membership = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in template.ContentGroups ?? new())
            {
                if (group == null || string.IsNullOrWhiteSpace(group.Id) || !groupIds.Add(group.Id)
                    || group.AnchorIds == null || group.AnchorIds.Count == 0
                    || float.IsNaN(group.SpawnChance) || group.SpawnChance < 0 || group.SpawnChance > 1)
                    throw Error(template, "Grupa musi miec unikalne Id, kotwy i szanse 0-1.");
                ValidateDistributionKey(template, "grupa '" + group.Id + "'", group.IsRequired, group.SpawnChance, group.DistributionKey);
                foreach (var id in group.AnchorIds)
                {
                    if (string.IsNullOrWhiteSpace(id) || !anchors.TryGetValue(id, out var anchor)
                        || anchor.Type == DungeonSemanticAnchorType.Enemy || !membership.TryAdd(id, group.Id))
                        throw Error(template, $"Grupa '{group.Id}': nieprawidlowa lub wielokrotnie przypisana kotwa '{id}'.");
                    // Group settings are authoritative; conflicting per-anchor settings are authoring errors.
                    if (anchor.IsRequired || anchor.SpawnChance != 1f)
                        throw Error(template, $"Grupa '{group.Id}', kotwa '{id}': ustaw Required=false i Chance=1 na kotwie; konfiguruj grupe.");
                }
            }
            foreach (var anchor in anchors.Values)
            {
                if (anchor.Type != DungeonSemanticAnchorType.Enemy && !membership.ContainsKey(anchor.Id))
                    ValidateDistributionKey(template, "kotwa '" + anchor.Id + "'", anchor.IsRequired, anchor.SpawnChance, anchor.DistributionKey);
                var visiting = new HashSet<string>(StringComparer.Ordinal) { anchor.Id };
                var current = anchor;
                while (!string.IsNullOrWhiteSpace(current.RequiresAnchorId))
                {
                    if (!anchors.TryGetValue(current.RequiresAnchorId, out current)
                        || current.Type == DungeonSemanticAnchorType.Enemy
                        || !visiting.Add(current.Id))
                        throw Error(template, $"Kotwa '{anchor.Id}': brak zaleznosci, zaleznosc od Enemy lub cykl.");
                }
            }
            // Contracting groups can introduce cycles even when the anchor graph is acyclic.
            var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            string Unit(string id) => membership.TryGetValue(id, out var group) ? "group:" + group : "anchor:" + id;
            foreach (var anchor in anchors.Values)
            {
                var key = Unit(anchor.Id);
                if (!edges.ContainsKey(key)) edges.Add(key, new HashSet<string>(StringComparer.Ordinal));
                if (!string.IsNullOrWhiteSpace(anchor.RequiresAnchorId) && key != Unit(anchor.RequiresAnchorId))
                    edges[key].Add(Unit(anchor.RequiresAnchorId));
            }
            var done = new HashSet<string>();
            var active = new HashSet<string>();
            void Visit(string id)
            {
                if (done.Contains(id)) return;
                if (!active.Add(id)) throw Error(template, $"Cykl zaleznosci grup przy '{id}'.");
                foreach (var dependency in edges[id]) Visit(dependency);
                active.Remove(id);
                done.Add(id);
            }
            foreach (var key in edges.Keys) Visit(key);
            DungeonDistributionValidator.Validate(new[] { template });
        }

        public static List<DungeonContentOpportunity> Build(
            DungeonRoomTemplateCatalog catalog, IReadOnlyList<DungeonSemanticAnchorPlacement> candidates)
        {
            var result = new List<DungeonContentOpportunity>();
            foreach (var roomCandidates in candidates.GroupBy(c => c.Room.RoomId))
            {
                var room = roomCandidates.First().Room;
                var template = catalog.AllTemplates.Single(t => t != null && t.Id == room.TemplateId);
                ValidateTemplate(template);
                var byAnchor = new Dictionary<string, DungeonContentOpportunity>(StringComparer.Ordinal);
                var byId = roomCandidates.ToDictionary(c => c.Anchor.Id, StringComparer.Ordinal);
                foreach (var group in template.ContentGroups ?? new())
                {
                    var members = group.AnchorIds.Select(id => byId[id]).OrderBy(c => c.Anchor.Id, StringComparer.Ordinal).ToList();
                    var category = string.Join("+", members.Select(c => c.Anchor.Type.ToString()).Distinct().OrderBy(s => s, StringComparer.Ordinal));
                    var unit = new DungeonContentOpportunity(room.RoomId + "/group:" + group.Id,
                        category, group.SpawnChance, group.IsRequired, group.DistributionKey);
                    unit.Anchors.AddRange(members);
                    result.Add(unit);
                    foreach (var member in members) byAnchor.Add(member.Anchor.Id, unit);
                }
                foreach (var candidate in roomCandidates)
                {
                    if (byAnchor.ContainsKey(candidate.Anchor.Id)) continue;
                    var unit = new DungeonContentOpportunity(room.RoomId + "/anchor:" + candidate.Anchor.Id,
                        candidate.Anchor.Type.ToString(), candidate.Anchor.SpawnChance, candidate.Anchor.IsRequired, candidate.Anchor.DistributionKey);
                    unit.Anchors.Add(candidate);
                    result.Add(unit);
                    byAnchor.Add(candidate.Anchor.Id, unit);
                }
                foreach (var candidate in roomCandidates)
                {
                    var unit = byAnchor[candidate.Anchor.Id];
                    if (room.IsRequiredProgressionRoom && (candidate.Anchor.Type == DungeonSemanticAnchorType.Lever
                        || candidate.Anchor.Type == DungeonSemanticAnchorType.Condition)) unit.IsRequired = true;
                    if (string.IsNullOrWhiteSpace(candidate.Anchor.RequiresAnchorId)) continue;
                    var dependency = byAnchor[candidate.Anchor.RequiresAnchorId];
                    if (dependency != unit && !unit.Dependencies.Contains(dependency)) unit.Dependencies.Add(dependency);
                }
            }
            var sorted = new List<DungeonContentOpportunity>();
            var visited = new HashSet<DungeonContentOpportunity>();
            void Visit(DungeonContentOpportunity unit)
            {
                if (!visited.Add(unit)) return;
                foreach (var dependency in unit.Dependencies.OrderBy(d => d.Id, StringComparer.Ordinal)) Visit(dependency);
                sorted.Add(unit);
            }
            foreach (var unit in result.OrderBy(u => u.Id, StringComparer.Ordinal)) Visit(unit);
            void Require(DungeonContentOpportunity unit)
            {
                unit.IsRequired = true;
                foreach (var dependency in unit.Dependencies) Require(dependency);
            }
            foreach (var unit in sorted.Where(u => u.IsRequired).ToList()) Require(unit);
            return sorted;
        }

        private static void ValidateDistributionKey(DungeonRoomTemplateDefinition template, string owner,
            bool required, float chance, string key)
        {
            if (!required && chance > 0 && chance < 1
                && (string.IsNullOrWhiteSpace(key) || key != key.Trim()))
                throw Error(template, owner + ": podaj klucz rozkladu bez bialych znakow na poczatku i koncu.");
        }

        public static void ValidateRequiredSpace(DungeonRoomTemplateDefinition template)
        {
            ValidateTemplate(template);
            var anchors = template.SemanticAnchors ?? new();
            var required = new HashSet<string>(anchors.Where(a => a.IsRequired
                || template.CanBeRequiredProgressionBranch && (a.Type == DungeonSemanticAnchorType.Lever
                    || a.Type == DungeonSemanticAnchorType.Condition)).Select(a => a.Id), StringComparer.Ordinal);
            var previousCount = -1;
            while (previousCount != required.Count)
            {
                previousCount = required.Count;
                foreach (var group in template.ContentGroups ?? new())
                    if (group.IsRequired || group.AnchorIds.Any(required.Contains)) required.UnionWith(group.AnchorIds);
                foreach (var anchor in anchors)
                    if (required.Contains(anchor.Id) && !string.IsNullOrWhiteSpace(anchor.RequiresAnchorId))
                        required.Add(anchor.RequiresAnchorId);
            }
            var placements = new List<DungeonSemanticAnchorPlacement>();
            var reserved = new HashSet<Vector2Int>();
            foreach (var anchor in anchors)
            {
                var cells = new List<Vector2Int>();
                for (var x = 0; x < anchor.AreaSize.x; x++)
                    for (var y = 0; y < anchor.AreaSize.y; y++) cells.Add(anchor.LocalCell + new Vector2Int(x, y));
                if (anchor.Type == DungeonSemanticAnchorType.Enemy) reserved.UnionWith(cells);
                else if (required.Contains(anchor.Id)) placements.Add(new DungeonSemanticAnchorPlacement(
                    null, anchor, new RectInt(anchor.LocalCell, anchor.AreaSize), cells));
            }
            if (!TryAssign(placements, reserved, out _))
                throw Error(template, "Brak miejsca na komplet wymaganych kotew: " + string.Join(", ", required.OrderBy(s => s, StringComparer.Ordinal)));
        }

        // Bipartite matching avoids greedy failures for overlapping authored areas.
        public static bool TryAssign(IReadOnlyList<DungeonSemanticAnchorPlacement> anchors,
            HashSet<Vector2Int> reserved, out Dictionary<DungeonSemanticAnchorPlacement, Vector2Int> positions)
        {
            var occupants = new Dictionary<Vector2Int, DungeonSemanticAnchorPlacement>();
            bool Assign(DungeonSemanticAnchorPlacement anchor, HashSet<Vector2Int> visited)
            {
                foreach (var cell in anchor.Cells.OrderBy(c => c.y).ThenBy(c => c.x))
                {
                    if (reserved.Contains(cell) || !visited.Add(cell)) continue;
                    if (occupants.TryGetValue(cell, out var previous) && !Assign(previous, visited)) continue;
                    occupants[cell] = anchor;
                    return true;
                }
                return false;
            }
            foreach (var anchor in anchors)
            {
                if (Assign(anchor, new HashSet<Vector2Int>())) continue;
                positions = null;
                return false;
            }
            positions = occupants.ToDictionary(p => p.Value, p => p.Key);
            return true;
        }

        private static InvalidOperationException Error(DungeonRoomTemplateDefinition template, string message)
            => new($"Szablon '{template.Id}': {message}");
    }

    public class DungeonSemanticAnchorResolver
    {
        public List<DungeonSemanticAnchorPlacement> GetCandidates(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog,
            IReadOnlyCollection<DungeonSemanticAnchorType> types,
            Func<DungeonRoomType, bool> roomTypeFilter,
            bool normalizeLegacyData = true)
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

                    if (normalizeLegacyData) anchor.Normalize();

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

            foreach (var template in catalog.AllTemplates)
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
