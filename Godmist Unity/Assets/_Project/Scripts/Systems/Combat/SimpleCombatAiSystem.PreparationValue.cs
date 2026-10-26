using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
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
        // Preparation is identified by effects, not skill or creature IDs.
        // Defensive utilities and hybrid hostile skills retain their own scorer.
        private static bool IsOffensivePreparation(SkillData skill) => skill?.Effects?.Count > 0
            && skill.Effects.Any(IsOffensivePreparationEffect)
            && skill.Effects.All(effect => effect != null && effect.TargetModeIndex >= 0
                && effect.TargetModeIndex < skill.TargetModes.Count
                && IsPreparationMode(skill.TargetModes[effect.TargetModeIndex])
                && (IsOffensivePreparationEffect(effect) || effect.EffectType is EffectType.Heal or EffectType.HealPercent
                    || effect.EffectType == EffectType.ModifyActionValue && effect.ActionValuePercent <= 0f
                    || effect.EffectType == EffectType.ApplyStatus && string.Equals(effect.StatusId, "invisibility", StringComparison.OrdinalIgnoreCase)
                    || effect.EffectType == EffectType.RemoveStatus && !string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId)
                        && string.Equals(effect.StatusId, skill.RequiredCasterStatusId, StringComparison.OrdinalIgnoreCase)));

        private static bool IsPreparationMode(SkillTargetModeData mode) => mode?.TargetType == TargetType.Caster
            || mode?.TargetType == TargetType.All && mode.TargetingType == TargetingType.Allies;

        private static bool IsOffensivePreparationEffect(SkillEffectData effect) => effect != null
            && effect.DurationTurns > 0
            && (effect.EffectType == EffectType.ApplyUniqueBuff && effect.UniqueBuffType == UniqueBuffType.Hawkeye
                || effect.ModifierValue > 0f && (effect.EffectType == EffectType.ModifyVar
                    && effect.ModifierVariableType is GameplayVariableType.DamageDealtPercent or GameplayVariableType.HitChanceBonus
                    || effect.EffectType == EffectType.ModifyStat && effect.ModifierStatType is CharacterStatType.MinAttack
                        or CharacterStatType.MaxAttack or CharacterStatType.CritChance or CharacterStatType.CritDamage
                        or CharacterStatType.WeaponAccuracy or CharacterStatType.Speed
                        or CharacterStatType.PhysicalDefensePenetration or CharacterStatType.MagicalDefensePenetration));

        private static bool HasActiveOffensivePreparation(BattleUnit unit) => unit.Character.GetActiveSkills()
            .Where(IsOffensivePreparation).Any(skill => skill.Effects.Select((effect, index) => (effect, index)).Any(pair =>
                IsOffensivePreparationEffect(pair.effect) && (pair.effect.EffectType == EffectType.ApplyUniqueBuff
                    ? CombatDamageSystem.HasUniqueBuff(unit, pair.effect.UniqueBuffType)
                    : PassiveEffectLifecycleSystem.HasActiveEffect(unit.Character,
                        $"{skill.SkillId}.{(pair.effect.EffectType == EffectType.ModifyStat ? "modify_stat" : "modify_var")}.{pair.index}"))));

        private IEnumerable<bool> EvaluatePreparationWork(AiTurnPlanNode node, CombatSession live, BattleUnit liveActor)
        {
            node.CarryoverValue = node.PreparationPriority = 0f;
            if (node.Preparations.Count == 0) yield break;
            foreach (var recipient in node.PreparationRecipients)
            {
                yield return false;
                var prepared = CopyPlanningUnit(node.World.Units[recipient]);
                if (prepared?.Character?.LifeState != CharacterLifeState.Alive) continue;
                var original = live.Units[recipient];
                var unprepared = CopyPlanningUnit(prepared);
                RemoveProjectedPreparations(unprepared, node.Preparations, original, int.MaxValue);
                unprepared.MaxActionPoints = PlanningFullBudget(node.World, unprepared);
                unprepared.CurrentActionPoints = Mathf.Min(unprepared.CurrentActionPoints, unprepared.MaxActionPoints);
                var before = new AiResult<float>(); var after = new AiResult<float>();
                if (original == liveActor)
                {
                    foreach (var checkpoint in FindPreparationOpportunityWork(node.World, unprepared, original, live, liveActor, false, before)) yield return checkpoint;
                    foreach (var checkpoint in FindPreparationOpportunityWork(node.World, prepared, original, live, liveActor, false, after)) yield return checkpoint;
                    // Used only to keep promising setup branches in the beam. It is not
                    // added to the final score: following attacks already earn that gain.
                    node.PreparationPriority += Mathf.Max(0f, after.Value - before.Value) * node.PreparationConfidence;
                }
                if (!node.Preparations.Any(skill => skill.Effects.Any(effect => IsOffensivePreparationEffect(effect) && effect.DurationTurns > 1)))
                    continue;
                RemoveProjectedPreparations(prepared, node.Preparations, original, 1);
                prepared.MaxActionPoints = PlanningFullBudget(node.World, prepared);
                // One conservative next-turn window, not free damage for every turn
                // written in the tooltip. No assumed mana regeneration, new target,
                // movement or discovery. Already-spent resources stay spent.
                foreach (var checkpoint in FindPreparationOpportunityWork(node.World, unprepared, original, live, liveActor, true, before)) yield return checkpoint;
                foreach (var checkpoint in FindPreparationOpportunityWork(node.World, prepared, original, live, liveActor, true, after)) yield return checkpoint;
                var rate = prepared.Character.Stats.Speed.GetValue() / Mathf.Max(1f, unprepared.Character.Stats.Speed.GetValue());
                // Speed also changes turn frequency. This is a bounded opportunity
                // estimate, not a forecast of the full initiative timeline.
                node.PreparationTurnDelays.TryGetValue(recipient, out var delay);
                rate /= 1f + delay;
                node.CarryoverValue += Mathf.Max(0f, after.Value * Mathf.Min(3f, rate) - before.Value)
                    * .5f * node.PreparationConfidence;
                if (original == liveActor && after.Value <= 0f && rate > 1f && !StatusEffectSystem.IsImmobilized(prepared))
                {
                    var approach = new AiResult<float>();
                    foreach (var checkpoint in FindSpeedPreparationApproachWork(node.World, prepared, live, liveActor, approach)) yield return checkpoint;
                    node.CarryoverValue += approach.Value * Mathf.Min(2f, rate - 1f) * .5f * node.PreparationConfidence;
                }
            }
        }

        private static int PlanningFullBudget(CombatSession world, BattleUnit unit) => Mathf.Max(0,
            Mathf.RoundToInt(unit.Character.Stats.Speed.GetValue() * world.ActionPointsPerSpeed * ActionPointUnits.PerPoint
                * (1f + EquipmentEffectSystem.Sum(unit.Character, GalduryteEffectType.ActionPointsPercent))));

        private static void RemoveProjectedPreparations(BattleUnit unit, IEnumerable<SkillData> preparations,
            BattleUnit liveActor, int maximumDuration)
        {
            var oldValues = PlanningStatTypes.ToDictionary(type => type, type => CharacterStatPipelineSystem.EvaluateModifiers(
                unit.Character.Stats.GetStat(type).BaseValue, type, unit.Character.PassiveEffects));
            foreach (var skill in preparations)
            for (var index = 0; index < skill.Effects.Count; index++)
            {
                var effect = skill.Effects[index];
                if (!IsOffensivePreparationEffect(effect) || effect.DurationTurns > maximumDuration) continue;
                if (effect.EffectType == EffectType.ApplyUniqueBuff)
                {
                    if (!CombatDamageSystem.HasUniqueBuff(liveActor, effect.UniqueBuffType))
                        unit.Character.PassiveEffects.RemoveAll(active => active?.EffectId == $"{skill.SkillId}.unique_buff.{index}");
                    continue;
                }
                var suffix = effect.EffectType == EffectType.ModifyStat ? "modify_stat" : "modify_var";
                var id = $"{skill.SkillId}.{suffix}.{index}";
                if (!PassiveEffectLifecycleSystem.HasActiveEffect(liveActor.Character, id))
                    unit.Character.PassiveEffects.RemoveAll(active => active?.EffectId == id);
            }
            foreach (var type in PlanningStatTypes)
            {
                ref var stat = ref unit.Character.Stats.GetStat(type);
                var value = CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue, type, unit.Character.PassiveEffects);
                stat.SetEffectiveValue(Mathf.Max(0f, stat.GetValue() + value - oldValues[type]));
                if (type == CharacterStatType.WeaponAccuracy)
                    unit.Character.WeaponAccuracy = Mathf.Max(0f, unit.Character.WeaponAccuracy + value - oldValues[type]);
            }
        }

        private IEnumerable<bool> FindSpeedPreparationApproachWork(CombatSession world, BattleUnit caster,
            CombatSession live, BattleUnit observer, AiResult<float> result)
        {
            result.Value = 0f;
            foreach (var skill in observer.Character.GetActiveSkills())
            {
                yield return false;
                if (!SupportsTurnProjection(skill) || skill.Effects.Any(effect => effect.EffectType == EffectType.Charge)
                    || !skill.Effects.Any(effect => effect.EffectType == EffectType.DealDamage)) continue;
                var cost = PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, skill);
                if (cost > 0 && (caster.Character.ResourcePool?.ResourceType != skill.ResourceType
                    || caster.Character.ResourcePool.CurrentValue < cost)) continue;
                for (var index = 0; index < world.Units.Count; index++)
                {
                    yield return false;
                    var target = world.Units[index];
                    if (!IsObservedLivingOpponent(live, observer, live.Units[index])
                        || target?.Character?.LifeState != CharacterLifeState.Alive) continue;
                    var path = new AiResult<List<GridPosition>>();
                    foreach (var checkpoint in FindPathWork(world, caster, target.Position, false, path,
                        cell => CanPreparedAttackFrom(world, caster, skill, target, cell))) yield return checkpoint;
                    if (path.Value == null || path.Value.Count == 0) continue;
                    var damage = skill.Effects.First(effect => effect.EffectType == EffectType.DealDamage);
                    var expected = PreparationAttackUtility(world, caster, target, skill, damage.TargetModeIndex);
                    result.Value = Mathf.Max(result.Value, Mathf.Min(expected, target.Character.CurrentHealth) / Mathf.Max(1, path.Value.Count));
                }
            }
        }

        private IEnumerable<bool> FindPreparationOpportunityWork(CombatSession world, BattleUnit caster, BattleUnit original,
            CombatSession live, BattleUnit observer, bool fullTurn, AiResult<float> result)
        {
            result.Value = 0f;
            var ap = fullTurn ? caster.MaxActionPoints : caster.CurrentActionPoints;
            foreach (var skill in original.Character.GetActiveSkills())
            {
                yield return false;
                if (!SupportsTurnProjection(skill) || skill.Effects.Any(effect => effect.EffectType == EffectType.Charge)) continue;
                var damage = skill.Effects.FirstOrDefault(effect => effect.EffectType == EffectType.DealDamage);
                if (damage == null || skill.TargetModes[damage.TargetModeIndex].TargetType != TargetType.Single) continue;
                var cost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);
                var count = cost > 0 ? Mathf.Min(TurnPlanDepth, ap / cost) : 1;
                var resource = PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, skill);
                if (resource > 0)
                {
                    if (caster.Character.ResourcePool?.ResourceType != skill.ResourceType) continue;
                    count = Mathf.Min(count, caster.Character.ResourcePool.CurrentValue / resource);
                }
                if (count <= 0) continue;
                for (var index = 0; index < world.Units.Count; index++)
                {
                    yield return false;
                    var target = world.Units[index];
                    if (!IsObservedLivingOpponent(live, observer, live.Units[index])
                        || !CanPreparedAttackFrom(world, caster, skill, target, caster.Position)
                        || !SkillExecutionSystem.CanPlanEffect(world, damage, caster, target)) continue;
                    var expected = PreparationAttackUtility(world, caster, target, skill, damage.TargetModeIndex);
                    result.Value = Mathf.Max(result.Value, Mathf.Min(expected * count,
                        target.Character.CurrentHealth + (target.Character.ShieldPool?.TotalShield ?? 0)));
                }
            }
        }

        private static float PreparationAttackUtility(CombatSession world, BattleUnit caster, BattleUnit target, SkillData skill, int mode)
        {
            var damage = CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(world, caster, target, skill, mode);
            var utility = damage;
            if (damage >= target.Character.CurrentHealth + (target.Character.ShieldPool?.TotalShield ?? 0)) return utility;
            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(caster, target, skill, mode);
            foreach (var effect in skill.Effects)
            {
                if (effect.EffectType != EffectType.ApplyStatus || effect.TargetModeIndex != mode
                    || !SkillExecutionSystem.CanPlanEffect(world, effect, caster, target)
                    || StatusEffectSystem.HasStatus(target, effect.StatusId) && !StatusEffectSystem.IsDamageOverTimeStatus(effect.StatusId)) continue;
                var chance = string.Equals(preview.StatusId, effect.StatusId, StringComparison.OrdinalIgnoreCase) ? preview.StatusChance
                    : StatusEffectSystem.CalculateStatusApplicationChance(caster, target, effect.StatusId, effect.BaseChance);
                utility += chance * (IsHardControl(effect.StatusId)
                    ? Ai(Godmist.Data.Configuration.GameDefineIds.AiHardControlBonus, 300f)
                    : Ai(Godmist.Data.Configuration.GameDefineIds.AiSoftControlBonus, 100f));
            }
            return utility;
        }
    }
}
