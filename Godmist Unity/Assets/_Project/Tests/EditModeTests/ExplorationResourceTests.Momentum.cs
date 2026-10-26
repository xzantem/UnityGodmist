using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class ExplorationResourceTests
    {
        [TestCase(1, 2)] [TestCase(2, 6)] [TestCase(3, 12)] [TestCase(6, 42)] [TestCase(7, 50)]
        public void ExplorationMovementEscalatesAndStopsAtFifty(int cells, int expected)
        {
            var character = Unit(ResourceType.Momentum).Character;
            ExplorationStatusEffectSystem.ProcessMovement(character, cells);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(expected));
        }

        [Test] public void SubCellMovementIsIndependentOfFrameSegmentation()
        {
            var character = Unit(ResourceType.Momentum).Character;
            for (int i = 0; i < 300; i++) ExplorationStatusEffectSystem.ProcessMovement(character, .01f);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(12));
            Assert.That(character.ExplorationMomentumSteps, Is.EqualTo(3));
            Assert.That(character.ExplorationMomentumDistanceRemainder, Is.EqualTo(0f).Within(.0001f));
        }

        [Test] public void MovingResetsIdleClockButNotTheSeriesUntilSixSecondsOfRest()
        {
            var unit = Unit(ResourceType.Momentum);
            var system = new ExplorationStatusEffectSystem();
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 3);
            system.Advance(5.9f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(12));
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 1);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
            system.Advance(5.9f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
            system.Advance(.11f, new[] { Occupant(unit), Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 1);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(12));
        }

        [Test] public void ProlongedRestLosesTenPerSixSecondsAcrossStatusOwners()
        {
            var unit = Unit(ResourceType.Momentum, 50);
            new ExplorationStatusEffectSystem().Advance(5f, new[] { Occupant(unit) });
            new ExplorationStatusEffectSystem().Advance(14f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
            Assert.That(unit.Character.ExplorationMomentumIdleSeconds, Is.EqualTo(1f));
        }

        [Test] public void MovementAtCapStillPreventsIdleDecay()
        {
            var unit = Unit(ResourceType.Momentum, 50);
            var system = new ExplorationStatusEffectSystem();
            system.Advance(5f, new[] { Occupant(unit) });
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, .1f);
            system.Advance(5f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidMovementDoesNotGenerateOrResetIdle(float distance)
        {
            var unit = Unit(ResourceType.Momentum, 20);
            unit.Character.ExplorationMomentumIdleSeconds = 5;
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, distance);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
            Assert.That(unit.Character.ExplorationMomentumIdleSeconds, Is.EqualTo(5));
        }

        [Test] public void BlockingRecoveryAndDowningPreventGainsButNotIdleLoss()
        {
            var unit = Unit(ResourceType.Momentum, 20);
            unit.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { BlocksResourceRegeneration = true });
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 2);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
            new ExplorationStatusEffectSystem().Advance(6f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
            unit.Character.LifeState = CharacterLifeState.Downed;
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 3);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
        }

        [TestCase(ResourceType.Mana)] [TestCase(ResourceType.Rage)]
        public void WalkingDoesNotGenerateOtherResourceTypes(ResourceType type)
        {
            var unit = Unit(type, 20);
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 5);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
        }

        [TestCase(0)] [TestCase(50)] [TestCase(1000)]
        public void CombatMovementCountsCellsEvenWhenFreeOrDiscounted(int apCost)
        {
            var unit = Unit(ResourceType.Momentum, 48);
            var system = new ResourceRuleSystem();
            for (int i = 1; i <= 3; i++)
            {
                unit.MovementStepsThisTurn = i;
                system.ProcessUnitMoved(unit, apCost);
            }
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(60));
            system.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(60));
            unit.MovementStepsThisTurn = 0;
            system.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            unit.MovementStepsThisTurn = 1;
            system.ProcessUnitMoved(unit, apCost);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(52));
        }

        [Test] public void CombatBatchedPathAndIndividualStepsHaveTheSameGainAcrossThreshold()
        {
            var single = Unit(ResourceType.Momentum, 95);
            var batch = Unit(ResourceType.Momentum, 95);
            var system = new ResourceRuleSystem();
            for (int i = 1; i <= 3; i++) { single.MovementStepsThisTurn = i; system.ProcessUnitMoved(single, 0); }
            batch.MovementStepsThisTurn = 3;
            system.ProcessUnitMoved(batch, 0, 3);
            Assert.That(single.Character.ResourcePool.CurrentValue, Is.EqualTo(103));
            Assert.That(batch.Character.ResourcePool.CurrentValue, Is.EqualTo(single.Character.ResourcePool.CurrentValue));
            Assert.That(batch.Character.ResourcePool.FractionalGainRemainder, Is.EqualTo(single.Character.ResourcePool.FractionalGainRemainder));
        }

        [Test] public void IdleCombatTurnRefreshesMomentumSpeedAndCannotDrainBelowZero()
        {
            var unit = Unit(ResourceType.Momentum, 130);
            unit.Character.Stats = new Godmist.Runtime.Characters.CharacterStats {
                MaxHealth = new Stat(100), MaxResource = new Stat(200), Speed = new Stat(60) };
            new Godmist.Systems.Characters.CharacterStatPipelineSystem().Refresh(unit.Character);
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(64));
            var system = new ResourceRuleSystem();
            system.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(120));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(60));
            unit.Character.ResourcePool.CurrentValue = 3;
            system.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
        }

        [Test] public void ViewCountsMovementButNotTeleportOrCombatPresentation()
        {
            var go = new GameObject("momentum movement integration");
            try
            {
                var view = go.AddComponent<ExplorationCharacterView>();
                var unit = Unit(ResourceType.Momentum);
                view.Bind(unit.Character);
                view.SetPosition(new Vector2(10000, 10000));
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
                view.Move(Vector2.right);
                view.Move(Vector2.up);
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(6));
                view.BindCombat(unit);
                view.Move(Vector2.right * 3);
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(6));
                unit.Character.ResourcePool.CurrentValue = 120;
                view.ReleaseCombat();
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void PlayerKeepsEarnedMomentumWhenCombatStarts()
        {
            var unit = Unit(ResourceType.Momentum);
            ExplorationStatusEffectSystem.ProcessMovement(unit.Character, 7);
            var session = new CombatSession(); session.Units.Add(unit);
            ScoutKnifeSystem.ResetForCombat(session);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [Test] public void BlockedExplorationInputCannotGenerateMomentumOrPreventIdleDecay()
        {
            var go = new GameObject("blocked momentum actor");
            var wall = new GameObject("momentum test wall");
            try
            {
                var view = go.AddComponent<ExplorationCharacterView>();
                var unit = Unit(ResourceType.Momentum, 20); view.Bind(unit.Character);
                view.SetPosition(new Vector2(11000, 11000));
                wall.transform.position = new Vector2(11000.6f, 11000);
                wall.AddComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
                view.Move(Vector2.right);
                Assert.That(view.Position, Is.EqualTo(new Vector2(11000, 11000)));
                new ExplorationStatusEffectSystem().Advance(6f, new[] { Occupant(unit) });
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(wall); }
        }

        [TestCase(false)] [TestCase(true)]
        public void RealCombatPathCreditsCommittedCellsIncludingFreeMoves(bool free)
        {
            var unit = Unit(ResourceType.Momentum);
            unit.Character.Stats = new Godmist.Runtime.Characters.CharacterStats();
            unit.CurrentActionPoints = unit.MaxActionPoints = 1000;
            unit.WarriorState.FreeMovementTurns = free ? 1 : 0;
            unit.Position = new GridPosition(0, 0);
            var session = new CombatSession { Grid = new BattleGrid(5, 2) };
            session.Units.Add(unit);
            var movement = new CombatMovementSystem(resourceRuleSystem: new ResourceRuleSystem());
            Assert.That(movement.TryMoveAlongPath(session, unit,
                new[] { new GridPosition(1, 0), new GridPosition(2, 0) }, out var completed), Is.True);
            Assert.That(completed.Count, Is.EqualTo(2));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(6));
            Assert.That(movement.TryMoveOneStep(session, unit, 1, 0), Is.True);
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(12));
        }

        [Test] public void ExplorationRespectsResourceMaximumBelowFifty()
        {
            var character = Unit(ResourceType.Momentum).Character;
            character.ResourcePool.MaxValue = 10;
            ExplorationStatusEffectSystem.ProcessMovement(character, 10);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(10));
        }
    }
}
