using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed class SummonSystem
    {
        private readonly Action<string> battleLog;
        private readonly CharacterInstanceFactory characterFactory = new();
        private int nextCreationOrder;

        public SummonSystem(Action<string> battleLog = null)
        {
            this.battleLog = battleLog;
        }

        public bool TrySummon(CombatSession session, BattleUnit caster,
            SkillData skill, SkillEffectData effect, int effectIndex,
            GridPosition position, out BattleUnit summonedUnit)
        {
            summonedUnit = null;
            if (session?.Grid == null || caster?.Character == null || skill == null
                || effect?.SummonedCharacterDefinition == null
                || effect.SummonedClassData == null
                || !IsValidPosition(session, caster, effect, position))
                return false;

            RemoveOldestOverLimit(session, caster, skill, effectIndex,
                effect.MaxActiveSummons);

            var character = characterFactory.Create(effect.SummonedCharacterDefinition,
                effect.SummonedClassData);
            character.CharacterId = $"summon.{caster.Character.CharacterId}.{skill.SkillId}.{effectIndex}.{++nextCreationOrder}";
            character.CurrentHealth = Mathf.Max(1, Mathf.CeilToInt(
                character.Stats.MaxHealth.GetValue() * effect.SummonPercentMaxHealth));

            var speed = Mathf.Max(0.01f, character.Stats.Speed.GetValue());
            var maxAp = Mathf.Max(0, Mathf.RoundToInt(
                speed * session.ActionPointsPerSpeed));
            summonedUnit = new BattleUnit
            {
                Character = character,
                Position = position,
                IsPlayerSide = caster.IsPlayerSide,
                CurrentActionPoints = maxAp,
                MaxActionPoints = maxAp,
                CurrentActionValue = CombatTurnSystem.BaseActionValue / speed
            };

            session.Summons ??= new();
            session.Units.Add(summonedUnit);
            session.Summons.Add(new SummonedBattleUnit(summonedUnit,
                caster.Character.CharacterId, skill.SkillId, effectIndex,
                effect.SummonDurationTurns, nextCreationOrder));
            session.TurnParticipants ??= new();
            session.TurnParticipants.Add(new CombatTurnParticipant(summonedUnit));
            battleLog?.Invoke($"{caster.Character.Definition.DisplayName} przyzywa {character.Definition.DisplayName}.");
            return true;
        }

        public bool AdvanceDurationAtTurnEnd(CombatSession session, BattleUnit unit)
        {
            var summon = session?.Summons?.FirstOrDefault(item => item?.Unit == unit);
            return summon != null && summon.AdvanceDuration() && RemoveSummon(session, summon);
        }

        public int RemoveSummonsCreatedBy(CombatSession session, string creatorCharacterId)
        {
            if (session?.Summons == null || string.IsNullOrWhiteSpace(creatorCharacterId)) return 0;
            var matching = session.Summons.Where(item => item != null
                && string.Equals(item.CreatorCharacterId, creatorCharacterId,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var summon in matching) RemoveSummon(session, summon);
            return matching.Count;
        }

        public bool RemoveSummon(CombatSession session, SummonedBattleUnit summon)
        {
            if (session == null || summon?.Unit == null) return false;
            session.Summons.Remove(summon);
            session.Units.Remove(summon.Unit);
            session.TurnParticipants?.RemoveAll(item => item?.Unit == summon.Unit);
            session.TurnOrder?.Remove(summon.Unit);
            if (session.ActiveUnit == summon.Unit)
            {
                session.ActiveUnit = null;
                session.ActiveParticipant = null;
            }
            battleLog?.Invoke($"{summon.Unit.Character.Definition.DisplayName} znika.");
            return true;
        }

        private static bool IsValidPosition(CombatSession session, BattleUnit caster,
            SkillEffectData effect, GridPosition position)
        {
            return session.Grid.IsWithinBounds(position)
                && session.Grid.IsWalkable(position)
                && !session.Grid.IsBlocked(position)
                && !session.IsPositionOccupied(position)
                && !session.IsPositionOccupiedByMapObject(position)
                && CombatTargetingService.IsPositionWithinRange(caster.Position,
                    position, effect.SummonPlacementRange);
        }

        private void RemoveOldestOverLimit(CombatSession session, BattleUnit caster,
            SkillData skill, int effectIndex, int limit)
        {
            var matches = session.Summons.Where(item => item != null
                && item.CreatorCharacterId == caster.Character.CharacterId
                && item.SourceSkillId == skill.SkillId
                && item.SourceEffectIndex == effectIndex)
                .OrderBy(item => item.CreationOrder).ToList();
            while (matches.Count >= Mathf.Max(1, limit))
            {
                RemoveSummon(session, matches[0]);
                matches.RemoveAt(0);
            }
        }
    }
}
