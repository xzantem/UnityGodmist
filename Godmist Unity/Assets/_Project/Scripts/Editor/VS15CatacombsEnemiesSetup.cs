using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Repeatable authoring for the seven regular Catacombs enemies.</summary>
    public static class VS15CatacombsEnemiesSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";
        private const string EnemyRoot = Root + "Catacombs/Enemies/";
        private const string SkillRoot = Root + "Catacombs/Skills/";
        private const string EncounterRoot = Root + "Catacombs/Encounters/";

        private static string F(string field) => "<" + field + ">k__BackingField";

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("VS-15: missing " + path);

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void Set(SerializedProperty property, object value)
        {
            if (property == null) throw new InvalidOperationException("VS-15: missing serialized field");
            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case UnityEngine.Object obj: property.objectReferenceValue = obj; break;
                case string str: property.stringValue = str; break;
                case bool flag: property.boolValue = flag; break;
                case Enum enumeration: property.enumValueIndex = Convert.ToInt32(enumeration); break;
                case int integer: property.intValue = integer; break;
                case float number: property.floatValue = number; break;
                default: throw new ArgumentException("Unsupported VS-15 serialized value: " + value);
            }
        }

        private static void Field(SerializedObject data, string name, object value) => Set(data.FindProperty(F(name)), value);
        private static void Sub(SerializedProperty data, string name, object value) => Set(data.FindPropertyRelative(F(name)), value);
        private static void Raw(SerializedProperty data, string name, object value) => Set(data.FindPropertyRelative(name), value);

        private static SerializedProperty Add(SerializedProperty list)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            return list.GetArrayElementAtIndex(list.arraySize - 1);
        }

        private static void Mode(SerializedObject skill, TargetType type, TargetingType side,
            int range, bool caster = false)
        {
            var mode = Add(skill.FindProperty("targetModes"));
            Raw(mode, "targetType", type);
            Raw(mode, "targetingType", side);
            Raw(mode, "includesCaster", caster);
            var absolute = mode.FindPropertyRelative("absoluteRange");
            Raw(absolute, "shape", RangeShape.Radial);
            Raw(absolute, "minRange", type == TargetType.Caster || type == TargetType.All ? 0 : 1);
            Raw(absolute, "maxRange", range);
        }

        private static SerializedProperty Effect(SerializedObject skill, EffectType type, int mode = 0)
        {
            var effect = Add(skill.FindProperty("effects"));
            Raw(effect, "effectType", type);
            Raw(effect, "targetModeIndex", mode);
            Raw(effect, "baseChance", 1f);
            Raw(effect, "damageMultiplier", 1f);
            Raw(effect, "damageHitCount", 1);
            Raw(effect, "durationTurns", 1);
            Raw(effect, "stacks", 1);
            Raw(effect, "alwaysHits", type != EffectType.DealDamage);
            effect.FindPropertyRelative("conditions").ClearArray();
            return effect;
        }

        private static SerializedProperty Damage(SerializedObject skill, DamageType type,
            float multiplier = 1f, int hits = 1)
        {
            var effect = Effect(skill, EffectType.DealDamage);
            Raw(effect, "damageType", type);
            Raw(effect, "damageMultiplier", multiplier);
            Raw(effect, "damageHitCount", hits);
            return effect;
        }

        private static SerializedProperty Status(SerializedObject skill, string id,
            float chance, int turns, float strength = 0f, int mode = 0)
        {
            var effect = Effect(skill, EffectType.ApplyStatus, mode);
            Raw(effect, "statusId", id);
            Raw(effect, "baseChance", chance);
            Raw(effect, "durationTurns", turns);
            Raw(effect, "statusDamageMultiplier", strength);
            return effect;
        }

        private static SerializedProperty Stat(SerializedObject skill, CharacterStatType stat,
            float value, int turns, int mode = 0,
            StatModifierOperation operation = StatModifierOperation.FlatBase)
        {
            var effect = Effect(skill, EffectType.ModifyStat, mode);
            Raw(effect, "modifierStatType", stat);
            Raw(effect, "modifierValue", value);
            Raw(effect, "modifierOperation", operation);
            Raw(effect, "durationTurns", turns);
            return effect;
        }

        private static SerializedProperty Variable(SerializedObject skill, GameplayVariableType variable,
            float value, int turns, int mode = 0)
        {
            var effect = Effect(skill, EffectType.ModifyVar, mode);
            Raw(effect, "modifierVariableType", variable);
            Raw(effect, "modifierValue", value);
            Raw(effect, "durationTurns", turns);
            return effect;
        }

        private static void Condition(SerializedProperty effect, EffectConditionType type,
            string statusId = null, int referencedEffect = -1)
        {
            var condition = Add(effect.FindPropertyRelative("conditions"));
            Raw(condition, "conditionType", type);
            Raw(condition, "statusId", statusId ?? string.Empty);
            Raw(condition, "referencedEffectIndex", referencedEffect);
        }

        private static SkillData Skill(string key, string name, int mp, int ap, float accuracy,
            TargetType target, TargetingType side, int range, Action<SerializedObject> effects,
            AttackType attack = AttackType.Unknown, string requiredStatus = "")
        {
            var skill = GetOrCreate<SkillData>(SkillRoot + "SD_Catacombs_" + key + ".asset");
            var so = new SerializedObject(skill);
            Field(so, "SkillId", "skill.enemy.catacombs." + key.ToLowerInvariant());
            Field(so, "DisplayName", name);
            Field(so, "TooltipDescription", Description(key));
            Field(so, "SkillAccuracy", accuracy);
            Field(so, "AttackType", attack);
            Field(so, "ActionCost", ap);
            Field(so, "ResourceCost", mp);
            Field(so, "ResourceType", ResourceType.Mana);
            Field(so, "RequiresLineOfSight", target == TargetType.Single);
            Field(so, "RequiredCasterStatusId", requiredStatus);
            so.FindProperty("targetModes").ClearArray();
            so.FindProperty("effects").ClearArray();
            Mode(so, target, side, range, target == TargetType.Caster || target == TargetType.All);
            effects(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            var report = CombatDataValidator.ValidateSkill(skill);
            if (!report.IsValid)
                throw new InvalidOperationException(skill.SkillId + ": " + string.Join("; ", report.Errors));
            return skill;
        }

        private static string Description(string key) => key switch
        {
            "bone_sword" => "Zadaje losowe obrażenia fizyczne w zwarciu.",
            "cross_cut" => "Dwa ciosy po 60% obrażeń. Każde trafienie osobno ma 60% szansy na krwawienie o sile 85% przez 3 tury.",
            "bone_fervor" => "Sojusznicy zadają o 6% więcej obrażeń i mają +4 szybkości przez 2 tury; ich następny ruch przyspiesza o 15%.",
            "manic_bite" => "Zadaje losowe obrażenia i ma 80% szansy na krwawienie o sile 30% przez 2 tury.",
            "vomit" => "Ma 90% szansy na zatrucie o sile 75% i obniża szybkość celu o 8 na 3 tury.",
            "blood_scent" => "Zwiększa szybkość o 16 na 2 tury, jeśli którykolwiek wróg krwawi; w przeciwnym razie o 4.",
            "fright" => "Doskok o 2 pola i losowe obrażenia magiczne.",
            "dematerialize" => "Staje się niewidzialny na 3 tury.",
            "materialize" => "Gdy jest niewidzialny, ujawnia się, leczy 15% maksymalnego HP i zwiększa obrażenia o 50% na 1 turę.",
            "ancient_incantation" => "Zadaje losowe obrażenia magiczne i ma 25% szansy na unieruchomienie na 1 turę.",
            "life_drain" => "Zadaje 150% losowych obrażeń magicznych. Każdy sojusznik odzyskuje 30% zadanych obrażeń jako HP.",
            "ancient_whip" => "Odrzuca cel o 3 pola i ma 70% szansy na krwawienie o sile 90% przez 2 tury.",
            "bone_bow" => "Zadaje losowe obrażenia fizyczne z dystansu.",
            "burning_arrow" => "Zadaje losowe obrażenia i ma 90% szansy na podpalenie o sile 100% przez 3 tury.",
            "quiver_of_envy" => "Zwiększa szansę na trafienie krytyczne o 10% i obrażenia o 20% na 3 tury.",
            "rend" => "Zadaje 60% losowych obrażeń i ma 40% szansy na krwawienie o sile 65% przez 2 tury.",
            "savage_roar" => "Wrogowie w zasięgu 6: 40% szansy na ogłuszenie na 1 turę i 90% szansy na krwawienie o sile 80% przez 3 tury.",
            "disorienting_leap" => "Doskok do wroga w zasięgu 2 i +12 uniku na 2 tury.",
            "bone_club" => "Zadaje losowe obrażenia i ma 30% szansy na ogłuszenie na 1 turę.",
            "battle_fervor" => "Sojusznicy zyskują 50% penetracji obrony fizycznej na 3 tury; ich następny ruch przyspiesza o 10%.",
            "bone_guardian" => "Przez 3 tury przejmuje 70% obrażeń otrzymanych przez przyległych sojuszników.",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Missing enemy skill description")
        };

        private static Dictionary<string, SkillData[]> ConfigureSkills()
        {
            var skills = new Dictionary<string, SkillData[]>();
            skills["swordsman"] = new[]
            {
                Skill("bone_sword", "Kościany Miecz", 0, 60, 79, TargetType.Single,
                    TargetingType.Enemies, 1, s => Damage(s, DamageType.Physical), AttackType.Melee),
                Skill("cross_cut", "Cięcie Krzyżowe", 50, 60, 72, TargetType.Single,
                    TargetingType.Enemies, 1, s => {
                        var damage = Damage(s, DamageType.Physical, .6f, 2);
                        Raw(damage, "onHitStatusId", StatusEffectIds.Bleed);
                        Raw(damage, "onHitStatusChance", .6f);
                        Raw(damage, "onHitStatusDurationTurns", 3);
                        Raw(damage, "statusDamageMultiplier", .85f);
                    }, AttackType.Melee),
                Skill("bone_fervor", "Kościany Zapał", 20, 30, 100, TargetType.All,
                    TargetingType.Allies, 0, s => {
                        Variable(s, GameplayVariableType.DamageDealtPercent, .06f, 2);
                        Stat(s, CharacterStatType.Speed, 4f, 2);
                        var av = Effect(s, EffectType.ModifyActionValue);
                        Raw(av, "actionValuePercent", -.15f);
                    })
            };
            skills["undead"] = new[]
            {
                Skill("manic_bite", "Maniakalne Ugryzienie", 0, 65, 69, TargetType.Single,
                    TargetingType.Enemies, 1, s => {
                        Damage(s, DamageType.Physical);
                        var bleed = Status(s, StatusEffectIds.Bleed, .8f, 2, .3f);
                        Condition(bleed, EffectConditionType.ReferencedEffectSucceeded, referencedEffect: 0);
                    }, AttackType.Melee),
                Skill("vomit", "Wymiociny", 40, 60, 78, TargetType.Single,
                    TargetingType.Enemies, 1, s => {
                        var poison = Status(s, StatusEffectIds.Poison, .9f, 3, .75f);
                        Raw(poison, "alwaysHits", false);
                        var slow = Stat(s, CharacterStatType.Speed, -8f, 3);
                        Raw(slow, "alwaysHits", false);
                    }, AttackType.Melee),
                Skill("blood_scent", "Zapach Krwi", 20, 35, 100, TargetType.Caster,
                    TargetingType.Allies, 0, s => {
                        Condition(Stat(s, CharacterStatType.Speed, 16f, 2),
                            EffectConditionType.AnyOpponentHasStatus, StatusEffectIds.Bleed);
                        Condition(Stat(s, CharacterStatType.Speed, 4f, 2),
                            EffectConditionType.NoOpponentHasStatus, StatusEffectIds.Bleed);
                    })
            };
            skills["wraith"] = new[]
            {
                Skill("fright", "Nastraszenie", 5, 55, 100, TargetType.Single,
                    TargetingType.Enemies, 3, s => {
                        var charge = Effect(s, EffectType.Charge);
                        Raw(charge, "displacementDistance", 2);
                        Damage(s, DamageType.Magical);
                    }, AttackType.Ranged),
                Skill("dematerialize", "Dematerializacja", 50, 100, 100, TargetType.Caster,
                    TargetingType.Allies, 0, s => Status(s, StatusEffectIds.Invisibility, 1f, 3)),
                Skill("materialize", "Materializacja", 20, 45, 100, TargetType.Caster,
                    TargetingType.Allies, 0, s => {
                        Variable(s, GameplayVariableType.DamageDealtPercent, .5f, 1);
                        var heal = Effect(s, EffectType.HealPercent);
                        Raw(heal, "percentageValue", .15f);
                        var reveal = Effect(s, EffectType.RemoveStatus);
                        Raw(reveal, "statusId", StatusEffectIds.Invisibility);
                    }, requiredStatus: StatusEffectIds.Invisibility)
            };
            skills["occultist"] = new[]
            {
                Skill("ancient_incantation", "Starożytna Inkantacja", 5, 65, 100,
                    TargetType.Single, TargetingType.Enemies, 9, s => {
                        Damage(s, DamageType.Magical);
                        var root = Status(s, StatusEffectIds.Immobilize, .25f, 1);
                        Condition(root, EffectConditionType.ReferencedEffectSucceeded, referencedEffect: 0);
                    }, AttackType.Ranged),
                Skill("life_drain", "Wyssanie Życia", 50, 70, 100, TargetType.Single,
                    TargetingType.Enemies, 9, s => {
                        var damage = Damage(s, DamageType.Magical, 1.5f);
                        Raw(damage, "allyLifeStealPercent", .3f);
                    }, AttackType.Ranged),
                Skill("ancient_whip", "Starożytny Bicz", 30, 40, 100, TargetType.Single,
                    TargetingType.Enemies, 3, s => {
                        var push = Effect(s, EffectType.Push);
                        Raw(push, "displacementDistance", 3);
                        Status(s, StatusEffectIds.Bleed, .7f, 2, .9f);
                    }, AttackType.Ranged)
            };
            skills["archer"] = new[]
            {
                Skill("bone_bow", "Łuk z Kości", 0, 55, 95, TargetType.Single,
                    TargetingType.Enemies, 7, s => Damage(s, DamageType.Physical), AttackType.Ranged),
                Skill("burning_arrow", "Podpalona Strzała", 30, 55, 92,
                    TargetType.Single, TargetingType.Enemies, 7, s => {
                        Damage(s, DamageType.Physical);
                        var burn = Status(s, StatusEffectIds.Burn, .9f, 3, 1f);
                        Condition(burn, EffectConditionType.ReferencedEffectSucceeded, referencedEffect: 0);
                    }, AttackType.Ranged),
                Skill("quiver_of_envy", "Kołczan Zawiści", 65, 40, 100,
                    TargetType.Caster, TargetingType.Allies, 0, s => {
                        Stat(s, CharacterStatType.CritChance, .1f, 3);
                        Variable(s, GameplayVariableType.DamageDealtPercent, .2f, 3);
                    })
            };
            skills["ghoul"] = new[]
            {
                Skill("rend", "Rozszarpanie", 0, 35, 68, TargetType.Single,
                    TargetingType.Enemies, 1, s => {
                        Damage(s, DamageType.Physical, .6f);
                        var bleed = Status(s, StatusEffectIds.Bleed, .4f, 2, .65f);
                        Condition(bleed, EffectConditionType.ReferencedEffectSucceeded, referencedEffect: 0);
                    }, AttackType.Melee),
                Skill("savage_roar", "Dziki Ryk", 60, 70, 70, TargetType.AreaAroundCaster,
                    TargetingType.Enemies, 6, s => {
                        Raw(Status(s, StatusEffectIds.Stun, .4f, 1), "alwaysHits", false);
                        Raw(Status(s, StatusEffectIds.Bleed, .9f, 3, .8f), "alwaysHits", false);
                    }),
                Skill("disorienting_leap", "Dezorientujący Skok", 40, 30, 100,
                    TargetType.Single, TargetingType.Enemies, 2, s => {
                        var charge = Effect(s, EffectType.Charge);
                        Raw(charge, "displacementDistance", 2);
                        Mode(s, TargetType.Caster, TargetingType.Allies, 0, true);
                        Stat(s, CharacterStatType.Dodge, 12f, 2, 1);
                    }, AttackType.Melee)
            };
            skills["defender"] = new[]
            {
                Skill("bone_club", "Kościana Maczuga", 0, 65, 74, TargetType.Single,
                    TargetingType.Enemies, 1, s => {
                        Damage(s, DamageType.Physical);
                        var stun = Status(s, StatusEffectIds.Stun, .3f, 1);
                        Condition(stun, EffectConditionType.ReferencedEffectSucceeded, referencedEffect: 0);
                    }, AttackType.Melee),
                Skill("battle_fervor", "Ferwor Bitewny", 55, 35, 100, TargetType.All,
                    TargetingType.Allies, 0, s => {
                        Stat(s, CharacterStatType.PhysicalDefensePenetration, .5f, 3);
                        var av = Effect(s, EffectType.ModifyActionValue);
                        Raw(av, "actionValuePercent", -.1f);
                    }),
                Skill("bone_guardian", "Kościany Obrońca", 40, 50, 100,
                    TargetType.AreaAroundCaster, TargetingType.Allies, 1, s => {
                        var guard = Effect(s, EffectType.ApplyUniqueBuff);
                        Raw(guard, "uniqueBuffType", UniqueBuffType.DamageRedirect);
                        Raw(guard, "redirectedDamagePercent", .7f);
                        Raw(guard, "durationTurns", 3);
                    })
            };
            return skills;
        }

        private sealed class EnemySpec
        {
            public string Key, Name;
            public int Health, AttackMin, AttackMax;
            public float HealthGrowth, MinGrowth, MaxGrowth, Dodge, DodgeGrowth,
                PhysicalDefense, PhysicalGrowth, MagicDefense, MagicGrowth,
                Crit, CritGrowth, CritMultiplier, Bleed, Poison, Fire, Suppression, Speed;
            public CombatAiRole Role;
            public CombatAiPersonality Personality;
        }

        private static readonly EnemySpec[] Enemies =
        {
            new() { Key="swordsman", Name="Szkielet Miecznik", Health=350, HealthGrowth=23,
                AttackMin=42, AttackMax=64, MinGrowth=2.73f, MaxGrowth=4.16f,
                Dodge=15, DodgeGrowth=.1125f, PhysicalDefense=10, PhysicalGrowth=2,
                MagicDefense=4, MagicGrowth=.8f, Crit=.11f, CritGrowth=.000275f,
                CritMultiplier=2, Bleed=1, Poison=.2f, Fire=.4f, Suppression=.25f,
                Speed=44, Role=CombatAiRole.Balanced, Personality=CombatAiPersonality.Aggressive },
            new() { Key="undead", Name="Umarlak", Health=500, HealthGrowth=33,
                AttackMin=30, AttackMax=48, MinGrowth=1.95f, MaxGrowth=3.12f,
                Dodge=4, DodgeGrowth=.03f, PhysicalDefense=13, PhysicalGrowth=2.6f,
                MagicDefense=11, MagicGrowth=2.2f, Crit=.09f, CritGrowth=.000225f,
                CritMultiplier=1.7f, Bleed=.65f, Poison=.6f, Fire=.1f, Suppression=.4f,
                Speed=32, Role=CombatAiRole.Tank, Personality=CombatAiPersonality.Neutral },
            new() { Key="wraith", Name="Upiór", Health=270, HealthGrowth=18,
                AttackMin=25, AttackMax=75, MinGrowth=1.63f, MaxGrowth=4.88f,
                Dodge=25, DodgeGrowth=.1875f, PhysicalDefense=3, PhysicalGrowth=.6f,
                MagicDefense=16, MagicGrowth=3.2f, Crit=.08f, CritGrowth=.0002f,
                CritMultiplier=1.8f, Bleed=.65f, Poison=.2f, Fire=1, Suppression=1,
                Speed=60, Role=CombatAiRole.Dps, Personality=CombatAiPersonality.Cautious },
            new() { Key="occultist", Name="Szkielet Okultysta", Health=250, HealthGrowth=17,
                AttackMin=55, AttackMax=80, MinGrowth=3.58f, MaxGrowth=5.2f,
                Dodge=11, DodgeGrowth=.0825f, PhysicalDefense=4, PhysicalGrowth=.8f,
                MagicDefense=14, MagicGrowth=2.8f, Crit=.06f, CritGrowth=.00015f,
                CritMultiplier=1.85f, Bleed=1, Poison=.2f, Fire=.4f, Suppression=.35f,
                Speed=50, Role=CombatAiRole.Support, Personality=CombatAiPersonality.Cautious },
            new() { Key="archer", Name="Szkielet Łucznik", Health=300, HealthGrowth=20,
                AttackMin=40, AttackMax=60, MinGrowth=2.6f, MaxGrowth=3.9f,
                Dodge=20, DodgeGrowth=.15f, PhysicalDefense=7, PhysicalGrowth=1.4f,
                MagicDefense=5, MagicGrowth=1, Crit=.12f, CritGrowth=.0003f,
                CritMultiplier=2.1f, Bleed=1, Poison=.2f, Fire=.4f, Suppression=.2f,
                Speed=56, Role=CombatAiRole.Dps, Personality=CombatAiPersonality.Cautious },
            new() { Key="ghoul", Name="Ghul", Health=400, HealthGrowth=26,
                AttackMin=36, AttackMax=66, MinGrowth=2.34f, MaxGrowth=4.29f,
                Dodge=7, DodgeGrowth=.0525f, PhysicalDefense=11, PhysicalGrowth=2.2f,
                MagicDefense=12, MagicGrowth=2.4f, Crit=.08f, CritGrowth=.0002f,
                CritMultiplier=1.8f, Bleed=.3f, Poison=.65f, Fire=.25f, Suppression=.5f,
                Speed=52, Role=CombatAiRole.Dps, Personality=CombatAiPersonality.Aggressive },
            new() { Key="defender", Name="Szkielet Obrońca", Health=550, HealthGrowth=36,
                AttackMin=25, AttackMax=45, MinGrowth=1.63f, MaxGrowth=2.93f,
                Dodge=8, DodgeGrowth=.06f, PhysicalDefense=16, PhysicalGrowth=3.2f,
                MagicDefense=8, MagicGrowth=1.6f, Crit=.06f, CritGrowth=.00015f,
                CritMultiplier=1.65f, Bleed=1, Poison=.2f, Fire=.4f, Suppression=.25f,
                Speed=36, Role=CombatAiRole.Tank, Personality=CombatAiPersonality.Neutral }
        };

        private static CharacterClassData ConfigureClass()
        {
            var cls = GetOrCreate<CharacterClassData>(EnemyRoot + "CCD_CatacombsEnemy.asset");
            var so = new SerializedObject(cls);
            Field(so, "Id", "class.enemy.catacombs");
            Field(so, "ClassName", "Nieumarły z Katakumb");
            Field(so, "ResourceType", ResourceType.Mana);
            Field(so, "BasicAttackSkill", (UnityEngine.Object)null);
            so.FindProperty(F("SkillTrees")).ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cls);
            return cls;
        }

        private static CharacterDefinition ConfigureEnemy(EnemySpec spec,
            CharacterClassData enemyClass, SkillData[] skills, Sprite fallbackSprite)
        {
            var enemy = GetOrCreate<CharacterDefinition>(EnemyRoot + "CD_Catacombs_" + spec.Key + ".asset");
            var so = new SerializedObject(enemy);
            Field(so, "Id", "enemy.catacombs." + spec.Key);
            Field(so, "DisplayName", spec.Name);
            Field(so, "ClassData", enemyClass);
            Field(so, "AiRole", spec.Role);
            Field(so, "AiPersonality", spec.Personality);
            Field(so, "IsMainCharacter", false);
            Field(so, "BasicAttackSkill", skills[0]);
            if (enemy.VisualSprite == null) Field(so, "VisualSprite", fallbackSprite);
            var tags = so.FindProperty(F("Tags"));
            tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Undead;
            var stat = so.FindProperty(F("BaseStats"));
            Sub(stat, "MaxHealth", spec.Health);
            Sub(stat, "MinAttack", spec.AttackMin);
            Sub(stat, "MaxAttack", spec.AttackMax);
            Sub(stat, "PhysicalDefense", spec.PhysicalDefense);
            Sub(stat, "MagicalDefense", spec.MagicDefense);
            Sub(stat, "Dodge", spec.Dodge);
            Sub(stat, "CritChance", spec.Crit);
            Sub(stat, "CritDamage", spec.CritMultiplier);
            Sub(stat, "Speed", spec.Speed);
            Sub(stat, "WeaponAccuracy", 100f);
            Sub(stat, "MaxResource", 100);
            Sub(stat, "ResourceRegeneration", 15);
            var growth = so.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", spec.HealthGrowth);
            Sub(growth, "MinAttack", spec.MinGrowth);
            Sub(growth, "MaxAttack", spec.MaxGrowth);
            Sub(growth, "PhysicalDefense", spec.PhysicalGrowth);
            Sub(growth, "MagicalDefense", spec.MagicGrowth);
            Sub(growth, "Dodge", spec.DodgeGrowth);
            Sub(growth, "CritChance", spec.CritGrowth);
            var resist = so.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", spec.Bleed);
            Sub(resist, "PoisonResistance", spec.Poison);
            Sub(resist, "FireResistance", spec.Fire);
            Sub(resist, "SuppressionResistance", spec.Suppression);
            var starters = so.FindProperty(F("StartingSkills"));
            starters.ClearArray();
            foreach (var skill in skills) Add(starters).objectReferenceValue = skill;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(enemy);
            return enemy;
        }

        private static readonly (string Key, string[] Members)[] Compositions =
        {
            ("skeleton_guard", new[] { "swordsman", "defender" }),
            ("grave_watch", new[] { "swordsman", "archer", "archer" }),
            ("rotting_pair", new[] { "undead", "ghoul" }),
            ("occult_escort", new[] { "defender", "occultist", "swordsman" }),
            ("haunting", new[] { "wraith", "undead", "wraith" }),
            ("ambush", new[] { "ghoul", "archer", "swordsman" }),
            ("bone_line", new[] { "defender", "swordsman", "archer", "occultist" }),
            ("undead_pack", new[] { "undead", "ghoul", "swordsman", "archer" })
        };

        private static List<EncounterDefinition> ConfigureEncounters(
            IReadOnlyDictionary<string, CharacterDefinition> enemies, CharacterClassData cls)
        {
            var result = new List<EncounterDefinition>();
            foreach (var composition in Compositions)
            {
                var encounter = GetOrCreate<EncounterDefinition>(EncounterRoot + "ED_Catacombs_" + composition.Key + ".asset");
                var so = new SerializedObject(encounter);
                Field(so, "Id", "encounter.catacombs." + composition.Key);
                Field(so, "DisplayName", composition.Key.Replace('_', ' '));
                Field(so, "GridWidth", 12);
                Field(so, "GridHeight", 12);
                var list = so.FindProperty(F("Enemies"));
                list.ClearArray();
                for (var i = 0; i < composition.Members.Length; i++)
                {
                    var enemy = Add(list);
                    Sub(enemy, "InstanceId", "enemy.catacombs." + composition.Key + "." + (i + 1));
                    Sub(enemy, "CharacterDefinition", enemies[composition.Members[i]]);
                    Sub(enemy, "ClassData", cls);
                    enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7 + i % 2, 3 + i * 2);
                    Sub(enemy, "VisionRange", composition.Members[i] == "archer" ? 8f : 6f);
                    Sub(enemy, "VisionAngle", composition.Members[i] == "wraith" ? 150f : 110f);
                    enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(encounter);
                result.Add(encounter);
            }
            return result;
        }

        private static void ConfigureSummonPool(IReadOnlyDictionary<string, CharacterDefinition> enemies)
        {
            var item = Require<UniqueEquipmentDefinition>(
                Root + "Equipment/BossUnique/EID_BossDeadKing_MageStaff.asset");
            var so = new SerializedObject(item);
            var pool = so.FindProperty(F("SummonPool"));
            pool.ClearArray();
            foreach (var key in new[] { "swordsman", "archer", "occultist", "defender" })
                Add(pool).objectReferenceValue = enemies[key];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void AddDistinct(SerializedProperty list, UnityEngine.Object asset)
        {
            for (var i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) return;
            Add(list).objectReferenceValue = asset;
        }

        private static void ConfigureDungeonAndCatalog(CharacterClassData enemyClass,
            IReadOnlyDictionary<string, CharacterDefinition> enemies,
            IReadOnlyDictionary<string, SkillData[]> skills,
            IReadOnlyList<EncounterDefinition> encounters)
        {
            foreach (var path in new[] { "DD_Catacombs_Expedition.asset", "DD_Catacombs_BossExpedition.asset" })
            {
                var dungeon = Require<DungeonDefinition>(Root + "Dungeons/" + path);
                var so = new SerializedObject(dungeon);
                var list = so.FindProperty(F("EncounterDefinitions"));
                list.ClearArray();
                foreach (var encounter in encounters) Add(list).objectReferenceValue = encounter;
                if (path.Contains("BossExpedition")) Field(so, "IsAvailableAsOffer", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dungeon);
            }
            var catalog = Require<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            var data = new SerializedObject(catalog);
            AddDistinct(data.FindProperty(F("CharacterClasses")), enemyClass);
            foreach (var enemy in enemies.Values)
                AddDistinct(data.FindProperty(F("CharacterDefinitions")), enemy);
            foreach (var group in skills.Values)
                foreach (var skill in group)
                    AddDistinct(data.FindProperty(F("SkillDefinitions")), skill);
            foreach (var encounter in encounters)
                AddDistinct(data.FindProperty(F("EncounterDefinitions")), encounter);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        public static IReadOnlyList<EncounterDefinition> Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("VS-15: exit Play Mode first");
            foreach (var directory in new[] { Root + "Catacombs", EnemyRoot, SkillRoot, EncounterRoot })
            {
                var parent = System.IO.Path.GetDirectoryName(directory.TrimEnd('/'))?.Replace('\\', '/');
                var name = System.IO.Path.GetFileName(directory.TrimEnd('/'));
                if (parent != null && !AssetDatabase.IsValidFolder(directory.TrimEnd('/')))
                    AssetDatabase.CreateFolder(parent, name);
            }
            var enemyClass = ConfigureClass();
            var skills = ConfigureSkills();
            var fallbackSprite = Require<CharacterDefinition>(Root + "Characters/CD_Skeleton_Test.asset").VisualSprite;
            var enemies = new Dictionary<string, CharacterDefinition>();
            foreach (var spec in Enemies)
                enemies[spec.Key] = ConfigureEnemy(spec, enemyClass, skills[spec.Key], fallbackSprite);
            var encounters = ConfigureEncounters(enemies, enemyClass);
            ConfigureSummonPool(enemies);
            ConfigureDungeonAndCatalog(enemyClass, enemies, skills, encounters);
            AssetDatabase.SaveAssets();
            return encounters;
        }
    }
}
