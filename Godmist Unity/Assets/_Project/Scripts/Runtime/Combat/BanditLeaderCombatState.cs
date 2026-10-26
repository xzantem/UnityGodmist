using System.Collections.Generic;
using Godmist.Data.Characters;
namespace Godmist.Runtime.Combat
{
    public sealed class BanditLeaderCombatState
    {
        public CharacterDefinition OriginalDefinition;
        public bool PhaseTwo, PhasePending;
        public int DamageSinceTurn;
        public CombatSession Session;
        public HashSet<BattleUnit> Guards = new();
    }
}
