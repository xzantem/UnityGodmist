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
                saveData.CampaignId = "campaign.restore";

                var restoreFactory = new GameSessionRestoreFactory();

                var result = restoreFactory.TryCreate(
                    saveData,
                    catalog,
                    out GameSession restoredSession);

                Assert.That(result, Is.True);
                Assert.That(restoredSession, Is.Not.Null);
                Assert.That(restoredSession.Gold, Is.EqualTo(0));
                Assert.That(restoredSession.CampaignId, Is.EqualTo("campaign.restore"));
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

        [Test]
        public void TryCreate_RestoresExplorationPosition()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            try
            {
                var saveData = GameSaveData.CreateNew("Pozycja Arungardu");
                saveData.Party.MaxActiveMembers = 5;
                saveData.HasExplorationPosition = true;
                saveData.ExplorationPositionX = 14.5f;
                saveData.ExplorationPositionY = -7.25f;

                var restoreFactory = new GameSessionRestoreFactory();

                var result = restoreFactory.TryCreate(
                    saveData,
                    catalog,
                    out GameSession restoredSession);

                Assert.That(result, Is.True);
                Assert.That(restoredSession.HasExplorationPosition, Is.True);
                Assert.That(
                    restoredSession.ExplorationPosition,
                    Is.EqualTo(new Vector2(14.5f, -7.25f)));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}