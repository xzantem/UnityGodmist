using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SaveManagerTests
    {
        private const string CampaignId = "campaign.test";

        private string temporaryDirectoryPath;
        private GameContentCatalog contentCatalog;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectoryPath = Path.Combine(
                Application.temporaryCachePath,
                $"GodmistSaveManagerTests_{Guid.NewGuid():N}");

            contentCatalog = ScriptableObject.CreateInstance<GameContentCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectoryPath))
            {
                Directory.Delete(temporaryDirectoryPath, true);
            }

            UnityEngine.Object.DestroyImmediate(contentCatalog);
        }

        [Test]
        public void TrySaveAndLoadQuick_RoundTripsValidSession()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var sourceSession = new GameSession(
                GameModeState.Exploration,
                CampaignId);

            var saveResult = saveManager.TrySaveQuick(
                sourceSession,
                out var saveMessage);

            var loadResult = saveManager.TryLoadQuick(
                CampaignId,
                contentCatalog,
                out var restoredSession,
                out var loadMessage);

            Assert.That(saveResult, Is.True, saveMessage);
            Assert.That(
                File.Exists(saveManager.GetQuickSavePath(CampaignId)),
                Is.True);

            Assert.That(loadResult, Is.True, loadMessage);
            Assert.That(restoredSession, Is.Not.Null);
            Assert.That(
                restoredSession.CurrentMode,
                Is.EqualTo(GameModeState.Exploration));

            Assert.That(
                restoredSession.CampaignId,
                Is.EqualTo(CampaignId));

            Assert.That(
                restoredSession.PlayerParty.MaxActiveMembers,
                Is.EqualTo(sourceSession.PlayerParty.MaxActiveMembers));
        }

        [Test]
        public void TrySaveManual_CreatesLoadableManualSave()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var sourceSession = new GameSession(
                GameModeState.Exploration,
                CampaignId);

            var saveResult = saveManager.TrySaveManual(
                sourceSession,
                "Przed Katakumbami",
                out var summary,
                out var saveMessage);

            var loadResult = saveManager.TryLoadSave(
                summary,
                contentCatalog,
                out var restoredSession,
                out var loadMessage);

            Assert.That(saveResult, Is.True, saveMessage);
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.SlotType, Is.EqualTo(SaveSlotType.Manual));
            Assert.That(summary.DisplayName, Is.EqualTo("Przed Katakumbami"));
            Assert.That(File.Exists(summary.FilePath), Is.True);

            Assert.That(loadResult, Is.True, loadMessage);
            Assert.That(restoredSession, Is.Not.Null);
            Assert.That(restoredSession.CampaignId, Is.EqualTo(CampaignId));
        }

        [Test]
        public void TryGetSaveSummaries_ReturnsManualQuickAndAutosave()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var session = new GameSession(
                GameModeState.Exploration,
                CampaignId);

            Assert.That(
                saveManager.TrySaveQuick(session, out var quickMessage),
                Is.True,
                quickMessage);

            Assert.That(
                saveManager.TrySaveAutosave(session, out var autosaveMessage),
                Is.True,
                autosaveMessage);

            Assert.That(
                saveManager.TrySaveManual(
                    session,
                    "Ręczny zapis",
                    out _,
                    out var manualMessage),
                Is.True,
                manualMessage);

            var result = saveManager.TryGetSaveSummaries(
                out var summaries,
                out var listMessage);

            Assert.That(result, Is.True, listMessage);
            Assert.That(summaries, Has.Count.EqualTo(3));
            Assert.That(
                summaries.Any(summary => summary.SlotType == SaveSlotType.Quick),
                Is.True);

            Assert.That(
                summaries.Any(summary => summary.SlotType == SaveSlotType.Autosave),
                Is.True);

            Assert.That(
                summaries.Any(summary => summary.SlotType == SaveSlotType.Manual),
                Is.True);

            Assert.That(
                summaries.All(summary => summary.CampaignId == CampaignId),
                Is.True);
        }

        [Test]
        public void TryDeleteManualSave_DeletesManualSaveOnly()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var session = new GameSession(
                GameModeState.Exploration,
                CampaignId);

            Assert.That(
                saveManager.TrySaveManual(
                    session,
                    "Do usunięcia",
                    out var manualSave,
                    out var saveMessage),
                Is.True,
                saveMessage);

            var deleteResult = saveManager.TryDeleteManualSave(
                manualSave,
                out var deleteMessage);

            Assert.That(deleteResult, Is.True, deleteMessage);
            Assert.That(File.Exists(manualSave.FilePath), Is.False);
        }

        [Test]
        public void TryDeleteManualSave_RejectsQuickSave()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var session = new GameSession(
                GameModeState.Exploration,
                CampaignId);

            Assert.That(
                saveManager.TrySaveQuick(session, out var saveMessage),
                Is.True,
                saveMessage);

            Assert.That(
                saveManager.TryGetSaveSummaries(
                    out var summaries,
                    out var listMessage),
                Is.True,
                listMessage);

            var quickSave = summaries.Single(
                summary => summary.SlotType == SaveSlotType.Quick);

            var result = saveManager.TryDeleteManualSave(
                quickSave,
                out var deleteMessage);

            Assert.That(result, Is.False);
            Assert.That(File.Exists(quickSave.FilePath), Is.True);
            Assert.That(deleteMessage, Is.Not.Empty);
        }

        [Test]
        public void TryLoadQuick_WhenFileDoesNotExist_ReturnsFalse()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var result = saveManager.TryLoadQuick(
                CampaignId,
                contentCatalog,
                out var restoredSession,
                out var message);

            Assert.That(result, Is.False);
            Assert.That(restoredSession, Is.Null);
            Assert.That(message, Is.Not.Empty);
        }

        [Test]
        public void TryLoadQuick_WhenJsonIsInvalid_ReturnsFalse()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            Directory.CreateDirectory(
                saveManager.GetCampaignDirectoryPath(CampaignId));

            File.WriteAllText(
                saveManager.GetQuickSavePath(CampaignId),
                "niepoprawny json");

            var result = saveManager.TryLoadQuick(
                CampaignId,
                contentCatalog,
                out var restoredSession,
                out var message);

            Assert.That(result, Is.False);
            Assert.That(restoredSession, Is.Null);
            Assert.That(message, Is.Not.Empty);
        }

        [Test]
        public void TrySaveAutosave_WritesSeparateCampaignAutosaveFile()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var session = new GameSession(
                GameModeState.Exploration,
                CampaignId);

            var result = saveManager.TrySaveAutosave(
                session,
                out var message);

            Assert.That(result, Is.True, message);

            Assert.That(
                File.Exists(saveManager.GetAutosavePath(CampaignId)),
                Is.True);

            Assert.That(
                File.Exists(saveManager.GetQuickSavePath(CampaignId)),
                Is.False);

            var json = File.ReadAllText(
                saveManager.GetAutosavePath(CampaignId));

            var serializer = new GameSaveJsonSerializer();

            Assert.That(
                serializer.TryDeserialize(json, out var saveData),
                Is.True);

            Assert.That(
                saveData.DisplayName,
                Is.EqualTo("Automatyczny zapis"));

            Assert.That(
                saveData.CampaignId,
                Is.EqualTo(CampaignId));
        }

        [Test]
        public void TrySaveQuick_WhenCampaignIdHasPathSeparator_ReturnsFalse()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var session = new GameSession(
                GameModeState.Exploration,
                "campaign/invalid");

            var result = saveManager.TrySaveQuick(
                session,
                out var message);

            Assert.That(result, Is.False);
            Assert.That(message, Is.Not.Empty);
        }
    }
}