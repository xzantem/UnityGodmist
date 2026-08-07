using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "NpcDefinition",
        menuName = "Godmist/NPC/NPC Definition")]
    public class NpcDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public Sprite Portrait { get; private set; }
        [field: SerializeField] public DialogueDefinition BaseDialogue { get; private set; }
    }
}