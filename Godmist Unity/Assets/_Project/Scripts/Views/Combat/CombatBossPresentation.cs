using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
namespace Godmist.Views.Combat
{
    public static class CombatBossPresentation
    {
        // Presentation also works for resumed/debug encounters which have
        // the Ent definition/state but no expedition-derived IsBoss flag.
        public static bool IsBoss(BattleUnit unit) => unit != null && (unit.IsBoss
            || unit.ForestEntState != null
            || unit.Character?.Definition?.Id == ForestEntBossSystem.BossId
            || unit.Character?.Definition?.Id == ForestEntBossSystem.PhaseTwoId);
    }
}
