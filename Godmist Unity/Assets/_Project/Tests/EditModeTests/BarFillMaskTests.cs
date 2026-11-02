using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Editor;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Tests.EditMode
{
    public class BarFillMaskTests
    {
        const string Vitals = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatVitals.prefab";
        static string Output => Path.GetFullPath("../LocalHandoffs/bar-fill-mask-20261030");
        readonly List<UnityEngine.Object> objects = new();
        [TearDown] public void TearDown(){foreach(var obj in objects.AsEnumerable().Reverse())if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);objects.Clear();}
        Slider Prototype()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Vitals);
            var view=prefab.GetComponent<CombatVitalsView>();Assert.That(view,Is.Not.Null);
            return (Slider)new SerializedObject(view).FindProperty("healthSlider").objectReferenceValue;
        }
        Slider Bar(Transform parent,int x,int y,int height,float value)
        {
            var slider=UnityEngine.Object.Instantiate(Prototype(),parent,false);objects.Add(slider.gameObject);
            slider.gameObject.SetActive(true);
            foreach(var child in slider.GetComponentsInChildren<Slider>(true))if(child!=slider)child.gameObject.SetActive(false);
            var rect=(RectTransform)slider.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(280,height);rect.localScale=Vector3.one;
            slider.minValue=0;slider.maxValue=50;slider.SetValueWithoutNotify(value);
            foreach(var text in slider.GetComponentsInChildren<TMPro.TMP_Text>(true))text.text=value.ToString("0")+"/50";
            var fill=slider.fillRect.GetComponent<Image>();fill.color=Color.red;
            return slider;
        }
        [Test,Explicit("Read-only visual baseline before scoped prefab authoring.")]
        public void CaptureBefore(){Capture("before.png",false);}
        [Test,Explicit("Authorized prefab authoring through Editor API.")]
        public void ApplyAuthoredMasks()
        {
            var changed=BarFillMaskSetup.Apply(Path.Combine(Output,"before-prefabs"));
            Assert.That(changed.Count,Is.GreaterThan(0));
            Capture("after.png",true);
        }
        [Test] public void AllAuthoredThinBarsHaveClippingAndAnOverlayBorder()
        {
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{BarFillMaskSetup.PrefabFolder}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var slider in prefab.GetComponentsInChildren<Slider>(true))
                {
                    if(slider.fillRect==null||slider.handleRect!=null)continue;
                    if(slider.GetComponent<UIBarFillMask>() is { enabled:false })continue;
                    var background=slider.transform.Find("Background")?.GetComponent<Image>();
                    if(background==null || background.sprite==null || background.sprite.name is not ("slider_track" or "bar_frame"))continue;
                    var mask=slider.GetComponent<UIBarFillMask>();Assert.That(mask,Is.Not.Null,path+"/"+slider.name);
                    Assert.That(slider.fillRect.parent.GetComponent<RectMask2D>(),Is.Not.Null,path);
                    Assert.That(mask.Border.enabled,Is.True,path);Assert.That(mask.Border.fillCenter,Is.False,path);
                    Assert.That(mask.Border.transform.GetSiblingIndex(),Is.GreaterThan(mask.FillArea.GetSiblingIndex()),path);
                    count++;
                }
                foreach(var image in prefab.GetComponentsInChildren<Image>(true))
                    if(image.sprite!=null && (image.sprite.name is "slider_track" or "bar_frame")&&image.transform.Find("Fill")!=null)
                        Assert.Fail("Unmasked image bar: "+path+"/"+image.name);
            }
            Assert.That(count,Is.GreaterThan(10));
        }
        [TestCase(6,0)][TestCase(6,10)][TestCase(6,50)]
        [TestCase(12,0)][TestCase(12,10)][TestCase(12,50)]
        [TestCase(18,0)][TestCase(18,10)][TestCase(18,50)]
        public void LayoutKeepsTheFillInsideBothBorders(int height,int value)
        {
            var root=new GameObject("Bar layout",typeof(RectTransform));objects.Add(root);
            var slider=Bar(root.transform,0,0,height,value);var mask=slider.GetComponent<UIBarFillMask>();Assert.That(mask,Is.Not.Null);
            mask.Apply();Canvas.ForceUpdateCanvases();mask.Apply();
            var border=mask.Background.sprite.border/(mask.Background.pixelsPerUnit*mask.Background.pixelsPerUnitMultiplier);
            Assert.That(mask.FillArea.offsetMin.y,Is.EqualTo(border.y).Within(.001));
            Assert.That(mask.FillArea.offsetMax.y,Is.EqualTo(-border.w).Within(.001));
            Assert.That(mask.FillArea.rect.height,Is.GreaterThanOrEqualTo(1.99f));
            Assert.That(slider.normalizedValue,Is.EqualTo(value/50f).Within(.001));
            Assert.That(slider.fillRect.anchorMax.x,Is.EqualTo(value/50f).Within(.001));
        }
        [Test] public void ShieldOverlayIsClippedInsideTheHealthBar()
        {
            var root=new GameObject("Shield layout",typeof(RectTransform));objects.Add(root);
            var slider=Bar(root.transform,0,0,12,10);var mask=slider.GetComponent<UIBarFillMask>();mask.Apply();
            Image overlay=null;CombatHealthPresentation.UpdateShieldOverlay(ref overlay,slider,20,50);
            Assert.That(overlay.transform.parent,Is.SameAs(mask.FillArea));Assert.That(overlay.maskable,Is.True);
            Assert.That(overlay.rectTransform.pivot.y,Is.EqualTo(1));
            Assert.That(overlay.rectTransform.anchorMax.x,Is.EqualTo(.4f).Within(.001));
            CombatHealthPresentation.UpdateShieldOverlay(ref overlay,slider,0,50);Assert.That(overlay.gameObject.activeSelf,Is.False);
        }
        [Test] public void AuthoredShieldStripSharesHealthInteriorWithoutCoveringTheBorder()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Vitals));objects.Add(root);
            var view=root.GetComponent<CombatVitalsView>();var data=new SerializedObject(view);
            var health=(Slider)data.FindProperty("healthSlider").objectReferenceValue;
            var shield=(Slider)data.FindProperty("shieldSlider").objectReferenceValue;
            var character=new Godmist.Runtime.Characters.CharacterInstance{CurrentHealth=10,
                Stats=new Godmist.Runtime.Characters.CharacterStats{MaxHealth=new Godmist.Core.Types.Stat(50)},
                ShieldPool=new Godmist.Runtime.Characters.ShieldPool()};
            character.ShieldPool.AddLayer(20);view.Bind(new Godmist.Runtime.Combat.BattleUnit{Character=character});
            var mask=health.GetComponent<UIBarFillMask>();mask.Apply();
            Assert.That(shield.fillRect.parent.GetComponent<RectMask2D>(),Is.Not.Null);
            var strip=(RectTransform)shield.transform;
            var interior=new Vector3[4];var shieldCorners=new Vector3[4];
            mask.FillArea.GetWorldCorners(interior);strip.GetWorldCorners(shieldCorners);
            Assert.That(shieldCorners[0].x,Is.EqualTo(interior[0].x).Within(.001));
            Assert.That(shieldCorners[2].x,Is.EqualTo(interior[2].x).Within(.001));
            Assert.That(shieldCorners[2].y,Is.EqualTo(interior[2].y).Within(.001));
            Assert.That(strip.rect.height,Is.LessThanOrEqualTo(mask.FillArea.rect.height));
            Assert.That(mask.Border.transform.GetSiblingIndex(),Is.GreaterThan(mask.FillArea.GetSiblingIndex()));
        }
        [Test] public void InteractiveSlidersKeepTheirAuthoredHandleAndFrameVisibility()
        {
            var root=new GameObject("Interactive layout",typeof(RectTransform));objects.Add(root);
            var slider=Bar(root.transform,0,0,18,10);var mask=slider.GetComponent<UIBarFillMask>();
            var handle=new GameObject("Handle",typeof(RectTransform));handle.transform.SetParent(slider.transform,false);
            slider.handleRect=(RectTransform)handle.transform;slider.interactable=true;mask.Border.enabled=false;
            mask.Apply();Assert.That(mask.Border.enabled,Is.False);Assert.That(slider.value,Is.EqualTo(10));
            Assert.That(slider.handleRect,Is.SameAs(handle.transform));
        }
        [Test] public void ResizingRecomputesClippingAndDoesNotChangeTheValue()
        {
            var root=new GameObject("Resize",typeof(RectTransform));objects.Add(root);var slider=Bar(root.transform,0,0,18,10);
            var mask=slider.GetComponent<UIBarFillMask>();mask.Apply();
            ((RectTransform)slider.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,6);mask.Apply();
            Assert.That(mask.FillArea.rect.height,Is.GreaterThanOrEqualTo(1.99f));Assert.That(slider.value,Is.EqualTo(10));
            var before=EditorJsonUtility.ToJson(mask.FillArea);mask.Apply();Assert.That(EditorJsonUtility.ToJson(mask.FillArea),Is.EqualTo(before));
        }
        [Test] public void RenderedFillNeverLeaksOutsideItsMask(){Capture("after.png",true);Capture("detail.png",true,true);}
        void Capture(string name,bool verify,bool detail=false)
        {
            Directory.CreateDirectory(Output);
            var target=new RenderTexture(detail?1200:960,detail?240:430,24);objects.Add(target);target.Create();
            var cameraObject=new GameObject("Bar render camera",typeof(Camera));objects.Add(cameraObject);var camera=cameraObject.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.115f,.11f,.13f);camera.cullingMask=1<<30;
            camera.orthographic=true;camera.orthographicSize=target.height*.5f;camera.transform.position=new Vector3(0,0,-100);camera.targetTexture=target;
            var root=new GameObject("Actual vitals bar gallery",typeof(RectTransform),typeof(Canvas));objects.Add(root);root.layer=30;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            var masks=new List<RectTransform>();
            var heights=new[]{6,12,18};
            for(var row=0;row<(detail?1:3);row++)for(var col=0;col<(detail?1:3);col++)
            {
                var slider=Bar(root.transform,detail?40:24+col*310,detail?140:70+row*110,detail?12:heights[row],detail?10:col==0?0:col==1?10:50);
                if(detail)slider.transform.localScale=Vector3.one*4;
                foreach(var transform in slider.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=30;
                var presentation=slider.GetComponent<UIBarFillMask>();presentation?.Apply();
                if(verify){Assert.That(presentation,Is.Not.Null);masks.Add(presentation.FillArea);}
            }
            Canvas.ForceUpdateCanvases();foreach(var presentation in root.GetComponentsInChildren<UIBarFillMask>())presentation.Apply();
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var read=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);objects.Add(read);read.ReadPixels(new Rect(0,0,target.width,target.height),0,0);read.Apply();RenderTexture.active=old;
            File.WriteAllBytes(Path.Combine(Output,name),read.EncodeToPNG());
            if(verify)
            {
                var regions=new List<Rect>();
                foreach(var mask in masks){var corners=new Vector3[4];mask.GetWorldCorners(corners);var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);regions.Add(Rect.MinMaxRect(min.x-.51f,min.y-.51f,max.x+.51f,max.y+.51f));}
                var red=0;var pixels=read.GetPixels32();
                for(var y=0;y<read.height;y++)for(var x=0;x<read.width;x++)
                {var color=pixels[y*read.width+x];if(color.r<200||color.g>25||color.b>25)continue;red++;Assert.That(regions.Any(region=>region.Contains(new Vector2(x+.5f,y+.5f))),Is.True,$"Fill leaked at {x},{y}");}
                Assert.That(red,Is.GreaterThan(100));
            }
            camera.targetTexture=null;target.Release();
        }
    }
}
