using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ApprovedBossUniqueTests
    {
        readonly List<UnityEngine.Object> temporary = new();
        static UniqueEquipmentDefinition Slayer(string role) => AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>(VS16CatacombsBossSetup.SlayerUniqueRoot + role + ".asset");
        static UniqueEquipmentDefinition Cloak => AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>(VS16CatacombsBossSetup.SmugglerCloakPath);
        [TearDown] public void Cleanup() { foreach(var obj in temporary) UnityEngine.Object.DestroyImmediate(obj); }
        BattleUnit Unit()
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); temporary.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("<Id>k__BackingField").stringValue = Guid.NewGuid().ToString();
            serialized.FindProperty("<DisplayName>k__BackingField").stringValue = "Test"; serialized.ApplyModifiedPropertiesWithoutUndo();
            return new BattleUnit { Character = new CharacterInstance { Definition = definition,
                CharacterId = definition.Id, LifeState = CharacterLifeState.Alive, CurrentHealth = 1000,
                Stats = new CharacterStats { MaxHealth = new Stat(1000), MaxResource = new Stat(100),
                    MinAttack = new Stat(100), MaxAttack = new Stat(100), Speed = new Stat(20) },
                ResourcePool = new ResourcePool(ResourceType.Mana, 100, 100, 0), Resistances = new ResistanceSet(),
                ShieldPool = new ShieldPool(), WeaponAccuracy = 100f } };
        }
        static void Equip(BattleUnit unit, UniqueEquipmentDefinition definition, int level = 10)
        {
            var item = new EquipmentItemInstance { Definition = definition, ItemLevel = level, InstanceId = Guid.NewGuid().ToString() };
            unit.Character.Equipment.SetItem(definition.EquipmentSlot, item);
            new CharacterStatPipelineSystem().Refresh(unit.Character);
        }
        static int Hit(BattleUnit source, BattleUnit target, DamageType type = DamageType.Physical,
            AttackType attack = AttackType.Melee, int amount = 100)
        {
            return new CombatDamageSystem().ApplyDamage(new CombatSession(), source, target, amount, type,
                passiveContext: new PassiveEffectCombatContext(source: source, target: target, attackType: attack, damageType: type));
        }

        [TestCase(10, .25f, .30f)] [TestCase(19, .25f, .30f)]
        [TestCase(20, .30f, .35f)] [TestCase(29, .30f, .35f)]
        [TestCase(30, .35f, .40f)] [TestCase(39, .35f, .40f)]
        [TestCase(40, .40f, .45f)] [TestCase(49, .40f, .45f)] [TestCase(50, .50f, .55f)]
        public void ApprovedArmorBracketsSelectCorrectValue(int level, float ribbed, float cloak)
        {
            var source=Unit(); var target=Unit(); Equip(target,Slayer("ScoutArmor"),level);
            Assert.That(Hit(source,target),Is.EqualTo(Mathf.RoundToInt(100*(1-ribbed))));
            target=Unit(); Equip(target,Cloak,level);
            Assert.That(Hit(source,target,attack:AttackType.Ranged),Is.EqualTo(Mathf.RoundToInt(100*(1-cloak))));
        }
        [Test]
        public void OnlyFirstPhysicalHitIsReducedAndOnlyOwnersTurnRearmsIt()
        {
            var source=Unit(); var target=Unit(); Equip(target,Slayer("ScoutArmor"));
            Assert.That(Hit(source,target),Is.EqualTo(75)); Assert.That(Hit(source,target),Is.EqualTo(100));
            var turnSession = new CombatSession { Units = new List<BattleUnit> { source, target } };
            new CombatTurnSystem().BeginActiveTurn(turnSession,source);
            Assert.That(Hit(source,target),Is.EqualTo(100));
            new CombatTurnSystem().BeginActiveTurn(turnSession,target);
            Assert.That(Hit(source,target),Is.EqualTo(75));
        }
        [Test]
        public void MagicalDotsZeroDamageAndFullyShieldedHitsDoNotConsumeProtection()
        {
            var source=Unit(); var target=Unit(); Equip(target,Slayer("ScoutArmor"));
            Assert.That(Hit(source,target,DamageType.Magical),Is.EqualTo(100));
            Assert.That(Hit(source,target,DamageType.Bleed),Is.EqualTo(100));
            Assert.That(Hit(source,target,amount:0),Is.Zero);
            target.Character.ShieldPool.AddLayer(500,"test"); Assert.That(Hit(source,target),Is.Zero);
            Assert.That(target.RibbedTunicProtectionUsed,Is.False);
            target.Character.ShieldPool.RemoveLayersBySourceId("test");
            Assert.That(Hit(source,target),Is.EqualTo(75));
        }
        [Test]
        public void GuaranteedDodgeDoesNotConsumeProtection()
        {
            var source=Unit(); var target=Unit(); Equip(target,Slayer("ScoutArmor"));
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime { UniqueBuffType=UniqueBuffType.GuaranteedDodge });
            var result=new CombatDamageSystem().ResolveAndApplyDamage(new CombatSession(),source,target,100,DamageType.Physical,100f);
            Assert.That(result.Hit,Is.False); Assert.That(target.RibbedTunicProtectionUsed,Is.False);
            Assert.That(Hit(source,target),Is.EqualTo(75));
        }
        [Test]
        public void MissAndInvulnerabilityDoNotConsumeProtection()
        {
            var source=Unit();var target=Unit();Equip(target,Slayer("ScoutArmor"));
            Assert.That(new CombatDamageSystem().ResolveAndApplyDamage(new CombatSession(),source,target,100,DamageType.Physical,false).Hit,Is.False);
            target.Character.DebugIsInvulnerable=true;Assert.That(Hit(source,target),Is.Zero);
            target.Character.DebugIsInvulnerable=false;Assert.That(Hit(source,target),Is.EqualTo(75));
        }
        static void Private(object obj,string name,object value) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        SkillData ThreeHitSkill(AttackType attack)
        {
            var skill=ScriptableObject.CreateInstance<SkillData>();temporary.Add(skill);
            Private(skill,"<AttackType>k__BackingField",attack);
            var effect=new SkillEffectData();
            Private(effect,"effectType",EffectType.DealDamage);Private(effect,"damageType",DamageType.Physical);
            Private(effect,"damageValueSource",DamageValueSource.AttackRange);Private(effect,"damageMultiplier",1f);Private(effect,"damageHitCount",3);
            Private(skill,"effects",new List<SkillEffectData>{effect});return skill;
        }
        [TestCase(0,275)] [TestCase(1000,225)]
        public void MultiHitPreviewReducesOnlyFirstHealthHitWithoutSpendingProtection(int shield,int expected)
        {
            var source=Unit();var target=Unit();Equip(target,Slayer("ScoutArmor"));
            target.Character.ShieldPool.AddLayer(shield,"test");var skill=ThreeHitSkill(AttackType.Melee);
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill);
            Assert.That(preview.Damage.Minimum,Is.EqualTo(expected));Assert.That(preview.Damage.Maximum,Is.EqualTo(expected));
            Assert.That(target.RibbedTunicProtectionUsed,Is.False);
        }
        [Test]
        public void RangedPreviewMatchesExecutionAtPointBlank()
        {
            var source=Unit();var target=Unit();Equip(target,Cloak);var skill=ThreeHitSkill(AttackType.Ranged);
            target.Position=new GridPosition(0,1);
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill);
            Assert.That(preview.Damage.Minimum,Is.EqualTo(210));
            var dealt=Hit(source,target,attack:AttackType.Ranged)+Hit(source,target,attack:AttackType.Ranged)+Hit(source,target,attack:AttackType.Ranged);
            Assert.That(dealt,Is.EqualTo(preview.Damage.Minimum));
        }
        [Test]
        public void HammerStunsOnSuccessfulHitAndDoesNotProcOnMiss()
        {
            var source=Unit();var target=Unit();source.IsPlayerSide=true;Equip(source,Slayer("PaladinHammer"));
            var session=new CombatSession { Units=new(){source,target}, PassiveEffectEventDispatcher=new PassiveEffectCombatEventDispatcher() };
            using var executor=new PassiveEffectCombatExecutionSystem(session,new StatusEffectSystem());
            var damage=new CombatDamageSystem();var savedRandom=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(42);
                for(var i=0;i<100;i++)damage.ResolveAndApplyDamage(session,source,target,1,DamageType.Physical,false);
                Assert.That(StatusEffectSystem.HasStatus(target,Godmist.Core.Constants.StatusEffectIds.Stun),Is.False);
                for(var i=0;i<100 && !StatusEffectSystem.HasStatus(target,Godmist.Core.Constants.StatusEffectIds.Stun);i++)
                    damage.ResolveAndApplyDamage(session,source,target,1,DamageType.Physical,true);
                Assert.That(StatusEffectSystem.HasStatus(target,Godmist.Core.Constants.StatusEffectIds.Stun),Is.True);
            }
            finally { UnityEngine.Random.state=savedRandom; }
        }
        [Test]
        public void RangedProtectionUsesAttackCategoryAndAppliesToEveryHit()
        {
            var source=Unit(); var target=Unit(); Equip(target,Cloak,50);
            source.Position=new GridPosition(0,0); target.Position=new GridPosition(0,1);
            Assert.That(Hit(source,target,attack:AttackType.Ranged),Is.EqualTo(45));
            Assert.That(Hit(source,target,attack:AttackType.Ranged),Is.EqualTo(45));
            target.Position=new GridPosition(0,50);
            Assert.That(Hit(source,target),Is.EqualTo(100));
            Assert.That(Hit(source,target,DamageType.Magical,AttackType.Ranged),Is.EqualTo(100));
            Assert.That(Hit(source,target,DamageType.Poison,AttackType.Ranged),Is.EqualTo(100));
        }
        [Test]
        public void PartiallyShieldedHitConsumesProtectionWhenHealthIsDamaged()
        {
            var source=Unit();var target=Unit();Equip(target,Slayer("ScoutArmor"));
            target.Character.ShieldPool.AddLayer(50,"test");
            Assert.That(Hit(source,target),Is.EqualTo(25));Assert.That(target.RibbedTunicProtectionUsed,Is.True);
            Assert.That(Hit(source,target),Is.EqualTo(100));
        }
        [Test]
        public void ArmorReductionMultipliesOtherProtection()
        {
            var source=Unit(); var target=Unit(); Equip(target,Cloak);
            var rune=new GalduryteItemInstance { IsIdentified=true,Effects=new(){new GalduryteEffectInstance(
                GalduryteEffectType.PhysicalDamageReduction,GaldurytePowderColor.Emerald,GalduryteEffectGrade.S,.30f)} };
            target.Character.Equipment.Armor.UpgradeProgress=1;
            target.Character.Equipment.Armor.SocketedGaldurytes.Add(rune);
            Assert.That(Hit(source,target,attack:AttackType.Ranged),Is.EqualTo(49));
        }
        [TestCase(10,25)] [TestCase(20,30)]
        public void FuryArmorIncreasesMaximumRageWithoutAccumulating(int level,int bonus)
        {
            var target=Unit();target.Character.ResourcePool.ResourceType=ResourceType.Rage;
            Equip(target,Slayer("WarriorArmor"),level);
            Assert.That(target.Character.ResourcePool.MaxValue,Is.EqualTo(100+bonus));
            new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.ResourcePool.MaxValue,Is.EqualTo(100+bonus));
            target.Character.Equipment.Armor=null;new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.ResourcePool.MaxValue,Is.EqualTo(100));
        }
        [TestCase(10,30)] [TestCase(20,45)]
        public void RunicRobesUseMaximumManaAndRefreshWithoutAccumulation(int level,int bonus)
        {
            var target=Unit();Equip(target,Slayer("MageArmor"),level);target.Character.ResourcePool.CurrentValue=0;
            new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(1000+bonus));
            target.Character.Stats.MaxResource.BaseValue=200;new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(1000+2*bonus));
            target.Character.Equipment.Armor=null;new CharacterStatPipelineSystem().Refresh(target.Character);
            Assert.That(target.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(1000));
        }
        [Test]
        public void AllNewUniquesAreRegisteredValidAndOldKingFallbackIsUnchanged()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            foreach(var item in new[]{Slayer("WarriorArmor"),Slayer("ScoutArmor"),Slayer("MageArmor"),Slayer("PaladinHammer"),Cloak})
            {
                Assert.That(item,Is.Not.Null);Assert.That(catalog.ItemDefinitions.Count(i=>i==item),Is.EqualTo(1));
                Assert.That(item.Icon,Is.Not.Null);Assert.That(item.RequiredClass,Is.Not.Null);
                foreach(var level in new[]{10,20,30,40,50})foreach(var effect in item.GetEffects(level))
                    Assert.That(PassiveEffectDefinitionValidator.Validate(effect).Errors,Is.Empty);
            }
            Assert.That(Slayer("ScoutArmor").DisplayName,Is.EqualTo("Żebrowana Tunika"));
            var king=AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>("Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BossDeadKing_PaladinArmor.asset");
            Assert.That(king.GetEffects(50),Is.SameAs(king.EffectsLevel20));
        }
    }
}
