using System;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;

namespace Godmist.Systems.NPC
{
    public class NpcInstanceFactory
    {
        public NpcInstance Create(NpcDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException("NpcDefinition musi miec niepuste Id.", nameof(definition));
            }

            return new NpcInstance
            {
                NpcId = definition.Id,
                Definition = definition,
                LoyaltyLevel = 1,
                SpentGoldTotal = 0
            };
        }
    }
}