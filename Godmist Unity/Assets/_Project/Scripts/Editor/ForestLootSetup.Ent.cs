using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;
namespace Godmist.Editor
{
    public static partial class ForestLootSetup
    {
        private static void Apply(SerializedObject so)
        { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(so.targetObject); }
        public static void ConfigureEntLoot()
        {
            Configure();
            var uniques = new[] {
                EntUnique("Glaive", "Praelficka Glewia Wojenna", Equipment(ItemType.Weapon)[0], 0),
                EntUnique("Tunic", "Liściasta Tunika", Equipment(ItemType.Armor)[1], 1),
                EntUnique("Finger", "Palec Enta", Equipment(ItemType.Weapon)[2], 2),
                EntUnique("Hammer", "Elficki Młot Życia", Equipment(ItemType.Weapon)[3], 3)
            };
            ConfigureMaterial("ID_ForestSpirit", "item.alchemy.forest_spirit", "Żywioł lasu", 1282, RarityTier.Ancient, MaterialDiscipline.Alchemy);
            var items = IndexItems();
            var spirit = Item(items, "item.alchemy.forest_spirit");
            var bag = Bag("ID_Forest_EntBag", "item.lootbag.forest.infected_ent", "Torba Zarażonego Enta", "", so =>
            {
                Bracket(so, LootLevelBracket.Level1To10, _ => {});
                Bracket(so, LootLevelBracket.Level11To20, pools =>
                {
                    Pool(pools, .5f, (spirit, 1, 2));
                    Pool(pools, .6f, (Require<ItemDefinition>(Root + "ContentPass1/item.vs10.szmaragd.asset"), 3, 5));
                    Pool(pools, .3f, (Require<ItemDefinition>(Root + "Items/Materials/Runeforging/ID_Topaz.asset"), 2, 3));
                    Pool(pools, .3f, (Item(items, "item.galduryte.generic"), 3, 3));
                    var entry = pools.GetArrayElementAtIndex(pools.arraySize - 1).FindPropertyRelative(F("Entries")).GetArrayElementAtIndex(0);
                    Sub(entry, "MinimumGalduryteRarity", RarityTier.Rare); Sub(entry, "FixedGalduryteLevel", 20);
                });
                BossGemLootSetup.ConfigureBossBag(so, true);
                var unique = so.FindProperty(F("BossUniqueDrop")); Sub(unique, "DropChance", .3f);
                var list = unique.FindPropertyRelative(F("EquipmentDefinitions")); list.ClearArray();
                foreach (var definition in uniques) Add(list).objectReferenceValue = definition;
            });
            var resources = Require<LootBagItemDefinition>(LootRoot + "ID_Forest_Resources.asset");
            var galdurytes = Require<LootBagItemDefinition>(LootRoot + "ID_Forest_Galdurytes.asset");
            var weapons = Require<LootBagItemDefinition>(LootRoot + "ID_Forest_Weapons.asset");
            var armor = Require<LootBagItemDefinition>(LootRoot + "ID_Forest_Armor.asset");
            var low = Table("LT_Forest_Boss_1_10_Production", (bag,1f,1,1), (resources,.15f,1,1),
                (galdurytes,.08f,1,1),(galdurytes,.08f,1,1),(weapons,.06f,1,1),(armor,.06f,1,1));
            var high = Table("LT_Forest_Boss_11_20_Production", (bag,1f,1,1), (resources,.15f,1,1),
                (galdurytes,.08f,1,1),(galdurytes,.08f,1,1),(weapons,.06f,1,1),(armor,.06f,1,1));
            var catalog = new SerializedObject(Require<DungeonLootTableCatalog>(LootRoot + "DLC_Forest_Loot.asset"));
            var brackets = catalog.FindProperty(F("Entries"));
            for (var i = 0; i < brackets.arraySize; i++)
            {
                var entry = brackets.GetArrayElementAtIndex(i);
                Sub(entry, "BossLootTable", entry.FindPropertyRelative(F("Bracket")).enumValueIndex == (int)LootLevelBracket.Level1To10 ? low : high);
            }
            Apply(catalog);
            var content = new SerializedObject(Require<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset"));
            var definitions = content.FindProperty(F("ItemDefinitions")); AddCatalogItem(definitions, bag); AddCatalogItem(definitions, spirit);
            foreach (var unique in uniques) AddCatalogItem(definitions, unique);
            Apply(content);
            AssetDatabase.SaveAssets();
        }
        private static UniqueEquipmentDefinition EntUnique(string file, string name, EquipmentItemDefinition basis, int kind)
        {
            var item = GetOrCreate<UniqueEquipmentDefinition>(LootRoot + "EID_Forest_Ent" + file + ".asset");
            var so = new SerializedObject(item);
            Field(so, "Id", "unique.forest.ent." + file.ToLowerInvariant()); Field(so, "DisplayName", name);
            Field(so, "Description", ""); Field(so, "BaseDefinition", basis); Field(so, "RequiredClass", basis.RequiredClass);
            Field(so, "ItemType", basis.ItemType); Field(so, "BaseRarity", RarityTier.Ancient);
            foreach (var high in new[] { false, true })
            {
                var effects = so.FindProperty(F(high ? "EffectsLevel20" : "Effects")); effects.ClearArray();
                var effect = Add(effects); Sub(effect, "Id", "passive.forest.ent." + file.ToLowerInvariant()); Sub(effect, "DisplayName", name);
                Sub(effect, "Lifetime", PassiveEffectLifetime.Persistent);
                var triggers = effect.FindPropertyRelative(F("Triggers")); triggers.ClearArray();
                Sub(Add(triggers), "Type", kind == 1 ? PassiveEffectTriggerType.OnStatusApplied : kind == 3 ? PassiveEffectTriggerType.OnHeal : PassiveEffectTriggerType.OnAttack);
                var conditions = effect.FindPropertyRelative(F("Conditions")); conditions.ClearArray();
                if (kind == 0) Sub(Add(conditions), "Type", PassiveEffectConditionType.TargetHasMoreMaxHealth);
                var operations = effect.FindPropertyRelative(F("Operations")); operations.ClearArray(); var op = Add(operations);
                Sub(op, "Id", "operation.forest.ent." + file.ToLowerInvariant());
                Sub(op, "Type", kind == 0 ? PassiveEffectOperationType.ModifyStat : kind == 1 ? PassiveEffectOperationType.PreventStatus : kind == 2 ? PassiveEffectOperationType.ApplyStatus : PassiveEffectOperationType.Heal);
                Sub(op, "Target", kind == 2 ? PassiveEffectTargetType.PrimaryTarget : kind == 3 ? PassiveEffectTargetType.LowestHealthAlly : PassiveEffectTargetType.Self);
                Sub(op, "StatType", CharacterStatType.PhysicalDefensePenetration);
                Sub(op, "ReferenceId", kind == 1 ? StatusEffectIds.Poison : kind == 2 ? StatusEffectIds.Immobilize : "");
                Sub(op, "DurationTurns", kind == 2 ? 2 : 0);
                var value = op.FindPropertyRelative(F("Value"));
                Sub(value, "Source", kind == 3 ? PassiveEffectValueSourceType.EventValue : PassiveEffectValueSourceType.Constant);
                Sub(value, "NumberValue", kind == 0 ? high ? .24f : .2f : kind == 1 ? high ? .55f : .5f : kind == 2 ? high ? .12f : .1f : 0f);
                Sub(value, "Scale", kind == 3 ? high ? .54f : .5f : 1f);
                var parameters = op.FindPropertyRelative(F("Parameters")); parameters.ClearArray();
                if (kind == 3)
                { var chance = Add(parameters); Sub(chance, "Key", "chance"); Sub(chance, "NumberValue", high ? .35f : .3f); }
                var description = kind == 0 ? (high ? "24%" : "20%") + " przebicia obrony fizycznej przeciw celom z większym maksymalnym zdrowiem."
                    : kind == 1 ? (high ? "55%" : "50%") + " szansy na uniknięcie zatrucia."
                    : kind == 2 ? (high ? "12%" : "10%") + " szansy przy ataku na unieruchomienie celu na 2 tury."
                    : (high ? "35%" : "30%") + " szansy na dodatkowe leczenie sojusznika o najniższym zdrowiu za " + (high ? "54%" : "50%") + " przywróconego zdrowia.";
                Sub(effect, "Description", description);
                Sub(effect.FindPropertyRelative(F("Tooltip")), "DescriptionOverride", description);
            }
            Apply(so); return item;
        }
    }
}
