using Godmist.Data.Items;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(JewelryGemDefinition))]
    public sealed class JewelryGemDefinitionEditor : UnityEditor.Editor
    {
        private ReorderableList modifiers;

        private void OnEnable()
        {
            var property = serializedObject.FindProperty("<Modifiers>k__BackingField");
            modifiers = new ReorderableList(serializedObject, property, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Modifiers"),
                elementHeightCallback = index => JewelryItemDefinitionEditor.GetModifierHeight(
                    property.GetArrayElementAtIndex(index)),
                drawElementCallback = (rect, index, _, _) => JewelryItemDefinitionEditor.DrawModifier(
                    rect, property.GetArrayElementAtIndex(index))
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("<CraftingMaterial>k__BackingField"),
                new GUIContent("Material"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("<Amount>k__BackingField"),
                new GUIContent("Amount"));
            EditorGUILayout.Space(6);
            modifiers?.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
