using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ContentImportApplier
    {
        private const string ItemsFolder = "Assets/_Project/ScriptableObjects/Items";
        private const string ConsumablesFolder = ItemsFolder + "/Consumables";
        private const string EquipmentFolder = "Assets/_Project/ScriptableObjects/Equipment";
        private const string SkillsFolder = "Assets/_Project/ScriptableObjects/Skills";
        private const string CharactersFolder = "Assets/_Project/ScriptableObjects/Characters";

        public static bool TryApply(
            ContentImportPreviewRow row,
            GameContentCatalog catalog,
            out UnityEngine.Object importedAsset,
            out string error)
        {
            importedAsset = null;
            error = string.Empty;
            if (row == null)
            {
                error = "Brak rekordu importu.";
                return false;
            }
            if (catalog == null)
            {
                error = "Wybierz aktywny GameContentCatalog.";
                return false;
            }
            if (!row.IsValid || row.ChangeKind is ContentImportChangeKind.Error or ContentImportChangeKind.Conflict)
            {
                error = row.ProblemSummary;
                return false;
            }

            var created = row.ExistingAsset == null;
            var target = row.ExistingAsset;
            var undoGroup = Undo.GetCurrentGroup();
            try
            {
                if (created)
                {
                    target = CreateAsset(row, out var path);
                    if (target == null)
                    {
                        error = "Nie udało się utworzyć assetu.";
                        return false;
                    }
                    importedAsset = target;
                    Undo.RegisterCreatedObjectUndo(target, "Import content asset");
                }
                else if (!IsExpectedType(row.TargetType, target))
                {
                    error = "Asset z katalogu ma inny typ niż typ wybrany w preview.";
                    return false;
                }

                Undo.RecordObject(target, created ? "Import content asset" : "Update imported content");
                ApplyValues(row, target, catalog);
                EditorUtility.SetDirty(target);

                var report = ContentAuthoringValidator.Validate(target, catalog);
                if (!report.IsValid)
                {
                    error = "Walidacja po mapowaniu nie powiodła się: "
                            + string.Join(" | ", report.Errors);
                    if (created)
                    {
                        var path = AssetDatabase.GetAssetPath(target);
                        if (!string.IsNullOrWhiteSpace(path)) AssetDatabase.DeleteAsset(path);
                    }
                    else
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                    }
                    importedAsset = null;
                    return false;
                }

                RenameAssetToConvention(target, row);
                AssignToCatalog(target, catalog);
                EditorUtility.SetDirty(catalog);
                importedAsset = target;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Nie udało się zastosować rekordu: {exception.Message}";
                if (created && target != null)
                {
                    var path = AssetDatabase.GetAssetPath(target);
                    if (!string.IsNullOrWhiteSpace(path)) AssetDatabase.DeleteAsset(path);
                }
                else if (target != null)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                importedAsset = null;
                return false;
            }
        }

        private static UnityEngine.Object CreateAsset(ContentImportPreviewRow row, out string path)
        {
            var folder = GetFolder(row.TargetType);
            EnsureFolder(folder);
            var fileName = BuildAssetFileName(row.TargetType, row.Id);
            path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            var asset = ScriptableObject.CreateInstance(GetDefinitionType(row.TargetType));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) ?? asset;
        }

        public static string BuildAssetFileName(
            ContentImportTargetType target,
            string id)
        {
            var prefix = target switch
            {
                ContentImportTargetType.ItemDefinition => "ID_",
                ContentImportTargetType.ConsumableItemDefinition => "CID_",
                ContentImportTargetType.CraftableItemDefinition => "CID_",
                ContentImportTargetType.EquipmentItemDefinition => "EID_",
                ContentImportTargetType.EquipmentPartDefinition => "EPD_",
                ContentImportTargetType.SkillData => "SD_",
                ContentImportTargetType.CharacterDefinition => "CD_",
                _ => "Imported_"
            };
            var segment = (id ?? string.Empty)
                .Split(new[] { '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();
            if (string.IsNullOrWhiteSpace(segment)) segment = "Content";
            var words = segment.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var builder = new System.Text.StringBuilder(prefix);
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;
                builder.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1) builder.Append(word.Substring(1));
            }
            return builder.ToString();
        }

        private static void RenameAssetToConvention(
            UnityEngine.Object asset,
            ContentImportPreviewRow row)
        {
            var currentPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(currentPath)) return;
            var directory = Path.GetDirectoryName(currentPath)?.Replace('\\', '/') ?? string.Empty;
            var desiredPath = $"{directory}/{BuildAssetFileName(row.TargetType, row.Id)}.asset";
            if (string.Equals(currentPath, desiredPath, StringComparison.OrdinalIgnoreCase)) return;
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(desiredPath) != null)
                desiredPath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
            var moveError = AssetDatabase.MoveAsset(currentPath, desiredPath);
            if (!string.IsNullOrWhiteSpace(moveError)) throw new InvalidOperationException(moveError);
        }

        private static Type GetDefinitionType(ContentImportTargetType target) => target switch
        {
            ContentImportTargetType.ItemDefinition => typeof(ItemDefinition),
            ContentImportTargetType.ConsumableItemDefinition => typeof(ConsumableItemDefinition),
            ContentImportTargetType.CraftableItemDefinition => typeof(CraftableItemDefinition),
            ContentImportTargetType.EquipmentItemDefinition => typeof(EquipmentItemDefinition),
            ContentImportTargetType.EquipmentPartDefinition => typeof(EquipmentPartDefinition),
            ContentImportTargetType.SkillData => typeof(SkillData),
            ContentImportTargetType.CharacterDefinition => typeof(CharacterDefinition),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

        private static string GetFolder(ContentImportTargetType target) => target switch
        {
            ContentImportTargetType.ConsumableItemDefinition => ConsumablesFolder,
            ContentImportTargetType.EquipmentItemDefinition => EquipmentFolder,
            ContentImportTargetType.EquipmentPartDefinition => EquipmentFolder,
            ContentImportTargetType.SkillData => SkillsFolder,
            ContentImportTargetType.CharacterDefinition => CharactersFolder,
            _ => ItemsFolder
        };

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void ApplyValues(
            ContentImportPreviewRow row,
            UnityEngine.Object target,
            GameContentCatalog catalog)
        {
            var serialized = new SerializedObject(target);
            serialized.Update();
            switch (row.TargetType)
            {
                case ContentImportTargetType.ItemDefinition:
                case ContentImportTargetType.ConsumableItemDefinition:
                case ContentImportTargetType.CraftableItemDefinition:
                case ContentImportTargetType.EquipmentItemDefinition:
                    ApplyItem(row, serialized, catalog);
                    break;
                case ContentImportTargetType.EquipmentPartDefinition:
                    ApplyPart(row, serialized, catalog);
                    break;
                case ContentImportTargetType.SkillData:
                    SetString(serialized, "SkillId", row.Id);
                    SetValue(serialized, "DisplayName", row.DisplayName);
                    SetValues(row, serialized, ("TooltipDescription", SerializedPropertyType.String),
                        ("SkillAccuracy", SerializedPropertyType.Float), ("ActionCost", SerializedPropertyType.Integer),
                        ("ResourceCost", SerializedPropertyType.Integer), ("ResourceType", SerializedPropertyType.Enum));
                    break;
                case ContentImportTargetType.CharacterDefinition:
                    SetString(serialized, "Id", row.Id);
                    SetValue(serialized, "DisplayName", row.DisplayName);
                    SetValues(row, serialized, ("ExperienceReward", SerializedPropertyType.Integer),
                        ("GoldReward", SerializedPropertyType.Integer));
                    SetNestedValues(row, serialized, "BaseStats",
                        "MaxHealth", "MinAttack", "MaxAttack", "PhysicalDefense", "MagicalDefense",
                        "Dodge", "Speed", "MaxResource");
                    SetClassReference(row, serialized, catalog);
                    break;
            }
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(target);
        }

        private static void ApplyItem(
            ContentImportPreviewRow row,
            SerializedObject serialized,
            GameContentCatalog catalog)
        {
            SetString(serialized, "Id", row.Id);
            SetValue(serialized, "DisplayName", row.DisplayName);
            SetValues(row, serialized, ("Description", SerializedPropertyType.String),
                ("ItemType", SerializedPropertyType.Enum), ("MaterialDiscipline", SerializedPropertyType.Enum),
                ("BaseRarity", SerializedPropertyType.Enum), ("BaseValue", SerializedPropertyType.Integer),
                ("Weight", SerializedPropertyType.Float), ("IsStackable", SerializedPropertyType.Boolean));
            if (row.TargetType == ContentImportTargetType.ConsumableItemDefinition)
                SetValues(row, serialized, ("UseType", SerializedPropertyType.Enum), ("MaxCharges", SerializedPropertyType.Integer));
            if (row.TargetType == ContentImportTargetType.CraftableItemDefinition)
            {
                SetValues(row, serialized, ("CraftingDiscipline", SerializedPropertyType.Enum),
                    ("OutputAmount", SerializedPropertyType.Integer));
                ApplyIngredient(row, serialized, catalog);
            }
            if (row.TargetType == ContentImportTargetType.EquipmentItemDefinition)
            {
                SetClassReference(row, serialized, catalog);
                SetValues(row, serialized, ("JewelrySlot", SerializedPropertyType.Enum));
            }
        }

        private static void ApplyPart(
            ContentImportPreviewRow row,
            SerializedObject serialized,
            GameContentCatalog catalog)
        {
            SetString(serialized, "Id", row.Id);
            SetValue(serialized, "DisplayName", row.DisplayName);
            SetValues(row, serialized, ("Description", SerializedPropertyType.String));
            SetValues(row, serialized, ("Kind", SerializedPropertyType.Enum), ("LevelBracket", SerializedPropertyType.Enum));
            SetClassReference(row, serialized, catalog);
            var stats = FindProperty(serialized, "Stats");
            if (stats != null)
            {
                foreach (var key in new[]
                         {
                             "MinAttackBase", "MaxAttackBase", "CritChanceBase", "CritDamageBase",
                             "WeaponAccuracyBase", "MaxResourceBase"
                         })
                {
                    if (!row.Values.ContainsKey(key)) continue;
                    SetValue(stats, key, row.Values[key]);
                }
            }
            var materialId = GetValue(row, "CraftingMaterial");
            if (!string.IsNullOrWhiteSpace(materialId))
                ApplyMaterialList(serialized, "CraftingMaterials", materialId, GetInt(row, "CraftingMaterialAmount", 1), catalog);
        }

        private static void ApplyIngredient(
            ContentImportPreviewRow row,
            SerializedObject serialized,
            GameContentCatalog catalog)
        {
            var list = FindProperty(serialized, "Ingredients");
            if (list == null || list.propertyType != SerializedPropertyType.Generic) return;
            var ingredients = new List<(string Id, int Amount)>();
            for (var index = 1; ; index++)
            {
                var materialId = GetValue(row, $"IngredientMaterial_{index}");
                if (string.IsNullOrWhiteSpace(materialId)) break;
                ingredients.Add((materialId, GetInt(row, $"IngredientAmount_{index}", 1)));
            }
            list.arraySize = ingredients.Count;
            for (var index = 0; index < ingredients.Count; index++)
            {
                var ingredient = ingredients[index];
                var material = catalog.ItemDefinitions?.FirstOrDefault(item => item != null
                    && string.Equals(item.Id, ingredient.Id, StringComparison.OrdinalIgnoreCase));
                var element = list.GetArrayElementAtIndex(index);
                if (material != null) SetValue(element, "Material", material);
                SetValue(element, "Amount", ingredient.Amount.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void ApplyMaterialList(
            SerializedObject serialized,
            string propertyName,
            string materialId,
            int amount,
            GameContentCatalog catalog)
        {
            var list = FindProperty(serialized, propertyName);
            var material = catalog.ItemDefinitions?.FirstOrDefault(item => item != null
                && string.Equals(item.Id, materialId, StringComparison.OrdinalIgnoreCase));
            if (list == null || material == null || list.propertyType != SerializedPropertyType.Generic) return;
            list.arraySize = 1;
            var element = list.GetArrayElementAtIndex(0);
            SetValue(element, "Material", material);
            SetValue(element, "Amount", amount.ToString(CultureInfo.InvariantCulture));
        }

        private static void SetClassReference(
            ContentImportPreviewRow row,
            SerializedObject serialized,
            GameContentCatalog catalog)
        {
            var classId = GetValue(row, "RequiredClass");
            if (string.IsNullOrWhiteSpace(classId)) return;
            var classData = catalog.CharacterClasses?.FirstOrDefault(value => value != null
                && string.Equals(value.Id, classId, StringComparison.OrdinalIgnoreCase));
            if (classData == null) return;
            SetValue(serialized,
                row.TargetType == ContentImportTargetType.CharacterDefinition ? "ClassData" : "RequiredClass",
                classData);
        }

        private static bool IsExpectedType(ContentImportTargetType target, UnityEngine.Object value) => target switch
        {
            ContentImportTargetType.ItemDefinition => value is ItemDefinition && value.GetType() == typeof(ItemDefinition),
            ContentImportTargetType.ConsumableItemDefinition => value is ConsumableItemDefinition,
            ContentImportTargetType.CraftableItemDefinition => value is CraftableItemDefinition,
            ContentImportTargetType.EquipmentItemDefinition => value is EquipmentItemDefinition && value.GetType() == typeof(EquipmentItemDefinition),
            ContentImportTargetType.EquipmentPartDefinition => value is EquipmentPartDefinition,
            ContentImportTargetType.SkillData => value is SkillData,
            ContentImportTargetType.CharacterDefinition => value is CharacterDefinition,
            _ => false
        };

        private static void AssignToCatalog(UnityEngine.Object asset, GameContentCatalog catalog)
        {
            Undo.RecordObject(catalog, "Assign imported content");
            var serialized = new SerializedObject(catalog);
            serialized.Update();
            var propertyName = asset switch
            {
                EquipmentPartDefinition => "EquipmentPartDefinitions",
                SkillData => "SkillDefinitions",
                CharacterDefinition => "CharacterDefinitions",
                ItemDefinition => "ItemDefinitions",
                _ => string.Empty
            };
            if (!string.IsNullOrWhiteSpace(propertyName)) AddReference(serialized, propertyName, asset);
            serialized.ApplyModifiedProperties();
        }

        private static void AddReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var list = FindProperty(serialized, propertyName);
            if (list == null || list.propertyType != SerializedPropertyType.Generic) return;
            for (var index = 0; index < list.arraySize; index++)
                if (list.GetArrayElementAtIndex(index).objectReferenceValue == value) return;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
        }

        private static void SetValues(
            ContentImportPreviewRow row,
            SerializedObject serialized,
            params (string Name, SerializedPropertyType Type)[] fields)
        {
            foreach (var field in fields)
                if (row.Values.ContainsKey(field.Name)) SetValue(serialized, field.Name, row.Values[field.Name], field.Type);
        }

        private static void SetNestedValues(
            ContentImportPreviewRow row,
            SerializedObject serialized,
            string parentName,
            params string[] fields)
        {
            var parent = FindProperty(serialized, parentName);
            if (parent == null) return;
            foreach (var field in fields)
                if (row.Values.ContainsKey(parentName + "." + field)) SetValue(parent, field, row.Values[parentName + "." + field]);
        }

        private static void SetString(SerializedObject serialized, string name, string value) => SetValue(serialized, name, value);

        private static void SetValue(SerializedObject serialized, string name, object value, SerializedPropertyType? expectedType = null)
        {
            var property = FindProperty(serialized, name);
            if (property != null) SetValue(property, value?.ToString() ?? string.Empty, value as UnityEngine.Object, expectedType);
        }

        private static void SetValue(SerializedProperty parent, string name, object value)
        {
            var property = FindRelativeProperty(parent, name);
            if (property != null) SetValue(property, value?.ToString() ?? string.Empty, value as UnityEngine.Object, null);
        }

        private static void SetValue(
            SerializedProperty property,
            string text,
            UnityEngine.Object objectValue = null,
            SerializedPropertyType? expectedType = null)
        {
            if (objectValue != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = objectValue;
                return;
            }
            if (expectedType.HasValue && property.propertyType != expectedType.Value) return;
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = text; break;
                case SerializedPropertyType.Integer:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) property.intValue = integer;
                    break;
                case SerializedPropertyType.Float:
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var single)) property.floatValue = single;
                    break;
                case SerializedPropertyType.Boolean:
                    if (bool.TryParse(text, out var boolean)) property.boolValue = boolean;
                    break;
                case SerializedPropertyType.Enum:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumValue))
                    {
                        var enumIndex = Array.FindIndex(property.enumNames, name =>
                            string.Equals(name, enumValue.ToString(), StringComparison.Ordinal)
                            || name.EndsWith(enumValue.ToString(), StringComparison.Ordinal));
                        if (enumIndex >= 0) property.enumValueIndex = enumIndex;
                        else property.intValue = enumValue;
                    }
                    else
                    {
                        var index = Array.IndexOf(property.enumNames, text);
                        if (index >= 0) property.enumValueIndex = index;
                    }
                    break;
            }
        }

        private static SerializedProperty FindProperty(SerializedObject serialized, string name)
        {
            var property = FindPropertyByPath(serialized, name);
            if (property != null) return property;
            var iterator = serialized.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (string.Equals(iterator.name, name, StringComparison.Ordinal)
                    || string.Equals(iterator.name, $"<{name}>k__BackingField", StringComparison.Ordinal))
                    return iterator.Copy();
            }
            return null;
        }

        private static SerializedProperty FindPropertyByPath(SerializedObject serialized, string path)
        {
            var segments = path.Split('.');
            SerializedProperty current = null;
            foreach (var segment in segments)
            {
                current = current == null
                    ? FindDirect(serialized, segment)
                    : FindRelativeProperty(current, segment);
                if (current == null) return null;
            }
            return current;
        }

        private static SerializedProperty FindDirect(SerializedObject serialized, string name) =>
            serialized.FindProperty(name) ?? serialized.FindProperty($"<{name}>k__BackingField");

        private static SerializedProperty FindRelativeProperty(SerializedProperty property, string name) =>
            property.FindPropertyRelative(name) ?? property.FindPropertyRelative($"<{name}>k__BackingField");

        private static string GetValue(ContentImportPreviewRow row, string key) =>
            row.Values.TryGetValue(key, out var value) ? value : string.Empty;

        private static int GetInt(ContentImportPreviewRow row, string key, int fallback) =>
            int.TryParse(GetValue(row, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : fallback;
    }
}
