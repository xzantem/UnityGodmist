using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Effects;
using Godmist.Data.Skills;

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
                report.AddError("SkillData nie może być puste.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(skill.SkillId))
            {
                report.AddError("Skill Id jest wymagane.");
            }

            if (string.IsNullOrWhiteSpace(skill.DisplayName))
            {
                report.AddError("Display Name jest wymagane.");
            }

            if (skill.SkillAccuracy < 0f || skill.SkillAccuracy > 100f)
            {
                report.AddError("Skill Accuracy musi mieścić się w zakresie od 0 do 100.");
            }

            if (skill.ActionCost < 0 || skill.ActionCost > 100)
            {
                report.AddError(
                    "Action Cost musi być procentem Max AP w zakresie od 0 do 100.");
            }

            if (skill.ResourceCost < 0)
            {
                report.AddError("Resource Cost nie może być ujemne.");
            }

            if (skill.TargetModes == null || skill.TargetModes.Count == 0)
            {
                report.AddError("Skill musi posiadać przynajmniej jeden Target Mode.");
            }
            else
            {
                ValidateTargetModes(skill, report);
            }

            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                report.AddError("Skill musi posiadać przynajmniej jeden efekt.");
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
                report.AddError("EncounterDefinition nie może być puste.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(encounter.Id))
            {
                report.AddError("Encounter Id jest wymagane.");
            }

            if (string.IsNullOrWhiteSpace(encounter.DisplayName))
            {
                report.AddError("Display Name jest wymagane.");
            }

            if (encounter.GridWidth <= 0 || encounter.GridHeight <= 0)
            {
                report.AddError("Wymiary gridu encountera muszą być większe od zera.");
            }

            if (encounter.Enemies == null || encounter.Enemies.Count == 0)
            {
                report.AddError(
                    "Encounter musi posiadać przynajmniej jednego przeciwnika.");
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
                    report.AddError($"{prefix}: wpis przeciwnika nie może być pusty.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(enemy.InstanceId))
                {
                    report.AddError($"{prefix}: Instance Id jest wymagane.");
                }
                else if (!usedInstanceIds.Add(enemy.InstanceId))
                {
                    report.AddError(
                        $"{prefix}: Instance Id '{enemy.InstanceId}' jest powtórzone.");
                }

                if (enemy.CharacterDefinition == null)
                {
                    report.AddError(
                        $"{prefix}: Character Definition jest wymagane.");
                }

                if (enemy.ClassData == null)
                {
                    report.AddError($"{prefix}: Class Data jest wymagane.");
                }

                var spawn = enemy.SpawnPosition;

                if (spawn.x < 0
                    || spawn.y < 0
                    || spawn.x >= encounter.GridWidth
                    || spawn.y >= encounter.GridHeight)
                {
                    report.AddError(
                        $"{prefix}: Spawn Position ({spawn.x}, {spawn.y}) " +
                        "znajduje się poza gridem encountera.");
                }

                if (!usedSpawnPositions.Add((spawn.x, spawn.y)))
                {
                    report.AddError(
                        $"{prefix}: Spawn Position ({spawn.x}, {spawn.y}) " +
                        "jest używana przez więcej niż jednego przeciwnika.");
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
                    report.AddError($"{prefix}: Target Mode nie może być pusty.");
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
                        $"{prefix}: Caster nie może używać Relative Range.");
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
                    report.AddError($"{prefix}: efekt nie może być pusty.");
                    continue;
                }

                if (effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= skill.TargetModes.Count)
                {
                    report.AddError(
                        $"{prefix}: Target Mode Index {effect.TargetModeIndex} " +
                        "nie wskazuje istniejącego Target Mode.");
                }

                if (effect.EffectType == EffectType.DealDamage
                    && effect.DamageMultiplier <= 0f)
                {
                    report.AddError(
                        $"{prefix}: Damage Multiplier musi być większy od zera.");
                }

                if (effect.EffectType == EffectType.Push
                    && effect.PushDistance <= 0)
                {
                    report.AddError(
                        $"{prefix}: Push Distance musi być większy od zera.");
                }

                if (effect.EffectType == EffectType.Heal
                    || effect.EffectType == EffectType.RestoreResource
                    || effect.EffectType == EffectType.Shield)
                {
                    if (effect.BaseValue <= 0)
                    {
                        report.AddError(
                            $"{prefix}: Base Value musi być większe od zera.");
                    }
                }

                if (effect.EffectType == EffectType.ApplyStatus)
                {
                    if (string.IsNullOrWhiteSpace(effect.StatusId))
                    {
                        report.AddError(
                            $"{prefix}: Status Id jest wymagane dla Apply Status.");
                    }

                    if (effect.DurationTurns <= 0)
                    {
                        report.AddError(
                            $"{prefix}: Duration Turns musi być większe od zera.");
                    }

                    if (effect.BaseChance < 0f || effect.BaseChance > 1f)
                    {
                        report.AddError(
                            $"{prefix}: Base Chance musi mieścić się w zakresie 0-1.");
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
                            $"{prefix}, warunek [{conditionIndex}]: " +
                            "warunek nie może być pusty.");

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
                            $"{prefix}, warunek [{conditionIndex}]: " +
                            $"Referenced Effect Index " +
                            $"{condition.ReferencedEffectIndex} musi wskazywać " +
                            "wcześniejszy efekt.");
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
                report.AddError($"{label} nie może być puste.");
                return;
            }

            if (range.MinRange < 0)
            {
                report.AddError($"{label}: Min Range nie może być ujemne.");
            }

            if (range.MaxRange < range.MinRange)
            {
                report.AddError(
                    $"{label}: Max Range nie może być mniejsze od Min Range.");
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