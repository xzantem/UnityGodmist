using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void GetUpcomingTurns_IncludesRuneAndStopsPreviewAfterItsThirdTurn()
        {
            var definition = ScriptableObject.CreateInstance<SummonedObjectDefinition>();
            try
            {
                typeof(SummonedObjectDefinition).GetField("defaultDurationTurns",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(definition, 3);
                typeof(SummonedObjectDefinition).GetField("turnSpeed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(definition, 80);
                var rune = new SummonedCombatMapObject("rune", new GridPosition(1, 1),
                    "boss", CatacombsBossSystem.VengeanceSkillId, 0, definition);
                var hero = CreateUnit(10f, 10000f);
                var session = new CombatSession
                {
                    TurnParticipants = new List<CombatTurnParticipant> { new(rune), new(hero) }
                };

                var turns = CombatTurnSystem.GetUpcomingTurns(session, 8);

                Assert.That(turns.Count(p => p.SummonedObject == rune), Is.EqualTo(3));
                Assert.That(rune.RemainingDurationTurns, Is.EqualTo(3));
                Assert.That(rune.CurrentActionValue, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
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
