using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(AlchemyConsumableDefinition))]
    public sealed class AlchemyConsumableDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawProperty("<Id>k__BackingField");
            DrawProperty("<Icon>k__BackingField");
            DrawProperty("<Weight>k__BackingField");
            DrawProperty("<UseType>k__BackingField");
            DrawProperty("<MaxCharges>k__BackingField");

            var useType = serializedObject.FindProperty("<UseType>k__BackingField");
            var itemType = serializedObject.FindProperty("<ItemType>k__BackingField");
            if (useType != null && itemType != null)
            {
                itemType.enumValueIndex = useType.enumValueIndex == (int)ConsumableUseType.Bomb
                    ? (int)ItemType.Bomb
                    : (int)ItemType.Potion;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Nazwa, opis, wartość, rzadkość i efekty są ustalane na runtime "
                + "z materiałów użytych przez Alchemika. Typ przedmiotu jest ustawiany "
                + "automatycznie: Potion albo Bomb.",
                MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
    }
}
