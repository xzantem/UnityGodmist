using System.Reflection;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using System;
using System.Collections;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;

namespace Godmist.Tests.EditMode
{
    public class DungeonLightingCadenceTests
    {
        [Test]
        public void ForestTorchEnergyBreathesGentlyWithoutAlternatingFiveHzSamples()
        {
            for (int phase = 0; phase < 16; phase++)
            {
                float previous = DungeonSilhouetteLighting.ForestTorchIntensity(0, phase);
                for (int frame = 1; frame <= 600; frame++)
                {
                    float next = DungeonSilhouetteLighting.ForestTorchIntensity(frame / 60f, phase);
                    Assert.That(next, Is.InRange(.919f, .981f));
                    Assert.That(Mathf.Abs(next - previous), Is.LessThan(.0025f),
                        "Energy must not jump between bright and dark geometry ticks.");
                    previous = next;
                }
                Assert.That(DungeonSilhouetteLighting.ForestTorchIntensity(.251f, phase),
                    Is.Not.EqualTo(DungeonSilhouetteLighting.ForestTorchIntensity(.26f, phase)),
                    "Forest energy is sampled continuously inside one geometry tick.");
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void GroundPropsKeepSpriteColoursWhileForestUnderlayKeepsSurfaceBlend(bool underlay)
        {
            var root = new GameObject("Ground material regression");
            var prop = new GameObject("Forest prop", typeof(SpriteRenderer));
            var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            try
            {
                var lighting = root.AddComponent<DungeonSilhouetteLighting>();
                typeof(DungeonSilhouetteLighting).GetField("floorMaterial", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(lighting, material);
                material.SetFloat("_ForestSurfaceEnabled", 1);
                var renderer = prop.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                var block = new MaterialPropertyBlock();
                if (underlay) block.SetVector("_ForestPatchBounds", new Vector4(0, 0, 1, 1));
                renderer.SetPropertyBlock(block);
                typeof(DungeonSilhouetteLighting).GetMethod("ApplyGroundLighting", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(lighting, new object[] { renderer, false, false });
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ForestSurfaceEnabled"), Is.EqualTo(underlay ? 1 : 0));
                Assert.That(material.GetFloat("_ForestSurfaceEnabled"), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(prop); Object.DestroyImmediate(sprite); if (material != null) Object.DestroyImmediate(material); }
        }

        [Test]
        public void FiftyFlickerUpdatesReuseOneMaskButSpriteAndDoorChangesInvalidateIt()
        {
            using var fixture = new ShadowCacheFixture();
            var data = fixture.Get<Vector4[]>("lightData");
            var colors = fixture.Get<Vector4[]>("lightColors");
            int rebuilds = 0;
            for (int tick = 0; tick < 50; tick++)
            {
                data[0].w = DungeonSilhouetteLighting.TorchIntensity(tick * .2f, 1.7f);
                colors[0] = new Vector4(tick * .01f, .5f, .2f, 1);
                if (!fixture.NeedsRebuild()) continue;
                rebuilds++;
                fixture.Commit();
            }
            Assert.That(rebuilds, Is.EqualTo(1), "Flicker changes light uniforms but not projected silhouettes.");
            fixture.Renderer.sprite = fixture.SecondSprite;
            Assert.That(fixture.NeedsRebuild(), Is.True, "Idle/walk sprite frames change silhouettes.");
            fixture.Commit();
            fixture.Entry.GetType().GetField("projectedElevation").SetValue(fixture.Entry, 1f);
            Assert.That(fixture.NeedsRebuild(), Is.True, "Door elevation/visual transitions invalidate the mask.");
            fixture.Commit();
            Assert.That(fixture.NeedsRebuild(), Is.False);
        }

        [TestCase("move")]
        [TestCase("scale")]
        [TestCase("flip")]
        [TestCase("contact")]
        [TestCase("lightPosition")]
        [TestCase("lightRadius")]
        [TestCase("lightHeight")]
        [TestCase("lightCount")]
        [TestCase("bounds")]
        [TestCase("removeCaster")]
        [TestCase("replaceCaster")]
        [TestCase("reset")]
        public void ShadowGeometryChangesInvalidateCacheUntilSuccessfulSubmission(string change)
        {
            using var fixture = new ShadowCacheFixture();
            Assert.That(fixture.NeedsRebuild(), Is.True);
            fixture.Commit();
            Assert.That(fixture.NeedsRebuild(), Is.False);
            var data = fixture.Get<Vector4[]>("lightData");
            switch (change)
            {
                case "move": fixture.Renderer.transform.position += Vector3.right; break;
                case "scale": fixture.Renderer.transform.localScale = Vector3.one * 2; break;
                case "flip": fixture.Renderer.flipX = true; break;
                case "contact": fixture.Set("contacts", new[] { new DungeonSilhouetteLighting.ContactProfile
                    { sprite = fixture.Renderer.sprite, footLocal = Vector2.up, slope = .3f, offset = .1f } }); break;
                case "lightPosition": data[0].x += 1; break;
                case "lightRadius": data[0].z += 1; break;
                case "lightHeight": fixture.Set("lightHeight", 5f); break;
                case "lightCount": fixture.Get<IList>("activeTorches").Clear(); break;
                case "bounds": fixture.Bounds = new Rect(-20, -20, 40, 40); break;
                case "removeCaster": fixture.Get<IDictionary>("entries").Remove(fixture.Renderer); break;
                case "replaceCaster":
                    fixture.Get<IDictionary>("entries").Remove(fixture.Renderer);
                    var replacement = new GameObject("Replacement caster", typeof(SpriteRenderer));
                    replacement.transform.SetParent(fixture.Root.transform);
                    var renderer = replacement.GetComponent<SpriteRenderer>(); renderer.sprite = fixture.Renderer.sprite;
                    fixture.Touch(renderer); break;
                case "reset": fixture.Set("shadowCacheReady", false); break;
            }
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.NeedsRebuild(), Is.True, "Do not accept cached inputs before mask submission succeeds.");
            fixture.Commit();
            Assert.That(fixture.NeedsRebuild(), Is.False);
        }

        private sealed class ShadowCacheFixture : IDisposable
        {
            private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
            public readonly GameObject Root = new("shadow input cache");
            public readonly DungeonSilhouetteLighting Lighting;
            public readonly SpriteRenderer Renderer;
            public readonly Sprite FirstSprite, SecondSprite;
            public readonly object Entry;
            public Rect Bounds = new(-16, -16, 32, 32);
            public ShadowCacheFixture()
            {
                Lighting = Root.AddComponent<DungeonSilhouetteLighting>();
                var caster = new GameObject("Caster", typeof(SpriteRenderer)); caster.transform.SetParent(Root.transform);
                Renderer = caster.GetComponent<SpriteRenderer>();
                FirstSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 32);
                SecondSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 2), Vector2.one * .5f, 32);
                Renderer.sprite = FirstSprite;
                Set("cameraGroundBounds", new Rect(-10, -10, 20, 20));
                Touch(Renderer);
                Entry = Get<IDictionary>("entries")[Renderer];
                var torchType = typeof(DungeonSilhouetteLighting).GetNestedType("Torch", BindingFlags.NonPublic);
                Get<IList>("activeTorches").Add(Activator.CreateInstance(torchType, true));
                Get<Vector4[]>("lightData")[0] = new Vector4(1, 2, 5, 1);
            }
            public T Get<T>(string name) => (T)typeof(DungeonSilhouetteLighting).GetField(name, Flags).GetValue(Lighting);
            public void Set(string name, object value) => typeof(DungeonSilhouetteLighting).GetField(name, Flags).SetValue(Lighting, value);
            public void Touch(SpriteRenderer renderer, bool actor = true) => typeof(DungeonSilhouetteLighting).GetMethod("Touch", Flags)
                .Invoke(Lighting, new object[] { renderer, actor, 0f });
            public SpriteRenderer AddStaticCaster()
            {
                var root = new GameObject("Static prop", typeof(SpriteRenderer)); root.transform.SetParent(Root.transform);
                var renderer = root.GetComponent<SpriteRenderer>(); renderer.sprite = FirstSprite;
                renderer.transform.position = Vector3.left;
                Touch(renderer, false); return renderer;
            }
            public void SetLights(params Vector4[] sources)
            {
                var lights = Get<IList>("activeTorches"); lights.Clear();
                var torchType = typeof(DungeonSilhouetteLighting).GetNestedType("Torch", BindingFlags.NonPublic);
                var data = Get<Vector4[]>("lightData");
                for (int i = 0; i < sources.Length; i++)
                {
                    lights.Add(Activator.CreateInstance(torchType, true)); data[i] = sources[i];
                }
            }
            public bool NeedsRebuild() => (bool)typeof(DungeonSilhouetteLighting).GetMethod("NeedsShadowMaskRebuild", Flags)
                .Invoke(Lighting, new object[] { Bounds });
            public void Commit()
            {
                Set("bounds", Bounds);
                typeof(DungeonSilhouetteLighting).GetMethod("RememberShadowConfiguration", Flags).Invoke(Lighting, null);
            }
            public void Dispose()
            {
                typeof(DungeonSilhouetteLighting).GetMethod("OnDisable", Flags).Invoke(Lighting, null);
                Object.DestroyImmediate(Root); Object.DestroyImmediate(FirstSprite); Object.DestroyImmediate(SecondSprite);
            }
        }

        [Test]
        public void ActorMovementInvalidatesOnlyOldAndNewTorchSlicesAndRepeatedChecksStayLocal()
        {
            using var fixture = new ShadowCacheFixture();
            fixture.SetLights(new Vector4(0, 1, 2, 1), new Vector4(6, 1, 2, 1), new Vector4(-6, 1, 2, 1));
            fixture.NeedsRebuild(); fixture.Commit();
            fixture.Renderer.transform.position = Vector3.right * .5f;
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(1));
            Assert.That(fixture.Get<int>("dirtyStaticShadowLights"), Is.Zero);
            fixture.NeedsRebuild();
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(1));
            fixture.Commit();
            fixture.Renderer.transform.position = Vector3.right * 6;
            fixture.NeedsRebuild();
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(3), "Old torch must clear its previous shadow; new torch must draw it.");
            Assert.That(fixture.Get<int>("dirtyStaticShadowLights"), Is.Zero);
        }

        [Test]
        public void HiddenActorAndMovingStaticPropOnlyInvalidateTheirOwnTorchSlices()
        {
            using var fixture = new ShadowCacheFixture();
            fixture.SetLights(new Vector4(0, 1, 2, 1), new Vector4(6, 1, 2, 1), new Vector4(-6, 1, 2, 1));
            var prop = fixture.AddStaticCaster(); prop.transform.position = Vector3.right * 6;
            fixture.NeedsRebuild(); fixture.Commit();
            prop.transform.position += Vector3.up * .2f;
            fixture.NeedsRebuild();
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(2));
            Assert.That(fixture.Get<int>("dirtyStaticShadowLights"), Is.EqualTo(2));
            fixture.Commit();
            fixture.Renderer.forceRenderingOff = true;
            fixture.Set("revision", fixture.Get<int>("revision") + 1);
            fixture.Touch(prop, false); fixture.Touch(fixture.Renderer);
            typeof(DungeonSilhouetteLighting).GetMethod("PruneCasters", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(fixture.Lighting, null);
            fixture.NeedsRebuild();
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(1));
            Assert.That(fixture.Get<int>("dirtyStaticShadowLights"), Is.Zero);
        }

        [Test]
        public void ReorderingSourcesInvalidatesOnlyLayersWhoseSourceChanged()
        {
            using var fixture = new ShadowCacheFixture();
            fixture.SetLights(new Vector4(0, 1, 2, 1), new Vector4(6, 1, 2, 1), new Vector4(-6, 1, 2, 1));
            fixture.NeedsRebuild(); fixture.Commit();
            var data = fixture.Get<Vector4[]>("lightData");
            (data[0], data[1]) = (data[1], data[0]);
            fixture.NeedsRebuild();
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(3));
            Assert.That(fixture.Get<int>("dirtyStaticShadowLights"), Is.EqualTo(3));
        }

        [Test]
        public void CarriedTorchMovementDoesNotRedrawStationaryTorchMasks()
        {
            using var fixture = new ShadowCacheFixture();
            fixture.SetLights(new Vector4(0, 1, 2, 1), new Vector4(6, 1, 2, 1), new Vector4(-6, 1, 2, 1));
            fixture.NeedsRebuild(); fixture.Commit();
            fixture.Get<Vector4[]>("lightData")[1].x += .3f;
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(2));
            Assert.That(fixture.Get<int>("dirtyStaticShadowLights"), Is.EqualTo(2));
            fixture.NeedsRebuild();
            Assert.That(fixture.Get<int>("dirtyShadowLights"), Is.EqualTo(2));
            fixture.Commit();
            Assert.That(fixture.NeedsRebuild(), Is.False);
        }

        [Test]
        public void CameraCoverageRefillsKeepWorldShadowMappingUntilItNoLongerFits()
        {
            using var fixture = new ShadowCacheFixture();
            fixture.NeedsRebuild(); fixture.Commit();
            var build = typeof(DungeonSilhouetteLighting).GetMethod("BuildShadowBounds", BindingFlags.NonPublic | BindingFlags.Instance);
            Rect Bounds() => (Rect)build.Invoke(fixture.Lighting, new object[] { 1024 });
            Assert.That(Bounds(), Is.EqualTo(fixture.Bounds));
            fixture.Set("cameraGroundBounds", new Rect(-8, -9, 20, 20));
            Assert.That(Bounds(), Is.EqualTo(fixture.Bounds), "A camera refill must not move an already adequate world mask.");
            fixture.Set("cameraGroundBounds", new Rect(-2, -9, 20, 20));
            var shifted = Bounds();
            Assert.That(shifted, Is.Not.EqualTo(fixture.Bounds));
            Assert.That(shifted.xMin, Is.LessThan(-2)); Assert.That(shifted.xMax, Is.GreaterThan(18));
            Assert.That(shifted.xMin / (shifted.width / 1024), Is.EqualTo(Mathf.Round(shifted.xMin / (shifted.width / 1024))));
            fixture.Set("cameraGroundBounds", new Rect(-20, -20, 40, 40));
            Assert.That(Bounds().width, Is.EqualTo(44), "Zoom still changes the mask span and invalidates every slice.");
        }

        [Test]
        public void LocalSliceUpdatesMatchFullGpuRedrawAcrossMovementHideAndPropChanges()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null
                || (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
                Assert.Ignore("Requires a graphics device with texture copies.");
            using var fixture = new ShadowCacheFixture();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
            try
            {
                fixture.Bounds = new Rect(-10, -10, 20, 20);
                fixture.SetLights(new Vector4(0, 1, 2, 1), new Vector4(6, 1, 2, 1), new Vector4(-6, 1, 2, 1));
                fixture.Renderer.sprite = sprite;
                var prop = fixture.AddStaticCaster(); prop.sprite = sprite; prop.transform.position = Vector3.left * 6;
                var other = fixture.AddStaticCaster(); other.sprite = sprite; other.transform.position = new Vector3(6, 1.5f);
                fixture.Set("mask", MakeMask("Partial combined mask"));
                fixture.Set("staticMask", MakeMask("Partial static mask"));
                fixture.Set("projection", new Material(Shader.Find("Godmist/Dungeon/Animated Silhouette Volume Mask")));
                fixture.Set("commands", new CommandBuffer { name = "Partial shadow slices pixel test" });
                fixture.Set("properties", new MaterialPropertyBlock());
                var rebuild = typeof(DungeonSilhouetteLighting).GetMethod("RebuildShadowMasks", BindingFlags.NonPublic | BindingFlags.Instance);
                var drawCounts = new System.Collections.Generic.List<string> { "combinedSlices,staticSlices,partialDraws,fullReferenceDraws" };
                void Compare(int expectedSlices, int expectedStaticSlices)
                {
                    Assert.That(fixture.NeedsRebuild(), Is.True);
                    fixture.Set("bounds", fixture.Bounds);
                    rebuild.Invoke(fixture.Lighting, new object[] { fixture.Bounds.width / 128 });
                    Assert.That(fixture.Lighting.LastShadowSliceCount, Is.EqualTo(expectedSlices));
                    Assert.That(fixture.Lighting.LastStaticShadowSliceCount, Is.EqualTo(expectedStaticSlices));
                    var partialDraws = fixture.Lighting.LastShadowDrawCount;
                    var target = fixture.Get<RenderTexture>("mask");
                    var pixels = new[] { ReadMask(target, 0), ReadMask(target, 1), ReadMask(target, 2) };
                    var staticTarget = fixture.Get<RenderTexture>("staticMask");
                    try
                    {
                        fixture.Set("staticMask", null);
                        fixture.Set("shadowCacheReady", false);
                        fixture.NeedsRebuild();
                        rebuild.Invoke(fixture.Lighting, new object[] { fixture.Bounds.width / 128 });
                        for (int light = 0; light < 3; light++) CollectionAssert.AreEqual(ReadMask(target, light), pixels[light]);
                        drawCounts.Add(expectedSlices + "," + expectedStaticSlices + "," + partialDraws + "," + fixture.Lighting.LastShadowDrawCount);
                    }
                    finally { fixture.Set("staticMask", staticTarget); }
                }
                Compare(3, 3);
                for (int light = 0; light < 3; light++)
                    Assert.That(Array.Exists(ReadMask(fixture.Get<RenderTexture>("mask"), light), pixel => pixel.r > 0), Is.True);
                fixture.Renderer.transform.position += Vector3.right * .3f;
                Compare(1, 0);
                fixture.Renderer.transform.position = Vector3.right * 6;
                Compare(2, 0);
                fixture.Renderer.forceRenderingOff = true;
                fixture.Set("revision", fixture.Get<int>("revision") + 1);
                fixture.Touch(prop, false); fixture.Touch(other, false); fixture.Touch(fixture.Renderer);
                typeof(DungeonSilhouetteLighting).GetMethod("PruneCasters", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(fixture.Lighting, null);
                Compare(1, 0);
                prop.transform.position += Vector3.up * .3f;
                Compare(1, 1);
                fixture.Get<Vector4[]>("lightData")[1].x += .4f;
                Compare(1, 1);
                var sources = fixture.Get<Vector4[]>("lightData");
                (sources[0], sources[1]) = (sources[1], sources[0]);
                Compare(2, 2);
                var output = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../LocalHandoffs/shadow-fog-pass-20261017"));
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(output, "shadow-slice-comparison.csv"), drawCounts);
            }
            finally { Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void MovingAnimatedActorReusesStaticBaseUntilPropOrDoorChanges()
        {
            using var fixture = new ShadowCacheFixture();
            var prop = fixture.AddStaticCaster();
            fixture.NeedsRebuild(); fixture.Commit();
            for (int frame = 1; frame <= 40; frame++)
            {
                fixture.Renderer.transform.position = Vector3.right * (frame % 5) * .1f;
                fixture.Renderer.sprite = frame % 2 == 0 ? fixture.FirstSprite : fixture.SecondSprite;
                Assert.That(fixture.NeedsRebuild(), Is.True);
                Assert.That(fixture.Get<bool>("staticShadowCacheReady"), Is.True, "Actor changes must only rebuild the combined mask.");
                fixture.Commit();
            }
            prop.transform.position += Vector3.up;
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<bool>("staticShadowCacheReady"), Is.False, "Moving/opening a prop or door invalidates the base.");
            fixture.Commit();
            Assert.That(fixture.NeedsRebuild(), Is.False);
        }

        [Test]
        public void HiddenStaticPropAndCasterRoleChangesInvalidateStaticBase()
        {
            using var fixture = new ShadowCacheFixture();
            var prop = fixture.AddStaticCaster();
            fixture.NeedsRebuild(); fixture.Commit();
            prop.forceRenderingOff = true;
            fixture.Set("revision", fixture.Get<int>("revision") + 1);
            fixture.Touch(fixture.Renderer); fixture.Touch(prop, false);
            typeof(DungeonSilhouetteLighting).GetMethod("PruneCasters", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(fixture.Lighting, null);
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<bool>("staticShadowCacheReady"), Is.False, "Remove hidden static silhouettes from the base.");
            fixture.Commit();
            fixture.Touch(fixture.Renderer, false);
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<bool>("staticShadowCacheReady"), Is.False);
            fixture.Commit();
            fixture.Touch(fixture.Renderer, true);
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<bool>("staticShadowCacheReady"), Is.False, "Moving a caster out of the static role must remove its old silhouette.");
        }

        [TestCase("bounds")]
        [TestCase("lightPosition")]
        [TestCase("lightRadius")]
        [TestCase("lightHeight")]
        [TestCase("lightCount")]
        public void SharedProjectionChangesInvalidateStaticAndActorMasks(string change)
        {
            using var fixture = new ShadowCacheFixture();
            fixture.AddStaticCaster(); fixture.NeedsRebuild(); fixture.Commit();
            switch (change)
            {
                case "bounds": fixture.Bounds = new Rect(-20, -20, 40, 40); break;
                case "lightPosition": fixture.Get<Vector4[]>("lightData")[0].x++; break;
                case "lightRadius": fixture.Get<Vector4[]>("lightData")[0].z++; break;
                case "lightHeight": fixture.Set("lightHeight", 5f); break;
                case "lightCount": fixture.Get<IList>("activeTorches").Clear(); break;
            }
            Assert.That(fixture.NeedsRebuild(), Is.True);
            Assert.That(fixture.Get<bool>("staticShadowCacheReady"), Is.False);
        }

        [Test]
        public void CachedStaticPlusActorMaskMatchesFullRedrawPixelsWithoutGhostShadows()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null
                || (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
                Assert.Ignore("Requires a graphics device supporting texture copies; run this test without -nographics.");
            using var fixture = new ShadowCacheFixture();
            var shader = Shader.Find("Godmist/Dungeon/Animated Silhouette Volume Mask");
            Assert.That(shader != null && shader.isSupported, Is.True);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
            try
            {
                fixture.Bounds = new Rect(-4, -4, 8, 8);
                fixture.Renderer.sprite = sprite;
                fixture.Renderer.transform.position = new Vector3(.7f, 0);
                var prop = fixture.AddStaticCaster(); prop.sprite = sprite;
                prop.transform.position = new Vector3(-.7f, 0);
                fixture.Set("mask", MakeMask("Combined test mask"));
                fixture.Set("staticMask", MakeMask("Static test mask"));
                fixture.Set("projection", new Material(shader));
                fixture.Set("commands", new CommandBuffer { name = "Static shadow pixel test" });
                fixture.Set("properties", new MaterialPropertyBlock());
                var rebuild = typeof(DungeonSilhouetteLighting).GetMethod("RebuildShadowMasks", BindingFlags.NonPublic | BindingFlags.Instance);
                void CompareWithFullRedraw()
                {
                    Assert.That(fixture.NeedsRebuild(), Is.True);
                    fixture.Set("bounds", fixture.Bounds);
                    rebuild.Invoke(fixture.Lighting, new object[] { fixture.Bounds.width / 128 });
                    var cached = ReadMask(fixture.Get<RenderTexture>("mask"));
                    var staticTarget = fixture.Get<RenderTexture>("staticMask");
                    try
                    {
                        fixture.Set("staticMask", null); // Original full-render fallback is the reference.
                        fixture.Set("shadowCacheReady", false);
                        fixture.NeedsRebuild();
                        rebuild.Invoke(fixture.Lighting, new object[] { fixture.Bounds.width / 128 });
                        CollectionAssert.AreEqual(ReadMask(fixture.Get<RenderTexture>("mask")), cached);
                    }
                    finally { fixture.Set("staticMask", staticTarget); }
                }
                CompareWithFullRedraw();
                Assert.That(Array.Exists(ReadMask(fixture.Get<RenderTexture>("mask")), pixel => pixel.r > 0), Is.True,
                    "A blank mask must not pass the pixel-equivalence check.");
                Assert.That(fixture.Lighting.StaticShadowMaskRebuildCount, Is.EqualTo(1));
                fixture.Renderer.transform.position += Vector3.up * .5f;
                CompareWithFullRedraw();
                Assert.That(fixture.Lighting.StaticShadowMaskRebuildCount, Is.EqualTo(1));
                fixture.Renderer.forceRenderingOff = true;
                fixture.Set("revision", fixture.Get<int>("revision") + 1);
                fixture.Touch(prop, false); fixture.Touch(fixture.Renderer);
                typeof(DungeonSilhouetteLighting).GetMethod("PruneCasters", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(fixture.Lighting, null);
                CompareWithFullRedraw();
                Assert.That(fixture.Lighting.StaticShadowMaskRebuildCount, Is.EqualTo(1));
                prop.transform.position += Vector3.up * .5f;
                CompareWithFullRedraw();
                Assert.That(fixture.Lighting.StaticShadowMaskRebuildCount, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(sprite); }
        }

        private static RenderTexture MakeMask(string name)
        {
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = name, dimension = TextureDimension.Tex2DArray, volumeDepth = 8, filterMode = FilterMode.Point };
            Assert.That(target.Create(), Is.True); return target;
        }

        private static Color32[] ReadMask(RenderTexture mask, int light = 0)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.SetRenderTarget(mask, 0, CubemapFace.Unknown, light);
                pixels.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0); pixels.Apply();
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
        }

        [TestCase("renderer")]
        [TestCase("fog")]
        [TestCase("inactive")]
        [TestCase("transparent")]
        public void HiddenCastersArePrunedAndTheirOldShadowsMustBeCleared(string visibility)
        {
            using var fixture = new ShadowCacheFixture();
            fixture.NeedsRebuild(); fixture.Commit();
            switch (visibility)
            {
                case "renderer": fixture.Renderer.enabled = false; break;
                case "fog": fixture.Renderer.forceRenderingOff = true; break;
                case "inactive": fixture.Renderer.gameObject.SetActive(false); break;
                case "transparent": fixture.Renderer.color = Color.clear; break;
            }
            fixture.Set("revision", fixture.Get<int>("revision") + 1);
            fixture.Touch(fixture.Renderer);
            typeof(DungeonSilhouetteLighting).GetMethod("PruneCasters", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(fixture.Lighting, null);
            Assert.That(fixture.Get<IDictionary>("entries").Count, Is.Zero);
            Assert.That(fixture.NeedsRebuild(), Is.True, "Retaining the old mask would leave a ghost shadow.");
            fixture.Commit();
            Assert.That(fixture.NeedsRebuild(), Is.False);
        }

        [Test]
        public void TorchLightSamplesRemainStableForTwoHundredMilliseconds()
        {
            var first = DungeonSilhouetteLighting.SampleTorchTime(.251f);
            Assert.That(DungeonSilhouetteLighting.SampleTorchTime(.399f), Is.EqualTo(first));
            Assert.That(DungeonSilhouetteLighting.SampleTorchTime(.401f), Is.GreaterThan(first));
            Assert.That(DungeonSilhouetteLighting.TorchIntensity(first, 1.7f),
                Is.EqualTo(DungeonSilhouetteLighting.TorchIntensity(DungeonSilhouetteLighting.SampleTorchTime(.399f), 1.7f)));
        }

        [Test]
        public void LightingReusesPreparedRegionButMapAndNewPresentationChangesAreImmediate()
        {
            var root = new GameObject("lighting cadence");
            try
            {
                var lighting = root.AddComponent<DungeonSilhouetteLighting>();
                var method = typeof(DungeonSilhouetteLighting).GetMethod("ShouldRefreshLighting", BindingFlags.NonPublic | BindingFlags.Instance);
                var viewport = new Rect(-8, -5, 16, 10); int revision = 1;
                bool Refresh(float time) => (bool)method.Invoke(lighting, new object[] { time, viewport, revision });
                Assert.That(Refresh(.251f), Is.True);
                Assert.That(Refresh(.26f), Is.False); Assert.That(Refresh(.399f), Is.False);
                Assert.That(Refresh(.401f), Is.True); Assert.That(Refresh(.41f), Is.False);
                viewport.x += 1; Assert.That(Refresh(.41f), Is.False, "Panning within cached coverage must reuse the mask.");
                viewport = new Rect(-9, -6, 18, 12);
                Assert.That(Refresh(.41f), Is.False, "A small zoom within cached coverage also reuses it.");
                revision++; Assert.That(Refresh(.41f), Is.True); Assert.That(Refresh(.41f), Is.False);
                var renderer = root.AddComponent<SpriteRenderer>();
                lighting.RegisterGroundPresentation(renderer); Assert.That(Refresh(.41f), Is.True);
                lighting.RegisterGroundPresentation(renderer); Assert.That(Refresh(.41f), Is.False);
                typeof(DungeonSilhouetteLighting).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lighting, null);
                Assert.That(Refresh(.41f), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SmallPanAcrossLightingTicksPreservesPreparedCoverageAndShadowMapping()
        {
            var root = new GameObject("lighting retained coverage");
            try
            {
                var lighting = root.AddComponent<DungeonSilhouetteLighting>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var refresh = typeof(DungeonSilhouetteLighting).GetMethod("ShouldRefreshLighting", flags);
                var prepared = typeof(DungeonSilhouetteLighting).GetField("cameraGroundBounds", flags);
                var shadowBounds = typeof(DungeonSilhouetteLighting).GetMethod("BuildShadowBounds", flags);
                var viewport = new Rect(-8, -5, 16, 10);
                Assert.That(refresh.Invoke(lighting, new object[] { .251f, viewport, 1 }), Is.True);
                var coverage = (Rect)prepared.GetValue(lighting);
                var mask = (Rect)shadowBounds.Invoke(lighting, new object[] { 1024 });
                for (var tick = 2; tick <= 5; tick++)
                {
                    viewport.x += .1f;
                    Assert.That(refresh.Invoke(lighting, new object[] { tick * .2f + .01f, viewport, 1 }), Is.True,
                        "Light flicker and dynamic actors still refresh at 5 Hz.");
                    Assert.That(prepared.GetValue(lighting), Is.EqualTo(coverage), "Small pans must not invalidate static shadows every tick.");
                    Assert.That(shadowBounds.Invoke(lighting, new object[] { 1024 }), Is.EqualTo(mask));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ContinuousPanAtSixtyFpsPreparesOnlyTenLightingFramesInTwoSeconds()
        {
            var root = new GameObject("lighting continuous pan");
            try
            {
                var lighting = root.AddComponent<DungeonSilhouetteLighting>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var refresh = typeof(DungeonSilhouetteLighting).GetMethod("ShouldRefreshLighting", flags);
                var shadowBounds = typeof(DungeonSilhouetteLighting).GetMethod("BuildShadowBounds", flags);
                var prepared = typeof(DungeonSilhouetteLighting).GetField("cameraGroundBounds", flags);
                int builds = 0, coverageBuilds = 0;
                Rect previousCoverage = default, previousMask = default;
                for (var frame = 0; frame < 120; frame++)
                {
                    var viewport = new Rect(-8 + frame * .05f, -5, 16, 10);
                    bool changed = (bool)refresh.Invoke(lighting, new object[] { (frame + .5f) / 60, viewport, 1 });
                    var coverage = (Rect)prepared.GetValue(lighting);
                    var mask = (Rect)shadowBounds.Invoke(lighting, new object[] { 1024 });
                    Assert.That(coverage.xMin, Is.LessThanOrEqualTo(viewport.xMin));
                    Assert.That(coverage.xMax, Is.GreaterThanOrEqualTo(viewport.xMax));
                    Assert.That(mask.xMin, Is.LessThan(coverage.xMin));
                    Assert.That(mask.xMax, Is.GreaterThan(coverage.xMax));
                    Assert.That(mask.yMin, Is.LessThan(coverage.yMin));
                    Assert.That(mask.yMax, Is.GreaterThan(coverage.yMax));
                    if (changed) builds++;
                    else
                    {
                        Assert.That(coverage, Is.EqualTo(previousCoverage));
                        Assert.That(mask, Is.EqualTo(previousMask), "Cached shadow UV mapping must not slide with the camera.");
                    }
                    if (coverage != previousCoverage) coverageBuilds++;
                    previousCoverage = coverage; previousMask = mask;
                }
                Assert.That(builds, Is.EqualTo(10), "Continuous camera motion must not bypass the 5 Hz cadence.");
                Assert.That(coverageBuilds, Is.LessThanOrEqualTo(5), "Ordinary panning must reuse coverage across lighting ticks instead of rebuilding static geometry at 5 Hz.");
                TestContext.WriteLine($"120 camera frames: {builds} lighting ticks, {coverageBuilds} coverage preparations.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(50f, -5f, 16f, 10f)] // Teleport
        [TestCase(-8f, 30f, 16f, 10f)] // Vertical jump
        [TestCase(-20f, -10f, 40f, 20f)] // Zoom out/aspect expansion
        public void LeavingPreparedCoverageRefreshesOnceWithoutWaitingForCadence(float x, float y, float width, float height)
        {
            var root = new GameObject("lighting coverage escape");
            try
            {
                var lighting = root.AddComponent<DungeonSilhouetteLighting>();
                var method = typeof(DungeonSilhouetteLighting).GetMethod("ShouldRefreshLighting", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(method.Invoke(lighting, new object[] { .251f, new Rect(-8, -5, 16, 10), 1 }), Is.True);
                var viewport = new Rect(x, y, width, height);
                Assert.That(method.Invoke(lighting, new object[] { .26f, viewport, 1 }), Is.True);
                Assert.That(method.Invoke(lighting, new object[] { .27f, viewport, 1 }), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
