using System;
using System.Collections.Generic;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.Quests;
using Godmist.Data.Skills;
using Godmist.Data.Combat;
using UnityEngine;

namespace Godmist.Data.World
{
    [CreateAssetMenu(
        fileName = "GameContentCatalog",
        menuName = "Godmist/World/Game Content Catalog")]
    public class GameContentCatalog : ScriptableObject
    {
        [field: SerializeField] public List<CharacterDefinition> CharacterDefinitions { get; private set; } = new();
        [field: SerializeField] public List<CharacterClassData> CharacterClasses { get; private set; } = new();
        [field: SerializeField] public List<ItemDefinition> ItemDefinitions { get; private set; } = new();
        [field: SerializeField] public List<EquipmentPartDefinition> EquipmentPartDefinitions { get; private set; } = new();
        [field: SerializeField] public List<SkillData> SkillDefinitions { get; private set; } = new();
        [field: SerializeField] public List<QuestDefinition> QuestDefinitions { get; private set; } = new();
        [field: SerializeField] public List<NpcDefinition> NpcDefinitions { get; private set; } = new();
        [field: SerializeField] public List<DungeonDefinition> DungeonDefinitions { get; private set; } = new();
        [field: SerializeField] public List<EncounterDefinition> EncounterDefinitions { get; private set; } = new();
        // Retain definitions here when removing them from a dungeon's generation pool.
        [field: SerializeField] public List<DungeonObjectDefinition> DungeonObjectDefinitions { get; private set; } = new();

        public bool TryGetDungeonObject(string id, out DungeonObjectDefinition definition)
        {
            if (TryFind(DungeonObjectDefinitions, id, out definition)) return true;
            foreach (var dungeon in DungeonDefinitions)
                if (dungeon != null && TryFind(dungeon.ObjectDefinitions, id, out definition)) return true;
            return false;
        }

        public bool TryGetCharacter(
            string characterId,
            out CharacterDefinition definition)
        {
            return TryFind(CharacterDefinitions, characterId, out definition);
        }

        public bool TryGetCharacterClass(
            string classId,
            out CharacterClassData definition)
        {
            return TryFind(CharacterClasses, classId, out definition);
        }

        public bool TryGetItem(
            string itemId,
            out ItemDefinition definition)
        {
            return TryFind(ItemDefinitions, itemId, out definition);
        }

        public bool TryGetSkill(
            string skillId,
            out SkillData definition)
        {
            return TryFind(SkillDefinitions, skillId, out definition);
        }

        public bool TryGetQuest(
            string questId,
            out QuestDefinition definition)
        {
            return TryFind(QuestDefinitions, questId, out definition);
        }

        public bool TryGetNpc(
            string npcId,
            out NpcDefinition definition)
        {
            return TryFind(NpcDefinitions, npcId, out definition);
        }

        public bool TryGetDungeon(
            string dungeonId,
            out DungeonDefinition definition)
        {
            return TryFind(DungeonDefinitions, dungeonId, out definition);
        }

        public bool TryGetEncounter(
            string encounterId,
            out EncounterDefinition definition)
        {
            return TryFind(
                EncounterDefinitions,
                encounterId,
                out definition);
        }

        private static bool TryFind<T>(
            List<T> definitions,
            string id,
            out T definition)
            where T : ScriptableObject
        {
            definition = null;

            if (definitions == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            foreach (var candidate in definitions)
            {
                if (candidate == null)
                {
                    continue;
                }

                var candidateId = GetId(candidate);

                if (!string.Equals(
                        candidateId,
                        id,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                definition = candidate;
                return true;
            }

            return false;
        }

        private static string GetId(ScriptableObject definition)
        {
            return definition switch
            {
                CharacterDefinition value => value.Id,
                CharacterClassData value => value.Id,
                ItemDefinition value => value.Id,
                SkillData value => value.SkillId,
                QuestDefinition value => value.Id,
                NpcDefinition value => value.Id,
                EncounterDefinition value => value.Id,
                DungeonDefinition value => value.Id,
                DungeonObjectDefinition value => value.Id,
                _ => string.Empty
            };
        }
    }
}
