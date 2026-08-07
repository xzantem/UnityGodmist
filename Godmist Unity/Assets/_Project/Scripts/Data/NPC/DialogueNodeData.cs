using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [Serializable]
    public class DialogueNodeData
    {
        [field: SerializeField] public string NodeId { get; private set; } = string.Empty;
        [field: SerializeField] public string SpeakerId { get; private set; } = string.Empty;

        [field: SerializeField, TextArea(3, 8)]
        public string Text { get; private set; } = string.Empty;

        [field: SerializeField]
        public List<DialogueChoiceData> Choices { get; private set; } = new();
    }
}