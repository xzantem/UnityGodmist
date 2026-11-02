using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class ForestEnemiesSetup
    {
        public const string ArcherPath = Root + "CD_Forest_ElfArcher.asset";
        public const string ArcherEncounterPath = Root + "ED_Forest_ElfArchers.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Archer Gameplay")]
        public static void ConfigureArcher()
        {
            ConfigureShooter(); // Retains authored presentation fields.
            var arrow = Skill("arrow", "Elficka Strzała", 0, 60, 98, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Single, 9);
                Raw(Effect(so, EffectType.DealDamage), "alwaysHits", false);
            }, "Archer");
            var rain = Skill("arrow_rain", "Deszcz Strzał", 30, 70, 77, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundTile, 7);
                var relative = so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("relativeRange");
                Raw(relative, "shape", RangeShape.Square);
                Raw(relative, "minRange", 0); Raw(relative, "maxRange", 2);
                var damage = Effect(so, EffectType.DealDamage);
                Raw(damage, "alwaysHits", false); Raw(damage, "damageMultiplier", .6f);
                var dodge = Effect(so, EffectType.ModifyStat);
                Raw(dodge, "modifierStatType", CharacterStatType.Dodge);
                Raw(dodge, "modifierOperation", StatModifierOperation.Percent);
                Raw(dodge, "modifierValue", -.08f);
                Raw(dodge, "baseChance", .5f); Raw(dodge, "durationTurns", 3);
                RequireShooterHit(dodge);
            }, "Archer");
            var eye = Skill("hawkeye", "Sokole Oko", 50, 30, 100, AttackType.Unknown, so =>
            {
                Mode(so, TargetType.Caster, 0);
                var buff = Effect(so, EffectType.ApplyUniqueBuff);
                Raw(buff, "uniqueBuffType", UniqueBuffType.Hawkeye);
                Raw(buff, "durationTurns", 3); Raw(buff, "polarity", EffectPolarity.Positive);
            }, "Archer");
            var ear = Asset<ItemDefinition>("ID_Forest_ElfEar");
            var item = new SerializedObject(ear);
            Field(item, "Id", "item.material.elf_ear"); Field(item, "DisplayName", "Ucho elfa");
            Field(item, "Description", "Składnik wykorzystywany do sporządzania mikstur.");
            Field(item, "ItemType", ItemType.Material); Field(item, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            Field(item, "BaseRarity", RarityTier.Common); Field(item, "BaseValue", 17); Field(item, "IsStackable", true);
            Save(item);
            var loot = Asset<LootTableData>("LT_Forest_Elf"); var lootData = new SerializedObject(loot);
            var entries = lootData.FindProperty(F("Entries")); entries.ClearArray();
            var entry = Add(entries); Sub(entry, "ItemDefinition", ear); Sub(entry, "DropChance", .15f);
            var amount = entry.FindPropertyRelative(F("Amount")); Raw(amount, "Min", 1); Raw(amount, "Max", 2);
            Save(lootData);

            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset");
            var bandit = Asset<CharacterDefinition>("CD_Forest_ElfArcher");
            var data = new SerializedObject(bandit);
            Field(data, "Id", "enemy.forest.elf_archer");
            Field(data, "DisplayName", "Elfi Łucznik");
            Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Dps);
            Field(data, "AiPersonality", CombatAiPersonality.Aggressive);
            Field(data, "BasicAttackSkill", arrow); Field(data, "LootTable", loot);
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Human;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 240);
            Sub(stats, "Dodge", 30f); Sub(stats, "PhysicalDefense", 4f); Sub(stats, "MagicalDefense", 5f);
            Sub(stats, "CritChance", .1f); Sub(stats, "CritDamage", 1.9f); Sub(stats, "Speed", 54f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 16f);
            Sub(growth, "Dodge", .225f); Sub(growth, "PhysicalDefense", .8f); Sub(growth, "MagicalDefense", 1f);
            Sub(growth, "CritChance", .00025f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .3f); Sub(resist, "PoisonResistance", .6f);
            Sub(resist, "FireResistance", .25f); Sub(resist, "SuppressionResistance", .4f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { arrow, rain, eye }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_ElfArchers");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.elf_archers");
            Field(encounterData, "DisplayName", "Elfi zwiadowcy");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.elf_archers." + i);
                Sub(enemy, "CharacterDefinition", bandit); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        private static void RegisterArcherIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var bandit = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ArcherPath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(ArcherEncounterPath);
            if (bandit == null || encounter == null) return;
            if (!dungeon.EncounterDefinitions.Contains(encounter)) dungeon.EncounterDefinitions.Add(encounter);
            if (!catalog.CharacterDefinitions.Contains(bandit)) catalog.CharacterDefinitions.Add(bandit);
            if (!catalog.EncounterDefinitions.Contains(encounter)) catalog.EncounterDefinitions.Add(encounter);
            foreach (var skill in bandit.StartingSkills)
                if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            foreach (var entry in bandit.LootTable.Entries)
                if (!catalog.ItemDefinitions.Contains(entry.ItemDefinition)) catalog.ItemDefinitions.Add(entry.ItemDefinition);
            EditorUtility.SetDirty(dungeon);
        }
    }
}
