using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Data.Skills;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterClassData",
        menuName = "Godmist/Characters/Character Class Data")]
    public class CharacterClassData : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string ClassName { get; private set; } = string.Empty;
        [field: SerializeField] public ResourceType ResourceType { get; private set; } = ResourceType.None;
        // -1 preserves the legacy start rule (full Mana, zero other resources).
        [field: SerializeField, Range(-1, 100)] public int StartingResourcePercent { get; private set; } = -1;
        [field: SerializeField] public SkillData BasicAttackSkill { get; private set; }
        [field: SerializeField] public List<SkillTreeDefinition> SkillTrees { get; private set; } = new();
    }
}
