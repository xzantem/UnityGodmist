using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class ContentImportPreviewWindow : EditorWindow
    {
        private static readonly string[] TargetLabels =
        {
            "ItemDefinition", "ConsumableItemDefinition", "CraftableItemDefinition",
            "EquipmentItemDefinition", "EquipmentPartDefinition", "SkillData", "CharacterDefinition"
        };

        private GameContentCatalog activeCatalog;
        private ContentImportFile sourceFile;
        private string sourcePath = string.Empty;
        private int selectedSheet;
        private ContentImportTargetType targetType;
        private int headerRow;
        private List<ContentImportPreviewRow> previewRows = new();
        private Vector2 tableScroll;
        private bool showOnlyProblems;
        private bool showMappedValues = true;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("Godmist/Content/Import Preview")]
        private static void OpenFromMenu()
        {
            var window = GetWindow<ContentImportPreviewWindow>("Import Preview");
            window.minSize = new Vector2(1120f, 650f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSourceControls();
            DrawSummary();
            DrawPreviewTable();
            if (!string.IsNullOrWhiteSpace(statusMessage)) EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            activeCatalog = (GameContentCatalog)EditorGUILayout.ObjectField(
                new GUIContent("Aktywny katalog"), activeCatalog, typeof(GameContentCatalog), false,
                GUILayout.Width(360f));
            if (GUILayout.Button("Wczyść preview", EditorStyles.toolbarButton, GUILayout.Width(110f))) ClearPreview();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Odrzuć import", EditorStyles.toolbarButton, GUILayout.Width(110f))) ClearPreview();
            EditorGUILayout.EndHorizontal();
            if (activeCatalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Wybierz aktywny GameContentCatalog. Tylko assety z tego katalogu mogą być rozpoznane jako UPDATE.",
                    MessageType.Info);
            }
        }

        private void DrawSourceControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Źródło", GUILayout.Width(70f));
            EditorGUILayout.SelectableLabel(
                string.IsNullOrWhiteSpace(sourcePath) ? "Nie wybrano pliku" : sourcePath,
                EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Wybierz .xlsx / .csv", GUILayout.Width(150f))) SelectSource();
            EditorGUILayout.EndHorizontal();

            if (sourceFile != null && sourceFile.Sheets.Count > 0)
            {
                var sheetNames = sourceFile.Sheets.Select(sheet => sheet.Name).ToArray();
                var nextSheet = EditorGUILayout.Popup("Arkusz", selectedSheet, sheetNames);
                if (nextSheet != selectedSheet)
                {
                    selectedSheet = nextSheet;
                    targetType = ContentImportMapper.GetDefaultTarget(sourcePath, CurrentSheet.Name);
                    headerRow = CurrentSheet.HeaderRowIndex + 1;
                    previewRows.Clear();
                }

                var nextTarget = (ContentImportTargetType)EditorGUILayout.Popup(
                    "Mapuj na", (int)targetType, TargetLabels);
                if (nextTarget != targetType)
                {
                    targetType = nextTarget;
                    previewRows.Clear();
                }
                headerRow = Mathf.Max(1, EditorGUILayout.IntField("Wiersz nagłówków", headerRow));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Zbuduj / przebuduj preview", GUILayout.Height(26f))) BuildPreview();
                showOnlyProblems = GUILayout.Toggle(showOnlyProblems, "Tylko błędne / konfliktowe", "Button", GUILayout.Width(170f));
                showMappedValues = GUILayout.Toggle(showMappedValues, "Pokaż mapowane wartości", "Button", GUILayout.Width(190f));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSummary()
        {
            if (previewRows.Count == 0) return;
            var create = previewRows.Count(row => row.ChangeKind == ContentImportChangeKind.Create);
            var update = previewRows.Count(row => row.ChangeKind == ContentImportChangeKind.Update);
            var problems = previewRows.Count(row => row.ChangeKind is ContentImportChangeKind.Error or ContentImportChangeKind.Conflict);
            EditorGUILayout.LabelField(
                $"Rekordów: {previewRows.Count}    CREATE: {create}    UPDATE: {update}    BŁĘDNE/KONFLIKTY: {problems}",
                EditorStyles.boldLabel);
        }

        private void DrawPreviewTable()
        {
            if (previewRows.Count == 0)
            {
                EditorGUILayout.HelpBox("Wybierz źródło i zbuduj preview, aby zobaczyć planowane zmiany.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!CanApply(ContentImportChangeKind.Create)))
            {
                if (GUILayout.Button("Zastosuj zaznaczone CREATE", GUILayout.Height(28f)))
                    ConfirmAndApply(ContentImportChangeKind.Create);
            }
            using (new EditorGUI.DisabledScope(!CanApply(ContentImportChangeKind.Update)))
            {
                if (GUILayout.Button("Zastosuj zaznaczone UPDATE", GUILayout.Height(28f)))
                    ConfirmAndApply(ContentImportChangeKind.Update);
            }
            EditorGUILayout.EndHorizontal();

            tableScroll = EditorGUILayout.BeginScrollView(tableScroll);
            DrawHeader();
            foreach (var row in previewRows)
            {
                if (showOnlyProblems && row.IsValid && row.ChangeKind is not ContentImportChangeKind.Conflict) continue;
                DrawRow(row);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("✓", GUILayout.Width(24f));
            GUILayout.Label("Wiersz", GUILayout.Width(48f));
            GUILayout.Label("Status", GUILayout.Width(70f));
            GUILayout.Label("ID (edytowalne)", GUILayout.Width(230f));
            GUILayout.Label("Nazwa / źródło", GUILayout.Width(270f));
            GUILayout.Label("Asset katalogu", GUILayout.Width(190f));
            GUILayout.Label("Problemy", GUILayout.MinWidth(300f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(ContentImportPreviewRow row)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            using (new EditorGUI.DisabledScope(!row.IsValid || row.ChangeKind is ContentImportChangeKind.Conflict))
                row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(24f));
            EditorGUILayout.LabelField(row.SourceRowNumber.ToString(), GUILayout.Width(48f));
            var color = row.ChangeKind switch
            {
                ContentImportChangeKind.Create => Color.green,
                ContentImportChangeKind.Update => Color.cyan,
                _ => Color.red
            };
            var previous = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(row.StatusLabel, GUILayout.Width(70f));
            GUI.color = previous;

            EditorGUI.BeginChangeCheck();
            row.Id = EditorGUILayout.TextField(row.Id ?? string.Empty, GUILayout.Width(230f));
            var idChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.DisplayName) ? row.SourceSummary : row.DisplayName,
                EditorStyles.miniLabel, GUILayout.Width(270f));
            EditorGUILayout.LabelField(row.ExistingAsset == null ? "—" : row.ExistingAsset.name,
                EditorStyles.miniLabel, GUILayout.Width(190f));
            EditorGUILayout.LabelField(row.ProblemSummary, EditorStyles.miniLabel, GUILayout.MinWidth(300f));
            EditorGUILayout.EndHorizontal();

            if (showMappedValues && row.Values.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Mapowane wartości", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    var mapped = string.Join(
                        "\n",
                        row.Values
                            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(pair => $"{pair.Key}: {pair.Value}"));
                    EditorGUILayout.TextArea(mapped, EditorStyles.miniLabel,
                        GUILayout.MinHeight(Mathf.Max(24f, mapped.Count(character => character == '\n' || character == '|') * 12f + 24f)));
                }
                EditorGUILayout.EndVertical();
            }

            if (idChanged)
            {
                ContentImportMapper.ReevaluateStatuses(previewRows, activeCatalog);
                GUI.changed = true;
            }
            if (row.Warnings.Count > 0)
                EditorGUILayout.LabelField("  Ostrzeżenia: " + string.Join(" | ", row.Warnings), EditorStyles.miniLabel);
        }

        private void SelectSource()
        {
            var designFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "design"));
            var selected = EditorUtility.OpenFilePanel("Wybierz źródło contentu", designFolder, "xlsx,csv,tsv");
            if (string.IsNullOrWhiteSpace(selected)) return;
            if (!ContentImportSourceReader.TryRead(selected, out var file, out var error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }
            sourcePath = selected;
            sourceFile = file;
            selectedSheet = 0;
            targetType = ContentImportMapper.GetDefaultTarget(sourcePath, CurrentSheet.Name);
            headerRow = CurrentSheet.HeaderRowIndex + 1;
            previewRows.Clear();
            SetStatus($"Wczytano {file.Sheets.Count} arkusz(y). Wybierz arkusz i zbuduj preview.", MessageType.Info);
        }

        private void BuildPreview()
        {
            if (sourceFile == null || sourceFile.Sheets.Count == 0) return;
            var rows = CurrentSheet.Rows.ToList();
            var sheet = new ContentImportSheet(CurrentSheet.Name, rows, Mathf.Clamp(headerRow - 1, 0, rows.Count - 1));
            previewRows = ContentImportMapper.BuildPreview(sourcePath, sheet, targetType, activeCatalog);
            SetStatus($"Preview gotowy: {previewRows.Count} rekordów. Zmiany nie zostały jeszcze zapisane.", MessageType.Info);
        }

        private bool CanApply(ContentImportChangeKind kind) =>
            activeCatalog != null && previewRows.Any(row => row.Selected && row.IsValid && row.ChangeKind == kind);

        private void ConfirmAndApply(ContentImportChangeKind kind)
        {
            var selected = previewRows.Where(row => row.Selected && row.IsValid && row.ChangeKind == kind).ToList();
            if (selected.Count == 0) return;
            var verb = kind == ContentImportChangeKind.Create ? "utworzyć" : "zaktualizować";
            if (!EditorUtility.DisplayDialog(
                    "Potwierdź import",
                    $"Czy na pewno {verb} {selected.Count} asset(ów)?\n\nImport nie obejmuje rekordów z błędami ani konfliktami.",
                    "Zastosuj", "Anuluj")) return;

            var applied = 0;
            var failed = 0;
            foreach (var row in selected)
            {
                if (ContentImportApplier.TryApply(row, activeCatalog, out _, out var error))
                {
                    row.Selected = false;
                    applied++;
                }
                else
                {
                    row.Errors.Add(error);
                    row.ChangeKind = ContentImportChangeKind.Error;
                    row.Selected = false;
                    failed++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ContentImportMapper.ReevaluateStatuses(previewRows, activeCatalog);
            SetStatus($"Zastosowano: {applied}. Nie zastosowano: {failed}.", failed == 0 ? MessageType.Info : MessageType.Warning);
        }

        private void ClearPreview()
        {
            sourceFile = null;
            sourcePath = string.Empty;
            previewRows.Clear();
            statusMessage = string.Empty;
        }

        private ContentImportSheet CurrentSheet => sourceFile?.Sheets[Mathf.Clamp(selectedSheet, 0, sourceFile.Sheets.Count - 1)];

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }
    }
}
