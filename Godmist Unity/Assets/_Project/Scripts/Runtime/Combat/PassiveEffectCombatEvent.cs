using System;
using System.Collections.Generic;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    public sealed class PassiveEffectCombatEvent
    {
        public PassiveEffectTriggerType TriggerType { get; }
        public string EventId { get; }
        public PassiveEffectCombatContext Context { get; }
        public long Sequence { get; internal set; }

        public PassiveEffectCombatEvent(
            PassiveEffectTriggerType triggerType,
            PassiveEffectCombatContext context,
            string eventId = null)
        {
            TriggerType = triggerType;
            Context = context;
            EventId = eventId ?? string.Empty;
        }
    }

    /// <summary>
    /// Deterministic, re-entrancy-safe event boundary for future passive execution.
    /// Publishing an event never executes a passive by itself; subscribers decide
    /// whether and how to evaluate the current definitions.
    /// </summary>
    public sealed class PassiveEffectCombatEventDispatcher
    {
        private readonly Queue<PassiveEffectCombatEvent> pendingEvents = new();
        private bool isDispatching;
        private long nextSequence;

        public event Action<PassiveEffectCombatEvent> Published;

        public void Publish(
            PassiveEffectTriggerType triggerType,
            PassiveEffectCombatContext context,
            string eventId = null)
        {
            Publish(new PassiveEffectCombatEvent(triggerType, context, eventId));
        }

        public void Publish(PassiveEffectCombatEvent combatEvent)
        {
            if (combatEvent?.Context == null)
            {
                return;
            }

            combatEvent.Sequence = ++nextSequence;
            pendingEvents.Enqueue(combatEvent);
            if (isDispatching)
            {
                return;
            }

            isDispatching = true;
            try
            {
                while (pendingEvents.Count > 0)
                {
                    var nextEvent = pendingEvents.Dequeue();
                    Published?.Invoke(nextEvent);
                }
            }
            finally
            {
                isDispatching = false;
            }
        }
    }
}
