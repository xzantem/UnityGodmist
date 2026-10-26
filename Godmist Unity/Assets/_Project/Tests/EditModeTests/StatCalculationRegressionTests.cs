using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class StatCalculationRegressionTests
    {
        readonly List<Object> assets=new();
        [TearDown] public void Cleanup() { foreach(var value in assets)if(value!=null)Object.DestroyImmediate(value);assets.Clear(); }
        static BattleUnit Unit(float resistance=.25f)=>new() { IsPlayerSide=true,Character=new CharacterInstance {
            CharacterId="stat.audit",Level=48,LifeState=CharacterLifeState.Alive,CurrentHealth=10000,
            Stats=new CharacterStats { MaxHealth=new Stat(10000),MinAttack=new Stat(120),MaxAttack=new Stat(120),
                WeaponAccuracy=new Stat(100),CritChance=new Stat(0),CritDamage=new Stat(1.5f),
                MaxResource=new Stat(100),ResourceRegeneration=new Stat(15) },
            Resistances=Resistances(resistance),BaseResistances=Resistances(resistance) } };
        static ResistanceSet Resistances(float value)=>new() { BleedResistance=value,PoisonResistance=value,FireResistance=value,
            WeakeningResistance=value,SuppressionResistance=value,MoveResistance=value };
        static PassiveEffectRuntime Modifier(ResistanceType type,float value,PassiveEffectLifetime lifetime=PassiveEffectLifetime.Persistent)
        {
            var effect=new PassiveEffectRuntime("stat.audit.resistance",new List<PassiveEffectSourceRuntime>(),1,1,0,lifetime,
                lifetime==PassiveEffectLifetime.Timed?PassiveEffectTickPhase.TurnEnd:PassiveEffectTickPhase.None);
            effect.SetPayload(new ResistanceModifierPayload { ResistanceType=type,Value=value });return effect;
        }
        static float Snapshot(CharacterInstance c,ResistanceType t)=>t switch {
            ResistanceType.Bleed=>c.Resistances.BleedResistance,ResistanceType.Poison=>c.Resistances.PoisonResistance,
            ResistanceType.Fire=>c.Resistances.FireResistance,ResistanceType.Weakening=>c.Resistances.WeakeningResistance,
            ResistanceType.Suppression=>c.Resistances.SuppressionResistance,_=>c.Resistances.MoveResistance };
        static void Set(object target,string field,object value)=>target.GetType()
            .GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);

        [TestCase(ResistanceType.Bleed)][TestCase(ResistanceType.Poison)][TestCase(ResistanceType.Fire)]
        [TestCase(ResistanceType.Weakening)][TestCase(ResistanceType.Suppression)][TestCase(ResistanceType.Move)]
        public void RefreshAndCombatCountTypedResistanceOnce(ResistanceType type)
        {
            var unit=Unit();unit.Character.PassiveEffects.Add(Modifier(type,.07f));var pipeline=new CharacterStatPipelineSystem();
            for(int i=0;i<3;i++) {
                pipeline.Refresh(unit.Character);
                Assert.That(Snapshot(unit.Character,type),Is.EqualTo(.32f).Within(.00001));
                Assert.That(ModifyResistanceSystem.GetEffectiveResistance(unit,type),Is.EqualTo(.32f).Within(.00001));
            }
            unit.ResistanceModifiers[type]=.1f;
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(unit,type),Is.EqualTo(.42f).Within(.00001));
            new PassiveEffectLifecycleSystem().RemoveEffect(unit.Character,"stat.audit.resistance");
            Assert.That(Snapshot(unit.Character,type),Is.EqualTo(.25f).Within(.00001));
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(unit,type),Is.EqualTo(.35f).Within(.00001));
        }
        [Test] public void BraceletExampleUses32PercentForDotInsteadOf39Percent()
        {
            var unit=Unit();unit.Character.PassiveEffects.Add(Modifier(ResistanceType.Poison,.07f));
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            Assert.That(DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(unit,"poison",154),Is.EqualTo(209));
        }
        [Test] public void RefreshDoesNotBakeModifiersIntoLegacyBase()
        {
            var unit=Unit();unit.Character.BaseResistances=null;
            unit.Character.PassiveEffects.Add(Modifier(ResistanceType.Poison,.1f));var pipeline=new CharacterStatPipelineSystem();
            pipeline.Refresh(unit.Character);pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.BaseResistances.PoisonResistance,Is.EqualTo(.25f));
            Assert.That(unit.Character.Resistances.PoisonResistance,Is.EqualTo(.35f).Within(.00001));
            unit.Character.PassiveEffects.Clear();pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.Resistances.PoisonResistance,Is.EqualTo(.25f));
        }
        [Test] public void AllResistanceAffectsEverySnapshotAndExpiresImmediately()
        {
            var unit=Unit();var life=new PassiveEffectLifecycleSystem();
            Assert.That(life.TryApplyTimedEffect(unit.Character,Modifier(ResistanceType.All,.1f,PassiveEffectLifetime.Timed),
                PassiveEffectStackingMode.ReplaceDurationAndValues,out _),Is.True);
            foreach(var type in new[]{ResistanceType.Bleed,ResistanceType.Poison,ResistanceType.Fire,ResistanceType.Weakening,ResistanceType.Suppression,ResistanceType.Move})
                Assert.That(Snapshot(unit.Character,type),Is.EqualTo(.35f).Within(.00001));
            life.ProcessTurnEnd(unit);
            Assert.That(unit.Character.Resistances.PoisonResistance,Is.EqualTo(.25f).Within(.00001));
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(unit,ResistanceType.Poison),Is.EqualTo(.25f).Within(.00001));
        }
        [Test] public void SumIsClampedAfterClassPassiveAndLegacyModifiers()
        {
            var unit=Unit(.8f);unit.Character.PassiveEffects.Add(Modifier(ResistanceType.Fire,-1f));
            // A single fire-tree point contributes 1pp. A raw negative subtotal
            // must not be clamped before the final class/combat contributions.
            unit.Character.SkillTreeProgress.Add(new SkillTreeProgress { TreeId=MageCombatSystem.Fire,
                NodeInvestments=new(){new SkillTreeNodeInvestment { NodeId="node.mage.fire.pyromania",Ranks=1 }} });
            unit.ResistanceModifiers[ResistanceType.All]=.3f;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(unit,ResistanceType.Fire),Is.EqualTo(.11f).Within(.00001));
            Assert.That(unit.Character.Resistances.FireResistance,Is.Zero);
            unit.Character.PassiveEffects.Clear();unit.Character.PassiveEffects.Add(Modifier(ResistanceType.Fire,.6f));
            unit.ResistanceModifiers[ResistanceType.All]=-.3f;
            Assert.That(ModifyResistanceSystem.GetEffectiveResistance(unit,ResistanceType.Fire),Is.EqualTo(1f));
        }
        [Test] public void GalduryteResistanceAppliesToStatusAndDotOnceAndStopsWhenDestroyed()
        {
            var unit=Unit();var stone=new GalduryteItemInstance { IsIdentified=true,Effects=new() {
                new GalduryteEffectInstance(GalduryteEffectType.DotResistance,GaldurytePowderColor.Red,GalduryteEffectGrade.S,.1f),
                new GalduryteEffectInstance(GalduryteEffectType.AllResistance,GaldurytePowderColor.Red,GalduryteEffectGrade.S,.05f) } };
            unit.Character.Equipment.Weapon=new EquipmentItemInstance { UpgradeProgress=1,SocketedGaldurytes=new(){stone} };
            var pipeline=new CharacterStatPipelineSystem();pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.Resistances.PoisonResistance,Is.EqualTo(.4f).Within(.00001));
            Assert.That(DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(unit,"poison",1000),Is.EqualTo(1200));
            Assert.That(StatusEffectSystem.CalculateStatusApplicationChance(null,unit,"poison",.5f),
                Is.EqualTo(StatusEffectSystem.CalculateEffectChance(.5f,.4f)).Within(.00001));
            stone.IsDestroyed=true;pipeline.Refresh(unit.Character);
            Assert.That(DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(unit,"poison",1000),Is.EqualTo(1500));
        }
        [Test] public void WeaponAccuracySynchronizesWithoutResourcePool()
        {
            var unit=Unit();unit.Character.Stats.WeaponAccuracy.BaseValue=37;
            new CharacterStatPipelineSystem().Refresh(unit.Character);
            Assert.That(unit.Character.ResourcePool,Is.Null);Assert.That(unit.Character.WeaponAccuracy,Is.EqualTo(37));
        }
        [Test]
        public void RepeatedJsonSaveRestoreDoesNotBakeResistanceBuffIntoBase()
        {
            var definition=ScriptableObject.CreateInstance<CharacterDefinition>();assets.Add(definition);
            var cls=ScriptableObject.CreateInstance<CharacterClassData>();assets.Add(cls);
            var catalog=ScriptableObject.CreateInstance<GameContentCatalog>();assets.Add(catalog);
            Set(definition,"<Id>k__BackingField","character.resistance.audit");
            Set(definition.BaseResistances,"<PoisonResistance>k__BackingField",.25f);
            Set(cls,"<Id>k__BackingField","class.resistance.audit");
            Set(cls,"<ResourceType>k__BackingField",ResourceType.Mana);
            catalog.CharacterDefinitions.Add(definition);catalog.CharacterClasses.Add(cls);
            var character=new CharacterInstanceFactory().Create(definition,cls);
            var effect=Modifier(ResistanceType.Poison,.07f,PassiveEffectLifetime.Timed);
            Assert.That(new PassiveEffectLifecycleSystem().TryApplyTimedEffect(character,effect,
                PassiveEffectStackingMode.ReplaceDurationAndValues,out _),Is.True);
            var session=new GameSession(GameModeState.Exploration);
            session.TryRegisterCharacter(character);session.TryAddCharacterToActiveParty(character.CharacterId);
            var serializer=new GameSaveJsonSerializer();
            for(int i=0;i<3;i++)
            {
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Resistance regression");
                Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot),out var data),Is.True);
                Assert.That(new GameSessionRestoreFactory().TryCreate(data,catalog,out session),Is.True);
                character=session.CharactersById[character.CharacterId];
                Assert.That(character.BaseResistances.PoisonResistance,Is.EqualTo(.25f).Within(.00001));
                Assert.That(character.Resistances.PoisonResistance,Is.EqualTo(.32f).Within(.00001));
                Assert.That(DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(
                    new BattleUnit { Character=character },"poison",154),Is.EqualTo(209));
            }
            new PassiveEffectLifecycleSystem().RemoveEffect(character,effect.EffectId);
            Assert.That(character.Resistances.PoisonResistance,Is.EqualTo(.25f).Within(.00001));
        }
        [Test] public void MissingManaRegenerationUsesNewMaximumOnFirstRefresh()
        {
            var unit=Unit();unit.Character.ResourcePool=new ResourcePool(ResourceType.Mana,50,100,15);
            unit.Character.Stats.MaxResource.BaseValue=200;
            unit.Character.SkillTreeProgress.Add(new SkillTreeProgress { TreeId=MageCombatSystem.Warlock,
                NodeInvestments=new(){new SkillTreeNodeInvestment { NodeId="node.mage.warlock.energy_saving",Ranks=2 }} });
            var pipeline=new CharacterStatPipelineSystem();pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.ResourcePool.MaxValue,Is.EqualTo(200));
            Assert.That(unit.Character.Stats.ResourceRegeneration.GetValue(),Is.EqualTo(20.625f).Within(.00001));
            Assert.That(unit.Character.ResourcePool.Regeneration,Is.EqualTo(21));
            pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.Stats.ResourceRegeneration.GetValue(),Is.EqualTo(20.625f).Within(.00001));
            unit.Character.Stats.MaxResource.BaseValue=100.4f;
            unit.Character.ResourcePool.CurrentValue=100;
            pipeline.Refresh(unit.Character);
            Assert.That(unit.Character.ResourcePool.MaxValue,Is.EqualTo(100));
            Assert.That(unit.Character.Stats.ResourceRegeneration.GetValue(),Is.EqualTo(15f));
        }
        [TestCase(DamageType.Physical,72)][TestCase(DamageType.Magical,60)]
        public void AnalysisPenetrationMatchesPreviewAndExecutionWithoutDoubling(DamageType type,int expected)
        {
            var source=Unit();var target=Unit();target.IsPlayerSide=false;
            source.ScoutState.AnalysisTurns=2;source.ScoutState.AnalysisPenetration=1;
            source.Character.Stats.PhysicalDefensePenetration=new Stat(.5f);source.Character.Stats.MagicalDefensePenetration=new Stat(.5f);
            target.Character.Stats.PhysicalDefense=new Stat(240);target.Character.Stats.MagicalDefense=new Stat(240);
            var effect=new SkillEffectData();Set(effect,"damageType",type);Set(effect,"bonusPenetration",.5f);
            var skill=ScriptableObject.CreateInstance<SkillData>();assets.Add(skill);Set(skill,"effects",new List<SkillEffectData>{effect});
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill);
            var session=new CombatSession();session.Units.Add(source);session.Units.Add(target);
            var context=new PassiveEffectCombatContext(session:session,source:source,target:target,skill:skill,damageType:type);
            var result=new CombatDamageSystem().ResolveAndApplyDamage(session,source,target,120,type,wasHit:true,bonusDefensePenetration:.5f,passiveContext:context);
            Assert.That(result.HealthDamage,Is.EqualTo(expected));
            Assert.That(preview.Damage.Minimum,Is.EqualTo(result.HealthDamage));Assert.That(preview.Damage.Maximum,Is.EqualTo(result.HealthDamage));
        }
    }
}
