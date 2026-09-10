using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(JewelryItemDefinition))]
    public sealed class JewelryItemDefinitionEditor : UnityEditor.Editor
    {
        private readonly Dictionary<string, ReorderableList> modifierLists = new();
        private ReorderableList metals;
        private ReorderableList gems;

        private void OnEnable()
        {
            metals = CreateMetalList("<Metals>k__BackingField");
            gems = CreateGemList("<Gems>k__BackingField");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var itemType = serializedObject.FindProperty("<ItemType>k__BackingField");
            if (itemType != null) itemType.enumValueIndex = (int)ItemType.Jewelry;

            DrawProperty("<Id>k__BackingField");
            DrawProperty("<Icon>k__BackingField");
            DrawProperty("<Weight>k__BackingField");
            DrawProperty("<JewelrySlot>k__BackingField");
            EditorGUILayout.Space(6);
            metals?.DoLayoutList();
            EditorGUILayout.Space(6);
            gems?.DoLayoutList();
            EditorGUILayout.HelpBox(
                "Name, value, rarity and level are generated from the selected metal and gem. " +
                "Jewelry has no class requirement.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }

        private ReorderableList CreateMetalList(string propertyName)
        {
            var variants = serializedObject.FindProperty(propertyName);
            return new ReorderableList(serializedObject, variants, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Metals"),
                elementHeightCallback = index => GetVariantHeight(variants.GetArrayElementAtIndex(index)),
                drawElementCallback = (rect, index, _, _) => DrawVariant(rect, variants.GetArrayElementAtIndex(index))
            };
        }

        private ReorderableList CreateGemList(string propertyName)
        {
            var gemsProperty = serializedObject.FindProperty(propertyName);
            return new ReorderableList(serializedObject, gemsProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Available gems"),
                drawElementCallback = (rect, index, _, _) => EditorGUI.PropertyField(
                    rect, gemsProperty.GetArrayElementAtIndex(index), GUIContent.none)
            };
        }

        private float GetVariantHeight(SerializedProperty variant)
        {
            var modifiers = variant.FindPropertyRelative("<Modifiers>k__BackingField");
            return EditorGUIUtility.singleLineHeight * 2f + 20f + GetModifierList(modifiers).GetHeight();
        }

        private void DrawVariant(Rect rect, SerializedProperty variant)
        {
            rect.y += 2f;
            var line = EditorGUIUtility.singleLineHeight;
            var material = variant.FindPropertyRelative("<Material>k__BackingField");
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), material, new GUIContent("Material"));
            rect.y += line + 3f;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), variant.FindPropertyRelative("<Amount>k__BackingField"), new GUIContent("Amount"));
            rect.y += line + 5f;
            var modifiers = GetModifierList(variant.FindPropertyRelative("<Modifiers>k__BackingField"));
            modifiers.DoList(new Rect(rect.x, rect.y, rect.width, modifiers.GetHeight()));
        }

        private ReorderableList GetModifierList(SerializedProperty modifiers)
        {
            if (modifierLists.TryGetValue(modifiers.propertyPath, out var list)) return list;
            list = new ReorderableList(serializedObject, modifiers, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Modifiers"),
                elementHeightCallback = index => GetModifierHeight(modifiers.GetArrayElementAtIndex(index)),
                drawElementCallback = (rect, index, _, _) => DrawModifier(rect, modifiers.GetArrayElementAtIndex(index))
            };
            modifierLists.Add(modifiers.propertyPath, list);
            return list;
        }

        internal static float GetModifierHeight(SerializedProperty modifier)
        {
            var target = (JewelryModifierTarget)modifier.FindPropertyRelative("<Target>k__BackingField").enumValueIndex;
            var lines = target == JewelryModifierTarget.Stat ? 4 : target == JewelryModifierTarget.Resistance ? 3 : 3;
            if (target == JewelryModifierTarget.GameplayVariable
                && (GameplayVariableType)modifier.FindPropertyRelative("<GameplayVariable>k__BackingField").enumValueIndex
                    == GameplayVariableType.DamageDealtPercentInDungeonTheme)
                lines++;
            return lines * (EditorGUIUtility.singleLineHeight + 3f) + 9f;
        }

        internal static void DrawModifier(Rect rect, SerializedProperty modifier)
        {
            const float spacing = 3f;
            var line = EditorGUIUtility.singleLineHeight;
            var target = modifier.FindPropertyRelative("<Target>k__BackingField");
            target.enumValueIndex = EditorGUI.Popup(
                new Rect(rect.x, rect.y, rect.width, line),
                "Modify",
                target.enumValueIndex,
                new[] { "Stat", "Resistance", "Gameplay variable" });
            rect.y += line + spacing;
            switch ((JewelryModifierTarget)target.enumValueIndex)
            {
                case JewelryModifierTarget.Stat:
                    DrawEnumPopup(new Rect(rect.x, rect.y, rect.width, line), "Stat",
                        modifier.FindPropertyRelative("<StatType>k__BackingField"), StatNames);
                    rect.y += line + spacing;
                    DrawEnumPopup(new Rect(rect.x, rect.y, rect.width, line), "Operation",
                        modifier.FindPropertyRelative("<Operation>k__BackingField"), OperationNames);
                    rect.y += line + spacing;
                    break;
                case JewelryModifierTarget.Resistance:
                    DrawEnumPopup(new Rect(rect.x, rect.y, rect.width, line), "Resistance",
                        modifier.FindPropertyRelative("<ResistanceType>k__BackingField"), ResistanceNames);
                    rect.y += line + spacing;
                    break;
                case JewelryModifierTarget.GameplayVariable:
                    DrawEnumPopup(new Rect(rect.x, rect.y, rect.width, line), "Variable",
                        modifier.FindPropertyRelative("<GameplayVariable>k__BackingField"), GameplayVariableNames);
                    rect.y += line + spacing;
                    var variable = (GameplayVariableType)modifier.FindPropertyRelative("<GameplayVariable>k__BackingField").enumValueIndex;
                    if (variable == GameplayVariableType.DamageDealtPercentInDungeonTheme)
                    {
                        EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), modifier.FindPropertyRelative("<ReferenceId>k__BackingField"), new GUIContent("Dungeon theme"));
                        rect.y += line + spacing;
                    }
                    break;
            }
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, line), modifier.FindPropertyRelative("<Value>k__BackingField"), new GUIContent("Value"));
        }

        private static void DrawEnumPopup(
            Rect rect,
            string label,
            SerializedProperty property,
            string[] names)
        {
            property.enumValueIndex = EditorGUI.Popup(rect, label, property.enumValueIndex, names);
        }

        private static readonly string[] StatNames =
        {
            "Max health", "Min attack", "Max attack", "Physical defense", "Magical defense",
            "Dodge", "Critical chance", "Critical damage", "Speed", "Resource regeneration",
            "Weapon accuracy", "Max resource", "Physical defense penetration",
            "Magical defense penetration", "Healing power"
        };

        private static readonly string[] OperationNames =
        {
            "Flat base", "Percent", "Flat absolute", "Maximum"
        };

        private static readonly string[] ResistanceNames =
        {
            "Bleed", "Poison", "Fire", "Weakening", "Suppression", "Move"
        };

        private static readonly string[] GameplayVariableNames =
        {
            "Damage dealt", "Damage received", "Experience gain", "Gold gain",
            "Damage dealt in dungeon theme"
        };
    }
}
