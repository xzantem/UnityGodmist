using System;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public static class PassiveEffectValueResolver
    {
        public const string RankStateKey = "passive.rank";
        public static int Rank(PassiveEffectRuntime runtime) => Math.Max(1, runtime?.GetStateValue(RankStateKey)?.IntegerValue ?? 1);

        public static float Constant(PassiveEffectValueDefinition value, int rank = 1)
        {
            if (value?.Source != PassiveEffectValueSourceType.Constant)
                throw new InvalidOperationException("The Constant resolver requires a constant passive value.");
            rank = Math.Max(1, rank);
            var raw = value.RankValues != null && rank <= value.RankValues.Count ? value.RankValues[rank - 1] : value.NumberValue * rank;
            return Transform(value, raw);
        }

        public static float Transform(PassiveEffectValueDefinition value, float raw) => raw * value.Scale + value.Offset;
    }
}
