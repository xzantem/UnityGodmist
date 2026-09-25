using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.Configuration;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.NPC;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public sealed class EquipmentUpgradePreview
    {
        public bool CanExecute;
        public string FailureReason = string.Empty;
        public int GoldCost;
        public float TargetProgress;
        public float SuccessChance;
        public int SocketCapacity;
        public float CurrentValue;
        public RarityTier TargetRarity;
        public float IncreaseChance;
        public float UnchangedChance;
        public float DecreaseChance;
    }

    /// <summary>Owns mutable upgrade/reforge state of an existing equipment instance.</summary>
    public sealed class EquipmentUpgradeService
    {
        private readonly System.Random random;
        private readonly CharacterStatPipelineSystem statPipeline = new();
        private readonly NpcLoyaltyService npcLoyaltyService = new();

        public EquipmentUpgradeService(System.Random random = null) => this.random = random ?? new System.Random();

        public EquipmentUpgradePreview CreateUpgradePreview(GameSession session, NpcInstance blacksmith,
            EquipmentItemInstance item, float targetProgress, float successChance, GameContentCatalog catalog)
        {
            var preview = new EquipmentUpgradePreview
            {
                TargetProgress = targetProgress,
                SuccessChance = successChance,
                SocketCapacity = EquipmentItemInstance.GetSocketCapacity(targetProgress)
            };
            if (!TryValidate(session, blacksmith, item, catalog, out var baseValue, out var error))
            {
                preview.FailureReason = error;
                return preview;
            }
            if (targetProgress <= item.UpgradeProgress || targetProgress > 1f || targetProgress < 0f || float.IsNaN(targetProgress)
                || targetProgress - item.UpgradeProgress > .20001f)
            {
                preview.FailureReason = "Wybierz przyrost od ponad 0 do 20 punktów procentowych, maksymalnie do 100%.";
                return preview;
            }
            var maximumSuccessChance = GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeSuccessChanceMax, .99f);
            if (float.IsNaN(successChance) || successChance < 0f || successChance > maximumSuccessChance)
            {
                preview.FailureReason = "Szansa powodzenia musi mieścić się między 0% a 99%.";
                return preview;
            }
            preview.CurrentValue = item.CurrentEquipmentValue(baseValue);
            var baselineValue = baseValue * EquipmentItemInstance.QualityValueMultiplier(item.Quality)
                * EquipmentItemInstance.RarityMultiplier(item.CurrentRarity);
            // Round endpoints before subtraction: successful partitions telescope exactly.
            var baseGoldCost = Math.Max(1, (int)Math.Min(int.MaxValue,
                Math.Ceiling(UpgradeCost(baselineValue, targetProgress, successChance))
                - Math.Ceiling(UpgradeCost(baselineValue, item.UpgradeProgress, successChance))));
            preview.GoldCost = npcLoyaltyService.GetDiscountedGoldCost(
                blacksmith,
                baseGoldCost);
            preview.CanExecute = session.Gold >= preview.GoldCost;
            preview.FailureReason = preview.CanExecute ? string.Empty : "Brakuje złota.";
            return preview;
        }

        public bool TryUpgrade(GameSession session, NpcInstance blacksmith, CharacterInstance owner,
            EquipmentItemInstance item, float targetProgress, float successChance, GameContentCatalog catalog,
            out bool succeeded, out int paidGold, out string failureReason)
        {
            succeeded = false; paidGold = 0;
            var preview = CreateUpgradePreview(session, blacksmith, item, targetProgress, successChance, catalog);
            if (!preview.CanExecute) { failureReason = preview.FailureReason; return false; }
            if (!session.TrySpendGold(preview.GoldCost)) { failureReason = "Nie udało się pobrać złota."; return false; }
            item.RecalculateStatModifiers(); // Capture legacy baseline before changing progress or rarity.
            paidGold = preview.GoldCost;
            npcLoyaltyService.TryRecordGoldSpent(blacksmith, paidGold);
            new NpcEconomyService().TryReceiveGold(blacksmith, paidGold);
            if (random.NextDouble() < successChance)
            {
                item.UpgradeProgress = targetProgress;
                succeeded = true;
                failureReason = string.Empty;
            }
            else
            {
                var loss = (10 + Math.Min(10, (int)(random.NextDouble() * 11))) / 100f;
                item.UpgradeProgress = Mathf.Max(0f, item.UpgradeProgress - loss);
                failureReason = $"Ulepszenie nie powiodło się; utrata {loss:P0} postępu. Nadmiarowe gniazda są nieaktywne.";
            }
            ApplyUpgradeStats(item);
            if (owner != null && owner.Equipment.GetEquippedItems().Any(equipped => ReferenceEquals(equipped, item))) statPipeline.Refresh(owner);
            return true;
        }

        public EquipmentUpgradePreview CreateReforgePreview(GameSession session, NpcInstance blacksmith,
            EquipmentItemInstance item, GameContentCatalog catalog)
        {
            var preview = new EquipmentUpgradePreview();
            if (!TryValidate(session, blacksmith, item, catalog, out var baseValue, out var error)) { preview.FailureReason = error; return preview; }
            if (item.CurrentRarity == RarityTier.Godly) { preview.FailureReason = "Boskie wyposażenie nie może zostać reforgowane."; return preview; }
            var (up, unchanged) = GetReforgeChances(item.CurrentRarity);
            preview.TargetRarity = (RarityTier)((int)item.CurrentRarity + 1);
            preview.IncreaseChance = (float)up;
            preview.UnchangedChance = (float)unchanged;
            preview.DecreaseChance = 1f - preview.IncreaseChance - preview.UnchangedChance;
            preview.CurrentValue = item.CurrentEquipmentValue(baseValue);
            var baseGoldCost = Mathf.Max(1, Mathf.CeilToInt(preview.CurrentValue * (.25f + .15f * Mathf.Max(0, (int)item.CurrentRarity - (int)RarityTier.Common))));
            preview.GoldCost = npcLoyaltyService.GetDiscountedGoldCost(
                blacksmith,
                baseGoldCost);
            preview.CanExecute = session.Gold >= preview.GoldCost;
            preview.FailureReason = preview.CanExecute ? string.Empty : "Brakuje złota.";
            return preview;
        }

        public bool TryReforge(GameSession session, NpcInstance blacksmith, CharacterInstance owner,
            EquipmentItemInstance item, GameContentCatalog catalog, out RarityTier result, out int paidGold, out string failureReason)
        {
            result = item?.CurrentRarity ?? RarityTier.Trash; paidGold = 0;
            var preview = CreateReforgePreview(session, blacksmith, item, catalog);
            if (!preview.CanExecute) { failureReason = preview.FailureReason; return false; }
            if (!session.TrySpendGold(preview.GoldCost)) { failureReason = "Nie udało się pobrać złota."; return false; }
            item.RecalculateStatModifiers(); // Capture legacy baseline before changing progress or rarity.
            paidGold = preview.GoldCost;
            npcLoyaltyService.TryRecordGoldSpent(blacksmith, paidGold);
            new NpcEconomyService().TryReceiveGold(blacksmith, paidGold);
            var (up, unchanged) = GetReforgeChances(item.CurrentRarity);
            var roll = random.NextDouble();
            result = roll < up ? (RarityTier)((int)item.CurrentRarity + 1)
                : roll < up + unchanged ? item.CurrentRarity : (RarityTier)((int)item.CurrentRarity - 1);
            item.CurrentRarity = result;
            ApplyUpgradeStats(item);
            if (owner != null && owner.Equipment.GetEquippedItems().Any(equipped => ReferenceEquals(equipped, item)))
                statPipeline.Refresh(owner);
            failureReason = string.Empty;
            return true;
        }

        public static float GetCurrentValue(float baseValue, RarityTier rarity, float progress) =>
            baseValue * EquipmentItemInstance.RarityMultiplier(rarity)
            * (1f + 4f * Mathf.Clamp01(progress));

        // baseValue is the quality/rarity-adjusted value at zero upgrade progress.
        public static double UpgradeCost(float baseValue, float progress, float chance)
        {
            var p = Math.Round(Mathf.Clamp01(progress), 6);
            var c = Math.Round(Mathf.Clamp(chance, 0f, .99f), 6);
            var chanceFactor = (57d - 37d * c) / (76d - 75d * c);
            var referenceFactor = (57d - 37d * .6d) / (76d - 75d * .6d);
            return baseValue * (.6d * p + 6.4d * p * p * p) * chanceFactor / referenceFactor;
        }

        public static int GetRarityValue(RarityTier rarity) => (int)rarity - 2;

        private static (double Up, double Unchanged) GetReforgeChances(RarityTier rarity) => rarity switch
        {
            RarityTier.Trash => Chances(GameDefineIds.ReforgeTrashIncreaseChance, .90f,
                GameDefineIds.ReforgeTrashUnchangedChance, .10f),
            RarityTier.Broken => Chances(GameDefineIds.ReforgeBrokenIncreaseChance, .80f,
                GameDefineIds.ReforgeBrokenUnchangedChance, .15f),
            RarityTier.Damaged => Chances(GameDefineIds.ReforgeDamagedIncreaseChance, .80f,
                GameDefineIds.ReforgeDamagedUnchangedChance, .15f),
            RarityTier.Common => Chances(GameDefineIds.ReforgeCommonIncreaseChance, .70f,
                GameDefineIds.ReforgeCommonUnchangedChance, .20f),
            RarityTier.Uncommon => Chances(GameDefineIds.ReforgeUncommonIncreaseChance, .65f,
                GameDefineIds.ReforgeUncommonUnchangedChance, .20f),
            RarityTier.Rare => Chances(GameDefineIds.ReforgeRareIncreaseChance, .60f,
                GameDefineIds.ReforgeRareUnchangedChance, .25f),
            RarityTier.Ancient => Chances(GameDefineIds.ReforgeAncientIncreaseChance, .55f,
                GameDefineIds.ReforgeAncientUnchangedChance, .25f),
            RarityTier.Legendary => Chances(GameDefineIds.ReforgeLegendaryIncreaseChance, .45f,
                GameDefineIds.ReforgeLegendaryUnchangedChance, .30f),
            RarityTier.Mythic => Chances(GameDefineIds.ReforgeMythicIncreaseChance, .35f,
                GameDefineIds.ReforgeMythicUnchangedChance, .40f),
            _ => (0, 0)
        };

        private static (double Up, double Unchanged) Chances(
            string increaseId, float increaseFallback,
            string unchangedId, float unchangedFallback)
        {
            return (
                GameDefinesRuntime.GetFloat(increaseId, increaseFallback),
                GameDefinesRuntime.GetFloat(unchangedId, unchangedFallback));
        }

        private static bool TryValidate(GameSession session, NpcInstance blacksmith, EquipmentItemInstance item,
            GameContentCatalog catalog, out float baseValue, out string error)
        {
            baseValue = 0; error = string.Empty;
            if (session == null || blacksmith?.Definition?.GetRole<BlacksmithRoleData>() == null || item?.EquipmentDefinition == null || catalog == null)
            { error = "Nieprawidłowa usługa kowala lub wyposażenie."; return false; }
            if (item.Definition.ItemType is not (ItemType.Weapon or ItemType.Armor)) { error = "Ulepszać i reforgować można tylko broń oraz pancerz."; return false; }
            if (item.EquipmentDefinition.IsStarterEquipment)
            { error = "Wyposażenia startowego nie można ulepszać ani reforgować."; return false; }
            var parts = (item.PartIds ?? new()).Select(id => catalog.EquipmentPartDefinitions.FirstOrDefault(part => part != null && part.Id == id)).ToList();
            if (parts.Count == 0 || parts.Any(part => part == null)) { error = "Wyposażenie ma brakujące dane części."; return false; }
            baseValue = parts.Sum(part => (part.CraftingMaterials ?? new()).Where(requirement => requirement?.Material != null).Sum(requirement => requirement.Amount * GetDefinitionValue(requirement.Material)));
            if (item.CraftedBaseValue > 0) baseValue = item.CraftedBaseValue;
            if (baseValue <= 0) { error = "Części wyposażenia muszą mieć dodatnią wartość materiałów."; return false; }
            return true;
        }

        private static float GetDefinitionValue(ItemDefinition definition) => definition is CraftableItemDefinition craftable ? craftable.CalculatedBaseValue : Mathf.Max(0, definition.BaseValue);

        private static void ApplyUpgradeStats(EquipmentItemInstance item) => item.RecalculateStatModifiers();
    }
}
