using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Runtime.Items
{
    /// <summary>
    /// Resolves the sprite shown for an item instance. The lookup is intentionally
    /// read-only: definitions own static sprites and the library owns cached
    /// compositions, without changing gameplay instances or their save format.
    /// </summary>
    public static class ItemIconPresentation
    {
        public const string LibraryResourcePath = "Godmist/ItemIcons/ItemIconLibrary";

        private static readonly GaldurytePowderColor[] SupportedGalduryteColors =
        {
            GaldurytePowderColor.Red,
            GaldurytePowderColor.Blue,
            GaldurytePowderColor.Purple,
            GaldurytePowderColor.Lemon,
            GaldurytePowderColor.Emerald,
            GaldurytePowderColor.Black,
            GaldurytePowderColor.Pink,
            GaldurytePowderColor.White,
            GaldurytePowderColor.Gold,
            GaldurytePowderColor.Amber
        };

        public static Sprite GetIcon(ItemInstance item)
        {
            if (item == null) return null;

            var library = Resources.Load<ItemIconLibrary>(LibraryResourcePath);
            if (library != null && item is ConsumableItemInstance consumable &&
                consumable.ConsumableDefinition?.UseType is ConsumableUseType.Potion or ConsumableUseType.Bomb &&
                consumable.RuntimeEffects != null && consumable.RuntimeEffects.Count > 0 && consumable.RuntimeEffects.Count <= 3)
            {
                var colors = new Color32[consumable.RuntimeEffects.Count];
                for (var index = 0; index < colors.Length; index++) colors[index] = EffectColor(consumable.RuntimeEffects[index]);
                var mixed = library.ComposeConsumable(consumable.IsBomb, colors);
                if (mixed != null) return mixed;
            }
            var variant = library?.Find(GetVariantKey(item));
            return variant != null ? variant : item.Definition?.Icon;
        }

        // Color describes the effect, not ingredients: the saved instance contains
        // effects but does not retain its original recipe materials.
        private static Color32 EffectColor(ConsumableEffectRuntime effect)
        {
            var type = effect?.EffectType;
            return type switch
            {
                ConsumableRuntimeEffectType.Heal or ConsumableRuntimeEffectType.RestoreHealth or ConsumableRuntimeEffectType.RegenHealth or ConsumableRuntimeEffectType.ApplyRegeneration => new Color32(190, 42, 52, 255),
                ConsumableRuntimeEffectType.RestoreResource or ConsumableRuntimeEffectType.RegenResource or ConsumableRuntimeEffectType.ModifyMaxResource or ConsumableRuntimeEffectType.ApplyFreeze or ConsumableRuntimeEffectType.ApplyChill => new Color32(44, 101, 204, 255),
                ConsumableRuntimeEffectType.ApplyPoison or ConsumableRuntimeEffectType.ModifyDodge or ConsumableRuntimeEffectType.ModifySpeed or ConsumableRuntimeEffectType.ModifyAccuracy => new Color32(44, 164, 88, 255),
                ConsumableRuntimeEffectType.ApplyBurn or ConsumableRuntimeEffectType.DealPhysicalDamage or ConsumableRuntimeEffectType.DealDamage => new Color32(184, 126, 48, 255),
                ConsumableRuntimeEffectType.ApplyBleed or ConsumableRuntimeEffectType.ModifyCritChance => new Color32(190, 42, 52, 255),
                ConsumableRuntimeEffectType.ModifyDefense or ConsumableRuntimeEffectType.ModifyPhysicalDefense or ConsumableRuntimeEffectType.ModifyMagicalDefense or ConsumableRuntimeEffectType.ModifyResistance or ConsumableRuntimeEffectType.ModifyAllResistances => new Color32(211, 82, 146, 255),
                _ => new Color32(132, 62, 184, 255)
            };
        }

        public static string GetVariantKey(ItemInstance item)
        {
            if (item is JewelryItemInstance jewelry)
                return BuildJewelryKey(jewelry);

            if (item is GalduryteItemInstance galduryte)
                return BuildGalduryteKey(galduryte);

            return string.Empty;
        }

        public static string BuildJewelryKey(JewelryItemInstance jewelry)
        {
            if (jewelry?.EquipmentDefinition is not Data.Items.JewelryItemDefinition definition)
                return string.Empty;

            return BuildJewelryKey(definition.JewelrySlot, jewelry.MetalId, jewelry.GemId);
        }

        public static string BuildJewelryKey(
            EquipmentSlot slot,
            string metalId,
            string gemId)
        {
            if (string.IsNullOrWhiteSpace(metalId) || string.IsNullOrWhiteSpace(gemId))
                return string.Empty;

            var canonicalMetalId = metalId.Split(':')[0].Trim();
            return $"jewelry.{slot.ToString().ToLowerInvariant()}.{canonicalMetalId}.{gemId.Trim()}";
        }

        public static string BuildGalduryteKey(GalduryteItemInstance galduryte)
        {
            if (galduryte == null || !galduryte.IsIdentified || galduryte.IsDestroyed || galduryte.Effects == null)
                return string.Empty;

            var effects = new List<string>();
            for (var index = 0; index < galduryte.Effects.Count; index++)
            {
                var effect = galduryte.Effects[index];
                if (effect == null) continue;

                var color = (int)effect.PowderColor;
                if (color < 0 || color >= SupportedGalduryteColors.Length)
                    return string.Empty;

                if (!Enum.IsDefined(typeof(GalduryteEffectType),effect.EffectType) || effects.Count == 6)
                    return string.Empty;
                effects.Add($"{color}:{(int)effect.EffectType}");
            }

            return effects.Count == 0
                ? string.Empty
                : $"galduryte.{string.Join("-", effects)}";
        }

        public static bool IsSupportedGalduryteColor(GaldurytePowderColor color) =>
            (int)color >= 0 && (int)color < SupportedGalduryteColors.Length;
    }
}
