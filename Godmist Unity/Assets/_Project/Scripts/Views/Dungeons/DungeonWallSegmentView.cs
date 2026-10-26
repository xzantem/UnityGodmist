using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

namespace Godmist.Views.Dungeons
{
    public enum DungeonWallSurfaceProfile
    {
        Auto,
        Side,
        North,
        South
    }

    public partial class DungeonWallSegmentView : MonoBehaviour
    {
        public const float DefaultThickness = 0.25f;
        public const float NativeWallWidthWorld = 0.25f;
        public const float NativeWallDepthWorld = 0.25f;
        public const float NativeNorthWallDepthWorld = 1f;
        public const float NativeNorthWallFaceDepthWorld = 0.75f;
        // A low wall has a standing masonry face and a separate horizontal cap.
        public const float CatacombsLowWallFrontHeightWorld = .5625f;
        public const float CatacombsLowWallCapDepthWorld = .1875f;
        public const float CatacombsLowWallSideWidthWorld = .375f;
        public const int HorizontalWallSortingOrder = 3;
        public const int VerticalWallSortingOrder = 2;
        public const int UpperVerticalCornerSortingOrder = 4;
        private const int RuntimeCornerTexturePixels = 8;
        private const int RuntimeCornerBandPixels = 2;

        [SerializeField] private Collider2D movementCollider;
        [SerializeField] private Sprite verticalWallSprite;
        [SerializeField] private Sprite horizontalWallSprite;
        [SerializeField] private Sprite sideWallSprite;
        [SerializeField] private Sprite northWallSprite;
        [SerializeField] private Sprite southWallSprite;
        [SerializeField] private float thickness = DefaultThickness;
        [SerializeField] private bool useNativeWallCrossSection = true;
        [SerializeField] private float southVisualOffsetCells = .75f;

        private SpriteRenderer upperVerticalCornerRenderer;
        private readonly List<SpriteRenderer> northSideCapOverlays = new();
        private readonly List<SpriteRenderer> southSideCapOverlays = new();
        private readonly List<SpriteRenderer> cornerMiterRenderers = new();
        private readonly List<Sprite> cornerMiterSprites = new();
        private readonly List<Texture2D> cornerMiterTextures = new();
        private readonly Dictionary<string, Texture2D> cornerMiterTextureCache = new();
        private sealed class CornerSourcePixels
        {
            public int Width;
            public int Height;
            public Color32[] Pixels;
        }

        // Native wall textures are shared by every spawned wall segment. Keep
        // one GPU readback per source texture and compose tiny corner textures
        // from cached CPU pixels during a map rebuild.
        private static readonly Dictionary<Texture, CornerSourcePixels> sharedCornerSourcePixels = new();
        private readonly List<SpriteRenderer> northSideJoinOverlays = new();
        private readonly List<Sprite> northSideJoinSprites = new();
        private readonly List<Texture2D> northSideJoinTextures = new();
        // A South root can be in front of a Side root even when their visual
        // join is owned by Side. Keep a tiny copy of the Side endpoint patch
        // under the selected owner so the global role lanes remain intact.
        private readonly List<SpriteRenderer> sideSouthJoinOverlays = new();
        private readonly List<Sprite> sideSouthJoinSprites = new();
        private readonly List<Texture2D> sideSouthJoinTextures = new();
        private readonly List<SpriteRenderer> sideVoidOcclusionRenderers = new();
        private SortingGroup depthSortingGroup;
        private SpriteRenderer southOcclusionRenderer;
        private Sprite southOcclusionSprite;
        private Sprite sideVoidOcclusionSprite;
        private Sprite mixedSideFaceSprite;
        private Texture2D mixedSideFaceTexture;
        private Transform catacombsAuditVisualRoot;
        private SpriteRenderer catacombsAuditRailingRenderer;
        private Transform catacombsLowWallVisualRoot;
        private SpriteRenderer catacombsLowWallRenderer;
        private SpriteRenderer catacombsLowWallDetailRenderer;
        private SpriteRenderer catacombsLowWallFootOutline;
        private Sprite catacombsLowWallFootOutlineSprite;
        private Sprite catacombsLowWallSource, catacombsLowWallFace, catacombsLowWallCap, catacombsLowWallEndFace;
        private Sprite catacombsLowWallSideCap;
        private readonly List<SpriteRenderer> lowWallJunctionRenderers = new();
        private readonly List<Sprite> lowWallJunctionSprites = new();

        public float Thickness => thickness;
        public Sprite VerticalWallSprite => verticalWallSprite;
        public Sprite HorizontalWallSprite => horizontalWallSprite;
        public Sprite SideWallSprite => sideWallSprite;
        public Sprite NorthWallSprite => northWallSprite;
        public Sprite SouthWallSprite => southWallSprite;
        public bool UsesNativeWallCrossSection => useNativeWallCrossSection;
        public float SouthVisualOffsetCells => southVisualOffsetCells;
        public bool HasUpperVerticalCornerOverlay => upperVerticalCornerRenderer != null
                                                     && upperVerticalCornerRenderer.gameObject.activeSelf;

        /// <summary>
        /// Replaces only the stale railing artwork in the Catacombs audit
        /// fixture.  The prefab root owns the movement collider and its
        /// legacy scale is part of that collider's serialized footprint, so
        /// the replacement is a compensated child visual rather than a root
        /// transform mutation.
        /// </summary>
        public bool ConfigureCatacombsAuditRailing(
            DungeonCellEdge edge,
            Sprite horizontalSprite,
            Sprite verticalSprite)
        {
            // DungeonCellEdge stores the two adjacent cells, not the two
            // vertices of the wall.  Cells differing in Y share a horizontal
            // wall; cells differing in X share a vertical wall.
            var wallRunsHorizontally = edge.FirstCell.y != edge.SecondCell.y;
            var sprite = wallRunsHorizontally ? horizontalSprite : verticalSprite;
            if (sprite == null)
                return false;

            var sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null)
                return false;

            var sourceMaterial = sourceRenderer.sharedMaterial;
            var sourceLayer = sourceRenderer.sortingLayerID;
            var sourceOrder = sourceRenderer.sortingOrder;
            var rootScale = transform.localScale;

            if (catacombsAuditVisualRoot == null)
            {
                var neutral = new GameObject("Catacombs audit railing visual");
                neutral.transform.SetParent(transform, false);
                neutral.transform.localPosition = Vector3.zero;
                neutral.transform.localRotation = Quaternion.identity;
                neutral.transform.localScale = new Vector3(
                    1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.x)),
                    1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.y)),
                    1f);
                catacombsAuditVisualRoot = neutral.transform;
            }

            if (catacombsAuditRailingRenderer == null)
                catacombsAuditRailingRenderer = catacombsAuditVisualRoot.gameObject
                    .AddComponent<SpriteRenderer>();

            // Both assets are authored in their natural world orientation. A
            // neutral presentation child cancels the legacy prefab rotation;
            // rotating the side asset would turn its vertical posts sideways.
            var desiredRotation = Quaternion.identity;
            var desiredWidth = wallRunsHorizontally ? VisualLength : NativeWallWidthWorld;
            var desiredHeight = wallRunsHorizontally ? .625f : VisualLength;
            catacombsAuditRailingRenderer.transform.localPosition = Vector3.zero;
            catacombsAuditRailingRenderer.transform.localRotation =
                Quaternion.Inverse(transform.rotation) * desiredRotation;
            catacombsAuditRailingRenderer.transform.localScale = new Vector3(
                desiredWidth / Mathf.Max(.0001f, sprite.bounds.size.x),
                desiredHeight / Mathf.Max(.0001f, sprite.bounds.size.y),
                1f);
            catacombsAuditRailingRenderer.sprite = sprite;
            catacombsAuditRailingRenderer.drawMode = SpriteDrawMode.Simple;
            // The authored railing highlights are intentionally restrained in
            // the Catacombs palette so the metal does not read brighter/cooler
            // than the floor and v4 masonry faces under the same lighting.
            catacombsAuditRailingRenderer.color = new Color(.78f, .77f, .75f, 1f);
            catacombsAuditRailingRenderer.sortingLayerID = sourceLayer;
            catacombsAuditRailingRenderer.sortingOrder = sourceOrder;
            catacombsAuditRailingRenderer.sharedMaterial = sourceMaterial;
            catacombsAuditRailingRenderer.maskInteraction = sourceRenderer.maskInteraction;
            catacombsAuditRailingRenderer.enabled = true;

            // The old root and Post0/Post1/Post2 sprites are deliberately
            // disabled.  Their transforms remain untouched so the gameplay
            // collider and prefab state are still owned by the normal prefab.
            foreach (var stale in GetComponentsInChildren<SpriteRenderer>(true))
                if (stale != null && stale != catacombsAuditRailingRenderer)
                    stale.enabled = false;

            return true;
        }

        /// <summary>
        /// Replaces the legacy low-wall tile with the Catacombs masonry
        /// profile.  A DungeonCellEdge contains the two cells on either side
        /// of the wall: equal X means the physical wall runs horizontally,
        /// while equal Y means it runs vertically.  The gameplay root scale,
        /// transform and collider remain owned by the prefab; only an inverse
        /// scaled child renderer is changed.
        /// </summary>
        public bool ConfigureCatacombsLowWall(
            DungeonCellEdge edge,
            Sprite masonrySheet,
            bool hasSouthContinuation = false,
            bool northEndBehindFullWall = false,
            bool westMeetsFullWall = false,
            bool eastMeetsFullWall = false)
        {
            var wallRunsHorizontally = edge.FirstCell.x == edge.SecondCell.x;
            if (masonrySheet == null || masonrySheet.rect.height < 192 || masonrySheet.rect.width < 128)
                return false;

            var sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null || VisualLength <= .0001f)
                return false;

            var sourceMaterial = sourceRenderer.sharedMaterial;
            var sourceLayer = sourceRenderer.sortingLayerID;
            var sourceOrder = sourceRenderer.sortingOrder;
            var rootScale = transform.localScale;
            var faceHeight = CatacombsLowWallFrontHeightWorld - CatacombsLowWallCapDepthWorld;
            if (catacombsLowWallSource != masonrySheet)
            {
                ReleaseLowWallSprites();
                catacombsLowWallSource = masonrySheet;
                // Dedicated low masonry artwork: the face, foreshortened top,
                // side return and end all share one authored sprite sheet.
                catacombsLowWallFace = LowWallSprite(masonrySheet, 20, 38, 32, 12);
                catacombsLowWallCap = LowWallSprite(masonrySheet, 20, 32, 32, 6);
                catacombsLowWallSideCap = LowWallSprite(masonrySheet, 90, 17, 12, 16);
                catacombsLowWallEndFace = LowWallSprite(masonrySheet, 88, 49, 12, 12);
            }

            if (catacombsLowWallVisualRoot == null)
            {
                var visual = new GameObject("Catacombs low wall visual");
                visual.transform.SetParent(transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = new Vector3(
                    1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.x)),
                    1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.y)),
                    1f);
                catacombsLowWallVisualRoot = visual.transform;
            }
            else
            {
                catacombsLowWallVisualRoot.localScale = new Vector3(
                    1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.x)),
                    1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.y)),
                    1f);
            }

            if (catacombsLowWallRenderer == null)
            {
                var profileObject = new GameObject("Catacombs low wall profile");
                profileObject.transform.SetParent(catacombsLowWallVisualRoot, false);
                catacombsLowWallRenderer = profileObject.AddComponent<SpriteRenderer>();
            }
            if (catacombsLowWallDetailRenderer == null)
            {
                var detail = new GameObject("Catacombs low wall detail");
                detail.transform.SetParent(catacombsLowWallVisualRoot, false);
                catacombsLowWallDetailRenderer = detail.AddComponent<SpriteRenderer>();
            }
            var capCenterHeight = faceHeight + CatacombsLowWallCapDepthWorld * .5f;
            // The north full-wall face starts at the ground boundary. It hides
            // the raised end of a low wall meeting it; otherwise that end is
            // painted as a little post halfway up the tall wall's face.
            var northClip = !wallRunsHorizontally && northEndBehindFullWall ? CatacombsLowWallFrontHeightWorld : 0f;
            var westClip = wallRunsHorizontally && westMeetsFullWall ? (CatacombsLowWallSideWidthWorld + NativeWallWidthWorld) * .5f : 0;
            var eastClip = wallRunsHorizontally && eastMeetsFullWall ? (CatacombsLowWallSideWidthWorld + NativeWallWidthWorld) * .5f : 0;
            var horizontalLength = VisualLength + CatacombsLowWallSideWidthWorld - westClip - eastClip;
            var horizontalOffset = Vector3.right * ((westClip - eastClip) * .5f);
            Configure(catacombsLowWallRenderer,
                wallRunsHorizontally ? catacombsLowWallFace : catacombsLowWallSideCap,
                wallRunsHorizontally ? horizontalLength : CatacombsLowWallSideWidthWorld,
                wallRunsHorizontally ? faceHeight : Mathf.Max(1f / 32, VisualLength + CatacombsLowWallCapDepthWorld - northClip),
                horizontalOffset + Vector3.up * (wallRunsHorizontally ? faceHeight * .5f : capCenterHeight - northClip * .5f),
                0, sourceOrder);
            catacombsLowWallDetailRenderer.name = wallRunsHorizontally ? "Catacombs low wall cap" : "Catacombs low wall end face";
            Configure(catacombsLowWallDetailRenderer,
                wallRunsHorizontally ? catacombsLowWallCap : catacombsLowWallEndFace,
                wallRunsHorizontally ? horizontalLength : CatacombsLowWallSideWidthWorld,
                wallRunsHorizontally ? CatacombsLowWallCapDepthWorld : faceHeight,
                horizontalOffset + Vector3.up * (wallRunsHorizontally ? faceHeight + CatacombsLowWallCapDepthWorld * .5f : -VisualLength * .5f + faceHeight * .5f),
                0, sourceOrder + (wallRunsHorizontally ? 1 : -1));
            catacombsLowWallDetailRenderer.enabled = wallRunsHorizontally || !hasSouthContinuation;
            if (catacombsLowWallFootOutlineSprite == null)
            {
                catacombsLowWallFootOutlineSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1), Vector2.one * .5f, 32, 0, SpriteMeshType.FullRect);
                catacombsLowWallFootOutlineSprite.hideFlags = HideFlags.DontSave;
            }
            if (catacombsLowWallFootOutline == null)
            {
                var outline = new GameObject("Catacombs low wall foot outline");
                outline.transform.SetParent(catacombsLowWallVisualRoot, false);
                catacombsLowWallFootOutline = outline.AddComponent<SpriteRenderer>();
            }
            var faceRenderer = wallRunsHorizontally ? catacombsLowWallRenderer : catacombsLowWallDetailRenderer;
            var faceBounds = faceRenderer.bounds;
            Configure(catacombsLowWallFootOutline, catacombsLowWallFootOutlineSprite,
                faceBounds.size.x, 1f / 32,
                new Vector3(faceBounds.center.x, faceBounds.min.y - .5f / 32, transform.position.z) - transform.position,
                0, sourceOrder - 2);
            catacombsLowWallFootOutline.drawMode = SpriteDrawMode.Simple;
            catacombsLowWallFootOutline.transform.localScale = new Vector3(faceBounds.size.x * 32, 1, 1);
            catacombsLowWallFootOutline.color = new Color32(0, 0, 0, 70);
            catacombsLowWallFootOutline.enabled = faceRenderer.enabled;

            void Configure(SpriteRenderer renderer, Sprite sprite, float width, float height, Vector3 offset, float angle, int order)
            {
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(width, height);
                renderer.transform.localScale = Vector3.one;
                renderer.transform.rotation = Quaternion.Euler(0,0,angle);
                renderer.transform.position = transform.position + offset;
                renderer.color = new Color(.75f, .85f, 1f, 1f);
                renderer.gameObject.layer = sourceRenderer.gameObject.layer;
                renderer.sortingLayerID = sourceLayer;
                renderer.sortingOrder = order;
                renderer.sharedMaterial = sourceMaterial;
                renderer.maskInteraction = sourceRenderer.maskInteraction;
                renderer.enabled = true;
            }

            // The old tile is a second, unrelated visual (the screenshot's
            // brown vertical line). Disable every stale child renderer while
            // leaving the prefab root and its movement collider untouched.
            foreach (var stale in GetComponentsInChildren<SpriteRenderer>(true))
                if (stale != null && stale != catacombsLowWallRenderer && stale != catacombsLowWallDetailRenderer
                    && stale != catacombsLowWallFootOutline
                    && !lowWallJunctionRenderers.Contains(stale))
                    stale.enabled = false;

            return true;
        }

        internal void ConfigureLowWallFootOutlineJoins(bool horizontal, bool westContinues, bool eastContinues, bool southMeetsHorizontal)
        {
            if (catacombsLowWallFootOutline == null) return;
            if (!horizontal)
            {
                // The horizontal face already owns this contact line.
                if (southMeetsHorizontal) catacombsLowWallFootOutline.enabled = false;
                return;
            }
            // Partition a continuous line at cell boundaries, so alpha does
            // not double at the overlap of adjacent masonry sprites.
            var west = westContinues ? CatacombsLowWallSideWidthWorld * .5f : 0;
            var east = eastContinues ? CatacombsLowWallSideWidthWorld * .5f : 0;
            var face = catacombsLowWallRenderer.bounds;
            var position = catacombsLowWallFootOutline.transform.position;
            position.x = face.center.x + (west - east) * .5f;
            catacombsLowWallFootOutline.transform.position = position;
            catacombsLowWallFootOutline.transform.localScale = new Vector3((face.size.x - west - east) * 32, 1, 1);
        }

        // Bits are the arms reaching left/right/down/up from a grid vertex.
        // These are authored end/corner/T/cross images, not generated patches.
        internal void AddLowWallCapJunction(Vector2 vertex, int arms)
        {
            if (catacombsLowWallSource == null || catacombsLowWallRenderer == null) return;
            var flipY = false;
            var rect = arms switch
            {
                1 => new RectInt(50, 32, 12, 6), // east end
                2 => new RectInt(6, 32, 12, 6), // west end
                4 => new RectInt(91, 10, 12, 6), // north end
                8 => new RectInt(88, 45, 12, 6), // south end
                5 or 9 => new RectInt(106, 99, 12, 6), // west turn
                6 or 10 => new RectInt(10, 99, 12, 6), // east turn
                7 or 11 => new RectInt(29, 156, 12, 6), // transverse T
                _ => new RectInt(90, 149, 12, 6) // longitudinal T/cross
            };
            flipY = arms == 5 || arms == 6 || arms == 7;
            var sprite = LowWallSprite(catacombsLowWallSource, rect.x, rect.y, rect.width, rect.height);
            var child = new GameObject("Catacombs low wall cap junction");
            child.transform.SetParent(catacombsLowWallVisualRoot, false);
            child.transform.rotation = Quaternion.identity;
            child.transform.position = vertex + Vector2.up * (CatacombsLowWallFrontHeightWorld - CatacombsLowWallCapDepthWorld * .5f);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = new Color(.75f, .85f, 1f, 1f); renderer.flipY = flipY;
            renderer.sharedMaterial = catacombsLowWallRenderer.sharedMaterial;
            renderer.sortingLayerID = catacombsLowWallRenderer.sortingLayerID;
            renderer.sortingOrder = UpperVerticalCornerSortingOrder + 1;
            renderer.maskInteraction = catacombsLowWallRenderer.maskInteraction;
            child.layer = catacombsLowWallRenderer.gameObject.layer;
            lowWallJunctionRenderers.Add(renderer); lowWallJunctionSprites.Add(sprite);
        }

        private static Sprite LowWallSprite(Sprite sheet, int x, int y, int width, int height)
        {
            var sprite = Sprite.Create(sheet.texture,
                new Rect(sheet.rect.x + x, sheet.rect.yMax - y - height, width, height),
                Vector2.one * .5f, 32, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // Reuse the authored stone pixels without importing/editing textures.
        // The closed border belongs to a standalone panel, not every cell of a run.
        internal static Sprite CreateMasonryCapSprite(Sprite source)
        {
            var rect = source.rect;
            var border = rect.width > 2 ? 1 : 0;
            var height = Mathf.Min(8, Mathf.Max(1,Mathf.Floor(rect.height / 3)));
            return Sprite.Create(source.texture, new Rect(rect.x + border, rect.yMax - height, rect.width - border * 2, height),
                Vector2.one * .5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        internal static Sprite CreateMasonryFaceSprite(Sprite source, float widthWorld = 0)
        {
            var rect = source.rect;
            var border = rect.width > 2 ? 1 : 0;
            var bottom = rect.height > 2 ? 1 : 0;
            var width = widthWorld > 0 ? Mathf.Min(rect.width - border * 2, Mathf.Max(1,Mathf.Round(widthWorld * source.pixelsPerUnit))) : rect.width - border * 2;
            var height = Mathf.Max(1, rect.height - Mathf.Min(8, Mathf.Max(1,Mathf.Floor(rect.height / 3))) - bottom);
            return Sprite.Create(source.texture, new Rect(rect.center.x - width * .5f, rect.y + bottom, width, height),
                Vector2.one * .5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void ReleaseLowWallSprites()
        {
            foreach (var renderer in lowWallJunctionRenderers)
                if (renderer != null) { if (Application.isPlaying) Destroy(renderer.gameObject); else DestroyImmediate(renderer.gameObject); }
            foreach (var sprite in lowWallJunctionSprites) DestroyRuntimeCornerSprite(sprite);

            lowWallJunctionRenderers.Clear(); lowWallJunctionSprites.Clear();
            DestroyRuntimeCornerSprite(catacombsLowWallFace);
            DestroyRuntimeCornerSprite(catacombsLowWallCap);
            DestroyRuntimeCornerSprite(catacombsLowWallSideCap);
            DestroyRuntimeCornerSprite(catacombsLowWallEndFace);
            DestroyRuntimeCornerSprite(catacombsLowWallFootOutlineSprite);
            catacombsLowWallFootOutlineSprite = null;
            catacombsLowWallSideCap = null;



            catacombsLowWallSource = catacombsLowWallFace = catacombsLowWallCap = catacombsLowWallEndFace = null;
        }

        /// <summary>
        /// The world-space length used by the visual after <see cref="Bind"/>.
        /// The small extension over one cell makes adjacent wall sprites overlap at corners.
        /// </summary>
        public float VisualLength { get; private set; }
        public DungeonWallSurfaceProfile SurfaceProfile { get; private set; }
        public float DepthSortBaselineY { get; private set; }
        public bool IsVerticalLowWall { get; internal set; }
        public int DepthSortRole => SurfaceProfile switch
        {
            DungeonWallSurfaceProfile.North => 0,
            DungeonWallSurfaceProfile.Side => 1,
            DungeonWallSurfaceProfile.South => 2,
            _ => 3
        };

        public bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            var renderer = FindEnabledPresentationRenderer();
            if (renderer == null)
                return false;

            bounds = renderer.bounds;
            if (catacombsLowWallRenderer != null && catacombsLowWallRenderer.enabled && !catacombsLowWallRenderer.forceRenderingOff)
                bounds.Encapsulate(catacombsLowWallRenderer.bounds);
            if (catacombsLowWallDetailRenderer != null && catacombsLowWallDetailRenderer.enabled && !catacombsLowWallDetailRenderer.forceRenderingOff)
                bounds.Encapsulate(catacombsLowWallDetailRenderer.bounds);
            if (catacombsLowWallFootOutline != null && catacombsLowWallFootOutline.enabled && !catacombsLowWallFootOutline.forceRenderingOff)
                bounds.Encapsulate(catacombsLowWallFootOutline.bounds);
            return bounds.size.sqrMagnitude > .000001f;
        }

        private SpriteRenderer FindEnabledPresentationRenderer()
        {
            var rootRenderer = GetComponent<SpriteRenderer>();
            if (rootRenderer != null && rootRenderer.enabled && !rootRenderer.forceRenderingOff)
                return rootRenderer;

            foreach (var candidate in GetComponentsInChildren<SpriteRenderer>(true))
                if (candidate != null
                    && candidate.enabled
                    && !candidate.forceRenderingOff
                    && candidate.gameObject.activeSelf
                    && candidate.name != "SideVoidOcclusion"
                    && candidate.name != "SouthWallOcclusionMask")
                    return candidate;

            return null;
        }

        internal void CollectWallVisualBounds(List<Bounds> target)
        {
            if (target == null)
                return;

            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null
                    || !renderer.enabled
                    || !renderer.gameObject.activeSelf
                    || renderer.name == "SideVoidOcclusion"
                    || renderer.name == "SouthWallOcclusionMask")
                    continue;

                var bounds = renderer.bounds;
                if (bounds.size.x > .0001f && bounds.size.y > .0001f)
                    target.Add(bounds);
            }
        }

        public void SetDepthSortingOrder(int sortingOrder)
        {

            var sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null)
                return;

            depthSortingGroup ??= GetComponent<SortingGroup>();
            if (depthSortingGroup == null)
                depthSortingGroup = gameObject.AddComponent<SortingGroup>();

            depthSortingGroup.sortingLayerID = sourceRenderer.sortingLayerID;
            depthSortingGroup.sortingOrder = sortingOrder;
        }

        public void ClearSideVoidOcclusionRenderers()
        {
            foreach (var renderer in sideVoidOcclusionRenderers)
                if (renderer != null)
                    renderer.gameObject.SetActive(false);
        }

        public void ClearNorthSideJoinOverlays()
        {
            // Join miters share the same wall lifecycle as the rectangular join
            // overlay. Clear them here as well when the floor map rebuilds before
            // Unity destroys the old spawned wall object.
            ClearCornerMiterOverlays();
            ClearSideSouthJoinOverlays();
            foreach (var renderer in northSideJoinOverlays)
            {
                if (renderer == null)
                    continue;

                renderer.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(renderer.gameObject);
                else
                    DestroyImmediate(renderer.gameObject);
            }

            northSideJoinOverlays.Clear();
            foreach (var sprite in northSideJoinSprites)
            {
                if (sprite == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(sprite);
                else
                    DestroyImmediate(sprite);
            }

            northSideJoinSprites.Clear();
            foreach (var texture in northSideJoinTextures)
            {
                if (texture == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(texture);
                else
                    DestroyImmediate(texture);
            }

            northSideJoinTextures.Clear();
        }

        internal bool ConfigureSideSouthJoinOverlayAt(
            DungeonWallSegmentView sideSource,
            Vector2 endpoint,
            float southVisualOffsetWorld)
        {
            if (sideSource == null
                || SurfaceProfile != DungeonWallSurfaceProfile.South
                || !sideSource.TryGetSouthSideJoinOverlay(
                    endpoint,
                    southVisualOffsetWorld,
                    out var sourceOverlay))
                return false;

            var worldCenter = sourceOverlay.bounds.center;
            var overlay = CreateSideSouthJoinOverlay(sourceOverlay);
            if (overlay == null)
                return false;

            overlay.transform.position = worldCenter;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;
            // Rehoming must preserve the source role. A bind-time South face
            // patch is authored with the native South material, while an
            // ordinary South cap is authored with Side art. Treating both as
            // Side art creates a dark block at U-shaped South joins; treating
            // both as South art damages ordinary cap corners.
            var isSouthFace = sourceOverlay.name == "SouthSideFaceSourceOverlay";
            var sideRenderer = sideSource.GetComponent<SpriteRenderer>();
            var phaseSprite = !isSouthFace
                              && sideRenderer != null
                              && sideRenderer.enabled
                ? CreateSouthSidePhaseSprite(
                    sideRenderer.sprite,
                    sideRenderer.bounds,
                    sourceOverlay.bounds)
                : null;
            overlay.sprite = isSouthFace
                             && HasNativeSprite(southWallSprite, 32f, 8f)
                ? southWallSprite
                : phaseSprite ?? sourceOverlay.sprite;
            overlay.drawMode = phaseSprite != null
                ? SpriteDrawMode.Simple
                : sourceOverlay.drawMode;
            if (phaseSprite == null)
                overlay.size = sourceOverlay.size;
            overlay.sortingLayerID = sourceOverlay.sortingLayerID;
            overlay.sortingOrder = UpperVerticalCornerSortingOrder;
            overlay.color = sourceOverlay.color;
            overlay.sharedMaterial = sourceOverlay.sharedMaterial;
            overlay.maskInteraction = sourceOverlay.maskInteraction;
            overlay.forceRenderingOff = sourceOverlay.forceRenderingOff;
            overlay.gameObject.SetActive(true);
            return true;
        }

        private Sprite CreateSouthSidePhaseSprite(
            Sprite sourceSprite,
            Bounds sourceBounds,
            Bounds targetBounds)
        {
            if (!HasNativeSprite(sourceSprite, 8f, 32f)
                || sourceSprite.texture == null
                || sourceBounds.size.y <= .0001f
                || targetBounds.size.y <= .0001f)
                return null;

            var textureRect = ToTextureRectInt(sourceSprite.textureRect);
            var cropRect = GetCornerSourceCrop(
                sourceSprite,
                targetBounds.min,
                sourceBounds,
                CornerMiterRole.SouthFace,
                false);
            if (cropRect.width != 8 || cropRect.height != 8)
                return null;

            var pixels = GetCornerSourceRegion(
                sourceSprite.texture,
                cropRect,
                textureRect);
            var texture = new Texture2D(
                8,
                8,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "RuntimeDungeonSouthSidePhaseTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            sideSouthJoinTextures.Add(texture);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(.5f, .5f),
                sourceSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            sprite.name = "RuntimeDungeonSouthSidePhase";
            sprite.hideFlags = HideFlags.DontSave;
            sideSouthJoinSprites.Add(sprite);
            return sprite;
        }

        private SpriteRenderer CreateSideSouthJoinOverlay(SpriteRenderer sourceOverlay)
        {
            if (sourceOverlay == null || sourceOverlay.sprite == null)
                return null;

            foreach (var candidate in sideSouthJoinOverlays)
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.name = "SouthSideFaceOverlay";
                    return candidate;
                }

            var child = new GameObject("SouthSideFaceOverlay");
            child.transform.SetParent(transform, true);
            var renderer = child.AddComponent<SpriteRenderer>();
            sideSouthJoinOverlays.Add(renderer);
            return renderer;
        }

        private bool TryGetSouthSideJoinOverlay(
            Vector2 endpoint,
            float southVisualOffsetWorld,
            out SpriteRenderer overlay)
        {
            var expectedCenter = endpoint
                                 + Vector2.up
                                 * (southVisualOffsetWorld - NativeWallDepthWorld * .5f);
            SpriteRenderer faceCandidate = null;
            SpriteRenderer capCandidate = null;
            foreach (var candidate in southSideCapOverlays)
            {
                if (candidate == null
                    || !candidate.gameObject.activeInHierarchy
                    || !candidate.enabled
                    || candidate.sprite == null)
                    continue;

                if (Vector2.Distance(candidate.bounds.center, expectedCenter) <= .001f)
                {
                    if (candidate.name == "SouthSideFaceSourceOverlay")
                        faceCandidate = candidate;
                    else
                        capCandidate = candidate;
                }
            }

            overlay = faceCandidate != null ? faceCandidate : capCandidate;
            return overlay != null;
        }

        internal void DisableSouthSideJoinOverlayAt(
            Vector2 endpoint,
            float southVisualOffsetWorld)
        {
            var expectedCenter = endpoint
                                 + Vector2.up
                                 * (southVisualOffsetWorld - NativeWallDepthWorld * .5f);
            foreach (var candidate in southSideCapOverlays)
            {
                if (candidate == null
                    || !candidate.gameObject.activeSelf
                    || Vector2.Distance(candidate.bounds.center, expectedCenter) > .001f)
                    continue;

                candidate.gameObject.SetActive(false);
            }
        }

        private void ClearSideSouthJoinOverlays()
        {
            foreach (var renderer in sideSouthJoinOverlays)
            {
                if (renderer == null)
                    continue;

                renderer.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(renderer.gameObject);
                else
                    DestroyImmediate(renderer.gameObject);
            }

            sideSouthJoinOverlays.Clear();
            foreach (var sprite in sideSouthJoinSprites)
            {
                if (sprite == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(sprite);
                else
                    DestroyImmediate(sprite);
            }

            sideSouthJoinSprites.Clear();
            foreach (var texture in sideSouthJoinTextures)
            {
                if (texture == null)
                    continue;

                sharedCornerSourcePixels.Remove(texture);
                if (Application.isPlaying)
                    Destroy(texture);
                else
                    DestroyImmediate(texture);
            }

            sideSouthJoinTextures.Clear();
        }

        internal void DisableSouthCornerMiterAt(
            Vector2 endpoint,
            float southVisualOffsetWorld)
        {
            var cornerCenter = endpoint
                               + Vector2.up
                               * (southVisualOffsetWorld - NativeWallDepthWorld * .5f);
            foreach (var renderer in cornerMiterRenderers)
            {
                if (renderer == null)
                    continue;

                // South rehoming may replace a bind-time SouthCap pair
                // (CornerMiterSide/CornerMiterNorth) as well as a previous
                // SouthFace pair. Remove every miter exactly at this South
                // corner; the North cap at the same logical endpoint is
                // vertically offset by the native North depth and is kept.
                if (renderer.name.StartsWith("CornerMiter")
                    && Vector2.Distance(renderer.transform.position, cornerCenter) < .001f)
                    renderer.gameObject.SetActive(false);
            }

        }

        internal bool ConfigureSouthCornerMiterAt(
            Vector2 endpoint,
            Vector2 otherEndpoint,
            Vector2 horizontalDirection,
            float southVisualOffsetWorld,
            Bounds horizontalPhaseBounds,
            Bounds sidePhaseBounds,
            Sprite sideSourceSprite = null)
        {
            if (SurfaceProfile != DungeonWallSurfaceProfile.South
                && SurfaceProfile != DungeonWallSurfaceProfile.Side)
                return false;

            var sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null)
                return false;

            DisableSouthCornerMiterAt(endpoint, southVisualOffsetWorld);
            var before = cornerMiterRenderers.Count;
            ConfigureCornerMiter(
                endpoint,
                otherEndpoint,
                Vector2.right,
                thickness,
                sourceRenderer,
                CornerMiterRole.SouthFace,
                southVisualOffsetWorld,
                horizontalDirection,
                horizontalPhaseBounds,
                sidePhaseBounds,
                sideSourceSprite);
            return cornerMiterRenderers.Count > before;
        }

        private void ClearCornerMiterOverlays()
        {
            foreach (var renderer in cornerMiterRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(renderer.gameObject);
                else
                    DestroyImmediate(renderer.gameObject);
            }

            cornerMiterRenderers.Clear();
            foreach (var sprite in cornerMiterSprites)
            {
                if (sprite == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(sprite);
                else
                    DestroyImmediate(sprite);
            }

            cornerMiterSprites.Clear();
            foreach (var texture in cornerMiterTextures)
            {
                if (texture == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(texture);
                else
                    DestroyImmediate(texture);
            }

            cornerMiterTextures.Clear();
            cornerMiterTextureCache.Clear();
        }

        public void ConfigureNorthSideJoinOverlay(
            Bounds worldBounds,
            int sortingOrder)
        {
            ConfigureNorthSideJoinOverlay(worldBounds, sortingOrder, default);
        }

        public void ConfigureNorthSideJoinOverlay(
            Bounds worldBounds,
            int sortingOrder,
            Bounds sideSourceBounds,
            DungeonWallSegmentView sideSource = null)
        {
            if (SurfaceProfile != DungeonWallSurfaceProfile.North
                || worldBounds.size.x <= .0001f
                || worldBounds.size.y <= .0001f)
                return;

            var sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null || !sourceRenderer.enabled || sourceRenderer.sprite == null)
                return;

            var sourceSprite = sourceRenderer.sprite;
            var sourceBounds = sourceRenderer.bounds;
            var textureRect = sourceSprite.textureRect;
            var sourcePixelsPerUnit = sourceSprite.pixelsPerUnit;
            var cropWidth = Mathf.Clamp(
                Mathf.CeilToInt(worldBounds.size.x * sourcePixelsPerUnit),
                1,
                Mathf.Max(1, Mathf.FloorToInt(textureRect.width)));
            var cropHeight = Mathf.Clamp(
                Mathf.CeilToInt(worldBounds.size.y * sourcePixelsPerUnit),
                1,
                Mathf.Max(1, Mathf.FloorToInt(textureRect.height)));
            var cropMinX = Mathf.FloorToInt(Mathf.Repeat(
                (worldBounds.min.x - sourceBounds.min.x) * sourcePixelsPerUnit,
                textureRect.width));
            var cropMinY = Mathf.FloorToInt(Mathf.Repeat(
                (worldBounds.min.y - sourceBounds.min.y) * sourcePixelsPerUnit,
                textureRect.height));
            var cropRect = new RectInt(
                Mathf.FloorToInt(textureRect.x) + cropMinX,
                Mathf.FloorToInt(textureRect.y) + cropMinY,
                cropWidth,
                cropHeight);
            var joinTexture = new Texture2D(
                cropWidth,
                cropHeight,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = $"RuntimeDungeonNorthJoin_{GetInstanceID()}_{northSideJoinTextures.Count}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            joinTexture.SetPixels32(GetCornerSourceRegion(
                sourceSprite.texture,
                cropRect,
                ToTextureRectInt(textureRect)));
            joinTexture.Apply(false, false);
            northSideJoinTextures.Add(joinTexture);
            var joinSprite = Sprite.Create(
                joinTexture,
                new Rect(0f, 0f, cropWidth, cropHeight),
                new Vector2(.5f, .5f),
                sourcePixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            joinSprite.hideFlags = HideFlags.DontSave;
            northSideJoinSprites.Add(joinSprite);

            var child = new GameObject("NorthSideJoinOverlay");
            child.transform.SetParent(transform, true);
            child.transform.position = worldBounds.center;
            child.transform.rotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            var sortingGroup = child.AddComponent<SortingGroup>();
            sortingGroup.sortingLayerID = sourceRenderer.sortingLayerID;
            sortingGroup.sortingOrder = sortingOrder;
            sortingGroup.sortAtRoot = true;

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = joinSprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            var sourceScale = transform.lossyScale;
            var spriteWorldWidth = cropWidth / sourcePixelsPerUnit;
            var spriteWorldHeight = cropHeight / sourcePixelsPerUnit;
            renderer.transform.localScale = new Vector3(
                worldBounds.size.x / Mathf.Max(.0001f, spriteWorldWidth)
                    / Mathf.Max(.0001f, sourceScale.x),
                worldBounds.size.y / Mathf.Max(.0001f, spriteWorldHeight)
                    / Mathf.Max(.0001f, sourceScale.y),
                1f);
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = 0;
            renderer.color = sourceRenderer.color;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.forceRenderingOff = sourceRenderer.forceRenderingOff;
            northSideJoinOverlays.Add(renderer);
            if (TryConfigureNorthSideJoinCornerMiter(worldBounds, sortingOrder, sideSourceBounds)
                && sideSource != null)
                sideSource.DisableNorthCornerMiterAt(new Vector2(worldBounds.center.x,
                    sourceRenderer.bounds.max.y - NativeWallWidthWorld * .5f));
        }

        public void ConfigureNorthSideJoinCornerMiter(
            Bounds worldBounds,
            int sortingOrder)
        {
            ConfigureNorthSideJoinCornerMiter(worldBounds, sortingOrder, default);
        }

        public void ConfigureNorthSideJoinCornerMiter(
            Bounds worldBounds,
            int sortingOrder,
            Bounds sideSourceBounds)
        {
            TryConfigureNorthSideJoinCornerMiter(worldBounds, sortingOrder, sideSourceBounds);
        }

        private bool TryConfigureNorthSideJoinCornerMiter(
            Bounds worldBounds,
            int sortingOrder,
            Bounds sideSourceBounds)
        {
            if (SurfaceProfile != DungeonWallSurfaceProfile.North
                || worldBounds.size.x <= .0001f
                || worldBounds.size.y <= .0001f)
                return false;

            var sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null
                || !sourceRenderer.enabled
                || !HasNativeSprite(sideWallSprite, 8f, 32f)
                || !HasNativeSprite(northWallSprite, 32f, 32f))
                return false;

            var sourceBounds = sourceRenderer.bounds;
            var actualSideBounds = sideSourceBounds.size.sqrMagnitude > .0001f
                ? sideSourceBounds
                : sourceBounds;
            var cornerCenter = new Vector2(
                worldBounds.center.x,
                sourceBounds.max.y - NativeWallWidthWorld * .5f);
            foreach (var existing in cornerMiterRenderers)
            {
                if (existing == null
                    || !existing.gameObject.activeSelf
                    || existing.name != "CornerMiterNorthCap")
                    continue;

                // Full North and cap/Side bounds can describe the same
                // endpoint during one depth-sort pass. Keep one root-sorted
                // miter at that endpoint instead of stacking a second flat
                // strip over the L turn.
                if (Vector2.Distance(existing.transform.position, cornerCenter) < .001f)
                    return true;
            }

            // This is the lower Side endpoint of a stepped North wall. The
            // overlap may include the whole native North face, so its visible
            // elbow belongs to the upper 8 px band where the Side flank ends.
            // Anchoring to sourceBounds.min.y created a second, detached
            // horizontal strip at the bottom of the face.
            // The North arm runs from the shared endpoint toward the rest of
            // the horizontal wall. A join on the right side therefore samples
            // the left-going arm, and vice versa; keep this in world-neighbor
            // order instead of mirroring the authored North texture.
            var faceDirection = worldBounds.center.x >= sourceBounds.center.x ? 1f : -1f;
            // This join is selected only for the Side's lower logical endpoint.
            // Keep the cap miter in the upper North face band; the independent
            // endpoint cap resolver owns the North wall's other cap. The
            // miter's left edge is the same world pixel as the join crop's left
            // edge.
            // NorthCap sampling ignores Y, but Side sampling must use the actual
            // vertical renderer bounds rather than the North wall's bounds.
            var phaseWorldPosition = new Vector2(
                worldBounds.min.x,
                cornerCenter.y - NativeWallWidthWorld * .5f);
            var cornerSprite = CreateDedicatedCornerSprite(
                sideWallSprite,
                northWallSprite,
                phaseWorldPosition,
                actualSideBounds,
                sourceBounds,
                CornerMiterRole.NorthCap,
                faceDirection,
                -1f);
            if (cornerSprite == null)
                return false;

            var localPosition = transform.InverseTransformPoint(cornerCenter);
            var renderer = CreateCornerMiterRenderer(
                "CornerMiterNorthCap",
                cornerSprite,
                transform,
                localPosition,
                sourceRenderer,
                sortingOrder + 1,
                true);
            if (renderer != null)
                cornerMiterRenderers.Add(renderer);
            return renderer != null;
        }

        private void DisableNorthCornerMiterAt(Vector2 cornerCenter)
        {
            // The root-sorted North owner replaces the bind-time Side corner.
            // Keep a single crop at the shared endpoint across both wall owners.
            foreach (var renderer in cornerMiterRenderers)
                if (renderer != null && renderer.name == "CornerMiterNorthCap"
                    && Vector2.Distance(renderer.transform.position, cornerCenter) < .001f)
                    renderer.gameObject.SetActive(false);
        }

        public void ConfigureSideVoidOcclusion(Bounds worldBounds)
        {
            if (SurfaceProfile != DungeonWallSurfaceProfile.Side
                || worldBounds.size.x <= .0001f
                || worldBounds.size.y <= .0001f)
                return;

            var sourceRenderer = FindEnabledPresentationRenderer();
            if (sourceRenderer == null)
                return;

            SpriteRenderer renderer = null;
            foreach (var candidate in sideVoidOcclusionRenderers)
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    renderer = candidate;
                    break;
                }

            if (renderer == null)
            {
                var child = new GameObject("SideVoidOcclusion");
                child.transform.SetParent(transform, false);
                renderer = child.AddComponent<SpriteRenderer>();
                sideVoidOcclusionRenderers.Add(renderer);
            }

            if (sideVoidOcclusionSprite == null)
            {
                sideVoidOcclusionSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(.5f, .5f),
                    1f,
                    0,
                    SpriteMeshType.FullRect,
                    Vector4.zero);
                sideVoidOcclusionSprite.hideFlags = HideFlags.DontSave;
            }

            var localCorners = new Vector3[4];
            localCorners[0] = transform.InverseTransformPoint(
                new Vector3(worldBounds.min.x, worldBounds.min.y, transform.position.z));
            localCorners[1] = transform.InverseTransformPoint(
                new Vector3(worldBounds.min.x, worldBounds.max.y, transform.position.z));
            localCorners[2] = transform.InverseTransformPoint(
                new Vector3(worldBounds.max.x, worldBounds.min.y, transform.position.z));
            localCorners[3] = transform.InverseTransformPoint(
                new Vector3(worldBounds.max.x, worldBounds.max.y, transform.position.z));

            var localMin = localCorners[0];
            var localMax = localCorners[0];
            for (var index = 1; index < localCorners.Length; index++)
            {
                localMin = Vector3.Min(localMin, localCorners[index]);
                localMax = Vector3.Max(localMax, localCorners[index]);
            }

            var childTransform = renderer.transform;
            childTransform.localPosition = (localMin + localMax) * .5f;
            childTransform.localRotation = Quaternion.identity;
            childTransform.localScale = Vector3.one;
            renderer.sprite = sideVoidOcclusionSprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(localMax.x - localMin.x, localMax.y - localMin.y);
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = VerticalWallSortingOrder - 1;
            renderer.color = Color.black;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            ReleaseLowWallSprites();
            ClearCornerMiterOverlays();
            ClearNorthSideJoinOverlays();
            if (mixedSideFaceSprite != null)
            {
                if (Application.isPlaying)
                    Destroy(mixedSideFaceSprite);
                else
                    DestroyImmediate(mixedSideFaceSprite);
                mixedSideFaceSprite = null;
            }
            if (mixedSideFaceTexture != null)
            {
                sharedCornerSourcePixels.Remove(mixedSideFaceTexture);
                if (Application.isPlaying)
                    Destroy(mixedSideFaceTexture);
                else
                    DestroyImmediate(mixedSideFaceTexture);
                mixedSideFaceTexture = null;
            }
            if (southOcclusionSprite != null)
            {
                if (southOcclusionRenderer != null)
                    southOcclusionRenderer.sprite = null;

                if (Application.isPlaying)
                    Destroy(southOcclusionSprite);
                else
                    DestroyImmediate(southOcclusionSprite);
                southOcclusionSprite = null;
            }

            if (sideVoidOcclusionSprite != null)
            {
                if (Application.isPlaying)
                    Destroy(sideVoidOcclusionSprite);
                else
                    DestroyImmediate(sideVoidOcclusionSprite);
                sideVoidOcclusionSprite = null;
            }
        }

        public void BindVegetationBoundary(DungeonCellEdge edge, Tilemap floorTilemap, DungeonWallSurfaceProfile profile)
        {
            var first = floorTilemap.GetCellCenterWorld(new Vector3Int(edge.FirstCell.x, edge.FirstCell.y, 0));
            var second = floorTilemap.GetCellCenterWorld(new Vector3Int(edge.SecondCell.x, edge.SecondCell.y, 0));
            var midpoint = (first + second) * .5f;
            VisualLength = Vector2.Distance(first, second);
            SurfaceProfile = ResolveSurfaceProfile(edge, profile);
            if (SurfaceProfile == DungeonWallSurfaceProfile.North && !HasNativeSprite(northWallSprite, 32f, 32f))
                SurfaceProfile = DungeonWallSurfaceProfile.Side;
            else if (SurfaceProfile == DungeonWallSurfaceProfile.South && !HasNativeSprite(southWallSprite, 32f, 8f))
                SurfaceProfile = DungeonWallSurfaceProfile.Side;
            DepthSortBaselineY = midpoint.y;
            if (SurfaceProfile == DungeonWallSurfaceProfile.Side && edge.FirstCell.x != edge.SecondCell.x)
                DepthSortBaselineY -= VisualLength * .5f;
            transform.position = midpoint;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        }

        public void Bind(
            DungeonCellEdge edge,
            Tilemap floorTilemap,
            bool blocksMovement = true,
            bool extendFirstEnd = true,
            bool extendSecondEnd = true,
            bool renderUpperVerticalCorner = false,
            DungeonWallSurfaceProfile surfaceProfile = DungeonWallSurfaceProfile.Auto,
            bool renderNorthSideCapAtFirstEnd = false,
            bool renderNorthSideCapAtSecondEnd = false,
            bool renderSouthSideCapAtFirstEnd = false,
            bool renderSouthSideCapAtSecondEnd = false,
            bool renderSouthSideFaceAtFirstEnd = false,
            bool renderSouthSideFaceAtSecondEnd = false,
            bool offsetSouthSideVisualAtFirstEnd = false,
            bool offsetSouthSideVisualAtSecondEnd = false,
            bool renderNorthSideMiterAtFirstEnd = false,
            bool renderNorthSideMiterAtSecondEnd = false,
            bool renderSouthSideMiterAtFirstEnd = false,
            bool renderSouthSideMiterAtSecondEnd = false,
            Vector2 northCornerHorizontalDirectionAtFirstEnd = default,
            Vector2 northCornerHorizontalDirectionAtSecondEnd = default,
            Vector2 southCornerHorizontalDirectionAtFirstEnd = default,
            Vector2 southCornerHorizontalDirectionAtSecondEnd = default,
            bool renderSideBodyAsFace = false)
        {
            SetUpperVerticalCornerOverlayActive(false);
            SetNorthSideCapOverlaysActive(false);
            SetSouthSideCapOverlaysActive(false);
            SetSouthOcclusionMaskActive(false);
            ClearSideVoidOcclusionRenderers();
            ClearCornerMiterOverlays();

            if (floorTilemap == null)
            {
                return;
            }

            if (movementCollider != null) movementCollider.enabled = blocksMovement;
            var firstCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    edge.FirstCell.x,
                    edge.FirstCell.y,
                    0));

            var secondCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    edge.SecondCell.x,
                    edge.SecondCell.y,
                    0));

            var delta = edge.SecondCell - edge.FirstCell;
            var cellLength = Vector2.Distance(firstCenter, secondCenter);
            SurfaceProfile = ResolveSurfaceProfile(edge, surfaceProfile);
            if (SurfaceProfile == DungeonWallSurfaceProfile.North
                && !HasNativeSprite(northWallSprite, 32f, 32f))
                SurfaceProfile = DungeonWallSurfaceProfile.Side;
            else if (SurfaceProfile == DungeonWallSurfaceProfile.South
                     && !HasNativeSprite(southWallSprite, 32f, 8f))
                SurfaceProfile = DungeonWallSurfaceProfile.Side;
            var sprite = ResolveSprite(delta, SurfaceProfile);
            var renderer = GetComponent<SpriteRenderer>();
            var useNativeVisual = useNativeWallCrossSection && renderer != null && sprite != null;
            var usesScreenOrientedProfile = useNativeVisual
                                            && (SurfaceProfile == DungeonWallSurfaceProfile.North
                                                || SurfaceProfile == DungeonWallSurfaceProfile.South)
                                            && delta.y != 0;
            var wallThickness = Mathf.Max(0f, thickness);
            var normal = (secondCenter - firstCenter).normalized;
            var tangent = new Vector2(-normal.y, normal.x);
            var halfThickness = wallThickness * .5f;
            // The map passes endpoint flags only where a perpendicular wall closes
            // the corner. Direct callers can keep the legacy extension defaults.
            var canExtendVisual = useNativeWallCrossSection;
            var firstExtension = canExtendVisual && extendFirstEnd ? halfThickness : 0f;
            var secondExtension = canExtendVisual && extendSecondEnd ? halfThickness : 0f;
            var midpoint = (Vector2)(firstCenter + secondCenter) * .5f;
            DepthSortBaselineY = midpoint.y;
            if (SurfaceProfile == DungeonWallSurfaceProfile.Side && delta.x != 0)
                DepthSortBaselineY -= cellLength * .5f;
            var visualStart = midpoint - tangent * (cellLength * .5f + firstExtension);
            var visualEnd = midpoint + tangent * (cellLength * .5f + secondExtension);
            var sideSouthOffsetPerCell = useNativeVisual
                && SurfaceProfile == DungeonWallSurfaceProfile.Side
                && delta.x != 0
                ? GetVerticalCellLength(floorTilemap, edge.FirstCell)
                    * Mathf.Max(0f, southVisualOffsetCells)
                : 0f;
            var firstSideSouthOffset = offsetSouthSideVisualAtFirstEnd
                && !renderNorthSideCapAtFirstEnd
                ? sideSouthOffsetPerCell
                : 0f;
            var secondSideSouthOffset = offsetSouthSideVisualAtSecondEnd
                && !renderNorthSideCapAtSecondEnd
                ? sideSouthOffsetPerCell
                : 0f;
            if (firstSideSouthOffset > 0f)
                visualStart += Vector2.up * firstSideSouthOffset;
            if (secondSideSouthOffset > 0f)
                visualEnd += Vector2.up * secondSideSouthOffset;
            VisualLength = Vector2.Distance(visualStart, visualEnd);

            transform.position = (visualStart + visualEnd) * 0.5f;

            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                usesScreenOrientedProfile || delta.x != 0 ? 0f : 90f);

            var visualDepth = GetVisualDepth(SurfaceProfile);
            var outwardSign = SurfaceProfile == DungeonWallSurfaceProfile.North ? 1f
                : SurfaceProfile == DungeonWallSurfaceProfile.South ? -1f : 0f;
            var hasSouthVisualOffset = useNativeVisual
                && (SurfaceProfile == DungeonWallSurfaceProfile.South
                    || SurfaceProfile == DungeonWallSurfaceProfile.Side && delta.x != 0);
            var southOffsetCellLength = SurfaceProfile == DungeonWallSurfaceProfile.Side && delta.x != 0
                ? GetVerticalCellLength(floorTilemap, edge.FirstCell)
                : cellLength;
            var southVisualOffsetWorld = hasSouthVisualOffset
                ? southOffsetCellLength * Mathf.Max(0f, southVisualOffsetCells)
                : 0f;
            if (usesScreenOrientedProfile)
                transform.position += transform.up * (outwardSign * visualDepth * .5f
                    + (SurfaceProfile == DungeonWallSurfaceProfile.South ? southVisualOffsetWorld : 0f));

            if (renderer != null)
            {
                // Keep the profile's local band ordering independent from the
                // optional South visual offset and preserve legacy presentation.
                if (useNativeWallCrossSection)
                    renderer.sortingOrder = usesScreenOrientedProfile || delta.x == 0
                        ? HorizontalWallSortingOrder
                        : VerticalWallSortingOrder;

                if (useNativeVisual)
                {
                    renderer.sprite = SurfaceProfile == DungeonWallSurfaceProfile.Side
                                       && delta.x != 0
                                       && renderSideBodyAsFace
                        ? ResolveMixedSideFaceSprite() ?? sprite
                        : sprite;
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    transform.localScale = Vector3.one;
                    renderer.size = usesScreenOrientedProfile
                        ? new Vector2(VisualLength, visualDepth)
                        : new Vector2(wallThickness, VisualLength);

                    if (renderUpperVerticalCorner
                        && SurfaceProfile == DungeonWallSurfaceProfile.Auto
                        && delta.x != 0)
                        ConfigureUpperVerticalCornerOverlay(
                            firstCenter,
                            secondCenter,
                            midpoint,
                            tangent,
                            wallThickness,
                            renderer);

                    if (SurfaceProfile == DungeonWallSurfaceProfile.Side
                        && delta.x != 0)
                    {
                        var firstSideEndpoint = midpoint - tangent * (cellLength * .5f);
                        var secondSideEndpoint = midpoint + tangent * (cellLength * .5f);
                        if (renderNorthSideCapAtFirstEnd)
                        {
                            ConfigureNorthSideCapOverlay(
                                firstSideEndpoint,
                                wallThickness,
                                renderer);
                            ConfigureCornerMiter(
                                firstSideEndpoint,
                                secondSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.NorthCap,
                                0f,
                                northCornerHorizontalDirectionAtFirstEnd);
                        }
                        if (renderNorthSideCapAtSecondEnd)
                        {
                            ConfigureNorthSideCapOverlay(
                                secondSideEndpoint,
                                wallThickness,
                                renderer);
                            ConfigureCornerMiter(
                                secondSideEndpoint,
                                firstSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.NorthCap,
                                0f,
                                northCornerHorizontalDirectionAtSecondEnd);
                        }
                        if (renderSouthSideCapAtFirstEnd)
                        {
                            ConfigureSouthSideCapOverlay(
                                firstSideEndpoint,
                                wallThickness,
                                renderer,
                                false,
                                southVisualOffsetWorld);
                            ConfigureCornerMiter(
                                firstSideEndpoint,
                                secondSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.SouthCap,
                                southVisualOffsetWorld,
                                southCornerHorizontalDirectionAtFirstEnd);
                        }
                        if (renderSouthSideCapAtSecondEnd)
                        {
                            ConfigureSouthSideCapOverlay(
                                secondSideEndpoint,
                                wallThickness,
                                renderer,
                                false,
                                southVisualOffsetWorld);
                            ConfigureCornerMiter(
                                secondSideEndpoint,
                                firstSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.SouthCap,
                                southVisualOffsetWorld,
                                southCornerHorizontalDirectionAtSecondEnd);
                        }
                        if (renderSouthSideFaceAtFirstEnd)
                        {
                            ConfigureSouthSideCapOverlay(
                                firstSideEndpoint,
                                wallThickness,
                                renderer,
                                true,
                                southVisualOffsetWorld);
                            ConfigureCornerMiter(
                                firstSideEndpoint,
                                secondSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.SouthFace,
                                southVisualOffsetWorld,
                                southCornerHorizontalDirectionAtFirstEnd);
                        }
                        if (renderSouthSideFaceAtSecondEnd)
                        {
                            ConfigureSouthSideCapOverlay(
                                secondSideEndpoint,
                                wallThickness,
                                renderer,
                                true,
                                southVisualOffsetWorld);
                            ConfigureCornerMiter(
                                secondSideEndpoint,
                                firstSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.SouthFace,
                                southVisualOffsetWorld,
                                southCornerHorizontalDirectionAtSecondEnd);
                        }
                        if (renderSouthSideMiterAtFirstEnd
                            && !renderSouthSideCapAtFirstEnd
                            && !renderSouthSideFaceAtFirstEnd)
                        {
                            ConfigureCornerMiter(
                                firstSideEndpoint,
                                secondSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.SouthFace,
                                southVisualOffsetWorld,
                                southCornerHorizontalDirectionAtFirstEnd);
                        }
                        if (renderSouthSideMiterAtSecondEnd
                            && !renderSouthSideCapAtSecondEnd
                            && !renderSouthSideFaceAtSecondEnd)
                        {
                            ConfigureCornerMiter(
                                secondSideEndpoint,
                                firstSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.SouthFace,
                                southVisualOffsetWorld,
                                southCornerHorizontalDirectionAtSecondEnd);
                        }
                        if (renderNorthSideMiterAtFirstEnd
                            && !renderNorthSideCapAtFirstEnd)
                        {
                            ConfigureCornerMiter(
                                firstSideEndpoint,
                                secondSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.NorthCap,
                                0f,
                                northCornerHorizontalDirectionAtFirstEnd);
                        }
                        if (renderNorthSideMiterAtSecondEnd
                            && !renderNorthSideCapAtSecondEnd)
                        {
                            ConfigureCornerMiter(
                                secondSideEndpoint,
                                firstSideEndpoint,
                                normal,
                                wallThickness,
                                renderer,
                                CornerMiterRole.NorthCap,
                                0f,
                                northCornerHorizontalDirectionAtSecondEnd);
                        }
                    }
                }
                else
                {
                    // Legacy variants use their existing sprite and root X scale;
                    // their child decorations remain in the original coordinate
                    // system and are deliberately not tiled or extended.
                    renderer.drawMode = SpriteDrawMode.Simple;
                    transform.localScale = new Vector3(wallThickness, VisualLength, 1f);
                }
            }
            else
            {
                transform.localScale = new Vector3(wallThickness, VisualLength, 1f);
            }

            if (movementCollider is BoxCollider2D boxCollider && VisualLength > 0f)
            {
                // Keep the logical blocker centred on the original edge even when
                // a North/South sprite is visually offset from that boundary.
                var tangentOffset = (firstExtension - secondExtension) * .5f;
                var localColliderWidth = useNativeVisual
                    ? usesScreenOrientedProfile ? cellLength : wallThickness
                    : 1f;
                var localColliderLength = useNativeVisual
                    ? usesScreenOrientedProfile ? wallThickness : cellLength
                    : cellLength / VisualLength;
                var normalOffset = usesScreenOrientedProfile
                    ? -outwardSign * visualDepth * .5f
                        - (SurfaceProfile == DungeonWallSurfaceProfile.South
                            ? southVisualOffsetWorld
                            : 0f)
                    : 0f;
                boxCollider.size = new Vector2(localColliderWidth, localColliderLength);
                var localTangentOffset = usesScreenOrientedProfile && delta.y != 0
                    ? -tangentOffset
                    : tangentOffset - (firstSideSouthOffset + secondSideSouthOffset) * .5f;
                boxCollider.offset = usesScreenOrientedProfile
                    ? new Vector2(localTangentOffset, normalOffset)
                    : new Vector2(0f, localTangentOffset);
            }

            if (SurfaceProfile == DungeonWallSurfaceProfile.South
                && usesScreenOrientedProfile
                && useNativeVisual)
                ConfigureSouthOcclusionMask(
                    midpoint,
                    VisualLength,
                    visualDepth,
                    southVisualOffsetWorld,
                    renderer);
        }

        private DungeonWallSurfaceProfile ResolveSurfaceProfile(
            DungeonCellEdge edge,
            DungeonWallSurfaceProfile requestedProfile)
        {
            if (requestedProfile == DungeonWallSurfaceProfile.North
                || requestedProfile == DungeonWallSurfaceProfile.South)
                return edge.FirstCell.y != edge.SecondCell.y
                    ? requestedProfile
                    : DungeonWallSurfaceProfile.Side;

            return requestedProfile;
        }

        private Sprite ResolveSprite(Vector2Int delta, DungeonWallSurfaceProfile profile)
        {
            switch (profile)
            {
                case DungeonWallSurfaceProfile.Side:
                    return HasNativeSprite(sideWallSprite, 8f, 32f)
                        ? sideWallSprite
                        : delta.x != 0 ? verticalWallSprite : horizontalWallSprite;
                case DungeonWallSurfaceProfile.North:
                    return northWallSprite;
                case DungeonWallSurfaceProfile.South:
                    return southWallSprite;
                default:
                    return delta.x != 0 ? verticalWallSprite : horizontalWallSprite;
            }
        }

        private float GetVisualDepth(DungeonWallSurfaceProfile profile)
        {
            return profile == DungeonWallSurfaceProfile.North
                ? NativeNorthWallDepthWorld
                : NativeWallDepthWorld;
        }

        private Sprite ResolveMixedSideFaceSprite()
        {
            if (mixedSideFaceSprite != null)
                return mixedSideFaceSprite;

            if (!HasNativeSprite(northWallSprite, 32f, 32f)
                || northWallSprite.texture == null)
                return null;

            var northRect = ToTextureRectInt(northWallSprite.textureRect);
            var faceHeight = 24;
            var capHeight = 8;
            var facePixels = GetCornerSourceRegion(
                northWallSprite.texture,
                new RectInt(northRect.x, northRect.y, 8, faceHeight),
                northRect);
            mixedSideFaceTexture = new Texture2D(
                8,
                32,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "RuntimeDungeonMixedSideFaceTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var composedPixels = new Color32[8 * 32];
            // The mixed body is a face all the way to its lower boundary. A
            // Side32 endpoint band here is a real horizontal cap, so placing
            // it in the lower eight pixels creates a small square in the
            // middle of an internal partition. Continue the North face below
            // its native 24-pixel crop by mirroring its bottom eight pixels;
            // the boundary row (dest y=7/source y=0) remains identical to the
            // first face row at dest y=8. The lower segment stays face art
            // rather than introducing a second endpoint material; the native
            // North cap rows are never copied.
            for (var y = 0; y < capHeight; y++)
                for (var x = 0; x < 8; x++)
                    composedPixels[y * 8 + x]
                        = facePixels[(capHeight - 1 - y) * 8 + x];
            for (var y = 0; y < faceHeight; y++)
                for (var x = 0; x < 8; x++)
                    composedPixels[(y + capHeight) * 8 + x] = facePixels[y * 8 + x];
            mixedSideFaceTexture.SetPixels32(composedPixels);
            mixedSideFaceTexture.Apply(false, false);
            mixedSideFaceSprite = Sprite.Create(
                mixedSideFaceTexture,
                new Rect(0f, 0f, 8f, 32f),
                new Vector2(.5f, .5f),
                northWallSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            mixedSideFaceSprite.name = "RuntimeDungeonMixedSideFace";
            mixedSideFaceSprite.hideFlags = HideFlags.DontSave;
            return mixedSideFaceSprite;
        }

        private enum CornerMiterRole
        {
            NorthCap,
            SouthCap,
            SouthFace
        }

        private void ConfigureCornerMiter(
            Vector2 endpoint,
            Vector2 otherEndpoint,
            Vector2 wallNormal,
            float wallThickness,
            SpriteRenderer sourceRenderer,
            CornerMiterRole role,
            float southVisualOffsetWorld,
            Vector2 horizontalDirection = default,
            Bounds horizontalPhaseBounds = default,
            Bounds sidePhaseBounds = default,
            Sprite sideSourceOverride = null)
        {
            var miterSideSource = role == CornerMiterRole.SouthFace
                                  && HasNativeSprite(sideSourceOverride, 8f, 32f)
                ? sideSourceOverride
                : role == CornerMiterRole.SouthFace
                                  && HasNativeSprite(sourceRenderer != null
                                      ? sourceRenderer.sprite
                                      : null, 8f, 32f)
                ? sourceRenderer.sprite
                : sideWallSprite;
            if (wallThickness <= .0001f
                || sourceRenderer == null
                || !sourceRenderer.enabled
                || !HasNativeSprite(miterSideSource, 8f, 32f))
                return;

            var horizontalSprite = role == CornerMiterRole.NorthCap
                ? northWallSprite
                : southWallSprite;
            var horizontalWidth = 32f;
            var horizontalHeight = role == CornerMiterRole.NorthCap ? 32f : 8f;
            if (!HasNativeSprite(horizontalSprite, horizontalWidth, horizontalHeight))
                return;

            var isUpperEndpoint = endpoint.y >= otherEndpoint.y;
            var cornerCenter = role == CornerMiterRole.NorthCap
                // The North wall face occupies the logical boundary through
                // one cell of visual depth. Its authored cap is the upper
                // 8 px, so the miter must close that top band rather than the
                // lower face at the logical endpoint.
                ? endpoint + Vector2.up * (NativeNorthWallDepthWorld - wallThickness * .5f)
                : endpoint + Vector2.up * (southVisualOffsetWorld - wallThickness * .5f);
            var faceDirection = horizontalDirection.sqrMagnitude > .0001f
                ? horizontalDirection.x >= 0f ? -1f : 1f
                : wallNormal.x >= 0f ? -1f : 1f;
            var verticalDirection = isUpperEndpoint ? 1f : -1f;
            var phaseWorldPosition = role == CornerMiterRole.SouthFace
                ? cornerCenter - Vector2.one * (NativeWallWidthWorld * .5f)
                : endpoint;
            var localPosition = transform.InverseTransformPoint(cornerCenter);
            var sourceBounds = sourceRenderer.bounds;
            var sideSourceBounds = sidePhaseBounds.size.sqrMagnitude > .0001f
                ? sidePhaseBounds
                : sourceBounds;
            var horizontalSourceBounds = horizontalPhaseBounds.size.sqrMagnitude > .0001f
                ? horizontalPhaseBounds
                : sourceBounds;
            var cornerSprite = CreateDedicatedCornerSprite(
                miterSideSource,
                horizontalSprite,
                phaseWorldPosition,
                sideSourceBounds,
                horizontalSourceBounds,
                role,
                faceDirection,
                verticalDirection);
            if (cornerSprite == null)
                return;

            var renderer = CreateCornerMiterRenderer(
                role == CornerMiterRole.NorthCap
                    ? "CornerMiterNorthCap"
                    : role == CornerMiterRole.SouthCap
                        ? "CornerMiterSouthCap"
                        : "CornerMiterSouthFace",
                cornerSprite,
                transform,
                localPosition,
                sourceRenderer,
                role == CornerMiterRole.SouthFace
                    ? UpperVerticalCornerSortingOrder + 2
                    : UpperVerticalCornerSortingOrder + 1,
                false);
            if (renderer != null)
                cornerMiterRenderers.Add(renderer);
        }

        private static Color32 SelectBrighterBorderColor(Color32 first, Color32 second)
        {
            var firstLuma = (int)first.r + first.g + first.b;
            var secondLuma = (int)second.r + second.g + second.b;
            return secondLuma > firstLuma ? second : first;
        }

        private static Color32 SelectBrighterVerticalBorderColor(
            Color32[] source,
            int x,
            int y)
        {
            if (source == null
                || source.Length < RuntimeCornerTexturePixels * RuntimeCornerTexturePixels
                || x < 0
                || x >= RuntimeCornerTexturePixels
                || y < 0
                || y >= RuntimeCornerTexturePixels)
                return default;

            var selected = source[y * RuntimeCornerTexturePixels + x];
            if (y > 0)
                selected = SelectBrighterBorderColor(
                    selected,
                    source[(y - 1) * RuntimeCornerTexturePixels + x]);
            if (y + 1 < RuntimeCornerTexturePixels)
                selected = SelectBrighterBorderColor(
                    selected,
                    source[(y + 1) * RuntimeCornerTexturePixels + x]);
            return selected;
        }

        private Sprite CreateDedicatedCornerSprite(
            Sprite sideSource,
            Sprite horizontalSource,
            Vector2 worldPosition,
            Bounds sideSourceBounds,
            Bounds horizontalSourceBounds,
            CornerMiterRole role,
            float geometryXDirection,
            float geometryYDirection,
            int endpointArms = 0)
        {
            if (sideSource == null
                || horizontalSource == null
                || sideSource.texture == null
                || horizontalSource.texture == null
                || sideSource.pixelsPerUnit <= .0001f
                || horizontalSource.pixelsPerUnit <= .0001f)
                return null;

            var sideRect = GetCornerSourceCrop(
                sideSource,
                worldPosition,
                sideSourceBounds,
                role,
                false);
            var horizontalRect = GetCornerSourceCrop(
                horizontalSource,
                worldPosition,
                horizontalSourceBounds,
                role,
                true);
            var cacheKey = string.Join(
                ":",
                sideSource.texture.GetInstanceID(),
                horizontalSource.texture.GetInstanceID(),
                sideRect.x,
                sideRect.y,
                horizontalRect.x,
                horizontalRect.y,
                (int)role,
                endpointArms,
                geometryXDirection < 0f ? 0 : 1,
                geometryYDirection < 0f ? 0 : 1);

            if (!cornerMiterTextureCache.TryGetValue(cacheKey, out var cornerTexture)
                || cornerTexture == null)
            {
                var sideColors = GetCornerSourceRegion(
                    sideSource.texture,
                    sideRect,
                    ToTextureRectInt(sideSource.textureRect));
                var horizontalColors = GetCornerSourceRegion(
                    horizontalSource.texture,
                    horizontalRect,
                    ToTextureRectInt(horizontalSource.textureRect));
                // A corner is a border junction, not another tiled wall
                // slice. Use one native face pixel for the interior so an
                // unrelated vertical ornament from the South crop cannot
                // become a second seam inside the 8x8 turn.
                var interiorColor = horizontalColors[
                    (RuntimeCornerTexturePixels / 2) * RuntimeCornerTexturePixels
                    + RuntimeCornerTexturePixels / 2];
                var cornerColors = new Color32[
                    RuntimeCornerTexturePixels * RuntimeCornerTexturePixels];
                for (var y = 0; y < RuntimeCornerTexturePixels; y++)
                    for (var x = 0; x < RuntimeCornerTexturePixels; x++)
                    {
                        // Build one axis-aligned L/turn. Horizontal wall art
                        // is authored in world-X order, so its crop phase
                        // already selects the world-aligned source window and
                        // U remains x. Only the Side strip chooses an edge
                        // orientation; no diagonal mesh or mirrored
                        // horizontal UV is introduced.
                        var horizontalSourceX = x;
                        var sideSourceX = role == CornerMiterRole.SouthFace
                            || role == CornerMiterRole.NorthCap
                            ? x
                            : geometryXDirection < 0f
                                ? x
                                : RuntimeCornerTexturePixels - 1 - x;
                        // South face art always meets the walkable floor on
                        // its upper edge. Keep that authored top border on the
                        // physical top of the dedicated sprite; using the
                        // endpoint ordering here mirrored the border to the
                        // bottom and left a black cross at SW/SE joins.
                        var sourceY = role == CornerMiterRole.SouthFace
                            ? y
                            : role == CornerMiterRole.NorthCap
                                ? y
                            : geometryYDirection < 0f
                                ? y
                                : RuntimeCornerTexturePixels - 1 - y;
                        var sideSourceY = role == CornerMiterRole.SouthFace
                            ? y
                            : sourceY;
                        var sideIndex = sideSourceY * RuntimeCornerTexturePixels + sideSourceX;
                        var horizontalIndex = sourceY * RuntimeCornerTexturePixels + horizontalSourceX;
                        if (endpointArms != 0 && role == CornerMiterRole.NorthCap
                            && endpointArms != 5 && endpointArms != 6 && endpointArms != 9 && endpointArms != 10)
                        {
                            // Ends, T and cross use the very same native border
                            // tracks as L miters. An incoming arm opens its side
                            // of the vertex instead of leaving a closed seam.
                            var horizontalBorder=(y==1&&(endpointArms&4)==0)||(y==6&&(endpointArms&8)==0);
                            var sideBorder=((x==1&&(endpointArms&1)==0)||(x==6&&(endpointArms&2)==0))
                                && (endpointArms != 4 || y <= 6) && (endpointArms != 8 || y >= 1);
                            var horizontalColor=horizontalColors[horizontalIndex];
                            var sideColor=SelectBrighterVerticalBorderColor(sideColors,sideSourceX,sideSourceY);
                            cornerColors[y*RuntimeCornerTexturePixels+x]=horizontalBorder&&sideBorder
                                ?SelectBrighterBorderColor(horizontalColor,sideColor)
                                :horizontalBorder?horizontalColor:sideBorder?sideColor
                                :sideColors[Mathf.Clamp(y,2,4)*RuntimeCornerTexturePixels+Mathf.Clamp(x,2,5)];
                        }
                        else if (role == CornerMiterRole.SouthFace)
                        {
                            // South32 contains two authored horizontal border runs
                            // (rows 1 and 6), and Side32 contains their vertical
                            // counterparts (columns 1 and 6). Each source run is
                            // clipped to the physical arm that reaches this corner:
                            // for LEFT+DOWN (SE), for example, row6/col6 end at
                            // (6,6) and row1/col1 end at (1,1). This creates two
                            // continuous L paths instead of a full cross.
                            var horizontalArmLeft = geometryXDirection > 0f;
                            var verticalArmDown = geometryYDirection > 0f;
                            // Both arms must derive their turn from the same
                            // pair of physical directions. Otherwise one
                            // mirrored combination joins row 1 to column 6
                            // while the other still joins row 1 to column 1.
                            var sameTurnSide = horizontalArmLeft == verticalArmDown;
                            var turnX = sameTurnSide
                                ? y
                                : RuntimeCornerTexturePixels - 1 - y;
                            var turnY = sameTurnSide
                                ? x
                                : RuntimeCornerTexturePixels - 1 - x;
                            var sideBorderColumn = x == 1 || x == 6;
                            var horizontalBorder = (y == 1 || y == 6)
                                                   && (horizontalArmLeft ? x <= turnX : x >= turnX);
                            var sideBorder = sideBorderColumn
                                             && (verticalArmDown ? y <= turnY : y >= turnY);
                            var horizontalColor = horizontalColors[horizontalIndex];
                            var sideColor = sideColors[sideIndex];
                            cornerColors[y * RuntimeCornerTexturePixels + x] =
                                horizontalBorder && sideBorder
                                    ? SelectBrighterBorderColor(horizontalColor, sideColor)
                                    : horizontalBorder
                                        ? horizontalColor
                                        : sideBorder
                                            ? sideColor
                                        : interiorColor;
                        }
                        else if (role == CornerMiterRole.NorthCap)
                        {
                            // North caps use the same two authored border
                            // tracks as the native cap/Side join. Keep the
                            // source sampling in world order, and use the
                            // physical arm directions only to clip each track
                            // into an L. This avoids the old full 2 px bands
                            // that made the inner turn look like a flat block.
                            var horizontalArmLeft = geometryXDirection > 0f;
                            var verticalArmDown = geometryYDirection > 0f;
                            var sameTurnSide = horizontalArmLeft == verticalArmDown;
                            var turnX = sameTurnSide
                                ? y
                                : RuntimeCornerTexturePixels - 1 - y;
                            var turnY = sameTurnSide
                                ? x
                                : RuntimeCornerTexturePixels - 1 - x;
                            var sideBorderColumn = x == 1 || x == 6;
                            var horizontalBorder = (y == 1 || y == 6)
                                                   && (horizontalArmLeft ? x <= turnX : x >= turnX);
                            var sideBorder = sideBorderColumn
                                             && (verticalArmDown ? y <= turnY : y >= turnY);
                            var horizontalColor = horizontalColors[horizontalIndex];
                            var sideColor = sideBorder
                                ? SelectBrighterVerticalBorderColor(
                                    sideColors,
                                    sideSourceX,
                                    sideSourceY)
                                : sideColors[sideIndex];
                            // Outside the clipped L, keep the cap's native
                            // stone grain but never copy an authored border
                            // row back into the stopped arm. The 2..4 window
                            // avoids the authored grout row at the lower edge
                            // of the Side crop.
                            var bodyX = Mathf.Clamp(horizontalSourceX, 2, 5);
                            var bodyY = Mathf.Clamp(sourceY, 2, 4);
                            // The stopped North cap arm is still part of the
                            // Side flank. Sampling the North edge columns here
                            // copied its dark seam into the cap body (visible as
                            // a band at the raised alcove corners). Keep the
                            // border phase from the native North crop, while
                            // filling the stopped body from the neutral Side
                            // interior pixels.
                            var bodyColor = sideColors[
                                bodyY * RuntimeCornerTexturePixels + bodyX];
                            cornerColors[y * RuntimeCornerTexturePixels + x] =
                                horizontalBorder && sideBorder
                                    ? SelectBrighterBorderColor(horizontalColor, sideColor)
                                    : horizontalBorder
                                        ? horizontalColor
                                        : sideBorder
                                            ? sideColor
                                            : bodyColor;
                        }
                        else
                        {
                            var isSideBand = sideSourceX < RuntimeCornerBandPixels;
                            var isHorizontalBand = sourceY < RuntimeCornerBandPixels;
                            cornerColors[y * RuntimeCornerTexturePixels + x] =
                                isSideBand && !isHorizontalBand
                                    ? sideColors[sideIndex]
                                    : isHorizontalBand
                                        ? horizontalColors[horizontalIndex]
                                        : role == CornerMiterRole.NorthCap
                                            ? horizontalColors[horizontalIndex]
                                            : interiorColor;
                        }
                    }

                cornerTexture = new Texture2D(
                    RuntimeCornerTexturePixels,
                    RuntimeCornerTexturePixels,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    name = $"RuntimeDungeonCorner_{role}_{cacheKey}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                cornerTexture.SetPixels32(cornerColors);
                cornerTexture.Apply(false, false);
                cornerMiterTextures.Add(cornerTexture);
                cornerMiterTextureCache[cacheKey] = cornerTexture;
            }

            var sprite = Sprite.Create(
                cornerTexture,
                new Rect(0f, 0f, RuntimeCornerTexturePixels, RuntimeCornerTexturePixels),
                new Vector2(.5f, .5f),
                sideSource.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
            sprite.name = $"RuntimeDungeonCornerSprite_{role}";
            sprite.hideFlags = HideFlags.DontSave;
            cornerMiterSprites.Add(sprite);
            return sprite;
        }

        private RectInt GetCornerSourceCrop(
            Sprite source,
            Vector2 worldPosition,
            Bounds sourceBounds,
            CornerMiterRole role,
            bool horizontalSource)
        {
            var textureRect = source.textureRect;
            var ppu = source.pixelsPerUnit;
            var maxX = Mathf.Max(1, Mathf.FloorToInt(textureRect.width));
            var maxY = Mathf.Max(1, Mathf.FloorToInt(textureRect.height));
            var width = Mathf.Min(RuntimeCornerTexturePixels, maxX);
            var height = Mathf.Min(RuntimeCornerTexturePixels, maxY);
            var minX = Mathf.FloorToInt(textureRect.x);
            var minY = Mathf.FloorToInt(textureRect.y);

            if (horizontalSource && maxX > width)
            {
                var phaseX = (worldPosition.x - sourceBounds.min.x) * ppu;
                // Keep the start in the source tile's coordinate frame. The
                // region reader wraps the eight-pixel window; clamping here
                // would turn a phase at 28px into 24px and visibly break the
                // border at a native 32px tile boundary.
                minX += Mathf.FloorToInt(Mathf.Repeat(phaseX, maxX));
            }

            if (role == CornerMiterRole.NorthCap)
                minY += maxY - height;
            else if (maxY > height)
            {
                var phaseY = (worldPosition.y - sourceBounds.min.y) * ppu;
                minY += Mathf.FloorToInt(Mathf.Repeat(phaseY, maxY));
            }

            minX = horizontalSource && maxX > width
                ? Mathf.Clamp(
                    minX,
                    Mathf.FloorToInt(textureRect.x),
                    Mathf.FloorToInt(textureRect.xMax) - 1)
                : Mathf.Clamp(
                    minX,
                    Mathf.FloorToInt(textureRect.x),
                    Mathf.FloorToInt(textureRect.xMax) - width);
            minY = maxY > height && role != CornerMiterRole.NorthCap
                ? Mathf.Clamp(
                    minY,
                    Mathf.FloorToInt(textureRect.y),
                    Mathf.FloorToInt(textureRect.yMax) - 1)
                : Mathf.Clamp(
                    minY,
                    Mathf.FloorToInt(textureRect.y),
                    Mathf.FloorToInt(textureRect.yMax) - height);
            return new RectInt(minX, minY, width, height);
        }

        private static RectInt ToTextureRectInt(Rect textureRect)
        {
            return new RectInt(
                Mathf.FloorToInt(textureRect.x),
                Mathf.FloorToInt(textureRect.y),
                Mathf.Max(1, Mathf.FloorToInt(textureRect.width)),
                Mathf.Max(1, Mathf.FloorToInt(textureRect.height)));
        }

        private Color32[] GetCornerSourceRegion(
            Texture source,
            RectInt sourceRect,
            RectInt textureRect)
        {
            PruneSharedCornerSourcePixels();
            if (!sharedCornerSourcePixels.TryGetValue(source, out var cached)
                || cached == null
                || cached.Pixels == null
                || cached.Width != source.width
                || cached.Height != source.height)
            {
                cached = ReadCornerSourcePixels(source);
                sharedCornerSourcePixels[source] = cached;
            }

            var result = new Color32[sourceRect.width * sourceRect.height];
            for (var y = 0; y < sourceRect.height; y++)
                for (var x = 0; x < sourceRect.width; x++)
                {
                    var sourceX = textureRect.x
                                  + Mathf.RoundToInt(Mathf.Repeat(
                                      sourceRect.x - textureRect.x + x,
                                      textureRect.width));
                    var sourceY = textureRect.y
                                  + Mathf.RoundToInt(Mathf.Repeat(
                                      sourceRect.y - textureRect.y + y,
                                      textureRect.height));
                    sourceX = Mathf.Clamp(sourceX, 0, cached.Width - 1);
                    sourceY = Mathf.Clamp(sourceY, 0, cached.Height - 1);
                    result[y * sourceRect.width + x] = cached.Pixels[sourceY * cached.Width + sourceX];
                }

            return result;
        }

        private static void PruneSharedCornerSourcePixels()
        {
            // Production uses the three native wall textures. Test fixtures
            // may create and destroy temporary textures, so remove dead keys
            // before the shared source cache can retain those pixel arrays.
            if (sharedCornerSourcePixels.Count <= 8)
                return;

            var deadKeys = new List<Texture>();
            foreach (var pair in sharedCornerSourcePixels)
                if (pair.Key == null)
                    deadKeys.Add(pair.Key);

            foreach (var key in deadKeys)
                sharedCornerSourcePixels.Remove(key);
        }

        private CornerSourcePixels ReadCornerSourcePixels(Texture source)
        {
            var renderTexture = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var capture = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                false)
            {
                hideFlags = HideFlags.DontSave
            };
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                capture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                capture.Apply(false, false);
                var pixels = capture.GetPixels32();
                DestroyRuntimeCornerTexture(capture);
                return new CornerSourcePixels
                {
                    Width = source.width,
                    Height = source.height,
                    Pixels = pixels
                };
            }
            catch
            {
                DestroyRuntimeCornerTexture(capture);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private void DestroyRuntimeCornerTexture(Texture texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
        }

        private SpriteRenderer CreateCornerMiterRenderer(
            string objectName,
            Sprite sprite,
            Transform parent,
            Vector3 localPosition,
            SpriteRenderer sourceRenderer,
            int sortingOrder,
            bool sortAtRoot)
        {
            if (sprite == null)
                return null;

            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            // Direction is encoded by the runtime mesh vertices. Keep the
            // renderer scale positive so the source UV phase is not mirrored.
            child.transform.localScale = Vector3.one;
            if (sortAtRoot)
            {
                var sortingGroup = child.AddComponent<SortingGroup>();
                sortingGroup.sortingLayerID = sourceRenderer.sortingLayerID;
                sortingGroup.sortingOrder = sortingOrder;
                sortingGroup.sortAtRoot = true;
            }

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sortAtRoot ? 0 : sortingOrder;
            renderer.color = sourceRenderer.color;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.maskInteraction = sourceRenderer.maskInteraction;
            renderer.forceRenderingOff = sourceRenderer.forceRenderingOff;
            return renderer;
        }

        private void DestroyRuntimeCornerSprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            if (Application.isPlaying)
                Destroy(sprite);
            else
                DestroyImmediate(sprite);
        }

        private static float GetVerticalCellLength(Tilemap floorTilemap, Vector2Int cell)
        {
            var firstCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(cell.x, cell.y, 0));
            var secondCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(cell.x, cell.y + 1, 0));
            return Mathf.Abs(secondCenter.y - firstCenter.y);
        }

        private bool HasNativeSprite(Sprite sprite, float width, float height)
        {
            return sprite != null
                   && Mathf.Abs(sprite.rect.width - width) < .001f
                   && Mathf.Abs(sprite.rect.height - height) < .001f
                   && Mathf.Abs(sprite.pixelsPerUnit - 32f) < .001f;
        }

        private void ConfigureUpperVerticalCornerOverlay(
            Vector2 firstCenter,
            Vector2 secondCenter,
            Vector2 midpoint,
            Vector2 tangent,
            float wallThickness,
            SpriteRenderer sourceRenderer)
        {
            if (verticalWallSprite == null || wallThickness <= 0f)
                return;

            if (upperVerticalCornerRenderer == null)
            {
                var child = new GameObject("UpperVerticalCornerOverlay");
                child.transform.SetParent(transform, false);
                upperVerticalCornerRenderer = child.AddComponent<SpriteRenderer>();
            }

            // The endpoint is selected by world Y so the overlay remains the
            // upper cap even when the edge's cell order is reversed.
            var halfLength = Vector2.Distance(firstCenter, secondCenter) * .5f;
            var firstEndpoint = midpoint - tangent * halfLength;
            var secondEndpoint = midpoint + tangent * halfLength;
            var upperEndpoint = firstEndpoint.y >= secondEndpoint.y
                ? firstEndpoint
                : secondEndpoint;

            var childTransform = upperVerticalCornerRenderer.transform;
            childTransform.localPosition = transform.InverseTransformPoint(upperEndpoint);
            childTransform.localRotation = Quaternion.identity;
            childTransform.localScale = Vector3.one;

            upperVerticalCornerRenderer.sprite = verticalWallSprite;
            upperVerticalCornerRenderer.drawMode = SpriteDrawMode.Tiled;
            upperVerticalCornerRenderer.size = new Vector2(wallThickness, wallThickness);
            upperVerticalCornerRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            upperVerticalCornerRenderer.sortingOrder = UpperVerticalCornerSortingOrder;
            upperVerticalCornerRenderer.color = sourceRenderer.color;
            upperVerticalCornerRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            upperVerticalCornerRenderer.gameObject.SetActive(true);
        }

        private void SetUpperVerticalCornerOverlayActive(bool active)
        {
            if (upperVerticalCornerRenderer != null)
                upperVerticalCornerRenderer.gameObject.SetActive(active);
        }

        private void ConfigureNorthSideCapOverlay(
            Vector2 sideEndpoint,
            float wallThickness,
            SpriteRenderer sourceRenderer)
        {
            // A native side cap must use the native 8x32 side art. If a legacy
            // variant or an incomplete prefab reaches this path, leave its
            // existing visual untouched instead of stretching the old sprite.
            if (wallThickness <= 0f || !HasNativeSprite(sideWallSprite, 8f, 32f))
                return;

            SpriteRenderer overlay = null;
            foreach (var candidate in northSideCapOverlays)
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    overlay = candidate;
                    break;
                }

            if (overlay == null)
            {
                var child = new GameObject("NorthSideCapOverlay");
                child.transform.SetParent(transform, false);
                overlay = child.AddComponent<SpriteRenderer>();
                northSideCapOverlays.Add(overlay);
            }

            var childTransform = overlay.transform;
            var capCenter = sideEndpoint + Vector2.up * (NativeNorthWallFaceDepthWorld * .5f);
            childTransform.localPosition = transform.InverseTransformPoint(capCenter);
            childTransform.localRotation = Quaternion.identity;
            childTransform.localScale = Vector3.one;

            overlay.sprite = HasNativeSprite(sideWallSprite, 8f, 32f)
                ? sideWallSprite
                : verticalWallSprite;
            overlay.drawMode = SpriteDrawMode.Tiled;
            overlay.size = new Vector2(wallThickness, NativeNorthWallFaceDepthWorld);
            overlay.sortingLayerID = sourceRenderer.sortingLayerID;
            overlay.sortingOrder = UpperVerticalCornerSortingOrder;
            overlay.color = sourceRenderer.color;
            overlay.sharedMaterial = sourceRenderer.sharedMaterial;
            overlay.gameObject.SetActive(true);
        }

        private void SetNorthSideCapOverlaysActive(bool active)
        {
            foreach (var overlay in northSideCapOverlays)
                if (overlay != null)
                    overlay.gameObject.SetActive(active);
        }

        private void ConfigureSouthSideCapOverlay(
            Vector2 sideEndpoint,
            float wallThickness,
            SpriteRenderer sourceRenderer,
            bool useSouthFaceSprite,
            float southVisualOffsetWorld)
        {
            // South-only corners use the shared 8x8 side patch. A mixed
            // North/South junction reuses the same bounds with the dark South
            // face sprite so both visual roles remain visible at one endpoint.
            if (wallThickness <= 0f
                || !HasNativeSprite(sideWallSprite, 8f, 32f)
                || (useSouthFaceSprite && !HasNativeSprite(southWallSprite, 32f, 8f)))
                return;

            SpriteRenderer overlay = null;
            foreach (var candidate in southSideCapOverlays)
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    overlay = candidate;
                    break;
                }

            if (overlay == null)
            {
                var child = new GameObject("SouthSideCapOverlay");
                child.transform.SetParent(transform, false);
                overlay = child.AddComponent<SpriteRenderer>();
                southSideCapOverlays.Add(overlay);
            }

            overlay.gameObject.name = useSouthFaceSprite
                ? "SouthSideFaceSourceOverlay"
                : "SouthSideCapOverlay";

            var childTransform = overlay.transform;
            var southPatchCenter = sideEndpoint
                + Vector2.up * southVisualOffsetWorld
                + Vector2.down * (NativeWallDepthWorld * .5f);
            childTransform.localPosition = transform.InverseTransformPoint(southPatchCenter);
            childTransform.localRotation = Quaternion.identity;
            childTransform.localScale = Vector3.one;

            overlay.sprite = useSouthFaceSprite ? southWallSprite : sideWallSprite;
            overlay.drawMode = SpriteDrawMode.Tiled;
            overlay.size = new Vector2(wallThickness, NativeWallDepthWorld);
            overlay.sortingLayerID = sourceRenderer.sortingLayerID;
            overlay.sortingOrder = UpperVerticalCornerSortingOrder;
            overlay.color = sourceRenderer.color;
            overlay.sharedMaterial = sourceRenderer.sharedMaterial;
            overlay.gameObject.SetActive(true);
        }

        private void ConfigureSouthOcclusionMask(
            Vector2 logicalBoundary,
            float visualLength,
            float visualDepth,
            float southVisualOffsetWorld,
            SpriteRenderer sourceRenderer)
        {
            var gap = southVisualOffsetWorld - visualDepth;
            if (gap <= .0001f || sourceRenderer == null)
            {
                SetSouthOcclusionMaskActive(false);
                return;
            }

            if (southOcclusionRenderer == null)
            {
                var child = new GameObject("SouthWallOcclusionMask");
                child.transform.SetParent(transform, false);
                southOcclusionRenderer = child.AddComponent<SpriteRenderer>();
            }

            if (southOcclusionSprite == null)
            {
                southOcclusionSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(.5f, .5f),
                    1f,
                    0,
                    SpriteMeshType.FullRect,
                    Vector4.zero);
                southOcclusionSprite.hideFlags = HideFlags.DontSave;
            }

            var maskTransform = southOcclusionRenderer.transform;
            var maskCenter = logicalBoundary + Vector2.up * (gap * .5f);
            maskTransform.localPosition = transform.InverseTransformPoint(maskCenter);
            maskTransform.localRotation = Quaternion.identity;
            maskTransform.localScale = Vector3.one;

            southOcclusionRenderer.sprite = southOcclusionSprite;
            southOcclusionRenderer.drawMode = SpriteDrawMode.Tiled;
            southOcclusionRenderer.size = new Vector2(visualLength, gap);
            southOcclusionRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            southOcclusionRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            southOcclusionRenderer.color = Color.black;
            southOcclusionRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            southOcclusionRenderer.gameObject.SetActive(true);
        }

        private void SetSouthOcclusionMaskActive(bool active)
        {
            if (southOcclusionRenderer != null)
                southOcclusionRenderer.gameObject.SetActive(active);
        }

        private void SetSouthSideCapOverlaysActive(bool active)
        {
            foreach (var overlay in southSideCapOverlays)
                if (overlay != null)
                    overlay.gameObject.SetActive(active);
        }
    }
}
