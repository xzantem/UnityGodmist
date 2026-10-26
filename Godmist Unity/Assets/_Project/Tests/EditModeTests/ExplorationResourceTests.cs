using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Configuration;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;
using Unity.Profiling;

namespace Godmist.Tests.EditMode
{
    public partial class ExplorationResourceTests
    {
        private static BattleUnit Unit(ResourceType type, int value = 0) => new()
        {
            IsPlayerSide = true,
            Character = new CharacterInstance { LifeState = CharacterLifeState.Alive,
                CurrentHealth = 100, ResourcePool = new ResourcePool(type, value, 200, 20) }
        };
        private static DungeonTrapOccupant Occupant(BattleUnit unit, CombatSession combat = null)
            => new(unit, combat, Vector2Int.zero);

        [Test] public void WaitingDoesNotRestoreManaAndPreservesStatusTickTimeAcrossOwners()
        {
            var unit = Unit(ResourceType.Mana);
            new ExplorationStatusEffectSystem().Advance(5f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            new ExplorationStatusEffectSystem().Advance(14f, new[] { Occupant(unit), Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
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
        [Test] public void MomentumIsCappedAtFiftyIncludingFractionalCarry()
        {
            var unit = Unit(ResourceType.Momentum, 150);
            unit.Character.ResourcePool.FractionalGainRemainder = .5f;
            new ExplorationStatusEffectSystem().Advance(.01f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            Assert.That(unit.Character.ResourcePool.FractionalGainRemainder, Is.Zero);
        }
        [Test] public void ZeroMomentumPreservesFractionalCarryWithoutConfiguredThresholdAllocations()
        {
            var definitions = ScriptableObject.CreateInstance<GameDefines>();
            while (definitions.Entries.Count > 0) definitions.RemoveEntryAt(definitions.Entries.Count - 1);
            definitions.AddEntry(new GameDefineEntry {
                Id = GameDefineIds.MomentumHighThreshold, DisplayName = "Momentum threshold",
                Category = GameDefineCategory.Combat, ValueType = GameDefineValueType.Integer,
                CurrentIntegerValue = 100, DefaultIntegerValue = 100, MaximumIntegerValue = 200 });
            GameDefinesRuntime.Configure(definitions);
            try
            {
                var unit = Unit(ResourceType.Momentum);
                var occupants = new[] { Occupant(unit) };
                var system = new ExplorationStatusEffectSystem();
                for (var i = 0; i < 10; i++) system.Advance(.0001f, occupants);
                // Positive control: this is the redundant validation previously
                // performed twice per zero-momentum occupant on every frame.
                MomentumResourceRule.RefreshSpeedAfterChange(unit.Character, 0);
                long Count(System.Action action)
                {
                    using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                        ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                    if (!recorder.Valid) throw new System.InvalidOperationException("GC.Alloc recorder unavailable");
                    action(); recorder.Stop(); return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
                }
                Assert.That(Count(() => { for (var i = 0; i < 1000; i++) MomentumResourceRule.RefreshSpeedAfterChange(unit.Character, 0); }), Is.GreaterThan(0));
                var allocations = Count(() => {
                    for (var i = 0; i < 1000; i++) {
                        unit.Character.ResourcePool.FractionalGainRemainder = .5f;
                        system.Advance(.0001f, occupants);
                    }
                });
                Assert.That(allocations, Is.Zero);
                Assert.That(unit.Character.ResourcePool.FractionalGainRemainder, Is.EqualTo(.5f));
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.Zero);
            }
            finally { GameDefinesRuntime.Configure(null); Object.DestroyImmediate(definitions); }
        }
        [Test] public void NonzeroExplorationMomentumStillRemovesItsSpeedBonus()
        {
            var unit = Unit(ResourceType.Momentum, 150);
            unit.Character.Stats = new CharacterStats { MaxHealth = new Stat(100), MaxResource = new Stat(200), Speed = new Stat(60) };
            new Godmist.Systems.Characters.CharacterStatPipelineSystem().Refresh(unit.Character);
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(68));
            new ExplorationStatusEffectSystem().Advance(.01f, new[] { Occupant(unit) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            Assert.That(unit.Character.Stats.Speed.GetValue(), Is.EqualTo(60));
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
