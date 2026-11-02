using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI;
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
        [Serializable] public sealed class CalculationCard { public string group,character,kind,title,body,meta,file; }
        [Serializable] public sealed class CalculationGallery { public string save; public CalculationCard[] cards; public int skills,nodes,trees,calculations,variants; }
        private const BindingFlags CalculationHidden=BindingFlags.NonPublic|BindingFlags.Instance;
        private static Dictionary<string,CombatTooltipGlossary.Entry> Entries(UIWorkspaceTooltipView view)
            => (Dictionary<string,CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",CalculationHidden).GetValue(view);
        private static string GlobalDetails(UIWorkspaceTooltipView view,SkillTreeDefinition tree,int points,ResourceType resource)
            => (string)typeof(UIWorkspaceTooltipView).GetMethod("GlobalPassiveDetails",CalculationHidden).Invoke(view,new object[]{tree,points,resource});
        private static string[] CalculationIds(string rich)=>Regex.Matches(rich??"",@"calculation\.[a-z0-9]+").Cast<Match>().Select(m=>m.Value).Distinct().ToArray();
        private static void AssertNumericCoverage(string rich,string owner)
        {
            Assert.That(Regex.IsMatch(rich??"",@"Ã.|Ä.|Å.|â€|Â.|�"),Is.False,owner+" contains damaged Unicode");
            var unlinked=Regex.Replace(rich??"",@"<link=[""'][^>]*>.*?</link>","");
            unlinked=Regex.Replace(unlinked,@"<[^>]*>","");
            Assert.That(Regex.IsMatch(unlinked,@"\d"),Is.False,owner+" has numbers without value details: "+unlinked);
        }
        [Test]
        public void CompleteCalculationCoverageForEveryHeroSkillAndTalentRank()
        {
            var previous=UIWorkspaceTooltipView.ActiveInstance;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            try
            {
                var view=root.GetComponent<UIWorkspaceTooltipView>();typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,view);
                foreach(var hero in new[]{"Warrior","Scout","Mage","Paladin"})
                {
                    var character=Unit(hero).Character;var before=JsonUtility.ToJson(character);var random=UnityEngine.Random.state;
                    var skills=character.Skills.Concat(character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null&&s.SkillId!="skill.warrior.bandage").Distinct();
                    foreach(var skill in skills)
                    {
                        Entries(view).Clear();var body=view.BuildLoadoutSkillDetails(skill,character);var meta=UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,character);
                        AssertNumericCoverage(body,skill.SkillId);AssertNumericCoverage(meta,skill.SkillId+" costs");
                        foreach(var id in CalculationIds(body+meta))
                        {
                            Assert.That(Entries(view).ContainsKey(id),Is.True,skill.SkillId+" unresolved "+id);
                            Assert.That(Plain(Entries(view)[id].Description).Trim(),Is.Not.Empty,skill.SkillId+" empty "+Entries(view)[id].Title);
                            Assert.That(Regex.IsMatch(Entries(view)[id].Title+Entries(view)[id].Description,@"Ã.|Ä.|Å.|â€|Â.|�"),Is.False,skill.SkillId+" damaged calculation text");
                            Assert.That(Entries(view)[id].Title,Is.Not.EqualTo(skill.DisplayName),skill.SkillId+" generic effect bag");
                        }
                    }
                    foreach(var tree in character.ClassData.SkillTrees)
                    {
                        AssertNumericCoverage(GlobalDetails(view,tree,0,character.ResourcePool.ResourceType),tree.Id);
                        AssertNumericCoverage(GlobalDetails(view,tree,9,character.ResourcePool.ResourceType),tree.Id+" nine points");
                        foreach(var node in tree.Nodes.Where(n=>n.GrantedSkill==null))
                            for(var rank=0;rank<=node.EffectiveMaxRanks;rank++)
                                AssertNumericCoverage(SkillTreeNodeView.BuildDetails(node,rank,view,character),node.Id+" rank "+rank);
                    }
                    Assert.That(JsonUtility.ToJson(character),Is.EqualTo(before),hero+" character changed");Assert.That(UnityEngine.Random.state,Is.EqualTo(random),hero+" preview consumed RNG");
                }
            }
            finally{Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        }
        [Test]
        public void DedicatedDetailsUseResolvedTalentSourcesAndPureRuntimeValues()
        {
            var previous=UIWorkspaceTooltipView.ActiveInstance;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            try
            {
                var view=root.GetComponent<UIWorkspaceTooltipView>();typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,view);
                var mage=Unit("Mage");Rank(mage,Godmist.Systems.Combat.MageCombatSystem.Fire,"node.mage.fire.pyromania",1);
                var before=JsonUtility.ToJson(mage.Character);var random=UnityEngine.Random.state;
                view.BuildLoadoutSkillDetails(Skill("MageFireball"),mage.Character);
                var chance=Entries(view).Values.First(e=>e.Title.StartsWith("Szansa nałożenia:")&&Plain(e.Description).Contains("75%"));
                Assert.That(Plain(chance.Description),Does.Contain("10 pp").And.Contain("85%"));
                Assert.That(JsonUtility.ToJson(mage.Character),Is.EqualTo(before));
                var scout=Unit("Scout");Rank(scout,Godmist.Systems.Combat.ScoutCombatSystem.BladeTree,"node.scout.blade_dancer.series",2);Rank(scout,Godmist.Systems.Combat.ScoutCombatSystem.BladeTree,"node.scout.blade_dancer.dance_of_doom",1);
                Entries(view).Clear();view.BuildLoadoutSkillDetails(Skill("ScoutQuickStrikes"),scout.Character);
                var trials=Entries(view).Values.First(e=>e.Title.StartsWith("Dodatkowe ciosy:"));
                Assert.That(Plain(trials.Description),Does.Contain("+2").And.Contain("+1").And.Contain("3"));
                var paladin=Unit();paladin.Character.Stats.CritChance=new Godmist.Core.Types.Stat(.1f);
                Rank(paladin,Godmist.Systems.Combat.PaladinCombatSystem.Priest,"node.paladin.priest.unexpected_grace",1);Rank(paladin,Godmist.Systems.Combat.PaladinCombatSystem.Priest,"node.paladin.priest.critical_focus",2);
                Entries(view).Clear();view.BuildLoadoutSkillDetails(Skill("PaladinHeal"),paladin.Character);
                var critical=Entries(view).Values.First(e=>e.Title=="Szansa krytyczna leczenia");
                Assert.That(Plain(critical.Description),Does.Contain("10%").And.Contain("×50%").And.Contain("×200%"));
                var trace=new Godmist.Systems.Combat.CombatValueExplanation();
                var plain=Godmist.Systems.Combat.CombatSkillPreviewCalculator.CalculateDirectDamageRange(paladin,DamageType.Magical,.5f);
                var observed=Godmist.Systems.Combat.CombatSkillPreviewCalculator.CalculateDirectDamageRange(paladin,DamageType.Magical,.5f,trace);
                Assert.That(observed.Minimum,Is.EqualTo(plain.Minimum));Assert.That(observed.Maximum,Is.EqualTo(plain.Maximum));
                Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            }
            finally{Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        }
        [Test]
        public void TalentDamageBreakdownShowsTheRoundedRuntimeAttackAndCoefficient()
        {
            var previous=UIWorkspaceTooltipView.ActiveInstance;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            try
            {
                var view=root.GetComponent<UIWorkspaceTooltipView>();var unit=Unit("Warrior");
                unit.Character.Stats.MinAttack=new Godmist.Core.Types.Stat(39.26f);unit.Character.Stats.MaxAttack=new Godmist.Core.Types.Stat(55.46f);
                var node=unit.Character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).First(n=>n.Id=="node.warrior.commander.deep_wounds");
                var preview=SkillTreeRankPreview.Create(unit.Character,node,1);var source=new Godmist.Runtime.Combat.BattleUnit{Character=preview};
                var expected=Godmist.Systems.Combat.CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AverageAttack,1);
                SkillTreeNodeView.BuildDetails(node,0,view,unit.Character);
                var entry=Entries(view).Values.First(e=>e.Title.EndsWith("— obrażenia")&&e.Title.StartsWith(node.DisplayName));
                Assert.That(Plain(entry.Description),Does.Contain("Atak — średnia").And.Contain(expected.ToString()).And.Contain("×45%"));
                unit.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime(Godmist.Core.Constants.StatusEffectIds.Weakening,new(),1,3,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd));
                var weakened=SkillTreeRankPreview.Create(unit.Character,node,1);var expectedDamage=Godmist.Systems.Combat.StatusEffectSystem.CalculateStatusDamagePerTurn(new Godmist.Runtime.Combat.BattleUnit{Character=weakened},Godmist.Core.Constants.StatusEffectIds.Bleed,.45f);
                Entries(view).Clear();SkillTreeNodeView.BuildDetails(node,0,view,unit.Character);
                var reduced=Entries(view).Values.First(e=>e.Title.EndsWith("— obrażenia")&&e.Title.StartsWith(node.DisplayName));
                Assert.That(Plain(reduced.Description),Does.Contain("Osłabienie").And.Contain("×80%").And.Contain("Wynik"+expectedDamage));
            }
            finally{Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        }
        [Test,Explicit("All production value tooltips rendered from actual saved heroes; no save writes.")]
        public void ExportAllCalculationScreens()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode,Is.False);
            var output=Path.GetFullPath("../LocalHandoffs/skill-tooltip-review-20261027/all-calculations");Directory.CreateDirectory(output);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");var manager=new SaveManager();
            Assert.That(manager.TryGetSaveSummaries(out var saves,out var error),Is.True,error);
            Godmist.Runtime.Game.GameSession session=null;string savedPath=null;
            foreach(var summary in saves.OrderByDescending(s=>s.FilePath.Contains("manual_3a37053be336413894c86e2a3cf36f27")))
                if(manager.TryLoadSave(summary,catalog,out var restored,out _) && restored.CharactersById.Values.Count(c=>c.ClassData?.Id is "class.warrior" or "class.scout" or "class.mage" or "class.paladin")>=4){session=restored;savedPath=summary.FilePath;break;}
            Assert.That(session,Is.Not.Null);
            var previous=UIWorkspaceTooltipView.ActiveInstance;var previousRender=RenderTexture.active;
            var objects=new List<Object>();T Own<T>(T item)where T:Object{objects.Add(item);return item;}
            var cards=new List<CalculationCard>();var imageCache=new Dictionary<string,string>();var gallery=new CalculationGallery{save=savedPath};
            try
            {
                var canvasRoot=Own(new GameObject("All value tooltips",typeof(RectTransform),typeof(Canvas)));canvasRoot.hideFlags=HideFlags.HideAndDontSave;
                var cameraRoot=Own(new GameObject("Tooltip proof camera",typeof(Camera)));cameraRoot.hideFlags=HideFlags.HideAndDontSave;
                var camera=cameraRoot.GetComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.075f,.09f,1);camera.cullingMask=1<<31;
                var render=Own(new RenderTexture(640,1800,24));camera.targetTexture=render;var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
                var root=Object.Instantiate(prefab,canvasRoot.transform);objects.Add(root);root.SetActive(true);
                var view=root.GetComponent<UIWorkspaceTooltipView>();typeof(UIWorkspaceTooltipView).GetMethod("Awake",CalculationHidden).Invoke(view,null);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,view);
                foreach(var tr in root.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=31;
                var rect=(RectTransform)typeof(UIWorkspaceTooltipView).GetField("tooltipRect",CalculationHidden).GetValue(view);
                void Capture(CalculationCard card)
                {
                    var fingerprint=Hash128.Compute(card.title+"\n"+card.body+"\n"+card.meta).ToString();
                    if(!imageCache.TryGetValue(fingerprint,out var file))
                    {
                        typeof(UIWorkspaceTooltipView).GetMethod("SetContent",CalculationHidden).Invoke(view,new object[]{null,card.title,card.body,card.meta??"",false});
                        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-20);
                        Canvas.ForceUpdateCanvases();typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent",CalculationHidden).Invoke(view,null);Canvas.ForceUpdateCanvases();rect.anchoredPosition=new Vector2(0,-20);
                        var panel=new Vector3[4];rect.GetWorldCorners(panel);
                        foreach(var field in new[]{"titleText","bodyText","metaText"})
                        {
                            var text=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField(field,CalculationHidden).GetValue(view);if(!text.gameObject.activeSelf)continue;text.ForceMeshUpdate();
                            if(field=="bodyText")
                            {
                                var start=0;
                                foreach(var line in text.text.Split('\n'))
                                {
                                    var column=line.IndexOf("<pos=",StringComparison.Ordinal);
                                    if(column>=0)
                                    {
                                        var valuesStart=start+line.IndexOf('>',column)+1;
                                        var glyphs=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible&&c.index>=start&&c.index<start+line.Length).ToArray();
                                        var left=glyphs.Where(c=>c.index<start+column).ToArray();var right=glyphs.Where(c=>c.index>=valuesStart).ToArray();
                                        if(left.Length>0 && right.Length>0)Assert.That(left.Max(c=>c.topRight.x),Is.LessThanOrEqualTo(right[0].bottomLeft.x+.5f),card.group+" / "+card.title+" overlapping columns: "+Plain(line));
                                    }
                                    start+=line.Length+1;
                                }
                            }
                            foreach(var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible))
                            {
                                var top=text.transform.TransformPoint(c.topLeft);var bottom=text.transform.TransformPoint(c.bottomRight);
                                Assert.That(top.y,Is.LessThanOrEqualTo(panel[1].y+.01f),card.group+" / "+card.title+" escaped top");
                                Assert.That(bottom.x,Is.LessThanOrEqualTo(panel[2].x+.01f),card.group+" / "+card.title+" escaped right");
                                Assert.That(bottom.y,Is.GreaterThanOrEqualTo(panel[0].y-.01f),card.group+" / "+card.title+" escaped bottom");
                            }
                        }
                        camera.Render();RenderTexture.active=render;
                        var low=RectTransformUtility.WorldToScreenPoint(camera,panel[0]);var high=RectTransformUtility.WorldToScreenPoint(camera,panel[2]);
                        var x=Math.Max(0,Mathf.FloorToInt(low.x)-8);var y=Math.Max(0,Mathf.FloorToInt(low.y)-8);var width=Math.Min(640-x,Mathf.CeilToInt(high.x-low.x)+16);var height=Math.Min(1800-y,Mathf.CeilToInt(high.y-low.y)+16);
                        Assert.That(height,Is.GreaterThan(0));var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);
                        try{pixels.ReadPixels(new Rect(x,y,width,height),0,0);pixels.Apply();file=(imageCache.Count+1).ToString("0000")+".png";File.WriteAllBytes(Path.Combine(output,file),pixels.EncodeToPNG());}finally{Object.DestroyImmediate(pixels);}
                        imageCache[fingerprint]=file;
                    }
                    card.file=file;cards.Add(card);
                }
                void Document(string group,string name,string kind,string title,Func<(string body,string meta)> build)
                {
                    Entries(view).Clear();var data=build();AssertNumericCoverage(data.body,group);if(kind is "skill" or "variant")AssertNumericCoverage(data.meta,group+" costs");
                    var queue=new Queue<string>(CalculationIds(data.body+data.meta));var seen=new HashSet<string>();
                    Capture(new CalculationCard{group=group,character=name,kind=kind,title=title,body=data.body,meta=data.meta});
                    while(queue.Count>0)
                    {
                        var id=queue.Dequeue();if(!seen.Add(id))continue;Assert.That(Entries(view).ContainsKey(id),Is.True,id);
                        var entry=Entries(view)[id];Assert.That(Plain(entry.Description).Trim(),Is.Not.Empty,group+" / "+entry.Title);
                        foreach(var child in CalculationIds(entry.Description))queue.Enqueue(child);
                        Capture(new CalculationCard{group=group,character=name,kind="calculation",title=entry.Title,body=entry.Description,meta=""});gallery.calculations++;
                    }
                }
                foreach(var character in session.CharactersById.Values.Where(c=>c.ClassData?.Id is "class.warrior" or "class.scout" or "class.mage" or "class.paladin"))
                {
                    var before=JsonUtility.ToJson(character);var name=character.Definition.DisplayName+", poziom "+character.Level;
                    foreach(var skill in character.Skills.Concat(character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null&&s.SkillId!="skill.warrior.bandage").Distinct())
                    {Document(skill.SkillId,name,"skill",skill.DisplayName,()=>(view.BuildLoadoutSkillDetails(skill,character),UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,character)));gallery.skills++;}
                    foreach(var tree in character.ClassData.SkillTrees)
                    {
                        var points=character.SkillTreeProgress.FirstOrDefault(p=>p.TreeId==tree.Id)?.NodeInvestments.Sum(n=>n.Ranks)??0;
                        Document(tree.Id,name,"tree",tree.DisplayName,()=>(GlobalDetails(view,tree,points,character.ResourcePool.ResourceType),""));gallery.trees++;
                        foreach(var node in tree.Nodes.Where(n=>n.GrantedSkill==null))
                        {Document(node.Id,name+" — podgląd rangi 0","node",node.DisplayName,()=>(SkillTreeNodeView.BuildDetails(node,0,view,character),"Ranga: 0/"+node.EffectiveMaxRanks));gallery.nodes++;}
                    }
                    var variant=character;
                    foreach(var talent in character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Where(n=>n.GrantedSkill==null))variant=SkillTreeRankPreview.Create(variant,talent,talent.EffectiveMaxRanks);
                    foreach(var skill in variant.Skills.Concat(variant.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null&&s.SkillId!="skill.warrior.bandage").Distinct())
                    {
                        Document(skill.SkillId+".talent-preview",name+" — techniczny podgląd talentów na kopii postaci","variant",skill.DisplayName,()=>(view.BuildLoadoutSkillDetails(skill,variant),UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,variant)));gallery.variants++;
                    }
                    Assert.That(JsonUtility.ToJson(character),Is.EqualTo(before),"Export changed actual saved character");
                }
                gallery.cards=cards.ToArray();File.WriteAllText(Path.Combine(output,"gallery.json"),JsonUtility.ToJson(gallery,true));camera.targetTexture=null;
            }
            finally{RenderTexture.active=previousRender;foreach(var item in objects.AsEnumerable().Reverse())if(item!=null)Object.DestroyImmediate(item);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        }
    }
}
