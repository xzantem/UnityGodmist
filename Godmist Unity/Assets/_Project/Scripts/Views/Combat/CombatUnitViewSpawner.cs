using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatUnitViewSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private CharacterView battleUnitPrefab;
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private CombatTargetSelectionController targetSelectionController;
        [SerializeField] private CombatUnitInspectController inspectController;

        [Header("Summoned Object Visuals")]
        [SerializeField] private Transform summonedObjectsRoot;
        [SerializeField] private Color missingSpriteColor =
            new(0.78f, 0.35f, 0.95f, 1f);
        [SerializeField] private int summonedObjectSortingOrder = 1;
        [SerializeField, Min(0.1f)] private float summonedObjectScale = 0.7f;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;

        private readonly Dictionary<BattleUnit, CharacterView> views = new();
        private readonly Dictionary<SummonedCombatMapObject, SpriteRenderer>
            summonedObjectViews = new();
        private readonly Dictionary<KnifeCombatMapObject, SpriteRenderer> knifeViews = new();
        private readonly Dictionary<KnifeCombatMapObject, CombatUnitVisualStateView>
            knifeHighlightViews = new();
        private readonly HashSet<KnifeCombatMapObject> pointerHoveredKnives = new();
        private readonly HashSet<KnifeCombatMapObject> counterHighlightedKnives = new();
        private readonly HashSet<BattleUnit> presentationMovingUnits = new();
        private const float RecoverableKnifeOutlineWidth = .1f;

        // A CombatSession is runtime state and may contain nested counterattack payloads.
        // It must never enter Unity's scene/prefab serialization graph.
        [System.NonSerialized] private CombatSession boundSession;
        private Sprite missingSprite;
        private Godmist.Views.UI.Workspace.UIWorkspaceStylePalette downedStyle;
        private Godmist.Runtime.Game.GameSession dungeonSession;
        private Godmist.Views.Dungeons.DungeonFloorMapView floorMap;
        [SerializeField] private Godmist.Views.UI.CombatHudView hud;
        private readonly Dictionary<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView> borrowed = new();
        private readonly Dictionary<BattleUnit, CombatSession> owners = new();
        private readonly Dictionary<BattleUnit, GridPosition> movementTargets = new();
        private sealed class MovementPathPresentation
        {
            public readonly GridPosition[] Waypoints;
            public int Next;
            public MovementPathPresentation(IReadOnlyList<GridPosition> path)
            {
                Waypoints = new GridPosition[path.Count];
                for (var i = 0; i < path.Count; i++) Waypoints[i] = path[i];
            }
        }
        private readonly Dictionary<BattleUnit, MovementPathPresentation> movementPaths = new();
        public void ConfigureDungeon(Godmist.Runtime.Game.GameSession game, Godmist.Views.Dungeons.DungeonFloorMapView map)
        {
            if (!ReferenceEquals(floorMap, map))
            {
                ClearWorldEffects();
                foreach (var renderer in knifeViews.Values)
                    floorMap?.UnregisterLightingPresentation(renderer);
                foreach (var renderer in summonedObjectViews.Values)
                    floorMap?.UnregisterLightingPresentation(renderer);
                foreach (var view in views.Values)
                    if (view != null) floorMap?.UnregisterLightingPresentation(view.BodyRenderer);
            }
            dungeonSession = game;
            floorMap = map;
            foreach (var renderer in knifeViews.Values)
                floorMap?.RegisterGroundLightingPresentation(renderer, true);
            foreach (var pair in summonedObjectViews)
                if (pair.Key.SourceSkillId != Godmist.Systems.Combat.ForestEntBossSystem.SanctuarySkillId
                    || pair.Key.Definition?.ObjectId != "object.forest.ent.fire")
                    floorMap?.RegisterGroundLightingPresentation(pair.Value);
            foreach (var view in views.Values)
                if (view != null) floorMap?.RegisterActorLightingPresentation(view.BodyRenderer);
        }
        public void Borrow(CombatSession session, BattleUnit unit, Godmist.Views.Exploration.ExplorationCharacterView view)
        {
            borrowed[unit] = view;
            owners[unit] = session;
            view.BindCombat(unit, session.DungeonContext);
            view.SetPosition(WorldPosition(session, unit.Position));
        }
        public void Release(BattleUnit unit)
        {
            if (borrowed.Remove(unit, out var view) && view != null)
            {
                // Logical movement can finish before its animation. Restore exploration at
                // the committed cell, never at an intermediate point of that animation.
                if (owners.TryGetValue(unit, out var owner) && floorMap != null)
                    view.SetPosition(WorldPosition(owner, unit.Position));
                view.ReleaseCombat();
            }
            owners.Remove(unit);
            movementTargets.Remove(unit);
            presentationMovingUnits.Remove(unit);
            movementPaths.Remove(unit);
            if (views.TryGetValue(unit, out var standalone) && standalone != null)
                standalone.SetPathPresentationControlled(false);
        }
        private bool IsPositionVisible(CombatSession session, GridPosition position, bool ownUnit = false) => floorMap == null || session.DungeonContext == null
            || (ownUnit
                ? floorMap.IsTraversalProjected(Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, position))
                : floorMap.IsTraversalVisible(Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, position)));
        private Vector3 WorldPosition(CombatSession session, GridPosition position) =>
            floorMap.CellToWorldCenter(Godmist.Systems.Combat.DungeonBattleGridFactory.ToWorldPosition(session.DungeonContext, position));
        private Vector3 UnitWorldPosition(BattleUnit unit, GridPosition position) =>
            owners.TryGetValue(unit, out var owner) ? WorldPosition(owner, position) : GridToWorldPosition(position);
        private void SynchronizeDungeon(float deltaTime)
        {
            var aggregate = new CombatSession();
            foreach (var session in dungeonSession.Combats)
            {
                boundSession = session;
                foreach (var unit in session.Units) owners[unit] = session;
                EnsureViews(session);
                SynchronizeViews(session);
                EnsureSummonedObjectViews(session);
                SynchronizeSummonedObjectViews(session);
                aggregate.Units.AddRange(session.Units);
                aggregate.SummonedObjects.AddRange(session.SummonedObjects);
                aggregate.MapObjects.AddRange(session.MapObjects);
            }
            foreach (var pair in borrowed)
            {
                var unit = pair.Key;
                var view = pair.Value;
                if (view == null || !view.gameObject.activeInHierarchy)
                {
                    AdvancePresentationPath(unit, deltaTime);
                    continue;
                }
                var target = movementTargets.TryGetValue(unit, out var step) ? step : unit.Position;
                if (movementPaths.ContainsKey(unit)) AdvancePresentationPath(unit, deltaTime);
                else view.MoveTowards(UnitWorldPosition(unit, target), 6f * deltaTime, 0f);
                var owner = owners[unit];
                var isFocused = sandboxBootstrap.Session == owner;
                view.RefreshCombatPresentation(owner.ActiveUnit == unit
                        && unit.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive,
                    isFocused && ((targetSelectionController != null && targetSelectionController.HoveredUnit == unit)
                        || (inspectController != null && (inspectController.HoveredUnit == unit || inspectController.InspectedUnit == unit))
                        || (hud != null && hud.HighlightedUnit == unit)),
                    isFocused && targetSelectionController != null && targetSelectionController.HoveredUnit == unit && !unit.IsPlayerSide && targetSelectionController.IsSelectingTarget);
            }
            RemoveMissingViews(aggregate);
            RemoveMissingSummonedObjectViews(aggregate);
            // Knives use each owning room's mapping, but are removed against the union.
            foreach (var session in dungeonSession.Combats)
            {
                boundSession = session;
                SynchronizeKnifeViews(session, false);
            }
            RemoveMissingKnifeViews(aggregate);
        }


        public bool IsUnitVisible(BattleUnit unit)
        {
            if (Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)) return false;
            if (unit == null || floorMap == null) return true;
            if (owners.TryGetValue(unit, out var session)) return IsPositionVisible(session, unit.Position, unit.IsPlayerSide);
            if (ExplorationPresentationVisible != null) return ExplorationPresentationVisible(unit);
            var world = ExplorationWorldPosition?.Invoke(unit);
            return world.HasValue ? floorMap.IsSurfaceVisible(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(floorMap.WorldToCell(world.Value))) : floorMap.FogFloor == null;
        }

        public bool IsUnitKnown(BattleUnit unit)
        {
            if (unit == null) return false;
            if (Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)) return false;
            if (unit.IsPlayerSide || floorMap?.FogFloor == null) return true;
            var session = owners.TryGetValue(unit, out var owner) ? owner
                : dungeonSession?.Combats.FirstOrDefault(combat => combat.Units.Contains(unit));
            return session?.DungeonContext != null && floorMap.IsCurrentlySeen(
                Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, unit.Position));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Unity.Profiling.ProfilerMarker PresentationLogSample = new("Godmist.Combat.PresentationLog");
        public void LogPresentationSnapshot(
            BattleUnit unit,
            string phase,
            string actionState,
            bool aiActionInProgress,
            bool aiCoroutineActive,
            bool membershipChangePending,
            string aiJobState = null,
            string waitState = null,
            float skillPresentationEnds = float.NaN)
        {
            using var sample = PresentationLogSample.Auto();
            if (unit == null) return;

            owners.TryGetValue(unit, out var owner);
            borrowed.TryGetValue(unit, out var borrowedView);
            var logical = owner?.DungeonContext != null
                ? Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(owner.DungeonContext, unit.Position)
                : (Godmist.Runtime.Dungeons.DungeonTraversalPosition?)null;
            var viewPosition = borrowedView?.DungeonPosition;
            var rendererOff = borrowedView == null
                ? "none"
                : string.Join(",", borrowedView.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != null)
                    .Select(renderer => renderer.forceRenderingOff ? "off" : "on"));
            var visible = logical.HasValue && floorMap != null
                ? floorMap.IsTraversalVisible(logical.Value).ToString()
                : "n/a";
            var known = IsUnitKnown(unit).ToString();
            var active = owner?.ActiveUnit == unit;
            var member = owner?.Units?.Contains(unit) == true;
            var floorCell = borrowedView != null && floorMap != null
                ? floorMap.WorldToCell(borrowedView.Position).ToString()
                : "n/a";
            var logicalText = logical.HasValue ? FormatTraversalPosition(logical.Value) : "n/a";
            var viewText = viewPosition.HasValue ? FormatTraversalPosition(viewPosition.Value) : "n/a";
            Debug.Log(
                $"[CombatPresentationSnapshot] phase={phase} action={actionState} "
                + $"unit={unit.Character?.CharacterId ?? "none"} hp={unit.Character?.CurrentHealth ?? 0} "
                + $"ap={unit.CurrentActionPoints:0.##} grid=({unit.Position.X},{unit.Position.Y},{unit.Position.Level}) "
                + $"logical={logicalText} viewDungeon={viewText} floorCell={floorCell} "
                + $"member={member} borrowed={borrowedView != null} active={active} "
                + $"visible={visible} known={known} renderer={rendererOff} "
                + $"viewActive={borrowedView?.gameObject.activeSelf.ToString() ?? "none"} "
                + $"aiAction={aiActionInProgress} aiCoroutine={aiCoroutineActive} membershipPending={membershipChangePending} "
                + $"time={Time.time:0.###} timeScale={Time.timeScale:0.###} "
                + $"skillPresentationEnds={(float.IsNaN(skillPresentationEnds) ? "n/a" : skillPresentationEnds.ToString("0.###"))} "
                + $"aiJobState={aiJobState ?? "n/a"} waitState={waitState ?? "none"}");
        }

        private static string FormatTraversalPosition(Godmist.Runtime.Dungeons.DungeonTraversalPosition position)
            => position.IsOnStairs
                ? $"stair:{position.StairStep}"
                : $"cell=({position.Surface.Cell.x},{position.Surface.Cell.y}) level={position.Surface.Level}";
#endif

        public System.Func<BattleUnit, bool> ExplorationPresentationVisible { get; set; }
        public System.Func<BattleUnit, Vector3?> ExplorationWorldPosition { get; set; }
        public System.Func<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView> ExplorationView { get; set; }

        public Vector2 GetFacing(BattleUnit unit)
        {
            if (unit != null && borrowed.TryGetValue(unit, out var exploration) && exploration != null) return exploration.FacingDirection;
            var free=unit==null?null:ExplorationView?.Invoke(unit);
            if(free!=null)return free.FacingDirection;
            return unit != null && views.TryGetValue(unit, out var battle) && battle != null ? battle.FacingDirection : Vector2.down;
        }

        public bool TryPlayTransformation(CombatSession session,BattleUnit unit,IReadOnlyList<Sprite> frames,float fps)
        {
            if(borrowed.TryGetValue(unit,out var exploration) && exploration != null)
            {
                if(!owners.TryGetValue(unit,out var owner) || owner!=session) return false;
                exploration.PlayTransformation(frames,fps);return true;
            }
            if(views.TryGetValue(unit,out var view) && view != null)
            {view.PlayTransformation(frames,fps);return true;}
            return false;
        }
        public bool HasCombatPose(BattleUnit unit)
        {
            if (unit != null && borrowed.TryGetValue(unit, out var exploration) && exploration != null) return exploration.HasCombatPose;
            return unit != null && views.TryGetValue(unit, out var battle) && battle != null && battle.HasCombatPose;
        }

        public bool TryPlayTargetHeadEffects(CombatSession session, BattleUnit unit, Sprite[] effects, float duration, Vector2[] offsets = null)
        {
            if (unit == null || effects == null || effects.Length == 0 || !IsUnitVisible(unit)) return false;
            SpriteRenderer body = null;
            if (borrowed.TryGetValue(unit, out var exploration) && exploration != null)
            {
                if (!owners.TryGetValue(unit, out var owner) || owner != session) return false;
                body = exploration.BodyRenderer;
            }
            else if (views.TryGetValue(unit, out var view) && view != null) body = view.BodyRenderer;
            if (body == null) body=ExplorationView?.Invoke(unit)?.BodyRenderer;
            if (body == null) return false;
            CombatTargetEffectPlayer.Play(body, unit, effects, duration, offsets);
            return true;
        }

        public bool TryPlayCombatPose(CombatSession session, BattleUnit unit, CombatPose pose, Vector2 facing, float duration)
        {
            if (unit == null || pose == null || !IsUnitVisible(unit)) return false;
            if (borrowed.TryGetValue(unit, out var exploration) && exploration != null && exploration.isActiveAndEnabled)
            { exploration.PlayCombatPose(pose, facing, duration); return true; }
            var free=ExplorationView?.Invoke(unit);
            if(free!=null && free.isActiveAndEnabled) {free.PlayCombatPose(pose,facing,duration);return true;}
            if (!views.ContainsKey(unit)) EnsureViews(session);
            if (!views.TryGetValue(unit, out var battle) || battle == null || !battle.isActiveAndEnabled) return false;
            battle.PlayCombatPose(pose, facing, duration);
            return true;
        }

        public bool TryPlayHelpUpPose(CombatSession session, BattleUnit actor, BattleUnit target, float duration)
        {
            if (actor?.Character?.Definition == null || target == null || !IsUnitVisible(actor)) return false;

            Vector2 facing;
            if (TryGetWorldPosition(actor, out var actorPosition)
                && TryGetWorldPosition(target, out var targetPosition))
                facing = targetPosition - actorPosition;
            else
                facing = new Vector2(target.Position.X - actor.Position.X, target.Position.Y - actor.Position.Y);

            if (facing.sqrMagnitude < .000001f) facing = GetFacing(actor);
            if (!CombatPoseResolver.TryResolveHelpUp(actor.Character.Definition.Id, facing, out var pose)) return false;
            return TryPlayCombatPose(session, actor, pose, facing, duration);
        }

        public void ClearCombatPoses(CombatSession session)
        {
            if (session == null) return;
            foreach (var pair in borrowed)
                if (owners.TryGetValue(pair.Key, out var owner) && owner == session && pair.Value != null)
                { pair.Value.ClearCombatPose(); CombatTargetEffectPlayer.ClearAttached(pair.Value.BodyRenderer); }
            foreach (var pair in views)
                if (pair.Value != null && (session.Units.Contains(pair.Key) || !owners.TryGetValue(pair.Key, out var owner) || owner == session))
                { pair.Value.ClearCombatPose(); CombatTargetEffectPlayer.ClearAttached(pair.Value.BodyRenderer); }
        }

        public bool TryGetPresentationBounds(BattleUnit unit, out Bounds bounds)
        {
            SpriteRenderer body = null;
            if (unit != null && borrowed.TryGetValue(unit, out var existing) && existing != null) body = existing.BodyRenderer;
            else if (unit != null && views.TryGetValue(unit, out var view) && view != null) body = view.BodyRenderer;
            if (body != null && body.sprite != null) { bounds = body.bounds; return true; }
            if (TryGetWorldPosition(unit, out var position))
            { bounds = new Bounds(position + Vector3.up, new Vector3(1.6f, 2.4f, 0)); return true; }
            bounds = default; return false;
        }

        public bool TryGetWorldPosition(BattleUnit unit, out Vector3 position)
        {
            if (unit != null && borrowed.TryGetValue(unit, out var existing) && existing != null)
            { position = existing.transform.position; return true; }
            if (unit != null && views.TryGetValue(unit, out var view) && view != null)
            {
                position = view.transform.position;
                return true;
            }

            var explorationPosition = unit == null ? null : ExplorationWorldPosition?.Invoke(unit);
            if (explorationPosition.HasValue)
            { position = explorationPosition.Value; return true; }
            position = default;
            return false;
        }

        public bool TryGetWorldPosition(GridPosition gridPosition, out Vector3 position)
        {
            position = dungeonSession != null && sandboxBootstrap.Session?.DungeonContext != null
                ? WorldPosition(sandboxBootstrap.Session, gridPosition) : GridToWorldPosition(gridPosition);
            return true;
        }

        public void SetKnifeHover(KnifeCombatMapObject knife, bool hovered)
        {
            if (knife == null) return;
            if (hovered && knifeViews.ContainsKey(knife)) pointerHoveredKnives.Add(knife);
            else pointerHoveredKnives.Remove(knife);
            RefreshKnifeHighlight(knife);
        }

        private void RefreshKnifeHighlight(KnifeCombatMapObject knife)
        {
            if (!knifeHighlightViews.TryGetValue(knife, out var visual) || visual == null) return;
            if ((pointerHoveredKnives.Contains(knife) || counterHighlightedKnives.Contains(knife))
                && knifeViews.TryGetValue(knife, out var renderer) && renderer != null
                && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy)
                visual.Refresh(null, false, true, false, true);
            else visual.ClearHighlight();
        }

        public Vector3 GetPresentationMovementPosition(BattleUnit unit)
        {
            if (unit == null) return Vector3.zero;
            // An inactive view does not animate. Use its
            // current logical presentation step instead of its stale transform.
            if (movementTargets.TryGetValue(unit, out var step)
                && ((borrowed.TryGetValue(unit, out var borrowedView)
                        && (borrowedView == null || !borrowedView.gameObject.activeInHierarchy))
                    || (views.TryGetValue(unit, out var view)
                        && (view == null || !view.gameObject.activeInHierarchy))))
                return UnitWorldPosition(unit, step);
            return TryGetWorldPosition(unit, out var position) ? position : UnitWorldPosition(unit, unit.Position);
        }

        public bool TryBeginPresentationMovement(BattleUnit unit)
        {
            if (unit == null || (!views.ContainsKey(unit) && !borrowed.ContainsKey(unit))) return false;
            presentationMovingUnits.Add(unit);
            return true;
        }

        public bool TryBeginPresentationPath(BattleUnit unit, IReadOnlyList<GridPosition> path)
        {
            if (unit == null || path == null || path.Count == 0
                || (!(borrowed.TryGetValue(unit, out var existing) && existing != null)
                    && !(views.TryGetValue(unit, out var standalone) && standalone != null))
                || !TryBeginPresentationMovement(unit)) return false;
            movementPaths[unit] = new MovementPathPresentation(path);
            if (views.TryGetValue(unit, out var view) && view != null)
                view.SetPathPresentationControlled(true);
            return TryMovePresentationStep(unit, path[0]);
        }

        private void AdvancePresentationPath(BattleUnit unit, float deltaTime)
        {
            if (deltaTime <= 0 || !movementPaths.TryGetValue(unit, out var path)) return;
            borrowed.TryGetValue(unit, out var borrowedView);
            views.TryGetValue(unit, out var view);
            var active = borrowedView != null ? borrowedView.gameObject.activeInHierarchy
                : view != null && view.gameObject.activeInHierarchy;
            // Preserve hidden enemy camera steps without activating hidden views.
            if (!active)
            {
                if (path.Next < path.Waypoints.Length) path.Next++;
                if (path.Next < path.Waypoints.Length)
                    TryMovePresentationStep(unit, path.Waypoints[path.Next]);
                return;
            }
            var remaining = Mathf.Max(0, deltaTime) * (borrowedView != null ? 6f : view.MovementSpeed);
            while (path.Next < path.Waypoints.Length)
            {
                var waypoint = path.Waypoints[path.Next];
                var target = UnitWorldPosition(unit, waypoint);
                var before = borrowedView != null ? (Vector3)borrowedView.Position : view.transform.position;
                var distance = borrowedView != null ? Vector2.Distance(before, target) : Vector3.Distance(before, target);
                if (distance > .00001f)
                {
                    if (remaining <= 0) break;
                    var step = Mathf.Min(remaining, distance);
                    if (borrowedView != null) borrowedView.MoveTowards(target, step, 0);
                    else view.AdvancePathPresentation(target, step, path.Next == path.Waypoints.Length - 1);
                    remaining -= step;
                    var after = borrowedView != null ? (Vector3)borrowedView.Position : view.transform.position;
                    // Transformations can temporarily lock movement. Never skip
                    // a waypoint or spin if the view could not reach it.
                    if ((borrowedView != null ? Vector2.Distance(after, target) : Vector3.Distance(after, target)) > .00001f)
                        break;
                }
                path.Next++;
                if (path.Next < path.Waypoints.Length)
                    TryMovePresentationStep(unit, path.Waypoints[path.Next]);
            }
            if (path.Next == path.Waypoints.Length && view != null)
                view.SetWorldPosition(UnitWorldPosition(unit, path.Waypoints[path.Next - 1]), true);
        }

        public bool TryMovePresentationStep(BattleUnit unit, GridPosition targetPosition)
        {
            if (unit != null && borrowed.ContainsKey(unit) && presentationMovingUnits.Contains(unit))
            {
                movementTargets[unit] = targetPosition;
                if (owners.TryGetValue(unit, out var owner) && sandboxBootstrap.Session == owner
                    && owner.DungeonContext?.SurfaceMap != null)
                {
                    var node = Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(owner.DungeonContext, targetPosition);
                    if (owner.DungeonContext.SurfaceMap.TryGetTraversalHeight(node, out var height))
                        floorMap.FollowElevation(Mathf.CeilToInt(height), true);
                }
                return true;
            }
            if (unit == null
                || !presentationMovingUnits.Contains(unit)
                || !views.TryGetValue(unit, out var view)
                || view == null)
            {
                return false;
            }

            movementTargets[unit] = targetPosition;
            view.SetWorldPosition(UnitWorldPosition(unit, targetPosition));
            return true;
        }

        public bool IsPresentationMovementInProgress(BattleUnit unit)
        {
            if (unit != null && movementPaths.TryGetValue(unit, out var path))
                return path.Next < path.Waypoints.Length;
            if (unit != null && borrowed.TryGetValue(unit, out var existing) && existing != null
                && movementTargets.TryGetValue(unit, out var target))
            {
                // SynchronizeDungeon cannot advance an inactive borrowed view.
                // Treat it as no longer animating so the combat coroutine can
                // clean up and finish the logical turn.
                if (!existing.gameObject.activeSelf) return false;
                return Vector2.Distance(existing.Position, UnitWorldPosition(unit, target)) > 0.01f;
            }
            return unit != null
                && presentationMovingUnits.Contains(unit)
                && views.TryGetValue(unit, out var view)
                && view != null
                && view.IsMoving;
        }

        public void EndPresentationMovement(BattleUnit unit)
        {
            if (unit == null) return;
            presentationMovingUnits.Remove(unit);
            movementTargets.Remove(unit);
            movementPaths.Remove(unit);
            if (views.TryGetValue(unit, out var view) && view != null)
            {
                view.SetPathPresentationControlled(false);
                view.SetWorldPosition(UnitWorldPosition(unit, unit.Position));
            }
        }

        private void Awake()
        {
            if (sandboxBootstrap == null)
            {
                sandboxBootstrap = GetComponent<CombatSandboxBootstrap>();
            }

            if (unitsRoot == null)
            {
                unitsRoot = transform;
            }

            if (summonedObjectsRoot == null)
            {
                summonedObjectsRoot = unitsRoot;
            }

            if (targetSelectionController == null)
            {
                targetSelectionController = Object.FindAnyObjectByType<CombatTargetSelectionController>();
            }
            if (inspectController == null)
                inspectController = Object.FindAnyObjectByType<CombatUnitInspectController>();
        }

        private static readonly Unity.Profiling.ProfilerMarker CombatViewsLateSample = new("Godmist.Combat.ViewsLate");
        private void LateUpdate()
        {
            using var sample = CombatViewsLateSample.Auto();
            TickWorldEffects();
            TickPersistentAreaVfx();
            if (dungeonSession != null) { SynchronizeDungeon(Time.deltaTime); return; }
            var session = sandboxBootstrap?.Session;

            if (session == null)
            {
                return;
            }

            if (boundSession != session)
            {
                ClearViews();
                boundSession = session;
            }

            EnsureViews(session);
            SynchronizeViews(session);
            RemoveMissingViews(session);
            EnsureSummonedObjectViews(session);
            SynchronizeSummonedObjectViews(session);
            RemoveMissingSummonedObjectViews(session);
            SynchronizeKnifeViews(session);
        }

        private void EnsureViews(CombatSession session)
        {
            if (battleUnitPrefab == null)
            {
                Debug.LogError("CombatUnitViewSpawner: Battle Unit Prefab is not assigned.");
                return;
            }

            foreach (var unit in session.Units)
            {
                if (unit == null || views.ContainsKey(unit) || borrowed.ContainsKey(unit))
                {
                    continue;
                }

                if (unit.Character == null
                    || unit.Character.Definition == null)
                {
                    continue;
                }

                var view = Instantiate(battleUnitPrefab, unitsRoot);
                view.Bind(unit);
                view.BindTerrainView(floorMap);
                if (dungeonSession != null)
                {
                    // Existing combatants borrow exploration views (order 10).
                    // Fresh summons use PF_BattleUnit (order 0), which can be
                    // covered by the dungeon floor tilemap at the same order.
                    foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
                        renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 10);
                }
                if (view.GetComponent<CombatUnitVisualStateView>() == null)
                    view.gameObject.AddComponent<CombatUnitVisualStateView>();
                view.SetWorldPosition(UnitWorldPosition(unit, unit.Position), true);
                views.Add(unit, view);
                floorMap?.RegisterActorLightingPresentation(view.BodyRenderer);
            }
        }

        private void SynchronizeViews(CombatSession session)
        {
            foreach (var unit in session.Units)
            {
                if (unit == null || !views.TryGetValue(unit, out var view))
                {
                    continue;
                }

                if (movementPaths.ContainsKey(unit)) AdvancePresentationPath(unit, Time.deltaTime);
                else if (!presentationMovingUnits.Contains(unit))
                {
                    view.SetWorldPosition(UnitWorldPosition(unit, unit.Position));
                }
                view.Refresh();
                if (Godmist.Views.UI.CombatDownedIndicatorView.ShouldShow(unit) && downedStyle == null && hud != null)
                    downedStyle = hud.GetComponentInChildren<Godmist.Views.UI.Workspace.UIWorkspaceStyleApplier>(true)?.Palette;
                view.RefreshDownedIndicator(downedStyle);
                view.SetNameLabelVisible(false);
                view.SetElevationVisible(IsUnitVisible(unit));
                view.GetComponent<CombatUnitVisualStateView>()?.Refresh(
                    unit,
                    session.ActiveUnit == unit
                        && unit.Character?.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive,
                    (targetSelectionController != null && targetSelectionController.HoveredUnit == unit)
                    || (inspectController != null && (inspectController.HoveredUnit == unit || inspectController.InspectedUnit == unit))
                        || (hud != null && hud.HighlightedUnit == unit),
                    targetSelectionController != null && targetSelectionController.HoveredUnit == unit && !unit.IsPlayerSide && targetSelectionController.IsSelectingTarget,
                    preserveBodyColors: true);
            }
        }

        private void RemoveMissingViews(CombatSession session)
        {
            var existingUnits = new HashSet<BattleUnit>(session.Units);
            var unitsToRemove = new List<BattleUnit>();

            foreach (var pair in views)
            {
                if (!existingUnits.Contains(pair.Key))
                {
                    if (pair.Value != null && pair.Value.HasCombatPose) continue;
                    if (pair.Value != null)
                    {
                        floorMap?.UnregisterLightingPresentation(pair.Value.BodyRenderer);
                        DestroyPresentationObject(pair.Value.gameObject);
                    }

                    unitsToRemove.Add(pair.Key);
                }
            }

            foreach (var unit in unitsToRemove)
            {
                views.Remove(unit);
                owners.Remove(unit);
                movementTargets.Remove(unit);
                presentationMovingUnits.Remove(unit);
                movementPaths.Remove(unit);
            }
        }

        private void ClearViews()
        {
            ClearWorldEffects();
            foreach (var view in views.Values)
            {
                if (view != null)
                {
                    floorMap?.UnregisterLightingPresentation(view.BodyRenderer);
                    DestroyPresentationObject(view.gameObject);
                }
            }

            views.Clear();
            presentationMovingUnits.Clear();
            movementPaths.Clear();
            movementTargets.Clear();

            foreach (var renderer in summonedObjectViews.Values)
            {
                if (renderer != null)
                {
                    floorMap?.UnregisterLightingPresentation(renderer);
                    DestroyPresentationObject(renderer.gameObject);
                }
            }

            summonedObjectViews.Clear();

            foreach (var renderer in knifeViews.Values)
            {
                floorMap?.UnregisterLightingPresentation(renderer);
                if (renderer != null) DestroyPresentationObject(renderer.gameObject);
            }
            knifeViews.Clear();
            knifeHighlightViews.Clear();
            pointerHoveredKnives.Clear();
            counterHighlightedKnives.Clear();
        }

        private void EnsureSummonedObjectViews(CombatSession session)
        {
            foreach (var summonedObject in session.SummonedObjects)
            {
                if (summonedObject == null
                    || summonedObjectViews.ContainsKey(summonedObject))
                {
                    continue;
                }

                var objectName = summonedObject.Definition?.DisplayName;

                if (string.IsNullOrWhiteSpace(objectName))
                {
                    objectName = "Summoned Object";
                }

                var viewObject = new GameObject($"Summoned Object - {objectName}");
                viewObject.transform.SetParent(summonedObjectsRoot, false);
                viewObject.transform.localScale = Vector3.one * (summonedObject.SourceSkillId == Godmist.Systems.Combat.SkeletonSlayerBossSystem.HammerId
                    ? 1f : summonedObjectScale);

                var renderer = viewObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = summonedObjectSortingOrder;

                summonedObjectViews.Add(summonedObject, renderer);
                if (summonedObject.SourceSkillId != Godmist.Systems.Combat.ForestEntBossSystem.SanctuarySkillId
                    || summonedObject.Definition?.ObjectId != "object.forest.ent.fire")
                    floorMap?.RegisterGroundLightingPresentation(renderer);
            }
        }

        private void SynchronizeSummonedObjectViews(CombatSession session)
        {
            foreach (var summonedObject in session.SummonedObjects)
            {
                if (summonedObject == null
                    || !summonedObjectViews.TryGetValue(
                        summonedObject,
                        out var renderer)
                    || renderer == null)
                {
                    continue;
                }

                renderer.transform.position = GridToWorldPosition(summonedObject.Position)
                    + (Vector3)(summonedObject.Definition?.WorldSpriteOffset ?? Vector2.zero);
                renderer.forceRenderingOff = summonedObject.Definition?.HiddenUntilTriggered==true || !IsPositionVisible(session, summonedObject.Position)
                    || (Godmist.Systems.Combat.PaladinCombatSystem.IsField(summonedObject)
                        && Godmist.Views.Characters.CombatTargetVfxResolver.TryResolve("judgement", out _));

                var sanctuaryFire = summonedObject.SourceSkillId == Godmist.Systems.Combat.ForestEntBossSystem.SanctuarySkillId
                    && summonedObject.Definition?.ObjectId == "object.forest.ent.fire"
                    && CombatTargetVfxResolver.TryResolve("sanctuary-flame", out _);
                var worldSprite = summonedObject.Definition?.WorldSprite;
                if (sanctuaryFire && CombatTargetVfxResolver.TryResolve("sanctuary-flame", out var flame))
                {
                    worldSprite = flame.FrameAt(Time.time + (summonedObject.Position.X * .13f + summonedObject.Position.Y * .07f), true);
                    renderer.transform.position = GridToWorldPosition(summonedObject.Position) + (Vector3)flame.Offset;
                }
                // Authored 32-PPU art is already sized for the world grid. The
                // legacy .7 multiplier turns it into effective 45.7 PPU.
                renderer.transform.localScale = worldSprite != null && Mathf.Approximately(worldSprite.pixelsPerUnit, 32f)
                    ? Vector3.one : Vector3.one * (summonedObject.SourceSkillId == Godmist.Systems.Combat.SkeletonSlayerBossSystem.HammerId ? 1f : summonedObjectScale);
                renderer.sprite = worldSprite != null
                    ? worldSprite
                    : GetMissingSprite();

                renderer.color = worldSprite != null
                    ? Color.white
                    : missingSpriteColor;
            }
        }

        private void RemoveMissingSummonedObjectViews(CombatSession session)
        {
            var existingObjects = new HashSet<SummonedCombatMapObject>(
                session.SummonedObjects);

            var objectsToRemove = new List<SummonedCombatMapObject>();

            foreach (var pair in summonedObjectViews)
            {
                if (existingObjects.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    floorMap?.UnregisterLightingPresentation(pair.Value);
                    DestroyPresentationObject(pair.Value.gameObject);
                }

                objectsToRemove.Add(pair.Key);
            }

            foreach (var summonedObject in objectsToRemove)
            {
                summonedObjectViews.Remove(summonedObject);
            }
        }

        private void SynchronizeKnifeViews(CombatSession session, bool removeMissing = true)
        {
            var activeKnives = new HashSet<KnifeCombatMapObject>();
            foreach (var mapObject in session.MapObjects)
            {
                if (mapObject is not KnifeCombatMapObject knife) continue;
                activeKnives.Add(knife);
                var destination = GridToWorldPosition(knife.Position)
                    + (Vector3)DungeonCatacombsObjectVisuals.GroundDaggerPresentationOffset;
                if (!knifeViews.TryGetValue(knife, out var renderer) || renderer == null)
                {
                    var marker = new GameObject("Thrown Knife");
                    marker.transform.SetParent(unitsRoot, false);
                    renderer = marker.AddComponent<SpriteRenderer>();
                    Sprite catacombsSprite = null;
                    var hasCatacombsSprite = DungeonCatacombsObjectVisuals.TryLoadPresentationSprite(
                        "catacombs-ground-dagger-v2",
                        new Vector2(.5f, 0f),
                        out catacombsSprite);
                    renderer.sprite = hasCatacombsSprite ? catacombsSprite : GetMissingSprite();
                    renderer.color = hasCatacombsSprite
                        ? Color.white
                        : new Color(1f, 0.84f, 0.18f, 1f);
                    marker.transform.localScale = Vector3.one * (hasCatacombsSprite
                        ? DungeonCatacombsObjectVisuals.GroundDaggerPresentationScale
                        : .28f);
                    renderer.sortingOrder = 2;
                    floorMap?.RegisterGroundLightingPresentation(renderer, true);
                    knifeViews[knife] = renderer;
                    var knifeHighlight = marker.AddComponent<CombatUnitVisualStateView>();
                    knifeHighlight.SetOutlineWidth(RecoverableKnifeOutlineWidth);
                    knifeHighlightViews[knife] = knifeHighlight;
                }
                PlaceGroundKnife(knife, renderer, destination);
                // Keep the silhouette and blade atomic above the floor. Without
                // a group the outline's bodyOrder-1 can fall into the floor band.
                var knifeGroup = renderer.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (knifeGroup == null) knifeGroup = renderer.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                knifeGroup.sortingLayerID = floorMap != null ? floorMap.FloorFeedbackSortingLayerID : renderer.sortingLayerID;
                knifeGroup.sortingOrder = floorMap != null ? floorMap.GetDungeonObjectDepthSortingOrder() : renderer.sortingOrder;
                renderer.forceRenderingOff = !IsPositionVisible(session, knife.Position);
                var highlightOwner = hud?.KnifeHighlightOwner;
                if (highlightOwner != null
                    && string.Equals(knife.OwnerCharacterId, highlightOwner.Character?.CharacterId,
                        System.StringComparison.OrdinalIgnoreCase))
                    counterHighlightedKnives.Add(knife);
                else counterHighlightedKnives.Remove(knife);
                RefreshKnifeHighlight(knife);
            }
            if (removeMissing) RemoveMissingKnifeViews(session);
        }
        private void RemoveMissingKnifeViews(CombatSession session)
        {
            var activeKnives = new HashSet<CombatMapObject>(session.MapObjects);
            var removed = new List<KnifeCombatMapObject>();
            foreach (var pair in knifeViews)
            {
                if (activeKnives.Contains(pair.Key)) continue;
                floorMap?.UnregisterLightingPresentation(pair.Value);
                if (pair.Value != null) DestroyPresentationObject(pair.Value.gameObject);
                removed.Add(pair.Key);
            }
            foreach (var knife in removed)
            {
                knifeViews.Remove(knife);
                knifeHighlightViews.Remove(knife);
                pointerHoveredKnives.Remove(knife);
                counterHighlightedKnives.Remove(knife);
            }
        }

        private Sprite GetMissingSprite()
        {
            if (missingSprite != null)
            {
                return missingSprite;
            }

            missingSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

            missingSprite.name = "Missing Summoned Object Sprite";

            return missingSprite;
        }

        private static void DestroyPresentationObject(GameObject value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ClearWorldEffects();
            foreach (var renderer in summonedObjectViews.Values) floorMap?.UnregisterLightingPresentation(renderer);
            foreach (var renderer in knifeViews.Values) floorMap?.UnregisterLightingPresentation(renderer);
            foreach (var view in views.Values)
                if (view != null) floorMap?.UnregisterLightingPresentation(view.BodyRenderer);
            if (missingSprite != null)
            {
                Destroy(missingSprite);
            }
        }

        private Vector3 GridToWorldPosition(GridPosition gridPosition)
        {
            if (floorMap != null && boundSession?.DungeonContext != null)
                return WorldPosition(boundSession, gridPosition);
            return new Vector3(
                gridOrigin.x + gridPosition.X * cellSize,
                gridOrigin.y + gridPosition.Y * cellSize,
                0f);
        }
    }
}
