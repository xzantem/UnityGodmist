using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    /// <summary>
    /// Single source of truth for runtime consumable semantics. Consumable
    /// payloads are assembled at runtime, so these rules deliberately live
    /// outside ScriptableObject definitions.
    /// </summary>
    public static class ConsumableRuntimeRules
    {
        public static bool TryGetImpliedUtilityEffect(
            ConsumableItemDefinition definition,
            out ConsumableEffectRuntime runtimeEffect)
        {
            runtimeEffect = null;
            if (definition == null || definition.UseType != ConsumableUseType.Utility)
            {
                return false;
            }

            var identity = $"{definition.Id} {definition.DisplayName}";
            if (identity.Contains("return", StringComparison.OrdinalIgnoreCase))
            {
                runtimeEffect = new ConsumableEffectRuntime
                {
                    EffectType = ConsumableRuntimeEffectType.ReturnToArungard,
                    TargetType = TargetType.Caster,
                    TargetingType = TargetingType.Allies
                };
                return true;
            }

            var statusId = identity.Contains("bandage", StringComparison.OrdinalIgnoreCase)
                ? StatusEffectIds.Bleed
                : identity.Contains("antidote", StringComparison.OrdinalIgnoreCase)
                    ? StatusEffectIds.Poison
                    : identity.Contains("damp", StringComparison.OrdinalIgnoreCase)
                        || identity.Contains("dry", StringComparison.OrdinalIgnoreCase)
                        || identity.Contains("towel", StringComparison.OrdinalIgnoreCase)
                        || identity.Contains("cloth", StringComparison.OrdinalIgnoreCase)
                            ? StatusEffectIds.Burn
                            : string.Empty;
            if (string.IsNullOrEmpty(statusId)) return false;

            runtimeEffect = new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.RemoveStatus,
                StatusId = statusId,
                TargetType = TargetType.Caster,
                TargetingType = TargetingType.Allies
            };
            return true;
        }

        public static bool CanCombineEffects(
            ConsumableEffectRuntime source,
            ConsumableEffectRuntime candidate)
        {
            if (source == null || candidate == null
                || source.EffectType != candidate.EffectType
                || source.TargetModeIndex != candidate.TargetModeIndex
                || source.TargetType != candidate.TargetType
                || source.TargetingType != candidate.TargetingType
                || source.ResistanceType != candidate.ResistanceType)
            {
                return false;
            }

            var sourceStatusId = string.IsNullOrWhiteSpace(source.StatusId)
                ? GetImpliedStatusId(source.EffectType)
                : source.StatusId;
            var candidateStatusId = string.IsNullOrWhiteSpace(candidate.StatusId)
                ? GetImpliedStatusId(candidate.EffectType)
                : candidate.StatusId;
            return string.Equals(
                sourceStatusId,
                candidateStatusId,
                StringComparison.OrdinalIgnoreCase);
        }

        public static float GetDuplicateMultiplier(int occurrenceCount)
        {
            return occurrenceCount <= 1
                ? 1f
                : occurrenceCount == 2
                    ? GameDefinesRuntime.GetFloat(
                        GameDefineIds.ConsumableDuplicateSecondMultiplier, .75f)
                    : GameDefinesRuntime.GetFloat(
                        GameDefineIds.ConsumableDuplicateThirdPlusMultiplier, .60f);
        }

        public static string GetImpliedStatusId(ConsumableRuntimeEffectType effectType)
        {
            return effectType switch
            {
                ConsumableRuntimeEffectType.ApplyRegeneration => StatusEffectIds.Regeneration,
                ConsumableRuntimeEffectType.ApplyBleed => StatusEffectIds.Bleed,
                ConsumableRuntimeEffectType.ApplyPoison => StatusEffectIds.Poison,
                ConsumableRuntimeEffectType.ApplyBurn => StatusEffectIds.Burn,
                ConsumableRuntimeEffectType.ApplyStun => StatusEffectIds.Stun,
                ConsumableRuntimeEffectType.ApplyFreeze => StatusEffectIds.Freeze,
                ConsumableRuntimeEffectType.ApplySleep => StatusEffectIds.Sleep,
                ConsumableRuntimeEffectType.ApplyChill => StatusEffectIds.Chill,
                ConsumableRuntimeEffectType.ApplyParalysis => StatusEffectIds.Paralysis,
                ConsumableRuntimeEffectType.ApplyImmobilize => StatusEffectIds.Immobilize,
                ConsumableRuntimeEffectType.ApplyInvisibility => StatusEffectIds.Invisibility,
                ConsumableRuntimeEffectType.ApplyTaunt => StatusEffectIds.Taunt,
                _ => string.Empty
            };
        }

        public static bool IsImpliedStatus(ConsumableRuntimeEffectType effectType)
        {
            return !string.IsNullOrWhiteSpace(GetImpliedStatusId(effectType));
        }

        public static int GetDuration(
            ConsumableUseType useType,
            ConsumableRuntimeEffectType effectType,
            int explicitDuration = 0)
        {
            if (explicitDuration > 0)
            {
                return explicitDuration;
            }

            if (useType == ConsumableUseType.Potion)
            {
                return effectType switch
                {
                    ConsumableRuntimeEffectType.RegenHealth
                        or ConsumableRuntimeEffectType.RegenResource
                        or ConsumableRuntimeEffectType.ModifyMaxResource => GameDefinesRuntime.GetInt(
                            GameDefineIds.ConsumablePotionLongDuration, 6),
                    ConsumableRuntimeEffectType.ModifyDamage
                        or ConsumableRuntimeEffectType.ModifyDefense
                        or ConsumableRuntimeEffectType.ModifyPhysicalDefense
                        or ConsumableRuntimeEffectType.ModifyMagicalDefense
                        or ConsumableRuntimeEffectType.ModifyResistance
                        or ConsumableRuntimeEffectType.ModifyCritChance
                        or ConsumableRuntimeEffectType.ModifyDodge
                        or ConsumableRuntimeEffectType.ModifyAccuracy => GameDefinesRuntime.GetInt(
                            GameDefineIds.ConsumablePotionStatDuration, 5),
                    ConsumableRuntimeEffectType.ModifySpeed => GameDefinesRuntime.GetInt(
                        GameDefineIds.ConsumablePotionSpeedDuration, 3),
                    ConsumableRuntimeEffectType.ModifyActionPoints => GameDefinesRuntime.GetInt(
                        GameDefineIds.ConsumablePotionActionPointDuration, 4),
                    _ => 0
                };
            }

            if (useType == ConsumableUseType.Bomb)
            {
                return effectType switch
                {
                    ConsumableRuntimeEffectType.ApplyBleed
                        or ConsumableRuntimeEffectType.ApplyPoison => GameDefinesRuntime.GetInt(
                            GameDefineIds.ConsumableBombDamageOverTimeDuration, 4),
                    ConsumableRuntimeEffectType.ApplyBurn => GameDefinesRuntime.GetInt(
                        GameDefineIds.ConsumableBombBurnDuration, 3),
                    ConsumableRuntimeEffectType.ApplyStun
                        or ConsumableRuntimeEffectType.ApplyFreeze => GameDefinesRuntime.GetInt(
                            GameDefineIds.ConsumableBombControlDuration, 2),
                    ConsumableRuntimeEffectType.ModifyPhysicalDefense
                        or ConsumableRuntimeEffectType.ModifyMagicalDefense
                        or ConsumableRuntimeEffectType.ModifyDefense
                        or ConsumableRuntimeEffectType.ModifySpeed
                        or ConsumableRuntimeEffectType.ModifyCritChance
                        or ConsumableRuntimeEffectType.ModifyDodge
                        or ConsumableRuntimeEffectType.ModifyAccuracy
                        or ConsumableRuntimeEffectType.Slow => GameDefinesRuntime.GetInt(
                            GameDefineIds.ConsumableBombStatDuration, 3),
                    _ => 0
                };
            }

            return 0;
        }

        public static bool IsDamageOverTime(string statusId)
        {
            return string.Equals(statusId, StatusEffectIds.Bleed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Poison, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Burn, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsStatusEffect(ConsumableRuntimeEffectType effectType)
        {
            return IsImpliedStatus(effectType)
                || effectType == ConsumableRuntimeEffectType.ApplyStatus;
        }

        public static float ClampEffectValue(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

    }
}
