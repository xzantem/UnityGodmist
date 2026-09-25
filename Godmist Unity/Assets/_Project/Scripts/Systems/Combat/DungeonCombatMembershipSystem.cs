using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Combat
{
    // Runtime identity is separate from authored encounter IDs and persisted reward IDs.
    public static class DungeonCombatMembershipSystem
    {
        public static string EnemyKey(string encounterId, string enemyId)
            => $"enemy:{encounterId.Length}:{encounterId}:{enemyId}";

        public static void RegisterEnemy(CombatSession session, string encounterId, string enemyId,
            CombatRewardResult rewards, BattleUnit unit)
        {
            var key = EnemyKey(encounterId, enemyId);
            unit.Character.CharacterId = key;
            if (!session.EnemySources.TryAdd(key, (encounterId, enemyId))) return;
            var reward = rewards?.EnemyRewards.FirstOrDefault(value => value.EnemyInstanceId == enemyId);
            if (reward != null) session.Rewards.EnemyRewards.Add(CopyReward(reward, key));
        }

        private static CombatEnemyReward CopyReward(CombatEnemyReward source, string id) => new()
        {
            EnemyInstanceId = id, ExperiencePerActiveCharacter = source.ExperiencePerActiveCharacter,
            ExperienceLevel = source.ExperienceLevel, Gold = source.Gold, Items = new(source.Items)
        };

        // Projection is only for the existing persistence/body owner, never another running battle.
        public static IEnumerable<CombatSession> EncounterResults(DungeonInstance dungeon, CombatSession session)
        {
            foreach (var group in session.EnemySources.GroupBy(pair => pair.Value.EncounterId))
            {
                if (!dungeon.TryGetFloor(session.DungeonContext.FloorIndex, out var floor)
                    || !floor.TryGetEncounter(group.Key, out var encounter)) continue;
                var original = session.DungeonContext;
                var context = new DungeonCombatContext(original.DungeonId, original.FloorIndex,
                    group.Key, encounter.RoomId, original.PartyAnchorWorldPosition, original.EnemyAnchorWorldPosition,
                    original.WorldCells, original.WallEdges, original.MapObjects, original.DungeonLevel,
                    floor.Map.WallTypes, original.DifficultTerrainCells, floor.Map, original.IsFloorWide);
                var result = new CombatSession { CombatId = group.Key, DungeonContext = context, Units = session.Units };
                foreach (var pair in group)
                {
                    if (!session.DefeatedEnemyInstanceIds.Contains(pair.Key)) continue;
                    if (session.TryGetEnemyDeathPosition(pair.Key, out var position))
                        result.TryMarkEnemyDefeated(pair.Value.EnemyId, position);
                    else result.DefeatedEnemyInstanceIds.Add(pair.Value.EnemyId);
                    var reward = session.Rewards.EnemyRewards.FirstOrDefault(value => value.EnemyInstanceId == pair.Key);
                    if (reward != null) result.Rewards.EnemyRewards.Add(CopyReward(reward, pair.Value.EnemyId));
                }
                yield return result;
            }
        }

        public static bool TryMerge(CombatSession destination, CombatSession source)
        {
            if (destination == null || source == null || destination == source
                || !destination.IsDungeonWide || !source.IsDungeonWide
                || destination.Outcome != CombatOutcomeState.Ongoing || source.Outcome != CombatOutcomeState.Ongoing
                || destination.DungeonContext?.DungeonId != source.DungeonContext?.DungeonId
                || destination.DungeonContext?.FloorIndex != source.DungeonContext?.FloorIndex
                || destination.DungeonContext == null || source.DungeonContext == null
                || destination.DungeonContext.WorldBounds != source.DungeonContext.WorldBounds
                || destination.Units.Any(unit => source.Units.Any(other => other.Character.CharacterId == unit.Character.CharacterId))) return false;

            destination.Units.AddRange(source.Units);
            destination.Contact?.Absorb(source.Contact);
            destination.TurnParticipants.AddRange(source.TurnParticipants);
            destination.Summons.AddRange(source.Summons);
            destination.SummonedObjects.AddRange(source.SummonedObjects);
            destination.ScheduledEffects.AddRange(source.ScheduledEffects);
            foreach (var item in source.MapObjects)
                if (!destination.MapObjects.Any(existing => existing.ObjectId == item.ObjectId)) destination.MapObjects.Add(item);
            foreach (var pair in source.EnemySources) destination.EnemySources.Add(pair.Key, pair.Value);
            destination.Rewards.EnemyRewards.AddRange(source.Rewards.EnemyRewards);
            foreach (var id in source.DefeatedEnemyInstanceIds)
            {
                if (source.TryGetEnemyDeathPosition(id, out var position)) destination.TryMarkEnemyDefeated(id, position);
                else destination.DefeatedEnemyInstanceIds.Add(id);
            }
            foreach (var pair in source.DepartedUnits) destination.DepartedUnits[pair.Key] = pair.Value;
            destination.EscapePenaltyEligibleCharacterIds.UnionWith(source.EscapePenaltyEligibleCharacterIds);
            destination.HasEscapePenaltyEligibleCharacters |= source.HasEscapePenaltyEligibleCharacters;
            destination.SuspendedTurns.UnionWith(source.SuspendedTurns);
            if (source.ActiveParticipant != null) destination.SuspendedTurns.Add(source.ActiveParticipant);
            CombatTurnSystem.ReorderParticipants(destination);
            // All AV/AP/status objects are transferred as-is, including a partly spent active turn.
            return true;
        }
    }
}
