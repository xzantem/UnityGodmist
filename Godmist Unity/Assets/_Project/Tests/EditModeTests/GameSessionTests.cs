using Godmist.Core.Enums;
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
        public void TrySetMode_WhenModeIsUnchanged_DoesNotRaiseEvent()
        {
            var session = new GameSession(GameModeState.Combat);
            var eventCount = 0;

            session.ModeChanged += (_, _) => eventCount++;

            var result = session.TrySetMode(GameModeState.Combat);

            Assert.That(result, Is.False);
            Assert.That(eventCount, Is.EqualTo(0));
        }
    }
}