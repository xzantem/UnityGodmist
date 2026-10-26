using UnityEngine;
using System.Collections.Generic;
using Godmist.Runtime.Dungeons;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private Camera presentationCamera;
        public Camera PresentationCamera => presentationCamera;
        private readonly Plane[] presentationPlanes = new Plane[6];
        private Matrix4x4 presentationViewMatrix, presentationProjectionMatrix;
        private Matrix4x4 presentationGroundMatrix;
        private bool presentationCameraReady, presentationCoverageReady;
        private Rect presentationViewport, presentationCoverage;
        private BoundsInt presentationCellCoverage;
        private bool refreshingCameraPresentation;
        private int presentationQueryDepth;
        private bool presentationQueryReady;
        public int PresentationCameraReadCount { get; private set; }

        // Reuse camera inputs only during one synchronous lighting traversal.
        // A later caller/frame always observes pan, zoom and camera changes.
        public PresentationCameraQueryScope BeginPresentationCameraQueries() => new(this);
        public readonly struct PresentationCameraQueryScope : System.IDisposable
        {
            private readonly DungeonFloorMapView owner;
            internal PresentationCameraQueryScope(DungeonFloorMapView owner)
            {
                this.owner = owner;
                if (owner.presentationQueryDepth == 0)
                    owner.presentationQueryReady = owner.ReadPresentationCamera();
                owner.presentationQueryDepth++;
            }
            public void Dispose() => owner.presentationQueryDepth--;
        }
        private float presentationVisualMargin = 2;
        private DungeonFloorInstance worldPresentationFloor;
        private DungeonFloorMap worldPresentationMap;
        private int worldFogRevision = -1, worldDebugRevision = -1, worldDiscoveryCount = -1, worldTopologyRevision = -1;
        private int worldWallCount = -1, worldDecorationCount = -1;
        private int worldPresentationContentRevision, presentedWorldContentRevision = -1;
        private Matrix4x4 worldFloorMatrix, worldWallsMatrix;
        public int CoverageOnlyRefreshCount { get; private set; }
        public int FogCoverageKnowledgeReuseCount { get; private set; }

        private bool WorldPresentationIsCurrent() => ReferenceEquals(worldPresentationFloor, FogFloor)
            && ReferenceEquals(worldPresentationMap, boundMap) && FogFloor != null
            && worldFogRevision == FogFloor.Fog.Revision && worldDebugRevision == FogFloor.Fog.DebugRevision
            && worldDiscoveryCount == FogFloor.Fog.Discovered.Count && worldTopologyRevision == boundMap.TraversalRevision
            && worldWallCount == wallRenderers.Count && worldDecorationCount == presentationDecorations.Count
            && presentedWorldContentRevision == worldPresentationContentRevision
            && worldFloorMatrix.Equals(floorTilemap.transform.localToWorldMatrix)
            && worldWallsMatrix.Equals((wallsRoot != null ? wallsRoot : transform).localToWorldMatrix)
            && FogFloor.Fog.ClosedGates.SetEquals(fogPresentedClosedGates);

        private void RememberWorldPresentation()
        {
            worldPresentationFloor = FogFloor; worldPresentationMap = boundMap;
            worldFogRevision = FogFloor?.Fog.Revision ?? -1; worldDebugRevision = FogFloor?.Fog.DebugRevision ?? -1;
            worldDiscoveryCount = FogFloor?.Fog.Discovered.Count ?? -1; worldTopologyRevision = boundMap.TraversalRevision;
            worldWallCount = wallRenderers.Count; worldDecorationCount = presentationDecorations.Count;
            presentedWorldContentRevision = worldPresentationContentRevision;
            worldFloorMatrix = floorTilemap.transform.localToWorldMatrix;
            worldWallsMatrix = (wallsRoot != null ? wallsRoot : transform).localToWorldMatrix;
        }

        // Renderer arrays can be replaced without changing the number of wall owners.
        private void InvalidateWorldPresentation() => worldPresentationContentRevision++;

        public void ConfigurePresentationCamera(Camera camera, float visualMargin = 2)
        {
            presentationVisualMargin = Mathf.Max(2, visualMargin);
            if (presentationCamera == camera) return;
            presentationCamera = camera;
            presentationCameraReady = presentationCoverageReady = false;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeforePresentationCamera;
            if (camera != null) UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeforePresentationCamera;
        }
        private void BeforePresentationCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        { if (camera == presentationCamera && isActiveAndEnabled) EnsureCameraPresentation(); }
        private void ReleasePresentationCamera()
        { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeforePresentationCamera; }

        // This is a presentation-only camera. No fog/LOS, AI or colliders are culled.
        public bool IsInPresentationCamera(Bounds bounds)
        {
            if (!ReadPresentationCamera()) return true;
            return IntersectsPresentationCamera(bounds);
        }

        public bool AreAnyInPresentationCamera(Renderer[] renderers)
        {
            if (!ReadPresentationCamera()) return true;
            foreach (var renderer in renderers)
                if (renderer != null && IntersectsPresentationCamera(renderer.bounds)) return true;
            return false;
        }

        private bool IntersectsPresentationCamera(Bounds bounds)
        {
            // Lighting supplies its full source/caster reach, not just sprite
            // size: an off-camera shadow must not disappear inside the viewport.
            bounds.Expand(presentationVisualMargin * 2);
            return GeometryUtility.TestPlanesAABB(presentationPlanes, bounds);
        }

        private bool ReadPresentationCamera()
        {
            if (presentationQueryDepth > 0) return presentationQueryReady;
            PresentationCameraReadCount++;
            if (presentationCamera == null || !presentationCamera.isActiveAndEnabled) return false;
            var view = presentationCamera.worldToCameraMatrix;
            var projection = presentationCamera.projectionMatrix;
            var groundMatrix = floorTilemap != null ? floorTilemap.transform.localToWorldMatrix : Matrix4x4.identity;
            if (presentationCameraReady && view == presentationViewMatrix && projection == presentationProjectionMatrix && groundMatrix == presentationGroundMatrix) return true;
            if (groundMatrix != presentationGroundMatrix) presentationCoverageReady = false;
            presentationGroundMatrix = groundMatrix;
            presentationViewMatrix = view; presentationProjectionMatrix = projection;
            GeometryUtility.CalculateFrustumPlanes(presentationCamera, presentationPlanes);
            var ground = new Plane(Vector3.forward, floorTilemap != null ? floorTilemap.transform.position : Vector3.zero);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var y = 0; y <= 1; y++) for (var x = 0; x <= 1; x++)
            {
                var ray = presentationCamera.ViewportPointToRay(new Vector3(x, y, 0));
                if (!ground.Raycast(ray, out var distance)) { presentationCameraReady = false; return false; }
                var p = (Vector2)ray.GetPoint(distance); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            presentationViewport = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            presentationCameraReady = true;
            return true;
        }

        private bool UseLocalFogPresentation => UsesVegetationPresentation && boundMap != null
            && FogFloor != null && ReferenceEquals(FogFloor.Map, boundMap)
            && !boundMap.HasElevatedSurfaces && floorTilemap != null
            && floorTilemap.cellLayout == GridLayout.CellLayout.Rectangle && ReadPresentationCamera();

        private void PreparePresentationCoverage()
        {
            if (!UseLocalFogPresentation) { presentationCoverageReady = false; return; }
            if (presentationCoverageReady && CoverageContainsViewport()) return;
            presentationCoverage = Rect.MinMaxRect(presentationViewport.xMin - 4, presentationViewport.yMin - 4,
                presentationViewport.xMax + 4, presentationViewport.yMax + 4);
            var min = new Vector3Int(int.MaxValue, int.MaxValue, 0);
            var max = new Vector3Int(int.MinValue, int.MinValue, 0);
            for (var y = 0; y <= 1; y++) for (var x = 0; x <= 1; x++)
            {
                var p = floorTilemap.WorldToCell(new Vector3(x == 0 ? presentationCoverage.xMin : presentationCoverage.xMax,
                    y == 0 ? presentationCoverage.yMin : presentationCoverage.yMax, floorTilemap.transform.position.z));
                min = Vector3Int.Min(min, p); max = Vector3Int.Max(max, p);
            }
            presentationCellCoverage = new BoundsInt(new Vector3Int(min.x - 2, min.y - 2, 0),
                new Vector3Int(max.x - min.x + 5, max.y - min.y + 5, 1));
            presentationCoverageReady = true;
        }
        private bool CoverageContainsViewport() => presentationViewport.xMin >= presentationCoverage.xMin + 2
            && presentationViewport.yMin >= presentationCoverage.yMin + 2
            && presentationViewport.xMax <= presentationCoverage.xMax - 2
            && presentationViewport.yMax <= presentationCoverage.yMax - 2;

        private static readonly Unity.Profiling.ProfilerMarker CameraPresentationSample = new("Godmist.Dungeon.CameraPresentation");
        public void EnsureCameraPresentation()
        {
            if (refreshingCameraPresentation || boundMap == null || floorTilemap == null || !UseLocalFogPresentation) return;
            if (presentationCoverageReady && CoverageContainsViewport()) return;
            using var sample = CameraPresentationSample.Auto();
            refreshingCameraPresentation = true;
            try { RefreshElevationPresentation(); }
            finally { refreshingCameraPresentation = false; }
        }

        private bool IsFogCellInCoverage(Vector2Int cell)
        {
            if (!presentationCoverageReady) return true;
            // The neighbour ring and feathering are outside the camera reserve.
            return presentationCellCoverage.Contains(new Vector3Int(cell.x, cell.y, 0));
        }

        private IEnumerable<DungeonTraversalPosition> FogPresentationPositions()
        {
            if (!presentationCoverageReady || (long)presentationCellCoverage.size.x * presentationCellCoverage.size.y > boundMap.FloorCells.Count)
            { foreach (var position in elevation.VisiblePositions) yield return position; yield break; }
            // Query the existing projection index; don't enumerate the entire
            // floor merely to reject almost all of it outside the camera.
            for (var y = presentationCellCoverage.yMin; y < presentationCellCoverage.yMax; y++)
            for (var x = presentationCellCoverage.xMin; x < presentationCellCoverage.xMax; x++)
                if (elevation.TryGetVisiblePosition(new Vector2Int(x, y), out var position)) yield return position;
        }
    }
}
