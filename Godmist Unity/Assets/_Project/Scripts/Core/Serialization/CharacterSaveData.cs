using System;
using System.Collections.Generic;
using Godmist.Core.Enums;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class CharacterSaveData
    {
        public string CharacterId = string.Empty;
        public string ClassId = string.Empty;

        public int Level;
        public int CurrentExperience;
        public int AvailableSkillPoints;
        public int CurrentHealth;
        public float EquipmentHealthCarry, EquipmentResourceCarry, EquipmentGenerationCarry;
        public int Honor;
        public float WeaponAccuracy;

        public CharacterLifeState LifeState;
        public CharacterAvailabilityState AvailabilityState;

        public ResourcePoolSaveData ResourcePool = new();
        public bool HasPersistentResistances;
        public ResistanceSetSaveData Resistances = new();
        public List<ShieldLayerSaveData> ShieldLayers = new();
        public List<string> SkillIds = new();
        public bool HasActiveSkillLoadout;
        public List<string> ActiveSkillIds = new();
        // Fixed-capacity positions, including empty entries, introduced by UI Workspace v1.
        // ActiveSkillIds remains for backward compatibility with older saves.
        public List<string> ActiveSkillSlotIds = new();
        public List<SkillLoadoutCapacityModifierSaveData> ActiveSkillCapacityModifiers = new();
        public List<SkillTreeProgressSaveData> SkillTreeProgress = new();
        public List<PassiveEffectSaveData> PassiveEffects = new();

        public List<string> InventoryItemInstanceIds = new();
        public EquipmentSetSaveData Equipment = new();
    }

    [Serializable]
    public class SkillLoadoutCapacityModifierSaveData
    {
        public string SourceId = string.Empty;
        public int Amount;
    }

    [Serializable]
    public class SkillTreeProgressSaveData
    {
        public string TreeId = string.Empty;
        public List<SkillTreeNodeInvestmentSaveData> NodeInvestments = new();
    }

    [Serializable]
    public class SkillTreeNodeInvestmentSaveData
    {
        public string NodeId = string.Empty;
        public int Ranks;
    }

    [Serializable]
    public class ResourcePoolSaveData
    {
        public ResourceType ResourceType;
        public int CurrentValue;
        public int MaxValue;
        public int Regeneration;
        public bool HasStartedTurn;
        public bool HasGainedResourceSinceLastTurn;
        public float FractionalGainRemainder;
    }

    [Serializable]
    public class ResistanceSetSaveData
    {
        public float BleedResistance;
        public float PoisonResistance;
        public float FireResistance;
        public float WeakeningResistance;
        public float SuppressionResistance;
        public float MoveResistance;
    }

    [Serializable]
    public class ShieldLayerSaveData
    {
        public string SourceId = string.Empty;
        public int CurrentValue;
        public int RemainingTurns;
    }

    [Serializable]
    public class PassiveEffectSaveData
    {
        public string EffectId = string.Empty;
        public string DefinitionId = string.Empty;
        public string DisplayName = string.Empty;
        public List<PassiveEffectSourceSaveData> Sources = new();
        public PassiveEffectLifetime Lifetime;
        public int Stacks;
        public PassiveEffectStackingRule StackingRule;
        public int RemainingDuration;
        public PassiveEffectTickPhase TickPhase;
        public List<PassiveEffectPayloadSaveData> Payloads = new();
        public List<PassiveEffectRuntimeStateValueSaveData> StateValues = new();

    }

    [Serializable]
    public class PassiveEffectRuntimeStateValueSaveData
    {
        public string Key = string.Empty;
        public PassiveEffectRuntimeValueType Type;
        public float NumberValue;
        public int IntegerValue;
        public string TextValue = string.Empty;
        public bool BooleanValue;
    }

    [Serializable]
    public class PassiveEffectPayloadSaveData
    {
        public PassiveEffectPayloadType Type;
        public int RemainingTotalDamage;
        public int DamagePerTurn;
        public float MaxHealthPercent;
        public float HealthPercent;
        public float ResourcePercent;
        public float HealthRoundingCarry;
        public float ResourceRoundingCarry;
        public UniqueBuffType UniqueBuffType;
        public float ActionPointModifier;
        public float DamageRedirectPercent;
        public string ProtectorCharacterId = string.Empty;
        public string ProtectorDisplayName = string.Empty;
        public string ProtectedCharacterId = string.Empty;
        public string ProtectedCharacterDisplayName = string.Empty;
        public string OnHitStatusId = string.Empty;
        public float OnHitStatusChance;
        public int OnHitStatusDurationTurns;
        public float OnHitStatusDamageMultiplier;
        public int OnHitStatusDamagePerTurn;
        public List<StatModifierSaveData> StatModifiers = new();
        public List<GameplayVariableModifierSaveData> VariableModifiers = new();
        public ResistanceType ResistanceType;
        public float ResistanceModifierValue;
    }

    [Serializable]
    public class PassiveEffectSourceSaveData
    {
        public PassiveEffectSourceType SourceType;
        public string SourceId = string.Empty;
        public string SourceCharacterId = string.Empty;
    }

    [Serializable]
    public class GameplayVariableModifierSaveData
    {
        public GameplayVariableType VariableType;
        public string ReferenceId = string.Empty;
        public StatModifierStacking Stacking;
        public float Value;
    }

    [Serializable]
    public class StatModifierSaveData
    {
        public CharacterStatType StatType;
        public StatModifierOperation Operation;
        public StatModifierStacking Stacking;
        public float Value;
    }

    [Serializable]
    public class EquipmentSetSaveData
    {
        public string WeaponItemInstanceId = string.Empty;
        public string ArmorItemInstanceId = string.Empty;
        public string NecklaceItemInstanceId = string.Empty;
        public string RingItemInstanceId = string.Empty;
        public string BraceletItemInstanceId = string.Empty;
        public string EarringsItemInstanceId = string.Empty;
    }
}
