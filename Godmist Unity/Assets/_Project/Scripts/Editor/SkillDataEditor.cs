using Godmist.Core.Enums;
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
        private SerializedProperty icon;
        private SerializedProperty tooltipDescription;
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
            icon = serializedObject.FindProperty("<Icon>k__BackingField");
            tooltipDescription = serializedObject.FindProperty(
                "<TooltipDescription>k__BackingField");
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
            if (!UsesOnlyPlacementEffects())
            {
                DrawTargetingSection();
            }
            DrawEffectsSection();

            serializedObject.ApplyModifiedProperties();
            DrawValidation(CombatDataValidator.ValidateSkill((SkillData)target));
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(skillId, new GUIContent("Skill ID"));
            EditorGUILayout.PropertyField(displayName, new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(icon, new GUIContent("Icon"));
            EditorGUILayout.PropertyField(
                tooltipDescription,
                new GUIContent("Tooltip Description"));
            EditorGUILayout.Space(8);
        }

        private void DrawCostSection()
        {
            EditorGUILayout.LabelField("Costs and Behavior", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(actionCost, new GUIContent("AP Cost (%)"));
            EditorGUILayout.PropertyField(resourceCost, new GUIContent("Resource Cost"));
            EditorGUILayout.PropertyField(resourceType, new GUIContent("Resource Type"));

            if (RequiresSkillAccuracy())
            {
                EditorGUILayout.PropertyField(skillAccuracy, new GUIContent("Skill Accuracy"));
            }

            if (SupportsToggle())
            {
                EditorGUILayout.PropertyField(isToggle, new GUIContent("Toggle"));
            }

            if (SupportsLineOfSight())
            {
                EditorGUILayout.PropertyField(
                    requiresLineOfSight,
                    new GUIContent("Requires Line of Sight"));
            }
            EditorGUILayout.Space(8);
        }

        private void DrawTargetingSection()
        {
            EditorGUILayout.LabelField("Target Modes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                targetModes,
                new GUIContent($"Target Modes ({targetModes.arraySize})"),
                true);
            EditorGUILayout.Space(8);
        }

        private void DrawEffectsSection()
        {
            EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                effects,
                new GUIContent($"Effects ({effects.arraySize})"),
                true);
            EditorGUILayout.Space(8);
        }

        private bool RequiresSkillAccuracy()
        {
            if (effects == null || effects.arraySize == 0)
            {
                return false;
            }

            for (var index = 0; index < effects.arraySize; index++)
            {
                var effect = effects.GetArrayElementAtIndex(index);

                var effectType = (EffectType)effect
                    .FindPropertyRelative("effectType")
                    .enumValueIndex;

                if (effectType == EffectType.CreateObject
                    || effectType == EffectType.Summon)
                {
                    continue;
                }

                var alwaysHits = effect
                    .FindPropertyRelative("alwaysHits")
                    .boolValue;

                if (effectType == EffectType.DealDamage)
                {
                    var damageType = (DamageType)effect
                        .FindPropertyRelative("damageType")
                        .enumValueIndex;

                    if (damageType == DamageType.Physical && !alwaysHits)
                    {
                        return true;
                    }

                    continue;
                }

                var polarity = (EffectPolarity)effect
                    .FindPropertyRelative("polarity")
                    .enumValueIndex;

                if (polarity == EffectPolarity.Negative && !alwaysHits)
                {
                    return true;
                }
            }

            return false;
        }

        private bool SupportsToggle()
        {
            if (effects == null)
            {
                return false;
            }

            for (var index = 0; index < effects.arraySize; index++)
            {
                var effectType = (EffectType)effects
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("effectType").enumValueIndex;

                if (effectType == EffectType.ApplyStatus
                    || effectType == EffectType.ModifyStat
                    || effectType == EffectType.ModifyVar
                    || effectType == EffectType.Shield)
                {
                    return true;
                }
            }

            return false;
        }

        private bool UsesOnlyPlacementEffects()
        {
            if (effects == null || effects.arraySize == 0)
            {
                return false;
            }

            for (var index = 0; index < effects.arraySize; index++)
            {
                var effectType = (EffectType)effects
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("effectType")
                    .enumValueIndex;

                if (effectType != EffectType.CreateObject
                    && effectType != EffectType.Summon)
                {
                    return false;
                }
            }

            return true;
        }

        private bool SupportsLineOfSight()
        {
            if (effects != null)
            {
                for (var index = 0; index < effects.arraySize; index++)
                {
                    var effectType = (EffectType)effects
                        .GetArrayElementAtIndex(index)
                        .FindPropertyRelative("effectType")
                        .enumValueIndex;

                    if (effectType == EffectType.CreateObject
                        || effectType == EffectType.Summon)
                    {
                        return true;
                    }
                }
            }

            if (targetModes == null)
            {
                return false;
            }

            for (var index = 0; index < targetModes.arraySize; index++)
            {
                var targetType = (TargetType)targetModes
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("targetType")
                    .enumValueIndex;

                if (targetType != TargetType.Caster)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawValidation(CombatDataValidationReport report)
        {
            EditorGUILayout.LabelField("Combat Data Validation", EditorStyles.boldLabel);

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox("SkillData is valid.", MessageType.Info);
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
