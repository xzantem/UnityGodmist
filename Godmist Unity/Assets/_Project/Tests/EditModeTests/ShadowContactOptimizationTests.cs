using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ShadowContactOptimizationTests
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/shadow-contact-pass-20261017"));

        private sealed class RigFixture : IDisposable
        {
            private readonly IDisposable fixture;
            public readonly DungeonSilhouetteLighting Rig;
            public readonly SpriteRenderer Actor, Prop;
            public readonly Texture2D Texture = new(16, 16, TextureFormat.RGBA32, false, true);
            public readonly Sprite First, Second;
            public readonly CommandBuffer LegacyCommands = new();
            public readonly RenderTexture Reference = MakeMask();
            public readonly Action<float> Rebuild;
            public readonly Action<Matrix4x4> Prepare;
            public readonly Action<RenderTexture, Matrix4x4, float, bool?, bool, int> Record;
            private readonly Func<Sprite, bool, DungeonSilhouetteLighting.ContactProfile> Contact;
            private readonly Func<Sprite, Mesh> Mesh;
            private readonly MaterialPropertyBlock block = new();
            public Rect Bounds = new(-4, -4, 8, 8);
            public RigFixture()
            {
                var type = typeof(DungeonLightingCadenceTests).GetNestedType("ShadowCacheFixture", BindingFlags.NonPublic);
                var instance = Activator.CreateInstance(type, true); fixture = (IDisposable)instance;
                Rig = Read<DungeonSilhouetteLighting>(instance, "Lighting"); Actor = Read<SpriteRenderer>(instance, "Renderer");
                Prop = (SpriteRenderer)type.GetMethod("AddStaticCaster").Invoke(instance, null);
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    Texture.SetPixel(x, y, x < 11 && y > 1 && (x > 2 || y < 10) ? Color.white : Color.clear);
                Texture.Apply(); Texture.filterMode = FilterMode.Point;
                First = Sprite.Create(Texture, new Rect(0, 0, 16, 16), new Vector2(.4f, .1f), 16);
                Second = Sprite.Create(Texture, new Rect(0, 0, 8, 16), new Vector2(.4f, .1f), 16);
                Actor.sprite = Prop.sprite = First; Actor.transform.position = new Vector3(.7f, 0); Prop.transform.position = new Vector3(-.7f, 0);
                type.GetMethod("SetLights").Invoke(instance, new object[] { new[] {
                    new Vector4(1, 2, 10, 1), new Vector4(-2, 1, 10, 1), new Vector4(0, -2, 10, 1) } });
                Set(Rig, "mask", MakeMask()); Set(Rig, "staticMask", null);
                Set(Rig, "projection", new Material(Shader.Find("Godmist/Dungeon/Animated Silhouette Volume Mask")));
                Set(Rig, "commands", new CommandBuffer()); Set(Rig, "properties", new MaterialPropertyBlock());
                Rebuild = Bind<Action<float>>(Rig, "RebuildShadowMasks"); Prepare = Bind<Action<Matrix4x4>>(Rig, "PrepareShadowCasters");
                Record = Bind<Action<RenderTexture, Matrix4x4, float, bool?, bool, int>>(Rig, "RecordShadowCasters");
                Contact = Bind<Func<Sprite, bool, DungeonSilhouetteLighting.ContactProfile>>(Rig, "ReadContactProfile");
                Mesh = Bind<Func<Sprite, Mesh>>(Rig, "GetMesh");
            }
            public Matrix4x4 VP => GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(Bounds.xMin, Bounds.xMax, Bounds.yMin, Bounds.yMax, -1, 1), true);
            public void RenderNew()
            {
                Set(Rig, "bounds", Bounds); Set(Rig, "dirtyShadowLights", 7); Set(Rig, "dirtyStaticShadowLights", 7);
                Rebuild(Bounds.width / 128);
            }
            public void LegacyRecord()
            {
                // Original non-instanced four-sample command recording kernel.
                LegacyCommands.Clear();
                var lights = Read<Vector4[]>(Rig, "lightData");
                var entries = Read<IDictionary>(Rig, "entries");
                var uvCache = Read<Dictionary<Sprite, Vector4>>(Rig, "spriteUvBounds");
                var projection = Read<Material>(Rig, "projection");
                float height = Read<float>(Rig, "lightHeight"), texel = Bounds.width / 128;
                var vp = VP;
                for (int light = 0; light < 3; light++)
                {
                    LegacyCommands.SetRenderTarget(Reference, 0, CubemapFace.Unknown, light);
                    LegacyCommands.ClearRenderTarget(false, true, Color.clear);
                    var source = lights[light]; var position = new Vector2(source.x, source.y);
                    foreach (DictionaryEntry pair in entries)
                    {
                        var renderer = (SpriteRenderer)pair.Key;
                        if (renderer == null || renderer.sprite == null || !renderer.enabled || renderer.forceRenderingOff
                            || !renderer.gameObject.activeInHierarchy || renderer.color.a <= 0
                            || renderer.bounds.SqrDistance(new Vector3(position.x, position.y, renderer.bounds.center.z)) > source.z * source.z) continue;
                        var sprite = renderer.sprite; var contact = Contact(sprite, Read<bool>(pair.Value, "actor"));
                        Vector2 scale = renderer.transform.lossyScale;
                        scale.x *= renderer.flipX ? -1 : 1; scale.y *= renderer.flipY ? -1 : 1;
                        var foot = (Vector2)renderer.transform.position + Vector2.Scale(contact.footLocal, scale)
                            - Vector2.up * Read<float>(pair.Value, "projectedElevation");
                        var away = foot - position; var direction = new Vector2(away.x, away.y / .75f);
                        direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
                        var longitudinal = new Vector2(direction.x, direction.y * .75f) * Mathf.Clamp(away.magnitude / height, .3f, 1.5f);
                        block.Clear(); block.SetTexture("_MainTex", sprite.texture); block.SetMatrix("_ShadowVP", vp);
                        block.SetVector("_FootLocal", contact.footLocal); block.SetVector("_Scale", scale); block.SetVector("_HeightBasis", longitudinal);
                        block.SetVector("_ContactLine", new Vector4(contact.slope, contact.offset, 0, 0));
                        float halfDepth = sprite.bounds.size.x * SilhouetteVolumeMesh.DepthToWidth * .5f;
                        block.SetFloat("_VolumeHalfDepth", halfDepth * Mathf.Abs(scale.x)); block.SetFloat("_DepthPixels", halfDepth * sprite.pixelsPerUnit);
                        if (!uvCache.TryGetValue(sprite, out var uv))
                        { var uvs = sprite.uv; uv = new Vector4(uvs.Min(p => p.x), uvs.Min(p => p.y), uvs.Max(p => p.x), uvs.Max(p => p.y)); uvCache.Add(sprite, uv); }
                        block.SetVector("_UVBounds", uv); var mesh = Mesh(sprite);
                        for (int i = 0; i < 4; i++)
                        {
                            block.SetVector("_Anchor", new Vector4(foot.x + ((i & 1) == 0 ? -.35f : .35f) * texel,
                                foot.y + ((i & 2) == 0 ? -.35f : .35f) * texel, 0, 0));
                            LegacyCommands.DrawMesh(mesh, Matrix4x4.identity, projection, 0, 0, block);
                        }
                    }
                }
            }
            public void Dispose()
            {
                LegacyCommands.Dispose(); Object.DestroyImmediate(Reference); fixture.Dispose();
                Object.DestroyImmediate(First); Object.DestroyImmediate(Second); Object.DestroyImmediate(Texture);
            }
        }

        [Test]
        public void PreparedMasksMatchOriginalFourSamplePixelsAcrossAllGeometricInputsAndLightMovement()
        {
            using var fixture = new RigFixture();
            var entry = Read<IDictionary>(fixture.Rig, "entries")[fixture.Actor];
            for (int step = 0; step < 12; step++)
            {
                switch (step)
                {
                    case 1: fixture.Actor.transform.localScale = new Vector3(-1.2f, .8f, 1); fixture.Actor.flipX = true; fixture.Actor.flipY = true; break;
                    case 2: fixture.Actor.transform.position += Vector3.up * .3f; break;
                    case 3: fixture.Actor.sprite = fixture.Second; break;
                    case 4: Set(fixture.Rig, "contacts", new[] { new DungeonSilhouetteLighting.ContactProfile { sprite = fixture.Second, footLocal = new Vector2(.1f, -.2f), slope = .2f, offset = .1f } }); break;
                    case 5: Set(entry, "actor", false); break;
                    case 6: Set(entry, "projectedElevation", .4f); break;
                    case 7: Set(fixture.Rig, "lightHeight", 4f); break;
                    case 8: Read<Vector4[]>(fixture.Rig, "lightData")[0] += new Vector4(.3f, -.2f, 0, 0); break;
                    case 9: fixture.Prop.forceRenderingOff = true; break;
                    case 10: Read<Vector4[]>(fixture.Rig, "lightData")[1].z = .1f; break;
                    case 11: fixture.Bounds.position += new Vector2(.25f, .125f); break;
                }
                fixture.RenderNew(); var actual = ReadMask(fixture.Rig.ShadowMask);
                fixture.LegacyRecord(); Graphics.ExecuteCommandBuffer(fixture.LegacyCommands);
                CollectionAssert.AreEqual(ReadMask(fixture.Reference), actual, "Legacy pixel mismatch at step " + step);
                if (step == 0) Assert.That(actual.Any(pixel => pixel.r > 0), Is.True, "A blank mask must not pass.");
            }
        }

        [Test]
        public void LightMovementReusesGeometryButSpriteRoleAndContactChangesInvalidateIt()
        {
            using var fixture = new RigFixture(); fixture.RenderNew();
            int count = fixture.Rig.ShadowGeometryPreparationCount;
            Assert.That(count, Is.EqualTo(2)); Assert.That(fixture.Rig.LastShadowDrawCount, Is.EqualTo(24));
            Read<Vector4[]>(fixture.Rig, "lightData")[0] += new Vector4(.1f, 0, 0, 0);
            fixture.RenderNew(); Assert.That(fixture.Rig.ShadowGeometryPreparationCount, Is.EqualTo(count));
            fixture.Bounds.position += new Vector2(.000001f, 0); fixture.RenderNew();
            var block = Read<MaterialPropertyBlock>(Read<IDictionary>(fixture.Rig, "entries")[fixture.Actor], "drawProperties");
            Assert.That(block.GetMatrix("_ShadowVP").Equals(fixture.VP), Is.True, "Even a tiny exact matrix change must update the projection.");
            fixture.Actor.sprite = fixture.Second; fixture.RenderNew();
            Assert.That(fixture.Rig.ShadowGeometryPreparationCount, Is.EqualTo(count + 1));
            Set(Read<IDictionary>(fixture.Rig, "entries")[fixture.Actor], "actor", false); fixture.RenderNew();
            Assert.That(fixture.Rig.ShadowGeometryPreparationCount, Is.EqualTo(count + 2));
        }

        [Test]
        public void MovingCastersReuseLocalPropertiesWhileKeepingExactLegacyMaskPixels()
        {
            using var fixture = new RigFixture(); fixture.RenderNew();
            int properties = fixture.Rig.ShadowPropertyPreparationCount;
            Assert.That(properties, Is.EqualTo(2));
            for (var i = 0; i < 12; i++)
            {
                fixture.Actor.transform.position += new Vector3(.0625f, -.03125f);
                fixture.Prop.transform.position += new Vector3(-.03125f, .0625f);
                fixture.RenderNew();
                Assert.That(fixture.Rig.ShadowPropertyPreparationCount, Is.EqualTo(properties), "Translation must update the anchor without rebuilding local sprite properties.");
                var actual = ReadMask(fixture.Rig.ShadowMask);
                fixture.LegacyRecord(); Graphics.ExecuteCommandBuffer(fixture.LegacyCommands);
                CollectionAssert.AreEqual(ReadMask(fixture.Reference), actual);
            }
            fixture.Actor.flipX = !fixture.Actor.flipX; fixture.RenderNew();
            Assert.That(fixture.Rig.ShadowPropertyPreparationCount, Is.EqualTo(++properties));
            fixture.Actor.transform.localScale = new Vector3(.8f, 1.2f, 1); fixture.RenderNew();
            Assert.That(fixture.Rig.ShadowPropertyPreparationCount, Is.EqualTo(++properties));
            fixture.Actor.sprite = fixture.Second; fixture.RenderNew();
            Assert.That(fixture.Rig.ShadowPropertyPreparationCount, Is.EqualTo(++properties));
            var changed = ReadMask(fixture.Rig.ShadowMask);
            fixture.LegacyRecord(); Graphics.ExecuteCommandBuffer(fixture.LegacyCommands);
            CollectionAssert.AreEqual(ReadMask(fixture.Reference), changed);
        }

        [Test]
        public void CleanStaticSlicesDoNotPrepareStaticCastersAndCopiedMasksMatchLegacy()
        {
            using var fixture = new RigFixture();
            var staticMask = new RenderTexture(fixture.Rig.ShadowMask.descriptor); staticMask.Create();
            Set(fixture.Rig, "staticMask", staticMask); // The rig owns/disposes this texture.
            fixture.RenderNew();
            int properties = fixture.Rig.ShadowPropertyPreparationCount;
            var entries = Read<IDictionary>(fixture.Rig, "entries");
            Set(entries[fixture.Actor], "drawStateReady", false);
            Set(entries[fixture.Prop], "drawStateReady", false);
            Set(fixture.Rig, "dirtyShadowLights", 1); Set(fixture.Rig, "dirtyStaticShadowLights", 0);
            fixture.Rebuild(fixture.Bounds.width / 128);
            Assert.That(fixture.Rig.ShadowPropertyPreparationCount, Is.EqualTo(properties + 1));
            Assert.That(Read<bool>(entries[fixture.Prop], "drawStateReady"), Is.False, "Clean static slices must retain the existing texture, without preparing their draw block.");
            var actual = ReadMask(fixture.Rig.ShadowMask);
            fixture.LegacyRecord(); Graphics.ExecuteCommandBuffer(fixture.LegacyCommands);
            CollectionAssert.AreEqual(ReadMask(fixture.Reference), actual);
        }

        [Test]
        public void CasterLightingSkipsIdenticalUploadsButRepairsReplacedBlocksAndKeepsForeignValues()
        {
            using var fixture = new RigFixture();
            var entry = Read<IDictionary>(fixture.Rig, "entries")[fixture.Actor];
            var method = typeof(DungeonSilhouetteLighting).GetMethod("ApplyCasterLighting", Fields);
            void Apply() => method.Invoke(fixture.Rig, new object[] { fixture.Actor, entry });
            var block = new MaterialPropertyBlock();
            Apply(); int uploads = fixture.Rig.CasterPropertyBlockUploadCount;
            fixture.Actor.GetPropertyBlock(block); block.SetFloat("_ForeignPulse", 17); fixture.Actor.SetPropertyBlock(block);
            for (var i = 0; i < 100; i++) Apply();
            Assert.That(fixture.Rig.CasterPropertyBlockUploadCount, Is.EqualTo(uploads));
            fixture.Actor.GetPropertyBlock(block); Assert.That(block.GetFloat("_ForeignPulse"), Is.EqualTo(17));
            fixture.Actor.transform.position += new Vector3(.125f, -.0625f); Apply();
            Assert.That(fixture.Rig.CasterPropertyBlockUploadCount, Is.EqualTo(++uploads));
            fixture.Actor.GetPropertyBlock(block); Assert.That(block.GetVector("_FootWorld"), Is.EqualTo((Vector4)fixture.Actor.transform.position));
            block.Clear(); block.SetFloat("_ForeignPulse", 23); fixture.Actor.SetPropertyBlock(block); Apply();
            Assert.That(fixture.Rig.CasterPropertyBlockUploadCount, Is.EqualTo(++uploads));
            fixture.Actor.GetPropertyBlock(block); Assert.That(block.GetFloat("_IsActor"), Is.EqualTo(1));
            Assert.That(block.GetFloat("_ForeignPulse"), Is.EqualTo(23));
            Set(entry, "actor", false); Set(entry, "projectedElevation", .3f); Apply();
            Assert.That(fixture.Rig.CasterPropertyBlockUploadCount, Is.EqualTo(++uploads));
            fixture.Actor.GetPropertyBlock(block); Assert.That(block.GetFloat("_IsActor"), Is.Zero);
            Assert.That(block.GetVector("_FootWorld"), Is.EqualTo((Vector4)(fixture.Actor.transform.position - Vector3.up * .3f)));
            Assert.That(block.GetFloat("_ForeignPulse"), Is.EqualTo(23));
        }

        [Test]
        public void ContactTemplatesMatchOriginalScanlineArithmeticAndRemainBounded()
        {
            using var fixture = new ContactFixture();
            var bands = Read<List<(Rect Rect, byte Opacity)>>(fixture.Contact, "bands");
            var add = Bind<Action<Vector2, float>>(fixture.Contact, "AddForestContact");
            for (int i = 0; i < 300; i++)
            {
                var foot = new Vector2(-4.3f + i * .071f, 3.7f - i * .053f); float artWidth = i % 4 == 0 ? 10 : .01f + i * .01f;
                bands.Clear(); add(foot, artWidth);
                CollectionAssert.AreEqual(LegacyBands(foot, artWidth), bands);
            }
            Assert.That(Read<IDictionary>(fixture.Contact, "forestBandTemplates").Count, Is.LessThanOrEqualTo(128));
        }

        [Test]
        public void ContactCellBatchMatchesOriginalClippingAndRequeriesAfterVisibilityChanges()
        {
            using var fixture = new ContactFixture();
            var bands = LegacyBands(new Vector2(2.6f, 2.1f), 2f);
            bands.AddRange(LegacyBands(new Vector2(2.8f, 2.2f), 2f));
            var emit = Bind<Action<Rect, byte>>(fixture.Contact, "Emit");
            var firstComparison = true;
            void Compare()
            {
                Read<IDictionary>(fixture.Contact, "contactCells").Clear();
                Read<HashSet<Rect>>(fixture.Contact, "projectedBands").Clear();
                Read<List<Vector3>>(fixture.Contact, "vertices").Clear(); Read<List<Color32>>(fixture.Contact, "colors").Clear();
                Read<List<int>>(fixture.Contact, "triangles").Clear(); Read<HashSet<Rect>>(fixture.Contact, "emitted").Clear();
                int previous = Property<int>(fixture.Contact, "CellVisibilityQueryCount");
                foreach (var band in bands) emit(band.Rect, band.Opacity);
                var expected = fixture.LegacyClip(bands);
                if (firstComparison) Assert.That(expected.Vertices.Count, Is.GreaterThan(0), "Clipping comparison must include visible geometry.");
                CollectionAssert.AreEqual(expected.Vertices, Read<List<Vector3>>(fixture.Contact, "vertices"));
                CollectionAssert.AreEqual(expected.Colors, Read<List<Color32>>(fixture.Contact, "colors"));
                Assert.That(Property<int>(fixture.Contact, "CellVisibilityQueryCount") - previous,
                    Is.EqualTo(Read<IDictionary>(fixture.Contact, "contactCells").Count));
                Assert.That(expected.Queries, Is.GreaterThan(Read<IDictionary>(fixture.Contact, "contactCells").Count * 2));
                if (firstComparison)
                {
                    Directory.CreateDirectory(Output);
                    File.WriteAllText(Path.Combine(Output, "contact-query-counts.json"), JsonUtility.ToJson(new ContactMetric {
                        legacyQueries = expected.Queries, bufferedQueries = Read<IDictionary>(fixture.Contact, "contactCells").Count,
                        bandCount = bands.Count, vertices = expected.Vertices.Count }, true));
                    firstComparison = false;
                }
            }
            Compare();
            fixture.Floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(2, 2))) });
            fixture.View.ApplyFog(fixture.Floor); Compare();
            fixture.Floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); fixture.View.ApplyFog(fixture.Floor); Compare();
        }

        private sealed class ContactFixture : IDisposable
        {
            public readonly GameObject Root = new("Contact cell fixture", typeof(Grid));
            public readonly DungeonFloorMapView View;
            public readonly DungeonFloorInstance Floor = new(0);
            public readonly object Contact;
            public ContactFixture()
            {
                var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); floor.transform.SetParent(Root.transform);
                View = Root.AddComponent<DungeonFloorMapView>(); Set(View, "floorTilemap", floor.GetComponent<Tilemap>());
                var map = new DungeonFloorMap(); Floor.TrySetMap(map);
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Set(View, "boundMap", map); Read<DungeonElevationViewState>(View, "elevation").Bind(map);
                var type = typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows", true);
                Contact = Activator.CreateInstance(type, new object[] { Root.transform, Shader.Find("Godmist/Dungeon/Contact Shadows") });
                Set(Contact, "terrain", View); Set(Contact, "viewport", new Rect(-1, -1, 12, 12));
            }
            public (List<Vector3> Vertices, List<Color32> Colors, int Queries) LegacyClip(List<(Rect Rect, byte Opacity)> bands)
            {
                var vertices = new List<Vector3>(); var colors = new List<Color32>(); var emitted = new HashSet<Rect>(); int queries = 0;
                foreach (var band in bands)
                {
                    float Snap(float value) => Mathf.Round(value * 32) / 32;
                    var r = band.Rect; r = Rect.MinMaxRect(Snap(r.xMin), Snap(r.yMin), Snap(r.xMax), Snap(r.yMax));
                    if (!new Rect(-1, -1, 12, 12).Overlaps(r)) continue;
                    var first = View.WorldToCell(new Vector3(r.xMin + .0001f, r.yMin + .0001f));
                    var last = View.WorldToCell(new Vector3(r.xMax - .0001f, r.yMax - .0001f));
                    for (int y = first.y; y <= last.y; y++) for (int x = first.x; x <= last.x; x++)
                    {
                        queries++; var center = View.CellToWorldCenter(new Vector2Int(x, y));
                        if (!View.TryGetVisibleTraversal(center, out var position) || !View.IsTraversalVisible(position)) continue;
                        var clipped = Rect.MinMaxRect(Mathf.Max(r.xMin, center.x - .5f), Mathf.Max(r.yMin, center.y - .5f), Mathf.Min(r.xMax, center.x + .5f), Mathf.Min(r.yMax, center.y + .5f));
                        if (clipped.width <= 0 || clipped.height <= 0 || !emitted.Add(clipped)) continue;
                        vertices.Add(new Vector3(clipped.xMin, clipped.yMin)); vertices.Add(new Vector3(clipped.xMax, clipped.yMin));
                        vertices.Add(new Vector3(clipped.xMax, clipped.yMax)); vertices.Add(new Vector3(clipped.xMin, clipped.yMax));
                        for (int i = 0; i < 4; i++) colors.Add(new Color32(0, 0, 0, band.Opacity));
                    }
                }
                return (vertices, colors, queries);
            }
            public void Dispose()
            {
                try { Bind<Action>(Contact, "Dispose")(); }
                finally { Object.DestroyImmediate(Root); }
            }
        }

        private static List<(Rect Rect, byte Opacity)> LegacyBands(Vector2 foot, float artWidth)
        {
            const float pixel = 1f / 32; var result = new List<(Rect, byte)>(); float width = Mathf.Clamp(artWidth * .72f, .25f, 2.5f);
            for (int layer = 0; layer < 4; layer++)
            {
                var spread = layer * 2 * pixel; var halfWidth = width * .5f + spread; var halfHeight = .12f + spread * .65f; var rows = Mathf.CeilToInt(halfHeight / pixel);
                for (int row = -rows; row < rows; row++)
                {
                    var y = (row + .5f) * pixel; var normalized = y / halfHeight; if (Mathf.Abs(normalized) >= 1) continue;
                    var x = halfWidth * Mathf.Sqrt(1 - normalized * normalized);
                    result.Add((new Rect(foot.x - x, foot.y + row * pixel, 2 * x, pixel), (byte)(layer == 0 ? 72 : 13)));
                }
            }
            return result;
        }
        private static RenderTexture MakeMask()
        {
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { dimension = TextureDimension.Tex2DArray, volumeDepth = 8, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            target.Create(); return target;
        }
        private static Color32[] ReadMask(RenderTexture mask)
        {
            var result = new List<Color32>(); var previous = RenderTexture.active;
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            try
            {
                for (int slice = 0; slice < 3; slice++)
                {
                    Graphics.SetRenderTarget(mask, 0, CubemapFace.Unknown, slice); image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                    result.AddRange(image.GetPixels32());
                }
                return result.ToArray();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static T Bind<T>(object owner, string method) where T : Delegate => (T)owner.GetType().GetMethod(method, Fields).CreateDelegate(typeof(T), owner);
        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Fields).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Fields).SetValue(owner, value);
        private static T Property<T>(object owner, string property) => (T)owner.GetType().GetProperty(property).GetValue(owner);
        [Serializable] private sealed class ContactMetric { public int legacyQueries, bufferedQueries, bandCount, vertices; }
    }
}
