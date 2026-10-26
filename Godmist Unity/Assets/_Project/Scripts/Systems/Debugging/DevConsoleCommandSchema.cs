using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godmist.Core.Enums;

namespace Godmist.Systems.Debugging
{
    public enum DevConsoleArgumentType
    {
        Integer,
        NonNegativeInteger,
        CoordinateInteger,
        Float,
        CharacterId,
        ItemId,
        EncounterId,
        InstanceId,
        EffectName,
        EffectValue,
        ResistanceType,
        StatusId,
        QuestId,
        NpcId,
        WorldFlagId,
        EquipmentPartId,
        EquipmentQuality,
        QuestObjectiveId,
        DungeonObjectId,
        String
    }

    public sealed class DevConsoleArgumentDefinition
    {
        public string Name { get; }
        public DevConsoleArgumentType Type { get; }
        public bool IsRequired { get; }
        public string DefaultValue { get; }

        public DevConsoleArgumentDefinition(
            string name,
            DevConsoleArgumentType type,
            bool isRequired = true,
            string defaultValue = null)
        {
            Name = name ?? string.Empty;
            Type = type;
            IsRequired = isRequired;
            DefaultValue = defaultValue;
        }
    }

    public sealed class DevConsoleCommandSignature
    {
        public string Usage { get; }
        public IReadOnlyList<DevConsoleArgumentDefinition> Arguments { get; }
        public bool AllowsUnstructuredTrailingArguments { get; }

        public DevConsoleCommandSignature(
            string usage,
            IReadOnlyList<DevConsoleArgumentDefinition> arguments,
            bool allowsUnstructuredTrailingArguments = false)
        {
            Usage = usage ?? string.Empty;
            Arguments = arguments ?? Array.Empty<DevConsoleArgumentDefinition>();
            AllowsUnstructuredTrailingArguments = allowsUnstructuredTrailingArguments;
        }
    }

    public sealed class DevConsoleCommandSchema
    {
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<DevConsoleCommandSignature> Signatures { get; }
        public IReadOnlyList<string> Examples { get; }

        public DevConsoleCommandSchema(
            string name,
            string description,
            IReadOnlyList<DevConsoleCommandSignature> signatures,
            IReadOnlyList<string> examples)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Signatures = signatures ?? Array.Empty<DevConsoleCommandSignature>();
            Examples = examples ?? Array.Empty<string>();
        }
    }

    public static class DevConsoleCommandSchemaRegistry
    {
        private static readonly IReadOnlyList<DevConsoleCommandSchema> schemas =
            new[]
            {
                Schema("help", "Wyświetla składnię, opis i przykłady komendy.",
                    Signature("help [command]",
                        Arg("command", DevConsoleArgumentType.String, false)),
                    "help", "help createitem"),
                Schema("combattrace", "Rejestruje decyzje AI i obrażenia; zapis ograniczony do 4000 wpisów.",
                    Signature("combattrace [action]", Arg("action", DevConsoleArgumentType.String, false)), "combattrace on", "combattrace export"),
                Schema("state", "Wyświetla stan sesji i aktywnej walki.",
                    Signature("state"), "state"),
                Schema("characters", "Wyświetla znane postacie i ich bieżący stan.",
                    Signature("characters"), "characters"),
                Schema("queststate", "Wyświetla stan jednej albo wszystkich zarejestrowanych questów.",
                    Signature("queststate [questId]",
                        Arg("questId", DevConsoleArgumentType.QuestId, false)),
                    "queststate", "queststate quest.main"),
                Schema("worldflags", "Wyświetla ustawione world flags sesji.",
                    Signature("worldflags"), "worldflags"),
                Schema("setworldflag", "Ustawia wartość world flaga w bieżącej sesji.",
                    Signature("setworldflag <flagId> <value>",
                        Arg("flagId", DevConsoleArgumentType.WorldFlagId),
                        Arg("value", DevConsoleArgumentType.String)),
                    "setworldflag tutorial.completed true"),
                Schema("unit", "Wyświetla szczegółowy stan postaci.",
                    Signature("unit [characterId]", Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "unit", "unit jerzy"),
                Schema("sethonor", "Ustawia honor lidera albo wskazanej postaci, także poza zakresem −100…100.",
                    Signature("sethonor <value>", Arg("value", DevConsoleArgumentType.CoordinateInteger)),
                    Signature("sethonor <characterId> <value>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("value", DevConsoleArgumentType.CoordinateInteger)),
                    "sethonor 200", "sethonor jerzy -80"),
                Schema("gold", "Dodaje złoto do sesji.",
                    Signature("gold <amount>", Arg("amount", DevConsoleArgumentType.Integer)),
                    "gold 100"),
                Schema("xp", "Dodaje XP aktywnej drużynie albo wskazanej postaci.",
                    Signature("xp <amount>", Arg("amount", DevConsoleArgumentType.Integer)),
                    Signature("xp <characterId> <amount>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("amount", DevConsoleArgumentType.Integer)),
                    "xp 100", "xp jerzy 100", "xp amount=100 characterId=jerzy"),
                Schema("heal", "Leczy aktywną jednostkę albo wskazaną postać.",
                    Signature("heal <amount>", Arg("amount", DevConsoleArgumentType.Integer)),
                    Signature("heal <characterId> <amount>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("amount", DevConsoleArgumentType.Integer)),
                    "heal 25", "heal jerzy 25"),
                Schema("resource", "Odnawia zasób aktywnej jednostki albo postaci.",
                    Signature("resource <amount>", Arg("amount", DevConsoleArgumentType.Integer)),
                    Signature("resource <characterId> <amount>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("amount", DevConsoleArgumentType.Integer)),
                    "resource 25", "resource jerzy 25"),
                Schema("godmode", "Przełącza nieśmiertelność postaci.",
                    Signature("godmode <characterId>", Arg("characterId", DevConsoleArgumentType.CharacterId)),
                    "godmode jerzy"),
                Schema("sethp", "Ustawia HP wskazanej postaci.",
                    Signature("sethp <characterId> <amount>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("amount", DevConsoleArgumentType.Integer)),
                    "sethp jerzy 100"),
                Schema("setresource", "Ustawia zasób wskazanej postaci.",
                    Signature("setresource <characterId> <amount>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("amount", DevConsoleArgumentType.Integer)),
                    "setresource jerzy 60"),
                Schema("giveitem", "Dodaje item do inventory wskazanej postaci.",
                    Signature("giveitem <characterId> <itemId> [amount]",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("itemId", DevConsoleArgumentType.ItemId),
                        Arg("amount", DevConsoleArgumentType.Integer, false, "1")),
                    "giveitem jerzy item.material.iron", "giveitem jerzy item.material.iron amount=3"),
                Schema("createitem", "Tworzy item i dodaje go do inventory postaci.",
                    Signature("createitem <itemId> [amount] [characterId]",
                        Arg("itemId", DevConsoleArgumentType.ItemId),
                        Arg("amount", DevConsoleArgumentType.Integer, false, "1"),
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "createitem item.weapon.debug", "createitem item.weapon.debug characterId=jerzy amount=3"),
                Schema("creategalduryte", "Tworzy galduryt z wylosowanymi efektami i dodaje go do inventory.",
                    Signature("creategalduryte <tier> <weapon|armor> [identified] [characterId]",
                        Arg("tier", DevConsoleArgumentType.Integer),
                        Arg("target", DevConsoleArgumentType.String),
                        Arg("identified", DevConsoleArgumentType.String, false, "identified"),
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "creategalduryte 1 weapon", "creategalduryte 1 armor unidentified jerzy"),
                Schema("createconsumable", "Tworzy jedną potion/bombę z efektami runtime.",
                    SignatureWithTrailing("createconsumable <itemId> [Effect value ...] [characterId]",
                        true,
                        Arg("itemId", DevConsoleArgumentType.ItemId),
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "createconsumable item.consumable.potion RestoreHealth 25 RestoreResource 10 ModifyDamage 0.2",
                    "createconsumable item.consumable.potion characterId=jerzy RestoreHealth 25 RestoreResource 10 ModifyDamage 0.2"),
                Schema("items", "Wyświetla inventory wskazanej postaci.",
                    Signature("items <characterId>", Arg("characterId", DevConsoleArgumentType.CharacterId)),
                    "items jerzy"),
                Schema("inspectitem", "Wyświetla pełny payload itemu z inventory lidera.",
                    Signature("inspectitem <instanceId>", Arg("instanceId", DevConsoleArgumentType.InstanceId)),
                    "inspectitem item.instance.001"),
                Schema("itemdefs", "Wyświetla itemy z GameContentCatalog.",
                    Signature("itemdefs"), "itemdefs"),
                Schema("encounterdefs", "Wyświetla encountery z GameContentCatalog.",
                    Signature("encounterdefs"), "encounterdefs"),
                Schema("combat", "Uruchamia debugową walkę z encounterem.",
                    Signature("combat <encounterId>", Arg("encounterId", DevConsoleArgumentType.EncounterId)),
                    "combat combat.encounter.debug"),
                Schema("createequipment", "Tworzy wyposażenie z trzema wybranymi częściami bez kosztu materiałów.",
                    Signature("createequipment <itemId> <quality> <partId> <partId> <partId> [characterId]",
                        Arg("itemId", DevConsoleArgumentType.ItemId),
                        Arg("quality", DevConsoleArgumentType.EquipmentQuality),
                        Arg("part1Id", DevConsoleArgumentType.EquipmentPartId),
                        Arg("part2Id", DevConsoleArgumentType.EquipmentPartId),
                        Arg("part3Id", DevConsoleArgumentType.EquipmentPartId),
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "createequipment item.weapon.debug Normal part.head part.binder part.handle"),
                Schema("upgrade", "Ulepsza istniejące wyposażenie u kowala.",
                    Signature("upgrade <npcId> <instanceId> <targetProgress> [successChance] [characterId]",
                        Arg("npcId", DevConsoleArgumentType.NpcId),
                        Arg("instanceId", DevConsoleArgumentType.InstanceId),
                        Arg("targetProgress", DevConsoleArgumentType.Float),
                        Arg("successChance", DevConsoleArgumentType.Float, false, "0.99"),
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "upgrade npc.blacksmith item.instance.001 0.5", "upgrade npc.blacksmith item.instance.001 0.5 successChance=0.99 characterId=jerzy"),
                Schema("reforge", "Reforguje istniejące wyposażenie u kowala.",
                    Signature("reforge <npcId> <instanceId> [characterId]",
                        Arg("npcId", DevConsoleArgumentType.NpcId),
                        Arg("instanceId", DevConsoleArgumentType.InstanceId),
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "reforge npc.blacksmith item.instance.001"),
                Schema("markquestobjective", "Dodaje postęp wskazanego celu questa.",
                    Signature("markquestobjective <questId> <objectiveId> [amount]",
                        Arg("questId", DevConsoleArgumentType.QuestId),
                        Arg("objectiveId", DevConsoleArgumentType.QuestObjectiveId),
                        Arg("amount", DevConsoleArgumentType.Integer, false, "1")),
                    "markquestobjective quest.main objective.kill amount=1"),
                Schema("removeitem", "Usuwa konkretny item z inventory postaci.",
                    Signature("removeitem <characterId> <instanceId>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("instanceId", DevConsoleArgumentType.InstanceId)),
                    "removeitem jerzy item.instance.001"),
                Schema("clearinventory", "Usuwa wszystkie itemy z inventory postaci.",
                    Signature("clearinventory [characterId]",
                        Arg("characterId", DevConsoleArgumentType.CharacterId, false)),
                    "clearinventory", "clearinventory jerzy"),
                Schema("setlevel", "Ustawia poziom wskazanej postaci.",
                    Signature("setlevel <characterId> <level>",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("level", DevConsoleArgumentType.Integer)),
                    "setlevel jerzy 10"),
                Schema("setdanger", "Ustawia Danger motywu lochu (100% odblokowuje ofertę bossa).",
                    Signature("setdanger <theme> <0-100>",
                        Arg("theme", DevConsoleArgumentType.String),
                        Arg("value", DevConsoleArgumentType.Integer)),
                    "setdanger catacombs 100"),
                Schema("setloyalty", "Ustawia poziom lojalności wskazanego NPC.",
                    Signature("setloyalty <npcId> <level>",
                        Arg("npcId", DevConsoleArgumentType.NpcId),
                        Arg("level", DevConsoleArgumentType.Integer)),
                    "setloyalty npc.enchanter 5"),
                Schema("noclip", "Włącza albo wyłącza przechodzenie przez przeszkody.",
                    Signature("noclip [on|off]",
                        Arg("mode", DevConsoleArgumentType.String, false)),
                    "noclip", "noclip off"),
                Schema("revealmap", "Odkrywa teren i zapamiętuje obiekty wszystkich pięter aktywnego lochu.",
                    Signature("revealmap"), "revealmap"),
                Schema("noaggro", "Przełącza blokadę automatycznego aggro i alarmów przeciwników; patrole i trwające walki pozostają aktywne.",
                    Signature("noaggro [on|off]", Arg("mode", DevConsoleArgumentType.String, false)),
                    "noaggro on", "noaggro off"),
                Schema("togglefog", "Przełącza debugowy widok bez mgły; nie zmienia wiedzy mapy ani wykrywania.",
                    Signature("togglefog"), "togglefog"),
                Schema("clearmap", "Czyści pamięć mapy aktywnego lochu; widoczny teren jest nadal widoczny.",
                    Signature("clearmap"), "clearmap"),
                Schema("goto", "Przechodzi do wskazanego lochu i piętra.",
                    Signature("goto <dungeonId> [floor] [roomId]",
                        Arg("dungeonId", DevConsoleArgumentType.String),
                        Arg("floor", DevConsoleArgumentType.NonNegativeInteger, false),
                        Arg("roomId", DevConsoleArgumentType.String, false)),
                    "goto dungeon.caves", "goto dungeon.caves 1 room.start"),
                Schema("assetroom", "Otwiera jawny dev-only pokój audytowy assetów Katakumb.",
                    Signature("assetroom <dungeonId> <seed>",
                        Arg("dungeonId", DevConsoleArgumentType.String),
                        Arg("seed", DevConsoleArgumentType.Integer)),
                    "assetroom catacombs 240917"),
                Schema("teleport", "Przenosi drużynę na współrzędne w bieżącej scenie.",
                    Signature("teleport <x> <y>",
                        Arg("x", DevConsoleArgumentType.Float),
                        Arg("y", DevConsoleArgumentType.Float)),
                    "teleport 12.5 8"),
                Schema("advance", "Przesuwa aktywną walkę o wskazaną liczbę tur.",
                    Signature("advance [turns]",
                        Arg("turns", DevConsoleArgumentType.Integer, false, "1")),
                    "advance", "advance 3"),
                Schema("tick", "Przesuwa debugowy czas o wskazaną liczbę sekund.",
                    Signature("tick <seconds>", Arg("seconds", DevConsoleArgumentType.Float)),
                    "tick 5"),
                Schema("applystatus", "Nakłada status na wskazaną postać.",
                    Signature("applystatus <characterId> <statusId> [duration] [stacks]",
                        Arg("characterId", DevConsoleArgumentType.CharacterId),
                        Arg("statusId", DevConsoleArgumentType.StatusId),
                        Arg("duration", DevConsoleArgumentType.Integer, false, "1"),
                        Arg("stacks", DevConsoleArgumentType.Integer, false, "1")),
                    "applystatus jerzy status.poison 3 1"),
                Schema("clearstatuses", "Usuwa wszystkie statusy z wskazanej postaci.",
                    Signature("clearstatuses <characterId>", Arg("characterId", DevConsoleArgumentType.CharacterId)),
                    "clearstatuses jerzy"),
                Schema("forcecombatresult", "Kończy aktywną walkę wskazanym wynikiem.",
                    Signature("forcecombatresult <result>", Arg("result", DevConsoleArgumentType.String)),
                    "forcecombatresult win"),
                Schema("seed", "Bez argumentu pokazuje seed, ID, długość, poziom i piętro wyprawy oraz ostatnią pozycję drużyny. Z liczbą ustawia seed debugowy nowego generowania.",
                    Signature("seed [number]", Arg("number", DevConsoleArgumentType.CoordinateInteger, false)),
                    "seed", "seed 12345"),
                Schema("triggerdialogue", "Uruchamia dialog wskazany identyfikatorem.",
                    Signature("triggerdialogue <dialogueId>", Arg("dialogueId", DevConsoleArgumentType.String)),
                    "triggerdialogue dialogue.intro"),
                Schema("inspectdungeon", "Wyświetla stan aktywnego lochu.",
                    Signature("inspectdungeon"), "inspectdungeon"),
                Schema("inspectroom", "Wyświetla stan pomieszczenia aktywnego lochu.",
                    Signature("inspectroom [roomId]", Arg("roomId", DevConsoleArgumentType.String, false)),
                    "inspectroom", "inspectroom room.start"),
                Schema("spawnobject", "Dodaje obiekt dungeonowy na wskazanej komórce.",
                    Signature("spawnobject <objectId> <x> <y> [instanceId]",
                        Arg("objectId", DevConsoleArgumentType.DungeonObjectId),
                        Arg("x", DevConsoleArgumentType.CoordinateInteger),
                        Arg("y", DevConsoleArgumentType.CoordinateInteger),
                        Arg("instanceId", DevConsoleArgumentType.InstanceId, false)),
                    "spawnobject dungeon.object.obstacle 10 8")
            };

        private static readonly Dictionary<string, DevConsoleCommandSchema> schemasByName =
            schemas.ToDictionary(
                schema => schema.Name,
                StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<DevConsoleCommandSchema> All => schemas;

        public static bool TryGet(
            string command,
            out DevConsoleCommandSchema schema)
        {
            return schemasByName.TryGetValue(command ?? string.Empty, out schema);
        }

        public static string BuildHelp()
        {
            var output = new StringBuilder("Dostępne komendy:");

            foreach (var schema in schemas)
            {
                output.Append($"\n{schema.Name} - {schema.Description}");

                foreach (var signature in schema.Signatures)
                {
                    output.Append($"\n  Użycie: {signature.Usage}");
                }

                foreach (var example in schema.Examples)
                {
                    output.Append($"\n  Przykład: {example}");
                }
            }

            return output.ToString();
        }

        public static IReadOnlyList<DevConsoleArgumentDefinition>
            GetArgumentsAtPosition(
                string command,
                int tokenIndex)
        {
            if (!TryGet(command, out var schema))
            {
                return Array.Empty<DevConsoleArgumentDefinition>();
            }

            return schema.Signatures
                .SelectMany(signature => signature.Arguments
                    .Where((_, index) => index == tokenIndex - 1))
                .ToList();
        }

        public static bool TryGetArgument(
            string command,
            string argumentName,
            out DevConsoleArgumentDefinition argument)
        {
            argument = null;

            if (!TryGet(command, out var schema))
            {
                return false;
            }

            argument = schema.Signatures
                .SelectMany(signature => signature.Arguments)
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Name,
                    argumentName,
                    StringComparison.OrdinalIgnoreCase));

            return argument != null;
        }

        private static DevConsoleCommandSchema Schema(
            string name,
            string description,
            DevConsoleCommandSignature signature,
            params string[] examples)
        {
            return new DevConsoleCommandSchema(
                name,
                description,
                new[] { signature },
                examples);
        }

        private static DevConsoleCommandSchema Schema(
            string name,
            string description,
            DevConsoleCommandSignature firstSignature,
            DevConsoleCommandSignature secondSignature,
            params string[] examples)
        {
            return new DevConsoleCommandSchema(
                name,
                description,
                new[] { firstSignature, secondSignature },
                examples);
        }

        private static DevConsoleCommandSignature Signature(
            string usage,
            params DevConsoleArgumentDefinition[] arguments)
        {
            return new DevConsoleCommandSignature(usage, arguments);
        }

        private static DevConsoleCommandSignature SignatureWithTrailing(
            string usage,
            bool allowsUnstructuredTrailingArguments,
            params DevConsoleArgumentDefinition[] arguments)
        {
            return new DevConsoleCommandSignature(
                usage,
                arguments,
                allowsUnstructuredTrailingArguments);
        }

        private static DevConsoleArgumentDefinition Arg(
            string name,
            DevConsoleArgumentType type,
            bool isRequired = true,
            string defaultValue = null)
        {
            return new DevConsoleArgumentDefinition(
                name,
                type,
                isRequired,
                defaultValue);
        }
    }

    public static class DevConsoleCommandSchemaParser
    {
        public static bool TryNormalize(
            string commandLine,
            out List<string> tokens,
            out string error)
        {
            tokens = commandLine?.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .ToList()
                ?? new List<string>();
            error = string.Empty;

            if (tokens.Count == 0)
            {
                return true;
            }

            if (!DevConsoleCommandSchemaRegistry.TryGet(
                    tokens[0],
                    out var schema))
            {
                return true;
            }

            foreach (var signature in schema.Signatures)
            {
                if (signature.AllowsUnstructuredTrailingArguments)
                {
                    if (TryValidateVariableSignature(
                            tokens,
                            signature,
                            out error))
                    {
                        return true;
                    }

                    continue;
                }

                if (TryNormalizeSignature(
                        tokens,
                        signature,
                        out var normalized,
                        out error))
                {
                    tokens = normalized;
                    return true;
                }
            }

            return false;
        }

        private static bool TryValidateVariableSignature(
            IReadOnlyList<string> tokens,
            DevConsoleCommandSignature signature,
            out string error)
        {
            error = string.Empty;
            var namedArguments = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (var index = 1; index < tokens.Count; index++)
            {
                if (!TrySplitNamedArgument(
                        tokens[index],
                        out var name,
                        out var value))
                {
                    if (tokens[index].Contains('='))
                    {
                        error = $"Nazwany argument '{tokens[index]}' musi mieć format name=value z wartością.";
                        return false;
                    }

                    continue;
                }

                if (!DevConsoleCommandSchemaRegistry.TryGetArgument(
                        tokens[0],
                        name,
                        out var argument)
                    || !signature.Arguments.Contains(argument))
                {
                    error = $"Nieznany argument '{name}'.";
                    return false;
                }

                if (!namedArguments.Add(name))
                {
                    error = $"Argument '{name}' został podany więcej niż raz.";
                    return false;
                }

                if (!MatchesType(argument.Type, value))
                {
                    error = InvalidValueMessage(argument, value);
                    return false;
                }
            }

            var positionalCount = tokens
                .Skip(1)
                .Count(token => !token.Contains('='));
            var hasNamedRequiredArgument = signature.Arguments
                .Where(argument => argument.IsRequired)
                .Any(argument => namedArguments.Contains(argument.Name));

            if (positionalCount == 0 && !hasNamedRequiredArgument)
            {
                error = $"Brak wymaganego argumentu. Użycie: {signature.Usage}";
                return false;
            }

            return true;
        }

        private static bool TryNormalizeSignature(
            IReadOnlyList<string> tokens,
            DevConsoleCommandSignature signature,
            out List<string> normalized,
            out string error)
        {
            normalized = new List<string>();
            error = string.Empty;

            var positionalArguments = new List<string>();
            var namedArguments = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            for (var index = 1; index < tokens.Count; index++)
            {
                if (!TrySplitNamedArgument(
                        tokens[index],
                        out var name,
                        out var value))
                {
                    if (tokens[index].Contains('='))
                    {
                        error = $"Nazwany argument '{tokens[index]}' musi mieć format name=value z wartością.";
                        return false;
                    }

                    positionalArguments.Add(tokens[index]);
                    continue;
                }

                var definition = signature.Arguments.FirstOrDefault(argument =>
                    string.Equals(
                        argument.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase));

                if (definition == null)
                {
                    error = $"Nieznany argument '{name}'. Użycie: {signature.Usage}";
                    return false;
                }

                if (!namedArguments.TryAdd(name, value))
                {
                    error = $"Argument '{name}' został podany więcej niż raz.";
                    return false;
                }

                if (!MatchesType(definition.Type, value))
                {
                    error = InvalidValueMessage(definition, value);
                    return false;
                }
            }

            var assignments = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var namedArgument in namedArguments)
            {
                assignments[namedArgument.Key] = namedArgument.Value;
            }

            var argumentIndex = 0;

            foreach (var value in positionalArguments)
            {
                while (argumentIndex < signature.Arguments.Count
                    && assignments.ContainsKey(
                        signature.Arguments[argumentIndex].Name))
                {
                    argumentIndex++;
                }

                var matched = false;

                for (var candidateIndex = argumentIndex;
                     candidateIndex < signature.Arguments.Count;
                     candidateIndex++)
                {
                    var candidate = signature.Arguments[candidateIndex];

                    if (assignments.ContainsKey(candidate.Name))
                    {
                        continue;
                    }

                    if (!MatchesType(candidate.Type, value))
                    {
                        if (candidate.IsRequired)
                        {
                            error = InvalidValueMessage(candidate, value);
                            return false;
                        }

                        continue;
                    }

                    assignments[candidate.Name] = value;
                    argumentIndex = candidateIndex + 1;
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    error = $"Zbyt wiele argumentów. Użycie: {signature.Usage}";
                    return false;
                }
            }

            foreach (var argument in signature.Arguments)
            {
                if (assignments.ContainsKey(argument.Name))
                {
                    continue;
                }

                if (argument.IsRequired)
                {
                    error = $"Brak wymaganego argumentu '{argument.Name}'. " +
                        $"Użycie: {signature.Usage}";
                    return false;
                }

                if (argument.DefaultValue != null)
                {
                    assignments[argument.Name] = argument.DefaultValue;
                }
            }

            normalized.Add(tokens[0]);

            foreach (var argument in signature.Arguments)
            {
                if (assignments.TryGetValue(argument.Name, out var value))
                {
                    normalized.Add(value);
                }
            }

            return true;
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

        private static bool MatchesType(
            DevConsoleArgumentType type,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return type switch
            {
                DevConsoleArgumentType.Integer =>
                    int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                    out var integer)
                    && integer > 0,
                DevConsoleArgumentType.NonNegativeInteger =>
                    int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var nonNegativeInteger)
                    && nonNegativeInteger >= 0,
                DevConsoleArgumentType.CoordinateInteger =>
                    int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out _),
                DevConsoleArgumentType.Float =>
                    float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var floatingPoint)
                    && !float.IsNaN(floatingPoint)
                    && !float.IsInfinity(floatingPoint),
                DevConsoleArgumentType.ResistanceType =>
                    Enum.TryParse<ResistanceType>(value, true, out _),
                DevConsoleArgumentType.EquipmentQuality =>
                    Enum.TryParse<EquipmentQuality>(value, true, out _),
                _ => true
            };
        }

        private static string InvalidValueMessage(
            DevConsoleArgumentDefinition argument,
            string value)
        {
            return $"Argument '{argument.Name}' ma nieprawidłową wartość " +
                $"'{value}'. Oczekiwany typ: {argument.Type}.";
        }
    }
}
