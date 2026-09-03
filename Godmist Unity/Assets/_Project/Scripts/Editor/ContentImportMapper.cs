using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ContentImportMapper
    {
        private sealed class SourceRecord
        {
            public int RowNumber;
            public string Summary;
            public Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);
        }

        public static ContentImportTargetType GetDefaultTarget(string path, string sheetName)
        {
            var file = System.IO.Path.GetFileName(path) ?? string.Empty;
            if (file.IndexOf("Wyposażenie", StringComparison.OrdinalIgnoreCase) >= 0)
                return ContentImportTargetType.EquipmentPartDefinition;
            if (file.IndexOf("Przeciwnicy NPC", StringComparison.OrdinalIgnoreCase) >= 0
                && sheetName.IndexOf("Statystyki", StringComparison.OrdinalIgnoreCase) >= 0)
                return ContentImportTargetType.CharacterDefinition;
            if (file.IndexOf("Umiejętności aktywne", StringComparison.OrdinalIgnoreCase) >= 0)
                return ContentImportTargetType.SkillData;
            if (file.IndexOf("Mikstury", StringComparison.OrdinalIgnoreCase) >= 0)
                return ContentImportTargetType.ConsumableItemDefinition;
            return ContentImportTargetType.ItemDefinition;
        }

        public static List<ContentImportPreviewRow> BuildPreview(
            string sourcePath,
            ContentImportSheet sheet,
            ContentImportTargetType targetType,
            GameContentCatalog catalog)
        {
            var records = BuildSourceRecords(sourcePath, sheet);
            var result = records.Select(record => MapRecord(record, targetType, catalog)).ToList();
            MarkDuplicateIds(result);
            return result;
        }

        public static void ReevaluateStatuses(
            IReadOnlyList<ContentImportPreviewRow> rows,
            GameContentCatalog catalog)
        {
            foreach (var row in rows)
            {
                row.Errors.RemoveAll(error => error.StartsWith("ID '", StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(row.Id))
                    row.Errors.Add("ID nie może być puste.");
                else if (!Regex.IsMatch(row.Id, "^[a-z0-9]+(?:[._][a-z0-9]+)*$"))
                    row.Errors.Add($"ID '{row.Id}' ma niepoprawny format; użyj małych liter, cyfr, kropek lub podkreśleń.");
                row.ExistingAsset = FindExistingAsset(row.TargetType, row.Id, catalog, out var conflict);
                if (conflict)
                {
                    row.ChangeKind = ContentImportChangeKind.Conflict;
                    row.Selected = false;
                    row.Errors.Add($"ID '{row.Id}' koliduje z istniejącym assetem lub innym rekordem.");
                }
                else if (row.Errors.Count > 0 || row.MissingReferences.Count > 0)
                {
                    row.ChangeKind = ContentImportChangeKind.Error;
                    row.Selected = false;
                }
                else
                {
                    row.ChangeKind = row.ExistingAsset == null
                        ? ContentImportChangeKind.Create
                        : ContentImportChangeKind.Update;
                }
            }
            MarkDuplicateIds(rows);
        }

        private static ContentImportPreviewRow MapRecord(
            SourceRecord record,
            ContentImportTargetType targetType,
            GameContentCatalog catalog)
        {
            var name = Get(record.Values, "name", "display_name", "nazwa", "nazwa przedmiotu", "przedmiot", "skill");
            var id = ContentImportIdUtility.Generate(targetType, name, record.Values, record.RowNumber);
            var existing = FindExistingAsset(targetType, id, catalog, out var conflict);
            var row = new ContentImportPreviewRow(
                record.RowNumber,
                targetType,
                record.Summary,
                id,
                name,
                existing);
            row.Values["DisplayName"] = name;
            var description = Get(record.Values, "description", "tooltip", "opis");
            if (!string.IsNullOrWhiteSpace(description)) row.Values["Description"] = description;

            if (string.IsNullOrWhiteSpace(name)) row.Errors.Add("Brak nazwy rekordu.");
            if (conflict)
            {
                row.ChangeKind = ContentImportChangeKind.Conflict;
                row.Selected = false;
                row.Errors.Add($"ID '{id}' koliduje z innym typem assetu albo występuje wielokrotnie w katalogu.");
            }

            switch (targetType)
            {
                case ContentImportTargetType.ItemDefinition:
                    MapItem(record.Values, row, false, false, catalog); break;
                case ContentImportTargetType.ConsumableItemDefinition:
                    MapItem(record.Values, row, true, false, catalog); break;
                case ContentImportTargetType.CraftableItemDefinition:
                    MapItem(record.Values, row, false, true, catalog); break;
                case ContentImportTargetType.EquipmentItemDefinition:
                    MapItem(record.Values, row, false, false, catalog);
                    MapEquipment(record.Values, row, catalog); break;
                case ContentImportTargetType.EquipmentPartDefinition:
                    MapEquipmentPart(record.Values, row, catalog); break;
                case ContentImportTargetType.SkillData:
                    MapSkill(record.Values, row); break;
                case ContentImportTargetType.CharacterDefinition:
                    MapCharacter(record.Values, row, catalog); break;
            }

            if (row.Errors.Count > 0 || row.MissingReferences.Count > 0)
            {
                row.ChangeKind = row.ChangeKind == ContentImportChangeKind.Conflict
                    ? row.ChangeKind
                    : ContentImportChangeKind.Error;
                row.Selected = false;
            }
            return row;
        }

        private static void MapItem(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            bool consumable,
            bool craftable,
            GameContentCatalog catalog)
        {
            var type = Get(source, "item_type", "typ przedmiotu", "typ", "category");
            if (TryParseItemType(type, out var itemType)) row.Values["ItemType"] = itemType.ToString();
            else if (type.IndexOf("rzemios", StringComparison.OrdinalIgnoreCase) >= 0)
                row.Values["ItemType"] = ItemType.Material.ToString();
            else if (!string.IsNullOrWhiteSpace(type)) row.Warnings.Add($"Nie rozpoznano ItemType '{type}'.");

            SetInt(source, row, "BaseValue", "base_value", "cena", "price");
            SetFloat(source, row, "Weight", "weight", "waga");
            SetEnum(source, row, "BaseRarity", TryParseRarity, "rarity", "base_rarity", "unikalność");
            SetEnum(source, row, "MaterialDiscipline", TryParseDiscipline, "material_discipline", "dyscyplina");
            SetBool(source, row, "IsStackable", "stackable", "is_stackable", "unikalność");

            if (consumable)
            {
                SetEnum(source, row, "UseType", TryParseUseType, "use_type", "zastosowanie", "consumable");
                SetInt(source, row, "MaxCharges", "max_charges", "charges", "ładunki");
                if (!row.Values.ContainsKey("UseType")) row.Warnings.Add("Nie ustawiono typu użycia consumabla.");
            }

            if (craftable)
            {
                SetEnum(source, row, "CraftingDiscipline", TryParseDiscipline, "crafting_discipline", "dyscyplina");
                SetInt(source, row, "OutputAmount", "output_amount", "ilość wyjściowa", "amount");
                MapCraftingIngredients(source, row, catalog);
                if (!row.Values.ContainsKey("CraftingDiscipline"))
                    row.Warnings.Add("Nie ustawiono CraftingDiscipline.");
            }
        }

        private static void MapEquipment(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            GameContentCatalog catalog)
        {
            var type = Get(source, "equipment_type", "typ wyposażenia", "item_type", "typ");
            if (type.IndexOf("broń", StringComparison.OrdinalIgnoreCase) >= 0)
                row.Values["ItemType"] = ItemType.Weapon.ToString();
            else if (type.IndexOf("pancerz", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("armor", StringComparison.OrdinalIgnoreCase) >= 0)
                row.Values["ItemType"] = ItemType.Armor.ToString();
            ResolveClass(source, row, catalog);
        }

        private static void MapEquipmentPart(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            GameContentCatalog catalog)
        {
            ResolveClass(source, row, catalog);
            var kind = Get(source, "part", "kind", "część");
            if (TryParsePartKind(kind, out var partKind)) row.Values["Kind"] = partKind.ToString();
            else if (!string.IsNullOrWhiteSpace(kind)) row.Warnings.Add($"Nie rozpoznano EquipmentPartKind '{kind}'.");

            var level = Get(source, "level", "level_bracket", "wymagany poziom", "poziom");
            if (TryParseInt(level, out var levelValue)) row.Values["LevelBracket"] = levelValue.ToString(CultureInfo.InvariantCulture);
            else if (!string.IsNullOrWhiteSpace(level)) row.Errors.Add($"Niepoprawny level bracket '{level}'.");

            var material = Get(source, "material", "nazwa materiału", "crafting_material");
            if (!string.IsNullOrWhiteSpace(material))
            {
                var materialAsset = FindItem(material, catalog, out var ambiguous);
                if (ambiguous) row.MissingReferences.Add($"Materiał '{material}' jest niejednoznaczny.");
                else if (materialAsset == null) row.MissingReferences.Add($"Brak materiału '{material}' w aktywnym katalogu.");
                else row.Values["CraftingMaterial"] = materialAsset.Id;
                SetInt(source, row, "CraftingMaterialAmount", "material_amount", "ilość materiału", "amount");
            }

            SetRange(source, row, "MinAttackBase", "MaxAttackBase", "atak", "attack");
            SetFloat(source, row, "CritDamageBase", "obrażenia krytyczne", "crit_damage");
            SetFloat(source, row, "CritChanceBase", "szansa krytyczna", "crit_chance");
            SetFloat(source, row, "WeaponAccuracyBase", "celność", "accuracy");
            SetFloat(source, row, "MaxResourceBase", "maksymalna mana", "max_resource");
        }

        private static void MapSkill(Dictionary<string, string> source, ContentImportPreviewRow row)
        {
            SetFloat(source, row, "SkillAccuracy", "skill_accuracy", "celność", "accuracy");
            SetEnum(source, row, "AttackType", TryParseAttackType,
                "attack_type", "typ ataku", "rodzaj ataku");
            SetEnum(source, row, "PointBlankAttackType", TryParseAttackType,
                "point_blank_attack_type", "typ ataku z bliska", "typ ataku punktowego");
            SetInt(source, row, "ActionCost", "action_cost", "ap", "koszt ap");
            var resource = Get(source, "resource_cost", "resource", "koszt", "koszt zasobu");
            if (TryParseCost(resource, out var resourceCost, out var resourceType))
            {
                row.Values["ResourceCost"] = resourceCost.ToString(CultureInfo.InvariantCulture);
                row.Values["ResourceType"] = resourceType.ToString();
            }
            var actionCost = Regex.Match(resource ?? string.Empty, @"(?<amount>\d+)\s*%\s*PA", RegexOptions.IgnoreCase);
            if (actionCost.Success && int.TryParse(actionCost.Groups["amount"].Value, out var parsedActionCost))
                row.Values["ActionCost"] = parsedActionCost.ToString(CultureInfo.InvariantCulture);
            SetInt(source, row, "ResourceCost", "resource_cost", "koszt many", "koszt zasobu");
            var description = Get(source, "description", "tooltip", "opis", "efekt");
            if (!string.IsNullOrWhiteSpace(description)) row.Values["TooltipDescription"] = description;
            row.Warnings.Add("Efekty i target modes wymagają osobnych kanonicznych CSV; nie są parsowane z opisu.");
        }

        private static void MapCharacter(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            GameContentCatalog catalog)
        {
            ResolveClass(source, row, catalog);
            SetInt(source, row, "ExperienceReward", "experience_reward", "xp", "experience");
            SetInt(source, row, "GoldReward", "gold_reward", "gold");
            SetInt(source, row, "BaseStats.MaxHealth", "hp", "health", "zdrowie");
            SetInt(source, row, "BaseStats.MinAttack", "min_attack", "atk1", "atak min");
            SetInt(source, row, "BaseStats.MaxAttack", "max_attack", "atk2", "atak max");
            SetFloat(source, row, "BaseStats.PhysicalDefense", "physical_defense", "obrona fizyczna");
            SetFloat(source, row, "BaseStats.MagicalDefense", "magical_defense", "obrona magiczna");
            SetFloat(source, row, "BaseStats.Dodge", "dodge", "unik");
            SetFloat(source, row, "BaseStats.Speed", "speed", "szybkość");
            SetInt(source, row, "BaseStats.MaxResource", "max_resource", "maksymalna mana");
        }

        private static void MapCraftingIngredients(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            GameContentCatalog catalog)
        {
            var ingredients = GetExplicitIngredients(source);
            if (ingredients.Count == 0)
            {
                var recipe = Get(source, "recipe", "acquisition", "sposób uzyskania", "sposob uzyskania");
                ingredients = ParseRecipeIngredients(recipe);
            }

            if (ingredients.Count == 0)
            {
                row.Errors.Add("Craftable wymaga co najmniej jednego składnika.");
                return;
            }

            for (var index = 0; index < ingredients.Count; index++)
            {
                var ingredient = ingredients[index];
                var number = index + 1;
                var asset = FindItem(ingredient.Name, catalog, out var ambiguous);
                if (ambiguous) row.MissingReferences.Add($"Składnik '{ingredient.Name}' jest niejednoznaczny.");
                else if (asset == null) row.MissingReferences.Add($"Brak składnika '{ingredient.Name}' w aktywnym katalogu.");
                else row.Values[$"IngredientMaterial_{number}"] = asset.Id;
                row.Values[$"IngredientAmount_{number}"] = ingredient.Amount.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static List<(string Name, int Amount)> GetExplicitIngredients(Dictionary<string, string> source)
        {
            var result = new List<(string Name, int Amount)>();
            foreach (var pair in source.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                var normalized = Normalize(pair.Key);
                if (!normalized.StartsWith("ingredient", StringComparison.Ordinal)
                    && !normalized.StartsWith("skladnik", StringComparison.Ordinal)
                    && !normalized.StartsWith("material", StringComparison.Ordinal)) continue;
                if (normalized.Contains("amount") || normalized.Contains("ilos") || normalized.Contains("quantity")) continue;
                if (string.IsNullOrWhiteSpace(pair.Value)) continue;
                var amount = GetIngredientAmount(source, normalized);
                result.Add((pair.Value.Trim(), amount));
            }
            return result;
        }

        private static int GetIngredientAmount(Dictionary<string, string> source, string ingredientKey)
        {
            var suffix = ingredientKey
                .Replace("ingredient", string.Empty)
                .Replace("skladnik", string.Empty)
                .Replace("material", string.Empty);
            var candidates = new[]
            {
                $"ingredientamount{suffix}", $"ingredientilosc{suffix}", $"ingredientquantity{suffix}",
                $"skladnikilosc{suffix}", $"materialamount{suffix}", $"materialilosc{suffix}"
            };
            var amountText = source.FirstOrDefault(pair => candidates.Contains(Normalize(pair.Key))).Value;
            return TryParseInt(amountText, out var amount) && amount > 0 ? amount : 1;
        }

        private static List<(string Name, int Amount)> ParseRecipeIngredients(string recipe)
        {
            var result = new List<(string Name, int Amount)>();
            if (string.IsNullOrWhiteSpace(recipe)) return result;
            var arrow = Regex.Match(recipe, @"(?:->|→)", RegexOptions.CultureInvariant);
            var input = arrow.Success ? recipe.Substring(0, arrow.Index) : recipe;
            foreach (var part in input.Split('+'))
            {
                var match = Regex.Match(part.Trim(), @"^(?<amount>\d+)\s*x\s*(?<name>.+?)\s*$", RegexOptions.IgnoreCase);
                if (!match.Success || !int.TryParse(match.Groups["amount"].Value, out var amount)) continue;
                var name = match.Groups["name"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name)) result.Add((name, amount));
            }
            return result;
        }

        private static List<SourceRecord> BuildSourceRecords(string path, ContentImportSheet sheet)
        {
            if (sheet == null || sheet.Rows == null || sheet.Rows.Count == 0) return new List<SourceRecord>();
            var fileName = System.IO.Path.GetFileName(path) ?? string.Empty;
            if (fileName.IndexOf("Umiejętności aktywne", StringComparison.OrdinalIgnoreCase) >= 0
                && sheet.Name.Equals("Current", StringComparison.OrdinalIgnoreCase))
            {
                return BuildCurrentSkillRecords(sheet);
            }

            var header = sheet.Rows[Mathf.Clamp(sheet.HeaderRowIndex, 0, sheet.Rows.Count - 1)];
            var headers = MakeUniqueHeaders(header);
            var records = new List<SourceRecord>();
            var carry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var rowIndex = sheet.HeaderRowIndex + 1; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                var row = sheet.Rows[rowIndex];
                if (!row.Any(value => !string.IsNullOrWhiteSpace(value))) continue;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var column = 0; column < headers.Count; column++)
                {
                    var value = column < row.Count ? row[column]?.Trim() ?? string.Empty : string.Empty;
                    values[headers[column]] = value;
                }
                CopyCarryForward(values, carry, headers, "Klasa", "Class", "Przedmiot", "Item", "Część", "Part");
                records.Add(new SourceRecord
                {
                    RowNumber = rowIndex + 1,
                    Summary = string.Join(" | ", values.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Take(4)),
                    Values = values
                });
            }
            return records;
        }

        private static List<SourceRecord> BuildCurrentSkillRecords(ContentImportSheet sheet)
        {
            var records = new List<SourceRecord>();
            var maxColumns = sheet.Rows.Max(row => row.Count);
            for (var column = 0; column < maxColumns; column++)
            {
                var name = Cell(sheet.Rows, 0, column);
                if (string.IsNullOrWhiteSpace(name) || column < 2) continue;
                records.Add(new SourceRecord
                {
                    RowNumber = column + 1,
                    Summary = name,
                    Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Nazwa"] = name,
                        ["Koszt"] = Cell(sheet.Rows, 1, column),
                        ["Opis"] = Cell(sheet.Rows, 2, column),
                        ["Celność"] = Cell(sheet.Rows, 3, column)
                    }
                });
            }
            return records;
        }

        private static void CopyCarryForward(
            Dictionary<string, string> values,
            Dictionary<string, string> carry,
            IReadOnlyList<string> headers,
            params string[] aliases)
        {
            foreach (var header in headers)
            {
                if (!aliases.Any(alias => Normalize(header) == Normalize(alias))) continue;
                if (!string.IsNullOrWhiteSpace(values[header])) carry[header] = values[header];
                else if (carry.TryGetValue(header, out var previous)) values[header] = previous;
            }
        }

        private static string Cell(IReadOnlyList<IReadOnlyList<string>> rows, int row, int column) =>
            row >= 0 && row < rows.Count && column >= 0 && column < rows[row].Count
                ? rows[row][column] ?? string.Empty : string.Empty;

        private static List<string> MakeUniqueHeaders(IReadOnlyList<string> row)
        {
            var result = new List<string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < row.Count; index++)
            {
                var header = string.IsNullOrWhiteSpace(row[index]) ? $"Column{index + 1}" : row[index].Trim();
                var candidate = header;
                var suffix = 2;
                while (!used.Add(candidate)) candidate = $"{header}_{suffix++}";
                result.Add(candidate);
            }
            return result;
        }

        private static string Get(Dictionary<string, string> values, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                var match = values.FirstOrDefault(pair => Normalize(pair.Key) == Normalize(alias));
                if (!string.IsNullOrWhiteSpace(match.Value)) return match.Value;
            }
            foreach (var alias in aliases)
            {
                var normalized = Normalize(alias);
                var match = values.FirstOrDefault(pair => Normalize(pair.Key).Contains(normalized));
                if (!string.IsNullOrWhiteSpace(match.Value)) return match.Value;
            }
            return string.Empty;
        }

        private static string Normalize(string value) =>
            new string((value ?? string.Empty).Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();

        private static void SetInt(Dictionary<string, string> source, ContentImportPreviewRow row, string key, params string[] aliases)
        {
            var value = Get(source, aliases);
            if (string.IsNullOrWhiteSpace(value)) return;
            if (TryParseInt(value, out var parsed)) row.Values[key] = parsed.ToString(CultureInfo.InvariantCulture);
            else row.Errors.Add($"Pole '{key}' ma niepoprawną liczbę '{value}'.");
        }

        private static void SetFloat(Dictionary<string, string> source, ContentImportPreviewRow row, string key, params string[] aliases)
        {
            var value = Get(source, aliases);
            if (string.IsNullOrWhiteSpace(value)) return;
            if (TryParseFloat(value, out var parsed)) row.Values[key] = parsed.ToString(CultureInfo.InvariantCulture);
            else row.Errors.Add($"Pole '{key}' ma niepoprawną liczbę '{value}'.");
        }

        private static void SetBool(Dictionary<string, string> source, ContentImportPreviewRow row, string key, params string[] aliases)
        {
            var value = Get(source, aliases);
            if (string.IsNullOrWhiteSpace(value)) return;
            if (bool.TryParse(value, out var parsed)) row.Values[key] = parsed.ToString();
            else if (value.IndexOf("pospolity", StringComparison.OrdinalIgnoreCase) >= 0) row.Values[key] = "true";
        }

        private static void SetEnum<T>(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            string key,
            Func<string, (bool success, T value)> parser,
            params string[] aliases) where T : struct
        {
            var value = Get(source, aliases);
            if (string.IsNullOrWhiteSpace(value)) return;
            var parsed = parser(value);
            if (parsed.success) row.Values[key] = parsed.value.ToString();
            else row.Warnings.Add($"Nie rozpoznano wartości '{value}' dla pola {key}.");
        }

        private static void SetRange(
            Dictionary<string, string> source,
            ContentImportPreviewRow row,
            string minKey,
            string maxKey,
            params string[] aliases)
        {
            var value = Get(source, aliases);
            if (string.IsNullOrWhiteSpace(value)) return;
            var parts = Regex.Split(value.Replace(',', '.'), @"\s*[-–]\s*");
            if (parts.Length == 2 && TryParseFloat(parts[0], out var min) && TryParseFloat(parts[1], out var max))
            {
                row.Values[minKey] = min.ToString(CultureInfo.InvariantCulture);
                row.Values[maxKey] = max.ToString(CultureInfo.InvariantCulture);
            }
            else row.Warnings.Add($"Nie rozpoznano zakresu '{value}' dla pola {minKey}.");
        }

        private static bool TryParseInt(string value, out int result)
        {
            var normalized = (value ?? string.Empty).Trim().Replace(" ", string.Empty);
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.GetCultureInfo("pl-PL"), out result);
        }

        private static bool TryParseFloat(string value, out float result)
        {
            var normalized = (value ?? string.Empty).Trim().Replace("%", string.Empty).Replace(" ", "").Replace(',', '.');
            if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return false;
            if ((value ?? string.Empty).Contains("%")) result /= 100f;
            return float.IsFinite(result);
        }

        private static bool TryParseCost(string value, out int amount, out ResourceType resourceType)
        {
            amount = 0;
            resourceType = ResourceType.None;
            var match = Regex.Match(value ?? string.Empty, @"(?<amount>\d+)\s*(?<resource>PW|MP|MANA|MOMENTUM|RAGE)?", RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups["amount"].Value, out amount)) return false;
            var resource = match.Groups["resource"].Value.ToUpperInvariant();
            resourceType = resource switch
            {
                "MP" or "MANA" => ResourceType.Mana,
                "PW" or "MOMENTUM" => ResourceType.Momentum,
                "RAGE" => ResourceType.Rage,
                _ => ResourceType.None
            };
            return true;
        }

        private static bool TryParseItemType(string value, out ItemType result)
        {
            if (Enum.TryParse(value, true, out result)) return true;
            if (value.IndexOf("mater", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("rzemios", StringComparison.OrdinalIgnoreCase) >= 0) result = ItemType.Material;
            else if (value.IndexOf("broń", StringComparison.OrdinalIgnoreCase) >= 0) result = ItemType.Weapon;
            else if (value.IndexOf("pancerz", StringComparison.OrdinalIgnoreCase) >= 0) result = ItemType.Armor;
            else if (value.IndexOf("mikstur", StringComparison.OrdinalIgnoreCase) >= 0) result = ItemType.Potion;
            else { result = default; return false; }
            return true;
        }

        private static (bool success, RarityTier value) TryParseRarity(string value)
        {
            if (Enum.TryParse(value, true, out RarityTier result)) return (true, result);
            if (value.IndexOf("pospol", StringComparison.OrdinalIgnoreCase) >= 0) return (true, RarityTier.Common);
            if (value.IndexOf("rzad", StringComparison.OrdinalIgnoreCase) >= 0) return (true, RarityTier.Uncommon);
            if (value.IndexOf("unikat", StringComparison.OrdinalIgnoreCase) >= 0) return (true, RarityTier.Legendary);
            return (false, default);
        }

        private static (bool success, MaterialDiscipline value) TryParseDiscipline(string value)
        {
            if (Enum.TryParse(value, true, out MaterialDiscipline result)) return (true, result);
            if (value.IndexOf("kowal", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("smith", StringComparison.OrdinalIgnoreCase) >= 0) return (true, MaterialDiscipline.Smithing);
            if (value.IndexOf("alchem", StringComparison.OrdinalIgnoreCase) >= 0) return (true, MaterialDiscipline.Alchemy);
            if (value.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0) return (true, MaterialDiscipline.Runic);
            return (false, default);
        }

        private static (bool success, ConsumableUseType value) TryParseUseType(string value)
        {
            if (Enum.TryParse(value, true, out ConsumableUseType result)) return (true, result);
            if (value.IndexOf("mikstur", StringComparison.OrdinalIgnoreCase) >= 0) return (true, ConsumableUseType.Potion);
            if (value.IndexOf("bomba", StringComparison.OrdinalIgnoreCase) >= 0) return (true, ConsumableUseType.Bomb);
            if (value.IndexOf("utility", StringComparison.OrdinalIgnoreCase) >= 0) return (true, ConsumableUseType.Utility);
            return (false, default);
        }

        private static (bool success, AttackType value) TryParseAttackType(string value)
        {
            if (Enum.TryParse(value, true, out AttackType result)
                && Enum.IsDefined(typeof(AttackType), result))
            {
                return (true, result);
            }

            if (value.IndexOf("wręcz", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("melee", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (true, AttackType.Melee);
            }

            if (value.IndexOf("dystans", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("ranged", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("zasięg", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (true, AttackType.Ranged);
            }

            return (false, default);
        }

        private static bool TryParsePartKind(string value, out EquipmentPartKind result)
        {
            if (Enum.TryParse(value, true, out result)) return true;
            if (value.IndexOf("głown", StringComparison.OrdinalIgnoreCase) >= 0) result = EquipmentPartKind.WeaponHead;
            else if (value.IndexOf("ręko", StringComparison.OrdinalIgnoreCase) >= 0) result = EquipmentPartKind.WeaponHandle;
            else if (value.IndexOf("wiązan", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("rzemie", StringComparison.OrdinalIgnoreCase) >= 0) result = EquipmentPartKind.WeaponBinder;
            else if (value.IndexOf("napier", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("płyt", StringComparison.OrdinalIgnoreCase) >= 0) result = EquipmentPartKind.ArmorPlate;
            else if (value.IndexOf("umoc", StringComparison.OrdinalIgnoreCase) >= 0) result = EquipmentPartKind.ArmorBinder;
            else { result = default; return false; }
            return true;
        }

        private static void ResolveClass(Dictionary<string, string> source, ContentImportPreviewRow row, GameContentCatalog catalog)
        {
            var value = Get(source, "class", "class_id", "klasa", "klasa postaci", "przynależność");
            if (string.IsNullOrWhiteSpace(value)) return;
            var classData = catalog?.CharacterClasses?.FirstOrDefault(candidate => candidate != null
                && (string.Equals(candidate.Id, value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.ClassName, value, StringComparison.OrdinalIgnoreCase)));
            if (classData == null) row.MissingReferences.Add($"Brak CharacterClassData '{value}'.");
            else row.Values["RequiredClass"] = classData.Id;
        }

        private static ItemDefinition FindItem(string value, GameContentCatalog catalog, out bool ambiguous)
        {
            ambiguous = false;
            var candidates = catalog?.ItemDefinitions?.Where(item => item != null
                && (string.Equals(item.Id, value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.DisplayName, value, StringComparison.OrdinalIgnoreCase))).ToList()
                ?? new List<ItemDefinition>();
            if (candidates.Count > 1) ambiguous = true;
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static UnityEngine.Object FindExistingAsset(
            ContentImportTargetType target,
            string id,
            GameContentCatalog catalog,
            out bool conflict)
        {
            conflict = false;
            if (catalog == null || string.IsNullOrWhiteSpace(id)) return null;
            var candidates = target switch
            {
                ContentImportTargetType.ItemDefinition => catalog.ItemDefinitions?.Where(item => item != null && item.GetType() == typeof(ItemDefinition) && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                ContentImportTargetType.ConsumableItemDefinition => catalog.ItemDefinitions?.Where(item => item is ConsumableItemDefinition && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                ContentImportTargetType.CraftableItemDefinition => catalog.ItemDefinitions?.Where(item => item is CraftableItemDefinition && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                ContentImportTargetType.EquipmentItemDefinition => catalog.ItemDefinitions?.Where(item => item is EquipmentItemDefinition && item.GetType() == typeof(EquipmentItemDefinition) && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                ContentImportTargetType.EquipmentPartDefinition => catalog.EquipmentPartDefinitions?.Where(part => part != null && string.Equals(part.Id, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                ContentImportTargetType.SkillData => catalog.SkillDefinitions?.Where(skill => skill != null && string.Equals(skill.SkillId, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                ContentImportTargetType.CharacterDefinition => catalog.CharacterDefinitions?.Where(character => character != null && string.Equals(character.Id, id, StringComparison.OrdinalIgnoreCase)).Cast<UnityEngine.Object>().ToList(),
                _ => null
            } ?? new List<UnityEngine.Object>();
            var allMatches = GetCatalogObjects(catalog)
                .Where(asset => string.Equals(GetAssetId(asset), id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            conflict = candidates.Count > 1 || allMatches.Count > candidates.Count;
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static IEnumerable<UnityEngine.Object> GetCatalogObjects(GameContentCatalog catalog)
        {
            if (catalog == null) yield break;
            foreach (var item in catalog.ItemDefinitions ?? new List<ItemDefinition>()) if (item != null) yield return item;
            foreach (var part in catalog.EquipmentPartDefinitions ?? new List<EquipmentPartDefinition>()) if (part != null) yield return part;
            foreach (var skill in catalog.SkillDefinitions ?? new List<SkillData>()) if (skill != null) yield return skill;
            foreach (var character in catalog.CharacterDefinitions ?? new List<CharacterDefinition>()) if (character != null) yield return character;
        }

        private static string GetAssetId(UnityEngine.Object asset) => asset switch
        {
            ItemDefinition item => item.Id,
            EquipmentPartDefinition part => part.Id,
            SkillData skill => skill.SkillId,
            CharacterDefinition character => character.Id,
            _ => string.Empty
        } ?? string.Empty;

        private static void MarkDuplicateIds(IReadOnlyList<ContentImportPreviewRow> rows)
        {
            foreach (var group in rows.Where(row => !string.IsNullOrWhiteSpace(row.Id))
                         .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                foreach (var row in group)
                {
                    row.ChangeKind = ContentImportChangeKind.Conflict;
                    row.Selected = false;
                    if (!row.Errors.Any(error => error.StartsWith("ID '", StringComparison.OrdinalIgnoreCase)
                                                 && error.Contains("powtarza się w preview", StringComparison.OrdinalIgnoreCase)))
                        row.Errors.Add($"ID '{row.Id}' powtarza się w preview.");
                }
            }
        }
    }

    public static class ContentImportIdUtility
    {
        public static string Generate(
            ContentImportTargetType target,
            string name,
            IReadOnlyDictionary<string, string> source,
            int rowNumber)
        {
            if (TryGetExplicitId(source, out var explicitId)) return explicitId;

            var slug = target switch
            {
                ContentImportTargetType.EquipmentPartDefinition => GeneratePartSlug(source, rowNumber),
                ContentImportTargetType.EquipmentItemDefinition => Slug(string.Join(" ", new[]
                {
                    Get(source, "class", "klasa"), name
                }.Where(value => !string.IsNullOrWhiteSpace(value)))),
                _ => Slug(name)
            };
            if (string.IsNullOrWhiteSpace(slug)) slug = $"unnamed_{rowNumber}";
            return GetPrefix(target) + slug;
        }

        private static bool TryGetExplicitId(
            IReadOnlyDictionary<string, string> source,
            out string id)
        {
            id = string.Empty;
            foreach (var pair in source)
            {
                var key = Normalize(pair.Key);
                var isDuplicateIdColumn = key.StartsWith("id", StringComparison.Ordinal)
                    && key.Length > 2
                    && key.Substring(2).All(char.IsDigit);
                if ((key is not ("id" or "contentid" or "canonicalid" or "assetid")
                    && !isDuplicateIdColumn)
                    || string.IsNullOrWhiteSpace(pair.Value)) continue;

                var value = pair.Value.Trim();
                // Legacy design sheets contain a numeric `ID` column. It is a
                // row number, not a stable content identifier, so keep the
                // old generated-ID behavior for that case.
                if (value.All(char.IsDigit)) continue;
                id = value;
                return true;
            }
            return false;
        }

        private static string GeneratePartSlug(IReadOnlyDictionary<string, string> source, int rowNumber)
        {
            var classSlug = Slug(Get(source, "class", "class_id", "klasa"));
            var equipment = Get(source, "item_type", "typ wyposażenia", "typ", "item", "przedmiot");
            var equipmentSlug = equipment.IndexOf("pancerz", StringComparison.OrdinalIgnoreCase) >= 0
                                || equipment.IndexOf("armor", StringComparison.OrdinalIgnoreCase) >= 0
                ? "armor"
                : "weapon";
            var part = Get(source, "part", "kind", "część");
            var partSlug = part.IndexOf("głown", StringComparison.OrdinalIgnoreCase) >= 0 ? "head"
                : part.IndexOf("ręko", StringComparison.OrdinalIgnoreCase) >= 0 ? "handle"
                : part.IndexOf("wiązan", StringComparison.OrdinalIgnoreCase) >= 0 || part.IndexOf("rzemie", StringComparison.OrdinalIgnoreCase) >= 0 ? "binder"
                : part.IndexOf("napier", StringComparison.OrdinalIgnoreCase) >= 0 || part.IndexOf("płyt", StringComparison.OrdinalIgnoreCase) >= 0 ? "plate"
                : part.IndexOf("umoc", StringComparison.OrdinalIgnoreCase) >= 0 ? "binder"
                : Slug(part);
            var rarity = Slug(Get(source, "rarity", "base_rarity", "rzadkość", "unikalność"));
            if (string.IsNullOrWhiteSpace(rarity)) rarity = "common";
            var material = Slug(Get(source, "material", "nazwa materiału", "crafting_material"));
            var segments = new[] { classSlug, equipmentSlug, partSlug, rarity, material }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var result = string.Join(".", segments);
            return string.IsNullOrWhiteSpace(result) ? $"unnamed_{rowNumber}" : result;
        }

        private static string GetPrefix(ContentImportTargetType target) => target switch
        {
            ContentImportTargetType.ConsumableItemDefinition => "item.consumable.",
            ContentImportTargetType.CraftableItemDefinition => "item.craftable.",
            ContentImportTargetType.EquipmentItemDefinition => "equipment.",
            ContentImportTargetType.EquipmentPartDefinition => "part.",
            ContentImportTargetType.SkillData => "skill.",
            ContentImportTargetType.CharacterDefinition => "character.",
            _ => "item."
        };

        private static string Slug(string value)
        {
            var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
                else if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
            }
            return builder.ToString().Trim('_');
        }

        private static string Get(IReadOnlyDictionary<string, string> source, params string[] aliases)
        {
            foreach (var pair in source)
                if (aliases.Any(alias => Normalize(pair.Key) == Normalize(alias)) && !string.IsNullOrWhiteSpace(pair.Value))
                    return pair.Value;
            return string.Empty;
        }

        private static string Normalize(string value) =>
            new string((value ?? string.Empty).Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }
}
