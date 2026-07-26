using Godmist.Core.Enums;
using Godmist.Data.Effects;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(SkillEffectData))]
    public class SkillEffectDataDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override bool CanCacheInspectorGUI(SerializedProperty property) { return false; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var effectType = property.FindPropertyRelative("effectType");
            var targetModeIndex = property.FindPropertyRelative("targetModeIndex");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageType = property.FindPropertyRelative("damageType");
            var damageValueSource = property.FindPropertyRelative("damageValueSource");
            var damageMultiplier = property.FindPropertyRelative("damageMultiplier");
            var pushDistance = property.FindPropertyRelative("pushDistance");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var statusDamageMultiplier = property.FindPropertyRelative("statusDamageMultiplier");
            var conditions = property.FindPropertyRelative("conditions");

            var y = position.y;

            DrawField(position, ref y, effectType);
            DrawTargetModePopup(position, ref y, property, targetModeIndex);

            var selectedEffect = (EffectType)effectType.enumValueIndex;

            if (selectedEffect == EffectType.DealDamage)
            {
                DrawField(position, ref y, damageType);
                DrawField(position, ref y, damageValueSource);
                DrawField(position, ref y, damageMultiplier);
            }
            else if (selectedEffect == EffectType.Push)
            {
                DrawField(position, ref y, pushDistance);
            }
            else if (selectedEffect == EffectType.Heal || selectedEffect == EffectType.RestoreResource
                || selectedEffect == EffectType.Shield)
            {
                DrawField(position, ref y, baseValue);
            }
            else if (selectedEffect == EffectType.ApplyStatus)
            {
                DrawField(position, ref y, statusId);
                DrawField(position, ref y, baseChance);
                DrawField(position, ref y, durationTurns);
                DrawField(position, ref y, statusDamageMultiplier);
            }
            DrawField(position, ref y, conditions, true);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var effectType = property.FindPropertyRelative("effectType");
            var targetModeIndex = property.FindPropertyRelative("targetModeIndex");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageType = property.FindPropertyRelative("damageType");
            var damageValueSource = property.FindPropertyRelative("damageValueSource");
            var damageMultiplier = property.FindPropertyRelative("damageMultiplier");
            var pushDistance = property.FindPropertyRelative("pushDistance");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var statusDamageMultiplier = property.FindPropertyRelative("statusDamageMultiplier");
            var conditions = property.FindPropertyRelative("conditions");

            var height = 0f;

            AddHeight(ref height, effectType);

            AddHeight(ref height, targetModeIndex);

            var selectedEffect = (EffectType)effectType.enumValueIndex;

            if (selectedEffect == EffectType.DealDamage)
            {
                AddHeight(ref height, damageType);
                AddHeight(ref height, damageValueSource);
                AddHeight(ref height, damageMultiplier);
            }
            else if (selectedEffect == EffectType.Push)
            {
                AddHeight(ref height, pushDistance);
            }
            else if (selectedEffect == EffectType.Heal || selectedEffect == EffectType.RestoreResource 
                || selectedEffect == EffectType.Shield)
            {
                AddHeight(ref height, baseValue);
            }
            else if (selectedEffect == EffectType.ApplyStatus)
            {
                AddHeight(ref height, statusId);
                AddHeight(ref height, baseChance);
                AddHeight(ref height, durationTurns);
                AddHeight(ref height, statusDamageMultiplier);
            }

            AddHeight(ref height, conditions, true);

            return height;
        }

        private static void DrawField(
            Rect position,
            ref float y,
            SerializedProperty property,
            bool includeChildren = false)
        {
            var height = EditorGUI.GetPropertyHeight(
                property,
                includeChildren);

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            EditorGUI.PropertyField(
                fieldRect,
                property,
                includeChildren);

            y += height + Spacing;
        }

        private static void DrawTargetModePopup(
            Rect position,
            ref float y,
            SerializedProperty effectProperty,
            SerializedProperty targetModeIndex)
        {
            var height = EditorGUIUtility.singleLineHeight;
            var fieldRect = new Rect(position.x, y, position.width, height);
            var targetModes = effectProperty.serializedObject.FindProperty("targetModes");

            if (targetModes == null || targetModes.arraySize == 0)
            {
                EditorGUI.PropertyField(
                    fieldRect,
                    targetModeIndex,
                    new GUIContent("Target Mode"));

                y += height + Spacing;
                return;
            }

            var labels = new string[targetModes.arraySize];

            for (var index = 0; index < targetModes.arraySize; index++)
            {
                var targetMode = targetModes.GetArrayElementAtIndex(index);
                var targetType = targetMode.FindPropertyRelative("targetType");
                var targetingType = targetMode.FindPropertyRelative("targetingType");
                var targetTypeName = targetType.enumDisplayNames[targetType.enumValueIndex];
                var selectedTargetType = (TargetType)targetType.enumValueIndex;

                if (selectedTargetType == TargetType.Caster)
                {
                    labels[index] = $"{index} - {targetTypeName}";
                }
                else
                {
                    var targetingTypeName = targetingType.enumDisplayNames[targetingType.enumValueIndex];
                    labels[index] = $"{index} - {targetTypeName} / {targetingTypeName}";
                }
            }

            var selectedIndex = Mathf.Clamp(
                targetModeIndex.intValue,
                0,
                targetModes.arraySize - 1);

            targetModeIndex.intValue = EditorGUI.Popup(
                fieldRect,
                "Target Mode",
                selectedIndex,
                labels);

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
