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
            var choices = new[] { "Weapon", "Armor", "Jewelry" };
            var selected = itemType.enumValueIndex == (int)ItemType.Armor ? 1
                : itemType.enumValueIndex == (int)ItemType.Jewelry ? 2 : 0;
            selected = EditorGUILayout.Popup("Item Type", selected, choices);
            itemType.enumValueIndex = selected == 0 ? (int)ItemType.Weapon
                : selected == 1 ? (int)ItemType.Armor : (int)ItemType.Jewelry;

            DrawProperty("<BaseRarity>k__BackingField");
            DrawProperty("<Weight>k__BackingField");
            if (selected != 2) DrawProperty("<RequiredClass>k__BackingField");
            if (selected == 2) DrawProperty("<JewelrySlot>k__BackingField");
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
