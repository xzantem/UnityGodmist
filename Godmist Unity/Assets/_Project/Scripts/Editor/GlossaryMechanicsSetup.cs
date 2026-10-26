using System;
using Godmist.Views.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Editor
{
    public static class GlossaryMechanicsSetup
    {
        [MenuItem("Godmist/Content/Refresh Glossary Mechanics")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            if (glossary == null) throw new InvalidOperationException("Missing production glossary.");
            var serialized = new SerializedObject(glossary);
            var entries = serialized.FindProperty("entries");
            foreach (var source in CombatTooltipGlossary.BuiltInEntries)
            {
                SerializedProperty entry = null;
                for (var i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == source.Id)
                    { entry = entries.GetArrayElementAtIndex(i); break; }
                if (entry == null)
                {
                    var i = entries.arraySize++;
                    entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Id").stringValue = source.Id;
                    entry.FindPropertyRelative("Icon").objectReferenceValue = null;
                    entry.FindPropertyRelative("Aliases").ClearArray();
                }
                entry.FindPropertyRelative("Title").stringValue = source.Title;
                entry.FindPropertyRelative("Description").stringValue = source.Description;
                var aliases = entry.FindPropertyRelative("Aliases");
                foreach (var alias in source.Aliases)
                {
                    var exists = false;
                    for (var i = 0; i < aliases.arraySize; i++) exists |= aliases.GetArrayElementAtIndex(i).stringValue == alias;
                    if (!exists) aliases.GetArrayElementAtIndex(aliases.arraySize++).stringValue = alias;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(glossary);
            WireResourceTooltip("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatVitals.prefab");
            WireResourceTooltip("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab");
            WireResourceTooltip("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
        }

        private static void WireResourceTooltip(string path)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var changed=false;
                foreach(var view in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(view==null)continue;
                    var property=new SerializedObject(view).FindProperty("resourceSlider");
                    if(property?.objectReferenceValue is not Slider slider)continue;
                    if(slider.GetComponent<CombatSimpleTooltipTrigger>()==null)
                    {slider.gameObject.AddComponent<CombatSimpleTooltipTrigger>();changed=true;}
                    var hit=slider.GetComponent<Graphic>();
                    if(hit==null)
                    {
                        var image=slider.gameObject.AddComponent<Image>();
                        image.color=Color.clear;hit=image;changed=true;
                    }
                    if(!hit.raycastTarget){hit.raycastTarget=true;changed=true;}
                }
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
