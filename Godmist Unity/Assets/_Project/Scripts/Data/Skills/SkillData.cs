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
        [field: SerializeField, Min(0)] public int ToggleUpkeepFlatResourceCost { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float ToggleUpkeepMaxResourcePercent { get; private set; }
        [field: SerializeField] public bool RequiresLineOfSight { get; private set; }
        [SerializeField] private List<SkillTargetModeData> targetModes = new();
        [SerializeField] private List<SkillEffectData> effects = new();

        public IReadOnlyList<SkillTargetModeData> TargetModes => targetModes;
        public IReadOnlyList<SkillEffectData> Effects => effects;
    }

    /// <summary>
    /// Defines effects whose runtime state can be kept and deterministically removed
    /// when a toggle is disabled.
    /// </summary>
    public static class ToggleSkillRules
    {
        public static readonly EffectType[] AllowedEffectTypes =
        {
            EffectType.ModifyStat,
            EffectType.ModifyVar,
            EffectType.ApplyUniqueBuff,
            EffectType.BlockResourceRegeneration
        };

        public static readonly UniqueBuffType[] AllowedUniqueBuffTypes =
        {
            UniqueBuffType.ActionPointsNextTurn,
            UniqueBuffType.OnHitStatus,
            UniqueBuffType.DamageRedirect,
            UniqueBuffType.AuraStatus
        };

        public static bool SupportsToggle(EffectType effectType)
        {
            return effectType == EffectType.ModifyStat
                || effectType == EffectType.ModifyVar
                || effectType == EffectType.ApplyUniqueBuff
                || effectType == EffectType.BlockResourceRegeneration;
        }

        public static bool SupportsToggle(
            EffectType effectType,
            UniqueBuffType uniqueBuffType)
        {
            if (effectType != EffectType.ApplyUniqueBuff)
            {
                return SupportsToggle(effectType);
            }

            return uniqueBuffType == UniqueBuffType.ActionPointsNextTurn
                || uniqueBuffType == UniqueBuffType.OnHitStatus
                || uniqueBuffType == UniqueBuffType.DamageRedirect
                || uniqueBuffType == UniqueBuffType.AuraStatus;
        }

        public static bool IsValid(SkillData skill)
        {
            if (skill?.IsToggle != true
                || skill.Effects == null
                || skill.Effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect == null
                    || !SupportsToggle(effect.EffectType, effect.UniqueBuffType)
                    || effect.DelayCasterTurns > 0
                    || (effect.Conditions != null && effect.Conditions.Count > 0))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
