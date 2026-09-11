using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;

namespace Godmist.Systems.Debugging
{
    public static class DevConsoleSuggestionService
    {
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

            var namedArgument = GetNamedArgumentContext(
                activePrefix,
                command,
                out var namedPrefix);
            var suggestionPrefix = activePrefix;

            if (namedArgument != null) suggestionPrefix = namedPrefix;

            var candidates = tokenIndex == 0
                ? DevConsoleCommandSchemaRegistry.All
                    .Select(schema => schema.Name)
                : GetArgumentCandidates(
                    commandLine,
                    command,
                    tokenIndex,
                    gameSession,
                    combatSession,
                    contentCatalog,
                    namedArgument);

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Where(candidate => candidate.StartsWith(
                    suggestionPrefix,
                    StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate)
                .ToList();
        }

        public static bool CommandRequiresArgument(string command)
        {
            return DevConsoleCommandSchemaRegistry.TryGet(
                    command,
                    out var schema)
                && schema.Signatures.Any(signature => signature.Arguments
                    .Any(argument => argument.IsRequired));
        }

        private static IEnumerable<string> GetArgumentCandidates(
            string commandLine,
            string command,
            int tokenIndex,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog,
            DevConsoleArgumentDefinition namedArgument)
        {
            var commandTokens = commandLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();

            if (namedArgument != null)
            {
                return GetCandidatesForArgument(
                    namedArgument,
                    command,
                    tokenIndex,
                    commandTokens,
                    gameSession,
                    combatSession,
                    contentCatalog);
            }

            if (command == "createconsumable" && tokenIndex == 1)
            {
                return GetConsumableItemIds(contentCatalog);
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

            return DevConsoleCommandSchemaRegistry.GetArgumentsAtPosition(
                    command,
                    tokenIndex)
                .SelectMany(argument => GetCandidatesForArgument(
                    argument,
                    command,
                    tokenIndex,
                    commandTokens,
                    gameSession,
                    combatSession,
                    contentCatalog));
        }

        private static IEnumerable<string> GetCandidatesForArgument(
            DevConsoleArgumentDefinition argument,
            string command,
            int tokenIndex,
            IReadOnlyList<string> commandTokens,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog)
        {
            if (command == "creategalduryte")
            {
                if (string.Equals(argument.Name, "tier", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "1", "2", "3" };
                }

                if (string.Equals(argument.Name, "target", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "armor", "weapon" };
                }

                if (string.Equals(argument.Name, "identified", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "identified", "unidentified" };
                }
            }

            return argument.Type switch
            {
                DevConsoleArgumentType.CharacterId =>
                    GetCharacterIds(gameSession, combatSession),
                DevConsoleArgumentType.ItemId =>
                    command == "createconsumable"
                            ? GetConsumableItemIds(contentCatalog)
                            : GetItemIds(contentCatalog, command, gameSession, commandTokens),
                DevConsoleArgumentType.EncounterId =>
                    contentCatalog?.EncounterDefinitions?
                        .Where(definition => definition != null)
                        .Select(definition => definition.Id)
                    ?? Enumerable.Empty<string>(),
                DevConsoleArgumentType.QuestId => GetQuestIds(
                    gameSession,
                    contentCatalog),
                DevConsoleArgumentType.QuestObjectiveId => GetQuestObjectiveIds(
                    gameSession,
                    commandTokens),
                DevConsoleArgumentType.NpcId => GetNpcIds(command, gameSession),
                DevConsoleArgumentType.DungeonObjectId => GetDungeonObjectIds(
                    gameSession,
                    contentCatalog),
                DevConsoleArgumentType.String when command == "goto" && tokenIndex == 1 =>
                    contentCatalog?.DungeonDefinitions?
                        .Where(definition => definition != null && definition.IsProcedural)
                    .Select(definition => definition.Id)
                    ?? Enumerable.Empty<string>(),
                DevConsoleArgumentType.String when command == "help" =>
                    DevConsoleCommandSchemaRegistry.All.Select(schema => schema.Name),
                DevConsoleArgumentType.String when command == "noclip" =>
                    new[] { "off", "on" },
                DevConsoleArgumentType.String when command == "forcecombatresult" =>
                    new[] { "escape", "loss", "win" },
                DevConsoleArgumentType.String when command == "triggerdialogue" =>
                    GetDialogueIds(gameSession),
                DevConsoleArgumentType.String when command == "inspectroom" =>
                    GetRoomIds(gameSession),
                DevConsoleArgumentType.WorldFlagId => gameSession?.WorldState?.FlagsById?
                        .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                        .Select(pair => pair.Key)
                    ?? Enumerable.Empty<string>(),
                DevConsoleArgumentType.EquipmentPartId => GetEquipmentPartIds(
                    contentCatalog,
                    commandTokens,
                    command,
                    tokenIndex),
                DevConsoleArgumentType.EquipmentQuality => new[]
                {
                    EquipmentQuality.Normal.ToString(),
                    EquipmentQuality.Poor.ToString(),
                    EquipmentQuality.Excellent.ToString(),
                    EquipmentQuality.Masterpiece.ToString()
                },
                DevConsoleArgumentType.InstanceId =>
                    GetInventoryInstanceIds(gameSession, command, commandTokens),
                DevConsoleArgumentType.ResistanceType =>
                    Enum.GetNames(typeof(ResistanceType)),
                DevConsoleArgumentType.StatusId => StatusEffectIds.KnownIds,
                DevConsoleArgumentType.EffectName =>
                    TryGetConsumableDefinition(
                            commandTokens,
                            contentCatalog,
                            out var consumable)
                        ? GetLegalEffectNames(consumable.UseType)
                        : Enumerable.Empty<string>(),
                _ => Enumerable.Empty<string>()
            };
        }

        private static IEnumerable<string> GetQuestIds(
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            var ids = new List<string>();

            if (gameSession?.QuestsById != null)
            {
                ids.AddRange(gameSession.QuestsById.Keys);
            }

            if (contentCatalog?.QuestDefinitions != null)
            {
                ids.AddRange(contentCatalog.QuestDefinitions
                    .Where(definition => definition != null)
                    .Select(definition => definition.Id));
            }

            return ids;
        }

        private static IEnumerable<string> GetQuestObjectiveIds(
            GameSession gameSession,
            IReadOnlyList<string> commandTokens)
        {
            var questId = commandTokens?.Count > 1 ? commandTokens[1] : string.Empty;
            if (TryGetNamedValue(commandTokens, "questId", out var namedQuestId)) questId = namedQuestId;
            if (gameSession?.TryGetQuest(questId, out var quest) == true)
            {
                return quest.CurrentStage?.Objectives?
                    .Where(objective => objective != null)
                    .Select(objective => objective.ObjectiveId)
                    ?? Enumerable.Empty<string>();
            }
            return Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetDungeonObjectIds(
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            var activeDefinitions = gameSession?.ActiveDungeon?.Definition?.ObjectDefinitions;
            if (activeDefinitions != null && activeDefinitions.Count > 0)
            {
                return activeDefinitions.Where(definition => definition != null).Select(definition => definition.Id);
            }
            return contentCatalog?.DungeonDefinitions?
                .SelectMany(definition => definition?.ObjectDefinitions ?? new List<DungeonObjectDefinition>())
                .Where(definition => definition != null)
                .Select(definition => definition.Id)
                ?? Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetDialogueIds(GameSession gameSession)
        {
            return gameSession?.NpcsById?.Values
                .Where(npc => npc?.Definition?.BaseDialogue != null)
                .Select(npc => npc.Definition.BaseDialogue.Id)
                ?? Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetRoomIds(GameSession gameSession)
        {
            return gameSession?.ActiveDungeon?.Floors?
                .FirstOrDefault(floor => floor.FloorIndex == gameSession.ActiveDungeon.CurrentFloorIndex)?
                .Rooms
                .Where(room => room != null)
                .Select(room => room.RoomId)
                ?? Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetNpcIds(
            string command,
            GameSession gameSession)
        {
            var npcs = gameSession?.NpcsById?.Values
                .Where(npc => npc != null)
                ?? Enumerable.Empty<NpcInstance>();

            if (command is "upgrade" or "reforge")
            {
                npcs = npcs.Where(npc => npc.Definition?.GetRole<BlacksmithRoleData>() != null);
            }

            return npcs.Select(npc => npc.NpcId);
        }

        private static IEnumerable<string> GetItemIds(
            GameContentCatalog contentCatalog,
            string command,
            GameSession gameSession,
            IReadOnlyList<string> commandTokens)
        {
            return contentCatalog?.ItemDefinitions?
                .Where(definition => definition != null
                    && (command != "createequipment"
                        || definition is EquipmentItemDefinition))
                .Select(definition => definition.Id)
                ?? Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetEquipmentPartIds(
            GameContentCatalog contentCatalog,
            IReadOnlyList<string> commandTokens,
            string command,
            int tokenIndex)
        {
            var definitions = contentCatalog?.EquipmentPartDefinitions?
                .Where(definition => definition != null)
                ?? Enumerable.Empty<EquipmentPartDefinition>();

            if (command != "createequipment"
                || commandTokens == null
                || commandTokens.Count < 2)
            {
                return definitions.Select(definition => definition.Id);
            }

            var itemId = commandTokens[1];
            if (TryGetNamedValue(commandTokens, "itemId", out var namedItemId))
            {
                itemId = namedItemId;
            }

            if (!contentCatalog.TryGetItem(itemId, out var item)
                || item is not EquipmentItemDefinition equipment
                || equipment.RequiredClass == null)
            {
                return definitions.Select(definition => definition.Id);
            }

            var expectedKind = tokenIndex switch
            {
                3 => equipment.ItemType == ItemType.Weapon
                    ? EquipmentPartKind.WeaponHead
                    : EquipmentPartKind.ArmorPlate,
                4 => equipment.ItemType == ItemType.Weapon
                    ? EquipmentPartKind.WeaponBinder
                    : EquipmentPartKind.ArmorBinder,
                _ => equipment.ItemType == ItemType.Weapon
                    ? EquipmentPartKind.WeaponHandle
                    : EquipmentPartKind.ArmorBase
            };

            return definitions
                .Where(definition => definition.RequiredClass == equipment.RequiredClass
                    && definition.Kind == expectedKind
                    && definition.Stats != null)
                .Select(definition => definition.Id);
        }

        private static bool TryGetNamedValue(
            IReadOnlyList<string> tokens,
            string argumentName,
            out string value)
        {
            value = string.Empty;
            if (tokens == null) return false;

            foreach (var token in tokens)
            {
                var separator = token?.IndexOf('=') ?? -1;
                if (separator <= 0
                    || !string.Equals(token.Substring(0, separator), argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = token.Substring(separator + 1);
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static IEnumerable<string> GetConsumableItemIds(
            GameContentCatalog contentCatalog)
        {
            return contentCatalog?.ItemDefinitions?
                .Where(definition => definition is ConsumableItemDefinition consumable
                    && consumable.UseType != ConsumableUseType.Utility)
                .Select(definition => definition.Id)
                ?? Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetInventoryInstanceIds(
            GameSession gameSession,
            string command = null,
            IReadOnlyList<string> commandTokens = null)
        {
            if (gameSession?.PlayerParty?.ActiveMemberIds == null)
            {
                return Enumerable.Empty<string>();
            }

            var characterIds = gameSession.PlayerParty.ActiveMemberIds;
            if (string.Equals(command, "removeitem", StringComparison.OrdinalIgnoreCase)
                && commandTokens?.Count > 1)
            {
                characterIds = new[] { commandTokens[1] };
            }

            return characterIds
                .Where(gameSession.CharactersById.ContainsKey)
                .SelectMany(characterId =>
                {
                    var character = gameSession.CharactersById[characterId];
                    var inventoryItems = character?.Inventory?.Items
                        ?? Enumerable.Empty<ItemInstance>();
                    var equippedItems = character?.Equipment?.GetEquippedItems()
                        ?? Enumerable.Empty<EquipmentItemInstance>();
                    return inventoryItems.Concat(equippedItems);
                })
                .Where(item => item != null)
                .Select(item => item.InstanceId)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool TryGetConsumableDefinition(
            IReadOnlyList<string> commandTokens,
            GameContentCatalog contentCatalog,
            out ConsumableItemDefinition consumable)
        {
            consumable = null;

            var itemId = commandTokens?.Count >= 2
                ? commandTokens[1]
                : string.Empty;

            if (commandTokens != null)
            {
                foreach (var token in commandTokens.Skip(1))
                {
                    if (!TrySplitNamedArgument(
                            token,
                            out var name,
                            out var value)
                        || !string.Equals(
                            name,
                            "itemId",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    itemId = value;
                    break;
                }
            }

            return !string.IsNullOrWhiteSpace(itemId)
                && contentCatalog != null
                && contentCatalog.TryGetItem(itemId, out var selectedDefinition)
                && (consumable = selectedDefinition as ConsumableItemDefinition) != null;
        }

        private static DevConsoleArgumentDefinition GetNamedArgumentContext(
            string activePrefix,
            string command,
            out string namedPrefix)
        {
            namedPrefix = activePrefix;
            var separatorIndex = activePrefix?.IndexOf('=') ?? -1;

            if (separatorIndex <= 0)
            {
                return null;
            }

            var argumentName = activePrefix.Substring(0, separatorIndex);

            if (!DevConsoleCommandSchemaRegistry.TryGetArgument(
                    command,
                    argumentName,
                    out var argument))
            {
                return null;
            }

            namedPrefix = activePrefix.Substring(separatorIndex + 1);
            return argument;
        }

        private static bool TrySplitNamedArgument(
            string token,
            out string name,
            out string value)
        {
            name = string.Empty;
            value = string.Empty;
            var separatorIndex = token?.IndexOf('=') ?? -1;

            if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
            {
                return false;
            }

            name = token.Substring(0, separatorIndex);
            value = token.Substring(separatorIndex + 1);
            return true;
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
