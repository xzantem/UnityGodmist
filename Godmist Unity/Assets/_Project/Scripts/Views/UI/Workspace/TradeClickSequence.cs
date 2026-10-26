using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Owns a rapid click series across rebuilt grid slots; never follows a shifted neighbour.</summary>
    public sealed class TradeClickSequence
    {
        public const float Interval = .35f;
        private HashSet<string> ids;
        private TradeItemSlotSource source;
        private Vector2 position;
        private float lastTime = float.NegativeInfinity;

        public bool Click(TradeItemSlotSource nextSource, string id, IReadOnlyList<string> stackIds,
            Vector2 point, float time)
        {
            var continuation = ids != null && time - lastTime <= Interval
                && nextSource == source && (point - position).sqrMagnitude <= 36;
            lastTime = time;
            if (continuation) return ids.Contains(id);
            ids = new HashSet<string>(stackIds); source = nextSource; position = point;
            return false;
        }
        public void Reset() { ids = null; lastTime = float.NegativeInfinity; }
    }
}
