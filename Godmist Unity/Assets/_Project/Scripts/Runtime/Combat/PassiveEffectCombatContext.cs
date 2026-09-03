using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Skills;

namespace Godmist.Runtime.Combat
{
    /// <summary>
    /// Immutable snapshot of combat facts used by passive conditions and triggers.
    /// It deliberately contains resolved values from the caller. For example,
    /// a range-dependent skill supplies the actual distance and AttackType instead
    /// of making the passive framework infer a skill's category.
    /// </summary>
    public sealed class PassiveEffectCombatContext
    {
        public CombatSession Session { get; }
        public BattleUnit Source { get; }
        public BattleUnit Target { get; }
        public string SkillId { get; }
        public SkillData Skill { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> TargetsByModeIndex { get; }
        public string ActionId { get; }
        public bool IsPassiveDuplicate { get; }
        public AttackType AttackType { get; }
        public AttackPosition AttackPosition { get; }
        public DamageType? DamageType { get; }
        public int Distance { get; }
        public int MovementCost { get; }
        public int MovementSteps { get; }
        public ResourceType ResourceType { get; }
        public int ResourceAmount { get; }
        public string StatusId { get; }
        public int StackCount { get; }
        public int TurnNumber { get; }
        public bool? Succeeded { get; }
        public bool? IsCritical { get; }
        public bool? WasDodged { get; }
        public bool? WasMissed { get; }
        public int HealthDamage { get; }
        public float EventValue { get; }
        public IReadOnlyCollection<string> TargetTags { get; }
        public bool? ReferencedEffectSucceeded { get; }
        public bool? ReferencedEffectCrit { get; }
        public bool? ReferencedEffectDodged { get; }

        public PassiveEffectCombatContext(
            CombatSession session = null,
            BattleUnit source = null,
            BattleUnit target = null,
            string skillId = null,
            SkillData skill = null,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex = null,
            string actionId = null,
            bool isPassiveDuplicate = false,
            AttackType attackType = AttackType.Unknown,
            AttackPosition attackPosition = AttackPosition.Unknown,
            DamageType? damageType = null,
            int distance = -1,
            int movementCost = 0,
            int movementSteps = 0,
            ResourceType resourceType = ResourceType.None,
            int resourceAmount = 0,
            string statusId = null,
            int stackCount = 1,
            int turnNumber = -1,
            bool? succeeded = null,
            bool? isCritical = null,
            bool? wasDodged = null,
            bool? wasMissed = null,
            int healthDamage = 0,
            float eventValue = 0f,
            IReadOnlyCollection<string> targetTags = null,
            bool? referencedEffectSucceeded = null,
            bool? referencedEffectCrit = null,
            bool? referencedEffectDodged = null)
        {
            Session = session;
            Source = source;
            Target = target;
            SkillId = skillId ?? string.Empty;
            Skill = skill;
            TargetsByModeIndex = targetsByModeIndex;
            ActionId = actionId ?? string.Empty;
            IsPassiveDuplicate = isPassiveDuplicate;
            AttackType = attackType;
            AttackPosition = attackPosition;
            DamageType = damageType;
            Distance = distance;
            MovementCost = movementCost;
            MovementSteps = movementSteps;
            ResourceType = resourceType;
            ResourceAmount = resourceAmount;
            StatusId = statusId ?? string.Empty;
            StackCount = Math.Max(1, stackCount);
            TurnNumber = turnNumber;
            Succeeded = succeeded;
            IsCritical = isCritical;
            WasDodged = wasDodged;
            WasMissed = wasMissed;
            HealthDamage = Math.Max(0, healthDamage);
            EventValue = eventValue;
            TargetTags = targetTags == null
                ? ResolveTargetTags(target)
                : new List<string>(targetTags).AsReadOnly();
            ReferencedEffectSucceeded = referencedEffectSucceeded;
            ReferencedEffectCrit = referencedEffectCrit;
            ReferencedEffectDodged = referencedEffectDodged;
        }

        public PassiveEffectCombatContext WithOutcome(
            bool? succeeded = null,
            bool? isCritical = null,
            bool? wasDodged = null,
            bool? wasMissed = null,
            int healthDamage = 0,
            float eventValue = 0f)
        {
            return new PassiveEffectCombatContext(
                Session,
                Source,
                Target,
                SkillId,
                Skill,
                TargetsByModeIndex,
                ActionId,
                IsPassiveDuplicate,
                AttackType,
                AttackPosition,
                DamageType,
                Distance,
                MovementCost,
                MovementSteps,
                ResourceType,
                ResourceAmount,
                StatusId,
                StackCount,
                TurnNumber,
                succeeded,
                isCritical,
                wasDodged,
                wasMissed,
                healthDamage,
                eventValue,
                TargetTags,
                ReferencedEffectSucceeded,
                ReferencedEffectCrit,
                ReferencedEffectDodged);
        }

        public PassiveEffectCombatContext AsPassiveDuplicate()
        {
            return new PassiveEffectCombatContext(
                Session,
                Source,
                Target,
                SkillId,
                Skill,
                TargetsByModeIndex,
                ActionId,
                isPassiveDuplicate: true,
                AttackType,
                AttackPosition,
                DamageType,
                Distance,
                MovementCost,
                MovementSteps,
                ResourceType,
                ResourceAmount,
                StatusId,
                StackCount,
                TurnNumber,
                Succeeded,
                IsCritical,
                WasDodged,
                WasMissed,
                HealthDamage,
                EventValue,
                TargetTags,
                ReferencedEffectSucceeded,
                ReferencedEffectCrit,
                ReferencedEffectDodged);
        }

        private static IReadOnlyCollection<string> ResolveTargetTags(BattleUnit target)
        {
            var tags = target?.Character?.Definition?.Tags;
            if (tags == null || tags.Count == 0)
                return Array.Empty<string>();

            var resolvedTags = new List<string>(tags.Count);
            foreach (var tag in tags)
                resolvedTags.Add(CharacterTagUtility.ToConditionReference(tag));

            return resolvedTags.AsReadOnly();
        }
    }
}
