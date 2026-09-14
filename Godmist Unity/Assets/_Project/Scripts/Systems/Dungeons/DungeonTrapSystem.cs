using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public readonly struct DungeonTrapOccupant
    {
        public readonly BattleUnit Unit;
        public readonly CombatSession Session;
        public readonly Vector2Int Cell;
        public readonly bool IsOnGroundSurface;
        public DungeonTrapOccupant(BattleUnit unit, CombatSession session, Vector2Int cell, bool isOnGroundSurface = true)
        { Unit = unit; Session = session; Cell = cell; IsOnGroundSurface = isOnGroundSurface; }
    }

    // Shared floor state is authoritative even when occupants belong to different combat sessions.
    public sealed class DungeonTrapSystem
    {
        private readonly DungeonFloorInstance floor;
        private readonly int level;
        private readonly Func<CombatSession, CombatFeedbackBus> feedback;
        private readonly ExplorationStatusEffectSystem explorationEffects = new();
        private readonly CombatSession environment = new();
        public const float ExplorationTickSeconds = 6f;
        public DungeonTrapSystem(DungeonFloorInstance floor, int level,
            Func<CombatSession, CombatFeedbackBus> feedback = null)
        { this.floor = floor; this.level = level; this.feedback = feedback; }

        public int Scale(SpikeTrapDungeonObjectDefinition definition, int baseValue) => Mathf.CeilToInt(
            baseValue * (1f + definition.DamageGrowthPerLevel * new Godmist.Systems.Characters.CharacterLevelGrowthSystem().GetCumulativeGrowthMultiplier(Mathf.Clamp(level, 1, 50))));

        public bool EnterCell(Vector2Int cell, IReadOnlyList<DungeonTrapOccupant> occupants)
        {
            if (!occupants.Any(o => o.IsOnGroundSurface && o.Cell == cell && o.Unit?.Character?.LifeState == CharacterLifeState.Alive)) return false;
            var traps = floor.Objects.OfType<SpikeTrapDungeonObjectInstance>()
                .Where(t => t.Position == cell && t.IsActive && !t.IsTriggered).ToArray();
            bool activated = false;
            foreach (var source in traps)
            {
                if (!floor.TryTriggerSpikeTrap(source.InstanceId, out var group)) continue;
                activated = true;
                foreach (var trap in group)
                    foreach (var occupant in occupants.Where(o => o.IsOnGroundSurface && o.Cell == trap.Position))
                        Apply(trap, occupant);
            }
            return activated;
        }

        private void Apply(SpikeTrapDungeonObjectInstance trap, DungeonTrapOccupant occupant)
        {
            var unit = occupant.Unit;
            if (unit?.Character?.LifeState != CharacterLifeState.Alive) return;
            var definition = (SpikeTrapDungeonObjectDefinition)trap.Definition;
            var session = occupant.Session ?? environment;
            var bus = feedback?.Invoke(occupant.Session);
            var damage = new CombatDamageSystem(feedbackBus: bus);
            if (definition.DealsImmediateDamage)
                damage.ApplySummonedObjectDamage(session, unit, Scale(definition, definition.Damage), definition.DamageType);
            if (unit.Character.LifeState != CharacterLifeState.Alive || string.IsNullOrWhiteSpace(definition.StatusId)) return;
            if (new StatusEffectSystem(damageSystem: damage, feedbackBus: bus).ApplyStatus(
                null, unit, definition.StatusId, definition.StatusChance, definition.Duration, 1, 0f, 0f,
                "trap." + trap.InstanceId, statusDamagePerTurnOverride: Scale(definition, definition.StatusDamage)))
            {
                var applied = unit.Character.PassiveEffects.Find(effect => effect.EffectId == definition.StatusId);
                if (applied != null) applied.DisplayName = definition.StatusId switch
                { StatusEffectIds.Bleed => "Krwawienie", StatusEffectIds.Poison => "Zatrucie", StatusEffectIds.Weakening => "Osłabienie", _ => definition.StatusId };
            }
        }

        public void AdvanceExploration(float seconds, IReadOnlyList<DungeonTrapOccupant> occupants) =>
            explorationEffects.Advance(seconds, occupants, feedback?.Invoke(null));

        public string RouteState(IReadOnlyList<Vector2Int> path)
        {
            var cells = new HashSet<Vector2Int>(path);
            return string.Join("|", floor.Objects.OfType<SpikeTrapDungeonObjectInstance>()
                .Where(t => !t.IsTriggered && t.IsActive && cells.Contains(t.Position))
                .Select(t => string.IsNullOrEmpty(t.ActivationGroupId) ? t.InstanceId
                    : (floor.Map.TryGetRoomId(t.Position, out var room) ? room : "") + ":" + t.ActivationGroupId)
                .Distinct().OrderBy(id => id));
        }

        // Conservative prediction: includes all successful DoT ticks and counts a linked group once.
        public float EstimateRisk(BattleUnit unit, IReadOnlyList<Vector2Int> path)
        {
            if (unit?.Character == null) return float.PositiveInfinity;
            if (unit.Character.DebugIsInvulnerable) return 0f;
            var consumed = new HashSet<string>();
            float damage = 0f, statusCost = 0f;
            foreach (var cell in path)
            {
                foreach (var trap in floor.Objects.OfType<SpikeTrapDungeonObjectInstance>())
                {
                    if (!trap.IsActive || trap.IsTriggered || trap.Position != cell || consumed.Contains(trap.InstanceId)) continue;
                    floor.Map.TryGetRoomId(trap.Position, out var room);
                    foreach(var linked in floor.Objects.OfType<SpikeTrapDungeonObjectInstance>())
                        if (linked == trap || (!string.IsNullOrEmpty(trap.ActivationGroupId)
                            && linked.ActivationGroupId == trap.ActivationGroupId
                            && floor.Map.TryGetRoomId(linked.Position, out var otherRoom) && room == otherRoom)) consumed.Add(linked.InstanceId);
                    var def = (SpikeTrapDungeonObjectDefinition)trap.Definition;
                    if (def.DealsImmediateDamage) damage += CombatDamageSystem.ApplyDefense(unit, Scale(def, def.Damage), def.DamageType);
                    var resistance = def.StatusId == StatusEffectIds.Bleed ? unit.Character.Resistances?.BleedResistance ?? 0f
                        : def.StatusId == StatusEffectIds.Poison ? unit.Character.Resistances?.PoisonResistance ?? 0f
                        : unit.Character.Resistances?.WeakeningResistance ?? 0f;
                    if (StatusEffectSystem.CalculateEffectChance(def.StatusChance, resistance) <= 0f) continue;
                    if (StatusEffectSystem.IsDamageOverTimeStatus(def.StatusId))
                        damage += DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(unit, def.StatusId, Scale(def, def.StatusDamage)) * def.Duration;
                    else if (!string.IsNullOrEmpty(def.StatusId)) statusCost += .01f;
                }
            }
            return damage >= unit.Character.CurrentHealth + (unit.Character.ShieldPool?.TotalShield ?? 0) ? float.PositiveInfinity : damage + statusCost;
        }
    }
}
