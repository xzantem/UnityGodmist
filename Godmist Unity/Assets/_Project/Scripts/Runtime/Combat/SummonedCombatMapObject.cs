using System;
using Godmist.Core.Enums;
using Godmist.Data.Combat;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class SummonedCombatMapObject : CombatMapObject
    {
        public override CombatMapObjectType ObjectType =>
            CombatMapObjectType.SummonedObject;

        public readonly string CreatorCharacterId;
        public readonly string SourceSkillId;
        public readonly int SourceEffectIndex;
        public readonly SummonedObjectDefinition Definition;

        public int RemainingHits;
        public int RemainingDurationTurns;
        public float CurrentActionValue;

        public override bool BlocksMovement =>
            Definition != null && Definition.BlocksMovement;

        public override bool BlocksLineOfSight =>
            Definition != null && Definition.BlocksLineOfSight;

        public SummonedCombatMapObject(
            string objectId,
            GridPosition position,
            string creatorCharacterId,
            string sourceSkillId,
            int sourceEffectIndex,
            SummonedObjectDefinition definition)
            : base(objectId, position)
        {
            CreatorCharacterId = creatorCharacterId ?? string.Empty;
            SourceSkillId = sourceSkillId ?? string.Empty;
            SourceEffectIndex = sourceEffectIndex;
            Definition = definition;

            RemainingHits = Math.Max(1, definition?.MaxHits ?? 1);

            RemainingDurationTurns = Math.Max(
                1,
                definition?.DefaultDurationTurns ?? 1);
        }

        public bool CanReceiveHit()
        {
            return Definition?.ObjectId != "paladin.judgement_field" && RemainingHits > 0;
        }

        public bool ApplyHits(int hitCount)
        {
            if (hitCount <= 0 || !CanReceiveHit())
            {
                return false;
            }

            RemainingHits = Math.Max(0, RemainingHits - hitCount);

            return RemainingHits <= 0;
        }

        public bool AdvanceDuration()
        {
            RemainingDurationTurns = Math.Max(
                0,
                RemainingDurationTurns - 1);

            return RemainingDurationTurns <= 0;
        }

        public bool IsExpired()
        {
            return RemainingHits <= 0 || RemainingDurationTurns <= 0;
        }

        public override bool CanInteract()
        {
            return false;
        }

        public override void Interact()
        {
        }
    }
}
