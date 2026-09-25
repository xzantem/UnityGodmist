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
        [SerializeField] private GameObject skillCastBanner;
        [SerializeField] private TMPro.TextMeshProUGUI skillCastText;
        [SerializeField, Min(0.1f)] private float skillCastDuration = 1.6f;

        [SerializeField] private CombatMovementInputController movement;
        [SerializeField] private CombatTargetSelectionController targeting;
        [SerializeField] private CombatUnitInspectController inspect;
        [SerializeField] private CombatSkillBarView skills;
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private TMPro.TextMeshProUGUI targetName, targetHealth;
        private CombatTurnPortraitView timelineSource;
        private BattleUnit timelineUnit;
        private CombatFeedbackBus skillFeedbackBus;
        private float skillCastExpiresAt;
        private BattleUnit Visible(BattleUnit unit) => unit?.IsPlayerSide == true || sandboxBootstrap == null || sandboxBootstrap.IsUnitVisible(unit) ? unit : null;
        public BattleUnit HoveredUnit => Visible(timelineUnit ?? targeting?.HoveredUnit ?? inspect?.HoveredUnit);
        public BattleUnit HighlightedUnit => HoveredUnit ?? Visible(inspect?.InspectedUnit);
        public void SetTimelineHover(CombatTurnPortraitView source, BattleUnit unit)
        {
            if(unit == null && timelineSource != source)return;
            timelineSource=unit==null?null:source;timelineUnit=unit;
        }
        private void OnDisable()
        {
            BindSkillFeedback(null);
            HideSkillCast();
            timelineSource = null;
            timelineUnit = null;
            if (targetPanel != null) targetPanel.SetActive(false);
        }
        private readonly List<CombatTurnPortraitView> turnEntries = new();

        private void LateUpdate()
        {
            var session = sandboxBootstrap?.Session;
            BindSkillFeedback(session == null ? null
                : sandboxBootstrap.GetDriver(session)?.FeedbackBus);
            if (skillCastBanner != null && skillCastBanner.activeSelf
                && Time.unscaledTime >= skillCastExpiresAt)
                HideSkillCast();
            if (session == null)
            {
                HideSkillCast();
                SetVisible(false);
                return;
            }

            SetVisible(true);
            RefreshTurnOrder(session);
            var skill=skills?.HoveredSkill ?? targeting?.SelectedSkill;
            var unit=session.ActiveUnit;
            var cost=unit!=null && unit.IsPlayerSide
                ? skill!=null ? (skill.IsToggle && unit.ActiveToggleSkillIds.Contains(skill.SkillId) ? 0 : PaladinCombatSystem.PreviewHealCost(session,unit,skill,targeting?.HoveredUnit,CombatActionCostCalculator.CalculateSkillCost(unit,skill))) : movement?.PreviewCost??0
                : 0;
            activeUnitView?.Bind(Visible(unit), true, cost);
            var target=HoveredUnit;
            if(targetPanel!=null)targetPanel.SetActive(target?.Character!=null && session.Units.Contains(target));
            if(target?.Character!=null){if(targetName!=null)targetName.text=target.Character.Definition?.DisplayName??target.Character.CharacterId;if(targetHealth!=null)targetHealth.text=$"{target.Character.CurrentHealth}/{target.Character.Stats?.MaxHealth.GetValue():0} HP";}
        }

        private void BindSkillFeedback(CombatFeedbackBus nextBus)
        {
            if (skillFeedbackBus == nextBus) return;
            if (skillFeedbackBus != null)
                skillFeedbackBus.Published -= HandleSkillFeedback;
            skillFeedbackBus = nextBus;
            if (skillFeedbackBus != null)
                skillFeedbackBus.Published += HandleSkillFeedback;
            HideSkillCast();
        }

        private void HandleSkillFeedback(CombatFeedbackEvent feedback)
        {
            if (feedback.Type != CombatFeedbackType.SkillUsed
                || skillCastBanner == null || skillCastText == null
                || feedback.Target?.Character == null
                || sandboxBootstrap?.Session?.Units.Contains(feedback.Target) != true
                || !sandboxBootstrap.IsUnitVisible(feedback.Target)) return;

            if (string.IsNullOrWhiteSpace(feedback.EffectName)) return;
            skillCastText.text = feedback.EffectName;
            skillCastExpiresAt = Time.unscaledTime + skillCastDuration;
            skillCastBanner.SetActive(true);
        }

        private void HideSkillCast()
        {
            if (skillCastBanner != null) skillCastBanner.SetActive(false);
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

            var upcomingTurns = CombatTurnSystem.GetUpcomingTurns(
                session,
                displayedTurnCount);
            var count = upcomingTurns.Count;
            while (turnEntries.Count < count)
            {
                turnEntries.Add(Instantiate(turnOrderEntryPrefab, turnOrderContainer));
            }

            var occurrences = new Dictionary<BattleUnit, int>();
            var objectOccurrences = new Dictionary<SummonedCombatMapObject, int>();
            for (var index = 0; index < turnEntries.Count; index++)
            {
                var visible = index < count;
                turnEntries[index].gameObject.SetActive(visible);
                if (visible)
                {
                    var participant = upcomingTurns[index];
                    if (participant.IsUnit)
                    {
                        occurrences.TryGetValue(participant.Unit, out var occurrence);
                        occurrences[participant.Unit] = ++occurrence;
                        turnEntries[index].Bind(
                            participant.Unit,
                            index == 0 && participant.Unit == session.ActiveUnit,
                            occurrence, this, participant.Unit == HighlightedUnit,
                            sandboxBootstrap.IsUnitKnown(participant.Unit));
                    }
                    else if (participant.IsSummonedObject)
                    {
                        objectOccurrences.TryGetValue(participant.SummonedObject, out var occurrence);
                        objectOccurrences[participant.SummonedObject] = ++occurrence;
                        turnEntries[index].BindObject(participant.SummonedObject,
                            index == 0 && session.ActiveSummonedObject == participant.SummonedObject,
                            occurrence, this);
                    }
                }
            }
        }
    }
}
