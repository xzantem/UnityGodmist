using System;
using Godmist.Data.Dungeons;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonInstance
    {
        public DungeonDefinition Definition { get; }
        public string DungeonId { get; }
        public int Seed { get; }
        public int CurrentFloorIndex { get; private set; }
        public bool IsCompleted { get; private set; }

        public DungeonInstance(DungeonDefinition definition, int seed)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException("DungeonDefinition musi miec niepuste Id.", nameof(definition));
            }

            Definition = definition;
            DungeonId = definition.Id;
            Seed = seed;
        }

        public DungeonInstance(string dungeonId, int seed)
        {
            if (string.IsNullOrWhiteSpace(dungeonId))
            {
                throw new ArgumentException("DungeonId musi byc niepuste.", nameof(dungeonId));
            }

            DungeonId = dungeonId;
            Seed = seed;
        }

        public bool TrySetCurrentFloorIndex(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex == CurrentFloorIndex) return false;

            CurrentFloorIndex = floorIndex;
            return true;
        }

        public bool TryMarkCompleted()
        {
            if (IsCompleted) return false;

            IsCompleted = true;
            return true;
        }
    }
}