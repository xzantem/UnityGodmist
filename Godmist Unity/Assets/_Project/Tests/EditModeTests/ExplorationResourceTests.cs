using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ExplorationResourceTests
    {
        private static BattleUnit Unit(ResourceType type, int value = 0) => new()
        {
            IsPlayerSide = true,
            Character = new CharacterInstance { LifeState = CharacterLifeState.Alive,
                CurrentHealth = 100, ResourcePool = new ResourcePool(type, value, 200, 20) }
        };
        private static DungeonTrapOccupant Occupant(BattleUnit unit, CombatSession combat = null)
            => new(unit, combat, Vector2Int.zero);

        [Test] public void ManaTicksWithoutStatusesAndPreservesPartialTimeAcrossOwners()
        {
            var unit = Unit(ResourceType.Mana);
            new ExplorationStatusEffectSystem().Advance(5f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            new ExplorationStatusEffectSystem().Advance(14f, new[] { Occupant(unit), Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(60));
            Assert.That(unit.Character.ExplorationStatusElapsedSeconds, Is.EqualTo(1f));
        }
        [Test] public void RageDecaysOnFirstTickEvenIfLastCombatAttackGainedRage()
        {
            var unit = Unit(ResourceType.Rage, 25);
            unit.Character.ResourcePool.HasGainedResourceSinceLastTurn = true;
            var system = new ExplorationStatusEffectSystem();
            system.Advance(6f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(15));
            system.Advance(12f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
        }
        [Test] public void MomentumIsZeroImmediatelyIncludingFractionalCarry()
        {
            var unit = Unit(ResourceType.Momentum, 150);
            unit.Character.ResourcePool.FractionalGainRemainder = .5f;
            new ExplorationStatusEffectSystem().Advance(.01f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(unit.Character.ResourcePool.FractionalGainRemainder, Is.Zero);
        }
        [Test] public void CombatOccupantsAndDeadCharactersAreNotTicked()
        {
            var alive = Unit(ResourceType.Mana);
            var dead = Unit(ResourceType.Mana);
            dead.Character.LifeState = CharacterLifeState.Dead;
            new ExplorationStatusEffectSystem().Advance(60f, new[] { Occupant(alive, new CombatSession()), Occupant(dead) });
            Assert.That(alive.Character.ResourcePool.CurrentValue, Is.Zero);
            Assert.That(dead.Character.ResourcePool.CurrentValue, Is.Zero);
        }
        [Test] public void ResourceRegenerationBlockStillBlocksNaturalManaRecovery()
        {
            var unit = Unit(ResourceType.Mana);
            unit.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { BlocksResourceRegeneration = true });
            new ExplorationStatusEffectSystem().Advance(6f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
        }
    }
}
