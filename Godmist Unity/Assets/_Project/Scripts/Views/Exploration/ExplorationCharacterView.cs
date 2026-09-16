using Godmist.Runtime.Characters;
using System.Collections.Generic;
using Godmist.Data.Characters;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationCharacterView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Godmist.Views.Combat.CombatUnitVisualStateView combatVisualState;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private GameObject awarenessRoot;
        [SerializeField] private UnityEngine.UI.Image awarenessFill;
        [SerializeField] private Color awarenessLowColor = new(1f, .8f, .1f, 1f);
        [SerializeField] private Color awarenessHighColor = new(1f, .12f, .08f, 1f);
        private bool awarenessVisible;

        public void ShowAwareness(float progress, bool visibleToParty)
        {
            awarenessVisible = visibleToParty && progress > 0f;
            if (awarenessFill != null)
            {
                awarenessFill.fillAmount = Mathf.Clamp01(progress);
                awarenessFill.color = Color.Lerp(awarenessLowColor, awarenessHighColor, awarenessFill.fillAmount);
            }
            RefreshAwarenessVisibility();
        }

        private void RefreshAwarenessVisibility()
        {
            if (awarenessRoot == null) return;
            var visible = awarenessVisible && CombatUnit == null
                && RuntimeCharacter?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                && (terrainView == null || terrainView.IsTraversalVisible(DungeonPosition));
            if (awarenessRoot.activeSelf != visible) awarenessRoot.SetActive(visible);
        }
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D movementCollider;
        [SerializeField] private Color leaderColor = new(0.92f, 0.68f, 0.27f, 1f);
        [SerializeField] private Color followerColor = new(0.35f, 0.75f, 1f, 1f);

        private IReadOnlyList<Sprite> idleAnimationFrames;
        private IReadOnlyList<Sprite> runAnimationFrames;
        private IReadOnlyList<Sprite> activeAnimationFrames;
        private float nextAnimationFrameTime;
        private float secondsPerAnimationFrame;
        private int animationFrameIndex;
        private float lastMovementTime = float.NegativeInfinity;
        private bool noclipEnabled;
        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public float VisionRange { get; private set; } = 10f;
        public float VisionAngle { get; private set; } = 120f;
        public void ConfigureEnemyVision(Godmist.Data.Combat.EncounterEnemyData data)
        {
            VisionRange = data.VisionRange;
            VisionAngle = data.VisionAngle;
            SetFacingDirection(data.InitialFacing.sqrMagnitude > .000001f ? data.InitialFacing : Vector2.right);
        }
        private Godmist.Runtime.Dungeons.DungeonFloorMap terrainMap;
        private Godmist.Views.Dungeons.DungeonFloorMapView terrainView;
        public Godmist.Views.Dungeons.DungeonFloorMapView TerrainView => terrainView;
        private Renderer[] elevationRenderers;
        private void LateUpdate()
        {
            if (nameLabel != null && nameLabel.gameObject.activeSelf) nameLabel.gameObject.SetActive(false);
            RefreshAwarenessVisibility();
            if (terrainView == null) return;
            elevationRenderers ??= GetComponentsInChildren<Renderer>(true);
            var visible = terrainView.IsTraversalVisible(DungeonPosition);
            foreach (var renderer in elevationRenderers) if (renderer != null) renderer.forceRenderingOff = !visible;
        }
        private Vector2 crossingStart, crossingEnd;
        private float crossingElapsed;
        private bool restoreBody, restoreCollider;
        public bool IsCrossingLowWall { get; private set; }
        public System.Func<Vector2Int, bool> CanCrossTo { get; set; }
        public void ConfigureTerrain(Godmist.Runtime.Dungeons.DungeonFloorMap map,
            Godmist.Views.Dungeons.DungeonFloorMapView view,
            Godmist.Runtime.Dungeons.DungeonFloorInstance floor = null, string actorKey = null)
        {
            terrainMap = map; terrainView = view; terrainFloor = floor; traversalActorKey = actorKey;
            RestoreTraversalPosition();
            if (floor != null && !string.IsNullOrWhiteSpace(actorKey) && floor.TryGetActorFacing(actorKey, out var facing)) SetFacingDirection(facing);
        }

        private void EndWallCrossing()
        {
            if (!IsCrossingLowWall) return;
            IsCrossingLowWall = false;
            if (body != null) { body.position = transform.position; body.simulated = restoreBody; }
            if (movementCollider != null) movementCollider.enabled = restoreCollider;
        }

        public string EnemyInstanceId { get; private set; }
        public CharacterInstance RuntimeCharacter { get; private set; }
        public void SetEnemyInstanceId(string value) => EnemyInstanceId = value;
        public Godmist.Runtime.Combat.BattleUnit CombatUnit { get; private set; }
        public void BindCombat(Godmist.Runtime.Combat.BattleUnit unit, Godmist.Runtime.Combat.DungeonCombatContext context = null)
        {
            EndWallCrossing();
            combatContext = context;
            CombatUnit = unit;
            RuntimeCharacter = unit.Character;
            if (body != null) body.simulated = false;
            if (movementCollider != null) movementCollider.enabled = false;
        }
        public void ReleaseCombat()
        {
            var position = DungeonPosition;
            PreferredTraversalDestination = null;
            CombatUnit = null;
            combatContext = null;
            if (UsesLayeredTraversal) StoreTraversalPosition(position);
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            combatVisualState?.ClearHighlight();
            if (body != null) body.position = transform.position;
        }

        public void RefreshCombatPresentation(bool isActive, bool isHovered, bool isTargeting)
        {
            if (CombatUnit == null) { combatVisualState?.ClearHighlight(); return; }
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            combatVisualState?.Refresh(CombatUnit, isActive, isHovered, isTargeting, true);
        }

        public bool IsPointerHit(Vector2 point) => isActiveAndEnabled && bodyRenderer != null
            && bodyRenderer.enabled && !bodyRenderer.forceRenderingOff
            && bodyRenderer.bounds.Contains(new Vector3(point.x, point.y, bodyRenderer.bounds.center.z))
            && IsVisibleForInspection;
        public bool IsVisibleForInspection => isActiveAndEnabled && RuntimeCharacter != null
            && RuntimeCharacter.LifeState != Godmist.Core.Enums.CharacterLifeState.Dead
            && (terrainView == null || (terrainView.IsTraversalVisible(DungeonPosition)
                && terrainView.IsCurrentlySeen(DungeonPosition)));
        public void SetInspectionHighlight(bool value)
        {
            if (CombatUnit == null) combatVisualState?.Refresh(null, false, value, false, true);
        }
        public string CharacterId { get; private set; } = string.Empty;
        public Vector2 Position => transform.position;

        public void Bind(CharacterInstance character)
        {
            if (character == null) return;

            ShowAwareness(0f, false);

            CharacterId = character.CharacterId;
            RuntimeCharacter = character;

            ConfigureVisual(character.Definition);

            if (nameLabel != null)
            {
                nameLabel.text = character.Definition != null ? character.Definition.DisplayName : CharacterId;
            }
        }

        public void Bind(CharacterDefinition definition)
        {
            if (definition == null) return;

            CharacterId = definition.Id;
            ConfigureVisual(definition);

            if (nameLabel != null)
            {
                nameLabel.text = definition.DisplayName;
            }
        }

        public void SetTint(Color tint)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.color = tint;
            }
        }

        public void SetIsActiveLeader(bool isActiveLeader)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.color = isActiveLeader ? leaderColor : followerColor;
            }

            if (IsCrossingLowWall)
            { restoreBody = isActiveLeader && !noclipEnabled; restoreCollider = restoreBody; return; }
            if (body != null) body.simulated = CombatUnit == null && isActiveLeader && !noclipEnabled && !UsesLayeredTraversal;
            if (movementCollider != null) movementCollider.enabled = CombatUnit == null && isActiveLeader && !noclipEnabled && !UsesLayeredTraversal;
        }

        public void SetNoclip(bool enabled)
        {
            noclipEnabled = enabled;

            if (enabled)
            {
                if (body != null) body.simulated = false;
                if (movementCollider != null) movementCollider.enabled = false;
                return;
            }

            if (body != null) body.simulated = movementCollider != null && movementCollider.enabled;
        }

        public void Move(Vector2 offset)
        {
            if (UsesLayeredTraversal && CombatUnit == null && !noclipEnabled)
            { MoveLayeredDirection(offset); return; }
            if (IsCrossingLowWall || offset.sqrMagnitude < 0.0001f) return;
            if (CombatUnit == null && terrainMap != null && terrainView != null && !noclipEnabled)
            {
                var from = terrainView.WorldToCell(Position);
                if (terrainMap.IsDifficultTerrain(from)) offset *= .5f;
                var to = terrainView.WorldToCell(Position + offset);
                if (from.x != to.x && from.y != to.y)
                {
                    // Crossing a corner must not bypass a low edge just because input is diagonal.
                    var horizontal = new Vector2Int(to.x, from.y);
                    var vertical = new Vector2Int(from.x, to.y);
                    foreach (var adjacent in new[] { horizontal, vertical })
                        if (terrainMap.HasWallBetween(from, adjacent)
                            && terrainMap.GetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(from, adjacent)) == Godmist.Core.Enums.DungeonWallType.Low)
                        { to = adjacent; break; }
                }
                if (from != to && terrainMap.HasWallBetween(from, to)
                    && terrainMap.GetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(from, to)) == Godmist.Core.Enums.DungeonWallType.Low
                    && terrainMap.CanMoveBetween(from, to))
                {
                    if (CanCrossTo?.Invoke(to) == false) return;
                    crossingStart = Position;
                    crossingEnd = terrainView.CellToWorldCenter(to);
                    crossingElapsed = 0f;
                    restoreBody = body != null && body.simulated;
                    restoreCollider = movementCollider != null && movementCollider.enabled;
                    if (body != null) body.simulated = false;
                    if (movementCollider != null) movementCollider.enabled = false;
                    IsCrossingLowWall = true;
                    return;
                }
            }

            if (terrainMap == null && !noclipEnabled)
            {
                if (!HasClearMovementSegment(Position, Position + offset))
                {
                    var horizontal = new Vector2(offset.x, 0);
                    var vertical = new Vector2(0, offset.y);
                    offset = HasClearMovementSegment(Position, Position + horizontal) ? horizontal
                        : HasClearMovementSegment(Position, Position + vertical) ? vertical : Vector2.zero;
                }
                if (offset.sqrMagnitude < .000001f) return;
            }
            SetFacingDirection(offset);
            PlayRunAnimation();
            lastMovementTime = Time.time;

            if (body != null && body.simulated)
            {
                body.MovePosition(body.position + offset);
                return;
            }

            transform.position += (Vector3)offset;
        }

        private readonly List<RaycastHit2D> movementHits = new();
        public float MovementRadius => movementCollider is CircleCollider2D circle
            ? circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y)) : .2f;

        public bool HasClearMovementSegment(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var colliderOffset = movementCollider != null ? (Vector2)movementCollider.bounds.center - Position : Vector2.zero;
            if (movementCollider is CircleCollider2D circle) colliderOffset = (Vector2)circle.transform.TransformPoint(circle.offset) - Position;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            movementHits.Clear();
            Physics2D.CircleCast(from + colliderOffset, MovementRadius, delta.sqrMagnitude > 0 ? delta.normalized : Vector2.right,
                filter, movementHits, delta.magnitude);
            foreach (var hit in movementHits)
                if (hit.collider != null && hit.collider.GetComponentInParent<ExplorationCharacterView>() == null) return false;
            return true;
        }

        public void MoveTowards(Vector2 targetPosition, float maximumDistance, float stopDistance)
        {
            var offset = targetPosition - Position;

            if (offset.sqrMagnitude <= stopDistance * stopDistance) return;

            var destination = targetPosition - offset.normalized * stopDistance;
            var nextPosition = Vector2.MoveTowards(Position, destination, maximumDistance);

            Move(nextPosition - Position);
        }

        private void Awake()
        {
            if (bodyRenderer == null || nameLabel == null) return;

            var labelRenderer = nameLabel.GetComponent<MeshRenderer>();

            if (labelRenderer == null) return;

            labelRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            labelRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }

        private void Update()
        {
            if (IsCrossingLowWall && !Godmist.Views.UI.DevConsoleView.IsOpen)
            {
                if (RuntimeCharacter != null && RuntimeCharacter.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive)
                    EndWallCrossing();
                else
                {
                    crossingElapsed += Time.deltaTime;
                    transform.position = Vector2.Lerp(crossingStart, crossingEnd, Mathf.Clamp01(crossingElapsed / .6f));
                    PlayRunAnimation();
                    lastMovementTime = Time.time;
                    if (crossingElapsed >= .6f) { EndWallCrossing(); if (UsesLayeredTraversal) terrainFloor?.SetActorOffset(TraversalActorId, Vector2.zero); }
                }
            }
            if (Time.time - lastMovementTime > 0.08f)
            {
                PlayIdleAnimation();
            }

            if (bodyRenderer == null || activeAnimationFrames == null || activeAnimationFrames.Count < 2) return;
            if (Time.time < nextAnimationFrameTime) return;

            animationFrameIndex = (animationFrameIndex + 1) % activeAnimationFrames.Count;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void ConfigureVisual(CharacterDefinition definition)
        {
            if (bodyRenderer == null || definition == null) return;

            idleAnimationFrames = definition.IdleAnimationFrames;
            runAnimationFrames = definition.RunAnimationFrames;

            if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            {
                idleAnimationFrames = definition.VisualSprite != null
                    ? new[] { definition.VisualSprite }
                    : null;
            }

            PlayAnimation(idleAnimationFrames, definition.IdleAnimationFramesPerSecond);
        }

        private void PlayIdleAnimation()
        {
            if (activeAnimationFrames == idleAnimationFrames) return;

            PlayAnimation(idleAnimationFrames, 4f);
        }

        private void PlayRunAnimation()
        {
            if (runAnimationFrames == null || runAnimationFrames.Count == 0) return;
            if (activeAnimationFrames == runAnimationFrames) return;

            PlayAnimation(runAnimationFrames, 8f);
        }

        private void PlayAnimation(IReadOnlyList<Sprite> frames, float framesPerSecond)
        {
            if (bodyRenderer == null || frames == null || frames.Count == 0) return;

            activeAnimationFrames = frames;
            animationFrameIndex = 0;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            secondsPerAnimationFrame = 1f / Mathf.Max(1f, framesPerSecond);
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        public void FaceTowards(Vector2 worldPosition) => SetFacingDirection(worldPosition - Position);

        private void SetFacingDirection(Vector2 movementDirection)
        {
            if (movementDirection.sqrMagnitude > .000001f)
            {
                FacingDirection = movementDirection.normalized;
                terrainFloor?.TrySetActorFacing(TraversalActorId, FacingDirection);
            }
            if (bodyRenderer == null || Mathf.Abs(movementDirection.x) < 0.0001f) return;

            bodyRenderer.transform.localRotation = movementDirection.x > 0f
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(180f, 0f, 180f);
        }

        public void SetPosition(Vector2 position)
        {
            PreferredTraversalDestination = null;
            if (UsesLayeredTraversal && CombatUnit == null)
            {
                var surface = new Godmist.Runtime.Dungeons.DungeonTraversalPosition(
                    new Godmist.Runtime.Dungeons.DungeonSurfacePosition(terrainView.WorldToCell(position)));
                if (terrainMap.TryGetTraversalCell(surface, out _)) StoreTraversalPosition(surface);
                else if (terrainView.TryGetVisibleTraversal(position, out var visible)) StoreTraversalPosition(visible);
                else if (!noclipEnabled) return;
            }
            EndWallCrossing();
            lastMovementTime = float.NegativeInfinity;
            PlayIdleAnimation();
            transform.position = position;

            if (body != null)
            {
                body.position = position;
            }
        }
    }
}
