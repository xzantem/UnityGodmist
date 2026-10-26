using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
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
        [Serializable] sealed class TheoryStats
        {
            public string stage,id,gear; public int level,profile,material,hp,maxResource,regeneration,maxAp;
            public float min,max,physical,magical,dodge,crit,critDamage,speed,penetration,magicPenetration,accuracy;
        }
        [Serializable] sealed class TheoryHit
        {
            public string stage,enemy,hero; public int level,profile,material;
            public double physical,magical,incoming,physicalHits,magicalHits,incomingHits;
            public float rawPhysicalMitigation,rawMagicalMitigation;
        }
        [Serializable] sealed class TheoryDot
        {
            public string status,note;public int turns,rawPerTurn;
            public float applications,payload,expectedDamage;
        }
        [Serializable] sealed class TheorySkill
        {
            public string stage,source,target,skill,type; public int level,profile,material,rage,ap,rp;
            public float direct,hit,crit,ignore,regular,noPenRegular,unarmoredRegular; public List<TheoryDot> dots=new();
        }
        [Serializable] sealed class TheoryExport
        {
            public string method="Read-only static expectations. No combat engine, AI, turns, casts or RNG. x1 ATK index and single-target skill previews are separate.";
            public List<TheoryStats> stats=new(); public List<TheoryHit> hits=new();
            public List<TheorySkill> skills=new();public List<AnalyticalRoster> rosters=new();
        }
        static TheoryStats TheorySnapshot(BattleUnit unit,string stage,int profile,int material=0)
        {
            CombatTurnSystem.RefreshMaxActionPoints(new CombatSession(),unit,true);
            var c=unit.Character;var s=c.Stats;
            return new TheoryStats{stage=stage,id=c.Definition.Id,level=c.Level,profile=profile,material=material,hp=c.CurrentHealth,
                gear=string.Join("|",c.Equipment.GetEquippedItems().Select(g=>g.Definition.Id+":"+g.ItemLevel+":"+g.Quality)),
                min=s.MinAttack.GetValue(),max=s.MaxAttack.GetValue(),physical=s.PhysicalDefense.GetValue(),magical=s.MagicalDefense.GetValue(),
                dodge=s.Dodge.GetValue(),crit=s.CritChance.GetValue(),critDamage=s.CritDamage.GetValue(),speed=s.Speed.GetValue(),
                penetration=s.PhysicalDefensePenetration.GetValue(),magicPenetration=s.MagicalDefensePenetration.GetValue(),accuracy=c.WeaponAccuracy,
                maxResource=c.ResourcePool.MaxValue,regeneration=c.ResourcePool.Regeneration,maxAp=unit.MaxActionPoints};
        }
        static float TheoryHitChance(BattleUnit source,BattleUnit target,SkillData skill,SkillEffectData effect)
            => effect.AlwaysHits || CombatDamageSystem.IsGuaranteedHitDamageType(effect.DamageType)
                ? 1f : CombatDamageSystem.CalculateSkillHitChance(source,target,skill);
        static float TheoryTrigger(BattleUnit source,BattleUnit target,SkillData skill,SkillEffectData effect,int index)
        {
            var probability=1f;
            foreach(var condition in effect.Conditions ?? Array.Empty<SkillEffectConditionData>())
            {
                if(condition==null || condition.ConditionType==EffectConditionType.Always)continue;
                if(condition.ConditionType!=EffectConditionType.ReferencedEffectSucceeded || condition.ReferencedEffectIndex<0 || condition.ReferencedEffectIndex>=index)return -1;
                var prior=PassiveEffectCombatExecutionSystem.ResolveEffect(source,skill,skill.Effects[condition.ReferencedEffectIndex]);
                if(prior.EffectType!=EffectType.DealDamage)return -1;
                var hit=TheoryHitChance(source,target,skill,prior);
                probability*=1-Mathf.Pow(1-hit,prior.DamageHitCount)*Mathf.Pow(1-prior.AdditionalHitChance*hit,prior.AdditionalHitTrials);
            }
            return probability;
        }
        static TheoryDot TheoryPeriodic(BattleUnit source,BattleUnit target,SkillData skill,SkillEffectData effect,int index)
        {
            var embedded=effect.EffectType==EffectType.DealDamage;
            var id=embedded?effect.OnHitStatusId:effect.StatusId;
            if(id is not ("bleed" or "poison" or "burn"))return null;
            var trigger=TheoryTrigger(source,target,skill,effect,index);
            if(trigger<0)return new TheoryDot{status=id,note="Conditional status omitted: requires combat state."};
            var dot=new TheoryDot{status=id,turns=embedded?effect.OnHitStatusDurationTurns:effect.DurationTurns,
                rawPerTurn=StatusEffectSystem.CalculateStatusDamagePerTurn(source,effect)};
            // On-hit status damage uses the same status multiplier; its ID is not StatusId.
            if(embedded && dot.rawPerTurn==0)
                dot.rawPerTurn=CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AverageAttack,
                    effect.StatusDamageMultiplier*(id=="poison"?ScoutCombatSystem.PoisonMultiplier(source):1));
            if(dot.turns<=0 || dot.rawPerTurn<=0)return null;
            var type=id=="poison"?ResistanceType.Poison:id=="burn"?ResistanceType.Fire:ResistanceType.Bleed;
            var delta=0f;var modifierChance=0f;
            // Evaluate a preceding same-target resistance debuff as weighted branches,
            // without applying a status or altering authored/runtime target stats.
            for(var i=0;i<index;i++)
            {
                var prior=PassiveEffectCombatExecutionSystem.ResolveEffect(source,skill,skill.Effects[i]);
                if(prior.EffectType!=EffectType.ModifyResistance || prior.TargetModeIndex!=effect.TargetModeIndex
                    || prior.ResistanceType!=type && prior.ResistanceType!=ResistanceType.All)continue;
                var gate=TheoryTrigger(source,target,skill,prior,i);if(gate<0)continue;
                // A shared preceding-hit condition is already included in the outer trigger.
                var sameGate=prior.Conditions.All(c=>c==null || c.ConditionType==EffectConditionType.Always
                    || effect.Conditions.Any(d=>d.ConditionType==c.ConditionType && d.ReferencedEffectIndex==c.ReferencedEffectIndex));
                delta=prior.ModifierValue;modifierChance=Mathf.Clamp01(prior.BaseChance)*(sameGate?1:gate)
                    *(prior.AlwaysHits?1:CombatDamageSystem.CalculateSkillHitChance(source,target,skill));
                break;
            }
            float FullDuration(float modifier)
            {
                var remaining=dot.rawPerTurn*dot.turns;
                if(id=="burn")remaining=Mathf.RoundToInt(remaining*(1+.02f*MageCombatSystem.Points(source.Character,MageCombatSystem.Fire))*(1+.1f*MageCombatSystem.F(source,"arcana")));
                var total=0f;
                for(var turns=dot.turns;turns>0;turns--)
                {
                    var raw=Mathf.CeilToInt(remaining/(float)turns);remaining-=raw;
                    var adjusted=id=="burn" && raw<target.Character.Stats.MaxHealth.GetValue()*.1f
                        ? Mathf.RoundToInt(raw*(1+.15f*MageCombatSystem.F(source,"sanguine"))):raw;
                    if(id=="burn" && MageCombatSystem.F(target,"fire_resistance")>0)adjusted=Mathf.RoundToInt(adjusted*.75f);
                    total+=Mathf.RoundToInt(adjusted*DamageOverTimePassiveEffectProcessor.CalculateDamageMultiplier(ModifyResistanceSystem.GetEffectiveResistance(target,type,modifier)));
                }
                return total;
            }
            var baseChance=embedded?effect.OnHitStatusChance:effect.BaseChance;
            var ordinary=StatusEffectSystem.CalculateStatusApplicationChance(source,target,id,baseChance);
            var debuffed=StatusEffectSystem.CalculateStatusApplicationChance(source,target,id,baseChance,delta);
            var hits=embedded?TheoryHitChance(source,target,skill,effect)*effect.ExpectedHitCount
                :effect.AlwaysHits?1:CombatDamageSystem.CalculateSkillHitChance(source,target,skill);
            dot.applications=trigger*hits*Mathf.Lerp(ordinary,debuffed,modifierChance);
            dot.payload=FullDuration(0);
            dot.expectedDamage=trigger*hits*((1-modifierChance)*ordinary*FullDuration(0)+modifierChance*debuffed*FullDuration(delta));
            dot.note="Single application, full duration, no cleanse/death/refresh/spread; proc and hit probabilities included.";
            return dot;
        }
        static TheorySkill TheoryCard(BattleUnit source,BattleUnit target,SkillData skill,string stage,int profile,int rage=0,int material=0)
        {
            var session=new CombatSession();session.Units.Add(source);session.Units.Add(target);
            CombatTurnSystem.RefreshMaxActionPoints(session,source,true);
            // Compare melee skills at adjacent front contact, ranged skills at four tiles.
            var range=skill.TargetModes.Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(source,skill,m).AbsoluteRange.MaxRange).DefaultIfEmpty(1).Max();
            source.Position=new GridPosition(2,2);target.Position=new GridPosition(range<=1?3:6,2);
            target.ScoutState.Facing=Vector2Int.left;
            var row=new TheorySkill{stage=stage,source=source.Character.Definition.Id,target=target.Character.Definition.Id,skill=skill.SkillId,
                level=source.Character.Level,profile=profile,material=material,rage=rage,ap=CombatActionCostCalculator.CalculateSkillCost(source,skill),
                rp=skill.SkillId==WarriorDischargeRules.SkillId?WarriorDischargeRules.Cost(source):PassiveEffectCombatExecutionSystem.ResolveResourceCost(source,skill),
                ignore=WarriorDischargeRules.DefenseIgnore(source,skill)};
            var damage=skill.Effects.Where(e=>e.EffectType==EffectType.DealDamage).ToArray();
            row.type=string.Join("+",damage.Select(e=>e.DamageType.ToString()).Distinct());
            row.direct=damage.Select(e=>e.TargetModeIndex).Distinct().Sum(mode=>CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,source,target,skill,mode));
            row.hit=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill).HitChance;
            row.crit=damage.Length>0?CombatDamageSystem.PreviewCriticalChance(session,source,target,skill,damage[0].DamageType):0;
            if(skill.SkillId=="skill.warrior.cut" && target.Character.Definition.Id=="enemy.catacombs.defender")
            {
                float Mid(BattleUnit t)
                {
                    var value=CombatSkillPreviewCalculator.CalculateTargetPreview(source,t,skill).Damage;
                    return (value.Minimum+value.Maximum)*.5f;
                }
                row.regular=Mid(target);
                ref var stat=ref source.Character.Stats.PhysicalDefensePenetration;var original=stat;
                try{stat.SetEffectiveValue(0);row.noPenRegular=Mid(target);}
                finally{stat=original;}
                var d=target.Character.Definition;
                var unarmored=PointUnit(new CharacterInstanceFactory().Create(d,d.ClassData,target.Character.Level),false);
                unarmored.Position=target.Position;unarmored.ScoutState.Facing=target.ScoutState.Facing;
                unarmored.Character.Stats.PhysicalDefense.SetEffectiveValue(0);unarmored.Character.Stats.MagicalDefense.SetEffectiveValue(0);
                row.unarmoredRegular=Mid(unarmored);
            }
            for(var i=0;i<skill.Effects.Count;i++)
            {
                var e=PassiveEffectCombatExecutionSystem.ResolveEffect(source,skill,skill.Effects[i]);
                if(e.EffectType is not (EffectType.DealDamage or EffectType.ApplyStatus))continue;
                var dot=TheoryPeriodic(source,target,skill,e,i);if(dot!=null)row.dots.Add(dot);
            }
            return row;
        }
        [TestCase(1),TestCase(10),TestCase(20),TestCase(30),TestCase(40),TestCase(48),TestCase(50)]
        [Explicit("Read-only role comparison: static stat/hit/skill expectations, without battle simulation or AI.")]
        [Timeout(600000)]
        public void AuditEnemyRoleTheory(int level)
        {
            File.AppendAllText(Path.Combine(RoleFolder,"theory-progress.log"),DateTime.UtcNow.ToString("O")+" START L"+level+Environment.NewLine);
            var report=new TheoryExport();var factory=new CharacterInstanceFactory();
            var current=PointCatalog.CharacterDefinitions.Where(d=>d!=null && File.Exists(Path.Combine(RoleFolder,"before-definitions",d.Id+".json"))).OrderBy(d=>d.Id).ToArray();
            Assert.That(current,Has.Length.EqualTo(23));
            var before=current.Select(d=>
            {
                var clone=ScriptableObject.CreateInstance<CharacterDefinition>();temporary.Add(clone);
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(RoleFolder,"before-definitions",d.Id+".json")),clone);return clone;
            }).ToArray();
            var immutable=current.ToDictionary(d=>d,EditorJsonUtility.ToJson);
            {
                foreach(var profile in new[]{0,1})foreach(var heroIndex in Enumerable.Range(0,4))foreach(var material in Enumerable.Range(0,4))
                {
                    // Identical level-fit Common gear, quality, jewelry and legal tree
                    // builder as the preceding role-stat proposal; no unique selection.
                    var hero=PointUnit(EnemyScalingBalanceTests.Hero(heroIndex,level,material:material,treeIndex:profile),true);
                    report.stats.Add(TheorySnapshot(hero,"hero",profile,material));
                    var heroOriginal=JsonUtility.ToJson(TheorySnapshot(hero,"hero",profile,material));
                    foreach(var stage in new[]{"before","after"})foreach(var definition in stage=="before"?before:current)
                    {
                        var enemy=PointUnit(factory.Create(definition,definition.ClassData,level),false);
                        var randomBefore=UnityEngine.Random.state;
                        if(profile==0 && heroIndex==0 && material==0)report.stats.Add(TheorySnapshot(enemy,stage,-1,-1));
                        if(definition.Id.Contains("spectral_knight"))continue; // Three damaging hits, not HP/damage.
                        var physical=PointExpected(hero,enemy,DamageType.Physical);var magical=PointExpected(hero,enemy,DamageType.Magical);
                        var incomingType=enemy.Character.GetBasicAttackSkill().Effects.FirstOrDefault(e=>e.EffectType==EffectType.DealDamage)?.DamageType
                            ?? enemy.Character.GetKnownSkills().SelectMany(s=>s.Effects).FirstOrDefault(e=>e.EffectType==EffectType.DealDamage)?.DamageType ?? DamageType.Physical;
                        var incoming=PointExpected(enemy,hero,incomingType);
                        var attack=(hero.Character.Stats.MinAttack.GetValue()+hero.Character.Stats.MaxAttack.GetValue())*.5f;
                        var ep=enemy.Character.Stats.PhysicalDefense.GetValue();var em=enemy.Character.Stats.MagicalDefense.GetValue();
                        report.hits.Add(new TheoryHit{stage=stage,enemy=definition.Id,hero=hero.Character.Definition.Id,level=level,profile=profile,material=material,
                            physical=physical,magical=magical,incoming=incoming,physicalHits=enemy.Character.CurrentHealth/physical,magicalHits=enemy.Character.CurrentHealth/magical,
                            incomingHits=hero.Character.CurrentHealth/incoming,rawPhysicalMitigation=ep/(attack+ep),rawMagicalMitigation=em/(attack+em)});
                        // Actual ordinary-enemy skills plus all known damaging hero skills, one target only.
                        if(!SkillPointEnemyIds.Contains(definition.Id))continue;
                        foreach(var skill in hero.Character.GetKnownSkills().Where(s=>(material==0 || s==hero.Character.GetBasicAttackSkill()) && s.Effects.Any(e=>e.EffectType==EffectType.DealDamage || e.StatusDamageMultiplier>0)))
                        {
                            if(skill.SkillId==WarriorDischargeRules.SkillId)
                            {
                                foreach(var rage in new[]{20,50,Math.Min(100,hero.Character.ResourcePool.MaxValue)}.Where(r=>r<=hero.Character.ResourcePool.MaxValue).Distinct())
                                {
                                    hero.Character.ResourcePool.CurrentValue=rage;new CharacterStatPipelineSystem().Refresh(hero.Character);
                                    report.skills.Add(TheoryCard(hero,enemy,skill,stage,profile,rage,material));
                                }
                                hero.Character.ResourcePool.CurrentValue=0;new CharacterStatPipelineSystem().Refresh(hero.Character);
                            }
                            else report.skills.Add(TheoryCard(hero,enemy,skill,stage,profile,material:material));
                        }
                        foreach(var skill in enemy.Character.GetKnownSkills().Where(s=>(material==0 || s==enemy.Character.GetBasicAttackSkill()) && s.Effects.Any(e=>e.EffectType==EffectType.DealDamage || e.StatusDamageMultiplier>0)))
                            report.skills.Add(TheoryCard(enemy,hero,skill,stage,profile,material:material));
                        Assert.That(UnityEngine.Random.state,Is.EqualTo(randomBefore),"Theory calculation must never draw combat RNG.");
                        hero.Position=new GridPosition(2,2);
                    }
                    Assert.That(JsonUtility.ToJson(TheorySnapshot(hero,"hero",profile,material)),Is.EqualTo(heroOriginal),"Static audit must restore resource-derived hero stats.");
                }
            }
            foreach(var biome in new[]{"forest","catacombs"})foreach(var encounter in PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition").EncounterDefinitions)
                report.rosters.Add(new AnalyticalRoster{biome=biome,id=encounter.Id,enemies=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray()});
            foreach(var pair in immutable)Assert.That(EditorJsonUtility.ToJson(pair.Key),Is.EqualTo(pair.Value),"Read-only audit changed "+pair.Key.Id);
            var defenderReduction=report.skills.Where(r=>r.stage=="after" && r.skill=="skill.warrior.cut" && r.target=="enemy.catacombs.defender")
                .Average(r=>1-r.noPenRegular/r.unarmoredRegular);
            var defenderArmor=report.stats.Single(s=>s.stage=="after"&&s.id=="enemy.catacombs.defender").physical;
            var paladinArmor=report.stats.Where(s=>s.stage=="hero"&&s.id=="character.paladin").Select(s=>s.physical).ToArray();
            Assert.That(defenderArmor,Is.InRange(paladinArmor.Min()*.75f,paladinArmor.Max()*2f),
                "Defender specialization follows equipped same-level paladins, not a fixed mitigation target.");
            File.WriteAllText(Path.Combine(RoleFolder,"theory-level-"+level+".json"),JsonUtility.ToJson(report,true));
            File.AppendAllText(Path.Combine(RoleFolder,"theory-progress.log"),DateTime.UtcNow.ToString("O")+" DONE L"+level+Environment.NewLine);
        }
    }
}
