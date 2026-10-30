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
        [SerializeField] private string stableEffectId = Guid.NewGuid().ToString("N");
        public string StableId => stableEffectId;
        public static SkillEffectData ForDamage(DamageType type,DamageValueSource source,float multiplier)
            => new() { effectType=EffectType.DealDamage,damageType=type,damageValueSource=source,damageMultiplier=multiplier };

        public SkillEffectData WithRuntimeParameters(int hits, int duration, float chance)
        {
            if (hits == damageHitCount && duration == durationTurns && Mathf.Approximately(chance, baseChance)) return this;
            var copy = (SkillEffectData)MemberwiseClone();
            copy.damageHitCount = Mathf.Max(1, hits);
            copy.durationTurns = Mathf.Max(0, duration);
            copy.baseChance = Mathf.Clamp01(chance);
            return copy;
        }

        [SerializeField] private EffectType effectType = EffectType.DealDamage;
        [SerializeField] private EffectPolarity polarity = EffectPolarity.Negative;
        [SerializeField] private bool alwaysHits;
        [SerializeField, Min(0)] private int targetModeIndex;
        [SerializeField, Min(0)] private int baseValue;
        [SerializeField, Min(0f)] private float healAverageAttackMultiplier;
        [SerializeField] private DamageValueSource damageValueSource =
            DamageValueSource.AttackRange;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float rearAttackDamageBonus;
        [SerializeField, Min(1)] private int damageHitCount = 1;
        [SerializeField, Min(0)] private int additionalHitTrials;
        [SerializeField, Range(0f, 1f)] private float additionalHitChance = .5f;
        [SerializeField, Min(0f)] private float bonusPenetration;
        [SerializeField, Range(0f, 1f)] private float bonusDamageMultiplierChance;
        [SerializeField, Min(1f)] private float bonusDamageMultiplier = 1f;
        [SerializeField] private string onHitStatusId = string.Empty;
        [SerializeField, Range(0f, 1f)] private float onHitStatusChance = 1f;
        [SerializeField, Min(1)] private int onHitStatusDurationTurns = 1;
        [SerializeField, Min(0f)] private float lifeStealPercent;
        [SerializeField, Range(0f, 1f)] private float allyLifeStealPercent;
        [SerializeField, Range(0f, 1f)] private float resourceDrainPercent;
        [SerializeField] private DamageType damageType = DamageType.Physical;

        [Header("Scheduled Effect")]
        [SerializeField, Min(0)] private int delayCasterTurns;

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
        [SerializeField] private List<CharacterDefinition> summonPool = new();
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
        [SerializeField, Range(0f, 1f)] private float percentageValue;

        [Header("Counterattack")]
        [SerializeField] private SkillRangeData counterattackRange =
            new SkillRangeData(RangeShape.Square, 1, 1);
        [SerializeField] private List<SkillEffectData> counterattackEffects = new();

        [SerializeField] private CharacterStatType modifierStatType =
            CharacterStatType.MaxHealth;

        [SerializeField] private GameplayVariableType modifierVariableType =
            GameplayVariableType.DamageDealtPercent;

        [SerializeField] private StatModifierOperation modifierOperation =
            StatModifierOperation.FlatBase;

        [SerializeField] private StatModifierStacking modifierStacking =
            StatModifierStacking.Additive;

        [SerializeField] private float modifierValue;
        [SerializeField] private UniqueBuffType uniqueBuffType;
        [SerializeField, Min(1)] private int uniqueBuffStacks = 1;
        [SerializeField] private ResistanceType resistanceType;
        [SerializeField, Min(0)] private int actionPointModifier;
        [SerializeField] private float actionValuePercent;
        [SerializeField, Min(0)] private int resourceRegenerationBlockTurns;
        [SerializeField, Range(0f, 1f)] private float healthSacrificePercent;
        [SerializeField, Min(0f)] private float resourceRestorePerSacrificedHealth;
        [SerializeField, Range(0f, 1f)] private float redirectedDamagePercent;
        [SerializeField, Min(0)] private int auraRadius;
        [SerializeField] private bool auraIncludesCaster;
        [SerializeField] private TargetingType auraTargetingType = TargetingType.Allies;
        [SerializeField, Range(0f, 10f)] private float shieldAverageAttackMultiplier;
        [SerializeField] private List<SkillEffectConditionData> conditions = new();

        public EffectType EffectType => effectType;
        public EffectPolarity Polarity => polarity;
        public bool AlwaysHits => alwaysHits;
        public int TargetModeIndex => targetModeIndex;
        public int BaseValue => baseValue;
        public float HealAverageAttackMultiplier => Mathf.Max(0f, healAverageAttackMultiplier);
        public DamageType DamageType => damageType;
        public DamageValueSource DamageValueSource => damageValueSource;
        public float DamageMultiplier => damageMultiplier;
        public float RearAttackDamageBonus => Mathf.Max(0f, rearAttackDamageBonus);
        public int DamageHitCount => Mathf.Max(1, damageHitCount);
        public int AdditionalHitTrials => Mathf.Max(0, additionalHitTrials);
        public float AdditionalHitChance => Mathf.Clamp01(additionalHitChance);
        public float ExpectedHitCount => DamageHitCount + AdditionalHitTrials * AdditionalHitChance;
        public int MaximumHitCount => DamageHitCount + (AdditionalHitChance > 0f ? AdditionalHitTrials : 0);

        public SkillEffectData DeepCopy() => DeepCopy(new Dictionary<SkillEffectData, SkillEffectData>());
        private SkillEffectData DeepCopy(Dictionary<SkillEffectData, SkillEffectData> copies)
        {
            if (copies.TryGetValue(this, out var existing)) return existing;
            var copy = (SkillEffectData)MemberwiseClone(); copies.Add(this, copy);
            copy.teleportDestinationRange = teleportDestinationRange?.DeepCopy();
            copy.objectPlacementRange = objectPlacementRange?.DeepCopy();
            copy.summonPlacementRange = summonPlacementRange?.DeepCopy();
            copy.counterattackRange = counterattackRange?.DeepCopy();
            copy.summonPool = summonPool == null ? new List<CharacterDefinition>() : new List<CharacterDefinition>(summonPool);
            copy.conditions = new List<SkillEffectConditionData>();
            foreach (var condition in conditions ?? new List<SkillEffectConditionData>()) copy.conditions.Add(condition?.DeepCopy());
            copy.counterattackEffects = new List<SkillEffectData>();
            foreach (var effect in counterattackEffects ?? new List<SkillEffectData>()) copy.counterattackEffects.Add(effect?.DeepCopy(copies));
            return copy;
        }

        public SkillEffectData WithAdditionalHitTrials(int trials)
        {
            if (trials == additionalHitTrials) return this;
            var copy = (SkillEffectData)MemberwiseClone();
            copy.additionalHitTrials = Mathf.Max(0, trials);
            return copy;
        }
        public SkillEffectData WithScoutParameters(int extraHits, float doubleChance, float dotMultiplier, float statValue, int turns)
        {
            var copy = (SkillEffectData)MemberwiseClone();
            copy.additionalHitTrials = Mathf.Max(0, extraHits);
            copy.bonusDamageMultiplierChance = Mathf.Clamp01(doubleChance);
            copy.statusDamageMultiplier = dotMultiplier;
            copy.modifierValue = statValue;
            copy.durationTurns = turns;
            return copy;
        }
        public SkillEffectData WithDamageMultiplier(float value)
        {
            var copy = (SkillEffectData)MemberwiseClone();
            copy.damageMultiplier = Mathf.Max(0, value);
            return copy;
        }
        public SkillEffectData WithBonusPenetration(float value)
        {
            var copy = (SkillEffectData)MemberwiseClone();
            copy.bonusPenetration = Mathf.Max(0, value);
            return copy;
        }
        public float BonusPenetration => Mathf.Max(0f, bonusPenetration);
        public float BonusDamageMultiplierChance => Mathf.Clamp01(bonusDamageMultiplierChance);
        public float BonusDamageMultiplier => Mathf.Max(1f, bonusDamageMultiplier);
        public string OnHitStatusId => onHitStatusId;
        public float OnHitStatusChance => Mathf.Clamp01(onHitStatusChance);
        public int OnHitStatusDurationTurns => Mathf.Max(1, onHitStatusDurationTurns);
        public float LifeStealPercent => Mathf.Max(0f, lifeStealPercent);
        public float AllyLifeStealPercent => Mathf.Clamp01(allyLifeStealPercent);
        public float ResourceDrainPercent => Mathf.Clamp01(resourceDrainPercent);
        public int DelayCasterTurns => Mathf.Max(0, delayCasterTurns);
        public int DisplacementDistance => displacementDistance;
        public SkillEffectData WithDisplacementDistance(int value)
        { var copy=(SkillEffectData)MemberwiseClone();copy.displacementDistance=value;return copy; }
        public SkillRangeData TeleportDestinationRange => teleportDestinationRange;
        public SummonedObjectDefinition SummonedObjectDefinition =>
            summonedObjectDefinition;

        public SkillRangeData ObjectPlacementRange =>
            objectPlacementRange;

        public int MaxActiveObjects => Mathf.Max(1, maxActiveObjects);
        [SerializeField] private bool summonUsesCasterStats;
        [SerializeField] private bool summonStationary;
        [SerializeField] private bool summonPersistent;
        [SerializeField] private string summonGroupId;
        [SerializeField] private string damagePerOwnedSummonGroup;
        public bool SummonUsesCasterStats => summonUsesCasterStats;
        public bool SummonStationary => summonStationary;
        public bool SummonPersistent => summonPersistent;
        public string SummonGroupId => summonGroupId;
        public string DamagePerOwnedSummonGroup => damagePerOwnedSummonGroup;
        public CharacterDefinition SummonedCharacterDefinition => summonedCharacterDefinition;
        public IReadOnlyList<CharacterDefinition> SummonPool => summonPool;
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
        public float PercentageValue => Mathf.Clamp01(percentageValue);
        public SkillEffectData WithPercentageValue(float value)
        {
            var copy = (SkillEffectData)MemberwiseClone();
            copy.percentageValue = Mathf.Clamp01(value);
            return copy;
        }
        public SkillRangeData CounterattackRange => counterattackRange;
        public IReadOnlyList<SkillEffectData> CounterattackEffects =>
            counterattackEffects;
        public CharacterStatType ModifierStatType => modifierStatType;
        public GameplayVariableType ModifierVariableType =>
            modifierVariableType;

        public StatModifierOperation ModifierOperation =>
            modifierOperation;

        public StatModifierStacking ModifierStacking =>
            modifierStacking;

        public float ModifierValue => modifierValue;
        public UniqueBuffType UniqueBuffType => uniqueBuffType;
        public int UniqueBuffStacks => Mathf.Max(1, uniqueBuffStacks);
        public ResistanceType ResistanceType => resistanceType;
        public int ActionPointModifier => actionPointModifier;
        public float ActionValuePercent => actionValuePercent;
        public int ResourceRegenerationBlockTurns => Mathf.Max(0, resourceRegenerationBlockTurns);
        public float HealthSacrificePercent => Mathf.Clamp01(healthSacrificePercent);
        public float ResourceRestorePerSacrificedHealth => Mathf.Max(0f, resourceRestorePerSacrificedHealth);
        public float RedirectedDamagePercent => Mathf.Clamp01(redirectedDamagePercent);
        public int AuraRadius => Mathf.Max(0, auraRadius);
        public bool AuraIncludesCaster => auraIncludesCaster;
        public TargetingType AuraTargetingType => auraTargetingType;
        public float ShieldAverageAttackMultiplier => Mathf.Max(0f, shieldAverageAttackMultiplier);

        public IReadOnlyList<SkillEffectConditionData>
            Conditions => conditions;
    }
}
