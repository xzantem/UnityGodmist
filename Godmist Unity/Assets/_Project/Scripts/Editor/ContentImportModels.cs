using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Editor
{
    public enum ContentImportTargetType
    {
        ItemDefinition,
        ConsumableItemDefinition,
        CraftableItemDefinition,
        EquipmentItemDefinition,
        EquipmentPartDefinition,
        SkillData,
        CharacterDefinition
    }

    public enum ContentImportChangeKind
    {
        Create,
        Update,
        Conflict,
        Error
    }

    public sealed class ContentImportSheet
    {
        public string Name { get; }
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
        public int HeaderRowIndex { get; }

        public ContentImportSheet(
            string name,
            IReadOnlyList<IReadOnlyList<string>> rows,
            int headerRowIndex)
        {
            Name = name;
            Rows = rows;
            HeaderRowIndex = headerRowIndex;
        }
    }

    public sealed class ContentImportFile
    {
        public string Path { get; }
        public IReadOnlyList<ContentImportSheet> Sheets { get; }

        public ContentImportFile(string path, IReadOnlyList<ContentImportSheet> sheets)
        {
            Path = path;
            Sheets = sheets;
        }
    }

    public sealed class ContentImportPreviewRow
    {
        public int SourceRowNumber { get; }
        public ContentImportTargetType TargetType { get; }
        public string SourceSummary { get; }
        public string Id { get; set; }
        public string DisplayName { get; }
        public UnityEngine.Object ExistingAsset { get; set; }
        public ContentImportChangeKind ChangeKind { get; set; }
        public bool Selected { get; set; }
        public Dictionary<string, string> Values { get; }
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public List<string> MissingReferences { get; }

        public bool IsValid => Errors.Count == 0 && MissingReferences.Count == 0;

        public ContentImportPreviewRow(
            int sourceRowNumber,
            ContentImportTargetType targetType,
            string sourceSummary,
            string id,
            string displayName,
            UnityEngine.Object existingAsset)
        {
            SourceRowNumber = sourceRowNumber;
            TargetType = targetType;
            SourceSummary = sourceSummary;
            Id = id;
            DisplayName = displayName;
            ExistingAsset = existingAsset;
            Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Errors = new List<string>();
            Warnings = new List<string>();
            MissingReferences = new List<string>();
            ChangeKind = existingAsset == null
                ? ContentImportChangeKind.Create
                : ContentImportChangeKind.Update;
            Selected = IsValid;
        }

        public string StatusLabel => ChangeKind switch
        {
            ContentImportChangeKind.Create => "CREATE",
            ContentImportChangeKind.Update => "UPDATE",
            ContentImportChangeKind.Conflict => "CONFLICT",
            ContentImportChangeKind.Error => "ERROR",
            _ => string.Empty
        };

        public string ProblemSummary
        {
            get
            {
                var problems = new List<string>();
                problems.AddRange(Errors);
                problems.AddRange(MissingReferences);
                return string.Join(" | ", problems);
            }
        }
    }
}
