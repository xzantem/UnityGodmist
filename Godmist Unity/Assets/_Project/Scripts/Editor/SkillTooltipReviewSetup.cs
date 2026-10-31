using System;
using System.Text.RegularExpressions;
using Godmist.Data.Skills;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Godmist.Editor
{
    public static class SkillTooltipReviewSetup
    {
        public static void ApplyGlobalPassiveTooltip()
        {
            const string path="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillTreeWindow Variant.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponentInChildren<SkillTreeProgressionPanelView>(true);
                var serialized=new SerializedObject(view);
                var panel=(RectTransform)serialized.FindProperty("globalPassivePanel").objectReferenceValue;
                var trigger=panel.GetComponent<CombatSimpleTooltipTrigger>() ?? panel.gameObject.AddComponent<CombatSimpleTooltipTrigger>();
                var image=panel.GetComponent<Image>();if(image!=null)image.raycastTarget=true;
                serialized.FindProperty("globalPassiveTooltip").objectReferenceValue=trigger;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        [MenuItem("Godmist/Polish/Apply accepted hero skill names")]
        public static void ApplyNames()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            foreach(var guid in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/ScriptableObjects/Skills"}))
            {
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if(skill==null || skill.SkillId.StartsWith("skill.enemy."))continue;
                var name=Regex.Replace(skill.DisplayName??"",@"\\u([0-9a-fA-F]{4})",m=>((char)Convert.ToInt32(m.Groups[1].Value,16)).ToString());
                if(skill.DisplayName==name)continue;
                var serialized=new SerializedObject(skill);
                serialized.FindProperty("<DisplayName>k__BackingField").stringValue=name;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(skill);
            }
        }
    }
}
