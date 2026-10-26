using System;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [Serializable]
    public class CharacterHealthGrowthPoint
    {
        [field: SerializeField, Range(1, 50)] public int Level { get; private set; }
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; }
    }
}
