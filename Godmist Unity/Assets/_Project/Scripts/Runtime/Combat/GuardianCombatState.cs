namespace Godmist.Runtime.Combat
{
    // Combat participant state only; never persisted on a character or shared asset.
    public sealed class GuardianCombatState
    {
        public int OwnTurn, FreeStepExpiresAfterTurn, FreeStepsTaken, CounterTurns, ReactionDepth;
        public bool HasFreeStep, StepAwarded, DodgeRewardUsed, FirstHitUsed, PaidMovementUsed, DiscountUsed;
        public int PaidResourceThisAction;
        public void CopyFrom(GuardianCombatState other)
        {
            OwnTurn=other.OwnTurn;FreeStepExpiresAfterTurn=other.FreeStepExpiresAfterTurn;
            FreeStepsTaken=other.FreeStepsTaken;CounterTurns=other.CounterTurns;
            HasFreeStep=other.HasFreeStep;StepAwarded=other.StepAwarded;DodgeRewardUsed=other.DodgeRewardUsed;
            FirstHitUsed=other.FirstHitUsed;PaidMovementUsed=other.PaidMovementUsed;DiscountUsed=other.DiscountUsed;
            // An in-progress nested action is never carried into an AI projection.
            ReactionDepth=0;PaidResourceThisAction=0;
        }
        public int StateHash()=>System.HashCode.Combine(OwnTurn,FreeStepExpiresAfterTurn,FreeStepsTaken,CounterTurns,
            System.HashCode.Combine(HasFreeStep,StepAwarded,DodgeRewardUsed,FirstHitUsed,PaidMovementUsed,DiscountUsed));
    }
}
