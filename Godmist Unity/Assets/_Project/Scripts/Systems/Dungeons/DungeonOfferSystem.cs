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
        public static DungeonLength LengthForSeed(int seed) => (DungeonLength)new Random(unchecked(seed ^ 0x4c454e)).Next(1,4);
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
            if (pending != null && pending.Length == DungeonLength.Unspecified)
            {
                var existing = board.Offers.FirstOrDefault(o => o.DefinitionId == pending.DefinitionId
                    && o.FixedLevel == pending.Level);
                var length = existing != null && existing.Length != DungeonLength.Unspecified
                    ? existing.Length : LengthForSeed(existing?.Seed
                        ?? Seed(session.CampaignId, board.Generation, pending.Level));
                board.TryAssignLegacyBossLength(pending.DefinitionId, pending.Level, length);
                pending = FirstAvailableBoss(board, pool);
            }
            if (board.Offers.Count != 0 && board.Offers.Any(offer =>
                pool.Any(definition => definition.Id == offer.DefinitionId && !IsRegular(definition))
                && !board.BossOpportunities.Any(boss => boss.DefinitionId == offer.DefinitionId
                    && boss.Level == OfferLevel(PartyLevel(session), offer))))
                board.InvalidateOffers();
            if (board.Offers.Count != 0 && pending != null
                && !board.Offers.Any(offer => offer.DefinitionId == pending.DefinitionId
                    && offer.FixedLevel == pending.Level)) board.InvalidateOffers();
            if (board.Offers.Count != 0)
            {
                // One-time upgrade of old board offers, preserving identity, seed,
                // selection and queued boss. Reopening never rolls another length.
                if (board.Offers.Any(o => o.Length == DungeonLength.Unspecified))
                    board.TryRestore(board.Generation, board.SelectedIndex, board.Offers.Select(o =>
                        new DungeonExpeditionOffer(o.DefinitionId,o.Seed,o.LevelOffset,o.FixedLevel,
                            o.Length == DungeonLength.Unspecified
                                ? (pending != null && o.DefinitionId == pending.DefinitionId
                                    && o.FixedLevel == pending.Level ? pending.Length : LengthForSeed(o.Seed))
                                : o.Length)).ToArray());
                return EnsureUniqueOffers(session, pool, regular);
            }

            var random = new Random(Seed(session.CampaignId, board.Generation, 0));
            var shuffled = regular.ToArray();
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                var swap = random.Next(i + 1);
                (shuffled[i], shuffled[swap]) = (shuffled[swap], shuffled[i]);
            }

            var offers = new List<DungeonExpeditionOffer>();
            var seeds = new HashSet<int>();
            var used = new HashSet<(int, DungeonTheme, DungeonLength)>();
            var average = PartyLevel(session);
            if (pending != null)
            {
                int bossSeed;
                do { bossSeed = random.Next(); } while (!seeds.Add(bossSeed));
                offers.Add(new DungeonExpeditionOffer(pending.DefinitionId, bossSeed, 0, pending.Level, pending.Length));
                used.Add((pending.Level, pool.First(d=>d.Id==pending.DefinitionId).Theme, pending.Length));
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
                offers.Add(UniqueRegularOffer(definition, seed, offset, LengthForSeed(seed), regular, used, average));
            }
            return board.TryRestore(board.Generation, 0, offers);
        }

        private static DungeonExpeditionOffer UniqueRegularOffer(DungeonDefinition preferred, int seed, int offset,
            DungeonLength length, DungeonDefinition[] regular, HashSet<(int, DungeonTheme, DungeonLength)> used, int average)
        {
            // Finite combinations, including clamped/folded levels at 1 and 50.
            // Prefer the requested offset so the ordinary easier/harder offers remain.
            var offsets = offset == 0 ? new[] { 0 } : new[] { offset, -1, 1, -2, 2, -3, 3 }.Distinct();
            foreach (var candidateOffset in offsets)
            foreach (var definition in new[] { preferred }.Concat(regular).Distinct())
            for (var step=0;step<3;step++)
            {
                var candidateLength=(DungeonLength)(1+((int)length-1+step)%3);
                if (used.Add((OfferLevel(average,candidateOffset), definition.Theme, candidateLength)))
                    return new DungeonExpeditionOffer(definition.Id,seed,candidateOffset,length:candidateLength);
            }
            throw new InvalidOperationException("Insufficient unique expedition combinations.");
        }

        private static bool EnsureUniqueOffers(GameSession session, DungeonDefinition[] pool, DungeonDefinition[] regular)
        {
            var board=session.DungeonOffers; var average=PartyLevel(session);
            var used=new HashSet<(int, DungeonTheme, DungeonLength)>(); var next=new List<DungeonExpeditionOffer>();
            foreach (var offer in board.Offers)
            {
                var definition=pool.FirstOrDefault(d=>d.Id==offer.DefinitionId);
                if (definition==null) return false;
                if (used.Add((OfferLevel(average,offer),definition.Theme,offer.Length))) next.Add(offer);
                else next.Add(UniqueRegularOffer(regular.FirstOrDefault(d=>d.Id==offer.DefinitionId)??regular[0],
                    offer.Seed,offer.LevelOffset,offer.Length,regular,used,average));
            }
            return board.TryRestore(board.Generation,board.SelectedIndex,next);
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
                        QueueBoss(board, pool, otherTheme, HighestCharacterLevel(session), false, session.CampaignId);
                }
            }

            board.CompleteExpedition();
            board.RecordCompletedExpedition();
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
                    QueueBoss(board, pool, theme, highest, false, session.CampaignId);
            var milestoneReached = false;
            for (var level = board.HighestMilestoneProcessed + 10; level <= highest; level += 10)
            {
                var random = new Random(Seed(session.CampaignId, level, 0));
                var theme = themes[random.Next(themes.Length)];
                board.SetDanger(theme, 100);
                QueueBoss(board, pool, theme, level, true, session.CampaignId);
                board.MarkMilestoneProcessed(level);
                milestoneReached = true;
            }
            if (milestoneReached && board.Offers.Count != 0) board.InvalidateOffers();
        }

        private static void QueueBoss(DungeonOfferBoard board, DungeonDefinition[] pool,
            DungeonTheme theme, int level, bool exactLevel, string campaignId)
        {
            var candidates = pool.Where(definition => definition.Theme == theme && !IsRegular(definition))
                .SelectMany(definition => definition.FixedOfferLevels.Select(fixedLevel =>
                    (definition, fixedLevel)))
                .Where(candidate => !exactLevel || candidate.fixedLevel == level)
                .OrderBy(candidate => Math.Abs(candidate.fixedLevel - level))
                .ThenBy(candidate => candidate.fixedLevel)
                .ThenBy(candidate => candidate.definition.Id, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0) return;
            // Preserve the existing nearest-level rule; randomize only the boss
            // identity at that level. Queued choices survive board regeneration/save.
            var selectedLevel = candidates[0].fixedLevel;
            if (board.BossOpportunities.Any(boss => boss.Theme == theme && boss.Level == selectedLevel)) return;
            var eligible = candidates.Where(candidate => candidate.fixedLevel == selectedLevel).ToArray();
            var random = new Random(Seed(campaignId, board.Generation, unchecked(level * 31 + (int)theme)));
            var boss = eligible[random.Next(eligible.Length)];
            board.QueueBoss(theme, boss.definition.Id, boss.fixedLevel, LengthForSeed(random.Next()));
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
