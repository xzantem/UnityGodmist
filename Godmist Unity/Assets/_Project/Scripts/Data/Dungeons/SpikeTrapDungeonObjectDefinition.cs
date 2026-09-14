using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "SpikeTrapDungeonObject",
        menuName = "Godmist/Dungeons/Objects/Spike Trap")]
    public class SpikeTrapDungeonObjectDefinition : DungeonObjectDefinition
    {
        [field: SerializeField, Min(1)]
        public int Damage { get; private set; } = 10;

        [field: SerializeField] public DamageType DamageType { get; private set; } = DamageType.Physical;
        [field: SerializeField] public string StatusId { get; private set; } = string.Empty;
        [field: SerializeField, Min(0)] public int StatusDamage { get; private set; }
        [field: SerializeField, Min(1)] public int Duration { get; private set; } = 2;
        [field: SerializeField, Range(0f, 1f)] public float StatusChance { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float DamageGrowthPerLevel { get; private set; } = 0f;
        [field: SerializeField] public bool DealsImmediateDamage { get; private set; } = true;



        public override DungeonObjectType ObjectType => DungeonObjectType.SpikeTrap;
    }
}
