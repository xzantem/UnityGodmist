using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
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

            if (skill.SkillAccuracy < 0f || float.IsNaN(skill.SkillAccuracy) || float.IsInfinity(skill.SkillAccuracy))
            {
                report.AddError("Skill Accuracy must be a finite non-negative value.");
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

            if (skill.ToggleUpkeepFlatResourceCost < 0)
            {
                report.AddError("Toggle Upkeep Flat Resource Cost cannot be negative.");
            }

            if (skill.ToggleUpkeepMaxResourcePercent < 0f
                || skill.ToggleUpkeepMaxResourcePercent > 1f)
            {
                report.AddError(
                    "Toggle Upkeep Max Resource Percent must be between 0 and 1.");
            }

            if (!skill.IsToggle
                && (skill.ToggleUpkeepFlatResourceCost > 0
                    || skill.ToggleUpkeepMaxResourcePercent > 0f))
            {
                report.AddError("Only toggle skills can define toggle upkeep.");
            }

            if (skill.KnifeCost < 0)
            {
                report.AddError("Knife Cost cannot be negative.");
            }

            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                report.AddError("Skill must define at least one effect.");
                return report;
            }

            var isObjectPlacementOnlySkill =
                IsObjectPlacementOnlySkill(skill);

            if (!isObjectPlacementOnlySkill
                && (skill.TargetModes == null || skill.TargetModes.Count == 0))
            {
                report.AddError("Skill must define at least one Target Mode.");
            }
            else if (!isObjectPlacementOnlySkill)
            {
                ValidateTargetModes(skill, report);
            }

            ValidateEffects(skill, report);
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

            if (encounter.BossEnemyIndex < 0
                || encounter.BossEnemyIndex >= encounter.Enemies.Count)
            {
                report.AddError(
                    $"Boss Enemy Index ({encounter.BossEnemyIndex}) "
                    + "must point to an enemy in the encounter.");
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
            var createObjectEffectCount = skill.Effects.Count(
                effect => effect?.EffectType == EffectType.CreateObject);
            var summonEffectCount = skill.Effects.Count(
                effect => effect?.EffectType == EffectType.Summon);

            var isObjectPlacementOnlySkill =
                IsObjectPlacementOnlySkill(skill);

            if (createObjectEffectCount > 0
                && !isObjectPlacementOnlySkill)
            {
                report.AddError(
                    "CreateObject can only be used in a skill containing CreateObject effects only in v1.");
            }

            if (createObjectEffectCount > 1)
            {
                report.AddError(
                    "A skill can define at most one CreateObject effect in v1.");
            }

            if (summonEffectCount > 1)
            {
                report.AddError(
                    "A skill can define at most one Summon effect in v1.");
            }

            if (createObjectEffectCount > 0 && summonEffectCount > 0)
            {
                report.AddError(
                    "CreateObject and Summon cannot be mixed in one skill in v1.");
            }

            var teleportEffectCount = skill.Effects.Count(
                effect => effect?.EffectType == EffectType.Teleport);

            if (teleportEffectCount > 1)
            {
                report.AddError(
                    "A skill can define at most one Teleport effect in v1.");
            }

            for (var index = 0; index < skill.Effects.Count; index++)
            {
                var effect = skill.Effects[index];
                var prefix = $"Effect [{index}]";

                if (effect == null)
                {
                    report.AddError($"{prefix}: effect cannot be null.");
                    continue;
                }

                if (skill.IsToggle
                    && !ToggleSkillRules.SupportsToggle(
                        effect.EffectType,
                        effect.UniqueBuffType))
                {
                    report.AddError(
                        $"{prefix}: {effect.EffectType} cannot be used by a toggle skill.");
                }

                if (skill.IsToggle && effect.DelayCasterTurns > 0)
                {
                    report.AddError(
                        $"{prefix}: toggle effects cannot use delayed execution.");
                }

                if (skill.IsToggle
                    && effect.Conditions != null
                    && effect.Conditions.Count > 0)
                {
                    report.AddError(
                        $"{prefix}: toggle effects cannot use conditions.");
                }

                if (effect.EffectType == EffectType.CreateObject)
                {
                    if (effect.SummonedObjectDefinition == null)
                    {
                        report.AddError(
                            $"{prefix}: Summoned Object Definition is required.");
                    }

                    ValidateRange(
                        effect.ObjectPlacementRange,
                        $"{prefix}: Object Placement Range",
                        report);

                    if (effect.MaxActiveObjects < 1)
                    {
                        report.AddError(
                            $"{prefix}: Max Active Objects must be at least 1.");
                    }

                    if (effect.Conditions != null
                        && effect.Conditions.Count > 0)
                    {
                        report.AddError(
                            $"{prefix}: CreateObject conditions are not supported in v1.");
                    }

                    continue;
                }

                if (effect.DelayCasterTurns > 0
                    && (effect.EffectType == EffectType.CreateObject
                        || effect.EffectType == EffectType.Summon
                        || effect.EffectType == EffectType.Teleport
                        || effect.EffectType == EffectType.Charge
                        || effect.EffectType == EffectType.Push
                        || effect.EffectType == EffectType.Pull
                        || effect.EffectType == EffectType.Fallback))
                {
                    report.AddError(
                        $"{prefix}: delayed {effect.EffectType} is not supported in v1.");
                }

                if (effect.EffectType == EffectType.Summon)
                {
                    if (effect.SummonedCharacterDefinition == null)
                        report.AddError($"{prefix}: Summoned Character Definition is required.");
                    if (effect.SummonedClassData == null)
                        report.AddError($"{prefix}: Summoned Class Data is required.");
                    ValidateRange(effect.SummonPlacementRange,
                        $"{prefix}: Summon Placement Range", report);
                    if (effect.SummonDurationTurns < 1)
                        report.AddError($"{prefix}: Summon Duration Turns must be at least 1.");
                    if (effect.MaxActiveSummons < 1)
                        report.AddError($"{prefix}: Max Active Summons must be at least 1.");
                    if (effect.SummonPercentMaxHealth <= 0f)
                        report.AddError($"{prefix}: Summon Percent Max Health must be greater than zero.");
                    if (effect.Conditions != null && effect.Conditions.Count > 0)
                        report.AddError($"{prefix}: Summon conditions are not supported in v1.");
                    continue;
                }

                if (skill.TargetModes == null
                    || effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= skill.TargetModes.Count)
                {
                    report.AddError(
                        $"{prefix}: Target Mode Index {effect.TargetModeIndex} " +
                        "does not reference an existing Target Mode.");

                    continue;
                }

                if (effect.EffectType == EffectType.DealDamage
                    && effect.DamageMultiplier <= 0f)
                {
                    report.AddError(
                        $"{prefix}: Damage Multiplier must be greater than zero.");
                }

                if (effect.EffectType == EffectType.Teleport)
                {
                    ValidateRange(
                        effect.TeleportDestinationRange,
                        $"{prefix}: Teleport Destination Range",
                        report);

                    var targetMode = skill.TargetModes[effect.TargetModeIndex];

                    if (targetMode != null
                        && targetMode.TargetType != TargetType.Caster
                        && targetMode.TargetType != TargetType.Single)
                    {
                        report.AddError(
                            $"{prefix}: Teleport requires Target Type set to Caster or Single.");
                    }
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
                    if (effect.BaseValue <= 0 && !(effect.EffectType == EffectType.Shield && effect.ShieldAverageAttackMultiplier > 0))
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
                            $"{prefix}: multiplicative Modifier Value cannot be lower than -1.");
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

                    if (effect.ModifierStacking
                            == StatModifierStacking.Multiplicative
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

                if (effect.EffectType == EffectType.ModifyResistance)
                {
                    if (Mathf.Approximately(effect.ModifierValue, 0f))
                    {
                        report.AddError(
                            $"{prefix}: Modifier Value cannot be zero.");
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

                    if (string.Equals(effect.StatusId, StatusEffectIds.Counterattack,
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        ValidateRange(effect.CounterattackRange,
                            $"{prefix}: Counterattack Range", report);
                        ValidateCounterattackEffects(
                            effect.CounterattackEffects,
                            prefix,
                            report);
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
                            $"{prefix}, condition [{conditionIndex}]: condition cannot be null.");

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
                            $"{prefix}, condition [{conditionIndex}]: Referenced Effect Index " +
                            $"{condition.ReferencedEffectIndex} must reference a previous effect.");
                    }
                }
            }
        }

        private static bool IsObjectPlacementOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateCounterattackEffects(
            IReadOnlyList<SkillEffectData> effects,
            string prefix,
            CombatDataValidationReport report)
        {
            if (effects == null || effects.Count == 0)
            {
                report.AddError($"{prefix}: Counterattack requires at least one reaction effect.");
                return;
            }

            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects[index];
                var effectPrefix = $"{prefix}: Counterattack Effect [{index}]";

                if (effect == null)
                {
                    report.AddError($"{effectPrefix} cannot be null.");
                    continue;
                }

                if (!SupportsCounterattackEffect(effect.EffectType))
                {
                    report.AddError(
                        $"{effectPrefix}: {effect.EffectType} is not supported in v1.");
                }

                if (effect.DelayCasterTurns > 0)
                {
                    report.AddError(
                        $"{effectPrefix}: delayed effects are not supported in v1.");
                }

                if (effect.EffectType == EffectType.ApplyStatus
                    && string.IsNullOrWhiteSpace(effect.StatusId))
                {
                    report.AddError($"{effectPrefix}: Status ID is required.");
                }

                if (effect.EffectType == EffectType.ApplyStatus
                    && string.Equals(effect.StatusId, StatusEffectIds.Counterattack,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    report.AddError(
                        $"{effectPrefix}: nested Counterattack is not supported in v1.");
                }
            }
        }

        private static bool SupportsCounterattackEffect(EffectType effectType)
        {
            return effectType == EffectType.DealDamage
                || effectType == EffectType.ApplyStatus
                || effectType == EffectType.Heal
                || effectType == EffectType.RestoreResource
                || effectType == EffectType.Shield
                || effectType == EffectType.ModifyStat
                || effectType == EffectType.ModifyVar;
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
