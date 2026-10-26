using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Imports the approved Catacombs floor module and binds its sixteen logical
/// tiles to the existing dungeon floor views. The runtime keeps the original
/// single floor tile as a fallback for test fixtures and older scenes.
/// </summary>
public static class VS22CatacombsTilesetSetup
{
    private const string FloorTilePath = "Assets/_Project/Art/Tiles/DungeonFloorSimple32.png";
    private const string FloorTileAssetPath = "Assets/_Project/Art/Tiles/T_DungeonFloor_0x72.asset";
    private const string ModuleTileRoot = "Assets/_Project/Art/Tiles/CatacombsFloor/";
    private const string WallPrefabPath = "Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab";
    private const string PillarPrefabPath = "Assets/_Project/Prefabs/Dungeon/PF_DungeonPillar.prefab";
    private const string ColumnTilePath = "Assets/_Project/Art/Tiles/DungeonColumn32x64.png";
    private const string ColumnVisualChildName = "CatacombsColumnVisual";
    private const string ReviewRelativeRoot = "catacombs-column-style-tiles";
    private const string ColumnReviewRelativeRoot = "catacombs-columns";
    private const string ModulePrefix = "floor-module128-v3-tile-";
    private const int ModuleSide = 4;
    private const int ModuleTileCount = ModuleSide * ModuleSide;

    private static readonly string[] ScenePaths =
    {
        "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity"
    };

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string ReviewRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", "output", "asset-review", ReviewRelativeRoot));
    private static string ColumnReviewRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", "output", "asset-review", ColumnReviewRelativeRoot));
    private static string PreviewPath => Path.Combine(ReviewRoot, "unity-catacombs-tileset-preview.png");

    [MenuItem("Godmist/Content/Configure VS-22 Catacombs floor module")]
    public static void ConfigureFloorModule()
    {
        EnsureNotPlaying();
        ConfigureFloorModuleInternal();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        VerifyFloorModule();
        Debug.Log("VS-22: configured Catacombs floor module (16 tiles, 4x4 world modulo) in DungeonSandbox.");
    }

    [MenuItem("Godmist/Content/Configure VS-22 Catacombs tileset")]
    public static void ConfigureCatacombsTileset()
    {
        EnsureNotPlaying();
        ConfigureFloorModuleInternal();
        ConfigureWallSprites();
        ConfigureColumnSprite();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        VerifyAll();
        GenerateUnityPreview();
        Debug.Log("VS-22: configured complete Catacombs tileset (16 floor tiles, 5 wall sprites, 1 pillar sprite).");
    }

    private static void EnsureNotPlaying()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before authoring the Catacombs tileset.");
    }

    private static void ConfigureFloorModuleInternal()
    {
        EnsureFolder(ModuleTileRoot);
        var tiles = new TileBase[ModuleTileCount];
        for (var index = 0; index < ModuleTileCount; index++)
        {
            var sourceName = ModulePrefix + index.ToString("00") + ".png";
            var sourcePath = Path.Combine(ReviewRoot, sourceName);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Missing Catacombs floor module source", sourcePath);

            var targetPath = index == 0
                ? FloorTilePath
                : ModuleTileRoot + sourceName;
            if (index == 0 && !FilesEqual(sourcePath, ProjectPath(targetPath)))
                PreserveExistingFloorTexture(targetPath);
            CopyIfDifferent(sourcePath, ProjectPath(targetPath));
            var sprite = ImportSprite(targetPath);
            tiles[index] = ConfigureTileAsset(index, sprite);
        }

        BindScenes(tiles);
    }

    [MenuItem("Godmist/Content/Verify VS-22 Catacombs floor module")]
    public static void VerifyFloorModule()
    {
        var tiles = new TileBase[ModuleTileCount];
        for (var index = 0; index < ModuleTileCount; index++)
        {
            var sourceName = ModulePrefix + index.ToString("00") + ".png";
            var targetPath = index == 0 ? FloorTilePath : ModuleTileRoot + sourceName;
            AssertImportedTexture(targetPath);
            var tilePath = index == 0
                ? FloorTileAssetPath
                : ModuleTileRoot + "T_DungeonFloor_Module_" + index.ToString("00") + ".asset";
            tiles[index] = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
            if (tiles[index] == null)
                throw new InvalidOperationException("Missing Catacombs floor tile asset: " + tilePath);
            if (tiles[index] is Tile tile && tile.sprite == null)
                throw new InvalidOperationException("Missing sprite on Catacombs floor tile: " + tilePath);
        }

        foreach (var scenePath in ScenePaths)
            VerifyScene(scenePath, tiles);

        Debug.Log("VS-22: verified Catacombs floor module (16 imported 32x32 tiles, stable 4x4 world modulo, DungeonSandbox).");
    }

    [MenuItem("Godmist/Content/Verify VS-22 Catacombs tileset")]
    public static void VerifyAll()
    {
        VerifyFloorModule();
        VerifyWallSprites();
        VerifyColumnSprite();
        Debug.Log("VS-22: verified complete Catacombs tileset bindings.");
    }

    private static void ConfigureWallSprites()
    {
        var bindings = new[]
        {
            new SpriteBinding("wall-top-v3.png", "Assets/_Project/Art/Tiles/DungeonWallTop32.png", "verticalWallSprite", 8, 32),
            new SpriteBinding("wall-simple-v3.png", "Assets/_Project/Art/Tiles/DungeonWallSimple32.png", "horizontalWallSprite", 8, 32),
            new SpriteBinding("wall-side-v3.png", "Assets/_Project/Art/Tiles/DungeonWallSide32.png", "sideWallSprite", 8, 32),
            new SpriteBinding("wall-north-v3.png", "Assets/_Project/Art/Tiles/DungeonWallNorth32.png", "northWallSprite", 32, 32),
            new SpriteBinding("wall-south-v3.png", "Assets/_Project/Art/Tiles/DungeonWallSouth32.png", "southWallSprite", 32, 8)
        };

        var sprites = new Dictionary<string, Sprite>();
        foreach (var binding in bindings)
        {
            var sourcePath = Path.Combine(ReviewRoot, binding.SourceName);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Missing Catacombs wall source", sourcePath);
            if (!FilesEqual(sourcePath, ProjectPath(binding.TargetPath)))
                PreserveExistingTexture(binding.TargetPath, Path.GetFileNameWithoutExtension(binding.TargetPath) + "-before-wall-v3.png");
            CopyIfDifferent(sourcePath, ProjectPath(binding.TargetPath));
            sprites[binding.FieldName] = ImportSprite(binding.TargetPath);
        }

        var prefabContents = PrefabUtility.LoadPrefabContents(WallPrefabPath);
        try
        {
            var view = prefabContents.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();
            if (view == null)
                throw new InvalidOperationException("Missing DungeonWallSegmentView on " + WallPrefabPath);
            var serialized = new SerializedObject(view);
            foreach (var binding in bindings)
            {
                var property = serialized.FindProperty(binding.FieldName);
                if (property == null)
                    throw new InvalidOperationException("Missing wall prefab field " + binding.FieldName);
                property.objectReferenceValue = sprites[binding.FieldName];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, WallPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static void VerifyWallSprites()
    {
        var expected = new[]
        {
            new SpriteBinding("wall-top-v3.png", "Assets/_Project/Art/Tiles/DungeonWallTop32.png", "verticalWallSprite", 8, 32),
            new SpriteBinding("wall-simple-v3.png", "Assets/_Project/Art/Tiles/DungeonWallSimple32.png", "horizontalWallSprite", 8, 32),
            new SpriteBinding("wall-side-v3.png", "Assets/_Project/Art/Tiles/DungeonWallSide32.png", "sideWallSprite", 8, 32),
            new SpriteBinding("wall-north-v3.png", "Assets/_Project/Art/Tiles/DungeonWallNorth32.png", "northWallSprite", 32, 32),
            new SpriteBinding("wall-south-v3.png", "Assets/_Project/Art/Tiles/DungeonWallSouth32.png", "southWallSprite", 32, 8)
        };
        foreach (var binding in expected)
            AssertImportedTexture(binding.TargetPath, binding.Width, binding.Height);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Missing Catacombs wall prefab: " + WallPrefabPath);
        var view = prefab.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();
        if (view == null)
            throw new InvalidOperationException("Missing DungeonWallSegmentView on " + WallPrefabPath);
        var serialized = new SerializedObject(view);
        foreach (var binding in expected)
        {
            var property = serialized.FindProperty(binding.FieldName);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(binding.TargetPath);
            if (property == null || property.objectReferenceValue != sprite)
                throw new InvalidOperationException("Wall prefab binding mismatch: " + binding.FieldName);
        }
    }

    private static void ConfigureColumnSprite()
    {
        const string sourceName = "column-intact-32x64-v1.png";
        var sourcePath = Path.Combine(ColumnReviewRoot, sourceName);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Missing Catacombs column source", sourcePath);
        if (!FilesEqual(sourcePath, ProjectPath(ColumnTilePath)))
            PreserveExistingTexture(ColumnTilePath, "DungeonColumn32x64-before-column-v1.png");
        CopyIfDifferent(sourcePath, ProjectPath(ColumnTilePath));
        var sprite = ImportSprite(ColumnTilePath, true);

        var prefabContents = PrefabUtility.LoadPrefabContents(PillarPrefabPath);
        try
        {
            var rootRenderer = prefabContents.GetComponent<SpriteRenderer>();
            if (rootRenderer == null)
                rootRenderer = prefabContents.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();
            if (rootRenderer == null)
                throw new InvalidOperationException("Missing SpriteRenderer on " + PillarPrefabPath);

            var visualObject = prefabContents.transform.Find(ColumnVisualChildName);
            if (visualObject == null)
            {
                visualObject = new GameObject(ColumnVisualChildName).transform;
                visualObject.SetParent(prefabContents.transform, false);
            }
            visualObject.localPosition = Vector3.zero;
            visualObject.localRotation = Quaternion.identity;
            var parentScaleY = Mathf.Abs(prefabContents.transform.localScale.y);
            visualObject.localScale = new Vector3(1f, parentScaleY > .001f ? 1f / parentScaleY : 1f, 1f);
            var visualRenderer = visualObject.GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
                visualRenderer = visualObject.gameObject.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = sprite;
            visualRenderer.enabled = true;
            visualRenderer.sortingLayerID = rootRenderer.sortingLayerID;
            visualRenderer.sortingOrder = rootRenderer.sortingOrder;
            visualRenderer.color = rootRenderer.color;
            visualRenderer.sharedMaterial = rootRenderer.sharedMaterial;
            visualRenderer.flipX = rootRenderer.flipX;
            visualRenderer.flipY = rootRenderer.flipY;
            rootRenderer.sprite = sprite;
            rootRenderer.enabled = false;
            EditorUtility.SetDirty(rootRenderer);
            EditorUtility.SetDirty(visualRenderer);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, PillarPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static void VerifyColumnSprite()
    {
        AssertImportedTexture(ColumnTilePath, 32, 64);
        var importer = AssetImporter.GetAtPath(ColumnTilePath) as TextureImporter;
        var columnSettings = importer == null ? null : new TextureImporterSettings();
        importer?.ReadTextureSettings(columnSettings);
        if (importer == null
            || columnSettings == null
            || columnSettings.spriteAlignment != (int)SpriteAlignment.Custom
            || Vector2.Distance(columnSettings.spritePivot, new Vector2(.5f, 0f)) > .001f)
            throw new InvalidOperationException("Catacombs column must use a centered foot pivot.");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ColumnTilePath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PillarPrefabPath);
        if (sprite == null || prefab == null)
            throw new InvalidOperationException("Missing imported Catacombs pillar asset or prefab.");
        var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        var visualRenderer = renderers.FirstOrDefault(renderer => renderer.name == ColumnVisualChildName);
        var rootRenderer = prefab.GetComponent<SpriteRenderer>();
        if (visualRenderer == null || visualRenderer.sprite != sprite || !visualRenderer.enabled
            || rootRenderer == null || rootRenderer.enabled
            || Mathf.Abs(prefab.transform.lossyScale.y * visualRenderer.transform.localScale.y - 1f) > .001f)
            throw new InvalidOperationException("Pillar prefab does not reference " + ColumnTilePath);
    }

    private static void GenerateUnityPreview()
    {
        var tileAssets = Enumerable.Range(0, ModuleTileCount)
            .Select(index => AssetDatabase.LoadAssetAtPath<TileBase>(index == 0
                ? FloorTileAssetPath
                : ModuleTileRoot + "T_DungeonFloor_Module_" + index.ToString("00") + ".asset"))
            .ToArray();
        var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
        var pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PillarPrefabPath);
        if (tileAssets.Any(tile => tile == null) || wallPrefab == null || pillarPrefab == null)
            throw new InvalidOperationException("Cannot build Catacombs Unity preview because a bound asset is missing.");

        Directory.CreateDirectory(ReviewRoot);
        var previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(previewScene);
        RenderTexture renderTexture = null;
        Texture2D capture = null;
        Camera camera = null;
        try
        {
            var previewRoot = new GameObject("PreviewDungeon");
            SceneManager.MoveGameObjectToScene(previewRoot, previewScene);
            var grid = previewRoot.AddComponent<Grid>();
            var tilemapObject = new GameObject("PreviewFloor");
            tilemapObject.transform.SetParent(previewRoot.transform, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = 0;
            var viewObject = new GameObject("PreviewFloorMapView");
            viewObject.transform.SetParent(previewRoot.transform, false);
            var floorView = viewObject.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
            var viewSerialized = new SerializedObject(floorView);
            viewSerialized.FindProperty("floorTilemap").objectReferenceValue = tilemap;
            viewSerialized.FindProperty("floorTile").objectReferenceValue = tileAssets[0];
            viewSerialized.FindProperty("wallSegmentPrefab").objectReferenceValue =
                wallPrefab.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();
            var module = viewSerialized.FindProperty("floorModuleTiles");
            module.arraySize = tileAssets.Length;
            for (var index = 0; index < tileAssets.Length; index++)
                module.GetArrayElementAtIndex(index).objectReferenceValue = tileAssets[index];
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
            const int roomWidth = 8;
            const int roomHeight = 6;
            for (var y = 0; y < roomHeight; y++)
                for (var x = 0; x < roomWidth; x++)
                    map.TryAddFloorCell(new Vector2Int(x, y), "VS22PreviewRoom");
            for (var x = 0; x < roomWidth; x++)
            {
                map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(
                    new Vector2Int(x, 0), new Vector2Int(x, -1)), Godmist.Core.Enums.DungeonWallType.Full);
                map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(
                    new Vector2Int(x, roomHeight - 1), new Vector2Int(x, roomHeight)), Godmist.Core.Enums.DungeonWallType.Full);
            }
            for (var y = 0; y < roomHeight; y++)
            {
                map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(
                    new Vector2Int(0, y), new Vector2Int(-1, y)), Godmist.Core.Enums.DungeonWallType.Full);
                map.SetWallType(new Godmist.Runtime.Dungeons.DungeonCellEdge(
                    new Vector2Int(roomWidth - 1, y), new Vector2Int(roomWidth, y)), Godmist.Core.Enums.DungeonWallType.Full);
            }
            floorView.Rebuild(map);

            var pillar = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, previewScene);
            pillar.name = "PreviewColumn";
            pillar.transform.position = new Vector3(3.5f, 2.5f, 0f);
            foreach (var renderer in pillar.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sortingOrder = 5;

            var cameraObject = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            cameraObject.transform.position = new Vector3(3.5f, 2.5f, -10f);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.75f;
            camera.aspect = 4f / 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.03f, 0.04f, 1f);
            renderTexture = new RenderTexture(768, 576, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            capture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(PreviewPath, capture.EncodeToPNG());
        }
        finally
        {
            if (camera != null)
                camera.targetTexture = null;
            RenderTexture.active = null;
            if (capture != null)
                UnityEngine.Object.DestroyImmediate(capture);
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
            if (previewScene.IsValid() && previewScene.isLoaded)
                EditorSceneManager.CloseScene(previewScene, true);
        }
    }

    private static int FloorModuleIndex(int x, int y)
    {
        var column = PositiveModulo(x, ModuleSide);
        var row = PositiveModulo(3 - y, ModuleSide);
        return row * ModuleSide + column;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private static void BindPreviewWall(
        GameObject prefab,
        Tilemap tilemap,
        Vector2Int first,
        Vector2Int second,
        Godmist.Views.Dungeons.DungeonWallSurfaceProfile profile)
    {
        var wall = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tilemap.gameObject.scene);
        wall.name = "PreviewWall_" + profile;
        var view = wall.GetComponent<Godmist.Views.Dungeons.DungeonWallSegmentView>();
        view.Bind(new Godmist.Runtime.Dungeons.DungeonCellEdge(first, second), tilemap, true, false, false, false, profile);
    }

    private readonly struct SpriteBinding
    {
        public SpriteBinding(string sourceName, string targetPath, string fieldName, int width, int height)
        {
            SourceName = sourceName;
            TargetPath = targetPath;
            FieldName = fieldName;
            Width = width;
            Height = height;
        }

        public string SourceName { get; }
        public string TargetPath { get; }
        public string FieldName { get; }
        public int Width { get; }
        public int Height { get; }
    }

    private static TileBase ConfigureTileAsset(int index, Sprite sprite)
    {
        var path = index == 0
            ? FloorTileAssetPath
            : ModuleTileRoot + "T_DungeonFloor_Module_" + index.ToString("00") + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = sprite;
        tile.flags = TileFlags.LockTransform;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void BindScenes(IReadOnlyList<TileBase> tiles)
    {
        foreach (var scenePath in ScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var views = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonFloorMapView>(true))
                .ToArray();
            if (views.Length == 0)
                throw new InvalidOperationException("No DungeonFloorMapView found in " + scenePath);

            foreach (var view in views)
            {
                var serialized = new SerializedObject(view);
                var module = serialized.FindProperty("floorModuleTiles");
                if (module == null)
                    throw new InvalidOperationException("DungeonFloorMapView.floorModuleTiles is missing.");
                module.arraySize = tiles.Count;
                for (var index = 0; index < tiles.Count; index++)
                    module.GetArrayElementAtIndex(index).objectReferenceValue = tiles[index];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + scenePath);
        }
    }

    private static void VerifyScene(string scenePath, IReadOnlyList<TileBase> expected)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var views = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonFloorMapView>(true))
            .ToArray();
        if (views.Length == 0)
            throw new InvalidOperationException("No DungeonFloorMapView found in " + scenePath);

        foreach (var view in views)
        {
            var serialized = new SerializedObject(view);
            var module = serialized.FindProperty("floorModuleTiles");
            if (module == null || module.arraySize != expected.Count)
                throw new InvalidOperationException("Invalid floor module binding in " + scenePath);
            for (var index = 0; index < expected.Count; index++)
                if (module.GetArrayElementAtIndex(index).objectReferenceValue != expected[index])
                    throw new InvalidOperationException("Floor module tile " + index + " is not bound in " + scenePath);
        }
    }

    private static Sprite ImportSprite(string assetPath, bool footPivot = false)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
            ?? throw new InvalidOperationException("Missing texture importer: " + assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteAlignment = (int)(footPivot ? SpriteAlignment.Custom : SpriteAlignment.Center);
        if (footPivot)
            textureSettings.spritePivot = new Vector2(.5f, 0f);
        importer.SetTextureSettings(textureSettings);
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.textureCompression = TextureImporterCompression.Uncompressed;
        defaults.crunchedCompression = false;
        importer.SetPlatformTextureSettings(defaults);
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        standalone.textureCompression = TextureImporterCompression.Uncompressed;
        standalone.crunchedCompression = false;
        importer.SetPlatformTextureSettings(standalone);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)
            ?? throw new InvalidOperationException("Missing imported sprite: " + assetPath);
    }

    private static void AssertImportedTexture(string assetPath)
    {
        AssertImportedTexture(assetPath, 32, 32);
    }

    private static void AssertImportedTexture(string assetPath, int expectedWidth, int expectedHeight)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (texture == null || importer == null)
            throw new InvalidOperationException("Missing imported texture: " + assetPath);
        if (texture.width != expectedWidth || texture.height != expectedHeight
            || importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || Mathf.Abs(importer.spritePixelsPerUnit - 32f) > .001f
            || importer.filterMode != FilterMode.Point
            || importer.wrapMode != TextureWrapMode.Clamp
            || importer.mipmapEnabled
            || importer.GetDefaultPlatformTextureSettings().textureCompression != TextureImporterCompression.Uncompressed
            || importer.GetPlatformTextureSettings("Standalone").textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException("Invalid texture import settings: " + assetPath);
    }

    private static void PreserveExistingFloorTexture(string targetPath)
    {
        var target = ProjectPath(targetPath);
        if (!File.Exists(target))
            return;
        var backup = Path.Combine(ReviewRoot, "DungeonFloorSimple32-before-floor-module128-v3.png");
        if (!File.Exists(backup))
        {
            File.Copy(target, backup);
            return;
        }

        if (!FilesEqual(target, backup))
            throw new InvalidOperationException("Existing floor module backup does not match target; refusing overwrite.");
    }

    private static void PreserveExistingTexture(string targetPath, string backupName)
    {
        var target = ProjectPath(targetPath);
        if (!File.Exists(target))
            return;
        Directory.CreateDirectory(ReviewRoot);
        var backup = Path.Combine(ReviewRoot, backupName);
        if (!File.Exists(backup))
        {
            File.Copy(target, backup);
            return;
        }

        if (!FilesEqual(target, backup))
            throw new InvalidOperationException("Existing asset backup does not match target; refusing overwrite: " + targetPath);
    }

    private static void CopyIfDifferent(string source, string target)
    {
        if (!FilesEqual(source, target))
            File.Copy(source, target, true);
    }

    private static bool FilesEqual(string first, string second)
    {
        if (!File.Exists(first) || !File.Exists(second))
            return false;
        var firstInfo = new FileInfo(first);
        var secondInfo = new FileInfo(second);
        return firstInfo.Length == secondInfo.Length
               && File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second));
    }

    private static string ProjectPath(string assetPath)
        => Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

    private static void EnsureFolder(string assetPath)
    {
        var absolute = ProjectPath(assetPath.TrimEnd('/'));
        Directory.CreateDirectory(absolute);
        AssetDatabase.Refresh();
    }
}
