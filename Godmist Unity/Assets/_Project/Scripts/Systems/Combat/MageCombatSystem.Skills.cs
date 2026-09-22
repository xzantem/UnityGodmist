using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class MageCombatSystem
    {
        public static float OfferingBonus(BattleUnit u) => W(u, "superhuman_offering") switch { 1 => .35f, 2 => .4f, 3 => .5f, _ => .3f };
        public static bool ExecuteSkill(CombatSession session, BattleUnit source, BattleUnit target, SkillData skill, SkillEffectData effect, StatusEffectSystem statuses)
        {
            if (source?.Character == null || target?.Character == null) return false;
            switch (effect.EffectType)
            {
                case EffectType.MageSacrifice:
                    if (target == source) return false;
                    WarriorCombatSystem.Variable(source, target, GameplayVariableType.DamageDealtPercent, -.2f, 3, "mage.sacrifice.loss."+source.Character.CharacterId, "Poświęcenie");
                    // Each distinct donated ally adds 20%; repeated casts on that ally refresh.
                    WarriorCombatSystem.Variable(source, source, GameplayVariableType.DamageDealtPercent, .2f, 3, "mage.sacrifice.gain."+target.Character.CharacterId, "Poświęcenie");
                    return true;
                case EffectType.MageRitual:
                    var cost = Mathf.Min(Mathf.FloorToInt(target.Character.Stats.MaxHealth.GetValue() * .1f), Math.Max(0, target.Character.CurrentHealth - 1));
                    target.Character.CurrentHealth -= cost;
                    return statuses.ApplyPeriodicRecovery(source, target, skill, effect.WithPercentageValue(effect.PercentageValue / Math.Max(1, effect.DurationTurns)), 0, false);
                case EffectType.MageFireAura:
                    if (!CombatAuraSystem.Register(target, skill, effect, 0)) return false;
                    var aura = target.ActiveAuras.Last();
                    aura.Creator = source; aura.RemainingTurns = 3;
                    CombatAuraSystem.Refresh(session, statuses);
                    return true;
            }
            return false;
        }
        public static void TurnEnd(BattleUnit unit)
        {
            if (unit?.ActiveAuras == null) return;
            foreach (var aura in unit.ActiveAuras.ToArray())
            {
                if (aura.RemainingTurns <= 0) continue;
                if (--aura.RemainingTurns > 0) continue;
                var session = unit.WarriorState.Session;
                if (session != null) foreach (var target in session.Units)
                    StatusEffectSystem.RemoveStatusAppliedBySource(target, aura.StatusId, aura.AuraId);
                unit.ActiveAuras.Remove(aura);
            }
            if (Points(unit.Character, Warlock) + Points(unit.Character, Fire) > 0)
                new CharacterStatPipelineSystem().Refresh(unit.Character);
        }
    }
}
