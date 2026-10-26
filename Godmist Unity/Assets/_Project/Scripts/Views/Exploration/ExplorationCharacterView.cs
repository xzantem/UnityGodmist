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
        public SpriteRenderer BodyRenderer => bodyRenderer;
        [SerializeField] private Godmist.Views.Combat.CombatUnitVisualStateView combatVisualState;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private GameObject awarenessRoot;
        [SerializeField] private UnityEngine.UI.Image awarenessFill;
        [SerializeField] private Godmist.Views.UI.Workspace.UIWorkspaceStylePalette awarenessStyle;
        [SerializeField] private Color awarenessLowColor = new(1f, .8f, .1f, 1f);
        [SerializeField] private Color awarenessHighColor = new(1f, .12f, .08f, 1f);
        private bool awarenessVisible;
        private Godmist.Views.UI.CombatDownedIndicatorView downedIndicator;
        private const float AwarenessHeadGap = 2f / 32f;
        private ExplorationAwarenessIndicator awarenessIndicator;
        private TextMeshProUGUI awarenessExclamation;
        private float lastAwarenessProgress = float.NaN;
        private bool lastAwarenessPartyVisibility;

        public void ShowAwareness(float progress, bool visibleToParty)
        {
            awarenessVisible = visibleToParty && progress > 0f;
            progress = Mathf.Clamp01(progress);
            if (progress == lastAwarenessProgress && visibleToParty == lastAwarenessPartyVisibility
                && awarenessIndicator != null)
            { RefreshAwarenessVisibility(); return; }
            lastAwarenessProgress = progress; lastAwarenessPartyVisibility = visibleToParty;
            EnsureAwarenessIndicator();
            if (awarenessFill != null)
            {
                // Keep the serialized field as a compatibility hook for old
                // prefabs, but never leave the old rectangular Image visible.
                awarenessFill.fillAmount = progress;
                awarenessFill.color = Color.Lerp(awarenessLowColor, awarenessHighColor, progress);
            }
            var lowColor = awarenessStyle != null
                ? awarenessStyle.GetAccentColor(Godmist.Views.UI.Workspace.UIWorkspaceAccentStyle.Warning) : awarenessLowColor;
            var highColor = awarenessStyle != null
                ? awarenessStyle.GetAccentColor(Godmist.Views.UI.Workspace.UIWorkspaceAccentStyle.Negative) : awarenessHighColor;
            awarenessIndicator?.SetProgress(progress, Color.Lerp(lowColor, highColor, progress));
            RefreshAwarenessVisibility();
        }

        private void EnsureAwarenessIndicator()
        {
            if (awarenessRoot == null) return;

            var rootTransform = awarenessRoot.transform as RectTransform;
            if (rootTransform == null) return;
            if (awarenessIndicator != null && awarenessExclamation != null
                && awarenessIndicator.transform.parent == rootTransform
                && awarenessExclamation.transform.parent == rootTransform)
                return;

            if (awarenessFill != null)
            {
                awarenessFill.enabled = false;
                var legacyBackground = awarenessFill.transform.parent;
                if (legacyBackground != null && legacyBackground != rootTransform)
                {
                    var backgroundImage = legacyBackground.GetComponent<UnityEngine.UI.Image>();
                    if (backgroundImage != null) backgroundImage.enabled = false;
                    legacyBackground.gameObject.SetActive(false);
                }
            }

            rootTransform.sizeDelta = new Vector2(64f, 64f);
            var indicatorTransform = rootTransform.Find("AwarenessDiamond");
            awarenessIndicator = indicatorTransform != null
                ? indicatorTransform.GetComponent<ExplorationAwarenessIndicator>()
                : null;
            if (awarenessIndicator == null)
            {
                GameObject indicatorObject;
                if (indicatorTransform != null)
                {
                    indicatorObject = indicatorTransform.gameObject;
                }
                else
                {
                    indicatorObject = new GameObject("AwarenessDiamond", typeof(RectTransform));
                    indicatorObject.transform.SetParent(rootTransform, false);
                }
                var indicatorRect = (RectTransform)indicatorObject.transform;
                indicatorRect.anchorMin = Vector2.zero;
                indicatorRect.anchorMax = Vector2.one;
                indicatorRect.offsetMin = Vector2.zero;
                indicatorRect.offsetMax = Vector2.zero;
                awarenessIndicator = indicatorObject.AddComponent<ExplorationAwarenessIndicator>();
            }
            awarenessIndicator.raycastTarget = false;
            awarenessIndicator.SetStyle(awarenessStyle);

            var exclamationTransform = rootTransform.Find("AwarenessExclamation");
            awarenessExclamation = exclamationTransform != null
                ? exclamationTransform.GetComponent<TextMeshProUGUI>()
                : null;
            if (awarenessExclamation == null)
            {
                GameObject exclamationObject;
                if (exclamationTransform != null)
                {
                    exclamationObject = exclamationTransform.gameObject;
                }
                else
                {
                    exclamationObject = new GameObject("AwarenessExclamation", typeof(RectTransform));
                    exclamationObject.transform.SetParent(rootTransform, false);
                }
                awarenessExclamation = exclamationObject.AddComponent<TextMeshProUGUI>();
            }
            var exclamationRect = (RectTransform)awarenessExclamation.transform;
            exclamationRect.anchorMin = Vector2.zero;
            exclamationRect.anchorMax = Vector2.one;
            exclamationRect.offsetMin = Vector2.zero;
            exclamationRect.offsetMax = Vector2.zero;
            if (nameLabel != null)
            {
                if (nameLabel.font != null) awarenessExclamation.font = nameLabel.font;
                if (nameLabel.fontSharedMaterial != null) awarenessExclamation.fontSharedMaterial = nameLabel.fontSharedMaterial;
            }
            if (awarenessStyle != null && awarenessStyle.Font != null)
                awarenessExclamation.font = awarenessStyle.Font;
            awarenessExclamation.text = "!";
            awarenessExclamation.alignment = TextAlignmentOptions.Center;
            awarenessExclamation.fontSize = 42f;
            awarenessExclamation.fontStyle = FontStyles.Normal;
            awarenessExclamation.color = awarenessStyle != null
                ? awarenessStyle.GetTextColor(Godmist.Views.UI.Workspace.UIWorkspaceTextStyle.Body)
                : new Color(1f, .95f, .75f, 1f);
            awarenessExclamation.outlineColor = new Color(.015f, .02f, .025f, 1f);
            awarenessExclamation.outlineWidth = .24f;
            awarenessExclamation.raycastTarget = false;
            awarenessExclamation.textWrappingMode = TextWrappingModes.NoWrap;
            awarenessExclamation.overflowMode = TextOverflowModes.Overflow;
            awarenessExclamation.transform.SetAsLastSibling();
        }

        private void RefreshAwarenessVisibility()
        {
            if (awarenessRoot == null) return;
            var visible = awarenessVisible && CombatUnit == null
                && !IsInvisibleEnemy
                && RuntimeCharacter?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                && (terrainView == null || terrainView.IsTraversalVisible(DungeonPosition));
            if (visible) RefreshAwarenessPosition();
            if (awarenessRoot.activeSelf != visible) awarenessRoot.SetActive(visible);
        }

        private void RefreshAwarenessPosition()
        {
            if (bodyRenderer == null || bodyRenderer.sprite == null
                || awarenessRoot.transform is not RectTransform root) return;

            // Use the current world bounds so animation, pivots, elevation and
            // actor scale cannot leave the diamond inside a taller sprite.
            var bounds = bodyRenderer.bounds;
            var bottomOffset = root.TransformVector(new Vector3(root.rect.center.x, root.rect.yMin, 0f));
            var position = root.position;
            position.x = bounds.center.x - bottomOffset.x;
            position.y = bounds.max.y + AwarenessHeadGap - bottomOffset.y;
            root.position = position;
        }
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D movementCollider;
        [SerializeField] private Color leaderColor = new(0.92f, 0.68f, 0.27f, 1f);
        [SerializeField] private Color followerColor = new(0.35f, 0.75f, 1f, 1f);

        private IReadOnlyList<Sprite> idleAnimationFrames;
        private IReadOnlyList<Sprite> runAnimationFrames;
        private IReadOnlyList<Sprite> activeAnimationFrames;
        private Godmist.Views.Characters.CharacterAnimationClip idleImportedAnimation;
        private Godmist.Views.Characters.CharacterAnimationClip runImportedAnimation;
        private Godmist.Views.Characters.CharacterAnimationClip activeImportedAnimation;
        private float nextAnimationFrameTime;
        private float secondsPerAnimationFrame;
        private int animationFrameIndex;
        private int animationDirectionIndex = -1;
        private float lastMovementTime = float.NegativeInfinity;
        private bool noclipEnabled;
        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        private Godmist.Views.Characters.CombatPosePlayer combatPose;
        private Godmist.Core.Enums.CharacterLifeState displayedLifeState;
        private bool hasDisplayedLifeState;
        public bool HasCombatPose => combatPose?.IsPlaying == true;
        public void PlayCombatPose(Godmist.Views.Characters.CombatPose pose, Vector2 facing, float duration)
        {
            if (transformation.IsPlaying) return;
            // BindCombat invalidates the cached life state. Establish it before
            // the first action, so Update cannot clear that newly played pose.
            SyncLifeStatePose();
            if (RuntimeCharacter != null && RuntimeCharacter.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive
                && !(RuntimeCharacter.LifeState == Godmist.Core.Enums.CharacterLifeState.Dead && pose?.Action == "hit")) return;
            SetFacingDirection(facing);
            combatPose ??= new Godmist.Views.Characters.CombatPosePlayer(bodyRenderer);
            combatPose.Play(pose, duration);
        }

        public void PlayHelpUpPose(Vector2 facing, float duration)
        {
            var characterId = RuntimeCharacter?.Definition?.Id ?? CharacterId;
            if (string.IsNullOrWhiteSpace(characterId)) return;
            if (!Godmist.Views.Characters.CombatPoseResolver.TryResolveHelpUp(characterId, facing, out var pose)) return;
            PlayCombatPose(pose, facing, duration);
        }

        private void SyncLifeStatePose()
        {
            var state = RuntimeCharacter?.LifeState ?? Godmist.Core.Enums.CharacterLifeState.Alive;
            if (state == Godmist.Core.Enums.CharacterLifeState.Downed && bodyRenderer != null)
                bodyRenderer.transform.localRotation = Quaternion.identity;
            if (hasDisplayedLifeState && displayedLifeState == state) return;
            displayedLifeState = state;
            hasDisplayedLifeState = true;
            // Damage feedback precedes the logical death transition. Finish its
            // hit pose before the existing corpse presenter replaces the body.
            if (state == Godmist.Core.Enums.CharacterLifeState.Dead && combatPose?.IsHitReaction == true) return;
            combatPose?.Clear();
            var characterId = RuntimeCharacter?.Definition?.Id ?? CharacterId;
            if (state != Godmist.Core.Enums.CharacterLifeState.Downed || string.IsNullOrWhiteSpace(characterId)) return;
            if (!Godmist.Views.Characters.CombatPoseResolver.TryResolveDowned(characterId, out var pose)) return;
            combatPose ??= new Godmist.Views.Characters.CombatPosePlayer(bodyRenderer);
            combatPose.Play(pose, float.PositiveInfinity);
        }
        public bool IsTransforming => transformation.IsPlaying;
        public void PlayTransformation(IReadOnlyList<Sprite> frames,float fps)
        {
            combatPose?.Clear();ConfigureVisual(RuntimeCharacter.Definition);transformation.Play(frames,fps);
            if(frames != null && frames.Count>0 && bodyRenderer != null) bodyRenderer.transform.localRotation=Quaternion.identity;
            transformation.Tick(bodyRenderer);
        }
        public void ClearCombatPose()
        {
            combatPose?.Clear();
            hasDisplayedLifeState = false;
            SyncLifeStatePose();
        }
        private void OnDisable()
        {
            EndWallCrossing();
            statusVfx?.Clear();
            combatPose?.Clear();
            hasDisplayedLifeState = false;
        }
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
        private UnityEngine.Rendering.SortingGroup worldDepthSortingGroup;
        private bool worldDepthSortingGroupCreated;
        private bool worldDepthSortingGroupStateCaptured;
        private bool worldDepthSortingGroupOriginalEnabled;
        private int worldDepthSortingGroupOriginalLayerID;
        private int worldDepthSortingGroupOriginalOrder;

        private void OnDestroy()
        {
            combatPose?.Dispose();
            statusVfx?.Dispose();
            DisableWorldDepthSorting();
        }

        private Godmist.Views.Characters.CombatStatusVfxPlayer statusVfx;
        private bool presentationOutsideCamera;
        private bool IsOutsidePresentationCamera => CombatUnit == null && !transformation.IsPlaying
            && bodyRenderer != null && terrainView != null && (!terrainView.IsInPresentationCamera(bodyRenderer.bounds)
                || !string.IsNullOrEmpty(EnemyInstanceId) && terrainView.FogFloor != null && !terrainView.IsCurrentlySeen(DungeonPosition));
        private void TickStatusVfx()
        {
            if (bodyRenderer == null) return;
            statusVfx ??= new Godmist.Views.Characters.CombatStatusVfxPlayer(bodyRenderer);
            statusVfx.Tick(RuntimeCharacter, CombatUnit, !transformation.IsPlaying);
        }
        private static readonly Unity.Profiling.ProfilerMarker ActorLateSample = new("Godmist.Exploration.ActorLate");
        private void LateUpdate()
        {
            using var sample = ActorLateSample.Auto();
            FlushExplorationMovement();
            if (nameLabel != null && nameLabel.gameObject.activeSelf) nameLabel.gameObject.SetActive(false);
            if (IsOutsidePresentationCamera)
            {
                elevationRenderers ??= GetComponentsInChildren<Renderer>(true);
                if (!presentationOutsideCamera)
                    foreach (var renderer in elevationRenderers)
                        if (renderer != null && !renderer.forceRenderingOff) renderer.forceRenderingOff = true;
                if (worldDepthSortingGroup != null && worldDepthSortingGroup.enabled) worldDepthSortingGroup.enabled = false;
                if (awarenessRoot != null && awarenessRoot.activeSelf) awarenessRoot.SetActive(false);
                if (!presentationOutsideCamera) statusVfx?.Clear();
                presentationOutsideCamera = true;
                return;
            }
            presentationOutsideCamera = false;
            RefreshAwarenessVisibility();
            if (terrainView == null)
            {
                RefreshInvisibilityVisibility(true);
                DisableWorldDepthSorting();
                combatPose?.Tick();
                TickStatusVfx();
                return;
            }
            RefreshWorldDepthSorting();
            elevationRenderers ??= GetComponentsInChildren<Renderer>(true);
            var visible = string.IsNullOrEmpty(EnemyInstanceId)
                ? terrainView.IsTraversalProjected(DungeonPosition)
                : terrainView.IsTraversalVisible(DungeonPosition);
            RefreshInvisibilityVisibility(visible);
            combatPose?.Tick();
            TickStatusVfx();
        }
        private Vector2 crossingStart, crossingEnd;
        private Godmist.Runtime.Dungeons.DungeonSurfaceEdge? crossingDepthEdge;
        private float crossingElapsed;
        private bool restoreBody, restoreCollider;
        public bool IsCrossingLowWall { get; private set; }
        public System.Func<Vector2Int, bool> CanCrossTo { get; set; }
        public void ConfigureTerrain(Godmist.Runtime.Dungeons.DungeonFloorMap map,
            Godmist.Views.Dungeons.DungeonFloorMapView view,
            Godmist.Runtime.Dungeons.DungeonFloorInstance floor = null, string actorKey = null)
        {
            terrainMap = map; terrainView = view; terrainFloor = floor; traversalActorKey = actorKey;
            ConfigureMovementFeetAnchor();
            RestoreTraversalPosition();
            if (floor != null && !string.IsNullOrWhiteSpace(actorKey) && floor.TryGetActorFacing(actorKey, out var facing)) SetFacingDirection(facing);
        }

        private void ConfigureMovementFeetAnchor()
        {
            if (movementCollider is not CircleCollider2D circle)
                return;

            // Only the grounded VS-22 world sprites use the root as their
            // visible feet baseline. Keep older/center-pivoted art on its
            // authored collision centre until it is migrated.
            if (bodyRenderer != null && bodyRenderer.sprite != null
                && !UsesGroundedWorldPivot(bodyRenderer.sprite))
                return;

            // World sprites use a grounded pivot: the root position is the
            // visible feet baseline. Keep the circular body above that point
            // so its lower tangent reaches the wall before the feet do.
            circle.offset = new Vector2(circle.offset.x, circle.radius);
        }

        private static bool UsesGroundedWorldPivot(Sprite sprite)
        {
            if (sprite == null || Mathf.Abs(sprite.pixelsPerUnit - 32f) > .001f)
                return false;

            return sprite.pivot.y / Mathf.Max(1f, sprite.rect.height) < .2f;
        }

        private void EndWallCrossing()
        {
            crossingDepthEdge = null;
            if (!IsCrossingLowWall) return;
            IsCrossingLowWall = false;
            if (body != null) { body.position = transform.position; body.simulated = restoreBody; }
            if (movementCollider != null) movementCollider.enabled = restoreCollider;
        }

        public string EnemyInstanceId { get; private set; }
        public CharacterInstance RuntimeCharacter { get; private set; }
        public void SetEnemyInstanceId(string value) => EnemyInstanceId = value;
        public Godmist.Runtime.Combat.BattleUnit CombatUnit { get; private set; }
        private float? normalInvisibilityAlpha;
        private bool HasInvisibility => CombatUnit != null
            ? Godmist.Systems.Combat.StatusEffectSystem.IsInvisible(CombatUnit)
            : Godmist.Systems.Combat.StatusEffectSystem.IsInvisible(RuntimeCharacter);
        private bool IsEnemyUnit => CombatUnit != null ? !CombatUnit.IsPlayerSide : !string.IsNullOrEmpty(EnemyInstanceId);
        private bool IsInvisibleEnemy => IsEnemyUnit && HasInvisibility;

        private void RefreshInvisibilityVisibility(bool visible)
        {
            var invisible = HasInvisibility;
            visible &= !(IsEnemyUnit && invisible);
            elevationRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in elevationRenderers) if (renderer != null) renderer.forceRenderingOff = !visible;
            RefreshInvisibilityAlpha(!IsEnemyUnit && invisible);
        }

        private void RefreshInvisibilityAlpha() => RefreshInvisibilityAlpha(!IsEnemyUnit && HasInvisibility);
        private void RefreshInvisibilityAlpha(bool translucent)
        {
            if (bodyRenderer == null) return;
            var color = bodyRenderer.color;
            if (translucent)
            {
                normalInvisibilityAlpha ??= color.a;
                color.a = normalInvisibilityAlpha.Value * .45f;
                bodyRenderer.color = color;
            }
            else if (normalInvisibilityAlpha.HasValue)
            {
                color.a = normalInvisibilityAlpha.Value;
                bodyRenderer.color = color;
                normalInvisibilityAlpha = null;
            }
        }

        public void BindCombat(Godmist.Runtime.Combat.BattleUnit unit, Godmist.Runtime.Combat.DungeonCombatContext context = null)
        {
            ClearPendingExplorationMovement();
            Godmist.Systems.Combat.ExplorationStatusEffectSystem.ResetMovementSeries(unit?.Character);
            statusVfx?.Clear();
            combatPose?.Clear();
            hasDisplayedLifeState = false;
            EndWallCrossing();
            combatContext = context;
            CombatUnit = unit;
            RuntimeCharacter = unit.Character;
            if (body != null) body.simulated = false;
            if (movementCollider != null) movementCollider.enabled = false;
        }
        public void ReleaseCombat()
        {
            EndWallCrossing();
            ClearPendingExplorationMovement();
            Godmist.Systems.Combat.ExplorationStatusEffectSystem.ResetMovementSeries(RuntimeCharacter);
            Godmist.Systems.Combat.ExplorationStatusEffectSystem.ClampExplorationMomentum(RuntimeCharacter);
            ClearCombatPose();
            var position = DungeonPosition;
            PreferredTraversalDestination = null;
            CombatUnit = null;
            downedIndicator?.Bind(null);
            combatContext = null;
            if (UsesLayeredTraversal) StoreTraversalPosition(position);
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            combatVisualState?.ClearHighlight();
            if (body != null) body.position = transform.position;
        }

        public void RefreshCombatPresentation(bool isActive, bool isHovered, bool isTargeting)
        {
            Godmist.Views.UI.CombatDownedIndicatorView.UpdateWorld(ref downedIndicator, bodyRenderer, CombatUnit, awarenessStyle);
            if (CombatUnit == null) { combatVisualState?.ClearHighlight(); return; }
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            combatVisualState?.Refresh(CombatUnit, isActive, isHovered, isTargeting, true);
        }

        public bool IsPointerHit(Vector2 point) => isActiveAndEnabled && bodyRenderer != null
            && bodyRenderer.enabled && !bodyRenderer.forceRenderingOff
            && bodyRenderer.bounds.Contains(new Vector3(point.x, point.y, bodyRenderer.bounds.center.z))
            && IsVisibleForInspection;
        public bool IsVisibleForInspection => isActiveAndEnabled && RuntimeCharacter != null
            && !IsInvisibleEnemy
            && RuntimeCharacter.LifeState != Godmist.Core.Enums.CharacterLifeState.Dead
            && (terrainView == null || (string.IsNullOrEmpty(EnemyInstanceId)
                ? terrainView.IsTraversalProjected(DungeonPosition)
                : terrainView.IsTraversalVisible(DungeonPosition) && terrainView.IsCurrentlySeen(DungeonPosition)));
        public void SetInspectionHighlight(bool value)
        {
            if (CombatUnit == null) combatVisualState?.Refresh(null, false, value && !IsInvisibleEnemy, false, true);
        }
        public string CharacterId { get; private set; } = string.Empty;
        public Vector2 Position => transform.position;

        public void Bind(CharacterInstance character)
        {
            if (character == null) return;

            ClearPendingExplorationMovement();

            combatPose?.Clear();
            ShowAwareness(0f, false);

            CharacterId = character.CharacterId;
            RuntimeCharacter = character;
            hasDisplayedLifeState = false;

            ConfigureVisual(character.Definition);

            if (nameLabel != null)
            {
                nameLabel.text = character.Definition != null ? character.Definition.DisplayName : CharacterId;
            }
        }

        public void Bind(CharacterDefinition definition)
        {
            if (definition == null) return;

            combatPose?.Clear();
            CharacterId = definition.Id;
            hasDisplayedLifeState = false;
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
                normalInvisibilityAlpha = null;
                bodyRenderer.color = tint;
                RefreshInvisibilityAlpha();
            }
        }

        public void SetIsActiveLeader(bool isActiveLeader)
        {
            if (bodyRenderer != null)
            {
                SetTint(isActiveLeader ? leaderColor : followerColor);
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
            using var moveSample = MoveSample.Auto();
            if (transformation.IsPlaying) return;
            if (CombatUnit == null && RuntimeCharacter != null
                && RuntimeCharacter.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive) return;
            if (offset.sqrMagnitude < 0.0001f) return;
            if (HasCombatPose) ClearCombatPose();
            if (UsesLayeredTraversal && CombatUnit == null && !noclipEnabled)
            {
                var start = Position;
                MoveLayeredDirection(offset);
                RecordExplorationMovement(Vector2.Distance(start, Position));
                return;
            }
            if (IsCrossingLowWall) return;
            var currentPosition = Position;
            // Combat movement has already been validated and committed by
            // CombatMovementSystem. A borrowed exploration view is only a
            // presentation of that logical move; applying exploration
            // collision checks here can reject the visual step and leave the
            // combat AI waiting forever for the view to reach its target.
            var isCombatPresentation = CombatUnit != null;
            if (!isCombatPresentation && terrainMap != null && terrainView != null && !noclipEnabled)
            {
                var from = terrainView.WorldToCell(currentPosition);
                if (terrainMap.IsDifficultTerrain(from)) offset *= .5f;
                var to = terrainView.WorldToCell(currentPosition + offset);
                if (from.x != to.x && from.y != to.y)
                {
                    // Crossing a corner must not bypass a low edge just because input is diagonal.
                    var horizontal = new Vector2Int(to.x, from.y);
                    var vertical = new Vector2Int(from.x, to.y);
                    if (terrainMap.HasWallBetween(from, horizontal)
                        && terrainMap.GetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(from, horizontal)) == Godmist.Core.Enums.DungeonWallType.Low)
                        to = horizontal;
                    else if (terrainMap.HasWallBetween(from, vertical)
                        && terrainMap.GetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(from, vertical)) == Godmist.Core.Enums.DungeonWallType.Low)
                        to = vertical;
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
                    crossingDepthEdge = new(new Godmist.Runtime.Dungeons.DungeonCellEdge(from, to));
                    return;
                }
            }

            if (!isCombatPresentation && terrainMap == null && !noclipEnabled)
            {
                var startCanStand = CanStandAt(currentPosition);
                bool CanCommitCityStep(Vector2 step)
                    => HasClearMovementSegment(currentPosition, currentPosition + step)
                       && (!startCanStand || CanStandAt(currentPosition + step));
                if (!CanCommitCityStep(offset))
                {
                    var horizontal = new Vector2(offset.x, 0);
                    var vertical = new Vector2(0, offset.y);
                    offset = CanCommitCityStep(horizontal) ? horizontal
                        : CanCommitCityStep(vertical) ? vertical : Vector2.zero;
                }
                if (offset.sqrMagnitude < .000001f) return;
            }
            else if (!isCombatPresentation && terrainMap != null && !noclipEnabled && !HasClearMovementSegment(currentPosition, currentPosition + offset))
            {
                return;
            }
            SetFacingDirection(offset);
            PlayRunAnimation();
            lastMovementTime = Time.time;

            using var commitSample = CommitMoveSample.Auto();
            if (body != null && body.simulated)
            {
                QueueExplorationMovement(offset.magnitude);
                body.MovePosition(body.position + offset);
                return;
            }

            transform.position += (Vector3)offset;
            RecordExplorationMovement(Vector2.Distance(currentPosition, Position));
        }

        private readonly List<RaycastHit2D> movementHits = new();
        private readonly List<Collider2D> standingHits = new();
        private static readonly Unity.Profiling.ProfilerMarker StandingQuerySample = new("Godmist.Exploration.StandingQuery");
        private static readonly Unity.Profiling.ProfilerMarker MovementQuerySample = new("Godmist.Exploration.MovementQuery");
        private static readonly Unity.Profiling.ProfilerMarker MoveSample = new("Godmist.Exploration.Move");
        private static readonly Unity.Profiling.ProfilerMarker FacingSample = new("Godmist.Exploration.Facing");
        private static readonly Unity.Profiling.ProfilerMarker CommitMoveSample = new("Godmist.Exploration.CommitMove");
        public float MovementRadius => movementCollider is CircleCollider2D circle
            ? circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y)) : .2f;

        /// <summary>
        /// World-space offset from the grounded actor position to the movement
        /// collider's actual centre. Exploration sprites use their visible feet
        /// as the root pivot, so this is not necessarily zero.
        /// </summary>
        public Vector2 MovementColliderCenterOffset
        {
            get
            {
                if (movementCollider is CircleCollider2D circle)
                    return (Vector2)circle.transform.TransformPoint(circle.offset) - Position;
                return movementCollider != null
                    ? (Vector2)movementCollider.bounds.center - Position
                    : Vector2.zero;
            }
        }

        public bool CanStandAt(Vector2 position)
        {
            using var querySample = StandingQuerySample.Auto();
            var layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(layerMask);
            standingHits.Clear();
            ReadMovementFootprint(out var centerOffset, out var radius);
            Physics2D.OverlapCircle(position + centerOffset, radius, filter, standingHits);
            foreach (var hit in standingHits)
            {
                if (!hit.isTrigger && hit.GetComponentInParent<ExplorationCharacterView>() == null)
                    return false;
            }
            return true;
        }

        public bool HasClearMovementSegment(Vector2 from, Vector2 to)
        {
            using var querySample = MovementQuerySample.Auto();
            var delta = to - from;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            movementHits.Clear();
            ReadMovementFootprint(out var centerOffset, out var radius);
            Physics2D.CircleCast(from + centerOffset, radius, delta.sqrMagnitude > 0 ? delta.normalized : Vector2.right,
                filter, movementHits, delta.magnitude);
            foreach (var hit in movementHits)
            {
                if (hit.collider == null || hit.collider.GetComponentInParent<ExplorationCharacterView>() != null) continue;
                // A just-spawned actor can overlap an obstacle by a small amount
                // because its grounded feet root and movement collider have
                // different centres. Permit only a contact at cast origin that
                // demonstrably reduces penetration along this segment. Hits
                // encountered after leaving the initial overlap remain blockers.
                if (hit.fraction <= .00001f && IsSeparatingInitialOverlap(hit.collider, from, to)) continue;
                return false;
            }
            return true;
        }

        private bool IsSeparatingInitialOverlap(Collider2D obstacle, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            if (obstacle == null || delta.sqrMagnitude <= .000001f) return false;

            var center = from + MovementColliderCenterOffset;
            var normal = Vector2.zero;
            var startDistance = default(ColliderDistance2D);
            var endDistance = default(ColliderDistance2D);
            if (movementCollider != null)
            {
                var colliderTransformOffset = (Vector2)movementCollider.transform.position - Position;
                var colliderAngle = movementCollider.transform.eulerAngles.z;
                var obstaclePosition = (Vector2)obstacle.transform.position;
                var obstacleAngle = obstacle.transform.eulerAngles.z;
                try
                {
                    startDistance = movementCollider.Distance(
                        from + colliderTransformOffset, colliderAngle,
                        obstacle, obstaclePosition, obstacleAngle);
                    endDistance = movementCollider.Distance(
                        to + colliderTransformOffset, colliderAngle,
                        obstacle, obstaclePosition, obstacleAngle);
                    if (startDistance.isValid && endDistance.isValid)
                    {
                        if (!startDistance.isOverlapped) return false;
                        normal = startDistance.normal;
                        return normal.sqrMagnitude > .000001f
                            && Vector2.Dot(delta, normal) > .00001f
                            && endDistance.distance > startDistance.distance + .00001f;
                    }
                }
                catch (System.InvalidOperationException) { }
            }

            // Disabled layered movement colliders may not provide a valid
            // ColliderDistance2D. For the common edge overlap, the obstacle's
            // closest point still gives a conservative outward normal. If the
            // centre is inside the obstacle, do not guess and keep the block.
            var closest = obstacle.ClosestPoint(center);
            var away = center - closest;
            if (away.sqrMagnitude <= .000001f) return false;
            normal = away.normalized;
            return Vector2.Dot(delta, normal) > .00001f;
        }

        public void MoveTowards(Vector2 targetPosition, float maximumDistance, float stopDistance)
        {
            var current = Position;
            var offset = targetPosition - current;

            if (offset.sqrMagnitude <= stopDistance * stopDistance)
            {
                if (CombatUnit != null) crossingDepthEdge = null;
                return;
            }

            var destination = targetPosition - offset.normalized * stopDistance;
            var nextPosition = Vector2.MoveTowards(current, destination, maximumDistance);

            if (CombatUnit != null && terrainMap != null && terrainView != null)
            {
                var from = terrainView.WorldToCell(current);
                var to = terrainView.WorldToCell(destination);
                if (from != to) crossingDepthEdge = terrainView.TryGetLowWallCrossing(current, destination, PresentationLevel, out var edge) ? edge : null;
            }

            Move(nextPosition - current);
            if (CombatUnit != null && Position == destination) crossingDepthEdge = null;
        }

        // A footprint snapshot lives only inside one synchronous physics query.
        // Later queries still observe scale, offset, parenting and collider changes.
        private void ReadMovementFootprint(out Vector2 centerOffset, out float radius)
        {
            if (movementCollider is CircleCollider2D circle)
            {
                var colliderTransform = circle.transform;
                var scale = colliderTransform.lossyScale;
                centerOffset = (Vector2)colliderTransform.TransformPoint(circle.offset) - Position;
                radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
            else { centerOffset = MovementColliderCenterOffset; radius = .2f; }
        }

        private void Awake()
        {
            if (bodyRenderer == null || nameLabel == null) return;

            var labelRenderer = nameLabel.GetComponent<MeshRenderer>();

            if (labelRenderer == null) return;

            labelRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            labelRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }

        private CharacterDefinition configuredDefinition;
        private readonly Godmist.Views.Characters.CharacterTransformationPlayer transformation = new();
        private void Update()
        {
            var definition = RuntimeCharacter?.Definition;
            if (definition != null && definition != configuredDefinition)
            {
                var frames = configuredDefinition?.TransformationFrames;
                var transitionFps = configuredDefinition?.TransformationFramesPerSecond ?? 8f;
                PlayTransformation(frames,transitionFps);
            }
            if(RuntimeCharacter?.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive) transformation.Stop();
            if (transformation.Tick(bodyRenderer)) return;
            SyncLifeStatePose();
            if (Godmist.Views.Characters.CombatStatusVfxPlayer.IsMotionLocked(RuntimeCharacter)) return;
            if (IsCrossingLowWall && !Godmist.Views.UI.DevConsoleView.IsOpen)
            {
                if (RuntimeCharacter != null && RuntimeCharacter.LifeState != Godmist.Core.Enums.CharacterLifeState.Alive)
                    EndWallCrossing();
                else
                {
                    crossingElapsed += Time.deltaTime;
                    var beforeCrossing = Position;
                    transform.position = Vector2.Lerp(crossingStart, crossingEnd, Mathf.Clamp01(crossingElapsed / .6f));
                    RecordExplorationMovement(Vector2.Distance(beforeCrossing, Position));
                    PlayRunAnimation();
                    lastMovementTime = Time.time;
                    if (crossingElapsed >= .6f) { EndWallCrossing(); if (UsesLayeredTraversal) terrainFloor?.SetActorOffset(TraversalActorId, Vector2.zero); }
                }
            }
            if (Time.time - lastMovementTime > 0.08f)
            {
                PlayIdleAnimation();
            }

            if (bodyRenderer == null) return;

            if (IsOutsidePresentationCamera) return;

            if (combatPose?.Tick() == true) return;

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

        private void ConfigureVisual(CharacterDefinition definition)
        {
            if (bodyRenderer == null || definition == null) return;

            configuredDefinition = definition;
            idleAnimationFrames = definition.IdleAnimationFrames;
            runAnimationFrames = definition.RunAnimationFrames;

            // Party directions are small shared packs. Resolve them during
            // configuration rather than doing synchronous texture IO on the
            // first turn of a moving follower. The dungeon spawner prepares
            // enemy packs once per imported unit before binding their views.
            if (RuntimeCharacter != null && string.IsNullOrEmpty(EnemyInstanceId))
                Godmist.Views.Characters.CharacterAnimationResolver.PreparePartyDirections(definition.Id);
            animationDirectionIndex = Godmist.Views.Characters.CharacterAnimationResolver.DirectionIndexFor(FacingDirection);
            Godmist.Views.Characters.CharacterAnimationResolver.TryResolveCharacter(
                definition.Id,
                FacingDirection,
                Godmist.Views.Characters.CharacterAnimationState.Idle,
                out idleImportedAnimation);
            Godmist.Views.Characters.CharacterAnimationResolver.TryResolveCharacter(
                definition.Id,
                FacingDirection,
                Godmist.Views.Characters.CharacterAnimationState.Walk,
                out runImportedAnimation);

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

        private void PlayAnimation(Godmist.Views.Characters.CharacterAnimationClip clip)
        {
            if (bodyRenderer == null || clip == null || clip.FrameCount == 0) return;

            activeAnimationFrames = null;
            activeImportedAnimation = clip;
            animationFrameIndex = 0;
            bodyRenderer.sprite = clip.Frames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + clip.GetFrameDuration(animationFrameIndex);
        }

        public void FaceTowards(Vector2 worldPosition) => SetFacingDirection(worldPosition - Position);

        private void SetFacingDirection(Vector2 movementDirection)
        {
            using var facingSample = FacingSample.Auto();
            if (movementDirection.sqrMagnitude > .000001f)
            {
                FacingDirection = movementDirection.normalized;
                terrainFloor?.TrySetActorFacing(TraversalActorId, FacingDirection);
            }
            var characterId = RuntimeCharacter?.Definition?.Id ?? CharacterId;
            var directionIndex = Godmist.Views.Characters.CharacterAnimationResolver.DirectionIndexFor(FacingDirection);
            if (!string.IsNullOrWhiteSpace(characterId) && directionIndex != animationDirectionIndex)
            {
                animationDirectionIndex = directionIndex;
                Godmist.Views.Characters.CharacterAnimationResolver.TryResolveCharacter(
                    characterId,
                    FacingDirection,
                    Godmist.Views.Characters.CharacterAnimationState.Idle,
                    out idleImportedAnimation);
                Godmist.Views.Characters.CharacterAnimationResolver.TryResolveCharacter(
                    characterId,
                    FacingDirection,
                    Godmist.Views.Characters.CharacterAnimationState.Walk,
                    out runImportedAnimation);
            }

            if (bodyRenderer == null) return;
            if (idleImportedAnimation != null || runImportedAnimation != null)
            {
                if (!bodyRenderer.transform.localRotation.Equals(Quaternion.identity)) bodyRenderer.transform.localRotation = Quaternion.identity;
                return;
            }

            if (RuntimeCharacter?.LifeState == Godmist.Core.Enums.CharacterLifeState.Downed)
            {
                if (!bodyRenderer.transform.localRotation.Equals(Quaternion.identity)) bodyRenderer.transform.localRotation = Quaternion.identity;
                return;
            }
            if (Mathf.Abs(movementDirection.x) < 0.0001f) return;

            var rotation = movementDirection.x > 0f
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(180f, 0f, 180f);
            if (!bodyRenderer.transform.localRotation.Equals(rotation)) bodyRenderer.transform.localRotation = rotation;
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
            ClearPendingExplorationMovement();
            PlayIdleAnimation();
            transform.position = position;

            if (body != null)
            {
                body.position = position;
            }
        }

        private void RefreshWorldDepthSorting()
        {
            if (terrainView == null || bodyRenderer == null || !bodyRenderer.enabled)
            {
                DisableWorldDepthSorting();
                return;
            }

            EnsureWorldDepthSortingGroup();
            if (!worldDepthSortingGroup.enabled) worldDepthSortingGroup.enabled = true;
            if (worldDepthSortingGroup.sortingLayerID != bodyRenderer.sortingLayerID) worldDepthSortingGroup.sortingLayerID = bodyRenderer.sortingLayerID;
            var order = ReadableForestOrder(crossingDepthEdge.HasValue
                ? terrainView.GetLowWallCrossingDepthSortingOrder(Position.y, crossingDepthEdge.Value)
                : terrainView.GetActorDepthSortingOrder(Position.y));
            if (worldDepthSortingGroup.sortingOrder != order) worldDepthSortingGroup.sortingOrder = order;
        }

        private void EnsureWorldDepthSortingGroup()
        {
            if (worldDepthSortingGroup == null)
            {
                worldDepthSortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (worldDepthSortingGroup == null)
                {
                    worldDepthSortingGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    worldDepthSortingGroupCreated = true;
                }
            }

            if (worldDepthSortingGroupStateCaptured)
                return;

            worldDepthSortingGroupOriginalEnabled = worldDepthSortingGroup.enabled;
            worldDepthSortingGroupOriginalLayerID = worldDepthSortingGroup.sortingLayerID;
            worldDepthSortingGroupOriginalOrder = worldDepthSortingGroup.sortingOrder;
            worldDepthSortingGroupStateCaptured = true;
        }

        private void DisableWorldDepthSorting()
        {
            if (worldDepthSortingGroup == null || !worldDepthSortingGroupStateCaptured)
                return;

            if (worldDepthSortingGroupCreated)
            {
                worldDepthSortingGroup.enabled = false;
                return;
            }

            worldDepthSortingGroup.enabled = worldDepthSortingGroupOriginalEnabled;
            worldDepthSortingGroup.sortingLayerID = worldDepthSortingGroupOriginalLayerID;
            worldDepthSortingGroup.sortingOrder = worldDepthSortingGroupOriginalOrder;
        }
    }
}
