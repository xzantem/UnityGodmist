using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonWallSegmentView
    {
        public const float NativeFullWallCapCenterHeight = NativeNorthWallFaceDepthWorld + NativeWallDepthWorld * .5f;
        private Transform mixedLowWallVisualRoot;
        private SpriteRenderer clippedMixedSide;
        private bool mixedSourceEnabled;
        private bool mixedSideClipped;
        private int mixedJunctionOrderOffset;
        private float mixedClipMinX = float.NegativeInfinity;
        private float mixedClipMaxX = float.PositiveInfinity;
        private float mixedNorthFaceTop;
        private Vector2 southOcclusionUnclippedSize;
        private Vector3 southOcclusionUnclippedPosition;
        private bool southOcclusionBoundsSaved;
        private sealed class MixedFullCap
        {
            public Vector2 Center;
            public int Arms;
            public SpriteRenderer Renderer;
        }
        private readonly List<MixedFullCap> mixedFullCaps = new();
        private readonly List<(SpriteRenderer Renderer, bool Enabled)> mixedHiddenEndpointCaps = new();

        internal void BeginMixedLowWallJunctions()
        {
            ClearNativeLowWallJoins();
            mixedFullCaps.Clear();
            mixedJunctionOrderOffset = 0;
            mixedClipMinX = float.NegativeInfinity;
            mixedClipMaxX = float.PositiveInfinity;
            if (southOcclusionBoundsSaved && southOcclusionRenderer != null)
            {
                southOcclusionRenderer.size = southOcclusionUnclippedSize;
                southOcclusionRenderer.transform.localPosition = southOcclusionUnclippedPosition;
                southOcclusionRenderer.enabled = true;
            }
            foreach (var cap in mixedHiddenEndpointCaps)
                if (cap.Renderer != null) cap.Renderer.enabled = cap.Enabled;
            mixedHiddenEndpointCaps.Clear();
            var source = GetComponent<SpriteRenderer>();
            if (source == null) return;
            if (mixedSideClipped)
            { clippedMixedSide.enabled = false; source.enabled = mixedSourceEnabled; }
            mixedSideClipped = false;
            mixedSourceEnabled = source != null && source.enabled;
        }

        internal void ClipSideAtRaisedLowWall(float minimumY)
        {
            var source = GetComponent<SpriteRenderer>();
            if (source == null || source.sprite == null) return;
            var bounds = source.bounds;
            var bottom = Mathf.Clamp(minimumY, bounds.min.y, bounds.max.y);
            if (bottom <= bounds.min.y) return;
            if (clippedMixedSide == null)
                clippedMixedSide = NewMixedPiece("Side strip above mixed low junction", source);
            var renderer = clippedMixedSide;
            renderer.sprite = source.sprite; renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(bounds.size.x, bounds.max.y - bottom);
            var pivot = source.sprite.pivot / source.sprite.rect.size;
            renderer.transform.position = new Vector3(bounds.center.x, (bottom + bounds.max.y) * .5f, source.transform.position.z)
                - (Vector3)((Vector2.one * .5f - pivot) * renderer.size);
            renderer.enabled = mixedSourceEnabled && renderer.size.y > .00001f;
            source.enabled = false;
            mixedSideClipped = true;
        }

        internal void ConfigureMixedLowWallJunction(Sprite side, Sprite north, Vector2 vertex, int lowArms, int sideArms,
            int southArms = 0, int northArms = 0, float clipMinX = float.NegativeInfinity, float clipMaxX = float.PositiveInfinity)
        {
            var source = GetComponent<SpriteRenderer>();
            var mixedSouth = southArms != 0 && (sideArms != 0 || northArms != 0);
            mixedJunctionOrderOffset = mixedSouth ? 1 : 0;
            // Keep the adjacent standing North face in front of this lower
            // roof. Only its opposite half-column belongs to the low join.
            mixedClipMinX = clipMinX;
            mixedClipMaxX = clipMaxX;
            mixedNorthFaceTop = vertex.y + NativeNorthWallFaceDepthWorld;
            var depth = 8f / north.pixelsPerUnit;
            var height = (north.rect.height - 8) / north.pixelsPerUnit;
            var faceHeight = height - depth;
            if ((sideArms & 8) != 0)
            {
                // The full north arm owns the tall transition even if a
                // South-profile branch also meets its flank.
                var rect = north.rect; rect.height -= 8; rect.x = rect.center.x - 4; rect.width = 8;
                AddFace("Mixed full wall junction face", rect, (north.rect.height - 8) / north.pixelsPerUnit);
                AddFullCap(side, north, vertex + Vector2.up * NativeFullWallCapCenterHeight, 8 | northArms, source);
                return;
            }
            if (southArms != 0 && (lowArms & 4) != 0)
            {
                // The south-going low roof projects into the South profile's
                // occluded strip. Own this overlapping part with that profile,
                // above its floor mask and the neighbouring horizontal face.
                // The low wall still owns the rest of its original roof run.
                // Continue the low strip's UV phase, whose south endpoint is
                // raised by (height - half a cap), instead of restarting the
                // stone pattern at the logical vertex.
                var rect = side.rect;
                rect.y += Mathf.Repeat(-(height - depth * .5f), side.rect.height / side.pixelsPerUnit) * side.pixelsPerUnit;
                rect.height = faceHeight * side.pixelsPerUnit;
                var sprite = Sprite.Create(side.texture, rect, new Vector2(.5f, 0), side.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.hideFlags = HideFlags.DontSave; lowWallJunctionSprites.Add(sprite);
                var roof = NewMixedPiece("South low wall roof continuation", source);
                roof.sprite = sprite; roof.drawMode = SpriteDrawMode.Tiled;
                roof.size = new Vector2(depth, faceHeight);
                roof.transform.position = vertex; roof.sortingOrder++;
                lowWallJunctionRenderers.Add(roof);
                ClipMixedPieceHorizontal(roof);
            }
            if (sideArms == 4 || mixedSouth && (sideArms & 4) != 0)
                AddFullCap(side, north, vertex - Vector2.up * (depth * .5f), sideArms & 7, source);
            // Horizontal low faces need the shared centre column; an exposed
            // south end of a north-going low strip needs the same standing face.
            var needsFace = (lowArms & 3) != 0 || (lowArms & 8) != 0 && (lowArms & 4) == 0;
            if (southArms != 0) needsFace &= sideArms != 0 && (lowArms & 4) == 0;
            if (needsFace)
            {
                var rect = north.rect; rect.y += 8; rect.height -= 16; rect.x = rect.center.x - 4; rect.width = 8;
                AddFace("Mixed low wall junction face", rect, faceHeight);
            }
            // Low roofs and South-profile roofs share this projected height.
            // Reduced Side arms meet the lower step; a north-going Side arm
            // has already returned through the taller transition above.
            var capArms = lowArms | southArms;
            // Horizontal low arms were clipped to the Side footprint, so even
            // their straight cap needs its central strip. Vertical cap runs
            // already meet exactly at the raised clipping boundary.
            if (capArms == 12) return;
            var cap = CreateDedicatedCornerSprite(side, north, vertex, source.bounds, source.bounds,
                CornerMiterRole.NorthCap, (capArms & 1) != 0 ? 1 : -1, (capArms & 4) != 0 ? 1 : -1, capArms);
            if (cap == null) return;
            cornerMiterSprites.Remove(cap); lowWallJunctionSprites.Add(cap);
            var renderer = NewMixedPiece("Mixed low wall cap junction " + capArms, source);
            renderer.sprite = cap; renderer.transform.position = vertex + Vector2.up * (height - depth * .5f);
            renderer.sortingOrder++; lowWallJunctionRenderers.Add(renderer);
            ClipMixedPieceHorizontal(renderer);

            void AddFace(string name, Rect rect, float heightWorld)
            {
                var sprite = Sprite.Create(north.texture, rect, new Vector2(.5f, 0), north.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.hideFlags = HideFlags.DontSave; lowWallJunctionSprites.Add(sprite);
                var face = NewMixedPiece(name, source);
                face.sprite = sprite; face.drawMode = SpriteDrawMode.Tiled; face.size = new Vector2(depth, heightWorld);
                face.transform.position = vertex; lowWallJunctionRenderers.Add(face);
                ClipMixedPieceHorizontal(face);
            }
        }

        private void AddFullCap(Sprite side, Sprite north, Vector2 center, int arms, SpriteRenderer source)
        {
            // A one-cell full arm can have its two cap endpoints in the same
            // projected square. Combine closures rather than drawing two caps.
            MergeEndpoint(sideFreeUpperCap, 4);
            MergeEndpoint(sideTerminalFaceCap, 8);
            var cap = mixedFullCaps.Find(value => (value.Center - center).sqrMagnitude < .00000001f);
            if (cap == null)
            {
                cap = new MixedFullCap { Center = center, Arms = arms, Renderer = NewMixedPiece("Mixed full wall cap", source) };
                cap.Renderer.transform.position = center; cap.Renderer.sortingOrder++;
                mixedFullCaps.Add(cap); lowWallJunctionRenderers.Add(cap.Renderer);
            }
            else
            {
                cap.Arms &= arms;
                lowWallJunctionSprites.Remove(cap.Renderer.sprite);
                DestroyRuntimeCornerSprite(cap.Renderer.sprite);
            }
            var sprite = CreateDedicatedCornerSprite(side, north, center, source.bounds, source.bounds,
                CornerMiterRole.NorthCap,
                (cap.Arms & 3) != 0 && (cap.Arms & 1) == 0 ? -1 : 1,
                (cap.Arms & 12) != 0 && (cap.Arms & 4) == 0 ? -1 : 1, cap.Arms, true);
            if (sprite == null) return;
            cornerMiterSprites.Remove(sprite); lowWallJunctionSprites.Add(sprite);
            cap.Renderer.sprite = sprite;
            cap.Renderer.name = "Mixed full wall cap " + cap.Arms;
            ClipMixedPieceHorizontal(cap.Renderer);

            void MergeEndpoint(SpriteRenderer renderer, int endpointArms)
            {
                if (renderer == null || ((Vector2)renderer.bounds.center - center).sqrMagnitude > .00000001f) return;
                arms &= endpointArms;
                if (mixedHiddenEndpointCaps.Exists(value => value.Renderer == renderer)) return;
                mixedHiddenEndpointCaps.Add((renderer, renderer.enabled)); renderer.enabled = false;
            }
        }

        private SpriteRenderer NewMixedPiece(string name, SpriteRenderer source)
        {
            if (mixedLowWallVisualRoot == null)
            {
                var root = new GameObject("Mixed masonry junction visuals"); root.transform.SetParent(transform, false);
                var scale = transform.lossyScale;
                root.transform.localScale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1);
                mixedLowWallVisualRoot = root.transform;
            }
            var piece = new GameObject(name); piece.transform.SetParent(mixedLowWallVisualRoot, false);
            piece.transform.rotation = Quaternion.identity; piece.layer = source.gameObject.layer;
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source.sharedMaterial; renderer.color = source.color;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + mixedJunctionOrderOffset;
            renderer.maskInteraction = source.maskInteraction; renderer.forceRenderingOff = source.forceRenderingOff;
            renderer.enabled = mixedSourceEnabled;
            return renderer;
        }

        private void ClipMixedPieceHorizontal(SpriteRenderer renderer)
        {
            var bounds = renderer.bounds;
            // The full cap is on the same plane as the North roof, so it
            // joins its arms instead of being clipped against its lower face.
            if (bounds.min.y >= mixedNorthFaceTop - .00001f) return;
            var minimum = Mathf.Max(bounds.min.x, mixedClipMinX);
            var maximum = Mathf.Min(bounds.max.x, mixedClipMaxX);
            if (minimum <= bounds.min.x && maximum >= bounds.max.x) return;
            if (maximum <= minimum) { renderer.enabled = false; return; }
            var original = renderer.sprite;
            var rect = original.rect;
            rect.x += (minimum - bounds.min.x) * original.pixelsPerUnit;
            rect.width = (maximum - minimum) * original.pixelsPerUnit;
            var sprite = Sprite.Create(original.texture, rect,
                new Vector2(.5f, original.pivot.y / original.rect.height), original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave; lowWallJunctionSprites.Add(sprite);
            renderer.sprite = sprite;
            if (renderer.drawMode != SpriteDrawMode.Simple)
                renderer.size = new Vector2(maximum - minimum, renderer.size.y);
            var position = renderer.transform.position; position.x = (minimum + maximum) * .5f;
            renderer.transform.position = position;
        }

        private void RememberSouthOcclusionBounds()
        {
            southOcclusionUnclippedSize = southOcclusionRenderer.size;
            southOcclusionUnclippedPosition = southOcclusionRenderer.transform.localPosition;
            southOcclusionBoundsSaved = true;
        }

        internal void TrimSouthOcclusionAtStandingFace(Bounds face)
        {
            if (southOcclusionRenderer == null || !southOcclusionRenderer.gameObject.activeSelf) return;
            var bounds = southOcclusionRenderer.bounds;
            // A neighbouring standing face covers the whole height of this
            // gap. Trim only the overlapping endpoint, preserving floor
            // occlusion everywhere else along the South wall.
            if (face.min.y > bounds.min.y + .00001f || face.max.y < bounds.max.y - .00001f) return;
            var minimum = bounds.min.x; var maximum = bounds.max.x;
            if (face.center.x > bounds.center.x) maximum = Mathf.Min(maximum, face.min.x);
            else minimum = Mathf.Max(minimum, face.max.x);
            if (minimum >= maximum) { southOcclusionRenderer.enabled = false; return; }
            var position = southOcclusionRenderer.transform.position;
            position.x = (minimum + maximum) * .5f;
            southOcclusionRenderer.transform.position = position;
            southOcclusionRenderer.size = new Vector2(maximum - minimum, southOcclusionRenderer.size.y);
        }
    }
}
