using System;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(SkillEffectConditionData))]
    public class SkillEffectConditionDataDrawer : PropertyDrawer
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
            EditorGUI.BeginProperty(position, label, property);

            var conditionType = property.FindPropertyRelative("conditionType");
            var statusId = property.FindPropertyRelative("statusId");
            var healthThreshold = property.FindPropertyRelative(
                "healthPercentThreshold");
            var referencedEffectIndex = property.FindPropertyRelative(
                "referencedEffectIndex");

            var y = position.y;

            DrawField(position, ref y, conditionType);

            var selectedCondition = (EffectConditionType)
                conditionType.enumValueIndex;

            if (selectedCondition == EffectConditionType.TargetHasStatus
                || selectedCondition == EffectConditionType.CasterHasStatus)
            {
                DrawStatusIdPopup(position, ref y, statusId);
            }

            if (RequiresEffectReference(selectedCondition))
            {
                DrawReferencedEffectPopup(
                    position,
                    ref y,
                    property,
                    referencedEffectIndex);
            }

            if (selectedCondition == EffectConditionType.TargetHealthBelowPercent
                || selectedCondition == EffectConditionType.CasterHealthBelowPercent)
            {
                DrawField(position, ref y, healthThreshold);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var conditionType = property.FindPropertyRelative("conditionType");

            var selectedCondition = (EffectConditionType)
                conditionType.enumValueIndex;

            var height = EditorGUIUtility.singleLineHeight;

            if (RequiresEffectReference(selectedCondition))
            {
                height += Spacing + EditorGUIUtility.singleLineHeight;
            }

            if (selectedCondition == EffectConditionType.TargetHasStatus
                || selectedCondition == EffectConditionType.CasterHasStatus
                || selectedCondition == EffectConditionType.TargetHealthBelowPercent
                || selectedCondition == EffectConditionType.CasterHealthBelowPercent)
            {
                height += Spacing + EditorGUIUtility.singleLineHeight;
            }

            return height;
        }

        private static void DrawReferencedEffectPopup(
            Rect position,
            ref float y,
            SerializedProperty conditionProperty,
            SerializedProperty referencedEffectIndex)
        {
            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            var effects = conditionProperty.serializedObject
                .FindProperty("effects");

            var currentEffectIndex = GetOwningEffectIndex(
                conditionProperty.propertyPath);

            if (effects == null || currentEffectIndex <= 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.Popup(
                    fieldRect,
                    "Wynik efektu",
                    0,
                    new[] { "Brak wcześniejszego efektu" });
                EditorGUI.EndDisabledGroup();

                referencedEffectIndex.intValue = -1;
                y += EditorGUIUtility.singleLineHeight + Spacing;
                return;
            }

            var options = new string[currentEffectIndex];

            for (var index = 0; index < currentEffectIndex; index++)
            {
                var effect = effects.GetArrayElementAtIndex(index);
                var effectType = effect.FindPropertyRelative("effectType");

                var effectTypeName = effectType == null
                    ? "Nieznany"
                    : ((EffectType)effectType.enumValueIndex).ToString();

                options[index] = $"{index} - {effectTypeName}";
            }

            var selectedIndex = Mathf.Clamp(
                referencedEffectIndex.intValue,
                0,
                currentEffectIndex - 1);

            referencedEffectIndex.intValue = EditorGUI.Popup(
                fieldRect,
                "Wynik efektu",
                selectedIndex,
                options);

            y += EditorGUIUtility.singleLineHeight + Spacing;
        }

        private static int GetOwningEffectIndex(string propertyPath)
        {
            const string marker = "effects.Array.data[";

            var markerIndex = propertyPath.IndexOf(
                marker,
                StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                return -1;
            }

            var startIndex = markerIndex + marker.Length;
            var endIndex = propertyPath.IndexOf(']', startIndex);

            if (endIndex < 0)
            {
                return -1;
            }

            var indexText = propertyPath.Substring(
                startIndex,
                endIndex - startIndex);

            return int.TryParse(indexText, out var effectIndex)
                ? effectIndex
                : -1;
        }

        private static void DrawField(
            Rect position,
            ref float y,
            SerializedProperty property)
        {
            var height = EditorGUI.GetPropertyHeight(property);

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            EditorGUI.PropertyField(fieldRect, property);

            y += height + Spacing;
        }

        private static void DrawStatusIdPopup(
            Rect position,
            ref float y,
            SerializedProperty statusId)
        {
            var height = EditorGUIUtility.singleLineHeight;

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            StatusEffectIdPopup.Draw(fieldRect, statusId, "Status");

            y += height + Spacing;
        }

        private static bool RequiresEffectReference(
            EffectConditionType conditionType)
        {
            return conditionType
                == EffectConditionType.ReferencedEffectSucceeded
                || conditionType
                == EffectConditionType.ReferencedEffectDealtHealthDamage;
        }
    }
}