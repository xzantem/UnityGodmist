using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.NPC;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public sealed class GalduryteSocketingPreview
    {
        public int GoldCost;
        public int FreeSockets;
        public bool CanSocket;
        public string FailureReason = string.Empty;
    }

    /// <summary>Owns all validation and state mutation for inserting a galduryte.</summary>
    public sealed class GalduryteSocketingService
    {
        private readonly InventoryService inventory = new();
        private readonly CharacterStatPipelineSystem stats = new();
        private readonly NpcLoyaltyService loyalty = new();

        public GalduryteSocketingPreview CreatePreview(
            GameSession session,
            NpcInstance enchanter,
            EquipmentItemInstance equipment,
            GalduryteItemInstance galduryte)
        {
            var preview = new GalduryteSocketingPreview
            {
                FreeSockets = GetFreeSockets(equipment),
                GoldCost = GetGoldCost(galduryte)
            };
            preview.CanSocket = Validate(session, enchanter, equipment, galduryte, preview, out var reason);
            preview.FailureReason = reason;
            return preview;
        }

        public int GetGoldCost(GalduryteItemInstance galduryte)
        {
            if (galduryte == null) return 0;
            var percent = Mathf.Clamp01(GameDefinesRuntime.GetFloat(
                GameDefineIds.GalduryteSocketingCostPercent, .5f));
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, galduryte.BaseValue) * percent));
        }

        public static int GetFreeSockets(EquipmentItemInstance equipment) => Mathf.Max(
            0,
            (equipment?.SocketCapacity ?? 0) - (equipment?.SocketedGaldurytes?.Count ?? 0));

        public bool TrySocket(
            GameSession session,
            NpcInstance enchanter,
            EquipmentItemInstance equipment,
            GalduryteItemInstance galduryte,
            out int paidGold,
            out string failureReason)
        {
            paidGold = 0;
            var preview = CreatePreview(session, enchanter, equipment, galduryte);
            if (!preview.CanSocket)
            {
                failureReason = preview.FailureReason;
                return false;
            }

            if (!TryFindInventory(session, galduryte, out var sourceInventory))
            {
                failureReason = "Wybrany galduryt nie znajduje się w ekwipunku ani magazynie.";
                return false;
            }

            if (!inventory.TryRemoveItem(sourceInventory, galduryte.InstanceId, out var removed)
                || removed is not GalduryteItemInstance removedGalduryte)
            {
                failureReason = "Nie udało się pobrać wybranego galdurytu.";
                return false;
            }

            if (!session.TrySpendGold(preview.GoldCost))
            {
                inventory.TryAddItem(sourceInventory, removedGalduryte);
                failureReason = "Brakuje złota na osadzenie galdurytu.";
                return false;
            }

            equipment.SocketedGaldurytes ??= new List<GalduryteItemInstance>();
            equipment.SocketedGaldurytes.Add(removedGalduryte);
            loyalty.TryRecordGoldSpent(enchanter, preview.GoldCost);
            RefreshOwnerStats(session, equipment);
            paidGold = preview.GoldCost;
            failureReason = string.Empty;
            return true;
        }

        public IEnumerable<EquipmentItemInstance> GetSocketableEquipment(GameSession session)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in GetPartyCharacters(session))
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

        public IEnumerable<GalduryteItemInstance> GetIdentifiedGaldurytes(GameSession session)
        {
            foreach (var source in GetInventories(session))
                foreach (var item in source.Items?.OfType<GalduryteItemInstance>()
                             ?? Enumerable.Empty<GalduryteItemInstance>())
                    if (item.IsIdentified) yield return item;
        }

        private static bool Validate(
            GameSession session,
            NpcInstance enchanter,
            EquipmentItemInstance equipment,
            GalduryteItemInstance galduryte,
            GalduryteSocketingPreview preview,
            out string reason)
        {
            if (session == null || enchanter?.Definition?.GetRole<EnchanterRoleData>() == null)
            {
                reason = "Brak aktywnej usługi Zaklinacza.";
                return false;
            }
            if (!IsWeaponOrArmor(equipment))
            {
                reason = "Wybierz broń albo pancerz.";
                return false;
            }
            if (galduryte == null || !galduryte.IsIdentified)
            {
                reason = "Wybierz zidentyfikowany galduryt.";
                return false;
            }
            if (preview.FreeSockets <= 0)
            {
                reason = "Wybrane wyposażenie nie ma wolnego socketu.";
                return false;
            }
            var expectedTarget = equipment.Definition.ItemType == ItemType.Weapon
                ? GalduryteTargetType.Weapon
                : GalduryteTargetType.Armor;
            if (galduryte.TargetType != expectedTarget)
            {
                reason = expectedTarget == GalduryteTargetType.Weapon
                    ? "Ten galduryt można osadzić wyłącznie w broni."
                    : "Ten galduryt można osadzić wyłącznie w pancerzu.";
                return false;
            }
            if (equipment.CurrentRarity < galduryte.CurrentRarity)
            {
                reason = "Rzadkość wyposażenia jest zbyt niska dla tego galdurytu.";
                return false;
            }
            if (equipment.ItemLevel <= galduryte.ItemLevel)
            {
                reason = "Poziom wyposażenia musi być wyższy od poziomu galdurytu.";
                return false;
            }
            if (session.Gold < preview.GoldCost)
            {
                reason = "Brakuje złota na osadzenie galdurytu.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool IsWeaponOrArmor(EquipmentItemInstance item) => item?.Definition != null
            && (item.Definition.ItemType == ItemType.Weapon || item.Definition.ItemType == ItemType.Armor);

        private static IEnumerable<Inventory> GetInventories(GameSession session)
        {
            foreach (var character in GetPartyCharacters(session))
                if (character.Inventory != null) yield return character.Inventory;
            if (session?.Stash != null) yield return session.Stash;
        }

        private static IEnumerable<CharacterInstance> GetPartyCharacters(GameSession session)
        {
            foreach (var characterId in session?.PlayerParty?.ActiveMemberIds ?? Enumerable.Empty<string>())
                if (session.CharactersById.TryGetValue(characterId, out var character))
                    yield return character;
        }

        private static bool TryFindInventory(GameSession session, ItemInstance item, out Inventory result)
        {
            result = GetInventories(session).FirstOrDefault(source => source.Items?.Any(
                candidate => ReferenceEquals(candidate, item)
                    || candidate.InstanceId == item?.InstanceId) == true);
            return result != null;
        }

        private void RefreshOwnerStats(GameSession session, EquipmentItemInstance equipment)
        {
            var owner = GetPartyCharacters(session).FirstOrDefault(character =>
                character.Equipment?.GetEquippedItems().Any(item => ReferenceEquals(item, equipment)) == true);
            if (owner != null) stats.Refresh(owner);
        }
    }
}
