using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public sealed class SaveRestoreDiagnosticsTests
    {
        private GameContentCatalog catalog;
        private ItemDefinition material;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            material = ScriptableObject.CreateInstance<ItemDefinition>();
            typeof(ItemDefinition).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(material, "material.diagnostic");
            catalog.ItemDefinitions.Add(material);
        }

        [TearDown]
        public void TearDown()
        { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(material); }

        private static GameSaveData Empty()
        {
            var data = GameSaveData.CreateNew("Diagnostic fixture", "diagnostic.fixture");
            data.Party.MaxActiveMembers = Party.DefaultMaxActiveMembers;
            return data;
        }

        [TestCase("null", "$")]
        [TestCase("version", "$.SaveVersion")]
        [TestCase("party", "$.Party")]
        [TestCase("limit", "$.Party.MaxActiveMembers")]
        [TestCase("gold", "$.Gold")]
        public void HeaderFailure_IdentifiesTheActualField(string kind, string path)
        {
            var data = Empty();
            switch (kind)
            {
                case "null": data = null; break;
                case "version": data.SaveVersion = 999; break;
                case "party": data.Party = null; break;
                case "limit": data.Party.MaxActiveMembers++; break;
                case "gold": data.Gold = -1; break;
            }
            var failure = Reject(data);
            Assert.That(failure.Stage, Is.EqualTo("Header"));
            Assert.That(failure.FieldPath, Is.EqualTo(path));
            if (kind == "version") Assert.That(failure.Code, Is.EqualTo(SaveRestoreErrorCode.UnsupportedVersion));
        }

        [TestCase("missing-definition", "ItemDefinitionId", SaveRestoreErrorCode.UnknownReference)]
        [TestCase("blank-id", "InstanceId", SaveRestoreErrorCode.MissingValue)]
        [TestCase("rarity", "CurrentRarity", SaveRestoreErrorCode.InvalidValue)]
        [TestCase("duplicate", "InstanceId", SaveRestoreErrorCode.DuplicateId)]
        public void ItemFailure_ReportsIndexIdAndSpecificField(string kind, string field, SaveRestoreErrorCode code)
        {
            var data = Empty();
            data.ItemInstances.Add(Item("item.first"));
            data.StashItemInstanceIds.Add("item.first");
            var bad = Item("item.bad");
            switch (kind)
            {
                case "missing-definition": bad.ItemDefinitionId = "missing.definition"; break;
                case "blank-id": bad.InstanceId = ""; break;
                case "rarity": bad.CurrentRarity = (RarityTier)999; break;
                case "duplicate": bad.InstanceId = "item.first"; break;
            }
            data.ItemInstances.Add(bad);
            var failure = Reject(data);
            Assert.That(failure.Stage, Is.EqualTo("Items"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.ItemInstances[1]." + field));
            Assert.That(failure.EntityId, Is.EqualTo(bad.InstanceId));
            Assert.That(failure.Code, Is.EqualTo(code));
        }

        [TestCase(false)] [TestCase(true)]
        public void ItemReferenceFailure_DistinguishesMissingFromAlreadyAssigned(bool duplicate)
        {
            var data = Empty();
            data.ItemInstances.Add(Item("item.first"));
            data.StashItemInstanceIds.Add("item.first");
            data.StashItemInstanceIds.Add(duplicate ? "item.first" : "item.missing");
            var failure = Reject(data);
            Assert.That(failure.Stage, Is.EqualTo("Stash"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.StashItemInstanceIds[1]"));
            Assert.That(failure.EntityId, Is.EqualTo(data.StashItemInstanceIds[1]));
            Assert.That(failure.Code, Is.EqualTo(duplicate ? SaveRestoreErrorCode.AlreadyAssigned : SaveRestoreErrorCode.UnknownReference));
        }

        [Test]
        public void UnknownCharacter_IsReportedBeforeItsParentFailure()
        {
            var data = Empty();
            data.Characters.Add(new CharacterSaveData { CharacterId = "missing.character", ClassId = "missing.class" });
            var failure = Reject(data);
            Assert.That(failure.Stage, Is.EqualTo("Characters"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.Characters[0].CharacterId"));
            Assert.That(failure.EntityId, Is.EqualTo("missing.character"));
            Assert.That(failure.Code, Is.EqualTo(SaveRestoreErrorCode.UnknownReference));
        }

        [Test]
        public void PartyReference_HasItsOwnStageAndRecord()
        {
            var data = Empty();
            data.Party.ActiveMemberIds.Add("missing.member");
            var failure = Reject(data);
            Assert.That(failure.Stage, Is.EqualTo("Party"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.Party.ActiveMemberIds[0]"));
            Assert.That(failure.EntityId, Is.EqualTo("missing.member"));
        }

        [TestCase("shield")] [TestCase("payload")]
        public void NestedCharacterFailure_KeepsFullPathAndNestedId(string kind)
        {
            var realCatalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>("Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile, out var source, out var reason), Is.True, reason);
            var data = new GameSessionSaveSnapshotFactory().CreateSnapshot(source, "Nested diagnostic");
            var character = data.Characters[0];
            string suffix, entity;
            if (kind == "shield")
            {
                character.ShieldLayers = new List<ShieldLayerSaveData> { new() { SourceId = "broken.shield", CurrentValue = -1 } };
                suffix = ".ShieldLayers[0].CurrentValue"; entity = "broken.shield";
            }
            else
            {
                character.PassiveEffects = new List<PassiveEffectSaveData> { new()
                {
                    EffectId = "broken.effect", Stacks = 1,
                    Payloads = new List<PassiveEffectPayloadSaveData> { new() { Type = (PassiveEffectPayloadType)999 } }
                } };
                suffix = ".PassiveEffects[0].Payloads[0].Type"; entity = "broken.effect";
            }
            var failure = Reject(data, realCatalog);
            Assert.That(failure.Stage, Is.EqualTo("Characters"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.Characters[0]" + suffix));
            Assert.That(failure.EntityId, Is.EqualTo(entity));
        }

        [Test]
        public void MissingCatalog_HasExplicitConfigurationDiagnostic()
        {
            Assert.That(new GameSessionRestoreFactory().TryCreate(Empty(), null, out var session, out var failure), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(failure.Code, Is.EqualTo(SaveRestoreErrorCode.MissingValue));
            Assert.That(failure.FieldPath, Is.EqualTo("$.ContentCatalog"));
        }

        [Test]
        public void OrphanItem_ReportsItsIndexAndId()
        {
            var data = Empty();
            data.ItemInstances.Add(Item("orphan.item"));
            var failure = Reject(data);
            Assert.That(failure.Code, Is.EqualTo(SaveRestoreErrorCode.UnassignedItem));
            Assert.That(failure.Stage, Is.EqualTo("Ownership"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.ItemInstances[0].InstanceId"));
            Assert.That(failure.EntityId, Is.EqualTo("orphan.item"));
        }

        [Test]
        public void FailureIsPerInvocation_AndValidV1SaveIsNotModified()
        {
            var factory = new GameSessionRestoreFactory();
            var bad = Empty(); bad.Gold = -1;
            Assert.That(factory.TryCreate(bad, catalog, out _, out var failure), Is.False);
            var firstPath = failure.FieldPath;
            var valid = Empty();
            var serializer = new GameSaveJsonSerializer();
            var before = serializer.Serialize(valid);
            Assert.That(factory.TryCreate(valid, catalog, out var session, out var successFailure), Is.True);
            Assert.That(session, Is.Not.Null);
            Assert.That(successFailure, Is.Null);
            Assert.That(serializer.Serialize(valid), Is.EqualTo(before));
            Assert.That(failure.FieldPath, Is.EqualTo(firstPath));
        }

        [Test]
        public void SaveManager_PropagatesDiagnosticsWithoutRewritingTheFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "GodmistDiagnostics-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new SaveManager(directory);
                var active = new GameSession(GameModeState.Exploration, "diagnostic.fixture");
                active.TryAddGold(123);
                Assert.That(manager.TrySaveQuick(active, out var message), Is.True, message);
                var path = manager.GetQuickSavePath(active.CampaignId);
                var serializer = new GameSaveJsonSerializer();
                Assert.That(serializer.TryDeserialize(File.ReadAllText(path), out var data), Is.True);
                data.ItemInstances.Add(new ItemSaveData { InstanceId = "broken.item", ItemDefinitionId = "missing.definition" });
                var bytes = serializer.Serialize(data);
                File.WriteAllText(path, bytes);
                LogAssert.Expect(LogType.Error, new Regex(@"Etap: Items; pole: \$\.ItemInstances\[0\]\.ItemDefinitionId; kod: UnknownReference.*ID: 'broken.item'"));
                Assert.That(manager.TryLoadQuick(active.CampaignId, catalog, out var loaded, out message), Is.False);
                Assert.That(loaded, Is.Null);
                Assert.That(message, Does.Contain("$.ItemInstances[0].ItemDefinitionId"));
                Assert.That(message, Does.Contain("UnknownReference"));
                Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
                Assert.That(active.Gold, Is.EqualTo(123));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [TestCase(true)] [TestCase(false)]
        public void ExplorationCoordinatesIdentifyTheFailingCoordinate(bool x)
        {
            var data = Empty(); data.HasExplorationPosition = true;
            if (x) data.ExplorationPositionX = float.NaN; else data.ExplorationPositionY = float.PositiveInfinity;
            var failure = Reject(data);
            Assert.That(failure.FieldPath, Is.EqualTo(x ? "$.ExplorationPositionX" : "$.ExplorationPositionY"));
            Assert.That(failure.Detail, Does.Not.Contain("Sprawdzenie pól"));
        }

        [TestCase("duration", "RemainingDuration")]
        [TestCase("stacks", "Stacks")]
        [TestCase("lifetime", "Lifetime")]
        [TestCase("redirect", "Payloads[0].DamageRedirectPercent")]
        [TestCase("recovery", "Payloads[0].ResourcePercent")]
        [TestCase("state", "StateValues[0].NumberValue")]
        public void EachNestedPredicateReportsItsOwnField(string kind, string expected)
        {
            var realCatalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>("Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile, out var source, out var reason), Is.True, reason);
            var data = new GameSessionSaveSnapshotFactory().CreateSnapshot(source, "Predicate diagnostic");
            var effect = new PassiveEffectSaveData { EffectId = "test.diagnostic", Stacks = 1, RemainingDuration = 1, Lifetime = PassiveEffectLifetime.Timed };
            data.Characters[0].PassiveEffects = new() { effect };
            switch (kind)
            {
                case "duration": effect.RemainingDuration = -1; break;
                case "stacks": effect.Stacks = -1; break;
                case "lifetime": effect.Lifetime = (PassiveEffectLifetime)999; break;
                case "redirect": effect.Payloads = new() { new() { Type = PassiveEffectPayloadType.DamageRedirect, DamageRedirectPercent = 2 } }; break;
                case "recovery": effect.Payloads = new() { new() { Type = PassiveEffectPayloadType.PeriodicRecovery, ResourcePercent = -1 } }; break;
                case "state": effect.StateValues = new() { new() { Key = "test", NumberValue = float.NaN } }; break;
            }
            Assert.That(Reject(data, realCatalog).FieldPath, Is.EqualTo("$.Characters[0].PassiveEffects[0]." + expected));
        }

        [Test] public void LegacyChillIsNormalizedDuringRestoreAndPersistsAsOrdinaryPayload()
        {
            var realCatalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>("Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile, out var source, out var reason), Is.True, reason);
            var data = new GameSessionSaveSnapshotFactory().CreateSnapshot(source, "Legacy chill");
            data.Characters[0].PassiveEffects = new() { new() { EffectId = Godmist.Core.Constants.StatusEffectIds.Chill,
                Stacks = 1, RemainingDuration = 2, Lifetime = PassiveEffectLifetime.Timed } };
            Assert.That(new GameSessionRestoreFactory().TryCreate(data, realCatalog, out var restored, out var failure), Is.True, failure?.ToString());
            var character = restored.CharactersById[data.Characters[0].CharacterId];
            var effect = character.PassiveEffects.Single(e => e.EffectId == Godmist.Core.Constants.StatusEffectIds.Chill);
            Assert.That(effect.StatModifiers.Single().Value, Is.EqualTo(20));
            Assert.That(character.Stats.Speed.GetValue(), Is.LessThanOrEqualTo(20));
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(restored, "Normalized chill");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, realCatalog, out var second, out failure), Is.True, failure?.ToString());
            Assert.That(second.CharactersById[character.CharacterId].PassiveEffects.Single(e => e.EffectId == effect.EffectId).StatModifiers.Count, Is.EqualTo(1));
        }

        [TestCase("selection", "SelectedIndex")]
        [TestCase("progress", "HighestMilestoneProcessed")]
        [TestCase("completed", "CompletedExpeditions")]
        [TestCase("offers", "Offers")]
        public void DungeonOfferFailureComesFromItsDomainOwner(string kind, string expected)
        {
            var data = Empty();
            data.DungeonOffers = new DungeonOfferBoardSaveData();
            switch (kind)
            {
                case "selection": data.DungeonOffers.SelectedIndex = 3; break;
                case "progress": data.DungeonOffers.HighestMilestoneProcessed = 11; break;
                case "completed": data.DungeonOffers.CompletedExpeditions = -1; break;
                case "offers": data.DungeonOffers.Offers = null; break;
            }
            var failure = Reject(data);
            Assert.That(failure.Stage, Is.EqualTo("DungeonOffers"));
            Assert.That(failure.FieldPath, Is.EqualTo("$.DungeonOffers." + expected));
            Assert.That(failure.Detail, Is.Not.Empty);
        }

        private ItemSaveData Item(string id) => new() { InstanceId = id, ItemDefinitionId = material.Id, CurrentRarity = RarityTier.Common };

        private SaveRestoreFailure Reject(GameSaveData data, GameContentCatalog fromCatalog = null)
        {
            Assert.That(new GameSessionRestoreFactory().TryCreate(data, fromCatalog ?? catalog, out var session, out var failure), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(failure, Is.Not.Null);
            TestContext.WriteLine(failure.ToString());
            return failure;
        }
    }
}
