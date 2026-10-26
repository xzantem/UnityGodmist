using System.IO;
using Godmist.Data.Dungeons;
using Godmist.Views.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ForestScalePreview
    {
        // Unlit placement audit: actual map decoration builder and authored
        // anchors, without simulating gameplay fog, lights or their cost.
        [MenuItem("Godmist/Content/Pass 1b/Forest Composition Audit")]
        public static void RenderCompositionAudit()
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            Directory.CreateDirectory("out");
            foreach (var id in new[] { "roots", "hollow", "blind_glade", "overgrown_ruins" })
            {
                var room = System.Linq.Enumerable.Single(definition.RoomTemplateCatalog.Templates, t => t.Id == "room.forest." + id);
                var root = new GameObject("Forest composition audit", typeof(Grid));
                var materials = new System.Collections.Generic.List<Material>();
                RenderTexture target = null;
                Texture2D pixels = null;
                var previous = RenderTexture.active;
                try
                {
                    var floor = new GameObject("Audit floor", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
                    floor.transform.SetParent(root.transform, false);
                    var view = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                    var wiring = new SerializedObject(view);
                    wiring.FindProperty("floorTilemap").objectReferenceValue = floor.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                    wiring.FindProperty("floorTile").objectReferenceValue = definition.VegetationPresentation.GroundTile;
                    wiring.FindProperty("wallSegmentPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();
                    wiring.ApplyModifiedPropertiesWithoutUndo();
                    var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
                    foreach (var cell in room.Footprint.FloorCellOffsets) map.TryAddFloorCell(cell, room.Id);
                    var anchors = System.Linq.Enumerable.Where(room.SemanticAnchors, a => a.ObjectDefinition is ObstacleDungeonObjectDefinition);
                    var occupied = System.Linq.Enumerable.Select(anchors, a => a.LocalCell);
                    view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                    view.SetForestPathObstacles(occupied);
                    view.SetPresentationOccupancy(occupied);
                    view.Rebuild(map);
                    foreach (var anchor in anchors)
                    {
                        var prop = (ObstacleDungeonObjectDefinition)anchor.ObjectDefinition;
                        var sprite = prop.PresentationSprite;
                        var renderer = new GameObject(prop.DisplayName).AddComponent<SpriteRenderer>();
                        renderer.transform.SetParent(root.transform, false);
                        renderer.sprite = sprite;
                        renderer.transform.position = view.CellToWorldCenter(anchor.LocalCell);
                        var floorRenderer = floor.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                        renderer.sortingLayerID = floorRenderer.sortingLayerID;
                        renderer.sortingOrder = floorRenderer.sortingOrder + 100 - anchor.LocalCell.y;
                    }
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        var material = new Material(Shader.Find("Sprites/Default"));
                        if (renderer is SpriteRenderer sr)
                        {
                            material.mainTexture = sr.sprite != null ? sr.sprite.texture : null;
                            sr.forceRenderingOff = false;
                        }
                        materials.Add(material);
                        renderer.sharedMaterial = material;
                    }
                    var camera = new GameObject("Audit camera").AddComponent<Camera>();
                    camera.transform.SetParent(root.transform, false);
                    camera.transform.position = new Vector3(room.SizeInCells.x * .5f, room.SizeInCells.y * .5f, -10);
                    camera.orthographic = true;
                    camera.orthographicSize = Mathf.Max(room.SizeInCells.y, room.SizeInCells.x) * .5f + 2;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.025f,.03f,.025f);
                    target = new RenderTexture(1000,1000,24);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    pixels = new Texture2D(1000,1000,TextureFormat.RGBA32,false);
                    pixels.ReadPixels(new Rect(0,0,1000,1000),0,0); pixels.Apply();
                    File.WriteAllBytes("out/forest-composition-" + id + ".png", pixels.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(root);
                    foreach (var material in materials) Object.DestroyImmediate(material);
                    if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                    if (pixels != null) Object.DestroyImmediate(pixels);
                }
            }
        }

        [MenuItem("Godmist/Content/Pass 1b/Forest Scale Preview")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ForestPrototypeSetup.Configure();
            Directory.CreateDirectory("Assets/_Project/Scenes/Prototypes/ForestScaleMaterials");
            var camera = new GameObject("Scale camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f,.17f,.15f);
            var vegetation = AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset");
            var sprites = new[] { vegetation.Deadwood, vegetation.LowShrub, vegetation.ExposedRoots, vegetation.WoodlandFungi, vegetation.WoodlandFern, vegetation.FallenLog, vegetation.SmallStone, vegetation.DryShrub };
            var widths = new[] { 1.225f, 1.125f, 1.175f, 1.525f, 1.225f, 2f, .75f, 1.25f };
            var names = new[] { "Deadwood", "Shrub", "Roots", "Fungi", "Fern", "Fallen log", "Small stone", "Dry shrub" };
            for (var i=0;i<sprites.Length;i++)
            {
                var at = new Vector3(-9.1f+i*2.6f,3,0);
                AddSprite(names[i], sprites[i], at, widths[i], true);
                Hero(at+Vector3.right*.9f);
                Label(names[i],at+Vector3.down*.7f);
            }
            var assets = new[] { "Boulder", "Stump", "RuinedPillar", "ColdCampfire", "BrokenCrate", "Gravestone", "Bedroll" };
            for(var i=0;i<assets.Length;i++)
            {
                var definition=AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_"+assets[i]+"_Prototype.asset");
                var at=new Vector3(-9f+i*3,-.5f,0);
                AddSprite(assets[i],definition.PresentationSprite,at,definition.PresentationWidth,false);
                Hero(at+Vector3.right*.9f);
                Label(assets[i],at+Vector3.down*.7f);
            }
            var stump = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Stump_Prototype.asset");
            AddSprite("Rotten stump", stump.PresentationVariants[0], new Vector3(3,-5f,0), 1.25f, false);
            Hero(new Vector3(4,-5f,0));
            Label("Rotten stump",new Vector3(3,-5.5f,0));
            var boulder = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Boulder_Prototype.asset");
            AddSprite("Fractured boulder", boulder.PresentationVariants[0], new Vector3(7,-5f,0), 1.5f, false);
            Hero(new Vector3(8.1f,-5f,0));
            Label("Fractured boulder",new Vector3(7,-5.5f,0));
            var hollow = vegetation.TreeVariants[2];
            AddSprite("Hollow tree", hollow, new Vector3(-2,-5f,0), 2f, false);
            Hero(new Vector3(-.5f,-5f,0));
            Label("Hollow tree",new Vector3(-2,-5.5f,0));
            for(int x=-8;x<=8;x++) Line(new Vector3(x,-4),new Vector3(x,4));
            for(int y=-4;y<=4;y++) Line(new Vector3(-8,y),new Vector3(8,y));
            Directory.CreateDirectory("Assets/_Project/Scenes/Prototypes");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/Prototypes/ForestScalePreview.unity");
            Directory.CreateDirectory("out");
            var target=new RenderTexture(1600,900,24);
            camera.targetTexture=target;
            camera.Render();
            RenderTexture.active=target;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);
            image.Apply();
            File.WriteAllBytes("out/forest-scale-preview.png",image.EncodeToPNG());
            camera.targetTexture=null;
            RenderTexture.active=null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
        [MenuItem("Godmist/Content/Pass 1b/Forest Ten Variants Preview")]
        public static void BuildTenVariants()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ForestPrototypeSetup.Configure();
            Directory.CreateDirectory("Assets/_Project/Scenes/Prototypes/ForestScaleMaterials");
            var camera = new GameObject("Ten variants scale camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0,0,-10);
            camera.orthographic = true; camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f,.17f,.15f);
            var names = new[] { "Shelf fungi", "Broad fern", "Tangled roots", "Forked branch", "Hollow log", "Angular stone", "Bramble", "Sedge", "Charred stump", "Lichen boulder" };
            var stems = new[] { "forest-fungi-shelf-v1", "forest-fern-broad-v1", "forest-roots-tangled-v1", "forest-deadwood-forked-v1", "forest-log-hollow-v1", "forest-stone-angular-v1", "forest-shrub-bramble-v1", "forest-grass-sedge-v1", "forest-stump-charred-v1", "forest-boulder-lichen-v1" };
            for (var i=0; i<stems.Length; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/"+stems[i]+"-32ppu.png");
                var at = new Vector3(-7.2f+(i%5)*3.5f,i<5?2f:-1.8f,0);
                AddSprite(names[i],sprite,at,1,i<8);
                Hero(at+Vector3.right*1.2f);
                Label(names[i],at+Vector3.down*.85f);
            }
            for(int x=-8;x<=8;x++) Line(new Vector3(x,-4),new Vector3(x,4));
            for(int y=-4;y<=4;y++) Line(new Vector3(-8,y),new Vector3(8,y));
            Directory.CreateDirectory("Assets/_Project/Scenes/Prototypes");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/Prototypes/ForestTenVariantsPreview.unity");
            Directory.CreateDirectory("out");
            var target = new RenderTexture(1800,1000,24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1800,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1800,1000),0,0); image.Apply();
            File.WriteAllBytes("out/forest-ten-variants-preview.png",image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
        private static void Hero(Vector3 at)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position=at;
            var view=instance.GetComponent<ExplorationCharacterView>();
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
            view.BodyRenderer.enabled=true;
            view.BodyRenderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png");
            view.BodyRenderer.sortingOrder=20;
            view.BodyRenderer.sharedMaterial=PreviewMaterial("Jerzy",view.BodyRenderer.sprite.texture);
        }
        private static void AddSprite(string name,Sprite sprite,Vector3 at,float width,bool centered)
        {
            var renderer=new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite=sprite;
            renderer.sharedMaterial=PreviewMaterial(name,sprite.texture);
            renderer.transform.localScale=Vector3.one;
            renderer.transform.position=at;
            if(centered) renderer.transform.position+=at-renderer.bounds.center;
            renderer.sortingOrder=10;
        }
        private static void Label(string text,Vector3 at)
        {
            var label=new GameObject(text+" label").AddComponent<TextMesh>();
            label.text=text; label.characterSize=.12f; label.fontSize=32;
            label.anchor=TextAnchor.MiddleCenter; label.transform.position=at;
        }
        private static Material PreviewMaterial(string name,Texture texture)
        {
            var path="Assets/_Project/Scenes/Prototypes/ForestScaleMaterials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material,path);}
            material.mainTexture=texture; EditorUtility.SetDirty(material);
            return material;
        }
        private static void Line(Vector3 from,Vector3 to)
        {
            var line=new GameObject("1 unit grid").AddComponent<LineRenderer>();
            line.sharedMaterial=PreviewMaterial("Grid",null);
            line.positionCount=2;line.SetPosition(0,from);line.SetPosition(1,to);
            line.startWidth=line.endWidth=.012f;
            line.startColor=line.endColor=new Color(.3f,.32f,.28f);
            line.sortingOrder=-10;
        }
    }
}
