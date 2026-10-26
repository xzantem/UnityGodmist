using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
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
        [Serializable] sealed class DruidPolarityRow
        {
            public string stage,hero;public int level,profile,material,growths,ap,rp;
            public float direct;
        }
        [Serializable] sealed class DruidPolarityReport {public List<DruidPolarityRow> rows=new();}
        [TestCase(1),TestCase(10),TestCase(20),TestCase(30),TestCase(40),TestCase(48),TestCase(50)]
        [Explicit("Read-only Druid basic preview at fixed 0/1/3 owned growths. No summoning, casts, AI, turns or RNG.")]
        [Timeout(120000)]
        public void ExportDruidPolarityContexts(int level)
        {
            var folder=Path.GetFullPath("../LocalHandoffs/enemy-role-polarity-20261025");Directory.CreateDirectory(folder);
            var current=PointCatalog.CharacterDefinitions.Single(d=>d!=null&&d.Id=="enemy.forest.elf_druid");
            var immutable=EditorJsonUtility.ToJson(current);
            var original=ScriptableObject.CreateInstance<CharacterDefinition>();temporary.Add(original);
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(RoleFolder,"before-definitions",current.Id+".json")),original);
            var growth=PointCatalog.CharacterDefinitions.Single(d=>d!=null&&d.Id=="summon.forest.poisongrowth");
            var factory=new CharacterInstanceFactory();var report=new DruidPolarityReport();
            foreach(var profile in new[]{0,1})foreach(var heroIndex in Enumerable.Range(0,4))foreach(var material in Enumerable.Range(0,4))
            {
                var hero=PointUnit(EnemyScalingBalanceTests.Hero(heroIndex,level,material:material,treeIndex:profile),true);
                foreach(var stage in new[]{"before","after"})foreach(var count in new[]{0,1,3})
                {
                    var definition=stage=="before"?original:current;
                    var druid=PointUnit(factory.Create(definition,definition.ClassData,level),false);
                    var session=new CombatSession();session.Units.Add(druid);session.Units.Add(hero);
                    druid.WarriorState.Session=session;
                    for(var i=0;i<count;i++)
                    {
                        var unit=PointUnit(factory.Create(growth,growth.ClassData,level),false);
                        session.Summons.Add(new SummonedBattleUnit(unit,druid.Character.CharacterId,"audit.growth",0,-1,i){GroupId="forest.growth"});
                    }
                    Assert.That(SummonSystem.CountOwnedSummons(session,druid,"forest.growth"),Is.EqualTo(count));
                    var random=UnityEngine.Random.state;
                    var card=TheoryCard(druid,hero,druid.Character.GetBasicAttackSkill(),stage,profile,material:material);
                    Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
                    Assert.That(card.direct,count==0?Is.EqualTo(0):Is.GreaterThan(0));
                    report.rows.Add(new DruidPolarityRow{stage=stage,hero=hero.Character.Definition.Id,level=level,profile=profile,material=material,growths=count,
                        direct=card.direct,ap=card.ap,rp=card.rp});
                }
            }
            Assert.That(report.rows,Has.Count.EqualTo(192));
            Assert.That(EditorJsonUtility.ToJson(current),Is.EqualTo(immutable));
            File.WriteAllText(Path.Combine(folder,"druid-level-"+level+".json"),JsonUtility.ToJson(report,true));
        }
    }
}
