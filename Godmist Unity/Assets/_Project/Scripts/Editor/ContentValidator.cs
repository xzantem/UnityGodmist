using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.Quests;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public enum ContentValidationSeverity
    {
        Error,
        Warning,
        Info
    }

    public sealed class ContentValidationIssue
    {
        public ContentValidationSeverity Severity { get; }
        public UnityEngine.Object Asset { get; }
        public string FieldPath { get; }
        public string Message { get; }
        public string AssetPath => Asset == null ? string.Empty : AssetDatabase.GetAssetPath(Asset);

        public ContentValidationIssue(
            ContentValidationSeverity severity,
            UnityEngine.Object asset,
            string fieldPath,
            string message)
        {
            Severity = severity;
            Asset = asset;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public void SelectAsset()
        {
            if (Asset == null) return;
            Selection.activeObject = Asset;
            EditorGUIUtility.PingObject(Asset);
        }

        public override string ToString()
        {
            var owner = Asset == null ? string.Empty : Asset.name;
            var field = string.IsNullOrWhiteSpace(FieldPath) ? string.Empty : $" [{FieldPath}]";
            return $"{Severity}: {owner}{field} {Message}".Trim();
        }
    }

    public sealed class ContentProjectValidationReport
    {
        public IReadOnlyList<ContentValidationIssue> Issues { get; }
        public IReadOnlyList<ContentValidationIssue> Errors { get; }
        public IReadOnlyList<ContentValidationIssue> Warnings { get; }
        public IReadOnlyList<ContentValidationIssue> Infos { get; }
        public int ScannedAssetCount { get; }
        public bool IsValid => Errors.Count == 0;

        public ContentProjectValidationReport(
            IEnumerable<ContentValidationIssue> issues,
            int scannedAssetCount)
        {
            Issues = (issues ?? Enumerable.Empty<ContentValidationIssue>()).ToList();
            Errors = Issues.Where(issue => issue.Severity == ContentValidationSeverity.Error).ToList();
            Warnings = Issues.Where(issue => issue.Severity == ContentValidationSeverity.Warning).ToList();
            Infos = Issues.Where(issue => issue.Severity == ContentValidationSeverity.Info).ToList();
            ScannedAssetCount = scannedAssetCount;
        }
    }

    public static partial class ContentAuthoringValidator
    {
        private const string ContentRoot = "Assets/_Project/ScriptableObjects";

        private static readonly string[] ContentSearchFilters =
        {
            "t:CharacterDefinition",
            "t:CharacterClassData",
            "t:ItemDefinition",
            "t:ConsumableItemDefinition",
            "t:CraftableItemDefinition",
            "t:EquipmentItemDefinition",
            "t:LootBagItemDefinition",
            "t:EquipmentPartDefinition",
            "t:SkillData",
            "t:SkillTreeDefinition",
            "t:QuestDefinition",
            "t:NpcDefinition",
            "t:DialogueDefinition",
            "t:DungeonDefinition",
            "t:EncounterDefinition",
            "t:DungeonLootTableCatalog",
            "t:DungeonRoomTemplateCatalog",
            "t:DungeonRoomTemplateDefinition",
            "t:LootTableData",
            "t:ChestDungeonObjectDefinition",
            "t:ConditionDungeonObjectDefinition",
            "t:DoorDungeonObjectDefinition",
            "t:LeverDungeonObjectDefinition",
            "t:ObstacleDungeonObjectDefinition",
            "t:SpikeTrapDungeonObjectDefinition"
        };

        private static readonly IEqualityComparer<UnityEngine.Object> ObjectReferenceComparer =
            new UnityObjectReferenceComparer();

        public static ContentProjectValidationReport ValidateProject(GameContentCatalog activeCatalog)
        {
            return ValidateProject(activeCatalog, FindProjectContentAssets());
        }

        public static ContentProjectValidationReport ValidateProject(
            GameContentCatalog activeCatalog,
            IEnumerable<UnityEngine.Object> projectAssets)
        {
            var assets = (projectAssets ?? Enumerable.Empty<UnityEngine.Object>())
                .Where(IsKnownContentAsset)
                .Distinct(ObjectReferenceComparer)
                .ToList();
            var collector = new Collector();

            if (activeCatalog == null)
            {
                collector.Error(null, "Aktywny katalog", "Nie wybrano GameContentCatalog.");
            }
            else
            {
                ValidateCatalog(activeCatalog, collector);
            }

            foreach (var asset in assets)
            {
                ValidateKnownAsset(asset, collector);
                ValidateDanglingObjectReferences(asset, collector);
            }

            if (activeCatalog != null)
            {
                ValidateDanglingObjectReferences(activeCatalog, collector);
                ValidateDuplicateIds(assets, collector);
                ValidateCatalogMembership(assets, activeCatalog, collector);
                ValidateEquipmentCompatibility(assets, activeCatalog, collector);
            }
            else
            {
                ValidateDuplicateIds(assets, collector);
            }

            ValidateDeadAssets(assets, activeCatalog, collector);
            collector.Info(null, string.Empty,
                $"Przeskanowano {assets.Count} assetów contentu.");
            return new ContentProjectValidationReport(collector.Issues, assets.Count);
        }

        private static IEnumerable<UnityEngine.Object> FindProjectContentAssets()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filter in ContentSearchFilters)
            {
                foreach (var guid in AssetDatabase.FindAssets(filter, new[] { ContentRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrWhiteSpace(path)) paths.Add(path);
                }
            }

            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (IsKnownContentAsset(asset)) yield return asset;
            }
        }

        private static void ValidateKnownAsset(UnityEngine.Object asset, Collector collector)
        {
            if (asset is EquipmentPartDefinition
                || asset is ItemDefinition
                || asset is SkillData
                || asset is CharacterDefinition
                || asset is EncounterDefinition
                || asset is DungeonDefinition)
            {
                var singleReport = Validate(asset);
                foreach (var error in singleReport.Errors)
                    collector.Error(asset, InferField(error), error);
                foreach (var warning in singleReport.Warnings)
                    collector.Warning(asset, InferField(warning), warning);
            }

            switch (asset)
            {
                case CharacterClassData classData:
                    ValidateCharacterClass(classData, collector);
                    break;
                case QuestDefinition quest:
                    ValidateQuest(quest, collector);
                    break;
                case NpcDefinition npc:
                    ValidateNpc(npc, collector);
                    break;
                case DialogueDefinition dialogue:
                    ValidateDialogue(dialogue, collector);
                    break;
                case SkillTreeDefinition tree:
                    ValidateSkillTree(tree, collector);
                    break;
                case LootTableData lootTable:
                    ValidateLootTable(lootTable, collector);
                    break;
                case DungeonLootTableCatalog lootCatalog:
                    ValidateDungeonLootCatalog(lootCatalog, collector);
                    break;
                case DungeonRoomTemplateCatalog roomCatalog:
                    ValidateRoomTemplateCatalog(roomCatalog, collector);
                    break;
                case DungeonRoomTemplateDefinition roomTemplate:
                    ValidateRoomTemplate(roomTemplate, collector);
                    break;
                case DungeonObjectDefinition dungeonObject:
                    ValidateDungeonObject(dungeonObject, collector);
                    break;
                case LootBagItemDefinition lootBag:
                    ValidateLootBag(lootBag, collector);
                    break;
            }
        }

        private static void ValidateCatalog(GameContentCatalog catalog, Collector collector)
        {
            ValidateCatalogList(catalog, "CharacterDefinitions", catalog.CharacterDefinitions, collector);
            ValidateCatalogList(catalog, "CharacterClasses", catalog.CharacterClasses, collector);
            ValidateCatalogList(catalog, "ItemDefinitions", catalog.ItemDefinitions, collector);
            ValidateCatalogList(catalog, "EquipmentPartDefinitions", catalog.EquipmentPartDefinitions, collector);
            ValidateCatalogList(catalog, "SkillDefinitions", catalog.SkillDefinitions, collector);
            ValidateCatalogList(catalog, "QuestDefinitions", catalog.QuestDefinitions, collector);
            ValidateCatalogList(catalog, "NpcDefinitions", catalog.NpcDefinitions, collector);
            ValidateCatalogList(catalog, "DungeonDefinitions", catalog.DungeonDefinitions, collector);
            ValidateCatalogList(catalog, "EncounterDefinitions", catalog.EncounterDefinitions, collector);
        }

        private static void ValidateCatalogList<T>(
            GameContentCatalog catalog,
            string field,
            List<T> values,
            Collector collector)
            where T : UnityEngine.Object
        {
            if (values == null)
            {
                collector.Error(catalog, field, "Lista katalogu nie może być nullem.");
                return;
            }

            var seen = new HashSet<T>(new UnityObjectReferenceComparer<T>());
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                if (value == null)
                {
                    collector.Error(catalog, $"{field}[{index}]",
                        "Katalog zawiera brakującą albo usuniętą referencję.");
                    continue;
                }

                if (!seen.Add(value))
                {
                    collector.Error(catalog, $"{field}[{index}]",
                        "Ten sam asset występuje w katalogu więcej niż raz.");
                }
            }
        }

        private static void ValidateDuplicateIds(
            IEnumerable<UnityEngine.Object> assets,
            Collector collector)
        {
            foreach (var group in assets
                         .Select(asset => new { Asset = asset, Id = GetProjectId(asset) })
                         .Where(value => !string.IsNullOrWhiteSpace(value.Id))
                         .GroupBy(value => value.Id, StringComparer.Ordinal))
            {
                if (group.Count() < 2) continue;
                foreach (var value in group)
                {
                    collector.Error(value.Asset, GetProjectIdField(value.Asset),
                        $"Id '{group.Key}' jest zduplikowane w projekcie contentu.");
                }
            }
        }

        private static void ValidateCatalogMembership(
            IEnumerable<UnityEngine.Object> assets,
            GameContentCatalog catalog,
            Collector collector)
        {
            foreach (var asset in assets.Where(IsCatalogOwnedAsset))
            {
                if (IsAssignedToCatalog(asset, catalog)) continue;
                collector.Warning(asset, "GameContentCatalog",
                    "Asset nie jest przypisany do aktywnego GameContentCatalog.");
            }
        }

        private static void ValidateEquipmentCompatibility(
            IEnumerable<UnityEngine.Object> assets,
            GameContentCatalog catalog,
            Collector collector)
        {
            var parts = (catalog.EquipmentPartDefinitions ?? new List<EquipmentPartDefinition>())
                .Where(part => part != null)
                .ToList();
            foreach (var equipment in assets.OfType<EquipmentItemDefinition>())
            {
                if (equipment.RequiredClass == null) continue;
                var requiredKinds = equipment.ItemType switch
                {
                    ItemType.Weapon => new[]
                    {
                        EquipmentPartKind.WeaponHead, EquipmentPartKind.WeaponBinder,
                        EquipmentPartKind.WeaponHandle
                    },
                    ItemType.Armor => new[]
                    {
                        EquipmentPartKind.ArmorPlate, EquipmentPartKind.ArmorBinder,
                        EquipmentPartKind.ArmorBase
                    },
                    _ => Array.Empty<EquipmentPartKind>()
                };

                foreach (var kind in requiredKinds)
                {
                    if (parts.Any(part => part.RequiredClass == equipment.RequiredClass
                                          && part.Kind == kind)) continue;
                    collector.Error(equipment, "RequiredClass / ItemType",
                        $"Brak części '{kind}' dla klasy '{equipment.RequiredClass.Id}'.");
                }
            }
        }

        private static void ValidateCharacterClass(CharacterClassData value, Collector collector)
        {
            RequireText(value.Id, value, "Id", collector);
            RequireText(value.ClassName, value, "ClassName", collector);
            if (!Enum.IsDefined(typeof(ResourceType), value.ResourceType)
                || value.ResourceType == ResourceType.None)
                collector.Error(value, "ResourceType", "Klasa musi mieć poprawny typ zasobu.");
            ValidateListNulls(value, "SkillTrees", value.SkillTrees, collector, false);
        }

        private static void ValidateQuest(QuestDefinition quest, Collector collector)
        {
            RequireText(quest.Id, quest, "Id", collector);
            RequireText(quest.DisplayName, quest, "DisplayName", collector);
            var stages = quest.Stages ?? new List<QuestStageDefinition>();
            if (stages.Count == 0)
                collector.Warning(quest, "Stages", "Quest nie ma zdefiniowanych stage'y.");

            if (!string.IsNullOrWhiteSpace(quest.StartStageId)
                && stages.All(stage => stage == null || stage.StageId != quest.StartStageId))
                collector.Error(quest, "StartStageId", "StartStageId wskazuje na nieistniejący stage.");

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in stages)
            {
                if (stage == null)
                {
                    collector.Error(quest, "Stages", "Lista quest stages zawiera pusty wpis.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(stage.StageId))
                    collector.Error(quest, "Stages.StageId", "StageId nie może być puste.");
                else if (!stageIds.Add(stage.StageId))
                    collector.Error(quest, "Stages.StageId", $"StageId '{stage.StageId}' występuje więcej niż raz.");

                var objectives = stage.Objectives ?? new List<QuestObjectiveDefinition>();
                var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var objective in objectives)
                {
                    if (objective == null)
                    {
                        collector.Error(quest, "Stages.Objectives", "Objective nie może być nullem.");
                        continue;
                    }
                    if (!objectiveIds.Add(objective.ObjectiveId))
                        collector.Error(quest, "Stages.Objectives.ObjectiveId",
                            $"ObjectiveId '{objective.ObjectiveId}' występuje więcej niż raz w stage.");
                    if (objective.RequiredAmount < 1)
                        collector.Error(quest, "Stages.Objectives.RequiredAmount",
                            "RequiredAmount musi być większe od zera.");
                }

                foreach (var transition in stage.Transitions ?? new List<QuestStageTransitionDefinition>())
                {
                    if (transition == null) continue;
                    if (!string.IsNullOrWhiteSpace(transition.NextStageId)
                        && stages.All(candidate => candidate == null || candidate.StageId != transition.NextStageId))
                        collector.Error(quest, "Stages.Transitions.NextStageId",
                            $"NextStageId '{transition.NextStageId}' wskazuje na nieistniejący stage.");
                }
            }
        }

        private static void ValidateNpc(NpcDefinition npc, Collector collector)
        {
            RequireText(npc.Id, npc, "Id", collector);
            RequireText(npc.DisplayName, npc, "DisplayName", collector);
            if (npc.BaseDialogue == null)
                collector.Warning(npc, "BaseDialogue", "NPC nie ma przypisanego dialogu bazowego.");
            ValidateListNulls(npc, "Roles", npc.Roles, collector, false);
        }

        private static void ValidateDialogue(DialogueDefinition dialogue, Collector collector)
        {
            RequireText(dialogue.Id, dialogue, "Id", collector);
            var nodes = dialogue.Nodes ?? new List<DialogueNodeData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    collector.Error(dialogue, "Nodes", "Lista dialogu zawiera pusty node.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.NodeId) || !ids.Add(node.NodeId))
                    collector.Error(dialogue, "Nodes.NodeId", "NodeId musi być niepuste i unikalne.");
            }
            if (!string.IsNullOrWhiteSpace(dialogue.StartNodeId)
                && nodes.All(node => node == null || node.NodeId != dialogue.StartNodeId))
                collector.Error(dialogue, "StartNodeId", "StartNodeId wskazuje na nieistniejący node.");

            foreach (var node in nodes.Where(node => node != null))
            {
                foreach (var choice in node.Choices ?? new List<DialogueChoiceData>())
                {
                    if (choice == null || string.IsNullOrWhiteSpace(choice.NextNodeId)) continue;
                    if (nodes.All(candidate => candidate == null || candidate.NodeId != choice.NextNodeId))
                        collector.Error(dialogue, "Nodes.Choices.NextNodeId",
                            $"NextNodeId '{choice.NextNodeId}' wskazuje na nieistniejący node.");
                }
            }
        }

        private static void ValidateSkillTree(SkillTreeDefinition tree, Collector collector)
        {
            RequireText(tree.Id, tree, "Id", collector);
            RequireText(tree.DisplayName, tree, "DisplayName", collector);
            var nodes = tree.Nodes ?? new List<SkillTreeNodeDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    collector.Error(tree, "Nodes", "Skill tree zawiera pusty node.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                    collector.Error(tree, "Nodes.Id", "Node Id musi być niepuste i unikalne.");
                if (node.Tier < 1 || node.Tier > 7)
                    collector.Error(tree, "Nodes.Tier", "Tier musi być w zakresie 1-7.");
                if (node.Column < 1 || node.Column > 5)
                    collector.Error(tree, "Nodes.Column", "Column musi być w zakresie 1-5.");
                if (node.MaxRanks < 1 || node.MaxRanks > 3)
                    collector.Error(tree, "Nodes.MaxRanks", "MaxRanks musi być w zakresie 1-3.");
                ValidatePassiveDefinitions(tree, node.PassiveEffects, $"Nodes[{node.Id}].PassiveEffects", collector);
            }
            foreach (var node in nodes.Where(node => node != null))
            {
                foreach (var requirement in node.Requirements ?? new List<SkillTreeNodeRequirement>())
                {
                    if (requirement == null) continue;
                    if (!ids.Contains(requirement.NodeId))
                        collector.Error(tree, "Nodes.Requirements.NodeId",
                            $"Requirement wskazuje na nieistniejący node '{requirement.NodeId}'.");
                    if (requirement.MinimumRanks < 1)
                        collector.Error(tree, "Nodes.Requirements.MinimumRanks",
                            "MinimumRanks musi być większe od zera.");
                }
            }

            ValidatePassiveDefinitions(
                tree,
                tree.GlobalPassive?.PassivesPerPoint,
                "GlobalPassive.PassivesPerPoint",
                collector);
        }

        private static void ValidatePassiveDefinitions(
            SkillTreeDefinition tree,
            IEnumerable<Godmist.Data.Effects.PassiveEffectDefinition> passives,
            string field,
            Collector collector)
        {
            if (passives == null)
            {
                collector.Warning(tree, field, "Lista pasywek nie została zainicjalizowana.");
                return;
            }

            var index = 0;
            foreach (var passive in passives)
            {
                if (passive == null)
                {
                    collector.Error(tree, $"{field}[{index}]", "Definicja pasywki nie może być pusta.");
                }
                else
                {
                    foreach (var error in PassiveEffectDefinitionValidator.Validate(passive).Errors)
                        collector.Error(tree, $"{field}[{index}]", error);
                }

                index++;
            }
        }

        private static void ValidateLootTable(LootTableData lootTable, Collector collector)
        {
            var entries = lootTable.Entries ?? new List<LootEntryData>();
            if (entries.Count == 0)
                collector.Warning(lootTable, "Entries", "Loot table nie ma wpisów.");
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    collector.Error(lootTable, $"Entries[{index}]", "Wpis loot table nie może być nullem.");
                    continue;
                }
                if (entry.ItemDefinition == null)
                    collector.Error(lootTable, $"Entries[{index}].ItemDefinition", "Brak itemu w loot table.");
                if (!float.IsFinite(entry.DropChance) || entry.DropChance < 0f || entry.DropChance > 1f)
                    collector.Error(lootTable, $"Entries[{index}].DropChance", "DropChance musi być w zakresie 0-1.");
                ValidateRange(entry.Amount, lootTable, $"Entries[{index}].Amount", collector);
            }
        }

        private static void ValidateDungeonLootCatalog(
            DungeonLootTableCatalog catalog,
            Collector collector)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = catalog.Entries ?? new List<DungeonLootTableEntryData>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    collector.Error(catalog, $"Entries[{index}]", "Wpis dungeon loot catalog nie może być nullem.");
                    continue;
                }
                if (!Enum.IsDefined(typeof(DungeonTheme), entry.Theme)
                    || !Enum.IsDefined(typeof(LootLevelBracket), entry.Bracket))
                    collector.Error(catalog, $"Entries[{index}].Bracket", "Dungeon loot ma niepoprawny bracket/theme.");
                var key = $"{entry.Theme}:{entry.Bracket}";
                if (!seen.Add(key))
                    collector.Error(catalog, $"Entries[{index}]", "Ten sam theme i bracket występuje więcej niż raz.");
                if (entry.EnemyLootTable == null && entry.ChestLootTable == null && entry.BossLootTable == null)
                    collector.Warning(catalog, $"Entries[{index}]", "Wpis nie ma żadnej loot table.");
            }
        }

        private static void ValidateRoomTemplateCatalog(
            DungeonRoomTemplateCatalog catalog,
            Collector collector)
        {
            RequireText(catalog.Id, catalog, "Id", collector);
            var templates = catalog.Templates ?? new List<DungeonRoomTemplateDefinition>();
            var seen = new HashSet<DungeonRoomTemplateDefinition>(
                new UnityObjectReferenceComparer<DungeonRoomTemplateDefinition>());
            for (var index = 0; index < templates.Count; index++)
            {
                var template = templates[index];
                if (template == null)
                {
                    collector.Error(catalog, $"Templates[{index}]", "Room template catalog zawiera pustą referencję.");
                }
                else if (!seen.Add(template))
                {
                    collector.Error(catalog, $"Templates[{index}]", "Room template występuje w katalogu więcej niż raz.");
                }
            }
        }

        private static void ValidateRoomTemplate(
            DungeonRoomTemplateDefinition template,
            Collector collector)
        {
            var report = DungeonRoomTemplateEditorValidator.Validate(template);
            foreach (var error in report.Errors)
                collector.Error(template, "RoomTemplate", error);
        }

        private static void ValidateDungeonObject(
            DungeonObjectDefinition value,
            Collector collector)
        {
            RequireText(value.Id, value, "Id", collector);
            RequireText(value.DisplayName, value, "DisplayName", collector);
            if (value.SpawnWeight < 1)
                collector.Error(value, "SpawnWeight", "SpawnWeight musi być większe od zera.");
            if (value is ChestDungeonObjectDefinition chest
                && chest.LockType != ChestLockType.Unlocked
                && chest.RequiredKey == null)
                collector.Error(value, "RequiredKey", "Zamknięta skrzynia musi mieć wymagany klucz.");
        }

        private static void ValidateLootBag(LootBagItemDefinition lootBag, Collector collector)
        {
            if (lootBag.ItemType != ItemType.Lootbag)
                collector.Error(lootBag, "ItemType", "LootBagItemDefinition musi mieć ItemType = Lootbag.");
            var brackets = lootBag.Brackets ?? new List<LootBagBracketData>();
            var seen = new HashSet<LootLevelBracket>();
            for (var index = 0; index < brackets.Count; index++)
            {
                var bracket = brackets[index];
                if (bracket == null)
                {
                    collector.Error(lootBag, $"Brackets[{index}]", "Lootbag bracket nie może być nullem.");
                    continue;
                }
                if (!Enum.IsDefined(typeof(LootLevelBracket), bracket.Bracket))
                    collector.Error(lootBag, $"Brackets[{index}].Bracket", "Lootbag ma niepoprawny level bracket.");
                if (!seen.Add(bracket.Bracket))
                    collector.Error(lootBag, $"Brackets[{index}].Bracket", "Lootbag ma zduplikowany level bracket.");
                foreach (var pool in bracket.Pools ?? new List<LootBagPoolData>())
                {
                    if (pool == null) continue;
                    if (!float.IsFinite(pool.DropChance) || pool.DropChance < 0f || pool.DropChance > 1f)
                        collector.Error(lootBag, "Brackets.Pools.DropChance", "DropChance musi być w zakresie 0-1.");
                    foreach (var entry in pool.Entries ?? new List<LootBagPoolEntryData>())
                    {
                        if (entry == null || entry.ItemDefinition == null)
                            collector.Error(lootBag, "Brackets.Pools.Entries.ItemDefinition", "Brak itemu w lootbag pool.");
                        else ValidateRange(entry.Amount, lootBag, "Brackets.Pools.Entries.Amount", collector);
                    }
                }
            }
        }

        private static void ValidateDanglingObjectReferences(
            UnityEngine.Object asset,
            Collector collector)
        {
            var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue == null
                    && !property.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)))
                {
                    collector.Error(asset, NormalizeFieldPath(property.propertyPath),
                        "Referencja wskazuje na usunięty albo niedostępny asset.");
                }
            }
        }

        private static void ValidateDeadAssets(
            IReadOnlyList<UnityEngine.Object> assets,
            GameContentCatalog catalog,
            Collector collector)
        {
            var owners = assets.ToList();
            if (catalog != null) owners.Add(catalog);
            var incoming = new HashSet<UnityEngine.Object>(ObjectReferenceComparer);
            foreach (var owner in owners)
            {
                foreach (var reference in GetDirectObjectReferences(owner))
                    if (reference != null) incoming.Add(reference);
            }

            foreach (var asset in assets)
            {
                if (!IsCatalogOwnedAsset(asset) && incoming.Contains(asset)) continue;
                if (IsCatalogOwnedAsset(asset) && catalog != null && IsAssignedToCatalog(asset, catalog)) continue;
                if (!incoming.Contains(asset))
                    collector.Warning(asset, "References", "Asset jest martwy: nie ma żadnego użycia w content data.");
            }
        }

        private static IEnumerable<UnityEngine.Object> GetDirectObjectReferences(UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue != null)
                    yield return property.objectReferenceValue;
            }
        }

        private static bool IsAssignedToCatalog(UnityEngine.Object asset, GameContentCatalog catalog)
        {
            return asset switch
            {
                CharacterDefinition value => catalog.CharacterDefinitions?.Contains(value) == true,
                CharacterClassData value => catalog.CharacterClasses?.Contains(value) == true,
                ItemDefinition value => catalog.ItemDefinitions?.Contains(value) == true,
                EquipmentPartDefinition value => catalog.EquipmentPartDefinitions?.Contains(value) == true,
                SkillData value => catalog.SkillDefinitions?.Contains(value) == true,
                QuestDefinition value => catalog.QuestDefinitions?.Contains(value) == true,
                NpcDefinition value => catalog.NpcDefinitions?.Contains(value) == true,
                DungeonDefinition value => catalog.DungeonDefinitions?.Contains(value) == true,
                EncounterDefinition value => catalog.EncounterDefinitions?.Contains(value) == true,
                _ => false
            };
        }

        private static bool IsCatalogOwnedAsset(UnityEngine.Object asset) =>
            asset is CharacterDefinition || asset is CharacterClassData || asset is ItemDefinition
            || asset is EquipmentPartDefinition || asset is SkillData || asset is QuestDefinition
            || asset is NpcDefinition || asset is DungeonDefinition || asset is EncounterDefinition;

        private static bool IsKnownContentAsset(UnityEngine.Object asset) =>
            IsCatalogOwnedAsset(asset)
            || asset is SkillTreeDefinition || asset is DialogueDefinition || asset is LootTableData
            || asset is DungeonLootTableCatalog || asset is DungeonRoomTemplateCatalog
            || asset is DungeonRoomTemplateDefinition || asset is DungeonObjectDefinition;

        private static string GetProjectId(UnityEngine.Object asset) => asset switch
        {
            ItemDefinition value => value.Id,
            EquipmentPartDefinition value => value.Id,
            SkillData value => value.SkillId,
            CharacterDefinition value => value.Id,
            CharacterClassData value => value.Id,
            QuestDefinition value => value.Id,
            NpcDefinition value => value.Id,
            EncounterDefinition value => value.Id,
            DungeonDefinition value => value.Id,
            SkillTreeDefinition value => value.Id,
            DialogueDefinition value => value.Id,
            DungeonRoomTemplateCatalog value => value.Id,
            DungeonRoomTemplateDefinition value => value.Id,
            DungeonObjectDefinition value => value.Id,
            _ => string.Empty
        } ?? string.Empty;

        private static string GetProjectIdField(UnityEngine.Object asset) => asset is SkillData ? "SkillId" : "Id";

        private static string InferField(string message)
        {
            var separators = new[] { ' ', '(', '.', ':' };
            var index = message.IndexOfAny(separators);
            return index > 0 ? message.Substring(0, index) : "Content";
        }

        private static string NormalizeFieldPath(string path)
        {
            return path.Replace("<", string.Empty).Replace(">k__BackingField", string.Empty);
        }

        private static void RequireText(string value, UnityEngine.Object asset, string field, Collector collector)
        {
            if (string.IsNullOrWhiteSpace(value)) collector.Error(asset, field, $"{field} nie może być puste.");
        }

        private static void ValidateListNulls<T>(
            UnityEngine.Object asset,
            string field,
            IEnumerable<T> values,
            Collector collector,
            bool error)
            where T : class
        {
            if (values == null)
            {
                if (error) collector.Error(asset, field, "Lista nie może być nullem.");
                else collector.Warning(asset, field, "Lista nie została zainicjalizowana.");
                return;
            }
            var index = 0;
            foreach (var value in values)
            {
                if (value == null)
                {
                    if (error) collector.Error(asset, $"{field}[{index}]", "Lista zawiera pusty wpis.");
                    else collector.Warning(asset, $"{field}[{index}]", "Lista zawiera pusty wpis.");
                }
                index++;
            }
        }

        private static void ValidateRange(IntRange range, UnityEngine.Object asset, string field, Collector collector)
        {
            if (range.Min < 1 || range.Max < range.Min)
                collector.Error(asset, field, "Zakres musi mieć wartości dodatnie i Min <= Max.");
        }

        private sealed class Collector
        {
            public List<ContentValidationIssue> Issues { get; } = new();

            public void Error(UnityEngine.Object asset, string field, string message) =>
                Issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, asset, field, message));

            public void Warning(UnityEngine.Object asset, string field, string message) =>
                Issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, asset, field, message));

            public void Info(UnityEngine.Object asset, string field, string message) =>
                Issues.Add(new ContentValidationIssue(ContentValidationSeverity.Info, asset, field, message));
        }

        private sealed class UnityObjectReferenceComparer : IEqualityComparer<UnityEngine.Object>
        {
            public bool Equals(UnityEngine.Object left, UnityEngine.Object right) => ReferenceEquals(left, right);
            public int GetHashCode(UnityEngine.Object value) =>
                value == null ? 0 : RuntimeHelpers.GetHashCode(value);
        }

        private sealed class UnityObjectReferenceComparer<T> : IEqualityComparer<T>
            where T : UnityEngine.Object
        {
            public bool Equals(T left, T right) => ReferenceEquals(left, right);
            public int GetHashCode(T value) =>
                value == null ? 0 : RuntimeHelpers.GetHashCode(value);
        }
    }
}
