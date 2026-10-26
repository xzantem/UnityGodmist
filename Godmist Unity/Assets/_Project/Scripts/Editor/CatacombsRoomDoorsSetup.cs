using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CatacombsRoomDoorsSetup
{
    private const string Rooms = "Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/";
    private const string Art = "Assets/_Project/Art/Props/Catacombs/";
    private const string DoorPrefab = "Assets/_Project/Prefabs/Dungeon/PF_DungeonDoor_Debug.prefab";
    private static readonly string[] RoomNames = { "DRT_Catacombs_Tactical_Passages_Production", "DRT_Catacombs_Tactical_Corridor_Production" };

    [MenuItem("Godmist/Content/Configure Catacombs Room Doors")]
    public static void Configure()
    {
        Import("CatacombsDoorFront32-v3", new Vector2(.5f, 0));
        Import("CatacombsDoorSide32-v2", new Vector2(.5f, .5f));
        var passages = LoadRoom(RoomNames[0]);
        // Single-cell bridges through the existing void divide. The north
        // bridge at x=7 has void below and no floor to its upper-right.
        AddAnchor(passages, DungeonStructuralAnchorType.Door, new Vector2Int(4, 5), DungeonEdgeDirection.Up);
        AddAnchor(passages, DungeonStructuralAnchorType.Door, new Vector2Int(7, 9), DungeonEdgeDirection.Right);
        var corridor = LoadRoom(RoomNames[1]);
        // Close the three-cell neck with one door between two wall segments.
        AddAnchor(corridor, DungeonStructuralAnchorType.Wall, new Vector2Int(5, 3), DungeonEdgeDirection.Up);
        AddAnchor(corridor, DungeonStructuralAnchorType.Door, new Vector2Int(6, 3), DungeonEdgeDirection.Up);
        AddAnchor(corridor, DungeonStructuralAnchorType.Wall, new Vector2Int(7, 3), DungeonEdgeDirection.Up);
        // A door reserves both cells touching its edge for interaction. Keep
        // the mandatory trap one cell deeper in the passage, beyond the door.
        var roomData = new SerializedObject(corridor);
        var anchors = roomData.FindProperty("<SemanticAnchors>k__BackingField");
        for (var i = 0; i < anchors.arraySize; i++)
        {
            var anchor = anchors.GetArrayElementAtIndex(i);
            var cell = anchor.FindPropertyRelative("<LocalCell>k__BackingField");
            if (anchor.FindPropertyRelative("<Id>k__BackingField").stringValue == "trap.1"
                && cell.vector2IntValue == new Vector2Int(6, 4)) cell.vector2IntValue = new Vector2Int(6, 5);
        }
        roomData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        var root = PrefabUtility.LoadPrefabContents(DoorPrefab);
        try
        {
            var data = new SerializedObject(root.GetComponent<DungeonDoorView>());
            data.FindProperty("horizontalEdgeSprite").objectReferenceValue = Sprite("CatacombsDoorFront32-v3");
            data.FindProperty("verticalEdgeSprite").objectReferenceValue = Sprite("CatacombsDoorSide32-v2");
            data.FindProperty("preserveSpriteProportions").boolValue = true;
            data.FindProperty("projectedHeight").floatValue = 0;
            data.FindProperty("sideLengthRatio").floatValue = 56f / 32f;
            data.FindProperty("frontHeightRatio").floatValue = 24f / 32f;
            data.FindProperty("closedColor").colorValue = Color.white;
            data.FindProperty("openColor").colorValue = Color.white;
            data.ApplyModifiedPropertiesWithoutUndo();
            var renderer = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite("CatacombsDoorFront32-v3"); renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, DoorPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Validate();
    }

    private static DungeonRoomTemplateDefinition LoadRoom(string name) =>
        AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(Rooms + name + ".asset")
        ?? throw new InvalidOperationException("Missing room: " + name);
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

    private static void Import(string name, Vector2 pivot)
    {
        var path = Art + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot; importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }

    private static void AddAnchor(DungeonRoomTemplateDefinition room, DungeonStructuralAnchorType type,
        Vector2Int cell, DungeonEdgeDirection direction)
    {
        if (room.StructuralAnchors.Any(a => a.Type == type && a.LocalCell == cell && a.Direction == direction)) return;
        room.StructuralAnchors.Add(new DungeonStructuralAnchorData(type, cell, direction));
        EditorUtility.SetDirty(room);
    }

    public static void Validate()
    {
        var production = AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Expedition.asset");
        var definition = AssetDatabase.LoadAssetAtPath<DoorDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Door_Debug.asset");
        int doorChecks = 0, horizontal = 0, vertical = 0;
        var testRoot = new GameObject("Room doors verification");
        try
        {
            var grid = new GameObject("Grid", typeof(Grid)); grid.transform.SetParent(testRoot.transform);
            var tiles = new GameObject("Floor", typeof(Tilemap)); tiles.transform.SetParent(grid.transform);
            var party = new GameObject("Party", typeof(ExplorationPartyController)); party.transform.SetParent(testRoot.transform);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
            foreach (var name in RoomNames)
            foreach (var rotation in new[] { 0, 90, 180, 270 })
            {
                var template = LoadRoom(name);
                var floor = new DungeonFloorInstance(0);
                floor.TryAddRoom(new DungeonRoomInstance("room", template.RoomType, template.Id, rotationDegrees: rotation));
                var map = new DungeonFloorMapBuilder().Build(floor, production.RoomTemplateCatalog);
                floor.TrySetMap(map);
                foreach (var anchor in template.StructuralAnchors.Where(a => a.Type == DungeonStructuralAnchorType.Door))
                {
                    var first = DungeonRoomTransformUtility.TransformCell(anchor.LocalCell, template.SizeInCells, rotation);
                    var direction = DungeonRoomTransformUtility.TransformDirection(anchor.Direction, rotation);
                    var second = first + DungeonRoomTransformUtility.GetDirectionOffset(direction);
                    var door = new DoorDungeonObjectInstance("door." + doorChecks, definition, new DungeonCellEdge(first, second));
                    if (!map.CanMoveBetween(first, second) || !floor.TryAddObject(door) || map.CanMoveBetween(first, second))
                        throw new InvalidOperationException("Invalid closed door edge: " + name + " / " + rotation);
                    var instance = UnityEngine.Object.Instantiate(prefab, testRoot.transform);
                    var view = instance.GetComponent<DungeonDoorView>();
                    view.Initialize(party.GetComponent<ExplorationPartyController>(), floor, door, tiles.GetComponent<Tilemap>());
                    var renderer = instance.GetComponentInChildren<SpriteRenderer>();
                    var side = first.x != second.x;
                    if (side) vertical++; else horizontal++;
                    var depthView = instance.AddComponent<DungeonFloorMapView>();
                    view.BindElevationView(depthView);
                    var baseline = view.transform.position.y - (side ? .5f : 0);
                    var doorOrder = instance.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder;
                    if (depthView.GetActorDepthSortingOrder(baseline - 1f / 32f) <= doorOrder
                        || depthView.GetActorDepthSortingOrder(baseline + 1f / 32f) >= doorOrder)
                        throw new InvalidOperationException("Door does not interleave with actors across its ground baseline.");
                    if (renderer.sprite != Sprite(side ? "CatacombsDoorSide32-v2" : "CatacombsDoorFront32-v3")
                        || renderer.transform.localPosition != (side ? new Vector3(0, .375f, 0) : Vector3.zero)
                        || renderer.transform.localScale != new Vector3(1, side ? 1.75f : .75f, 1)
                        || Mathf.Abs(renderer.bounds.size.y - (side ? 1.75f : .75f)) > .0001f
                        || Mathf.Abs(renderer.bounds.min.y - (view.transform.position.y - (side ? .5f : 0))) > .0001f)
                        throw new InvalidOperationException("Wrong orientation sprite.");
                    floor.TrySetDoorOpen(door.InstanceId, true); view.RefreshState();
                    var openPosition = side ? new Vector3(.5f, -.5f, 0) : new Vector3(-.5f, .875f, 0);
                    var openSprite = Sprite(side ? "CatacombsDoorFront32-v3" : "CatacombsDoorSide32-v2");
                    if (!map.CanMoveBetween(first, second) || instance.GetComponent<BoxCollider2D>().enabled || renderer.color.a != 1
                        || renderer.sprite != openSprite || renderer.transform.localPosition != openPosition
                        || view.ProjectedVisualElevation != 0
                        || renderer.transform.localScale != new Vector3(1, side ? .75f : 1.75f, 1))
                        throw new InvalidOperationException("Open door still blocks movement.");
                    floor.TrySetDoorOpen(door.InstanceId, false); view.RefreshState();
                    if (!instance.GetComponent<BoxCollider2D>().enabled || renderer.transform.localPosition != (side ? new Vector3(0, .375f, 0) : Vector3.zero))
                        throw new InvalidOperationException("Reclosed door did not return to its threshold.");
                    floor.TrySetDoorOpen(door.InstanceId, true);
                    doorChecks++; UnityEngine.Object.DestroyImmediate(instance);
                }
                // After opening, partitions must not isolate any part of the authored room.
                var cells = map.Surfaces.Select(s => s.Cell).ToHashSet();
                var seen = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
                queue.Enqueue(cells.First()); seen.Add(queue.Peek());
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var step in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                        if (!seen.Contains(cell + step) && map.CanMoveBetween(cell, cell + step))
                        { seen.Add(cell + step); queue.Enqueue(cell + step); }
                }
                if (seen.Count != cells.Count) throw new InvalidOperationException("Disconnected room after opening doors: " + name);
            }
            int generatedDoors = 0;
            foreach (var seed in new[] { 230412, 4205, 100, 101, 102, 103, 104, 105 })
            {
                var dungeon = new DungeonGeneratorService().Generate(production, seed);
                new DungeonObjectPlacementSystem().Place(dungeon);
                var count = dungeon.Floors.Sum(f => f.Objects.OfType<DoorDungeonObjectInstance>().Count());
                generatedDoors += count;
                Debug.Log("CATACOMBS DOOR GENERATION seed=" + seed + " doors=" + count);
            }
            if (generatedDoors == 0 || horizontal == 0 || vertical == 0) throw new InvalidOperationException("Doors missing from production generation.");
            Debug.Log("CATACOMBS ROOM DOORS PASS: rotatedChecks=" + doorChecks + " horizontal=" + horizontal + " vertical=" + vertical + " generatedDoors=" + generatedDoors);
        }
        finally { UnityEngine.Object.DestroyImmediate(testRoot); }
    }

    public static void ApplyHeightAndOpenArt()
    {
        Configure();
        CatacombsInteractableArtSetup.Apply();
    }

    public static void ApplySideLength()
    {
        var root = PrefabUtility.LoadPrefabContents(DoorPrefab);
        try
        {
            var data = new SerializedObject(root.GetComponent<DungeonDoorView>());
            data.FindProperty("sideLengthRatio").floatValue = 56f / 32f;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DoorPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Validate();
    }
}
