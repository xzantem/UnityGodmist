using System;
using Godmist.Data.Configuration;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using UnityEngine;

namespace Godmist.Systems.NPC
{
    /// <summary>
    /// Owns NPC treasury accounting and guild payroll progression. Loyalty is
    /// intentionally not touched here; it is driven by player spending in the
    /// existing NpcLoyaltyService.
    /// </summary>
    public sealed class NpcEconomyService
    {
        public const int DefaultPayoutBaseAmount = 250;

        public int GetInitialGold(NpcInstance npc, int campaignLevel = 1)
        {
            return GetGuildPayoutAmount(npc, campaignLevel);
        }

        public int GetGuildPayoutAmount(NpcInstance npc, int campaignLevel)
        {
            var baseAmount = Mathf.Max(
                1,
                npc?.Definition?.GuildPayoutBaseAmount ?? DefaultPayoutBaseAmount);
            var growthBase = Mathf.Max(
                1.0001f,
                GameDefinesRuntime.GetFloat(
                    GameDefineIds.NpcGuildPayoutGrowthBase,
                    4f));
            var campaignExponent = Mathf.Max(
                0f,
                GameDefinesRuntime.GetFloat(
                    GameDefineIds.NpcGuildPayoutCampaignExponent,
                    .1f));
            var normalizedLevel = Mathf.Max(1, campaignLevel);
            var multiplier = Mathf.Pow(
                growthBase,
                (normalizedLevel - 1) * campaignExponent);

            return Mathf.Max(
                1,
                Mathf.FloorToInt(baseAmount * multiplier));
        }

        public bool TryApplyDungeonProgress(
            GameSession session,
            DungeonInstance dungeon,
            out float progressAwarded,
            out int payoutsGranted)
        {
            progressAwarded = 0f;
            payoutsGranted = 0;

            if (session == null || dungeon == null || dungeon.Floors.Count == 0)
            {
                return false;
            }

            var completedFloorCount = dungeon.GetCompletedFloorCount();
            var creditedFloorCount = dungeon.GuildProgressCreditedFloorCount;
            var newFloorCount = Mathf.Max(creditedFloorCount, completedFloorCount);
            var deltaFloorCount = newFloorCount - creditedFloorCount;

            if (deltaFloorCount <= 0)
            {
                return false;
            }

            if (!dungeon.TrySetGuildProgressCreditedFloorCount(newFloorCount))
            {
                return false;
            }

            var progressPerFullDungeon = Mathf.Clamp01(
                GameDefinesRuntime.GetFloat(
                    GameDefineIds.NpcGuildPayoutProgressPerDungeon,
                    .4f));
            progressAwarded = progressPerFullDungeon
                * deltaFloorCount
                / dungeon.Floors.Count;

            var campaignLevel = GetHighestActivePartyLevel(session);

            foreach (var npc in session.NpcsById.Values)
            {
                if (npc == null) continue;

                npc.GuildPayoutProgress = Mathf.Max(
                    0f,
                    npc.GuildPayoutProgress) + progressAwarded;

                if (npc.GuildPayoutProgress < 1f) continue;

                npc.GuildPayoutProgress = 0f;
                if (TryReceiveGold(
                        npc,
                        GetGuildPayoutAmount(npc, campaignLevel)))
                {
                    payoutsGranted++;
                }
            }

            return true;
        }

        public bool TryReceiveGold(NpcInstance npc, long amount)
        {
            if (npc == null || amount <= 0) return false;

            npc.GoldBalance = SaturatingAdd(npc.GoldBalance, amount);
            npc.TotalGoldReceived = SaturatingAdd(
                npc.TotalGoldReceived,
                amount);
            return true;
        }

        public bool TrySpendGold(NpcInstance npc, long amount)
        {
            if (npc == null || amount <= 0 || npc.GoldBalance < amount)
            {
                return false;
            }

            npc.GoldBalance -= amount;
            npc.TotalGoldSpent = SaturatingAdd(
                npc.TotalGoldSpent,
                amount);
            return true;
        }

        private static int GetHighestActivePartyLevel(GameSession session)
        {
            var highestLevel = 1;

            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (session.CharactersById.TryGetValue(
                        characterId,
                        out var character))
                {
                    highestLevel = Mathf.Max(highestLevel, character.Level);
                }
            }

            return highestLevel;
        }

        private static long SaturatingAdd(long value, long amount)
        {
            if (amount <= 0) return value;
            if (value > long.MaxValue - amount) return long.MaxValue;
            return value + amount;
        }
    }
}
