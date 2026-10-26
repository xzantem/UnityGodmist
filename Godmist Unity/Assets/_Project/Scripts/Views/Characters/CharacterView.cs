using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Characters
{
    public class CharacterView : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        public SpriteRenderer BodyRenderer => bodyRenderer;
        private Godmist.Views.UI.CombatDownedIndicatorView downedIndicator;
        public void RefreshDownedIndicator(Godmist.Views.UI.Workspace.UIWorkspaceStylePalette palette)
            => Godmist.Views.UI.CombatDownedIndicatorView.UpdateWorld(ref downedIndicator, bodyRenderer, BoundUnit, palette);
        [SerializeField] private TextMeshPro nameLabel;

        [Header("Colors")]
        [SerializeField] private Color playerSideColor = new(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color enemySideColor = new(1f, 0.4f, 0.4f, 1f);

        private Renderer[] elevationRenderers;
        private bool elevationVisible = true;
        public void SetElevationVisible(bool visible)
        {
            elevationVisible = visible;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            var visible = elevationVisible && !Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(BoundUnit);
            elevationRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in elevationRenderers) if (renderer != null) renderer.forceRenderingOff = !visible;
        }

        [Header("Animation")]
        [SerializeField] private float movementSpeed = 6f;
        [SerializeField] private float hitReactionDuration = 0.15f;

        private IReadOnlyList<Sprite> idleAnimationFrames;
        private IReadOnlyList<Sprite> runAnimationFrames;
        private IReadOnlyList<Sprite> activeAnimationFrames;
        private CharacterAnimationClip idleImportedAnimation;
        private CharacterAnimationClip runImportedAnimation;
        private CharacterAnimationClip activeImportedAnimation;
        private Vector2 facingDirection = Vector2.right;
        private int animationDirectionIndex = -1;
        private Sprite hitSprite;
        private float nextAnimationFrameTime;
        private float secondsPerAnimationFrame;
        private float hitReactionEndTime;
        private bool hitReactionVisible;
        private int animationFrameIndex;
        private int lastKnownHealth;
        private Vector3 targetWorldPosition;
        private bool hasTargetWorldPosition;
        private CombatPosePlayer combatPose;
        private CharacterLifeState displayedLifeState;
        private bool hasDisplayedLifeState;
        public bool HasCombatPose => combatPose?.IsPlaying == true;
        public Vector2 FacingDirection => facingDirection;

        public void PlayCombatPose(CombatPose pose, Vector2 facing, float duration)
        {
            if (transformation.IsPlaying) return;
            SyncLifeStatePose();
            if (BoundUnit?.Character != null && BoundUnit.Character.LifeState != CharacterLifeState.Alive
                && !(BoundUnit.Character.LifeState == CharacterLifeState.Dead && pose?.Action == "hit")) return;
            SetFacingDirection(facing);
            PlayIdleAnimation();
            combatPose ??= new CombatPosePlayer(bodyRenderer);
            combatPose.Play(pose, duration);
        }

        public void PlayHelpUpPose(Vector2 facing, float duration)
        {
            if (BoundUnit?.Character?.Definition == null) return;
            if (!CombatPoseResolver.TryResolveHelpUp(BoundUnit.Character.Definition.Id, facing, out var pose)) return;
            PlayCombatPose(pose, facing, duration);
        }

        private void SyncLifeStatePose()
        {
            var state = BoundUnit?.Character?.LifeState ?? CharacterLifeState.Alive;
            if (state == CharacterLifeState.Downed && bodyRenderer != null)
                bodyRenderer.transform.localRotation = Quaternion.identity;
            if (hasDisplayedLifeState && displayedLifeState == state) return;
            displayedLifeState = state;
            hasDisplayedLifeState = true;
            if (state == CharacterLifeState.Dead && combatPose?.IsHitReaction == true) return;
            combatPose?.Clear();
            if (state != CharacterLifeState.Downed || BoundUnit?.Character?.Definition == null) return;
            if (!CombatPoseResolver.TryResolveDowned(BoundUnit.Character.Definition.Id, out var pose)) return;
            combatPose ??= new CombatPosePlayer(bodyRenderer);
            combatPose.Play(pose, float.PositiveInfinity);
        }

        public bool IsTransforming => transformation.IsPlaying;
        public void PlayTransformation(IReadOnlyList<Sprite> frames, float fps)
        {
            combatPose?.Clear();ConfigureVisual();transformation.Play(frames,fps);
            if (frames != null && frames.Count>0 && bodyRenderer != null) bodyRenderer.transform.localRotation=Quaternion.identity;
            transformation.Tick(bodyRenderer);
        }
        public void ClearCombatPose()
        {
            combatPose?.Clear();
            hasDisplayedLifeState = false;
            SyncLifeStatePose();
        }
        private CombatStatusVfxPlayer statusVfx;
        private Godmist.Views.Dungeons.DungeonFloorMapView terrainView;
        private Godmist.Runtime.Dungeons.DungeonSurfaceEdge? crossingDepthEdge;
        private UnityEngine.Rendering.SortingGroup terrainDepthGroup;
        private bool createdTerrainDepthGroup, capturedTerrainDepthGroup, originalTerrainDepthEnabled;
        private int originalTerrainDepthLayer, originalTerrainDepthOrder;
        public void BindTerrainView(Godmist.Views.Dungeons.DungeonFloorMapView view)
        {
            crossingDepthEdge = null;
            terrainView = view;
            if (view == null) RestoreTerrainDepth();
        }
        private void RefreshTerrainDepth()
        {
            if (terrainDepthGroup == null)
            {
                terrainDepthGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
                capturedTerrainDepthGroup = false; createdTerrainDepthGroup = false;
            }
            if (terrainDepthGroup == null)
            { terrainDepthGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>(); createdTerrainDepthGroup = true; }
            if (!capturedTerrainDepthGroup)
            {
                originalTerrainDepthEnabled = terrainDepthGroup.enabled;
                originalTerrainDepthLayer = terrainDepthGroup.sortingLayerID;
                originalTerrainDepthOrder = terrainDepthGroup.sortingOrder;
                capturedTerrainDepthGroup = true;
            }
            var enabled = bodyRenderer.enabled && !bodyRenderer.forceRenderingOff;
            if (terrainDepthGroup.enabled != enabled) terrainDepthGroup.enabled = enabled;
            if (terrainDepthGroup.sortingLayerID != bodyRenderer.sortingLayerID) terrainDepthGroup.sortingLayerID = bodyRenderer.sortingLayerID;
            if (terrainDepthGroup.sortingOrder != bodyRenderer.sortingOrder) terrainDepthGroup.sortingOrder = bodyRenderer.sortingOrder;
        }
        private void RestoreTerrainDepth()
        {
            if (terrainDepthGroup == null || !capturedTerrainDepthGroup) return;
            terrainDepthGroup.enabled = !createdTerrainDepthGroup && originalTerrainDepthEnabled;
            terrainDepthGroup.sortingLayerID = originalTerrainDepthLayer;
            terrainDepthGroup.sortingOrder = originalTerrainDepthOrder;
        }
        private void LateUpdate()
        {
            RefreshVisibility();
            if (terrainView != null && bodyRenderer != null)
            {
                bodyRenderer.sortingLayerID = terrainView.FloorFeedbackSortingLayerID;
                bodyRenderer.sortingOrder = crossingDepthEdge.HasValue
                    ? terrainView.GetLowWallCrossingDepthSortingOrder(transform.position.y, crossingDepthEdge.Value)
                    : terrainView.GetActorDepthSortingOrder(transform.position.y);
                RefreshTerrainDepth();
            }
            combatPose?.Tick();
            if (bodyRenderer != null)
            {
                statusVfx ??= new CombatStatusVfxPlayer(bodyRenderer);
                statusVfx.Tick(BoundUnit?.Character, BoundUnit, !transformation.IsPlaying);
            }
        }
        private void OnDisable()
        {
            crossingDepthEdge = null;
            RestoreTerrainDepth();
            statusVfx?.Clear();
            combatPose?.Clear();
            hasDisplayedLifeState = false;
        }
        private void OnDestroy() { combatPose?.Dispose(); statusVfx?.Dispose(); }

        public BattleUnit BoundUnit { get; private set; }
        public bool IsMoving => hasTargetWorldPosition;

        public void Bind(BattleUnit unit)
        {
            crossingDepthEdge = null;
            statusVfx?.Clear();
            combatPose?.Clear();
            BoundUnit = unit;
            hasDisplayedLifeState = false;
            ConfigureVisual();
            lastKnownHealth = BoundUnit?.Character?.CurrentHealth ?? 0;
            SetNameLabelVisible(false);
            Refresh();
        }

        public void SetNameLabelVisible(bool visible)
        {
            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(visible);
            }
        }

        public void Refresh()
        {
            if (BoundUnit == null)
            {
                return;
            }

            SyncLifeStatePose();

            if (nameLabel != null)
            {
                nameLabel.text = BoundUnit.Character.Definition.DisplayName;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = CharacterAnimationResolver.HasImportedCharacterAnimation(BoundUnit.Character.Definition.Id)
                    ? Color.white : BoundUnit.IsPlayerSide ? playerSideColor : enemySideColor;
                if (BoundUnit.IsPlayerSide && Godmist.Systems.Combat.StatusEffectSystem.IsInvisible(BoundUnit))
                { var color = bodyRenderer.color; color.a *= .45f; bodyRenderer.color = color; }
            }

            RefreshVisibility();

            var currentHealth = BoundUnit.Character.CurrentHealth;

            if (currentHealth < lastKnownHealth)
            {
                PlayHitReaction();
            }

            lastKnownHealth = currentHealth;
        }

        public void SetWorldPosition(Vector3 position, bool snapToPosition = false)
        {
            targetWorldPosition = position;

            if (snapToPosition)
            {
                crossingDepthEdge = null;
                transform.position = position;
                hasTargetWorldPosition = false;
                PlayIdleAnimation();
                return;
            }

            hasTargetWorldPosition = Vector3.SqrMagnitude(transform.position - targetWorldPosition) > 0.0001f;

            if (!hasTargetWorldPosition) crossingDepthEdge = null;
            else if (terrainView != null && terrainView.WorldToCell(transform.position) != terrainView.WorldToCell(position))
                crossingDepthEdge = terrainView.TryGetLowWallCrossing(transform.position, position,
                    BoundUnit?.Position.Level ?? 0, out var edge) ? edge : null;

            if (hasTargetWorldPosition)
            {
                SetFacingDirection(targetWorldPosition - transform.position);
                PlayRunAnimation();
            }
        }

        private Godmist.Data.Characters.CharacterDefinition configuredDefinition;
        private readonly CharacterTransformationPlayer transformation = new();
        private void Update()
        {
            var definition = BoundUnit?.Character?.Definition;
            if (definition != null && definition != configuredDefinition)
            {
                var frames = configuredDefinition?.TransformationFrames;
                var transitionFps = configuredDefinition?.TransformationFramesPerSecond ?? 8f;
                hasDisplayedLifeState = false;
                PlayTransformation(frames,transitionFps);
            }
            if(BoundUnit?.Character?.LifeState != CharacterLifeState.Alive) transformation.Stop();
            if (transformation.Tick(bodyRenderer)) return;
            UpdateMovement();
            UpdateAnimation();
        }

        private void UpdateMovement()
        {
            if (!hasTargetWorldPosition) return;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorldPosition,
                movementSpeed * Time.deltaTime);

            if (Vector3.SqrMagnitude(transform.position - targetWorldPosition) > 0.0001f) return;

            transform.position = targetWorldPosition;
            hasTargetWorldPosition = false;
            crossingDepthEdge = null;
            PlayIdleAnimation();
        }

        private void UpdateAnimation()
        {
            if (bodyRenderer == null) return;
            SyncLifeStatePose();
            if (CombatStatusVfxPlayer.IsMotionLocked(BoundUnit?.Character)) return;
            if (combatPose?.Tick() == true) return;

            if (Time.time < hitReactionEndTime)
            {
                hitReactionVisible = true;
                bodyRenderer.sprite = hitSprite;
                return;
            }

            if (hitReactionVisible)
            {
                hitReactionVisible = false;
                RestoreActiveAnimationFrame();
            }

            if (activeImportedAnimation != null)
            {
                if (activeImportedAnimation.FrameCount < 2 || Time.time < nextAnimationFrameTime) return;

                animationFrameIndex = (animationFrameIndex + 1) % activeImportedAnimation.FrameCount;
                bodyRenderer.sprite = activeImportedAnimation.Frames[animationFrameIndex];
                nextAnimationFrameTime = Time.time + activeImportedAnimation.GetFrameDuration(animationFrameIndex);
                return;
            }

            if (activeAnimationFrames == null || activeAnimationFrames.Count < 2) return;
            if (Time.time < nextAnimationFrameTime) return;

            animationFrameIndex = (animationFrameIndex + 1) % activeAnimationFrames.Count;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void ConfigureVisual()
        {
            var definition = BoundUnit?.Character?.Definition;

            if (bodyRenderer == null || definition == null) return;

            configuredDefinition = definition;
            idleAnimationFrames = definition.IdleAnimationFrames;
            runAnimationFrames = definition.RunAnimationFrames;
            hitSprite = definition.HitSprite;

            animationDirectionIndex = CharacterAnimationResolver.DirectionIndexFor(facingDirection);
            CharacterAnimationResolver.TryResolveCharacter(
                definition.Id,
                facingDirection,
                CharacterAnimationState.Idle,
                out idleImportedAnimation);
            CharacterAnimationResolver.TryResolveCharacter(
                definition.Id,
                facingDirection,
                CharacterAnimationState.Walk,
                out runImportedAnimation);

            if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            {
                idleAnimationFrames = definition.VisualSprite != null
                    ? new[] { definition.VisualSprite }
                    : null;
            }

            PlayIdleAnimation();
        }

        private void PlayIdleAnimation()
        {
            if (transformation.IsPlaying) return;
            if (idleImportedAnimation != null)
            {
                if (activeImportedAnimation == idleImportedAnimation) return;
                PlayAnimation(idleImportedAnimation);
                return;
            }

            activeImportedAnimation = null;
            if (activeAnimationFrames == idleAnimationFrames) return;

            PlayAnimation(idleAnimationFrames, 4f);
        }

        private void PlayRunAnimation()
        {
            if (transformation.IsPlaying) return;
            if (runImportedAnimation != null)
            {
                if (activeImportedAnimation == runImportedAnimation) return;
                PlayAnimation(runImportedAnimation);
                return;
            }

            activeImportedAnimation = null;
            if (runAnimationFrames == null || runAnimationFrames.Count == 0) return;
            if (activeAnimationFrames == runAnimationFrames) return;

            PlayAnimation(runAnimationFrames, 8f);
        }

        private void PlayHitReaction()
        {
            if (HasCombatPose || transformation.IsPlaying) return;
            if (hitSprite == null) return;

            hitReactionEndTime = Time.time + hitReactionDuration;
            hitReactionVisible = true;
            bodyRenderer.sprite = hitSprite;
        }

        private void RestoreActiveAnimationFrame()
        {
            if (activeImportedAnimation != null && activeImportedAnimation.FrameCount > 0)
            {
                animationFrameIndex = Mathf.Clamp(animationFrameIndex, 0, activeImportedAnimation.FrameCount - 1);
                bodyRenderer.sprite = activeImportedAnimation.Frames[animationFrameIndex];
                nextAnimationFrameTime = Time.time + activeImportedAnimation.GetFrameDuration(animationFrameIndex);
                return;
            }

            if (activeAnimationFrames == null || activeAnimationFrames.Count == 0) return;

            animationFrameIndex = Mathf.Clamp(animationFrameIndex, 0, activeAnimationFrames.Count - 1);
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void PlayAnimation(IReadOnlyList<Sprite> frames, float framesPerSecond)
        {
            if (bodyRenderer == null || frames == null || frames.Count == 0) return;

            activeImportedAnimation = null;
            activeAnimationFrames = frames;
            animationFrameIndex = 0;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            secondsPerAnimationFrame = 1f / Mathf.Max(1f, framesPerSecond);
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void PlayAnimation(CharacterAnimationClip clip)
        {
            if (bodyRenderer == null || clip == null || clip.FrameCount == 0) return;

            activeAnimationFrames = null;
            activeImportedAnimation = clip;
            animationFrameIndex = 0;
            bodyRenderer.sprite = clip.Frames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + clip.GetFrameDuration(animationFrameIndex);
        }

        private void SetFacingDirection(Vector3 movementDirection)
        {
            if (movementDirection.sqrMagnitude < .000001f) return;

            facingDirection = new Vector2(movementDirection.x, movementDirection.y).normalized;
            var definition = BoundUnit?.Character?.Definition;
            var directionIndex = CharacterAnimationResolver.DirectionIndexFor(facingDirection);
            if (definition != null && directionIndex != animationDirectionIndex)
            {
                animationDirectionIndex = directionIndex;
                CharacterAnimationResolver.TryResolveCharacter(
                    definition.Id,
                    facingDirection,
                    CharacterAnimationState.Idle,
                    out idleImportedAnimation);
                CharacterAnimationResolver.TryResolveCharacter(
                    definition.Id,
                    facingDirection,
                    CharacterAnimationState.Walk,
                    out runImportedAnimation);
            }

            if (bodyRenderer == null) return;

            if (idleImportedAnimation != null || runImportedAnimation != null)
            {
                bodyRenderer.transform.localRotation = Quaternion.identity;
                return;
            }

            if (BoundUnit?.Character?.LifeState == CharacterLifeState.Downed)
            {
                bodyRenderer.transform.localRotation = Quaternion.identity;
                return;
            }

            if (Mathf.Abs(movementDirection.x) < 0.0001f) return;

            bodyRenderer.transform.localRotation = movementDirection.x > 0f
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(180f, 0f, 180f);
        }
    }
}
