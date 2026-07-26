using System.Collections.Generic;
using Godmist.Core.Enums;

namespace Godmist.Systems.Combat
{
    public class ResourceRuleRegistry
    {
        private readonly Dictionary<ResourceType,IResourceRule> rules = new()
        {
            { ResourceType.Mana, new ManaResourceRule() },
            { ResourceType.Momentum, new MomentumResourceRule() },
            { ResourceType.Rage, new RageResourceRule() }
        };

        public bool TryGetRule(ResourceType resourceType, out IResourceRule rule)
        {
            return rules.TryGetValue(resourceType, out rule);
        }
    }
}