using System.Collections.Generic;
using System.Linq;
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
        [field: SerializeField] public bool HasVegetationFringe { get; private set; }

        [field: SerializeField]
        public List<DungeonRoomTemplateDefinition> Templates { get; private set; } = new();

        // Keep legacy templates for saved layouts and isolated fixtures. Only
        // this explicit pool participates in new production route-plan maps.
        [field: SerializeField] public List<DungeonRoomTemplateDefinition> ProductionTemplates { get; private set; } = new();
        [field: SerializeField] public bool UseRoutedConnections { get; private set; }
        public IReadOnlyList<DungeonRoomTemplateDefinition> GenerationTemplates =>
            ProductionTemplates.Count > 0 ? ProductionTemplates : Templates;
        public IEnumerable<DungeonRoomTemplateDefinition> AllTemplates => Templates.Concat(ProductionTemplates).Distinct();
    }
}
