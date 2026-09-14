using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Views.Characters;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatUnitViewSpawner : MonoBehaviour
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
        private readonly HashSet<BattleUnit> presentationMovingUnits = new();

        // A CombatSession is runtime state and may contain nested counterattack payloads.
        // It must never enter Unity's scene/prefab serialization graph.
        [System.NonSerialized] private CombatSession boundSession;
        private Sprite missingSprite;
        private Godmist.Runtime.Game.GameSession dungeonSession;
        private Godmist.Views.Dungeons.DungeonFloorMapView floorMap;
        [SerializeField] private Godmist.Views.UI.CombatHudView hud;
        private readonly Dictionary<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView> borrowed = new();
        private readonly Dictionary<BattleUnit, CombatSession> owners = new();
        private readonly Dictionary<BattleUnit, GridPosition> movementTargets = new();
        public void ConfigureDungeon(Godmist.Runtime.Game.GameSession game, Godmist.Views.Dungeons.DungeonFloorMapView map)
        { dungeonSession = game; floorMap = map; }
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
        }
        private bool IsPositionVisible(CombatSession session, GridPosition position) => floorMap == null || session.DungeonContext == null
            || floorMap.IsTraversalVisible(Godmist.Systems.Combat.DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, position));
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
                if (view == null || !view.gameObject.activeSelf) continue;
                var target = movementTargets.TryGetValue(unit, out var step) ? step : unit.Position;
                view.MoveTowards(UnitWorldPosition(unit, target), 6f * deltaTime, 0f);
                var owner = owners[unit];
                var isFocused = sandboxBootstrap.Session == owner;
                view.RefreshCombatPresentation(owner.ActiveUnit == unit,
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
            if (unit == null || floorMap == null) return true;
            if (owners.TryGetValue(unit, out var session)) return IsPositionVisible(session, unit.Position);
            if (ExplorationPresentationVisible != null) return ExplorationPresentationVisible(unit);
            var world = ExplorationWorldPosition?.Invoke(unit);
            return !world.HasValue || !floorMap.IsSurfaceOccluded(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(floorMap.WorldToCell(world.Value)));
        }

        public System.Func<BattleUnit, bool> ExplorationPresentationVisible { get; set; }
        public System.Func<BattleUnit, Vector3?> ExplorationWorldPosition { get; set; }

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

        public bool TryBeginPresentationMovement(BattleUnit unit)
        {
            if (unit == null || (!views.ContainsKey(unit) && !borrowed.ContainsKey(unit))) return false;
            presentationMovingUnits.Add(unit);
            return true;
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

            view.SetWorldPosition(UnitWorldPosition(unit, targetPosition));
            return true;
        }

        public bool IsPresentationMovementInProgress(BattleUnit unit)
        {
            if (unit != null && borrowed.TryGetValue(unit, out var existing) && existing != null
                && movementTargets.TryGetValue(unit, out var target))
                return Vector2.Distance(existing.Position, UnitWorldPosition(unit, target)) > 0.01f;
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
            if (views.TryGetValue(unit, out var view) && view != null)
            {
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

        private void LateUpdate()
        {
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
                if (view.GetComponent<CombatUnitVisualStateView>() == null)
                    view.gameObject.AddComponent<CombatUnitVisualStateView>();
                view.SetWorldPosition(UnitWorldPosition(unit, unit.Position), true);
                views.Add(unit, view);
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

                if (!presentationMovingUnits.Contains(unit))
                {
                    view.SetWorldPosition(UnitWorldPosition(unit, unit.Position));
                }
                view.Refresh();
                view.SetNameLabelVisible(false);
                view.SetElevationVisible(IsPositionVisible(session, unit.Position));
                view.GetComponent<CombatUnitVisualStateView>()?.Refresh(
                    unit,
                    session.ActiveUnit == unit,
                    (targetSelectionController != null && targetSelectionController.HoveredUnit == unit)
                    || (inspectController != null && (inspectController.HoveredUnit == unit || inspectController.InspectedUnit == unit))
                        || (hud != null && hud.HighlightedUnit == unit),
                    targetSelectionController != null && targetSelectionController.HoveredUnit == unit && !unit.IsPlayerSide && targetSelectionController.IsSelectingTarget);
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
                    if (pair.Value != null)
                    {
                        Destroy(pair.Value.gameObject);
                    }

                    unitsToRemove.Add(pair.Key);
                }
            }

            foreach (var unit in unitsToRemove)
            {
                views.Remove(unit);
                owners.Remove(unit);
                presentationMovingUnits.Remove(unit);
            }
        }

        private void ClearViews()
        {
            foreach (var view in views.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            views.Clear();
            presentationMovingUnits.Clear();

            foreach (var renderer in summonedObjectViews.Values)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }

            summonedObjectViews.Clear();

            foreach (var renderer in knifeViews.Values)
            {
                if (renderer != null) Destroy(renderer.gameObject);
            }
            knifeViews.Clear();
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
                viewObject.transform.localScale = Vector3.one * summonedObjectScale;

                var renderer = viewObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = summonedObjectSortingOrder;

                summonedObjectViews.Add(summonedObject, renderer);
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

                renderer.transform.position = GridToWorldPosition(summonedObject.Position);
                renderer.forceRenderingOff = !IsPositionVisible(session, summonedObject.Position);

                var worldSprite = summonedObject.Definition?.WorldSprite;
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
                    Destroy(pair.Value.gameObject);
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
                if (!knifeViews.TryGetValue(knife, out var renderer) || renderer == null)
                {
                    var marker = new GameObject("Thrown Knife");
                    marker.transform.SetParent(unitsRoot, false);
                    marker.transform.localScale = Vector3.one * 0.28f;
                    renderer = marker.AddComponent<SpriteRenderer>();
                    renderer.sprite = GetMissingSprite();
                    renderer.color = new Color(1f, 0.84f, 0.18f, 1f);
                    renderer.sortingOrder = 2;
                    knifeViews[knife] = renderer;
                }
                renderer.transform.position = GridToWorldPosition(knife.Position);
                renderer.forceRenderingOff = !IsPositionVisible(session, knife.Position);
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
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                removed.Add(pair.Key);
            }
            foreach (var knife in removed) knifeViews.Remove(knife);
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

        private void OnDestroy()
        {
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
