using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Effects
{
    [Serializable]
    public class SkillEffectData
    {
        [SerializeField] private EffectType effectType = EffectType.DealDamage;
        [SerializeField, Min(0)] private int targetModeIndex;
        [SerializeField, Min(0)] private int baseValue;
        [SerializeField] private DamageValueSource damageValueSource = DamageValueSource.AttackRange;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private int pushDistance = 1;
        [SerializeField, Range(0f, 1f)] private float baseChance = 1f;
        [SerializeField] private string statusId = string.Empty;
        [SerializeField] private int durationTurns = 1;
        [SerializeField] private int stacks = 1;
        [SerializeField, Min(0f)] private float statusDamageMultiplier;
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
        public int TargetModeIndex => targetModeIndex;
        public int BaseValue => baseValue;
        public DamageType DamageType => damageType;
        public DamageValueSource DamageValueSource => damageValueSource;
        public float DamageMultiplier => damageMultiplier;
        public int PushDistance => pushDistance;
        public float BaseChance => baseChance;
        public string StatusId => statusId;
        public int DurationTurns => durationTurns;
        public int Stacks => stacks;
        public float StatusDamageMultiplier => statusDamageMultiplier;
        public CharacterStatType ModifierStatType => modifierStatType;
        public GameplayVariableType ModifierVariableType => modifierVariableType;
        public StatModifierOperation ModifierOperation => modifierOperation;
        public StatModifierStacking ModifierStacking => modifierStacking;
        public float ModifierValue => modifierValue;

        public IReadOnlyList<SkillEffectConditionData>
            Conditions => conditions;
    }
}
