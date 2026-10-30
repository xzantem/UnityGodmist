using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        // Seed complete rear-attack routes instead of letting a narrow beam
        // discard every sideways step in favour of a weaker immediate strike.
        // The route still pays through the shared movement/door projection.
        private IEnumerable<bool> FindFlankingPlanWork(AiTurnPlanNode root, CombatSession live, BattleUnit actor,
            SkillData[] skills, AiResult<AiTurnPlanStep> result)
        {
            var attacks = skills.Where(skill => skill.Effects.Any(effect => effect?.EffectType == EffectType.DealDamage
                && effect.RearAttackDamageBonus > 0f && skill.TargetModes[effect.TargetModeIndex].TargetType == TargetType.Single
                && skill.TargetModes[effect.TargetModeIndex].TargetingType == TargetingType.Enemies)
                && !skill.Effects.Any(effect => effect.EffectType == EffectType.Charge)).ToArray();
            if (attacks.Length == 0 || StatusEffectSystem.IsImmobilized(actor)) yield break;
            var starts = new List<AiTurnPlanNode> { root };
            foreach (var preparation in skills.Where(skill => IsOffensivePreparation(skill)
                         && skill.Effects.Any(effect => effect.EffectType == EffectType.ApplyStatus
                             && string.Equals(effect.StatusId, "invisibility", StringComparison.OrdinalIgnoreCase))))
            {
                yield return false;
                if (StatusEffectSystem.IsInvisible(actor) || !CanAffordSkill(actor, preparation)
                    || live.CanUseBossSkill?.Invoke(actor, preparation) == false) continue;
                var targets = preparation.TargetModes.Select((mode, index) => (mode, index)).ToDictionary(pair => pair.index,
                    pair => (IReadOnlyList<BattleUnit>)new[] { root.World.ActiveUnit });
                if (preparation.TargetModes.Any(mode => mode.TargetType != TargetType.Caster)) continue;
                var projected = ProjectSkill(root, new AiSkillSelection { Skill = preparation, TargetsByModeIndex = targets }, live, actor);
                if (projected == null) continue;
                projected.First = new AiTurnPlanStep { Selection = new AiSkillSelection { Skill = preparation,
                    TargetsByModeIndex = targets.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<BattleUnit>)new[] { actor }) } };
                starts.Add(projected);
            }
            var best = float.NegativeInfinity;
            foreach (var start in starts)
            for (var index = 0; index < live.Units.Count; index++)
            {
                yield return false;
                if (!IsObservedLivingOpponent(live, actor, live.Units[index])
                    || GetManhattanDistance(actor.Position, live.Units[index].Position) > TurnPlanDepth) continue;
                foreach (var attack in attacks)
                {
                    var target = start.World.Units[index];
                    var probe = CopyPlanningUnit(start.World.ActiveUnit);
                    bool Ready(GridPosition cell)
                    {
                        probe.Position = cell;
                        return ScoutCombatSystem.Behind(probe, target)
                            && CanPreparedAttackFrom(start.World, probe, attack, target, cell);
                    }
                    var route = new AiResult<List<GridPosition>>();
                    foreach (var checkpoint in FindPathWork(start.World, start.World.ActiveUnit, target.Position, false, route, Ready, TurnPlanDepth))
                        yield return checkpoint;
                    if (route.Value == null || route.Value.Count > TurnPlanDepth) continue;
                    var node = start;
                    var future = false;
                    var currentScore = 0f;
                    var currentFirst = start.First;
                    var failed = false;
                    foreach (var cell in route.Value)
                    {
                        yield return false;
                        // Opening a door and entering its cell are distinct actions.
                        var child = ProjectFlankCell(node, cell, live, actor);
                        if (child == null)
                        {
                            // Only already active, non-expiring stealth may bridge
                            // one turn. Do not cast a new ambush for a speculative
                            // distant flank or assume free resource regeneration.
                            if (future || start != root || !HasFlankingTime(node.World.ActiveUnit)) { failed = true; break; }
                            currentFirst = node.First;
                            if (currentFirst == null) { failed = true; break; }
                            currentScore = node.Score;
                            node = ProjectFlankingTurn(node, live, actor);
                            future = true;
                            child = ProjectFlankCell(node, cell, live, actor);
                            if (child == null) { failed = true; break; }
                        }
                        node = child;
                    }
                    if (failed) continue;
                    if (!CanAffordSkill(node.World.ActiveUnit, attack) && !future && start == root
                        && HasFlankingTime(node.World.ActiveUnit))
                    {
                        currentFirst = node.First ?? new AiTurnPlanStep { WaitForNextTurn = true }; currentScore = node.Score;
                        node = ProjectFlankingTurn(node, live, actor);
                        future = true;
                    }
                    if (!CanAffordSkill(node.World.ActiveUnit, attack)
                        || !ScoutCombatSystem.Behind(node.World.ActiveUnit, node.World.Units[index])
                        || !CanPreparedAttackFrom(node.World, node.World.ActiveUnit, attack, node.World.Units[index], node.World.ActiveUnit.Position)) continue;
                    var selection = new AiSkillSelection { Skill = attack,
                        TargetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { node.World.Units[index] } } };
                    if (attack.TargetModes.Count != 1) continue;
                    var finished = ProjectSkill(node, selection, live, actor);
                    if (finished == null || finished.Terminal || finished.PendingPreparation) continue;
                    foreach (var checkpoint in EvaluatePreparationWork(finished, live, actor)) yield return checkpoint;
                    var score = future ? currentScore + (finished.Score - currentScore) * .5f : finished.Score + finished.CarryoverValue;
                    if (score <= best) continue;
                    var first = future ? currentFirst : node.First;
                    first ??= new AiTurnPlanStep { Selection = RemapSelection(selection, node.World, live) };
                    first.Score = best = score;
                    result.Value = first;
                }
            }
        }

        private AiTurnPlanNode ProjectFlankCell(AiTurnPlanNode node, GridPosition cell, CombatSession live, BattleUnit actor)
        {
            var next = ProjectMovement(node, cell, live, actor);
            if (next != null && next.World.ActiveUnit.Position != cell) next = ProjectMovement(next, cell, live, actor);
            return next?.World.ActiveUnit.Position == cell ? next : null;
        }

        private static AiTurnPlanNode ProjectFlankingTurn(AiTurnPlanNode node, CombatSession live, BattleUnit actor)
        {
            var next = CopyPlanNode(node, live, actor);
            var unit = next.World.ActiveUnit;
            var before = PlanningStatTypes.ToDictionary(type => type, type => CharacterStatPipelineSystem.EvaluateModifiers(
                unit.Character.Stats.GetStat(type).BaseValue, type, unit.Character.PassiveEffects));
            // Remove expiring payloads from the detached list, never tick or
            // mutate shared runtime effects and never run combat callbacks.
            unit.Character.PassiveEffects.RemoveAll(effect => effect?.Lifetime == PassiveEffectLifetime.Timed && effect.RemainingDuration <= 1);
            foreach (var type in PlanningStatTypes)
            {
                ref var stat = ref unit.Character.Stats.GetStat(type);
                if (type != CharacterStatType.Speed)
                    stat.SetEffectiveValue(Mathf.Max(0, stat.GetValue() - before[type]
                        + CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue, type, unit.Character.PassiveEffects)));
            }
            unit.Character.Stats.Speed.SetEffectiveValue(CharacterStatPipelineSystem.CalculateEffectiveSpeed(unit.Character));
            unit.Character.WeaponAccuracy = unit.Character.Stats.WeaponAccuracy.GetValue();
            unit.MaxActionPoints = PlanningFullBudget(next.World, unit);
            unit.CurrentActionPoints = unit.MaxActionPoints;
            unit.MovementStepsThisTurn = 0;
            return next;
        }

        private static bool HasFlankingTime(BattleUnit unit) => StatusEffectSystem.IsInvisible(unit)
            && unit.Character.PassiveEffects.Any(effect => effect != null
                && string.Equals(effect.EffectId, "invisibility", StringComparison.OrdinalIgnoreCase)
                && effect.RemainingDuration > 1 && PassiveEffectLifecycleSystem.IsActive(effect));
    }
}
