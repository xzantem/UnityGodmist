using System;
using Godmist.Core.Enums;
using System.Collections.Generic;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class EquipmentSet
    {
        public EquipmentItemInstance Weapon;
        public EquipmentItemInstance Armor;
        public EquipmentItemInstance Necklace;
        public EquipmentItemInstance Ring;
        public EquipmentItemInstance Bracelet;
        public EquipmentItemInstance Earrings;

        public EquipmentItemInstance GetItem(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => Weapon,
                EquipmentSlot.Armor => Armor,
                EquipmentSlot.Necklace => Necklace,
                EquipmentSlot.Ring => Ring,
                EquipmentSlot.Bracelet => Bracelet,
                EquipmentSlot.Earrings => Earrings,
                _ => null
            };
        }

        public void SetItem(
            EquipmentSlot slot,
            EquipmentItemInstance item)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    Weapon = item;
                    break;
                case EquipmentSlot.Armor:
                    Armor = item;
                    break;
                case EquipmentSlot.Necklace:
                    Necklace = item;
                    break;
                case EquipmentSlot.Ring:
                    Ring = item;
                    break;
                case EquipmentSlot.Bracelet:
                    Bracelet = item;
                    break;
                case EquipmentSlot.Earrings:
                    Earrings = item;
                    break;
            }
        }

        public IEnumerable<EquipmentItemInstance> GetEquippedItems()
        {
            if (Weapon != null) yield return Weapon;
            if (Armor != null) yield return Armor;
            if (Necklace != null) yield return Necklace;
            if (Ring != null) yield return Ring;
            if (Bracelet != null) yield return Bracelet;
            if (Earrings != null) yield return Earrings;
        }
    }
}