using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Configuration;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.Characters;
using Godmist.Views.Game;
using UnityEngine;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Quests;

namespace Godmist.Views.Combat
{
    public sealed class CombatExecutionDriver : IDisposable
    {
        private static readonly Unity.Profiling.ProfilerMarker SelectablePositionsSample = new("Godmist.Targeting.SelectablePositions");
        private static readonly Unity.Profiling.ProfilerMarker AbsoluteRangeSample = new("Godmist.Targeting.AbsoluteRange");
        private readonly CharacterDefinition jerzyDefinition;
        private readonly CharacterDefinition allyDefinition;
        private readonly CharacterDefinition enemyDefinition;
        private readonly CharacterClassData scoutClass;
        private readonly CharacterClassData warriorClass;
        private readonly int gridWidth;
        private readonly int gridHeight;
        private readonly float aiDecisionDelay;
        private readonly float aiMovementResultDelay;
        private readonly float aiSkillResultDelay;
        private readonly CombatBattleLog battleLog;
        private readonly CombatFloatingFeedbackController floatingFeedback;
        private readonly List<GridPosition> blockedPositions;
        private readonly CombatDungeonAreaView dungeonAreaView;
        private readonly CombatCameraFramer cameraFramer;
        private readonly CombatScenarioRunner scenarioRunner;
        private readonly CombatSandboxBootstrap host;
        public CombatExecutionDriver(CombatSandboxBootstrap host, CombatUnitViewSpawner spawner, CharacterDefinition jerzyDefinition, CharacterDefinition allyDefinition, CharacterDefinition enemyDefinition, CharacterClassData scoutClass, CharacterClassData warriorClass, int gridWidth, int gridHeight, float aiDecisionDelay, float aiMovementResultDelay, float aiSkillResultDelay, CombatBattleLog battleLog, CombatFloatingFeedbackController floatingFeedback, List<GridPosition> blockedPositions, CombatDungeonAreaView dungeonAreaView, CombatCameraFramer cameraFramer, CombatScenarioRunner scenarioRunner)
        {
            this.host = host;
            unitViewSpawner = spawner;
            this.jerzyDefinition = jerzyDefinition;
            this.allyDefinition = allyDefinition;
            this.enemyDefinition = enemyDefinition;
            this.scoutClass = scoutClass;
            this.warriorClass = warriorClass;
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.aiDecisionDelay = aiDecisionDelay;
            this.aiMovementResultDelay = aiMovementResultDelay;
            this.aiSkillResultDelay = aiSkillResultDelay;
            this.battleLog = battleLog;
            this.floatingFeedback = floatingFeedback;
            this.blockedPositions = blockedPositions;
            this.dungeonAreaView = dungeonAreaView;
            this.cameraFramer = cameraFramer;
            this.scenarioRunner = scenarioRunner;
        }
        private readonly List<string> logEntries = new();
        public CombatFeedbackBus FeedbackBus => feedbackBus;
        public void BindFeedback()
        {
            if (!Session.IsSeamless) floatingFeedback?.Bind(feedbackBus);
            battleLog?.ClearLog();
            foreach (var entry in logEntries) battleLog?.AddEntry(entry);
        }
        public bool TryJoin(BattleUnit unit, GridPosition position, ISet<GridPosition> reservedArrivals = null, bool beginIfIdle = true, bool allowDowned = false)
        {
            if (!DungeonCombatParticipationSystem.TryJoin(Session, unit, position, reservedArrivals, allowDowned)) return false;
            ScoutKnifeSystem.ResetForCombat(Session, true);
            if (beginIfIdle && Session.ActiveParticipant == null) turnSystem.AdvanceTurn(Session);
            return true;
        }
        public bool TryLeave(BattleUnit unit)
        {
            if (Session == null || IsActionInProgress || Session.MembershipChangePending) return false;
            if (!DungeonCombatParticipationSystem.TryLeave(Session, unit)) return false;
            summonSystem?.RemoveSummonsCreatedBy(Session, unit.Character.CharacterId);
            skillExecutionSystem?.CancelScheduledEffectsByCaster(Session, unit.Character.CharacterId);
            Session.HasEscapePenaltyEligibleCharacters = true;
            Session.EscapePenaltyEligibleCharacterIds.Add(unit.Character.CharacterId);
            // Release the borrowed exploration view before advancing the next turn.
            UnitLeftCombat?.Invoke(Session, unit);
            outcomeSystem.EvaluateCombatOutcome(Session);
            if (Session.Outcome == CombatOutcomeState.Ongoing && Session.ActiveParticipant == null)
                turnSystem.AdvanceTurn(Session);
            return true;
        }
        // Systems
        [field: NonSerialized] public CombatSession Session { get; private set; }
        private CombatOutcomeSystem outcomeSystem;
        private CombatTurnSystem turnSystem;
        private SimpleCombatAiSystem aiSystem;
        private SkillExecutionSystem skillExecutionSystem;
        private StatusEffectSystem statusEffectSystem;

        public bool TryExtractScoutKnife(BattleUnit owner, BattleUnit target)
        {
            if (Session == null || IsActionInProgress || Session.MembershipChangePending
                || !ScoutCombatSystem.TryExtractKnife(Session, owner, target, statusEffectSystem, recoverySystem)) return false;
            var direction = new Vector2(target.Position.X - owner.Position.X, target.Position.Y - owner.Position.Y);
            if (CombatPoseResolver.TryResolveAction(owner.Character.Definition.Id, "extract", direction, out var pose))
                PresentPose(owner, pose, direction, SkillPoseDuration);
            return true;
        }

        private CombatMovementSystem movementSystem;
        private CombatDamageSystem damageSystem;
        private CombatShieldSystem shieldSystem;
        private CombatFeedbackBus feedbackBus;
        private CombatReviveSystem reviveSystem;
        private CombatTargetingService targetingService;
        private ResourceRuleSystem resourceRuleSystem;
        private CombatRecoverySystem recoverySystem;
        private CombatInteractionSystem interactionSystem;
        private CombatEscapeSystem escapeSystem;
        private CombatLineOfSightService lineOfSightService;
        private SummonedObjectSystem summonedObjectSystem;
        private CatacombsBossSystem catacombsBossSystem;
        private ForestEntBossSystem forestEntBossSystem;
        private DeadKingBossSystem deadKingBossSystem;
        private SkeletonSlayerBossSystem skeletonSlayerBossSystem;
        private BanditLeaderBossSystem banditLeaderBossSystem;
        private SummonSystem summonSystem;
        private CharacterStatPipelineSystem statPipeline;
        private PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;
        private ModifyStatSystem modifyStatSystem;
        private CombatVariableSystem combatVariableSystem;
        private ModifyVarSystem modifyVarSystem;
        private ModifyResistanceSystem modifyResistanceSystem;
        private PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;
        private PassiveEffectCombatExecutionSystem passiveEffectCombatExecutionSystem;
        private bool hasClearedCombatScopedEffects;
        private readonly CombatRewardSystem rewardSystem = new();
        private readonly DungeonCombatOutcomeSystem dungeonCombatOutcomeSystem = new();
        private bool usesGameSessionCombat;
        private bool hasRequestedExplorationReturn;
        private Coroutine activeAiTurnCoroutine;
        private Coroutine activePlayerMovementPresentationCoroutine;
        private CombatUnitViewSpawner unitViewSpawner;
        public event Action<CombatMapObject> CombatMapObjectChanged;
        public event Action<CombatSession, BattleUnit> UnitLeftCombat;
        public bool IsPlayerMovementPresentationInProgress =>
            activePlayerMovementPresentationCoroutine != null;

        private readonly CharacterInstanceFactory characterInstanceFactory = new();

        public void Initialize(CombatSession suppliedSession = null)
        {
            passiveEffectEventDispatcher = new PassiveEffectCombatEventDispatcher();
            feedbackBus = new CombatFeedbackBus();
            feedbackBus.Published += HandlePoseFeedback;
            resourceRuleSystem = new ResourceRuleSystem(AddBattleLog, feedbackBus);
            turnSystem = new CombatTurnSystem(
                AddBattleLog,
                HandleUnitTurnBegan,
                HandleUnitTurnEnded,
                HandleSummonedObjectTurnBegan,
                HandleSummonedObjectTurnEnded,
                HandleUnitDied,
                passiveEffectEventDispatcher,
                onUnitDownedTurn: unit => statusEffectSystem.ProcessDownedTurn(unit),
                resourceRuleSystem: resourceRuleSystem);
            Session = suppliedSession ?? CreateInitialSession();
            Session.Diagnostics = GameBootstrap.Instance?.Session?.CombatDiagnostics;
            Session.EnemyDefeated = id => rewardSystem.TryGrantDefeatExperience(GameBootstrap.Instance?.Session, Session, id);
            if (Session != null) Session.ResumeTurn = ResumeSuspendedTurn;
            usesGameSessionCombat = suppliedSession != null || usesGameSessionCombat;
            Session.PassiveEffectEventDispatcher = passiveEffectEventDispatcher;
            if (!Session.IsSeamless) ScoutKnifeSystem.ResetForCombat(Session);
            RebuildDungeonCombatArea();
            outcomeSystem = new CombatOutcomeSystem(AddBattleLog);
            escapeSystem = new CombatEscapeSystem(AddBattleLog);
            if (!Session.IsSeamless) floatingFeedback?.Bind(feedbackBus);
            combatVariableSystem = new CombatVariableSystem();
            damageSystem = new CombatDamageSystem(
                AddBattleLog,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                RemoveUnitFromCombat,
                combatVariableSystem,
                feedbackBus,
                passiveEffectEventDispatcher);
            recoverySystem = new CombatRecoverySystem(AddBattleLog, feedbackBus);
            shieldSystem = new CombatShieldSystem(AddBattleLog, feedbackBus);
            reviveSystem = new CombatReviveSystem(AddBattleLog);
            statPipeline = new CharacterStatPipelineSystem();

            passiveEffectLifecycleSystem = new PassiveEffectLifecycleSystem(
                AddBattleLog,
                new IPassiveEffectTurnProcessor[]
                {
                    new DamageOverTimePassiveEffectProcessor(damageSystem),
                    new SleepPassiveEffectProcessor(recoverySystem),
                    new PeriodicRecoveryPassiveEffectProcessor(recoverySystem)
                },
                statPipeline);

            modifyStatSystem = new ModifyStatSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            modifyVarSystem = new ModifyVarSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            modifyResistanceSystem = new ModifyResistanceSystem(
                AddBattleLog,
                passiveEffectLifecycleSystem);
            statusEffectSystem = new StatusEffectSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                damageSystem,
                passiveEffectLifecycleSystem,
                feedbackBus,
                passiveEffectEventDispatcher);
            passiveEffectCombatExecutionSystem = new PassiveEffectCombatExecutionSystem(
                Session,
                statusEffectSystem,
                passiveEffectLifecycleSystem,
                AddBattleLog);
            passiveEffectCombatExecutionSystem.ReactionDamageSystem = damageSystem;
            lineOfSightService = new CombatLineOfSightService();
            summonedObjectSystem = new SummonedObjectSystem(
                AddBattleLog,
                HandleCombatMapObjectChanged,
                new SummonedObjectEffectExecutor(
                    AddBattleLog,
                    damageSystem,
                    recoverySystem,
                    shieldSystem,
                    statusEffectSystem,
                    modifyStatSystem,
                    modifyVarSystem,
                    feedbackBus));
            catacombsBossSystem = new CatacombsBossSystem(
                summonedObjectSystem, damageSystem, AddBattleLog, feedbackBus);
            Session.EnemyHealthChanged = (enemy, previousHealth) =>
                catacombsBossSystem.OnEnemyHealthChanged(Session, enemy, previousHealth);
            Session.BossRuneEnteredCell = unit =>
                catacombsBossSystem.OnUnitEnteredCell(Session, unit);
            forestEntBossSystem = new ForestEntBossSystem(Session, damageSystem, statusEffectSystem,
                recoverySystem, HandleCombatMapObjectChanged, AddBattleLog, feedbackBus);
            deadKingBossSystem = new DeadKingBossSystem(Session, damageSystem, statusEffectSystem, HandleCombatMapObjectChanged, AddBattleLog, feedbackBus);
            skeletonSlayerBossSystem = new SkeletonSlayerBossSystem(Session, damageSystem, statusEffectSystem, HandleCombatMapObjectChanged, AddBattleLog, feedbackBus);
            banditLeaderBossSystem = new BanditLeaderBossSystem(Session,damageSystem,statusEffectSystem,recoverySystem,summonedObjectSystem,AddBattleLog,feedbackBus);
            summonSystem = new SummonSystem(AddBattleLog);
            skillExecutionSystem = new SkillExecutionSystem(
                AddBattleLog,
                RemoveUnitFromCombat,
                session => outcomeSystem.EvaluateCombatOutcome(session),
                statusEffectSystem,
                damageSystem,
                recoverySystem,
                shieldSystem,
                lineOfSightService: lineOfSightService,
                modifyStatSystem: modifyStatSystem,
                modifyVarSystem: modifyVarSystem,
                summonedObjectSystem: summonedObjectSystem,
                summonSystem: summonSystem,
                feedbackBus: feedbackBus,
                modifyResistanceSystem: modifyResistanceSystem,
                passiveEffectEventDispatcher: passiveEffectEventDispatcher,
                resourceRuleSystem: resourceRuleSystem);
            passiveEffectCombatExecutionSystem.ReactionDamageSystem = damageSystem;
            ConfigurePassiveEffectExecutionCallbacks();
            movementSystem = new CombatMovementSystem(
                AddBattleLog,
                resourceRuleSystem,
                statusEffectSystem,
                feedbackBus,
                passiveEffectEventDispatcher);
            interactionSystem = new CombatInteractionSystem(
                AddBattleLog, HandleCombatMapObjectChanged, feedbackBus);
            interactionSystem.InitializeMapObjects(Session);
            Session.ObjectDurabilityChanged = HandleCombatMapObjectChanged;
            targetingService = new CombatTargetingService();
            aiSystem = new SimpleCombatAiSystem(
                battleLog: AddBattleLog, 
                endTurn: session => turnSystem.EndActiveTurn(session), 
                evaluateOutcome: session => outcomeSystem.EvaluateCombatOutcome(session),
                skillExecutionSystem: skillExecutionSystem, targetingService: targetingService,
                movementSystem: movementSystem, lineOfSightService: lineOfSightService,
                interactionSystem: interactionSystem, catacombsBossSystem: catacombsBossSystem);
            if (!Session.IsSeamless) battleLog?.ClearLog();
            AddBattleLog("Walka rozpoczęta.");

            if (!Session.IsSeamless) StartTurns();
        }

        private bool aiActionInProgress;
        public const float SkillPoseDuration = 2f;
        private const float RevivePoseDuration = 1.25f;
        private float skillPresentationEnds;
        private readonly Dictionary<string, float> skillVfxDeadlines = new();
        public bool IsSkillPresentationInProgress => Time.time < skillPresentationEnds;
        public bool IsActionInProgress => IsPlayerMovementPresentationInProgress || aiActionInProgress || IsSkillPresentationInProgress;

        // Pure presentation for an executed non-combat support action; no turn ownership or engine initialization.
        public void PresentExplorationSupportFeedback(CombatFeedbackEvent feedback) => HandlePoseFeedback(feedback);
        private void HandlePoseFeedback(CombatFeedbackEvent feedback)
        {
            if (feedback.Type == CombatFeedbackType.ProcTriggered && feedback.EffectName == CombatBossVfxFeedback.TrapResolutionCue)
            { RegisterSkillVfxDeadline(feedback.ActionId); return; }
            if (feedback.Type == CombatFeedbackType.TargetEffect)
            {
                float? deadline = feedback.ActionId != null && skillVfxDeadlines.TryGetValue(feedback.ActionId, out var end)
                    && end > Time.time ? end : null;
                unitViewSpawner?.TryPlayTargetVfx(Session, feedback, deadline);
                return;
            }
            if(feedback.Type==CombatFeedbackType.MapObjectTriggered)
            {
                // Leader traps already publish their resolved floor and victim cues.
                // Preserve the activation notification without rendering a second central burst.
                if (!CombatBossVfxFeedback.IsLeaderTrap(feedback.MapObject))
                    unitViewSpawner?.TryPlayMapObjectTrigger(Session,feedback.MapObject);
                return;
            }
            if(feedback.Type==CombatFeedbackType.VisualCue)
            {
                var unit=feedback.Target;
                if(feedback.EffectName==BanditLeaderBossSystem.ConcealCue && unit?.Character?.Definition!=null
                    && unitViewSpawner?.IsUnitVisible(unit)==true
                    && CombatPoseResolver.TryResolveAction(unit.Character.Definition.Id,"conceal",Vector2.down,out var cue))
                    for(var i=0;i<cue.Effects.Length;i++)unitViewSpawner.TryPlayWorldEffect(Session,unit.Position,cue.Effects[i],cue.EffectOffsets[i],.18f);
                return;
            }
            if(feedback.Type==CombatFeedbackType.ProcTriggered && feedback.EffectName=="Nieugięty Dowódca"
                && feedback.Target?.BanditLeaderState is {PhaseTwo:true} bandit)
            {PresentTransformation(feedback.Target,bandit.OriginalDefinition);return;}
            if(feedback.Type==CombatFeedbackType.ProcTriggered && feedback.Target?.SkeletonSlayerState is { } slayer)
            {
                if(feedback.EffectName=="Zemsta Zabójcy" && slayer.PhaseTwo)
                {PresentTransformation(feedback.Target,slayer.OriginalDefinition);return;}
                if(feedback.EffectName=="Uderzenie Młota Zniszczenia")
                {
                    RegisterSkillVfxDeadline(feedback.ActionId);
                    var unit=feedback.Target;
                    var facing=new Vector2(slayer.HammerCenter.X-unit.Position.X,slayer.HammerCenter.Y-unit.Position.Y);
                    if(CombatPoseResolver.TryResolveAction(unit.Character.Definition.Id,"destruction-impact",facing,out var impact))
                        PresentPose(unit,impact,facing,SkillPoseDuration,false);
                    return;
                }
            }
            if (feedback.Type == CombatFeedbackType.ProcTriggered && feedback.EffectName == DeadKingBossSystem.ShadowImpactCue)
            {
                RegisterSkillVfxDeadline(feedback.ActionId);
                var unit = feedback.Target;
                if (unit?.Character?.Definition != null && CombatPoseResolver.TryResolveSkill(unit.Character.Definition.Id,
                    DeadKingBossSystem.ShadowId, Vector2.down, AttackType.Unknown, false, out var burst))
                    PresentPose(unit, burst, Vector2.down, SkillPoseDuration, false);
                return;
            }
            // The boss owner has already applied the existing phase/skillset rules.
            // Reuse that one-shot notification to hold presentation for all six frames.
            if (feedback.Type == CombatFeedbackType.ProcTriggered && feedback.EffectName == "Widmowy Szał"
                && feedback.Target?.DeadKingState?.PhaseTwo == true)
            {
                PresentTransformation(feedback.Target, feedback.Target.DeadKingState.OriginalDefinition);
                return;
            }
            if (feedback.SkillPresentation.HasValue)
            {
                var context = feedback.SkillPresentation.Value;
                var unit = context.Caster;
                if (unit?.Character?.Definition == null || context.Skill == null) return;
                RegisterSkillVfxDeadline(feedback.ActionId);
                var facing = CombatPoseResolver.Facing(context, unitViewSpawner?.GetFacing(unit) ?? Vector2.down);
                if (CombatPoseResolver.TryResolveSkill(unit.Character.Definition.Id, context.Skill.SkillId,
                        facing, context.AttackType, context.KnifeRecovery, out var pose))
                {
                    PresentPose(unit, pose, facing, SkillPoseDuration, false);
                    if (pose.TargetHeadEffects.Length > 0 && context.Targets != null
                        && context.Skill.SkillId != BanditLeaderBossSystem.OrderId)
                        foreach (var markedTarget in context.Targets)
                            unitViewSpawner?.TryPlayTargetHeadEffects(Session, markedTarget, pose.TargetHeadEffects,
                                SkillPoseDuration,pose.TargetHeadOffsets);
                }
                BeginSkillCameraFocus(context);
                return;
            }
            if (feedback.Type != CombatFeedbackType.Miss
                && ((feedback.Type != CombatFeedbackType.Damage && feedback.Type != CombatFeedbackType.ShieldLoss)
                    || feedback.Amount <= 0)) return;
            if (feedback.DamageType == DamageType.Bleed || feedback.DamageType == DamageType.Poison
                || feedback.DamageType == DamageType.Burn) return;
            var target = feedback.Target;
            if (target?.Character?.Definition == null) return;
            if (feedback.Source == target) return; // HP conversion keeps its casting pose.
            var direction = unitViewSpawner?.GetFacing(target) ?? Vector2.down;
            if (feedback.SourcePosition.HasValue)
            {
                var source = feedback.SourcePosition.Value;
                direction = new Vector2(source.X - feedback.Position.X, source.Y - feedback.Position.Y);
            }
            var action = feedback.Type == CombatFeedbackType.Miss ? "dodge" : "hit";
            if (CombatPoseResolver.TryResolveAction(target.Character.Definition.Id, action, direction, out var reaction))
                PresentPose(target, reaction, direction, Mathf.Max(.6f, skillPresentationEnds - Time.time), false);
        }

        private void RegisterSkillVfxDeadline(string actionId)
        {
            foreach (var expired in skillVfxDeadlines.Where(p => p.Value <= Time.time).Select(p => p.Key).ToArray())
                skillVfxDeadlines.Remove(expired);
            if (!string.IsNullOrEmpty(actionId)) skillVfxDeadlines[actionId] = Time.time + SkillPoseDuration;
            skillPresentationEnds = Mathf.Max(skillPresentationEnds, Time.time + SkillPoseDuration);
        }

        private void PresentTransformation(BattleUnit unit,CharacterDefinition previousDefinition)
        {
            var frames=previousDefinition.TransformationFrames;
            if(frames == null || frames.Count==0 || unitViewSpawner?.IsUnitVisible(unit)==false) return;
            if(unitViewSpawner?.TryPlayTransformation(Session,unit,frames,previousDefinition.TransformationFramesPerSecond)!=true) return;
            var duration=frames.Count/Mathf.Max(.01f,previousDefinition.TransformationFramesPerSecond);
            skillPresentationEnds=Mathf.Max(skillPresentationEnds,Time.time+duration);
            cameraFramer?.BeginActionFocus(Session,unit);
        }
        private void PresentPose(BattleUnit unit, CombatPose pose, Vector2 facing, float duration, bool focus = true)
        {
            if (unitViewSpawner?.TryPlayCombatPose(Session, unit, pose, facing, duration) != true) return;
            skillPresentationEnds = Mathf.Max(skillPresentationEnds, Time.time + duration);
            if (focus) cameraFramer?.BeginActionFocus(Session, unit);
        }

        private void BeginSkillCameraFocus(CombatSkillPresentation context)
        {
            if (cameraFramer == null || cameraFramer.FocusedSession != Session || unitViewSpawner == null
                || !unitViewSpawner.TryGetPresentationBounds(context.Caster, out var casterBounds)) return;
            var participants = new List<(BattleUnit Unit, Bounds KnownBounds)> { (context.Caster, casterBounds) };
            if (context.Targets != null)
                foreach (var target in context.Targets)
                    if (target != context.Caster && unitViewSpawner.IsUnitVisible(target)
                        && unitViewSpawner.TryGetPresentationBounds(target, out var bounds)) participants.Add((target, bounds));
            Bounds? groundAim = null;
            if ((context.Targets == null || context.Targets.Count == 0) && context.Aim.HasValue
                && unitViewSpawner.TryGetWorldPosition(context.Aim.Value, out var aim))
                groundAim = new Bounds(aim, Vector3.zero);
            Bounds FrameBounds()
            {
                var result = casterBounds;
                for (var i = 0; i < participants.Count; i++)
                {
                    var participant = participants[i];
                    // Retain the last known frame after a killing hit or loss of
                    // visibility, without following a newly hidden position.
                    var bounds = unitViewSpawner.IsUnitVisible(participant.Unit)
                        && unitViewSpawner.TryGetPresentationBounds(participant.Unit, out var current)
                            ? current : participant.KnownBounds;
                    participants[i] = (participant.Unit, bounds);
                    result.Encapsulate(bounds);
                }
                if (groundAim.HasValue) result.Encapsulate(groundAim.Value);
                result.Expand(new Vector3(.25f, .25f, 0)); // Silhouette clearance; text margins are in screen pixels.
                return result;
            }
            skillPresentationEnds = Mathf.Max(skillPresentationEnds, Time.time + SkillPoseDuration);
            cameraFramer.BeginActionFocus(Session, context.Caster, FrameBounds);
        }
        public void ResumeSuspendedTurn(CombatTurnParticipant participant)
        {
            if (participant?.Unit is not {} unit) return;
            cameraFramer?.NotifyTurnBegan(Session, unit);
            if (TryGetTurnBlockingStatus(unit, out var label))
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(unit, CombatFeedbackType.TurnSkipped, effectName: label));
                aiActionInProgress = true;
                activeAiTurnCoroutine = host.StartCoroutine(PresentSkippedTurn(unit));
            }
            else if (!unit.IsPlayerControlled)
                activeAiTurnCoroutine = host.StartCoroutine(ExecuteAiTurnVisually(unit));
        }

        // A targeting-only driver has no subscriptions, turn ticks, bosses or effects.
        public void InitializeTargetingPreview(CombatSession session)
        {
            Session = session;
            targetingService = new CombatTargetingService();
            lineOfSightService = new CombatLineOfSightService();
            skillExecutionSystem = new SkillExecutionSystem();
        }

        public bool ValidateOpeningSkill(SkillData skill, IReadOnlyDictionary<int, GridPosition?> positions,
            GridPosition? teleport, GridPosition? placement, IReadOnlyDictionary<int, int> rotations,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosen,
            out Dictionary<int, IReadOnlyList<BattleUnit>> targets)
        {
            targets = null;
            return Session?.ActiveUnit?.Character?.GetActiveSkills()?.Contains(skill) == true
                && TryBuildSkillTargetGroups(skill, positions, rotations, out targets, chosen)
                && skillExecutionSystem.CanPreview(Session, Session.ActiveUnit, skill, targets, teleport, placement, positions);
        }

        public bool TryExecuteEntrySkill(BattleUnit actor, SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targets,
            IReadOnlyDictionary<int, GridPosition?> positions, GridPosition? teleport, GridPosition? placement,
            IReadOnlyDictionary<int, int> rotations = null)
        {
            if (host?.IsGameplaySuspended == true || Session?.IsSeamless != true || IsActionInProgress
                || Session.MembershipChangePending || actor?.IsPlayerControlled != true
                || !Session.Units.Contains(actor) || Session.EntryActionCaster != null) return false;
            Session.EntryActionCaster = actor;
            try
            {
                if (!skillExecutionSystem.CanPreview(Session, actor, skill, targets, teleport, placement, positions)) return false;
                if (!turnSystem.BeginEntryTurn(Session, actor, ApplyUnitTurnStartEffects)) return false;
                return skillExecutionSystem.TryExecute(Session, actor, skill, targets, teleport, placement, positions,
                    rotationsByModeIndex: rotations);
            }
            finally
            {
                Session.EntryActionCaster = null;
                if (Session.Outcome == CombatOutcomeState.Ongoing && Session.ActiveParticipant == null)
                    turnSystem.AdvanceTurn(Session);
            }
        }

        public void StartTurns(BattleUnit openingActor = null, IReadOnlyDictionary<BattleUnit, float> detection = null)
        {
            ScoutKnifeSystem.ResetForCombat(Session, Session.IsSeamless);
            turnSystem.InitializeTurnOrder(Session, openingActor, detection);
        }

        public void Dispose() => Dispose(false);

        public void Dispose(bool preserveCombatState)
        {
            if (feedbackBus != null) feedbackBus.Published -= HandlePoseFeedback;
            unitViewSpawner?.ClearCombatPoses(Session);
            cameraFramer?.EndActionFocus(Session);
            cameraFramer?.EndMovementFocus(Session);
            skillPresentationEnds = 0f;
            skillVfxDeadlines.Clear();
            if (!preserveCombatState) passiveEffectLifecycleSystem?.RemoveCombatScopedEffects(Session);
            banditLeaderBossSystem?.Dispose(preserveCombatState);
            banditLeaderBossSystem=null;
            skeletonSlayerBossSystem?.Dispose(preserveCombatState);
            skeletonSlayerBossSystem = null;
            deadKingBossSystem?.Dispose(preserveCombatState);
            deadKingBossSystem = null;
            forestEntBossSystem?.Dispose(preserveCombatState);
            forestEntBossSystem = null;
            if (Session != null) { Session.EnemyDefeated = null; Session.PassiveEffectEventDispatcher = null; Session.ObjectDurabilityChanged = null; Session.EnemyHealthChanged = null; Session.BossRuneEnteredCell = null; }
            passiveEffectCombatExecutionSystem?.Dispose();
            passiveEffectCombatExecutionSystem = null;
            if (activeAiTurnCoroutine != null)
            {
                host.StopCoroutine(activeAiTurnCoroutine);
                activeAiTurnCoroutine = null;
            }

            if (activePlayerMovementPresentationCoroutine != null)
            {
                host.StopCoroutine(activePlayerMovementPresentationCoroutine);
                activePlayerMovementPresentationCoroutine = null;
            }
        }
        public void Tick(bool acceptsInput)
        {
            if (!IsSkillPresentationInProgress)
                cameraFramer?.EndActionFocus(Session);
            ClearCombatScopedEffectsIfNeeded();
            if (host.IsGameplaySuspended) return;
            if (Session?.IsSeamless == true) return;
            if (usesGameSessionCombat && TryReturnToExploration()) return;
            if (!acceptsInput) return;
            if (DevConsoleView.IsOpen) return;
            if (Session == null || Session.ActiveUnit == null || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                RequestEndTurn();
                return;
            }
            
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.K))
            {
                RemoveFirstEnemyForDebug();
                return;
            }
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.L))
            {
                RemoveActivePlayerUnitForDebug();
                return;
            }
            if (!usesGameSessionCombat && Input.GetKeyDown(KeyCode.O))
            {
                DownFirstAllyForDebug();
                return;
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                RequestEscape();
                return;
            }
        }
        private CombatSession CreateSandboxSession()
        {
            var session = new CombatSession
            {
                CombatId = "sandbox.combat.01",
                Outcome = CombatOutcomeState.Ongoing,
                ActionPointsPerSpeed = GameDefinesRuntime.GetFloat(
                    GameDefineIds.ActionPointsPerSpeed, .25f),
                Grid = new BattleGrid(gridWidth, gridHeight),
                Units = new List<BattleUnit>()
            };
            foreach (var blockedPosition in blockedPositions)
            {
                session.Grid.SetBlocked(blockedPosition);
            }
            session.MapObjects.Add(new DoorCombatMapObject(
                "door.sandbox.01",
                new GridPosition(4, 4),
                new GridPosition(4, 5),
                DoorInteractionMode.DirectAndLever));
            session.MapObjects.Add(new LeverCombatMapObject("lever.sandbox.01",
                new GridPosition(4, 3), "door.sandbox.01"));

            session.MapObjects.Add(new ObstacleCombatMapObject("obstacle.high_tree.01",
                new GridPosition(3, 4), blocksMovement: true, blocksLineOfSight: true));
            session.MapObjects.Add(new ObstacleCombatMapObject("obstacle.sandbox.02",
                new GridPosition(5, 4), blocksMovement: true, blocksLineOfSight: false));

            var jerzy = CreateUnit(jerzyDefinition, scoutClass, new GridPosition(1, 1),
                100, 0, 200, ResourceType.Momentum, 60f, true);
            CopyDebugConsumablesFromGameSession(jerzy.Character);
            var ally = CreateUnit(allyDefinition, scoutClass, new GridPosition(1, 2),
                100, 0, 200, ResourceType.Momentum, 55f, true);
            var enemyA = CreateUnit(enemyDefinition, warriorClass, new GridPosition(6, 4),
                90, 0, 50, ResourceType.Rage, 48f, false);
            var enemyB = CreateUnit(enemyDefinition, warriorClass, new GridPosition(6, 2),
                90, 0, 50, ResourceType.Rage, 48f, false);
                
            session.Units.Add(jerzy);
            session.Units.Add(ally);
            session.Units.Add(enemyA);
            session.Units.Add(enemyB);

            return session;
        }

        private static void CopyDebugConsumablesFromGameSession(CharacterInstance target)
        {
            var gameSession = GameBootstrap.Instance?.Session;
            var leaderId = gameSession?.PlayerParty?.ActiveLeaderId;
            if (target?.Inventory?.Items == null
                || string.IsNullOrWhiteSpace(leaderId)
                || gameSession?.CharactersById == null
                || !gameSession.CharactersById.TryGetValue(leaderId, out var source)
                || source?.Inventory?.Items == null)
            {
                return;
            }

            // The standalone debug sandbox creates throwaway characters. Keep
            // debug-created consumables usable there by copying the runtime
            // item references from the active game-session leader at startup.
            foreach (var item in source.Inventory.Items)
            {
                if (item is ConsumableItemInstance consumable
                    && consumable.HasCharges
                    && !target.Inventory.Items.Contains(consumable))
                {
                    target.Inventory.Items.Add(consumable);
                }
            }
        }

        public void SyncDebugConsumablesToActiveUnit()
        {
            if (host.IsGameplaySuspended) return;
            if (Session?.IsSeamless == true) return;
            if (Session?.ActiveUnit?.IsPlayerControlled == true)
            {
                CopyDebugConsumablesFromGameSession(Session.ActiveUnit.Character);
            }
        }

        private void RebuildDungeonCombatArea()
        {
            if (Session?.IsSeamless == true || dungeonAreaView == null) return;

            if (!usesGameSessionCombat)
            {
                dungeonAreaView.ClearArea();
                return;
            }

            var context = GameBootstrap.Instance?
                .Session
                .ActiveDungeonCombatContext;

            if (context == null) return;

            dungeonAreaView.Rebuild(context, host);

            if (dungeonAreaView.TryGetWorldBounds(out var worldBounds))
            {
                cameraFramer?.Frame(worldBounds);
            }
        }

        private BattleUnit CreateUnit(
            CharacterDefinition definition,
            CharacterClassData classData,
            GridPosition position,
            int health,
            int resourceCurrent,
            int resourceMax,
            ResourceType resourceType,
            float speed,
            bool isPlayerSide)
        {
            var instance = characterInstanceFactory.Create(definition, classData);
            instance.CurrentHealth = health;
            instance.WeaponAccuracy = 100f;
            instance.Stats.Speed = new Core.Types.Stat(speed);
            instance.Stats.MaxHealth = new Core.Types.Stat(health);
            instance.Stats.MinAttack = new Core.Types.Stat(8);
            instance.Stats.MaxAttack = new Core.Types.Stat(12);
            instance.Stats.PhysicalDefense = new Core.Types.Stat(10);
            instance.Stats.MagicalDefense = new Core.Types.Stat(10);
            instance.Stats.Dodge = new Core.Types.Stat(15);
            instance.Stats.CritChance = new Core.Types.Stat(0.25f);
            instance.Stats.CritDamage = new Core.Types.Stat(1.5f);
            instance.Stats.ResourceRegeneration = new Core.Types.Stat(0);
            instance.ResourcePool = new ResourcePool(resourceType, resourceCurrent, resourceMax, 0);
            var maxAp = turnSystem.CalculateMaxActionPoints(instance, GameDefinesRuntime.GetFloat(
                GameDefineIds.ActionPointsPerSpeed, .25f));

            return new BattleUnit
            {
                Character = instance,
                Position = position,
                CurrentActionPoints = maxAp,
                MaxActionPoints = maxAp,
                CurrentActionValue = 0f,
                HasReacted = false,
                IsReadyToEscape = false,
                IsPlayerSide = isPlayerSide
            };
        }
        // Debug Commands
        private void RestartCombatForDebug()
        {
            if (usesGameSessionCombat)
            {
                Debug.LogWarning("Restart debugowy jest niedostepny dla walki uruchomionej z eksploracji.");
                return;
            }

            passiveEffectCombatExecutionSystem?.Dispose();
            passiveEffectCombatExecutionSystem = null;
            Session = CreateInitialSession();
            Session.PassiveEffectEventDispatcher = passiveEffectEventDispatcher;
            passiveEffectCombatExecutionSystem = new PassiveEffectCombatExecutionSystem(
                Session,
                statusEffectSystem,
                passiveEffectLifecycleSystem,
                AddBattleLog);
            passiveEffectCombatExecutionSystem.ReactionDamageSystem = damageSystem;
            ConfigurePassiveEffectExecutionCallbacks();
            ScoutKnifeSystem.ResetForCombat(Session, Session.IsSeamless);
            hasClearedCombatScopedEffects = false;

            if (!Session.IsSeamless) battleLog?.ClearLog();
            AddBattleLog("Walka rozpoczęta ponownie.");

            turnSystem.InitializeTurnOrder(Session);
        }

        private void ConfigurePassiveEffectExecutionCallbacks()
        {
            if (passiveEffectCombatExecutionSystem == null)
                return;

            passiveEffectCombatExecutionSystem.DuplicateActionExecutor =
                skillExecutionSystem == null
                    ? null
                    : skillExecutionSystem.TryExecutePassiveDuplicate;
            passiveEffectCombatExecutionSystem.RuntimeFlagTriggered -=
                HandleRuntimeFlagTriggered;
            passiveEffectCombatExecutionSystem.RuntimeFlagTriggered +=
                HandleRuntimeFlagTriggered;
            if (skillExecutionSystem != null)
            {
                skillExecutionSystem.PassiveActionResolved =
                    passiveEffectCombatExecutionSystem.NotifyActionResolved;
            }
        }

        private void HandleRuntimeFlagTriggered(
            BattleUnit owner,
            PassiveEffectRuntime runtime,
            PassiveEffectOperationDefinition operation,
            PassiveEffectCombatContext context)
        {
            if (!string.Equals(
                    operation?.ReferenceId,
                    "summon.random-skeleton",
                    System.StringComparison.OrdinalIgnoreCase)
                || owner?.Character == null
                || summonSystem == null)
            {
                return;
            }

            var staff = owner.Character.Equipment.GetEquippedItems().FirstOrDefault(i =>
                i?.Definition is Godmist.Data.Items.UniqueEquipmentDefinition unique && unique.Id == "unique.deadking.staff.mage");
            var definition = staff?.Definition as Godmist.Data.Items.UniqueEquipmentDefinition;
            var pool = definition?.SummonPool?.Where(d => d != null && d.ClassData != null).ToList();
            if (pool == null || pool.Count == 0) { AddBattleLog("Kostur Umarłych: pula szkieletów nie jest jeszcze skonfigurowana."); return; }
            var skeletonDefinition = pool[UnityEngine.Random.Range(0, pool.Count)];

            if (!summonSystem.TrySummonAdjacent(
                    Session,
                    owner,
                    skeletonDefinition,
                    skeletonDefinition.ClassData,
                    "unique.deadking.staff.skeleton",
                    durationTurns: -1,
                    maxActiveSummons: 1,
                    maxHealthPercent: staff.ItemLevel >= 20 ? .28f : .25f,
                    out _, level: owner.Character.Level))
            {
                AddBattleLog("Nie ma wolnego pola na przyzwanie szkieletu.");
            }
        }

        private void RemoveFirstEnemyForDebug()
        {
            if (Session == null)
            {
                return;
            }

            var enemy = Session.Units
                .FirstOrDefault(unit => unit != null && !unit.IsPlayerSide);

            if (enemy == null)
            {
                Debug.LogWarning("No enemy to remove.");
                return;
            }

            RemoveUnitFromCombat(enemy);
            outcomeSystem.EvaluateCombatOutcome(Session);
        }
        private void RemoveActivePlayerUnitForDebug()
        {
            if (Session?.ActiveUnit == null)
            {
                Debug.LogWarning("No active unit to remove.");
                return;
            }

            if (!Session.ActiveUnit.IsPlayerControlled)
            {
                Debug.Log("Debug remove skipped: active unit is not on player side.");
                return;
            }

            var removedUnit = Session.ActiveUnit;
            RemoveUnitFromCombat(removedUnit);
            outcomeSystem.EvaluateCombatOutcome(Session);
        }

        private void RemoveUnitFromCombat(BattleUnit removedUnit)
        {
            if (Session == null || removedUnit == null)
            {
                return;
            }

            var removedWasActive = Session.ActiveUnit == removedUnit;

            if (removedUnit.Character?.LifeState == CharacterLifeState.Dead)
            {
                catacombsBossSystem?.OnBossDied(Session, removedUnit);
                deadKingBossSystem?.OnBossDied(removedUnit);
                skeletonSlayerBossSystem?.OnBossDied(removedUnit);
                banditLeaderBossSystem?.OnBossDied(removedUnit);
                summonSystem?.RemoveSummonsCreatedBy(
                    Session,
                    removedUnit.Character.CharacterId);
                skillExecutionSystem?.CancelScheduledEffectsByCaster(
                    Session,
                    removedUnit.Character.CharacterId);
            }

            Session.Units.Remove(removedUnit);
            Session.TurnParticipants ??= new();
            Session.TurnParticipants?.RemoveAll(
                participant => participant?.Unit == removedUnit);
            Session.TurnOrder = Session.TurnParticipants
                .Where(participant => participant?.Unit != null)
                .Select(participant => participant.Unit)
                .ToList();

            if (Session.TurnParticipants.Count == 0)
            {
                Session.ActiveParticipant = null;
                Session.ActiveUnit = null;
                Session.ActiveTurnIndex = 0;
                return;
            }

            if (removedWasActive)
            {
                Session.ActiveParticipant = null;
                Session.ActiveUnit = null;
                Session.ActiveSummonedObject = null;
                Session.ActiveTurnIndex = -1;
                if (Session.EntryActionCaster == null) turnSystem.AdvanceTurn(Session);
            }
            else
            {
                var activeIndex = Session.TurnOrder.IndexOf(Session.ActiveUnit);

                if (activeIndex >= 0)
                {
                    Session.ActiveTurnIndex = activeIndex;
                }
                else
                {
                    Session.ActiveUnit = Session.TurnOrder.Count > 0
                        ? Session.TurnOrder[0]
                        : null;
                    Session.ActiveTurnIndex = Session.ActiveUnit == null
                        ? -1
                        : 0;
                }

                Session.ActiveParticipant = Session.TurnParticipants
                    .FirstOrDefault(
                        participant => participant?.Unit == Session.ActiveUnit);
            }
        }
        private void AddBattleLog(string message)
        {
            if(Session?.Diagnostics?.Enabled == true) Session.Diagnostics.Record("combat",Session,Session.ActiveUnit,message);
            if (Session?.IsSeamless == true)
                foreach (var unit in Session.Units)
                {
                    if (host.IsUnitKnown(unit)) continue;
                    var name = unit.Character?.Definition?.DisplayName;
                    var id = unit.Character?.CharacterId;
                    if (unit == Session.ActiveUnit || (!string.IsNullOrEmpty(name) && message?.Contains(name) == true)
                        || (!string.IsNullOrEmpty(id) && message?.Contains(id) == true)) return;
                }
            logEntries.Add(message);
            if (logEntries.Count > 300) logEntries.RemoveAt(0);
            if (host.Session == Session) battleLog?.AddEntry(message);
        }
        private void HandleCombatMapObjectChanged(CombatMapObject mapObject)
        {
            CombatMapObjectChanged?.Invoke(mapObject);
        }
        private void ApplyUnitTurnStartEffects(BattleUnit unit)
        {
            Session.Contact?.BeginTurn(Session, unit);
            resourceRuleSystem?.ProcessTurnStart(unit);
            skillExecutionSystem?.ProcessToggleUpkeep(Session, unit);
            deadKingBossSystem?.BeginTurn(unit);
            skeletonSlayerBossSystem?.BeginTurn(unit);
            banditLeaderBossSystem?.BeginTurn(unit);
            var previousDefinition=unit.Character?.Definition;
            forestEntBossSystem?.BeginTurn(unit);
            if(previousDefinition != null && previousDefinition != unit.Character?.Definition
                && forestEntBossSystem?.IsPhaseTwo(unit) == true)
                PresentTransformation(unit,previousDefinition);
            statusEffectSystem?.ProcessTurnStart(Session, unit);
            forestEntBossSystem?.CheckPhases();
            CombatTurnSystem.RefreshMaxActionPoints(
                Session,
                unit,
                refillCurrentActionPoints: true);

        }

        private void HandleUnitTurnBegan(BattleUnit unit)
        {
            if (unit == null || Session == null)
            {
                return;
            }
            ApplyUnitTurnStartEffects(unit);

            if (Session.Outcome != CombatOutcomeState.Ongoing
                || !Session.Units.Contains(unit))
            {
                return;
            }

            if (unit.Character == null
                || unit.Character.LifeState != CharacterLifeState.Alive)
            {
                turnSystem.AdvanceTurn(Session);
                return;
            }
            resourceRuleSystem?.RecordRageOffensiveOpportunity(Session, unit);
            cameraFramer?.NotifyTurnBegan(Session, unit);
            if (TryGetTurnBlockingStatus(unit, out var blockingStatusLabel))
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit,
                    CombatFeedbackType.TurnSkipped,
                    effectName: blockingStatusLabel));
                AddBattleLog(
                    $"{unit.Character.Definition.DisplayName} " +
                    "jest pod wpływem twardej kontroli i traci turę.");

                aiActionInProgress = true;
                activeAiTurnCoroutine = host.StartCoroutine(PresentSkippedTurn(unit));
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            unitViewSpawner?.LogPresentationSnapshot(
                unit,
                "turn-start",
                "turn-start",
                aiActionInProgress,
                activeAiTurnCoroutine != null,
                Session.MembershipChangePending);
#endif
            if (!unit.IsPlayerControlled)
            {
                activeAiTurnCoroutine = host.StartCoroutine(ExecuteAiTurnVisually(unit));
            }
        }

        private static bool TryGetTurnBlockingStatus(
            BattleUnit unit,
            out string statusLabel)
        {
            if (StatusEffectSystem.IsStunned(unit))
            {
                statusLabel = "Ogłuszony";
                return true;
            }

            if (StatusEffectSystem.IsFrozen(unit))
            {
                statusLabel = "Zamrożony";
                return true;
            }

            if (StatusEffectSystem.IsParalyzed(unit))
            {
                statusLabel = "Sparaliżowany";
                return true;
            }

            if (StatusEffectSystem.IsSleeping(unit))
            {
                statusLabel = "Śpiący";
                return true;
            }

            statusLabel = string.Empty;
            return false;
        }

        private IEnumerator ExecuteAiTurnVisually(BattleUnit unit)
        {
            var turnState = aiSystem?.CreateTurnState();
            var stalled = false;
            var deadline = Time.timeAsDouble + SimpleCombatAiActionJob.DecisionTimeoutSeconds;
            while (Session != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit == unit
                && unit?.Character?.LifeState == CharacterLifeState.Alive
                && (unit.CurrentActionPoints > 0
                    || catacombsBossSystem?.CanCastVengeance(Session, unit) == true)
                && turnState != null)
            {
                yield return WaitForAiPresentation(aiDecisionDelay);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (IsAiTurnStillActive(unit) && IsSkillPresentationInProgress)
                    unitViewSpawner?.LogPresentationSnapshot(
                        unit,
                        "ai-state-wait",
                        "skill-presentation",
                        aiActionInProgress,
                        activeAiTurnCoroutine != null,
                        Session.MembershipChangePending,
                        aiJobState: "waiting",
                        waitState: "skill-presentation",
                        skillPresentationEnds: skillPresentationEnds);
#endif
                var waitDeadline = Time.timeAsDouble + SimpleCombatAiActionJob.DecisionTimeoutSeconds;
                while (IsAiTurnStillActive(unit) && IsSkillPresentationInProgress
                    && Time.timeAsDouble < waitDeadline) yield return null;
                if (IsAiTurnStillActive(unit) && IsSkillPresentationInProgress) { stalled = true; break; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (IsAiTurnStillActive(unit) && Session.MembershipChangePending)
                    unitViewSpawner?.LogPresentationSnapshot(
                        unit,
                        "ai-state-wait",
                        "membership",
                        aiActionInProgress,
                        activeAiTurnCoroutine != null,
                        Session.MembershipChangePending,
                        aiJobState: "waiting",
                        waitState: "membership",
                        skillPresentationEnds: skillPresentationEnds);
#endif
                while (IsAiTurnStillActive(unit) && Session.MembershipChangePending
                    && Time.timeAsDouble < deadline) yield return null;
                if (IsAiTurnStillActive(unit) && Session.MembershipChangePending) stalled = true;

                if (stalled || !IsAiTurnStillActive(unit))
                {
                    break;
                }

                var decisionSecondsLeft = deadline - Time.timeAsDouble;
                if (decisionSecondsLeft <= 0) { stalled = true; break; }
                aiActionInProgress = true;
                var thinkingSession = Session;
                using var thinking = aiSystem.CreateNextActionJob(thinkingSession, unit, turnState,
                    () => ReferenceEquals(Session, thinkingSession), timeoutSeconds: decisionSecondsLeft);
                while (!thinking.IsComplete)
                {
                    thinking.SetPaused(host.IsGameplaySuspended);
                    if (host.IsGameplaySuspended) { yield return null; continue; }
                    thinking.AdvanceSlice();
                    if (!thinking.IsComplete) yield return null;
                }
                if (thinking.IsCanceled)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    unitViewSpawner?.LogPresentationSnapshot(
                        unit,
                        "ai-job-canceled",
                        "canceled",
                        aiActionInProgress,
                        activeAiTurnCoroutine != null,
                        Session.MembershipChangePending,
                        aiJobState: "canceled",
                        skillPresentationEnds: skillPresentationEnds);
#endif
                    aiActionInProgress = false;
                    if (thinking.HasTimedOut || Time.timeAsDouble >= deadline)
                    { stalled = true; break; }
                    continue;
                }
                var action = thinking.Action;
                if (!thinking.Executed)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    unitViewSpawner?.LogPresentationSnapshot(
                        unit,
                        "ai-no-action",
                        "no-action",
                        aiActionInProgress,
                        activeAiTurnCoroutine != null,
                        Session.MembershipChangePending,
                        aiJobState: "completed-no-action",
                        skillPresentationEnds: skillPresentationEnds);
#endif
                    break;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                unitViewSpawner?.LogPresentationSnapshot(
                    unit,
                    "action-start",
                    action.Type.ToString(),
                    aiActionInProgress,
                    activeAiTurnCoroutine != null,
                    Session.MembershipChangePending,
                    aiJobState: "executed",
                    skillPresentationEnds: skillPresentationEnds);
#endif

                if (action.Type == SimpleCombatAiActionType.Movement)
                {
                    yield return PlayMovementPath(unit, action.MovementPath);
                }

                var resultDelay = action.Type == SimpleCombatAiActionType.Skill
                    ? aiSkillResultDelay
                    : aiMovementResultDelay;
                yield return WaitForAiPresentation(resultDelay);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (IsAiTurnStillActive(unit) && IsSkillPresentationInProgress)
                    unitViewSpawner?.LogPresentationSnapshot(
                        unit,
                        "ai-state-wait",
                        "skill-presentation",
                        aiActionInProgress,
                        activeAiTurnCoroutine != null,
                        Session.MembershipChangePending,
                        aiJobState: "executed",
                        waitState: "skill-presentation",
                        skillPresentationEnds: skillPresentationEnds);
#endif
                waitDeadline = Time.timeAsDouble + SimpleCombatAiActionJob.DecisionTimeoutSeconds;
                while (IsAiTurnStillActive(unit) && IsSkillPresentationInProgress
                    && Time.timeAsDouble < waitDeadline) yield return null;
                if (IsAiTurnStillActive(unit) && IsSkillPresentationInProgress) { stalled = true; break; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                unitViewSpawner?.LogPresentationSnapshot(
                    unit,
                    "action-post",
                    action.Type.ToString(),
                    aiActionInProgress,
                    activeAiTurnCoroutine != null,
                    Session.MembershipChangePending,
                    aiJobState: "completed",
                    skillPresentationEnds: skillPresentationEnds);
#endif
                aiActionInProgress = false;
                deadline = Time.timeAsDouble + SimpleCombatAiActionJob.DecisionTimeoutSeconds;
            }

            aiActionInProgress = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsAiTurnStillActive(unit) && Session.MembershipChangePending)
                unitViewSpawner?.LogPresentationSnapshot(
                    unit,
                    "ai-state-wait",
                    "membership",
                    aiActionInProgress,
                    activeAiTurnCoroutine != null,
                    Session.MembershipChangePending,
                    aiJobState: "completed",
                    waitState: "membership",
                    skillPresentationEnds: skillPresentationEnds);
#endif
            while (!stalled && IsAiTurnStillActive(unit) && Session.MembershipChangePending
                && Time.timeAsDouble < deadline) yield return null;
            stalled |= IsAiTurnStillActive(unit) && Session.MembershipChangePending;
            if (stalled && IsAiTurnStillActive(unit))
            {
                skillPresentationEnds = 0;
                unitViewSpawner?.ClearCombatPoses(Session);
                AddBattleLog($"{unit.Character.Definition.DisplayName}: przekroczono limit oczekiwania AI (8 s), koniec tury.");
                Debug.LogWarning($"AI_TURN_TIMEOUT: {unit.Character.CharacterId}; ending stalled turn.");
            }
            while (host.IsGameplaySuspended) yield return null;
            activeAiTurnCoroutine = null;
            if (Session != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit == unit)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                unitViewSpawner?.LogPresentationSnapshot(
                    unit,
                    "turn-end-before-advance",
                    "end-turn",
                    aiActionInProgress,
                    true,
                    Session.MembershipChangePending,
                    aiJobState: "completed",
                    skillPresentationEnds: skillPresentationEnds);
#endif
                turnSystem.EndActiveTurn(Session);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                unitViewSpawner?.LogPresentationSnapshot(
                    unit,
                    "turn-end-after-advance",
                    "advanced",
                    aiActionInProgress,
                    activeAiTurnCoroutine != null,
                    Session.MembershipChangePending,
                    aiJobState: "advanced",
                    skillPresentationEnds: skillPresentationEnds);
#endif
            }
        }

        private bool IsAiTurnStillActive(BattleUnit unit)
        {
            return Session != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit == unit
                && unit?.Character?.LifeState == CharacterLifeState.Alive;
        }

        private IEnumerator PresentSkippedTurn(BattleUnit unit)
        {
            var elapsed = 0f;
            while (elapsed < .65f)
            {
                if (!host.IsGameplaySuspended) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            activeAiTurnCoroutine = null;
            aiActionInProgress = false;
            if (Session?.Outcome == CombatOutcomeState.Ongoing && Session.ActiveUnit == unit)
                turnSystem.EndActiveTurn(Session);
        }

        private static IEnumerator WaitForAiPresentation(float duration)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            else
            {
                yield return null;
            }
            while (GameBootstrap.Instance?.Session?.CurrentMode is GameModeState.Menu or GameModeState.Loading)
                yield return null;
        }

        private IEnumerator PlayMovementPath(
            BattleUnit unit,
            IReadOnlyList<GridPosition> path)
        {
            if (unitViewSpawner == null
                || path == null
                || path.Count == 0
                || !unitViewSpawner.TryBeginPresentationMovement(unit))
            {
                yield break;
            }

            var movementSession = Session;
            var followEnemy = unit != null && !unit.IsPlayerSide;
            if (followEnemy) cameraFramer?.BeginMovementFocus(movementSession,
                () => unitViewSpawner.GetPresentationMovementPosition(unit), unit);
            try
            {
                foreach (var position in path)
                {
                    if (Session == null
                        || Session.Outcome != CombatOutcomeState.Ongoing
                        || !unitViewSpawner.TryMovePresentationStep(unit, position)) break;

                    // Inactive hidden views finish their step immediately. Give
                    // the camera a presentation frame without revealing the view.
                    if (followEnemy && !unitViewSpawner.IsPresentationMovementInProgress(unit)) yield return null;
                    while (unitViewSpawner.IsPresentationMovementInProgress(unit)) yield return null;
                }
            }
            finally
            {
                if (followEnemy) cameraFramer?.EndMovementFocus(movementSession);
                unitViewSpawner.EndPresentationMovement(unit);
            }
        }

        private IEnumerator PlayPlayerMovementPath(
            BattleUnit unit,
            IReadOnlyList<GridPosition> path)
        {
            yield return PlayMovementPath(unit, path);
            activePlayerMovementPresentationCoroutine = null;
        }

        private void HandleUnitTurnEnded(BattleUnit unit)
        {
            forestEntBossSystem?.EndTurn(unit);
            statusEffectSystem?.ProcessTurnEnd(unit);
            skillExecutionSystem?.ProcessScheduledEffects(Session, unit);
            summonSystem?.AdvanceDurationAtTurnEnd(Session, unit);
            CombatTurnSystem.RefreshMaxActionPoints(Session, unit);
            Session.Contact?.EndTurn(Session, unit);
        }

        private void HandleUnitDied(BattleUnit unit)
        {
            if (Session == null || unit?.Character == null)
            {
                return;
            }
            catacombsBossSystem?.OnBossDied(Session, unit);
            deadKingBossSystem?.OnBossDied(unit);
            skeletonSlayerBossSystem?.OnBossDied(unit);
            banditLeaderBossSystem?.OnBossDied(unit);

            summonSystem?.RemoveSummonsCreatedBy(
                Session,
                unit.Character.CharacterId);
            skillExecutionSystem?.CancelScheduledEffectsByCaster(
                Session,
                unit.Character.CharacterId);
            skillExecutionSystem?.DeactivateAllToggles(Session, unit);
        }

        private void HandleSummonedObjectTurnBegan(
            SummonedCombatMapObject summonedObject)
        {
            if (summonedObject == null || Session == null)
            {
                return;
            }

            AddBattleLog(
                $"{summonedObject.Definition?.DisplayName ?? summonedObject.ObjectId} " +
                "wykonuje automatyczna ture.");

            if (CatacombsBossSystem.IsRune(summonedObject))
                catacombsBossSystem.ExecuteRuneTurn(Session, summonedObject);
            else
                summonedObjectSystem.ExecuteTurnEffects(Session, summonedObject);

            turnSystem.EndActiveTurn(Session);
        }

        private void HandleSummonedObjectTurnEnded(
            SummonedCombatMapObject summonedObject)
        {
            if (summonedObject == null || Session == null)
            {
                return;
            }

            if (!summonedObject.AdvanceDuration())
            {
                return;
            }

            summonedObjectSystem?.Remove(Session, summonedObject);
        }

        private bool IsActiveUnitHardCrowdControlled()
        {
            return Session?.ActiveUnit != null
                && StatusEffectSystem.IsHardCrowdControlled(Session.ActiveUnit);
        }

        public bool CanEndTurn()
        {
            return host?.IsGameplaySuspended != true && Session != null
                && Session.ActiveUnit != null
                && Session.Outcome == CombatOutcomeState.Ongoing
                && Session.ActiveUnit.IsPlayerControlled
                && !Session.MembershipChangePending && !IsActionInProgress;
        }

        public bool CanEscape() => string.IsNullOrEmpty(GetEscapeBlockReason());

        public string GetEscapeBlockReason()
        {
            if (host?.IsGameplaySuspended == true) return "Gra jest wstrzymana.";
            var actor = Session?.ActiveUnit;
            if (Session == null || Session.Outcome != CombatOutcomeState.Ongoing)
                return "Nie jesteś w walce.";
            if (actor?.IsPlayerControlled != true || !Session.Units.Contains(actor))
                return "Zaczekaj na swoją turę.";
            if (actor.Character?.LifeState != CharacterLifeState.Alive)
                return "Najpierw musisz stanąć na nogi.";
            if (Session.MembershipChangePending || IsActionInProgress)
                return "Zaczekaj, aż skończy się akcja.";
            if (IsActiveUnitHardCrowdControlled())
                return "Nie możesz teraz uciec.";
            if (!Session.IsSeamless) return string.Empty;
            if (!Session.IsDungeonWide)
                return Session.IsExitPosition(actor.Position) ? string.Empty : "Najpierw wydostań się z pola walki.";
            if (Session.Contact == null) return "Nie możesz stąd uciec.";

            // Both hover state and the click recheck current visibility. A cached
            // ready button must never allow escape after contact is reacquired.
            Session.Contact.Refresh(Session);
            foreach (var enemy in Session.Units)
                if (!enemy.IsPlayerSide && enemy.Character?.LifeState == CharacterLifeState.Alive
                    && Session.Contact.Sees(enemy, actor)) return "Wrogowie wciąż cię widzą.";
            return Session.Contact.CanLeave(Session, actor) ? string.Empty : "Wrogowie wciąż cię szukają.";
        }

        public void RequestEscape()
        {
            if (!CanEscape()) return;

            var actor = Session.ActiveUnit;
            if (Session.IsSeamless)
            {
                if (TryLeave(actor)) AddBattleLog($"{actor.Character.Definition?.DisplayName ?? actor.Character.CharacterId} wycofuje się z walki.");
                return;
            }
            if (escapeSystem == null) return;

            if (!escapeSystem.TryEscape(Session, actor)) return;

            if (Session.Outcome == CombatOutcomeState.Ongoing)
            {
                turnSystem.EndActiveTurn(Session);
            }
        }

        public void RequestEndTurn()
        {
            if (!CanEndTurn())
            {
                return;
            }

            turnSystem.EndActiveTurn(Session);
        }

        public bool TryForceOutcome(CombatOutcomeState outcome)
        {
            if (Session == null
                || Session.Outcome != CombatOutcomeState.Ongoing
                || outcome == CombatOutcomeState.Ongoing)
            {
                return false;
            }

            Session.Outcome = outcome;
            return true;
        }

        public Dictionary<GridPosition, List<GridPosition>>
            GetActiveUnitMovementPaths()
        {
            if (Session == null
                || Session.ActiveUnit == null
                || !Session.ActiveUnit.IsPlayerControlled
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return new Dictionary<
                    GridPosition,
                    List<GridPosition>>();
            }

            return movementSystem.CalculateReachablePaths(
                Session,
                Session.ActiveUnit, forPlayerPreview: true);
        }

        public Dictionary<GridPosition, List<GridPosition>>
            GetUnitNextTurnMovementPaths(BattleUnit unit)
        {
            if (Session == null
                || unit == null
                || !Session.Units.Contains(unit)
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return new Dictionary<GridPosition, List<GridPosition>>();
            }

            return movementSystem.CalculateReachablePathsForNextTurn(Session, unit, forPlayerPreview: true);
        }

        public int GetActiveUnitMovementPathCost(IReadOnlyList<GridPosition> path)
        {
            if (path == null || Session?.ActiveUnit == null) return 0;
            return CombatMovementSystem.CalculatePathCost(Session, Session.ActiveUnit.Position,
                Session.ActiveUnit.MovementStepsThisTurn, path, ScoutCombatSystem.MovementDiscount(Session.ActiveUnit), WarriorCombatSystem.FreeSteps(Session.ActiveUnit));
        }

        public bool TryMoveActiveUnitAlongPath(
            IReadOnlyList<GridPosition> path)
        {
            if (host?.IsGameplaySuspended == true || Session == null
                || Session.ActiveUnit == null
                || !Session.ActiveUnit.IsPlayerControlled
                || Session.Outcome != CombatOutcomeState.Ongoing
                || Session.MembershipChangePending || IsActionInProgress)
            {
                return false;
            }

            var unit = Session.ActiveUnit;
            if (!movementSystem.TryMoveAlongPath(
                Session,
                unit,
                path))
            {
                return false;
            }

            activePlayerMovementPresentationCoroutine = host.StartCoroutine(
                PlayPlayerMovementPath(unit, path));
            return true;
        }
        public bool TryExecuteSkill(
            SkillData skill,
            GridPosition? selectedPosition)
        {
            if (skill?.TargetModes == null || skill.TargetModes.Count != 1)
            {
                Debug.LogWarning(
                    $"Skill '{skill?.SkillId}' wymaga wielu celów. " +
                    "Użyj wariantu z selectedPositionsByModeIndex.");

                return false;
            }

            var selectedPositionsByModeIndex =
                new Dictionary<int, GridPosition?>
                {
                    [0] = selectedPosition
                };

            return TryExecuteSkill(skill, selectedPositionsByModeIndex);
        }

        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex)
        {
            return TryExecuteSkill(
                skill,
                selectedPositionsByModeIndex,
                null,
                null);
        }

        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            GridPosition? teleportDestination)
        {
            return TryExecuteSkill(
                skill,
                selectedPositionsByModeIndex,
                teleportDestination,
                null);
        }

        public bool TryExecuteSkill(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            GridPosition? teleportDestination,
            GridPosition? objectPlacementDestination,
            IReadOnlyDictionary<int, int> rotationsByModeIndex = null,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosenPositionsByModeIndex = null)
        {
            if (host?.IsGameplaySuspended == true || Session == null || Session.ActiveUnit == null
                || skillExecutionSystem == null || targetingService == null
                || Session.MembershipChangePending || IsActionInProgress)
            {
                return false;
            }

            if (skill?.IsToggle == true
                && Session.ActiveUnit.ActiveToggleSkillIds != null
                && Session.ActiveUnit.ActiveToggleSkillIds.Contains(skill.SkillId))
            {
                return skillExecutionSystem.TryExecute(
                    Session,
                    Session.ActiveUnit,
                    skill,
                    new Dictionary<int, IReadOnlyList<BattleUnit>>(),
                    null,
                    null,
                    new Dictionary<int, GridPosition?>());
            }

            if (!TryBuildSkillTargetGroups(
                    skill,
                    selectedPositionsByModeIndex,
                    rotationsByModeIndex,
                    out var targetsByModeIndex, chosenPositionsByModeIndex))
            {
                return false;
            }

            return skillExecutionSystem.TryExecute(
                Session,
                Session.ActiveUnit,
                skill,
                targetsByModeIndex,
                teleportDestination,
                objectPlacementDestination,
                selectedPositionsByModeIndex, rotationsByModeIndex: rotationsByModeIndex);
        }

        private bool TryBuildSkillTargetGroups(
            SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex,
            IReadOnlyDictionary<int, int> rotationsByModeIndex,
            out Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex,
            IReadOnlyDictionary<int, IReadOnlyList<GridPosition>> chosenPositionsByModeIndex = null)
        {
            targetsByModeIndex =
                new Dictionary<int, IReadOnlyList<BattleUnit>>();

            if (skill == null || selectedPositionsByModeIndex == null)
            {
                return false;
            }

            if (IsObjectPlacementOnlySkill(skill))
            {
                return true;
            }

            if (skill.TargetModes == null || skill.TargetModes.Count == 0)
            {
                return false;
            }

            for (var targetModeIndex = 0;
                targetModeIndex < skill.TargetModes.Count;
                targetModeIndex++)
            {
                var targetMode = PassiveEffectCombatExecutionSystem.ResolveMode(Session.ActiveUnit, skill, skill.TargetModes[targetModeIndex]);

                if (targetMode == null)
                {
                    return false;
                }

                if (!selectedPositionsByModeIndex.TryGetValue(
                        targetModeIndex,
                        out var selectedPosition))
                {
                    if (targetMode.TargetType != TargetType.Caster)
                    {
                        return false;
                    }

                    selectedPosition = null;
                }

                var targets = GetSkillTargets(
                    skill,
                    targetMode,
                    targetModeIndex,
                    selectedPosition,
                    rotationsByModeIndex != null
                        && rotationsByModeIndex.TryGetValue(targetModeIndex, out var rotation)
                        ? rotation
                        : 0);

                if (targetMode.TargetType == TargetType.MultipleChosen && chosenPositionsByModeIndex != null
                    && chosenPositionsByModeIndex.TryGetValue(targetModeIndex, out var chosen))
                {
                    var resolved = new List<BattleUnit>();
                    foreach (var position in chosen)
                    {
                        var group = GetSkillTargets(skill, targetMode, targetModeIndex, position, 0);
                        if (group.Count != 1 || resolved.Contains(group[0])) return false;
                        resolved.Add(group[0]);
                    }
                    targets = resolved;
                }
                if (targets.Count == 0
                    && !(targetModeIndex == 0 && ScoutCombatSystem.RecoverableKnife(Session,Session.ActiveUnit,skill,selectedPosition)!=null)
                    && !(CombatObjectDamageSystem.SkillDamagesMode(skill, targetModeIndex)
                        && CombatObjectDamageSystem.Targets(Session, Session.ActiveUnit, skill, targetModeIndex, selectedPosition).Count > 0)
                    && targetMode.TargetType != TargetType.AreaAroundTile
                    && targetMode.TargetType != TargetType.AreaAroundCaster)
                {
                    return false;
                }

                targetsByModeIndex.Add(targetModeIndex, targets);
            }

            return true;
        }

        public bool CanInteractWithDoor(string objectId) => Session?.MembershipChangePending != true
            && !IsActionInProgress && Session?.ActiveUnit?.IsPlayerControlled == true
            && Session.Outcome == CombatOutcomeState.Ongoing && !IsActiveUnitHardCrowdControlled()
            && interactionSystem?.CanInteractWithDoor(Session, Session.ActiveUnit, objectId) == true;

        public bool TryInteractWithDoor(string objectId)
        {
            if (!CanInteractWithDoor(objectId)) return false;
            if (!interactionSystem.TryInteractWithDoor(Session, Session.ActiveUnit, objectId)) return false;
            turnSystem.EndActiveTurn(Session);
            return true;
        }

        public bool TryInteractAt(GridPosition position)
        {
            if (Session?.MembershipChangePending == true || IsActionInProgress) return false;
            if (Session == null || Session.ActiveUnit == null || interactionSystem == null 
                || !Session.ActiveUnit.IsPlayerControlled || Session.Outcome != CombatOutcomeState.Ongoing) return false;

            if (IsActiveUnitHardCrowdControlled())
            {
                AddBattleLog("Nie można interagować przez status kontroli.");
                return false;
            }

            if (!interactionSystem.TryInteract(Session, Session.ActiveUnit, position)) return false;

            turnSystem.EndActiveTurn(Session);

            return true;
        }

        public CombatMapObject GetCombatMapObject(string objectId)
        {
            return Session?.GetMapObject(objectId);
        }

        public bool TryReviveAt(GridPosition position)
        {
            if (Session?.MembershipChangePending == true || IsActionInProgress) return false;
            if (Session == null
                || Session.ActiveUnit == null
                || reviveSystem == null
                || !Session.ActiveUnit.IsPlayerControlled
                || Session.Outcome != CombatOutcomeState.Ongoing)
            {
                return false;
            }

            if (IsActiveUnitHardCrowdControlled())
            {
                AddBattleLog("Nie można podnieść sojusznika przez status kontroli.");
                return false;
            }

            var target = Session.Units.FirstOrDefault(
                unit => unit != null
                    && unit.IsPlayerSide
                    && unit.Position == position
                    && unit.Character != null
                    && unit.Character.LifeState
                        == CharacterLifeState.Downed);

            if (target == null)
            {
                return false;
            }

            var revived = reviveSystem.TryRevive(
                Session,
                Session.ActiveUnit,
                target);

            if (!revived)
            {
                return false;
            }

            if (unitViewSpawner?.TryPlayHelpUpPose(Session, Session.ActiveUnit, target, RevivePoseDuration) == true)
                skillPresentationEnds = Mathf.Max(skillPresentationEnds, Time.time + RevivePoseDuration);
            turnSystem.EndActiveTurn(Session);
            return true;
        }
        private void DownFirstAllyForDebug()
        {
            if (Session == null)
            {
                return;
            }

            var ally = Session.Units.FirstOrDefault(
                unit => unit != null
                    && unit.IsPlayerSide
                    && unit != Session.ActiveUnit
                    && unit.Character != null
                    && unit.Character.LifeState
                        == CharacterLifeState.Alive);

            if (ally == null)
            {
                return;
            }

            ally.Character.CurrentHealth = 0;
            ally.Character.LifeState = CharacterLifeState.Downed;
            ally.DownedTurnsRemaining = BattleUnit.DownedTurnLimit;

            AddBattleLog(
                $"{ally.Character.Definition.DisplayName} " +
                "zostaje powalony testowo.");
        }
        public IReadOnlyList<BattleUnit> GetSkillTargets(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex,
            GridPosition? selectedPosition,
            int rotationQuarterTurns = 0)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || targetMode == null
                || targetingService == null)
            {
                return Array.Empty<BattleUnit>();
            }
            if (!ScoutCombatSystem.CanPayHookCost(Session.ActiveUnit,skill)) return Array.Empty<BattleUnit>();

            var includeDownedTargets =
                CanTargetModeIncludeDownedTargets(
                    skill,
                    targetModeIndex);

            var requiredEnemyTarget =
                GetRequiredEnemyTarget(targetMode);

            var targets = targetingService.GetTargets(
                Session,
                Session.ActiveUnit,
                targetMode,
                selectedPosition,
                includeDownedTargets,
                requiredEnemyTarget,
                rotationQuarterTurns);

            if (skill.KnifeCost > 0 && Session.ActiveUnit.AvailableKnives < skill.KnifeCost)
                targets = targets.Where(target => ScoutKnifeSystem.CanTargetPosition(Session.ActiveUnit, skill, target.Position)).ToArray();
            if(targetMode.TargetType==TargetType.Chain)
                return targets.Count>0 && skill.RequiresLineOfSight && !lineOfSightService.HasLineOfSight(Session,Session.ActiveUnit.Position,targets[0].Position)
                    ? Array.Empty<BattleUnit>() : targets;
            return FilterTargetsByLineOfSight(skill, targets);
        }
        public IReadOnlyList<GridPosition> GetSelectableSkillPositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex)
        {
            using var sample = SelectablePositionsSample.Auto();
            if (Session == null || Session.ActiveUnit == null
                || skill == null || targetMode == null
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var includeDownedTargets =
                CanTargetModeIncludeDownedTargets(
                    skill,
                    targetModeIndex);

            var requiredEnemyTarget =
                GetRequiredEnemyTarget(targetMode);

            var positions = targetingService.GetSelectablePositions(
                Session,
                Session.ActiveUnit,
                targetMode,
                includeDownedTargets,
                requiredEnemyTarget);
            if (skill.KnifeCost > 0 && Session.ActiveUnit.AvailableKnives < skill.KnifeCost)
                positions = positions.Where(position => ScoutKnifeSystem.CanTargetPosition(Session.ActiveUnit, skill, position)).ToArray();
            if (!ScoutCombatSystem.CanPayHookCost(Session.ActiveUnit,skill)) positions=Array.Empty<GridPosition>();
            if (requiredEnemyTarget == null && targetModeIndex == 0 && skill.CanRecoverKnives
                && ScoutCombatSystem.CanPayHookCost(Session.ActiveUnit,skill,true))
                positions = positions.Concat(Session.MapObjects.OfType<KnifeCombatMapObject>()
                    .Where(k=>ScoutCombatSystem.RecoverableKnife(Session,Session.ActiveUnit,skill,k.Position)!=null)
                    .Select(k=>k.Position)).Distinct().ToList();

            if (requiredEnemyTarget == null && CombatObjectDamageSystem.SkillDamagesMode(skill, targetModeIndex))
            {
                var expanded = positions.ToList();
                foreach (var obj in Session.MapObjects)
                    if (CombatObjectDamageSystem.CanDamage(obj)
                        && targetMode.TargetType == TargetType.Single
                        && CombatTargetingService.IsPositionWithinRange(Session.ActiveUnit.Position, obj.Position, targetMode.AbsoluteRange)
                        && ScoutKnifeSystem.CanTargetPosition(Session.ActiveUnit, skill, obj.Position)
                        && !expanded.Contains(obj.Position)) expanded.Add(obj.Position);
                if (targetMode.TargetType == TargetType.AreaAroundCaster
                    && CombatTargetingService.IsDirectionalShape(targetMode.AbsoluteRange?.Shape ?? RangeShape.None)) return expanded;
                return FilterPositionsByLineOfSight(skill, expanded, ignoreDamageableObjects: true);
            }

            if (targetMode.TargetType == TargetType.AreaAroundCaster
                && CombatTargetingService.IsDirectionalShape(
                    targetMode.AbsoluteRange?.Shape ?? RangeShape.None))
            {
                return positions;
            }

            return FilterPositionsByLineOfSight(skill, positions);
        }
        public IReadOnlyList<GridPosition> GetAbsoluteSkillRangePositions(SkillData skill, 
            SkillTargetModeData targetMode)
        {
            using var sample = AbsoluteRangeSample.Auto();
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var positions = targetingService.GetAbsoluteRangePositions(Session, Session.ActiveUnit, targetMode);

            return FilterPositionsByLineOfSight(skill, positions);
        }

        public IReadOnlyList<GridPosition> GetRelativeSkillRangePositions(SkillData skill, 
            SkillTargetModeData targetMode, GridPosition center, int rotationQuarterTurns = 0)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null) return Array.Empty<GridPosition>();

            var positions = targetingService.GetRelativeRangePositions(Session, Session.ActiveUnit, 
                targetMode, center, rotationQuarterTurns);

            return FilterPositionsByLineOfSight(skill, positions);
        }

        public IReadOnlyList<GridPosition> GetDirectionalSkillRangePositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            GridPosition directionReference,
            int rotationQuarterTurns = 0)
        {
            if (Session == null || Session.ActiveUnit == null || skill == null
                || targetMode == null || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var positions = targetingService.GetDirectionalRangePositions(
                Session,
                Session.ActiveUnit,
                targetMode.AbsoluteRange,
                directionReference,
                null,
                rotationQuarterTurns);
            return FilterPositionsByLineOfSight(skill, positions);
        }

        public IReadOnlyList<GridPosition> GetTeleportDestinationPositions(
            SkillData skill,
            SkillEffectData teleportEffect,
            GridPosition selectedTargetPosition)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || teleportEffect == null
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var destinationPositions = targetingService.GetPositionsInRange(
                Session,
                selectedTargetPosition,
                teleportEffect.TeleportDestinationRange);

            var validPositions = new List<GridPosition>();

            foreach (var position in destinationPositions)
            {
                if (position == selectedTargetPosition
                    || !Session.Grid.IsWalkable(position)
                    || Session.Grid.IsBlocked(position)
                    || IsOccupiedByLivingUnit(position))
                {
                    continue;
                }

                if (skill.RequiresLineOfSight
                    && !lineOfSightService.HasLineOfSight(
                        Session,
                        Session.ActiveUnit.Position,
                        position))
                {
                    continue;
                }

                if(skill.SkillId=="skill.scout.shadow" && !Session.Grid.CanMoveBetween(selectedTargetPosition,position))continue;
                validPositions.Add(position);
            }

            return validPositions;
        }

        public IReadOnlyList<GridPosition> GetObjectPlacementPositions(
            SkillData skill,
            SkillEffectData createObjectEffect)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || createObjectEffect == null
                || (createObjectEffect.EffectType == EffectType.CreateObject
                    && createObjectEffect.SummonedObjectDefinition == null)
                || (createObjectEffect.EffectType == EffectType.Summon
                    && (createObjectEffect.SummonedCharacterDefinition == null
                        || createObjectEffect.SummonedClassData == null))
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var positions = targetingService.GetPositionsInRange(
                Session,
                Session.ActiveUnit.Position,
                createObjectEffect.EffectType == EffectType.Summon
                    ? createObjectEffect.SummonPlacementRange
                    : createObjectEffect.ObjectPlacementRange);

            var validPositions = new List<GridPosition>();

            foreach (var position in positions)
            {
                if (!Session.Grid.IsWalkable(position)
                    || Session.Grid.IsBlocked(position)
                    || (createObjectEffect.EffectType == EffectType.Summon
                        && Session.IsPositionOccupied(position))
                    || Session.IsPositionOccupiedByMapObject(position, createObjectEffect.SummonedObjectDefinition?.ObjectId==PaladinCombatSystem.FieldId ? Session.SummonedObjects.FirstOrDefault(PaladinCombatSystem.IsField) : null))
                {
                    continue;
                }

                if (skill.RequiresLineOfSight
                    && !lineOfSightService.HasLineOfSight(
                        Session,
                        Session.ActiveUnit.Position,
                        position))
                {
                    continue;
                }

                validPositions.Add(position);
            }

            return validPositions;
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

        private bool IsOccupiedByLivingUnit(GridPosition position)
        {
            foreach (var unit in Session.Units)
            {
                if (unit == null || unit.Character == null
                    || unit.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<GridPosition> GetEligibleSkillTargetPositions(
            SkillData skill,
            SkillTargetModeData targetMode,
            int targetModeIndex)
        {
            if (Session == null || Session.ActiveUnit == null
                || skill == null || targetMode == null
                || targetingService == null)
            {
                return Array.Empty<GridPosition>();
            }

            var includeDownedTargets =
                CanTargetModeIncludeDownedTargets(
                    skill,
                    targetModeIndex);

            var requiredEnemyTarget =
                GetRequiredEnemyTarget(targetMode);

            var positions = targetingService.GetEligibleTargetPositions(
                Session,
                Session.ActiveUnit,
                targetMode,
                includeDownedTargets,
                requiredEnemyTarget);

            if (!ScoutCombatSystem.CanPayHookCost(Session.ActiveUnit,skill)) positions=Array.Empty<GridPosition>();
            if (requiredEnemyTarget == null && targetModeIndex == 0 && skill.CanRecoverKnives
                && ScoutCombatSystem.CanPayHookCost(Session.ActiveUnit,skill,true))
                positions = positions.Concat(Session.MapObjects.OfType<KnifeCombatMapObject>()
                    .Where(k=>ScoutCombatSystem.RecoverableKnife(Session,Session.ActiveUnit,skill,k.Position)!=null)
                    .Select(k=>k.Position)).Distinct().ToList();
            return FilterPositionsByLineOfSight(skill, positions);
        }

        private static bool CanTargetModeIncludeDownedTargets(
            SkillData skill,
            int targetModeIndex)
        {
            if (skill?.Effects == null || targetModeIndex < 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect == null
                    || effect.TargetModeIndex != targetModeIndex)
                {
                    continue;
                }

                if (effect.EffectType == EffectType.PaladinHeal
                    || effect.EffectType == EffectType.Heal
                    || effect.EffectType == EffectType.HealPercent
                    || effect.EffectType == EffectType.Teleport)
                {
                    return true;
                }
            }

            return false;
        }

        private BattleUnit GetRequiredEnemyTarget(
            SkillTargetModeData targetMode)
        {
            if (targetMode?.TargetingType != TargetingType.Enemies)
            {
                return null;
            }

            return StatusEffectSystem.TryGetTaunter(
                Session,
                Session.ActiveUnit,
                out var taunter)
                ? taunter
                : null;
        }

        private IReadOnlyList<BattleUnit> FilterTargetsByLineOfSight(SkillData skill, 
            IReadOnlyList<BattleUnit> targets)
        {
            if (skill == null || !skill.RequiresLineOfSight || Session?.ActiveUnit == null) return targets;

            return targets.Where(target => target != null && 
                lineOfSightService.HasLineOfSight(Session, Session.ActiveUnit.Position, target.Position)).ToList();
        }

        private IReadOnlyList<GridPosition> FilterPositionsByLineOfSight(SkillData skill,
            IReadOnlyList<GridPosition> positions, bool ignoreDamageableObjects = false)
        {
            if (skill == null || !skill.RequiresLineOfSight || Session?.ActiveUnit == null) return positions;

            CombatMapObject IgnoredObject(GridPosition position)
            {
                var obj = Session.GetMapObjectAt(position);
                return CombatObjectDamageSystem.CanDamage(obj) ? obj : null;
            }
            return lineOfSightService.FilterVisiblePositions(Session, Session.ActiveUnit.Position, positions,
                ignoreDamageableObjects ? IgnoredObject : null);
        }

        private CombatSession CreateInitialSession()
        {
            var gameSession = GameBootstrap.Instance?.Session;

            if (gameSession?.ActiveCombat != null)
            {
                usesGameSessionCombat = true;
                return gameSession.ActiveCombat;
            }

            usesGameSessionCombat = false;

            if (scenarioRunner != null
                && scenarioRunner.TryCreateSession(
                    gameSession,
                    GameDefinesRuntime.GetFloat(GameDefineIds.ActionPointsPerSpeed, .25f),
                    out var encounterSession))
            {
                return encounterSession;
            }

            return CreateSandboxSession();
        }

        private static void RestoreDungeonPositionAfterEscape(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (gameSession?.ActiveDungeon == null || combatSession == null) return;

            if (!gameSession.ActiveDungeon.TryGetCurrentFloor(out var floor)) return;

            if (!floor.TryGetEncounter(
                    combatSession.CombatId,
                    out var encounter))
            {
                return;
            }

            if (!encounter.HasRetreatPosition) return;

            gameSession.ActiveDungeon.SetPlayerPosition(
                new Vector2(
                    encounter.RetreatPosition.x,
                    encounter.RetreatPosition.y));
        }

        private void ClearCombatScopedEffectsIfNeeded()
        {
            if (hasClearedCombatScopedEffects
                || Session == null
                || Session.Outcome == CombatOutcomeState.Ongoing)
            {
                return;
            }

            passiveEffectLifecycleSystem?.RemoveCombatScopedEffects(Session);
            hasClearedCombatScopedEffects = true;
        }

        private bool TryReturnToExploration()
        {
            if (hasRequestedExplorationReturn || Session == null || IsSkillPresentationInProgress)
            {
                return false;
            }

            var outcome = Session.Outcome;
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null)
            {
                return false;
            }

            var gameSession = gameBootstrap.Session;

            if (outcome == CombatOutcomeState.Defeat)
            {
                if (Session.IsDebugSession)
                {
                    return TryFinishDebugCombat(
                        gameBootstrap,
                        gameSession);
                }

                var restarted = gameBootstrap.TryRestartAfterDefeat();

                if (!restarted)
                {
                    return false;
                }

                hasRequestedExplorationReturn = true;
                return true;
            }

            if (outcome != CombatOutcomeState.Victory
                && outcome != CombatOutcomeState.Escaped)
            {
                return false;
            }

            if (Session.IsDebugSession)
            {
                return TryFinishDebugCombat(
                    gameBootstrap,
                    gameSession);
            }

            if (!dungeonCombatOutcomeSystem.TryApplyOutcome(
                    gameSession,
                    Session,
                    gameBootstrap?.QuestService == null ? null
                        : new QuestObjectiveProgressSystem(gameSession, gameBootstrap.QuestService)))
            {
                Debug.LogError(
                    "CombatSandboxBootstrap: nie udalo sie zapisac wyniku walki w stanie lochu.");

                return false;
            }

            if (outcome == CombatOutcomeState.Victory)
            {
                rewardSystem.TryGrantVictoryRewards(gameSession, Session);
            }

            if (outcome == CombatOutcomeState.Escaped)
            {
                rewardSystem.TryApplyEscapePenalty(gameSession, Session);
                RestoreDungeonPositionAfterEscape(gameSession, Session);
            }

            if (!gameBootstrap.TryReturnFromCombat())
            {
                return false;
            }

            hasRequestedExplorationReturn = true;
            gameSession.TryClearActiveCombat();

            return true;
        }

        private bool TryFinishDebugCombat(
            GameBootstrap gameBootstrap,
            GameSession gameSession)
        {
            if (!gameBootstrap.TryReturnFromCombat())
            {
                return false;
            }

            hasRequestedExplorationReturn = true;
            gameSession.TryClearActiveCombat();

            return true;
        }
    }
}
