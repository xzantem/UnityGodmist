using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class MageShieldLifecycleTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + path);
        static SkillData Shield => Load<SkillData>("Skills/SD_MageShield.asset");
        static BattleUnit Unit(int x = 1)
        {
            var character = new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_Mage.asset"),
                Load<CharacterClassData>("Classes/CCD_Mage.asset"));
            character.CharacterId = Guid.NewGuid().ToString();
            character.Stats.MinAttack = new Stat(100); character.Stats.MaxAttack = new Stat(100);
            character.Stats.MaxHealth = new Stat(1000); character.CurrentHealth = 500;
            character.Resistances = new ResistanceSet(); character.BaseResistances = new ResistanceSet();
            character.ResourcePool.MaxValue = character.ResourcePool.CurrentValue = 100;
            return new BattleUnit { Character = character, IsPlayerSide = true, Position = new GridPosition(x, 2),
                MaxActionPoints = 10000, CurrentActionPoints = 10000 };
        }
        static CombatSession Session(params BattleUnit[] units) => new() { Grid = new BattleGrid(10, 8), Units = units.ToList(), ActiveUnit = units[0] };
        static bool Cast(CombatSession session, BattleUnit caster, BattleUnit target)
        {
            caster.CurrentActionPoints = caster.MaxActionPoints;
            caster.Character.ResourcePool.CurrentValue = caster.Character.ResourcePool.MaxValue;
            return new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, caster, Shield, target);
        }
        static ShieldLayer MagicLayer(BattleUnit unit) => unit.Character.ShieldPool.Layers.Single(l => l.SourceId == CombatShieldSystem.MageShieldSourceId);

        [Test]
        public void ProductionShieldHasApprovedStrengthDurationAndUnchangedCosts()
        {
            Assert.That(Shield.Effects.Single().ShieldAverageAttackMultiplier, Is.EqualTo(2.5f));
            Assert.That(Shield.Effects.Single().DurationTurns, Is.EqualTo(3));
            Assert.That(Shield.ActionCost, Is.EqualTo(90)); Assert.That(Shield.ResourceCost, Is.EqualTo(40));
            var caster = Unit(); caster.Character.Stats.MinAttack = new Stat(80); caster.Character.Stats.MaxAttack = new Stat(120);
            var target = Unit(2); Assert.That(Cast(Session(caster, target), caster, target), Is.True);
            Assert.That(target.Character.ShieldPool.TotalShield, Is.EqualTo(250));
            Assert.That(MagicLayer(target).RemainingTurns, Is.EqualTo(3));
        }

        [Test]
        public void ShieldExpiresOnlyAfterThreeRecipientTurnEndsAndPublishesRemainingLoss()
        {
            var caster = Unit(); var target = Unit(2); var session = Session(caster, target);
            Assert.That(Cast(session, caster, target), Is.True);
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            var statuses = new StatusEffectSystem(feedbackBus: bus);
            for (var i = 0; i < 4; i++) statuses.ProcessTurnEnd(caster);
            Assert.That(MagicLayer(target).RemainingTurns, Is.EqualTo(3));
            target.Character.ShieldPool.AbsorbDamage(50);
            statuses.ProcessTurnEnd(target); Assert.That(MagicLayer(target).RemainingTurns, Is.EqualTo(2));
            statuses.ProcessTurnEnd(target); Assert.That(target.Character.ShieldPool.TotalShield, Is.EqualTo(200));
            statuses.ProcessTurnEnd(target); Assert.That(target.Character.ShieldPool.Layers, Is.Empty);
            var expired = events.Where(e => e.Type == CombatFeedbackType.ShieldLoss).ToArray();
            Assert.That(expired.Length, Is.EqualTo(1)); Assert.That(expired[0].Amount, Is.EqualTo(200));
            Assert.That(expired[0].Source, Is.SameAs(target), "Expiry must not trigger an incoming-hit pose.");
            statuses.ProcessTurnEnd(target); Assert.That(events.Count(e => e.Type == CombatFeedbackType.ShieldLoss), Is.EqualTo(1));
        }

        [Test]
        public void RecastReplacesRemainingValueAndRenewsDurationWithoutTouchingOtherSources()
        {
            var caster = Unit(); var target = Unit(2); var session = Session(caster, target);
            target.Character.ShieldPool.AddLayer(70, "equipment.test");
            Assert.That(Cast(session, caster, target), Is.True);
            target.Character.ShieldPool.AbsorbDamage(100); new StatusEffectSystem().ProcessTurnEnd(target);
            Assert.That(Cast(session, caster, target), Is.True);
            Assert.That(MagicLayer(target).CurrentValue, Is.EqualTo(250)); Assert.That(MagicLayer(target).RemainingTurns, Is.EqualTo(3));
            Assert.That(target.Character.ShieldPool.TotalShield, Is.EqualTo(320)); Assert.That(target.Character.ShieldPool.Layers.Count, Is.EqualTo(2));
            for (var i = 0; i < 3; i++) new StatusEffectSystem().ProcessTurnEnd(target);
            Assert.That(target.Character.ShieldPool.TotalShield, Is.EqualTo(70));
            Assert.That(target.Character.ShieldPool.Layers.Single().RemainingTurns, Is.Zero);
        }

        [Test]
        public void AnotherCasterAlsoReplacesTheSameNamedShield()
        {
            var first = Unit(); var second = Unit(2); var target = Unit(3); var session = Session(first, second, target);
            Assert.That(Cast(session, first, target), Is.True);
            second.Character.Stats.MinAttack = new Stat(40); second.Character.Stats.MaxAttack = new Stat(40);
            session.ActiveUnit = second;
            Assert.That(Cast(session, second, target), Is.True);
            Assert.That(target.Character.ShieldPool.Layers.Count, Is.EqualTo(1));
            Assert.That(MagicLayer(target).CurrentValue, Is.EqualTo(100)); Assert.That(MagicLayer(target).RemainingTurns, Is.EqualTo(3));
        }

        [Test]
        public void AbsorbedShieldDoesNotLeaveATimerOrLaterExpirationFeedback()
        {
            var unit = Unit(); Assert.That(Cast(Session(unit), unit, unit), Is.True);
            Assert.That(unit.Character.ShieldPool.AbsorbDamage(300), Is.EqualTo(50));
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            new StatusEffectSystem(feedbackBus: bus).ProcessTurnEnd(unit);
            Assert.That(unit.Character.ShieldPool.Layers, Is.Empty);
            Assert.That(events.Any(e => e.Type == CombatFeedbackType.ShieldLoss), Is.False);
        }

        [TestCase("stun")]
        [TestCase("freeze")]
        [TestCase("paralysis")]
        public void OwnSkippedTurnStillCountsTowardShieldDuration(string status)
        {
            var unit = Unit(); Assert.That(Cast(Session(unit), unit, unit), Is.True);
            var statuses = new StatusEffectSystem();
            Assert.That(statuses.ApplyStatus(unit, unit, status, 1f, 3, 1, 0f, 0f, "test"), Is.True);
            statuses.ProcessTurnEnd(unit);
            Assert.That(MagicLayer(unit).RemainingTurns, Is.EqualTo(2));
        }

        [Test]
        public void ExplorationUsesExistingSixSecondTicksWithoutDoubleCountingSharedOccupants()
        {
            var unit = Unit(); Assert.That(Cast(Session(unit), unit, unit), Is.True);
            var occupants = new[] { new DungeonTrapOccupant(unit, null, Vector2Int.zero), new DungeonTrapOccupant(unit, null, Vector2Int.one) };
            var system = new ExplorationStatusEffectSystem();
            system.Advance(5f, occupants); Assert.That(MagicLayer(unit).RemainingTurns, Is.EqualTo(3));
            system.Advance(1f, occupants); Assert.That(MagicLayer(unit).RemainingTurns, Is.EqualTo(2));
            system.Advance(12f, occupants); Assert.That(unit.Character.ShieldPool.Layers, Is.Empty);
        }

        [Test]
        public void ExplorationDoesNotAdvanceShieldWhileUnitBelongsToCombat()
        {
            var unit = Unit(); var session = Session(unit); Assert.That(Cast(session, unit, unit), Is.True);
            new ExplorationStatusEffectSystem().Advance(30f, new[] { new DungeonTrapOccupant(unit, session, Vector2Int.zero) });
            Assert.That(MagicLayer(unit).RemainingTurns, Is.EqualTo(3));
        }

        [Test]
        public void RejoiningCombatDoesNotRefreshOrClearShieldLifetime()
        {
            var unit = Unit(); Assert.That(Cast(Session(unit), unit, unit), Is.True);
            new StatusEffectSystem().ProcessTurnEnd(unit);
            ScoutKnifeSystem.ResetForCombat(Session(unit));
            Assert.That(MagicLayer(unit).RemainingTurns, Is.EqualTo(2)); Assert.That(MagicLayer(unit).CurrentValue, Is.EqualTo(250));
        }

        [Test]
        public void ShieldTooltipExplainsDurationRecipientAndReplacement()
        {
            var node = new GameObject("Shield tooltip test");
            try
            {
                var body = Regex.Replace(node.AddComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(Shield, null), "<[^>]*>", "");
                Assert.That(body, Does.Contain("250%").And.Contain("3 tury odbiorcy").And.Contain("zastępuje poprzednią Magiczną Tarczę"));
                Assert.That(body, Does.Not.Contain("400%"));
            }
            finally { Object.DestroyImmediate(node); }
        }

        static GameSaveData Snapshot(BattleUnit unit)
        {
            unit.Character.CharacterId = unit.Character.Definition.Id;
            var game = new GameSession(GameModeState.Exploration, "campaign.shield.test");
            Assert.That(game.TryRegisterCharacter(unit.Character), Is.True);
            Assert.That(game.TryAddCharacterToActiveParty(unit.Character.CharacterId), Is.True);
            return new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Shield test");
        }
        static BattleUnit Restore(GameSaveData save)
        {
            var catalog = Load<GameContentCatalog>("World/GC_MainContentCatalog.asset");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, catalog, out var game), Is.True);
            return new BattleUnit { Character = game.CharactersById[save.Characters.Single().CharacterId] };
        }

        [Test]
        public void JsonSaveRoundTripPreservesRemainingValueAndDurationWithoutReset()
        {
            var unit = Unit(); unit.Character.ShieldPool.AddLayer(40, "equipment.test");
            Assert.That(Cast(Session(unit), unit, unit), Is.True);
            unit.Character.ShieldPool.AbsorbDamage(70); new StatusEffectSystem().ProcessTurnEnd(unit);
            var save = Snapshot(unit);
            Assert.That(save.Characters.Single().ShieldLayers.Single(l => l.SourceId == CombatShieldSystem.MageShieldSourceId).RemainingTurns, Is.EqualTo(2));
            var restored = Restore(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)));
            Assert.That(MagicLayer(restored).CurrentValue, Is.EqualTo(180)); Assert.That(MagicLayer(restored).RemainingTurns, Is.EqualTo(2));
            new StatusEffectSystem().ProcessTurnEnd(restored); Assert.That(MagicLayer(restored).RemainingTurns, Is.EqualTo(1));
            new StatusEffectSystem().ProcessTurnEnd(restored); Assert.That(restored.Character.ShieldPool.TotalShield, Is.EqualTo(40));
        }

        [Test]
        public void LegacyJsonWithoutDurationKeepsOnlyLastMagicCastForThreeTurns()
        {
            var save = Snapshot(Unit());
            save.Characters.Single().ShieldLayers = new() {
                new() { SourceId = CombatShieldSystem.MageShieldSourceId, CurrentValue = 400 },
                new() { SourceId = "equipment.test", CurrentValue = 30 },
                new() { SourceId = CombatShieldSystem.MageShieldSourceId, CurrentValue = 120 }
            };
            var json = Regex.Replace(JsonUtility.ToJson(save), ",\\s*\"RemainingTurns\"\\s*:\\s*-?\\d+", "");
            Assert.That(json, Does.Not.Contain("RemainingTurns"));
            var restored = Restore(JsonUtility.FromJson<GameSaveData>(json));
            Assert.That(restored.Character.ShieldPool.TotalShield, Is.EqualTo(150));
            Assert.That(MagicLayer(restored).CurrentValue, Is.EqualTo(120)); Assert.That(MagicLayer(restored).RemainingTurns, Is.EqualTo(3));
            for (var i = 0; i < 3; i++) new StatusEffectSystem().ProcessTurnEnd(restored);
            Assert.That(restored.Character.ShieldPool.TotalShield, Is.EqualTo(30));
        }

        [Test]
        public void SaveWithNegativeShieldDurationIsRejected()
        {
            var save = Snapshot(Unit());
            save.Characters.Single().ShieldLayers.Add(new() { SourceId = "test", CurrentValue = 20, RemainingTurns = -1 });
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Odtwarzanie GameSession odrzucone przy odtwarzaniu postaci.");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"), out _), Is.False);
        }
    }
}
