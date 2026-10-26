using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class VS16CatacombsBossSetup
    {
        public const string SlayerUniqueRoot = "Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_SkeletonSlayer_";
        public const string SmugglerCloakPath = "Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BanditLeader_MageArmor.asset";

        [MenuItem("Godmist/Content/Bosses 2.0/Configure Approved Unique Equipment")]
        public static void ConfigureApprovedUniqueEquipment()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var items = ConfigureSlayerUniques();
            var cloak = ConfigureUnique(SmugglerCloakPath, "unique.bandit_leader.armor.mage", "Płaszcz Przemytnika",
                "EID_MageRobe", PassiveEffectOperationType.ReduceIncomingDamage, UniqueEquipmentCombatRules.PhysicalRanged,
                new[] { .30f, .35f, .40f, .45f, .55f },
                value => $"Obrażenia fizyczne od ataków dystansowych: −{value * 100:0}%.");
            var catalog = new SerializedObject(Require<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset"));
            foreach (var item in items) AddDistinct(catalog.FindProperty(F("ItemDefinitions")), item);
            AddDistinct(catalog.FindProperty(F("ItemDefinitions")), cloak);
            Save(catalog);
            ConfigureSkeletonSlayerExpedition();
            AssetDatabase.SaveAssets();
            Debug.Log("APPROVED_BOSS_UNIQUES_CONFIGURED: Slayer own four uniques; Smuggler cloak catalogued; Bandit Leader encounter not authored yet.");
        }

        private static UniqueEquipmentDefinition[] ConfigureSlayerUniques()
        {
            return new[]
            {
                ConfigureUnique(SlayerUniqueRoot + "WarriorArmor.asset", "unique.skeleton_slayer.armor.warrior", "Kolczuga Furiata",
                    "EID_WarriorHauberk", PassiveEffectOperationType.ModifyStat, "", new[] { 25f, 30f },
                    value => $"Maksymalna wściekłość: +{value:0}.", CharacterStatType.MaxResource),
                ConfigureUnique(SlayerUniqueRoot + "ScoutArmor.asset", "unique.skeleton_slayer.armor.scout", "Żebrowana Tunika",
                    "EID_ScoutTunic", PassiveEffectOperationType.ReduceIncomingDamage, UniqueEquipmentCombatRules.FirstPhysicalHit,
                    new[] { .25f, .30f, .35f, .40f, .50f },
                    value => $"Pierwsze trafienie fizyczne między własnymi turami: −{value * 100:0}% obrażeń. Pudła, uniki, obrażenia okresowe i trafienia całkowicie pochłonięte nie zużywają ochrony."),
                ConfigureUnique(SlayerUniqueRoot + "MageArmor.asset", "unique.skeleton_slayer.armor.mage", "Szaty Runiczne",
                    "EID_MageRobe", PassiveEffectOperationType.MaxHealthFromMaxResource, "", new[] { .30f, .45f },
                    value => $"Maksymalne zdrowie: +{value * 100:0}% maksymalnej many."),
                ConfigureUnique(SlayerUniqueRoot + "PaladinHammer.asset", "unique.skeleton_slayer.hammer.paladin", "Młot Zabójcy",
                    "EID_PaladinHammer", PassiveEffectOperationType.ApplyStatus, StatusEffectIds.Stun, new[] { .15f, .18f },
                    value => $"Przy trafieniu: {value * 100:0}% szansy na ogłuszenie na 2 tury.")
            };
        }

        private static UniqueEquipmentDefinition ConfigureUnique(string path, string id, string name,
            string basisFile, PassiveEffectOperationType type, string reference, float[] values,
            Func<float, string> description, CharacterStatType stat = CharacterStatType.MaxHealth)
        {
            var basis = Require<EquipmentItemDefinition>(Root + "Equipment/" + basisFile + ".asset");
            var item = GetOrCreate<UniqueEquipmentDefinition>(path);
            var data = new SerializedObject(item);
            Field(data, "Id", id); Field(data, "DisplayName", name); Field(data, "Description", "");
            Field(data, "BaseDefinition", basis); Field(data, "RequiredClass", basis.RequiredClass);
            Field(data, "ItemType", basis.ItemType); Field(data, "BaseRarity", RarityTier.Ancient);
            Field(data, "Weight", basis.Weight); Field(data, "IsStackable", false);
            // Reuse the shipped class/slot icon until dedicated unique art is available.
            if (item.Icon == null) Field(data, "Icon", basis.Icon);
            var tiers = new[] { "Effects", "EffectsLevel20", "EffectsLevel30", "EffectsLevel40", "EffectsLevel50" };
            for (var tier = 0; tier < tiers.Length; tier++)
            {
                var effects = data.FindProperty(F(tiers[tier])); effects.ClearArray();
                if (tier >= values.Length) continue; // Unauthored higher brackets retain the last known tier.
                var effect = Add(effects);
                Sub(effect, "Id", id + ".effect"); Sub(effect, "DisplayName", name);
                Sub(effect, "Description", description(values[tier])); Sub(effect, "Lifetime", PassiveEffectLifetime.Persistent);
                Sub(effect, "StackingRule", PassiveEffectStackingRule.Unique);
                effect.FindPropertyRelative(F("Conditions")).ClearArray();
                var triggers = effect.FindPropertyRelative(F("Triggers")); triggers.ClearArray();
                if (type == PassiveEffectOperationType.ApplyStatus) Sub(Add(triggers), "Type", PassiveEffectTriggerType.OnHit);
                var operations = effect.FindPropertyRelative(F("Operations")); operations.ClearArray();
                var operation = Add(operations);
                Sub(operation, "Id", id + ".operation"); Sub(operation, "Type", type);
                Sub(operation, "ReferenceId", reference); Sub(operation, "StatType", stat);
                Sub(operation, "Target", type == PassiveEffectOperationType.ApplyStatus ? PassiveEffectTargetType.PrimaryTarget : PassiveEffectTargetType.Self);
                Sub(operation, "ModifierOperation", StatModifierOperation.FlatAbsolute);
                Sub(operation, "DurationTurns", type == PassiveEffectOperationType.ApplyStatus ? 2 : 0);
                var value = operation.FindPropertyRelative(F("Value"));
                Sub(value, "Source", PassiveEffectValueSourceType.Constant); Sub(value, "NumberValue", values[tier]); Sub(value, "Scale", 1f);
                operation.FindPropertyRelative(F("Parameters")).ClearArray();
                var tooltip = effect.FindPropertyRelative(F("Tooltip"));
                Sub(tooltip, "DescriptionOverride", description(values[tier]));
                Sub(tooltip, "ShowRuntimeState", false);
            }
            Save(data); return item;
        }
    }
}
