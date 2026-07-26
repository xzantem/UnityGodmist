using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Skills
{
    [Serializable]
    public class SkillTargetModeData
    {
        [SerializeField] private TargetType targetType =
            TargetType.Single;

        [SerializeField] private TargetingType targetingType =
            TargetingType.Enemies;

        [Header("Absolute Range")]
        [SerializeField] private SkillRangeData absoluteRange =
            new();

        [Header("Relative Range")]
        [SerializeField] private bool useRelativeRange;
        [SerializeField] private SkillRangeData relativeRange =
            new();

        public TargetType TargetType => targetType;
        public TargetingType TargetingType => targetingType;
        public SkillRangeData AbsoluteRange => absoluteRange;
        public bool UseRelativeRange => SupportsRelativeRange(TargetType) && useRelativeRange;
        public SkillRangeData RelativeRange => relativeRange;

        public static bool SupportsRelativeRange(TargetType targetType)
        {
            return targetType == TargetType.Single
                || targetType == TargetType.AreaAroundTile;
        }
    }
}
