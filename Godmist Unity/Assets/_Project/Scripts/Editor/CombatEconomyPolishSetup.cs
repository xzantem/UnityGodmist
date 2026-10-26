using System;
using System.IO;
using System.Linq;
using Godmist.Data.World;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    public static class CombatEconomyPolishSetup
    {
        public const string Output = "../LocalHandoffs/combat-economy-ui-20261026";
        public const string TrackerPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_TrackedQuestHud.prefab";
        public static readonly string[] ScenePaths = { "Assets/_Project/Scenes/City/Arungard.unity", "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity" };
        public static void CaptureBefore()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            Directory.CreateDirectory(Path.Combine(Output,"before-definitions"));
            foreach(var definition in catalog.CharacterDefinitions.Where(d=>d!=null && d.Id.StartsWith("enemy.",StringComparison.Ordinal)))
            {
                var file=Path.Combine(Output,"before-definitions",definition.Id+".json");
                if(!File.Exists(file)) File.WriteAllText(file,EditorJsonUtility.ToJson(definition,true));
            }
            foreach(var path in ScenePaths.Append("Assets/_Project/ScriptableObjects/Skills/SD_ScoutAimedThrow.asset")
                .Append("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset"))
            {
                var file=Path.Combine(Output,"serialized-before",path);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                if(!File.Exists(file)) File.Copy(path,file);
            }
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            foreach(var path in ScenePaths)
            {
                var scene=SceneManager.GetSceneByPath(path);
                if(scene.isLoaded && scene.isDirty)throw new InvalidOperationException("Unsaved user scene: "+path);
            }
            CaptureBefore();
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var changed=new System.Collections.Generic.List<string>();
            foreach(var definition in catalog.CharacterDefinitions.Where(d=>d!=null))
                if(EnemyArchetypeStatsSetup.ApplyToDefinition(definition))
                { AssetDatabase.SaveAssetIfDirty(definition);changed.Add(definition.Id); }
            File.WriteAllLines(Path.Combine(Output,"authored-enemies.txt"),changed);
            var aimed=catalog.SkillDefinitions.Single(s=>s!=null&&s.SkillId=="skill.scout.aimed_throw");
            var skill=new SerializedObject(aimed);skill.FindProperty("<SkillAccuracy>k__BackingField").floatValue=120;
            skill.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(aimed);
            var glossary=AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var glossaryData=new SerializedObject(glossary);var entries=glossaryData.FindProperty("entries");
            for(var i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative("Id").stringValue=="mana")
                    entry.FindPropertyRelative("Description").stringValue=CombatTooltipGlossary.BuiltInEntries.Single(e=>e.Id=="mana").Description;
            }
            glossaryData.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(glossary);
            BuildTracker();
            foreach(var path in ScenePaths)
            {
                var scene=SceneManager.GetSceneByPath(path);var openedHere=!scene.isLoaded;
                if(openedHere)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var owner=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<QuestJournalPanelView>(true)).Single();
                    var data=new SerializedObject(owner);
                    data.FindProperty("trackedQuestHudPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<TrackedQuestHudView>(TrackerPath);
                    data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally { if(openedHere)EditorSceneManager.CloseScene(scene,true); }
            }
        }
        public static void BuildTracker()
        {
            var root=new GameObject("TrackedQuestHud",typeof(RectTransform),typeof(TrackedQuestHudView));
            try
            {
                var rect=(RectTransform)root.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one;
                rect.anchoredPosition=new Vector2(-10,-274);rect.sizeDelta=new Vector2(300,350);
                var section=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Section.prefab");
                var panel=(GameObject)PrefabUtility.InstantiatePrefab(section,root.transform);
                panel.name="TrackedQuestSection";
                foreach(var style in panel.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))style.enabled=false;
                foreach(var graphic in panel.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                var p=(RectTransform)panel.transform;p.anchorMin=p.anchorMax=p.pivot=Vector2.one;p.anchoredPosition=Vector2.zero;p.sizeDelta=new Vector2(300,0);
                var border=panel.transform.Find("Border");
                var ignored=border.GetComponent<LayoutElement>()??border.gameObject.AddComponent<LayoutElement>();ignored.ignoreLayout=true;
                var layout=panel.GetComponent<VerticalLayoutGroup>()??panel.AddComponent<VerticalLayoutGroup>();
                layout.padding=new RectOffset(14,14,12,14);layout.spacing=8;
                layout.childControlHeight=layout.childControlWidth=layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                var fitter=panel.GetComponent<ContentSizeFitter>()??panel.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                var title=panel.transform.Find("SectionTitle").GetComponent<TextMeshProUGUI>();title.text=string.Empty;
                title.enableWordWrapping=true;title.fontSize=22;title.raycastTarget=false;
                var body=panel.transform.Find("Content").gameObject;
                var objectives=body.GetComponent<TextMeshProUGUI>()??body.AddComponent<TextMeshProUGUI>();
                objectives.font=title.font;objectives.fontSharedMaterial=title.fontSharedMaterial;objectives.fontSize=18;
                objectives.color=QuestJournalPresentation.Cream;objectives.enableWordWrapping=true;objectives.raycastTarget=false;
                var data=new SerializedObject(root.GetComponent<TrackedQuestHudView>());
                data.FindProperty("content").objectReferenceValue=panel;data.FindProperty("title").objectReferenceValue=title;
                data.FindProperty("objectives").objectReferenceValue=objectives;data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,TrackerPath);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
