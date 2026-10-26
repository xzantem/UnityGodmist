using System;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Systems.Characters;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class SeamlessDungeonCombatTests
    {
        [TestCase(Godmist.Core.Enums.DamageType.Bleed)]
        [TestCase(Godmist.Core.Enums.DamageType.Poison)]
        [TestCase(Godmist.Core.Enums.DamageType.Burn)]
        public void EntryTurnResumesRemainingApAndTicksDotExactlyOnce(DamageType type)
        {
            var actor=OpeningMage("outside");var ally=OpeningMage("active",true,4);
            var session=OpeningSession(ally,actor);actor.HasInitializedCombatResources=true;
            actor.AvailableKnives=2;actor.MaxKnives=7;actor.MovementStepsThisTurn=3;
            var id=type==DamageType.Bleed?Godmist.Core.Constants.StatusEffectIds.Bleed:type==DamageType.Poison?Godmist.Core.Constants.StatusEffectIds.Poison:Godmist.Core.Constants.StatusEffectIds.Burn;
            actor.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
                EffectId=id,Lifetime=Godmist.Core.Enums.PassiveEffectLifetime.Timed,
                TickPhase=Godmist.Core.Enums.PassiveEffectTickPhase.TurnStart,RemainingDuration=4,TotalDamage=400 });
            var bus=new CombatFeedbackBus();int ticks=0,starts=0,resumes=0;
            bus.Published+=e=>{if(e.Target==actor&&e.Type==CombatFeedbackType.Damage&&e.DamageType==type)ticks++;};
            var damage=new CombatDamageSystem(feedbackBus:bus);
            var lifecycle=new PassiveEffectLifecycleSystem(turnProcessors:new IPassiveEffectTurnProcessor[]{new DamageOverTimePassiveEffectProcessor(damage)});
            var status=new StatusEffectSystem(damageSystem:damage,passiveEffectLifecycleSystem:lifecycle,feedbackBus:bus);
            void Start(BattleUnit u){if(u==actor)starts++;status.ProcessTurnStart(session,u);}
            var turns=new CombatTurnSystem(onUnitTurnBegan:Start,onUnitTurnEnded:status.ProcessTurnEnd);
            session.TurnParticipants.AddRange(session.Units.Select(u=>new CombatTurnParticipant(u)));
            session.ActiveParticipant=session.TurnParticipants[0];session.ActiveUnit=ally;ally.HasUnfinishedCombatTurn=true;
            session.ResumeTurn=p=>resumes++;
            Assert.That(turns.BeginEntryTurn(session,actor,Start),Is.True);
            Assert.That(ticks,Is.EqualTo(1));Assert.That(starts,Is.EqualTo(1));
            var budget=actor.CurrentActionPoints;actor.CurrentActionPoints-=600;actor.MovementStepsThisTurn=2;
            var movement=new CombatMovementSystem();
            var ordinary=movement.CalculateReachablePaths(session,actor);
            var projected=movement.CalculateReachablePathsForNextTurn(session,actor);
            Assert.That(projected.Keys,Is.EquivalentTo(ordinary.Keys),"The pending first-turn preview must use the remaining budget too.");
            actor.CurrentActionValue=1;
            turns.EndActiveTurn(session);
            Assert.That(session.ActiveUnit,Is.SameAs(actor));
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(budget-600));Assert.That(actor.MovementStepsThisTurn,Is.EqualTo(2));
            Assert.That(actor.AvailableKnives,Is.EqualTo(2));Assert.That(ticks,Is.EqualTo(1));Assert.That(starts,Is.EqualTo(1));Assert.That(resumes,Is.EqualTo(1));
            // A repeated admission and a departure do not reopen the same start.
            Assert.That(turns.BeginEntryTurn(session,actor,Start),Is.True);Assert.That(ticks,Is.EqualTo(1));
            var saved=actor.CurrentActionPoints;
            session.IsDungeonWide=false;actor.Position=new GridPosition(99,99);
            Assert.That(DungeonCombatParticipationSystem.TryLeave(session,actor),Is.True);
            Assert.That(DungeonCombatParticipationSystem.TryJoin(session,actor,new GridPosition(1,2)),Is.True);
            Assert.That(turns.BeginEntryTurn(session,actor,Start),Is.True);
            Assert.That(actor.CurrentActionPoints,Is.EqualTo(saved));Assert.That(ticks,Is.EqualTo(1));
            // Ending that turn makes the following one genuinely new.
            session.ActiveUnit=actor;session.ActiveParticipant=session.TurnParticipants.Single(p=>p.Unit==actor);
            turns.EndActiveTurn(session);
            if(session.ActiveUnit!=actor)turns.EndActiveTurn(session);
            Assert.That(ticks,Is.EqualTo(2));Assert.That(starts,Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void RealDriverExecutesOutsiderAttackWithoutStealingActiveTurnOrRestoringSpentBudget(bool retained)
        {
            var root=new GameObject("entry driver");
            try
            {
                var host=OpeningHost(root,true);
                var active=OpeningMage("active",true,4);var enemy=OpeningMage("target",false,3);
                var session=OpeningSession(active,enemy);Assert.That(host.TryBegin(session),Is.True);
                var driver=host.GetDriver(session);driver.StartTurns(active);
                var beforeActive=session.ActiveParticipant;var activeAp=active.CurrentActionPoints;
                var actor=OpeningMage("outside");actor.HasUnfinishedCombatTurn=retained;
                actor.HasInitializedCombatResources=true;actor.MaxKnives=5;actor.AvailableKnives=1;
                actor.CurrentActionPoints=retained?1000:1250;
                Assert.That(driver.TryJoin(actor,actor.Position,beginIfIdle:false),Is.True);
                var skill=OpeningSkill("EnergyOrb");var hp=enemy.Character.CurrentHealth;
                var positions=new Dictionary<int,GridPosition?>{[0]=enemy.Position};
                var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{[0]=new[]{enemy}};
                Assert.That(new SkillExecutionSystem().CanPreview(session,actor,skill,targets,null,null,positions),Is.False,"No permanent off-turn permission.");
                var expected=actor.CurrentActionPoints-CombatActionCostCalculator.CalculateSkillCost(actor,skill);
                Assert.That(driver.TryExecuteEntrySkill(actor,skill,targets,positions,null,null),Is.True);
                Assert.That(actor.CurrentActionPoints,Is.EqualTo(expected));Assert.That(actor.AvailableKnives,Is.EqualTo(1));
                Assert.That(enemy.Character.CurrentHealth,Is.LessThan(hp));
                Assert.That(session.ActiveParticipant,Is.SameAs(beforeActive));Assert.That(session.ActiveUnit,Is.SameAs(active));
                Assert.That(active.CurrentActionPoints,Is.EqualTo(activeAp));Assert.That(session.EntryActionCaster,Is.Null);
                Assert.That(actor.HasUnfinishedCombatTurn,Is.True);
            }
            finally{Object.DestroyImmediate(root);}
        }

        [TestCase(false,false,false,false,0)] [TestCase(true,false,false,false,0)]
        [TestCase(false,true,false,false,0)] [TestCase(true,true,false,false,0)]
        [TestCase(false,false,true,false,0)]
        [TestCase(true,false,false,true,0)] [TestCase(true,true,false,true,0)]
        [TestCase(false,false,false,true,0)]
        [TestCase(true,false,false,false,1)] [TestCase(false,false,false,false,2)]
        [TestCase(false,true,false,false,3)] [TestCase(true,true,false,false,4)]
        [TestCase(true,false,false,false,5)]
        public void SceneOwnerAdmitsOutsideAttackHealQueuedActionAndAreaMerge(bool healing,bool queued,bool merge,bool supportOnly,int special)
        {
            var root=new GameObject("outside action scene");
            var encounterDefinition=ScriptableObject.CreateInstance<EncounterDefinition>();
            DungeonCombatCoordinator coordinator=null;SkillData copy=null;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            void Field(object owner,string name,object value)=>owner.GetType().GetField(name,flags).SetValue(owner,value);
            GameObject Child(string name){var child=new GameObject(name);child.transform.SetParent(root.transform);return child;}
            try
            {
                root.AddComponent<Camera>().orthographic = true;
                var camera = root.AddComponent<CombatCameraFramer>();
                var host = OpeningHost(root, true); var spawner = root.GetComponent<CombatUnitViewSpawner>();
                var game = new GameSession(GameModeState.Exploration, "opening.scene");
                var dungeon = new DungeonInstance("opening.dungeon", 7); var floor = new DungeonFloorInstance(0); var model = new DungeonFloorMap();
                for (var x = 0; x < 8; x++) for (var y = 0; y < 8; y++) model.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(model); dungeon.TryAddFloor(floor); game.TrySetActiveDungeon(dungeon);
                var map = root.AddComponent<DungeonFloorMapView>();
                var gridRoot = Child("grid"); gridRoot.AddComponent<Grid>();
                var tileObject = new GameObject("tilemap", typeof(UnityEngine.Tilemaps.Tilemap)); tileObject.transform.SetParent(gridRoot.transform);
                Field(map, "floorTilemap", tileObject.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                spawner.ConfigureDungeon(game,map);
                var party = root.AddComponent<ExplorationPartyController>();
                var playerViews = new List<ExplorationCharacterView>();
                for (var n = 0; n < 4; n++)
                {
                    var unit = OpeningMage("player" + n); game.TryRegisterCharacter(unit.Character); game.TryAddCharacterToActiveParty(unit.Character.CharacterId);
                    var view = Child(unit.Character.CharacterId).AddComponent<ExplorationCharacterView>(); view.Bind(unit.Character);
                    view.SetPosition(new Vector2(1.5f, false && n == 1 ? .5f : n + .5f)); playerViews.Add(view);
                }
                party.Initialize(game, playerViews);
                var enemies = new List<ExplorationCharacterView>(); var entries = new List<EncounterEnemyData>();
                foreach (var id in new[] { "guard", "peer" })
                {
                    var unit = OpeningMage(DungeonCombatMembershipSystem.EnemyKey("encounter", id), false);
                    var view = Child(id).AddComponent<ExplorationCharacterView>(); view.Bind(unit.Character); view.SetEnemyInstanceId(id);
                    view.SetPosition(new Vector2(3.5f, id == "guard" ? .5f : 1.5f)); enemies.Add(view);
                    var entry = new EncounterEnemyData(); SetProperty(entry,"InstanceId",id); SetProperty(entry,"CharacterDefinition",unit.Character.Definition);
                    SetProperty(entry,"ClassData",unit.Character.ClassData); entries.Add(entry);
                }
                SetProperty(encounterDefinition,"Enemies",entries);
                var encounter = new DungeonEncounterInstance("encounter",encounterDefinition,"room",new RectInt(0,0,8,8));
                dungeon.TryStorePreRolledRewards("encounter",new CombatRewardResult { EnemyRewards = new()
                    { new CombatEnemyReward { EnemyInstanceId="guard", Gold=3 }, new CombatEnemyReward { EnemyInstanceId="peer", Gold=5 } } });
                var trigger = Child("trigger").AddComponent<EncounterTriggerView>();
                trigger.Initialize(party,model,map,encounter,false?new List<ExplorationCharacterView>{enemies[0]}:enemies);
                var encounters = root.AddComponent<DungeonEncounterSpawner>();
                var triggers=(List<EncounterTriggerView>)typeof(DungeonEncounterSpawner).GetField("spawnedTriggers",flags).GetValue(encounters);
                triggers.Add(trigger);
                if(false)
                {
                    var other=new DungeonEncounterInstance("other.encounter",encounterDefinition,"room",new RectInt(0,0,8,8));
                    var otherTrigger=Child("other trigger").AddComponent<EncounterTriggerView>();
                    otherTrigger.Initialize(party,model,map,other,new List<ExplorationCharacterView>{enemies[1]});triggers.Add(otherTrigger);
                    dungeon.TryStorePreRolledRewards("other.encounter",new CombatRewardResult { EnemyRewards = new()
                        { new CombatEnemyReward { EnemyInstanceId="peer", Gold=5 } } });
                }
                var targeting = root.AddComponent<CombatTargetSelectionController>(); Field(targeting,"sandboxBootstrap",host);
                var movement = root.AddComponent<CombatMovementInputController>(); var inspect = root.AddComponent<CombatUnitInspectController>();
                var feedback = root.AddComponent<CombatGridFeedbackRenderer>();
                var follow = root.AddComponent<ExplorationCameraFollow>();
                var hud = Child("combat HUD"); hud.SetActive(false);
                coordinator = root.AddComponent<DungeonCombatCoordinator>();
                foreach (var pair in new Dictionary<string,object> { ["game"]=game,["floor"]=floor,["map"]=map,["party"]=party,["encounters"]=encounters,
                    ["combatHost"]=host,["unitViews"]=spawner,["targeting"]=targeting,["movementInput"]=movement,["inspectInput"]=inspect,
                    ["gridFeedback"]=feedback,["combatHudRoot"]=hud,["explorationHudRoot"]=Child("exploration HUD"),["combatCamera"]=camera,
                    ["explorationCamera"]=follow,["initialized"]=true }) Field(coordinator,pair.Key,pair.Value);
                party.SetCombatCoordinator(coordinator);

                Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon,encounter,out var context,true),Is.True);
                CombatSession Fight(int allyIndex,int enemyIndex)
                {
                    var ally=new CombatSessionFactory().CreateJoiningUnit(playerViews[allyIndex].RuntimeCharacter,
                        DungeonBattleGridFactory.ToBattleTraversalPosition(context,playerViews[allyIndex].DungeonPosition));
                    var enemy=new CombatSessionFactory().CreateJoiningUnit(enemies[enemyIndex].RuntimeCharacter,
                        DungeonBattleGridFactory.ToBattleTraversalPosition(context,enemies[enemyIndex].DungeonPosition),false);
                    var fight=OpeningSession(ally,enemy);fight.DungeonContext=context;
                    Assert.That(new DungeonBattleGridFactory().TryCreate(context,out var battleGrid),Is.True);fight.Grid=battleGrid;
                    dungeon.TryGetPreRolledRewards("encounter",out var loot);
                    DungeonCombatMembershipSystem.RegisterEnemy(fight,"encounter",enemies[enemyIndex].EnemyInstanceId,loot,enemy);
                    var activate=typeof(DungeonCombatCoordinator).GetMethod("ActivateBoundSession",flags);
                    Assert.That((bool)activate.Invoke(coordinator,new object[]{fight,new Dictionary<BattleUnit,ExplorationCharacterView>{[ally]=playerViews[allyIndex],[enemy]=enemies[enemyIndex]},trigger,
                        new Action<CombatExecutionDriver>(d=>d.StartTurns(ally))}),Is.True);
                    ally.CurrentActionPoints=400;return fight;
                }
                if(supportOnly)
                {
                    var unrelated=healing?null:Fight(2,0);
                    if(unrelated!=null)typeof(DungeonCombatCoordinator).GetMethod("SetFocus",flags).Invoke(coordinator,new object[]{null,true});
                    if(queued)foreach(var enemyView in enemies)enemyView.RuntimeCharacter.LifeState=CharacterLifeState.Dead;
                    ((List<DungeonEncounterInstance>)floor.GetType().GetField("encounters",flags).GetValue(floor)).Add(encounter);
                    copy=Object.Instantiate(OpeningSkill("EnergyOrb"));
                    var supportMode=new SkillTargetModeData();Field(supportMode,"targetingType",TargetingType.Allies);
                    Field(supportMode,"absoluteRange",new SkillRangeData(RangeShape.Square,0,8));
                    Field(copy,"targetModes",new List<SkillTargetModeData>{supportMode});
                    var heal=new Godmist.Data.Effects.SkillEffectData();Field(heal,"effectType",EffectType.Heal);Field(heal,"polarity",EffectPolarity.Positive);Field(heal,"baseValue",100);
                    Field(copy,"effects",new List<Godmist.Data.Effects.SkillEffectData>{heal});
                    var caster=playerViews[0].RuntimeCharacter;
                    caster.Skills[caster.Skills.FindIndex(s=>s.SkillId==copy.SkillId)]=copy;
                    playerViews[1].RuntimeCharacter.CurrentHealth=500;
                    SkillData supportSkill=copy;
                    if(queued)
                    {
                        var paladin=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset"));
                        paladin.CharacterId=caster.CharacterId;paladin.ResourcePool.CurrentValue=paladin.ResourcePool.MaxValue;
                        ((Dictionary<string,CharacterInstance>)game.GetType().GetField("charactersById",flags).GetValue(game))[paladin.CharacterId]=paladin;
                        playerViews[0].Bind(paladin);caster=paladin;
                        supportSkill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinHeal.asset");
                    }
                    var mana=caster.ResourcePool.CurrentValue;
                    Assert.That(coordinator.TrySelectOpeningSkill(supportSkill),Is.True);
                    var picked=host.Session.Units.Single(u=>u.Character==playerViews[1].RuntimeCharacter).Position;
                    Assert.That(host.TryExecuteSkill(supportSkill,picked),Is.True);
                    Assert.That(playerViews[1].RuntimeCharacter.CurrentHealth,queued?Is.GreaterThan(500):Is.EqualTo(600));
                    Assert.That(caster.ResourcePool.CurrentValue,Is.EqualTo(mana-supportSkill.ResourceCost));
                    Assert.That(game.Combats.Count,Is.EqualTo(unrelated==null?0:1));
                    Assert.That(host.Drivers.Count,Is.EqualTo(unrelated==null?0:1));Assert.That(host.IsOpeningPreview,Is.False);
                    Assert.That(game.GetCombatForCharacter(caster.CharacterId),Is.Null);
                    if(unrelated!=null)Assert.That(unrelated.ActiveUnit.CurrentActionPoints,Is.EqualTo(400));
                    return;
                }
                var first=Fight(1,0);var firstActive=first.ActiveUnit;var firstParticipant=first.ActiveParticipant;
                var second=merge?Fight(2,1):null;var secondActive=second?.ActiveUnit;
                typeof(DungeonCombatCoordinator).GetMethod("SetFocus",flags).Invoke(coordinator,new object[]{null,true});
                var skill=OpeningSkill("EnergyOrb");
                if(healing||merge)
                {
                    copy=Object.Instantiate(skill);skill=copy;
                    var mode=new SkillTargetModeData();Field(mode,"targetType",merge||special==5?TargetType.AreaAroundTile:TargetType.Single);
                    Field(mode,"targetingType",healing?TargetingType.Allies:TargetingType.Enemies);
                    Field(mode,"absoluteRange",new SkillRangeData(RangeShape.Square,0,8,1));
                    Field(mode,"relativeRange",new SkillRangeData(special==5?RangeShape.Radial:RangeShape.Square,0,2,1));
                    Field(skill,"targetModes",new List<SkillTargetModeData>{mode});
                    if(healing)
                    {
                        var effect=new Godmist.Data.Effects.SkillEffectData();Field(effect,"effectType",EffectType.Heal);
                        Field(effect,"polarity",EffectPolarity.Positive);Field(effect,"baseValue",100);
                        Field(skill,"effects",new List<Godmist.Data.Effects.SkillEffectData>{effect});
                    }
                    var character=playerViews[0].RuntimeCharacter;
                    character.Skills[character.Skills.FindIndex(s=>s.SkillId==skill.SkillId)]=skill;
                }
                var target=healing?first.Units.First(u=>u.IsPlayerSide):first.Units.First(u=>!u.IsPlayerSide);
                if(special is 1 or 2)
                {
                    var summon=OpeningMage("combat-only-summon",special==1,3);summon.Position=new GridPosition(4,2);
                    summon.IsAiControlledSummon=true;
                    Assert.That(host.GetDriver(first).TryJoin(summon,summon.Position,beginIfIdle:false),Is.True);
                    target=summon;
                }
                if(healing)target.Character.CurrentHealth=500;
                if(special==5){playerViews[3].RuntimeCharacter.CurrentHealth=0;playerViews[3].RuntimeCharacter.LifeState=CharacterLifeState.Downed;}
                var hp=target.Character.CurrentHealth;
                var driver=host.GetDriver(first);if(queued)Field(driver,"aiActionInProgress",true);
                Assert.That(coordinator.TrySelectOpeningSkill(skill),Is.True);
                Assert.That(host.Session.Units.Any(u=>u.Character==target.Character),Is.True,"Existing combat participants remain selectable.");
                var budget=host.Session.ActiveUnit.CurrentActionPoints;
                if(special==5)
                {
                    Assert.That(host.OpeningPreview.ValidateOpeningSkill(skill,new Dictionary<int,GridPosition?>{[0]=target.Position},null,null,null,null,out var selected),Is.True);
                    Assert.That(selected[0].Any(u=>u.Character==playerViews[3].RuntimeCharacter),Is.True,
                        string.Join("; ",host.Session.Units.Select(u=>$"{u.Character.CharacterId} ({u.Position.X},{u.Position.Y}) {u.Character.LifeState}")));
                }
                Assert.That(host.TryExecuteSkill(skill,target.Position),Is.True);
                if(queued)
                {
                    Assert.That(game.GetCombatForCharacter(playerViews[0].CharacterId),Is.Null);
                    Assert.That(target.Character.CurrentHealth,Is.EqualTo(hp));
                    Assert.That(host.Session.ActiveUnit.CurrentActionPoints,Is.EqualTo(budget));
                    if(special==3) target.Position=new GridPosition(7,7);
                    if(special==4)playerViews[0].RuntimeCharacter.ResourcePool.CurrentValue=0;
                    Field(driver,"aiActionInProgress",false);
                    typeof(DungeonCombatCoordinator).GetMethod("UpdateOpeningPreparation",flags).Invoke(coordinator,null);
                    if(special is 3 or 4)
                    {
                        Assert.That(game.GetCombatForCharacter(playerViews[0].CharacterId),Is.Null);
                        Assert.That(target.Character.CurrentHealth,Is.EqualTo(hp));
                        Assert.That(firstActive.CurrentActionPoints,Is.EqualTo(400));
                        Assert.That(host.IsOpeningPreview,Is.False);
                        return;
                    }
                }
                Assert.That(game.GetCombatForCharacter(playerViews[0].CharacterId),Is.SameAs(first));
                Assert.That(first.ActiveUnit,Is.SameAs(firstActive));Assert.That(first.ActiveParticipant,Is.SameAs(firstParticipant));
                Assert.That(firstActive.CurrentActionPoints,Is.EqualTo(400));
                var entrant=first.Units.Single(u=>u.Character==playerViews[0].RuntimeCharacter);
                Assert.That(entrant.CurrentActionPoints,Is.EqualTo(budget-CombatActionCostCalculator.CalculateSkillCost(entrant,skill)));
                Assert.That(entrant.HasUnfinishedCombatTurn,Is.True);
                Assert.That(target.Character.CurrentHealth,healing?Is.GreaterThan(hp):Is.LessThan(hp));
                Assert.That(host.IsOpeningPreview,Is.False);Assert.That(game.Combats.Count,Is.EqualTo(1));
                if(special==5)
                {
                    Assert.That(playerViews[3].RuntimeCharacter.LifeState,Is.EqualTo(CharacterLifeState.Alive));
                    Assert.That(playerViews[3].RuntimeCharacter.CurrentHealth,Is.GreaterThan(0));
                    Assert.That(game.GetCombatForCharacter(playerViews[3].CharacterId),Is.SameAs(first));
                }
                if(merge)
                {
                    Assert.That(secondActive.CurrentActionPoints,Is.EqualTo(400));
                    Assert.That(first.SuspendedTurns.Any(p=>p.Unit==secondActive),Is.True);
                    Assert.That(host.GetDriver(second),Is.Null);
                }
            }
            finally
            {
                if(coordinator!=null)Field(coordinator,"initialized",false);
                Object.DestroyImmediate(root);Object.DestroyImmediate(encounterDefinition);if(copy!=null)Object.DestroyImmediate(copy);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void EntryRejectsUnfundedRetainedActionAndLethalDotCannotCast(bool lethalDot)
        {
            var root=new GameObject("entry rejection");
            try
            {
                var host=OpeningHost(root,true);var active=OpeningMage("active",true,4);var enemy=OpeningMage("enemy",false,3);
                var session=OpeningSession(active,enemy);Assert.That(host.TryBegin(session),Is.True);
                var driver=host.GetDriver(session);driver.StartTurns(active);
                var actor=OpeningMage("entrant");actor.HasUnfinishedCombatTurn=!lethalDot;actor.CurrentActionPoints=lethalDot?1250:100;
                actor.HasInitializedCombatResources=true;actor.AvailableKnives=1;actor.MaxKnives=5;
                var poison=new Godmist.Runtime.Effects.PassiveEffectRuntime {EffectId=Godmist.Core.Constants.StatusEffectIds.Poison,
                    Lifetime=PassiveEffectLifetime.Timed,TickPhase=PassiveEffectTickPhase.TurnStart,RemainingDuration=3,TotalDamage=300};
                actor.Character.PassiveEffects.Add(poison);if(lethalDot)actor.Character.CurrentHealth=1;
                Assert.That(driver.TryJoin(actor,actor.Position,beginIfIdle:false),Is.True);
                var enemyHp=enemy.Character.CurrentHealth;var mana=actor.Character.ResourcePool.CurrentValue;
                var groups=new Dictionary<int,IReadOnlyList<BattleUnit>>{[0]=new[]{enemy}};
                Assert.That(driver.TryExecuteEntrySkill(actor,OpeningSkill("EnergyOrb"),groups,
                    new Dictionary<int,GridPosition?>{[0]=enemy.Position},null,null),Is.False);
                Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(enemyHp));Assert.That(session.ActiveUnit,Is.SameAs(active));
                Assert.That(session.EntryActionCaster,Is.Null);Assert.That(actor.AvailableKnives,Is.EqualTo(1));
                if(lethalDot)Assert.That(actor.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
                else
                {
                    Assert.That(actor.CurrentActionPoints,Is.EqualTo(100));Assert.That(poison.TotalDamage,Is.EqualTo(300));
                    Assert.That(actor.Character.ResourcePool.CurrentValue,Is.EqualTo(mana));
                }
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void KillingCurrentEnemyWithEntryActionDoesNotAdvanceInsideSkillResolution()
        {
            var root=new GameObject("entry lethal hit");
            try
            {
                var host=OpeningHost(root,true);var active=OpeningMage("enemy",false,3);var ally=OpeningMage("ally",true,4);
                active.Character.CurrentHealth=1;
                var session=OpeningSession(active,ally);Assert.That(host.TryBegin(session),Is.True);
                var driver=host.GetDriver(session);
                foreach(var u in session.Units)session.TurnParticipants.Add(new CombatTurnParticipant(u));
                session.ActiveParticipant=session.TurnParticipants[0];session.ActiveUnit=active;active.HasUnfinishedCombatTurn=true;
                var actor=OpeningMage("entrant");Assert.That(driver.TryJoin(actor,actor.Position,beginIfIdle:false),Is.True);
                var groups=new Dictionary<int,IReadOnlyList<BattleUnit>>{[0]=new[]{active}};
                Assert.That(driver.TryExecuteEntrySkill(actor,OpeningSkill("EnergyOrb"),groups,
                    new Dictionary<int,GridPosition?>{[0]=active.Position},null,null),Is.True);
                Assert.That(active.Character.LifeState,Is.EqualTo(CharacterLifeState.Dead));
                Assert.That(session.EntryActionCaster,Is.Null);Assert.That(actor.HasUnfinishedCombatTurn,Is.True);
                Assert.That(session.ActiveUnit,Is.Not.SameAs(active));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
