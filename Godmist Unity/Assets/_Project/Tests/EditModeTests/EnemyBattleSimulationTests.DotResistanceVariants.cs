using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string ResistanceVariantFolder=>Path.GetFullPath("../LocalHandoffs/dot-resistance-variants-20261103");
        [Serializable] sealed class ResistanceIncoming
        {
            public int variant,enemyLevel,hp,turns,applications,rawTick,tick,packet;
            public string hero,enemy,enemyName,skill,skillName,status;
            public bool ordinary;public float resistance,chanceAnyGivenHits,expectedGivenHits;
        }
        [Serializable] sealed class ResistanceOutgoing
        {
            public int variant,level,profile,hp,trials,ap,cost;public string hero,skill,enemy;
            public bool known;public float direct,dot,applied,kills;
        }
        [Serializable] sealed class ResistanceVariantReport
        {
            public List<ResistanceIncoming> incoming=new();public List<ResistanceOutgoing> outgoing=new();public int validatedPackets;
        }
        static void ResistanceFloor(CharacterInstance c,bool enabled)
        {
            if(!enabled)return;
            c.BaseResistances=JsonUtility.FromJson<ResistanceSet>(JsonUtility.ToJson(c.BaseResistances));
            c.BaseResistances.BleedResistance=Math.Max(c.BaseResistances.BleedResistance,.4f);
            c.BaseResistances.PoisonResistance=Math.Max(c.BaseResistances.PoisonResistance,.4f);
            c.BaseResistances.FireResistance=Math.Max(c.BaseResistances.FireResistance,.4f);
            new CharacterStatPipelineSystem().Refresh(c);
        }
        static bool DotEffect(SkillEffectData e)=>e.EffectType==EffectType.ApplyStatus && StatusEffectSystem.IsDamageOverTimeStatus(e.StatusId)
            || e.EffectType==EffectType.DealDamage && StatusEffectSystem.IsDamageOverTimeStatus(e.OnHitStatusId);
        static GameDefines EffectiveDefinesSnapshot()
        {
            var copy=ScriptableObject.CreateInstance<GameDefines>();
            foreach(var e in copy.Entries)
                switch(e.ValueType)
                {
                    case GameDefineValueType.Float:e.CurrentFloatValue=GameDefinesRuntime.GetFloat(e.Id);break;
                    case GameDefineValueType.Integer:e.CurrentIntegerValue=GameDefinesRuntime.GetInt(e.Id);break;
                    case GameDefineValueType.Boolean:e.CurrentBooleanValue=GameDefinesRuntime.GetBool(e.Id);break;
                    case GameDefineValueType.String:e.CurrentStringValue=GameDefinesRuntime.GetString(e.Id);break;
                }
            return copy;
        }
        static void SetCurve(GameDefines config,bool flat)
        {
            Assert.That(config.TryGet(GameDefineIds.DotResistanceLowMultiplier,out var low),Is.True);
            Assert.That(config.TryGet(GameDefineIds.DotResistanceLowSlope,out var slope),Is.True);
            low.CurrentFloatValue=flat?1.5f:2f;slope.CurrentFloatValue=flat?1f:2f;
            GameDefinesRuntime.Configure(config);
            Assert.That(GameDefinesRuntime.Current.Validate(),Is.Empty);
            for(var i=0;i<=100;i++)
            {
                var r=i/100f;var expected=flat||r>.5f?1.5f-r:2-2*r;
                Assert.That(DamageOverTimePassiveEffectProcessor.CalculateDamageMultiplier(r),Is.EqualTo(expected).Within(.00001f));
            }
        }
        [Test,Explicit("Four detached DoT resistance scenarios: baseline, party minimum40%, global1.5-r, both. No content writes.")]
        [Timeout(900000)]
        public void AuditFourDotResistanceVariants()
        {
            Directory.CreateDirectory(ResistanceVariantFolder);Directory.CreateDirectory(Folder);
            var report=new ResistanceVariantReport();var rng=UnityEngine.Random.state;
            var prior=EffectiveDefinesSnapshot();var asset=AssetDatabase.LoadAssetAtPath<GameDefines>("Assets/_Project/Resources/GameDefines.asset");
            Assert.That(asset,Is.Not.Null);var config=UnityEngine.Object.Instantiate(asset);var assetJson=EditorJsonUtility.ToJson(asset);
            var skills=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/ScriptableObjects"}).Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var skillSnapshots=skills.ToDictionary(s=>s,EditorJsonUtility.ToJson);
            var enemies=PointCatalog.CharacterDefinitions.Where(d=>d!=null && d.ClassData!=null && d.Id.StartsWith("enemy.") && d.name!="CD_Enemy_Test").ToArray();
            foreach(var d in enemies.Concat(Enumerable.Range(0,4).Select(PointHeroDefinition)))originals[d]=EditorJsonUtility.ToJson(d);
            try
            {
                for(var variant=0;variant<4;variant++)
                {
                    var floor=variant is 1 or 3;var flat=variant>=2;SetCurve(config,flat);
                    for(var hero=0;hero<4;hero++)foreach(var definition in enemies)
                    foreach(var level in HistoricalEnemyBalanceReference.UsesBossBaseBoost(definition.Id)?new[]{1,10}:new[]{1})
                    foreach(var skill in definition.StartingSkills.Append(definition.BasicAttackSkill).Where(s=>s!=null).Distinct())
                    foreach(var original in skill.Effects.Where(DotEffect))
                    {
                        var caster=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,level),false);
                        var victim=PointUnit(HealthComparisonHero(hero,1,0),true);ResistanceFloor(victim.Character,floor);
                        var e=PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,original);
                        var status=e.EffectType==EffectType.DealDamage?e.OnHitStatusId:e.StatusId;
                        var turns=e.EffectType==EffectType.DealDamage?e.OnHitStatusDurationTurns:e.DurationTurns;
                        var count=e.EffectType==EffectType.DealDamage?Math.Max(1,e.DamageHitCount)+e.AdditionalHitTrials:1;
                        var raw=StatusEffectSystem.CalculateStatusDamagePerTurn(caster,status,e.StatusDamageMultiplier);
                        var chance=StatusEffectSystem.CalculateStatusApplicationChance(caster,victim,status,e.EffectType==EffectType.DealDamage?e.OnHitStatusChance:e.BaseChance);
                        var probabilities=new double[count+1];probabilities[0]=1;
                        for(var hit=0;hit<count;hit++)
                        {
                            var p=chance*(hit>=e.DamageHitCount && e.EffectType==EffectType.DealDamage?e.AdditionalHitChance:1);
                            for(var k=hit+1;k>=0;k--)probabilities[k]=probabilities[k]*(1-p)+(k>0?probabilities[k-1]*p:0);
                        }
                        var expected=Enumerable.Range(1,count).Sum(k=>probabilities[k]*DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(victim,status,raw*k)*turns);
                        var resistance=ModifyResistanceSystem.GetEffectiveResistance(victim,status=="bleed"?ResistanceType.Bleed:status=="poison"?ResistanceType.Poison:ResistanceType.Fire);
                        var tick=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(victim,status,raw*count);
                        report.incoming.Add(new ResistanceIncoming{variant=variant,hero=victim.Character.Definition.DisplayName,enemy=definition.Id,enemyName=definition.DisplayName,skill=skill.SkillId,skillName=skill.DisplayName,status=status,
                            ordinary=SkillPointEnemyIds.Contains(definition.Id),enemyLevel=level,hp=victim.Character.MaximumHealth,turns=turns,applications=count,rawTick=raw*count,tick=tick,packet=tick*turns,
                            resistance=resistance,chanceAnyGivenHits=(float)(1-probabilities[0]),expectedGivenHits=(float)expected});
                        if(hero==2 && level==1 && definition.Id is "enemy.catacombs.occultist" or "enemy.forest.rabid_wolf" or "enemy.forest.bandit_thug" && status=="bleed")
                        {
                            var fight=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){caster,victim}};var system=new StatusEffectSystem();
                            Assert.That(system.ApplyStatus(caster,victim,status,1,turns,1,1,0,"audit.packet",publishFeedback:false,statusDamagePerTurnOverride:raw*count,sharedRoll:0f),Is.True);
                            var hp=victim.Character.CurrentHealth;
                            for(var t=0;t<turns&&victim.Character.LifeState==CharacterLifeState.Alive;t++){system.ProcessTurnStart(fight,victim);system.ProcessTurnEnd(victim);}
                            Assert.That(hp-victim.Character.CurrentHealth,Is.EqualTo(Math.Min(hp,tick*turns)));report.validatedPackets++;
                        }
                    }
                    var chosen=PointEnemyDefinitions.Where(d=>new[]{"swordsman","defender","occultist","ghoul","bandit_thug","rabid_wolf","elf_druid","infected_tree"}.Contains(d.Id.Split('.').Last())).ToArray();
                    foreach(var level in new[]{1,5,10,50})foreach(var profile in new[]{0,1})
                    foreach(var pair in new[]{(hero:2,id:"skill.mage.fire_arrow"),(hero:2,id:"skill.mage.fireball"),(hero:1,id:"skill.scout.poisoned_dagger"),(hero:0,id:"skill.warrior.bleeding_destruction")})
                    foreach(var definition in chosen)
                    {
                        var template=DotIdentityHero(pair.hero,level,profile,pair.id);ResistanceFloor(template,floor);var skill=skills.Single(s=>s.SkillId==pair.id);
                        var caster=PointUnit(template,true);var target=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                        CombatTurnSystem.RefreshMaxActionPoints(new CombatSession(),caster,true);
                        var row=new ResistanceOutgoing{variant=variant,level=level,profile=profile,hero=template.Definition.DisplayName,skill=pair.id,enemy=definition.Id,hp=target.MaximumHealth,
                            known=template.GetKnownSkills().Any(s=>s.SkillId==pair.id),cost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill),ap=CombatActionCostCalculator.CalculateSkillCost(caster,skill)};
                        if(row.known)
                        {
                            row.trials=16;
                            for(var seed=0;seed<row.trials;seed++)
                            {
                                UnityEngine.Random.InitState(774400+seed);
                                var c=DotIdentityHero(pair.hero,level,profile,pair.id);ResistanceFloor(c,floor);
                                var needed=PassiveEffectCombatExecutionSystem.ResolveResourceCost(PointUnit(c,true),skill);c.ResourcePool.CurrentValue=Math.Max(c.ResourcePool.CurrentValue,needed);
                                new CharacterStatPipelineSystem().Refresh(c);
                                var source=PointUnit(c,true);var victim=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,level),false);
                                source.Position=new GridPosition(2,2);victim.Position=new GridPosition(pair.hero==2?4:3,2);victim.ScoutState.Facing=Vector2Int.left;
                                var fight=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){source,victim}};
                                using(var engine=new Engine(fight,new Result()))
                                {
                                    Assert.That(engine.CastProbe(source,skill,victim),Is.True,pair.id+" "+level);
                                    var after=victim.Character.CurrentHealth;row.direct+=row.hp-after;
                                    bool Active()=>victim.Character.PassiveEffects.Any(e=>StatusEffectSystem.IsDamageOverTimeStatus(e.EffectId)&&PassiveEffectLifecycleSystem.IsActive(e));
                                    if(Active())row.applied++;
                                    for(var t=0;t<12&&victim.Character.LifeState==CharacterLifeState.Alive&&Active();t++)engine.TickProbeTarget(victim);
                                    Assert.That(victim.Character.LifeState!=CharacterLifeState.Alive||!Active(),Is.True);
                                    row.dot+=after-victim.Character.CurrentHealth;if(victim.Character.CurrentHealth<=0)row.kills++;
                                    Assert.That(DamageOverTimePassiveEffectProcessor.CalculateDamageMultiplier(0),Is.EqualTo(flat?1.5f:2));
                                }
                            }
                            row.direct/=row.trials;row.dot/=row.trials;row.applied/=row.trials;row.kills/=row.trials;
                        }
                        report.outgoing.Add(row);
                    }
                    File.WriteAllText(Path.Combine(ResistanceVariantFolder,"comparison.json"),JsonUtility.ToJson(report,true));
                    File.AppendAllText(Path.Combine(ResistanceVariantFolder,"progress.txt"),"DONE variant "+variant+Environment.NewLine);
                }
                foreach(var s in skillSnapshots)Assert.That(EditorJsonUtility.ToJson(s.Key),Is.EqualTo(s.Value),s.Key.SkillId);
                Assert.That(EditorJsonUtility.ToJson(asset),Is.EqualTo(assetJson));
                Assert.That(report.validatedPackets,Is.EqualTo(12));
            }
            finally
            {
                GameDefinesRuntime.Configure(prior);UnityEngine.Random.state=rng;
                UnityEngine.Object.DestroyImmediate(config);UnityEngine.Object.DestroyImmediate(prior);
            }
        }
    }
}
