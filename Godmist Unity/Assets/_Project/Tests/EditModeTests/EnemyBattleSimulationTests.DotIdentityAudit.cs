using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
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
        static string DotIdentityFolder=>Path.GetFullPath("../LocalHandoffs/dot-identity-audit-20261103");
        [Serializable] sealed class DotIdentityRow
        {
            public string skill,name,enemy,enemyName,resourceType,tree;
            public int level,profile,hp,ap,maxAp,cost,maxResource,startResource,regen,duration,perTick,trials,prepSteps,prepAp,basicAp,prepTurns,legalPrepSteps,legalPrepAp;
            // dotExpected is raw payload expectation, before tick resistance and conditional talent damage.
            public bool known; public float directExpected,dotExpected,statusChance,basicExpected,initialRaw,hpDirect,hpDot,applied,paid,netPaid,directKills;
            public float[] killedByTick=new float[5];
        }
        [Serializable] sealed class DotIdentityReport { public List<DotIdentityRow> rows=new();public List<string> census=new(); }
        static CharacterInstance DotIdentityHero(int heroIndex,int level,int profile,string skillId)
        {
            var c=HealthComparisonHero(heroIndex,level,profile);
            if(profile==1 && level>1)
            {
                c.SkillTreeProgress.Clear();c.AvailableSkillPoints=PointBudget(level);var progression=new SkillTreeProgressionSystem();
                var trees=c.ClassData.SkillTrees.Where(t=>t!=null).OrderBy(t=>heroIndex==2?t.Id==MageCombatSystem.Fire?0:1:t.Nodes.Any(n=>n.GrantedSkill?.SkillId==skillId)?0:1).ToArray();
                while(c.AvailableSkillPoints>0)
                {
                    var invested=false;
                    foreach(var tree in trees)
                    {
                        foreach(var node in tree.Nodes.OrderBy(n=>n.Tier).ThenBy(n=>n.Column))
                            if(progression.TryInvest(c,tree.Id,node.Id)){invested=true;break;}
                        if(invested)break;
                    }
                    Assert.That(invested,Is.True,"Only legally earned SP may be used.");
                }
                PointRefresh(c);
            }
            return c;
        }
        static readonly Dictionary<(int,int,string),(int turns,int ap,int steps)> DotPreparationCache=new();
        static void DotMomentumPreparation(DotIdentityRow row,int hero,string skill)
        {
            // Optimistic open-floor preparation, no incoming attacks. Respect per-turn PA,
            // increasing movement cost, PP generation and the idle-turn PP loss.
            var key=(row.level,row.profile,skill);
            if(DotPreparationCache.TryGetValue(key,out var cached)){row.prepTurns=cached.turns;row.legalPrepAp=cached.ap;row.legalPrepSteps=cached.steps;return;}
            var states=new Dictionary<int,(int ap,int steps)>{{0,(0,0)}};
            for(var turn=1;turn<=12;turn++)
            {
                var next=new Dictionary<int,(int ap,int steps)>();var candidates=new List<(int ap,int steps)>();
                foreach(var state in states)for(var steps=0;steps<=20;steps++)
                {
                    var prep=PointUnit(DotIdentityHero(hero,row.level,row.profile,skill),true);prep.Character.ResourcePool.CurrentValue=state.Key;prep.MovementStepsThisTurn=0;
                    var ap=CombatMovementSystem.CalculatePathCost(prep,steps);if(ap>row.maxAp)break;
                    var rule=new ResourceRuleSystem();
                    for(var n=0;n<steps;n++){prep.MovementStepsThisTurn++;rule.ProcessUnitMoved(prep,0,1);}
                    var total=(ap:state.Value.ap+ap,steps:state.Value.steps+steps);
                    if(prep.Character.ResourcePool.CurrentValue>=row.cost && ap+row.ap<=row.maxAp)candidates.Add(total);
                    rule.ProcessTurnEnd(prep);var value=prep.Character.ResourcePool.CurrentValue;
                    if(!next.TryGetValue(value,out var prior)||total.ap<prior.ap)next[value]=total;
                }
                if(candidates.Count>0)
                {var best=candidates.OrderBy(c=>c.ap).ThenBy(c=>c.steps).First();row.prepTurns=turn;row.legalPrepAp=best.ap;row.legalPrepSteps=best.steps;DotPreparationCache[key]=(turn,best.ap,best.steps);return;}
                states=next;
            }
            Assert.Fail("No PP preparation path found within 12 turns: "+skill);
        }
        [Test,Explicit("Read-only DoT audit after the approved HP/ATK rebalance: current gear, legal SP, real single casts and scheduled ticks.")]
        [Timeout(900000)]
        public void AuditDotIdentityCurrentContent()=>RunDotIdentity(false);
        [Test,Explicit("Detached proposal only: Fireball 0.5 direct +0.45/tick for two turns; Dagger 0.5 direct +0.5/tick for three turns; both 85% base status, unchanged costs.")]
        [Timeout(900000)]
        public void AuditDotIdentityProposal()=>RunDotIdentity(true);
        void RunDotIdentity(bool proposal)
        {
            Directory.CreateDirectory(DotIdentityFolder);Directory.CreateDirectory(Folder);
            var report=new DotIdentityReport();var previous=UnityEngine.Random.state;
            var assets=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/ScriptableObjects"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            if(proposal)
            {
                for(var i=0;i<assets.Length;i++)
                {
                    if(assets[i].SkillId is not ("skill.mage.fireball" or "skill.scout.poisoned_dagger"))continue;
                    var copy=UnityEngine.Object.Instantiate(assets[i]);temporary.Add(copy);assets[i]=copy;
                    var data=new SerializedObject(copy);var effects=data.FindProperty("effects");
                    for(var n=0;n<effects.arraySize;n++)
                    {
                        var e=effects.GetArrayElementAtIndex(n);
                        if(e.FindPropertyRelative("effectType").enumValueIndex==(int)EffectType.DealDamage)e.FindPropertyRelative("damageMultiplier").floatValue=.5f;
                        if(e.FindPropertyRelative("effectType").enumValueIndex!=(int)EffectType.ApplyStatus)continue;
                        if(!StatusEffectSystem.IsDamageOverTimeStatus(e.FindPropertyRelative("statusId").stringValue))continue;
                        e.FindPropertyRelative("baseChance").floatValue=.85f;
                        e.FindPropertyRelative("durationTurns").intValue=copy.SkillId=="skill.mage.fireball"?2:3;
                        e.FindPropertyRelative("statusDamageMultiplier").floatValue=copy.SkillId=="skill.mage.fireball"?.45f:.5f;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            foreach(var s in assets.Where(s=>s.Effects.Any(e=>e.EffectType==EffectType.ApplyStatus && StatusEffectSystem.IsDamageOverTimeStatus(e.StatusId) || e.EffectType==EffectType.DealDamage && StatusEffectSystem.IsDamageOverTimeStatus(e.OnHitStatusId) || e.EffectType==EffectType.MageFireAura)))
            {
                report.census.Add(s.SkillId);File.WriteAllText(Path.Combine(DotIdentityFolder,(proposal?"proposal-":"")+s.name+"-config.json"),EditorJsonUtility.ToJson(s,true));
            }
            var definitions=PointEnemyDefinitions.Where(d=>new[]{"swordsman","defender","occultist","ghoul","bandit_thug","rabid_wolf","elf_druid","infected_tree"}.Contains(d.Id.Split('.').Last())).ToArray();
            foreach(var d in definitions)originals[d]=EditorJsonUtility.ToJson(d);
            try
            {
                foreach(var level in new[]{1,3,5,10,20,50})foreach(var profile in new[]{0,1})foreach(var pair in new[]{(hero:2,id:"skill.mage.fire_arrow"),(hero:2,id:"skill.mage.fireball"),(hero:1,id:"skill.scout.poisoned_dagger"),(hero:0,id:"skill.warrior.bleeding_destruction")})
                foreach(var definition in definitions)
                {
                    var template=DotIdentityHero(pair.hero,level,profile,pair.id);var caster=PointUnit(template,true);
                    var skill=assets.Single(s=>s.SkillId==pair.id);var target=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,level),false);
                    caster.Position=new GridPosition(2,2);target.Position=new GridPosition(pair.hero==2?4:3,2);target.ScoutState.Facing=Vector2Int.left;
                    var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){caster,target},ActiveUnit=caster};caster.WarriorState.Session=session;target.WarriorState.Session=session;
                    CombatTurnSystem.RefreshMaxActionPoints(session,caster,true);
                    var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,target,skill);
                    var effect=skill.Effects.Select(e=>PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,e)).Single(e=>e.EffectType==EffectType.ApplyStatus && StatusEffectSystem.IsDamageOverTimeStatus(e.StatusId) || e.EffectType==EffectType.DealDamage && StatusEffectSystem.IsDamageOverTimeStatus(e.OnHitStatusId));
                    var status=string.IsNullOrEmpty(effect.StatusId)?effect.OnHitStatusId:effect.StatusId;
                    var duration=effect.EffectType==EffectType.DealDamage?effect.OnHitStatusDurationTurns:effect.DurationTurns;
                    var tick=StatusEffectSystem.CalculateStatusDamagePerTurn(caster,status,effect.StatusDamageMultiplier);
                    var basic=template.GetBasicAttackSkill();var range=basic.TargetModes.Max(m=>PassiveEffectCombatExecutionSystem.ResolveMode(caster,basic,m).AbsoluteRange.MaxRange);
                    target.Position=new GridPosition(2+Math.Min(4,Math.Max(1,range)),2);
                    var basicExpected=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,basic,0);
                    target.Position=new GridPosition(pair.hero==2?4:3,2);
                    var row=new DotIdentityRow{skill=pair.id,name=skill.DisplayName,enemy=definition.Id,enemyName=definition.DisplayName,level=level,profile=profile,hp=target.Character.MaximumHealth,
                        ap=CombatActionCostCalculator.CalculateSkillCost(caster,skill),maxAp=caster.MaxActionPoints,cost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill),
                        resourceType=template.ResourcePool.ResourceType.ToString(),maxResource=template.ResourcePool.MaxValue,startResource=template.ResourcePool.CurrentValue,regen=template.ResourcePool.Regeneration,
                        tree=string.Join("|",template.SkillTreeProgress.Select(t=>t.TreeId)),known=template.GetKnownSkills().Any(s=>s.SkillId==pair.id),perTick=tick,duration=duration,statusChance=preview.StatusChance,
                        directExpected=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,skill,0),dotExpected=tick*duration*preview.StatusChance,basicExpected=basicExpected,
                        basicAp=CombatActionCostCalculator.CalculateSkillCost(caster,basic)};
                    if(template.ResourcePool.ResourceType==ResourceType.Momentum)
                    {
                        var prep=PointUnit(DotIdentityHero(pair.hero,level,profile,pair.id),true);prep.Character.ResourcePool.CurrentValue=0;
                        var rule=new ResourceRuleSystem();
                        while(prep.Character.ResourcePool.CurrentValue<row.cost && row.prepSteps<30)
                        {prep.MovementStepsThisTurn++;row.prepSteps++;rule.ProcessUnitMoved(prep,0,1);}
                        Assert.That(prep.Character.ResourcePool.CurrentValue,Is.GreaterThanOrEqualTo(row.cost));
                        prep.MovementStepsThisTurn=0;row.prepAp=CombatMovementSystem.CalculatePathCost(prep,row.prepSteps);
                        DotMomentumPreparation(row,pair.hero,pair.id);
                    }
                    else if(template.ResourcePool.ResourceType==ResourceType.Rage)
                    {
                        row.prepSteps=Mathf.CeilToInt(row.cost/(float)Math.Max(1,RageResourceRule.PreviewOffensiveGain(template)));row.prepAp=row.prepSteps*row.basicAp;
                        var perTurn=Math.Max(1,row.maxAp/Math.Max(1,row.basicAp));
                        row.prepTurns=(row.prepSteps+perTurn-1)/perTurn;
                        var lastCount=(row.prepSteps-1)%perTurn+1;
                        if(row.maxAp-lastCount*row.basicAp<row.ap)row.prepTurns++;
                        row.legalPrepAp=row.prepAp;row.legalPrepSteps=row.prepSteps;
                    }
                    if(row.known)
                    {
                        row.trials=32;
                        for(var seed=0;seed<row.trials;seed++)
                        {
                            UnityEngine.Random.InitState(773300+seed);
                            var c=DotIdentityHero(pair.hero,level,profile,pair.id);var needed=PassiveEffectCombatExecutionSystem.ResolveResourceCost(PointUnit(c,true),skill);
                            c.ResourcePool.CurrentValue=Math.Max(c.ResourcePool.CurrentValue,needed);new CharacterStatPipelineSystem().Refresh(c);
                            var source=PointUnit(c,true);var victim=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,level),false);
                            source.Position=caster.Position;victim.Position=target.Position;victim.ScoutState.Facing=Vector2Int.left;
                            var fight=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){source,victim}};
                            var raw=0;fight.DamageApplied+=(a,t,n,type,id)=>{if(t==victim && type is not (DamageType.Burn or DamageType.Poison or DamageType.Bleed))raw+=n;};
                            var result=new Result();var resourceBefore=c.ResourcePool.CurrentValue;
                            using(var engine=new Engine(fight,result))
                            {
                                Assert.That(engine.CastProbe(source,skill,victim),Is.True,pair.id+" L"+level+" "+definition.Id);
                                row.initialRaw+=raw;row.hpDirect+=row.hp-victim.Character.CurrentHealth;row.paid+=source.LastSkillResourceSpent;row.netPaid+=resourceBefore-c.ResourcePool.CurrentValue;
                                bool Active()=>victim.Character.PassiveEffects.Any(e=>StatusEffectSystem.IsDamageOverTimeStatus(e.EffectId)&&PassiveEffectLifecycleSystem.IsActive(e));
                                if(Active())row.applied++;
                                var hpAfter=victim.Character.CurrentHealth;if(hpAfter<=0)row.directKills++;
                                for(var t=1;t<=5;t++)
                                {
                                    if(victim.Character.LifeState==CharacterLifeState.Alive && Active())engine.TickProbeTarget(victim);
                                    if(victim.Character.CurrentHealth<=0)row.killedByTick[t-1]++;
                                }
                                Assert.That(victim.Character.LifeState!=CharacterLifeState.Alive||!Active(),Is.True,"All observed DoTs must finish within five target turns.");
                                row.hpDot+=hpAfter-victim.Character.CurrentHealth;
                            }
                        }
                        row.initialRaw/=row.trials;row.hpDirect/=row.trials;row.hpDot/=row.trials;row.applied/=row.trials;
                        row.paid/=row.trials;row.netPaid/=row.trials;row.directKills/=row.trials;
                        for(var i=0;i<row.killedByTick.Length;i++)row.killedByTick[i]/=row.trials;
                    }
                    report.rows.Add(row);
                }
                File.WriteAllText(Path.Combine(DotIdentityFolder,proposal?"proposal.json":"current.json"),JsonUtility.ToJson(report,true));
                Assert.That(report.rows.Count,Is.EqualTo(384));
            }
            finally { UnityEngine.Random.state=previous; }
        }
    }
}
