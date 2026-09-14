using System;
using System.Collections.Generic;
using Godmist.Core.Utilities;

namespace Godmist.Systems.Dungeons
{
    // One instance for the complete generation pass, shared by all floors.
    public sealed class DungeonContentDistribution
    {
        private readonly int dungeonSeed;
        private readonly Dictionary<string, ContinuousChanceStream> streams = new(StringComparer.Ordinal);

        public DungeonContentDistribution(int dungeonSeed) => this.dungeonSeed = dungeonSeed;

        public bool Roll(string key, double chance)
        {
            if (double.IsNaN(chance) || chance < 0 || chance > 1)
                throw new ArgumentOutOfRangeException(nameof(chance));
            if (chance == 0) return false;
            if (chance == 1) return true;
            if (string.IsNullOrWhiteSpace(key) || key != key.Trim())
                throw new ArgumentException("A distribution key is required.", nameof(key));
            if (!streams.TryGetValue(key, out var stream))
            {
                // Explicit, stable UTF-16 hash; string.GetHashCode is process-dependent.
                uint hash = 2166136261;
                unchecked
                {
                    foreach (var character in "dungeon.content/" + key)
                        hash = (hash ^ character) * 16777619;
                    hash = (hash ^ (uint)dungeonSeed) * 16777619;
                }
                stream = new ContinuousChanceStream(unchecked((int)hash));
                streams.Add(key, stream);
            }
            return stream.Roll(chance);
        }
    }
}
