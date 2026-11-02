using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatBalanceOctober24Tests
    {
        static CombatSession Session(BattleUnit caster,BattleUnit target)
        {
            caster.Position=new GridPosition(1,1);target.Position=new GridPosition(2,1);
            return new CombatSession{Grid=new BattleGrid(8,8),Units=new(){caster,target},ActiveUnit=caster};
        }
        [Test]
        public void CrushingBlowForecastIncludesPriorDebuffAndRefreshingDoesNotDoubleItsStrength()
        {
            var caster=Unit();var target=Unit(false);var skill=Skill("PaladinCrushingBlow");
            Session(caster,target);target.Character.BaseResistances=new ResistanceSet{SuppressionResistance=.7f};
            var before=StatusEffectSystem.CalculateStatusApplicationChance(caster,target,StatusEffectIds.Stun,.5f);
            var after=StatusEffectSystem.CalculateStatusApplicationChance(caster,target,StatusEffectIds.Stun,.5f,-.3f);
            var hit=CombatDamageSystem.CalculateSkillHitChance(caster,target,skill);
            var random=UnityEngine.Random.state;var count=target.Character.PassiveEffects.Count;
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,target,skill,0);
            Assert.That(preview.StatusChance,Is.EqualTo(hit*(before*.1f+after*.9f)).Within(.0001f));
            Assert.That(after,Is.GreaterThan(before));Assert.That(target.Character.PassiveEffects.Count,Is.EqualTo(count));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            var modifiers=new ModifyResistanceSystem();modifiers.Apply(caster,target,skill,skill.Effects[1],1);
            modifiers.Apply(caster,target,skill,skill.Effects[1],1);
            var key=Godmist.Data.Skills.SkillEffectIdentity.RuntimeId(skill,1,"modify_resistance");
            Assert.That(target.Character.PassiveEffects.Count(e=>e.EffectId==key),Is.EqualTo(1));
            var refreshed=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,target,skill,0);
            Assert.That(refreshed.StatusChance,Is.EqualTo(hit*StatusEffectSystem.CalculateStatusApplicationChance(caster,target,StatusEffectIds.Stun,.5f)).Within(.0001f));
        }
        [Test]
        public void CrushingBlowAppliesSuppressionBeforeRollingStun()
        {
            var skill=Object.Instantiate(Skill("PaladinCrushingBlow"));var random=UnityEngine.Random.state;
            try
            {
                var data=new SerializedObject(skill);var effects=data.FindProperty("effects");
                effects.GetArrayElementAtIndex(0).FindPropertyRelative("alwaysHits").boolValue=true;
                effects.GetArrayElementAtIndex(1).FindPropertyRelative("baseChance").floatValue=1;
                effects.GetArrayElementAtIndex(2).FindPropertyRelative("baseChance").floatValue=1;data.ApplyModifiedPropertiesWithoutUndo();
                for(var seed=0;seed<20;seed++)
                {
                    var caster=Unit();var target=Unit(false);target.Character.CurrentHealth=5000;
                    target.Character.BaseResistances=new ResistanceSet{SuppressionResistance=.7f};
                    UnityEngine.Random.InitState(seed);
                    Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(Session(caster,target),caster,skill,target),Is.True);
                    Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun),Is.True,"seed "+seed);
                }
            }
            finally{UnityEngine.Random.state=random;Object.DestroyImmediate(skill);}
        }
        [Test]
        public void HolyTransfusionRestoresTwiceActualHealthDamage()
        {
            var caster=Unit();caster.Character.Stats.CritChance=new Stat(0);var target=Unit(false);
            target.Character.CurrentHealth=5000;target.Character.Stats.MagicalDefense=new Stat(0);
            var previous=target.Character.CurrentHealth;var health=caster.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem().TryExecute(Session(caster,target),caster,Skill("PaladinTransfusion"),target),Is.True);
            var dealt=previous-target.Character.CurrentHealth;Assert.That(dealt,Is.GreaterThan(0));
            Assert.That(caster.Character.CurrentHealth-health,Is.EqualTo(2*dealt));
        }
        [Test]
        public void HealingVariesWithinForecastAndRetainsItsMean()
        {
            var caster=Unit();caster.Character.SkillTreeProgress.Clear();var target=Unit();var skill=Skill("PaladinHeal");
            var session=Session(caster,target);var preview=CombatSkillPreviewCalculator.CalculateHealingPreview(session,caster,target,skill,0);
            var random=UnityEngine.Random.state;var minimum=int.MaxValue;var maximum=0;var sum=0;
            try
            {
                UnityEngine.Random.InitState(1465);
                for(var n=0;n<256;n++)
                {
                    target.Character.CurrentHealth=100;
                    PaladinCombatSystem.Execute(session,caster,target,skill,skill.Effects[0],new CombatRecoverySystem());
                    var healed=target.Character.CurrentHealth-100;minimum=Math.Min(minimum,healed);maximum=Math.Max(maximum,healed);sum+=healed;
                    Assert.That(healed,Is.InRange(preview.Minimum,preview.Maximum));
                }
                Assert.That(minimum,Is.LessThanOrEqualTo(102));Assert.That(maximum,Is.GreaterThanOrEqualTo(138));
                Assert.That(sum/256f,Is.EqualTo(120).Within(3));
            }
            finally{UnityEngine.Random.state=random;}
        }
        [Test]
        public void TiringSpellsHasPlayerFacingNameAndActualSkillIconIncludingLegacySource()
        {
            var caster=Unit();var target=Unit(false);var skill=Skill("MageTiringSpells");
            ScoutCombatSystem.TimedStat(caster,target,CharacterStatType.Speed,-3,3,"mage.tiring","Wycieńczające Zaklęcia","skill.mage.tiring");
            var effect=target.Character.PassiveEffects.Single(e=>e.EffectId=="mage.tiring");
            Assert.That(effect.DisplayName,Is.EqualTo("Wycieńczające Zaklęcia"));
            var presentation=new CombatStatusPresentation(effect,CombatStatusKind.Debuff,1);
            Assert.That(skill.Icon,Is.Not.Null);
            Assert.That(CombatStatusPresentationFactory.ResolveIcon(presentation,_=>null,new[]{skill}),Is.SameAs(skill.Icon));
            effect.Sources.Clear();
            Assert.That(CombatStatusPresentationFactory.ResolveIcon(presentation,_=>null,new[]{skill}),Is.SameAs(skill.Icon));
        }
        [TestCase(19,false,false)] [TestCase(20,false,true)] [TestCase(24,false,true)] [TestCase(25,true,true)]
        public void HookChecksEnemyAndKnifeMomentumCostsIndependently(int momentum,bool enemy,bool knife)
        {
            var caster=Unit();caster.Character.SkillTreeProgress.Clear();
            caster.Character.ResourcePool=new ResourcePool(ResourceType.Momentum,momentum,200,0);
            var hook=Skill("ScoutHook");
            Assert.That(ScoutCombatSystem.CanPayHookCost(caster,hook),Is.EqualTo(enemy));
            Assert.That(ScoutCombatSystem.CanPayHookCost(caster,hook,true),Is.EqualTo(knife));
        }
        [Test]
        public void HookSelectablePositionsAndTargetsExcludeUnaffordableEnemiesButKeepKnives()
        {
            var node=new GameObject("Hook query owner");node.SetActive(false);
            try
            {
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var host=node.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();
                var driver=(Godmist.Views.Combat.CombatExecutionDriver)typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetMethod("CreateDriver",flags).Invoke(host,null);
                var caster=Unit();caster.Character.SkillTreeProgress.Clear();var target=Unit(false);var session=Session(caster,target);
                caster.Character.ResourcePool=new ResourcePool(ResourceType.Momentum,20,200,0);caster.MaxKnives=5;caster.AvailableKnives=0;
                var knife=new KnifeCombatMapObject("knife",new GridPosition(3,1),caster.Character.CharacterId,1);session.MapObjects.Add(knife);
                typeof(Godmist.Views.Combat.CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
                typeof(Godmist.Views.Combat.CombatExecutionDriver).GetField("targetingService",flags).SetValue(driver,new CombatTargetingService());
                typeof(Godmist.Views.Combat.CombatExecutionDriver).GetField("lineOfSightService",flags).SetValue(driver,new CombatLineOfSightService());
                var hook=Skill("ScoutHook");var mode=hook.TargetModes[0];
                Assert.That(driver.GetSelectableSkillPositions(hook,mode,0),Is.EquivalentTo(new[]{knife.Position}));
                Assert.That(driver.GetEligibleSkillTargetPositions(hook,mode,0),Is.EquivalentTo(new[]{knife.Position}));
                Assert.That(driver.GetSkillTargets(hook,mode,0,target.Position),Is.Empty);
                caster.Character.ResourcePool.CurrentValue=25;
                Assert.That(driver.GetEligibleSkillTargetPositions(hook,mode,0),Does.Contain(target.Position));
                Assert.That(driver.GetSkillTargets(hook,mode,0,target.Position),Does.Contain(target));
            }
            finally{Object.DestroyImmediate(node);}
        }
    }
}
