using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;

namespace Godmist.Runtime.Game
{
    public class GameSession
    {
        private readonly Dictionary<string, CharacterInstance> charactersById = new();

        public GameModeState CurrentMode { get; private set; }
        public Party PlayerParty { get; } = new();
        public IReadOnlyDictionary<string, CharacterInstance> CharactersById => charactersById;

        public event Action<GameModeState, GameModeState> ModeChanged;

        public GameSession(GameModeState initialMode = GameModeState.Menu)
        {
            CurrentMode = initialMode;
        }

        public bool TrySetMode(GameModeState nextMode)
        {
            if (CurrentMode == nextMode) return false;

            var previousMode = CurrentMode;
            CurrentMode = nextMode;

            ModeChanged?.Invoke(previousMode, nextMode);
            return true;
        }

        public bool TryRegisterCharacter(CharacterInstance character)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.CharacterId) 
                || charactersById.ContainsKey(character.CharacterId)) return false;

            charactersById.Add(character.CharacterId, character);
            return true;
        }

        public bool TryAddCharacterToActiveParty(string characterId)
        {
            if (!CanJoinActiveParty(characterId)) return false;

            var added = PlayerParty.TryAddActiveMember(characterId);

            if (added && string.IsNullOrEmpty(PlayerParty.ActiveLeaderId))
            {
                TryAssignFirstAvailableLeader();
            }

            return added;
        }

        public bool TryAddCharacterToBench(string characterId)
        {
            return charactersById.ContainsKey(characterId) && PlayerParty.TryAddBenchMember(characterId);
        }

        public bool TryMoveActiveMemberToBench(string characterId)
        {
            var wasLeader = PlayerParty.ActiveLeaderId == characterId;

            if (!PlayerParty.TryMoveActiveMemberToBench(characterId)) return false;

            if (wasLeader)
            {
                TryAssignFirstAvailableLeader();
            }

            return true;
        }

        public bool TryMoveBenchMemberToActive(string characterId)
        {
            if (!CanJoinActiveParty(characterId)) return false;

            var moved = PlayerParty.TryMoveBenchMemberToActive(characterId);

            if (moved && string.IsNullOrEmpty(PlayerParty.ActiveLeaderId))
            {
                TryAssignFirstAvailableLeader();
            }

            return moved;
        }

        public bool TrySetActiveLeader(string characterId)
        {
            if (!CanLeadParty(characterId)) return false;

            return PlayerParty.TrySetActiveLeader(characterId);
        }

        private bool CanJoinActiveParty(string characterId)
        {
            if (!charactersById.TryGetValue(characterId, out var character)) return false;

            return character.AvailabilityState == CharacterAvailabilityState.Available 
                && character.LifeState != CharacterLifeState.Dead;
        }

        private bool CanLeadParty(string characterId)
        {
            if (!charactersById.TryGetValue(characterId, out var character)) return false;

            return character.AvailabilityState == CharacterAvailabilityState.Available
                && character.LifeState == CharacterLifeState.Alive 
                && PlayerParty.IsActiveMember(characterId);
        }

        private bool TryAssignFirstAvailableLeader()
        {
            foreach (var characterId in PlayerParty.ActiveMemberIds)
            {
                if (CanLeadParty(characterId))
                {
                    return PlayerParty.TrySetActiveLeader(characterId);
                }
            }

            return false;
        }
    }
}