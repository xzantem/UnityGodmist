using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed class EquipmentActionScope : IDisposable
    {
        private readonly CombatSession session;
        private readonly BattleUnit source;
        private readonly string actionId;
        public EquipmentActionScope(CombatSession session, BattleUnit source, string actionId, bool enabled)
        {
            this.session = session; this.source = source; this.actionId = actionId;
            if (enabled && !session.EquipmentActionHits.ContainsKey(actionId)) session.EquipmentActionHits[actionId] = new Dictionary<BattleUnit, int>();
        }
        public static void Record(CombatSession session, string id, BattleUnit target, int damage)
        {
            if (string.IsNullOrEmpty(id) || session?.EquipmentActionHits.TryGetValue(id, out var hits) != true) return;
            hits.TryGetValue(target, out var previous);
            hits[target] = previous + Mathf.Max(0, damage);
        }
        public void Resolve(StatusEffectSystem statuses, CombatRecoverySystem recovery, Func<float> random = null)
        {
            if (!session.EquipmentActionHits.TryGetValue(actionId, out var hits)) return;
            if (session.ScheduledEffects?.Any(e => e?.ActionId == actionId) == true) return;
            session.EquipmentActionHits.Remove(actionId);
            if (hits.Count == 0 || source.Character.LifeState != CharacterLifeState.Alive) return;
            random ??= () => UnityEngine.Random.value;
            var totalDamage = hits.Values.Sum();
            var c = source.Character;
            recovery.Heal(source, Mathf.FloorToInt(totalDamage * EquipmentEffectSystem.Sum(c, GalduryteEffectType.HealOnAttack)));
            recovery.RestoreResource(source, Mathf.FloorToInt((c.ResourcePool?.MaxValue ?? 0) * EquipmentEffectSystem.Sum(c, GalduryteEffectType.ResourceOnAttack)));
            var advance = EquipmentEffectSystem.Sum(c, GalduryteEffectType.ActionValueOnAttack);
            if (session.ActiveUnit == source) source.PendingEquipmentActionValueReduction += advance;
            else source.CurrentActionValue = Mathf.Max(0, source.CurrentActionValue - advance);
            foreach (var group in EquipmentEffectSystem.Effects(c).GroupBy(e => e.EffectType))
            {
                var status = group.Key switch {
                    GalduryteEffectType.BleedOnAttack => StatusEffectIds.Bleed,
                    GalduryteEffectType.PoisonOnAttack => StatusEffectIds.Poison,
                    GalduryteEffectType.BurnOnAttack => StatusEffectIds.Burn,
                    GalduryteEffectType.StunOnAttack => StatusEffectIds.Stun,
                    GalduryteEffectType.FreezeOnAttack => StatusEffectIds.Freeze,
                    GalduryteEffectType.SlowOnAttack => StatusEffectIds.Slow, _ => null };
                if (status == null) continue;
                var roll = random();
                foreach (var target in hits.Keys)
                    statuses?.ApplyStatus(source, target, status, Mathf.Clamp01(group.Sum(e => e.Value)),
                        3, 1, .6f, 0, "equipment.galduryte." + group.Key, sharedRoll: roll);
            }
            CombatTurnSystem.ReorderParticipants(session);
        }
        public void Dispose()
        {
            if (session.ScheduledEffects?.Any(e => e?.ActionId == actionId) != true)
                session.EquipmentActionHits.Remove(actionId);
        }
    }
}
