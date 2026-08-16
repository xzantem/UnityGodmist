using Godmist.Data.Combat;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(EncounterDefinition))]
    public class EncounterDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            DrawValidation(
                CombatDataValidator.ValidateEncounter(
                    (EncounterDefinition)target));
        }

        private static void DrawValidation(
            CombatDataValidationReport report)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField(
                "Combat data validation",
                EditorStyles.boldLabel);

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "EncounterDefinition is valid.",
                    MessageType.Info);
            }

            foreach (var warning in report.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            foreach (var error in report.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
