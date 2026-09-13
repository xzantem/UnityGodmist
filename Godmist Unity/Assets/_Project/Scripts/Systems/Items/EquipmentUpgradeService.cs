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
            var preview = new EquipmentUpgradePreview { TargetProgress = targetProgress, SuccessChance = successChance };
            if (!TryValidate(session, blacksmith, item, catalog, out var baseValue, out var error))
            {
                preview.FailureReason = error;
                return preview;
            }
            if (targetProgress <= item.UpgradeProgress || targetProgress > 1f || targetProgress < 0f)
            {
                preview.FailureReason = "Wybierz docelowy postęp większy od obecnego i nie większy niż 100%.";
                return preview;
            }
            var maximumSuccessChance = GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeSuccessChanceMax, .99f);
            if (successChance < 0f || successChance > maximumSuccessChance)
            {
                preview.FailureReason = "Szansa powodzenia musi mieścić się między 0% a 99%.";
                return preview;
            }
            var baseGoldCost = Mathf.Max(1, Mathf.CeilToInt((float)(UpgradeCost(
                baseValue,
                targetProgress,
                successChance)
                - UpgradeCost(baseValue, item.UpgradeProgress, successChance))));
            preview.GoldCost = npcLoyaltyService.GetDiscountedGoldCost(
                blacksmith,
                baseGoldCost);
            var maximumSocketCapacity = GameDefinesRuntime.GetInt(
                GameDefineIds.SocketCapacityAtFullUpgrade, 5);
            preview.SocketCapacity = Mathf.Clamp(
                Mathf.FloorToInt(targetProgress * maximumSocketCapacity + .0001f),
                0,
                maximumSocketCapacity);
            preview.CurrentValue = GetCurrentValue(baseValue, item.CurrentRarity, item.UpgradeProgress);
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
                failureReason = "Ulepszenie nie powiodło się; postęp pozostał bez zmian.";
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
            preview.CurrentValue = GetCurrentValue(baseValue, item.CurrentRarity, item.UpgradeProgress);
            var baseGoldCost = Mathf.Max(1, Mathf.CeilToInt(preview.CurrentValue * GameDefinesRuntime.GetFloat(
                GameDefineIds.ReforgeCostMultiplier, .25f)));
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
            paidGold = preview.GoldCost;
            npcLoyaltyService.TryRecordGoldSpent(blacksmith, paidGold);
            new NpcEconomyService().TryReceiveGold(blacksmith, paidGold);
            var (up, unchanged) = GetReforgeChances(item.CurrentRarity);
            var roll = random.NextDouble();
            result = roll < up ? (RarityTier)((int)item.CurrentRarity + 1)
                : roll < up + unchanged ? item.CurrentRarity : (RarityTier)((int)item.CurrentRarity - 1);
            item.CurrentRarity = result;
            failureReason = string.Empty;
            return true;
        }

        public static float GetCurrentValue(float baseValue, RarityTier rarity, float progress) =>
            GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeValueBaseMultiplier, .25f)
            * (GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeValueRarityBase, 3f)
                + GetRarityValue(rarity))
            * (1f + GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeValueProgressMultiplier, 4f)
                * Mathf.Clamp01(progress)) * baseValue;

        public static double UpgradeCost(float baseValue, float progress, float chance)
        {
            var m = Mathf.Clamp01(progress);
            var c = Mathf.Clamp(chance, 0f, GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeSuccessChanceMax, .99f));
            var progressBase = GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeCostProgressBase, 3f);
            var progressSlope = GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeCostProgressSlope, 7f);
            var progressDenominatorBase = GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeCostProgressDenominatorBase, 12f);
            var progressDenominatorSlope = GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeCostProgressDenominatorSlope, 11f);
            var chanceBase = GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeCostChanceBase, 57f);
            var chanceSlope = GameDefinesRuntime.GetFloat(GameDefineIds.UpgradeCostChanceSlope, 37f);
            var chanceDenominatorBase = GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeCostChanceDenominatorBase, 76f);
            var chanceDenominatorSlope = GameDefinesRuntime.GetFloat(
                GameDefineIds.UpgradeCostChanceDenominatorSlope, 75f);
            return ((1d + baseValue) / 2d)
                * ((progressSlope * m + progressBase)
                    / (progressDenominatorBase - progressDenominatorSlope * m))
                * ((chanceBase - chanceSlope * c)
                    / (chanceDenominatorBase - chanceDenominatorSlope * c));
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
            var parts = (item.PartIds ?? new()).Select(id => catalog.EquipmentPartDefinitions.FirstOrDefault(part => part != null && part.Id == id)).ToList();
            if (parts.Count == 0 || parts.Any(part => part == null)) { error = "Wyposażenie ma brakujące dane części."; return false; }
            baseValue = parts.Sum(part => (part.CraftingMaterials ?? new()).Where(requirement => requirement?.Material != null).Sum(requirement => requirement.Amount * GetDefinitionValue(requirement.Material)));
            if (baseValue <= 0) { error = "Części wyposażenia muszą mieć dodatnią wartość materiałów."; return false; }
            return true;
        }

        private static float GetDefinitionValue(ItemDefinition definition) => definition is CraftableItemDefinition craftable ? craftable.CalculatedBaseValue : Mathf.Max(0, definition.BaseValue);

        private static void ApplyUpgradeStats(EquipmentItemInstance item)
        {
            item.BaseStatModifiers ??= new();
            if (item.BaseStatModifiers.Count == 0 && item.StatModifiers != null) item.BaseStatModifiers = item.StatModifiers.Select(Copy).ToList();
            var multiplier = 1f + item.UpgradeProgress;
            item.StatModifiers = item.BaseStatModifiers.Select(modifier =>
            {
                var result = Copy(modifier);
                var applies = item.Definition.ItemType == ItemType.Weapon
                    ? result.StatType is CharacterStatType.MinAttack or CharacterStatType.MaxAttack
                    : result.StatType is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense;
                if (applies) result.Value *= multiplier;
                return result;
            }).ToList();
        }

        private static StatModifier Copy(StatModifier modifier) => new() { StatType = modifier.StatType, Operation = modifier.Operation, Stacking = modifier.Stacking, Value = modifier.Value };
    }
}
