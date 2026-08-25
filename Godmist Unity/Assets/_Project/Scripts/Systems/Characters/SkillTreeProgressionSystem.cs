using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;

namespace Godmist.Systems.Characters
{
    public class SkillTreeProgressionSystem
    {
        public const int MaximumPointsPerTree = 50;

        private readonly CharacterStatPipelineSystem statPipeline;

        public SkillTreeProgressionSystem(CharacterStatPipelineSystem statPipeline = null)
        {
            this.statPipeline = statPipeline ?? new CharacterStatPipelineSystem();
        }

        public bool TryInvest(CharacterInstance character, string treeId, string nodeId)
        {
            if (!string.IsNullOrWhiteSpace(GetInvestmentBlockReason(character, treeId, nodeId)))
            {
                return false;
            }

            var tree = character.ClassData?.SkillTrees?.FirstOrDefault(value => value?.Id == treeId);
            var node = tree?.Nodes?.FirstOrDefault(value => value?.Id == nodeId);
            var progress = GetOrCreateProgress(character, treeId);
            var investment = progress.NodeInvestments.FirstOrDefault(value => value.NodeId == nodeId);

            if (investment == null)
            {
                investment = new SkillTreeNodeInvestment { NodeId = nodeId };
                progress.NodeInvestments.Add(investment);
            }

            investment.Ranks++;
            character.AvailableSkillPoints--;
            statPipeline.Refresh(character);
            return true;
        }

        public bool IsNodeStructurallyUnlocked(CharacterInstance character, string treeId, string nodeId)
        {
            if (character?.ClassData == null || string.IsNullOrWhiteSpace(treeId) || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var tree = character.ClassData.SkillTrees?.FirstOrDefault(value => value?.Id == treeId);
            var node = tree?.Nodes?.FirstOrDefault(value => value?.Id == nodeId);
            if (node == null) return false;

            var progress = character.SkillTreeProgress?.FirstOrDefault(value => value?.TreeId == treeId);
            return GetInvestedRanks(progress) >= GetRequiredRanksForTier(node.Tier)
                && RequirementsMet(progress, node.Requirements);
        }

        public string GetInvestmentBlockReason(
            CharacterInstance character,
            string treeId,
            string nodeId)
        {
            if (character == null) return "Brak postaci.";
            if (character.AvailableSkillPoints <= 0) return "Brak dostępnych punktów umiejętności.";
            if (string.IsNullOrWhiteSpace(treeId) || string.IsNullOrWhiteSpace(nodeId)) return "Nieprawidłowy węzeł.";

            var tree = character.ClassData?.SkillTrees?.FirstOrDefault(value => value?.Id == treeId);
            var node = tree?.Nodes?.FirstOrDefault(value => value?.Id == nodeId);
            if (node == null) return "Nie znaleziono węzła w tym drzewku.";

            var progress = character.SkillTreeProgress?.FirstOrDefault(value => value?.TreeId == treeId);
            var investedRanks = GetInvestedRanks(progress);
            var requiredRanks = GetRequiredRanksForTier(node.Tier);
            if (investedRanks < requiredRanks)
            {
                return $"Wymaga {requiredRanks} pkt wydanych w tym drzewku ({investedRanks}/{requiredRanks}).";
            }

            var investment = progress?.NodeInvestments?.FirstOrDefault(value => value?.NodeId == nodeId);
            if ((investment?.Ranks ?? 0) >= node.EffectiveMaxRanks)
            {
                return "Osiągnięto maksymalną rangę.";
            }

            if (investedRanks >= MaximumPointsPerTree)
            {
                return $"Osiągnięto limit {MaximumPointsPerTree} pkt w drzewku.";
            }

            if (!RequirementsMet(progress, node.Requirements))
            {
                return BuildRequirementsText(tree, progress, node.Requirements);
            }

            return string.Empty;
        }

        public static int GetInvestedRanks(CharacterInstance character, string treeId)
        {
            if (character == null || string.IsNullOrWhiteSpace(treeId)) return 0;

            return GetInvestedRanks(character.SkillTreeProgress?.FirstOrDefault(
                value => value?.TreeId == treeId));
        }

        public static List<StatModifier> GetGlobalPassiveModifiers(
            SkillTreeDefinition tree,
            int investedRanks)
        {
            var result = new List<StatModifier>();
            if (tree?.GlobalPassive?.ModifiersPerPoint == null || investedRanks <= 0)
            {
                return result;
            }

            foreach (var modifier in tree.GlobalPassive.ModifiersPerPoint)
            {
                if (modifier == null) continue;

                result.Add(new StatModifier
                {
                    StatType = modifier.StatType,
                    Operation = modifier.Operation,
                    Stacking = modifier.Stacking,
                    Value = modifier.Value * investedRanks
                });
            }

            return result;
        }

        public static int GetRequiredRanksForTier(int tier)
        {
            if (tier is 1 or 2) return 0;
            if (tier is 3 or 4) return 10;
            if (tier is 5 or 6) return 20;
            if (tier == 7) return 30;
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        private static bool IsTierUnlocked(int tier, int investedRanks)
        {
            return investedRanks >= GetRequiredRanksForTier(tier);
        }

        private static SkillTreeProgress GetOrCreateProgress(CharacterInstance character, string treeId)
        {
            character.SkillTreeProgress ??= new List<SkillTreeProgress>();
            var progress = character.SkillTreeProgress.FirstOrDefault(value => value?.TreeId == treeId);
            if (progress != null) return progress;
            progress = new SkillTreeProgress { TreeId = treeId };
            character.SkillTreeProgress.Add(progress);
            return progress;
        }

        public static int GetInvestedRanks(SkillTreeProgress progress)
        {
            return progress?.NodeInvestments?.Sum(value => Math.Max(0, value.Ranks)) ?? 0;
        }

        private static bool RequirementsMet(SkillTreeProgress progress, List<SkillTreeNodeRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0) return true;
            if (progress?.NodeInvestments == null) return false;
            return requirements.All(requirement => requirement != null
                && progress.NodeInvestments.Any(investment => investment.NodeId == requirement.NodeId
                    && investment.Ranks >= requirement.MinimumRanks));
        }

        private static string BuildRequirementsText(
            SkillTreeDefinition tree,
            SkillTreeProgress progress,
            List<SkillTreeNodeRequirement> requirements)
        {
            var missing = new List<string>();
            foreach (var requirement in requirements ?? new List<SkillTreeNodeRequirement>())
            {
                if (requirement == null || string.IsNullOrWhiteSpace(requirement.NodeId)) continue;
                var node = tree?.Nodes?.FirstOrDefault(value => value?.Id == requirement.NodeId);
                var ranks = progress?.NodeInvestments?.FirstOrDefault(value => value?.NodeId == requirement.NodeId)?.Ranks ?? 0;
                if (ranks < requirement.MinimumRanks)
                {
                    var nodeName = !string.IsNullOrWhiteSpace(node?.DisplayName)
                        ? node.DisplayName
                        : requirement.NodeId;
                    missing.Add($"{nodeName} {ranks}/{requirement.MinimumRanks}");
                }
            }

            return missing.Count == 0
                ? "Nie spełniono wymagań węzła."
                : $"Wymaga: {string.Join(", ", missing)}.";
        }
    }
}
