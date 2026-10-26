using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Data.Items;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.Combat;
using Godmist.Data.World;
using Godmist.Data.Configuration;

namespace Godmist.Systems.Quests
{
    public class QuestRewardSystem
    {
        private readonly CharacterProgressionSystem progressionSystem;
        private readonly ItemInstanceFactory itemInstanceFactory;
        private readonly GameContentCatalog contentCatalog;
        private readonly Random random;

        public QuestRewardSystem(
            CharacterProgressionSystem nextProgressionSystem = null,
            ItemInstanceFactory nextItemInstanceFactory = null,
            GameContentCatalog nextContentCatalog = null,
            Random nextRandom = null)
        {
            progressionSystem = nextProgressionSystem
                ?? new CharacterProgressionSystem();

            itemInstanceFactory = nextItemInstanceFactory
                ?? new ItemInstanceFactory();
            contentCatalog = nextContentCatalog;
            random = nextRandom ?? new Random();
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

        public QuestRewardResult GrantRadiantRewards(
            GameSession gameSession,
            int completedDungeonLevel,
            bool isBossMission,
            string rewardVariant)
        {
            if (gameSession == null || completedDungeonLevel < 1 || completedDungeonLevel > 50)
                return new QuestRewardResult();
            return GrantRadiantRewards(gameSession, CreateRadiantRewardOffer(
                completedDungeonLevel, isBossMission, rewardVariant));
        }

        public RadiantRewardOffer CreateRadiantRewardOffer(
            int level, bool isBossMission, string rewardVariant)
        {
            var baseExperience = CharacterProgressionSystem.GetStandardMissionExperience(level);
            var goldPerEnemy = CombatEconomyScaling.Scale(
                GameDefinesRuntime.GetInt(GameDefineIds.EnemyGoldBase, 10),
                level);
            var baseGold = (int)Math.Min(int.MaxValue, (long)goldPerEnemy * (isBossMission ? 8 : 4));
            var experience = (int)Math.Min(int.MaxValue, Math.Round(baseExperience
                * (rewardVariant == "experience" ? 1.25d : rewardVariant == "gold" ? .75d : 1d)));
            var gold = (int)Math.Min(int.MaxValue, Math.Round(baseGold
                * (rewardVariant == "gold" ? 1.25d : rewardVariant == "experience" ? .75d : 1d)));
            var honor = isBossMission ? 3 : 2;

            var bonusItem = random.Next(10) == 0 ? RollBonusItem(level) : null;
            return new RadiantRewardOffer(level, experience, gold, honor, bonusItem);
        }

        public QuestRewardResult GrantRadiantRewards(
            GameSession gameSession, RadiantRewardOffer offer)
        {
            var result = new QuestRewardResult();
            if (gameSession == null || offer == null) return result;

            GrantExperience(gameSession, offer.Experience);
            gameSession.TryAddGold(offer.Gold);
            GrantHonor(gameSession, offer.Honor);
            result.ExperiencePerActiveCharacter = offer.Experience;
            result.Gold = offer.Gold;
            result.HonorPerActiveCharacter = offer.Honor;

            if (offer.BonusItem != null)
            {
                gameSession.AddPendingLoot(new[] { offer.BonusItem });
                result.Items.Add(offer.BonusItem);
            }
            return result;
        }

        private Godmist.Runtime.Items.ItemInstance RollBonusItem(int level)
        {
            if (contentCatalog?.ItemDefinitions == null) return null;
            var candidates = contentCatalog.ItemDefinitions.Where(item =>
                item is JewelryItemDefinition && level >= 10
                || item is GalduryteItemDefinition && level >= 11 && level <= 20
                || item is EquipmentItemDefinition equipment
                    && item is not UniqueEquipmentDefinition
                    && !equipment.IsStarterEquipment
                    && equipment.ItemType is ItemType.Weapon or ItemType.Armor).ToList();
            if (candidates.Count == 0) return null;
            var generator = new LootTableSystem(random,
                contentCatalog.EquipmentPartDefinitions, lootLevel: level);
            for (var attempt = 0; attempt < candidates.Count; attempt++)
            {
                var index = random.Next(candidates.Count);
                var item = generator.GenerateSingleItem(candidates[index]);
                if (item is Godmist.Runtime.Items.JewelryItemInstance jewelry
                    && string.IsNullOrWhiteSpace(jewelry.MetalId)) item = null;
                if (item != null) return item;
                candidates.RemoveAt(index);
            }
            return null;
        }

        private void GrantReward(
            GameSession gameSession,
            QuestRewardDefinition reward,
            QuestRewardResult result)
        {
            switch (reward.RewardType)
            {
                case QuestRewardType.Experience:
                    var experience = reward.StandardExperienceLevel > 0
                        ? CharacterProgressionSystem.GetStandardMissionExperience(reward.StandardExperienceLevel)
                        : reward.Amount;
                    GrantExperience(gameSession, experience);
                    result.ExperiencePerActiveCharacter += experience;
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
                QuestRewardType.Experience => reward.StandardExperienceLevel > 0 || reward.Amount > 0,
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
