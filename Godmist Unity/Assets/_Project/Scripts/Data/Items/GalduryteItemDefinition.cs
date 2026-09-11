using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "GalduryteItemDefinition",
        menuName = "Godmist/Items/Galduryte Item Definition")]
    public sealed class GalduryteItemDefinition : ItemDefinition
    {
        private void OnValidate()
        {
            ItemType = ItemType.Galduryte;
        }
    }
}
