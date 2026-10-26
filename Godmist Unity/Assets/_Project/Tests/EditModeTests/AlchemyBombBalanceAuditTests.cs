using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class AlchemyBombBalanceAuditTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        [Serializable] public sealed class Row
        {
            public int level,damage,raw,normalHit,physicalPart,magicalPart;
            public string enemy,name,dungeon,recipe; public bool boss,catalyst;
            public float hp,physicalDefense,magicalDefense,damageHpPercent,normalHitRatio,
                physicalHitChance,expectedDamage,currentPreviewHitChance,dotTotal,dotExpectedTotal,dotHpPercent;
        }
        [Serializable] public sealed class Ingredient
        { public string id,name,rarity,effect; public int effectTier,itemLevel; public float strength; }
        [Serializable] public sealed class Summary
        {
            public int[] levels={1,5,10,11,15,20,25,30,35,40,45,48,50};
            public List<Row> rows=new(); public List<Ingredient> ingredients=new();
            public string roleAsset,catalystName; public int catalystTier,actionCost,baseCharges,ingredientSlots;
            public float catalystMultiplier; public List<string> media=new();
            public int executionSamples;
            public List<Reference> currentNormalReferences=new();
        }
        [Serializable] public sealed class Reference { public int level; public float hardcoded,actualMean; public float[] heroAttacks; }

        [Test] public void ExportProductionAlchemyDamageAgainstBothDungeons()
        {
            var npc=AssetDatabase.LoadAssetAtPath<NpcDefinition>(Root+"NPC/ND_Arungard_Alchemist.asset");
            var role=npc.GetRole<AlchemistRoleData>(); Assert.That(role,Is.Not.Null);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var report=new Summary { roleAsset=AssetDatabase.GetAssetPath(role),
                ingredientSlots=AlchemyRuntimeRules.RequiredIngredientCount,
                actionCost=ConsumableRuntimeActionBuilder.ActionCostPercent,baseCharges=role.BombBaseDefinition.MaxCharges };
            foreach(var option in role.IngredientOptions)
            {
                var e=option.BombEffect; if(e==null || option.Material==null)continue;
                var tier=AlchemyRuntimeRules.GetEffectTier(option.Material);
                report.ingredients.Add(new Ingredient { id=option.Material.Id,name=option.Material.DisplayName,
                    rarity=option.Material.BaseRarity.ToString(),effect=e.EffectType.ToString(),effectTier=tier,
                    itemLevel=AlchemyRuntimeRules.GetConsumableLevel(tier),strength=e.Value });
            }
            foreach(var medium in role.BombGunpowders)report.media.Add(medium.Material?.Id+" tier="+medium.Tier);
            var strengthCatalyst=role.Catalysts.Where(c=>c.Material!=null && c.GetTypeFor(ConsumableUseType.Bomb)==ConsumableCatalystType.Strength)
                .OrderByDescending(c=>c.Tier).FirstOrDefault();
            Assert.That(strengthCatalyst,Is.Not.Null);
            report.catalystName=strengthCatalyst.Material.DisplayName;report.catalystTier=strengthCatalyst.Tier;
            var physical=role.IngredientOptions.Where(i=>i.Material!=null && i.BombEffect?.EffectType==ConsumableRuntimeEffectType.DealPhysicalDamage).ToArray();
            var magical=role.IngredientOptions.Where(i=>i.Material!=null && i.BombEffect?.EffectType==ConsumableRuntimeEffectType.DealMagicalDamage).ToArray();
            Assert.That(physical.Length,Is.EqualTo(2));Assert.That(magical.Length,Is.EqualTo(2));
            var utilities=role.IngredientOptions.Where(i=>i.Material!=null && i.BombEffect!=null
                && i.BombEffect.EffectType!=ConsumableRuntimeEffectType.DealPhysicalDamage && i.BombEffect.EffectType!=ConsumableRuntimeEffectType.DealMagicalDamage
                && AlchemyRuntimeRules.GetEffectTier(i.Material)==1).Take(2).ToArray();
            Assert.That(utilities.Length,Is.EqualTo(2));
            AlchemyIngredientData Ingredient(ConsumableRuntimeEffectType type)=>role.IngredientOptions.First(i=>i.BombEffect?.EffectType==type);
            var recipes=new Dictionary<string,AlchemyIngredientData[]> {
                ["1P"]=new[]{physical[0],utilities[0],utilities[1]}, ["2P"]=new[]{physical[0],physical[1],utilities[0]},
                ["1M"]=new[]{magical[0],utilities[0],utilities[1]}, ["2M"]=new[]{magical[0],magical[1],utilities[0]},
                ["2P1M"]=new[]{physical[0],physical[1],magical[0]},
                ["1P2M"]=new[]{physical[0],magical[0],magical[1]}, ["1P1M"]=new[]{physical[0],magical[0],utilities[0]},
                ["1DoT"]=new[]{utilities[0],Ingredient(ConsumableRuntimeEffectType.ApplyStun),Ingredient(ConsumableRuntimeEffectType.Slow)},
                ["3DoT"]=new[]{utilities[0],Ingredient(ConsumableRuntimeEffectType.ApplyPoison),Ingredient(ConsumableRuntimeEffectType.ApplyBurn)} };
            var enemies=catalog.CharacterDefinitions.Where(d=>d!=null
                && (Godmist.Editor.EnemyScalingBalanceSetup.UsesEarlyThreatCalibration(d.Id) || Godmist.Editor.EnemyScalingBalanceSetup.UsesBossBaseBoost(d.Id))
                && !d.Id.EndsWith(".enraged") && !d.Id.EndsWith(".burning") && !d.Id.EndsWith(".revealed")).ToArray();
            Assert.That(enemies.Length,Is.EqualTo(18));
            var factory=new CharacterInstanceFactory();
            var craft=new AlchemyCraftingService();
            var alchemist=new NpcInstance { NpcId=npc.Id,Definition=npc };
            foreach(var level in report.levels)
            {
                var caster=new BattleUnit { IsPlayerSide=true, Position=new GridPosition(0,0), Character=new CharacterInstance {
                    CharacterId="audit.alchemist",Level=level,CurrentHealth=1000000,LifeState=CharacterLifeState.Alive,WeaponAccuracy=100,
                    Stats=new CharacterStats { MinAttack=new Stat(1),MaxAttack=new Stat(1),MaxHealth=new Stat(1000000),
                        WeaponAccuracy=new Stat(100),CritChance=new Stat(0),CritDamage=new Stat(1.5f) } } };
                foreach(var recipe in recipes)foreach(var boost in new[]{false,true})
                {
                    var bomb=craft.CreateForMerchantStock(alchemist,new AlchemyCraftRequest { UseType=ConsumableUseType.Bomb,
                        Ingredients=recipe.Value.Select(i=>i.Material).ToList(), Medium=role.BombGunpowders.First().Material,
                        Catalyst=boost?strengthCatalyst.Material:null });
                    Assert.That(bomb,Is.Not.Null,"Audit must use a craftable three-ingredient recipe.");
                    report.catalystMultiplier=bomb.CatalystEffectMultiplier;
                    var skill=ConsumableRuntimeActionBuilder.Build(bomb);
                    try
                    {
                        Assert.That(skill.ActionCost,Is.EqualTo(20)); Assert.That(skill.SkillAccuracy,Is.EqualTo(100));
                        foreach(var definition in enemies)
                        {
                            var target=new BattleUnit { Character=factory.Create(definition,definition.ClassData,level),Position=new GridPosition(2,0) };
                            var hp=target.Character.Stats.MaxHealth.GetValue();
                            var row=new Row { level=level,enemy=definition.Id,name=definition.DisplayName,
                                dungeon=definition.Id.Contains(".forest.")?"forest":"catacombs",
                                boss=Godmist.Editor.EnemyScalingBalanceSetup.UsesBossBaseBoost(definition.Id),recipe=recipe.Key,catalyst=boost,
                                hp=hp,physicalDefense=target.Character.Stats.PhysicalDefense.GetValue(),magicalDefense=target.Character.Stats.MagicalDefense.GetValue() };
                            row.physicalHitChance=CombatDamageSystem.CalculateSkillHitChance(caster,target,skill);
                            foreach(var effect in skill.Effects.Where(e=>e.EffectType==EffectType.ApplyStatus && e.StatusDamageMultiplier>0))
                            {
                                Assert.That(effect.DamageValueSource,Is.EqualTo(DamageValueSource.AlchemicalPower));
                                var tick=StatusEffectSystem.CalculateStatusDamagePerTurn(caster,effect);
                                var total=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(target,effect.StatusId,tick)*effect.DurationTurns;
                                row.dotTotal+=total;
                                row.dotExpectedTotal+=total*StatusEffectSystem.CalculateStatusApplicationChance(caster,target,effect.StatusId,effect.BaseChance);
                            }
                            row.dotHpPercent=row.dotTotal/hp*100;
                            foreach(var effect in skill.Effects.Where(e=>e.EffectType==EffectType.DealDamage))
                            {
                                Assert.That(effect.DamageValueSource,Is.EqualTo(DamageValueSource.AlchemicalPower));
                                var raw=CombatDamageValueCalculator.CalculateDamageValue(caster,effect.DamageValueSource,effect.DamageMultiplier);
                                var damage=CombatDamageSystem.ApplyDefense(caster,target,raw,effect.DamageType);
                                row.raw+=raw;row.damage+=damage;
                                if(effect.DamageType==DamageType.Physical)row.physicalPart+=damage;else row.magicalPart+=damage;
                            }
                            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,target,skill);
                            Assert.That(preview.Damage.Minimum,Is.EqualTo(row.damage),definition.Id+" "+level+" "+recipe.Key);
                            Assert.That(preview.Damage.Maximum,Is.EqualTo(row.damage));
                            if (!boost && (recipe.Key=="2P" || recipe.Key=="2P1M") && new[]{1,10,20,48,50}.Contains(level))
                            {
                                var damageSystem=new CombatDamageSystem();
                                var executionSession=new CombatSession { ActiveUnit=caster };
                                executionSession.Units.Add(caster);executionSession.Units.Add(target);
                                var healthDamage=0;
                                foreach(var effect in skill.Effects.Where(e=>e.EffectType==EffectType.DealDamage))
                                {
                                    var raw=CombatDamageValueCalculator.CalculateDamageValue(caster,effect.DamageValueSource,effect.DamageMultiplier);
                                    healthDamage+=damageSystem.ResolveAndApplyDamage(executionSession,caster,target,raw,effect.DamageType,wasHit:true).HealthDamage;
                                }
                                Assert.That(healthDamage,Is.EqualTo(row.damage),"Actual damage application disagrees with preview: "+definition.Id+" "+recipe.Key+" "+level);
                                report.executionSamples++;
                            }
                            row.currentPreviewHitChance=preview.HitChance;
                            row.expectedDamage=row.physicalPart*row.physicalHitChance+row.magicalPart;
                            row.damageHpPercent=row.damage/hp*100;
                            row.normalHit=CombatDamageSystem.ApplyDefense(caster,target,Mathf.RoundToInt(CombatDamageValueCalculator.GetAlchemicalReferenceAttack(level)),DamageType.Physical);
                            row.normalHitRatio=row.damage/(float)Mathf.Max(1,row.normalHit);
                            report.rows.Add(row);
                        }
                    }
                    finally { Object.DestroyImmediate(skill); }
                }
            }
            try
            {
                foreach(var level in report.levels)
                {
                    var values=new List<float>();
                    for(int index=0;index<4;index++)
                    {
                        var hero=EnemyScalingBalanceTests.Hero(index,level,developed:false);
                        foreach(EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                        {
                            var item=hero.Equipment.GetItem(slot); if(item==null)continue;
                            if(slot!=EquipmentSlot.Weapon && slot!=EquipmentSlot.Armor) { hero.Equipment.SetItem(slot,null); continue; }
                            item.Quality=EquipmentQuality.Normal;item.RecalculateStatModifiers();
                        }
                        new CharacterStatPipelineSystem().Refresh(hero);
                        values.Add((hero.Stats.MinAttack.GetValue()+hero.Stats.MaxAttack.GetValue())*.5f);
                    }
                    report.currentNormalReferences.Add(new Reference { level=level,
                        hardcoded=CombatDamageValueCalculator.GetAlchemicalReferenceAttack(level),actualMean=values.Average(),heroAttacks=values.ToArray() });
                }
            }
            finally { new EnemyScalingBalanceTests().ReleaseReferenceObjects(); }
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/alchemy-balance-20261021"));
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"production-audit.json"),JsonUtility.ToJson(report,true));
            Assert.That(report.rows.Count,Is.EqualTo(13*9*2*18));
        }
    }
}
