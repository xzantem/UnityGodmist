using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {

        static string EncounterThreatFolder=>Path.GetFullPath("../LocalHandoffs/encounter-threat-20261019");
        [Serializable] sealed class EncounterThreatUnit {public string instance;public int steps,awaySteps,towardSteps,lateralSteps,ccTurns,actualHealing;}
        [Serializable] sealed class EncounterThreatHit {public int turn,amount;public string source,target,action;}
        [Serializable] sealed class EncounterThreatDefinition {public string key,name;public int range,apPercent,rp;public string[] effects;}
        [Serializable] sealed class EncounterThreatControl {public int turn;public string target,effect;public string[] sources,origins;}
        [Serializable] sealed class EncounterThreatMetrics {public List<EncounterThreatUnit> units=new();public List<EncounterThreatHit> hits=new();public List<KnockdownEvent> events=new();public List<EncounterThreatControl> controls=new();}
        [Serializable] sealed class EncounterThreatReport {public string method;public List<BiomeFight> battles=new();public List<EncounterThreatMetrics> metrics=new();public List<EncounterThreatDefinition> definitions=new();}
        [TestCase("catacombs",7)][TestCase("forest",7)]
        [TestCase("catacombs",5)][TestCase("forest",5)]
        [Explicit("Read-only current-policy roster threat comparison with opening-distance sensitivity.")]
        public void AuditEncounterThreatAt48(string selectedBiome,int opening)
        {
            const float upgrade=0;var policy=opening==7?"current":"close-opening";
            Directory.CreateDirectory(EncounterThreatFolder);
            var report=new EncounterThreatReport{method="Fresh L48, current party-v6, EQ0, real trees and per-seed historical drops. Twelve paired seeds for every actual roster. Open arena12x10; enemy opening x7 or x5 only. No stat, skill, AI or player-policy changes. Read-only per-cell movement, actual damage/HP ledger, exact healing, CC and resource observers."};
            var random=UnityEngine.Random.state;
            try
            {
                foreach(var biome in new[]{selectedBiome})
                {
                    var dungeon=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                    foreach(var encounter in dungeon.EncounterDefinitions.OrderBy(e=>e.Id,StringComparer.Ordinal))
                    for(var seed=0;seed<12;seed++)
                    {
                        var session=new CombatSession{Grid=new BattleGrid(12,10),DungeonTheme=dungeon.Theme};
                        var history=RealisticDropHistory(48,seed).ToArray();var reference=PointEnemies(48);
                        for(var index=0;index<4;index++)
                        {
                            var gear=PointInput.rows.Single(r=>r.level==48&&r.hero==index&&r.weaponTier==0&&r.armorTier==0);
                            var hero=PointHero(gear,RealisticProfile(index,0),PointBudget(48),upgrade,reference);
                            hero.CharacterId="biome-hero-"+index;
                            EquipHistoricalDrops(hero,index,history,upgrade,reference);PointRefresh(hero);
                            SetExpeditionLoadout(hero,RealisticProfile(index,0));
                            session.Units.Add(new BattleUnit{Character=hero,IsPlayerSide=true,Position=new GridPosition(index==0||index==3?3:2,3+index)});
                        }
                        for(var index=0;index<encounter.Enemies.Count;index++)
                        {
                            var entry=encounter.Enemies[index];var npc=new CharacterInstanceFactory().Create(entry.CharacterDefinition,entry.ClassData,48);
                            npc.CharacterId="biome-enemy-"+index;
                            session.Units.Add(new BattleUnit{Character=npc,Position=new GridPosition((policy=="close-opening"?5:7)+index/6,2+index%6)});
                        }
                        var row=new BiomeFight{biome=biome,policy=policy,seed=seed,upgrade=upgrade,result=new Result{candidate="biome-diagnosis",encounter=encounter.Id,level=48,seed=seed,build="profile-0",enemyCount=encounter.Enemies.Count,enemyIds=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray()}};
                        report.battles.Add(row);
                        var extra=new EncounterThreatMetrics();report.metrics.Add(extra);
                        var units=new Dictionary<BattleUnit,BiomeUnit>();var positions=new Dictionary<BattleUnit,GridPosition>();
                        BiomeUnit Get(BattleUnit unit)
                        {
                            if(unit==null)return null;
                            if(units.TryGetValue(unit,out var existing))return existing;
                            var s=unit.Character.Stats;
                            var next=new BiomeUnit{instance=unit.Character.CharacterId,id=unit.Character.Definition.Id,hero=unit.IsPlayerSide,hp=s.MaxHealth.GetValue(),atk=(s.MinAttack.GetValue()+s.MaxAttack.GetValue())*.5f,physicalDefense=s.PhysicalDefense.GetValue(),magicalDefense=s.MagicalDefense.GetValue(),speed=s.Speed.GetValue(),resource=unit.Character.ResourcePool.ResourceType.ToString(),peakResource=unit.Character.ResourcePool.CurrentValue};
                            units[unit]=next;positions[unit]=unit.Position;row.units.Add(next);return next;
                        }
                        foreach(var unit in session.Units)Get(unit);
                        var observedPositions=session.Units.ToDictionary(u=>u,u=>u.Position);
                        var metrics=session.Units.ToDictionary(u=>u,u=>new EncounterThreatUnit{instance=u.Character.CharacterId});
                        extra.units=metrics.Values.ToList();
                        int Nearest(GridPosition p,BattleUnit mover)=>session.Units.Where(u=>u.IsPlayerSide!=mover.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive)
                            .Select(u=>Math.Abs(p.X-u.Position.X)+Math.Abs(p.Y-u.Position.Y)).DefaultIfEmpty(0).Min();
                        session.UnitEnteredCell+=unit=>
                        {
                            if(!observedPositions.TryGetValue(unit,out var previous))return;
                            var m=metrics[unit];m.steps++;
                            var delta=Nearest(unit.Position,unit)-Nearest(previous,unit);
                            if(delta>0)m.awaySteps++;else if(delta<0)m.towardSteps++;else m.lateralSteps++;
                            observedPositions[unit]=unit.Position;
                        };
                        foreach(var unit in session.Units)
                        {
                            var saved=unit;
                            unit.Character.HealingReceived+=(amount,origin,bus)=>metrics[saved].actualHealing+=amount;
                            if(unit.IsPlayerSide)continue;
                            foreach(var skill in unit.Character.GetActiveSkills().Concat(new[]{unit.Character.GetBasicAttackSkill()}).Where(s=>s!=null).Distinct())
                            {
                                var key=unit.Character.Definition.Id+":"+skill.SkillId;
                                if(report.definitions.Any(d=>d.key==key))continue;
                                report.definitions.Add(new EncounterThreatDefinition{key=key,name=skill.DisplayName,
                                    range=skill.TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies).Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(unit,skill,m).AbsoluteRange.MaxRange).DefaultIfEmpty(0).Max(),
                                    apPercent=skill.ActionCost,rp=PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit,skill),
                                    effects=skill.Effects.Select(e=>JsonUtility.ToJson(PassiveEffectCombatExecutionSystem.ResolveEffect(unit,skill,e))).ToArray()});
                            }
                        }
                        session.DamageApplied+=(source,target,amount,type,id)=>
                        {
                            if(source?.IsPlayerSide==false&&target.IsPlayerSide)
                                extra.hits.Add(new EncounterThreatHit{turn=row.result.turns,source=source.Character.CharacterId,target=target.Character.CharacterId,amount=amount,action=id});
                        };

                        void Positions()
                        {
                            foreach(var unit in session.Units){var value=Get(unit);if(positions[unit]!=unit.Position){value.moves++;positions[unit]=unit.Position;}value.peakResource=Math.Max(value.peakResource,unit.Character.ResourcePool.CurrentValue);}
                        }
                        var ledger=new KnockdownTrace();
                        var used=new HashSet<string>();var lastTarget=new Dictionary<BattleUnit,string>();
                        session.DamageApplied+=(source,target,amount,type,id)=>
                        {
                            Get(target).damageTaken+=amount;
                            if(target.IsPlayerSide)
                            {
                                var key=type.ToString();var count=row.heroDamageTypes.FirstOrDefault(x=>x.id==key);
                                if(count==null){count=new SkillCount{id=key};row.heroDamageTypes.Add(count);}count.count+=amount;
                                if(type==DamageType.Burn && target.Character.PassiveEffects.Any(e=>e.EffectId=="burn" && e.GetStateValue("mage.burn.self")?.BooleanValue==true))row.selfBurnDamage+=amount;
                            }
                            if(source!=null)Get(source).damageDealt+=amount;
                            else if(target.IsPlayerSide)row.unattributedHeroDamage+=amount;else row.unattributedEnemyDamage+=amount;
                            if(row.trace.Count<100)row.trace.Add($"T{row.result.turns} DAMAGE {source?.Character.CharacterId??"unattributed"}->{target.Character.CharacterId} {type} {amount} HP={target.Character.CurrentHealth}");
                        };
                        session.SkillResolved+=(actor,skill,id)=>
                        {
                            Positions();var value=Get(actor);var key=skill.SkillId;var count=value.skills.FirstOrDefault(x=>x.id==key);
                            if(count==null){count=new SkillCount{id=key};value.skills.Add(count);}count.count++;
                            if(actor.IsPlayerSide)used.Add(actor.Character.CharacterId+":"+key);
                            if(row.trace.Count<100)row.trace.Add($"T{row.result.turns} SKILL {actor.Character.CharacterId} {key} pos={actor.Position.X},{actor.Position.Y} AP={actor.CurrentActionPoints} resource={actor.Character.ResourcePool.CurrentValue}");
                        };
                        UnityEngine.Random.InitState(81016+seed);
                        using(var engine=new Engine(session,row.result,ledger))
                        {
                            engine.AuditTurnLimit=600;engine.AuditLateKnifeExtraction=true;engine.UseLegacyPlayerPolicy=false;
                            engine.AdditionalAuditScore=(actor,skill,mode,targets)=>
                            {
                                return ExpeditionUtilityScore(session,actor,skill,mode,targets,used,row.result.turns>=200);
                            };
                            engine.AuditUnitTurn=actor=>
                            {
                                Positions();Get(actor).turns++;
                                if(StatusEffectSystem.IsHardCrowdControlled(actor))
                                {
                                    metrics[actor].ccTurns++;
                                    foreach(var effect in actor.Character.PassiveEffects.Where(e=>e.RemainingDuration>0
                                        && e.EffectId is "stun" or "freeze" or "sleep" or "paralysis"))
                                        extra.controls.Add(new EncounterThreatControl{turn=row.result.turns,target=actor.Character.CharacterId,effect=effect.EffectId,
                                            sources=effect.Sources.Select(s=>s.SourceCharacterId).ToArray(),origins=effect.Sources.Select(s=>s.SourceId).ToArray()});
                                }
                                used.RemoveWhere(id=>id.StartsWith(actor.Character.CharacterId+":",StringComparison.Ordinal));
                            };
                            engine.AuditFeedback=feedback=>
                            {
                                if(feedback.Type==CombatFeedbackType.Heal&&feedback.Target!=null)Get(feedback.Target).healReceived+=feedback.Amount;
                                if(feedback.Type==CombatFeedbackType.ResourceSpend&&feedback.Target!=null)Get(feedback.Target).spent+=feedback.Amount;
                            };
                            engine.AuditChosenPlan=(actor,skill,targets,score)=>
                            {
                                row.decisions++;var key=string.Join(",",targets.Where(t=>!t.IsPlayerSide).Select(t=>t.Character.CharacterId));
                                if(key.Length>0){if(lastTarget.TryGetValue(actor,out var last)&&last!=key)row.targetSwitches++;lastTarget[actor]=key;}
                                if(row.trace.Count<100)row.trace.Add($"T{row.result.turns} PLAN {actor.Character.CharacterId} {skill.SkillId} targets={key} score={score}");
                            };
                            engine.Run();Positions();ledger.Verify(row.result);extra.events=ledger.events;
                        }
                        Assert.That(row.result.stop,Is.Null.Or.Empty,"Budget exhaustion is not a defeat.");
                    }
                }
                Assert.That(report.battles.Count,Is.EqualTo((selectedBiome=="forest"?7:8)*12));
                Assert.That(report.metrics.Sum(m=>m.events.Count(e=>e.kind=="down")),Is.EqualTo(report.battles.Sum(b=>b.result.downings)));
                Assert.That(report.metrics.Sum(m=>m.units.Where(u=>u.instance.StartsWith("biome-hero-")).Sum(u=>u.actualHealing)),Is.EqualTo(report.battles.Sum(b=>b.result.heroHealing)));
                File.WriteAllText(Path.Combine(EncounterThreatFolder,$"fresh-{selectedBiome}-x{opening}.json"),JsonUtility.ToJson(report,true));
            }
            finally{UnityEngine.Random.state=random;}
        }
    }
}
