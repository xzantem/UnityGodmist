using System;
using System.Collections.Generic;
using Godmist.Data.Configuration;

namespace Godmist.Runtime.Game
{
    public class Party
    {
        public static int DefaultMaxActiveMembers => GameDefinesRuntime.GetInt(
            GameDefineIds.DefaultMaxActivePartyMembers, 5);

        private readonly List<string> activeMemberIds = new();
        private readonly List<string> benchMemberIds = new();
        private readonly List<List<string>> groups = new();
        public int GroupCount => groups.Count;
        public event Action GroupsChanged;
        public IReadOnlyList<string> GetGroup(int index) => groups[index].AsReadOnly();
        public int GetGroupIndex(string id) => groups.FindIndex(group => group.Contains(id));
        public bool AreGrouped(string first, string second)
            => GetGroupIndex(first) is var index && index >= 0 && index == GetGroupIndex(second);

        public bool TryDetach(string id, int insertionIndex = -1)
        {
            var index = GetGroupIndex(id);
            if (index < 0) return false;
            if (groups[index].Count == 1 && insertionIndex < 0) return false;
            var destination = insertionIndex < 0 ? groups.Count : Math.Clamp(insertionIndex, 0, groups.Count);
            groups[index].Remove(id);
            if (groups[index].Count == 0) { groups.RemoveAt(index); if (index < destination) destination--; }
            groups.Insert(destination, new List<string> { id });
            GroupsChanged?.Invoke();
            return true;
        }

        // The target member identifies a group without exposing mutable group state to views.
        public bool TryJoinGroup(string id, string targetMember, int insertionIndex = -1)
        {
            var sourceIndex = GetGroupIndex(id); var targetIndex = GetGroupIndex(targetMember);
            if (sourceIndex < 0 || targetIndex < 0) return false;
            var source = groups[sourceIndex]; var target = groups[targetIndex];
            var destination = insertionIndex < 0 ? target.Count : Math.Clamp(insertionIndex, 0, target.Count);
            var oldIndex = source.IndexOf(id);
            if (source == target && oldIndex < destination) destination--;
            source.Remove(id);
            if (source != target && source.Count == 0) groups.Remove(source);
            target.Insert(destination, id);
            GroupsChanged?.Invoke();
            return true;
        }

        public bool TryRestoreGroups(IReadOnlyList<List<string>> savedGroups)
        {
            // Missing field in old saves means the original single party group.
            if (savedGroups == null) return true;
            var seen = new HashSet<string>();
            var restored = new List<List<string>>();
            foreach (var group in savedGroups)
            {
                if (group == null || group.Count == 0) return false;
                foreach (var id in group) if (!activeMemberIds.Contains(id) || !seen.Add(id)) return false;
                restored.Add(new List<string>(group));
            }
            if (seen.Count != activeMemberIds.Count) return false;
            groups.Clear(); groups.AddRange(restored);
            GroupsChanged?.Invoke();
            return true;
        }

        public int MaxActiveMembers { get; }
        public IReadOnlyList<string> ActiveMemberIds => activeMemberIds;
        public IReadOnlyList<string> BenchMemberIds => benchMemberIds;
        public string ActiveLeaderId { get; private set; } = string.Empty;

        public event Action<string, string> ActiveLeaderChanged;

        public Party(int maxActiveMembers = -1)
        {
            if (maxActiveMembers < 1)
            {
                maxActiveMembers = DefaultMaxActiveMembers;
            }

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
            if (groups.Count == 0) groups.Add(new List<string>());
            groups[0].Add(characterId);
            GroupsChanged?.Invoke();
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
            var group = groups[GetGroupIndex(characterId)];
            group.Remove(characterId);
            if (group.Count == 0) groups.Remove(group);
            GroupsChanged?.Invoke();

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
            if (groups.Count == 0) groups.Add(new List<string>());
            groups[0].Add(characterId);
            GroupsChanged?.Invoke();
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
