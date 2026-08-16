using System.Collections.Generic;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatStatusCollectionView : MonoBehaviour
    {
        [SerializeField] private CombatTooltipView tooltipView;
        [SerializeField] private Transform iconRoot;
        [SerializeField] private CombatStatusIconView iconTemplate;
        [SerializeField] private TextMeshProUGUI overflowText;
        [SerializeField, Min(1)] private int maximumVisibleIcons = 6;

        private readonly List<CombatStatusIconView> spawnedIcons = new();
        private BattleUnit boundUnit;

        public void Bind(BattleUnit unit)
        {
            boundUnit = unit;
            Refresh();
        }

        private void Refresh()
        {
            tooltipView ??= CombatTooltipView.ActiveInstance
                ?? Object.FindAnyObjectByType<CombatTooltipView>(
                    FindObjectsInactive.Include);

            var effects = boundUnit?.Character?.PassiveEffects;
            var statuses = CombatStatusPresentationFactory.Create(effects);
            var visibleCount = Mathf.Min(statuses.Count, maximumVisibleIcons);
            EnsureIconCount(visibleCount);

            for (var index = 0; index < spawnedIcons.Count; index++)
            {
                var visible = index < visibleCount;
                var iconView = spawnedIcons[index];
                iconView.gameObject.SetActive(visible);
                if (visible)
                {
                    iconView.Bind(statuses[index], tooltipView);
                }
            }

            var overflow = Mathf.Max(0, statuses.Count - maximumVisibleIcons);
            if (overflowText != null)
            {
                overflowText.gameObject.SetActive(overflow > 0);
                overflowText.text = overflow > 0 ? $"+{overflow}" : string.Empty;
                overflowText.transform.SetAsLastSibling();
            }
        }

        private void EnsureIconCount(int count)
        {
            if (iconRoot == null || iconTemplate == null) return;
            iconTemplate.gameObject.SetActive(false);
            while (spawnedIcons.Count < count)
            {
                var instance = Instantiate(iconTemplate, iconRoot);
                spawnedIcons.Add(instance);
            }
        }
    }
}
