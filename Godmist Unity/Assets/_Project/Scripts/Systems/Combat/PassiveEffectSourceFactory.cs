using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public static class PassiveEffectSourceFactory
    {
        public static PassiveEffectSourceRuntime FromSkill(
            BattleUnit source,
            SkillData skill,
            int effectIndex, Godmist.Data.Effects.SkillEffectData effect = null)
        {
            var sourceCharacterId = source?.Character?.CharacterId
                ?? string.Empty;

            var skillId = skill?.SkillId ?? "unknown_skill";

            return new PassiveEffectSourceRuntime
            {
                SourceType = PassiveEffectSourceType.Skill,
                SourceId = skill == null ? "unknown_skill" : SkillEffectIdentity.RuntimeId(skill, effect ?? skill.Effects[effectIndex], "effect"),
                SourceCharacterId = sourceCharacterId
            };
        }

        public static PassiveEffectSourceRuntime FromRuntimeSystem(
            BattleUnit source,
            string sourceId)
        {
            return new PassiveEffectSourceRuntime
            {
                SourceType = PassiveEffectSourceType.RuntimeSystem,
                SourceId = sourceId ?? string.Empty,
                SourceCharacterId = source?.Character?.CharacterId
                    ?? string.Empty
            };
        }
    }
}