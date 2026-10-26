using System;
using System.IO;
using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using UnityEditor;

namespace Godmist.Editor
{
    public static class MageTiringStacksSetup
    {
        public const string SkillPath = "Assets/_Project/ScriptableObjects/Skills/SD_MageTiringSpells.asset";
        [MenuItem("Godmist/Content/Apply stacking Tiring Spells description")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var backup = Path.GetFullPath("../LocalHandoffs/mage-tiring-stacks-20261024/before-assets/" + SkillPath);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(SkillPath, backup);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath);
            var data = new SerializedObject(skill);
            data.FindProperty("<TooltipDescription>k__BackingField").stringValue = MageTiringSpellSystem.Description;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(skill);
        }
    }
}
