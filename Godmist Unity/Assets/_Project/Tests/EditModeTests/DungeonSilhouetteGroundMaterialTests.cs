using System;
using System.Reflection;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonSilhouetteGroundMaterialTests
    {
        [Test]
        public void GroundPresentationBindsDistinctTexturesAndRestoresPropertyBlocks()
        {
            var root = new GameObject("ground lighting test");
            var lighting = root.AddComponent<DungeonSilhouetteLighting>();
            var shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);

            var floorMaterial = new Material(shader);
            SetField(lighting, "floorMaterial", floorMaterial);
            var apply = typeof(DungeonSilhouetteLighting).GetMethod(
                "ApplyGroundLighting",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);

            var renderers = new SpriteRenderer[3];
            var sprites = new Sprite[4];
            var sourceTextures = new Texture2D[4];
            var originalTextures = new Texture2D[3];
            var originalMaterials = new Material[3];

            try
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    sourceTextures[index] = MakeTexture(index + 1);
                    sprites[index] = MakeSprite(sourceTextures[index]);
                    originalTextures[index] = MakeTexture(index + 11);
                    originalMaterials[index] = new Material(shader);

                    var item = new GameObject("ground item " + index);
                    item.transform.SetParent(root.transform, false);
                    var renderer = item.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[index];
                    renderer.sharedMaterial = originalMaterials[index];
                    var original = new MaterialPropertyBlock();
                    original.SetTexture("_MainTex", originalTextures[index]);
                    original.SetFloat("_GroundAuditMarker", index + .5f);
                    renderer.SetPropertyBlock(original);
                    renderers[index] = renderer;

                    apply.Invoke(lighting, new object[] { renderer, false, false });
                }

                for (var index = 0; index < renderers.Length; index++)
                {
                    var current = new MaterialPropertyBlock();
                    renderers[index].GetPropertyBlock(current);
                    Assert.That(current.GetTexture("_MainTex") == sourceTextures[index], Is.True);
                    Assert.That(current.GetFloat("_GroundAuditMarker"), Is.EqualTo(index + .5f));
                }

                sourceTextures[3] = MakeTexture(99);
                sprites[3] = MakeSprite(sourceTextures[3]);
                renderers[2].sprite = sprites[3];
                apply.Invoke(lighting, new object[] { renderers[2], false, false });
                var changed = new MaterialPropertyBlock();
                renderers[2].GetPropertyBlock(changed);
                Assert.That(changed.GetTexture("_MainTex") == sourceTextures[3], Is.True);

                typeof(DungeonSilhouetteLighting).GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(lighting, null);

                for (var index = 0; index < renderers.Length; index++)
                {
                    var restored = new MaterialPropertyBlock();
                    renderers[index].GetPropertyBlock(restored);
                    Assert.That(renderers[index].sharedMaterial == originalMaterials[index], Is.True);
                    Assert.That(restored.GetTexture("_MainTex") == originalTextures[index], Is.True);
                    Assert.That(restored.GetFloat("_GroundAuditMarker"), Is.EqualTo(index + .5f));
                }
            }
            finally
            {
                UnityObject.DestroyImmediate(root);
                foreach (var sprite in sprites)
                    if (sprite != null) UnityObject.DestroyImmediate(sprite);
                foreach (var texture in sourceTextures)
                    if (texture != null) UnityObject.DestroyImmediate(texture);
                foreach (var texture in originalTextures)
                    if (texture != null) UnityObject.DestroyImmediate(texture);
                foreach (var material in originalMaterials)
                    if (material != null) UnityObject.DestroyImmediate(material);
                UnityObject.DestroyImmediate(floorMaterial);
            }
        }

        private static Texture2D MakeTexture(int value)
        {
            var texture = new Texture2D(2, 2);
            var color = new Color(value / 100f, .2f, .3f, 1f);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static Sprite MakeSprite(Texture2D texture)
            => Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * .5f, 32f);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
    }
}
