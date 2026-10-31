using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    /// <summary>Exhaustive local topology review using the production renderer, in a disposable preview scene.</summary>
    public static class CatacombsWallRulesetReview
    {
        // Quadrants SW, SE, NW, NE; arms W, E, S, N.
        static readonly Vector2Int[] Cells = { new(-1,-1), new(0,-1), new(-1,0), new(0,0) };
        static readonly int[] First = { 0, 1, 0, 2 };
        static readonly int[] Second = { 2, 3, 1, 3 };
        public static string Output => Path.GetFullPath("../LocalHandoffs/catacombs-wall-ruleset-20261030");

        [Serializable] public sealed class Case
        {
            public string id;
            public int floorMask;
            public int wallCode;
            public int[] arms;
            public string shape;
            public int low;
            public int full;
            public bool boundaryLow;
            public string[] profiles;
            public string[] joins;
        }
        [Serializable] sealed class Catalog { public Case[] cases; public string contract; }

        public static Case[] Cases()
        {
            var cases = new List<Case>();
            for (var floor = 1; floor < 16; floor++)
                for (var code = 0; code < 81; code++)
                {
                    var arms = new int[4]; var remaining = code; var valid = true; var boundaryLow = false;
                    for (var i = 0; i < 4; i++)
                    {
                        arms[i] = remaining % 3; remaining /= 3;
                        var occupied = ((floor & (1 << First[i])) != 0 ? 1 : 0)
                            + ((floor & (1 << Second[i])) != 0 ? 1 : 0);
                        // Match the actual map API: either wall type needs at least one floor side.
                        if (arms[i] != 0 && occupied == 0) valid = false;
                        if (arms[i] == 1 && occupied == 1) boundaryLow = true;
                    }
                    if (!valid) continue;
                    var count = arms.Count(a => a != 0);
                    var opposite = arms[0] != 0 && arms[1] != 0 || arms[2] != 0 && arms[3] != 0;
                    cases.Add(new Case { id = $"F{floor:D2}-A{code:D2}", floorMask = floor, wallCode = code,
                        arms = arms, boundaryLow = boundaryLow, low = arms.Count(a => a == 1), full = arms.Count(a => a == 2),
                        shape = count switch { 0 => "empty", 1 => "end", 2 => opposite ? "straight" : "corner", 3 => "tee", _ => "cross" } });
                }
            return cases.ToArray();
        }

        public static DungeonFloorMap BuildMap(Case sample, int armLength = 1)
        {
            if (armLength < 1) throw new ArgumentOutOfRangeException(nameof(armLength));
            var map = new DungeonFloorMap();
            for (var i = 0; i < Cells.Length; i++)
                if ((sample.floorMask & (1 << i)) != 0)
                    for (var x = 0; x < armLength; x++)
                        for (var y = 0; y < armLength; y++)
                            map.TryAddFloorCell(Cells[i] + new Vector2Int(Cells[i].x < 0 ? -x : x, Cells[i].y < 0 ? -y : y), "ruleset");
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
            for (var i = 0; i < 4; i++)
                for (var step = 0; step < armLength; step++)
                if (sample.arms[i] != 0 && !map.SetWallType(new DungeonCellEdge(Cells[First[i]] + directions[i] * step, Cells[Second[i]] + directions[i] * step),
                        sample.arms[i] == 1 ? DungeonWallType.Low : DungeonWallType.Full))
                    throw new InvalidOperationException("Invalid wall in " + sample.id);
            return map;
        }

        [MenuItem("Godmist/Diagnostics/Export Catacombs wall ruleset")]
        public static void Export() => Export(Output);

        public static void Export(string output, string[] caseIds = null, int armLength = 1, bool reviewOcclusion = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rendering the ruleset.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            var resolution = reviewOcclusion ? 384 : 192;
            var target = new RenderTexture(resolution,resolution,24);
            var pixels = new Texture2D(resolution,resolution,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var roots = new List<GameObject>();
            Material wallMaterial = null, floorMaterial = null;
            try
            {
                var authored = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonFloorMapView>(true)).Single();
                var authoredCamera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                    .Single(c => c.CompareTag("MainCamera"));
                wallMaterial = Lit("Godmist/Dungeon/Torch Wall");
                floorMaterial = Lit("Godmist/Dungeon/Silhouette Floor");
                var catalog = Cases();
                if (caseIds != null) catalog = catalog.Where(sample => caseIds.Contains(sample.id)).ToArray();
                Directory.CreateDirectory(Path.Combine(output,"native"));
                Directory.CreateDirectory(Path.Combine(output,"lit"));
                if (reviewOcclusion)
                    foreach (var folder in new[] { "without-masks", "floor-only" })
                        Directory.CreateDirectory(Path.Combine(output, folder));
                foreach (var sample in catalog)
                {
                    var root = new GameObject(sample.id,typeof(Grid)); roots.Add(root);
                    SceneManager.MoveGameObjectToScene(root,scene);
                    var view = root.AddComponent<DungeonFloorMapView>();
                    var tilesObject = new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));
                    tilesObject.transform.SetParent(root.transform,false);
                    var destination = new SerializedObject(view); var source = new SerializedObject(authored);
                    destination.FindProperty("floorTilemap").objectReferenceValue = tilesObject.GetComponent<Tilemap>();
                    foreach (var field in new[] { "floorTile", "wallSegmentPrefab", "lowWallPrefab", "railingPrefab" })
                        destination.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
                    destination.ApplyModifiedPropertiesWithoutUndo();
                    view.ConfigureCatacombsArtPresentation(true);
                    var map = BuildMap(sample, armLength); var revision = map.TraversalRevision;
                    view.Rebuild(map);
                    if (revision != map.TraversalRevision) throw new InvalidOperationException("Presentation mutated map: " + sample.id);
                    var walls = view.GetComponentsInChildren<DungeonWallSegmentView>();
                    if (walls.Length != (sample.low + sample.full) * armLength)
                        throw new InvalidOperationException("Missing/duplicate segment in " + sample.id);
                    sample.profiles = walls.Select(w => w.SurfaceProfile.ToString()).Distinct().ToArray();
                    sample.joins = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.Contains("junction") || r.name.Contains("miter"))
                        .Select(r => r.name).ToArray();
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                    var cameraObject = new GameObject("Ruleset camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
                    var camera = cameraObject.GetComponent<Camera>();camera.enabled=false;
                    camera.scene=scene;
                    camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                    camera.orthographic=true;camera.orthographicSize=reviewOcclusion ? 6 : 3;camera.aspect=1;
                    camera.transform.position=new Vector3(0,.5f,-10);
                    camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;
                    camera.backgroundColor=authoredCamera.backgroundColor;camera.targetTexture=target;
                    Capture(camera,target,pixels,Path.Combine(output,"native",sample.id+".png"));
                    if (reviewOcclusion)
                    {
                        // Disposable preview objects only; never edit the authored scene or prefab.
                        var masks = root.GetComponentsInChildren<SpriteRenderer>(true)
                            .Where(r => r.enabled && r.gameObject.activeInHierarchy
                                && (r.name == "SideVoidOcclusion" || r.name == "SouthWallOcclusionMask")).ToArray();
                        foreach (var mask in masks) mask.enabled = false;
                        Capture(camera,target,pixels,Path.Combine(output,"without-masks",sample.id+".png"));
                        foreach (var mask in masks) mask.enabled = true;
                        var visibleSprites = root.GetComponentsInChildren<SpriteRenderer>(true)
                            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                        foreach (var sprite in visibleSprites) sprite.enabled = false;
                        Capture(camera,target,pixels,Path.Combine(output,"floor-only",sample.id+".png"));
                        foreach (var sprite in visibleSprites) sprite.enabled = true;
                    }
                    foreach(var renderer in view.LightingWallRenderers)renderer.sharedMaterial=wallMaterial;
                    tilesObject.GetComponent<TilemapRenderer>().sharedMaterial=floorMaterial;
                    Capture(camera,target,pixels,Path.Combine(output,"lit",sample.id+".png"));
                    camera.targetTexture=null;Object.DestroyImmediate(root);roots.Remove(root);
                }
                var data=JsonUtility.ToJson(new Catalog { cases=catalog,
                    contract=$"{catalog.Length} ground-level local cases: 4 floor quadrants, arms W/E/S/N each absent/low/full; 390 cases include low walls with floor on only one side; flagged separately, not automatically classified as defects. Native32px per world unit; controlled torch pass has no shadow casting. Doors, railings, elevation and wider room layouts are separate contexts." },true);
                File.WriteAllText(Path.Combine(output,"cases.json"),data);
                File.WriteAllText(Path.Combine(output,"cases.js"),"window.ruleset="+data+";");
                var template = Path.Combine(Application.dataPath,"../Tools/Diagnostics/CatacombsWallRuleset/report.html");
                File.Copy(template,Path.Combine(output,"report.html"),true);
                Debug.Log("Catacombs wall ruleset exported: " + catalog.Length + " cases," + (catalog.Length*2) + " production renders. " + output);
            }
            finally
            {
                RenderTexture.active=previous;
                foreach(var root in roots)if(root!=null)Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                if(wallMaterial!=null)Object.DestroyImmediate(wallMaterial);
                if(floorMaterial!=null)Object.DestroyImmediate(floorMaterial);
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            }
        }

        static Material Lit(string shader)
        {
            var material=new Material(Shader.Find(shader));material.SetFloat("_Ambient",.22f);material.SetFloat("_ReceiveShadows",0);
            material.SetInt("_TorchCount",1);
            material.SetVectorArray("_Torches",new[] {new Vector4(-1,-1,6,1),Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            material.SetVectorArray("_TorchColors",Enumerable.Repeat(new Vector4(1.45f,.55f,.14f,1),8).ToArray());
            return material;
        }
        static void Capture(Camera camera,RenderTexture target,Texture2D pixels,string file)
        {
            SortingGroup.UpdateAllSortingGroups();camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            if(pixels.GetPixels32().Select(c=>((int)c.r<<16)|((int)c.g<<8)|c.b).Distinct().Take(5).Count()<5)
                throw new InvalidOperationException("Blank or incomplete GPU capture: "+file);
            File.WriteAllBytes(file,pixels.EncodeToPNG());
        }
    }
}
