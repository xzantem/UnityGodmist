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

        private void ShowWheel(string title, IReadOnlyList<ControllerWheelView.Entry> entries, string preferred = null, Action back = null)
        {
            if (selectorMenu == null || entries.Count == 0) return;
            selectorMenu.Hide(); actionWheelActor = null;
            if (wheel == null)
            {
                wheel = selectorMenu.CreateControllerWheel();
                wheel.SelectionChanged = () => { if (skillBar != null) skillBar.DismissControllerSkillInspection(wheel); };
            }
            precisePointer = false;
            wheel.Show(title, entries, preferred, back ?? (() => { wheel.Hide(); ReturnToWorld(); }));
            uiRequested = true; SetContext(true);
        }

        private void OpenPartySelector()
        {
            var session = party?.Session;
            if (session?.PlayerParty == null) return;
            var entries = new List<ControllerWheelView.Entry>();
            foreach (var id in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(id, out var character)) continue;
                var captured = id;
                entries.Add(new(id, character.Definition?.DisplayName ?? id, () =>
                { panelSubject = captured; OpenPanelSelector(); },icon:character.Definition?.UiPortraitSprite));
            }
            var current = (combat?.Session?.ActiveUnit is { IsPlayerSide: true } actor
                ? actor.Character?.CharacterId : session.PlayerParty.ActiveLeaderId);
            ShowWheel("Drużyna", entries, current);
        }

        private void OpenPanelSelector()
        {
            var entries = new List<ControllerWheelView.Entry>();
            var session = party?.Session;
            var subjectId = panelSubject ?? session?.PlayerParty?.ActiveLeaderId;
            var canDevelop = session != null && subjectId != null && !session.IsCharacterInCombat(subjectId);
            if (partyRail != null && session != null)
            {
                void Panel(string label, ControllerCharacterPanel panel, bool enabled = true)
                    => entries.Add(new(panel.ToString(), label, () =>
                    {
                        lastPanel = panel.ToString(); ReturnToWorld();
                        partyRail.OpenControllerCharacterPanel(panel, subjectId);
                    }, () => enabled, enabled ? null : "Postać uczestniczy w walce."));
                Panel("Ekwipunek", ControllerCharacterPanel.Inventory);
                Panel("Statystyki i efekty", ControllerCharacterPanel.Stats);
                Panel("Drzewko umiejętności", ControllerCharacterPanel.Development, canDevelop);
                Panel("Zestaw umiejętności", ControllerCharacterPanel.Skills, canDevelop);
            }
            if (combat?.Session == null && subjectId != null)
                entries.Add(new("control", "Steruj postacią", () => { ReturnToWorld(); party.SelectMember(subjectId); }));
            if (journal != null) entries.Add(new("journal", "Dziennik", () => { ReturnToWorld(); journal.Open(); },
                () => session?.CurrentMode == Godmist.Core.Enums.GameModeState.Exploration, "Dostępny podczas eksploracji."));
            if (minimap != null) entries.Add(new("map", "Mapa", () => { ReturnToWorld(); minimap.SetOverviewOpen(true); }));
            var name = session != null && subjectId != null && session.CharactersById.TryGetValue(subjectId, out var character)
                ? character.Definition?.DisplayName : "Postać";
            ShowWheel(name, entries, lastPanel, OpenPartySelector);
        }

        private void OpenActionWheel()
        {
            var entries = new List<ControllerWheelView.Entry>();
            var actor = skillBar?.DisplayActor;
            var actorId = actor?.Character?.CharacterId;
            if (skillBar != null)
                foreach (var slot in skillBar.ControllerSlots)
                {
                    if (slot == null || !slot.isActiveAndEnabled || slot.ControllerSkill == null) continue;
                    var captured = slot; var skill = slot.ControllerSkill;
                    entries.Add(new(skill.SkillId, skill.DisplayName, () =>
                    {
                        if (captured.TryActivate() && actorId != null) lastSkills[actorId] = skill.SkillId;
                        ReturnToWorld();
                    }, () => captured != null && captured.isActiveAndEnabled && captured.ControllerSkill == skill
                        && skillBar.DisplayActor?.Character?.CharacterId == actorId && captured.ControllerAvailable,
                    captured.ControllerUnavailableReason, skill.Icon, () => skillBar.InspectControllerSkill(captured, wheel)));
                }
            if (combat?.Session != null && combatActions != null)
            {
                entries.Add(new("items", "Przedmioty", () => { ReturnToWorld(); combatActions.OpenControllerItems(); },
                    () => combatActions.CanOpenControllerItems, "Przedmioty są teraz niedostępne."));
                entries.Add(new("escape", "Ucieczka", () => { ReturnToWorld(); combatActions.ControllerEscape(); },
                    () => combatActions.CanControllerEscape, "Nie można teraz uciec."));
            }
            ShowWheel("Akcje", entries, actorId != null ? lastSkills.GetValueOrDefault(actorId) : null);
            actionWheelActor = actorId;
        }

        private void CyclePartyMember(int direction)
        {
            var session = party?.Session;
            if (session?.PlayerParty == null || !party.CanCycleLeader) return;
            var members = session.PlayerParty.ActiveMemberIds.ToArray();
            var index = Array.IndexOf(members, session.PlayerParty.ActiveLeaderId);
            for (var step = 1; step < members.Length; step++)
            {
                var next = (index + direction * step + members.Length) % members.Length;
                if (party.SelectMember(members[next])) return;
            }
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
                // Stable reading order follows the visible arrangement, rather than map axes.
                .OrderByDescending(unit => unitViews.TryGetWorldPosition(unit, out var p) ? worldCamera.WorldToScreenPoint(p).y : 0)
                .ThenBy(unit => unitViews.TryGetWorldPosition(unit, out var p) ? worldCamera.WorldToScreenPoint(p).x : 0)
                .ThenBy(unit => unit.Character?.CharacterId, StringComparer.Ordinal).ToArray();
            if (units.Length == 0) return;
            var current = Array.IndexOf(units, selectedWorldUnit);
            var next = current < 0 ? (direction > 0 ? 0 : units.Length - 1) : (current + direction + units.Length) % units.Length;
            selectedWorldUnit = units[next];
            if (unitViews.TryGetWorldPosition(selectedWorldUnit, out var point)) { worldSelection = point; hasWorldSelection = true; position = worldCamera.WorldToScreenPoint(point); }
        }
    }
}
