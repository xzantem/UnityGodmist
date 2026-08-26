using System;
using Godmist.Core.Enums;
using Godmist.Core.Types;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public class CharacterStats
    {
        public Stat MaxHealth;
        public Stat MinAttack;
        public Stat MaxAttack;
        public Stat PhysicalDefense;
        public Stat MagicalDefense;
        public Stat PhysicalDefensePenetration;
        public Stat MagicalDefensePenetration;
        public Stat Dodge;
        public Stat CritChance;
        public Stat CritDamage;
        public Stat Speed;
        public Stat ResourceRegeneration;
        public Stat WeaponAccuracy;
        public Stat MaxResource;

        public ref Stat GetStat(CharacterStatType statType)
        {
            switch (statType)
            {
                case CharacterStatType.MaxHealth:
                    return ref MaxHealth;
                case CharacterStatType.MinAttack:
                    return ref MinAttack;
                case CharacterStatType.MaxAttack:
                    return ref MaxAttack;
                case CharacterStatType.PhysicalDefense:
                    return ref PhysicalDefense;
                case CharacterStatType.MagicalDefense:
                    return ref MagicalDefense;
                case CharacterStatType.PhysicalDefensePenetration:
                    return ref PhysicalDefensePenetration;
                case CharacterStatType.MagicalDefensePenetration:
                    return ref MagicalDefensePenetration;
                case CharacterStatType.Dodge:
                    return ref Dodge;
                case CharacterStatType.CritChance:
                    return ref CritChance;
                case CharacterStatType.CritDamage:
                    return ref CritDamage;
                case CharacterStatType.Speed:
                    return ref Speed;
                case CharacterStatType.ResourceRegeneration:
                    return ref ResourceRegeneration;
                case CharacterStatType.WeaponAccuracy:
                    return ref WeaponAccuracy;
                case CharacterStatType.MaxResource:
                    return ref MaxResource;
                default:
                    throw new ArgumentOutOfRangeException(nameof(statType), statType, null);
            }
        }
    }
}
