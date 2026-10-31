using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.UI
{
    public sealed partial class ControllerInputView
    {
        [SerializeField] private PartyRailMenuView selectorMenu;
        [SerializeField] private CharacterInfoPartyRailView partyRail;
        [SerializeField] private QuestJournalPanelView journal;
        [SerializeField] private CombatActionButtonsView combatActions;
        private IInteractable selectedInteraction;
        private IInteractable pressedInteraction;
        [NonSerialized] private BattleUnit selectedWorldUnit;

        private Vector2 SelectorPosition => new(Screen.width * .5f, Screen.height * .65f);

        private void ShowSelector(string title, IReadOnlyList<PartyRailMenuView.Entry> entries)
        {
            if (selectorMenu == null || entries.Count == 0) return;
            precisePointer = false;
            selectorMenu.Show(SelectorPosition, entries, title);
            uiRequested = true;
            SetContext(true);
        }

        private void OpenPartySelector()
        {
            var session = party?.Session;
            if (session?.PlayerParty == null) return;
            var entries = new List<PartyRailMenuView.Entry>();
            foreach (var id in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(id, out var character)) continue;
                var captured = id;
                entries.Add(new(character.Definition?.DisplayName ?? id, () =>
                {
                    ReturnToWorld();
                    party.SelectMember(captured);
                }, !session.IsDefeated && character.LifeState == Godmist.Core.Enums.CharacterLifeState.Alive
                    && character.AvailabilityState == Godmist.Core.Enums.CharacterAvailabilityState.Available));
            }
            ShowSelector("Drużyna", entries);
        }

        private void OpenPanelSelector()
        {
            var entries = new List<PartyRailMenuView.Entry>();
            var session = party?.Session;
            var subjectId = combat?.Session?.ActiveUnit is { IsPlayerSide: true } actor
                ? actor.Character?.CharacterId : session?.PlayerParty?.ActiveLeaderId;
            var canDevelop = session != null && subjectId != null && !session.IsCharacterInCombat(subjectId);
            if (partyRail != null && session != null)
            {
                void Panel(string label, ControllerCharacterPanel panel, bool enabled = true)
                    => entries.Add(new(label, () => partyRail.OpenControllerCharacterPanel(panel, subjectId), enabled));
                Panel("Karta postaci i ekwipunek", ControllerCharacterPanel.Inventory);
                Panel("Statystyki i efekty", ControllerCharacterPanel.Stats);
                Panel("Drzewko umiejętności", ControllerCharacterPanel.Development, canDevelop);
                Panel("Zestaw umiejętności", ControllerCharacterPanel.Skills, canDevelop);
            }
            if (journal != null) entries.Add(new("Dziennik", journal.Open,
                session?.CurrentMode == Godmist.Core.Enums.GameModeState.Exploration));
            if (minimap != null) entries.Add(new("Mapa", () => { ReturnToWorld(); minimap.SetOverviewOpen(true); }));
            if (combat?.Session != null && combatActions != null)
            {
                entries.Add(new("Przedmioty", combatActions.OpenControllerItems, combatActions.CanOpenControllerItems));
                entries.Add(new("Ucieczka", () => { ReturnToWorld(); combatActions.ControllerEscape(); }, combatActions.CanControllerEscape));
            }
            // Existing windows retain their own state, scope and close behavior.
            foreach (var state in scopes.OrderBy(value => value.OpeningOrder).ToArray())
            {
                var window = state.Scope != null ? state.Scope.GetComponent<UIWorkspaceWindow>() : null;
                if (window == null || state.Scope.IsModal) continue;
                var label = window.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault()?.text;
                entries.Add(new("Otwarte: " + (string.IsNullOrWhiteSpace(label) ? "okno" : label), window.Focus));
            }
            ShowSelector("Panele", entries);
        }

        private void OpenInteractionSelector()
        {
            var interactions = party?.GetControllerInteractions();
            if (interactions == null) return;
            var entries = interactions.Select(value => new PartyRailMenuView.Entry(
                InteractionName(value), () =>
                {
                    ReturnToWorld();
                    if (value is Component target && target != null && value.CanInteract()) value.Interact();
                })).ToArray();
            ShowSelector("W pobliżu", entries);
        }

        private static string InteractionName(IInteractable value) => value switch
        {
            Godmist.Views.NPC.NpcView npc => npc.DisplayName,
            Godmist.Views.Dungeons.DungeonDoorView => "Drzwi",
            Godmist.Views.Dungeons.DungeonChestView => "Skrzynia",
            Godmist.Views.Dungeons.DungeonEntranceView => "Wejście do lochu",
            Godmist.Views.Dungeons.DungeonExitView => "Wyjście z lochu",
            Godmist.Views.Dungeons.DungeonLeverView => "Dźwignia",
            Component component => component.name.Replace("(Clone)", string.Empty),
            _ => "Interakcja"
        };

        private void CycleWorldTarget(int direction)
        {
            if (combat?.Session == null)
            {
                var interactions = party?.GetControllerInteractions();
                if (interactions == null || interactions.Count == 0) { selectedInteraction = null; return; }
                var index = interactions.ToList().IndexOf(selectedInteraction);
                index = index < 0 ? (direction > 0 ? 0 : interactions.Count - 1)
                    : (index + direction + interactions.Count) % interactions.Count;
                selectedInteraction = interactions[index];
                if (selectedInteraction is Component target) position = worldCamera.WorldToScreenPoint(target.transform.position);
                return;
            }
            if (worldCamera == null || unitViews == null) return;
            var units = combat.Session.Units.Where(unit => unit != null && unitViews.IsUnitKnown(unit)
                    && unitViews.IsUnitVisible(unit) && (targeting?.IsSelectingTarget != true || targeting.IsControllerTarget(unit)))
                .OrderBy(unit => unit.Position.X).ThenBy(unit => unit.Position.Y)
                .ThenBy(unit => unit.Character?.CharacterId, StringComparer.Ordinal).ToArray();
            if (units.Length == 0) return;
            var current = Array.IndexOf(units, selectedWorldUnit);
            var next = current < 0 ? (direction > 0 ? 0 : units.Length - 1) : (current + direction + units.Length) % units.Length;
            selectedWorldUnit = units[next];
            if (unitViews.TryGetWorldPosition(selectedWorldUnit, out var point)) position = worldCamera.WorldToScreenPoint(point);
        }
    }
}
