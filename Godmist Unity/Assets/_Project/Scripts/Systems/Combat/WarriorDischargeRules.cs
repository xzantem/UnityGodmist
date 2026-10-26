using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class WarriorDischargeRules
    {
        public const string SkillId = "skill.warrior.discharge";
        public const int MinimumRage = 20, MaximumRage = 100;
        public static int Cost(BattleUnit caster) => Mathf.Clamp(caster?.Character?.ResourcePool?.CurrentValue ?? 0, 0, MaximumRage);
        public static float Multiplier(int spent) => 1f + .01f * Mathf.Clamp(spent, 0, MaximumRage);
        public static float DefenseIgnore(BattleUnit caster, SkillData skill, int? spent = null, DamageType type = DamageType.Physical) => type == DamageType.Physical && skill?.SkillId == SkillId
            ? .2f * Mathf.Clamp(WarriorCombatSystem.B(caster, "larger_wound"), 0, 2) * ((spent ?? Cost(caster)) >= 50 ? 2f : 1f) : 0f;
        public static WarriorDischargeCast Capture(BattleUnit caster, SkillData skill) => skill?.SkillId == SkillId
            ? new WarriorDischargeCast(caster.Character.Stats.MinAttack.GetValue(), caster.Character.Stats.MaxAttack.GetValue(),
                caster.Character.Stats.CritChance.GetValue(), Cost(caster)) : null;
        // Detached AI state only: mirror rage-based derived bonuses without running
        // reconciliation against equipment instances shared with the live world.
        public static void UpdateProjectedBonuses(BattleUnit caster, int previousRage)
        {
            var stats = caster.Character.Stats;
            var maximum = Mathf.RoundToInt(stats.MaxResource.GetValue());
            var before = Mathf.Min(previousRage, maximum);
            var after = Mathf.Min(caster.Character.ResourcePool.CurrentValue, maximum);
            stats.CritChance.SetEffectiveValue(Mathf.Max(0, stats.CritChance.GetValue() + .0005f * WarriorCombatSystem.B(caster, "revenge") * (after - before)));
            stats.Speed.SetEffectiveValue(Mathf.Max(0, stats.Speed.GetValue() + 4 * WarriorCombatSystem.B(caster, "adrenaline") * (after / 25 - before / 25)));
        }
        public static int RefundKill(BattleUnit caster, WarriorDischargeCast cast, CombatRecoverySystem recovery)
        {
            if (cast == null || WarriorCombatSystem.B(caster, "wounding") <= 0 || !cast.TryMarkRefunded()) return 0;
            var restored = recovery.RestoreResource(caster, Mathf.FloorToInt(cast.RageSpent * .25f * Mathf.Clamp(WarriorCombatSystem.B(caster, "wounding"), 0, 2)));
            WarriorCombatSystem.Refresh(caster);
            return restored;
        }
    }
}
