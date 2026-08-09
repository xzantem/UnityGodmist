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

        [field: SerializeField]
        public Vector2Int SizeInCells { get; private set; } = new(8, 8);
        [field: Header("Structural anchors")]
        [field: SerializeField]
        public List<DungeonStructuralAnchorData> StructuralAnchors { get; private set; } = new();

        private void OnValidate()
        {
            SizeInCells = new Vector2Int(
                Mathf.Max(1, SizeInCells.x),
                Mathf.Max(1, SizeInCells.y));
        }
    }
}