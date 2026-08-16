using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using UnityEngine;

namespace Godmist.Data.Combat
{
    [CreateAssetMenu(
        fileName = "SummonedObjectDefinition",
        menuName = "Godmist/Combat/Summoned Object Definition")]
    public class SummonedObjectDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite worldSprite;

        [Header("Lifetime")]
        [SerializeField, Min(1)] private int maxHits = 1;
        [SerializeField, Min(1)] private int defaultDurationTurns = 1;
        [SerializeField, Min(0.01f)] private float turnSpeed = 10f;

        [Header("Map Rules")]
        [SerializeField] private bool blocksMovement = true;
        [SerializeField] private bool blocksLineOfSight;

        [Header("Automatic Effects")]
        [SerializeField] private TargetingType effectTargetingType =
            TargetingType.Enemies;

        [SerializeField] private SkillRangeData effectRange =
            new(RangeShape.Square, 0, 1);

        [SerializeField, Range(0f, 100f)] private float effectAccuracy = 100f;

        [SerializeField] private List<SummonedObjectEffectData> effects = new();

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Sprite WorldSprite => worldSprite;
        public int MaxHits => Mathf.Max(1, maxHits);
        public int DefaultDurationTurns => Mathf.Max(1, defaultDurationTurns);
        public float TurnSpeed => Mathf.Max(0.01f, turnSpeed);
        public bool BlocksMovement => blocksMovement;
        public bool BlocksLineOfSight => blocksLineOfSight;
        public TargetingType EffectTargetingType => effectTargetingType;
        public SkillRangeData EffectRange => effectRange;
        public float EffectAccuracy => Mathf.Clamp(effectAccuracy, 0f, 100f);
        public IReadOnlyList<SummonedObjectEffectData> Effects => effects;
    }
}
