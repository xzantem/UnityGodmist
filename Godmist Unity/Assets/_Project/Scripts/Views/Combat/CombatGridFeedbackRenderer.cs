using System.Collections.Generic;
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
        MovementPreview
    }

    public class CombatGridFeedbackRenderer : MonoBehaviour
    {
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

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField, Min(0.001f)] private float edgeThickness = .045f;
        [SerializeField, Min(0.01f)] private float pathSegmentLength = 1f;
        [SerializeField, Min(0.001f)] private float pathSegmentThickness = .065f;
        [SerializeField] private float feedbackZ = -.2f;
        [SerializeField] private Vector2 costLabelOffset = new(0f, .22f);
        [SerializeField, Min(0.001f)] private float pathCostWorldScale = .12f;

        private readonly Dictionary<CombatGridFeedbackLayer, List<SpriteRenderer>> outlines = new();
        private readonly Dictionary<CombatGridFeedbackLayer, List<SpriteRenderer>> fills = new();
        private readonly List<SpriteRenderer> pathMarkers = new();
        private TMP_Text pathCost;

        private void Awake()
        {
            if (feedbackRoot == null) feedbackRoot = transform;
            if (pathCostFont == null) pathCostFont = TMP_Settings.defaultFontAsset;
        }

        public void ShowOutline(
            CombatGridFeedbackLayer layer,
            IEnumerable<GridPosition> positions,
            Color color)
        {
            ClearOutline(layer);
            if (feedbackSprite == null) return;

            var renderers = new List<SpriteRenderer>();
            foreach (var edge in CombatGridFeedbackGeometry.BuildOutlineEdges(positions))
            {
                var renderer = CreateSpriteRenderer("Combat feedback edge");
                renderer.color = color;
                ConfigureEdge(renderer.transform, renderer.sprite, edge);
                renderers.Add(renderer);
            }

            outlines[layer] = renderers;
        }

        public void ShowFill(
            CombatGridFeedbackLayer layer,
            IEnumerable<GridPosition> positions,
            Color color)
        {
            ClearFill(layer);
            if (feedbackSprite == null || positions == null) return;

            var renderers = new List<SpriteRenderer>();
            foreach (var position in positions)
            {
                var renderer = CreateSpriteRenderer("Combat feedback fill");
                renderer.color = color;
                renderer.transform.localPosition = GridToLocal(position) + Vector3.back * .01f;
                ScaleSprite(renderer.transform, renderer.sprite, Vector2.one * cellSize);
                renderers.Add(renderer);
            }

            fills[layer] = renderers;
        }

        public void ClearOutline(CombatGridFeedbackLayer layer)
        {
            if (!outlines.TryGetValue(layer, out var renderers)) return;
            foreach (var renderer in renderers)
            {
                if (renderer != null) Destroy(renderer.gameObject);
            }

            outlines.Remove(layer);
        }

        public void ClearFill(CombatGridFeedbackLayer layer)
        {
            if (!fills.TryGetValue(layer, out var renderers)) return;
            foreach (var renderer in renderers)
            {
                if (renderer != null) Destroy(renderer.gameObject);
            }

            fills.Remove(layer);
        }

        public void ShowMovementPreview(
            IReadOnlyList<GridPosition> path,
            GridPosition origin,
            int actionPointCost,
            Color color)
        {
            ClearMovementPreview();
            if (path == null || path.Count == 0) return;

            var previous = origin;
            for (var index = 0; index < path.Count; index++)
            {
                if (feedbackSprite == null) break;
                var marker = CreateSpriteRenderer("Combat movement path");
                marker.color = color;
                ConfigurePathSegment(marker.transform, marker.sprite, previous, path[index]);
                pathMarkers.Add(marker);
                previous = path[index];
            }

            var destination = path[path.Count - 1];
            ShowOutline(
                CombatGridFeedbackLayer.MovementPreview,
                new[] { destination },
                color);

            if (pathCostFont != null)
            {
                pathCost = CreatePathCost();
                pathCost.text = actionPointCost.ToString();
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
                if (marker != null) Destroy(marker.gameObject);
            }

            pathMarkers.Clear();
            ClearOutline(CombatGridFeedbackLayer.MovementPreview);
            DestroyObject(ref pathCost);
        }

        public void ClearAll()
        {
            foreach (var layer in new List<CombatGridFeedbackLayer>(outlines.Keys)) ClearOutline(layer);
            foreach (var layer in new List<CombatGridFeedbackLayer>(fills.Keys)) ClearFill(layer);
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

        private SpriteRenderer CreateSpriteRenderer(string objectName)
        {
            var feedbackObject = new GameObject(objectName, typeof(SpriteRenderer));
            feedbackObject.transform.SetParent(feedbackRoot, false);

            var renderer = feedbackObject.GetComponent<SpriteRenderer>();
            renderer.sprite = feedbackSprite;
            renderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
            renderer.sortingOrder = feedbackSortingOrder;
            return renderer;
        }

        private TMP_Text CreatePathCost()
        {
            var feedbackObject = new GameObject("Combat movement AP cost", typeof(TextMeshPro));
            feedbackObject.transform.SetParent(feedbackRoot, false);

            var text = feedbackObject.GetComponent<TextMeshPro>();
            text.font = pathCostFont;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            var meshRenderer = feedbackObject.GetComponent<MeshRenderer>();
            meshRenderer.sortingLayerID = SortingLayer.NameToID(feedbackSortingLayer);
            meshRenderer.sortingOrder = feedbackSortingOrder;
            return text;
        }

        private static void DestroyObject<T>(ref T objectReference) where T : Component
        {
            if (objectReference != null) Destroy(objectReference.gameObject);
            objectReference = null;
        }

        private void OnDisable() => ClearAll();
    }
}
