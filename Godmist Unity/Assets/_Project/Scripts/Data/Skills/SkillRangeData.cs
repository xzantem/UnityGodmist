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

        public RangeShape Shape => shape;
        public int MinRange => Mathf.Max(0, minRange);
        public int MaxRange => Mathf.Max(MinRange, maxRange);

        public SkillRangeData()
        {
        }

        public SkillRangeData(
            RangeShape shape,
            int minRange,
            int maxRange)
        {
            this.shape = shape;
            this.minRange = Mathf.Max(0, minRange);
            this.maxRange = Mathf.Max(
                this.minRange,
                maxRange);
        }
    }
}