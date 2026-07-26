using Godmist.Core.Enums;
using Godmist.Data.Effects;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(
        typeof(SkillEffectConditionData))]
    public class SkillEffectConditionDataDrawer :
        PropertyDrawer
    {
        private const float Spacing = 2f;

        public override bool CanCacheInspectorGUI(
            SerializedProperty property)
        {
            return false;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(
                position,
                label,
                property);

            var conditionType = property
                .FindPropertyRelative("conditionType");

            var statusId = property
                .FindPropertyRelative("statusId");

            var healthThreshold = property
                .FindPropertyRelative(
                    "healthPercentThreshold");

            var y = position.y;

            DrawField(
                position,
                ref y,
                conditionType);

            var selectedCondition =
                (EffectConditionType)
                conditionType.enumValueIndex;

            if (selectedCondition
                    == EffectConditionType.TargetHasStatus
                || selectedCondition
                    == EffectConditionType.CasterHasStatus)
            {
                DrawField(
                    position,
                    ref y,
                    statusId);
            }

            if (selectedCondition
                    == EffectConditionType.TargetHealthBelowPercent
                || selectedCondition
                    == EffectConditionType.CasterHealthBelowPercent)
            {
                DrawField(
                    position,
                    ref y,
                    healthThreshold);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var conditionType = property
                .FindPropertyRelative("conditionType");

            var selectedCondition =
                (EffectConditionType)
                conditionType.enumValueIndex;

            var height =
                EditorGUIUtility.singleLineHeight;

            if (selectedCondition
                    == EffectConditionType.TargetHasStatus
                || selectedCondition
                    == EffectConditionType.CasterHasStatus
                || selectedCondition
                    == EffectConditionType.TargetHealthBelowPercent
                || selectedCondition
                    == EffectConditionType.CasterHealthBelowPercent)
            {
                height += Spacing
                    + EditorGUIUtility.singleLineHeight;
            }

            return height;
        }

        private static void DrawField(
            Rect position,
            ref float y,
            SerializedProperty property)
        {
            var height =
                EditorGUI.GetPropertyHeight(property);

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            EditorGUI.PropertyField(
                fieldRect,
                property);

            y += height + Spacing;
        }
    }
}