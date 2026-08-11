using System;
using System.Linq;
using System.Text;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Debugging
{
    public static class DevConsoleUnitInspector
    {
        public static string BuildReport(
            CharacterInstance character,
            CombatSession combatSession)
        {
            if (character == null)
            {
                return "Brak postaci do podglądu.";
            }

            var output = new StringBuilder();
            var stats = character.Stats;
            var resource = character.ResourcePool;
            var battleUnit = combatSession?.Units?.FirstOrDefault(
                unit => unit?.Character == character);

            output.AppendLine(
                $"Postać: {GetDisplayName(character)} ({character.CharacterId})");

            output.AppendLine(
                $"Klasa: {character.ClassData?.ClassName ?? "brak"} | " +
                $"Poziom: {character.Level} | XP: {character.CurrentExperience} | " +
                $"Punkty umiejętności: {character.AvailableSkillPoints}");

            output.AppendLine(
                $"Stan życia: {character.LifeState} | " +
                $"Dostępność: {character.AvailabilityState} | " +
                $"Honor: {character.Honor}");
                

            output.AppendLine(
                $"HP: {character.CurrentHealth}/{GetStatValue(stats?.MaxHealth)} | " +
                $"Tarcza: {character.ShieldPool?.TotalShield ?? 0} | " +
                $"Godmode: {(character.DebugIsInvulnerable ? "ON" : "OFF")}");

            if (resource != null)
            {
                output.AppendLine(
                    $"Zasób: {resource.ResourceType} " +
                    $"{resource.CurrentValue}/{resource.MaxValue} | " +
                    $"Regeneracja: {resource.Regeneration}");
            }
            else
            {
                output.AppendLine("Zasób: brak");
            }

            if (stats != null)
            {
                output.AppendLine(
                    $"Atak: {GetStatValue(stats.MinAttack)}-" +
                    $"{GetStatValue(stats.MaxAttack)} | " +
                    $"Obrona fizyczna: {GetStatValue(stats.PhysicalDefense)} | " +
                    $"Obrona magiczna: {GetStatValue(stats.MagicalDefense)}");

                output.AppendLine(
                    $"Unik: {GetStatValue(stats.Dodge)} | " +
                    $"Kryt: {GetStatValue(stats.CritChance)}% | " +
                    $"Obrażenia krytyczne: {GetStatValue(stats.CritDamage)} | " +
                    $"Szybkość: {GetStatValue(stats.Speed)} | " +
                    $"Celność broni: {character.WeaponAccuracy}");
            }

            if (character.Resistances != null)
            {
                output.AppendLine(
                    $"Odporności: Krwawienie {character.Resistances.BleedResistance:P0} | " +
                    $"Trucizna {character.Resistances.PoisonResistance:P0} | " +
                    $"Ogień {character.Resistances.FireResistance:P0}");

                output.AppendLine(
                    $"Osłabienia {character.Resistances.WeakeningResistance:P0} | " +
                    $"Ograniczenia {character.Resistances.SuppressionResistance:P0} | " +
                    $"Przesunięcia {character.Resistances.MoveResistance:P0}");
            }

            AppendCombatData(output, battleUnit);
            AppendSkills(output, character);
            AppendPassiveEffects(output, character);
            AppendInventory(output, character);
            AppendEquipment(output, character);

            return output.ToString().TrimEnd();
        }

        private static void AppendCombatData(
            StringBuilder output,
            BattleUnit battleUnit)
        {
            if (battleUnit == null)
            {
                output.AppendLine("Walka: postać nie uczestniczy w aktywnej walce.");
                return;
            }

            output.AppendLine(
                $"Walka: {(battleUnit.IsPlayerSide ? "sojusznik" : "wróg")} | " +
                $"Pozycja: {battleUnit.Position.X}, {battleUnit.Position.Y} | " +
                $"AP: {battleUnit.CurrentActionPoints}/{battleUnit.MaxActionPoints}");

            output.AppendLine(
                $"AV: {battleUnit.CurrentActionValue} | " +
                $"Kroki ruchu: {battleUnit.MovementStepsThisTurn} | " +
                $"Reakcja: {(battleUnit.HasReacted ? "wykorzystana" : "gotowa")} | " +
                $"Gotowy do ucieczki: {(battleUnit.IsReadyToEscape ? "tak" : "nie")}");

            if (battleUnit.DownedTurnsRemaining > 0)
            {
                output.AppendLine(
                    $"Pozostałe tury powalenia: {battleUnit.DownedTurnsRemaining}/" +
                    $"{BattleUnit.DownedTurnLimit}");
            }
        }

        private static void AppendSkills(
            StringBuilder output,
            CharacterInstance character)
        {
            var skills = character.Skills?
                .Where(skill => skill != null)
                .ToList();

            if (skills == null || skills.Count == 0)
            {
                output.AppendLine("Skille: brak");
                return;
            }

            output.AppendLine(
                $"Skille ({skills.Count}): " +
                string.Join(
                    ", ",
                    skills.Select(
                        skill => $"{skill.DisplayName} [{skill.SkillId}]")));
        }

        private static void AppendPassiveEffects(
            StringBuilder output,
            CharacterInstance character)
        {
            var effects = character.PassiveEffects?
                .Where(effect => effect != null)
                .ToList();

            if (effects == null || effects.Count == 0)
            {
                output.AppendLine("Efekty pasywne/statusy: brak");
                return;
            }

            output.AppendLine($"Efekty pasywne/statusy ({effects.Count}):");

            foreach (var effect in effects)
            {
                var duration = effect.RemainingDuration > 0
                    ? effect.RemainingDuration.ToString()
                    : "stały";

                var sources = effect.SourceEntityIds == null
                    || effect.SourceEntityIds.Count == 0
                    ? "brak"
                    : string.Join(", ", effect.SourceEntityIds);

                output.AppendLine(
                    $"- {effect.EffectId} | stacki: {effect.Stacks} | " +
                    $"czas: {duration} | {GetEffectValueText(effect, character)} | " +
                    $"źródła: {sources} | modyfikatory: " +
                    $"{effect.StatModifiers?.Count ?? 0}");
            }
        }

        private static string GetEffectValueText(
            PassiveEffectRuntime effect,
            CharacterInstance character)
        {
            if (!string.Equals(
                    effect.EffectId,
                    BleedPassiveEffectProcessor.BleedId,
                    StringComparison.OrdinalIgnoreCase)
                || effect.RemainingDuration <= 0)
            {
                return $"pula obrażeń: {effect.TotalDamage}";
            }

            var rawDamage = Mathf.CeilToInt(
                effect.TotalDamage / (float)effect.RemainingDuration);

            var resistance = Mathf.Clamp01(
                character.Resistances?.BleedResistance ?? 0f);

            var multiplier = GetBleedDamageMultiplier(resistance);

            var finalDamage = Mathf.Max(
                0,
                Mathf.RoundToInt(rawDamage * multiplier));

            return $"obrażenia/turę: {finalDamage} | " +
                   $"surowe: {rawDamage} | " +
                   $"odporność: {resistance:P0} | " +
                   $"mnożnik: {multiplier:0.##}x | " +
                   $"pula: {effect.TotalDamage}";
        }

        private static float GetBleedDamageMultiplier(float resistance)
        {
            if (resistance <= 0.5f)
            {
                return 2f - 2f * resistance;
            }

            return 1.5f - resistance;
        }

        private static void AppendInventory(
            StringBuilder output,
            CharacterInstance character)
        {
            var inventory = character.Inventory;
            var itemCount = inventory?.Items?.Count ?? 0;
            var maxWeight = inventory?.MaxWeight?.ToString() ?? "bez limitu";

            output.AppendLine(
                $"Inventory: {itemCount} itemów | " +
                $"waga: {GetInventoryWeight(character)}/{maxWeight}");
        }

        private static void AppendEquipment(
            StringBuilder output,
            CharacterInstance character)
        {
            var equippedItems = character.Equipment?
                .GetEquippedItems()
                .Where(item => item?.Definition != null)
                .Select(item => item.Definition.DisplayName)
                .ToList();

            if (equippedItems == null || equippedItems.Count == 0)
            {
                output.AppendLine("Wyposażenie: brak");
                return;
            }

            output.AppendLine(
                $"Wyposażenie ({equippedItems.Count}): " +
                string.Join(", ", equippedItems));
        }

        private static string GetDisplayName(CharacterInstance character)
        {
            if (!string.IsNullOrWhiteSpace(character.Definition?.DisplayName))
            {
                return character.Definition.DisplayName;
            }

            return string.IsNullOrWhiteSpace(character.CharacterId)
                ? "bez nazwy"
                : character.CharacterId;
        }

        private static int GetStatValue(Core.Types.Stat? stat)
        {
            if (!stat.HasValue)
            {
                return 0;
            }

            return (int)Math.Round(stat.Value.GetValue());
        }

        private static float GetInventoryWeight(CharacterInstance character)
        {
            if (character.Inventory?.Items == null)
            {
                return 0f;
            }

            return character.Inventory.Items
                .Where(item => item?.Definition != null)
                .Sum(item => item.Definition.Weight);
        }
    }
}
