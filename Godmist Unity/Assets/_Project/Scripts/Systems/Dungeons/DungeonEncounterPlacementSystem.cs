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
            if (dungeon.Definition.EncounterDefinitions == null
                || dungeon.Definition.EncounterDefinitions.Count == 0)
            {
                return;
            }

            var random = new System.Random(dungeon.Seed);

            foreach (var floor in dungeon.Floors)
            {
                PlaceEncountersOnFloor(dungeon, floor, random);
            }
        }

        private void PlaceEncountersOnFloor(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            System.Random random)
        {
            if (floor == null || floor.Encounters.Count > 0) return;

            var candidateRooms = GetCandidateRooms(
                dungeon.Definition.RoomTemplateCatalog,
                floor);

            var candidateDefinitions = GetCandidateDefinitions(
                dungeon.Definition.EncounterDefinitions);

            if (candidateRooms.Count == 0 || candidateDefinitions.Count == 0) return;

            Shuffle(candidateRooms, random);
            Shuffle(candidateDefinitions, random);

            var maximumCount = Mathf.Min(
                dungeon.Definition.MaxEncounterCountPerFloor,
                candidateRooms.Count);

            if (!dungeon.Definition.AllowEncounterDefinitionRepeats)
            {
                maximumCount = Mathf.Min(
                    maximumCount,
                    candidateDefinitions.Count);
            }

            if (maximumCount <= 0) return;

            var minimumCount = Mathf.Min(
                dungeon.Definition.MinEncounterCountPerFloor,
                maximumCount);

            var encounterCount = minimumCount == maximumCount
                ? minimumCount
                : random.Next(minimumCount, maximumCount + 1);

            for (var index = 0; index < encounterCount; index++)
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
                    template.SizeInCells);

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

                floor.TryAddEncounter(encounter);
            }
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

            var preferredCell = room.LayoutPosition
                                + new Vector2Int(
                                    template.SizeInCells.x / 2,
                                    template.SizeInCells.y / 2);

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
            DungeonFloorInstance floor)
        {
            var candidates = new List<DungeonRoomInstance>();

            foreach (var room in floor.Rooms)
            {
                if (!IsEligibleRoomType(room.RoomType)) continue;

                if (!TryGetTemplate(catalog, room.TemplateId, out _)) continue;

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

            foreach (var candidate in catalog.Templates)
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
