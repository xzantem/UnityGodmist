using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class MageCombatSystem
    {
        public const string Warlock = "tree.mage.warlock", Fire = "tree.mage.fire";
        public const float ManaCostReductionPerPoint = .005f;
        public const float MissingManaRegenerationPerRank = .25f;
        public const int BaseManaRegeneration = 15;
        public static int Rank(CharacterInstance c, string tree, string node) => Mathf.Clamp(c?.SkillTreeProgress?
            .FirstOrDefault(p => p.TreeId == tree)?.NodeInvestments?
            .FirstOrDefault(n => n.NodeId == "node.mage." + (tree == Warlock ? "warlock." : "fire.") + node)?.Ranks ?? 0, 0, 3);
        public static int W(BattleUnit u, string n) => Rank(u?.Character, Warlock, n);
        public static int F(BattleUnit u, string n) => Rank(u?.Character, Fire, n);
        public static int Points(CharacterInstance c, string tree) => c?.SkillTreeProgress?.FirstOrDefault(p => p.TreeId == tree)?.NodeInvestments?.Sum(n => Math.Max(0, n.Ranks)) ?? 0;
        public static void RefreshConditionalStats(BattleUnit unit)
        {
            if (Points(unit?.Character, Fire) > 0) new CharacterStatPipelineSystem().Refresh(unit.Character);
        }
        public static bool Burning(BattleUnit u) => StatusEffectSystem.HasStatus(u, StatusEffectIds.Burn);
        public static float SpeedBonus(CharacterInstance c) =>
            PassiveEffectLifecycleSystem.HasActiveEffect(c, StatusEffectIds.Burn) ? 15 * Rank(c, Fire, "adrenaline") : 0;
        public static void ApplyStats(CharacterInstance c, System.Action<CharacterStatType,string,StatModifierOperation,float> observe = null)
        {
            if (c?.Stats == null) return;
            var s = c.Stats;
            var defense = Rank(c, Warlock, "defensive_power");
            s.MaxHealth.SetEffectiveValue(s.MaxHealth.GetValue() + (s.MinAttack.GetValue() + s.MaxAttack.GetValue()) * .5f * (defense switch { 1 => .1f, 2 => .3f, 3 => .5f, _ => 0f }));
            observe?.Invoke(CharacterStatType.MaxHealth,"node.mage.warlock.defensive_power",StatModifierOperation.FlatAbsolute,(s.MinAttack.GetValue()+s.MaxAttack.GetValue())*.5f*(defense switch{1=>.1f,2=>.3f,3=>.5f,_=>0f}));
            var pool = c.ResourcePool;
            var maximumMana = Mathf.Max(0, Mathf.RoundToInt(s.MaxResource.GetValue()));
            var missing = pool == null || maximumMana <= 0 ? 0f : Mathf.Clamp01(1f - pool.CurrentValue / (float)maximumMana);
            s.ResourceRegeneration.SetEffectiveValue(s.ResourceRegeneration.GetValue() * (1 + missing * MissingManaRegenerationPerRank * Rank(c, Warlock, "energy_saving")));
            observe?.Invoke(CharacterStatType.ResourceRegeneration,"node.mage.warlock.energy_saving",StatModifierOperation.Percent,missing*MissingManaRegenerationPerRank*Rank(c,Warlock,"energy_saving"));
            c.ActiveSkillLoadout.TrySetCapacityModifier("mage.memory", Rank(c, Warlock, "memory"));
            if (!PassiveEffectLifecycleSystem.HasActiveEffect(c, StatusEffectIds.Burn)) return;
            s.Speed.SetEffectiveValue(s.Speed.GetValue() + SpeedBonus(c));
            var protection = Rank(c, Fire, "protection") switch { 1 => .2f, 2 => .5f, _ => 0f };
            s.PhysicalDefense.SetEffectiveValue(s.PhysicalDefense.GetValue() * (1 + protection));
            s.MagicalDefense.SetEffectiveValue(s.MagicalDefense.GetValue() * (1 + protection));
            observe?.Invoke(CharacterStatType.Speed,"node.mage.fire.adrenaline",StatModifierOperation.FlatAbsolute,SpeedBonus(c));
            observe?.Invoke(CharacterStatType.PhysicalDefense,"node.mage.fire.protection",StatModifierOperation.Percent,protection);
            observe?.Invoke(CharacterStatType.MagicalDefense,"node.mage.fire.protection",StatModifierOperation.Percent,protection);
        }
        public static int Cost(BattleUnit u, SkillData skill, int cost)
        {
            if (skill?.ResourceType != ResourceType.Mana || u?.Character == null) return cost;
            var multiplier = Mathf.Max(0, 1 - ManaCostReductionPerPoint * Points(u.Character, Warlock));
            if (W(u, "magic_saving") > 0 && u.Character.CurrentHealth < u.Character.Stats.MaxHealth.GetValue() * .5f) multiplier *= .8f;
            return Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0, cost - 5 * W(u, "mastery")) * multiplier - .00001f));
        }
        public static SkillEffectData ResolveEffect(BattleUnit u, SkillData skill, SkillEffectData e)
        {
            if (skill?.SkillId == "skill.mage.energy_split" && e.EffectType == EffectType.DealDamage)
                e = e.WithDamageMultiplier(e.DamageMultiplier + .15f * W(u, "power_saving"));
            if (skill?.SkillId == "skill.mage.fire_arrow") e = e.WithBonusPenetration(.3f + .2f * F(u,"piercing"));
            var turns = e.DurationTurns; var chance = e.BaseChance; var dot = e.StatusDamageMultiplier;
            if (skill?.SkillId == "skill.mage.magic_focus") turns += 2 * W(u, "master_concentration");
            if (skill?.SkillId is "skill.mage.fireball" or "skill.mage.fire_aura") chance += F(u, "pyromania") switch { 1 => .1f, 2 => .25f, _ => 0 };
            if (skill?.SkillId == "skill.mage.fire_aura") dot *= 1 + .15f * F(u, "burning_aura");
            return e.WithScoutParameters(e.AdditionalHitTrials, e.BonusDamageMultiplierChance, dot, e.ModifierValue, turns).WithRuntimeParameters(e.DamageHitCount, turns, chance);
        }
        public static float DamageMultiplier(PassiveEffectCombatContext ctx,CombatValueExplanation explanation=null)
        {
            var u=ctx?.Source;
            if(u?.Character==null)return 1;
            var multiplier=1f;
            void Apply(string id,float value,bool applies=true)
            {
                multiplier*=applies?value:1;
                if(!Mathf.Approximately(value,1))explanation?.Add(id,(applies?"×":"Nie uwzględniono: ×")+value.ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL")));
            }
            if(CombatDamageSystem.HasUniqueBuff(u,UniqueBuffType.MageOffering) || W(u,"superhuman_offering")>0)
                Apply("node.mage.warlock.superhuman_offering",1+OfferingBonus(u),CombatDamageSystem.HasUniqueBuff(u,UniqueBuffType.MageOffering));
            var targets=ctx.TargetsByModeIndex?.Values.SelectMany(v=>v).Where(v=>v!=null&&v.IsPlayerSide!=u.IsPlayerSide).Distinct().Count()??1;
            Apply(Warlock,1+.01f*Points(u.Character,Warlock),targets==1);
            if(ctx.SkillId=="skill.mage.energy_orb")Apply("node.mage.warlock.dispersion",1+(W(u,"dispersion") switch{1=>.1f,2=>.175f,3=>.25f,_=>0}));
            if(ctx.SkillId is "skill.mage.fireball" or "skill.mage.fire_arrow")Apply(Fire,1+.02f*Points(u.Character,Fire));
            Apply("node.mage.fire.cauterize",1+.1f*F(u,"cauterize"),Burning(ctx.Target));
            return multiplier;
        }
        public static void BeginSkill(BattleUnit u, SkillData skill, string actionId, CombatSession session = null)
        {
            if (skill.SkillId == "skill.mage.sacrifice" && session != null)
                foreach (var unit in session.Units)
                    foreach (var effect in unit.Character.PassiveEffects.ToArray())
                        if (effect.EffectId.StartsWith("mage.sacrifice.", StringComparison.Ordinal)
                            && effect.Sources.Any(s => s.SourceCharacterId == u.Character.CharacterId))
                            new PassiveEffectLifecycleSystem().RemoveEffect(unit.Character, effect.EffectId);
            u.MageState.OverflowCritChance = 0;
            u.MageState.OverflowActionId = actionId;
            var pool = u.Character.ResourcePool;
            var rank = W(u, "energy_discharge");
            if (rank == 0 || pool == null || !skill.Effects.Any(e => e.EffectType == EffectType.DealDamage)) return;
            var excess = Math.Max(0, pool.CurrentValue - pool.MaxValue);
            u.MageState.OverflowCritChance = excess * (rank == 1 ? .002f : rank == 2 ? .006f : .01f);
            pool.Drain(excess);
        }
        public static float MagicCritChance(PassiveEffectCombatContext ctx) => ctx?.Source != null && ctx.ActionId != null && ctx.Source.MageState.OverflowActionId == ctx.ActionId
            ? ctx.Source.MageState.OverflowCritChance : 0;
        // This chance belongs to the attacking mage, never to their whole team.
        public static float BurningCriticalChance(BattleUnit attacker, BattleUnit target)
            => Burning(target) ? F(attacker, "weakness") switch { 1 => .15f, 2 => .4f, _ => 0f } : 0f;

        public static float CalculateMagicCriticalChance(PassiveEffectCombatContext context)
        {
            var chance = BurningCriticalChance(context?.Source, context?.Target);
            if(chance<=0 && MagicCritChance(context)<=0)return 0;
            chance += context?.Source?.Character?.Stats?.CritChance.GetValue() ?? 0;
            chance = PassiveEffectCombatExecutionSystem.ApplyCriticalChance(context?.Session, context, chance);
            return Mathf.Clamp01(chance + MagicCritChance(context));
        }
        public static int AdaptIncoming(BattleUnit source, BattleUnit target, int damage)
        {
            if (source?.Character == null || target == null) return damage;
            if (!target.MageState.Adaptation.TryGetValue(source.Character.CharacterId, out var entry) || entry.RemainingHits <= 0) return damage;
            entry.RemainingHits--;
            return Math.Max(0, damage - entry.Reduction);
        }
        public static void DamageTaken(BattleUnit source, BattleUnit target, int damage)
        {
            var rank = W(target, "adaptation");
            if (rank == 0 || source?.Character == null || damage < target.Character.Stats.MaxHealth.GetValue() * .2f) return;
            target.MageState.Adaptation[source.Character.CharacterId] = new MageAdaptation { RemainingHits = 3, Reduction = Mathf.RoundToInt(damage * .1f * rank) };
        }
        public static void TurnStart(CombatSession session, BattleUnit unit, CombatRecoverySystem recovery)
        {
            var rank = W(unit, "energy_sense");
            if (rank > 0)
            {
                var count = session.Units.Count(u => u.IsPlayerSide != unit.IsPlayerSide && u.Character.LifeState == CharacterLifeState.Alive && Mathf.Max(Mathf.Abs(u.Position.X-unit.Position.X),Mathf.Abs(u.Position.Y-unit.Position.Y)) <= 5);
                recovery.Heal(unit, Mathf.FloorToInt(unit.Character.Stats.MaxHealth.GetValue() * Math.Min(3, count) * .005f * rank));
            }
            if (Points(unit.Character, Warlock) + Points(unit.Character, Fire) > 0)
                new CharacterStatPipelineSystem().Refresh(unit.Character);
        }
    }
}
