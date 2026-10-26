using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    public partial class CombatHudView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Transform turnOrderContainer;
        [SerializeField] private CombatTurnPortraitView turnOrderEntryPrefab;
        [SerializeField, Min(1)] private int displayedTurnCount = 8;
        [SerializeField] private CombatActiveUnitView activeUnitView;
        [SerializeField] private GameObject skillCastBanner;
        [SerializeField] private TMPro.TextMeshProUGUI skillCastText;
        [SerializeField] private UnityEngine.UI.Image skillCastIcon;
        [SerializeField] private CanvasGroup skillCastOpacity;
        [SerializeField, Min(0.1f)] private float skillCastDuration = 1.4f;
        private const float SkillCastFadeSeconds = .25f;

        [SerializeField] private CombatMovementInputController movement;
        [SerializeField] private CombatTargetSelectionController targeting;
        [SerializeField] private CombatUnitInspectController inspect;
        [SerializeField] private CombatSkillBarView skills;
        // Retained for existing serialized HUDs; unit details now belong to
        // the shared rich tooltip, not this retired name/HP panel.
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private TMPro.TextMeshProUGUI targetName, targetHealth;
        [SerializeField] private CombatTargetPreviewView targetPreviewTemplate;
        private void Awake()
        {
            targeting?.SetPreviewTemplate(targetPreviewTemplate);
            if (targetPanel != null) targetPanel.SetActive(false);
        }
        private readonly Vector3[] bannerCorners = new Vector3[4];
        private void OnEnable()
        {
            RefreshSkillFeedbackBinding();
            if (sandboxBootstrap?.CameraFramer != null)
                sandboxBootstrap.CameraFramer.ActionSafeViewport = ActionSafeViewport;

            var canvas = GetComponentInParent<Canvas>();
            ContextualInputHintsView.Ensure(canvas)?.BindCombat(
                sandboxBootstrap,
                targeting,
                movement,
                sandboxBootstrap?.CameraFramer);
        }

        private void PositionSkillCastBanner()
        {
            if (skillCastBanner == null || skillCastBanner.transform is not RectTransform banner
                || banner.parent is not RectTransform parent) return;
            var top = parent.rect.yMax - 32f;
            if (turnOrderContainer is RectTransform timeline)
            {
                timeline.GetWorldCorners(bannerCorners);
                var bottom = float.PositiveInfinity;
                foreach (var corner in bannerCorners) bottom = Mathf.Min(bottom, parent.InverseTransformPoint(corner).y);
                top = Mathf.Min(top, bottom - 28f);
            }
            var width = Mathf.Min(skillCastContentWidth, Mathf.Max(1, parent.rect.width - 24f));
            if (!Mathf.Approximately(banner.rect.width, width)) banner.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            banner.localPosition = new Vector3(parent.rect.center.x + (banner.pivot.x - .5f) * banner.rect.width,
                top - (1 - banner.pivot.y) * banner.rect.height, banner.localPosition.z);
        }

        private Rect ActionSafeViewport(Camera camera)
        {
            PositionSkillCastBanner();
            if (skillCastBanner == null || skillCastBanner.transform is not RectTransform banner) return new Rect(0, 0, 1, 1);
            banner.GetWorldCorners(bannerCorners);
            var canvas = banner.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var bottom = float.PositiveInfinity;
            foreach (var corner in bannerCorners) bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(uiCamera, corner).y);
            var area = camera.pixelRect;
            return new Rect(0, 0, 1, Mathf.Clamp((bottom - area.yMin - 16f) / Mathf.Max(1, area.height), .1f, 1));
        }
        public CombatTargetSelectionController Targeting => targeting;
        public BattleUnit KnifeHighlightOwner => isActiveAndEnabled ? activeUnitView?.KnifeHighlightOwner : null;
        private CombatTurnPortraitView timelineSource;
        private BattleUnit timelineUnit;
        private CombatFeedbackBus skillFeedbackBus;
        private float skillCastExpiresAt;
        private float skillCastContentWidth = 240f;
        private BattleUnit Visible(BattleUnit unit) => unit?.IsPlayerSide == true || sandboxBootstrap == null || sandboxBootstrap.IsUnitVisible(unit) ? unit : null;
        public BattleUnit HoveredUnit => Visible(timelineUnit ?? targeting?.HoveredUnit ?? inspect?.HoveredUnit);
        public BattleUnit HighlightedUnit => HoveredUnit ?? Visible(inspect?.InspectedUnit);
        public void SetTimelineHover(CombatTurnPortraitView source, BattleUnit unit)
        {
            if(unit == null && timelineSource != source)return;
            timelineSource=unit==null?null:source;timelineUnit=unit;
        }

        public void HandleTurnPortraitClick(CombatTurnPortraitView source, PointerEventData eventData)
        {
            if (source == null || eventData == null) return;
            var unit = source.BoundInputUnit;
            if (!CanInteractWithTimelineUnit(unit)) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                inspect?.ShowTurnOrderContextMenu(unit, eventData.position);
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
                sandboxBootstrap?.CameraFramer?.FocusUnit(unit);
        }

        private bool CanInteractWithTimelineUnit(BattleUnit unit)
        {
            var session = sandboxBootstrap?.Session;
            return unit?.Character != null
                && unit.Character.LifeState != Godmist.Core.Enums.CharacterLifeState.Dead
                && session != null
                && session.Units.Contains(unit)
                && sandboxBootstrap.IsUnitKnown(unit)
                && sandboxBootstrap.IsUnitVisible(unit);
        }
        private void OnDisable()
        {
            if (sandboxBootstrap?.CameraFramer != null
                && sandboxBootstrap.CameraFramer.ActionSafeViewport == ActionSafeViewport)
                sandboxBootstrap.CameraFramer.ActionSafeViewport = null;
            BindSkillFeedback(null);
            HideSkillCast();
            timelineSource = null;
            timelineUnit = null;
            if (targetPanel != null) targetPanel.SetActive(false);
        }
        private readonly List<CombatTurnPortraitView> turnEntries = new();
        private CombatEdgeDrawer timelineDrawer;

        private static readonly Unity.Profiling.ProfilerMarker HudLateSample = new("Godmist.UI.CombatHudLate");
        private void LateUpdate()
        {
            using var sample = HudLateSample.Auto();
            var session = sandboxBootstrap?.Session;
            timelineDrawer ??= turnOrderContainer?.GetComponentInParent<CombatEdgeDrawer>(true);
            var showTimeline = session != null && !sandboxBootstrap.IsOpeningPreview;
            timelineDrawer?.SetAvailable(showTimeline);
            // The drawer owns its outer frame; the HUD also owns this inner content.
            // Restore both when returning from exploration or an opening preview.
            if (turnOrderContainer != null && turnOrderContainer.gameObject.activeSelf != showTimeline)
                turnOrderContainer.gameObject.SetActive(showTimeline);
            RefreshSkillFeedbackBinding();
            TickSkillCast(Time.unscaledTime);
            if (session == null)
            {
                HideSkillCast();
                var actor = skills?.DisplayActor;
                SetVisible(actor != null);
                activeUnitView?.Bind(actor, false, 0, known: actor != null);
                if (targetPanel != null) targetPanel.SetActive(false);
                return;
            }

            SetVisible(true);
            RefreshTurnOrder(session);
            var skill=skills?.HoveredSkill ?? targeting?.SelectedSkill;
            var unit=session.ActiveUnit;
            var recoveringKnife = targeting?.SelectedSkill == skill && targeting?.HoveredUnit == null
                && ScoutCombatSystem.RecoverableKnife(session, unit, skill, targeting?.HoveredPosition) != null;
            var cost=unit!=null && unit.IsPlayerSide
                ? skill!=null ? (skill.IsToggle && unit.ActiveToggleSkillIds.Contains(skill.SkillId) ? 0 : PaladinCombatSystem.PreviewHealCost(session,unit,skill,targeting?.HoveredUnit,CombatActionCostCalculator.CalculateSkillCost(unit,skill,recoveringKnife))) : movement?.PreviewCost??0
                : 0;
            activeUnitView?.Bind(unit, true, cost, known: Visible(unit) != null);
            if (targetPanel != null) targetPanel.SetActive(false);
        }

        public void RefreshSkillFeedbackBinding()
        {
            var session = sandboxBootstrap?.Session;
            BindSkillFeedback(session == null ? null : sandboxBootstrap.GetDriver(session)?.FeedbackBus);
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
            var skill = feedback.SkillPresentation?.Skill;
            skillCastText.text = string.IsNullOrWhiteSpace(skill?.DisplayName) ? feedback.EffectName : skill.DisplayName;
            if (skillCastIcon != null)
            {
                skillCastIcon.sprite = skill?.Icon;
                skillCastIcon.gameObject.SetActive(skillCastIcon.sprite != null);
            }
            if (skillCastOpacity != null) skillCastOpacity.alpha = 1;
            skillCastText.fontSize = skillCastText.fontSizeMax;
            skillCastContentWidth = Mathf.Clamp(skillCastText.GetPreferredValues(skillCastText.text).x
                + (skillCastIcon != null && skillCastIcon.gameObject.activeSelf ? 56f : 0f) + 32f, 180f, 600f);
            skillCastExpiresAt = Time.unscaledTime + skillCastDuration;
            PositionSkillCastBanner();
            skillCastBanner.SetActive(true);
        }

        private void TickSkillCast(float now)
        {
            if (skillCastBanner == null || !skillCastBanner.activeSelf) return;
            var remaining = skillCastExpiresAt - now;
            if (remaining <= 0) HideSkillCast();
            else if (skillCastOpacity != null)
            {
                var alpha = Mathf.Clamp01(remaining / Mathf.Min(SkillCastFadeSeconds, skillCastDuration));
                if (!Mathf.Approximately(skillCastOpacity.alpha, alpha)) skillCastOpacity.alpha = alpha;
            }
        }

        private void HideSkillCast()
        {
            if (skillCastBanner != null) skillCastBanner.SetActive(false);
            if (skillCastOpacity != null && skillCastOpacity.alpha != 0) skillCastOpacity.alpha = 0;
            if (skillCastIcon != null) skillCastIcon.sprite = null;
            if (skillCastText != null) skillCastText.text = string.Empty;
            skillCastExpiresAt = 0;
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

            if (sandboxBootstrap.IsOpeningPreview)
            {
                foreach (var entry in turnEntries) entry.gameObject.SetActive(false);
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
