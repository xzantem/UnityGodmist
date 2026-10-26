namespace Godmist.Systems.Items
{
    public static class DungeonFoodRules
    {
        public const string ItemId = "item.consumable.ration";
        public const float HealthFraction = .10f;
        // Typical groups contain 2–4 enemies: roughly one portion per encounter.
        public const float EnemyDropChance = .35f, ChestDropChance = .5f;
    }
}
