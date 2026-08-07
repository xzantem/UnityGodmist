using System.Collections.Generic;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.Quests
{
    public class QuestRewardResult
    {
        public int ExperiencePerActiveCharacter { get; set; }
        public int Gold { get; set; }
        public int HonorPerActiveCharacter { get; set; }

        public List<ItemInstance> Items { get; } = new();
        public List<string> UpdatedWorldFlagIds { get; } = new();

        public bool HasRewards =>
            ExperiencePerActiveCharacter > 0
            || Gold > 0
            || HonorPerActiveCharacter > 0
            || Items.Count > 0
            || UpdatedWorldFlagIds.Count > 0;
    }
}