using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Characters
{
    public static class SkillTreeRankPreview
    {
        public static CharacterInstance Create(CharacterInstance source,SkillTreeNodeDefinition node,int rank)
        {
            if (source == null || node == null) return source;
            var tree = source.ClassData?.SkillTrees?.FirstOrDefault(t => t != null && t.Nodes.Contains(node));
            if (tree == null) return source;
            var copy = new CharacterInstance
            {
                CharacterId = source.CharacterId, ShieldPool=source.ShieldPool, Definition = source.Definition, ClassData = source.ClassData,
                Level = source.Level, CurrentHealth = source.CurrentHealth, LifeState = source.LifeState,
                WeaponAccuracy = source.WeaponAccuracy, Equipment = source.Equipment, EquipmentGenerationCarry=source.EquipmentGenerationCarry,
                Stats = new CharacterStats(), BaseResistances = Copy(source.BaseResistances), Resistances = Copy(source.Resistances),
                ResourcePool = source.ResourcePool == null ? null : new ResourcePool(source.ResourcePool.ResourceType,
                    source.ResourcePool.CurrentValue,source.ResourcePool.MaxValue,source.ResourcePool.Regeneration),
                PassiveEffects = new List<PassiveEffectRuntime>(source.PassiveEffects ?? new()),
                Skills = new(source.Skills ?? new()),
                SkillTreeProgress = (source.SkillTreeProgress ?? new()).Select(p => new SkillTreeProgress
                {
                    TreeId = p.TreeId, NodeInvestments = p.NodeInvestments.Select(n => new SkillTreeNodeInvestment
                    { NodeId = n.NodeId, Ranks = n.Ranks }).ToList()
                }).ToList()
            };
            foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)))
                copy.Stats.GetStat(type) = source.Stats.GetStat(type);
            var progress = copy.SkillTreeProgress.Find(p => p.TreeId == tree.Id);
            if (progress == null) { progress = new SkillTreeProgress { TreeId = tree.Id }; copy.SkillTreeProgress.Add(progress); }
            var investment = progress.NodeInvestments.Find(n => n.NodeId == node.Id);
            if (investment == null) { investment = new SkillTreeNodeInvestment { NodeId = node.Id }; progress.NodeInvestments.Add(investment); }
            investment.Ranks = Math.Clamp(rank,0,node.EffectiveMaxRanks);
            new CharacterStatPipelineSystem().Refresh(copy,treePreview:true);
            return copy;
        }
        private static ResistanceSet Copy(ResistanceSet s) => s == null ? new() : new()
        {
            BleedResistance=s.BleedResistance,PoisonResistance=s.PoisonResistance,FireResistance=s.FireResistance,
            WeakeningResistance=s.WeakeningResistance,SuppressionResistance=s.SuppressionResistance,MoveResistance=s.MoveResistance
        };
    }
}
