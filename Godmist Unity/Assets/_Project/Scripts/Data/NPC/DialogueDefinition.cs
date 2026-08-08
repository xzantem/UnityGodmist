using System.Collections.Generic;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "DialogueDefinition",
        menuName = "Godmist/NPC/Dialogue Definition")]
    public class DialogueDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string StartNodeId { get; private set; } = string.Empty;

        [field: SerializeField]
        public List<DialogueNodeData> Nodes { get; private set; } = new();

        public DialogueNodeData FindNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || Nodes == null)
            {
                return null;
            }

            foreach (var node in Nodes)
            {
                if (node != null && node.NodeId == nodeId)
                {
                    return node;
                }
            }

            return null;
        }
    }
}