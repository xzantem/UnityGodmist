using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Dungeons
{
    public static class DungeonOfferSystem
    {
        public static int PartyLevel(GameSession session)
        {
            var levels = session.PlayerParty.ActiveMemberIds.Where(session.CharactersById.ContainsKey)
                .Select(id => Math.Clamp(session.CharactersById[id].Level, 1, 50)).ToArray();
            return levels.Length == 0 ? 1 : (int)Math.Round(levels.Average(), MidpointRounding.AwayFromZero);
        }

        public static int HighestCharacterLevel(GameSession session) =>
            session.CharactersById.Count == 0 ? 1 : session.CharactersById.Values.Max(character =>
                Math.Clamp(character.Level, 1, 50));

        public static int OfferLevel(int average, int offset)
        {
            average = Math.Clamp(average, 1, 50);
            if (offset == 0) return average;
            var direction = offset < 0 ? -1 : 1;
            if (average == 1 && direction < 0 || average == 50 && direction > 0) direction = -direction;
            var distance = Math.Min(Math.Abs(offset), direction < 0 ? average - 1 : 50 - average);
            return average + direction * distance;
        }

        public static int OfferLevel(int average, DungeonExpeditionOffer offer) =>
            offer.FixedLevel > 0 ? offer.FixedLevel : OfferLevel(average, offer.LevelOffset);

        public static string DangerBand(int value) => value switch
        {
            < 40 => "Low",
            < 70 => "Medium",
            < 100 => "High",
            _ => "Max"
        };

        public static bool EnsureOffers(GameSession session, IEnumerable<DungeonDefinition> candidates)
        {
            if (session == null || session.ActiveDungeon != null) return false;
            var pool = OfferableDefinitions(candidates);
            var regular = pool.Where(IsRegular).ToArray();
            if (regular.Length == 0) return false;
            AdvanceMilestones(session, pool, regular);
            var board = session.DungeonOffers;
            var pending = FirstAvailableBoss(board, pool);
            if (board.Offers.Count != 0 && board.Offers.Any(offer =>
                pool.Any(definition => definition.Id == offer.DefinitionId && !IsRegular(definition))
                && !board.BossOpportunities.Any(boss => boss.DefinitionId == offer.DefinitionId
                    && boss.Level == OfferLevel(PartyLevel(session), offer))))
                board.InvalidateOffers();
            if (board.Offers.Count != 0 && pending != null
                && !board.Offers.Any(offer => offer.DefinitionId == pending.DefinitionId
                    && offer.FixedLevel == pending.Level)) board.InvalidateOffers();
            if (board.Offers.Count != 0) return true;

            var random = new Random(Seed(session.CampaignId, board.Generation, 0));
            var shuffled = regular.ToArray();
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                var swap = random.Next(i + 1);
                (shuffled[i], shuffled[swap]) = (shuffled[swap], shuffled[i]);
            }

            var offers = new List<DungeonExpeditionOffer>();
            var seeds = new HashSet<int>();
            if (pending != null)
            {
                int bossSeed;
                do { bossSeed = random.Next(); } while (!seeds.Add(bossSeed));
                offers.Add(new DungeonExpeditionOffer(pending.DefinitionId, bossSeed, 0, pending.Level));
            }
            var firstRegularIndex = offers.Count;
            for (var i = firstRegularIndex; i < 6; i++)
            {
                int seed;
                do { seed = random.Next(); } while (!seeds.Add(seed));
                var regularPosition = i - firstRegularIndex;
                var offset = regularPosition == 0 ? 0 : (regularPosition == 1 ? -1 : regularPosition == 2 ? 1
                    : random.Next(2) == 0 ? -1 : 1) * random.Next(1, 4);
                var definition = regularPosition < shuffled.Length
                    ? shuffled[regularPosition]
                    : regular[random.Next(regular.Length)];
                offers.Add(new DungeonExpeditionOffer(definition.Id, seed, offset));
            }
            return board.TryRestore(board.Generation, 0, offers);
        }

        public static void CompleteExpedition(GameSession session, DungeonInstance dungeon,
            IEnumerable<DungeonDefinition> candidates)
        {
            if (session == null || dungeon?.Definition == null) return;
            var pool = OfferableDefinitions(candidates);
            var regular = pool.Where(IsRegular).ToArray();
            var board = session.DungeonOffers;
            var theme = dungeon.Definition.Theme;
            var completedBoss = board.CompleteBoss(dungeon.DungeonId, dungeon.DungeonLevel);
            if (!completedBoss && !IsRegular(dungeon.Definition)
                && dungeon.Definition.FixedOfferLevels.Contains(dungeon.DungeonLevel))
            {
                // An expedition already in progress in an older save has no Danger opportunity yet.
                board.SetDanger(theme, 0);
                board.MarkMilestoneProcessed(dungeon.DungeonLevel);
                completedBoss = true;
            }
            if (!completedBoss && HighestCharacterLevel(session) >= 8)
            {
                board.SetDanger(theme, board.GetDanger(theme) - 30);
                foreach (var otherTheme in regular.Select(definition => definition.Theme).Distinct()
                    .Where(candidate => candidate != theme))
                {
                    var previous = board.GetDanger(otherTheme);
                    var random = new Random(Seed(session.CampaignId, board.Generation + 1, (int)otherTheme));
                    board.SetDanger(otherTheme, previous + random.Next(10, 21));
                    if (previous < 100 && board.GetDanger(otherTheme) == 100)
                        QueueBoss(board, pool, otherTheme, HighestCharacterLevel(session), false);
                }
            }

            board.CompleteExpedition();
            AdvanceMilestones(session, pool, regular);
        }

        private static void AdvanceMilestones(GameSession session, DungeonDefinition[] pool,
            DungeonDefinition[] regular)
        {
            var board = session.DungeonOffers;
            var themes = regular.Select(definition => definition.Theme).Distinct().OrderBy(theme => theme).ToArray();
            if (themes.Length == 0) return;
            var highest = HighestCharacterLevel(session);
            foreach (var theme in themes)
                if (board.GetDanger(theme) == 100
                    && !board.BossOpportunities.Any(boss => boss.Theme == theme))
                    QueueBoss(board, pool, theme, highest, false);
            var milestoneReached = false;
            for (var level = board.HighestMilestoneProcessed + 10; level <= highest; level += 10)
            {
                var random = new Random(Seed(session.CampaignId, level, 0));
                var theme = themes[random.Next(themes.Length)];
                board.SetDanger(theme, 100);
                QueueBoss(board, pool, theme, level, true);
                board.MarkMilestoneProcessed(level);
                milestoneReached = true;
            }
            if (milestoneReached && board.Offers.Count != 0) board.InvalidateOffers();
        }

        private static void QueueBoss(DungeonOfferBoard board, DungeonDefinition[] pool,
            DungeonTheme theme, int level, bool exactLevel)
        {
            var boss = pool.Where(definition => definition.Theme == theme && !IsRegular(definition))
                .SelectMany(definition => definition.FixedOfferLevels.Select(fixedLevel =>
                    (definition, fixedLevel)))
                .Where(candidate => !exactLevel || candidate.fixedLevel == level)
                .OrderBy(candidate => Math.Abs(candidate.fixedLevel - level))
                .ThenBy(candidate => candidate.fixedLevel)
                .ThenBy(candidate => candidate.definition.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (boss.definition != null)
                board.QueueBoss(theme, boss.definition.Id, boss.fixedLevel);
        }

        private static DungeonBossOpportunity FirstAvailableBoss(DungeonOfferBoard board,
            DungeonDefinition[] pool) => board.BossOpportunities
                .Where(boss => pool.Any(definition => definition.Id == boss.DefinitionId
                    && definition.Theme == boss.Theme && !IsRegular(definition)
                    && definition.FixedOfferLevels.Contains(boss.Level)))
                .OrderBy(boss => boss.Level).FirstOrDefault();

        private static bool IsRegular(DungeonDefinition definition) =>
            definition.FixedOfferLevels == null || definition.FixedOfferLevels.Count == 0;

        private static DungeonDefinition[] OfferableDefinitions(IEnumerable<DungeonDefinition> candidates) =>
            candidates?.Where(definition => definition != null && definition.IsProcedural
                && !definition.IsStoryDungeon && definition.IsAvailableAsOffer
                && !string.IsNullOrWhiteSpace(definition.Id))
                .GroupBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(group => group.First()).OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<DungeonDefinition>();

        private static int Seed(string campaignId, int sequence, int salt)
        {
            uint hash = 2166136261;
            foreach (var character in campaignId ?? string.Empty)
                hash = unchecked((hash ^ character) * 16777619);
            return unchecked((int)hash + sequence * 16777619 + salt * 7919);
        }
    }
}
