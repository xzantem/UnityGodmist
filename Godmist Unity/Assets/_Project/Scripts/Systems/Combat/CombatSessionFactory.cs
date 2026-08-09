using System;
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
        private readonly DungeonBattleGridFactory dungeonBattleGridFactory = new();
        private readonly DungeonCombatSpawnResolver dungeonCombatSpawnResolver = new();

        public CombatSessionFactory(float actionPointsPerSpeed = 0.25f)
        {
            this.actionPointsPerSpeed = actionPointsPerSpeed;
        }

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
                || encounter.Enemies == null
                || encounter.Enemies.Count == 0)
            {
                return false;
            }

            var dungeonContext = GetDungeonCombatContext(
                gameSession,
                combatId);

            if (dungeonContext == null
                && (encounter.GridWidth <= 0
                    || encounter.GridHeight <= 0))
            {
                return false;
            }

            if (!TryCreateGrid(
                    encounter,
                    dungeonContext,
                    out var battleGrid))
            {
                return false;
            }

            var session = new CombatSession
            {
                CombatId = combatId,
                Outcome = CombatOutcomeState.Ongoing,
                Grid = battleGrid,
                Units = new List<BattleUnit>(),
                Rewards = ResolveRewards(
                    gameSession,
                    encounter,
                    combatId)
            };

            var occupiedPositions = new HashSet<GridPosition>();

            if (!TryAddPlayerUnits(
                    gameSession,
                    session,
                    dungeonContext,
                    occupiedPositions))
            {
                return false;
            }

            if (!TryAddEnemyUnits(
                    encounter,
                    session,
                    dungeonContext,
                    occupiedPositions))
            {
                return false;
            }

            combatSession = session;
            return true;
        }

        private bool TryCreateGrid(
            EncounterDefinition encounter,
            DungeonCombatContext dungeonContext,
            out BattleGrid battleGrid)
        {
            battleGrid = null;

            if (dungeonContext == null)
            {
                battleGrid = new BattleGrid(
                    encounter.GridWidth,
                    encounter.GridHeight);

                return true;
            }

            return dungeonBattleGridFactory.TryCreate(
                dungeonContext,
                out battleGrid);
        }

        private bool TryAddPlayerUnits(
            GameSession gameSession,
            CombatSession session,
            DungeonCombatContext dungeonContext,
            HashSet<GridPosition> occupiedPositions)
        {
            var characters = new List<CharacterInstance>();

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    continue;
                }

                if (character.LifeState != CharacterLifeState.Alive)
                {
                    continue;
                }

                characters.Add(character);
            }

            if (characters.Count == 0) return false;

            List<GridPosition> positions;

            if (dungeonContext == null)
            {
                positions = new List<GridPosition>();

                for (var index = 0; index < characters.Count; index++)
                {
                    positions.Add(new GridPosition(1, index + 1));
                }
            }
            else
            {
                var partyAnchor =
                    DungeonBattleGridFactory.ToBattlePosition(
                        dungeonContext,
                        dungeonContext.PartyAnchorWorldPosition);

                var reservedPositions = new HashSet<GridPosition>(
                    occupiedPositions);

                if (!dungeonCombatSpawnResolver.TryResolveGroupPositions(
                        session.Grid,
                        partyAnchor,
                        characters.Count,
                        reservedPositions,
                        out positions))
                {
                    return false;
                }
            }

            for (var index = 0; index < characters.Count; index++)
            {
                var position = positions[index];

                if (!session.Grid.IsWalkable(position)
                    || session.Grid.IsBlocked(position)
                    || occupiedPositions.Contains(position))
                {
                    return false;
                }

                session.Units.Add(CreateBattleUnit(
                    characters[index],
                    position,
                    isPlayerSide: true));

                occupiedPositions.Add(position);
            }

            return true;
        }

        private bool TryAddEnemyUnits(
            EncounterDefinition encounter,
            CombatSession session,
            DungeonCombatContext dungeonContext,
            HashSet<GridPosition> occupiedPositions)
        {
            var validEnemies = new List<EncounterEnemyData>();

            foreach (var enemyData in encounter.Enemies)
            {
                if (enemyData == null
                    || enemyData.CharacterDefinition == null
                    || enemyData.ClassData == null
                    || string.IsNullOrWhiteSpace(enemyData.InstanceId))
                {
                    return false;
                }

                validEnemies.Add(enemyData);
            }

            List<GridPosition> positions;

            if (dungeonContext == null)
            {
                positions = new List<GridPosition>();

                foreach (var enemyData in validEnemies)
                {
                    positions.Add(new GridPosition(
                        enemyData.SpawnPosition.x,
                        enemyData.SpawnPosition.y));
                }
            }
            else
            {
                var enemyAnchor =
                    DungeonBattleGridFactory.ToBattlePosition(
                        dungeonContext,
                        dungeonContext.EnemyAnchorWorldPosition);

                var reservedPositions = new HashSet<GridPosition>(
                    occupiedPositions);

                if (!dungeonCombatSpawnResolver.TryResolveGroupPositions(
                        session.Grid,
                        enemyAnchor,
                        validEnemies.Count,
                        reservedPositions,
                        out positions))
                {
                    return false;
                }
            }

            for (var index = 0; index < validEnemies.Count; index++)
            {
                var enemyData = validEnemies[index];
                var position = positions[index];

                if (!session.Grid.IsWalkable(position)
                    || session.Grid.IsBlocked(position)
                    || occupiedPositions.Contains(position))
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

                occupiedPositions.Add(position);
            }

            return true;
        }

        private static DungeonCombatContext GetDungeonCombatContext(
            GameSession gameSession,
            string combatId)
        {
            var context = gameSession.ActiveDungeonCombatContext;

            if (context == null
                || !string.Equals(
                    context.EncounterInstanceId,
                    combatId,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return context;
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
                && dungeon.TryGetPreRolledRewards(
                    combatId,
                    out var rewards))
            {
                return rewards;
            }

            var rolledRewards = rewardSystem.RollEncounterRewards(
                encounter);

            dungeon?.TryStorePreRolledRewards(
                combatId,
                rolledRewards);

            return rolledRewards;
        }
    }
}