using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Configuration;
using Godmist.Data.Skills;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;

[assembly: InternalsVisibleTo("Systems")]
[assembly: InternalsVisibleTo("EditModeTests")]

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
        public ResistanceSet BaseResistances;
        public ShieldPool ShieldPool;
        
        public Inventory Inventory = new();
        public EquipmentSet Equipment = new();

        public int CurrentHealth;
        [NonSerialized] public float ExplorationStatusElapsedSeconds;
        public int Honor;
        // Temporary effective value, equipment will calculate it later.
        public float WeaponAccuracy = GameDefinesRuntime.GetFloat(
            GameDefineIds.BaseWeaponAccuracy, 100f);
        [NonSerialized] public bool DebugIsInvulnerable;

        public CharacterLifeState LifeState;
        public CharacterAvailabilityState AvailabilityState;

        public List<SkillData> Skills = new();
        public ActiveSkillLoadout ActiveSkillLoadout { get; } = new();
        public List<SkillTreeProgress> SkillTreeProgress = new();

        public List<PassiveEffectRuntime> PassiveEffects = new();

        public SkillData GetBasicAttackSkill()
        {
            if (Definition?.BasicAttackSkill != null)
            {
                return Definition.BasicAttackSkill;
            }

            // Serialized content created before the per-character basic-attack
            // field uses the first known skill as its deterministic migration.
            foreach (var skill in Skills ?? new List<SkillData>())
            {
                if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                {
                    return skill;
                }
            }

            return ClassData?.BasicAttackSkill;
        }

        public IReadOnlyList<SkillData> GetActiveSkills()
        {
            // Classes without the new explicit basic-attack reference are still
            // valid legacy content. Keep their known skills usable until the
            // serialized class configuration is completed in the Editor.
            if (ActiveSkillLoadout == null || !ActiveSkillLoadout.IsInitialized)
            {
                return Skills ?? new List<SkillData>();
            }

            return ActiveSkillLoadout.ResolveSkills(Skills);
        }

    }
}
