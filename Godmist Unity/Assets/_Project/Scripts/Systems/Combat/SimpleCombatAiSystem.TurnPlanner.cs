using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        private const int TurnPlanDepth = 6;
        private const int TurnPlanWidth = 6;
        private readonly ResourceRuleRegistry planningResourceRules = new();

        private static bool HasBossAiOwner(BattleUnit unit) => unit?.IsBoss == true || unit?.DeadKingState != null
            || unit?.ForestEntState != null || unit?.SkeletonSlayerState != null || unit?.BanditLeaderState != null;

        private static bool SupportsTurnProjection(SkillData skill) => skill != null && !skill.IsToggle
            && skill.KnifeCost == 0 && skill.Effects.Count > 0
            && skill.Effects.Count(e => e?.EffectType == EffectType.DealDamage) <= 1
            && skill.Effects.All(e => e != null && e.DelayCasterTurns == 0 && string.IsNullOrEmpty(e.DamagePerOwnedSummonGroup)
                && !(e.EffectType == EffectType.ModifyStat && e.ModifierStatType is CharacterStatType.MaxHealth
                    or CharacterStatType.MaxResource or CharacterStatType.ResourceRegeneration)
                && (e.EffectType != EffectType.RemoveStatus || !string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId)
                    && string.Equals(e.StatusId, skill.RequiredCasterStatusId, StringComparison.OrdinalIgnoreCase)
                    && skill.TargetModes[e.TargetModeIndex].TargetType == TargetType.Caster)
                && (e.EffectType is EffectType.DealDamage or EffectType.ApplyStatus or EffectType.RemoveStatus
                    or EffectType.ModifyStat or EffectType.ModifyVar or EffectType.Heal or EffectType.HealPercent or EffectType.PaladinHeal or EffectType.PaladinRegeneration or EffectType.Shield
                    || e.EffectType == EffectType.ApplyUniqueBuff && e.UniqueBuffType == UniqueBuffType.Hawkeye
                    || e.EffectType == EffectType.ModifyActionValue && e.ActionValuePercent <= 0f
                        && IsPreparationMode(skill.TargetModes[e.TargetModeIndex])
                    || e.EffectType == EffectType.Charge))
            && skill.Effects.All(e => e.Conditions.All(c => c == null || c.ConditionType is EffectConditionType.Always
                or EffectConditionType.ReferencedEffectSucceeded or EffectConditionType.CasterHasStatus
                or EffectConditionType.TargetHasStatus or EffectConditionType.AnyOpponentHasStatus or EffectConditionType.NoOpponentHasStatus))
            && skill.Effects.Select((effect, index) => (effect, index)).All(pair => pair.effect.Conditions.All(condition =>
                condition?.ConditionType != EffectConditionType.ReferencedEffectSucceeded
                || SkillEffectIdentity.ReferenceIndex(skill, condition) >= 0 && SkillEffectIdentity.ReferenceIndex(skill, condition) < skill.Effects.Count
                    && skill.Effects[SkillEffectIdentity.ReferenceIndex(skill, condition)].EffectType == EffectType.DealDamage
                    && skill.Effects[SkillEffectIdentity.ReferenceIndex(skill, condition)].TargetModeIndex == pair.effect.TargetModeIndex));

        private static bool IsShortOffensivePreparation(SkillData skill) => skill?.Effects.Any(effect => effect != null
            && effect.DurationTurns == 1 && effect.ModifierValue > 0f
            && skill.TargetModes[effect.TargetModeIndex].TargetType == TargetType.Caster
            && (effect.EffectType == EffectType.ModifyVar && effect.ModifierVariableType == GameplayVariableType.DamageDealtPercent
                || effect.EffectType == EffectType.ModifyStat && effect.ModifierStatType is CharacterStatType.MinAttack
                    or CharacterStatType.MaxAttack or CharacterStatType.CritChance or CharacterStatType.CritDamage
                    or CharacterStatType.PhysicalDefensePenetration or CharacterStatType.MagicalDefensePenetration)) == true;

        private IEnumerable<bool> FindTurnPlanWork(CombatSession live, BattleUnit actor, SimpleCombatAiTurnState turn,
            AiSkillSelection greedy, List<AiSkillSelection> currentCandidates, AiResult<AiTurnPlanStep> result)
        {
            if (actor.IsPlayerSide || HasBossAiOwner(actor) || actor.Character.Stats == null) yield break;
            // Complex utility has not been converted to the projection's score
            // scale yet. Preserve its existing owner instead of comparing an
            // immediate utility bonus with a speculative multi-hit kill bonus.
            if (greedy != null && !SupportsTurnProjection(greedy.Skill)) yield break;
            var skills = actor.Character.GetActiveSkills().Where(SupportsTurnProjection).ToArray();
            if (skills.Length == 0) yield break;
            var actorIndex = live.Units.IndexOf(actor);
            if (actorIndex < 0 || live.ActiveUnit != actor) yield break;
            var known = live.Units.Select(unit => unit != null && (unit.IsPlayerSide == actor.IsPlayerSide
                || IsObservedLivingOpponent(live, actor, unit))).ToArray();
            var ownedUnits = new HashSet<int> { actorIndex };
            var root = new AiTurnPlanNode { World = CopyPlanningWorld(live, live, actor, ownedUnits), OwnedUnits = ownedUnits,
                Used = new HashSet<SkillData>(turn.UsedZeroActionSkills), Visited = new HashSet<GridPosition> { actor.Position } };
            var frontier = new List<AiTurnPlanNode> { root };
            var best = float.NegativeInfinity;
            // Keep unsupported abilities as a real outside option, not speculative
            // effects. Boss phase decisions stay with their existing owner.
            var outside = greedy?.Score ?? 0f;
            if (greedy != null && SupportsTurnProjection(greedy.Skill))
            {
                var baseline = ProjectSkill(root, RemapSelection(greedy, live, root.World), live, actor);
                if (baseline != null)
                {
                    foreach (var checkpoint in EvaluatePreparationWork(baseline, live, actor)) yield return checkpoint;
                    outside = Mathf.Max(0f, baseline.Score + baseline.CarryoverValue);
                }
            }
            AiTurnPlanStep bestStep = null;
            var flank = new AiResult<AiTurnPlanStep>();
            foreach (var checkpoint in FindFlankingPlanWork(root, live, actor, skills, flank)) yield return checkpoint;
            if (flank.Value != null) { bestStep = flank.Value; best = bestStep.Score; }
            for (var depth = 0; depth < TurnPlanDepth && frontier.Count > 0; depth++)
            {
                var next = new List<AiTurnPlanNode>();
                foreach (var node in frontier)
                {
                    yield return false;
                    if (node.Terminal || node.World.ActiveUnit.CurrentActionPoints <= 0) continue;
                    var options = new List<AiSkillSelection>();
                    var ignored = new AiResult<AiSkillSelection>();
                    bool Visible(BattleUnit unit) => known[node.World.Units.IndexOf(unit)];
                    foreach (var checkpoint in FindBestSkillSelectionWork(node.World, node.World.ActiveUnit, node.Used,
                                 ignored, options, skills, Visible)) yield return checkpoint;
                    foreach (var option in options)
                    {
                        yield return false;
                        if (node.Used.Contains(option.Skill)) continue;
                        var child = ProjectSkill(node, option, live, actor);
                        if (child == null) continue;
                        foreach (var checkpoint in EvaluatePreparationWork(child, live, actor)) yield return checkpoint;
                        child.First ??= new AiTurnPlanStep { Selection = RemapSelection(option, node.World, live) };
                        var value = child.Score + child.CarryoverValue;
                        if (value > best && !child.PendingCharge && (!child.PendingPreparation || child.CarryoverValue > 0f))
                        { best = value; bestStep = child.First; }
                        next.Add(child);
                    }
                    if (node.World.ActiveUnit.IsStationarySummon || StatusEffectSystem.IsImmobilized(node.World.ActiveUnit)) continue;
                    foreach (var cell in node.World.GetNavigationNeighbours(node.World.ActiveUnit.Position))
                    {
                        yield return false;
                        var child = ProjectMovement(node, cell, live, actor);
                        if (child != null)
                        {
                            foreach (var checkpoint in EvaluatePreparationWork(child, live, actor)) yield return checkpoint;
                            next.Add(child);
                        }
                    }
                }
                // Stable ordering makes equal-valued plans deterministic. The
                // horizon is bounded; the real next action always triggers a new query.
                frontier = next.OrderByDescending(node => node.Score + node.CarryoverValue + node.PreparationPriority)
                    .Take(TurnPlanWidth).ToList();
            }
            if (bestStep != null && best > outside + .01f)
            { bestStep.Score = best; result.Value = bestStep; }
            else if (greedy != null && SupportsTurnProjection(greedy.Skill) && (IsOffensivePreparation(greedy.Skill) || IsApproachCharge(greedy.Skill))
                && !IsEmergencyPreparationHeal(live, actor, greedy.Skill))
            {
                // A one-turn offensive buff cannot pay for itself by scoring its
                // own modifier; it needs a usable attack in the same plan.
                // The ordinary scorer enumerates candidates, but its flat buff
                // points may no longer decide whether preparation is useful.
                result.Value = bestStep != null && best > 0f ? bestStep : new AiTurnPlanStep { Selection = currentCandidates
                    .Where(candidate => !IsOffensivePreparation(candidate.Skill) && !IsApproachCharge(candidate.Skill))
                    .OrderByDescending(candidate => candidate.Score).FirstOrDefault() };
            }
        }

        private static AiSkillSelection RemapSelection(AiSkillSelection selection, CombatSession from, CombatSession to) =>
            new()
            {
                Skill = selection.Skill, Score = selection.Score, SummonPlacementDestination = selection.SummonPlacementDestination,
                TargetsByModeIndex = selection.TargetsByModeIndex.ToDictionary(pair => pair.Key,
                    pair => (IReadOnlyList<BattleUnit>)pair.Value.Select(unit => to.Units[from.Units.IndexOf(unit)]).ToArray())
            };

        private AiTurnPlanNode ProjectMovement(AiTurnPlanNode node, GridPosition cell, CombatSession live, BattleUnit actor)
        {
            var world = node.World;
            var unit = world.ActiveUnit;
            if (node.Visited.Contains(cell) || !world.Grid.IsWalkable(cell) || world.Grid.IsBlocked(cell) || world.IsPositionOccupied(cell, unit)
                || world.DoesObjectBlockMovement(world.GetMapObjectAt(cell))) return null;
            var door = world.FindClosedDirectDoor(unit.Position, cell);
            if (door != null)
            {
                if (!interactionSystem.CanInteractWithDoor(world, unit, door.ObjectId)) return null;
                var child = CopyPlanNode(node, live, actor);
                var price = CombatInteractionSystem.CalculateActionCost(child.World.ActiveUnit);
                child.World.ActiveUnit.CurrentActionPoints -= price;
                var projected = new DoorCombatMapObject(door.ObjectId, door.Edge.FirstCell, door.Edge.SecondCell, door.InteractionMode, true);
                child.World.MapObjects[child.World.MapObjects.FindIndex(obj => obj?.ObjectId == door.ObjectId)] = projected;
                child.World.Grid = world.Grid.CopyForPlanning();
                child.World.Grid.SetBlockedEdge(projected.Edge.FirstCell, projected.Edge.SecondCell, false);
                child.First ??= new AiTurnPlanStep { DoorId = door.ObjectId };
                child.OnlyMovement = false;
                child.Score -= ActionPointUnits.ToPoints(price) * Ai(GameDefineIds.AiActionCostPenaltyPerPoint, .35f);
                return child;
            }
            if (!world.CanMoveBetween(unit, unit.Position, cell)) return null;
            var role = unit.Character.Definition?.AiRole ?? CombatAiRole.Balanced;
            if (role is CombatAiRole.Dps or CombatAiRole.Healer && GetPreferredCombatRange(unit) > 1)
            {
                var ranged = GetAvailableRangedAttacks(unit);
                for (var index = 0; ranged.Count > 0 && index < world.Units.Count; index++)
                {
                    var target = world.Units[index];
                    if (target?.Character?.LifeState == CharacterLifeState.Alive
                        && IsObservedLivingOpponent(live, actor, live.Units[index])
                        && CanUseRangedSkillAgainst(world, target, unit.Position, ranged)
                        && GetManhattanDistance(cell, target.Position) < GetManhattanDistance(unit.Position, target.Position))
                        return null;
                }
            }
            var cost = PassiveEffectCombatExecutionSystem.ApplyMovementCost(world, unit,
                CombatActionCostCalculator.CalculateMovementCost(unit))
                + world.Grid.GetMovementSurcharge(unit.Position, cell) * ActionPointUnits.PerPoint;
            if (WarriorCombatSystem.FreeStep(unit)) cost = 0;
            if (cost <= 0 || cost > unit.CurrentActionPoints) return null;
            var moved = CopyPlanNode(node, live, actor);
            moved.World.ActiveUnit.CurrentActionPoints -= cost;
            moved.World.ActiveUnit.MovementStepsThisTurn++;
            ScoutCombatSystem.Face(moved.World.ActiveUnit, cell);
            moved.World.ActiveUnit.Position = cell;
            ProjectResourceGain(moved.World.ActiveUnit, cost, false);
            moved.Visited.Add(cell);
            moved.First ??= new AiTurnPlanStep { MovementPath = new List<GridPosition>() };
            if (moved.OnlyMovement && moved.First.MovementPath != null) moved.First.MovementPath.Add(cell);
            var risk = live.EvaluateTrapRisk?.Invoke(actor, new[] { cell }) ?? 0f;
            if(!float.IsFinite(risk))return null;
            moved.Score -= ActionPointUnits.ToPoints(cost) * Ai(GameDefineIds.AiActionCostPenaltyPerPoint, .35f)
                + Mathf.Max(0f, risk) * Ai(GameDefineIds.AiHardControlBonus, 300f);
            return moved;
        }

        private static AiTurnPlanNode CopyPlanNode(AiTurnPlanNode node, CombatSession live, BattleUnit actor) =>
            new() { World = CopyPlanningWorld(node.World, live, actor, node.OwnedUnits), OwnedUnits = new HashSet<int>(node.OwnedUnits),
                First = node.First == null ? null : new AiTurnPlanStep { Selection = node.First.Selection,
                    WaitForNextTurn = node.First.WaitForNextTurn,
                    DoorId = node.First.DoorId, MovementPath = node.First.MovementPath == null ? null : new List<GridPosition>(node.First.MovementPath) },
                Score = node.Score, OnlyMovement = node.OnlyMovement, PendingPreparation = node.PendingPreparation, PendingCharge = node.PendingCharge,
                Preparations = new HashSet<SkillData>(node.Preparations), PreparationConfidence = node.PreparationConfidence,
                PreparationRecipients = new HashSet<int>(node.PreparationRecipients),
                PreparationTurnDelays = new Dictionary<int, float>(node.PreparationTurnDelays),
                Visited = new HashSet<GridPosition>(node.Visited), Used = new HashSet<SkillData>(node.Used),
                PendingHealing = new Dictionary<int, float>(node.PendingHealing),
                StatusConfidence = new Dictionary<(int, string), float>(node.StatusConfidence) };
    }
}
