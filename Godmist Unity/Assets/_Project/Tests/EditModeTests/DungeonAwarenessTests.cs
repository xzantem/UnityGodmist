using System;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonAwarenessTests
    {
        [Test]
        public void ProductionPrefabHasWiredNonInteractiveWorldSpaceAwarenessDiamond()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var serialized = new UnityEditor.SerializedObject(view);
                var root = serialized.FindProperty("awarenessRoot").objectReferenceValue as UnityEngine.GameObject;
                var fill = serialized.FindProperty("awarenessFill").objectReferenceValue as UnityEngine.UI.Image;
                var palette = serialized.FindProperty("awarenessStyle").objectReferenceValue
                    as Godmist.Views.UI.Workspace.UIWorkspaceStylePalette;
                Assert.That(palette, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Views.UI.Workspace.UIWorkspaceStylePalette>(
                    "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset")));
                Assert.That(root, Is.Not.Null);
                Assert.That(fill, Is.Not.Null);
                Assert.That(root.activeSelf, Is.False);
                Assert.That(root.transform.parent, Is.EqualTo(instance.transform));
                Assert.That(root.GetComponent<UnityEngine.Canvas>().renderMode, Is.EqualTo(UnityEngine.RenderMode.WorldSpace));
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true), Is.Empty);
                foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False);
                view.Bind(new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive });
                view.ShowAwareness(.5f, true);
                Assert.That(root.activeSelf, Is.True);
                Assert.That(fill.fillAmount, Is.EqualTo(.5f));
                Assert.That(fill.enabled, Is.False, "The old rectangular fill must not remain visible.");
                var indicator = root.GetComponentInChildren<Godmist.Views.Exploration.ExplorationAwarenessIndicator>(true);
                Assert.That(indicator, Is.Not.Null);
                Assert.That(indicator.Progress, Is.EqualTo(.5f));
                Assert.That(indicator.raycastTarget, Is.False);
                var exclamation = root.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                Assert.That(exclamation, Is.Not.Null);
                Assert.That(exclamation.text, Is.EqualTo("!"));
                Assert.That(exclamation.font, Is.SameAs(palette.Font));
                Assert.That(root.transform.Find("GodmistAwarenessFrame").GetComponent<UnityEngine.UI.Image>().sprite,
                    Is.SameAs(palette.GetArtwork("icon_button_normal")));
                Assert.That(root.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new UnityEngine.Vector2(64f, 64f)));
                Assert.That(exclamation.fontSize, Is.EqualTo(42f));
                Assert.That(exclamation.raycastTarget, Is.False);
                view.ShowAwareness(.5f, false);
                Assert.That(root.activeSelf, Is.False);
                view.ShowAwareness(2f, true);
                Assert.That(indicator.Progress, Is.EqualTo(1f));
                view.ShowAwareness(-1f, true);
                Assert.That(indicator.Progress, Is.Zero);
                Assert.That(root.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [TestCase(.25f)]
        [TestCase(.5f)]
        [TestCase(1f)]
        public void GothicAwarenessReusesSharedFrameFontAndPaletteWithInsetProgress(float progress)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Views.UI.Workspace.UIWorkspaceStylePalette>(
                "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
            Assert.That(palette, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var data = new UnityEditor.SerializedObject(view);
                data.FindProperty("awarenessStyle").objectReferenceValue = palette;
                data.ApplyModifiedPropertiesWithoutUndo();
                view.Bind(new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive });
                view.ShowAwareness(progress, true);
                var root = AwarenessRect(view);
                var frame = root.Find("GodmistAwarenessFrame").GetComponent<UnityEngine.UI.Image>();
                var indicator = root.GetComponentInChildren<Godmist.Views.Exploration.ExplorationAwarenessIndicator>();
                var text = root.GetComponentInChildren<TMPro.TextMeshProUGUI>();

                Assert.That(frame.sprite, Is.SameAs(palette.GetArtwork("icon_button_normal")));
                Assert.That(frame.color, Is.EqualTo(Color.white), "Preserve the frame's authored metal and gold colours.");
                Assert.That(frame.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(frame.rectTransform.localEulerAngles.z, Is.EqualTo(45f).Within(.001f));
                Assert.That(frame.rectTransform.rect.width * Mathf.Sqrt(2f), Is.EqualTo(root.rect.width).Within(.001f));
                Assert.That(frame.transform.GetSiblingIndex(), Is.LessThan(indicator.transform.GetSiblingIndex()));
                Assert.That(text.transform.GetSiblingIndex(), Is.GreaterThan(indicator.transform.GetSiblingIndex()));
                Assert.That(text.font, Is.SameAs(palette.Font));
                Assert.That(text.color, Is.EqualTo(palette.GetTextColor(Godmist.Views.UI.Workspace.UIWorkspaceTextStyle.Body)));
                Assert.That(indicator.FillColor, Is.EqualTo(Color.Lerp(
                    palette.GetAccentColor(Godmist.Views.UI.Workspace.UIWorkspaceAccentStyle.Warning),
                    palette.GetAccentColor(Godmist.Views.UI.Workspace.UIWorkspaceAccentStyle.Negative), progress)));
                foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False);

                using (var vertices = new UnityEngine.UI.VertexHelper())
                {
                    typeof(Godmist.Views.Exploration.ExplorationAwarenessIndicator).GetMethod("OnPopulateMesh",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                        null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null)
                        .Invoke(indicator, new object[] { vertices });
                    Assert.That(vertices.currentVertCount, Is.GreaterThanOrEqualTo(3));
                    var vertex = new UIVertex();
                    for (var i = 0; i < vertices.currentVertCount; i++)
                    {
                        vertices.PopulateUIVertex(ref vertex, i);
                        Assert.That(Mathf.Abs(vertex.position.x) + Mathf.Abs(vertex.position.y), Is.LessThanOrEqualTo(20f),
                            "The progress overlay must not paint over the Gothic frame.");
                        Assert.That(vertex.color.a, Is.InRange((byte)113, (byte)116), "Progress is a restrained tint over the dark backing.");
                    }
                }
                view.ShowAwareness(.8f, true);
                Assert.That(root.Find("GodmistAwarenessFrame").GetComponent<UnityEngine.UI.Image>(), Is.SameAs(frame));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [TestCase(32, 0f, .7f)]
        [TestCase(64, .5f, 1f)]
        [TestCase(128, .05f, 1.6f)]
        public void IndicatorBottomStaysAboveSpriteForDifferentHeightsPivotsAndScales(
            int height, float pivotY, float scale)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            var texture = new Texture2D(32, height);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, height), new Vector2(.5f, pivotY), 32f);
            try
            {
                var view = instance.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                view.Bind(new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive });
                instance.transform.position = new Vector3(-8f, 11f, 2f);
                instance.transform.localScale = new Vector3(1.2f, .8f, 1f);
                view.BodyRenderer.sprite = sprite;
                view.BodyRenderer.transform.localPosition = new Vector3(.2f, .4f, 0f);
                view.BodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                view.BodyRenderer.flipX = true;
                var root = AwarenessRect(view);
                root.pivot = new Vector2(.3f, .8f);
                var originalDepth = root.position.z;

                view.ShowAwareness(.5f, true);

                AssertIndicatorAboveBody(view, root);
                Assert.That(root.position.z, Is.EqualTo(originalDepth).Within(.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase("enemy.catacombs.ghoul")]
        [TestCase("enemy.catacombs.dead_king")]
        [TestCase("enemy.forest.rabid_wolf")]
        public void IndicatorFollowsCurrentNativeAnimationFrameWithoutAnotherProgressUpdate(string characterId)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                view.Bind(new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive });
                view.ShowAwareness(.5f, true);
                var root = AwarenessRect(view);
                var lateUpdate = typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetMethod(
                    "LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var frameIndex = 0;
                for (var direction = 0; direction < 8; direction++)
                {
                    var angle = direction * Mathf.PI / 4f;
                    Assert.That(Godmist.Views.Characters.CharacterAnimationResolver.TryResolveCharacter(
                        characterId, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                        Godmist.Views.Characters.CharacterAnimationState.Walk, out var clip), Is.True);
                    foreach (var frame in clip.Frames)
                    {
                        view.BodyRenderer.sprite = frame;
                        view.BodyRenderer.transform.localScale = new Vector3(.75f, 1f + .05f * (frameIndex++ % 4), 1f);
                        instance.transform.position += new Vector3(.5f, -.25f, 0f);
                        lateUpdate.Invoke(view, null);

                        AssertIndicatorAboveBody(view, root);
                        Assert.That(root.gameObject.activeSelf, Is.True);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static RectTransform AwarenessRect(Godmist.Views.Exploration.ExplorationCharacterView view)
        {
            var serialized = new UnityEditor.SerializedObject(view);
            var root = (GameObject)serialized.FindProperty("awarenessRoot").objectReferenceValue;
            return (RectTransform)root.transform;
        }

        private static void AssertIndicatorAboveBody(
            Godmist.Views.Exploration.ExplorationCharacterView view, RectTransform root)
        {
            var bottom = root.TransformPoint(new Vector3(root.rect.center.x, root.rect.yMin, 0f));
            var bounds = view.BodyRenderer.bounds;
            Assert.That(bottom.x, Is.EqualTo(bounds.center.x).Within(.0001f));
            Assert.That(bottom.y - bounds.max.y, Is.EqualTo(2f / 32f).Within(.0001f),
                "The whole diamond must clear the current sprite, including its scale and pivot.");
        }

        [Test]
        public void DetectsAfterTwoSecondsAndDecaysOverThree()
        {
            var state = new DungeonAwarenessState();
            Assert.That(state.Advance(1.9f, true, false), Is.False);
            Assert.That(state.Advance(.11f, true, false), Is.True);
            Assert.That(state.Advance(1.5f, false, false), Is.False);
            Assert.That(state.Progress, Is.EqualTo(.5f).Within(.00001f));
            state.Advance(1.5f, false, false);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void ReacquisitionContinuesRemainingProgress()
        {
            var state = new DungeonAwarenessState();
            state.Advance(1f, true, false);
            state.Advance(.6f, false, false);
            Assert.That(state.Progress, Is.EqualTo(.3f).Within(.00001f));
            Assert.That(state.Advance(1f, true, false), Is.False);
            Assert.That(state.Advance(.41f, true, false), Is.True);
        }

        [Test]
        public void FightingObserverBypassesDelayButStillNeedsSight()
        {
            var state = new DungeonAwarenessState();
            Assert.That(state.Advance(0f, true, true), Is.True);
            Assert.That(state.Advance(0f, false, true), Is.False);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void FailedStartCanRetryWhileVisibleButFullProgressDoesNotSeeThroughWalls()
        {
            var state = new DungeonAwarenessState();
            state.Advance(2f, true, false);
            Assert.That(state.Advance(0f, false, false), Is.False);
            Assert.That(state.Advance(0f, true, false), Is.True);
        }

        [Test]
        public void EnemyHistoryIsIndependentAndFloorInstanceRetainsIt()
        {
            var floor = new DungeonFloorInstance(0);
            floor.GetEnemyAwareness("a").Advance(1f, true, false);
            Assert.That(floor.GetEnemyAwareness("a").Progress, Is.EqualTo(.5f));
            Assert.That(floor.GetEnemyAwareness("b").Progress, Is.Zero);
            Assert.That(new DungeonFloorInstance(1).GetEnemyAwareness("a").Progress, Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidElapsedTimeCannotCorruptState(float elapsed)
        {
            var state = new DungeonAwarenessState();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(elapsed, true, false));
            Assert.That(state.Progress, Is.Zero);
        }

        [TestCase(-.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSavedProgressIsRejected(float progress)
        {
            Assert.That(new DungeonAwarenessState().TryRestore(progress), Is.False);
        }

        [Test]
        public void BarHidesWithoutPartyVisibilityAtZeroAndAfterDeath()
        {
            var actor = new UnityEngine.GameObject("observer");
            var bar = new UnityEngine.GameObject("awareness", typeof(UnityEngine.RectTransform));
            try
            {
                var view = actor.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var fill = bar.AddComponent<UnityEngine.UI.Image>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetField("awarenessRoot", flags).SetValue(view, bar);
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetField("awarenessFill", flags).SetValue(view, fill);
                var character = new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive };
                view.Bind(character);
                view.ShowAwareness(.5f, true);
                Assert.That(bar.activeSelf, Is.True);
                Assert.That(fill.fillAmount, Is.EqualTo(.5f));
                view.ShowAwareness(.5f, false);
                Assert.That(bar.activeSelf, Is.False);
                view.ShowAwareness(0f, true);
                Assert.That(bar.activeSelf, Is.False);
                character.LifeState = Godmist.Core.Enums.CharacterLifeState.Dead;
                view.ShowAwareness(1f, true);
                Assert.That(bar.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(bar);
            }
        }

        [Test]
        public void DiamondFillRemainsInsideSilhouetteAndRisesFromBottom()
        {
            var rect = new UnityEngine.Rect(0f, 0f, 32f, 32f);
            var empty = Godmist.Views.Exploration.ExplorationAwarenessIndicator.BuildFillPolygon(rect, 0f);
            Assert.That(empty, Is.Empty);

            var half = Godmist.Views.Exploration.ExplorationAwarenessIndicator.BuildFillPolygon(rect, .5f);
            Assert.That(half.Length, Is.GreaterThanOrEqualTo(3));
            var center = rect.center;
            var radius = rect.width * .5f - 4f;
            foreach (var point in half)
            {
                Assert.That(point.y, Is.LessThanOrEqualTo(center.y + .0001f));
                var silhouetteWidth = radius - Mathf.Abs(point.y - center.y);
                Assert.That(Mathf.Abs(point.x - center.x), Is.LessThanOrEqualTo(silhouetteWidth + .0001f));
            }

            var full = Godmist.Views.Exploration.ExplorationAwarenessIndicator.BuildFillPolygon(rect, 1f);
            Assert.That(full.Length, Is.EqualTo(4));
            Assert.That(full[0].y, Is.EqualTo(center.y - radius).Within(.0001f));
        }
    }
}
