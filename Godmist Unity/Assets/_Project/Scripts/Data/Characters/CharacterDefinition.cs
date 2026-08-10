using System.Collections.Generic;
using Godmist.Core.Interfaces;
using Godmist.Data.Skills;
using Godmist.Data.Items;
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
        [field: SerializeField] public Sprite VisualSprite { get; private set; }
        [field: SerializeField] public List<Sprite> IdleAnimationFrames { get; private set; } = new();
        [field: SerializeField] public float IdleAnimationFramesPerSecond { get; private set; } = 4f;
        [field: SerializeField] public List<Sprite> RunAnimationFrames { get; private set; } = new();
        [field: SerializeField] public float RunAnimationFramesPerSecond { get; private set; } = 8f;
        [field: SerializeField] public Sprite HitSprite { get; private set; }
        [field: SerializeField] public bool IsMainCharacter { get; private set; }
        [field: SerializeField] public string RecruitmentQuestId { get; private set; } = string.Empty;
        [field: SerializeField] public CharacterBaseStatsData BaseStats { get; private set; } = new();
        [field: SerializeField] public List<SkillData> StartingSkills { get; private set; } = new();
        [field: SerializeField] public int ExperienceReward { get; private set; }
        [field: SerializeField] public int GoldReward { get; private set; }
        [field: SerializeField] public LootTableData LootTable { get; private set; }
    }
}
