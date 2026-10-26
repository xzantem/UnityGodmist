using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    /// <summary>Presentation-only snapshots from committed boss mechanics; no timers or gameplay state.</summary>
    public static class CombatBossVfxFeedback
    {
        public const string TrapResolutionCue = "vfx.boss.trap-resolution";
        public static bool IsLeaderTrap(SummonedCombatMapObject trap) => trap?.SourceSkillId is
            "skill.enemy.forest.bandit_leader_bear" or "skill.enemy.forest.bandit_leader_mine";

        public static string BeginTrapResolution(CombatFeedbackBus bus, BattleUnit source, SummonedCombatMapObject trap)
        {
            if (bus == null || !IsLeaderTrap(trap)) return null;
            var action = Guid.NewGuid().ToString("N");
            bus.Publish(new CombatFeedbackEvent(source, CombatFeedbackType.ProcTriggered,
                effectName: TrapResolutionCue, actionId: action));
            return action;
        }

        public static void PublishTrapArea(CombatFeedbackBus bus, CombatSession session, BattleUnit source,
            SummonedCombatMapObject trap, string action)
        {
            if (action == null) return;
            var kind = trap.SourceSkillId == "skill.enemy.forest.bandit_leader_mine"
                ? CombatTargetVfxKind.MineBurst : CombatTargetVfxKind.TrapGround;
            // Exactly the same range predicate used by the trap executor, including empty cells.
            PublishResolutionArea(bus, source, session.Grid.Positions.Where(p => session.Grid.IsWalkable(p)
                && CombatTargetingService.IsPositionWithinRange(trap.Position, p, trap.Definition.EffectRange)), kind, action);
        }

        public static bool HasRoyalCommand(BattleUnit target) => target?.Character?.PassiveEffects?.Any(e =>
            PassiveEffectLifecycleSystem.IsActive(e) && e.Sources?.Any(s => s?.SourceId?.StartsWith(
                DeadKingBossSystem.CommandId + ".", StringComparison.Ordinal) == true) == true) == true;

        public static void PublishState(CombatFeedbackBus bus, BattleUnit boss, string skillId, string actionId)
        {
            var kind = skillId switch
            {
                "skill.enemy.catacombs.skeleton_slayer_defense" when boss?.SkeletonSlayerState?.DefenseTurns > 0 => (CombatTargetVfxKind?)CombatTargetVfxKind.SlayerWard,
                "skill.enemy.catacombs.skeleton_slayer_task" when boss?.SkeletonSlayerState?.AccelerateNextTurn == true => CombatTargetVfxKind.Rally,
                "skill.enemy.catacombs.skeleton_slayer_hammer" when boss?.SkeletonSlayerState?.HammerTurns > 0 => CombatTargetVfxKind.HammerCharge,
                "skill.enemy.catacombs.dead_king_shadow" when boss?.DeadKingState?.ShadowTurns > 0 => CombatTargetVfxKind.ShadowImpact,
                _ => null
            };
            if (kind.HasValue) bus?.Publish(new CombatFeedbackEvent(boss, CombatFeedbackType.TargetEffect,
                source: boss, targetVfx: kind, actionId: actionId));
        }

        public static void PublishArrival(CombatFeedbackBus bus, BattleUnit boss, GridPosition before, string actionId)
        {
            if (boss == null || before == boss.Position || StatusEffectSystem.IsInvisible(boss)) return;
            bus?.Publish(new CombatFeedbackEvent(boss, CombatFeedbackType.TargetEffect,
                source: boss, targetVfx: CombatTargetVfxKind.SpectralLanding, actionId: actionId));
        }

        public static void PublishRune(CombatFeedbackBus bus, BattleUnit source, SummonedCombatMapObject rune, string actionId = null)
        {
            if (rune?.SourceSkillId != "skill.enemy.catacombs.dead_king_vengeance" || source == null) return;
            bus?.Publish(new CombatFeedbackEvent(source, CombatFeedbackType.TargetEffect, position: rune.Position,
                source: source, targetVfx: CombatTargetVfxKind.SpectralLanding, actionId: actionId));
        }

        public static void PublishContact(CombatFeedbackBus bus, BattleUnit boss, BattleUnit target,
            CombatDamageResult result, CombatTargetVfxKind kind, int hit, string actionId, GridPosition impactPosition)
        {
            if (!result.Hit) return;
            bus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                source: boss, position: impactPosition, targetVfx: kind, cueDelaySeconds: hit * .4f, actionId: actionId));
        }

        public static void PublishResolutionArea(CombatFeedbackBus bus, BattleUnit boss,
            IEnumerable<GridPosition> resolvedCells, CombatTargetVfxKind kind, string actionId)
        {
            if (bus == null || boss == null) return;
            var cells = new HashSet<GridPosition>(resolvedCells);
            foreach (var p in cells)
            {
                // Existing warning markers already show the full area during
                // preparation. Large blasts use an exact contour, not an opaque fill.
                if (cells.Count > 96 && cells.Contains(new(p.X - 1, p.Y, p.Level)) && cells.Contains(new(p.X + 1, p.Y, p.Level))
                    && cells.Contains(new(p.X, p.Y - 1, p.Level)) && cells.Contains(new(p.X, p.Y + 1, p.Level))) continue;
                bus.Publish(new CombatFeedbackEvent(boss, CombatFeedbackType.TargetEffect, position: p,
                    source: boss, targetVfx: kind, actionId: actionId));
            }
        }
    }
}
