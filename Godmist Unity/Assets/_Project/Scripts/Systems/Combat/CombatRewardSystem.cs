using Godmist.Core.Enums;
using Godmist.Data.Combat;
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

        public CombatRewardSystem(
            CharacterProgressionSystem progressionSystem = null,
            LootTableSystem lootTableSystem = null)
        {
            this.progressionSystem = progressionSystem ?? new CharacterProgressionSystem();
            this.lootTableSystem = lootTableSystem ?? new LootTableSystem();
        }

        public CombatRewardResult RollEncounterRewards(EncounterDefinition encounter)
        {
            var rewards = new CombatRewardResult();

            if (encounter?.Enemies == null) return rewards;

            foreach (var enemyData in encounter.Enemies)
            {
                var definition = enemyData?.CharacterDefinition;

                if (definition == null) continue;

                rewards.ExperiencePerActiveCharacter += definition.ExperienceReward;
                rewards.Gold += definition.GoldReward;
                rewards.Items.AddRange(lootTableSystem.RollLoot(definition.LootTable));
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

            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    continue;
                }

                progressionSystem.AddExperience(
                    character,
                    rewards.ExperiencePerActiveCharacter);
            }

            gameSession.TryAddGold(rewards.Gold);
            gameSession.AddPendingLoot(rewards.Items);
            gameSession.SetLastCombatRewards(rewards);

            combatSession.RewardsGranted = true;
            return true;
        }
    }
}