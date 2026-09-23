using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public sealed class AlchemyIngredientData
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField] public ConsumableEffectDefinition PotionEffect { get; private set; }
        [field: SerializeField] public ConsumableEffectDefinition BombEffect { get; private set; }
        [field: SerializeField] public string EffectAdjective { get; private set; } = string.Empty;

        public ConsumableEffectDefinition GetEffect(ConsumableUseType useType) =>
            useType == ConsumableUseType.Bomb ? BombEffect : PotionEffect;
    }

    [Serializable]
    public sealed class AlchemyMediumData
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField, Range(1, 5)] public int Tier { get; private set; } = 1;
    }

    [Serializable]
    public sealed class AlchemyCatalystData
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField] public ConsumableCatalystType CatalystType { get; private set; }
        [field: SerializeField] public ConsumableCatalystType BombCatalystType { get; private set; }
        [field: SerializeField] public bool HasBombCatalystOverride { get; private set; }
        [field: SerializeField, Range(1, 5)] public int Tier { get; private set; } = 1;

        public ConsumableCatalystType GetTypeFor(ConsumableUseType useType) =>
            useType == ConsumableUseType.Bomb && HasBombCatalystOverride
                ? BombCatalystType : CatalystType;
    }

    [Serializable]
    public sealed class AlchemyTierNameData
    {
        [field: SerializeField, Range(1, 3)] public int EffectTier { get; private set; } = 1;
        [field: SerializeField] public string Adjective { get; private set; } = string.Empty;
    }
}
