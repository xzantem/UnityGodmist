using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Systems.Items;
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
        [Serializable] sealed class HealthComparisonRow
        {
            public int level,profile,hero,reduction,hp,originalHp,ap,resource,resourceMax,regen,startResource;
            public string enemy,name,biome,role,heroName,skill,gear;
            public float damage,hit,actionsIndex,apIndex;
        }
        [Serializable] sealed class HealthDotRow
        {
            public int level,profile,reduction,hp,trials,directKills,delayedKills,ap,resource,resourceMax,startResource,regen;
            public string enemy,name,skill,heroName; public bool available;
            public float direct,dot,meanTicks,statusApplied,manaSpent;
        }
        [Serializable] sealed class HealthComparisonReport
        {
            public string method="Read-only: current authored assets/factory/skill preview. HP reductions on detached instances only. Basic indices = HP / production expected direct damage (not actual battle turns). Same-level frontal attacks: melee adjacent, ranged up to 4 legal tiles; starter unchanged/no SP vs current production Common Normal level-fit parts/legal primary tree; no jewelry, upgrades or unique items. No enemy AI, movement, incoming attacks, allies or boss phases simulated. DoT probe: legally learned skills only (scout developed profile prioritizes blade dancer); unavailable skills recorded without casts. One real cast with only the missing cast cost supplied (mage retains natural full starting mana), then real scheduled turns until DoT expires; paired seeds; no other actions. Boss roots static only, spectral knight excluded (hit-count mechanic).";
            public List<HealthComparisonRow> basics=new();public List<HealthDotRow> dots=new();
        }
        static string HealthComparisonFolder=>Path.GetFullPath("../LocalHandoffs/enemy-health-comparison-20261103");
        static string[] ComparisonBosses=>new[]{"enemy.catacombs.dead_king","enemy.catacombs.skeleton_slayer","enemy.forest.infected_ent","enemy.forest.bandit_leader"};
        static CharacterInstance HealthComparisonHero(int index,int level,int profile,bool dotBuild=false)
        {
            var definition=PointHeroDefinition(index);
            var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
            var starter=profile==0 || level<5;
            foreach(var slot in new[]{EquipmentSlot.Weapon,EquipmentSlot.Armor})
            {
                var type=slot==EquipmentSlot.Weapon?ItemType.Weapon:ItemType.Armor;
                var item=PointCatalog.ItemDefinitions.OfType<EquipmentItemDefinition>().Where(d=>d.RequiredClass==hero.ClassData && d.ItemType==type
                    && d.IsStarterEquipment==starter && d is not UniqueEquipmentDefinition).OrderBy(d=>d.Id).First();
                var kinds=slot==EquipmentSlot.Weapon?new[]{EquipmentPartKind.WeaponHead,EquipmentPartKind.WeaponBinder,EquipmentPartKind.WeaponHandle}
                    :new[]{EquipmentPartKind.ArmorPlate,EquipmentPartKind.ArmorBinder,EquipmentPartKind.ArmorBase};
                var parts=kinds.Select(kind=>PointCatalog.EquipmentPartDefinitions.Where(p=>p.RequiredClass==hero.ClassData && p.Kind==kind
                    && p.IsStarterOnly==starter && (int)p.LevelBracket<=level).OrderByDescending(p=>p.LevelBracket).ThenBy(p=>p.Id).First()).ToArray();
                var gear=new EquipmentGenerationService(new System.Random(42)).GenerateFromSelectedParts(item,parts,EquipmentQuality.Normal,RarityTier.Common);
                Assert.That(gear.RequiredLevel,Is.LessThanOrEqualTo(level));hero.Equipment.SetItem(slot,gear);
            }
            if(profile==1 && level>1)
            {
                hero.AvailableSkillPoints=PointBudget(level);var progression=new SkillTreeProgressionSystem();
                var trees=hero.ClassData.SkillTrees.Where(t=>t!=null).OrderBy(t=>dotBuild && index==1 && t.Id=="tree.scout.blade_dancer"?0:1).ToArray();
                while(hero.AvailableSkillPoints>0)
                {
                    var invested=false;
                    foreach(var tree in trees)
                    {
                        foreach(var node in tree.Nodes.OrderBy(n=>n.Tier).ThenBy(n=>n.Column))
                            if(progression.TryInvest(hero,tree.Id,node.Id)){invested=true;break;}
                        if(invested)break;
                    }
                    Assert.That(invested,Is.True,"Only real earned points invested legally at this level.");
                }
            }
            PointRefresh(hero);return hero;
        }
        CharacterDefinition HealthProbeDefinition(CharacterDefinition original,int hp)
        {
            var copy=UnityEngine.Object.Instantiate(original);temporary.Add(copy);
            // Fixed precomputed HP at this probe's level, durable across pipeline refreshes.
            // Only the detached clone is edited; no authoring/setup or asset save runs.
            var data=new SerializedObject(copy);
            data.FindProperty("<BaseStats>k__BackingField").FindPropertyRelative("<MaxHealth>k__BackingField").intValue=hp;
            var curve=data.FindProperty("<GrowthData>k__BackingField").FindPropertyRelative("<HealthCurve>k__BackingField");curve.arraySize=2;
            for(var i=0;i<2;i++)
            {var point=curve.GetArrayElementAtIndex(i);point.FindPropertyRelative("<Level>k__BackingField").intValue=i==0?1:50;point.FindPropertyRelative("<MaxHealth>k__BackingField").intValue=hp;}
            data.ApplyModifiedPropertiesWithoutUndo();return copy;
        }
        [Test,Explicit("Read-only current HP vs -20/-25/-30 early-game audit; no balance authoring.")]
        [Timeout(900000)]
        public void AuditEarlyHealthComparison() => RunEarlyHealthComparison(true);

        [Test,Explicit("Read-only static retest at correct melee/ranged distance; preserves completed DoT probes.")]
        [Timeout(300000)]
        public void AuditEarlyHealthComparisonBasicPositions() => RunEarlyHealthComparison(false);

        void RunEarlyHealthComparison(bool includeDots)
        {
            Directory.CreateDirectory(HealthComparisonFolder);var report=new HealthComparisonReport();
            if(!includeDots)report.dots=JsonUtility.FromJson<HealthComparisonReport>(File.ReadAllText(Path.Combine(HealthComparisonFolder,"comparison.json"))).dots;
            var random=UnityEngine.Random.state;
            try
            {
                var definitions=PointEnemyDefinitions.Concat(ComparisonBosses.Select(id=>PointCatalog.CharacterDefinitions.Single(d=>d.Id==id))).ToArray();
                foreach(var definition in definitions)originals[definition]=EditorJsonUtility.ToJson(definition);
                foreach(var level in new[]{1,2,3,5,8,10})
                {
                    File.AppendAllText(Path.Combine(HealthComparisonFolder,"progress.txt"),"START L"+level+Environment.NewLine);
                    foreach(var profile in new[]{0,1})foreach(var heroIndex in Enumerable.Range(0,4))
                    {
                        var character=HealthComparisonHero(heroIndex,level,profile);
                        var hero=PointUnit(character,true);var basic=character.GetBasicAttackSkill();
                        foreach(var definition in definitions)
                        {
                            var target=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,level),false);
                            var range=basic.TargetModes.Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(hero,basic,m).AbsoluteRange.MaxRange).DefaultIfEmpty(1).Max();
                            hero.Position=new GridPosition(2,2);target.Position=new GridPosition(2+Math.Min(4,Math.Max(1,range)),2);target.ScoutState.Facing=Vector2Int.left;
                            var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new List<BattleUnit>{hero,target},ActiveUnit=hero};
                            hero.WarriorState.Session=session;target.WarriorState.Session=session;
                            CombatTurnSystem.RefreshMaxActionPoints(session,hero,true);
                            var rng=UnityEngine.Random.state;
                            var damage=basic.Effects.Select(e=>e.TargetModeIndex).Distinct().Sum(mode=>CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,hero,target,basic,mode));
                            var hit=CombatSkillPreviewCalculator.CalculateTargetPreview(hero,target,basic).HitChance;
                            Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));Assert.That(damage,Is.GreaterThan(0));
                            foreach(var reduction in new[]{0,20,25,30})
                            {
                                var hp=Mathf.Max(1,Mathf.RoundToInt(target.Character.MaximumHealth*(1-reduction/100f)));
                                var ap=CombatActionCostCalculator.CalculateSkillCost(hero,basic);Assert.That(ap,Is.GreaterThan(0));
                                report.basics.Add(new HealthComparisonRow{level=level,profile=profile,hero=heroIndex,reduction=reduction,hp=hp,originalHp=target.Character.MaximumHealth,
                                    enemy=definition.Id,name=definition.DisplayName,biome=definition.Id.Contains("catacombs")?"catacombs":"forest",role=ComparisonBosses.Contains(definition.Id)?"boss":"ordinary",
                                    heroName=character.Definition.DisplayName,skill=basic.SkillId,gear=string.Join(" | ",character.Equipment.GetEquippedItems().Select(g=>g.Definition.Id+" L"+g.ItemLevel+" "+g.Quality)),
                                    damage=damage,hit=hit,ap=ap,resource=PassiveEffectCombatExecutionSystem.ResolveResourceCost(hero,basic),resourceMax=character.ResourcePool.MaxValue,
                                    regen=character.ResourcePool.Regeneration,startResource=character.ResourcePool.CurrentValue,actionsIndex=hp/damage,apIndex=hp/damage*ap});
                            }
                        }
                    }
                    File.WriteAllText(Path.Combine(HealthComparisonFolder,"comparison.json"),JsonUtility.ToJson(report,true));
                    if(!includeDots || level is not (1 or 3 or 5 or 10))continue;
                    var probeIds=new[]{"swordsman","defender","occultist","ghoul","bandit_thug","rabid_wolf","elf_druid","infected_tree"};
                    foreach(var profile in new[]{0,1})foreach(var heroIndex in new[]{1,2})foreach(var definition in PointEnemyDefinitions.Where(d=>probeIds.Contains(d.Id.Split('.').Last())))
                    foreach(var reduction in new[]{0,20,25,30})
                    {
                        var original=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                        var hp=Mathf.Max(1,Mathf.RoundToInt(original.MaximumHealth*(1-reduction/100f)));
                        var detached=HealthProbeDefinition(definition,hp);
                        var row=new HealthDotRow{level=level,profile=profile,reduction=reduction,hp=hp,enemy=definition.Id,name=definition.DisplayName,trials=32,
                            skill=heroIndex==2?"skill.mage.fireball":"skill.scout.poisoned_dagger"};
                        var template=HealthComparisonHero(heroIndex,level,profile,dotBuild:true);
                        row.available=template.GetKnownSkills().Any(s=>s.SkillId==row.skill);
                        if(!row.available){row.trials=0;report.dots.Add(row);continue;}
                        for(var seed=0;seed<row.trials;seed++)
                        {
                            UnityEngine.Random.InitState(770300+seed);
                            var character=HealthComparisonHero(heroIndex,level,profile,dotBuild:true);
                            row.heroName=character.Definition.DisplayName;row.startResource=character.ResourcePool.CurrentValue;row.regen=character.ResourcePool.Regeneration;
                            var chosenSkill=character.GetKnownSkills().Single(s=>s.SkillId==row.skill);
                            var required=PassiveEffectCombatExecutionSystem.ResolveResourceCost(PointUnit(character,true),chosenSkill);
                            character.ResourcePool.CurrentValue=Math.Max(character.ResourcePool.CurrentValue,required);
                            new CharacterStatPipelineSystem().Refresh(character);
                            var caster=PointUnit(character,true);var target=PointUnit(new CharacterInstanceFactory().Create(detached,detached.ClassData,level),false);
                            caster.Position=new GridPosition(2,2);target.Position=new GridPosition(heroIndex==1?3:4,2);target.ScoutState.Facing=Vector2Int.left;
                            Assert.That(target.Character.MaximumHealth,Is.EqualTo(hp));
                            var skill=character.GetKnownSkills().Single(s=>s.SkillId==row.skill);
                            CombatTurnSystem.RefreshMaxActionPoints(new CombatSession(),caster,true);
                            row.ap=CombatActionCostCalculator.CalculateSkillCost(caster,skill);row.resource=PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill);row.resourceMax=character.ResourcePool.MaxValue;
                            var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new List<BattleUnit>{caster,target}};
                            var result=new Result();var direct=0;var dot=0;
                            session.DamageApplied+=(a,t,n,type,id)=>{if(t==target){if(type is DamageType.Burn or DamageType.Poison or DamageType.Bleed)dot+=n;else direct+=n;}};
                            using(var engine=new Engine(session,result))
                            {
                                Assert.That(engine.CastProbe(caster,skill,target),Is.True,row.skill+" L"+level);
                                if(target.Character.LifeState!=CharacterLifeState.Alive)row.directKills++;
                                bool ActiveDot()=>target.Character.PassiveEffects.Any(e=>StatusEffectSystem.IsDamageOverTimeStatus(e.EffectId)&&PassiveEffectLifecycleSystem.IsActive(e));
                                if(ActiveDot())row.statusApplied++;
                                var ticks=0;
                                while(target.Character.LifeState==CharacterLifeState.Alive&&ActiveDot()&&ticks<12){engine.TickProbeTarget(target);ticks++;}
                                Assert.That(target.Character.LifeState!=CharacterLifeState.Alive||!ActiveDot(),Is.True);
                                Assert.That(target.Character.MaximumHealth,Is.EqualTo(hp),"HP candidate survives refreshes.");
                                if(target.Character.LifeState!=CharacterLifeState.Alive)row.delayedKills++;
                                row.meanTicks+=ticks;
                            }
                            row.direct+=direct;row.dot+=dot;row.manaSpent+=result.heroManaSpent;
                        }
                        row.direct/=row.trials;row.dot/=row.trials;row.meanTicks/=row.trials;row.statusApplied/=row.trials;row.manaSpent/=row.trials;
                        report.dots.Add(row);
                    }
                    File.WriteAllText(Path.Combine(HealthComparisonFolder,"comparison.json"),JsonUtility.ToJson(report,true));
                    File.AppendAllText(Path.Combine(HealthComparisonFolder,"progress.txt"),"DONE L"+level+Environment.NewLine);
                }
                Assert.That(report.basics.Count,Is.EqualTo(3456));Assert.That(report.dots.Count,Is.EqualTo(512));
            }
            finally{UnityEngine.Random.state=random;}
        }
    }
}
