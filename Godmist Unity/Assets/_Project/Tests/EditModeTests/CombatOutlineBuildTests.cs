using System.Linq;
using Godmist.Editor;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class CombatOutlineBuildTests
    {
        [Test]
        public void OutlineShaderHasAnExplicitPlayerResourceDependency()
        {
            var shader = CombatOutlineShader.Load();
            Assert.That(shader, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo(CombatOutlineBuildValidation.AssetPath));
            Assert.That(shader.name, Is.EqualTo("Godmist/Combat/Solid Sprite Silhouette"));
            Assert.That(shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.DoesNotThrow(CombatOutlineBuildValidation.ValidateResourceDependency);
        }

        [Test]
        public void HoverCreatesVisibleOutlinesWithThePlayerResourceShader()
        {
            var root = new GameObject("outline dependency test");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                var body = root.AddComponent<SpriteRenderer>(); body.sprite = sprite;
                var view = root.AddComponent<CombatUnitVisualStateView>();
                view.Refresh(null, false, true, false);
                var outlines = root.GetComponentsInChildren<SpriteRenderer>()
                    .Where(r => r.name == "Visual State Outline").ToArray();
                Assert.That(outlines.Length, Is.EqualTo(8));
                foreach (var outline in outlines)
                {
                    Assert.That(outline.enabled, Is.True);
                    Assert.That(outline.sharedMaterial.shader, Is.SameAs(CombatOutlineShader.Load()));
                    Assert.That(outline.sharedMaterial.HasProperty("_OutlineColor"), Is.True);
                }
                view.ClearHighlight();
                Assert.That(outlines.All(r => !r.enabled), Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

    }
}
