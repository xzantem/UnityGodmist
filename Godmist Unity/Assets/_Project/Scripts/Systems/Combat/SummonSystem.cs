using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
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

        public static int CountOwnedSummons(CombatSession session, BattleUnit caster, string groupId)
            => session?.Summons?.Count(s => s?.Unit?.Character?.LifeState == CharacterLifeState.Alive
                && s.CreatorCharacterId == caster?.Character?.CharacterId && s.GroupId == groupId) ?? 0;

        public static float DamageScale(BattleUnit caster, SkillEffectData effect)
            => string.IsNullOrWhiteSpace(effect?.DamagePerOwnedSummonGroup) ? 1f
                : CountOwnedSummons(caster?.WarriorState.Session, caster, effect.DamagePerOwnedSummonGroup);

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

            var candidates = effect.SummonPool?.Where(item => item != null).ToArray();
            var definition = candidates != null && candidates.Length > 0
                ? candidates[UnityEngine.Random.Range(0, candidates.Length)]
                : effect.SummonedCharacterDefinition;
            var character = characterFactory.Create(definition,
                effect.SummonedClassData, caster.Character.Level);
            character.CharacterId = $"summon.{caster.Character.CharacterId}.{skill.SkillId}.{effectIndex}.{++nextCreationOrder}";
            character.CurrentHealth = Mathf.Max(1, Mathf.CeilToInt(
                character.Stats.MaxHealth.GetValue() * effect.SummonPercentMaxHealth));

            if (effect.SummonUsesCasterStats)
            {
                character.Stats.MaxHealth = new Godmist.Core.Types.Stat(Mathf.Max(1,
                    Mathf.RoundToInt(caster.Character.Stats.MaxHealth.GetValue() * effect.SummonPercentMaxHealth)));
                character.CurrentHealth = Mathf.RoundToInt(character.Stats.MaxHealth.GetValue());
                character.Stats.MinAttack = new Godmist.Core.Types.Stat(caster.Character.Stats.MinAttack.GetValue());
                character.Stats.MaxAttack = new Godmist.Core.Types.Stat(caster.Character.Stats.MaxAttack.GetValue());
                character.Stats.Speed = new Godmist.Core.Types.Stat(caster.Character.Stats.Speed.GetValue());
            }
            var speed = Mathf.Max(0.01f, character.Stats.Speed.GetValue());
            var maxAp = Mathf.Max(0, Mathf.RoundToInt(
                speed * session.ActionPointsPerSpeed * ActionPointUnits.PerPoint));
            summonedUnit = new BattleUnit
            {
                Character = character,
                Position = position,
                IsPlayerSide = caster.IsPlayerSide,
                IsAiControlledSummon = effect.SummonStationary,
                IsStationarySummon = effect.SummonStationary,
                CurrentActionPoints = maxAp,
                MaxActionPoints = maxAp,
                CurrentActionValue = CombatTurnSystem.BaseActionValue / speed
            };

            session.Summons ??= new();
            session.Units.Add(summonedUnit);
            session.Summons.Add(new SummonedBattleUnit(summonedUnit,
                caster.Character.CharacterId, skill.SkillId, effectIndex,
                effect.SummonPersistent ? -1 : effect.SummonDurationTurns, nextCreationOrder) { GroupId = effect.SummonGroupId });
            summonedUnit.WarriorState.Session = session;
            session.TurnParticipants ??= new();
            session.TurnParticipants.Add(new CombatTurnParticipant(summonedUnit));
            battleLog?.Invoke($"{caster.Character.Definition.DisplayName} przyzywa {character.Definition.DisplayName}.");
            return true;
        }

        public bool TrySummonAdjacent(
            CombatSession session,
            BattleUnit caster,
            CharacterDefinition characterDefinition,
            CharacterClassData classData,
            string sourceSkillId,
            int durationTurns,
            int maxActiveSummons,
            float maxHealthPercent,
            out BattleUnit summonedUnit, int level = 1)
        {
            summonedUnit = null;
            if (session?.Grid == null
                || caster?.Character == null
                || characterDefinition == null
                || classData == null)
            {
                return false;
            }

            var existing = session.Summons.Where(item => item != null
                && item.CreatorCharacterId == caster.Character.CharacterId
                && item.SourceSkillId == sourceSkillId)
                .OrderBy(item => item.CreationOrder).ToList();
            var replaceable = existing.Count >= Mathf.Max(1, maxActiveSummons)
                ? existing[0].Unit : null;

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var position = new GridPosition(
                        caster.Position.X + x,
                        caster.Position.Y + y, caster.Position.Level);
                    if (!IsValidAdjacentPosition(session, position, replaceable)
                        || (session.DungeonContext?.SurfaceMap?.HasElevatedSurfaces == true
                            && !new CombatLineOfSightService().HasLineOfSight(session, caster.Position, position)))
                        continue;

                    RemoveOldestOverLimit(
                        session,
                        caster.Character.CharacterId,
                        sourceSkillId,
                        maxActiveSummons);

                    var character = characterFactory.Create(
                        characterDefinition,
                        classData, level);
                    character.CharacterId =
                        $"summon.{caster.Character.CharacterId}.{sourceSkillId}.{++nextCreationOrder}";
                    ApplySummonHealth(character, maxHealthPercent);
                    character.CurrentHealth = Mathf.Max(1, Mathf.CeilToInt(
                        character.Stats.MaxHealth.GetValue()));

                    var speed = Mathf.Max(.01f, character.Stats.Speed.GetValue());
                    var maxAp = Mathf.Max(0, Mathf.RoundToInt(
                        speed * session.ActionPointsPerSpeed * ActionPointUnits.PerPoint));
                    summonedUnit = new BattleUnit
                    {
                        Character = character,
                        Position = position,
                        IsPlayerSide = caster.IsPlayerSide,
                        IsAiControlledSummon = true,
                        CurrentActionPoints = maxAp,
                        MaxActionPoints = maxAp,
                        CurrentActionValue = CombatTurnSystem.BaseActionValue / speed
                    };

                    session.Summons ??= new();
                    session.Units.Add(summonedUnit);
                    session.Summons.Add(new SummonedBattleUnit(
                        summonedUnit,
                        caster.Character.CharacterId,
                        sourceSkillId,
                        -1,
                        durationTurns < 0 ? -1 : Mathf.Max(1, durationTurns),
                        nextCreationOrder));
                    session.TurnParticipants ??= new();
                    session.TurnParticipants.Add(
                        new CombatTurnParticipant(summonedUnit));
                    battleLog?.Invoke(
                        $"{caster.Character.Definition.DisplayName} przyzywa "
                        + $"{character.Definition.DisplayName}.");
                    return true;
                }
            }

            return false;
        }

        private static void ApplySummonHealth(Godmist.Runtime.Characters.CharacterInstance character, float fraction)
        {
            character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
                EffectId = "summon.health", DefinitionId = "summon.health", DisplayName = "Zdrowie przywołania",
                Lifetime = Godmist.Core.Enums.PassiveEffectLifetime.CombatScoped,
                StatModifiers = new System.Collections.Generic.List<Godmist.Core.Types.StatModifier> { new() {
                    StatType = Godmist.Core.Enums.CharacterStatType.MaxHealth,
                    Operation = Godmist.Core.Enums.StatModifierOperation.Percent,
                    Stacking = Godmist.Core.Enums.StatModifierStacking.Additive,
                    Value = Mathf.Clamp01(fraction) - 1 } } });
            new Godmist.Systems.Characters.CharacterStatPipelineSystem().Refresh(character);
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

        private static bool IsValidAdjacentPosition(
            CombatSession session,
            GridPosition position, BattleUnit replaceable = null)
        {
            return session.Grid.IsWithinBounds(position)
                && session.Grid.IsWalkable(position)
                && !session.Grid.IsBlocked(position)
                && !session.IsPositionOccupied(position, replaceable)
                && !session.IsPositionOccupiedByMapObject(position);
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

        private void RemoveOldestOverLimit(
            CombatSession session,
            string creatorCharacterId,
            string sourceSkillId,
            int limit)
        {
            var matches = session.Summons.Where(item => item != null
                && item.CreatorCharacterId == creatorCharacterId
                && item.SourceSkillId == sourceSkillId)
                .OrderBy(item => item.CreationOrder)
                .ToList();
            while (matches.Count >= Mathf.Max(1, limit))
            {
                RemoveSummon(session, matches[0]);
                matches.RemoveAt(0);
            }
        }
    }
}
