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
        public const string ShooterPath = Root + "CD_Forest_BanditShooter.asset";
        public const string ShooterEncounterPath = Root + "ED_Forest_BanditPatrol.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Shooter Gameplay")]
        public static void ConfigureShooter()
        {
            ConfigureBandit(); // Retains existing presentation fields.
            var shot = Skill("shot", "Wystrzał z Kuszy", 0, 65, 78, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Single, 9);
                Raw(Effect(so, EffectType.DealDamage), "alwaysHits", false);
                var slow = Effect(so, EffectType.ModifyStat);
                Raw(slow, "baseChance", .2f);
                Raw(slow, "modifierStatType", CharacterStatType.Speed);
                Raw(slow, "modifierOperation", StatModifierOperation.FlatBase);
                Raw(slow, "modifierValue", -7f);
                Raw(slow, "durationTurns", 2);
                RequireShooterHit(slow);
            }, "Shooter");
            var bolt = Skill("piercing_bolt", "Bełt Przebijający", 25, 65, 76, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.AreaAroundCaster, 8);
                var range = so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange");
                Raw(range, "shape", RangeShape.Rectangle);
                Raw(range, "rectangleWidth", 2);
                var damage = Effect(so, EffectType.DealDamage);
                Raw(damage, "alwaysHits", false); Raw(damage, "damageMultiplier", .7f);
                var bleed = Effect(so, EffectType.ApplyStatus);
                Raw(bleed, "statusId", StatusEffectIds.Bleed);
                Raw(bleed, "baseChance", .8f);
                Raw(bleed, "statusDamageMultiplier", .4f);
                Raw(bleed, "durationTurns", 2);
                RequireShooterHit(bleed);
            }, "Shooter");
            var flare = Skill("flare", "Flara", 50, 35, 86, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.All, 0);
                Mode(so, TargetType.All, 0);
                var allyMode = so.FindProperty("targetModes").GetArrayElementAtIndex(1);
                Raw(allyMode, "targetingType", TargetingType.Allies);
                Raw(allyMode, "includesCaster", true);
                var accuracy = Effect(so, EffectType.ModifyStat);
                Raw(accuracy, "alwaysHits", false); Raw(accuracy, "baseChance", .7f);
                Raw(accuracy, "modifierStatType", CharacterStatType.WeaponAccuracy);
                Raw(accuracy, "modifierOperation", StatModifierOperation.FlatBase);
                Raw(accuracy, "modifierValue", -15f); Raw(accuracy, "durationTurns", 2);
                var stun = Effect(so, EffectType.ApplyStatus);
                Raw(stun, "alwaysHits", false); Raw(stun, "statusId", StatusEffectIds.Stun);
                Raw(stun, "baseChance", .3f); Raw(stun, "durationTurns", 1);
                var cleanse = Effect(so, EffectType.RemoveStatus, 1);
                Raw(cleanse, "statusId", StatusEffectIds.Stun);
                Raw(cleanse, "polarity", EffectPolarity.Positive);
            }, "Shooter");

            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root + "CCD_ForestEnemy.asset");
            var bandit = Asset<CharacterDefinition>("CD_Forest_BanditShooter");
            var data = new SerializedObject(bandit);
            Field(data, "Id", "enemy.forest.bandit_shooter");
            Field(data, "DisplayName", "Bandyta Strzelec");
            Field(data, "ClassData", cls);
            Field(data, "AiRole", CombatAiRole.Dps);
            Field(data, "AiPersonality", CombatAiPersonality.Aggressive);
            Field(data, "BasicAttackSkill", shot);
            var tags = data.FindProperty(F("Tags")); tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Human;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 270); Sub(stats, "MinAttack", 45); Sub(stats, "MaxAttack", 70);
            Sub(stats, "Dodge", 19f); Sub(stats, "PhysicalDefense", 5f); Sub(stats, "MagicalDefense", 4f);
            Sub(stats, "CritChance", .11f); Sub(stats, "CritDamage", 2f); Sub(stats, "Speed", 55f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 18f); Sub(growth, "MinAttack", 2.93f); Sub(growth, "MaxAttack", 4.55f);
            Sub(growth, "Dodge", .1425f); Sub(growth, "PhysicalDefense", 1f); Sub(growth, "MagicalDefense", .8f);
            Sub(growth, "CritChance", .000275f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .35f); Sub(resist, "PoisonResistance", .3f);
            Sub(resist, "FireResistance", .45f); Sub(resist, "SuppressionResistance", .3f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { shot, bolt, flare }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_BanditPatrol");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.bandit_patrol");
            Field(encounterData, "DisplayName", "Bandycki patrol");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.bandit_patrol." + i);
                Sub(enemy, "CharacterDefinition", i == 0 ? bandit : AssetDatabase.LoadAssetAtPath<CharacterDefinition>(BanditPath)); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        private static void RequireShooterHit(SerializedProperty effect)
        {
            var condition = Add(effect.FindPropertyRelative("conditions"));
            Raw(condition, "conditionType", EffectConditionType.ReferencedEffectSucceeded);
            Raw(condition, "referencedEffectIndex", 0);
        }

        private static void RegisterShooterIfAuthored(DungeonDefinition dungeon, GameContentCatalog catalog)
        {
            var bandit = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ShooterPath);
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(ShooterEncounterPath);
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
