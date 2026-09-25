using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;

namespace Godmist.Systems.Combat
{
    public sealed class PassiveEffectReconciliationSystem
    {
        public const string ManagedEquipmentSourcePrefix = "passive-migration.equipment.";
        public const string ManagedSkillTreeSourcePrefix = "passive-migration.skill-tree.";

        public void Reconcile(CharacterInstance character)
        {
            if (character == null) return;

            character.PassiveEffects ??= new List<PassiveEffectRuntime>();
            RemoveManagedEffects(character.PassiveEffects);

            foreach (var item in character.Equipment?.GetEquippedItems()
                         ?? Array.Empty<EquipmentItemInstance>())
            {
                item?.RecalculateStatModifiers();
                AddEquipmentStatEffect(character, item);
                AddEquipmentDefinitions(character, item);
                AddSocketedGaldurytes(character, item);
            }

            foreach (var tree in character.ClassData?.SkillTrees
                         ?? new List<SkillTreeDefinition>())
            {
                AddSkillTreePassives(character, tree);
            }
        }

        public static bool IsManaged(PassiveEffectRuntime effect)
        {
            foreach (var source in effect?.Sources ?? new List<PassiveEffectSourceRuntime>())
            {
                if (source?.SourceType == PassiveEffectSourceType.Equipment
                    && source.SourceId.StartsWith(
                        ManagedEquipmentSourcePrefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                if (source?.SourceType == PassiveEffectSourceType.SkillTree
                    && source.SourceId.StartsWith(
                        ManagedSkillTreeSourcePrefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveManagedEffects(
            List<PassiveEffectRuntime> passiveEffects)
        {
            for (var index = passiveEffects.Count - 1; index >= 0; index--)
            {
                if (IsManaged(passiveEffects[index]))
                    passiveEffects.RemoveAt(index);
            }
        }

        private static void AddEquipmentStatEffect(
            CharacterInstance character,
            EquipmentItemInstance item)
        {
            if (item == null || item.StatModifiers == null || item.StatModifiers.Count == 0)
                return;

            var sourceId = BuildEquipmentSourceId(item);
            character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                EffectId = $"{sourceId}.stats",
                DefinitionId = "equipment.generated.stats",
                DisplayName = item.DisplayName,
                Sources = new List<PassiveEffectSourceRuntime>
                {
                    CreateSource(PassiveEffectSourceType.Equipment, sourceId)
                },
                Lifetime = PassiveEffectLifetime.Persistent,
                StatModifiers = CopyStatModifiers(item.StatModifiers)
            });
        }

        private static void AddEquipmentDefinitions(
            CharacterInstance character,
            EquipmentItemInstance item)
        {
            if (item?.EquipmentDefinition == null) return;

            var sourceId = BuildEquipmentSourceId(item);
            AddDefinitions(
                character,
                (item.Definition as UniqueEquipmentDefinition)?.GetEffects(item.ItemLevel),
                sourceId,
                PassiveEffectSourceType.Equipment,
                item.DisplayName);

            if (item is not JewelryItemInstance jewelry
                || item.EquipmentDefinition is not JewelryItemDefinition definition)
            {
                return;
            }

            var metal = FindMetal(definition, jewelry.MetalId);
            AddJewelryModifiers(
                character,
                metal?.Modifiers,
                $"{sourceId}.metal.{jewelry.MetalId}",
                jewelry.MetalDisplayName);

            var gem = FindGem(definition, jewelry.GemId);
            AddJewelryModifiers(
                character,
                gem?.Modifiers,
                $"{sourceId}.gem.{jewelry.GemId}",
                jewelry.GemDisplayName);
        }

        private static void AddSocketedGaldurytes(
            CharacterInstance character,
            EquipmentItemInstance equipment)
        {
            var socketIndex = 0;
            foreach (var galduryte in equipment?.SocketedGaldurytes
                         ?? new List<GalduryteItemInstance>())
            {
                if (!equipment.IsSocketActive(socketIndex++)) continue;
                if (galduryte?.IsIdentified != true || galduryte.IsDestroyed) continue;
                var sourceId = $"{BuildEquipmentSourceId(equipment)}.galduryte.{galduryte.InstanceId}";
                var stats = new List<StatModifier>();
                var variables = new List<GameplayVariableModifier>();
                foreach (var effect in galduryte.Effects ?? new List<GalduryteEffectInstance>())
                {
                    if (effect == null) continue;
                    switch (effect.EffectType)
                    {
                        case GalduryteEffectType.CriticalDamageFlat:
                            stats.Add(CreateGalduryteStat(CharacterStatType.CritDamage, effect.Value, StatModifierOperation.FlatAbsolute));
                            break;
                        case GalduryteEffectType.DodgePercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.Dodge, effect.Value, StatModifierOperation.Percent));
                            break;
                        case GalduryteEffectType.MagicDefensePercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.MagicalDefense, effect.Value, StatModifierOperation.Percent));
                            break;
                        case GalduryteEffectType.MaxResourcePercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.MaxResource, effect.Value, StatModifierOperation.Percent));
                            break;
                        case GalduryteEffectType.PhysicalPenetration:
                            stats.Add(CreateGalduryteStat(CharacterStatType.PhysicalDefensePenetration, effect.Value, StatModifierOperation.FlatAbsolute));
                            break;
                        case GalduryteEffectType.MagicPenetration:
                            stats.Add(CreateGalduryteStat(CharacterStatType.MagicalDefensePenetration, effect.Value, StatModifierOperation.FlatAbsolute));
                            break;
                        case GalduryteEffectType.AllDefensePercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.PhysicalDefense, effect.Value, StatModifierOperation.Percent));
                            stats.Add(CreateGalduryteStat(CharacterStatType.MagicalDefense, effect.Value, StatModifierOperation.Percent));
                            break;
                        case GalduryteEffectType.ExperiencePercent:
                            variables.Add(new GameplayVariableModifier { VariableType = GameplayVariableType.ExperienceGainPercent, Stacking = StatModifierStacking.Additive, Value = effect.Value });
                            break;
                        case GalduryteEffectType.GoldPercent:
                            variables.Add(new GameplayVariableModifier { VariableType = GameplayVariableType.GoldGainPercent, Stacking = StatModifierStacking.Additive, Value = effect.Value });
                            break;
                        case GalduryteEffectType.GeneralDamagePercent:
                            variables.Add(new GameplayVariableModifier
                            {
                                VariableType = GameplayVariableType.DamageDealtPercent,
                                Stacking = StatModifierStacking.Additive,
                                Value = effect.Value
                            });
                            break;
                        case GalduryteEffectType.CriticalChancePercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.CritChance, effect.Value,
                                StatModifierOperation.FlatAbsolute));
                            break;
                        case GalduryteEffectType.AccuracyPercent:
                            variables.Add(new GameplayVariableModifier
                            {
                                VariableType = GameplayVariableType.HitChanceBonus,
                                Stacking = StatModifierStacking.Additive,
                                Value = effect.Value
                            });
                            break;
                        case GalduryteEffectType.MaxHealthPercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.MaxHealth, effect.Value,
                                StatModifierOperation.Percent));
                            break;
                        case GalduryteEffectType.PhysicalDefensePercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.PhysicalDefense, effect.Value,
                                StatModifierOperation.Percent));
                            break;
                        case GalduryteEffectType.SpeedPercent:
                            stats.Add(CreateGalduryteStat(CharacterStatType.Speed, effect.Value,
                                StatModifierOperation.Percent));
                            break;
                    }
                }
                if (stats.Count == 0 && variables.Count == 0) continue;
                character.PassiveEffects.Add(new PassiveEffectRuntime
                {
                    EffectId = sourceId,
                    DefinitionId = "equipment.socketed_galduryte",
                    DisplayName = galduryte.DisplayName,
                    Sources = new List<PassiveEffectSourceRuntime>
                    {
                        CreateSource(PassiveEffectSourceType.Equipment, sourceId)
                    },
                    Lifetime = PassiveEffectLifetime.Persistent,
                    StatModifiers = stats,
                    VariableModifiers = variables
                });
            }
        }

        private static StatModifier CreateGalduryteStat(
            CharacterStatType stat,
            float value,
            StatModifierOperation operation) => new()
        {
            StatType = stat,
            Operation = operation,
            Stacking = StatModifierStacking.Additive,
            Value = value
        };

        private static void AddJewelryModifiers(
            CharacterInstance character,
            List<JewelryModifierData> modifiers,
            string sourceId,
            string displayName)
        {
            for (var index = 0; index < (modifiers?.Count ?? 0); index++)
            {
                var modifier = modifiers[index];
                if (modifier == null) continue;

                var runtime = new PassiveEffectRuntime
                {
                    EffectId = $"{sourceId}.modifier.{index}",
                    DefinitionId = "jewelry.modifier",
                    DisplayName = displayName ?? string.Empty,
                    Lifetime = PassiveEffectLifetime.Persistent,
                    Sources = new List<PassiveEffectSourceRuntime>
                    {
                        CreateSource(PassiveEffectSourceType.Equipment, sourceId)
                    }
                };

                switch (modifier.Target)
                {
                    case JewelryModifierTarget.Stat:
                        // Stats are generated into EquipmentItemInstance.BaseStatModifiers
                        // so upgrades and normal equipment presentation use one path.
                        continue;
                    case JewelryModifierTarget.Resistance:
                        runtime.SetPayload(new ResistanceModifierPayload
                        {
                            ResistanceType = modifier.ResistanceType,
                            Value = modifier.Value
                        });
                        break;
                    case JewelryModifierTarget.GameplayVariable:
                        runtime.VariableModifiers = new List<GameplayVariableModifier>
                        {
                            new()
                            {
                                VariableType = modifier.GameplayVariable,
                                ReferenceId = modifier.ReferenceId ?? string.Empty,
                                Stacking = StatModifierStacking.Additive,
                                Value = modifier.Value
                            }
                        };
                        break;
                    default:
                        continue;
                }

                character.PassiveEffects.Add(runtime);
            }
        }

        private static void AddSkillTreePassives(
            CharacterInstance character,
            SkillTreeDefinition tree)
        {
            if (tree == null || string.IsNullOrWhiteSpace(tree.Id)) return;

            var ranks = SkillTreeProgressionSystem.GetInvestedRanks(character, tree.Id);
            if (ranks <= 0) return;

            var sourceId = $"{ManagedSkillTreeSourcePrefix}{tree.Id}";
            foreach (var definition in tree.GlobalPassive?.PassivesPerPoint
                         ?? new List<PassiveEffectDefinition>())
            {
                if (ShouldSkipUniqueDefinition(character, definition))
                    continue;

                if (!PassiveEffectRuntimeFactory.TryCreate(
                        definition,
                        CreateSource(PassiveEffectSourceType.SkillTree, sourceId),
                        $"{sourceId}.{definition?.Id}",
                        out var runtime))
                {
                    continue;
                }

                ScaleRuntimeValues(runtime, ranks);
                character.PassiveEffects.Add(runtime);
            }

            foreach (var node in tree.Nodes ?? new List<SkillTreeNodeDefinition>())
            {
                var nodeRanks = GetNodeRanks(character, tree.Id, node?.Id);
                if (node == null || nodeRanks <= 0)
                    continue;

                foreach (var definition in node.PassiveEffects
                             ?? new List<PassiveEffectDefinition>())
                {
                    if (ShouldSkipUniqueDefinition(character, definition))
                        continue;

                    if (!PassiveEffectRuntimeFactory.TryCreate(
                            definition,
                            CreateSource(
                                PassiveEffectSourceType.SkillTree,
                                sourceId),
                            out var runtime))
                    {
                        continue;
                    }

                    runtime.EffectId =
                        $"{sourceId}.node.{node.Id}.{definition?.Id}";
                    runtime.Stacks = nodeRanks;
                    ScaleRuntimeValues(runtime, nodeRanks);
                    if (string.IsNullOrWhiteSpace(runtime.DisplayName))
                        runtime.DisplayName = node.DisplayName ?? string.Empty;
                    character.PassiveEffects.Add(runtime);
                }
            }
        }

        private static int GetNodeRanks(
            CharacterInstance character,
            string treeId,
            string nodeId)
        {
            if (string.IsNullOrWhiteSpace(treeId) || string.IsNullOrWhiteSpace(nodeId))
                return 0;

            return Math.Max(
                0,
                character.SkillTreeProgress?
                    .Find(progress => progress?.TreeId == treeId)?
                    .NodeInvestments?
                    .Find(investment => investment?.NodeId == nodeId)?.Ranks ?? 0);
        }

        private static void AddDefinitions(
            CharacterInstance character,
            List<PassiveEffectDefinition> definitions,
            string sourceId,
            PassiveEffectSourceType sourceType,
            string displayName)
        {
            if (definitions == null || definitions.Count == 0) return;

            foreach (var definition in definitions)
            {
                if (ShouldSkipUniqueDefinition(character, definition))
                    continue;

                if (!PassiveEffectRuntimeFactory.TryCreate(
                        definition,
                        CreateSource(sourceType, sourceId),
                        out var runtime))
                {
                    continue;
                }

                runtime.EffectId = $"{sourceId}.{definition?.Id}";

                if (string.IsNullOrWhiteSpace(runtime.DisplayName))
                    runtime.DisplayName = displayName ?? string.Empty;

                character.PassiveEffects.Add(runtime);
            }
        }

        private static bool ShouldSkipUniqueDefinition(
            CharacterInstance character,
            PassiveEffectDefinition definition)
        {
            if (definition == null
                || definition.StackingRule != PassiveEffectStackingRule.Unique
                || string.IsNullOrWhiteSpace(definition.Id))
            {
                return false;
            }

            foreach (var existing in character?.PassiveEffects
                         ?? new List<PassiveEffectRuntime>())
            {
                if (existing != null
                    && IsManaged(existing)
                    && existing.DefinitionId == definition.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private static JewelryMetalData FindMetal(
            JewelryItemDefinition definition,
            string metalId)
        {
            foreach (var metal in definition?.Metals ?? new List<JewelryMetalData>())
            {
                if (Godmist.Systems.Items.JewelryGenerationService.GetMetalRecipeKey(metal) == metalId) return metal;
            }

            return null;
        }

        private static JewelryGemDefinition FindGem(
            JewelryItemDefinition definition,
            string gemId)
        {
            foreach (var gem in definition?.Gems ?? new List<JewelryGemDefinition>())
            {
                if (gem?.CraftingMaterial?.Id == gemId) return gem;
            }

            return null;
        }

        private static PassiveEffectSourceRuntime CreateSource(
            PassiveEffectSourceType sourceType,
            string sourceId)
        {
            return new PassiveEffectSourceRuntime
            {
                SourceType = sourceType,
                SourceId = sourceId ?? string.Empty
            };
        }

        private static string BuildEquipmentSourceId(EquipmentItemInstance item)
        {
            return $"{ManagedEquipmentSourcePrefix}{item?.InstanceId ?? string.Empty}";
        }

        private static void ScaleRuntimeValues(
            PassiveEffectRuntime runtime,
            int multiplier)
        {
            if (runtime == null) return;

            foreach (var modifier in runtime.StatModifiers ?? new List<StatModifier>())
                modifier.Value *= multiplier;

            foreach (var modifier in runtime.VariableModifiers
                         ?? new List<GameplayVariableModifier>())
            {
                modifier.Value *= multiplier;
            }

            var resistance = runtime.GetPayload<ResistanceModifierPayload>();
            if (resistance != null) resistance.Value *= multiplier;
        }

        private static List<StatModifier> CopyStatModifiers(
            List<StatModifier> modifiers)
        {
            var result = new List<StatModifier>();
            foreach (var modifier in modifiers ?? new List<StatModifier>())
            {
                if (modifier == null) continue;
                result.Add(new StatModifier
                {
                    StatType = modifier.StatType,
                    Operation = modifier.Operation,
                    Stacking = modifier.Stacking,
                    Value = modifier.Value
                });
            }

            return result;
        }
    }
}
