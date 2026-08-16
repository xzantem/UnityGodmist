using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;

namespace Godmist.Systems.Characters
{
    public class SkillTreeProgressionSystem
    {
        public const int MaximumPointsPerTree = 50;

        public bool TryInvest(CharacterInstance character, string treeId, string nodeId)
        {
            if (character == null || character.AvailableSkillPoints <= 0
                || string.IsNullOrWhiteSpace(treeId) || string.IsNullOrWhiteSpace(nodeId)) return false;

            var tree = character.ClassData?.SkillTrees?.FirstOrDefault(value => value?.Id == treeId);
            var node = tree?.Nodes?.FirstOrDefault(value => value?.Id == nodeId);
            if (node == null || !IsTierUnlocked(node.Tier, GetInvestedRanks(character, treeId))) return false;

            var progress = GetOrCreateProgress(character, treeId);
            var investment = progress.NodeInvestments.FirstOrDefault(value => value.NodeId == nodeId);
            var currentRanks = investment?.Ranks ?? 0;
            if (currentRanks >= node.MaxRanks || GetInvestedRanks(progress) >= MaximumPointsPerTree
                || !RequirementsMet(progress, node.Requirements)) return false;

            if (investment == null)
            {
                investment = new SkillTreeNodeInvestment { NodeId = nodeId };
                progress.NodeInvestments.Add(investment);
            }

            investment.Ranks++;
            character.AvailableSkillPoints--;
            return true;
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

        private static int GetInvestedRanks(CharacterInstance character, string treeId)
        {
            return GetInvestedRanks(character.SkillTreeProgress?.FirstOrDefault(value => value?.TreeId == treeId));
        }

        private static int GetInvestedRanks(SkillTreeProgress progress)
        {
            return progress?.NodeInvestments?.Sum(value => Math.Max(0, value.Ranks)) ?? 0;
        }

        private static bool RequirementsMet(SkillTreeProgress progress, List<SkillTreeNodeRequirement> requirements)
        {
            if (requirements == null) return true;
            return requirements.All(requirement => requirement != null
                && progress.NodeInvestments.Any(investment => investment.NodeId == requirement.NodeId
                    && investment.Ranks >= requirement.MinimumRanks));
        }
    }
}
