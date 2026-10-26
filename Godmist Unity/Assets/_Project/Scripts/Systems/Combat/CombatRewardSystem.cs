using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;

namespace Godmist.Systems.Combat
{
    public class CombatRewardSystem
    {
        private readonly Func<double> goldRandom;
        private readonly CharacterProgressionSystem progressionSystem;
        private readonly LootTableSystem lootTableSystem;
        private readonly Func<CharacterDefinition, LootTableData> lootTableResolver;

        public CombatRewardSystem(
            CharacterProgressionSystem progressionSystem = null,
            LootTableSystem lootTableSystem = null,
            Func<CharacterDefinition, LootTableData> lootTableResolver = null,
            Func<double> goldRandom = null)
        {
            this.progressionSystem = progressionSystem ?? new CharacterProgressionSystem();
            this.lootTableSystem = lootTableSystem ?? new LootTableSystem();
            this.lootTableResolver = lootTableResolver;
            this.goldRandom = goldRandom ?? (() => UnityEngine.Random.value);
        }

        public CombatRewardResult RollEncounterRewards(EncounterDefinition encounter)
        {
            return RollEncounterRewards(encounter, null);
        }

        public CombatRewardResult RollEncounterRewards(
            EncounterDefinition encounter,
            Func<EncounterEnemyData, LootTableData> encounterEnemyLootTableResolver, int enemyLevel = 1,
            DungeonInstance dungeon = null)
        {
            var rewards = new CombatRewardResult();

            if (encounter?.Enemies == null) return rewards;

            foreach (var enemyData in encounter.Enemies)
            {
                var definition = enemyData?.CharacterDefinition;

                if (definition == null) continue;

                if (string.IsNullOrWhiteSpace(enemyData.InstanceId)) continue;

                bool isBoss = ScoutCombatSystem.IsBoss(dungeon, encounter, enemyData.InstanceId);
                rewards.EnemyRewards.Add(new CombatEnemyReward
                {
                    EnemyInstanceId = enemyData.InstanceId,
                    ExperienceLevel = Math.Clamp(enemyLevel, 1, 50),
                    IsBoss = isBoss,
                    ExperiencePerActiveCharacter = CalculateEnemyExperience(enemyLevel, enemyLevel, isBoss),
                    Gold = CombatEconomyScaling.RollEnemyGold(enemyLevel, goldRandom()),
                    Items = lootTableSystem.RollDirectLoot(
                        encounterEnemyLootTableResolver?.Invoke(enemyData)
                        ?? lootTableResolver?.Invoke(definition)
                        ?? definition.LootTable)
                });
            }

            return rewards;
        }

        public bool TryGrantVictoryRewards(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (gameSession == null || combatSession == null) return false;
            if (combatSession.Outcome != CombatOutcomeState.Victory) return false;
            if (combatSession.RewardsGranted) return false;

            var rewards = combatSession.Rewards ?? new CombatRewardResult();
            var defeatedEnemyRewards = GetDefeatedEnemyRewards(
                rewards,
                combatSession.DefeatedEnemyInstanceIds);

            foreach (var reward in defeatedEnemyRewards)
                TryGrantDefeatExperience(gameSession, combatSession, reward.EnemyInstanceId);

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    continue;
                }

                if (character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                character.Honor += defeatedEnemyRewards.Count;
            }

            gameSession.SetLastCombatRewards(rewards);

            combatSession.RewardsGranted = true;
            return true;
        }

        public bool TryGrantDefeatExperience(GameSession gameSession, CombatSession combatSession, string enemyId)
        {
            if (gameSession == null || combatSession == null
                || !combatSession.DefeatedEnemyInstanceIds.Contains(enemyId)
                || combatSession.ExperienceGrantedEnemyIds.Contains(enemyId)) return false;
            CombatEnemyReward reward = null;
            foreach (var candidate in combatSession.Rewards?.EnemyRewards ?? new())
                if (candidate?.EnemyInstanceId == enemyId) { reward = candidate; break; }
            // Summons and reinforcement enemies without authored rewards grant nothing.
            if (reward == null) return false;
            combatSession.ExperienceGrantedEnemyIds.Add(enemyId);
            foreach (var id in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(id, out var character)
                    || character.LifeState == CharacterLifeState.Dead) continue;
                var amount = reward.ExperienceLevel > 0
                    ? CalculateEnemyExperience(reward.ExperienceLevel, character.Level, reward.IsBoss)
                    : reward.ExperiencePerActiveCharacter;
                progressionSystem.AddExperience(character, ApplyExperiencePassive(character, amount));
            }
            return true;
        }

        public static int CalculateEnemyExperience(int enemyLevel, int characterLevel, bool isBoss)
            => CharacterProgressionSystem.GetEnemyExperience(enemyLevel, characterLevel) * (isBoss ? 1 : 2);

        public bool TryApplyEscapePenalty(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (gameSession == null || combatSession == null) return false;
            if (combatSession.Outcome != CombatOutcomeState.Escaped) return false;
            if (combatSession.EscapePenaltyApplied) return false;

            IEnumerable<string> characterIds =
                combatSession.HasEscapePenaltyEligibleCharacters
                ? combatSession.EscapePenaltyEligibleCharacterIds
                : gameSession.PlayerParty.ActiveMemberIds;

            foreach (var characterId in characterIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character)
                    || (!combatSession.HasEscapePenaltyEligibleCharacters
                        && character.LifeState == CharacterLifeState.Dead))
                {
                    continue;
                }

                character.Honor -= GameDefinesRuntime.GetInt(
                    GameDefineIds.EscapeHonorPenalty, 5);
            }

            combatSession.EscapePenaltyApplied = true;
            return true;
        }

        private static List<CombatEnemyReward> GetDefeatedEnemyRewards(
            CombatRewardResult rewards,
            IEnumerable<string> defeatedEnemyInstanceIds)
        {
            var result = new List<CombatEnemyReward>();

            if (rewards?.EnemyRewards == null
                || defeatedEnemyInstanceIds == null)
            {
                return result;
            }

            var defeatedIds = new HashSet<string>(defeatedEnemyInstanceIds);

            foreach (var reward in rewards.EnemyRewards)
            {
                if (reward != null
                    && defeatedIds.Contains(reward.EnemyInstanceId))
                {
                    result.Add(reward);
                }
            }

            return result;
        }

        private static int ApplyExperiencePassive(
            CharacterInstance character,
            int experience)
        {
            if (experience <= 0) return experience;

            return Math.Max(0, (int)Math.Round(
                experience * new CombatVariableSystem().GetExperienceMultiplier(character)));
        }
    }
}
