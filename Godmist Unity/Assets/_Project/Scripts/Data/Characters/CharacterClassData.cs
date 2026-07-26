using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterClassData",
        menuName = "Godmist/Characters/Character Class Data")]
    public class CharacterClassData : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string ClassName { get; private set; } = string.Empty;
        [field: SerializeField] public ResourceType ResourceType { get; private set; } = ResourceType.None;
    }
}