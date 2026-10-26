using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
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
        sealed partial class Engine
        {
            internal bool CastProbe(BattleUnit caster,SkillData skill,BattleUnit target)
            {
                turns.InitializeTurnOrder(Session,caster);
                Assert.That(Session.ActiveUnit,Is.SameAs(caster));
                return skills.TryExecute(Session,caster,skill,target);
            }
            internal void TickProbeTarget(BattleUnit target)
            {
                var guard=0;
                while(Session.Outcome==CombatOutcomeState.Ongoing&&Session.ActiveUnit!=target&&guard++<30)
                    turns.EndActiveTurn(Session);
                Assert.That(guard,Is.LessThan(30),"Target must get a real scheduled turn.");
                if(Session.Outcome==CombatOutcomeState.Ongoing&&Session.ActiveUnit==target)turns.EndActiveTurn(Session);
            }
        }
        [Serializable] sealed class FireballProbeRow
        {
            public string enemy;public float upgrade;public int trials,health,directKills,eventualKills,burnApplied;
            public float meanDirectDamage,meanBurnDamage,meanManaSpent;
        }
        [Serializable] sealed class FireballProbeReport
        {
            public string policy="L1 mage, no SP, mean Common Normal weapon/armor, no jewelry. One actual Fireball, then only target turns until burn expires; no further attacks, no allied damage or enemy actions. Direct and delayed one-cast lethality reported separately. Production accuracy, critical, resistance, conditional status and DoT rules. 128 independent seeded trials per enemy/upgrade; paired seeds between upgrades.";
            public List<FireballProbeRow> rows=new();
        }
        [TestCase(0f),TestCase(.5f),TestCase(1f),Explicit("Opt-in L1 one-Fireball lethality analysis.")]
        public void AuditFireballOneCast(float upgrade)
        {
            Directory.CreateDirectory(RealisticFolder);var report=new FireballProbeReport();
            var randomState=UnityEngine.Random.state;
            try
            {
                foreach(var definition in PointEnemyDefinitions)
                {
                    var row=new FireballProbeRow{enemy=definition.Id,upgrade=upgrade,trials=128};
                    for(var seed=0;seed<row.trials;seed++)
                    {
                        UnityEngine.Random.InitState(901018+seed);
                        var gear=PointInput.rows.Single(r=>r.level==1&&r.hero==2&&r.weaponTier==0&&r.armorTier==0);
                        var mage=PointHero(gear,-1,0,upgrade,PointEnemies(1));mage.CharacterId="fireball-mage";
                        var target=new CharacterInstanceFactory().Create(definition,definition.ClassData,1);target.CharacterId="fireball-target";
                        row.health=target.CurrentHealth;
                        var caster=new BattleUnit{Character=mage,IsPlayerSide=true,Position=new GridPosition(2,2)};
                        var enemy=new BattleUnit{Character=target,Position=new GridPosition(4,2)};
                        var session=new CombatSession{Grid=new BattleGrid(8,8)};session.Units.Add(caster);session.Units.Add(enemy);
                        var fireball=mage.GetKnownSkills().Single(s=>s.SkillId=="skill.mage.fireball");
                        var direct=0;var burn=0;var result=new Result();
                        session.DamageApplied+=(source,victim,amount,type,id)=>
                        {if(victim==enemy){if(type==DamageType.Burn)burn+=amount;else direct+=amount;}};
                        using(var engine=new Engine(session,result))
                        {
                            Assert.That(engine.CastProbe(caster,fireball,enemy),Is.True,"Exactly one legal cast required.");
                            if(target.LifeState!=CharacterLifeState.Alive)row.directKills++;
                            bool Burning()=>target.PassiveEffects.Any(e=>e.EffectId=="burn"&&PassiveEffectLifecycleSystem.IsActive(e));
                            if(Burning())row.burnApplied++;
                            var turns=0;
                            while(target.LifeState==CharacterLifeState.Alive&&Burning()&&turns++<12)engine.TickProbeTarget(enemy);
                            Assert.That(target.LifeState!=CharacterLifeState.Alive||!Burning(),Is.True,"All burn damage must be resolved.");
                            if(target.LifeState!=CharacterLifeState.Alive)row.eventualKills++;
                        }
                        row.meanDirectDamage+=direct;row.meanBurnDamage+=burn;row.meanManaSpent+=result.heroManaSpent;
                    }
                    row.meanDirectDamage/=row.trials;row.meanBurnDamage/=row.trials;row.meanManaSpent/=row.trials;report.rows.Add(row);
                }
            }
            finally{UnityEngine.Random.state=randomState;}
            File.WriteAllText(Path.Combine(RealisticFolder,$"fireball-U{Mathf.RoundToInt(upgrade*100)}.json"),JsonUtility.ToJson(report,true));
        }
    }
}
