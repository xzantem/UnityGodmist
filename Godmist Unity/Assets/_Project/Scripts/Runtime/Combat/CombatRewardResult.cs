using System;
using System.Collections.Generic;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class CombatRewardResult
    {
        // Legacy aggregate fields are kept so existing runtime state can still
        // deserialize. New combat rewards are represented per defeated enemy.
        public int ExperiencePerActiveCharacter;
        public int Gold;
        public List<ItemInstance> Items = new();
        public List<CombatEnemyReward> EnemyRewards = new();
    }

    [Serializable]
    public class CombatEnemyReward
    {
        public string EnemyInstanceId = string.Empty;
        // Zero denotes legacy fixed XP. Positive levels use the shared reward curve.
        public int ExperienceLevel;
        public int ExperiencePerActiveCharacter;
        public int Gold;
        public List<ItemInstance> Items = new();
    }
}
