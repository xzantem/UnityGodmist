using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;

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

        public DevConsoleCommandResult Execute(
            string commandLine,
            GameSession gameSession,
            CombatSession combatSession,
            GameContentCatalog contentCatalog = null,
            Func<string, DevConsoleCommandResult> combatHandler = null)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return Fail("Wpisz komendę albo użyj: help");
            }

            var tokens = commandLine.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            var command = tokens[0].ToLowerInvariant();

            return command switch
            {
                "help" => GetHelp(),
                "state" => GetState(gameSession, combatSession),
                "characters" => GetCharacters(gameSession, combatSession),
                "gold" => AddGold(tokens, gameSession),
                "xp" => AddExperience(tokens, gameSession, combatSession),
                "heal" => Heal(tokens, gameSession, combatSession),
                "resource" => RestoreResource(tokens, gameSession, combatSession),
                "godmode" => ToggleGodMode(tokens, gameSession, combatSession),
                "sethp" => SetHealth(tokens, gameSession, combatSession),
                "setresource" => SetResource(tokens, gameSession, combatSession),
                "giveitem" => GiveItem(
                    tokens,
                    gameSession,
                    combatSession,
                    contentCatalog),
                "createitem" => CreateItem(tokens, gameSession, contentCatalog),
                "items" => GetInventory(tokens, gameSession, combatSession),
                "inspectitem" => InspectItem(tokens, gameSession),
                "itemdefs" => GetItemDefinitions(contentCatalog),
                "encounterdefs" => GetEncounterDefinitions(contentCatalog),
                "combat" => StartCombat(tokens, combatSession, combatHandler),
                "unit" => InspectUnit(tokens, gameSession, combatSession),
                _ => Fail($"Nieznana komenda: {tokens[0]}. Użyj: help")
            };
        }

        private static DevConsoleCommandResult GetHelp()
        {
            return Success(
                "Dostępne komendy:\n" +
                "help - lista komend\n" +
                "state - stan sesji i walki\n" +
                "characters - lista postaci oraz ich ID\n" +
                "unit [characterId] - szczegółowy stan runtime postaci\n" +
                "gold <liczba> - dodaje złoto\n" +
                "xp <liczba> - XP aktywnej drużyny\n" +
                "xp <characterId> <liczba> - XP wskazanej postaci\n" +
                "heal <liczba> - leczy aktywną jednostkę walki\n" +
                "heal <characterId> <liczba> - leczy wskazaną postać\n" +
                "resource <liczba> - odnawia zasób aktywnej jednostki\n" +
                "resource <characterId> <liczba> - odnawia zasób postaci\n" +
                "godmode <characterId> - przełącza nieśmiertelność\n" +
                "sethp <characterId> <liczba> - ustawia HP żywej postaci\n" +
                "setresource <characterId> <liczba> - ustawia zasób postaci\n" +
                "itemdefs - lista itemId z katalogu contentu\n" +
                "items <characterId> - zawartość inventory postaci\n" +
                "giveitem <characterId> <itemId> [ilość] - dodaje przedmiot\n" +
                "createitem <itemId> - dodaje item do inventory aktywnego lidera\n" +
                "encounterdefs - lista encounterId z katalogu contentu\n" +
                "inspectitem <instanceId> - pełny payload itemu aktywnego lidera\n" +
                "combat <encounterId> - uruchamia debugową walkę z encounterem");
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
            if (tokens.Count != 2) return Fail("Użycie: createitem <itemId>");
            if (gameSession == null || contentCatalog == null)
                return Fail("Brak GameSession albo GameContentCatalog.");
            if (string.IsNullOrWhiteSpace(gameSession.PlayerParty.ActiveLeaderId)
                || !gameSession.CharactersById.TryGetValue(
                    gameSession.PlayerParty.ActiveLeaderId, out var leader))
                return Fail("Brak aktywnego lidera drużyny.");
            if (!contentCatalog.TryGetItem(tokens[1], out var definition))
                return Fail($"Nie znaleziono itemu o ID: {tokens[1]}");

            ItemInstance item;

            if (definition is EquipmentItemDefinition equipmentDefinition)
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

            if (!inventoryService.TryAddItem(leader.Inventory, item))
                return Fail($"Nie udało się dodać '{definition.DisplayName}' do inventory lidera.");

            return Success($"Utworzono {definition.DisplayName} | instanceId: {item.InstanceId}");
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
