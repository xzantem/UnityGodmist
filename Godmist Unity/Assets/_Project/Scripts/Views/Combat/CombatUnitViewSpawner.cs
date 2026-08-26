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

        public bool TryGetWorldPosition(BattleUnit unit, out Vector3 position)
        {
            if (unit != null && views.TryGetValue(unit, out var view) && view != null)
            {
                position = view.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryGetWorldPosition(GridPosition gridPosition, out Vector3 position)
        {
            position = GridToWorldPosition(gridPosition);
            return true;
        }

        public bool TryBeginPresentationMovement(BattleUnit unit)
        {
            if (unit == null || !views.ContainsKey(unit)) return false;
            presentationMovingUnits.Add(unit);
            return true;
        }

        public bool TryMovePresentationStep(BattleUnit unit, GridPosition targetPosition)
        {
            if (unit == null
                || !presentationMovingUnits.Contains(unit)
                || !views.TryGetValue(unit, out var view)
                || view == null)
            {
                return false;
            }

            view.SetWorldPosition(GridToWorldPosition(targetPosition));
            return true;
        }

        public bool IsPresentationMovementInProgress(BattleUnit unit)
        {
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
            if (views.TryGetValue(unit, out var view) && view != null)
            {
                view.SetWorldPosition(GridToWorldPosition(unit.Position));
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
                if (unit == null || views.ContainsKey(unit))
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
                view.SetWorldPosition(GridToWorldPosition(unit.Position), true);
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
                    view.SetWorldPosition(GridToWorldPosition(unit.Position));
                }
                view.Refresh();
                view.SetNameLabelVisible(false);
                view.GetComponent<CombatUnitVisualStateView>()?.Refresh(
                    unit,
                    session.ActiveUnit == unit,
                    (targetSelectionController != null && targetSelectionController.HoveredUnit == unit)
                    || (inspectController != null && inspectController.HoveredUnit == unit),
                    targetSelectionController != null && targetSelectionController.IsSelectingTarget);
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

        private void SynchronizeKnifeViews(CombatSession session)
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
            }
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
            return new Vector3(
                gridOrigin.x + gridPosition.X * cellSize,
                gridOrigin.y + gridPosition.Y * cellSize,
                0f);
        }
    }
}
