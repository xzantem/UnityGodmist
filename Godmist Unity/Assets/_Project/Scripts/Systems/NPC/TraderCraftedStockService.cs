using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;

namespace Godmist.Systems.NPC
{
    /// <summary>
    /// Keeps a small offer of actual crafted instances alongside material stock.
    /// Instance IDs distinguish potions and bombs with the same base definition.
    /// </summary>
    public sealed class TraderCraftedStockService
    {
        private const int SlotsPerCategory = 3;
        private readonly Random random;
        private readonly AlchemyCraftingService alchemy = new();
        private readonly EquipmentGenerationService equipment;
        private readonly JewelryGenerationService jewelry;
        private readonly GalduryteCraftingService galduryte;

        public TraderCraftedStockService(Random random = null)
        {
            this.random = random ?? new Random();
            equipment = new EquipmentGenerationService(this.random);
            jewelry = new JewelryGenerationService(() => (float)this.random.NextDouble());
            galduryte = new GalduryteCraftingService(random: this.random);
        }

        public void Refresh(NpcInstance npc, int level, GameContentCatalog catalog,
            bool rotate = true)
        {
            if (npc?.ShopInventory?.Items == null || catalog == null) return;
            var kinds = GetKinds(npc);
            if (kinds.Count == 0) return;
            var trackedIds = new HashSet<string>(npc.CraftedStockInstanceIds
                ?? new List<string>(), StringComparer.Ordinal);
            var retainedIds = new HashSet<string>(StringComparer.Ordinal);
            var shop = npc.ShopInventory.Items;
            foreach (var kind in kinds)
            {
                var maximum = kind == StockKind.Galduryte && level < 11
                    ? 0 : SlotsPerCategory;
                var current = shop.Where(item => item != null
                    && trackedIds.Contains(item.InstanceId) && Classify(item) == kind)
                    .OrderBy(_ => random.Next()).ToList();
                if (kind is StockKind.Weapon or StockKind.Armor)
                {
                    // At the T1/T2 boundary, replace outdated stock rather than
                    // retaining a piece whose parts or quality are far behind.
                    var dominant = GetDominantEquipmentQuality(level);
                    foreach (var outdated in current.Where(item =>
                        (item.ItemLevel >= 11) != (level >= 11)
                        || item is EquipmentItemInstance equipmentItem
                            && ((dominant == EquipmentQuality.Poor
                                    && equipmentItem.Quality == EquipmentQuality.Excellent)
                                || (dominant == EquipmentQuality.Excellent
                                    && equipmentItem.Quality == EquipmentQuality.Poor))).ToList())
                    {
                        shop.Remove(outdated);
                        current.Remove(outdated);
                    }
                }
                var removeCount = Math.Max(0, current.Count - maximum);
                if (rotate && maximum > 0)
                    removeCount = Math.Max(removeCount, (current.Count + 1) / 2);
                foreach (var item in current.Take(removeCount))
                    shop.Remove(item);
                foreach (var item in current.Skip(removeCount))
                    retainedIds.Add(item.InstanceId);

                var remaining = current.Count - removeCount;
                var equipmentQualities = kind is StockKind.Weapon or StockKind.Armor
                    ? BuildEquipmentQualityPlan(level, current.Skip(removeCount))
                    : null;
                var generated = 0;
                // Bounded retries: invalid/missing content never hangs a refresh.
                for (var attempt = 0; remaining < maximum && attempt < maximum * 12; attempt++)
                {
                    var quality = equipmentQualities != null
                        && generated < equipmentQualities.Count
                            ? equipmentQualities[generated] : (EquipmentQuality?)null;
                    var item = Create(npc, kind, level, catalog, quality);
                    if (item == null || string.IsNullOrWhiteSpace(item.InstanceId)) continue;
                    shop.Add(item);
                    retainedIds.Add(item.InstanceId);
                    remaining++;
                    generated++;
                }
            }
            npc.CraftedStockInstanceIds = retainedIds.ToList();
        }

        private static List<StockKind> GetKinds(NpcInstance npc)
        {
            var result = new List<StockKind>();
            if (npc.Definition.GetRole<AlchemistRoleData>() != null)
            {
                result.Add(StockKind.Potion);
                result.Add(StockKind.Bomb);
            }
            if (npc.Definition.GetRole<BlacksmithRoleData>() != null)
            {
                result.Add(StockKind.Weapon);
                result.Add(StockKind.Armor);
            }
            if (npc.Definition.GetRole<EnchanterRoleData>() != null)
            {
                result.Add(StockKind.Jewelry);
                result.Add(StockKind.Galduryte);
            }
            return result;
        }

        private ItemInstance Create(NpcInstance npc, StockKind kind, int level,
            GameContentCatalog catalog, EquipmentQuality? quality) => kind switch
        {
            StockKind.Potion => CreateAlchemy(npc, ConsumableUseType.Potion),
            StockKind.Bomb => CreateAlchemy(npc, ConsumableUseType.Bomb),
            StockKind.Weapon => CreateEquipment(npc, catalog, level, ItemType.Weapon, quality),
            StockKind.Armor => CreateEquipment(npc, catalog, level, ItemType.Armor, quality),
            StockKind.Jewelry => CreateJewelry(npc, level),
            StockKind.Galduryte => CreateGalduryte(npc),
            _ => null
        };

        private ItemInstance CreateAlchemy(NpcInstance npc, ConsumableUseType useType)
        {
            var role = npc.Definition.GetRole<AlchemistRoleData>();
            var ingredients = role.IngredientOptions.Where(option => option?.Material != null
                && option.GetEffect(useType) != null
                && AlchemyRuntimeRules.GetEffectTier(option.Material) == 1)
                .OrderBy(_ => random.Next()).Take(AlchemyRuntimeRules.RequiredIngredientCount)
                .Select(option => option.Material).ToList();
            if (ingredients.Count != AlchemyRuntimeRules.RequiredIngredientCount) return null;
            var media = useType == ConsumableUseType.Bomb
                ? role.BombGunpowders : role.PotionAlcohols;
            var medium = media?.Where(option => option?.Material != null
                && AlchemyRuntimeRules.IsMediumCompatible(
                    AlchemyRuntimeRules.GetConsumableLevel(1), option.Tier))
                .OrderBy(option => option.Tier).FirstOrDefault();
            if (medium == null) return null;
            return alchemy.CreateForMerchantStock(npc, new AlchemyCraftRequest
            {
                UseType = useType,
                Ingredients = ingredients,
                Medium = medium.Material
            });
        }

        private List<EquipmentQuality> BuildEquipmentQualityPlan(int level,
            IEnumerable<ItemInstance> retained)
        {
            var withinTier = GetLevelWithinEquipmentTier(level);
            var dominant = GetDominantEquipmentQuality(level);
            var adjacent = dominant switch
            {
                EquipmentQuality.Poor => EquipmentQuality.Normal,
                EquipmentQuality.Excellent => EquipmentQuality.Normal,
                _ when withinTier < 6 => EquipmentQuality.Poor,
                _ when withinTier > 6 => EquipmentQuality.Excellent,
                _ => random.Next(2) == 0 ? EquipmentQuality.Poor : EquipmentQuality.Excellent
            };
            if (dominant == EquipmentQuality.Normal)
            {
                var retainedAdjacent = retained.OfType<EquipmentItemInstance>()
                    .Select(item => item.Quality)
                    .FirstOrDefault(quality => quality is EquipmentQuality.Poor
                        or EquipmentQuality.Excellent);
                if (retainedAdjacent != EquipmentQuality.Normal)
                    adjacent = retainedAdjacent;
            }
            var plan = new List<EquipmentQuality> { dominant, dominant, adjacent };
            foreach (var existing in retained.OfType<EquipmentItemInstance>())
            {
                if (!plan.Remove(existing.Quality)) plan.Remove(dominant);
            }
            return plan.OrderBy(_ => random.Next()).ToList();
        }

        private static int GetLevelWithinEquipmentTier(int level) =>
            (Math.Clamp(level, 1, 20) - 1) % 10 + 1;

        private static EquipmentQuality GetDominantEquipmentQuality(int level) =>
            GetLevelWithinEquipmentTier(level) switch
            {
                < 5 => EquipmentQuality.Poor,
                < 8 => EquipmentQuality.Normal,
                _ => EquipmentQuality.Excellent
            };

        private ItemInstance CreateEquipment(NpcInstance npc,
            GameContentCatalog catalog, int level, ItemType type,
            EquipmentQuality? selectedQuality)
        {
            var role = npc.Definition.GetRole<BlacksmithRoleData>();
            var bases = role.ClassOffers.Select(offer => type == ItemType.Weapon
                    ? offer?.WeaponBase : offer?.ArmorBase)
                .Where(definition => definition != null && !definition.IsStarterEquipment)
                .OrderBy(_ => random.Next()).ToList();
            var bracket = level <= 10 ? EquipmentPartLevelBracket.Level5
                : EquipmentPartLevelBracket.Level15;
            var parts = catalog.EquipmentPartDefinitions?.Where(part => part != null
                && !part.IsStarterOnly && part.LevelBracket == bracket).ToList();
            foreach (var definition in bases)
            {
                var item = equipment.Generate(definition, parts,
                    selectedQuality ?? EquipmentQuality.Normal, RarityTier.Common);
                if (item != null) return item;
            }
            return null;
        }

        private ItemInstance CreateJewelry(NpcInstance npc, int level)
        {
            var role = npc.Definition.GetRole<EnchanterRoleData>();
            var trader = npc.Definition.GetRole<TraderRoleData>();
            var allowedGems = new HashSet<ItemDefinition>((trader.LocalStock
                ?? new List<TraderStockEntry>()).Where(entry => entry?.Item != null)
                .Select(entry => entry.Item));
            foreach (var group in trader.RotatingStock ?? new List<TraderStockGroup>())
                foreach (var entry in group?.Candidates ?? new List<TraderStockEntry>())
                    if (entry?.Item != null) allowedGems.Add(entry.Item);
            foreach (var definition in role.JewelryOffers.Where(item => item != null)
                .OrderBy(_ => random.Next()))
            {
                var metals = definition.Metals.Where(metal => metal?.Material != null
                    && JewelryGenerationService.GetItemLevel(metal) ==
                        (level <= 10 ? 10 : 20)).ToList();
                var gems = definition.Gems.Where(gem => gem?.CraftingMaterial != null
                    && allowedGems.Contains(gem.CraftingMaterial)).ToList();
                if (metals.Count == 0 || gems.Count == 0) continue;
                return jewelry.GenerateSelected(definition,
                    metals[random.Next(metals.Count)], gems[random.Next(gems.Count)]);
            }
            return null;
        }

        private ItemInstance CreateGalduryte(NpcInstance npc)
        {
            var role = npc.Definition.GetRole<EnchanterRoleData>();
            var stone = role.GalduryteStones?.FirstOrDefault(option => option?.Material != null
                && option.Tier == GalduryteTier.Limestone)?.Material;
            var powders = role.GaldurytePowders?.Where(option => option?.Material != null)
                .ToList();
            if (stone == null || powders == null || powders.Count == 0) return null;
            var powder = powders[random.Next(powders.Count)].Material;
            return galduryte.CreateForMerchantStock(npc, new GalduryteCraftRequest
            {
                Stone = stone,
                Powder = powder,
                TargetType = random.Next(2) == 0
                    ? GalduryteTargetType.Weapon : GalduryteTargetType.Armor
            });
        }

        private static StockKind? Classify(ItemInstance item) => item switch
        {
            GalduryteItemInstance => StockKind.Galduryte,
            JewelryItemInstance => StockKind.Jewelry,
            ConsumableItemInstance consumable => consumable.IsBomb
                ? StockKind.Bomb : StockKind.Potion,
            EquipmentItemInstance equipment when equipment.Definition.ItemType == ItemType.Weapon
                => StockKind.Weapon,
            EquipmentItemInstance equipment when equipment.Definition.ItemType == ItemType.Armor
                => StockKind.Armor,
            _ => null
        };

        private enum StockKind { Potion, Bomb, Weapon, Armor, Jewelry, Galduryte }
    }
}
