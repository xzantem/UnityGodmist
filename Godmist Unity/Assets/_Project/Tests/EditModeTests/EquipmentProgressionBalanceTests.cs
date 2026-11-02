using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class EquipmentProgressionBalanceTests
    {
        readonly List<Object> objects = new();
        [TearDown] public void Cleanup()
        { foreach(var obj in objects) if(obj!=null)Object.DestroyImmediate(obj);objects.Clear(); }

        EquipmentItemInstance Item(bool armor, CharacterStatType stat, float value=100)
        {
            var definition=ScriptableObject.CreateInstance<EquipmentItemDefinition>();objects.Add(definition);
            typeof(ItemDefinition).GetField("<Id>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(definition,armor?"balance.armor":"balance.weapon");
            typeof(ItemDefinition).GetField("<ItemType>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(definition,armor?ItemType.Armor:ItemType.Weapon);
            return new EquipmentItemInstance(definition){CurrentRarity=RarityTier.Common,
                BaseStatModifiers=new(){new StatModifier{StatType=stat,Value=value}}};
        }
        [TestCase(false,0f,100f)] [TestCase(false,.5f,125f)] [TestCase(false,1f,150f)]
        [TestCase(true,0f,100f)] [TestCase(true,.5f,175f)] [TestCase(true,1f,250f)]
        [TestCase(false,-1f,100f)] [TestCase(false,2f,150f)]
        [TestCase(true,-1f,100f)] [TestCase(true,2f,250f)]
        public void UpgradeUsesSlotCoefficientAndClampedProgress(bool armor,float progress,float expected)
        {
            var item=Item(armor,armor?CharacterStatType.PhysicalDefense:CharacterStatType.MinAttack);
            item.UpgradeProgress=progress;item.RecalculateStatModifiers();item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(expected).Within(.001f));
            Assert.That(item.BaseStatModifiers[0].Value,Is.EqualTo(100));
            if(armor){item.BaseStatModifiers[0].StatType=CharacterStatType.MagicalDefense;item.RecalculateStatModifiers();
                Assert.That(item.StatModifiers[0].Value,Is.EqualTo(expected).Within(.001f));}
        }
        [TestCase(RarityTier.Trash,.85f)] [TestCase(RarityTier.Broken,.9f)] [TestCase(RarityTier.Damaged,.95f)]
        [TestCase(RarityTier.Common,1f)] [TestCase(RarityTier.Rare,1.125f)]
        [TestCase(RarityTier.Legendary,1.375f)] [TestCase(RarityTier.Godly,1.85f)]
        public void ReforgeCritDamageUsesHalfRarityBonusWithoutCompounding(RarityTier rarity,float multiplier)
        {
            var item=Item(false,CharacterStatType.CritDamage,.5f);
            item.CurrentRarity=rarity;item.UpgradeProgress=1;item.RecalculateStatModifiers();item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(.5f*multiplier).Within(.0001f));
            item.CurrentRarity=RarityTier.Common;item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(.5f));
        }
        [TestCase(false,125f)] [TestCase(true,175f)]
        public void BaselineLessLegacySaveRecoversOldRuleThenUsesNewRule(bool armor,float expected)
        {
            var item=Item(armor,armor?CharacterStatType.PhysicalDefense:CharacterStatType.MinAttack);
            item.BaseStatModifiers.Clear();item.UpgradeProgress=.5f;
            item.StatModifiers=new(){new StatModifier{StatType=armor?CharacterStatType.PhysicalDefense:CharacterStatType.MinAttack,Value=150}};
            Assert.That(Godmist.Systems.SaveLoad.LegacySaveMigrations.RecoverEquipmentBaseline(item), Is.True);
            item.RecalculateStatModifiers();item.RecalculateStatModifiers();
            Assert.That(item.BaseStatModifiers[0].Value,Is.EqualTo(100).Within(.001f));
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(expected).Within(.001f));
        }
        [TestCase(false,150f)] [TestCase(true,250f)]
        public void UpgradePreviewUsesRuntimeFormulaAndPreservesSelectedProgress(bool armor,float expected)
        {
            var item=Item(armor,armor?CharacterStatType.PhysicalDefense:CharacterStatType.MinAttack);
            item.UpgradeProgress=.4f;item.RecalculateStatModifiers();
            var host=new GameObject("upgrade-preview-test");objects.Add(host);
            var view=host.AddComponent<EquipmentUpgradeWindowView>();
            typeof(EquipmentUpgradeWindowView).GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,item);
            var preview=(EquipmentItemInstance)typeof(EquipmentUpgradeWindowView).GetMethod("BuildPreviewItem",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(view,new object[]{new EquipmentUpgradePreview{TargetProgress=1}});
            Assert.That(preview.StatModifiers[0].Value,Is.EqualTo(expected).Within(.001f));
            Assert.That(item.UpgradeProgress,Is.EqualTo(.4f));
            Assert.That(item.BaseStatModifiers[0].Value,Is.EqualTo(100));
            Assert.That(preview.BaseStatModifiers[0],Is.Not.SameAs(item.BaseStatModifiers[0]));
        }
        [Test] public void ReforgeSuccessPreviewUsesReducedCritScalingWithoutChangingSelectedRarity()
        {
            var item=Item(false,CharacterStatType.CritDamage,.5f);
            item.CurrentRarity=RarityTier.Mythic;item.RecalculateStatModifiers();
            var host=new GameObject("reforge-preview-test");objects.Add(host);
            var view=host.AddComponent<EquipmentUpgradeWindowView>();
            typeof(EquipmentUpgradeWindowView).GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,item);
            var projected=(EquipmentItemInstance)typeof(EquipmentUpgradeWindowView).GetMethod("BuildReforgeSuccessPreview",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(view,new object[]{new EquipmentUpgradePreview{GoldCost=1,IncreaseChance=.35f,TargetRarity=RarityTier.Godly}});
            Assert.That(projected.StatModifiers[0].Value,Is.EqualTo(.925f).Within(.0001f));
            Assert.That(projected.CurrentRarity,Is.EqualTo(RarityTier.Godly));
            Assert.That(item.CurrentRarity,Is.EqualTo(RarityTier.Mythic));
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(.7875f).Within(.0001f));
        }
        [Test] public void ArmorHpAndDodgeDependOnRarityNotUpgrade()
        {
            var item=Item(true,CharacterStatType.MaxHealth);
            item.BaseStatModifiers.Add(new StatModifier{StatType=CharacterStatType.Dodge,Value=20});
            item.CurrentRarity=RarityTier.Godly;item.UpgradeProgress=1;item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(270).Within(.001f));
            Assert.That(item.StatModifiers[1].Value,Is.EqualTo(30.2f).Within(.001f));
            var hp=item.StatModifiers[0].Value;item.UpgradeProgress=0;item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers[0].Value,Is.EqualTo(hp));
        }
    }
}
