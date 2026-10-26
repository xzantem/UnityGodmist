using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class EnemyRoleBalanceTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        static CharacterDefinition Definition(string id)=>Catalog.CharacterDefinitions.Single(d=>d!=null&&d.Id==id);
        static SerializedProperty Field(SerializedObject data,string group,string name)=>data.FindProperty("<"+group+">k__BackingField").FindPropertyRelative("<"+name+">k__BackingField");
        [Test,Explicit("Apply user-authorized role-stat changes to production CharacterDefinitions through Editor API only.")]
        public void AuthorApprovedContent()
        {
            var definitions=Catalog.CharacterDefinitions.Where(d=>d!=null&&d.Id.StartsWith("enemy.")&&d.name!="CD_Enemy_Test"&&!d.Id.Contains(".test")).ToArray();
            var factory=new CharacterInstanceFactory();var levels=new[]{1,10,20,30,40,48,50};
            var originals=definitions.ToDictionary(d=>d.Id,d=>EditorJsonUtility.ToJson(d));
            var stats=definitions.SelectMany(d=>levels.Select(level=>(id:d.Id,level,c:factory.Create(d,d.ClassData,level))))
                .ToDictionary(r=>(r.id,r.level),r=>(hp:r.c.Stats.MaxHealth.GetValue(),min:r.c.Stats.MinAttack.GetValue(),max:r.c.Stats.MaxAttack.GetValue(),
                    speed:r.c.Stats.Speed.GetValue(),accuracy:r.c.WeaponAccuracy,resource:r.c.Stats.MaxResource.GetValue(),regen:r.c.Stats.ResourceRegeneration.GetValue()));
            var folder=Path.GetFullPath("../LocalHandoffs/enemy-role-implementation-20261025");Directory.CreateDirectory(Path.Combine(folder,"before-definitions"));
            foreach(var pair in originals)
            {
                var path=Path.Combine(folder,"before-definitions",pair.Key+".json");if(!File.Exists(path))File.WriteAllText(path,pair.Value);
            }
            EnemyRoleStatsSetup.ApplyAll();
            foreach(var d in definitions)foreach(var level in levels)
            {
                var c=factory.Create(d,d.ClassData,level);var before=stats[(d.Id,level)];
                Assert.That(c.Stats.MaxHealth.GetValue(),Is.EqualTo(before.hp),d.Id+" HP");
                Assert.That(c.Stats.MinAttack.GetValue(),Is.EqualTo(before.min),d.Id+" ATK minimum");
                Assert.That(c.Stats.MaxAttack.GetValue(),Is.EqualTo(before.max),d.Id+" ATK maximum");
                Assert.That(c.Stats.Speed.GetValue(),Is.EqualTo(before.speed),d.Id+" speed");
                Assert.That(c.WeaponAccuracy,Is.EqualTo(before.accuracy));Assert.That(c.Stats.MaxResource.GetValue(),Is.EqualTo(before.resource));
                Assert.That(c.Stats.ResourceRegeneration.GetValue(),Is.EqualTo(before.regen));
            }
            Assert.That(EditorJsonUtility.ToJson(Definition("enemy.catacombs.spectral_knight")),Is.EqualTo(originals["enemy.catacombs.spectral_knight"]));
            Directory.CreateDirectory(Path.Combine(folder,"after-definitions"));
            foreach(var definition in definitions)
                File.WriteAllText(Path.Combine(folder,"after-definitions",definition.Id+".json"),EditorJsonUtility.ToJson(definition));
        }
        [TestCase(1,40,12)][TestCase(10,85,28)][TestCase(20,224,82)]
        [TestCase(30,433,176)][TestCase(40,687,312)][TestCase(48,979,491)][TestCase(50,1180,604)]
        public void DefenderUsesApprovedArmorAtEachAnchor(int level,float physical,float magical)
        {
            var original=Definition("enemy.catacombs.defender");var clone=Object.Instantiate(original);
            try
            {
                EnemyRoleStatsSetup.ApplyToDefinition(clone);var c=new CharacterInstanceFactory().Create(clone,clone.ClassData,level);
                Assert.That(c.Stats.PhysicalDefense.GetValue(),Is.EqualTo(physical));Assert.That(c.Stats.MagicalDefense.GetValue(),Is.EqualTo(magical));
                Assert.That(c.Stats.Dodge.GetValue(),Is.InRange(7,15.115f));
            }
            finally{Object.DestroyImmediate(clone);}
        }
        [TestCase("enemy.catacombs.defender")][TestCase("enemy.catacombs.wraith")][TestCase("enemy.forest.infected_ent.burning")]
        [TestCase("enemy.forest.bandit_leader.revealed")][TestCase("enemy.forest.elf_archer")]
        public void AuthoringIsIdempotentAndDoesNotChangeCoreStats(string id)
        {
            var clone=Object.Instantiate(Definition(id));
            try
            {
                var health=clone.BaseStats.MaxHealth;var attack=clone.BaseStats.MaxAttack;var multiplier=clone.AttackMultiplier;
                EnemyRoleStatsSetup.ApplyToDefinition(clone);var first=EditorJsonUtility.ToJson(clone);
                Assert.That(EnemyRoleStatsSetup.ApplyToDefinition(clone),Is.False);Assert.That(EditorJsonUtility.ToJson(clone),Is.EqualTo(first));
                Assert.That(clone.BaseStats.MaxHealth,Is.EqualTo(health));Assert.That(clone.BaseStats.MaxAttack,Is.EqualTo(attack));
                Assert.That(clone.AttackMultiplier,Is.EqualTo(multiplier));
                Assert.That(clone.GrowthData.DefenseCurve.Length,Is.EqualTo(id is "enemy.catacombs.defender" or "enemy.catacombs.wraith" or "enemy.forest.elf_archer"?9:6));
            }
            finally{Object.DestroyImmediate(clone);}
        }
        [Test]
        public void CurveInterpolatesGrowthUnitsAndRefreshNeverHealsOrCompounds()
        {
            var clone=Object.Instantiate(Definition("enemy.catacombs.defender"));
            try
            {
                EnemyRoleStatsSetup.ApplyToDefinition(clone);var c=new CharacterInstanceFactory().Create(clone,clone.ClassData,12);
                Assert.That(c.Stats.PhysicalDefense.GetValue(),Is.EqualTo(127)); // G12=13: 40% between G10=9 and G15=19, rounded once.
                c.CurrentHealth=3;var maximum=c.Stats.MaxHealth.GetValue();var attack=c.Stats.MaxAttack.GetValue();
                var growth=new CharacterLevelGrowthSystem();growth.Apply(c,false);growth.Apply(c,false);
                Assert.That(c.CurrentHealth,Is.EqualTo(3));Assert.That(c.Stats.PhysicalDefense.GetValue(),Is.EqualTo(127));
                Assert.That(c.Stats.MaxHealth.GetValue(),Is.EqualTo(maximum));Assert.That(c.Stats.MaxAttack.GetValue(),Is.EqualTo(attack));
            }
            finally{Object.DestroyImmediate(clone);}
        }
        [TestCase(false)][TestCase(true)]
        public void AbsentOrMalformedCurvePreservesLegacyGrowth(bool malformed)
        {
            var clone=Object.Instantiate(Definition("enemy.catacombs.defender"));
            try
            {
                var data=new SerializedObject(clone);Field(data,"BaseStats","PhysicalDefense").floatValue=17;Field(data,"GrowthData","PhysicalDefense").floatValue=1.75f;
                Field(data,"BaseStats","MagicalDefense").floatValue=11;Field(data,"GrowthData","MagicalDefense").floatValue=2.25f;
                var curve=Field(data,"GrowthData","DefenseCurve");curve.arraySize=malformed?2:0;
                if(malformed)for(var i=0;i<2;i++)
                {
                    var knot=curve.GetArrayElementAtIndex(i);knot.FindPropertyRelative("<Level>k__BackingField").intValue=10;
                    knot.FindPropertyRelative("<PhysicalDefense>k__BackingField").floatValue=1000;
                    knot.FindPropertyRelative("<MagicalDefense>k__BackingField").floatValue=1000;
                }
                data.ApplyModifiedPropertiesWithoutUndo();var c=new CharacterInstanceFactory().Create(clone,clone.ClassData,15);
                Assert.That(c.Stats.PhysicalDefense.BaseValue,Is.EqualTo(17+19*1.75f));
                Assert.That(c.Stats.MagicalDefense.BaseValue,Is.EqualTo(11+19*2.25f));
            }
            finally{Object.DestroyImmediate(clone);}
        }
        [Test]
        public void UnsupportedDefinitionsAndThreeHitKnightStayUntouched()
        {
            foreach(var id in new[]{"wladimir","enemy.catacombs.spectral_knight"})
            {
                var clone=Object.Instantiate(Definition(id));
                try{var before=EditorJsonUtility.ToJson(clone);Assert.That(EnemyRoleStatsSetup.ApplyToDefinition(clone),Is.False);Assert.That(EditorJsonUtility.ToJson(clone),Is.EqualTo(before));}
                finally{Object.DestroyImmediate(clone);}
            }
        }
    }
}
