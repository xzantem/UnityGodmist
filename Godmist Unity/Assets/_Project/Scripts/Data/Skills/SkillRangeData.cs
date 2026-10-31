using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Skills
{
    [Serializable]
    public class SkillRangeData
    {
        [SerializeField] private RangeShape shape =
            RangeShape.Square;

        [SerializeField, Min(0)] private int minRange;
        [SerializeField, Min(0)] private int maxRange = 1;
        [SerializeField, Min(1)] private int rectangleWidth = 1;

        public RangeShape Shape => shape;
        public int MinRange => Mathf.Max(0, minRange);
        public int MaxRange => Mathf.Max(MinRange, maxRange);
        public int RectangleWidth => Mathf.Max(1, rectangleWidth);

        public SkillRangeData DeepCopy() => (SkillRangeData)MemberwiseClone();

        public SkillRangeData()
        {
        }

        public SkillRangeData(
            RangeShape shape,
            int minRange,
            int maxRange,
            int rectangleWidth = 1)
        {
            this.shape = shape;
            this.minRange = Mathf.Max(0, minRange);
            this.maxRange = Mathf.Max(
                this.minRange,
                maxRange);
            this.rectangleWidth = Mathf.Max(1, rectangleWidth);
        }
    }
}
