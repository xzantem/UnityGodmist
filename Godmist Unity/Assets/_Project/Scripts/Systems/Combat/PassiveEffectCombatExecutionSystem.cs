using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    /// <summary>
    /// Resolves contextual passive effects without flattening them into character
    /// stats. Persistent always-active definitions are still handled by the stat
    /// pipeline; this system handles definitions that depend on a combat event.
    /// </summary>
    public sealed partial class PassiveEffectCombatExecutionSystem : IDisposable
    {
        private readonly CombatSession session;
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly PassiveEffectLifecycleSystem lifecycleSystem;
        private readonly Action<string> battleLog;
        private readonly List<PendingDuplicateAction> pendingDuplicateActions = new();
        private readonly HashSet<string> queuedDuplicateKeys = new();

        public Func<PassiveEffectCombatContext, bool> DuplicateActionExecutor { get; set; }
        public event Action<BattleUnit, PassiveEffectRuntime,
            PassiveEffectOperationDefinition, PassiveEffectCombatContext> RuntimeFlagTriggered;

        public PassiveEffectCombatExecutionSystem(
            CombatSession session,
            StatusEffectSystem statusEffectSystem,
            PassiveEffectLifecycleSystem lifecycleSystem = null,
            Action<string> battleLog = null)
        {
            this.session = session;
            this.statusEffectSystem = statusEffectSystem;
            this.lifecycleSystem = lifecycleSystem ?? new PassiveEffectLifecycleSystem();
            this.battleLog = battleLog;

            if (session?.PassiveEffectEventDispatcher != null)
                session.PassiveEffectEventDispatcher.Published += HandlePublishedEvent;
        }

        public void Dispose()
        {
            if (session?.PassiveEffectEventDispatcher != null)
                session.PassiveEffectEventDispatcher.Published -= HandlePublishedEvent;
        }

        public static float ApplyDamageMultiplier(
            CombatSession session,
            PassiveEffectCombatContext context,
            float damage)
        {
            if (damage <= 0f || context?.Source == null)
                return damage;

            var multiplier = ResolveParameter(context.Source, context.Skill, "damage_multiplier", 1f);
            foreach (var match in FindMatches(
                         session,
                         PassiveEffectTriggerType.OnAttack,
                         context,
                         offensiveOwner: true))
            {
                foreach (var operation in match.Operations)
                {
                    if (operation == null || !TargetsOwner(operation.Target))
                        continue;

                    if (operation.Type == PassiveEffectOperationType.ModifyGameplayVariable
                        && operation.VariableType == GameplayVariableType.DamageDealtPercent)
                    {
                        multiplier = ApplyStacking(
                            multiplier,
                            ResolveValue(operation.Value, match.Runtime),
                            operation.ModifierStacking);
                    }
                    else if (operation.Type == PassiveEffectOperationType.ModifySkillParameter
                        && !string.Equals(operation.ReferenceId, "damage_multiplier", StringComparison.OrdinalIgnoreCase)
                        && TryGetParameter(operation, "damage_multiplier", out var value))
                    {
                        multiplier = ApplyStacking(
                            multiplier,
                            value * Math.Max(1, match.Runtime?.Stacks ?? 1),
                            operation.ModifierStacking);
                    }
                }
            }

            return Mathf.Max(0f, damage * multiplier);
        }

        public static float ApplyCriticalChance(
            CombatSession session,
            PassiveEffectCombatContext context,
            float chance)
        {
            if (context?.Source == null)
                return chance;

            var result = chance;
            foreach (var match in FindMatches(
                         session,
                         PassiveEffectTriggerType.OnAttack,
                         context,
                         offensiveOwner: true))
            {
                foreach (var operation in match.Operations)
                {
                    if (operation == null
                        || operation.Type != PassiveEffectOperationType.ModifyStat
                        || operation.StatType != CharacterStatType.CritChance
                        || !TargetsOwner(operation.Target))
                    {
                        continue;
                    }

                    var value = ResolveValue(operation.Value, match.Runtime);
                    result = operation.ModifierOperation == StatModifierOperation.Percent
                        ? ApplyStacking(result, value, operation.ModifierStacking)
                        : result + value;
                }
            }

            return Mathf.Clamp01(result);
        }

        public static int ApplyMovementCost(
            CombatSession session,
            BattleUnit unit,
            int baseCost)
        {
            if (unit == null || baseCost <= 0)
                return baseCost;

            var context = new PassiveEffectCombatContext(
                session: session,
                source: unit,
                target: unit,
                movementSteps: unit.MovementStepsThisTurn,
                movementCost: ActionPointUnits.ToPoints(baseCost));
            var result = baseCost;

            foreach (var match in FindMatches(
                         session,
                         PassiveEffectTriggerType.OnMove,
                         context,
                         offensiveOwner: true))
            {
                foreach (var operation in match.Operations)
                {
                    if (operation == null
                        || operation.Type != PassiveEffectOperationType.ModifyMovementCost
                        || !TargetsOwner(operation.Target))
                    {
                        continue;
                    }

                    var value = ResolveValue(operation.Value, match.Runtime);
                    if (operation.ModifierOperation == StatModifierOperation.Percent)
                    {
                        result = Mathf.RoundToInt(result * Mathf.Max(0f, 1f + value));
                    }
                    else
                    {
                        result += ActionPointUnits.FromPoints(value);
                    }
                }
            }

            return Mathf.Max(0, result);
        }

        public static bool TryConvertMiss(
            CombatSession session,
            PassiveEffectCombatContext context)
        {
            if (context?.Source == null)
                return false;

            foreach (var match in FindMatches(
                         session,
                         PassiveEffectTriggerType.OnMiss,
                         context,
                         offensiveOwner: true))
            {
                foreach (var operation in match.Operations)
                {
                    if (operation == null
                        || operation.Type != PassiveEffectOperationType.ChangeHitResolution
                        || !IsMissToHit(operation))
                    {
                        continue;
                    }

                    var chance = Mathf.Clamp01(
                        ResolveValue(operation.Value, match.Runtime));
                    if (UnityEngine.Random.value <= chance)
                        return true;
                }
            }

            return false;
        }

        public static bool TryPreventDamage(
            CombatSession session,
            PassiveEffectCombatContext context,
            DamageType damageType)
        {
            if (damageType != DamageType.Magical || context?.Target == null)
                return false;

            foreach (var match in FindMatches(
                         session,
                         PassiveEffectTriggerType.OnDamageTaken,
                         context,
                         offensiveOwner: false))
            {
                foreach (var operation in match.Operations)
                {
                    if (operation == null
                        || operation.Type != PassiveEffectOperationType.PreventDamage
                        || !TargetsOwner(operation.Target))
                    {
                        continue;
                    }

                    var chance = Mathf.Clamp01(
                        ResolveValue(operation.Value, match.Runtime));
                    if (UnityEngine.Random.value <= chance)
                        return true;
                }
            }

            return false;
        }

        public static bool TryConsumeRuntimeFlagForAttack(
            CombatSession session,
            PassiveEffectCombatContext context,
            string flagId)
        {
            if (context?.Source == null || string.IsNullOrWhiteSpace(flagId))
                return false;

            foreach (var match in FindMatches(
                         session,
                         PassiveEffectTriggerType.OnAttack,
                         context,
                         offensiveOwner: true))
            {
                foreach (var operation in match.Operations)
                {
                    if (operation == null
                        || operation.Type != PassiveEffectOperationType.SetRuntimeFlag
                        || !string.Equals(operation.ReferenceId, flagId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var chance = Mathf.Clamp01(
                        ResolveValue(operation.Value, match.Runtime));
                    if (UnityEngine.Random.value <= chance)
                        return true;
                }
            }

            return false;
        }

        public void NotifyActionResolved(PassiveEffectCombatContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ActionId))
                return;

            queuedDuplicateKeys.RemoveWhere(key => key.EndsWith("|" + context.ActionId, StringComparison.Ordinal));
            for (var index = pendingDuplicateActions.Count - 1;
                index >= 0;
                index--)
            {
                var pending = pendingDuplicateActions[index];
                if (!string.Equals(
                        pending.Context.ActionId,
                        context.ActionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                pendingDuplicateActions.RemoveAt(index);
                queuedDuplicateKeys.Remove(pending.Key);
                DuplicateActionExecutor?.Invoke(
                    pending.Context.AsPassiveDuplicate());
            }
        }

        private void HandlePublishedEvent(PassiveEffectCombatEvent combatEvent)
        {
            if (combatEvent?.Context == null || session?.Units == null || combatEvent.Context.IsPassiveReaction)
                return;

            var units = new List<BattleUnit>(session.Units);
            foreach (var owner in units)
            {
                if (!IsOwnerRelevant(owner, combatEvent))
                    continue;

                foreach (var match in FindMatchesForOwner(
                             owner,
                             combatEvent.TriggerType,
                             combatEvent.Context))
                {
                    foreach (var operation in match.Operations)
                    {
                        if (operation?.Type == PassiveEffectOperationType.DuplicateAction)
                        {
                            QueueDuplicateAction(match.Runtime, operation,
                                combatEvent.Context);
                            continue;
                        }

                        // Damage prevention is resolved before damage is applied;
                        // it must not be evaluated again after OnDamageTaken.
                        if (operation?.Type == PassiveEffectOperationType.PreventDamage)
                            continue;

                        // Knife consumption is resolved before the attack starts.
                        // The OnAttack event is still available for other runtime
                        // flags, but this flag has already been rolled by the
                        // action-cost boundary.
                        if (operation?.Type == PassiveEffectOperationType.SetRuntimeFlag
                            && string.Equals(operation.ReferenceId,
                                "dagger.prevent-consume",
                                StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(combatEvent.Context.ActionId))
                        {
                            continue;
                        }

                        ExecuteOperation(owner, match.Runtime, operation,
                            combatEvent.Context);
                    }
                }
            }
        }

        private void ExecuteOperation(
            BattleUnit owner,
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (operation == null)
                return;

            switch (operation.Type)
            {
                case PassiveEffectOperationType.ModifyActionValue:
                case PassiveEffectOperationType.GrantShield:
                case PassiveEffectOperationType.GrantActionPoints:
                case PassiveEffectOperationType.Heal:
                case PassiveEffectOperationType.RestoreResource:
                case PassiveEffectOperationType.DealDamage:
                    ExecuteReaction(owner, runtime, operation, context);
                    break;
                case PassiveEffectOperationType.ApplyStatus:
                    ApplyStatus(owner, runtime, operation, context);
                    break;
                case PassiveEffectOperationType.ModifyStat:
                    ApplyTimedStatModifier(owner, runtime, operation, context);
                    break;
                case PassiveEffectOperationType.SetRuntimeFlag:
                    SetRuntimeFlag(owner, runtime, operation, context);
                    break;
            }
        }

        private void QueueDuplicateAction(
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (context?.IsPassiveDuplicate == true
                || runtime == null
                || context == null
                || string.IsNullOrWhiteSpace(context.ActionId))
            {
                return;
            }

            var key = $"{runtime.EffectId}|{context.ActionId}";
            if (!queuedDuplicateKeys.Add(key))
                return;

            var chance = Mathf.Clamp01(ResolveValue(operation.Value, runtime));
            if (UnityEngine.Random.value > chance)
            {
                return;
            }

            pendingDuplicateActions.Add(new PendingDuplicateAction(
                key,
                context));
        }

        private void SetRuntimeFlag(
            BattleUnit owner,
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(operation.ReferenceId))
                return;

            if (operation.ReferenceId == "summon.random-skeleton")
            {
                if (context == null || context.IsPassiveDuplicate || string.IsNullOrEmpty(context.ActionId)
                    || !queuedDuplicateKeys.Add(runtime.EffectId + "|" + context.ActionId)
                    || UnityEngine.Random.value >= Mathf.Clamp01(ResolveValue(operation.Value, runtime))) return;
            }
            runtime.SetStateValue(
                PassiveEffectRuntimeStateValue.Boolean(
                    operation.ReferenceId,
                    true));
            RuntimeFlagTriggered?.Invoke(owner, runtime, operation, context);
        }

        private void ApplyStatus(
            BattleUnit owner,
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (statusEffectSystem == null || string.IsNullOrWhiteSpace(operation.ReferenceId))
                return;

            var chance = Mathf.Clamp01(ResolveValue(operation.Value, runtime));
            var duration = Math.Max(1, operation.DurationTurns);
            foreach (var target in ResolveTargets(owner, operation.Target, context))
            {
                if (statusEffectSystem.ApplyStatus(
                        owner,
                        target,
                        operation.ReferenceId,
                        chance,
                        duration,
                        stacks: 1,
                        statusDamageMultiplier: 0f,
                        statusHealPercent: 0f,
                        sourceId: runtime?.EffectId ?? "passive.combat",
                        publishFeedback: true))
                {
                    battleLog?.Invoke(
                        $"Pasywka {runtime?.DisplayName ?? runtime?.DefinitionId} " +
                        $"nakłada {operation.ReferenceId}.");
                }
            }
        }

        private void ApplyTimedStatModifier(
            BattleUnit owner,
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (lifecycleSystem == null || operation.Value == null)
                return;

            var value = ResolveValue(operation.Value, runtime);
            if (Mathf.Approximately(value, 0f))
                return;

            var lifetime = operation.DurationTurns > 0
                ? PassiveEffectLifetime.Timed
                : PassiveEffectLifetime.CombatScoped;
            foreach (var target in ResolveTargets(owner, operation.Target, context))
            {
                if (target?.Character == null)
                    continue;

                var effectId = $"{runtime?.EffectId ?? "passive.combat"}.{operation.Id}";
                var temporary = new PassiveEffectRuntime(
                    effectId,
                    new List<PassiveEffectSourceRuntime>
                    {
                        new()
                        {
                            SourceType = PassiveEffectSourceType.RuntimeSystem,
                            SourceId = effectId
                        }
                    },
                    stacks: 1,
                    remainingDuration: operation.DurationTurns,
                    totalDamage: 0,
                    lifetime: lifetime,
                    tickPhase: PassiveEffectTickPhase.TurnEnd)
                {
                    DefinitionId = runtime?.DefinitionId ?? string.Empty,
                    DisplayName = runtime?.DisplayName ?? string.Empty,
                    StatModifiers = new List<StatModifier>
                    {
                        new()
                        {
                            StatType = operation.StatType,
                            Operation = operation.ModifierOperation,
                            Stacking = operation.ModifierStacking,
                            Value = value
                        }
                    }
                };

                lifecycleSystem.TryApplyEffectForExecution(
                    target.Character,
                    temporary,
                    PassiveEffectStackingMode.ReplaceDurationAndValues);
            }
        }

        private static bool IsOwnerRelevant(
            BattleUnit owner,
            PassiveEffectCombatEvent combatEvent)
        {
            if (owner == null || combatEvent?.Context == null)
                return false;

            return combatEvent.TriggerType == PassiveEffectTriggerType.OnDamageTaken
                ? ReferenceEquals(owner, combatEvent.Context.Target)
                : ReferenceEquals(owner, combatEvent.Context.Source);
        }

        private static bool IsMissToHit(PassiveEffectOperationDefinition operation)
        {
            return string.Equals(operation.ReferenceId, "miss-to-hit",
                       StringComparison.OrdinalIgnoreCase)
                || HasParameter(operation, "resolution", "miss-to-hit");
        }

        private static bool TargetsOwner(PassiveEffectTargetType target)
        {
            return target == PassiveEffectTargetType.Self
                || target == PassiveEffectTargetType.Source;
        }

        private static float ApplyStacking(
            float current,
            float value,
            StatModifierStacking stacking)
        {
            return stacking == StatModifierStacking.Multiplicative
                ? current * Mathf.Max(0f, 1f + value)
                : current + value;
        }

        private static float ResolveValue(
            PassiveEffectValueDefinition value,
            PassiveEffectRuntime runtime)
        {
            if (value == null || value.Source != PassiveEffectValueSourceType.Constant)
                return 0f;

            var rank = Math.Max(1, runtime?.Stacks ?? 1);
            var hasRankValue = value.RankValues != null
                && rank <= value.RankValues.Count;
            var resolved = hasRankValue
                ? value.RankValues[rank - 1]
                : value.NumberValue * rank;
            return resolved * value.Scale + value.Offset;
        }

        private static bool TryGetParameter(
            PassiveEffectOperationDefinition operation,
            string key,
            out float value)
        {
            value = 0f;
            foreach (var parameter in operation?.Parameters
                         ?? new List<PassiveEffectParameterDefinition>())
            {
                if (parameter != null
                    && string.Equals(parameter.Key, key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = parameter.NumberValue;
                    return true;
                }
            }

            return false;
        }

        private static bool HasParameter(
            PassiveEffectOperationDefinition operation,
            string key,
            string expected)
        {
            foreach (var parameter in operation?.Parameters
                         ?? new List<PassiveEffectParameterDefinition>())
            {
                if (parameter != null
                    && string.Equals(parameter.Key, key,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parameter.StringValue, expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<PassiveEffectMatch> FindMatches(
            CombatSession session,
            PassiveEffectTriggerType triggerType,
            PassiveEffectCombatContext context,
            bool offensiveOwner)
        {
            if (session?.Units == null || context == null)
                yield break;

            foreach (var owner in session.Units)
            {
                if (offensiveOwner
                    ? !ReferenceEquals(owner, context.Source)
                    : !IsOwnerRelevant(owner, new PassiveEffectCombatEvent(
                        triggerType, context)))
                {
                    continue;
                }

                foreach (var match in FindMatchesForOwner(owner, triggerType, context))
                    yield return match;
            }
        }

        private static IEnumerable<PassiveEffectMatch> FindMatchesForOwner(
            BattleUnit owner,
            PassiveEffectTriggerType triggerType,
            PassiveEffectCombatContext context)
        {
            foreach (var runtime in owner?.Character?.PassiveEffects
                         ?? new List<PassiveEffectRuntime>())
            {
                if (!PassiveEffectLifecycleSystem.IsActive(runtime)
                    || string.IsNullOrWhiteSpace(runtime.DefinitionId)
                    || !TryResolveDefinition(owner, runtime.DefinitionId, out var definition))
                {
                    continue;
                }

                if (definition.IsAlwaysActive && !definition.IsTriggered)
                    continue;

                var result = PassiveEffectCombatEvaluator.Evaluate(
                    definition,
                    runtime,
                    new PassiveEffectCombatEvent(triggerType, context));
                if (result.CanApply)
                    yield return new PassiveEffectMatch(runtime, result.Operations);
            }
        }

        public static bool TryResolveDefinition(
            BattleUnit owner,
            string definitionId,
            out PassiveEffectDefinition definition)
        {
            definition = null;
            if (owner?.Character == null || string.IsNullOrWhiteSpace(definitionId))
                return false;

            foreach (var candidate in EnumerateDefinitions(owner))
            {
                if (candidate != null
                    && string.Equals(candidate.Id, definitionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<PassiveEffectDefinition> EnumerateDefinitions(
            BattleUnit owner)
        {
            foreach (var tree in owner?.Character?.ClassData?.SkillTrees
                         ?? new List<SkillTreeDefinition>())
            {
                foreach (var definition in tree?.GlobalPassive?.PassivesPerPoint
                             ?? new List<PassiveEffectDefinition>())
                    yield return definition;

                foreach (var node in tree?.Nodes ?? new List<SkillTreeNodeDefinition>())
                {
                    foreach (var definition in node?.PassiveEffects
                                 ?? new List<PassiveEffectDefinition>())
                        yield return definition;
                }
            }

            foreach (var item in owner?.Character?.Equipment?.GetEquippedItems()
                         ?? Array.Empty<EquipmentItemInstance>())
            {
                foreach (var definition in (item?.Definition as UniqueEquipmentDefinition)?.GetEffects(item.ItemLevel)
                             ?? new List<PassiveEffectDefinition>())
                    yield return definition;

                // Jewelry uses persistent domain modifiers (stats, resistances and
                // gameplay variables), materialized by PassiveEffectReconciliationSystem.
                // It intentionally cannot carry arbitrary triggered combat passives.
            }
        }

        private static IEnumerable<BattleUnit> ResolveTargets(
            BattleUnit owner,
            PassiveEffectTargetType targetType,
            PassiveEffectCombatContext context)
        {
            switch (targetType)
            {
                case PassiveEffectTargetType.Self:
                case PassiveEffectTargetType.Source:
                    if (owner != null) yield return owner;
                    yield break;
                case PassiveEffectTargetType.PrimaryTarget:
                    if (context?.Target != null) yield return context.Target;
                    yield break;
                case PassiveEffectTargetType.Attacker:
                    if (context?.Source != null) yield return context.Source;
                    yield break;
                case PassiveEffectTargetType.AllTargets:
                    foreach (var unit in context?.Session?.Units
                                 ?? new List<BattleUnit>())
                    {
                        if (unit != null && unit.IsPlayerSide != owner?.IsPlayerSide)
                            yield return unit;
                    }
                    yield break;
                default:
                    if (context?.Target != null) yield return context.Target;
                    yield break;
            }
        }

        private static JewelryMetalData FindMetal(
            JewelryItemDefinition definition,
            string metalId)
        {
            foreach (var metal in definition?.Metals ?? new List<JewelryMetalData>())
            {
                if (Godmist.Systems.Items.JewelryGenerationService.GetMetalRecipeKey(metal) == metalId) return metal;
            }

            return null;
        }

        private static JewelryGemDefinition FindGem(
            JewelryItemDefinition definition,
            string gemId)
        {
            foreach (var gem in definition?.Gems ?? new List<JewelryGemDefinition>())
            {
                if (gem?.CraftingMaterial?.Id == gemId) return gem;
            }

            return null;
        }

        private readonly struct PassiveEffectMatch
        {
            public PassiveEffectRuntime Runtime { get; }
            public IReadOnlyList<PassiveEffectOperationDefinition> Operations { get; }

            public PassiveEffectMatch(
                PassiveEffectRuntime runtime,
                IReadOnlyList<PassiveEffectOperationDefinition> operations)
            {
                Runtime = runtime;
                Operations = operations;
            }
        }

        private readonly struct PendingDuplicateAction
        {
            public string Key { get; }
            public PassiveEffectCombatContext Context { get; }

            public PendingDuplicateAction(
                string key,
                PassiveEffectCombatContext context)
            {
                Key = key;
                Context = context;
            }
        }
    }
}
