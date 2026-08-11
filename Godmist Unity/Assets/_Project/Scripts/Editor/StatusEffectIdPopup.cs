using System;
using System.Linq;
using Godmist.Core.Constants;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class StatusEffectIdPopup
    {
        public static void Draw(
            Rect position,
            SerializedProperty statusId,
            string label)
        {
            var options = StatusEffectIds.KnownIds.ToArray();

            var selectedIndex = GetKnownStatusIndex(
                statusId.stringValue);

            selectedIndex = Mathf.Clamp(
                selectedIndex,
                0,
                options.Length - 1);

            var nextIndex = EditorGUI.Popup(
                position,
                label,
                selectedIndex,
                options);

            statusId.stringValue = options[nextIndex];
        }

        private static int GetKnownStatusIndex(string statusId)
        {
            for (var index = 0; index < StatusEffectIds.KnownIds.Count; index++)
            {
                if (string.Equals(
                        StatusEffectIds.KnownIds[index],
                        statusId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }
    }
}