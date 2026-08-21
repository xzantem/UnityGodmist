using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
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
        [field: SerializeField] public CharacterClassData RequiredClass { get; private set; }
        [field: SerializeField] public EquipmentPartKind Kind { get; private set; }
        [field: SerializeField] public EquipmentPartLevelBracket LevelBracket { get; private set; }
        [field: SerializeField] public EquipmentPartStats Stats { get; private set; } = new();
    }
}
