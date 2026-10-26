using System;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CatacombsInteractableArtSetup
{
    private const string Prefabs = "Assets/_Project/Prefabs/Dungeon/";
    private const string Art = "Assets/_Project/Art/Props/Catacombs/";

    [MenuItem("Godmist/Art/Apply Catacombs Interactable Art")]
    public static void Apply()
    {
        ImportPreviewAssets();
        Godmist.Editor.CampingKitIconSetup.ConfigureIcon();
        EditPrefab("PF_DungeonChest", root =>
        {
            root.transform.localScale = Vector3.one;
            SetSprite(root.GetComponent<SpriteRenderer>(), "CatacombsChestClosed32-v1");
        });
        EditPrefab("PF_DungeonDoor_Debug", root =>
        {
            SetSprite(root.transform.Find("Visual").GetComponent<SpriteRenderer>(), "CatacombsDoorClosed32-v1");
            var serialized = new SerializedObject(root.GetComponent<DungeonDoorView>());
            // Keep orientation art assigned by the room-door setup when this
            // general import action is repeated later.
            var oriented = serialized.FindProperty("horizontalEdgeSprite").objectReferenceValue as Sprite;
            if (oriented != null) root.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite = oriented;
            serialized.FindProperty("preserveSpriteProportions").boolValue = true;
            serialized.FindProperty("closedColor").colorValue = Color.white;
            serialized.FindProperty("openColor").colorValue = oriented != null ? Color.white : new Color(1, 1, 1, 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        });
        EditPrefab("PF_DungeonExitView", root =>
        {
            var marker = root.transform.Find("Marker");
            marker.localScale = Vector3.one;
            SetSprite(marker.GetComponent<SpriteRenderer>(), "CatacombsStairsDown32-v2");
            var label = marker.GetComponentInChildren<TMPro.TextMeshPro>(true);
            if (label != null)
            {
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            }
        });
        // The open doorway is already drawn by the tilemap. A second legacy
        // door-shaped marker would overlap the real interactive door sprite.
        EditPrefab("PF_DungeonDoorwayView", root =>
            root.GetComponentInChildren<SpriteRenderer>(true).enabled = false);
        AssetDatabase.SaveAssets();
        ValidateDoorPresentation();
        Debug.Log("CATACOMBS INTERACTABLE INTEGRATION PASS: camping kit, chest, door and stairs bound; both door edges and open collision checked.");
    }

    private static void EditPrefab(string name, Action<GameObject> edit)
    {
        var path = Prefabs + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void SetSprite(SpriteRenderer renderer, string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        if (renderer == null || sprite == null) throw new InvalidOperationException("Missing interactable art: " + name);
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.transform.localScale = Vector3.one;
    }

    private static void ValidateDoorPresentation()
    {
        var test = new GameObject("Interactable art validation");
        var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
        try
        {
            var party = new GameObject("Party").AddComponent<ExplorationPartyController>();
            party.transform.SetParent(test.transform);
            var grid = new GameObject("Grid", typeof(Grid)); grid.transform.SetParent(test.transform);
            var tileObject = new GameObject("Floor", typeof(Tilemap)); tileObject.transform.SetParent(grid.transform);
            var tilemap = tileObject.GetComponent<Tilemap>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "PF_DungeonDoor_Debug.prefab");
            foreach (var delta in new[] { Vector2Int.right, Vector2Int.up })
            {
                var instance = UnityEngine.Object.Instantiate(prefab, test.transform);
                var door = new DoorDungeonObjectInstance("art-door", definition, new DungeonCellEdge(Vector2Int.zero, delta));
                var view = instance.GetComponent<DungeonDoorView>();
                view.Initialize(party, new DungeonFloorInstance(0), door, tilemap);
                var renderer = instance.GetComponentInChildren<SpriteRenderer>();
                var collider = instance.GetComponent<BoxCollider2D>();
                var expectedCollider = delta.x != 0 ? new Vector2(.16f, 1) : new Vector2(1, .16f);
                var frontRatio = new SerializedObject(view).FindProperty("frontHeightRatio").floatValue;
                var sideRatio = new SerializedObject(view).FindProperty("sideLengthRatio").floatValue;
                var expectedScale = new Vector3(1, delta.x != 0 ? sideRatio : frontRatio, 1);
                if (renderer.transform.localScale != expectedScale || renderer.color != Color.white ||
                    !collider.enabled || (collider.size - expectedCollider).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Door art or blocking geometry changed.");
                door.TrySetOpen(true); view.RefreshState();
                var hasOpenArt = new SerializedObject(view).FindProperty("verticalEdgeSprite").objectReferenceValue != null;
                if (collider.enabled || renderer.color.a != (hasOpenArt ? 1 : 0))
                    throw new InvalidOperationException("Open door still blocks or displays closed art.");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            foreach (var pair in new[] { ("PF_DungeonChest", "CatacombsChestClosed32-v1"), ("PF_DungeonExitView", "CatacombsStairsDown32-v2") })
            {
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + pair.Item1 + ".prefab"), test.transform);
                var renderer = instance.GetComponentInChildren<SpriteRenderer>();
                if (renderer.sprite != AssetDatabase.LoadAssetAtPath<Sprite>(Art + pair.Item2 + ".png") ||
                    renderer.transform.lossyScale != Vector3.one || renderer.color != Color.white)
                    throw new InvalidOperationException("Incorrect art or scale: " + pair.Item1);
                if (pair.Item1 == "PF_DungeonExitView")
                {
                    var label = instance.GetComponentInChildren<TMPro.TextMeshPro>(true);
                    if (label != null) throw new InvalidOperationException("Legacy exit label is still present.");
                }
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(test); UnityEngine.Object.DestroyImmediate(definition); }
    }

    public static void ImportPreviewAssets()
    {
        AssetDatabase.Refresh();
        foreach (var name in new[] { "CatacombsDoorClosed32-v1", "CatacombsChestClosed32-v1", "CatacombsStairsDown32-v2" })
        {
            var path = "Assets/_Project/Art/Props/Catacombs/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = name == "CatacombsStairsDown32-v2" ? new Vector2(.5f, .5f) : new Vector2(.5f, 2f / 32f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.rect.width != 32 || sprite.rect.height != 32)
                throw new System.InvalidOperationException("Invalid catacombs sprite: " + path);
        }
        Debug.Log("CATACOMBS INTERACTABLE ART IMPORT PASS: three 32x32 Point sprites, 32 PPU");
    }
}
