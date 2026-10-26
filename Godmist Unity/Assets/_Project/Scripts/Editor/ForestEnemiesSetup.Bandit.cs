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
        public const string BanditPath = Root + "CD_Forest_BanditThug.asset";
        public const string BanditEncounterPath = Root + "ED_Forest_BanditPair.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Bandit Gameplay")]
        public static void ConfigureBandit()
        {
            Configure(); // Creates shared class and retains authored presentation fields.
            var knife = Skill("knife", "Bandycki Nóż", 0, 60, 74, AttackType.Melee, so =>
            {
                Mode(so, TargetType.Single, 1);
                Raw(Effect(so, EffectType.DealDamage), "alwaysHits", false);
                var bleed = Effect(so, EffectType.ApplyStatus);
                Raw(bleed, "statusId", StatusEffectIds.Bleed);
                Raw(bleed, "baseChance", .25f);
                Raw(bleed, "statusDamageMultiplier", .75f);
                Raw(bleed, "durationTurns", 3);
                var condition = Add(bleed.FindPropertyRelative("conditions"));
                Raw(condition, "conditionType", EffectConditionType.ReferencedEffectSucceeded);
                Raw(condition, "referencedEffectIndex", 0);
            }, "Bandit");
            var ambush = Skill("ambush", "Zasadzka", 45, 25, 100, AttackType.Unknown, so =>
            {
                Mode(so, TargetType.Caster, 0);
                var stealth = Effect(so, EffectType.ApplyStatus);
                Raw(stealth, "statusId", StatusEffectIds.Invisibility);
                Raw(stealth, "durationTurns", 2);
                Raw(stealth, "polarity", EffectPolarity.Positive);
                var crit = Effect(so, EffectType.ModifyStat);
                Raw(crit, "modifierStatType", CharacterStatType.CritChance);
                Raw(crit, "modifierOperation", StatModifierOperation.FlatBase);
                Raw(crit, "modifierValue", .1f);
                Raw(crit, "durationTurns", 2);
                Raw(crit, "polarity", EffectPolarity.Positive);
            }, "Bandit");
            var backstab = Skill("backstab", "Cios w Plecy", 35, 70, 86, AttackType.Melee, so =>
            {
                Mode(so, TargetType.Single, 1);
                var damage = Effect(so, EffectType.DealDamage);
                Raw(damage, "alwaysHits", false);
                Raw(damage, "rearAttackDamageBonus", .5f);
            }, "Bandit");

            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset");
            var bandit = Asset<CharacterDefinition>("CD_Forest_BanditThug");
            var data = new SerializedObject(bandit);
            Field(data, "Id", "enemy.forest.bandit_thug");
            Field(data, "DisplayName", "Bandyta Rzezimieszek");
            Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Dps);
            Field(data, "AiPersonality", CombatAiPersonality.Aggressive);
            Field(data, "BasicAttackSkill", knife);
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Human;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 350); Sub(stats, "MinAttack", 35); Sub(stats, "MaxAttack", 55);
            Sub(stats, "Dodge", 17f); Sub(stats, "PhysicalDefense", 10f); Sub(stats, "MagicalDefense", 6f);
            Sub(stats, "CritChance", .09f); Sub(stats, "CritDamage", 1.85f); Sub(stats, "Speed", 52f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 23f); Sub(growth, "MinAttack", 2.28f); Sub(growth, "MaxAttack", 3.58f);
            Sub(growth, "Dodge", .1275f); Sub(growth, "PhysicalDefense", 2f); Sub(growth, "MagicalDefense", 1.2f);
            Sub(growth, "CritChance", .000225f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .45f); Sub(resist, "PoisonResistance", .3f);
            Sub(resist, "FireResistance", .5f); Sub(resist, "SuppressionResistance", .35f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { knife, ambush, backstab }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_BanditPair");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.bandit_pair");
            Field(encounterData, "DisplayName", "Bandycka zasadzka");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.bandit_pair." + i);
                Sub(enemy, "CharacterDefinition", bandit); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        private static void RegisterBanditIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var bandit = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(BanditPath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(BanditEncounterPath);
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
