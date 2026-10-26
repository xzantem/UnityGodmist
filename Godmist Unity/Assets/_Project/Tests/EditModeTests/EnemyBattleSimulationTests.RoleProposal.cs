using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [Serializable] sealed class RoleProposalCard
        {
            public float magicFactor, attackFactor;
            public TheorySkill card;
        }
        [Serializable] sealed class RoleProposalGrid
        {
            public List<RoleProposalCard> rows = new();
        }

        [TestCase(1), TestCase(10), TestCase(20), TestCase(30), TestCase(40), TestCase(48), TestCase(50)]
        [Explicit("Read-only proposal grid on detached runtime stats. No asset authoring, casts, combat AI or RNG.")]
        [Timeout(180000)]
        public void ExportRoleProposalStatGrid(int level)
        {
            var folder = Path.GetFullPath("../LocalHandoffs/enemy-role-correction-proposal-20261025");
            Directory.CreateDirectory(folder);
            var definitions = PointCatalog.CharacterDefinitions.Where(d => d != null && SkillPointEnemyIds.Contains(d.Id)).ToArray();
            Assert.That(definitions, Has.Length.EqualTo(14));
            var originals = definitions.ToDictionary(d => d, EditorJsonUtility.ToJson);
            var growth = PointCatalog.CharacterDefinitions.Single(d => d != null && d.Id == "summon.forest.poisongrowth");
            var report = new RoleProposalGrid();
            var factory = new CharacterInstanceFactory();
            foreach (var profile in new[] { 0, 1 })
            foreach (var heroIndex in Enumerable.Range(0, 4))
            foreach (var material in Enumerable.Range(0, 4))
            {
                var hero = PointUnit(EnemyScalingBalanceTests.Hero(heroIndex, level, material: material, treeIndex: profile), true);
                var heroOriginal = JsonUtility.ToJson(TheorySnapshot(hero, "hero", profile, material));
                foreach (var definition in definitions)
                {
                    var enemy = PointUnit(factory.Create(definition, definition.ClassData, level), false);
                    var originalMagic = enemy.Character.Stats.MagicalDefense.GetValue();
                    var originalMin = enemy.Character.Stats.MinAttack.GetValue();
                    var originalMax = enemy.Character.Stats.MaxAttack.GetValue();
                    var random = UnityEngine.Random.state;
                    foreach (var factor in new[] { .5f, .75f, 1f })
                    {
                        enemy.Character.Stats.MagicalDefense.SetEffectiveValue(Mathf.Round(originalMagic * factor));
                        foreach (var skill in hero.Character.GetKnownSkills().Where(s =>
                            (material == 0 || s == hero.Character.GetBasicAttackSkill()) && s.Effects.Any(e => e.EffectType == Godmist.Core.Enums.EffectType.DealDamage)))
                        {
                            if (skill.SkillId == WarriorDischargeRules.SkillId)
                            {
                                foreach (var rage in new[] { 20, 50, Math.Min(100, hero.Character.ResourcePool.MaxValue) }.Where(r => r <= hero.Character.ResourcePool.MaxValue).Distinct())
                                {
                                    hero.Character.ResourcePool.CurrentValue = rage;
                                    new CharacterStatPipelineSystem().Refresh(hero.Character);
                                    report.rows.Add(new RoleProposalCard { magicFactor = factor, attackFactor = 1, card = TheoryCard(hero, enemy, skill, "proposal-grid", profile, rage, material) });
                                }
                                hero.Character.ResourcePool.CurrentValue = 0;
                                new CharacterStatPipelineSystem().Refresh(hero.Character);
                            }
                            else report.rows.Add(new RoleProposalCard { magicFactor = factor, attackFactor = 1, card = TheoryCard(hero, enemy, skill, "proposal-grid", profile, material: material) });
                        }
                    }
                    enemy.Character.Stats.MagicalDefense.SetEffectiveValue(originalMagic);
                    // Druid's own attack depends on owned growths. Keep one fixed,
                    // without executing any summon ability or including summon damage.
                    if (definition.Id == "enemy.forest.elf_druid")
                    {
                        var session = new CombatSession(); session.Units.Add(enemy); session.Units.Add(hero);
                        enemy.WarriorState.Session = session;
                        session.Summons.Add(new SummonedBattleUnit(PointUnit(factory.Create(growth, growth.ClassData, level), false),
                            enemy.Character.CharacterId, "proposal.growth", 0, -1, 0) { GroupId = "forest.growth" });
                    }
                    foreach (var factor in new[] { .9f, 1f })
                    {
                        enemy.Character.Stats.MinAttack.SetEffectiveValue(originalMin * factor);
                        enemy.Character.Stats.MaxAttack.SetEffectiveValue(originalMax * factor);
                        foreach (var skill in enemy.Character.GetKnownSkills().Where(s =>
                            (material == 0 || s == enemy.Character.GetBasicAttackSkill()) && s.Effects.Any(e => e.EffectType == Godmist.Core.Enums.EffectType.DealDamage)))
                            report.rows.Add(new RoleProposalCard { magicFactor = 1, attackFactor = factor, card = TheoryCard(enemy, hero, skill, "proposal-grid", profile, material: material) });
                    }
                    Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
                    Assert.That(JsonUtility.ToJson(TheorySnapshot(hero, "hero", profile, material)), Is.EqualTo(heroOriginal));
                }
            }
            foreach (var pair in originals) Assert.That(EditorJsonUtility.ToJson(pair.Key), Is.EqualTo(pair.Value), "Proposal audit must not author " + pair.Key.Id);
            Assert.That(report.rows, Is.Not.Empty);
            File.WriteAllText(Path.Combine(folder, "grid-level-" + level + ".json"), JsonUtility.ToJson(report, true));
        }
    }
}
