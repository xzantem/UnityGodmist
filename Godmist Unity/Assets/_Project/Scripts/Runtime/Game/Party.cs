using System;
using System.Collections.Generic;

namespace Godmist.Runtime.Game
{
    public class Party
    {
        public const int DefaultMaxActiveMembers = 5;

        private readonly List<string> activeMemberIds = new();
        private readonly List<string> benchMemberIds = new();

        public int MaxActiveMembers { get; }
        public IReadOnlyList<string> ActiveMemberIds => activeMemberIds;
        public IReadOnlyList<string> BenchMemberIds => benchMemberIds;
        public string ActiveLeaderId { get; private set; } = string.Empty;

        public event Action<string, string> ActiveLeaderChanged;

        public Party(int maxActiveMembers = DefaultMaxActiveMembers)
        {
            if (maxActiveMembers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxActiveMembers));
            }

            MaxActiveMembers = maxActiveMembers;
        }

        public bool ContainsMember(string characterId)
        {
            return activeMemberIds.Contains(characterId) || benchMemberIds.Contains(characterId);
        }

        public bool IsActiveMember(string characterId)
        {
            return activeMemberIds.Contains(characterId);
        }

        public bool TryAddActiveMember(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId) || ContainsMember(characterId) 
                || activeMemberIds.Count >= MaxActiveMembers) return false;

            activeMemberIds.Add(characterId);
            return true;
        }

        public bool TryAddBenchMember(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId) || ContainsMember(characterId)) return false;

            benchMemberIds.Add(characterId);
            return true;
        }

        public bool TryMoveActiveMemberToBench(string characterId)
        {
            if (!activeMemberIds.Remove(characterId)) return false;

            benchMemberIds.Add(characterId);

            if (ActiveLeaderId == characterId)
            {
                SetActiveLeaderInternal(string.Empty);
            }

            return true;
        }

        public bool TryMoveBenchMemberToActive(string characterId)
        {
            if (!benchMemberIds.Contains(characterId) || activeMemberIds.Count >= MaxActiveMembers) return false;

            benchMemberIds.Remove(characterId);
            activeMemberIds.Add(characterId);
            return true;
        }

        public bool TrySetActiveLeader(string characterId)
        {
            if (!activeMemberIds.Contains(characterId)) return false;

            SetActiveLeaderInternal(characterId);
            return true;
        }

        private void SetActiveLeaderInternal(string nextLeaderId)
        {
            if (ActiveLeaderId == nextLeaderId) return;

            var previousLeaderId = ActiveLeaderId;
            ActiveLeaderId = nextLeaderId;

            ActiveLeaderChanged?.Invoke(previousLeaderId, nextLeaderId);
        }
    }
}