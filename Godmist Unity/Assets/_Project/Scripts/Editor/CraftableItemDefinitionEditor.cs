using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(CraftableItemDefinition))]
    public sealed class CraftableItemDefinitionEditor : UnityEditor.Editor
    {
        private const string ItemTypePropertyName = "<ItemType>k__BackingField";
        private const string MaterialDisciplinePropertyName =
            "<MaterialDiscipline>k__BackingField";
        private const string BaseValuePropertyName = "<BaseValue>k__BackingField";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script"
                    || property.propertyPath == MaterialDisciplinePropertyName
                    || property.propertyPath == BaseValuePropertyName)
                {
                    continue;
                }

                if (property.propertyPath == ItemTypePropertyName)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(property, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }

                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            using (new EditorGUI.DisabledScope(true))
            {
                if (((CraftableItemDefinition)target).TryCalculateBaseValue(out var value, out var failure))
                    EditorGUILayout.IntField("Calculated Base Value", value);
                else
                    EditorGUILayout.HelpBox(failure.Message, MessageType.Error);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
