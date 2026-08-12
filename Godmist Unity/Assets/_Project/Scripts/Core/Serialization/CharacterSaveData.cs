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
        public int Honor;
        public float WeaponAccuracy;

        public CharacterLifeState LifeState;
        public CharacterAvailabilityState AvailabilityState;

        public ResourcePoolSaveData ResourcePool = new();
        public ResistanceSetSaveData Resistances = new();
        public List<ShieldLayerSaveData> ShieldLayers = new();
        public List<string> SkillIds = new();
        public List<PassiveEffectSaveData> PassiveEffects = new();

        public List<string> InventoryItemInstanceIds = new();
        public EquipmentSetSaveData Equipment = new();
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
    }

    [Serializable]
    public class PassiveEffectSaveData
    {
        public string EffectId = string.Empty;
        public List<PassiveEffectSourceSaveData> Sources = new();
        public PassiveEffectLifetime Lifetime;
        public int Stacks;
        public int RemainingDuration;
        public PassiveEffectTickPhase TickPhase;
        public int TotalDamage;
        public List<StatModifierSaveData> StatModifiers = new();
        public List<GameplayVariableModifierSaveData> VariableModifiers = new();

        // Migracja zapisów utworzonych przed wprowadzeniem Lifetime i Sources.
        public bool HasLifetime;
        public List<string> SourceEntityIds = new();
        public bool IsTimed;
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