namespace Godmist.Core.Enums
{
    /// <summary>
    /// Broad combat-facing category used by target conditions.
    /// Keep this list intentionally small and stable; content can add more
    /// tags later without changing the passive condition contract.
    /// </summary>
    public enum CharacterTag
    {
        Human = 0,
        Beast = 1,
        Undead = 2,
        Demon = 3,
        Giant = 4
    }
}
