using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Core.Types;
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
        static string VariantFolder=>Path.GetFullPath("../LocalHandoffs/equipment-variants-20261020");
        [Serializable] sealed class VariantItem
        { public string id;public int level,tier,hero,slot,index;public PointGearStat[] stats; }
        [Serializable] sealed class VariantMean
        {public int level,hero,slot;public PointGearStat[] stats;}
        [Serializable] sealed class VariantJewelry
        {public int tier,index;public string slot,metal,label;public PointGearStat[] stats;}
        [Serializable] sealed class VariantInput
        {public VariantItem[] variants;public VariantMean[] means;public VariantJewelry[] jewelry;}
        [Serializable] sealed class VariantRow
        {
            public string variant,mode;public int level,hero,profile,rarity,jewel=-1;
            public float upgrade,hp,min,max,physicalDefense,magicalDefense,dodge,critChance,critDamage,accuracy,speed,mana,regen,emptyRegen;
            public double damage,backDamage,rangedDamage,overflowDamage,heroPhysical,heroMagical,heal,healingCrit,hit,effectiveCrit,overflowCrit;
        }
        [Serializable] sealed class VariantOutput
        {public string inputSha256;public List<VariantRow> rows=new();}

        static (double damage,float hit,float crit) VariantExpected(BattleUnit source,BattleUnit target,bool melee,bool behind=false,string action=null)
        {
            var kind=PointDamageType(source.Character);var skill=source.Character.GetBasicAttackSkill();
            source.Position=new GridPosition(2,2);target.Position=new GridPosition(melee?3:6,2);
            target.ScoutState.Facing=new Vector2Int(behind?1:-1,0);
            var session=new CombatSession();session.Units.Add(source);session.Units.Add(target);
            var ctx=new PassiveEffectCombatContext(session,source,target,skillId:skill.SkillId,skill:skill,
                targetsByModeIndex:new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{target}}},
                attackType:melee?AttackType.Melee:skill.ResolveAttackType(4),attackPosition:behind?AttackPosition.Back:AttackPosition.Front,
                damageType:kind,distance:melee?1:4,actionId:action);
            var s=source.Character.Stats;var t=target.Character.Stats;
            var hit=kind==DamageType.Physical?CombatDamageSystem.ApplyHitChanceBonus(source.Character,CombatDamageSystem.CalculateHitChance(source.Character.WeaponAccuracy,100,t.Dodge.GetValue())):1f;
            var crit=kind==DamageType.Magical?Mathf.Clamp01(MageCombatSystem.MagicCritChance(ctx))
                :Mathf.Clamp01((PassiveEffectCombatExecutionSystem.ApplyCriticalChance(session,ctx,s.CritChance.GetValue())+ScoutCombatSystem.CriticalChance(ctx))
                *WarriorCombatSystem.CriticalMultiplier(source,target)*MageCombatSystem.CritReceived(session,target,source)*PaladinCombatSystem.CriticalMultiplier(source,target));
            var critical=Mathf.Max(1,s.CritDamage.GetValue()+ScoutCombatSystem.WeakPointCriticalDamage(target));
            var pre=ScoutCombatSystem.DamageMultiplier(ctx)*WarriorCombatSystem.DamageMultiplier(ctx)*MageCombatSystem.DamageMultiplier(ctx)*PaladinCombatSystem.DamageMultiplier(ctx);
            var vars=new CombatVariableSystem();var post=vars.GetMultiplier(source.Character,GameplayVariableType.DamageDealtPercent)
                *vars.GetMultiplier(target.Character,GameplayVariableType.DamageReceivedPercent)*ScoutCombatSystem.ReceivedDamageMultiplier(source,target)
                *WarriorCombatSystem.Received(session,source,target,kind)*MageCombatSystem.FireOutgoing(source,session);
            var penetration=(kind==DamageType.Physical?s.PhysicalDefensePenetration.GetValue():s.MagicalDefensePenetration.GetValue())
                +PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(source,target,kind);
            var defense=(kind==DamageType.Physical?t.PhysicalDefense.GetValue():t.MagicalDefense.GetValue())*WarriorCombatSystem.DefenseMultiplier(target,session)/(1+penetration);
            var low=Mathf.Max(0,Mathf.RoundToInt(s.MinAttack.GetValue()));var high=Mathf.Max(low,Mathf.RoundToInt(s.MaxAttack.GetValue()));
            var key=(low,high,hit,crit,critical,pre,post,defense,0f);
            if(!pointDamageCache.TryGetValue(key,out var damage))
            {
                int Mitigate(int raw)=>defense<=0?raw:Mathf.CeilToInt((float)raw*raw/(raw+defense));
                double total=0;
                for(var raw=low;raw<=high;raw++)
                {
                    var value=Mathf.RoundToInt(raw*pre);
                    total+=(1-crit)*Mathf.RoundToInt(Mitigate(value)*post)+crit*Mathf.RoundToInt(Mitigate(Mathf.CeilToInt(value*critical))*post);
                }
                damage=hit*total/(high-low+1);pointDamageCache[key]=damage;
            }
            return(damage,hit,crit);
        }

        static VariantRow VariantMeasure(VariantInput input,VariantItem variant,int profile,int rarity,float upgrade,string mode,int jewelryIndex,BattleUnit[] enemies)
        {
            var definition=PointHeroDefinition(variant.hero);var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,variant.level);
            hero.SkillTreeProgress=PointProgress(variant.hero,PointBudget(variant.level),profile);
            for(var slot=0;slot<2;slot++)
            {
                var values=variant.slot==slot?variant.stats:input.means.Single(m=>m.level==variant.level&&m.hero==variant.hero&&m.slot==slot).stats;
                var equipmentSlot=slot==0?EquipmentSlot.Weapon:EquipmentSlot.Armor;
                var item=PointItem(hero,values,equipmentSlot,upgrade,variant.tier,false);
                item.CurrentRarity=(RarityTier)rarity;item.RecalculateStatModifiers();hero.Equipment.SetItem(equipmentSlot,item);
            }
            if(jewelryIndex>=0)
            {
                var type=mode=="ring"?"ring":mode=="neck"?"neck":"ear";
                var j=input.jewelry.Single(x=>x.tier==40&&x.slot==type&&x.index==jewelryIndex);
                var slot=type=="ring"?EquipmentSlot.Ring:type=="neck"?EquipmentSlot.Necklace:EquipmentSlot.Earrings;
                var asset=type=="ring"?"EID_Ring":type=="neck"?"EID_Necklace":"EID_Earrings";
                var mods=j.stats.Select(v=>JewelryAuditStat((CharacterStatType)Enum.Parse(typeof(CharacterStatType),v.name),
                    type=="ear"&&jewelryIndex==1?0:v.value)).ToArray();
                // Resource generation earrings are a gameplay variable, not flat regeneration.
                // They are not included in the hit/HP metric; no false +12 flat regen is granted.
                JewelryAuditEquip(hero,slot,asset,mods);
            }
            PointRefresh(hero);var unit=PointUnit(hero,true);var stats=hero.Stats;
            var r=new VariantRow{variant=variant.id,mode=mode,level=variant.level,hero=variant.hero,profile=profile,rarity=rarity,upgrade=upgrade,jewel=jewelryIndex,
                hp=stats.MaxHealth.GetValue(),min=stats.MinAttack.GetValue(),max=stats.MaxAttack.GetValue(),physicalDefense=stats.PhysicalDefense.GetValue(),magicalDefense=stats.MagicalDefense.GetValue(),
                dodge=stats.Dodge.GetValue(),critChance=stats.CritChance.GetValue(),critDamage=stats.CritDamage.GetValue(),accuracy=hero.WeaponAccuracy,speed=stats.Speed.GetValue(),mana=hero.ResourcePool.MaxValue,regen=hero.ResourcePool.Regeneration};
            // Compare only identical front/melee contexts for physical weapons. Scout gets a separate ranged/back view.
            foreach(var e in enemies)
            {
                var front=VariantExpected(unit,e,variant.hero!=2);r.damage+=front.damage/enemies.Length;r.hit+=front.hit/enemies.Length;r.effectiveCrit+=front.crit/enemies.Length;
                if(variant.hero==1)
                {
                    r.backDamage+=VariantExpected(unit,e,true,true).damage/enemies.Length;
                    r.rangedDamage+=VariantExpected(unit,e,false).damage/enemies.Length;
                }
            }
            // Restore ordinary positions/facing before incoming queries; no persistent target effects are installed.
            unit.Position=new GridPosition(2,2);
            foreach(var e in enemies)e.Position=new GridPosition(6,2);
            r.heroPhysical=enemies.Where(e=>PointDamageType(e.Character)==DamageType.Physical).Average(e=>hero.CurrentHealth/PointExpected(e,unit,DamageType.Physical));
            r.heroMagical=enemies.Where(e=>PointDamageType(e.Character)==DamageType.Magical).Average(e=>hero.CurrentHealth/PointExpected(e,unit,DamageType.Magical));
            if(variant.hero==2)
            {
                hero.ResourcePool.CurrentValue=hero.ResourcePool.RecoveryLimit;
                MageCombatSystem.BeginSkill(unit,hero.GetBasicAttackSkill(),"variant-overflow");
                r.overflowCrit=Mathf.Clamp01(unit.MageState.OverflowCritChance);
                r.overflowDamage=enemies.Average(e=>VariantExpected(unit,e,false,false,"variant-overflow").damage);
                hero.ResourcePool.CurrentValue=0;new CharacterStatPipelineSystem().Refresh(hero);r.emptyRegen=hero.ResourcePool.Regeneration;
            }
            if(variant.hero==3)
            {
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>(Root+"Skills/SD_PaladinHeal.asset");
                var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(unit,skill,skill.Effects.First(e=>e.EffectType==EffectType.PaladinHeal));
                var amount=(r.min+r.max)*.5f*(1+.01f*PaladinCombatSystem.Points(hero,PaladinCombatSystem.Priest))*effect.DamageMultiplier
                    *(1+.15f*PaladinCombatSystem.P(unit,"healing_power"))*(1+.15f*PaladinCombatSystem.P(unit,"healer_focus"));
                r.healingCrit=PaladinCombatSystem.HealingCritChance(unit,true);
                r.heal=(1-r.healingCrit)*Mathf.RoundToInt(amount)+r.healingCrit*Mathf.RoundToInt(amount*Mathf.Max(1,r.critDamage));
            }
            Assert.That(double.IsFinite(r.damage)&&r.damage>0&&r.heroPhysical>0&&r.heroMagical>0,Is.True);
            return r;
        }

        [Test,Explicit("Cross-check material evaluator against the prior analytical estimator in matching contexts.")]
        public void VerifyEquipmentVariantEstimator()
        {
            var input=JsonUtility.FromJson<VariantInput>(File.ReadAllText(Path.Combine(VariantFolder,"input.json")));
            foreach(var level in new[]{5,20,48})
            foreach(var index in Enumerable.Range(0,4))
            foreach(var profile in new[]{0,1})
            {
                var definition=PointHeroDefinition(index);var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                hero.SkillTreeProgress=PointProgress(index,PointBudget(level),profile);
                foreach(var slot in new[]{0,1})
                {
                    var values=input.means.Single(m=>m.level==level&&m.hero==index&&m.slot==slot).stats;
                    var equipmentSlot=slot==0?EquipmentSlot.Weapon:EquipmentSlot.Armor;
                    hero.Equipment.SetItem(equipmentSlot,PointItem(hero,values,equipmentSlot,.5f,PointMaterial(level),false));
                }
                PointRefresh(hero);var unit=PointUnit(hero,true);
                foreach(var enemy in PointEnemies(level))
                foreach(var melee in new[]{false,true})
                {
                    var actual=VariantExpected(unit,enemy,melee);
                    var expected=PointExpected(unit,enemy,PointDamageType(hero),melee);
                    Assert.That(actual.damage,Is.EqualTo(expected).Within(.00001),$"L{level} h{index} p{profile} melee{melee}");
                }
            }
        }

        [Test,Explicit("Exhaustive material choices, analytical damage and defense, no combat AI or asset changes.")]
        [Timeout(600000)]
        public void AuditEquipmentMaterialVariants()
        {
            var path=Path.Combine(VariantFolder,"input.json");var input=JsonUtility.FromJson<VariantInput>(File.ReadAllText(path));
            var output=new VariantOutput();using(var hash=SHA256.Create())output.inputSha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
            foreach(var group in input.variants.GroupBy(v=>v.level))
            {
                var enemies=PointEnemies(group.Key);
                foreach(var variant in group)
                foreach(var profile in new[]{0,1})
                {
                    foreach(var upgrade in new[]{0f,.5f})
                        output.rows.Add(VariantMeasure(input,variant,profile,5,upgrade,variant.slot==0?"weapon":"armor",-1,enemies));
                    if(variant.level!=48)continue;
                    foreach(var rarity in new[]{3,9})
                        output.rows.Add(VariantMeasure(input,variant,profile,rarity,.5f,variant.slot==0?"weapon":"armor",-1,enemies));
                    for(var jewel=0;jewel<4;jewel++)
                        output.rows.Add(VariantMeasure(input,variant,profile,5,.5f,variant.slot==0?"ring":"neck",jewel,enemies));
                }
                File.AppendAllText(Path.Combine(VariantFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" L"+group.Key+" rows="+output.rows.Count+Environment.NewLine);
            }
            Assert.That(output.rows.Count,Is.EqualTo(12288));
            File.WriteAllText(Path.Combine(VariantFolder,"export.json"),JsonUtility.ToJson(output));
        }
    }
}
