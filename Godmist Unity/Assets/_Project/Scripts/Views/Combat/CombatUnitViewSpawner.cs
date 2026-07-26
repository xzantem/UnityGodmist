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

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;

        private readonly Dictionary<BattleUnit, CharacterView> views = new();
        private CombatSession boundSession;

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

                view.transform.position = GridToWorldPosition(unit.Position);
                view.Refresh();
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