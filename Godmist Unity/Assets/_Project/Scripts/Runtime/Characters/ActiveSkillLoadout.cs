using System;
using System.Collections.Generic;
using Godmist.Data.Skills;
using Godmist.Data.Configuration;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public sealed class ActiveSkillLoadout
    {
        public static int DefaultBaseCapacity => GameDefinesRuntime.GetInt(
            GameDefineIds.DefaultActiveSkillCapacity, 5);

        private readonly List<string> activeSkillIds = new();
        // Slot zero is always the basic attack. Remaining positions stay stable so the
        // UI can preserve intentional gaps in a character's loadout.
        private readonly List<string> activeSkillSlotIds = new();
        private readonly List<SkillLoadoutCapacityModifier> capacityModifiers = new();

        public IReadOnlyList<string> ActiveSkillIds => new List<string>(activeSkillIds);
        public IReadOnlyList<string> ActiveSkillSlotIds => new List<string>(activeSkillSlotIds);
        public int ActiveSkillCount => activeSkillIds.Count;
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
            RebuildSlotSkillIds(nextActiveSkillIds, nextCapacity);
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

            var targetIndex = FindFirstEmptyNonBasicSlot();
            if (targetIndex < 0)
            {
                return false;
            }

            activeSkillSlotIds[targetIndex] = skillId;
            RebuildActiveSkillIdsFromSlots();
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

            var slotIndex = activeSkillSlotIds.IndexOf(skillId);
            if (slotIndex <= 0)
            {
                return false;
            }

            activeSkillSlotIds[slotIndex] = string.Empty;
            RebuildActiveSkillIdsFromSlots();
            return true;
        }

        internal bool TryReplaceSkill(
            string targetSkillId,
            string replacementSkillId,
            IEnumerable<string> knownSkillIds)
        {
            var knownIds = CreateKnownSkillIdSet(knownSkillIds, out _);
            if (!IsInitialized
                || string.IsNullOrWhiteSpace(targetSkillId)
                || string.IsNullOrWhiteSpace(replacementSkillId)
                || knownIds == null
                || !knownIds.Contains(replacementSkillId)
                || activeSkillIds.Contains(replacementSkillId)
                || string.Equals(
                    targetSkillId,
                    BasicAttackSkillId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var targetIndex = activeSkillSlotIds.IndexOf(targetSkillId);
            if (targetIndex <= 0)
            {
                return false;
            }

            activeSkillSlotIds[targetIndex] = replacementSkillId;
            RebuildActiveSkillIdsFromSlots();
            return true;
        }

        internal bool TryAddSkillAtSlot(
            string skillId,
            int targetIndex,
            IEnumerable<string> knownSkillIds)
        {
            var knownIds = CreateKnownSkillIdSet(knownSkillIds, out _);
            if (!IsInitialized
                || string.IsNullOrWhiteSpace(skillId)
                || knownIds == null
                || !knownIds.Contains(skillId)
                || activeSkillIds.Contains(skillId)
                || targetIndex <= 0
                || targetIndex >= Capacity
                || !string.IsNullOrWhiteSpace(activeSkillSlotIds[targetIndex]))
            {
                return false;
            }

            activeSkillSlotIds[targetIndex] = skillId;
            RebuildActiveSkillIdsFromSlots();
            return true;
        }

        internal bool TryMoveSkill(string skillId, int targetIndex)
        {
            if (!IsInitialized
                || string.IsNullOrWhiteSpace(skillId)
                || string.Equals(
                    skillId,
                    BasicAttackSkillId,
                    StringComparison.Ordinal)
                || targetIndex == 0
                || targetIndex < 0
                || targetIndex >= activeSkillSlotIds.Count)
            {
                return false;
            }

            var currentIndex = activeSkillSlotIds.IndexOf(skillId);
            if (currentIndex < 0 || currentIndex == targetIndex)
            {
                return currentIndex == targetIndex;
            }

            var targetSkillId = activeSkillSlotIds[targetIndex];
            activeSkillSlotIds[targetIndex] = skillId;
            activeSkillSlotIds[currentIndex] = targetSkillId;
            RebuildActiveSkillIdsFromSlots();
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
                if (existing == null)
                {
                    return true;
                }

                var removedAmount = existing.Amount;
                var existingIndex = capacityModifiers.IndexOf(existing);
                capacityModifiers.RemoveAt(existingIndex);
                if (activeSkillIds.Count > Capacity || HasAssignedSkillOutsideCapacity())
                {
                    existing.Amount = removedAmount;
                    capacityModifiers.Insert(existingIndex, existing);
                    return false;
                }

                ResizeSlotsToCapacity();
                return true;
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

            if (activeSkillIds.Count > Capacity || HasAssignedSkillOutsideCapacity())
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

            ResizeSlotsToCapacity();
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

        public List<string> CopyActiveSkillSlotIds()
        {
            return new List<string>(activeSkillSlotIds);
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
                return result;
            }

            var basicIndex = result.IndexOf(basicAttackSkillId);
            if (basicIndex > 0)
            {
                result.RemoveAt(basicIndex);
                result.Insert(0, basicAttackSkillId);
            }

            return result;
        }

        internal bool TryInitializeSlots(
            IEnumerable<string> knownSkillIds,
            string basicAttackSkillId,
            IEnumerable<string> requestedActiveSkillSlotIds,
            IEnumerable<SkillLoadoutCapacityModifier> requestedCapacityModifiers = null)
        {
            var knownIds = CreateKnownSkillIdSet(knownSkillIds, out _);
            if (knownIds == null
                || string.IsNullOrWhiteSpace(basicAttackSkillId)
                || !knownIds.Contains(basicAttackSkillId))
            {
                return false;
            }

            var nextCapacityModifiers = CreateCapacityModifiers(requestedCapacityModifiers, out var validModifiers);
            if (!validModifiers)
            {
                return false;
            }

            var nextCapacity = Math.Max(1, BaseCapacity + SumCapacityModifiers(nextCapacityModifiers));
            var slots = new List<string>(requestedActiveSkillSlotIds ?? new List<string>());
            if (slots.Count == 0 || slots.Count > nextCapacity || !string.Equals(slots[0], basicAttackSkillId, StringComparison.Ordinal))
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slotSkillId in slots)
            {
                if (string.IsNullOrWhiteSpace(slotSkillId))
                {
                    continue;
                }

                if (!knownIds.Contains(slotSkillId) || !seen.Add(slotSkillId))
                {
                    return false;
                }
            }

            while (slots.Count < nextCapacity)
            {
                slots.Add(string.Empty);
            }

            BasicAttackSkillId = basicAttackSkillId;
            activeSkillSlotIds.Clear();
            activeSkillSlotIds.AddRange(slots);
            RebuildActiveSkillIdsFromSlots();
            capacityModifiers.Clear();
            capacityModifiers.AddRange(nextCapacityModifiers);
            return true;
        }

        private static int SumCapacityModifiers(IEnumerable<SkillLoadoutCapacityModifier> modifiers)
        {
            var total = 0;
            foreach (var modifier in modifiers)
            {
                if (modifier != null)
                {
                    total += modifier.Amount;
                }
            }

            return total;
        }

        private int FindFirstEmptyNonBasicSlot()
        {
            for (var index = 1; index < activeSkillSlotIds.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(activeSkillSlotIds[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        private void RebuildSlotSkillIds(IReadOnlyList<string> skillIds, int capacity)
        {
            activeSkillSlotIds.Clear();
            foreach (var skillId in skillIds)
            {
                activeSkillSlotIds.Add(skillId);
            }

            while (activeSkillSlotIds.Count < capacity)
            {
                activeSkillSlotIds.Add(string.Empty);
            }
        }

        private void RebuildActiveSkillIdsFromSlots()
        {
            activeSkillIds.Clear();
            foreach (var slotSkillId in activeSkillSlotIds)
            {
                if (!string.IsNullOrWhiteSpace(slotSkillId))
                {
                    activeSkillIds.Add(slotSkillId);
                }
            }
        }

        private void ResizeSlotsToCapacity()
        {
            var capacity = Capacity;
            while (activeSkillSlotIds.Count > capacity)
            {
                activeSkillSlotIds.RemoveAt(activeSkillSlotIds.Count - 1);
            }

            while (activeSkillSlotIds.Count < capacity)
            {
                activeSkillSlotIds.Add(string.Empty);
            }
        }

        private bool HasAssignedSkillOutsideCapacity()
        {
            for (var index = Capacity; index < activeSkillSlotIds.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(activeSkillSlotIds[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class SkillLoadoutCapacityModifier
    {
        public string SourceId = string.Empty;
        public int Amount;
    }
}
