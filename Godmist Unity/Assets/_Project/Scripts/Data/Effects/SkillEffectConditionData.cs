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
        private string statusId = string.Empty;

        [SerializeField, Range(0f, 1f)]
        private float healthPercentThreshold = 0.5f;

        public EffectConditionType ConditionType =>
            conditionType;

        public string StatusId => statusId;

        public float HealthPercentThreshold =>
            healthPercentThreshold;
    }
}