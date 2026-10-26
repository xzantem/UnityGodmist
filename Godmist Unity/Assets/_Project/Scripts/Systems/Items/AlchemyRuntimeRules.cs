using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;

namespace Godmist.Systems.Items
{
    public static class AlchemyRuntimeRules
    {
        public static int RequiredIngredientCount => GameDefinesRuntime.GetInt(
            GameDefineIds.AlchemyIngredientCount, 3);

        public static int GetConsumableLevel(int effectTier)
        {
            return effectTier switch
            {
                1 => GameDefinesRuntime.GetInt(GameDefineIds.AlchemyLevelTier1, 11),
                2 => GameDefinesRuntime.GetInt(GameDefineIds.AlchemyLevelTier2, 21),
                _ => GameDefinesRuntime.GetInt(GameDefineIds.AlchemyLevelTier3, 41)
            };
        }

        public static int GetEffectTier(ItemDefinition ingredient)
        {
            return ingredient?.BaseRarity switch
            {
                RarityTier.Uncommon or RarityTier.Rare => 2,
                RarityTier.Ancient or RarityTier.Legendary => 3,
                _ => 1
            };
        }

        public static int GetRequiredMediumTier(int consumableLevel)
        {
            if (consumableLevel <= GetConsumableLevel(1))
            {
                return GameDefinesRuntime.GetInt(GameDefineIds.AlchemyMediumTierForLevel1, 1);
            }

            if (consumableLevel <= GetConsumableLevel(2))
            {
                return GameDefinesRuntime.GetInt(GameDefineIds.AlchemyMediumTierForLevel2, 3);
            }

            return GameDefinesRuntime.GetInt(GameDefineIds.AlchemyMediumTierForLevel3, 5);
        }

        public static bool IsMediumCompatible(int consumableLevel, int mediumTier) =>
            mediumTier >= GetRequiredMediumTier(consumableLevel);

        public static bool IsValidConsumableType(ConsumableUseType useType) =>
            useType is ConsumableUseType.Potion or ConsumableUseType.Bomb;

        public static string GetConsumableLabel(ConsumableUseType useType) =>
            useType == ConsumableUseType.Bomb ? "bombę" : "potion";

        public static string GetConsumableNoun(ConsumableUseType useType) =>
            useType == ConsumableUseType.Bomb ? "bomba" : "mikstura";

        public static string GetFallbackEffectAdjective(
            ConsumableRuntimeEffectType effectType,
            ConsumableUseType useType = ConsumableUseType.Potion)
        {
            return effectType switch
            {
                ConsumableRuntimeEffectType.Heal or ConsumableRuntimeEffectType.RestoreHealth
                    or ConsumableRuntimeEffectType.RegenHealth => "lecznicza",
                ConsumableRuntimeEffectType.RestoreResource or ConsumableRuntimeEffectType.RegenResource
                    => "krzepiąca",
                ConsumableRuntimeEffectType.ModifySpeed => "zwinna",
                ConsumableRuntimeEffectType.ModifyDamage => "niszczycielska",
                ConsumableRuntimeEffectType.ModifyDefense => "ochronna",
                ConsumableRuntimeEffectType.ModifyPhysicalDefense
                    or ConsumableRuntimeEffectType.ModifyMagicalDefense =>
                    useType == ConsumableUseType.Bomb ? "przełamująca" : "ochronna",
                ConsumableRuntimeEffectType.ModifyAllResistances
                    or ConsumableRuntimeEffectType.ModifyResistance => "odporna",
                ConsumableRuntimeEffectType.ModifyDodge =>
                    useType == ConsumableUseType.Bomb ? "dezorientująca" : "zwinna",
                ConsumableRuntimeEffectType.ModifyAccuracy => "oślepiająca",
                ConsumableRuntimeEffectType.ModifyActionPoints => "żwawa",
                ConsumableRuntimeEffectType.ModifyMaxResource => "zasobna",
                ConsumableRuntimeEffectType.Slow => "spowalniająca",
                ConsumableRuntimeEffectType.ModifyCritChance => "precyzyjna",
                ConsumableRuntimeEffectType.ApplyPoison => "zatruta",
                ConsumableRuntimeEffectType.ApplyBurn => "płonąca",
                ConsumableRuntimeEffectType.ApplyBleed => "krwawa",
                ConsumableRuntimeEffectType.ApplyStun => "ogłuszająca",
                ConsumableRuntimeEffectType.ApplyFreeze => "mrożąca",
                _ => "alchemiczna"
            };
        }

        public static string GetFallbackTierAdjective(int effectTier)
        {
            return effectTier switch
            {
                1 => "zwyczajna",
                2 => "wyrafinowana",
                _ => "doskonała"
            };
        }
    }

    internal static class ItemDefinitionValueUtility
    {
        public static int GetValue(ItemDefinition definition)
        {
            return definition is CraftableItemDefinition craftable
                ? craftable.CalculatedBaseValue
                : definition?.BaseValue > 0
                    ? definition.BaseValue
                    : 0;
        }
    }
}
