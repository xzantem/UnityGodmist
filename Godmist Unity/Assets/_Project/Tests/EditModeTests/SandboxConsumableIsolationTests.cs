using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class SandboxConsumableIsolationTests
    {
        private ConsumableItemDefinition definition;
        private CharacterInstance campaign;
        private CharacterInstance sandbox;
        private ConsumableItemInstance original;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            Set("Id", "test.sandbox.potion");
            Set("MaxCharges", 3);
            Set("UseType", ConsumableUseType.Potion);
            campaign = new CharacterInstance();
            sandbox = new CharacterInstance();
            original = new ConsumableItemInstance(definition, "campaign.potion")
            {
                CurrentRarity = RarityTier.Rare, ItemLevel = 17, CustomName = "Mikstura testowa",
                IsSaleProtected = true, CurrentCharges = 3, BlastRange = 7, BlastAreaSize = 4,
                CatalystDurationModifier = 2, CatalystEffectMultiplier = 1.15f,
                CatalystChargeBonus = 1, CatalystDisplayName = "Katalizator", CraftedBaseValue = 123
            };
            original.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.ModifyDamage, Value = .25f,
                DurationTurns = 4, Chance = .8f, TargetModeIndex = 1, StatusId = "test.status",
                ResistanceType = ResistanceType.Fire, TargetType = TargetType.Caster,
                TargetingType = TargetingType.Allies
            });
            campaign.Inventory.Items.Add(original);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(definition);

        [Test]
        public void Import_PreservesSnapshotButSharesOnlyDefinition()
        {
            Assert.That(new SandboxConsumableSnapshot().Import(campaign, sandbox), Is.EqualTo(1));
            var copy = (ConsumableItemInstance)sandbox.Inventory.Items.Single();
            Assert.That(copy, Is.Not.SameAs(original));
            Assert.That(copy.Definition, Is.SameAs(definition));
            Assert.That(copy.InstanceId, Is.Not.EqualTo(original.InstanceId));
            foreach (var field in typeof(ConsumableItemInstance).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name == nameof(ItemInstance.InstanceId) || field.Name == nameof(ConsumableItemInstance.RuntimeEffects)) continue;
                Assert.That(field.GetValue(copy), Is.EqualTo(field.GetValue(original)), field.Name);
            }
            Assert.That(copy.RuntimeEffects, Is.Not.SameAs(original.RuntimeEffects));
            Assert.That(copy.RuntimeEffects[0], Is.Not.SameAs(original.RuntimeEffects[0]));
            foreach (var field in typeof(ConsumableEffectRuntime).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(field.GetValue(copy.RuntimeEffects[0]), Is.EqualTo(field.GetValue(original.RuntimeEffects[0])), field.Name);

            copy.RuntimeEffects[0].Value = 999;
            copy.RuntimeEffects.Clear();
            copy.CatalystEffectMultiplier = 9;
            Assert.That(original.RuntimeEffects.Single().Value, Is.EqualTo(.25f));
            Assert.That(original.CatalystEffectMultiplier, Is.EqualTo(1.15f));
        }

        [Test]
        public void SpendingAndRefillingSandboxCharges_DoesNotChangeCampaignOrReimportConsumedCopy()
        {
            var snapshot = new SandboxConsumableSnapshot();
            snapshot.Import(campaign, sandbox);
            var copy = (ConsumableItemInstance)sandbox.Inventory.Items.Single();
            var service = new ConsumableService();
            Assert.That(service.TrySpendCharge(sandbox, copy), Is.True);
            Assert.That(copy.CurrentCharges, Is.EqualTo(2));
            Assert.That(original.CurrentCharges, Is.EqualTo(3));
            copy.CurrentCharges = copy.MaxCharges; // The refill service mutates this same field.
            Assert.That(original.CurrentCharges, Is.EqualTo(3));
            while (copy.HasCharges) Assert.That(service.TrySpendCharge(sandbox, copy), Is.True);
            Assert.That(sandbox.Inventory.Items, Is.Empty);
            Assert.That(campaign.Inventory.Items.Single(), Is.SameAs(original));
            Assert.That(original.CurrentCharges, Is.EqualTo(3));
            Assert.That(snapshot.Import(campaign, sandbox), Is.Zero);
            Assert.That(sandbox.Inventory.Items, Is.Empty);

            var added = new ConsumableItemInstance(definition, "campaign.new");
            campaign.Inventory.Items.Add(added);
            Assert.That(snapshot.Import(campaign, sandbox), Is.EqualTo(1));
            Assert.That(sandbox.Inventory.Items.Single(), Is.Not.SameAs(added));
            Assert.That(snapshot.Import(campaign, sandbox), Is.Zero);
        }

        [Test]
        public void RepeatedSync_DoesNotOverwriteSnapshotWithCampaignChanges()
        {
            var snapshot = new SandboxConsumableSnapshot();
            snapshot.Import(campaign, sandbox);
            original.CurrentCharges = 1;
            original.RuntimeEffects[0].DurationTurns = 50;
            original.CatalystChargeBonus = 5;
            Assert.That(snapshot.Import(campaign, sandbox), Is.Zero);
            var copy = (ConsumableItemInstance)sandbox.Inventory.Items.Single();
            Assert.That(copy.CurrentCharges, Is.EqualTo(3));
            Assert.That(copy.RuntimeEffects[0].DurationTurns, Is.EqualTo(4));
            Assert.That(copy.CatalystChargeBonus, Is.EqualTo(1));
        }

        [Test]
        public void TargetsAndNewSandboxLifetimes_HaveIndependentCopies()
        {
            var snapshot = new SandboxConsumableSnapshot();
            var second = new CharacterInstance();
            snapshot.Import(campaign, sandbox);
            snapshot.Import(campaign, second);
            Assert.That(second.Inventory.Items.Single(), Is.Not.SameAs(sandbox.Inventory.Items.Single()));
            sandbox.Inventory.Items.Clear();
            Assert.That(snapshot.Import(campaign, sandbox), Is.Zero);
            Assert.That(new SandboxConsumableSnapshot().Import(campaign, sandbox), Is.EqualTo(1));
        }

        [Test]
        public void Import_HandlesEmptyEffectsAndDoesNotImportIntoSource()
        {
            var snapshot = new SandboxConsumableSnapshot();
            Assert.That(snapshot.Import(campaign, campaign), Is.Zero);
            Assert.That(campaign.Inventory.Items, Has.Count.EqualTo(1));
            original.RuntimeEffects = null;
            Assert.That(snapshot.Import(campaign, sandbox), Is.EqualTo(1));
            Assert.That(((ConsumableItemInstance)sandbox.Inventory.Items.Single()).RuntimeEffects, Is.Null);
        }

        private void Set(string name, object value)
        {
            for (var type = definition.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(definition, value);
                return;
            }
            Assert.Fail("Missing fixture field: " + name);
        }
    }
}
