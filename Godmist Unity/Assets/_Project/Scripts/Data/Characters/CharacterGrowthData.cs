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
        // Optional content bonus. Zero preserves the legacy curve exactly;
        // CharacterLevelGrowthSystem enables it gradually from level10 to48.
        [field: SerializeField] public float LateMaxHealth { get; private set; }
        [field: SerializeField] public float LateMinAttack { get; private set; }
        [field: SerializeField] public float LateMaxAttack { get; private set; }
        // Optional early adjustment, weighted by the part of the growth curve
        // not yet covered by Late growth. Its contribution is zero at48..50.
        [field: SerializeField] public float EarlyMaxHealth { get; private set; }
        [field: SerializeField] public float EarlyMinAttack { get; private set; }
        [field: SerializeField] public float EarlyMaxAttack { get; private set; }
        // Flat corrections blended with the same late-growth curve (not per growth unit).
        // They let content raise starting stats without moving either the L48 or L50 anchor.
        // Zero preserves legacy definitions and all hero growth exactly.
        [field: SerializeField] public float LateMaxHealthOffset { get; private set; }
        // Optional absolute health anchors, interpolated in cumulative growth units.
        // Empty preserves legacy health growth, including heroes and bosses.
        [field: SerializeField] public CharacterHealthGrowthPoint[] HealthCurve { get; private set; } = Array.Empty<CharacterHealthGrowthPoint>();
        [field: SerializeField] public float LateMinAttackOffset { get; private set; }
        [field: SerializeField] public float LateMaxAttackOffset { get; private set; }
        [field: SerializeField] public float PhysicalDefense { get; private set; }
        [field: SerializeField] public float MagicalDefense { get; private set; }
        // Optional absolute defense anchors. Empty keeps the original linear growth.
        [field: SerializeField] public CharacterDefenseGrowthPoint[] DefenseCurve { get; private set; } = Array.Empty<CharacterDefenseGrowthPoint>();
        [field: SerializeField] public float Dodge { get; private set; }
        [field: SerializeField] public float CritChance { get; private set; }
        [field: SerializeField] public float CritDamage { get; private set; }
        [field: SerializeField] public float Speed { get; private set; }
        [field: SerializeField] public float WeaponAccuracy { get; private set; }
        [field: SerializeField] public float MaxResource { get; private set; }
        [field: SerializeField] public float ResourceRegeneration { get; private set; }
    }
}
