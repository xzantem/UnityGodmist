using System;
using System.IO;
using System.Linq;
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
    /// <summary>Pass 1b gameplay authoring. Presentation fields belong to the art pass.</summary>
    public static partial class ForestEnemiesSetup
    {
        public const string Root = "Assets/_Project/ScriptableObjects/Dungeons/Forest/";
        public const string WolfPath = Root + "CD_Forest_RabidWolf.asset";
        public const string EncounterPath = Root + "ED_Forest_WolfPair.asset";
        public const string WolfId = "enemy.forest.rabid_wolf";
        private static string F(string name) => "<" + name + ">k__BackingField";

        private static T Asset<T>(string file) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + file + ".asset");
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, Root + file + ".asset");
            return asset;
        }

        private static void Set(SerializedProperty p, object value)
        {
            if (p == null) throw new InvalidOperationException("Forest: missing serialized field");
            switch (value)
            {
                case UnityEngine.Object asset: p.objectReferenceValue = asset; break;
                case string text: p.stringValue = text; break;
                case bool flag: p.boolValue = flag; break;
                case Enum e: p.enumValueIndex = Convert.ToInt32(e); break;
                case int number: p.intValue = number; break;
                case float number: p.floatValue = number; break;
                default: throw new ArgumentException("Unsupported forest value");
            }
        }
        private static void Field(SerializedObject so, string name, object value) => Set(so.FindProperty(F(name)), value);
        private static void Sub(SerializedProperty p, string name, object value) => Set(p.FindPropertyRelative(F(name)), value);
        private static void Raw(SerializedProperty p, string name, object value) => Set(p.FindPropertyRelative(name), value);
        private static SerializedProperty Add(SerializedProperty list)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            return list.GetArrayElementAtIndex(list.arraySize - 1);
        }
        private static void Save(SerializedObject so)
        {
            ContextualSkillHitSetup.Apply(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so.targetObject);
            if (so.targetObject is CharacterDefinition definition) EnemyResourceProfilesSetup.ApplyToDefinition(definition);
            if (so.targetObject is CharacterDefinition balancedEnemy) EnemyScalingBalanceSetup.ApplyToDefinition(balancedEnemy);
            if (so.targetObject is EncounterDefinition encounter) EnemyResourceProfilesSetup.ApplyToEncounter(encounter);
        }

        private static void Mode(SerializedObject so, TargetType type, int range)
        {
            var mode = Add(so.FindProperty("targetModes"));
            Raw(mode, "targetType", type);
            Raw(mode, "targetingType", type == TargetType.Caster ? TargetingType.Allies : TargetingType.Enemies);
            Raw(mode, "includesCaster", type == TargetType.Caster);
            var absolute = mode.FindPropertyRelative("absoluteRange");
            Raw(absolute, "shape", RangeShape.Square);
            Raw(absolute, "minRange", type == TargetType.Caster ? 0 : 1);
            Raw(absolute, "maxRange", range);
        }

        private static SerializedProperty Effect(SerializedObject so, EffectType type, int mode = 0)
        {
            var p = Add(so.FindProperty("effects"));
            Raw(p, "effectType", type);
            Raw(p, "targetModeIndex", mode);
            Raw(p, "baseChance", 1f);
            Raw(p, "alwaysHits", true);
            Raw(p, "damageMultiplier", 1f);
            Raw(p, "damageHitCount", 1);
            Raw(p, "durationTurns", 1);
            Raw(p, "stacks", 1);
            p.FindPropertyRelative("conditions").ClearArray();
            return p;
        }

        private static SkillData Skill(string key, string name, int mp, int ap, float accuracy,
            AttackType attack, Action<SerializedObject> configure, string family = "Wolf")
        {
            var skill = Asset<SkillData>("SD_Forest_" + family + "_" + key);
            var so = new SerializedObject(skill);
            Field(so, "SkillId", "skill.enemy.forest." + family.ToLowerInvariant() + "_" + key);
            Field(so, "DisplayName", name);
            Field(so, "TooltipDescription", ""); // Dynamic effects own the rules text.
            Field(so, "SkillAccuracy", accuracy);
            Field(so, "AttackType", attack);
            Field(so, "ResourceType", ResourceType.Mana);
            Field(so, "ResourceCost", mp);
            Field(so, "ActionCost", ap);
            Field(so, "RequiresLineOfSight", attack != AttackType.Unknown);
            so.FindProperty("targetModes").ClearArray();
            so.FindProperty("effects").ClearArray();
            configure(so);
            Save(so);
            return skill;
        }

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Wolf Gameplay")]
        public static void Configure()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var bite = Skill("bite", "Ugryzienie z Szarpaniem", 0, 55, 73, AttackType.Melee, so =>
            {
                Mode(so, TargetType.Single, 1);
                var damage = Effect(so, EffectType.DealDamage);
                Raw(damage, "alwaysHits", false);
                Raw(damage, "damageType", DamageType.Physical);
                Raw(damage, "damageValueSource", DamageValueSource.AttackRange);
                var bleed = Effect(so, EffectType.ApplyStatus);
                Raw(bleed, "statusId", StatusEffectIds.Bleed);
                Raw(bleed, "baseChance", .25f);
                Raw(bleed, "statusDamageMultiplier", .95f);
                Raw(bleed, "durationTurns", 2);
                var condition = Add(bleed.FindPropertyRelative("conditions"));
                Raw(condition, "conditionType", EffectConditionType.ReferencedEffectSucceeded);
                Raw(condition, "referencedEffectIndex", 0);
            });
            var leap = Skill("leap", "Głodny Doskok", 15, 20, 100, AttackType.Ranged, so =>
            {
                Mode(so, TargetType.Single, 3);
                Mode(so, TargetType.Caster, 0);
                Raw(Effect(so, EffectType.Charge), "displacementDistance", 2);
                var speed = Effect(so, EffectType.ModifyStat, 1);
                Raw(speed, "modifierStatType", CharacterStatType.Speed);
                Raw(speed, "modifierOperation", StatModifierOperation.FlatBase);
                Raw(speed, "modifierValue", 2f);
                Raw(speed, "durationTurns", 2);
                Raw(speed, "polarity", EffectPolarity.Positive);
            });
            var lick = Skill("lick_wounds", "Lizanie Ran", 30, 60, 100, AttackType.Unknown, so =>
            {
                Mode(so, TargetType.Caster, 0);
                var heal = Effect(so, EffectType.Heal);
                Raw(heal, "baseValue", 0);
                Raw(heal, "healAverageAttackMultiplier", .8f);
                Raw(heal, "polarity", EffectPolarity.Positive);
            });
            var cls = Asset<CharacterClassData>("CCD_ForestEnemy");
            var classData = new SerializedObject(cls);
            Field(classData, "Id", "class.enemy.forest");
            Field(classData, "ClassName", "Przeciwnik lasu");
            Field(classData, "ResourceType", ResourceType.Mana);
            Save(classData);

            var fang = Asset<ItemDefinition>("ID_Forest_WolfFang");
            var item = new SerializedObject(fang);
            Field(item, "Id", "item.material.wolf_fang");
            Field(item, "DisplayName", "Kieł wilka");
            Field(item, "Description", "Ostry kieł wykorzystywany do sporządzania mikstur.");
            Field(item, "ItemType", ItemType.Material);
            Field(item, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            Field(item, "BaseRarity", RarityTier.Uncommon);
            Field(item, "BaseValue", 273);
            Field(item, "IsStackable", true);
            Save(item);
            var loot = Asset<LootTableData>("LT_Forest_Wolf");
            var lootData = new SerializedObject(loot);
            var entries = lootData.FindProperty(F("Entries")); entries.ClearArray();
            var entry = Add(entries);
            Sub(entry, "ItemDefinition", fang); Sub(entry, "DropChance", .1f);
            var amount = entry.FindPropertyRelative(F("Amount"));
            Raw(amount, "Min", 1); Raw(amount, "Max", 1);
            Save(lootData);

            var wolf = Asset<CharacterDefinition>("CD_Forest_RabidWolf");
            var data = new SerializedObject(wolf);
            Field(data, "Id", WolfId); Field(data, "DisplayName", "Wściekły Wilk");
            Field(data, "ClassData", cls); Field(data, "AiRole", CombatAiRole.Dps);
            Field(data, "AiPersonality", CombatAiPersonality.Aggressive);
            Field(data, "BasicAttackSkill", bite); Field(data, "LootTable", loot);
            var tags = data.FindProperty(F("Tags")); tags.ClearArray(); Add(tags).enumValueIndex = (int)CharacterTag.Beast;
            var stats = data.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 190); Sub(stats, "MinAttack", 36); Sub(stats, "MaxAttack", 72);
            Sub(stats, "Dodge", 25f); Sub(stats, "PhysicalDefense", 5f); Sub(stats, "MagicalDefense", 2f);
            Sub(stats, "CritChance", .14f); Sub(stats, "CritDamage", 2.1f); Sub(stats, "Speed", 64f);
            Sub(stats, "WeaponAccuracy", 100f); Sub(stats, "MaxResource", 100); Sub(stats, "ResourceRegeneration", 15);
            var growth = data.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 13f); Sub(growth, "MinAttack", 2.34f); Sub(growth, "MaxAttack", 4.68f);
            Sub(growth, "Dodge", .1875f); Sub(growth, "PhysicalDefense", 1f); Sub(growth, "MagicalDefense", .4f);
            Sub(growth, "CritChance", .00035f);
            var resist = data.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .25f); Sub(resist, "PoisonResistance", .4f);
            Sub(resist, "FireResistance", .3f); Sub(resist, "SuppressionResistance", .15f);
            var skills = data.FindProperty(F("StartingSkills")); skills.ClearArray();
            foreach (var skill in new[] { bite, leap, lick }) Add(skills).objectReferenceValue = skill;
            Save(data);

            var encounter = Asset<EncounterDefinition>("ED_Forest_WolfPair");
            var encounterData = new SerializedObject(encounter);
            Field(encounterData, "Id", "encounter.forest.wolf_pair"); Field(encounterData, "DisplayName", "Wataha wilków");
            Field(encounterData, "GridWidth", 12); Field(encounterData, "GridHeight", 12);
            var enemies = encounterData.FindProperty(F("Enemies")); enemies.ClearArray();
            for (var i = 0; i < 2; i++)
            {
                var enemy = Add(enemies);
                Sub(enemy, "InstanceId", "enemy.forest.wolf_pair." + i);
                Sub(enemy, "CharacterDefinition", wolf); Sub(enemy, "ClassData", cls);
                enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 3 + i * 3);
                Sub(enemy, "VisionRange", 6f); Sub(enemy, "VisionAngle", 120f);
                enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            }
            Save(encounterData);
            AssetDatabase.SaveAssets();
            ForestPrototypeSetup.Configure();
        }

        public static void WireIfAuthored(DungeonDefinition dungeon, DungeonRoomTemplateCatalog rooms, GameContentCatalog catalog)
        {
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(EncounterPath);
            var wolf = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(WolfPath);
            if (encounter == null || wolf == null) return;
            if (!dungeon.EncounterDefinitions.Contains(encounter)) dungeon.EncounterDefinitions.Add(encounter);
            var data = new SerializedObject(dungeon);
            Field(data, "MinEncounterCountPerFloor", 1); Field(data, "MaxEncounterCountPerFloor", 2);
            Field(data, "AllowEncounterDefinitionRepeats", true); Save(data);
            foreach (var corridor in rooms.Templates.Where(r => r.RoomType == DungeonRoomType.Corridor))
            {
                var roomData = new SerializedObject(corridor);
                Field(roomData, "ExcludeFromRandomEncounters", true); Save(roomData);
            }
            foreach (var room in rooms.Templates.Where(r => r.RoomType == DungeonRoomType.Normal || r.RoomType == DungeonRoomType.Optional))
            {
                room.SemanticAnchors.RemoveAll(a => a.Id.StartsWith("forest.wolf.", StringComparison.Ordinal));
                var reserved = room.SemanticAnchors.Select(a => a.LocalCell).ToList();
                var center = (Vector2)room.SizeInCells * .5f;
                for (var i = 0; i < 2; i++)
                {
                    var candidates = room.Footprint.FloorCellOffsets.Where(c => !reserved.Contains(c)
                        && !room.DifficultTerrainCells.Contains(c))
                        .OrderBy(c => ((Vector2)c-center).sqrMagnitude).ThenBy(c => c.y).ThenBy(c => c.x).ToArray();
                    if (candidates.Length == 0) throw new InvalidOperationException("No wolf anchor in " + room.Id);
                    var cell = candidates[0]; reserved.Add(cell);
                    room.SemanticAnchors.Add(new DungeonSemanticAnchorData("forest.wolf." + i,
                        DungeonSemanticAnchorType.Enemy, cell, Vector2Int.one, encounterEnemyIndex: i));
                }
                EditorUtility.SetDirty(room);
            }
            if (!catalog.CharacterDefinitions.Contains(wolf)) catalog.CharacterDefinitions.Add(wolf);
            if (!catalog.CharacterClasses.Contains(wolf.ClassData)) catalog.CharacterClasses.Add(wolf.ClassData);
            if (!catalog.EncounterDefinitions.Contains(encounter)) catalog.EncounterDefinitions.Add(encounter);
            foreach (var skill in wolf.StartingSkills)
                if (!catalog.SkillDefinitions.Contains(skill)) catalog.SkillDefinitions.Add(skill);
            foreach (var entry in wolf.LootTable.Entries)
                if (!catalog.ItemDefinitions.Contains(entry.ItemDefinition)) catalog.ItemDefinitions.Add(entry.ItemDefinition);
            RegisterBanditIfAuthored(dungeon, catalog);
            RegisterShooterIfAuthored(dungeon, catalog);
            RegisterArcherIfAuthored(dungeon, catalog);
            RegisterPoacherIfAuthored(dungeon, catalog);
            RegisterTreeIfAuthored(dungeon, catalog);
            RegisterDruidIfAuthored(dungeon, catalog);
            ForestLootSetup.WireIfAuthored(dungeon);
            RegisterEntIfAuthored(dungeon, catalog);
            EncounterBalanceSetup.ApplyToDungeon(dungeon, rooms, catalog);
            EditorUtility.SetDirty(catalog);
        }
    }
}
