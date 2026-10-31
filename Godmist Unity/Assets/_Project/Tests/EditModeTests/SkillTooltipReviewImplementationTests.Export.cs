using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class SkillTooltipReviewImplementationTests
    {
        [Serializable] public sealed class ReviewCard {public string title,body,meta,character,file;}
        [Serializable] public sealed class ReviewGallery {public string save; public ReviewCard[] cards;}
        [Test,Explicit("Native production tooltip screenshots from restored actual characters; no save writes.")]
        public void ExportActualCharacterScreens()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode,Is.False);
            var output=Path.GetFullPath("../LocalHandoffs/skill-tooltip-review-20261027/implementation");Directory.CreateDirectory(output);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var manager=new SaveManager();Assert.That(manager.TryGetSaveSummaries(out var saves,out var error),Is.True,error);
            Godmist.Runtime.Game.GameSession restored=null;string save=null;
            foreach(var summary in saves.OrderByDescending(s=>s.FilePath.Contains("manual_3a37053be336413894c86e2a3cf36f27")))
            {
                if(!manager.TryLoadSave(summary,catalog,out var loaded,out _))continue;
                if(loaded.CharactersById.Values.Count(c=>c.ClassData?.Id is "class.warrior" or "class.scout" or "class.mage" or "class.paladin")<4)continue;
                restored=loaded;save=summary.FilePath;break;
            }
            Assert.That(restored,Is.Not.Null);
            var old=UIWorkspaceTooltipView.ActiveInstance;var oldRender=RenderTexture.active;
            var owned=new List<Object>();
            T Own<T>(T value)where T:Object{owned.Add(value);return value;}
            var cards=new List<ReviewCard>();
            const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
            try
            {
                var canvasRoot=Own(new GameObject("Review evidence canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)));
                canvasRoot.hideFlags=HideFlags.HideAndDontSave;
                var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
                var cameraRoot=Own(new GameObject("Review evidence camera",typeof(Camera)));cameraRoot.hideFlags=HideFlags.HideAndDontSave;
                var camera=cameraRoot.GetComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.075f,.09f,1);camera.cullingMask=1<<31;
                var render=Own(new RenderTexture(640,1400,24));camera.targetTexture=render;canvas.worldCamera=camera;canvas.planeDistance=1;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
                void Capture(ReviewCard card,Action<UIWorkspaceTooltipView> build)
                {
                    var root=Object.Instantiate(prefab,canvasRoot.transform);root.SetActive(true);
                    try
                    {
                        var view=root.GetComponent<UIWorkspaceTooltipView>();
                        typeof(UIWorkspaceTooltipView).GetMethod("Awake",Hidden).Invoke(view,null);build(view);
                        typeof(UIWorkspaceTooltipView).GetMethod("SetContent",Hidden).Invoke(view,new object[]{null,card.title,card.body,card.meta,false});
                        var rect=(RectTransform)typeof(UIWorkspaceTooltipView).GetField("tooltipRect",Hidden).GetValue(view);
                        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-20);
                        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                        Canvas.ForceUpdateCanvases();foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate();
                        typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent",Hidden).Invoke(view,null);Canvas.ForceUpdateCanvases();rect.anchoredPosition=new Vector2(0,-20);
                        var title=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField("titleText",Hidden).GetValue(view);
                        title.ForceMeshUpdate();
                        var titleBounds=new Vector3[4];rect.GetWorldCorners(titleBounds);
                        foreach(var c in title.textInfo.characterInfo.Where(c=>c.isVisible))
                        {
                            var top=title.transform.TransformPoint(c.topLeft);
                            var bottom=title.transform.TransformPoint(c.bottomRight);
                            Assert.That(top.y,Is.LessThanOrEqualTo(titleBounds[1].y),card.title+" title escaped top");
                            Assert.That(bottom.x,Is.LessThanOrEqualTo(titleBounds[2].x),card.title+" title escaped right");
                        }
                        if(card.title=="Obrażenia: Cięcie")
                        {
                            var body=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField("bodyText",Hidden).GetValue(view);
                            body.ForceMeshUpdate();
                            Assert.That(body.rectTransform.rect.width,Is.GreaterThanOrEqualTo(rect.rect.width-48),"Calculation columns must fill the panel");
                            Assert.That(body.textInfo.lineCount,Is.EqualTo(4),"Compact calculation rows must not overlap/wrap their values");
                        }
                        camera.Render();RenderTexture.active=render;
                        var corners=new Vector3[4];rect.GetWorldCorners(corners);var low=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var high=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                        var x=Mathf.Max(0,Mathf.FloorToInt(low.x)-8);var y=Mathf.Max(0,Mathf.FloorToInt(low.y)-8);
                        var width=Mathf.Min(640-x,Mathf.CeilToInt(high.x-low.x)+16);var height=Mathf.Min(1400-y,Mathf.CeilToInt(high.y-low.y)+16);
                        Assert.That(width,Is.GreaterThan(0));Assert.That(height,Is.GreaterThan(0));
                        var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);
                        try{pixels.ReadPixels(new Rect(x,y,width,height),0,0);pixels.Apply();card.file=(cards.Count+1).ToString("00")+".png";File.WriteAllBytes(Path.Combine(output,card.file),pixels.EncodeToPNG());}finally{Object.DestroyImmediate(pixels);}
                        cards.Add(card);
                    }finally{Object.DestroyImmediate(root);}
                }
                foreach(var character in restored.CharactersById.Values.Where(c=>c.ClassData?.Id is "class.warrior" or "class.scout" or "class.mage" or "class.paladin"))
                {
                    var before=JsonUtility.ToJson(character);
                    var chosen=character.ClassData.Id switch{
                        "class.warrior"=>new[]{"WarriorCut","WarriorWhirlwind","WarriorKick"},
                        "class.scout"=>new[]{"ScoutAimedThrow","ScoutQuickStrikes","ScoutPoisonedDagger"},
                        "class.mage"=>new[]{"MageFireball","MageShield","MageOffering"},
                        _=>new[]{"PaladinGroupHeal","PaladinHolyRegeneration","PaladinJudgementField"}};
                    foreach(var name in chosen)
                    {
                        var skill=Skill(name);Assert.That(skill,Is.Not.Null,name);
                        var card=new ReviewCard{title=skill.DisplayName,character=character.Definition.DisplayName+", poziom "+character.Level};
                        Capture(card,view=>{card.body=view.BuildLoadoutSkillDetails(skill,character);card.meta=UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,character);});
                    }
                    var tree=character.ClassData.SkillTrees[0];var node=tree.Nodes.First(n=>n.GrantedSkill==null&&n.EffectiveMaxRanks>1);
                    var talentCard=new ReviewCard{title=node.DisplayName,character=character.Definition.DisplayName+" — ranga 0"};
                    Capture(talentCard,view=>{talentCard.body=SkillTreeNodeView.BuildDetails(node,0,view,character);talentCard.meta="Ranga: 0/"+node.EffectiveMaxRanks;});
                    var preview=SkillTreeRankPreview.Create(character,node,1);
                    var currentCard=new ReviewCard{title=node.DisplayName,character=character.Definition.DisplayName+" — podgląd rangi 1"};
                    Capture(currentCard,view=>{currentCard.body=SkillTreeNodeView.BuildDetails(node,1,view,preview);currentCard.meta="Ranga: 1/"+node.EffectiveMaxRanks;});
                    if(character.ClassData.Id=="class.warrior")
                    {
                        var cut=Skill("WarriorCut");var buffed=new BattleUnit{Character=preview};
                        WarriorCombatSystem.Variable(buffed,buffed,GameplayVariableType.DamageDealtPercent,.2f,3,"review.preview.damage","Okrzyk Wojenny");
                        var calc=new ReviewCard{title="Obrażenia: Cięcie",character=character.Definition.DisplayName+" — wyposażenie z zapisu, talent 1, buff +20%"};
                        Capture(calc,view=>{
                            var details=view.BuildLoadoutSkillDetails(cut,preview);
                            var entries=(Dictionary<string,Godmist.Views.UI.CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
                            var id=entries.Values.First(e=>e.Title.StartsWith("Obrażenia:")).Id;
                            calc.body=entries[id].Description;calc.meta=string.Empty;
                        });
                        var attack=new ReviewCard{title="Atak postaci",character=character.Definition.DisplayName+" — wyposażenie z zapisu"};
                        Capture(attack,view=>{
                            view.BuildLoadoutSkillDetails(cut,preview);
                            var entries=(Dictionary<string,Godmist.Views.UI.CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
                            attack.body=entries.Values.First(e=>e.Title=="Atak postaci").Description;attack.meta=string.Empty;
                            Assert.That(attack.body.Split('\n').Count(line=>!string.IsNullOrWhiteSpace(line)),Is.EqualTo(3),"Base, equipped sword and result should each use one attack range row");
                        });
                    }
                    Assert.That(JsonUtility.ToJson(character),Is.EqualTo(before),"Screenshot changed actual saved character");
                }
                var healer=restored.CharactersById.Values.First(c=>c.ClassData?.Id=="class.paladin");
                var healerBefore=JsonUtility.ToJson(healer);
                var healing=new ReviewCard{title="Siła leczenia",character=healer.Definition.DisplayName+", poziom "+healer.Level+" — Uzdrowienie"};
                Capture(healing,view=>{
                    view.BuildLoadoutSkillDetails(Skill("PaladinHeal"),healer);
                    var entries=(Dictionary<string,Godmist.Views.UI.CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
                    healing.body=entries.Values.First(e=>e.Title=="Siła leczenia").Description;healing.meta=string.Empty;
                });
                Assert.That(JsonUtility.ToJson(healer),Is.EqualTo(healerBefore),"Healing screenshot changed actual saved character");
                var warrior=restored.CharactersById.Values.First(c=>c.ClassData?.Id=="class.warrior");
                var warriorBefore=JsonUtility.ToJson(warrior);
                foreach(var title in new[]{"Odległość odepchnięcia","Szansa nałożenia: Unik","Unik — zmiana","Czas trwania: Unik"})
                {
                    var parameterCard=new ReviewCard{title=title,character=warrior.Definition.DisplayName+", poziom "+warrior.Level+" — Kopnięcie"};
                    Capture(parameterCard,view=>{
                        view.BuildLoadoutSkillDetails(Skill("WarriorKick"),warrior);
                        var entries=(Dictionary<string,Godmist.Views.UI.CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
                        parameterCard.body=entries.Values.First(e=>e.Title==title).Description;parameterCard.meta=string.Empty;
                    });
                }
                Assert.That(JsonUtility.ToJson(warrior),Is.EqualTo(warriorBefore),"Kick screenshots changed actual saved character");
                var coverage=new List<string>{"skill_id\tskill\tcharacter\tdedicated_breakdowns\tgeneric_fallbacks\tunlinked_numbers"};
                foreach(var owner in restored.CharactersById.Values.Where(c=>c.ClassData?.Id is "class.warrior" or "class.scout" or "class.mage" or "class.paladin"))
                {
                    var beforeCoverage=JsonUtility.ToJson(owner);
                    var auditRoot=Object.Instantiate(prefab,canvasRoot.transform);
                    try
                    {
                        var view=auditRoot.GetComponent<UIWorkspaceTooltipView>();
                        foreach(var skill in owner.Skills.Concat(owner.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null && s.SkillId!="skill.warrior.bandage").Distinct())
                        {
                            var rich=view.BuildLoadoutSkillDetails(skill,owner);
                            var entries=(Dictionary<string,Godmist.Views.UI.CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
                            var used=System.Text.RegularExpressions.Regex.Matches(rich,@"calculation\.[a-z0-9]+").Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value).Distinct().Select(id=>entries[id]).ToArray();
                            var fallbacks=used.Count(e=>e.Title==Godmist.Core.Utilities.UiTerms.Present(skill.DisplayName));
                            var withoutCalculations=System.Text.RegularExpressions.Regex.Replace(rich,@"<link=[""']calculation\.[^>]*>.*?</link>","");
                            var unlinked=System.Text.RegularExpressions.Regex.Matches(Plain(withoutCalculations),@"\d+(?:[.,]\d+)?(?:%| pp)?").Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value);
                            coverage.Add(string.Join("\t",skill.SkillId,Godmist.Core.Utilities.UiTerms.Present(skill.DisplayName),owner.Definition.DisplayName,(used.Length-fallbacks).ToString(),fallbacks.ToString(),string.Join("; ",unlinked)));
                        }
                    }
                    finally{Object.DestroyImmediate(auditRoot);}
                    Assert.That(JsonUtility.ToJson(owner),Is.EqualTo(beforeCoverage),"Coverage audit changed saved character");
                }
                File.WriteAllLines(Path.Combine(output,"calculation-coverage.tsv"),coverage);
                File.WriteAllText(Path.Combine(output,"gallery.json"),JsonUtility.ToJson(new ReviewGallery{save=save,cards=cards.ToArray()},true));
                camera.targetTexture=null;
            }
            finally
            {
                RenderTexture.active=oldRender;
                foreach(var item in owned.AsEnumerable().Reverse())if(item!=null)Object.DestroyImmediate(item);
                typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,old);
            }
        }
    }
}
