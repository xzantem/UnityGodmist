using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public partial class DungeonGeneratorService
    {
        private static void CompactAuthoredLayout(DungeonFloorRoutePlan plan,
            Dictionary<string, Vector2Int> sites, Dictionary<string, Vector2Int> sizes,
            Dictionary<string, Vector2Int> alignment, Dictionary<string, string> xGroups,
            Dictionary<string, string> yGroups, Dictionary<string, Vector2Int> positions, System.Random random, bool forest)
        {
            // Connected vertical chains share X; horizontal chains share Y.
            // Compact those chains independently. A long room elsewhere in the
            // same lattice column must not lengthen every passage in that column.
            (string, string) Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);
            var gaps = plan.Edges.ToDictionary(e => Key(e.First, e.Second), _ => SampleConnectorGap(random, forest));
            var ids = plan.Nodes.Select(n => n.Id).ToArray();
            void Axis(bool horizontal)
            {
                int At(Vector2Int v) => horizontal ? v.x : v.y;
                int Other(Vector2Int v) => horizontal ? v.y : v.x;
                var groups = horizontal ? xGroups : yGroups;
                int Relative(string id) => At(alignment[id]) - At(sizes[id]) / 2;
                var incoming = groups.Values.Distinct().ToDictionary(g => g, _ => new Dictionary<string, int>());
                for (var i = 0; i < ids.Length; i++)
                for (var j = i + 1; j < ids.Length; j++)
                {
                    var a = ids[i]; var b = ids[j];
                    if (At(sites[a]) == At(sites[b])) continue;
                    var connected = gaps.TryGetValue(Key(a, b), out var gap);
                    var overlap = Other(positions[a]) < Other(positions[b]) + Other(sizes[b])
                        && Other(positions[b]) < Other(positions[a]) + Other(sizes[a]);
                    if (!connected && !overlap) continue;
                    if (At(sites[a]) > At(sites[b])) (a, b) = (b, a);
                    var weight = Relative(a) + At(sizes[a]) - Relative(b) + (connected ? gap : 1);
                    var rules = incoming[groups[b]];
                    rules.TryGetValue(groups[a], out var previous);
                    rules[groups[a]] = Math.Max(previous, weight);
                    if (connected)
                    {
                        var reverse = incoming[groups[a]];
                        var maximum = Relative(a) + At(sizes[a]) - Relative(b) + 12;
                        if (!reverse.TryGetValue(groups[b], out var old) || old < -maximum) reverse[groups[b]] = -maximum;
                    }
                }
                // A loose minimum pitch retains the planned shape of remote
                // branches; actual neighbouring bodies impose stronger bounds.
                var minimumSizes = ids.GroupBy(id => At(sites[id]))
                    .ToDictionary(g => g.Key, g => g.Min(id => At(sizes[id])));
                var baseCoordinates = new Dictionary<int, int>();
                var ordered = minimumSizes.Keys.OrderBy(k => k).ToArray();
                var coordinate = 0;
                for (var i = 0; i < ordered.Length; i++)
                {
                    if (i > 0) coordinate += (minimumSizes[ordered[i - 1]] + minimumSizes[ordered[i]]) / 2;
                    baseCoordinates.Add(ordered[i], coordinate);
                }
                var order = incoming.Keys.OrderBy(g => At(sites[g])).ThenBy(g => g, StringComparer.Ordinal).ToArray();
                var solved = order.ToDictionary(g => g, g => baseCoordinates[At(sites[g])]);
                // Both lower and upper spacing bounds matter. Propagating only
                // lower bounds can push one chain far away and leave a 40-cell
                // empty link on the other arm of a loop.
                for (var iteration = 0; iteration <= order.Length; iteration++)
                {
                    var changed = false;
                    foreach (var group in order)
                    foreach (var rule in incoming[group])
                    {
                        var value = solved[rule.Key] + rule.Value;
                        if (value <= solved[group]) continue;
                        solved[group] = value; changed = true;
                    }
                    if (!changed) break;
                    if (iteration == order.Length)
                        throw new LayoutAttemptFailedException("Organic plan cannot fit within short connector bounds.");
                }
                foreach (var id in ids)
                {
                    var value = solved[groups[id]] + Relative(id);
                    positions[id] = horizontal ? new Vector2Int(value, positions[id].y) : new Vector2Int(positions[id].x, value);
                }
            }
            for (var pass = 0; pass < 2; pass++) { Axis(true); Axis(false); }
        }
    }
}
