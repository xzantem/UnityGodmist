using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Configuration;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class GameDefinesWindow : EditorWindow
    {
        private const string AssetPath = "Assets/_Project/Resources/GameDefines.asset";
        private const string FolderPath = "Assets/_Project/Resources";

        private GameDefines definitions;
        private SerializedObject serializedDefinitions;
        private string searchQuery = string.Empty;
        private int categoryIndex;
        private int selectedIndex = -1;
        private Vector2 listScroll;
        private Vector2 detailsScroll;

        private static readonly string[] CategoryLabels =
        {
            "Wszystkie", "Economy", "Combat", "Items", "Crafting",
            "Upgrade / Reforge", "Progression", "Exploration"
        };

        [MenuItem("Godmist/Content/Game Defines Window")]
        private static void OpenWindow()
        {
            var window = GetWindow<GameDefinesWindow>("Game Defines");
            window.minSize = new Vector2(1120f, 680f);
            window.EnsureAsset();
            window.Show();
        }

        [MenuItem("Godmist/Content/Create or Repair GameDefines Asset")]
        private static void CreateOrRepairAsset()
        {
            var window = GetWindow<GameDefinesWindow>("Game Defines");
            window.EnsureAsset();
            window.Show();
        }

        private void OnEnable()
        {
            EnsureAsset();
        }

        private void OnGUI()
        {
            EnsureAsset();
            DrawToolbar();
            if (definitions == null)
            {
                EditorGUILayout.HelpBox("Nie udało się utworzyć GameDefines.asset.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawEntryList();
            DrawEntryDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Odśwież", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                EnsureAsset(forceReload: true);
            }

            if (GUILayout.Button("Dodaj parametr", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                AddEntry();
            }

            if (GUILayout.Button("Przywróć brakujące domyślne", EditorStyles.toolbarButton, GUILayout.Width(190f)))
            {
                RestoreMissingDefaults();
            }

            GUILayout.FlexibleSpace();
            var errors = definitions?.Validate() ?? Array.Empty<string>();
            EditorGUILayout.LabelField(
                errors.Count == 0 ? "Valid" : $"Błędy: {errors.Count}",
                errors.Count == 0 ? EditorStyles.miniLabel : EditorStyles.boldLabel,
                GUILayout.Width(100f));
            using (new EditorGUI.DisabledScope(errors.Count > 0))
            {
                if (GUILayout.Button("Zapisz", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    SaveAsset();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntryList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(470f));
            EditorGUILayout.BeginHorizontal();
            searchQuery = EditorGUILayout.TextField("Szukaj", searchQuery);
            categoryIndex = EditorGUILayout.Popup(categoryIndex, CategoryLabels, GUILayout.Width(150f));
            EditorGUILayout.EndHorizontal();

            var visible = GetVisibleEntries();
            EditorGUILayout.LabelField($"Parametry: {visible.Count}/{definitions.Entries.Count}", EditorStyles.miniLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            foreach (var pair in visible)
            {
                var entry = pair.Entry;
                var label = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? entry.Id
                    : $"{entry.DisplayName}  |  {entry.Id}";
                if (GUILayout.Toggle(selectedIndex == pair.Index, label, "Button"))
                {
                    selectedIndex = pair.Index;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntryDetails()
        {
            EditorGUILayout.BeginVertical();
            if (selectedIndex < 0 || selectedIndex >= definitions.Entries.Count
                || definitions.Entries[selectedIndex] == null)
            {
                EditorGUILayout.HelpBox("Wybierz parametr z listy.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var entry = definitions.Entries[selectedIndex];
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            EditorGUILayout.LabelField("Parametr gameplayowy", EditorStyles.boldLabel);
            serializedDefinitions.Update();
            var entriesProperty = serializedDefinitions.FindProperty("entries");
            var entryProperty = entriesProperty?.GetArrayElementAtIndex(selectedIndex);
            if (entryProperty != null)
            {
                EditorGUILayout.PropertyField(entryProperty, GUIContent.none, true);
            }
            serializedDefinitions.ApplyModifiedProperties();
            entry.Normalize();
            EditorUtility.SetDirty(definitions);

            EditorGUILayout.Space(8f);
            var entryError = entry.TryValidate(out var error) ? null : error;
            if (entryError != null) EditorGUILayout.HelpBox(entryError, MessageType.Error);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplikuj")) DuplicateEntry(entry);
            using (new EditorGUI.DisabledScope(definitions.Entries.Count <= 1))
            {
                if (GUILayout.Button("Usuń")) RemoveEntry();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private List<(int Index, GameDefineEntry Entry)> GetVisibleEntries()
        {
            var result = new List<(int, GameDefineEntry)>();
            if (definitions?.Entries == null) return result;
            var query = searchQuery?.Trim() ?? string.Empty;

            for (var index = 0; index < definitions.Entries.Count; index++)
            {
                var entry = definitions.Entries[index];
                if (entry == null) continue;
                if (categoryIndex > 0 && (int)entry.Category != categoryIndex - 1) continue;
                if (query.Length > 0
                    && (entry.Id?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) < 0
                    && (entry.DisplayName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                {
                    continue;
                }
                result.Add((index, entry));
            }
            return result;
        }

        private void AddEntry()
        {
            Undo.RecordObject(definitions, "Add game define");
            definitions.AddEntry(new GameDefineEntry
            {
                Id = "new.define",
                DisplayName = "New Define",
                Description = "",
                Category = GameDefineCategory.Combat,
                Unit = "",
                ValueType = GameDefineValueType.Float,
                DefaultFloatValue = 0f,
                CurrentFloatValue = 0f,
                MinimumFloatValue = 0f,
                MaximumFloatValue = 1f
            });
            selectedIndex = definitions.Entries.Count - 1;
            EditorUtility.SetDirty(definitions);
        }

        private void DuplicateEntry(GameDefineEntry source)
        {
            Undo.RecordObject(definitions, "Duplicate game define");
            var copy = new GameDefineEntry
            {
                Id = source.Id + ".copy",
                DisplayName = source.DisplayName + " Copy",
                Description = source.Description,
                Category = source.Category,
                Unit = source.Unit,
                ValueType = source.ValueType,
                DefaultIntegerValue = source.DefaultIntegerValue,
                CurrentIntegerValue = source.CurrentIntegerValue,
                MinimumIntegerValue = source.MinimumIntegerValue,
                MaximumIntegerValue = source.MaximumIntegerValue,
                DefaultFloatValue = source.DefaultFloatValue,
                CurrentFloatValue = source.CurrentFloatValue,
                MinimumFloatValue = source.MinimumFloatValue,
                MaximumFloatValue = source.MaximumFloatValue,
                DefaultBooleanValue = source.DefaultBooleanValue,
                CurrentBooleanValue = source.CurrentBooleanValue,
                DefaultStringValue = source.DefaultStringValue,
                CurrentStringValue = source.CurrentStringValue
            };
            definitions.AddEntry(copy);
            selectedIndex = definitions.Entries.Count - 1;
            EditorUtility.SetDirty(definitions);
        }

        private void RemoveEntry()
        {
            if (selectedIndex < 0 || selectedIndex >= definitions.Entries.Count) return;
            Undo.RecordObject(definitions, "Remove game define");
            definitions.RemoveEntryAt(selectedIndex);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, definitions.Entries.Count - 1);
            EditorUtility.SetDirty(definitions);
        }

        private void RestoreMissingDefaults()
        {
            Undo.RecordObject(definitions, "Restore game define defaults");
            var ids = new HashSet<string>(definitions.Entries.Where(entry => entry != null).Select(entry => entry.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in GameDefineDefaults.CreateEntries())
            {
                if (!ids.Add(entry.Id)) continue;
                definitions.AddEntry(entry);
            }
            EditorUtility.SetDirty(definitions);
        }

        private void EnsureAsset(bool forceReload = false)
        {
            if (!forceReload && definitions != null) return;
            definitions = AssetDatabase.LoadAssetAtPath<GameDefines>(AssetPath);
            if (definitions != null)
            {
                serializedDefinitions = new SerializedObject(definitions);
                return;
            }

            EnsureFolder(FolderPath);
            definitions = CreateInstance<GameDefines>();
            AssetDatabase.CreateAsset(definitions, AssetPath);
            serializedDefinitions = new SerializedObject(definitions);
            RestoreMissingDefaults();
            SaveAsset();
        }

        private void SaveAsset()
        {
            if (definitions == null) return;
            serializedDefinitions?.ApplyModifiedProperties();
            EditorUtility.SetDirty(definitions);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        }
    }
}
