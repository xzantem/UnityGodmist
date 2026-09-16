using Godmist.Data.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class GameDefinesTests
    {
        [SetUp]
        public void SetUp()
        {
            GameDefinesRuntime.Configure(null);
        }

        [TearDown]
        public void TearDown()
        {
            GameDefinesRuntime.Configure(null);
        }

        [Test]
        public void WindowRecreatesSerializedWrapperWhenAssetSurvivesReload()
        {
            System.Type type = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((type = assembly.GetType("Godmist.Editor.GameDefinesWindow")) != null) break;
            Assert.That(type, Is.Not.Null);
            var window = ScriptableObject.CreateInstance(type);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var wrapperField = type.GetField("serializedDefinitions", flags);
            var assetField = type.GetField("definitions", flags);
            try
            {
                var asset = assetField.GetValue(window);
                Assert.That(asset, Is.Not.Null);
                ((UnityEditor.SerializedObject)wrapperField.GetValue(window))?.Dispose();
                wrapperField.SetValue(window, null);
                type.GetMethod("EnsureAsset", flags).Invoke(window, new object[] { false });
                var restored = (UnityEditor.SerializedObject)wrapperField.GetValue(window);
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.targetObject, Is.SameAs(asset));
                Assert.DoesNotThrow(() => restored.Update());
                type.GetMethod("OnDisable", flags).Invoke(window, null);
                Assert.That(wrapperField.GetValue(window), Is.Null);
                type.GetMethod("OnEnable", flags).Invoke(window, null);
                Assert.That(wrapperField.GetValue(window), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void ContactEscapeReadsConfiguredTurnsAtCreation()
        {
            var before = new Godmist.Runtime.Combat.DungeonContactState((enemy, player) => false);
            Assert.That(before.EscapeReactionTurns, Is.EqualTo(2));
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            try
            {
                while (definitions.Entries.Count > 0) definitions.RemoveEntryAt(definitions.Entries.Count - 1);
                definitions.AddEntry(new GameDefineEntry
                {
                    Id = GameDefineIds.EscapeContactLossTurns,
                    DisplayName = "Tury bez kontaktu",
                    Category = GameDefineCategory.Combat,
                    ValueType = GameDefineValueType.Integer,
                    CurrentIntegerValue = 3, DefaultIntegerValue = 2,
                    MinimumIntegerValue = 1, MaximumIntegerValue = 100
                });
                GameDefinesRuntime.Configure(definitions);
                Assert.That(new Godmist.Runtime.Combat.DungeonContactState((enemy, player) => false).EscapeReactionTurns, Is.EqualTo(3));
                Assert.That(before.EscapeReactionTurns, Is.EqualTo(2), "Existing fights retain their reaction period.");
            }
            finally { Object.DestroyImmediate(definitions); }
        }

        [Test]
        public void MissingAssetUsesCurrentSafeDefaults()
        {
            Assert.That(
                GameDefinesRuntime.GetFloat(GameDefineIds.ActionPointsPerSpeed),
                Is.EqualTo(.25f));
            Assert.That(
                GameDefinesRuntime.GetInt(GameDefineIds.MaximumCharacterLevel),
                Is.EqualTo(50));
            Assert.That(
                GameDefinesRuntime.GetFloat(GameDefineIds.ConsumableDuplicateSecondMultiplier),
                Is.EqualTo(.75f));
            Assert.That(
                GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandEndgameEndLevel),
                Is.EqualTo(40));
            Assert.That(
                GameDefinesRuntime.GetFloat(GameDefineIds.EquipmentRarityCommonWeight),
                Is.EqualTo(.5f));
        }

        [Test]
        public void ConfiguredAssetOverridesDefaultWithoutSaveStateCoupling()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            while (definitions.Entries.Count > 0)
            {
                definitions.RemoveEntryAt(definitions.Entries.Count - 1);
            }
            definitions.AddEntry(new GameDefineEntry
            {
                Id = GameDefineIds.ActionPointsPerSpeed,
                DisplayName = "AP na Speed",
                Category = GameDefineCategory.Combat,
                ValueType = GameDefineValueType.Float,
                CurrentFloatValue = .4f,
                DefaultFloatValue = .25f,
                MinimumFloatValue = 0f,
                MaximumFloatValue = 1f
            });

            GameDefinesRuntime.Configure(definitions);

            Assert.That(
                GameDefinesRuntime.GetFloat(GameDefineIds.ActionPointsPerSpeed),
                Is.EqualTo(.4f));
            Assert.That(GameDefinesRuntime.GetInt(GameDefineIds.MaximumCharacterLevel), Is.EqualTo(50));

            Object.DestroyImmediate(definitions);
        }

        [Test]
        public void InvalidRangeIsReported()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            definitions.AddEntry(new GameDefineEntry
            {
                Id = "combat.invalid",
                DisplayName = "Invalid",
                Category = GameDefineCategory.Combat,
                ValueType = GameDefineValueType.Integer,
                DefaultIntegerValue = 5,
                CurrentIntegerValue = 7,
                MinimumIntegerValue = 0,
                MaximumIntegerValue = 6
            });

            var errors = definitions.Validate();

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("poza zakresem"));
            Object.DestroyImmediate(definitions);
        }

        [Test]
        public void BooleanAndStringValuesAreSupported()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            definitions.AddEntry(new GameDefineEntry
            {
                Id = "combat.experimental",
                DisplayName = "Experimental",
                Category = GameDefineCategory.Combat,
                ValueType = GameDefineValueType.Boolean,
                DefaultBooleanValue = false,
                CurrentBooleanValue = true
            });
            definitions.AddEntry(new GameDefineEntry
            {
                Id = "content.default_tag",
                DisplayName = "Default tag",
                Category = GameDefineCategory.Items,
                ValueType = GameDefineValueType.String,
                DefaultStringValue = "default",
                CurrentStringValue = "authoring"
            });

            GameDefinesRuntime.Configure(definitions);

            Assert.That(GameDefinesRuntime.GetBool("combat.experimental"), Is.True);
            Assert.That(GameDefinesRuntime.GetString("content.default_tag"), Is.EqualTo("authoring"));
            Object.DestroyImmediate(definitions);
        }

        [Test]
        public void DuplicateIdsAreReported()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            definitions.AddEntry(CreateInteger("duplicate", 1));
            definitions.AddEntry(CreateInteger("duplicate", 2));

            var errors = definitions.Validate();

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Zduplikowany"));
            Object.DestroyImmediate(definitions);
        }

        private static GameDefineEntry CreateInteger(string id, int value)
        {
            return new GameDefineEntry
            {
                Id = id,
                DisplayName = id,
                Category = GameDefineCategory.Combat,
                ValueType = GameDefineValueType.Integer,
                DefaultIntegerValue = value,
                CurrentIntegerValue = value,
                MinimumIntegerValue = 0,
                MaximumIntegerValue = 10
            };
        }
    }
}
