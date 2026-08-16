using System;
using System.Collections.Generic;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public class SkillTreeProgress
    {
        public string TreeId = string.Empty;
        public List<SkillTreeNodeInvestment> NodeInvestments = new();
    }

    [Serializable]
    public class SkillTreeNodeInvestment
    {
        public string NodeId = string.Empty;
        public int Ranks;
    }
}
