using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [TestCase(1)] [TestCase(5)] [TestCase(10)] [TestCase(15)] [TestCase(20)]
        [TestCase(25)] [TestCase(30)] [TestCase(40)] [TestCase(50)]
        [Explicit("Static before/after stat and skill expectations, no autoplay.")]
        public void AuditHeroBasedEnemyProfiles(int level)
        {
            const string folder="../LocalHandoffs/combat-economy-ui-20261026";
            var report=new TheoryExport();var factory=new CharacterInstanceFactory();
            var current=PointCatalog.CharacterDefinitions.Where(d=>d!=null&&d.ClassData!=null&&d.Id.StartsWith("enemy.")
                && Godmist.Editor.EnemyArchetypeStatsSetup.Configure(d.Id,new SerializedObject(d))).ToArray();
            // Configure touches only a SerializedObject buffer here; never applies it to assets.
            Assert.That(current.Length,Is.EqualTo(14));
            var before=current.Select(d=>{
                var clone=ScriptableObject.CreateInstance<CharacterDefinition>();temporary.Add(clone);
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(folder,"before-definitions",d.Id+".json")),clone);return clone;
            }).ToArray();
            var immutable=current.ToDictionary(d=>d,EditorJsonUtility.ToJson);
            foreach(var profile in new[]{0,1})foreach(var heroIndex in Enumerable.Range(0,4))foreach(var material in Enumerable.Range(0,4))
            {
                var hero=PointUnit(EnemyScalingBalanceTests.Hero(heroIndex,level,material:material,treeIndex:profile),true);
                report.stats.Add(TheorySnapshot(hero,"hero",profile,material));
                foreach(var stage in new[]{"before","after"})foreach(var def in stage=="before"?before:current)
                {
                    var enemy=PointUnit(factory.Create(def,def.ClassData,level),false);
                    if(profile==0&&heroIndex==0&&material==0)report.stats.Add(TheorySnapshot(enemy,stage,-1,-1));
                    var physical=PointExpected(hero,enemy,DamageType.Physical);var magical=PointExpected(hero,enemy,DamageType.Magical);
                    var incoming=PointExpected(enemy,hero,PointDamageType(enemy.Character));
                    report.hits.Add(new TheoryHit{stage=stage,enemy=def.Id,hero=hero.Character.Definition.Id,level=level,profile=profile,material=material,
                        physical=physical,magical=magical,incoming=incoming,physicalHits=enemy.Character.CurrentHealth/physical,
                        magicalHits=enemy.Character.CurrentHealth/magical,incomingHits=hero.Character.CurrentHealth/incoming});
                    // Eight real builds per level, actual skill chance/cost/penetration and expected DoT.
                    if(material!=0)continue;
                    foreach(var skill in hero.Character.GetKnownSkills().Where(s=>s.Effects.Any(e=>e.EffectType==EffectType.DealDamage||e.StatusDamageMultiplier>0)))
                        report.skills.Add(TheoryCard(hero,enemy,skill,stage,profile));
                    foreach(var skill in enemy.Character.GetKnownSkills().Where(s=>s.Effects.Any(e=>e.EffectType==EffectType.DealDamage||e.StatusDamageMultiplier>0)))
                        report.skills.Add(TheoryCard(enemy,hero,skill,stage,profile));
                    hero.Position=new GridPosition(2,2);
                }
            }
            foreach(var pair in immutable)Assert.That(EditorJsonUtility.ToJson(pair.Key),Is.EqualTo(pair.Value));
            File.WriteAllText(Path.Combine(folder,"theory-level-"+level+".json"),JsonUtility.ToJson(report,true));
        }
    }
}
