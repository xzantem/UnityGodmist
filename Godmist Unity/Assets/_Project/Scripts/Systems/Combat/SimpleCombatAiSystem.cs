using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using UnityEngine;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Data.Configuration;
using System.Linq;

namespace Godmist.Systems.Combat
{
    public enum SimpleCombatAiActionType
    {
        Movement,
        Skill,
        Interaction
    }

    public readonly struct SimpleCombatAiAction
    {
        public SimpleCombatAiAction(
            SimpleCombatAiActionType type,
            SkillData skill = null,
            IReadOnlyList<GridPosition> movementPath = null)
        {
            Type = type;
            Skill = skill;
            MovementPath = movementPath;
        }

        public SimpleCombatAiActionType Type { get; }
        public SkillData Skill { get; }
        public IReadOnlyList<GridPosition> MovementPath { get; }
    }

    public sealed class SimpleCombatAiTurnState
    {
        internal readonly HashSet<SkillData> UsedZeroActionSkills = new();
        internal bool MomentumRepositioned;
    }

    public partial class SimpleCombatAiSystem
    {
        private readonly Action<string> battleLog;
        private readonly Action<CombatSession> endTurn;
        private readonly Action<CombatSession> evaluateOutcome;

        private readonly SkillExecutionSystem skillExecutionSystem;
        private readonly CombatTargetingService targetingService;
        private readonly CombatMovementSystem movementSystem;
        private readonly CombatLineOfSightService lineOfSightService;
        private readonly CombatInteractionSystem interactionSystem;
        private readonly CatacombsBossSystem catacombsBossSystem;
        private static readonly Vector2Int[] MovementDirections =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
        };

        public SimpleCombatAiSystem(
            Action<string> battleLog = null,
            Action<CombatSession> endTurn = null,
            Action<CombatSession> evaluateOutcome = null,
            SkillExecutionSystem skillExecutionSystem = null,
            CombatTargetingService targetingService = null,
            CombatMovementSystem movementSystem = null,
            CombatLineOfSightService lineOfSightService = null,
            CombatInteractionSystem interactionSystem = null,
            CatacombsBossSystem catacombsBossSystem = null)
        {
            this.battleLog = battleLog;
            this.endTurn = endTurn;
            this.evaluateOutcome = evaluateOutcome;
            this.skillExecutionSystem = skillExecutionSystem;
            this.targetingService = targetingService ?? new CombatTargetingService();
            this.movementSystem = movementSystem ?? new CombatMovementSystem(battleLog,
                resourceRuleSystem: new ResourceRuleSystem(battleLog));
            this.lineOfSightService = lineOfSightService ?? new CombatLineOfSightService();
            this.interactionSystem = interactionSystem ?? new CombatInteractionSystem(battleLog);
            this.catacombsBossSystem = catacombsBossSystem;
        }

        public void ExecuteSimpleAiTurn(CombatSession session, BattleUnit aiUnit)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing || aiUnit?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive) return;

            var turnState = CreateTurnState();
            while (TryExecuteNextAction(session, aiUnit, turnState, out _))
            {
            }

            if (session.Outcome == CombatOutcomeState.Ongoing) endTurn?.Invoke(session);
        }

        public SimpleCombatAiTurnState CreateTurnState()
        {
            return new SimpleCombatAiTurnState();
        }

        public bool TryExecuteNextAction(
            CombatSession session, BattleUnit aiUnit, SimpleCombatAiTurnState turnState,
            out SimpleCombatAiAction action)
        {
            var result = new AiActionResult();
            foreach (var step in ExecuteNextActionWork(session, aiUnit, turnState, result)) { }
            action = result.Action;
            return result.Executed;
        }



        private AiSkillSelection FindBestSkillSelection(
            CombatSession session, BattleUnit aiUnit, ISet<SkillData> excludedZeroActionSkills)
        {
            var result = new AiResult<AiSkillSelection>();
            foreach (var step in FindBestSkillSelectionWork(session, aiUnit, excludedZeroActionSkills, result)) { }
            return result.Value;
        }

        private IEnumerable<bool> FindBestSkillSelectionWork(
            CombatSession session, BattleUnit aiUnit, ISet<SkillData> excludedZeroActionSkills,
            AiResult<AiSkillSelection> result, List<AiSkillSelection> candidates = null,
            IReadOnlyList<SkillData> planningSkills = null, Func<BattleUnit, bool> targetFilter = null)
        {
            if (aiUnit != null) aiUnit.WarriorState.Session = session;
            var activeSkills = planningSkills ?? aiUnit?.Character?.GetActiveSkills();
            if (activeSkills == null) yield break;
            var preparations = FindConditionalStatusPreparations(aiUnit, activeSkills);

            AiSkillSelection bestSelection = null;

            foreach (var skill in activeSkills)
            {
                yield return false;
                // Threshold charges are executed before ordinary skill selection.
                if (skill?.SkillId == CatacombsBossSystem.VengeanceSkillId) continue;
                if (skill?.SkillId == CatacombsBossSystem.GuardSkillId
                    && session.Summons.Count(item => item?.CreatorCharacterId
                        == aiUnit.Character.CharacterId
                        && item.SourceSkillId == CatacombsBossSystem.GuardSkillId) >= 3)
                    continue;
                if (session.CanUseBossSkill?.Invoke(aiUnit, skill) == false || !CanAffordSkill(aiUnit, skill)) continue;

                if (CombatActionCostCalculator.CalculateSkillCost(aiUnit, skill) == 0
                    && excludedZeroActionSkills != null
                    && excludedZeroActionSkills.Contains(skill))
                {
                    continue;
                }

                GridPosition? summonPlacementDestination = null;
                Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex;

                if (IsEntryTrapSkill(skill))
                {
                    var placement = new AiResult<GridPosition?>();
                    foreach (var step in FindTrapPlacementWork(session, aiUnit, skill, placement)) yield return step;
                    if (!placement.Value.HasValue) continue;
                    summonPlacementDestination = placement.Value;
                    targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>();
                }
                else if (IsSummonOnlySkill(skill))
                {
                    var placement = new AiResult<GridPosition?>();
                    foreach (var step in FindSummonPlacementWork(session, aiUnit, skill.Effects[0],
                                 skill.RequiresLineOfSight, placement)) yield return step;
                    if (!placement.Value.HasValue) continue;
                    summonPlacementDestination = placement.Value;
                    targetsByModeIndex = new Dictionary<
                        int,
                        IReadOnlyList<BattleUnit>>();
                }
                else
                {
                    var targets = new AiResult<Dictionary<int, IReadOnlyList<BattleUnit>>>();
                    foreach (var step in BuildTargetsWork(session, aiUnit, skill, targets, targetFilter)) yield return step;
                    if (targets.Value == null) continue;
                    targetsByModeIndex = targets.Value;
                }

                if (IsRedundantBuff(session, aiUnit, skill, targetsByModeIndex)) continue;

                if (!ShouldUseTimedPreparationSkill(session, aiUnit, skill)) continue;
                if (ShouldDeferConditionalPreparation(session, aiUnit, skill, preparations)) continue;

                var score = IsEntryTrapSkill(skill) && summonPlacementDestination.HasValue
                    ? TrapPlacementValue(session,aiUnit,skill,summonPlacementDestination.Value)
                    : IsSummonOnlySkill(skill) && skill.Effects[0].SummonStationary && summonPlacementDestination.HasValue
                    ? StationarySummonPlacementScore(session, aiUnit, skill.Effects[0], summonPlacementDestination.Value)
                    : CalculateSkillScore(session, aiUnit, skill, targetsByModeIndex);

                if (skill.SkillId == ForestEntBossSystem.SanctuarySkillId) score = 200f;
                if (skill.SkillId == DeadKingBossSystem.ShadowId) score = 2000f;
                if (skill.SkillId == SkeletonSlayerBossSystem.DefenseId) score = 150f;
                if (skill.SkillId == SkeletonSlayerBossSystem.HammerId) score = 250f;
                if (skill.SkillId == SkeletonSlayerBossSystem.TaskId) score = 100f;
                if(skill.SkillId==BanditLeaderBossSystem.OrderId)score=180f;
                score += ConditionalStatusPreparationBonus(session, aiUnit, skill, targetsByModeIndex, preparations);
                score += SetupOpportunity(session,aiUnit,skill);
                if (score <= 0f)
                {
                    if(candidates != null && SupportsTurnProjection(skill) && IsOffensivePreparation(skill))
                        candidates.Add(new AiSkillSelection { Skill=skill,TargetsByModeIndex=targetsByModeIndex,Score=0 });
                    continue;
                }

                candidates?.Add(new AiSkillSelection { Skill = skill, TargetsByModeIndex = targetsByModeIndex,
                    SummonPlacementDestination = summonPlacementDestination, Score = score });

                if (bestSelection == null || score > bestSelection.Score)
                {
                    bestSelection = new AiSkillSelection
                    {
                        Skill = skill,
                        TargetsByModeIndex = targetsByModeIndex,
                        SummonPlacementDestination = summonPlacementDestination,
                        Score = score
                    };
                }
            }
            result.Value = bestSelection;
        }

        private static bool IsRedundantBuff(
            CombatSession session, BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            if (skill?.Effects == null || skill.Effects.Count == 0) return false;
            for (var index = 0; index < skill.Effects.Count; index++)
            {
                var effect = skill.Effects[index];
                if (effect == null || !targetsByModeIndex.TryGetValue(effect.TargetModeIndex, out var targets)
                    || targets.Count == 0) return false;
                targets = targets.Where(target => SkillExecutionSystem.CanPlanEffect(session, effect, caster, target)).ToArray();
                if (targets.Count == 0) continue;
                if (effect.EffectType == EffectType.ApplyStatus)
                {
                    if (string.IsNullOrWhiteSpace(effect.StatusId)
                        || StatusEffectSystem.IsDamageOverTimeStatus(effect.StatusId)
                        || targets.Any(target => target?.Character == null))
                        return false;
                    if (targets.Any(target => !StatusEffectSystem.HasStatus(target, effect.StatusId)
                        && StatusEffectSystem.CalculateStatusApplicationChance(
                            caster, target, effect.StatusId, effect.BaseChance) > 0f))
                        return false;
                    continue;
                }
                var suffix = effect.EffectType switch
                {
                    EffectType.ModifyStat => "modify_stat",
                    EffectType.ModifyVar => "modify_var",
                    _ => null
                };
                if (suffix == null || targets.Any(target => target?.Character?.PassiveEffects == null
                    || !target.Character.PassiveEffects.Exists(active => active != null
                        && active.EffectId == $"{skill.SkillId}.{suffix}.{index}"
                        && PassiveEffectLifecycleSystem.IsActive(active)))) return false;
            }
            return true;
        }

        private IEnumerable<bool> FindSummonPlacementWork(
            CombatSession session, BattleUnit caster, SkillEffectData effect,
            bool requiresLineOfSight, AiResult<GridPosition?> result)
        {


            if (session?.Grid == null || caster == null || effect == null)
            {
                yield break;
            }

            foreach (var candidate in targetingService.GetPositionsInRange(
                session,
                caster.Position,
                effect.SummonPlacementRange).OrderByDescending(cell => effect.SummonStationary
                    ? StationarySummonPlacementScore(session, caster, effect, cell) : 0f))
            {
                yield return false;
                if (!session.Grid.IsWalkable(candidate)
                    || session.Grid.IsBlocked(candidate)
                    || session.IsPositionOccupied(candidate)
                    || session.IsPositionOccupiedByMapObject(candidate)
                    || (requiresLineOfSight && !lineOfSightService.HasLineOfSight(
                        session, caster.Position, candidate)))
                {
                    continue;
                }

                if (effect.SummonStationary && StationarySummonPlacementScore(session, caster, effect, candidate) <= 0f) continue;
                result.Value = candidate;
                yield break;
            }

            yield break;
        }

        private static float StationarySummonPlacementScore(CombatSession session, BattleUnit caster, SkillEffectData effect, GridPosition cell)
        {
            var pulse = effect.SummonedCharacterDefinition?.BasicAttackSkill;
            if (pulse?.Effects == null || pulse.TargetModes.Count == 0) return 0f;
            var mode = pulse.TargetModes[0];
            var score = 0f;
            foreach (var unit in session.Units)
            {
                if (unit?.Character?.LifeState != CharacterLifeState.Alive
                    || session.Contact != null && !session.Contact.Sees(caster, unit) && unit.IsPlayerSide != caster.IsPlayerSide
                    || !CombatTargetingService.IsPositionWithinRange(cell, unit.Position, mode.AbsoluteRange)
                    || !new CombatLineOfSightService().HasLineOfSight(session, cell, unit.Position)) continue;
                if (mode.TargetingType == TargetingType.Enemies && unit.IsPlayerSide != caster.IsPlayerSide) score += 100f;
                if (mode.TargetingType == TargetingType.Allies && unit.IsPlayerSide == caster.IsPlayerSide)
                    score += Mathf.Min(GetMissingHealth(unit), CombatRecoverySystem.CalculateSkillHealing(caster, pulse.Effects[0])) * 4f;
            }
            return score;
        }

        private static bool IsEntryTrapSkill(SkillData skill) => skill?.Effects?.Count == 1
            && skill.Effects[0]?.EffectType == EffectType.CreateObject
            && skill.Effects[0].SummonedObjectDefinition?.TriggersOnEntry == true;

        private IEnumerable<bool> FindTrapPlacementWork(CombatSession session, BattleUnit caster,
            SkillData skill, AiResult<GridPosition?> result)
        {
            var effect = skill.Effects[0];
            if (session.SummonedObjects.Count(item => item.CreatorCharacterId == caster.Character.CharacterId
                    && item.SourceSkillId == skill.SkillId) >= effect.MaxActiveObjects) yield break;
            var best=0f;
            foreach(var cell in targetingService.GetPositionsInRange(session,caster.Position,effect.ObjectPlacementRange))
            {
                yield return false;
                if(!session.Grid.IsWalkable(cell) || session.Grid.IsBlocked(cell) || session.IsPositionOccupied(cell)
                    || session.IsPositionOccupiedByMapObject(cell) || skill.RequiresLineOfSight && !lineOfSightService.HasLineOfSight(session,caster.Position,cell))continue;
                var value=TrapPlacementValue(session,caster,skill,cell)
                    -ActionPointUnits.ToPoints(CombatActionCostCalculator.CalculateSkillCost(caster,skill))*.35f
                    -PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill)*.1f;
                if(value>best){best=value;result.Value=cell;}
            }
        }

        private static bool IsSummonOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count != 1)
            {
                return false;
            }

            return skill.Effects[0]?.EffectType == EffectType.Summon;
        }

        private bool CanAffordSkill(BattleUnit unit, SkillData skill)
        {
            if (unit?.Character == null || skill == null) return false;
            if (!string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId)
                && !StatusEffectSystem.HasStatus(unit, skill.RequiredCasterStatusId)) return false;

            if (skill.Effects.Any(effect => effect != null && !string.IsNullOrWhiteSpace(effect.DamagePerOwnedSummonGroup)
                && SummonSystem.DamageScale(unit, effect) == 0)) return false;
            var actionCost = CombatActionCostCalculator.CalculateSkillCost(unit, skill);

            if (unit.CurrentActionPoints < actionCost || !WarriorCombatSystem.CanActivate(unit,skill)) return false;

            if (PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, skill) <= 0) return true;

            var resourcePool = unit.Character.ResourcePool;

            return resourcePool != null
                && resourcePool.ResourceType == skill.ResourceType
                && (resourcePool.CurrentValue >= PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, skill)
                    || WarriorCombatSystem.CanOverspend(unit,skill,PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit,skill)));
        }

        private IEnumerable<bool> BuildTargetsWork(CombatSession session, BattleUnit caster,
            SkillData skill, AiResult<Dictionary<int, IReadOnlyList<BattleUnit>>> result,
            Func<BattleUnit, bool> targetFilter = null)
        {
            var targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>();

            if (skill?.TargetModes == null || skill.TargetModes.Count == 0
                || skill.Effects == null || skill.Effects.Count == 0) yield break;

            for (var index = 0; index < skill.TargetModes.Count; index++)
            {
                yield return false;
                var mode = PassiveEffectCombatExecutionSystem.ResolveMode(caster, skill, skill.TargetModes[index]);

                if (mode == null) yield break;

                var requiredEnemyTarget = mode.TargetingType == TargetingType.Enemies
                    && StatusEffectSystem.TryGetTaunter(
                        session,
                        caster,
                        out var taunter)
                    ? taunter
                    : null;

                IReadOnlyList<BattleUnit> targets;

                if (mode.TargetType == TargetType.Single
                    || mode.TargetType == TargetType.AreaAroundTile
                    || (mode.TargetType == TargetType.AreaAroundCaster
                        && CombatTargetingService.IsDirectionalShape(mode.AbsoluteRange.Shape)))
                {
                    var selection = new AiResult<(bool Found, GridPosition Position, int Rotation)>();
                    foreach (var step in FindBestTargetPositionWork(session, caster, skill, mode,
                                 index, requiredEnemyTarget, selection, targetFilter)) yield return step;
                    if (!selection.Value.Found) yield break;
                    var selectedPosition = selection.Value.Position;
                    var selectedRotation = selection.Value.Rotation;

                    targets = targetingService.GetTargets(
                        session,
                        caster,
                        mode,
                        selectedPosition,
                        includeDownedTargets: SkillExecutionSystem.CanTargetModeIncludeDownedTargets(skill,index),
                        requiredEnemyTarget: requiredEnemyTarget,
                        rotationQuarterTurns: selectedRotation);
                }
                else
                {
                    targets = targetingService.GetTargets(
                        session,
                        caster,
                        mode,
                        null,
                        includeDownedTargets: SkillExecutionSystem.CanTargetModeIncludeDownedTargets(skill,index),
                        requiredEnemyTarget: requiredEnemyTarget);
                }

                targets = FilterTargetsByLineOfSight(session, caster, skill, targets);
                if (targetFilter != null) targets = targets.Where(targetFilter).ToArray();
                if (skill.SkillId == DeadKingBossSystem.BladeId) targets = targets.Where(t => DeadKingBossSystem.CanBladeTarget(session,caster,t)).ToList();
                if(skill.SkillId==BanditLeaderBossSystem.OrderId)targets=targets.Where(t=>BanditLeaderBossSystem.CanOrderTarget(caster,t)).ToList();

                if (targets.Count == 0) yield break;

                targetsByModeIndex[index] = targets;
            }

            result.Value = targetsByModeIndex;
        }

        private IEnumerable<bool> FindBestTargetPositionWork(
            CombatSession session, BattleUnit caster, SkillData skill, SkillTargetModeData mode,
            int targetModeIndex, BattleUnit requiredEnemyTarget,
            AiResult<(bool Found, GridPosition Position, int Rotation)> result, Func<BattleUnit, bool> targetFilter = null)
        {
            var selectedPosition = default(GridPosition);
            var selectedRotation = 0;

            // A rectangle is authored facing +X; choosing a tile alone does not
            // rotate it. Evaluate its four actual orientations instead of
            // repeatedly scoring the same +X area for every tile on the grid.
            var isRectangle = mode.TargetType == TargetType.AreaAroundCaster
                && mode.AbsoluteRange.Shape == RangeShape.Rectangle;
            IEnumerable<(GridPosition? position, int rotation)> choices;
            if (isRectangle)
            {
                var origin = caster.Position;
                choices = new[]
                {
                    (new GridPosition(origin.X + 1, origin.Y, origin.Level), 0),
                    (new GridPosition(origin.X, origin.Y + 1, origin.Level), 1),
                    (new GridPosition(origin.X - 1, origin.Y, origin.Level), 2),
                    (new GridPosition(origin.X, origin.Y - 1, origin.Level), 3)
                }.Where(choice => session.Grid.IsWithinBounds(choice.Item1))
                    .Select(choice => ((GridPosition?)choice.Item1, choice.Item2));
            }
            else
            {
                choices = targetingService.EnumerateSelectablePositionWork(session, caster,
                        mode, includeDownedTargets: SkillExecutionSystem.CanTargetModeIncludeDownedTargets(skill,targetModeIndex), requiredEnemyTarget: requiredEnemyTarget)
                    .Select(position => (position, 0));
            }

            var bestScore = float.MinValue;
            var foundPosition = false;

            foreach (var (candidate, rotation) in choices)
            {
                yield return false;
                if (!candidate.HasValue) continue;
                var position = candidate.Value;
                if (skill.RequiresLineOfSight
                    && !lineOfSightService.HasLineOfSight(
                        session, caster.Position, position)) continue;
                var targets = targetingService.GetTargets(
                    session,
                    caster,
                    mode,
                    position,
                    includeDownedTargets: SkillExecutionSystem.CanTargetModeIncludeDownedTargets(skill,targetModeIndex),
                    requiredEnemyTarget: requiredEnemyTarget,
                    rotationQuarterTurns: rotation);

                targets = FilterTargetsByLineOfSight(session, caster, skill, targets);
                if (targetFilter != null) targets = targets.Where(targetFilter).ToArray();
                if (skill.SkillId == DeadKingBossSystem.BladeId) targets = targets.Where(t => DeadKingBossSystem.CanBladeTarget(session,caster,t)).ToList();

                if (skill.Effects.Any(effect => effect?.EffectType == EffectType.Charge && effect.TargetModeIndex == targetModeIndex
                    && !targets.Any(target => preparationChargePreview.CanStartCharge(session, caster, target, effect)))) continue;

                var score = CalculateTargetGroupScore(
                    session, caster,
                    skill,
                    targetModeIndex,
                    targets);
                if(mode.TargetingType==TargetingType.Enemies && targets.Count>0)score*=targets.Average(t=>WarriorCombatSystem.TargetPriority(session,t));

                if (!foundPosition || score > bestScore)
                {
                    selectedPosition = position;
                    selectedRotation = rotation;
                    bestScore = score;
                    foundPosition = true;
                }
            }

            result.Value = (foundPosition, selectedPosition, selectedRotation);
        }

        private static float CalculateTargetGroupScore(
            CombatSession session, BattleUnit caster,
            SkillData skill,
            int targetModeIndex,
            IReadOnlyList<BattleUnit> targets)
        {
            if (skill == null || targets == null || targets.Count == 0) return float.MinValue;

            return CalculateEffectsScore(session, caster, skill, targetModeIndex, targets);
        }

        private static float CalculateSkillScore(CombatSession session, BattleUnit caster, SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            var score = 0f;

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType == EffectType.CreateObject && effect.SummonedObjectDefinition?.TriggersOnEntry == true)
                {
                    score += Ai(GameDefineIds.AiHardControlBonus, 300f) * .5f;
                    continue;
                }
                if (effect?.EffectType == EffectType.Summon)
                {
                    score += Ai(GameDefineIds.AiSummonScore, 75f);
                    continue;
                }

                if (effect == null || !targetsByModeIndex.TryGetValue(effect.TargetModeIndex, out var targets))
                    continue;

                score += CalculateEffectsScore(session, caster, skill, effect.TargetModeIndex, targets, effect);

                if (effect.DelayCasterTurns > 0)
                {
                    score += Ai(GameDefineIds.AiDelayedEffectPerTarget, 25f) * targets.Count;
                }
            }

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            var preference = score > 0f ? Mathf.Min(score*.15f,
                CalculateRoleBonus(caster,skill,targetsByModeIndex)+CalculatePersonalityBonus(caster,skill,targetsByModeIndex)) : 0f;
            return score + preference
                - ActionPointUnits.ToPoints(actionCost)*Ai(GameDefineIds.AiActionCostPenaltyPerPoint,.35f)
                - PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill)*Ai(GameDefineIds.AiResourceCostPenaltyPerPoint,.1f);
        }

        private static float CalculatePersonalityBonus(
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            var personality = caster?.Character?.Definition?.AiPersonality
                ?? CombatAiPersonality.Neutral;
            var bonus = 0f;

            foreach (var effect in skill.Effects)
            {
                if (effect == null || !targetsByModeIndex.TryGetValue(
                        effect.TargetModeIndex, out var targets))
                {
                    continue;
                }

                var count = targets.Count;
                bonus += personality switch
                {
                    CombatAiPersonality.Aggressive
                        when effect.EffectType == EffectType.DealDamage =>
                            Ai(GameDefineIds.AiAggressiveDamagePerTarget, 30f) * count,
                    CombatAiPersonality.Aggressive
                        when effect.EffectType == EffectType.ApplyStatus =>
                            Ai(GameDefineIds.AiAggressiveStatusPerTarget, 15f) * count,
                    CombatAiPersonality.Cautious
                        when effect.EffectType == EffectType.Heal
                        || effect.EffectType == EffectType.Shield =>
                            Ai(GameDefineIds.AiCautiousRecoveryPerTarget, 30f) * count,
                    _ => 0f
                };
            }

            return bonus;
        }

        private static float CalculateRoleBonus(
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            var role = caster?.Character?.Definition?.AiRole ?? CombatAiRole.Balanced;
            var bonus = 0f;

            foreach (var effect in skill.Effects)
            {
                if (effect == null || !targetsByModeIndex.TryGetValue(
                        effect.TargetModeIndex, out var targets))
                {
                    continue;
                }

                var count = targets.Count;
                bonus += role switch
                {
                    CombatAiRole.Dps when effect.EffectType == EffectType.DealDamage
                        => Ai(GameDefineIds.AiDpsDamagePerTarget, 60f) * count,
                    CombatAiRole.Dps when effect.EffectType == EffectType.ApplyStatus
                        => Ai(GameDefineIds.AiDpsStatusPerTarget, 20f) * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.Shield
                        => Ai(GameDefineIds.AiTankShieldPerTarget, 35f) * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.ApplyStatus
                        && string.Equals(effect.StatusId, StatusEffectIds.Taunt,
                            StringComparison.OrdinalIgnoreCase) =>
                            Ai(GameDefineIds.AiTankTauntPerTarget, 90f) * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.Push
                        || effect.EffectType == EffectType.Pull =>
                            Ai(GameDefineIds.AiTankPushPullPerTarget, 25f) * count,
                    CombatAiRole.Healer when effect.EffectType == EffectType.Heal
                        || effect.EffectType == EffectType.RestoreResource =>
                        Ai(GameDefineIds.AiHealerRecoveryPerTarget, 70f) * count,
                    CombatAiRole.Support when effect.EffectType == EffectType.Shield
                        || effect.EffectType == EffectType.ModifyStat
                        || effect.EffectType == EffectType.ModifyVar =>
                        Ai(GameDefineIds.AiSupportUtilityPerTarget, 55f) * count,
                    CombatAiRole.Support when effect.EffectType == EffectType.ApplyStatus
                        => Ai(GameDefineIds.AiSupportStatusPerTarget, 40f) * count,
                    _ => 0f
                };
            }

            return bonus;
        }



        private List<(SkillRangeData Range, bool RequiresLineOfSight)> GetAvailableRangedAttacks(BattleUnit unit)
        {
            var attacks = new List<(SkillRangeData, bool)>();
            foreach (var skill in unit.Character.GetActiveSkills())
            {
                if (!CanAffordSkill(unit, skill) || skill.TargetModes == null || skill.Effects == null
                    || skill.Effects.Any(effect => effect?.EffectType == EffectType.Charge)) continue;
                foreach (var effect in skill.Effects)
                {
                    if (effect?.EffectType != EffectType.DealDamage) continue;
                    if (effect.TargetModeIndex < 0 || effect.TargetModeIndex >= skill.TargetModes.Count) continue;
                    var mode = PassiveEffectCombatExecutionSystem.ResolveMode(unit, skill,
                        skill.TargetModes[effect.TargetModeIndex]);
                    if (mode?.TargetingType == TargetingType.Enemies
                        && (mode.TargetType == TargetType.Single || mode.TargetType == TargetType.AreaAroundTile)
                        && mode.AbsoluteRange?.MaxRange > 1)
                        attacks.Add((mode.AbsoluteRange, skill.RequiresLineOfSight));
                }
            }
            return attacks;
        }

        private bool CanUseRangedSkillAgainst(CombatSession session, BattleUnit target,
            GridPosition position, IReadOnlyList<(SkillRangeData Range, bool RequiresLineOfSight)> attacks)
        {
            if (GetManhattanDistance(position, target.Position) < 2) return false;
            foreach (var attack in attacks)
                if (CombatTargetingService.IsPositionWithinRange(position, target.Position, attack.Range)
                    && (!attack.RequiresLineOfSight
                        || lineOfSightService.HasLineOfSight(session, position, target.Position)))
                    return true;
            return false;
        }

        private static int GetPreferredCombatRange(BattleUnit unit)
        {
            var maxRange = 1;
            foreach (var skill in unit?.Character?.GetActiveSkills()
                         ?? Array.Empty<SkillData>())
            {
                if (skill?.TargetModes == null || skill.Effects == null) continue;
                foreach (var effect in skill.Effects)
                {
                    if (effect == null
                        || effect.TargetModeIndex < 0
                        || effect.TargetModeIndex >= skill.TargetModes.Count)
                    {
                        continue;
                    }

                    var mode = PassiveEffectCombatExecutionSystem.ResolveMode(unit, skill, skill.TargetModes[effect.TargetModeIndex]);
                    if (mode?.TargetingType == TargetingType.Enemies
                        && mode.TargetType != TargetType.Caster
                        && mode.AbsoluteRange != null)
                    {
                        maxRange = Mathf.Max(
                            maxRange,
                            mode.AbsoluteRange.MaxRange);
                    }
                }
            }

            return maxRange;
        }

        private static float CalculateEffectsScore(
            CombatSession session, BattleUnit caster,
            SkillData skill,
            int targetModeIndex,
            IReadOnlyList<BattleUnit> targets,
            SkillEffectData singleEffect = null)
        {
            var score = 0f;

            for (var effectIndex = 0; effectIndex < skill.Effects.Count; effectIndex++)
            {
                var authoredEffect = skill.Effects[effectIndex];
                if (authoredEffect == null || authoredEffect.TargetModeIndex != targetModeIndex
                    || singleEffect != null && authoredEffect != singleEffect)
                {
                    continue;
                }

                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, authoredEffect);
                foreach (var target in targets)
                {
                    if (target?.Character == null || !SkillExecutionSystem.CanPlanEffect(session, effect, caster, target)) continue;

                    switch (effect.EffectType)
                    {
                        case EffectType.DealDamage:
                        {
                            score += DamageUtility(session, caster, target, skill, authoredEffect);
                            break;
                        }
                        case EffectType.ApplyStatus:
                            if (string.IsNullOrWhiteSpace(effect.StatusId)) break;
                            if (!StatusEffectSystem.IsDamageOverTimeStatus(effect.StatusId)
                                && StatusEffectSystem.HasStatus(target, effect.StatusId)) break;
                            score += StatusUtility(session, caster, target, skill, effect);
                            break;
                        case EffectType.ApplyUniqueBuff:
                            if (effect.UniqueBuffType == UniqueBuffType.Hawkeye
                                && !CombatDamageSystem.HasUniqueBuff(target, UniqueBuffType.Hawkeye))
                                score += Ai(GameDefineIds.AiModifyBonus, 40f) * effect.DurationTurns;
                            break;
                        case EffectType.ExtendStatusDuration:
                            if (target.IsPlayerSide != caster.IsPlayerSide && StatusEffectSystem.HasStatus(target, effect.StatusId))
                                score += Ai(GameDefineIds.AiSoftControlBonus, 100f) * effect.DurationTurns;
                            break;
                        case EffectType.RemoveStatus:
                            score += CleanseUtility(session, caster, target, effect.StatusId);
                            break;
                        case EffectType.Heal:
                        case EffectType.HealPercent:
                        case EffectType.HealMissingHealthPercent:
                            score += HealingUtility(session,caster,target,HealingAmount(caster,target,effect));
                            break;
                        case EffectType.RestoreResource:
                        case EffectType.RestoreResourcePercent:
                            var pool = target.Character.ResourcePool;
                            if (pool != null && !ResourceRuleSystem.IsResourceGainBlocked(target))
                            {
                                var amount = effect.EffectType == EffectType.RestoreResource ? effect.BaseValue : Mathf.CeilToInt(pool.MaxValue*effect.PercentageValue);
                                score += Mathf.Max(0,Mathf.Min(amount,pool.RecoveryLimit-pool.CurrentValue))*(target.IsPlayerSide==caster.IsPlayerSide?1f:-1f);
                            }
                            break;
                        case EffectType.Shield:
                            score += ShieldUtility(session,caster,target,effect);
                            break;
                        case EffectType.ModifyStat:
                        case EffectType.ModifyVar:
                            var suffix = effect.EffectType == EffectType.ModifyStat
                                ? "modify_stat" : "modify_var";
                            if (target.Character.PassiveEffects == null
                                || !target.Character.PassiveEffects.Exists(active => active != null
                                    && active.EffectId == $"{skill.SkillId}.{suffix}.{effectIndex}"
                                    && PassiveEffectLifecycleSystem.IsActive(active)))
                                score += DefensiveModifierUtility(session,caster,target,effect)*Mathf.Clamp01(effect.BaseChance);
                            break;
                        case EffectType.Push:
                        case EffectType.Pull:
                            score += Ai(GameDefineIds.AiPushPullBonus, 30f);
                            break;
                        case EffectType.Fallback:
                            score += Ai(GameDefineIds.AiFallbackBonus, 10f);
                            break;
                    }
                }
            }

            return score;
        }

        private static float CalculateExpectedDamage(BattleUnit caster, SkillEffectData effect)
        {
            if (caster?.Character?.Stats == null || effect == null) return 0f;

            if (effect.DamageValueSource == DamageValueSource.AlchemicalPower)
                return CombatDamageValueCalculator.CalculateAlchemicalDamage(caster.Character.Level, effect.DamageMultiplier);

            var minAttack = caster.Character.Stats.MinAttack.GetValue();
            var maxAttack = caster.Character.Stats.MaxAttack.GetValue();
            var attackValue = effect.DamageValueSource switch
            {
                DamageValueSource.MinAttack => minAttack,
                DamageValueSource.MaxAttack => maxAttack,
                DamageValueSource.AverageAttack => (minAttack + maxAttack) * 0.5f,
                _ => (minAttack + maxAttack) * 0.5f
            };

            return Mathf.Max(0f, attackValue * effect.DamageMultiplier * SummonSystem.DamageScale(caster, effect));
        }

        private static float GetMissingHealth(BattleUnit target)
        {
            if (target?.Character == null) return 0f;

            var maxHealth = target.Character.Stats?.MaxHealth.GetValue()
                ?? target.Character.CurrentHealth;
            return Mathf.Max(0f, maxHealth - target.Character.CurrentHealth);
        }

        private static bool IsHardControl(string statusId)
        {
            return string.Equals(statusId, StatusEffectIds.Stun, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Immobilize, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Freeze, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Sleep, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Paralysis, StringComparison.OrdinalIgnoreCase);
        }

        private static float Ai(string id, float fallback)
        {
            return GameDefinesRuntime.GetFloat(id, fallback);
        }

        public BattleUnit FindClosestPlayerUnit(CombatSession session, BattleUnit aiUnit)
        {
            BattleUnit closestUnit = null;
            var closestDistance = int.MaxValue;

            foreach (var unit in session.Units)
            {
                if (unit == null || unit.IsPlayerSide == aiUnit.IsPlayerSide)
                {
                    continue;
                }

                if (unit.Character == null
                    || unit.Character.LifeState != CharacterLifeState.Alive)
                {
                    continue;
                }

                if (session.Contact != null && !session.Contact.Sees(aiUnit, unit)) continue;
                var distance = GetManhattanDistance(aiUnit.Position, unit.Position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestUnit = unit;
                }
            }

            return closestUnit;
        }

        public bool TryMoveAiTowardsTarget(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target)
        {
            return TryMoveAiTowardsTarget(session, aiUnit, target, out _);
        }

        private bool TryMoveAiTowardsTarget(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target,
            out List<GridPosition> movementPath)
        {
            movementPath = null;
            if (session?.Grid == null
                || aiUnit?.Character == null
                || target?.Character == null)
            {
                return false;
            }

            if (!TryFindPathToTarget(session, aiUnit, target, out var path)) return false;
            return TryExecuteMovementPath(session, aiUnit, path, out movementPath);
        }

        private bool TryExecuteMovementPath(CombatSession session, BattleUnit aiUnit,
            List<GridPosition> path, out List<GridPosition> movementPath, bool searching = false)
        {
            movementPath = null;
            if (path == null || path.Count == 0) return false;
            // Opening is a separate action so the existing driver presents/synchronizes it
            // before movement. Never toggle an already-open door.
            var firstDoor = FindClosedDirectDoor(session, aiUnit.Position, path[0]);
            if (firstDoor != null)
                return interactionSystem.TryInteractWithDoor(session, aiUnit, firstDoor.ObjectId);

            var movementSegment = new List<GridPosition>();
            var previous = aiUnit.Position;
            foreach (var next in path)
            {
                if (FindClosedDirectDoor(session, previous, next) != null) break;
                movementSegment.Add(next);
                previous = next;
            }

            // The movement owner applies real costs/statuses and may stop before the planned
            // endpoint. Present only the completed prefix, retaining its aggregated feedback.
            movementSystem.TryMoveAlongPath(session, aiUnit, movementSegment, out var completedPath,
                searching ? () =>
                {
                    session.Contact?.Refresh(session);
                    return FindClosestPlayerUnit(session, aiUnit) != null;
                } : null);
            if (completedPath.Count == 0) return false;
            movementPath = completedPath;
            return true;
        }

        private bool TryFindPathToTarget(
            CombatSession session, BattleUnit aiUnit, BattleUnit target,
            out List<GridPosition> path)
        {
            path = null;
            if (session?.Grid == null || aiUnit?.Character == null || target?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive
                || target.Character.LifeState != CharacterLifeState.Alive) return false;

            return TryFindPathToPosition(session, aiUnit, target.Position, true, out path);
        }

        private bool TryFindPathToPosition(CombatSession session, BattleUnit aiUnit, GridPosition destination,
            bool stopAdjacent, out List<GridPosition> path, Func<GridPosition, bool> goal = null)
        {
            var result = new AiResult<List<GridPosition>>();
            foreach (var step in FindPathWork(session, aiUnit, destination, stopAdjacent, result, goal)) { }
            path = result.Value;
            return path != null;
        }

        private IEnumerable<bool> FindPathWork(CombatSession session, BattleUnit aiUnit, GridPosition destination,
            bool stopAdjacent, AiResult<List<GridPosition>> result, Func<GridPosition, bool> goal = null, int maximumSteps = int.MaxValue, Func<GridPosition, IReadOnlyList<GridPosition>, bool> routeGoal = null)
        {

            var start = aiUnit.Position;
            // Cache route costs and order incrementally: sorting every frontier while
            // recalculating every full path stalls large/disconnected layered rooms.
            var order = Comparer<(GridPosition cell, List<GridPosition> path, float risk, int cost, string traps, int serial)>.Create((a, b) =>
            {
                var result = a.risk.CompareTo(b.risk);
                if (result == 0) result = a.cost.CompareTo(b.cost);
                return result != 0 ? result : a.serial.CompareTo(b.serial);
            });
            var pending = new SortedSet<(GridPosition cell, List<GridPosition> path, float risk, int cost, string traps, int serial)>(order);
            var serial = 0;
            pending.Add((start, new List<GridPosition>(), 0f, 0, "", serial++));
            var best = new Dictionary<(GridPosition, string), (float risk, int cost)> { [(start, "")] = (0f, 0) };
            while (pending.Count > 0)
            {
                yield return false;
                var node = pending.Min;
                pending.Remove(node);
                var current = node.cell;
                if (best.TryGetValue((current, node.traps), out var known)
                    && (known.risk < node.risk || known.risk == node.risk && known.cost < node.cost)) continue;
                if ((routeGoal != null ? routeGoal(current,node.path) : goal?.Invoke(current) == true || goal == null && (stopAdjacent
                    ? IsAdjacent(current, destination) && CanPlanAcross(session, aiUnit, current, destination)
                    : current == destination)))
                {
                    var path = node.path;
                    if (stopAdjacent && FindClosedDirectDoor(session, current, destination) != null) path.Add(destination);
                    result.Value = path;
                    yield break;
                }
                if (node.path.Count >= maximumSteps) continue;
                foreach (var candidate in PlanningNeighbours(session, current))
                {
                    yield return false;
                    if (candidate == start || node.path.Contains(candidate) || session.IsPositionOccupied(candidate, aiUnit)
                        || session.DoesObjectBlockMovement(session.GetMapObjectAt(candidate))
                        || !CanPlanAcross(session, aiUnit, current, candidate)) continue;
                    var next = new List<GridPosition>(node.path) { candidate };
                    var risk = session.EvaluateTrapRisk?.Invoke(aiUnit, next) ?? 0f;
                    if (!float.IsFinite(risk)) continue;
                    var key = (candidate, session.TrapRouteState?.Invoke(next) ?? "");
                    var cost = node.cost + (aiUnit.MovementStepsThisTurn + next.Count
                        + session.Grid.GetMovementSurcharge(current, candidate)) * ActionPointUnits.PerPoint;
                    if (best.TryGetValue(key, out var old) && (old.risk < risk || old.risk == risk && old.cost <= cost)) continue;
                    best[key] = (risk, cost);
                    pending.Add((candidate, next, risk, cost, key.Item2, serial++));
                }
            }
            yield break;
        }

        private static IEnumerable<GridPosition> PlanningNeighbours(CombatSession session, GridPosition current)
            => session.GetNavigationNeighbours(current);

        private static bool CanPlanAcross(
            CombatSession session, BattleUnit unit, GridPosition from, GridPosition to)
        {
            // AI navigation never expands into an exit cell, including AI-controlled allies.
            if (!session.Grid.IsWalkable(to) || session.Grid.IsBlocked(to)
                || session.ExternalPositionOccupied?.Invoke(to) == true) return false;
            if (session.CanMoveBetween(unit, from, to)) return true;
            return FindClosedDirectDoor(session, from, to) != null;
        }

        private static DoorCombatMapObject FindClosedDirectDoor(
            CombatSession session, GridPosition from, GridPosition to)
            => session.FindClosedDirectDoor(from, to);

        private static List<GridPosition> BuildPath(
            IReadOnlyDictionary<GridPosition, GridPosition> previous,
            GridPosition start,
            GridPosition destination)
        {
            var path = new List<GridPosition>();
            var current = destination;
            while (current != start)
            {
                path.Insert(0, current);
                if (!previous.TryGetValue(current, out current))
                {
                    return new List<GridPosition>();
                }
            }

            return path;
        }

        public int GetManhattanDistance(GridPosition a, GridPosition b)
        {
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        public bool IsAdjacent(GridPosition a, GridPosition b)
        {
            return GetManhattanDistance(a, b) == 1;
        }

        private IReadOnlyList<BattleUnit> FilterTargetsByLineOfSight(CombatSession session, BattleUnit caster,
            SkillData skill, IReadOnlyList<BattleUnit> targets)
        {
            if (targets != null && session.Contact != null && !caster.IsPlayerSide)
                targets = targets.Where(target => target != null && (!target.IsPlayerSide || session.Contact.Sees(caster, target))).ToList();
            if (targets == null || skill == null || !skill.RequiresLineOfSight)
            {
                return targets ?? new List<BattleUnit>();
            }

            return targets.Where(target => target != null
                && lineOfSightService.HasLineOfSight(session, caster.Position, target.Position)).ToList();
        }

        private sealed class AiSkillSelection
        {
            public SkillData Skill;
            public Dictionary<int, IReadOnlyList<BattleUnit>> TargetsByModeIndex;
            public GridPosition? SummonPlacementDestination;
            public float Score;
        }
    }
}
