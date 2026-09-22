using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Core.Constants;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class VS11EquipmentTests
    {
        readonly List<UnityEngine.Object> assets = new();
        [TearDown] public void Cleanup() { foreach (var a in assets) UnityEngine.Object.DestroyImmediate(a); }
        T Asset<T>() where T : ScriptableObject { var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a; }
        static void Set(object o,string n,object v) => o.GetType().GetField("<"+n+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
        BattleUnit Unit()
        {
            var def=Asset<CharacterDefinition>(); Set(def,"Id",Guid.NewGuid().ToString());Set(def,"DisplayName","test");
            return new BattleUnit { Character=new CharacterInstance { CharacterId=def.Id,Definition=def,LifeState=CharacterLifeState.Alive,
                CurrentHealth=10,Stats=new CharacterStats { MaxHealth=new Stat(100),MinAttack=new Stat(10),MaxAttack=new Stat(10),Speed=new Stat(20) },
                ResourcePool=new ResourcePool(ResourceType.Mana,0,100,0),Resistances=new ResistanceSet() },CurrentActionValue=20 };
        }
        static GalduryteItemInstance Equip(BattleUnit u, params (GalduryteEffectType,float)[] values)
        {
            var rune=new GalduryteItemInstance { IsIdentified=true,Effects=values.Select(v=>new GalduryteEffectInstance(v.Item1,GaldurytePowderColor.Red,GalduryteEffectGrade.S,v.Item2)).ToList() };
            u.Character.Equipment.Weapon=new EquipmentItemInstance { UpgradeProgress=1,SocketedGaldurytes=new(){rune} };
            return rune;
        }

        [Test] public void GalduryteTooltipExplainsEffectsWithoutInternalAbbreviations()
        {
            var rune = new GalduryteItemInstance
            {
                IsIdentified = true,
                Effects = new List<GalduryteEffectInstance>
                {
                    new(GalduryteEffectType.BurnOnAttack, GaldurytePowderColor.Red, GalduryteEffectGrade.S, .2f),
                    new(GalduryteEffectType.ActionValueOnAttack, GaldurytePowderColor.Blue, GalduryteEffectGrade.S, 4f)
                }
            };
            var summary = rune.GetEffectSummary();
            Assert.That(summary, Does.Contain("20% szansy na podpalenie"));
            Assert.That(summary, Does.Contain("raz na użycie umiejętności"));
            Assert.That(summary, Does.Contain("wszystkich trafionych wrogów"));
            Assert.That(summary, Does.Not.Contain("AV"));
            Assert.That(summary, Does.Not.Contain("[p]"));
            Assert.That(GalduryteRuntimeText.FormatEffectRange(GalduryteEffectType.CriticalDamageFlat, .1f, .25f),
                Does.Contain("0,1–0,25×").Or.Contain("0.1–0.25×"));
            Assert.That(GalduryteRuntimeText.FormatEffectRange(GalduryteEffectType.HealthPerTurn, .005f, .01f),
                Does.Contain("maksymalnego zdrowia"));
        }

        [Test] public void MaterialAndPartTooltipsDescribeTheItemInsteadOfLootSourcesOrSpreadsheetRows()
        {
            var granat = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/ScriptableObjects" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Single(item => item != null && item.Id == "item.vs10.granat");
            Assert.That(granat.Description, Does.Contain("kamień szlachetny"));
            Assert.That(granat.Description, Does.Not.Contain("torba zasobów"));

            var part = AssetDatabase.FindAssets("t:EquipmentPartDefinition", new[] { "Assets/_Project/ScriptableObjects" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EquipmentPartDefinition>)
                .First(item => item != null && item.Id.StartsWith("part.vs10.warrior.0."));
            Assert.That(part.Description, Does.Contain("Ostrze długiego miecza"));
            Assert.That(part.Description, Does.Not.Contain(".xlsx"));
        }

        [Test] public void ExpandedPoolContains53RowsAndEveryColorCanRollMoreThanItsFirstEntry()
        {
            Assert.That(GalduryteRuntimeRules.GetEffectPool(GalduryteTargetType.Weapon).Count+GalduryteRuntimeRules.GetEffectPool(GalduryteTargetType.Armor).Count,Is.EqualTo(53));
            foreach(var target in new[]{GalduryteTargetType.Weapon,GalduryteTargetType.Armor})
            foreach(GaldurytePowderColor color in Enum.GetValues(typeof(GaldurytePowderColor)))
            {
                var seen=new HashSet<GalduryteEffectType>();
                for(var seed=0;seed<100;seed++)
                {
                    Assert.That(GalduryteRuntimeRules.TryRollEffects(target,color,RarityTier.Rare,11,new System.Random(seed),out var effects,out var reason),Is.True,reason);
                    Assert.That(effects.Select(e=>e.PowderColor).Distinct().Count(),Is.EqualTo(effects.Count));
                    seen.Add(effects.Single(e=>e.PowderColor==color).EffectType);
                }
                Assert.That(seen.Count,Is.EqualTo(GalduryteRuntimeRules.GetEffectPool(target).Count(e=>e.PowderColor==color)));
            }
            Assert.That(GalduryteRuntimeRules.GetEffectValue(GalduryteEffectType.CriticalDamageFlat,GalduryteEffectGrade.S),Is.EqualTo(.25f));
            Assert.That(GalduryteRuntimeRules.GetEffectValue(GalduryteEffectType.PhysicalPenetration,GalduryteEffectGrade.S),Is.EqualTo(.10f));
        }

        [Test] public void MultiHitAreaAttackRecoversOnceAndUsesOneRollForAllTargets()
        {
            var source=Unit();var a=Unit();var b=Unit();var session=new CombatSession();
            Equip(source,(GalduryteEffectType.HealOnAttack,.1f),(GalduryteEffectType.ResourceOnAttack,.1f),(GalduryteEffectType.ActionValueOnAttack,4f),(GalduryteEffectType.SlowOnAttack,1f));
            using(var action=new EquipmentActionScope(session,source,"attack",true))
            {
                EquipmentActionScope.Record(session,"attack",a,10);EquipmentActionScope.Record(session,"attack",a,10);EquipmentActionScope.Record(session,"attack",b,20);
                var rolls=0;action.Resolve(new StatusEffectSystem(),new CombatRecoverySystem(),()=>{rolls++;return 0;});
                Assert.That(rolls,Is.EqualTo(1));
                action.Resolve(new StatusEffectSystem(),new CombatRecoverySystem(),()=>throw new Exception("second proc"));
            }
            Assert.That(source.Character.CurrentHealth,Is.EqualTo(14));Assert.That(source.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            Assert.That(source.CurrentActionValue,Is.EqualTo(16));
            Assert.That(StatusEffectSystem.HasStatus(a,StatusEffectIds.Slow),Is.True);
            Assert.That(StatusEffectSystem.HasStatus(b,StatusEffectIds.Slow),Is.True);
            Assert.That(a.GetStatusTolerance(StatusEffectIds.Stun),Is.Zero);
            Assert.That(session.EquipmentActionHits,Is.Empty);
        }
        [Test] public void ActiveTurnAdvanceIsCarriedIntoNextTurn()
        {
            var source=Unit();var target=Unit();var session=new CombatSession { ActiveUnit=source };
            Equip(source,(GalduryteEffectType.ActionValueOnAttack,4.5f));source.CurrentActionValue=0;
            using(var action=new EquipmentActionScope(session,source,"active",true))
            { EquipmentActionScope.Record(session,"active",target,1);action.Resolve(new StatusEffectSystem(),new CombatRecoverySystem()); }
            Assert.That(source.PendingEquipmentActionValueReduction,Is.EqualTo(4.5f));
        }
        [Test] public void InactiveAndDestroyedSocketsDoNotGrantBonuses()
        {
            var unit=Unit();var rune=Equip(unit,(GalduryteEffectType.MagicDamagePercent,.2f));
            Assert.That(EquipmentEffectSystem.Sum(unit.Character,GalduryteEffectType.MagicDamagePercent),Is.EqualTo(.2f));
            unit.Character.Equipment.Weapon.UpgradeProgress=0;
            Assert.That(EquipmentEffectSystem.Sum(unit.Character,GalduryteEffectType.MagicDamagePercent),Is.Zero);
            unit.Character.Equipment.Weapon.UpgradeProgress=1;rune.IsDestroyed=true;
            Assert.That(EquipmentEffectSystem.Sum(unit.Character,GalduryteEffectType.MagicDamagePercent),Is.Zero);
        }
        [Test] public void FractionalPeriodicHealingAccumulatesInsteadOfRoundingToZero()
        {
            var u=Unit();Equip(u,(GalduryteEffectType.HealthPerTurn,.004f));
            for(var i=0;i<10;i++)EquipmentEffectSystem.Tick(u);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(14));
        }
        [Test] public void DungeonEnvironmentBonusesRequireConfiguredTraits()
        {
            var source=Unit();var target=Unit();var dungeon=Asset<DungeonDefinition>();
            source.Character.PassiveEffects.Add(new PassiveEffectRuntime {
                VariableModifiers=new List<GameplayVariableModifier> {
                    new() { VariableType=GameplayVariableType.DamageInEnvironment,ReferenceId="Indoor",Value=.1f },
                    new() { VariableType=GameplayVariableType.DamageInEnvironment,ReferenceId="Cold",Value=.2f }
                }
            });
            var session=new CombatSession { DungeonDefinition=dungeon };
            Assert.That(EquipmentEffectSystem.Outgoing(source,target,DamageType.Physical,session),Is.EqualTo(1f));
            Set(dungeon,"EnvironmentConfigured",true);Set(dungeon,"IsIndoor",true);Set(dungeon,"IsCold",true);
            Assert.That(EquipmentEffectSystem.Outgoing(source,target,DamageType.Physical,session),Is.EqualTo(1.3f).Within(.0001f));
        }
        [Test] public void DropBonusAffectsPositiveChanceButDoesNotEnableDisabledLoot()
        {
            var item=Asset<ItemDefinition>();Set(item,"Id","item.vs11.loot");
            var table=Asset<LootTableData>();
            var available=new LootEntryData();Set(available,"ItemDefinition",item);Set(available,"DropChance",.2f);Set(available,"Amount",new IntRange(1,1));
            var disabled=new LootEntryData();Set(disabled,"ItemDefinition",item);Set(disabled,"DropChance",0f);Set(disabled,"Amount",new IntRange(1,1));
            table.Entries.Add(available);table.Entries.Add(disabled);
            Assert.That(new LootTableSystem(()=>.4f,dropChanceBonus:.25f).RollLoot(table),Has.Count.EqualTo(1));
            Assert.That(new LootTableSystem(()=>.4f).RollLoot(table),Is.Empty);
            table.Entries.Clear();table.Entries.Add(disabled);
            Assert.That(new LootTableSystem(()=>0f,dropChanceBonus:.25f).RollLoot(table),Is.Empty);
        }
        [Test] public void JewelryAssetsCoverEveryMetalAndGem()
        {
            foreach(var slot in new[]{"Ring","Necklace","Bracelet","Earrings"})
            {
                var d=AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>("Assets/_Project/ScriptableObjects/Equipment/Jewelry/EID_"+slot+".asset");
                Assert.That(d.Metals.Count,Is.EqualTo(8));Assert.That(d.Gems.Count,Is.EqualTo(11));
                foreach(var metal in d.Metals)foreach(var gem in d.Gems)
                {
                    var item=new JewelryGenerationService().GenerateSelected(d,metal,gem);
                    Assert.That(item.ItemLevel,Is.EqualTo(10).Or.EqualTo(20));Assert.That(item.BaseValue,Is.GreaterThan(0));
                    Assert.That(item.MetalId,Is.Not.Empty);Assert.That(item.GemId,Is.Not.Empty);
                }
            }
        }
        [Test] public void DeadKingUniqueHasTwoEffectTiers()
        {
            foreach(var name in new[]{"WarriorSword","ScoutDagger","MageStaff","PaladinArmor"})
            {
                var d=AssetDatabase.LoadAssetAtPath<UniqueEquipmentDefinition>("Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BossDeadKing_"+name+".asset");
                Assert.That(d.GetEffects(10),Is.Not.Empty);Assert.That(d.GetEffects(20),Is.Not.SameAs(d.GetEffects(10)));
                Assert.That(d.GetEffects(20)[0].Operations[0].Value.NumberValue,Is.GreaterThan(d.GetEffects(10)[0].Operations[0].Value.NumberValue));
            }
        }
        [Test] public void PermanentSummonScalesToCasterLevelHasReducedMaximumAndReplacesOldest()
        {
            var definition=Asset<CharacterDefinition>();Set(definition,"Id","skeleton");Set(definition,"BaseStats",new CharacterBaseStatsData());
            var cls=Asset<CharacterClassData>();Set(cls,"Id","skeleton.class");
            var session=new CombatSession { Grid=new BattleGrid(6,6) };var caster=Unit();caster.Position=new GridPosition(3,3);caster.IsPlayerSide=true;session.Units.Add(caster);
            var system=new SummonSystem();
            Assert.That(system.TrySummonAdjacent(session,caster,definition,cls,"staff",-1,1,.25f,out var first,level:10),Is.True);
            var reference=new CharacterInstanceFactory().Create(definition,cls,10);
            Assert.That(first.Character.Level,Is.EqualTo(10));
            Assert.That(first.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(reference.Stats.MaxHealth.GetValue()*.25f).Within(1));
            Assert.That(first.IsPlayerSide,Is.True);Assert.That(first.IsPlayerControlled,Is.False);
            for(var i=0;i<100;i++)Assert.That(system.AdvanceDurationAtTurnEnd(session,first),Is.False);
            for(var x=-1;x<=1;x++)for(var y=-1;y<=1;y++)
            {
                var cell=new GridPosition(3+x,3+y);
                if((x!=0 || y!=0) && cell!=first.Position)session.Grid.SetBlocked(cell);
            }
            Assert.That(system.TrySummonAdjacent(session,caster,definition,cls,"staff",-1,1,.25f,out var second,level:10),Is.True);
            Assert.That(second.Position,Is.EqualTo(first.Position));
            Assert.That(session.Units.Contains(first),Is.False);Assert.That(session.Summons.Count,Is.EqualTo(1));
            system.RemoveSummonsCreatedBy(session,caster.Character.CharacterId);Assert.That(session.Summons,Is.Empty);
        }
    }
}
