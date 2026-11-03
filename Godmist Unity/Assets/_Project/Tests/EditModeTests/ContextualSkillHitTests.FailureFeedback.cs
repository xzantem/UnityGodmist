using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed partial class ContextualSkillHitTests
    {
        [TestCase(0f)][TestCase(.1f)][TestCase(-.7f)][TestCase(.5f)]
        public void SameRollClassifiesDodgeWithoutChangingHitOrRandomSequence(float bonus)
        {
            var source=Unit(true);var target=Unit(false,2,1);
            source.Character.WeaponAccuracy=50;
            target.Character.Stats.Dodge=new Stat(75f*75f/65f-75f);
            source.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId="test.hit.bonus",
                VariableModifiers=new List<GameplayVariableModifier>{new() {VariableType=GameplayVariableType.HitChanceBonus,
                    Value=bonus,Stacking=StatModifierStacking.Additive}} });
            var expectedChance=CombatDamageSystem.ApplyHitChanceBonus(source.Character,
                CombatDamageSystem.CalculateHitChance(50,100,target.Character.Stats.Dodge.GetValue()));
            var withoutDodge=CombatDamageSystem.ApplyHitChanceBonus(source.Character,
                CombatDamageSystem.CalculateHitChance(50,100,0));
            if(bonus==0) {Assert.That(expectedChance,Is.EqualTo(.65f).Within(.00001));Assert.That(withoutDodge,Is.EqualTo(.75f).Within(.00001));}
            for(var seed=0;seed<512;seed++)
            {
                Random.InitState(seed);var roll=Random.value;var next=Random.value;
                Random.InitState(seed);
                var hit=CombatDamageSystem.RollHit(source,target,100,out var dodged);
                Assert.That(hit,Is.EqualTo(roll<=expectedChance),"seed "+seed);
                Assert.That(dodged,Is.EqualTo(roll>expectedChance && roll<=withoutDodge),"seed "+seed);
                Assert.That(Random.value,Is.EqualTo(next),"Exactly one roll, including bonus clamps.");
            }
        }

        private static int SeedForInterval(float lower,float upper)
        {
            for(var seed=0;seed<10000;seed++)
            {Random.InitState(seed);var roll=Random.value;if(roll>lower && roll<=upper)return seed;}
            Assert.Fail("No deterministic seed for interval");return 0;
        }

        [TestCase(false)][TestCase(true)]
        public void DamageLabelsOrdinaryDodgeButKeepsOriginalOnMissGameplayEvent(bool dodge)
        {
            var source=Unit(true);var target=Unit(false,2,1);var session=Session(source,target);
            source.Character.WeaponAccuracy=50;target.Character.Stats.Dodge=new Stat(75f*75f/65f-75f);
            var bus=new CombatFeedbackBus();var messages=new List<CombatFeedbackEvent>();bus.Published+=messages.Add;
            var triggers=new List<PassiveEffectTriggerType>();session.PassiveEffectEventDispatcher=new PassiveEffectCombatEventDispatcher();
            session.PassiveEffectEventDispatcher.Published+=e=>triggers.Add(e.TriggerType);
            var seed=SeedForInterval(dodge ? .65f : .75f, dodge ? .75f : 1f);
            Random.InitState(seed);_=Random.value;var next=Random.value;Random.InitState(seed);
            var result=new CombatDamageSystem(feedbackBus:bus).ResolveAndApplyDamage(session,source,target,10,DamageType.Physical,100f);
            Assert.That(result.Hit,Is.False);Assert.That(target.Character.CurrentHealth,Is.EqualTo(10000));
            Assert.That(messages.Single(e=>e.Type==CombatFeedbackType.Miss).WasDodged,Is.EqualTo(dodge));
            CollectionAssert.Contains(triggers,PassiveEffectTriggerType.OnMiss);
            CollectionAssert.DoesNotContain(triggers,PassiveEffectTriggerType.OnDodge);
            Assert.That(Random.value,Is.EqualTo(next));
        }

        [TestCase(false)][TestCase(true)]
        public void NonDamageEffectsShareOneClassifiedHitRollPerTarget(bool dodge)
        {
            var source=Unit(true);var target=Unit(false,2,1);var session=Session(source,target);
            source.Character.WeaponAccuracy=50;target.Character.Stats.Dodge=new Stat(75f*75f/65f-75f);
            var first=Modifier();var second=Modifier();Set(first,"alwaysHits",false);Set(second,"alwaysHits",false);
            var skill=Skill(first,second);Set(skill,"<SkillAccuracy>k__BackingField",100f);
            var bus=new CombatFeedbackBus();var messages=new List<CombatFeedbackEvent>();bus.Published+=messages.Add;
            Random.InitState(SeedForInterval(dodge ? .65f : .75f, dodge ? .75f : 1f));
            var state=Random.state;_=Random.value;var next=Random.value;Random.state=state;
            Assert.That(new SkillExecutionSystem(feedbackBus:bus).TryExecute(session,source,skill,target),Is.True);
            Assert.That(messages.Count(e=>e.Type==CombatFeedbackType.Miss),Is.EqualTo(1));
            Assert.That(messages.Single(e=>e.Type==CombatFeedbackType.Miss).WasDodged,Is.EqualTo(dodge));
            Assert.That(Modified(target,skill),Is.False);
            Assert.That(Random.value,Is.EqualTo(next));
        }

        [TestCase(false, false)][TestCase(true, false)][TestCase(false, true)]
        public void AuthoredKickUsesCollisionStunFeedbackForAnImmediateBlock(bool partial, bool missed)
        {
            var source=Unit(true); var target=Unit(false,2,1); var session=Session(source,target);
            var skill=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorKick.asset")));
            Set(skill,"<ResourceCost>k__BackingField",0);
            // Extended test-only distance covers partial movement through the same executor.
            if(partial)Set(skill.Effects[0],"displacementDistance",3);
            session.Grid.SetBlocked(new GridPosition(partial?4:3,1));
            if(missed) target.Character.Stats.Dodge=new Stat(1e20f);
            var events=new List<CombatFeedbackEvent>(); var bus=new CombatFeedbackBus(); bus.Published+=events.Add;
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem(feedbackBus:bus),feedbackBus:bus)
                .TryExecute(session,source,skill,target),Is.True);
            if(missed)
            {
                Assert.That(events.Count(e=>e.Type==CombatFeedbackType.Miss),Is.EqualTo(1));
                Assert.That(events.Any(e=>e.Type==CombatFeedbackType.EffectFailed),Is.False,"Skipped dependent effects stay quiet.");
                Assert.That(target.Position,Is.EqualTo(new GridPosition(2,1)));
            }
            else
            {
                var failures=events.Where(e=>e.Type==CombatFeedbackType.EffectFailed && e.EffectName=="Odepchnięcie").ToArray();
                Assert.That(failures.Select(e=>e.FailureReason),Is.EqualTo(partial
                    ? new[]{CombatEffectFailureReason.Interrupted} : System.Array.Empty<CombatEffectFailureReason>()));
                Assert.That(events.Any(e=>e.Type==CombatFeedbackType.StatusApplied),Is.True);
                Assert.That(events.Any(e=>e.Type==CombatFeedbackType.Miss),Is.False);
                Assert.That(target.Position,Is.EqualTo(new GridPosition(partial?3:2,1)));
                Assert.That(StatusEffectSystem.IsStunned(target),Is.True,"Blocked push preserves collision stun.");
            }
        }

        [TestCase(false)][TestCase(true)]
        public void AuthoredHookReportsBlockAndLeavesSuccessfulPullQuiet(bool blocked)
        {
            var source=Unit(true);var target=Unit(false,4,1);var session=Session(source,target);
            var skill=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutHook.asset")));
            Set(skill,"<ResourceCost>k__BackingField",0);
            if(blocked)session.Grid.SetBlocked(new GridPosition(3,1));
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem(feedbackBus:bus),feedbackBus:bus)
                .TryExecute(session,source,skill,target),Is.True);
            Assert.That(events.Any(e=>e.Type==CombatFeedbackType.Miss),Is.False);
            Assert.That(events.Any(e=>e.Type==CombatFeedbackType.EffectFailed && e.EffectName=="Przyciągnięcie"),Is.EqualTo(blocked));
            Assert.That(target.Position.X,blocked?Is.EqualTo(4):Is.LessThan(4));
        }

        [Test]
        public void HookReportsFullMovementResistanceAndDeadTargetsRemainSilent()
        {
            var source=Unit(true);var target=Unit(false,4,1);var session=Session(source,target);
            var skill=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutHook.asset")));
            Set(skill,"<ResourceCost>k__BackingField",0);
            target.Character.BaseResistances.MoveResistance=1f;
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem(feedbackBus:bus),feedbackBus:bus)
                .TryExecute(session,source,skill,target),Is.True);
            Assert.That(target.Position,Is.EqualTo(new GridPosition(4,1)));
            Assert.That(events.Single(e=>e.Type==CombatFeedbackType.EffectFailed).FailureReason,Is.EqualTo(CombatEffectFailureReason.Immune));
            events.Clear();target.Character.LifeState=CharacterLifeState.Dead;
            CombatFailureFeedback.Effect(bus,source,target,EffectType.Pull);
            Assert.That(events,Is.Empty);
        }

        [TestCase(0f,1f,true,false)][TestCase(.5f,1f,true,true)]
        [TestCase(1f,0f,true,false)][TestCase(.5f,1f,false,false)]
        public void StatusAttemptsRespectDisabledChanceSuccessAndSilentCallers(float chance,float roll,bool publish,bool fails)
        {
            var source=Unit(true);var target=Unit(false,2,1);Session(source,target);
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            var applied=new StatusEffectSystem(feedbackBus:bus).ApplyStatus(source,target,StatusEffectIds.Poison,
                chance,2,1,.1f,0,"test",publishFeedback:publish,sharedRoll:roll);
            Assert.That(applied,Is.EqualTo(chance>0 && roll<=chance));
            Assert.That(events.Count(e=>e.Type==CombatFeedbackType.EffectFailed),Is.EqualTo(fails?1:0));
            if(fails) {Assert.That(events.Single().EffectName,Is.EqualTo("Zatrucie"));Assert.That(events.Single().FailureReason,Is.EqualTo(CombatEffectFailureReason.NotApplied));}
            if(!publish)Assert.That(events,Is.Empty);
        }

        [Test]
        public void FullyToleratedStatusReportsImmunityWithoutChangingRandomState()
        {
            var source=Unit(true);var target=Unit(false,2,1);Session(source,target);
            for(var i=0;i<4;i++)target.AddStatusTolerance(StatusEffectIds.Stun);
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            var state=Random.state;var expected=Random.value;Random.state=state;
            Assert.That(new StatusEffectSystem(feedbackBus:bus).ApplyStatus(source,target,StatusEffectIds.Stun,1,1,1,0,0,"test"),Is.False);
            Assert.That(Random.value,Is.EqualTo(expected));
            Assert.That(events.Single().FailureReason,Is.EqualTo(CombatEffectFailureReason.Immune));
        }

        [Test]
        public void ModifierFailureUsesCanonicalStatNameAndNoExtraRolls()
        {
            var source=Unit(true);var target=Unit(false,2,1);var session=Session(source,target);
            var modifier=Modifier(chance:.000001f);Set(modifier,"modifierStatType",CharacterStatType.Dodge);
            var skill=Skill(modifier);var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            var before=Random.state;
            Assert.That(new SkillExecutionSystem(feedbackBus:bus).TryExecute(session,source,skill,target),Is.True);
            var following=Random.value;
            Random.state=before;
            var otherSource=Unit(true);var otherTarget=Unit(false,2,1);var other=Session(otherSource,otherTarget);
            Assert.That(new SkillExecutionSystem().TryExecute(other,otherSource,skill,otherTarget),Is.True);
            Assert.That(Random.value,Is.EqualTo(following));
            Assert.That(events.Single(e=>e.Type==CombatFeedbackType.EffectFailed).EffectName,Is.EqualTo("Unik"));
        }

        [TestCase(false)][TestCase(true)]
        public void ChargeReportsObstacleButPreservesCollisionStun(bool chain)
        {
            var source=Unit(true);var target=Unit(false,chain?2:4,1);var session=Session(source,target);
            session.Grid.SetBlocked(new GridPosition(3,1));
            var effect=new Godmist.Data.Effects.SkillEffectData();Set(effect,"displacementDistance",4);Set(effect,"baseChance",1f);
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            Assert.That(new CombatChargeSystem(feedbackBus:bus).TryCharge(session,source,target,Skill(effect),effect,0),Is.True);
            Assert.That(events.Count(e=>e.Type==CombatFeedbackType.EffectFailed),Is.EqualTo(1));
            Assert.That(events.Single(e=>e.Type==CombatFeedbackType.EffectFailed).EffectName,Is.EqualTo("Szarża"));
            Assert.That(StatusEffectSystem.IsStunned(target),Is.EqualTo(chain));
        }

        [Test]
        public void CrowdedTwoLineFeedbackFindsSpaceBesideExistingLabels()
        {
            var root=Keep(new GameObject("feedback layout",typeof(RectTransform)));
            var controller=root.AddComponent<CombatFloatingFeedbackController>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var views=(List<CombatFloatingFeedbackView>)typeof(CombatFloatingFeedbackController).GetField("activeFeedbackViews",flags).GetValue(controller);
            var sizes=(Dictionary<CombatFloatingFeedbackView,Vector2>)typeof(CombatFloatingFeedbackController).GetField("activeFeedbackSizes",flags).GetValue(controller);
            foreach(var position in new[]{Vector2.zero,new Vector2(0,100)})
            {
                var node=Keep(new GameObject("existing",typeof(RectTransform)));
                node.transform.SetParent(root.transform,false);
                ((RectTransform)node.transform).anchoredPosition=position;
                var view=node.AddComponent<CombatFloatingFeedbackView>();views.Add(view);sizes.Add(view,new Vector2(230,90));
            }
            var candidate=(Vector2)typeof(CombatFloatingFeedbackController).GetMethod("FindAvailablePosition",flags)
                .Invoke(controller,new object[]{Vector2.zero,Unit(false),new Vector2(230,90)});
            var candidateRect=new Rect(candidate-new Vector2(115,45),new Vector2(230,90));
            foreach(var view in views)
            {
                var center=((RectTransform)view.transform).anchoredPosition;
                Assert.That(candidateRect.Overlaps(new Rect(center-new Vector2(115,45),new Vector2(230,90))),Is.False);
            }
            Assert.That(candidate.magnitude,Is.LessThanOrEqualTo(320));
        }

        [TestCase(CombatEffectFailureReason.NotApplied, "Przyciągnięcie", "Odporność")]
        [TestCase(CombatEffectFailureReason.Immune, "Zatrucie", "Odporność")]
        [TestCase(CombatEffectFailureReason.Immune, "Podpalenie", "Odporność")]
        [TestCase(CombatEffectFailureReason.Blocked, "Odepchnięcie", "Zablokowano")]
        [TestCase(CombatEffectFailureReason.Interrupted, "Szarża", "Przerwano")]
        public void FailedEffectsShowOnlyTheOutcome(CombatEffectFailureReason reason,string name,string expected)
        {
            var text=typeof(CombatFloatingFeedbackController).GetMethod("GetText",BindingFlags.Static|BindingFlags.NonPublic);
            var feedback=new CombatFeedbackEvent(Unit(false),CombatFeedbackType.EffectFailed,effectName:name,failureReason:reason);
            Assert.That(text.Invoke(null,new object[]{feedback}),Is.EqualTo(expected));
            Assert.That(feedback.EffectName,Is.EqualTo(name),"Diagnostic identity is preserved.");
        }

        [Test]
        public void DifferentResistedEffectsCoalesceIntoOneVisibleResistancePerTargetAndCast()
        {
            var root=Keep(new GameObject("failure coalescing",typeof(RectTransform)));
            var view=root.AddComponent<CombatFloatingFeedbackController>();var target=Unit(false);var other=Unit(false);
            var accept=typeof(CombatFloatingFeedbackController).GetMethod("TryAcceptNamedNotification",BindingFlags.Instance|BindingFlags.NonPublic);
            CombatFeedbackEvent Failure(BattleUnit unit,string name,CombatEffectFailureReason reason)
                =>new(unit,CombatFeedbackType.EffectFailed,effectName:name,failureReason:reason);
            bool Accept(CombatFeedbackEvent e)=>(bool)accept.Invoke(view,new object[]{e,1});
            Assert.That(Accept(Failure(target,"Przyciągnięcie",CombatEffectFailureReason.NotApplied)),Is.True);
            Assert.That(Accept(Failure(target,"Zatrucie",CombatEffectFailureReason.Immune)),Is.False);
            Assert.That(Accept(Failure(other,"Zatrucie",CombatEffectFailureReason.Immune)),Is.True);
            Assert.That(Accept(new CombatFeedbackEvent(target,CombatFeedbackType.StatusApplied,effectName:"Podpalenie")),Is.True);
            Accept(new CombatFeedbackEvent(target,CombatFeedbackType.SkillUsed));
            Assert.That(Accept(Failure(target,"Zatrucie",CombatEffectFailureReason.Immune)),Is.True);
        }

        [Test]
        public void FailureTextDeduplicatesOnlySameOutcomeAndDoesNotConsumeCombatRandom()
        {
            var root=Keep(new GameObject("feedback",typeof(RectTransform)));
            var view=root.AddComponent<CombatFloatingFeedbackController>();var target=Unit(false);
            const BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic;
            var accept=typeof(CombatFloatingFeedbackController).GetMethod("TryAcceptNamedNotification",instance);
            var text=typeof(CombatFloatingFeedbackController).GetMethod("GetText",BindingFlags.Static|BindingFlags.NonPublic);
            var bus=new CombatFeedbackBus();CombatFeedbackEvent failure=default;bus.Published+=e=>failure=e;
            CombatFailureFeedback.Effect(bus,Unit(true),target,EffectType.Push,CombatEffectFailureReason.Blocked);
            Assert.That(text.Invoke(null,new object[]{failure}),Is.EqualTo("Zablokowano"));
            Assert.That(accept.Invoke(view,new object[]{failure,1}),Is.True);
            Assert.That(accept.Invoke(view,new object[]{failure,1}),Is.False);
            var immune=new CombatFeedbackEvent(target,CombatFeedbackType.EffectFailed,effectName:failure.EffectName,failureReason:CombatEffectFailureReason.Immune);
            Assert.That(accept.Invoke(view,new object[]{immune,1}),Is.True);
            Assert.That(accept.Invoke(view,new object[]{failure,2}),Is.True);
            var state=Random.state;var expected=Random.value;Random.state=state;
            typeof(CombatFloatingFeedbackController).GetMethod("GetRandomLayoutCandidate",instance).Invoke(view,new object[]{Vector2.zero});
            Assert.That(Random.value,Is.EqualTo(expected));
            Assert.That(text.Invoke(null,new object[]{new CombatFeedbackEvent(target,CombatFeedbackType.Miss)}),Is.EqualTo("Pudło"));
            Assert.That(text.Invoke(null,new object[]{new CombatFeedbackEvent(target,CombatFeedbackType.Miss,wasDodged:true)}),Is.EqualTo("Unik"));
        }
    }
}
