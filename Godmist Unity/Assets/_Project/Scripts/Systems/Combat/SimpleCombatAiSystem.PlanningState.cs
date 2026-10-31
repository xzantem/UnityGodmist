using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        private static readonly CharacterStatType[] PlanningStatTypes = (CharacterStatType[])Enum.GetValues(typeof(CharacterStatType));

        private sealed class AiTurnPlanNode
        {
            public CombatSession World;
            public HashSet<Godmist.Data.Skills.SkillData> Used = new();
            public Dictionary<(int Unit, string Status), float> StatusConfidence = new();
            public Dictionary<int, float> PendingHealing = new();
            public HashSet<GridPosition> Visited = new();
            public HashSet<int> OwnedUnits = new();
            public HashSet<Godmist.Data.Skills.SkillData> Preparations = new();
            public HashSet<int> PreparationRecipients = new();
            public float PreparationConfidence = 1f;
            public float CarryoverValue;
            public float PreparationPriority;
            public Dictionary<int, float> PreparationTurnDelays = new();
            public bool OnlyMovement = true;
            public bool PendingPreparation;
            public bool PendingCharge;
            public AiTurnPlanStep First;
            public float Score;
            public bool Terminal;
        }

        private sealed class AiTurnPlanStep
        {
            public bool WaitForNextTurn;
            public AiSkillSelection Selection;
            public List<GridPosition> MovementPath;
            public string DoorId;
            public float Score;
        }

        private static CombatSession CopyPlanningWorld(CombatSession source, CombatSession live, BattleUnit liveActor,
            ISet<int> ownedUnits)
        {
            var copy = new CombatSession
            {
                Grid = source.Grid, DungeonContext = source.DungeonContext, DungeonTheme = source.DungeonTheme,
                DungeonDefinition = source.DungeonDefinition, ActionPointsPerSpeed = source.ActionPointsPerSpeed,
                IsSeamless = source.IsSeamless, IsDungeonWide = source.IsDungeonWide,
                ExternalPositionOccupied = source.ExternalPositionOccupied,
                EnvironmentBlocksLineOfSight = source.EnvironmentBlocksLineOfSight,
                CanUseBossSkill = (_, skill) => live.CanUseBossSkill?.Invoke(liveActor, skill) != false,
                Units = new List<BattleUnit>(source.Units), MapObjects = new List<CombatMapObject>(source.MapObjects)
            };
            // Dungeon-wide sessions may contain hundreds of unrelated actors.
            // Only the actor and affected targets get detached mutable state.
            foreach (var index in ownedUnits) copy.Units[index] = CopyPlanningUnit(source.Units[index]);
            copy.ActiveUnit = copy.Units[source.Units.IndexOf(source.ActiveUnit)];
            foreach (var index in ownedUnits) if (copy.Units[index] != null) copy.Units[index].WarriorState.Session = copy;
            copy.OpenExitEdges.UnionWith(source.OpenExitEdges);
            return copy;
        }

        private static BattleUnit CopyPlanningUnit(BattleUnit source)
        {
            if (source?.Character == null) return null;
            var character = source.Character;
            var stats = new CharacterStats();
            foreach (var type in PlanningStatTypes) stats.GetStat(type) = character.Stats.GetStat(type);
            var pool = character.ResourcePool;
            var shield = new ShieldPool();
            foreach (var layer in character.ShieldPool?.Layers ?? Array.Empty<ShieldLayer>())
                shield.AddLayer(layer.CurrentValue, layer.SourceId, layer.RemainingTurns);
            var copy = new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = character.CharacterId, Definition = character.Definition, ClassData = character.ClassData,
                    Level = character.Level, CurrentHealth = character.CurrentHealth, LifeState = character.LifeState,
                    Stats = stats, WeaponAccuracy = character.WeaponAccuracy, Resistances = character.Resistances,
                    BaseResistances = character.BaseResistances, ShieldPool = shield,
                    ResourcePool = pool == null ? null : new ResourcePool(pool.ResourceType, pool.CurrentValue, pool.MaxValue, pool.Regeneration)
                    {
                        FractionalGainRemainder = pool.FractionalGainRemainder, ManaOverflowPercent = pool.ManaOverflowPercent,
                        HasStartedTurn = pool.HasStartedTurn, HasGainedResourceSinceLastTurn = pool.HasGainedResourceSinceLastTurn
                    },
                    // Existing effects/equipment are only read. Projections add/remove
                    // list entries; they never edit a shared payload or run lifecycle.
                    PassiveEffects = new List<PassiveEffectRuntime>(character.PassiveEffects),
                    Skills = new List<Godmist.Data.Skills.SkillData>(character.Skills),
                    SkillTreeProgress = character.SkillTreeProgress, Equipment = character.Equipment,
                    Inventory = character.Inventory, EquipmentGenerationCarry = character.EquipmentGenerationCarry
                },
                Position = source.Position, CurrentActionPoints = source.CurrentActionPoints, MaxActionPoints = source.MaxActionPoints,
                MovementStepsThisTurn = source.MovementStepsThisTurn, AvailableKnives = source.AvailableKnives, MaxKnives = source.MaxKnives,
                IsPlayerSide = source.IsPlayerSide, IsBoss = source.IsBoss, IsStationarySummon = source.IsStationarySummon,
                IsAiControlledSummon = source.IsAiControlledSummon, RibbedTunicProtectionUsed = source.RibbedTunicProtectionUsed,
                UniqueBuffs = new List<UniqueBuffType>(source.UniqueBuffs),
                StatusTolerances = new Dictionary<string, StatusToleranceRuntime>(source.StatusTolerances),
                ResistanceModifiers = new Dictionary<ResistanceType, float>(source.ResistanceModifiers),
                ActiveToggleSkillIds = new HashSet<string>(source.ActiveToggleSkillIds),
                LastSkillResourceSpent = source.LastSkillResourceSpent
            };
            copy.ScoutState.Facing = source.ScoutState.Facing;
            copy.ScoutState.WeakPointTurns = source.ScoutState.WeakPointTurns;
            copy.ScoutState.WeakPointCritDamage = source.ScoutState.WeakPointCritDamage;
            copy.ScoutState.AnalysisTurns = source.ScoutState.AnalysisTurns;
            copy.ScoutState.AnalysisPenetration = source.ScoutState.AnalysisPenetration;
            copy.WarriorState.OrderSource = source.WarriorState.OrderSource;
            copy.WarriorState.OrderTurns = source.WarriorState.OrderTurns;
            copy.WarriorState.ProtectorSource = source.WarriorState.ProtectorSource;
            copy.WarriorState.ProtectionTurns = source.WarriorState.ProtectionTurns;
            copy.WarriorState.TraumaTurns = source.WarriorState.TraumaTurns;
            copy.WarriorState.TraumaBonus = source.WarriorState.TraumaBonus;
            copy.WarriorState.LastCutTarget = source.WarriorState.LastCutTarget;
            copy.WarriorState.ConsecutiveCuts = source.WarriorState.ConsecutiveCuts;
            copy.WarriorState.FreeMovementTurns = source.WarriorState.FreeMovementTurns;
            copy.WarriorState.FreeStepsUsed = source.WarriorState.FreeStepsUsed;
            copy.MageState.OffensiveTargetCount = source.MageState.OffensiveTargetCount;
            copy.MageState.OverflowCritChance = source.MageState.OverflowCritChance;
            copy.MageState.OverflowActionId = source.MageState.OverflowActionId;
            foreach (var pair in source.MageState.Adaptation) copy.MageState.Adaptation.Add(pair.Key, pair.Value);
            foreach (var pair in source.MageState.FireErosion) copy.MageState.FireErosion.Add(pair.Key, pair.Value);
            copy.PaladinState.HealingDiscount = source.PaladinState.HealingDiscount;
            return copy;
        }

        private static long PlanningStateStamp(CombatSession session, BattleUnit observer, int radius, bool globalAllies)
        {
            unchecked
            {
                long hash = 17;
                void Add(int value) => hash = hash * 31 + value;
                Add((int)session.Outcome); Add(session.Units.Count); Add(session.MapObjects.Count);
                Add(session.Grid?.GetHashCode() ?? 0); Add(session.Grid?.NavigationRevision ?? 0);
                Add(session.Grid?.Width ?? 0); Add(session.Grid?.Height ?? 0);
                Add(session.Grid?.DynamicBlocked?.GetHashCode() ?? 0); Add(session.Grid?.TraversalNeighbours?.GetHashCode() ?? 0);
                foreach (var unit in session.Units)
                {
                    if (unit?.Character == null) { Add(0); continue; }
                    var near = observer != null && Math.Abs(unit.Position.X - observer.Position.X)
                        + Math.Abs(unit.Position.Y - observer.Position.Y) <= radius;
                    var observedOpponent = observer != null && unit.IsPlayerSide != observer.IsPlayerSide
                        && (session.Contact == null || session.Contact.Sees(observer, unit));
                    if (!near && !observedOpponent && !(globalAllies && unit.IsPlayerSide == observer?.IsPlayerSide)) continue;
                    Add(unit.GetHashCode());
                    // A remote ally walking elsewhere must not cancel every frame
                    // of a dungeon-wide query. Global buffs only depend on its stats.
                    if (near || observedOpponent) Add(unit.Position.GetHashCode());
                    Add((int)unit.Character.LifeState);
                    Add(unit.Character.CurrentHealth); Add(unit.CurrentActionPoints); Add(unit.MaxActionPoints);
                    Add(unit.Character.ResourcePool?.CurrentValue ?? 0); Add(unit.Character.ShieldPool?.TotalShield ?? 0);
                    Add(unit.PaladinState.HealingDiscount ? 1 : 0);
                    Add(unit.AvailableKnives); Add(unit.MovementStepsThisTurn); Add(unit.ScoutState.Facing.GetHashCode());
                    foreach (var buff in unit.UniqueBuffs) Add((int)buff);
                    var resistances = unit.Character.Resistances;
                    Add(resistances?.BleedResistance.GetHashCode() ?? 0); Add(resistances?.PoisonResistance.GetHashCode() ?? 0);
                    Add(resistances?.FireResistance.GetHashCode() ?? 0); Add(resistances?.SuppressionResistance.GetHashCode() ?? 0);
                    Add(resistances?.WeakeningResistance.GetHashCode() ?? 0); Add(resistances?.MoveResistance.GetHashCode() ?? 0);
                    if (unit.StatusTolerances != null) foreach (var tolerance in unit.StatusTolerances)
                    { Add(tolerance.Key.GetHashCode()); Add(tolerance.Value?.Stacks ?? 0); Add(tolerance.Value?.RemainingTurns ?? 0); }
                    if (unit.Character.Stats != null)
                        foreach (var type in PlanningStatTypes) Add(unit.Character.Stats.GetStat(type).GetValue().GetHashCode());
                    foreach (var effect in unit.Character.PassiveEffects)
                    {
                        Add(effect?.EffectId?.GetHashCode() ?? 0); Add(effect?.RemainingDuration ?? 0); Add(effect?.Stacks ?? 0);
                        if (effect == null) continue;
                        foreach (var state in effect.StateValues)
                        {
                            if (state == null) continue;
                            Add(state.Key?.GetHashCode() ?? 0); Add((int)state.Type); Add(state.IntegerValue);
                            Add(state.NumberValue.GetHashCode()); Add(state.BooleanValue ? 1 : 0); Add(state.TextValue?.GetHashCode() ?? 0);
                        }
                        foreach (var source in effect.Sources) Add(source?.SourceCharacterId?.GetHashCode() ?? 0);
                        var variables = effect.GetPayload<VariableModifiersPayload>()?.Modifiers;
                        if (variables != null) foreach (var modifier in variables) Add(modifier?.Value.GetHashCode() ?? 0);
                        var stats = effect.GetPayload<StatModifiersPayload>()?.Modifiers;
                        if (stats != null) foreach (var modifier in stats) Add(modifier?.Value.GetHashCode() ?? 0);
                    }
                }
                foreach (var obj in session.MapObjects)
                {
                    Add(obj?.GetHashCode() ?? 0); Add(obj?.BlocksMovement == true ? 1 : 0); Add(obj?.BlocksLineOfSight == true ? 1 : 0);
                    if (obj is DoorCombatMapObject door) Add((int)door.InteractionMode);
                }
                return hash;
            }
        }
    }
}
