using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    // Authored equipment operations; queries never consume a charge or alter preview state.
    public static class UniqueEquipmentCombatRules
    {
        public const string FirstPhysicalHit = "first-physical-hit";
        public const string PhysicalRanged = "physical-ranged";

        private static IEnumerable<PassiveEffectOperationDefinition> Operations(CharacterInstance character)
        {
            foreach (var item in character?.Equipment?.GetEquippedItems() ?? System.Array.Empty<EquipmentItemInstance>())
            {
                if (item?.Definition is not UniqueEquipmentDefinition unique) continue;
                foreach (var effect in unique.GetEffects(item.ItemLevel) ?? new List<PassiveEffectDefinition>())
                {
                    if (effect == null || !effect.IsAlwaysActive || effect.IsTriggered) continue;
                    foreach (var operation in effect.Operations ?? new List<PassiveEffectOperationDefinition>())
                        if (operation?.Target == PassiveEffectTargetType.Self
                            && operation.Value?.Source == PassiveEffectValueSourceType.Constant)
                            yield return operation;
                }
            }
        }

        public static float IncomingMultiplier(BattleUnit target, DamageType type, AttackType attackType,
            bool? firstProtectionUsed = null)
        {
            if (target == null || type != DamageType.Physical) return 1f;
            var multiplier = 1f;
            foreach (var operation in Operations(target.Character))
            {
                if (operation.Type != PassiveEffectOperationType.ReduceIncomingDamage) continue;
                if (operation.ReferenceId == FirstPhysicalHit && !(firstProtectionUsed ?? target.RibbedTunicProtectionUsed)
                    || operation.ReferenceId == PhysicalRanged && attackType == AttackType.Ranged)
                    multiplier *= 1f - Mathf.Clamp01(operation.Value.NumberValue);
            }
            return multiplier;
        }

        public static void RecordHealthDamage(BattleUnit target, DamageType type, int healthDamage)
        {
            if (target == null || type != DamageType.Physical || healthDamage <= 0) return;
            foreach (var operation in Operations(target.Character))
                if (operation.Type == PassiveEffectOperationType.ReduceIncomingDamage
                    && operation.ReferenceId == FirstPhysicalHit)
                    target.RibbedTunicProtectionUsed = true;
        }

        public static void ApplyDerivedStats(CharacterInstance character)
        {
            if (character?.Stats == null || character.ResourcePool?.ResourceType != ResourceType.Mana) return;
            var bonus = 0f;
            foreach (var operation in Operations(character))
                if (operation.Type == PassiveEffectOperationType.MaxHealthFromMaxResource)
                    bonus += character.Stats.MaxResource.GetValue() * Mathf.Max(0, operation.Value.NumberValue);
            character.Stats.MaxHealth.SetEffectiveValue(character.Stats.MaxHealth.GetValue() + bonus);
        }

        public static float OrderCostMultiplier(BattleUnit unit,SkillData skill)
        {
            if(skill?.SkillId is not ("skill.warrior.destruction" or "skill.warrior.protection" or "skill.warrior.regroup"))return 1;
            var result=1f;
            foreach(var op in Operations(unit?.Character))
                if(op.Type==PassiveEffectOperationType.OrderActionCostReduction)result*=1-Mathf.Clamp01(op.Value.NumberValue);
            return result;
        }
        public static void BeginTurn(BattleUnit unit)
        {
            if(unit==null)return;
            if(unit.PoacherArmorShieldTurns>0 && --unit.PoacherArmorShieldTurns==0)
                unit.Character?.ShieldPool?.RemoveLayersBySourceId("equipment.poacher-armor");
            if(unit.PoacherArmorCooldown>0)unit.PoacherArmorCooldown--;
            TryPoacherShield(unit);
        }
        public static void TryPoacherShield(BattleUnit unit)
        {
            if(unit?.Character?.LifeState!=CharacterLifeState.Alive || unit.Character.CurrentHealth<=0 || unit.PoacherArmorCooldown>0)return;
            foreach(var op in Operations(unit.Character))
            {
                if(op.Type!=PassiveEffectOperationType.LowHealthShield)continue;
                var threshold=.30f;
                foreach(var parameter in op.Parameters ?? new List<PassiveEffectParameterDefinition>())
                    if(parameter.Key=="health_threshold")threshold=parameter.NumberValue;
                var thresholdHealth=Mathf.CeilToInt(unit.Character.Stats.MaxHealth.GetValue()*threshold-.0001f);
                if(unit.Character.CurrentHealth>=thresholdHealth)continue;
                unit.Character.ShieldPool ??= new ShieldPool();
                unit.Character.ShieldPool.RemoveLayersBySourceId("equipment.poacher-armor");
                unit.Character.ShieldPool.AddLayer(Mathf.RoundToInt(unit.Character.Stats.MaxHealth.GetValue()*op.Value.NumberValue),"equipment.poacher-armor");
                unit.PoacherArmorCooldown=5;unit.PoacherArmorShieldTurns=5;break;
            }
        }
    }
}
