using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class NpcLoyaltyServiceTests
    {
        [SetUp]
        public void SetUp() => GameDefinesRuntime.Configure(null);

        [TearDown]
        public void TearDown() => GameDefinesRuntime.Configure(null);

        [Test]
        public void DefaultFormula_UsesConfiguredTransitionCostsAndCumulativeThresholds()
        {
            var service = new NpcLoyaltyService();

            Assert.That(service.InitialLevel, Is.EqualTo(1));
            Assert.That(service.MaximumLevel, Is.EqualTo(15));
            Assert.That(service.GetGoldCostForNextLevel(1), Is.EqualTo(407));
            Assert.That(service.GetGoldCostForNextLevel(2), Is.EqualTo(646));
            Assert.That(service.GetGoldCostForNextLevel(3), Is.EqualTo(1024));
            Assert.That(service.GetGoldRequiredToReachLevel(15), Is.EqualTo(445591));
        }

        [Test]
        public void RecordGoldSpent_AdvancesOnlyAfterCurrentTransitionIsPaid()
        {
            var service = new NpcLoyaltyService();
            var npc = new NpcInstance();

            Assert.That(service.TryRecordGoldSpent(npc, 406), Is.True);
            Assert.That(npc.LoyaltyLevel, Is.EqualTo(1));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(406));
            Assert.That(service.GetGoldSpentTowardNextLevel(npc), Is.EqualTo(406));
            Assert.That(service.GetGoldRequiredForNextLevel(npc), Is.EqualTo(407));

            Assert.That(service.TryRecordGoldSpent(npc, 1), Is.True);
            Assert.That(npc.LoyaltyLevel, Is.EqualTo(2));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(407));
            Assert.That(service.GetGoldSpentTowardNextLevel(npc), Is.EqualTo(0));
        }

        [Test]
        public void RecordGoldSpent_CanCrossMultipleThresholdsAndStopsAtMaximum()
        {
            var service = new NpcLoyaltyService();
            var npc = new NpcInstance();

            Assert.That(service.TryRecordGoldSpent(npc, 445591), Is.True);

            Assert.That(npc.LoyaltyLevel, Is.EqualTo(15));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(445591));
            Assert.That(service.GetGoldRequiredForNextLevel(npc), Is.EqualTo(0));
            Assert.That(service.GetGoldSpentTowardNextLevel(npc), Is.EqualTo(0));

            Assert.That(service.TryRecordGoldSpent(npc, 10), Is.True);
            Assert.That(npc.LoyaltyLevel, Is.EqualTo(15));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(445601));
            Assert.That(service.GetGoldSpentTowardNextLevel(npc), Is.EqualTo(0));
        }

        [Test]
        public void Factory_UsesConfiguredInitialLoyaltyLevel()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            ClearEntries(definitions);
            definitions.AddEntry(CreateInteger(
                GameDefineIds.NpcLoyaltyInitialLevel, 3, 1, 15));
            definitions.AddEntry(CreateInteger(
                GameDefineIds.NpcLoyaltyMaximumLevel, 15, 1, 100));
            GameDefinesRuntime.Configure(definitions);

            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(npcDefinition, "Id", "npc.loyalty.test");
            SetAutoProperty(npcDefinition, "Roles", new List<NpcRoleData>());

            var npc = new NpcInstanceFactory().Create(npcDefinition);

            Assert.That(npc.LoyaltyLevel, Is.EqualTo(3));

            Object.DestroyImmediate(npcDefinition);
            Object.DestroyImmediate(definitions);
        }

        [Test]
        public void Formula_UsesGameDefineOverrides()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            ClearEntries(definitions);
            definitions.AddEntry(CreateInteger(
                GameDefineIds.NpcLoyaltyMaximumLevel, 5, 1, 100));
            definitions.AddEntry(CreateFloat(
                GameDefineIds.NpcLoyaltyThresholdBase, 2f, 1.01f, 100f));
            definitions.AddEntry(CreateFloat(
                GameDefineIds.NpcLoyaltyThresholdExponentOffset, 0f, 0f, 100f));
            definitions.AddEntry(CreateFloat(
                GameDefineIds.NpcLoyaltyThresholdLevelDivisor, 1f, .01f, 100f));
            GameDefinesRuntime.Configure(definitions);

            var service = new NpcLoyaltyService();

            Assert.That(service.GetGoldCostForNextLevel(1), Is.EqualTo(2));

            Object.DestroyImmediate(definitions);
        }

        [Test]
        public void InvalidSpend_DoesNotMutateNpcState()
        {
            var service = new NpcLoyaltyService();
            var npc = new NpcInstance { LoyaltyLevel = 2, SpentGoldTotal = 10 };

            Assert.That(service.TryRecordGoldSpent(npc, 0), Is.False);
            Assert.That(service.TryRecordGoldSpent(npc, -1), Is.False);
            Assert.That(npc.LoyaltyLevel, Is.EqualTo(2));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(10));
        }

        [Test]
        public void Discount_UsesCumulativeLoyaltyBands()
        {
            var service = new NpcLoyaltyService();

            Assert.That(service.GetDiscountPercent(1), Is.EqualTo(0));
            Assert.That(service.GetDiscountPercent(2), Is.EqualTo(1));
            Assert.That(service.GetDiscountPercent(3), Is.EqualTo(1));
            Assert.That(service.GetDiscountPercent(4), Is.EqualTo(4));
            Assert.That(service.GetDiscountPercent(6), Is.EqualTo(4));
            Assert.That(service.GetDiscountPercent(7), Is.EqualTo(9));
            Assert.That(service.GetDiscountPercent(8), Is.EqualTo(9));
            Assert.That(service.GetDiscountPercent(9), Is.EqualTo(16));
            Assert.That(service.GetDiscountPercent(11), Is.EqualTo(16));
            Assert.That(service.GetDiscountPercent(12), Is.EqualTo(25));
            Assert.That(service.GetDiscountPercent(15), Is.EqualTo(25));
            Assert.That(service.GetDiscountedGoldCost(
                new NpcInstance { LoyaltyLevel = 12 },
                100),
                Is.EqualTo(75));
        }

        private static GameDefineEntry CreateInteger(
            string id, int value, int minimum, int maximum) => new()
        {
            Id = id,
            DisplayName = id,
            Category = GameDefineCategory.Progression,
            ValueType = GameDefineValueType.Integer,
            DefaultIntegerValue = value,
            CurrentIntegerValue = value,
            MinimumIntegerValue = minimum,
            MaximumIntegerValue = maximum
        };

        private static GameDefineEntry CreateFloat(
            string id, float value, float minimum, float maximum) => new()
        {
            Id = id,
            DisplayName = id,
            Category = GameDefineCategory.Progression,
            ValueType = GameDefineValueType.Float,
            DefaultFloatValue = value,
            CurrentFloatValue = value,
            MinimumFloatValue = minimum,
            MaximumFloatValue = maximum
        };

        private static void ClearEntries(GameDefines definitions)
        {
            while (definitions.Entries.Count > 0)
            {
                definitions.RemoveEntryAt(definitions.Entries.Count - 1);
            }
        }

        private static void SetAutoProperty(
            object target, string propertyName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, $"Nie znaleziono backing field dla {propertyName}.");
            field.SetValue(target, value);
        }
    }
}
