using System;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class GlossaryMechanicsTests
    {
        private const BindingFlags LivePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T LiveGet<T>(object target,string field) => (T)target.GetType().GetField(field,LivePrivate).GetValue(target);
        private static void LiveSet(object target,string field,object value) => target.GetType().GetField(field,LivePrivate).SetValue(target,value);
        private static void LiveInvoke(object target,string method) => target.GetType().GetMethod(method,LivePrivate).Invoke(target,null);

        private static UIWorkspaceTooltipView LiveTooltip(GameObject canvas)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
            var view=Object.Instantiate(prefab,canvas.transform).GetComponent<UIWorkspaceTooltipView>();
            LiveInvoke(view,"Awake"); LiveInvoke(view,"OnEnable");
            return view;
        }

        private static void DisplayPendingStatus(UIWorkspaceTooltipView view,CombatStatusPresentation presentation,object source)
        {
            view.ShowStatus(presentation,source);
            var show=LiveGet<Action>(view,"pendingShow");
            Assert.That(show,Is.Not.Null);
            LiveSet(view,"pendingShow",null); show();
        }

        [TestCase(false)] [TestCase(true)]
        public void OpenExplorationStatusCountsDownWithoutReenteringAndPreservesPin(bool pinned)
        {
            var canvas=new GameObject("Live status canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var view=LiveTooltip(canvas);
                var character=new CharacterInstance { LifeState=CharacterLifeState.Alive, ExplorationStatusElapsedSeconds=.2f };
                var effect=new PassiveEffectRuntime { EffectId="slow", DisplayName="Spowolnienie", Stacks=1,
                    Lifetime=PassiveEffectLifetime.Timed, RemainingDuration=3 };
                var presentation=new CombatStatusPresentation(effect,CombatStatusKind.Status,0,explorationCharacter:character);
                var source=new object(); DisplayPendingStatus(view,presentation,source);
                if(pinned) view.PinAtCurrentPosition();
                var label=LiveGet<TextMeshProUGUI>(view,"metaText");
                var shownAt=LiveGet<float>(view,"shownAt");
                Assert.That(label.text,Does.Contain("18 s"));
                character.ExplorationStatusElapsedSeconds=1.2f;
                LiveInvoke(view,"Update");
                Assert.That(label.text,Is.EqualTo("Pozostało: 17 s"));
                Assert.That(LiveGet<object>(view,"sourceIdentity"),Is.SameAs(source));
                Assert.That(LiveGet<float>(view,"shownAt"),Is.EqualTo(shownAt));
                Assert.That(view.IsPinned,Is.EqualTo(pinned));
                effect.RemainingDuration=2; character.ExplorationStatusElapsedSeconds=.2f;
                LiveInvoke(view,"Update");
                Assert.That(label.text,Is.EqualTo("Pozostało: 12 s"));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test] public void ExpiredExplorationStatusDoesNotKeepAPinnedStaleTimer()
        {
            var canvas=new GameObject("Expired status canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var view=LiveTooltip(canvas);
                var effect=new PassiveEffectRuntime { EffectId="slow",Stacks=1,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=1 };
                DisplayPendingStatus(view,new CombatStatusPresentation(effect,CombatStatusKind.Status,0,
                    explorationCharacter:new CharacterInstance()),new object());
                view.PinAtCurrentPosition(); effect.RemainingDuration=0;
                LiveInvoke(view,"Update");
                Assert.That(LiveGet<bool>(view,"visible"),Is.False);
                Assert.That(LiveGet<CombatStatusPresentation?>(view,"liveExplorationStatus"),Is.Null);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test] public void ReplacingStatusWithOtherContentStopsTimerUpdates()
        {
            var canvas=new GameObject("Replaced status canvas",typeof(RectTransform),typeof(Canvas));
            try
            {
                var view=LiveTooltip(canvas);
                var effect=new PassiveEffectRuntime { EffectId="slow",Stacks=1,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=3 };
                DisplayPendingStatus(view,new CombatStatusPresentation(effect,CombatStatusKind.Status,0,
                    explorationCharacter:new CharacterInstance()),new object());
                view.Hide(); view.ShowText("Inny opis","Treść",new object(),"Stałe dane");
                var show=LiveGet<Action>(view,"pendingShow"); LiveSet(view,"pendingShow",null); show();
                effect.RemainingDuration=0;
                LiveInvoke(view,"Update");
                Assert.That(LiveGet<bool>(view,"visible"),Is.True);
                Assert.That(LiveGet<TextMeshProUGUI>(view,"metaText").text,Is.EqualTo("Stałe dane"));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test] public void SuppressingResourceHoverCancelsAnAlreadyQueuedCardButKeepsGenericHovers()
        {
            var node=new GameObject("Resource hover lifecycle");
            try
            {
                var view=node.AddComponent<UIWorkspaceTooltipView>();
                var trigger=node.AddComponent<CombatSimpleTooltipTrigger>();
                trigger.Initialize(view,"Mana","12/100"); trigger.OnPointerEnter(null);
                Assert.That(LiveGet<Action>(view,"pendingShow"),Is.Not.Null);
                trigger.InitializeResource(view,new ResourcePool(ResourceType.Mana,12,100,15));
                Assert.That(LiveGet<Action>(view,"pendingShow"),Is.Null);
                trigger.OnPointerEnter(null);
                Assert.That(LiveGet<Action>(view,"pendingShow"),Is.Null);
                trigger.Initialize(view,"Inny tooltip","Treść"); trigger.OnPointerEnter(null);
                Assert.That(LiveGet<Action>(view,"pendingShow"),Is.Not.Null);
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test] public void StatusShadeFollowsSharedSixSecondTickAndIsAbsentInCombatOrOnPermanentEffects()
        {
            var node=new GameObject("Status tick icon",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            node.layer=5;
            var texture=new Texture2D(2,2); var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
            try
            {
                var icon=node.AddComponent<CombatStatusIconView>();
                LiveSet(icon,"icon",node.GetComponent<UnityEngine.UI.Image>());
                var character=new CharacterInstance();
                var effect=new PassiveEffectRuntime { EffectId="slow",Stacks=1,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=3 };
                var status=new CombatStatusPresentation(effect,CombatStatusKind.Status,0,explorationCharacter:character);
                icon.Bind(status,null,sprite);
                var shade=LiveGet<UnityEngine.UI.Image>(icon,"tickShade");
                Assert.That(shade.fillAmount,Is.EqualTo(1));
                Assert.That(shade.color.a,Is.InRange(.3f,.7f));
                Assert.That(shade.raycastTarget,Is.False);
                Assert.That(shade.gameObject.layer,Is.EqualTo(node.layer));
                character.ExplorationStatusElapsedSeconds=3; LiveInvoke(icon,"Update");
                Assert.That(shade.fillAmount,Is.EqualTo(.5f));
                effect.RemainingDuration=2; character.ExplorationStatusElapsedSeconds=0; LiveInvoke(icon,"Update");
                Assert.That(shade.fillAmount,Is.EqualTo(1));
                Assert.That(node.GetComponentsInChildren<UnityEngine.UI.Image>().Length,Is.EqualTo(2),"Reuse one overlay across all ticks.");
                icon.Bind(new CombatStatusPresentation(effect,CombatStatusKind.Status,0),null,sprite);
                Assert.That(shade.gameObject.activeSelf,Is.False);
                effect.Lifetime=PassiveEffectLifetime.Persistent;
                icon.Bind(status,null,sprite);
                Assert.That(shade.gameObject.activeSelf,Is.False);
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test] public void RenderStatusShadeRevealsClockwiseAndKeepsIconVisible()
        {
            const int size=128;
            var canvasNode=new GameObject("Status radial render",typeof(RectTransform),typeof(Canvas));
            var cameraNode=new GameObject("Status radial camera",typeof(Camera));
            var texture=new Texture2D(2,2); texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white}); texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
            var pixels=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var strip=new Texture2D(size*5,size,TextureFormat.RGBA32,false);
            var target=new RenderTexture(size,size,24); var previous=RenderTexture.active;
            try
            {
                var camera=cameraNode.GetComponent<Camera>(); camera.enabled=false; camera.orthographic=true;
                camera.transform.position=new Vector3(10000,10000,-10); camera.cullingMask=1<<31;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.gray;
                var canvas=canvasNode.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=1;
                var node=new GameObject("Icon",typeof(RectTransform),typeof(UnityEngine.UI.Image)); node.transform.SetParent(canvasNode.transform,false);
                ((RectTransform)node.transform).sizeDelta=new Vector2(96,96);
                var view=node.AddComponent<CombatStatusIconView>(); LiveSet(view,"icon",node.GetComponent<UnityEngine.UI.Image>());
                var character=new CharacterInstance();
                var effect=new PassiveEffectRuntime { EffectId="slow",Stacks=1,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=3 };
                view.Bind(new CombatStatusPresentation(effect,CombatStatusKind.Status,0,explorationCharacter:character),null,sprite);
                foreach(var child in canvasNode.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31;
                target.Create(); camera.targetTexture=target;
                for(int step=0;step<5;step++)
                {
                    character.ExplorationStatusElapsedSeconds=step*1.5f; LiveInvoke(view,"Update");
                    Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,size,size),0,0); pixels.Apply();
                    strip.SetPixels(step*size,0,size,size,pixels.GetPixels());
                    var left=pixels.GetPixel(40,64).grayscale; var right=pixels.GetPixel(88,64).grayscale;
                    if(step==0) Assert.That(left,Is.InRange(.15f,.85f),"The initial shade must not hide the icon completely.");
                    if(step==2) Assert.That(right,Is.GreaterThan(left+.15f),"Clockwise reveal must expose the right half before the left.");
                    if(step==4) Assert.That(left,Is.GreaterThan(.9f));
                }
                strip.Apply();
                var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../LocalHandoffs/resource-status-tooltip-20261021/radial-tick.png"));
                System.IO.File.WriteAllBytes(output,strip.EncodeToPNG());
            }
            finally
            {
                cameraNode.GetComponent<Camera>().targetTexture=null; RenderTexture.active=previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(strip);
                Object.DestroyImmediate(canvasNode); Object.DestroyImmediate(cameraNode); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }
    }
}
