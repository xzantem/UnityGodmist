using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(EquipmentItemDefinition))]
    public class EquipmentItemDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawProperty("<Id>k__BackingField");
            DrawProperty("<DisplayName>k__BackingField");
            DrawProperty("<Description>k__BackingField");
            DrawProperty("<Icon>k__BackingField");

            var itemType = serializedObject.FindProperty("<ItemType>k__BackingField");
            var selected = itemType.enumValueIndex == (int)ItemType.Armor ? 1 : 0;
            selected = EditorGUILayout.Popup("Item Type", selected, new[] { "Weapon", "Armor" });
            itemType.enumValueIndex = selected == 0 ? (int)ItemType.Weapon : (int)ItemType.Armor;

            DrawProperty("<BaseRarity>k__BackingField");
            DrawProperty("<Weight>k__BackingField");
            DrawProperty("<RequiredClass>k__BackingField");
            EditorGUILayout.HelpBox(
                "EquipmentSlot i IsStackable wynikaja z Item Type. Required Level oraz Base Value sa wyliczane runtime; Base Value pozostaje nieobslugiwane do czasu modelu kosztow materialow.",
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
