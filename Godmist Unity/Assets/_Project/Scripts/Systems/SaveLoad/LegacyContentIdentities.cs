using System;
using System.Collections.Generic;
namespace Godmist.Systems.SaveLoad
{
    public static partial class LegacyContentIdentities
    {
        public static string Effect(string id)
        {
            if (id == null) return null;
            if (EffectAliases.TryGetValue(id,out var canonical)) return canonical;
            // Aura sources include the caster identity after the historical effect token.
            foreach (var alias in EffectAliases)
                if (alias.Key.EndsWith(".",StringComparison.Ordinal) && id.StartsWith(alias.Key,StringComparison.Ordinal))
                    return alias.Value+id.Substring(alias.Key.Length);
            return id;
        }
        public static string EffectToken(string skillId, int historicalIndex)
        {
            var oldId = skillId+".effect."+historicalIndex;
            if (!EffectAliases.TryGetValue(oldId,out var canonical))
                throw new InvalidOperationException("Unknown historical effect selector: "+oldId);
            return canonical.Substring(canonical.LastIndexOf('.')+1);
        }
        public static bool TryRewards(string questId, IEnumerable<string> saved, out List<string> keys)
        {
            keys = new List<string>();
            foreach (var key in saved ?? Array.Empty<string>())
            {
                if (key == null) return false;
                if (RewardAliases.TryGetValue(questId+"|"+key, out var canonical)) keys.Add(canonical);
                else if (key.LastIndexOf(':') is var colon && colon>=0 && int.TryParse(key.Substring(colon+1),out _)
                    && !key.StartsWith("reward:",StringComparison.Ordinal)) return false;
                else keys.Add(key);
            }
            return true;
        }
    }
}
