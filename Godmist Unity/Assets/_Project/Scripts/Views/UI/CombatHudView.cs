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

        private readonly List<CombatTurnPortraitView> turnEntries = new();

        private void Update()
        {
            var session = sandboxBootstrap?.Session;
            if (session == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            RefreshTurnOrder(session);
            activeUnitView?.Bind(session.ActiveUnit, true);
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

            for (var index = 0; index < turnEntries.Count; index++)
            {
                var visible = index < count;
                turnEntries[index].gameObject.SetActive(visible);
                if (visible)
                {
                    turnEntries[index].Bind(
                        upcomingTurns[index],
                        index == 0 && upcomingTurns[index] == session.ActiveUnit);
                }
            }
        }
    }
}
