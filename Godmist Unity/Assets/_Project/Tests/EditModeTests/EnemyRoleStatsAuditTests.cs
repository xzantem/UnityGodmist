using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.World;
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
    // Analysis only. Never runs the authoring/setup fixture or saves an asset.
    public class EnemyRoleStatsAuditTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        [Serializable] public class Snapshot
        {
            public string id, name, role, scenario;
            public int level, material, weaponLevel, armorLevel;
            public float hp, minimum, maximum, physical, magical, dodge, critical, criticalDamage, speed, accuracy, physicalPenetration, magicalPenetration;
            public string definition;
        }
        [Serializable] public class Matchup
        {
            public string attacker, target, skill, scenario;
            public int level, material, min, max, unarmoredMin, unarmoredMax;
            public float hit, crit, critDamage;
        }
        [Serializable] public class Content { public string id, json; }
        [Serializable] public class Plan { public int[] levels; public float[] physical, magical; }
        [Serializable] public class Counter
        {
            public int level, normalRaw, normalAfterArmor, doubleRaw, doubleAfterArmor, dischargeAfterArmor;
            public float armor, armorMagic, normalReduction, doubleReduction, dischargeReduction;
            public Matchup before, after;
        }
        [Serializable] public class Audit
        {
            public List<Snapshot> stats = new();
            public List<Matchup> matchups = new();
            public List<Content> skills = new();
        }
        static Snapshot Row(CharacterInstance c, string scenario, int material)
        {
            var s=c.Stats;
            return new Snapshot {id=c.Definition.Id,name=c.Definition.DisplayName,role=c.Definition.AiRole.ToString(),scenario=scenario,
                level=c.Level,material=material,weaponLevel=c.Equipment.GetItem(EquipmentSlot.Weapon)?.ItemLevel??0,
                armorLevel=c.Equipment.GetItem(EquipmentSlot.Armor)?.ItemLevel??0,hp=s.MaxHealth.GetValue(),minimum=s.MinAttack.GetValue(),
                maximum=s.MaxAttack.GetValue(),physical=s.PhysicalDefense.GetValue(),magical=s.MagicalDefense.GetValue(),dodge=s.Dodge.GetValue(),
                critical=s.CritChance.GetValue(),criticalDamage=s.CritDamage.GetValue(),speed=s.Speed.GetValue(),accuracy=c.WeaponAccuracy,
                physicalPenetration=s.PhysicalDefensePenetration.GetValue(),magicalPenetration=s.MagicalDefensePenetration.GetValue(),
                definition=EditorJsonUtility.ToJson(c.Definition)};
        }
        static Matchup Compare(CharacterInstance source, CharacterInstance target, bool player, string scenario,int material)
        {
            var a=new BattleUnit{Character=source,IsPlayerSide=player,Position=new GridPosition(2,2)};
            var b=new BattleUnit{Character=target,IsPlayerSide=!player,Position=new GridPosition(3,2)};
            var skill=source.GetBasicAttackSkill();
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(a,b,skill);
            var physical=target.Stats.PhysicalDefense;var magical=target.Stats.MagicalDefense;
            CombatSkillTargetPreview raw;
            try
            {
                target.Stats.PhysicalDefense=new Stat(0);target.Stats.MagicalDefense=new Stat(0);
                raw=CombatSkillPreviewCalculator.CalculateTargetPreview(a,b,skill);
            }
            finally {target.Stats.PhysicalDefense=physical;target.Stats.MagicalDefense=magical;}
            return new Matchup{attacker=source.Definition.Id,target=target.Definition.Id,skill=skill.SkillId,scenario=scenario,material=material,
                level=source.Level,min=preview.Damage.Minimum,max=preview.Damage.Maximum,unarmoredMin=raw.Damage.Minimum,
                unarmoredMax=raw.Damage.Maximum,hit=preview.HasHitChance?preview.HitChance:1f,crit=source.Stats.CritChance.GetValue(),critDamage=source.Stats.CritDamage.GetValue()};
        }
        static void EquipProduction(CharacterInstance hero, GameContentCatalog catalog, int material)
        {
            var parts=catalog.EquipmentPartDefinitions.Where(p=>p!=null&&p.RequiredClass==hero.ClassData
                &&!p.IsStarterOnly&&(int)p.LevelBracket<=hero.Level).ToArray();
            if(parts.Length==0)return;
            var bracket=parts.Max(p=>(int)p.LevelBracket);
            var quality=hero.Level==50?EquipmentQuality.Masterpiece:hero.Level>=bracket+3?EquipmentQuality.Excellent:EquipmentQuality.Normal;
            foreach(var slot in new[]{EquipmentSlot.Weapon,EquipmentSlot.Armor})
            {
                var type=slot==EquipmentSlot.Weapon?ItemType.Weapon:ItemType.Armor;
                var definition=catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().Where(d=>d.RequiredClass==hero.ClassData
                    &&d.ItemType==type&&!d.IsStarterEquipment&&!(d is UniqueEquipmentDefinition)).OrderBy(d=>d.Id).First();
                var kinds=type==ItemType.Weapon?new[]{EquipmentPartKind.WeaponHead,EquipmentPartKind.WeaponBinder,EquipmentPartKind.WeaponHandle}
                    :new[]{EquipmentPartKind.ArmorPlate,EquipmentPartKind.ArmorBinder,EquipmentPartKind.ArmorBase};
                var selected=kinds.Select(kind=>parts.Where(p=>(int)p.LevelBracket==bracket&&p.Kind==kind).OrderBy(p=>p.Id).ToArray())
                    .Select(p=>p[material%p.Length]).ToArray();
                hero.Equipment.SetItem(slot,new EquipmentGenerationService(new System.Random(42)).GenerateFromSelectedParts(definition,selected,quality,RarityTier.Common));
            }
            new CharacterStatPipelineSystem().Refresh(hero);
            hero.CurrentHealth=Mathf.RoundToInt(hero.Stats.MaxHealth.GetValue());
        }
        [Test,Explicit("Read-only export of enemy roles and current hero power; no asset authoring.")]
        public void ExportCurrentRoleAndHeroPower()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var definitions=catalog.CharacterDefinitions.Where(d=>d!=null&&d.Id.StartsWith("enemy.")&&!d.Id.Contains(".test")&&d.name!="CD_Enemy_Test").ToArray();
            var before=definitions.Select(EditorJsonUtility.ToJson).ToArray();
            var audit=new Audit();
            try
            {
                foreach(var level in new[]{1,10,20,30,40,50})
                {
                    var enemies=definitions.Select(d=>new CharacterInstanceFactory().Create(d,d.ClassData,level)).ToArray();
                    foreach(var enemy in enemies)audit.stats.Add(Row(enemy,"enemy",0));
                    foreach(var scenario in new[]{"reference-progression","production-parts"})
                    for(var material=0;material<4;material++)for(var index=0;index<4;index++)
                    {
                        var hero=EnemyScalingBalanceTests.Hero(index,level,material:material);
                        if(scenario=="production-parts")EquipProduction(hero,catalog,material);
                        audit.stats.Add(Row(hero,scenario,material));
                        foreach(var enemy in enemies)
                        {
                            audit.matchups.Add(Compare(hero,enemy,true,scenario,material));
                            audit.matchups.Add(Compare(enemy,hero,false,scenario,material));
                        }
                    }
                }
                foreach(var skill in definitions.SelectMany(d=>d.StartingSkills).Where(s=>s!=null).Distinct())
                    audit.skills.Add(new Content{id=skill.SkillId,json=EditorJsonUtility.ToJson(skill)});
                for(var i=0;i<definitions.Length;i++)Assert.That(EditorJsonUtility.ToJson(definitions[i]),Is.EqualTo(before[i]),definitions[i].Id);
                Assert.That(definitions.Length,Is.EqualTo(23));
                Assert.That(audit.matchups.All(m=>m.unarmoredMax>=m.max),Is.True);
                var output=Path.GetFullPath("../LocalHandoffs/enemy-role-stats-20261025");Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"snapshot.json"),JsonUtility.ToJson(audit,true));
            }
            finally {new EnemyScalingBalanceTests().ReleaseReferenceObjects();}
        }
        [TestCase(1),TestCase(5),TestCase(8),TestCase(10),TestCase(15),TestCase(18),TestCase(20),TestCase(25),
         TestCase(28),TestCase(30),TestCase(35),TestCase(38),TestCase(40),TestCase(45),TestCase(48),TestCase(50)]
        public void CheckDefenderProposalAndPenetrationCounters(int level)
        {
            var folder=Path.GetFullPath("../LocalHandoffs/enemy-role-stats-20261025");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(folder,"proposal.json")));
            var growth=new CharacterLevelGrowthSystem();
            float Curve(float[] values)
            {
                for(var k=1;k<plan.levels.Length;k++)
                    if(level<=plan.levels[k])return Mathf.Lerp(values[k-1],values[k],Mathf.InverseLerp(
                        growth.GetCumulativeGrowthMultiplier(plan.levels[k-1]),growth.GetCumulativeGrowthMultiplier(plan.levels[k]),
                        growth.GetCumulativeGrowthMultiplier(level)));
                return values[values.Length-1];
            }
            // User target: 60-70% against ordinary physical hits before penetration.
            var armor=Mathf.Round(Curve(plan.physical)*2f);var magicArmor=Mathf.Round(Curve(plan.magical)*.2f);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
            var definition=catalog.CharacterDefinitions.Single(d=>d!=null&&d.Id=="enemy.catacombs.defender");
            var serialized=EditorJsonUtility.ToJson(definition);
            try
            {
                var hero=EnemyScalingBalanceTests.Hero(0,level);
                var defender=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                var hp=defender.Stats.MaxHealth.GetValue();var attack=defender.Stats.MaxAttack.GetValue();
                var before=Compare(hero,defender,true,"reference-progression",0);
                defender.Stats.PhysicalDefense=new Stat(armor);defender.Stats.MagicalDefense=new Stat(magicArmor);
                var after=Compare(hero,defender,true,"reference-progression",0);
                var source=new BattleUnit{Character=hero,IsPlayerSide=true};var target=new BattleUnit{Character=defender};
                var raw=Mathf.RoundToInt((before.unarmoredMin+before.unarmoredMax)*.5f);
                var ordinary=CombatDamageSystem.ApplyDefense(source,target,raw,DamageType.Physical);
                var doubleDamage=CombatDamageSystem.ApplyDefense(source,target,raw*2,DamageType.Physical);
                var discharge=CombatDamageSystem.ApplyDefense(source,target,raw*2,DamageType.Physical,defenseIgnorePercent:.8f);
                var result=new Counter{level=level,normalRaw=raw,normalAfterArmor=ordinary,doubleRaw=raw*2,
                    doubleAfterArmor=doubleDamage,dischargeAfterArmor=discharge,armor=armor,armorMagic=magicArmor,
                    normalReduction=1f-(float)ordinary/raw,doubleReduction=1f-(float)doubleDamage/(raw*2),
                    dischargeReduction=1f-(float)discharge/(raw*2),before=before,after=after};
                Assert.That(result.normalReduction,Is.InRange(.5f,.8f));
                Assert.That(result.doubleReduction,Is.LessThan(result.normalReduction));
                Assert.That(result.dischargeReduction,Is.LessThan(.22f));
                Assert.That(defender.Stats.MaxHealth.GetValue(),Is.EqualTo(hp));
                Assert.That(defender.Stats.MaxAttack.GetValue(),Is.EqualTo(attack));
                Assert.That(EditorJsonUtility.ToJson(definition),Is.EqualTo(serialized));
                File.WriteAllText(Path.Combine(folder,"counter-level-"+level+".json"),JsonUtility.ToJson(result,true));
            }
            finally {new EnemyScalingBalanceTests().ReleaseReferenceObjects();}
        }
    }
}
