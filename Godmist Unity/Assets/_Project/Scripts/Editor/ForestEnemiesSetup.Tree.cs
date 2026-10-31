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
        public const string TreePath = Root + "CD_Forest_InfectedTree.asset";
        public const string TreeEncounterPath = Root + "ED_Forest_InfectedTrees.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Tree Gameplay")]
        public static void ConfigureTree()
        {
            ConfigurePoacher(); // Retains authored presentation fields.
            var roots = Skill("dark_roots", "Ciemne Korzenie", 5, 70, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Single, 5);
                Raw(Effect(so, EffectType.DealDamage), "damageType", DamageType.Magical);
                var poison = Effect(so, EffectType.ApplyStatus);
                Raw(poison, "statusId", StatusEffectIds.Poison); Raw(poison, "baseChance", .35f);
                Raw(poison, "statusDamageMultiplier", .55f); Raw(poison, "durationTurns", 3);
            }, "Tree");
            var bindingRoots = Skill("binding_roots", "Zniewalające Korzenie", 45, 65, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Single, 5);
                var root = Effect(so, EffectType.ApplyStatus);
                Raw(root, "statusId", StatusEffectIds.Immobilize); Raw(root, "durationTurns", 2);
                var dodge = Effect(so, EffectType.ModifyStat);
                Raw(dodge, "modifierStatType", CharacterStatType.Dodge);
                Raw(dodge, "modifierOperation", StatModifierOperation.Percent);
                Raw(dodge, "modifierValue", -.12f); Raw(dodge, "baseChance", .7f); Raw(dodge, "durationTurns", 3);
            }, "Tree");
            var plague = Skill("plague_growth", "Rozwój Plagi", 25, 30, 88, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundCaster, 9);
                Raw(so.FindProperty("targetModes").GetArrayElementAtIndex(0), "requiredStatusId", StatusEffectIds.Poison);
                var damage = Effect(so, EffectType.DealDamage);
                Raw(damage, "damageType", DamageType.Physical); Raw(damage, "damageMultiplier", .6f); Raw(damage, "alwaysHits", false);
                var extend = Effect(so, EffectType.ExtendStatusDuration);
                Raw(extend, "statusId", StatusEffectIds.Poison); Raw(extend, "durationTurns", 1);
                Field(so, "TooltipDescription", "Przedłuża zatrucie o 1 turę.");
            }, "Tree");
            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset");
            var tree = Asset<CharacterDefinition>("CD_Forest_InfectedTree");
            var data = new SerializedObject(tree);
            Field(data, "Id", "enemy.forest.infected_tree");
            Field(data, "DisplayName", "Zarażone Drzewo");
            Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Dps);
            Field(data, "AiPersonality", CombatAiPersonality.Neutral);
            Field(data, "BasicAttackSkill", roots);
            data.FindProperty(F("LootTable")).objectReferenceValue = null;
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Undead;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 450);
            Sub(stats, "Dodge", 11f); Sub(stats, "PhysicalDefense", 12f); Sub(stats, "MagicalDefense", 15f);
            Sub(stats, "CritChance", .06f); Sub(stats, "CritDamage", 1.6f); Sub(stats, "Speed", 30f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 30f);
            Sub(growth, "Dodge", .0825f); Sub(growth, "PhysicalDefense", 2.4f); Sub(growth, "MagicalDefense", 3f);
            Sub(growth, "CritChance", .00015f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", 1f); Sub(resist, "PoisonResistance", .7f);
            Sub(resist, "FireResistance", .1f); Sub(resist, "SuppressionResistance", .55f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { roots, bindingRoots, plague }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_InfectedTrees");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.infected_trees");
            Field(encounterData, "DisplayName", "Zarażone drzewa");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.infected_trees." + i);
                Sub(enemy, "CharacterDefinition", tree); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        private static void RegisterTreeIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var tree = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TreePath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(TreeEncounterPath);
            if (tree == null || encounter == null) return;
            if (!dungeon.EncounterDefinitions.Contains(encounter)) dungeon.EncounterDefinitions.Add(encounter);
            if (!catalog.CharacterDefinitions.Contains(tree)) catalog.CharacterDefinitions.Add(tree);
            if (!catalog.EncounterDefinitions.Contains(encounter)) catalog.EncounterDefinitions.Add(encounter);
            foreach (var skill in tree.StartingSkills)
                if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            EditorUtility.SetDirty(dungeon);
        }
    }
}
