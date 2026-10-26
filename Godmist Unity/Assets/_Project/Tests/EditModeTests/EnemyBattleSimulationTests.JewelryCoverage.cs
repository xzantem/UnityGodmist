using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string JewelryAuditFolder => Path.GetFullPath("../LocalHandoffs/jewelry-coverage-balance-20261019");
        [Serializable] sealed class JewelryAuditTier
        { public int level; public float hp, atk, dotResistance, speed; public string metal, speedMetal, range; }
        [Serializable] sealed class JewelryAuditInput
        { public JewelryAuditTier[] tiers; public float commonGem, bossGem; }
        [Serializable] sealed class JewelryAuditPair
        {
            public string enemy, kind, biome; public int hp;
            public double incoming, incomingJewelry, outgoing, outgoingJewelry, outgoingCommon, outgoingBoss;
        }
        [Serializable] sealed class JewelryAuditRow
        {
            public int level, hero, profile, weaponTier, armorTier, jewelryLevel;
            public float upgrade, hp, jewelryHp, min, max, jewelryMin, jewelryMax, speed, jewelrySpeed, dotResistance;
            public double probability;
            public List<JewelryAuditPair> pairs = new();
        }
        [Serializable] sealed class JewelryAuditReport
        {
            public string policy = "Analytical x1 ATK indices, no AI or battle simulation. Workbook jewelry projection; production assets unchanged. HP/ATK/speed metals; bracelet is one selected DoT resistance, not generic mitigation. Two full sets on four heroes, uniformly over six legal pairs, aggregated after export. Common gems and boss gems are separate scenarios.";
            public List<JewelryAuditRow> rows = new();
        }
        static readonly Dictionary<(int,int,float,float,float,float,float,float,float,float),double> jewelryAuditDamageCache = new();

        // This projection mirrors PointExpected but keeps the jewelry theme bonus before
        // defense and environment bonus after defense, as in CombatDamageSystem. It never rolls.
        static double JewelryAuditExpected(BattleUnit source, BattleUnit target, DamageType kind, float theme = 1, float environment = 1)
        {
            var skill = source.Character.GetBasicAttackSkill();
            target.ScoutState.Facing = new Vector2Int(Math.Sign(source.Position.X-target.Position.X), Math.Sign(source.Position.Y-target.Position.Y));
            var session = new CombatSession(); session.Units.Add(source); session.Units.Add(target);
            var ctx = new PassiveEffectCombatContext(session, source, target, skillId: skill.SkillId, skill: skill,
                targetsByModeIndex: new Dictionary<int,IReadOnlyList<BattleUnit>> {{0,new[]{target}}},
                attackType: skill.ResolveAttackType(4), attackPosition: AttackPosition.Front, damageType: kind, distance: 4);
            var s = source.Character.Stats; var t = target.Character.Stats;
            var hit = kind == DamageType.Physical ? CombatDamageSystem.ApplyHitChanceBonus(source.Character,
                CombatDamageSystem.CalculateHitChance(source.Character.WeaponAccuracy,100,t.Dodge.GetValue())) : 1f;
            var crit = kind == DamageType.Physical ? Mathf.Clamp01((PassiveEffectCombatExecutionSystem.ApplyCriticalChance(session,ctx,s.CritChance.GetValue())
                + ScoutCombatSystem.CriticalChance(ctx))*WarriorCombatSystem.CriticalMultiplier(source,target)
                * MageCombatSystem.CritReceived(session,target,source)*PaladinCombatSystem.CriticalMultiplier(source,target)) : 0f;
            var critical = Mathf.Max(1,s.CritDamage.GetValue()+ScoutCombatSystem.WeakPointCriticalDamage(target));
            var pre = ScoutCombatSystem.DamageMultiplier(ctx)*WarriorCombatSystem.DamageMultiplier(ctx)
                * MageCombatSystem.DamageMultiplier(ctx)*PaladinCombatSystem.DamageMultiplier(ctx);
            var vars = new CombatVariableSystem();
            var post = vars.GetMultiplier(source.Character,GameplayVariableType.DamageDealtPercent)
                * vars.GetMultiplier(target.Character,GameplayVariableType.DamageReceivedPercent)
                * ScoutCombatSystem.ReceivedDamageMultiplier(source,target)*WarriorCombatSystem.Received(session,source,target,kind)
                * MageCombatSystem.FireOutgoing(source,session);
            var penetration = (kind == DamageType.Physical ? s.PhysicalDefensePenetration.GetValue() : s.MagicalDefensePenetration.GetValue())
                + PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(source,target,kind);
            var defense = (kind == DamageType.Physical ? t.PhysicalDefense.GetValue() : t.MagicalDefense.GetValue())
                * WarriorCombatSystem.DefenseMultiplier(target,session)/(1+penetration);
            var low = Mathf.Max(0,Mathf.RoundToInt(s.MinAttack.GetValue())); var high = Mathf.Max(low,Mathf.RoundToInt(s.MaxAttack.GetValue()));
            var key = (low,high,hit,crit,critical,pre,post,defense,theme,environment);
            if (jewelryAuditDamageCache.TryGetValue(key,out var cached)) return cached;
            int Mitigate(int raw) => defense <= 0 ? raw : Mathf.CeilToInt((float)raw*raw/(raw+defense));
            int Result(int raw) => Mathf.RoundToInt(Mitigate(Mathf.RoundToInt(raw*theme))*post*environment);
            double total = 0;
            for (var raw = low; raw <= high; raw++)
            {
                var value = Mathf.RoundToInt(raw*pre);
                total += (1-crit)*Result(value)+crit*Result(Mathf.CeilToInt(value*critical));
            }
            cached = hit*total/(high-low+1); jewelryAuditDamageCache[key] = cached;
            Assert.That(double.IsFinite(cached) && cached>0,Is.True); return cached;
        }

        static void JewelryAuditEquip(CharacterInstance hero, EquipmentSlot slot, string asset, params StatModifier[] modifiers)
        {
            var definition = AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>(Root+"Equipment/Jewelry/"+asset+".asset");
            Assert.That(definition,Is.Not.Null);
            // A transient item carries the workbook's higher-tier values; no asset,
            // authored definition, inventory or save is edited to simulate missing content.
            hero.Equipment.SetItem(slot,new JewelryItemInstance(definition)
            { BaseStatModifiers=modifiers.ToList(),StatModifiers=modifiers.ToList() });
        }
        static StatModifier JewelryAuditStat(CharacterStatType type,float value) => new()
        { StatType=type,Operation=StatModifierOperation.FlatBase,Stacking=StatModifierStacking.Additive,Value=value };

        [Test,Explicit("Read-only analytic workbook jewelry projection on two out of four heroes; zero simulated battles.")]
        [Timeout(600000)]
        public void AuditJewelryCoverageBalance()
            => WriteJewelryCoverageAudit(false);

        [Test,Explicit("Complete the analytical L1 upgrade bounds only; no battles or production mutations.")]
        public void AuditJewelryStarterUpgradeBounds()
            => WriteJewelryCoverageAudit(true);

        static void WriteJewelryCoverageAudit(bool starterOnly)
        {
            var input = JsonUtility.FromJson<JewelryAuditInput>(File.ReadAllText(Path.Combine(JewelryAuditFolder,"input.json")));
            var gearInput = JsonUtility.FromJson<PointGearInput>(File.ReadAllText(Path.Combine(JewelryAuditFolder,"gear-input-all-levels.json")));
            var report = starterOnly
                ? JsonUtility.FromJson<JewelryAuditReport>(File.ReadAllText(Path.Combine(JewelryAuditFolder,"export.json")))
                : new JewelryAuditReport();
            if (starterOnly) report.rows.RemoveAll(r=>r.level==1);
            var profiles = new[]{1,0,1,0};
            foreach (var level in starterOnly ? new[]{1} : Enumerable.Range(1,48))
            {
                var enemies = PointEnemies(level); var tier = input.tiers.LastOrDefault(t=>t.level<=level);
                File.AppendAllText(Path.Combine(JewelryAuditFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" L"+level+Environment.NewLine);
                foreach (var gear in gearInput.rows.Where(g=>g.level==level))
                foreach (var upgrade in level==1 && !starterOnly ? new[]{0f} : new[]{0f,.5f,1f})
                {
                    var profile = profiles[gear.hero]; var hero = PointHero(gear,profile,PointBudget(level),upgrade,enemies);
                    var unit = PointUnit(hero,true); var s = hero.Stats;
                    var row = new JewelryAuditRow { level=level,hero=gear.hero,profile=profile,weaponTier=gear.weaponTier,
                        armorTier=gear.armorTier,probability=gear.probability,upgrade=upgrade,jewelryLevel=tier?.level??0,
                        hp=s.MaxHealth.GetValue(),min=s.MinAttack.GetValue(),max=s.MaxAttack.GetValue(),speed=s.Speed.GetValue(),dotResistance=tier?.dotResistance??0 };
                    foreach (var enemy in enemies)
                    {
                        var kind = PointDamageType(enemy.Character);
                        var pair = new JewelryAuditPair { enemy=enemy.Character.Definition.Id,kind=kind.ToString(),hp=enemy.Character.CurrentHealth,
                            biome=enemy.Character.Definition.Id.StartsWith("enemy.forest.")?"forest":"catacombs",
                            incoming=PointExpected(enemy,unit,kind),outgoing=PointExpected(unit,enemy,PointDamageType(hero)) };
                        Assert.That(JewelryAuditExpected(unit,enemy,PointDamageType(hero)),Is.EqualTo(pair.outgoing).Within(1e-7));
                        row.pairs.Add(pair);
                    }
                    if (tier != null)
                    {
                        JewelryAuditEquip(hero,EquipmentSlot.Necklace,"EID_Necklace",JewelryAuditStat(CharacterStatType.MaxHealth,tier.hp));
                        JewelryAuditEquip(hero,EquipmentSlot.Ring,"EID_Ring",JewelryAuditStat(CharacterStatType.MinAttack,tier.atk),JewelryAuditStat(CharacterStatType.MaxAttack,tier.atk));
                        JewelryAuditEquip(hero,EquipmentSlot.Earrings,"EID_Earrings",JewelryAuditStat(CharacterStatType.Speed,tier.speed));
                        JewelryAuditEquip(hero,EquipmentSlot.Bracelet,"EID_Bracelet");
                        foreach (var item in hero.Equipment.GetEquippedItems().OfType<JewelryItemInstance>())
                        {
                            item.RequiredLevel=item.ItemLevel=tier.level;
                            Assert.That(item.RequiredLevel,Is.LessThanOrEqualTo(hero.Level));
                        }
                        PointRefresh(hero);
                        Assert.That(s.Speed.GetValue()-row.speed,Is.EqualTo(tier.speed).Within(.001f));
                        // At higher budgets the Fire preset also invests in Warlock:
                        // defensive_power converts part of the ring's ATK into extra HP.
                        Assert.That(s.MaxHealth.GetValue()-row.hp,Is.GreaterThanOrEqualTo(tier.hp-.01f));
                    }
                    row.jewelryHp=s.MaxHealth.GetValue();row.jewelryMin=s.MinAttack.GetValue();row.jewelryMax=s.MaxAttack.GetValue();row.jewelrySpeed=s.Speed.GetValue();
                    foreach (var enemy in enemies)
                    {
                        var pair = row.pairs.Single(p=>p.enemy==enemy.Character.Definition.Id);
                        pair.incomingJewelry=PointExpected(enemy,unit,PointDamageType(enemy.Character));
                        pair.outgoingJewelry=JewelryAuditExpected(unit,enemy,PointDamageType(hero));
                        pair.outgoingCommon=JewelryAuditExpected(unit,enemy,PointDamageType(hero),1,tier==null?1:1+4*input.commonGem);
                        pair.outgoingBoss=JewelryAuditExpected(unit,enemy,PointDamageType(hero),tier==null?1:1+4*input.bossGem);
                        Assert.That(pair.outgoingCommon,Is.GreaterThanOrEqualTo(pair.outgoingJewelry));
                    }
                    report.rows.Add(row);
                }
            }
            File.WriteAllText(Path.Combine(JewelryAuditFolder,"export.json"),JsonUtility.ToJson(report,true));
            Assert.That(report.rows,Is.Not.Empty);
        }
    }
}
