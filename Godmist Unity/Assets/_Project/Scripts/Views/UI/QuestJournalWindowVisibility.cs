using System;
using UnityEngine;

namespace Godmist.Views.UI
{
    /// <summary>Notifies the journal owner when X, Escape, or a scene closes its window.</summary>
    public sealed class QuestJournalWindowVisibility : MonoBehaviour
    {
        public event Action Hidden;
        private void OnDisable() => Hidden?.Invoke();
    }
}
