using UnityEngine;

namespace Godmist.Data.Items
{
    /// <summary>
    /// Static identity for a boss unique. Its runtime uniqueness is represented
    /// by this definition together with Masterpiece quality; no separate
    /// runtime boolean is needed.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BossUniqueEquipmentDefinition",
        menuName = "Godmist/Items/Boss Unique Equipment Definition")]
    public sealed class BossUniqueEquipmentDefinition : EquipmentItemDefinition
    {
        [field: SerializeField] public string BossId { get; private set; } = string.Empty;
    }
}
