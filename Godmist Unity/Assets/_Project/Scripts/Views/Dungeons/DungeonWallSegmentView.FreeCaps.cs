using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonWallSegmentView
    {
        private SpriteRenderer sideFreeUpperCap;
        private SpriteRenderer sideTerminalFaceCap;
        // Reuse the native 8px compositor for L/T/cross junctions. A single
        // incoming vertical arm leaves the opposite border closed.
        public void ConfigureSideFreeEndCaps(bool lowerFree, bool upperFree)
        {
            var body = GetComponent<SpriteRenderer>();
            if (!UsesNativeWallCrossSection || body == null || body.sprite == null
                || SurfaceProfile != DungeonWallSurfaceProfile.Side
                || !HasNativeSprite(sideWallSprite, 8f, 32f)
                || !HasNativeSprite(northWallSprite, 32f, 32f)) return;
            var bounds = body.bounds;
            if (lowerFree)
            {
                // The southern terminal segment is a standing face, capped
                // only in its upper 8px. Do not put a cap below that face.
                body.sprite = ResolveMixedSideFaceSprite() ?? body.sprite;
            }
            void End(bool upper)
            {
                var center = new Vector2(bounds.center.x, bounds.max.y - NativeWallDepthWorld * .5f);
                var sprite = CreateDedicatedCornerSprite(sideWallSprite, northWallSprite, center,
                    bounds, bounds, CornerMiterRole.NorthCap, 1f, 1f, upper ? 4 : 8);
                if (sprite == null) return;
                var renderer = CreateCornerMiterRenderer(upper ? "SideFreeUpperCap" : "SideTerminalFaceCap",
                    sprite, transform, transform.InverseTransformPoint(center), body,
                    VerticalWallSortingOrder + 1, false);
                if (renderer != null)
                {
                    cornerMiterRenderers.Add(renderer);
                    if (upper) sideFreeUpperCap = renderer;
                    else sideTerminalFaceCap = renderer;
                }
            }
            if (upperFree) End(true);
            else if (lowerFree) End(false);
        }
    }
}
