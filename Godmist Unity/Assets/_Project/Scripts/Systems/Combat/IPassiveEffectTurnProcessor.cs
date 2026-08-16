using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public interface IPassiveEffectTurnProcessor
    {
        bool CanProcess(
            PassiveEffectRuntime effect,
            PassiveEffectTickPhase tickPhase);

        bool ProcessTurn(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectRuntime effect,
            out bool shouldExpire);
    }
}