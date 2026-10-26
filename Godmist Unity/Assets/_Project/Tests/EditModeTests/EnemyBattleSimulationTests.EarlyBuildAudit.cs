using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string EarlyBuildFolder=>Path.GetFullPath("../LocalHandoffs/early-build-expeditions-20261019");
        static readonly int[] EarlyBuildLevels={1,5,10,20,30,40,48};
        static readonly int[] EarlyBuildProfiles={0,1,2,4};
        [Serializable] sealed class EarlyTreeInput
        {public string id,name;public int ranks;public string[] nodes;}
        [Serializable] sealed class EarlyHeroInput
        {
            public int level,profile,hero,budget,spent,weaponLevel,armorLevel;
            public string id,resource,weaponQuality,armorQuality;
            public float hp,attack,physicalDefense,magicalDefense;
            public List<EarlyTreeInput> trees=new();public string[] skills;
        }
        [Serializable] sealed class EarlyBuildManifest {public List<EarlyHeroInput> heroes=new();}

        [Test]
        public void EarlyBuildInputsUseLegalTreesUnupgradedGearAndActualLoadouts()
        {
            Directory.CreateDirectory(EarlyBuildFolder);var report=new EarlyBuildManifest();
            foreach(var level in EarlyBuildLevels)
            {
                var enemies=PointEnemies(level);
                foreach(var profile in EarlyBuildProfiles)
                for(var index=0;index<4;index++)
                {
                    var gear=PointInput.rows.Single(r=>r.level==level&&r.hero==index&&r.weaponTier==0&&r.armorTier==0);
                    var hero=PointHero(gear,RealisticProfile(index,profile),PointBudget(level),0,enemies);
                    SetExpeditionLoadout(hero,RealisticProfile(index,profile));
                    var weapon=hero.Equipment.GetItem(EquipmentSlot.Weapon);var armor=hero.Equipment.GetItem(EquipmentSlot.Armor);
                    var row=new EarlyHeroInput{level=level,profile=profile,hero=index,id=hero.Definition.Id,budget=PointBudget(level),
                        spent=hero.SkillTreeProgress.Sum(t=>t.NodeInvestments.Sum(n=>n.Ranks)),hp=hero.Stats.MaxHealth.GetValue(),
                        attack=(hero.Stats.MinAttack.GetValue()+hero.Stats.MaxAttack.GetValue())*.5f,
                        physicalDefense=hero.Stats.PhysicalDefense.GetValue(),magicalDefense=hero.Stats.MagicalDefense.GetValue(),resource=hero.ResourcePool.ResourceType.ToString(),
                        weaponLevel=weapon.RequiredLevel,armorLevel=armor.RequiredLevel,weaponQuality=weapon.Quality.ToString(),armorQuality=armor.Quality.ToString(),
                        skills=hero.GetActiveSkills().Select(s=>s.SkillId).ToArray()};
                    foreach(var tree in hero.ClassData.SkillTrees)
                    {
                        var progress=hero.SkillTreeProgress.FirstOrDefault(t=>t.TreeId==tree.Id);
                        row.trees.Add(new EarlyTreeInput{id=tree.Id,name=tree.DisplayName,ranks=progress?.NodeInvestments.Sum(n=>n.Ranks)??0,
                            nodes=progress?.NodeInvestments.Select(n=>n.NodeId+":"+n.Ranks).ToArray()??Array.Empty<string>()});
                    }
                    Assert.That(row.spent,Is.EqualTo(row.budget));
                    Assert.That(weapon.RequiredLevel,Is.LessThanOrEqualTo(level));Assert.That(armor.RequiredLevel,Is.LessThanOrEqualTo(level));
                    Assert.That(weapon.UpgradeProgress,Is.Zero);Assert.That(armor.UpgradeProgress,Is.Zero);
                    Assert.That(hero.GetActiveSkills().All(s=>hero.GetKnownSkills().Any(k=>k.SkillId==s.SkillId)),Is.True);
                    report.heroes.Add(row);
                }
            }
            Assert.That(report.heroes.Count,Is.EqualTo(7*4*4));
            File.WriteAllText(Path.Combine(EarlyBuildFolder,"build-inputs.json"),JsonUtility.ToJson(report,true));
        }

        static IEnumerable<TestCaseData> EarlyBuildCases()
        {
            foreach(var level in EarlyBuildLevels)
            foreach(var profile in EarlyBuildProfiles)
            foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(level,profile,biome);
        }
        [TestCaseSource(nameof(EarlyBuildCases))]
        [Explicit("Read-only current-v7 full expedition matrix: earlier levels and legal alternative builds, four paired seeds per cell.")]
        public void AuditEarlyBuildExpeditions(int level,int profile,string biome)
            =>RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,4,$"L{level}-P{profile}-{biome}",
                outputFolder:Path.Combine(EarlyBuildFolder,"cells",$"L{level}-P{profile}-{biome}"),fullOnly:true,traceKnockdowns:true,previousBuildPolicy:true);
    }
}
