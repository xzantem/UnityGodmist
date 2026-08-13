using Godmist.Core.Constants;
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

            var effectType = property.FindPropertyRelative("effectType");
            var polarity = property.FindPropertyRelative("polarity");
            var alwaysHits = property.FindPropertyRelative("alwaysHits");
            var targetModeIndex = property.FindPropertyRelative("targetModeIndex");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageType = property.FindPropertyRelative("damageType");
            var damageValueSource =
                property.FindPropertyRelative("damageValueSource");
            var damageMultiplier =
                property.FindPropertyRelative("damageMultiplier");
            var displacementDistance =
                property.FindPropertyRelative("displacementDistance");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var statusDamageMultiplier =
                property.FindPropertyRelative("statusDamageMultiplier");
            var statusHealPercent =
                property.FindPropertyRelative("statusHealPercent");
            var conditions = property.FindPropertyRelative("conditions");
            var modifierStatType =
                property.FindPropertyRelative("modifierStatType");
            var modifierVariableType =
                property.FindPropertyRelative("modifierVariableType");
            var modifierOperation =
                property.FindPropertyRelative("modifierOperation");
            var modifierStacking =
                property.FindPropertyRelative("modifierStacking");
            var modifierValue =
                property.FindPropertyRelative("modifierValue");
            var teleportDestinationRange =
                property.FindPropertyRelative("teleportDestinationRange");
            var summonedObjectDefinition =
                property.FindPropertyRelative("summonedObjectDefinition");
            var objectPlacementRange =
                property.FindPropertyRelative("objectPlacementRange");
            var maxActiveObjects =
                property.FindPropertyRelative("maxActiveObjects");

            var y = position.y;
            var selectedEffect =
                (EffectType)effectType.enumValueIndex;

            DrawField(position, ref y, effectType);

            if (selectedEffect == EffectType.CreateObject)
            {
                DrawField(
                    position,
                    ref y,
                    summonedObjectDefinition,
                    true);

                DrawField(
                    position,
                    ref y,
                    objectPlacementRange,
                    true);

                DrawField(
                    position,
                    ref y,
                    maxActiveObjects);

                EditorGUI.EndProperty();
                return;
            }

            DrawTargetModePopup(
                position,
                ref y,
                property,
                targetModeIndex);

            var selectedPolarity =
                (EffectPolarity)polarity.enumValueIndex;

            if (selectedEffect != EffectType.DealDamage)
            {
                DrawField(position, ref y, polarity);

                if (selectedPolarity == EffectPolarity.Negative)
                {
                    DrawField(position, ref y, alwaysHits);
                }
            }

            if (selectedEffect == EffectType.DealDamage)
            {
                DrawField(position, ref y, damageType);
                DrawField(position, ref y, damageValueSource);
                DrawField(position, ref y, damageMultiplier);

                if ((DamageType)damageType.enumValueIndex
                    == DamageType.Physical)
                {
                    DrawField(position, ref y, alwaysHits);
                }
            }
            else if (selectedEffect == EffectType.Push
                     || selectedEffect == EffectType.Pull
                     || selectedEffect == EffectType.Fallback
                     || selectedEffect == EffectType.Charge)
            {
                DrawField(position, ref y, displacementDistance);

                if (selectedEffect == EffectType.Pull
                    || selectedEffect == EffectType.Charge)
                {
                    DrawField(position, ref y, baseChance);
                }
            }
            else if (selectedEffect == EffectType.Teleport)
            {
                DrawField(
                    position,
                    ref y,
                    teleportDestinationRange,
                    true);
            }
            else if (selectedEffect == EffectType.Heal
                     || selectedEffect == EffectType.RestoreResource
                     || selectedEffect == EffectType.Shield)
            {
                DrawField(position, ref y, baseValue);
            }
            else if (selectedEffect == EffectType.ModifyStat)
            {
                DrawField(position, ref y, modifierStatType);
                DrawField(position, ref y, modifierOperation);

                if ((StatModifierOperation)modifierOperation.enumValueIndex
                    == StatModifierOperation.Percent)
                {
                    DrawField(position, ref y, modifierStacking);
                }

                DrawField(position, ref y, modifierValue);
                DrawField(position, ref y, durationTurns);
            }
            else if (selectedEffect == EffectType.ModifyVar)
            {
                DrawField(position, ref y, modifierVariableType);
                DrawField(position, ref y, modifierStacking);
                DrawField(position, ref y, modifierValue);
                DrawField(position, ref y, durationTurns);
            }
            else if (selectedEffect == EffectType.ApplyStatus)
            {
                DrawStatusIdPopup(position, ref y, statusId);
                DrawField(position, ref y, baseChance);
                DrawField(position, ref y, durationTurns);

                if (UsesStatusDamageMultiplier(statusId.stringValue))
                {
                    DrawField(
                        position,
                        ref y,
                        statusDamageMultiplier);
                }

                if (statusId.stringValue == StatusEffectIds.Sleep)
                {
                    DrawField(
                        position,
                        ref y,
                        statusHealPercent);
                }
            }

            DrawField(position, ref y, conditions, true);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var effectType = property.FindPropertyRelative("effectType");
            var polarity = property.FindPropertyRelative("polarity");
            var alwaysHits = property.FindPropertyRelative("alwaysHits");
            var targetModeIndex = property.FindPropertyRelative("targetModeIndex");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageType = property.FindPropertyRelative("damageType");
            var damageValueSource =
                property.FindPropertyRelative("damageValueSource");
            var damageMultiplier =
                property.FindPropertyRelative("damageMultiplier");
            var displacementDistance =
                property.FindPropertyRelative("displacementDistance");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var statusDamageMultiplier =
                property.FindPropertyRelative("statusDamageMultiplier");
            var statusHealPercent =
                property.FindPropertyRelative("statusHealPercent");
            var conditions = property.FindPropertyRelative("conditions");
            var modifierStatType =
                property.FindPropertyRelative("modifierStatType");
            var modifierVariableType =
                property.FindPropertyRelative("modifierVariableType");
            var modifierOperation =
                property.FindPropertyRelative("modifierOperation");
            var modifierStacking =
                property.FindPropertyRelative("modifierStacking");
            var modifierValue =
                property.FindPropertyRelative("modifierValue");
            var teleportDestinationRange =
                property.FindPropertyRelative("teleportDestinationRange");
            var summonedObjectDefinition =
                property.FindPropertyRelative("summonedObjectDefinition");
            var objectPlacementRange =
                property.FindPropertyRelative("objectPlacementRange");
            var maxActiveObjects =
                property.FindPropertyRelative("maxActiveObjects");

            var height = 0f;
            var selectedEffect =
                (EffectType)effectType.enumValueIndex;

            AddHeight(ref height, effectType);

            if (selectedEffect == EffectType.CreateObject)
            {
                AddHeight(
                    ref height,
                    summonedObjectDefinition,
                    true);

                AddHeight(
                    ref height,
                    objectPlacementRange,
                    true);

                AddHeight(ref height, maxActiveObjects);

                return height;
            }

            AddHeight(ref height, targetModeIndex);

            var selectedPolarity =
                (EffectPolarity)polarity.enumValueIndex;

            if (selectedEffect != EffectType.DealDamage)
            {
                AddHeight(ref height, polarity);

                if (selectedPolarity == EffectPolarity.Negative)
                {
                    AddHeight(ref height, alwaysHits);
                }
            }

            if (selectedEffect == EffectType.DealDamage)
            {
                AddHeight(ref height, damageType);
                AddHeight(ref height, damageValueSource);
                AddHeight(ref height, damageMultiplier);

                if ((DamageType)damageType.enumValueIndex
                    == DamageType.Physical)
                {
                    AddHeight(ref height, alwaysHits);
                }
            }
            else if (selectedEffect == EffectType.Push
                     || selectedEffect == EffectType.Pull
                     || selectedEffect == EffectType.Fallback
                     || selectedEffect == EffectType.Charge)
            {
                AddHeight(ref height, displacementDistance);

                if (selectedEffect == EffectType.Pull
                    || selectedEffect == EffectType.Charge)
                {
                    AddHeight(ref height, baseChance);
                }
            }
            else if (selectedEffect == EffectType.Teleport)
            {
                AddHeight(
                    ref height,
                    teleportDestinationRange,
                    true);
            }
            else if (selectedEffect == EffectType.Heal
                     || selectedEffect == EffectType.RestoreResource
                     || selectedEffect == EffectType.Shield)
            {
                AddHeight(ref height, baseValue);
            }
            else if (selectedEffect == EffectType.ModifyStat)
            {
                AddHeight(ref height, modifierStatType);
                AddHeight(ref height, modifierOperation);

                if ((StatModifierOperation)modifierOperation.enumValueIndex
                    == StatModifierOperation.Percent)
                {
                    AddHeight(ref height, modifierStacking);
                }

                AddHeight(ref height, modifierValue);
                AddHeight(ref height, durationTurns);
            }
            else if (selectedEffect == EffectType.ModifyVar)
            {
                AddHeight(ref height, modifierVariableType);
                AddHeight(ref height, modifierStacking);
                AddHeight(ref height, modifierValue);
                AddHeight(ref height, durationTurns);
            }
            else if (selectedEffect == EffectType.ApplyStatus)
            {
                AddHeight(ref height, statusId);
                AddHeight(ref height, baseChance);
                AddHeight(ref height, durationTurns);

                if (UsesStatusDamageMultiplier(statusId.stringValue))
                {
                    AddHeight(
                        ref height,
                        statusDamageMultiplier);
                }

                if (statusId.stringValue == StatusEffectIds.Sleep)
                {
                    AddHeight(ref height, statusHealPercent);
                }
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
            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            var targetModes =
                effectProperty.serializedObject.FindProperty("targetModes");

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

            for (var index = 0;
                 index < targetModes.arraySize;
                 index++)
            {
                var targetMode =
                    targetModes.GetArrayElementAtIndex(index);

                var targetType =
                    targetMode.FindPropertyRelative("targetType");

                var targetingType =
                    targetMode.FindPropertyRelative("targetingType");

                var targetTypeName =
                    targetType.enumDisplayNames[targetType.enumValueIndex];

                var selectedTargetType =
                    (TargetType)targetType.enumValueIndex;

                labels[index] = selectedTargetType == TargetType.Caster
                    ? $"{index} - {targetTypeName}"
                    : $"{index} - {targetTypeName} / " +
                      $"{targetingType.enumDisplayNames[targetingType.enumValueIndex]}";
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

            StatusEffectIdPopup.Draw(
                fieldRect,
                statusId,
                "Status");

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

        private static bool UsesStatusDamageMultiplier(
            string statusId)
        {
            return statusId == StatusEffectIds.Bleed
                || statusId == StatusEffectIds.Poison
                || statusId == StatusEffectIds.Burn;
        }
    }
}
