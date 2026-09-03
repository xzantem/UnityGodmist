using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Data.Quests;
using Godmist.Data.World;
using Godmist.Data.Items;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Debugging
{
    public readonly struct DevConsoleCommandResult
    {
        public bool Success { get; }
        public string Message { get; }

        public DevConsoleCommandResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }
    }

    public class DevConsoleCommandService
    {
        private readonly CharacterProgressionSystem progressionSystem = new();
        private readonly InventoryService inventoryService = new();
        private readonly ItemInstanceFactory itemInstanceFactory = new();
        private readonly EquipmentGenerationService equipmentGenerationService = new();
        private readonly EquipmentUpgradeService equipmentUpgradeService = new();
        private readonly StatusEffectSystem statusEffectSystem = new();

        private int? debugSeed;

        public int? DebugSeed => debugSeed;

        public DevConsoleCommandResult Execute(
            string commandLine,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog = null,
            Func<string, DevConsoleCommandResult> combatHandler = null,
            Func<bool?, DevConsoleCommandResult> noclipHandler = null,
            Func<float, float, DevConsoleCommandResult> teleportHandler = null,
            Func<IReadOnlyList<string>, int?, DevConsoleCommandResult> gotoHandler = null,
            Func<int, DevConsoleCommandResult> advanceHandler = null,
            Func<float, DevConsoleCommandResult> tickHandler = null,
            Func<CombatOutcomeState, DevConsoleCommandResult> forceCombatResultHandler = null,
            Func<string, DevConsoleCommandResult> dialogueHandler = null)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return Fail("Wpisz komendę albo użyj: help");
            }

            if (!DevConsoleCommandSchemaParser.TryNormalize(
                    commandLine,
                    out var tokens,
                    out var schemaError))
            {
                return Fail(schemaError);
            }

            var command = tokens[0].ToLowerInvariant();

            return command switch
            {
                "help" => GetHelp(tokens),
                "state" => GetState(gameSession, combatSession),
                "characters" => GetCharacters(gameSession, combatSession),
                "queststate" => GetQuestState(tokens, gameSession),
                "worldflags" => GetWorldFlags(gameSession),
                "setworldflag" => SetWorldFlag(tokens, gameSession),
                "gold" => AddGold(tokens, gameSession),
                "xp" => AddExperience(tokens, gameSession, combatSession),
                "heal" => Heal(tokens, gameSession, combatSession),
                "resource" => RestoreResource(tokens, gameSession, combatSession),
                "godmode" => ToggleGodMode(tokens, gameSession, combatSession),
                "sethp" => SetHealth(tokens, gameSession, combatSession),
                "setresource" => SetResource(tokens, gameSession, combatSession),
                "giveitem" => GiveItem(tokens, gameSession, combatSession, contentCatalog),
                "createitem" => CreateItem(tokens, gameSession, contentCatalog),
                "createconsumable" => CreateConsumable(tokens, gameSession, contentCatalog),
                "items" => GetInventory(tokens, gameSession, combatSession),
                "inspectitem" => InspectItem(tokens, gameSession),
                "itemdefs" => GetItemDefinitions(contentCatalog),
                "encounterdefs" => GetEncounterDefinitions(contentCatalog),
                "combat" => StartCombat(tokens, combatSession, combatHandler),
                "createequipment" => CreateEquipment(tokens, gameSession, contentCatalog),
                "upgrade" => UpgradeEquipment(tokens, gameSession, contentCatalog),
                "reforge" => ReforgeEquipment(tokens, gameSession, contentCatalog),
                "markquestobjective" => MarkQuestObjective(tokens, gameSession),
                "removeitem" => RemoveItem(tokens, gameSession),
                "clearinventory" => ClearInventory(tokens, gameSession),
                "setlevel" => SetLevel(tokens, gameSession),
                "noclip" => SetNoclip(tokens, noclipHandler),
                "revealmap" => SetMapVisibility(gameSession, true),
                "clearmap" => SetMapVisibility(gameSession, false),
                "goto" => Goto(tokens, gotoHandler, debugSeed),
                "teleport" => Teleport(tokens, teleportHandler),
                "advance" => Advance(tokens, advanceHandler),
                "tick" => Tick(tokens, tickHandler),
                "applystatus" => ApplyStatus(tokens, gameSession, combatSession),
                "clearstatuses" => ClearStatuses(tokens, gameSession, combatSession),
                "forcecombatresult" => ForceCombatResult(tokens, combatSession, forceCombatResultHandler),
                "seed" => SetSeed(tokens),
                "triggerdialogue" => TriggerDialogue(tokens, dialogueHandler),
                "inspectdungeon" => InspectDungeon(gameSession),
                "inspectroom" => InspectRoom(tokens, gameSession),
                "spawnobject" => SpawnObject(tokens, gameSession),
                "unit" => InspectUnit(tokens, gameSession, combatSession),
                _ => Fail($"Nieznana komenda: {tokens[0]}. Użyj: help")
            };
        }

        private static DevConsoleCommandResult GetHelp(IReadOnlyList<string> tokens)
        {
            if (tokens.Count > 2)
            {
                return Fail("Użycie: help [command]");
            }

            if (tokens.Count == 2)
            {
                if (!DevConsoleCommandSchemaRegistry.TryGet(tokens[1], out var schema))
                {
                    return Fail($"Nieznana komenda: {tokens[1]}. Użyj: help");
                }

                var output = new StringBuilder()
                    .AppendLine($"{schema.Name} - {schema.Description}");

                foreach (var signature in schema.Signatures)
                {
                    output.AppendLine($"Użycie: {signature.Usage}");
                }

                foreach (var example in schema.Examples)
                {
                    output.AppendLine($"Przykład: {example}");
                }

                return Success(output.ToString().TrimEnd());
            }

            return Success(DevConsoleCommandSchemaRegistry.BuildHelp());
        }

        private DevConsoleCommandResult MarkQuestObjective(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count < 3 || tokens.Count > 4)
            {
                return Fail("Użycie: markquestobjective <questId> <objectiveId> [amount]");
            }

            if (gameSession == null || !gameSession.TryGetQuest(tokens[1], out var quest))
            {
                return Fail($"Nie znaleziono questa o ID: {tokens[1]}");
            }

            var amount = 1;
            if (tokens.Count == 4
                && (!int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
                    || amount <= 0))
            {
                return Fail("amount musi być dodatnią liczbą całkowitą.");
            }

            if (!quest.TryAddObjectiveProgress(tokens[2], amount)
                || !quest.TryGetObjectiveProgress(tokens[2], out var progress))
            {
                return Fail("Cel nie jest aktywny, nie należy do bieżącego etapu albo osiągnął już wymagany postęp.");
            }

            var requiredAmount = quest.CurrentStage?.FindObjective(tokens[2])?.RequiredAmount ?? progress.CurrentAmount;
            return Success(
                $"Postęp celu {tokens[2]}: {progress.CurrentAmount}/{requiredAmount}.");
        }

        private DevConsoleCommandResult RemoveItem(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count != 3) return Fail("Użycie: removeitem <characterId> <instanceId>");
            if (!TryResolveCharacter(tokens[1], gameSession, null, out var character))
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");

            if (!inventoryService.TryRemoveItem(character.Inventory, tokens[2], out var removed))
            {
                return Fail($"Nie znaleziono itemu {tokens[2]} w inventory postaci.");
            }

            return Success($"Usunięto {removed.Definition?.Id ?? removed.InstanceId} z inventory {GetCharacterName(character)}.");
        }

        private DevConsoleCommandResult ClearInventory(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count > 2) return Fail("Użycie: clearinventory [characterId]");

            var characterId = tokens.Count == 2 ? tokens[1] : gameSession?.PlayerParty?.ActiveLeaderId;
            if (!TryResolveCharacter(characterId, gameSession, null, out var character))
                return Fail("Nie znaleziono postaci docelowej.");

            var count = character.Inventory?.Items?.Count ?? 0;
            character.Inventory?.Items?.Clear();
            return Success($"Wyczyszczono inventory {GetCharacterName(character)}. Usunięto itemów: {count}.");
        }

        private DevConsoleCommandResult SetLevel(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count != 3) return Fail("Użycie: setlevel <characterId> <level>");
            if (!TryResolveCharacter(tokens[1], gameSession, null, out var character))
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
                return Fail("level musi być liczbą całkowitą.");
            if (!progressionSystem.TrySetLevel(character, level, out var previousLevel))
                return Fail($"level musi mieścić się w zakresie {CharacterProgressionSystem.MinimumLevel}-{CharacterProgressionSystem.MaximumLevel}.");

            return Success($"Ustawiono poziom {GetCharacterName(character)}: {previousLevel} -> {character.Level}.");
        }

        private static DevConsoleCommandResult SetNoclip(
            IReadOnlyList<string> tokens,
            Func<bool?, DevConsoleCommandResult> handler)
        {
            if (tokens.Count > 2) return Fail("Użycie: noclip [on|off]");
            if (tokens.Count == 1)
            {
                return handler == null
                    ? Fail("Noclip jest dostępny tylko w scenie eksploracji.")
                    : handler(null);
            }

            var enabled = string.Equals(tokens[1], "on", StringComparison.OrdinalIgnoreCase);
            if (!enabled && !string.Equals(tokens[1], "off", StringComparison.OrdinalIgnoreCase))
                return Fail("Tryb noclip musi mieć wartość on albo off.");
            return handler == null ? Fail("Noclip jest dostępny tylko w scenie eksploracji.") : handler(enabled);
        }

        private static DevConsoleCommandResult SetMapVisibility(GameSession gameSession, bool visible)
        {
            var dungeon = gameSession?.ActiveDungeon;
            if (dungeon == null) return Fail("Brak aktywnego lochu.");

            var changed = 0;
            foreach (var floor in dungeon.Floors)
            {
                foreach (var room in floor.Rooms)
                {
                    if (room != null && room.IsVisited != visible)
                    {
                        floor.TryRestoreRoomVisited(room.RoomId, visible);
                        changed++;
                    }
                }
            }

            return Success(visible
                ? $"Odkryto pomieszczenia lochu. Zmieniono: {changed}."
                : $"Ukryto pomieszczenia lochu. Zmieniono: {changed}.");
        }

        private static DevConsoleCommandResult Goto(
            IReadOnlyList<string> tokens,
            Func<IReadOnlyList<string>, int?, DevConsoleCommandResult> handler,
            int? seed)
        {
            if (tokens.Count < 2 || tokens.Count > 4)
                return Fail("Użycie: goto <dungeonId> [floor] [roomId]");
            return handler == null ? Fail("goto jest dostępne tylko w scenie gry.") : handler(tokens, seed);
        }

        private static DevConsoleCommandResult Teleport(
            IReadOnlyList<string> tokens,
            Func<float, float, DevConsoleCommandResult> handler)
        {
            if (tokens.Count != 3) return Fail("Użycie: teleport <x> <y>");
            if (!TryReadFloat(tokens[1], out var x) || !TryReadFloat(tokens[2], out var y))
                return Fail("Współrzędne muszą być skończonymi liczbami.");
            return handler == null ? Fail("teleport jest dostępne tylko w scenie eksploracji.") : handler(x, y);
        }

        private static DevConsoleCommandResult Advance(
            IReadOnlyList<string> tokens,
            Func<int, DevConsoleCommandResult> handler)
        {
            if (tokens.Count > 2) return Fail("Użycie: advance [turns]");
            var turns = 1;
            if (tokens.Count == 2 && (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out turns) || turns <= 0))
                return Fail("turns musi być dodatnią liczbą całkowitą.");
            return handler == null ? Fail("advance jest dostępne tylko podczas aktywnej walki.") : handler(turns);
        }

        private static DevConsoleCommandResult Tick(
            IReadOnlyList<string> tokens,
            Func<float, DevConsoleCommandResult> handler)
        {
            if (tokens.Count != 2 || !TryReadFloat(tokens[1], out var seconds) || seconds <= 0f)
                return Fail("Użycie: tick <dodatnia liczba sekund>");
            return handler == null ? Fail("tick nie jest dostępny w bieżącym kontekście gry.") : handler(seconds);
        }

        private DevConsoleCommandResult ApplyStatus(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count < 3 || tokens.Count > 5)
                return Fail("Użycie: applystatus <characterId> <statusId> [duration] [stacks]");
            if (!TryResolveCharacter(tokens[1], gameSession, combatSession, out var character))
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            var duration = 1;
            var stacks = 1;
            if (tokens.Count >= 4 && (!int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out duration) || duration <= 0))
                return Fail("duration musi być dodatnią liczbą całkowitą.");
            if (tokens.Count == 5 && (!int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out stacks) || stacks <= 0))
                return Fail("stacks musi być dodatnią liczbą całkowitą.");
            if (!Godmist.Core.Constants.StatusEffectIds.KnownIds.Any(id => string.Equals(id, tokens[2], StringComparison.OrdinalIgnoreCase)))
                return Fail($"Nieznany status: {tokens[2]}.");

            var unit = combatSession?.Units?.FirstOrDefault(candidate => candidate?.Character == character)
                       ?? new BattleUnit { Character = character, IsPlayerSide = true };
            var applied = statusEffectSystem.ApplyStatus(null, unit, tokens[2], 1f, duration, stacks, 0f, 0f, "devconsole", publishFeedback: false);
            return applied
                ? Success($"Nałożono status {tokens[2]} na {GetCharacterName(character)} na {duration} tur, stosy: {stacks}.")
                : Fail($"Nie udało się nałożyć statusu {tokens[2]} (postać może być odporna albo nieaktywna).");
        }

        private static DevConsoleCommandResult ClearStatuses(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count != 2) return Fail("Użycie: clearstatuses <characterId>");
            if (!TryResolveCharacter(tokens[1], gameSession, combatSession, out var character))
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");

            var lifecycle = new PassiveEffectLifecycleSystem();
            var removed = 0;
            foreach (var statusId in Godmist.Core.Constants.StatusEffectIds.KnownIds)
            {
                while (lifecycle.RemoveEffect(character, statusId)) removed++;
            }
            return Success($"Usunięto statusy z {GetCharacterName(character)}: {removed}.");
        }

        private static DevConsoleCommandResult ForceCombatResult(
            IReadOnlyList<string> tokens,
            CombatSession combatSession,
            Func<CombatOutcomeState, DevConsoleCommandResult> handler)
        {
            if (tokens.Count != 2) return Fail("Użycie: forcecombatresult <win|loss|escape>");
            var result = tokens[1].ToLowerInvariant() switch
            {
                "win" or "victory" => CombatOutcomeState.Victory,
                "loss" or "defeat" => CombatOutcomeState.Defeat,
                "escape" or "escaped" => CombatOutcomeState.Escaped,
                _ => (CombatOutcomeState?)null
            };
            if (!result.HasValue) return Fail("Wynik musi mieć wartość win, loss albo escape.");
            if (combatSession == null || combatSession.Outcome != CombatOutcomeState.Ongoing)
                return Fail("Brak aktywnej walki.");
            if (handler != null) return handler(result.Value);

            combatSession.Outcome = result.Value;
            return Success($"Wymuszono wynik walki: {result.Value}.");
        }

        private DevConsoleCommandResult SetSeed(IReadOnlyList<string> tokens)
        {
            if (tokens.Count != 2 || !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                return Fail("Użycie: seed <liczba całkowita>");
            debugSeed = seed;
            UnityEngine.Random.InitState(seed);
            return Success($"Ustawiono seed debugowy: {seed}. Nowe generowanie dungeonów użyje tego ziarna.");
        }

        private static DevConsoleCommandResult TriggerDialogue(
            IReadOnlyList<string> tokens,
            Func<string, DevConsoleCommandResult> handler)
        {
            if (tokens.Count != 2) return Fail("Użycie: triggerdialogue <dialogueId>");
            return handler == null ? Fail("triggerdialogue jest dostępne tylko w scenie gry.") : handler(tokens[1]);
        }

        private static DevConsoleCommandResult InspectDungeon(GameSession gameSession)
        {
            var dungeon = gameSession?.ActiveDungeon;
            if (dungeon == null) return Fail("Brak aktywnego lochu.");
            var output = new StringBuilder()
                .AppendLine($"Dungeon: {dungeon.DungeonId}")
                .AppendLine($"Seed: {dungeon.Seed}")
                .AppendLine($"Floor: {dungeon.CurrentFloorIndex}")
                .AppendLine($"PlayerPosition: {dungeon.PlayerPosition}")
                .AppendLine($"Cleared encounters: {dungeon.ClearedEncounterIds.Count}");
            foreach (var floor in dungeon.Floors)
                output.AppendLine($"- Floor {floor.FloorIndex}: rooms={floor.Rooms.Count}, encounters={floor.Encounters.Count}, objects={floor.Objects.Count}, visited={floor.Rooms.Count(room => room.IsVisited)}");
            return Success(output.ToString().TrimEnd());
        }

        private static DevConsoleCommandResult InspectRoom(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count > 2) return Fail("Użycie: inspectroom [roomId]");
            var dungeon = gameSession?.ActiveDungeon;
            if (dungeon == null || !dungeon.TryGetCurrentFloor(out var floor)) return Fail("Brak aktywnego piętra lochu.");
            var roomId = tokens.Count == 2 ? tokens[1] : floor.Rooms.FirstOrDefault(room => room.IsVisited)?.RoomId;
            if (!floor.TryGetRoom(roomId, out var room)) return Fail("Nie znaleziono pomieszczenia.");
            var output = new StringBuilder($"Room: {room.RoomId} ({room.RoomType}) visited={room.IsVisited}");
            foreach (var encounter in floor.Encounters.Where(item => item != null && item.RoomId == room.RoomId)) output.Append($"\n- Encounter {encounter.InstanceId} ({encounter.Definition?.Id})");
            foreach (var dungeonObject in floor.Objects.Where(item => item != null && item.Position == room.LayoutPosition || item != null && floor.Map != null && floor.Map.TryGetRoomId(item.Position, out var mappedRoomId) && mappedRoomId == room.RoomId)) output.Append($"\n- Object {dungeonObject.InstanceId} ({dungeonObject.Definition?.Id}) active={dungeonObject.IsActive} at {dungeonObject.Position}");
            return Success(output.ToString());
        }

        private static DevConsoleCommandResult SpawnObject(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count < 4 || tokens.Count > 5) return Fail("Użycie: spawnobject <objectId> <x> <y> [instanceId]");
            var dungeon = gameSession?.ActiveDungeon;
            if (dungeon == null || !dungeon.TryGetCurrentFloor(out var floor) || dungeon.Definition?.ObjectDefinitions == null)
                return Fail("Brak aktywnego piętra lochu albo definicji obiektów.");
            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                return Fail("x i y muszą być liczbami całkowitymi.");
            var definition = dungeon.Definition.ObjectDefinitions.FirstOrDefault(item => item != null && item.Id == tokens[1]);
            if (definition == null) return Fail($"Nie znaleziono obiektu dungeonowego: {tokens[1]}.");
            var position = new Vector2Int(x, y);
            if (floor.Map == null) return Fail("Aktywne piętro nie ma mapy.");
            if (definition is not DoorDungeonObjectDefinition
                && !floor.Map.IsWalkable(position))
                return Fail("Pozycja obiektu musi znajdować się na przechodniej komórce mapy.");
            var instanceId = tokens.Count == 5 ? tokens[4] : $"dev.{definition.Id}.{Guid.NewGuid():N}";
            DungeonObjectInstance instance = definition switch
            {
                DoorDungeonObjectDefinition door => new DoorDungeonObjectInstance(
                    instanceId,
                    door,
                    new DungeonCellEdge(position, position + Vector2Int.right)),
                ChestDungeonObjectDefinition chest => new ChestDungeonObjectInstance(instanceId, chest, new Vector2Int(x, y), Array.Empty<ItemInstance>()),
                ObstacleDungeonObjectDefinition obstacle => new ObstacleDungeonObjectInstance(instanceId, obstacle, new Vector2Int(x, y)),
                SpikeTrapDungeonObjectDefinition trap => new SpikeTrapDungeonObjectInstance(instanceId, trap, new Vector2Int(x, y)),
                LeverDungeonObjectDefinition lever => new LeverDungeonObjectInstance(instanceId, lever, new Vector2Int(x, y)),
                ConditionDungeonObjectDefinition condition => new ConditionDungeonObjectInstance(instanceId, condition, new Vector2Int(x, y)),
                _ => null
            };
            if (instance == null || !floor.TryAddObject(instance)) return Fail("Nie udało się dodać obiektu; komórka może być zajęta albo nieprzechodnia.");
            return Success($"Dodano obiekt {definition.Id} jako {instanceId} na ({x}, {y}).");
        }

        private static DevConsoleCommandResult GetState(
            GameSession gameSession,
            CombatSession combatSession)
        {
            var mode = gameSession?.CurrentMode.ToString() ?? "brak sesji";
            var gold = gameSession?.Gold ?? 0;

            if (combatSession == null)
            {
                return Success(
                    $"Tryb: {mode}\n" +
                    $"Złoto: {gold}\n" +
                    "Walka: brak");
            }

            var activeUnit = combatSession.ActiveUnit;
            var activeName = GetCharacterName(activeUnit?.Character);

            return Success(
                $"Tryb: {mode}\n" +
                $"Złoto: {gold}\n" +
                $"Walka: {combatSession.CombatId}\n" +
                $"Wynik: {combatSession.Outcome}\n" +
                $"Aktywna jednostka: {activeName}\n" +
                $"Jednostki: {combatSession.Units.Count}");
        }

        private static DevConsoleCommandResult GetCharacters(
            GameSession gameSession,
            CombatSession combatSession)
        {
            var characters = GetKnownCharacters(gameSession, combatSession);

            if (characters.Count == 0)
            {
                return Fail("Nie znaleziono żadnych postaci.");
            }

            var output = new StringBuilder("Postacie:");

            foreach (var character in characters)
            {
                var resource = character.ResourcePool;
                var resourceText = resource == null
                    ? "brak zasobu"
                    : $"{resource.ResourceType}: {resource.CurrentValue}/{resource.MaxValue}";

                var godModeText = character.DebugIsInvulnerable
                    ? " | GODMODE"
                    : string.Empty;

                output.Append(
                    $"\n- {character.CharacterId} | " +
                    $"{GetCharacterName(character)} | " +
                    $"HP {character.CurrentHealth}/{GetMaxHealth(character)} | " +
                    $"{resourceText}{godModeText}");
            }

            return Success(output.ToString());
        }

        private static DevConsoleCommandResult GetQuestState(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (gameSession == null)
            {
                return Fail("Brak aktywnej GameSession.");
            }

            if (tokens.Count > 2)
            {
                return Fail("Użycie: queststate [questId]");
            }

            var quests = tokens.Count == 2
                ? gameSession.QuestsById.TryGetValue(tokens[1], out var selectedQuest)
                    ? new[] { selectedQuest }
                    : Array.Empty<QuestInstance>()
                : gameSession.QuestsById.Values
                    .Where(quest => quest != null)
                    .OrderBy(quest => quest.QuestId)
                    .ToArray();

            if (quests.Length == 0)
            {
                return Fail(tokens.Count == 2
                    ? $"Nie znaleziono zarejestrowanego questa o ID: {tokens[1]}"
                    : "Sesja nie zawiera zarejestrowanych questów.");
            }

            var reports = quests.Select(BuildQuestReport);
            return Success(string.Join("\n\n---\n\n", reports));
        }

        private static string BuildQuestReport(QuestInstance quest)
        {
            var output = new StringBuilder()
                .AppendLine($"Quest: {quest.QuestId}")
                .AppendLine($"Nazwa: {quest.Definition?.DisplayName}")
                .AppendLine($"Status: {quest.Status}")
                .AppendLine($"Stage: {quest.CurrentStageId}")
                .AppendLine($"Ukryty: {quest.IsHidden}");

            var currentStage = quest.CurrentStage;
            if (currentStage != null)
            {
                output.AppendLine($"Stage description: {currentStage.Description}");
            }

            if (quest.ProgressByObjectiveId.Count > 0)
            {
                output.AppendLine("Cele:");

                foreach (var progress in quest.ProgressByObjectiveId.Values
                    .Where(progress => progress != null)
                    .OrderBy(progress => progress.ObjectiveId))
                {
                    var objective = quest.Definition?.Stages?
                        .SelectMany(stage => stage?.Objectives ?? new List<QuestObjectiveDefinition>())
                        .FirstOrDefault(candidate => candidate?.ObjectiveId == progress.ObjectiveId);
                    var required = Math.Max(1, objective?.RequiredAmount ?? 1);
                    output.AppendLine(
                        $"- {progress.ObjectiveId}: {progress.CurrentAmount}/{required}" +
                        (progress.IsCompleted ? " (completed)" : string.Empty));
                }
            }

            if (quest.QuestFlags.Count > 0)
            {
                output.AppendLine("Quest flags: " + string.Join(
                    ", ",
                    quest.QuestFlags.OrderBy(pair => pair.Key)
                        .Select(pair => $"{pair.Key}={pair.Value}")));
            }

            if (quest.ChosenBranchIds.Count > 0)
            {
                output.AppendLine("Branches: " + string.Join(
                    ", ", quest.ChosenBranchIds.OrderBy(id => id)));
            }

            return output.ToString().TrimEnd();
        }

        private static DevConsoleCommandResult GetWorldFlags(GameSession gameSession)
        {
            if (gameSession == null)
            {
                return Fail("Brak aktywnej GameSession.");
            }

            if (gameSession.WorldState.FlagsById.Count == 0)
            {
                return Success("Brak ustawionych world flags.");
            }

            var output = new StringBuilder("World flags:");
            foreach (var flag in gameSession.WorldState.FlagsById.OrderBy(pair => pair.Key))
            {
                output.Append($"\n- {flag.Key}={flag.Value}");
            }

            return Success(output.ToString());
        }

        private static DevConsoleCommandResult SetWorldFlag(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count != 3)
            {
                return Fail("Użycie: setworldflag <flagId> <value>");
            }

            if (gameSession == null)
            {
                return Fail("Brak aktywnej GameSession.");
            }

            var flagId = tokens[1];
            var value = tokens[2];

            if (gameSession.WorldState.TryGetFlag(flagId, out var currentValue)
                && currentValue == value)
            {
                return Success($"World flag '{flagId}' już ma wartość '{value}'.");
            }

            if (!gameSession.WorldState.TrySetFlag(flagId, value))
            {
                return Fail($"Nie udało się ustawić world flaga '{flagId}'.");
            }

            return Success($"Ustawiono world flag '{flagId}' = '{value}'.");
        }

        private static DevConsoleCommandResult InspectUnit(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count > 2)
            {
                return Fail("Użycie: unit [characterId]");
            }

            CharacterInstance target;

            if (tokens.Count == 2)
            {
                var combatMatches = combatSession?.Units?
                    .Where(unit => unit?.Character != null
                        && string.Equals(
                            unit.Character.CharacterId,
                            tokens[1],
                            StringComparison.OrdinalIgnoreCase))
                    .Select(unit => unit.Character)
                    .ToList();

                if (combatMatches != null && combatMatches.Count > 0)
                {
                    var reports = combatMatches.Select(character =>
                        DevConsoleUnitInspector.BuildReport(
                            character,
                            combatSession));

                    return Success(string.Join("\n\n---\n\n", reports));
                }

                if (!TryResolveCharacter(
                        tokens[1],
                        gameSession,
                        combatSession,
                        out target))
                {
                    return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
                }
            }
            else
            {
                target = combatSession?.ActiveUnit?.Character;

                if (target == null
                    && gameSession != null
                    && !string.IsNullOrWhiteSpace(
                        gameSession.PlayerParty.ActiveLeaderId))
                {
                    gameSession.CharactersById.TryGetValue(
                        gameSession.PlayerParty.ActiveLeaderId,
                        out target);
                }

                if (target == null)
                {
                    return Fail(
                        "Brak domyślnej postaci. Użycie: unit <characterId>");
                }
            }

            return Success(
                DevConsoleUnitInspector.BuildReport(target, combatSession));
        }

        private static DevConsoleCommandResult AddGold(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count != 2)
            {
                return Fail("Użycie: gold <liczba>");
            }

            if (!TryReadAmount(tokens, 1, out var amount, out var failure))
            {
                return failure;
            }

            if (gameSession == null)
            {
                return Fail("Brak aktywnej GameSession.");
            }

            if (!gameSession.TryAddGold(amount))
            {
                return Fail("Nie udało się dodać złota.");
            }

            return Success(
                $"Dodano {amount} złota. Aktualne złoto: {gameSession.Gold}.");
        }

        private DevConsoleCommandResult AddExperience(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (!TryReadOptionalCharacterTarget(
                    tokens,
                    out var characterId,
                    out var amount,
                    out var failure))
            {
                return failure;
            }

            List<CharacterInstance> targets;

            if (string.IsNullOrWhiteSpace(characterId))
            {
                targets = GetActivePartyCharacters(gameSession, combatSession);
            }
            else if (TryResolveCharacter(
                         characterId,
                         gameSession,
                         combatSession,
                         out var character))
            {
                targets = new List<CharacterInstance> { character };
            }
            else
            {
                return Fail($"Nie znaleziono postaci o ID: {characterId}");
            }

            if (targets.Count == 0)
            {
                return Fail("Nie znaleziono postaci gracza.");
            }

            var levelsGained = 0;

            foreach (var target in targets)
            {
                levelsGained += progressionSystem.AddExperience(target, amount);
            }

            return Success(
                $"Dodano {amount} XP postaciom: {targets.Count}. " +
                $"Zdobyte poziomy: {levelsGained}.");
        }

        private static DevConsoleCommandResult Heal(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (!TryReadOptionalCharacterTarget(
                    tokens,
                    out var characterId,
                    out var amount,
                    out var failure))
            {
                return failure;
            }

            CharacterInstance target;

            if (string.IsNullOrWhiteSpace(characterId))
            {
                target = combatSession?.ActiveUnit?.Character;

                if (target == null)
                {
                    return Fail(
                        "Poza walką użyj: heal <characterId> <liczba>");
                }
            }
            else if (!TryResolveCharacter(
                         characterId,
                         gameSession,
                         combatSession,
                         out target))
            {
                return Fail($"Nie znaleziono postaci o ID: {characterId}");
            }

            if (target.LifeState != CharacterLifeState.Alive)
            {
                return Fail("Komenda heal działa tylko na żywej postaci.");
            }

            var maxHealth = GetMaxHealth(target);

            if (maxHealth <= 0)
            {
                return Fail("Postać nie ma poprawnego MaxHealth.");
            }

            var previousHealth = target.CurrentHealth;

            target.CurrentHealth = Math.Min(
                maxHealth,
                target.CurrentHealth + amount);

            return Success(
                $"Uleczono {GetCharacterName(target)} o " +
                $"{target.CurrentHealth - previousHealth} HP. " +
                $"HP: {target.CurrentHealth}/{maxHealth}.");
        }

        private static DevConsoleCommandResult RestoreResource(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (!TryReadOptionalCharacterTarget(
                    tokens,
                    out var characterId,
                    out var amount,
                    out var failure))
            {
                return failure;
            }

            CharacterInstance target;

            if (string.IsNullOrWhiteSpace(characterId))
            {
                target = combatSession?.ActiveUnit?.Character;

                if (target == null)
                {
                    return Fail(
                        "Poza walką użyj: resource <characterId> <liczba>");
                }
            }
            else if (!TryResolveCharacter(
                         characterId,
                         gameSession,
                         combatSession,
                         out target))
            {
                return Fail($"Nie znaleziono postaci o ID: {characterId}");
            }

            if (target.ResourcePool == null)
            {
                return Fail("Postać nie ma ResourcePool.");
            }

            var restored = target.ResourcePool.Restore(amount);

            return Success(
                $"Przywrócono {GetCharacterName(target)} " +
                $"{restored} zasobu. " +
                $"{target.ResourcePool.ResourceType}: " +
                $"{target.ResourcePool.CurrentValue}/" +
                $"{target.ResourcePool.MaxValue}.");
        }

        private static DevConsoleCommandResult ToggleGodMode(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count != 2)
            {
                return Fail("Użycie: godmode <characterId>");
            }

            if (!TryResolveCharacter(
                    tokens[1],
                    gameSession,
                    combatSession,
                    out var target))
            {
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            }

            target.DebugIsInvulnerable = !target.DebugIsInvulnerable;

            var state = target.DebugIsInvulnerable
                ? "włączony"
                : "wyłączony";

            return Success(
                $"Godmode dla {GetCharacterName(target)}: {state}.");
        }

        private static DevConsoleCommandResult SetHealth(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count != 3)
            {
                return Fail("Użycie: sethp <characterId> <liczba>");
            }

            if (!TryReadAmount(tokens, 2, out var value, out var failure))
            {
                return failure;
            }

            if (!TryResolveCharacter(
                    tokens[1],
                    gameSession,
                    combatSession,
                    out var target))
            {
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            }

            if (target.LifeState != CharacterLifeState.Alive)
            {
                return Fail(
                    "Komenda sethp działa tylko na żywej postaci.");
            }

            var maxHealth = GetMaxHealth(target);

            if (maxHealth <= 0)
            {
                return Fail("Postać nie ma poprawnego MaxHealth.");
            }

            target.CurrentHealth = Math.Min(value, maxHealth);

            return Success(
                $"Ustawiono HP {GetCharacterName(target)}: " +
                $"{target.CurrentHealth}/{maxHealth}.");
        }

        private static DevConsoleCommandResult SetResource(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count != 3)
            {
                return Fail("Użycie: setresource <characterId> <liczba>");
            }

            if (!TryReadAmount(tokens, 2, out var value, out var failure))
            {
                return failure;
            }

            if (!TryResolveCharacter(
                    tokens[1],
                    gameSession,
                    combatSession,
                    out var target))
            {
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            }

            if (target.ResourcePool == null)
            {
                return Fail("Postać nie ma ResourcePool.");
            }

            target.ResourcePool.CurrentValue = Math.Min(
                value,
                target.ResourcePool.MaxValue);

            return Success(
                $"Ustawiono zasób {GetCharacterName(target)}: " +
                $"{target.ResourcePool.CurrentValue}/" +
                $"{target.ResourcePool.MaxValue}.");
        }

        private DevConsoleCommandResult GiveItem(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog)
        {
            if (tokens.Count < 3 || tokens.Count > 4)
            {
                return Fail(
                    "Użycie: giveitem <characterId> <itemId> [ilość]");
            }

            if (contentCatalog == null)
            {
                return Fail("Brak GameContentCatalog.");
            }

            if (!TryResolveCharacter(
                    tokens[1],
                    gameSession,
                    combatSession,
                    out var target))
            {
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            }

            if (!contentCatalog.TryGetItem(tokens[2], out var definition))
            {
                return Fail($"Nie znaleziono itemu o ID: {tokens[2]}");
            }

            var amount = 1;

            if (tokens.Count == 4
                && !TryReadAmount(tokens, 3, out amount, out var failure))
            {
                return failure;
            }

            var addedCount = 0;

            for (var index = 0; index < amount; index++)
            {
                var item = itemInstanceFactory.Create(definition);

                if (!inventoryService.TryAddItem(target.Inventory, item))
                {
                    break;
                }

                addedCount++;
            }

            if (addedCount == 0)
            {
                return Fail(
                    $"Nie udało się dodać '{definition.DisplayName}' do inventory.");
            }

            return Success(
                $"Dodano {addedCount}x {definition.DisplayName} do inventory " +
                $"postaci {GetCharacterName(target)}.");
        }

        private DevConsoleCommandResult CreateItem(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            if (gameSession == null || contentCatalog == null)
                return Fail("Brak GameSession albo GameContentCatalog.");
            if (!contentCatalog.TryGetItem(tokens[1], out var definition))
                return Fail($"Nie znaleziono itemu o ID: {tokens[1]}");

            var amount = tokens.Count >= 3
                ? int.Parse(tokens[2], CultureInfo.InvariantCulture)
                : 1;
            var target = ResolveDefaultCharacter(
                gameSession,
                tokens.Count >= 4 ? tokens[3] : string.Empty,
                out var targetFailure);

            if (target == null)
            {
                return targetFailure;
            }

            var createdCount = 0;

            for (var index = 0; index < amount; index++)
            {
                ItemInstance item;

                if (definition is LootBagItemDefinition lootBagDefinition)
                {
                    item = new LootTableSystem(
                            new System.Random(),
                            contentCatalog.EquipmentPartDefinitions,
                            itemInstanceFactory,
                            target.Level)
                        .CreateLootBag(lootBagDefinition);
                }
                else if (definition is EquipmentItemDefinition equipmentDefinition)
                {
                    item = equipmentGenerationService.Generate(
                        equipmentDefinition,
                        contentCatalog.EquipmentPartDefinitions);

                    if (item == null)
                    {
                        return Fail(
                            "Nie udało się wygenerować equipmentu. Sprawdź Required Class " +
                            "EID oraz komplet trzech części tej klasy w GameContentCatalog.");
                    }
                }
                else
                {
                    item = itemInstanceFactory.Create(definition);
                }

                if (!inventoryService.TryAddItem(target.Inventory, item))
                {
                    break;
                }

                createdCount++;
            }

            if (createdCount == 0)
            {
                return Fail(
                    $"Nie udało się dodać '{definition.DisplayName}' do inventory " +
                    $"postaci {GetCharacterName(target)}.");
            }

            return Success(
                $"Utworzono {createdCount}x {definition.DisplayName} w inventory " +
                $"postaci {GetCharacterName(target)}.");
        }

        private DevConsoleCommandResult CreateConsumable(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            if (tokens.Count < 2)
                return Fail("Użycie: createconsumable <itemId> [Effect value ...]. Potion/bomb wymagają dokładnie 3 efektów; czasy statusów są implied. Bomby mają bazowo zasięg 5 i obszar 3x3; katalizator zwiększa te parametry.");
            if (gameSession == null || contentCatalog == null)
                return Fail("Brak GameSession albo GameContentCatalog.");

            var effectTokens = ExtractNamedConsumableArguments(
                tokens,
                out var namedItemId,
                out var namedCharacterId);
            var itemId = string.IsNullOrWhiteSpace(namedItemId)
                ? effectTokens[1]
                : namedItemId;

            if (!contentCatalog.TryGetItem(itemId, out var definition)
                || definition is not ConsumableItemDefinition consumableDefinition)
                return Fail($"'{itemId}' nie jest ConsumableItemDefinition.");
            if (consumableDefinition.UseType == ConsumableUseType.Utility)
                return Fail($"Utility '{itemId}' nie przyjmuje efektów. Użyj: createitem {itemId}");

            var positionalCharacterId = string.IsNullOrWhiteSpace(namedCharacterId)
                && effectTokens.Count > 2
                && TryResolveCharacter(
                    effectTokens[^1],
                    gameSession,
                    null,
                    out _)
                && !Enum.TryParse<ConsumableRuntimeEffectType>(
                    effectTokens[^1],
                    true,
                    out _)
                ? effectTokens[^1]
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(positionalCharacterId))
            {
                effectTokens.RemoveAt(effectTokens.Count - 1);
            }

            var characterId = string.IsNullOrWhiteSpace(namedCharacterId)
                ? positionalCharacterId
                : namedCharacterId;
            var target = ResolveDefaultCharacter(
                gameSession,
                characterId,
                out var targetFailure);

            if (target == null)
            {
                return targetFailure;
            }

            var item = itemInstanceFactory.Create(consumableDefinition) as ConsumableItemInstance;
            if (item == null) return Fail("Nie udało się utworzyć instancji consumable.");
            var targetType = consumableDefinition.UseType == ConsumableUseType.Bomb
                ? TargetType.AreaAroundTile : TargetType.Single;
            var targetingType = consumableDefinition.UseType == ConsumableUseType.Bomb
                ? TargetingType.Enemies : TargetingType.Allies;

            if (effectTokens.Count == 3 && (effectTokens[2].Contains('=') || effectTokens[2].Contains(';')))
            {
                // Keep the old one-token syntax working for existing smoke-test commands.
                foreach (var spec in effectTokens[2].Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryParseLegacyEffect(spec, consumableDefinition.UseType, targetType, targetingType, out var runtimeEffect, out var error))
                        return Fail(error);
                    item.RuntimeEffects.Add(runtimeEffect);
                }
            }
            else
            {
                if (!TryParseSequentialEffects(effectTokens, consumableDefinition.UseType, targetType, targetingType, item.RuntimeEffects, out var error))
                    return Fail(error);
            }
            if (consumableDefinition.UseType == ConsumableUseType.Potion && item.RuntimeEffects.Count != 3)
                return Fail("Potion musi mieć dokładnie 3 efekty.");
            if (consumableDefinition.UseType == ConsumableUseType.Bomb && item.RuntimeEffects.Count != 3)
                return Fail("Bomba musi mieć dokładnie 3 efekty.");
            var illegalEffect = item.RuntimeEffects.FirstOrDefault(effect =>
                effect == null || !IsEffectAllowed(consumableDefinition.UseType, effect.EffectType));
            if (illegalEffect != null)
                return Fail($"Efekt '{illegalEffect.EffectType}' nie jest dostępny dla typu '{consumableDefinition.UseType}'.");
            var invalidValue = item.RuntimeEffects.FirstOrDefault(effect =>
                effect.Value < 0f
                || (effect.EffectType != ConsumableRuntimeEffectType.ModifyDodge
                    && effect.Value > 1f));
            if (invalidValue != null)
            {
                return Fail($"Nieprawidłowa wartość efektu '{invalidValue.EffectType}'.");
            }
            if (!inventoryService.TryAddItem(target.Inventory, item))
                return Fail(
                    $"Nie udało się dodać '{definition.DisplayName}' do inventory " +
                    $"postaci {GetCharacterName(target)}.");
            return Success(
                $"Utworzono {definition.DisplayName} z {item.RuntimeEffects.Count} " +
                $"runtime effectami dla {GetCharacterName(target)} | " +
                $"instanceId: {item.InstanceId}");
        }

        private static CharacterInstance ResolveDefaultCharacter(
            GameSession gameSession,
            string characterId,
            out DevConsoleCommandResult failure)
        {
            failure = default;

            if (!string.IsNullOrWhiteSpace(characterId))
            {
                if (TryResolveCharacter(
                        characterId,
                        gameSession,
                        null,
                        out var target))
                {
                    return target;
                }

                failure = Fail($"Nie znaleziono postaci o ID: {characterId}");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(
                    gameSession?.PlayerParty?.ActiveLeaderId)
                && gameSession.CharactersById.TryGetValue(
                    gameSession.PlayerParty.ActiveLeaderId,
                    out var leader))
            {
                return leader;
            }

            failure = Fail("Brak aktywnego lidera drużyny.");
            return null;
        }

        private static List<string> ExtractNamedConsumableArguments(
            IReadOnlyList<string> tokens,
            out string itemId,
            out string characterId)
        {
            itemId = string.Empty;
            characterId = string.Empty;

            var positionalTokens = new List<string> { tokens[0] };

            for (var index = 1; index < tokens.Count; index++)
            {
                var token = tokens[index];
                var separatorIndex = token.IndexOf('=');

                if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
                {
                    positionalTokens.Add(token);
                    continue;
                }

                var name = token.Substring(0, separatorIndex);
                var value = token.Substring(separatorIndex + 1);

                if (string.Equals(
                        name,
                        "itemId",
                        StringComparison.OrdinalIgnoreCase))
                {
                    itemId = value;
                }
                else if (string.Equals(
                             name,
                             "characterId",
                             StringComparison.OrdinalIgnoreCase))
                {
                    characterId = value;
                }
                else
                {
                    positionalTokens.Add(token);
                }
            }

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                if (positionalTokens.Count > 1)
                {
                    positionalTokens.RemoveAt(1);
                }

                positionalTokens.Insert(1, itemId);
            }

            return positionalTokens;
        }

        private static bool IsEffectAllowed(
            ConsumableUseType useType,
            ConsumableRuntimeEffectType effectType)
        {
            return useType switch
            {
                ConsumableUseType.Potion => effectType is
                    ConsumableRuntimeEffectType.RestoreHealth
                    or ConsumableRuntimeEffectType.RegenHealth
                    or ConsumableRuntimeEffectType.RestoreResource
                    or ConsumableRuntimeEffectType.RegenResource
                    or ConsumableRuntimeEffectType.ModifyMaxResource
                    or ConsumableRuntimeEffectType.ModifyDamage
                    or ConsumableRuntimeEffectType.ModifyPhysicalDefense
                    or ConsumableRuntimeEffectType.ModifyMagicalDefense
                    or ConsumableRuntimeEffectType.ModifyResistance
                    or ConsumableRuntimeEffectType.ModifySpeed
                    or ConsumableRuntimeEffectType.ModifyCritChance
                    or ConsumableRuntimeEffectType.ModifyDodge
                    or ConsumableRuntimeEffectType.ModifyActionPoints,
                ConsumableUseType.Bomb => effectType is
                    ConsumableRuntimeEffectType.DealPhysicalDamage
                    or ConsumableRuntimeEffectType.DealMagicalDamage
                    or ConsumableRuntimeEffectType.DealInevitableDamage
                    or ConsumableRuntimeEffectType.ApplyBleed
                    or ConsumableRuntimeEffectType.ApplyPoison
                    or ConsumableRuntimeEffectType.ApplyBurn
                    or ConsumableRuntimeEffectType.ApplyStun
                    or ConsumableRuntimeEffectType.ApplyFreeze
                    or ConsumableRuntimeEffectType.ModifyPhysicalDefense
                    or ConsumableRuntimeEffectType.ModifyMagicalDefense
                    or ConsumableRuntimeEffectType.ModifySpeed
                    or ConsumableRuntimeEffectType.ModifyCritChance
                    or ConsumableRuntimeEffectType.ModifyDodge
                    or ConsumableRuntimeEffectType.Slow,
                _ => false
            };
        }

        private static bool TryParseSequentialEffects(
            IReadOnlyList<string> tokens,
            ConsumableUseType useType,
            TargetType targetType,
            TargetingType targetingType,
            List<ConsumableEffectRuntime> effects,
            out string error)
        {
            error = string.Empty;
            if (tokens.Count < 4)
            {
                error = "Podaj pary: <EffectName> <value>. Potion musi mieć dokładnie 3 efekty.";
                return false;
            }

            var index = 2;
            while (index < tokens.Count)
            {
                if (!Enum.TryParse(tokens[index], true, out ConsumableRuntimeEffectType effectType))
                {
                    error = $"Nieznany efekt '{tokens[index]}'.";
                    return false;
                }

                if (index + 1 >= tokens.Count
                    || !float.TryParse(tokens[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    error = $"Efekt '{tokens[index]}' wymaga liczbowej wartości.";
                    return false;
                }

                var runtimeEffect = new ConsumableEffectRuntime
                {
                    EffectType = effectType,
                    Value = value,
                    TargetType = targetType,
                    TargetingType = targetingType
                };
                index += 2;

                var impliedStatusId = ConsumableRuntimeRules.GetImpliedStatusId(effectType);
                if (!string.IsNullOrWhiteSpace(impliedStatusId))
                {
                    runtimeEffect.StatusId = impliedStatusId;
                    runtimeEffect.DurationTurns = ConsumableRuntimeRules.GetDuration(useType, effectType);
                    if (index < tokens.Count
                        && int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        error = $"Efekt '{effectType}' ma czas trwania implied — nie podawaj liczby tur po wartości.";
                        return false;
                    }
                }
                else if (effectType == ConsumableRuntimeEffectType.ApplyStatus
                    || effectType == ConsumableRuntimeEffectType.RemoveStatus)
                {
                    if (index >= tokens.Count)
                    {
                        error = $"Efekt '{effectType}' wymaga identyfikatora statusu.";
                        return false;
                    }

                    runtimeEffect.StatusId = tokens[index++];
                    if (index < tokens.Count
                        && int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration))
                    {
                        runtimeEffect.DurationTurns = duration < 0 ? 0 : duration;
                        index++;
                    }
                }

                if (effectType == ConsumableRuntimeEffectType.ModifyResistance)
                {
                    if (index >= tokens.Count
                        || !Enum.TryParse(tokens[index], true, out ResistanceType resistanceType))
                    {
                        error = "Efekt 'ModifyResistance' wymaga typu odporności (Bleed, Poison, Fire, Weakening, Suppression albo Move).";
                        return false;
                    }

                    runtimeEffect.ResistanceType = resistanceType;
                    index++;
                }

                if (runtimeEffect.DurationTurns <= 0)
                {
                    runtimeEffect.DurationTurns = ConsumableRuntimeRules.GetDuration(useType, effectType);
                }

                effects.Add(runtimeEffect);
            }

            return true;
        }

        private static bool TryParseLegacyEffect(
            string spec,
            ConsumableUseType useType,
            TargetType targetType,
            TargetingType targetingType,
            out ConsumableEffectRuntime runtimeEffect,
            out string error)
        {
            runtimeEffect = null;
            error = string.Empty;
            var parts = spec.Split(',');
            var head = parts[0].Split('=', 2);
            if (head.Length != 2 || !Enum.TryParse(head[0], true, out ConsumableRuntimeEffectType effectType)
                || !float.TryParse(head[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                error = $"Nieprawidłowy effect spec '{spec}'. Użyj <EffectName>=<value>,...";
                return false;
            }

            var duration = 0;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out duration) || duration < 0))
            {
                error = $"Nieprawidłowy duration w '{spec}'.";
                return false;
            }

            runtimeEffect = new ConsumableEffectRuntime
            {
                EffectType = effectType,
                Value = value,
                DurationTurns = duration,
                StatusId = parts.Length >= 2 ? parts[1] : string.Empty,
                TargetType = targetType,
                TargetingType = targetingType
            };
            if (string.IsNullOrWhiteSpace(runtimeEffect.StatusId))
                    runtimeEffect.StatusId = ConsumableRuntimeRules.GetImpliedStatusId(effectType);
            runtimeEffect.DurationTurns = ConsumableRuntimeRules.GetDuration(
                useType, effectType, runtimeEffect.DurationTurns);
            if (effectType == ConsumableRuntimeEffectType.ModifyResistance
                && parts.Length >= 2
                && !Enum.TryParse(parts[1], true, out runtimeEffect.ResistanceType))
            {
                error = $"Nieznany typ odporności '{parts[1]}'.";
                return false;
            }
            return true;
        }

        private DevConsoleCommandResult CreateEquipment(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            if (tokens.Count < 6 || tokens.Count > 7)
            {
                return Fail("Użycie: createequipment <itemId> <quality> <partId> <partId> <partId> [characterId]");
            }

            if (gameSession == null || contentCatalog == null)
            {
                return Fail("Brak GameSession albo GameContentCatalog.");
            }

            if (!contentCatalog.TryGetItem(tokens[1], out var itemDefinition)
                || itemDefinition is not EquipmentItemDefinition equipmentDefinition)
            {
                return Fail($"'{tokens[1]}' nie jest EquipmentItemDefinition.");
            }

            if (!Enum.TryParse(tokens[2], true, out EquipmentQuality quality))
            {
                return Fail("Jakość musi być jedną z wartości: Poor, Normal, Excellent, Masterpiece.");
            }

            if (equipmentDefinition is BossUniqueEquipmentDefinition
                && quality != EquipmentQuality.Masterpiece)
            {
                return Fail("Bossowy unikat można utworzyć wyłącznie z jakością Masterpiece.");
            }

            var parts = new List<EquipmentPartDefinition>(3);
            for (var index = 3; index <= 5; index++)
            {
                var part = contentCatalog.EquipmentPartDefinitions?
                    .FirstOrDefault(candidate => candidate != null
                        && string.Equals(candidate.Id, tokens[index], StringComparison.OrdinalIgnoreCase));
                if (part == null)
                {
                    return Fail($"Nie znaleziono EquipmentPartDefinition o ID: {tokens[index]}");
                }

                parts.Add(part);
            }

            var characterId = tokens.Count == 7 ? tokens[6] : string.Empty;
            var recipient = ResolveDefaultCharacter(gameSession, characterId, out var targetFailure);
            if (recipient == null)
            {
                return targetFailure;
            }

            var createdItem = equipmentGenerationService.GenerateFromSelectedParts(
                equipmentDefinition,
                parts,
                quality);
            if (createdItem == null)
            {
                return Fail("Nie udało się wygenerować equipmentu. Sprawdź Required Class EID oraz komplet trzech zgodnych części w GameContentCatalog.");
            }

            if (!inventoryService.TryAddItem(recipient.Inventory, createdItem))
            {
                return Fail(
                    $"Nie udało się dodać equipmentu do inventory " +
                    $"postaci {GetCharacterName(recipient)}.");
            }

            return Success(
                $"Utworzono {createdItem.Definition.DisplayName} " +
                $"({quality}) w inventory postaci {GetCharacterName(recipient)} | " +
                $"instanceId: {createdItem.InstanceId}");
        }

        private DevConsoleCommandResult UpgradeEquipment(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            if (tokens.Count < 5 || tokens.Count > 6)
            {
                return Fail("Użycie: upgrade <npcId> <instanceId> <targetProgress> [successChance] [characterId]");
            }

            if (gameSession == null || contentCatalog == null)
            {
                return Fail("Brak GameSession albo GameContentCatalog.");
            }

            if (!TryResolveBlacksmith(gameSession, tokens[1], out var blacksmith, out var npcFailure))
            {
                return npcFailure;
            }

            if (!TryReadFloat(tokens[3], out var targetProgress)
                || !TryReadFloat(tokens[4], out var successChance))
            {
                return Fail("targetProgress i successChance muszą być skończonymi liczbami.");
            }

            var characterId = tokens.Count == 6 ? tokens[5] : string.Empty;
            var owner = ResolveDefaultCharacter(gameSession, characterId, out var targetFailure);
            if (owner == null)
            {
                return targetFailure;
            }

            if (!TryFindEquipment(owner, tokens[2], out var item))
            {
                return Fail($"Nie znaleziono equipmentu o instanceId: {tokens[2]}");
            }

            if (!equipmentUpgradeService.TryUpgrade(
                    gameSession,
                    blacksmith,
                    owner,
                    item,
                    targetProgress,
                    successChance,
                    contentCatalog,
                    out var succeeded,
                    out var paidGold,
                    out var failureReason))
            {
                return Fail($"Upgrade nieudany: {failureReason}");
            }

            return Success(
                succeeded
                    ? $"Upgrade zakończony sukcesem dla {GetCharacterName(owner)}. " +
                      $"Postęp: {item.UpgradeProgress:0.###}; zapłacono: {paidGold} złota."
                    : $"Upgrade nie powiódł się dla {GetCharacterName(owner)}. " +
                      $"Postęp po próbie: {item.UpgradeProgress:0.###}; zapłacono: {paidGold} złota.");
        }

        private DevConsoleCommandResult ReforgeEquipment(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            GameContentCatalog contentCatalog)
        {
            if (tokens.Count < 3 || tokens.Count > 4)
            {
                return Fail("Użycie: reforge <npcId> <instanceId> [characterId]");
            }

            if (gameSession == null || contentCatalog == null)
            {
                return Fail("Brak GameSession albo GameContentCatalog.");
            }

            if (!TryResolveBlacksmith(gameSession, tokens[1], out var blacksmith, out var npcFailure))
            {
                return npcFailure;
            }

            var characterId = tokens.Count == 4 ? tokens[3] : string.Empty;
            var owner = ResolveDefaultCharacter(gameSession, characterId, out var targetFailure);
            if (owner == null)
            {
                return targetFailure;
            }

            if (!TryFindEquipment(owner, tokens[2], out var item))
            {
                return Fail($"Nie znaleziono equipmentu o instanceId: {tokens[2]}");
            }

            if (!equipmentUpgradeService.TryReforge(
                    gameSession,
                    blacksmith,
                    owner,
                    item,
                    contentCatalog,
                    out var resultRarity,
                    out var paidGold,
                    out var failureReason))
            {
                return Fail($"Reforge nieudany: {failureReason}");
            }

            return Success(
                $"Reforge zakończony dla {GetCharacterName(owner)}. " +
                $"Rzadkość: {resultRarity}; zapłacono: {paidGold} złota.");
        }

        private static DevConsoleCommandResult InspectItem(
            IReadOnlyList<string> tokens,
            GameSession gameSession)
        {
            if (tokens.Count != 2) return Fail("Użycie: inspectitem <instanceId>");
            if (gameSession == null
                || string.IsNullOrWhiteSpace(gameSession.PlayerParty.ActiveLeaderId)
                || !gameSession.CharactersById.TryGetValue(gameSession.PlayerParty.ActiveLeaderId, out var leader))
                return Fail("Brak aktywnego lidera drużyny.");

            var item = leader.Inventory?.Items?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.InstanceId, tokens[1], StringComparison.OrdinalIgnoreCase));
            if (item == null) return Fail($"Nie znaleziono itemu o instanceId: {tokens[1]}");

            var output = new StringBuilder()
                .AppendLine($"InstanceId: {item.InstanceId}")
                .AppendLine($"DefinitionId: {item.Definition?.Id}")
                .AppendLine($"Name: {item.Definition?.DisplayName}")
                .AppendLine($"ItemType: {item.Definition?.ItemType}")
                .AppendLine($"Rarity: {item.CurrentRarity}")
                .AppendLine($"ItemLevel: {item.ItemLevel}");
            if (item is EquipmentItemInstance equipment)
            {
                output.AppendLine($"EquipmentSlot: {equipment.EquipmentSlot}")
                    .AppendLine($"RequiredClassId: {equipment.RequiredClassId}")
                    .AppendLine($"RequiredLevel: {equipment.RequiredLevel}")
                    .AppendLine($"Quality: {equipment.Quality}")
                    .AppendLine($"PartIds: {string.Join(", ", equipment.PartIds ?? new List<string>())}")
                    .AppendLine("StatModifiers:");
                foreach (var modifier in equipment.StatModifiers ?? new List<Godmist.Core.Types.StatModifier>())
                    output.AppendLine($"- {modifier.StatType} {modifier.Operation} {modifier.Stacking}: {modifier.Value}");
            }
            if (item is ConsumableItemInstance consumable)
            {
                output.AppendLine($"Charges: {consumable.CurrentCharges}/{consumable.MaxCharges}");
                output.AppendLine($"Catalyst: duration {consumable.CatalystDurationModifier}, strength x{consumable.CatalystEffectMultiplier:0.##}, charge bonus {consumable.CatalystChargeBonus}");
                if (consumable.IsBomb)
                {
                    output.AppendLine($"BombTargeting: range {consumable.BlastRange}, area {consumable.BlastAreaSize}x{consumable.BlastAreaSize}");
                }
                foreach (var effect in consumable.RuntimeEffects ?? new List<ConsumableEffectRuntime>())
                {
                    if (effect == null) continue;
                    output.AppendLine($"- Effect: {effect.EffectType}, value {effect.Value:0.###}, duration {effect.DurationTurns}, chance {effect.Chance:0.###}, status {effect.StatusId}, resistance {effect.ResistanceType}");
                }
            }
            return Success(output.ToString().TrimEnd());
        }

        private static DevConsoleCommandResult GetInventory(
            IReadOnlyList<string> tokens,
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (tokens.Count != 2)
            {
                return Fail("Użycie: items <characterId>");
            }

            if (!TryResolveCharacter(
                    tokens[1],
                    gameSession,
                    combatSession,
                    out var target))
            {
                return Fail($"Nie znaleziono postaci o ID: {tokens[1]}");
            }

            var items = target.Inventory?.Items;

            if (items == null || items.Count == 0)
            {
                return Success(
                    $"Inventory {GetCharacterName(target)} jest puste.");
            }

            var output = new StringBuilder(
                $"Inventory {GetCharacterName(target)}:");

            foreach (var group in items
                .Where(item => item?.Definition != null)
                .GroupBy(item => item.Definition.Id)
                .OrderBy(group => group.Key))
            {
                var displayName = group.First().Definition.DisplayName;

                output.Append(
                    $"\n- {group.Key} | {displayName} x{group.Count()}");
            }

            return Success(output.ToString());
        }

        private static DevConsoleCommandResult GetItemDefinitions(
            GameContentCatalog contentCatalog)
        {
            if (contentCatalog?.ItemDefinitions == null
                || contentCatalog.ItemDefinitions.Count == 0)
            {
                return Fail("GameContentCatalog nie zawiera ItemDefinition.");
            }

            var output = new StringBuilder("ItemDefinition:");

            foreach (var definition in contentCatalog.ItemDefinitions
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Id))
            {
                output.Append(
                    $"\n- {definition.Id} | {definition.DisplayName}");
            }

            return Success(output.ToString());
        }

        private static DevConsoleCommandResult GetEncounterDefinitions(
            GameContentCatalog contentCatalog)
        {
            if (contentCatalog?.EncounterDefinitions == null
                || contentCatalog.EncounterDefinitions.Count == 0)
            {
                return Fail(
                    "GameContentCatalog nie zawiera EncounterDefinition.");
            }

            var output = new StringBuilder("EncounterDefinition:");

            foreach (var definition in contentCatalog.EncounterDefinitions
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Id))
            {
                var enemyNames = definition.Enemies == null
                    ? string.Empty
                    : string.Join(
                        ", ",
                        definition.Enemies
                            .Where(enemy => enemy?.CharacterDefinition != null)
                            .Select(enemy => enemy.CharacterDefinition.DisplayName));

                var enemiesText = string.IsNullOrWhiteSpace(enemyNames)
                    ? "brak"
                    : enemyNames;

                output.Append(
                    $"\n- {definition.Id} | {definition.DisplayName} | " +
                    $"{definition.GridWidth}x{definition.GridHeight} | " +
                    $"wrogowie: {enemiesText}");
            }

            return Success(output.ToString());
        }
        
        private static DevConsoleCommandResult StartCombat(
            IReadOnlyList<string> tokens,
            CombatSession combatSession,
            Func<string, DevConsoleCommandResult> combatHandler)
        {
            if (tokens.Count != 2)
            {
                return Fail("Uzycie: combat <encounterId>");
            }

            if (combatSession != null
                && combatSession.Outcome == CombatOutcomeState.Ongoing)
            {
                return Fail(
                    "Nie mozna uruchomic nowej walki podczas aktywnej walki.");
            }

            if (combatHandler == null)
            {
                return Fail(
                    "Komenda combat nie jest dostepna w tym kontekscie.");
            }

            return combatHandler(tokens[1]);
        }

        private static bool TryReadOptionalCharacterTarget(
            IReadOnlyList<string> tokens,
            out string characterId,
            out int amount,
            out DevConsoleCommandResult failure)
        {
            characterId = string.Empty;
            amount = 0;
            failure = default;

            if (tokens.Count == 2)
            {
                return TryReadAmount(tokens, 1, out amount, out failure);
            }

            if (tokens.Count == 3)
            {
                characterId = tokens[1];

                return TryReadAmount(tokens, 2, out amount, out failure);
            }

            failure = Fail(
                "Użycie: <komenda> <liczba> albo " +
                "<komenda> <characterId> <liczba>");

            return false;
        }

        private static bool TryReadAmount(
            IReadOnlyList<string> tokens,
            int amountIndex,
            out int amount,
            out DevConsoleCommandResult failure)
        {
            amount = 0;
            failure = default;

            if (tokens.Count <= amountIndex
                || !int.TryParse(
                    tokens[amountIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out amount)
                || amount <= 0)
            {
                failure = Fail("Podaj dodatnią liczbę całkowitą.");
                return false;
            }

            return true;
        }

        private static bool TryReadFloat(
            string value,
            out float result)
        {
            return float.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out result)
                && !float.IsNaN(result)
                && !float.IsInfinity(result);
        }

        private static bool TryResolveBlacksmith(
            GameSession gameSession,
            string npcId,
            out NpcInstance blacksmith,
            out DevConsoleCommandResult failure)
        {
            blacksmith = null;
            failure = default;

            if (gameSession?.NpcsById == null)
            {
                failure = Fail("Sesja nie zawiera zarejestrowanych NPC-ów.");
                return false;
            }

            blacksmith = gameSession.NpcsById.Values.FirstOrDefault(npc =>
                npc != null
                && string.Equals(npc.NpcId, npcId, StringComparison.OrdinalIgnoreCase));

            if (blacksmith == null)
            {
                failure = Fail($"Nie znaleziono NPC o ID: {npcId}");
                return false;
            }

            if (blacksmith.Definition?.GetRole<BlacksmithRoleData>() == null)
            {
                failure = Fail($"NPC '{npcId}' nie oferuje usług kowalskich.");
                return false;
            }

            return true;
        }

        private static bool TryFindEquipment(
            CharacterInstance owner,
            string instanceId,
            out EquipmentItemInstance equipment)
        {
            equipment = owner?.Inventory?.Items?
                .OfType<EquipmentItemInstance>()
                .FirstOrDefault(item => string.Equals(
                    item.InstanceId,
                    instanceId,
                    StringComparison.OrdinalIgnoreCase));

            if (equipment != null)
            {
                return true;
            }

            equipment = owner?.Equipment?.GetEquippedItems()?
                .FirstOrDefault(item => string.Equals(
                    item?.InstanceId,
                    instanceId,
                    StringComparison.OrdinalIgnoreCase));

            return equipment != null;
        }

        private static bool TryResolveCharacter(
            string characterId,
            GameSession gameSession,
            CombatSession combatSession,
            out CharacterInstance character)
        {
            character = null;

            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            if (gameSession != null)
            {
                foreach (var candidate in gameSession.CharactersById.Values)
                {
                    if (candidate != null
                        && string.Equals(
                            candidate.CharacterId,
                            characterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        character = candidate;
                        return true;
                    }
                }
            }

            if (combatSession == null)
            {
                return false;
            }

            foreach (var unit in combatSession.Units)
            {
                var candidate = unit?.Character;

                if (candidate != null
                    && string.Equals(
                        candidate.CharacterId,
                        characterId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    character = candidate;
                    return true;
                }
            }

            return false;
        }

        private static List<CharacterInstance> GetActivePartyCharacters(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (combatSession != null)
            {
                return combatSession.Units
                    .Where(unit => unit != null
                        && unit.IsPlayerSide
                        && unit.Character != null)
                    .Select(unit => unit.Character)
                    .Distinct()
                    .ToList();
            }

            if (gameSession == null)
            {
                return new List<CharacterInstance>();
            }

            return gameSession.PlayerParty.ActiveMemberIds
                .Where(gameSession.CharactersById.ContainsKey)
                .Select(characterId =>
                    gameSession.CharactersById[characterId])
                .ToList();
        }

        private static List<CharacterInstance> GetKnownCharacters(
            GameSession gameSession,
            CombatSession combatSession)
        {
            var characters = new List<CharacterInstance>();

            if (gameSession != null)
            {
                characters.AddRange(
                    gameSession.CharactersById.Values
                        .Where(character => character != null));
            }

            if (combatSession != null)
            {
                characters.AddRange(
                    combatSession.Units
                        .Where(unit => unit?.Character != null)
                        .Select(unit => unit.Character));
            }

            return characters
                .Distinct()
                .OrderBy(character => character.CharacterId)
                .ToList();
        }

        private static string GetCharacterName(
            CharacterInstance character)
        {
            if (character == null)
            {
                return "brak";
            }

            if (!string.IsNullOrWhiteSpace(
                    character.Definition?.DisplayName))
            {
                return character.Definition.DisplayName;
            }

            return string.IsNullOrWhiteSpace(character.CharacterId)
                ? "bez nazwy"
                : character.CharacterId;
        }

        private static int GetMaxHealth(
            CharacterInstance character)
        {
            if (character?.Stats == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                (int)Math.Round(character.Stats.MaxHealth.GetValue()));
        }

        private static DevConsoleCommandResult Success(
            string message)
        {
            return new DevConsoleCommandResult(true, message);
        }

        private static DevConsoleCommandResult Fail(
            string message)
        {
            return new DevConsoleCommandResult(false, message);
        }
    }
}
