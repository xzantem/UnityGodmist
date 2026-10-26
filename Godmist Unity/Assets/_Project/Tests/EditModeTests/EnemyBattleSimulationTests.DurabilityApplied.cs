using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
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
        static string DurabilityFolder => Path.GetFullPath("../LocalHandoffs/enemy-role-durability-implementation-20261025");

        [TestCase(1), TestCase(11), TestCase(21), TestCase(31), TestCase(41)]
        [Explicit("Read-only post-authoring comparison at every level1–50: frozen original / previous roles / current. No battle simulation.")]
        [Timeout(600000)]
        public void ExportAppliedDurabilityRange(int firstLevel)
            => ExportAppliedDurabilityLevels(Enumerable.Range(firstLevel, 10).ToArray());

        [Test, Explicit("Refresh the production exports only where the HP15/25 correction changes interpolation.")]
        [Timeout(600000)]
        public void ExportAppliedDurabilityIntermediateCorrection()
            => ExportAppliedDurabilityLevels(Enumerable.Range(11, 19).ToArray());

        void ExportAppliedDurabilityLevels(int[] levels)
        {
            var current = PointCatalog.CharacterDefinitions.Where(d => d != null && File.Exists(Path.Combine(DurabilityFolder, "before-definitions", d.Id + ".json"))).OrderBy(d => d.Id).ToArray();
            Assert.That(current, Has.Length.EqualTo(23));
            CharacterDefinition[] Clones(string folder) => current.Select(d =>
            {
                var clone = ScriptableObject.CreateInstance<CharacterDefinition>(); temporary.Add(clone);
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(folder, d.Id + ".json")), clone);
                return clone;
            }).ToArray();
            var original = Clones(Path.Combine(RoleFolder, "before-definitions"));
            var previous = Clones(Path.Combine(DurabilityFolder, "before-definitions"));
            var immutable = current.ToDictionary(d => d, EditorJsonUtility.ToJson);
            var factory = new CharacterInstanceFactory();
            var growthDefinition = PointCatalog.CharacterDefinitions.Single(d => d != null && d.Id == "summon.forest.poisongrowth");
            foreach (var level in levels)
            {
                File.AppendAllText(Path.Combine(DurabilityFolder, "progress.log"), DateTime.UtcNow.ToString("O") + " START L" + level + Environment.NewLine);
                var report = new TheoryExport();
                foreach (var profile in new[] { 0, 1 })
                foreach (var heroIndex in Enumerable.Range(0, 4))
                foreach (var material in Enumerable.Range(0, 4))
                {
                    var hero = PointUnit(EnemyScalingBalanceTests.Hero(heroIndex, level, material: material, treeIndex: profile), true);
                    var heroBefore = JsonUtility.ToJson(TheorySnapshot(hero, "hero", profile, material));
                    report.stats.Add(TheorySnapshot(hero, "hero", profile, material));
                    foreach (var stage in new[] { "original", "previous", "current" })
                    foreach (var definition in stage == "original" ? original : stage == "previous" ? previous : current)
                    {
                        var enemy = PointUnit(factory.Create(definition, definition.ClassData, level), false);
                        if (profile == 0 && heroIndex == 0 && material == 0) report.stats.Add(TheorySnapshot(enemy, stage, -1, -1));
                        if (!SkillPointEnemyIds.Contains(definition.Id)) continue;
                        if (definition.Id == "enemy.forest.elf_druid")
                        {
                            var session = new CombatSession(); session.Units.Add(enemy); session.Units.Add(hero); enemy.WarriorState.Session = session;
                            session.Summons.Add(new SummonedBattleUnit(PointUnit(factory.Create(growthDefinition, growthDefinition.ClassData, level), false),
                                enemy.Character.CharacterId, "durability.growth", 0, -1, 0) { GroupId = "forest.growth" });
                        }
                        var random = UnityEngine.Random.state;
                        var fullSkills = material == 0 && new[] { 1, 5, 10, 11, 15, 20, 21, 25, 30, 31, 35, 40, 41, 45, 48, 49, 50 }.Contains(level);
                        foreach (var skill in hero.Character.GetKnownSkills().Where(s => (fullSkills || s == hero.Character.GetBasicAttackSkill())
                            && s.Effects.Any(e => e.EffectType == EffectType.DealDamage || e.StatusDamageMultiplier > 0)))
                        {
                            if (skill.SkillId == WarriorDischargeRules.SkillId)
                            {
                                foreach (var rage in new[] { 20, 50, Math.Min(100, hero.Character.ResourcePool.MaxValue) }.Where(r => r <= hero.Character.ResourcePool.MaxValue).Distinct())
                                {
                                    hero.Character.ResourcePool.CurrentValue = rage; new CharacterStatPipelineSystem().Refresh(hero.Character);
                                    report.skills.Add(TheoryCard(hero, enemy, skill, stage, profile, rage, material));
                                }
                                hero.Character.ResourcePool.CurrentValue = 0; new CharacterStatPipelineSystem().Refresh(hero.Character);
                            }
                            else report.skills.Add(TheoryCard(hero, enemy, skill, stage, profile, material: material));
                        }
                        foreach (var skill in enemy.Character.GetKnownSkills().Where(s => (fullSkills || s == enemy.Character.GetBasicAttackSkill())
                            && s.Effects.Any(e => e.EffectType == EffectType.DealDamage || e.StatusDamageMultiplier > 0)))
                            report.skills.Add(TheoryCard(enemy, hero, skill, stage, profile, material: material));
                        Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
                    }
                    Assert.That(JsonUtility.ToJson(TheorySnapshot(hero, "hero", profile, material)), Is.EqualTo(heroBefore));
                }
                foreach (var id in current.Select(d => d.Id))
                {
                    var a = report.stats.Single(r => r.stage == "previous" && r.id == id);
                    var b = report.stats.Single(r => r.stage == "current" && r.id == id);
                    Assert.That(b.min, Is.EqualTo(a.min), id + " minimum ATK L" + level);
                    Assert.That(b.max, Is.EqualTo(a.max), id + " maximum ATK L" + level);
                    Assert.That(b.physical, Is.EqualTo(a.physical), id + " PDEF L" + level);
                    Assert.That(b.dodge, Is.EqualTo(a.dodge)); Assert.That(b.crit, Is.EqualTo(a.crit)); Assert.That(b.critDamage, Is.EqualTo(a.critDamage));
                    Assert.That(b.speed, Is.EqualTo(a.speed)); Assert.That(b.accuracy, Is.EqualTo(a.accuracy)); Assert.That(b.maxAp, Is.EqualTo(a.maxAp));
                    Assert.That(b.maxResource, Is.EqualTo(a.maxResource)); Assert.That(b.regeneration, Is.EqualTo(a.regeneration));
                    if (!SkillPointEnemyIds.Contains(id)) { b.stage = a.stage; Assert.That(JsonUtility.ToJson(b), Is.EqualTo(JsonUtility.ToJson(a))); b.stage = "current"; }
                }
                File.WriteAllText(Path.Combine(DurabilityFolder, "applied-level-" + level + ".json"), JsonUtility.ToJson(report));
                File.AppendAllText(Path.Combine(DurabilityFolder, "progress.log"), DateTime.UtcNow.ToString("O") + " DONE L" + level + Environment.NewLine);
            }
            foreach (var pair in immutable) Assert.That(EditorJsonUtility.ToJson(pair.Key), Is.EqualTo(pair.Value));
        }
    }
}
