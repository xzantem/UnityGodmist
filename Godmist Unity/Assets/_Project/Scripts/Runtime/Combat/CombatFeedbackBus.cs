using System;

namespace Godmist.Runtime.Combat
{
    public class CombatFeedbackBus
    {
        public event Action<CombatFeedbackEvent> Published;

        public void Publish(CombatFeedbackEvent feedbackEvent)
        {
            if (feedbackEvent.Target != null
                || feedbackEvent.Type == CombatFeedbackType.MapObjectTriggered && feedbackEvent.MapObject != null)
            {
                Published?.Invoke(feedbackEvent);
            }
        }
    }
}
