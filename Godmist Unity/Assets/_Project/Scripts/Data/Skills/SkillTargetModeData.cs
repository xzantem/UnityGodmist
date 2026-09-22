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

        // Most ally-targeted skills intentionally exclude their caster. Runtime
        // consumables are the exception: a potion must be usable on the active
        // character as well as on another ally.
        [SerializeField] private bool includesCaster;

        [Header("Absolute Range")]
        [SerializeField] private SkillRangeData absoluteRange =
            new();

        [Header("Relative Range")]
        [SerializeField] private SkillRangeData relativeRange =
            new();

        public TargetType TargetType => targetType;
        public TargetingType TargetingType => targetingType;
        public bool IncludesCaster => includesCaster;
        public SkillRangeData AbsoluteRange => absoluteRange;
        public bool UseRelativeRange => SupportsRelativeRange(TargetType);
        public SkillRangeData RelativeRange => relativeRange;

        public SkillTargetModeData WithRuntimeRanges(int absoluteMaximum, int relativeMaximum)
        {
            if (absoluteMaximum == absoluteRange.MaxRange && relativeMaximum == relativeRange.MaxRange) return this;
            var copy = (SkillTargetModeData)MemberwiseClone();
            copy.absoluteRange = new SkillRangeData(absoluteRange.Shape, absoluteRange.MinRange, absoluteMaximum, absoluteRange.RectangleWidth);
            copy.relativeRange = new SkillRangeData(relativeRange.Shape, relativeRange.MinRange, relativeMaximum, relativeRange.RectangleWidth);
            return copy;
        }

        public static bool SupportsRelativeRange(TargetType targetType)
        {
            return targetType == TargetType.AreaAroundTile;
        }
    }
}
