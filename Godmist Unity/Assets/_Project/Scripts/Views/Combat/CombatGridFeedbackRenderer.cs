using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public enum CombatGridFeedbackLayer
    {
        MovementRange,
        InspectedMovementRange,
        SkillRange,
        SkillTarget,
        RelativeRange,
        AffectedArea,
        MovementPreview,
        ActiveMovementUnit,
        EnemyFov,
        ChosenSkillTargets,
        JudgementField,
        SkillHover
    }

    public partial class CombatGridFeedbackRenderer : MonoBehaviour
    {
        public System.Func<GridPosition, bool> IsPositionVisible { get; set; }
        public System.Func<GridPosition, bool> IsEnemyFovPositionVisible { get; set; }
        private bool CanPresent(CombatGridFeedbackLayer layer, GridPosition position) =>
            (layer == CombatGridFeedbackLayer.EnemyFov ? IsEnemyFovPositionVisible : IsPositionVisible)?.Invoke(position) != false;
        private readonly Dictionary<CombatGridFeedbackLayer, (GridPosition[] Cells, Color Color, System.Func<GridPosition, Color> Tint)> outlineSources = new();
        private readonly Dictionary<CombatGridFeedbackLayer, (GridPosition[] Cells, Color Color, System.Func<GridPosition, Color> Tint)> fillSources = new();
        public void RefreshVisibility()
        {
            if (pathCost != null) pathCost.gameObject.SetActive(IsPositionVisible?.Invoke(pathCostPosition) != false);
            foreach (var marker in pathMarkerCells)
                if (marker.Key != null) marker.Key.gameObject.SetActive(CanPresent(CombatGridFeedbackLayer.MovementPreview, marker.Value));
            foreach (var pair in outlineSources.ToArray()) ShowOutline(pair.Key, pair.Value.Cells, pair.Value.Color, pair.Value.Tint);
            foreach (var pair in fillSources.ToArray()) ShowFill(pair.Key, pair.Value.Cells, pair.Value.Color, pair.Value.Tint);
        }

        public void SetGridMapping(Vector2 origin, float size)
        {
            gridOrigin = origin;
            cellSize = Mathf.Max(0.01f, size);
        }
        [Header("Visual Assets")]
        [SerializeField] private Sprite feedbackSprite;
        [SerializeField] private TMP_FontAsset pathCostFont;
        [SerializeField] private Transform feedbackRoot;
        [SerializeField] private string feedbackSortingLayer = "Default";
        [SerializeField] private int feedbackSortingOrder = -1;
        [SerializeField] private int pathSortingOrder = 1;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField, Min(0.001f)] private float edgeThickness = .045f;
        [SerializeField, Min(0.01f)] private float pathSegmentLength = 1f;
        [SerializeField, Min(0.001f)] private float pathSegmentThickness = .065f;
        [SerializeField] private float feedbackZ = -.2f;
        [SerializeField] private Vector2 costLabelOffset = new(0f, .22f);
        [SerializeField, Min(0.001f)] private float pathCostWorldScale = .12f;

        [SerializeField, Range(0f, 1f)] private float innerGridAlpha = .18f;
        [SerializeField, Min(.001f)] private float innerGridThickness = .012f;
        [SerializeField, Range(0f, 1f)] private float haloAlpha = .12f;
        [SerializeField, Min(1f)] private float haloWidthMultiplier = 3f;
        [SerializeField, Range(0f, 1f)] private float destinationFillAlpha = .1f;

        private readonly Dictionary<CombatGridFeedbackLayer, List<SpriteRenderer>> outlines = new();
        private readonly Dictionary<CombatGridFeedbackLayer, List<SpriteRenderer>> fills = new();
        private sealed class FillBatch
        {
            public GameObject GameObject;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public GridPosition[] Cells;
            public Color Tint;
            public Vector2 Origin;
            public float Size;
            public float Depth;
        }
        private readonly Dictionary<CombatGridFeedbackLayer, FillBatch> fillBatches = new();
        private readonly Dictionary<CombatGridFeedbackLayer, FillBatch> softOutlines = new();
        private readonly Dictionary<CombatGridFeedbackLayer, FillBatch> cornerBatches = new();
        private Material cornerMaterial;
        private Texture2D softFillTexture, softBoundaryTexture;
        private Material softFillMaterial, softBoundaryMaterial;
        private readonly bool[] softMasks = new bool[256];
        private readonly float[,] softWeights = new float[32, 3];
        private Material fillBatchMaterial;
        private readonly List<SpriteRenderer> pathMarkers = new();
        private readonly Dictionary<SpriteRenderer, GridPosition> pathMarkerCells = new();
        private Sprite targetCornerSprite, areaEdgeSprite, pathGemSprite;
        private UnityEngine.Rendering.SortingGroup floorSortingGroup;
        private bool createdFloorSortingGroup, capturedFloorSortingGroup;
        private int originalFloorLayer, originalFloorOrder;
        private bool originalFloorEnabled;
        private TMP_Text pathCost;
        private UnityEngine.Rendering.SortingGroup pathCostSortingGroup;
        private const int PathCostOverlaySortingOrder = 32000;
        private GridPosition pathCostPosition;

        public void SetFloorSorting(int layerID, int order)
        {
            var root = feedbackRoot != null ? feedbackRoot : transform;
            feedbackRoot = root;
            if (floorSortingGroup == null)
            {
                floorSortingGroup = root.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (floorSortingGroup == null)
                {
                    floorSortingGroup = root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    createdFloorSortingGroup = true;
                }
            }
            if (!capturedFloorSortingGroup)
            {
                originalFloorLayer = floorSortingGroup.sortingLayerID;
                originalFloorOrder = floorSortingGroup.sortingOrder;
                originalFloorEnabled = floorSortingGroup.enabled;
                capturedFloorSortingGroup = true;
            }
            floorSortingGroup.enabled = true;
            floorSortingGroup.sortingLayerID = layerID;
            floorSortingGroup.sortingOrder = order;
            if (pathCostSortingGroup != null) pathCostSortingGroup.sortingLayerID = layerID;
        }

        private static bool IsPixelMarkerLayer(CombatGridFeedbackLayer layer) => layer is
            CombatGridFeedbackLayer.SkillTarget or CombatGridFeedbackLayer.ChosenSkillTargets
            or CombatGridFeedbackLayer.AffectedArea or CombatGridFeedbackLayer.RelativeRange
            or CombatGridFeedbackLayer.MovementPreview or CombatGridFeedbackLayer.ActiveMovementUnit
            or CombatGridFeedbackLayer.JudgementField;

        private void Awake()
        {
            if (feedbackRoot == null) feedbackRoot = transform;
            if (pathCostFont == null) pathCostFont = TMP_Settings.defaultFontAsset;
            // Allocate/upload the small presentation atlas during scene setup,
            // rather than in the first skill-selection input callback.
            EnsureSoftTextures();
        }

        public void ShowOutline(
            CombatGridFeedbackLayer layer,
            IEnumerable<GridPosition> positions,
            Color color, System.Func<GridPosition, Color> cellColor = null)
        {
            ClearOutline(layer);
            if (positions == null) return;

            var source = positions.ToArray();
            outlineSources[layer] = (source, color, cellColor);
            if (IsPixelMarkerLayer(layer))
            {
                ShowPixelMarkers(layer, source, color, cellColor);
                return;
            }
            if (layer != CombatGridFeedbackLayer.EnemyFov)
            {
                color.a *= .65f;
                if (cellColor != null)
                {
                    var originalTint = cellColor;
                    cellColor = p => { var tint = originalTint(p); tint.a *= .65f; return tint; };
                }
                ShowSoftFloor(layer, source, color, cellColor, true);
                return;
            }
            if (feedbackSprite == null) return;
            var projected = source.Where(p => CanPresent(layer, p))
                .GroupBy(p => new GridPosition(p.X, p.Y)).ToDictionary(group => group.Key, group => group.First());
            var cells = new HashSet<GridPosition>(projected.Keys);
            var renderers = new List<SpriteRenderer>();
            foreach (var edge in CombatGridFeedbackGeometry.BuildOutlineEdges(cells))
            {
                var renderer = CreateSpriteRenderer("Combat feedback edge");
                renderer.color = cellColor?.Invoke(projected[edge.Cell]) ?? color;
                ConfigureEdge(renderer.transform, renderer.sprite, edge);
                if (layer == CombatGridFeedbackLayer.MovementPreview || layer == CombatGridFeedbackLayer.ActiveMovementUnit)
                    renderer.sortingOrder = pathSortingOrder;
                renderers.Add(renderer);
                if (haloAlpha > 0f)
                {
                    var halo = CreateSpriteRenderer("Combat boundary halo");
                    halo.sortingOrder = renderer.sortingOrder;
                    halo.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, renderer.color.a * haloAlpha);
                    ConfigureEdge(halo.transform, halo.sprite, edge);
                    var scale = halo.transform.localScale;
                    if (edge.Direction == CombatGridEdgeDirection.Top || edge.Direction == CombatGridEdgeDirection.Bottom) scale.y *= haloWidthMultiplier;
                    else scale.x *= haloWidthMultiplier;
                    halo.transform.localScale = scale;
                    renderers.Add(halo);
                }
            }
            if (layer == CombatGridFeedbackLayer.MovementRange || layer == CombatGridFeedbackLayer.InspectedMovementRange)
            {
                foreach (var cell in cells)
                {
                    if (cells.Contains(new GridPosition(cell.X + 1, cell.Y))) AddInnerEdge(cell, CombatGridEdgeDirection.Right);
                    if (cells.Contains(new GridPosition(cell.X, cell.Y + 1))) AddInnerEdge(cell, CombatGridEdgeDirection.Top);
                }
            }
            void AddInnerEdge(GridPosition cell, CombatGridEdgeDirection direction)
            {
                var line = CreateSpriteRenderer("Combat individual cell edge");
                var tint = cellColor?.Invoke(projected[cell]) ?? color;
                line.color = new Color(tint.r, tint.g, tint.b, innerGridAlpha);
                ConfigureEdge(line.transform, line.sprite, new CombatGridOutlineEdge(cell, direction));
                var scale = line.transform.localScale;
                if (direction == CombatGridEdgeDirection.Top) scale.y *= innerGridThickness / edgeThickness;
                else scale.x *= innerGridThickness / edgeThickness;
                line.transform.localScale = scale;
                renderers.Add(line);
            }
            outlines[layer] = renderers;
        }

        public void ShowFill(
            CombatGridFeedbackLayer layer,
            IEnumerable<GridPosition> positions,
            Color color, System.Func<GridPosition, Color> cellColor = null)
        {
            ClearFill(layer);
            if (positions == null) return;

            var renderers = new List<SpriteRenderer>();
            var source = positions.ToArray();
            fillSources[layer] = (source, color, cellColor);
            // Target markers are outlines only. A second wash over the range
            // made overlapping active/destination/target cells much brighter.
            if (IsPixelMarkerLayer(layer)) return;
            // Store the source tint above so fog refresh never compounds opacity.
            var opacity = layer switch
            {
                CombatGridFeedbackLayer.MovementRange or CombatGridFeedbackLayer.InspectedMovementRange => .06f,
                CombatGridFeedbackLayer.SkillRange or CombatGridFeedbackLayer.RelativeRange => .18f,
                CombatGridFeedbackLayer.SkillTarget or CombatGridFeedbackLayer.AffectedArea => .55f,
                _ => 1f
            };
            color.a *= opacity;
            if (cellColor != null)
            {
                var sourceTint = cellColor;
                cellColor = position => { var tint = sourceTint(position); tint.a *= opacity; return tint; };
            }
            if (layer != CombatGridFeedbackLayer.EnemyFov)
            {
                ShowSoftFloor(layer, source, color, cellColor, false);
                return;
            }
            if (feedbackSprite == null) return;
            if (cellColor == null && source.Length >= 48 && TryShowFillBatch(layer, source, color))
                return;
            foreach (var position in source.Where(p => CanPresent(layer, p)))
            {
                var renderer = CreateSpriteRenderer("Combat feedback fill");
                renderer.color = cellColor?.Invoke(position) ?? color;
                renderer.transform.localPosition = GridToLocal(position) + Vector3.back * .01f;
                ScaleSprite(renderer.transform, renderer.sprite, Vector2.one * cellSize);
                renderers.Add(renderer);

            }

            fills[layer] = renderers;
        }

        private bool TryShowFillBatch(CombatGridFeedbackLayer layer, GridPosition[] source, Color color)
        {
            if (feedbackSprite?.texture == null) return false;
            if (fillBatchMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return false;
                fillBatchMaterial = new Material(shader) { mainTexture = feedbackSprite.texture };
            }
            if (!fillBatches.TryGetValue(layer, out var batch))
            {
                var gameObject = new GameObject("Combat feedback fill batch", typeof(MeshFilter), typeof(MeshRenderer));
                gameObject.transform.SetParent(feedbackRoot, false);
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = gameObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = fillBatchMaterial;
                renderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
                renderer.sortingOrder = feedbackSortingOrder;
                fillBatches.Add(layer, batch = new FillBatch
                {
                    GameObject = gameObject, Mesh = mesh, Renderer = renderer
                });
            }

            var uv = feedbackSprite.uv;
            var left = uv.Min(value => value.x);
            var right = uv.Max(value => value.x);
            var bottom = uv.Min(value => value.y);
            var top = uv.Max(value => value.y);
            var vertices = new List<Vector3>(source.Length * 4);
            var colors = new List<Color32>(source.Length * 4);
            var uvs = new List<Vector2>(source.Length * 4);
            var triangles = new List<int>(source.Length * 6);
            var half = cellSize * .5f;
            foreach (var position in source)
            {
                if (!CanPresent(layer, position)) continue;
                var center = GridToLocal(position) + Vector3.back * .01f;
                var index = vertices.Count;
                vertices.Add(center + new Vector3(-half, -half));
                vertices.Add(center + new Vector3(-half, half));
                vertices.Add(center + new Vector3(half, half));
                vertices.Add(center + new Vector3(half, -half));
                for (var i = 0; i < 4; i++) colors.Add(color);
                uvs.Add(new Vector2(left, bottom));
                uvs.Add(new Vector2(left, top));
                uvs.Add(new Vector2(right, top));
                uvs.Add(new Vector2(right, bottom));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            batch.Mesh.Clear();
            batch.Mesh.SetVertices(vertices);
            batch.Mesh.SetColors(colors);
            batch.Mesh.SetUVs(0, uvs);
            batch.Mesh.SetTriangles(triangles, 0);
            batch.Mesh.RecalculateBounds();
            batch.Renderer.enabled = vertices.Count > 0;
            return true;
        }

        public void ClearOutline(CombatGridFeedbackLayer layer)
        {
            outlineSources.Remove(layer);
            if (cornerBatches.TryGetValue(layer, out var corners)) corners.Renderer.enabled = false;
            if (softOutlines.TryGetValue(layer, out var batch)) batch.Renderer.enabled = false;
            if (!outlines.TryGetValue(layer, out var renderers)) return;
            foreach (var renderer in renderers)
            {
                if (renderer != null) RecycleSprite(renderer);
            }

            outlines.Remove(layer);
        }

        public void ClearFill(CombatGridFeedbackLayer layer)
        {
            fillSources.Remove(layer);
            if (fillBatches.TryGetValue(layer, out var batch)) batch.Renderer.enabled = false;
            if (!fills.TryGetValue(layer, out var renderers)) return;
            foreach (var renderer in renderers)
            {
                if (renderer != null) RecycleSprite(renderer);
            }

            fills.Remove(layer);
        }

        public void ShowMovementPreview(
            IReadOnlyList<GridPosition> path,
            GridPosition origin,
            int actionPointCost,
            Color color, bool danger = false, System.Func<GridPosition, GridPosition, Color> stepColor = null, bool showCost = true)
        {
            ClearMovementPreview();
            if (path == null || path.Count == 0) return;

            var previous = origin;
            EnsurePixelSprites();
            for (var index = 0; index < path.Count; index++)
            {
                for (var pip = 1; pip <= 4; pip++)
                {
                    var marker = CreateSpriteRenderer("Combat pixel path gem");
                    marker.sprite = pathGemSprite;
                    marker.sortingOrder = pathSortingOrder;
                    marker.color = !danger && stepColor != null ? stepColor(previous, path[index]) : color;
                    marker.transform.localPosition = Vector3.Lerp(GridToLocal(previous), GridToLocal(path[index]), pip / 4f);
                    ScaleSprite(marker.transform, marker.sprite, Vector2.one * cellSize * (5f / 32f));
                    marker.gameObject.SetActive(CanPresent(CombatGridFeedbackLayer.MovementPreview, path[index]));
                    pathMarkers.Add(marker);
                    pathMarkerCells[marker] = path[index];
                }
                previous = path[index];
            }

            var destination = path[path.Count - 1];
            ShowOutline(
                CombatGridFeedbackLayer.MovementPreview,
                new[] { destination },
                color);

            ShowFill(CombatGridFeedbackLayer.MovementPreview, new[] { destination },
                new Color(color.r, color.g, color.b, destinationFillAlpha));

            if (showCost && pathCostFont != null)
            {
                pathCostPosition = destination;
                pathCost = CreatePathCost();
                pathCost.gameObject.SetActive(IsPositionVisible?.Invoke(destination) != false);
                pathCost.text = ActionPointUnits.Format(actionPointCost) + (danger ? " !" : "");
                pathCost.color = color;
                // GridToLocal returns the cell centre. Keep the label inside the
                // destination cell, near its upper-right corner; the old vertical
                // offset placed it above the cell instead.
                pathCost.transform.localPosition = GridToLocal(destination)
                    + new Vector3(cellSize * .30f, cellSize * .30f, 0f)
                    + new Vector3(costLabelOffset.x, 0f, 0f);
                pathCost.transform.localScale = Vector3.one * pathCostWorldScale;
            }
        }

        public void ClearMovementPreview()
        {
            foreach (var marker in pathMarkers)
            {
                if (marker != null) RecycleSprite(marker);
            }

            pathMarkers.Clear();
            pathMarkerCells.Clear();
            ClearOutline(CombatGridFeedbackLayer.MovementPreview);
            ClearFill(CombatGridFeedbackLayer.MovementPreview);
            DestroyObject(ref pathCost);
            pathCostSortingGroup = null;
        }

        public void ClearAll()
        {
            foreach (var layer in new List<CombatGridFeedbackLayer>(outlineSources.Keys)) ClearOutline(layer);
            foreach (var layer in new List<CombatGridFeedbackLayer>(fillSources.Keys)) ClearFill(layer);
            ClearMovementPreview();
        }

        private void ConfigureEdge(
            Transform edgeTransform,
            Sprite edgeSprite,
            CombatGridOutlineEdge edge)
        {
            var horizontal = edge.Direction == CombatGridEdgeDirection.Bottom
                || edge.Direction == CombatGridEdgeDirection.Top;
            var center = GridToLocal(edge.Cell);
            var halfCell = cellSize * .5f;

            switch (edge.Direction)
            {
                case CombatGridEdgeDirection.Bottom: center.y -= halfCell; break;
                case CombatGridEdgeDirection.Top: center.y += halfCell; break;
                case CombatGridEdgeDirection.Left: center.x -= halfCell; break;
                case CombatGridEdgeDirection.Right: center.x += halfCell; break;
            }

            Vector2 spriteSize = edgeSprite != null
                ? new Vector2(edgeSprite.bounds.size.x, edgeSprite.bounds.size.y)
                : Vector2.one;
            var size = horizontal
                ? new Vector2(cellSize, edgeThickness)
                : new Vector2(edgeThickness, cellSize);

            edgeTransform.localPosition = center;
            edgeTransform.localScale = new Vector3(
                size.x / spriteSize.x,
                size.y / spriteSize.y,
                1f);
        }

        private static void ScaleSprite(
            Transform targetTransform,
            Sprite sprite,
            Vector2 desiredSize)
        {
            if (sprite == null) return;
            var spriteSize = sprite.bounds.size;
            targetTransform.localScale = new Vector3(
                desiredSize.x / spriteSize.x,
                desiredSize.y / spriteSize.y,
                1f);
        }

        private void ConfigurePathSegment(
            Transform segmentTransform,
            Sprite segmentSprite,
            GridPosition from,
            GridPosition to)
        {
            var horizontal = from.Y == to.Y;
            var fromLocal = GridToLocal(from);
            var toLocal = GridToLocal(to);
            var size = horizontal
                ? new Vector2(pathSegmentLength, pathSegmentThickness)
                : new Vector2(pathSegmentThickness, pathSegmentLength);

            segmentTransform.localPosition = (fromLocal + toLocal) * .5f;
            ScaleSprite(segmentTransform, segmentSprite, size);
        }

        private Vector3 GridToLocal(GridPosition position)
        {
            return new Vector3(
                gridOrigin.x + position.X * cellSize,
                gridOrigin.y + position.Y * cellSize,
                feedbackZ);
        }

        private readonly Stack<SpriteRenderer> recycledSprites = new();
        private void RecycleSprite(SpriteRenderer renderer)
        {
            renderer.gameObject.SetActive(false);
            recycledSprites.Push(renderer);
        }

        private SpriteRenderer CreateSpriteRenderer(string objectName)
        {
            SpriteRenderer reused = null;
            while (recycledSprites.Count > 0 && reused == null) reused = recycledSprites.Pop();
            var feedbackObject = reused != null ? reused.gameObject : new GameObject(objectName, typeof(SpriteRenderer));
            feedbackObject.name = objectName;
            feedbackObject.SetActive(true);
            feedbackObject.transform.localPosition = Vector3.zero;
            feedbackObject.transform.localRotation = Quaternion.identity;
            feedbackObject.transform.localScale = Vector3.one;
            var root = feedbackRoot != null ? feedbackRoot : transform;
            if (feedbackObject.transform.parent != root) feedbackObject.transform.SetParent(root, false);

            var renderer = reused != null ? reused : feedbackObject.GetComponent<SpriteRenderer>();
            renderer.sprite = feedbackSprite;
            renderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
            renderer.sortingOrder = feedbackSortingOrder;
            return renderer;
        }

        private TMP_Text CreatePathCost()
        {
            var feedbackObject = new GameObject("Combat movement AP cost", typeof(TextMeshPro));
            feedbackObject.transform.SetParent(feedbackRoot != null ? feedbackRoot : transform, false);

            var text = feedbackObject.GetComponent<TextMeshPro>();
            text.font = pathCostFont;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            var meshRenderer = feedbackObject.GetComponent<MeshRenderer>();
            meshRenderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
            meshRenderer.sortingOrder = pathSortingOrder;
            // Text is a world UI overlay, unlike the floor-bound route markers.
            // A high renderer order alone cannot escape the parent's floor group.
            pathCostSortingGroup = feedbackObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            pathCostSortingGroup.sortAtRoot = true;
            pathCostSortingGroup.sortingLayerID = floorSortingGroup != null
                ? floorSortingGroup.sortingLayerID : meshRenderer.sortingLayerID;
            pathCostSortingGroup.sortingOrder = PathCostOverlaySortingOrder;
            return text;
        }

        private static void DestroyObject<T>(ref T objectReference) where T : Component
        {
            if (objectReference != null) Release(objectReference.gameObject);
            objectReference = null;
        }

        private void OnDisable() => ClearAll();
        private void OnDestroy()
        {
            if (floorSortingGroup != null)
            {
                if (createdFloorSortingGroup) Release(floorSortingGroup);
                else if (capturedFloorSortingGroup)
                {
                    floorSortingGroup.sortingLayerID = originalFloorLayer;
                    floorSortingGroup.sortingOrder = originalFloorOrder;
                    floorSortingGroup.enabled = originalFloorEnabled;
                }
            }
            foreach(var sprite in new[]{targetCornerSprite, areaEdgeSprite, pathGemSprite})
                if(sprite != null){Release(sprite.texture);Release(sprite);}
            foreach (var batch in fillBatches.Values.Concat(softOutlines.Values).Concat(cornerBatches.Values))
            {
                if (Application.isPlaying) { Destroy(batch.GameObject); Destroy(batch.Mesh); }
                else { DestroyImmediate(batch.GameObject); DestroyImmediate(batch.Mesh); }
            }
            fillBatches.Clear();
            softOutlines.Clear();
            cornerBatches.Clear();
            Release(cornerMaterial);
            Release(softFillMaterial); Release(softBoundaryMaterial);
            Release(softFillTexture); Release(softBoundaryTexture);
            if (fillBatchMaterial != null)
            {
                if (Application.isPlaying) Destroy(fillBatchMaterial);
                else DestroyImmediate(fillBatchMaterial);
            }
            foreach (var renderer in recycledSprites) if (renderer != null)
            {
                if (Application.isPlaying) Destroy(renderer.gameObject);
                else DestroyImmediate(renderer.gameObject);
            }
            recycledSprites.Clear();
        }

        private void EnsurePixelSprites()
        {
            if (targetCornerSprite != null) return;
            var marks = new bool[32 * 32];
            bool Inside(int x, int y)
            {
                if(x < 2 || x > 29 || y < 2 || y > 29) return false;
                return Mathf.Min(x,31-x) + Mathf.Min(y,31-y) >= 7;
            }
            for(var y=0;y<32;y++)for(var x=0;x<32;x++)
                marks[y*32+x] = (x<12 || x>19) && (y<12 || y>19) && Inside(x,y)
                    && (!Inside(x-1,y) || !Inside(x+1,y) || !Inside(x,y-1) || !Inside(x,y+1));
            var pixels = new Color32[marks.Length];
            for(var y=0;y<32;y++)for(var x=0;x<32;x++)
            {
                if(marks[y*32+x]){pixels[y*32+x] = new Color32(255,255,255,255);continue;}
                for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                    if(x+dx>=0 && x+dx<32 && y+dy>=0 && y+dy<32 && marks[(y+dy)*32+x+dx])
                        pixels[y*32+x] = new Color32(0,0,0,180);
            }
            targetCornerSprite = CreatePixelSprite("Combat pixel target corners",32,32,pixels);
            var edge = new Color32[32*3];
            for(var x=0;x<32;x++){edge[x]=new Color32(0,0,0,150);edge[32+x]=new Color32(255,255,255,255);edge[64+x]=new Color32(0,0,0,150);}
            areaEdgeSprite = CreatePixelSprite("Combat pixel area edge",32,3,edge);
            var gem = new Color32[7*7];
            for(var y=0;y<7;y++)for(var x=0;x<7;x++)
            {
                var distance = Mathf.Abs(x-3)+Mathf.Abs(y-3);
                if(distance==3)gem[y*7+x]=new Color32(0,0,0,220);
                else if(distance<3){var shade=(byte)(y<3?170:255);gem[y*7+x]=new Color32(shade,shade,shade,255);}
            }
            pathGemSprite = CreatePixelSprite("Combat pixel path gem",7,7,gem);
        }

        private static Sprite CreatePixelSprite(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false)
                {name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,false);
            var sprite = Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,32f,0,SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        private void ShowPixelMarkers(CombatGridFeedbackLayer layer, GridPosition[] source, Color tint,
            System.Func<GridPosition,Color> cellTint)
        {
            EnsurePixelSprites();
            var cells=source.Where(p=>CanPresent(layer,p)).Distinct().ToArray();
            var renderers=new List<SpriteRenderer>();
            if(layer is CombatGridFeedbackLayer.SkillTarget or CombatGridFeedbackLayer.ChosenSkillTargets
                or CombatGridFeedbackLayer.MovementPreview or CombatGridFeedbackLayer.ActiveMovementUnit)
            {
                ShowCornerBatch(layer, cells, tint, cellTint);
            }
            else
            {
                var projected=cells.GroupBy(p=>new GridPosition(p.X,p.Y)).ToDictionary(g=>g.Key,g=>g.First());
                foreach(var edge in CombatGridFeedbackGeometry.BuildOutlineEdges(new HashSet<GridPosition>(projected.Keys)))
                {
                    var marker=CreateSpriteRenderer("Combat pixel area edge");marker.sprite=areaEdgeSprite;
                    marker.color=cellTint?.Invoke(projected[edge.Cell]) ?? tint;
                    var center=GridToLocal(edge.Cell);
                    switch(edge.Direction)
                    {
                        case CombatGridEdgeDirection.Top:center.y+=cellSize*.5f;break;
                        case CombatGridEdgeDirection.Bottom:center.y-=cellSize*.5f;break;
                        case CombatGridEdgeDirection.Left:center.x-=cellSize*.5f;break;
                        case CombatGridEdgeDirection.Right:center.x+=cellSize*.5f;break;
                    }
                    marker.transform.localPosition=center;
                    ScaleSprite(marker.transform,marker.sprite,new Vector2(cellSize,cellSize*3f/32f));
                    if(edge.Direction is CombatGridEdgeDirection.Left or CombatGridEdgeDirection.Right)
                        marker.transform.localRotation=Quaternion.Euler(0,0,90);
                    renderers.Add(marker);
                }
            }
            outlines[layer]=renderers;
        }

        // Keep the existing pixel-art texture, but submit one mesh per layer
        // instead of creating/activating a GameObject for every selectable cell.
        private void ShowCornerBatch(CombatGridFeedbackLayer layer, GridPosition[] cells, Color tint,
            System.Func<GridPosition, Color> cellTint)
        {
            if (cells.Length == 0) return;
            if (cornerMaterial == null)
                cornerMaterial = new Material(Shader.Find("Sprites/Default"))
                    { name = "Combat pixel target corners", mainTexture = targetCornerSprite.texture };
            if (!cornerBatches.TryGetValue(layer, out var batch))
            {
                var go = new GameObject("Combat pixel target corner batch", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(feedbackRoot != null ? feedbackRoot : transform, false);
                var mesh = new Mesh { name = go.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                cornerBatches[layer] = batch = new FillBatch
                    { GameObject = go, Mesh = mesh, Renderer = go.GetComponent<MeshRenderer>() };
                batch.Renderer.sharedMaterial = cornerMaterial;
            }
            batch.Renderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
            batch.Renderer.sortingOrder = feedbackSortingOrder;
            if (ResolveExplorationMarker == null && cellTint == null && batch.Cells != null && batch.Cells.SequenceEqual(cells)
                && batch.Tint == tint && batch.Origin == gridOrigin && batch.Size == cellSize && batch.Depth == feedbackZ)
            { batch.Renderer.enabled = true; return; }
            batch.Cells = cellTint == null ? cells : null;
            batch.Tint = tint; batch.Origin = gridOrigin; batch.Size = cellSize; batch.Depth = feedbackZ;
            var vertices = new List<Vector3>(cells.Length * 4);
            var colors = new List<Color>(cells.Length * 4);
            var uv = new List<Vector2>(cells.Length * 4);
            var triangles = new List<int>(cells.Length * 6);
            var half = cellSize * .5f;
            foreach (var cell in cells)
            {
                var center = MarkerToLocal(layer, cell);
                var index = vertices.Count;
                vertices.Add(center + new Vector3(-half, -half)); vertices.Add(center + new Vector3(-half, half));
                vertices.Add(center + new Vector3(half, half)); vertices.Add(center + new Vector3(half, -half));
                var color = cellTint?.Invoke(cell) ?? tint;
                for (var i = 0; i < 4; i++) colors.Add(color);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            batch.Mesh.Clear(); batch.Mesh.SetVertices(vertices); batch.Mesh.SetColors(colors);
            batch.Mesh.SetUVs(0, uv); batch.Mesh.SetTriangles(triangles, 0); batch.Mesh.RecalculateBounds();
            batch.Renderer.enabled = true;
        }

        // A white, tintable tile atlas. Each tile records the eight neighbouring
        // cells, so only the outside of a connected area fades; shared tile edges
        // remain seamless. Padding keeps bilinear sampling inside each sprite.
        private const int SoftTileSize = 32, SoftTileStride = 36, SoftAtlasSize = 16 * SoftTileStride;
        private static readonly Dictionary<int, (Color32[] Fill, Color32[] Boundary)> softTilePixels = new();
        private static readonly float[,] softPixelWeights = new float[1024, 8], softPixelDistances = new float[1024, 8];
        private static readonly float[] softPixelCenters = new float[1024], softPixelGrain = new float[1024];
        private static bool softPixelGeometryReady;
        private void EnsureSoftTextures()
        {
            if (softFillTexture != null) return;
            var clear = new Color32[SoftAtlasSize * SoftAtlasSize];
            softFillTexture = CreateSoftTexture("Combat soft floor fill", clear);
            softBoundaryTexture = CreateSoftTexture("Combat soft floor boundary", clear);
            var shader = Shader.Find("Sprites/Default");
            softFillMaterial = new Material(shader) { name = "Combat soft floor fill", mainTexture = softFillTexture };
            softBoundaryMaterial = new Material(shader) { name = "Combat soft floor boundary", mainTexture = softBoundaryTexture };
            for(var pixel=0;pixel<SoftTileSize;pixel++)
            for(var neighbour=-1;neighbour<=1;neighbour++)
            {
                var coordinate=(pixel+.5f)/SoftTileSize;
                float Cdf(float value)=>1f/(1f+Mathf.Exp(-value*14f));
                softWeights[pixel,neighbour+1]=Cdf(neighbour+1f-coordinate)-Cdf(neighbour-coordinate);
            }
            if (!softPixelGeometryReady)
            {
                for (var py = 0; py < 32; py++) for (var px = 0; px < 32; px++)
                {
                    var i = py * 32 + px;
                    var point = new Vector2((px + .5f) / 32, (py + .5f) / 32);
                    softPixelCenters[i] = softWeights[px, 1] * softWeights[py, 1];
                    softPixelGrain[i] = .94f + .06f * Mathf.Sin(point.x * Mathf.PI * 4f) * Mathf.Sin(point.y * Mathf.PI * 6f);
                    var bit = 0;
                    for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        softPixelWeights[i, bit] = softWeights[px, dx + 1] * softWeights[py, dy + 1];
                        var q = point - new Vector2(dx + .5f, dy + .5f);
                        softPixelDistances[i, bit++] = new Vector2(Mathf.Max(Mathf.Abs(q.x) - .5f, 0),
                            Mathf.Max(Mathf.Abs(q.y) - .5f, 0)).magnitude;
                    }
                }
                softPixelGeometryReady = true;
            }
        }

        private bool EnsureSoftTile(int mask)
        {
            if (softMasks[mask]) return false;
            if (!softTilePixels.TryGetValue(mask, out var pixels))
            {
                var fill = new Color32[SoftTileStride * SoftTileStride];
                var boundary = new Color32[fill.Length];
                for (var y = 0; y < SoftTileStride; y++)
                for (var x = 0; x < SoftTileStride; x++)
                {
                    var px = Mathf.Clamp(x - 2, 0, SoftTileSize - 1);
                    var py = Mathf.Clamp(y - 2, 0, SoftTileSize - 1);
                    var sample = py * 32 + px;
                    var distance = 1f;
                    var coverage = softPixelCenters[sample];
                    for (var bit = 0; bit < 8; bit++)
                    {
                        if ((mask & (1 << bit)) != 0)
                        {
                            coverage += softPixelWeights[sample, bit];
                            continue;
                        }
                        distance = Mathf.Min(distance, softPixelDistances[sample, bit]);
                    }
                    // Blur the union rather than each cell: convex and concave
                    // corners soften without introducing internal tile seams.
                    var fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .93f, coverage));
                    // Periodic, subtle grain: no seams and no opaque paint over the floor.
                    var grain = softPixelGrain[sample];
                    var edge = .20f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.025f, .16f, distance)));
                    var boundaryFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, .75f, coverage));
                    var index = y * SoftTileStride + x;
                    fill[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * fade * grain));
                    boundary[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * boundaryFade * edge * grain));
                }
                softTilePixels.Add(mask, pixels = (fill, boundary));
            }
            var left = (mask % 16) * SoftTileStride;
            var bottom = (mask / 16) * SoftTileStride;
            softFillTexture.SetPixels32(left,bottom,SoftTileStride,SoftTileStride,pixels.Fill);
            softBoundaryTexture.SetPixels32(left,bottom,SoftTileStride,SoftTileStride,pixels.Boundary);
            softMasks[mask] = true;
            return true;
        }

        private static Texture2D CreateSoftTexture(string name, Color32[] pixels)
        {
            var texture = new Texture2D(SoftAtlasSize, SoftAtlasSize, TextureFormat.RGBA32, false)
                { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void ShowSoftFloor(CombatGridFeedbackLayer layer, GridPosition[] source, Color tint,
            System.Func<GridPosition, Color> cellTint, bool boundary)
        {
            var cells = source.Where(p => CanPresent(layer, p)).Distinct().ToArray();
            var batches = boundary ? softOutlines : fillBatches;
            if (cells.Length == 0) return;
            EnsureSoftTextures();
            if (!batches.TryGetValue(layer, out var batch))
            {
                var go = new GameObject(boundary ? "Combat soft floor boundary" : "Combat soft floor fill", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(feedbackRoot != null ? feedbackRoot : transform, false);
                var mesh = new Mesh { name = go.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                batches[layer] = batch = new FillBatch { GameObject = go, Mesh = mesh, Renderer = go.GetComponent<MeshRenderer>() };
            }
            batch.Renderer.sharedMaterial = boundary ? softBoundaryMaterial : softFillMaterial;
            batch.Renderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
            // Floor feedback stays below the characters, including the destination marker.
            batch.Renderer.sortingOrder = feedbackSortingOrder;
            if (cellTint == null && batch.Cells != null && batch.Cells.SequenceEqual(cells)
                && batch.Tint == tint && batch.Origin == gridOrigin && batch.Size == cellSize)
            { batch.Renderer.enabled = true; return; }
            batch.Cells = cellTint == null ? cells : null;
            batch.Tint = tint; batch.Origin = gridOrigin; batch.Size = cellSize;
            var set = new HashSet<GridPosition>(cells);
            var vertices = new List<Vector3>(cells.Length * 4);
            var colors = new List<Color>(cells.Length * 4);
            var uv = new List<Vector2>(cells.Length * 4);
            var triangles = new List<int>(cells.Length * 6);
            var changed = false;
            foreach (var cell in cells)
            {
                var mask = 0; var bit = 0;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (set.Contains(new GridPosition(cell.X + dx, cell.Y + dy, cell.Level))) mask |= 1 << bit;
                    bit++;
                }
                changed |= EnsureSoftTile(mask);
                var left = ((mask % 16) * SoftTileStride + 2f) / SoftAtlasSize;
                var bottom = ((mask / 16) * SoftTileStride + 2f) / SoftAtlasSize;
                var width = (float)SoftTileSize / SoftAtlasSize;
                var center = GridToLocal(cell); var half = cellSize * .5f; var index = vertices.Count;
                vertices.Add(center + new Vector3(-half, -half)); vertices.Add(center + new Vector3(-half, half));
                vertices.Add(center + new Vector3(half, half)); vertices.Add(center + new Vector3(half, -half));
                var color = cellTint?.Invoke(cell) ?? tint;
                for (var i = 0; i < 4; i++) colors.Add(color);
                uv.Add(new Vector2(left, bottom)); uv.Add(new Vector2(left, bottom + width));
                uv.Add(new Vector2(left + width, bottom + width)); uv.Add(new Vector2(left + width, bottom));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            if(changed){softFillTexture.Apply(false,false);softBoundaryTexture.Apply(false,false);}
            batch.Mesh.Clear(); batch.Mesh.SetVertices(vertices); batch.Mesh.SetColors(colors);
            batch.Mesh.SetUVs(0, uv); batch.Mesh.SetTriangles(triangles, 0); batch.Mesh.RecalculateBounds();
            batch.Renderer.enabled = true;
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
