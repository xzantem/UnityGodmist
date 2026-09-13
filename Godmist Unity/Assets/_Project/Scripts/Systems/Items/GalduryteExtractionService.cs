using System;
using System.Linq;
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
    public sealed class GalduryteExtractionPreview
    {
        public int GoldCost;
        public float SuccessChance;
        public bool CanExtract;
        public string FailureReason = string.Empty;
    }

    public sealed class GalduryteExtractionResult
    {
        public bool Succeeded;
        public bool RemovedDestroyedGalduryte;
        public int PaidGold;
    }

    public sealed class GalduryteExtractionService
    {
        private readonly System.Random random;
        private readonly InventoryService inventory = new();

        public GalduryteExtractionService(System.Random random = null) => this.random = random ?? new System.Random();

        public int GetGoldCost(GalduryteItemInstance galduryte)
        {
            if (galduryte == null) return 0;
            var multiplier = Mathf.Max(0, GameDefinesRuntime.GetFloat(GameDefineIds.GalduryteExtractionCostPercent, 1.2f));
            if (galduryte.IsDestroyed)
                multiplier *= Mathf.Max(1, GameDefinesRuntime.GetFloat(GameDefineIds.GalduryteDestroyedExtractionMultiplier, 1.5f));
            // Decimal arithmetic avoids charging an extra gold due to float representation of 1.2.
            return (int)Math.Min(int.MaxValue, Math.Max(1m, Math.Ceiling(Math.Max(0, galduryte.BaseValue) * (decimal)multiplier)));
        }

        public float GetSuccessChance(NpcInstance npc)
        {
            var chance = GameDefinesRuntime.GetFloat(GameDefineIds.GalduryteExtractionSuccessChance, .7f);
            if (npc != null && npc.LoyaltyLevel >= GameDefinesRuntime.GetInt(GameDefineIds.GalduryteExtractionLoyaltyThreshold, 6))
                chance += GameDefinesRuntime.GetFloat(GameDefineIds.GalduryteExtractionLoyaltyBonus, .1f);
            return Mathf.Clamp01(chance);
        }

        public GalduryteExtractionPreview CreatePreview(GameSession session, NpcInstance npc,
            EquipmentItemInstance equipment, GalduryteItemInstance galduryte)
        {
            var preview = new GalduryteExtractionPreview { GoldCost = GetGoldCost(galduryte), SuccessChance = GetSuccessChance(npc) };
            if (session == null || npc?.Definition?.GetRole<EnchanterRoleData>() == null)
                preview.FailureReason = "Brak aktywnej usługi Zaklinacza.";
            else if (!TryFindOwner(session, equipment, out var destination, out _))
                preview.FailureReason = "Wybierz własną broń albo pancerz.";
            else if (galduryte == null || equipment.SocketedGaldurytes?.Contains(galduryte) != true)
                preview.FailureReason = "Wybierz osadzony galduryt.";
            else if (!galduryte.IsDestroyed && !CanReturn(destination, galduryte))
                preview.FailureReason = "Brak miejsca na wyciągnięty galduryt.";
            else if (session.Gold < preview.GoldCost)
                preview.FailureReason = "Brakuje złota na wyciągnięcie galdurytu.";
            else preview.CanExtract = true;
            return preview;
        }

        /// <summary>True means a paid attempt occurred; Succeeded distinguishes its outcome.</summary>
        public bool TryExtract(GameSession session, NpcInstance npc, EquipmentItemInstance equipment,
            GalduryteItemInstance galduryte, out GalduryteExtractionResult result, out string failureReason)
        {
            result = null;
            var preview = CreatePreview(session, npc, equipment, galduryte);
            failureReason = preview.FailureReason;
            if (!preview.CanExtract) return false;
            TryFindOwner(session, equipment, out var destination, out var owner);
            if (!session.TrySpendGold(preview.GoldCost))
            {
                failureReason = "Brakuje złota na wyciągnięcie galdurytu.";
                return false;
            }
            var succeeded = random.NextDouble() < preview.SuccessChance;
            var wasDestroyed = galduryte.IsDestroyed;
            if (succeeded)
            {
                // Preflight uses exactly the same capacity rules; retain the socket if return fails.
                if (!wasDestroyed && !inventory.TryAddItem(destination, galduryte))
                {
                    session.TryAddGold(preview.GoldCost);
                    failureReason = "Nie udało się zwrócić galdurytu do ekwipunku.";
                    return false;
                }
                equipment.SocketedGaldurytes.Remove(galduryte);
            }
            else galduryte.IsDestroyed = true;

            new NpcLoyaltyService().TryRecordGoldSpent(npc, preview.GoldCost);
            if (owner != null) new CharacterStatPipelineSystem().Refresh(owner);
            result = new GalduryteExtractionResult
            {
                Succeeded = succeeded,
                RemovedDestroyedGalduryte = succeeded && wasDestroyed,
                PaidGold = preview.GoldCost
            };
            failureReason = string.Empty;
            return true;
        }

        private bool CanReturn(Inventory destination, GalduryteItemInstance item) =>
            destination?.Items != null && item.Definition != null && !string.IsNullOrWhiteSpace(item.InstanceId)
            && !destination.Items.Any(candidate => candidate?.InstanceId == item.InstanceId)
            && (!destination.MaxWeight.HasValue || inventory.GetCurrentWeight(destination)
                + Mathf.Max(0, item.Definition.Weight) <= destination.MaxWeight.Value);

        private static bool TryFindOwner(GameSession session, EquipmentItemInstance equipment,
            out Inventory destination, out CharacterInstance equippedOwner)
        {
            destination = null;
            equippedOwner = null;
            if (equipment?.Definition == null || session == null
                || (equipment.Definition.ItemType != Godmist.Core.Enums.ItemType.Weapon
                    && equipment.Definition.ItemType != Godmist.Core.Enums.ItemType.Armor)) return false;
            foreach (var id in session.PlayerParty?.ActiveMemberIds ?? Enumerable.Empty<string>())
            {
                if (!session.CharactersById.TryGetValue(id, out var character)) continue;
                var equipped = character.Equipment?.GetEquippedItems().Any(item => ReferenceEquals(item, equipment)) == true;
                if (!equipped && character.Inventory?.Items.Contains(equipment) != true) continue;
                destination = character.Inventory;
                equippedOwner = equipped ? character : null;
                return true;
            }
            if (session.Stash?.Items.Contains(equipment) != true) return false;
            destination = session.Stash;
            return true;
        }
    }
}
