using Godmist.Core.Enums;
using Godmist.Data.Skills;

namespace Godmist.Runtime.Combat
{
    /// <summary>Presentation snapshot captured before charge, swap, push or teleport changes positions.</summary>
    public readonly struct CombatSkillPresentation
    {
        public CombatSkillPresentation(BattleUnit caster, SkillData skill, GridPosition? aim = null,
            AttackType attackType = AttackType.Unknown, bool knifeRecovery = false,
            System.Collections.Generic.IReadOnlyList<BattleUnit> targets = null)
        {
            Caster = caster;
            Skill = skill;
            Origin = caster?.Position ?? default;
            Aim = aim;
            AttackType = attackType;
            KnifeRecovery = knifeRecovery;
            Targets = targets;
        }

        public BattleUnit Caster { get; }
        public SkillData Skill { get; }
        public GridPosition Origin { get; }
        public GridPosition? Aim { get; }
        public AttackType AttackType { get; }
        public bool KnifeRecovery { get; }
        public System.Collections.Generic.IReadOnlyList<BattleUnit> Targets { get; }
    }
}
