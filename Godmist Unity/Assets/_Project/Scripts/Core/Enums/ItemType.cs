namespace Godmist.Core.Enums
{
    public enum ItemType
    {
        Weapon = 0,
        Armor = 1,
        Jewelry = 2,
        Material = 3,
        Potion = 4,
        Galduryte = 5,
        Lootbag = 6,
        Key = 7,
        QuestItem = 8,
        Valuable = 9,
        Tool = 10
    }

    /// <summary>
    /// Describes which specialist service may use a material. ItemType remains
    /// Material so inventory, loot and trade do not need separate item families.
    /// </summary>
    public enum MaterialDiscipline
    {
        None = 0,
        Smithing = 1,
        Alchemy = 2,
        Runic = 3
    }

    public enum ChestLockType
    {
        Unlocked = 0,
        KeyRequired = 1,
        KeyOrLockpick = 2
    }
}
