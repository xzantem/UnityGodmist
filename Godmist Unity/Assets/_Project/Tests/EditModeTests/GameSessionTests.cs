using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class GameSessionTests
    {
        [Test]
        public void Constructor_UsesProvidedInitialMode()
        {
            var session = new GameSession(GameModeState.Exploration);

            Assert.That(session.CurrentMode, Is.EqualTo(GameModeState.Exploration));
        }

        [Test]
        public void TrySetMode_WhenModeChanges_UpdatesStateAndRaisesEvent()
        {
            var session = new GameSession(GameModeState.Menu);

            var eventCount = 0;
            var previousMode = GameModeState.Menu;
            var nextMode = GameModeState.Menu;

            session.ModeChanged += (previous, next) =>
            {
                eventCount++;
                previousMode = previous;
                nextMode = next;
            };

            var result = session.TrySetMode(GameModeState.Exploration);

            Assert.That(result, Is.True);
            Assert.That(session.CurrentMode, Is.EqualTo(GameModeState.Exploration));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(previousMode, Is.EqualTo(GameModeState.Menu));
            Assert.That(nextMode, Is.EqualTo(GameModeState.Exploration));
        }

        [Test]
        public void TryAddCharacterToActiveParty_WhenCapacityIsReached_RejectsNextMember()
        {
            var session = new GameSession();
            var party = new Party(maxActiveMembers: 2);

            Assert.That(party.TryAddActiveMember("jerzy"), Is.True);
            Assert.That(party.TryAddActiveMember("maela"), Is.True);
            Assert.That(party.TryAddActiveMember("wladimir"), Is.False);

            Assert.That(session.PlayerParty.MaxActiveMembers, Is.EqualTo(5));
        }

        [Test]
        public void TrySetActiveLeader_WhenCharacterIsActiveAndAvailable_ChangesLeader()
        {
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var maela = CreateCharacter("maela");

            session.TryRegisterCharacter(jerzy);
            session.TryRegisterCharacter(maela);
            session.TryAddCharacterToActiveParty("jerzy");
            session.TryAddCharacterToActiveParty("maela");

            var result = session.TrySetActiveLeader("maela");

            Assert.That(result, Is.True);
            Assert.That(session.PlayerParty.ActiveLeaderId, Is.EqualTo("maela"));
        }

        [Test]
        public void TrySetActiveLeader_WhenCharacterIsUnavailable_RejectsLeaderChange()
        {
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var unavailableCompanion = CreateCharacter(
                "maela",
                availabilityState: CharacterAvailabilityState.StoryLocked);

            session.TryRegisterCharacter(jerzy);
            session.TryRegisterCharacter(unavailableCompanion);
            session.TryAddCharacterToActiveParty("jerzy");
            session.PlayerParty.TryAddActiveMember("maela");

            var result = session.TrySetActiveLeader("maela");

            Assert.That(result, Is.False);
            Assert.That(session.PlayerParty.ActiveLeaderId, Is.EqualTo("jerzy"));
        }

        [Test]
        public void TryMoveActiveMemberToBench_WhenLeaderMoves_AssignsNextAvailableLeader()
        {
            var session = new GameSession();
            var jerzy = CreateCharacter("jerzy");
            var maela = CreateCharacter("maela");

            session.TryRegisterCharacter(jerzy);
            session.TryRegisterCharacter(maela);
            session.TryAddCharacterToActiveParty("jerzy");
            session.TryAddCharacterToActiveParty("maela");

            var result = session.TryMoveActiveMemberToBench("jerzy");

            Assert.That(result, Is.True);
            Assert.That(session.PlayerParty.ActiveLeaderId, Is.EqualTo("maela"));
            CollectionAssert.DoesNotContain(
                session.PlayerParty.ActiveMemberIds,
                "jerzy");
            CollectionAssert.Contains(
                session.PlayerParty.BenchMemberIds,
                "jerzy");
        }

        private static CharacterInstance CreateCharacter(
            string characterId,
            CharacterAvailabilityState availabilityState
                = CharacterAvailabilityState.Available)
        {
            return new CharacterInstance
            {
                CharacterId = characterId,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = availabilityState
            };
        }
    }
}