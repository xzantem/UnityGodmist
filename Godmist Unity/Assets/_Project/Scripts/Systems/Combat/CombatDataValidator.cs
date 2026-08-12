using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatDataValidationReport
    {
        private readonly List<string> errors = new();
        private readonly List<string> warnings = new();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool IsValid => errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                warnings.Add(message);
            }
        }
    }

    public static class CombatDataValidator
    {
        public static CombatDataValidationReport ValidateSkill(SkillData skill)
        {
            var report = new CombatDataValidationReport();

            if (skill == null)
            {
                report.AddError("SkillData cannot be null.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(skill.SkillId))
            {
                report.AddError("Skill ID is required.");
            }

            if (string.IsNullOrWhiteSpace(skill.DisplayName))
            {
                report.AddError("Display Name is required.");
            }

            if (skill.SkillAccuracy < 0f || skill.SkillAccuracy > 100f)
            {
                report.AddError("Skill Accuracy must be between 0 and 100.");
            }

            if (skill.ActionCost < 0 || skill.ActionCost > 100)
            {
                report.AddError(
                    "Action Cost must be a Max AP percentage between 0 and 100.");
            }

            if (skill.ResourceCost < 0)
            {
                report.AddError("Resource Cost cannot be negative.");
            }

            if (skill.TargetModes == null || skill.TargetModes.Count == 0)
            {
                report.AddError("Skill must define at least one Target Mode.");
            }
            else
            {
                ValidateTargetModes(skill, report);
            }

            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                report.AddError("Skill must define at least one effect.");
            }
            else
            {
                ValidateEffects(skill, report);
            }

            return report;
        }

        public static CombatDataValidationReport ValidateEncounter(
            EncounterDefinition encounter)
        {
            var report = new CombatDataValidationReport();

            if (encounter == null)
            {
                report.AddError("EncounterDefinition cannot be null.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(encounter.Id))
            {
                report.AddError("Encounter ID is required.");
            }

            if (string.IsNullOrWhiteSpace(encounter.DisplayName))
            {
                report.AddError("Display Name is required.");
            }

            if (encounter.GridWidth <= 0 || encounter.GridHeight <= 0)
            {
                report.AddError("Encounter grid dimensions must be greater than zero.");
            }

            if (encounter.Enemies == null || encounter.Enemies.Count == 0)
            {
                report.AddError(
                    "Encounter must define at least one enemy.");
                return report;
            }

            var usedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            var usedSpawnPositions = new HashSet<(int x, int y)>();

            for (var index = 0; index < encounter.Enemies.Count; index++)
            {
                var enemy = encounter.Enemies[index];
                var prefix = $"Enemy [{index}]";

                if (enemy == null)
                {
                    report.AddError($"{prefix}: enemy entry cannot be null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(enemy.InstanceId))
                {
                    report.AddError($"{prefix}: Instance ID is required.");
                }
                else if (!usedInstanceIds.Add(enemy.InstanceId))
                {
                    report.AddError(
                        $"{prefix}: Instance ID '{enemy.InstanceId}' is duplicated.");
                }

                if (enemy.CharacterDefinition == null)
                {
                    report.AddError(
                        $"{prefix}: Character Definition is required.");
                }

                if (enemy.ClassData == null)
                {
                    report.AddError($"{prefix}: Class Data is required.");
                }

                var spawn = enemy.SpawnPosition;

                if (spawn.x < 0
                    || spawn.y < 0
                    || spawn.x >= encounter.GridWidth
                    || spawn.y >= encounter.GridHeight)
                {
                    report.AddError(
                        $"{prefix}: Spawn Position ({spawn.x}, {spawn.y}) " +
                        "is outside the encounter grid.");
                }

                if (!usedSpawnPositions.Add((spawn.x, spawn.y)))
                {
                    report.AddError(
                        $"{prefix}: Spawn Position ({spawn.x}, {spawn.y}) " +
                        "is used by more than one enemy.");
                }
            }

            return report;
        }

        private static void ValidateTargetModes(
            SkillData skill,
            CombatDataValidationReport report)
        {
            for (var index = 0; index < skill.TargetModes.Count; index++)
            {
                var targetMode = skill.TargetModes[index];
                var prefix = $"Target Mode [{index}]";

                if (targetMode == null)
                {
                    report.AddError($"{prefix}: Target Mode cannot be null.");
                    continue;
                }

                ValidateRange(
                    targetMode.AbsoluteRange,
                    $"{prefix}: Absolute Range",
                    report);

                if (targetMode.UseRelativeRange)
                {
                    ValidateRange(
                        targetMode.RelativeRange,
                        $"{prefix}: Relative Range",
                        report);
                }

                if (targetMode.TargetType == TargetType.Caster
                    && targetMode.UseRelativeRange)
                {
                    report.AddError(
                        $"{prefix}: Caster cannot use Relative Range.");
                }
            }
        }

        private static void ValidateEffects(
            SkillData skill,
            CombatDataValidationReport report)
        {
            for (var index = 0; index < skill.Effects.Count; index++)
            {
                var effect = skill.Effects[index];
                var prefix = $"Effect [{index}]";

                if (effect == null)
                {
                    report.AddError($"{prefix}: effect cannot be null.");
                    continue;
                }

                if (effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= skill.TargetModes.Count)
                {
                    report.AddError(
                        $"{prefix}: Target Mode Index {effect.TargetModeIndex} " +
                        "does not reference an existing Target Mode.");
                }

                if (effect.EffectType == EffectType.DealDamage
                    && effect.DamageMultiplier <= 0f)
                {
                    report.AddError(
                        $"{prefix}: Damage Multiplier must be greater than zero.");
                }

                if ((effect.EffectType == EffectType.Push
                    || effect.EffectType == EffectType.Pull
                    || effect.EffectType == EffectType.Fallback)
                    && effect.DisplacementDistance <= 0)
                {
                    report.AddError(
                        $"{prefix}: Displacement Distance must be greater than zero.");
                }

                if (effect.EffectType == EffectType.Fallback
                    && effect.TargetModeIndex >= 0
                    && effect.TargetModeIndex < skill.TargetModes.Count
                    && skill.TargetModes[effect.TargetModeIndex].TargetType
                        != TargetType.Single)
                {
                    report.AddError(
                        $"{prefix}: Fallback requires Target Type set to Single.");
                }

                if (effect.EffectType == EffectType.Heal
                    || effect.EffectType == EffectType.RestoreResource
                    || effect.EffectType == EffectType.Shield)
                {
                    if (effect.BaseValue <= 0)
                    {
                        report.AddError(
                            $"{prefix}: Base Value must be greater than zero.");
                    }
                }

                if (effect.EffectType == EffectType.ModifyStat)
                {
                    if (Mathf.Approximately(effect.ModifierValue, 0f))
                    {
                        report.AddError(
                            $"{prefix}: Modifier Value cannot be zero.");
                    }

                    if (effect.ModifierOperation == StatModifierOperation.Percent
                        && effect.ModifierStacking
                            == StatModifierStacking.Multiplicative
                        && effect.ModifierValue < -1f)
                    {
                        report.AddError(
                            $"{prefix}: multiplicative Modifier Value " +
                            "cannot be lower than -1.");
                    }

                    if (effect.DurationTurns < 0)
                    {
                        report.AddError(
                            $"{prefix}: Duration Turns cannot be negative.");
                    }
                }

                if (effect.EffectType == EffectType.ModifyVar)
                {
                    if (Mathf.Approximately(effect.ModifierValue, 0f))
                    {
                        report.AddError(
                            $"{prefix}: Modifier Value cannot be zero.");
                    }

                    if (effect.ModifierStacking == StatModifierStacking.Multiplicative
                        && effect.ModifierValue < -1f)
                    {
                        report.AddError(
                            $"{prefix}: multiplicative Modifier Value cannot be lower than -1.");
                    }

                    if (effect.DurationTurns < 0)
                    {
                        report.AddError(
                            $"{prefix}: Duration Turns cannot be negative.");
                    }
                }

                if (effect.EffectType == EffectType.ApplyStatus)
                {
                    if (string.IsNullOrWhiteSpace(effect.StatusId))
                    {
                        report.AddError(
                            $"{prefix}: Status ID is required for Apply Status.");
                    }

                    if (effect.DurationTurns <= 0)
                    {
                        report.AddError(
                            $"{prefix}: Duration Turns must be greater than zero.");
                    }

                    if (effect.BaseChance < 0f || effect.BaseChance > 1f)
                    {
                        report.AddError(
                            $"{prefix}: Base Chance must be between 0 and 1.");
                    }
                }

                if (effect.Conditions == null)
                {
                    continue;
                }

                for (var conditionIndex = 0;
                    conditionIndex < effect.Conditions.Count;
                    conditionIndex++)
                {
                    var condition = effect.Conditions[conditionIndex];

                    if (condition == null)
                    {
                        report.AddError(
                            $"{prefix}, condition [{conditionIndex}]: " +
                            "condition cannot be null.");

                        continue;
                    }

                    if (!ConditionRequiresEffectReference(condition.ConditionType))
                    {
                        continue;
                    }

                    if (condition.ReferencedEffectIndex < 0
                        || condition.ReferencedEffectIndex >= index)
                    {
                        report.AddError(
                            $"{prefix}, condition [{conditionIndex}]: " +
                            $"Referenced Effect Index " +
                            $"{condition.ReferencedEffectIndex} must reference " +
                            "a previous effect.");
                    }
                }
            }
        }

        private static void ValidateRange(
            SkillRangeData range,
            string label,
            CombatDataValidationReport report)
        {
            if (range == null)
            {
                report.AddError($"{label} cannot be null.");
                return;
            }

            if (range.MinRange < 0)
            {
                report.AddError($"{label}: Min Range cannot be negative.");
            }

            if (range.MaxRange < range.MinRange)
            {
                report.AddError(
                    $"{label}: Max Range cannot be lower than Min Range.");
            }
        }

        private static bool ConditionRequiresEffectReference(
            EffectConditionType conditionType)
        {
            return conditionType
                == EffectConditionType.ReferencedEffectSucceeded
                || conditionType
                == EffectConditionType.ReferencedEffectDealtHealthDamage;
        }
    }
}
