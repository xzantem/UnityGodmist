using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class BattleUnit
    {
        public CharacterInstance Character;
        public GridPosition Position;

        public int CurrentActionPoints;
        public int MaxActionPoints;
        public int MovementStepsThisTurn;
        public int MaxKnives;
        public int AvailableKnives;
        public float CurrentActionValue;

        // Combat-scoped state. It intentionally lives on the battle participant rather
        // than on CharacterInstance, so it cannot leak into exploration/save state.
        public int NextTurnActionPointModifier;
        public int LastSkillResourceSpent;
        public int ResourceRegenerationBlockedTurns;
        public string DamageRedirectProtectorCharacterId = string.Empty;
        public float DamageRedirectPercent;
        public List<UniqueBuffType> UniqueBuffs = new();
        public Dictionary<ResistanceType, float> ResistanceModifiers = new();
        public List<CombatAuraRuntime> ActiveAuras = new();
        public List<CombatDamageRedirectRuntime> OutgoingDamageRedirects = new();
        public HashSet<string> ActiveToggleSkillIds = new();
        public HashSet<string> ToggleSkillIdsUsedThisTurn = new();

        public bool HasReacted;
        public bool IsReadyToEscape;
        public bool IsPlayerSide;
        public int DownedTurnsRemaining;

        public const int DownedTurnLimit = 5;
    }

    [Serializable]
    public class CombatAuraRuntime
    {
        public string AuraId = string.Empty;
        public string DisplayName = string.Empty;
        public string StatusId = string.Empty;
        public int Radius;
        public bool IncludesCaster;
        public TargetingType TargetingType;
        public float Chance = 1f;
        public int DurationTurns = 1;
        public float StatusDamageMultiplier;
        public int StatusDamagePerTurn;
    }

    [Serializable]
    public class CombatDamageRedirectRuntime
    {
        [NonSerialized] public BattleUnit Target;
        public string SourceEffectId = string.Empty;
        public string TargetCharacterId = string.Empty;
        public string TargetDisplayName = string.Empty;
        public float RedirectPercent;
    }
}
