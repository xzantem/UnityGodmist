using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DotBudgetAuditTests
    {
        public static readonly (string Id, string Path, string Character)[] Cases = {
            ("skill.mage.fire_arrow", "Assets/_Project/ScriptableObjects/Skills/SD_MageFireArrow.asset", "Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset"),
            ("skill.mage.fireball", "Assets/_Project/ScriptableObjects/Skills/SD_MageFireball.asset", "Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset"),
            ("skill.scout.poisoned_dagger", "Assets/_Project/ScriptableObjects/Skills/SD_ScoutPoisonedDagger.asset", "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"),
            ("skill.warrior.bleeding_destruction", "Assets/_Project/ScriptableObjects/Skills/SD_WarriorBleedingDestruction.asset", "Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset"),
            ("skill.enemy.forest.shooter_piercing_bolt", "Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Shooter_piercing_bolt.asset", "Assets/_Project/ScriptableObjects/Dungeons/Forest/CD_Forest_BanditShooter.asset"),
            ("skill.enemy.catacombs.burning_arrow", "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_burning_arrow.asset", "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_archer.asset"),
            ("skill.enemy.catacombs.cross_cut", "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_cross_cut.asset", "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset"),
            ("skill.enemy.catacombs.vomit", "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_vomit.asset", "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_undead.asset"),
        };
        [Test, Explicit("Read-only paired baseline: real skill execution, resource/AP payment, proc rolls and all DoT ticks.")]
        public void Before() => Report("before");
        [Test, Explicit("Read-only paired after measurement on the same controlled actors and seeds.")]
        public void After() => Report("after");
        static string N(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
        internal static BattleUnit Unit(string path, bool player, float resistance, bool target)
        {
            var c = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path));
            c.CharacterId = Guid.NewGuid().ToString(); c.SkillTreeProgress.Clear(); c.PassiveEffects.Clear();
            c.Stats.MinAttack = new Stat(100); c.Stats.MaxAttack = new Stat(100); c.Stats.CritChance = new Stat(0);
            c.Stats.MaxHealth = new Stat(1000000); c.CurrentHealth = 1000000;
            c.Stats.PhysicalDefense = new Stat(target ? 100 : 0); c.Stats.MagicalDefense = new Stat(target ? 100 : 0);
            c.Stats.Dodge = new Stat(0); c.Stats.WeaponAccuracy = new Stat(100); c.WeaponAccuracy = 100;
            c.Stats.MaxResource = new Stat(1000); c.ResourcePool.MaxValue = 1000; c.ResourcePool.CurrentValue = 100;
            c.BaseResistances = new ResistanceSet { BleedResistance = resistance, PoisonResistance = resistance, FireResistance = resistance };
            c.Resistances = new ResistanceSet { BleedResistance = resistance, PoisonResistance = resistance, FireResistance = resistance };
            return new BattleUnit { Character = c, IsPlayerSide = player, Position = new GridPosition(2, target ? 3 : 2),
                CurrentActionPoints = 1000, MaxActionPoints = 1000, AvailableKnives = 5, MaxKnives = 5 };
        }
        static void Report(string phase)
        {
            var output = "../LocalHandoffs/dot-balance-20261024/" + phase; Directory.CreateDirectory(output);
            var rows = new List<string> { "skill,resistance,samples,apCost,resourceType,resourcePaid,resourceNetSpent,knivesPaid,initialDamageMean,dotDamageMean,totalDamageMean,anyDotChance,applicationsMean,totalPer100AP,totalPerResourcePaid" };
            var previous = UnityEngine.Random.state;
            try
            {
                foreach (var entry in Cases) foreach (var resistance in new[] { .25f, .5f, .75f })
                {
                    var skill = AssetDatabase.LoadAssetAtPath<SkillData>(entry.Path); Assert.That(skill, Is.Not.Null, entry.Path);
                    File.WriteAllText(output + "/" + skill.name + "-config.json", EditorJsonUtility.ToJson(skill, true));
                    const int samples = 64;
                    float initial = 0, dot = 0, resource = 0, net = 0, ap = 0, knives = 0, successes = 0, applications = 0;
                    for (var seed = 0; seed < samples; seed++)
                    {
                        var enemy = entry.Id.Contains(".enemy."); var source = Unit(entry.Character, !enemy, .5f, false);
                        var target = Unit("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset", enemy, resistance, true);
                        if (entry.Id == "skill.scout.poisoned_dagger") target.Position = new GridPosition(2, 4);
                        var session = new CombatSession { Grid = new BattleGrid(12, 12), Units = new() { source, target }, ActiveUnit = source };
                        var bus = new CombatFeedbackBus(); var applied = 0;
                        bus.Published += e => { if (e.Type == CombatFeedbackType.StatusApplied && e.Target == target) applied++; };
                        var status = new StatusEffectSystem(feedbackBus: bus);
                        var log = new List<string>();
                        var execution = new SkillExecutionSystem(battleLog: log.Add, statusEffectSystem: status, feedbackBus: bus);
                        UnityEngine.Random.InitState(241026 + seed);
                        Assert.That(execution.TryExecute(session, source, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { [0] = new[] { target } },
                            null, null, new Dictionary<int, GridPosition?> { [0] = target.Position }, rotationsByModeIndex: new Dictionary<int, int> { [0] = 0 }), Is.True, entry.Id + " " + string.Join(" / ", log) + " pool=" + source.Character.ResourcePool.ResourceType + ":" + source.Character.ResourcePool.CurrentValue + " knives=" + source.AvailableKnives);
                        initial += 1000000 - target.Character.CurrentHealth; var health = target.Character.CurrentHealth;
                        ap += 1000 - source.CurrentActionPoints; resource += source.LastSkillResourceSpent;
                        net += 100 - source.Character.ResourcePool.CurrentValue; knives += 5 - source.AvailableKnives;
                        applications += applied;
                        if (target.Character.PassiveEffects.Any(e => StatusEffectSystem.IsDamageOverTimeStatus(e.EffectId))) successes++;
                        for (var turn = 0; turn < 8; turn++) { status.ProcessTurnStart(session, target); status.ProcessTurnEnd(target); }
                        dot += health - target.Character.CurrentHealth;
                    }
                    var total = (initial + dot) / samples; var paid = resource / samples; var apPaid = ap / samples;
                    rows.Add($"{entry.Id},{N(resistance)},{samples},{N(apPaid)},{skill.ResourceType},{N(paid)},{N(net / samples)},{N(knives / samples)},{N(initial / samples)},{N(dot / samples)},{N(total)},{N(successes / samples)},{N(applications / samples)},{N(total * 100 / Math.Max(1, apPaid))},{N(total / Math.Max(1, paid))}");
                }
            }
            finally { UnityEngine.Random.state = previous; }
            File.WriteAllLines(output + "/budget.csv", rows);
        }
    }
}
