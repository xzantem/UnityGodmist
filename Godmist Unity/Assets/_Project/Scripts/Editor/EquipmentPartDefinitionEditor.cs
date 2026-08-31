using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEditor;

namespace Godmist.Editor
{
    [CustomEditor(typeof(EquipmentPartDefinition))]
    public class EquipmentPartDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("<Id>k__BackingField");
            Draw("<RequiredClass>k__BackingField");
            Draw("<Kind>k__BackingField");
            Draw("<LevelBracket>k__BackingField");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Materiały craftingu", EditorStyles.boldLabel);
            Draw("<CraftingMaterials>k__BackingField");

            var part = (EquipmentPartDefinition)target;
            var stats = serializedObject.FindProperty("<Stats>k__BackingField");
            var isMage = part.RequiredClass?.Id == "class.mage";
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statystyki czesci", EditorStyles.boldLabel);
            DrawSchema(stats, part.Kind, isMage);
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSchema(SerializedProperty stats, EquipmentPartKind kind, bool isMage)
        {
            switch (kind)
            {
                case EquipmentPartKind.WeaponHead:
                    Draw(stats, "MinAttackBase", "MaxAttackBase", "CritDamageBase");
                    Draw(stats, isMage ? "ResourceRegenerationPercent" : "CritChancePercent",
                        isMage ? "MaxResourcePercent" : "WeaponAccuracyPercent");
                    break;
                case EquipmentPartKind.WeaponBinder:
                    Draw(stats, isMage ? "ResourceRegenerationBase" : "CritChanceBase");
                    Draw(stats, "MinAttackPercent", "MaxAttackPercent", "CritDamagePercent",
                        isMage ? "MaxResourcePercent" : "WeaponAccuracyPercent");
                    break;
                case EquipmentPartKind.WeaponHandle:
                    Draw(stats, isMage ? "MaxResourceBase" : "WeaponAccuracyBase");
                    Draw(stats, "MinAttackPercent", "MaxAttackPercent", "CritDamagePercent",
                        isMage ? "ResourceRegenerationPercent" : "CritChancePercent");
                    break;
                case EquipmentPartKind.ArmorPlate:
                    Draw(stats, "PhysicalDefenseBase", "MagicalDefenseBase", "MaxHealthPercent", "DodgePercent");
                    break;
                case EquipmentPartKind.ArmorBinder:
                    Draw(stats, "MaxHealthBase", "DodgePercent", "PhysicalDefensePercent", "MagicalDefensePercent");
                    break;
                case EquipmentPartKind.ArmorBase:
                    Draw(stats, "DodgeBase", "MaxHealthPercent", "PhysicalDefensePercent", "MagicalDefensePercent");
                    break;
            }
        }

        private void Draw(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }

        private static void Draw(SerializedProperty parent, params string[] names)
        {
            foreach (var name in names)
            {
                var property = parent.FindPropertyRelative(name);
                if (property != null) EditorGUILayout.PropertyField(property);
            }
        }
    }
}
