using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class CombatPresentationV4Tests
    {
        [TestCase(1000, false)]
        [TestCase(240, false)]
        [TestCase(125, true)]
        public void MovementHighlightDisplaysRealActionPoints(int cost, bool danger)
        {
            var root = new GameObject("movement-cost-test");
            try
            {
                var renderer = root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
                var settings = new SerializedObject(renderer);
                settings.FindProperty("feedbackRoot").objectReferenceValue = root.transform;
                settings.FindProperty("pathCostFont").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                settings.ApplyModifiedPropertiesWithoutUndo();
                renderer.ShowMovementPreview(new[] { new Godmist.Runtime.Combat.GridPosition(1, 0) },
                    new Godmist.Runtime.Combat.GridPosition(0, 0), cost, Color.white, danger);
                var label = root.transform.Find("Combat movement AP cost")?.GetComponent("TextMeshPro");
                Assert.That(label, Is.Not.Null);
                Assert.That(new SerializedObject(label).FindProperty("m_text").stringValue, Is.EqualTo((cost / 100m).ToString("0.##") + (danger ? " !" : "")));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void MovementCostRendersOverWorldOccluderWhileRouteStaysInFloorGroup(bool sortingBeforePreview)
        {
            var root = new GameObject("movement cost occlusion", typeof(UnityEngine.Rendering.SortingGroup));
            var cameraObject = new GameObject("movement cost camera", typeof(Camera));
            var occluderObject = new GameObject("world occluder", typeof(SpriteRenderer));
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.blue); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var target = new RenderTexture(192, 192, 24);
            var pixels = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var feedback = root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
                var data = new SerializedObject(feedback);
                var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
                    "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
                data.FindProperty("feedbackRoot").objectReferenceValue = root.transform;
                data.FindProperty("pathCostFont").objectReferenceValue = palette.Font;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (sortingBeforePreview) feedback.SetFloorSorting(0, -2000);
                var destination = new Godmist.Runtime.Combat.GridPosition(1, 0);
                feedback.ShowMovementPreview(new[] { destination }, new Godmist.Runtime.Combat.GridPosition(0, 0), 240, Color.white);
                if (!sortingBeforePreview) feedback.SetFloorSorting(0, -2000);
                var label = root.transform.Find("Combat movement AP cost").GetComponent<TMPro.TextMeshPro>();
                label.ForceMeshUpdate();
                foreach (var child in label.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                Assert.That(label.text, Is.EqualTo(2.4m.ToString("0.##")));
                Assert.That(label.textInfo.meshInfo[0].vertexCount, Is.GreaterThan(0));
                var group = label.GetComponent<UnityEngine.Rendering.SortingGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.sortAtRoot, Is.True);
                Assert.That(root.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder, Is.EqualTo(-2000));
                Assert.That(group.sortingOrder, Is.GreaterThan(2000));

                var bounds = label.GetComponent<MeshRenderer>().bounds;
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.4f;
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                var occluder = occluderObject.GetComponent<SpriteRenderer>();
                occluderObject.layer = 30;
                occluder.sprite = sprite;
                occluder.sharedMaterial = material;
                occluder.sortingOrder = 2000;
                occluder.transform.position = bounds.center;
                occluder.transform.localScale = Vector3.one * camera.orthographicSize * 4f;

                int RenderVisibleTextPixels()
                {
                    UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 192, 192), 0, 0); pixels.Apply();
                    var count = 0;
                    foreach (var pixel in pixels.GetPixels32())
                        if (pixel.r > 100 && pixel.g > 80) count++;
                    return count;
                }
                occluder.enabled = false;
                var standalonePixels = RenderVisibleTextPixels();
                Debug.Log($"AP GPU setup: font={label.font.name}, shader={label.fontSharedMaterial.shader.name}, queue={label.fontSharedMaterial.renderQueue}, bounds={bounds}, ortho={camera.orthographicSize}, standalone={standalonePixels}");
                Assert.That(standalonePixels, Is.GreaterThan(50), "Control: the native AP font must render before adding a world occluder.");
                occluder.enabled = true;
                group.sortAtRoot = false;
                Assert.That(RenderVisibleTextPixels(), Is.Zero, "Control: a floor-group label is fully hidden behind the world occluder.");
                group.sortAtRoot = true;
                Assert.That(RenderVisibleTextPixels(), Is.GreaterThan(50), "AP glyphs must actually render above the occluder.");

                feedback.IsPositionVisible = position => false;
                feedback.RefreshVisibility();
                Assert.That(label.gameObject.activeSelf, Is.False, "Overlay sorting must not bypass fog visibility.");
                feedback.IsPositionVisible = position => true;
                feedback.RefreshVisibility();
                Assert.That(label.gameObject.activeSelf, Is.True);
                feedback.ClearMovementPreview();
                Assert.That(typeof(Godmist.Views.Combat.CombatGridFeedbackRenderer).GetField("pathCostSortingGroup",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(feedback), Is.Null);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(occluderObject);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); Object.DestroyImmediate(material);
                Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void RepeatedFovRenderingReusesSpritesInsteadOfRecreatingObjects()
        {
            var root=new GameObject("fov-render-test");var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
            try
            {
                var renderer=root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();var so=new SerializedObject(renderer);
                so.FindProperty("feedbackRoot").objectReferenceValue=root.transform;so.FindProperty("feedbackSprite").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();
                var cells=new[]{new Godmist.Runtime.Combat.GridPosition(0,0),new Godmist.Runtime.Combat.GridPosition(1,0)};
                renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.EnemyFov,cells,Color.red);
                var first=root.GetComponentsInChildren<SpriteRenderer>();
                for(var i=0;i<20;i++)renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.EnemyFov,cells,Color.red);
                CollectionAssert.AreEquivalent(first,root.GetComponentsInChildren<SpriteRenderer>());
                Assert.That(root.transform.childCount,Is.EqualTo(2));
                renderer.ClearFill(Godmist.Views.Combat.CombatGridFeedbackLayer.EnemyFov);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(),Is.Empty);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);}
        }

        [Test]
        public void LargeUniformSkillFillUsesOneReusableMesh()
        {
            var root = new GameObject("skill-fill-test");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                var renderer = root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
                var settings = new SerializedObject(renderer);
                settings.FindProperty("feedbackRoot").objectReferenceValue = root.transform;
                settings.FindProperty("feedbackSprite").objectReferenceValue = sprite;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var cells = new System.Collections.Generic.List<Godmist.Runtime.Combat.GridPosition>();
                for (var x = 0; x < 12; x++) for (var y = 0; y < 12; y++)
                    cells.Add(new Godmist.Runtime.Combat.GridPosition(x, y));
                renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange, cells, Color.blue);
                var mesh = root.GetComponentInChildren<MeshFilter>();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.sharedMesh.vertexCount, Is.EqualTo(cells.Count * 4));
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
                renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange, cells, Color.red);
                Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
                renderer.ClearFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange);
                Assert.That(mesh.GetComponent<MeshRenderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ExplorationDurationUsesRemainingTicksAndCurrentTickProgress()
        {
            var character = new CharacterInstance { ExplorationStatusElapsedSeconds = 2.5f };
            var effect = new PassiveEffectRuntime { RemainingDuration = 3 };
            var status = new CombatStatusPresentation(effect, CombatStatusKind.Status, 0, explorationCharacter: character);
            Assert.That(UIWorkspaceTooltipView.ExplorationStatusDuration(status), Is.EqualTo("Pozostało: 16 s"));
            effect.RemainingDuration = 0;
            Assert.That(UIWorkspaceTooltipView.ExplorationStatusDuration(status), Is.EqualTo("Bez limitu czasu"));
        }

        [Test]
        public void InspectViewportIsAuthoredWithoutWhiteMaskAndWithRowTemplates()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatUnitInspectPanel.prefab");
            try
            {
                var view = root.GetComponent<CombatUnitInspectWindowView>();
                var so = new SerializedObject(view);
                var scroll = (ScrollRect)so.FindProperty("contentScroll").objectReferenceValue;
                Assert.That(scroll.viewport.GetComponent<Mask>(), Is.Null);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(scroll.viewport.GetComponent<Image>().color.a, Is.Zero);
                foreach (var field in new[] { "statusTemplate", "skillTemplate", "emptyStatuses", "emptySkills", "closeButton", "titleText" })
                    Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                Assert.That(((Component)so.FindProperty("statusTemplate").objectReferenceValue).gameObject.activeSelf, Is.False);
                Assert.That(((Component)so.FindProperty("skillTemplate").objectReferenceValue).gameObject.activeSelf, Is.False);
                foreach (var field in new[] { "skillTemplate", "statusTemplate" })
                {
                    var row = (Component)so.FindProperty(field).objectReferenceValue;
                    Assert.That(row.GetComponentsInChildren<Selectable>(true), Is.Empty, "Inspect rows only show information");
                    var icon = (Image)new SerializedObject(row).FindProperty("icon").objectReferenceValue;
                    Assert.That(icon.rectTransform.rect.width, Is.GreaterThanOrEqualTo(40));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void ExplorationPortraitHasThreeIconsWithoutCounters()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterPortrait Variant.prefab");
            try
            {
                var owner = root.GetComponent<CharacterInfoPartyPortraitView>();
                var collection = new SerializedObject(owner).FindProperty("statusCollection").objectReferenceValue;
                Assert.That(collection, Is.Not.Null);
                var so = new SerializedObject(collection);
                Assert.That(so.FindProperty("maximumVisibleIcons").intValue, Is.EqualTo(3));
                var icon = (CombatStatusIconView)so.FindProperty("iconTemplate").objectReferenceValue;
                Assert.That(new SerializedObject(icon).FindProperty("showCounters").boolValue, Is.False);
                Assert.That(icon.GetComponent<Graphic>(), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void TacticalTooltipDoesNotAdvertiseEmptyFloorAndExplainsDifficultTerrain()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); floor.TrySetMap(map);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Is.Empty);
            map.SetDifficultTerrain(Vector2Int.zero);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Does.Contain("+1 AP"));
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.right), Is.Empty);
            map.SetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), DungeonWallType.Low);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Does.Contain("tylko na istniejącą podłogę"));
        }
    }
}
