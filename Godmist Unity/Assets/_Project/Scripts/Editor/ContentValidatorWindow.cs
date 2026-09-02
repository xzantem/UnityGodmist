using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class ContentValidatorWindow : EditorWindow
    {
        private GameContentCatalog activeCatalog;
        private ContentProjectValidationReport report;
        private Vector2 scrollPosition;
        private string search = string.Empty;
        private bool showErrors = true;
        private bool showWarnings = true;
        private bool showInfos = true;

        [MenuItem("Godmist/Content/Content Validator")]
        private static void Open()
        {
            var window = GetWindow<ContentValidatorWindow>("Content Validator");
            window.minSize = new Vector2(780f, 420f);
            window.TrySelectMainCatalog();
            window.RefreshReport();
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (report == null)
            {
                EditorGUILayout.HelpBox("Uruchom skan, aby wygenerować raport contentu.", MessageType.Info);
                return;
            }

            DrawSummary();
            DrawIssues();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            activeCatalog = (GameContentCatalog)EditorGUILayout.ObjectField(
                new GUIContent("Aktywny katalog"), activeCatalog, typeof(GameContentCatalog), false,
                GUILayout.Width(360f));
            if (EditorGUI.EndChangeCheck()) report = null;
            if (GUILayout.Button("Skanuj", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                RefreshReport();
            if (GUILayout.Button("Odśwież assety", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                RefreshReport();
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search, FindStyle("ToolbarSearchTextField"), GUILayout.Width(220f));
            if (GUILayout.Button("×", GUI.skin.FindStyle("ToolbarSearchCancelButton"), GUILayout.Width(18f)))
                search = string.Empty;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            showErrors = GUILayout.Toggle(showErrors, "Błędy", EditorStyles.toolbarButton);
            showWarnings = GUILayout.Toggle(showWarnings, "Ostrzeżenia", EditorStyles.toolbarButton);
            showInfos = GUILayout.Toggle(showInfos, "Informacje", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Assety: {report.ScannedAssetCount}", GUILayout.Width(110f));
            EditorGUILayout.LabelField($"Błędy: {report.Errors.Count}", GUILayout.Width(100f));
            EditorGUILayout.LabelField($"Ostrzeżenia: {report.Warnings.Count}", GUILayout.Width(130f));
            EditorGUILayout.LabelField($"Informacje: {report.Infos.Count}", GUILayout.Width(120f));
            EditorGUILayout.LabelField(report.IsValid ? "VALID" : "INVALID", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawIssues()
        {
            var issues = GetVisibleIssues().ToList();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var issue in issues)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(GetSeverityLabel(issue.Severity), EditorStyles.boldLabel);
                var owner = issue.Asset == null ? "Projekt" : issue.Asset.name;
                var field = string.IsNullOrWhiteSpace(issue.FieldPath) ? string.Empty : $" — {issue.FieldPath}";
                EditorGUILayout.LabelField($"{owner}{field}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrWhiteSpace(issue.AssetPath))
                    EditorGUILayout.LabelField(issue.AssetPath, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                using (new EditorGUI.DisabledScope(issue.Asset == null))
                {
                    if (GUILayout.Button("Przejdź", GUILayout.Width(72f), GUILayout.Height(ThirtyHeight())))
                        issue.SelectAsset();
                }
                EditorGUILayout.EndHorizontal();
            }
            if (issues.Count == 0)
                EditorGUILayout.HelpBox("Brak wpisów dla aktywnych filtrów.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private IEnumerable<ContentValidationIssue> GetVisibleIssues()
        {
            return report.Issues.Where(issue =>
            {
                var severityVisible = issue.Severity switch
                {
                    ContentValidationSeverity.Error => showErrors,
                    ContentValidationSeverity.Warning => showWarnings,
                    ContentValidationSeverity.Info => showInfos,
                    _ => true
                };
                if (!severityVisible) return false;
                if (string.IsNullOrWhiteSpace(search)) return true;
                var text = $"{issue.Asset?.name} {issue.AssetPath} {issue.FieldPath} {issue.Message}";
                return text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private void RefreshReport()
        {
            report = ContentAuthoringValidator.ValidateProject(activeCatalog);
            Repaint();
        }

        private void TrySelectMainCatalog()
        {
            if (activeCatalog != null) return;
            var guids = AssetDatabase.FindAssets(
                "t:GameContentCatalog", new[] { "Assets/_Project/ScriptableObjects" });
            if (guids.Length == 0) return;
            activeCatalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static string GetSeverityLabel(ContentValidationSeverity severity) => severity switch
        {
            ContentValidationSeverity.Error => "ERROR",
            ContentValidationSeverity.Warning => "WARNING",
            _ => "INFO"
        };

        private static GUIStyle FindStyle(string name)
        {
            return GUI.skin.FindStyle(name) ?? EditorStyles.toolbarTextField;
        }

        private static float ThirtyHeight() => 34f;
    }
}
