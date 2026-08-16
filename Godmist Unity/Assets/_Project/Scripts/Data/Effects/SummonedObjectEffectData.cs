using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Effects
{
    [Serializable]
    public class SummonedObjectEffectData
    {
        [SerializeField] private SummonedObjectEffectType effectType =
            SummonedObjectEffectType.DealDamage;
        [SerializeField] private int baseValue;
        [SerializeField] private DamageValueSource damageValueSource =
            DamageValueSource.AverageAttack;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
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

        public SummonedObjectEffectType EffectType => effectType;
        public int BaseValue => baseValue;
        public DamageValueSource DamageValueSource => damageValueSource;
        public float DamageMultiplier => damageMultiplier;
        public DamageType DamageType => damageType;
        public float BaseChance => baseChance;
        public string StatusId => statusId;
        public int DurationTurns => durationTurns;
        public int Stacks => stacks;
        public float StatusDamageMultiplier => statusDamageMultiplier;
        public float StatusHealPercent => statusHealPercent;
        public CharacterStatType ModifierStatType => modifierStatType;
        public GameplayVariableType ModifierVariableType => modifierVariableType;
        public StatModifierOperation ModifierOperation => modifierOperation;
        public StatModifierStacking ModifierStacking => modifierStacking;
        public float ModifierValue => modifierValue;
    }
}
