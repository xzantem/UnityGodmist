using System.IO;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SkillIconRefreshTests
    {
        [Test, Explicit("Scoped icon authoring; requires user authorization and Edit Mode.")]
        public void ApplyApprovedIcons() => SkillIconRefreshSetup.Apply();

        [Test]
        public void DedicatedIconsAreDistinctAndUseNativePointImport()
        {
            var sprites = SkillIconRefreshSetup.Icons.Select(p =>
                AssetDatabase.LoadAssetAtPath<Sprite>(SkillIconRefreshSetup.Art + p)).ToArray();
            Assert.That(sprites.All(s => s != null));
            Assert.That(sprites.Distinct().Count(), Is.EqualTo(5));
            foreach (var sprite in sprites)
            {
                var path = AssetDatabase.GetAssetPath(sprite);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(32, 32)));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.GetDefaultPlatformTextureSettings().textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
            Assert.That(sprites.Select(s => System.Convert.ToBase64String(File.ReadAllBytes(AssetDatabase.GetAssetPath(s))))
                .Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void SkillAndTreeBindingsPreserveGrantedSkillReuseWithoutCollisions()
        {
            var brawler = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(SkillIconRefreshSetup.Brawler);
            var dancer = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(SkillIconRefreshSetup.Dancer);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(WarriorDischargeSetup.Path);
            var discharge = brawler.Nodes.Single(n => n.Id == "node.warrior.brawler.bleeding_destruction");
            Assert.That(discharge.GrantedSkill, Is.SameAs(skill));
            Assert.That(discharge.Icon, Is.SameAs(skill.Icon));
            Assert.That(AssetDatabase.GetAssetPath(skill.Icon), Is.EqualTo(WarriorDischargeSetup.IconPath));
            Assert.That(skill.Icon, Is.Not.SameAs(brawler.Nodes.Single(n => n.Id == "node.warrior.brawler.release_rage").Icon));
            var recovery = dancer.Nodes.Single(n => n.Id == "node.scout.blade_dancer.efficient_recovery");
            var hook = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutHook.asset");
            Assert.That(recovery.Icon, Is.Not.SameAs(hook.Icon));
            Assert.That(AssetDatabase.GetAssetPath(recovery.Icon), Is.EqualTo(SkillIconRefreshSetup.Art + SkillIconRefreshSetup.Icons[4]));
            Assert.DoesNotThrow(VS22WarriorBrawlerPassiveSetup.Verify);
            Assert.DoesNotThrow(VS22RemainingPassiveSetup.VerifyAllProduction);
        }
    }
}
