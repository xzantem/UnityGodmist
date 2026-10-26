using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class ForestEnemiesSetup
    {
        public const string EntPath = Root + "CD_Forest_InfectedEnt.asset";
        public const string EntBurningPath = Root + "CD_Forest_InfectedEntBurning.asset";
        public const string EntEncounterPath = Root + "ED_Forest_InfectedEnt.asset";
        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Ent Gameplay")]
        public static void ConfigureEnt()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset")
                ?? throw new InvalidOperationException("Configure forest enemies first");
            var branch = Skill("branch", "Zamach Gałęzią", 0, 60, 80, AttackType.Melee, so =>
            {
                Mode(so, TargetType.AreaAroundCaster, 1);
                var range = so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange");
                Raw(range, "shape", RangeShape.Rectangle); Raw(range, "rectangleWidth", 3);
                Raw(Effect(so, EffectType.DealDamage), "alwaysHits", false);
                var push = Effect(so, EffectType.Push); Raw(push, "baseChance", .5f); Raw(push, "displacementDistance", 1);
                EntHit(push);
                EntStatus(so, StatusEffectIds.Poison, .5f, 1f, 1);
            }, "Ent");
            var roots = Skill("roots", "Splugawione Korzenie", 40, 70, 89, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.All, 0); Field(so, "RequiresLineOfSight", false);
                var immobilize = Effect(so, EffectType.ApplyStatus);
                Raw(immobilize, "statusId", StatusEffectIds.Immobilize);
                Raw(immobilize, "baseChance", .4f); Raw(immobilize, "durationTurns", 2); Raw(immobilize, "alwaysHits", false);
                {
                    var effect = Effect(so, EffectType.ModifyResistance);
                    Raw(effect, "resistanceType", ResistanceType.All); Raw(effect, "modifierValue", -.25f);
                    Raw(effect, "baseChance", .5f); Raw(effect, "durationTurns", 2); Raw(effect, "alwaysHits", false);
                }
            }, "Ent");
            var bark = Skill("bark", "Twarda Kora", 60, 20, 100, AttackType.Unknown, so =>
            {
                Mode(so, TargetType.Caster, 0);
                foreach (var stat in new[] { CharacterStatType.PhysicalDefense, CharacterStatType.MagicalDefense, CharacterStatType.Speed })
                {
                    var effect = Effect(so, EffectType.ModifyStat);
                    Raw(effect, "modifierStatType", stat); Raw(effect, "durationTurns", 3);
                    Raw(effect, "modifierOperation", stat == CharacterStatType.Speed ? StatModifierOperation.FlatBase : StatModifierOperation.Percent);
                    Raw(effect, "modifierValue", stat == CharacterStatType.Speed ? -40f : 1f);
                    Raw(effect, "polarity", stat == CharacterStatType.Speed ? EffectPolarity.Negative : EffectPolarity.Positive);
                }
            }, "Ent");
            var fist = Skill("fist", "Spaczona Pięść", 0, 60, 83, AttackType.Melee, so =>
            {
                Mode(so, TargetType.AreaAroundTile, 2);
                var relative = so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("relativeRange");
                Raw(relative, "shape", RangeShape.Square); Raw(relative, "minRange", 0); Raw(relative, "maxRange", 2);
                Raw(Effect(so, EffectType.DealDamage), "alwaysHits", false);
                EntStatus(so, StatusEffectIds.Burn, .5f, .6f, 2);
            }, "Ent");
            var ignition = Skill("ignition", "Zapłon", 25, 35, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundCaster, 6);
                Raw(so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange"), "shape", RangeShape.Line);
                var effect = Effect(so, EffectType.DealDamage); Raw(effect, "damageType", DamageType.Magical); Raw(effect, "alwaysHits", false);
                EntStatus(so, StatusEffectIds.Burn, .5f, .6f, 2);
            }, "Ent");
            var sanctuary = Skill("sanctuary", "Piekielne Sanktuarium", 70, 100, 100, AttackType.Unknown, so =>
            {
                Mode(so, TargetType.Caster, 0);
                // Encounter owner handles marking and its boss-turn clock after the paid action resolves.
                Effect(so, EffectType.ModifyActionValue);
                Field(so, "TooltipDescription", "Oznacza połowę pól areny. Zapłon na początku następnej tury Enta, na 3 tury. Wejście lub początek tury: obrażenia magiczne i 70% podpalenia na 2 tury. Raz na turę celu.");
            }, "Ent");
            var warning = EntMarker("Warning", "object.forest.ent.warning", "Sanktuarium — zapłon w następnej turze", new Color32(174, 102, 35, 210), false);
            var fire = EntMarker("Fire", "object.forest.ent.fire", "Piekielne Sanktuarium", new Color32(145, 63, 157, 220), true);
            var boss = EntCharacter("CD_Forest_InfectedEnt", ForestEntBossSystem.BossId, cls, branch, roots, bark);
            var burning = EntCharacter("CD_Forest_InfectedEntBurning", ForestEntBossSystem.PhaseTwoId, cls, fist, ignition, sanctuary);
            var bossData = new SerializedObject(boss); Field(bossData, "SecondPhaseDefinition", burning); Save(bossData);
            var burningData = new SerializedObject(burning);
            Field(burningData, "SanctuaryWarning", warning); Field(burningData, "SanctuaryFire", fire); Save(burningData);
            var encounter = Asset<EncounterDefinition>("ED_Forest_InfectedEnt");
            var data = new SerializedObject(encounter);
            Field(data, "Id", "encounter.forest.infected_ent"); Field(data, "DisplayName", "Zarażony Ent");
            Field(data, "GridWidth", 16); Field(data, "GridHeight", 16);
            var enemies = data.FindProperty(F("Enemies")); enemies.ClearArray(); var enemy = Add(enemies);
            Sub(enemy, "InstanceId", "enemy.forest.infected_ent.boss"); Sub(enemy, "CharacterDefinition", boss); Sub(enemy, "ClassData", cls);
            enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(10, 8);
            Sub(enemy, "VisionRange", 9f); Sub(enemy, "VisionAngle", 360f);
            enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            Save(data);
            ForestLootSetup.ConfigureEntLoot();
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }
        private static void EntHit(SerializedProperty effect)
        {
            var c = Add(effect.FindPropertyRelative("conditions"));
            Raw(c, "conditionType", EffectConditionType.ReferencedEffectSucceeded); Raw(c, "referencedEffectIndex", 0);
        }
        private static void EntStatus(SerializedObject so, string id, float chance, float multiplier, int turns)
        {
            var effect = Effect(so, EffectType.ApplyStatus); Raw(effect, "statusId", id);
            Raw(effect, "baseChance", chance); Raw(effect, "statusDamageMultiplier", multiplier); Raw(effect, "durationTurns", turns); EntHit(effect);
        }
        private static CharacterDefinition EntCharacter(string file, string id, CharacterClassData cls, params SkillData[] skills)
        {
            var boss = Asset<CharacterDefinition>(file); var data = new SerializedObject(boss);
            Field(data, "Id", id); Field(data, "DisplayName", "Zarażony Ent"); Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Dps); Field(data, "AiPersonality", CombatAiPersonality.Neutral);
            Field(data, "BasicAttackSkill", skills[0]);
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Undead; Add(tags).enumValueIndex = (int)CharacterTag.Giant;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 1400); Sub(stats, "MinAttack", 55); Sub(stats, "MaxAttack", 75);
            Sub(stats, "Dodge", 11f); Sub(stats, "PhysicalDefense", 22f); Sub(stats, "MagicalDefense", 28f);
            Sub(stats, "CritChance", .07f); Sub(stats, "CritDamage", 1.9f); Sub(stats, "Speed", 80f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 91f); Sub(growth, "MinAttack", 3.58f); Sub(growth, "MaxAttack", 4.88f);
            Sub(growth, "Dodge", .0825f); Sub(growth, "PhysicalDefense", 4.4f); Sub(growth, "MagicalDefense", 5.6f); Sub(growth, "CritChance", .000175f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", 1f); Sub(resist, "PoisonResistance", .8f); Sub(resist, "FireResistance", .15f); Sub(resist, "SuppressionResistance", .75f);
            var list = data.FindProperty(F("StartingSkills")); list.ClearArray(); foreach (var skill in skills) Add(list).objectReferenceValue = skill;
            Save(data); return boss;
        }
        private static SummonedObjectDefinition EntMarker(string file, string id, string name, Color32 color, bool fire)
        {
            var definition = Asset<SummonedObjectDefinition>("SOD_Forest_Ent" + file);
            var data = new SerializedObject(definition);
            Set(data.FindProperty("objectId"), id); Set(data.FindProperty("displayName"), name);
            Set(data.FindProperty("blocksMovement"), false); Set(data.FindProperty("blocksLineOfSight"), false);
            data.FindProperty("effects").ClearArray();
            var path = Root + "EntSanctuary" + file + ".png";
            if (!System.IO.File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = new Color32[1024];
                for (var y = 2; y < 30; y++) for (var x = 2; x < 30; x++)
                    if (fire ? (x * 7 + y * 13) % 11 < 3 : x == 3 || y == 3 || x == 28 || y == 28)
                        pixels[y * 32 + x] = color;
                texture.SetPixels32(pixels); texture.Apply(); System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            Set(data.FindProperty("worldSprite"), AssetDatabase.LoadAssetAtPath<Sprite>(path)); Save(data);
            return definition;
        }
        private static void RegisterEntIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var boss = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(EntPath);
            var burning = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(EntBurningPath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(EntEncounterPath);
            if (boss == null || burning == null || encounter == null) return;
            var data = new SerializedObject(dungeon); Field(data, "BossEncounterDefinition", encounter); Save(data);
            foreach (var character in new[] { boss, burning })
            {
                if (!catalog.CharacterDefinitions.Contains(character)) catalog.CharacterDefinitions.Add(character);
                foreach (var skill in character.StartingSkills) if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            }
            if (!catalog.EncounterDefinitions.Contains(encounter)) catalog.EncounterDefinitions.Add(encounter);
            EditorUtility.SetDirty(catalog);
        }
    }
}
