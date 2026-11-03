using Godmist.Views.Combat;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Characters;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonEnemyBodyView : MonoBehaviour
    {
        private DungeonFloorMapView elevationView;
        private Renderer[] elevationRenderers;
        private Color? fogBodyColor;
        public bool IsElevationVisible => elevationView == null
            || elevationView.IsTraversalVisible(BodyPosition);
        private string bodyActorKey, bodyActorInstanceId;
        private DungeonTraversalPosition BodyPosition
        {
            get
            {
                if (body != null && bodyActorInstanceId != body.InstanceId)
                { bodyActorInstanceId = body.InstanceId; bodyActorKey = "body:" + bodyActorInstanceId; }
                return floor != null && body != null && floor.TryGetActorPosition(bodyActorKey, out var position)
                    ? position : new DungeonTraversalPosition(new DungeonSurfacePosition(body?.Position ?? Vector2Int.zero));
            }
        }
        public void BindElevationView(DungeonFloorMapView view)
        {
            elevationView?.UnregisterLightingPresentation(bodyRenderer);
            elevationView = view;
            elevationRenderers = GetComponentsInChildren<Renderer>(true);
            // A corpse is a ground prop, including its lighting. Reserve the
            // existing outline lane so hovering cannot put it above actors.
            elevationView?.RegisterGroundLightingPresentation(bodyRenderer, true, preserveDepthSorting: true);
            RefreshGroundSorting();
        }
        private static readonly Unity.Profiling.ProfilerMarker BodyLateSample = new("Godmist.Dungeon.BodyLate");
        private void LateUpdate()
        {
            using var sample = BodyLateSample.Auto();
            RefreshGroundSorting();
            var remembered = elevationView?.FogFloor != null && body != null && elevationView.FogFloor.Fog.Bodies.Contains(body.InstanceId)
                && elevationView.Knowledge(BodyPosition) != DungeonKnowledge.Unexplored
                && !elevationView.IsSurfaceOccluded(BodyPosition.IsOnStairs ? BodyPosition.Stair.First : BodyPosition.Surface);
            if (elevationRenderers != null)
                foreach (var renderer in elevationRenderers) if (renderer != null) renderer.forceRenderingOff = !(IsElevationVisible || remembered);
            if (bodyRenderer != null)
            {
                fogBodyColor ??= bodyRenderer.color;
                if (!IsElevationVisible && elevationView?.UsesVegetationPresentation != true)
                    bodyRenderer.color = DungeonFloorMapView.Dim(fogBodyColor.Value);
                else bodyRenderer.color = fogBodyColor.Value;
            }
            RefreshHoverVisual();
        }

        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private SpriteRenderer bodyRenderer;

        private CombatUnitVisualStateView hoverOutline;

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private DungeonEnemyBodyInstance body;
        private DungeonLootWindowView lootWindowPrefab;
        private Tilemap floorTilemap;
        private bool isPointerHovered;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            DungeonEnemyBodyInstance nextBody,
            Vector3 worldPosition,
            DungeonLootWindowView nextLootWindowPrefab = null,
            Tilemap nextFloorTilemap = null)
        {
            if (nextFloor == null
                || nextBody == null
                || !nextFloor.TryGetEnemyBody(
                    nextBody.InstanceId,
                    out var currentBody))
            {
                return;
            }

            partyController = nextPartyController;
            floor = nextFloor;
            body = currentBody;
            lootWindowPrefab = nextLootWindowPrefab;
            floorTilemap = nextFloorTilemap;
            transform.position = worldPosition;
            ConfigureVisual();
        }

        private void Update()
        {
            if (body != null
                && floor != null
                && !floor.TryGetEnemyBody(body.InstanceId, out _))
            {
                Destroy(gameObject);
                return;
            }

            isPointerHovered = partyController != null
                && partyController.TryGetPointerWorldPosition(out var point)
                && FindPointerBody(point, partyController) == this;
        }

        private void OnEnable()
        {
            DungeonLootWindowView.LootClaimed += HandleLootClaimed;
        }

        private void OnDisable()
        {
            DungeonLootWindowView.LootClaimed -= HandleLootClaimed;
            hoverOutline?.ClearHighlight();
        }

        public bool CanInteract()
        {
            var session = partyController?.Session;

            if (!CanRequestLoot
                || partyController?.ActiveLeaderTransform == null
                || session == null
                || session.CurrentMode != GameModeState.Exploration
                || session.ActiveCombat != null
                || !floor.TryGetEnemyBody(body.InstanceId, out var currentBody))
            {
                return false;
            }

            body = currentBody;

            return (floor.Map.HasElevatedSurfaces && partyController.CanInteractAt(BodyPosition)
                    && IsWithinLootRadius(partyController.ActiveLeaderTransform.position))
                || (!floor.Map.HasElevatedSurfaces && CanLootFrom(partyController.ActiveLeaderTransform.position));
        }

        public bool CanRequestLoot => isActiveAndEnabled && IsElevationVisible
            && body is { IsEmpty: false } && floor?.Map != null && floor.TryGetEnemyBody(body.InstanceId, out _);
        public float InteractionRadius => interactionRadius;

        private bool IsWithinLootRadius(Vector2 point)
            => (point - (Vector2)transform.position).sqrMagnitude <= interactionRadius * interactionRadius;

        public bool CanLootFrom(Vector2 point)
        {
            if (!IsWithinLootRadius(point)) return false;
            if (floor.Map.HasElevatedSurfaces)
                return elevationView != null && elevationView.TryGetVisibleTraversal(point, out var position)
                    && (position == BodyPosition || floor.Map.GetTraversalNeighbours(position).Contains(BodyPosition));
            return floorTilemap != null && DungeonExplorationInteractionUtility.HasDirectAccess(
                floor, ToDungeonCell(floorTilemap.WorldToCell(point)), body.Position);
        }

        private static Vector2Int ToDungeonCell(Vector3Int cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public static bool IsPointerOverBody(Vector2 pointerWorldPosition)
        {
            foreach (var hit in Physics2D.OverlapPointAll(pointerWorldPosition))
            {
                if (hit != null
                    && hit.GetComponentInParent<DungeonEnemyBodyView>() is { IsElevationVisible: true })
                {
                    return true;
                }
            }

            return false;
        }

        public static DungeonEnemyBodyView FindPointerBody(Vector2 point, ExplorationPartyController controller)
        {
            foreach (var hit in Physics2D.OverlapPointAll(point))
            {
                if (hit != null
                    && hit.GetComponentInParent<DungeonEnemyBodyView>() is { CanRequestLoot: true } candidate
                    && candidate.partyController == controller)
                    return candidate;
            }

            return null;
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            var session = partyController.Session;
            DungeonLootWindowView.OpenOrReuse(session, body.InstanceId, lootWindowPrefab);
        }

        private void OnDestroy()
        {
            elevationView?.UnregisterLightingPresentation(bodyRenderer);
        }

        private void HandleLootClaimed(string sourceBodyInstanceId, string message)
        {
            if (body == null || body.InstanceId != sourceBodyInstanceId) return;

            ShowFloatingText(message);
            RefreshOrDestroy();
        }

        private void RefreshOrDestroy()
        {
            if (floor == null
                || !floor.TryGetEnemyBody(body.InstanceId, out var currentBody))
            {
                Destroy(gameObject);
                return;
            }

            body = currentBody;

        }

        private void ConfigureVisual()
        {
            bodyRenderer ??= GetComponentInChildren<SpriteRenderer>(true);

            if (bodyRenderer == null)
            {
                Debug.LogError("DungeonEnemyBodyView: brak SpriteRenderer ciala.");
                return;
            }

            bodyRenderer.sortingOrder = 9; // Unbound legacy/sandbox presentation only.
            var hasCorpseSprite = false;

            var definition = ResolveEnemyDefinition();
            if (definition == null)
            {
                Debug.LogError(
                    $"DungeonEnemyBodyView: nie znaleziono definicji przeciwnika "
                    + $"dla ciala '{body?.InstanceId}'.");
            }
            else
            {
                var sprite = definition.DeadSprite;
                hasCorpseSprite = sprite != null
                    || CharacterAnimationResolver.TryResolveCorpse(definition.Id, out sprite);
                if (!hasCorpseSprite)
                {
                    // Characters can have animation frames without a standalone VisualSprite.
                    if (CharacterAnimationResolver.TryResolveCharacter(definition.Id, Vector2.down,
                        CharacterAnimationState.Idle, out var idle))
                        sprite = idle.Frames[0];
                    if (sprite == null && definition.IdleAnimationFrames != null)
                        sprite = definition.IdleAnimationFrames.Find(frame => frame != null);
                    sprite ??= definition.VisualSprite;
                }
                bodyRenderer.sprite = sprite;
            }

            bodyRenderer.color = hasCorpseSprite ? Color.white : new Color(0.48f, 0.09f, 0.09f, 1f);
            fogBodyColor = bodyRenderer.color;
            transform.localRotation = hasCorpseSprite ? Quaternion.identity : Quaternion.Euler(0f, 0f, -90f);
            if (hasCorpseSprite && bodyRenderer.sprite != null && Mathf.Approximately(bodyRenderer.sprite.pixelsPerUnit, 32f))
                transform.localScale = new Vector3(.7f, .7f, 1f);
            RefreshGroundSorting();
            ConfigureInteractionCollider();
            RefreshHoverVisual();
        }

        private void RefreshGroundSorting()
        {
            if (elevationView == null || bodyRenderer == null) return;
            bodyRenderer.sortingLayerID = elevationView.FloorFeedbackSortingLayerID;
            bodyRenderer.sortingOrder = elevationView.GetDungeonObjectDepthSortingOrder();
        }

        private void ConfigureInteractionCollider()
        {
            var collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            if (collider == null)
            {
                Debug.LogError(
                    "DungeonEnemyBodyView: nie udalo sie dodac CircleCollider2D.");
                return;
            }

            collider.isTrigger = true;

            var radius = 0.35f;

            if (bodyRenderer?.sprite != null)
            {
                var extents = bodyRenderer.sprite.bounds.extents;
                radius = Mathf.Max(radius, extents.x, extents.y);
            }

            collider.radius = radius;
        }

        private void RefreshHoverVisual()
        {
            if (!isPointerHovered || !IsElevationVisible || body is not { IsEmpty: false }
                || bodyRenderer == null || UIWorkspaceInputRouter.IsPointerOverUi() || DevConsoleView.IsOpen)
            {
                hoverOutline?.ClearHighlight();
                return;
            }

            hoverOutline ??= bodyRenderer.GetComponent<CombatUnitVisualStateView>()
                ?? bodyRenderer.gameObject.AddComponent<CombatUnitVisualStateView>();
            hoverOutline.Refresh(null, false, true, false, preserveBodyColors: true);
        }

        private void ShowFloatingText(string text)
        {
            var feedbackObject = new GameObject(
                "Dungeon Loot Feedback",
                typeof(TextMeshPro),
                typeof(DungeonLootFloatingText));
            feedbackObject.transform.position = transform.position + Vector3.up * 0.6f;

            var feedback = feedbackObject.GetComponent<DungeonLootFloatingText>();
            feedback.Initialize(text);
        }

        private CharacterDefinition ResolveEnemyDefinition()
        {
            if (floor == null || body == null)
            {
                return null;
            }

            foreach (var encounter in floor.Encounters)
            {
                if (encounter?.Enemies == null)
                {
                    continue;
                }

                foreach (var enemy in encounter.Enemies)
                {
                    if (enemy != null
                        && enemy.InstanceId == body.EnemyInstanceId)
                    {
                        return enemy.CharacterDefinition;
                    }
                }
            }

            return null;
        }

        public static DungeonEnemyBodyView CreateFallback(
            Transform parent)
        {
            var gameObject = new GameObject("EnemyBody");
            gameObject.transform.SetParent(parent, false);

            var view = gameObject.AddComponent<DungeonEnemyBodyView>();
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 9;
            gameObject.transform.localScale = Vector3.one * 0.7f;

            return view;
        }
    }

    public class DungeonLootFloatingText : MonoBehaviour
    {
        private TextMeshPro label;
        private Material feedbackMaterial;
        private float startedAt;

        public void Initialize(string text)
        {
            label = GetComponent<TextMeshPro>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = 6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.88f, 0.78f, 0.3f, 1f);
            ReleaseMaterial();
            var sourceMaterial = label.font?.material;
            if (sourceMaterial != null)
            {
                feedbackMaterial = new Material(sourceMaterial) { hideFlags = HideFlags.HideAndDontSave };
                feedbackMaterial.EnableKeyword("OUTLINE_ON");
                feedbackMaterial.SetColor("_OutlineColor", new Color(0.06f, 0.03f, 0.02f, 1f));
                feedbackMaterial.SetFloat("_OutlineWidth", .22f);
                label.fontSharedMaterial = feedbackMaterial;
            }
            label.GetComponent<MeshRenderer>().sortingOrder = 12;
            transform.localScale = Vector3.one * 0.32f;
            startedAt = Time.unscaledTime;
        }

        private void OnDestroy() => ReleaseMaterial();

        private void ReleaseMaterial()
        {
            if (feedbackMaterial == null) return;
            if (Application.isPlaying) Destroy(feedbackMaterial); else DestroyImmediate(feedbackMaterial);
            feedbackMaterial = null;
        }

        private void Update()
        {
            var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / 1.1f);
            transform.position += Vector3.up * (Time.unscaledDeltaTime * 0.55f);

            if (label != null)
            {
                var color = label.color;
                color.a = progress < 0.55f
                    ? 1f
                    : 1f - (progress - 0.55f) / 0.45f;
                label.color = color;
            }

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
