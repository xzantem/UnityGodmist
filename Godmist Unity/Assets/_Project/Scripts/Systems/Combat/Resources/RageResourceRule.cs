using Godmist.Runtime.Characters;
using Godmist.Data.Configuration;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class RageResourceRule : IResourceRule
    {
        // Keep the existing define ID for saved/user configuration compatibility.
        public static int RagePerSkill => GameDefinesRuntime.GetInt(
            GameDefineIds.RagePerHit, 10);
        private static int RageDecayPerMissedTurn => GameDefinesRuntime.GetInt(
            GameDefineIds.RageDecayPerMissedTurn, 10);

        public static int PreviewOffensiveGain(CharacterInstance character)
        {
            var carry=character?.EquipmentGenerationCarry??0;
            return RagePerSkill+PeriodicRecoveryPassiveEffectProcessor.GetRecoveryAmount(
                RagePerSkill*(EquipmentEffectSystem.ResourceMultiplier(character)-1),ref carry);
        }

        public int ProcessTurnStart(ResourcePool pool)
        {
            if (pool == null)
            {
                return 0;
            }

            pool.HasStartedTurn = true;
            pool.HasGainedResourceSinceLastTurn = false;
            return 0;
        }

        public int ProcessTurnEnd(ResourcePool pool, WarriorCombatState state)
        {
            if (pool == null || state == null) return 0;
            if (state.RageOffensiveActionUsed) { state.RageIdleTurns = 0; return 0; }
            if (state.RageControlBlocked && !state.RageHadOffensiveOpportunity) return 0;
            state.RageIdleTurns = System.Math.Min(2, System.Math.Max(0, state.RageIdleTurns) + 1);
            return state.RageIdleTurns < 2 ? 0 : -pool.Drain(RageDecayPerMissedTurn);
        }

        public int ProcessMovement(
            ResourcePool pool,
            int spentActionPoints)
        {
            return 0;
        }

        public int ProcessOffensiveSkillUsed(ResourcePool pool)
        {
            if (pool == null)
            {
                return 0;
            }

            // Preserve gain bookkeeping even at the cap; the combat-local state
            // separately records offense for end-of-turn decay.
            pool.HasGainedResourceSinceLastTurn = true;

            return pool.Restore(RagePerSkill);
        }
    }
}
