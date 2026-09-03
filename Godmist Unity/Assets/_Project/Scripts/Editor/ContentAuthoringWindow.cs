using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class ContentAuthoringWindow : EditorWindow
    {
        private enum ContentDomain
        {
            BaseItems,
            Consumables,
            CraftableItems,
            Equipment,
            EquipmentParts,
            Skills,
            SkillTrees,
            Characters,
            Encounters,
            Dungeons
        }

        private enum Preset
        {
            EmptyItem,
            Potion,
            Bomb,
            Utility,
            MaterialRecipe,
            PartyWeapon,
            PartyArmor,
            EmptyEquipmentPart,
            EmptySkill,
            EmptySkillTree,
            EmptyCharacter,
            EmptyEncounter,
            EmptyDungeon
        }

        [Flags]
        private enum TableColumns
        {
            Id = 1 << 0,
            Name = 1 << 1,
            Type = 1 << 2,
            Category = 1 << 3,
            Level = 1 << 4,
            Value = 1 << 5,
            Status = 1 << 6
        }

        private const string AllCategory = "all";
        private const string ItemsFolder = "Assets/_Project/ScriptableObjects/Items";
        private const string ConsumablesFolder = ItemsFolder + "/Consumables";
        private const string EquipmentFolder = "Assets/_Project/ScriptableObjects/Equipment";
        private const string SkillsFolder = "Assets/_Project/ScriptableObjects/Skills";
        private const string SkillTreesFolder = SkillsFolder + "/SkillTrees";
        private const string CharactersFolder = "Assets/_Project/ScriptableObjects/Characters";
        private const string EncountersFolder = "Assets/_Project/ScriptableObjects/Encounters";
        private const string DungeonsFolder = "Assets/_Project/ScriptableObjects/Dungeons";

        private static readonly string[] DomainLabels =
        {
            "Base Items", "Consumables", "Craftable Items", "Equipment", "Equipment Parts",
            "Skills", "Skill Trees", "Characters", "Encounters", "Dungeons"
        };

        private const TableColumns DefaultColumns =
            TableColumns.Id | TableColumns.Name | TableColumns.Type
            | TableColumns.Category | TableColumns.Level | TableColumns.Value
            | TableColumns.Status;

        private GameContentCatalog activeCatalog;
        private UnityEngine.Object selectedAsset;
        private SerializedObject serializedAsset;
        private UnityEditor.Editor selectedAssetEditor;
        private string searchQuery = string.Empty;
        private string categoryFilter = AllCategory;
        private ContentDomain activeDomain;
        private TableColumns visibleColumns = DefaultColumns;
        private Vector2 assetScroll;
        private Vector2 inspectorScroll;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;
        private List<UnityEngine.Object> allAssets = new();

        [MenuItem("Godmist/Content/Content Authoring Window")]
        private static void OpenFromMenu()
        {
            var window = GetWindow<ContentAuthoringWindow>("Content Authoring");
            window.minSize = new Vector2(1180f, 680f);
            window.RefreshAssets();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshAssets();
            Selection.selectionChanged += HandleSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= HandleSelectionChanged;
            DisposeSelectedAssetEditor();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawDomainTabs();
            DrawFilters();

            EditorGUILayout.BeginHorizontal();
            DrawAssetTable();
            DrawAssetEditor();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            activeCatalog = (GameContentCatalog)EditorGUILayout.ObjectField(
                new GUIContent("Aktywny katalog"), activeCatalog, typeof(GameContentCatalog), false,
                GUILayout.Width(330f));
            if (EditorGUI.EndChangeCheck()) Repaint();

            if (GUILayout.Button("Odśwież", EditorStyles.toolbarButton, GUILayout.Width(72f))) RefreshAssets();
            using (new EditorGUI.DisabledScope(activeCatalog == null))
            {
                if (GUILayout.Button("Nowy", EditorStyles.toolbarButton, GUILayout.Width(58f))) ShowCreateMenu();
                using (new EditorGUI.DisabledScope(selectedAsset == null))
                {
                    if (GUILayout.Button("Duplikuj", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        DuplicateSelectedAsset();
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Kolumny", EditorStyles.toolbarButton, GUILayout.Width(70f))) ShowColumnMenu();
            using (new EditorGUI.DisabledScope(activeCatalog == null))
            {
                if (GUILayout.Button("Dodaj widoczne", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                    AddVisibleAssetsToCatalog();
            }
            EditorGUILayout.EndHorizontal();

            if (activeCatalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Wybierz aktywny GameContentCatalog, aby tworzyć, duplikować i zapisywać content.",
                    MessageType.Info);
            }
        }

        private void DrawDomainTabs()
        {
            var nextDomain = GUILayout.Toolbar((int)activeDomain, DomainLabels, EditorStyles.toolbarButton);
            if (nextDomain == (int)activeDomain) return;
            activeDomain = (ContentDomain)nextDomain;
            categoryFilter = AllCategory;
            assetScroll = Vector2.zero;
            ClearSelectedAsset();
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            searchQuery = EditorGUILayout.TextField(new GUIContent("Szukaj"), searchQuery, GUILayout.MinWidth(260f));
            var categories = GetCategoryOptions();
            var categoryIndex = Mathf.Max(0, Array.IndexOf(
                categories.Select(option => option.Value).ToArray(), categoryFilter));
            var nextCategoryIndex = EditorGUILayout.Popup(
                new GUIContent("Kategoria"), categoryIndex,
                categories.Select(option => option.Label).ToArray(), GUILayout.Width(320f));
            categoryFilter = categories[nextCategoryIndex].Value;
            EditorGUILayout.LabelField(
                $"{GetDomainLabel(activeDomain)}: {GetVisibleAssets().Count} assetów",
                EditorStyles.miniLabel, GUILayout.Width(240f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetTable()
        {
            var visibleAssets = GetVisibleAssets();
            EditorGUILayout.BeginVertical(GUILayout.Width(620f));
            EditorGUILayout.LabelField("Tabela contentu", EditorStyles.boldLabel);
            DrawTableHeader();
            assetScroll = EditorGUILayout.BeginScrollView(assetScroll, true, false);
            foreach (var asset in visibleAssets) DrawTableRow(asset);
            if (visibleAssets.Count == 0)
            {
                EditorGUILayout.HelpBox("Brak assetów pasujących do zakładki i filtrów.", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTableHeader()
        {
            var rect = GUILayoutUtility.GetRect(
                GetTableWidth(),
                EditorGUIUtility.singleLineHeight + 6f);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            DrawCells(rect, null, true);
        }

        private void DrawTableRow(UnityEngine.Object asset)
        {
            var rect = GUILayoutUtility.GetRect(GetTableWidth(), EditorGUIUtility.singleLineHeight + 8f);
            var isSelected = ReferenceEquals(asset, selectedAsset);
            var isAssigned = activeCatalog != null && IsAssignedToCatalog(asset);
            EditorGUI.DrawRect(rect, isSelected ? new Color(0.22f, 0.42f, 0.68f) : new Color(0.12f, 0.12f, 0.12f));
            DrawCells(rect, asset, false);
            if (isAssigned)
            {
                EditorGUI.LabelField(new Rect(rect.xMax - 20f, rect.y, 18f, rect.height), "✓", EditorStyles.boldLabel);
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                SetSelectedAsset(asset);
                Event.current.Use();
            }
        }

        private void DrawCells(Rect rowRect, UnityEngine.Object asset, bool header)
        {
            var x = rowRect.x + 6f;
            foreach (var column in GetActiveColumns())
            {
                var width = GetColumnWidth(column);
                var cell = new Rect(x, rowRect.y + 2f, width - 6f, rowRect.height - 4f);
                EditorGUI.LabelField(cell, header ? GetColumnLabel(column) : GetCellValue(asset, column),
                    header ? EditorStyles.boldLabel : EditorStyles.miniLabel);
                x += width;
            }
        }

        private void DrawAssetEditor()
        {
            EditorGUILayout.BeginVertical();
            if (selectedAsset == null)
            {
                EditorGUILayout.HelpBox("Wybierz asset z tabeli albo utwórz nowy z przycisku Nowy.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            EnsureSelectedAssetEditor();
            EditorGUILayout.LabelField($"Edycja: {GetAssetLabel(selectedAsset)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(selectedAsset), EditorStyles.miniLabel);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            selectedAssetEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
            serializedAsset = selectedAssetEditor.serializedObject;
            DrawValidationAndActions();
            EditorGUILayout.EndVertical();
        }

        private void EnsureSelectedAssetEditor()
        {
            if (selectedAssetEditor != null && selectedAssetEditor.target == selectedAsset)
            {
                serializedAsset = selectedAssetEditor.serializedObject;
                return;
            }

            DisposeSelectedAssetEditor();
            selectedAssetEditor = UnityEditor.Editor.CreateEditor(selectedAsset);
            serializedAsset = selectedAssetEditor?.serializedObject;
        }

        private void DrawValidationAndActions()
        {
            var report = ContentAuthoringValidator.Validate(selectedAsset, activeCatalog);
            foreach (var error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var warning in report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            var catalogBacked = IsCatalogBackedAsset(selectedAsset);
            using (new EditorGUI.DisabledScope(!report.IsValid || activeCatalog == null))
            {
                var label = catalogBacked ? "Zapisz i przypisz do katalogu" : "Zapisz asset";
                if (GUILayout.Button(label, GUILayout.Height(28f))) SaveSelectedAsset();
            }
            using (new EditorGUI.DisabledScope(!catalogBacked || activeCatalog == null || IsAssignedToCatalog(selectedAsset)
                                               || !report.IsValid))
            {
                if (GUILayout.Button("Przypisz", GUILayout.Width(100f), GUILayout.Height(28f))) AssignSelectedAsset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ShowColumnMenu()
        {
            var menu = new GenericMenu();
            AddColumnMenuItem(menu, TableColumns.Id, "ID");
            AddColumnMenuItem(menu, TableColumns.Name, "Nazwa");
            AddColumnMenuItem(menu, TableColumns.Type, "Typ");
            AddColumnMenuItem(menu, TableColumns.Category, "Kategoria");
            AddColumnMenuItem(menu, TableColumns.Level, "Level / bracket");
            AddColumnMenuItem(menu, TableColumns.Value, "Wartość / parametry");
            AddColumnMenuItem(menu, TableColumns.Status, "Status katalogu");
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Przywróć wszystkie"), false, () =>
            {
                visibleColumns = DefaultColumns;
                Repaint();
            });
            menu.ShowAsContext();
        }

        private void AddColumnMenuItem(GenericMenu menu, TableColumns column, string label)
        {
            menu.AddItem(new GUIContent(label), visibleColumns.HasFlag(column), () =>
            {
                var next = visibleColumns ^ column;
                if ((next & (TableColumns.Id | TableColumns.Name)) == 0) next |= TableColumns.Name;
                visibleColumns = next;
                Repaint();
            });
        }

        private void ShowCreateMenu()
        {
            var menu = new GenericMenu();
            AddCreateItem(menu, "Pusty ItemDefinition", Preset.EmptyItem, ItemsFolder);
            menu.AddSeparator(string.Empty);
            AddCreateItem(menu, "Consumable / Potion", Preset.Potion, ConsumablesFolder);
            AddCreateItem(menu, "Consumable / Bomb", Preset.Bomb, ConsumablesFolder);
            AddCreateItem(menu, "Consumable / Utility", Preset.Utility, ConsumablesFolder);
            AddCreateItem(menu, "Craftable / Material Recipe", Preset.MaterialRecipe, ItemsFolder);
            menu.AddSeparator(string.Empty);
            AddCreateItem(menu, "Equipment / Party Weapon", Preset.PartyWeapon, EquipmentFolder);
            AddCreateItem(menu, "Equipment / Party Armor", Preset.PartyArmor, EquipmentFolder);
            AddCreateItem(menu, "Equipment Part / Empty", Preset.EmptyEquipmentPart, EquipmentFolder);
            menu.AddSeparator(string.Empty);
            AddCreateItem(menu, "Skill / Empty", Preset.EmptySkill, SkillsFolder);
            AddCreateItem(menu, "Skill Tree / Empty", Preset.EmptySkillTree, SkillTreesFolder);
            AddCreateItem(menu, "Character / Empty", Preset.EmptyCharacter, CharactersFolder);
            AddCreateItem(menu, "Encounter / Empty", Preset.EmptyEncounter, EncountersFolder);
            AddCreateItem(menu, "Dungeon / Empty", Preset.EmptyDungeon, DungeonsFolder);
            menu.ShowAsContext();
        }

        private static void AddCreateItem(GenericMenu menu, string label, Preset preset, string folder)
        {
            menu.AddItem(new GUIContent(label), false, () => CreateAsset(preset, folder));
        }

        private static void CreateAsset(Preset preset, string folder)
        {
            GetWindow<ContentAuthoringWindow>().CreateAssetInternal(preset, folder);
        }

        private void CreateAssetInternal(Preset preset, string folder)
        {
            if (activeCatalog == null)
            {
                SetStatus("Najpierw wybierz aktywny GameContentCatalog.", MessageType.Error);
                return;
            }
            EnsureFolder(folder);
            var defaultName = GetPresetName(preset);
            var path = EditorUtility.SaveFilePanelInProject(
                "Utwórz content", defaultName, "asset", "Wybierz miejsce dla nowego assetu.", folder);
            if (string.IsNullOrWhiteSpace(path)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var created = ScriptableObject.CreateInstance(GetPresetType(preset));
            AssetDatabase.CreateAsset(created, path);
            ApplyPreset(created, preset, path);
            AssetDatabase.ImportAsset(path);
            SetSelectedAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
            SetStatus("Utworzono asset. Zostanie przypisany do katalogu dopiero po użyciu zapisu.", MessageType.Info);
            RefreshAssets();
        }

        private void DuplicateSelectedAsset()
        {
            if (activeCatalog == null || selectedAsset == null) return;
            var sourcePath = AssetDatabase.GetAssetPath(selectedAsset);
            if (string.IsNullOrWhiteSpace(sourcePath)) return;
            var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? ItemsFolder;
            var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            var targetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{sourceName}_Copy.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                SetStatus("Nie udało się zduplikować assetu.", MessageType.Error);
                return;
            }
            AssetDatabase.ImportAsset(targetPath);
            var sourceId = GetId(selectedAsset);
            var sourceDisplayName = GetDisplayName(selectedAsset);
            var duplicated = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
            SetSelectedAsset(duplicated);
            SetUniqueIdentity(duplicated, sourceId + ".copy", sourceDisplayName + " Copy");
            SetStatus("Zduplikowano asset. Zapisz go, aby przypisać go do aktywnego katalogu.", MessageType.Info);
            RefreshAssets();
        }

        private void SaveSelectedAsset()
        {
            if (selectedAsset == null) return;
            if (activeCatalog == null)
            {
                SetStatus("Najpierw wybierz aktywny GameContentCatalog.", MessageType.Error);
                return;
            }
            serializedAsset?.ApplyModifiedProperties();
            var report = ContentAuthoringValidator.Validate(selectedAsset, activeCatalog);
            if (!report.IsValid)
            {
                SetStatus("Nie można zapisać assetu z błędami walidacji.", MessageType.Error);
                return;
            }
            Undo.RecordObject(selectedAsset, "Save content asset");
            AssignToCatalog(selectedAsset);
            EditorUtility.SetDirty(selectedAsset);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            SetStatus("Asset zapisany i przypisany do aktywnego katalogu.", MessageType.Info);
        }

        private void AssignSelectedAsset()
        {
            if (selectedAsset == null || activeCatalog == null) return;
            if (!ContentAuthoringValidator.Validate(selectedAsset, activeCatalog).IsValid)
            {
                SetStatus("Nie można przypisać assetu z błędami walidacji.", MessageType.Error);
                return;
            }
            AssignToCatalog(selectedAsset);
            AssetDatabase.SaveAssets();
            SetStatus("Asset przypisany do aktywnego katalogu.", MessageType.Info);
        }

        private void AddVisibleAssetsToCatalog()
        {
            if (activeCatalog == null)
            {
                SetStatus("Najpierw wybierz aktywny GameContentCatalog.", MessageType.Error);
                return;
            }
            var added = 0;
            var skipped = 0;
            foreach (var asset in GetVisibleAssets())
            {
                if (!IsCatalogBackedAsset(asset)) continue;
                if (!ContentAuthoringValidator.Validate(asset, activeCatalog).IsValid)
                {
                    skipped++;
                    continue;
                }
                if (!IsAssignedToCatalog(asset))
                {
                    AssignToCatalog(asset);
                    added++;
                }
            }
            AssetDatabase.SaveAssets();
            SetStatus($"Dodano do katalogu: {added}. Pominięto z błędami: {skipped}.", MessageType.Info);
            Repaint();
        }

        private void AssignToCatalog(UnityEngine.Object asset)
        {
            if (activeCatalog == null || asset == null) return;
            Undo.RecordObject(activeCatalog, "Assign content asset to catalog");
            switch (asset)
            {
                case ItemDefinition item when !activeCatalog.ItemDefinitions.Contains(item):
                    activeCatalog.ItemDefinitions.Add(item); break;
                case EquipmentPartDefinition part when !activeCatalog.EquipmentPartDefinitions.Contains(part):
                    activeCatalog.EquipmentPartDefinitions.Add(part); break;
                case SkillData skill when !activeCatalog.SkillDefinitions.Contains(skill):
                    activeCatalog.SkillDefinitions.Add(skill); break;
                case CharacterDefinition character when !activeCatalog.CharacterDefinitions.Contains(character):
                    activeCatalog.CharacterDefinitions.Add(character); break;
                case EncounterDefinition encounter when !activeCatalog.EncounterDefinitions.Contains(encounter):
                    activeCatalog.EncounterDefinitions.Add(encounter); break;
                case DungeonDefinition dungeon when !activeCatalog.DungeonDefinitions.Contains(dungeon):
                    activeCatalog.DungeonDefinitions.Add(dungeon); break;
            }
            EditorUtility.SetDirty(activeCatalog);
        }

        private bool IsAssignedToCatalog(UnityEngine.Object asset)
        {
            if (activeCatalog == null || asset == null) return false;
            return asset switch
            {
                ItemDefinition item => activeCatalog.ItemDefinitions.Contains(item),
                EquipmentPartDefinition part => activeCatalog.EquipmentPartDefinitions.Contains(part),
                SkillData skill => activeCatalog.SkillDefinitions.Contains(skill),
                CharacterDefinition character => activeCatalog.CharacterDefinitions.Contains(character),
                EncounterDefinition encounter => activeCatalog.EncounterDefinitions.Contains(encounter),
                DungeonDefinition dungeon => activeCatalog.DungeonDefinitions.Contains(dungeon),
                _ => false
            };
        }

        private void RefreshAssets()
        {
            var assets = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
            AddAssetsBySearch("t:ItemDefinition", assets);
            AddAssetsBySearch("t:EquipmentPartDefinition", assets);
            AddAssetsBySearch("t:SkillData", assets);
            AddAssetsBySearch("t:SkillTreeDefinition", assets);
            AddAssetsBySearch("t:CharacterDefinition", assets);
            AddAssetsBySearch("t:EncounterDefinition", assets);
            AddAssetsBySearch("t:DungeonDefinition", assets);
            allAssets = assets.Values.Where(IsSupportedAsset).OrderBy(GetAssetLabel, StringComparer.OrdinalIgnoreCase).ToList();
            if (selectedAsset != null && !allAssets.Contains(selectedAsset))
            {
                ClearSelectedAsset();
            }
            Repaint();
        }

        private static void AddAssetsBySearch(string filter, Dictionary<string, UnityEngine.Object> assets)
        {
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null) assets[path] = asset;
            }
        }

        private List<UnityEngine.Object> GetVisibleAssets()
        {
            return allAssets.Where(asset => GetDomain(asset) == activeDomain)
                .Where(MatchesCategoryFilter).Where(MatchesSearch)
                .OrderBy(GetAssetLabel, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private bool MatchesCategoryFilter(UnityEngine.Object asset)
        {
            if (categoryFilter == AllCategory) return true;
            return asset switch
            {
                ItemDefinition item when categoryFilter == $"item:{item.ItemType}" => true,
                ConsumableItemDefinition consumable when categoryFilter == $"consumable:{consumable.UseType}" => true,
                CraftableItemDefinition craftable when categoryFilter == $"discipline:{craftable.CraftingDiscipline}" => true,
                EquipmentPartDefinition part when categoryFilter == $"part:{part.Kind}" => true,
                SkillData skill when categoryFilter == $"resource:{skill.ResourceType}" => true,
                SkillData skill when categoryFilter == "skill:toggle" => skill.IsToggle,
                SkillData skill when categoryFilter == "skill:normal" => !skill.IsToggle,
                CharacterDefinition character when categoryFilter == "character:main" => character.IsMainCharacter,
                CharacterDefinition character when categoryFilter == "character:recruitable" => !character.IsMainCharacter,
                CharacterDefinition character when categoryFilter == $"ai:{character.AiRole}" => true,
                EncounterDefinition encounter when categoryFilter == "encounter:boss" => encounter.Enemies != null
                    && encounter.Enemies.Count > 0
                    && encounter.BossEnemyIndex < encounter.Enemies.Count,
                DungeonDefinition dungeon when categoryFilter == "dungeon:procedural" => dungeon.IsProcedural,
                DungeonDefinition dungeon when categoryFilter == "dungeon:story" => dungeon.IsStoryDungeon,
                DungeonDefinition dungeon when categoryFilter == $"theme:{dungeon.Theme}" => true,
                _ => false
            };
        }

        private bool MatchesSearch(UnityEngine.Object asset)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return true;
            var query = searchQuery.Trim();
            return GetAssetLabel(asset).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || AssetDatabase.GetAssetPath(asset).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<CategoryOption> GetCategoryOptions()
        {
            var options = new List<CategoryOption> { new("Wszystkie", AllCategory) };
            switch (activeDomain)
            {
                case ContentDomain.BaseItems:
                    options.AddRange(Enum.GetValues(typeof(ItemType)).Cast<ItemType>()
                        .Select(value => new CategoryOption($"ItemType: {value}", $"item:{value}"))); break;
                case ContentDomain.Consumables:
                    options.AddRange(Enum.GetValues(typeof(ConsumableUseType)).Cast<ConsumableUseType>()
                        .Select(value => new CategoryOption($"Consumable: {value}", $"consumable:{value}"))); break;
                case ContentDomain.CraftableItems:
                    options.AddRange(Enum.GetValues(typeof(MaterialDiscipline)).Cast<MaterialDiscipline>()
                        .Where(value => value != MaterialDiscipline.None)
                        .Select(value => new CategoryOption($"Dyscyplina: {value}", $"discipline:{value}"))); break;
                case ContentDomain.EquipmentParts:
                    options.AddRange(Enum.GetValues(typeof(EquipmentPartKind)).Cast<EquipmentPartKind>()
                        .Select(value => new CategoryOption($"Część: {value}", $"part:{value}"))); break;
                case ContentDomain.Skills:
                    options.AddRange(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                        .Select(value => new CategoryOption($"Resource: {value}", $"resource:{value}")));
                    options.Add(new CategoryOption("Toggle", "skill:toggle"));
                    options.Add(new CategoryOption("Normalne", "skill:normal")); break;
                case ContentDomain.SkillTrees:
                    break;
                case ContentDomain.Characters:
                    options.Add(new CategoryOption("Główne postacie", "character:main"));
                    options.Add(new CategoryOption("Pozostałe postacie", "character:recruitable"));
                    options.AddRange(Enum.GetValues(typeof(CombatAiRole)).Cast<CombatAiRole>()
                        .Select(value => new CategoryOption($"AI: {value}", $"ai:{value}"))); break;
                case ContentDomain.Encounters:
                    options.Add(new CategoryOption("Boss", "encounter:boss")); break;
                case ContentDomain.Dungeons:
                    options.Add(new CategoryOption("Procedural", "dungeon:procedural"));
                    options.Add(new CategoryOption("Story", "dungeon:story"));
                    options.AddRange(Enum.GetValues(typeof(DungeonTheme)).Cast<DungeonTheme>()
                        .Select(value => new CategoryOption($"Theme: {value}", $"theme:{value}"))); break;
            }
            return options;
        }

        private void SetSelectedAsset(UnityEngine.Object asset)
        {
            if (ReferenceEquals(selectedAsset, asset))
            {
                Repaint();
                return;
            }

            DisposeSelectedAssetEditor();
            selectedAsset = asset;
            serializedAsset = asset == null ? null : new SerializedObject(asset);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            Repaint();
        }

        private void HandleSelectionChanged()
        {
            if (Selection.activeObject is not UnityEngine.Object asset || !IsSupportedAsset(asset)) return;
            DisposeSelectedAssetEditor();
            selectedAsset = asset;
            activeDomain = GetDomain(asset);
            serializedAsset = new SerializedObject(asset);
            Repaint();
        }

        private void ClearSelectedAsset()
        {
            DisposeSelectedAssetEditor();
            selectedAsset = null;
            serializedAsset = null;
        }

        private void DisposeSelectedAssetEditor()
        {
            if (selectedAssetEditor == null) return;
            DestroyImmediate(selectedAssetEditor);
            selectedAssetEditor = null;
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }

        private static bool IsSupportedAsset(UnityEngine.Object asset) =>
            asset is ItemDefinition || asset is EquipmentPartDefinition || asset is SkillData
            || asset is SkillTreeDefinition || asset is CharacterDefinition
            || asset is EncounterDefinition || asset is DungeonDefinition;

        private static bool IsCatalogBackedAsset(UnityEngine.Object asset) =>
            asset is not SkillTreeDefinition;

        private static ContentDomain GetDomain(UnityEngine.Object asset)
        {
            return asset switch
            {
                ConsumableItemDefinition => ContentDomain.Consumables,
                CraftableItemDefinition => ContentDomain.CraftableItems,
                EquipmentItemDefinition => ContentDomain.Equipment,
                ItemDefinition => ContentDomain.BaseItems,
                EquipmentPartDefinition => ContentDomain.EquipmentParts,
                SkillData => ContentDomain.Skills,
                SkillTreeDefinition => ContentDomain.SkillTrees,
                CharacterDefinition => ContentDomain.Characters,
                EncounterDefinition => ContentDomain.Encounters,
                DungeonDefinition => ContentDomain.Dungeons,
                _ => ContentDomain.BaseItems
            };
        }

        private static string GetDomainLabel(ContentDomain domain) => DomainLabels[(int)domain];

        private static string GetAssetLabel(UnityEngine.Object asset)
        {
            var id = GetId(asset);
            var displayName = GetDisplayName(asset);
            return string.IsNullOrWhiteSpace(id) ? displayName : $"{id} — {displayName}";
        }

        private static string GetId(UnityEngine.Object asset)
        {
            return asset switch
            {
                ItemDefinition item => item.Id,
                EquipmentPartDefinition part => part.Id,
                SkillData skill => skill.SkillId,
                SkillTreeDefinition tree => tree.Id,
                CharacterDefinition character => character.Id,
                EncounterDefinition encounter => encounter.Id,
                DungeonDefinition dungeon => dungeon.Id,
                _ => string.Empty
            } ?? string.Empty;
        }

        private static string GetDisplayName(UnityEngine.Object asset)
        {
            return asset switch
            {
                ItemDefinition item => item.DisplayName,
                EquipmentPartDefinition part => part.DisplayName,
                SkillData skill => skill.DisplayName,
                SkillTreeDefinition tree => tree.DisplayName,
                CharacterDefinition character => character.DisplayName,
                EncounterDefinition encounter => encounter.DisplayName,
                DungeonDefinition dungeon => dungeon.DisplayName,
                _ => asset?.name ?? string.Empty
            } ?? string.Empty;
        }

        private static string GetColumnLabel(TableColumns column)
        {
            return column switch
            {
                TableColumns.Id => "ID", TableColumns.Name => "Nazwa", TableColumns.Type => "Typ",
                TableColumns.Category => "Kategoria", TableColumns.Level => "Level / bracket",
                TableColumns.Value => "Wartość / parametry", TableColumns.Status => "Katalog",
                _ => string.Empty
            };
        }

        private static float GetColumnWidth(TableColumns column)
        {
            return column switch
            {
                TableColumns.Id => 180f, TableColumns.Name => 150f, TableColumns.Type => 105f,
                TableColumns.Category => 125f, TableColumns.Level => 100f,
                TableColumns.Value => 150f, TableColumns.Status => 70f, _ => 100f
            };
        }

        private static string GetCellValue(UnityEngine.Object asset, TableColumns column)
        {
            if (asset == null) return string.Empty;
            return column switch
            {
                TableColumns.Id => GetId(asset), TableColumns.Name => GetDisplayName(asset),
                TableColumns.Type => GetTypeLabel(asset), TableColumns.Category => GetCategoryLabel(asset),
                TableColumns.Level => GetLevelLabel(asset), TableColumns.Value => GetValueLabel(asset),
                TableColumns.Status => string.Empty, _ => string.Empty
            };
        }

        private static string GetTypeLabel(UnityEngine.Object asset)
        {
            return asset switch
            {
                ConsumableItemDefinition => "Consumable", CraftableItemDefinition => "Craftable",
                EquipmentItemDefinition => "Equipment", ItemDefinition => "Item",
                EquipmentPartDefinition => "Part", SkillData => "Skill", SkillTreeDefinition => "Skill Tree",
                CharacterDefinition => "Character",
                EncounterDefinition => "Encounter", DungeonDefinition => "Dungeon", _ => asset.GetType().Name
            };
        }

        private static string GetCategoryLabel(UnityEngine.Object asset)
        {
            return asset switch
            {
                ItemDefinition item => item.ItemType.ToString(),
                EquipmentPartDefinition part => part.Kind.ToString(),
                SkillData skill => skill.IsToggle ? "Toggle" : skill.ResourceType.ToString(),
                SkillTreeDefinition => "Passive nodes",
                CharacterDefinition character => character.IsMainCharacter ? "Main" : character.AiRole.ToString(),
                EncounterDefinition encounter => $"{encounter.GridWidth}x{encounter.GridHeight}",
                DungeonDefinition dungeon => dungeon.Theme.ToString(), _ => string.Empty
            };
        }

        private static string GetLevelLabel(UnityEngine.Object asset)
        {
            return asset switch
            {
                EquipmentPartDefinition part => part.LevelBracket.ToString(),
                DungeonDefinition dungeon => $"Lv {dungeon.DungeonLevel}", _ => string.Empty
            };
        }

        private static string GetValueLabel(UnityEngine.Object asset)
        {
            return asset switch
            {
                CraftableItemDefinition craftable => $"x{craftable.OutputAmount} / {craftable.CraftingDiscipline}",
                ItemDefinition item => $"{item.BaseValue}g / {item.Weight:0.##}",
                EquipmentPartDefinition part => part.RequiredClass == null ? "brak klasy" : part.RequiredClass.Id,
                SkillData skill => $"AP {skill.ActionCost} / R {skill.ResourceCost}",
                SkillTreeDefinition tree => $"{tree.Nodes?.Count ?? 0} nodes",
                CharacterDefinition character => $"XP {character.ExperienceReward} / G {character.GoldReward}",
                EncounterDefinition encounter => $"{encounter.Enemies?.Count ?? 0} enemy",
                DungeonDefinition dungeon => $"{dungeon.MinFloorCount}-{dungeon.MaxFloorCount} floor",
                _ => string.Empty
            };
        }

        private static Type GetPresetType(Preset preset)
        {
            return preset switch
            {
                Preset.EmptyEquipmentPart => typeof(EquipmentPartDefinition),
                Preset.Potion or Preset.Bomb or Preset.Utility => typeof(ConsumableItemDefinition),
                Preset.MaterialRecipe => typeof(CraftableItemDefinition),
                Preset.PartyWeapon or Preset.PartyArmor => typeof(EquipmentItemDefinition),
                Preset.EmptySkill => typeof(SkillData), Preset.EmptyCharacter => typeof(CharacterDefinition),
                Preset.EmptySkillTree => typeof(SkillTreeDefinition),
                Preset.EmptyEncounter => typeof(EncounterDefinition), Preset.EmptyDungeon => typeof(DungeonDefinition),
                _ => typeof(ItemDefinition)
            };
        }

        private static string GetPresetName(Preset preset)
        {
            return preset switch
            {
                Preset.Potion => "CID_Potion_New", Preset.Bomb => "CID_Bomb_New",
                Preset.Utility => "CID_Utility_New", Preset.MaterialRecipe => "CID_MaterialRecipe_New",
                Preset.PartyWeapon => "EID_PartyWeapon_New", Preset.PartyArmor => "EID_PartyArmor_New",
                Preset.EmptyEquipmentPart => "EPD_EquipmentPart_New", Preset.EmptySkill => "SkillData_New",
                Preset.EmptySkillTree => "SkillTree_New",
                Preset.EmptyCharacter => "CharacterDefinition_New", Preset.EmptyEncounter => "EncounterDefinition_New",
                Preset.EmptyDungeon => "DungeonDefinition_New", _ => "ID_Item_New"
            };
        }

        private static void ApplyPreset(UnityEngine.Object asset, Preset preset, string path)
        {
            var serialized = new SerializedObject(asset);
            var prefix = GetPresetPrefix(preset);
            SetProperty(serialized, "<Id>k__BackingField", BuildUniqueId(prefix));
            SetProperty(serialized, "<SkillId>k__BackingField", BuildUniqueId(prefix));
            SetProperty(serialized, "<DisplayName>k__BackingField", GetPresetName(preset));
            SetProperty(serialized, "<Description>k__BackingField", string.Empty);
            SetProperty(serialized, "<TooltipDescription>k__BackingField", string.Empty);
            SetProperty(serialized, "<BaseRarity>k__BackingField", (int)RarityTier.Common);
            SetProperty(serialized, "<BaseValue>k__BackingField", 0);
            SetProperty(serialized, "<Weight>k__BackingField", 1f);
            SetProperty(serialized, "<IsStackable>k__BackingField", false);
            switch (preset)
            {
                case Preset.Potion:
                    SetProperty(serialized, "<ItemType>k__BackingField", (int)ItemType.Potion);
                    SetProperty(serialized, "<UseType>k__BackingField", (int)ConsumableUseType.Potion);
                    SetProperty(serialized, "<MaxCharges>k__BackingField", 5); break;
                case Preset.Bomb:
                    SetProperty(serialized, "<ItemType>k__BackingField", (int)ItemType.Tool);
                    SetProperty(serialized, "<UseType>k__BackingField", (int)ConsumableUseType.Bomb);
                    SetProperty(serialized, "<MaxCharges>k__BackingField", 5); break;
                case Preset.Utility:
                    SetProperty(serialized, "<ItemType>k__BackingField", (int)ItemType.Tool);
                    SetProperty(serialized, "<UseType>k__BackingField", (int)ConsumableUseType.Utility);
                    SetProperty(serialized, "<MaxCharges>k__BackingField", 1); break;
                case Preset.MaterialRecipe:
                    SetProperty(serialized, "<ItemType>k__BackingField", (int)ItemType.Material);
                    SetProperty(serialized, "<CraftingDiscipline>k__BackingField", (int)MaterialDiscipline.Smithing);
                    SetProperty(serialized, "<OutputAmount>k__BackingField", 1); break;
                case Preset.PartyWeapon:
                    SetProperty(serialized, "<ItemType>k__BackingField", (int)ItemType.Weapon); break;
                case Preset.PartyArmor:
                    SetProperty(serialized, "<ItemType>k__BackingField", (int)ItemType.Armor); break;
                case Preset.EmptyEquipmentPart:
                    SetProperty(serialized, "<Kind>k__BackingField", (int)EquipmentPartKind.WeaponHead);
                    SetProperty(serialized, "<LevelBracket>k__BackingField", (int)EquipmentPartLevelBracket.Level5); break;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(asset);
        }

        private static string GetPresetPrefix(Preset preset)
        {
            return preset switch
            {
                Preset.EmptyEquipmentPart => "equipment.part.authoring",
                Preset.Potion or Preset.Bomb or Preset.Utility => "item.consumable.authoring",
                Preset.MaterialRecipe => "item.material.recipe.authoring", Preset.PartyWeapon => "item.weapon.party.authoring",
                Preset.PartyArmor => "item.armor.party.authoring", Preset.EmptySkill => "skill.authoring",
                Preset.EmptySkillTree => "skill.tree.authoring",
                Preset.EmptyCharacter => "character.authoring", Preset.EmptyEncounter => "encounter.authoring",
                Preset.EmptyDungeon => "dungeon.authoring", _ => "item.authoring"
            };
        }

        private static void SetUniqueIdentity(UnityEngine.Object asset, string id, string displayName)
        {
            var serialized = new SerializedObject(asset);
            var uniqueId = BuildUniqueId(string.IsNullOrWhiteSpace(id) ? "authoring.copy" : id);
            SetProperty(serialized, "<Id>k__BackingField", uniqueId);
            SetProperty(serialized, "<SkillId>k__BackingField", uniqueId);
            SetProperty(serialized, "<DisplayName>k__BackingField", displayName);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static string BuildUniqueId(string prefix)
        {
            var candidate = prefix;
            var suffix = 1;
            var ids = FindAllContentAssets().Select(GetId).Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (ids.Contains(candidate)) candidate = $"{prefix}.{suffix++}";
            return candidate;
        }

        private static IEnumerable<UnityEngine.Object> FindAllContentAssets()
        {
            var filters = new[]
            {
                "t:ItemDefinition", "t:EquipmentPartDefinition", "t:SkillData", "t:SkillTreeDefinition",
                "t:CharacterDefinition", "t:EncounterDefinition", "t:DungeonDefinition"
            };
            var paths = filters.SelectMany(AssetDatabase.FindAssets).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return paths.Select(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)).Where(IsSupportedAsset);
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void SetProperty(SerializedObject serialized, string name, string value)
        {
            var property = serialized.FindProperty(name);
            if (property != null && property.propertyType == SerializedPropertyType.String) property.stringValue = value;
        }

        private static void SetProperty(SerializedObject serialized, string name, int value)
        {
            var property = serialized.FindProperty(name);
            if (property == null) return;
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = name == "<LevelBracket>k__BackingField"
                    ? Array.IndexOf(new[] { 5, 15, 25, 35, 45 }, value) : value;
            }
            else if (property.propertyType == SerializedPropertyType.Integer) property.intValue = value;
        }

        private static void SetProperty(SerializedObject serialized, string name, float value)
        {
            var property = serialized.FindProperty(name);
            if (property != null && property.propertyType == SerializedPropertyType.Float) property.floatValue = value;
        }

        private static void SetProperty(SerializedObject serialized, string name, bool value)
        {
            var property = serialized.FindProperty(name);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean) property.boolValue = value;
        }

        private TableColumns[] GetActiveColumns()
        {
            var values = new[]
            {
                TableColumns.Id, TableColumns.Name, TableColumns.Type, TableColumns.Category,
                TableColumns.Level, TableColumns.Value, TableColumns.Status
            };
            return values.Where(column => visibleColumns.HasFlag(column)).ToArray();
        }

        private float GetTableWidth()
        {
            return GetActiveColumns().Sum(GetColumnWidth) + 12f;
        }

        private readonly struct CategoryOption
        {
            public string Label { get; }
            public string Value { get; }

            public CategoryOption(string label, string value)
            {
                Label = label;
                Value = value;
            }
        }
    }
}
