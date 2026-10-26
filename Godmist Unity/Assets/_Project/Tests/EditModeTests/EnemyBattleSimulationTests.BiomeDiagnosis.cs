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
        static string BiomeAuditFolder=>Path.GetFullPath("../LocalHandoffs/forest-encounter-diagnosis-20261019");
        [Serializable] sealed class BiomeUnit
        {
            public string instance,id,resource;public bool hero;public float hp,atk,physicalDefense,magicalDefense,speed;
            public int turns,moves,damageDealt,damageTaken,healReceived,peakResource,spent;public List<SkillCount> skills=new();
        }
        [Serializable] sealed class BiomeFight
        {
            public string biome,policy;public int seed;public float upgrade;public Result result;
            public int unattributedHeroDamage,unattributedEnemyDamage,targetSwitches,decisions,selfBurnDamage;
            public List<SkillCount> heroDamageTypes=new();
            public List<BiomeUnit> units=new();public List<string> trace=new();
        }
        [Serializable] sealed class BiomeAuditReport{public string method;public List<BiomeFight> battles=new();}

        [TestCase("catacombs")][TestCase("forest")]
        [Explicit("Remove only simulator-only once-per-ally rescue cap; production actions unchanged.")]
        public void AuditBiomeRepeatRescueExpedition(string biome)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,4,
                $"repeat-rescue-L48-U0-{biome}",outputFolder:BiomeAuditFolder,fullOnly:true,repeatRescues:true,legacyPolicy:true);

        [TestCase(0f,"catacombs","ranged-hold")][TestCase(0f,"forest","ranged-hold")]
        [TestCase(1f,"catacombs","ranged-hold")][TestCase(1f,"forest","ranged-hold")]
        [TestCase(0f,"catacombs","close-opening")][TestCase(0f,"forest","close-opening")]
        [TestCase(1f,"catacombs","close-opening")][TestCase(1f,"forest","close-opening")]
        [Explicit("Paired full expeditions; only stop unnecessary ranged closing. No stat or roster changes.")]
        public void AuditBiomeExpeditionControl(float upgrade,string biome,string policy)
            =>RunRealisticExpeditions(48,upgrade,biome,Godmist.Core.Enums.DungeonLength.Normal,0,4,
                $"{policy}-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}",outputFolder:BiomeAuditFolder,holdRanged:policy=="ranged-hold",fullOnly:true,enemyStartX:policy=="close-opening"?5:7,legacyPolicy:true);

        [TestCase(0f,"baseline")][TestCase(1f,"baseline")]
        [TestCase(0f,"repeat-rescue")]
        [TestCase(0f,"ranged-hold")][TestCase(1f,"ranged-hold")]
        [TestCase(0f,"effective-cost")][TestCase(1f,"effective-cost")]
        [TestCase(0f,"focus-in-range")][TestCase(1f,"focus-in-range")]
        [TestCase(0f,"close-opening")][TestCase(1f,"close-opening")]
        [Explicit("Read-only biome roster and simulator-decision diagnosis; no balance asset authoring.")]
        public void AuditBiomeRosterDecisionsAt48(float upgrade,string policy)
        {
            Directory.CreateDirectory(BiomeAuditFolder);
            var report=new BiomeAuditReport{method="Fresh L48 party for every roster/seed; profile0 trees, same historical unique stream per seed, Common ordinary gear, no carryover/XP/camping. Four paired seeds. Baseline uses original 12x10 arena and original heuristic. Controls change only actual AP denominator, legal in-range focus bonus, or opening distance (enemy x7->x5). No NPC stats/skills/AI changes; these controls are sensitivity tests, not an optimal player policy."};
            var random=UnityEngine.Random.state;
            try
            {
                foreach(var biome in new[]{"catacombs","forest"})
                {
                    var dungeon=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                    foreach(var encounter in dungeon.EncounterDefinitions.OrderBy(e=>e.Id,StringComparer.Ordinal))
                    for(var seed=0;seed<4;seed++)
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
                        void Positions()
                        {
                            foreach(var unit in session.Units){var value=Get(unit);if(positions[unit]!=unit.Position){value.moves++;positions[unit]=unit.Position;}value.peakResource=Math.Max(value.peakResource,unit.Character.ResourcePool.CurrentValue);}
                        }
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
                        using(var engine=new Engine(session,row.result))
                        {
                            engine.AuditTurnLimit=600;engine.AuditLateKnifeExtraction=true;engine.UseLegacyPlayerPolicy=true;
                            engine.AuditUseEffectiveActionCost=policy=="effective-cost";engine.AuditHoldRangedPosition=policy=="ranged-hold";engine.AuditRepeatRescues=policy=="repeat-rescue";
                            engine.AdditionalAuditScore=(actor,skill,mode,targets)=>
                            {
                                var score=ExpeditionUtilityScore(session,actor,skill,mode,targets,used,row.result.turns>=200);
                                if(policy=="focus-in-range")
                                {
                                    var focus=session.Units.Where(u=>!u.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive&&!StatusEffectSystem.IsInvisible(u)).OrderBy(u=>u.Character.CurrentHealth).ThenBy(u=>u.Character.CharacterId,StringComparer.Ordinal).FirstOrDefault();
                                    if(focus!=null&&targets.Contains(focus)&&CombatSkillPreviewCalculator.CalculateTargetPreview(actor,focus,skill).Damage.HasDamage)
                                        score+=Math.Min(focus.Character.CurrentHealth,(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue()))*2;
                                }
                                return score;
                            };
                            engine.AuditUnitTurn=actor=>{Positions();Get(actor).turns++;used.RemoveWhere(id=>id.StartsWith(actor.Character.CharacterId+":",StringComparison.Ordinal));};
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
                            engine.Run();Positions();
                        }
                        Assert.That(row.result.stop,Is.Null.Or.Empty,"Budget exhaustion is not a defeat.");
                    }
                }
                Assert.That(report.battles.Count,Is.EqualTo(60),"Eight catacombs and seven forest rosters, four paired seeds.");
                File.WriteAllText(Path.Combine(BiomeAuditFolder,$"fresh-U{Mathf.RoundToInt(upgrade*100)}-{policy}.json"),JsonUtility.ToJson(report,true));
            }
            finally{UnityEngine.Random.state=random;}
        }
    }
}
