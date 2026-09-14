using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "DungeonRoomTemplate",
        menuName = "Godmist/Dungeons/Room Template")]
    public class DungeonRoomTemplateDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public DungeonRoomType RoomType { get; private set; }

        [field: Header("Progression branch")]
        [field: Tooltip("Szablon moze zostac wybrany jako wymagany pokoj odblokowujacy dalsza trase lochu.")]
        [field: SerializeField]
        public bool CanBeRequiredProgressionBranch { get; private set; }

        [field: SerializeField]
        public Vector2Int SizeInCells { get; private set; } = new(8, 8);

        [field: Header("Allowed rotations")]
        [field: Tooltip("Dopuszczalne obroty instancji szablonu w stopniach. Puste = wszystkie cztery obroty.")]
        [field: SerializeField]
        public List<int> AllowedRotationDegrees { get; private set; } = new()
        {
            0,
            90,
            180,
            270
        };

        [field: Header("Room footprint")]
        [field: Tooltip("Puste = pelny prostokat. Wpisz lokalne komorki podlogi, aby zdefiniowac dowolny ksztalt pokoju.")]
        [field: SerializeField]
        public DungeonRoomFootprint Footprint { get; private set; } = new();

        // Empty preserves legacy level-zero footprint. Otherwise this is the complete surface set.
        [field: SerializeField] public List<DungeonRoomSurfaceData> Surfaces { get; private set; } = new();
        [field: SerializeField] public List<DungeonRoomStairData> Stairs { get; private set; } = new();

        public IEnumerable<DungeonRoomSurfaceData> EnumerateSurfaces()
        {
            if (Surfaces != null && Surfaces.Count > 0)
            {
                foreach (var surface in Surfaces) yield return surface;
            }
            else
                foreach (var cell in (Footprint ?? new DungeonRoomFootprint()).Enumerate(SizeInCells))
                    yield return new DungeonRoomSurfaceData(cell);
        }

        public bool ContainsSurface(Vector2Int cell, int level)
            => Surfaces == null || Surfaces.Count == 0
                ? level == 0 && (Footprint ?? new DungeonRoomFootprint()).Contains(cell, SizeInCells)
                : Surfaces.Any(s => s != null && s.LocalCell == cell && s.Level == level);

        public void ValidateSurfaceGeometry()
        {
            var footprint = new HashSet<Vector2Int>((Footprint ?? new DungeonRoomFootprint()).Enumerate(SizeInCells));
            var keys = new HashSet<(Vector2Int, int)>();
            foreach (var surface in EnumerateSurfaces())
                if (surface == null || !footprint.Contains(surface.LocalCell) || !keys.Add((surface.LocalCell, surface.Level)))
                    throw new InvalidOperationException($"Szablon '{Id}': powierzchnia null, poza footprintem lub duplikat (pole, poziom).");
            var projectedCells = keys.Select(k => k.Item1).ToHashSet();
            var stairs = new HashSet<((Vector2Int, int), (Vector2Int, int))>();
            foreach (var stair in Stairs ?? new List<DungeonRoomStairData>())
            {
                if (stair?.First == null || stair.Second == null || stair.First.Level == stair.Second.Level)
                    throw new InvalidOperationException($"Szablon '{Id}': schody wymagaja dwoch koncow na roznych poziomach.");
                var first = (stair.First.LocalCell, stair.First.Level);
                var second = (stair.Second.LocalCell, stair.Second.Level);
                if (!keys.Contains(first) || !keys.Contains(second) || stairs.Contains((second, first)) || !stairs.Add((first, second)))
                    throw new InvalidOperationException($"Szablon '{Id}': schody maja brakujaca powierzchnie albo powtorzone polaczenie.");
                if (stair.PathCells != null && stair.PathCells.Count > 0)
                {
                    if (!Godmist.Core.Utilities.DungeonStairPathUtility.TryResolve(stair.First.LocalCell, stair.Second.LocalCell,
                            stair.PathCells, out var path, out var error))
                        throw new InvalidOperationException($"Szablon '{Id}': {error}");
                    foreach (var cell in path)
                    {
                        if (cell.x < 0 || cell.y < 0 || cell.x >= SizeInCells.x || cell.y >= SizeInCells.y)
                            throw new InvalidOperationException($"Szablon '{Id}': pole schodów {cell} poza rozmiarem pokoju.");
                        projectedCells.Add(cell);
                    }
                }
            }
            if (!footprint.SetEquals(projectedCells))
                throw new InvalidOperationException($"Szablon '{Id}': rzut powierzchni i schodów musi pokrywać cały footprint.");
            var exits = new HashSet<(Vector2Int, int, DungeonEdgeDirection)>();
            if (BoundaryEdgesConfigured)
                foreach (var edge in AllowedBoundaryEdges ?? new List<DungeonRoomBoundaryEdgeData>())
                    if (edge == null || !Enum.IsDefined(typeof(DungeonEdgeDirection), edge.Direction)
                        || !keys.Contains((edge.LocalCell, edge.Level))
                        || !DungeonRoomBoundaryUtility.IsExternalBoundaryEdge(edge.LocalCell, edge.Direction, SizeInCells, footprint)
                        || !exits.Add((edge.LocalCell, edge.Level, edge.Direction)))
                        throw new InvalidOperationException($"Szablon '{Id}': nieprawidlowe lub powtorzone wyjscie (pole, poziom, kierunek).");
            foreach (var anchor in StructuralAnchors ?? new List<DungeonStructuralAnchorData>())
                if (anchor != null && !keys.Contains((anchor.LocalCell, anchor.Level)))
                    throw new InvalidOperationException($"Szablon '{Id}': kotwica strukturalna {anchor.LocalCell}, poziom {anchor.Level} nie ma powierzchni.");
        }

        [field: Header("External passage edges")]
        [field: Tooltip("Jawna lista zewnetrznych krawedzi, na ktorych generator moze utworzyc przejscie.")]
        [field: SerializeField]
        public List<DungeonRoomBoundaryEdgeData> AllowedBoundaryEdges { get; private set; } = new();

        [field: SerializeField]
        public bool BoundaryEdgesConfigured { get; private set; }

        [field: Header("Structural anchors")]
        [field: SerializeField]
        public List<DungeonStructuralAnchorData> StructuralAnchors { get; private set; } = new();

        [field: Header("Semantic content anchors")]
        [field: SerializeField]
        public List<DungeonSemanticAnchorData> SemanticAnchors { get; private set; } = new();

        [field: Header("Atomic content groups")]
        [field: SerializeField]
        public List<DungeonContentGroupData> ContentGroups { get; private set; } = new();

        [field: SerializeField] public List<Vector2Int> DifficultTerrainCells { get; private set; } = new();

        private void OnValidate()
        {
            SizeInCells = new Vector2Int(
                Mathf.Max(1, SizeInCells.x),
                Mathf.Max(1, SizeInCells.y));

            Footprint ??= new DungeonRoomFootprint();
            AllowedBoundaryEdges ??= new List<DungeonRoomBoundaryEdgeData>();
            NormalizeAllowedRotations();

            MigrateLegacyBoundaryEdges();

            if (SemanticAnchors == null) return;

            foreach (var anchor in SemanticAnchors)
            {
                anchor?.Normalize();
            }
        }

        public bool AllowsBoundaryEdge(
            Vector2Int localCell,
            DungeonEdgeDirection direction, int level = 0)
        {
            if (!ContainsSurface(localCell, level)) return false;
            var floorCells = new HashSet<Vector2Int>(
                (Footprint ?? new DungeonRoomFootprint()).Enumerate(SizeInCells));

            if (!DungeonRoomBoundaryUtility.IsExternalBoundaryEdge(
                    localCell,
                    direction,
                    SizeInCells,
                    floorCells))
            {
                return false;
            }

            foreach (var anchor in StructuralAnchors ?? new List<DungeonStructuralAnchorData>())
                if (anchor != null && anchor.Type == DungeonStructuralAnchorType.Wall
                    && anchor.LocalCell == localCell && anchor.Level == level && anchor.Direction == direction) return false;

            if (!BoundaryEdgesConfigured)
            {
                return true;
            }

            return AllowedBoundaryEdges != null && AllowedBoundaryEdges.Any(edge => edge != null
                && edge.LocalCell == localCell && edge.Direction == direction && edge.Level == level);
        }

        public void MigrateLegacyBoundaryEdges()
        {
            if (BoundaryEdgesConfigured)
            {
                return;
            }

            AllowedBoundaryEdges = new List<DungeonRoomBoundaryEdgeData>(
                DungeonRoomBoundaryUtility.EnumerateExternalBoundaryEdges(this));
            BoundaryEdgesConfigured = true;
        }

        public IReadOnlyList<int> GetAllowedRotationDegrees()
        {
            if (AllowedRotationDegrees == null
                || AllowedRotationDegrees.Count == 0)
            {
                return DungeonRoomTransformUtility.AllRotations;
            }

            return AllowedRotationDegrees;
        }

        private void NormalizeAllowedRotations()
        {
            var normalized = new List<int>();

            foreach (var rotation in AllowedRotationDegrees
                         ?? new List<int>())
            {
                if (!DungeonRoomTransformUtility.IsSupportedRotation(rotation))
                {
                    continue;
                }

                var normalizedRotation = rotation % 360;
                if (normalizedRotation < 0) normalizedRotation += 360;

                if (!normalized.Contains(normalizedRotation))
                {
                    normalized.Add(normalizedRotation);
                }
            }

            normalized.Sort();

            AllowedRotationDegrees = normalized.Count > 0
                ? normalized
                : new List<int>(DungeonRoomTransformUtility.AllRotations);
        }
    }

    [System.Serializable]
    public sealed class DungeonContentGroupData
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public List<string> AnchorIds { get; private set; } = new();
        [field: SerializeField] public bool IsRequired { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float SpawnChance { get; private set; } = 1f;
        [field: SerializeField] public string DistributionKey { get; private set; } = string.Empty;

        public DungeonContentGroupData() { }

        public DungeonContentGroupData(string id, IEnumerable<string> anchorIds,
            bool isRequired = false, float spawnChance = 1f, string distributionKey = null)
        {
            DistributionKey = distributionKey ?? string.Empty;
            Id = id;
            AnchorIds = new List<string>(anchorIds);
            IsRequired = isRequired;
            SpawnChance = spawnChance;
        }
    }
}

namespace Godmist.Data.Dungeons
{
    [Serializable]
    public sealed class DungeonRoomSurfaceData
    {
        [field: SerializeField] public Vector2Int LocalCell { get; private set; }
        [field: SerializeField] public int Level { get; private set; }
        public DungeonRoomSurfaceData() { }
        public DungeonRoomSurfaceData(Vector2Int cell, int level = 0) { LocalCell = cell; Level = level; }
    }
    [Serializable]
    public sealed class DungeonRoomStairData
    {
        [field: SerializeField] public DungeonRoomSurfaceData First { get; private set; }
        [field: SerializeField] public DungeonRoomSurfaceData Second { get; private set; }
        [field: SerializeField] public List<Vector2Int> PathCells { get; private set; } = new();
        public DungeonRoomStairData() { }
        public DungeonRoomStairData(DungeonRoomSurfaceData first, DungeonRoomSurfaceData second, IEnumerable<Vector2Int> pathCells = null)
        { First = first; Second = second; PathCells = pathCells == null ? new List<Vector2Int>() : new List<Vector2Int>(pathCells); }
    }
}
