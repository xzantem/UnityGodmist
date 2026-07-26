using System;
using Godmist.Data.Characters;
using UnityEngine;

namespace Godmist.Data.Combat
{
    [Serializable]
    public class EncounterEnemyData
    {
        [field: SerializeField] public string InstanceId { get; private set; } = string.Empty;
        [field: SerializeField] public CharacterDefinition CharacterDefinition { get; private set; }
        [field: SerializeField] public CharacterClassData ClassData { get; private set; }
        [field: SerializeField] public Vector2Int SpawnPosition { get; private set; }
    }
}