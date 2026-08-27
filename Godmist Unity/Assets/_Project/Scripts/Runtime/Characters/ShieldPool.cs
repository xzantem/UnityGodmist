using System;
using System.Collections.Generic;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public class ShieldPool
    {
        private readonly List<ShieldLayer> layers = new();

        public IReadOnlyList<ShieldLayer> Layers => layers;

        public int TotalShield
        {
            get
            {
                var totalShield = 0;

                foreach (var layer in layers)
                {
                    totalShield += layer.CurrentValue;
                }

                return totalShield;
            }
        }

        public void AddLayer(int value, string sourceId = null)
        {
            if (value <= 0) return;

            layers.Add(new ShieldLayer(value, sourceId));
        }

        public int AbsorbDamage(int incomingDamage)
        {
            var remainingDamage = Math.Max(0, incomingDamage);

            for (var index = layers.Count - 1; index >= 0 && remainingDamage > 0; index--)
            {
                var layer = layers[index];
                remainingDamage -= layer.Absorb(remainingDamage);

                if (layer.CurrentValue <= 0)
                {
                    layers.RemoveAt(index);
                }
            }

            return remainingDamage;
        }

        public int RemoveLayersBySourceId(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) return 0;

            var removedValue = 0;
            for (var index = layers.Count - 1; index >= 0; index--)
            {
                if (!string.Equals(
                        layers[index].SourceId,
                        sourceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                removedValue += layers[index].CurrentValue;
                layers.RemoveAt(index);
            }

            return removedValue;
        }
    }
}
