using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatHudView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Transform turnOrderContainer;
        [SerializeField] private CombatTurnPortraitView turnOrderEntryPrefab;
        [SerializeField, Min(1)] private int displayedTurnCount = 8;
        [SerializeField] private CombatActiveUnitView activeUnitView;

        [SerializeField] private CombatMovementInputController movement;
        [SerializeField] private CombatTargetSelectionController targeting;
        [SerializeField] private CombatUnitInspectController inspect;
        [SerializeField] private CombatSkillBarView skills;
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private TMPro.TextMeshProUGUI targetName, targetHealth;
        private CombatTurnPortraitView timelineSource;
        private BattleUnit timelineUnit;
        private BattleUnit Known(BattleUnit unit) => sandboxBootstrap == null || sandboxBootstrap.IsUnitKnown(unit) ? unit : null;
        public BattleUnit HoveredUnit => Known(timelineUnit ?? targeting?.HoveredUnit ?? inspect?.HoveredUnit);
        public BattleUnit HighlightedUnit => HoveredUnit ?? Known(inspect?.InspectedUnit);
        public void SetTimelineHover(CombatTurnPortraitView source, BattleUnit unit)
        {
            if(unit == null && timelineSource != source)return;
            timelineSource=unit==null?null:source;timelineUnit=unit;
        }
        private void OnDisable(){timelineSource=null;timelineUnit=null;if(targetPanel!=null)targetPanel.SetActive(false);}
        private readonly List<CombatTurnPortraitView> turnEntries = new();

        private void LateUpdate()
        {
            var session = sandboxBootstrap?.Session;
            if (session == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            RefreshTurnOrder(session);
            var skill=skills?.HoveredSkill ?? targeting?.SelectedSkill;
            var unit=session.ActiveUnit;
            var cost=unit!=null && unit.IsPlayerSide
                ? skill!=null ? (skill.IsToggle && unit.ActiveToggleSkillIds.Contains(skill.SkillId) ? 0 : CombatActionCostCalculator.CalculateSkillCost(unit,skill)) : movement?.PreviewCost??0
                : 0;
            activeUnitView?.Bind(Known(unit), true, cost);
            var target=HoveredUnit;
            if(targetPanel!=null)targetPanel.SetActive(target?.Character!=null && session.Units.Contains(target));
            if(target?.Character!=null){if(targetName!=null)targetName.text=target.Character.Definition?.DisplayName??target.Character.CharacterId;if(targetHealth!=null)targetHealth.text=$"{target.Character.CurrentHealth}/{target.Character.Stats?.MaxHealth.GetValue():0} HP";}
        }

        private void SetVisible(bool visible)
        {
            if (activeUnitView != null)
            {
                activeUnitView.gameObject.SetActive(visible);
            }

            if (visible)
            {
                return;
            }

            timelineUnit=null;timelineSource=null;
            if(targetPanel!=null)targetPanel.SetActive(false);
            foreach (var entry in turnEntries)
            {
                if (entry != null)
                {
                    entry.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshTurnOrder(CombatSession session)
        {
            if (turnOrderContainer == null || turnOrderEntryPrefab == null
                || session.TurnParticipants == null)
            {
                return;
            }

            var upcomingTurns = CombatTurnSystem.GetUpcomingUnitTurns(
                session,
                displayedTurnCount);
            var count = upcomingTurns.Count;
            while (turnEntries.Count < count)
            {
                turnEntries.Add(Instantiate(turnOrderEntryPrefab, turnOrderContainer));
            }

            var occurrences = new Dictionary<BattleUnit, int>();
            for (var index = 0; index < turnEntries.Count; index++)
            {
                var visible = index < count;
                turnEntries[index].gameObject.SetActive(visible);
                if (visible)
                {
                    occurrences.TryGetValue(upcomingTurns[index], out var occurrence);
                    occurrences[upcomingTurns[index]] = ++occurrence;
                    turnEntries[index].Bind(
                        upcomingTurns[index],
                        index == 0 && upcomingTurns[index] == session.ActiveUnit, occurrence, this, upcomingTurns[index]==HighlightedUnit,
                        sandboxBootstrap.IsUnitKnown(upcomingTurns[index]));
                }
            }
        }
    }
}
