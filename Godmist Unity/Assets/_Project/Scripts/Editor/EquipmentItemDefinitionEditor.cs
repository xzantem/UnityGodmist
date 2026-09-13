using Godmist.Data.Items;
using UnityEditor;

namespace Godmist.Editor
{
    [CustomEditor(typeof(EquipmentItemDefinition), true)]
    public sealed class EquipmentItemDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("<Id>k__BackingField");

            if (target is UniqueEquipmentDefinition)
            {
                Draw("<BaseDefinition>k__BackingField");
                Draw("<DisplayName>k__BackingField");
                Draw("<Description>k__BackingField");
                Draw("<Effects>k__BackingField");
                EditorGUILayout.HelpBox(
                    "Typ, waga, klasa, slot oraz wartości bazowe pochodzą z Base Definition. "
                    + "Rzadkość i wartość instancji ustala generator.",
                    MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            Draw("<Icon>k__BackingField");
            Draw("<Weight>k__BackingField");
            Draw("<ItemType>k__BackingField");
            EditorGUILayout.HelpBox("Zwykłe wyposażenie otrzymuje nazwę z pierwszej wybranej części.", MessageType.Info);

            Draw("<RequiredClass>k__BackingField");
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
    }
}
