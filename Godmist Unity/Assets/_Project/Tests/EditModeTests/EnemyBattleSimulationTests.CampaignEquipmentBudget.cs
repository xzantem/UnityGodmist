using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Data.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string CampaignBudgetFolder => Path.GetFullPath("../LocalHandoffs/campaign-equipment-budget-20261020");
        [Serializable] sealed class CampaignBudgetItem
        { public int hero,slot,tier,rarity,progress; public bool master; public PointGearStat[] stats; }
        [Serializable] sealed class CampaignBudgetInputRow
        { public int id,level,hero; public CampaignBudgetItem weapon,armor; public int[] jewelry; }
        [Serializable] sealed class CampaignBudgetInput
        { public CampaignBudgetInputRow[] rows; public JewelryAuditTier[] jewelry; }
        [Serializable] sealed class CampaignBudgetMetrics
        { public string biome; public double heroPhysical,heroMagical,enemy,damagePerAv; }
        [Serializable] sealed class CampaignBudgetResult
        { public int id,level,hero; public float hp,min,max,speed; public List<CampaignBudgetMetrics> metrics=new(); }
        [Serializable] sealed class CampaignBudgetOutput
        { public string inputSha256; public List<CampaignBudgetResult> rows=new(); }

        [Test,Explicit("Bounded L1-20 loot valuation for the campaign budget; no encounters or combat run.")]
        [Timeout(240000)]
        public void AuditCampaignEquipmentLootIncome()
        {
            var output=new EconomyExport();
            foreach(var biome in new[]{"forest","catacombs"})
            {
                var definition=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                foreach(var level in Enumerable.Range(1,20))
                {
                    LootTableData theme=null;
                    var hasTheme=definition.LootTableCatalog!=null&&definition.LootTableCatalog.TryGetEnemyLootTable(definition.Theme,level,out theme);
                    Assert.That(hasTheme,Is.True);
                    var common=EconomySampleLoot(theme,level,12349+level,1024);
                    foreach(var enemy in PointEnemyDefinitions.Where(e=>e.Id.StartsWith("enemy."+biome+".")))
                    {
                        var species=EconomySampleLoot(enemy.LootTable,level,23641+level,256);
                        output.loot.Add(new EconomyLoot{biome=biome,enemy=enemy.Id,level=level,hasThemeLoot=true,
                            table=theme.name,samples=1024,meanSell=common.mean+species.mean,se=Math.Sqrt(common.se*common.se+species.se*species.se)});
                    }
                    LootTableData chest=null;definition.LootTableCatalog.TryGetChestLootTable(definition.Theme,level,out chest);
                    var value=EconomySampleLoot(chest,level,39467+level,1024);
                    output.loot.Add(new EconomyLoot{biome=biome,enemy="CHEST",level=level,hasThemeLoot=chest!=null,
                        table=chest?.name,samples=1024,meanSell=value.mean,se=value.se});
                }
            }
            File.WriteAllText(Path.Combine(CampaignBudgetFolder,"loot-income.json"),JsonUtility.ToJson(output));
            Assert.That(output.loot.Count,Is.EqualTo(320));
        }

        [Test,Explicit("Read-only evaluation of budgeted persistent equipment histories; no fights or AI.")]
        [Timeout(600000)]
        public void AuditCampaignEquipmentBudget()
        {
            var input=JsonUtility.FromJson<CampaignBudgetInput>(File.ReadAllText(Path.Combine(CampaignBudgetFolder,"input.json")));
            var output=new CampaignBudgetOutput();
            using(var hash=SHA256.Create()) output.inputSha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(CampaignBudgetFolder,"input.json")))).Replace("-","").ToLowerInvariant();
            foreach(var group in input.rows.GroupBy(r=>r.level).OrderBy(g=>g.Key))
            {
                var enemies=PointEnemies(group.Key);
                foreach(var row in group)
                {
                    var definition=PointHeroDefinition(row.hero);
                    var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,row.level);
                    hero.SkillTreeProgress=PointProgress(row.hero,PointBudget(row.level),new[]{1,0,1,0}[row.hero]);
                    foreach(var g in new[]{row.weapon,row.armor})
                    {
                        Assert.That(g.hero,Is.EqualTo(row.hero));
                        Assert.That(g.progress,Is.InRange(0,100));
                        if(g.tier==1) Assert.That(g.progress,Is.Zero);
                        var slot=g.slot==0?EquipmentSlot.Weapon:EquipmentSlot.Armor;
                        var item=PointItem(hero,g.stats,slot,g.progress/100f,g.tier,g.master);
                        item.CurrentRarity=(RarityTier)g.rarity;item.RecalculateStatModifiers();
                        hero.Equipment.SetItem(slot,item);
                    }
                    for(var s=0;s<4;s++)
                    {
                        var j=input.jewelry.SingleOrDefault(t=>t.level==row.jewelry[s]);
                        if(j==null)continue;
                        Assert.That(j.level,Is.LessThanOrEqualTo(row.level));
                        var slot=new[]{EquipmentSlot.Necklace,EquipmentSlot.Ring,EquipmentSlot.Earrings,EquipmentSlot.Bracelet}[s];
                        var asset=new[]{"EID_Necklace","EID_Ring","EID_Earrings","EID_Bracelet"}[s];
                        var mods=s==0?new[]{JewelryAuditStat(CharacterStatType.MaxHealth,j.hp)}
                            :s==1?new[]{JewelryAuditStat(CharacterStatType.MinAttack,j.atk),JewelryAuditStat(CharacterStatType.MaxAttack,j.atk)}
                            :s==2?new[]{JewelryAuditStat(CharacterStatType.Speed,j.speed)}:Array.Empty<StatModifier>();
                        JewelryAuditEquip(hero,slot,asset,mods);
                        var item=hero.Equipment.GetItem(slot);item.RequiredLevel=item.ItemLevel=j.level;
                    }
                    PointRefresh(hero);var unit=PointUnit(hero,true);var stats=hero.Stats;
                    var result=new CampaignBudgetResult{id=row.id,level=row.level,hero=row.hero,
                        hp=stats.MaxHealth.GetValue(),min=stats.MinAttack.GetValue(),max=stats.MaxAttack.GetValue(),speed=stats.Speed.GetValue()};
                    var gems=row.jewelry.Count(t=>t>0);
                    foreach(var biome in new[]{"both","catacombs","forest"})
                    {
                        var selection=enemies.Where(e=>biome=="both"||e.Character.Definition.Id.StartsWith("enemy."+biome+".")).ToArray();
                        var physical=selection.Where(e=>PointDamageType(e.Character)==DamageType.Physical).ToArray();
                        var magical=selection.Where(e=>PointDamageType(e.Character)==DamageType.Magical).ToArray();
                        result.metrics.Add(new CampaignBudgetMetrics{biome=biome,
                            heroPhysical=physical.Average(e=>hero.CurrentHealth/PointExpected(e,unit,PointDamageType(e.Character))),
                            heroMagical=magical.Average(e=>hero.CurrentHealth/PointExpected(e,unit,PointDamageType(e.Character))),
                            enemy=selection.Average(e=>e.Character.CurrentHealth/JewelryAuditExpected(unit,e,PointDamageType(hero),1,1+.025f*gems)),
                            damagePerAv=selection.Average(e=>JewelryAuditExpected(unit,e,PointDamageType(hero),1,1+.025f*gems))*stats.Speed.GetValue()/10000});
                    }
                    Assert.That(result.metrics.All(m=>double.IsFinite(m.enemy)&&m.enemy>0&&m.heroPhysical>0&&m.heroMagical>0),Is.True);
                    output.rows.Add(result);
                }
                File.AppendAllText(Path.Combine(CampaignBudgetFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" L"+group.Key+" rows="+output.rows.Count+Environment.NewLine);
            }
            Assert.That(output.rows.Count,Is.EqualTo(input.rows.Length));
            File.WriteAllText(Path.Combine(CampaignBudgetFolder,"export.json"),JsonUtility.ToJson(output));
        }
    }
}
