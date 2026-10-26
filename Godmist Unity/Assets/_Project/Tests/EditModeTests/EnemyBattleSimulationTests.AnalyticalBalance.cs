using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string AnalyticalFolder=>Path.GetFullPath("../LocalHandoffs/analytical-balance-20261019");
        [Serializable] sealed class AnalyticalPair
        {public string enemy;public double incoming,outgoing,melee;public string incomingType,outgoingType;}
        [Serializable] sealed class AnalyticalSkill
        {
            public string id;public int ap,rp,range;public float costPercent,healExpected,regenFraction;
            public List<string> effects=new(),effectTypes=new();public int minimum,maximum;public float hit;
        }
        [Serializable] sealed class AnalyticalUnit
        {
            public int level,hero,profile,hp,resource,maxResource,regeneration,points,maxAp;public float upgrade,speed,av,min,max;
            public string id,basic;public int basicAp;public List<AnalyticalPair> pairs=new();public List<AnalyticalSkill> skills=new();
        }
        [Serializable] sealed class AnalyticalNpc
        {
            public string id,kind;public int hp,range,cost,maxAp,startResource,maxResource,regen;public float min,max,speed,av,physicalDefense,magicalDefense;
            public List<AnalyticalSkill> skills=new();
        }
        [Serializable] sealed class AnalyticalRoster {public string biome,id;public string[] enemies;}
        [Serializable] sealed class AnalyticalBurn
        {public int level,profile;public float upgrade,chance,payload;public string enemy;public int hp,directMin,directMax;}
        [Serializable] sealed class AnalyticalSupplement
        {public List<AnalyticalBurn> burns=new();public List<AnalyticalNpc> npcs=new();}
        [Serializable] sealed class AnalyticalReport
        {
            public int level;public string policy="Static analytic expectations only; no Engine.Run, AI, turns, hit rolls or combat simulation. x1 ATK indices separate from skill previews/conditional effects.";
            public List<PointRow> durability=new();public List<AnalyticalUnit> units=new();public List<AnalyticalNpc> npcs=new();public List<AnalyticalRoster> rosters=new();
        }
        static AnalyticalSkill AnalyticalCard(BattleUnit unit,SkillData skill,BattleUnit target)
        {
            var card=new AnalyticalSkill{id=skill.SkillId,ap=CombatActionCostCalculator.CalculateSkillCost(unit,skill),
                rp=PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit,skill),costPercent=skill.ActionCost,
                range=skill.TargetModes.Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(unit,skill,m).AbsoluteRange.MaxRange).DefaultIfEmpty(0).Max()};
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(unit,target,skill);
            card.minimum=preview.Damage.HasDamage?preview.Damage.Minimum:0;card.maximum=preview.Damage.HasDamage?preview.Damage.Maximum:0;card.hit=preview.HitChance;
            foreach(var original in skill.Effects)
            {
                var e=PassiveEffectCombatExecutionSystem.ResolveEffect(unit,skill,original);card.effects.Add(JsonUtility.ToJson(e));card.effectTypes.Add(e.EffectType.ToString());
                if(e.EffectType==EffectType.PaladinHeal)
                {
                    var single=skill.SkillId=="skill.paladin.heal";
                    var power=(unit.Character.Stats.MinAttack.GetValue()+unit.Character.Stats.MaxAttack.GetValue())*.5f
                        *(1+.01f*PaladinCombatSystem.Points(unit.Character,PaladinCombatSystem.Priest))*e.DamageMultiplier;
                    if(single||skill.SkillId=="skill.paladin.group_heal")power*=1+.15f*PaladinCombatSystem.P(unit,"healing_power");
                    if(single)power*=1+.15f*PaladinCombatSystem.P(unit,"healer_focus");
                    var chance=PaladinCombatSystem.HealingCritChance(unit,single);
                    card.healExpected+=(1-chance)*Mathf.RoundToInt(power)+chance*Mathf.RoundToInt(power*Mathf.Max(1,unit.Character.Stats.CritDamage.GetValue()));
                }
                if(e.EffectType==EffectType.PaladinRegeneration)
                {
                    var chance=PaladinCombatSystem.HealingCritChance(unit,true);
                    card.regenFraction=.4f*(1+chance*(Mathf.Max(1,unit.Character.Stats.CritDamage.GetValue())-1));
                }
            }
            return card;
        }
        [TestCase(1)][TestCase(5)][TestCase(10)][TestCase(15)][TestCase(20)][TestCase(25)]
        [TestCase(30)][TestCase(35)][TestCase(40)][TestCase(45)][TestCase(48)][TestCase(50)]
        [Explicit("Read-only analytic export from current stat formulas; no battles, no decisions, no balancing mutations.")]
        [Timeout(600000)]
        public void AuditAnalyticalBalanceExport(int level)
        {
            Directory.CreateDirectory(AnalyticalFolder);File.AppendAllText(Path.Combine(AnalyticalFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" L"+level+Environment.NewLine);
            var report=new AnalyticalReport{level=level};var enemies=PointEnemies(level);var session=new CombatSession();
            foreach(var enemy in enemies)
            {
                CombatTurnSystem.RefreshMaxActionPoints(session,enemy,true);var c=enemy.Character;var s=c.Stats;var basic=c.GetBasicAttackSkill();
                report.npcs.Add(new AnalyticalNpc{id=c.Definition.Id,hp=c.CurrentHealth,min=s.MinAttack.GetValue(),max=s.MaxAttack.GetValue(),speed=s.Speed.GetValue(),
                    av=CombatTurnSystem.CalculateBaseActionValue(enemy),cost=CombatActionCostCalculator.CalculateSkillCost(enemy,basic),maxAp=enemy.MaxActionPoints,
                    range=basic.TargetModes.Select(m=>m.AbsoluteRange.MaxRange).DefaultIfEmpty(0).Max(),kind=PointDamageType(c).ToString(),
                    startResource=c.ResourcePool.CurrentValue,maxResource=c.ResourcePool.MaxValue,regen=c.ResourcePool.Regeneration,
                    physicalDefense=s.PhysicalDefense.GetValue(),magicalDefense=s.MagicalDefense.GetValue()});
            }
            foreach(var gear in PointInput.rows.Where(r=>r.level==level))
            foreach(var upgrade in level==1?new[]{0f}:new[]{0f,.5f,1f})foreach(var profile in new[]{-1,0,1,2})
            {
                var budget=profile<0?0:PointBudget(level);var hero=PointHero(gear,profile,budget,upgrade,enemies);
                report.durability.Add(PointSnapshot(hero,gear,profile,budget,upgrade,enemies));
                if(gear.weaponTier!=0||gear.armorTier!=0)continue;
                var unit=PointUnit(hero,true);CombatTurnSystem.RefreshMaxActionPoints(session,unit,true);var basic=hero.GetBasicAttackSkill();var s=hero.Stats;
                var row=new AnalyticalUnit{level=level,hero=gear.hero,profile=profile,upgrade=upgrade,id=hero.Definition.Id,hp=hero.CurrentHealth,points=budget,
                    min=s.MinAttack.GetValue(),max=s.MaxAttack.GetValue(),speed=s.Speed.GetValue(),av=CombatTurnSystem.CalculateBaseActionValue(unit),
                    resource=hero.ResourcePool.CurrentValue,maxResource=hero.ResourcePool.MaxValue,regeneration=hero.ResourcePool.Regeneration,maxAp=unit.MaxActionPoints,
                    basic=basic.SkillId,basicAp=CombatActionCostCalculator.CalculateSkillCost(unit,basic)};
                foreach(var enemy in enemies)
                    row.pairs.Add(new AnalyticalPair{enemy=enemy.Character.Definition.Id,incoming=PointExpected(enemy,unit,PointDamageType(enemy.Character)),
                        outgoing=PointExpected(unit,enemy,PointDamageType(hero)),melee=PointExpected(unit,enemy,PointDamageType(hero),true),
                        incomingType=PointDamageType(enemy.Character).ToString(),outgoingType=PointDamageType(hero).ToString()});
                foreach(var skill in hero.GetKnownSkills())row.skills.Add(AnalyticalCard(unit,skill,enemies[0]));
                report.units.Add(row);
            }
            foreach(var biome in new[]{"catacombs","forest"})
            {
                var dungeon=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                foreach(var encounter in dungeon.EncounterDefinitions)
                {
                    var ids=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray();Assert.That(ids.All(id=>report.npcs.Any(n=>n.id==id)),Is.True,encounter.Id);
                    report.rosters.Add(new AnalyticalRoster{biome=biome,id=encounter.Id,enemies=ids});
                }
            }
            Assert.That(report.durability.All(r=>double.IsFinite(r.enemy)&&r.heroPhysical>0&&r.heroMagical>0),Is.True);
            File.WriteAllText(Path.Combine(AnalyticalFolder,"level-"+level+".json"),JsonUtility.ToJson(report,true));
        }
        [Test,Explicit("Pure status probability/payload and NPC skill inventory; no rolls, casts or turns.")]
        public void AuditAnalyticalBalanceSupplement()
        {
            var report=new AnalyticalSupplement();
            foreach(var level in new[]{1,5,10,15,20,25,30,35,40,45,48,50})
            {
                var enemies=PointEnemies(level);var gear=PointInput.rows.Single(g=>g.level==level&&g.hero==2&&g.weaponTier==0&&g.armorTier==0);
                foreach(var profile in new[]{0,1,2})foreach(var upgrade in level==1?new[]{0f}:new[]{0f,.5f,1f})
                {
                    var hero=PointUnit(PointHero(gear,profile,PointBudget(level),upgrade,enemies),true);
                    var fireball=hero.Character.GetKnownSkills().Single(s=>s.SkillId=="skill.mage.fireball");
                    var burn=fireball.Effects.Select(e=>PassiveEffectCombatExecutionSystem.ResolveEffect(hero,fireball,e)).Single(e=>e.StatusId=="burn");
                    var rng=UnityEngine.Random.state;
                    var baseDamage=CombatDamageValueCalculator.CalculateDamageValue(hero,DamageValueSource.AverageAttack,burn.StatusDamageMultiplier);
                    var payload=Mathf.RoundToInt(baseDamage*burn.DurationTurns*(1+.02f*MageCombatSystem.Points(hero.Character,MageCombatSystem.Fire))*(1+.1f*MageCombatSystem.F(hero,"arcana")));
                    foreach(var enemy in enemies)
                    {
                        var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(hero,enemy,fireball);
                        report.burns.Add(new AnalyticalBurn{level=level,profile=profile,upgrade=upgrade,enemy=enemy.Character.Definition.Id,hp=enemy.Character.CurrentHealth,
                            directMin=preview.Damage.Minimum,directMax=preview.Damage.Maximum,payload=payload,
                            chance=StatusEffectSystem.CalculateStatusApplicationChance(hero,enemy,"burn",burn.BaseChance)});
                    }
                    Assert.That(UnityEngine.Random.state,Is.EqualTo(rng),"Analytical evaluation must never draw combat RNG.");
                }
                if(level!=48)continue;
                var target=PointUnit(PointHero(gear,1,PointBudget(level),0,enemies),true);var session=new CombatSession();
                foreach(var enemy in enemies)
                {
                    CombatTurnSystem.RefreshMaxActionPoints(session,enemy,true);
                    report.npcs.Add(new AnalyticalNpc{id=enemy.Character.Definition.Id,skills=enemy.Character.GetKnownSkills().Select(s=>AnalyticalCard(enemy,s,target)).ToList()});
                }
            }
            Assert.That(report.burns.All(b=>b.chance>=0&&b.chance<=1&&b.directMin<=b.directMax),Is.True);
            File.WriteAllText(Path.Combine(AnalyticalFolder,"supplement.json"),JsonUtility.ToJson(report,true));
        }
    }
}
