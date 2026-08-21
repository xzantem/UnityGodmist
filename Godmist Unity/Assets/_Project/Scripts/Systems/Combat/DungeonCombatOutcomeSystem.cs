using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatOutcomeSystem
    {
        private readonly DungeonCombatStateSyncSystem stateSyncSystem;

        public DungeonCombatOutcomeSystem(
            DungeonCombatStateSyncSystem stateSyncSystem = null)
        {
            this.stateSyncSystem =
                stateSyncSystem ?? new DungeonCombatStateSyncSystem();
        }

        public bool TryApplyOutcome(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (combatSession == null
                || (combatSession.Outcome != CombatOutcomeState.Victory
                    && combatSession.Outcome != CombatOutcomeState.Escaped))
            {
                return false;
            }

            if (gameSession == null
                || gameSession.ActiveDungeon == null
                || gameSession.ActiveDungeonCombatContext == null)
            {
                return false;
            }

            var context = gameSession.ActiveDungeonCombatContext;

            if (!string.Equals(
                    combatSession.CombatId,
                    context.EncounterInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!stateSyncSystem.TrySyncDoorStates(gameSession, combatSession)
                || !stateSyncSystem.TrySyncDefeatedEnemyStates(gameSession, combatSession))
            {
                return false;
            }

            if (combatSession.Outcome == CombatOutcomeState.Escaped)
            {
                return true;
            }

            var dungeon = gameSession.ActiveDungeon;

            if (!dungeon.IsEncounterCleared(context.EncounterInstanceId)
                && !dungeon.TryMarkEncounterCleared(
                    context.EncounterInstanceId))
            {
                return false;
            }

            return TryCreateEnemyBodies(
                dungeon,
                context,
                combatSession);
        }

        private static bool TryCreateEnemyBodies(
            DungeonInstance dungeon,
            DungeonCombatContext context,
            CombatSession combatSession)
        {
            if (dungeon == null
                || context == null
                || combatSession?.Rewards?.EnemyRewards == null
                || !dungeon.TryGetFloor(context.FloorIndex, out var floor)
                || !floor.TryGetEncounter(
                    context.EncounterInstanceId,
                    out var encounter))
            {
                return true;
            }

            var rewardsByEnemyId = new Dictionary<string, CombatEnemyReward>(
                StringComparer.Ordinal);

            foreach (var reward in combatSession.Rewards.EnemyRewards)
            {
                if (reward == null
                    || string.IsNullOrWhiteSpace(reward.EnemyInstanceId))
                {
                    continue;
                }

                rewardsByEnemyId.TryAdd(reward.EnemyInstanceId, reward);
            }

            var bodyOffsetIndex = 0;

            foreach (var enemyInstanceId in combatSession.DefeatedEnemyInstanceIds)
            {
                if (!rewardsByEnemyId.TryGetValue(
                        enemyInstanceId,
                        out var reward))
                {
                    continue;
                }

                if (encounter.IsEnemyBodyGenerated(enemyInstanceId))
                {
                    continue;
                }

                var bodyInstanceId =
                    $"{encounter.InstanceId}.body.{enemyInstanceId}";

                if (floor.TryGetEnemyBody(bodyInstanceId, out _))
                {
                    encounter.TryMarkEnemyBodyGenerated(enemyInstanceId);
                    continue;
                }

                if (reward.Gold < 0)
                {
                    return false;
                }

                var hasItems = false;

                foreach (var item in reward.Items
                             ?? new List<Runtime.Items.ItemInstance>())
                {
                    if (item != null)
                    {
                        hasItems = true;
                        break;
                    }
                }

                if (reward.Gold == 0 && !hasItems)
                {
                    encounter.TryMarkEnemyBodyGenerated(enemyInstanceId);
                    continue;
                }

                var body = new DungeonEnemyBodyInstance(
                    bodyInstanceId,
                    enemyInstanceId,
                    GetBodyPosition(
                        context.EnemyAnchorWorldPosition,
                        bodyOffsetIndex++),
                    reward.Gold,
                    reward.Items);

                if (!floor.TryAddEnemyBody(body))
                {
                    return false;
                }

                encounter.TryMarkEnemyBodyGenerated(enemyInstanceId);
            }

            return true;
        }

        private static UnityEngine.Vector2Int GetBodyPosition(
            UnityEngine.Vector2Int anchorPosition,
            int bodyOffsetIndex)
        {
            return anchorPosition + new UnityEngine.Vector2Int(
                bodyOffsetIndex % 2,
                bodyOffsetIndex / 2);
        }
    }
}
