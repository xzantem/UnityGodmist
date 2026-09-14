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
            DungeonEdgeDirection direction)
        {
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
                    && anchor.LocalCell == localCell && anchor.Direction == direction) return false;

            if (!BoundaryEdgesConfigured)
            {
                return true;
            }

            return DungeonRoomBoundaryUtility.ContainsEdge(
                AllowedBoundaryEdges,
                localCell,
                direction);
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

        public DungeonContentGroupData() { }

        public DungeonContentGroupData(string id, IEnumerable<string> anchorIds,
            bool isRequired = false, float spawnChance = 1f)
        {
            Id = id;
            AnchorIds = new List<string>(anchorIds);
            IsRequired = isRequired;
            SpawnChance = spawnChance;
        }
    }
}
