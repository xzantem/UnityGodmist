using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using UnityEngine;
using UnityEngine.Serialization;

namespace Godmist.Data.Effects
{
    [Serializable]
    public class SkillEffectData
    {
        [SerializeField] private EffectType effectType = EffectType.DealDamage;
        [SerializeField] private EffectPolarity polarity = EffectPolarity.Negative;
        [SerializeField] private bool alwaysHits;
        [SerializeField, Min(0)] private int targetModeIndex;
        [SerializeField, Min(0)] private int baseValue;
        [SerializeField] private DamageValueSource damageValueSource =
            DamageValueSource.AttackRange;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField, Min(1)] private int damageHitCount = 1;
        [SerializeField] private DamageType damageType = DamageType.Physical;

        [FormerlySerializedAs("pushDistance")]
        [SerializeField, Min(1)] private int displacementDistance = 1;

        [SerializeField] private SkillRangeData teleportDestinationRange =
            new SkillRangeData(RangeShape.Square, 0, 2);

        [Header("Create Object")]
        [SerializeField] private SummonedObjectDefinition summonedObjectDefinition;

        [SerializeField] private SkillRangeData objectPlacementRange =
            new SkillRangeData(RangeShape.Square, 0, 2);

        [SerializeField, Min(1)] private int maxActiveObjects = 1;

        [Header("Summon")]
        [SerializeField] private CharacterDefinition summonedCharacterDefinition;
        [SerializeField] private CharacterClassData summonedClassData;
        [SerializeField] private SkillRangeData summonPlacementRange =
            new SkillRangeData(RangeShape.Square, 1, 2);
        [SerializeField, Min(1)] private int summonDurationTurns = 1;
        [SerializeField, Min(1)] private int maxActiveSummons = 1;
        [SerializeField, Range(0.01f, 1f)] private float summonPercentMaxHealth = 1f;

        [SerializeField, Range(0f, 1f)] private float baseChance = 1f;
        [SerializeField] private string statusId = string.Empty;
        [SerializeField] private int durationTurns = 1;
        [SerializeField] private int stacks = 1;
        [SerializeField, Min(0f)] private float statusDamageMultiplier;
        [SerializeField, Range(0f, 1f)] private float statusHealPercent;

        [SerializeField] private CharacterStatType modifierStatType =
            CharacterStatType.MaxHealth;

        [SerializeField] private GameplayVariableType modifierVariableType =
            GameplayVariableType.DamageDealtPercent;

        [SerializeField] private StatModifierOperation modifierOperation =
            StatModifierOperation.FlatBase;

        [SerializeField] private StatModifierStacking modifierStacking =
            StatModifierStacking.Additive;

        [SerializeField] private float modifierValue;
        [SerializeField] private List<SkillEffectConditionData> conditions = new();

        public EffectType EffectType => effectType;
        public EffectPolarity Polarity => polarity;
        public bool AlwaysHits => alwaysHits;
        public int TargetModeIndex => targetModeIndex;
        public int BaseValue => baseValue;
        public DamageType DamageType => damageType;
        public DamageValueSource DamageValueSource => damageValueSource;
        public float DamageMultiplier => damageMultiplier;
        public int DamageHitCount => Mathf.Max(1, damageHitCount);
        public int DisplacementDistance => displacementDistance;
        public SkillRangeData TeleportDestinationRange => teleportDestinationRange;
        public SummonedObjectDefinition SummonedObjectDefinition =>
            summonedObjectDefinition;

        public SkillRangeData ObjectPlacementRange =>
            objectPlacementRange;

        public int MaxActiveObjects => Mathf.Max(1, maxActiveObjects);
        public CharacterDefinition SummonedCharacterDefinition => summonedCharacterDefinition;
        public CharacterClassData SummonedClassData => summonedClassData;
        public SkillRangeData SummonPlacementRange => summonPlacementRange;
        public int SummonDurationTurns => Mathf.Max(1, summonDurationTurns);
        public int MaxActiveSummons => Mathf.Max(1, maxActiveSummons);
        public float SummonPercentMaxHealth =>
            Mathf.Clamp(summonPercentMaxHealth, 0.01f, 1f);

        public float BaseChance => baseChance;
        public string StatusId => statusId;
        public int DurationTurns => durationTurns;
        public int Stacks => stacks;
        public float StatusDamageMultiplier => statusDamageMultiplier;
        public float StatusHealPercent => statusHealPercent;
        public CharacterStatType ModifierStatType => modifierStatType;
        public GameplayVariableType ModifierVariableType =>
            modifierVariableType;

        public StatModifierOperation ModifierOperation =>
            modifierOperation;

        public StatModifierStacking ModifierStacking =>
            modifierStacking;

        public float ModifierValue => modifierValue;

        public IReadOnlyList<SkillEffectConditionData>
            Conditions => conditions;
    }
}
