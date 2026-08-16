using System;
using System.Collections.Generic;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Skills
{
    [CreateAssetMenu(fileName = "SkillTree", menuName = "Godmist/Skills/Skill Tree")]
    public class SkillTreeDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public List<SkillTreeNodeDefinition> Nodes { get; private set; } = new();
    }

    [Serializable]
    public class SkillTreeNodeDefinition
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField, Range(1, 7)] public int Tier { get; private set; } = 1;
        [field: SerializeField, Range(1, 5)] public int Column { get; private set; } = 1;
        [field: SerializeField, Range(1, 3)] public int MaxRanks { get; private set; } = 1;
        [field: SerializeField] public List<SkillTreeNodeRequirement> Requirements { get; private set; } = new();
    }

    [Serializable]
    public class SkillTreeNodeRequirement
    {
        [field: SerializeField] public string NodeId { get; private set; } = string.Empty;
        [field: SerializeField, Min(1)] public int MinimumRanks { get; private set; } = 1;
    }
}
