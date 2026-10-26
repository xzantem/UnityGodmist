using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    // Explicit local request only. Runs inside the existing Editor, without
    // closing it, opening scenes, saving scene state or changing Play Mode.
    [InitializeOnLoad]
    internal static class LiveVfxVerification
    {
        static readonly string PassFiveOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass5-20261015"));
        static readonly string PassSixOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass6-20261015"));
        static readonly string PassSevenOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass7-20261015"));
        static readonly string PassEightOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass8-20261015"));
        static readonly string PassNineOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass9-20261015"));
        static readonly string PassTenOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass10-20261015"));
        static readonly string PassElevenOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass11-20261015"));
        static readonly string PassTwelveOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/target-vfx-pass12-20261015"));
        static readonly string ForestOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/forest-playtest-pass-20261015"));
        static readonly string[] ForestFixtures = { "ForestPlaytestPerformanceTests", "ForestPlaytestPresentationTests", "ForestCanopyPresentationTests", "DungeonLightingCadenceTests", "DungeonEnemyBodyTests", "ForestArtIntegrationTests", "ForestHudPolishTests", "EntArenaDecorationTests", "ForestStaticLightTests", "ForestEnemyNavigationTests", "CombatTargetVfxTests" };
        static readonly string CampingOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/camping-tooltip-pass-20261015"));
        static readonly string[] CampingFixtures = { "CampingTooltipLifecycleTests", "DungeonCampTests", "ItemTooltipPresentationTests", "RichUnitTooltipLifecycleTests", "CharacterCardPresentationTests" };
        static readonly string ItemActionOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/item-default-action-pass-20261015"));
        static readonly string[] ItemActionFixtures = { "ItemDefaultActionTests", "CampingTooltipLifecycleTests", "DungeonCampTests", "ItemTooltipPresentationTests", "EquipmentServiceTests", "TradeServiceTests", "CraftingUpgradingConsumablesAcceptanceTests", "CharacterCardPresentationTests" };
        static readonly string ChestOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/chest-primary-pass-20261015"));
        static readonly string[] ChestFixtures = { "ChestPrimaryInteractionTests", "DungeonEnemyBodyTests", "WorldUnitInteractionTests", "DungeonExplorationPathfinderTests", "DungeonNavigationMeshTests", "DungeonObjectPlacementSystemTests", "LootCollectionServiceTests", "ForestContainerTests" };
        static readonly string ScaleOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/actor-scale-restore-20261015"));
        static readonly string[] ScaleFixtures = { "ForestPlaytestPresentationTests", "CombatVisualLifecycleRegressionTests", "DungeonEnemyBodyTests", "CombatPoseIntegrationTests", "CombatDownedIndicatorTests", "ForestDruidTests", "DungeonAwarenessTests" };
        static readonly string CardModelOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/character-card-model-pass-20261015"));
        static readonly string[] CardModelFixtures = { "CharacterCardPresentationTests", "ItemDefaultActionTests", "CampingTooltipLifecycleTests", "VS22PresentationArtTests", "ForestPlaytestPresentationTests" };
        static readonly string KnifeHudOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/scout-knife-hud-pass-20261015"));
        static readonly string[] KnifeHudFixtures = { "ScoutKnifeHudTests", "ScoutMechanicsTests", "ScoutContentTests", "CombatHudPassTests", "CombatActionFeedbackLayoutTests" };
        static readonly string ProgressionOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/progression-feedback-pass-20261015"));
        static readonly string[] ProgressionFixtures = { "ProgressionFeedbackTests", "CharacterProgressionSystemTests", "CombatRewardSystemTests", "QuestServiceTests", "SkillTreeProgressionSystemTests", "PartyRailPresentationTests", "PartyRailContextMenuTests", "CharacterCardPresentationTests", "CombatActionFeedbackLayoutTests", "SkillTreePresentationTests", "SkillTreePassiveTooltipContentTests" };
        static readonly string KnifeLandingOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/knife-landing-pass-20261015"));
        static readonly string[] KnifeLandingFixtures = { "ScoutKnifeLandingTests", "ScoutKnifeHudTests", "ScoutMechanicsTests", "ScoutContentTests", "SeamlessDungeonCombatTests" };
        static readonly string ControlNamingOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/control-resistance-naming-pass-20261015"));
        static readonly string[] ControlNamingFixtures = { "CombatTooltipGlossaryTests", "ItemTooltipContentTests", "PaladinContentTests" };
        static readonly string KnifeRewardOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/knife-recovery-reward-pass-20261015"));
        static readonly string[] KnifeRewardFixtures = { "ScoutMechanicsTests", "ScoutContentTests", "ScoutKnifeHudTests", "ScoutKnifeLandingTests", "CombatRecoverySystemTests", "SeamlessDungeonCombatTests" };
        static readonly string MageShieldOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/mage-shield-pass-20261015"));
        static readonly string[] MageShieldFixtures = { "MageShieldLifecycleTests", "MageContentTests", "ShieldPoolTests", "CombatRecoverySystemTests", "StatusEffectSystemTests", "GameSessionSaveSnapshotFactoryTests", "GameSessionRestoreFactoryTests", "CombatTooltipGlossaryTests", "ExplorationResourceTests", "CombatStatusPresentationTests" };
        static readonly string EnemyResourceOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/enemy-resources-pass-20261015"));
        static readonly string EnemyBalanceOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/enemy-scaling-pass-20261016"));
        static readonly string SkillPointOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/skill-point-value-20261018"));
        static readonly string FrameTimeOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/dungeon-frame-pass-20261016"));
        static readonly string GenerationOutput = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/dungeon-generation-pass2-20261016"));
        static readonly string[] FrameTimeFixtures = { "DungeonFrameTimeTests", "DungeonRemainingFrameCostTests", "DungeonFogPatrolOptimizationTests", "DungeonVisibilityBudgetTests", "DungeonPatrolTrapLightingPassTests", "ExplorationKeyboardInputTests", "LeaderClickMovementOptimizationTests", "DungeonCounterFrameCaptureTests", "DungeonShadowFogOptimizationTests", "PatrolMaterialOptimizationTests", "MovementSpikeOptimizationTests", "LightFogMapOptimizationTests", "ShadowContactOptimizationTests", "LightSourceMeshOptimizationTests", "FollowerPatrolFogOptimizationTests", "PatrolAnimationPreparationTests", "LightingCoverageFogMeshTests", "PatrolHiddenReceiverOptimizationTests", "PatrolContactMinimapOptimizationTests", "ExplorationConsolidatedOptimizationTests", "ContactShadowCostTests", "EnvironmentPackageReplayTests", "EnvironmentPackageOptimizationTests", "EnvironmentRegressionOptimizationTests", "DungeonMinimapTests", "BehaviourGcDiagnosticsTests", "FullSpikeDiagnosticsTests", "FogCameraLightBudgetTests", "GcHotpathOptimizationTests", "DungeonTacticalTooltipTests", "DestructibleObstacleTests", "CampingTooltipLifecycleTests", "RichUnitTooltipLifecycleTests", "CombatPresentationV4Tests", "DungeonEnemyBodyTests", "CombatDownedIndicatorTests", "CombatCameraControlTests", "DungeonTrapCombatTests", "DungeonTrapAndConditionTests", "ExplorationResourceTests", "DungeonLightingOptimizationTests", "DungeonEntryPerformanceTests", "DungeonNavigationMeshTests", "DungeonSimpleVisualTests", "DungeonAwarenessTests", "DungeonFogTests", "DungeonFogTransitionTests", "DungeonLightingCadenceTests", "DungeonPerformanceRegressionTests", "DungeonPatrolTests", "DungeonPatrolDoorTests", "DungeonSurfaceFoundationTests", "DungeonExplorationPathfinderTests", "DungeonLayoutSnapshotTests", "DungeonCombatStateSyncSystemTests", "CombatLineOfSightServiceTests", "ForestEnemyNavigationTests" };
        static readonly string[] GenerationFixtures = FrameTimeFixtures.Concat(new[] { "ForestLightingStabilityTests", "ForestShadowReadabilityTests", "DungeonRoomContentCompletenessTests", "GameSessionRestoreFactoryTests", "GameSessionSaveSnapshotFactoryTests", "SpriteRenderingAuditTests", "SeamlessDungeonCombatTests", "DungeonFogBlendTests", "DungeonGenerationOptimizationTests", "ForestCanopyPresentationTests", "ForestPlaytestPresentationTests", "ForestArtIntegrationTests", "ForestExpeditionTests", "ForestPrototypeTests", "ForestStaticLightTests", "EntArenaFireTests", "ForestGroundingReadabilityTests", "ForestWholeCanopyFadeTests", "ForestExplorationObjectTests", "MissionBoardGenerationTests", "DungeonOfferTests", "QuestServiceTests", "QuestInstanceTests", "QuestInstanceRestoreTests", "QuestObjectiveProgressSystemTests", "VS17MissionWindowTests", "QuestJournalWindowTests", "WorldObjectPrimaryInteractionTests", "CombatInteractionSystemTests", "ChestPrimaryInteractionTests", "WorldUnitInteractionTests", "DoorDungeonObjectInstanceTests", "DungeonGeneratorServiceTests", "DungeonFloorInstanceTests", "ForestContainerTests", "DungeonTrapCombatTests", "DungeonTrapAndConditionTests", "DungeonObjectPlacementSystemTests" }).ToArray();
        static readonly string[] EnemyResourceFixtures = { "StatusEffectSystemTests", "CombatVariableSystemTests", "VS11EquipmentTests", "JewelryGenerationServiceTests", "DungeonTrapCombatTests", "DungeonTrapAndConditionTests", "StatCalculationRegressionTests", "AlchemyBombBalanceTests", "AlchemyCraftingAndRefillTests", "ItemTooltipPresentationTests", "AlchemyBombBalanceAuditTests", "GlossaryMechanicsTests", "ManaEconomyTests", "CombatTooltipGlossaryTests", "SkillTreePresentationTests", "SkillTreePassiveTooltipContentTests", "PaladinContentTests", "CombatRecoverySystemTests", "CombatTurnSystemTests", "DownedStatusExpiryTests", "PassiveEffectLifecycleSystemTests", "ExplorationResourceTests", "MageShieldLifecycleTests", "EquipmentProgressionBalanceTests", "GalduryteExtractionServiceTests", "GalduryteExtractionPrefabTests", "CraftingUpgradingConsumablesAcceptanceTests", "EquipmentGenerationServiceTests", "EquipmentServiceTests", "CharacterStatPipelineSystemTests", "EnemyBattleSimulationTests", "EnemyScalingBalanceTests", "CharacterLevelGrowthSystemTests", "MageContentTests", "WarriorContentTests", "CombatDamageSystemTests", "CombatTurnSystemTimelinePreviewTests", "CombatSkillPreviewCalculatorTests", "SkillHighlightRangeTests", "ContextualSkillHitTests", "ScheduledCombatEffectTests", "EnemyResourceProfileTests", "SeamlessDungeonCombatTests", "ScoutContentTests", "ResourceRuleSystemTests", "SimpleCombatAiSystemTests", "VS15CatacombsContentTests", "ForestWolfTests", "ForestBanditTests", "ForestShooterTests", "ForestArcherTests", "ForestPoacherTests", "ForestTreeTests", "ForestDruidTests", "ForestEntBossTests", "SkeletonSlayerBossTests", "BanditLeaderBossTests", "DeadKingV2Tests", "GameSessionRestoreFactoryTests", "GameSessionSaveSnapshotFactoryTests" };
        static readonly string RefreshRequest = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/unity-refresh.request"));
        static string Output = PassFiveOutput;
        static readonly string[] Fixtures = { "VS22ItemArtTests", "CombatTargetVfxTests", "CombatTargetVfxPass5Tests", "PaladinContentTests", "ContextualSkillHitTests", "CombatPoseIntegrationTests", "WarriorContentTests", "ScoutContentTests", "MageContentTests", "VS15CatacombsContentTests", "DestructibleObstacleTests", "ForestDruidTests", "ForestPoacherTests", "CatacombsBossTests", "DeadKingV2Tests", "SkeletonSlayerBossTests", "BanditLeaderBossTests", "ForestEntBossTests", "BanditLeaderArtIntegrationTests", "EntBossPresentationTests" };
        static readonly string ManaBalanceRequest = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/mana-package-20261021/apply-content.request"));
        static readonly string GlossaryRefreshRequest = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/glossary-ai-completion-20261021/apply-glossary.request"));
        static readonly string PolishOutput = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/dungeon-combat-polish-20261025"));
        static readonly string EntryActionsOutput = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/outsider-combat-actions-20261025"));
        static string[] requestedTestPrefixes;
        static TestRunnerApi api;
        static double nextCheck;
        static LiveVfxVerification() => EditorApplication.update += Tick;

        [MenuItem("Godmist/Verification/VFX pass5 (keep Editor open)")]
        static void RequestRun()
        { Directory.CreateDirectory(PassFiveOutput); File.WriteAllText(Path.Combine(PassFiveOutput, "run-live-tests.request"), "Explicit VFX pass5 verification"); }

        [MenuItem("Godmist/Verification/VFX pass6 (keep Editor open)")]
        static void RequestPassSix()
        { Directory.CreateDirectory(PassSixOutput); File.WriteAllText(Path.Combine(PassSixOutput, "run-live-tests.request"), "Explicit VFX pass6 verification"); }

        [MenuItem("Godmist/Verification/VFX pass7 (keep Editor open)")]
        static void RequestPassSeven()
        { Directory.CreateDirectory(PassSevenOutput); File.WriteAllText(Path.Combine(PassSevenOutput, "run-live-tests.request"), "Explicit VFX pass7 verification"); }

        [MenuItem("Godmist/Verification/VFX pass8 (keep Editor open)")]
        static void RequestPassEight()
        { Directory.CreateDirectory(PassEightOutput); File.WriteAllText(Path.Combine(PassEightOutput, "run-live-tests.request"), "Explicit VFX pass8 verification"); }

        [MenuItem("Godmist/Verification/VFX pass9 (keep Editor open)")]
        static void RequestPassNine()
        { Directory.CreateDirectory(PassNineOutput); File.WriteAllText(Path.Combine(PassNineOutput, "run-live-tests.request"), "Explicit VFX pass9 verification"); }

        [MenuItem("Godmist/Verification/VFX pass10 (keep Editor open)")]
        static void RequestPassTen()
        { Directory.CreateDirectory(PassTenOutput); File.WriteAllText(Path.Combine(PassTenOutput, "run-live-tests.request"), "Explicit VFX pass10 verification"); }

        [MenuItem("Godmist/Verification/VFX pass11 (keep Editor open)")]
        static void RequestPassEleven()
        { Directory.CreateDirectory(PassElevenOutput); File.WriteAllText(Path.Combine(PassElevenOutput, "run-live-tests.request"), "Explicit VFX pass11 verification"); }

        [MenuItem("Godmist/Verification/VFX pass12 (keep Editor open)")]
        static void RequestPassTwelve()
        { Directory.CreateDirectory(PassTwelveOutput); File.WriteAllText(Path.Combine(PassTwelveOutput, "run-live-tests.request"), "Explicit VFX pass12 verification"); }

        [MenuItem("Godmist/Verification/Forest playtest pass (keep Editor open)")]
        static void RequestForest()
        { Directory.CreateDirectory(ForestOutput); File.WriteAllText(Path.Combine(ForestOutput, "run-live-tests.request"), "Explicit forest verification"); }

        [MenuItem("Godmist/Verification/Camping tooltip pass (keep Editor open)")]
        static void RequestCamping()
        { Directory.CreateDirectory(CampingOutput); File.WriteAllText(Path.Combine(CampingOutput, "run-live-tests.request"), "Explicit camping tooltip verification"); }

        [MenuItem("Godmist/Verification/Item default action pass (keep Editor open)")]
        static void RequestItemAction()
        { Directory.CreateDirectory(ItemActionOutput); File.WriteAllText(Path.Combine(ItemActionOutput, "run-live-tests.request"), "Explicit item default action verification"); }

        [MenuItem("Godmist/Verification/Chest primary pass (keep Editor open)")]
        static void RequestChest()
        { Directory.CreateDirectory(ChestOutput); File.WriteAllText(Path.Combine(ChestOutput, "run-live-tests.request"), "Explicit chest primary verification"); }

        [MenuItem("Godmist/Verification/Actor scale restoration (keep Editor open)")]
        static void RequestScale()
        { Directory.CreateDirectory(ScaleOutput); File.WriteAllText(Path.Combine(ScaleOutput, "run-live-tests.request"), "Explicit actor scale restoration verification"); }

        [MenuItem("Godmist/Verification/Character card models (keep Editor open)")]
        static void RequestCardModels()
        { Directory.CreateDirectory(CardModelOutput); File.WriteAllText(Path.Combine(CardModelOutput, "run-live-tests.request"), "filter:Godmist.Tests.EditMode.CharacterCardPresentationTests;Godmist.Tests.EditMode.ItemDefaultActionTests;Godmist.Tests.EditMode.CampingTooltipLifecycleTests;Godmist.Tests.EditMode.VS22PresentationArtTests.CharacterInfoWindowUsesCurrentFrontIdleWithPreservedAspect;Godmist.Tests.EditMode.ForestPlaytestPresentationTests"); }

        [MenuItem("Godmist/Verification/Scout knife HUD (keep Editor open)")]
        static void RequestKnifeHud()
        { Directory.CreateDirectory(KnifeHudOutput); File.WriteAllText(Path.Combine(KnifeHudOutput, "run-live-tests.request"), "Explicit scout knife HUD verification"); }

        [MenuItem("Godmist/Verification/Progression feedback (keep Editor open)")]
        static void RequestProgression()
        { Directory.CreateDirectory(ProgressionOutput); File.WriteAllText(Path.Combine(ProgressionOutput, "run-live-tests.request"), "Explicit progression feedback verification"); }

        [MenuItem("Godmist/Verification/Knife landing (keep Editor open)")]
        static void RequestKnifeLanding()
        { Directory.CreateDirectory(KnifeLandingOutput); File.WriteAllText(Path.Combine(KnifeLandingOutput, "run-live-tests.request"), "Explicit knife landing verification"); }

        [MenuItem("Godmist/Verification/Control resistance wording (keep Editor open)")]
        static void RequestControlNaming()
        { Directory.CreateDirectory(ControlNamingOutput); File.WriteAllText(Path.Combine(ControlNamingOutput, "run-live-tests.request"), "Explicit control resistance wording verification"); }

        [MenuItem("Godmist/Verification/Knife recovery reward (keep Editor open)")]
        static void RequestKnifeReward()
        { Directory.CreateDirectory(KnifeRewardOutput); File.WriteAllText(Path.Combine(KnifeRewardOutput, "run-live-tests.request"), "Explicit knife recovery reward verification"); }

        [MenuItem("Godmist/Verification/Mage shield playtest pass (keep Editor open)")]
        static void RequestMageShield()
        { Directory.CreateDirectory(MageShieldOutput); File.WriteAllText(Path.Combine(MageShieldOutput, "run-live-tests.request"), "Explicit Mage shield verification"); }

        [MenuItem("Godmist/Verification/Enemy resource profiles (keep Editor open)")]
        static void RequestEnemyResources()
        { Directory.CreateDirectory(EnemyResourceOutput); File.WriteAllText(Path.Combine(EnemyResourceOutput, "run-live-tests.request"), "Explicit enemy resource verification"); }

        [MenuItem("Godmist/Verification/Enemy scaling balance (keep Editor open)")]
        static void RequestEnemyBalance()
        { Directory.CreateDirectory(EnemyBalanceOutput); File.WriteAllText(Path.Combine(EnemyBalanceOutput, "run-live-tests.request"), "Explicit enemy scaling balance verification"); }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            var diagnostic = Path.ChangeExtension(RefreshRequest, ".diagnostic.request");
            if (File.Exists(diagnostic))
            {
                File.Delete(diagnostic);
                File.WriteAllText(Path.ChangeExtension(diagnostic, ".txt"),
                    $"playing={EditorApplication.isPlayingOrWillChangePlaymode}; paused={EditorApplication.isPaused}; compiling={EditorApplication.isCompiling}; updating={EditorApplication.isUpdating}; testApi={api != null}; utc={DateTime.UtcNow:O}");
            }
            if (api != null || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // Explicit local requests only. No preference changes, scene saves or
            // second process: import pending files before discovering new tests.
            if (File.Exists(RefreshRequest))
            {
                File.Delete(RefreshRequest);
                AssetDatabase.Refresh();
                File.WriteAllText(Path.ChangeExtension(RefreshRequest, ".state.txt"), "Explicit Assets Refresh completed: " + DateTime.UtcNow.ToString("O"));
                return;
            }
            if (File.Exists(GlossaryRefreshRequest))
            {
                if (EditorUtility.scriptCompilationFailed) return;
                Godmist.Editor.GlossaryMechanicsSetup.Apply();
                File.Delete(GlossaryRefreshRequest);
                File.WriteAllText(Path.ChangeExtension(GlossaryRefreshRequest, ".state.txt"), DateTime.UtcNow.ToString("O"));
                return;
            }
            if (File.Exists(ManaBalanceRequest))
            {
                if (EditorUtility.scriptCompilationFailed) return;
                VS09MageSetup.ApplyManaEconomyBalance();
                File.Delete(ManaBalanceRequest);
                File.WriteAllText(Path.ChangeExtension(ManaBalanceRequest, ".state.txt"), "Mana content applied: " + DateTime.UtcNow.ToString("O"));
                return;
            }
            Output = File.Exists(Path.Combine(EntryActionsOutput, "run-live-tests.request")) ? EntryActionsOutput
                : File.Exists(Path.Combine(PolishOutput, "run-live-tests.request")) ? PolishOutput
                : File.Exists(Path.Combine(SkillPointOutput, "run-live-tests.request")) ? SkillPointOutput
                : File.Exists(Path.Combine(GenerationOutput, "run-live-tests.request")) ? GenerationOutput
                : File.Exists(Path.Combine(FrameTimeOutput, "run-live-tests.request")) ? FrameTimeOutput
                : File.Exists(Path.Combine(EnemyBalanceOutput, "run-live-tests.request")) ? EnemyBalanceOutput
                : File.Exists(Path.Combine(EnemyResourceOutput, "run-live-tests.request")) ? EnemyResourceOutput
                : File.Exists(Path.Combine(MageShieldOutput, "run-live-tests.request")) ? MageShieldOutput
                : File.Exists(Path.Combine(KnifeRewardOutput, "run-live-tests.request")) ? KnifeRewardOutput
                : File.Exists(Path.Combine(ControlNamingOutput, "run-live-tests.request")) ? ControlNamingOutput
                : File.Exists(Path.Combine(KnifeLandingOutput, "run-live-tests.request")) ? KnifeLandingOutput
                : File.Exists(Path.Combine(ProgressionOutput, "run-live-tests.request")) ? ProgressionOutput
                : File.Exists(Path.Combine(KnifeHudOutput, "run-live-tests.request")) ? KnifeHudOutput
                : File.Exists(Path.Combine(CardModelOutput, "run-live-tests.request")) ? CardModelOutput
                : File.Exists(Path.Combine(ScaleOutput, "run-live-tests.request")) ? ScaleOutput
                : File.Exists(Path.Combine(ChestOutput, "run-live-tests.request")) ? ChestOutput
                : File.Exists(Path.Combine(ItemActionOutput, "run-live-tests.request")) ? ItemActionOutput
                : File.Exists(Path.Combine(CampingOutput, "run-live-tests.request")) ? CampingOutput
                : File.Exists(Path.Combine(ForestOutput, "run-live-tests.request")) ? ForestOutput
                : File.Exists(Path.Combine(PassTwelveOutput, "run-live-tests.request")) ? PassTwelveOutput
                : File.Exists(Path.Combine(PassElevenOutput, "run-live-tests.request")) ? PassElevenOutput
                : File.Exists(Path.Combine(PassTenOutput, "run-live-tests.request")) ? PassTenOutput
                : File.Exists(Path.Combine(PassNineOutput, "run-live-tests.request")) ? PassNineOutput
                : File.Exists(Path.Combine(PassEightOutput, "run-live-tests.request")) ? PassEightOutput
                : File.Exists(Path.Combine(PassSevenOutput, "run-live-tests.request")) ? PassSevenOutput
                : File.Exists(Path.Combine(PassSixOutput, "run-live-tests.request")) ? PassSixOutput : PassFiveOutput;
            var request = Path.Combine(Output, "run-live-tests.request");
            if (!File.Exists(request)) return;
            if (EditorUtility.scriptCompilationFailed)
            {
                // Do not silently run old test DLLs after a failed compilation.
                // Keep the request for the next successful automatic import.
                var state = Path.Combine(Output, "live-state.txt");
                const string message = "BLOCKED: script compilation failed; test request retained.";
                if (!File.Exists(state) || File.ReadAllText(state) != message) File.WriteAllText(state, message);
                return;
            }
            Directory.CreateDirectory(Output);
            var requestText = File.ReadAllText(request).Trim();
            var catalogOnly = requestText == "catalog-only";
            requestedTestPrefixes = requestText.StartsWith("filter:", StringComparison.Ordinal)
                ? requestText.Substring(7).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) : null;
            File.Delete(request);
            File.WriteAllText(Path.Combine(Output, "live-state.txt"), "Retrieving EditMode tests; Editor stays open.");
            try
            {
                if (Output == ControlNamingOutput) VS09PaladinSetup.RefreshControlResistanceWording();
                if (Output == MageShieldOutput) VS09MageSetup.ApplyShieldPlaytestBalance();
                if (Output == EnemyResourceOutput) Godmist.Editor.EnemyResourceProfilesSetup.ApplyAll();
                if (Output == EnemyBalanceOutput) Godmist.Editor.EnemyScalingBalanceSetup.ApplyAll();
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(new Callbacks(Output));
                // Explicit known test names bypass a stuck asynchronous catalog
                // lookup. Execution still uses the normal Unity test runner.
                var asynchronousExact = requestText.StartsWith("async-exact:", StringComparison.Ordinal);
                if (asynchronousExact || requestText.StartsWith("exact:", StringComparison.Ordinal))
                {
                    var exact = requestText.Substring(asynchronousExact ? 12 : 6).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (exact.Length == 0 || exact.Any(name => !name.StartsWith("Godmist.Tests.", StringComparison.Ordinal)))
                        throw new InvalidOperationException("Expected explicit Godmist test names.");
                    File.WriteAllLines(Path.Combine(Output, "live-selected-tests.txt"), exact);
                    api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = exact })
                        { runSynchronously = !asynchronousExact });
                    return;
                }
                api.RetrieveTestList(TestMode.EditMode, tree =>
                {
                    try
                    {
                        var names = new List<string>(); Collect(tree, names);
                        if (catalogOnly) names.RemoveAll(n => !n.Contains(".PassFiveSeparatesPersistentAreaLoopsFromExecutionFreezes("));
                        if (names.Count == 0) throw new InvalidOperationException("No focused VFX tests discovered.");
                        File.WriteAllLines(Path.Combine(Output, "live-selected-tests.txt"), names);
                        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() })
                            { runSynchronously = true });
                    }
                    catch (Exception error)
                    { File.WriteAllText(Path.Combine(Output, "live-state.txt"), "ERROR: " + error); Release(); }
                });
            }
            catch (Exception error)
            { File.WriteAllText(Path.Combine(Output, "live-state.txt"), "ERROR: " + error); Release(); }
        }

        static void Collect(ITestAdaptor test, List<string> names)
        {
            if (test.IsSuite) { foreach (var child in test.Children) Collect(child, names); return; }
            if (!(Output == GenerationOutput ? GenerationFixtures : Output == FrameTimeOutput ? FrameTimeFixtures : Output == EnemyResourceOutput || Output == EnemyBalanceOutput || Output == SkillPointOutput ? EnemyResourceFixtures : Output == MageShieldOutput ? MageShieldFixtures : Output == KnifeRewardOutput ? KnifeRewardFixtures : Output == ControlNamingOutput ? ControlNamingFixtures : Output == KnifeLandingOutput ? KnifeLandingFixtures : Output == ProgressionOutput ? ProgressionFixtures : Output == KnifeHudOutput ? KnifeHudFixtures : Output == CardModelOutput ? CardModelFixtures : Output == ScaleOutput ? ScaleFixtures : Output == ChestOutput ? ChestFixtures : Output == ItemActionOutput ? ItemActionFixtures : Output == CampingOutput ? CampingFixtures : Output == ForestOutput ? ForestFixtures : Fixtures).Any(f => test.FullName.StartsWith("Godmist.Tests.EditMode." + f + ".", StringComparison.Ordinal)
                || test.FullName.StartsWith("Godmist.Tests.EditModeTests." + f + ".", StringComparison.Ordinal))) return;
            if (requestedTestPrefixes != null && !requestedTestPrefixes.Any(prefix =>
                test.FullName.StartsWith(prefix, StringComparison.Ordinal))) return;
            // The bounded balance matrix is deliberately opt-in; a normal
            // regression request must not silently rerun calibration batches.
            if (requestedTestPrefixes == null && (test.FullName.Contains(".ContactShadowCostTests.CompareProductionForestReplay") || test.FullName.Contains(".EnvironmentPackageReplayTests.ReplaySameProductionSequence") || test.FullName.Contains(".EnvironmentPackageReplayTests.ReplayDetailedFogSequence"))) return;
            if (requestedTestPrefixes == null && test.FullName.Contains(".EnemyBattleSimulationTests.CompareFullBattles(")) return;
            if (requestedTestPrefixes == null && test.FullName.Contains(".EnemyBattleSimulationTests.CompareExpeditionAttrition")) return;
            if (requestedTestPrefixes == null && (test.FullName.Contains(".EnemyBattleSimulationTests.Audit")
                || test.FullName.Contains(".EnemyBattleSimulationTests.SkillPointProjectionAgreesWithProductionFirstContact"))) return;
            // Coroutine tests can enter Play Mode and replace the user's scene.
            // Keep this live path strictly synchronous EditMode verification.
            if (test.Method?.MethodInfo.GetCustomAttributes(false).Any(a => a.GetType().Name == "UnityTestAttribute") == true) return;
            names.Add(test.FullName);
        }

        static void Release()
        { if (api != null) UnityEngine.Object.DestroyImmediate(api); api = null; }
        sealed class Callbacks : ICallbacks
        {
            private readonly string output;
            public Callbacks(string output) { this.output = output; }
            public void RunStarted(ITestAdaptor testsToRun) => File.WriteAllText(Path.Combine(output, "live-state.txt"), "RUNNING: " + testsToRun.TestCaseCount);
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, Path.Combine(output, "live-tests.xml"));
                File.WriteAllText(Path.Combine(output, "live-state.txt"), "FINISHED: " + result.TestStatus);
                EditorApplication.delayCall += Release;
            }
        }
    }
}


