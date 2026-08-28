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

        private void OnValidate()
        {
            SizeInCells = new Vector2Int(
                Mathf.Max(1, SizeInCells.x),
                Mathf.Max(1, SizeInCells.y));

            Footprint ??= new DungeonRoomFootprint();
            AllowedBoundaryEdges ??= new List<DungeonRoomBoundaryEdgeData>();

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
    }
}
