using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Quests;

namespace Godmist.Runtime.Quests
{
    public class QuestInstance
    {
        private readonly Dictionary<string, QuestObjectiveProgress>
            progressByObjectiveId = new();

        private readonly Dictionary<string, string> questFlags = new();
        private readonly HashSet<string> chosenBranchIds = new();
        private readonly HashSet<string> claimedRewardKeys = new();

        public QuestDefinition Definition { get; }
        public string QuestId { get; }
        public QuestStatus Status { get; private set; }
        public string CurrentStageId { get; private set; }
        public bool IsHidden { get; private set; }

        public IReadOnlyDictionary<string, QuestObjectiveProgress>
            ProgressByObjectiveId => progressByObjectiveId;

        public IReadOnlyDictionary<string, string> QuestFlags => questFlags;
        public IReadOnlyCollection<string> ChosenBranchIds => chosenBranchIds;
        public IReadOnlyCollection<string> ClaimedRewardKeys => claimedRewardKeys;

        public QuestStageDefinition CurrentStage =>
            Definition?.FindStage(CurrentStageId);

        public QuestInstance(
            QuestDefinition definition,
            QuestStatus initialStatus = QuestStatus.Available)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException(
                    "QuestDefinition musi miec niepuste Id.",
                    nameof(definition));
            }

            if (definition.FindStage(definition.StartStageId) == null)
            {
                throw new ArgumentException(
                    $"Quest '{definition.Id}' nie ma poprawnego StartStageId.",
                    nameof(definition));
            }

            Definition = definition;
            QuestId = definition.Id;
            Status = initialStatus;
            CurrentStageId = definition.StartStageId;

            InitializeObjectiveProgress();
        }

        public bool TrySetStatus(QuestStatus nextStatus)
        {
            if (Status == nextStatus)
            {
                return false;
            }

            Status = nextStatus;
            return true;
        }

        public bool TrySetHidden(bool isHidden)
        {
            if (IsHidden == isHidden)
            {
                return false;
            }

            IsHidden = isHidden;
            return true;
        }

        public bool TrySetQuestFlag(string flagId, string value)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            if (questFlags.TryGetValue(flagId, out var currentValue)
                && currentValue == value)
            {
                return false;
            }

            questFlags[flagId] = value ?? string.Empty;
            return true;
        }

        public bool TryRecordBranch(string branchId)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                return false;
            }

            return chosenBranchIds.Add(branchId);
        }

        public bool HasChosenBranch(string branchId)
        {
            return !string.IsNullOrWhiteSpace(branchId)
                && chosenBranchIds.Contains(branchId);
        }

        public bool TryClaimReward(string rewardKey)
        {
            if (string.IsNullOrWhiteSpace(rewardKey))
            {
                return false;
            }

            return claimedRewardKeys.Add(rewardKey);
        }

        public bool TryGetObjectiveProgress(
            string objectiveId,
            out QuestObjectiveProgress progress)
        {
            progress = null;

            return !string.IsNullOrWhiteSpace(objectiveId)
                && progressByObjectiveId.TryGetValue(
                    objectiveId,
                    out progress);
        }

        public bool TryAddObjectiveProgress(
            string objectiveId,
            int amount = 1)
        {
            if (Status != QuestStatus.Active
                || string.IsNullOrWhiteSpace(objectiveId)
                || CurrentStage == null)
            {
                return false;
            }

            var objective = CurrentStage.FindObjective(objectiveId);

            if (objective == null
                || !progressByObjectiveId.TryGetValue(
                    objectiveId,
                    out var progress))
            {
                return false;
            }

            return progress.TryAddAmount(
                amount,
                Math.Max(1, objective.RequiredAmount));
        }

        public bool IsCurrentStageCompleted()
        {
            if (CurrentStage == null)
            {
                return false;
            }

            foreach (var objective in CurrentStage.Objectives)
            {
                if (objective == null || objective.IsOptional)
                {
                    continue;
                }

                if (!progressByObjectiveId.TryGetValue(
                    objective.ObjectiveId,
                    out var progress)
                    || !progress.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryAdvanceStage(string selectedBranchId = null)
        {
            if (Status != QuestStatus.Active || !IsCurrentStageCompleted())
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedBranchId))
            {
                TryRecordBranch(selectedBranchId);
            }

            var nextTransition = FindAvailableTransition(CurrentStage);

            if (nextTransition == null)
            {
                return false;
            }

            CurrentStageId = nextTransition.NextStageId;
            return true;
        }

        public bool TryRestoreState(
            QuestStatus status,
            string currentStageId,
            bool isHidden,
            IReadOnlyDictionary<string, int> objectiveAmounts,
            IReadOnlyDictionary<string, string> restoredQuestFlags,
            IEnumerable<string> restoredBranchIds,
            IEnumerable<string> restoredClaimedRewardKeys)
        {
            if (!Enum.IsDefined(typeof(QuestStatus), status)
                || Definition.FindStage(currentStageId) == null)
            {
                return false;
            }

            var validatedObjectiveAmounts = new Dictionary<string, int>();

            if (objectiveAmounts != null)
            {
                foreach (var objectiveAmount in objectiveAmounts)
                {
                    if (string.IsNullOrWhiteSpace(objectiveAmount.Key)
                        || objectiveAmount.Value < 0
                        || !progressByObjectiveId.ContainsKey(objectiveAmount.Key))
                    {
                        return false;
                    }

                    var requiredAmount = FindRequiredAmount(objectiveAmount.Key);

                    if (requiredAmount <= 0)
                    {
                        return false;
                    }

                    validatedObjectiveAmounts[objectiveAmount.Key] = Math.Min(
                        objectiveAmount.Value,
                        requiredAmount);
                }
            }

            var validatedFlags = new Dictionary<string, string>();

            if (restoredQuestFlags != null)
            {
                foreach (var flag in restoredQuestFlags)
                {
                    if (string.IsNullOrWhiteSpace(flag.Key))
                    {
                        return false;
                    }

                    validatedFlags[flag.Key] = flag.Value ?? string.Empty;
                }
            }

            if (!TryCreateValidatedIdSet(
                    restoredBranchIds,
                    out var validatedBranchIds)
                || !TryCreateValidatedIdSet(
                    restoredClaimedRewardKeys,
                    out var validatedClaimedRewardKeys))
            {
                return false;
            }

            foreach (var progressPair in progressByObjectiveId)
            {
                var requiredAmount = FindRequiredAmount(progressPair.Key);

                if (requiredAmount <= 0)
                {
                    return false;
                }

                validatedObjectiveAmounts.TryGetValue(
                    progressPair.Key,
                    out var restoredAmount);

                progressPair.Value.TryRestoreState(
                    restoredAmount,
                    requiredAmount);
            }

            questFlags.Clear();

            foreach (var flag in validatedFlags)
            {
                questFlags.Add(flag.Key, flag.Value);
            }

            chosenBranchIds.Clear();

            foreach (var branchId in validatedBranchIds)
            {
                chosenBranchIds.Add(branchId);
            }

            claimedRewardKeys.Clear();

            foreach (var rewardKey in validatedClaimedRewardKeys)
            {
                claimedRewardKeys.Add(rewardKey);
            }

            Status = status;
            CurrentStageId = currentStageId;
            IsHidden = isHidden;

            return true;
        }

        private QuestStageTransitionDefinition FindAvailableTransition(
            QuestStageDefinition stage)
        {
            if (stage?.Transitions == null)
            {
                return null;
            }

            QuestStageTransitionDefinition fallbackTransition = null;

            foreach (var transition in stage.Transitions)
            {
                if (transition == null
                    || string.IsNullOrWhiteSpace(transition.NextStageId)
                    || Definition.FindStage(transition.NextStageId) == null)
                {
                    continue;
                }

                var hasBranchCondition =
                    !string.IsNullOrWhiteSpace(
                        transition.RequiredBranchId);

                var hasFlagCondition =
                    !string.IsNullOrWhiteSpace(
                        transition.RequiredQuestFlagId);

                if (!hasBranchCondition && !hasFlagCondition)
                {
                    fallbackTransition ??= transition;
                    continue;
                }

                if (DoesTransitionMatch(transition))
                {
                    return transition;
                }
            }

            return fallbackTransition;
        }

        private bool DoesTransitionMatch(
            QuestStageTransitionDefinition transition)
        {
            if (!string.IsNullOrWhiteSpace(transition.RequiredBranchId)
                && !HasChosenBranch(transition.RequiredBranchId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                transition.RequiredQuestFlagId))
            {
                return true;
            }

            if (!questFlags.TryGetValue(
                transition.RequiredQuestFlagId,
                out var currentFlagValue))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(
                    transition.RequiredQuestFlagValue)
                || currentFlagValue ==
                    transition.RequiredQuestFlagValue;
        }

        private int FindRequiredAmount(string objectiveId)
        {
            if (Definition.Stages == null)
            {
                return 0;
            }

            foreach (var stage in Definition.Stages)
            {
                var objective = stage?.FindObjective(objectiveId);

                if (objective != null)
                {
                    return Math.Max(1, objective.RequiredAmount);
                }
            }

            return 0;
        }

        private static bool TryCreateValidatedIdSet(
            IEnumerable<string> source,
            out HashSet<string> validatedIds)
        {
            validatedIds = new HashSet<string>();

            if (source == null)
            {
                return true;
            }

            foreach (var id in source)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                validatedIds.Add(id);
            }

            return true;
        }

        private void InitializeObjectiveProgress()
        {
            if (Definition.Stages == null)
            {
                return;
            }

            foreach (var stage in Definition.Stages)
            {
                if (stage?.Objectives == null)
                {
                    continue;
                }

                foreach (var objective in stage.Objectives)
                {
                    if (objective == null
                        || string.IsNullOrWhiteSpace(
                            objective.ObjectiveId))
                    {
                        continue;
                    }

                    if (progressByObjectiveId.ContainsKey(
                        objective.ObjectiveId))
                    {
                        throw new ArgumentException(
                            $"Quest '{QuestId}' ma powtorzone " +
                            $"ObjectiveId: '{objective.ObjectiveId}'.",
                            nameof(Definition));
                    }

                    progressByObjectiveId.Add(
                        objective.ObjectiveId,
                        new QuestObjectiveProgress(
                            objective.ObjectiveId));
                }
            }
        }
    }
}