using Godmist.Core.Enums;
using Godmist.Data.Quests;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(QuestRewardDefinition))]
    public class QuestRewardDefinitionDrawer : PropertyDrawer
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

            var rewardType = property.FindPropertyRelative("rewardType");
            var amount = property.FindPropertyRelative("amount");
            var itemDefinition = property.FindPropertyRelative("itemDefinition");
            var worldFlagId = property.FindPropertyRelative("worldFlagId");
            var worldFlagValue = property.FindPropertyRelative("worldFlagValue");

            var y = position.y;

            DrawField(position, ref y, rewardType);

            var selectedRewardType =
                (QuestRewardType)rewardType.enumValueIndex;

            switch (selectedRewardType)
            {
                case QuestRewardType.Experience:
                case QuestRewardType.Gold:
                case QuestRewardType.Honor:
                    DrawField(position, ref y, amount);
                    break;

                case QuestRewardType.Item:
                    DrawField(position, ref y, amount);
                    DrawField(position, ref y, itemDefinition);
                    break;

                case QuestRewardType.WorldFlag:
                    DrawField(position, ref y, worldFlagId);
                    DrawField(position, ref y, worldFlagValue);
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var rewardType = property.FindPropertyRelative("rewardType");
            var selectedRewardType =
                (QuestRewardType)rewardType.enumValueIndex;

            var height = 0f;

            AddHeight(ref height, rewardType);

            switch (selectedRewardType)
            {
                case QuestRewardType.Experience:
                case QuestRewardType.Gold:
                case QuestRewardType.Honor:
                    AddHeight(
                        ref height,
                        property.FindPropertyRelative("amount"));
                    break;

                case QuestRewardType.Item:
                    AddHeight(
                        ref height,
                        property.FindPropertyRelative("amount"));

                    AddHeight(
                        ref height,
                        property.FindPropertyRelative("itemDefinition"));
                    break;

                case QuestRewardType.WorldFlag:
                    AddHeight(
                        ref height,
                        property.FindPropertyRelative("worldFlagId"));

                    AddHeight(
                        ref height,
                        property.FindPropertyRelative("worldFlagValue"));
                    break;
            }

            return height;
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

        private static void AddHeight(
            ref float totalHeight,
            SerializedProperty property)
        {
            if (totalHeight > 0f)
            {
                totalHeight += Spacing;
            }

            totalHeight += EditorGUI.GetPropertyHeight(property);
        }
    }
}