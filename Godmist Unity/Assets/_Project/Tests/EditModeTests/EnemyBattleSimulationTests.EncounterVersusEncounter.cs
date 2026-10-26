using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string EncounterVsFolder=>Path.GetFullPath("../LocalHandoffs/encounter-versus-encounter-20261019");
        [Serializable] sealed class EncounterVsUnit
        {
            public string instance,id,resource;public bool forest,summon,active;
            public int startHp,hp,initialResource,maxResource,startX,startY,turns,aiCalls,steps,ccTurns,healing,hpDamage,resourceGained;
            public float atk;public string life;public List<SkillCount> skills=new();
        }
        [Serializable] sealed class EncounterVsRow
        {
            public string forest,catacombs,winner,stop;public int gap,seed,turns,actions,objectTurns,objectsCreated;
            public bool forestLeft;public float forestHp,catacombsHp;public List<EncounterVsUnit> units=new();
        }
        [Serializable] sealed class EncounterVsReport
        {
            public string method="Actual regular7forest x8catacombs rosters, L48, fullHP, standard resources, authored ranges9/8 (no range7 candidate). Production NPC AI on BOTH teams.8paired seeds, mirrored sides, front-line gaps4/8.32x14 open arena; ranged/support units2cells behind melee, authored order retained within rows. Real summons, traps, status, AP/AV, costs and resource rules. NPCs die at0HP; no revive. Each battle fresh; no hero policy/gear/trees/carryover.";
            public List<EncounterVsRow> battles=new();
        }
        sealed partial class Engine
        {
            internal readonly Dictionary<BattleUnit,bool> EncounterForest=new();
            internal Action<BattleUnit> AuditEncounterRegister;
            internal Action<BattleUnit> AuditEncounterAiCall;
            bool encounterRemoving;
            internal void RegisterEncounterUnits()
            {
                foreach(var unit in Session.Units.ToArray())
                {
                    if(!EncounterForest.ContainsKey(unit))
                    {
                        var summon=Session.Summons.FirstOrDefault(s=>s.Unit==unit);
                        Assert.That(summon,Is.Not.Null,"Every new participant must have an actual summon owner.");
                        var creator=EncounterForest.Keys.Single(u=>u.Character.CharacterId==summon.CreatorCharacterId);
                        EncounterForest[unit]=EncounterForest[creator];
                    }
                    AuditEncounterRegister?.Invoke(unit);
                }
            }
            internal void NormalizeEncounterSide(bool actingForest)
            {
                RegisterEncounterUnits();
                foreach(var unit in Session.Units)unit.IsPlayerSide=EncounterForest[unit]!=actingForest;
            }
            internal void EvaluateNpcEncounter(CombatSession session)
            {
                RegisterEncounterUnits();
                if(encounterRemoving)return;
                encounterRemoving=true;
                try
                {
                    // Actor-side relabeling must not grant hero knockdown/revive
                    // rules to the other NPC team. Match CombatDamageSystem.MarkDead
                    // cleanup, using the same production helpers and Remove owner.
                    foreach(var unit in session.Units.Where(u=>u.Character.LifeState==CharacterLifeState.Downed).ToArray())
                    {
                        ScoutCombatSystem.ReleaseKnivesOnDeath(session,unit);
                        unit.Character.CurrentHealth=0;unit.Character.LifeState=CharacterLifeState.Dead;
                        unit.DownedTurnsRemaining=0;unit.IsPlayerSide=false;
                        StatusEffectSystem.RemoveTauntsAppliedBy(session,unit.Character.CharacterId);
                        session.TryMarkEnemyDefeated(unit.Character.CharacterId,unit.Position);
                        Remove(unit);
                    }
                }
                finally{encounterRemoving=false;}
                var forest=session.Units.Any(u=>u.Character.LifeState==CharacterLifeState.Alive&&EncounterForest[u]);
                var cat=session.Units.Any(u=>u.Character.LifeState==CharacterLifeState.Alive&&!EncounterForest[u]);
                if(!forest||!cat)session.Outcome=forest?CombatOutcomeState.Victory:CombatOutcomeState.Defeat;
            }
            internal void StartNpcEncounter(Dictionary<BattleUnit,bool> teams)
            {
                foreach(var pair in teams)EncounterForest[pair.Key]=pair.Value;
                AuditNpcDuelOutcome=EvaluateNpcEncounter;
                EvaluateNpcEncounter(Session);
            }
            internal void RunNpcEncounter(Dictionary<BattleUnit,bool> teams,EncounterVsRow row)
            {
                StartNpcEncounter(teams);
                var clock=System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    while(Session.Outcome==CombatOutcomeState.Ongoing&&Result.turns<600&&Result.actions<2400&&clock.Elapsed.TotalSeconds<20)
                    {
                        EvaluateNpcEncounter(Session);if(Session.Outcome!=CombatOutcomeState.Ongoing)break;
                        Result.turns++;
                        if(Session.ActiveSummonedObject is {} item)
                        {
                            var owner=EncounterForest.Keys.SingleOrDefault(u=>u.Character.CharacterId==item.CreatorCharacterId);
                            if(owner!=null)NormalizeEncounterSide(EncounterForest[owner]);
                            row.objectTurns++;objects.ExecuteTurnEffects(Session,item);
                            EvaluateNpcEncounter(Session);
                            if(Session.Outcome==CombatOutcomeState.Ongoing)turns.EndActiveTurn(Session);
                            continue;
                        }
                        var actor=Session.ActiveUnit;
                        if(actor?.Character.LifeState!=CharacterLifeState.Alive)
                        {if(Session.Outcome==CombatOutcomeState.Ongoing)turns.AdvanceTurn(Session);continue;}
                        NormalizeEncounterSide(EncounterForest[actor]);AuditUnitTurn?.Invoke(actor);
                        if(!StatusEffectSystem.IsHardCrowdControlled(actor))
                        {
                            var state=ai.CreateTurnState();
                            for(var action=0;action<24&&Session.Outcome==CombatOutcomeState.Ongoing&&Session.ActiveUnit==actor;action++)
                            {
                                AuditEncounterAiCall?.Invoke(actor);
                                using var job=ai.CreateNextActionJob(Session,actor,state,timeoutSeconds:1);
                                var steps=0;
                                while(!job.IsComplete&&steps++<2000)job.AdvanceSlice(2,2000);
                                if(job.HasTimedOut||!job.IsComplete){Result.stop="AI budget";break;}
                                if(!job.Executed)break;
                                Result.actions++;EvaluateNpcEncounter(Session);
                            }
                        }
                        EvaluateNpcEncounter(Session);
                        if(Result.stop!=null||Session.Outcome!=CombatOutcomeState.Ongoing)break;
                        if(Session.ActiveParticipant==null)turns.AdvanceTurn(Session);else turns.EndActiveTurn(Session);
                    }
                    if(Session.Outcome==CombatOutcomeState.Ongoing&&Result.stop==null)Result.stop="encounter budget";
                }
                finally
                {
                    foreach(var pair in EncounterForest)pair.Key.IsPlayerSide=pair.Value;
                    AuditNpcDuelOutcome=null;
                }
            }
            internal BattleUnit AuditEncounterSpawn(BattleUnit caster,SkillData skill,GridPosition cell)
            {
                var effect=skill.Effects.Single(e=>e.EffectType==EffectType.Summon);
                Assert.That(summons.TrySummon(Session,caster,skill,effect,0,cell,out var result),Is.True);
                RegisterEncounterUnits();return result;
            }
        }
        static List<EncounterDefinition> EncounterVsDefinitions(string biome)
            =>PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition").EncounterDefinitions.OrderBy(e=>e.Id,StringComparer.Ordinal).ToList();

        EncounterVsRow EncounterVersus(EncounterDefinition forest,EncounterDefinition cat,int gap,int seed,bool forestLeft)
        {
            var row=new EncounterVsRow{forest=forest.Id,catacombs=cat.Id,gap=gap,seed=seed,forestLeft=forestLeft};
            var session=new CombatSession{Grid=new BattleGrid(32,14)};var sides=new Dictionary<BattleUnit,bool>();
            var originals=new HashSet<BattleUnit>();
            void Add(EncounterDefinition definition,bool isForest)
            {
                var left=isForest==forestLeft;var front=left?(31-gap)/2:(31-gap)/2+gap;
                for(var i=0;i<definition.Enemies.Count;i++)
                {
                    var entry=definition.Enemies[i];var c=new CharacterInstanceFactory().Create(entry.CharacterDefinition,entry.ClassData,48);
                    c.CharacterId=$"encounter-{(isForest?"forest":"cat")}-{i}";
                    var basic=c.GetBasicAttackSkill();
                    var ranged=basic.TargetModes.Any(m=>m.TargetingType==TargetingType.Enemies&&m.AbsoluteRange.MaxRange>1);
                    var u=new BattleUnit{Character=c,IsPlayerSide=isForest,Position=new GridPosition(front+(ranged?(left?-2:2):0),4+i)};
                    session.Units.Add(u);sides[u]=isForest;originals.Add(u);
                }
            }
            if(forestLeft){Add(forest,true);Add(cat,false);}else{Add(cat,false);Add(forest,true);}
            Assert.That(session.Units.Select(u=>u.Position).Distinct().Count(),Is.EqualTo(session.Units.Count));
            var result=new Result();using var engine=new Engine(session,result);
            var tracked=new Dictionary<BattleUnit,EncounterVsUnit>();
            EncounterVsUnit Get(BattleUnit u)
            {
                if(tracked.TryGetValue(u,out var value))return value;
                var p=u.Character.ResourcePool;
                value=new EncounterVsUnit{instance=u.Character.CharacterId,id=u.Character.Definition.Id,forest=originals.Contains(u)?sides[u]:engine.EncounterForest[u],summon=!originals.Contains(u),
                    startHp=u.Character.CurrentHealth,atk=(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f,
                    initialResource=p.CurrentValue,maxResource=p.MaxValue,resource=p.ResourceType.ToString(),startX=u.Position.X,startY=u.Position.Y};
                tracked[u]=value;row.units.Add(value);return value;
            }
            engine.AuditEncounterRegister=u=>Get(u);
            foreach(var unit in session.Units)Get(unit);
            var observedHealing=new HashSet<BattleUnit>();
            void Track(BattleUnit u)
            {
                var value=Get(u);
                if(observedHealing.Add(u))u.Character.HealingReceived+=(amount,origin,bus)=>value.healing+=amount;
            }
            engine.AuditEncounterRegister=Track;
            foreach(var unit in session.Units)Track(unit);
            session.UnitEnteredCell+=u=>Get(u).steps++;
            session.SkillResolved+=(u,skill,id)=>
            {
                engine.RegisterEncounterUnits();var value=Get(u);var count=value.skills.FirstOrDefault(s=>s.id==skill.SkillId);
                if(count==null){count=new SkillCount{id=skill.SkillId};value.skills.Add(count);}count.count++;
                row.objectsCreated=Math.Max(row.objectsCreated,session.SummonedObjects.Count);
            };
            engine.AuditUnitTurn=u=>{Get(u).turns++;if(StatusEffectSystem.IsHardCrowdControlled(u))Get(u).ccTurns++;};
            engine.AuditEncounterAiCall=u=>{Assert.That(u.IsPlayerSide,Is.False);Get(u).aiCalls++;};
            engine.AuditFeedback=e=>
            {
                if(e.Target==null)return;
                if(e.Type==CombatFeedbackType.Damage)Get(e.Target).hpDamage+=e.Amount;
                if(e.Type==CombatFeedbackType.ResourceGain)Get(e.Target).resourceGained+=e.Amount;
            };
            engine.RunNpcEncounter(sides,row);
            foreach(var pair in tracked){pair.Value.hp=pair.Key.Character.CurrentHealth;pair.Value.life=pair.Key.Character.LifeState.ToString();pair.Value.active=session.Units.Contains(pair.Key);}
            var alive=session.Units.Where(u=>u.Character.LifeState==CharacterLifeState.Alive).ToArray();
            row.winner=alive.Any(u=>engine.EncounterForest[u])?"forest":alive.Any(u=>!engine.EncounterForest[u])?"catacombs":"draw";
            row.turns=result.turns;row.actions=result.actions;row.stop=result.stop;
            if(row.stop!=null)row.winner="unresolved";
            row.forestHp=originals.Where(u=>sides[u]).Sum(u=>u.Character.CurrentHealth)/(float)row.units.Where(u=>u.forest&&!u.summon).Sum(u=>u.startHp);
            row.catacombsHp=originals.Where(u=>!sides[u]).Sum(u=>u.Character.CurrentHealth)/(float)row.units.Where(u=>!u.forest&&!u.summon).Sum(u=>u.startHp);
            Assert.That(row.units.Any(u=>u.life=="Downed"),Is.False,"NPCs cannot retain hero knockdown states.");
            return row;
        }
        [TestCase(0,4)][TestCase(1,4)][TestCase(2,4)][TestCase(3,4)][TestCase(4,4)][TestCase(5,4)][TestCase(6,4)][TestCase(7,4)]
        [TestCase(0,8)][TestCase(1,8)][TestCase(2,8)][TestCase(3,8)][TestCase(4,8)][TestCase(5,8)][TestCase(6,8)][TestCase(7,8)]
        [Explicit("All56 regular roster pairs; authored stats/ranges and the same production AI for both complete teams.")]
        public void AuditEncounterVersusEncounter(int catIndex,int gap)
        {
            var report=new EncounterVsReport();var random=UnityEngine.Random.state;
            var cat=EncounterVsDefinitions("catacombs")[catIndex];Directory.CreateDirectory(EncounterVsFolder);
            try
            {
                foreach(var forest in EncounterVsDefinitions("forest"))
                {
                    for(var seed=0;seed<8;seed++)foreach(var mirror in new[]{false,true})
                    {
                        UnityEngine.Random.InitState(719026+seed);
                        var row=EncounterVersus(forest,cat,gap,seed,mirror);report.battles.Add(row);
                        Assert.That(row.stop,Is.Not.EqualTo("AI budget"));
                    }
                    File.WriteAllText(Path.Combine(EncounterVsFolder,$"battles-C{catIndex}-G{gap}.json"),JsonUtility.ToJson(report,true));
                }
                Assert.That(report.battles.Count,Is.EqualTo(112));
            }
            finally{UnityEngine.Random.state=random;}
        }
        [Test]
        public void EncounterVersusNpcDeathsAndSummonsKeepStableFactionAcrossSideChanges()
        {
            var druid=PointUnit(new CharacterInstanceFactory().Create(PointEnemyDefinitions.Single(d=>d.Id=="enemy.forest.elf_druid"),null,48),true);druid.Character.CharacterId="test-druid";druid.Position=new GridPosition(4,4);
            var ally=RecoveryArcher(4,5);ally.IsPlayerSide=true;
            var enemy=PointUnit(new CharacterInstanceFactory().Create(PointEnemyDefinitions.Single(d=>d.Id=="enemy.catacombs.swordsman"),null,48),false);enemy.Character.CharacterId="test-cat";enemy.Position=new GridPosition(12,4);
            var session=new CombatSession{Grid=new BattleGrid(24,12),Units=new(){druid,ally,enemy}};
            using var engine=new Engine(session,new Result());
            var teams=new Dictionary<BattleUnit,bool>{{druid,true},{ally,true},{enemy,false}};
            engine.StartNpcEncounter(teams);engine.NormalizeEncounterSide(true);
            var skill=druid.Character.GetActiveSkills().Single(s=>s.SkillId=="skill.enemy.forest.druid_healing_growth");
            var random=UnityEngine.Random.state;
            try
            {
                var growth=engine.AuditEncounterSpawn(druid,skill,new GridPosition(5,4));
                Assert.That(engine.EncounterForest[growth],Is.True);Assert.That(growth.IsPlayerSide,Is.False);
                engine.NormalizeEncounterSide(false);Assert.That(growth.IsPlayerSide,Is.True);Assert.That(druid.IsPlayerSide,Is.True);Assert.That(enemy.IsPlayerSide,Is.False);
                ally.Character.CurrentHealth=0;ally.Character.LifeState=CharacterLifeState.Downed;
                engine.EvaluateNpcEncounter(session);
                Assert.That(ally.Character.LifeState,Is.EqualTo(CharacterLifeState.Dead));Assert.That(session.Units.Contains(ally),Is.False);
                Assert.That(session.Outcome,Is.EqualTo(CombatOutcomeState.Ongoing));
                druid.Character.CurrentHealth=0;druid.Character.LifeState=CharacterLifeState.Downed;engine.EvaluateNpcEncounter(session);
                Assert.That(session.Units.Contains(growth),Is.False,"Actual summon cleanup must remove the dead creator's growth.");
                Assert.That(session.Outcome,Is.EqualTo(CombatOutcomeState.Defeat));
            }
            finally{UnityEngine.Random.state=random;}
        }
        [Test]
        public void EncounterVersusBothTeamsUseAiWithRealSupportAndNoHeroRescue()
        {
            var random=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(719026);
                var row=EncounterVersus(EncounterVsDefinitions("forest").Single(e=>e.Id.Contains("bandit_patrol")),EncounterVsDefinitions("catacombs").Single(e=>e.Id.Contains("bone_line")),4,0,true);
                Assert.That(row.stop,Is.Null.Or.Empty);Assert.That(row.units.Where(u=>u.forest).Sum(u=>u.aiCalls),Is.GreaterThan(0));Assert.That(row.units.Where(u=>!u.forest).Sum(u=>u.aiCalls),Is.GreaterThan(0));
                Assert.That(row.units.Count(u=>!u.summon),Is.EqualTo(8));
            }
            finally{UnityEngine.Random.state=random;}
        }
        [TestCase("forest",0)][TestCase("catacombs",6)]
        [Explicit("Default party-v6 must remain identical; group harness is opt-in and test-only.")]
        public void AuditEncounterVersusDefaultControl(string biome,int seed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"control-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(EncounterVsFolder,"controls"),fullOnly:true,traceKnockdowns:true);
    }
}
