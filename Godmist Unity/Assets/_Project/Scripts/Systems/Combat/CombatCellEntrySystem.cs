using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    // Shared arrival effects for walking, swapping, teleporting and displacement.
    public static class CombatCellEntrySystem
    {
        public static void Notify(CombatSession session, BattleUnit unit,
            CombatRecoverySystem recoverySystem = null)
        {
            session.NotifyUnitEnteredCell(unit);
            // A lethal trap resolves before a pickup can reward its victim.
            if (unit?.Character?.LifeState == CharacterLifeState.Alive)
                ScoutKnifeSystem.RecoverAtPosition(session, unit, recoverySystem);
        }
    }
}
