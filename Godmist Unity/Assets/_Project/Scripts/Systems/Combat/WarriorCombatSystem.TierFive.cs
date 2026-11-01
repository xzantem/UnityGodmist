using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class WarriorCombatSystem
    {
        // Capture before payment; nested casts restore the enclosing cast's snapshot.
        public sealed class CastScope : IDisposable
        {
            readonly BattleUnit unit;
            readonly int? previous;
            readonly string previousSkill;
            public CastScope(BattleUnit unit, SkillData skill)
            {
                this.unit = unit;
                previous = unit.WarriorState.AccuracyRageSnapshot;
                previousSkill = unit.WarriorState.AccuracySnapshotSkill;
                unit.WarriorState.AccuracyRageSnapshot = unit.Character.ResourcePool?.CurrentValue ?? 0;
                unit.WarriorState.AccuracySnapshotSkill = skill?.SkillId;
            }
            public void Dispose()
            {
                unit.WarriorState.AccuracyRageSnapshot = previous;
                unit.WarriorState.AccuracySnapshotSkill = previousSkill;
            }
        }

        public static float AccuracyBonus(BattleUnit unit, SkillData skill)
        {
            if (!IsRage(unit) || B(unit, "t5_1") == 0) return 0;
            var rage = unit.WarriorState.AccuracySnapshotSkill == skill?.SkillId
                ? unit.WarriorState.AccuracyRageSnapshot ?? unit.Character.ResourcePool.CurrentValue
                : unit.Character.ResourcePool.CurrentValue;
            return .5f * Mathf.Max(0, rage);
        }

        public static int OrderRangeBonus(BattleUnit unit, SkillData skill) =>
            skill?.SkillId is "skill.warrior.destruction" or "skill.warrior.protection" or "skill.warrior.regroup"
                ? 2 * C(unit, "t5_5") : 0;

        public static void ApplyAegis(BattleUnit source, BattleUnit target)
        {
            var rank = C(source, "t5_1");
            if (rank > 0) Variable(source, target, GameplayVariableType.DamageDealtPercent,
                .15f * rank, 3, "warrior.aegis", "Egida");
        }

        public static float DuelCriticalChance(CombatSession session, BattleUnit source, BattleUnit target)
        {
            if (C(source, "t5_3") == 0 || source?.Character == null || target?.Character == null
                || source.IsPlayerSide == target.IsPlayerSide) return 0;
            session ??= source.WarriorState.Session;
            bool AdjacentEnemy(BattleUnit enemy) => enemy?.Character?.LifeState == CharacterLifeState.Alive
                && enemy.IsPlayerSide != source.IsPlayerSide && enemy.Position.Level == source.Position.Level
                && Math.Abs(enemy.Position.X-source.Position.X)+Math.Abs(enemy.Position.Y-source.Position.Y)==1
                && session?.Grid?.CanMoveBetween(source.Position, enemy.Position) == true;
            return AdjacentEnemy(target) && session.Units.Count(AdjacentEnemy) == 1 ? .1f*C(source,"t5_3") : 0;
        }

        static int HealthAfterPayment(BattleUnit unit, SkillData previewSkill)
        {
            var health = unit?.Character?.CurrentHealth ?? 0;
            if (previewSkill == null || !IsRage(unit) || previewSkill.ResourceType != ResourceType.Rage) return health;
            var spent = Math.Min(unit.Character.ResourcePool.CurrentValue,
                PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit,previewSkill));
            var healing = Mathf.FloorToInt(unit.WarriorState.HealingRemainder + spent*.02f*Points(unit.Character,Brawler));
            return Math.Min(unit.Character.MaximumHealth,health+healing);
        }

        public static float MissingHealthPercent(BattleUnit unit, SkillData previewSkill = null) => unit?.Character == null ? 0 :
            100f * Mathf.Clamp01(1f - (float)HealthAfterPayment(unit,previewSkill) / Mathf.Max(1, unit.Character.MaximumHealth));

        public static float CriticalDamage(BattleUnit unit, SkillData previewSkill = null)
        {
            var rank = B(unit, "t5_3");
            return (unit?.Character?.Stats?.CritDamage.GetValue() ?? 1f)
                + MissingHealthPercent(unit,previewSkill) * (rank == 1 ? .003f : rank > 1 ? .005f : 0);
        }

        public static float LowHealthDamageBonus(BattleUnit unit, SkillData previewSkill = null)
        {
            var rank = B(unit,"t5_2");
            if (rank == 0 || unit?.Character == null) return 0;
            return (long)HealthAfterPayment(unit,previewSkill) * 100 < (long)unit.Character.MaximumHealth * (rank == 1 ? 10 : 20)
                ? rank == 1 ? .3f : .5f : 0;
        }

        public static void BeginFencing(BattleUnit unit, SkillData skill, System.Collections.Generic.IReadOnlyList<BattleUnit> targets)
        {
            if (!PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(skill)) return;
            var state = unit.WarriorState;
            var enemies = targets?.Where(t => t?.Character != null && t.IsPlayerSide != unit.IsPlayerSide).Distinct().ToArray();
            if (C(unit,"t5_4") == 0 || enemies?.Length != 1 || HasAreaDamage(skill))
            { state.FencingTarget = null; state.FencingStacks = 0; return; }
            var id = enemies[0].Character.CharacterId;
            state.FencingStacks = state.FencingTarget == id ? Math.Min(5,state.FencingStacks+1) : 1;
            state.FencingTarget = id;
        }

        public static float FencingMultiplier(PassiveEffectCombatContext context, bool preview)
        {
            var unit = context?.Source;
            if (unit == null || context.Target?.Character == null || C(unit,"t5_4") == 0
                || !PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(context.Skill) || HasAreaDamage(context.Skill)) return 1;
            var state = unit.WarriorState;
            var stacks = state.FencingTarget == context.Target.Character.CharacterId ? state.FencingStacks : 0;
            if (preview)
            {
                var targets = context.TargetsByModeIndex?.Values.SelectMany(t => t ?? Array.Empty<BattleUnit>())
                    .Where(t => t?.Character != null && t.IsPlayerSide != unit.IsPlayerSide).Distinct().Count();
                if (targets > 1) return 1;
                stacks = Math.Min(5,stacks+1);
            }
            return 1 + .02f*C(unit,"t5_4")*stacks;
        }

        static bool HasAreaDamage(SkillData skill) => skill?.Effects?.Any(effect =>
            effect.EffectType == EffectType.DealDamage && effect.TargetModeIndex >= 0
            && effect.TargetModeIndex < skill.TargetModes.Count
            && skill.TargetModes[effect.TargetModeIndex].TargetType != TargetType.Single) == true;

        public static int ResolveExecution(CombatSession session, PassiveEffectCombatContext context,
            bool critical, int damage, Func<float> roll = null)
        {
            if (!critical || C(context?.Source,"t5_2") == 0 || context?.Target?.Character == null || damage <= 0) return damage;
            var target = context.Target;
            if (!target.IsBoss)
            {
                var effective = CombatDamageSystem.CalculateIncomingBeforeRedirect(session,context.Source,target,
                    damage,context.DamageType ?? DamageType.Physical,context);
                if ((long)target.Character.CurrentHealth + (target.Character.ShieldPool?.TotalShield ?? 0) > 5L*effective) return damage;
            }
            return (roll?.Invoke() ?? UnityEngine.Random.value) < .1f
                ? (int)Math.Min(int.MaxValue,(long)damage*(target.IsBoss ? 2 : 5)) : damage;
        }

        // The value is measured only at the Death Wish HP clamp, after mitigation and shields.
        public static void RecordDeathWishPrevention(BattleUnit unit, int prevented)
        {
            if (prevented <= 0) return;
            if (B(unit,"t5_4") > 0) unit.WarriorState.DeathWishPreventedDamage =
                Math.Min(int.MaxValue,unit.WarriorState.DeathWishPreventedDamage+(long)prevented);
            if (B(unit,"t5_5") > 0 && IsRage(unit) && !GainBlocked(unit))
            {
                unit.Character.ResourcePool.Restore(10);
                unit.Character.ResourcePool.HasGainedResourceSinceLastTurn = true;
                Refresh(unit);
            }
        }

        public static void EndDeathWish(BattleUnit unit, CombatRecoverySystem recovery)
        {
            var bank = unit.WarriorState.DeathWishPreventedDamage;
            unit.WarriorState.DeathWishPreventedDamage = 0;
            var rank = B(unit,"t5_4");
            if (rank > 0 && unit.Character.LifeState == CharacterLifeState.Alive)
                recovery?.Heal(unit, (int)Math.Min(int.MaxValue,Math.Floor(bank*(rank==1?.25:.5))));
        }
    }
}
