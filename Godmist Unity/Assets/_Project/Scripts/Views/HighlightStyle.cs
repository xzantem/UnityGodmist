using UnityEngine;

namespace Godmist.Views
{
    /// <summary>Shared presentation semantics. No gameplay state or targeting rules.</summary>
    public static class HighlightStyle
    {
        // Match the aged brass, bone and restrained pigments of the game's frames.
        public static Color Selection => new(.80f, .65f, .36f, 1f);
        public static Color Hover => new(.66f, .77f, .80f, 1f);
        public static Color Range => new(.40f, .56f, .66f, 1f);
        public static Color Hostile => new(.82f, .39f, .32f, 1f);
        public static Color Friendly => new(.53f, .72f, .45f, 1f);
        public static Color InspectedRange => new(.66f, .53f, .73f, 1f);
        public static Color Ink => new(.055f, .065f, .075f, .9f);
        public static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        public static Color SelectionSurface(Color background) =>
            WithAlpha(Color.Lerp(background, Selection, .25f), background.a);
        public static Color Target(bool casterIsPlayerSide, bool targetIsPlayerSide) =>
            casterIsPlayerSide == targetIsPlayerSide ? Friendly : Hostile;
    }
}
