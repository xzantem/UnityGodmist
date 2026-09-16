using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
namespace Godmist.Systems.Dungeons
{
    /// <summary>Partitions a room graph into connected territories, then builds closed walks within them.</summary>
    public static class DungeonPatrolPlanner
    {
        public static List<List<DungeonTraversalPosition>> Build(DungeonFloorMap map, string room,
            IReadOnlyList<DungeonTraversalPosition> starts, System.Func<DungeonTraversalPosition, bool> canEnter)
            => Build(map, room, starts, canEnter, out _);

        public static List<List<DungeonTraversalPosition>> Build(DungeonFloorMap map, string room,
            IReadOnlyList<DungeonTraversalPosition> starts, System.Func<DungeonTraversalPosition, bool> canEnter,
            out List<List<int>> stopIndices)
        {
            stopIndices = starts.Select(_ => new List<int>()).ToList();
            var result = starts.Select(_ => new List<DungeonTraversalPosition>()).ToList();
            if (map == null || starts.Count == 0) return result;
            bool InRoom(DungeonTraversalPosition p)
            {
                var surface = p.IsOnStairs ? p.Stair.First : p.Surface;
                if (!map.TryGetRoomId(surface, out var id) || id != room || !canEnter(p)) return false;
                return !p.IsOnStairs || map.TryGetRoomId(p.Stair.Second, out var other) && other == room;
            }
            // Discover only components reachable by at least one member, using physical open edges.
            var reachable = new HashSet<DungeonTraversalPosition>();
            var queue = new Queue<DungeonTraversalPosition>();
            foreach (var start in starts) if (InRoom(start) && reachable.Add(start)) queue.Enqueue(start);
            var ordered = new List<DungeonTraversalPosition>();
            while (queue.Count > 0)
            {
                var at = queue.Dequeue(); ordered.Add(at);
                foreach (var next in map.GetTraversalNeighbours(at)) if (InRoom(next) && reachable.Add(next)) queue.Enqueue(next);
            }
            if (ordered.Count == 0) return result;
            var seeds = new List<DungeonTraversalPosition>();
            var distances = new Dictionary<DungeonTraversalPosition, int>();
            for (var i = 0; i < starts.Count && i < ordered.Count; i++)
            {
                var seed = seeds.Count == 0 ? ordered[0] : ordered.Where(p => !seeds.Contains(p))
                    .OrderByDescending(p => distances.TryGetValue(p, out var d) ? d : int.MaxValue).First();
                seeds.Add(seed);
                var local = new Dictionary<DungeonTraversalPosition, int> { [seed] = 0 };queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();var distance = local[at];
                    if (!distances.TryGetValue(at, out var old) || distance < old) distances[at] = distance;
                    foreach (var next in map.GetTraversalNeighbours(at))
                        if (reachable.Contains(next) && !local.ContainsKey(next)) { local[next] = distance + 1;queue.Enqueue(next); }
                }
            }
            // Match each territory to a reachable guard. Unreachable isolated surfaces stay unassigned.
            var available = new HashSet<int>(Enumerable.Range(0, starts.Count));
            var seedOwners = new List<int>();
            foreach (var seed in seeds)
            {
                var owner = -1;var shortest = int.MaxValue;
                foreach (var index in available.OrderBy(i => i))
                    if (DungeonExplorationPathfinder.TryFindPath(map, starts[index], seed, out var path, InRoom) && path.Count < shortest)
                    { owner = index; shortest = path.Count; }
                seedOwners.Add(owner);if (owner >= 0) available.Remove(owner);
            }
            var owners = new Dictionary<DungeonTraversalPosition, int>();
            for (var i = 0; i < seeds.Count; i++) if (seedOwners[i] >= 0) { owners[seeds[i]] = seedOwners[i];queue.Enqueue(seeds[i]); }
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in map.GetTraversalNeighbours(at)) if (reachable.Contains(next) && !owners.ContainsKey(next))
                { owners[next] = owners[at];queue.Enqueue(next); }
            }
            for (var i = 0; i < seeds.Count; i++)
            {
                var owner = seedOwners[i];
                if (owner < 0) continue;
                var territory = ordered.Where(p => owners.TryGetValue(p, out var assigned) && assigned == owner).ToList();
                BuildLoop(map, territory, seeds[i], result[owner], stopIndices[owner]);
            }
            for (var i = 0; i < result.Count; i++)
                if (result[i].Count == 0 && InRoom(starts[i]))
                { result[i].Add(starts[i]); stopIndices[i].Add(0); }
            return result;
        }

        private static void BuildLoop(DungeonFloorMap map, List<DungeonTraversalPosition> territory,
            DungeonTraversalPosition seed, List<DungeonTraversalPosition> route, List<int> stops)
        {
            var allowed = territory.ToHashSet();
            var paths = new Dictionary<DungeonTraversalPosition, Dictionary<DungeonTraversalPosition, List<DungeonTraversalPosition>>>();
            Dictionary<DungeonTraversalPosition, List<DungeonTraversalPosition>> PathsFrom(DungeonTraversalPosition from)
            {
                if (paths.TryGetValue(from, out var cached)) return cached;
                var found = new Dictionary<DungeonTraversalPosition, List<DungeonTraversalPosition>> { [from] = new() { from } };
                var queue = new Queue<DungeonTraversalPosition>(); queue.Enqueue(from);
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    foreach (var next in map.GetTraversalNeighbours(at))
                        if (allowed.Contains(next) && !found.ContainsKey(next))
                        {
                            var path = new List<DungeonTraversalPosition>(found[at]) { next };
                            found.Add(next, path); queue.Enqueue(next);
                        }
                }
                paths.Add(from, found); return found;
            }
            // Sparse graph coverage: increase the number of stops only when the area needs it.
            // Distances follow traversable edges, so a nearby point behind a wall is not coverage.
            var points = new List<DungeonTraversalPosition> { seed };
            var distance = territory.ToDictionary(p => p, p => PathsFrom(seed)[p].Count - 1);
            while (points.Count < territory.Count)
            {
                var farthest = territory.OrderByDescending(p => distance[p]).First();
                if (distance[farthest] <= 4 && (points.Count > 1 || territory.Count == 1)) break;
                points.Add(farthest);
                var from = PathsFrom(farthest);
                foreach (var p in territory) distance[p] = System.Math.Min(distance[p], from[p].Count - 1);
            }
            int Cost(DungeonTraversalPosition a, DungeonTraversalPosition b) => PathsFrom(a)[b].Count - 1;
            // Cheapest insertion gives a stable cycle rather than independent out-and-back branches.
            var tour = new List<DungeonTraversalPosition> { points[0] };
            foreach (var point in points.Skip(1))
            {
                var insertion = 0; var best = int.MaxValue;
                for (var i = 0; i < tour.Count; i++)
                {
                    var next = tour[(i + 1) % tour.Count];
                    var cost = Cost(tour[i], point) + Cost(point, next) - Cost(tour[i], next);
                    if (cost < best) { best = cost; insertion = i + 1; }
                }
                tour.Insert(insertion, point);
            }
            // Uncross strictly improving segments; ties keep their deterministic order.
            bool improved;
            do
            {
                improved = false;
                for (var i = 0; i < tour.Count - 2 && !improved; i++)
                    for (var j = i + 2; j < tour.Count; j++)
                    {
                        var after = tour[(j + 1) % tour.Count];
                        if (Cost(tour[i], tour[i + 1]) + Cost(tour[j], after)
                            <= Cost(tour[i], tour[j]) + Cost(tour[i + 1], after)) continue;
                        tour.Reverse(i + 1, j - i); improved = true; break;
                    }
            } while (improved);
            route.Add(tour[0]); stops.Add(0);
            for (var i = 0; i < tour.Count; i++)
            {
                var leg = PathsFrom(tour[i])[tour[(i + 1) % tour.Count]];
                for (var n = 1; n < leg.Count; n++)
                    if (i != tour.Count - 1 || n != leg.Count - 1) route.Add(leg[n]);
                if (i < tour.Count - 1) stops.Add(route.Count - 1);
            }
            // A forced reversal (dead end / narrow branch) is always an explicit pause point.
            for (var i = 0; i < route.Count; i++)
                if ((tour.Contains(route[i]) || route.Count > 1
                    && route[(i + route.Count - 1) % route.Count] == route[(i + 1) % route.Count])
                    && !stops.Contains(i)) stops.Add(i);
            stops.Sort();
        }
    }
}
