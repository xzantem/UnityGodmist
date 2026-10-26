using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Dungeons
{
    /// <summary>Generation-only pacing on the realised room graph; never changes its topology or combat budget.</summary>
    public static class DungeonEncounterRoomSelector
    {
        public static void Prioritize(DungeonFloorInstance floor, List<DungeonRoomInstance> candidates, int count, bool finalHasBoss)
        {
            if (count <= 0 || count >= candidates.Count || floor.Connections.Count == 0) return;
            var search = new Search(floor, candidates, finalHasBoss);
            search.Prioritize(candidates, count);
        }

        private sealed class Search
        {
            private const int PreferredEmptyRun = 4;
            private const int MaximumSwapPasses = 4;
            private readonly List<int>[] neighbours;
            private readonly int[,] distances;
            private readonly List<int[]> routes = new();
            private readonly bool[] occupied, fixedRooms, battleRooms;
            private readonly int[] candidateNodes, centres, nearestDistances;
            private readonly int size;

            public Search(DungeonFloorInstance floor, List<DungeonRoomInstance> candidates, bool finalHasBoss)
            {
                size = floor.Rooms.Count;
                neighbours = new List<int>[size]; occupied = new bool[size]; fixedRooms = new bool[size]; battleRooms = new bool[size]; centres = new int[size]; nearestDistances = new int[size];
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < size; i++)
                {
                    index.Add(floor.Rooms[i].RoomId, i); neighbours[i] = new List<int>();
                    // The entrance is a pacing boundary, not another encounter.
                    fixedRooms[i] = floor.Rooms[i].RoomType == DungeonRoomType.Start
                        || finalHasBoss && floor.Rooms[i].RoomType == DungeonRoomType.Final;
                    battleRooms[i] = finalHasBoss && floor.Rooms[i].RoomType == DungeonRoomType.Final;
                }
                foreach (var connection in floor.Connections)
                {
                    if (!index.TryGetValue(connection.FirstRoomId, out var a) || !index.TryGetValue(connection.SecondRoomId, out var b)) continue;
                    if (!neighbours[a].Contains(b)) { neighbours[a].Add(b); neighbours[b].Add(a); }
                }
                candidateNodes = new int[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    candidateNodes[i] = index[candidates[i].RoomId];
                    battleRooms[candidateNodes[i]] = true;
                    if (candidates[i].RoomType == DungeonRoomType.Final) fixedRooms[candidateNodes[i]] = true;
                }
                distances = new int[size, size];
                var queue = new Queue<int>(size);
                for (int source = 0; source < size; source++)
                {
                    for (int i = 0; i < size; i++) distances[source, i] = size;
                    queue.Clear(); queue.Enqueue(source); distances[source, source] = 0;
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        foreach (var next in neighbours[current])
                            if (distances[source, next] == size)
                            { distances[source, next] = distances[source, current] + 1; queue.Enqueue(next); }
                    }
                }
                BuildRoutes(floor);
            }

            private void BuildRoutes(DungeonFloorInstance floor)
            {
                var anchors = new bool[size];
                for (int i = 0; i < size; i++) anchors[i] = neighbours[i].Count != 2
                    || floor.Rooms[i].RoomType == DungeonRoomType.Start || floor.Rooms[i].RoomType == DungeonRoomType.Final;
                var visited = new HashSet<long>();
                long Edge(int a, int b) => (long)Math.Min(a, b) * size + Math.Max(a, b);
                void Walk(int start, int next)
                {
                    if (!visited.Add(Edge(start, next))) return;
                    var chain = new List<int> { start, next };
                    int previous = start, current = next;
                    while (current != start && !anchors[current])
                    {
                        int following = neighbours[current][0] == previous ? neighbours[current][1] : neighbours[current][0];
                        if (!visited.Add(Edge(current, following))) break;
                        chain.Add(following); previous = current; current = following;
                    }
                    routes.Add(chain.ToArray());
                }
                for (int i = 0; i < size; i++) if (anchors[i]) foreach (var next in neighbours[i]) Walk(i, next);
                // Also support a closed ring with no fork/start anchor.
                for (int i = 0; i < size; i++) foreach (var next in neighbours[i]) Walk(i, next);
                var chains = routes.ToArray();
                // A fork is not a fight: count empty walks that turn through it,
                // rather than resetting spacing at every junction or room role.
                for (int junction = 0; junction < size; junction++)
                {
                    if (neighbours[junction].Count < 3) continue;
                    for (int a = 0; a < chains.Length; a++)
                    {
                        var left = chains[a];
                        if (left[0] == left[left.Length - 1] || left[0] != junction && left[left.Length - 1] != junction) continue;
                        for (int b = a + 1; b < chains.Length; b++)
                        {
                            var right = chains[b];
                            if (right[0] == right[right.Length - 1] || right[0] != junction && right[right.Length - 1] != junction) continue;
                            var path = new List<int>(left.Length + right.Length - 1);
                            for (int i = 0; i < left.Length; i++) path.Add(left[left.Length - 1] == junction ? left[i] : left[left.Length - 1 - i]);
                            for (int i = 1; i < right.Length; i++) path.Add(right[0] == junction ? right[i] : right[right.Length - 1 - i]);
                            // Cactus loops may share their other endpoint too;
                            // that produces one legitimate closed cycle.
                            var unique = new HashSet<int>(); bool valid = true;
                            for (int i = 0; i < path.Count; i++)
                                if (!unique.Add(path[i]) && !(i == path.Count - 1 && path[i] == path[0])) { valid = false; break; }
                            if (valid) routes.Add(path.ToArray());
                        }
                    }
                }
            }

            public void Prioritize(List<DungeonRoomInstance> candidates, int count)
            {
                var baseline = new bool[candidates.Count];
                for (int i = 0; i < count; i++) baseline[i] = true;
                Apply(baseline); long baselineScore = Score();
                var chosen = new bool[candidates.Count]; int placed = 0;
                for (int i = 0; i < chosen.Length; i++) if (fixedRooms[candidateNodes[i]]) { chosen[i] = true; placed++; }
                Apply(chosen);
                while (placed < count)
                {
                    RefreshNearestDistances();
                    int best = -1; long bestScore = long.MaxValue;
                    for (int i = 0; i < chosen.Length; i++)
                    {
                        if (chosen[i]) continue;
                        occupied[candidateNodes[i]] = true; long score = ScoreRoutes() + CoverageWithAddition(candidateNodes[i]); occupied[candidateNodes[i]] = false;
                        // Candidate order is already seeded/shuffled by placement.
                        // Keep it for equivalent scores, without consuming combat RNG.
                        if (score < bestScore) { best = i; bestScore = score; }
                    }
                    chosen[best] = true; occupied[candidateNodes[best]] = true; placed++;
                }
                long currentScore = Score();
                // Never replace the existing draw by a worse pacing score.
                if (baselineScore < currentScore) { chosen = baseline; Apply(chosen); currentScore = baselineScore; }
                for (int pass = 0; pass < MaximumSwapPasses; pass++)
                {
                    int remove = -1, add = -1; long bestScore = currentScore;
                    for (int a = 0; a < chosen.Length; a++)
                    {
                        if (!chosen[a] || fixedRooms[candidateNodes[a]]) continue;
                        occupied[candidateNodes[a]] = false;
                        RefreshNearestDistances();
                        for (int b = 0; b < chosen.Length; b++)
                        {
                            if (chosen[b]) continue;
                            occupied[candidateNodes[b]] = true; long score = ScoreRoutes() + CoverageWithAddition(candidateNodes[b]); occupied[candidateNodes[b]] = false;
                            if (score < bestScore) { remove = a; add = b; bestScore = score; }
                        }
                        occupied[candidateNodes[a]] = true;
                    }
                    if (remove < 0) break;
                    chosen[remove] = false; chosen[add] = true; Apply(chosen); currentScore = bestScore;
                }
                var ordered = new List<DungeonRoomInstance>(candidates.Count);
                for (int i = 0; i < chosen.Length; i++) if (chosen[i]) ordered.Add(candidates[i]);
                for (int i = 0; i < chosen.Length; i++) if (!chosen[i]) ordered.Add(candidates[i]);
                candidates.Clear(); candidates.AddRange(ordered);
            }

            private void Apply(bool[] chosen)
            {
                Array.Copy(fixedRooms, occupied, size);
                for (int i = 0; i < chosen.Length; i++) if (chosen[i]) occupied[candidateNodes[i]] = true;
            }

            private static long Gap(int run)
            {
                long excess = Math.Max(0, run - PreferredEmptyRun);
                return 2L * run * run + 256 * excess * excess;
            }

            private long Score()
            {
                RefreshNearestDistances();
                long score = ScoreRoutes();
                for (int i = 0; i < size; i++) score += 3L * nearestDistances[i] * nearestDistances[i];
                return score;
            }

            private void RefreshNearestDistances()
            {
                int count = 0;
                for (int i = 0; i < size; i++) if (occupied[i]) centres[count++] = i;
                for (int i = 0; i < size; i++)
                {
                    int nearest = size;
                    for (int j = 0; j < count; j++) nearest = Math.Min(nearest, distances[i, centres[j]]);
                    nearestDistances[i] = nearest;
                }
            }

            private long CoverageWithAddition(int node)
            {
                long score = 0;
                for (int i = 0; i < size; i++)
                { int nearest = Math.Min(nearestDistances[i], distances[i, node]); score += 3L * nearest * nearest; }
                return score;
            }

            private long ScoreRoutes()
            {
                long score = 0;
                foreach (var route in routes)
                {
                    int length = route.Length, start = 0;
                    bool cycle = route[0] == route[length - 1];
                    if (cycle)
                    {
                        length--;
                        while (start < length && !occupied[route[start]]) start++;
                        if (start == length) { score += Gap(length); continue; }
                    }
                    int run = 0;
                    for (int step = 0; step < length; step++)
                    {
                        int node = route[cycle ? (start + step) % length : step];
                        if (occupied[node]) { score += Gap(run); run = 0; } else run++;
                    }
                    score += Gap(run);
                }
                for (int i = 0; i < size; i++)
                {
                    if (!occupied[i]) continue;
                    foreach (var next in neighbours[i])
                        if (next > i && occupied[next] && battleRooms[i] && battleRooms[next]) score += 24;
                }
                return score;
            }

        }
    }
}
