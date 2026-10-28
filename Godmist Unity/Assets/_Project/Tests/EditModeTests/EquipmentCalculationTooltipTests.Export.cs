using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using CalculationCard=Godmist.Tests.EditMode.SkillTooltipReviewImplementationTests.CalculationCard;
using CalculationGallery=Godmist.Tests.EditMode.SkillTooltipReviewImplementationTests.CalculationGallery;

namespace Godmist.Tests.EditMode
{
    public partial class EquipmentCalculationTooltipTests
    {
        static Dictionary<string,CombatTooltipGlossary.Entry> EquipmentEntries(UIWorkspaceTooltipView view)
            => (Dictionary<string,CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",Hidden).GetValue(view);
        static IEnumerable<string> CalculationIds(string text)=>Regex.Matches(text??"", "<link=\"(calculation\\.[^\"]+)\">").Cast<Match>().Select(m=>m.Groups[1].Value);
        [Test,Explicit("Equipment stats from actual saved heroes and labeled technical examples; no save writes.")]
        public void ExportEquipmentCalculationScreens()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode,Is.False);
            var output=Path.GetFullPath("../LocalHandoffs/equipment-stat-breakdown-20261028/screens");Directory.CreateDirectory(output);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);var manager=new SaveManager();
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
                var view=root.GetComponent<UIWorkspaceTooltipView>();typeof(UIWorkspaceTooltipView).GetMethod("Awake",Hidden).Invoke(view,null);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,view);
                foreach(var tr in root.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=31;
                var rect=(RectTransform)typeof(UIWorkspaceTooltipView).GetField("tooltipRect",Hidden).GetValue(view);
                void Capture(CalculationCard card)
                {
                    var fingerprint=Hash128.Compute(card.title+"\n"+card.body+"\n"+card.meta).ToString();
                    if(!imageCache.TryGetValue(fingerprint,out var file))
                    {
                        typeof(UIWorkspaceTooltipView).GetMethod("SetContent",Hidden).Invoke(view,new object[]{null,card.title,card.body,card.meta??"",false});
                        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-20);
                        Canvas.ForceUpdateCanvases();typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent",Hidden).Invoke(view,null);Canvas.ForceUpdateCanvases();rect.anchoredPosition=new Vector2(0,-20);
                        var panel=new Vector3[4];rect.GetWorldCorners(panel);
                        foreach(var field in new[]{"titleText","bodyText","metaText"})
                        {
                            var text=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField(field,Hidden).GetValue(view);if(!text.gameObject.activeSelf)continue;text.ForceMeshUpdate();
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
                void Document(string group,string name,string kind,string title,EquipmentItemInstance documentItem,Func<(string body,string meta)> build)
                {
                    EquipmentEntries(view).Clear();var data=build();
                    if(kind is "equipment" or "variant")
                    {
                        // Exercise the production glossary-decoration path as well as the stat builder.
                        data.body=(string)typeof(UIWorkspaceTooltipView).GetMethod("LinkItemMechanics",Hidden).Invoke(view,new object[]{documentItem,data.body});
                    }
                    var queue=new Queue<string>(CalculationIds(data.body+data.meta));var seen=new HashSet<string>();
                    Capture(new CalculationCard{group=group,character=name,kind=kind,title=title,body=data.body,meta=data.meta});
                    while(queue.Count>0)
                    {
                        var id=queue.Dequeue();if(!seen.Add(id))continue;Assert.That(EquipmentEntries(view).ContainsKey(id),Is.True,id);
                        var entry=EquipmentEntries(view)[id];Assert.That(Plain(entry.Description).Trim(),Is.Not.Empty,group+" / "+entry.Title);
                        foreach(var child in CalculationIds(entry.Description))queue.Enqueue(child);
                        Capture(new CalculationCard{group=group,character=name,kind="calculation",title=entry.Title,body=entry.Description,meta=""});gallery.calculations++;
                    }
                }
                foreach(var character in session.CharactersById.Values.Where(c=>c.ClassData?.Id is "class.warrior" or "class.scout" or "class.mage" or "class.paladin"))
                {
                    var before=JsonUtility.ToJson(character);var name=character.Definition.DisplayName+", poziom "+character.Level;
                    var items=character.Inventory.Items.OfType<EquipmentItemInstance>().Concat(Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>().Select(slot=>character.Equipment.GetItem(slot)).Where(i=>i!=null)).Distinct().ToArray();
                    foreach(var item in items){Document(item.InstanceId,name,"equipment",item.DisplayName,item,()=>(view.BuildEquipmentCalculationDetails(item,character,catalog),""));gallery.skills++;}
                    Assert.That(JsonUtility.ToJson(character),Is.EqualTo(before),"Export changed saved equipment");
                }
                foreach(var definition in Definitions())
                {
                    var item=Generate(definition,EquipmentQuality.Masterpiece,RarityTier.Rare);item.UpgradeProgress=.4f;item.RecalculateStatModifiers();
                    Document(definition.Id+".example","Techniczny przykład — "+definition.RequiredClass.ClassName,"variant",item.DisplayName,item,()=>(view.BuildEquipmentCalculationDetails(item,null,catalog),""));gallery.variants++;
                }
                gallery.cards=cards.ToArray();File.WriteAllText(Path.Combine(output,"gallery.json"),JsonUtility.ToJson(gallery,true));camera.targetTexture=null;
            }
            finally{RenderTexture.active=previousRender;foreach(var item in objects.AsEnumerable().Reverse())if(item!=null)Object.DestroyImmediate(item);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        }
    }
}
