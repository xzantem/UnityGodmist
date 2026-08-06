using System.Collections.Generic;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "DungeonRoomTemplateCatalog",
        menuName = "Godmist/Dungeons/Room Template Catalog")]
    public class DungeonRoomTemplateCatalog : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;

        [field: SerializeField]
        public List<DungeonRoomTemplateDefinition> Templates { get; private set; } = new();
    }
}