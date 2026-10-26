using System;
using System.IO;
using System.Linq;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports the reviewed Arungard presentation prefab and authors its explicit
/// references into the existing city scene. This is an Editor-only bridge; the
/// runtime presentation components themselves contain no scene discovery.
/// </summary>
public static class ArungardIntegrationAuthoring
{
    const string ScenePath = "Assets/_Project/Scenes/City/Arungard.unity";
    const string PrefabPath = "Assets/_Project/Prefabs/City/PF_ArungardPresentationV10.prefab";
    const string InstalledName = "ArungardPresentation_V10";
    const string PackagePath = "output/integration-stage/arungard-presentation-v10.unitypackage";
    const string PendingKey = "ArungardIntegration.PackageImportPending";
    static double pollStartedAt;

    [MenuItem("Godmist/Content/Characters/Connect NPC Workstations")]
    public static void ConnectNpcWorkstations()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Connect workstations outside Play Mode.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var blacksmith = roots.SelectMany(root => root.GetComponentsInChildren<NpcView>(true))
            .Single(view => view.name == "Blacksmith");
        var anvil = roots.SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
            .Single(renderer => renderer.name == "Anvil");
        var data = new SerializedObject(blacksmith);
        data.FindProperty("workstationRenderer").objectReferenceValue = anvil;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(blacksmith);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("NPC_WORKSTATION_CONNECTED: Blacksmith -> existing Anvil. No duplicate prop; authored collider retained.");
    }

    [MenuItem("Godmist/Content/UI/Configure Expedition Gate")]
    public static void ConfigureExpeditionGate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Configure the gate outside Play Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open Assets/_Project/Scenes/City/Arungard.unity first.");
        var entrance = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<DungeonEntranceView>(true))
            .Single(value => value.name == "CatacombsEntrance");
        var placeholder = entrance.GetComponent<SpriteRenderer>();
        var label = entrance.transform.Find("ExpeditionsLabel");
        var collider = entrance.GetComponent<BoxCollider2D>();
        if (placeholder == null || label == null || collider == null)
            throw new InvalidOperationException("The existing expedition gate presentation is incomplete.");
        Undo.RecordObjects(new UnityEngine.Object[] { entrance, placeholder, collider, label.gameObject }, "Configure expedition gate");
        var arrow = entrance.transform.Find("ExitArrow");
        if (arrow == null)
        {
            var child = new GameObject("ExitArrow", typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(child, "Create expedition arrow");
            arrow = child.transform;
            arrow.SetParent(entrance.transform, false);
        }
        Undo.RecordObjects(new UnityEngine.Object[] { arrow, arrow.GetComponent<SpriteRenderer>() }, "Configure expedition arrow");
        // Compensate the legacy entrance's 2x horizontal scale; marker stays 32 PPU.
        arrow.localPosition = new Vector3(0f, -1.25f, -.1f);
        arrow.localRotation = Quaternion.identity;
        arrow.localScale = new Vector3(.5f, 1f, 1f);
        var renderer = arrow.GetComponent<SpriteRenderer>();
        renderer.color = Color.white;
        renderer.sortingLayerID = placeholder.sortingLayerID;
        renderer.sortingOrder = 100;
        placeholder.enabled = false;
        label.gameObject.SetActive(false);
        collider.enabled = true;
        collider.isTrigger = true;
        collider.offset = new Vector2(0f, -1.25f);
        collider.size = new Vector2(.375f, 1f);
        var data = new SerializedObject(entrance);
        data.FindProperty("useGateExit").boolValue = true;
        data.FindProperty("gateExitOffset").vector2Value = new Vector2(0f, 1.3f);
        data.FindProperty("gateHalfWidth").floatValue = 1.1f;
        data.FindProperty("gateReturnMargin").floatValue = .5f;
        data.FindProperty("gateArrow").objectReferenceValue = renderer;
        data.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = entrance.gameObject;
        Debug.Log("Expedition gate configured. Save Arungard. The transparent arrow is generated in Play Mode; exit line: " + entrance.GateExitPosition);
    }

    [InitializeOnLoadMethod]
    static void ResumePackageImport()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        pollStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= PollPackageImport;
        EditorApplication.update += PollPackageImport;
    }

    public static void ImportPresentationPackage()
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var repositoryRoot = Directory.GetParent(projectRoot).FullName;
        var absolutePackagePath = Path.GetFullPath(Path.Combine(repositoryRoot, PackagePath));
        if (!File.Exists(absolutePackagePath))
            throw new FileNotFoundException("Arungard presentation package is missing", absolutePackagePath);

        SessionState.SetBool(PendingKey, true);
        pollStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= PollPackageImport;
        EditorApplication.update += PollPackageImport;
        AssetDatabase.ImportPackage(absolutePackagePath, false);
        Debug.Log("ARUNGARD_IMPORT_STARTED package=" + absolutePackagePath + " prefab=" + PrefabPath);
    }

    static void PollPackageImport()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update -= PollPackageImport;
            Debug.Log("ARUNGARD_IMPORT_COMPLETED prefab=" + PrefabPath);
            EditorApplication.Exit(0);
            return;
        }

        if (EditorApplication.timeSinceStartup - pollStartedAt > 180)
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update -= PollPackageImport;
            Debug.LogError("ARUNGARD_IMPORT_TIMEOUT prefab=" + PrefabPath);
            EditorApplication.Exit(1);
        }
    }

    public static void InstallPresentation()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Missing imported presentation prefab: " + PrefabPath);

        var missingPrefabScripts = prefab.GetComponentsInChildren<MonoBehaviour>(true).Count(component => component == null);
        var prefabRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer => renderer.sprite != null);
        if (missingPrefabScripts != 0 || prefabRenderers == 0)
            throw new InvalidOperationException($"Presentation prefab dependencies invalid: missingScripts={missingPrefabScripts} sprites={prefabRenderers}");

        var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == InstalledName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
            throw new InvalidOperationException("Could not instantiate " + PrefabPath);

        instance.name = InstalledName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        // The reviewed presentation owns the city art and collision layout.
        // Keep the existing World service objects (NPCs, gate, camera bounds)
        // intact while retiring only the legacy floor/wall presentation that
        // would otherwise render and collide underneath the imported layout.
        DisableLegacyCityPresentation(scene);
        AuthorGameplayLayout(scene, instance);

        var party = FindParty(scene);
        // The party's ActiveMemberViews collection is populated during gameplay
        // bootstrap. Author the stable scene character view references directly
        // so presentation depth/light bindings survive that lifecycle boundary.
        var views = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ExplorationCharacterView>(true))
            .Where(view => view != null)
            .Distinct()
            .ToArray();

        foreach (var roof in instance.GetComponentsInChildren<ArungardBuildingRoofPreview>(true))
            roof.party = party;
        foreach (var roof in instance.GetComponentsInChildren<ArungardForgeRoofPreview>(true))
            roof.party = party;

        foreach (var depth in instance.GetComponentsInChildren<ArungardCityDepthPreview>(true))
            depth.actors = views.Select(view => new ArungardCityDepthPreview.Entry
            {
                feet = view.transform,
                group = EnsureWorldDepthGroup(view)
            }).Where(entry => entry.group != null).ToArray();

        foreach (var daylight in instance.GetComponentsInChildren<ArungardDaylightPreview>(true))
            daylight.actors = views.Select(view => new ArungardDaylightPreview.Actor
            {
                source = view.BodyRenderer,
                slope = 0,
                offset = 0
            }).Where(actor => actor.source != null).ToArray();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"ARUNGARD_INSTALL scene={ScenePath} root={InstalledName} objects={instance.GetComponentsInChildren<Transform>(true).Length} colliders={instance.GetComponentsInChildren<Collider2D>(true).Length} partyViews={views.Length} depthActors={instance.GetComponentsInChildren<ArungardCityDepthPreview>(true).Sum(depth => depth.actors == null ? 0 : depth.actors.Length)} daylightActors={instance.GetComponentsInChildren<ArungardDaylightPreview>(true).Sum(daylight => daylight.actors == null ? 0 : daylight.actors.Length)} legacyPresentationDisabled={LegacyPresentationDisabled(scene)} layout={GameplayLayoutValid(scene, instance)} roofPreviews={instance.GetComponentsInChildren<ArungardBuildingRoofPreview>(true).Length} daylight={instance.GetComponentsInChildren<ArungardDaylightPreview>(true).Length}");
    }

    public static void ValidatePresentation()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == InstalledName);
        if (root == null) throw new InvalidOperationException("Installed Arungard presentation root is missing");

        var missingScripts = root.GetComponentsInChildren<MonoBehaviour>(true).Count(component => component == null);
        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer => renderer.sprite != null);
        var colliders = root.GetComponentsInChildren<Collider2D>(true).Count(collider => collider.enabled && !collider.isTrigger);
        var roofs = root.GetComponentsInChildren<ArungardBuildingRoofPreview>(true);
        var forgeRoofs = root.GetComponentsInChildren<ArungardForgeRoofPreview>(true);
        var depthActors = root.GetComponentsInChildren<ArungardCityDepthPreview>(true).Sum(depth => depth.actors == null ? 0 : depth.actors.Length);
        var daylightActors = root.GetComponentsInChildren<ArungardDaylightPreview>(true).Sum(daylight => daylight.actors == null ? 0 : daylight.actors.Length);
        var party = FindParty(scene);
        var wiredRoofs = roofs.Count(roof => roof.party == party && roof.party != null);
        var wiredForgeRoofs = forgeRoofs.Count(roof => roof.party == party && roof.party != null);
        var layoutValid = GameplayLayoutValid(scene, root);
        Debug.Log($"ARUNGARD_VALIDATE root={root.name} objects={root.GetComponentsInChildren<Transform>(true).Length} sprites={renderers} solidColliders={colliders} missingScripts={missingScripts} depthActors={depthActors} daylightActors={daylightActors} roofs={roofs.Length}/{wiredRoofs} forgeRoofs={forgeRoofs.Length}/{wiredForgeRoofs} legacyPresentationDisabled={LegacyPresentationDisabled(scene)} layout={layoutValid} party={(party != null)}");
        if (missingScripts != 0 || renderers == 0 || depthActors == 0 || daylightActors == 0 || wiredRoofs != roofs.Length || wiredForgeRoofs != forgeRoofs.Length || !LegacyPresentationDisabled(scene) || !layoutValid)
            throw new InvalidOperationException("Arungard presentation validation failed");
    }

    static void AuthorGameplayLayout(Scene scene, GameObject presentation)
    {
        var npcPositions = new System.Collections.Generic.Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["QuestGiver"] = new Vector3(12f, 20f, 0f),
            ["Blacksmith"] = new Vector3(6f, 11.5f, 0f),
            ["Alchemist"] = new Vector3(6f, 2.5f, 0f),
            ["Enchanter"] = new Vector3(27f, 3.5f, 0f),
            ["Priest"] = new Vector3(27f, 12.5f, 0f)
        };
        foreach (var npc in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcView>(true)))
            if (npcPositions.TryGetValue(npc.name, out var position))
                npc.transform.position = position;

        var entrance = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DungeonEntranceView>(true)).FirstOrDefault(entrance => entrance.name == "CatacombsEntrance");
        if (entrance != null)
            entrance.transform.position = new Vector3(16f, 24.7f, 0f);

        var characterPositions = new System.Collections.Generic.Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["Jerzy"] = new Vector3(16f, 10f, 0f),
            ["Wladimir"] = new Vector3(15.3f, 10f, 0f),
            ["StartingCharacter_Mage"] = new Vector3(14.6f, 10f, 0f),
            ["StartingCharacter_Paladin"] = new Vector3(13.9f, 10f, 0f)
        };
        foreach (var view in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ExplorationCharacterView>(true)))
            if (characterPositions.TryGetValue(view.name, out var position))
                view.transform.position = position;

        var camera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
        if (camera != null)
        {
            camera.transform.position = new Vector3(16f, 10f, camera.transform.position.z);
            camera.orthographicSize = 5f;
            var follow = camera.GetComponent<ExplorationCameraFollow>();
            var bounds = presentation.transform.Find("GrayboxCameraBounds")?.GetComponent<BoxCollider2D>();
            if (follow != null && bounds != null)
            {
                var serialized = new SerializedObject(follow);
                var property = serialized.FindProperty("cameraBounds");
                if (property == null) throw new InvalidOperationException("ExplorationCameraFollow.cameraBounds field is missing");
                property.objectReferenceValue = bounds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(follow);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"ARUNGARD_LAYOUT npcPositions={npcPositions.Count} entrance={(entrance == null ? "missing" : entrance.transform.position.ToString())} camera={(camera == null ? "missing" : camera.transform.position.ToString())} cameraBounds={(presentation.transform.Find("GrayboxCameraBounds") != null)}");
    }

    static bool GameplayLayoutValid(Scene scene, GameObject presentation)
    {
        var expectedNpcs = new System.Collections.Generic.Dictionary<string, Vector2>(StringComparer.Ordinal)
        {
            ["QuestGiver"] = new Vector2(12f, 20f),
            ["Blacksmith"] = new Vector2(6f, 11.5f),
            ["Alchemist"] = new Vector2(6f, 2.5f),
            ["Enchanter"] = new Vector2(27f, 3.5f),
            ["Priest"] = new Vector2(27f, 12.5f)
        };
        foreach (var npc in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcView>(true)))
            if (expectedNpcs.TryGetValue(npc.name, out var expected) && Vector2.Distance(npc.transform.position, expected) > .01f)
                return false;
        var entrance = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DungeonEntranceView>(true)).FirstOrDefault(value => value.name == "CatacombsEntrance");
        if (entrance == null || Vector2.Distance(entrance.transform.position, new Vector2(16f, 24.7f)) > .01f) return false;
        var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault(value => value.CompareTag("MainCamera"));
        var bounds = presentation.transform.Find("GrayboxCameraBounds")?.GetComponent<BoxCollider2D>();
        if (camera == null || bounds == null || Mathf.Abs(camera.orthographicSize - 5f) > .01f) return false;
        var follow = camera.GetComponent<ExplorationCameraFollow>();
        if (follow == null) return false;
        var followData = new SerializedObject(follow);
        return followData.FindProperty("cameraBounds")?.objectReferenceValue == bounds;
    }

    static void DisableLegacyCityPresentation(Scene scene)
    {
        var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "World");
        if (world == null) return;
        foreach (var child in world.transform.Cast<Transform>())
            if (child.name == "Walls" || child.name == "Ground")
            {
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
    }

    static bool LegacyPresentationDisabled(Scene scene)
    {
        var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "World");
        if (world == null) return false;
        return world.transform.Cast<Transform>()
            .Where(child => child.name == "Walls" || child.name == "Ground")
            .All(child => !child.gameObject.activeSelf);
    }

    static SortingGroup EnsureWorldDepthGroup(ExplorationCharacterView view)
    {
        var group = view.GetComponent<SortingGroup>();
        if (group == null)
        {
            group = view.gameObject.AddComponent<SortingGroup>();
            EditorUtility.SetDirty(view.gameObject);
        }
        return group;
    }

    static ExplorationPartyController FindParty(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var party = root.GetComponentInChildren<ExplorationPartyController>(true);
            if (party != null) return party;
        }
        return null;
    }
}
