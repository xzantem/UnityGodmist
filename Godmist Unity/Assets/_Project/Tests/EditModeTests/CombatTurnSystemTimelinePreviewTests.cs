using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CombatTurnSystemTimelinePreviewTests
    {
        [Test]
        public void GetUpcomingUnitTurns_ProjectsRepeatedTurnsWithoutMutatingSession()
        {
            var fastUnit = CreateUnit(speed: 20f, currentActionValue: 0f);
            var slowUnit = CreateUnit(speed: 10f, currentActionValue: 400f);
            var session = new CombatSession
            {
                TurnParticipants = new List<CombatTurnParticipant>
                {
                    new(fastUnit),
                    new(slowUnit)
                },
                ActiveParticipant = new CombatTurnParticipant(fastUnit),
                ActiveUnit = fastUnit
            };

            var turns = CombatTurnSystem.GetUpcomingUnitTurns(session, 5);

            CollectionAssert.AreEqual(
                new[] { fastUnit, slowUnit, fastUnit, fastUnit, slowUnit },
                turns);
            Assert.That(fastUnit.CurrentActionValue, Is.EqualTo(0f));
            Assert.That(slowUnit.CurrentActionValue, Is.EqualTo(400f));
        }

        private static BattleUnit CreateUnit(float speed, float currentActionValue)
        {
            return new BattleUnit
            {
                CurrentActionValue = currentActionValue,
                Character = new CharacterInstance
                {
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats
                    {
                        Speed = new Stat(speed)
                    }
                }
            };
        }
    }
}
