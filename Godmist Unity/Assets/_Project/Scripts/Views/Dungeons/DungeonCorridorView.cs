using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonCorridorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer corridorCellPrefab;

        private readonly List<GameObject> spawnedCells = new();

        public bool HasWorldBounds { get; private set; }
        public Bounds WorldBounds { get; private set; }

        public void Bind(IReadOnlyList<Vector2Int> layoutPath)
        {
            Clear();

            if (corridorCellPrefab == null || layoutPath == null)
            {
                return;
            }

            var uniqueCells = new HashSet<Vector2Int>();

            foreach (var cell in layoutPath)
            {
                if (!uniqueCells.Add(cell))
                {
                    continue;
                }

                var cellView = Instantiate(
                    corridorCellPrefab,
                    transform);

                cellView.transform.position = new Vector3(
                    cell.x + 0.5f,
                    cell.y + 0.5f,
                    0f);

                cellView.transform.localScale = Vector3.one;

                spawnedCells.Add(cellView.gameObject);

                var cellBounds = new Bounds(
                    cellView.transform.position,
                    Vector3.one);

                AddWorldBounds(cellBounds);
            }
        }

        private void Clear()
        {
            foreach (var spawnedCell in spawnedCells)
            {
                if (spawnedCell != null)
                {
                    Destroy(spawnedCell);
                }
            }

            spawnedCells.Clear();
            HasWorldBounds = false;
        }

        private void AddWorldBounds(Bounds bounds)
        {
            if (!HasWorldBounds)
            {
                WorldBounds = bounds;
                HasWorldBounds = true;
                return;
            }

            WorldBounds.Encapsulate(bounds);
        }
    }
}