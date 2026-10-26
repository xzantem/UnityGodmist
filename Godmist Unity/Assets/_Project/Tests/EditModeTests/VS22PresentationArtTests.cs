using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.NPC;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class VS22PresentationArtTests
    {
        private const string JerzyPath = "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset";
        private const string WladimirPath = "Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset";
        private const string MagePath = "Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset";
        private const string PaladinPath = "Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset";
        private const string EnemyPath = "Assets/_Project/ScriptableObjects/Characters/CD_Enemy_Test.asset";
        private const string SkeletonTestPath = "Assets/_Project/ScriptableObjects/Characters/CD_Skeleton_Test.asset";
        private const string BossTestPath = "Assets/_Project/ScriptableObjects/Characters/CD_Boss_Test.asset";
        private const string CatacombsRoot = "Assets/_Project/ScriptableObjects/Catacombs/Enemies/";
        private const string BlacksmithPath = "Assets/_Project/ScriptableObjects/NPC/ND_Arungard_Blacksmith.asset";
        private const string GlossaryPath = "Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset";
        private const string ManifestRelativePath = "skills-complete/skills-complete-manifest.json";
        private const string SkillArtRoot = "Assets/_Project/Art/Icons/Skills/VS22/";

        private static readonly Dictionary<string, string> ExistingSkillTargets =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["skill.warrior.cut"] = "cut-b-v2-32.png",
                ["skill.scout.poisoned_dagger"] = "poisoned-dagger-b-v2-32.png",
                ["skill.mage.shield"] = "magic-shield-b-v2-32.png",
                ["skill.paladin.group_heal"] = "group-heal-b-v2-32.png"
             };

        private static readonly Dictionary<string, int> ApprovedAlphaMinY =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["jerzy-front-64-v1.png"] = 4,
                ["wladimir-front-64-v1.png"] = 4,
                ["alison-front-64-v2.png"] = 3,
                ["reimond-front-64-v1.png"] = 4,
                ["skeleton-swordsman-idle-64.png"] = 4,
                ["skeleton-archer-idle-64.png"] = 4,
                ["skeleton-defender-idle-64.png"] = 4,
                ["skeleton-occultist-idle-64.png"] = 4,
                ["undead-fleshy-idle-64.png"] = 2,
                ["ghoul-quadruped-idle-64.png"] = 2,
                ["wraith-idle-64.png"] = 4,
                ["dead-king-idle-128.png"] = 1,
                ["blacksmith-64-v1.png"] = 3
            };

        [Serializable]
        private sealed class SkillManifest
        {
            public SkillManifestEntry[] entries;
        }

        [Serializable]
        private sealed class SkillManifestEntry
        {
            public string skillId;
            public string assetPath;
            public string image;
            public string file;
            public string group;
        }

        [Test]
        public void CharacterPortraitsUseDedicatedSpriteAndKeepFallbackForUnassignedCharacters()
        {
            var jerzy = LoadCharacter(JerzyPath, "jerzy");
            var enemy = LoadCharacter(EnemyPath, "enemy.catacombs.skeleton_swordsman");
            var wladimir = LoadCharacter(WladimirPath, "wladimir");
            var mage = LoadCharacter(MagePath, "character.mage");
            var paladin = LoadCharacter(PaladinPath, "character.paladin");

            Assert.That(jerzy.VisualSprite, Is.Not.Null);
            Assert.That(jerzy.PortraitSprite, Is.Not.Null);
            Assert.That(jerzy.UiPortraitSprite, Is.SameAs(jerzy.PortraitSprite));
            Assert.That(enemy.PortraitSprite, Is.Not.Null);
            Assert.That(enemy.UiPortraitSprite, Is.SameAs(enemy.PortraitSprite));

            foreach (var character in new[] { wladimir, mage, paladin })
            {
                Assert.That(character.PortraitSprite, Is.Not.Null, character.Id);
                Assert.That(character.UiPortraitSprite, Is.SameAs(character.PortraitSprite), character.Id);
                Assert.That(character.PortraitSprite, Is.Not.SameAs(character.VisualSprite), character.Id);
            }
            var unassigned = LoadCharacter(SkeletonTestPath, "skeleton.test");
            Assert.That(unassigned.PortraitSprite, Is.Null);
            Assert.That(unassigned.UiPortraitSprite, Is.SameAs(unassigned.VisualSprite));
        }

        [Test]
        public void CharacterInfoWindowUsesCurrentFrontIdleWithPreservedAspect()
        {
            var workspace = new GameObject("VS22 Character Info Test", typeof(RectTransform));
            workspace.AddComponent<UIWorkspaceWindowManager>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab");
            Assert.That(prefab, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(prefab, workspace.transform);
            var view = instance.GetComponent<CharacterInfoWindowView>();
            Assert.That(view, Is.Not.Null);

            var portraitField = typeof(CharacterInfoWindowView).GetField(
                "portrait",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var portrait = portraitField?.GetValue(view) as Image;
            Assert.That(portrait, Is.Not.Null);
            portrait.preserveAspect = false;

            var definition = LoadCharacter(JerzyPath, "jerzy");
            var character = new CharacterInstance
            {
                CharacterId = "vs22.ui.jerzy",
                Definition = definition,
                Level = 1,
                Stats = new CharacterStats(),
                Resistances = new ResistanceSet(),
                CurrentHealth = 1
            };
            var session = new GameSession(GameModeState.Exploration, "vs22-ui");
            Assert.That(session.TryRegisterCharacter(character), Is.True);

            try
            {
                view.Initialize(session, character.CharacterId);

                Assert.That(Godmist.Views.Characters.CharacterAnimationResolver.TryResolveCharacter(definition.Id,
                    Vector2.down, Godmist.Views.Characters.CharacterAnimationState.Idle, out var idle), Is.True);
                Assert.That(portrait.sprite, Is.SameAs(idle.Frames[0]));
                Assert.That(portrait.sprite, Is.Not.SameAs(definition.PortraitSprite));
                Assert.That(portrait.preserveAspect, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(workspace);
            }
        }

        [Test]
        public void AllSelectedCharacterDefinitionsHaveExactStaticBindings()
        {
            AssertStaticCharacter(JerzyPath, "jerzy", "jerzy-front-64-v1", "jerzy-combat-portrait-128");
            AssertStaticCharacter(WladimirPath, "wladimir", "wladimir-front-64-v1", "wladimir-combat-portrait-128");
            AssertStaticCharacter(MagePath, "character.mage", "alison-front-64-v2", "alison-combat-portrait-128");
            AssertStaticCharacter(PaladinPath, "character.paladin", "reimond-front-64-v1", "reimond-combat-portrait-128");
            AssertStaticCharacter(EnemyPath, "enemy.catacombs.skeleton_swordsman", "skeleton-swordsman-idle-64", "skeleton-swordsman-combat-portrait-128");
            AssertStaticCharacter(SkeletonTestPath, "skeleton.test", "skeleton-swordsman-idle-64");
            AssertStaticCharacter(BossTestPath, "boss.test", "dead-king-idle-128", "deadking-combat-portrait-128");

            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_swordsman.asset", "enemy.catacombs.swordsman", "skeleton-swordsman-idle-64", "skeleton-swordsman-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_archer.asset", "enemy.catacombs.archer", "skeleton-archer-idle-64", "archer-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_defender.asset", "enemy.catacombs.defender", "skeleton-defender-idle-64", "defender-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_occultist.asset", "enemy.catacombs.occultist", "skeleton-occultist-idle-64", "occultist-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_undead.asset", "enemy.catacombs.undead", "undead-fleshy-idle-64", "undead-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_ghoul.asset", "enemy.catacombs.ghoul", "ghoul-quadruped-idle-64", "ghoul-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_wraith.asset", "enemy.catacombs.wraith", "wraith-idle-64", "wraith-combat-portrait-128");
            AssertStaticCharacter(CatacombsRoot + "CD_Catacombs_dead_king.asset", "enemy.catacombs.dead_king", "dead-king-idle-128", "deadking-combat-portrait-128");
        }

        [Test]
        public void AllManifestSkillsHaveExactIdsIconsAndImportProfiles()
        {
            var manifest = LoadManifest();
            Assert.That(manifest.entries, Has.Length.EqualTo(71));
            Assert.That(manifest.entries.Select(entry => entry.skillId).Distinct(StringComparer.Ordinal).ToArray(), Has.Length.EqualTo(71));
            Assert.That(manifest.entries.Count(entry => string.Equals(entry.group, "enemy", StringComparison.Ordinal)), Is.EqualTo(24));
            Assert.That(manifest.entries.Count(entry => !string.Equals(entry.group, "enemy", StringComparison.Ordinal)), Is.EqualTo(47));

            foreach (var entry in manifest.entries)
            {
                var skillPath = NormalizeAssetPath(entry.assetPath);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(skillPath);
                Assert.That(skill, Is.Not.Null, entry.skillId);
                Assert.That(skill.SkillId, Is.EqualTo(entry.skillId), skillPath);
                Assert.That(skill.Icon, Is.Not.Null, entry.skillId);

                var targetPath = SkillTargetPath(entry);
                Assert.That(skill.Icon.name, Is.EqualTo(Path.GetFileNameWithoutExtension(targetPath)), entry.skillId);
                AssertTexture(targetPath, 32, 32f, FilterMode.Point, true);
            }

            Assert.That(manifest.entries.Any(entry => entry.skillId == "skill.knife_toss"), Is.True);
            Assert.That(manifest.entries.Any(entry => entry.skillId == "skill.battle_cry"), Is.True);
        }

        [Test]
        public void SelectedPresentationTexturesUseApprovedImportProfiles()
        {
            foreach (var name in new[]
            {
                "jerzy-front-64-v1", "wladimir-front-64-v1", "alison-front-64-v2", "reimond-front-64-v1",
                "skeleton-swordsman-idle-64", "skeleton-archer-idle-64", "skeleton-defender-idle-64",
                "skeleton-occultist-idle-64", "undead-fleshy-idle-64", "ghoul-quadruped-idle-64", "wraith-idle-64"
            })
            {
                AssertTexture("Assets/_Project/Art/Characters/Sprites/VS22/" + name + ".png", 64, 32f, FilterMode.Point, true);
            }
            AssertTexture("Assets/_Project/Art/Characters/Sprites/VS22/dead-king-idle-128.png", 128, 32f, FilterMode.Point, true);
            AssertTexture("Assets/_Project/Art/Characters/Portraits/VS22/jerzy-combat-portrait-128.png", 128, 128f, FilterMode.Bilinear, false);
            foreach (var name in new[] { "wladimir", "alison", "reimond", "archer", "defender", "occultist", "undead", "ghoul", "wraith", "deadking" })
                AssertTexture($"Assets/_Project/Art/Characters/Portraits/VS22/{name}-combat-portrait-128.png", 128, 128f, FilterMode.Bilinear, false);
            AssertTexture("Assets/_Project/Art/Characters/Portraits/VS22/skeleton-swordsman-combat-portrait-128.png", 128, 128f, FilterMode.Bilinear, false);
            AssertTexture("Assets/_Project/Art/Characters/NPC/VS22/blacksmith-64-v1.png", 64, 32f, FilterMode.Point, true);

            foreach (var name in new[] { "bleed", "poison", "burn", "stun" })
                AssertTexture("Assets/_Project/Art/Icons/Statuses/VS22/" + name + "-32.png", 32, 32f, FilterMode.Point, true);
        }

        [Test]
        public void WorldAndNpcSpritesUseGroundedCustomPivotsWithoutChangingUiArt()
        {
            foreach (var name in new[]
            {
                "jerzy-front-64-v1", "wladimir-front-64-v1", "alison-front-64-v2", "reimond-front-64-v1",
                "skeleton-swordsman-idle-64", "skeleton-archer-idle-64", "skeleton-defender-idle-64",
                "skeleton-occultist-idle-64", "undead-fleshy-idle-64", "ghoul-quadruped-idle-64", "wraith-idle-64"
            })
            {
                AssertGroundedSprite(
                    "Assets/_Project/Art/Characters/Sprites/VS22/" + name + ".png",
                    ApprovedAlphaMinY[name + ".png"]);
            }
            AssertGroundedSprite(
                "Assets/_Project/Art/Characters/Sprites/VS22/dead-king-idle-128.png",
                ApprovedAlphaMinY["dead-king-idle-128.png"]);
            AssertGroundedSprite(
                "Assets/_Project/Art/Characters/NPC/VS22/blacksmith-64-v1.png",
                ApprovedAlphaMinY["blacksmith-64-v1.png"]);

            foreach (var path in new[]
            {
                "Assets/_Project/Art/Characters/Portraits/VS22/jerzy-combat-portrait-128.png",
                "Assets/_Project/Art/Characters/Portraits/VS22/skeleton-swordsman-combat-portrait-128.png",
                "Assets/_Project/Art/Icons/Statuses/VS22/bleed-32.png",
                "Assets/_Project/Art/Icons/Statuses/VS22/poison-32.png",
                "Assets/_Project/Art/Icons/Statuses/VS22/burn-32.png",
                "Assets/_Project/Art/Icons/Statuses/VS22/stun-32.png"
            })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Center), path);
                Assert.That(settings.spritePivot.x, Is.EqualTo(0.5f).Within(0.0001f), path);
                Assert.That(settings.spritePivot.y, Is.EqualTo(0.5f).Within(0.0001f), path);
            }
        }

        [Test]
        public void StatusGlossaryAndBlacksmithKeepApprovedBindings()
        {
            var blacksmith = AssetDatabase.LoadAssetAtPath<NpcDefinition>(BlacksmithPath);
            Assert.That(blacksmith, Is.Not.Null);
            Assert.That(blacksmith.Portrait, Is.Not.Null);
            Assert.That(blacksmith.Portrait.name, Is.EqualTo("blacksmith-64-v1"));

            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>(GlossaryPath);
            Assert.That(glossary, Is.Not.Null);
            foreach (var id in new[] { "bleed", "poison", "burn", "stun", "immobilize", "sleep",
                         "freeze", "paralysis", "invisibility", "paladin.holy_regeneration",
                         "paladin.blessing", "warrior.order", "warrior.protection", "warrior.trauma",
                         "warrior.strategy", "scout.anger", "scout.weak_point", "scout.embedded_knives",
                         "scout.analysis", "weakening" })
            {
                Assert.That(glossary.TryGet(id, out var entry), Is.True, id);
                Assert.That(entry.Icon, Is.Not.Null, id);
                Assert.That(entry.Icon.name, Is.EqualTo(id + "-32"), id);
                Assert.That(entry.Title, Is.Not.Empty, id);
                Assert.That(entry.Description, Is.Not.Empty, id);
                AssertTexture("Assets/_Project/Art/Icons/Statuses/VS22/" + id + "-32.png", 32, 32f, FilterMode.Point, true);
                var entries = new SerializedObject(glossary).FindProperty("entries");
                var matchingEntries = 0;
                for (var i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == id)
                        matchingEntries++;
                Assert.That(matchingEntries, Is.EqualTo(1), id);
            }
        }

        private static SkillManifest LoadManifest()
        {
            var manifestPath = Path.Combine(Application.dataPath, "_Project/Editor/ArtCatalogs", ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(manifestPath), Is.True, manifestPath);
            var manifest = JsonUtility.FromJson<SkillManifest>(File.ReadAllText(manifestPath));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.entries, Is.Not.Null);
            return manifest;
        }

        private static CharacterDefinition LoadCharacter(string path, string expectedId)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            Assert.That(character, Is.Not.Null, path);
            Assert.That(character.Id, Is.EqualTo(expectedId), path);
            return character;
        }

        private static void AssertStaticCharacter(string path, string expectedId, string expectedSpriteName, string expectedPortraitName = null)
        {
            var character = LoadCharacter(path, expectedId);
            Assert.That(character.VisualSprite, Is.Not.Null, path);
            Assert.That(character.VisualSprite.name, Is.EqualTo(expectedSpriteName), path);
            Assert.That(character.IdleAnimationFrames, Has.Count.EqualTo(1), path);
            Assert.That(character.IdleAnimationFrames[0], Is.SameAs(character.VisualSprite), path);
            Assert.That(character.RunAnimationFrames, Has.Count.EqualTo(1), path);
            Assert.That(character.RunAnimationFrames[0], Is.SameAs(character.VisualSprite), path);
            Assert.That(character.HitSprite, Is.SameAs(character.VisualSprite), path);
            if (expectedPortraitName == null)
            {
                Assert.That(character.PortraitSprite, Is.Null, path);
                Assert.That(character.UiPortraitSprite, Is.SameAs(character.VisualSprite), path);
            }
            else
            {
                Assert.That(character.PortraitSprite, Is.Not.Null, path);
                Assert.That(character.PortraitSprite.name, Is.EqualTo(expectedPortraitName), path);
                Assert.That(character.UiPortraitSprite, Is.SameAs(character.PortraitSprite), path);
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            var normalized = path.Replace('\\', '/');
            const string projectPrefix = "Godmist Unity/";
            if (normalized.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(projectPrefix.Length);
            Assert.That(normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase), Is.True, path);
            return normalized;
        }

        private static string SkillTargetPath(SkillManifestEntry entry)
        {
            // The approved Dead King V2 art replaced this older manifest icon.
            if (entry.skillId == "skill.enemy.catacombs.dead_king_guard")
                return "Assets/Resources/Godmist/DeadKingArtV2/Icons/dead-king-2-icon-guard-32.png";
            var fileName = ExistingSkillTargets.TryGetValue(entry.skillId, out var existing) ? existing : entry.file;
            return SkillArtRoot + fileName;
        }

        private static void AssertTexture(
            string path,
            int expectedSize,
            float expectedPixelsPerUnit,
            FilterMode expectedFilter,
            bool expectedAlphaIsTransparency)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(texture, Is.Not.Null, path);
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(texture.width, Is.EqualTo(expectedSize), path);
            Assert.That(texture.height, Is.EqualTo(expectedSize), path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(expectedPixelsPerUnit), path);
            Assert.That(importer.filterMode, Is.EqualTo(expectedFilter), path);
            Assert.That(importer.alphaIsTransparency, Is.EqualTo(expectedAlphaIsTransparency), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.GetDefaultPlatformTextureSettings().textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            Assert.That(importer.GetPlatformTextureSettings("Standalone").textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed), path);
        }

        private static void AssertGroundedSprite(string path, int expectedAlphaMinY)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(texture, Is.Not.Null, path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(importer, Is.Not.Null, path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight), path);
            Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Custom), path);
            Assert.That(settings.spritePivot.x, Is.EqualTo(0.5f).Within(0.0001f), path);
            var alphaMinY = ReadAlphaMinY(Path.GetFullPath(path));
            var baselineError = alphaMinY - sprite.rect.y - settings.spritePivot.y * sprite.rect.height;
            Assert.That(baselineError, Is.EqualTo(0f).Within(0.001f), path);
            Assert.That(sprite.pivot.x / sprite.rect.width, Is.EqualTo(0.5f).Within(0.0001f), path);
            Assert.That(sprite.bounds.min.y, Is.LessThanOrEqualTo(0f), path);
            Assert.That(alphaMinY, Is.EqualTo(expectedAlphaMinY), path);
        }

        private static int ReadAlphaMinY(string sourcePath)
        {
            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(sourceTexture, File.ReadAllBytes(sourcePath), false), Is.True, sourcePath);
                var pixels = sourceTexture.GetPixels32();
                for (var y = 0; y < sourceTexture.height; y++)
                {
                    for (var x = 0; x < sourceTexture.width; x++)
                    {
                        if (pixels[y * sourceTexture.width + x].a > 0)
                            return y;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceTexture);
            }

            Assert.Fail("Source image has no visible alpha: " + sourcePath);
            return -1;
        }
    }
}

