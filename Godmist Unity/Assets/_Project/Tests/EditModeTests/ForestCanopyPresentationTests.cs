using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using Godmist.Data.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;
namespace Godmist.Tests.EditMode
{
    public class ForestCanopyPresentationTests
    {
        [TestCase("Godmist/Dungeon/Silhouette Floor")]
        [TestCase("Godmist/Dungeon/Torch Body")]
        [TestCase("Godmist/Dungeon/Torch Wall")]
        public void PropSeparationDoesNotEmitLightInAnUnlitForest(string shaderName)
        {
            var root = new GameObject("Unlit forest prop");
            root.layer = 30;
            var camera = root.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = .6f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var values = new Color[1024];
            for (var y = 8; y < 24; y++) for (var x = 8; x < 24; x++) values[y * 32 + x] = new Color(.4f, .4f, .3f, 1);
            texture.SetPixels(values); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32, 0, SpriteMeshType.FullRect);
            var material = new Material(Shader.Find(shaderName));
            var target = new RenderTexture(120, 120, 24);
            var pixels = new Texture2D(120, 120, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                var prop = new GameObject("Prop", typeof(SpriteRenderer)); prop.layer = 30;
                prop.transform.SetParent(root.transform, false); prop.transform.position = Vector3.zero;
                var renderer = prop.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
                material.SetFloat("_Ambient", 0); material.SetInt("_TorchCount", 0);
                material.SetFloat("_ForestPropReadability", .55f);
                material.SetVector("_ForestPropUV", new Vector4(0, 0, 1, 1));
                material.SetVector("_ForestPropTexel", new Vector4(1f / 32, 1f / 32, 0, 0));
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 120, 120), 0, 0); pixels.Apply();
                var brightest = pixels.GetPixels32().Max(c => Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                Assert.That(brightest, Is.LessThanOrEqualTo(1), "A grounding rim must receive the prop's light, not glow independently of it.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root); Object.DestroyImmediate(texture); Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(material); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [TestCase("Godmist/Dungeon/Silhouette Floor")]
        [TestCase("Godmist/Dungeon/Torch Body")]
        [TestCase("Godmist/Dungeon/Torch Wall")]
        public void PropShaderAddsOnePixelRimOnlyWhenOptedIn(string shaderName)
        {
            var root=new GameObject("Native pixel prop rim");
            var camera=root.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.6f;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.4f,.4f,.3f);
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var values=new Color[1024];for(var y=8;y<24;y++)for(var x=8;x<24;x++)values[y*32+x]=new Color(.4f,.4f,.3f,1);
            texture.SetPixels(values);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*.5f,32,0,SpriteMeshType.FullRect);
            var material=new Material(Shader.Find(shaderName));var target=new RenderTexture(120,120,24);
            var pixels=new Texture2D(120,120,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
            try
            {
                Assert.That(ShaderUtil.ShaderHasError(material.shader),Is.False);
                var prop=new GameObject("Prop",typeof(SpriteRenderer));prop.transform.SetParent(root.transform,false);
                prop.transform.position=Vector3.zero;
                var sr=prop.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=material;
                material.SetFloat("_Ambient",1);material.SetFloat("_ForestPropReadability",0);
                material.SetVector("_ForestPropUV",new Vector4(0,0,1,1));material.SetVector("_ForestPropTexel",new Vector4(1f/32,1f/32,0,0));
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,120,120),0,0);pixels.Apply();var baseline=pixels.GetPixels();
                material.SetFloat("_ForestPropReadability",.55f);camera.Render();pixels.ReadPixels(new Rect(0,0,120,120),0,0);pixels.Apply();
                var improved=pixels.GetPixels();var darkened=0;
                for(var i=0;i<improved.Length;i++)if(improved[i].r<baseline[i].r-.04f)darkened++;
                Assert.That(darkened,Is.GreaterThan(50),"A restrained dark native-pixel rim must separate art from equal-colour terrain.");
                Assert.That(improved[60*120+60].r,Is.GreaterThanOrEqualTo(baseline[60*120+60].r),"Contact/rim must not darken the prop interior.");
                material.SetFloat("_ForestPropReadability",0);camera.Render();pixels.ReadPixels(new Rect(0,0,120,120),0,0);pixels.Apply();
                Assert.That(pixels.GetPixels32()[60*120+34],Is.EqualTo((Color32)baseline[60*120+34]),"Disabled path restores the original shader result.");
            }
            finally
            {
                RenderTexture.active=previous;Object.DestroyImmediate(root);Object.DestroyImmediate(texture);Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(material);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }

        [TestCase(.58f)]
        [TestCase(.12f)]
        public void NativeForestPropsReceiveContactAndSeparationWithoutExtraSpriteRenderers(float ambient)
        {
            var root=new GameObject("Forest props gallery",typeof(Grid));
            var floor=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));floor.transform.SetParent(root.transform,false);
            var cameraObject=new GameObject("Gallery camera",typeof(Camera));
            var material=new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var target=new RenderTexture(768,384,24);var pixels=new Texture2D(768,384,TextureFormat.RGBA32,false);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var previous=RenderTexture.active;object shadow=null;
            try
            {
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",flags).SetValue(view,floor.GetComponent<Tilemap>());
                var map=new DungeonFloorMap();for(var y=0;y<8;y++)for(var x=0;x<16;x++)map.TryAddFloorCell(new Vector2Int(x,y),"glade");
                var data=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Godmist.Editor.ForestPrototypeSetup.DefinitionPath);
                typeof(DungeonFloorMapView).GetField("floorTile",flags).SetValue(view,data.VegetationPresentation.GroundTile);
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab",flags).SetValue(view,
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                view.ConfigureVegetationPresentation(data.VegetationPresentation);view.Rebuild(map);view.ConfigureForestGroundMaterial(material);
                material.SetFloat("_Ambient",ambient);material.SetFloat("_ReceiveShadows",0);floor.GetComponent<TilemapRenderer>().sharedMaterial=material;
                var lighting=root.AddComponent<DungeonSilhouetteLighting>();
                foreach(var field in new[]{("terrain",(object)view),("floorMaterial",material),("floorReceiver",floor.GetComponent<TilemapRenderer>())})
                    typeof(DungeonSilhouetteLighting).GetField(field.Item1,flags).SetValue(lighting,field.Item2);
                var names=new[]{"shrub","forest-dry-shrub-v1","forest-deadwood-v1","forest-stump-v1","forest-boulder-v1","forest-small-stone"};
                var contacts=(Dictionary<SpriteRenderer,Vector2>)typeof(DungeonFloorMapView).GetField("forestPropContacts",flags).GetValue(view);
                var props=new List<SpriteRenderer>();
                for(var i=0;i<names.Length;i++)
                {
                    var sr=new GameObject(names[i],typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();sr.transform.SetParent(root.transform,false);
                    sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/"+names[i]+"-32ppu.png");
                    Assert.That(sr.sprite,Is.Not.Null);Assert.That(sr.sprite.pixelsPerUnit,Is.EqualTo(32));
                    var position=new Vector2(1.5f+i*2.5f,3.5f);sr.transform.position=position;
                    sr.transform.position+=(Vector3)(position-new Vector2(sr.bounds.center.x,sr.bounds.min.y));
                    sr.color=new Color(.68f,.70f,.60f,1);sr.sortingOrder=floor.GetComponent<TilemapRenderer>().sortingOrder+2;sr.sharedMaterial=material;
                    var block=new MaterialPropertyBlock();block.SetFloat("_ForestSurfaceEnabled",0);sr.SetPropertyBlock(block);props.Add(sr);
                }
                var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=4;
                camera.transform.position=new Vector3(8,4,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.targetTexture=target;
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,768,384),0,0);pixels.Apply();
                var before=pixels.GetPixels();var output=Environment.GetEnvironmentVariable("GODMIST_TEST_ARTIFACTS");
                var spriteCount=root.GetComponentsInChildren<SpriteRenderer>().Length;
                var suffix=ambient < .2f ? "-dim" : "";
                if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"forest-props-before"+suffix+".png"),pixels.EncodeToPNG());
                foreach(var sr in props)
                {
                    contacts[sr]=new Vector2(sr.bounds.center.x,sr.bounds.min.y);
                    typeof(DungeonSilhouetteLighting).GetMethod("ApplyGroundLighting",flags).Invoke(lighting,new object[]{sr,true,true});
                    var block=new MaterialPropertyBlock();sr.GetPropertyBlock(block);Assert.That(block.GetFloat("_ForestPropReadability"),Is.GreaterThan(0));
                }
                var type=typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows",true);
                shadow=Activator.CreateInstance(type,new object[]{root.transform,Shader.Find("Godmist/Dungeon/Contact Shadows")});
                var cadence=typeof(DungeonSilhouetteLighting).GetMethod("ShouldRefreshLighting",flags);
                var prepared=typeof(DungeonSilhouetteLighting).GetField("cameraGroundBounds",flags);
                cadence.Invoke(lighting,new object[]{0f,new Rect(0,0,16,8),view.PresentationRevision});
                void Refresh()=>type.GetMethod("Rebuild").Invoke(shadow,new object[]{view,floor.GetComponent<TilemapRenderer>(),(Rect)prepared.GetValue(lighting),Array.Empty<DungeonObjectView>(),Array.Empty<SpriteRenderer>(),null,null});
                Refresh();Refresh();Assert.That((int)type.GetProperty("RebuildCount").GetValue(shadow),Is.EqualTo(1));
                for(var tick=1;tick<=4;tick++)
                {
                    cadence.Invoke(lighting,new object[]{tick*.2f+.01f,new Rect(tick*.1f,0,16,8),view.PresentationRevision});
                    Refresh();
                }
                Assert.That((int)type.GetProperty("RebuildCount").GetValue(shadow),Is.EqualTo(1),"Small camera pans across lighting ticks must reuse the real contact mesh.");
                var contactRenderer=(MeshRenderer)type.GetField("renderer",flags).GetValue(shadow);
                foreach(var sr in props)Assert.That(sr.sortingOrder,Is.GreaterThan(contactRenderer.sortingOrder));
                camera.Render();pixels.ReadPixels(new Rect(0,0,768,384),0,0);pixels.Apply();var after=pixels.GetPixels();var changed=0;
                // Separation follows illumination: use the same relative
                // contrast threshold for bright and dim forest renders.
                for(var i=0;i<after.Length;i++)if(Mathf.Abs(after[i].r-before[i].r)>.02f*ambient/.58f)changed++;
                Assert.That(changed,Is.GreaterThan(100));Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(spriteCount));
                if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"forest-props-after"+suffix+".png"),pixels.EncodeToPNG());
                view.SetPresentationOccupancy(new[]{Vector2Int.one});
                Assert.That(view.ForestPropContacts.All(p=>p.Key!=null),Is.True,"Rebuilt decorations must not leave destroyed contact owners.");
            }
            finally
            {
                RenderTexture.active=previous;
                if(shadow!=null)foreach(var field in new[]{"root","mesh","material"})Object.DestroyImmediate((Object)shadow.GetType().GetField(field,flags).GetValue(shadow));
                Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);if(material!=null)Object.DestroyImmediate(material);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }
        [Test]
        public void OnlyCanopyAboveTheRootCellRevealsOpaqueActorWithoutExtraCopy()
        {
            var root=new GameObject("Actual canopy pixel test");var camera=root.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1.25f;camera.aspect=.5f;camera.transform.position=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
            var bodyTexture=new Texture2D(4,8,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var treeTexture=new Texture2D(4,8,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            for(var y=0;y<8;y++) for(var x=0;x<4;x++) {bodyTexture.SetPixel(x,y,Color.red);treeTexture.SetPixel(x,y,x<2?new Color(0,.3f,0,1):Color.clear);}
            bodyTexture.Apply();treeTexture.Apply();
            var body=Sprite.Create(bodyTexture,new Rect(0,0,4,8),Vector2.one*.5f,4);
            var tree=Sprite.Create(treeTexture,new Rect(0,0,4,8),Vector2.one*.5f,4);
            var material=new Material(Shader.Find("Godmist/Dungeon/Torch Body"));
            var target=new RenderTexture(64,128,24);var pixels=new Texture2D(64,128,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
            try
            {
                for(var i=0;i<2;i++)
                {
                    var go=new GameObject("Layer "+i);go.layer=30;go.transform.SetParent(root.transform,false);go.transform.position=Vector3.zero;
                    var sr=go.AddComponent<SpriteRenderer>();sr.sprite=i==1?tree:body;sr.sortingOrder=i;
                    if(i==1) sr.sharedMaterial=material;
                }
                material.SetFloat("_Ambient",1);material.SetFloat("_ForestCanopy",1);material.SetFloat("_ForestCanopyRootTopY",0);
                material.SetFloat("_ForestCanopyCellSize",1);
                material.SetFloat("_ForestCanopyFade",.8f);
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,64,128),0,0);pixels.Apply();
                var output=Environment.GetEnvironmentVariable("GODMIST_TEST_ARTIFACTS");
                if(!string.IsNullOrEmpty(output)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"forest-canopy-mask-test.png"),pixels.EncodeToPNG());
                // Readback is sRGB; compare opacity in the linear space used by blending.
                var exposed=pixels.GetPixel(48,64).linear;var covered=pixels.GetPixel(16,32).linear;
                Assert.That(exposed.r,Is.GreaterThan(.7f));Assert.That(exposed.g,Is.LessThan(.05f),"No gold wash on exposed actor pixels.");
                Assert.That(covered.r,Is.LessThan(.05f));Assert.That(covered.g,Is.GreaterThan(.05f),"First root cell stays opaque.");
                Assert.That(pixels.GetPixel(16,90).linear.r,Is.InRange(.75f,.85f),"The crown leaves the whole silhouette readable.");
                Assert.That(pixels.GetPixel(16,90).linear.g,Is.GreaterThan(.005f),"Leaves remain visible over the opaque actor.");
            }
            finally
            {
                RenderTexture.active=previous;Object.DestroyImmediate(root);Object.DestroyImmediate(material);Object.DestroyImmediate(body);Object.DestroyImmediate(tree);
                Object.DestroyImmediate(bodyTexture);Object.DestroyImmediate(treeTexture);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }
        [Test]
        public void ForestContactIsRoundedAboveLitterAndReusesUnchangedMesh()
        {
            var root=new GameObject("Forest contact test",typeof(Grid));
            var floor=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));floor.transform.SetParent(root.transform,false);
            var prop=new GameObject("Rock",typeof(SpriteRenderer));prop.transform.SetParent(root.transform,false);
            var texture=new Texture2D(32,32);var sprite=Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*.5f,32);
            var tile=ScriptableObject.CreateInstance<Tile>();
            var type=typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows",true);object shadow=null;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                var view=root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap",flags).SetValue(view,floor.GetComponent<Tilemap>());
                typeof(DungeonFloorMapView).GetField("floorTile",flags).SetValue(view,tile);
                var wall=new GameObject("Wall template",typeof(SpriteRenderer),typeof(BoxCollider2D));wall.transform.SetParent(root.transform,false);
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab",flags).SetValue(view,wall.AddComponent<DungeonWallSegmentView>());
                var map=new DungeonFloorMap();for(var y=0;y<3;y++)for(var x=0;x<3;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                var data=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Godmist.Editor.ForestPrototypeSetup.DefinitionPath);
                view.ConfigureVegetationPresentation(data.VegetationPresentation);view.Rebuild(map);
                prop.transform.position=view.CellToWorldCenter(Vector2Int.one);prop.GetComponent<SpriteRenderer>().sprite=sprite;
                shadow=Activator.CreateInstance(type,new object[]{root.transform,Shader.Find("Godmist/Dungeon/Contact Shadows")});
                void Refresh()=>type.GetMethod("Rebuild").Invoke(shadow,new object[]{view,floor.GetComponent<TilemapRenderer>(),new Rect(-5,-5,20,20),Array.Empty<DungeonObjectView>(),Array.Empty<SpriteRenderer>(),null,new[]{prop.GetComponent<SpriteRenderer>()}});
                Refresh();var renderer=(MeshRenderer)type.GetField("renderer",flags).GetValue(shadow);
                Assert.That(renderer.sortingOrder,Is.EqualTo(floor.GetComponent<TilemapRenderer>().sortingOrder+2),
                    "Forest contacts must sit above fog (floor+1) and below readable props (floor+3/4).");
                var mesh=(Mesh)type.GetField("mesh",flags).GetValue(shadow);
                Assert.That(mesh.vertexCount,Is.GreaterThan(16),"Rounded scanline bands replace the four rectangular pads.");
                Refresh();Assert.That((int)type.GetProperty("RebuildCount").GetValue(shadow),Is.EqualTo(1));
            }
            finally
            {
                if(shadow!=null) foreach(var name in new[]{"root","mesh","material"}) Object.DestroyImmediate((Object)type.GetField(name,flags).GetValue(shadow));
                Object.DestroyImmediate(root);Object.DestroyImmediate(tile);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
            }
        }
    }
}
