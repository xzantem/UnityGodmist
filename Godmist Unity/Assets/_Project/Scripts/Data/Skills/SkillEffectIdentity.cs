using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
namespace Godmist.Data.Skills
{
    // Content identity is serialized once; indices only locate entries during one cast.
    public static class SkillEffectIdentity
    {
        public static string RuntimeId(SkillData skill, int index, string kind)
        {
            if (skill == null || index < 0 || index >= skill.Effects.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Effect identity requires its owning skill.");
            return RuntimeId(skill, skill.Effects[index], kind);
        }
        public static string RuntimeId(SkillData skill, SkillEffectData effect, string kind)
        {
            var id = effect?.StableId;
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"Missing effect identity: {skill?.SkillId}");
            return $"{skill?.SkillId ?? "runtime"}.{kind}.{id}";
        }
        public static bool IsReference(SkillEffectConditionData condition) => condition != null
            && condition.ConditionType is EffectConditionType.ReferencedEffectSucceeded or EffectConditionType.ReferencedEffectDealtHealthDamage;
        public static int ReferenceIndex(SkillData skill, SkillEffectConditionData condition, IReadOnlyList<SkillEffectData> scope = null)
        {
            if (condition == null) return -1;
            var effects = scope ?? skill?.Effects;
            if (!string.IsNullOrEmpty(condition.ReferencedEffectId))
            {
                for (var i=0; effects != null && i<effects.Count; i++)
                    if (effects[i]?.StableId == condition.ReferencedEffectId) return i;
                return -1;
            }
            // Only pre-migration definitions and transient test/authoring objects use this bridge.
            return skill?.HasStableEffectIds == true ? -1 : condition.ReferencedEffectIndex;
        }
        public static IReadOnlyList<int> ExecutionOrder(SkillData skill, IReadOnlyList<SkillEffectData> scope = null)
        {
            var effects = scope ?? skill.Effects;
            var result = new List<int>();
            var visiting = new HashSet<int>();
            var done = new HashSet<int>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (var i=0; i<effects.Count; i++)
                if (effects[i] == null || string.IsNullOrWhiteSpace(effects[i].StableId)
                    || !identities.Add(effects[i].StableId))
                    throw new InvalidOperationException($"Missing/duplicate effect identity: {skill.SkillId}/{i}");
            void Visit(int index)
            {
                if (done.Contains(index)) return;
                if (!visiting.Add(index)) throw new InvalidOperationException($"Cyclic effect references: {skill.SkillId}");
                foreach (var condition in effects[index].Conditions)
                {
                    if (!IsReference(condition)) continue;
                    var prior = ReferenceIndex(skill, condition, effects);
                    if (prior<0 || prior>=effects.Count) throw new InvalidOperationException($"Missing effect reference: {skill.SkillId}/{index}");
                    Visit(prior);
                }
                visiting.Remove(index); done.Add(index); result.Add(index);
            }
            for (var i=0;i<effects.Count;i++) Visit(i);
            return result;
        }
    }
}
