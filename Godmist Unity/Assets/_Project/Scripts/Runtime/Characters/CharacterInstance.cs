using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public class CharacterInstance
    {
        public string CharacterId = string.Empty;
        
        public CharacterDefinition Definition;
        public CharacterClassData ClassData;

        public int Level;
        public int CurrentExperience;
        public int AvailableSkillPoints;

        public CharacterStats Stats;
        public ResourcePool ResourcePool;
        public ResistanceSet Resistances;
        public ShieldPool ShieldPool;
        
        public Inventory Inventory = new();
        public EquipmentSet Equipment = new();

        public int CurrentHealth;
        public int Honor;
        public float WeaponAccuracy = 100f; // Temporary effective value, equipment will calculate it later
        [NonSerialized] public bool DebugIsInvulnerable;

        public CharacterLifeState LifeState;
        public CharacterAvailabilityState AvailabilityState;

        public List<SkillData> Skills = new();

        public List<PassiveEffectRuntime> PassiveEffects = new();

    }
}