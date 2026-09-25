using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    /// <summary>Health gates and disarmable AV runes of the Dead King encounter.</summary>
    public sealed class CatacombsBossSystem
    {
        public const string BossId = "enemy.catacombs.dead_king";
        public const string VengeanceSkillId = "skill.enemy.catacombs.dead_king_vengeance";
        public const string GuardSkillId = "skill.enemy.catacombs.dead_king_guard";
        public const string RuneObjectId = "object.catacombs.dead_king_rune";

        private static readonly float[] Thresholds = { .75f, .5f, .25f };
        private readonly HashSet<int> triggeredThresholds = new();
        private int pendingVengeanceCasts;
        private readonly SummonedObjectSystem objectSystem;
        private readonly CombatDamageSystem damageSystem;
        private readonly Action<string> battleLog;
        private readonly CombatFeedbackBus feedbackBus;

        public CatacombsBossSystem(SummonedObjectSystem objectSystem,
            CombatDamageSystem damageSystem, Action<string> battleLog = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.objectSystem = objectSystem;
            this.damageSystem = damageSystem;
            this.battleLog = battleLog;
            this.feedbackBus = feedbackBus;
        }

        public static bool IsRune(SummonedCombatMapObject item) =>
            item?.Definition != null && item.Definition.ObjectId == RuneObjectId;

        public void OnEnemyHealthChanged(CombatSession session, BattleUnit enemy,
            int previousHealth)
        {
            if (session?.Grid == null || enemy?.Character?.Definition?.Id != BossId
                || enemy.Character.CurrentHealth <= 0) return;

            var maxHealth = Mathf.Max(1f, enemy.Character.Stats.MaxHealth.GetValue());
            if (FindVengeanceSkill(enemy) == null) return;

            for (var i = 0; i < Thresholds.Length; i++)
            {
                var limit = maxHealth * Thresholds[i];
                if (previousHealth <= limit || enemy.Character.CurrentHealth > limit
                    || !triggeredThresholds.Add(i)) continue;

                pendingVengeanceCasts++;
            }
        }

        public bool CanCastVengeance(CombatSession session, BattleUnit boss) =>
            pendingVengeanceCasts > 0 && session != null && boss != null
            && session.ActiveUnit == boss
            && session.Outcome == CombatOutcomeState.Ongoing
            && boss?.Character?.LifeState == CharacterLifeState.Alive
            && boss.Character.Definition?.Id == BossId
            && !session.SummonedObjects.Any(IsRune);

        public bool TryExecuteVengeance(CombatSession session, BattleUnit boss,
            out SkillData executedSkill)
        {
            executedSkill = null;
            if (!CanCastVengeance(session, boss)) return false;

            var skill = FindVengeanceSkill(boss);
            var runeEffect = skill?.Effects?.FirstOrDefault(item =>
                item?.EffectType == EffectType.CreateObject
                && item.SummonedObjectDefinition?.ObjectId == RuneObjectId);
            if (runeEffect == null || skill.ActionCost != 0 || skill.ResourceCost != 0) return false;

            var placed = PlaceRunes(session, boss, skill, runeEffect);
            if (placed == 0) return false;

            pendingVengeanceCasts--;
            executedSkill = skill;
            feedbackBus?.Publish(new CombatFeedbackEvent(boss,
                CombatFeedbackType.SkillUsed, effectName: skill.DisplayName));
            battleLog?.Invoke($"{boss.Character.Definition.DisplayName} używa Zemsty Zaświatów: "
                + $"{placed} run. Wejdź na runę, aby ją rozbroić.");
            return true;
        }

        private static SkillData FindVengeanceSkill(BattleUnit boss) =>
            boss?.Character?.GetActiveSkills()?.FirstOrDefault(item =>
                item?.SkillId == VengeanceSkillId);

        private int PlaceRunes(CombatSession session, BattleUnit boss,
            SkillData skill, SkillEffectData effect)
        {
            var context = session.DungeonContext;
            string bossRoomId = null;
            if (context?.SurfaceMap != null)
            {
                var bossSurface = new Godmist.Runtime.Dungeons.DungeonSurfacePosition(
                    DungeonBattleGridFactory.ToWorldPosition(context, boss.Position), boss.Position.Level);
                if (!context.SurfaceMap.TryGetRoomId(bossSurface, out bossRoomId))
                    return 0;
            }

            var candidates = session.Grid.Positions.Where(position =>
                position.Level == boss.Position.Level
                && (bossRoomId == null || context.SurfaceMap.TryGetRoomId(
                    new Godmist.Runtime.Dungeons.DungeonSurfacePosition(
                        DungeonBattleGridFactory.ToWorldPosition(context, position), position.Level),
                    out var roomId) && roomId == bossRoomId)
                && session.Grid.IsWalkable(position)
                && !session.Grid.IsBlocked(position)
                && !session.IsPositionOccupied(position)
                && !session.IsPositionOccupiedByMapObject(position))
                .ToList();
            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var swap = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
            }

            var placed = 0;
            foreach (var position in candidates)
            {
                if (placed == 5) break;
                if (objectSystem.TryCreate(session, boss, skill, effect, 0,
                    position, out _)) placed++;
            }
            return placed;
        }

        public void OnUnitEnteredCell(CombatSession session, BattleUnit unit)
        {
            if (session == null || unit?.IsPlayerSide != true) return;
            var rune = session.SummonedObjects.FirstOrDefault(item =>
                IsRune(item) && item.Position == unit.Position);
            if (rune == null) return;
            objectSystem.Remove(session, rune);
            battleLog?.Invoke($"{unit.Character.Definition.DisplayName} rozbraja runę Zemsty Zaświatów.");
        }

        public void ExecuteRuneTurn(CombatSession session,
            SummonedCombatMapObject rune)
        {
            if (session == null || !IsRune(rune)) return;
            if (rune.RemainingDurationTurns > 1)
            {
                battleLog?.Invoke($"Runa Zemsty Zaświatów pulsuje. Wybuch za {rune.RemainingDurationTurns - 1} "
                    + (rune.RemainingDurationTurns == 2 ? "turę runy." : "tury runy."));
                return;
            }

            var boss = session.Units.FirstOrDefault(unit =>
                unit?.Character?.CharacterId == rune.CreatorCharacterId);
            var target = session.Units.Where(unit =>
                unit?.IsPlayerSide == true
                && unit.Character?.LifeState == CharacterLifeState.Alive)
                .OrderBy(unit => Math.Abs(unit.Position.X - rune.Position.X)
                    + Math.Abs(unit.Position.Y - rune.Position.Y))
                .FirstOrDefault();
            if (boss?.Character?.LifeState != CharacterLifeState.Alive || target == null)
                return;

            var damage = CombatDamageValueCalculator.CalculateDamageValue(
                boss, DamageValueSource.AttackRange, 1.5f);
            battleLog?.Invoke($"Runa Zemsty Zaświatów uderza w {target.Character.Definition.DisplayName}.");
            damageSystem.ApplySummonedObjectDamage(session, target, damage,
                DamageType.Magical);
        }

        public void OnBossDied(CombatSession session, BattleUnit unit)
        {
            if (session == null || unit?.Character?.Definition?.Id != BossId) return;
            foreach (var rune in session.SummonedObjects.Where(IsRune).ToArray())
                objectSystem.Remove(session, rune);
        }
    }
}
