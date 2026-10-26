using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class AlchemyBombBalanceTests
    {
        readonly List<Object> temporary = new();
        Random.State randomState;
        [SetUp] public void SetUp() => randomState = Random.state;
        [TearDown] public void Cleanup()
        {
            Random.state = randomState;
            foreach (var value in temporary) if (value != null) Object.DestroyImmediate(value);
            temporary.Clear();
        }
        ConsumableItemInstance Bomb(params ConsumableRuntimeEffectType[] types)
        {
            var definition=ScriptableObject.CreateInstance<ConsumableItemDefinition>();temporary.Add(definition);
            typeof(ItemDefinition).GetField("<Id>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(definition,"alchemy.balance.test");
            typeof(ConsumableItemDefinition).GetField("<UseType>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(definition,ConsumableUseType.Bomb);
            var result=new ConsumableItemInstance(definition);
            foreach(var type in types) result.RuntimeEffects.Add(new ConsumableEffectRuntime {
                EffectType=type,Value=type is ConsumableRuntimeEffectType.ApplyBleed or ConsumableRuntimeEffectType.ApplyPoison
                    or ConsumableRuntimeEffectType.ApplyBurn ? .1f : .15f,
                Chance=1f,TargetType=TargetType.AreaAroundTile,TargetingType=TargetingType.Enemies });
            return result;
        }
        SkillData Action(ConsumableItemInstance item)
        { var result=ConsumableRuntimeActionBuilder.Build(item);temporary.Add(result);return result; }
        static BattleUnit Unit(int level=48, float attack=1f, float accuracy=100f, bool player=true) => new() {
            IsPlayerSide=player,Position=new GridPosition(player?0:2,0),
            Character=new CharacterInstance { CharacterId=player?"alchemy.caster":"alchemy.target",Level=level,
                CurrentHealth=1000000,LifeState=CharacterLifeState.Alive,WeaponAccuracy=accuracy,
                Stats=new CharacterStats { MinAttack=new Stat(attack),MaxAttack=new Stat(attack),
                    MaxHealth=new Stat(1000000),Dodge=new Stat(40),CritChance=new Stat(0),CritDamage=new Stat(1.5f) } } };

        [TestCase(1)][TestCase(10)][TestCase(30)][TestCase(48)][TestCase(50)]
        public void TwoDamageIngredientsShareBudgetAcrossTypes(int level)
        {
            var pure=Action(Bomb(ConsumableRuntimeEffectType.DealPhysicalDamage,ConsumableRuntimeEffectType.DealPhysicalDamage,
                ConsumableRuntimeEffectType.ApplyStun));
            var mixed=Action(Bomb(ConsumableRuntimeEffectType.DealPhysicalDamage,ConsumableRuntimeEffectType.DealMagicalDamage,
                ConsumableRuntimeEffectType.ApplyStun));
            var a=pure.Effects.Where(e=>e.EffectType==EffectType.DealDamage).ToArray();
            var b=mixed.Effects.Where(e=>e.EffectType==EffectType.DealDamage).ToArray();
            Assert.That(a.Sum(e=>e.DamageMultiplier),Is.EqualTo(.225f).Within(.00001f));
            Assert.That(b.Sum(e=>e.DamageMultiplier),Is.EqualTo(a.Sum(e=>e.DamageMultiplier)).Within(.00001f));
            var target=Unit(player:false);var defense=CombatDamageValueCalculator.GetAlchemicalReferenceAttack(level);
            target.Character.Stats.PhysicalDefense=new Stat(defense);target.Character.Stats.MagicalDefense=new Stat(defense);
            int Damage(IEnumerable<Godmist.Data.Effects.SkillEffectData> effects)=>effects.Sum(e=>CombatDamageSystem.ApplyDefense(
                Unit(level),target,CombatDamageValueCalculator.CalculateDamageValue(Unit(level),e.DamageValueSource,e.DamageMultiplier),e.DamageType));
            Assert.That(Damage(b),Is.EqualTo(Damage(a)).Within(3),"At reference armor, splitting damage must not create another damage budget.");
        }
        [Test] public void ThreeIngredientMixedDamageKeepsThirdIngredientDiminishingReturns()
        {
            var skill=Action(Bomb(ConsumableRuntimeEffectType.DealPhysicalDamage,ConsumableRuntimeEffectType.DealPhysicalDamage,
                ConsumableRuntimeEffectType.DealMagicalDamage));
            Assert.That(skill.Effects.Sum(e=>e.DamageMultiplier),Is.EqualTo(.27f).Within(.00001f));
        }

        [TestCase(ConsumableRuntimeEffectType.ApplyBleed,1)]
        [TestCase(ConsumableRuntimeEffectType.ApplyPoison,20)]
        [TestCase(ConsumableRuntimeEffectType.ApplyBurn,30)]
        [TestCase(ConsumableRuntimeEffectType.ApplyBleed,48)]
        [TestCase(ConsumableRuntimeEffectType.ApplyBurn,50)]
        public void DotApplicationTicksAndTooltipUseLevelPowerRegardlessOfWeapon(ConsumableRuntimeEffectType type,int level)
        {
            var item=Bomb(type,ConsumableRuntimeEffectType.ApplyStun,ConsumableRuntimeEffectType.Slow);
            var skill=Action(item);var effect=skill.Effects.First(e=>e.StatusDamageMultiplier>0);
            var source=Unit(level);var target=Unit(level,player:false);
            var expected=CombatDamageValueCalculator.CalculateAlchemicalPeriodicDamage(level,.1f);
            Assert.That(effect.DamageValueSource,Is.EqualTo(DamageValueSource.AlchemicalPower));
            Assert.That(StatusEffectSystem.CalculateStatusDamagePerTurn(source,effect),Is.EqualTo(expected));
            source.Character.Stats.MinAttack=new Stat(10000);source.Character.Stats.MaxAttack=new Stat(20000);
            Assert.That(StatusEffectSystem.CalculateStatusDamagePerTurn(source,effect),Is.EqualTo(expected));
            var summary=UIWorkspaceTooltipView.BuildBombEffectSummary(item,source.Character);
            Assert.That(summary,Does.Contain(expected+" obrażeń / turę"));
            Assert.That(UIWorkspaceTooltipView.BuildBombEffectSummary(item),Does.Contain("mocy alchemicznej"));
            var status=new StatusEffectSystem();
            Assert.That(status.ApplyStatus(source,target,skill,effect,0),Is.True);
            var runtime=target.Character.PassiveEffects.Single(e=>e.EffectId==effect.StatusId);
            Assert.That(runtime.TotalDamage,Is.EqualTo(expected*effect.DurationTurns));
            var session=new CombatSession { ActiveUnit=source };session.Units.Add(source);session.Units.Add(target);
            var start=target.Character.CurrentHealth;
            // The existing resistance curve doubles DoT at zero resistance.
            // Compare actual ticks with resisted damage, not the raw snapshot.
            var effectiveTick=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(target,effect.StatusId,expected);
            for(int i=0;i<effect.DurationTurns;i++) status.ProcessTurnStart(session,target);
            Assert.That(start-target.Character.CurrentHealth,Is.EqualTo(effectiveTick*effect.DurationTurns));
            Assert.That(target.Character.PassiveEffects,Is.Empty);
        }
        [Test] public void OrdinarySkillDotStillUsesAttackAndExistingStatusSnapshotDoesNotChange()
        {
            var skill=Action(Bomb(ConsumableRuntimeEffectType.ApplyPoison));var effect=skill.Effects[0];
            typeof(Godmist.Data.Effects.SkillEffectData).GetField("damageValueSource",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(effect,DamageValueSource.AttackRange);
            var source=Unit(50,100);var target=Unit(player:false);var status=new StatusEffectSystem();
            Assert.That(StatusEffectSystem.CalculateStatusDamagePerTurn(source,effect),Is.EqualTo(10));
            Assert.That(status.ApplyStatus(source,target,skill,effect,0),Is.True);
            source.Character.Stats.MinAttack=new Stat(1000);source.Character.Stats.MaxAttack=new Stat(1000);
            Assert.That(target.Character.PassiveEffects.Single().TotalDamage,Is.EqualTo(10*effect.DurationTurns));
            Assert.That(StatusEffectSystem.CalculateStatusDamagePerTurn(source,effect),Is.EqualTo(100));
        }
        [Test] public void BombHitRollAndPreviewIgnoreWeaponAccuracyWhileNormalAttackStillUsesIt()
        {
            var bomb=Action(Bomb(ConsumableRuntimeEffectType.DealPhysicalDamage));
            var normal=ScriptableObject.CreateInstance<SkillData>();temporary.Add(normal);
            var source=Unit(48,accuracy:1);var target=Unit(player:false);
            var chance=CombatDamageSystem.CalculateHitChance(100,100,40);
            Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,bomb).HitChance,Is.EqualTo(chance).Within(.00001));
            var normalLow=CombatDamageSystem.CalculateSkillHitChance(source,target,normal);
            var session=new CombatSession();session.Units.Add(source);session.Units.Add(target);
            var context=new PassiveEffectCombatContext(session:session,source:source,target:target,skill:bomb);
            var damage=new CombatDamageSystem();
            var rng=Random.state;
            var low=Enumerable.Range(0,128).Select(_=>damage.ResolveAndApplyDamage(session,source,target,10,DamageType.Physical,100,passiveContext:context).Hit).ToArray();
            source.Character.WeaponAccuracy=200;Random.state=rng;
            var high=Enumerable.Range(0,128).Select(_=>damage.ResolveAndApplyDamage(session,source,target,10,DamageType.Physical,100,passiveContext:context).Hit).ToArray();
            Assert.That(high,Is.EqualTo(low));Assert.That(low.Any(x=>x),Is.True);Assert.That(low.Any(x=>!x),Is.True);
            Assert.That(CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,bomb).HitChance,Is.EqualTo(chance).Within(.00001));
            Assert.That(CombatDamageSystem.CalculateSkillHitChance(source,target,normal),Is.GreaterThan(normalLow));
        }
        [Test] public void LateCurveIsContinuousAndKeepsGrowingThroughEveryLevel()
        {
            Assert.That(CombatDamageValueCalculator.GetAlchemicalReferenceAttack(1),Is.EqualTo(28));
            Assert.That(CombatDamageValueCalculator.GetAlchemicalReferenceAttack(30),Is.EqualTo(344.5625f));
            Assert.That(CombatDamageValueCalculator.GetAlchemicalReferenceAttack(50),Is.EqualTo(1200));
            Assert.That(CombatDamageValueCalculator.GetAlchemicalReferenceAttack(100),Is.EqualTo(1200));
            var previous=CombatDamageValueCalculator.GetAlchemicalReferenceAttack(30);
            for(int level=31;level<=50;level++) {
                var current=CombatDamageValueCalculator.GetAlchemicalReferenceAttack(level);
                Assert.That(current,Is.GreaterThan(previous));previous=current;
            }
            Assert.That(CombatDamageValueCalculator.CalculateAlchemicalDamage(50,.225f),
                Is.GreaterThan(CombatDamageValueCalculator.CalculateAlchemicalDamage(45,.225f)*1.3f));
        }
    }
}
