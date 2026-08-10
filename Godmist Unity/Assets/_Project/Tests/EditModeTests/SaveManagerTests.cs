using System;
using System.IO;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SaveManagerTests
    {
        private string temporaryDirectoryPath;
        private GameContentCatalog contentCatalog;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectoryPath = Path.Combine(
                Application.temporaryCachePath,
                $"GodmistSaveManagerTests_{Guid.NewGuid():N}");

            contentCatalog = ScriptableObject.CreateInstance<
                GameContentCatalog>();
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
                GameModeState.Exploration);

            var saveResult = saveManager.TrySaveQuick(
                sourceSession,
                out var saveMessage);

            var loadResult = saveManager.TryLoadQuick(
                contentCatalog,
                out var restoredSession,
                out var loadMessage);

            Assert.That(saveResult, Is.True, saveMessage);
            Assert.That(
                File.Exists(saveManager.QuickSavePath),
                Is.True);

            Assert.That(loadResult, Is.True, loadMessage);
            Assert.That(restoredSession, Is.Not.Null);
            Assert.That(
                restoredSession.CurrentMode,
                Is.EqualTo(GameModeState.Exploration));
            Assert.That(
                restoredSession.PlayerParty.MaxActiveMembers,
                Is.EqualTo(sourceSession.PlayerParty.MaxActiveMembers));
        }

        [Test]
        public void TryLoadQuick_WhenFileDoesNotExist_ReturnsFalse()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);

            var result = saveManager.TryLoadQuick(
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
                saveManager.SaveDirectoryPath);

            File.WriteAllText(
                saveManager.QuickSavePath,
                "niepoprawny json");

            var result = saveManager.TryLoadQuick(
                contentCatalog,
                out var restoredSession,
                out var message);

            Assert.That(result, Is.False);
            Assert.That(restoredSession, Is.Null);
            Assert.That(message, Is.Not.Empty);
        }

        [Test]
        public void TrySaveAutosave_WritesSeparateAutosaveFile()
        {
            var saveManager = new SaveManager(temporaryDirectoryPath);
            var session = new GameSession(
                GameModeState.Exploration);

            var result = saveManager.TrySaveAutosave(
                session,
                out var message);

            Assert.That(result, Is.True, message);
            Assert.That(
                File.Exists(saveManager.AutosavePath),
                Is.True);

            Assert.That(
                File.Exists(saveManager.QuickSavePath),
                Is.False);

            var json = File.ReadAllText(
                saveManager.AutosavePath);

            var serializer = new GameSaveJsonSerializer();

            Assert.That(
                serializer.TryDeserialize(json, out var saveData),
                Is.True);

            Assert.That(
                saveData.DisplayName,
                Is.EqualTo("Automatyczny zapis"));
        }
    }
}