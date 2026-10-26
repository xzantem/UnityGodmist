using System.Collections.Generic;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatStatusCollectionView : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerMoveHandler
    {
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
        [SerializeField] private Transform iconRoot;
        [SerializeField] private CombatStatusIconView iconTemplate;
        [SerializeField] private TextMeshProUGUI overflowText;
        [SerializeField, Min(1)] private int maximumVisibleIcons = 6;

        private readonly List<CombatStatusIconView> spawnedIcons = new();
        private BattleUnit boundUnit;
        private Godmist.Runtime.Characters.CharacterInstance boundCharacter;
        private float nextRefresh;
        private List<CombatStatusPresentation> currentStatuses = new();
        public void BindCharacter(Godmist.Runtime.Characters.CharacterInstance character)
        { boundUnit = null; boundCharacter = character; Refresh(); }
        private void Update()
        {
            if (boundCharacter == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data)
        {
            if (currentStatuses.Count <= maximumVisibleIcons || overflowText == null
                || !RectTransformUtility.RectangleContainsScreenPoint(overflowText.rectTransform, data.position, data.enterEventCamera))
            {
                tooltipView?.NotifySourceExit(this);
                return;
            }
            var lines = new List<string>();
            for (var i = maximumVisibleIcons; i < currentStatuses.Count; i++)
            {
                var entry = currentStatuses[i];
                lines.Add((string.IsNullOrWhiteSpace(entry.Effect.DisplayName) ? entry.Effect.EffectId : entry.Effect.DisplayName) +
                    (entry.ExplorationCharacter != null ? " · " + UIWorkspaceTooltipView.ExplorationStatusDuration(entry) : ""));
            }
            tooltipView?.ShowText("Pozostałe efekty", string.Join("\n", lines), this);
        }
        public void OnPointerMove(UnityEngine.EventSystems.PointerEventData data) => OnPointerEnter(data);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data) => tooltipView?.NotifySourceExit(this);
        private void OnDisable() => tooltipView?.NotifySourceExit(this);

        public void Bind(BattleUnit unit)
        {
            boundUnit = unit;
            boundCharacter = null;
            Refresh();
        }

        private void Refresh()
        {
            tooltipView ??= UIWorkspaceTooltipView.ActiveInstance;

            var effects = (boundCharacter ?? boundUnit?.Character)?.PassiveEffects;
            var statuses = CombatStatusPresentationFactory.Create(
                effects,
                boundUnit?.ActiveAuras,
                boundUnit?.OutgoingDamageRedirects,
                boundUnit);
            if (boundCharacter != null)
                for (var i = 0; i < statuses.Count; i++)
                {
                    var status = statuses[i];
                    statuses[i] = new CombatStatusPresentation(status.Effect, status.Kind, status.Priority,
                        explorationCharacter: boundCharacter, groupedEffects: status.Effects);
                }
            currentStatuses = statuses;
            var visibleCount = Mathf.Min(statuses.Count, maximumVisibleIcons);
            EnsureIconCount(visibleCount);

            for (var index = 0; index < spawnedIcons.Count; index++)
            {
                var visible = index < visibleCount;
                var iconView = spawnedIcons[index];
                iconView.gameObject.SetActive(visible);
                if (visible)
                {
                    var statusIcon = tooltipView?.GetStatusIcon(statuses[index]);
                    iconView.Bind(statuses[index], tooltipView, statusIcon);
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
