using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "ItemDefinition",
        menuName = "Godmist/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public string Description { get; private set; } = string.Empty;
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public ItemType ItemType { get; protected set; }
        [field: SerializeField] public MaterialDiscipline MaterialDiscipline { get; private set; }
        [field: SerializeField] public RarityTier BaseRarity { get; private set; }
        [field: SerializeField] public int BaseValue { get; private set; }
        [field: SerializeField] public float Weight { get; private set; }
        [field: SerializeField] public bool IsStackable { get; private set; }
    }
}
