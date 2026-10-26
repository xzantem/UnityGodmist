using System;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatBalanceOctober24Tests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static SkillData Skill(string name)=>AssetDatabase.LoadAssetAtPath<SkillData>(Root+"Skills/SD_"+name+".asset");
        static BattleUnit Unit(bool player=true)
        {
            var c=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"Characters/CD_Paladin.asset"));
            c.CharacterId=Guid.NewGuid().ToString();c.Stats.MaxHealth=new Stat(10000);c.CurrentHealth=100;
            c.Stats.MinAttack=new Stat(100);c.Stats.MaxAttack=new Stat(100);c.Stats.CritDamage=new Stat(2);
            c.ResourcePool.MaxValue=1000;c.ResourcePool.CurrentValue=1000;
            return new BattleUnit{Character=c,IsPlayerSide=player,CurrentActionPoints=10000,MaxActionPoints=10000};
        }
        [Test,Explicit("User-authorized skill, enemy and stair prefab authoring via Editor API.")]
        public void ApplyContent()
        {
            CombatBalanceOctober24Setup.Apply();
            Assert.That(Skill("PaladinCrushingBlow").Effects.Select(e=>e.EffectType),
                Is.EqualTo(new[]{EffectType.DealDamage,EffectType.ModifyResistance,EffectType.ApplyStatus}));
            Assert.That(Skill("PaladinTransfusion").Effects[0].LifeStealPercent,Is.EqualTo(2));
        }
        [Test]
        public void HealingForecastIncludesSpreadAndCriticalWithoutRandomOrMutation()
        {
            var caster=Unit();var target=Unit();
            caster.Character.Stats.CritChance=new Stat(.5f);
            caster.Character.SkillTreeProgress.Clear();
            caster.Character.SkillTreeProgress.Add(new Godmist.Runtime.Characters.SkillTreeProgress{TreeId=PaladinCombatSystem.Priest,
                NodeInvestments=new(){new(){NodeId="node.paladin.priest.unexpected_grace",Ranks=2}}});
            var session=new CombatSession{Units=new(){caster,target},ActiveUnit=caster};
            var random=UnityEngine.Random.state;var health=target.Character.CurrentHealth;
            var preview=CombatSkillPreviewCalculator.CalculateHealingPreview(session,caster,target,Skill("PaladinHeal"),0);
            // Two invested priest points also grant +2% healing power.
            Assert.That(preview.Minimum,Is.EqualTo(102));Assert.That(preview.Maximum,Is.EqualTo(143));
            Assert.That(preview.CriticalMinimum,Is.EqualTo(204));Assert.That(preview.CriticalMaximum,Is.EqualTo(286));
            Assert.That(preview.CriticalChance,Is.EqualTo(.5f));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(target.Character.CurrentHealth,Is.EqualTo(health));
        }
        [TestCase(1)] [TestCase(10)] [TestCase(25)] [TestCase(48)] [TestCase(50)]
        public void EnemyAttackDropsFifteenPercentWithoutChangingHealthAndDoesNotCompound(int level)
        {
            var definitions=AssetDatabase.FindAssets("t:CharacterDefinition",new[]{"Assets/_Project/ScriptableObjects"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d=>d!=null&&d.Id.StartsWith("enemy.")&&!d.Id.Contains(".test")&&d.name!="CD_Enemy_Test").ToArray();
            Assert.That(definitions.Length,Is.GreaterThan(20));
            var rows=new System.Collections.Generic.List<string>{"id,hp,attackBeforeMin,attackBeforeMax,attackAfterMin,attackAfterMax,physicalDefense,magicalDefense,dodge,critChance,speed"};
            foreach(var definition in definitions)
            {
                var baseline=Object.Instantiate(definition);
                try
                {
                    var data=new SerializedObject(baseline);data.FindProperty("<AttackMultiplier>k__BackingField").floatValue=1;data.ApplyModifiedPropertiesWithoutUndo();
                    var factory=new CharacterInstanceFactory();var before=factory.Create(baseline,baseline.ClassData,level);
                    var after=factory.Create(definition,definition.ClassData,level);
                    Assert.That(after.Stats.MaxHealth.GetValue(),Is.EqualTo(before.Stats.MaxHealth.GetValue()),definition.Id);
                    Assert.That(after.Stats.MinAttack.GetValue(),Is.EqualTo(before.Stats.MinAttack.GetValue()*.85f).Within(.01f),definition.Id);
                    Assert.That(after.Stats.MaxAttack.GetValue(),Is.EqualTo(before.Stats.MaxAttack.GetValue()*.85f).Within(.01f),definition.Id);
                    rows.Add(string.Join(",",new object[]{definition.Id,after.Stats.MaxHealth.GetValue(),before.Stats.MinAttack.GetValue(),before.Stats.MaxAttack.GetValue(),
                        after.Stats.MinAttack.GetValue(),after.Stats.MaxAttack.GetValue(),after.Stats.PhysicalDefense.GetValue(),after.Stats.MagicalDefense.GetValue(),
                        after.Stats.Dodge.GetValue(),after.Stats.CritChance.GetValue(),after.Stats.Speed.GetValue()}.Select(v=>Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture))));
                    var value=after.Stats.MaxAttack.GetValue();new CharacterStatPipelineSystem().Refresh(after);
                    Assert.That(after.Stats.MaxAttack.GetValue(),Is.EqualTo(value),definition.Id);
                }
                finally{Object.DestroyImmediate(baseline);}
            }
            var output=System.IO.Path.GetFullPath("../LocalHandoffs/combat-balance-20261024");System.IO.Directory.CreateDirectory(output);
            System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"enemy-stats-level-"+level+".csv"),rows);
        }
        [Test]
        public void UpwardTransitionUsesItsDedicatedSpriteAndDownwardRetainsOriginal()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab");
            var view=Object.Instantiate(prefab);
            try
            {
                var marker=(SpriteRenderer)typeof(DungeonExitView).GetField("transitionRenderer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                var original=marker.sprite;view.ConfigureFloorDirection(true);
                Assert.That(marker.sprite,Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(CombatBalanceOctober24Setup.UpPath)));
                Assert.That(marker.sprite,Is.Not.SameAs(original));view.ConfigureFloorDirection(false);Assert.That(marker.sprite,Is.SameAs(original));
            }
            finally{Object.DestroyImmediate(view.gameObject);}
        }
    }
}
