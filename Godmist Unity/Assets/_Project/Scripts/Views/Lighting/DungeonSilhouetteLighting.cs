using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Lighting
{
    // Scene-owned renderer. It never changes the game camera target or the UI camera.
    [DefaultExecutionOrder(11000)]
    public sealed partial class DungeonSilhouetteLighting : MonoBehaviour
    {
        [Serializable] public struct ContactProfile
        {
            public Sprite sprite;
            public Vector2 footLocal;
            public float slope, offset;
        }
        [SerializeField] private Camera worldCamera;
        [SerializeField] private TilemapRenderer floorReceiver;
        [SerializeField] private Shader maskShader, floorShader, bodyShader;
        [SerializeField] private ContactProfile[] contacts = Array.Empty<ContactProfile>();
        [SerializeField] private Shader torchShader;
        [SerializeField] private Shader wallShader;
        [SerializeField] private Shader contactShader;
        [SerializeField] private Sprite pillarContactSprite;
        private DungeonContactShadows contactShadows;
        private readonly List<SpriteRenderer> localWallRenderers = new();
        private readonly List<SpriteRenderer> localGroundRenderers = new();
        public int ContactShadowQuadCount => contactShadows?.QuadCount ?? 0;
        public int WallContactShadowQuadCount => contactShadows?.WallQuadCount ?? 0;
        [SerializeField] private Sprite torchSprite;
        [SerializeField] private Texture2D torchFlameMask;
        [SerializeField, Min(1f)] private float torchRadius = 5f;
        [SerializeField] private Color torchColor = new(1.45f, .55f, .14f, 1f);
        public const float LightingUpdatesPerSecond = 5f;
        public static float SampleTorchTime(float time) =>
            Mathf.Floor(Mathf.Max(0, time) * LightingUpdatesPerSecond) / LightingUpdatesPerSecond;
        private static readonly Unity.Profiling.ProfilerMarker RefreshSample = new("Godmist.Lighting.Refresh");
        private static readonly Unity.Profiling.ProfilerMarker SourceSelectionSample = new("Godmist.Lighting.SourceSelection");
        private static readonly Unity.Profiling.ProfilerMarker SourceFixturesSample = new("Godmist.Lighting.SourceFixtures");
        private static readonly Unity.Profiling.ProfilerMarker TorchPresentationSample = new("Godmist.Lighting.TorchPresentation");
        private static readonly Unity.Profiling.ProfilerMarker SourceCandidatesSample = new("Godmist.Lighting.SourceCandidates");
        private static readonly Unity.Profiling.ProfilerMarker ArenaSourcesSample = new("Godmist.Lighting.ArenaSources");
        private static readonly Unity.Profiling.ProfilerMarker MeshLookupSample = new("Godmist.Lighting.MeshLookup");
        private static readonly Unity.Profiling.ProfilerMarker MeshBuildSample = new("Godmist.Lighting.MeshBuild");
        private static readonly Unity.Profiling.ProfilerMarker ReceiverInputsSample = new("Godmist.Lighting.ReceiverInputs");
        private static readonly Unity.Profiling.ProfilerMarker ReceiverWallSelectionSample = new("Godmist.Lighting.ReceiverWallSelection");
        private static readonly Unity.Profiling.ProfilerMarker ReceiverWallsSample = new("Godmist.Lighting.ReceiverWalls");
        private static readonly Unity.Profiling.ProfilerMarker ReceiverGroundSelectionSample = new("Godmist.Lighting.ReceiverGroundSelection");
        private static readonly Unity.Profiling.ProfilerMarker ReceiverGroundSample = new("Godmist.Lighting.ReceiverGround");
        private static readonly Unity.Profiling.ProfilerMarker ReceiverBlockReadSample = new("Godmist.Lighting.ReceiverBlockRead");
        private static readonly Unity.Profiling.ProfilerMarker CasterCollectionSample = new("Godmist.Lighting.CasterCollection");
        private static readonly Unity.Profiling.ProfilerMarker ShadowMasksSample = new("Godmist.Lighting.ShadowMasks");
        private static readonly Unity.Profiling.ProfilerMarker ShadowInputsSample = new("Godmist.Lighting.ShadowInputs");
        private static readonly Unity.Profiling.ProfilerMarker StaticShadowsSample = new("Godmist.Lighting.StaticShadows");
        private static readonly Unity.Profiling.ProfilerMarker ShadowPrepareSample = new("Godmist.Lighting.PrepareDraws");
        private static readonly Unity.Profiling.ProfilerMarker ShadowPropertiesSample = new("Godmist.Lighting.PrepareProperties");
        private static readonly Unity.Profiling.ProfilerMarker ShadowStateSample = new("Godmist.Lighting.ReadCasterState");
        private static readonly Unity.Profiling.ProfilerMarker CasterPropertiesSample = new("Godmist.Lighting.CasterProperties");
        private static readonly Unity.Profiling.ProfilerMarker ShadowRecordSample = new("Godmist.Lighting.RecordDraws");
        private static readonly Unity.Profiling.ProfilerMarker ShadowSubmitSample = new("Godmist.Lighting.SubmitDraws");
        private const float LightingCoverageMargin = 2f;
        private int lastLightingFrame = int.MinValue;
        private int lastLightingMapRevision;
        private const int MaxLights = 8;
        private DungeonFloorMapView terrain;
        private DungeonFloorMap torchMap;
        private readonly List<Torch> torches = new();
        private readonly List<Torch> activeTorches = new();
        private readonly Vector4[] lightData = new Vector4[MaxLights];
        private readonly Vector4[] lightColors = new Vector4[MaxLights];
        private readonly Vector4[] shadowLights = new Vector4[MaxLights];
        private bool shadowCacheReady;
        private bool staticShadowCacheReady;
        private int staticShadowCasterCount, pendingStaticShadowCasterCount;
        private int dirtyShadowLights, dirtyStaticShadowLights;
        private bool shadowRemovalsObserved;
        private Rect shadowBounds;
        private int shadowLightCount, shadowCasterCount;
        private float shadowLightHeight;
        private readonly Dictionary<SpriteRenderer, Material> wallMaterials = new();
        private readonly Dictionary<SpriteRenderer, (Material material, int layer, int order, bool groundOrder)> presentationMaterials = new();
        private readonly Dictionary<SpriteRenderer, MaterialPropertyBlock> presentationPropertyBlocks = new();
        private MaterialPropertyBlock receiverWorkingProperties;
        private bool receiverSortingBatch;
        private int receiverFloorLayer, receiverFloorOrder;
        private readonly Dictionary<SpriteRenderer, (bool Floor, bool Outline)> preparedReceivers = new();
        private readonly Dictionary<Sprite, (Texture Texture, Vector4 Texel, bool HasTexel)> receiverSpriteInputs = new();
        private readonly Dictionary<SpriteRenderer, Bounds> preparedReceiverBounds = new();
        private bool receiverBoundsBatch;
        private Func<SpriteRenderer, Bounds> preparedBoundsReader;
        public int ReceiverBoundsReadCount { get; private set; }
        public int ReceiverPreparationCount { get; private set; }
        public int ReceiverSpriteInputReadCount { get; private set; }
        private Material flameMaterial;
        private readonly Dictionary<Material, Material> wallVariants = new();
        private readonly Vector4[] wallLightData = new Vector4[MaxLights];
        private sealed class Torch { public Vector2 position; public DungeonSurfacePosition cell; public SpriteRenderer view; public float phase; public bool carried; public float radiusScale = 1f; public SpriteRenderer[] companions; public SortingGroup mountGroup; public SpriteRenderer mountSprite; public bool forestFixture, arenaFire; public float fogBrightness = -1; public Color[] companionBaseColors; }
        public int TorchCount => torches.Count;
        public int ActiveTorchCount => activeTorches.Count;
        public Vector2 FirstTorchPosition => torches.Count > 0 ? torches[0].position : Vector2.zero;
        public static float TorchIntensity(float time, float phase)
            => .95f + .10f * Mathf.Sin(time * (2.5f * Mathf.PI * 2) + phase)
                + .008f * Mathf.Sin(time * (6.7f * Mathf.PI * 2) + phase * 1.7f);
        public static float ForestTorchIntensity(float time, float phase)
            => .95f + .025f * Mathf.Sin(time * (.65f * Mathf.PI * 2) + phase)
                + .006f * Mathf.Sin(time * (1.1f * Mathf.PI * 2) + phase * 1.7f);
        [SerializeField, Min(.1f)] private float lightHeight = 3f;
        [SerializeField, Range(.1f, 1f)] private float ambient = .22f;
        private float forestAmbient = .34f;
        private float forestShadowDetail = .3f;
        // Forest art already contains deep painted shadows. Give it diffuse
        // fill before local torches, rather than brightening textures or fog.
        private float EffectiveAmbient => terrain != null && terrain.UsesVegetationPresentation
            ? forestAmbient : ambient;
        private ExplorationPartyController party;
        private DungeonEncounterSpawner encounters;
        private DungeonObjectSpawner objects;
        private readonly Dictionary<SpriteRenderer, Entry> entries = new();
        private readonly Dictionary<DungeonObjectView, SpriteRenderer[]> obstacles = new();
        private readonly HashSet<DungeonObjectView> additionalObjectViews = new();
        private readonly Dictionary<DungeonObjectView, SpriteRenderer[]> additionalObjectRenderers = new();
        private readonly HashSet<DungeonObjectView> groundObjectViews = new();
        private readonly Dictionary<DungeonObjectView, SpriteRenderer[]> groundObjectRenderers = new();
        private readonly List<SpriteRenderer> groundPresentations = new();
        // Low, floor-standing props may use the floor light response without
        // entering the tall silhouette caster pass. Keep their small contact
        // footprint explicit so a low prop still sits on the tile.
        private readonly List<SpriteRenderer> groundContactPresentations = new();
        private readonly Dictionary<Sprite, Mesh> meshes = new();
        private readonly Dictionary<SilhouetteVolumeMesh.GeometryKey, Mesh> sharedMeshes = new();
        private readonly Dictionary<Sprite, Vector4> spriteUvBounds = new();
        private readonly List<(Entry Entry, int Lights)> preparedShadowCasters = new();
        private static readonly int ShadowVpId = Shader.PropertyToID("_ShadowVP");
        private static readonly int ShadowFootId = Shader.PropertyToID("_FootLocal");
        private static readonly int ShadowScaleId = Shader.PropertyToID("_Scale");
        private static readonly int ShadowHeightId = Shader.PropertyToID("_HeightBasis");
        private static readonly int ShadowContactId = Shader.PropertyToID("_ContactLine");
        private static readonly int ShadowVolumeId = Shader.PropertyToID("_VolumeHalfDepth");
        private static readonly int ShadowDepthId = Shader.PropertyToID("_DepthPixels");
        private static readonly int ShadowUvId = Shader.PropertyToID("_UVBounds");
        private static readonly int ShadowAnchorId = Shader.PropertyToID("_Anchor");
        private static readonly int ReceiverTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int ForestSurfaceId = Shader.PropertyToID("_ForestSurfaceEnabled");
        private static readonly int ForestPatchId = Shader.PropertyToID("_ForestPatchBounds");
        private static readonly int ForestFogLitterId = Shader.PropertyToID("_ForestFogLitter");
        private static readonly int ForestReadabilityId = Shader.PropertyToID("_ForestPropReadability");
        private static readonly int ForestUvId = Shader.PropertyToID("_ForestPropUV");
        private static readonly int ForestTexelId = Shader.PropertyToID("_ForestPropTexel");
        private static readonly int FootWorldId = Shader.PropertyToID("_FootWorld");
        private static readonly int IsActorId = Shader.PropertyToID("_IsActor");
        private static readonly int TorchCountId = Shader.PropertyToID("_TorchCount");
        private static readonly int TorchesId = Shader.PropertyToID("_Torches");
        private static readonly int TorchColorsId = Shader.PropertyToID("_TorchColors");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int ForestShadowDetailId = Shader.PropertyToID("_ForestShadowDetail");
        private static readonly int ShadowMasksId = Shader.PropertyToID("_ShadowMasks");
        private static readonly int GroundBoundsId = Shader.PropertyToID("_GroundBounds");
        public int ReceiverPropertyBlockUploadCount { get; private set; }
        private readonly List<SpriteRenderer> expired = new();
        private Material projection, floorMaterial, bodyMaterial, originalFloor;
        private RenderTexture mask, staticMask;
        private CommandBuffer commands;
        private MaterialPropertyBlock properties;
        private Rect bounds;
        private int revision;
        private Rect cameraGroundBounds;
        public int LastCasterCount { get; private set; }
        public int LastShadowDrawCount { get; private set; }
        public int ShadowMaskRebuildCount { get; private set; }
        public int StaticShadowMaskRebuildCount { get; private set; }
        public int LastShadowSliceCount { get; private set; }
        public int LastStaticShadowSliceCount { get; private set; }
        public int ShadowGeometryPreparationCount { get; private set; }
        public int ShadowPropertyPreparationCount { get; private set; }
        public int CasterPropertyBlockUploadCount { get; private set; }
        public int ShadowMeshBuildCount { get; private set; }
        public int TorchSortingQueryCount { get; private set; }
        public RenderTexture ShadowMask => mask;
        public Vector2 LightPosition { get; private set; }
        private sealed class Entry
        {
            public Material original;
            public MaterialPropertyBlock properties;
            public bool actor;
            public float projectedElevation;
            public int seen;
            public bool shadowStateReady;
            public ShadowCasterState shadowState;
            public int shadowInfluence;
            public MaterialPropertyBlock drawProperties;
            public bool drawStateReady;
            public ShadowCasterState drawState;
            public Matrix4x4 drawVp;
            public Mesh drawMesh;
            public Texture drawTexture;
            public Vector2 drawFoot;
        }

        private readonly struct ShadowCasterState : IEquatable<ShadowCasterState>
        {
            private readonly Sprite sprite;
            private readonly Vector3 position, scale;
            private readonly Bounds worldBounds;
            private readonly bool flipX, flipY, actor;
            private readonly float elevation;
            private readonly ContactProfile contact;
            public bool Actor => actor;
            public Vector3 Position => position;
            public Bounds WorldBounds => worldBounds;
            public ContactProfile Contact => contact;
            public Vector2 SignedScale => new(scale.x * (flipX ? -1 : 1), scale.y * (flipY ? -1 : 1));

            public ShadowCasterState(SpriteRenderer renderer, Entry entry, ContactProfile profile, Bounds bounds)
            {
                sprite = renderer.sprite;
                var transform = renderer.transform;
                position = transform.position; scale = transform.lossyScale;
                worldBounds = bounds;
                flipX = renderer.flipX; flipY = renderer.flipY; actor = entry.actor;
                elevation = entry.projectedElevation; contact = profile;
            }

            public bool Equals(ShadowCasterState other) => sprite == other.sprite
                && position.Equals(other.position) && scale.Equals(other.scale) && worldBounds.Equals(other.worldBounds)
                && flipX == other.flipX && flipY == other.flipY && actor == other.actor && elevation == other.elevation
                && contact.footLocal.Equals(other.contact.footLocal) && contact.slope == other.contact.slope
                && contact.offset == other.contact.offset;

            // Translation changes the draw anchor, not the sprite's local
            // volume/UV/contact parameters. Size still catches live bounds
            // changes, while scale/flip/contact/role retain their own inputs.
            public bool SameDrawProperties(ShadowCasterState other) => sprite == other.sprite
                && scale.Equals(other.scale) && worldBounds.size.Equals(other.worldBounds.size)
                && flipX == other.flipX && flipY == other.flipY && actor == other.actor && elevation == other.elevation
                && contact.footLocal.Equals(other.contact.footLocal) && contact.slope == other.contact.slope
                && contact.offset == other.contact.offset;
        }

        public void Initialize(ExplorationPartyController partyController,
            DungeonEncounterSpawner encounterSpawner, DungeonObjectSpawner objectSpawner, DungeonFloorMapView mapView)
        {
            party = partyController;
            encounters = encounterSpawner;
            objects = objectSpawner;
            terrain = mapView;
            terrain?.ConfigurePresentationCamera(worldCamera, 2 * torchRadius * 1.3f + LightingCoverageMargin);
            lastLightingFrame = int.MinValue;
            shadowCacheReady = false;
            staticShadowCacheReady = false;
            dirtyShadowLights = dirtyStaticShadowLights = 0;
            shadowRemovalsObserved = false;
            additionalObjectViews.Clear();
            additionalObjectRenderers.Clear();
            groundObjectViews.Clear();
            groundObjectRenderers.Clear();
            groundContactPresentations.Clear();
            if (terrain != null && terrain.UsesVegetationPresentation && objects != null)
                foreach (var view in objects.SpawnedObjects)
                {
                    if (view is DungeonSpikeTrapView || view is DungeonConditionView || view is DungeonLeverView
                        || view is DungeonObstacleView obstacle && obstacle.IsGroundPresentation)
                        RegisterGroundObjectView(view);
                    if (view is DungeonLeverView)
                        foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
                            RegisterGroundContactPresentation(renderer);
                }
        }

        /// <summary>
        /// Adds a scene-owned object view to the same body-material and
        /// silhouette pass used by regular dungeon obstacle presentations.
        /// Runtime-created vertical Catacombs audit views use this path so
        /// they receive the normal body silhouette pass. Flat audit props
        /// register through RegisterGroundObjectView instead.
        /// </summary>
        public void RegisterAdditionalObjectView(DungeonObjectView view)
        {
            if (view != null && additionalObjectViews.Add(view)) lastLightingFrame = int.MinValue;
        }

        /// <summary>
        /// Adds a runtime object whose art sits on the floor plane. These
        /// renderers receive the floor light response without entering the
        /// vertical silhouette caster pass, which would make flat traps and
        /// rubble appear to float above the dungeon floor.
        /// </summary>
        public void RegisterGroundObjectView(DungeonObjectView view)
        {
            if (view != null && groundObjectViews.Add(view)) lastLightingFrame = int.MinValue;
        }

        public void RegisterGroundPresentation(SpriteRenderer renderer)
        {
            if (renderer != null && !groundPresentations.Contains(renderer))
            { groundPresentations.Add(renderer); lastLightingFrame = int.MinValue; }
        }

        public void RegisterGroundContactPresentation(SpriteRenderer renderer)
        {
            if (renderer != null && !groundContactPresentations.Contains(renderer))
            { groundContactPresentations.Add(renderer); lastLightingFrame = int.MinValue; }
        }

        private bool ShouldRefreshLighting(float time, Rect viewport, int mapRevision)
        {
            int frame = Mathf.FloorToInt(Mathf.Max(0, time) * LightingUpdatesPerSecond);
            // Shadow sampling uses world coordinates, not camera coordinates.
            // Retain both the mask and its mapping while the camera stays inside
            // the prepared region. A teleport/large zoom must not expose an
            // unprepared strip of terrain until the next scheduled refresh.
            bool covered = viewport.xMin >= cameraGroundBounds.xMin && viewport.xMax <= cameraGroundBounds.xMax
                && viewport.yMin >= cameraGroundBounds.yMin && viewport.yMax <= cameraGroundBounds.yMax;
            bool scheduled = frame != lastLightingFrame;
            if (!scheduled && mapRevision == lastLightingMapRevision && covered) return false;
            lastLightingFrame = frame;
            lastLightingMapRevision = mapRevision;
            // Flicker and actors still update every tick, but a small camera pan
            // must not invalidate static masks/contact meshes at the same rate.
            // Refill coverage near its edge on a scheduled tick, retaining a
            // reserve for ordinary camera movement before the next tick.
            float reserve = LightingCoverageMargin * .5f;
            bool nearEdge = viewport.xMin < cameraGroundBounds.xMin + reserve
                || viewport.xMax > cameraGroundBounds.xMax - reserve
                || viewport.yMin < cameraGroundBounds.yMin + reserve
                || viewport.yMax > cameraGroundBounds.yMax - reserve;
            if (!covered || scheduled && nearEdge)
                cameraGroundBounds = Rect.MinMaxRect(viewport.xMin - LightingCoverageMargin,
                    viewport.yMin - LightingCoverageMargin, viewport.xMax + LightingCoverageMargin,
                    viewport.yMax + LightingCoverageMargin);
            return true;
        }

        private static readonly Unity.Profiling.ProfilerMarker LightingLateSample = new("Godmist.Lighting.Late");
        private void LateUpdate()
        {
            using var lateSample = LightingLateSample.Auto();
            RefreshLighting(Time.time);
            RefreshForestPartyReveal();
            AdvanceForestCanopyFade(Time.deltaTime);
        }

        private void RefreshLighting(float time)
        {
            // All receiver/contact/caster work is synchronous. Share its live
            // bounds only for this call; movement and other callers never read
            // a snapshot retained from an earlier refresh.
            receiverBoundsBatch = true;
            preparedReceiverBounds.Clear();
            try { RefreshLightingCore(time); }
            finally { receiverBoundsBatch = false; preparedReceiverBounds.Clear(); }
        }

        private void RefreshLightingCore(float time)
        {
            if (party == null || terrain == null || worldCamera == null || floorReceiver == null) return;
            terrain.EnsureCameraPresentation();
            if (projection == null && !CreateResources()) return;
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * worldCamera.aspect;
            var cameraPosition = worldCamera.transform.position;
            var viewport = new Rect(cameraPosition.x-halfWidth, cameraPosition.y-halfHeight, halfWidth*2, halfHeight*2);
            if (!ShouldRefreshLighting(time, viewport, terrain.PresentationRevision))
            {
                AdvanceForestLightIntensity(time);
                return;
            }
            using var refreshSample = RefreshSample.Auto();
            using var cameraQueries = terrain.BeginPresentationCameraQueries();
            using (SourceSelectionSample.Auto())
                RefreshLightSources(cameraPosition, terrain.UsesVegetationPresentation ? time : SampleTorchTime(time));
            try
            {
                using (ReceiverInputsSample.Auto())
                {
                    receiverSortingBatch = true;
                    preparedReceivers.Clear(); receiverSpriteInputs.Clear();
                    receiverFloorLayer = floorReceiver.sortingLayerID;
                    receiverFloorOrder = floorReceiver.sortingOrder;
                    try
                    {
                        IEnumerable<SpriteRenderer> walls = terrain.LightingWallRenderers;
                        using (ReceiverWallSelectionSample.Auto()) if (terrain.UsesVegetationPresentation)
                        {
                            terrain.CollectLightingWallRenderers(cameraGroundBounds, localWallRenderers);
                            walls = localWallRenderers;
                        }
                        using (ReceiverWallsSample.Auto()) foreach (var wall in walls)
                        {
                            if (wall == null || wall.forceRenderingOff) continue;
                            var wallColor = wall.color;
                            if (wallColor.r + wallColor.g + wallColor.b < .001f
                                || !IntersectsCamera(ReadPreparedReceiverBounds(wall), 0)) continue;
                            if (!wallMaterials.ContainsKey(wall))
                            {
                                var original = wall.sharedMaterial;
                                if (original == null) continue;
                                wallMaterials.Add(wall, original);
                                if (!wallVariants.TryGetValue(original, out var variant))
                                {
                                    variant = new Material(original) { shader = wallShader, name = original.name + " (torch wall)" };
                                    wallVariants.Add(original, variant);
                                }
                                // Native sprite vertex/instance path, original tint/alpha and property blocks.
                                wall.sharedMaterial = variant;
                            }
                            if (terrain.UsesVegetationPresentation)
                            {
                                wall.GetPropertyBlock(properties);
                                bool changed = ConfigureForestReadability(wall, properties, terrain.IsForestReadableProp(wall));
                                changed |= ConfigureCanopyReveal(wall, properties);
                                if (changed)
                                { wall.SetPropertyBlock(properties); ReceiverPropertyBlockUploadCount++; }
                            }
                        }
                        IEnumerable<SpriteRenderer> groundRenderers = terrain.LightingPresentationRenderers;
                        using (ReceiverGroundSelectionSample.Auto()) if (terrain.UsesVegetationPresentation)
                        {
                            terrain.CollectLightingGroundRenderers(cameraGroundBounds, localGroundRenderers);
                            groundRenderers = localGroundRenderers;
                        }
                        using (ReceiverGroundSample.Auto())
                        {
                            foreach (var renderer in groundRenderers)
                            {
                                var sortOnFloor = terrain.IsGroundLightingPresentation(renderer)
                                    && !terrain.PreservesGroundPresentationSorting(renderer);
                                ApplyGroundLighting(
                                    renderer,
                                    sortOnFloor,
                                    sortOnFloor && (terrain.ReservesGroundOutlineBand(renderer) || terrain.IsForestReadableProp(renderer)));
                            }
                            foreach (var renderer in groundPresentations) ApplyGroundLighting(renderer, true);
                            foreach (var view in groundObjectViews)
                                if (view != null && view.IsLightingPresentationVisible && view.gameObject.activeInHierarchy)
                                {
                                    if (!groundObjectRenderers.TryGetValue(view, out var renderers))
                                        groundObjectRenderers.Add(
                                            view,
                                            renderers = view.GetComponentsInChildren<SpriteRenderer>(true));
                                    foreach (var renderer in renderers) ApplyGroundLighting(renderer);
                                }
                        }
                    }
                    finally { receiverSortingBatch = false; preparedReceivers.Clear(); receiverSpriteInputs.Clear(); }
                }
                if (contactShadows != null) contactShadows.BoundsSnapshot = preparedBoundsReader ??= ReadPreparedReceiverBounds;
                contactShadows?.Rebuild(terrain, floorReceiver, cameraGroundBounds,
                    objects?.SpawnedObjects, groundPresentations, pillarContactSprite,
                    groundContactPresentations);
            }
            finally
            {
                if (contactShadows != null) contactShadows.BoundsSnapshot = null;
            }
            using (CasterCollectionSample.Auto())
            {
                revision++;
                foreach (var renderer in terrain.ActorLightingPresentationRenderers)
                    Touch(renderer, true);
                foreach (var view in party.ActiveMemberViews)
                    if (view != null && view.IsVisibleForInspection) Touch(view.BodyRenderer, true);
                if (encounters != null)
                    for (int i = 0; i < encounters.EnemyViews.Count; i++)
                    {
                        var view = encounters.EnemyViews[i];
                        if (view != null && view.IsVisibleForInspection) Touch(view.BodyRenderer, true);
                    }
                if (objects != null) for (int i = 0; i < objects.SpawnedObjects.Count; i++)
                {
                    var view = objects.SpawnedObjects[i];
                    if (!groundObjectViews.Contains(view)
                        && (view is DungeonObstacleView || view is DungeonChestView || view is DungeonDoorView)
                        && view.IsLightingPresentationVisible && view.gameObject.activeInHierarchy)
                    {
                        if (!obstacles.TryGetValue(view, out var renderers))
                            obstacles.Add(view, renderers = view.GetComponentsInChildren<SpriteRenderer>(true));
                        foreach (var renderer in renderers) Touch(renderer, false,
                            view is DungeonDoorView door ? door.ProjectedVisualElevation : 0);
                    }
                }
                foreach (var view in additionalObjectViews)
                    if (view != null && view.IsLightingPresentationVisible && view.gameObject.activeInHierarchy)
                    {
                        if (!additionalObjectRenderers.TryGetValue(view, out var renderers))
                            additionalObjectRenderers.Add(
                                view,
                                renderers = view.GetComponentsInChildren<SpriteRenderer>(true));
                        foreach (var renderer in renderers)
                            Touch(renderer, false);
                    }

            }
            // Fixed allocation; texels stay on a world-aligned grid while panning.
            bounds = BuildShadowBounds(mask.width);
            float span = bounds.width;
            float texel = span / mask.width;
            PruneCasters();
            if (NeedsShadowMaskRebuild(bounds)) RebuildShadowMasks(texel);
            // Even a room without a visible torch uses the darker ambient on actors.
            foreach (var pair in entries) if (IsCasterVisible(pair.Key))
            {
                var sr = pair.Key;
                if (sr.sharedMaterial != bodyMaterial) sr.sharedMaterial = bodyMaterial;
                ApplyCasterLighting(sr, pair.Value);
            }
            for (int i=0; i<activeTorches.Count; i++)
            {
                // Flame centre in the current sprite, separate from the ground shadow source.
                var torch = activeTorches[i];
                var flameBounds = torch.view.sprite.bounds;
                Vector3 flame = torch.view.transform.TransformPoint(new Vector3(
                    flameBounds.center.x,
                    Mathf.Lerp(flameBounds.min.y, flameBounds.max.y, .88f), 0));
                wallLightData[i] = new Vector4(flame.x, flame.y, torchRadius * torch.radiusScale, lightData[i].w);
            }
            foreach (var material in wallVariants.Values)
            {
                material.SetInt(TorchCountId, activeTorches.Count);
                material.SetVectorArray(TorchesId, wallLightData);
                material.SetVectorArray(TorchColorsId, lightColors);
                UpdateAmbient(material);
            }
            if (flameMaterial != null)
            {
                // Torch metal uses the same source positions and palette as
                // the wall receiver. Its shader skips shadow reception for
                // the torch itself but still uses the shared stone response.
                flameMaterial.SetInt(TorchCountId, activeTorches.Count);
                flameMaterial.SetVectorArray(TorchesId, wallLightData);
                flameMaterial.SetVectorArray(TorchColorsId, lightColors);
                UpdateAmbient(flameMaterial);
            }
            terrain.ConfigureForestGroundMaterial(floorMaterial);
            UpdateGroundMaterial(floorMaterial, span);
            UpdateGroundMaterial(bodyMaterial, span);
        }

        private void ApplyCasterLighting(SpriteRenderer renderer, Entry entry)
        {
            using var sample = CasterPropertiesSample.Auto();
            properties.Clear(); renderer.GetPropertyBlock(properties);
            bool changed = SetReceiverVector(properties, FootWorldId,
                renderer.transform.position - Vector3.up * entry.projectedElevation);
            changed |= SetReceiverFloat(properties, IsActorId, entry.actor ? 1f : 0f);
            changed |= ConfigureForestReadability(renderer, properties,
                terrain != null && terrain.UsesVegetationPresentation, entry.actor);
            changed |= ConfigureCanopyReveal(renderer, properties);
            // Read the live block even when stationary: another effect may
            // replace it or add values. Only avoid redundant native uploads.
            if (changed) { renderer.SetPropertyBlock(properties); CasterPropertyBlockUploadCount++; }
        }

        private void PruneCasters()
        {
            LastCasterCount = 0; expired.Clear();
            foreach (var pair in entries)
            {
                if (pair.Key == null || pair.Value.seen != revision)
                {
                    var entry = pair.Value;
                    dirtyShadowLights |= entry.shadowInfluence;
                    if (!entry.actor || entry.shadowStateReady && !entry.shadowState.Actor)
                        dirtyStaticShadowLights |= entry.shadowInfluence;
                    shadowRemovalsObserved = true;
                    if (pair.Key != null) Restore(pair.Key, entry);
                    expired.Add(pair.Key);
                }
                else if (IsCasterVisible(pair.Key)) LastCasterCount++;
            }
            foreach (var sr in expired) entries.Remove(sr);
        }

        private bool NeedsShadowMaskRebuild(Rect nextBounds)
        {
            using var sample = ShadowInputsSample.Auto();
            bool configurationChanged = nextBounds != shadowBounds
                || lightHeight != shadowLightHeight || activeTorches.Count != shadowLightCount;
            for (int i = 0; i < activeTorches.Count; i++)
            {
                // Intensity/color do not change the silhouette projection.
                var light = lightData[i]; var previous = shadowLights[i];
                // A moving carried torch must not discard every stationary
                // torch's static mask. Reordering sources also dirties exactly
                // the slots whose geometric inputs changed.
                if (light.x != previous.x || light.y != previous.y || light.z != previous.z)
                    dirtyStaticShadowLights |= 1 << i;
            }
            bool changed = !shadowCacheReady || configurationChanged || entries.Count != shadowCasterCount;
            bool staticChanged = !staticShadowCacheReady || configurationChanged;
            int allLights = (1 << Mathf.Max(1, activeTorches.Count)) - 1;
            if (configurationChanged || !shadowCacheReady && dirtyShadowLights == 0) dirtyShadowLights |= allLights;
            if (configurationChanged || !staticShadowCacheReady && dirtyStaticShadowLights == 0) dirtyStaticShadowLights |= allLights;
            pendingStaticShadowCasterCount = 0;
            bool anyCasterChanged = false, anyStaticCasterChanged = false;
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                var state = ReadShadowCasterState(pair.Key, entry);
                bool casterChanged = !entry.shadowStateReady || !state.Equals(entry.shadowState);
                changed |= casterChanged;
                anyCasterChanged |= casterChanged;
                int influence = AffectedShadowLights(state.WorldBounds);
                int affected = entry.shadowInfluence | influence;
                if (casterChanged) dirtyShadowLights |= affected;
                // A role change must remove the old static silhouette as well.
                bool staticCasterChanged = casterChanged && (!entry.actor || entry.shadowStateReady && !entry.shadowState.Actor);
                staticChanged |= staticCasterChanged;
                anyStaticCasterChanged |= staticCasterChanged;
                if (staticCasterChanged) dirtyStaticShadowLights |= affected;
                if (!entry.actor) pendingStaticShadowCasterCount++;
                entry.shadowInfluence = influence;
                entry.shadowState = state; entry.shadowStateReady = true;
            }
            staticChanged |= pendingStaticShadowCasterCount != staticShadowCasterCount;
            // Defensive fallback for membership changes made without PruneCasters.
            if (entries.Count != shadowCasterCount && !shadowRemovalsObserved && !anyCasterChanged) dirtyShadowLights |= allLights;
            if (pendingStaticShadowCasterCount != staticShadowCasterCount && !shadowRemovalsObserved && !anyStaticCasterChanged)
                dirtyStaticShadowLights |= allLights;
            dirtyShadowLights |= dirtyStaticShadowLights;
            changed |= dirtyShadowLights != 0;
            staticChanged |= dirtyStaticShadowLights != 0;
            if (staticChanged) { staticShadowCacheReady = false; changed = true; }
            // Do not treat a failed submission as a valid cached mask.
            if (changed) shadowCacheReady = false;
            return changed;
        }

        private int AffectedShadowLights(Bounds casterBounds)
        {
            int result = 0;
            for (int light = 0; light < activeTorches.Count; light++)
            {
                var source = lightData[light];
                if (casterBounds.SqrDistance(new Vector3(source.x, source.y, casterBounds.center.z)) <= source.z * source.z)
                    result |= 1 << light;
            }
            return result;
        }

        private void RebuildShadowMasks(float texel)
        {
            using var sample = ShadowMasksSample.Auto();
            var vp = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax, -1, 1), true);
            commands.Clear(); LastShadowDrawCount = 0;
            int validSlices = (1 << Mathf.Max(1, activeTorches.Count)) - 1;
            int combinedSlices = dirtyShadowLights & validSlices;
            int staticSlices = dirtyStaticShadowLights & validSlices;
            PrepareDirtyShadowCasters(vp, combinedSlices, staticMask != null ? staticSlices : combinedSlices);
            LastShadowSliceCount = CountShadowSlices(combinedSlices);
            LastStaticShadowSliceCount = staticMask != null ? CountShadowSlices(staticSlices) : 0;
            bool rebuildStatic = staticMask != null && staticSlices != 0;
            if (staticMask != null)
            {
                if (rebuildStatic)
                {
                    using var staticSample = StaticShadowsSample.Auto();
                    RecordShadowCasters(staticMask, vp, texel, false, true, staticSlices);
                }
                // Both targets have identical descriptors. Preserve the existing
                // Max-blended result and the single texture lookup in receivers.
                for (int light = 0; light < Mathf.Max(1, activeTorches.Count); light++)
                    if ((combinedSlices & (1 << light)) != 0)
                        commands.CopyTexture(staticMask, light, 0, mask, light, 0);
                RecordShadowCasters(mask, vp, texel, true, false, combinedSlices);
            }
            else RecordShadowCasters(mask, vp, texel, null, true, combinedSlices);
            using (ShadowSubmitSample.Auto()) Graphics.ExecuteCommandBuffer(commands);
            RememberShadowConfiguration();
            ShadowMaskRebuildCount++;
            if (rebuildStatic) StaticShadowMaskRebuildCount++;
        }

        private static int CountShadowSlices(int bits)
        {
            int count = 0;
            while (bits != 0) { bits &= bits - 1; count++; }
            return count;
        }

        private void RecordShadowCasters(RenderTexture target, Matrix4x4 vp, float texel, bool? actorFilter, bool clear, int slices)
        {
            using var sample = ShadowRecordSample.Auto();
            for (int light = 0; light < Mathf.Max(1, activeTorches.Count); light++)
            {
                if ((slices & (1 << light)) == 0) continue;
                commands.SetRenderTarget(target, 0, CubemapFace.Unknown, light);
                if (clear) commands.ClearRenderTarget(false, true, Color.clear);
                if (light >= activeTorches.Count) continue;
                var source = lightData[light];
                LightPosition = new Vector2(source.x, source.y);
                foreach (var draw in preparedShadowCasters)
                    if ((!actorFilter.HasValue || draw.Entry.actor == actorFilter.Value)
                        && (draw.Lights & (1 << light)) != 0)
                        RenderPreparedCaster(draw.Entry, texel);
            }
        }

        private void RememberShadowConfiguration()
        {
            shadowBounds = bounds; shadowLightHeight = lightHeight;
            shadowLightCount = activeTorches.Count; shadowCasterCount = entries.Count;
            Array.Copy(lightData, shadowLights, MaxLights);
            shadowCacheReady = true;
            staticShadowCacheReady = true;
            staticShadowCasterCount = pendingStaticShadowCasterCount;
            dirtyShadowLights = dirtyStaticShadowLights = 0;
            shadowRemovalsObserved = false;
        }

        private Rect BuildShadowBounds(int resolution)
        {
            // Keep spare texels beyond the padded culling region. This also
            // preserves the previous mask span (viewport + eight world units).
            float span = Mathf.Ceil(Mathf.Max(32f, Mathf.Max(cameraGroundBounds.width, cameraGroundBounds.height) + 4f));
            float texel = span / resolution;
            // The spare mask area is already projected in world coordinates.
            // Recentring it on every coverage refill redraws all static lights,
            // even while the visible region still fits inside the old mask.
            // Retain the exact world texel grid until coverage reaches its edge.
            if (shadowBounds.width == span && shadowBounds.height == span
                && cameraGroundBounds.xMin >= shadowBounds.xMin + texel
                && cameraGroundBounds.xMax <= shadowBounds.xMax - texel
                && cameraGroundBounds.yMin >= shadowBounds.yMin + texel
                && cameraGroundBounds.yMax <= shadowBounds.yMax - texel) return shadowBounds;
            var center = cameraGroundBounds.center;
            return new Rect(Mathf.Floor((center.x - span / 2) / texel) * texel,
                Mathf.Floor((center.y - span / 2) / texel) * texel, span, span);
        }
        private void ApplyGroundLighting(
            SpriteRenderer renderer,
            bool sortOnFloor = false,
            bool reservesOutlineBand = false)
        {
            if (renderer == null) return;
            if (receiverSortingBatch && preparedReceivers.TryGetValue(renderer, out var prepared)
                && prepared.Floor == sortOnFloor && prepared.Outline == reservesOutlineBand) return;
            if (!renderer.enabled || renderer.forceRenderingOff) return;
            // Prepared bounds include the movement reserve. Offscreen props share
            // material light uniforms, but need no repeated per-renderer MPB uploads.
            if (cameraGroundBounds.width > 0 && cameraGroundBounds.height > 0
                && !IntersectsCamera(receiverSortingBatch ? ReadPreparedReceiverBounds(renderer) : renderer.bounds, 0)) return;
            if (receiverSortingBatch) preparedReceivers[renderer] = (sortOnFloor, reservesOutlineBand);
            ReceiverPreparationCount++;
            if (!presentationMaterials.TryGetValue(renderer, out var originalPresentation))
            {
                presentationMaterials.Add(renderer, (renderer.sharedMaterial, renderer.sortingLayerID, renderer.sortingOrder, sortOnFloor));
                var originalProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalProperties);
                presentationPropertyBlocks.Add(renderer, originalProperties);
            }
            else if (sortOnFloor)
            {
                // A renderer can be observed through the terrain decoration
                // stream before a direct ground registration. Preserve the
                // first original order while upgrading its ownership to the
                // floor lane for teardown restoration.
                var state = originalPresentation;
                if (!state.groundOrder)
                    presentationMaterials[renderer] =
                        (state.material, state.layer, state.order, true);
            }
            if (renderer.sharedMaterial != floorMaterial) renderer.sharedMaterial = floorMaterial;
            // GetPropertyBlock overwrites this scratch buffer. Uploads copy it
            // natively; receivers need separate original snapshots, not one
            // working block allocation/dictionary entry for every discovered prop.
            var properties = receiverWorkingProperties ??= new MaterialPropertyBlock();
            using (ReceiverBlockReadSample.Auto()) renderer.GetPropertyBlock(properties);
            var sprite = renderer.sprite;
            var texture = ReadReceiverSpriteInputs(sprite).Texture;
            bool changed = properties.GetTexture(ReceiverTextureId) != texture
                || texture == null && !properties.HasTexture(ReceiverTextureId);
            if (changed) properties.SetTexture(ReceiverTextureId, texture);
            // Only forest ground patches use world-space surface blending.
            // Props share lighting, but must retain their own sprite colours.
            changed |= SetReceiverFloat(properties, ForestSurfaceId,
                properties.GetVector(ForestPatchId).z > 0 || properties.GetFloat(ForestFogLitterId) > .5f ? 1f : 0f);
            changed |= ConfigureForestSpriteReadability(sprite, properties, terrain != null && terrain.UsesVegetationPresentation
                && (terrain.IsForestReadableProp(renderer) || (!sortOnFloor && !terrain.IsGroundLightingPresentation(renderer))));
            // Always read the live block: other effects may replace it or add
            // properties. Only upload when our own values need correction.
            if (changed) { renderer.SetPropertyBlock(properties); ReceiverPropertyBlockUploadCount++; }
            // Stairs and transitions are painted on the floor, below every
            // actor/wall band, even on maps whose depth orders are negative.
            if (sortOnFloor)
            {
                var layer = receiverSortingBatch ? receiverFloorLayer : floorReceiver.sortingLayerID;
                var floorOrder = receiverSortingBatch ? receiverFloorOrder : floorReceiver.sortingOrder;
                if (renderer.sortingLayerID != layer) renderer.sortingLayerID = layer;
                // Forest: floor < fog < contacts < outline < outlined prop.
                var order = floorOrder + terrain.GroundPresentationSortingOffset(reservesOutlineBand);
                if (renderer.sortingOrder != order) renderer.sortingOrder = order;
            }
        }

        private Bounds ReadPreparedReceiverBounds(SpriteRenderer renderer)
        {
            if (!preparedReceiverBounds.TryGetValue(renderer, out var bounds))
            {
                bounds = renderer.bounds;
                preparedReceiverBounds.Add(renderer, bounds); ReceiverBoundsReadCount++;
            }
            return bounds;
        }

        private void UpdateGroundMaterial(Material material, float span)
        {
            material.SetTexture(ShadowMasksId, mask);
            material.SetVector(GroundBoundsId, new Vector4(bounds.xMin, bounds.yMin, span, span));
            material.SetInt(TorchCountId, activeTorches.Count);
            material.SetVectorArray(TorchesId, lightData); material.SetVectorArray(TorchColorsId, lightColors);
            UpdateAmbient(material);
        }

        private void UpdateAmbient(Material material)
        {
            material.SetFloat(AmbientId, EffectiveAmbient);
            material.SetFloat(ForestShadowDetailId,
                terrain != null && terrain.UsesVegetationPresentation ? forestShadowDetail : 0f);
        }

        private static bool SetReceiverFloat(MaterialPropertyBlock block, int id, float value)
        {
            if (block.GetFloat(id) == value && (value != 0 || block.HasFloat(id))) return false;
            block.SetFloat(id, value); return true;
        }
        private static bool SetReceiverVector(MaterialPropertyBlock block, int id, Vector4 value)
        {
            if (block.GetVector(id).Equals(value) && (!value.Equals(Vector4.zero) || block.HasVector(id))) return false;
            block.SetVector(id, value); return true;
        }
        private bool ConfigureForestReadability(SpriteRenderer renderer, MaterialPropertyBlock block, bool enabled, bool actor = false)
        {
            float profile = actor ? 1f : terrain != null && terrain.IsForestCanopyRenderer(renderer) ? 0f : 2f;
            return ConfigureForestSpriteReadability(renderer.sprite, block, enabled, profile);
        }

        private bool ConfigureForestSpriteReadability(Sprite sprite, MaterialPropertyBlock block, bool enabled, float profile = 2f)
        {
            bool changed = SetReceiverFloat(block, ForestReadabilityId, enabled && sprite != null ? .55f : 0);
            changed |= SetReceiverFloat(block, ForestReadabilityProfileId, enabled ? profile : 0f);
            if (!enabled || sprite == null) return changed;
            if (!spriteUvBounds.TryGetValue(sprite, out var uvBounds))
            {
                var uv = sprite.uv;
                uvBounds = new Vector4(uv.Min(p=>p.x),uv.Min(p=>p.y),uv.Max(p=>p.x),uv.Max(p=>p.y));
                spriteUvBounds.Add(sprite,uvBounds);
            }
            changed |= SetReceiverVector(block, ForestUvId, uvBounds);
            changed |= SetReceiverVector(block, ForestTexelId, ReadReceiverSpriteInputs(sprite, true).Texel);
            return changed;
        }

        private (Texture Texture, Vector4 Texel) ReadReceiverSpriteInputs(Sprite sprite, bool needTexel = false)
        {
            if (sprite == null) return default;
            if (receiverSortingBatch && receiverSpriteInputs.TryGetValue(sprite, out var cached))
            {
                if (needTexel && !cached.HasTexel)
                {
                    cached.Texel = new Vector4(1f / cached.Texture.width, 1f / cached.Texture.height, 0, 0);
                    cached.HasTexel = true; receiverSpriteInputs[sprite] = cached;
                }
                return (cached.Texture, cached.Texel);
            }
            var texture = sprite.texture;
            var texel = needTexel ? new Vector4(1f / texture.width, 1f / texture.height, 0, 0) : Vector4.zero;
            ReceiverSpriteInputReadCount++;
            if (receiverSortingBatch) receiverSpriteInputs.Add(sprite, (texture, texel, needTexel));
            return (texture, texel);
        }

        private void WriteLightInputs(float sampledTime)
        {
            for (int i=0; i<MaxLights; i++)
            {
                lightData[i] = i < activeTorches.Count ? new Vector4(activeTorches[i].position.x, activeTorches[i].position.y,
                    torchRadius * activeTorches[i].radiusScale, terrain.UsesVegetationPresentation
                        ? ForestTorchIntensity(sampledTime, activeTorches[i].phase)
                        : TorchIntensity(sampledTime, activeTorches[i].phase)) : Vector4.zero;
                lightColors[i] = i < activeTorches.Count && activeTorches[i].arenaFire
                    ? DungeonFloorMapView.ArenaFireLightColor
                    : i < activeTorches.Count && activeTorches[i].forestFixture
                    ? new Color(1.55f, .66f, .20f, 1f)
                    : terrain.UsesVegetationPresentation ? new Color(1.05f, .72f, .43f, 1f) : torchColor;
            }
        }
        private void AdvanceForestLightIntensity(float time)
        {
            if (!terrain.UsesVegetationPresentation || activeTorches.Count == 0) return;
            // Only light energy changes between geometry ticks. Keep source
            // selection, bounds, contacts and shadow texture arrays at 5 Hz.
            for (int i = 0; i < activeTorches.Count; i++)
            {
                float intensity = ForestTorchIntensity(time, activeTorches[i].phase);
                lightData[i].w = intensity; wallLightData[i].w = intensity;
            }
            floorMaterial.SetVectorArray(TorchesId, lightData);
            bodyMaterial.SetVectorArray(TorchesId, lightData);
            foreach (var material in wallVariants.Values) material.SetVectorArray(TorchesId, wallLightData);
            if (flameMaterial != null) flameMaterial.SetVectorArray(TorchesId, wallLightData);
        }
        private static float DistanceSquaredToRect(Vector2 point, Rect rect)
        {
            // Two-argument overloads avoid a params-array allocation per torch.
            float dx = Mathf.Max(Mathf.Max(rect.xMin-point.x, 0f), point.x-rect.xMax);
            float dy = Mathf.Max(Mathf.Max(rect.yMin-point.y, 0f), point.y-rect.yMax);
            return dx*dx+dy*dy;
        }
        private bool IntersectsCamera(Bounds spriteBounds, float margin)
            => spriteBounds.max.x+margin >= cameraGroundBounds.xMin
                && spriteBounds.min.x-margin <= cameraGroundBounds.xMax
                && spriteBounds.max.y+margin >= cameraGroundBounds.yMin
                && spriteBounds.min.y-margin <= cameraGroundBounds.yMax;
        private static bool IsCasterVisible(SpriteRenderer sr) => sr != null && sr.sprite != null && sr.enabled
            && !sr.forceRenderingOff && sr.gameObject.activeInHierarchy && sr.color.a > 0;

        private void BuildTorches()
        {
            if (terrain.UsesVegetationPresentation)
            {
                BuildForestFixtures();
                return;
            }
            var map = terrain.LightingMap; if (map == null) return;
            foreach (var cell in map.FloorCells.OrderBy(c=>c.y).ThenBy(c=>c.x))
            {
                if (map.IsWalkable(cell + Vector2Int.up)) continue;
                var position = terrain.CellToWorldCenter(cell) + Vector2.up * .35f;
                if (torches.Any(t => Vector2.Distance(t.position, position) < 5.5f)) continue;
                var go = new GameObject("Wall torch"); go.transform.SetParent(transform, false);
                go.transform.position = position + Vector2.up * .15f;
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = torchSprite; renderer.sharedMaterial = flameMaterial;
                renderer.sortingLayerID = terrain.FloorFeedbackSortingLayerID;
                renderer.sortingOrder = terrain.GetWallMountedDepthSortingOrder(position.y);
                float phase = Mathf.Repeat(cell.x * 1.73f + cell.y * 3.17f, Mathf.PI * 2);
                var block = new MaterialPropertyBlock(); block.SetFloat("_Phase", phase); renderer.SetPropertyBlock(block);
                torches.Add(new Torch {position=position, cell=new DungeonSurfacePosition(cell), view=renderer, phase=phase});
            }
        }

        private void Touch(SpriteRenderer sr, bool actor, float projectedElevation = 0)
        {
            if (!IsCasterVisible(sr)) return;
            // Also retain nearby offscreen casters whose shadows can enter
            // the viewport; debug fog must not generate meshes for the whole map.
            var sourceBounds = ReadCasterBounds(sr);
            if (!IntersectsCamera(sourceBounds, sourceBounds.size.y*1.5f+.5f)) return;
            if (!entries.TryGetValue(sr, out var entry))
            {
                entry = new Entry { original = sr.sharedMaterial, properties = new MaterialPropertyBlock(), actor = actor };
                sr.GetPropertyBlock(entry.properties); entries.Add(sr, entry);
            }
            entry.seen = revision;
            entry.actor = actor;
            entry.projectedElevation = projectedElevation;
        }

        private void PrepareShadowCasters(Matrix4x4 vp)
            => PrepareDirtyShadowCasters(vp, int.MaxValue, int.MaxValue);

        private ShadowCasterState ReadShadowCasterState(SpriteRenderer renderer, Entry entry)
        {
            using var sample = ShadowStateSample.Auto();
            return new ShadowCasterState(renderer, entry, ReadContactProfile(renderer.sprite, entry.actor), ReadCasterBounds(renderer));
        }

        private Bounds ReadCasterBounds(SpriteRenderer renderer)
            => receiverBoundsBatch ? ReadPreparedReceiverBounds(renderer) : renderer.bounds;

        private void PrepareDirtyShadowCasters(Matrix4x4 vp, int actorSlices, int staticSlices)
        {
            using var sample = ShadowPrepareSample.Auto();
            preparedShadowCasters.Clear();
            foreach (var pair in entries)
            {
                var sr = pair.Key; var entry = pair.Value;
                if (!IsCasterVisible(sr)) continue;
                int influence = AffectedShadowLights(ReadCasterBounds(sr));
                // A clean static texture is copied as-is; only the casters
                // drawn into dirty slices need fresh draw preparation.
                if ((influence & (entry.actor ? actorSlices : staticSlices)) == 0) continue;
                var sprite = sr.sprite;
                var state = ReadShadowCasterState(sr, entry);
                var contact = state.Contact;
                var stateChanged = !entry.drawStateReady || !state.Equals(entry.drawState);
                var texture = sprite.texture;
                var propertiesChanged = !entry.drawStateReady || !state.SameDrawProperties(entry.drawState)
                    || entry.drawTexture != texture;
                var block = entry.drawProperties ??= new MaterialPropertyBlock();
                if (stateChanged)
                {
                    var scale = state.SignedScale;
                    entry.drawFoot = (Vector2)state.Position + Vector2.Scale(contact.footLocal, scale)
                        - Vector2.up * entry.projectedElevation;
                    ShadowGeometryPreparationCount++;
                }
                if (propertiesChanged)
                {
                    using var propertySample = ShadowPropertiesSample.Auto();
                    var scale = state.SignedScale;
                    block.Clear(); block.SetTexture(ReceiverTextureId, texture);
                    block.SetVector(ShadowFootId, contact.footLocal);
                    block.SetVector(ShadowScaleId, scale);
                    block.SetVector(ShadowContactId, new Vector4(contact.slope, contact.offset, 0, 0));
                    float halfDepth = sprite.bounds.size.x * SilhouetteVolumeMesh.DepthToWidth * .5f;
                    block.SetFloat(ShadowVolumeId, halfDepth * Mathf.Abs(scale.x));
                    block.SetFloat(ShadowDepthId, halfDepth * sprite.pixelsPerUnit);
                    if (!spriteUvBounds.TryGetValue(sprite, out var uvBounds))
                    {
                        var uv = sprite.uv;
                        uvBounds = new Vector4(uv.Min(p => p.x), uv.Min(p => p.y), uv.Max(p => p.x), uv.Max(p => p.y));
                        spriteUvBounds.Add(sprite, uvBounds);
                    }
                    block.SetVector(ShadowUvId, uvBounds);
                    entry.drawMesh = GetMesh(sprite);
                    entry.drawTexture = texture;
                    ShadowPropertyPreparationCount++;
                }
                if (propertiesChanged || !entry.drawVp.Equals(vp))
                { block.SetMatrix(ShadowVpId, vp); entry.drawVp = vp; }
                entry.drawState = state; entry.drawStateReady = true;
                preparedShadowCasters.Add((entry, influence));
            }
        }

        private void RenderPreparedCaster(Entry entry, float texel)
        {
            var foot = entry.drawFoot;
            var away = foot - LightPosition;
            var direction = new Vector2(away.x, away.y / .75f);
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
            var longitudinal = new Vector2(direction.x, direction.y * .75f) * Mathf.Clamp(away.magnitude / lightHeight, .3f, 1.5f);
            var block = entry.drawProperties;
            block.SetVector(ShadowHeightId, longitudinal);
            for (int i = 0; i < 4; i++)
            {
                block.SetVector(ShadowAnchorId, new Vector4(foot.x + ((i & 1) == 0 ? -.35f : .35f) * texel,
                    foot.y + ((i & 2) == 0 ? -.35f : .35f) * texel, 0, 0));
                commands.DrawMesh(entry.drawMesh, Matrix4x4.identity, projection, 0, 0, block);
                LastShadowDrawCount++;
            }
        }

        private ContactProfile ReadContactProfile(Sprite sprite, bool actor)
        {
            foreach (var profile in contacts) if (profile.sprite == sprite) return profile;
            return new ContactProfile { footLocal = actor || terrain != null && terrain.UsesVegetationPresentation ? Vector2.zero : new Vector2(0, sprite.bounds.min.y) };
        }

        private Mesh GetMesh(Sprite sprite)
        {
            using var sample = MeshLookupSample.Auto();
            if (meshes.TryGetValue(sprite, out var mesh)) return mesh;
            var geometry = new SilhouetteVolumeMesh.GeometryKey(sprite);
            if (!sharedMeshes.TryGetValue(geometry, out mesh))
            {
                using (MeshBuildSample.Auto()) mesh = SilhouetteVolumeMesh.Build(geometry);
                sharedMeshes.Add(geometry, mesh); ShadowMeshBuildCount++;
            }
            meshes.Add(sprite, mesh); return mesh;
        }

        private bool CreateResources()
        {
            if (maskShader == null || floorShader == null || bodyShader == null || torchShader == null || wallShader == null || torchSprite == null)
            { Debug.LogError("Dungeon silhouette lighting requires shader references.", this); enabled = false; return false; }
            projection = new Material(maskShader); floorMaterial = new Material(floorShader); bodyMaterial = new Material(bodyShader);
            flameMaterial = new Material(torchShader);
            if (torchFlameMask != null) flameMaterial.SetTexture("_FlameMask", torchFlameMask);
            flameMaterial.SetFloat("_FlameBase", .70f);
            originalFloor = floorReceiver.sharedMaterial; floorReceiver.sharedMaterial = floorMaterial;
            if (contactShader != null) contactShadows = new DungeonContactShadows(transform, contactShader);
            mask = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1, dimension = TextureDimension.Tex2DArray, volumeDepth = MaxLights };
            mask.Create(); commands = new CommandBuffer { name = "Dungeon silhouette shadows" }; properties = new MaterialPropertyBlock();
            // Same format/size/layers makes the copy lossless. Unsupported
            // devices retain the original full redraw without another texture.
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0)
            {
                staticMask = new RenderTexture(mask.descriptor)
                    { name = "Static dungeon silhouette shadows", filterMode = mask.filterMode, wrapMode = mask.wrapMode };
                if (!staticMask.Create()) { DestroyLightingObject(staticMask); staticMask = null; }
            }
            shadowCacheReady = staticShadowCacheReady = false;
            dirtyShadowLights = dirtyStaticShadowLights = 0;
            shadowRemovalsObserved = false;
            return true;
        }
        private static void Restore(SpriteRenderer sr, Entry entry)
        { sr.sharedMaterial = entry.original; sr.SetPropertyBlock(entry.properties); }
        private static void DestroyLightingObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDisable()
        {
            ResetForestCanopyFade();
            lastLightingFrame = int.MinValue;
            shadowCacheReady = false;
            staticShadowCacheReady = false;
            dirtyShadowLights = dirtyStaticShadowLights = 0;
            shadowRemovalsObserved = false;
            contactShadows?.Dispose(); contactShadows = null;
            foreach (var pair in entries) if (pair.Key != null) Restore(pair.Key, pair.Value);
            entries.Clear(); preparedShadowCasters.Clear(); obstacles.Clear(); spriteUvBounds.Clear();
            preparedReceivers.Clear(); receiverSpriteInputs.Clear();
            foreach (var wall in wallMaterials) if (wall.Key != null) wall.Key.sharedMaterial = wall.Value;
            wallMaterials.Clear();
            foreach (var presentation in presentationMaterials)
                if (presentation.Key != null)
                {
                    presentation.Key.sharedMaterial = presentation.Value.material;
                    if (presentationPropertyBlocks.TryGetValue(presentation.Key, out var originalProperties))
                        presentation.Key.SetPropertyBlock(originalProperties);
                    if (presentation.Value.groundOrder)
                    {
                        presentation.Key.sortingLayerID = presentation.Value.layer;
                        presentation.Key.sortingOrder = presentation.Value.order;
                    }
                }
            presentationMaterials.Clear();
            presentationPropertyBlocks.Clear();
            receiverWorkingProperties = null;
            additionalObjectViews.Clear();
            additionalObjectRenderers.Clear();
            groundObjectViews.Clear();
            groundObjectRenderers.Clear();
            groundContactPresentations.Clear();
            foreach (var material in wallVariants.Values) DestroyLightingObject(material);
            wallVariants.Clear();
            ClearTorchFixtures();
            torchMap = null;
            if (flameMaterial != null) DestroyLightingObject(flameMaterial);
            if (floorReceiver != null && floorMaterial != null && floorReceiver.sharedMaterial == floorMaterial) floorReceiver.sharedMaterial = originalFloor;
            foreach (var mesh in sharedMeshes.Values) DestroyLightingObject(mesh);
            sharedMeshes.Clear(); meshes.Clear();
            commands?.Release(); commands = null;
            if (mask != null) { mask.Release(); DestroyLightingObject(mask); } mask = null;
            if (staticMask != null) { staticMask.Release(); DestroyLightingObject(staticMask); } staticMask = null;
            if (projection != null) DestroyLightingObject(projection); if (floorMaterial != null) DestroyLightingObject(floorMaterial); if (bodyMaterial != null) DestroyLightingObject(bodyMaterial);
            projection = null; floorMaterial = null; bodyMaterial = null;
        }
    }
}
