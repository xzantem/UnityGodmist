using System;
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
        public Stat Dodge;
        public Stat CritChance;
        public Stat CritDamage;
        public Stat Speed;
        public Stat ResourceRegeneration;
    }
}