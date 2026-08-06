using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatSessionFactory
    {
        private readonly float actionPointsPerSpeed;
        private readonly CharacterInstanceFactory characterFactory = new();
        private readonly CombatRewardSystem rewardSystem = new();

        public CombatSessionFactory(float actionPointsPerSpeed = 0.25f)
        {
            this.actionPointsPerSpeed = actionPointsPerSpeed;
        }

        // Zachowujemy ten wariant dla CombatSandboxa i ręcznie uruchamianych walk.
        public bool TryCreate(
            GameSession gameSession,
            EncounterDefinition encounter,
            out CombatSession combatSession)
        {
            var combatId = encounter != null ? encounter.Id : string.Empty;

            return TryCreate(
                gameSession,
                encounter,
                combatId,
                out combatSession);
        }

        // Wariant używany przez encounter wygenerowany wewnątrz lochu.
        public bool TryCreate(
            GameSession gameSession,
            DungeonEncounterInstance encounterInstance,
            out CombatSession combatSession)
        {
            combatSession = null;

            if (encounterInstance == null) return false;

            return TryCreate(
                gameSession,
                encounterInstance.Definition,
                encounterInstance.InstanceId,
                out combatSession);
        }

        private bool TryCreate(
            GameSession gameSession,
            EncounterDefinition encounter,
            string combatId,
            out CombatSession combatSession)
        {
            combatSession = null;

            if (gameSession == null
                || encounter == null
                || string.IsNullOrWhiteSpace(combatId)
                || encounter.GridWidth <= 0
                || encounter.GridHeight <= 0
                || encounter.Enemies == null
                || encounter.Enemies.Count == 0)
            {
                return false;
            }

            var session = new CombatSession
            {
                CombatId = combatId,
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(encounter.GridWidth, encounter.GridHeight),
                Units = new List<BattleUnit>(),
                Rewards = ResolveRewards(gameSession, encounter, combatId)
            };

            var playerIndex = 0;

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    continue;
                }

                if (character.LifeState != CharacterLifeState.Alive) continue;

                var position = new GridPosition(1, playerIndex + 1);

                if (position.Y >= encounter.GridHeight) return false;

                session.Units.Add(CreateBattleUnit(
                    character,
                    position,
                    isPlayerSide: true));

                playerIndex++;
            }

            if (playerIndex == 0) return false;

            foreach (var enemyData in encounter.Enemies)
            {
                if (enemyData == null
                    || enemyData.CharacterDefinition == null
                    || enemyData.ClassData == null
                    || string.IsNullOrWhiteSpace(enemyData.InstanceId))
                {
                    return false;
                }

                var position = new GridPosition(
                    enemyData.SpawnPosition.x,
                    enemyData.SpawnPosition.y);

                if (position.X < 0 || position.Y < 0
                    || position.X >= encounter.GridWidth
                    || position.Y >= encounter.GridHeight
                    || session.IsPositionOccupied(position))
                {
                    return false;
                }

                var enemy = characterFactory.Create(
                    enemyData.CharacterDefinition,
                    enemyData.ClassData);

                enemy.CharacterId = enemyData.InstanceId;

                session.Units.Add(CreateBattleUnit(
                    enemy,
                    position,
                    isPlayerSide: false));
            }

            combatSession = session;
            return true;
        }

        private BattleUnit CreateBattleUnit(
            CharacterInstance character,
            GridPosition position,
            bool isPlayerSide)
        {
            var speed = character.Stats.Speed.GetValue();

            var maxActionPoints = Mathf.Max(
                0,
                Mathf.RoundToInt(speed * actionPointsPerSpeed));

            return new BattleUnit
            {
                Character = character,
                Position = position,
                CurrentActionPoints = maxActionPoints,
                MaxActionPoints = maxActionPoints,
                CurrentActionValue = 0f,
                HasReacted = false,
                IsReadyToEscape = false,
                IsPlayerSide = isPlayerSide
            };
        }

        private CombatRewardResult ResolveRewards(
            GameSession gameSession,
            EncounterDefinition encounter,
            string combatId)
        {
            var dungeon = gameSession.ActiveDungeon;

            if (dungeon != null
                && dungeon.TryGetPreRolledRewards(combatId, out var rewards))
            {
                return rewards;
            }

            var rolledRewards = rewardSystem.RollEncounterRewards(encounter);

            dungeon?.TryStorePreRolledRewards(combatId, rolledRewards);

            return rolledRewards;
        }
    }
}