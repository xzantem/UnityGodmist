using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Items;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public class DungeonObjectPlacementSystem
    {
        private const int ObjectPlacementSeedSalt =
            unchecked((int)0x5F3759DF);

        public void Place(
            DungeonInstance dungeon,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null)
        {
            PlaceInternal(dungeon, equipmentParts, false);
        }

        // Only pre-snapshot saves may execute the historical selection/RNG sequence.
        public void PlaceLegacySave(DungeonInstance dungeon,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts = null)
        {
            PlaceInternal(dungeon, equipmentParts, true);
        }

        private void PlaceInternal(DungeonInstance dungeon,
            IReadOnlyList<EquipmentPartDefinition> equipmentParts, bool legacy)
        {
            if (dungeon == null || dungeon.Definition == null) return;
            if (!legacy && dungeon.HasPlacedContent) return;
            if (!legacy)
            {
                var catalog = dungeon.Definition.RoomTemplateCatalog;
                var usedIds = dungeon.Floors.Where(f => f != null).SelectMany(f => f.Rooms)
                    .Select(r => r.TemplateId).Distinct(StringComparer.Ordinal).ToList();
                var templates = usedIds.Select(id => catalog?.AllTemplates.FirstOrDefault(t => t != null && t.Id == id)
                    ?? throw new InvalidOperationException($"Brak szablonu '{id}'.")).ToList();
                foreach (var template in templates) DungeonRoomContentPlanner.ValidateTemplate(template);
                DungeonDistributionValidator.Validate(templates);
            }

            LootTableData themedChestLootTable = null;
            var hasThemedChestLootTable =
                dungeon.Definition.LootTableCatalog != null
                && dungeon.Definition.LootTableCatalog.TryGetChestLootTable(
                    dungeon.Definition.Theme,
                    dungeon.DungeonLevel,
                    out themedChestLootTable);

            var objectDefinitions = GetValidObjectDefinitions(
                dungeon.Definition.ObjectDefinitions,
                hasThemedChestLootTable ? themedChestLootTable : null);

            var doorDefinitions = GetValidDoorDefinitions(
                dungeon.Definition.ObjectDefinitions);

            var random = new System.Random(
                dungeon.Seed ^ ObjectPlacementSeedSalt);

            var lootTableSystem = new LootTableSystem(
                random,
                equipmentParts,
                lootLevel: dungeon.DungeonLevel, dropChanceBonus: dungeon.ItemDropChanceBonus);

            var distribution = legacy ? null : new DungeonContentDistribution(dungeon.Seed);

            foreach (var floor in dungeon.Floors)
            {
                if (floor == null
                    || floor.Map == null
                    || floor.Objects.Count > 0)
                {
                    continue;
                }

                if (doorDefinitions.Count > 0)
                {
                    PlaceDoorsOnFloor(
                        dungeon,
                        floor,
                        doorDefinitions,
                        random);
                }

                if (!legacy)
                {
                    PlaceCompleteObjects(dungeon, floor, objectDefinitions, lootTableSystem,
                        random, hasThemedChestLootTable ? themedChestLootTable : null, distribution);
                }
                else if (objectDefinitions.Count > 0)
                {
                    PlaceObjectsOnFloor(
                        dungeon,
                        floor,
                        objectDefinitions,
                        lootTableSystem,
                        random,
                        hasThemedChestLootTable
                            ? themedChestLootTable
                            : null);
                }

                if (!BindProgressionObjects(floor))
                {
                    throw new InvalidOperationException(
                        $"Nie udalo sie przypisac wszystkich obiektow progresji "
                        + $"na pietrze {floor.FloorIndex}.");
                }
            }
            dungeon.MarkContentPlaced();
        }

        private static bool BindProgressionObjects(DungeonFloorInstance floor)
        {
            if (floor == null) return false;

            foreach (var connection in floor.Connections)
            {
                if (connection == null
                    || !connection.HasProgressionGate
                    || string.IsNullOrWhiteSpace(connection.RequiredBranchRoomId))
                {
                    continue;
                }

                var sourceIds = new List<string>();

                foreach (var dungeonObject in floor.Objects)
                {
                    var isProgressionSource =
                        dungeonObject is LeverDungeonObjectInstance
                        || (dungeonObject is ConditionDungeonObjectInstance
                            && dungeonObject.IsProgressionRequirement);

                    if (!isProgressionSource
                        || !floor.Map.TryGetRoomId(
                            dungeonObject.Position,
                            out var roomId)
                        || !string.Equals(
                            roomId,
                            connection.RequiredBranchRoomId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var bound = dungeonObject switch
                    {
                        LeverDungeonObjectInstance lever =>
                            lever.TryBindCondition(
                                connection.ProgressionConditionId),
                        ConditionDungeonObjectInstance condition =>
                            condition.TryBindCondition(
                                connection.ProgressionConditionId),
                        _ => false
                    };

                    if (!bound)
                    {
                        return false;
                    }

                    sourceIds.Add(dungeonObject.InstanceId);
                }

                if (!connection.TrySetProgressionRequirementSources(sourceIds))
                {
                    return false;
                }

                foreach (var dungeonObject in floor.Objects)
                {
                    var isSatisfied = dungeonObject switch
                    {
                        LeverDungeonObjectInstance lever => lever.IsActivated,
                        ConditionDungeonObjectInstance condition =>
                            condition.IsSatisfied,
                        _ => false
                    };

                    if (!isSatisfied
                        || !sourceIds.Contains(dungeonObject.InstanceId))
                    {
                        continue;
                    }

                    floor.TrySatisfyProgressionCondition(
                        connection.ProgressionConditionId,
                        dungeonObject.InstanceId);
                }
            }

            return true;
        }

        private static void PlaceDoorsOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DoorDungeonObjectDefinition> definitions,
            System.Random random)
        {
            if (definitions.Count == 0) return;

            var candidates = GetDoorAnchorCandidates(
                floor,
                dungeon.Definition.RoomTemplateCatalog);

            candidates.Sort(CompareDoorCandidates);
            Shuffle(candidates, random);

            for (var index = 0; index < candidates.Count; index++)
            {
                var definition = SelectDefinition(definitions, random);

                if (definition == null) continue;

                var candidate = candidates[index];

                var instanceId =
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}.door_{index:00}";

                var door = new DoorDungeonObjectInstance(
                    instanceId,
                    definition,
                    candidate.Edge);

                if (!floor.TryAddObject(door))
                {
                    throw new InvalidOperationException(
                        $"Nie udalo sie dodac drzwi '{instanceId}' do pietra "
                        + $"{floor.FloorIndex}.");
                }
            }
        }

        private static List<DoorAnchorCandidate> GetDoorAnchorCandidates(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog)
        {
            var result = new List<DoorAnchorCandidate>();

            if (catalog == null) return result;

            var templatesById = BuildTemplateLookup(catalog);
            var registeredEdges = new HashSet<DungeonCellEdge>();

            foreach (var room in floor.Rooms)
            {
                if (!templatesById.TryGetValue(
                        room.TemplateId,
                        out var template))
                {
                    continue;
                }

                foreach (var anchor in template.StructuralAnchors)
                {
                    if (anchor == null
                        || anchor.Type != DungeonStructuralAnchorType.Door)
                    {
                        continue;
                    }

                    var firstCell = room.LayoutPosition
                                    + DungeonRoomTransformUtility.TransformCell(
                                        anchor.LocalCell,
                                        template.SizeInCells,
                                        room.RotationDegrees);
                    var secondCell = firstCell
                                     + DungeonRoomTransformUtility.GetDirectionOffset(
                                         DungeonRoomTransformUtility.TransformDirection(
                                             anchor.Direction,
                                             room.RotationDegrees));

                    if (!floor.Map.IsWalkable(firstCell)
                        || !floor.Map.IsWalkable(secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "wskazuje pole poza pokojem.");
                    }

                    if (!floor.Map.TryGetRoomId(firstCell, out var firstRoomId)
                        || !floor.Map.TryGetRoomId(secondCell, out var secondRoomId)
                        || firstRoomId != room.RoomId
                        || secondRoomId != room.RoomId)
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "nie moze wychodzic poza granice pokoju.");
                    }

                    var edge = new DungeonCellEdge(firstCell, secondCell);

                    if (floor.Map.HasWallBetween(firstCell, secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "naklada sie na statyczna sciane.");
                    }

                    if (!floor.Map.CanMoveBetween(firstCell, secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica drzwi w szablonie '{template.Id}' "
                            + "nie wskazuje dostepnej krawedzi.");
                    }

                    if (!registeredEdges.Add(edge))
                    {
                        throw new InvalidOperationException(
                            $"Powtorzona kotwica drzwi w szablonie "
                            + $"'{template.Id}'.");
                    }

                    result.Add(new DoorAnchorCandidate(edge));
                }
            }

            return result;
        }

        public static void ValidateContentDefinitions(DungeonDefinition definition)
        {
            if (definition == null || definition.RoomTemplateCatalog == null)
                throw new InvalidOperationException("Brak definicji lochu lub katalogu szablonow.");
            LootTableData themedLoot = null;
            definition.LootTableCatalog?.TryGetChestLootTable(definition.Theme, definition.DungeonLevel, out themedLoot);
            var valid = GetValidObjectDefinitions(definition.ObjectDefinitions, themedLoot);
            foreach (var template in definition.RoomTemplateCatalog.GenerationTemplates)
            {
                if (template == null) throw new InvalidOperationException($"Loch '{definition.Id}': pusta referencja szablonu.");
                DungeonRoomContentPlanner.ValidateTemplate(template);
                foreach (var anchor in template.SemanticAnchors ?? new())
                {
                    if (anchor.Type == DungeonSemanticAnchorType.Enemy) continue;
                    var matching = GetDefinitionsForAnchorType(valid, anchor.Type);
                    if (matching.Count == 0 || anchor.ObjectDefinition != null && !matching.Contains(anchor.ObjectDefinition))
                        throw new InvalidOperationException($"Loch '{definition.Id}', szablon '{template.Id}', kotwa '{anchor.Id}': "
                            + "brak poprawnej definicji w puli (typ, waga, loot lub klucz).");
                }
            }
            DungeonDistributionValidator.Validate(definition.RoomTemplateCatalog.GenerationTemplates);
        }

        private static void PlaceCompleteObjects(DungeonInstance dungeon, DungeonFloorInstance floor,
            List<DungeonObjectDefinition> definitions, LootTableSystem lootTableSystem,
            System.Random random, LootTableData themedChestLootTable, DungeonContentDistribution distribution)
        {
            var catalog = dungeon.Definition.RoomTemplateCatalog;
            if (catalog == null)
                throw new InvalidOperationException($"Pietro {floor.FloorIndex}: brak katalogu szablonow.");
            foreach (var room in floor.Rooms)
            {
                var template = catalog.AllTemplates.FirstOrDefault(t => t != null && t.Id == room.TemplateId);
                if (template == null)
                    throw new InvalidOperationException($"Pietro {floor.FloorIndex}, pokoj '{room.RoomId}': brak szablonu '{room.TemplateId}'.");
                DungeonRoomContentPlanner.ValidateTemplate(template);
            }
            var candidates = new DungeonSemanticAnchorResolver().GetCandidates(floor, catalog,
                new[] { DungeonSemanticAnchorType.Chest, DungeonSemanticAnchorType.Obstacle,
                    DungeonSemanticAnchorType.Trap, DungeonSemanticAnchorType.Lever,
                    DungeonSemanticAnchorType.Condition, DungeonSemanticAnchorType.Reward },
                _ => true, normalizeLegacyData: false);
            var choices = new Dictionary<DungeonSemanticAnchorPlacement, List<DungeonObjectDefinition>>();
            foreach (var candidate in candidates)
            {
                var matching = GetDefinitionsForAnchorType(definitions, candidate.Anchor.Type);
                if (candidate.Anchor.ObjectDefinition != null)
                    matching = matching.FindAll(d => d == candidate.Anchor.ObjectDefinition);
                if (matching.Count == 0)
                    throw PlacementError(floor, candidate, "Brak poprawnej definicji w puli lochu (typ, waga, loot lub klucz).");
                choices.Add(candidate, matching);
            }
            var opportunities = DungeonRoomContentPlanner.Build(catalog, candidates);
            var reserved = new DungeonEnemyAnchorResolver().GetReservedCells(floor, catalog, normalizeLegacyData: false);
            foreach (var candidate in candidates)
                foreach (var cell in candidate.Cells)
                    if (IsOccupiedByDungeonObject(floor, cell)) reserved.Add(cell);

            var selected = opportunities.Where(o => o.IsRequired).ToList();
            var selectedAnchors = selected.SelectMany(o => o.Anchors).ToList();
            if (!TryAssignPlacementPlan(
                    selectedAnchors,
                    choices,
                    floor,
                    reserved,
                    random: null,
                    out _))
                throw new InvalidOperationException($"Pietro {floor.FloorIndex}: brak miejsca na komplet wymaganego wyposazenia: "
                    + string.Join(", ", selected.Select(o => o.Id)) + ". Kotwy: "
                    + string.Join(", ", selectedAnchors.Select(a => a.Room.TemplateId + "/" + a.Anchor.Id)));

            foreach (var opportunity in opportunities)
            {
                if (opportunity.IsRequired || opportunity.Dependencies.Any(d => !selected.Contains(d))) continue;
                var trial = new List<DungeonSemanticAnchorPlacement>(selectedAnchors);
                trial.AddRange(opportunity.Anchors);
                if (!TryAssignPlacementPlan(
                        trial,
                        choices,
                        floor,
                        reserved,
                        random: null,
                        out _)) continue;
                // One draw only after dependencies and the complete group's space qualify.
                if (!distribution.Roll(opportunity.DistributionKey, opportunity.SpawnChance)) continue;
                selected.Add(opportunity);
                selectedAnchors = trial;
            }

            if (!TryAssignPlacementPlan(
                    selectedAnchors,
                    choices,
                    floor,
                    reserved,
                    random,
                    out var placementPlan))
            {
                throw new InvalidOperationException($"Pietro {floor.FloorIndex}: brak miejsca na komplet wybranego wyposazenia "
                    + "z uwzglednieniem zajetych komorek obiektow: "
                    + string.Join(", ", selected.Select(o => o.Id)));
            }

            var instances = new List<DungeonObjectInstance>();
            var pendingPlacementCells = new HashSet<Vector2Int>();
            for (var index = 0; index < selectedAnchors.Count; index++)
            {
                var anchor = selectedAnchors[index];
                var planned = placementPlan[anchor];
                var instance = CreateObjectInstance(
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}.object_{index:00}", planned.Definition,
                    planned.Position, lootTableSystem, themedChestLootTable, anchor.Anchor.ActivationGroupId,
                    anchor.Room.IsRequiredProgressionRoom && (anchor.Anchor.Type == DungeonSemanticAnchorType.Lever
                        || anchor.Anchor.Type == DungeonSemanticAnchorType.Condition), dungeon, floor, index,
                    dungeon.Floors.Sum(f => f.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional)));
                if (instance == null) throw PlacementError(floor, anchor, "Nieobslugiwany typ definicji.");
                if (!CanPlaceDungeonObject(floor, instance, pendingPlacementCells))
                {
                    throw PlacementError(floor, anchor,
                        "Wybrane obiekty nachodza na zajety footprint (w tym obiekt wielokomorkowy).");
                }

                foreach (var cell in EnumeratePlacementCells(instance))
                    pendingPlacementCells.Add(cell);
                instances.Add(instance);
            }
            foreach (var instance in instances)
                if (!floor.TryAddObject(instance))
                    throw new InvalidOperationException($"Pietro {floor.FloorIndex}: nie mozna dodac zaplanowanego obiektu '{instance.InstanceId}'.");
        }

        private static bool TryAssignPlacementPlan(
            IReadOnlyList<DungeonSemanticAnchorPlacement> anchors,
            IReadOnlyDictionary<DungeonSemanticAnchorPlacement, List<DungeonObjectDefinition>> choices,
            DungeonFloorInstance floor,
            ISet<Vector2Int> reserved,
            System.Random random,
            out Dictionary<DungeonSemanticAnchorPlacement, PlannedPlacement> placements)
        {
            placements = new Dictionary<DungeonSemanticAnchorPlacement, PlannedPlacement>();
            if (anchors == null || anchors.Count == 0) return true;

            var blocked = new HashSet<Vector2Int>(reserved ?? new HashSet<Vector2Int>());
            AddExistingObjectFootprints(floor, blocked);

            var optionsByAnchor = new Dictionary<
                DungeonSemanticAnchorPlacement,
                List<PlacementOption>>();

            foreach (var anchor in anchors)
            {
                if (!choices.TryGetValue(anchor, out var definitions)
                    || definitions == null
                    || definitions.Count == 0)
                {
                    return false;
                }

                var options = BuildPlacementOptions(anchor, definitions, floor, blocked);
                if (options.Count == 0) return false;
                optionsByAnchor.Add(anchor, options);
            }

            var orderedAnchors = anchors
                .OrderBy(anchor => optionsByAnchor[anchor].Count)
                .ThenBy(anchor => anchor.Room.RoomId, StringComparer.Ordinal)
                .ThenBy(anchor => anchor.Anchor.Id, StringComparer.Ordinal)
                .ToList();

            var occupied = new HashSet<Vector2Int>(blocked);
            var assigned = new Dictionary<DungeonSemanticAnchorPlacement, PlannedPlacement>();
            bool Assign(int anchorIndex)
            {
                if (anchorIndex >= orderedAnchors.Count) return true;

                var anchor = orderedAnchors[anchorIndex];
                var options = OrderPlacementOptions(optionsByAnchor[anchor], random);
                foreach (var option in options)
                {
                    if (option.Cells.Any(occupied.Contains)) continue;

                    foreach (var cell in option.Cells) occupied.Add(cell);
                    assigned[anchor] = new PlannedPlacement(option.Definition, option.Position);

                    if (Assign(anchorIndex + 1)) return true;

                    assigned.Remove(anchor);
                    foreach (var cell in option.Cells) occupied.Remove(cell);
                }

                return false;
            }

            if (Assign(0))
            {
                placements = assigned;
                return true;
            }

            return false;
        }

        private static List<PlacementOption> BuildPlacementOptions(
            DungeonSemanticAnchorPlacement anchor,
            IReadOnlyList<DungeonObjectDefinition> definitions,
            DungeonFloorInstance floor,
            ISet<Vector2Int> blocked)
        {
            var positions = anchor.Cells
                .Distinct()
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .ToList();
            var orderedDefinitions = definitions
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToList();
            var result = new List<PlacementOption>();

            foreach (var position in positions)
            {
                foreach (var definition in orderedDefinitions)
                {
                    var cells = EnumeratePlacementCells(definition, position).ToArray();
                    if (cells.Any(blocked.Contains)
                        || !IsValidPlacementFootprint(floor, anchor, cells)) continue;
                    result.Add(new PlacementOption(definition, position, cells));
                }
            }

            return result;
        }

        private static bool IsValidPlacementFootprint(
            DungeonFloorInstance floor,
            DungeonSemanticAnchorPlacement anchor,
            IReadOnlyList<Vector2Int> cells)
        {
            if (floor?.Map == null || anchor?.Room == null || cells == null) return false;
            return cells.All(cell =>
                floor.Map.IsWalkable(cell)
                && floor.Map.TryGetRoomId(cell, out var roomId)
                && string.Equals(roomId, anchor.Room.RoomId, StringComparison.Ordinal))
                // A 2x2 reservation includes a diagonal cell: validate its
                // internal cardinal edges, rather than requiring a diagonal move.
                && cells.All(cell => (!cells.Contains(cell + Vector2Int.right) || floor.Map.CanMoveBetween(cell, cell + Vector2Int.right))
                    && (!cells.Contains(cell + Vector2Int.up) || floor.Map.CanMoveBetween(cell, cell + Vector2Int.up)));
        }

        private static List<PlacementOption> OrderPlacementOptions(
            IReadOnlyList<PlacementOption> options,
            System.Random random)
        {
            var result = options
                .OrderBy(option => option.Position.y)
                .ThenBy(option => option.Position.x)
                .ThenBy(option => option.Definition.Id, StringComparer.Ordinal)
                .ToList();
            if (random == null) return result;

            // Keep weighted definition selection while allowing the planner to
            // try the next definition/position when occupancy conflicts.
            var positions = result
                .Select(option => option.Position)
                .Distinct()
                .ToList();
            Shuffle(positions, random);

            var weightedResult = new List<PlacementOption>(result.Count);
            foreach (var position in positions)
            {
                var remaining = result
                    .Where(option => option.Position == position)
                    .ToList();
                while (remaining.Count > 0)
                {
                    var definitions = remaining
                        .Select(option => option.Definition)
                        .ToList();
                    var definition = SelectDefinition(definitions, random);
                    var optionIndex = remaining.FindIndex(option => option.Definition == definition);
                    weightedResult.Add(remaining[optionIndex]);
                    remaining.RemoveAt(optionIndex);
                }
            }

            return weightedResult;
        }

        private static void AddExistingObjectFootprints(
            DungeonFloorInstance floor,
            ISet<Vector2Int> blocked)
        {
            if (floor?.Objects == null) return;

            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject == null
                    || dungeonObject is ObstacleDungeonObjectInstance obstacle
                        && !obstacle.IsActive)
                {
                    continue;
                }

                foreach (var cell in EnumeratePlacementCells(dungeonObject))
                    blocked.Add(cell);
            }
        }

        private static IEnumerable<Vector2Int> EnumeratePlacementCells(
            DungeonObjectDefinition definition,
            Vector2Int position)
        {
            if (definition is ObstacleDungeonObjectDefinition obstacle)
            {
                foreach (var cell in obstacle.EnumeratePlacementReservationCells(position)) yield return cell;
                yield break;
            }
            yield return position;
        }

        private sealed class PlacementOption
        {
            public DungeonObjectDefinition Definition { get; }
            public Vector2Int Position { get; }
            public IReadOnlyList<Vector2Int> Cells { get; }

            public PlacementOption(
                DungeonObjectDefinition definition,
                Vector2Int position,
                IReadOnlyList<Vector2Int> cells)
            {
                Definition = definition;
                Position = position;
                Cells = cells;
            }
        }

        private readonly struct PlannedPlacement
        {
            public DungeonObjectDefinition Definition { get; }
            public Vector2Int Position { get; }

            public PlannedPlacement(
                DungeonObjectDefinition definition,
                Vector2Int position)
            {
                Definition = definition;
                Position = position;
            }
        }

        private static InvalidOperationException PlacementError(DungeonFloorInstance floor,
            DungeonSemanticAnchorPlacement candidate, string message)
            => new($"Pietro {floor.FloorIndex}, pokoj '{candidate.Room.RoomId}', szablon '{candidate.Room.TemplateId}', "
                + $"kotwa '{candidate.Anchor.Id}': {message}");

        private static void PlaceObjectsOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DungeonObjectDefinition> definitions,
            LootTableSystem lootTableSystem,
            System.Random random,
            LootTableData themedChestLootTable)
        {
            if (definitions.Count == 0) return;

            var semanticResolver = new DungeonSemanticAnchorResolver();
            var semanticCandidates = semanticResolver.GetCandidates(
                floor,
                dungeon.Definition.RoomTemplateCatalog,
                new[]
                {
                    DungeonSemanticAnchorType.Chest,
                    DungeonSemanticAnchorType.Obstacle,
                    DungeonSemanticAnchorType.Trap,
                    DungeonSemanticAnchorType.Lever,
                    DungeonSemanticAnchorType.Condition,
                    DungeonSemanticAnchorType.Reward
                },
                roomType => IsEligibleRoomType(roomType)
                    || roomType == DungeonRoomType.Corridor);

            semanticCandidates = semanticCandidates.FindAll(
                IsEligibleSemanticCandidate);

            foreach (var candidate in semanticCandidates)
            {
                if (candidate.Anchor.ObjectDefinition != null
                    && !GetDefinitionsForAnchorType(definitions, candidate.Anchor.Type).Contains(candidate.Anchor.ObjectDefinition))
                    throw new InvalidOperationException($"Kotwa '{candidate.Anchor.Id}' wskazuje obiekt spoza zgodnej puli lochu.");
            }

            semanticCandidates = semanticCandidates.FindAll(candidate =>
                GetDefinitionsForAnchorType(
                    definitions,
                    candidate.Anchor.Type).Count > 0);

            var reservedEnemyCells = new DungeonEnemyAnchorResolver()
                .GetReservedCells(
                    floor,
                    dungeon.Definition.RoomTemplateCatalog);

            semanticCandidates = semanticCandidates.FindAll(candidate =>
                candidate.Cells.All(cell => !reservedEnemyCells.Contains(cell)));

            if (semanticCandidates.Count == 0)
            {
                if (dungeon.Definition.MinObjectCountPerFloor > 0)
                {
                    throw new InvalidOperationException(
                        $"Pietro {floor.FloorIndex} nie ma uzytecznych kotew "
                        + "semantycznych dla skonfigurowanych obiektow.");
                }

                return;
            }

            PlaceAnchoredObjects(
                dungeon,
                floor,
                definitions,
                lootTableSystem,
                random,
                semanticResolver,
                semanticCandidates,
                themedChestLootTable);
        }

        private static void PlaceAnchoredObjects(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            List<DungeonObjectDefinition> definitions,
            LootTableSystem lootTableSystem,
            System.Random random,
            DungeonSemanticAnchorResolver semanticResolver,
            List<DungeonSemanticAnchorPlacement> candidates,
            LootTableData themedChestLootTable)
        {
            var maximumCount = Mathf.Min(
                dungeon.Definition.MaxObjectCountPerFloor,
                candidates.Count);

            if (maximumCount <= 0) return;

            var minimumCount = Mathf.Min(
                dungeon.Definition.MinObjectCountPerFloor,
                maximumCount);

            var selectedAnchors = semanticResolver.Select(
                candidates,
                minimumCount,
                maximumCount,
                random);

            for (var index = 0; index < selectedAnchors.Count; index++)
            {
                var selectedAnchor = selectedAnchors[index];
                var matchingDefinitions = GetDefinitionsForAnchorType(
                    definitions,
                    selectedAnchor.Anchor.Type);

                if (selectedAnchor.Anchor.ObjectDefinition != null)
                {
                    if (!matchingDefinitions.Contains(selectedAnchor.Anchor.ObjectDefinition))
                        throw new InvalidOperationException($"Kotwa '{selectedAnchor.Anchor.Id}' wskazuje obiekt spoza zgodnej puli lochu.");
                    matchingDefinitions = new List<DungeonObjectDefinition> { selectedAnchor.Anchor.ObjectDefinition };
                }
                if (matchingDefinitions.Count == 0) continue;

                var position = GetAvailableAnchorPosition(
                    floor,
                    selectedAnchor.Cells,
                    random);

                if (!position.HasValue) continue;

                var definition = SelectDefinition(matchingDefinitions, random);

                if (definition == null) continue;

                var instanceId =
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}."
                    + $"object_{index:00}";

                var dungeonObject = CreateObjectInstance(
                    instanceId,
                    definition,
                    position.Value,
                    lootTableSystem,
                    themedChestLootTable,
                    selectedAnchor.Anchor.ActivationGroupId,
                    selectedAnchor.Room.IsRequiredProgressionRoom
                    && (selectedAnchor.Anchor.Type
                        == DungeonSemanticAnchorType.Lever
                        || selectedAnchor.Anchor.Type
                           == DungeonSemanticAnchorType.Condition),
                    dungeon, floor, index);

                if (dungeonObject != null)
                {
                    if (!CanPlaceDungeonObject(floor, dungeonObject))
                        continue;

                    floor.TryAddObject(dungeonObject);
                }
            }
        }

        private static List<DungeonObjectDefinition> GetDefinitionsForAnchorType(
            List<DungeonObjectDefinition> definitions,
            DungeonSemanticAnchorType anchorType)
        {
            var result = new List<DungeonObjectDefinition>();

            foreach (var definition in definitions)
            {
                if ((anchorType == DungeonSemanticAnchorType.Chest
                     && definition is ChestDungeonObjectDefinition)
                    || (anchorType == DungeonSemanticAnchorType.Obstacle
                        && definition is ObstacleDungeonObjectDefinition)
                    || (anchorType == DungeonSemanticAnchorType.Trap
                        && definition is SpikeTrapDungeonObjectDefinition)
                    || (anchorType == DungeonSemanticAnchorType.Lever
                        && definition is LeverDungeonObjectDefinition)
                    || ((anchorType == DungeonSemanticAnchorType.Condition
                         || anchorType == DungeonSemanticAnchorType.Reward)
                        && definition is ConditionDungeonObjectDefinition))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        private static Vector2Int? GetAvailableAnchorPosition(
            DungeonFloorInstance floor,
            IReadOnlyList<Vector2Int> cells,
            System.Random random)
        {
            var candidates = new List<Vector2Int>(cells);
            candidates.Sort(ComparePositions);
            Shuffle(candidates, random);

            foreach (var candidate in candidates)
            {
                if (!IsOccupiedByDungeonObject(floor, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static List<DungeonObjectDefinition> GetValidObjectDefinitions(
            List<DungeonObjectDefinition> objectDefinitions,
            LootTableData themedChestLootTable)
        {
            var result = new List<DungeonObjectDefinition>();

            if (objectDefinitions == null) return result;

            foreach (var definition in objectDefinitions)
            {
                if (definition == null
                    || definition is DoorDungeonObjectDefinition
                    || string.IsNullOrWhiteSpace(definition.Id)
                    || definition.SpawnWeight <= 0)
                {
                    continue;
                }

                if (definition is ChestDungeonObjectDefinition chest
                    && chest.LootTable == null
                    && themedChestLootTable == null)
                {
                    continue;
                }

                if (definition is ChestDungeonObjectDefinition lockedChest
                    && lockedChest.LockType != ChestLockType.Unlocked
                    && (lockedChest.RequiredKey == null
                        || lockedChest.RequiredKey.ItemType != ItemType.Key))
                {
                    continue;
                }

                result.Add(definition);
            }

            return result;
        }

        private static List<DoorDungeonObjectDefinition> GetValidDoorDefinitions(
            List<DungeonObjectDefinition> objectDefinitions)
        {
            var result = new List<DoorDungeonObjectDefinition>();

            if (objectDefinitions == null) return result;

            foreach (var definition in objectDefinitions)
            {
                if (definition is not DoorDungeonObjectDefinition door
                    || string.IsNullOrWhiteSpace(door.Id)
                    || door.SpawnWeight <= 0)
                {
                    continue;
                }

                result.Add(door);
            }

            return result;
        }

        private static Dictionary<string, DungeonRoomTemplateDefinition>
            BuildTemplateLookup(DungeonRoomTemplateCatalog catalog)
        {
            var result =
                new Dictionary<string, DungeonRoomTemplateDefinition>();

            foreach (var template in catalog.AllTemplates)
            {
                if (template == null
                    || string.IsNullOrWhiteSpace(template.Id))
                {
                    continue;
                }

                if (!result.TryAdd(template.Id, template))
                {
                    throw new InvalidOperationException(
                        $"Powtorzony RoomTemplateId '{template.Id}'.");
                }
            }

            return result;
        }

        private static bool IsEligibleRoomType(DungeonRoomType roomType)
        {
            return roomType == DungeonRoomType.Normal
                || roomType == DungeonRoomType.Optional;
        }

        private static bool IsEligibleSemanticCandidate(
            DungeonSemanticAnchorPlacement candidate)
        {
            if (candidate?.Room == null) return false;

            return IsEligibleRoomType(candidate.Room.RoomType)
                || candidate.Anchor.Type == DungeonSemanticAnchorType.Trap
                   && candidate.Room.RoomType == DungeonRoomType.Corridor;
        }

        private static bool IsOccupiedByDungeonObject(
            DungeonFloorInstance floor,
            Vector2Int cell)
        {
            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject is DoorDungeonObjectInstance door)
                {
                    if (door.Edge.FirstCell == cell
                        || door.Edge.SecondCell == cell)
                    {
                        return true;
                    }

                    continue;
                }

                if ((dungeonObject is ObstacleDungeonObjectInstance obstacle
                        && obstacle.IsActive && new RectInt(obstacle.Position, ((ObstacleDungeonObjectDefinition)obstacle.Definition).PlacementReservationSizeInCells).Contains(cell))
                    || (dungeonObject != null
                        && dungeonObject is not ObstacleDungeonObjectInstance
                        && dungeonObject.Position == cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanPlaceDungeonObject(
            DungeonFloorInstance floor,
            DungeonObjectInstance dungeonObject,
            ISet<Vector2Int> pendingPlacementCells = null)
        {
            if (floor == null || dungeonObject == null)
                return false;

            foreach (var cell in EnumeratePlacementCells(dungeonObject))
            {
                if (floor.Map == null || !floor.Map.IsWalkable(cell)) return false;
                if (pendingPlacementCells != null && pendingPlacementCells.Contains(cell))
                    return false;
                if (IsOccupiedByDungeonObject(floor, cell))
                    return false;
            }

            return true;
        }

        private static IEnumerable<Vector2Int> EnumeratePlacementCells(
            DungeonObjectInstance dungeonObject)
        {
            if (dungeonObject is ObstacleDungeonObjectInstance obstacle)
            {
                foreach (var cell in ((ObstacleDungeonObjectDefinition)obstacle.Definition).EnumeratePlacementReservationCells(obstacle.Position))
                    yield return cell;
                yield break;
            }

            if (dungeonObject is DoorDungeonObjectInstance door)
            {
                yield return door.Edge.FirstCell;
                yield return door.Edge.SecondCell;
                yield break;
            }

            yield return dungeonObject.Position;
        }

        private static T SelectDefinition<T>(
            List<T> definitions,
            System.Random random)
            where T : DungeonObjectDefinition
        {
            var totalWeight = 0;

            foreach (var definition in definitions)
            {
                totalWeight += definition.SpawnWeight;
            }

            if (totalWeight <= 0) return null;

            var selectedWeight = random.Next(totalWeight);

            foreach (var definition in definitions)
            {
                selectedWeight -= definition.SpawnWeight;

                if (selectedWeight < 0)
                {
                    return definition;
                }
            }

            return definitions[^1];
        }

        private static DungeonObjectInstance CreateObjectInstance(
            string instanceId,
            DungeonObjectDefinition definition,
            Vector2Int position,
            LootTableSystem lootTableSystem,
            LootTableData themedChestLootTable,
            string activationGroupId,
            bool isProgressionRequirement,
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            int objectIndex,
            int equipmentChestCount = 0)
        {
            if (definition is ChestDungeonObjectDefinition chest)
            {
                var items = lootTableSystem.RollChestLoot(
                    themedChestLootTable ?? chest.LootTable, equipmentChestCount);

                return new ChestDungeonObjectInstance(
                    instanceId,
                    chest,
                    position,
                    items,
                    RollChestGold(dungeon, floor, objectIndex));
            }

            if (definition is ObstacleDungeonObjectDefinition obstacle)
            {
                return new ObstacleDungeonObjectInstance(
                    instanceId,
                    obstacle,
                    position);
            }

            if (definition is SpikeTrapDungeonObjectDefinition spikeTrap)
            {
                return new SpikeTrapDungeonObjectInstance(
                    instanceId,
                    spikeTrap,
                    position,
                    activationGroupId);
            }

            if (definition is LeverDungeonObjectDefinition lever)
            {
                return new LeverDungeonObjectInstance(
                    instanceId,
                    lever,
                    position,
                    isProgressionRequirement);
            }

            if (definition is ConditionDungeonObjectDefinition condition)
            {
                return new ConditionDungeonObjectInstance(
                    instanceId,
                    condition,
                    position,
                    isProgressionRequirement);
            }

            return null;
        }

        private static int RollChestGold(DungeonInstance dungeon, DungeonFloorInstance floor, int objectIndex)
        {
            var ordinaryEncounters = floor.Encounters.Where(encounter => encounter != null
                && !encounter.IsReinforcement
                && encounter.Definition != dungeon.Definition.BossEncounterDefinition).ToList();
            if (ordinaryEncounters.Count == 0) return 0;

            // Keep the currency roll independent of object/loot selection so an extra
            // chest does not shift the contents or positions of other objects.
            var seed = unchecked(dungeon.Seed ^ ObjectPlacementSeedSalt
                ^ (floor.FloorIndex + 1) * 73856093 ^ (objectIndex + 1) * 19349663);
            var goldRandom = new System.Random(seed);
            var encounterCount = 2 + goldRandom.Next(2);
            long gold = 0;
            for (var encounterIndex = 0; encounterIndex < encounterCount; encounterIndex++)
            {
                var encounter = ordinaryEncounters[goldRandom.Next(ordinaryEncounters.Count)];
                foreach (var enemy in encounter.Enemies)
                {
                    if (enemy?.CharacterDefinition == null) continue;
                    gold += CombatEconomyScaling.RollEnemyGold(dungeon.DungeonLevel,
                        goldRandom.NextDouble());
                }
            }

            return (int)Math.Min(gold, int.MaxValue);
        }

        private static int ComparePositions(
            Vector2Int left,
            Vector2Int right)
        {
            var yComparison = left.y.CompareTo(right.y);

            return yComparison != 0
                ? yComparison
                : left.x.CompareTo(right.x);
        }

        private static int CompareDoorCandidates(
            DoorAnchorCandidate left,
            DoorAnchorCandidate right)
        {
            var firstCellComparison = ComparePositions(
                left.Edge.FirstCell,
                right.Edge.FirstCell);

            return firstCellComparison != 0
                ? firstCellComparison
                : ComparePositions(
                    left.Edge.SecondCell,
                    right.Edge.SecondCell);
        }

        private static void Shuffle<T>(
            List<T> items,
            System.Random random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);

                (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
            }
        }

        private readonly struct DoorAnchorCandidate
        {
            public DungeonCellEdge Edge { get; }

            public DoorAnchorCandidate(DungeonCellEdge edge)
            {
                Edge = edge;
            }
        }

    }

    public sealed class DungeonChestInteractionService
        {
            private readonly InventoryService inventoryService = new();

            public bool TryUnlock(
                GameSession session,
                ChestDungeonObjectInstance chest,
                out string failureReason)
            {
                failureReason = string.Empty;

                if (session == null || chest == null)
                {
                    failureReason = "Nie można otworzyć skrzyni.";
                    return false;
                }

                if (chest.IsUnlocked)
                {
                    return true;
                }

                if (chest.LockType == ChestLockType.KeyRequired
                    || chest.LockType == ChestLockType.KeyOrLockpick)
                {
                    if (TryConsumeKey(session, chest))
                    {
                        return chest.TryUnlockWithKey(chest.RequiredKey);
                    }
                }

                if (chest.LockType == ChestLockType.KeyOrLockpick
                    && TryConsumeLockpick(session))
                {
                    return chest.TryUnlockWithLockpick();
                }

                failureReason = chest.LockType == ChestLockType.KeyRequired
                    ? "Potrzebujesz odpowiedniego klucza."
                    : "Potrzebujesz odpowiedniego klucza albo lockpicka.";
                return false;
            }

            private bool TryConsumeKey(
                GameSession session,
                ChestDungeonObjectInstance chest)
            {
                if (chest.RequiredKey == null
                    || chest.RequiredKey.ItemType != ItemType.Key)
                {
                    return false;
                }

                foreach (var characterId in session.PlayerParty.ActiveMemberIds)
                {
                    if (!session.CharactersById.TryGetValue(
                            characterId,
                            out var character)
                        || character?.Inventory == null)
                    {
                        continue;
                    }

                    foreach (var item in character.Inventory.Items)
                    {
                        if (item?.Definition == null
                            || item.Definition.ItemType != ItemType.Key
                            || item.Definition.Id != chest.RequiredKey.Id)
                        {
                            continue;
                        }

                        return inventoryService.TryRemoveItem(
                            character.Inventory,
                            item.InstanceId,
                            out _);
                    }
                }

                return false;
            }

            private bool TryConsumeLockpick(GameSession session)
            {
                foreach (var characterId in session.PlayerParty.ActiveMemberIds)
                {
                    if (!session.CharactersById.TryGetValue(
                            characterId,
                            out var character)
                        || character?.Inventory == null)
                    {
                        continue;
                    }

                    foreach (var item in character.Inventory.Items)
                    {
                        if (item?.Definition == null
                            || item.Definition.ItemType != ItemType.Tool)
                        {
                            continue;
                        }

                        return inventoryService.TryRemoveItem(
                            character.Inventory,
                            item.InstanceId,
                            out _);
                    }
                }

                return false;
            }
        }
}
