using System;
using System.Collections.Generic;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.World
{
    [Serializable]
    public sealed class StartingCharacterDefinition
    {
        public CharacterDefinition Character;
        public CharacterClassData Class;
        public EquipmentItemDefinition Weapon;
        public EquipmentItemDefinition Armor;
        public List<EquipmentPartDefinition> WeaponParts = new();
        public List<EquipmentPartDefinition> ArmorParts = new();
    }

    [CreateAssetMenu(fileName = "NewCampaign", menuName = "Godmist/World/New Campaign")]
    public sealed class NewCampaignDefinition : ScriptableObject
    {
        [Min(0)] public int StartingGold = 500;
        public List<StartingCharacterDefinition> Characters = new();
    }
}
