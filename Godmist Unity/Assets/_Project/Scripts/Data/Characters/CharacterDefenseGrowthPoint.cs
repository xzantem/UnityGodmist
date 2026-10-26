using System;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [Serializable]
    public class CharacterDefenseGrowthPoint
    {
        [field: SerializeField, Range(1, 50)] public int Level { get; private set; }
        [field: SerializeField, Min(0)] public float PhysicalDefense { get; private set; }
        [field: SerializeField, Min(0)] public float MagicalDefense { get; private set; }
    }
}
