using System;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [Serializable]
    public class CharacterGrowthData
    {
        [field: SerializeField] public float MaxHealth { get; private set; }
        [field: SerializeField] public float MinAttack { get; private set; }
        [field: SerializeField] public float MaxAttack { get; private set; }
        [field: SerializeField] public float PhysicalDefense { get; private set; }
        [field: SerializeField] public float MagicalDefense { get; private set; }
        [field: SerializeField] public float Dodge { get; private set; }
        [field: SerializeField] public float CritChance { get; private set; }
        [field: SerializeField] public float CritDamage { get; private set; }
        [field: SerializeField] public float Speed { get; private set; }
        [field: SerializeField] public float WeaponAccuracy { get; private set; }
        [field: SerializeField] public float MaxResource { get; private set; }
        [field: SerializeField] public float ResourceRegeneration { get; private set; }
    }
}
