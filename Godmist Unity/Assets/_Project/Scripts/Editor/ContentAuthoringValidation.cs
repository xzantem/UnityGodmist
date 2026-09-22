using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class ContentAuthoringValidationReport
    {
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool IsValid => Errors.Count == 0;

        public ContentAuthoringValidationReport(
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
            Warnings = (warnings ?? Enumerable.Empty<string>()).ToList();
        }
    }

    public static partial class ContentAuthoringValidator
    {
        public static ContentAuthoringValidationReport Validate(
            UnityEngine.Object asset,
            GameContentCatalog activeCatalog = null)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (asset == null)
            {
                errors.Add("Nie wybrano assetu.");
                return new ContentAuthoringValidationReport(errors, warnings);
            }

            switch (asset)
            {
                case EquipmentPartDefinition part:
                    ValidatePart(part, errors, warnings);
                    ValidateDuplicateId(part, activeCatalog, errors);
                    break;
                case ItemDefinition item:
                    ValidateItem(item, errors, warnings);
                    ValidateDuplicateId(item, activeCatalog, errors);
                    break;
                case SkillData skill:
                    ValidateSkill(skill, errors, warnings);
                    ValidateDuplicateId(skill, activeCatalog, errors);
                    break;
                case SkillTreeDefinition tree:
                    ValidateSkillTree(tree, errors, warnings);
                    break;
                case CharacterDefinition character:
                    ValidateCharacter(character, errors, warnings);
                    ValidateDuplicateId(character, activeCatalog, errors);
                    break;
                case EncounterDefinition encounter:
                    ValidateEncounter(encounter, errors, warnings);
                    ValidateDuplicateId(encounter, activeCatalog, errors);
                    break;
                case DungeonDefinition dungeon:
                    ValidateDungeon(dungeon, errors, warnings);
                    ValidateDuplicateId(dungeon, activeCatalog, errors);
                    break;
                default:
                    errors.Add("Wybrany asset nie jest obsługiwanym contentem.");
                    break;
            }

            return new ContentAuthoringValidationReport(errors, warnings);
        }

        private static void ValidateItem(
            ItemDefinition item,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(item.Id, "Id", errors);
            RequireText(item.DisplayName, "DisplayName", errors);

            if (!Enum.IsDefined(typeof(Core.Enums.ItemType), item.ItemType))
            {
                errors.Add("ItemType ma nieprawidłową wartość.");
            }

            if (!Enum.IsDefined(typeof(Core.Enums.RarityTier), item.BaseRarity))
            {
                errors.Add("BaseRarity ma nieprawidłową wartość.");
            }

            if (item.BaseValue < 0)
            {
                errors.Add("BaseValue nie może być ujemne.");
            }

            if (!IsFiniteNonNegative(item.Weight))
            {
                errors.Add("Weight musi być skończoną liczbą nieujemną.");
            }

            if (item.ItemType == Core.Enums.ItemType.Material
                && item.MaterialDiscipline == Core.Enums.MaterialDiscipline.None)
            {
                warnings.Add("Materiał nie ma przypisanej MaterialDiscipline.");
            }

            if (item is ConsumableItemDefinition consumable)
            {
                if (!Enum.IsDefined(typeof(Core.Enums.ConsumableUseType), consumable.UseType))
                {
                    errors.Add("UseType ma nieprawidłową wartość.");
                }

                if (consumable.MaxCharges < 1)
                {
                    errors.Add("MaxCharges musi być większe od zera.");
                }

                if (consumable.UseType == Core.Enums.ConsumableUseType.Potion
                    && item.ItemType != Core.Enums.ItemType.Potion)
                {
                    errors.Add("Potion musi mieć ItemType = Potion.");
                }

                if (consumable.UseType == Core.Enums.ConsumableUseType.Bomb
                    && item.ItemType != Core.Enums.ItemType.Bomb)
                {
                    errors.Add("Bomba musi mieć ItemType = Bomb.");
                }

                if (consumable.UseType == Core.Enums.ConsumableUseType.Utility
                    && consumable.MaxCharges != 1)
                {
                    warnings.Add("Utility consumable powinien mieć dokładnie jedną charge.");
                }
            }

            if (item is CraftableItemDefinition craftable)
            {
                ValidateCraftable(craftable, errors, warnings);
            }

            if (item is EquipmentItemDefinition equipment)
            {
                ValidateEquipment(equipment, errors, warnings);
            }
        }

        private static void ValidateCraftable(
            CraftableItemDefinition craftable,
            List<string> errors,
            List<string> warnings)
        {
            if (craftable.CraftingDiscipline == Core.Enums.MaterialDiscipline.None)
            {
                errors.Add("CraftingDiscipline musi być ustawione dla receptury.");
            }

            if (craftable.OutputAmount < 1)
            {
                errors.Add("OutputAmount musi być większe od zera.");
            }

            if (craftable.Ingredients == null || craftable.Ingredients.Count == 0)
            {
                errors.Add("Receptura musi mieć co najmniej jeden składnik.");
                return;
            }

            var materials = new HashSet<ItemDefinition>();
            for (var index = 0; index < craftable.Ingredients.Count; index++)
            {
                var ingredient = craftable.Ingredients[index];
                if (ingredient == null || ingredient.Material == null)
                {
                    errors.Add($"Składnik {index + 1} nie ma przypisanego materiału.");
                    continue;
                }

                if (ingredient.Amount < 1)
                {
                    errors.Add($"Ilość składnika {index + 1} musi być większa od zera.");
                }

                if (ingredient.Material.ItemType != Core.Enums.ItemType.Material)
                {
                    errors.Add($"Składnik {index + 1} musi być itemem typu Material.");
                }

                if (!materials.Add(ingredient.Material))
                {
                    errors.Add($"Materiał '{ingredient.Material.Id}' występuje w recepturze więcej niż raz.");
                }

                if (ReferenceEquals(ingredient.Material, craftable))
                {
                    errors.Add("Receptura nie może używać samej siebie jako składnika.");
                }
            }

            if (craftable.CalculatedBaseValue <= 0)
            {
                warnings.Add("Calculated Base Value wynosi zero; sprawdź wartości składników.");
            }
        }

        private static void ValidateEquipment(
            EquipmentItemDefinition equipment,
            List<string> errors,
            List<string> warnings)
        {
            if (equipment.ItemType != Core.Enums.ItemType.Weapon
                && equipment.ItemType != Core.Enums.ItemType.Armor
                && equipment.ItemType != Core.Enums.ItemType.Jewelry)
            {
                errors.Add("Equipment musi mieć ItemType = Weapon, Armor albo Jewelry.");
            }

            if (equipment is JewelryItemDefinition jewelry
                && jewelry.JewelrySlot is not Core.Enums.EquipmentSlot.Necklace
                    and not Core.Enums.EquipmentSlot.Ring
                    and not Core.Enums.EquipmentSlot.Bracelet
                    and not Core.Enums.EquipmentSlot.Earrings)
            {
                errors.Add("JewelrySlot musi wskazywać slot biżuterii.");
            }

            if (equipment.RequiredClass == null
                && equipment.ItemType is Core.Enums.ItemType.Weapon or Core.Enums.ItemType.Armor)
            {
                warnings.Add("Equipment nie ma przypisanej klasy postaci.");
            }

            if (equipment is UniqueEquipmentDefinition unique)
            {
                ValidatePassives(unique.Effects, "Effects", errors, warnings);
                ValidatePassives(unique.EffectsLevel20, "EffectsLevel20", errors, warnings);
            }
        }

        private static void ValidatePart(
            EquipmentPartDefinition part,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(part.Id, "Id", errors);
            RequireText(part.DisplayName, "DisplayName", errors);

            if (!Enum.IsDefined(typeof(Core.Enums.EquipmentPartKind), part.Kind))
            {
                errors.Add("Kind ma nieprawidłową wartość.");
            }

            if (!Enum.IsDefined(typeof(Core.Enums.EquipmentPartLevelBracket), part.LevelBracket))
            {
                errors.Add("LevelBracket ma nieprawidłową wartość.");
            }

            if (part.Stats == null)
            {
                errors.Add("EquipmentPartDefinition musi mieć obiekt Stats.");
            }
            else
            {
                foreach (var value in GetStats(part.Stats))
                {
                    if (!float.IsFinite(value))
                    {
                        errors.Add("Statystyki części muszą być skończonymi liczbami.");
                        break;
                    }
                }
            }

            if (part.RequiredClass == null)
            {
                warnings.Add("Część equipmentu nie ma przypisanej klasy postaci.");
            }

            if (part.CraftingMaterials == null || part.CraftingMaterials.Count == 0)
            {
                warnings.Add("Część equipmentu nie ma materiałów craftingu.");
                return;
            }

            for (var index = 0; index < part.CraftingMaterials.Count; index++)
            {
                var material = part.CraftingMaterials[index];
                if (material == null || material.Material == null)
                {
                    errors.Add($"Materiał części {index + 1} nie jest przypisany.");
                }
                else if (material.Amount < 1)
                {
                    errors.Add($"Ilość materiału części {index + 1} musi być większa od zera.");
                }
            }
        }

        private static void ValidateSkill(
            SkillData skill,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(skill.SkillId, "SkillId", errors);
            RequireText(skill.DisplayName, "DisplayName", errors);
            if (!IsFiniteNonNegative(skill.SkillAccuracy))
                errors.Add("SkillAccuracy musi być skończoną liczbą nieujemną.");
            if (!Enum.IsDefined(typeof(Core.Enums.AttackType), skill.AttackType))
                errors.Add("AttackType ma nieprawidłową wartość.");
            if (!Enum.IsDefined(typeof(Core.Enums.AttackType), skill.PointBlankAttackType))
                errors.Add("PointBlankAttackType ma nieprawidłową wartość.");
            if (skill.ActionCost < 0) errors.Add("ActionCost nie może być ujemne.");
            if (skill.ResourceCost < 0) errors.Add("ResourceCost nie może być ujemne.");
            if (skill.KnifeCost < 0) errors.Add("KnifeCost nie może być ujemne.");
            if (skill.ToggleUpkeepFlatResourceCost < 0)
                errors.Add("ToggleUpkeepFlatResourceCost nie może być ujemne.");
            if (!float.IsFinite(skill.ToggleUpkeepMaxResourcePercent)
                || skill.ToggleUpkeepMaxResourcePercent < 0f
                || skill.ToggleUpkeepMaxResourcePercent > 1f)
            {
                errors.Add("ToggleUpkeepMaxResourcePercent musi być w zakresie 0-1.");
            }
            if (!IsObjectPlacementOnlySkill(skill)
                && (skill.TargetModes == null || skill.TargetModes.Count == 0))
                warnings.Add("Skill nie ma zdefiniowanych trybów celu.");
            if (skill.Effects == null || skill.Effects.Count == 0)
                warnings.Add("Skill nie ma zdefiniowanych efektów.");
            if (skill.IsToggle && !ToggleSkillRules.IsValid(skill))
                warnings.Add("Skill oznaczony jako toggle nie spełnia reguł toggle.");
        }

        private static void ValidateSkillTree(
            SkillTreeDefinition tree,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(tree.Id, "Id", errors);
            RequireText(tree.DisplayName, "DisplayName", errors);

            if (tree.Nodes == null || tree.Nodes.Count == 0)
            {
                warnings.Add("Skill tree nie ma zdefiniowanych node'ów.");
            }
            else
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < tree.Nodes.Count; index++)
                {
                    var node = tree.Nodes[index];
                    if (node == null)
                    {
                        errors.Add($"Nodes[{index}] nie może być puste.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                        errors.Add($"Nodes[{index}].Id musi być niepuste i unikalne.");
                    if (node.Tier < 1 || node.Tier > 7)
                        errors.Add($"Nodes[{index}].Tier musi być w zakresie 1-7.");
                    if (node.Column < 1 || node.Column > 5)
                        errors.Add($"Nodes[{index}].Column musi być w zakresie 1-5.");
                    if (node.MaxRanks < 1 || node.MaxRanks > 3)
                        errors.Add($"Nodes[{index}].MaxRanks musi być w zakresie 1-3.");

                    ValidatePassives(node.PassiveEffects, $"Nodes[{index}].PassiveEffects", errors, warnings);
                }
            }

            ValidatePassives(
                tree.GlobalPassive?.PassivesPerPoint,
                "GlobalPassive.PassivesPerPoint",
                errors,
                warnings);
        }

        private static void ValidatePassives(
            IEnumerable<Godmist.Data.Effects.PassiveEffectDefinition> passives,
            string field,
            List<string> errors,
            List<string> warnings)
        {
            if (passives == null)
            {
                warnings.Add($"{field} nie została zainicjalizowana.");
                return;
            }

            var index = 0;
            foreach (var passive in passives)
            {
                if (passive == null)
                {
                    errors.Add($"{field}[{index}] nie może być pusta.");
                    index++;
                    continue;
                }

                foreach (var error in PassiveEffectDefinitionValidator.Validate(passive).Errors)
                    errors.Add($"{field}[{index}]: {error}");
                index++;
            }
        }

        private static void ValidateCharacter(
            CharacterDefinition character,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(character.Id, "Id", errors);
            RequireText(character.DisplayName, "DisplayName", errors);
            if (!IsFiniteNonNegative(character.IdleAnimationFramesPerSecond))
                errors.Add("IdleAnimationFramesPerSecond musi być skończoną liczbą nieujemną.");
            if (!IsFiniteNonNegative(character.RunAnimationFramesPerSecond))
                errors.Add("RunAnimationFramesPerSecond musi być skończoną liczbą nieujemną.");
            if (character.ExperienceReward < 0) errors.Add("ExperienceReward nie może być ujemne.");
            if (character.GoldReward < 0) errors.Add("GoldReward nie może być ujemne.");
            if (character.BaseStats == null) errors.Add("CharacterDefinition musi mieć BaseStats.");
            if (character.BaseResistances == null) warnings.Add("Character nie ma BaseResistances.");
            if (character.GrowthData == null) warnings.Add("Character nie ma GrowthData.");

            var characterTags = new HashSet<Core.Enums.CharacterTag>();
            foreach (var tag in character.Tags ?? new List<Core.Enums.CharacterTag>())
            {
                if (!Enum.IsDefined(typeof(Core.Enums.CharacterTag), tag))
                {
                    errors.Add($"CharacterDefinition ma nieznany tag: {tag}.");
                }
                else if (!characterTags.Add(tag))
                {
                    errors.Add($"CharacterDefinition ma zduplikowany tag: {tag}.");
                }
            }
        }

        private static bool IsObjectPlacementOnlySkill(SkillData skill)
        {
            return skill?.Effects != null
                && skill.Effects.Count > 0
                && skill.Effects.All(effect => effect?.EffectType == Core.Enums.EffectType.CreateObject);
        }

        private static void ValidateEncounter(
            EncounterDefinition encounter,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(encounter.Id, "Id", errors);
            RequireText(encounter.DisplayName, "DisplayName", errors);
            if (encounter.GridWidth < 1) errors.Add("GridWidth musi być większe od zera.");
            if (encounter.GridHeight < 1) errors.Add("GridHeight musi być większe od zera.");
            if (encounter.BossEnemyIndex < 0) errors.Add("BossEnemyIndex nie może być ujemne.");
            if (encounter.Enemies == null || encounter.Enemies.Count == 0)
            {
                warnings.Add("Encounter nie ma jeszcze zdefiniowanych przeciwników.");
                if (encounter.BossEnemyIndex != 0)
                    errors.Add("BossEnemyIndex wskazuje poza pustą listę przeciwników.");
            }
            else if (encounter.BossEnemyIndex >= encounter.Enemies.Count)
            {
                errors.Add("BossEnemyIndex wskazuje poza listę przeciwników.");
            }
        }

        private static void ValidateDungeon(
            DungeonDefinition dungeon,
            List<string> errors,
            List<string> warnings)
        {
            RequireText(dungeon.Id, "Id", errors);
            RequireText(dungeon.DisplayName, "DisplayName", errors);
            if (dungeon.DungeonLevel < 1 || dungeon.DungeonLevel > 50)
                errors.Add("DungeonLevel musi być w zakresie 1-50.");
            ValidateRange(dungeon.MinFloorCount, dungeon.MaxFloorCount, "FloorCount", errors);
            ValidateRange(dungeon.MinRoomCountPerFloor, dungeon.MaxRoomCountPerFloor, "RoomCountPerFloor", errors);
            ValidateRange(dungeon.MinOptionalRoomCount, dungeon.MaxOptionalRoomCount, "OptionalRoomCount", errors);
            ValidateRange(dungeon.MinLoopConnections, dungeon.MaxLoopConnections, "LoopConnections", errors);
            ValidateRange(dungeon.MinProgressionGateCount, dungeon.MaxProgressionGateCount, "ProgressionGateCount", errors);
            ValidateRange(dungeon.MinEncounterCountPerFloor, dungeon.MaxEncounterCountPerFloor, "EncounterCountPerFloor", errors);
            ValidateRange(dungeon.MinObjectCountPerFloor, dungeon.MaxObjectCountPerFloor, "ObjectCountPerFloor", errors);
            if (dungeon.IsProcedural && dungeon.LayoutProfile == null && dungeon.RoomTemplateCatalog == null)
                warnings.Add("Procedural dungeon nie ma profilu layoutu ani katalogu template'ów.");
            if (dungeon.EncounterDefinitions == null || dungeon.EncounterDefinitions.Count == 0)
                warnings.Add("Dungeon nie ma przypisanych encounterów.");
        }

        private static void ValidateRange(int minimum, int maximum, string name, List<string> errors)
        {
            if (minimum < 0) errors.Add($"{name} minimum nie może być ujemne.");
            if (maximum < minimum) errors.Add($"{name} maximum nie może być mniejsze od minimum.");
        }

        private static IEnumerable<float> GetStats(EquipmentPartStats stats)
        {
            return new[]
            {
                stats.MinAttackBase, stats.MaxAttackBase, stats.CritChanceBase,
                stats.CritDamageBase, stats.WeaponAccuracyBase, stats.MaxResourceBase,
                stats.ResourceRegenerationBase, stats.MaxHealthBase, stats.DodgeBase,
                stats.PhysicalDefenseBase, stats.MagicalDefenseBase,
                stats.MinAttackPercent, stats.MaxAttackPercent, stats.CritChancePercent,
                stats.CritDamagePercent, stats.WeaponAccuracyPercent, stats.MaxResourcePercent,
                stats.ResourceRegenerationPercent, stats.MaxHealthPercent, stats.DodgePercent,
                stats.PhysicalDefensePercent, stats.MagicalDefensePercent
            };
        }

        private static void ValidateDuplicateId(
            UnityEngine.Object asset,
            GameContentCatalog catalog,
            List<string> errors)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(GetId(asset))) return;

            var duplicateItems = catalog.ItemDefinitions?.Count(item =>
                item != null
                && !ReferenceEquals(item, asset)
                && string.Equals(item.Id, GetId(asset), StringComparison.Ordinal)) ?? 0;
            var duplicateParts = catalog.EquipmentPartDefinitions?.Count(part =>
                part != null
                && !ReferenceEquals(part, asset)
                && string.Equals(part.Id, GetId(asset), StringComparison.Ordinal)) ?? 0;

            var duplicateSkills = catalog.SkillDefinitions?.Count(skill =>
                skill != null
                && !ReferenceEquals(skill, asset)
                && string.Equals(skill.SkillId, GetId(asset), StringComparison.Ordinal)) ?? 0;
            var duplicateCharacters = catalog.CharacterDefinitions?.Count(character =>
                character != null
                && !ReferenceEquals(character, asset)
                && string.Equals(character.Id, GetId(asset), StringComparison.Ordinal)) ?? 0;
            var duplicateEncounters = catalog.EncounterDefinitions?.Count(encounter =>
                encounter != null
                && !ReferenceEquals(encounter, asset)
                && string.Equals(encounter.Id, GetId(asset), StringComparison.Ordinal)) ?? 0;
            var duplicateDungeons = catalog.DungeonDefinitions?.Count(dungeon =>
                dungeon != null
                && !ReferenceEquals(dungeon, asset)
                && string.Equals(dungeon.Id, GetId(asset), StringComparison.Ordinal)) ?? 0;

            if (duplicateItems + duplicateParts + duplicateSkills + duplicateCharacters
                + duplicateEncounters + duplicateDungeons > 0)
            {
                errors.Add($"Id '{GetId(asset)}' jest już używane w aktywnym GameContentCatalog.");
            }
        }

        private static string GetId(UnityEngine.Object asset)
        {
            return asset switch
            {
                ItemDefinition item => item.Id,
                EquipmentPartDefinition part => part.Id,
                SkillData skill => skill.SkillId,
                CharacterDefinition character => character.Id,
                EncounterDefinition encounter => encounter.Id,
                DungeonDefinition dungeon => dungeon.Id,
                _ => string.Empty
            } ?? string.Empty;
        }

        private static void RequireText(
            string value,
            string name,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} nie może być puste.");
        }

        private static bool IsFiniteNonNegative(float value) =>
            float.IsFinite(value) && value >= 0f;
    }
}
