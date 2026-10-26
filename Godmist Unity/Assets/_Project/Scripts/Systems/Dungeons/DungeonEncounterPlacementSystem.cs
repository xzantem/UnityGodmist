using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public class DungeonEncounterPlacementSystem
    {
        public void Place(DungeonInstance dungeon)
        {
            if (dungeon == null || dungeon.Definition == null) return;
            if (dungeon.Definition.RoomTemplateCatalog == null) return;

            var random = new System.Random(dungeon.Seed);

            foreach (var floor in dungeon.Floors)
            {
                PlaceEncountersOnFloor(dungeon, floor, random);
            }

            PlaceBossEncounter(dungeon);
        }

        private void PlaceEncountersOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            System.Random random)
        {
            if (floor == null
                || HasRegularEncounters(
                    floor,
                    dungeon.Definition.BossEncounterDefinition))
            {
                return;
            }

            var reserveExit = dungeon.LengthProfile != null
                && !(dungeon.Definition.BossEncounterDefinition != null && floor == dungeon.Floors[dungeon.Floors.Count-1]);
            var candidateRooms = GetCandidateRooms(
                dungeon.Definition.RoomTemplateCatalog,
                floor, reserveExit);

            var candidateDefinitions = GetCandidateDefinitions(
                dungeon.Definition.EncounterDefinitions);

            if (candidateRooms.Count == 0 || candidateDefinitions.Count == 0)
            {
                if (dungeon.LengthProfile != null)
                    throw new InvalidOperationException($"Missing encounter rooms or definitions on {dungeon.DungeonId}/{floor.FloorIndex}.");
                return;
            }

            Shuffle(candidateRooms, random);
            if (reserveExit)
            {
                var finalIndex = candidateRooms.FindIndex(r => r.RoomType == DungeonRoomType.Final);
                if (finalIndex < 0) throw new InvalidOperationException("Missing final encounter room.");
                var finalRoom = candidateRooms[finalIndex]; candidateRooms.RemoveAt(finalIndex); candidateRooms.Insert(0,finalRoom);
            }
            Shuffle(candidateDefinitions, random);

            var maximumCount = Mathf.Min(
                dungeon.Definition.MaxEncounterCountPerFloor,
                candidateRooms.Count);
            var requestedMinimum = dungeon.Definition.MinEncounterCountPerFloor;
            if (dungeon.LengthProfile is { } lengthProfile)
            {
                var bossSlot = dungeon.Definition.BossEncounterDefinition != null
                    && floor == dungeon.Floors[dungeon.Floors.Count-1] ? 1 : 0;
                // Extra branch exploration must not silently expand the combat budget.
                var budgetRooms = 0;
                foreach (var room in floor.Rooms)
                    if (!room.IsBranchTransitRoom) budgetRooms++;
                requestedMinimum = Mathf.Max(lengthProfile.MinEncountersPerFloor,
                    Mathf.CeilToInt(budgetRooms*.3f)) - bossSlot;
                maximumCount = Mathf.Min(lengthProfile.MaxEncountersPerFloor,
                    Mathf.FloorToInt(budgetRooms*.4f)) - bossSlot;
                if (maximumCount < requestedMinimum || candidateRooms.Count < requestedMinimum)
                    throw new InvalidOperationException($"Cannot meet encounter density on {dungeon.DungeonId}/{floor.FloorIndex}.");
                maximumCount = Mathf.Min(maximumCount,candidateRooms.Count);
            }

            if (!dungeon.Definition.AllowEncounterDefinitionRepeats)
            {
                maximumCount = Mathf.Min(
                    maximumCount,
                    candidateDefinitions.Count);
            }

            if (dungeon.LengthProfile != null && maximumCount < requestedMinimum)
                throw new InvalidOperationException($"Insufficient distinct encounter definitions on {dungeon.DungeonId}/{floor.FloorIndex}.");

            if (maximumCount <= 0) return;

            var minimumCount = Mathf.Min(
                requestedMinimum,
                maximumCount);

            var encounterCount = minimumCount == maximumCount
                ? minimumCount
                : random.Next(minimumCount, maximumCount + 1);
            if (dungeon.LengthProfile?.MaxBranchTransitRoomCount > 0)
                DungeonEncounterRoomSelector.Prioritize(floor, candidateRooms, encounterCount,
                    dungeon.Definition.BossEncounterDefinition != null && floor == dungeon.Floors[dungeon.Floors.Count - 1]);
            var enemyAnchorResolver = new DungeonEnemyAnchorResolver();

            var placedCount = 0;
            for (var index = 0; index < candidateRooms.Count && placedCount < encounterCount; index++)
            {
                var room = candidateRooms[index];
                var definition = GetEncounterDefinition(
                    candidateDefinitions,
                    dungeon.Definition.AllowEncounterDefinitionRepeats,
                    random);

                if (definition == null
                    || !TryGetTemplate(
                        dungeon.Definition.RoomTemplateCatalog,
                        room.TemplateId,
                        out var template))
                {
                    continue;
                }

                var instanceId =
                    $"{dungeon.DungeonId}.floor_{floor.FloorIndex}.{room.RoomId}.encounter";

                var triggerArea = new RectInt(
                    room.LayoutPosition,
                    DungeonRoomTransformUtility.GetRotatedSize(
                        template.SizeInCells,
                        room.RotationDegrees));

                var encounter = CreateEncounter(
                    dungeon,
                    floor,
                    room,
                    template,
                    definition,
                    instanceId,
                    enemyAnchorResolver);

                if (encounter != null && floor.TryAddEncounter(encounter)) placedCount++;
                else if (reserveExit && room.RoomType == DungeonRoomType.Final)
                    throw new InvalidOperationException("Cannot place required final encounter.");
            }
            if (dungeon.LengthProfile != null && placedCount < minimumCount)
                throw new InvalidOperationException($"Insufficient legal encounter anchors on {dungeon.DungeonId}/{floor.FloorIndex}.");
        }

        private static void PlaceBossEncounter(DungeonInstance dungeon)
        {
            var bossDefinition = dungeon.Definition.BossEncounterDefinition;

            if (bossDefinition == null) return;
            if (HasBossEncounter(dungeon, bossDefinition)) return;

            var finalFloor = GetFinalFloor(dungeon);

            if (finalFloor == null)
            {
                throw new InvalidOperationException(
                    "Nie mozna umiescic bossa bez pietra Final.");
            }

            DungeonRoomInstance finalRoom = null;

            foreach (var room in finalFloor.Rooms)
            {
                if (room != null && room.RoomType == DungeonRoomType.Final)
                {
                    finalRoom = room;
                    break;
                }
            }

            if (finalRoom == null
                || !TryGetTemplate(
                    dungeon.Definition.RoomTemplateCatalog,
                    finalRoom.TemplateId,
                    out var finalTemplate))
            {
                throw new InvalidOperationException(
                    "Nie mozna umiescic bossa bez poprawnego pokoju Final.");
            }

            var instanceId =
                $"{dungeon.DungeonId}.floor_{finalFloor.FloorIndex}."
                + $"{finalRoom.RoomId}.boss";
            var enemyAnchorResolver = new DungeonEnemyAnchorResolver();
            var bossEncounter = CreateEncounter(
                dungeon,
                finalFloor,
                finalRoom,
                finalTemplate,
                bossDefinition,
                instanceId,
                enemyAnchorResolver);

            if (bossEncounter == null
                || !finalFloor.TryAddEncounter(bossEncounter))
            {
                throw new InvalidOperationException(
                    "Nie mozna dodac gwarantowanego boss encountera.");
            }
        }

        private static DungeonEncounterInstance CreateEncounter(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            DungeonRoomInstance room,
            DungeonRoomTemplateDefinition template,
            EncounterDefinition definition,
            string instanceId,
            DungeonEnemyAnchorResolver enemyAnchorResolver)
        {
            if (dungeon == null
                || floor == null
                || room == null
                || template == null
                || definition == null
                || enemyAnchorResolver == null)
            {
                return null;
            }

            var triggerArea = new RectInt(
                room.LayoutPosition,
                DungeonRoomTransformUtility.GetRotatedSize(
                    template.SizeInCells,
                    room.RotationDegrees));
            var encounter = new DungeonEncounterInstance(
                instanceId,
                definition,
                room.RoomId,
                triggerArea);

            if (TryGetPreferredRoomCell(
                    floor,
                    room,
                    template,
                    out var explorationCell))
            {
                encounter.TrySetExplorationPosition(explorationCell);
            }

            foreach (var anchoredEnemy in enemyAnchorResolver.Resolve(
                         floor,
                         dungeon.Definition.RoomTemplateCatalog,
                         encounter, stationaryHomes: encounter.StationaryEnemyHomes))
            {
                encounter.TrySetEnemyExplorationPosition(
                    anchoredEnemy.Key,
                    anchoredEnemy.Value);
            }

            return encounter;
        }

        private static bool HasRegularEncounters(
            DungeonFloorInstance floor,
            EncounterDefinition bossDefinition)
        {
            foreach (var encounter in floor.Encounters)
            {
                if (encounter != null && encounter.Definition != bossDefinition)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBossEncounter(
            DungeonInstance dungeon,
            EncounterDefinition bossDefinition)
        {
            foreach (var floor in dungeon.Floors)
            {
                foreach (var encounter in floor.Encounters)
                {
                    if (encounter != null && encounter.Definition == bossDefinition)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static DungeonFloorInstance GetFinalFloor(DungeonInstance dungeon)
        {
            DungeonFloorInstance finalFloor = null;

            foreach (var floor in dungeon.Floors)
            {
                if (floor != null
                    && (finalFloor == null
                        || floor.FloorIndex > finalFloor.FloorIndex))
                {
                    finalFloor = floor;
                }
            }

            return finalFloor;
        }

        private static bool TryGetPreferredRoomCell(
            DungeonFloorInstance floor,
            DungeonRoomInstance room,
            DungeonRoomTemplateDefinition template,
            out Vector2Int selectedCell)
        {
            selectedCell = default;

            if (floor?.Map == null || room == null || template == null)
            {
                return false;
            }

            var roomSize = DungeonRoomTransformUtility.GetRotatedSize(
                template.SizeInCells,
                room.RotationDegrees);
            var preferredCell = room.LayoutPosition
                                + new Vector2Int(
                                    roomSize.x / 2,
                                    roomSize.y / 2);

            var hasCandidate = false;
            var selectedDistance = int.MaxValue;

            foreach (var cell in floor.Map.FloorCells)
            {
                if (!floor.Map.TryGetRoomId(cell, out var roomId)
                    || roomId != room.RoomId)
                {
                    continue;
                }

                var offset = cell - preferredCell;
                var distance = offset.sqrMagnitude;

                if (hasCandidate
                    && (distance > selectedDistance
                        || (distance == selectedDistance
                            && CompareCells(cell, selectedCell) >= 0)))
                {
                    continue;
                }

                selectedCell = cell;
                selectedDistance = distance;
                hasCandidate = true;
            }

            return hasCandidate;
        }

        private static int CompareCells(
            Vector2Int first,
            Vector2Int second)
        {
            var xComparison = first.x.CompareTo(second.x);

            return xComparison != 0
                ? xComparison
                : first.y.CompareTo(second.y);
        }

        private static List<DungeonRoomInstance> GetCandidateRooms(
            DungeonRoomTemplateCatalog catalog,
            DungeonFloorInstance floor, bool includeFinal)
        {
            var candidates = new List<DungeonRoomInstance>();

            foreach (var room in floor.Rooms)
            {
                var final = includeFinal && room.RoomType == DungeonRoomType.Final;
                if (!final && !IsEligibleRoomType(room.RoomType)) continue;

                if (!TryGetTemplate(catalog, room.TemplateId, out var template)
                    || !final && template.ExcludeFromRandomEncounters) continue;

                candidates.Add(room);
            }

            return candidates;
        }

        private static List<EncounterDefinition> GetCandidateDefinitions(
            List<EncounterDefinition> encounterDefinitions)
        {
            var candidates = new List<EncounterDefinition>();

            foreach (var definition in encounterDefinitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) continue;
                if (definition.Enemies == null || definition.Enemies.Count == 0) continue;

                candidates.Add(definition);
            }

            return candidates;
        }

        private static EncounterDefinition GetEncounterDefinition(
            List<EncounterDefinition> definitions,
            bool allowRepeats,
            System.Random random)
        {
            if (definitions.Count == 0) return null;

            if (allowRepeats)
            {
                return definitions[random.Next(definitions.Count)];
            }

            var definition = definitions[0];
            definitions.RemoveAt(0);

            return definition;
        }

        private static bool IsEligibleRoomType(DungeonRoomType roomType)
        {
            return roomType == DungeonRoomType.Normal
                || roomType == DungeonRoomType.Optional
                || roomType == DungeonRoomType.Corridor;
        }

        private static bool TryGetTemplate(
            DungeonRoomTemplateCatalog catalog,
            string templateId,
            out DungeonRoomTemplateDefinition template)
        {
            template = null;

            if (catalog == null || string.IsNullOrWhiteSpace(templateId)) return false;

            foreach (var candidate in catalog.AllTemplates)
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.Id,
                        templateId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                template = candidate;
                return true;
            }

            return false;
        }

        private static void Shuffle<T>(List<T> items, System.Random random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);

                (items[index], items[swapIndex]) =
                    (items[swapIndex], items[index]);
            }
        }
    }
}
