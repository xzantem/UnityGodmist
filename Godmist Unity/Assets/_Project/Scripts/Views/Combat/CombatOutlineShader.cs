using UnityEngine;

namespace Godmist.Views.Combat
{
    /// <summary>Shared player-build dependency for actor and world-object outlines.</summary>
    public static class CombatOutlineShader
    {
        public const string ResourcePath = "Godmist/SolidSpriteSilhouette";
        public static Shader Load() => Resources.Load<Shader>(ResourcePath);
    }
}
