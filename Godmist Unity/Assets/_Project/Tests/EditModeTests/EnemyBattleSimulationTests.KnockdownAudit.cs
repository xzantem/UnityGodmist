using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string KnockdownFolder=>Path.GetFullPath("../LocalHandoffs/knockdown-attribution-20261019");
        sealed partial class Engine
        {
            internal CombatDamageSystem KnockdownTestDamage=>damage;
            internal CombatRecoverySystem KnockdownTestRecovery=>recovery;
        }
        [Serializable] sealed class KnockdownEvent
        {
            public int sequence,turn,hp,amount,readyTurns,actions,link=-1,ordinal;
            public string kind,unit,source,sourceDefinition,type,cause,effect,active,actionId,skill;
            public bool selfBurn;
            public string[] origins;
        }
        // Opt-in event ledger. Reads live state; never rolls RNG or modifies combat.
        // Actual HP loss comes from Damage feedback, not HP+shield DamageApplied.
        [Serializable] sealed class KnockdownTrace
        {
            public List<KnockdownEvent> events=new();
            CombatSession session;Result result;
            readonly Dictionary<BattleUnit,CharacterLifeState> life=new();
            readonly Dictionary<BattleUnit,int> ready=new(),actions=new(),downs=new(),lastDamage=new();
            readonly Dictionary<BattleUnit,string> actionIds=new();
            readonly Dictionary<string,string> skillIds=new();
            internal BattleUnit tickingUnit;
            internal PassiveEffectRuntime tickingEffect;
            public void Attach(CombatSession value,Result valueResult)
            {
                session=value;result=valueResult;
                foreach(var unit in session.Units.Where(u=>u.IsPlayerSide))
                {life[unit]=unit.Character.LifeState;Add("initial",unit);}
            }
            KnockdownEvent Add(string kind,BattleUnit unit,BattleUnit source=null,int amount=0,DamageType? type=null)
            {
                var value=new KnockdownEvent{sequence=events.Count,turn=result.turns,kind=kind,
                    unit=unit.Character.CharacterId,hp=unit.Character.CurrentHealth,amount=amount,
                    source=source?.Character.CharacterId,sourceDefinition=source?.Character.Definition.Id,
                    type=type?.ToString(),active=session.ActiveUnit?.Character.CharacterId,
                    readyTurns=ready.GetValueOrDefault(unit),actions=actions.GetValueOrDefault(unit)};
                // A processor can trigger other effects; only its own null-source
                // hit is attributed to the currently ticking DoT.
                if(source==null&&unit==tickingUnit&&tickingEffect!=null&&type.HasValue)
                {
                    value.effect=tickingEffect.EffectId;
                    value.selfBurn=tickingEffect.GetStateValue("mage.burn.self")?.BooleanValue==true;
                    value.origins=tickingEffect.Sources.Select(s=>s.SourceCharacterId).Distinct().ToArray();
                    value.cause=value.selfBurn&&value.origins.Length>0&&value.origins.All(id=>id==value.unit)
                        ?"self-burn":value.origins.Any(id=>id==value.unit)?"mixed-or-self-dot":"other-dot";
                }
                else value.cause=source==null?"unattributed":source.IsPlayerSide?"player":"enemy";
                events.Add(value);return value;
            }
            public void Observe(BattleUnit manualTarget=null,BattleUnit helper=null)
            {
                foreach(var unit in life.Keys.ToArray())
                {
                    var before=life[unit];var next=unit.Character.LifeState;
                    if(before==next)continue;
                    if(before==CharacterLifeState.Alive&&next!=before)
                    {
                        var entry=Add("down",unit);entry.ordinal=downs.GetValueOrDefault(unit)+1;downs[unit]=entry.ordinal;
                        entry.link=lastDamage.GetValueOrDefault(unit,-1);
                    }
                    else if(before==CharacterLifeState.Downed&&next==CharacterLifeState.Alive)
                        Add(unit==manualTarget?"manual-revive":"heal-revive",unit,unit==manualTarget?helper:null);
                    else Add("life-"+next,unit);
                    life[unit]=next;
                }
            }
            public void Applied(BattleUnit source,BattleUnit target,int amount,DamageType type,string actionId)
            {
                if(!target.IsPlayerSide)return;
                actionIds[target]=actionId;var value=Add("applied",target,source,amount,type);value.actionId=actionId;
            }
            public void Feedback(CombatFeedbackEvent feedback)
            {
                if(feedback.Target?.IsPlayerSide!=true)return;
                Observe();
                var kind=feedback.Type==CombatFeedbackType.Damage?"hp-damage":feedback.Type==CombatFeedbackType.ShieldLoss?"shield-loss":
                    feedback.Type==CombatFeedbackType.Heal?"heal":feedback.Type==CombatFeedbackType.ShieldGain?"shield-gain":null;
                if(kind==null)return;
                var value=Add(kind,feedback.Target,feedback.Source,feedback.Amount,feedback.DamageType);
                if(kind=="heal")value.effect=feedback.EffectName;
                if(kind=="hp-damage")
                {
                    value.actionId=actionIds.GetValueOrDefault(feedback.Target);
                    lastDamage[feedback.Target]=value.sequence;
                }
            }
            public void ManualRevive(BattleUnit actor,BattleUnit fallen)=>Observe(fallen,actor);
            public void TurnStart(BattleUnit actor){if(actor.IsPlayerSide){Observe();Add("turn-start",actor);}}
            public void Ready(BattleUnit actor)
            {
                if(!actor.IsPlayerSide)return;
                ready[actor]=ready.GetValueOrDefault(actor)+1;Add(StatusEffectSystem.IsHardCrowdControlled(actor)?"ready-cc":"ready",actor);
            }
            public void Action(BattleUnit actor)
            {if(actor.IsPlayerSide){actions[actor]=actions.GetValueOrDefault(actor)+1;Add("action",actor);}}
            public void Skill(BattleUnit actor,SkillData skill,string id)
            {if(!string.IsNullOrEmpty(id))skillIds[id]=skill.SkillId;}
            public void Verify(Result value)
            {
                Observe();
                foreach(var entry in events.Where(e=>!string.IsNullOrEmpty(e.actionId)))entry.skill=skillIds.GetValueOrDefault(entry.actionId);
                Assert.That(events.Count(e=>e.kind=="down"),Is.EqualTo(value.downings),"Every reported downing must have a ledger transition.");
                Assert.That(events.Count(e=>e.kind=="manual-revive"),Is.EqualTo(value.heroRevives),"Every help-up must be identified separately from healing.");
                Assert.That(events.Where(e=>e.kind=="applied").Sum(e=>e.amount),Is.EqualTo(value.heroDamageTaken));
                foreach(var entry in events.Where(e=>e.kind=="down"))
                    Assert.That(entry.link,Is.GreaterThanOrEqualTo(0),"No downing may silently lose its fatal damage source.");
            }
        }
        // The real production processor still performs every tick and mutation.
        sealed class TracedDotProcessor : IPassiveEffectTurnProcessor
        {
            readonly DamageOverTimePassiveEffectProcessor owner;readonly KnockdownTrace trace;
            public TracedDotProcessor(CombatDamageSystem damage,KnockdownTrace value)
            {owner=new DamageOverTimePassiveEffectProcessor(damage);trace=value;}
            public bool CanProcess(PassiveEffectRuntime effect,PassiveEffectTickPhase phase)=>owner.CanProcess(effect,phase);
            public bool ProcessTurn(CombatSession session,BattleUnit unit,PassiveEffectRuntime effect,out bool shouldExpire)
            {
                var previousUnit=trace.tickingUnit;var previousEffect=trace.tickingEffect;
                trace.tickingUnit=unit;trace.tickingEffect=effect;
                try{return owner.ProcessTurn(session,unit,effect,out shouldExpire);}
                finally{trace.tickingUnit=previousUnit;trace.tickingEffect=previousEffect;}
            }
        }
        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("Observe exact archived L48 EQ50 full expeditions without changing decisions or statistics.")]
        public void AuditKnockdownAttribution(string biome,int firstSeed)
            =>RunRealisticExpeditions(48,.5f,biome,DungeonLength.Normal,0,firstSeed+4,
                $"trace-L48-U50-{biome}-S{firstSeed}",firstSeed:firstSeed,outputFolder:KnockdownFolder,fullOnly:true,traceKnockdowns:true,previousRescuePolicy:true);

        [Test]
        public void KnockdownLedgerDistinguishesOwnBurnEnemyHitAndOneHpRevive()
        {
            var mage=PolicyHero(2,true,3,4);var helper=PolicyHero(0,true,2,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,helper,enemy}};
            var result=new Result();var ledger=new KnockdownTrace();
            using var engine=new Engine(session,result,ledger);
            PolicyActivate(session,helper);
            engine.KnockdownTestDamage.ApplyDamage(session,enemy,mage,100000,DamageType.Physical);
            Assert.That(engine.PlayerAction(helper),Is.True);
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(1));
            var burn=new PassiveEffectRuntime{EffectId="burn",RemainingDuration=1,TotalDamage=100,
                Sources=new(){new PassiveEffectSourceRuntime{SourceCharacterId=mage.Character.CharacterId}}};
            burn.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("mage.burn.self",true));
            var random=UnityEngine.Random.state;
            new TracedDotProcessor(engine.KnockdownTestDamage,ledger).ProcessTurn(session,mage,burn,out _);
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random),"Observation must not roll RNG.");
            ledger.Verify(result);
            var downs=ledger.events.Where(e=>e.kind=="down").ToArray();
            Assert.That(downs.Length,Is.EqualTo(2));
            Assert.That(ledger.events[downs[0].link].cause,Is.EqualTo("enemy"));
            Assert.That(ledger.events[downs[1].link].cause,Is.EqualTo("self-burn"));
            Assert.That(ledger.events[downs[1].link].amount,Is.EqualTo(1),"HP loss is capped at real remaining HP, not nominal DoT power.");
            Assert.That(ledger.events.Count(e=>e.kind=="manual-revive"),Is.EqualTo(1));
        }
        [Test]
        public void KnockdownLedgerSeparatesHealingReviveAndShieldDamage()
        {
            var mage=PolicyHero(2,true,3,4);var helper=PolicyHero(0,true,2,4);var enemy=PolicyHero(0,false,10,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){mage,helper,enemy}};
            var result=new Result();var ledger=new KnockdownTrace();using var engine=new Engine(session,result,ledger);
            engine.KnockdownTestDamage.ApplyDamage(session,enemy,mage,100000,DamageType.Physical);
            engine.KnockdownTestRecovery.Heal(mage,100);
            ledger.Observe();ledger.Verify(result);
            Assert.That(ledger.events.Single(e=>e.kind=="heal-revive").hp,Is.EqualTo(100));
            Assert.That(ledger.events.Count(e=>e.kind=="manual-revive"),Is.Zero);
            var first=ledger.events.Count;
            new CombatShieldSystem().GrantShield(mage,50);
            engine.KnockdownTestDamage.ApplyDamage(session,enemy,mage,60,DamageType.Physical);
            var split=ledger.events.Skip(first).ToArray();
            Assert.That(split.Where(e=>e.kind=="shield-loss").Sum(e=>e.amount),Is.EqualTo(50));
            Assert.That(split.Where(e=>e.kind=="applied").Sum(e=>e.amount),
                Is.EqualTo(split.Where(e=>e.kind=="shield-loss"||e.kind=="hp-damage").Sum(e=>e.amount)));
        }
    }
}
