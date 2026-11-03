using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatSaveTests
    {
        internal static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        internal static GameSession Campaign(string theme="forest")
        {
            var profile=AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>("Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile,out var game,out var error),Is.True,error);
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+theme+".expedition");
            var dungeon=new DungeonGeneratorService().Generate(definition,231023,1,DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            new DungeonObjectPlacementSystem().Place(dungeon);
            new DungeonEncounterRewardPreparationSystem().Prepare(dungeon,Catalog.EquipmentPartDefinitions);
            Assert.That(game.TrySetActiveDungeon(dungeon),Is.True);
            return game;
        }
        internal static CombatSession Battle(GameSession game)
        {
            Assert.That(game.ActiveDungeon.TryGetCurrentFloor(out var floor),Is.True);
            var encounter=floor.Encounters.Skip(game.Combats.Count).First();
            Assert.That(new DungeonCombatContextFactory().TryCreate(game.ActiveDungeon,encounter,out var context,true),Is.True);
            var cells=context.WorldCells.ToArray();
            var playerId=game.PlayerParty.ActiveMemberIds.First(id=>game.GetCombatForCharacter(id)==null);
            var players=new Dictionary<string,Vector2Int> { [playerId]=cells[0] };
            var enemies=new Dictionary<string,Vector2Int> { [encounter.Enemies[0].InstanceId]=cells[1] };
            Assert.That(new CombatSessionFactory().TryCreateSeamless(game,encounter,context,players,enemies,out var combat,out _,true),Is.True);
            Assert.That(game.TryRegisterDungeonCombat(combat),Is.True);
            game.TryFocusCombat(combat);
            var actor=combat.Units.First(u=>u.IsPlayerControlled);
            new CombatTurnSystem().InitializeTurnOrder(combat,actor);
            Assert.That(combat.ActiveUnit,Is.SameAs(actor));
            actor.CurrentActionPoints-=100;
            return combat;
        }
        internal static GameSaveData Snapshot(GameSession game) => new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"Combat test");
        internal static GameSession Roundtrip(GameSession game)
        {
            var serializer=new GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(Snapshot(game)),out var data),Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(data,Catalog,out var restored,out var failure),Is.True,failure?.ToString());
            return restored;
        }
        [TestCase("forest"),TestCase("catacombs")]
        public void SaveRestoresSpentApExactTurnAndReferencesWithoutStartingTurn(string theme)
        {
            var game=Campaign(theme);var combat=Battle(game);var actor=combat.ActiveUnit;var enemy=combat.Units.Single(u=>!u.IsPlayerSide);
            enemy.CurrentActionValue=123.125f;actor.Character.CurrentHealth-=13;
            actor.NextTurnActionPointModifier=-100;actor.PaladinState.DamageThisTurn=37;
            enemy.ScoutState.Knives.Add(new() {Owner=actor,BleedMultiplier=.45f});
            enemy.ScoutState.AngerBySource.Add(actor,3);actor.WarriorState.OrderSource=enemy;
            actor.WarriorState.OrderTurns=2;actor.MageState.Adaptation.Add(enemy.Character.CharacterId,new() {RemainingHits=2,Reduction=15});
            actor.ActiveToggleSkillIds.Add("test.toggle");actor.ToggleSkillIdsUsedThisTurn.Add("test.toggle");
            actor.ConsumableUseTypesUsedThisTurn.Add(ConsumableUseType.Potion);
            actor.ResourceRegenerationBlockedTurns=2;enemy.AddStatusTolerance("status.bleed");
            combat.MapObjects.Add(new KnifeCombatMapObject("knife.test",actor.Position,actor.Character.CharacterId,2));
            var restored=Roundtrip(game);var actual=restored.ActiveCombat;
            Assert.That(actual.CombatId,Is.EqualTo(combat.CombatId));
            Assert.That(actual.ActiveUnit.Character,Is.SameAs(restored.CharactersById[actor.Character.CharacterId]));
            Assert.That(actual.ActiveUnit.CurrentActionPoints,Is.EqualTo(actor.CurrentActionPoints));
            Assert.That(actual.ActiveUnit.Character.CurrentHealth,Is.EqualTo(actor.Character.CurrentHealth));
            Assert.That(actual.ActiveUnit.Character.ResourcePool.CurrentValue,Is.EqualTo(actor.Character.ResourcePool.CurrentValue));
            Assert.That(actual.ActiveUnit.Position,Is.EqualTo(actor.Position));
            var target=actual.Units.Single(u=>!u.IsPlayerSide);
            Assert.That(target.ScoutState.Knives.Single().Owner,Is.SameAs(actual.ActiveUnit));
            Assert.That(target.ScoutState.AngerBySource[actual.ActiveUnit],Is.EqualTo(3));
            Assert.That(actual.ActiveUnit.WarriorState.OrderSource,Is.SameAs(target));
            Assert.That(actual.ActiveUnit.MageState.Adaptation[target.Character.CharacterId].Reduction,Is.EqualTo(15));
            Assert.That(actual.MapObjects.OfType<KnifeCombatMapObject>().Single().KnifeCount,Is.EqualTo(2));
            Assert.That(actual.Units.Select(u=>u.CurrentActionValue),Is.EqualTo(combat.Units.Select(u=>u.CurrentActionValue)));
            Assert.That(JsonUtility.ToJson(Snapshot(restored).Combats[0]),Is.EqualTo(JsonUtility.ToJson(Snapshot(game).Combats[0])),"A second snapshot must retain the full logical state.");
        }
        [TestCase("animation"),TestCase("ai"),TestCase("membership"),TestCase("resolution")]
        public void UnsafeBoundaryCannotOverwriteExistingQuickSave(string block)
        {
            var game=Campaign();var combat=Battle(game);var folder=Path.Combine(Path.GetTempPath(),"godmist-combat-save-"+Guid.NewGuid().ToString("N"));
            try
            {
                var manager=new SaveManager(folder);Assert.That(manager.TrySaveQuick(game,out var error),Is.True,error);
                var file=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Single();var original=File.ReadAllBytes(file);
                if(block=="animation")combat.IsExecutionBusy=()=>true;
                if(block=="ai")combat.ActiveUnit=combat.Units.Single(u=>!u.IsPlayerSide);
                if(block=="membership")combat.MembershipChangePending=true;
                if(block=="resolution")combat.SkillResolutionDepth=1;
                Assert.That(manager.TrySaveQuick(game,out error),Is.False);Assert.That(error,Is.Not.Empty);
                Assert.That(File.ReadAllBytes(file),Is.EqualTo(original));
            }
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [TestCase("unit"),TestCase("turn"),TestCase("version"),TestCase("floor")]
        public void CorruptCombatIsRejectedWithCombatDiagnostic(string field)
        {
            var game=Campaign();Battle(game);var data=Snapshot(game);
            if(field=="unit")data.Combats[0].ActiveUnitIds[0]="missing";
            if(field=="turn")data.Combats[0].ActiveTurnIndex=999;
            if(field=="version")data.Combats[0].Version=999;
            if(field=="floor")data.Combats[0].FloorIndex=999;
            Assert.That(new GameSessionRestoreFactory().TryCreate(data,Catalog,out var restored,out var failure),Is.False);
            Assert.That(restored,Is.Null);Assert.That(failure.Stage,Is.EqualTo("Combat"));Assert.That(failure.FieldPath,Does.StartWith("$.Combats"));
        }
        [Test]
        public void VersionOneExplorationSaveRemainsReadable()
        {
            var game=Campaign();var data=Snapshot(game);data.SaveVersion=1;data.Combats=null;
            Assert.That(new GameSessionRestoreFactory().TryCreate(data,Catalog,out var restored,out var error),Is.True,error?.ToString());
            Assert.That(restored.HasCombat,Is.False);Assert.That(Snapshot(restored).SaveVersion,Is.EqualTo(2));
        }

        [Test]
        public void ParallelCombatsKeepFocusAndSharedDepartedParticipantIdentity()
        {
            var game=Campaign();var first=Battle(game);var departed=first.ActiveUnit;
            var second=Battle(game);
            // Retained turn state may be referenced by the old encounter after joining another one.
            first.DepartedUnits.Add(second.ActiveUnit.Character.CharacterId,second.ActiveUnit);
            Assert.That(game.Combats.Count,Is.EqualTo(2));var restored=Roundtrip(game);
            Assert.That(restored.Combats.Count,Is.EqualTo(2));Assert.That(restored.ActiveCombat.CombatId,Is.EqualTo(second.CombatId));
            Assert.That(restored.Combats[0].DepartedUnits[second.ActiveUnit.Character.CharacterId],Is.SameAs(restored.Combats[1].ActiveUnit));
            restored.Combats[0].ActiveUnit=restored.Combats[0].Units.Single(u=>!u.IsPlayerSide);
            Assert.That(CombatPersistence.CanSave(restored,out _),Is.False,"Every running combat must be at a stable player boundary.");
        }

        [TestCase("ent"),TestCase("king"),TestCase("slayer"),TestCase("bandit")]
        public void BossPhaseCountersAndNativeMarkersSurviveJson(string type)
        {
            var game=Campaign();var combat=Battle(game);var enemy=combat.Units.Single(u=>!u.IsPlayerSide);var id=enemy.Character.CharacterId;
            var definitionId=type switch {"ent"=>ForestEntBossSystem.BossId,"king"=>CatacombsBossSystem.BossId,
                "slayer"=>SkeletonSlayerBossSystem.BossId,_=>BanditLeaderBossSystem.BossId};
            var authored=Catalog.EncounterDefinitions.Where(e=>e!=null).SelectMany(e=>e.Enemies)
                .First(e=>e?.CharacterDefinition?.Id==definitionId);
            var definition=authored.CharacterDefinition;
            enemy.Character=new CharacterInstanceFactory().Create(definition.SecondPhaseDefinition??definition,authored.ClassData);
            enemy.Character.CharacterId=id;
            var markerDefinition=definition.SanctuaryWarning??definition.SecondPhaseDefinition?.SanctuaryWarning;
            SummonedCombatMapObject marker=null;
            if(type!="bandit")
            {
                Assert.That(markerDefinition,Is.Not.Null);
                marker=new SummonedCombatMapObject("marker.persist",enemy.Position,id,"",0,markerDefinition) {RemainingDurationTurns=2};
                combat.SummonedObjects.Add(marker);combat.MapObjects.Add(marker);
            }
            if(type=="ent")
            {
                enemy.ForestEntState=new() {OriginalDefinition=definition,PhaseTwo=true,FireTurns=2,Warning=true,ManaCarry=.35f,ArenaRoomId="fixture.room"};
                enemy.ForestEntState.Fields.Add(enemy.Position);enemy.ForestEntState.Markers.Add(marker);
                enemy.ForestEntState.Plague.Add(combat.ActiveUnit,3);enemy.ForestEntState.ActionHits.Add(("action",combat.ActiveUnit));
            }
            if(type=="king")
            {
                enemy.DeadKingState=new() {OriginalDefinition=definition,PhaseTwo=true,ShadowTurns=2};
                enemy.DeadKingState.SafeFields.Add(enemy.Position);enemy.DeadKingState.DarkFields.Add(combat.ActiveUnit.Position);
                enemy.DeadKingState.Markers.Add(marker);
            }
            if(type=="slayer")
            {
                enemy.SkeletonSlayerState=new() {OriginalDefinition=definition,PhaseTwo=true,RevengeHits=3,HammerTurns=2,HammerDamage=87,
                    SelectedHammerCenter=enemy.Position,HammerCenter=enemy.Position,AccelerateNextTurn=true};
                enemy.SkeletonSlayerState.HammerFields.Add(enemy.Position);enemy.SkeletonSlayerState.Markers.Add(marker);
            }
            if(type=="bandit")
            { enemy.BanditLeaderState=new() {OriginalDefinition=definition,PhaseTwo=true,DamageSinceTurn=39,Session=combat};enemy.BanditLeaderState.Guards.Add(combat.ActiveUnit); }
            var restored=Roundtrip(game);
            Assert.That(JsonUtility.ToJson(Snapshot(restored).Combats[0]),Is.EqualTo(JsonUtility.ToJson(Snapshot(game).Combats[0])));
            if(marker!=null)
                Assert.That(restored.ActiveCombat.SummonedObjects.Single().Definition,Is.SameAs(markerDefinition));
        }

        [TestCase(false),TestCase(true)]
        public void ContactMemoryScheduledEffectAndSummonLifetimeRemainUnchanged(bool detached)
        {
            var game=Campaign();var combat=Battle(game);var actor=combat.ActiveUnit;var enemy=combat.Units.Single(u=>!u.IsPlayerSide);
            combat.Contact=new DungeonContactState((_,_)=>true);combat.Contact.Refresh(combat);
            combat.Contact.BindVisibility((_,_)=>false);combat.Contact.BeginTurn(combat,enemy);combat.Contact.EndTurn(combat,enemy);
            var skill=Catalog.SkillDefinitions.First(s=>s!=null&&s.Effects.Any(e=>e.DelayCasterTurns>0));
            var index=Enumerable.Range(0,skill.Effects.Count).First(i=>skill.Effects[i].DelayCasterTurns>0);
            combat.ScheduledEffects.Add(new(actor.Character.CharacterId,skill,skill.Effects[index],index,2,enemy.Character.CharacterId,null) {ActionId="scheduled.test"});
            var summoned=new BattleUnit {Character=new CharacterInstanceFactory().Create(enemy.Character.Definition,enemy.Character.ClassData),
                Position=combat.Grid.GetNeighbours(enemy.Position).First(p=>combat.Grid.IsWalkable(p)&&!combat.IsPositionOccupied(p)),IsPlayerSide=true,IsAiControlledSummon=true,MaxActionPoints=300,CurrentActionPoints=200};
            summoned.Character.CharacterId="summon.persist";summoned.Character.Stats.MinAttack.BaseValue=777;
            combat.Units.Add(summoned);combat.TurnParticipants.Add(new(summoned));combat.TurnOrder.Add(summoned);combat.Summons.Add(new(summoned,actor.Character.CharacterId,"skill.fixture",0,3,7) {GroupId="group.fixture"});
            if(detached)
            {
                summoned.Character.LifeState=CharacterLifeState.Dead;summoned.Character.CurrentHealth=0;
                combat.Units.Remove(summoned);combat.TurnParticipants.RemoveAll(p=>p.Unit==summoned);combat.TurnOrder.Remove(summoned);
            }
            var restored=Roundtrip(game);var actual=restored.ActiveCombat;
            Assert.That(actual.ScheduledEffects.Single().TargetPosition,Is.Null,"JSON must preserve absence of a selected position.");
            Assert.That(actual.ScheduledEffects.Single().RemainingCasterTurns,Is.EqualTo(2));
            Assert.That(actual.Units.Any(u=>u.Character.CharacterId=="summon.persist"),Is.EqualTo(!detached));
            if(!detached)Assert.That(actual.Summons.Single().Unit,Is.SameAs(actual.Units.Single(u=>u.Character.CharacterId=="summon.persist")));
            else Assert.That(actual.Summons.Single().Unit.Character.LifeState,Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(actual.Summons.Single().Unit.Character.Stats.MinAttack.BaseValue,Is.EqualTo(777));
            Assert.That(JsonUtility.ToJson(Snapshot(restored).Combats[0]),Is.EqualTo(JsonUtility.ToJson(Snapshot(game).Combats[0])));
        }

        [Test]
        public void DamageOverTimeAndShieldTickOnceAtTheNextRealTurn()
        {
            var game=Campaign();var combat=Battle(game);var actor=combat.ActiveUnit;var enemy=combat.Units.Single(u=>!u.IsPlayerSide);
            Assert.That(new StatusEffectSystem().ApplyStatus(enemy,actor,Godmist.Core.Constants.StatusEffectIds.Bleed,
                1,3,1,.4f,0,"save.test.bleed",statusDamagePerTurnOverride:23,sharedRoll:0),Is.True);
            actor.Character.ShieldPool.AddLayer(37,"save.test.shield",3);
            var restored=Roundtrip(game);var actual=restored.ActiveCombat.ActiveUnit;
            Assert.That(JsonUtility.ToJson(Snapshot(restored).Characters.Single(c=>c.CharacterId==actor.Character.CharacterId)),
                Is.EqualTo(JsonUtility.ToJson(Snapshot(game).Characters.Single(c=>c.CharacterId==actor.Character.CharacterId))));
            var originalEffect=actor.Character.PassiveEffects.Single(e=>e.Lifetime==PassiveEffectLifetime.Timed);
            var savedEffect=actual.Character.PassiveEffects.Single(e=>e.EffectId==originalEffect.EffectId);
            Assert.That(savedEffect.RemainingDuration,Is.EqualTo(originalEffect.RemainingDuration));
            var healthAndShield=actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield;
            foreach(var pair in new[]{(combat,actor),(restored.ActiveCombat,actual)})
            {
                var damage=new CombatDamageSystem();
                new PassiveEffectLifecycleSystem(turnProcessors:new IPassiveEffectTurnProcessor[]{new DamageOverTimePassiveEffectProcessor(damage)})
                    .ProcessTurnStart(pair.Item1,pair.Item2);
            }
            Assert.That(actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield,Is.LessThan(healthAndShield),"The real DoT processor must execute a tick.");
            Assert.That(actual.Character.CurrentHealth,Is.EqualTo(actor.Character.CurrentHealth));
            Assert.That(actual.Character.ShieldPool.Layers.Select(l=>l.CurrentValue),Is.EqualTo(actor.Character.ShieldPool.Layers.Select(l=>l.CurrentValue)));
            Assert.That(savedEffect.RemainingDuration,Is.EqualTo(originalEffect.RemainingDuration));
        }

        [Test]
        public void DefeatedEnemyKeepsDeathPositionAndCannotGrantExperienceAgain()
        {
            var game=Campaign();var combat=Battle(game);var enemy=combat.Units.Single(u=>!u.IsPlayerSide);
            var id=enemy.Character.CharacterId;
            combat.TryMarkEnemyDefeated(id,enemy.Position);
            Assert.That(new CombatRewardSystem().TryGrantDefeatExperience(game,combat,id),Is.True);
            enemy.Character.LifeState=CharacterLifeState.Dead;enemy.Character.CurrentHealth=0;
            combat.Units.Remove(enemy);combat.TurnParticipants.RemoveAll(t=>t.Unit==enemy);combat.TurnOrder.Remove(enemy);
            // Add a second native encounter participant so the saved fight remains ongoing.
            game.ActiveDungeon.TryGetCurrentFloor(out var floor);var origin=floor.Encounters.Skip(1).First();var authored=origin.Enemies[0];
            var actor=new CharacterInstanceFactory().Create(authored.CharacterDefinition,authored.ClassData);
            var survivor=new CombatSessionFactory().CreateJoiningUnit(actor,enemy.Position,false);
            game.ActiveDungeon.TryGetPreRolledRewards(origin.InstanceId,out var rewards);
            DungeonCombatMembershipSystem.RegisterEnemy(combat,origin.InstanceId,authored.InstanceId,rewards,survivor);
            combat.Units.Add(survivor);combat.TurnParticipants.Add(new(survivor));combat.TurnOrder.Add(survivor);
            Assert.That(CombatPersistence.CanSave(game,out _),Is.False,"A death still awaiting world/body synchronization is not a stable snapshot.");
            Assert.That(new DungeonCombatStateSyncSystem().TrySyncDefeatedEnemyStates(game,combat),Is.True);
            Assert.That(DungeonCombatOutcomeSystem.TryCreateEnemyBodies(game.ActiveDungeon,combat.DungeonContext,combat),Is.True);
            var xp=game.CharactersById.ToDictionary(p=>p.Key,p=>p.Value.CurrentExperience);
            var restored=Roundtrip(game);
            Assert.That(restored.ActiveCombat.TryGetEnemyDeathPosition(id,out var death),Is.True);Assert.That(death,Is.EqualTo(enemy.Position));
            Assert.That(new CombatRewardSystem().TryGrantDefeatExperience(restored,restored.ActiveCombat,id),Is.False);
            Assert.That(restored.CharactersById.ToDictionary(p=>p.Key,p=>p.Value.CurrentExperience),Is.EqualTo(xp));
            Assert.That(restored.Gold,Is.EqualTo(game.Gold));
        }

        [Test]
        public void RebuildingEquipmentOnLoadDoesNotResetItsSavedRuntimeCounters()
        {
            var game=Campaign();var combat=Battle(game);var actor=combat.ActiveUnit;
            var managed=actor.Character.PassiveEffects.First(PassiveEffectReconciliationSystem.IsManaged);
            managed.StateValues.Add(Godmist.Runtime.Effects.PassiveEffectRuntimeStateValue.Integer("save.test.spent",2));
            var restored=Roundtrip(game);
            var effect=restored.ActiveCombat.ActiveUnit.Character.PassiveEffects.Single(e=>e.EffectId==managed.EffectId);
            Assert.That(effect.StateValues.Single(v=>v.Key=="save.test.spent").IntegerValue,Is.EqualTo(2));
        }

        [TestCase("manual"),TestCase("quick"),TestCase("auto")]
        public void SaveSlotsExposeCombatPreviewAndRestoreTheSameBattle(string kind)
        {
            var game=Campaign();var combat=Battle(game);
            var folder=Path.Combine(Path.GetTempPath(),"godmist-combat-slots-"+Guid.NewGuid().ToString("N"));
            try
            {
                var manager=new SaveManager(folder);string error;
                var saved=kind=="manual"?manager.TrySaveManual(game,"Walka",out _,out error)
                    :kind=="quick"?manager.TrySaveQuick(game,out error):manager.TrySaveAutosave(game,out error);
                Assert.That(saved,Is.True,error);
                Assert.That(manager.TryGetSaveSummaries(out var summaries,out error),Is.True,error);
                var summary=summaries.Single();Assert.That(summary.Preview.IsInCombat,Is.True);
                Assert.That(Godmist.Views.UI.SaveSummaryPresentation.Location(summary,Catalog),Does.Contain("walka"));
                Assert.That(manager.TryLoadSave(summary,Catalog,out var restored,out error),Is.True,error);
                Assert.That(restored.ActiveCombat.CombatId,Is.EqualTo(combat.CombatId));
                Assert.That(restored.ActiveCombat.ActiveUnit.CurrentActionPoints,Is.EqualTo(combat.ActiveUnit.CurrentActionPoints));
            }
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
    }
}
