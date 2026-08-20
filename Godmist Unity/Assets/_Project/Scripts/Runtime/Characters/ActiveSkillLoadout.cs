using System;
using System.Collections.Generic;
using Godmist.Data.Skills;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public sealed class ActiveSkillLoadout
    {
        public const int DefaultBaseCapacity = 5;

        private readonly List<string> activeSkillIds = new();
        private readonly List<SkillLoadoutCapacityModifier> capacityModifiers = new();

        public IReadOnlyList<string> ActiveSkillIds => new List<string>(activeSkillIds);
        public IReadOnlyList<SkillLoadoutCapacityModifier> CapacityModifiers
            => CopyCapacityModifiers();
        public int BaseCapacity { get; private set; } = DefaultBaseCapacity;
        public int Capacity
        {
            get
            {
                var capacity = BaseCapacity;
                foreach (var modifier in capacityModifiers)
                {
                    if (modifier != null)
                    {
                        capacity += modifier.Amount;
                    }
                }

                return Math.Max(1, capacity);
            }
        }

        public string BasicAttackSkillId { get; private set; } = string.Empty;
        public bool IsInitialized => !string.IsNullOrWhiteSpace(BasicAttackSkillId);

        internal bool TryInitialize(
            IEnumerable<string> knownSkillIds,
            string basicAttackSkillId,
            IEnumerable<string> requestedActiveSkillIds = null,
            IEnumerable<SkillLoadoutCapacityModifier> requestedCapacityModifiers = null)
        {
            var knownIds = CreateKnownSkillIdSet(
                knownSkillIds,
                out var knownIdOrder);

            if (knownIds == null
                || string.IsNullOrWhiteSpace(basicAttackSkillId)
                || !knownIds.Contains(basicAttackSkillId))
            {
                return false;
            }

            var nextCapacityModifiers = CreateCapacityModifiers(
                requestedCapacityModifiers,
                out var validModifiers);

            if (!validModifiers)
            {
                return false;
            }

            var nextCapacity = BaseCapacity;
            foreach (var modifier in nextCapacityModifiers)
            {
                nextCapacity += modifier.Amount;
            }

            nextCapacity = Math.Max(1, nextCapacity);

            List<string> nextActiveSkillIds;
            bool validActiveSkillIds;

            if (requestedActiveSkillIds == null)
            {
                nextActiveSkillIds = CreateDefaultActiveSkillIds(
                    knownIdOrder,
                    basicAttackSkillId,
                    nextCapacity);
                validActiveSkillIds = true;
            }
            else
            {
                nextActiveSkillIds = CreateRequestedActiveSkillIds(
                    requestedActiveSkillIds,
                    knownIds,
                    basicAttackSkillId,
                    nextCapacity,
                    out validActiveSkillIds);
            }

            if (!validActiveSkillIds)
            {
                return false;
            }

            BasicAttackSkillId = basicAttackSkillId;
            activeSkillIds.Clear();
            activeSkillIds.AddRange(nextActiveSkillIds);
            capacityModifiers.Clear();
            capacityModifiers.AddRange(nextCapacityModifiers);
            return true;
        }

        internal bool TryAddSkill(string skillId, IEnumerable<string> knownSkillIds)
        {
            var knownIds = CreateKnownSkillIdSet(knownSkillIds, out _);

            if (!IsInitialized
                || string.IsNullOrWhiteSpace(skillId)
                || knownIds == null
                || !knownIds.Contains(skillId)
                || activeSkillIds.Contains(skillId)
                || activeSkillIds.Count >= Capacity)
            {
                return false;
            }

            activeSkillIds.Add(skillId);
            return true;
        }

        internal bool TryRemoveSkill(string skillId)
        {
            if (!IsInitialized
                || string.Equals(
                    skillId,
                    BasicAttackSkillId,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            return activeSkillIds.Remove(skillId);
        }

        internal bool TryMoveSkill(string skillId, int targetIndex)
        {
            if (!IsInitialized
                || string.IsNullOrWhiteSpace(skillId)
                || targetIndex < 0
                || targetIndex >= activeSkillIds.Count)
            {
                return false;
            }

            var currentIndex = activeSkillIds.IndexOf(skillId);
            if (currentIndex < 0 || currentIndex == targetIndex)
            {
                return currentIndex == targetIndex;
            }

            activeSkillIds.RemoveAt(currentIndex);
            activeSkillIds.Insert(targetIndex, skillId);
            return true;
        }

        internal bool TrySetCapacityModifier(string sourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            var existing = capacityModifiers.Find(
                value => value != null
                    && string.Equals(
                        value.SourceId,
                        sourceId,
                        StringComparison.Ordinal));

            if (amount == 0)
            {
                return existing == null || capacityModifiers.Remove(existing);
            }

            var previousAmount = existing?.Amount ?? 0;
            var added = existing == null;

            if (added)
            {
                capacityModifiers.Add(new SkillLoadoutCapacityModifier
                {
                    SourceId = sourceId,
                    Amount = amount
                });
            }
            else
            {
                existing.Amount = amount;
            }

            if (activeSkillIds.Count > Capacity)
            {
                if (added)
                {
                    capacityModifiers.RemoveAt(capacityModifiers.Count - 1);
                }
                else
                {
                    existing.Amount = previousAmount;
                }

                return false;
            }

            return true;
        }

        public List<SkillData> ResolveSkills(IReadOnlyList<SkillData> knownSkills)
        {
            var resolvedSkills = new List<SkillData>();

            if (!IsInitialized || knownSkills == null)
            {
                return resolvedSkills;
            }

            foreach (var activeSkillId in activeSkillIds)
            {
                foreach (var knownSkill in knownSkills)
                {
                    if (knownSkill != null
                        && string.Equals(
                            knownSkill.SkillId,
                            activeSkillId,
                            StringComparison.Ordinal))
                    {
                        resolvedSkills.Add(knownSkill);
                        break;
                    }
                }
            }

            return resolvedSkills;
        }

        public List<string> CopyActiveSkillIds()
        {
            return new List<string>(activeSkillIds);
        }

        public List<SkillLoadoutCapacityModifier> CopyCapacityModifiers()
        {
            var result = new List<SkillLoadoutCapacityModifier>();

            foreach (var modifier in capacityModifiers)
            {
                if (modifier == null)
                {
                    continue;
                }

                result.Add(new SkillLoadoutCapacityModifier
                {
                    SourceId = modifier.SourceId,
                    Amount = modifier.Amount
                });
            }

            return result;
        }

        private static HashSet<string> CreateKnownSkillIdSet(
            IEnumerable<string> knownSkillIds,
            out List<string> knownIdOrder)
        {
            knownIdOrder = new List<string>();

            if (knownSkillIds == null)
            {
                return null;
            }

            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var knownSkillId in knownSkillIds)
            {
                if (string.IsNullOrWhiteSpace(knownSkillId)
                    || !knownIds.Add(knownSkillId))
                {
                    continue;
                }

                knownIdOrder.Add(knownSkillId);
            }

            return knownIds;
        }

        private static List<SkillLoadoutCapacityModifier> CreateCapacityModifiers(
            IEnumerable<SkillLoadoutCapacityModifier> requestedCapacityModifiers,
            out bool valid)
        {
            valid = true;
            var result = new List<SkillLoadoutCapacityModifier>();

            foreach (var modifier in requestedCapacityModifiers ?? new List<SkillLoadoutCapacityModifier>())
            {
                if (modifier == null
                    || string.IsNullOrWhiteSpace(modifier.SourceId)
                    || result.Exists(value => string.Equals(
                        value.SourceId,
                        modifier.SourceId,
                        StringComparison.Ordinal)))
                {
                    valid = false;
                    return result;
                }

                result.Add(new SkillLoadoutCapacityModifier
                {
                    SourceId = modifier.SourceId,
                    Amount = modifier.Amount
                });
            }

            return result;
        }

        private static List<string> CreateDefaultActiveSkillIds(
            IReadOnlyList<string> knownIdOrder,
            string basicAttackSkillId,
            int capacity)
        {
            var result = new List<string> { basicAttackSkillId };

            foreach (var knownId in knownIdOrder)
            {
                if (result.Count >= capacity)
                {
                    break;
                }

                if (!string.Equals(
                        knownId,
                        basicAttackSkillId,
                        StringComparison.Ordinal))
                {
                    result.Add(knownId);
                }
            }

            return result;
        }

        private static List<string> CreateRequestedActiveSkillIds(
            IEnumerable<string> requestedActiveSkillIds,
            HashSet<string> knownIds,
            string basicAttackSkillId,
            int capacity,
            out bool valid)
        {
            valid = true;
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var requestedSkillId in requestedActiveSkillIds)
            {
                if (string.IsNullOrWhiteSpace(requestedSkillId)
                    || !knownIds.Contains(requestedSkillId)
                    || !seen.Add(requestedSkillId))
                {
                    valid = false;
                    return result;
                }

                result.Add(requestedSkillId);
            }

            if (result.Count == 0
                || !seen.Contains(basicAttackSkillId)
                || result.Count > capacity)
            {
                valid = false;
            }

            return result;
        }
    }

    [Serializable]
    public sealed class SkillLoadoutCapacityModifier
    {
        public string SourceId = string.Empty;
        public int Amount;
    }
}
