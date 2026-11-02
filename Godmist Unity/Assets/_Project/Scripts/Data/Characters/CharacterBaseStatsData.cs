using System;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [Serializable]
    public class CharacterBaseStatsData
    {
        [field: SerializeField] public int MaxHealth { get; private set; } = 100;
        [field: SerializeField] public float MinAttack { get; private set; } = 8;
        [field: SerializeField] public float MaxAttack { get; private set; } = 12;

        [field: SerializeField] public float PhysicalDefense { get; private set; } = 10f;
        [field: SerializeField] public float MagicalDefense { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float PhysicalDefensePenetration { get; private set; }
        [field: SerializeField, Min(0f)] public float MagicalDefensePenetration { get; private set; }
        [field: SerializeField] public float Dodge { get; private set; } = 15f;
        [field: SerializeField] public float CritChance { get; private set; } = 0.25f;
        [field: SerializeField] public float CritDamage { get; private set; } = 1.5f;
        [field: SerializeField] public float Speed { get; private set; } = 50f;
        [field: SerializeField] public float WeaponAccuracy { get; private set; } = 100f;

        [field: SerializeField] public int MaxResource { get; private set; } = 100;
        [field: SerializeField] public int ResourceRegeneration { get; private set; }
        [field: SerializeField] public int HealingPower { get; private set; }
    }

    [Serializable]
    public class CharacterResistanceData
    {
        [field: SerializeField, Range(0f, 1f)] public float BleedResistance { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float PoisonResistance { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float FireResistance { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float WeakeningResistance { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float SuppressionResistance { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float MoveResistance { get; private set; }
    }
}
