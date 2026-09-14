using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    /// <summary>Rejects feedback into a shared history through eligibility or available space.</summary>
    public static class DungeonDistributionValidator
    {
        private sealed class Unit
        {
            public string Id, Key;
            public float Chance;
            public bool Required;
            public readonly List<DungeonSemanticAnchorData> Anchors = new();
            public readonly HashSet<Unit> Dependencies = new();
            public bool Rolls => !Required && Chance > 0 && Chance < 1;
        }

        // Structural template validation must precede this method.
        public static void Validate(IEnumerable<DungeonRoomTemplateDefinition> templates)
        {
            var edges = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var template in templates.Distinct()) Collect(template, edges);
            var active = new HashSet<string>(StringComparer.Ordinal);
            var done = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key)
            {
                if (done.Contains(key)) return;
                active.Add(key);
                if (edges.TryGetValue(key, out var outgoing))
                    foreach (var pair in outgoing.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        if (active.Contains(pair.Key))
                            throw new InvalidOperationException($"Konflikt historii RNG '{key}' -> '{pair.Key}': {pair.Value}. "
                                + "Zalezne lub konkurujace okazje wymagaja niezaleznych kluczy; "
                                + "nie tworz cyklu zaleznosci miedzy historiami, takze w innych szablonach.");
                        Visit(pair.Key);
                    }
                active.Remove(key);
                done.Add(key);
            }
            foreach (var key in edges.Keys.OrderBy(k => k, StringComparer.Ordinal)) Visit(key);
        }

        private static void Collect(DungeonRoomTemplateDefinition template,
            Dictionary<string, Dictionary<string, string>> edges)
        {
            var units = new List<Unit>();
            var byAnchor = new Dictionary<string, Unit>(StringComparer.Ordinal);
            var anchors = (template.SemanticAnchors ?? new()).Where(a => a.Type != DungeonSemanticAnchorType.Enemy)
                .ToDictionary(a => a.Id, StringComparer.Ordinal);
            foreach (var group in template.ContentGroups ?? new())
            {
                var unit = new Unit { Id = "group:" + group.Id, Key = group.DistributionKey,
                    Required = group.IsRequired, Chance = group.SpawnChance };
                foreach (var id in group.AnchorIds) { unit.Anchors.Add(anchors[id]); byAnchor.Add(id, unit); }
                units.Add(unit);
            }
            foreach (var anchor in anchors.Values)
            {
                if (byAnchor.ContainsKey(anchor.Id)) continue;
                var unit = new Unit { Id = "anchor:" + anchor.Id, Key = anchor.DistributionKey,
                    Required = anchor.IsRequired, Chance = anchor.SpawnChance };
                unit.Anchors.Add(anchor);
                byAnchor.Add(anchor.Id, unit);
                units.Add(unit);
            }
            foreach (var anchor in anchors.Values)
                if (!string.IsNullOrWhiteSpace(anchor.RequiresAnchorId))
                {
                    var owner = byAnchor[anchor.Id];
                    var dependency = byAnchor[anchor.RequiresAnchorId];
                    if (owner != dependency) owner.Dependencies.Add(dependency);
                }
            void Require(Unit unit)
            {
                unit.Required = true;
                foreach (var dependency in unit.Dependencies) Require(dependency);
            }
            foreach (var unit in units.Where(u => u.Required).ToList()) Require(unit);
            var sorted = new List<Unit>();
            var visited = new HashSet<Unit>();
            void Sort(Unit unit)
            {
                if (!visited.Add(unit)) return;
                foreach (var dependency in unit.Dependencies.OrderBy(d => d.Id, StringComparer.Ordinal)) Sort(dependency);
                sorted.Add(unit);
            }
            foreach (var unit in units.OrderBy(u => u.Id, StringComparer.Ordinal)) Sort(unit);

            // Connected candidate areas include bridges through wide required anchors:
            // matching may relocate those anchors after an optional selection.
            var components = units.ToDictionary(u => u, u => new HashSet<Unit> { u });
            for (var i = 0; i < units.Count; i++)
                for (var j = i + 1; j < units.Count; j++)
                    if (units[i].Anchors.Any(a => units[j].Anchors.Any(b =>
                        new RectInt(a.LocalCell, a.AreaSize).Overlaps(new RectInt(b.LocalCell, b.AreaSize)))))
                    {
                        var merged = new HashSet<Unit>(components[units[i]]);
                        merged.UnionWith(components[units[j]]);
                        foreach (var member in merged) components[member] = merged;
                    }
            var influences = new Dictionary<Unit, HashSet<Unit>>();
            for (var i = 0; i < sorted.Count; i++)
            {
                var unit = sorted[i];
                var sources = new HashSet<Unit>();
                if (!unit.Required && unit.Chance > 0)
                {
                    var prior = unit.Dependencies.Concat(sorted.Take(i).Where(u =>
                        !u.Required && u.Chance > 0 && components[unit].Contains(u))).Distinct();
                    foreach (var source in prior)
                    {
                        if (source.Rolls) sources.Add(source);
                        if (influences.TryGetValue(source, out var indirect)) sources.UnionWith(indirect);
                    }
                }
                influences.Add(unit, sources);
                if (!unit.Rolls) continue;
                foreach (var source in sources)
                {
                    if (!edges.TryGetValue(source.Key, out var outgoing))
                        edges.Add(source.Key, outgoing = new Dictionary<string, string>(StringComparer.Ordinal));
                    outgoing[unit.Key] = $"szablon '{template.Id}', '{source.Id}' -> '{unit.Id}' (zaleznosc lub wspolny obszar kotew)";
                }
            }
        }
    }
}
