using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class DungeonRoomTemplateValidationReport
    {
        private readonly List<string> errors = new();

        public IReadOnlyList<string> Errors => errors;
        public bool IsValid => errors.Count == 0;

        public void AddError(string message)
        {
            errors.Add(message);
        }
    }

    public static class DungeonRoomTemplateEditorValidator
    {
        private static readonly Vector2Int[] cardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static DungeonRoomTemplateValidationReport Validate(
            DungeonRoomTemplateDefinition template)
        {
            var report = new DungeonRoomTemplateValidationReport();

            if (template == null)
            {
                report.AddError("Room template nie moze byc nullem.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(template.Id))
            {
                report.AddError("Id room template jest wymagane.");
            }

            if (string.IsNullOrWhiteSpace(template.DisplayName))
            {
                report.AddError("Display Name room template jest wymagane.");
            }

            if (!Enum.IsDefined(typeof(DungeonRoomType), template.RoomType))
            {
                report.AddError("Room Type ma nieprawidlowa wartosc.");
            }

            var size = template.SizeInCells;

            if (size.x <= 0 || size.y <= 0)
            {
                report.AddError("Rozmiar room template musi byc wiekszy od zera.");
                return report;
            }

            var floorCells = ValidateFootprint(template, size, report);
            ValidateBoundaryEdges(template, floorCells, report);
            ValidateStructuralAnchors(template, floorCells, report);
            ValidateSemanticAnchors(template, floorCells, report);
            ValidateProgressionBranchTemplate(template, report);
            ValidateNonProgressionControls(template, report);

            return report;
        }

        private static void ValidateProgressionBranchTemplate(
            DungeonRoomTemplateDefinition template,
            DungeonRoomTemplateValidationReport report)
        {
            if (!template.CanBeRequiredProgressionBranch)
            {
                return;
            }

            if (template.RoomType != DungeonRoomType.Optional)
            {
                report.AddError(
                    "Szablon wymaganej odnogi progresji musi byc typu Optional.");
            }

            var hasProgressionControl = false;

            foreach (var anchor in template.SemanticAnchors
                         ?? new List<DungeonSemanticAnchorData>())
            {
                if (anchor?.Type == DungeonSemanticAnchorType.Lever
                    || anchor?.Type == DungeonSemanticAnchorType.Condition)
                {
                    hasProgressionControl = true;
                    break;
                }
            }

            if (!hasProgressionControl)
            {
                report.AddError(
                    "Szablon wymaganej odnogi progresji musi zawierac "
                    + "co najmniej jeden anchor Lever albo Condition.");
            }

            return;
        }

        private static void ValidateNonProgressionControls(
            DungeonRoomTemplateDefinition template,
            DungeonRoomTemplateValidationReport report)
        {
            if (template.CanBeRequiredProgressionBranch)
            {
                return;
            }

            foreach (var anchor in template.SemanticAnchors
                         ?? new List<DungeonSemanticAnchorData>())
            {
                if (anchor?.Type != DungeonSemanticAnchorType.Lever
                    && anchor?.Type != DungeonSemanticAnchorType.Condition)
                {
                    continue;
                }

                report.AddError(
                    $"Anchor '{anchor.Id}' typu Lever albo Condition moze "
                    + "znajdowac sie tylko w szablonie wymaganej odnogi progresji.");
            }
        }

        private static HashSet<Vector2Int> ValidateFootprint(
            DungeonRoomTemplateDefinition template,
            Vector2Int size,
            DungeonRoomTemplateValidationReport report)
        {
            var footprint = template.Footprint;
            var floorCells = new HashSet<Vector2Int>();

            if (footprint == null || footprint.UsesFullBoundingBox)
            {
                for (var x = 0; x < size.x; x++)
                {
                    for (var y = 0; y < size.y; y++)
                    {
                        floorCells.Add(new Vector2Int(x, y));
                    }
                }

                return floorCells;
            }

            foreach (var offset in footprint.FloorCellOffsets)
            {
                if (offset.x < 0
                    || offset.y < 0
                    || offset.x >= size.x
                    || offset.y >= size.y)
                {
                    report.AddError(
                        $"Footprint zawiera komorke ({offset.x}, {offset.y}) "
                        + "poza rozmiarem room template.");
                    continue;
                }

                if (!floorCells.Add(offset))
                {
                    report.AddError(
                        $"Footprint zawiera zduplikowana komorke "
                        + $"({offset.x}, {offset.y}).");
                }
            }

            if (floorCells.Count == 0)
            {
                report.AddError("Jawny footprint musi zawierac co najmniej jedna komorke.");
                return floorCells;
            }

            ValidateFootprintConnectivity(floorCells, report);
            return floorCells;
        }

        private static void ValidateBoundaryEdges(
            DungeonRoomTemplateDefinition template,
            HashSet<Vector2Int> floorCells,
            DungeonRoomTemplateValidationReport report)
        {
            if (!template.BoundaryEdgesConfigured)
            {
                return;
            }

            var usedEdges = new HashSet<BoundaryEdgeKey>();

            foreach (var edge in template.AllowedBoundaryEdges
                         ?? new List<DungeonRoomBoundaryEdgeData>())
            {
                if (edge == null)
                {
                    report.AddError("Lista external passage edges zawiera null.");
                    continue;
                }

                if (!Enum.IsDefined(
                        typeof(DungeonEdgeDirection),
                        edge.Direction))
                {
                    report.AddError("External passage edge ma nieprawidlowy kierunek.");
                    continue;
                }

                if (!floorCells.Contains(edge.LocalCell)
                    || !DungeonRoomBoundaryUtility.IsExternalBoundaryEdge(
                        edge.LocalCell,
                        edge.Direction,
                        template.SizeInCells,
                        floorCells))
                {
                    report.AddError(
                        $"External passage edge ({edge.LocalCell.x}, {edge.LocalCell.y}) "
                        + $"/{edge.Direction} nie lezy na zewnetrznej krawedzi footprintu.");
                    continue;
                }

                if (!usedEdges.Add(new BoundaryEdgeKey(
                        edge.LocalCell,
                        edge.Direction)))
                {
                    report.AddError(
                        $"External passage edge ({edge.LocalCell.x}, {edge.LocalCell.y}) "
                        + $"/{edge.Direction} jest zduplikowany.");
                }
            }
        }

        private static void ValidateFootprintConnectivity(
            HashSet<Vector2Int> floorCells,
            DungeonRoomTemplateValidationReport report)
        {
            var visited = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            var first = default(Vector2Int);

            foreach (var cell in floorCells)
            {
                first = cell;
                break;
            }

            pending.Enqueue(first);
            visited.Add(first);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();

                foreach (var direction in cardinalDirections)
                {
                    var neighbour = current + direction;

                    if (floorCells.Contains(neighbour) && visited.Add(neighbour))
                    {
                        pending.Enqueue(neighbour);
                    }
                }
            }

            if (visited.Count != floorCells.Count)
            {
                report.AddError(
                    "Wszystkie komorki footprintu musza tworzyc jeden "
                    + "fizycznie spojny obszar.");
            }
        }

        private static void ValidateStructuralAnchors(
            DungeonRoomTemplateDefinition template,
            HashSet<Vector2Int> floorCells,
            DungeonRoomTemplateValidationReport report)
        {
            var usedEdges = new HashSet<StructuralEdgeKey>();

            foreach (var anchor in template.StructuralAnchors
                         ?? new List<DungeonStructuralAnchorData>())
            {
                if (anchor == null)
                {
                    report.AddError("Lista structural anchors zawiera null.");
                    continue;
                }

                if (!Enum.IsDefined(
                        typeof(DungeonStructuralAnchorType),
                        anchor.Type))
                {
                    report.AddError("Structural anchor ma nieprawidlowy typ.");
                }

                if (!Enum.IsDefined(
                        typeof(DungeonEdgeDirection),
                        anchor.Direction))
                {
                    report.AddError("Structural anchor ma nieprawidlowy kierunek.");
                    continue;
                }

                var firstCell = anchor.LocalCell;
                var secondCell = firstCell + anchor.GetDirectionOffset();

                if (!floorCells.Contains(firstCell)
                    || !floorCells.Contains(secondCell))
                {
                    report.AddError(
                        $"Structural anchor ({firstCell.x}, {firstCell.y}) "
                        + "musi wskazywac dwie komorki podlogi wewnatrz pokoju.");
                    continue;
                }

                var edge = new StructuralEdgeKey(firstCell, secondCell);

                if (!usedEdges.Add(edge))
                {
                    report.AddError(
                        $"Structural anchor na krawedzi ({firstCell.x}, {firstCell.y}) "
                        + $"- ({secondCell.x}, {secondCell.y}) jest zduplikowany.");
                }
            }
        }

        private static void ValidateSemanticAnchors(
            DungeonRoomTemplateDefinition template,
            HashSet<Vector2Int> floorCells,
            DungeonRoomTemplateValidationReport report)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
            var enemyIndices = new HashSet<int>();
            var enemyCells = new HashSet<Vector2Int>();
            var enemyAnchors = new List<DungeonSemanticAnchorData>();
            var semanticAnchors = template.SemanticAnchors
                                  ?? new List<DungeonSemanticAnchorData>();

            foreach (var anchor in semanticAnchors)
            {
                if (anchor == null)
                {
                    report.AddError("Lista semantic anchors zawiera null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(anchor.Id))
                {
                    report.AddError("Kazdy semantic anchor musi miec Id.");
                }
                else if (!ids.Add(anchor.Id))
                {
                    report.AddError(
                        $"Semantic anchor Id '{anchor.Id}' jest zduplikowane.");
                }

                if (!Enum.IsDefined(
                        typeof(DungeonSemanticAnchorType),
                        anchor.Type))
                {
                    report.AddError("Semantic anchor ma nieprawidlowy typ.");
                }

                if (anchor.AreaSize.x <= 0 || anchor.AreaSize.y <= 0)
                {
                    report.AddError(
                        $"Semantic anchor '{anchor.Id}' musi miec dodatni rozmiar obszaru.");
                }
                else
                {
                    for (var x = anchor.LocalCell.x;
                        x < anchor.LocalCell.x + anchor.AreaSize.x;
                        x++)
                    {
                        for (var y = anchor.LocalCell.y;
                            y < anchor.LocalCell.y + anchor.AreaSize.y;
                            y++)
                        {
                            if (!floorCells.Contains(new Vector2Int(x, y)))
                            {
                                report.AddError(
                                    $"Semantic anchor '{anchor.Id}' wskazuje "
                                    + "obszar poza footprintem.");
                                x = anchor.LocalCell.x + anchor.AreaSize.x;
                                break;
                            }
                        }
                    }
                }

                if (anchor.Type == DungeonSemanticAnchorType.Enemy)
                {
                    enemyAnchors.Add(anchor);

                    if (anchor.EncounterEnemyIndex < 0)
                    {
                        report.AddError(
                            $"Enemy anchor '{anchor.Id}' musi wskazywac "
                            + "nieujemny indeks przeciwnika z encounteru.");
                    }
                    else if (!enemyIndices.Add(anchor.EncounterEnemyIndex))
                    {
                        report.AddError(
                            $"Enemy index '{anchor.EncounterEnemyIndex}' "
                            + "jest przypisane do wiecej niz jednego anchora.");
                    }

                    if (anchor.AreaSize != Vector2Int.one)
                    {
                        report.AddError(
                            $"Enemy anchor '{anchor.Id}' musi wskazywac "
                            + "dokladnie jedna komorke.");
                    }

                    if (!enemyCells.Add(anchor.LocalCell))
                    {
                        report.AddError(
                            $"Enemy anchory nie moga dzielic komorki "
                            + $"({anchor.LocalCell.x}, {anchor.LocalCell.y}).");
                    }
                }

                if (anchor.SpawnChance < 0f || anchor.SpawnChance > 1f)
                {
                    report.AddError(
                        $"Semantic anchor '{anchor.Id}' ma Spawn Chance poza zakresem 0-1.");
                }

                if (!string.IsNullOrWhiteSpace(anchor.Id)
                    && !string.IsNullOrWhiteSpace(anchor.RequiresAnchorId))
                {
                    dependencies[anchor.Id] = anchor.RequiresAnchorId;
                }
            }

            foreach (var dependency in dependencies)
            {
                if (!ids.Contains(dependency.Value))
                {
                    report.AddError(
                        $"Semantic anchor '{dependency.Key}' wymaga nieistniejacej "
                        + $"kotwy '{dependency.Value}'.");
                }
            }

            for (var firstIndex = 0;
                firstIndex < enemyAnchors.Count;
                firstIndex++)
            {
                for (var secondIndex = 0;
                    secondIndex < semanticAnchors.Count;
                    secondIndex++)
                {
                    var other = semanticAnchors[secondIndex];

                    if (other == null
                        || other.Type == DungeonSemanticAnchorType.Enemy
                        || !AreAreasOverlapping(
                            enemyAnchors[firstIndex],
                            other))
                    {
                        continue;
                    }

                    report.AddError(
                        $"Enemy anchor '{enemyAnchors[firstIndex].Id}' "
                        + $"koliduje z semantic anchorem '{other.Id}'.");
                }
            }

            ValidateDependencyCycles(dependencies, report);
        }

        private static bool AreAreasOverlapping(
            DungeonSemanticAnchorData first,
            DungeonSemanticAnchorData second)
        {
            var firstArea = new RectInt(
                first.LocalCell,
                first.AreaSize);
            var secondArea = new RectInt(
                second.LocalCell,
                second.AreaSize);

            return firstArea.Overlaps(secondArea);
        }

        private static void ValidateDependencyCycles(
            Dictionary<string, string> dependencies,
            DungeonRoomTemplateValidationReport report)
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var id in dependencies.Keys)
            {
                if (VisitDependency(id, dependencies, states))
                {
                    report.AddError(
                        $"Semantic anchors zawieraja cykl zaleznosci od '{id}'.");
                }
            }
        }

        private static bool VisitDependency(
            string id,
            Dictionary<string, string> dependencies,
            Dictionary<string, int> states)
        {
            if (states.TryGetValue(id, out var state))
            {
                return state == 1;
            }

            states[id] = 1;

            if (dependencies.TryGetValue(id, out var dependency)
                && dependencies.ContainsKey(dependency)
                && VisitDependency(dependency, dependencies, states))
            {
                return true;
            }

            states[id] = 2;
            return false;
        }

        private readonly struct StructuralEdgeKey : IEquatable<StructuralEdgeKey>
        {
            private readonly Vector2Int first;
            private readonly Vector2Int second;

            public StructuralEdgeKey(Vector2Int firstCell, Vector2Int secondCell)
            {
                if (firstCell.x < secondCell.x
                    || firstCell.x == secondCell.x && firstCell.y <= secondCell.y)
                {
                    first = firstCell;
                    second = secondCell;
                }
                else
                {
                    first = secondCell;
                    second = firstCell;
                }
            }

            public bool Equals(StructuralEdgeKey other)
            {
                return first == other.first && second == other.second;
            }

            public override bool Equals(object obj)
            {
                return obj is StructuralEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(first, second);
            }
        }

        private readonly struct BoundaryEdgeKey : IEquatable<BoundaryEdgeKey>
        {
            private readonly Vector2Int cell;
            private readonly DungeonEdgeDirection direction;

            public BoundaryEdgeKey(
                Vector2Int localCell,
                DungeonEdgeDirection edgeDirection)
            {
                cell = localCell;
                direction = edgeDirection;
            }

            public bool Equals(BoundaryEdgeKey other)
            {
                return cell == other.cell && direction == other.direction;
            }

            public override bool Equals(object obj)
            {
                return obj is BoundaryEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(cell, direction);
            }
        }
    }

    public sealed class DungeonRoomTemplateAssetSaveGuard : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            var allowedPaths = new List<string>(paths.Length);

            foreach (var path in paths)
            {
                var template = AssetDatabase.LoadAssetAtPath<
                    DungeonRoomTemplateDefinition>(path);

                if (template == null)
                {
                    allowedPaths.Add(path);
                    continue;
                }

                var report = DungeonRoomTemplateEditorValidator.Validate(template);

                if (report.IsValid)
                {
                    allowedPaths.Add(path);
                    continue;
                }

                Debug.LogError(
                    $"Nie zapisano room template '{path}', poniewaz "
                    + "walidacja nadal zawiera bledy:\n- "
                    + string.Join("\n- ", report.Errors));
            }

            return allowedPaths.ToArray();
        }
    }
}
