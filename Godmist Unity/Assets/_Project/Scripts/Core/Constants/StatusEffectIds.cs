using System;
using System.Collections.Generic;

namespace Godmist.Core.Constants
{
    public static class StatusEffectIds
    {
        public const string Weakening = "weakening";
        public const string Bleed = "bleed";
        public const string Poison = "poison";
        public const string Burn = "burn";

        public const string Stun = "stun";
        public const string Freeze = "freeze";
        public const string Sleep = "sleep";
        public const string Paralysis = "paralysis";
        public const string Immobilize = "immobilize";
        public const string Chill = "chill";
        public const string Slow = "slow";

        public const string Taunt = "taunt";
        public const string Invisibility = "invisibility";
        public const string Regeneration = "regeneration";
        public const string Counterattack = "counterattack";

        public static bool UsesSuppressionResistance(string statusId) =>
            statusId?.ToLowerInvariant() is Stun or Freeze or Sleep or Paralysis
                or Immobilize or Chill or Taunt;

        public static readonly IReadOnlyList<string> KnownIds =
            new[]
            {
                Weakening,
                Bleed,
                Poison,
                Burn,
                Stun,
                Freeze,
                Sleep,
                Paralysis,
                Immobilize,
                Chill,
                Slow,
                Taunt,
                Invisibility,
                Regeneration,
                Counterattack
            };
    }
}
