using System;
using System.Linq;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class SummonedObjectSystem
    {
        private readonly Action<string> battleLog;
        private readonly Action<CombatMapObject> onMapObjectChanged;
        private readonly SummonedObjectEffectExecutor effectExecutor;

        public SummonedObjectSystem(
            Action<string> battleLog = null,
            Action<CombatMapObject> onMapObjectChanged = null,
            SummonedObjectEffectExecutor effectExecutor = null)
        {
            this.battleLog = battleLog;
            this.onMapObjectChanged = onMapObjectChanged;
            this.effectExecutor = effectExecutor
                ?? new SummonedObjectEffectExecutor(battleLog);
        }

        public void ExecuteTurnEffects(
            CombatSession session,
            SummonedCombatMapObject summonedObject)
        {
            effectExecutor?.ExecuteTurn(session, summonedObject);
        }

        public bool TryCreate(
            CombatSession session,
            BattleUnit creator,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex,
            GridPosition position,
            out SummonedCombatMapObject createdObject)
        {
            createdObject = null;

            if (session?.Grid == null
                || creator?.Character == null
                || skill == null
                || effect?.SummonedObjectDefinition == null)
            {
                return false;
            }

            if (!session.Grid.IsWalkable(position)
                || session.Grid.IsBlocked(position)
                || session.IsPositionOccupiedByMapObject(position))
            {
                return false;
            }

            RemoveOldestObjectsOverLimit(
                session,
                creator.Character.CharacterId,
                skill.SkillId,
                effectIndex,
                effect.MaxActiveObjects);

            var instanceNumber = session.SummonedObjects.Count + 1;

            var objectId =
                $"summoned.{creator.Character.CharacterId}." +
                $"{skill.SkillId}.{effectIndex}.{instanceNumber}";

            createdObject = new SummonedCombatMapObject(
                objectId,
                position,
                creator.Character.CharacterId,
                skill.SkillId,
                effectIndex,
                effect.SummonedObjectDefinition);

            session.SummonedObjects.Add(createdObject);
            session.MapObjects.Add(createdObject);

            // Aury statystyk i zmiennych zaczynają działać w chwili postawienia.
            effectExecutor?.ApplyPersistentEffects(session, createdObject);

            if (session.TurnParticipants != null
                && session.TurnParticipants.Count > 0)
            {
                createdObject.CurrentActionValue =
                    CombatTurnSystem.BaseActionValue /
                    createdObject.Definition.TurnSpeed;

                session.TurnParticipants.Add(
                    new CombatTurnParticipant(createdObject));
            }

            battleLog?.Invoke(
                $"{creator.Character.Definition.DisplayName} tworzy " +
                $"{createdObject.Definition.DisplayName}.");

            onMapObjectChanged?.Invoke(createdObject);

            return true;
        }

        public bool TryApplyHitsAtPosition(
            CombatSession session,
            GridPosition position,
            int hitCount)
        {
            if (session == null || hitCount <= 0)
            {
                return false;
            }

            var summonedObject = session.SummonedObjects.FirstOrDefault(
                item => item != null && item.Position == position);

            if (summonedObject == null)
            {
                return false;
            }

            var wasDestroyed = summonedObject.ApplyHits(hitCount);

            if (wasDestroyed)
            {
                Remove(session, summonedObject);
                return true;
            }

            onMapObjectChanged?.Invoke(summonedObject);

            return true;
        }

        public bool Remove(
            CombatSession session,
            SummonedCombatMapObject summonedObject)
        {
            if (session == null || summonedObject == null)
            {
                return false;
            }

            effectExecutor?.RemovePersistentEffects(session, summonedObject);

            var wasRemoved = session.SummonedObjects.Remove(summonedObject);

            session.SummonedObjectTurnOrder.Remove(summonedObject);
            session.TurnParticipants?.RemoveAll(
                participant => participant?.SummonedObject == summonedObject);
            session.MapObjects.Remove(summonedObject);

            if (ReferenceEquals(session.ActiveSummonedObject, summonedObject))
            {
                session.ActiveSummonedObject = null;
            }

            if (wasRemoved)
            {
                onMapObjectChanged?.Invoke(summonedObject);
            }

            return wasRemoved;
        }

        public int RemoveObjectsCreatedBy(
            CombatSession session,
            string creatorCharacterId)
        {
            if (session == null
                || string.IsNullOrWhiteSpace(creatorCharacterId))
            {
                return 0;
            }

            var objectsToRemove = session.SummonedObjects
                .Where(item => item != null
                    && item.CreatorCharacterId == creatorCharacterId)
                .ToList();

            foreach (var summonedObject in objectsToRemove)
            {
                Remove(session, summonedObject);
            }

            return objectsToRemove.Count;
        }

        private void RemoveOldestObjectsOverLimit(
            CombatSession session,
            string creatorCharacterId,
            string skillId,
            int effectIndex,
            int maxActiveObjects)
        {
            var limit = Math.Max(1, maxActiveObjects);

            var matchingObjects = session.SummonedObjects
                .Where(item => item != null
                    && item.CreatorCharacterId == creatorCharacterId
                    && item.SourceSkillId == skillId
                    && item.SourceEffectIndex == effectIndex)
                .ToList();

            while (matchingObjects.Count >= limit)
            {
                var oldestObject = matchingObjects[0];

                Remove(session, oldestObject);
                matchingObjects.RemoveAt(0);
            }
        }
    }
}
