using Godmist.Data.Configuration;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(GameDefineEntry))]
    public sealed class GameDefineEntryDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float DescriptionHeight = 48f;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var lineCount = 5;
            switch (GetValueType(property))
            {
                case GameDefineValueType.Integer:
                case GameDefineValueType.Float:
                    lineCount += 4;
                    break;
                case GameDefineValueType.Boolean:
                case GameDefineValueType.String:
                    lineCount += 2;
                    break;
            }

            return lineCount * EditorGUIUtility.singleLineHeight
                + DescriptionHeight
                + EditorGUIUtility.singleLineHeight
                + (lineCount + 2) * Spacing;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                property.isExpanded,
                string.IsNullOrWhiteSpace(label.text)
                    ? "Parametr"
                    : label.text,
                true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            var y = position.y + EditorGUIUtility.singleLineHeight + Spacing;
            DrawLine(ref y, position, property, "Id", "Stabilny ID");
            DrawLine(ref y, position, property, "DisplayName", "Nazwa");

            var description = property.FindPropertyRelative("Description");
            if (description != null)
            {
                var descriptionRect = new Rect(
                    position.x,
                    y,
                    position.width,
                    DescriptionHeight);
                EditorGUI.LabelField(
                    new Rect(descriptionRect.x, descriptionRect.y,
                        EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight),
                    "Opis");
                descriptionRect.xMin += EditorGUIUtility.labelWidth;
                description.stringValue = EditorGUI.TextArea(
                    descriptionRect,
                    description.stringValue ?? string.Empty);
                y += DescriptionHeight + Spacing;
            }

            DrawLine(ref y, position, property, "Category", "Kategoria");
            DrawLine(ref y, position, property, "Unit", "Jednostka");
            DrawLine(ref y, position, property, "ValueType", "Typ wartości");

            switch (GetValueType(property))
            {
                case GameDefineValueType.Integer:
                    DrawLine(ref y, position, property, "DefaultIntegerValue", "Domyślna");
                    DrawLine(ref y, position, property, "CurrentIntegerValue", "Aktualna");
                    DrawLine(ref y, position, property, "MinimumIntegerValue", "Minimum");
                    DrawLine(ref y, position, property, "MaximumIntegerValue", "Maksimum");
                    break;
                case GameDefineValueType.Float:
                    DrawLine(ref y, position, property, "DefaultFloatValue", "Domyślna");
                    DrawLine(ref y, position, property, "CurrentFloatValue", "Aktualna");
                    DrawLine(ref y, position, property, "MinimumFloatValue", "Minimum");
                    DrawLine(ref y, position, property, "MaximumFloatValue", "Maksimum");
                    break;
                case GameDefineValueType.Boolean:
                    DrawLine(ref y, position, property, "DefaultBooleanValue", "Domyślna");
                    DrawLine(ref y, position, property, "CurrentBooleanValue", "Aktualna");
                    break;
                case GameDefineValueType.String:
                    DrawLine(ref y, position, property, "DefaultStringValue", "Domyślna");
                    DrawLine(ref y, position, property, "CurrentStringValue", "Aktualna");
                    break;
            }

            EditorGUI.EndProperty();
        }

        private static void DrawLine(
            ref float y,
            Rect parent,
            SerializedProperty entry,
            string propertyName,
            string label)
        {
            var child = entry.FindPropertyRelative(propertyName);
            if (child == null) return;

            var rect = new Rect(
                parent.x,
                y,
                parent.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(rect, child, new GUIContent(label));
            y += EditorGUIUtility.singleLineHeight + Spacing;
        }

        private static GameDefineValueType GetValueType(SerializedProperty property)
        {
            var valueType = property.FindPropertyRelative("ValueType");
            return valueType == null
                ? GameDefineValueType.Float
                : (GameDefineValueType)valueType.enumValueIndex;
        }
    }
}
