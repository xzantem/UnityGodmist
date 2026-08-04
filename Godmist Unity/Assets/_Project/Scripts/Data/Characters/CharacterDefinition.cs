using System.Collections.Generic;
using Godmist.Core.Interfaces;
using Godmist.Data.Skills;
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
        [field: SerializeField] public bool IsMainCharacter { get; private set; }
        [field: SerializeField] public string RecruitmentQuestId { get; private set; } = string.Empty;
        [field: SerializeField] public CharacterBaseStatsData BaseStats { get; private set; } = new();
        [field: SerializeField] public List<SkillData> StartingSkills { get; private set; } = new();
    }
}