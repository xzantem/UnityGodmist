using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Effects
{
    [Serializable]
    public class SkillEffectConditionData
    {
        [SerializeField]
        private EffectConditionType conditionType =
            EffectConditionType.Always;

        [SerializeField]
        private int referencedEffectIndex = -1;

        [SerializeField]
        private string statusId = string.Empty;

        [SerializeField, Range(0f, 1f)]
        private float healthPercentThreshold = 0.5f;

        [SerializeField, Min(0)]
        private int integerThreshold;


        public EffectConditionType ConditionType =>
            conditionType;

        public int ReferencedEffectIndex => referencedEffectIndex;

        public string StatusId => statusId;

        public float HealthPercentThreshold =>
            healthPercentThreshold;

        public int IntegerThreshold => integerThreshold;

    }
}
