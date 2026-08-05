using Godmist.Data.Dungeons;
using UnityEditor;

namespace Godmist.Editor
{
    [CustomEditor(typeof(DungeonDefinition))]
    public class DungeonDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty idProperty;
        private SerializedProperty displayNameProperty;
        private SerializedProperty themeProperty;
        private SerializedProperty isProceduralProperty;
        private SerializedProperty isStoryDungeonProperty;
        private SerializedProperty minFloorCountProperty;
        private SerializedProperty maxFloorCountProperty;
        private SerializedProperty minRoomCountProperty;
        private SerializedProperty maxRoomCountProperty;
        private SerializedProperty encounterDefinitionsProperty;

        private void OnEnable()
        {
            idProperty = serializedObject.FindProperty("<Id>k__BackingField");
            displayNameProperty = serializedObject.FindProperty("<DisplayName>k__BackingField");
            themeProperty = serializedObject.FindProperty("<Theme>k__BackingField");
            isProceduralProperty = serializedObject.FindProperty("<IsProcedural>k__BackingField");
            isStoryDungeonProperty = serializedObject.FindProperty("<IsStoryDungeon>k__BackingField");
            minFloorCountProperty = serializedObject.FindProperty("<MinFloorCount>k__BackingField");
            maxFloorCountProperty = serializedObject.FindProperty("<MaxFloorCount>k__BackingField");
            minRoomCountProperty = serializedObject.FindProperty("<MinRoomCountPerFloor>k__BackingField");
            maxRoomCountProperty = serializedObject.FindProperty("<MaxRoomCountPerFloor>k__BackingField");
            encounterDefinitionsProperty = serializedObject.FindProperty("<EncounterDefinitions>k__BackingField");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(idProperty);
            EditorGUILayout.PropertyField(displayNameProperty);
            EditorGUILayout.PropertyField(themeProperty);
            EditorGUILayout.PropertyField(isProceduralProperty);
            EditorGUILayout.PropertyField(isStoryDungeonProperty);

            if (isProceduralProperty.boolValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Procedural generation",
                    EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(minFloorCountProperty);
                EditorGUILayout.PropertyField(maxFloorCountProperty);
                EditorGUILayout.PropertyField(minRoomCountProperty);
                EditorGUILayout.PropertyField(maxRoomCountProperty);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(encounterDefinitionsProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}