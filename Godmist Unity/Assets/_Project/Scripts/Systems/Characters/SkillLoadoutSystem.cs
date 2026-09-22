using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Characters
{
    public class SkillLoadoutSystem
    {
        internal bool TryInitialize(CharacterInstance character)
        {
            if (character == null || character.ActiveSkillLoadout == null)
            {
                return false;
            }

            return character.ActiveSkillLoadout.TryInitialize(
                GetKnownSkillIds(character),
                character.GetBasicAttackSkill()?.SkillId);
        }

        public bool TrySetActiveSkills(
            GameSession session,
            string characterId,
            IReadOnlyList<string> activeSkillIds)
        {
            if (!TryGetCharacterOutsideCombat(
                    session,
                    characterId,
                    out var character))
            {
                return false;
            }

            return TrySetActiveSkillsInternal(character, activeSkillIds);
        }

        public bool TryAddSkill(
            GameSession session,
            string characterId,
            string skillId)
        {
            if (!TryGetCharacterOutsideCombat(
                    session,
                    characterId,
                    out var character))
            {
                return false;
            }

            return character.ActiveSkillLoadout != null
                && character.ActiveSkillLoadout.TryAddSkill(
                    skillId,
                    GetKnownSkillIds(character));
        }

        public bool TryAddSkillAtSlot(
            GameSession session,
            string characterId,
            string skillId,
            int targetIndex)
        {
            if (!TryGetCharacterOutsideCombat(session, characterId, out var character))
            {
                return false;
            }

            return character.ActiveSkillLoadout != null
                && character.ActiveSkillLoadout.TryAddSkillAtSlot(
                    skillId,
                    targetIndex,
                    GetKnownSkillIds(character));
        }

        public bool TryRemoveSkill(
            GameSession session,
            string characterId,
            string skillId)
        {
            if (!TryGetCharacterOutsideCombat(
                    session,
                    characterId,
                    out var character))
            {
                return false;
            }

            return character.ActiveSkillLoadout != null
                && character.ActiveSkillLoadout.TryRemoveSkill(skillId);
        }

        public bool TryReplaceSkill(
            GameSession session,
            string characterId,
            string targetSkillId,
            string replacementSkillId)
        {
            if (!TryGetCharacterOutsideCombat(
                    session,
                    characterId,
                    out var character))
            {
                return false;
            }

            return character.ActiveSkillLoadout != null
                && character.ActiveSkillLoadout.TryReplaceSkill(
                    targetSkillId,
                    replacementSkillId,
                    GetKnownSkillIds(character));
        }

        public bool TryMoveSkill(
            GameSession session,
            string characterId,
            string skillId,
            int targetIndex)
        {
            if (!TryGetCharacterOutsideCombat(
                    session,
                    characterId,
                    out var character))
            {
                return false;
            }

            return character.ActiveSkillLoadout != null
                && character.ActiveSkillLoadout.TryMoveSkill(
                    skillId,
                    targetIndex);
        }

        public bool TrySetCapacityModifier(
            GameSession session,
            string characterId,
            string sourceId,
            int amount)
        {
            if (!TryGetCharacterOutsideCombat(
                    session,
                    characterId,
                    out var character))
            {
                return false;
            }

            return character.ActiveSkillLoadout != null
                && character.ActiveSkillLoadout.TrySetCapacityModifier(
                    sourceId,
                    amount);
        }

        private static bool TrySetActiveSkillsInternal(
            CharacterInstance character,
            IReadOnlyList<string> activeSkillIds)
        {
            if (character?.ActiveSkillLoadout == null)
            {
                return false;
            }

            return character.ActiveSkillLoadout.TryInitialize(
                GetKnownSkillIds(character),
                character.GetBasicAttackSkill()?.SkillId,
                activeSkillIds,
                character.ActiveSkillLoadout.CopyCapacityModifiers());
        }

        private static bool TryGetCharacterOutsideCombat(
            GameSession session,
            string characterId,
            out CharacterInstance character)
        {
            character = null;

            if (session == null
                || session.IsCharacterInCombat(characterId)
                || string.IsNullOrWhiteSpace(characterId)
                || !session.CharactersById.TryGetValue(
                    characterId,
                    out character))
            {
                character = null;
                return false;
            }

            return true;
        }

        private static List<string> GetKnownSkillIds(
            CharacterInstance character)
        {
            var knownSkillIds = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var skill in character?.GetKnownSkills() ?? new List<SkillData>())
            {
                if (skill != null
                    && !string.IsNullOrWhiteSpace(skill.SkillId)
                    && seen.Add(skill.SkillId))
                {
                    knownSkillIds.Add(skill.SkillId);
                }
            }

            return knownSkillIds;
        }
    }
}
