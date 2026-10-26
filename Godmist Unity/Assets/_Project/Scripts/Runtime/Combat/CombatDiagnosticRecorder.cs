using System;
using System.Collections.Generic;
using System.Linq;

namespace Godmist.Runtime.Combat
{
    // Campaign-owned, opt-in and bounded. No file I/O or save-state mutations in combat.
    public sealed class CombatDiagnosticRecorder
    {
        public const int Capacity = 4000;
        readonly Queue<string> records = new();
        long sequence;
        public bool Enabled { get; set; }
        public int Count => records.Count;
        public void Clear() { records.Clear(); sequence = 0; }
        public void Record(string kind, CombatSession session, BattleUnit actor, string detail)
        {
            if (!Enabled) return;
            if (records.Count == Capacity) records.Dequeue();
            records.Enqueue($"{++sequence}|{kind}|combat={session?.CombatId}|actor={actor?.Character?.CharacterId}|definition={actor?.Character?.Definition?.Id}|pos=({actor?.Position.X},{actor?.Position.Y},{actor?.Position.Level})|ap={actor?.CurrentActionPoints}/{actor?.MaxActionPoints}|resource={actor?.Character?.ResourcePool?.CurrentValue}|{detail}");
        }
        public string Read(int last = Capacity) => string.Join(Environment.NewLine, records.Skip(Math.Max(0, records.Count - Math.Max(0,last))));
    }
}
