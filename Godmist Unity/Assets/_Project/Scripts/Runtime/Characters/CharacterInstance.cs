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
        [field: NonSerialized] public event Action<int, int> ExperienceGained;
        internal void NotifyExperienceGained(int amount, int levels) => ExperienceGained?.Invoke(amount, levels);

        public CharacterStats Stats;
        public ResourcePool ResourcePool;
        public ResistanceSet Resistances;
        public ResistanceSet BaseResistances;
        public ShieldPool ShieldPool;
        
        public Inventory Inventory = new();
        public EquipmentSet Equipment = new();

        public int CurrentHealth;
        // Observation only: actual HP restored, optional origin, feedback availability.
        // Per-instance and transient; consumers must unsubscribe at combat end.
        [field: NonSerialized] public event Action<int,string,bool> HealingReceived;
        internal void NotifyHealingReceived(int amount,string origin,bool publishedFeedback)
            => HealingReceived?.Invoke(amount,origin,publishedFeedback);
        public float EquipmentHealthCarry, EquipmentResourceCarry, EquipmentGenerationCarry;
        [NonSerialized] public float ExplorationStatusElapsedSeconds;
        [NonSerialized] public float ExplorationMomentumIdleSeconds;
        [NonSerialized] public float ExplorationMomentumDistanceRemainder;
        [NonSerialized] public int ExplorationMomentumSteps;
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

        // Tree grants are derived from investments, never persisted as permanent skills.
        public IReadOnlyList<SkillData> GetKnownSkills()
        {
            var result = new List<SkillData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in Skills ?? new List<SkillData>())
                if (skill != null && ids.Add(skill.SkillId)) result.Add(skill);
            foreach (var tree in ClassData?.SkillTrees ?? new List<SkillTreeDefinition>())
            {
                if (tree == null) continue;
                var progress = SkillTreeProgress?.Find(p => p.TreeId == tree.Id);
                foreach (var node in tree.Nodes)
                    if (node?.GrantedSkill != null && progress?.NodeInvestments?.Exists(i => i.NodeId == node.Id && i.Ranks > 0) == true
                        && ids.Add(node.GrantedSkill.SkillId)) result.Add(node.GrantedSkill);
            }
            return result;
        }

        public IReadOnlyList<SkillData> GetActiveSkills()
        {
            // Classes without the new explicit basic-attack reference are still
            // valid legacy content. Keep their known skills usable until the
            // serialized class configuration is completed in the Editor.
            if (ActiveSkillLoadout == null || !ActiveSkillLoadout.IsInitialized)
            {
                return GetKnownSkills();
            }

            return ActiveSkillLoadout.ResolveSkills(GetKnownSkills());
        }

    }
}
