using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Configuration;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        private AiTurnPlanNode ProjectSkill(AiTurnPlanNode node, AiSkillSelection selection,
            CombatSession live, BattleUnit actor)
        {
            var skill = selection.Skill;
            if (!SupportsTurnProjection(skill)) return null;
            var next = CopyPlanNode(node, live, actor);
            next.OnlyMovement = false;
            var globalPreparation = IsOffensivePreparation(skill) && skill.TargetModes.Any(mode => mode.TargetType == TargetType.All);
            var preparationRadius = TurnPlanDepth + GetPreferredCombatRange(actor);
            bool InPreparationScope(BattleUnit unit) => !globalPreparation || unit == node.World.ActiveUnit || unit == next.World.ActiveUnit
                || GetManhattanDistance(unit.Position, actor.Position) <= preparationRadius;
            foreach (var targets in selection.TargetsByModeIndex.Values)
            foreach (var target in targets)
            {
                if (!InPreparationScope(target)) continue;
                var index = node.World.Units.IndexOf(target);
                if (next.OwnedUnits.Add(index))
                {
                    next.World.Units[index] = CopyPlanningUnit(next.World.Units[index]);
                    next.World.Units[index].WarriorState.Session = next.World;
                }
            }
            var mapped = RemapSelection(selection, node.World, next.World);
            var caster = next.World.ActiveUnit;
            var price = CombatActionCostCalculator.CalculateSkillCostForTarget(caster, skill, mapped.TargetsByModeIndex.Values.SelectMany(value => value).FirstOrDefault());
            var resource = PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, skill);
            if (!WarriorCombatSystem.CanActivate(caster, skill)) return null;
            var previewCaster = skill.SkillId == WarriorDischargeRules.SkillId ? CopyPlanningUnit(caster) : caster;
            if (price > caster.CurrentActionPoints || resource > 0
                && (caster.Character.ResourcePool?.ResourceType != skill.ResourceType || caster.Character.ResourcePool.CurrentValue < resource)) return null;
            caster.CurrentActionPoints -= price;
            var previousResource = caster.Character.ResourcePool?.CurrentValue ?? 0;
            caster.Character.ResourcePool?.Drain(resource);
            AdjustProjectedMomentumSpeed(caster, previousResource);
            if (PassiveEffectCombatExecutionSystem.IsOffensiveSkill(skill) && skill.SkillId != WarriorDischargeRules.SkillId) ProjectResourceGain(caster, 0, true);
            var utility = -ActionPointUnits.ToPoints(price) * Ai(GameDefineIds.AiActionCostPenaltyPerPoint, .35f)
                - resource * Ai(GameDefineIds.AiResourceCostPenaltyPerPoint, .1f);
            var requiredChance = string.IsNullOrEmpty(skill.RequiredCasterStatusId) ? 1f
                : ProjectedStatusChance(node, node.World.ActiveUnit, skill.RequiredCasterStatusId);
            var dischargeRefunded = false;
            var preparations = FindConditionalStatusPreparations(caster, caster.Character.GetActiveSkills());
            foreach (var index in SkillEffectIdentity.ExecutionOrder(skill))
            {
                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, skill.Effects[index]);
                if (!mapped.TargetsByModeIndex.TryGetValue(effect.TargetModeIndex, out var targets)) return null;
                foreach (var target in targets)
                {
                    if (!InPreparationScope(target)) continue;
                    if (!SkillExecutionSystem.CanPlanEffect(next.World, effect, caster, target)) continue;
                    var chance = EffectPrerequisiteChance(next, skill, effect, caster, target) * requiredChance;
                    if (chance <= 0f) continue;
                    var amount = 0f;
                    switch (effect.EffectType)
                    {
                        case EffectType.DealDamage:
                        {
                            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(previewCaster, target, skill, effect.TargetModeIndex);
                            var expected = CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(next.World, previewCaster, target, skill, effect.TargetModeIndex, skill.Effects[index]);
                            var shieldBefore = target.Character.ShieldPool?.TotalShield ?? 0;
                            amount = DamageUtility(next.World, previewCaster, target, skill, skill.Effects[index],live,actor);
                            var healthLoss = target.Character.ShieldPool?.AbsorbDamage(Mathf.RoundToInt(expected)) ?? Mathf.RoundToInt(expected);
                            var certainKill = preview.HitChance >= 1f && preview.Damage.Minimum >= target.Character.CurrentHealth
                                + shieldBefore;
                            target.Character.CurrentHealth = Mathf.Max(certainKill ? 0 : 1, target.Character.CurrentHealth - healthLoss);
                            if (certainKill)
                            {
                                target.Character.LifeState = CharacterLifeState.Dead;
                                if (skill.SkillId == WarriorDischargeRules.SkillId && !dischargeRefunded)
                                {
                                    dischargeRefunded = true;
                                    if (!ResourceRuleSystem.IsResourceGainBlocked(caster))
                                        caster.Character.ResourcePool?.Restore(WarriorDischargeRules.RequestedRefund(resource, WarriorCombatSystem.B(caster, "wounding")));
                                }
                            }
                            if (effect.LifeStealPercent > 0f)
                                caster.Character.CurrentHealth += Mathf.RoundToInt(Mathf.Min(GetMissingHealth(caster),Mathf.Max(0,expected-shieldBefore)*effect.LifeStealPercent));
                            if (effect.AllyLifeStealPercent > 0f) next.Terminal = true;
                            break;
                        }
                        case EffectType.ApplyStatus:
                        {
                            if (string.IsNullOrWhiteSpace(effect.StatusId)) return null;
                            var application = StatusEffectSystem.CalculateStatusApplicationChance(caster, target, effect.StatusId, effect.BaseChance);
                            if (target.IsPlayerSide != caster.IsPlayerSide)
                            {
                                var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, skill, effect.TargetModeIndex);
                                if (string.Equals(preview.StatusId, effect.StatusId, StringComparison.OrdinalIgnoreCase)) application = preview.StatusChance;
                            }
                            if (application <= 0f || target.Character.LifeState == CharacterLifeState.Dead) break;
                            if (!IsOffensivePreparation(skill))
                                amount = StatusUtility(live, actor, live.Units[next.World.Units.IndexOf(target)], skill, effect);
                            if (!StatusEffectSystem.HasStatus(target, effect.StatusId))
                            {
                                target.Character.PassiveEffects.Add(StatusEffectSystem.NormalizeStatus(new PassiveEffectRuntime
                                {
                                    EffectId = effect.StatusId, Lifetime = PassiveEffectLifetime.Timed,
                                    RemainingDuration = Math.Max(1, effect.DurationTurns)
                                }));
                                target.Character.Stats.Speed.SetEffectiveValue(CharacterStatPipelineSystem.CalculateEffectiveSpeed(target.Character));
                                next.StatusConfidence[(next.World.Units.IndexOf(target), effect.StatusId)] = application * chance;
                            }
                            break;
                        }
                        case EffectType.ModifyStat:
                        case EffectType.ModifyVar:
                        {
                            var suffix = effect.EffectType == EffectType.ModifyStat ? "modify_stat" : "modify_var";
                            var id = SkillEffectIdentity.RuntimeId(skill, index, suffix);
                            if (PassiveEffectLifecycleSystem.HasActiveEffect(target.Character, id)) break;
                            var runtime = new PassiveEffectRuntime { EffectId = id,
                                Lifetime = effect.DurationTurns > 0 ? PassiveEffectLifetime.Timed : PassiveEffectLifetime.CombatScoped,
                                RemainingDuration = effect.DurationTurns };
                            if (effect.EffectType == EffectType.ModifyStat)
                            {
                                ref var stat = ref target.Character.Stats.GetStat(effect.ModifierStatType);
                                var before = CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue, effect.ModifierStatType, target.Character.PassiveEffects);
                                runtime.StatModifiers = new List<StatModifier> { new() { StatType = effect.ModifierStatType,
                                    Operation = effect.ModifierOperation, Stacking = effect.ModifierStacking, Value = effect.ModifierValue } };
                                target.Character.PassiveEffects.Add(runtime);
                                var after = CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue, effect.ModifierStatType, target.Character.PassiveEffects);
                                stat.SetEffectiveValue(effect.ModifierStatType == CharacterStatType.Speed
                                    ? CharacterStatPipelineSystem.CalculateEffectiveSpeed(target.Character)
                                    : Mathf.Max(0f, stat.GetValue() + (after - before)));
                                if (effect.ModifierStatType == CharacterStatType.WeaponAccuracy) target.Character.WeaponAccuracy = stat.GetValue();
                            }
                            else
                            {
                                runtime.VariableModifiers = new List<GameplayVariableModifier> { new() {
                                    VariableType = effect.ModifierVariableType, Stacking = effect.ModifierStacking, Value = effect.ModifierValue } };
                                target.Character.PassiveEffects.Add(runtime);
                            }
                            amount = IsOffensivePreparation(skill) || IsApproachCharge(skill) ? 0f : DefensiveModifierUtility(live,actor,live.Units[next.World.Units.IndexOf(target)],effect);
                            var paired = preparations.FirstOrDefault(preparation => preparation.Skill == skill && preparation.StrongIndex == index);
                            if (paired != null) amount *= paired.Strong.ModifierValue / paired.Weak.ModifierValue;
                            // An uncertain modifier is a leaf: do not invent a
                            // fractional speed/AP budget for a random outcome.
                            if (chance < 1f) next.Terminal = true;
                            if (IsOffensivePreparation(skill)) next.PreparationConfidence = Mathf.Min(next.PreparationConfidence, chance);
                            break;
                        }
                        case EffectType.PaladinHeal:
                        case EffectType.PaladinRegeneration:
                        {
                            var recipient = next.World.Units.IndexOf(target);
                            var previous = next.PendingHealing.TryGetValue(recipient, out var pending) ? pending : PendingRegeneration(target);
                            var forecast = PaladinCombatSystem.ForecastHealing(next.World, caster, target, skill, effect,
                                PaladinCombatSystem.IsSingleHealingTarget(skill, mapped.TargetsByModeIndex), RecoveryForecastTurns);
                            amount = PaladinRecoveryUtility(next.World, caster, target, skill, effect,
                                PaladinCombatSystem.IsSingleHealingTarget(skill, mapped.TargetsByModeIndex), previous);
                            target.Character.CurrentHealth += Mathf.RoundToInt(forecast.Immediate);
                            if (forecast.Immediate > 0 && target.Character.LifeState == CharacterLifeState.Downed)
                            { target.Character.LifeState = CharacterLifeState.Alive; target.DownedTurnsRemaining = 0; }
                            next.PendingHealing[recipient] = effect.EffectType == EffectType.PaladinRegeneration
                                ? forecast.Future : Mathf.Min(previous, GetMissingHealth(target));
                            if (skill.SkillId == "skill.paladin.heal") target.PaladinState.HealingDiscount = false;
                            // Secondary buffs and timeline changes need a real refresh, just like arbitrary cleanses.
                            // Their effects are not invented in the detached same-turn projection.
                            if (caster != target && PaladinCombatSystem.Points(caster.Character, PaladinCombatSystem.Priest) > 0 && GetMissingHealth(caster) > 0
                                || PaladinCombatSystem.P(caster, "encouragement") > 0
                                || PaladinCombatSystem.P(caster, "anointed_warrior") > 0
                                || skill.SkillId == "skill.paladin.group_heal" && (PaladinCombatSystem.P(caster, "battle_zeal") > 0
                                    || PaladinCombatSystem.P(caster, "universal_grace") > 0)) next.Terminal = true;
                            break;
                        }
                        case EffectType.Heal:
                        case EffectType.HealPercent:
                        {
                            var healing = HealingAmount(caster,target,effect);
                            amount = HealingUtility(live, actor, live.Units[next.World.Units.IndexOf(target)],healing);
                            target.Character.CurrentHealth += Mathf.RoundToInt(healing);
                            if(healing>0 && target.Character.LifeState==CharacterLifeState.Downed){target.Character.LifeState=CharacterLifeState.Alive;target.DownedTurnsRemaining=0;}
                            break;
                        }
                        case EffectType.Shield:
                            amount = ShieldUtility(live,caster,target,effect,actor,live.Units[next.World.Units.IndexOf(target)]);
                            target.Character.ShieldPool.AddLayer(Mathf.RoundToInt(ShieldAmount(caster,effect)));
                            break;
                        case EffectType.RemoveStatus:
                            // Removing an arbitrary status can also remove stat
                            // payloads. Leave its downstream reconciliation to
                            // the real owner instead of assuming unchanged costs.
                            if (target.Character.PassiveEffects.Any(active => string.Equals(active?.EffectId, effect.StatusId,
                                StringComparison.OrdinalIgnoreCase) && active.GetPayload<StatModifiersPayload>()?.Modifiers?.Count > 0))
                                next.Terminal = true;
                            amount = CleanseUtility(live,actor,live.Units[next.World.Units.IndexOf(target)],effect.StatusId);
                            target.Character.PassiveEffects.RemoveAll(active => string.Equals(active?.EffectId, effect.StatusId, StringComparison.OrdinalIgnoreCase));
                            next.StatusConfidence.Remove((next.World.Units.IndexOf(target), effect.StatusId));
                            break;
                        case EffectType.ApplyUniqueBuff:
                            if (!CombatDamageSystem.HasUniqueBuff(target, effect.UniqueBuffType))
                            {
                                target.Character.PassiveEffects.Add(new PassiveEffectRuntime
                                {
                                    EffectId = SkillEffectIdentity.RuntimeId(skill, index, "unique_buff"),
                                    Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = Math.Max(1, effect.DurationTurns),
                                    IsUniqueCombatBuff = true, UniqueBuffType = effect.UniqueBuffType,
                                    Stacks = Math.Max(1, effect.UniqueBuffStacks)
                                });
                                amount = IsOffensivePreparation(skill) ? 0f : Ai(GameDefineIds.AiModifyBonus, 40f) * effect.DurationTurns;
                            }
                            break;
                        case EffectType.ModifyActionValue:
                            // A self-delay is a real cost of these preparations,
                            // not an AP refund. Do not reorder the live timeline.
                            var recipientIndex = next.World.Units.IndexOf(target);
                            next.PreparationTurnDelays.TryGetValue(recipientIndex, out var previousDelay);
                            next.PreparationTurnDelays[recipientIndex] = previousDelay + Mathf.Abs(effect.ActionValuePercent);
                            break;
                        case EffectType.Charge:
                            if (!preparationChargePreview.TryPreviewCharge(next.World, caster, target, effect,
                                    out var positions, out var entered, out var complete)) return null;
                            foreach (var pair in positions)
                            {
                                var movedIndex = next.World.Units.IndexOf(pair.Key);
                                if (next.OwnedUnits.Add(movedIndex))
                                {
                                    next.World.Units[movedIndex] = CopyPlanningUnit(pair.Key);
                                    next.World.Units[movedIndex].WarriorState.Session = next.World;
                                }
                                next.World.Units[movedIndex].Position = pair.Value;
                            }
                            var chargeRisk = live.EvaluateTrapRisk?.Invoke(actor, entered) ?? 0f;
                            if(!float.IsFinite(chargeRisk))return null;
                            utility -= Mathf.Max(0f, chargeRisk) * Ai(GameDefineIds.AiHardControlBonus, 300f);
                            next.Terminal |= !complete || chargeRisk > 0f;
                            break;
                    }
                    utility += amount * chance;
                }
            }
            if (!IsOffensivePreparation(skill) && !IsApproachCharge(skill))
                utility += Mathf.Min(Mathf.Max(0,utility)*.15f,CalculateRoleBonus(node.World.ActiveUnit, skill, selection.TargetsByModeIndex)
                    + CalculatePersonalityBonus(node.World.ActiveUnit, skill, selection.TargetsByModeIndex));
            next.Score += utility + SetupOpportunity(live,actor,skill);
            if (IsOffensivePreparation(skill))
            {
                next.Preparations.Add(skill);
                foreach (var recipients in mapped.TargetsByModeIndex.Values)
                foreach (var recipient in recipients)
                    if (InPreparationScope(recipient)) next.PreparationRecipients.Add(next.World.Units.IndexOf(recipient));
                if (!IsEmergencyPreparationHeal(live, actor, skill)) next.PendingPreparation = true;
            }
            if (IsApproachCharge(skill)) { next.PendingPreparation = next.PendingCharge = true; next.Used.Add(skill); }
            if (skill.Effects.Any(effect => effect.EffectType == EffectType.DealDamage)) next.PendingPreparation = next.PendingCharge = false;
            if (PassiveEffectCombatExecutionSystem.IsOffensiveSkill(skill))
                caster.Character.PassiveEffects.RemoveAll(active => string.Equals(active?.EffectId, "invisibility", StringComparison.OrdinalIgnoreCase));
            if (price == 0 || IsOffensivePreparation(skill) || skill.Effects.All(effect => effect.EffectType is EffectType.ModifyStat or EffectType.ModifyVar
                or EffectType.RemoveStatus or EffectType.ApplyUniqueBuff
                || effect.EffectType == EffectType.ApplyStatus && skill.TargetModes[effect.TargetModeIndex].TargetType == TargetType.Caster))
                next.Used.Add(skill);
            if (skill.SkillId == WarriorDischargeRules.SkillId) WarriorDischargeRules.UpdateProjectedBonuses(caster, previousResource);
            RefreshProjectedAp(next.World);
            return next;
        }

        private static bool IsApproachCharge(SkillData skill) => skill.Effects.Any(effect => effect?.EffectType == EffectType.Charge)
            && !skill.Effects.Any(effect => effect?.EffectType == EffectType.DealDamage);

        private static float ProjectedStatusChance(AiTurnPlanNode node, BattleUnit target, string id) =>
            node.StatusConfidence.TryGetValue((node.World.Units.IndexOf(target), id), out var value) ? value : 1f;

        private static float EffectPrerequisiteChance(AiTurnPlanNode node, SkillData skill, SkillEffectData effect, BattleUnit caster, BattleUnit target)
        {
            var chance = effect.EffectType is EffectType.ModifyStat or EffectType.ModifyVar ? Mathf.Clamp01(effect.BaseChance) : 1f;
            foreach (var condition in effect.Conditions)
            {
                if (condition == null) continue;
                if (condition.ConditionType == EffectConditionType.ReferencedEffectSucceeded && effect.EffectType != EffectType.ApplyStatus)
                    chance *= CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, skill,
                        skill.Effects[SkillEffectIdentity.ReferenceIndex(skill, condition)].TargetModeIndex).HitChance;
                if (condition.ConditionType == EffectConditionType.CasterHasStatus) chance *= ProjectedStatusChance(node, caster, condition.StatusId);
                if (condition.ConditionType == EffectConditionType.TargetHasStatus) chance *= ProjectedStatusChance(node, target, condition.StatusId);
                if (condition.ConditionType == EffectConditionType.AnyOpponentHasStatus)
                {
                    var strongest = 0f;
                    foreach (var opponent in node.World.Units)
                        if (opponent?.Character != null && opponent.IsPlayerSide != caster.IsPlayerSide && StatusEffectSystem.HasStatus(opponent, condition.StatusId))
                            strongest = Mathf.Max(strongest, ProjectedStatusChance(node, opponent, condition.StatusId));
                    chance *= strongest;
                }
            }
            return chance;
        }

        private void ProjectResourceGain(BattleUnit caster, int movementCost, bool offensive)
        {
            var pool = caster.Character.ResourcePool;
            if (pool == null || ResourceRuleSystem.IsResourceGainBlocked(caster) || !planningResourceRules.TryGetRule(pool.ResourceType, out var rule)) return;
            var before = pool.CurrentValue;
            if (offensive) rule.ProcessOffensiveSkillUsed(pool);
            else if (rule is MomentumResourceRule momentum) momentum.ProcessMovementSteps(pool, caster.MovementStepsThisTurn);
            else rule.ProcessMovement(pool, movementCost);
            AdjustProjectedMomentumSpeed(caster, before);
        }

        private static void AdjustProjectedMomentumSpeed(BattleUnit caster, int previous)
        {
            var pool = caster.Character.ResourcePool;
            var difference = MomentumResourceRule.SpeedBonus(pool, pool?.CurrentValue ?? 0) - MomentumResourceRule.SpeedBonus(pool, previous);
            if (difference != 0) caster.Character.Stats.Speed.SetEffectiveValue(CharacterStatPipelineSystem.CalculateEffectiveSpeed(caster.Character));
        }

        private static void RefreshProjectedAp(CombatSession world)
        {
            var caster = world.ActiveUnit;
            caster.MaxActionPoints = Mathf.Max(0, Mathf.RoundToInt(caster.Character.Stats.Speed.GetValue()
                * world.ActionPointsPerSpeed * ActionPointUnits.PerPoint
                * (1 + EquipmentEffectSystem.Sum(caster.Character, GalduryteEffectType.ActionPointsPercent))));
            caster.CurrentActionPoints = Mathf.Min(caster.CurrentActionPoints, caster.MaxActionPoints);
        }
    }
}
