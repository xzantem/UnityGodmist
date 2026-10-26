using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;

namespace Godmist.Systems.Dungeons
{
    // Topology only. No template IDs, positions, footprint or passage queries.
    public sealed class DungeonFloorRoutePlan
    {
        public sealed class Node
        {
            public string Id { get; }
            public DungeonRoomType Type { get; }
            public bool Transit { get; }
            public bool Required { get; }
            public bool IsLoopRoom { get; }
            internal Node(string id, DungeonRoomType type, bool transit = false, bool required = false, bool loop = false)
            { Id = id; Type = type; Transit = transit; Required = required; IsLoopRoom = loop; }
        }
        public sealed class Edge
        {
            public string First { get; }
            public string Second { get; }
            public int GateIndex { get; }
            public string ControlRoomId { get; }
            internal Edge(string first, string second, int gate = -1, string control = null)
            { First = first; Second = second; GateIndex = gate; ControlRoomId = control; }
        }
        public sealed class Loop
        {
            public string Fork { get; }
            public string Join { get; }
            public IReadOnlyList<string> FirstArm { get; }
            public IReadOnlyList<string> SecondArm { get; }
            internal Loop(string fork, string join, List<string> first, List<string> second)
            { Fork = fork; Join = join; FirstArm = first.AsReadOnly(); SecondArm = second.AsReadOnly(); }
        }
        public sealed class Branch
        {
            public string Root { get; }
            public IReadOnlyList<string> Rooms { get; }
            internal Branch(string root, List<string> rooms) { Root = root; Rooms = rooms.AsReadOnly(); }
        }
        public sealed class Block
        {
            public string First { get; }
            public string Last { get; }
            public Loop Cycle { get; }
            internal Block(string node) { First = Last = node; }
            internal Block(Loop loop) { First = loop.Fork; Last = loop.Join; Cycle = loop; }
        }
        public int FloorIndex { get; }
        public int BaseRoomCount { get; }
        public IReadOnlyList<Node> Nodes { get; }
        public IReadOnlyList<Edge> Edges { get; }
        public IReadOnlyList<Loop> Loops { get; }
        public IReadOnlyList<Branch> Branches { get; }
        public IReadOnlyList<Block> MainBlocks { get; }
        internal DungeonFloorRoutePlan(int floor, int budget, List<Node> nodes, List<Edge> edges,
            List<Loop> loops, List<Branch> branches, List<Block> blocks)
        {
            FloorIndex = floor; BaseRoomCount = budget; Nodes = nodes.AsReadOnly(); Edges = edges.AsReadOnly();
            Loops = loops.AsReadOnly(); Branches = branches.AsReadOnly(); MainBlocks = blocks.AsReadOnly();
        }
    }

    public static class DungeonRoutePlanner
    {
        public static IReadOnlyList<DungeonFloorRoutePlan> CreateExpedition(DungeonDefinition definition,
            DungeonLengthProfile profile, System.Random random)
        {
            if (definition == null || profile == null || random == null || profile.FloorCount < 1)
                throw new ArgumentException("Expedition planning requires a definition, profile and RNG.");
            var minimumPerFloor = Math.Max(1, definition.MinProgressionGateCount);
            var minimum = Math.Max(profile.MinBranchCount, profile.FloorCount * minimumPerFloor);
            if (minimum > profile.MaxBranchCount) throw new InvalidOperationException("The expedition branch budget cannot cover its floors.");
            var total = random.Next(minimum, profile.MaxBranchCount + 1);
            var counts = new int[profile.FloorCount];
            for (var index = 0; index < counts.Length; index++) counts[index] = minimumPerFloor;
            for (var extra = counts.Length * minimumPerFloor; extra < total; extra++) counts[random.Next(counts.Length)]++;
            var plans = new DungeonFloorRoutePlan[counts.Length];
            for (var floor = 0; floor < plans.Length; floor++) plans[floor] = Create(definition, profile, floor, counts[floor], random);
            return Array.AsReadOnly(plans);
        }

        public static DungeonFloorRoutePlan Create(DungeonDefinition definition, DungeonLengthProfile profile,
            int floorIndex, int branchCount, System.Random random)
        {
            if (definition == null || profile == null || random == null || branchCount < 1)
                throw new ArgumentException("Route planning requires a definition, profile, RNG and branches.");
            var gates = Math.Min(branchCount, random.Next(definition.MinProgressionGateCount,
                definition.MaxProgressionGateCount + 1));
            var budget = random.Next(profile.MinRoomsPerFloor, profile.MaxRoomsPerFloor + 1);
            var loopCount = Math.Max(Math.Max(1, definition.MinLoopConnections), budget / 20);
            var minArm = profile.MinLoopArmRoomCount;
            var maxArm = profile.MaxLoopArmRoomCount;
            if (minArm < 2 || maxArm < minArm) throw new InvalidOperationException("Invalid planned loop arm range.");
            // Start/final plus a control junction in any gated section without a loop.
            int FixedRooms() => 2 + Math.Max(0, gates - loopCount);
            var minimumBudget = branchCount + FixedRooms() + loopCount * (2 * minArm + 2);
            if (minimumBudget > profile.MaxRoomsPerFloor)
                throw new InvalidOperationException("The room budget cannot fit planned loops, controls and gates.");
            budget = Math.Max(budget, minimumBudget);
            // Reserve unequal arms where the existing floor budget can afford
            // them. Grid paths between the same endpoints need equal parity.
            var asymmetricMinimum = minimumBudget + (maxArm - minArm >= 2 ? 2 * loopCount : 0);
            if (asymmetricMinimum <= profile.MaxRoomsPerFloor) budget = Math.Max(budget, asymmetricMinimum);
            var arms = new (int first, int second)[loopCount];
            var available = budget - branchCount - FixedRooms();
            for (var i = 0; i < loopCount; i++)
            {
                var reserve = (loopCount - i - 1) * (2 * minArm + 2);
                var pairs = new List<(int first, int second)>();
                for (var first = minArm; first <= maxArm; first++)
                for (var second = minArm; second <= maxArm; second++)
                    if ((first + second) % 2 == 0 && first + second + 2 <= available - reserve)
                        pairs.Add((first, second));
                var unequal = pairs.Where(p => p.first != p.second).ToList();
                var choices = unequal.Count > 0 ? unequal : pairs;
                arms[i] = choices[random.Next(choices.Count)];
                available -= arms[i].first + arms[i].second + 2;
            }
            var extraPerSection = new int[gates + 1];
            for (var i = 0; i < available; i++) extraPerSection[random.Next(extraPerSection.Length)]++;
            var nodes = new List<DungeonFloorRoutePlan.Node>();
            var edges = new List<DungeonFloorRoutePlan.Edge>();
            var loops = new List<DungeonFloorRoutePlan.Loop>();
            var blocks = new List<DungeonFloorRoutePlan.Block>();
            var branches = new List<DungeonFloorRoutePlan.Branch>();
            var sections = new List<List<string>>();
            var gateEnds = new List<(string first, string second)>();
            var number = 0;
            string Main(DungeonRoomType type, bool loop = false)
            {
                var id = $"floor.{floorIndex}.room.{number++:00}";
                nodes.Add(new DungeonFloorRoutePlan.Node(id, type, loop: loop)); return id;
            }
            string previous = null;
            void Append(DungeonFloorRoutePlan.Block block, bool gate)
            {
                if (previous != null)
                {
                    if (gate) gateEnds.Add((previous, block.First));
                    else edges.Add(new DungeonFloorRoutePlan.Edge(previous, block.First));
                }
                blocks.Add(block); previous = block.Last;
            }
            var nextLoop = 0;
            for (var section = 0; section <= gates; section++)
            {
                var members = new List<string>(); sections.Add(members);
                var firstBlock = true;
                void AddBlock(DungeonFloorRoutePlan.Block block)
                {
                    Append(block, section > 0 && firstBlock); firstBlock = false;
                }
                if (section == 0) AddBlock(new DungeonFloorRoutePlan.Block(Main(DungeonRoomType.Start)));
                // Distribute cycles over progression sections; surplus cycles fit
                // in the final section. No cycle spans a planned gate bridge.
                var sectionLoops = section == gates ? loopCount - nextLoop : nextLoop < loopCount ? 1 : 0;
                var ordinaryCount = extraPerSection[section] + (section < gates && sectionLoops == 0 ? 1 : 0);
                void AddOrdinary(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var id = Main(random.NextDouble() < definition.LayoutProfile.CorridorRoomChance
                            ? DungeonRoomType.Corridor : DungeonRoomType.Normal);
                        AddBlock(new DungeonFloorRoutePlan.Block(id)); members.Add(id);
                    }
                }
                // Plan lead-ins as well as tails: loops need not always begin
                // immediately after the entrance or a progression gate.
                var leadIn = random.Next(ordinaryCount + 1);
                AddOrdinary(leadIn);
                for (var cycle = 0; cycle < sectionLoops; cycle++)
                {
                    var count = arms[nextLoop++];
                    var fork = Main(DungeonRoomType.Normal, true);
                    var first = new List<string>(); var second = new List<string>();
                    for (var i = 0; i < count.first; i++) first.Add(Main(DungeonRoomType.Normal, true));
                    for (var i = 0; i < count.second; i++) second.Add(Main(DungeonRoomType.Normal, true));
                    var join = Main(DungeonRoomType.Normal, true);
                    var loop = new DungeonFloorRoutePlan.Loop(fork, join, first, second);
                    loops.Add(loop); AddBlock(new DungeonFloorRoutePlan.Block(loop));
                    foreach (var arm in new[] { first, second })
                    {
                        var from = fork;
                        foreach (var to in arm) { edges.Add(new DungeonFloorRoutePlan.Edge(from, to)); from = to; }
                        edges.Add(new DungeonFloorRoutePlan.Edge(from, join));
                    }
                    members.Add(fork); members.AddRange(first); members.AddRange(second); members.Add(join);
                }
                AddOrdinary(ordinaryCount - leadIn);
                if (section == gates) AddBlock(new DungeonFloorRoutePlan.Block(Main(DungeonRoomType.Final)));
            }
            var usedRoots = new HashSet<string>(StringComparer.Ordinal);
            var allRoots = sections.SelectMany(s => s).ToList();
            for (var branch = 0; branch < branchCount; branch++)
            {
                string root;
                if (branch < gates) root = sections[branch][sections[branch].Count - 1];
                else
                {
                    var candidates = allRoots.Where(r => !usedRoots.Contains(r)
                        && edges.Count(e => e.First == r || e.Second == r) < 4).ToList();
                    if (candidates.Count == 0) throw new InvalidOperationException("No independent planned branch junction.");
                    root = candidates[Math.Min(candidates.Count - 1, (branch * candidates.Count) / branchCount)];
                }
                usedRoots.Add(root);
                var approach = random.Next(profile.MinBranchTransitRoomCount, profile.MaxBranchTransitRoomCount + 1);
                var branchNodes = new List<string>();
                var from = root;
                for (var step = 0; step <= approach; step++)
                {
                    var end = step == approach;
                    var id = end ? $"floor.{floorIndex}.optional.{branch:00}"
                        : $"floor.{floorIndex}.branch.{branch:00}.transit.{step:00}";
                    var type = end ? DungeonRoomType.Optional : random.NextDouble() < definition.LayoutProfile.CorridorRoomChance
                        ? DungeonRoomType.Corridor : DungeonRoomType.Normal;
                    nodes.Add(new DungeonFloorRoutePlan.Node(id, type, !end, end && branch < gates));
                    edges.Add(new DungeonFloorRoutePlan.Edge(from, id)); from = id; branchNodes.Add(id);
                }
                branches.Add(new DungeonFloorRoutePlan.Branch(root, branchNodes));
                if (branch < gates)
                    edges.Add(new DungeonFloorRoutePlan.Edge(gateEnds[branch].first, gateEnds[branch].second, branch, from));
            }
            var result = new DungeonFloorRoutePlan(floorIndex, budget, nodes, edges, loops, branches, blocks);
            Validate(result);
            return result;
        }

        public static void Validate(DungeonFloorRoutePlan plan)
        {
            var ids = new HashSet<string>(plan.Nodes.Select(n => n.Id), StringComparer.Ordinal);
            if (ids.Count != plan.Nodes.Count || plan.Nodes.Count(n => !n.Transit) != plan.BaseRoomCount)
                throw new InvalidOperationException("Route room identities/budget disagree.");
            var adjacency = ids.ToDictionary(id => id, _ => new List<string>());
            foreach (var edge in plan.Edges)
            {
                if (edge.First == edge.Second || !ids.Contains(edge.First) || !ids.Contains(edge.Second)
                    || adjacency[edge.First].Contains(edge.Second)) throw new InvalidOperationException("Invalid route edge.");
                adjacency[edge.First].Add(edge.Second); adjacency[edge.Second].Add(edge.First);
            }
            var loopNodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loop in plan.Loops)
            {
                var members = new[] { loop.Fork, loop.Join }.Concat(loop.FirstArm).Concat(loop.SecondArm).ToArray();
                if (loop.FirstArm.Count < 2 || loop.SecondArm.Count < 2
                    || (loop.FirstArm.Count + loop.SecondArm.Count) % 2 != 0
                    || members.Distinct().Count() != members.Length || members.Any(id => !ids.Contains(id) || !loopNodes.Add(id)))
                    throw new InvalidOperationException("Loop arms must be complete, independent paths.");
                foreach (var arm in new[] { loop.FirstArm, loop.SecondArm })
                {
                    var path = new[] { loop.Fork }.Concat(arm).Concat(new[] { loop.Join }).ToArray();
                    for (var index = 1; index < path.Length; index++)
                        if (!adjacency[path[index - 1]].Contains(path[index]))
                            throw new InvalidOperationException("A planned loop arm has a missing edge.");
                }
            }
            var branchNodes = new HashSet<string>(StringComparer.Ordinal);
            var branchRoots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var branch in plan.Branches)
            {
                var previous = branch.Root;
                if (!ids.Contains(previous) || !branchRoots.Add(previous) || branch.Rooms.Count < 2)
                    throw new InvalidOperationException("Invalid planned branch junction/approach.");
                for (var index = 0; index < branch.Rooms.Count; index++)
                {
                    var id = branch.Rooms[index];
                    if (!ids.Contains(id) || !branchNodes.Add(id) || loopNodes.Contains(id) || !adjacency[previous].Contains(id))
                        throw new InvalidOperationException("A planned branch has overlapping or missing approach rooms.");
                    var node = plan.Nodes.Single(n => n.Id == id);
                    if (node.Transit != (index < branch.Rooms.Count - 1)
                        || (index == branch.Rooms.Count - 1 && node.Type != DungeonRoomType.Optional)
                        || adjacency[id].Count != (index == branch.Rooms.Count - 1 ? 1 : 2))
                        throw new InvalidOperationException("A planned branch has a shortcut or invalid destination.");
                    previous = id;
                }
            }
            if (plan.Nodes.Any(n => (n.Transit || n.Type == DungeonRoomType.Optional) && !branchNodes.Contains(n.Id)))
                throw new InvalidOperationException("Orphan planned branch room.");
            var start = plan.Nodes.Single(n => n.Type == DungeonRoomType.Start).Id;
            var final = plan.Nodes.Single(n => n.Type == DungeonRoomType.Final).Id;
            HashSet<string> Reach(Func<DungeonFloorRoutePlan.Edge, bool> allowed)
            {
                var reached = new HashSet<string> { start }; var queue = new Queue<string>(); queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    foreach (var edge in plan.Edges)
                    {
                        if (!allowed(edge)) continue;
                        var next = edge.First == node ? edge.Second : edge.Second == node ? edge.First : null;
                        if (next != null && reached.Add(next)) queue.Enqueue(next);
                    }
                }
                return reached;
            }
            if (Reach(_ => true).Count != ids.Count || adjacency.Any(p => p.Value.Count > 4)
                || plan.Edges.Count - ids.Count + 1 != plan.Loops.Count)
                throw new InvalidOperationException("Route connectivity, degrees or cycle rank disagree.");
            var gates = plan.Edges.Where(e => e.GateIndex >= 0).OrderBy(e => e.GateIndex).ToArray();
            foreach (var gate in gates)
            {
                if (Reach(e => e != gate).Contains(final)) throw new InvalidOperationException("A route bypasses a planned gate.");
                if (!Reach(e => e.GateIndex < 0 || e.GateIndex < gate.GateIndex).Contains(gate.ControlRoomId))
                    throw new InvalidOperationException("A gate control is not reachable before its gate.");
            }
        }
    }
}
