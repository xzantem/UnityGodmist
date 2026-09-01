using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(ItemDefinition), true)]
    public sealed class ItemDefinitionEditor : UnityEditor.Editor
    {
        private const string ItemTypePropertyName = "<ItemType>k__BackingField";
        private const string MaterialDisciplinePropertyName =
            "<MaterialDiscipline>k__BackingField";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var itemType = serializedObject.FindProperty(ItemTypePropertyName);
            var showMaterialDiscipline = itemType == null
                || itemType.hasMultipleDifferentValues
                || itemType.enumValueIndex == (int)ItemType.Material;

            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script"
                    || property.propertyPath == MaterialDisciplinePropertyName)
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
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                if (property.propertyPath == ItemTypePropertyName
                    && showMaterialDiscipline)
                {
                    var materialDiscipline = serializedObject.FindProperty(
                        MaterialDisciplinePropertyName);
                    if (materialDiscipline != null)
                    {
                        EditorGUILayout.PropertyField(materialDiscipline, true);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
