using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;

namespace Godmist.Views.Combat
{
    public sealed partial class CombatExecutionDriver
    {
        private CombatExecutionDriver() { }

        public bool IsPotentialApproachTarget(SkillData skill, int modeIndex, BattleUnit target)
        {
            if (Session?.ActiveUnit == null || skill?.TargetModes == null || modeIndex < 0 || modeIndex >= skill.TargetModes.Count) return false;
            var mode = PassiveEffectCombatExecutionSystem.ResolveMode(Session.ActiveUnit, skill, skill.TargetModes[modeIndex]);
            return mode.TargetType is TargetType.Single or TargetType.MultipleChosen or TargetType.Chain
                && CombatTargetingService.IsEligibleTarget(Session.ActiveUnit, target, mode.TargetingType,
                    CanTargetModeIncludeDownedTargets(skill, modeIndex), includesCaster: mode.IncludesCaster);
        }

        public bool TryPlanSkillApproach(SkillData skill, int modeIndex, GridPosition target,
            IReadOnlyDictionary<int, GridPosition?> previous, IReadOnlyDictionary<int, int> rotations,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosen, bool exploration,
            Func<GridPosition, bool> visible, out List<GridPosition> path, out int cost,
            Func<GridPosition, bool> canReachStandingPosition = null)
        {
            path = null; cost = 0;
            var actor = Session?.ActiveUnit;
            if (actor?.IsPlayerControlled != true || skill?.TargetModes == null || skill.Effects == null || modeIndex < 0
                || modeIndex >= skill.TargetModes.Count || Session.Outcome != CombatOutcomeState.Ongoing
                || Session.MembershipChangePending || IsActionInProgress || visible?.Invoke(target) == false) return false;
            var requestedMode = PassiveEffectCombatExecutionSystem.ResolveMode(actor, skill, skill.TargetModes[modeIndex]);
            if (requestedMode.TargetType is TargetType.Single or TargetType.MultipleChosen or TargetType.Chain)
            {
                var unitAtTarget = Session.Units.FirstOrDefault(u => u.Position == target);
                var unitEligible = CombatTargetingService.IsEligibleTarget(actor, unitAtTarget, requestedMode.TargetingType,
                    CanTargetModeIncludeDownedTargets(skill, modeIndex), includesCaster: requestedMode.IncludesCaster);
                var objectEligible = CombatObjectDamageSystem.SkillDamagesMode(skill, modeIndex)
                    && CombatObjectDamageSystem.CanDamage(Session.GetMapObjectAt(target));
                if (!unitEligible && !objectEligible && ScoutCombatSystem.RecoverableKnife(Session,actor,skill,target) == null) return false;
            }
            var picks = previous?.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<int, GridPosition?>();
            picks[modeIndex] = target;
            if (requestedMode.TargetType == TargetType.MultipleChosen)
            {
                var prospective = chosen?.ToDictionary(p => p.Key, p => p.Value)
                    ?? new Dictionary<int, IReadOnlyList<GridPosition>>();
                var selected = prospective.TryGetValue(modeIndex, out var existing) ? existing.ToList() : new List<GridPosition>();
                if (!selected.Contains(target)) selected.Add(target);
                prospective[modeIndex] = selected;
                chosen = prospective;
                picks[modeIndex] = selected[0];
            }
            var query = new CombatExecutionDriver();
            query.InitializeTargetingPreview(Session);
            bool CanCast(GridPosition origin, int movementCost)
            {
                query.Session = Session.ProjectActionQuery(origin,
                    exploration ? actor.CurrentActionPoints : actor.CurrentActionPoints - movementCost);
                var projected = query.Session.ActiveUnit;
                // Validate every already selected point from the same proposed origin.
                // Never temporarily teleport a live actor for a preview.
                foreach (var pick in picks)
                {
                    if (!pick.Value.HasValue) continue;
                    var mode = PassiveEffectCombatExecutionSystem.ResolveMode(projected, skill, skill.TargetModes[pick.Key]);
                    if (!CombatTargetingService.IsPositionWithinRange(origin, pick.Value.Value, mode.AbsoluteRange)
                        || !query.GetSelectableSkillPositions(skill, mode, pick.Key).Contains(pick.Value.Value)) return false;
                    if (chosen != null && chosen.TryGetValue(pick.Key, out var selected))
                        foreach (var point in selected)
                            if (!query.GetSelectableSkillPositions(skill, mode, pick.Key).Contains(point)) return false;
                }
                var groups = new SortedDictionary<int, IReadOnlyList<BattleUnit>>();
                foreach (var pick in picks)
                {
                    var mode = PassiveEffectCombatExecutionSystem.ResolveMode(projected, skill, skill.TargetModes[pick.Key]);
                    var point = chosen != null && chosen.TryGetValue(pick.Key, out var selected) && selected.Count > 0
                        ? selected[0] : pick.Value;
                    groups[pick.Key] = query.GetSkillTargets(skill, mode, pick.Key, point,
                        rotations != null && rotations.TryGetValue(pick.Key, out var rotation) ? rotation : 0);
                }
                if (!query.skillExecutionSystem.CanAfford(query.Session, projected, skill, groups, picks)) return false;
                var complete = skill.TargetModes.Select((m,i) => m.TargetType == TargetType.Caster || picks.ContainsKey(i)).All(v => v);
                if (!complete) return origin == actor.Position || canReachStandingPosition?.Invoke(origin) != false;
                var teleport = skill.Effects.FirstOrDefault(e => e?.EffectType == EffectType.Teleport);
                var placement = skill.Effects.FirstOrDefault(e => e != null && e.EffectType is EffectType.CreateObject or EffectType.Summon);
                GridPosition?[] teleportChoices = new GridPosition?[] { null }, placementChoices = new GridPosition?[] { null };
                if (teleport != null)
                {
                    var reference = picks.TryGetValue(teleport.TargetModeIndex, out var picked) && picked.HasValue ? picked.Value : origin;
                    teleportChoices = query.GetTeleportDestinationPositions(skill, teleport, reference).Select(p => (GridPosition?)p).ToArray();
                }
                if (placement != null) placementChoices = query.GetObjectPlacementPositions(skill, placement).Select(p => (GridPosition?)p).ToArray();
                foreach (var destination in teleportChoices)
                foreach (var objectPosition in placementChoices)
                    if (query.ValidateOpeningSkill(skill, picks, destination, objectPosition, rotations, chosen, out _))
                        return origin == actor.Position || canReachStandingPosition?.Invoke(origin) != false;
                return false;
            }
            return new CombatMovementSystem().TryFindSkillApproach(Session, actor,
                exploration ? int.MaxValue : actor.CurrentActionPoints, CanCast, visible, out path, out cost);
        }
    }
}
