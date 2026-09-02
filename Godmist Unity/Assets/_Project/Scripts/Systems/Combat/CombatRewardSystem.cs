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
using Godmist.Systems.Characters;
using Godmist.Systems.Items;

namespace Godmist.Systems.Combat
{
    public class CombatRewardSystem
    {
        private readonly CharacterProgressionSystem progressionSystem;
        private readonly LootTableSystem lootTableSystem;
        private readonly Func<CharacterDefinition, LootTableData> lootTableResolver;

        public CombatRewardSystem(
            CharacterProgressionSystem progressionSystem = null,
            LootTableSystem lootTableSystem = null,
            Func<CharacterDefinition, LootTableData> lootTableResolver = null)
        {
            this.progressionSystem = progressionSystem ?? new CharacterProgressionSystem();
            this.lootTableSystem = lootTableSystem ?? new LootTableSystem();
            this.lootTableResolver = lootTableResolver;
        }

        public CombatRewardResult RollEncounterRewards(EncounterDefinition encounter)
        {
            return RollEncounterRewards(encounter, null);
        }

        public CombatRewardResult RollEncounterRewards(
            EncounterDefinition encounter,
            Func<EncounterEnemyData, LootTableData> encounterEnemyLootTableResolver)
        {
            var rewards = new CombatRewardResult();

            if (encounter?.Enemies == null) return rewards;

            foreach (var enemyData in encounter.Enemies)
            {
                var definition = enemyData?.CharacterDefinition;

                if (definition == null) continue;

                if (string.IsNullOrWhiteSpace(enemyData.InstanceId)) continue;

                rewards.EnemyRewards.Add(new CombatEnemyReward
                {
                    EnemyInstanceId = enemyData.InstanceId,
                    ExperiencePerActiveCharacter = definition.ExperienceReward,
                    Gold = definition.GoldReward,
                    Items = lootTableSystem.RollLoot(
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

            var experiencePerCharacter = 0;

            foreach (var enemyReward in defeatedEnemyRewards)
            {
                experiencePerCharacter += enemyReward.ExperiencePerActiveCharacter;
            }

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

                progressionSystem.AddExperience(
                    character,
                    ApplyJewelryExperiencePassive(character, experiencePerCharacter));

                character.Honor += defeatedEnemyRewards.Count;
            }

            gameSession.SetLastCombatRewards(rewards);

            combatSession.RewardsGranted = true;
            return true;
        }

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

        private static int ApplyJewelryExperiencePassive(
            CharacterInstance character,
            int experience)
        {
            if (experience <= 0) return experience;

            var bonus = 0f;
            foreach (var item in character?.Equipment?.GetEquippedItems()
                         ?? new List<Runtime.Items.EquipmentItemInstance>())
            {
                if (item is Runtime.Items.JewelryItemInstance jewelry
                    && jewelry.PassiveType == JewelryPassiveType.ExperienceGainPercent)
                {
                    bonus += jewelry.PassiveValue;
                }
            }

            return Math.Max(0, (int)Math.Round(experience * (1f + bonus)));
        }
    }
}
