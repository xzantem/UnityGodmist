using System;

namespace Godmist.Data.Items
{
    [Serializable]
    public class EquipmentPartStats
    {
        public float MinAttackBase;
        public float MaxAttackBase;
        public float CritChanceBase;
        public float CritDamageBase;
        public float WeaponAccuracyBase;
        public float MaxResourceBase;
        public float ResourceRegenerationBase;
        public float MaxHealthBase;
        public float DodgeBase;
        public float PhysicalDefenseBase;
        public float MagicalDefenseBase;

        public float MinAttackPercent;
        public float MaxAttackPercent;
        public float CritChancePercent;
        public float CritDamagePercent;
        public float WeaponAccuracyPercent;
        public float MaxResourcePercent;
        public float ResourceRegenerationPercent;
        public float MaxHealthPercent;
        public float DodgePercent;
        public float PhysicalDefensePercent;
        public float MagicalDefensePercent;
    }
}
