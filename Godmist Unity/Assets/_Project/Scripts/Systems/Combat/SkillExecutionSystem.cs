using System;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using UnityEngine;
using System.Collections.Generic;

namespace Godmist.Systems.Combat
{
    public class SkillExecutionSystem
    {
        private readonly Action<string> battleLog;
        private readonly Action<BattleUnit> removeUnitFromCombat;
        private readonly Action<CombatSession> evaluateOutcome;
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly CombatDamageSystem damageSystem;
        private readonly CombatRecoverySystem recoverySystem;
        private readonly CombatShieldSystem shieldSystem;
        private readonly CombatLineOfSightService lineOfSightService;

        public SkillExecutionSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            Action<CombatSession> evaluateOutcome = null,
            StatusEffectSystem statusEffectSystem = null, 
            CombatDamageSystem damageSystem = null,
            CombatRecoverySystem recoverySystem = null,
            CombatShieldSystem shieldSystem = null,
            CombatLineOfSightService lineOfSightService = null)
        {
            this.battleLog = battleLog;
            this.removeUnitFromCombat = removeUnitFromCombat;
            this.evaluateOutcome = evaluateOutcome;
            this.statusEffectSystem = statusEffectSystem;
            this.damageSystem = damageSystem ?? 
                new CombatDamageSystem(battleLog, evaluateOutcome, removeUnitFromCombat);
            this.recoverySystem = recoverySystem ?? new CombatRecoverySystem(battleLog);
            this.shieldSystem = shieldSystem ?? new CombatShieldSystem(battleLog);
            this.lineOfSightService = lineOfSightService ?? new CombatLineOfSightService();
        }

        public bool TryExecute(CombatSession session, BattleUnit caster, SkillData skill, BattleUnit target)
        {
            if (target == null || skill?.TargetModes == null || skill.TargetModes.Count != 1) return false;

            var targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>
                { [0] = new List<BattleUnit> { target } };

            return TryExecute(session, caster, skill, targetsByModeIndex);
        }

        public bool TryExecute(CombatSession session, BattleUnit caster, SkillData skill, 
            IReadOnlyList<BattleUnit> targets)
        {
            if (targets == null || skill?.TargetModes == null || skill.TargetModes.Count != 1) return false;

            var targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>
                { [0] = targets };

            return TryExecute(session, caster, skill, targetsByModeIndex);
        }

        public bool TryExecute(CombatSession session, BattleUnit caster, SkillData skill, 
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            if (!CanExecute(session, caster, skill, targetsByModeIndex)) return false;

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            caster.CurrentActionPoints -= actionCost;
            caster.Character.ResourcePool?.Drain(skill.ResourceCost);

            battleLog?.Invoke($"{caster.Character.Definition.DisplayName} uzywa {skill.DisplayName}.");

            var resultsByEffectIndex =
                new Dictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>();

            for (var effectIndex = 0; effectIndex < skill.Effects.Count; effectIndex++)
            {
                var effect = skill.Effects[effectIndex];
                var targets = targetsByModeIndex[effect.TargetModeIndex];

                var resultsForEffect =
                    new Dictionary<BattleUnit, EffectExecutionResult>();

                foreach (var target in targets)
                {
                    if (target?.Character == null
                        || target.Character.LifeState == CharacterLifeState.Dead)
                    {
                        continue;
                    }

                    if (!ShouldExecuteEffect(
                            effect,
                            caster,
                            target,
                            resultsByEffectIndex))
                    {
                        resultsForEffect[target] = EffectExecutionResult.None;
                        continue;
                    }

                    resultsForEffect[target] =
                        ExecuteEffect(session, caster, target, skill, effect);
                }

                resultsByEffectIndex[effectIndex] = resultsForEffect;
            }

            return true;
        }
        private EffectExecutionResult ExecuteEffect(CombatSession session, BattleUnit caster, BattleUnit target, 
            SkillData skill, SkillEffectData effect)
        {
            switch (effect.EffectType)
            {
                case EffectType.DealDamage:
                {
                    var attackValue = CombatDamageValueCalculator.CalculateDamageValue(
                        caster, effect.DamageValueSource, effect.DamageMultiplier);
                    var damageResult = damageSystem.ResolveAndApplyDamage(session, caster, target, attackValue, 
                        effect.DamageType, skill.SkillAccuracy);

                    return new EffectExecutionResult
                    {
                        Succeeded = damageResult.Hit,
                        HealthDamage = damageResult.HealthDamage,   
                        WasCritical = damageResult.IsCritical
                    };
                }
                case EffectType.Push:
                {
                    var wasPushed = ApplyPush(session, caster, target, effect);
                    return new EffectExecutionResult { Succeeded = wasPushed };
                }
                case EffectType.ApplyStatus:
                {
                    var wasApplied = statusEffectSystem != null && 
                        statusEffectSystem.ApplyStatus(caster, target, effect);
                    return new EffectExecutionResult { Succeeded = wasApplied };
                }
                case EffectType.Heal:
                {
                    var restoredHealth = recoverySystem.Heal(target, effect.BaseValue);
                    return new EffectExecutionResult 
                        { Succeeded = restoredHealth > 0, HealthRestored = restoredHealth };
                }
                case EffectType.RestoreResource:
                {
                    var restoredResource = recoverySystem.RestoreResource(target, effect.BaseValue);
                    return new EffectExecutionResult 
                        { Succeeded = restoredResource > 0, ResourceRestored = restoredResource };
                }
                case EffectType.Shield:
                {
                    var grantedShield = shieldSystem.GrantShield(target, effect.BaseValue, skill.SkillId);

                    return new EffectExecutionResult
                        { Succeeded = grantedShield > 0, ShieldGranted = grantedShield };
                }

                default:
                    Debug.LogWarning($"Nieobslugiwany typ efektu w skillu '{effect.EffectType}'.");
                    return EffectExecutionResult.None;
            }
        }

        private static bool ShouldExecuteEffect(
            SkillEffectData effect,
            BattleUnit caster,
            BattleUnit target,
            IReadOnlyDictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>
                resultsByEffectIndex)
        {
            if (effect?.Conditions == null || effect.Conditions.Count == 0)
            {
                return true;
            }

            foreach (var condition in effect.Conditions)
            {
                if (!EvaluateCondition(
                        condition,
                        caster,
                        target,
                        resultsByEffectIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateCondition(
            SkillEffectConditionData condition,
            BattleUnit caster,
            BattleUnit target,
            IReadOnlyDictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>
                resultsByEffectIndex)
        {
            if (condition == null)
            {
                return false;
            }

            switch (condition.ConditionType)
            {
                case EffectConditionType.Always:
                    return true;

                case EffectConditionType.ReferencedEffectSucceeded:
                    return TryGetReferencedEffectResult(
                        condition,
                        target,
                        resultsByEffectIndex,
                        out var successResult)
                        && successResult.Succeeded;

                case EffectConditionType.ReferencedEffectDealtHealthDamage:
                    return TryGetReferencedEffectResult(
                        condition,
                        target,
                        resultsByEffectIndex,
                        out var damageResult)
                        && damageResult.HealthDamage > 0;

                case EffectConditionType.TargetHasStatus:
                    return HasStatus(target, condition.StatusId);

                case EffectConditionType.CasterHasStatus:
                    return HasStatus(caster, condition.StatusId);

                case EffectConditionType.TargetHealthBelowPercent:
                    return IsHealthBelowPercent(
                        target,
                        condition.HealthPercentThreshold);

                case EffectConditionType.CasterHealthBelowPercent:
                    return IsHealthBelowPercent(
                        caster,
                        condition.HealthPercentThreshold);

                default:
                    return false;
            }
        }

        private static bool TryGetReferencedEffectResult(
            SkillEffectConditionData condition,
            BattleUnit target,
            IReadOnlyDictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>
                resultsByEffectIndex,
            out EffectExecutionResult result)
        {
            result = EffectExecutionResult.None;

            if (condition == null
                || resultsByEffectIndex == null
                || !resultsByEffectIndex.TryGetValue(
                    condition.ReferencedEffectIndex,
                    out var resultsForEffect)
                || resultsForEffect == null)
            {
                return false;
            }

            return resultsForEffect.TryGetValue(target, out result);
        }

        private static bool HasStatus(
            BattleUnit unit,
            string statusId)
        {
            if (unit?.Character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(statusId))
            {
                return false;
            }

            foreach (var passiveEffect
                in unit.Character.PassiveEffects)
            {
                if (passiveEffect != null
                    && string.Equals(
                        passiveEffect.EffectId,
                        statusId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHealthBelowPercent(BattleUnit unit, float threshold)
        {
            if (unit?.Character?.Stats == null) return false;

            var maxHealth = unit.Character.Stats.MaxHealth.GetValue();

            if (maxHealth <= 0f) return false;

            var currentPercent = unit.Character.CurrentHealth / maxHealth;

            return currentPercent < Mathf.Clamp01(threshold);
        }

        private static bool ConditionRequiresStatusId(
            EffectConditionType conditionType)
        {
            return conditionType == EffectConditionType.TargetHasStatus 
                || conditionType == EffectConditionType.CasterHasStatus;
        }

        private static bool ConditionRequiresEffectReference(
            EffectConditionType conditionType)
        {
            return conditionType
                == EffectConditionType.ReferencedEffectSucceeded
                || conditionType
                == EffectConditionType.ReferencedEffectDealtHealthDamage;
        }

        private struct EffectExecutionResult
        {
            public static EffectExecutionResult None => new();
            public bool Succeeded;
            public int HealthDamage;
            public bool WasCritical;
            public int HealthRestored;
            public int ResourceRestored;
            public int ShieldGranted;
        }

        private bool CanExecute(CombatSession session, BattleUnit caster, SkillData skill, 
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            if (session == null || caster?.Character == null || skill == null || targetsByModeIndex == null 
                || caster.Character.LifeState != CharacterLifeState.Alive) return false;

            if (StatusEffectSystem.IsStunned(caster))
            {
                battleLog?.Invoke($"{caster.Character.Definition.DisplayName} " +
                    "jest ogłuszony i nie może użyć umiejętności.");
                return false;
            }

            if (session.Outcome != CombatOutcomeState.Ongoing || session.ActiveUnit != caster
                || !session.Units.Contains(caster)) return false;

            if (skill.TargetModes == null || skill.TargetModes.Count == 0)
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' nie ma trybow celu.");
                return false;
            }

            for (var targetModeIndex = 0; targetModeIndex < skill.TargetModes.Count; targetModeIndex++)
            {
                var targetMode = skill.TargetModes[targetModeIndex];

                if (targetMode == null)
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' ma pusty tryb celu.");
                    return false;
                }

                if (!targetsByModeIndex.TryGetValue(targetModeIndex, out var targets) || targets == null || targets.Count == 0)
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' nie ma celow dla trybu celu {targetModeIndex}.");
                    return false;
                }

                var canHealDownedTarget = CanHealDownedTarget(skill, targetModeIndex);

                foreach (var target in targets)
                {
                    if (target?.Character == null || !session.Units.Contains(target) 
                        || target.Character.LifeState == CharacterLifeState.Dead) return false;

                    if (skill.RequiresLineOfSight && !lineOfSightService.HasLineOfSight(session, caster.Position, 
                    target.Position))
                    {
                        battleLog?.Invoke("Brak linii widzenia do celu.");
                        return false;
                    }

                    if (target.Character.LifeState == CharacterLifeState.Downed && !canHealDownedTarget) return false;
                }
            }

            foreach (var targetModeIndex in targetsByModeIndex.Keys)
            {
                if (targetModeIndex < 0 || targetModeIndex >= skill.TargetModes.Count)
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' otrzymal nieznany indeks trybu celu {targetModeIndex}.");
                    return false;
                }
            }

            var actionCost =CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            if (caster.CurrentActionPoints < actionCost)
            {
                battleLog?.Invoke("Brak wystarczajacej liczby AP.");
                return false;
            }
            if (skill.ResourceCost > 0 && caster.Character.ResourcePool == null)
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' nie ma wymaganego resource pool.");
                return false;
            }
            if (skill.ResourceCost > 0 && caster.Character.ResourcePool.ResourceType != skill.ResourceType)
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' uzywa niezgodnego rodzaju zasobu.");
                return false;
            }
            if (caster.Character.ResourcePool != null && caster.Character.ResourcePool.CurrentValue < skill.ResourceCost)
            {
                battleLog?.Invoke("Brak wystarczajacego zasobu.");
                return false;
            }
            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' nie ma zadnych efektow.");
                return false;
            }

            for (var effectIndex = 0; effectIndex < skill.Effects.Count; effectIndex++)
            {
                var effect = skill.Effects[effectIndex];
                if (effect == null)
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' zawiera pusty efekt.");
                    return false;
                }

                if (effect.TargetModeIndex < 0 || effect.TargetModeIndex >= skill.TargetModes.Count)
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' ma efekt wskazujacy nieznany indeks trybu celu {effect.TargetModeIndex}.");
                    return false;
                }

                if (effect.EffectType == EffectType.ApplyStatus && string.IsNullOrWhiteSpace(effect.StatusId))
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' nie ma statusId.");
                    return false;
                }

                if (effect.Conditions == null) continue;

                foreach (var condition in effect.Conditions)
                {
                    if (condition == null)
                    {
                        Debug.LogWarning($"Skill '{skill.SkillId}' zawiera pusty warunek efektu.");
                        return false;
                    }
                    if (ConditionRequiresStatusId(condition.ConditionType) && string.IsNullOrWhiteSpace(condition.StatusId))
                    {
                        Debug.LogWarning($"Skill '{skill.SkillId}' ma warunek statusu bez statusId.");
                        return false;
                    }
                    if (ConditionRequiresEffectReference(condition.ConditionType)
                        && (condition.ReferencedEffectIndex < 0
                            || condition.ReferencedEffectIndex >= effectIndex))
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' ma warunek efektu " +
                            $"wskazujący niepoprawny indeks " +
                            $"{condition.ReferencedEffectIndex}. " +
                            $"Warunek efektu {effectIndex} może wskazywać wyłącznie " +
                            "wcześniejszy efekt.");

                        return false;
                    }
                }
            }

            return true;
        }
        private static bool CanHealDownedTarget(SkillData skill, int targetModeIndex)
        {
            if (skill?.Effects == null || targetModeIndex < 0) return false;

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType == EffectType.Heal
                    && effect.TargetModeIndex == targetModeIndex) return true;
            }
            return false;
        }
        private bool ApplyPush(
            CombatSession session,
            BattleUnit caster,
            BattleUnit target,
            SkillEffectData effect)
        {
            var directionX = Mathf.Clamp(
                target.Position.X - caster.Position.X,
                -1,
                1);
            var directionY = Mathf.Clamp(
                target.Position.Y - caster.Position.Y,
                -1,
                1);
            
            if (directionX == 0 && directionY == 0)
            {
                Debug.LogWarning("Nie można określić kierunku odepchnięcia");
                return false;
            }
            var movedTiles = 0;
            var pushDistance = Mathf.Max(1, effect.PushDistance);

            for (var i = 0; i < pushDistance; i++)
            {
                if (!session.Grid.TryMove(
                        target.Position,
                        directionX,
                        directionY,
                        out var nextPosition))                                                                                                                                                                                                  
                {
                    break;
                }
                if (session.IsPositionOccupied(nextPosition, target))
                {
                    break;
                }

                target.Position = nextPosition;
                movedTiles++;
            }

            if (movedTiles > 0)
            {
                battleLog?.Invoke(
                    $"{target.Character.Definition.DisplayName} zostaje " +
                    $"odepchnięty o {movedTiles} pole/pola.");

                return true;
            }
            else
            {
                battleLog?.Invoke(
                    $"{target.Character.Definition.DisplayName} nie może " +
                    $"zostać odecphnięty.");
            }
            return false;
        }
    }
}
