using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class MageCombatSystem
    {
        const string Generation = "mage.burn.generation", FirstTick = "mage.burn.ticked", BurnDamage = "mage.burn.damage", SelfBurn = "mage.burn.self";
        public static void PrepareBurn(BattleUnit source, BattleUnit target, PassiveEffectRuntime effect)
        {
            if (effect?.EffectId != StatusEffectIds.Burn) return;
            var dot = effect.GetPayload<DamageOverTimePayload>();
            if (dot == null) return;
            dot.RemainingTotalDamage = CalculateInitialBurnTotal(source,dot.RemainingTotalDamage);
            dot.DamagePerTurn = Mathf.CeilToInt(dot.RemainingTotalDamage / (float)Math.Max(1, effect.RemainingDuration));
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer(Generation, 0));
            var rank = F(source, "fire_offering");
            if (source == null || source == target || rank == 0 || UnityEngine.Random.value >= (rank == 1 ? .15f : rank == 2 ? .3f : .5f)) return;
            var copy = CopyBurn(effect, 1f, 0);
            copy.SetStateValue(PassiveEffectRuntimeStateValue.Boolean(SelfBurn, true));
            new PassiveEffectLifecycleSystem().TryApplyTimedEffect(source.Character, copy, PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage, out _);
            new CharacterStatPipelineSystem().Refresh(source.Character);
        }
        public static int CalculateInitialBurnTotal(BattleUnit source,int total,CombatValueExplanation explanation=null)
        {
            var tree=1+.02f*Points(source?.Character,Fire);
            var arcana=1+.1f*F(source,"arcana");
            if(tree!=1)explanation?.Add(Fire,"×"+tree.ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL")));
            if(arcana!=1)explanation?.Add("node.mage.fire.arcana","×"+arcana.ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL")));
            var multiplier=tree*arcana;
            return Mathf.RoundToInt(total*multiplier);
        }

        static PassiveEffectRuntime CopyBurn(PassiveEffectRuntime parent, float strength, int generation)
        {
            var dot = parent.GetPayload<DamageOverTimePayload>();
            var child = new PassiveEffectRuntime(StatusEffectIds.Burn, parent.Sources.Select(s => new PassiveEffectSourceRuntime
                { SourceType = s.SourceType, SourceCharacterId = s.SourceCharacterId,
                  SourceId = "mage.burn.copy." + Guid.NewGuid().ToString("N") }).ToList(), 1,
                Math.Max(1, parent.RemainingDuration), 0, PassiveEffectLifetime.Timed, PassiveEffectTickPhase.TurnStart);
            child.SetPayload(new DamageOverTimePayload
            {
                RemainingTotalDamage = Mathf.FloorToInt(dot.RemainingTotalDamage * strength),
                DamagePerTurn = Mathf.FloorToInt(dot.DamagePerTurn * strength)
            });
            child.SetStateValue(PassiveEffectRuntimeStateValue.Integer(Generation, generation));
            return child;
        }
        public static BattleUnit BurnSource(CombatSession session, PassiveEffectRuntime effect)
        {
            var id = effect?.Sources?.FirstOrDefault()?.SourceCharacterId;
            return session?.Units?.FirstOrDefault(u => u.Character?.CharacterId == id);
        }
        public static int BeforeBurnTick(CombatSession session, BattleUnit target, PassiveEffectRuntime effect, int damage)
        {
            if (effect?.EffectId != StatusEffectIds.Burn) return damage;
            var source = BurnSource(session, effect);
            var generation = effect.GetStateValue(Generation)?.IntegerValue ?? 0;
            if (effect.GetStateValue(FirstTick)?.BooleanValue != true)
            {
                effect.SetStateValue(PassiveEffectRuntimeStateValue.Boolean(FirstTick, true));
                if (source != null && F(source, "aggressive_fire") > 0 && generation < 3 && effect.GetStateValue(SelfBurn)?.BooleanValue != true)
                {
                    var radius = F(source, "expansion") > 0 ? 3 : 1;
                    foreach (var enemy in session.Units.ToArray())
                    {
                        if (enemy == target || enemy.IsPlayerSide == source.IsPlayerSide || enemy.Character.LifeState != CharacterLifeState.Alive
                            || Mathf.Max(Mathf.Abs(enemy.Position.X-target.Position.X),Mathf.Abs(enemy.Position.Y-target.Position.Y)) > radius) continue;
                        if (UnityEngine.Random.value >= .5f) continue;
                        var copy = CopyBurn(effect, .5f, generation + 1);
                        if (copy.GetPayload<DamageOverTimePayload>().RemainingTotalDamage <= 0) continue;
                        new PassiveEffectLifecycleSystem().TryApplyTimedEffect(enemy.Character, copy, PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage, out _);
                    }
                }
            }
            if (damage < target.Character.Stats.MaxHealth.GetValue() * .1f)
                damage = Mathf.RoundToInt(damage * (1 + .15f * F(source, "sanguine")));
            if (damage > target.Character.Stats.MaxHealth.GetValue() * .1f && F(source, "choleric") > 0)
                new StatusEffectSystem().ApplyStatus(source, target, StatusEffectIds.Stun, F(source, "choleric") == 1 ? .1f : .25f, 2, 1, 0, 0, "mage.choleric");
            if (F(target, "fire_resistance") > 0) damage = Mathf.RoundToInt(damage * (effect.GetStateValue(SelfBurn)?.BooleanValue == true ? .5f : .75f));
            return damage;
        }
        public static void AfterBurnTick(CombatSession session, PassiveEffectRuntime effect, int actualDamage, bool expires)
        {
            if (effect?.EffectId != StatusEffectIds.Burn) return;
            var total = (effect.GetStateValue(BurnDamage)?.IntegerValue ?? 0) + Math.Max(0, actualDamage);
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Integer(BurnDamage, total));
            if (!expires || session == null || effect.GetStateValue("mage.burn.paid")?.BooleanValue == true) return;
            effect.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("mage.burn.paid", true));
            foreach (var mage in session.Units.ToArray())
            {
                var rank = F(mage, "fire_feeding");
                if (rank > 0 && mage.Character.LifeState == CharacterLifeState.Alive)
                    new CombatRecoverySystem().Heal(mage, Mathf.FloorToInt(total * .1f * rank), reportingSourceId: "mage.fire_feeding");
            }
        }
        public static void ExtinguishBurns(CombatSession session, BattleUnit unit)
        {
            foreach (var burn in unit?.Character?.PassiveEffects?.ToArray() ?? Array.Empty<PassiveEffectRuntime>())
                if (burn.EffectId == StatusEffectIds.Burn) AfterBurnTick(session, burn, 0, true);
        }
        public static float FireOutgoing(BattleUnit source, CombatSession session)
        {
            if (!Burning(source)) return 1;
            var rank = session?.Units?.Where(u => u.IsPlayerSide != source.IsPlayerSide).Select(u => F(u, "distracting_fire")).DefaultIfEmpty(0).Max() ?? 0;
            return 1 - .1f * rank;
        }
        public static void AfterHit(CombatSession session, BattleUnit source, BattleUnit target, int dealt, CombatDamageSystem damage,
            CombatFeedbackBus feedback = null, PassiveEffectCombatContext context = null)
        {
            if (source?.Character == null || target?.Character == null || dealt <= 0) return;
            if (MageTiringSpellSystem.TryApply(source, target, context))
                feedback?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect, source: source,
                    targetVfx: CombatTargetVfxKind.Fatigue, actionId: context?.PresentationActionId,
                    cueDelaySeconds: context?.PresentationDelaySeconds ?? 0));
            var erosion = F(source, "erosion");
            if (erosion > 0)
            {
                var id = source.Character.CharacterId;
                target.MageState.FireErosion.TryGetValue(id, out var hits);
                hits = Math.Min(5, hits + 1); target.MageState.FireErosion[id] = hits;
                var effect = new PassiveEffectRuntime("mage.erosion." + id, new() { PassiveEffectSourceFactory.FromRuntimeSystem(source, "mage.erosion") }, 1, 0, 0, PassiveEffectLifetime.CombatScoped, PassiveEffectTickPhase.None);
                effect.SetPayload(new ResistanceModifierPayload { ResistanceType = ResistanceType.Fire, Value = -.05f * erosion * hits });
                new PassiveEffectLifecycleSystem().TryApplyCombatScopedEffect(target.Character, effect, PassiveEffectStackingMode.ReplaceDurationAndValues, out _);
            }
            if (Burning(target) && F(source, "conduction") > 0)
                damage.ApplyDamage(session, source, target, Mathf.RoundToInt(dealt * (F(source, "conduction") == 1 ? .07f : .15f)), DamageType.Inevitable, allowWarriorProcs: false);
        }
    }
}
