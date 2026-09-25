using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Core.Enums;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoPartyRailView : MonoBehaviour
    {
        [SerializeField] private Transform portraitRoot;
        [SerializeField] private CharacterInfoPartyPortraitView portraitTemplate;
        [SerializeField] private CharacterInfoWindowView characterInfoWindowPrefab;
        [SerializeField] private UIWorkspaceWindowManager workspaceManager;
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private PartyRailGroupView groupTemplate;
        [SerializeField] private PartyRailMenuView menuTemplate;
        [SerializeField] private CombatUnitInspectWindowView inspectTemplate;
        [SerializeField] private RectTransform dropZone;
        [SerializeField] private Image dragGhost, newGroupMarker;
        private readonly List<CharacterInfoPartyPortraitView> portraits = new();
        private readonly List<PartyRailGroupView> groupViews = new();
        private readonly Dictionary<string, CharacterInfoWindowView> windows = new();
        private GameSession session;
        private PartyRailMenuView menu;
        private CombatUnitInspectWindowView inspectWindow;
        private bool dirty;
        private CharacterInfoPartyPortraitView dragged;
        private string destinationMember;
        private int destinationIndex, newGroupIndex;
        private bool validDrop;

        private void Awake()
        {
            if (workspaceManager == null) workspaceManager = UIWorkspaceWindowManager.Instance;
            CancelDrag();
        }
        private void Update()
        {
            var next = Godmist.Views.Game.GameBootstrap.Instance?.Session;
            if (next != session)
            {
                Unsubscribe(); CancelDrag(); menu?.Hide(); session = next;
                if (session != null) { session.PartyRosterChanged += Refresh; session.PlayerParty.GroupsChanged += Refresh; }
                dirty = true;
            }
            if (dragged != null && Input.GetKeyDown(KeyCode.Escape)) CancelDrag();
            if (session != null && session.CurrentMode == GameModeState.Exploration
                && !DevConsoleView.IsOpen && !UIWorkspaceInputRouter.HasFocusedTextInput()
                && Input.GetKeyDown(KeyCode.I))
                ToggleActiveCharacterInfo();
            if (dirty && dragged == null) Rebuild();
            foreach (var portrait in portraits)
            {
                var combat = session?.GetCombatForCharacter(portrait.CharacterId);
                var unit = combat?.Units.FirstOrDefault(value => value.Character?.CharacterId == portrait.CharacterId);
                portrait.RefreshPresentation(unit, portrait.CharacterId == session?.PlayerParty.ActiveLeaderId,
                    unit != null && combat.ActiveUnit == unit);
            }
        }
        private void Unsubscribe()
        {
            if (session == null) return;
            session.PartyRosterChanged -= Refresh; session.PlayerParty.GroupsChanged -= Refresh;
        }
        private void OnDisable() { CancelDrag(); menu?.Hide(); }
        private void OnDestroy()
        {
            Unsubscribe();
            if (menu != null) Destroy(menu.gameObject);
            if (inspectWindow != null) Destroy(inspectWindow.gameObject);
            foreach (var window in windows.Values) if (window != null) Destroy(window.gameObject);
        }
        public void Refresh() => dirty = true;
        private void Rebuild()
        {
            dirty = false;
            foreach (var group in groupViews) if (group != null) { group.gameObject.SetActive(false); Destroy(group.gameObject); }
            groupViews.Clear(); portraits.Clear();
            if (session == null || portraitTemplate == null || portraitRoot == null || groupTemplate == null) return;
            for (var i = 0; i < session.PlayerParty.GroupCount; i++)
            {
                var group = Instantiate(groupTemplate, portraitRoot); group.gameObject.SetActive(true); group.Highlight(false);
                groupViews.Add(group);
                var members = session.PlayerParty.GetGroup(i);
                for (var j = 0; j < members.Count; j++)
                {
                    if (!session.CharactersById.TryGetValue(members[j], out var character)) continue;
                    var portrait = Instantiate(portraitTemplate, group.MemberRoot);
                    portrait.gameObject.SetActive(true);
                    portrait.Bind(character, value => Select(value.CharacterId));
                    portrait.ConfigureRail(this, j == 0, j == members.Count - 1);
                    portraits.Add(portrait);
                }
            }
        }
        private void Select(string id) { menu?.Hide(); partyController?.SelectMember(id); }
        public void Center(string id) => partyController?.CenterMember(id);
        public void ShowMenu(CharacterInfoPartyPortraitView portrait, Vector2 position)
        {
            if (session == null || dragged != null || menuTemplate == null || workspaceManager?.WorkspaceRoot == null) return;
            menu ??= Instantiate(menuTemplate, workspaceManager.WorkspaceRoot);
            var id = portrait.CharacterId;
            if (!session.CharactersById.TryGetValue(id, out var character)) return;
            var party = session.PlayerParty; var groupIndex = party.GetGroupIndex(id);
            if (groupIndex < 0) return;
            var actions = new List<(string, Action, bool)> {
                ("Odłącz", () => party.TryDetach(id), party.GetGroup(groupIndex).Count > 1)
            };
            for (var i = 0; i < party.GroupCount; i++)
            {
                if (i == groupIndex) continue;
                var target = party.GetGroup(i)[0];
                var name = session.CharactersById[target].Definition?.DisplayName ?? target;
                actions.Add(($"Dołącz do grupy: {name}", () => party.TryJoinGroup(id, target), true));
            }
            actions.Add(("Wycentruj", () => Center(id), partyController != null));
            actions.Add(("Inspektuj", () => Inspect(character), inspectTemplate != null));
            actions.Add(("Okno postaci", () => OpenCharacterInfo(character), characterInfoWindowPrefab != null));
            menu.Show(position, actions);
        }
        private void Inspect(CharacterInstance character)
        {
            if (workspaceManager?.WorkspaceRoot == null) return;
            inspectWindow ??= CombatUnitInspectWindowView.Create(workspaceManager.WorkspaceRoot, inspectTemplate);
            var unit = session.Combats.SelectMany(combat => combat.Units).FirstOrDefault(value => value.Character == character)
                ?? new BattleUnit { Character = character, IsPlayerSide = true };
            inspectWindow?.Show(unit, UIWorkspaceTooltipView.ActiveInstance);
        }
        public void BeginDrag(CharacterInfoPartyPortraitView portrait, PointerEventData e)
        {
            if (session == null) return;
            menu?.Hide(); dragged = portrait;
            if (dragGhost != null) { dragGhost.sprite = portrait.PortraitSprite; dragGhost.gameObject.SetActive(true); dragGhost.transform.SetAsLastSibling(); }
            Drag(e);
        }
        public void Drag(PointerEventData e)
        {
            if (dragged == null) return;
            foreach (var row in portraits) row.MarkInsertion(false, false);
            foreach (var group in groupViews) group.Highlight(false);
            if (newGroupMarker != null) newGroupMarker.enabled = false;
            validDrop = dropZone != null && RectTransformUtility.RectangleContainsScreenPoint(dropZone, e.position, e.pressEventCamera);
            destinationMember = null; destinationIndex = -1; newGroupIndex = groupViews.Count;
            MoveMarker(dragGhost, e, new Vector2(24, 0));
            if (!validDrop) return;
            for (var i = 0; i < groupViews.Count; i++)
            {
                var group = groupViews[i];
                var corners = new Vector3[4]; group.Rect.GetWorldCorners(corners);
                if (e.position.y > RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, corners[1]).y) { newGroupIndex = i; break; }
                if (!RectTransformUtility.RectangleContainsScreenPoint(group.Rect, e.position, e.pressEventCamera)) continue;
                var members = session.PlayerParty.GetGroup(i);
                destinationMember = members[0]; destinationIndex = members.Count; group.Highlight(true);
                foreach (var row in portraits.Where(row => members.Contains(row.CharacterId)))
                {
                    var center = RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, row.Rect.TransformPoint(row.Rect.rect.center));
                    if (e.position.y > center.y) { destinationIndex = members.ToList().IndexOf(row.CharacterId); row.MarkInsertion(true, false); break; }
                }
                if (destinationIndex == members.Count) portraits.First(row => row.CharacterId == members[members.Count - 1]).MarkInsertion(false, true);
                return;
            }
            if (newGroupMarker != null) { newGroupMarker.enabled = true; MoveMarker(newGroupMarker, e, Vector2.zero); }
        }
        private static void MoveMarker(Image marker, PointerEventData e, Vector2 offset)
        {
            if (marker == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)marker.transform.parent, e.position,
                e.pressEventCamera, out var local);
            marker.rectTransform.localPosition = new Vector3(local.x + offset.x, local.y + offset.y, 0);
        }
        public void EndDrag(PointerEventData e)
        {
            if (dragged == null) return;
            Drag(e); var id = dragged.CharacterId;
            if (validDrop && session != null)
            {
                if (destinationMember == null) session.PlayerParty.TryDetach(id, newGroupIndex);
                else session.PlayerParty.TryJoinGroup(id, destinationMember, destinationIndex);
            }
            CancelDrag();
        }
        public void CancelDrag()
        {
            dragged = null; validDrop = false;
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
            if (newGroupMarker != null) newGroupMarker.enabled = false;
            foreach (var row in portraits) if (row != null) row.MarkInsertion(false, false);
            foreach (var group in groupViews) if (group != null) group.Highlight(false);
        }
        private void OpenCharacterInfo(CharacterInstance character)
        {
            if (character == null || characterInfoWindowPrefab == null || session == null) return;
            if (windows.TryGetValue(character.CharacterId, out var existing) && existing != null)
            {
                existing.gameObject.SetActive(true); existing.Initialize(session, character.CharacterId); return;
            }
            var manager = workspaceManager ?? UIWorkspaceWindowManager.Instance;
            if (manager?.WorkspaceRoot == null) return;
            var previousWindow = manager.FocusedWindow;
            var view = Instantiate(characterInfoWindowPrefab, manager.WorkspaceRoot);
            windows[character.CharacterId] = view; view.Initialize(session, character.CharacterId);
            view.CampAction = (ownerId, itemId) => partyController != null ? partyController.TryCamp(ownerId, itemId) : "Brak aktywnej drużyny.";
            manager.CascadeAndFocus(view.GetComponent<UIWorkspaceWindow>(), previousWindow);
        }
        private void ToggleActiveCharacterInfo()
        {
            var leaderId = session?.PlayerParty.ActiveLeaderId;
            if (string.IsNullOrEmpty(leaderId)
                || !session.CharactersById.TryGetValue(leaderId, out var character)
                || session.IsCharacterInCombat(leaderId)) return;

            if (windows.TryGetValue(leaderId, out var existing)
                && existing != null && existing.gameObject.activeInHierarchy)
            {
                existing.GetComponent<UIWorkspaceWindow>()?.Close();
                return;
            }

            OpenCharacterInfo(character);
        }
    }
}
