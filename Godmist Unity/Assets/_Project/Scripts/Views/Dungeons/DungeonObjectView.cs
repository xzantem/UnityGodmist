using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public abstract class DungeonObjectView : MonoBehaviour
    {
        public string InstanceId { get; private set; } = string.Empty;

        protected DungeonObjectInstance DungeonObject { get; private set; }

        protected bool TryBind(
            DungeonObjectInstance dungeonObject,
            Vector3 worldPosition)
        {
            if (dungeonObject == null) return false;

            DungeonObject = dungeonObject;
            InstanceId = dungeonObject.InstanceId;
            transform.position = worldPosition;

            return true;
        }
    }
}