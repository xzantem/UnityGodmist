using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public partial class DungeonGeneratorService
    {
        // Slots reserve space, not room shapes. Select the body only after all
        // incident routes are known; corridors absorb different sizes/offsets.
        private static DungeonFloorInstance EmbedRoutedPlan(DungeonDefinition definition,
            DungeonFloorRoutePlan plan, System.Random random, PassageLookup passages)
        {
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            var sites = new Dictionary<string, Vector2Int>();
            var occupied = new HashSet<Vector2Int>();
            var neighbours = plan.Nodes.ToDictionary(n => n.Id, _ => new List<string>());
            foreach (var edge in plan.Edges) { neighbours[edge.First].Add(edge.Second); neighbours[edge.Second].Add(edge.First); }
            void Add(string id, Vector2Int point) { sites.Add(id, point); occupied.Add(point); }
            var finished = new HashSet<string>();
            string previous = null;
            foreach (var block in plan.MainBlocks)
            {
                if (block.Cycle == null)
                {
                    if (previous == null) Add(block.First, Vector2Int.zero);
                    else
                    {
                        var available = directions.Where(d => !occupied.Contains(sites[previous] + d))
                            .OrderBy(_ => random.Next()).OrderByDescending(d => directions.Count(v => !occupied.Contains(sites[previous] + d + v))).ToArray();
                        if (available.Length == 0) throw new LayoutAttemptFailedException("Routed main route has no free slot.");
                        Add(block.First, sites[previous] + available[0]);
                    }
                }
                else
                {
                    var loop = block.Cycle;
                    var ids = new List<string> { loop.Fork }; ids.AddRange(loop.FirstArm); ids.Add(loop.Join);
                    ids.AddRange(loop.SecondArm.Reverse());
                    var found = false;
                    for (var attempt = 0; attempt < 64 && !found; attempt++)
                    {
                        var ring = CreateLoopShape(ids.Count, random);
                        var start = random.Next(ring.Count);
                        var sign = random.Next(2) == 0 ? -1 : 1;
                        var relative = Enumerable.Range(0, ids.Count).Select(i =>
                            ring[(start + sign * i + ring.Count) % ring.Count] - ring[start]).ToArray();
                        foreach (var incoming in directions.OrderBy(_ => random.Next()))
                        {
                            var origin = previous == null ? Vector2Int.zero : sites[previous] + incoming;
                            var points = relative.Select(p => origin + p).ToArray();
                            var proposed = points.ToHashSet();
                            if (points.Any(occupied.Contains)) continue;
                            // External routes must be able to leave the ring,
                            // rather than being sent into its enclosed pocket.
                            var min = new Vector2Int(proposed.Min(p => p.x) - 1, proposed.Min(p => p.y) - 1);
                            var max = new Vector2Int(proposed.Max(p => p.x) + 1, proposed.Max(p => p.y) + 1);
                            var exterior = new HashSet<Vector2Int> { min };
                            var queue = new Queue<Vector2Int>(); queue.Enqueue(min);
                            while (queue.Count > 0)
                            {
                                var cell = queue.Dequeue();
                                foreach (var d in directions)
                                {
                                    var next = cell + d;
                                    if (next.x >= min.x && next.x <= max.x && next.y >= min.y && next.y <= max.y
                                        && !proposed.Contains(next) && !occupied.Contains(next) && exterior.Add(next)) queue.Enqueue(next);
                                }
                            }
                            if (Enumerable.Range(0, ids.Count).Any(i =>
                                directions.Count(d => exterior.Contains(points[i] + d)) < neighbours[ids[i]].Count - 2
                                    - (i == 0 && previous != null ? 1 : 0))) continue;
                            for (var i = 0; i < ids.Count; i++) Add(ids[i], points[i]);
                            found = true; break;
                        }
                    }
                    if (!found) throw new LayoutAttemptFailedException("Routed loop has no reserved footprint.");
                }
                previous = block.Last;
                foreach (var branch in plan.Branches)
                {
                    if (!sites.ContainsKey(branch.Root) || !finished.Add(branch.Root)) continue;
                    var budget = 256;
                    bool Chain(int index, Vector2Int from)
                    {
                        if (index == branch.Rooms.Count) return true;
                        foreach (var d in directions.OrderBy(_ => random.Next()))
                        {
                            var point = from + d;
                            if (occupied.Contains(point)) continue;
                            if (--budget < 0) return false;
                            Add(branch.Rooms[index], point);
                            if (Chain(index + 1, point)) return true;
                            sites.Remove(branch.Rooms[index]); occupied.Remove(point);
                        }
                        return false;
                    }
                    if (!Chain(0, sites[branch.Root])) throw new LayoutAttemptFailedException("Routed branch has no complete footprint.");
                }
            }
            var forest = definition.RoomTemplateCatalog.HasVegetationFringe;
            var width = forest ? 3 : 2;
            var floor = new DungeonFloorInstance(plan.FloorIndex);
            var selected = new Dictionary<string, (DungeonRoomTemplateDefinition template, int rotation)>();
            var sizes = new Dictionary<string, Vector2Int>();
            var drifts = new Dictionary<string, Vector2Int>();
            var uses = new Dictionary<string, int>();
            foreach (var node in plan.Nodes)
            {
                var required = neighbours[node.Id].Select(n => sites[n] - sites[node.Id]).ToArray();
                var candidates = new List<(DungeonRoomTemplateDefinition template, int rotation)>();
                foreach (var template in definition.RoomTemplateCatalog.GenerationTemplates)
                {
                    if (template == null || template.RoomType != node.Type
                        || (node.Type == DungeonRoomType.Optional && template.CanBeRequiredProgressionBranch != node.Required)) continue;
                    foreach (var rotation in GetRotationCandidates(template, random))
                        if (required.All(d => GetPort(template, rotation, d, width, passages).HasValue)) candidates.Add((template, rotation));
                }
                if (candidates.Count == 0) throw new LayoutAttemptFailedException($"No authored ports for {node.Type} ({required.Length} exits).");
                // Different functions and scales should not repeat in adjacent
                // rooms, or lose the selection lottery throughout a whole floor.
                var choice = candidates.OrderBy(_ => random.Next()).OrderBy(c =>
                    (uses.TryGetValue(c.template.Id, out var count) ? count : 0)
                    + (c.template.PreferredFloorIndex >= 0 && c.template.PreferredFloorIndex != plan.FloorIndex ? 3 : 0)
                    + neighbours[node.Id].Count(n => selected.TryGetValue(n, out var previousChoice)
                        && previousChoice.template == c.template) * 4
                    + neighbours[node.Id].Count(n => selected.TryGetValue(n, out var previousChoice)
                        && previousChoice.template.RoomFunction == c.template.RoomFunction) * 2).First();
                selected.Add(node.Id, choice);
                uses.TryGetValue(choice.template.Id, out var used); uses[choice.template.Id] = used + 1;
                var size = GetRotatedSize(choice.template, choice.rotation);
                if (size.x > 20 || size.y > 20) throw new InvalidOperationException("Production room exceeds reserved slot size.");
                sizes.Add(node.Id, size);
                drifts.Add(node.Id, forest ? new Vector2Int(random.Next(-2, 3), random.Next(-2, 3)) : Vector2Int.zero);
            }
            // Spacing must not change the semantic template-selection stream
            // on later floors (or consequently the encounter candidate pool).
            // Derive a separate deterministic stream from the embedded plan.
            var geometrySeed = 17;
            unchecked
            {
                foreach (var node in plan.Nodes)
                {
                    foreach (var letter in selected[node.Id].template.Id) geometrySeed = geometrySeed * 31 + letter;
                    geometrySeed = geometrySeed * 31 + sites[node.Id].x;
                    geometrySeed = geometrySeed * 31 + sites[node.Id].y;
                    geometrySeed = geometrySeed * 31 + drifts[node.Id].x * 5 + drifts[node.Id].y;
                }
            }
            var geometryRandom = new System.Random(geometrySeed);
            var alignment = plan.Nodes.ToDictionary(n => n.Id, _ => Vector2Int.zero);
            var xGroups = new Dictionary<string, string>();
            var yGroups = new Dictionary<string, string>();
            foreach (var horizontal in new[] { true, false })
            {
                // Align actual entrance bands, not the centres of bounding
                // boxes. Different ports on opposite sides let the sequence
                // drift naturally through a row/column without longer links.
                var aligned = new HashSet<string>();
                foreach (var node in plan.Nodes)
                {
                    if (!aligned.Add(node.Id)) continue;
                    var component = new List<string> { node.Id };
                    var pending = new Queue<string>(); pending.Enqueue(node.Id);
                    while (pending.Count > 0)
                    {
                        var current = pending.Dequeue();
                        foreach (var next in neighbours[current])
                        {
                            var direction = sites[next] - sites[current];
                            if (horizontal ? direction.y != 0 : direction.x != 0) continue;
                            var a = GetPort(selected[current].template, selected[current].rotation, direction, width, passages).Value - sizes[current] / 2;
                            var b = GetPort(selected[next].template, selected[next].rotation, -direction, width, passages).Value - sizes[next] / 2;
                            var offset = alignment[current] + a - b;
                            var value = horizontal ? offset.y : offset.x;
                            if (!aligned.Add(next))
                            {
                                if ((horizontal ? alignment[next].y : alignment[next].x) != value)
                                    throw new LayoutAttemptFailedException("Authored entrance alignment is inconsistent.");
                                continue;
                            }
                            alignment[next] = horizontal ? new Vector2Int(alignment[next].x, value) : new Vector2Int(value, alignment[next].y);
                            component.Add(next); pending.Enqueue(next);
                        }
                    }
                    var origin = (int)Math.Round(component.Average(id => horizontal ? alignment[id].y : alignment[id].x));
                    foreach (var id in component)
                    {
                        alignment[id] -= horizontal ? new Vector2Int(0, origin) : new Vector2Int(origin, 0);
                        (horizontal ? yGroups : xGroups).Add(id, node.Id);
                    }
                }
            }
            // Compact each row/column around its actual room bodies. A single
            // fixed pitch sized for the largest hall makes small crypts acquire
            // empty corridors as long as another room.
            Dictionary<int, int> Centers(bool horizontal)
            {
                int Low(string id) => horizontal ? alignment[id].x - sizes[id].x / 2 : alignment[id].y - sizes[id].y / 2;
                int High(string id) => Low(id) + (horizontal ? sizes[id].x : sizes[id].y);
                var extents = sites.GroupBy(p => horizontal ? p.Value.x : p.Value.y)
                    .ToDictionary(g => g.Key, g => (low: g.Min(p => Low(p.Key)), high: g.Max(p => High(p.Key))));
                var centers = new Dictionary<int, int>();
                var keys = extents.Keys.OrderBy(k => k).ToArray();
                var center = 0;
                for (var i = 0; i < keys.Length; i++)
                {
                    // A zero gap allows touching entrance bands. Most other
                    // gaps are short; occasional longer links retain breathing
                    // room without imposing that distance on every connection.
                    if (i > 0) center += extents[keys[i - 1]].high - extents[keys[i]].low
                        + SampleConnectorGap(geometryRandom, forest);
                    centers.Add(keys[i], center);
                }
                var origin = centers[0];
                return centers.ToDictionary(p => p.Key, p => p.Value - origin);
            }
            var columns = Centers(true); var rows = Centers(false);
            var positions = plan.Nodes.ToDictionary(n => n.Id, n =>
                new Vector2Int(columns[sites[n.Id].x], rows[sites[n.Id].y]) - sizes[n.Id] / 2 + alignment[n.Id]);
            CompactAuthoredLayout(plan, sites, sizes, alignment, xGroups, yGroups, positions, geometryRandom, forest);
            Vector2Int Entrance(string id, Vector2Int direction) => positions[id]
                + GetPort(selected[id].template, selected[id].rotation, direction, width, passages).Value;
            if (forest)
            {
                // Lateral drift is allowed only where the connector has room
                // for a proper bend. Keep touching/short entrances aligned;
                // forcing a bend there recreates the one-cell Tetris neck.
                foreach (var node in plan.Nodes)
                {
                    var drift = drifts[node.Id];
                    if (Math.Abs(drift.x) + Math.Abs(drift.y) > 2) continue;
                    var original = positions[node.Id];
                    var candidate = original + new Vector2Int(Math.Sign(drift.x), Math.Sign(drift.y));
                    positions[node.Id] = candidate;
                    var bounds = new RectInt(candidate, sizes[node.Id]);
                    var overlap = plan.Nodes.Any(n => n.Id != node.Id
                        && bounds.Overlaps(new RectInt(positions[n.Id], sizes[n.Id])));
                    if (overlap || neighbours[node.Id].Any(id =>
                    {
                        var normal = sites[id] - sites[node.Id];
                        var delta = Entrance(id, -normal) - Entrance(node.Id, normal);
                        var forward = delta.x * normal.x + delta.y * normal.y;
                        var lateral = normal.x != 0 ? delta.y : delta.x;
                        return forward < 1 || (lateral != 0 && forward - 1 < width * 2);
                    })) positions[node.Id] = original;
                }
            }
            foreach (var node in plan.Nodes)
            {
                var choice = selected[node.Id];
                if (plan.Nodes.Any(n => string.CompareOrdinal(n.Id, node.Id) < 0
                    && new RectInt(positions[node.Id], sizes[node.Id]).Overlaps(new RectInt(positions[n.Id], sizes[n.Id]))))
                    throw new LayoutAttemptFailedException("Organic room bounds overlap after compaction.");
                AddRoomOrThrow(floor, new DungeonRoomInstance(node.Id, node.Type, choice.template.Id, positions[node.Id],
                    node.Required, choice.rotation, node.Transit));
            }
            // Short gaps may bring a bend close to a neighbouring room. Reject
            // that embedding, rather than letting map construction fail or
            // carving an unintended shortcut through another room/corridor.
            var occupiedGeometry = new HashSet<Vector2Int>();
            foreach (var node in plan.Nodes)
                foreach (var cell in new RectInt(positions[node.Id], sizes[node.Id]).allPositionsWithin)
                    occupiedGeometry.Add(cell);
            foreach (var edge in plan.Edges)
            {
                floor.TryAddConnection(edge.First, edge.Second);
                var connection = floor.Connections[floor.Connections.Count - 1];
                var normal = sites[edge.Second] - sites[edge.First];
                var firstChoice = selected[edge.First]; var secondChoice = selected[edge.Second];
                floor.TryGetRoom(edge.First, out var firstRoom); floor.TryGetRoom(edge.Second, out var secondRoom);
                var first = firstRoom.LayoutPosition + GetPort(firstChoice.template, firstChoice.rotation, normal, width, passages).Value;
                var end = secondRoom.LayoutPosition + GetPort(secondChoice.template, secondChoice.rotation, -normal, width, passages).Value;
                var direct = end == first + normal;
                var footprint = direct ? null : BuildCorridor(first, end, normal, width);
                if (footprint != null && footprint.Any(c => !occupiedGeometry.Add(c)))
                    throw new LayoutAttemptFailedException("Compact connector intersects reserved geometry.");
                var configured = direct ? connection.TrySetDirectPassage(first, end, width)
                    : connection.TrySetRoutedPassage(first, first + normal, end, width, footprint);
                if (!configured)
                    throw new InvalidOperationException("Invalid routed corridor geometry.");
                if (edge.GateIndex >= 0 && !connection.TrySetProgressionGate($"floor.{plan.FloorIndex}.gate.{edge.GateIndex:00}",
                        $"floor.{plan.FloorIndex}.condition.{edge.GateIndex:00}", edge.ControlRoomId))
                    throw new InvalidOperationException("Invalid routed gate.");
            }
            floor.TrySetMap(new DungeonFloorMapBuilder().Build(floor, definition.RoomTemplateCatalog));
            ValidateForestTransitionBoundaries(definition, floor);
            ValidateFloorReachability(floor); ValidateProgressionGateSequence(floor);
            return floor;
        }

        private static int SampleConnectorGap(System.Random random, bool forest)
        {
            var roll = random.Next(100);
            if (roll < 35) return 0;
            if (roll < 70) return 1;
            if (roll < 90) return 2;
            return forest ? random.Next(3, 5) : 3;
        }

        private static Vector2Int? GetPort(DungeonRoomTemplateDefinition template, int rotation,
            Vector2Int normal, int width, PassageLookup passages)
        {
            var direction = normal.x > 0 ? DungeonEdgeDirection.Right : normal.x < 0 ? DungeonEdgeDirection.Left
                : normal.y > 0 ? DungeonEdgeDirection.Up : DungeonEdgeDirection.Down;
            var cells = passages.Get(template, rotation, direction);
            var lateral = normal.x != 0 ? Vector2Int.up : Vector2Int.right;
            foreach (var start in cells.OrderBy(c => c.cell.y).ThenBy(c => c.cell.x))
                if (start.level == 0 && Enumerable.Range(0, width).All(i => cells.Contains((start.cell + lateral * i, 0))))
                    return start.cell;
            return null;
        }

        private static HashSet<Vector2Int> BuildCorridor(Vector2Int first, Vector2Int end, Vector2Int normal, int width)
        {
            var span = end - first;
            if (span.x * normal.x + span.y * normal.y < 2)
                throw new InvalidOperationException("A non-direct connector needs space between entrance bands.");
            // A straight link can be shorter than its width. Square stamps
            // would intrude into the room for a one/two-cell-long connector.
            if (normal.x != 0 ? first.y == end.y : first.x == end.x)
            {
                var strip = new HashSet<Vector2Int>();
                var lateral = normal.x != 0 ? Vector2Int.up : Vector2Int.right;
                for (var point = first + normal; point != end; point += normal)
                    for (var i = 0; i < width; i++) strip.Add(point + lateral * i);
                return strip;
            }
            // Sweep a width-square along an orthogonal spine. Both entrance
            // bands retain their exact authored axis; an offset is absorbed in
            // the middle of the connector, never by a one-cell doorway kink.
            var from = first + normal * (normal.x < 0 || normal.y < 0 ? width : 1);
            var to = end - normal * (normal.x > 0 || normal.y > 0 ? width : 1);
            var middle = normal.x != 0 ? (from.x + to.x) / 2 : (from.y + to.y) / 2;
            var points = normal.x != 0
                ? new[] { from, new Vector2Int(middle, from.y), new Vector2Int(middle, to.y), to }
                : new[] { from, new Vector2Int(from.x, middle), new Vector2Int(to.x, middle), to };
            var result = new HashSet<Vector2Int>();
            void Stamp(Vector2Int point)
            { for (var y = 0; y < width; y++) for (var x = 0; x < width; x++) result.Add(point + new Vector2Int(x, y)); }
            for (var i = 1; i < points.Length; i++)
            {
                var point = points[i - 1]; Stamp(point);
                var delta = points[i] - point;
                var step = new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
                while (point != points[i]) { point += step; Stamp(point); }
            }
            return result;
        }
    }
}
