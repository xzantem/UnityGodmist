using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    /// <summary>Renders the live scene and authored UI without opening or focusing an Editor window.</summary>
    internal static class BackgroundGameCapture
    {
        private sealed class CanvasState
        {
            public Canvas Canvas; public Camera Camera; public RenderMode Mode; public float Plane, Scale;
            public CanvasScaler Scaler; public bool ScalerEnabled;
        }
        public static IEnumerator Capture(string path,bool preserveWindowSize=false)
        {
            var camera=Camera.main;
            Assert.That(camera,Is.Not.Null,"The production scene must provide its actual camera.");
            const int width=1920,height=1080;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);
            var oldTarget=camera.targetTexture;var oldMask=camera.cullingMask;var oldRect=camera.rect;
            var active=RenderTexture.active;
            var overlayCameraObject=new GameObject("Offscreen overlay camera",typeof(Camera));
            var overlayCamera=overlayCameraObject.GetComponent<Camera>();overlayCamera.enabled=false;
            overlayCamera.CopyFrom(camera);overlayCamera.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);overlayCamera.clearFlags=CameraClearFlags.Depth;
            var layers=new Dictionary<GameObject,int>();
            var used=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(node=>node.gameObject.layer).ToHashSet();
            var captureLayer=Enumerable.Range(8,24).Reverse().First(index=>!used.Contains(index));
            overlayCamera.cullingMask=1<<captureLayer;
            overlayCamera.targetTexture=target;overlayCamera.rect=new Rect(0,0,1,1);overlayCamera.aspect=(float)width/height;
            var states=new List<CanvasState>();
            var windows=Object.FindObjectsByType<UIWorkspaceWindow>(FindObjectsInactive.Exclude).ToDictionary(w=>w,w=>w.RectTransform.sizeDelta);
            var scrolls=Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Exclude).Where(s=>s.content!=null).ToDictionary(s=>s,s=>s.content.anchoredPosition);
            try
            {
                target.Create();camera.targetTexture=target;camera.cullingMask=oldMask&~(1<<captureLayer);camera.rect=new Rect(0,0,1,1);camera.aspect=(float)width/height;
                foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay))
                {
                    var scaler=canvas.GetComponent<CanvasScaler>();
                    states.Add(new CanvasState{Canvas=canvas,Camera=canvas.worldCamera,Mode=canvas.renderMode,Plane=canvas.planeDistance,Scale=canvas.scaleFactor,Scaler=scaler,ScalerEnabled=scaler!=null&&scaler.enabled});
                    if(scaler!=null)scaler.enabled=false;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=overlayCamera;canvas.planeDistance=overlayCamera.nearClipPlane+1;canvas.scaleFactor=1;
                    foreach(var node in canvas.GetComponentsInChildren<Transform>(true))
                    {if(!layers.ContainsKey(node.gameObject))layers.Add(node.gameObject,node.gameObject.layer);node.gameObject.layer=captureLayer;}
                }
                Canvas.ForceUpdateCanvases();
                yield return null;yield return null;
                if(preserveWindowSize)foreach(var pair in windows)if(pair.Key!=null)pair.Key.RectTransform.sizeDelta=pair.Value;
                overlayCamera.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
                overlayCamera.orthographicSize=camera.orthographicSize;
                // Camera pose changes the screen-space canvas transform. Settle that
                // before rebuilding TMP meshes, especially after a mode switch.
                Canvas.ForceUpdateCanvases();
                yield return null;yield return null;
                foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))text.ForceMeshUpdate(false,true);
                Canvas.ForceUpdateCanvases();
                foreach(var mask in Object.FindObjectsByType<RectMask2D>(FindObjectsInactive.Exclude))mask.PerformClipping();
                Canvas.ForceUpdateCanvases();camera.Render();
                // Overlay UI is a separate final pass, exactly as in the live viewport.
                // World sorting layers/nameplates must never draw over it.
                overlayCamera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                Assert.That(pixels.GetPixels32().Where((_,i)=>i%127==0).Select(c=>(c.r,c.g,c.b)).Distinct().Count(),Is.GreaterThan(32),"A native game capture must contain scene/UI detail, not an empty framebuffer.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget;camera.cullingMask=oldMask;camera.rect=oldRect;camera.ResetAspect();
                foreach(var pair in layers)if(pair.Key!=null)pair.Key.layer=pair.Value;
                foreach(var state in states)
                {
                    if(state.Canvas==null)continue;
                    state.Canvas.renderMode=state.Mode;state.Canvas.worldCamera=state.Camera;state.Canvas.planeDistance=state.Plane;
                    if(state.Scaler!=null)state.Scaler.enabled=state.ScalerEnabled;
                    state.Canvas.scaleFactor=state.Scale;
                }
                Canvas.ForceUpdateCanvases();
                foreach(var pair in windows)if(pair.Key!=null)pair.Key.RectTransform.sizeDelta=pair.Value;
                Canvas.ForceUpdateCanvases();
                foreach(var pair in scrolls)if(pair.Key!=null&&pair.Key.content!=null)pair.Key.content.anchoredPosition=pair.Value;
                RenderTexture.active=active;overlayCamera.targetTexture=null;Object.DestroyImmediate(overlayCameraObject);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
            // Let the normal player loop settle restored viewport/layout state before the next input.
            yield return null;yield return null;
        }
    }
}
