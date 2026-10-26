using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.SaveLoad;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Data.World;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
namespace Godmist.Tests.EditMode
{
    public sealed class ForestEntBossTests
    {
        private Random.State randomState;
        private CharacterDefinition Ent => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.EntPath);
        private CharacterDefinition Hero => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ForestEnemiesSetup.DruidPath);
        [SetUp] public void Preserve() { randomState = Random.state; Random.InitState(12345); }
        [TearDown] public void Restore() => Random.state = randomState;
        private BattleUnit Unit(bool boss, int x, int y)
        {
            var definition = boss ? Ent : Hero;
            var unit = new BattleUnit { Character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 1),
                Position = new GridPosition(x,y), IsPlayerSide = !boss, IsBoss = boss, CurrentActionPoints = 10000, MaxActionPoints = 10000 };
            unit.Character.CharacterId += "." + Guid.NewGuid(); unit.Character.Stats.CritChance = new Stat(0);
            unit.Character.ResourcePool.CurrentValue = 100;
            if (!boss) { unit.Character.Stats.MaxHealth = new Stat(10000); unit.Character.CurrentHealth = 10000; }
            return unit;
        }
        private CombatSession Session(params BattleUnit[] units)
        {
            var session = new CombatSession { CombatId = "test.forest.ent", Grid = new BattleGrid(8,8), Units = units.ToList(), ActiveUnit = units[0] };
            foreach (var unit in units) unit.WarriorState.Session = session;
            return session;
        }
        private ForestEntBossSystem Owner(CombatSession session) => new(session, new CombatDamageSystem(), new StatusEffectSystem(), new CombatRecoverySystem());
        private static int HalfHealth(BattleUnit boss) => Mathf.FloorToInt(boss.Character.Stats.MaxHealth.GetValue() * .5f);
        private void Transform(ForestEntBossSystem owner, BattleUnit boss)
        { boss.Character.CurrentHealth = HalfHealth(boss); owner.CheckPhases(); owner.BeginTurn(boss); Assert.That(owner.IsPhaseTwo(boss), Is.True, "Fixture must enter the burning phase before testing phase-II mechanics."); boss.CurrentActionPoints = boss.MaxActionPoints = 10000; }
        private bool Sanctuary(CombatSession session, BattleUnit boss) => new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem())
            .TryExecute(session,boss,boss.Character.Skills[2],boss);
        private void Equip(BattleUnit unit, string file, int level=10)
        {
            var definition=AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>(ForestEnemiesSetup.Root+"Loot/EID_Forest_Ent"+file+".asset");
            var item=new EquipmentItemInstance { Definition=definition, ItemLevel=level };
            unit.Character.Equipment.SetItem(definition.EquipmentSlot,item);
            new PassiveEffectReconciliationSystem().Reconcile(unit.Character);
        }
        [Test] public void GlaivePenetratesOnlyLargerMaxHpAndDoesNotLeavePermanentBuff()
        {
            var hero=Unit(false,1,1);var enemy=Unit(true,2,1);var s=Session(hero,enemy);Equip(hero,"Glaive");
            hero.Character.Stats.MaxHealth=new Stat(100);enemy.Character.Stats.MaxHealth=new Stat(200);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(hero,enemy,DamageType.Physical),Is.EqualTo(.2f));
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(hero,enemy,DamageType.Magical),Is.Zero);
            s.PassiveEffectEventDispatcher=new PassiveEffectCombatEventDispatcher();
            using var executor=new PassiveEffectCombatExecutionSystem(s,new StatusEffectSystem());
            s.PassiveEffectEventDispatcher.Publish(PassiveEffectTriggerType.OnAttack,new PassiveEffectCombatContext(session:s,source:hero,target:enemy));
            Assert.That(hero.Character.PassiveEffects.Any(e=>e.EffectId.Contains("operation.forest.ent.glaive")),Is.False);
            enemy.Character.Stats.MaxHealth=new Stat(100);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(hero,enemy,DamageType.Physical),Is.Zero);
        }
        [Test] public void TunicAvoidsOnlyPoisonAtAuthoredChance()
        {
            var hero=Unit(false,1,1);var enemy=Unit(true,2,1);Equip(hero,"Tunic");
            var count=0;
            for(var i=0;i<1000;i++) if(PassiveEffectCombatExecutionSystem.TryPreventStatus(enemy,hero,StatusEffectIds.Poison))count++;
            Assert.That(count,Is.InRange(400,600));
            for(var i=0;i<20;i++)Assert.That(PassiveEffectCombatExecutionSystem.TryPreventStatus(enemy,hero,StatusEffectIds.Bleed),Is.False);
        }
        [Test] public void HammerHealingUsesLowestAllyAndCannotRecursivelyHeal()
        {
            var healer=Unit(false,1,1);var ally=Unit(false,2,1);var full=Unit(false,3,1);var enemy=Unit(true,4,1);
            var s=Session(healer,ally,full,enemy);Equip(healer,"Hammer");
            healer.Character.Stats.MaxHealth=new Stat(10000);healer.Character.CurrentHealth=5000;
            ally.Character.CurrentHealth=1000;enemy.Character.CurrentHealth=1;
            s.PassiveEffectEventDispatcher=new PassiveEffectCombatEventDispatcher();
            using var executor=new PassiveEffectCombatExecutionSystem(s,new StatusEffectSystem());
            var recovery=new CombatRecoverySystem();var procs=0;
            for(var i=0;i<100;i++)
            {
                var before=ally.Character.CurrentHealth;
                recovery.Heal(healer,10,new PassiveEffectCombatContext(session:s,source:healer,target:healer));
                var bonus=ally.Character.CurrentHealth-before;
                Assert.That(bonus,Is.EqualTo(0).Or.EqualTo(5));if(bonus>0)procs++;
            }
            Assert.That(procs,Is.InRange(15,45));Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(1));Assert.That(full.Character.CurrentHealth,Is.EqualTo(10000));
        }
        [Test] public void RootsApplyAllResistanceReductionTogetherAndExpireAfterTwoTurns()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);
            var modifier=new ModifyResistanceSystem();var roots=Ent.StartingSkills[1];
            Assert.That(modifier.Apply(boss,hero,roots,roots.Effects[1],1),Is.True);
            foreach(ResistanceType type in Enum.GetValues(typeof(ResistanceType)))
                Assert.That(ModifyResistanceSystem.GetModifier(hero,type),Is.EqualTo(-.25f));
            var status=new StatusEffectSystem();status.ProcessTurnEnd(hero);
            Assert.That(ModifyResistanceSystem.GetModifier(hero,ResistanceType.Poison),Is.EqualTo(-.25f));status.ProcessTurnEnd(hero);
            Assert.That(ModifyResistanceSystem.GetModifier(hero,ResistanceType.Poison),Is.Zero);
        }
        [Test] public void AssetsContainBothSkillsetsAndApprovedRanges()
        {
            Assert.That(Ent.BaseStats.MaxHealth, Is.EqualTo(784)); Assert.That(Ent.GrowthData.MaxHealth, Is.EqualTo(91f)); // PT-06.
            Assert.That(Ent.StartingSkills.Select(s=>s.ResourceCost), Is.EqualTo(new[]{0,40,60}));
            Assert.That(Ent.StartingSkills.Select(s=>s.ActionCost), Is.EqualTo(new[]{60,70,20}));
            var roots = Ent.StartingSkills[1];
            Assert.That(roots.Effects.All(e=>e.DurationTurns==2), Is.True);
            Assert.That(roots.Effects.Count, Is.EqualTo(2)); Assert.That(roots.Effects[1].ResistanceType, Is.EqualTo(ResistanceType.All));
            var burning = Ent.SecondPhaseDefinition;
            Assert.That(burning.StartingSkills.Select(s=>s.ResourceCost), Is.EqualTo(new[]{0,25,70}));
            Assert.That(burning.StartingSkills.Select(s=>s.ActionCost), Is.EqualTo(new[]{60,35,100}));
            Assert.That(burning.StartingSkills[1].TargetModes[0].AbsoluteRange.Shape, Is.EqualTo(RangeShape.Line));
            Assert.That(burning.StartingSkills[1].TargetModes[0].AbsoluteRange.MaxRange, Is.EqualTo(6));
            Assert.That(burning.SanctuaryWarning.WorldSprite, Is.Not.Null); Assert.That(burning.SanctuaryFire.WorldSprite, Is.Not.Null);
        }
        [Test] public void IgnitionHitsTheSixthTileButNotSeventhOrOffLineAndPaysCosts()
        {
            var boss=Unit(true,0,4);var sixth=Unit(false,6,4);var seventh=Unit(false,7,4);var offLine=Unit(false,5,5);
            var s=Session(boss,sixth,seventh,offLine);using var owner=Owner(s);Transform(owner,boss);
            CombatTurnSystem.RefreshMaxActionPoints(s,boss,true);
            var initialAp=boss.CurrentActionPoints;var ignitionCost=CombatActionCostCalculator.CalculateSkillCost(boss,boss.Character.Skills[1]);
            sixth.Character.Stats.Dodge=new Stat(0);sixth.Character.Stats.MagicalDefense=new Stat(0);
            var skill=boss.Character.Skills[1];var targets=new CombatTargetingService().GetTargets(s,boss,skill.TargetModes[0],sixth.Position);
            Assert.That(targets,Is.EquivalentTo(new[]{sixth}));
            var hp=sixth.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(s,boss,skill,
                new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}},null,null,
                new Dictionary<int,GridPosition?>{{0,sixth.Position}}),Is.True);
            Assert.That(sixth.Character.CurrentHealth,Is.LessThan(hp));Assert.That(seventh.Character.CurrentHealth,Is.EqualTo(10000));
            Assert.That(offLine.Character.CurrentHealth,Is.EqualTo(10000));Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(75));
            Assert.That(boss.CurrentActionPoints,Is.EqualTo(initialAp-ignitionCost));
        }
        [Test] public void EntAiUsesThePaidSanctuaryWhenOpponentsAreOutOfAttackReach()
        {
            var boss=Unit(true,7,7);var hero=Unit(false,0,0);var s=Session(boss,hero);using var owner=Owner(s);Transform(owner,boss);
            CombatTurnSystem.RefreshMaxActionPoints(s,boss,true);
            var ai=new SimpleCombatAiSystem(skillExecutionSystem:new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()));
            Assert.That(ai.TryExecuteNextAction(s,boss,ai.CreateTurnState(),out var action),Is.True);
            Assert.That(action.Type,Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(action.Skill.SkillId,Is.EqualTo(ForestEntBossSystem.SanctuarySkillId));Assert.That(owner.IsWarning(boss),Is.True);
            Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(30));Assert.That(boss.CurrentActionPoints,Is.Zero);
        }
        [Test] public void TransformOccursOnceWithoutRefillingHpResourceOrAp()
        {
            var boss=Unit(true,4,4); var s=Session(boss); using var owner=Owner(s);
            boss.Character.ResourcePool.CurrentValue=12; boss.CurrentActionPoints=50;
            boss.Character.CurrentHealth=HalfHealth(boss)+1;owner.CheckPhases();Assert.That(owner.IsPhaseTwo(boss),Is.False);
            boss.Character.CurrentHealth=HalfHealth(boss);owner.CheckPhases();
            Assert.That(owner.IsPhaseTwo(boss),Is.False);Assert.That(boss.ForestEntState.PhaseTwoPending,Is.True);
            Assert.That(boss.Character.Definition,Is.SameAs(Ent));owner.BeginTurn(boss);
            Assert.That(owner.IsPhaseTwo(boss),Is.True); Assert.That(boss.Character.CurrentHealth,Is.EqualTo(HalfHealth(boss)));
            Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(12)); Assert.That(boss.CurrentActionPoints,Is.EqualTo(50));
            Assert.That(boss.Character.Skills,Is.EquivalentTo(Ent.SecondPhaseDefinition.StartingSkills));
            boss.Character.CurrentHealth=Mathf.RoundToInt(boss.Character.Stats.MaxHealth.GetValue());owner.CheckPhases();Assert.That(owner.IsPhaseTwo(boss),Is.True);
        }
        [Test]
        public void ArenaRoomIsCapturedBeforeEntMovesAndSurvivesHisTransformation()
        {
            var boss=Unit(true,4,4);
            var s=Session(boss);
            var map=new DungeonFloorMap();
            for(var x=0;x<8;x++) for(var y=0;y<8;y++) map.TryAddFloorCell(new Vector2Int(x,y),x<4?"other":"arena");
            s.DungeonContext=new DungeonCombatContext("forest",0,"ent","arena",Vector2Int.zero,Vector2Int.one,
                map.FloorCells,new DungeonCellEdge[0],new DungeonCombatMapObjectSnapshot[0],surfaceMap:map);
            using var owner=Owner(s);
            Assert.That(boss.ForestEntState.ArenaRoomId,Is.EqualTo("arena"));
            boss.Position=new GridPosition(0,0);Transform(owner,boss);
            Assert.That(boss.ForestEntState.ArenaRoomId,Is.EqualTo("arena"));
        }
        [Test] public void DeadEntNeverTransforms()
        {
            var boss=Unit(true,4,4);using var owner=Owner(Session(boss));boss.Character.CurrentHealth=0;owner.CheckPhases();
            Assert.That(owner.IsPhaseTwo(boss),Is.False); Assert.That(boss.Character.Definition,Is.SameAs(Ent));
        }
        [Test] public void MultiHitFinishesBeforeTransformation()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(hero,boss);using var owner=Owner(s);
            boss.Character.CurrentHealth=HalfHealth(boss)+100;boss.Character.Stats.PhysicalDefense=new Stat(0);
            hero.Character.Stats.MinAttack=hero.Character.Stats.MaxAttack=new Stat(100);
            var skill=UnityEngine.Object.Instantiate(Ent.BasicAttackSkill);
            try {
                var data=new SerializedObject(skill);var effects=data.FindProperty("effects"); effects.arraySize=1;
                effects.GetArrayElementAtIndex(0).FindPropertyRelative("damageHitCount").intValue=3;
                effects.GetArrayElementAtIndex(0).FindPropertyRelative("alwaysHits").boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
                var phases=new List<string>();s.DamageApplied+=(source,target,amount,type,id)=>phases.Add(boss.Character.Definition.Id);
                var targets=new CombatTargetingService().GetTargets(s,hero,skill.TargetModes[0],boss.Position);
                Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(s,hero,skill,
                    new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}},null,null,new Dictionary<int,GridPosition?>{{0,boss.Position}}),Is.True);
                Assert.That(phases,Has.Count.EqualTo(3));Assert.That(phases.All(id=>id==ForestEntBossSystem.BossId),Is.True);
                Assert.That(owner.IsPhaseTwo(boss),Is.False);Assert.That(boss.ForestEntState.PhaseTwoPending,Is.True);
                owner.BeginTurn(boss);Assert.That(owner.IsPhaseTwo(boss),Is.True);
            } finally { UnityEngine.Object.DestroyImmediate(skill); }
        }
        [Test] public void BarkIsRemovedButForeignDebuffRemains()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);using var owner=Owner(s);
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(s,boss,Ent.StartingSkills[2],boss),Is.True);
            var status=new StatusEffectSystem();boss.Character.Resistances.FireResistance=0;
            Assert.That(status.ApplyStatus(hero,boss,StatusEffectIds.Burn,1f,3,1,.5f,0f,"test.foreign"),Is.True);
            Transform(owner,boss);
            Assert.That(boss.Character.PassiveEffects.Any(e=>e.Sources.Any(source=>source.SourceId.StartsWith(ForestEntBossSystem.BarkSkillId))),Is.False);
            Assert.That(StatusEffectSystem.HasStatus(boss,StatusEffectIds.Burn),Is.True);
        }
        [Test] public void PlagueDeduplicatesMultiHitsCapsAtFiveAndNeverRecurses()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);using var owner=Owner(s);Transform(owner,boss);
            var damage=new CombatDamageSystem();
            for(var i=0;i<3;i++)damage.ApplyDamage(s,boss,hero,1,DamageType.Direct,
                passiveContext:new PassiveEffectCombatContext(session:s,source:boss,target:hero,actionId:"one"));
            Assert.That(owner.PlagueStacks(boss,hero),Is.EqualTo(1));
            for(var i=0;i<10;i++)damage.ApplyDamage(s,boss,hero,1,DamageType.Direct,
                passiveContext:new PassiveEffectCombatContext(session:s,source:boss,target:hero,actionId:"next"+i));
            Assert.That(owner.PlagueStacks(boss,hero),Is.EqualTo(5));
            var presentation=CombatStatusPresentationFactory.Create(hero.Character.PassiveEffects,owner:hero)
                .Single(entry=>entry.Effect.DisplayName=="Demoniczna Zaraza");
            Assert.That(presentation.Kind,Is.EqualTo(CombatStatusKind.Debuff));
            var hp=hero.Character.CurrentHealth;boss.Character.ResourcePool.CurrentValue=0;
            owner.BeginTurn(hero);Assert.That(hp-hero.Character.CurrentHealth,Is.EqualTo(250));
            Assert.That(owner.PlagueStacks(boss,hero),Is.EqualTo(5));Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(2));
            owner.BeginTurn(hero);Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(5));
        }
        [Test] public void PeriodicDamageAddsAtMostOneStackPerVictimTurn()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);using var owner=Owner(s);Transform(owner,boss);
            var damage=new CombatDamageSystem();
            damage.ApplyDamage(s,boss,hero,1,DamageType.Burn);damage.ApplyDamage(s,boss,hero,1,DamageType.Poison);
            Assert.That(owner.PlagueStacks(boss,hero),Is.EqualTo(1));owner.BeginTurn(hero);
            damage.ApplyDamage(s,boss,hero,1,DamageType.Burn);Assert.That(owner.PlagueStacks(boss,hero),Is.EqualTo(2));
        }
        [Test] public void SanctuaryWarnsAndCannotRecastAndKeepsReachableRefuge()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);using var owner=Owner(s);Transform(owner,boss);
            Assert.That(Sanctuary(s,boss),Is.True); Assert.That(owner.IsWarning(boss),Is.True);
            Assert.That(owner.BurningFields(boss),Has.Count.EqualTo(32));
            Assert.That(s.Grid.GetNeighbours(hero.Position).Any(p=>s.CanMoveBetween(hero,hero.Position,p)
                && !s.IsPositionOccupied(p) && !owner.BurningFields(boss).Contains(p)),Is.True);
            Assert.That(s.TurnParticipants,Is.Empty);
            hero.Position=owner.BurningFields(boss).First();var hp=hero.Character.CurrentHealth;s.NotifyUnitEnteredCell(hero);
            Assert.That(hero.Character.CurrentHealth,Is.EqualTo(hp));
            boss.Character.ResourcePool.CurrentValue=100;boss.CurrentActionPoints=10000;
            Assert.That(Sanctuary(s,boss),Is.False);Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
        }
        [Test] public void FireLastsThreeBossTurnsAndHitsOnlyOncePerVictimTurn()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);using var owner=Owner(s);Transform(owner,boss);
            Assert.That(Sanctuary(s,boss),Is.True); owner.BeginTurn(boss);Assert.That(owner.IsWarning(boss),Is.False);
            hero.Position=owner.BurningFields(boss).First();s.NotifyUnitEnteredCell(hero);var hp=hero.Character.CurrentHealth;
            s.NotifyUnitEnteredCell(hero);Assert.That(hero.Character.CurrentHealth,Is.EqualTo(hp));
            owner.BeginTurn(hero);Assert.That(hero.Character.CurrentHealth,Is.LessThan(hp));
            owner.BeginTurn(boss);owner.BeginTurn(boss);Assert.That(owner.BurningFields(boss),Is.Not.Empty);
            owner.BeginTurn(boss);Assert.That(owner.BurningFields(boss),Is.Empty);Assert.That(s.SummonedObjects,Is.Empty);
        }
        [Test] public void BossDeathRemovesFireAndDisposeRemovesPlagueAndCallbacks()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var s=Session(boss,hero);var owner=Owner(s);Transform(owner,boss);
            Assert.That(Sanctuary(s,boss),Is.True);boss.Character.CurrentHealth=0;owner.CheckPhases();
            Assert.That(s.SummonedObjects,Is.Empty);owner.Dispose();Assert.That(s.SkillResolved,Is.Null);Assert.That(s.DamageApplied,Is.Null);
        }
        [Test] public void MergingCombatsPreservesPhasePlagueWarningAndTerrainClock()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var source=Session(boss,hero);var firstOwner=Owner(source);Transform(firstOwner,boss);
            Assert.That(Sanctuary(source,boss),Is.True);
            new CombatDamageSystem().ApplyDamage(source,boss,hero,1,DamageType.Direct,
                passiveContext:new PassiveEffectCombatContext(session:source,source:boss,target:hero,actionId:"before.merge"));
            var destination=Session(Unit(false,0,0));using var nextOwner=Owner(destination);
            var cells=Enumerable.Range(0,8).SelectMany(x=>Enumerable.Range(0,8).Select(y=>new Vector2Int(x,y))).ToArray();
            source.IsDungeonWide=destination.IsDungeonWide=true;
            source.DungeonContext=new DungeonCombatContext("forest",0,"one","arena",Vector2Int.zero,Vector2Int.zero,cells,null,null);
            destination.DungeonContext=new DungeonCombatContext("forest",0,"two","arena",Vector2Int.zero,Vector2Int.zero,cells,null,null);
            Assert.That(DungeonCombatMembershipSystem.TryMerge(destination,source),Is.True);
            firstOwner.Dispose(true);nextOwner.CheckPhases();
            Assert.That(nextOwner.IsPhaseTwo(boss),Is.True);Assert.That(nextOwner.PlagueStacks(boss,hero),Is.EqualTo(1));
            Assert.That(nextOwner.IsWarning(boss),Is.True);Assert.That(destination.SummonedObjects,Has.Count.EqualTo(32));
            nextOwner.BeginTurn(boss);Assert.That(nextOwner.IsWarning(boss),Is.False);
            nextOwner.BeginTurn(boss);nextOwner.BeginTurn(boss);Assert.That(nextOwner.BurningFields(boss),Is.Not.Empty);
            nextOwner.BeginTurn(boss);Assert.That(nextOwner.BurningFields(boss),Is.Empty);
        }
        [Test] public void LateJoiningEntIsDiscoveredAndCombatEndRestoresLivingDefinition()
        {
            var hero=Unit(false,1,1);var s=Session(hero);var owner=Owner(s);var boss=Unit(true,4,4);s.Units.Add(boss);
            boss.Character.CurrentHealth=HalfHealth(boss);owner.CheckPhases();owner.BeginTurn(boss);Assert.That(owner.IsPhaseTwo(boss),Is.True);
            owner.Dispose();Assert.That(boss.Character.Definition,Is.SameAs(Ent));Assert.That(boss.ForestEntState,Is.Null);
        }
        [Test] public void ThresholdWaitsForOwnTurnAndRemainsLatchedAfterHealing()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var session=Session(hero,boss);using var owner=Owner(session);
            boss.Character.CurrentHealth=HalfHealth(boss);owner.CheckPhases();owner.BeginTurn(hero);
            Assert.That(owner.IsPhaseTwo(boss),Is.False);Assert.That(boss.Character.Definition,Is.SameAs(Ent));
            boss.Character.CurrentHealth=Mathf.RoundToInt(boss.Character.Stats.MaxHealth.GetValue());owner.CheckPhases();owner.BeginTurn(boss);
            Assert.That(owner.IsPhaseTwo(boss),Is.True);Assert.That(boss.ForestEntState.PhaseTwoPending,Is.False);
        }
        [Test] public void PendingTransformationIsCancelledByDeath()
        {
            var boss=Unit(true,4,4);using var owner=Owner(Session(boss));boss.Character.CurrentHealth=HalfHealth(boss);owner.CheckPhases();
            Assert.That(boss.ForestEntState.PhaseTwoPending,Is.True);boss.Character.CurrentHealth=0;owner.CheckPhases();owner.BeginTurn(boss);
            Assert.That(owner.IsPhaseTwo(boss),Is.False);Assert.That(boss.ForestEntState.PhaseTwoPending,Is.False);
        }
        [Test] public void PendingPhaseSurvivesOwnerRebindWithoutStartingOnAnotherUnitTurn()
        {
            var boss=Unit(true,4,4);var hero=Unit(false,3,4);var session=Session(hero,boss);var first=Owner(session);
            boss.Character.CurrentHealth=HalfHealth(boss);first.CheckPhases();first.Dispose(true);
            using var resumed=Owner(session);resumed.BeginTurn(hero);
            Assert.That(resumed.IsPhaseTwo(boss),Is.False);Assert.That(boss.ForestEntState.PhaseTwoPending,Is.True);
            resumed.BeginTurn(boss);Assert.That(resumed.IsPhaseTwo(boss),Is.True);
        }
        [Test] public void DamageAfterTurnStartWaitsUntilFollowingBossTurn()
        {
            var boss=Unit(true,4,4);using var owner=Owner(Session(boss));owner.BeginTurn(boss);
            boss.Character.CurrentHealth=HalfHealth(boss);owner.CheckPhases();owner.EndTurn(boss);
            Assert.That(owner.IsPhaseTwo(boss),Is.False);owner.BeginTurn(boss);Assert.That(owner.IsPhaseTwo(boss),Is.True);
        }
        [Test] public void ForestGenerationPlacesOneEntInFinalRoomAcrossSeeds()
        {
            var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            var content=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            foreach(var level in new[]{10,20}) foreach(var seed in new[]{17,219,3401})
            {
                var dungeon=new DungeonGeneratorService().Generate(definition,seed,level);
                new DungeonObjectPlacementSystem().Place(dungeon,content.EquipmentPartDefinitions);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                var bosses=dungeon.Floors.SelectMany(f=>f.Encounters).Where(e=>e.Definition==definition.BossEncounterDefinition).ToArray();
                Assert.That(bosses,Has.Length.EqualTo(1),$"{level}/{seed}");
                Assert.That(bosses[0].RoomId,Is.EqualTo(dungeon.Floors.Last().Rooms.Single(room=>room.RoomType==DungeonRoomType.Final).RoomId));
            }
        }
        [TestCase(10)] [TestCase(20)]
        public void ForestSaveRestorePreservesDefeatedEntAndExactBagContentsWithoutDuplication(int level)
        {
            var content=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            var dungeon=new DungeonGeneratorService().Generate(definition,219,level);
            new DungeonObjectPlacementSystem().Place(dungeon,content.EquipmentPartDefinitions);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var floor=dungeon.Floors[0];
            var boss=floor.Encounters.Single(e=>e.Definition==definition.BossEncounterDefinition);
            var enemy=boss.Enemies.Single();
            Assert.That(boss.TryMarkEnemyDefeated(enemy.InstanceId),Is.True);
            Assert.That(boss.TryMarkEnemyBodyGenerated(enemy.InstanceId),Is.True);
            Assert.That(dungeon.TryMarkEncounterCleared(boss.InstanceId),Is.True);
            var bagDefinition=AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(ForestEnemiesSetup.Root+"Loot/ID_Forest_EntBag.asset");
            var bag=new LootTableSystem(new System.Random(587),content.EquipmentPartDefinitions,lootLevel:level).CreateLootBag(bagDefinition);
            var itemIds=bag.Contents.Select(i=>i.InstanceId).ToArray();
            var position=boss.ExplorationPosition;
            var bodyId=boss.InstanceId+".body."+enemy.InstanceId;
            Assert.That(floor.TryAddEnemyBody(new DungeonEnemyBodyInstance(bodyId,enemy.InstanceId,position,37,new[]{bag})),Is.True);
            var session=new GameSession(GameModeState.Exploration);
            Assert.That(session.TrySetActiveDungeon(dungeon),Is.True);
            var serializer=new GameSaveJsonSerializer();
            for(var pass=0;pass<2;pass++)
            {
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"forest integration");
                Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot),out var data),Is.True);
                Assert.That(new GameSessionRestoreFactory().TryCreate(data,content,out session),Is.True);
                Assert.That(session.ActiveDungeon.DungeonLevel,Is.EqualTo(level));
                Assert.That(session.ActiveDungeon.IsEncounterCleared(boss.InstanceId),Is.True);
                var restoredFloor=session.ActiveDungeon.Floors[0];
                Assert.That(restoredFloor.TryGetEncounter(boss.InstanceId,out var restoredBoss),Is.True);
                Assert.That(restoredBoss.IsEnemyDefeated(enemy.InstanceId),Is.True);
                Assert.That(restoredBoss.IsEnemyBodyGenerated(enemy.InstanceId),Is.True);
                Assert.That(restoredFloor.EnemyBodies.Count,Is.EqualTo(1));
                var restoredBody=restoredFloor.EnemyBodies.Single();
                Assert.That(restoredBody.InstanceId,Is.EqualTo(bodyId));
                Assert.That(restoredBody.Position,Is.EqualTo(position));
                Assert.That(restoredBody.Gold,Is.EqualTo(37));
                var restoredBag=(LootBagItemInstance)restoredBody.Items.Single();
                Assert.That(restoredBag.InstanceId,Is.EqualTo(bag.InstanceId));
                Assert.That(restoredBag.Contents.Select(i=>i.InstanceId),Is.EqualTo(itemIds));
            }
        }
        [TestCase(10)] [TestCase(20)] public void EntBagActuallyGeneratesUniquesAtExpectedLevel(int level)
        {
            var content=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var bag=AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(ForestEnemiesSetup.Root+"Loot/ID_Forest_EntBag.asset");
            var loot=new LootTableSystem(new System.Random(587),content.EquipmentPartDefinitions,lootLevel:level);
            var uniqueIds=new HashSet<string>();
            for(var i=0;i<100;i++) foreach(var item in loot.CreateLootBag(bag).Contents.OfType<EquipmentItemInstance>())
            {
                Assert.That(item.Definition,Is.TypeOf<UniqueEquipmentDefinition>());Assert.That(item.ItemLevel,Is.EqualTo(level));
                Assert.That(item.PartIds,Has.Count.EqualTo(3));uniqueIds.Add(item.Definition.Id);
            }
            Assert.That(uniqueIds,Has.Count.EqualTo(4));
        }
        [Test] public void LootHasFourClassUniquesAndLevelTwentyGaldurytes()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<DungeonLootTableCatalog>(ForestEnemiesSetup.Root+"Loot/DLC_Forest_Loot.asset");
            foreach(var bracket in catalog.Entries) Assert.That(bracket.BossLootTable,Is.Not.Null);
            var bag=AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(ForestEnemiesSetup.Root+"Loot/ID_Forest_EntBag.asset");
            Assert.That(bag.BossUniqueDrop.DropChance,Is.EqualTo(.3f));Assert.That(bag.BossUniqueDrop.EquipmentDefinitions,Has.Count.EqualTo(4));
            Assert.That(bag.BossUniqueDrop.EquipmentDefinitions.Select(u=>u.RequiredClass.Id).Distinct().Count(),Is.EqualTo(4));
            Assert.That(bag.Brackets[0].Pools,Is.Empty);
            var rune=bag.Brackets[1].Pools.Last().Entries.Single();Assert.That(rune.FixedGalduryteLevel,Is.EqualTo(20));
            Assert.That(rune.MinimumGalduryteRarity,Is.EqualTo(RarityTier.Rare));
            foreach(var unique in bag.BossUniqueDrop.EquipmentDefinitions.Cast<UniqueEquipmentDefinition>())
                foreach(var effect in unique.Effects.Concat(unique.EffectsLevel20)) Assert.That(PassiveEffectDefinitionValidator.Validate(effect).IsValid,Is.True);
        }
    }
}
