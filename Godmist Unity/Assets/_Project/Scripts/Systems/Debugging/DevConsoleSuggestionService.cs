using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Debugging
{
    public static class DevConsoleSuggestionService
    {
        private static readonly string[] commandNames =
        {
            "help",
            "state",
            "unit",
            "characters",
            "gold",
            "xp",
            "heal",
            "resource",
            "godmode",
            "sethp",
            "setresource",
            "giveitem",
            "items",
            "itemdefs",
            "encounterdefs",
            "combat"
        };

        public static IReadOnlyList<string> GetSuggestions(
            string commandLine,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog)
        {
            GetTokenContext(
                commandLine,
                out var command,
                out var tokenIndex,
                out var activePrefix);

            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return Array.Empty<string>();
            }

            var candidates = tokenIndex == 0
                ? commandNames.AsEnumerable()
                : GetArgumentCandidates(
                    command,
                    tokenIndex,
                    gameSession,
                    combatSession,
                    contentCatalog);

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Where(candidate => candidate.StartsWith(
                    activePrefix,
                    StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate)
                .ToList();
        }

        public static bool CommandRequiresArgument(string command)
        {
            return command == "gold"
                || command == "xp"
                || command == "heal"
                || command == "resource"
                || command == "godmode"
                || command == "sethp"
                || command == "setresource"
                || command == "giveitem"
                || command == "items"
                || command == "combat";
        }

        private static IEnumerable<string> GetArgumentCandidates(
            string command,
            int tokenIndex,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog)
        {
            if (IsCharacterIdArgument(command, tokenIndex))
            {
                return GetCharacterIds(gameSession, combatSession);
            }

            if (command == "giveitem" && tokenIndex == 2)
            {
                return contentCatalog?.ItemDefinitions?
                    .Where(definition => definition != null)
                    .Select(definition => definition.Id)
                    ?? Enumerable.Empty<string>();
            }

            if (command == "combat" && tokenIndex == 1)
            {
                return contentCatalog?.EncounterDefinitions?
                    .Where(definition => definition != null)
                    .Select(definition => definition.Id)
                    ?? Enumerable.Empty<string>();
            }

            return Enumerable.Empty<string>();
        }

        private static bool IsCharacterIdArgument(
            string command,
            int tokenIndex)
        {
            if (tokenIndex != 1)
            {
                return false;
            }

            return command == "xp"
                || command == "heal"
                || command == "unit"
                || command == "resource"
                || command == "godmode"
                || command == "sethp"
                || command == "setresource"
                || command == "items"
                || command == "giveitem";
        }

        private static IEnumerable<string> GetCharacterIds(
            GameSession gameSession,
            CombatSession combatSession)
        {
            var characterIds = new List<string>();

            if (gameSession != null)
            {
                characterIds.AddRange(gameSession.CharactersById.Keys);
            }

            if (combatSession != null)
            {
                characterIds.AddRange(
                    combatSession.Units
                        .Where(unit => unit?.Character != null)
                        .Select(unit => unit.Character.CharacterId));
            }

            return characterIds;
        }

        private static void GetTokenContext(
            string commandLine,
            out string command,
            out int tokenIndex,
            out string activePrefix)
        {
            command = string.Empty;
            tokenIndex = 0;
            activePrefix = string.Empty;

            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return;
            }

            var endsWithWhitespace = char.IsWhiteSpace(
                commandLine[commandLine.Length - 1]);

            var tokens = commandLine.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                return;
            }

            command = tokens[0].ToLowerInvariant();

            if (endsWithWhitespace)
            {
                tokenIndex = tokens.Length;
                return;
            }

            tokenIndex = tokens.Length - 1;
            activePrefix = tokens[tokenIndex];

            if (tokenIndex == 0)
            {
                command = string.Empty;
            }
        }
    }
}