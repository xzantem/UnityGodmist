using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(SkillData))]
    public class SkillDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            DrawValidation(
                CombatDataValidator.ValidateSkill((SkillData)target));
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
                    "SkillData jest poprawne.",
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