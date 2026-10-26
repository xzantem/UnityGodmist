using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Data.NPC;
using Godmist.Data.Skills;
using Godmist.Views.UI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports and binds the approved VS-22 presentation slice.
/// The operation is repeatable and touches only the selected art assets and
/// their ScriptableObject references; scenes and prefabs remain authored data.
/// </summary>
public static class VS22PresentationSetup
{
    private const string WorldArtRoot = "Assets/_Project/Art/Characters/Sprites/VS22/";
    private const string PortraitArtRoot = "Assets/_Project/Art/Characters/Portraits/VS22/";
    private const string SkillArtRoot = "Assets/_Project/Art/Icons/Skills/VS22/";
    private const string StatusArtRoot = "Assets/_Project/Art/Icons/Statuses/VS22/";
    private const string NpcArtRoot = "Assets/_Project/Art/Characters/NPC/VS22/";
    private const string SkillManifestRelativePath = "skills-complete/skills-complete-manifest.json";

    // These four approved icons already have stable VS-22 target assets. Reuse
    // those targets so rerunning the full import does not create duplicates.
    private static readonly Dictionary<string, string> ExistingSkillTargets =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["skill.warrior.cut"] = "cut-b-v2-32.png",
            ["skill.scout.poisoned_dagger"] = "poisoned-dagger-b-v2-32.png",
            ["skill.mage.shield"] = "magic-shield-b-v2-32.png",
            ["skill.paladin.group_heal"] = "group-heal-b-v2-32.png"
        };

    private static string Field(string name) => "<" + name + ">k__BackingField";

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static string ReviewRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", "output", "asset-review"));

    private static string SkillManifestRoot => Path.Combine(ReviewRoot, "skills-complete");

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

    [MenuItem("Godmist/Content/Configure VS-22 presentation art")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Exit Play Mode before authoring VS-22 presentation art.");
        }

        var skillManifest = LoadAndValidateSkillManifest();

        // Validate every definition and its stable logical ID before importing
        // any art, so a path/ID mismatch cannot silently bind another asset.
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset", "jerzy");
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset", "wladimir");
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset", "character.mage");
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset", "character.paladin");
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Enemy_Test.asset", "enemy.catacombs.skeleton_swordsman");
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Skeleton_Test.asset", "skeleton.test");
        RequireCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Boss_Test.asset", "boss.test");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset", "enemy.catacombs.swordsman");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_archer.asset", "enemy.catacombs.archer");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_defender.asset", "enemy.catacombs.defender");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_occultist.asset", "enemy.catacombs.occultist");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_undead.asset", "enemy.catacombs.undead");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_ghoul.asset", "enemy.catacombs.ghoul");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_wraith.asset", "enemy.catacombs.wraith");
        RequireCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_dead_king.asset", "enemy.catacombs.dead_king");

        var jerzyWorld = ImportWorldSprite("catacombs-workbench/jerzy-front-64-v1.png", "jerzy-front-64-v1.png");
        var wladimirWorld = ImportWorldSprite("catacombs-workbench/wladimir-front-64-v1.png", "wladimir-front-64-v1.png");
        var alisonWorld = ImportWorldSprite("catacombs-workbench/alison-front-64-v2.png", "alison-front-64-v2.png");
        var reimondWorld = ImportWorldSprite("catacombs-workbench/reimond-front-64-v1.png", "reimond-front-64-v1.png");
        var skeletonWorld = ImportWorldSprite("catacombs-workbench/skeleton-swordsman-idle-64.png", "skeleton-swordsman-idle-64.png");
        var archerWorld = ImportWorldSprite("catacombs-workbench/skeleton-archer-idle-64.png", "skeleton-archer-idle-64.png");
        var defenderWorld = ImportWorldSprite("catacombs-workbench/skeleton-defender-idle-64.png", "skeleton-defender-idle-64.png");
        var occultistWorld = ImportWorldSprite("catacombs-workbench/skeleton-occultist-idle-64.png", "skeleton-occultist-idle-64.png");
        var undeadWorld = ImportWorldSprite("catacombs-workbench/undead-fleshy-idle-64.png", "undead-fleshy-idle-64.png");
        var ghoulWorld = ImportWorldSprite("catacombs-workbench/ghoul-quadruped-idle-64.png", "ghoul-quadruped-idle-64.png");
        var wraithWorld = ImportWorldSprite("catacombs-workbench/wraith-idle-64.png", "wraith-idle-64.png");
        var deadKingWorld = ImportWorldSprite("catacombs-workbench/dead-king-idle-128.png", "dead-king-idle-128.png");

        // Dedicated UI portraits remain separate from grounded world sprites.
        var jerzyPortrait = ImportSprite("combat-portraits-party-v2/jerzy-combat-portrait-128.png", PortraitArtRoot + "jerzy-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var skeletonPortrait = ImportSprite("combat-portraits-skeletons-v3/swordsman-combat-portrait-128.png", PortraitArtRoot + "skeleton-swordsman-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var wladimirPortrait = ImportSprite("combat-portraits-party-v2/wladimir-combat-portrait-128.png", PortraitArtRoot + "wladimir-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var alisonPortrait = ImportSprite("combat-portraits-party-v2/alison-combat-portrait-128.png", PortraitArtRoot + "alison-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var reimondPortrait = ImportSprite("combat-portraits-party-v2/reimond-combat-portrait-128.png", PortraitArtRoot + "reimond-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);

        var archerPortrait = ImportSprite("combat-portraits-skeletons-v3/archer-combat-portrait-128.png", PortraitArtRoot + "archer-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var defenderPortrait = ImportSprite("combat-portraits-skeletons-v3/defender-combat-portrait-128.png", PortraitArtRoot + "defender-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var occultistPortrait = ImportSprite("combat-portraits-skeletons-v3/occultist-combat-portrait-128.png", PortraitArtRoot + "occultist-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var undeadPortrait = ImportSprite("combat-portraits-remaining/undead-combat-portrait-128.png", PortraitArtRoot + "undead-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var ghoulPortrait = ImportSprite("combat-portraits-ghoul-humanoid/ghoul-combat-portrait-128.png", PortraitArtRoot + "ghoul-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var wraithPortrait = ImportSprite("combat-portraits-remaining/wraith-combat-portrait-128.png", PortraitArtRoot + "wraith-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);
        var deadKingPortrait = ImportSprite("combat-portraits-remaining/deadking-combat-portrait-128.png", PortraitArtRoot + "deadking-combat-portrait-128.png", 128f, FilterMode.Bilinear, false);

        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset", "jerzy", jerzyWorld, jerzyPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset", "wladimir", wladimirWorld, wladimirPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset", "character.mage", alisonWorld, alisonPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset", "character.paladin", reimondWorld, reimondPortrait);

        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Enemy_Test.asset", "enemy.catacombs.skeleton_swordsman", skeletonWorld, skeletonPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Skeleton_Test.asset", "skeleton.test", skeletonWorld);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Characters/CD_Boss_Test.asset", "boss.test", deadKingWorld, deadKingPortrait);

        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset", "enemy.catacombs.swordsman", skeletonWorld, skeletonPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_archer.asset", "enemy.catacombs.archer", archerWorld, archerPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_defender.asset", "enemy.catacombs.defender", defenderWorld, defenderPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_occultist.asset", "enemy.catacombs.occultist", occultistWorld, occultistPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_undead.asset", "enemy.catacombs.undead", undeadWorld, undeadPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_ghoul.asset", "enemy.catacombs.ghoul", ghoulWorld, ghoulPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_wraith.asset", "enemy.catacombs.wraith", wraithWorld, wraithPortrait);
        ConfigureCharacter("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_dead_king.asset", "enemy.catacombs.dead_king", deadKingWorld, deadKingPortrait);

        var statusSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal)
        {
            ["bleed"] = ImportSprite("status-icons/bleed-32.png", StatusArtRoot + "bleed-32.png", 32f, FilterMode.Point, true),
            ["poison"] = ImportSprite("status-icons/poison-32.png", StatusArtRoot + "poison-32.png", 32f, FilterMode.Point, true),
            ["burn"] = ImportSprite("status-icons/burn-32.png", StatusArtRoot + "burn-32.png", 32f, FilterMode.Point, true),
            ["stun"] = ImportSprite("status-icons/stun-32.png", StatusArtRoot + "stun-32.png", 32f, FilterMode.Point, true)
        };
        foreach (var id in new[] { "immobilize", "sleep", "freeze", "paralysis" })
            statusSprites[id] = ImportSprite("status-icons/" + id + "-32.png", StatusArtRoot + id + "-32.png", 32f, FilterMode.Point, true);
        foreach (var id in new[] { "invisibility", "paladin.holy_regeneration", "paladin.blessing",
                     "warrior.order", "warrior.protection", "warrior.trauma", "warrior.strategy",
                     "scout.anger", "scout.weak_point", "scout.embedded_knives", "scout.analysis" })
            statusSprites[id] = ImportSprite("status-buff-icons/" + id + "-32.png", StatusArtRoot + id + "-32.png", 32f, FilterMode.Point, true);
        statusSprites["weakening"] = ImportSprite("status-weakening-icon/weakening-32.png", StatusArtRoot + "weakening-32.png", 32f, FilterMode.Point, true);
        var blacksmith = ImportWorldSprite(
            "arungard-npc-sprites/blacksmith-64-v1.png",
            "blacksmith-64-v1.png",
            NpcArtRoot);

        ConfigureNpc("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_Blacksmith.asset", blacksmith);
        ConfigureSkills(skillManifest);
        ConfigureStatusGlossary(statusSprites);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"VS-22: imported 4 party sprites, 7 Catacombs enemy sprites, 1 128x128 Dead King sprite, 71 skill icons (47 player / 24 enemy), 20 status icons, and 12 dedicated portraits.");
    }

    private static IReadOnlyList<SkillManifestEntry> LoadAndValidateSkillManifest()
    {
        var manifestPath = Path.Combine(ReviewRoot, SkillManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Missing VS-22 skill manifest", manifestPath);

        var manifest = JsonUtility.FromJson<SkillManifest>(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.entries == null || manifest.entries.Length != 71)
            throw new InvalidOperationException("VS-22 skill manifest must contain exactly 71 entries.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in manifest.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.skillId) || string.IsNullOrWhiteSpace(entry.assetPath) || string.IsNullOrWhiteSpace(entry.image) || string.IsNullOrWhiteSpace(entry.file))
                throw new InvalidOperationException("VS-22 skill manifest contains an incomplete entry.");
            if (!ids.Add(entry.skillId))
                throw new InvalidOperationException("Duplicate VS-22 skill ID in manifest: " + entry.skillId);

            var sourcePath = Path.Combine(SkillManifestRoot, entry.image.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Missing VS-22 skill review asset", sourcePath);

            var assetPath = NormalizeAssetPath(entry.assetPath);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
            if (skill == null)
                throw new InvalidOperationException("Missing VS-22 skill definition: " + assetPath);
            if (!string.Equals(skill.SkillId, entry.skillId, StringComparison.Ordinal))
                throw new InvalidOperationException($"VS-22 skill ID mismatch at {assetPath}: manifest '{entry.skillId}', asset '{skill.SkillId}'.");

            var targetPath = SkillTargetPath(entry);
            if (targets.TryGetValue(targetPath, out var existingId) && !string.Equals(existingId, entry.skillId, StringComparison.Ordinal))
                throw new InvalidOperationException($"VS-22 skill target collision: {targetPath} for {existingId} and {entry.skillId}.");
            targets[targetPath] = entry.skillId;
        }

        var playerCount = manifest.entries.Count(entry => !string.Equals(entry.group, "enemy", StringComparison.Ordinal));
        var enemyCount = manifest.entries.Count(entry => string.Equals(entry.group, "enemy", StringComparison.Ordinal));
        if (playerCount != 47 || enemyCount != 24)
            throw new InvalidOperationException($"VS-22 skill manifest groups are {playerCount} player / {enemyCount} enemy, expected 47 / 24.");
        return manifest.entries;
    }

    private static string NormalizeAssetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        const string projectPrefix = "Godmist Unity/";
        if (normalized.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(projectPrefix.Length);
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("VS-22 manifest path is not a Unity asset path: " + path);
        return normalized;
    }

    private static string SkillTargetPath(SkillManifestEntry entry)
    {
        var fileName = ExistingSkillTargets.TryGetValue(entry.skillId, out var existing) ? existing : entry.file;
        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("VS-22 skill target is not a PNG: " + fileName);
        return SkillArtRoot + fileName;
    }

    private static Sprite ImportWorldSprite(
        string sourceRelativePath,
        string fileName,
        string targetRoot = WorldArtRoot)
    {
        return ImportSprite(
            sourceRelativePath,
            targetRoot + fileName,
            32f,
            FilterMode.Point,
            true,
            customPivot: new Vector2(0.5f, 0f),
            alignToVisibleBaseline: true);
    }

    private static Sprite ImportSprite(
        string sourceRelativePath,
        string targetAssetPath,
        float pixelsPerUnit,
        FilterMode filterMode,
        bool alphaIsTransparency,
        string sourceRoot = null,
        Vector2? customPivot = null,
        bool alignToVisibleBaseline = false)
    {
        var sourcePath = Path.Combine(sourceRoot ?? ReviewRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Missing VS-22 review asset", sourcePath);

        var targetPath = Path.Combine(ProjectRoot, targetAssetPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        if (!FilesEqual(sourcePath, targetPath))
            File.Copy(sourcePath, targetPath, true);
        AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceSynchronousImport);

        var sourceAlphaMinY = customPivot.HasValue && alignToVisibleBaseline
            ? ReadSourceAlphaMinY(sourcePath)
            : -1;

        var importer = AssetImporter.GetAtPath(targetAssetPath) as TextureImporter
            ?? throw new InvalidOperationException("Missing texture importer: " + targetAssetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = filterMode;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = alphaIsTransparency;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        if (customPivot.HasValue)
        {
            // The custom pivot is normalized to Unity's effective sprite rect
            // (the tight alpha rect), so the world origin sits on the visible
            // ground-contact row instead of the transparent canvas margin.
            textureSettings.spriteMeshType = SpriteMeshType.Tight;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = customPivot.Value;
        }
        importer.SetTextureSettings(textureSettings);

        var defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.textureCompression = TextureImporterCompression.Uncompressed;
        defaults.crunchedCompression = false;
        importer.SetPlatformTextureSettings(defaults);
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        standalone.textureCompression = TextureImporterCompression.Uncompressed;
        standalone.crunchedCompression = false;
        importer.SetPlatformTextureSettings(standalone);
        importer.SaveAndReimport();

        if (customPivot.HasValue && alignToVisibleBaseline)
        {
            // Sprite.rect is the effective imported rect, which may be the
            // alpha-tight rect rather than the full source canvas. Use the
            // source alpha baseline and that actual rect together so margins,
            // extrusion and per-asset framing are measured instead of guessed.
            var imported = LoadSprite(targetAssetPath);
            if (sourceAlphaMinY < imported.rect.y || sourceAlphaMinY > imported.rect.yMax)
                throw new InvalidOperationException($"VS-22 source baseline {sourceAlphaMinY} is outside imported rect {imported.rect} for {targetAssetPath}.");
            var baselinePivot = new Vector2(
                customPivot.Value.x,
                (sourceAlphaMinY - imported.rect.y) / Mathf.Max(1f, imported.rect.height));
            textureSettings.spritePivot = baselinePivot;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            imported = LoadSprite(targetAssetPath);
            Debug.Log($"VS-22 grounded pivot: {targetAssetPath}; sourceAlphaMinY={sourceAlphaMinY}; rectY={imported.rect.y}; rectHeight={imported.rect.height}; pivot=({baselinePivot.x:0.####},{baselinePivot.y:0.####});");
        }

        return LoadSprite(targetAssetPath);
    }

    private static Sprite LoadSprite(string targetAssetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(targetAssetPath).OfType<Sprite>().FirstOrDefault()
            ?? throw new InvalidOperationException("Missing imported sprite: " + targetAssetPath);
    }

    private static int ReadSourceAlphaMinY(string sourcePath)
    {
        var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(sourceTexture, File.ReadAllBytes(sourcePath), false))
                throw new InvalidOperationException("Unable to decode VS-22 source image: " + sourcePath);

            var pixels = sourceTexture.GetPixels32();
            for (var y = 0; y < sourceTexture.height; y++)
            {
                for (var x = 0; x < sourceTexture.width; x++)
                {
                    if (pixels[y * sourceTexture.width + x].a > 0)
                        return y;
                }
            }

            throw new InvalidOperationException("VS-22 source image has no visible alpha: " + sourcePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceTexture);
        }
    }

    private static bool FilesEqual(string sourcePath, string targetPath)
    {
        if (!File.Exists(targetPath)) return false;
        var sourceInfo = new FileInfo(sourcePath);
        var targetInfo = new FileInfo(targetPath);
        if (sourceInfo.Length != targetInfo.Length) return false;
        return File.ReadAllBytes(sourcePath).SequenceEqual(File.ReadAllBytes(targetPath));
    }

    private static CharacterDefinition RequireCharacter(string assetPath, string expectedId)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath)
            ?? throw new InvalidOperationException("Missing VS-22 character definition: " + assetPath);
        if (!string.Equals(character.Id, expectedId, StringComparison.Ordinal))
            throw new InvalidOperationException($"VS-22 character ID mismatch at {assetPath}: expected '{expectedId}', got '{character.Id}'.");
        return character;
    }

    private static void ConfigureCharacter(string assetPath, string expectedId, Sprite worldSprite, Sprite portraitSprite = null)
    {
        var character = RequireCharacter(assetPath, expectedId);
        var serializedObject = new SerializedObject(character);
        SetObject(serializedObject, Field("VisualSprite"), worldSprite);
        SetSingleFrame(serializedObject.FindProperty(Field("IdleAnimationFrames")), worldSprite);
        SetSingleFrame(serializedObject.FindProperty(Field("RunAnimationFrames")), worldSprite);
        SetObject(serializedObject, Field("HitSprite"), worldSprite);
        if (portraitSprite != null)
            SetObject(serializedObject, Field("PortraitSprite"), portraitSprite);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static void SetSingleFrame(SerializedProperty property, Sprite sprite)
    {
        if (property == null) throw new InvalidOperationException("VS-22 character animation field is missing.");
        property.arraySize = 1;
        property.GetArrayElementAtIndex(0).objectReferenceValue = sprite;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        var property = serializedObject.FindProperty(propertyName)
            ?? throw new InvalidOperationException("VS-22 serialized field is missing: " + propertyName);
        property.objectReferenceValue = value;
    }

    [MenuItem("Godmist/Content/Configure Battle Cry Icon")]
    public static void ConfigureBattleCryIcon()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before authoring the Battle Cry icon.");
        var entries = LoadAndValidateSkillManifest();
        ConfigureSkills(entries.Where(entry => entry.skillId == "skill.battle_cry").ToArray());
        AssetDatabase.SaveAssets();
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_BattleCry.asset");
        if (skill == null || skill.Icon == null || AssetDatabase.GetAssetPath(skill.Icon) != SkillArtRoot + "battle-cry-32.png")
            throw new InvalidOperationException("Battle Cry icon binding did not persist.");
        Debug.Log("BATTLE_CRY_ICON_BOUND: skill.battle_cry -> battle-cry-32; Point, 32 PPU, no compression/mipmaps.");
    }

    private static void ConfigureSkills(IReadOnlyList<SkillManifestEntry> entries)
    {
        foreach (var entry in entries)
        {
            var sprite = ImportSprite(entry.image, SkillTargetPath(entry), 32f, FilterMode.Point, true, SkillManifestRoot);
            var skillPath = NormalizeAssetPath(entry.assetPath);
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(skillPath)
                ?? throw new InvalidOperationException("Missing VS-22 skill definition: " + skillPath);
            if (!string.Equals(skill.SkillId, entry.skillId, StringComparison.Ordinal))
                throw new InvalidOperationException($"VS-22 skill ID changed before binding at {skillPath}: '{skill.SkillId}'.");

            var serializedObject = new SerializedObject(skill);
            SetObject(serializedObject, Field("Icon"), sprite);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
        }
    }

    private static void ConfigureNpc(string assetPath, Sprite sprite)
    {
        var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(assetPath)
            ?? throw new InvalidOperationException("Missing NPC definition: " + assetPath);
        var serializedObject = new SerializedObject(npc);
        SetObject(serializedObject, Field("Portrait"), sprite);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(npc);
    }

    private static void ConfigureStatusGlossary(IReadOnlyDictionary<string, Sprite> sprites)
    {
        const string assetPath = "Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset";
        var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>(assetPath)
            ?? throw new InvalidOperationException("Missing combat tooltip glossary: " + assetPath);
        var serializedObject = new SerializedObject(glossary);
        var entries = serializedObject.FindProperty("entries")
            ?? throw new InvalidOperationException("Combat tooltip glossary entries field is missing.");
        for (var index = 0; index < entries.arraySize; index++)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            var id = entry.FindPropertyRelative("Id").stringValue;
            if (sprites.TryGetValue(id, out var sprite))
                entry.FindPropertyRelative("Icon").objectReferenceValue = sprite;
        }
        var titles = new Dictionary<string, string>
        {
            ["paladin.holy_regeneration"] = "Święta Regeneracja", ["paladin.blessing"] = "Błogosławieństwo",
            ["warrior.order"] = "Rozkaz Unicestwienia", ["warrior.protection"] = "Rozkaz Ochrony",
            ["warrior.trauma"] = "Trauma", ["warrior.strategy"] = "Strategia",
            ["scout.anger"] = "Złość", ["scout.weak_point"] = "Słaby Punkt",
            ["scout.embedded_knives"] = "Wbite sztylety", ["scout.analysis"] = "Dogłębna Analiza",
            ["weakening"] = "Osłabienie"
        };
        var descriptions = new Dictionary<string, string>
        {
            ["paladin.holy_regeneration"] = "Okresowo przywraca zdrowie przez czas trwania efektu.",
            ["paladin.blessing"] = "Zwiększa zadawane obrażenia i ogranicza otrzymywane obrażenia.",
            ["warrior.order"] = "Zwiększa podatność oznaczonego celu na obrażenia.",
            ["warrior.protection"] = "Ogranicza otrzymywane obrażenia.",
            ["warrior.trauma"] = "Zwiększa obrażenia okresowe otrzymywane przez cel.",
            ["warrior.strategy"] = "Umożliwia wykonanie darmowych kroków.",
            ["scout.anger"] = "Złość zwiadowcy narasta przeciwko oznaczonemu celowi.",
            ["scout.weak_point"] = "Zwiększa podatność celu na trafienia krytyczne.",
            ["scout.embedded_knives"] = "Sztylety wbite w cel; liczba stosów oznacza ich liczbę.",
            ["scout.analysis"] = "Zwiększa penetrację dzięki analizie przeciwnika.",
            ["weakening"] = "Zmniejsza zadawane obrażenia."
        };
        foreach (var pair in sprites)
        {
            var exists = false;
            for (var i = 0; i < entries.arraySize; i++)
                exists |= entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == pair.Key;
            if (exists) continue;
            var index = entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Id").stringValue = pair.Key;
            entry.FindPropertyRelative("Icon").objectReferenceValue = pair.Value;
            entry.FindPropertyRelative("Title").stringValue = titles[pair.Key];
            entry.FindPropertyRelative("Description").stringValue = descriptions[pair.Key];
            entry.FindPropertyRelative("Aliases").ClearArray();
        }
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(glossary);
    }
}

