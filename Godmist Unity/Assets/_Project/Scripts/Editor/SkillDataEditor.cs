using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomEditor(typeof(SkillData))]
    public class SkillDataEditor : UnityEditor.Editor
    {
        private SerializedProperty skillId;
        private SerializedProperty displayName;
        private SerializedProperty skillAccuracy;
        private SerializedProperty actionCost;
        private SerializedProperty resourceCost;
        private SerializedProperty resourceType;
        private SerializedProperty isToggle;
        private SerializedProperty requiresLineOfSight;
        private SerializedProperty targetModes;
        private SerializedProperty effects;

        private void OnEnable()
        {
            skillId = serializedObject.FindProperty("<SkillId>k__BackingField");
            displayName = serializedObject.FindProperty("<DisplayName>k__BackingField");
            skillAccuracy = serializedObject.FindProperty("<SkillAccuracy>k__BackingField");
            actionCost = serializedObject.FindProperty("<ActionCost>k__BackingField");
            resourceCost = serializedObject.FindProperty("<ResourceCost>k__BackingField");
            resourceType = serializedObject.FindProperty("<ResourceType>k__BackingField");
            isToggle = serializedObject.FindProperty("<IsToggle>k__BackingField");
            requiresLineOfSight = serializedObject.FindProperty(
                "<RequiresLineOfSight>k__BackingField");

            targetModes = serializedObject.FindProperty("targetModes");
            effects = serializedObject.FindProperty("effects");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentitySection();
            DrawCostSection();
            DrawTargetingSection();
            DrawEffectsSection();

            serializedObject.ApplyModifiedProperties();

            DrawValidation(
                CombatDataValidator.ValidateSkill((SkillData)target));
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.LabelField(
                "Identyfikacja",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(skillId, new GUIContent("Skill ID"));
            EditorGUILayout.PropertyField(
                displayName,
                new GUIContent("Nazwa wyświetlana"));

            EditorGUILayout.Space(8);
        }

        private void DrawCostSection()
        {
            EditorGUILayout.LabelField(
                "Koszty i zachowanie",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                actionCost,
                new GUIContent("Koszt AP (%)"));

            EditorGUILayout.PropertyField(
                resourceCost,
                new GUIContent("Koszt zasobu"));

            EditorGUILayout.PropertyField(
                resourceType,
                new GUIContent("Typ zasobu"));

            EditorGUILayout.PropertyField(
                skillAccuracy,
                new GUIContent("Celność skilla"));

            EditorGUILayout.PropertyField(
                isToggle,
                new GUIContent("Toggle"));

            EditorGUILayout.PropertyField(
                requiresLineOfSight,
                new GUIContent("Wymaga line of sight"));

            EditorGUILayout.Space(8);
        }

        private void DrawTargetingSection()
        {
            EditorGUILayout.LabelField(
                "Tryby celu",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                targetModes,
                new GUIContent($"Target Modes ({targetModes.arraySize})"),
                true);

            EditorGUILayout.Space(8);
        }

        private void DrawEffectsSection()
        {
            EditorGUILayout.LabelField(
                "Efekty",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                effects,
                new GUIContent($"Effects ({effects.arraySize})"),
                true);

            EditorGUILayout.Space(8);
        }

        private static void DrawValidation(
            CombatDataValidationReport report)
        {
            EditorGUILayout.LabelField(
                "Walidacja danych combatowych",
                EditorStyles.boldLabel);

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "SkillData jest poprawne.",
                    MessageType.Info);
            }

            foreach (var warning in report.Warnings)
            {
                EditorGUILayout.HelpBox(
                    warning,
                    MessageType.Warning);
            }

            foreach (var error in report.Errors)
            {
                EditorGUILayout.HelpBox(
                    error,
                    MessageType.Error);
            }
        }
    }
}