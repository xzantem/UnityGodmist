using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Views.Exploration
{
    /// <summary>
    /// Applies the small amount of scene-owned Arungard presentation wiring
    /// that must also work when the city is loaded from a clean checkout.
    /// Gameplay remains on the existing chest and party owners.
    /// </summary>
    public static class ArungardPresentationRuntimeSetup
    {
        private const string SceneName = "Arungard";
        private const string PresentationRootName = "ArungardPresentation_V10";
        private const string CameraBoundsName = "GrayboxCameraBounds";
        private const string ChestName = "CampaignStorageChest";
        private const string StashSpriteResource = "Arungard/CampaignStorageChestV1";
        private const float BoundsPadding = 1f;
        private static readonly Vector2 StashWorldPosition = new(15f, 11.75f);

        public static void ApplyIfPresent(IReadOnlyList<ExplorationCharacterBinding> partyBindings = null,
            IReadOnlyList<NpcSceneBinding> npcBindings = null)
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;

            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(value => value.name == PresentationRootName);
            if (root == null) return;

            ConfigureActorDepth(root, partyBindings, npcBindings);

            var bounds = root.transform.Find(CameraBoundsName)?.GetComponent<BoxCollider2D>();
            if (bounds == null)
            {
                Debug.LogError("Arungard presentation is missing " + CameraBoundsName + ".");
                return;
            }

            var camera = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(value => value.CompareTag("MainCamera"));
            var follow = camera?.GetComponent<ExplorationCameraFollow>();
            if (camera == null || follow == null)
            {
                Debug.LogError("Arungard presentation is missing its MainCamera/ExplorationCameraFollow owner.");
                return;
            }

            ConfigureCamera(camera, follow, bounds, FindPlayableGeometry(root, bounds));

            var chest = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<CampaignStorageChestView>(true))
                .SingleOrDefault();
            if (chest == null)
            {
                Debug.LogError("Arungard scene is missing its existing " + ChestName + " owner.");
                return;
            }

            if (!ConfigureChest(root, chest))
                return;

            ConfigureStashLighting(root, chest, bounds);

            Debug.Log($"ARUNGARD_RUNTIME_SETUP PASS boundsCenter={bounds.bounds.center} boundsSize={bounds.bounds.size} stash={chest.transform.position}");
        }

        private static void ConfigureCamera(Camera camera, ExplorationCameraFollow follow, BoxCollider2D bounds, Bounds playable)
        {
            bounds.transform.position = new Vector3(playable.center.x, playable.center.y, bounds.transform.position.z);
            bounds.transform.rotation = Quaternion.identity;
            bounds.transform.localScale = Vector3.one;
            bounds.offset = Vector2.zero;
            bounds.size = playable.size + new Vector3(BoundsPadding * 2f, BoundsPadding * 2f, 0f);
            camera.orthographic = true;
            follow.SetCameraBounds(bounds);
        }

        public static void ConfigureActorDepth(GameObject presentationRoot,
            IReadOnlyList<ExplorationCharacterBinding> partyBindings, IReadOnlyList<NpcSceneBinding> npcBindings)
        {
            if (presentationRoot == null) return;
            foreach (var depth in presentationRoot.GetComponentsInChildren<ArungardCityDepthPreview>(true))
            {
                var actors = new List<ArungardCityDepthPreview.Entry>(depth.actors ?? Array.Empty<ArungardCityDepthPreview.Entry>());
                void Register(Transform feet, SpriteRenderer body)
                {
                    if (feet == null || body == null) return;
                    var group = feet.GetComponent<UnityEngine.Rendering.SortingGroup>();
                    if (group == null) group = feet.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    group.sortingLayerID = body.sortingLayerID;
                    var entry = new ArungardCityDepthPreview.Entry { feet = feet, group = group };
                    var index = actors.FindIndex(existing => existing.feet == feet);
                    if (index >= 0) actors[index] = entry;
                    else actors.Add(entry);
                }
                // Use the bootstrap's authored bindings, including inactive party
                // members, so a later roster change requires no scene discovery.
                foreach (var binding in partyBindings ?? Array.Empty<ExplorationCharacterBinding>())
                    if (binding?.CharacterView is { } view) Register(view.transform, view.BodyRenderer);
                foreach (var binding in npcBindings ?? Array.Empty<NpcSceneBinding>())
                    if (binding?.NpcView is { } view) Register(view.transform, view.BodyRenderer);
                depth.actors = actors.ToArray();
                depth.RefreshDepth();
            }
        }

        private static bool ConfigureChest(GameObject root, CampaignStorageChestView chest)
        {
            chest.transform.position = new Vector3(StashWorldPosition.x, StashWorldPosition.y, chest.transform.position.z);
            chest.transform.rotation = Quaternion.identity;
            chest.transform.localScale = Vector3.one;

            var renderer = chest.GetComponent<SpriteRenderer>() ?? chest.gameObject.AddComponent<SpriteRenderer>();
            var sprite = Resources.Load<Sprite>(StashSpriteResource);
            if (sprite == null)
            {
                Debug.LogError("Arungard runtime setup could not load Resources sprite: " + StashSpriteResource);
                return false;
            }

            foreach (var childRenderer in chest.GetComponentsInChildren<SpriteRenderer>(true))
                if (childRenderer != renderer)
                    childRenderer.enabled = false;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            renderer.sortingOrder = ArungardCityDepthPreview.Order(chest.transform.position.y);
            var pavedGround = root.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(value => value.gameObject.name == "Paved ground");
            if (pavedGround?.sharedMaterial != null)
            {
                renderer.sharedMaterial = pavedGround.sharedMaterial;
                renderer.sortingLayerID = pavedGround.sortingLayerID;
            }

            var collider = chest.GetComponent<BoxCollider2D>() ?? chest.gameObject.AddComponent<BoxCollider2D>();
            collider.enabled = true;
            collider.isTrigger = false;
            collider.offset = new Vector2(0f, .225f);
            collider.size = new Vector2(1.5f, .45f);
            Physics2D.SyncTransforms();
            return true;
        }

        private static void ConfigureStashLighting(
            GameObject root,
            CampaignStorageChestView chest,
            BoxCollider2D bounds)
        {
            var chestRenderer = chest.GetComponent<SpriteRenderer>();
            var chestCollider = chest.GetComponent<BoxCollider2D>();
            foreach (var daylight in root.GetComponentsInChildren<ArungardDaylightPreview>(true))
            {
                daylight.scenery = (daylight.scenery ?? Array.Empty<ArungardDaylightPreview.Caster>())
                    .Where(entry => entry.source != null && entry.source != chestRenderer)
                    .Concat(new[] { new ArungardDaylightPreview.Caster { source = chestRenderer, heightOffset = 0f } })
                    .ToArray();
                daylight.contacts = (daylight.contacts ?? Array.Empty<BoxCollider2D>())
                    .Where(value => value != null && value != chestCollider)
                    .Concat(new[] { chestCollider })
                    .ToArray();
                var currentMask = new Bounds(
                    new Vector3(daylight.maskBounds.x + daylight.maskBounds.z * .5f, daylight.maskBounds.y + daylight.maskBounds.w * .5f),
                    new Vector3(daylight.maskBounds.z, daylight.maskBounds.w));
                if (!currentMask.Contains(bounds.bounds.min) || !currentMask.Contains(bounds.bounds.max))
                {
                    var size = Mathf.Max(bounds.size.x, bounds.size.y);
                    daylight.maskBounds = new Vector4(
                        bounds.bounds.center.x - size * .5f,
                        bounds.bounds.center.y - size * .5f,
                        size,
                        size);
                }
            }
        }

        private static Bounds FindPlayableGeometry(GameObject presentation, BoxCollider2D cameraBounds)
        {
            var colliders = presentation.GetComponentsInChildren<Collider2D>(true)
                .Where(value => value != null && value.gameObject.activeInHierarchy && value.enabled && !value.isTrigger && value != cameraBounds)
                .ToArray();
            if (colliders.Length == 0)
                throw new InvalidOperationException("No active solid Arungard gameplay colliders found.");

            var result = colliders[0].bounds;
            foreach (var collider in colliders.Skip(1)) result.Encapsulate(collider.bounds);
            return result;
        }
    }
}
