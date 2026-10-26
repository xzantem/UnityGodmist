using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Combat;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;
namespace Godmist.Tests.EditMode
{
    public sealed class ForestPlaytestPresentationTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<Object> owned=new();
        T Keep<T>(T value) where T:Object {owned.Add(value);return value;}
        [TearDown]public void Cleanup(){foreach(var root in owned.OfType<GameObject>())if(root!=null)foreach(var camera in root.GetComponentsInChildren<Camera>(true))camera.targetTexture=null;foreach(var item in owned.AsEnumerable().Reverse())if(item!=null)Object.DestroyImmediate(item);owned.Clear();}
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        DungeonFloorMapView Terrain(bool rebuild=false)
        {
            var root=Keep(new GameObject("forest test map",typeof(Grid)));
            var floor=new GameObject("floor",typeof(Tilemap),typeof(TilemapRenderer));floor.transform.SetParent(root.transform,false);
            var view=root.AddComponent<DungeonFloorMapView>();Set(view,"floorTilemap",floor.GetComponent<Tilemap>());
            var map=new DungeonFloorMap();for(var y=0;y<4;y++)for(var x=0;x<4;x++)map.TryAddFloorCell(new Vector2Int(x,y),"glade");
            foreach(var cell in map.FloorCells.ToArray())foreach(var direction in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
                if(!map.IsWalkable(cell+direction))map.TryAddWall(cell,cell+direction);
            var data=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            view.ConfigureVegetationPresentation(data.VegetationPresentation);Set(view,"floorTile",data.VegetationPresentation.GroundTile);
            Set(view,"wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
            if(rebuild)view.Rebuild(map);else Set(view,"boundMap",map);
            return view;
        }
        Sprite Solid(int width,int height,Color color)
        {
            var texture=Keep(new Texture2D(width,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point});
            texture.SetPixels(Enumerable.Repeat(color,width*height).ToArray());texture.Apply();
            return Keep(Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,32));
        }
        SpriteRenderer Rendered(Transform parent,string name,Sprite sprite,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.layer=30;go.transform.SetParent(parent,false);go.transform.position=new Vector3(1.5f,1.5f,0);
            var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=order;return r;
        }

        [TestCase("party")] [TestCase("enemy")] [TestCase("summon")] [TestCase("corpse")]
        public void RealSpriteKeepsGroundedDepthWithoutCopyAndStillHidesWhenInvisible(string role)
        {
            var terrain=Terrain();var tree=Rendered(terrain.transform,"tree",Solid(32,64,Color.green),20);terrain.RegisterForestCanopy(tree);
            var actorRoot=Keep(new GameObject(role));actorRoot.transform.position=new Vector3(1.5f,1.5f,0);
            var body=Rendered(actorRoot.transform,"body",Solid(16,32,Color.red),0);
            var cameraRoot=Keep(new GameObject("test camera",typeof(Camera)));cameraRoot.transform.position=new Vector3(1.5f,1.5f,-10);
            var camera=cameraRoot.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.25f;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var rt=Keep(new RenderTexture(96,96,24));var pixels=Keep(new Texture2D(96,96,TextureFormat.RGBA32,false));camera.targetTexture=rt;
            var previous=RenderTexture.active;
            Color Capture(string suffix)
            {
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,96,96),0,0);pixels.Apply();
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-playtest-pass-20261015"));Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output,role+"-"+suffix+".png"),pixels.EncodeToPNG());return pixels.GetPixel(48,48);
            }
            try
            {
                Assert.That(Capture("before").g,Is.GreaterThan(.9f));
                if(role=="party"||role=="enemy")
                {
                    var view=actorRoot.AddComponent<ExplorationCharacterView>();Set(view,"bodyRenderer",body);Set(view,"terrainView",terrain);
                    if(role=="enemy")view.SetEnemyInstanceId("known.enemy");
                    Call(view,"RefreshWorldDepthSorting");
                }
                else if(role=="summon")
                {
                    var view=actorRoot.AddComponent<CharacterView>();Set(view,"bodyRenderer",body);view.BindTerrainView(terrain);Call(view,"LateUpdate");
                }
                else
                {
                    var view=actorRoot.AddComponent<DungeonEnemyBodyView>();Set(view,"bodyRenderer",body);Set(view,"elevationView",terrain);Call(view,"RefreshGroundSorting");
                }
                var shown=Capture("after");Assert.That(shown.g,Is.GreaterThan(.9f));Assert.That(shown.r,Is.LessThan(.05f),"a tree at equal feet remains in front; the canopy shader owns local reveal");
                Assert.That(body.color.a,Is.EqualTo(1));Assert.That(actorRoot.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(1),"no translucent extra pass");
                body.forceRenderingOff=true;Assert.That(Capture("hidden").g,Is.GreaterThan(.9f),"visibility gate cannot leak a raised actor");
                Assert.That(terrain.GetForestReadableSortingOrder(new Vector2Int(1,1),body.bounds,0,1),Is.Zero);
                Assert.That(terrain.GetForestReadableSortingOrder(new Vector2Int(1,1),body.bounds,0,2),Is.Zero);
                tree.forceRenderingOff=true;Assert.That(terrain.GetForestReadableSortingOrder(new Vector2Int(1,1),body.bounds,0),Is.Zero);
            }
            finally{RenderTexture.active=previous;}
        }

        [Test]public void NearestTreesStayOnBlockedEdgesAndLowScrubIsNotTheBoundary()
        {
            var view=Terrain(true);var renderers=view.LightingWallRenderers.Where(r=>r!=null).ToArray();
            Assert.That(renderers.Any(r=>r.name=="Forest boundary scrub"),Is.False);
            Assert.That(renderers.Length,Is.GreaterThan(20));
            var near=renderers.Where(r=>r.name=="Boundary vegetation"&&r.transform.position.x>-.4f&&r.transform.position.x<4.4f&&r.transform.position.y>-.4f&&r.transform.position.y<4.4f).ToArray();
            Assert.That(near,Is.Not.Empty);Assert.That(near.All(r=>r.sprite.bounds.size.y>1.5f),Is.True);
            foreach(var tree in near)
            {
                var p=tree.transform.position;
                Assert.That(Mathf.Min(Mathf.Min(Mathf.Abs(p.x),Mathf.Abs(p.x-4)),Mathf.Min(Mathf.Abs(p.y),Mathf.Abs(p.y-4))),Is.LessThan(.3f),"nearest trunk must remain on collision line");
            }
        }

        [TestCase("soothing-growth-world-64-v1")] [TestCase("poisonous-growth-world-64-v1")]
        public void SmallGrowthKeepsNativePixelsAndGroundedBase(string name)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Godmist/ForestArt/Summons/"+name+".png");
            Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(64,64)));
            var bounds=VisibleBounds(sprite);
            Assert.That(bounds.size.x,Is.LessThanOrEqualTo(20f/32));Assert.That(bounds.size.y,Is.LessThanOrEqualTo(22f/32));
            Assert.That(Mathf.Abs(bounds.min.y),Is.LessThanOrEqualTo(1f/32));
        }

        [Test]public void WolfCorpseLengthMatchesLiveBodyAtSamePixelScale()
        {
            Assert.That(CharacterAnimationResolver.TryResolveCorpse(ForestEnemiesSetup.WolfId,out var corpse),Is.True);
            Assert.That(CharacterAnimationResolver.TryResolveCharacter(ForestEnemiesSetup.WolfId,Vector2.right,CharacterAnimationState.Idle,out var live),Is.True);
            var deadBounds=VisibleBounds(corpse);var liveBounds=VisibleBounds(live.Frames[0]);
            Assert.That(corpse.pixelsPerUnit,Is.EqualTo(live.Frames[0].pixelsPerUnit));
            Assert.That(deadBounds.size.x,Is.LessThanOrEqualTo(liveBounds.size.x*1.05f));
        }

        [TestCase(ForestEnemiesSetup.WolfPath)]
        [TestCase("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset")]
        [TestCase("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset")]
        [TestCase("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset")]
        [TestCase("Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset")]
        [TestCase("Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_PoisonGrowth.asset")]
        [TestCase("Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_HealingGrowth.asset")]
        public void ExplorationAndCombatRetainAuthoredWorldScale(string path)
        {
            var root=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab")));
            var view=root.GetComponent<ExplorationCharacterView>();
            var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            Assert.That(definition,Is.Not.Null,path);
            var childScale=view.BodyRenderer.transform.localScale;
            var rootScale=root.transform.localScale;
            var collider=root.GetComponentInChildren<Collider2D>();var colliderSize=collider.bounds.size;
            Call(view,"ConfigureVisual",definition);
            Assert.That(view.BodyRenderer.transform.lossyScale.x,Is.EqualTo(.7f).Within(.001));
            Assert.That(view.BodyRenderer.transform.localScale,Is.EqualTo(childScale));
            Assert.That(root.transform.localScale,Is.EqualTo(rootScale));Assert.That(collider.bounds.size,Is.EqualTo(colliderSize));
            var battle=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab")));
            var combat=battle.GetComponent<CharacterView>();
            combat.Bind(new Godmist.Runtime.Combat.BattleUnit {Character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition,definition.ClassData,1)});
            Assert.That(combat.BodyRenderer.transform.lossyScale.x,Is.EqualTo(view.BodyRenderer.transform.lossyScale.x).Within(.001));
            var scale=combat.BodyRenderer.transform.localScale;
            combat.Bind(combat.BoundUnit);Call(view,"ConfigureVisual",definition);
            Assert.That(combat.BodyRenderer.transform.localScale,Is.EqualTo(scale));
            Assert.That(view.BodyRenderer.transform.localScale,Is.EqualTo(childScale));
            Assert.That(view.BodyRenderer.sprite.pixelsPerUnit,Is.EqualTo(32));
        }

        [Test]public void CorpseGroundLightingRetainsReadableDepthAndCleansRegistration()
        {
            var view=Terrain();var root=Keep(new GameObject("corpse",typeof(SpriteRenderer)));var renderer=root.GetComponent<SpriteRenderer>();
            view.RegisterGroundLightingPresentation(renderer,true,preserveDepthSorting:true);
            Assert.That(view.IsGroundLightingPresentation(renderer),Is.True);Assert.That(view.PreservesGroundPresentationSorting(renderer),Is.True);
            view.UnregisterLightingPresentation(renderer);Assert.That(view.PreservesGroundPresentationSorting(renderer),Is.False);
        }

        [Test]public void MovementCostActuallyRendersAboveObstacleDespiteFloorParentGroup()
        {
            var root=Keep(new GameObject("floor tactical feedback"));var feedback=root.AddComponent<CombatGridFeedbackRenderer>();
            Set(feedback,"pathCostFont",TMPro.TMP_Settings.defaultFontAsset);feedback.SetFloorSorting(0,-100);
            feedback.ShowMovementPreview(new[]{new Godmist.Runtime.Combat.GridPosition(1,1)},new Godmist.Runtime.Combat.GridPosition(0,1),300,Color.red);
            var label=root.GetComponentInChildren<TMPro.TMP_Text>();Assert.That(label,Is.Not.Null);label.ForceMeshUpdate(true,true);
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
            var blocker=Rendered(root.transform,"foreground obstacle",Solid(64,64,Color.green),99);blocker.transform.SetParent(null);
            owned.Add(blocker.gameObject);blocker.transform.position=label.transform.position;
            var camRoot=Keep(new GameObject("AP render camera",typeof(Camera)));camRoot.transform.position=label.transform.position+Vector3.back*10;
            var camera=camRoot.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.65f;camera.cullingMask=1<<30;
            var rt=Keep(new RenderTexture(128,128,24));var image=Keep(new Texture2D(128,128,TextureFormat.RGBA32,false));camera.targetTexture=rt;
            var previous=RenderTexture.active;
            try
            {
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                Assert.That(image.GetPixels32().Count(p=>p.r>100&&p.g<60),Is.GreaterThan(10),"world cost text must escape the low parent sorting group");
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-playtest-pass-20261015"));
                File.WriteAllBytes(Path.Combine(output,"movement-ap-above-obstacle.png"),image.EncodeToPNG());
                feedback.IsPositionVisible=_=>false;feedback.RefreshVisibility();Assert.That(label.gameObject.activeSelf,Is.False);
            }
            finally{RenderTexture.active=previous;}
        }

        Bounds VisibleBounds(Sprite sprite)
        {
            // Imported props have no actor-catalog rect. Inspect source alpha on a transient readable texture.
            var copy=Keep(new Texture2D(2,2,TextureFormat.RGBA32,false));copy.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
            var pixels=copy.GetPixels32();var min=new Vector2Int(copy.width,copy.height);var max=Vector2Int.zero;
            for(var y=0;y<copy.height;y++)for(var x=0;x<copy.width;x++)if(pixels[y*copy.width+x].a>=128)
            {min=Vector2Int.Min(min,new Vector2Int(x,y));max=Vector2Int.Max(max,new Vector2Int(x+1,y+1));}
            return new Bounds(((Vector2)(min+max)*.5f-sprite.pivot)/sprite.pixelsPerUnit,(Vector2)(max-min)/sprite.pixelsPerUnit);
        }
    }
}
