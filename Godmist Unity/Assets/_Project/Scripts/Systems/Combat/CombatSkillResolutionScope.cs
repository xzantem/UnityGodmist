using System;
using Godmist.Runtime.Combat;
namespace Godmist.Systems.Combat
{
    // Nested counterattacks/duplicates must finish before a boss can transform.
    internal sealed class CombatSkillResolutionScope : IDisposable
    {
        private readonly CombatSession session;
        public CombatSkillResolutionScope(CombatSession session)
        { this.session = session; session.SkillResolutionDepth++; }
        public void Dispose()
        { if (--session.SkillResolutionDepth == 0) session.SkillsFinished?.Invoke(); }
    }
}
