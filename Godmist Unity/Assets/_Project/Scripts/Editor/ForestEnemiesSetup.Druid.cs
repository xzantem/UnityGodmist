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
        public const string DruidPath = Root + "CD_Forest_ElfDruid.asset";
        public const string DruidEncounterPath = Root + "ED_Forest_ElfDruids.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Druid Gameplay")]
        public static void ConfigureDruid()
        {
            ConfigureTree(); // Retains authored presentation fields.
            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset");
            var poisonPulse = Skill("poison_pulse", "Trująca Narośl", 0, 100, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundCaster, 1);
                var poison = Effect(so, EffectType.ApplyStatus);
                Raw(poison, "statusId", StatusEffectIds.Poison); Raw(poison, "baseChance", .8f);
                Raw(poison, "statusDamageMultiplier", .6f); Raw(poison, "durationTurns", 3);
            }, "Druid");
            var healPulse = Skill("healing_pulse", "Łagodzące Korzenie", 0, 100, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundCaster, 1);
                Raw(so.FindProperty("targetModes").GetArrayElementAtIndex(0), "targetingType", TargetingType.Allies);
                Raw(Effect(so, EffectType.Heal), "healAverageAttackMultiplier", .9f);
            }, "Druid");
            var poisonGrowth = ConfigureDruidGrowth("PoisonGrowth", "Trująca Narośl", poisonPulse, cls);
            var healingGrowth = ConfigureDruidGrowth("HealingGrowth", "Łagodząca Narośl", healPulse, cls);
            var roots = Skill("nature_wrath", "Gniew Natury", 5, 50, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Single, 6);
                var damage = Effect(so, EffectType.DealDamage);
                Raw(damage, "damageType", DamageType.Magical); Raw(damage, "damageMultiplier", .5f);
                Raw(damage, "damagePerOwnedSummonGroup", "forest.growth");
                Field(so, "TooltipDescription", "Obrażenia za każdą własną narośl.");
            }, "Druid");
            var bindingRoots = ConfigureDruidSummon("poison_growth", "Trująca Narośl", 40, poisonGrowth, cls);
            var plague = ConfigureDruidSummon("healing_growth", "Łagodzące Korzenie", 30, healingGrowth, cls);
            var tree = Asset<CharacterDefinition>("CD_Forest_ElfDruid");
            var data = new SerializedObject(tree);
            Field(data, "Id", "enemy.forest.elf_druid");
            Field(data, "DisplayName", "Elfi Druid");
            Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Support);
            Field(data, "AiPersonality", CombatAiPersonality.Neutral);
            Field(data, "BasicAttackSkill", roots);
            Field(data, "LootTable", AssetDatabase.LoadAssetAtPath<Godmist.Data.Items.LootTableData>(Root + "LT_Forest_Elf.asset"));
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Human;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 200);
            Sub(stats, "Dodge", 17f); Sub(stats, "PhysicalDefense", 3f); Sub(stats, "MagicalDefense", 13f);
            Sub(stats, "CritChance", .03f); Sub(stats, "CritDamage", 1.4f); Sub(stats, "Speed", 42f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 13f);
            Sub(growth, "Dodge", .1275f); Sub(growth, "PhysicalDefense", .6f); Sub(growth, "MagicalDefense", 2.6f);
            Sub(growth, "CritChance", .000075f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .25f); Sub(resist, "PoisonResistance", .6f);
            Sub(resist, "FireResistance", .3f); Sub(resist, "SuppressionResistance", .65f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { roots, bindingRoots, plague }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_ElfDruids");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.elf_druids");
            Field(encounterData, "DisplayName", "Elfie korzenie");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.elf_druids." + i);
                Sub(enemy, "CharacterDefinition", i == 0 ? tree : AssetDatabase.LoadAssetAtPath<CharacterDefinition>(PoacherPath)); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        private static CharacterDefinition ConfigureDruidGrowth(string key, string name, SkillData pulse, CharacterClassData cls)
        {
            var growth = Asset<CharacterDefinition>("CD_Forest_" + key);
            var so = new SerializedObject(growth);
            Field(so, "Id", "summon.forest." + key.ToLowerInvariant()); Field(so, "DisplayName", name);
            Field(so, "ClassData", cls); Field(so, "AiRole", CombatAiRole.Support);
            Field(so, "AiPersonality", CombatAiPersonality.Neutral); Field(so, "BasicAttackSkill", pulse);
            so.FindProperty(F("LootTable")).objectReferenceValue = null;
            var stats = so.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 30); Sub(stats, "MinAttack", 20); Sub(stats, "MaxAttack", 30);
            Sub(stats, "Speed", 42f); Sub(stats, "WeaponAccuracy", 100f);
            Sub(stats, "CritChance", 0f); Sub(stats, "MaxResource", 100);
            var skills = so.FindProperty(F("StartingSkills")); skills.ClearArray(); Add(skills).objectReferenceValue = pulse;
            Save(so); return growth;
        }

        private static SkillData ConfigureDruidSummon(string key, string name, int mp, CharacterDefinition growth, CharacterClassData cls)
            => Skill(key, name, mp, 50, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Caster, 0);
                var effect = Effect(so, EffectType.Summon);
                Raw(effect, "summonedCharacterDefinition", growth); Raw(effect, "summonedClassData", cls);
                Raw(effect, "summonUsesCasterStats", true); Raw(effect, "summonStationary", true);
                Raw(effect, "summonPersistent", true); Raw(effect, "summonGroupId", "forest.growth");
                Raw(effect, "summonPercentMaxHealth", .15f); Raw(effect, "maxActiveSummons", int.MaxValue);
                var range = effect.FindPropertyRelative("summonPlacementRange");
                Raw(range, "shape", RangeShape.Square); Raw(range, "minRange", 1); Raw(range, "maxRange", 6);
                Field(so, "TooltipDescription", "Tworzy narośl z 15% maksymalnego zdrowia rzucającego.");
            }, "Druid");

        private static void RegisterDruidIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var tree = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(DruidPath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(DruidEncounterPath);
            if (tree == null || encounter == null) return;
            if (!dungeon.EncounterDefinitions.Contains(encounter)) dungeon.EncounterDefinitions.Add(encounter);
            if (!catalog.CharacterDefinitions.Contains(tree)) catalog.CharacterDefinitions.Add(tree);
            if (!catalog.EncounterDefinitions.Contains(encounter)) catalog.EncounterDefinitions.Add(encounter);
            foreach (var skill in tree.StartingSkills)
                if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            foreach (var key in new[] { "PoisonGrowth", "HealingGrowth" })
            {
                var growth = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "CD_Forest_" + key + ".asset");
                if (growth == null) continue;
                if (!catalog.CharacterDefinitions.Contains(growth)) catalog.CharacterDefinitions.Add(growth);
                foreach (var skill in growth.StartingSkills)
                    if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            }
            EditorUtility.SetDirty(dungeon);
        }
    }
}
