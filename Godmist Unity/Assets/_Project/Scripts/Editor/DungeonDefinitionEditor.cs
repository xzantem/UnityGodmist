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
        private SerializedProperty layoutProfileProperty;
        private SerializedProperty minFloorCountProperty;
        private SerializedProperty maxFloorCountProperty;
        private SerializedProperty minRoomCountProperty;
        private SerializedProperty maxRoomCountProperty;
        private SerializedProperty minOptionalRoomCountProperty;
        private SerializedProperty maxOptionalRoomCountProperty;
        private SerializedProperty minLoopConnectionsProperty;
        private SerializedProperty maxLoopConnectionsProperty;
        private SerializedProperty roomTemplateCatalogProperty;
        private SerializedProperty encounterDefinitionsProperty;
        private SerializedProperty minEncounterCountPerFloor;
        private SerializedProperty maxEncounterCountPerFloor;
        private SerializedProperty allowEncounterDefinitionRepeats;

        private void OnEnable()
        {
            idProperty = serializedObject.FindProperty("<Id>k__BackingField");
            displayNameProperty = serializedObject.FindProperty("<DisplayName>k__BackingField");
            themeProperty = serializedObject.FindProperty("<Theme>k__BackingField");
            isProceduralProperty = serializedObject.FindProperty("<IsProcedural>k__BackingField");
            isStoryDungeonProperty = serializedObject.FindProperty("<IsStoryDungeon>k__BackingField");
            layoutProfileProperty = serializedObject.FindProperty("<LayoutProfile>k__BackingField");
            minFloorCountProperty = serializedObject.FindProperty("<MinFloorCount>k__BackingField");
            maxFloorCountProperty = serializedObject.FindProperty("<MaxFloorCount>k__BackingField");
            minRoomCountProperty = serializedObject.FindProperty("<MinRoomCountPerFloor>k__BackingField");
            maxRoomCountProperty = serializedObject.FindProperty("<MaxRoomCountPerFloor>k__BackingField");
            minOptionalRoomCountProperty = serializedObject.FindProperty("<MinOptionalRoomCount>k__BackingField");
            maxOptionalRoomCountProperty = serializedObject.FindProperty("<MaxOptionalRoomCount>k__BackingField");
            minLoopConnectionsProperty = serializedObject.FindProperty("<MinLoopConnections>k__BackingField");
            maxLoopConnectionsProperty = serializedObject.FindProperty("<MaxLoopConnections>k__BackingField");
            roomTemplateCatalogProperty = serializedObject.FindProperty("<RoomTemplateCatalog>k__BackingField");
            encounterDefinitionsProperty = serializedObject.FindProperty("<EncounterDefinitions>k__BackingField");
            minEncounterCountPerFloor = serializedObject.FindProperty("<MinEncounterCountPerFloor>k__BackingField");
            maxEncounterCountPerFloor = serializedObject.FindProperty("<MaxEncounterCountPerFloor>k__BackingField");
            allowEncounterDefinitionRepeats = 
                serializedObject.FindProperty("<AllowEncounterDefinitionRepeats>k__BackingField");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(idProperty);
            EditorGUILayout.PropertyField(displayNameProperty);
            EditorGUILayout.PropertyField(themeProperty);
            EditorGUILayout.PropertyField(isProceduralProperty);
            EditorGUILayout.PropertyField(isStoryDungeonProperty);
            EditorGUILayout.PropertyField(layoutProfileProperty);

            if (isProceduralProperty.boolValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Procedural generation",
                    EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(roomTemplateCatalogProperty);
                EditorGUILayout.PropertyField(minFloorCountProperty);
                EditorGUILayout.PropertyField(maxFloorCountProperty);
                EditorGUILayout.PropertyField(minRoomCountProperty);
                EditorGUILayout.PropertyField(maxRoomCountProperty);
                EditorGUILayout.PropertyField(minOptionalRoomCountProperty);
                EditorGUILayout.PropertyField(maxOptionalRoomCountProperty);
                EditorGUILayout.PropertyField(minLoopConnectionsProperty);
                EditorGUILayout.PropertyField(maxLoopConnectionsProperty);

                EditorGUILayout.PropertyField(minEncounterCountPerFloor);
                EditorGUILayout.PropertyField(maxEncounterCountPerFloor);
                EditorGUILayout.PropertyField(allowEncounterDefinitionRepeats);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(encounterDefinitionsProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}