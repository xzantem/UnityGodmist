using System.Collections.Generic;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Combat
{
    [CreateAssetMenu(
        fileName = "EncounterDefinition",
        menuName = "Godmist/Combat/Encounter Definition")]
    public class EncounterDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public int GridWidth { get; private set; } = 8;
        [field: SerializeField] public int GridHeight { get; private set; } = 8;
        [field: SerializeField, Min(0)]
        public int BossEnemyIndex { get; private set; }
        [field: SerializeField] public List<EncounterEnemyData> Enemies { get; private set; } = new();

        private void OnValidate()
        {
            BossEnemyIndex = Mathf.Max(0, BossEnemyIndex);
        }
    }
}
