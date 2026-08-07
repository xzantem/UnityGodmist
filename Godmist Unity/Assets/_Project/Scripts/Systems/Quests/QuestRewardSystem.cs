using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;

namespace Godmist.Systems.Quests
{
    public class QuestRewardSystem
    {
        private readonly CharacterProgressionSystem progressionSystem;
        private readonly ItemInstanceFactory itemInstanceFactory;

        public QuestRewardSystem(
            CharacterProgressionSystem nextProgressionSystem = null,
            ItemInstanceFactory nextItemInstanceFactory = null)
        {
            progressionSystem = nextProgressionSystem
                ?? new CharacterProgressionSystem();

            itemInstanceFactory = nextItemInstanceFactory
                ?? new ItemInstanceFactory();
        }

        public QuestRewardResult GrantStageCompletionRewards(
            GameSession gameSession,
            QuestInstance quest,
            QuestStageDefinition stage)
        {
            var result = new QuestRewardResult();

            if (gameSession == null
                || quest == null
                || stage?.CompletionRewards == null)
            {
                return result;
            }

            for (var index = 0; index < stage.CompletionRewards.Count; index++)
            {
                var reward = stage.CompletionRewards[index];

                if (!CanGrantReward(reward))
                {
                    continue;
                }

                var rewardKey = $"{stage.StageId}:{index}";

                if (!quest.TryClaimReward(rewardKey))
                {
                    continue;
                }

                GrantReward(gameSession, reward, result);
            }

            return result;
        }

        private void GrantReward(
            GameSession gameSession,
            QuestRewardDefinition reward,
            QuestRewardResult result)
        {
            switch (reward.RewardType)
            {
                case QuestRewardType.Experience:
                    GrantExperience(gameSession, reward.Amount);
                    result.ExperiencePerActiveCharacter += reward.Amount;
                    break;

                case QuestRewardType.Gold:
                    gameSession.TryAddGold(reward.Amount);
                    result.Gold += reward.Amount;
                    break;

                case QuestRewardType.Item:
                    var item = itemInstanceFactory.Create(reward.ItemDefinition);
                    gameSession.AddPendingLoot(new[] { item });
                    result.Items.Add(item);
                    break;

                case QuestRewardType.Honor:
                    GrantHonor(gameSession, reward.Amount);
                    result.HonorPerActiveCharacter += reward.Amount;
                    break;

                case QuestRewardType.WorldFlag:
                    gameSession.WorldState.TrySetFlag(
                        reward.WorldFlagId,
                        reward.WorldFlagValue);

                    result.UpdatedWorldFlagIds.Add(reward.WorldFlagId);
                    break;
            }
        }

        private void GrantExperience(
            GameSession gameSession,
            int experienceAmount)
        {
            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    continue;
                }

                progressionSystem.AddExperience(character, experienceAmount);
            }
        }

        private static void GrantHonor(
            GameSession gameSession,
            int honorAmount)
        {
            foreach (var characterId in gameSession.PlayerParty.ActiveMemberIds)
            {
                if (!gameSession.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    continue;
                }

                character.Honor += honorAmount;
            }
        }

        private static bool CanGrantReward(QuestRewardDefinition reward)
        {
            if (reward == null)
            {
                return false;
            }

            return reward.RewardType switch
            {
                QuestRewardType.Experience => reward.Amount > 0,
                QuestRewardType.Gold => reward.Amount > 0,
                QuestRewardType.Item => reward.ItemDefinition != null,
                QuestRewardType.Honor => reward.Amount > 0,
                QuestRewardType.WorldFlag =>
                    !string.IsNullOrWhiteSpace(reward.WorldFlagId),
                _ => false
            };
        }
    }
}