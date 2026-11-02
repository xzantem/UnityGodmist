using System;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Data.Configuration;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterLevelGrowthSystem
    {
        private readonly CharacterStatPipelineSystem statPipeline;

        public CharacterLevelGrowthSystem(CharacterStatPipelineSystem statPipeline = null)
        {
            this.statPipeline = statPipeline ?? new CharacterStatPipelineSystem();
        }

        public void Apply(CharacterInstance character, bool grantCurrentHealthGrowth)
        {
            if (character?.Definition == null || character.Stats == null) return;

            var baseStats = character.Definition.BaseStats ?? new CharacterBaseStatsData();
            var growth = character.Definition.GrowthData ?? new CharacterGrowthData();
            var multiplier = GetCumulativeGrowthMultiplier(character.Level);
            var lateMultiplier = GetLateGrowthMultiplier(character.Level);
            var earlyMultiplier = multiplier - lateMultiplier;
            var lateBlend = multiplier > 0f ? lateMultiplier / multiplier : 0f;
            var previousMaxHealth = character.MaximumHealth;

            // Restore the anchored base before accumulating growth. At full blend this
            // preserves the previous floating-point operation order, not just its algebra.
            character.Stats.MaxHealth.BaseValue = (baseStats.MaxHealth + growth.LateMaxHealthOffset * lateBlend) + growth.MaxHealth * multiplier + growth.LateMaxHealth * lateMultiplier + growth.EarlyMaxHealth * earlyMultiplier;
            if (TryEvaluateHealthCurve(growth.HealthCurve, character.Level, multiplier, out var health))
                character.Stats.MaxHealth.BaseValue = health;
            character.Stats.MinAttack.BaseValue = (baseStats.MinAttack + growth.LateMinAttackOffset * lateBlend) + growth.MinAttack * multiplier + growth.LateMinAttack * lateMultiplier + growth.EarlyMinAttack * earlyMultiplier;
            character.Stats.MaxAttack.BaseValue = (baseStats.MaxAttack + growth.LateMaxAttackOffset * lateBlend) + growth.MaxAttack * multiplier + growth.LateMaxAttack * lateMultiplier + growth.EarlyMaxAttack * earlyMultiplier;
            character.Stats.PhysicalDefense.BaseValue = baseStats.PhysicalDefense
                + growth.PhysicalDefense * multiplier;
            character.Stats.MagicalDefense.BaseValue = baseStats.MagicalDefense
                + growth.MagicalDefense * multiplier;
            if (TryEvaluateDefenseCurve(growth.DefenseCurve, character.Level, multiplier, out var physical, out var magical))
            {
                character.Stats.PhysicalDefense.BaseValue = physical;
                character.Stats.MagicalDefense.BaseValue = magical;
            }
            character.Stats.PhysicalDefensePenetration.BaseValue =
                baseStats.PhysicalDefensePenetration;
            character.Stats.MagicalDefensePenetration.BaseValue =
                baseStats.MagicalDefensePenetration;
            character.Stats.Dodge.BaseValue = baseStats.Dodge + growth.Dodge * multiplier;
            character.Stats.CritChance.BaseValue = baseStats.CritChance
                + growth.CritChance * multiplier;
            character.Stats.CritDamage.BaseValue = baseStats.CritDamage
                + growth.CritDamage * multiplier;
            character.Stats.Speed.BaseValue = baseStats.Speed + growth.Speed * multiplier;
            character.Stats.ResourceRegeneration.BaseValue = baseStats.ResourceRegeneration
                + growth.ResourceRegeneration * multiplier;
            character.Stats.WeaponAccuracy.BaseValue = baseStats.WeaponAccuracy
                + growth.WeaponAccuracy * multiplier;
            character.Stats.MaxResource.BaseValue = baseStats.MaxResource
                + growth.MaxResource * multiplier;

            // Refresh computes the effective maximum and progression-derived recovery limit
            // before clamping. An earlier nominal clamp would discard saved mana overflow.
            statPipeline.Refresh(character);

            if (!grantCurrentHealthGrowth) return;

            var currentMaxHealth = character.MaximumHealth;
            character.CurrentHealth = Mathf.Clamp(
                character.CurrentHealth + Mathf.Max(0, currentMaxHealth - previousMaxHealth),
                0,
                currentMaxHealth);
        }

        bool TryEvaluateHealthCurve(CharacterHealthGrowthPoint[] points, int level, float units, out int health)
        {
            health = 0;
            if (points == null || points.Length < 2) return false;
            var previous = 0;
            foreach (var point in points)
            {
                if (point == null || point.Level <= previous || point.Level < CharacterProgressionSystem.MinimumLevel
                    || point.Level > CharacterProgressionSystem.MaximumLevel || point.MaxHealth < 1) return false;
                previous = point.Level;
            }
            if (level <= points[0].Level) { health = points[0].MaxHealth; return true; }
            for (var i = 1; i < points.Length; i++)
            {
                if (level > points[i].Level) continue;
                var blend = Mathf.InverseLerp(GetCumulativeGrowthMultiplier(points[i - 1].Level), GetCumulativeGrowthMultiplier(points[i].Level), units);
                health = Mathf.RoundToInt(Mathf.Lerp(points[i - 1].MaxHealth, points[i].MaxHealth, blend));
                return true;
            }
            health = points[points.Length - 1].MaxHealth;
            return true;
        }

        bool TryEvaluateDefenseCurve(CharacterDefenseGrowthPoint[] points, int level, float units,
            out float physical, out float magical)
        {
            physical = magical = 0;
            if (points == null || points.Length < 2) return false;
            var previous = 0;
            foreach (var point in points)
            {
                if (point == null || point.Level <= previous || point.Level < CharacterProgressionSystem.MinimumLevel
                    || point.Level > CharacterProgressionSystem.MaximumLevel || point.PhysicalDefense < 0 || point.MagicalDefense < 0
                    || float.IsNaN(point.PhysicalDefense) || float.IsInfinity(point.PhysicalDefense)
                    || float.IsNaN(point.MagicalDefense) || float.IsInfinity(point.MagicalDefense)) return false;
                previous = point.Level;
            }
            var first = points[0];
            if (level <= first.Level)
            { physical = Mathf.Round(first.PhysicalDefense); magical = Mathf.Round(first.MagicalDefense); return true; }
            for (var i = 1; i < points.Length; i++)
            {
                if (level > points[i].Level) continue;
                var blend = Mathf.InverseLerp(GetCumulativeGrowthMultiplier(points[i - 1].Level), GetCumulativeGrowthMultiplier(points[i].Level), units);
                physical = Mathf.Round(Mathf.Lerp(points[i - 1].PhysicalDefense, points[i].PhysicalDefense, blend));
                magical = Mathf.Round(Mathf.Lerp(points[i - 1].MagicalDefense, points[i].MagicalDefense, blend));
                return true;
            }
            physical = Mathf.Round(points[points.Length - 1].PhysicalDefense);
            magical = Mathf.Round(points[points.Length - 1].MagicalDefense);
            return true;
        }

        // Optional content bonus; heroes/legacy definitions have zero late growth.
        // Reach the full late bonus at the level48 baseline. Any optional early
        // adjustment has the complementary weight and disappears at that point.
        // The square root keeps the midgame from lagging behind equipment tiers.
        public float GetLateGrowthMultiplier(int level) => GetCumulativeGrowthMultiplier(level)
            * Mathf.Sqrt(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 48f, level)));

        public float GetCumulativeGrowthMultiplier(int level)
        {
            if (level < CharacterProgressionSystem.MinimumLevel
                || level > CharacterProgressionSystem.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            var multiplier = 0f;

            for (var targetLevel = CharacterProgressionSystem.MinimumLevel + 1;
                 targetLevel <= level;
                 targetLevel++)
            {
                multiplier += GetLevelBandCoefficient(targetLevel);
            }

            return multiplier;
        }

        public int GetLevelBandCoefficient(int targetLevel)
        {
            if (targetLevel < 2 || targetLevel > CharacterProgressionSystem.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(targetLevel));
            }

            if (targetLevel <= GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandEarlyEndLevel, 10))
                return GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandEarly, 1);
            if (targetLevel <= GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandMidEndLevel, 20))
                return GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandMid, 2);
            if (targetLevel <= GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandLateEndLevel, 30))
                return GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandLate, 3);
            if (targetLevel <= GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandEndgameEndLevel, 40))
                return GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandEndgame, 5);
            return GameDefinesRuntime.GetInt(GameDefineIds.GrowthBandFinal, 9);
        }
    }
}
