using System.Collections.Generic;
using Godmist.Core.Interfaces;
using Godmist.Data.Skills;
using Godmist.Data.Items;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterDefinition",
        menuName = "Godmist/Characters/Character Definition")]
    public class CharacterDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public List<CharacterTag> Tags { get; private set; } = new();
        [field: SerializeField] public Sprite VisualSprite { get; private set; }
        [field: SerializeField] public Sprite PortraitSprite { get; private set; }
        public Sprite UiPortraitSprite => PortraitSprite != null ? PortraitSprite : VisualSprite;
        [field: SerializeField] public List<Sprite> IdleAnimationFrames { get; private set; } = new();
        [field: SerializeField] public float IdleAnimationFramesPerSecond { get; private set; } = 4f;
        [field: SerializeField] public List<Sprite> RunAnimationFrames { get; private set; } = new();
        [field: SerializeField] public float RunAnimationFramesPerSecond { get; private set; } = 8f;
        [field: SerializeField] public Sprite HitSprite { get; private set; }
        [field: SerializeField] public Sprite DeadSprite { get; private set; }
        [field: SerializeField] public bool IsMainCharacter { get; private set; }
        [field: SerializeField] public CombatAiRole AiRole { get; private set; } =
            CombatAiRole.Balanced;
        [field: SerializeField] public CombatAiPersonality AiPersonality { get; private set; } =
            CombatAiPersonality.Neutral;
        [field: SerializeField] public string RecruitmentQuestId { get; private set; } = string.Empty;
        [field: SerializeField] public CharacterClassData ClassData { get; private set; }
        [field: SerializeField] public CharacterBaseStatsData BaseStats { get; private set; } = new();
        [field: SerializeField] public CharacterResistanceData BaseResistances { get; private set; } = new();
        [field: SerializeField] public CharacterGrowthData GrowthData { get; private set; } = new();
        [field: SerializeField, Min(0f)] public float AttackMultiplier { get; private set; } = 1f;
        // Optional boss phase data; art is authored independently for each definition.
        [field: SerializeField] public CharacterDefinition SecondPhaseDefinition { get; private set; }
        [field: SerializeField] public float TransformationFramesPerSecond { get; private set; } = 8f;
        [field: SerializeField] public List<Sprite> TransformationFrames { get; private set; } = new();
        [field: SerializeField] public Godmist.Data.Combat.SummonedObjectDefinition SanctuaryWarning { get; private set; }
        [field: SerializeField] public Godmist.Data.Combat.SummonedObjectDefinition SanctuaryFire { get; private set; }
        [field: SerializeField] public SkillData BasicAttackSkill { get; private set; }
        [field: SerializeField] public List<SkillData> StartingSkills { get; private set; } = new();
        [field: SerializeField] public int ExperienceReward { get; private set; }
        [field: SerializeField] public int GoldReward { get; private set; }
        [field: SerializeField] public LootTableData LootTable { get; private set; }
    }
}
