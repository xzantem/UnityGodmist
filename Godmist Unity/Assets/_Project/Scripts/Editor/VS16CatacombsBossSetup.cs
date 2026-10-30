using System;
using System.Linq;
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
    /// <summary>Idempotent production authoring for the VS-16 Dead King encounter.</summary>
    public static partial class VS16CatacombsBossSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";
        private const string Catacombs = Root + "Catacombs/";
        private const string Loot = Catacombs + "Loot/";
        private static string F(string name) => "<" + name + ">k__BackingField";

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("VS-16: missing " + path);

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (typeof(T) == typeof(CharacterDefinition))
                throw new System.InvalidOperationException("Historical content setup requires an authored CharacterDefinition at " + path + "; attack data is maintained in that asset.");
            var created = ScriptableObject.CreateInstance<T>();
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static SerializedProperty Add(SerializedProperty list)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            return list.GetArrayElementAtIndex(list.arraySize - 1);
        }

        private static void Set(SerializedProperty property, object value)
        {
            if (property == null) throw new InvalidOperationException("VS-16: missing serialized field");
            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case UnityEngine.Object asset: property.objectReferenceValue = asset; break;
                case string text: property.stringValue = text; break;
                case bool flag: property.boolValue = flag; break;
                case Enum enumeration: property.enumValueIndex = Convert.ToInt32(enumeration); break;
                case int integer: property.intValue = integer; break;
                case float number: property.floatValue = number; break;
                default: throw new ArgumentException("VS-16: unsupported value " + value);
            }
        }

        private static void Field(SerializedObject so, string name, object value) => Set(so.FindProperty(F(name)), value);
        private static void Sub(SerializedProperty property, string name, object value) => Set(property.FindPropertyRelative(F(name)), value);
        private static void Raw(SerializedProperty property, string name, object value) => Set(property.FindPropertyRelative(name), value);
        private static void Save(SerializedObject so)
        {
            ContextualSkillHitSetup.Apply(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so.targetObject);
            if (so.targetObject is CharacterDefinition definition) Godmist.Editor.EnemyResourceProfilesSetup.ApplyToDefinition(definition);
            if (so.targetObject is CharacterDefinition balancedEnemy) Godmist.Editor.EnemyScalingBalanceSetup.ApplyToDefinition(balancedEnemy);
            if (so.targetObject is EncounterDefinition encounter) Godmist.Editor.EnemyResourceProfilesSetup.ApplyToEncounter(encounter);
            if (so.targetObject is Godmist.Data.Dungeons.DungeonDefinition dungeon) DungeonLengthSetup.Apply(dungeon);
        }

        private static void AddDistinct(SerializedProperty list, UnityEngine.Object asset)
        {
            for (var i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) return;
            Add(list).objectReferenceValue = asset;
        }

        private static void Mode(SerializedObject so, TargetType type,
            TargetingType side, RangeShape shape, int range, int width = 1)
        {
            var modes = so.FindProperty("targetModes");
            modes.ClearArray();
            var mode = Add(modes);
            Raw(mode, "targetType", type);
            Raw(mode, "targetingType", side);
            Raw(mode, "includesCaster", type == TargetType.Caster);
            var absolute = mode.FindPropertyRelative("absoluteRange");
            Raw(absolute, "shape", shape);
            Raw(absolute, "minRange", type == TargetType.Caster ? 0 : 1);
            Raw(absolute, "maxRange", range);
            Raw(absolute, "rectangleWidth", width);
        }

        private static SerializedObject Skill(string file, string id, string name,
            string description, int mp, int ap, AttackType attack)
        {
            var asset = GetOrCreate<SkillData>(Catacombs + "Skills/" + file + ".asset");
            var so = new SerializedObject(asset);
            Field(so, "SkillId", id);
            Field(so, "DisplayName", name);
            Field(so, "TooltipDescription", description);
            Field(so, "SkillAccuracy", 100f);
            Field(so, "AttackType", attack);
            Field(so, "ResourceType", ResourceType.Mana);
            Field(so, "ResourceCost", mp);
            Field(so, "ActionCost", ap);
            Field(so, "RequiresLineOfSight", false);
            so.FindProperty("effects").ClearArray();
            return so;
        }

        private static SerializedProperty Effect(SerializedObject so, EffectType type)
        {
            var effect = Add(so.FindProperty("effects"));
            Raw(effect, "effectType", type);
            Raw(effect, "targetModeIndex", 0);
            Raw(effect, "baseChance", 1f);
            Raw(effect, "damageMultiplier", 1f);
            Raw(effect, "damageHitCount", 1);
            Raw(effect, "alwaysHits", type != EffectType.DealDamage);
            effect.FindPropertyRelative("conditions").ClearArray();
            return effect;
        }

        private static SkillData ConfigureCleave()
        {
            var so = Skill("SD_Catacombs_dead_king_cleave",
                "skill.enemy.catacombs.dead_king_cleave", "Królewskie Cięcie",
                "Tnie dwa pola przed sobą, zadając losowe obrażenia fizyczne.",
                0, 60, AttackType.Melee);
            Mode(so, TargetType.AreaAroundCaster, TargetingType.Enemies,
                RangeShape.Rectangle, 1, 2);
            var effect = Effect(so, EffectType.DealDamage);
            Raw(effect, "damageType", DamageType.Physical);
            Raw(effect, "damageValueSource", DamageValueSource.AttackRange);
            Save(so);
            return (SkillData)so.targetObject;
        }

        private static SkillData ConfigureGuard(CharacterClassData enemyClass)
        {
            var so = Skill("SD_Catacombs_dead_king_guard",
                "skill.enemy.catacombs.dead_king_guard", "Straż Przyboczna",
                "Przywołuje losowego szkieleta obok siebie. Może utrzymywać do trzech strażników.",
                60, 20, AttackType.Unknown);
            Mode(so, TargetType.Caster, TargetingType.Allies, RangeShape.Radial, 0);
            var effect = Effect(so, EffectType.Summon);
            var enemies = new[] { "swordsman", "archer", "occultist", "defender" }
                .Select(key => Require<CharacterDefinition>(Catacombs + "Enemies/CD_Catacombs_" + key + ".asset"))
                .ToArray();
            Raw(effect, "summonedCharacterDefinition", enemies[0]);
            Raw(effect, "summonedClassData", enemyClass);
            Raw(effect, "summonDurationTurns", 99);
            Raw(effect, "maxActiveSummons", 3);
            Raw(effect, "summonPercentMaxHealth", 1f);
            var pool = effect.FindPropertyRelative("summonPool");
            pool.ClearArray();
            foreach (var enemy in enemies) Add(pool).objectReferenceValue = enemy;
            var placement = effect.FindPropertyRelative("summonPlacementRange");
            Raw(placement, "shape", RangeShape.Radial);
            Raw(placement, "minRange", 1);
            Raw(placement, "maxRange", 1);
            Save(so);
            return (SkillData)so.targetObject;
        }

        private static SkillData ConfigureVengeance(Sprite runeSprite)
        {
            var rune = GetOrCreate<SummonedObjectDefinition>(Catacombs + "Objects/SO_DeadKingRune.asset");
            var runeData = new SerializedObject(rune);
            Set(runeData.FindProperty("objectId"), CatacombsBossSystem.RuneObjectId);
            Set(runeData.FindProperty("displayName"), "Runa Zemsty Zaświatów");
            Set(runeData.FindProperty("icon"), runeSprite);
            Set(runeData.FindProperty("worldSprite"), runeSprite);
            Set(runeData.FindProperty("maxHits"), 1);
            Set(runeData.FindProperty("defaultDurationTurns"), 2);
            Set(runeData.FindProperty("turnSpeed"), 80f);
            Set(runeData.FindProperty("blocksMovement"), false);
            Set(runeData.FindProperty("blocksLineOfSight"), false);
            runeData.FindProperty("effects").ClearArray();
            Save(runeData);

            var so = Skill("SD_Catacombs_dead_king_vengeance",
                CatacombsBossSystem.VengeanceSkillId, "Runy Zemsty",
                "Przy 75%, 50% i 25% HP stawia pięć run. Wejście na runę ją rozbraja; w drugiej własnej turze runa uderza najbliższego wroga za 150% losowych obrażeń magicznych.",
                0, 0, AttackType.Unknown);
            Mode(so, TargetType.Caster, TargetingType.Allies, RangeShape.Radial, 0);
            var effect = Effect(so, EffectType.CreateObject);
            Raw(effect, "summonedObjectDefinition", rune);
            Raw(effect, "maxActiveObjects", 15);
            Save(so);
            return (SkillData)so.targetObject;
        }

        private static CharacterDefinition ConfigureBoss(CharacterClassData cls,
            SkillData cleave, SkillData guard, SkillData vengeance)
        {
            var source = Require<CharacterDefinition>(Catacombs + "Enemies/CD_Catacombs_wraith.asset");
            var boss = GetOrCreate<CharacterDefinition>(Catacombs + "Enemies/CD_Catacombs_dead_king.asset");
            var so = new SerializedObject(boss);
            Field(so, "Id", CatacombsBossSystem.BossId);
            Field(so, "DisplayName", "Duch Umarłego Króla");
            Field(so, "ClassData", cls);
            Field(so, "AiRole", CombatAiRole.Dps);
            Field(so, "AiPersonality", CombatAiPersonality.Aggressive);
            Field(so, "VisualSprite", source.VisualSprite);
            Field(so, "BasicAttackSkill", cleave);
            var tags = so.FindProperty(F("Tags"));
            tags.ClearArray();
            Add(tags).enumValueIndex = (int)CharacterTag.Undead;
            var stats = so.FindProperty(F("BaseStats"));
            Sub(stats, "MaxHealth", 900);
            Sub(stats, "Dodge", 30f);
            Sub(stats, "PhysicalDefense", 10f);
            Sub(stats, "MagicalDefense", 25f);
            Sub(stats, "CritChance", .09f);
            Sub(stats, "CritDamage", 1.9f);
            Sub(stats, "Speed", 80f);
            Sub(stats, "WeaponAccuracy", 100f);
            Sub(stats, "MaxResource", 100);
            Sub(stats, "ResourceRegeneration", 15);
            var growth = so.FindProperty(F("GrowthData"));
            Sub(growth, "MaxHealth", 59f);
            Sub(growth, "Dodge", .225f);
            Sub(growth, "PhysicalDefense", 2f);
            Sub(growth, "MagicalDefense", 5f);
            Sub(growth, "CritChance", .000225f);
            var resist = so.FindProperty(F("BaseResistances"));
            Sub(resist, "BleedResistance", .7f);
            Sub(resist, "PoisonResistance", .3f);
            Sub(resist, "FireResistance", 1f);
            Sub(resist, "SuppressionResistance", 1f);
            var starters = so.FindProperty(F("StartingSkills"));
            starters.ClearArray();
            foreach (var skill in new[] { cleave, guard, vengeance })
                Add(starters).objectReferenceValue = skill;
            Save(so);
            return boss;
        }

        private static EncounterDefinition ConfigureEncounter(CharacterDefinition boss,
            CharacterClassData cls)
        {
            var encounter = GetOrCreate<EncounterDefinition>(Catacombs + "Encounters/ED_Catacombs_dead_king.asset");
            var so = new SerializedObject(encounter);
            Field(so, "Id", "encounter.catacombs.dead_king");
            Field(so, "DisplayName", "Duch Umarłego Króla");
            Field(so, "GridWidth", 15);
            Field(so, "GridHeight", 11);
            Field(so, "BossEnemyIndex", 0);
            var enemies = so.FindProperty(F("Enemies"));
            enemies.ClearArray();
            var enemy = Add(enemies);
            Sub(enemy, "InstanceId", "enemy.catacombs.dead_king.boss");
            Sub(enemy, "CharacterDefinition", boss);
            Sub(enemy, "ClassData", cls);
            enemy.FindPropertyRelative(F("SpawnPosition")).vector2IntValue = new Vector2Int(7, 5);
            Sub(enemy, "VisionRange", 8f);
            Sub(enemy, "VisionAngle", 180f);
            enemy.FindPropertyRelative(F("InitialFacing")).vector2Value = Vector2.left;
            Save(so);
            return encounter;
        }

        private static void LootEntry(SerializedProperty entries, ItemDefinition item,
            float chance)
        {
            var entry = Add(entries);
            Sub(entry, "ItemDefinition", item);
            Sub(entry, "DropChance", chance);
            var amount = entry.FindPropertyRelative(F("Amount"));
            Set(amount.FindPropertyRelative("Min"), 1);
            Set(amount.FindPropertyRelative("Max"), 1);
        }

        private static LootTableData BossTable(string file, LootBagItemDefinition bossBag,
            LootBagItemDefinition resources, LootBagItemDefinition galdurytes,
            LootBagItemDefinition weapons, LootBagItemDefinition armor)
        {
            var table = GetOrCreate<LootTableData>(Loot + file + ".asset");
            var so = new SerializedObject(table);
            var entries = so.FindProperty(F("Entries"));
            entries.ClearArray();
            LootEntry(entries, bossBag, 1f);
            LootEntry(entries, resources, .15f);
            // Two independent 8% galduryte rolls; the current T1 bag pools both effect families.
            LootEntry(entries, galdurytes, .08f);
            LootEntry(entries, galdurytes, .08f);
            LootEntry(entries, weapons, .06f);
            LootEntry(entries, armor, .06f);
            Save(so);
            return table;
        }

        private static LootBagItemDefinition ConfigureBossBag()
        {
            var ectoplasm = GetOrCreate<ItemDefinition>(Loot + "ID_DeadKingEctoplasm.asset");
            var ectoplasmData = new SerializedObject(ectoplasm);
            Field(ectoplasmData, "Id", "item.material.dead_king_ectoplasm");
            Field(ectoplasmData, "DisplayName", "Królewska ektoplazma");
            Field(ectoplasmData, "Description", "Gęsta esencja pozostała po zjawie Umarłego Króla.");
            Field(ectoplasmData, "Icon", Require<Sprite>(
                "Assets/_Project/Art/Icons/Items/VS22/gem-powder-icons-gem-powder-purple-32.png"));
            Field(ectoplasmData, "ItemType", ItemType.Material);
            Field(ectoplasmData, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            Field(ectoplasmData, "BaseRarity", RarityTier.Rare);
            Field(ectoplasmData, "BaseValue", 0);
            Field(ectoplasmData, "Weight", .1f);
            Field(ectoplasmData, "IsStackable", true);
            Save(ectoplasmData);

            var bag = GetOrCreate<LootBagItemDefinition>(Loot + "ID_Catacombs_DeadKingBag.asset");
            var so = new SerializedObject(bag);
            Field(so, "Id", "item.lootbag.catacombs.dead_king");
            Field(so, "DisplayName", "Torba Ducha Umarłego Króla");
            Field(so, "Description", "Łupy wyniesione z komnaty Umarłego Króla.");
            Field(so, "ItemType", ItemType.Lootbag);
            Field(so, "BaseRarity", RarityTier.Common);
            Field(so, "BaseValue", 0);
            Field(so, "Weight", 1f);
            Field(so, "IsStackable", false);
            var brackets = so.FindProperty(F("Brackets"));
            brackets.ClearArray();
            foreach (var bracket in new[] { LootLevelBracket.Level1To10, LootLevelBracket.Level11To20 })
            {
                var item = Add(brackets);
                Sub(item, "Bracket", bracket);
                var pools = item.FindPropertyRelative(F("Pools"));
                pools.ClearArray();
                if (bracket == LootLevelBracket.Level11To20)
                {
                    BossBagPool(pools, ectoplasm, .5f, 1, 2);
                    BossBagPool(pools, Require<ItemDefinition>(
                        Root + "ContentPass1/item.vs10.granat.asset"), .6f, 3, 5);
                    BossBagPool(pools, Require<ItemDefinition>(
                        Root + "Items/Materials/Runeforging/ID_Amber.asset"), .3f, 2, 3);
                    BossBagPool(pools, Require<GalduryteItemDefinition>(
                        Root + "Items/ID_GalduryteGeneric.asset"), .3f, 3, 3,
                        RarityTier.Rare);
                }
            }
            var unique = so.FindProperty(F("BossUniqueDrop"));
            Sub(unique, "DropChance", .3f);
            var definitions = unique.FindPropertyRelative(F("EquipmentDefinitions"));
            definitions.ClearArray();
            foreach (var name in new[] { "WarriorSword", "ScoutDagger", "MageStaff", "PaladinArmor" })
                Add(definitions).objectReferenceValue = Require<UniqueEquipmentDefinition>(
                    Root + "Equipment/BossUnique/EID_BossDeadKing_" + name + ".asset");
            Save(so);
            return bag;
        }

        private static Sprite ConfigureRuneSprite()
        {
            const string path = Catacombs + "Objects/DeadKingRune.png";
            if (!System.IO.File.Exists(path))
            {
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point };
                var pixels = new Color32[16 * 16];
                var outline = new Color32(38, 17, 67, 255);
                var glow = new Color32(184, 72, 239, 255);
                var core = new Color32(236, 184, 255, 255);
                for (var y = 1; y < 15; y++)
                for (var x = 1; x < 15; x++)
                {
                    var dx = x - 7.5f;
                    var dy = y - 7.5f;
                    var square = dx * dx + dy * dy;
                    if (square >= 29f && square <= 47f)
                        pixels[y * 16 + x] = outline;
                    else if (square >= 21f && square < 29f)
                        pixels[y * 16 + x] = glow;
                    if ((x == 7 || x == 8) && y >= 4 && y <= 11
                        || (y == 7 || y == 8) && x >= 4 && x <= 11)
                        pixels[y * 16 + x] = core;
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("VS-16: rune texture importer missing");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return Require<Sprite>(path);
        }

        private static void BossBagPool(SerializedProperty pools, ItemDefinition item,
            float chance, int min, int max,
            RarityTier minimumRarity = RarityTier.Common)
        {
            var pool = Add(pools);
            Sub(pool, "DropChance", chance);
            var entries = pool.FindPropertyRelative(F("Entries"));
            entries.ClearArray();
            var entry = Add(entries);
            Sub(entry, "ItemDefinition", item);
            var amount = entry.FindPropertyRelative(F("Amount"));
            Set(amount.FindPropertyRelative("Min"), min);
            Set(amount.FindPropertyRelative("Max"), max);
            Sub(entry, "MinimumGalduryteRarity", minimumRarity);
        }

        private static void ConfigureCatalog(CharacterDefinition boss,
            EncounterDefinition encounter, DungeonDefinition dungeon,
            LootBagItemDefinition bag, params SkillData[] skills)
        {
            var catalog = Require<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            var so = new SerializedObject(catalog);
            AddDistinct(so.FindProperty(F("CharacterDefinitions")), boss);
            AddDistinct(so.FindProperty(F("EncounterDefinitions")), encounter);
            AddDistinct(so.FindProperty(F("DungeonDefinitions")), dungeon);
            AddDistinct(so.FindProperty(F("ItemDefinitions")), bag);
            foreach (var skill in skills) AddDistinct(so.FindProperty(F("SkillDefinitions")), skill);
            Save(so);
        }

        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("VS-16: exit Play Mode first");
            if (!AssetDatabase.IsValidFolder(Catacombs + "Objects"))
                AssetDatabase.CreateFolder(Catacombs.TrimEnd('/'), "Objects");
            var cls = Require<CharacterClassData>(Catacombs + "Enemies/CCD_CatacombsEnemy.asset");
            var runeSprite = ConfigureRuneSprite();
            var cleave = ConfigureCleave();
            var guard = ConfigureGuard(cls);
            var vengeance = ConfigureVengeance(runeSprite);
            var boss = ConfigureBoss(cls, cleave, guard, vengeance);
            var encounter = ConfigureEncounter(boss, cls);
            var bag = ConfigureBossBag();
            var resources = Require<LootBagItemDefinition>(Loot + "ID_Catacombs_Resources.asset");
            var galdurytes = Require<LootBagItemDefinition>(Loot + "ID_Catacombs_Galdurytes.asset");
            var weapons = Require<LootBagItemDefinition>(Loot + "ID_Catacombs_Weapons.asset");
            var armor = Require<LootBagItemDefinition>(Loot + "ID_Catacombs_Armor.asset");
            var low = BossTable("LT_Catacombs_Boss_1_10_Production", bag,
                resources, galdurytes, weapons, armor);
            var high = BossTable("LT_Catacombs_Boss_11_20_Production", bag,
                resources, galdurytes, weapons, armor);
            var lootCatalog = Require<DungeonLootTableCatalog>(Root + "Dungeons/DLC_Catacombs_Loot.asset");
            var lootData = new SerializedObject(lootCatalog);
            var brackets = lootData.FindProperty(F("Entries"));
            for (var i = 0; i < brackets.arraySize; i++)
            {
                var entry = brackets.GetArrayElementAtIndex(i);
                var bracket = (LootLevelBracket)entry.FindPropertyRelative(F("Bracket")).enumValueIndex;
                Sub(entry, "BossLootTable", bracket == LootLevelBracket.Level1To10 ? low : high);
            }
            Save(lootData);
            var dungeon = Require<DungeonDefinition>(Root + "Dungeons/DD_Catacombs_BossExpedition.asset");
            var dungeonData = new SerializedObject(dungeon);
            Field(dungeonData, "BossEncounterDefinition", encounter);
            Field(dungeonData, "IsAvailableAsOffer", true);
            Field(dungeonData, "DungeonLevel", 10);
            var levels = dungeonData.FindProperty(F("FixedOfferLevels"));
            levels.ClearArray();
            Add(levels).intValue = 10;
            Add(levels).intValue = 20;
            Save(dungeonData);
            var finalRoom = Require<DungeonRoomTemplateDefinition>(
                Root + "Dungeons/RoomTemplates/DRT_Catacombs_Final_Production.asset");
            var finalRoomData = new SerializedObject(finalRoom);
            Field(finalRoomData, "DisplayName", "Komnata Umarłego Króla");
            Save(finalRoomData);
            ConfigureCatalog(boss, encounter, dungeon, bag, cleave, guard, vengeance);
            var catalog = new SerializedObject(Require<GameContentCatalog>(
                Root + "World/GC_MainContentCatalog.asset"));
            AddDistinct(catalog.FindProperty(F("ItemDefinitions")),
                Require<ItemDefinition>(Loot + "ID_DeadKingEctoplasm.asset"));
            Save(catalog);
            AssetDatabase.SaveAssets();
            ConfigureKingV2();
        }
    }
}
