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
        private static DungeonFloorInstance GeneratePlannedFloor(DungeonDefinition definition,
            DungeonFloorRoutePlan plan, System.Random random, PassageLookup passages)
        {
            var failures = new Dictionary<string, int>();
            for (var attempt = 0; attempt < MaxFloorGenerationAttempts; attempt++)
            {
                try { return EmbedRoutePlan(definition, plan, random, passages); }
                catch (LayoutAttemptFailedException error)
                { failures.TryGetValue(error.Message, out var count); failures[error.Message] = count + 1; }
            }
            throw new InvalidOperationException($"Cannot embed planned floor {plan.FloorIndex} without changing its graph: "
                + string.Join("; ", failures));
        }

        private static DungeonFloorInstance EmbedRoutePlan(DungeonDefinition definition, DungeonFloorRoutePlan plan,
            System.Random random, PassageLookup passages)
        {
            if (definition.RoomTemplateCatalog.UseRoutedConnections)
                return EmbedRoutedPlan(definition, plan, random, passages);
            var floor = new DungeonFloorInstance(plan.FloorIndex);
            var areas = new List<RectInt>();
            var templates = new Dictionary<string, DungeonRoomTemplateDefinition>();
            var placed = new Dictionary<string, PlacedRoom>();
            var placementOrder = new List<string>();
            var nodes = plan.Nodes.ToDictionary(n => n.Id);
            var neighbours = plan.Nodes.ToDictionary(n => n.Id, _ => new List<string>());
            foreach (var edge in plan.Edges) { neighbours[edge.First].Add(edge.Second); neighbours[edge.Second].Add(edge.First); }
            void Commit(PlacedRoom room, DungeonRoomTemplateDefinition template)
            {
                var expected = nodes[room.Room.RoomId];
                if (expected.Transit != room.Room.IsBranchTransitRoom || expected.Required != room.Room.IsRequiredProgressionRoom
                    || (room.Room.RoomType != expected.Type && !(expected.IsLoopRoom && room.Room.RoomType == DungeonRoomType.Corridor)))
                    throw new InvalidOperationException("Embedding changed a planned room role.");
                placed.Add(room.Room.RoomId, room); placementOrder.Add(room.Room.RoomId);
                templates.Add(room.Room.RoomId, template); areas.Add(new RectInt(room.Room.LayoutPosition, room.Size));
            }
            IEnumerable<(PlacedRoom room, DungeonRoomTemplateDefinition template)> SingleCandidates(string id, PlacedRoom? previous)
            {
                var node = nodes[id];
                for (var trial = 0; trial < 16; trial++)
                {
                    var template = SelectTemplate(definition.RoomTemplateCatalog, node.Type, random,
                        node.Type == DungeonRoomType.Optional ? node.Required : (bool?)null);
                    foreach (var rotation in GetRotationCandidates(template, random))
                    {
                        if (!HasEnoughExitDirections(template, rotation, neighbours[id].Count,
                                definition.LayoutProfile.MinDoorwayWidth, passages)) continue;
                        var size = GetRotatedSize(template, rotation);
                        var position = Vector2Int.zero;
                        if (previous.HasValue && !TryFindOptionalRoomPosition(new List<PlacedRoom> { previous.Value },
                                size, template, rotation, templates, definition.LayoutProfile, areas, random,
                                passages, out _, out position)) continue;
                        var room = new PlacedRoom(new DungeonRoomInstance(id, node.Type, template.Id, position,
                            node.Required, rotation, node.Transit), size);
                        yield return (room, template);
                    }
                }
            }
            PlacedRoom PlaceSingle(string id, PlacedRoom? previous)
            {
                foreach (var candidate in SingleCandidates(id, previous))
                { Commit(candidate.room, candidate.template); return candidate.room; }
                throw new LayoutAttemptFailedException($"No placement for planned room {id} after {previous?.Room.RoomId} ({placed.Count} placed).");
            }
            void PlaceBranch(DungeonFloorRoutePlan.Branch branch)
            {
                // Keep candidate geometry provisional until the complete branch
                // fits. Backtrack locally instead of discarding a whole floor
                // when a long approach walks into a dead end.
                var remaining = 128;
                bool TryChain(int index, PlacedRoom previous)
                {
                    if (index == branch.Rooms.Count) return true;
                    foreach (var candidate in SingleCandidates(branch.Rooms[index], previous))
                    {
                        if (remaining-- <= 0) return false;
                        Commit(candidate.room, candidate.template);
                        if (TryChain(index + 1, candidate.room)) return true;
                        placed.Remove(candidate.room.Room.RoomId); templates.Remove(candidate.room.Room.RoomId);
                        placementOrder.RemoveAt(placementOrder.Count - 1); areas.RemoveAt(areas.Count - 1);
                    }
                    return false;
                }
                if (!TryChain(0, placed[branch.Root]))
                    throw new LayoutAttemptFailedException($"No complete placement for planned branch at {branch.Root}.");
            }
            var finishedBranches = new HashSet<string>();
            PlacedRoom? previousMain = null;
            foreach (var block in plan.MainBlocks)
            {
                if (block.Cycle == null) PlaceSingle(block.First, previousMain);
                else
                {
                    var loopRooms = PlacePlannedLoop(definition, block.Cycle, neighbours, placed,
                        templates, areas, previousMain, random, passages);
                    foreach (var pair in loopRooms) Commit(pair.room, pair.template);
                }
                previousMain = placed[block.Last];
                // The full graph already exists. Embedding branches as soon as
                // their junction exists keeps later blocks out of that space.
                foreach (var branch in plan.Branches)
                {
                    if (!placed.ContainsKey(branch.Root) || !finishedBranches.Add(branch.Root)) continue;
                    PlaceBranch(branch);
                }
            }
            foreach (var id in placementOrder) AddRoomOrThrow(floor, placed[id].Room);
            foreach (var edge in plan.Edges)
            {
                if (!floor.TryAddConnection(edge.First, edge.Second))
                    throw new InvalidOperationException("Planned edge could not be instantiated.");
                if (edge.GateIndex >= 0 && !floor.Connections[floor.Connections.Count - 1].TrySetProgressionGate(
                        $"floor.{plan.FloorIndex}.gate.{edge.GateIndex:00}",
                        $"floor.{plan.FloorIndex}.condition.{edge.GateIndex:00}", edge.ControlRoomId))
                    throw new InvalidOperationException("Planned progression gate could not be instantiated.");
            }
            if (floor.Rooms.Count != plan.Nodes.Count || floor.Connections.Count != plan.Edges.Count)
                throw new InvalidOperationException("Embedding changed the planned graph.");
            BuildConnectionGeometry(floor, templates, definition.LayoutProfile, random, passages);
            floor.TrySetMap(new DungeonFloorMapBuilder().Build(floor, definition.RoomTemplateCatalog));
            ValidateForestTransitionBoundaries(definition, floor);
            ValidateFloorReachability(floor); ValidateProgressionGateSequence(floor);
            return floor;
        }

        private static void ValidateForestTransitionBoundaries(DungeonDefinition definition, DungeonFloorInstance floor)
        {
            if (!definition.RoomTemplateCatalog.HasVegetationFringe) return;
            foreach (var room in floor.Rooms)
            {
                if (room.RoomType != DungeonRoomType.Final
                    && !(room.RoomType == DungeonRoomType.Start && floor.FloorIndex > 0)) continue;
                if (!DungeonForestTransitionBoundary.TryResolve(floor, room.RoomId, out _, out _))
                    throw new LayoutAttemptFailedException("Forest transition room has no outside-facing boundary.");
            }
        }

        private static Vector2Int GetRotatedSize(DungeonRoomTemplateDefinition template, int rotation)
            => DungeonRoomTransformUtility.GetRotatedSize(template.SizeInCells, rotation);

        private static bool HasEnoughExitDirections(DungeonRoomTemplateDefinition template, int rotation,
            int count, int width, PassageLookup passages)
        {
            var available = 0;
            foreach (LayoutDirection direction in Enum.GetValues(typeof(LayoutDirection)))
            {
                var cells = passages.Get(template, rotation, ToEdgeDirection(direction));
                var lateral = direction == LayoutDirection.Left || direction == LayoutDirection.Right ? Vector2Int.up : Vector2Int.right;
                if (cells.Any(start => Enumerable.Range(0, width).All(i => cells.Contains((start.cell + lateral * i, start.level))))) available++;
            }
            return available >= count;
        }

        private static List<(PlacedRoom room, DungeonRoomTemplateDefinition template)> PlacePlannedLoop(
            DungeonDefinition definition, DungeonFloorRoutePlan.Loop loop,
            Dictionary<string, List<string>> neighbours,
            Dictionary<string, PlacedRoom> existing, Dictionary<string, DungeonRoomTemplateDefinition> templates,
            List<RectInt> areas, PlacedRoom? previous, System.Random random, PassageLookup passages)
        {
            var families = new Dictionary<Vector2Int, List<(DungeonRoomTemplateDefinition template, int rotation)>>();
            foreach (var template in definition.RoomTemplateCatalog.Templates)
            {
                if (template == null || (template.RoomType != DungeonRoomType.Normal && template.RoomType != DungeonRoomType.Corridor)) continue;
                foreach (var rotation in GetRotationCandidates(template, random))
                {
                    var size = GetRotatedSize(template, rotation);
                    if (!families.TryGetValue(size, out var family)) families.Add(size, family = new());
                    family.Add((template, rotation));
                }
            }
            // Compatible sizes keep the existing authored direct passages.
            // A closed walk determines geometry, not the two rectangular Ls
            // previously used to lay out equal arms.
            var ranked = families.OrderBy(_ => random.Next()).OrderByDescending(f => f.Value.Select(v => v.template).Distinct().Count()).ToArray();
            Vector2Int Rotate(Vector2Int point, int turn) => turn switch
            { 1 => new(-point.y, point.x), 2 => -point, 3 => new(point.y, -point.x), _ => point };
            var order = new List<string> { loop.Fork }; order.AddRange(loop.FirstArm); order.Add(loop.Join); order.AddRange(loop.SecondArm);
            var directions = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            for (var shapeAttempt = 0; shapeAttempt < 16; shapeAttempt++)
            {
                var ring = CreateLoopShape(order.Count, random);
                var start = random.Next(ring.Count);
                var clockwise = random.Next(2) == 0 ? 1 : -1;
                Vector2Int Point(int index) => ring[(start + clockwise * index + 2 * ring.Count) % ring.Count] - ring[start];
                var local = new Dictionary<string, Vector2Int> { [loop.Fork] = Vector2Int.zero,
                    [loop.Join] = Point(loop.FirstArm.Count + 1) };
                for (var i = 0; i < loop.FirstArm.Count; i++) local.Add(loop.FirstArm[i], Point(i + 1));
                for (var i = 0; i < loop.SecondArm.Count; i++) local.Add(loop.SecondArm[i], Point(-i - 1));
                var occupied = new HashSet<Vector2Int>(local.Values);
                // An empty site inside the ring is not an escape direction for
                // a main route or a long branch. Count exterior space only.
                var bounds = new RectInt(occupied.Min(p => p.x) - 1, occupied.Min(p => p.y) - 1,
                    occupied.Max(p => p.x) - occupied.Min(p => p.x) + 3,
                    occupied.Max(p => p.y) - occupied.Min(p => p.y) + 3);
                var outside = new HashSet<Vector2Int> { bounds.min };
                var queue = new Queue<Vector2Int>(); queue.Enqueue(bounds.min);
                while (queue.Count > 0)
                {
                    var point = queue.Dequeue();
                    foreach (var direction in directions)
                    {
                        var next = point + direction;
                        if (bounds.Contains(next) && !occupied.Contains(next) && outside.Add(next)) queue.Enqueue(next);
                    }
                }
                if (local.Any(p => directions.Count(d => outside.Contains(p.Value + d)) < neighbours[p.Key].Count - 2)) continue;
                foreach (var family in ranked)
                foreach (var turn in Enumerable.Range(0, 4).OrderBy(_ => random.Next()))
                foreach (var localIncoming in directions.Where(d => outside.Contains(-d)).OrderBy(_ => random.Next()))
                {
                    var offset = Rotate(localIncoming, turn);
                    var direction = offset.x > 0 ? LayoutDirection.Right : offset.x < 0 ? LayoutDirection.Left
                        : offset.y > 0 ? LayoutDirection.Up : LayoutDirection.Down;
                    var unit = family.Key;
                    var laterals = previous.HasValue ? GetLateralOffsets(previous.Value.Size, unit, direction, definition.LayoutProfile, random) : new List<int> { 0 };
                    foreach (var lateral in laterals)
                    {
                        var origin = previous.HasValue ? GetConnectedPosition(previous.Value.Room.LayoutPosition,
                            previous.Value.Size, unit, direction, lateral) : Vector2Int.zero;
                        var positions = local.ToDictionary(p => p.Key, p => origin + Vector2Int.Scale(Rotate(p.Value, turn), unit));
                        if (positions.Any(p => !IsAreaFree(p.Value, unit, areas))) continue;
                        for (var trial = 0; trial < 6; trial++)
                        {
                            var selected = new Dictionary<string, (PlacedRoom room, DungeonRoomTemplateDefinition template)>();
                            foreach (var id in order)
                            {
                                var found = false;
                                foreach (var choice in family.Value.OrderBy(_ => random.Next()))
                                {
                                    if (!HasEnoughExitDirections(choice.template, choice.rotation, neighbours[id].Count,
                                            definition.LayoutProfile.MinDoorwayWidth, passages)) continue;
                                    var candidate = new PlacedRoom(new DungeonRoomInstance(id, choice.template.RoomType,
                                        choice.template.Id, positions[id], rotationDegrees: choice.rotation), unit);
                                    var compatible = true;
                                    foreach (var other in neighbours[id])
                                    {
                                        if (selected.TryGetValue(other, out var selectedOther))
                                            compatible = HavePlannedPassage(candidate, choice.template, selectedOther.room,
                                                selectedOther.template, definition.LayoutProfile.MinDoorwayWidth, passages);
                                        else if (existing.TryGetValue(other, out var existingOther))
                                            compatible = HavePlannedPassage(candidate, choice.template, existingOther,
                                                templates[other], definition.LayoutProfile.MinDoorwayWidth, passages);
                                        else if (positions.TryGetValue(other, out var nextPosition))
                                            compatible = family.Value.Any(next => HavePlannedPassage(candidate, choice.template,
                                                new PlacedRoom(new DungeonRoomInstance(other, next.template.RoomType, next.template.Id,
                                                    nextPosition, rotationDegrees: next.rotation), unit), next.template,
                                                definition.LayoutProfile.MinDoorwayWidth, passages));
                                        if (!compatible) break;
                                    }
                                    if (!compatible) continue;
                                    selected.Add(id, (candidate, choice.template)); found = true; break;
                                }
                                if (!found) break;
                            }
                            if (selected.Count == order.Count) return order.Select(id => selected[id]).ToList();
                        }
                    }
                }
            }
            throw new LayoutAttemptFailedException("No geometry for the complete planned cycle.");
        }

        // Exact, simple, orthogonal cycle. Moving a corner across its diagonal
        // preserves every planned room/edge while introducing recesses and bends.
        // Unit edges cannot cross without sharing a vertex, which is forbidden.
        private static List<Vector2Int> CreateLoopShape(int count, System.Random random)
        {
            if (count < 6 || count % 2 != 0) throw new ArgumentException("A grid loop needs an even room count >= 6.");
            var half = count / 2;
            var widths = Enumerable.Range(2, Math.Max(1, half - 3))
                .Where(w => Math.Max(w, half - w) <= 2 * Math.Min(w, half - w)).ToArray();
            for (var attempt = 0; attempt < 64; attempt++)
            {
                var width = count == 6 ? 2 : widths[random.Next(widths.Length)];
                var height = half - width;
                var twiceArea = 2 * width * height;
                var minimumArea = width * height - Math.Max(1, count / 6);
                var ring = new List<Vector2Int>(count);
                for (var x = 0; x < width; x++) ring.Add(new Vector2Int(x, 0));
                for (var y = 0; y < height; y++) ring.Add(new Vector2Int(width, y));
                for (var x = width; x > 0; x--) ring.Add(new Vector2Int(x, height));
                for (var y = height; y > 0; y--) ring.Add(new Vector2Int(0, y));
                var occupied = new HashSet<Vector2Int>(ring);
                for (var step = 0; step < count * 4; step++)
                {
                    var index = random.Next(count);
                    var previous = ring[(index + count - 1) % count];
                    var next = ring[(index + 1) % count];
                    var diagonal = next - previous;
                    if (Math.Abs(diagonal.x) != 1 || Math.Abs(diagonal.y) != 1) continue;
                    var alternative = previous + next - ring[index];
                    if (occupied.Contains(alternative)) continue;
                    int Cross(Vector2Int a, Vector2Int b) => a.x * b.y - a.y * b.x;
                    var areaChange = Cross(previous, alternative) + Cross(alternative, next)
                        - Cross(previous, ring[index]) - Cross(ring[index], next);
                    // Keep the central void readable instead of folding both
                    // routes into a dense two-row ribbon of touching rooms.
                    if (twiceArea + areaChange < 2 * minimumArea) continue;
                    twiceArea += areaChange;
                    occupied.Remove(ring[index]); occupied.Add(alternative); ring[index] = alternative;
                }
                var turns = 0;
                for (var i = 0; i < count; i++)
                    if (ring[i] - ring[(i + count - 1) % count] != ring[(i + 1) % count] - ring[i]) turns++;
                // Six-room cycles cannot have a nonrectangular shape on this
                // direct-passage grid. Larger cycles must have at least a recess.
                if (count == 6 || turns >= (count >= 12 ? 8 : 6)) return ring;
            }
            throw new LayoutAttemptFailedException("Could not construct an irregular closed walk.");
        }

        private static bool HavePlannedPassage(PlacedRoom first, DungeonRoomTemplateDefinition firstTemplate,
            PlacedRoom second, DungeonRoomTemplateDefinition secondTemplate, int width, PassageLookup passages)
        {
            var a = new RectInt(first.Room.LayoutPosition, first.Size); var b = new RectInt(second.Room.LayoutPosition, second.Size);
            LayoutDirection direction;
            if (a.xMax == b.xMin) direction = LayoutDirection.Right;
            else if (a.xMin == b.xMax) direction = LayoutDirection.Left;
            else if (a.yMax == b.yMin) direction = LayoutDirection.Up;
            else if (a.yMin == b.yMax) direction = LayoutDirection.Down;
            else return false;
            return HasCompatiblePassage(a.position, a.size, firstTemplate, first.Room.RotationDegrees,
                b.position, b.size, secondTemplate, second.Room.RotationDegrees, direction, width, passages);
        }
    }
}
