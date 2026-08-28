using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class SpikeTrapDungeonObjectInstance : DungeonObjectInstance
    {
        public int Damage { get; }
        public string ActivationGroupId { get; }
        public bool IsTriggered { get; private set; }

        public SpikeTrapDungeonObjectInstance(
            string instanceId,
            SpikeTrapDungeonObjectDefinition definition,
            Vector2Int position,
            string activationGroupId = null)
            : base(instanceId, definition, position)
        {
            Damage = Mathf.Max(1, definition.Damage);
            ActivationGroupId = activationGroupId ?? string.Empty;
        }

        public bool TryTrigger()
        {
            if (IsTriggered) return false;

            IsTriggered = true;
            return true;
        }

        public bool TryRestoreTriggered(bool isTriggered)
        {
            IsTriggered = isTriggered;
            return true;
        }
    }
}
