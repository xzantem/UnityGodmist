using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using System.Collections.Generic;
using Godmist.Data.Characters;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "EquipmentPartDefinition",
        menuName = "Godmist/Items/Equipment Part Definition")]
    public class EquipmentPartDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField, TextArea(2, 4)] public string Description { get; private set; } = string.Empty;
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public CharacterClassData RequiredClass { get; private set; }
        [field: SerializeField] public bool IsStarterOnly { get; private set; }
        [field: SerializeField] public EquipmentPartKind Kind { get; private set; }
        [field: SerializeField] public EquipmentPartLevelBracket LevelBracket { get; private set; }
        [field: SerializeField] public EquipmentPartStats Stats { get; private set; } = new();
        [field: SerializeField] public List<CraftingMaterialRequirement> CraftingMaterials
        {
            get;
            private set;
        } = new();
    }
}
