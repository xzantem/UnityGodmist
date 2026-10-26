using System;
using System.Collections;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatEconomyPolishTests
    {
        static T Read<T>(object o,string field)=>(T)o.GetType().GetField(field,Private|System.Reflection.BindingFlags.Public).GetValue(o);
        static double Capture(Camera camera,string file,int width=640,int height=480)
        {
            var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            var old=camera.targetTexture;var active=RenderTexture.active;
            try {
                target.Create();camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,file+".png"),image.EncodeToPNG());
                return image.GetPixels().Sum(c=>(double)c.r+c.g+c.b);
            } finally {camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);Object.DestroyImmediate(target);}
        }
        [TestCase("dungeon.forest.expedition")][TestCase("dungeon.catacombs.expedition")]
        public void TorchSpriteDimsInRememberedFogButLightSourceRemains(string id)
        {
            var fixture=new DungeonEntryPerformanceTests{OutputOverride=Path.GetFullPath(Output),StageOverride="torch-fog",SeedOverride=261026};
            fixture.FrameProbe=(bootstrap,game)=>{
                var rig=Read<DungeonSilhouetteLighting>(bootstrap,"silhouetteLighting");
                var map=Read<DungeonFloorMapView>(bootstrap,"floorMapView");var camera=Read<Camera>(rig,"worldCamera");
                map.ApplyFog(game.ActiveDungeon.Floors[0]);
                typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting",Private).Invoke(rig,new object[]{Time.time+2});
                var torches=Read<IList>(rig,"torches");var torch=torches.Cast<object>().First(t=>!Read<bool>(t,"carried"));
                var cell=Read<DungeonSurfacePosition>(torch,"cell");var view=Read<SpriteRenderer>(torch,"view");
                var floor=game.ActiveDungeon.Floors[0];var position=new DungeonTraversalPosition(cell);
                var refresh=typeof(DungeonSilhouetteLighting).GetMethod("RefreshTorchPresentation",Private);
                var oldPosition=camera.transform.position;var oldSize=camera.orthographicSize;var oldMask=camera.cullingMask;
                var oldLayer=view.gameObject.layer;var oldClear=camera.clearFlags;var oldColor=camera.backgroundColor;
                try {
                    camera.transform.position=new Vector3(view.bounds.center.x,view.bounds.center.y,-10);camera.orthographicSize=1;
                    camera.cullingMask=1<<31;view.gameObject.layer=31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    var sums=new double[2];var sourceCount=rig.TorchCount;
                    for(var state=0;state<2;state++) {
                        floor.Fog.SetVisible(state==0?new[]{position}:Array.Empty<DungeonTraversalPosition>());map.ApplyFog(floor);
                        typeof(DungeonSilhouetteLighting).GetMethod("RefreshLighting",Private).Invoke(rig,new object[]{Time.time+2});
                        refresh.Invoke(rig,new object[]{torch,view.sortingLayerID});
                        Assert.That(view.enabled,Is.True);
                        var block=new MaterialPropertyBlock();view.GetPropertyBlock(block);
                        Assert.That(block.GetFloat("_FogBrightness"),Is.EqualTo(state==0?1:DungeonFogBlend.Brightness(DungeonKnowledge.Remembered)).Within(.001));
                        sums[state]=Capture(camera,id.Contains("forest")?"forest-torch-"+state:"catacombs-torch-"+state);
                    }
                    Assert.That(sums[0],Is.GreaterThan(1));Assert.That(sums[1]/sums[0],Is.InRange(.35,.85));
                    Assert.That(rig.TorchCount,Is.EqualTo(sourceCount));
                    floor.Fog.SetVisible(new[]{position});map.ApplyFog(floor);refresh.Invoke(rig,new object[]{torch,view.sortingLayerID});
                    var restored=new MaterialPropertyBlock();view.GetPropertyBlock(restored);Assert.That(restored.GetFloat("_FogBrightness"),Is.EqualTo(1));
                } finally {camera.transform.position=oldPosition;camera.orthographicSize=oldSize;camera.cullingMask=oldMask;
                    camera.clearFlags=oldClear;camera.backgroundColor=oldColor;view.gameObject.layer=oldLayer;}
            };
            fixture.MeasureProductionFloorEntry(id,DungeonLength.Short);
        }
        [Test]
        public void RenderTrackedQuestWithProductionFontAndPrefab()
        {
            Directory.CreateDirectory(Output);
            var root=new GameObject("Tracker render",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var cameraObject=new GameObject("Tracker camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.025f,.035f,.025f);camera.transform.position=new Vector3(0,0,-10);
            try {
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,900);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TrackedQuestHudView>(CombatEconomyPolishSetup.TrackerPath),root.transform);
                var game=new GameSession(GameModeState.Exploration);
                var quest=new QuestInstance(AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root+"Quests/QD_InvestigateCatacombs.asset"),QuestStatus.Active);
                game.TryRegisterQuest(quest);game.TrySetTrackedQuest(quest.QuestId);view.Bind(game,new QuestService(game));
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                Assert.That(Capture(camera,"tracked-quest-hud",1280,900),Is.GreaterThan(100));
                Assert.That(view.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),Is.True);
            } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);}
        }
    }
}
