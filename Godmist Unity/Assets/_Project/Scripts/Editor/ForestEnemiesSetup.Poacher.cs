using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class ForestEnemiesSetup
    {
        public const string PoacherPath = Root + "CD_Forest_ElfPoacher.asset";
        public const string PoacherEncounterPath = Root + "ED_Forest_ElfHuntingParty.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Poacher Gameplay")]
        public static void ConfigurePoacher()
        {
            ConfigureArcher(); // Retains authored presentation fields.
            var trap = Asset<SummonedObjectDefinition>("SOD_Forest_BearTrap");
            var trapData = new SerializedObject(trap);
            Set(trapData.FindProperty("objectId"), "forest.bear_trap");
            Set(trapData.FindProperty("displayName"), "Pułapka na Niedźwiedzie");
            Set(trapData.FindProperty("triggersOnEntry"), true);
            Set(trapData.FindProperty("blocksMovement"), false);
            Set(trapData.FindProperty("blocksLineOfSight"), false);
            Set(trapData.FindProperty("maxHits"), 1);
            Set(trapData.FindProperty("effectTargetingType"), TargetingType.Enemies);
            var trapEffects = trapData.FindProperty("effects"); trapEffects.ClearArray();
            var immobilize = Add(trapEffects);
            Raw(immobilize, "effectType", SummonedObjectEffectType.ApplyStatus);
            Raw(immobilize, "statusId", StatusEffectIds.Immobilize);
            Raw(immobilize, "baseChance", 1f); Raw(immobilize, "durationTurns", 2); Raw(immobilize, "stacks", 1);
            var trapBleed = Add(trapEffects);
            Raw(trapBleed, "effectType", SummonedObjectEffectType.ApplyStatus);
            Raw(trapBleed, "statusId", StatusEffectIds.Bleed);
            Raw(trapBleed, "baseChance", .7f); Raw(trapBleed, "durationTurns", 3);
            Raw(trapBleed, "statusDamageMultiplier", .5f); Raw(trapBleed, "stacks", 1);
            Save(trapData);
            var knife = Skill("knife", "Sztylet z Zębami", 0, 60, 69, AttackType.Melee, so =>
            {
                Mode(so, TargetType.Single, 1);
                Raw(Effect(so, EffectType.DealDamage), "alwaysHits", false);
                var bleed = Effect(so, EffectType.ApplyStatus);
                Raw(bleed, "statusId", StatusEffectIds.Bleed); Raw(bleed, "baseChance", .4f);
                Raw(bleed, "statusDamageMultiplier", .6f); Raw(bleed, "durationTurns", 2);
                RequireShooterHit(bleed);
            }, "Poacher");
            var bearTrap = Skill("bear_trap", "Pułapka na Niedźwiedzie", 25, 30, 100, AttackType.Unknown, so =>
            {
                Mode(so, TargetType.Caster, 0);
                Field(so, "TooltipDescription", "Po wejściu przeciwnika: unieruchomienie na 2 tury i 70% szansy na krwawienie (50% ataku przez 3 tury). Pułapka zużywa się po aktywacji.");
                var create = Effect(so, EffectType.CreateObject);
                Raw(create, "summonedObjectDefinition", trap); Raw(create, "maxActiveObjects", 1);
                var range = create.FindPropertyRelative("objectPlacementRange");
                Raw(range, "shape", RangeShape.Square); Raw(range, "minRange", 1); Raw(range, "maxRange", 1);
            }, "Poacher");
            var poison = Skill("poison_bag", "Worek z Trutką", 55, 70, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundTile, 3);
                var relative = so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("relativeRange");
                Raw(relative, "shape", RangeShape.Square); Raw(relative, "minRange", 0); Raw(relative, "maxRange", 1);
                var effect = Effect(so, EffectType.ApplyStatus);
                Raw(effect, "statusId", StatusEffectIds.Poison); Raw(effect, "baseChance", .8f);
                Raw(effect, "statusDamageMultiplier", .6f); Raw(effect, "durationTurns", 3);
            }, "Poacher");
            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset");
            var bandit = Asset<CharacterDefinition>("CD_Forest_ElfPoacher");
            var data = new SerializedObject(bandit);
            Field(data, "Id", "enemy.forest.elf_poacher");
            Field(data, "DisplayName", "Elfi Kłusownik");
            Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Dps);
            Field(data, "AiPersonality", CombatAiPersonality.Aggressive);
            Field(data, "BasicAttackSkill", knife);
            Field(data, "LootTable", AssetDatabase.LoadAssetAtPath<Godmist.Data.Items.LootTableData>(Root + "LT_Forest_Elf.asset"));
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Human;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 310);
            Sub(stats, "Dodge", 26f); Sub(stats, "PhysicalDefense", 8f); Sub(stats, "MagicalDefense", 8f);
            Sub(stats, "CritChance", .08f); Sub(stats, "CritDamage", 1.7f); Sub(stats, "Speed", 48f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 21f);
            Sub(growth, "Dodge", .195f); Sub(growth, "PhysicalDefense", 1.6f); Sub(growth, "MagicalDefense", 1.6f);
            Sub(growth, "CritChance", .0002f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .3f); Sub(resist, "PoisonResistance", .6f);
            Sub(resist, "FireResistance", .3f); Sub(resist, "SuppressionResistance", .45f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { knife, bearTrap, poison }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_ElfHuntingParty");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.elf_hunting_party");
            Field(encounterData, "DisplayName", "Elfie polowanie");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.elf_hunting_party." + i);
                Sub(enemy, "CharacterDefinition", i == 0 ? bandit : AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ArcherPath)); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        private static void RegisterPoacherIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var bandit = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(PoacherPath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(PoacherEncounterPath);
            if (bandit == null || encounter == null) return;
            if (!dungeon.EncounterDefinitions.Contains(encounter)) dungeon.EncounterDefinitions.Add(encounter);
            if (!catalog.CharacterDefinitions.Contains(bandit)) catalog.CharacterDefinitions.Add(bandit);
            if (!catalog.EncounterDefinitions.Contains(encounter)) catalog.EncounterDefinitions.Add(encounter);
            foreach (var skill in bandit.StartingSkills)
                if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            EditorUtility.SetDirty(dungeon);
        }
    }
}
