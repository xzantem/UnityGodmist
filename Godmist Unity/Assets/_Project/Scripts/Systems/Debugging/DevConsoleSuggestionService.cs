using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Items;
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
            "createitem",
            "createconsumable",
            "items",
            "inspectitem",
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
                    commandLine,
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
                || command == "createitem"
                || command == "createconsumable"
                || command == "inspectitem"
                || command == "items"
                || command == "combat";
        }

        private static IEnumerable<string> GetArgumentCandidates(
            string commandLine,
            string command,
            int tokenIndex,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog)
        {
            var commandTokens = commandLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();
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

            if (command == "createitem" && tokenIndex == 1)
            {
                return contentCatalog?.ItemDefinitions?
                    .Where(definition => definition != null)
                    .Select(definition => definition.Id)
                    ?? Enumerable.Empty<string>();
            }

            if (command == "createconsumable" && tokenIndex == 1)
            {
                return contentCatalog?.ItemDefinitions?
                    .Where(definition => definition is ConsumableItemDefinition consumable
                        && consumable.UseType != ConsumableUseType.Utility)
                    .Select(definition => definition.Id)
                    ?? Enumerable.Empty<string>();
            }

            if (command == "createconsumable"
                && TryGetConsumableDefinition(commandTokens, contentCatalog, out var consumable))
            {
                if (consumable.UseType == ConsumableUseType.Utility)
                {
                    return Enumerable.Empty<string>();
                }

                return GetConsumableArgumentCandidates(
                    commandTokens,
                    tokenIndex,
                    consumable.UseType);
            }

            if (command == "inspectitem" && tokenIndex == 1
                && gameSession?.PlayerParty?.ActiveLeaderId is { } leaderId
                && gameSession.CharactersById.TryGetValue(leaderId, out var leader))
            {
                return leader.Inventory?.Items?
                    .Where(item => item != null)
                    .Select(item => item.InstanceId)
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

        private static bool TryGetConsumableDefinition(
            IReadOnlyList<string> commandTokens,
            GameContentCatalog contentCatalog,
            out ConsumableItemDefinition consumable)
        {
            consumable = null;

            return commandTokens != null
                && commandTokens.Count >= 2
                && contentCatalog != null
                && contentCatalog.TryGetItem(commandTokens[1], out var selectedDefinition)
                && (consumable = selectedDefinition as ConsumableItemDefinition) != null;
        }

        private static IEnumerable<string> GetLegalEffectNames(ConsumableUseType useType)
        {
            return useType switch
            {
                ConsumableUseType.Potion => new[]
                {
                    "RestoreHealth",
                    "RegenHealth",
                    "RestoreResource",
                    "RegenResource",
                    "ModifyMaxResource",
                    "ModifyDamage",
                    "ModifyPhysicalDefense",
                    "ModifyMagicalDefense",
                    "ModifyResistance",
                    "ModifySpeed",
                    "ModifyCritChance",
                    "ModifyDodge",
                    "ModifyActionPoints"
                },
                ConsumableUseType.Bomb => new[]
                {
                    "DealPhysicalDamage",
                    "DealMagicalDamage",
                    "DealInevitableDamage",
                    "ApplyBleed",
                    "ApplyPoison",
                    "ApplyBurn",
                    "ApplyStun",
                    "ApplyFreeze",
                    "ModifyPhysicalDefense",
                    "ModifyMagicalDefense",
                    "ModifySpeed",
                    "ModifyDodge",
                    "ModifyCritChance",
                    "Slow"
                },
                _ => Enumerable.Empty<string>()
            };
        }

        private static IEnumerable<string> GetConsumableArgumentCandidates(
            IReadOnlyList<string> commandTokens,
            int tokenIndex,
            ConsumableUseType useType)
        {
            var position = 2;
            var effectCount = 0;

            while (position < tokenIndex)
            {
                if (!Enum.TryParse(commandTokens[position], true, out ConsumableRuntimeEffectType effectType))
                {
                    return Enumerable.Empty<string>();
                }

                if (!GetLegalEffectNames(useType).Contains(
                        effectType.ToString(),
                        StringComparer.OrdinalIgnoreCase))
                {
                    return Enumerable.Empty<string>();
                }

                effectCount++;

                position++;

                // The value is intentionally free-form numeric input, so do not
                // suggest anything while the cursor is on this argument.
                if (position == tokenIndex)
                {
                    return Enumerable.Empty<string>();
                }

                if (position >= commandTokens.Count
                    || !float.TryParse(commandTokens[position], NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    return Enumerable.Empty<string>();
                }
                position++;

                if (effectType == ConsumableRuntimeEffectType.ModifyResistance)
                {
                    if (position == tokenIndex)
                        return Enum.GetNames(typeof(ResistanceType));
                    if (position >= commandTokens.Count
                        || !Enum.TryParse(commandTokens[position], true, out ResistanceType _))
                        return Enumerable.Empty<string>();
                    position++;
                }
                else if (effectType is ConsumableRuntimeEffectType.ApplyStatus
                    or ConsumableRuntimeEffectType.RemoveStatus)
                {
                    if (position == tokenIndex)
                        return StatusEffectIds.KnownIds;
                    if (position >= commandTokens.Count) return Enumerable.Empty<string>();
                    position++;
                    if (position < commandTokens.Count
                        && int.TryParse(commandTokens[position], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        position++;
                }
            }

            if (position != tokenIndex)
            {
                return Enumerable.Empty<string>();
            }

            if (effectCount >= 3)
            {
                return Enumerable.Empty<string>();
            }

            // Repeated runtime effects are intentional: a potion may contain
            // the same effect more than once, and bombs may stack the same
            // damage/status component. Keep all legal names available after
            // every completed value/duration pair.
            return GetLegalEffectNames(useType);
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
