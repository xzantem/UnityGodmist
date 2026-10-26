using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Combat;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string RoleFolder=>Path.GetFullPath("../LocalHandoffs/enemy-role-implementation-20261025");
        static string RoleStage=>File.ReadAllText(Path.Combine(RoleFolder,"stage.txt")).Trim();
        readonly Dictionary<string,CharacterDefinition> roleEquipmentDefinitions=new();
        BattleUnit[] RoleEquipmentReference(int level)
        {
            return PointEnemyDefinitions.Select(original=>
            {
                if(!roleEquipmentDefinitions.TryGetValue(original.Id,out var clone)||clone==null)
                {
                    clone=ScriptableObject.CreateInstance<CharacterDefinition>();temporary.Add(clone);
                    EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(RoleFolder,"before-definitions",original.Id+".json")),clone);
                    roleEquipmentDefinitions[original.Id]=clone;
                }
                return PointUnit(new CharacterInstanceFactory().Create(clone,clone.ClassData,level),false);
            }).ToArray();
        }
        [Serializable] sealed class RoleDamage {public string actor,type;public int amount;}
        [Serializable] sealed class RoleCast {public string actor,skill;public int count,ap,rp;}
        [Serializable] sealed class RoleInput
        {
            public string instance,id,skills,gear,ranks;
            public float hp,min,max,physical,magical,dodge,penetration,magicPenetration;
        }
        [Serializable] sealed class RoleTelemetry
        {
            public string stage,biome,route;public int level,profile,seed;
            public List<RoleInput> units=new();public List<RoleDamage> damage=new(),hpDamage=new();public List<RoleCast> casts=new();
            public Result result;public int misses,dodges;
        }
        static void RoleLoadout(CharacterInstance hero)
        {
            if(hero.ClassData.Id!="class.warrior"||!hero.GetKnownSkills().Any(s=>s.SkillId==WarriorDischargeRules.SkillId))return;
            var ids=new[]{hero.GetBasicAttackSkill().SkillId,WarriorDischargeRules.SkillId}
                .Concat(hero.GetActiveSkills().Select(s=>s.SkillId)).Distinct().Take(hero.ActiveSkillLoadout.Capacity).ToArray();
            AssertBuildLoadout(hero,ids);
        }
        static RoleInput RoleSnapshot(BattleUnit unit)
        {
            var c=unit.Character;var s=c.Stats;
            return new RoleInput{instance=c.CharacterId,id=c.Definition.Id,hp=s.MaxHealth.GetValue(),min=s.MinAttack.GetValue(),max=s.MaxAttack.GetValue(),
                physical=s.PhysicalDefense.GetValue(),magical=s.MagicalDefense.GetValue(),dodge=s.Dodge.GetValue(),penetration=s.PhysicalDefensePenetration.GetValue(),
                magicPenetration=s.MagicalDefensePenetration.GetValue(),skills=string.Join("|",c.GetActiveSkills().Select(k=>k.SkillId)),
                gear=string.Join("|",c.Equipment.GetEquippedItems().Select(g=>g.Definition.Id+":"+g.ItemLevel+":"+g.CurrentRarity)),
                ranks=string.Join("|",c.SkillTreeProgress.SelectMany(t=>t.NodeInvestments.Select(n=>t.TreeId+"/"+n.NodeId+":"+n.Ranks)))};
        }
        static void TrackRoles(Engine engine,RoleTelemetry report)
        {
            engine.AuditWallClockLimitSeconds=40;engine.AuditAiTimeoutSeconds=5;engine.AuditAiSliceLimit=10000;
            foreach(var unit in engine.Session.Units.Where(u=>u.IsPlayerSide))RoleLoadout(unit.Character);
            report.units.AddRange(engine.Session.Units.Select(RoleSnapshot));
            engine.Session.DamageApplied+=(source,target,amount,type,action)=>
            {
                if(target.IsPlayerSide)return;
                var actor=source?.IsPlayerSide==true?source.Character.Definition.Id:"unattributed-or-enemy";
                var row=report.damage.FirstOrDefault(d=>d.actor==actor&&d.type==type.ToString());
                if(row==null){row=new RoleDamage{actor=actor,type=type.ToString()};report.damage.Add(row);}row.amount+=amount;
            };
            var prior=engine.AuditChosenPlan;
            engine.AuditChosenPlan=(actor,skill,targets,score)=>
            {
                prior?.Invoke(actor,skill,targets,score);
                var row=report.casts.FirstOrDefault(c=>c.actor==actor.Character.Definition.Id&&c.skill==skill.SkillId);
                if(row==null){row=new RoleCast{actor=actor.Character.Definition.Id,skill=skill.SkillId};report.casts.Add(row);}
                row.count++;
                // Resource for Discharge is spent before this observer: exact RP is recorded by feedback below.
            };
            var feedback=engine.AuditFeedback;
            engine.AuditFeedback=value=>
            {
                feedback?.Invoke(value);
                if(value.Type==CombatFeedbackType.Damage&&value.Target?.IsPlayerSide==false)
                {
                    var actor=value.Source?.IsPlayerSide==true?value.Source.Character.Definition.Id:"unattributed-or-enemy";
                    if(value.Source==null)
                    {
                        var origins=engine.RoleDotSourceIds(value.Target).Distinct().ToArray();
                        if(origins.Length==1)actor=report.units.FirstOrDefault(u=>u.instance==origins[0])?.id??"unattributed-or-enemy";
                        else if(origins.Length>1)actor="mixed-dot-sources";
                    }
                    var damage=report.hpDamage.FirstOrDefault(d=>d.actor==actor&&d.type==value.DamageType.ToString());
                    if(damage==null){damage=new RoleDamage{actor=actor,type=value.DamageType.ToString()};report.hpDamage.Add(damage);}damage.amount+=value.Amount;
                }
                if(value.Type is CombatFeedbackType.ResourceSpend or CombatFeedbackType.ActionPointSpend&&value.Target?.IsPlayerSide==true)
                {
                    var row=report.casts.FirstOrDefault(c=>c.actor==value.Target.Character.Definition.Id&&c.skill=="resource-total");
                    if(row==null){row=new RoleCast{actor=value.Target.Character.Definition.Id,skill="resource-total"};report.casts.Add(row);}
                    if(value.Type==CombatFeedbackType.ResourceSpend)row.rp+=value.Amount;else row.ap+=value.Amount;
                }
                if(value.Type.ToString()=="Miss")report.misses++;
                if(value.Type.ToString()=="Dodge")report.dodges++;
            };
        }
        sealed partial class Engine
        {
            internal IEnumerable<string> RoleDotSourceIds(BattleUnit target)=>knockdownTrace?.tickingUnit==target
                ?knockdownTrace.tickingEffect?.Sources.Select(s=>s.SourceCharacterId)??Array.Empty<string>():Array.Empty<string>();
        }
        static IEnumerable<TestCaseData> RoleRouteCases()
        {
            foreach(var level in new[]{10,48})foreach(var biome in new[]{"forest","catacombs"})foreach(var profile in new[]{0,1})
                yield return new TestCaseData(level,biome,profile,profile);
        }
        [TestCaseSource(nameof(RoleRouteCases)),Explicit("Paired full routes using current live definitions, legal builds and production combat; no asset changes.")]
        public void AuditRoleBalanceRoutes(int level,string biome,int profile,int seed)
            =>RoleRoutes(level,biome,profile,seed,false);
        [TestCase(10,"forest")][TestCase(10,"catacombs")][TestCase(48,"forest")][TestCase(48,"catacombs")]
        [Explicit("Prepared-party sensitivity: same fixed historical equipment reference, Common EQ50 and available production jewelry.")]
        public void AuditRoleBalancePreparedRoutes(int level,string biome)=>RoleRoutes(level,biome,0,0,true);
        static void RoleJewelry(CharacterInstance hero)
        {
            foreach(var jewelry in PointCatalog.ItemDefinitions.OfType<JewelryItemDefinition>())
            {
                var metal=jewelry.Metals.Where(m=>JewelryGenerationService.GetItemLevel(m)<=hero.Level)
                    .OrderByDescending(JewelryGenerationService.GetItemLevel).ThenBy(m=>m.Material.Id).FirstOrDefault();
                if(metal==null)continue;
                var gem=jewelry.Gems.FirstOrDefault(g=>g.name=="JGD_Topaz")??jewelry.Gems.First();
                hero.Equipment.SetItem(jewelry.EquipmentSlot,new JewelryGenerationService().GenerateSelected(jewelry,metal,gem));
            }
            PointRefresh(hero);
        }
        void RoleRoutes(int level,string biome,int profile,int seed,bool prepared)
        {
            var stage=RoleStage;var folder=Path.Combine(RoleFolder,stage,(prepared?"prepared-":"")+$"L{level}-{biome}-P{profile}-S{seed}");Directory.CreateDirectory(folder);
            var rows=new List<RoleTelemetry>();
            RunRealisticExpeditions(level,prepared ? .5f : 0,biome,DungeonLength.Normal,profile,seed+1,"role-balance",firstSeed:seed,outputFolder:folder,fullOnly:true,equipmentReferenceFactory:RoleEquipmentReference,
                prepareHero:prepared?RoleJewelry:null,
                auditEngine:(engine,battle)=>
                {
                    var row=new RoleTelemetry{stage=stage,biome=biome,level=level,profile=profile,seed=seed,result=engine.Result};rows.Add(row);TrackRoles(engine,row);
                });
            File.WriteAllText(Path.Combine(folder,"telemetry.json"),JsonUtility.ToJson(new RoleTelemetryList{rows=rows},true));
        }
        [Test]
        public void RoleSimulatorFocusHandlesNoLivingAllies()
        {
            var hero=new CharacterInstanceFactory().Create(PointHeroDefinition(0));hero.LifeState=CharacterLifeState.Downed;
            var enemy=new CharacterInstanceFactory().Create(PointEnemyDefinitions[0]);
            var actor=new BattleUnit{Character=hero,IsPlayerSide=true,Position=new GridPosition(2,2)};
            var session=new CombatSession{Grid=new BattleGrid(12,10)};session.Units.Add(actor);
            session.Units.Add(new BattleUnit{Character=enemy,Position=new GridPosition(7,2)});
            using(var engine=new Engine(session,new Result()))Assert.That(engine.UpdatePlayerFocus(actor),Is.Null);
        }
        [Serializable] sealed class RoleTelemetryList {public List<RoleTelemetry> rows=new();}
        static IEnumerable<TestCaseData> RolePackCases()
        {
            foreach(var level in new[]{10,48})foreach(var biome in new[]{"forest","catacombs"})yield return new TestCaseData(level,biome);
        }
        [TestCaseSource(nameof(RolePackCases)),Explicit("Paired fresh actual regular and boss rosters; same RNG seed, equipment and current player policy.")]
        public void AuditRoleBalanceFreshPacks(int level,string biome)
        {
            var stage=RoleStage;var folder=Path.Combine(RoleFolder,stage,$"fresh-L{level}-{biome}");Directory.CreateDirectory(folder);
            var reports=new RoleTelemetryList();var saved=UnityEngine.Random.state;
            try
            {
                var dungeon=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                var encounters=dungeon.EncounterDefinitions.Concat(PointCatalog.DungeonDefinitions.Where(d=>d!=null&&d.Id.StartsWith("dungeon."+biome+".")
                    &&d.BossEncounterDefinition!=null).Select(d=>d.BossEncounterDefinition)).Distinct().OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
                foreach(var encounter in encounters)
                {
                    const int seed=0;var session=new CombatSession{Grid=new BattleGrid(12,10),DungeonTheme=dungeon.Theme};
                    for(var index=0;index<4;index++)
                    {
                        var gear=PointInput.rows.Single(r=>r.level==level&&r.hero==index&&r.weaponTier==0&&r.armorTier==0);
                        var hero=PointHero(gear,RealisticProfile(index,0),PointBudget(level),0,PointEnemies(level));
                        hero.CharacterId="role-hero-"+index;SetJointLoadout(hero,RealisticProfile(index,0),JointDefaultLoadout);
                        session.Units.Add(new BattleUnit{Character=hero,IsPlayerSide=true,Position=new GridPosition(index==0||index==3?3:2,3+index)});
                    }
                    for(var index=0;index<encounter.Enemies.Count;index++)
                    {
                        var entry=encounter.Enemies[index];var npc=new CharacterInstanceFactory().Create(entry.CharacterDefinition,entry.ClassData,level);
                        npc.CharacterId="role-enemy-"+index;session.Units.Add(new BattleUnit{Character=npc,Position=new GridPosition(7+index/6,2+index%6)});
                    }
                    var result=new Result{candidate=stage,encounter=encounter.Id,level=level,seed=seed,build="profile-0",enemyCount=encounter.Enemies.Count,
                        enemyIds=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray()};
                    var row=new RoleTelemetry{stage=stage,biome=biome,level=level,seed=seed,result=result};reports.rows.Add(row);
                    var used=new HashSet<string>();session.SkillResolved+=(u,s,id)=>{if(u.IsPlayerSide)used.Add(u.Character.CharacterId+":"+s.SkillId);};
                    UnityEngine.Random.InitState(81016+seed);
                    using(var engine=new Engine(session,result,new KnockdownTrace()))
                    {
                        engine.AuditTurnLimit=600;engine.AuditLateKnifeExtraction=true;
                        engine.AdditionalAuditScore=(actor,skill,mode,targets)=>engine.BuildUtility(actor,skill,mode,targets,used,result.turns>=200);
                        engine.AuditUnitTurn=actor=>used.RemoveWhere(id=>id.StartsWith(actor.Character.CharacterId+":",StringComparison.Ordinal));
                        TrackRoles(engine,row);engine.Run();
                    }
                    File.WriteAllText(Path.Combine(folder,"telemetry.json"),JsonUtility.ToJson(reports,true));
                }
            }
            finally {UnityEngine.Random.state=saved;}
        }
    }
}
