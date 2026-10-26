using System.Collections.Generic;
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
            if (elevationRenderers == null) return;
            var remembered = elevationView?.FogFloor != null && body != null && elevationView.FogFloor.Fog.Bodies.Contains(body.InstanceId)
                && elevationView.Knowledge(BodyPosition) != DungeonKnowledge.Unexplored
                && !elevationView.IsSurfaceOccluded(BodyPosition.IsOnStairs ? BodyPosition.Stair.First : BodyPosition.Surface);
            foreach (var renderer in elevationRenderers) if (renderer != null) renderer.forceRenderingOff = !(IsElevationVisible || remembered);
            if (bodyRenderer != null)
            {
                fogBodyColor ??= bodyRenderer.color;
                if (!IsElevationVisible && elevationView?.UsesVegetationPresentation != true)
                    bodyRenderer.color = DungeonFloorMapView.Dim(fogBodyColor.Value);
                else if (!isPointerHovered) bodyRenderer.color = fogBodyColor.Value;
            }
        }

        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private SpriteRenderer bodyRenderer;

        private readonly List<SpriteRenderer> outlineRenderers = new();

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private DungeonEnemyBodyInstance body;
        private DungeonLootWindowView lootWindowPrefab;
        private Tilemap floorTilemap;
        private Material outlineMaterial;
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

            RefreshHoverVisual();

            var pointerOverBody = IsPointerOverThisBody();

            if (pointerOverBody != isPointerHovered)
            {
                isPointerHovered = pointerOverBody;
                RefreshHoverVisual();
            }

            if (!pointerOverBody
                || !Input.GetMouseButtonDown(0)
                || UIWorkspaceInputRouter.IsPointerOverUi()
                || DevConsoleView.IsOpen
                || !CanInteract())
            {
                return;
            }

            Interact();
        }

        private void OnEnable()
        {
            DungeonLootWindowView.LootClaimed += HandleLootClaimed;
        }

        private void OnDisable()
        {
            DungeonLootWindowView.LootClaimed -= HandleLootClaimed;
        }

        public bool CanInteract()
        {
            if (!IsElevationVisible) return false;
            if (floor?.Map?.HasElevatedSurfaces == true && partyController?.CanInteractAt(BodyPosition) != true) return false;
            var session = GameBootstrap.Instance?.Session;

            if (body == null
                || floor == null
                || partyController?.ActiveLeaderTransform == null
                || session == null
                || session.CurrentMode != GameModeState.Exploration
                || session.ActiveCombat != null
                || !floor.TryGetEnemyBody(body.InstanceId, out var currentBody))
            {
                return false;
            }

            body = currentBody;

            var offset = partyController.ActiveLeaderTransform.position
                         - transform.position;

            if (floor.Map.HasElevatedSurfaces) return offset.sqrMagnitude <= interactionRadius * interactionRadius && partyController.CanInteractAt(BodyPosition);
            return offset.sqrMagnitude
                   <= interactionRadius * interactionRadius
                   && floorTilemap != null
                   && DungeonExplorationInteractionUtility.HasDirectAccess(
                       floor,
                       ToDungeonCell(
                           floorTilemap.WorldToCell(
                               partyController.ActiveLeaderTransform.position)),
                       body.Position);
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

        private bool IsPointerOverThisBody()
        {
            if (!IsElevationVisible) return false;
            if (floor?.Map?.HasElevatedSurfaces == true && partyController?.CanInteractAt(BodyPosition) != true) return false;
            var worldCamera = Camera.main;

            if (worldCamera == null)
            {
                return false;
            }

            var pointerWorldPosition = worldCamera.ScreenToWorldPoint(
                Input.mousePosition);

            foreach (var hit in Physics2D.OverlapPointAll(pointerWorldPosition))
            {
                if (hit != null
                    && hit.GetComponentInParent<DungeonEnemyBodyView>() == this)
                {
                    return true;
                }
            }

            return false;
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            var session = GameBootstrap.Instance?.Session;
            DungeonLootWindowView.OpenOrReuse(session, body.InstanceId, lootWindowPrefab);
        }

        private void OnDestroy()
        {
            elevationView?.UnregisterLightingPresentation(bodyRenderer);
            if (outlineMaterial != null)
            {
                if (Application.isPlaying) Destroy(outlineMaterial); else DestroyImmediate(outlineMaterial);
            }
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
            RebuildOutline();
            RefreshGroundSorting();
            ConfigureInteractionCollider();
            RefreshHoverVisual();
        }

        private void RefreshGroundSorting()
        {
            if (elevationView == null || bodyRenderer == null) return;
            bodyRenderer.sortingLayerID = elevationView.FloorFeedbackSortingLayerID;
            bodyRenderer.sortingOrder = elevationView.GetDungeonObjectDepthSortingOrder();
            foreach (var outline in outlineRenderers)
                if (outline != null)
                {
                    outline.sortingLayerID = bodyRenderer.sortingLayerID;
                    outline.sortingOrder = bodyRenderer.sortingOrder - 1;
                }
        }

        private void RebuildOutline()
        {
            foreach (var outlineRenderer in outlineRenderers)
            {
                if (outlineRenderer != null)
                {
                    if (Application.isPlaying) Destroy(outlineRenderer.gameObject); else DestroyImmediate(outlineRenderer.gameObject);
                }
            }

            outlineRenderers.Clear();

            if (bodyRenderer?.sprite == null)
            {
                return;
            }

            var shader = Shader.Find("Godmist/Combat/Solid Sprite Silhouette");
            if (shader == null)
            {
                Debug.LogError("DungeonEnemyBodyView: nie znaleziono shadera obramowki.");
                return;
            }

            if (outlineMaterial != null)
            {
                if (Application.isPlaying) Destroy(outlineMaterial); else DestroyImmediate(outlineMaterial);
            }

            outlineMaterial = new Material(shader);
            var offsets = new[]
            {
                Vector2.left, Vector2.right, Vector2.up, Vector2.down,
                new Vector2(-1f, -1f), new Vector2(-1f, 1f),
                new Vector2(1f, -1f), new Vector2(1f, 1f)
            };

            foreach (var offset in offsets)
            {
                var outlineObject = new GameObject(
                    "Enemy Body Outline",
                    typeof(SpriteRenderer));
                outlineObject.transform.SetParent(bodyRenderer.transform, false);
                outlineObject.transform.localPosition = offset * 0.045f;

                var outlineRenderer = outlineObject.GetComponent<SpriteRenderer>();
                outlineRenderer.sprite = bodyRenderer.sprite;
                outlineRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                outlineRenderer.sortingOrder = bodyRenderer.sortingOrder - 1;
                outlineRenderer.sharedMaterial = outlineMaterial;
                outlineRenderers.Add(outlineRenderer);
            }
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
            var showOutline = isPointerHovered
                && body is { IsEmpty: false }
                && !DevConsoleView.IsOpen
                && CanInteract();

            foreach (var outlineRenderer in outlineRenderers)
            {
                if (outlineRenderer != null)
                {
                    outlineRenderer.enabled = showOutline;
                }
            }

            if (outlineMaterial != null)
            {
                outlineMaterial.SetColor(
                    "_OutlineColor",
                    showOutline
                        ? new Color(1f, 0.76f, 0.23f, 1f)
                        : Color.clear);
            }
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
