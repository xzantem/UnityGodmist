using System;
using System.IO;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using UnityEditor;

namespace Godmist.Editor
{
    public static class StunDurationSetup
    {
        [MenuItem("Godmist/Content/Apply agreed stun durations")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var name in new[] { "WarriorRightHook", "PaladinCrushingBlow", "WarriorShout" })
            {
                var path = "Assets/_Project/ScriptableObjects/Skills/SD_" + name + ".asset";
                var backup = Path.GetFullPath("../LocalHandoffs/stun-duration-20261024/before/" + path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                var data = new SerializedObject(skill); var effects = data.FindProperty("effects"); var found = false;
                for (var n = 0; n < effects.arraySize; n++)
                {
                    var effect = effects.GetArrayElementAtIndex(n);
                    if (effect.FindPropertyRelative("effectType").intValue != (int)EffectType.ApplyStatus
                        || effect.FindPropertyRelative("statusId").stringValue != StatusEffectIds.Stun) continue;
                    // Shout's empowered duration is resolved from actual rage spent at runtime.
                    effect.FindPropertyRelative("durationTurns").intValue = name == "WarriorShout" ? 1 : 2;
                    found = true;
                }
                if (!found) throw new InvalidOperationException("Missing stun in " + path);
                var description = data.FindProperty("<TooltipDescription>k__BackingField");
                description.stringValue = description.stringValue.Replace("ogłuszenie na 3 tury", "ogłuszenie na 2 tury")
                    .Replace("50 PW: 3 tury", "50 PW: 2 tury");
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(skill);
            }
        }
    }
}
