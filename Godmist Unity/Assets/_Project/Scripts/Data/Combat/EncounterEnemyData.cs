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
        [field: SerializeField, Min(0f)] public float VisionRange { get; private set; } = 10f;
        [field: SerializeField, Range(1f, 360f)] public float VisionAngle { get; private set; } = 120f;
        [field: SerializeField] public Vector2 InitialFacing { get; private set; } = Vector2.right;
    }
}
