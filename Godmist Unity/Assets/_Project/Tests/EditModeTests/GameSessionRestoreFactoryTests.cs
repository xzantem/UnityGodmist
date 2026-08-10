using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class GameSessionRestoreFactoryTests
    {
        [Test]
        public void TryCreate_RestoresEmptyValidSession()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            try
            {
                var saveData = GameSaveData.CreateNew("Restore test");
                saveData.Party.MaxActiveMembers = 5;

                var restoreFactory = new GameSessionRestoreFactory();

                var result = restoreFactory.TryCreate(
                    saveData,
                    catalog,
                    out GameSession restoredSession);

                Assert.That(result, Is.True);
                Assert.That(restoredSession, Is.Not.Null);
                Assert.That(restoredSession.Gold, Is.EqualTo(0));
                Assert.That(
                    restoredSession.PlayerParty.MaxActiveMembers,
                    Is.EqualTo(5));
                Assert.That(
                    restoredSession.CurrentMode,
                    Is.EqualTo(
                        Godmist.Core.Enums.GameModeState.Exploration));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}