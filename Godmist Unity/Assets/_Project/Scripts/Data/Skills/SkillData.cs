using Godmist.Core.Enums;
using UnityEngine;
using System.Collections.Generic;
using Godmist.Data.Effects;

namespace Godmist.Data.Skills
{
    [CreateAssetMenu(
        fileName = "SkillData",
        menuName = "Godmist/Skills/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [field: SerializeField] public string SkillId { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea(2, 5)] public string TooltipDescription { get; private set; }
        [field: SerializeField, Min(0f)] public float SkillAccuracy { get; private set; } = 100f;
        [field: SerializeField] public int ActionCost { get; private set; }
        [field: SerializeField] public int ResourceCost { get; private set; }
        [field: SerializeField] public ResourceType ResourceType { get; private set; }
        [field: SerializeField] public bool IsToggle { get; private set; }
        [field: SerializeField] public bool RequiresLineOfSight { get; private set; }
        [SerializeField] private List<SkillTargetModeData> targetModes = new();
        [SerializeField] private List<SkillEffectData> effects = new();

        public IReadOnlyList<SkillTargetModeData> TargetModes => targetModes;
        public IReadOnlyList<SkillEffectData> Effects => effects;
    }
}
