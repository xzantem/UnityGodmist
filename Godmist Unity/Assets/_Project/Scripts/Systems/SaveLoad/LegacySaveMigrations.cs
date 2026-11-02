using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;

namespace Godmist.Systems.SaveLoad
{
    // Restore-only migration stages. Gameplay recalculation must never infer historical data.
    // Equipment baseline is recovered before item recalculation; progression after tree restoration;
    // skill aliases before known-skill/loadout validation; shield duration before layer insertion.
    public static class LegacySaveMigrations
    {
        public static bool RecoverEquipmentBaseline(EquipmentItemInstance item)
        {
            if (item == null || !item.UsesEquipmentProgression || item.BaseStatModifiers?.Count > 0
                || item.StatModifiers == null || item.StatModifiers.Count == 0) return false;
            var primaryFactor = 1f / (1f + Math.Clamp(item.UpgradeProgress, 0f, 1f));
            item.BaseStatModifiers = item.StatModifiers.Where(value => value != null).Select(value => new StatModifier {
                StatType = value.StatType, Operation = value.Operation, Stacking = value.Stacking,
                Value = value.Value * (item.IsPrimaryStat(value.StatType) ? primaryFactor : 1f) }).ToList();
            return true;
        }

        public static bool RecoverSkillPointTotal(CharacterInstance character)
        {
            if (character == null || character.Level < 2) return false;
            var invested = character.SkillTreeProgress?.Sum(tree => tree?.NodeInvestments?.Sum(node => Math.Max(0, node?.Ranks ?? 0)) ?? 0) ?? 0;
            if (invested + character.AvailableSkillPoints != character.Level - 1) return false;
            character.AvailableSkillPoints++;
            return true;
        }

        public static string KnownSkillId(string classId, string savedId) => classId == "class.warrior" && savedId == "skill.warrior.bandage"
            ? "skill.warrior.bleeding_destruction" : savedId;

        public static string LoadoutSkillId(string classId, IReadOnlyList<string> originalSkillIds, string savedId)
        {
            if (classId != "class.warrior" || originalSkillIds?.Contains("skill.warrior.bandage") != true) return savedId;
            return savedId == "skill.warrior.bandage" ? "skill.warrior.bleeding_destruction"
                : savedId == "skill.warrior.bleeding_destruction" ? WarriorDischargeRules.SkillId : savedId;
        }

        public static int MageShieldDuration(int savedTurns) => savedTurns == 0 ? CombatShieldSystem.MageShieldDuration
            : Math.Min(savedTurns, CombatShieldSystem.MageShieldDuration);
    }
}
