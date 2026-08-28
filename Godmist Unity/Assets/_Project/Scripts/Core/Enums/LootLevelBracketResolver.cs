namespace Godmist.Core.Enums
{
    public static class LootLevelBracketResolver
    {
        public static LootLevelBracket Resolve(int level)
        {
            var clampedLevel = level < 1 ? 1 : level > 50 ? 50 : level;

            return clampedLevel switch
            {
                <= 10 => LootLevelBracket.Level1To10,
                <= 20 => LootLevelBracket.Level11To20,
                <= 30 => LootLevelBracket.Level21To30,
                <= 40 => LootLevelBracket.Level31To40,
                _ => LootLevelBracket.Level41To50
            };
        }
    }
}
