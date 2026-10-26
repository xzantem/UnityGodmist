using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
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
        static string ShooterRangeFolder=>Path.GetFullPath("../LocalHandoffs/shooter-range-seven-20261019");
        [Serializable] sealed class ShooterRangeProof
        {
            public string id,original,candidate;
        }
        [Serializable] sealed class ShooterRangeProofs
        {
            public string variant="shooter-enemy-target-range-cap7; party-v6 unchanged";
            public int shooters,encounters;
            public List<ShooterRangeProof> skills=new();
        }
        sealed partial class Engine
        {
            // An opt-in experimental definition swap is not a boss transformation.
            internal void AuditShooterDefinition(BattleUnit unit,CharacterDefinition candidate)
            {unit.Character.Definition=candidate;definitions[unit]=candidate;}
        }
        CharacterDefinition ShooterRangeCandidate(CharacterDefinition source,ShooterRangeProofs proof)
        {
            if(clones.TryGetValue(source,out var cached))return cached;
            originals[source]=EditorJsonUtility.ToJson(source);
            var copiedSkills=new Dictionary<SkillData,SkillData>();
            SkillData Copy(SkillData authored)
            {
                if(authored==null||!authored.TargetModes.Any(m=>m.TargetingType==TargetingType.Enemies&&m.AbsoluteRange.MaxRange>7))return authored;
                if(copiedSkills.TryGetValue(authored,out var existing))return existing;
                var before=JsonUtility.ToJson(authored);
                var copy=UnityEngine.Object.Instantiate(authored);copy.name=authored.name;temporary.Add(copy);
                var data=new SerializedObject(copy);var modes=data.FindProperty("targetModes");
                for(var i=0;i<authored.TargetModes.Count;i++)
                    if(authored.TargetModes[i].TargetingType==TargetingType.Enemies&&authored.TargetModes[i].AbsoluteRange.MaxRange>7)
                        modes.GetArrayElementAtIndex(i).FindPropertyRelative("absoluteRange").FindPropertyRelative("maxRange").intValue=7;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(JsonUtility.ToJson(authored),Is.EqualTo(before),"Source skill must remain untouched.");
                proof.skills.Add(new ShooterRangeProof{id=authored.SkillId,original=before,candidate=JsonUtility.ToJson(copy)});
                copiedSkills[authored]=copy;return copy;
            }
            var candidate=UnityEngine.Object.Instantiate(source);candidate.name=source.name;temporary.Add(candidate);
            var definition=new SerializedObject(candidate);
            definition.FindProperty("<BasicAttackSkill>k__BackingField").objectReferenceValue=Copy(source.BasicAttackSkill);
            var starting=definition.FindProperty("<StartingSkills>k__BackingField");
            for(var i=0;i<source.StartingSkills.Count;i++)starting.GetArrayElementAtIndex(i).objectReferenceValue=Copy(source.StartingSkills[i]);
            definition.ApplyModifiedPropertiesWithoutUndo();clones[source]=candidate;
            Assert.That(proof.skills.Select(p=>p.id),Is.EquivalentTo(new[]{"skill.enemy.forest.shooter_shot","skill.enemy.forest.shooter_piercing_bolt"}));
            return candidate;
        }
        void ApplyShooterRange(Engine engine,ShooterRangeProofs proof)
        {
            proof.encounters++;
            foreach(var unit in engine.Session.Units.Where(u=>!u.IsPlayerSide&&u.Character.Definition.Id=="enemy.forest.bandit_shooter"))
            {
                proof.shooters++;
                var candidate=ShooterRangeCandidate(unit.Character.Definition,proof);
                var skills=unit.Character.Skills.Select(s=>candidate.StartingSkills.FirstOrDefault(c=>c!=null&&c.SkillId==s.SkillId)??s).ToList();
                unit.Character.Skills=skills;engine.AuditShooterDefinition(unit,candidate);
                Assert.That(unit.Character.GetBasicAttackSkill().TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies).Max(m=>m.AbsoluteRange.MaxRange),Is.EqualTo(7));
                Assert.That(unit.Character.GetActiveSkills().SelectMany(s=>s.TargetModes).Where(m=>m.TargetingType==TargetingType.Enemies).All(m=>m.AbsoluteRange.MaxRange<=7),Is.True);
            }
        }
        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("Same24 L48 U0 full routes and v6 player; only temporary shooter range cap7.")]
        public void AuditShooterRangeSevenExpeditions(string biome,int firstSeed)
        {
            var proof=new ShooterRangeProofs();
            RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,firstSeed+4,$"range7-L48-U0-{biome}-S{firstSeed}",
                firstSeed:firstSeed,outputFolder:Path.Combine(ShooterRangeFolder,"after"),fullOnly:true,traceKnockdowns:true,
                auditEngine:(engine,battle)=>ApplyShooterRange(engine,proof));
            Assert.That(proof.shooters,biome=="forest"?Is.GreaterThan(0):Is.EqualTo(0));
            Directory.CreateDirectory(ShooterRangeFolder);
            File.WriteAllText(Path.Combine(ShooterRangeFolder,$"proof-{biome}-S{firstSeed}.json"),JsonUtility.ToJson(proof,true));
        }
        [TestCase(0)][TestCase(6)]
        [Explicit("Unmodified forest controls must exactly reproduce archived party-v6 routes.")]
        public void AuditShooterRangeNineControl(int seed)
            =>RunRealisticExpeditions(48,0,"forest",DungeonLength.Normal,0,seed+1,$"range9-forest-S{seed}",
                firstSeed:seed,outputFolder:Path.Combine(ShooterRangeFolder,"control"),fullOnly:true,traceKnockdowns:true);

        [Test]
        public void ShooterRangeCandidateRestrictsBasicAndSpecialWithoutChangingSourceOrCosts()
        {
            var source=PointEnemyDefinitions.Single(d=>d.Id=="enemy.forest.bandit_shooter");
            var npc=new CharacterInstanceFactory().Create(source,source.ClassData,48);
            var unit=PointUnit(npc,false);unit.Position=new GridPosition(1,4);
            var hero=PolicyHero(0,true,8,4);
            var session=new CombatSession{Grid=new BattleGrid(12,10),Units=new(){unit,hero}};
            using var engine=new Engine(session,new Result());
            var skill=unit.Character.GetBasicAttackSkill();var before=JsonUtility.ToJson(skill);
            var stats=JsonUtility.ToJson(npc.Stats);var hp=npc.CurrentHealth;var resource=npc.ResourcePool.CurrentValue;
            var state=UnityEngine.Random.state;var proof=new ShooterRangeProofs();ApplyShooterRange(engine,proof);
            var basic=npc.GetBasicAttackSkill();var targeter=new CombatTargetingService();
            Assert.That(CombatTargetingService.IsPositionWithinRange(unit.Position,hero.Position,basic.TargetModes[0].AbsoluteRange),Is.True);
            hero.Position=new GridPosition(9,4);
            Assert.That(CombatTargetingService.IsPositionWithinRange(unit.Position,hero.Position,basic.TargetModes[0].AbsoluteRange),Is.False);
            Assert.That(CombatTargetingService.IsPositionWithinRange(unit.Position,hero.Position,skill.TargetModes[0].AbsoluteRange),Is.True);
            Assert.That(JsonUtility.ToJson(skill),Is.EqualTo(before));Assert.That(basic.ActionCost,Is.EqualTo(skill.ActionCost));Assert.That(basic.ResourceCost,Is.EqualTo(skill.ResourceCost));
            Assert.That(JsonUtility.ToJson(npc.Stats),Is.EqualTo(stats));Assert.That(npc.CurrentHealth,Is.EqualTo(hp));Assert.That(npc.ResourcePool.CurrentValue,Is.EqualTo(resource));Assert.That(UnityEngine.Random.state,Is.EqualTo(state));
            Assert.That(engine.Result.phaseChanges,Is.Zero);
        }
    }
}
