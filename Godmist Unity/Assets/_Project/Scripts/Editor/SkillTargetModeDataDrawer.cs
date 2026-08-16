using Godmist.Core.Enums;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(SkillTargetModeData))]
    public class SkillTargetModeDataDrawer : PropertyDrawer
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

            var targetType = property.FindPropertyRelative(
                "targetType");

            var targetingType = property.FindPropertyRelative(
                "targetingType");

            var absoluteRange = property.FindPropertyRelative(
                "absoluteRange");

            var relativeRange = property.FindPropertyRelative(
                "relativeRange");

            var y = position.y;

            DrawField(position, ref y, targetType);
            var selectedType =
                (TargetType)targetType.enumValueIndex;

            if (selectedType != TargetType.Caster)
            {
                DrawField(position, ref y, targetingType);
            }

            var showsAbsoluteRange =
                selectedType == TargetType.Single
                || selectedType == TargetType.AreaAroundCaster
                || selectedType == TargetType.AreaAroundTile;

            var supportsRelativeRange =
                SkillTargetModeData.SupportsRelativeRange(
                    selectedType);
            if (showsAbsoluteRange)
            {
                DrawField(
                    position,
                    ref y,
                    absoluteRange,
                    true,
                    "Absolute Range");
            }
            if (supportsRelativeRange)
            {
                DrawField(
                    position,
                    ref y,
                    relativeRange,
                    true,
                    "Relative Range");
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var targetType = property.FindPropertyRelative(
                "targetType");

            var selectedType =
                (TargetType)targetType.enumValueIndex;

            var showsAbsoluteRange =
                selectedType == TargetType.Single
                || selectedType == TargetType.AreaAroundCaster
                || selectedType == TargetType.AreaAroundTile;

            var supportsRelativeRange =
                SkillTargetModeData.SupportsRelativeRange(
                    selectedType);

            var height = 0f;

            AddHeight(ref height, targetType);
            if (selectedType != TargetType.Caster)
            {
                AddHeight(
                    ref height,
                    property.FindPropertyRelative(
                        "targetingType"));
            }

            if (showsAbsoluteRange)
            {
                AddHeight(
                    ref height,
                    property.FindPropertyRelative(
                        "absoluteRange"),
                    true);
            }

            if (supportsRelativeRange)
            {
                AddHeight(
                    ref height,
                    property.FindPropertyRelative(
                        "relativeRange"),
                    true);
            }

            return height;
        }

        private static void DrawField(
            Rect position,
            ref float y,
            SerializedProperty property,
            bool includeChildren = false,
            string label = null)
        {
            var height = EditorGUI.GetPropertyHeight(
                property,
                includeChildren);

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            var content = string.IsNullOrEmpty(label)
                ? null
                : new GUIContent(label);

            EditorGUI.PropertyField(
                fieldRect,
                property,
                content,
                includeChildren);

            y += height + Spacing;
        }

        private static void AddHeight(
            ref float totalHeight,
            SerializedProperty property,
            bool includeChildren = false)
        {
            if (totalHeight > 0f)
            {
                totalHeight += Spacing;
            }

            totalHeight += EditorGUI.GetPropertyHeight(
                property,
                includeChildren);
        }
    }
}
