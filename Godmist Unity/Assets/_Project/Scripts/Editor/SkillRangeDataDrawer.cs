using Godmist.Core.Enums;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(SkillRangeData))]
    public class SkillRangeDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var shape = property.FindPropertyRelative("shape");
            var minRange = property.FindPropertyRelative("minRange");
            var maxRange = property.FindPropertyRelative("maxRange");
            var rectangleWidth = property.FindPropertyRelative("rectangleWidth");
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var y = position.y;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), shape);
            y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), minRange);
            y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), maxRange);
            if ((RangeShape)shape.enumValueIndex == RangeShape.Rectangle)
            {
                y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), rectangleWidth);
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var shape = property.FindPropertyRelative("shape");
            var rows = (RangeShape)shape.enumValueIndex == RangeShape.Rectangle ? 4 : 3;
            return rows * EditorGUIUtility.singleLineHeight
                + (rows - 1) * EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
