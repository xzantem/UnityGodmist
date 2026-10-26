using System;
using System.IO;
using System.Linq;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CatacombsRoomDoorsReview
{
    private const string Flag = "CatacombsRoomDoorsReview";
    private static int stage, frames;
    private static double started;
    private static DungeonDoorView[] doors;
    private static Camera camera;
    private static DungeonSilhouetteLighting lighting;
    private static SpriteRenderer stairs;
    private static ExplorationCharacterView actor;
    private static Bounds wallContactBounds;
    private static string Output => SessionState.GetString(Flag + ".Output", "../output/asset-review/catacombs-door-depth-v1");

    public static void Smoke()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
        SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
    }

    public static void DepthSmoke()
    {
        SessionState.SetBool(Flag + ".DepthOnly", true);
        Smoke();
    }

    public static void SideLengthSmoke()
    {
        SessionState.SetString(Flag + ".Output", "../output/asset-review/catacombs-side-door-length-v2");
        DepthSmoke();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        stage = 0; frames = 0; started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Door review timeout.");
            var boot = GameBootstrap.Instance;
            if (boot == null || boot.IsSceneLoading) return;
            if (stage == 0)
            {
                if (!boot.TryStartNewGame(out var message)) throw new Exception(message);
                stage++; return;
            }
            if (stage == 1)
            {
                var definition = AssetDatabase.LoadAssetAtPath<Godmist.Data.Dungeons.DungeonDefinition>(
                    "Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Expedition.asset");
                if (!boot.TryEnterDungeon(definition, 104)) throw new Exception("Could not enter door review dungeon.");
                stage++; frames = 0; return;
            }
            if (SceneManager.GetActiveScene().name != "DungeonSandbox" || ++frames < 30) return;
            var scene = SceneManager.GetActiveScene();
            if (stage == 2)
            {
                boot.Session.ActiveDungeon.TryGetCurrentFloor(out var floor);
                if (!floor.Fog.DebugFogDisabled) floor.Fog.ToggleDebugFog();
                var views = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonDoorView>()).ToArray();
                doors = new[]
                {
                    views.First(v => v.GetComponent<BoxCollider2D>().size.x > .5f),
                    views.First(v => v.GetComponent<BoxCollider2D>().size.y > .5f)
                };
                lighting = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonSilhouetteLighting>()).Single();
                camera = (Camera)new SerializedObject(lighting).FindProperty("worldCamera").objectReferenceValue;
                foreach (var follow in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationCameraFollow>())) follow.enabled = false;
                if (SessionState.GetBool(Flag + ".DepthOnly", false))
                {
                    var party = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationPartyController>()).Single();
                    actor = party.ActiveMemberViews.First(v => v.CharacterId == "jerzy");
                    party.enabled = false;
                    stage = 9; frames = 0; PositionDepthActor(0, true); return;
                }
                stage++; frames = 0; Focus(doors[0]); return;
            }
            if (stage == 7)
            {
                var floorRenderer = (UnityEngine.Tilemaps.TilemapRenderer)new SerializedObject(lighting).FindProperty("floorReceiver").objectReferenceValue;
                var actorGroup = actor.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (stairs.sharedMaterial != floorRenderer.sharedMaterial
                    || stairs.sharedMaterial.shader.name != "Godmist/Dungeon/Silhouette Floor"
                    || stairs.sortingOrder != floorRenderer.sortingOrder + 1
                    || actorGroup == null || actorGroup.sortingOrder <= stairs.sortingOrder
                    || stairs.sharedMaterial.GetInt("_TorchCount") < 1
                    || stairs.sharedMaterial.GetFloat("_Ambient") >= .9f)
                    throw new Exception("Stairs must receive floor lighting and remain below the overlapping actor.");
                CaptureImage("stairs-actor-overlap");
                var pillarSprite = (Sprite)new SerializedObject(lighting).FindProperty("pillarContactSprite").objectReferenceValue;
                var pillar = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DungeonObstacleView>())
                    .SelectMany(v=>v.GetComponentsInChildren<SpriteRenderer>()).FirstOrDefault(r=>r.sprite == pillarSprite && !r.forceRenderingOff);
                var terrain = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DungeonFloorMapView>()).Single();
                var north = terrain.LightingWallRenderers.Where(r=>r != null && r.enabled && !r.forceRenderingOff
                    && r.GetComponent<DungeonWallSegmentView>()?.SurfaceProfile == DungeonWallSurfaceProfile.North)
                    .OrderBy(r=>(r.bounds.center-actor.transform.position).sqrMagnitude).First();
                var anchor = new Vector3(north.bounds.center.x,north.bounds.min.y-.5f,0);
                wallContactBounds = north.bounds;
                Debug.Log("WALL CONTACT DIAG bounds="+north.bounds+" baseline="+north.GetComponent<DungeonWallSegmentView>().DepthSortBaselineY);
                var destination = new[] { Vector3.right,Vector3.left,Vector3.down,Vector3.up }
                    .Select(d=>anchor+d).First(p=>terrain.LightingMap.IsWalkable(terrain.WorldToCell(p)));
                stairs.transform.parent.position = destination;
                camera.transform.position = new Vector3((anchor.x+destination.x)*.5f,(anchor.y+destination.y)*.5f,camera.transform.position.z);
                camera.orthographicSize = 1.5f;
                stage = 8; frames = 0; return;
            }
            if (stage == 8)
            {
                if (lighting.ContactShadowQuadCount == 0) throw new Exception("Missing pixel contact geometry.");
                if (lighting.WallContactShadowQuadCount == 0) throw new Exception("Missing wall contact geometry.");
                var contact = lighting.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterial.shader.name == "Godmist/Dungeon/Contact Shadows");
                var floorRenderer = (UnityEngine.Tilemaps.TilemapRenderer)new SerializedObject(lighting).FindProperty("floorReceiver").objectReferenceValue;
                if (contact.sortingOrder != floorRenderer.sortingOrder+1) throw new Exception("Contact layer must remain on floor.");
                var map = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DungeonFloorMapView>()).Single();
                var vertices = contact.GetComponent<MeshFilter>().sharedMesh.vertices;
                for (int i=0;i<vertices.Length;i+=4)
                {
                    var midpoint = (vertices[i]+vertices[i+2])*.5f;
                    if (!map.TryGetVisibleTraversal(midpoint,out var traversal) || !map.IsTraversalVisible(traversal))
                        throw new Exception("Contact mesh entered hidden floor or void.");
                }
                CaptureImage("contact-on"); contact.enabled = false; CaptureImage("contact-off"); contact.enabled = true;
                var before = new Texture2D(2,2); var after = new Texture2D(2,2);
                int darkened = 0, wallDarkened = 0;
                try
                {
                    before.LoadImage(File.ReadAllBytes(Output+"/contact-on.png")); after.LoadImage(File.ReadAllBytes(Output+"/contact-off.png"));
                    var a = before.GetPixels32(); var b = after.GetPixels32();
                    for (int i=0;i<a.Length;i++) if (a[i].r+a[i].g+a[i].b+2 < b[i].r+b[i].g+b[i].b) darkened++;
                    if (darkened < 5) throw new Exception("Contact mesh does not darken actual floor pixels.");
                    float halfWidth = camera.orthographicSize*960/540;
                    int minX = Mathf.Clamp(Mathf.CeilToInt(((wallContactBounds.min.x-camera.transform.position.x)/(2*halfWidth)+.5f)*960),0,959);
                    int maxX = Mathf.Clamp(Mathf.FloorToInt(((wallContactBounds.max.x-camera.transform.position.x)/(2*halfWidth)+.5f)*960),0,959);
                    int minY = Mathf.Clamp(Mathf.CeilToInt(((wallContactBounds.min.y-2f/32-camera.transform.position.y)/(2*camera.orthographicSize)+.5f)*540),0,539);
                    int maxY = Mathf.Clamp(Mathf.FloorToInt(((wallContactBounds.min.y-camera.transform.position.y)/(2*camera.orthographicSize)+.5f)*540),0,539);
                    for (int y=minY;y<=maxY;y++) for (int x=minX;x<=maxX;x++)
                    {
                        int i=y*960+x;
                        if (a[i].r+a[i].g+a[i].b+2 < b[i].r+b[i].g+b[i].b) wallDarkened++;
                    }
                    if (wallDarkened < 10) throw new Exception("Contact band below the selected wall is not visible in GPU render.");
                }
                finally { UnityEngine.Object.Destroy(before); UnityEngine.Object.Destroy(after); }
                Debug.Log("CONTACT SHADOW GPU PASS: quads="+lighting.ContactShadowQuadCount+" wallQuads="+lighting.WallContactShadowQuadCount+" darkenedPixels="+darkened+" wallBandPixels="+wallDarkened);
                var sourceStairs = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab").GetComponentInChildren<SpriteRenderer>();
                lighting.enabled = false;
                if (lighting.ContactShadowQuadCount != 0) throw new Exception("Contact resources did not release.");
                if (stairs.sharedMaterial != sourceStairs.sharedMaterial || stairs.sortingOrder != sourceStairs.sortingOrder
                    || stairs.sortingLayerID != sourceStairs.sortingLayerID)
                    throw new Exception("Ground presentation material/sorting did not restore when lighting was disabled.");
                Debug.Log("CATACOMBS GROUND PRESENTATION PASS: stairs use lit floor material, active torches and ambient; actor sorts above stairs.");
                Debug.Log("CATACOMBS DOOR LIVE RENDER PASS: horizontal and vertical closed/open doors visible, seed 104.");
                lighting.enabled = true;
                scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationPartyController>()).Single().enabled = false;
                stage = 9; frames = 0; PositionDepthActor(0, true); return;
            }
            if (stage >= 9)
            {
                var doorIndex = (stage - 9) / 2;
                var inFront = (stage - 9) % 2 == 0;
                var doorGroup = doors[doorIndex].GetComponent<UnityEngine.Rendering.SortingGroup>();
                var actorGroup = actor.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (doorGroup.sortingLayerID != actorGroup.sortingLayerID
                    || (inFront ? actorGroup.sortingOrder <= doorGroup.sortingOrder : actorGroup.sortingOrder >= doorGroup.sortingOrder))
                    throw new Exception("Door must sort between the foreground and background actor: stage=" + stage
                        + " actorY=" + actor.Position.y + " doorY=" + doors[doorIndex].transform.position.y
                        + " orders=" + actorGroup.sortingOrder + "/" + doorGroup.sortingOrder
                        + " layers=" + actorGroup.sortingLayerID + "/" + doorGroup.sortingLayerID);
                CaptureImage((doorIndex == 0 ? "horizontal" : "vertical") + (inFront ? "-actor-front" : "-actor-behind"));
                Debug.Log("DOOR DEPTH PASS orientation=" + doorIndex + " front=" + inFront + " actor=" + actorGroup.sortingOrder + " door=" + doorGroup.sortingOrder);
                if (++stage == 13) { Finish(0); return; }
                frames = 0; PositionDepthActor((stage - 9) / 2, (stage - 9) % 2 == 0); return;
            }
            var index = stage < 5 ? 0 : 1;
            var open = stage == 4 || stage == 6;
            Capture(doors[index], (index == 0 ? "horizontal-door-" : "vertical-door-") + (open ? "open" : "closed"), open);
            if (stage < 6)
            {
                boot.Session.ActiveDungeon.TryGetCurrentFloor(out var floor);
                var door = floor.Objects.OfType<Godmist.Runtime.Dungeons.DoorDungeonObjectInstance>()
                    .Single(d => d.InstanceId == doors[index].InstanceId);
                floor.TrySetDoorOpen(door.InstanceId, !open);
                stage++; frames = 0; if (stage == 5) Focus(doors[1]); return;
            }
            actor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ExplorationPartyController>()).Single()
                .ActiveMemberViews.First(v => v.BodyRenderer.enabled);
            stairs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonExitView>()).First().GetComponentInChildren<SpriteRenderer>();
            stairs.transform.parent.position = actor.transform.position;
            camera.transform.position = new Vector3(actor.transform.position.x, actor.transform.position.y, camera.transform.position.z);
            stage = 7; frames = 0;
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static void Focus(DungeonDoorView door)
    {
        camera.transform.position = new Vector3(door.transform.position.x, door.transform.position.y, camera.transform.position.z);
        camera.orthographicSize = 2.5f;
    }

    private static void PositionDepthActor(int index, bool inFront)
    {
        var door = doors[index];
        var boot = GameBootstrap.Instance;
        boot.Session.ActiveDungeon.TryGetCurrentFloor(out var floor);
        floor.TrySetDoorOpen(door.InstanceId, false);
        door.RefreshState();
        var side = door.GetComponent<BoxCollider2D>().size.y > .5f;
        var baseline = door.transform.position.y - (side ? .5f : 0);
        // This is a render-order probe at sub-collider distances. Keep physics
        // from pushing the foreground probe through the closed threshold.
        actor.SetNoclip(true);
        actor.SetPosition(new Vector2(door.transform.position.x, baseline + (inFront ? -.125f : .125f)));
        Focus(door);
    }

    private static void Capture(DungeonDoorView door, string name, bool open)
    {
        var renderer = door.GetComponentInChildren<SpriteRenderer>();
        if (!renderer.enabled || renderer.forceRenderingOff || renderer.color.a < .9f || door.GetComponent<BoxCollider2D>().enabled == open)
            throw new Exception("Closed door is hidden in gameplay: " + name);
        CaptureImage(name);
    }

    private static void CaptureImage(string name)
    {
        var target = new RenderTexture(960, 540, 24); target.Create();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var texture = new Texture2D(960, 540, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
            Directory.CreateDirectory(Output);
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Flag + ".DepthOnly", false);
        SessionState.EraseString(Flag + ".Output");
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; EditorApplication.Exit(code);
    }
}
