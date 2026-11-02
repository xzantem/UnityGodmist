using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    /// <summary>Equipment available to item services: active party and campaign stash.</summary>
    public static class EquipmentServiceOwnership
    {
        public static IEnumerable<EquipmentItemInstance> GetAccessibleEquipment(GameSession session)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in ActiveCharacters(session))
            {
                foreach (var item in character.Inventory?.Items?.OfType<EquipmentItemInstance>()
                             ?? Enumerable.Empty<EquipmentItemInstance>())
                    if (IsWeaponOrArmor(item) && seen.Add(item.InstanceId)) yield return item;
                foreach (var item in character.Equipment?.GetEquippedItems()
                             ?? Enumerable.Empty<EquipmentItemInstance>())
                    if (IsWeaponOrArmor(item) && seen.Add(item.InstanceId)) yield return item;
            }
            foreach (var item in session?.Stash?.Items?.OfType<EquipmentItemInstance>()
                         ?? Enumerable.Empty<EquipmentItemInstance>())
                if (IsWeaponOrArmor(item) && seen.Add(item.InstanceId)) yield return item;
        }

        public static bool Contains(GameSession session, EquipmentItemInstance item)
            => item != null && GetAccessibleEquipment(session).Any(owned => ReferenceEquals(owned, item));

        public static CharacterInstance FindEquippedOwner(GameSession session, EquipmentItemInstance item)
            => item == null ? null : ActiveCharacters(session).FirstOrDefault(character =>
                character.Equipment?.GetEquippedItems().Any(owned => ReferenceEquals(owned, item)) == true);

        private static IEnumerable<CharacterInstance> ActiveCharacters(GameSession session)
        {
            foreach (var id in session?.PlayerParty?.ActiveMemberIds ?? Enumerable.Empty<string>())
                if (session.CharactersById.TryGetValue(id, out var character) && character != null)
                    yield return character;
        }

        private static bool IsWeaponOrArmor(EquipmentItemInstance item)
            => item?.Definition?.ItemType is ItemType.Weapon or ItemType.Armor;
    }
}
