namespace Godmist.Core.Constants
{
    /// <summary>The release identifier includes its channel; platform bundle metadata remains numeric.</summary>
    public static class GameVersion
    {
        public const string Release = "0.1.2-beta.1";
        public static readonly string Bundle = Release.Split('-')[0];
        public static string MenuLabel => Release + " — Early Access";
    }
}
