using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ForestHudPolishTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string VitalsPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatVitals.prefab";

        private static BattleUnit Unit(ResourceType resource = ResourceType.Rage, int value = 20) => new()
        {
            IsPlayerSide = true, CurrentActionPoints = 400, MaxActionPoints = 1000,
            Character = new CharacterInstance
            {
                CurrentHealth = 492, Stats = new CharacterStats { MaxHealth = new Stat(562.5f) },
                ResourcePool = new ResourcePool(resource, value, 50, 0), LifeState = CharacterLifeState.Alive
            }
        };

        [Test]
        public void FractionalHealthMaximumMatchesPartyAndActiveVitalsWithoutChangingSliderMaximum()
        {
            var vitalsObject = PrefabUtility.LoadPrefabContents(VitalsPath);
            var partyObject = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab");
            try
            {
                var unit = Unit();
                var vitals = vitalsObject.GetComponent<CombatVitalsView>();
                var party = partyObject.GetComponent<CombatPartyMemberView>();
                vitals.Bind(unit); party.Bind(unit);
                var bottomText = (TMP_Text)new SerializedObject(vitals).FindProperty("healthValueText").objectReferenceValue;
                var partyText = (TMP_Text)new SerializedObject(party).FindProperty("healthValueText").objectReferenceValue;
                Assert.That(bottomText.text, Is.EqualTo(partyText.text));
                Assert.That(bottomText.text, Is.EqualTo(CombatHealthPresentation.Format(unit.Character)));
                var slider = (Slider)new SerializedObject(vitals).FindProperty("healthSlider").objectReferenceValue;
                Assert.That(slider.maxValue, Is.EqualTo(562.5f));
                Assert.That(slider.normalizedValue, Is.EqualTo(492 / 562.5f).Within(.0001));
            }
            finally { PrefabUtility.UnloadPrefabContents(vitalsObject); PrefabUtility.UnloadPrefabContents(partyObject); }
        }

        [TestCase(ResourceType.Rage, 0)] [TestCase(ResourceType.Rage, 20)] [TestCase(ResourceType.Rage, 50)]
        [TestCase(ResourceType.Mana, 20)] [TestCase(ResourceType.Momentum, 50)]
        public void AuthoredResourceFillFitsFrameAndRetainsValueAndColour(ResourceType resource, int value)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VitalsPath));
            try
            {
                var view = root.GetComponent<CombatVitalsView>(); view.Bind(Unit(resource, value));
                var slider = (Slider)new SerializedObject(view).FindProperty("resourceSlider").objectReferenceValue;
                var area = (RectTransform)slider.fillRect.parent;
                var frame = slider.transform.Find("GothicBarFrame").GetComponent<Image>();
                Assert.That(frame.sprite.border.y / (frame.pixelsPerUnit * frame.pixelsPerUnitMultiplier),
                    Is.EqualTo(area.offsetMin.y).Within(.001), "Frame border must end at the fill, not cross over it.");
                Assert.That(frame.sprite.border.w / (frame.pixelsPerUnit * frame.pixelsPerUnitMultiplier),
                    Is.EqualTo(-area.offsetMax.y).Within(.001));
                Assert.That(slider.value, Is.EqualTo(value));
                Assert.That(slider.fillRect.GetComponent<Image>().color, Is.EqualTo(CombatResourceColors.Get(resource)));
                view.Bind(Unit(ResourceType.Mana, 50)); view.Bind(Unit(resource, value));
                Assert.That(frame.pixelsPerUnitMultiplier, Is.EqualTo(2), "Rebinding must not compound geometry changes.");
                Assert.That(slider.normalizedValue, Is.EqualTo(value / 50f).Within(.001));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ForestPerimeterFeathersMissingTerrainWhileMasonryKeepsItsExistingEdge(bool forest)
        {
            var root = new GameObject("Fog perimeter regression");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                if (forest)
                {
                    var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Godmist.Editor.ForestPrototypeSetup.DefinitionPath);
                    view.ConfigureVegetationPresentation(definition.VegetationPresentation);
                }
                var projection = new Dictionary<Vector2Int, DungeonKnowledge>();
                // Continuous known area to the left; no terrain to the right.
                for (var y = -1; y <= 1; y++) for (var x = -1; x <= 0; x++)
                    projection[new Vector2Int(x, y)] = DungeonKnowledge.Visible;
                var boundary = (bool)typeof(DungeonFloorMapView).GetMethod("ReadFogNeighbours", Private)
                    .Invoke(view, new object[] { projection, Vector2Int.zero, DungeonKnowledge.Visible });
                var neighbours = (DungeonKnowledge[])typeof(DungeonFloorMapView).GetField("fogNeighbours", Private).GetValue(view);
                var samples = new float[81];
                DungeonFogBlend.SampleGridBrightness(DungeonKnowledge.Visible, neighbours, samples, forest);
                Assert.That(boundary, Is.EqualTo(forest));
                Assert.That(samples[4 * 9 + 8], Is.EqualTo(forest ? 0 : 1).Within(.001));
                Assert.That(samples[4 * 9 + 4], Is.GreaterThan(samples[4 * 9 + 8] - .001));
                Assert.That(projection, Has.Count.EqualTo(6), "Presentation must not add discovered cells.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MovementContourIsThinAndSharedEdgesRemainClear()
        {
            var root = new GameObject("Thin movement range");
            try
            {
                var view = root.AddComponent<CombatGridFeedbackRenderer>();
                view.ShowOutline(CombatGridFeedbackLayer.MovementRange,
                    new[] { new GridPosition(0, 0), new GridPosition(1, 0) }, Color.yellow);
                var renderer = root.GetComponentsInChildren<MeshRenderer>().Single();
                var texture = (Texture2D)renderer.sharedMaterial.mainTexture;
                var uv = renderer.GetComponent<MeshFilter>().sharedMesh.uv;
                float Alpha(float x) => texture.GetPixelBilinear(Mathf.Lerp(uv[0].x, uv[3].x, x),
                    Mathf.Lerp(uv[0].y, uv[1].y, .5f)).a;
                Assert.That(Alpha(.07f), Is.GreaterThan(.02f), "Contour still needs to be readable.");
                Assert.That(Alpha(.21f), Is.LessThan(.005f), "No broad glowing strip inside the range.");
                Assert.That(Alpha(.99f), Is.LessThan(.005f), "No internal edge between adjacent cells.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EnemyArcOnlyShowsForVisibleLivingCombatEnemiesAndReusesItsMesh()
        {
            var root = new GameObject("Enemy arc", typeof(SpriteRenderer));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            try
            {
                var source = root.GetComponent<SpriteRenderer>(); source.sprite = sprite;
                var view = root.AddComponent<CombatUnitVisualStateView>();
                var unit = Unit(); unit.IsPlayerSide = false;
                view.Refresh(unit, false, false, false);
                var marker = root.GetComponentsInChildren<MeshRenderer>().Single();
                var mesh = marker.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(marker.enabled, Is.True); Assert.That(mesh.bounds.max.y, Is.LessThan(0));
                source.forceRenderingOff = true; view.Refresh(unit, false, false, false);
                Assert.That(marker.enabled, Is.False);
                source.forceRenderingOff = false; unit.Character.LifeState = CharacterLifeState.Dead;
                view.Refresh(unit, false, false, false); Assert.That(marker.enabled, Is.False);
                unit.Character.LifeState = CharacterLifeState.Alive; unit.IsPlayerSide = true;
                view.Refresh(unit, false, false, false); Assert.That(marker.enabled, Is.False);
                unit.IsPlayerSide = false; view.Refresh(unit, false, false, false);
                Assert.That(marker.enabled, Is.True); Assert.That(marker.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                view.ClearHighlight(); Assert.That(marker.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void RenderAuthoredVitalsForRageManaAndMomentum()
        {
            var root = new GameObject("Vitals render", typeof(Canvas));
            var cameraObject = new GameObject("Vitals preview camera", typeof(Camera));
            var target = new RenderTexture(640, 400, 24);
            var pixels = new Texture2D(640, 400, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(640, 400);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = 200; camera.aspect = 1.6f; camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .07f, .09f);
                camera.targetTexture = target;
                var resources = new[] { ResourceType.Rage, ResourceType.Mana, ResourceType.Momentum };
                for (var i = 0; i < resources.Length; i++)
                {
                    var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VitalsPath), root.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, 170 - i * 125);
                    rect.sizeDelta = new Vector2(400, 110);
                    go.GetComponent<CombatVitalsView>().Bind(Unit(resources[i], i == 0 ? 20 : 50));
                }
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 640, 400), 0, 0); pixels.Apply();
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Godmist/out"));
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, "forest-hud-vitals-preview.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
