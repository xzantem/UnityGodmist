using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.Characters;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Characters;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class RepairBatchPassiveContractTests
    {
        [Serializable] private class AuditRow { public string asset, id, definition; public string[] errors; }
        [Serializable] private class Audit { public List<AuditRow> rows = new(); }

        private static IEnumerable<(string Path, PassiveEffectDefinition Definition)> AuthoredPassives()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SkillTreeDefinition", new[] { "Assets/_Project" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path);
                foreach (var definition in (tree.GlobalPassive?.PassivesPerPoint ?? new()).Concat(tree.Nodes.SelectMany(node => node.PassiveEffects)))
                    if (definition != null) yield return (path, definition);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniqueEquipmentDefinition", new[] { "Assets/_Project" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var unique = AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>(path);
                var seen = new HashSet<PassiveEffectDefinition>();
                for (var level = 1; level <= 50; level++)
                foreach (var definition in unique.GetEffects(level) ?? new())
                    if (definition != null && seen.Add(definition)) yield return (path, definition);
            }
        }

        [Test]
        public void RecordAuthoredPassiveContractsBeforeTighteningValidation()
        {
            var audit = new Audit();
            foreach (var (path, definition) in AuthoredPassives()) audit.rows.Add(new AuditRow {
                asset = path, id = definition.Id, definition = JsonUtility.ToJson(definition),
                errors = PassiveEffectDefinitionValidator.Validate(definition).Errors.ToArray() });
            Assert.That(audit.rows, Is.Not.Empty);
            Assert.That(audit.rows.SelectMany(row => row.errors), Is.Empty);
            var folder = Path.GetFullPath("../LocalHandoffs/repair-batch-12-25-20261030/E01"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "passive-contract-inventory.json"), JsonUtility.ToJson(audit, true));
            TestContext.WriteLine($"Scanned {audit.rows.Count} authored passive definitions; {audit.rows.Count(row => row.errors.Length > 0)} currently invalid.");
        }

        [TestCase(2, 4)][TestCase(4, 2)][TestCase(5, 5)]
        public void LowHealthShieldUsesIndependentAuthoredDurationAndCooldown(int duration, int cooldown)
        {
            var authored = AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>(
                "Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BanditLeader_PaladinArmor.asset");
            var clone = UnityEngine.Object.Instantiate(authored);
            try
            {
                var operation = clone.GetEffects(15).SelectMany(effect => effect.Operations).Single(op => op.Type == PassiveEffectOperationType.LowHealthShield);
                Assert.That(operation.DurationTurns, Is.EqualTo(5)); Assert.That(operation.CooldownTurns, Is.EqualTo(5));
                typeof(PassiveEffectOperationDefinition).GetProperty(nameof(operation.DurationTurns)).SetValue(operation, duration);
                typeof(PassiveEffectOperationDefinition).GetProperty(nameof(operation.CooldownTurns)).SetValue(operation, cooldown);
                var unit = new BattleUnit { Character = new CharacterInstance { LifeState = CharacterLifeState.Alive,
                    CurrentHealth = 300, Stats = new CharacterStats { MaxHealth = new Stat(1000) } } };
                unit.Character.Equipment.Armor = new EquipmentItemInstance(clone) { ItemLevel = 15 };
                UniqueEquipmentCombatRules.TryPoacherShield(unit); Assert.That(unit.PoacherArmorCooldown, Is.Zero, "Threshold is strictly below 30%.");
                unit.Character.CurrentHealth = 299;
                UniqueEquipmentCombatRules.TryPoacherShield(unit);
                Assert.That(unit.PoacherArmorCooldown, Is.EqualTo(cooldown)); Assert.That(unit.PoacherArmorShieldTurns, Is.EqualTo(duration));
                Assert.That(unit.Character.ShieldPool.TotalShield, Is.EqualTo(Mathf.RoundToInt(1000 * operation.Value.NumberValue)));
                for (var turn = 1; turn < cooldown; turn++)
                {
                    UniqueEquipmentCombatRules.BeginTurn(unit);
                    Assert.That(unit.PoacherArmorCooldown, Is.EqualTo(cooldown - turn));
                    if (turn >= duration) Assert.That(unit.Character.ShieldPool.TotalShield, Is.Zero);
                }
                UniqueEquipmentCombatRules.BeginTurn(unit);
                Assert.That(unit.PoacherArmorCooldown, Is.EqualTo(cooldown)); Assert.That(unit.PoacherArmorShieldTurns, Is.EqualTo(duration));
                Assert.That(authored.GetEffects(15).SelectMany(effect => effect.Operations).Single(op => op.Type == PassiveEffectOperationType.LowHealthShield).DurationTurns, Is.EqualTo(5));
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [TestCase(.7f, "85%", "100%")][TestCase(.8f, "95%", "110%")]
        public void EnergySplitTreeTooltipReadsTheAuthoredCoefficient(float coefficient, string rankOne, string rankTwo)
        {
            var sourceClass = AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/_Project/ScriptableObjects/Classes/CCD_Mage.asset");
            var classClone = UnityEngine.Object.Instantiate(sourceClass);
            var sourceTree = sourceClass.SkillTrees.Single(tree => tree.Id == MageCombatSystem.Warlock);
            var treeClone = UnityEngine.Object.Instantiate(sourceTree);
            var skill = treeClone.Nodes.Select(node => node.GrantedSkill).First(value => value?.SkillId == "skill.mage.energy_split");
            var skillClone = UnityEngine.Object.Instantiate(skill);
            try
            {
                foreach (var effect in skillClone.Effects.Where(effect => effect.EffectType == EffectType.DealDamage))
                    typeof(Godmist.Data.Effects.SkillEffectData).GetField("damageMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(effect, coefficient);
                foreach (var node in treeClone.Nodes.Where(node => node.GrantedSkill == skill))
                    typeof(SkillTreeNodeDefinition).GetProperty(nameof(node.GrantedSkill)).SetValue(node, skillClone);
                typeof(CharacterClassData).GetProperty(nameof(classClone.SkillTrees)).SetValue(classClone, sourceClass.SkillTrees.Select(tree => tree == sourceTree ? treeClone : tree).ToList());
                var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset"), classClone);
                var talent = treeClone.Nodes.Single(node => node.Id == "node.mage.warlock.power_saving");
                var text = SkillTreeNodeView.BuildDetails(talent, 1, null, character);
                Assert.That(text, Does.Contain(rankOne).And.Contain(rankTwo));
                Assert.That(skill.Effects.First(effect => effect.EffectType == EffectType.DealDamage).DamageMultiplier, Is.EqualTo(.7f));
            }
            finally { UnityEngine.Object.DestroyImmediate(skillClone); UnityEngine.Object.DestroyImmediate(treeClone); UnityEngine.Object.DestroyImmediate(classClone); }
        }
    }
}
