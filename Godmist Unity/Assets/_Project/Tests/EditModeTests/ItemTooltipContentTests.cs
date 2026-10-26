using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ItemTooltipContentTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Plain(string text) => Regex.Replace(text ?? "", "<[^>]*>", "");
        static void Set(ItemDefinition definition, string field, object value) =>
            typeof(ItemDefinition).GetField("<" + field + ">k__BackingField", Hidden).SetValue(definition, value);
        static void UseType(ConsumableItemDefinition definition, ConsumableUseType type) =>
            typeof(ConsumableItemDefinition).GetField("<UseType>k__BackingField", Hidden).SetValue(definition, type);
        static string Body(UIWorkspaceTooltipView view, ItemInstance item) => Plain((string)
            typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody", Hidden).Invoke(view, new object[] { item, null }));
        static void AssertPlayerText(string body, string context)
        {
            Assert.That(body, Is.Not.Empty, context);
            foreach (var technical in new[] { "Modyfikator ", "modyfikator procentowy", "wartość statystyki", "addytywnie", "mnożnikowo", "skonfigurowanego", "Cel:" })
                Assert.That(body, Does.Not.Contain(technical), context);
        }

        [TestCase(ConsumableUseType.Potion)]
        [TestCase(ConsumableUseType.Bomb)]
        [TestCase(ConsumableUseType.Utility)]
        public void EveryRuntimeConsumableEffectHasReadableItemTextWithoutRepeatedTargets(ConsumableUseType useType)
        {
            var definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            UseType(definition, useType); Set(definition, "Id", "test.consumable.copy");
            Set(definition, "Description", "Modyfikator szybkości: stary opis, którego nie należy powtarzać.");
            var node = new GameObject("Consumable copy coverage");
            try
            {
                var view = node.AddComponent<UIWorkspaceTooltipView>();
                foreach (ConsumableRuntimeEffectType type in Enum.GetValues(typeof(ConsumableRuntimeEffectType)))
                {
                    var item = new ConsumableItemInstance(definition) { RuntimeEffects = new()
                    {
                        new() { EffectType = type, Value = .3f, Chance = .75f, DurationTurns = 3, StatusId = StatusEffectIds.Bleed }
                    }};
                    if (type == ConsumableRuntimeEffectType.ReturnToArungard)
                        item.RuntimeEffects.Add(new() { EffectType = type });
                    var body = Body(view, item);
                    AssertPlayerText(body, type + " " + useType);
                    Assert.That(body, Does.Not.Contain(type.ToString()), type + " " + useType);
                    if (type == ConsumableRuntimeEffectType.ReturnToArungard)
                        Assert.That(Regex.Matches(body, "Wraca do Arungardu").Count, Is.EqualTo(1));
                }
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void EveryKnownConsumableStatusUsesOneCompactChanceAndDuration()
        {
            var definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            UseType(definition, ConsumableUseType.Bomb);
            Set(definition, "Id", "test.status.copy");
            try
            {
                foreach (var id in StatusEffectIds.KnownIds)
                {
                    var item = new ConsumableItemInstance(definition) { RuntimeEffects = new()
                    {
                        new() { EffectType = ConsumableRuntimeEffectType.ApplyStatus, StatusId = id, Value = .5f, Chance = .75f, DurationTurns = 3 }
                    }};
                    var summary = UIWorkspaceTooltipView.BuildBombEffectSummary(item);
                    Assert.That(summary, Is.Not.Null.And.Not.Empty, id);
                    Assert.That(Regex.Matches(summary, "75%").Count, Is.EqualTo(1), id);
                    Assert.That(Regex.Matches(summary, "3 tur").Count, Is.EqualTo(1), id);
                    AssertPlayerText(summary, id);
                }
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void CatalystAndDuplicateIngredientsAreAlreadyIncludedInOneFinalEffect()
        {
            var definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            UseType(definition, ConsumableUseType.Potion);
            Set(definition, "Id", "test.catalyst.copy");
            var node = new GameObject("Final potion effects");
            try
            {
                var item = new ConsumableItemInstance(definition) { CatalystDisplayName = "Katalizator: Silny",
                    CatalystEffectMultiplier = 1.2f, CatalystDurationModifier = 1, RuntimeEffects = new()
                    {
                        new() { EffectType = ConsumableRuntimeEffectType.ModifySpeed, Value = 4, DurationTurns = 3 },
                        new() { EffectType = ConsumableRuntimeEffectType.ModifySpeed, Value = 4, DurationTurns = 3 }
                    }};
                var body = Body(node.AddComponent<UIWorkspaceTooltipView>(), item);
                Assert.That(Regex.Matches(body, "Szybkość:").Count, Is.EqualTo(1));
                Assert.That(body, Does.Contain(UIWorkspaceTooltipView.BuildPotionEffectSummary(item))
                    .And.Contain("4 tur").And.Contain("Katalizator: Silny").And.Not.Contain("siły efektów").And.Not.Contain("tura czasu trwania"));
                Assert.That(item.RuntimeEffects.Count, Is.EqualTo(2));
                Assert.That(item.RuntimeEffects[0].Value, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void EquipmentPercentageBonusDoesNotExplainItsInternalModifierType()
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            Set(definition, "Id", "test.equipment.copy");
            try
            {
                var item = new EquipmentItemInstance(definition) { StatModifiers = new List<StatModifier>
                {
                    new() { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.Percent, Value = .2f }
                }};
                var body = Plain(UIWorkspaceTooltipView.BuildEquipmentDetails(item, null));
                Assert.That(body, Does.Contain("Szybkość: +20%").And.Not.Contain("modyfikator"));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ItemDefinitionCatalogCanBeRenderedForContentAudit()
        {
            var node = new GameObject("Item catalog audit");
            var report = new StringBuilder();
            var definitions = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/ScriptableObjects" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null).OrderBy(definition => definition.Id).ToArray();
            try
            {
                var view = node.AddComponent<UIWorkspaceTooltipView>();
                foreach (var definition in definitions)
                {
                    ItemInstance item = definition switch
                    {
                        ConsumableItemDefinition consumable => new ConsumableItemInstance(consumable),
                        JewelryItemDefinition jewelry => new JewelryItemInstance(jewelry),
                        EquipmentItemDefinition equipment => new EquipmentItemInstance(equipment),
                        GalduryteItemDefinition galduryte => new GalduryteItemInstance(galduryte),
                        _ => new ItemInstance(definition)
                    };
                    var body = Body(view, item);
                    Assert.That(body, Is.Not.Empty, definition.Id);
                    report.AppendLine("## " + definition.Id + " — " + definition.DisplayName).AppendLine(body).AppendLine();
                }
                Assert.That(definitions.Length, Is.GreaterThan(20));
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../out/item-tooltip-catalog-rendered.md"));
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, report.ToString());
                TestContext.WriteLine("Rendered " + definitions.Length + " item definitions: " + path);
            }
            finally { Object.DestroyImmediate(node); }
        }
    }
}
