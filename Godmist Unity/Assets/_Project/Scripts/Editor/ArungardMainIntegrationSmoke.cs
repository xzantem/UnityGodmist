using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.NPC;
using Godmist.Views.UI.Workspace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authentic runtime smoke for the promoted Arungard scene. This is an
/// Editor-only verification runner: it does not participate in game runtime
/// wiring and does not capture through Camera.Render (which is unstable in
/// headless Unity).
/// </summary>
public static class ArungardMainIntegrationSmoke
{
    const string Flag = "ArungardMainIntegrationSmoke";
    const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity";
    const string SceneName = "Arungard";
    const string PresentationRoot = "ArungardPresentation_V10";
    static readonly MethodInfo SetCityDestination = typeof(ExplorationPartyController)
        .GetMethod("TrySetCityDestination", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly string[] NpcVisitOrder = { "Blacksmith", "Alchemist", "Priest", "Enchanter", "QuestGiver" };
    static double started;
    static int stage;
    static int frames;
    static float routeStarted;
    static ExplorationPartyController party;
    static ExplorationSandboxBootstrap sandbox;
    static DungeonEntranceView entrance;
    static DungeonSelectorWindowView selector;
    static Vector2 routeTarget;
    static string routeName;
    static ArungardBuildingRoofPreview roofUnderTest;
    static int npcIndex;

    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before starting the Arungard main smoke.");

        EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        started = EditorApplication.timeSinceStartup;
        stage = 0;
        frames = 0;
        npcIndex = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180)
                throw new InvalidOperationException("Arungard main smoke timed out at stage " + stage);

            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null || bootstrap.IsSceneLoading) return;

            if (stage == 0)
            {
                if (!bootstrap.TryStartNewGame(out var message))
                    throw new InvalidOperationException(message);
                stage = 1;
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneName || ++frames < 30) return;
            var scene = SceneManager.GetActiveScene();

            if (stage == 1)
            {
                party = Components<ExplorationPartyController>(scene).Single();
                sandbox = Components<ExplorationSandboxBootstrap>(scene).Single();
                entrance = Components<DungeonEntranceView>(scene).Single(value => value.name == "CatacombsEntrance");
                ValidateRuntimePresentation(scene);
                ValidateRuntimeCameraClamp(scene);
                ValidateServicesAndOffers(bootstrap);
                if (party.ActiveMemberViews.Count != 4)
                    throw new InvalidOperationException("Expected four active party views, got " + party.ActiveMemberViews.Count);
                var actualSpawn = (Vector2)party.ActiveLeaderTransform.position;
                if (Vector2.Distance(actualSpawn, new Vector2(16f, 10f)) > .1f)
                    throw new InvalidOperationException("New-game city spawn is not the authored central spawn: " + actualSpawn);
                foreach (var view in party.ActiveMemberViews)
                    if (!view.CanStandAt(view.Position))
                        throw new InvalidOperationException("City spawn overlaps a solid collider: " + view.CharacterId);
                ValidateRuntimeStash(scene);

                BeginNpcVisit(0);
                routeStarted = Time.time;
                stage = 2;
                return;
            }

            if (stage == 2)
            {
                if (Time.time - routeStarted > 30f)
                {
                    var stalledBinding = sandbox.NpcBindings.Single(value => value.NpcView != null && value.NpcView.name == NpcVisitOrder[npcIndex]);
                    var stalledLeader = party.ActiveLeaderTransform;
                    var stalledView = stalledLeader.GetComponent<ExplorationCharacterView>();
                    var blockers = Physics2D.OverlapCircleAll((Vector2)stalledLeader.position + stalledView.MovementColliderCenterOffset, stalledView.MovementRadius)
                        .Where(collider => collider != null && !collider.isTrigger && collider.GetComponentInParent<ExplorationCharacterView>() == null)
                        .Select(collider => collider.name + "@" + collider.bounds)
                        .ToArray();
                    throw new InvalidOperationException($"NPC approach stalled: {NpcVisitOrder[npcIndex]} leader={stalledLeader.position} target={stalledBinding.NpcView.transform.position} radius={stalledBinding.NpcView.InteractionRadius} npcId={stalledBinding.NpcView.NpcId} mode={GameBootstrap.Instance.Session.CurrentMode} canInteract={stalledBinding.NpcView.CanInteract()} clearTarget={stalledView.HasClearMovementSegment(stalledLeader.position, stalledBinding.NpcView.transform.position)} blockers=[{string.Join(";", blockers)}]");
                }
                var binding = sandbox.NpcBindings.Single(value => value.NpcView != null && value.NpcView.name == NpcVisitOrder[npcIndex]);
                ValidateIntermediateParty();
                if (bootstrap.DialogueService.ActiveDialogue == null) return;
                // Interact() transitions the session to Dialogue, so
                // NpcView.CanInteract() is intentionally false by this point.
                // Validate the reached perimeter by distance instead.
                if (Vector2.Distance(party.ActiveLeaderTransform.position, binding.NpcView.transform.position) > binding.NpcView.InteractionRadius + .05f)
                    throw new InvalidOperationException("Dialogue opened outside the NPC interaction radius: " + binding.NpcView.name);
                if (!FollowersAreClear()) return;
                var propertyName = binding.NpcView.name == "QuestGiver" ? "dialogue-missions-choice" : "dialogue-service-choice";
                var dialogueController = Components<NpcDialogueController>(scene).Single();
                var dialogueData = new SerializedObject(dialogueController);
                var choices = dialogueData.FindProperty("choicesContent").objectReferenceValue as Transform;
                var button = choices != null && choices.childCount > 0 ? choices.GetChild(0).GetComponent<UnityEngine.UI.Button>() : null;
                if (button == null || !button.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Interaction UI unavailable for " + binding.NpcView.name);
                var expectedAnswer = binding.NpcView.name == "QuestGiver" ? "Masz dla mnie jakieś zlecenia?" : "Chcę skorzystać z twoich usług";
                if (button.GetComponentInChildren<TMPro.TMP_Text>().text != expectedAnswer)
                    throw new InvalidOperationException("First dialogue action is incorrect for " + binding.NpcView.name);
                if (binding.NpcView.NpcId != binding.Definition.Id || !bootstrap.Session.NpcsById.ContainsKey(binding.Definition.Id))
                    throw new InvalidOperationException("NPC identity binding changed for " + binding.NpcView.name);
                Debug.Log($"ARUNGARD_MAIN_NPC_PASS npc={binding.NpcView.name} id={binding.NpcView.NpcId} seconds={(Time.time - routeStarted):F2} ui={propertyName}");
                bootstrap.DialogueService.TryEndDialogue();
                party.CancelMovement();
                npcIndex++;
                if (npcIndex < NpcVisitOrder.Length)
                {
                    BeginNpcVisit(npcIndex);
                    return;
                }
                // Keep each independent service approach reproducible from the
                // authored plaza spawn, matching the existing city smoke setup;
                // the new-game spawn itself was validated before this reset.
                party.CancelMovement();
                party.RestorePartyFormation(new Vector2(16f, 10f));
                Physics2D.SyncTransforms();
                if (!party.RequestEntranceInteraction(entrance))
                    throw new InvalidOperationException("Could not route the party to the north gate.");
                routeStarted = Time.time;
                stage = 3;
                return;
            }

            if (stage == 3)
            {
                if (Time.time - routeStarted > 30f)
                    throw new InvalidOperationException("North gate approach stalled.");
                selector = Components<DungeonSelectorWindowView>(scene).Single();
                if (!selector.IsOpen) return;
                if (bootstrap.Session.DungeonOffers.Offers.Count != 6)
                    throw new InvalidOperationException("Expected six expedition offers, got " + bootstrap.Session.DungeonOffers.Offers.Count);
                Debug.Log("ARUNGARD_MAIN_GATE_PASS offers=6 seconds=" + (Time.time - routeStarted).ToString("F2"));
                CloseSelector(selector);
                party.CancelMovement();
                BeginRoute(new Vector2(4f, 11f), new Vector2(4f, 19f), "forge");
                stage = 4;
                return;
            }

            if (stage == 4)
            {
                if (!RouteReached()) return;
                Debug.Log("ARUNGARD_MAIN_FORGE_ROUTE_PASS seconds=" + (Time.time - routeStarted).ToString("F2"));
                party.CancelMovement();
                BeginRoute(new Vector2(12f, 12f), new Vector2(12f, 18.7f), "well");
                stage = 5;
                return;
            }

            if (stage == 5)
            {
                if (!RouteReached()) return;
                Debug.Log("ARUNGARD_MAIN_WELL_ROUTE_PASS seconds=" + (Time.time - routeStarted).ToString("F2"));
                party.CancelMovement();
                BeginRoofRevealCheck();
                stage = 6;
                return;
            }

            if (stage == 6)
            {
                if (roofUnderTest == null || !roofUnderTest.IsOccluding) return;
                if (!party.TryTeleport(new Vector2(16f, 10f)))
                    throw new InvalidOperationException("Could not leave roof reveal test position.");
                Physics2D.SyncTransforms();
                stage = 7;
                return;
            }

            if (stage == 7)
            {
                if (roofUnderTest != null && roofUnderTest.IsOccluding) return;
                var daylight = Components<ArungardDaylightPreview>(scene).Single();
                if (daylight.LastActorCount < 4)
                    throw new InvalidOperationException("Daylight runtime did not process all actor bindings: " + daylight.LastActorCount);
                var depth = Components<ArungardCityDepthPreview>(scene).Single();
                if (depth.actors == null || depth.actors.Length < 4
                    || depth.actors.Any(entry => entry.feet == null || entry.group == null
                        || entry.group.sortingOrder != ArungardCityDepthPreview.Order(entry.feet.position.y)))
                    throw new InvalidOperationException("Runtime city depth sorting did not refresh all actor groups.");
                Debug.Log($"ARUNGARD_MAIN_PRESENTATION_RUNTIME_PASS roofRecovered={roofUnderTest?.name} daylightActors={daylight.LastActorCount} activeParty={party.ActiveMemberViews.Count} depthActors={depth.actors.Length}");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    static void ValidateRuntimePresentation(Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(value => value.name == PresentationRoot);
        if (root == null) throw new InvalidOperationException("Promoted presentation root is missing at runtime.");
        if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(value => value == null))
            throw new InvalidOperationException("Promoted presentation has a missing runtime script.");
        if (root.GetComponentsInChildren<SpriteRenderer>(true).Count(value => value.sprite != null) < 300)
            throw new InvalidOperationException("Promoted presentation sprite set is incomplete at runtime.");
    }

    static void ValidateServicesAndOffers(GameBootstrap bootstrap)
    {
        if (sandbox.NpcBindings.Count != 5)
            throw new InvalidOperationException("Expected five Arungard service/NPC bindings, got " + sandbox.NpcBindings.Count);
        if (bootstrap.Session.NpcsById.Count < 5)
            throw new InvalidOperationException("Expected five runtime Arungard NPC instances, got " + bootstrap.Session.NpcsById.Count);
        if (bootstrap.Session.DungeonOffers.Offers.Count != 6)
            throw new InvalidOperationException("Expected six expedition offers after campaign bootstrap, got " + bootstrap.Session.DungeonOffers.Offers.Count);
        Debug.Log("ARUNGARD_MAIN_SERVICES_PASS services=5 runtimeNpcs=" + bootstrap.Session.NpcsById.Count + " offers=6");
    }

    static void ValidateRuntimeCameraClamp(Scene scene)
    {
        var camera = Components<Camera>(scene).Single(value => value.CompareTag("MainCamera"));
        var follow = camera.GetComponent<ExplorationCameraFollow>();
        var bounds = Components<BoxCollider2D>(scene).Single(value => value.gameObject.name == "GrayboxCameraBounds");
        if (follow == null || bounds == null)
            throw new InvalidOperationException("Runtime camera clamp fixture is incomplete.");

        var originalPosition = camera.transform.position;
        var originalAspect = camera.aspect;
        var originalZoom = camera.orthographicSize;
        var tested = 0;
        try
        {
            foreach (var aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f })
            foreach (var zoom in new[] { 2.5f, 5f, 10f })
            {
                camera.aspect = aspect;
                camera.orthographicSize = zoom;
                var worldBounds = bounds.bounds;
                var halfWidth = zoom * aspect;
                var minX = worldBounds.min.x + halfWidth;
                var maxX = worldBounds.max.x - halfWidth;
                var minY = worldBounds.min.y + zoom;
                var maxY = worldBounds.max.y - zoom;
                foreach (var target in new[]
                {
                    new Vector3(worldBounds.min.x, worldBounds.min.y, 0f),
                    new Vector3(worldBounds.min.x, worldBounds.max.y, 0f),
                    new Vector3(worldBounds.max.x, worldBounds.min.y, 0f),
                    new Vector3(worldBounds.max.x, worldBounds.max.y, 0f)
                })
                {
                    follow.CenterOn(target);
                    var actual = camera.transform.position;
                    var expectedX = minX > maxX ? worldBounds.center.x : Mathf.Clamp(target.x, minX, maxX);
                    var expectedY = minY > maxY ? worldBounds.center.y : Mathf.Clamp(target.y, minY, maxY);
                    if (Mathf.Abs(actual.x - expectedX) > .01f || Mathf.Abs(actual.y - expectedY) > .01f)
                        throw new InvalidOperationException($"Camera clamp drifted at aspect={aspect:F4} zoom={zoom:F2} target={target} actual={actual} expected=({expectedX:F3},{expectedY:F3})");
                    tested++;
                }
            }
        }
        finally
        {
            camera.aspect = originalAspect;
            camera.orthographicSize = originalZoom;
            follow.CenterOn(originalPosition, true);
        }

        Debug.Log($"ARUNGARD_MAIN_CAMERA_CLAMP_PASS corners={tested} bounds={bounds.bounds}");
    }

    static void ValidateRuntimeStash(Scene scene)
    {
        var chest = Components<CampaignStorageChestView>(scene).Single();
        var leader = party.ActiveLeaderTransform;
        var approach = chest.transform.position + Vector3.down * 1.1f;
        if (!party.TryTeleport(approach))
            throw new InvalidOperationException("Could not place party at the authored campaign stash approach point.");
        Physics2D.SyncTransforms();
        if (!chest.CanInteract())
            throw new InvalidOperationException($"Campaign storage chest is not interactable from its approach point: leader={leader.position} chest={chest.transform.position}");

        chest.Interact();
        var window = CampaignStorageWindowView.Active;
        var chrome = window?.GetComponent<UIWorkspaceWindow>();
        if (window == null || chrome == null || !chrome.IsOpen)
            throw new InvalidOperationException("Campaign storage chest did not open the shared storage window.");
        chrome.Close();
        if (!party.TryTeleport(new Vector2(16f, 10f)))
            throw new InvalidOperationException("Could not restore the authored city smoke spawn after stash validation.");
        Physics2D.SyncTransforms();
        Debug.Log($"ARUNGARD_MAIN_STASH_PASS position={chest.transform.position} approach={approach} sharedWindow={window.name}");
    }

    static void BeginNpcVisit(int index)
    {
        var binding = sandbox.NpcBindings.Single(value => value.NpcView != null && value.NpcView.name == NpcVisitOrder[index]);
        if (binding.Definition == null || binding.NpcView == null)
            throw new InvalidOperationException("Missing authored NPC binding: " + NpcVisitOrder[index]);
        if (binding.NpcView.NpcId != binding.Definition.Id)
            throw new InvalidOperationException("NPC identity is not bound before approach: " + binding.NpcView.name);
        party.CancelMovement();
        party.RestorePartyFormation(new Vector2(16f, 10f));
        Physics2D.SyncTransforms();
        if (!party.RequestNpcInteraction(binding.NpcView))
            throw new InvalidOperationException("Could not route the party to " + binding.NpcView.name);
        routeStarted = Time.time;
    }

    static void BeginRoute(Vector2 start, Vector2 goal, string name)
    {
        if (SetCityDestination == null)
            throw new InvalidOperationException("City route method is unavailable to the smoke runner.");
        if (!party.TryTeleport(start))
            throw new InvalidOperationException("Could not place party at " + name + " route start.");
        Physics2D.SyncTransforms();
        var accepted = SetCityDestination.Invoke(party, new object[] { goal });
        if (!(accepted is bool ok) || !ok)
            throw new InvalidOperationException("City route planner rejected " + name + " route.");
        routeName = name;
        routeTarget = goal;
        routeStarted = Time.time;
    }

    static bool RouteReached()
    {
        if (Time.time - routeStarted > 30f)
            throw new InvalidOperationException(routeName + " route stalled at " + party.ActiveLeaderTransform.position + " target=" + routeTarget);
        ValidateIntermediateParty();
        return Vector2.Distance(party.ActiveLeaderTransform.position, routeTarget) <= .15f
            && FollowersAreClear();
    }

    static void ValidateIntermediateParty()
    {
        var leader = party.ActiveLeaderTransform?.GetComponent<ExplorationCharacterView>();
        if (leader == null) throw new InvalidOperationException("Party leader view disappeared during runtime route.");
        if (leader.CanStandAt(leader.Position)) return;
        var center = (Vector2)leader.Position + leader.MovementColliderCenterOffset;
        var actualCollider = new SerializedObject(leader)
            .FindProperty("movementCollider")?.objectReferenceValue as Collider2D;
        var beyondContactTolerance = false;
        var hits = Physics2D.OverlapCircleAll(center, leader.MovementRadius)
            .Where(collider => collider != null && !collider.isTrigger
                && collider.GetComponentInParent<ExplorationCharacterView>() == null)
            .Select(collider =>
            {
                var distance = actualCollider != null ? actualCollider.Distance(collider) : default;
                var separation = distance.isValid
                    ? distance.distance
                    : Vector2.Distance(center, collider.ClosestPoint(center)) - leader.MovementRadius;
                if (separation < -Physics2D.defaultContactOffset) beyondContactTolerance = true;
                return $"{collider.name}@{collider.bounds}:separation={separation:F5}:contactOffset={Physics2D.defaultContactOffset:F5}:distanceValid={distance.isValid}";
            })
            .ToArray();
        if (!beyondContactTolerance)
        {
            Debug.Log($"ARUNGARD_MAIN_CONTACT_TOLERANCE actor={leader.CharacterId} position={leader.Position} hits=[{string.Join(";", hits)}]");
            return;
        }
        throw new InvalidOperationException(
            $"Party leader lost walkable clearance during runtime route: "
            + $"npcIndex={npcIndex} character={leader.CharacterId} position={leader.Position} "
            + $"center={center} radius={leader.MovementRadius:F3} offset={leader.MovementColliderCenterOffset} "
            + $"hits=[{string.Join(";", hits)}]");
    }

    static bool FollowersAreClear()
    {
        return party.ActiveMemberViews.All(view => view != null && view.CanStandAt(view.Position));
    }

    static void BeginRoofRevealCheck()
    {
        var roofs = Components<ArungardBuildingRoofPreview>(SceneManager.GetActiveScene());
        roofUnderTest = roofs.FirstOrDefault(roof => roof.upper != null
            && roof.upper.bounds.max.x - roof.upper.bounds.min.x > .1f
            && roof.upper.bounds.max.y - roof.upper.bounds.min.y > .1f
            && roof.groundNorth < roof.upper.bounds.max.y - .1f);
        if (roofUnderTest == null)
            throw new InvalidOperationException("No authored building roof is available for reveal verification.");
        var bounds = roofUnderTest.upper.bounds;
        var x = (bounds.min.x + bounds.max.x) * .5f;
        var y = Mathf.Clamp(roofUnderTest.groundNorth + .4f, bounds.min.y + .1f, bounds.max.y - .1f);
        if (!party.TryTeleport(new Vector2(x, y)))
            throw new InvalidOperationException("Could not place party for roof reveal verification.");
        Physics2D.SyncTransforms();
        Debug.Log($"ARUNGARD_MAIN_ROOF_REVEAL_SETUP roof={roofUnderTest.name} target=({x:F2},{y:F2})");
    }

    static void CloseSelector(DungeonSelectorWindowView value)
    {
        var window = new SerializedObject(value).FindProperty("window")?.objectReferenceValue as UIWorkspaceWindow;
        window?.Close();
    }

    static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    static void Finish(int code)
    {
        SessionState.SetBool(Flag, false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.Exit(code);
    }
}
