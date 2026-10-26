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
        private SerializedProperty vegetationPresentationProperty;
        private SerializedProperty minFloorCountProperty;
        private SerializedProperty maxFloorCountProperty;
        private SerializedProperty minRoomCountProperty;
        private SerializedProperty maxRoomCountProperty;
        private SerializedProperty minOptionalRoomCountProperty;
        private SerializedProperty maxOptionalRoomCountProperty;
        private SerializedProperty minLoopConnectionsProperty;
        private SerializedProperty maxLoopConnectionsProperty;
        private SerializedProperty minProgressionGateCountProperty;
        private SerializedProperty maxProgressionGateCountProperty;
        private SerializedProperty roomTemplateCatalogProperty;
        private SerializedProperty encounterDefinitionsProperty;
        private SerializedProperty bossEncounterDefinitionProperty;
        private SerializedProperty minEncounterCountPerFloor;
        private SerializedProperty maxEncounterCountPerFloor;
        private SerializedProperty allowEncounterDefinitionRepeats;
        private SerializedProperty objectDefinitionsProperty;
        private SerializedProperty minObjectCountPerFloorProperty;
        private SerializedProperty maxObjectCountPerFloorProperty;

        private void OnEnable()
        {
            idProperty = serializedObject.FindProperty("<Id>k__BackingField");
            displayNameProperty = serializedObject.FindProperty("<DisplayName>k__BackingField");
            themeProperty = serializedObject.FindProperty("<Theme>k__BackingField");
            isProceduralProperty = serializedObject.FindProperty("<IsProcedural>k__BackingField");
            isStoryDungeonProperty = serializedObject.FindProperty("<IsStoryDungeon>k__BackingField");
            layoutProfileProperty = serializedObject.FindProperty("<LayoutProfile>k__BackingField");
            vegetationPresentationProperty = serializedObject.FindProperty("<VegetationPresentation>k__BackingField");
            minFloorCountProperty = serializedObject.FindProperty("<MinFloorCount>k__BackingField");
            maxFloorCountProperty = serializedObject.FindProperty("<MaxFloorCount>k__BackingField");
            minRoomCountProperty = serializedObject.FindProperty("<MinRoomCountPerFloor>k__BackingField");
            maxRoomCountProperty = serializedObject.FindProperty("<MaxRoomCountPerFloor>k__BackingField");
            minOptionalRoomCountProperty = serializedObject.FindProperty("<MinOptionalRoomCount>k__BackingField");
            maxOptionalRoomCountProperty = serializedObject.FindProperty("<MaxOptionalRoomCount>k__BackingField");
            minLoopConnectionsProperty = serializedObject.FindProperty("<MinLoopConnections>k__BackingField");
            maxLoopConnectionsProperty = serializedObject.FindProperty("<MaxLoopConnections>k__BackingField");
            minProgressionGateCountProperty = serializedObject.FindProperty("<MinProgressionGateCount>k__BackingField");
            maxProgressionGateCountProperty = serializedObject.FindProperty("<MaxProgressionGateCount>k__BackingField");
            roomTemplateCatalogProperty = serializedObject.FindProperty("<RoomTemplateCatalog>k__BackingField");
            encounterDefinitionsProperty = serializedObject.FindProperty("<EncounterDefinitions>k__BackingField");
            bossEncounterDefinitionProperty = serializedObject.FindProperty("<BossEncounterDefinition>k__BackingField");
            minEncounterCountPerFloor = serializedObject.FindProperty("<MinEncounterCountPerFloor>k__BackingField");
            maxEncounterCountPerFloor = serializedObject.FindProperty("<MaxEncounterCountPerFloor>k__BackingField");
            objectDefinitionsProperty = serializedObject.FindProperty("<ObjectDefinitions>k__BackingField");
            minObjectCountPerFloorProperty = serializedObject.FindProperty("<MinObjectCountPerFloor>k__BackingField");
            maxObjectCountPerFloorProperty = serializedObject.FindProperty("<MaxObjectCountPerFloor>k__BackingField");
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
            EditorGUILayout.PropertyField(vegetationPresentationProperty);

            if (isProceduralProperty.boolValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Procedural generation",
                    EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(roomTemplateCatalogProperty);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("<LengthProfiles>k__BackingField"), true);
                EditorGUILayout.PropertyField(minFloorCountProperty);
                EditorGUILayout.PropertyField(maxFloorCountProperty);
                EditorGUILayout.PropertyField(minRoomCountProperty);
                EditorGUILayout.PropertyField(maxRoomCountProperty);
                EditorGUILayout.PropertyField(minOptionalRoomCountProperty);
                EditorGUILayout.PropertyField(maxOptionalRoomCountProperty);
                EditorGUILayout.PropertyField(minLoopConnectionsProperty);
                EditorGUILayout.PropertyField(maxLoopConnectionsProperty);
                EditorGUILayout.PropertyField(minProgressionGateCountProperty);
                EditorGUILayout.PropertyField(maxProgressionGateCountProperty);

                EditorGUILayout.PropertyField(bossEncounterDefinitionProperty);
                EditorGUILayout.PropertyField(minEncounterCountPerFloor);
                EditorGUILayout.PropertyField(maxEncounterCountPerFloor);
                EditorGUILayout.PropertyField(allowEncounterDefinitionRepeats);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Dungeon objects", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(minObjectCountPerFloorProperty);
                EditorGUILayout.PropertyField(maxObjectCountPerFloorProperty);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(encounterDefinitionsProperty);
            EditorGUILayout.PropertyField(objectDefinitionsProperty);

            serializedObject.ApplyModifiedProperties();

            if (((DungeonDefinition)target).VegetationPresentation != null)
            {
                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying
                    || Godmist.Views.Game.GameBootstrap.Instance == null))
                    if (UnityEngine.GUILayout.Button("Wejdź do prototypu (seed 42)"))
                    {
                        var definition = (DungeonDefinition)target;
                        if (definition.Id == "dungeon.forest.prototype")
                            definition = ForestPrototypeSetup.LoadImportedPrototype();
                        if (!Godmist.Views.Game.GameBootstrap.Instance.TryEnterDungeon(definition, 42))
                            UnityEngine.Debug.LogWarning("Nie można wejść do prototypu: zakończ aktywny loch lub walkę i poczekaj na zakończenie ładowania.");
                    }
            }
        }
    }
}
