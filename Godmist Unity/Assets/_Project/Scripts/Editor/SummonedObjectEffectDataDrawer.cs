using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(SummonedObjectEffectData))]
    public class SummonedObjectEffectDataDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        private static readonly SummonedObjectEffectType[] AllowedEffectTypes =
        {
            SummonedObjectEffectType.DealDamage,
            SummonedObjectEffectType.ApplyStatus,
            SummonedObjectEffectType.Heal,
            SummonedObjectEffectType.RestoreResource,
            SummonedObjectEffectType.DrainResource,
            SummonedObjectEffectType.Shield,
            SummonedObjectEffectType.ModifyStat,
            SummonedObjectEffectType.ModifyVar
        };

        public override bool CanCacheInspectorGUI(SerializedProperty property)
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
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageValueSource = property.FindPropertyRelative("damageValueSource");
            var damageMultiplier = property.FindPropertyRelative("damageMultiplier");
            var damageType = property.FindPropertyRelative("damageType");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var stacks = property.FindPropertyRelative("stacks");
            var statusDamageMultiplier = property.FindPropertyRelative("statusDamageMultiplier");
            var statusHealPercent = property.FindPropertyRelative("statusHealPercent");
            var modifierStatType = property.FindPropertyRelative("modifierStatType");
            var modifierVariableType = property.FindPropertyRelative("modifierVariableType");
            var modifierOperation = property.FindPropertyRelative("modifierOperation");
            var modifierStacking = property.FindPropertyRelative("modifierStacking");
            var modifierValue = property.FindPropertyRelative("modifierValue");

            var y = position.y;
            var selectedEffect = DrawEffectTypePopup(position, ref y, effectType);

            if (selectedEffect == SummonedObjectEffectType.DealDamage)
            {
                DrawField(position, ref y, damageValueSource);
                DrawField(position, ref y, damageMultiplier);
                DrawField(position, ref y, damageType);
            }
            else if (selectedEffect == SummonedObjectEffectType.ApplyStatus)
            {
                DrawStatusIdPopup(position, ref y, statusId);
                DrawField(position, ref y, baseChance);
                DrawField(position, ref y, durationTurns);
                DrawField(position, ref y, stacks);

                if (UsesStatusDamageMultiplier(statusId.stringValue))
                {
                    DrawField(position, ref y, statusDamageMultiplier);
                }

                if (statusId.stringValue == StatusEffectIds.Sleep)
                {
                    DrawField(position, ref y, statusHealPercent);
                }
            }
            else if (selectedEffect == SummonedObjectEffectType.ModifyStat)
            {
                DrawField(position, ref y, modifierStatType);
                DrawField(position, ref y, modifierOperation);

                if ((StatModifierOperation)modifierOperation.enumValueIndex
                    == StatModifierOperation.Percent)
                {
                    DrawField(position, ref y, modifierStacking);
                }

                DrawField(position, ref y, modifierValue);
            }
            else if (selectedEffect == SummonedObjectEffectType.ModifyVar)
            {
                DrawField(position, ref y, modifierVariableType);
                DrawField(position, ref y, modifierStacking);
                DrawField(position, ref y, modifierValue);
            }
            else if (selectedEffect == SummonedObjectEffectType.Heal
                     || selectedEffect == SummonedObjectEffectType.RestoreResource
                     || selectedEffect == SummonedObjectEffectType.DrainResource
                     || selectedEffect == SummonedObjectEffectType.Shield)
            {
                DrawField(position, ref y, baseValue);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var effectType = property.FindPropertyRelative("effectType");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageValueSource = property.FindPropertyRelative("damageValueSource");
            var damageMultiplier = property.FindPropertyRelative("damageMultiplier");
            var damageType = property.FindPropertyRelative("damageType");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var stacks = property.FindPropertyRelative("stacks");
            var statusDamageMultiplier = property.FindPropertyRelative("statusDamageMultiplier");
            var statusHealPercent = property.FindPropertyRelative("statusHealPercent");
            var modifierStatType = property.FindPropertyRelative("modifierStatType");
            var modifierVariableType = property.FindPropertyRelative("modifierVariableType");
            var modifierOperation = property.FindPropertyRelative("modifierOperation");
            var modifierStacking = property.FindPropertyRelative("modifierStacking");
            var modifierValue = property.FindPropertyRelative("modifierValue");

            var height = EditorGUIUtility.singleLineHeight;
            var selectedEffect =
                (SummonedObjectEffectType)effectType.intValue;

            if (selectedEffect == SummonedObjectEffectType.DealDamage)
            {
                AddHeight(ref height, damageValueSource);
                AddHeight(ref height, damageMultiplier);
                AddHeight(ref height, damageType);
            }
            else if (selectedEffect == SummonedObjectEffectType.ApplyStatus)
            {
                AddHeight(ref height, statusId);
                AddHeight(ref height, baseChance);
                AddHeight(ref height, durationTurns);
                AddHeight(ref height, stacks);

                if (UsesStatusDamageMultiplier(statusId.stringValue))
                {
                    AddHeight(ref height, statusDamageMultiplier);
                }

                if (statusId.stringValue == StatusEffectIds.Sleep)
                {
                    AddHeight(ref height, statusHealPercent);
                }
            }
            else if (selectedEffect == SummonedObjectEffectType.ModifyStat)
            {
                AddHeight(ref height, modifierStatType);
                AddHeight(ref height, modifierOperation);

                if ((StatModifierOperation)modifierOperation.enumValueIndex
                    == StatModifierOperation.Percent)
                {
                    AddHeight(ref height, modifierStacking);
                }

                AddHeight(ref height, modifierValue);
            }
            else if (selectedEffect == SummonedObjectEffectType.ModifyVar)
            {
                AddHeight(ref height, modifierVariableType);
                AddHeight(ref height, modifierStacking);
                AddHeight(ref height, modifierValue);
            }
            else if (selectedEffect == SummonedObjectEffectType.Heal
                     || selectedEffect == SummonedObjectEffectType.RestoreResource
                     || selectedEffect == SummonedObjectEffectType.DrainResource
                     || selectedEffect == SummonedObjectEffectType.Shield)
            {
                AddHeight(ref height, baseValue);
            }

            return height;
        }

        private static SummonedObjectEffectType DrawEffectTypePopup(
            Rect position,
            ref float y,
            SerializedProperty effectType)
        {
            var labels = new string[AllowedEffectTypes.Length];

            for (var index = 0; index < AllowedEffectTypes.Length; index++)
            {
                labels[index] = AllowedEffectTypes[index].ToString();
            }

            var selectedIndex = 0;
            for (var index = 0; index < AllowedEffectTypes.Length; index++)
            {
                if (AllowedEffectTypes[index]
                    == (SummonedObjectEffectType)effectType.intValue)
                {
                    selectedIndex = index;
                    break;
                }
            }

            selectedIndex = EditorGUI.Popup(
                new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                "Effect Type",
                selectedIndex,
                labels);

            effectType.intValue = (int)AllowedEffectTypes[selectedIndex];
            y += EditorGUIUtility.singleLineHeight + Spacing;
            return AllowedEffectTypes[selectedIndex];
        }

        private static void DrawStatusIdPopup(
            Rect position,
            ref float y,
            SerializedProperty statusId)
        {
            var height = EditorGUIUtility.singleLineHeight;
            StatusEffectIdPopup.Draw(
                new Rect(position.x, y, position.width, height),
                statusId,
                "Status");
            y += height + Spacing;
        }

        private static void DrawField(
            Rect position,
            ref float y,
            SerializedProperty property)
        {
            var height = EditorGUI.GetPropertyHeight(property);
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, height),
                property);
            y += height + Spacing;
        }

        private static void AddHeight(
            ref float height,
            SerializedProperty property)
        {
            height += Spacing + EditorGUI.GetPropertyHeight(property);
        }

        private static bool UsesStatusDamageMultiplier(string statusId)
        {
            return statusId == StatusEffectIds.Bleed
                || statusId == StatusEffectIds.Poison
                || statusId == StatusEffectIds.Burn;
        }
    }
}
