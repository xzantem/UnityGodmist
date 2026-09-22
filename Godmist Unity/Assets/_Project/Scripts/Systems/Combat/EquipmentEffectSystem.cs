using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    // Read equipped, active sockets only. No derived state is persisted in saves.
    public static class EquipmentEffectSystem
    {
        public static IEnumerable<GalduryteEffectInstance> Effects(CharacterInstance character)
        {
            foreach (var item in character?.Equipment?.GetEquippedItems() ?? Enumerable.Empty<EquipmentItemInstance>())
                for (var i = 0; i < (item?.SocketedGaldurytes?.Count ?? 0); i++)
                {
                    var rune = item.SocketedGaldurytes[i];
                    if (!item.IsSocketActive(i) || rune?.IsIdentified != true || rune.IsDestroyed) continue;
                    foreach (var effect in rune.Effects ?? new List<GalduryteEffectInstance>())
                        if (effect != null) yield return effect;
                }
        }

        public static float Sum(CharacterInstance character, GalduryteEffectType type) =>
            Effects(character).Where(e => e.EffectType == type).Sum(e => Mathf.Max(0, e.Value));

        public static float Outgoing(BattleUnit source, BattleUnit target, DamageType type, CombatSession session)
        {
            var c = source?.Character;
            var bonus = type == DamageType.Physical ? Sum(c, GalduryteEffectType.PhysicalDamagePercent)
                : type == DamageType.Magical ? Sum(c, GalduryteEffectType.MagicDamagePercent) : 0;
            foreach (var tag in target?.Character?.Definition?.Tags ?? new List<CharacterTag>())
            {
                bonus += Sum(c, tag switch {
                    CharacterTag.Undead => GalduryteEffectType.UndeadDamagePercent,
                    CharacterTag.Human => GalduryteEffectType.HumanDamagePercent,
                    CharacterTag.Beast => GalduryteEffectType.BeastDamagePercent,
                    _ => GalduryteEffectType.DemonDamagePercent });
                bonus += new CombatVariableSystem().GetMultiplier(c, GameplayVariableType.DamageAgainstTag, tag.ToString()) - 1;
            }
            var environment = session?.DungeonDefinition;
            if (environment?.EnvironmentConfigured == true)
            {
                var variables = new CombatVariableSystem();
                bonus += variables.GetMultiplier(c, GameplayVariableType.DamageInEnvironment, environment.IsIndoor ? "Indoor" : "Outdoor") - 1;
                if (environment.IsCold) bonus += variables.GetMultiplier(c, GameplayVariableType.DamageInEnvironment, "Cold") - 1;
                if (environment.IsWarm) bonus += variables.GetMultiplier(c, GameplayVariableType.DamageInEnvironment, "Warm") - 1;
            }
            return Mathf.Max(0, 1 + bonus);
        }

        public static bool AvoidCritical(CharacterInstance character)
        {
            var chance = Mathf.Clamp01(Sum(character, GalduryteEffectType.CriticalAvoidance));
            return chance > 0 && UnityEngine.Random.value < chance;
        }

        public static float Incoming(CharacterInstance c, DamageType type)
        {
            var bonus = Sum(c, GalduryteEffectType.AllDamageReduction);
            bonus += type switch {
                DamageType.Physical => Sum(c, GalduryteEffectType.PhysicalDamageReduction),
                DamageType.Magical => Sum(c, GalduryteEffectType.MagicDamageReduction),
                DamageType.Bleed => Sum(c, GalduryteEffectType.BleedDamageReduction),
                DamageType.Poison => Sum(c, GalduryteEffectType.PoisonDamageReduction),
                DamageType.Burn => Sum(c, GalduryteEffectType.BurnDamageReduction), _ => 0 };
            return Mathf.Max(0, 1 - bonus);
        }

        public static float Resistance(CharacterInstance c, ResistanceType type) =>
            Sum(c, GalduryteEffectType.AllResistance) + Sum(c, type switch {
                ResistanceType.Bleed or ResistanceType.Poison or ResistanceType.Fire => GalduryteEffectType.DotResistance,
                ResistanceType.Suppression => GalduryteEffectType.SuppressionResistance,
                _ => GalduryteEffectType.DebuffResistance });

        public static float StatusChance(CharacterInstance c, string status) =>
            Sum(c, StatusEffectSystem.IsDamageOverTimeStatus(status) ? GalduryteEffectType.DotChance
                : StatusEffectIds.UsesSuppressionResistance(status) ? GalduryteEffectType.SuppressionChance
                : GalduryteEffectType.DebuffChance);

        public static float ResourceMultiplier(CharacterInstance c) =>
            1 + Sum(c, GalduryteEffectType.ResourceGenerationPercent)
                + new CombatVariableSystem().GetMultiplier(c, GameplayVariableType.ResourceGenerationPercent) - 1;

        public static void Tick(BattleUnit unit)
        {
            if (unit?.Character?.LifeState != CharacterLifeState.Alive) return;
            var c = unit.Character;
            var recovery = new CombatRecoverySystem();
            recovery.Heal(unit, PeriodicRecoveryPassiveEffectProcessor.GetRecoveryAmount((c.Stats?.MaxHealth.GetValue() ?? 0) * Sum(c, GalduryteEffectType.HealthPerTurn), ref c.EquipmentHealthCarry));
            recovery.RestoreResource(unit, PeriodicRecoveryPassiveEffectProcessor.GetRecoveryAmount((c.ResourcePool?.MaxValue ?? 0) * Sum(c, GalduryteEffectType.ResourcePerTurn), ref c.EquipmentResourceCarry));
        }
    }
}
