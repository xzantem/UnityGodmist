using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string TrapAuditFolder=>Path.GetFullPath("../LocalHandoffs/stat-calculation-fixes-20261021");
        [Serializable] sealed class TrapAuditRow
        {
            public int id,level,hero,hp,weaponTier,armorTier,weaponUpgrade,armorUpgrade,necklace,bracelet,rawHit,rawTick,ticks,hit,dot,firstTick;
            public string trap,heroName,braceletType;
            public float physicalDefense,magicalDefense,bleed,poison,bleedModifier,poisonModifier,statusChance,
                expectedDot,expectedTotal,fullPercent,expectedPercent,potentialDot;
            public bool downed;
        }
        [Serializable] sealed class TrapAuditReport
        {
            public List<TrapAuditRow> rows=new();
            public int fullDotCycles;
            public string policy="Budgeted historical gear; legal Brawler/Assassin/Fire/Priest SP. First reach of each level. No active protection, cleanses, healing, shields granted for free, or RNG status failures. Conditional DoT applied as runtime snapshot; chance measured separately. No combat AI or expedition simulation.";
        }
        static void TrapAuditSet(object target,string property,object value)=>target.GetType()
            .GetField("<"+property+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);

        [Test,Timeout(240000)]
        public void AuditBudgetedTrapHealthCosts()
        {
            var rng=UnityEngine.Random.state;
            var input=JsonUtility.FromJson<CampaignBudgetInput>(File.ReadAllText(Path.Combine(TrapAuditFolder,"audit-input.json")));
            var temporary=new List<Object>();
            var bracelets=new Dictionary<(int,ResistanceType),JewelryItemDefinition>();
            var report=new TrapAuditReport();
            var traps=new[]{"Dungeons/Objects/DO_Trap_Spikes.asset","Dungeons/Objects/DO_Trap_Gas.asset","Dungeons/Objects/DO_Trap_GraveSeal.asset"}
                .Select(p=>AssetDatabase.LoadAssetAtPath<SpikeTrapDungeonObjectDefinition>(Root+p)).ToArray();
            Assert.That(traps.All(t=>t!=null),Is.True);
            var factory=new CharacterInstanceFactory();
            var forest=new[]{"Dungeons/Forest/DO_Forest_Trap_Snare.asset","Dungeons/Forest/DO_Forest_Trap_Poison.asset","Dungeons/Forest/DO_Forest_Trap_Roots.asset"};
            for(int i=0;i<traps.Length;i++)
            {
                var other=AssetDatabase.LoadAssetAtPath<SpikeTrapDungeonObjectDefinition>(Root+forest[i]);
                Assert.That(other,Is.Not.Null);
                Assert.That((other.Damage,other.DamageType,other.StatusId,other.StatusDamage,other.Duration,other.StatusChance,other.DamageGrowthPerLevel,other.DealsImmediateDamage),
                    Is.EqualTo((traps[i].Damage,traps[i].DamageType,traps[i].StatusId,traps[i].StatusDamage,traps[i].Duration,traps[i].StatusChance,traps[i].DamageGrowthPerLevel,traps[i].DealsImmediateDamage)));
            }
            try
            {
                foreach(var state in input.rows)
                {
                    foreach(var trap in traps)
                    {
                        var scale=new DungeonTrapSystem(null,state.level);
                        var row=Measure(state,trap.name,trap.DealsImmediateDamage?scale.Scale(trap,trap.Damage):0,trap.DamageType,
                            trap.StatusId,trap.StatusChance,scale.Scale(trap,trap.StatusDamage),trap.Duration);
                        report.rows.Add(row);
                    }
                }
                Assert.That(report.rows.Count,Is.EqualTo(input.rows.Length*3));
                File.WriteAllText(Path.Combine(TrapAuditFolder,"production-audit.json"),JsonUtility.ToJson(report,true));
            }
            finally
            {
                UnityEngine.Random.state=rng;
                foreach(var obj in temporary)if(obj!=null)Object.DestroyImmediate(obj);
            }

            CharacterInstance Hero(CampaignBudgetInputRow state)
            {
                var definition=PointHeroDefinition(state.hero);
                var hero=factory.Create(definition,definition.ClassData,state.level);
                hero.SkillTreeProgress=PointProgress(state.hero,PointBudget(state.level),new[]{1,0,1,0}[state.hero]);
                foreach(var g in new[]{state.weapon,state.armor})
                {
                    var slot=g.slot==0?EquipmentSlot.Weapon:EquipmentSlot.Armor;
                    var item=PointItem(hero,g.stats,slot,g.progress/100f,g.tier,g.master);
                    item.CurrentRarity=(RarityTier)g.rarity;item.RecalculateStatModifiers();hero.Equipment.SetItem(slot,item);
                }
                for(int s=0;s<4;s++)
                {
                    var j=input.jewelry.SingleOrDefault(t=>t.level==state.jewelry[s]);if(j==null)continue;
                    var slot=new[]{EquipmentSlot.Necklace,EquipmentSlot.Ring,EquipmentSlot.Earrings,EquipmentSlot.Bracelet}[s];
                    var asset=new[]{"EID_Necklace","EID_Ring","EID_Earrings","EID_Bracelet"}[s];
                    if(s!=3)
                    {
                        var mods=s==0?new[]{JewelryAuditStat(CharacterStatType.MaxHealth,j.hp)}
                            :s==1?new[]{JewelryAuditStat(CharacterStatType.MinAttack,j.atk),JewelryAuditStat(CharacterStatType.MaxAttack,j.atk)}
                            :new[]{JewelryAuditStat(CharacterStatType.Speed,j.speed)};
                        JewelryAuditEquip(hero,slot,asset,mods);
                    }
                    else
                    {
                        // Fixed type per hero; do not swap protection to match each trap.
                        var type=state.hero is 0 or 2?ResistanceType.Poison:ResistanceType.Bleed;
                        var key=(j.level,type);
                        if(!bracelets.TryGetValue(key,out var projected))
                        {
                            var template=AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>(Root+"Equipment/Jewelry/EID_Bracelet.asset");
                            projected=Object.Instantiate(template);temporary.Add(projected);
                            var metal=new JewelryMetalData();TrapAuditSet(metal,"Material",template.Metals.First().Material);
                            var modifier=new JewelryModifierData();TrapAuditSet(modifier,"Target",JewelryModifierTarget.Resistance);
                            TrapAuditSet(modifier,"ResistanceType",type);TrapAuditSet(modifier,"Value",j.dotResistance);
                            TrapAuditSet(metal,"Modifiers",new List<JewelryModifierData>{modifier});
                            TrapAuditSet(projected,"Metals",new List<JewelryMetalData>{metal});bracelets.Add(key,projected);
                        }
                        hero.Equipment.SetItem(slot,new JewelryItemInstance(projected) {
                            MetalId=JewelryGenerationService.GetMetalRecipeKey(projected.Metals.Single()),
                            MetalDisplayName="Projected "+j.metal,RequiredLevel=j.level,ItemLevel=j.level });
                    }
                    hero.Equipment.GetItem(slot).RequiredLevel=hero.Equipment.GetItem(slot).ItemLevel=j.level;
                    Assert.That(j.level,Is.LessThanOrEqualTo(hero.Level));
                }
                PointRefresh(hero);return hero;
            }

            TrapAuditRow Measure(CampaignBudgetInputRow state,string id,int raw,DamageType kind,string status,float chance,int tick,int turns)
            {
                var hero=Hero(state);var unit=PointUnit(hero,true);var hp=hero.CurrentHealth;
                var session=new CombatSession { ActiveUnit=unit };session.Units.Add(unit);
                var damage=new CombatDamageSystem();
                var row=new TrapAuditRow { id=state.id,level=state.level,hero=state.hero,heroName=hero.Definition.DisplayName,
                    trap=id,hp=hp,weaponTier=state.weapon.tier,armorTier=state.armor.tier,
                    weaponUpgrade=state.weapon.progress,armorUpgrade=state.armor.progress,necklace=state.jewelry[0],bracelet=state.jewelry[3],
                    braceletType=state.hero is 0 or 2?"Poison":"Bleed",rawHit=raw,rawTick=tick,ticks=turns,
                    physicalDefense=hero.Stats.PhysicalDefense.GetValue(),magicalDefense=hero.Stats.MagicalDefense.GetValue(),
                    bleed=hero.Resistances.BleedResistance,poison=hero.Resistances.PoisonResistance,
                    bleedModifier=ModifyResistanceSystem.GetModifier(unit,ResistanceType.Bleed),
                    poisonModifier=ModifyResistanceSystem.GetModifier(unit,ResistanceType.Poison) };
                if(raw>0)row.hit=damage.ApplySummonedObjectDamage(session,unit,raw,kind);
                row.statusChance=string.IsNullOrEmpty(status)?0:StatusEffectSystem.CalculateStatusApplicationChance(null,unit,status,chance);
                if(tick>0 && turns>0)
                {
                    row.potentialDot=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(unit,status,tick)*turns;
                    // Conditional-on-application cost: same snapshot and processor as runtime,
                    // no RNG failures. Application chance is evaluated independently above.
                    var effect=new PassiveEffectRuntime(status,new List<PassiveEffectSourceRuntime>(),1,turns,tick*turns,
                        PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnStart);
                    unit.Character.PassiveEffects.Add(effect);
                    var processor=new DamageOverTimePassiveEffectProcessor(damage);
                    for(int turn=0;turn<turns && hero.LifeState==CharacterLifeState.Alive;turn++)
                    {
                        var health=hero.CurrentHealth;processor.ProcessTurn(session,unit,effect,out var expire);
                        var actual=health-hero.CurrentHealth;row.dot+=actual;if(turn==0)row.firstTick=actual;
                        effect.RemainingDuration--;if(expire)break;
                    }
                    report.fullDotCycles++;
                }
                row.expectedDot=row.dot*row.statusChance;row.expectedTotal=row.hit+row.expectedDot;
                row.fullPercent=(row.hit+row.dot)/(float)hp*100;row.expectedPercent=row.expectedTotal/hp*100;
                row.downed=hero.LifeState!=CharacterLifeState.Alive;
                Assert.That(row.fullPercent,Is.InRange(0,100.001f));Assert.That(row.statusChance,Is.InRange(0,1));
                return row;
            }
        }
    }
}
